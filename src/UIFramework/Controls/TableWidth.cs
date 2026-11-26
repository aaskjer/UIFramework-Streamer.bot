using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;

namespace UIFramework
{
    /// <summary>
    /// How a table meets its width (<c>fitWidth</c>). Kept within it: no horizontal scrollbar, a text too long for
    /// its column ends in an ellipsis, and whatever would make the columns wider than the table (longer contents, a
    /// column the user drags) is taken back from the widest ones. Or free to run past it: every column as wide as
    /// its contents, under a horizontal scrollbar.
    /// </summary>
    public static class TableWidth
    {
        /// <summary>A column narrower than this is left alone when room is taken back.</summary>
        public const double Least = 40;

        /// <summary>The scroll viewer of a table's rows, once its template is there.</summary>
        public static ScrollViewer Viewer(DataGrid grid)
        {
            ScrollViewer found = null;
            void Look(DependencyObject node)
            {
                for (int i = 0; found == null && i < VisualTreeHelper.GetChildrenCount(node); i++)
                {
                    var child = VisualTreeHelper.GetChild(node, i);
                    if (child is ScrollViewer viewer && viewer.TemplatedParent == grid) found = viewer;
                    else Look(child);
                }
            }
            Look(grid);
            return found;
        }

        /// <summary>The drag of a column's edge in the header, not of a scrollbar's thumb.</summary>
        public static bool IsColumnDrag(object source) => source is Thumb thumb && thumb.TemplatedParent is DataGridColumnHeader;

        /// <summary>An ellipsis where a cell's text does not fit its column.</summary>
        public static void Trim(Style cellText) => cellText.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis));

        /// <summary>Lets a table run past its width: a horizontal scrollbar, and the last column as wide as its
        /// contents but at least up to the table's edge, so short rows leave no empty strip.</summary>
        public static void RunOn(DataGrid grid, DataGridColumn last, double least)
        {
            grid.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
            last.Width = DataGridLength.Auto;
            last.MinWidth = least;
            bool pending = false;
            void Fill()
            {
                pending = false;
                var viewer = Viewer(grid);
                if (viewer == null || viewer.ViewportWidth <= 0) return;
                double others = grid.Columns.Where(c => c != last && c.Visibility == Visibility.Visible).Sum(c => c.ActualWidth);
                double wanted = Math.Max(least, Math.Floor(viewer.ViewportWidth - others));
                if (Math.Abs(last.MinWidth - wanted) > 0.5) last.MinWidth = wanted;
            }
            void Later()
            {
                if (pending) return;
                pending = true;
                grid.Dispatcher.BeginInvoke(new Action(Fill), DispatcherPriority.Loaded);
            }
            grid.SizeChanged += (s, e) => Later();
            grid.LoadingRow += (s, e) => Later();
            grid.AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler((s, e) => { if (IsColumnDrag(e.OriginalSource)) Later(); }), true);
        }

        /// <summary>Keeps the columns of a table within its width. For a table whose columns size themselves (by
        /// their contents, one of them taking what is left): what does not fit is taken from the widest columns,
        /// from the one the user dragged last. A wider table gives the columns their own widths back.</summary>
        public static void KeepWithin(DataGrid grid)
        {
            grid.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            var own = new Dictionary<DataGridColumn, DataGridLength>();   // what a column was before room was taken from it
            DataGridColumn dragged = null;
            Dictionary<DataGridColumn, double> held = null;
            bool pending = false, busy = false;

            void Squeeze()
            {
                pending = false;
                if (busy) return;
                var viewer = Viewer(grid);
                if (viewer == null || viewer.ViewportWidth <= 0) return;
                var columns = grid.Columns.Where(c => c.Visibility == Visibility.Visible).ToList();
                var widths = columns.ToDictionary(c => c, c => c.ActualWidth);
                double over = widths.Values.Sum() - viewer.ViewportWidth;
                if (over <= 0.5) return;
                double Floor(DataGridColumn c) => Math.Max(c.MinWidth, Least);
                var give = columns.Where(c => c != dragged).ToList();
                while (over > 0.5)
                {
                    var wide = give.Where(c => widths[c] > Floor(c) + 0.5).OrderByDescending(c => widths[c]).ToList();
                    if (wide.Count == 0)
                    {
                        // Nothing left to take but from the column the user made wider.
                        if (dragged == null || give.Contains(dragged) || !columns.Contains(dragged)) break;
                        give.Add(dragged);
                        continue;
                    }
                    // Down to the next widest, so the widest columns end alike instead of one giving everything.
                    double level = Math.Max(Floor(wide[0]), wide.Count > 1 ? widths[wide[1]] : 0);
                    double take = Math.Min(over, Math.Max(1, widths[wide[0]] - level));
                    take = Math.Min(take, widths[wide[0]] - Floor(wide[0]));
                    widths[wide[0]] -= take;
                    over -= take;
                }
                busy = true;
                try
                {
                    foreach (var column in columns)
                    {
                        if (Math.Abs(widths[column] - column.ActualWidth) <= 0.5) continue;
                        if (column != dragged && !own.ContainsKey(column)) own[column] = column.Width;
                        column.Width = new DataGridLength(Math.Floor(widths[column]));
                    }
                }
                finally { busy = false; }
            }
            void Later()
            {
                if (pending) return;
                pending = true;
                grid.Dispatcher.BeginInvoke(new Action(Squeeze), DispatcherPriority.Loaded);
            }

            grid.AddHandler(Thumb.DragStartedEvent, new DragStartedEventHandler((s, e) =>
            {
                if (IsColumnDrag(e.OriginalSource)) held = grid.Columns.ToDictionary(c => c, c => c.ActualWidth);
            }), true);
            grid.AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler((s, e) =>
            {
                if (held == null) return;
                var before = held;
                held = null;
                var moved = grid.Columns.FirstOrDefault(c => before.TryGetValue(c, out double was) && Math.Abs(c.ActualWidth - was) > 0.5);
                if (moved == null) return;
                dragged = moved;
                own.Remove(moved);   // the width is the user's now
                Later();
            }), true);
            grid.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler((s, e) =>
            {
                if (e.ViewportWidthChange == 0 || !(e.OriginalSource is ScrollViewer viewer) || viewer.TemplatedParent != grid) return;
                if (e.ViewportWidthChange > 0 && own.Count > 0)
                {
                    foreach (var pair in own.ToList()) pair.Key.Width = pair.Value;
                    own.Clear();
                }
                Later();
            }));
            grid.LoadingRow += (s, e) => Later();     // longer contents come into view
            grid.SizeChanged += (s, e) => Later();
        }
    }
}
