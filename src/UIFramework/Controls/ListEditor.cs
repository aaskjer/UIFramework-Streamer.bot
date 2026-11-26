using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UIFramework
{
    /// <summary>
    /// The editor behind the <c>list</c> kind: one row per array item, one cell per declared column, a remove
    /// button on every row, Add row and Restore defaults underneath. Its Tag is a <see cref="FieldTag"/> of kind
    /// List, so <see cref="FieldBinding"/> saves, resets and counts it like any other field; the value is a
    /// JSON array of objects whose properties are the column keys. Rows with nothing typed in are dropped.
    /// <para>A list the project fills as well (<c>rowKey</c>, <c>fixedRows</c>, <c>filter</c>) is written back as it
    /// was loaded wherever the user changed nothing: a row keeps the properties no column shows and a cell the form
    /// the file had it in. With <c>fixedRows</c> rows are neither added nor removed here; a filter
    /// (<see cref="RowFilter"/>) says which rows show, and the search and the count work on those. A JSON array in a
    /// read-only cell shows as "a, b" and is written back as the array it was.</para>
    /// </summary>
    public sealed class ListEditor : Border
    {
        private readonly List<SettingColumn> _columns;
        private readonly JArray _defaults;
        private readonly StackPanel _rows = new StackPanel();
        private readonly List<Row> _items = new List<Row>();
        private readonly TextBlock _count;
        private readonly TextBlock _empty;
        private readonly string _label;
        private readonly IHostHooks _hooks;
        private readonly TextBox _search;
        private readonly Grid _searchHost;
        private const int SearchFrom = 8;
        private bool _loading;
        private readonly ListTable _table;
        private readonly bool _fixedRows, _asLoaded;
        private readonly string _emptyText;
        private Func<JObject, bool> _rowFilter;

        public event Action Changed;

        private readonly IDictionary<string, string> _without;

        /// <summary>The two columns of a list stored as pairs of text, so a value handed over as that text is read
        /// (<see cref="LiveValues"/>); null for a list stored as rows.</summary>
        public List<SettingColumn> PairColumns { get; set; }

        private sealed class Row
        {
            public Grid Element;
            public readonly Dictionary<string, Control> Cells = new Dictionary<string, Control>(StringComparer.Ordinal);
            public TextBlock Number;
            /// <summary>The row as it was loaded; null for one added here.</summary>
            public JObject Source;
            /// <summary>The list's filter does not show it.</summary>
            public bool Out;
        }

        /// <param name="fixedRows">Rows are neither added nor removed here: no Add row, no remove, no Restore defaults.</param>
        /// <param name="asLoaded">The project fills the list as well: what the user did not change is written back as
        /// it was loaded. <paramref name="fixedRows"/> brings it along.</param>
        /// <param name="without">Per column, the characters its cells do not take (a list stored as pairs: the ones
        /// that part the pairs in the stored text).</param>
        /// <param name="rowNumbers">False: the table leaves out the column that counts its rows (the rows carry a
        /// number of their own).</param>
        /// <param name="fitWidth">False: the table's columns may run past its width, under a horizontal scrollbar.</param>
        public ListEditor(string key, string label, List<SettingColumn> columns, JArray value, JArray defaults, string help, IHostHooks hooks = null,
            bool fixedRows = false, bool asLoaded = false, IDictionary<string, string> without = null, bool rowNumbers = true, bool fitWidth = true)
        {
            _without = without;
            _label = label;
            _hooks = hooks;
            _fixedRows = fixedRows;
            _asLoaded = asLoaded || fixedRows;
            _columns = columns != null && columns.Count > 0 ? columns : new List<SettingColumn> { new SettingColumn { Key = "value", Kind = "string", Label = "Value" } };
            _defaults = defaults;
            Tag = new FieldTag(key, FieldKind.List);
            ToolTip = string.IsNullOrEmpty(help) ? null : help;
            AutomationProperties.SetName(this, label);

            // A list whose row would not fit one line (a question editor) is a table with an edit panel instead.
            if (ListTable.Suits(_columns))
            {
                _table = new ListTable(label, _columns, _defaults, () => Changed?.Invoke(), _fixedRows, rowNumbers, fitWidth);
                Child = _table.Element;
                _table.Set(value);
                LoadedRows = Value;
                return;
            }

            var panel = new StackPanel();

            // A search box above the rows once there are enough of them to lose one: rows whose cells don't contain
            // the text are hidden, the count says how many show. Nothing else changes; a hidden row is still saved.
            _search = new TextBox { ToolTip = Text.T("list.search.tip") };
            Ui.StyleInput(_search);
            _search.Height = 26;
            AutomationProperties.SetName(_search, Text.T("list.search", label));
            _searchHost = Ui.WithHint(_search, Text.T("list.search", label));
            _searchHost.Margin = new Thickness(0, 0, 0, 6);
            _searchHost.Visibility = Visibility.Collapsed;
            _search.TextChanged += (sender, e) => Filter();
            panel.Children.Add(_searchHost);

            var header = Header();
            panel.Children.Add(header);
            panel.Children.Add(_rows);

            _emptyText = _fixedRows ? Text.T("list.fixed.empty") : _defaults != null && _defaults.Count > 0 ? Text.T("list.emptyDefaults") : Text.T("list.empty");
            _empty = Ui.Note(_emptyText);
            _empty.Margin = new Thickness(0, 4, 0, 0);
            panel.Children.Add(_empty);

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
                add.Click += (sender, e) =>
                {
                    // A search would hide the new, empty row and the box that gets the focus: it goes first, as in the table.
                    if (_search.Text.Length > 0) _search.Text = "";
                    var row = AddRow(new JObject(), false);
                    Renumber();
                    Notify();
                    var first = row.Cells.Values.OfType<TextBox>().FirstOrDefault();
                    first?.Focus();
                };
                buttons.Children.Add(add);
            }
            if (!_fixedRows && _defaults != null && _defaults.Count > 0)
            {
                var restore = Ui.TextButton(Text.T("list.restore"), false);
                restore.ToolTip = Text.T("list.restore.tip");
                AutomationProperties.SetName(restore, Text.T("list.restoreOf", label));
                restore.Click += (sender, e) =>
                {
                    if (MessageBox.Show(Text.T("list.restore.confirm", label), Text.T("list.restore"), MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                        return;
                    Set(_defaults);
                };
                buttons.Children.Add(restore);
            }
            footer.Children.Add(buttons);
            panel.Children.Add(footer);

            Child = panel;
            Set(value);
            LoadedRows = Value;
        }

        /// <summary>What the window loaded: the rows as the list had them when it was built, or as the last save wrote
        /// them (<see cref="Reload"/>). What a save of a list with a <c>rowKey</c> compares the rows with to tell what
        /// the user changed (<see cref="ListMerge"/>). Restore defaults, a reset and an action's <c>set</c> leave it.</summary>
        public JArray LoadedRows { get; set; }

        /// <summary>The list as a save wrote it: its rows are shown where they differ from the ones here (the project
        /// added one meanwhile), and they are what the window loaded from now on.</summary>
        public void Reload(JArray written)
        {
            if (!JToken.DeepEquals(Value, written)) Set(written);
            LoadedRows = Value;
        }

        /// <summary>Which rows show (the list's <c>filter</c>); null shows every row. Applied to the rows as they are
        /// loaded and again with <see cref="Refilter"/>, not while a row is typed in. A row added here shows until then.</summary>
        public Func<JObject, bool> RowFilter
        {
            get => _rowFilter;
            set
            {
                _rowFilter = value;
                if (_table != null) _table.RowFilter = value;
                Refilter();
            }
        }

        /// <summary>Applies the filter again: a setting it reads changed.</summary>
        public void Refilter()
        {
            if (_table != null) { _table.Refilter(); return; }
            foreach (var row in _items)
                row.Out = _rowFilter != null && !_rowFilter(Item(row));
            Renumber();
        }

        /// <summary>The rows that show now: those the filter lets through and the search finds.</summary>
        public JArray ShownRows =>
            _table != null ? _table.ShownRows : new JArray(_items.Where(row => row.Element.Visibility == Visibility.Visible).Select(Item));

        /// <summary>The count under the rows ("3 rows", "2 of 3 shown"), and the text in the search box.</summary>
        public string CountText => _table != null ? _table.CountText : _count.Text;
        public string SearchText
        {
            get => _table != null ? _table.SearchText : _search.Text;
            set { if (_table != null) _table.SearchText = value; else _search.Text = value ?? ""; }
        }

        /// <summary>The rows as <see cref="Value"/> gives them, but without ending an edit in a table's cell: for a look
        /// at the window while the user is typing.</summary>
        public JArray Peek => _table != null ? _table.Peek : Value;

        /// <summary>The rows as they stand, empty rows left out.</summary>
        public JArray Value
        {
            get
            {
                if (_table != null) return _table.Value;
                return new JArray(_items.Where(Filled).Select(Item));
            }
        }

        /// <summary>Typed text or a switch turned on makes the row count; a row of empty cells does not. With fixed
        /// rows every row the list came with counts: none is dropped here.</summary>
        private bool Filled(Row row) =>
            (_fixedRows && row.Source != null) || row.Cells.Values.Any(control => control is CheckBox check ? check.IsChecked == true : !string.IsNullOrWhiteSpace((control as TextBox)?.Text));

        /// <summary>The row as it is saved: a cell per column. In a list the project fills as well, the row as it was
        /// loaded with the cells that read otherwise now laid over it. An array a read-only cell shows goes back as it came.</summary>
        private JObject Item(Row row)
        {
            bool kept = _asLoaded && row.Source != null;
            var item = kept ? (JObject)row.Source.DeepClone() : new JObject();
            foreach (var column in _columns)
            {
                var typed = CellValue(column, row.Cells[column.Key]);
                var source = row.Source?[column.Key];
                if (column.ReadOnly && source is JArray)
                    item[column.Key] = source.DeepClone();
                else if (!kept || ListMerge.Canon(column.Kind, source) != ListMerge.Canon(column.Kind, typed))
                    item[column.Key] = typed;
            }
            return item;
        }

        /// <summary>Replaces every row.</summary>
        public void Set(JArray value)
        {
            if (_table != null)
            {
                _table.Set(value);
                Changed?.Invoke();
                return;
            }
            _loading = true;
            _items.Clear();
            _rows.Children.Clear();
            if (value != null)
                foreach (var item in value.OfType<JObject>())
                    AddRow(item, true);
            _loading = false;
            Renumber();
            Notify();
        }

        public string Serialized => Value.ToString(Formatting.None);

        private Grid Header()
        {
            var cells = new List<FrameworkElement> { new TextBlock() };
            foreach (var column in _columns)
                cells.Add(Ui.SmallLabel(column.Label ?? column.Key, 0));
            if (!_fixedRows) cells.Add(new TextBlock());
            var grid = Layout(cells.ToArray());
            grid.Margin = new Thickness(0, 0, 0, 2);
            return grid;
        }

        private Grid Layout(FrameworkElement[] cells)
        {
            // Number | one star-sized cell per column (text columns twice as wide, number columns half: a weight
            // or a count is short, the emote or text beside it is what needs the room) | remove button (none with
            // fixed rows).
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
            for (int i = 0; i < _columns.Count; i++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(StarsOf(_columns[i].Kind), GridUnitType.Star) });
            }
            if (!_fixedRows)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
            }
            for (int i = 0; i < cells.Length; i++)
            {
                Grid.SetColumn(cells[i], i == 0 ? 0 : i * 2);
                grid.Children.Add(cells[i]);
            }
            return grid;
        }

        private static double StarsOf(string kind)
        {
            switch (kind)
            {
                case "text": return 2.0;
                case "int": case "long": case "double": return 0.5;
                default: return 1.0;
            }
        }

        private Row AddRow(JObject item, bool loaded)
        {
            var row = new Row();
            if (loaded)
            {
                row.Source = (JObject)item.DeepClone();
                row.Out = _rowFilter != null && !_rowFilter(row.Source);
            }
            row.Number = new TextBlock { FontSize = 11, Foreground = Theme.MutedFg, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 7, 0, 0) };
            var cells = new List<FrameworkElement> { row.Number };
            foreach (var column in _columns)
            {
                var control = Cell(column, item[column.Key]);
                row.Cells[column.Key] = control;
                cells.Add(column.Kind == "emote" && control is TextBox emote ? WithPicture(column, emote) : control);
            }
            if (!_fixedRows)
            {
                var remove = Ui.IconButton(Icons.Remove, Icons.Font, Text.T("list.removeRow"));
                remove.VerticalAlignment = VerticalAlignment.Top;
                remove.Click += (sender, e) =>
                {
                    _items.Remove(row);
                    _rows.Children.Remove(row.Element);
                    Renumber();
                    Notify();
                };
                cells.Add(remove);
            }
            row.Element = Layout(cells.ToArray());
            row.Element.Margin = new Thickness(0, 0, 0, 6);
            // A row the search kept only because it was being typed in is filtered once the keyboard has left it.
            row.Element.IsKeyboardFocusWithinChanged += (sender, e) =>
            {
                if (!row.Element.IsKeyboardFocusWithin && !_loading && _search.Text.Length > 0 && _items.Contains(row)) Filter();
            };
            _items.Add(row);
            _rows.Children.Add(row.Element);
            return row;
        }

        /// <summary>An emote cell: its picture beside the box, fetched for the typed code and again when the box is
        /// left. A YouTube cell shows the picture of one of YouTube's own emojis (<c>:name:</c>); a Unicode emoji
        /// shows itself in the box.</summary>
        private FrameworkElement WithPicture(SettingColumn column, TextBox box)
        {
            string platform = (column.Platform ?? "twitch").ToLowerInvariant();
            var image = EmotePictures.Picture(platform, box.Text, _hooks);
            box.LostFocus += (sender, e) => EmotePictures.Show(image, platform, box.Text, _hooks);
            var dock = new DockPanel();
            DockPanel.SetDock(image, Dock.Left);
            dock.Children.Add(image);
            dock.Children.Add(box);
            return dock;
        }

        private Control Cell(SettingColumn column, JToken value)
        {
            // An array the project keeps in a cell the user only reads: its elements, not its JSON.
            string text = column.ReadOnly && value is JArray array ? ListMerge.Shown(array) : FieldBinding.Invariant(value);
            switch (column.Kind)
            {
                case "bool":
                {
                    var check = new CheckBox { IsChecked = value != null && value.Type == JTokenType.Boolean && (bool)value, Template = Ui.SwitchTemplate(), Margin = new Thickness(0, 6, 0, 0), VerticalAlignment = VerticalAlignment.Top };
                    if (column.ReadOnly) check.IsEnabled = false;
                    AutomationProperties.SetName(check, column.Label ?? column.Key);
                    check.Checked += (sender, e) => Notify();
                    check.Unchecked += (sender, e) => Notify();
                    return check;
                }
                case "text":
                {
                    var box = new TextBox { Text = text, Height = 52, TextWrapping = TextWrapping.Wrap, AcceptsReturn = false, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(4) };
                    Ui.StyleColors(box);
                    if (column.ReadOnly) MakeReadOnly(box);
                    AutomationProperties.SetName(box, column.Label ?? column.Key);
                    box.TextChanged += (sender, e) => Notify();
                    return box;
                }
                default:
                {
                    var box = new TextBox { Text = text, VerticalAlignment = VerticalAlignment.Top };
                    Ui.StyleInput(box);
                    if (column.ReadOnly) MakeReadOnly(box);
                    if (column.Kind == "int" || column.Kind == "long") Ui.DigitsOnly(box);
                    else if (column.Kind == "double")
                    {
                        Ui.DecimalsOnly(box);
                        // Text that is no clear number ("1,000": a thousand or one?) is refused when the cell is left,
                        // as the table refuses it: the cell goes back to the last number it held.
                        // The last number is kept on the cell, so a save while the cell still has the focus takes it too.
                        box.Tag = text;
                        box.LostFocus += (sender, e) =>
                        {
                            string typed = box.Text.Trim();
                            if (typed.Length > 0 && !FieldBinding.TryParseTyped(typed, out _)) box.Text = box.Tag as string ?? "";
                            else box.Tag = box.Text;
                        };
                    }
                    AutomationProperties.SetName(box, column.Label ?? column.Key);
                    if (_without != null && _without.TryGetValue(column.Key, out string forbidden) && !column.ReadOnly)
                        Ui.Without(box, forbidden);
                    box.TextChanged += (sender, e) => Notify();
                    return box;
                }
            }
        }

        /// <summary>A cell the game fills and the user only reads: the text stays, selectable, but cannot be typed over.</summary>
        private static void MakeReadOnly(TextBox box)
        {
            box.IsReadOnly = true;
            box.IsTabStop = false;
            box.Opacity = 0.8;
            box.ToolTip = Text.T("list.readOnly.tip");
        }

        private static JToken CellValue(SettingColumn column, Control control)
        {
            if (control is CheckBox check)
                return check.IsChecked == true;
            string text = (control as TextBox)?.Text ?? "";
            switch (column.Kind)
            {
                case "int":
                case "long":
                    return long.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long l) ? l : 0L;
                case "double":
                    if (FieldBinding.TryParseTyped(text, out double d)) return d;
                    // Not a clear number ("1,000") in a cell that still has the focus: the last number it held.
                    return text.Trim().Length > 0 && control.Tag is string kept && FieldBinding.TryParseTyped(kept, out double was) ? was : 0.0;
                case "text":
                    return text.Trim();
                default:
                    return text.Trim();
            }
        }

        private void Renumber()
        {
            // The rows the filter lets through are the list as the user sees it: numbered, counted and searched.
            int count = 0;
            foreach (var row in _items)
                row.Number.Text = row.Out ? "" : (++count).ToString(CultureInfo.InvariantCulture);
            _empty.Text = count == 0 && _items.Count > 0 ? Text.T("list.filter.none") : _emptyText;
            _empty.Visibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;
            _searchHost.Visibility = count >= SearchFrom || _search.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
            Filter();
        }

        /// <summary>Whether a row shows under the search text: one of its texts holds it, or the row is being typed in.
        /// Every keystroke in a cell filters again, and a row that went away under the cursor would take the keyboard
        /// with it ("apple" under the search "app", one "p" deleted).</summary>
        public static bool Shows(string needle, IEnumerable<string> texts, bool typedIn) =>
            string.IsNullOrEmpty(needle) || typedIn || texts.Any(t => t != null && t.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0);

        /// <summary>Hides the rows the search text does not occur in, and counts.</summary>
        private void Filter()
        {
            string needle = (_search.Text ?? "").Trim();
            int shown = 0, rows = 0, usable = 0;
            foreach (var row in _items)
            {
                bool hit = !row.Out && Shows(needle, row.Cells.Values.OfType<TextBox>().Select(box => box.Text), row.Element.IsKeyboardFocusWithin);
                row.Element.Visibility = hit ? Visibility.Visible : Visibility.Collapsed;
                if (hit) shown++;
                if (row.Out) continue;
                rows++;
                if (Filled(row)) usable++;
            }
            _count.Text = needle.Length > 0 && shown != rows
                ? Text.T("list.count.shown", shown, rows)
                : rows == usable
                    ? (usable == 1 ? Text.T("list.count.one") : Text.T("list.count.many", usable))
                    : Text.T("list.count.filled", usable, rows);
            _count.ToolTip = _fixedRows ? null : Text.T("list.count.tip");
        }

        private void Notify()
        {
            if (_loading) return;
            Renumber();
            Changed?.Invoke();
        }
    }
}
