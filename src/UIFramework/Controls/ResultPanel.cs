using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Newtonsoft.Json.Linq;

namespace UIFramework
{
    /// <summary>
    /// What an action's answer looks like in its panel. Plain text is shown as it is. An answer that is a JSON
    /// object may carry <c>lines</c> (strings, or <c>{text, big}</c> for a headline figure) and <c>bar</c>
    /// (segments <c>{label, value, text, colour}</c>, drawn proportionally with a legend, the colour as #rrggbb;
    /// without one the segments take the accent, the success colour and the surface in turn). A line
    /// <c>{emotes: [codes], platform, text}</c> shows the emotes as pictures (see <see cref="EmotePictures"/>),
    /// each with its name beneath, after the text when there is one. A line <c>{label, text, copy}</c> is a labelled
    /// value, the labels of one box in a column of their own; with <c>copy</c> a small button puts the text on the
    /// clipboard. A line <c>{pill, tone}</c> is a pill, its tone <c>ok</c>, <c>warn</c> or <c>muted</c> (green,
    /// amber, grey, the colours of the Log page's results); it may have a <c>label</c> in front and a <c>text</c>
    /// behind it.
    /// </summary>
    public static class ResultPanel
    {
        /// <summary>What a copy button does with its text: onto the clipboard. A test puts its own here.</summary>
        public static Action<string> Copy = text => Clipboard.SetText(text);

        /// <summary>How long a copy button says that it copied, in milliseconds.</summary>
        public const int SaidMs = 1500;

        /// <summary>The colours of a pill's tone: <c>ok</c> green, <c>warn</c> amber, <c>muted</c> and whatever else
        /// grey, as the Log page colours its results.</summary>
        public static PillStyle Tone(string tone)
        {
            switch ((tone ?? "").Trim().ToLowerInvariant())
            {
                case "ok": return PillStyle.Success;
                case "warn": return PillStyle.Warning;
                default: return PillStyle.Muted;
            }
        }

        /// <summary>The answer as a JSON object, or null for plain text.</summary>
        public static JObject TryParse(string result)
        {
            string text = (result ?? "").Trim();
            if (!text.StartsWith("{"))
                return null;
            try { return JsonText.Parse(text) as JObject; } catch (Exception) { return null; }
        }

        /// <summary>Marks the line of a failure, so the next answer does not keep it.</summary>
        private const string Failure = "action-failed";

        /// <param name="failed">The text says what went wrong: it is shown in the colour of a failure.</param>
        public static void Render(StackPanel panel, string result, IHostHooks hooks = null, bool failed = false)
        {
            string text = (result ?? "").Trim();
            var structured = failed ? null : TryParse(text);
            if (structured == null)
            {
                panel.Children.Clear();
                panel.Tag = null;
                var line = Line(text, false);
                if (failed) { line.Foreground = new SolidColorBrush(Theme.DangerText); line.Tag = Failure; }
                panel.Children.Add(line);
                return;
            }
            if (structured["lines"] == null && structured["bar"] == null)
            {
                // An answer that only sets fields or a status leaves the panel as it was; an empty panel gets the status.
                // A failure of an earlier ask is not kept: it said the action failed under an answer that came.
                if (panel.Children.Count == 1 && panel.Children[0] is TextBlock before && Equals(before.Tag, Failure))
                    panel.Children.Clear();
                string status = (string)structured["status"];
                if (panel.Children.Count == 0 && !string.IsNullOrEmpty(status))
                    panel.Children.Add(Line(status, false));
                return;
            }
            // The answer the box shows already (an auto action that asks again every few seconds): nothing is drawn
            // anew, so a tooltip stays up and a copy button keeps the cursor and what it just said.
            if (panel.Children.Count > 0 && Equals(panel.Tag, text))
                return;
            panel.Children.Clear();
            panel.Tag = text;
            // The labels of the box's values share one column, as wide as the longest of them.
            Grid.SetIsSharedSizeScope(panel, true);

            var lines = structured["lines"] as JArray ?? new JArray();
            var bar = (structured["bar"] as JArray ?? new JArray()).OfType<JObject>().ToList();
            int shown = 0;
            foreach (var line in lines)
            {
                if (line is JObject pictures && pictures["emotes"] is JArray codes)
                {
                    string caption = (string)pictures["text"];
                    if (!string.IsNullOrEmpty(caption))
                        panel.Children.Add(Line(caption, false));
                    panel.Children.Add(Emotes(codes, (string)pictures["platform"], hooks));
                }
                else if (line is JObject row && (Said(row["label"]).Length > 0 || Said(row["pill"]).Length > 0 || Copies(row)))
                    panel.Children.Add(Row(row));
                else
                {
                    bool big = line is JObject o && (bool?)o["big"] == true;
                    string value = line is JObject lo ? (string)lo["text"] ?? "" : line.ToString();
                    panel.Children.Add(Line(value, big));
                }
                shown++;
                if (shown == 1 && bar.Count > 0)
                    panel.Children.Add(Bar(bar));
            }
            if (lines.Count == 0 && bar.Count > 0)
                panel.Children.Add(Bar(bar));
        }

        /// <summary>The emotes of a spin as pictures, each with its name beneath: a Kick code's name, a YouTube
        /// shortcode's name; a Unicode emoji on YouTube is drawn as itself.</summary>
        public static FrameworkElement Emotes(JArray codes, string platform, IHostHooks hooks)
        {
            platform = (platform ?? "twitch").Trim().ToLowerInvariant();
            var wrap = new WrapPanel { Margin = new Thickness(0, 4, 0, 4) };
            foreach (var token in codes)
            {
                string code = token?.ToString() ?? "";
                string name = EmotePictures.NameOf(code);
                var cell = new StackPanel { Margin = new Thickness(0, 0, 16, 0), VerticalAlignment = VerticalAlignment.Bottom };
                // Whether a YouTube shortcode has a picture can hang on the project's emote sources (its `youtube`
                // object), which may not have come yet. The window does not wait for them: the cell holds the
                // picture's place meanwhile, and a code that turns out to have none is drawn as its text then. A
                // Unicode emoji needs no sources and shows itself at once.
                var url = platform == "youtube" ? EmotePictures.YouTubeLookup(code, hooks) : null;
                if (url != null && url.IsCompleted && NoPicture(url))
                    cell.Children.Add(Glyph(code));
                else
                {
                    var image = EmotePictures.Picture(platform, code, hooks);
                    image.Width = 32;
                    image.Height = 32;
                    image.Margin = new Thickness(0);
                    image.HorizontalAlignment = HorizontalAlignment.Center;
                    cell.Children.Add(image);
                    if (url != null && !url.IsCompleted)
                    {
                        var dispatcher = cell.Dispatcher;
                        url.ContinueWith(found =>
                        {
                            if (!NoPicture(found))
                                return;
                            dispatcher.BeginInvoke(new Action(() =>
                            {
                                int at = cell.Children.IndexOf(image);
                                if (at < 0)
                                    return;
                                cell.Children.RemoveAt(at);
                                cell.Children.Insert(at, Glyph(code));
                            }));
                        }, TaskScheduler.Default);
                    }
                }
                cell.Children.Add(new TextBlock { Text = name, FontSize = 11, Foreground = Theme.SubFg, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 2, 0, 0) });
                wrap.Children.Add(cell);
            }
            return wrap;
        }

        /// <summary>The lookup found no picture for the code (or failed, which shows the same).</summary>
        private static bool NoPicture(Task<string> url) => url.Status != TaskStatus.RanToCompletion || url.Result == null;

        /// <summary>An emote without a picture, drawn as its text in the picture's place.</summary>
        private static TextBlock Glyph(string code) =>
            new TextBlock { Text = code, FontSize = 26, HorizontalAlignment = HorizontalAlignment.Center, Foreground = Theme.LabelFg };

        /// <summary>A line's text whatever the answer wrote it as (a number, a switch); "" for none.</summary>
        private static string Said(JToken value) => value is JValue ? FieldBinding.Invariant(value) : "";

        private static bool Copies(JObject row) =>
            row["copy"]?.Type == JTokenType.Boolean && (bool)row["copy"] && Said(row["text"]).Length > 0;

        /// <summary>A line that is more than text: a label in the box's label column, then a pill, the value and its
        /// copy button, whichever of them the line has. Nothing is cut off in a narrow window or a wordy language: the
        /// value wraps, with the button still beside it, and goes under its pill where the two do not fit side by side.</summary>
        private static FrameworkElement Row(JObject row)
        {
            string label = Said(row["label"]), pill = Said(row["pill"]), text = Said(row["text"]);
            // Left-aligned and as wide as what is in it, so the copy button sits at the end of a short value, not at
            // the far end of the box. The button is measured first; the pill and the value share what it leaves.
            var content = new DockPanel { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
            if (Copies(row))
            {
                var copy = CopyButton(text, label);
                DockPanel.SetDock(copy, Dock.Right);
                content.Children.Add(copy);
            }
            var flow = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
            if (pill.Length > 0)
            {
                var shown = Pill(pill, Said(row["tone"]));
                if (text.Length > 0) shown.Margin = new Thickness(0, 0, 8, 0);
                flow.Children.Add(shown);
            }
            if (text.Length > 0)
            {
                // The value as a line of its own is drawn, `big` for a headline figure included.
                var value = Line(text, row["big"]?.Type == JTokenType.Boolean && (bool)row["big"]);
                value.VerticalAlignment = VerticalAlignment.Center;
                flow.Children.Add(value);
            }
            content.Children.Add(flow);

            // Every row as high as one with a pill or a button, so a box of them reads as a table.
            var grid = new Grid { Margin = new Thickness(0, 2, 0, 2), MinHeight = 22 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, SharedSizeGroup = "ResultLabel" });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            if (label.Length > 0)
            {
                // A long label (another language's) wraps rather than pushing the value out of the box.
                grid.Children.Add(new TextBlock
                {
                    Text = label, FontSize = 12.5, Foreground = Theme.SubFg, TextWrapping = TextWrapping.Wrap, MaxWidth = 180,
                    VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 14, 0)
                });
                Grid.SetColumn(content, 1);
            }
            else
                Grid.SetColumnSpan(content, 2);
            grid.Children.Add(content);
            return grid;
        }

        /// <summary>A pill in its tone's colours, with a line around it between the two. The box it sits in is the
        /// grey a muted pill is filled with, and in the light theme about as bright as a green or amber one: the line
        /// keeps each of them a pill there.</summary>
        private static Border Pill(string text, string tone)
        {
            var colours = Ui.PillColours(Tone(tone));
            var pill = Ui.Pill(text, colours.Background, colours.Foreground);
            pill.Margin = new Thickness(0);
            pill.HorizontalAlignment = HorizontalAlignment.Left;
            pill.BorderThickness = new Thickness(1);
            pill.BorderBrush = new SolidColorBrush(Colours.Mix(colours.Foreground, colours.Background, 0.55));
            // A pill is a word or two; one that is a sentence wraps rather than running out of the box.
            ((TextBlock)pill.Child).TextWrapping = TextWrapping.Wrap;
            return pill;
        }

        /// <summary>The small button behind a value: its text goes onto the clipboard, and for a moment the button
        /// says so, with a tick in place of its two sheets and the word over it as its tooltip (or why nothing was
        /// copied, when another program holds the clipboard). Nothing on the page moves for it.</summary>
        private static Button CopyButton(string text, string label)
        {
            string tip = Text.T("panel.copy");
            // Several values of one box may have one: each says which value it copies.
            string name = label.Length > 0 ? tip + ": " + label : tip;
            var button = Ui.IconButton(Icons.Copy, Icons.Font, tip);
            button.Width = 22;
            button.Height = 22;
            button.FontSize = 11;
            button.Margin = new Thickness(6, 0, 0, 0);
            button.VerticalAlignment = VerticalAlignment.Center;
            System.Windows.Automation.AutomationProperties.SetName(button, name);
            ToolTip said = null;
            var back = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(SaidMs) };
            void Rest()
            {
                back.Stop();
                if (said != null) said.IsOpen = false;
                said = null;
                button.Content = Icons.Copy;
                button.Foreground = Theme.SubFg;
                button.ToolTip = tip;
                System.Windows.Automation.AutomationProperties.SetName(button, name);
            }
            back.Tick += (sender, e) => Rest();
            button.Click += (sender, e) =>
            {
                Rest();
                string words;
                bool copied = true;
                try { Copy(text); words = Text.T("panel.copied"); }
                catch (Exception ex) { copied = false; words = Text.T("panel.notCopied", ex.Message); }
                button.Content = copied ? Icons.Check : Icons.Copy;
                button.Foreground = new SolidColorBrush(copied ? Theme.SuccessText : Theme.DangerText);
                System.Windows.Automation.AutomationProperties.SetName(button, words);
                said = Ui.Tip(words);
                said.PlacementTarget = button;
                said.Placement = System.Windows.Controls.Primitives.PlacementMode.Top;
                button.ToolTip = said;
                // Opened here, not by the mouse resting on the button; a button in no window has nowhere to open it.
                if (PresentationSource.FromVisual(button) != null)
                    said.IsOpen = true;
                back.Start();
            };
            return button;
        }

        private static TextBlock Line(string text, bool big) => new TextBlock
        {
            Text = text,
            FontSize = big ? 18 : 12.5,
            FontWeight = big ? FontWeights.SemiBold : FontWeights.Normal,
            Foreground = Theme.LabelFg,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, big ? 4 : 2, 0, big ? 4 : 2)
        };

        private static FrameworkElement Bar(List<JObject> segments)
        {
            var palette = new[] { Theme.Accent, Theme.SuccessText, Theme.LineStrong, Theme.WarningText, Theme.DangerText };
            var grid = new Grid { Height = 8, Margin = new Thickness(0, 6, 0, 6), Background = new SolidColorBrush(Theme.Surface) };
            var legend = new WrapPanel();
            double total = segments.Sum(s => Math.Max(0, (double?)s["value"] ?? 0));
            for (int i = 0; i < segments.Count; i++)
            {
                double value = Math.Max(0, (double?)segments[i]["value"] ?? 0);
                var colour = Colours.TryParseHex((string)(segments[i]["colour"] ?? segments[i]["color"]), out var own) ? own : palette[i % palette.Length];
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(total > 0 ? value : 1, GridUnitType.Star), MinWidth = value > 0 ? 3 : 0 });
                var fill = new Border { Background = new SolidColorBrush(colour) };
                Grid.SetColumn(fill, i);
                grid.Children.Add(fill);

                string label = (string)segments[i]["label"] ?? "";
                string shown = (string)segments[i]["text"] ?? value.ToString(value > 0 && value < 1 ? "0.##" : "0.#", CultureInfo.InvariantCulture) + " %";
                var swatch = new Border
                {
                    Width = 9, Height = 9, CornerRadius = new CornerRadius(2),
                    Background = new SolidColorBrush(colour), BorderBrush = new SolidColorBrush(Theme.LineStrong), BorderThickness = new Thickness(1),
                    Margin = new Thickness(0, 0, 5, 0), VerticalAlignment = VerticalAlignment.Center
                };
                var item = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 14, 0) };
                item.Children.Add(swatch);
                item.Children.Add(new TextBlock { Text = (label.Length > 0 ? label + " " : "") + shown, FontSize = 12, Foreground = Theme.SubFg });
                legend.Children.Add(item);
            }
            grid.SizeChanged += (sender, e) => grid.Clip = new RectangleGeometry(new Rect(e.NewSize), 4, 4);
            var stack = new StackPanel();
            stack.Children.Add(grid);
            stack.Children.Add(legend);
            return stack;
        }
    }
}
