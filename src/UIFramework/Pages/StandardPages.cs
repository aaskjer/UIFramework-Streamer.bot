using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
        public static PageSpec Help(string title = "Help", string glyph = "📖") => new PageSpec("help", title, glyph, BuildHelp, bottom: true);
        public static PageSpec Log(string title = "Log", string glyph = "📜") => new PageSpec("log", title, glyph, BuildLog);
        public static PageSpec Messages(string title = "Messages", string glyph = "💬") => new PageSpec("messages", title, glyph, BuildMessages);

        // ---- About ----------------------------------------------------------------------------------------

        public static UIElement BuildAbout(PageContext ctx)
        {
            var spec = ctx.Spec;
            var page = Ui.PageStack();

            // No logo declared: no empty square beside the name.
            var logo = new Image { Width = 72, Height = 72, Margin = new Thickness(0, 0, 14, 0) };
            if (string.IsNullOrWhiteSpace(spec.LogoUrl))
                logo.Visibility = Visibility.Collapsed;
            else
                Web.LoadImageAsync(logo, spec.LogoUrl);
            var names = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            names.Children.Add(new TextBlock { Text = spec.Title, FontSize = 17, FontWeight = FontWeights.SemiBold, Foreground = Theme.LabelFg });
            names.Children.Add(new TextBlock
            {
                Text = Text.T("about.version", spec.Version ?? "?", Entry.FrameworkVersion),
                FontSize = 12,
                Foreground = Theme.SubFg,
                Margin = new Thickness(0, 2, 0, 0)
            });
            // Whether this game takes part in the one update notice in chat per Streamer.bot start.
            if (ctx.Layout != null)
            {
                var notices = new CheckBox
                {
                    Content = Text.T("about.updateNotices"),
                    IsChecked = ctx.Layout.UpdateNotices,
                    ToolTip = Text.T("about.updateNotices.tip"),
                    Template = Ui.SwitchTemplate(),
                    Foreground = Theme.SubFg,
                    FontSize = 12,
                    Cursor = Cursors.Hand,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(0, 6, 0, 0)
                };
                AutomationProperties.SetName(notices, Text.T("about.updateNotices"));
                notices.Click += (s, e) =>
                {
                    ctx.Layout.UpdateNotices = notices.IsChecked == true;
                    try { ctx.Layout.Save(); } catch (Exception ex) { ctx.Log?.Warn(spec.LogPrefix + " " + LayoutFile.FileName + ": " + ex.Message); }
                };
                names.Children.Add(notices);
            }
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

            page.Children.Add(Ui.Section(Text.T("about.links")));
            // Every address once: a game that lists its repository or releases among its own links (as the
            // framework already does from `releases`) does not get them twice.
            var shown = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            void Link(string text, string url)
            {
                if (string.IsNullOrWhiteSpace(url) || !shown.Add(url.Trim().TrimEnd('/'))) return;
                page.Children.Add(ctx.Fields.LinkLine(text, url));
            }
            if (spec.RepoUrl != null)
            {
                Link(Text.T("about.onGitHub", spec.Title), spec.RepoUrl);
                Link(Text.T("about.releases"), spec.ReleasesUrl);
            }
            foreach (var link in spec.About.Links)
                Link(link.Text, link.Url);
            // Then the links every window ends with, as conventions/keys.json lists them (the framework's page,
            // Streamer.bot, where to report a problem): a text is a window text's key or the text itself.
            if (spec.About.ShowDefaultLinks)
            {
                var conventions = spec.Conventions ?? Conventions.Default;
                foreach (var link in conventions.AboutLinks)
                {
                    if (!spec.About.ShowStreamerBotLink && link.Url.IndexOf("streamer.bot", StringComparison.OrdinalIgnoreCase) >= 0)
                        continue;
                    string url = link.Url.Trim() == "{issues}" ? (spec.RepoUrl != null ? spec.RepoUrl + "/issues" : conventions.FrameworkIssues) : link.Url;
                    Link(Text.T(link.Text), url);
                }
            }

            // The framework's logo, faint like a watermark, in the bottom right corner: this window is the original
            // UIFramework. Under the content, never over it: the page is at least as tall as the view, the logo in
            // its last row, so a short page has it in the corner and a long one at its end.
            var mark = Watermark(ctx);
            if (mark == null)
                return Ui.Scroll(page);
            var body = new Grid();
            body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            body.Children.Add(page);
            Grid.SetRow(mark, 1);
            body.Children.Add(mark);
            var scroll = Ui.Scroll(body);
            body.SetBinding(FrameworkElement.MinHeightProperty, new Binding("ViewportHeight") { Source = scroll });
            return scroll;
        }

        /// <summary>The name of the watermark on the About page, for tests and screen readers.</summary>
        public const string WatermarkName = "UIFramework watermark";

        private static readonly object LogoLock = new object();
        private static ImageSource _logoDark, _logoLight;

        /// <summary>The framework's logo (mark and wordmark) for a dark or a light ground, from the DLL; null when it is
        /// missing. Frozen, so any window's thread can show it.</summary>
        public static ImageSource FrameworkLogo(bool dark)
        {
            lock (LogoLock)
            {
                var cached = dark ? _logoDark : _logoLight;
                if (cached != null) return cached;
                try
                {
                    using (var stream = typeof(StandardPages).Assembly.GetManifestResourceStream("UIFramework.assets.uiframework-logo-" + (dark ? "dark" : "light") + ".png"))
                    {
                        if (stream == null) return null;
                        var image = new BitmapImage();
                        image.BeginInit();
                        image.CacheOption = BitmapCacheOption.OnLoad;
                        image.StreamSource = stream;
                        image.EndInit();
                        image.Freeze();
                        if (dark) _logoDark = image; else _logoLight = image;
                        return image;
                    }
                }
                catch (Exception) { return null; }
            }
        }

        private static FrameworkElement Watermark(PageContext ctx)
        {
            var source = FrameworkLogo(Theme.IsDark);
            if (source == null) return null;
            var conventions = ctx.Spec?.Conventions ?? Conventions.Default;
            string url = conventions.AboutLinks.FirstOrDefault(l => l.Text == "about.framework")?.Url ?? "https://github.com/aaskjer/UIFramework-Streamer.bot";
            const double faint = 0.35;
            var logo = new Image
            {
                Source = source,
                Width = 150,
                Stretch = Stretch.Uniform,
                Opacity = faint,
                Cursor = Cursors.Hand,
                ToolTip = Text.T("about.watermark", Entry.FrameworkVersion),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 8, 16, 12)
            };
            RenderOptions.SetBitmapScalingMode(logo, BitmapScalingMode.HighQuality);
            AutomationProperties.SetName(logo, WatermarkName);
            AutomationProperties.SetHelpText(logo, Text.T("about.watermark", Entry.FrameworkVersion));
            logo.MouseEnter += (s, e) => logo.Opacity = 0.9;
            logo.MouseLeave += (s, e) => logo.Opacity = faint;
            logo.MouseLeftButtonUp += (s, e) => ctx.OpenUrl(url);
            return logo;
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
            // In another language the conventions and the game's own translations (Tx) translate what they know,
            // heading by heading and line by line; what nothing knows stays as declared.
            var lang = ctx.Spec.Conventions?.Lang;
            string T(string declared) => lang != null && lang.Translates ? lang.Text(declared) : declared;
            if (!string.IsNullOrEmpty(section.Heading))
                page.Children.Add(Ui.Section(T(section.Heading), first));
            foreach (var line in section.Lines)
            {
                if (line.Note != null)
                    page.Children.Add(Ui.Note(T(line.Note)));
                else if (line.Command != null)
                    page.Children.Add(Ui.CommandRow(line.Command, T(line.What ?? "")));
                else if (line.LinkText != null)
                    page.Children.Add(ctx.Fields.LinkLine(T(line.LinkText), line.Url));
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
            if (ctx.Customizing)
                return Customize.MessagesPage(ctx);
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
                separator = f.StringCombo(Text.T("messages.numbers"), keys.NumberSeparator,
                    new[] { (".", "12.345"), (",", "12,345"), (" ", "12 345"), ("", "12345") },
                    Text.T("messages.numbers.tip"));
                top = Ui.Row(new[] { 2.4, 1.0 }, Ui.Labeled(Text.T("messages.message"), picker), Ui.Labeled(Text.T("messages.numbers"), separator));
            }
            else
                top = Ui.Row(Ui.Labeled(Text.T("messages.message"), picker));
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

            CheckBox useBot = null, sendAction = null, useBotCd = null, sendActionCd = null;
            var gameSender = new StackPanel { Orientation = Orientation.Horizontal };
            if (keys.UseBot != null)
            {
                useBot = f.Toggle(Text.T("messages.botAccount"), keys.UseBot, Text.T("messages.botAccount.tip"));
                gameSender.Children.Add(useBot);
            }
            if (keys.SendAction != null)
            {
                sendAction = f.Toggle(Text.T("messages.sendMe"), keys.SendAction, Text.T("messages.sendMe.tip"));
                Fields.TwitchOnly(sendAction);
                sendAction.Margin = new Thickness(18, 0, 0, 0);
                gameSender.Children.Add(sendAction);
            }
            bool hasCooldownSenders = keys.CooldownUseBot != null && defs.Any(d => keys.IsCooldownGroup(d.Group));
            var cooldownSender = new StackPanel { Orientation = Orientation.Horizontal };
            if (hasCooldownSenders)
            {
                useBotCd = f.Toggle(Text.T("messages.botAccount"), keys.CooldownUseBot, Text.T("messages.cooldownBot.tip"));
                cooldownSender.Children.Add(useBotCd);
                if (keys.CooldownSendAction != null)
                {
                    sendActionCd = f.Toggle(Text.T("messages.sendMe"), keys.CooldownSendAction, Text.T("messages.cooldownMe.tip"));
                    Fields.TwitchOnly(sendActionCd);
                    sendActionCd.Margin = new Thickness(18, 0, 0, 0);
                    cooldownSender.Children.Add(sendActionCd);
                }
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
                bool cooldown = hasCooldownSenders && keys.IsCooldownGroup(current.Group);
                string text = parts.Boxes[current.Key].Text;
                var samples = spec.SampleValues?.Invoke(ctx) ?? new Dictionary<string, string>();
                var picked = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var token in current.Tokens)
                    picked[token] = samples.TryGetValue(token, out var value) ? value : "";
                string botKey = cooldown ? keys.CooldownUseBot : keys.UseBot;
                string actionKey = cooldown ? keys.CooldownSendAction : keys.SendAction;
                bool bot = botKey == null || f.LiveSwitch(botKey);
                bool action = actionKey != null && f.LiveSwitch(actionKey);
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
                    tokens.Children.Add(new TextBlock { Text = Text.T("messages.noPlaceholders"), FontSize = 12, Foreground = Theme.MutedFg });
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

                bool cooldown = hasCooldownSenders && keys.IsCooldownGroup(current.Group);
                gameSender.Visibility = cooldown ? Visibility.Collapsed : Visibility.Visible;
                cooldownSender.Visibility = cooldown ? Visibility.Visible : Visibility.Collapsed;
                scope.Text = cooldown ? Text.T("messages.scope.cooldown") : Text.T("messages.scope.game");
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
            page.Children.Add(Ui.Section(Text.T("messages.preview")));
            page.Children.Add(previewBox);
            page.Children.Add(senders);
            page.Children.Add(Ui.Note(Text.T("messages.preview.note")));

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
                target.Inlines.Add(new Run(Text.T("messages.empty")) { Foreground = Theme.MutedFg, FontStyle = FontStyles.Italic });
                return;
            }
            target.Inlines.Add(new Run(bot ? Text.T("messages.yourBot") : Text.T("messages.yourChannel")) { FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Theme.AccentText) });
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
