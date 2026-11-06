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

        public event Action Changed;

        private sealed class Row
        {
            public Grid Element;
            public readonly Dictionary<string, Control> Cells = new Dictionary<string, Control>(StringComparer.Ordinal);
            public TextBlock Number;
        }

        public ListEditor(string key, string label, List<SettingColumn> columns, JArray value, JArray defaults, string help, IHostHooks hooks = null)
        {
            _label = label;
            _hooks = hooks;
            _columns = columns != null && columns.Count > 0 ? columns : new List<SettingColumn> { new SettingColumn { Key = "value", Kind = "string", Label = "Value" } };
            _defaults = defaults;
            Tag = new FieldTag(key, FieldKind.List);
            ToolTip = string.IsNullOrEmpty(help) ? null : help;
            AutomationProperties.SetName(this, label);

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

            _empty = Ui.Note(_defaults != null && _defaults.Count > 0 ? Text.T("list.emptyDefaults") : Text.T("list.empty"));
            _empty.Margin = new Thickness(0, 4, 0, 0);
            panel.Children.Add(_empty);

            var footer = new DockPanel { Margin = new Thickness(0, 8, 0, 0) };
            _count = new TextBlock { FontSize = 12, Foreground = Theme.MutedFg, VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(_count, Dock.Right);
            footer.Children.Add(_count);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal };
            var add = Ui.TextButton(Text.T("list.add"), false);
            add.Margin = new Thickness(0);
            add.ToolTip = Text.T("list.add.tip");
            AutomationProperties.SetName(add, Text.T("list.addTo", label));
            add.Click += (sender, e) =>
            {
                var row = AddRow(new JObject());
                Renumber();
                Notify();
                var first = row.Cells.Values.OfType<TextBox>().FirstOrDefault();
                first?.Focus();
            };
            buttons.Children.Add(add);
            if (_defaults != null && _defaults.Count > 0)
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
        }

        /// <summary>The rows as they stand, empty rows left out.</summary>
        public JArray Value
        {
            get
            {
                var array = new JArray();
                foreach (var row in _items)
                {
                    var item = new JObject();
                    bool any = false;
                    foreach (var column in _columns)
                    {
                        var control = row.Cells[column.Key];
                        item[column.Key] = CellValue(column, control);
                        // Typed text or a switch turned on makes the row count; a row of empty cells does not.
                        if (control is CheckBox check ? check.IsChecked == true : !string.IsNullOrWhiteSpace((control as TextBox)?.Text))
                            any = true;
                    }
                    if (any)
                        array.Add(item);
                }
                return array;
            }
        }

        /// <summary>Replaces every row.</summary>
        public void Set(JArray value)
        {
            _loading = true;
            _items.Clear();
            _rows.Children.Clear();
            if (value != null)
                foreach (var item in value.OfType<JObject>())
                    AddRow(item);
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
            cells.Add(new TextBlock());
            var grid = Layout(cells.ToArray());
            grid.Margin = new Thickness(0, 0, 0, 2);
            return grid;
        }

        private Grid Layout(FrameworkElement[] cells)
        {
            // Number | one star-sized cell per column (text columns twice as wide, number columns half: a weight
            // or a count is short, the emote or text beside it is what needs the room) | remove button.
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
            for (int i = 0; i < _columns.Count; i++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(StarsOf(_columns[i].Kind), GridUnitType.Star) });
            }
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
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

        private Row AddRow(JObject item)
        {
            var row = new Row();
            row.Number = new TextBlock { FontSize = 11, Foreground = Theme.MutedFg, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 7, 0, 0) };
            var cells = new List<FrameworkElement> { row.Number };
            foreach (var column in _columns)
            {
                var control = Cell(column, item[column.Key]);
                row.Cells[column.Key] = control;
                cells.Add(column.Kind == "emote" && control is TextBox emote ? WithPicture(column, emote) : control);
            }
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
            row.Element = Layout(cells.ToArray());
            row.Element.Margin = new Thickness(0, 0, 0, 6);
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
            string text = value == null || value.Type == JTokenType.Null ? "" : value.Type == JTokenType.String ? (string)value : value.ToString();
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
                    else if (column.Kind == "double") Ui.DecimalsOnly(box);
                    AutomationProperties.SetName(box, column.Label ?? column.Key);
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
                    return FieldBinding.TryParseFinite(text, out double d) ? d : 0.0;
                case "text":
                    return text.Trim();
                default:
                    return text.Trim();
            }
        }

        private void Renumber()
        {
            for (int i = 0; i < _items.Count; i++)
                _items[i].Number.Text = (i + 1).ToString(CultureInfo.InvariantCulture);
            _empty.Visibility = _items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            _searchHost.Visibility = _items.Count >= SearchFrom || _search.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
            Filter();
        }

        /// <summary>Hides the rows the search text does not occur in, and counts.</summary>
        private void Filter()
        {
            string needle = (_search.Text ?? "").Trim();
            int shown = 0;
            foreach (var row in _items)
            {
                bool hit = needle.Length == 0 || row.Cells.Values.Any(c => c is TextBox box && box.Text.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0);
                row.Element.Visibility = hit ? Visibility.Visible : Visibility.Collapsed;
                if (hit) shown++;
            }
            int usable = Value.Count;
            _count.Text = needle.Length > 0 && shown != _items.Count
                ? Text.T("list.count.shown", shown, _items.Count)
                : _items.Count == usable
                    ? (usable == 1 ? Text.T("list.count.one") : Text.T("list.count.many", usable))
                    : Text.T("list.count.filled", usable, _items.Count);
            _count.ToolTip = Text.T("list.count.tip");
        }

        private void Notify()
        {
            if (_loading) return;
            Renumber();
            Changed?.Invoke();
        }
    }
}
