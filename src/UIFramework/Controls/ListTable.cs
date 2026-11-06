using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Newtonsoft.Json.Linq;

namespace UIFramework
{
    /// <summary>
    /// The table layout of a list with many or long columns (a question editor): the rows as a compact, read-only
    /// grid in the style of the Log page, one line each, with a search box above; picking a row opens it in an edit
    /// panel underneath, where every column has a full-size field. Add row, Remove row and Restore defaults sit
    /// under the panel. A row keeps every property it came with, so keys the game writes and no column shows
    /// (a timestamp) survive a save.
    /// </summary>
    public sealed class ListTable
    {
        private readonly List<SettingColumn> _columns;
        private readonly JArray _defaults;
        private readonly string _label;
        private readonly Action _notify;
        private readonly ObservableCollection<Record> _records = new ObservableCollection<Record>();
        private readonly ICollectionView _view;
        private readonly DataGrid _grid;
        private readonly TextBox _search;
        private readonly StackPanel _editor = new StackPanel();
        private readonly TextBlock _count;
        private readonly Button _remove;
        private bool _loading;

        public readonly FrameworkElement Element;

        /// <summary>A list uses the table once a row would not fit one line: a multi-line column, or more than three.</summary>
        public static bool Suits(List<SettingColumn> columns) =>
            columns != null && (columns.Count > 3 || columns.Any(c => c.Kind == "text"));

        /// <summary>One row: the object as stored, and what the grid shows of it.</summary>
        private sealed class Record : INotifyPropertyChanged
        {
            public JObject Data;
            public int Number;
            private readonly List<SettingColumn> _columns;
            public Record(JObject data, List<SettingColumn> columns) { Data = data; _columns = columns; }
            public event PropertyChangedEventHandler PropertyChanged;

            /// <summary>The grid's text for a column: the first line of a text, a tick for a switch on.</summary>
            public string this[string key]
            {
                get
                {
                    if (key == "#") return Number.ToString(CultureInfo.InvariantCulture);
                    var column = _columns.FirstOrDefault(c => c.Key == key);
                    var token = Data[key];
                    if (column?.Kind == "bool")
                        return token != null && token.Type == JTokenType.Boolean && (bool)token ? "✓" : "";
                    string text = token == null || token.Type == JTokenType.Null ? "" : token.Type == JTokenType.String ? (string)token : token.ToString(Newtonsoft.Json.Formatting.None);
                    int line = text.IndexOfAny(new[] { '\r', '\n' });
                    return (line >= 0 ? text.Substring(0, line) + " …" : text).Trim();
                }
            }

            public string Haystack => string.Join(" ", _columns.Select(c => this[c.Key]));

            public bool Filled => _columns.Any(c =>
            {
                var t = Data[c.Key];
                if (t == null || t.Type == JTokenType.Null) return false;
                if (t.Type == JTokenType.Boolean) return (bool)t;
                return t.ToString().Trim().Length > 0;
            });

            public void Refresh() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(Binding.IndexerName));
        }

        public ListTable(string label, List<SettingColumn> columns, JArray defaults, Action notify)
        {
            _label = label;
            _columns = columns;
            _defaults = defaults;
            _notify = notify;
            _view = CollectionViewSource.GetDefaultView(_records);

            var panel = new StackPanel();

            _search = new TextBox { ToolTip = Text.T("list.search.tip") };
            Ui.StyleInput(_search);
            AutomationProperties.SetName(_search, Text.T("list.search", label));
            var searchHost = Ui.WithHint(_search, Text.T("list.search", label));
            searchHost.Margin = new Thickness(0, 0, 0, 8);
            _search.TextChanged += (s, e) => { _view.Refresh(); Count(); };
            _view.Filter = o =>
            {
                string needle = (_search.Text ?? "").Trim();
                return needle.Length == 0 || ((Record)o).Haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
            };
            panel.Children.Add(searchHost);

            _grid = new DataGrid
            {
                AutoGenerateColumns = false,
                IsReadOnly = true,
                CanUserAddRows = false,
                CanUserDeleteRows = false,
                CanUserSortColumns = false,
                SelectionMode = DataGridSelectionMode.Single,
                SelectionUnit = DataGridSelectionUnit.FullRow,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                RowHeaderWidth = 0,
                ItemsSource = _view,
                FontSize = 12,
                Height = 300,
                Background = new SolidColorBrush(Theme.ContentBg),
                RowBackground = new SolidColorBrush(Theme.ContentBg),
                Foreground = Theme.LabelFg,
                BorderBrush = new SolidColorBrush(Theme.Line),
                BorderThickness = new Thickness(1),
                HorizontalGridLinesBrush = new SolidColorBrush(Theme.Line),
                ColumnHeaderStyle = Ui.HeaderStyle(),
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            AutomationProperties.SetName(_grid, label);
            Ui.StyleSelection(_grid);
            var cell = new Style(typeof(TextBlock));
            cell.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(6, 4, 6, 4)));
            cell.Setters.Add(new Setter(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center));
            cell.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis));
            var muted = new Style(typeof(TextBlock), cell);
            muted.Setters.Add(new Setter(TextBlock.ForegroundProperty, Theme.MutedFg));
            _grid.Columns.Add(new DataGridTextColumn { Header = "", Binding = new Binding("[#]"), Width = new DataGridLength(30), ElementStyle = muted });
            // Shares of the width: a question takes most of it, a switch or a number little. Set in pixels once the
            // grid knows its width, since star columns collapse when a page measures them without one.
            var shares = new List<(DataGridColumn Column, double Share)>();
            foreach (var column in _columns)
            {
                double share = column.Kind == "text" ? 3.0 : column.Kind == "bool" ? 0.45 : column.Kind == "int" || column.Kind == "long" || column.Kind == "double" ? 0.7 : 1.0;
                var gridColumn = new DataGridTextColumn
                {
                    Header = column.Label ?? column.Key,
                    Binding = new Binding("[" + column.Key + "]"),
                    MinWidth = 30,
                    ElementStyle = column.ReadOnly ? muted : cell
                };
                _grid.Columns.Add(gridColumn);
                shares.Add((gridColumn, share));
            }
            _grid.SizeChanged += (s, e) =>
            {
                double room = e.NewSize.Width - 30 - 2 - SystemParameters.VerticalScrollBarWidth - 2;
                if (room <= 0) return;
                double total = shares.Sum(x => x.Share);
                foreach (var (column, share) in shares)
                    column.Width = new DataGridLength(Math.Max(30, Math.Floor(room * share / total)));
            };
            _grid.SelectionChanged += (s, e) => ShowEditor(_grid.SelectedItem as Record);
            panel.Children.Add(_grid);

            var editorBox = new Border
            {
                BorderBrush = new SolidColorBrush(Theme.Line),
                BorderThickness = new Thickness(1, 0, 1, 1),
                Padding = new Thickness(12, 10, 12, 12),
                Child = _editor
            };
            panel.Children.Add(editorBox);

            var footer = new DockPanel { Margin = new Thickness(0, 8, 0, 0) };
            _count = new TextBlock { FontSize = 12, Foreground = Theme.MutedFg, VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(_count, Dock.Right);
            footer.Children.Add(_count);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal };
            var add = Ui.TextButton(Text.T("list.add"), false);
            add.Margin = new Thickness(0);
            add.ToolTip = Text.T("list.add.tip");
            AutomationProperties.SetName(add, Text.T("list.addTo", label));
            add.Click += (s, e) =>
            {
                _search.Text = "";
                var record = Add(new JObject());
                Renumber();
                _grid.SelectedItem = record;
                _grid.ScrollIntoView(record);
                _editor.Children.OfType<FrameworkElement>().SelectMany(Inputs).FirstOrDefault()?.Focus();
                Changed();
            };
            buttons.Children.Add(add);
            _remove = Ui.TextButton(Text.T("list.remove"), false);
            _remove.ToolTip = Text.T("list.remove.tip");
            _remove.IsEnabled = false;
            _remove.Click += (s, e) =>
            {
                if (!(_grid.SelectedItem is Record record)) return;
                int at = _records.IndexOf(record);
                _records.Remove(record);
                Renumber();
                if (_records.Count > 0) _grid.SelectedItem = _records[Math.Min(at, _records.Count - 1)];
                Changed();
            };
            buttons.Children.Add(_remove);
            if (_defaults != null && _defaults.Count > 0)
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
            ShowEditor(null);
        }

        public JArray Value => new JArray(_records.Where(r => r.Filled).Select(r => r.Data.DeepClone()));

        public void Set(JArray value)
        {
            _loading = true;
            _records.Clear();
            if (value != null)
                foreach (var item in value.OfType<JObject>())
                    Add((JObject)item.DeepClone());
            Renumber();
            _loading = false;
            ShowEditor(null);
        }

        private Record Add(JObject data)
        {
            var record = new Record(data, _columns);
            _records.Add(record);
            return record;
        }

        private void Renumber()
        {
            for (int i = 0; i < _records.Count; i++)
            {
                _records[i].Number = i + 1;
                _records[i].Refresh();
            }
            Count();
        }

        private void Count()
        {
            int shown = _view.Cast<object>().Count(), filled = _records.Count(r => r.Filled);
            _count.Text = shown != _records.Count
                ? Text.T("list.count.shown", shown, _records.Count)
                : _records.Count == filled
                    ? (filled == 1 ? Text.T("list.count.one") : Text.T("list.count.many", filled))
                    : Text.T("list.count.filled", filled, _records.Count);
            _count.ToolTip = Text.T("list.count.tip");
        }

        private void Changed()
        {
            if (_loading) return;
            Count();
            _notify();
        }

        /// <summary>The edit panel for the picked row: short columns side by side, long ones across the width.</summary>
        private void ShowEditor(Record record)
        {
            _editor.Children.Clear();
            _remove.IsEnabled = record != null;
            if (record == null)
            {
                _editor.Children.Add(Ui.Note(_records.Count == 0
                    ? (_defaults != null && _defaults.Count > 0 ? Text.T("list.emptyDefaults") : Text.T("list.empty"))
                    : Text.T("list.pick")));
                return;
            }
            _editor.Children.Add(new TextBlock { Text = Text.T("list.editing", record.Number), FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = Theme.LabelFg, Margin = new Thickness(0, 0, 0, 6) });

            var shortOnes = new Grid();
            const int across = 3;
            for (int i = 0; i < across; i++)
                shortOnes.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            int placed = 0;
            foreach (var column in _columns.Where(c => c.Kind != "text"))
            {
                if (placed % across == 0) shortOnes.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var field = Field(record, column);
                field.Margin = new Thickness(placed % across == 0 ? 0 : 8, 0, 0, 8);
                Grid.SetRow(field, placed / across);
                Grid.SetColumn(field, placed % across);
                shortOnes.Children.Add(field);
                placed++;
            }
            foreach (var column in _columns.Where(c => c.Kind == "text"))
            {
                var field = Field(record, column);
                field.Margin = new Thickness(0, 0, 0, 8);
                _editor.Children.Add(field);
            }
            if (placed > 0)
                _editor.Children.Add(shortOnes);
        }

        private FrameworkElement Field(Record record, SettingColumn column)
        {
            string label = column.Label ?? column.Key;
            var token = record.Data[column.Key];
            if (column.Kind == "bool")
            {
                var check = new CheckBox { IsChecked = token != null && token.Type == JTokenType.Boolean && (bool)token, Template = Ui.SwitchTemplate(), IsEnabled = !column.ReadOnly, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 4, 0, 0) };
                AutomationProperties.SetName(check, label);
                RoutedEventHandler put = (s, e) => { record.Data[column.Key] = check.IsChecked == true; record.Refresh(); Changed(); };
                check.Checked += put;
                check.Unchecked += put;
                return Ui.Labeled(label, check);
            }
            string text = token == null || token.Type == JTokenType.Null ? "" : token.Type == JTokenType.String ? (string)token : token.ToString(Newtonsoft.Json.Formatting.None);
            var box = new TextBox { Text = text };
            if (column.Kind == "text")
            {
                Ui.StyleColors(box);
                box.Height = 58;
                box.TextWrapping = TextWrapping.Wrap;
                box.AcceptsReturn = false;
                box.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                box.Padding = new Thickness(4);
            }
            else
            {
                Ui.StyleInput(box);
                if (column.Kind == "int" || column.Kind == "long") Ui.DigitsOnly(box);
                else if (column.Kind == "double") Ui.DecimalsOnly(box);
            }
            if (column.ReadOnly)
            {
                box.IsReadOnly = true;
                box.Opacity = 0.8;
                box.ToolTip = Text.T("list.readOnly.tip");
            }
            AutomationProperties.SetName(box, label);
            box.TextChanged += (s, e) =>
            {
                record.Data[column.Key] = Typed(column, box.Text);
                record.Refresh();
                Changed();
            };
            return Ui.Labeled(label, box);
        }

        private static JToken Typed(SettingColumn column, string text)
        {
            switch (column.Kind)
            {
                case "int":
                case "long":
                    return long.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long l) ? l : 0L;
                case "double":
                    return FieldBinding.TryParseFinite(text, out double d) ? d : 0.0;
                default:
                    return text.Trim();
            }
        }

        private static IEnumerable<Control> Inputs(FrameworkElement root)
        {
            if (root is TextBox || root is CheckBox) { yield return (Control)root; yield break; }
            foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<FrameworkElement>())
                foreach (var c in Inputs(child))
                    yield return c;
        }
    }
}
