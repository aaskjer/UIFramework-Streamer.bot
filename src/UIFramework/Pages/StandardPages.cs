using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Run = System.Windows.Documents.Run;

namespace UIFramework
{
    /// <summary>
    /// The pages every host gets for free. Each builder takes the page context and returns the page's root, or
    /// null when the spec has nothing for it (no messages, no log), in which case the shell leaves the page out.
    /// </summary>
    public static class StandardPages
    {
        public static PageSpec About(string title = "About", string glyph = "ℹ️") => new PageSpec("about", title, glyph, BuildAbout, bottom: true);
        public static PageSpec Help(string title = "Help", string glyph = "📖") => new PageSpec("help", title, glyph, BuildHelp);
        public static PageSpec Log(string title = "Log", string glyph = "📜") => new PageSpec("log", title, glyph, BuildLog);
        public static PageSpec Messages(string title = "Messages", string glyph = "💬") => new PageSpec("messages", title, glyph, BuildMessages);

        // ---- About ----------------------------------------------------------------------------------------

        public static UIElement BuildAbout(PageContext ctx)
        {
            var spec = ctx.Spec;
            var page = Ui.PageStack();

            var logo = new Image { Width = 72, Height = 72, Margin = new Thickness(0, 0, 14, 0) };
            Web.LoadImageAsync(logo, spec.LogoUrl);
            var names = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            names.Children.Add(new TextBlock { Text = spec.Title, FontSize = 17, FontWeight = FontWeights.SemiBold, Foreground = Theme.LabelFg });
            names.Children.Add(new TextBlock
            {
                Text = "Version " + (spec.Version ?? "?") + "  ·  UIFramework " + Entry.FrameworkVersion,
                FontSize = 12,
                Foreground = Theme.SubFg,
                Margin = new Thickness(0, 2, 0, 0)
            });
            if (!string.IsNullOrWhiteSpace(spec.About.Credits))
                names.Children.Add(new TextBlock
                {
                    Text = spec.About.Credits,
                    FontSize = 12,
                    Foreground = Theme.SubFg,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 6, 0, 0)
                });
            var head = new DockPanel { Margin = new Thickness(0, 16, 0, 0) };
            DockPanel.SetDock(logo, Dock.Left);
            head.Children.Add(logo);
            head.Children.Add(names);
            page.Children.Add(head);

            foreach (var section in spec.About.Sections)
                AddSection(page, section, ctx, false);

            page.Children.Add(Ui.Section("Links"));
            if (spec.RepoUrl != null)
            {
                page.Children.Add(ctx.Fields.LinkLine(spec.Title + " on GitHub", spec.RepoUrl));
                page.Children.Add(ctx.Fields.LinkLine("Releases", spec.ReleasesUrl));
            }
            foreach (var link in spec.About.Links)
                page.Children.Add(ctx.Fields.LinkLine(link.Text, link.Url));
            if (spec.RepoUrl != null)
                page.Children.Add(ctx.Fields.LinkLine("Report something that looks wrong", spec.RepoUrl + "/issues"));
            if (spec.About.ShowAuthorLink)
                page.Children.Add(ctx.Fields.LinkLine("aaskjer on Twitch", "https://twitch.tv/aaskjer"));
            if (spec.About.ShowStreamerBotLink)
                page.Children.Add(ctx.Fields.LinkLine("Streamer.bot", "https://streamer.bot"));
            page.Children.Add(ctx.Fields.LinkLine("UIFramework for streamer.bot", "https://github.com/aaskjer/UIFramework-Streamer.bot"));

            return Ui.Scroll(page);
        }

        // ---- Help -----------------------------------------------------------------------------------------

        public static UIElement BuildHelp(PageContext ctx)
        {
            if (ctx.Spec.Help.Count == 0)
                return null;
            var page = Ui.PageStack();
            bool first = true;
            foreach (var section in ctx.Spec.Help)
            {
                AddSection(page, section, ctx, first);
                first = false;
            }
            return Ui.Scroll(page);
        }

        private static void AddSection(StackPanel page, HelpSection section, PageContext ctx, bool first)
        {
            if (!string.IsNullOrEmpty(section.Heading))
                page.Children.Add(Ui.Section(section.Heading, first));
            foreach (var line in section.Lines)
            {
                if (line.Note != null)
                    page.Children.Add(Ui.Note(line.Note));
                else if (line.Command != null)
                    page.Children.Add(Ui.CommandRow(line.Command, line.What ?? ""));
                else if (line.LinkText != null)
                    page.Children.Add(ctx.Fields.LinkLine(line.LinkText, line.Url));
                else if (line.Live != null)
                {
                    var live = new StackPanel();
                    page.Children.Add(live);
                    var read = line.Live;
                    void Redraw()
                    {
                        live.Children.Clear();
                        IEnumerable<string> notes;
                        try { notes = read(ctx) ?? Enumerable.Empty<string>(); }
                        catch (Exception ex) { notes = new[] { "(" + ex.Message + ")" }; }
                        foreach (var note in notes)
                            live.Children.Add(Ui.Note(note));
                    }
                    ctx.Parts.OnShow("help", Redraw);
                    Redraw();
                }
            }
        }

        // ---- Messages -------------------------------------------------------------------------------------

        public static UIElement BuildMessages(PageContext ctx)
        {
            var spec = ctx.Spec;
            var defs = spec.Messages;
            if (defs.Count == 0)
                return null;
            var f = ctx.Fields;
            var parts = ctx.Parts;
            var keys = spec.Senders;
            var page = Ui.PageStack();

            var picker = new ComboBox
            {
                Name = "MessagePicker",
                Height = 28,
                MaxDropDownHeight = 440,
                VerticalContentAlignment = VerticalAlignment.Center,
                Template = Ui.ComboTemplate(),
                ItemContainerStyle = Ui.ComboItemStyle(),
                Foreground = new SolidColorBrush(Theme.InputFg)
            };
            string group = null;
            foreach (var def in defs)
            {
                if (def.Group != group)
                {
                    group = def.Group;
                    picker.Items.Add(new ComboBoxItem { Content = group, IsEnabled = false, FontSize = 11, FontWeight = FontWeights.SemiBold });
                }
                picker.Items.Add(new ComboBoxItem { Content = def.Label, Tag = def.Key, Padding = new Thickness(16, 3, 6, 3) });
            }

            Grid top;
            ComboBox separator = null;
            if (!string.IsNullOrEmpty(keys.NumberSeparator))
            {
                separator = f.StringCombo("Numbers", keys.NumberSeparator,
                    new[] { (".", "12.345"), (",", "12,345"), (" ", "12 345"), ("", "12345") },
                    "The thousands separator used in every amount that is posted.");
                top = Ui.Row(new[] { 2.4, 1.0 }, Ui.Labeled("Message", picker), Ui.Labeled("Numbers", separator));
            }
            else
                top = Ui.Row(Ui.Labeled("Message", picker));
            top.Margin = new Thickness(0, 12, 0, 0);

            var help = new TextBlock { FontSize = 12, Foreground = Theme.MutedFg, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };

            var editors = new Grid { Margin = new Thickness(0, 8, 0, 0) };
            var touched = new HashSet<TextBox>();
            foreach (var def in defs)
            {
                var box = f.TemplateBox(def.Group + ": " + def.Label, def.Key);
                box.Visibility = Visibility.Collapsed;
                box.GotKeyboardFocus += (sender, e) => touched.Add(box);
                editors.Children.Add(box);
            }

            var tokens = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
            var preview = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 13 };
            var previewBox = new Border
            {
                Background = new SolidColorBrush(Theme.Surface),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12, 9, 12, 9),
                Child = preview
            };

            var useBot = f.Toggle("Bot account", keys.UseBot,
                "Sends the messages from your Streamer.bot bot account instead of the broadcaster account.\n(Platforms > Any platform > Accounts > Bot)");
            var sendAction = f.Toggle("Send as /me", keys.SendAction,
                "Twitch only: YouTube and Kick have no /me, so there the messages always go out as plain chat.");
            CheckBox useBotCd = null, sendActionCd = null;
            bool hasCooldownSenders = !string.IsNullOrEmpty(keys.CooldownUseBot) && defs.Any(d => d.Group == keys.CooldownGroup);
            Fields.TwitchOnly(sendAction);
            sendAction.Margin = new Thickness(18, 0, 0, 0);
            var gameSender = new StackPanel { Orientation = Orientation.Horizontal };
            gameSender.Children.Add(useBot);
            gameSender.Children.Add(sendAction);
            var cooldownSender = new StackPanel { Orientation = Orientation.Horizontal };
            if (hasCooldownSenders)
            {
                useBotCd = f.Toggle("Bot account", keys.CooldownUseBot, "Sends the cooldown replies from your bot account, separately from the other messages.");
                sendActionCd = f.Toggle("Send as /me", keys.CooldownSendAction, "Twitch only, and separate from the other messages.");
                Fields.TwitchOnly(sendActionCd);
                sendActionCd.Margin = new Thickness(18, 0, 0, 0);
                cooldownSender.Children.Add(useBotCd);
                cooldownSender.Children.Add(sendActionCd);
            }
            var scope = new TextBlock { FontSize = 12, Foreground = Theme.MutedFg, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 0, 0) };
            var senders = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
            senders.Children.Add(gameSender);
            senders.Children.Add(cooldownSender);
            if (hasCooldownSenders)
                senders.Children.Add(scope);

            MessageDef current = null;

            void Render()
            {
                if (current == null)
                    return;
                bool cooldown = hasCooldownSenders && current.Group == keys.CooldownGroup;
                string text = parts.Boxes[current.Key].Text;
                var samples = spec.SampleValues?.Invoke(ctx) ?? new Dictionary<string, string>();
                var picked = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var token in current.Tokens)
                    picked[token] = samples.TryGetValue(token, out var value) ? value : "";
                bool bot = f.LiveSwitch(cooldown ? keys.CooldownUseBot : keys.UseBot);
                bool action = f.LiveSwitch(cooldown ? keys.CooldownSendAction : keys.SendAction);
                RenderChat(preview, FillTemplate(text, picked), bot, action);
            }

            void Pick(string key)
            {
                current = defs.First(d => d.Key == key);
                foreach (var def in defs)
                    parts.Boxes[def.Key].Visibility = def.Key == key ? Visibility.Visible : Visibility.Collapsed;

                help.Inlines.Clear();
                help.Inlines.Add(new Run(current.Group) { FontWeight = FontWeights.SemiBold });
                help.Inlines.Add(new Run("  ·  " + current.Help));

                tokens.Children.Clear();
                if (current.Tokens.Length == 0)
                    tokens.Children.Add(new TextBlock { Text = "Sent as written: this one has no placeholders.", FontSize = 12, Foreground = Theme.MutedFg });
                string target = current.Key;
                foreach (var token in current.Tokens)
                {
                    string placeholder = "{" + token + "}";
                    var chip = Ui.ChipButton(placeholder, spec.TokenHelp?.Invoke(target, token));
                    chip.Click += (sender, e) =>
                    {
                        var box = parts.Boxes[target];
                        Ui.InsertAtCaret(box, placeholder, !touched.Contains(box));
                    };
                    tokens.Children.Add(chip);
                }

                bool cooldown = hasCooldownSenders && current.Group == keys.CooldownGroup;
                gameSender.Visibility = cooldown ? Visibility.Collapsed : Visibility.Visible;
                cooldownSender.Visibility = cooldown ? Visibility.Visible : Visibility.Collapsed;
                scope.Text = cooldown ? "Only for cooldown replies" : "Not for cooldown replies";
                Render();
            }

            foreach (var def in defs)
                parts.Boxes[def.Key].TextChanged += (sender, e) => Render();
            foreach (var toggle in new[] { useBot, sendAction, useBotCd, sendActionCd }.Where(t => t != null))
            {
                toggle.Checked += (sender, e) => Render();
                toggle.Unchecked += (sender, e) => Render();
            }
            if (separator != null)
                separator.SelectionChanged += (sender, e) => parts.Refresh("messages");
            picker.SelectionChanged += (sender, e) =>
            {
                if (picker.SelectedItem is ComboBoxItem item && item.Tag is string key)
                    Pick(key);
            };
            parts.OnShow("messages", Render);

            page.Children.Add(top);
            page.Children.Add(help);
            page.Children.Add(editors);
            page.Children.Add(tokens);
            page.Children.Add(Ui.Section("Chat preview"));
            page.Children.Add(previewBox);
            page.Children.Add(senders);
            page.Children.Add(Ui.Note("The preview fills every placeholder with a sample, so a stray brace or a misspelt name shows up here before chat sees it."));

            picker.SelectedItem = picker.Items.OfType<ComboBoxItem>().First(i => defs[0].Key.Equals(i.Tag));
            return Ui.Scroll(page);
        }

        public static string FillTemplate(string template, Dictionary<string, string> values)
        {
            string text = template ?? "";
            foreach (var pair in values)
                text = text.Replace("{" + pair.Key + "}", pair.Value ?? "");
            return text;
        }

        public static void RenderChat(TextBlock target, string message, bool bot, bool action)
        {
            target.Inlines.Clear();
            if (string.IsNullOrWhiteSpace(message))
            {
                target.Inlines.Add(new Run("Empty, so nothing is sent.") { Foreground = Theme.MutedFg, FontStyle = FontStyles.Italic });
                return;
            }
            target.Inlines.Add(new Run(bot ? "YourBot" : "YourChannel") { FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Theme.AccentText) });
            target.Inlines.Add(new Run(action ? " " : ": ") { Foreground = Theme.LabelFg });
            var body = new Run(message) { Foreground = action ? new SolidColorBrush(Theme.AccentText) : Theme.LabelFg };
            if (action)
                body.FontStyle = FontStyles.Italic;
            target.Inlines.Add(body);
        }

        // ---- Log ------------------------------------------------------------------------------------------

        public static UIElement BuildLog(PageContext ctx) => LogPage.Build(ctx);
    }
}
