using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace UIFramework
{
    /// <summary>One thing the search can find: a setting, a message or a page.</summary>
    public sealed class SearchEntry
    {
        public const string Page = "page", Setting = "setting", Message = "message";

        /// <summary><see cref="Page"/>, <see cref="Setting"/> or <see cref="Message"/>.</summary>
        public string Kind = Setting;
        /// <summary>The setting's key, the message's key or the page's id: found too, for those who know it.</summary>
        public string Key;
        /// <summary>What the result is called: the label, the page's title.</summary>
        public string Title;
        /// <summary>Where it is: "Game › Payouts".</summary>
        public string Where;
        /// <summary>The help text, searched but shown only as the result's second line.</summary>
        public string Help;
        /// <summary>Brings it to the front: its page, the field in view.</summary>
        public Action Go;
    }

    /// <summary>Finding entries by what the user typed. Every word has to occur somewhere in an entry (its title, key,
    /// place or help), in any order, whatever the case and whatever the accents: "uber" finds "Über".</summary>
    public static class SearchIndex
    {
        /// <summary>Text as it is compared: lower case, accents and umlaut dots taken off, ß as ss.</summary>
        public static string Fold(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var folded = new StringBuilder(text.Length);
            foreach (char c in text.Normalize(NormalizationForm.FormD))
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
                if (c == 'ß') { folded.Append("ss"); continue; }
                folded.Append(char.ToLowerInvariant(c));
            }
            return folded.ToString();
        }

        /// <summary>The entries that hold every word of <paramref name="query"/>, best first: a word that starts the
        /// title before one that starts a word of it, before one inside it, before one in the key or the place,
        /// before one only the help has. Entries that rank alike keep the order they came in (the window's own).</summary>
        public static List<SearchEntry> Find(IEnumerable<SearchEntry> entries, string query, int most = 40)
        {
            var words = Fold(query).Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            var found = new List<(int Score, int Order, SearchEntry Entry)>();
            if (words.Length == 0 || entries == null) return new List<SearchEntry>();
            int order = 0;
            foreach (var entry in entries)
            {
                order++;
                string title = Fold(entry.Title), key = Fold(entry.Key), where = Fold(entry.Where), help = Fold(entry.Help);
                int score = 0;
                bool all = true;
                foreach (string word in words)
                {
                    int best = Rank(title, key, where, help, word);
                    if (best < 0) { all = false; break; }
                    score += best;
                }
                if (all) found.Add((score, order, entry));
            }
            return found.OrderBy(f => f.Score).ThenBy(f => f.Order).Take(Math.Max(1, most)).Select(f => f.Entry).ToList();
        }

        private static int Rank(string title, string key, string where, string help, string word)
        {
            if (title.StartsWith(word, StringComparison.Ordinal)) return 0;
            if (title.Contains(" " + word) || title.Contains("(" + word) || title.Contains("'" + word)) return 1;
            if (title.Contains(word)) return 2;
            if (key.Contains(word) || where.Contains(word)) return 3;
            if (help.Contains(word)) return 4;
            return -1;
        }
    }

    /// <summary>The search bar: Ctrl+F or the magnifier in the header opens it under the header's right end, over the
    /// window's content. Typing lists what matches; Up and Down move, Enter or a click goes there, Esc or a click
    /// beside it closes it. It only finds and shows: nothing in the window changes by it.</summary>
    public sealed class SearchBar
    {
        public const string BoxName = "SearchBox", RowTag = "uiframework.search.row";

        private readonly Window _window;
        private readonly Grid _layers;
        private readonly FrameworkElement _anchor;
        private readonly Func<List<SearchEntry>> _entries;
        private readonly TextBox _box = new TextBox { Name = BoxName };
        private readonly TextBlock _placeholder, _note;
        private readonly StackPanel _rows = new StackPanel();
        private readonly ScrollViewer _scroll;
        private readonly Border _panel, _catcher;
        private List<SearchEntry> _all = new List<SearchEntry>(), _shown = new List<SearchEntry>();
        private int _selected = -1;

        public bool IsOpen { get; private set; }
        /// <summary>What is listed right now, best first.</summary>
        public IReadOnlyList<SearchEntry> Results => _shown;
        public TextBox Box => _box;

        /// <param name="layers">The grid over the window's content, where the tour and the problems panel go too.</param>
        /// <param name="anchor">The header button the bar hangs under.</param>
        /// <param name="entries">Everything that can be found, read anew each time the bar opens.</param>
        public SearchBar(Window window, Grid layers, FrameworkElement anchor, Func<List<SearchEntry>> entries)
        {
            _window = window;
            _layers = layers;
            _anchor = anchor;
            _entries = entries;

            Ui.StyleInput(_box);
            _box.Height = 30;
            _box.FontSize = 13;
            AutomationProperties.SetName(_box, Text.T("search.placeholder"));
            _placeholder = new TextBlock
            {
                Text = Text.T("search.placeholder"),
                FontSize = 13,
                Foreground = Theme.MutedFg,
                IsHitTestVisible = false,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(9, 0, 0, 0)
            };
            var input = new Grid();
            input.Children.Add(_box);
            input.Children.Add(_placeholder);

            _note = new TextBlock { FontSize = 11, Foreground = Theme.MutedFg, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(2, 8, 2, 0) };
            _scroll = new ScrollViewer { Content = _rows, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 8, 0, 0), Focusable = false };
            var body = new DockPanel();
            DockPanel.SetDock(input, Dock.Top);
            DockPanel.SetDock(_note, Dock.Bottom);
            body.Children.Add(input);
            body.Children.Add(_note);
            body.Children.Add(_scroll);

            _panel = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Thickness(12, 12, 12, 10),
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(Theme.PopupBg),
                BorderBrush = new SolidColorBrush(Theme.Accent),
                BorderThickness = new Thickness(1),
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 18, ShadowDepth = 3, Opacity = 0.35 },
                Child = body
            };
            AutomationProperties.SetName(_panel, Text.T("tip.search"));
            Grid.SetColumnSpan(_panel, 99);
            Grid.SetRowSpan(_panel, 99);
            _catcher = new Border { Background = Brushes.Transparent };
            Grid.SetColumnSpan(_catcher, 99);
            Grid.SetRowSpan(_catcher, 99);
            _catcher.MouseDown += (s, e) => Close();

            _box.TextChanged += (s, e) => List();
            _box.PreviewKeyDown += OnKey;
        }

        /// <summary>Opens the bar with an empty box, or puts the cursor back into it when it is open already.</summary>
        public void Open()
        {
            if (IsOpen)
            {
                _box.Focus();
                _box.SelectAll();
                return;
            }
            IsOpen = true;
            try { _all = _entries?.Invoke() ?? new List<SearchEntry>(); }
            catch (Exception) { _all = new List<SearchEntry>(); }
            Place();
            _layers.Children.Add(_catcher);
            _layers.Children.Add(_panel);
            _box.Text = "";
            List();
            _window.Dispatcher.BeginInvoke(new Action(() => { if (IsOpen) _box.Focus(); }), DispatcherPriority.Input);
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            _layers.Children.Remove(_panel);
            _layers.Children.Remove(_catcher);
        }

        /// <summary>Goes to the selected result, or to the first when none is: what Enter does.</summary>
        public void GoToSelected()
        {
            if (_shown.Count == 0) return;
            var entry = _shown[Math.Max(0, Math.Min(_selected, _shown.Count - 1))];
            Close();
            try { entry.Go?.Invoke(); } catch (Exception) { }
        }

        /// <summary>Moves the selection by <paramref name="step"/> rows, stopping at either end.</summary>
        public void Move(int step)
        {
            if (_shown.Count == 0) return;
            Select(Math.Max(0, Math.Min(_shown.Count - 1, (_selected < 0 ? 0 : _selected) + step)));
        }

        public int Selected => _selected;

        // Under the anchor, its right edge on the anchor's; as wide and as tall as the window leaves room for.
        private void Place()
        {
            double width = _layers.ActualWidth > 0 ? _layers.ActualWidth : 700, height = _layers.ActualHeight > 0 ? _layers.ActualHeight : 600;
            _panel.Width = Math.Min(440, Math.Max(260, width - 24));
            double top = 48, right = 12;
            try
            {
                if (_anchor != null && _anchor.IsVisible)
                {
                    var at = _anchor.TransformToVisual(_layers).TransformBounds(new Rect(0, 0, _anchor.ActualWidth, _anchor.ActualHeight));
                    top = at.Bottom + 6;
                    right = Math.Max(8, width - at.Right);
                }
            }
            catch (InvalidOperationException) { }
            _panel.Margin = new Thickness(12, top, right, 0);
            _panel.MaxHeight = Math.Max(160, height - top - 16);
        }

        private void OnKey(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Escape: Close(); break;
                case Key.Enter: GoToSelected(); break;
                case Key.Down: Move(1); break;
                case Key.Up: Move(-1); break;
                case Key.PageDown: Move(6); break;
                case Key.PageUp: Move(-6); break;
                default: return;
            }
            e.Handled = true;
        }

        private void List()
        {
            string query = _box.Text ?? "";
            _placeholder.Visibility = query.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            _shown = SearchIndex.Find(_all, query);
            _rows.Children.Clear();
            _selected = -1;
            for (int i = 0; i < _shown.Count; i++)
                _rows.Children.Add(Row(_shown[i], i));
            _scroll.Visibility = _shown.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            _note.Text = query.Trim().Length == 0 ? Text.T("search.hint") : _shown.Count == 0 ? Text.T("search.none", query.Trim()) : Text.T("search.keys");
            if (_shown.Count > 0) Select(0);
        }

        private Border Row(SearchEntry entry, int index)
        {
            var title = new TextBlock { Text = entry.Title ?? entry.Key ?? "", FontSize = 13, Foreground = Theme.LabelFg, TextTrimming = TextTrimming.CharacterEllipsis };
            string kind = entry.Kind == SearchEntry.Page ? Text.T("search.kind.page") : entry.Kind == SearchEntry.Message ? Text.T("search.kind.message") : null;
            string where = string.Join("  ·  ", new[] { kind, entry.Where }.Where(t => !string.IsNullOrEmpty(t)));
            var lines = new StackPanel();
            lines.Children.Add(title);
            if (where.Length > 0)
                lines.Children.Add(new TextBlock { Text = where, FontSize = 11, Foreground = Theme.MutedFg, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 1, 0, 0) });
            var row = new Border
            {
                Tag = RowTag,
                Child = lines,
                Padding = new Thickness(9, 5, 9, 6),
                CornerRadius = new CornerRadius(5),
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                ToolTip = string.IsNullOrWhiteSpace(entry.Help) ? null : entry.Help
            };
            AutomationProperties.SetName(row, entry.Title ?? entry.Key ?? "");
            row.MouseEnter += (s, e) => Select(index);
            row.MouseLeftButtonUp += (s, e) => { Select(index); GoToSelected(); };
            return row;
        }

        private void Select(int index)
        {
            if (index == _selected) return;
            if (_selected >= 0 && _selected < _rows.Children.Count)
                ((Border)_rows.Children[_selected]).Background = Brushes.Transparent;
            _selected = index;
            if (index < 0 || index >= _rows.Children.Count) return;
            var row = (Border)_rows.Children[index];
            row.Background = new SolidColorBrush(Theme.Hover);
            row.BringIntoView();
        }

        /// <summary>Scrolls <paramref name="element"/> into view, rings it in the accent colour for a moment and puts
        /// the cursor into its first control: what going to a found setting looks like.</summary>
        public static void Reveal(Window window, Grid layers, FrameworkElement element)
        {
            if (element == null) return;
            window.Dispatcher.BeginInvoke(new Action(() =>
            {
                element.BringIntoView();
                window.Dispatcher.BeginInvoke(new Action(() =>
                {
                    Ring(layers, element);
                    try { element.MoveFocus(new TraversalRequest(FocusNavigationDirection.First)); } catch (Exception) { }
                }), DispatcherPriority.Loaded);
            }), DispatcherPriority.Loaded);
        }

        // A ring around the element, over the window's content, that fades and takes itself away.
        private static void Ring(Grid layers, FrameworkElement element)
        {
            if (!element.IsVisible || element.ActualWidth <= 0 || element.ActualHeight <= 0) return;
            Rect at;
            try { at = element.TransformToVisual(layers).TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight)); }
            catch (InvalidOperationException) { return; }
            at.Inflate(5, 4);
            var ring = new Border
            {
                IsHitTestVisible = false,
                BorderBrush = new SolidColorBrush(Theme.Accent),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(7),
                Background = new SolidColorBrush(Color.FromArgb(28, Theme.Accent.R, Theme.Accent.G, Theme.Accent.B)),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Width = Math.Max(8, at.Width),
                Height = Math.Max(8, at.Height),
                Margin = new Thickness(Math.Max(0, at.Left), Math.Max(0, at.Top), 0, 0)
            };
            Grid.SetColumnSpan(ring, 99);
            Grid.SetRowSpan(ring, 99);
            layers.Children.Add(ring);
            var fade = new DoubleAnimation(1.0, 0.0, new Duration(TimeSpan.FromMilliseconds(900))) { BeginTime = TimeSpan.FromMilliseconds(1100) };
            fade.Completed += (s, e) => layers.Children.Remove(ring);
            ring.BeginAnimation(UIElement.OpacityProperty, fade);
        }
    }
}
