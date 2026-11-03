using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UIFramework
{
    /// <summary>
    /// The window: rail, header, pages, footer, saving, theme switch, reopen loop. One instance per call to
    /// <see cref="Run"/>; the static gate keeps one window per settings file across hosts and instances.
    /// </summary>
    public sealed class Shell
    {
        private static readonly object Gate = new object();
        private static readonly Dictionary<string, Window> OpenWindows = new Dictionary<string, Window>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> Claimed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private readonly WindowSpec _spec;
        private readonly IHostLog _log;
        private readonly IHostHooks _hooks;
        private readonly SettingsStore _store;
        private readonly string _gateKey;

        private Window _window;
        private volatile bool _shuttingDown;

        private Shell(WindowSpec spec, IHostLog log, IHostHooks hooks)
        {
            _spec = spec ?? throw new ArgumentNullException(nameof(spec));
            _log = log ?? new NullLog();
            _hooks = hooks ?? new NoHooks();
            if (string.IsNullOrWhiteSpace(spec.SettingsPath))
                throw new ArgumentException("WindowSpec.SettingsPath is required.");
            _store = new SettingsStore(spec.SettingsPath, spec.Defaults).WithAliases(spec.Aliases, spec.AliasFactors, spec.AliasesShared);
            _gateKey = Path.GetFullPath(spec.SettingsPath);
        }

        /// <summary>
        /// Opens the window and blocks until it is closed for good. A second call for the same settings file
        /// while a window is up fronts that window and returns false. <paramref name="pump"/>, when given, runs
        /// on the calling thread every 100 ms while the window is open and once after it closed: the stub uses it
        /// to apply what <see cref="IHostHooks.OnSaved"/> queued, because CPH calls belong to Streamer.bot's thread.
        /// </summary>
        public static bool Run(WindowSpec spec, IHostLog log, IHostHooks hooks, Action pump = null)
        {
            var shell = new Shell(spec, log, hooks);
            if (!shell.Claim())
                return false;
            try
            {
                shell.RunLoop(pump);
            }
            finally
            {
                shell.Release();
            }
            return true;
        }

        /// <summary>Closes the window that belongs to a settings file, without the unsaved-changes question. For
        /// the stub's Dispose: Streamer.bot recompiles or shuts down, and nobody is there to answer.</summary>
        public static void Close(string settingsPath)
        {
            Window window;
            lock (Gate)
                OpenWindows.TryGetValue(Path.GetFullPath(settingsPath), out window);
            if (window == null)
                return;
            try
            {
                window.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (window.Tag is Shell shell)
                        shell._shuttingDown = true;
                    try { window.Close(); } catch (Exception) { }
                }));
            }
            catch (Exception) { }
        }

        private bool Claim()
        {
            Window open;
            lock (Gate)
            {
                if (Claimed.Add(_gateKey))
                    return true;
                OpenWindows.TryGetValue(_gateKey, out open);
            }
            _log.Info(_spec.LogPrefix + " The settings window is already open - brought it to the front instead of opening another.");
            open?.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (open.WindowState == WindowState.Minimized)
                    open.WindowState = WindowState.Normal;
                open.Topmost = true;
                open.Topmost = false;
                open.Activate();
            }));
            return false;
        }

        private void Release()
        {
            lock (Gate)
            {
                Claimed.Remove(_gateKey);
                OpenWindows.Remove(_gateKey);
            }
        }

        /// <summary>A WPF window needs a thread of its own in single-threaded apartment state, which the thread
        /// pool does not hand out. Switching the theme or resetting to defaults closes the window and asks for a
        /// new one: the whole tree is rebuilt in the other palette instead of being recoloured in place.</summary>
        private void RunLoop(Action pump)
        {
            var update = Task.Run(() => Web.LatestRelease(_spec.GitHubRepo, _spec.Version, _log, _spec.LogPrefix));
            var thread = new Thread(() =>
            {
                try
                {
                    try
                    {
                        ShowWindow(update);
                    }
                    finally
                    {
                        Dispatcher.CurrentDispatcher.InvokeShutdown();
                    }
                }
                catch (Exception ex)
                {
                    _log.Error(_spec.LogPrefix + " GUI thread error: " + ex.Message);
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();

            while (thread.IsAlive)
            {
                pump?.Invoke();
                Thread.Sleep(100);
            }
            thread.Join();
            pump?.Invoke();
        }

        private void ShowWindow(Task<string> update)
        {
            string fileName = Path.GetFileName(_spec.SettingsPath);
            SettingsLoad loaded;
            try { loaded = _store.Load(); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                _log.Warn(_spec.LogPrefix + " " + fileName + " could not be opened: " + ex.Message);
                MessageBox.Show(
                    fileName + " could not be read or written:\n\n" + ex.Message + "\n\n" +
                    "Nothing was changed. Close whatever has the file open, or check that Streamer.bot may write to its folder, then open the settings again.",
                    "Settings Not Opened", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (loaded.Damaged != null)
                MessageBox.Show(
                    "The saved settings could not be read:\n\n" + loaded.Damaged + "\n\n" +
                    (loaded.FromBackup
                        ? "The window shows the settings from before the last save (" + fileName + ".bak), and Save puts them back."
                        : "The window shows the defaults, and Save writes a fresh file.") +
                    " The damaged file was kept next to it as " + fileName + ".broken.",
                    "Settings Damaged", MessageBoxButton.OK, MessageBoxImage.Warning);

            var settings = loaded.Settings;
            // Every open resolves afresh, so a layout change made in the customise mode shows up after the rebuild.
            if (_spec.Schema != null)
                Entry.Prepare(_spec, _spec.Schema);
            var layout = _spec.Layout ?? LayoutFile.Load(_spec.SettingsPath);
            _spec.Layout = layout;
            if (layout.AdoptFromHost(settings, "darkMode", "DarkMode"))
            {
                try { layout.Save(); if (loaded.Damaged == null) _store.Save(Sidecars.Strip(settings, _spec.ResolvedSettings)); }
                catch (Exception ex) { _log.Warn(_spec.LogPrefix + " Theme not moved to the layout file: " + ex.Message); }
            }
            layout.ApplyTheme();
            // Values kept in files of their own (a word bank, a question list) come in after the settings file.
            Sidecars.Load(settings, _spec.ResolvedSettings, _spec.SettingsPath, _log, _spec.LogPrefix);

            var window = new Window
            {
                Title = _spec.Title + " – Settings",
                Width = _spec.Width,
                Height = _spec.Height,
                MinWidth = _spec.MinWidth,
                MinHeight = _spec.MinHeight,
                FontSize = 13,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Background = new SolidColorBrush(Theme.ContentBg),
                Tag = this
            };
            Ui.WrapToolTips(window);
            lock (Gate)
                OpenWindows[_gateKey] = window;

            var iconHolder = new Image();
            Web.LoadImageAsync(iconHolder, _spec.LogoUrl, source => window.Icon = source);

            // What the content hands up to the window: the window stays, its content is rebuilt in place after a
            // layout change in the customise mode, a theme switch or a reset, on the same page and scroll position.
            bool closeConfirmed = false;
            Func<int> changes = () => 0;
            Func<(string Id, double Offset)> currentPage = () => (null, 0);
            double? keepOffset = null;
            DispatcherTimer rainbowTimer = null;
            KeyEventHandler keyHandler = null;

            void Rebuild(string pageId)
            {
                var shown = currentPage();
                if (pageId == null) pageId = shown.Id;
                keepOffset = pageId == shown.Id ? shown.Offset : (double?)null;
                if (_spec.Schema != null)
                    Entry.Prepare(_spec, _spec.Schema);
                layout = _spec.Layout ?? LayoutFile.Load(_spec.SettingsPath);
                _spec.Layout = layout;
                layout.ApplyTheme();
                _spec.ReopenPageId = pageId;
                Build();
            }

            void Build()
            {
            rainbowTimer?.Stop();
            rainbowTimer = null;
            Theme.IsRainbow = false;
            window.Title = _spec.Title + " – Settings";
            window.Background = new SolidColorBrush(Theme.ContentBg);

            var parts = new WindowParts();
            var defaults = _spec.Defaults;
            var ctx = new PageContext
            {
                Spec = _spec,
                Settings = settings,
                Defaults = defaults,
                Parts = parts,
                Log = _log,
                Hooks = _hooks
            };
            ctx.Fields = new Fields(parts, settings, defaults, url => Web.OpenUrl(url, _log, _spec.LogPrefix));
            ctx.RefreshPage = parts.Refresh;
            ctx.LiveSettings = () => settings;
            var changeListeners = new List<Action>();
            ctx.OnAnyChange = listener => changeListeners.Add(listener);
            bool customizing = _spec.Customizing && _spec.Schema != null;

            // ---- Pages ----
            var pages = new List<(PageSpec Spec, UIElement Page)>();
            foreach (var pageSpec in _spec.Pages)
            {
                UIElement page = null;
                try
                {
                    page = pageSpec.Build != null ? pageSpec.Build(ctx) : _hooks.BuildPage(pageSpec.Id, ctx);
                }
                catch (Exception ex)
                {
                    _log.Error(_spec.LogPrefix + " Page '" + pageSpec.Id + "' could not be built: " + ex.Message);
                    page = Ui.Scroll(Ui.Note("This page could not be built: " + ex.Message));
                }
                if (page != null)
                    pages.Add((pageSpec, page));
            }
            if (pages.Count == 0)
                pages.Add((new PageSpec("empty", "Settings", "⚙️", null), Ui.Scroll(Ui.Note("This window has no pages."))));
            foreach (var wire in ctx.AfterBuild)
            {
                try { wire(); }
                catch (Exception ex) { _log.Warn(_spec.LogPrefix + " Page wiring failed: " + ex.Message); }
            }

            var pageHost = new Grid();
            foreach (var page in pages)
            {
                page.Page.Visibility = Visibility.Collapsed;
                pageHost.Children.Add(page.Page);
            }

            var pageTitle = new TextBlock
            {
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = Theme.LabelFg,
                VerticalAlignment = VerticalAlignment.Center
            };

            var rail = new Rail();
            int current = 0;
            void ShowPage(int index)
            {
                current = index;
                for (int i = 0; i < pages.Count; i++)
                {
                    pages[i].Page.Visibility = i == index ? Visibility.Visible : Visibility.Collapsed;
                    rail.Style(rail.Items[i], i == index);
                }
                pageTitle.Text = pages[index].Spec.Title;
                parts.Refresh(pages[index].Spec.Id);
            }

            var railTop = new StackPanel();
            var railBottom = new StackPanel();
            for (int i = 0; i < pages.Count; i++)
            {
                int index = i;
                var item = rail.Item(pages[i].Spec.Title, pages[i].Spec.Glyph ?? "•", () => ShowPage(index));
                item.ToolTip = i < 9 ? pages[i].Spec.Title + " (Ctrl+" + (i + 1) + ")" : i == 9 ? pages[i].Spec.Title + " (Ctrl+0)" : pages[i].Spec.Title;
                if (customizing)
                    Customize.AttachRail(ctx, pages[i].Spec, item, () => pages.Where(p => !p.Spec.Bottom).Select(p => p.Spec).ToList());
                if (pages[i].Spec.Bottom) railBottom.Children.Add(item);
                else railTop.Children.Add(item);
            }
            var railDock = new DockPanel { Margin = new Thickness(6, 8, 6, 4) };
            DockPanel.SetDock(railBottom, Dock.Bottom);
            railDock.Children.Add(railBottom);
            railDock.Children.Add(railTop);
            var railBorder = new Border
            {
                BorderThickness = new Thickness(0, 0, 1, 0),
                BorderBrush = new SolidColorBrush(Theme.Line),
                Child = new ScrollViewer
                {
                    Content = railDock,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
                }
            };

            // ---- Header ----
            var pills = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var headerPills = new List<(HeaderPill Spec, Border Pill)>();
            foreach (var pillSpec in _spec.HeaderPills)
            {
                var pill = Ui.Pill("", Theme.Surface, Theme.SubColor);
                pill.ToolTip = pillSpec.Tooltip;
                headerPills.Add((pillSpec, pill));
                pills.Children.Add(pill);
            }
            void DrawPills()
            {
                foreach (var (pillSpec, pill) in headerPills)
                {
                    (string text, PillStyle style) = pillSpec.Read(ctx);
                    pill.Visibility = text == null ? Visibility.Collapsed : Visibility.Visible;
                    if (text == null) continue;
                    var colours = Ui.PillColours(style);
                    Ui.SetPill(pill, text, colours.Background, colours.Foreground);
                }
            }
            void ShowUpdate(string latestTag)
            {
                if (string.IsNullOrEmpty(latestTag))
                    return;
                window.Title = _spec.Title + " – Settings  ·  🔔 Update available (" + latestTag + ")";
                var updatePill = Ui.Pill("Update " + latestTag, Theme.Danger, Colors.White);
                updatePill.Cursor = Cursors.Hand;
                updatePill.ToolTip = "Open the releases page";
                updatePill.MouseLeftButtonUp += (s, e) => Web.OpenUrl(_spec.ReleasesUrl, _log, _spec.LogPrefix);
                pills.Children.Insert(0, updatePill);
            }
            // A framework release the user answered "No" to in the loader's dialog: a pill until it is taken.
            {
                var running = LayoutFile.ParseVersion(Entry.FrameworkVersion);
                var available = LayoutFile.ParseVersion(layout.FrameworkAvailable);
                if (running != null && available != null && available > running)
                {
                    var colours = Ui.PillColours(PillStyle.Accent);
                    var frameworkPill = Ui.Pill("UIFramework " + available + " available", colours.Background, colours.Foreground);
                    frameworkPill.ToolTip = "A newer settings window is published (running: " + running + "). The next open of the window offers it again; click for the releases.";
                    frameworkPill.Cursor = Cursors.Hand;
                    frameworkPill.MouseLeftButtonUp += (s, e) => Web.OpenUrl("https://github.com/aaskjer/UIFramework-Streamer.bot/releases", _log, _spec.LogPrefix);
                    pills.Children.Add(frameworkPill);
                }
                else if (available != null)
                {
                    layout.FrameworkAvailable = null;   // a stale note from an older loader
                    try { layout.Save(); } catch (Exception ex) { _log.Warn(_spec.LogPrefix + " Layout file not written: " + ex.Message); }
                }
            }
            if (update.Status == TaskStatus.RanToCompletion)
                ShowUpdate(update.Result);
            else
                update.ContinueWith(done =>
                {
                    if (done.Status == TaskStatus.RanToCompletion)
                        window.Dispatcher.BeginInvoke(new Action(() => ShowUpdate(done.Result)));
                }, TaskScheduler.Default);

            var header = new DockPanel();
            DockPanel.SetDock(pills, Dock.Right);
            header.Children.Add(pills);
            header.Children.Add(pageTitle);
            var headerBorder = new Border
            {
                Padding = new Thickness(16, 9, 14, 9),
                BorderThickness = new Thickness(0, 0, 0, 1),
                BorderBrush = new SolidColorBrush(Theme.Line),
                Child = header
            };

            var body = new Grid();
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(railBorder, 0);
            Grid.SetColumn(pageHost, 1);
            body.Children.Add(railBorder);
            body.Children.Add(pageHost);

            // ---- Footer ----
            var status = new TextBlock
            {
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0),
                Foreground = Theme.MutedFg
            };
            void SetStatus(string text, Color color, string why = null)
            {
                status.Text = text;
                status.Foreground = new SolidColorBrush(color);
                status.ToolTip = why;
            }
            ctx.SetStatus = (text, colour, why) => SetStatus(text, colour, why);

            var baseline = new Dictionary<object, string>();
            FieldBinding.Snapshot(pageHost, baseline);
            int Changes() => FieldBinding.CountChanged(pageHost, baseline);
            changes = Changes;
            currentPage = () => (pages[current].Spec.Id, (pages[current].Page as ScrollViewer)?.VerticalOffset ?? 0);
            void ShowChanges()
            {
                int n = Changes();
                if (n == 0) SetStatus("No unsaved changes", Theme.MutedColor);
                else SetStatus(n == 1 ? "1 unsaved change" : n + " unsaved changes", Theme.UnsavedText);
                DrawPills();
            }
            FieldBinding.HookChangeTracking(pageHost, () =>
            {
                ShowChanges();
                foreach (var listener in changeListeners)
                    listener();
            });
            ShowChanges();
            ctx.LiveSettings = () =>
            {
                parts.CommitNames();
                var live = (JObject)settings.DeepClone();
                FieldBinding.Save(live, pageHost, transientToo: true);
                return live;
            };

            var btnTheme = layout.Rainbow
                ? Ui.IconButton("🌈", null, "Switch between the light and dark theme")
                : Ui.IconButton(Theme.IsDark ? Icons.Sun : Icons.Moon, Icons.Font, "Switch between the light and dark theme (Shift: rainbow)");
            var btnReset = Ui.IconButton(Icons.Reset, Icons.Font, "Reset every setting on every page to its default");
            btnReset.Margin = new Thickness(6, 0, 0, 0);
            var btnResetPage = Ui.IconButton(Icons.Refresh, Icons.Font, "Reset only the page you are looking at, without writing anything yet");
            btnResetPage.Margin = new Thickness(6, 0, 0, 0);
            var btnCancel = Ui.TextButton("Cancel", false);
            var btnSave = Ui.TextButton("Save", true);
            var btnSaveExit = Ui.TextButton("Save & Exit", false);

            bool SaveNow()
            {
                parts.CommitNames();
                if (!ApplyAndSave(settings, pageHost, parts))
                {
                    SetStatus("Not saved", Theme.DangerText);
                    return false;
                }
                baseline.Clear();
                FieldBinding.Snapshot(pageHost, baseline);
                SetStatus("Saved", Theme.SuccessText);
                return true;
            }

            // A layout change or the mode switch: the content is rebuilt in place on the given page.
            ctx.Reopen = Rebuild;

            var btnCustomize = Ui.IconButton(Icons.Edit, Icons.Font, customizing ? "Leave the customise mode" : "Customise: rename, move and hide pages and fields");
            btnCustomize.Margin = new Thickness(6, 0, 0, 0);
            if (customizing)
                btnCustomize.Foreground = new SolidColorBrush(Theme.AccentText);
            btnCustomize.IsEnabled = _spec.Schema != null;
            btnCustomize.Click += (s, e) =>
            {
                if (!customizing && Changes() > 0)
                {
                    var answer = MessageBox.Show(
                        "Save your changes before customising?\nThe pages are rebuilt, and anything unsaved is lost.",
                        "Unsaved Changes", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                    if (answer == MessageBoxResult.Cancel) return;
                    if (answer == MessageBoxResult.Yes && !SaveNow()) return;
                }
                _spec.Customizing = !customizing;
                Rebuild(null);
            };

            // Export writes every setting as it stands in the window to a file of the user's choosing; import reads
            // such a file (or a settings file) into the window, as if typed, so Save is what keeps it.
            var btnExport = Ui.IconButton(Icons.Export, Icons.Font, "Export every setting as it stands in the window to a file");
            btnExport.Margin = new Thickness(6, 0, 0, 0);
            var btnImport = Ui.IconButton(Icons.Import, Icons.Font, "Import settings from an exported file into the window; nothing is saved until you save");
            btnImport.Margin = new Thickness(6, 0, 0, 0);
            string hostName = _spec.Schema?.Host ?? _spec.Title ?? "settings";
            string Folder() { try { return Path.GetDirectoryName(_spec.SettingsPath); } catch (Exception) { return null; } }
            btnExport.Click += (s, e) =>
            {
                parts.CommitNames();
                var live = (JObject)settings.DeepClone();
                FieldBinding.Save(live, pageHost);
                Sidecars.DropTransient(live, _spec.ResolvedSettings);
                var export = new JObject
                {
                    ["uiframework"] = "settings",
                    ["host"] = hostName,
                    ["version"] = _spec.Version ?? "",
                    ["exported"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture),
                    ["settings"] = live
                };
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Export settings",
                    FileName = Regex.Replace(hostName, @"[^\w-]+", "-").Trim('-') + "-settings-" + DateTime.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) + ".json",
                    Filter = "Settings export (*.json)|*.json|All files (*.*)|*.*",
                    DefaultExt = ".json",
                    AddExtension = true
                };
                if (Folder() != null) dialog.InitialDirectory = Folder();
                if (dialog.ShowDialog(window) != true)
                    return;
                try
                {
                    File.WriteAllText(dialog.FileName, export.ToString(Formatting.Indented));
                    int unsaved = Changes();
                    SetStatus("Exported to " + Path.GetFileName(dialog.FileName) + (unsaved > 0 ? ", with " + unsaved + " unsaved change" + (unsaved == 1 ? "" : "s") : ""), Theme.SuccessText, dialog.FileName);
                    _log.Info(_spec.LogPrefix + " Settings exported to " + dialog.FileName);
                }
                catch (Exception ex)
                {
                    SetStatus("Not exported", Theme.DangerText, ex.Message);
                    MessageBox.Show("The file could not be written:\n\n" + ex.Message, "Export Settings", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            };
            btnImport.Click += (s, e) =>
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Import settings",
                    Filter = "Settings export (*.json)|*.json|All files (*.*)|*.*",
                    CheckFileExists = true
                };
                if (Folder() != null) dialog.InitialDirectory = Folder();
                if (dialog.ShowDialog(window) != true)
                    return;
                JObject root;
                try
                {
                    using (var reader = new Newtonsoft.Json.JsonTextReader(new StringReader(File.ReadAllText(dialog.FileName))) { DateParseHandling = Newtonsoft.Json.DateParseHandling.None })
                        root = JObject.Load(reader);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("The file could not be read as settings:\n\n" + ex.Message, "Import Settings", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                // An export carries the settings under "settings"; a settings file itself is the settings.
                var incoming = root["settings"] as JObject ?? root;
                string from = (string)root["host"];
                if (from != null && !string.Equals(from, hostName, StringComparison.OrdinalIgnoreCase) &&
                    MessageBox.Show("This file was exported from '" + from + "', not from '" + hostName + "'.\nImport it anyway? Only the settings this window knows are taken.",
                        "Import Settings", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                    return;
                var known = new JObject();
                int unknown = 0;
                foreach (var property in incoming.Properties())
                {
                    string key = _spec.Aliases.TryGetValue(property.Name, out var canonical) ? canonical : property.Name;
                    var declared = _spec.ResolvedSettings.FirstOrDefault(d => d.Key == key);
                    if (declared == null || declared.Transient || declared.Kind == "action" || declared.Kind == "note" || declared.Kind == "table")
                    {
                        unknown++;
                        continue;
                    }
                    var value = property.Value;
                    if (_spec.AliasFactors.TryGetValue(property.Name, out double factor) && value.Type != JTokenType.String && double.TryParse(value.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double number))
                        value = number * factor;
                    known[key] = value;
                }
                if (known.Count == 0)
                {
                    MessageBox.Show("Nothing in " + Path.GetFileName(dialog.FileName) + " matches a setting of this window.", "Import Settings", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                var answer = MessageBox.Show(
                    "Put " + known.Count + " setting" + (known.Count == 1 ? "" : "s") + " from " + Path.GetFileName(dialog.FileName) + " into the window?" +
                    (unknown > 0 ? "\n" + unknown + " entr" + (unknown == 1 ? "y" : "ies") + " the window does not know " + (unknown == 1 ? "is" : "are") + " skipped." : "") +
                    "\nThe values replace what the window shows; nothing is saved until you save.",
                    "Import Settings", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (answer != MessageBoxResult.Yes)
                    return;
                var placed = FieldBinding.Apply(pageHost, known, true);
                // A setting without a field (hidden) goes straight into the settings, for the next save.
                foreach (var property in known.Properties().Where(p => !placed.Contains(p.Name)))
                    settings[property.Name] = property.Value.DeepClone();
                foreach (var page in pages)
                    parts.Refresh(page.Spec.Id);
                SetStatus("Imported " + known.Count + " setting" + (known.Count == 1 ? "" : "s") + " from " + Path.GetFileName(dialog.FileName) + "; save to keep them", Theme.UnsavedText, dialog.FileName);
                _log.Info(_spec.LogPrefix + " Settings imported from " + dialog.FileName + " (" + known.Count + " taken, " + unknown + " skipped).");
            };

            btnSave.Click += (s, e) => SaveNow();
            btnSaveExit.Click += (s, e) => { if (SaveNow()) { closeConfirmed = true; window.Close(); } };
            btnCancel.Click += (s, e) =>
            {
                int n = Changes();
                if (n > 0)
                {
                    var confirm = MessageBox.Show(
                        "Discard " + n + " unsaved change" + (n == 1 ? "" : "s") + "?",
                        "Unsaved Changes", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (confirm != MessageBoxResult.Yes)
                        return;
                }
                closeConfirmed = true;
                window.Close();
            };
            btnResetPage.Click += (s, e) =>
            {
                var confirm = MessageBox.Show(
                    "Reset every setting on '" + pages[current].Spec.Title + "' back to its default?\n" +
                    "The other pages are left alone, and nothing is written until you save.",
                    "Reset Page", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (confirm != MessageBoxResult.Yes)
                    return;
                FieldBinding.ResetToDefaults(pages[current].Page, defaults);
                parts.Refresh(pages[current].Spec.Id);
                ShowChanges();
            };
            btnReset.Click += (s, e) =>
            {
                var confirm = MessageBox.Show(
                    "Reset all settings to defaults?\nThis cannot be undone.",
                    "Reset Settings", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (confirm != MessageBoxResult.Yes) return;

                var fresh = (JObject)defaults.DeepClone();
                try { _store.Save(Sidecars.Strip(fresh, _spec.ResolvedSettings)); }
                catch (Exception ex)
                {
                    SetStatus("Not reset", Theme.DangerText, ex.Message);
                    return;
                }
                SafeHook(() => _hooks.OnSaved(fresh));
                settings = fresh;
                Sidecars.Load(settings, _spec.ResolvedSettings, _spec.SettingsPath, _log, _spec.LogPrefix);
                Rebuild(null);
            };
            btnTheme.Click += (s, e) =>
            {
                bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
                if (Changes() > 0)
                {
                    var answer = MessageBox.Show(
                        "Save your changes before switching?\nThe pages are rebuilt, and anything unsaved is lost.",
                        "Unsaved Changes", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                    if (answer == MessageBoxResult.Cancel) return;
                    if (answer == MessageBoxResult.Yes && !SaveNow()) return;
                }
                try
                {
                    if (shift) layout.Rainbow = !layout.Rainbow;
                    else layout.Dark = !(layout.Dark ?? true);
                    layout.Save();
                }
                catch (Exception ex)
                {
                    SetStatus("Not switched", Theme.DangerText, ex.Message);
                    return;
                }
                Rebuild(null);
            };

            // Ctrl+S saves, Ctrl+1 to Ctrl+9 jump to the first nine pages in the rail's order.
            if (keyHandler != null)
                window.PreviewKeyDown -= keyHandler;
            keyHandler = (s, e) =>
            {
                if ((Keyboard.Modifiers & ModifierKeys.Control) == 0)
                    return;
                if (e.Key == Key.S)
                {
                    SaveNow();
                    e.Handled = true;
                }
                else if (e.Key >= Key.D1 && e.Key <= Key.D9 && e.Key - Key.D1 < pages.Count)
                {
                    ShowPage(e.Key - Key.D1);
                    e.Handled = true;
                }
                else if (e.Key == Key.D0 && pages.Count >= 10)
                {
                    ShowPage(9);
                    e.Handled = true;
                }
            };
            window.PreviewKeyDown += keyHandler;

            var leftButtons = new StackPanel { Orientation = Orientation.Horizontal };
            leftButtons.Children.Add(btnTheme);
            leftButtons.Children.Add(btnReset);
            leftButtons.Children.Add(btnResetPage);
            leftButtons.Children.Add(btnCustomize);
            leftButtons.Children.Add(btnExport);
            leftButtons.Children.Add(btnImport);
            var rightButtons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            rightButtons.Children.Add(status);
            if (customizing)
            {
                SetStatus("Customise mode", Theme.AccentText);
                // Nothing to save in the customise mode: values are frozen, the layout file is written on every change.
                btnReset.IsEnabled = false;
                btnResetPage.IsEnabled = false;
                btnImport.IsEnabled = false;
                btnSave.Visibility = Visibility.Collapsed;
                btnSaveExit.Visibility = Visibility.Collapsed;
                btnCancel.Content = "Close";
            }
            rightButtons.Children.Add(btnCancel);
            rightButtons.Children.Add(btnSave);
            rightButtons.Children.Add(btnSaveExit);
            var footerDock = new DockPanel();
            DockPanel.SetDock(leftButtons, Dock.Left);
            footerDock.Children.Add(leftButtons);
            footerDock.Children.Add(rightButtons);
            var footer = new Border
            {
                Padding = new Thickness(12, 9, 12, 10),
                BorderThickness = new Thickness(0, 1, 0, 0),
                BorderBrush = new SolidColorBrush(Theme.Line),
                Child = footerDock
            };

            var root = new DockPanel();
            DockPanel.SetDock(headerBorder, Dock.Top);
            DockPanel.SetDock(footer, Dock.Bottom);
            root.Children.Add(headerBorder);
            if (customizing)
            {
                var bar = Customize.Bar(ctx, () => { _spec.Customizing = false; Rebuild(null); });
                DockPanel.SetDock(bar, Dock.Top);
                root.Children.Add(bar);
                window.Title = _spec.Title + " – Settings  ·  Customise";
            }
            root.Children.Add(footer);
            root.Children.Add(body);
            window.Content = root;

            int startPage = _spec.ReopenPageId == null ? -1 : pages.FindIndex(p => p.Spec.Id == _spec.ReopenPageId);
            _spec.ReopenPageId = null;
            ShowPage(startPage < 0 ? 0 : startPage);
            if (keepOffset != null && pages[current].Page is ScrollViewer scroller)
            {
                double offset = keepOffset.Value;
                keepOffset = null;
                window.Dispatcher.BeginInvoke(new Action(() => scroller.ScrollToVerticalOffset(offset)), DispatcherPriority.Loaded);
            }

            if (layout.Rainbow)
                rainbowTimer = Rainbow.Start(root, headerBorder, rail.Items);
            }

            Build();

            window.Closing += (s, e) =>
            {
                if (closeConfirmed || _shuttingDown)
                    return;
                int n = changes();
                if (n == 0)
                    return;
                var confirm = MessageBox.Show(
                    "Discard " + n + " unsaved change" + (n == 1 ? "" : "s") + "?",
                    "Unsaved Changes", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (confirm != MessageBoxResult.Yes)
                    e.Cancel = true;
            };

            window.Closed += (s, e) =>
            {
                _window = null;
                rainbowTimer?.Stop();
                Theme.IsRainbow = false;

                if (layout.Rainbow)
                {
                    // Rainbow is a one-off, like the games' Egg: off again once the window closes.
                    try { layout.Rainbow = false; layout.Save(); }
                    catch (Exception ex) { _log.Warn(_spec.LogPrefix + " Rainbow mode not turned off: " + ex.Message); }
                }
            };

            _window = window;
            window.ShowDialog();
        }

        private bool ApplyAndSave(JObject settings, UIElement root, WindowParts parts)
        {
            try
            {
                var invalid = FieldBinding.InvalidNumberFields(root);
                if (invalid.Count > 0)
                {
                    MessageBox.Show("These fields do not hold a usable number, so nothing was saved:\n\n" + string.Join("\n", invalid),
                        "Check the numbers", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                parts.CommitNames();
                FieldBinding.Save(settings, root);
                _spec.Clamp?.Invoke(settings);

                var warnings = _spec.Warnings?.Invoke(settings) ?? new List<string>();
                string fromHost = null;
                SafeHook(() => fromHost = _hooks.Validate(settings));
                if (!string.IsNullOrWhiteSpace(fromHost))
                    warnings.AddRange(fromHost.Replace("\r", "").Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0));
                if (warnings.Count > 0)
                {
                    var proceed = MessageBox.Show(
                        "These settings will not do what they look like they do:\n\n  • " +
                        string.Join("\n  • ", warnings) +
                        "\n\nSave anyway?",
                        "Check your settings", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (proceed != MessageBoxResult.Yes)
                        return false;
                }

                _store.Save(Sidecars.Split(settings, _spec.ResolvedSettings, _spec.SettingsPath));
                SafeHook(() => _hooks.OnSaved(settings));
                _log.Info(_spec.LogPrefix + " Settings saved.");
                return true;
            }
            catch (Exception ex)
            {
                _log.Error(_spec.LogPrefix + " Save error: " + ex.Message);
                MessageBox.Show("Could not save: " + ex.Message, "Settings", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private void SafeHook(Action hook)
        {
            try { hook(); }
            catch (Exception ex) { _log.Warn(_spec.LogPrefix + " Host hook failed: " + ex.Message); }
        }

        private sealed class NullLog : IHostLog
        {
            public void Info(string message) { }
            public void Warn(string message) { }
            public void Error(string message) { }
        }
    }

    /// <summary>The rainbow mode: headings, rail borders and the header line cycle through the hues.</summary>
    public static class Rainbow
    {
        public static DispatcherTimer Start(object root, Border headerBorder, List<Border> railItems)
        {
            var textTargets = new List<TextBlock>();
            FieldBinding.VisitTree(root, obj =>
            {
                if (obj is TextBlock tb && (tb.FontWeight == FontWeights.SemiBold || tb.FontWeight == FontWeights.Bold || tb.FontSize >= 13))
                    textTargets.Add(tb);
            });
            var rails = railItems.ToList();
            Theme.IsRainbow = true;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
            timer.Tick += (s, e) =>
            {
                Theme.RainbowTick();
                for (int i = 0; i < textTargets.Count; i++)
                    textTargets[i].Foreground = Theme.RainbowBrush(i * 18.0);
                for (int i = 0; i < rails.Count; i++)
                    rails[i].BorderBrush = Theme.RainbowBrush(i * 24.0);
                headerBorder.BorderBrush = Theme.RainbowBrush(0);
            };
            timer.Start();
            return timer;
        }
    }
}
