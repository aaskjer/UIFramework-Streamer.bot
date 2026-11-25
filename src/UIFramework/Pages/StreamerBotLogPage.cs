using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace UIFramework
{
    /// <summary>One entry of Streamer.bot's own log: <c>[2026-11-07 11:48:27.383 WRN] TokenManager :: text</c>. The
    /// source is what stands before <c> :: </c>, or what a project puts in brackets in front of its lines
    /// (<c>[Demo Game/Settings] text</c>); a line with neither has none. Lines that follow without a head of their
    /// own (a stack trace) belong to the entry before them.</summary>
    public sealed class SbLogLine
    {
        public DateTime Time { get; set; }
        public string Level { get; set; } = "";
        public string Source { get; set; } = "";
        public string Text { get; set; } = "";
        /// <summary>The entry as the file has it, its following lines included.</summary>
        public string Raw { get; set; } = "";
        public string Blob => (Level + " " + Source + " " + Text).ToLowerInvariant();
    }

    /// <summary>Reads Streamer.bot's log, one file per day (<c>logs\log_yyyyMMdd.log</c>), while Streamer.bot keeps
    /// writing it.</summary>
    public static class StreamerBotLog
    {
        /// <summary>The most entries of one day the page holds; the newest stay.</summary>
        public const int MaxRows = 5000;

        private static readonly Regex Head = new Regex(@"^\[(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}(?:\.\d{1,3})?) ([A-Za-z]{2,5})\] ?(.*)$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex FileName = new Regex(@"^log_(\d{4})(\d{2})(\d{2})\.log$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly string[] TimeFormats = { "yyyy-MM-dd HH:mm:ss.fff", "yyyy-MM-dd HH:mm:ss.ff", "yyyy-MM-dd HH:mm:ss.f", "yyyy-MM-dd HH:mm:ss" };

        /// <summary>The entry a line begins; null for a line that follows another (no head).</summary>
        public static SbLogLine Parse(string raw)
        {
            var head = Head.Match(raw ?? "");
            if (!head.Success)
                return null;
            DateTime.TryParseExact(head.Groups[1].Value, TimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime time);
            string rest = head.Groups[3].Value;
            string source = "", text = rest;
            int parted = rest.IndexOf(" :: ", StringComparison.Ordinal);
            if (parted > 0 && parted <= 60)
            {
                source = rest.Substring(0, parted).Trim();
                text = rest.Substring(parted + 4);
            }
            else if (rest.StartsWith("[", StringComparison.Ordinal))
            {
                int end = rest.IndexOf(']');
                if (end > 1 && end <= 60)
                {
                    source = rest.Substring(1, end - 1).Trim();
                    text = rest.Substring(end + 1).TrimStart();
                }
            }
            return new SbLogLine { Time = time, Level = head.Groups[2].Value.ToUpperInvariant(), Source = source, Text = text, Raw = raw };
        }

        /// <summary>Whether a line is one the window shows: it holds one of the texts the project names, whatever the
        /// case. A window that names none shows every line.</summary>
        public static bool Wanted(string raw, IList<string> match) =>
            match == null || match.Count == 0 || match.Any(m => !string.IsNullOrEmpty(m) && raw.IndexOf(m, StringComparison.OrdinalIgnoreCase) >= 0);

        /// <summary>The days that have a log file, newest first, as <c>2026-11-07</c>.</summary>
        public static List<string> Days(string folder)
        {
            var days = new List<string>();
            try
            {
                if (!Directory.Exists(folder)) return days;
                foreach (var file in Directory.GetFiles(folder, "log_*.log"))
                {
                    var name = FileName.Match(Path.GetFileName(file));
                    if (name.Success) days.Add(name.Groups[1].Value + "-" + name.Groups[2].Value + "-" + name.Groups[3].Value);
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { }
            days.Sort(StringComparer.Ordinal);
            days.Reverse();
            return days;
        }

        public static string FileOf(string folder, string day) => Path.Combine(folder ?? "", "log_" + (day ?? "").Replace("-", "") + ".log");

        /// <summary>Follows one day's file: the first read takes all of it, every later one only what was written
        /// since. Only whole lines are taken, so a line Streamer.bot is in the middle of writing waits for the next read.</summary>
        public sealed class Reader
        {
            private readonly string _file;
            private readonly IList<string> _match;
            private long _at;
            private SbLogLine _last;

            public Reader(string file, IList<string> match) { _file = file; _match = match; }

            /// <summary>Adds the wanted entries that are new to <paramref name="into"/>, in the file's order. False when
            /// the file is shorter than what was read (written anew): the caller starts over with a new reader.</summary>
            public bool ReadNew(List<SbLogLine> into)
            {
                if (!File.Exists(_file))
                    return _at == 0;
                byte[] bytes;
                using (var stream = new FileStream(_file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    if (stream.Length < _at) return false;
                    stream.Seek(_at, SeekOrigin.Begin);
                    bytes = new byte[stream.Length - _at];
                    int got = 0;
                    while (got < bytes.Length)
                    {
                        int read = stream.Read(bytes, got, bytes.Length - got);
                        if (read <= 0) break;
                        got += read;
                    }
                    if (got < bytes.Length) Array.Resize(ref bytes, got);
                }
                int whole = Array.LastIndexOf(bytes, (byte)'\n') + 1;
                if (whole == 0) return true;
                int from = _at == 0 && whole >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
                _at += whole;
                foreach (var line in Encoding.UTF8.GetString(bytes, from, whole - from).Split('\n'))
                {
                    string raw = line.TrimEnd('\r');
                    if (raw.Length == 0) continue;
                    var entry = Parse(raw);
                    if (entry == null)
                    {
                        // A line without a head goes with the entry before it, where that one is shown.
                        if (_last != null)
                        {
                            _last.Text += "\n" + raw;
                            _last.Raw += "\n" + raw;
                        }
                        continue;
                    }
                    _last = Wanted(raw, _match) ? entry : null;
                    if (_last != null) into.Add(_last);
                }
                return true;
            }
        }
    }

    /// <summary>
    /// Streamer.bot's own log as a page, for a project that logs there and writes no file of its own
    /// (<c>.StreamerBotLog("[My Project")</c>): the lines of one day that hold one of the project's texts, newest first,
    /// with a search, a filter by level and by source, the day to pick, and the whole entry under the list. It follows
    /// the file while Streamer.bot writes it.
    /// </summary>
    public static class StreamerBotLogPage
    {
        /// <summary>How a level is coloured: an error red, a warning amber, what is only for looking closer grey.</summary>
        public static PillStyle StyleOf(string level)
        {
            switch ((level ?? "").Trim().ToUpperInvariant())
            {
                case "ERR": case "FTL": return PillStyle.Danger;
                case "WRN": return PillStyle.Warning;
                case "DBG": case "VRB": return PillStyle.Muted;
                default: return PillStyle.None;
            }
        }

        // Errors first, then what is less and less urgent; a level the list does not know comes last, by its name.
        private static readonly string[] LevelOrder = { "FTL", "ERR", "WRN", "INF", "DBG", "VRB" };
        private static int Rank(string level) { int at = Array.IndexOf(LevelOrder, level); return at < 0 ? LevelOrder.Length : at; }

        public static UIElement Build(PageContext ctx)
        {
            var spec = ctx.Spec.SbLog;
            string folder = spec.FullFolder;
            string AllLevels = Text.T("sblog.allLevels"), AllSources = Text.T("sblog.allSources");

            var root = new DockPanel { Margin = new Thickness(16, 12, 16, 12) };
            var entries = new ObservableCollection<SbLogLine>();
            var view = CollectionViewSource.GetDefaultView(entries);

            var search = new TextBox { ToolTip = Text.T("log.search.tip") };
            Ui.StyleInput(search);
            AutomationProperties.SetName(search, Text.T("log.search"));
            var searchHost = Ui.WithHint(search, Text.T("log.search"));

            var levels = Ui.PlainCombo(Text.T("sblog.level"), AllLevels);
            var sources = Ui.PlainCombo(Text.T("sblog.source"), AllSources);
            var days = Ui.PlainCombo(Text.T("log.day"));

            var reload = Ui.IconButton(Icons.Refresh, Icons.Font, Text.T("log.reload"));
            reload.Margin = new Thickness(8, 0, 0, 0);
            var openFolder = Ui.IconButton(Icons.Folder, Icons.Font, Text.T("sblog.openFolder"));
            openFolder.Margin = new Thickness(6, 0, 0, 0);
            openFolder.Click += (sender, e) => Web.OpenFolder(folder, ctx.Log, ctx.Spec.LogPrefix);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal };
            buttons.Children.Add(reload);
            buttons.Children.Add(openFolder);
            var searchRow = new DockPanel();
            DockPanel.SetDock(buttons, Dock.Right);
            searchRow.Children.Add(buttons);
            searchRow.Children.Add(searchHost);
            var filters = Ui.Row(levels, sources, days);
            filters.Margin = new Thickness(0, 8, 0, 0);
            var toolbar = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
            toolbar.Children.Add(searchRow);
            toolbar.Children.Add(filters);

            var grid = new DataGrid
            {
                Name = "StreamerBotLog",
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
            grid.Columns.Add(new DataGridTextColumn { Header = Text.T("log.time"), Binding = new Binding("Time") { Converter = new ClockText() }, Width = DataGridLength.Auto, MinWidth = 60, ElementStyle = cellText });
            grid.Columns.Add(LevelColumn());
            grid.Columns.Add(new DataGridTextColumn { Header = Text.T("sblog.source"), Binding = new Binding("Source"), Width = DataGridLength.Auto, MinWidth = 60, MaxWidth = 220, ElementStyle = cellText });
            // The first line of an entry; the box under the list has all of it.
            grid.Columns.Add(new DataGridTextColumn { Header = Text.T("sblog.text"), Binding = new Binding("Text") { Converter = new FirstLine() }, Width = new DataGridLength(1, DataGridLengthUnitType.Star), MinWidth = 120, ElementStyle = cellText });

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
                Height = 60,
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(0, 8, 0, 0),
                FontSize = 12,
                ToolTip = Text.T("log.line.tip")
            };
            Ui.StyleColors(details);
            AutomationProperties.SetName(details, Text.T("log.line"));
            var capped = Ui.Note(Text.T("sblog.capped", StreamerBotLog.MaxRows.ToString("N0", CultureInfo.CurrentCulture)));
            capped.Visibility = Visibility.Collapsed;

            DockPanel.SetDock(toolbar, Dock.Top);
            root.Children.Add(toolbar);
            DockPanel.SetDock(capped, Dock.Bottom);
            root.Children.Add(capped);
            DockPanel.SetDock(details, Dock.Bottom);
            root.Children.Add(details);
            root.Children.Add(gridHost);

            StreamerBotLog.Reader reader = null;
            string shownDay = null;
            bool filling = false;

            void ApplyFilter()
            {
                string query = search.Text.Trim().ToLowerInvariant();
                string level = levels.SelectedItem as string ?? AllLevels;
                string source = sources.SelectedItem as string ?? AllSources;
                view.Filter = o =>
                {
                    if (!(o is SbLogLine x)) return false;
                    if (level != AllLevels && x.Level != level) return false;
                    if (source != AllSources && !string.Equals(x.Source, source, StringComparison.OrdinalIgnoreCase)) return false;
                    return query.Length == 0 || x.Blob.Contains(query);
                };
                view.Refresh();
                empty.Text = shownDay == null ? Text.T("sblog.noDay") : entries.Count == 0 ? Text.T("sblog.nothing") : Text.T("log.noMatch");
                empty.Visibility = view.IsEmpty ? Visibility.Visible : Visibility.Collapsed;
            }

            // The filters offer what the day holds; a pick stays while the day still has it.
            void FillFilters()
            {
                filling = true;
                try
                {
                    string keepLevel = levels.SelectedItem as string, keepSource = sources.SelectedItem as string;
                    levels.Items.Clear();
                    levels.Items.Add(AllLevels);
                    foreach (var level in entries.Select(e => e.Level).Where(l => l.Length > 0).Distinct().OrderBy(Rank).ThenBy(l => l, StringComparer.Ordinal))
                        levels.Items.Add(level);
                    levels.SelectedItem = keepLevel != null && levels.Items.Contains(keepLevel) ? keepLevel : AllLevels;
                    sources.Items.Clear();
                    sources.Items.Add(AllSources);
                    foreach (var source in entries.Select(e => e.Source).Where(s => s.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
                        sources.Items.Add(source);
                    sources.SelectedItem = keepSource != null && sources.Items.Contains(keepSource) ? keepSource : AllSources;
                }
                finally { filling = false; }
            }

            // Newest first: what is read in the file's order goes in at the top, one after the other.
            void Add(List<SbLogLine> read)
            {
                foreach (var line in read)
                    entries.Insert(0, line);
                while (entries.Count > StreamerBotLog.MaxRows)
                    entries.RemoveAt(entries.Count - 1);
                capped.Visibility = entries.Count >= StreamerBotLog.MaxRows ? Visibility.Visible : Visibility.Collapsed;
            }

            void Load(string day)
            {
                shownDay = day;
                entries.Clear();
                details.Text = "";
                reader = day == null ? null : new StreamerBotLog.Reader(StreamerBotLog.FileOf(folder, day), spec.Match);
                var read = new List<SbLogLine>();
                try { reader?.ReadNew(read); }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    ctx.Log?.Warn(ctx.Spec.LogPrefix + " Streamer.bot's log could not be read: " + ex.Message);
                }
                Add(read);
                FillFilters();
                ApplyFilter();
            }

            void FillDays(string keep)
            {
                filling = true;
                try
                {
                    days.Items.Clear();
                    foreach (var day in StreamerBotLog.Days(folder))
                        days.Items.Add(day);
                    days.SelectedItem = keep != null && days.Items.Contains(keep) ? keep : days.Items.Count > 0 ? days.Items[0] : null;
                }
                finally { filling = false; }
            }

            // What the file gained since the last look, while the same day is shown; a new day's file is offered, and
            // followed where the newest day was the one shown.
            void Follow()
            {
                string newestWas = days.Items.Count > 0 ? days.Items[0] as string : null;
                bool onNewest = shownDay == null || shownDay == newestWas;
                var now = StreamerBotLog.Days(folder);
                if (!now.SequenceEqual(days.Items.OfType<string>()))
                {
                    FillDays(onNewest ? null : shownDay);
                    if ((days.SelectedItem as string) != shownDay)
                    {
                        Load(days.SelectedItem as string);
                        return;
                    }
                }
                if (reader == null) return;
                var read = new List<SbLogLine>();
                bool same;
                try { same = reader.ReadNew(read); }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { return; }
                if (!same)
                {
                    Load(shownDay);
                    return;
                }
                if (read.Count == 0) return;
                var picked = grid.SelectedItem;
                Add(read);
                FillFilters();
                ApplyFilter();
                if (picked != null && entries.Contains(picked)) grid.SelectedItem = picked;
            }

            grid.SelectionChanged += (sender, e) => details.Text = grid.SelectedItem is SbLogLine picked ? picked.Raw ?? "" : "";
            search.TextChanged += (sender, e) => ApplyFilter();
            levels.SelectionChanged += (sender, e) => { if (!filling) ApplyFilter(); };
            sources.SelectionChanged += (sender, e) => { if (!filling) ApplyFilter(); };
            days.SelectionChanged += (sender, e) => { if (!filling && (days.SelectedItem as string) != shownDay) Load(days.SelectedItem as string); };
            reload.Click += (sender, e) => { FillDays(shownDay); Load(days.SelectedItem as string); };

            FillDays(null);
            Load(days.SelectedItem as string);
            Watch(root, folder, ctx, Follow);
            return root;
        }

        /// <summary>Calls <paramref name="changed"/> shortly after a log file in the folder was written, created or
        /// renamed; a folder that is not there yet is looked for every few seconds. It ends with the page or the window.</summary>
        private static void Watch(FrameworkElement page, string folder, PageContext ctx, Action changed)
        {
            FileSystemWatcher watcher = null;
            var settle = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
            settle.Tick += (sender, e) =>
            {
                settle.Stop();
                try { changed(); }
                catch (Exception ex) { ctx.Log?.Warn(ctx.Spec.LogPrefix + " Streamer.bot's log could not be read again: " + ex.Message); }
            };
            var dispatcher = page.Dispatcher;
            void Changed(object sender, FileSystemEventArgs e) => dispatcher.BeginInvoke(new Action(() => { if (!settle.IsEnabled) settle.Start(); }));
            bool Start()
            {
                if (!Directory.Exists(folder)) return false;
                try
                {
                    watcher = new FileSystemWatcher(folder, "log_*.log") { NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size };
                    watcher.Changed += Changed;
                    watcher.Created += Changed;
                    watcher.Renamed += (sender, e) => Changed(sender, e);
                    watcher.EnableRaisingEvents = true;
                }
                catch (Exception ex)
                {
                    ctx.Log?.Warn(ctx.Spec.LogPrefix + " Streamer.bot's log folder is not followed: " + ex.Message);
                    watcher = null;
                }
                return true;
            }
            var look = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            look.Tick += (sender, e) => { if (Start()) { look.Stop(); settle.Start(); } };
            if (!Start()) look.Start();
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

        private static DataGridTemplateColumn LevelColumn()
        {
            var converter = new LevelPill();
            var pill = new FrameworkElementFactory(typeof(Border));
            pill.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            pill.SetValue(Border.PaddingProperty, new Thickness(7, 1, 7, 2));
            pill.SetValue(FrameworkElement.MarginProperty, new Thickness(6, 3, 6, 3));
            pill.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left);
            pill.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            pill.SetBinding(Border.BackgroundProperty, new Binding("Level") { Converter = converter, ConverterParameter = "background" });
            var text = new FrameworkElementFactory(typeof(TextBlock));
            text.SetValue(TextBlock.FontSizeProperty, 11.0);
            text.SetBinding(TextBlock.TextProperty, new Binding("Level"));
            text.SetBinding(TextBlock.ForegroundProperty, new Binding("Level") { Converter = converter, ConverterParameter = "foreground" });
            pill.AppendChild(text);
            return new DataGridTemplateColumn
            {
                Header = Text.T("sblog.level"),
                CellTemplate = new DataTemplate { VisualTree = pill },
                Width = DataGridLength.Auto,
                MinWidth = 56,
                SortMemberPath = "Level"
            };
        }

        private sealed class ClockText : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
                value is DateTime time && time != DateTime.MinValue ? time.ToString("HH:mm:ss", CultureInfo.InvariantCulture) : "";
            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
        }

        private sealed class FirstLine : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                string text = value as string ?? "";
                int end = text.IndexOf('\n');
                return end < 0 ? text : text.Substring(0, end) + " …";
            }
            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
        }

        private sealed class LevelPill : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                var colours = Ui.PillColours(StyleOf(value as string));
                return new SolidColorBrush(parameter as string == "background" ? colours.Background : colours.Foreground);
            }
            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
        }
    }
}
