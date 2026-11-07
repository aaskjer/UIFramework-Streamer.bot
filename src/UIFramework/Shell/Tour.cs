using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace UIFramework
{
    /// <summary>
    /// The window's tour: the window dimmed but for one part at a time, ringed in the accent colour, and a note beside
    /// it with a heading, a text and Back / Next. The window stays as it is underneath and takes no clicks while the
    /// tour is up; Esc ends it, the arrow keys and Enter step through it.
    /// </summary>
    public sealed class Tour
    {
        /// <summary>One stop as the shell resolved it: the texts in the window's language, the part to point at (null:
        /// a note in the middle) and what brings that part into view first (its page in front).</summary>
        public sealed class Stop
        {
            public string Title, Text;
            public Func<FrameworkElement> Target;
            public Action Show;
            /// <summary>Left bright beside the target, not ringed: a page's content while its rail entry is ringed.
            /// The note then sits at the foot of this area.</summary>
            public Func<FrameworkElement> Area;
        }

        private readonly Window _window;
        private readonly Grid _host;
        private readonly List<Stop> _stops;
        private readonly Action _ended;
        private readonly Canvas _canvas = new Canvas { Background = Brushes.Transparent, ClipToBounds = true };
        private readonly Path _shade = new Path { IsHitTestVisible = false };
        private readonly Border _ring = new Border { IsHitTestVisible = false, BorderThickness = new Thickness(2), CornerRadius = new CornerRadius(7) };
        private readonly Border _note;
        private readonly TextBlock _count, _title, _text;
        private readonly Button _back, _next, _skip;
        private int _index;
        private bool _open;

        /// <summary>Whether a tour is up in this window: the shell's own shortcuts wait while it is.</summary>
        public bool Open => _open;

        private Tour(Window window, Grid host, List<Stop> stops, Action ended)
        {
            _window = window;
            _host = host;
            _stops = stops;
            _ended = ended;
            Grid.SetColumnSpan(_canvas, 99);
            Grid.SetRowSpan(_canvas, 99);
            _shade.Fill = new SolidColorBrush(Color.FromArgb(Theme.IsDark ? (byte)170 : (byte)140, 0, 0, 0));
            _ring.BorderBrush = new SolidColorBrush(Theme.Accent);

            _count = new TextBlock { FontSize = 11, Foreground = Theme.MutedFg, Margin = new Thickness(0, 0, 0, 4) };
            _title = new TextBlock { FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = Theme.LabelFg, TextWrapping = TextWrapping.Wrap };
            _text = new TextBlock { FontSize = 13, Foreground = Theme.LabelFg, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0), LineHeight = 19 };
            _skip = Ui.TextButton(Text.T("tour.skip"), false);
            _skip.Margin = new Thickness(0);
            _back = Ui.TextButton(Text.T("tour.back"), false);
            _next = Ui.TextButton(Text.T("tour.next"), true);
            _skip.Click += (s, e) => End();
            _back.Click += (s, e) => Go(_index - 1);
            _next.Click += (s, e) => Go(_index + 1);
            var buttons = new DockPanel { Margin = new Thickness(0, 14, 0, 0), LastChildFill = false };
            DockPanel.SetDock(_skip, Dock.Left);
            DockPanel.SetDock(_next, Dock.Right);
            DockPanel.SetDock(_back, Dock.Right);
            buttons.Children.Add(_skip);
            buttons.Children.Add(_next);
            buttons.Children.Add(_back);
            var body = new StackPanel();
            body.Children.Add(_count);
            body.Children.Add(_title);
            body.Children.Add(_text);
            body.Children.Add(buttons);
            _note = new Border
            {
                Width = 340,
                Padding = new Thickness(16, 12, 16, 14),
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(Theme.PopupBg),
                BorderBrush = new SolidColorBrush(Theme.Accent),
                BorderThickness = new Thickness(1),
                Child = body,
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 18, ShadowDepth = 3, Opacity = 0.35 }
            };
            System.Windows.Automation.AutomationProperties.SetName(_note, Text.T("tip.tour"));
            KeyboardNavigation.SetTabNavigation(_note, KeyboardNavigationMode.Cycle);
            _canvas.Children.Add(_shade);
            _canvas.Children.Add(_ring);
            _canvas.Children.Add(_note);
            _canvas.SizeChanged += (s, e) => Place();
        }

        /// <summary>Lays the tour over <paramref name="host"/> (the grid that holds the window's content) and shows its
        /// first stop. <paramref name="ended"/> runs once when it closes, however it closes.</summary>
        public static Tour Start(Window window, Grid host, List<Stop> stops, Action ended)
        {
            if (stops == null || stops.Count == 0) return null;
            var tour = new Tour(window, host, stops, ended);
            host.Children.Add(tour._canvas);
            window.PreviewKeyDown += tour.OnKey;
            tour._open = true;
            tour.Go(0);
            return tour;
        }

        /// <summary>Closes the tour, wherever it stands.</summary>
        public void End()
        {
            if (!_open) return;
            _open = false;
            _window.PreviewKeyDown -= OnKey;
            _host.Children.Remove(_canvas);
            try { _ended?.Invoke(); } catch (Exception) { }
        }

        public int Index => _index;
        public int Count => _stops.Count;

        /// <summary>To the stop at <paramref name="index"/>: past the last one the tour ends.</summary>
        public void Go(int index)
        {
            if (!_open) return;
            if (index >= _stops.Count) { End(); return; }
            _index = Math.Max(0, index);
            var stop = _stops[_index];
            try { stop.Show?.Invoke(); } catch (Exception) { }
            _count.Text = Text.T("tour.step", _index + 1, _stops.Count);
            _title.Text = stop.Title ?? "";
            _title.Visibility = string.IsNullOrEmpty(stop.Title) ? Visibility.Collapsed : Visibility.Visible;
            _text.Text = stop.Text ?? "";
            _text.Visibility = string.IsNullOrEmpty(stop.Text) ? Visibility.Collapsed : Visibility.Visible;
            _back.Visibility = _index > 0 ? Visibility.Visible : Visibility.Collapsed;
            bool last = _index == _stops.Count - 1;
            _next.Content = last ? Text.T("tour.done") : Text.T("tour.next");
            _skip.Visibility = last ? Visibility.Hidden : Visibility.Visible;
            Place();
            // The page that came to the front settles first; then its field is scrolled into view and the ring follows.
            _window.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!_open) return;
                Target(stop)?.BringIntoView();
                _window.Dispatcher.BeginInvoke(new Action(Place), DispatcherPriority.Loaded);
            }), DispatcherPriority.Loaded);
            _window.Dispatcher.BeginInvoke(new Action(() => { if (_open) _next.Focus(); }), DispatcherPriority.Input);
        }

        private static FrameworkElement Target(Stop stop)
        {
            try { return stop.Target?.Invoke(); }
            catch (Exception) { return null; }
        }

        /// <summary>Where <paramref name="element"/> shows on the canvas, widened by <paramref name="pad"/>; null when
        /// it does not show.</summary>
        private Rect? Bounds(FrameworkElement element, double pad)
        {
            if (element == null || !element.IsVisible || element.ActualWidth <= 0 || element.ActualHeight <= 0) return null;
            try
            {
                var bounds = element.TransformToVisual(_canvas).TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
                bounds = Visible(element, bounds);
                bounds.Inflate(pad, pad);
                bounds.Intersect(new Rect(2, 2, Math.Max(0, _canvas.ActualWidth - 4), Math.Max(0, _canvas.ActualHeight - 4)));
                return !bounds.IsEmpty && bounds.Width > 4 && bounds.Height > 4 ? bounds : (Rect?)null;
            }
            catch (InvalidOperationException) { return null; }   // not in this window's tree (yet)
        }

        private void OnKey(object sender, KeyEventArgs e)
        {
            if (!_open) return;
            switch (e.Key)
            {
                case Key.Escape: End(); break;
                case Key.Enter:
                    // Enter on Back or End tour is theirs; anywhere else it goes on.
                    if (Keyboard.FocusedElement == _back || Keyboard.FocusedElement == _skip) return;
                    Go(_index + 1);
                    break;
                case Key.Right: case Key.PageDown: Go(_index + 1); break;
                case Key.Left: case Key.PageUp: Go(_index - 1); break;
                case Key.Tab: case Key.Space: return;   // between the note's buttons, and pressing one
                default:
                    // Nothing else reaches the window underneath while the tour is up, Ctrl+S and Ctrl+1 included.
                    break;
            }
            e.Handled = true;
        }

        /// <summary>The ring around the stop's part, the shade around the ring, the note beside it where it fits.</summary>
        private void Place()
        {
            if (!_open) return;
            double width = _canvas.ActualWidth, height = _canvas.ActualHeight;
            if (width <= 0 || height <= 0) return;
            var whole = new Rect(0, 0, width, height);
            var stop = _stops[_index];
            Rect? hole = Bounds(Target(stop), 5);
            Rect? area = null;
            try { area = Bounds(stop.Area?.Invoke(), 0); } catch (Exception) { }

            var holes = new GeometryGroup { FillRule = FillRule.Nonzero };
            if (hole != null) holes.Children.Add(new RectangleGeometry(hole.Value, 7, 7));
            if (area != null) holes.Children.Add(new RectangleGeometry(area.Value));
            _shade.Data = holes.Children.Count == 0
                ? (Geometry)new RectangleGeometry(whole)
                : new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(whole), holes);
            if (hole != null)
            {
                _ring.Visibility = Visibility.Visible;
                _ring.Width = hole.Value.Width;
                _ring.Height = hole.Value.Height;
                Canvas.SetLeft(_ring, hole.Value.Left);
                Canvas.SetTop(_ring, hole.Value.Top);
            }
            else
                _ring.Visibility = Visibility.Collapsed;

            _note.Width = Math.Min(340, Math.Max(200, width - 24));
            _note.Measure(new Size(_note.Width, double.PositiveInfinity));
            double w = _note.DesiredSize.Width, h = _note.DesiredSize.Height, gap = 12, margin = 10;
            double left, top;
            if (area != null)
            {
                // A page: the note at the foot of its content, on the right, where pages are most often empty.
                left = area.Value.Right - w - 16;
                top = area.Value.Bottom - h - 16;
            }
            else if (hole == null)
            {
                left = (width - w) / 2;
                top = (height - h) / 2;
            }
            else
            {
                var r = hole.Value;
                if (r.Bottom + gap + h <= height - margin) { top = r.Bottom + gap; left = r.Left + r.Width / 2 - w / 2; }
                else if (r.Top - gap - h >= margin) { top = r.Top - gap - h; left = r.Left + r.Width / 2 - w / 2; }
                else if (r.Right + gap + w <= width - margin) { left = r.Right + gap; top = r.Top + r.Height / 2 - h / 2; }
                else if (r.Left - gap - w >= margin) { left = r.Left - gap - w; top = r.Top + r.Height / 2 - h / 2; }
                else { left = (width - w) / 2; top = height - h - margin; }   // a part as big as the window: the note at its foot
            }
            Canvas.SetLeft(_note, Math.Max(margin, Math.Min(left, width - w - margin)));
            Canvas.SetTop(_note, Math.Max(margin, Math.Min(top, height - h - margin)));
        }

        /// <summary>The part of <paramref name="bounds"/> its scrolling page shows: a tall field ringed only where it
        /// can be seen.</summary>
        private Rect Visible(FrameworkElement target, Rect bounds)
        {
            DependencyObject node = target;
            while (node != null)
            {
                node = VisualTreeHelper.GetParent(node);
                if (node is ScrollViewer viewer && viewer.ActualHeight > 0)
                {
                    var view = viewer.TransformToVisual(_canvas).TransformBounds(new Rect(0, 0, viewer.ActualWidth, viewer.ActualHeight));
                    var shown = Rect.Intersect(bounds, view);
                    return shown.IsEmpty ? bounds : shown;
                }
            }
            return bounds;
        }
    }
}
