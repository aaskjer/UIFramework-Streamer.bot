using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace UIFramework
{
    /// <summary>The icon rail down the left: one item per page, the active one highlighted. A window that groups its
    /// pages has a heading per group, which folds the group's pages away.</summary>
    public sealed class Rail
    {
        /// <summary>What a group's heading carries as its Tag, followed by the group's id.</summary>
        public const string GroupTag = "uiframework.rail.group:";

        public readonly List<Border> Items = new List<Border>();
        /// <summary>The pages' items and the groups' headings in the order they were made, which is the rail's from top
        /// to bottom: what Up and Down step through.</summary>
        public readonly List<Border> Stops = new List<Border>();
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

        /// <summary>Up and Down from one stop to the next the rail shows: the pages of a folded group are passed over.</summary>
        private bool Step(Border from, int by)
        {
            for (int at = Stops.IndexOf(from) + by; at >= 0 && at < Stops.Count; at += by)
            {
                var stop = Stops[at];
                if (stop.Visibility != Visibility.Visible || (stop.Parent as UIElement)?.Visibility == Visibility.Collapsed) continue;
                stop.Focus();
                return true;
            }
            return false;
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
            // By keyboard too: Tab reaches the rail, Up and Down move along it, Enter or Space opens the page. (In the
            // customise mode the Menu key or Shift+F10 opens the page's menu, as a right-click does.)
            item.Focusable = true;
            item.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter || e.Key == Key.Space) { select(); e.Handled = true; }
                else if (e.Key == Key.Down || e.Key == Key.Up)
                {
                    if (Step(item, e.Key == Key.Down ? 1 : -1)) e.Handled = true;
                }
            };
            item.MouseEnter += (s, e) => { if (item != _active) item.Background = new SolidColorBrush(Theme.Hover); };
            item.MouseLeave += (s, e) => { if (item != _active) item.Background = Brushes.Transparent; };
            Items.Add(item);
            Stops.Add(item);
            return item;
        }

        /// <summary>A group's heading: the group's title, as wide as a page's label, and a small arrow under it. A click
        /// folds the group's pages away or shows them again, and so do Enter and Space; Left folds, Right unfolds, Up and
        /// Down move along the rail. What folding means is the caller's (<paramref name="toggle"/>,
        /// <paramref name="fold"/>); how the heading looks folded or not is <see cref="Fold"/>.</summary>
        public Border Heading(string id, string title, System.Action toggle, System.Action<bool> fold = null)
        {
            var label = new TextBlock
            {
                Text = title,
                FontSize = 10.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = Theme.SubFg,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Fit(label, 58);
            var arrow = new TextBlock
            {
                FontFamily = Icons.Font,
                FontSize = 8,
                Foreground = Theme.SubFg,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 0)
            };
            var heading = new Border
            {
                Name = "RailGroup" + new string((id ?? "").Where(char.IsLetterOrDigit).ToArray()),
                Tag = GroupTag + id,
                Width = 64,
                Padding = new Thickness(2, 4, 2, 3),
                Margin = new Thickness(0, 0, 0, 2),
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(1),
                BorderBrush = Brushes.Transparent,
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                Focusable = true,
                Child = new StackPanel { Children = { label, arrow } }
            };
            AutomationProperties.SetName(heading, title);
            heading.MouseLeftButtonUp += (s, e) => toggle();
            heading.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter || e.Key == Key.Space) { toggle(); e.Handled = true; }
                else if ((e.Key == Key.Left || e.Key == Key.Right) && fold != null) { fold(e.Key == Key.Left); e.Handled = true; }
                else if (e.Key == Key.Down || e.Key == Key.Up)
                {
                    if (Step(heading, e.Key == Key.Down ? 1 : -1)) e.Handled = true;
                }
            };
            heading.MouseEnter += (s, e) => heading.Background = new SolidColorBrush(Theme.Hover);
            heading.MouseLeave += (s, e) => heading.Background = Brushes.Transparent;
            Fold(heading, false);
            Stops.Add(heading);
            return heading;
        }

        /// <summary>Whether a heading shows its group folded: the arrow points right over hidden pages, down over shown ones.</summary>
        public static bool IsFolded(Border heading) => ((TextBlock)((StackPanel)heading.Child).Children[1]).Text == Icons.ChevronRight;

        /// <summary>Draws a heading folded or not. <paramref name="fixedOpen"/>: the group cannot be folded right now (it
        /// holds the page shown, or the window is being customised), and the arrow is faint.</summary>
        public static void Fold(Border heading, bool folded, bool fixedOpen = false)
        {
            var arrow = (TextBlock)((StackPanel)heading.Child).Children[1];
            arrow.Text = folded ? Icons.ChevronRight : Icons.ChevronDown;
            arrow.Opacity = fixedOpen && !folded ? 0.4 : 1.0;
            AutomationProperties.SetItemStatus(heading, folded ? "collapsed" : "expanded");
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
