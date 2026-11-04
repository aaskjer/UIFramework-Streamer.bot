using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace UIFramework
{
    /// <summary>The icon rail down the left: one item per page, the active one highlighted.</summary>
    public sealed class Rail
    {
        public readonly List<Border> Items = new List<Border>();
        private Border _active;

        /// <summary>A word longer than the label is wide ("Abklingzeiten") would be broken mid-word by the wrapping:
        /// the label's font shrinks instead, as far as the longest word needs, so every title reads as its words.</summary>
        private static void Fit(TextBlock label, double width)
        {
            double longest = 0;
            var typeface = new Typeface(label.FontFamily, label.FontStyle, label.FontWeight, label.FontStretch);
            double dip = VisualTreeHelper.GetDpi(label).PixelsPerDip;
            foreach (var word in (label.Text ?? "").Split(' '))
            {
                var measured = new FormattedText(word, System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, typeface, label.FontSize, Brushes.Black, dip);
                longest = System.Math.Max(longest, measured.Width);
            }
            if (longest > width)
                label.FontSize = System.Math.Max(8.5, label.FontSize * width / longest);
        }

        public Border Item(string title, string glyph, System.Action select)
        {
            var icon = new TextBlock
            {
                Text = glyph,
                FontSize = 17,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            var label = new TextBlock
            {
                Text = title,
                FontSize = 11,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 3, 0, 0)
            };
            Fit(label, 58);
            // Sized so ten entries fit the window's default 720 px with its title bar, header and footer.
            var item = new Border
            {
                Name = "Rail" + new string(title.Where(char.IsLetter).ToArray()),
                Width = 64,
                Padding = new Thickness(2, 6, 2, 5),
                Margin = new Thickness(0, 0, 0, 2),
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(1),
                BorderBrush = Brushes.Transparent,
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                Child = new StackPanel { Children = { icon, label } }
            };
            AutomationProperties.SetName(item, title);
            item.MouseLeftButtonUp += (s, e) => select();
            item.MouseEnter += (s, e) => { if (item != _active) item.Background = new SolidColorBrush(Theme.Hover); };
            item.MouseLeave += (s, e) => { if (item != _active) item.Background = Brushes.Transparent; };
            Items.Add(item);
            return item;
        }

        public void Style(Border item, bool active)
        {
            if (active)
                _active = item;
            item.Background = active ? new SolidColorBrush(Theme.Surface) : Brushes.Transparent;
            var parts = ((StackPanel)item.Child).Children;
            // The glyph needs a colour of its own: Windows draws most of these as monochrome symbols rather than
            // colour emoji, and an uncoloured TextBlock inherits black, invisible on the dark theme.
            ((TextBlock)parts[0]).Foreground = active ? new SolidColorBrush(Theme.AccentText) : Theme.SubFg;
            ((TextBlock)parts[1]).Foreground = active ? Theme.LabelFg : Theme.SubFg;
        }
    }
}
