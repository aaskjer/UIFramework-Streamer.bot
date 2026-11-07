using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace UIFramework
{
    /// <summary>
    /// Everything that went wrong in a window, in one place: what the game declared that does not fit (the schema's
    /// problems, found again on every resolve) and what failed while the window was open (a page that could not be
    /// built, a handler that threw, a save that failed). A red "i" in the header shows that there is something, and
    /// a panel under it lists it all. Streamer.bot's log has every line as well.
    /// </summary>
    public sealed class ProblemList
    {
        private readonly Func<IReadOnlyList<string>> _declared;
        private readonly List<(DateTime At, string Text)> _occurred = new List<(DateTime, string)>();
        private const int Kept = 100;

        /// <param name="declared">The schema's problems as they stand now (they are found again on every rebuild).</param>
        public ProblemList(Func<IReadOnlyList<string>> declared) { _declared = declared ?? (() => new string[0]); }

        /// <summary>Runs after <see cref="Add"/>, on the thread that added.</summary>
        public event Action Changed;

        public IReadOnlyList<string> Declared => _declared() ?? new string[0];
        public IReadOnlyList<(DateTime At, string Text)> Occurred => _occurred;
        public int Count => Declared.Count + _occurred.Count;

        /// <summary>Something that failed while the window was open. The same text twice in a row counts once.</summary>
        public void Add(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            text = text.Trim();
            lock (_occurred)
            {
                if (_occurred.Count > 0 && _occurred[_occurred.Count - 1].Text == text) return;
                _occurred.Add((DateTime.Now, text));
                if (_occurred.Count > Kept) _occurred.RemoveAt(0);
            }
            Changed?.Invoke();
        }

        /// <summary>The whole list as plain text, for the clipboard and a bug report.</summary>
        public string AsText(string title)
        {
            var lines = new List<string> { title };
            if (Declared.Count > 0)
            {
                lines.Add("");
                lines.Add(Text.T("problems.declared") + ":");
                lines.AddRange(Declared.Select(p => "- " + p));
            }
            if (_occurred.Count > 0)
            {
                lines.Add("");
                lines.Add(Text.T("problems.occurred") + ":");
                lines.AddRange(_occurred.Select(p => "- " + p.At.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + "  " + p.Text));
            }
            return string.Join(Environment.NewLine, lines);
        }
    }

    /// <summary>The red "i" in the header and the panel it opens.</summary>
    public static class ProblemsView
    {
        /// <summary>A red round button with a white "i"; hidden while there is nothing to show.</summary>
        public static Button Button()
        {
            var face = new Grid { Width = 22, Height = 22 };
            face.Children.Add(new Ellipse { Fill = new SolidColorBrush(Theme.Danger) });
            face.Children.Add(new TextBlock
            {
                Text = "i",
                Foreground = Brushes.White,
                FontFamily = new FontFamily("Georgia"),
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, -1, 0, 0)
            });
            var template = new ControlTemplate(typeof(Button));
            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            template.VisualTree = presenter;
            var button = new Button
            {
                Content = face,
                Template = template,
                Cursor = Cursors.Hand,
                Width = 28,
                Height = 28,
                Focusable = true,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed
            };
            System.Windows.Automation.AutomationProperties.SetName(button, Text.T("problems.title"));
            return button;
        }

        /// <summary>Shows or hides the button and says in its tooltip how many there are.</summary>
        public static void Update(Button button, ProblemList problems)
        {
            int n = problems.Count;
            button.Visibility = n > 0 ? Visibility.Visible : Visibility.Collapsed;
            button.ToolTip = n == 1 ? Text.T("problems.tip.one") : Text.T("problems.tip.many", n);
        }

        /// <summary>The panel under <paramref name="anchor"/>, over the window's content in <paramref name="layers"/>;
        /// a click outside it, Esc or its close button takes it away. Returns the action that closes it.</summary>
        public static Action Open(Window window, Grid layers, FrameworkElement anchor, ProblemList problems)
        {
            if (problems.Count == 0) return () => { };   // nothing to list, and the button is hidden anyway
            var catcher = new Border { Background = Brushes.Transparent };
            Grid.SetColumnSpan(catcher, 99);
            Grid.SetRowSpan(catcher, 99);

            var list = new StackPanel();
            string title = Text.T("problems.title") + " (" + problems.Count + ")";
            var copy = Ui.TextButton(Text.T("problems.copy"), false);
            copy.Height = 26;
            var close = Ui.IconButton(Icons.Remove, Icons.Font, Text.T("problems.close"));
            close.Margin = new Thickness(6, 0, 0, 0);
            var head = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
            DockPanel.SetDock(close, Dock.Right);
            DockPanel.SetDock(copy, Dock.Right);
            head.Children.Add(close);
            head.Children.Add(copy);
            head.Children.Add(new TextBlock { Text = title, FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = Theme.LabelFg, VerticalAlignment = VerticalAlignment.Center });
            list.Children.Add(head);

            void Group(string heading, string note, IEnumerable<(string Time, string Text)> items)
            {
                list.Children.Add(new TextBlock { Text = heading, FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = Theme.SubFg, Margin = new Thickness(0, 6, 0, 2) });
                if (note != null)
                    list.Children.Add(new TextBlock { Text = note, FontSize = 12, Foreground = Theme.MutedFg, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 4) });
                foreach (var item in items)
                {
                    var line = new DockPanel { Margin = new Thickness(0, 5, 0, 0) };
                    var dot = new Ellipse { Width = 7, Height = 7, Fill = new SolidColorBrush(Theme.Danger), VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 6, 9, 0) };
                    DockPanel.SetDock(dot, Dock.Left);
                    line.Children.Add(dot);
                    if (item.Time != null)
                    {
                        var time = new TextBlock { Text = item.Time, FontSize = 12, Foreground = Theme.MutedFg, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Top };
                        DockPanel.SetDock(time, Dock.Left);
                        line.Children.Add(time);
                    }
                    line.Children.Add(new TextBlock { Text = item.Text, FontSize = 13, Foreground = Theme.LabelFg, TextWrapping = TextWrapping.Wrap });
                    list.Children.Add(line);
                }
            }
            if (problems.Declared.Count > 0)
                Group(Text.T("problems.declared"), Text.T("problems.declared.note"), problems.Declared.Select(p => ((string)null, p)));
            if (problems.Occurred.Count > 0)
                Group(Text.T("problems.occurred"), null, problems.Occurred.Reverse().Select(p => (p.At.ToString("HH:mm:ss", CultureInfo.InvariantCulture), p.Text)));
            list.Children.Add(new TextBlock { Text = Text.T("problems.log"), FontSize = 11, Foreground = Theme.MutedFg, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 0) });

            var panel = new Border
            {
                Width = Math.Min(460, Math.Max(260, layers.ActualWidth - 24)),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Thickness(16, 12, 12, 14),
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(Theme.PopupBg),
                BorderBrush = new SolidColorBrush(Theme.Danger),
                BorderThickness = new Thickness(1),
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 18, ShadowDepth = 3, Opacity = 0.35 },
                Child = new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(0, 0, 4, 0) }
            };
            Grid.SetColumnSpan(panel, 99);
            Grid.SetRowSpan(panel, 99);
            // Under the button, its right edge on the button's.
            try
            {
                var at = anchor.TransformToVisual(layers).TransformBounds(new Rect(0, 0, anchor.ActualWidth, anchor.ActualHeight));
                double top = at.Bottom + 6;
                panel.Margin = new Thickness(12, top, Math.Max(8, layers.ActualWidth - at.Right), 0);
                panel.MaxHeight = Math.Max(160, layers.ActualHeight - top - 16);
            }
            catch (InvalidOperationException) { panel.Margin = new Thickness(12, 48, 12, 0); }

            bool open = true;
            KeyEventHandler keys = null;
            void Close()
            {
                if (!open) return;
                open = false;
                window.PreviewKeyDown -= keys;
                layers.Children.Remove(panel);
                layers.Children.Remove(catcher);
            }
            keys = (s, e) => { if (e.Key == Key.Escape) { Close(); e.Handled = true; } };
            window.PreviewKeyDown += keys;
            catcher.MouseDown += (s, e) => Close();
            close.Click += (s, e) => Close();
            copy.Click += (s, e) =>
            {
                try { Clipboard.SetText(problems.AsText(title)); copy.Content = Text.T("problems.copied"); }
                catch (Exception) { }   // the clipboard held by another program: nothing copied, nothing broken
            };
            layers.Children.Add(catcher);
            layers.Children.Add(panel);
            return Close;
        }
    }
}
