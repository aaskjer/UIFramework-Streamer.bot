using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Newtonsoft.Json.Linq;

namespace UIFramework
{
    /// <summary>
    /// The table layout of a list with many or long columns (a question editor): every row one line of a grid,
    /// edited where it stands. A click into a cell edits it, a switch column is a checkbox in every row that toggles
    /// with one click, a read-only column only shows. A search box sits above; Add row, Remove row (the picked rows,
    /// or Delete) and Restore defaults below. A row keeps every property it came with, so keys the game writes and no
    /// column shows (a timestamp) survive a save. With fixed rows there is no Add row, Remove row or Restore defaults,
    /// and Delete removes nothing; a filter (<see cref="RowFilter"/>) says which rows show, and the search and the
    /// count work on those. A JSON array in a read-only cell shows as "a, b" and stays the array it is.
    /// </summary>
    public sealed class ListTable
    {
        private readonly List<SettingColumn> _columns;
        private readonly JArray _defaults;
        private readonly Action _notify;
        private readonly ObservableCollection<Record> _records = new ObservableCollection<Record>();
        private readonly ICollectionView _view;
        private readonly DataGrid _grid;
        private readonly TextBox _search;
        private readonly TextBlock _count;
        private readonly TextBlock _empty;
        private readonly Button _remove;
        private readonly bool _fixedRows;
        private bool _loading;
        private Func<JObject, bool> _rowFilter;

        public readonly FrameworkElement Element;

        /// <summary>A list uses the table once a row would not fit one line: a multi-line column, or more than three.</summary>
        public static bool Suits(List<SettingColumn> columns) =>
            columns != null && (columns.Count > 3 || columns.Any(c => c.Kind == "text"));

        /// <summary>One row: the object as stored, read and written by column key.</summary>
        private sealed class Record : INotifyPropertyChanged
        {
            public readonly JObject Data;
            public int Number;
            /// <summary>The list's filter does not show it.</summary>
            public bool Out;
            private readonly List<SettingColumn> _columns;
            private readonly Action _changed;
            public Record(JObject data, List<SettingColumn> columns, Action changed) { Data = data; _columns = columns; _changed = changed; }
            public event PropertyChangedEventHandler PropertyChanged;

            /// <summary>A switch column as a bool, any other as its text; set from the cell, typed by the column's kind.</summary>
            public object this[string key]
            {
                get
                {
                    if (key == "#") return Number.ToString(CultureInfo.InvariantCulture);
                    var column = _columns.FirstOrDefault(c => c.Key == key);
                    var token = Data[key];
                    if (column?.Kind == "bool")
                        return token != null && token.Type == JTokenType.Boolean && (bool)token;
                    // An array the project keeps in a cell the user only reads: its elements, not its JSON.
                    if (column != null && column.ReadOnly && token is JArray array)
                        return ListMerge.Shown(array);
                    return token == null || token.Type == JTokenType.Null ? "" : token.Type == JTokenType.String ? (string)token : token.ToString(Newtonsoft.Json.Formatting.None);
                }
                set
                {
                    var column = _columns.FirstOrDefault(c => c.Key == key);
                    if (column == null || column.ReadOnly) return;
                    JToken typed = Typed(column, value);
                    if (typed == null)
                    {
                        // Not a number in a number column: the cell goes back to what it held.
                        Refresh();
                        return;
                    }
                    if (JToken.DeepEquals(Data[key], typed)) return;
                    Data[key] = typed;
                    Refresh();
                    _changed();
                }
            }

            public string Haystack => string.Join(" ", _columns.Where(c => c.Kind != "bool").Select(c => (string)this[c.Key]));

            public bool Filled => _columns.Any(c =>
            {
                var t = Data[c.Key];
                if (t == null || t.Type == JTokenType.Null) return false;
                if (t.Type == JTokenType.Boolean) return (bool)t;
                return t.ToString().Trim().Length > 0;
            });

            public void Refresh() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(Binding.IndexerName));
        }

        public ListTable(string label, List<SettingColumn> columns, JArray defaults, Action notify, bool fixedRows = false)
        {
            _columns = columns;
            _defaults = defaults;
            _notify = notify;
            _fixedRows = fixedRows;
            _view = CollectionViewSource.GetDefaultView(_records);

            var panel = new StackPanel();

            _search = new TextBox { ToolTip = Text.T("list.search.tip") };
            Ui.StyleInput(_search);
            AutomationProperties.SetName(_search, Text.T("list.search", label));
            var searchHost = Ui.WithHint(_search, Text.T("list.search", label));
            searchHost.Margin = new Thickness(0, 0, 0, 8);
            _search.TextChanged += (s, e) => { CommitEdit(); _view.Refresh(); Count(); };
            _view.Filter = o =>
            {
                string needle = (_search.Text ?? "").Trim();
                return !((Record)o).Out && (needle.Length == 0 || ((Record)o).Haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0);
            };
            panel.Children.Add(searchHost);

            var lines = new SolidColorBrush(Theme.Line);
            _grid = new DataGrid
            {
                AutoGenerateColumns = false,
                IsReadOnly = false,
                CanUserAddRows = false,
                CanUserDeleteRows = false,
                CanUserSortColumns = false,
                CanUserReorderColumns = false,
                SelectionMode = DataGridSelectionMode.Extended,
                SelectionUnit = DataGridSelectionUnit.FullRow,
                GridLinesVisibility = DataGridGridLinesVisibility.All,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                RowHeaderWidth = 0,
                ItemsSource = _view,
                FontSize = 12,
                Height = 340,
                Background = new SolidColorBrush(Theme.ContentBg),
                RowBackground = new SolidColorBrush(Theme.ContentBg),
                AlternatingRowBackground = new SolidColorBrush(Theme.Surface),
                AlternationCount = 2,
                Foreground = Theme.LabelFg,
                BorderBrush = lines,
                BorderThickness = new Thickness(1),
                HorizontalGridLinesBrush = lines,
                VerticalGridLinesBrush = lines,
                ColumnHeaderStyle = Ui.HeaderStyle(),
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            AutomationProperties.SetName(_grid, label);
            Ui.StyleSelection(_grid);
            // A click into a cell starts editing it at once, as in a spreadsheet; no double-click needed.
            var cellStyle = new Style(typeof(DataGridCell), _grid.CellStyle);
            cellStyle.Setters.Add(new EventSetter(UIElement.GotFocusEvent, new RoutedEventHandler((s, e) =>
            {
                if (s is DataGridCell cell && !cell.IsEditing && !cell.IsReadOnly && !(cell.Column is DataGridTemplateColumn))
                    _grid.BeginEdit();
            })));
            _grid.CellStyle = cellStyle;

            var shown = new Style(typeof(TextBlock));
            shown.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(5, 4, 5, 4)));
            shown.Setters.Add(new Setter(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center));
            shown.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis));
            var muted = new Style(typeof(TextBlock), shown);
            muted.Setters.Add(new Setter(TextBlock.ForegroundProperty, Theme.MutedFg));
            var editing = new Style(typeof(TextBox));
            editing.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Theme.InputBg)));
            editing.Setters.Add(new Setter(Control.ForegroundProperty, new SolidColorBrush(Theme.InputFg)));
            editing.Setters.Add(new Setter(TextBoxBase.CaretBrushProperty, new SolidColorBrush(Theme.InputFg)));
            editing.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
            editing.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(3, 3, 3, 3)));

            _grid.Columns.Add(new DataGridTextColumn { Header = "", Binding = new Binding("[#]") { Mode = BindingMode.OneWay }, Width = new DataGridLength(30), IsReadOnly = true, ElementStyle = muted });
            var shares = new List<(DataGridColumn Column, double Share)>();
            foreach (var column in _columns)
            {
                DataGridColumn gridColumn;
                if (column.Kind == "bool")
                    gridColumn = SwitchColumn(column);
                else
                {
                    bool number = column.Kind == "int" || column.Kind == "long" || column.Kind == "double";
                    gridColumn = new DataGridTextColumn
                    {
                        Header = column.Label ?? column.Key,
                        Binding = new Binding("[" + column.Key + "]") { Mode = column.ReadOnly ? BindingMode.OneWay : BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.LostFocus },
                        IsReadOnly = column.ReadOnly,
                        ElementStyle = column.ReadOnly ? muted : shown,
                        EditingElementStyle = number ? NumberEditing(editing, column.Kind != "double") : editing
                    };
                }
                gridColumn.MinWidth = 30;
                _grid.Columns.Add(gridColumn);
                shares.Add((gridColumn, column.Kind == "text" ? 3.0 : column.Kind == "bool" ? 0.45 : column.Kind == "int" || column.Kind == "long" || column.Kind == "double" ? 0.7 : 1.0));
            }
            // Shares of the width in pixels once the grid knows its width: star columns collapse when a page measures
            // them without one. The width is the rows' viewport, so the vertical scrollbar counts only while it shows,
            // and it follows the grid's size and the scrollbar coming and going as rows are added or removed. What the
            // whole pixels leave over goes to the widest column, so the last one ends at the grid's edge and no empty
            // filler column shows after it.
            _grid.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler((s, e) =>
            {
                if (e.ViewportWidthChange == 0 || !(e.OriginalSource is ScrollViewer viewer) || viewer.TemplatedParent != _grid) return;
                double room = Math.Floor(e.ViewportWidth) - 30;
                if (room <= 0) return;
                double total = shares.Sum(x => x.Share);
                var widths = shares.Select(x => Math.Max(30, Math.Floor(room * x.Share / total))).ToList();
                double left = room - widths.Sum();
                if (left > 0)
                {
                    int widest = widths.IndexOf(widths.Max());
                    widths[widest] += left;
                }
                for (int i = 0; i < shares.Count; i++)
                    shares[i].Column.Width = new DataGridLength(widths[i]);
            }));
            _grid.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Delete && !IsEditing() && !_fixedRows)
                {
                    RemoveSelected();
                    e.Handled = true;
                }
            };

            _empty = new TextBlock
            {
                FontSize = 12,
                Foreground = Theme.MutedFg,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(24),
                IsHitTestVisible = false
            };
            var gridHost = new Grid();
            gridHost.Children.Add(_grid);
            gridHost.Children.Add(_empty);
            panel.Children.Add(gridHost);

            // The hint names the checkboxes only when the table has a switch column, and Delete only where it removes.
            bool switches = _columns.Any(c => c.Kind == "bool" && !c.ReadOnly);
            var hint = Ui.Note(Text.T(_fixedRows ? (switches ? "list.fixed.hint" : "list.fixed.hint.noSwitch") : switches ? "list.table.hint" : "list.table.hint.noSwitch"));
            hint.Margin = new Thickness(0, 6, 0, 0);
            panel.Children.Add(hint);

            var footer = new DockPanel { Margin = new Thickness(0, 8, 0, 0) };
            _count = new TextBlock { FontSize = 12, Foreground = Theme.MutedFg, VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(_count, Dock.Right);
            footer.Children.Add(_count);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal };
            if (!_fixedRows)
            {
                var add = Ui.TextButton(Text.T("list.add"), false);
                add.Margin = new Thickness(0);
                add.ToolTip = Text.T("list.add.tip");
                AutomationProperties.SetName(add, Text.T("list.addTo", label));
                add.Click += (s, e) =>
                {
                    CommitEdit();
                    _search.Text = "";
                    var record = Add(new JObject(), false);
                    Renumber();
                    _grid.SelectedItem = record;
                    _grid.ScrollIntoView(record);
                    var first = _grid.Columns.Skip(1).FirstOrDefault(c => !c.IsReadOnly && !(c is DataGridTemplateColumn));
                    if (first != null)
                    {
                        _grid.CurrentCell = new DataGridCellInfo(record, first);
                        _grid.Focus();
                        _grid.BeginEdit();
                    }
                    Changed();
                };
                buttons.Children.Add(add);
                _remove = Ui.TextButton(Text.T("list.remove"), false);
                _remove.ToolTip = Text.T("list.remove.tip");
                _remove.IsEnabled = false;
                _remove.Click += (s, e) => RemoveSelected();
                buttons.Children.Add(_remove);
                _grid.SelectionChanged += (s, e) => _remove.IsEnabled = _grid.SelectedItems.Count > 0;
            }
            if (!_fixedRows && _defaults != null && _defaults.Count > 0)
            {
                var restore = Ui.TextButton(Text.T("list.restore"), false);
                restore.ToolTip = Text.T("list.restore.tip");
                AutomationProperties.SetName(restore, Text.T("list.restoreOf", label));
                restore.Click += (s, e) =>
                {
                    if (MessageBox.Show(Text.T("list.restore.confirm", label), Text.T("list.restore"), MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                        return;
                    Set(_defaults);
                    _notify();
                };
                buttons.Children.Add(restore);
            }
            footer.Children.Add(buttons);
            panel.Children.Add(footer);

            Element = panel;
        }

        /// <summary>The editing box of a number column takes digits (and a minus, and a decimal mark for a double) only.</summary>
        private static Style NumberEditing(Style basedOn, bool whole)
        {
            var style = new Style(typeof(TextBox), basedOn);
            style.Setters.Add(new EventSetter(UIElement.PreviewTextInputEvent, new TextCompositionEventHandler((s, e) =>
                e.Handled = !e.Text.All(c => char.IsDigit(c) || c == '-' || (!whole && (c == '.' || c == ','))))));
            return style;
        }

        /// <summary>A switch column: a checkbox in every row that toggles with one click, no edit mode in between.</summary>
        private static DataGridTemplateColumn SwitchColumn(SettingColumn column)
        {
            var box = new FrameworkElementFactory(typeof(CheckBox));
            box.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            box.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            box.SetValue(UIElement.IsEnabledProperty, !column.ReadOnly);
            box.SetValue(AutomationProperties.NameProperty, column.Label ?? column.Key);
            box.SetBinding(ToggleButton.IsCheckedProperty, new Binding("[" + column.Key + "]") { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
            return new DataGridTemplateColumn
            {
                Header = column.Label ?? column.Key,
                CellTemplate = new DataTemplate { VisualTree = box },
                IsReadOnly = true
            };
        }

        public JArray Value
        {
            get
            {
                CommitEdit();
                return Peek;
            }
        }

        /// <summary>The rows without ending the edit of a cell: what is being typed there is not in it yet. With fixed
        /// rows none is dropped for being empty.</summary>
        public JArray Peek => new JArray(_records.Where(Counts).Select(r => r.Data.DeepClone()));

        private bool Counts(Record record) => _fixedRows || record.Filled;

        /// <summary>Which rows show (the list's <c>filter</c>); null shows every row. Applied to the rows as they are
        /// loaded and again with <see cref="Refilter"/>; a row added here shows until then.</summary>
        public Func<JObject, bool> RowFilter
        {
            get => _rowFilter;
            set { _rowFilter = value; Refilter(); }
        }

        /// <summary>Applies the filter again: a setting it reads changed.</summary>
        public void Refilter()
        {
            CommitEdit();
            foreach (var record in _records)
                record.Out = _rowFilter != null && !_rowFilter(record.Data);
            _view.Refresh();
            Renumber();
        }

        /// <summary>The rows that show now: those the filter lets through and the search finds.</summary>
        public JArray ShownRows => new JArray(_view.Cast<Record>().Select(r => r.Data.DeepClone()));

        public string CountText => _count.Text;
        public string SearchText
        {
            get => _search.Text;
            set => _search.Text = value ?? "";
        }

        public void Set(JArray value)
        {
            _loading = true;
            CancelEdit();
            _records.Clear();
            if (value != null)
                foreach (var item in value.OfType<JObject>())
                    Add((JObject)item.DeepClone(), true);
            Renumber();
            _loading = false;
        }

        private Record Add(JObject data, bool loaded)
        {
            var record = new Record(data, _columns, Changed) { Out = loaded && _rowFilter != null && !_rowFilter(data) };
            _records.Add(record);
            return record;
        }

        private void RemoveSelected()
        {
            var picked = _grid.SelectedItems.OfType<Record>().ToList();
            if (picked.Count == 0) return;
            CancelEdit();
            int at = _records.IndexOf(picked[0]);
            foreach (var record in picked)
                _records.Remove(record);
            Renumber();
            if (_records.Count > 0) _grid.SelectedItem = _records[Math.Min(at, _records.Count - 1)];
            Changed();
        }

        private bool IsEditing() => _view is IEditableCollectionView editable && (editable.IsEditingItem || editable.IsAddingNew);

        private void CommitEdit()
        {
            if (!IsEditing()) return;
            try { _grid.CommitEdit(DataGridEditingUnit.Row, true); } catch (InvalidOperationException) { }
        }

        private void CancelEdit()
        {
            if (!IsEditing()) return;
            try { _grid.CancelEdit(DataGridEditingUnit.Row); } catch (InvalidOperationException) { }
        }

        private void Renumber()
        {
            // The rows the filter lets through are the list as the user sees it: numbered, counted and searched.
            int number = 0;
            foreach (var record in _records)
            {
                record.Number = record.Out ? 0 : ++number;
                record.Refresh();
            }
            Count();
        }

        private void Count()
        {
            int shown = _view.Cast<object>().Count(), rows = _records.Count(r => !r.Out), filled = _records.Count(r => !r.Out && Counts(r));
            _count.Text = shown != rows
                ? Text.T("list.count.shown", shown, rows)
                : rows == filled
                    ? (filled == 1 ? Text.T("list.count.one") : Text.T("list.count.many", filled))
                    : Text.T("list.count.filled", filled, rows);
            _count.ToolTip = _fixedRows ? null : Text.T("list.count.tip");
            _empty.Text = _records.Count == 0
                ? (_fixedRows ? Text.T("list.fixed.empty") : _defaults != null && _defaults.Count > 0 ? Text.T("list.emptyDefaults") : Text.T("list.empty"))
                : rows == 0 ? Text.T("list.filter.none")
                : shown == 0 ? Text.T("log.noMatch") : "";
            _empty.Visibility = _empty.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Changed()
        {
            if (_loading) return;
            Count();
            _notify();
        }

        private static JToken Typed(SettingColumn column, object value)
        {
            if (column.Kind == "bool")
                return value is bool b ? b : string.Equals(value?.ToString(), "true", StringComparison.OrdinalIgnoreCase);
            string text = (value?.ToString() ?? "").Trim();
            switch (column.Kind)
            {
                case "int":
                case "long":
                    if (text.Length == 0) return 0L;
                    return long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long l) ? (JToken)l : null;
                case "double":
                    if (text.Length == 0) return 0.0;
                    return FieldBinding.TryParseTyped(text, out double d) ? (JToken)d : null;
                default:
                    return text;
            }
        }
    }
}
