using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
    /// each with its name beneath, after the text when there is one.
    /// </summary>
    public static class ResultPanel
    {
        /// <summary>The answer as a JSON object, or null for plain text.</summary>
        public static JObject TryParse(string result)
        {
            string text = (result ?? "").Trim();
            if (!text.StartsWith("{"))
                return null;
            try { return JObject.Parse(text); } catch (Exception) { return null; }
        }

        public static void Render(StackPanel panel, string result, IHostHooks hooks = null)
        {
            panel.Children.Clear();
            string text = (result ?? "").Trim();
            var structured = TryParse(text);
            if (structured == null)
            {
                panel.Children.Add(Line(text, false));
                return;
            }
            if (structured["lines"] == null && structured["bar"] == null)
            {
                // An answer that only sets fields or a status leaves the panel as it was, or empty.
                string status = (string)structured["status"];
                if (!string.IsNullOrEmpty(status))
                    panel.Children.Add(Line(status, false));
                return;
            }

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

        /// <summary>The emotes of a spin as pictures, each with its name beneath; a YouTube emote is its emoji.</summary>
        public static FrameworkElement Emotes(JArray codes, string platform, IHostHooks hooks)
        {
            platform = (platform ?? "twitch").Trim().ToLowerInvariant();
            var wrap = new WrapPanel { Margin = new Thickness(0, 4, 0, 4) };
            foreach (var token in codes)
            {
                string code = token?.ToString() ?? "";
                var kick = EmotePictures.KickCode.Match(code);
                string name = kick.Success ? kick.Groups[2].Value : code;
                var cell = new StackPanel { Margin = new Thickness(0, 0, 16, 0), VerticalAlignment = VerticalAlignment.Bottom };
                if (platform == "youtube")
                    cell.Children.Add(new TextBlock { Text = code, FontSize = 26, HorizontalAlignment = HorizontalAlignment.Center, Foreground = Theme.LabelFg });
                else
                {
                    var image = EmotePictures.Picture(platform, code, hooks);
                    image.Width = 32;
                    image.Height = 32;
                    image.Margin = new Thickness(0);
                    image.HorizontalAlignment = HorizontalAlignment.Center;
                    cell.Children.Add(image);
                }
                cell.Children.Add(new TextBlock { Text = name, FontSize = 11, Foreground = Theme.SubFg, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 2, 0, 0) });
                wrap.Children.Add(cell);
            }
            return wrap;
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
                var colour = Colours.TryParseHex((string)segments[i]["colour"], out var own) ? own : palette[i % palette.Length];
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
