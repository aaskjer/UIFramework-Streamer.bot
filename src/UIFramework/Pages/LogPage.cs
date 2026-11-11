using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;

namespace UIFramework
{
    /// <summary>
    /// The host's log in a filterable grid: search, result, platform and day filters, a detail line for the raw
    /// entry, a reload button, the folder button and the "write a log" switch. Entries come from
    /// <see cref="IHostHooks.ReadLog"/>; null means the host has no log and the page is left out.
    /// </summary>
    public static class LogPage
    {
        public static PillStyle DefaultStyle(string outcome)
        {
            string text = (outcome ?? "").Trim().ToUpperInvariant();
            if (text.Length == 0) return PillStyle.None;
            if (text.Contains("WIN") || text.Contains("JACKPOT") || text.Contains("CORRECT") || text.Contains("SOLVED") || text.Contains("BLACKJACK"))
                return PillStyle.Success;
            if (text.Contains("LOSS") || text.Contains("LOSE") || text.Contains("BUST") || text.Contains("WRONG") || text.Contains("FAIL"))
                return PillStyle.Muted;
            if (text.Contains("PUSH") || text.Contains("TIE") || text.Contains("DRAW"))
                return PillStyle.Warning;
            return PillStyle.Accent;
        }

        /// <summary>A text the game declared (a column header, the empty text) in the window's language: the game's own
        /// translation (Tx), else as declared.</summary>
        private static string Declared(PageContext ctx, string declared)
        {
            var lang = ctx.Spec.Conventions?.Lang;
            return declared != null && lang != null && lang.Translates ? lang.Text(declared) : declared;
        }

        public static UIElement Build(PageContext ctx)
        {
            var spec = ctx.Spec.Log;
            IEnumerable<LogEntry> read;
            try { read = ctx.Hooks.ReadLog(); }
            catch (Exception ex)
            {
                ctx.Log.Warn(ctx.Spec.LogPrefix + " Log could not be read: " + ex.Message);
                read = Enumerable.Empty<LogEntry>();
            }
            if (read == null)
                return null;

            string AllDates = Text.T("log.allDays");
            string AllResults = Text.T("log.allResults");
            string AllPlatforms = Text.T("log.allPlatforms");
            Func<string, string> outcome = spec.Outcome ?? (r => (r ?? "").Trim().ToUpperInvariant());
            Func<string, PillStyle> style = spec.ResultStyle ?? DefaultStyle;

            var root = new DockPanel { Margin = new Thickness(16, 12, 16, 12) };
            var entries = new ObservableCollection<LogEntry>(read.OrderByDescending(x => x.Timestamp));
            var view = CollectionViewSource.GetDefaultView(entries);

            var search = new TextBox { ToolTip = Text.T("log.search.tip") };
            Ui.StyleInput(search);
            AutomationProperties.SetName(search, Text.T("log.search"));
            var searchHost = Ui.WithHint(search, Text.T("log.search"));

            var results = Ui.PlainCombo(Text.T("log.result"), AllResults);
            var platforms = Ui.PlainCombo(Text.T("log.platform"), new[] { AllPlatforms }.Concat(spec.Platforms).ToArray());
            var dates = Ui.PlainCombo(Text.T("log.day"), AllDates);

            var reload = Ui.IconButton(Icons.Refresh, Icons.Font, Text.T("log.reload"));
            reload.Margin = new Thickness(8, 0, 0, 0);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal };
            buttons.Children.Add(reload);
            if (!string.IsNullOrEmpty(spec.Folder))
            {
                var openFolder = Ui.IconButton(Icons.Open, Icons.Font, Text.T("log.openFolder"));
                openFolder.Margin = new Thickness(6, 0, 0, 0);
                openFolder.Click += (sender, e) => Web.OpenFolder(spec.Folder, ctx.Log, ctx.Spec.LogPrefix);
                buttons.Children.Add(openFolder);
            }
            var searchRow = new DockPanel();
            DockPanel.SetDock(buttons, Dock.Right);
            searchRow.Children.Add(buttons);
            searchRow.Children.Add(searchHost);

            var filters = Ui.Row(results, platforms, dates);
            filters.Margin = new Thickness(0, 8, 0, 0);
            var toolbar = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
            toolbar.Children.Add(searchRow);
            toolbar.Children.Add(filters);

            var grid = new DataGrid
            {
                AutoGenerateColumns = false,
                IsReadOnly = true,
                CanUserAddRows = false,
                CanUserDeleteRows = false,
                SelectionMode = DataGridSelectionMode.Single,
                SelectionUnit = DataGridSelectionUnit.FullRow,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                RowHeaderWidth = 0,
                ItemsSource = view,
                FontSize = 12,
                Background = new SolidColorBrush(Theme.ContentBg),
                RowBackground = new SolidColorBrush(Theme.ContentBg),
                Foreground = Theme.LabelFg,
                BorderBrush = new SolidColorBrush(Theme.Line),
                BorderThickness = new Thickness(1),
                HorizontalGridLinesBrush = new SolidColorBrush(Theme.Line),
                ColumnHeaderStyle = Ui.HeaderStyle()
            };

            Ui.StyleSelection(grid);
            var cellText = new Style(typeof(TextBlock));
            cellText.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(6, 4, 6, 4)));
            cellText.Setters.Add(new Setter(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center));
            grid.Columns.Add(TextColumn(Text.T("log.time"), new Binding("Timestamp") { StringFormat = "yyyy-MM-dd HH:mm:ss" }, cellText));
            grid.Columns.Add(ResultColumn(outcome, style));
            var columns = spec.Columns.Count > 0 ? spec.Columns : new List<LogColumn> { new LogColumn(Text.T("log.user"), "User"), new LogColumn(Text.T("log.platform"), "Platform") };
            foreach (var column in columns)
                grid.Columns.Add(TextColumn(Declared(ctx, column.Header), new Binding { Converter = new FieldReader(column.Field) }, cellText));

            var empty = new TextBlock
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
            gridHost.Children.Add(grid);
            gridHost.Children.Add(empty);

            var details = new TextBox
            {
                Height = 44,
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(0, 8, 0, 0),
                FontSize = 12,
                ToolTip = Text.T("log.line.tip")
            };
            Ui.StyleColors(details);
            AutomationProperties.SetName(details, Text.T("log.line"));

            DockPanel.SetDock(toolbar, Dock.Top);
            root.Children.Add(toolbar);
            if (!string.IsNullOrEmpty(spec.WriteLogKey))
            {
                var lang = ctx.Spec.Conventions?.Lang;
                string T(string declared) => lang != null && lang.Translates ? lang.Text(declared) : declared;
                var writeLog = ctx.Fields.Toggle(T(spec.WriteLogLabel), spec.WriteLogKey, T(spec.WriteLogHelp));
                // Named like a schema page's fields, so the search and the tour find the switch.
                ctx.Parts.Elements[spec.WriteLogKey] = writeLog;
                writeLog.Margin = new Thickness(0, 10, 0, 0);
                // The customise mode has no Save: a switch flipped there would be dropped with the next rebuild.
                if (ctx.Customizing) writeLog.IsEnabled = false;
                DockPanel.SetDock(writeLog, Dock.Bottom);
                root.Children.Add(writeLog);
            }
            DockPanel.SetDock(details, Dock.Bottom);
            root.Children.Add(details);
            root.Children.Add(gridHost);

            void ApplyFilter()
            {
                string query = search.Text.Trim().ToLowerInvariant();
                string result = results.SelectedItem as string ?? AllResults;
                string where = platforms.SelectedItem as string ?? AllPlatforms;
                string day = dates.SelectedItem as string ?? AllDates;

                view.Filter = o =>
                {
                    if (!(o is LogEntry x))
                        return false;
                    if (result != AllResults && !string.Equals(outcome(x.Result), result.ToUpperInvariant(), StringComparison.OrdinalIgnoreCase))
                        return false;
                    if (where != AllPlatforms && !string.Equals(x.Platform, where, StringComparison.OrdinalIgnoreCase))
                        return false;
                    if (day != AllDates && x.Timestamp.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) != day)
                        return false;
                    if (query.Length > 0 && !x.SearchBlob.Contains(query))
                        return false;
                    return true;
                };
                view.Refresh();
                empty.Text = entries.Count == 0 ? (spec.EmptyText != null ? Declared(ctx, spec.EmptyText) : Text.T("log.nothingYet")) : Text.T("log.noMatch");
                empty.Visibility = view.IsEmpty ? Visibility.Visible : Visibility.Collapsed;
            }

            void FillDates(string keep)
            {
                dates.Items.Clear();
                dates.Items.Add(AllDates);
                foreach (var day in entries.Where(x => x.Timestamp != DateTime.MinValue).Select(x => x.Timestamp.Date).Distinct().OrderByDescending(d => d))
                    dates.Items.Add(day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                dates.SelectedItem = keep != null && dates.Items.Contains(keep) ? keep : AllDates;
            }

            // A game that declares no results offers the ones its log holds, so they are filled again with every
            // reload: a log that was empty when the window opened gets its results with the first round.
            void FillResults(string keep)
            {
                results.Items.Clear();
                results.Items.Add(AllResults);
                foreach (string option in spec.Results.Count > 0
                    ? spec.Results
                    : entries.Select(e => outcome(e.Result)).Where(r => r.Length > 0).Distinct().OrderBy(r => r).Select(Title))
                    results.Items.Add(option);
                results.SelectedItem = keep != null && results.Items.Contains(keep) ? keep : AllResults;
            }

            grid.SelectionChanged += (sender, e) => details.Text = grid.SelectedItem is LogEntry picked ? picked.Raw ?? "" : "";
            search.TextChanged += (sender, e) => ApplyFilter();
            results.SelectionChanged += (sender, e) => ApplyFilter();
            platforms.SelectionChanged += (sender, e) => ApplyFilter();
            dates.SelectionChanged += (sender, e) => ApplyFilter();
            void Reload()
            {
                string keepDay = dates.SelectedItem as string;
                string keepResult = results.SelectedItem as string;
                // The log is read again by itself while a round runs in chat: the picked entry, found again by its
                // raw line, and its detail line stay. The grid keeps the place it is scrolled to by itself.
                string keepRaw = (grid.SelectedItem as LogEntry)?.Raw;
                IEnumerable<LogEntry> reloaded;
                try { reloaded = ctx.Hooks.ReadLog() ?? Enumerable.Empty<LogEntry>(); }
                catch (Exception ex) { ctx.Log.Warn(ctx.Spec.LogPrefix + " Log could not be read: " + ex.Message); reloaded = Enumerable.Empty<LogEntry>(); }
                // Not inside view.DeferRefresh(): the grid reads the view with every line that comes in, and a view
                // whose refresh is deferred throws on that.
                entries.Clear();
                foreach (var item in reloaded.OrderByDescending(x => x.Timestamp))
                    entries.Add(item);
                FillDates(keepDay);
                FillResults(keepResult);
                ApplyFilter();
                var again = string.IsNullOrEmpty(keepRaw) ? null : view.OfType<LogEntry>().FirstOrDefault(x => x.Raw == keepRaw);
                grid.SelectedItem = again;
                if (again == null)
                    details.Text = "";
            }
            reload.Click += (sender, e) => Reload();
            Follow(root, spec, ctx, Reload);

            FillDates(null);
            FillResults(null);
            ApplyFilter();
            return root;
        }

        /// <summary>Reads the log again whenever a file in its folder changes, so a round played in chat shows
        /// up without the reload button. Writes come in bursts, so a short timer gathers them; a folder that is
        /// not there yet (no log written so far) is looked for every few seconds. Everything stops with the page or the window.</summary>
        private static void Follow(FrameworkElement page, LogSpec spec, PageContext ctx, Action reload)
        {
            string folder = spec.FullFolder;
            if (folder == null)
                return;
            System.IO.FileSystemWatcher watcher = null;
            var settle = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
            settle.Tick += (sender, e) =>
            {
                settle.Stop();
                try { reload(); }
                catch (Exception ex) { ctx.Log.Warn(ctx.Spec.LogPrefix + " Log could not be read again: " + ex.Message); }
            };
            var dispatcher = page.Dispatcher;
            void Changed(object sender, System.IO.FileSystemEventArgs e) => dispatcher.BeginInvoke(new Action(() => { settle.Stop(); settle.Start(); }));
            bool Start()
            {
                if (!System.IO.Directory.Exists(folder))
                    return false;
                try
                {
                    watcher = new System.IO.FileSystemWatcher(folder, string.IsNullOrWhiteSpace(spec.Files) ? "*" : spec.Files)
                    {
                        NotifyFilter = System.IO.NotifyFilters.LastWrite | System.IO.NotifyFilters.FileName | System.IO.NotifyFilters.Size
                    };
                    watcher.Changed += Changed;
                    watcher.Created += Changed;
                    watcher.Renamed += (sender, e) => Changed(sender, e);
                    watcher.EnableRaisingEvents = true;
                    return true;
                }
                catch (Exception ex)
                {
                    ctx.Log.Warn(ctx.Spec.LogPrefix + " The log folder is not followed: " + ex.Message);
                    watcher = null;
                    return true;
                }
            }
            var look = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            look.Tick += (sender, e) => { if (Start()) { look.Stop(); settle.Start(); } };
            if (!Start())
                look.Start();
            // The page going (a rebuild) and the window closing both end it. Unloaded alone missed the close: WPF queues
            // it, and the window's thread shuts its dispatcher down before the queue is run, so every window closed left
            // its watcher on the folder, holding the whole window.
            EventHandler closing = null;
            void Stop()
            {
                look.Stop();
                settle.Stop();
                if (watcher != null) { watcher.EnableRaisingEvents = false; watcher.Dispose(); watcher = null; }
                dispatcher.ShutdownStarted -= closing;
            }
            closing = (sender, e) => Stop();
            dispatcher.ShutdownStarted += closing;
            page.Unloaded += (sender, e) => Stop();
        }

        // "WIN" reads "Win"; every part of "GAME-OVER" or "CP_START" gets its capital: "Game-Over", "Cp_Start".
        private static string Title(string upper)
        {
            var chars = upper.ToLowerInvariant().ToCharArray();
            for (int i = 0; i < chars.Length; i++)
                if (i == 0 || chars[i - 1] == '-' || chars[i - 1] == '_' || chars[i - 1] == ' ') chars[i] = char.ToUpperInvariant(chars[i]);
            return new string(chars);
        }

        private static DataGridTextColumn TextColumn(string header, Binding binding, Style element)
        {
            return new DataGridTextColumn
            {
                Header = header,
                Binding = binding,
                Width = DataGridLength.Auto,
                MinWidth = 50,
                ElementStyle = element
            };
        }

        private static DataGridTemplateColumn ResultColumn(Func<string, string> outcome, Func<string, PillStyle> style)
        {
            var converter = new ResultPill(outcome, style);
            var pill = new FrameworkElementFactory(typeof(Border));
            pill.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            pill.SetValue(Border.PaddingProperty, new Thickness(7, 1, 7, 2));
            pill.SetValue(FrameworkElement.MarginProperty, new Thickness(6, 3, 6, 3));
            pill.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left);
            pill.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            pill.SetBinding(Border.BackgroundProperty, new Binding("Result") { Converter = converter, ConverterParameter = "background" });
            var text = new FrameworkElementFactory(typeof(TextBlock));
            text.SetValue(TextBlock.FontSizeProperty, 11.0);
            text.SetBinding(TextBlock.TextProperty, new Binding("Result") { Converter = converter, ConverterParameter = "text" });
            text.SetBinding(TextBlock.ForegroundProperty, new Binding("Result") { Converter = converter, ConverterParameter = "foreground" });
            pill.AppendChild(text);
            return new DataGridTemplateColumn
            {
                Header = Text.T("log.result"),
                CellTemplate = new DataTemplate { VisualTree = pill },
                Width = DataGridLength.Auto,
                MinWidth = 90,
                SortMemberPath = "Result"
            };
        }

        private sealed class FieldReader : IValueConverter
        {
            private readonly string _field;
            public FieldReader(string field) { _field = field; }
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => (value as LogEntry)?.Field(_field) ?? "";
            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
        }

        private sealed class ResultPill : IValueConverter
        {
            private readonly Func<string, string> _outcome;
            private readonly Func<string, PillStyle> _style;

            public ResultPill(Func<string, string> outcome, Func<string, PillStyle> style) { _outcome = outcome; _style = style; }

            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                string raw = (value as string ?? "").Trim();
                string part = parameter as string;
                if (part == "text")
                {
                    if (raw.Length == 0) return "";
                    string shown = raw.ToUpperInvariant() == raw ? Title(raw) : raw;
                    return shown;
                }
                var colours = Ui.PillColours(_style(_outcome(raw)));
                return new SolidColorBrush(part == "background" ? colours.Background : colours.Foreground);
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
        }
    }
}
