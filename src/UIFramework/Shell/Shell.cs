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
        // What went wrong, for the red "i" in the header: the schema's problems and what failed while the window was open.
        private readonly ProblemList _problems;
        // Lists in files of their own that could not be read when the window opened: they show their defaults, so they
        // are never written from this window.
        private readonly HashSet<string> _unreadableLists = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>Whether the keys held make a shortcut of the window's (Ctrl+S, Ctrl+F, Ctrl+1): Ctrl without Alt.
        /// AltGr is Ctrl+Alt to Windows, and types a character: "{" on a German keyboard, "@" on a Spanish one.</summary>
        public static bool IsShortcut(ModifierKeys held) =>
            (held & ModifierKeys.Control) != 0 && (held & ModifierKeys.Alt) == 0;
        // The status line of the window that is up, for an error a handler did not catch.
        private Action<string> _onError;

        /// <summary>Set by a tool (the preview's -windowshots): instead of waiting for the user, the window photographs
        /// every page whole (rail, header, footer, the page at its full height) and closes. Never set in Streamer.bot.</summary>
        public static WindowCapture Capture;

        private Shell(WindowSpec spec, IHostLog log, IHostHooks hooks)
        {
            _spec = spec ?? throw new ArgumentNullException(nameof(spec));
            _log = log ?? new NullLog();
            _hooks = hooks ?? new NoHooks();
            if (string.IsNullOrWhiteSpace(spec.SettingsPath))
                throw new ArgumentException("WindowSpec.SettingsPath is required.");
            _store = new SettingsStore(spec.SettingsPath, spec.Defaults).WithAliases(spec.Aliases, spec.AliasFactors, spec.AliasesShared);
            _gateKey = Path.GetFullPath(spec.SettingsPath);
            _problems = new ProblemList(() => _spec.Problems);
        }

        /// <summary>A failure while the window is open: into Streamer.bot's log as an error, and under the red "i".</summary>
        private void Failed(string message)
        {
            _log.Error(_spec.LogPrefix + " " + message);
            _problems.Add(message);
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
                    return _owns = true;
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

        // Whether this shell still holds the claim: the window thread lets go when its window closes, and Run's own
        // release afterwards must not take the claim of a window opened again in between.
        private bool _owns;

        private void Release()
        {
            lock (Gate)
            {
                if (!_owns) return;
                _owns = false;
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
                        // The window is gone: the next open may claim it again, even while the pump below is still
                        // stuck in a game action that never returns.
                        Release();
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
            // A mistake in one handler (a click, a timer) is logged and the window stays, with what the user typed.
            Dispatcher.CurrentDispatcher.UnhandledException += (s, e) =>
            {
                Failed(e.Exception.GetType().Name + " in the window: " + e.Exception.Message);
                try { _onError?.Invoke(e.Exception.Message); } catch (Exception) { }
                e.Handled = true;
            };
            string fileName = Path.GetFileName(_spec.SettingsPath);
            SettingsLoad loaded;
            try { loaded = _store.Load(); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                _log.Warn(_spec.LogPrefix + " " + fileName + " could not be opened: " + ex.Message);
                MessageBox.Show(Text.T("dialog.notOpened.text", fileName, ex.Message), Text.T("dialog.notOpened.title"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (loaded.Damaged != null)
                MessageBox.Show(
                    Text.T("dialog.damaged.text", loaded.Damaged) +
                    (loaded.FromBackup ? Text.T("dialog.damaged.backup", fileName) : Text.T("dialog.damaged.defaults")) +
                    Text.T("dialog.damaged.kept", fileName),
                    Text.T("dialog.damaged.title"), MessageBoxButton.OK, MessageBoxImage.Warning);

            var settings = loaded.Settings;
            // Every open resolves afresh, so a layout change made in the customise mode shows up after the rebuild.
            if (_spec.Schema != null)
                Entry.Prepare(_spec, _spec.Schema);
            var layout = _spec.Layout ?? LayoutFile.Load(_spec.SettingsPath);
            _spec.Layout = layout;
            if (layout.Problem != null)
            {
                _log.Warn(_spec.LogPrefix + " " + LayoutFile.FileName + ": " + layout.Problem);
                _problems.Add(LayoutFile.FileName + ": " + layout.Problem);
            }
            if (layout.AdoptFromHost(settings, new HashSet<string>(_spec.ResolvedSettings.Select(s => s.Key), StringComparer.Ordinal), "darkMode", "DarkMode"))
            {
                // Split with nothing changed: a list still in the settings file whose own file is not there yet moves
                // there now, instead of being dropped with the theme key's write.
                try { layout.Save(); if (loaded.Damaged == null) _store.Save(Sidecars.Split(settings, _spec.ResolvedSettings, _spec.SettingsPath, new HashSet<string>())); }
                catch (Exception ex) { _log.Warn(_spec.LogPrefix + " Theme not moved to the layout file: " + ex.Message); }
            }
            layout.ApplyTheme();
            if (_spec.Locked) Theme.AccentOverride = null;   // a locked window keeps the game's look
            // Values kept in files of their own (a word bank, a question list) come in after the settings file.
            Sidecars.Load(settings, _spec.ResolvedSettings, _spec.SettingsPath, _log, _spec.LogPrefix, _unreadableLists);
            foreach (var key in _unreadableLists)
            {
                var list = _spec.ResolvedSettings.FirstOrDefault(s => s.Key == key);
                _problems.Add("'" + (list?.Label ?? key) + "' could not be read from " + (list != null ? System.IO.Path.GetFileName(Sidecars.PathOf(list, _spec.SettingsPath)) : "its file")
                    + ", so it shows its defaults and this window will not write it. Close the window and open it again once the file is free or repaired.");
            }

            var window = new Window
            {
                Title = _spec.Title + Text.T("title.settings"),
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
            bool captureStarted = false;
            // The tour, while it is up; the shell's shortcuts wait for it. StartTour is set by every build.
            Tour tour = null;
            Action startTour = null;
            // The red "i": its panel, and the listener that shows the button when something new goes wrong.
            Action openProblems = null;
            Action problemsChanged = null;

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
                if (_spec.Locked) Theme.AccentOverride = null;
                _spec.ReopenPageId = pageId;
                Build();
            }

            void Build()
            {
            tour?.End();
            rainbowTimer?.Stop();
            rainbowTimer = null;
            Theme.IsRainbow = false;
            window.Title = _spec.Title + Text.T("title.settings");
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
            if (_spec.Locked) _spec.Customizing = false;
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
                    Failed("Page '" + pageSpec.Id + "' could not be built: " + ex.Message);
                    page = Ui.Scroll(Ui.Note(Text.T("page.failed", ex.Message)));
                }
                if (page != null)
                    pages.Add((pageSpec, page));
            }
            if (pages.Count == 0)
                pages.Add((new PageSpec("empty", Text.T("page.none.title"), "⚙️", null), Ui.Scroll(Ui.Note(Text.T("page.none.text")))));
            foreach (var wire in ctx.AfterBuild)
            {
                try { wire(); }
                catch (Exception ex) { _log.Warn(_spec.LogPrefix + " Page wiring failed: " + ex.Message); _problems.Add("Page wiring failed: " + ex.Message); }
            }
            // A list whose file could not be read is not written, so it is not to be edited either: rows typed into it
            // were dropped by a save that said "Saved".
            foreach (var key in _unreadableLists)
                if (parts.Lists.TryGetValue(key, out var unread))
                {
                    unread.IsEnabled = false;
                    unread.ToolTip = Text.T("list.unreadable");
                    System.Windows.Controls.ToolTipService.SetShowOnDisabled(unread, true);
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
                item.ToolTip = i < 9 ? Text.T("rail.shortcut", pages[i].Spec.Title, i + 1) : i == 9 ? Text.T("rail.shortcut", pages[i].Spec.Title, 0) : pages[i].Spec.Title;
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
                window.Title = _spec.Title + Text.T("title.settings") + Text.T("title.update", latestTag);
                var updatePill = Ui.Pill(Text.T("pill.update", latestTag), Theme.Danger, Colors.White);
                updatePill.Cursor = Cursors.Hand;
                updatePill.ToolTip = Text.T("pill.update.tip");
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
                    var frameworkPill = Ui.Pill(Text.T("pill.framework", available), colours.Background, colours.Foreground);
                    frameworkPill.ToolTip = Text.T("pill.framework.tip", running);
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
            // The ? at the far right shows the window's tour again, when it has one; not in the customise mode.
            var btnTour = Ui.IconButton(Icons.Help, Icons.Font, Text.T("tip.tour"));
            btnTour.Margin = new Thickness(8, -4, -4, -4);
            btnTour.Click += (s, e) => startTour?.Invoke();
            if (_spec.Tour.Count > 0 && !customizing)
            {
                DockPanel.SetDock(btnTour, Dock.Right);
                header.Children.Add(btnTour);
            }
            // The magnifier beside it, and Ctrl+F: the search for a setting, a message or a page.
            var layers = new Grid();
            var btnSearch = Ui.IconButton(Icons.Search, Icons.Font, Text.T("tip.search"));
            btnSearch.Name = "SearchButton";
            btnSearch.Margin = new Thickness(8, -4, 0, -4);
            SearchBar search = null;
            btnSearch.Click += (s, e) => search?.Open();
            DockPanel.SetDock(btnSearch, Dock.Right);
            header.Children.Add(btnSearch);
            // The red "i" beside it, while something went wrong: the schema's problems and the window's failures.
            var btnProblems = ProblemsView.Button();
            btnProblems.Margin = new Thickness(8, -4, 0, -4);
            openProblems = () => ProblemsView.Open(window, layers, btnProblems, _problems);
            btnProblems.Click += (s, e) => openProblems();
            DockPanel.SetDock(btnProblems, Dock.Right);
            header.Children.Add(btnProblems);
            if (problemsChanged != null) _problems.Changed -= problemsChanged;
            problemsChanged = () => window.Dispatcher.BeginInvoke(new Action(() => ProblemsView.Update(btnProblems, _problems)));
            _problems.Changed += problemsChanged;
            ProblemsView.Update(btnProblems, _problems);
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
            // The status takes what the footer's buttons leave, so a long text in a narrow window is cut with an
            // ellipsis (the whole text is in its tooltip) instead of running under the buttons on the left.
            var status = new TextBlock
            {
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Right,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(10, 0, 6, 0),
                Foreground = Theme.MutedFg
            };
            void SetStatus(string text, Color color, string why = null)
            {
                status.Text = text;
                status.Foreground = new SolidColorBrush(color);
                status.ToolTip = why ?? text;
            }
            ctx.SetStatus = (text, colour, why) => SetStatus(text, colour, why);
            _onError = message => SetStatus(Text.T("status.error"), Theme.DangerText, message);

            var baseline = new Dictionary<object, string>();
            FieldBinding.Snapshot(pageHost, baseline);
            // Numbers the window shows other than the file holds them (outside their limits, not a number): the next
            // save writes them too, so what the window shows is what the game reads.
            var repairs = FieldBinding.Repaired(settings, pageHost);
            int Changes() => FieldBinding.CountChanged(pageHost, baseline);
            changes = () => { parts.CommitNames(); return Changes(); };
            currentPage = () => (pages[current].Spec.Id, (pages[current].Page as ScrollViewer)?.VerticalOffset ?? 0);
            void ShowChanges()
            {
                int n = Changes();
                if (n == 0) SetStatus(Text.T("status.clean"), Theme.MutedColor);
                else SetStatus(n == 1 ? Text.T("status.unsaved.one") : Text.T("status.unsaved.many", n), Theme.UnsavedText);
                DrawPills();
            }
            FieldBinding.HookChangeTracking(pageHost, () =>
            {
                ShowChanges();
                foreach (var listener in changeListeners)
                    listener();
            });
            ShowChanges();
            // What the window shows right now, read without touching it: a name half typed or a cell being edited
            // stays as it is (an action that follows the fields asks 350 ms after a change, in the middle of typing).
            ctx.LiveSettings = () =>
            {
                var live = (JObject)settings.DeepClone();
                FieldBinding.Save(live, pageHost, transientToo: true);
                return live;
            };

            var btnTheme = layout.Rainbow
                ? Ui.IconButton("🌈", null, Text.T("tip.theme"))
                : Ui.IconButton(Theme.IsDark ? Icons.Sun : Icons.Moon, Icons.Font, Text.T("tip.theme.rainbow"));
            var btnReset = Ui.IconButton(Icons.Reset, Icons.Font, Text.T("tip.resetAll"));
            btnReset.Margin = new Thickness(6, 0, 0, 0);
            var btnResetPage = Ui.IconButton(Icons.Refresh, Icons.Font, Text.T("tip.resetPage"));
            btnResetPage.Margin = new Thickness(6, 0, 0, 0);
            var btnCancel = Ui.TextButton(Text.T("btn.cancel"), false);
            var btnSave = Ui.TextButton(Text.T("btn.save"), true);
            var btnSaveExit = Ui.TextButton(Text.T("btn.saveExit"), false);

            bool SaveNow()
            {
                parts.CommitNames();
                if (!ApplyAndSave(settings, pageHost, parts, baseline, repairs))
                {
                    SetStatus(Text.T("status.notSaved"), Theme.DangerText);
                    return false;
                }
                baseline.Clear();
                FieldBinding.Snapshot(pageHost, baseline);
                repairs.Clear();
                SetStatus(Text.T("status.saved"), Theme.SuccessText);
                return true;
            }

            // A layout change or the mode switch: the content is rebuilt in place on the given page.
            ctx.Reopen = Rebuild;

            var btnCustomize = Ui.IconButton(Icons.Edit, Icons.Font, customizing ? Text.T("tip.customise.leave") : Text.T("tip.customise"));
            btnCustomize.Margin = new Thickness(6, 0, 0, 0);
            if (customizing)
                btnCustomize.Foreground = new SolidColorBrush(Theme.AccentText);
            btnCustomize.IsEnabled = _spec.Schema != null;
            btnCustomize.Click += (s, e) =>
            {
                if (!customizing && Changes() > 0)
                {
                    var answer = MessageBox.Show(Text.T("dialog.unsaved.customise"), Text.T("dialog.unsaved.title"), MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                    if (answer == MessageBoxResult.Cancel) return;
                    if (answer == MessageBoxResult.Yes && !SaveNow()) return;
                }
                _spec.Customizing = !customizing;
                Rebuild(null);
            };

            // Export and import live in the customise mode's bar. Export writes the saved settings to a file of the
            // user's choosing; import replaces the saved settings with such a file (or a settings file) at once, old
            // keys through the aliases, and rebuilds the window on them.
            string hostName = _spec.Schema?.Host ?? _spec.Title ?? "settings";
            string Folder() { try { return Path.GetDirectoryName(_spec.SettingsPath); } catch (Exception) { return null; } }
            void Export()
            {
                var live = (JObject)settings.DeepClone();
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
                    Title = Text.T("export.title"),
                    FileName = Regex.Replace(hostName, @"[^\w-]+", "-").Trim('-') + "-settings-" + DateTime.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) + ".json",
                    Filter = Text.T("export.filter"),
                    DefaultExt = ".json",
                    AddExtension = true
                };
                if (Folder() != null) dialog.InitialDirectory = Folder();
                if (dialog.ShowDialog(window) != true)
                    return;
                try
                {
                    File.WriteAllText(dialog.FileName, export.ToString(Formatting.Indented));
                    SetStatus(Text.T("status.exported", Path.GetFileName(dialog.FileName)), Theme.SuccessText, dialog.FileName);
                    _log.Info(_spec.LogPrefix + " Settings exported to " + dialog.FileName);
                }
                catch (Exception ex)
                {
                    SetStatus(Text.T("status.notExported"), Theme.DangerText, ex.Message);
                    MessageBox.Show(Text.T("export.failed.text", ex.Message), Text.T("export.failed.title"), MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            void Import()
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = Text.T("import.title"),
                    Filter = Text.T("export.filter"),
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
                    MessageBox.Show(Text.T("import.unreadable", ex.Message), Text.T("import.dialog.title"), MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                // An export carries the settings under "settings"; a settings file itself is the settings.
                var incoming = root["settings"] as JObject ?? root;
                string from = (string)root["host"];
                if (from != null && !string.Equals(from, hostName, StringComparison.OrdinalIgnoreCase) &&
                    MessageBox.Show(Text.T("import.otherHost", from, hostName), Text.T("import.dialog.title"), MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                    return;
                // Read as the settings file is read: old keys with their factors, dotted ones inside an object, the
                // current key winning over an old one left beside it.
                var provided = new HashSet<string>(StringComparer.Ordinal);
                var read = _store.Merged(incoming, provided);
                var known = new JObject();
                int unknown = 0;
                foreach (var key in provided)
                {
                    var declared = _spec.ResolvedSettings.FirstOrDefault(d => d.Key == key);
                    if (declared == null || declared.Transient || declared.Kind == "action" || declared.Kind == "note" || declared.Kind == "table")
                    {
                        unknown++;
                        continue;
                    }
                    known[key] = read[key]?.DeepClone();
                }
                if (known.Count == 0)
                {
                    MessageBox.Show(Text.T("import.nothing", Path.GetFileName(dialog.FileName)), Text.T("import.dialog.title"), MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                var answer = MessageBox.Show(
                    (known.Count == 1 ? Text.T("import.confirm.one", Path.GetFileName(dialog.FileName)) : Text.T("import.confirm.many", known.Count, Path.GetFileName(dialog.FileName))) +
                    (unknown == 0 ? "" : unknown == 1 ? Text.T("import.skipped.one") : Text.T("import.skipped.many", unknown)) +
                    Text.T("import.written", Path.GetFileName(_spec.SettingsPath)),
                    Text.T("import.dialog.title"), MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (answer != MessageBoxResult.Yes)
                    return;
                // Over the file as it is now, as a save does: what the game wrote meanwhile stays, and a list file is
                // written only when the import brings its list.
                JObject merged;
                try
                {
                    var now = _store.Load();
                    merged = now.Damaged == null ? now.Settings : (JObject)settings.DeepClone();
                    if (now.Damaged == null)
                        Sidecars.Load(merged, _spec.ResolvedSettings, _spec.SettingsPath, null, _spec.LogPrefix);
                    foreach (var property in known.Properties())
                        merged[property.Name] = property.Value?.DeepClone();
                    _spec.Clamp?.Invoke(merged);
                    _store.Save(Sidecars.Split(merged, _spec.ResolvedSettings, _spec.SettingsPath, new HashSet<string>(known.Properties().Select(p => p.Name), StringComparer.Ordinal), _unreadableLists));
                }
                catch (Exception ex)
                {
                    MessageBox.Show(Text.T("import.notWritten", ex.Message), Text.T("import.dialog.title"), MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                SafeHook(() => _hooks.OnSaved(merged));
                settings = merged;
                _log.Info(_spec.LogPrefix + " Settings imported from " + dialog.FileName + " (" + known.Count + " taken, " + unknown + " skipped) and saved.");
                Rebuild(null);
            }

            btnSave.Click += (s, e) => SaveNow();
            btnSaveExit.Click += (s, e) => { if (SaveNow()) { closeConfirmed = true; window.Close(); } };
            btnCancel.Click += (s, e) =>
            {
                int n = Changes();
                if (n > 0)
                {
                    var confirm = MessageBox.Show(
                        n == 1 ? Text.T("dialog.discard.one") : Text.T("dialog.discard.many", n),
                        Text.T("dialog.unsaved.title"), MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (confirm != MessageBoxResult.Yes)
                        return;
                }
                closeConfirmed = true;
                window.Close();
            };
            btnResetPage.Click += (s, e) =>
            {
                var confirm = MessageBox.Show(Text.T("dialog.resetPage.text", pages[current].Spec.Title), Text.T("dialog.resetPage.title"), MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (confirm != MessageBoxResult.Yes)
                    return;
                // As Reset all: a list in a file of its own (its own Restore defaults button resets it), a transient
                // and a read-only setting keep what they show.
                var resettable = new JObject(defaults.Properties()
                    .Where(p => _spec.ResolvedSettings.FirstOrDefault(x => x.Key == p.Name) is var st && (st == null || (string.IsNullOrWhiteSpace(st.File) && !st.Transient && !st.ReadOnly)))
                    .Select(p => new JProperty(p.Name, p.Value?.DeepClone())));
                FieldBinding.Apply(pages[current].Page, resettable, true);
                parts.Refresh(pages[current].Spec.Id);
                ShowChanges();
            };
            btnReset.Click += (s, e) =>
            {
                var confirm = MessageBox.Show(Text.T("dialog.resetAll.text"), Text.T("dialog.resetAll.title"), MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (confirm != MessageBoxResult.Yes) return;

                // The file as it is now with the defaults laid over what the window shows: keys it has no field for (a
                // game's own state) and hidden settings keep their values. A damaged file starts from the window's copy.
                SettingsLoad now = null;
                try { now = _store.Load(); } catch (Exception) { now = null; }
                var fresh = SettingsStore.ResetShown(now != null && now.Damaged == null ? now.Settings : settings, defaults, _spec.ResolvedSettings, FieldBinding.ShownKeys(pageHost));
                try { _store.Save(Sidecars.Split(fresh, _spec.ResolvedSettings, _spec.SettingsPath, new HashSet<string>(), _unreadableLists)); }
                catch (Exception ex)
                {
                    SetStatus(Text.T("status.notReset"), Theme.DangerText, ex.Message);
                    return;
                }
                // The lists in their own files are not reset: the game hears of them as they are in those files.
                Sidecars.Load(fresh, _spec.ResolvedSettings, _spec.SettingsPath, _log, _spec.LogPrefix);
                SafeHook(() => _hooks.OnSaved(fresh));
                settings = fresh;
                Rebuild(null);
            };
            btnTheme.Click += (s, e) =>
            {
                bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
                if (Changes() > 0)
                {
                    var answer = MessageBox.Show(Text.T("dialog.unsaved.switch"), Text.T("dialog.unsaved.title"), MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
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
                    SetStatus(Text.T("status.notSwitched"), Theme.DangerText, ex.Message);
                    return;
                }
                Rebuild(null);
            };

            // Ctrl+S saves, Ctrl+F searches, Ctrl+1 to Ctrl+9 jump to the first nine pages in the rail's order.
            if (keyHandler != null)
                window.PreviewKeyDown -= keyHandler;
            keyHandler = (s, e) =>
            {
                // Ctrl alone: AltGr arrives as Ctrl+Alt, and AltGr+7 is the "{" of a German keyboard, not Ctrl+7.
                if (!IsShortcut(Keyboard.Modifiers) || (tour != null && tour.Open))
                    return;
                if (e.Key == Key.S)
                {
                    if (!customizing) SaveNow();   // the customise mode has no Save; its changes are written as made
                    e.Handled = true;
                }
                else if (e.Key == Key.F)
                {
                    search?.Open();
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
            if (!_spec.Locked)
                leftButtons.Children.Add(btnCustomize);
            else
            {
                // Locked: no customise mode, but the language stays the user's to pick when the game offers more than one.
                var language = Customize.LanguagePicker(_spec, layout, () =>
                {
                    try { layout.Save(); } catch (Exception ex) { _log.Warn(_spec.LogPrefix + " Language not saved: " + ex.Message); }
                    // A rebuild starts from what is saved: unsaved edits are offered for saving first, as the theme does.
                    // Cancel keeps the window as it is; the language then applies the next time it opens.
                    if (Changes() > 0)
                    {
                        var answer = MessageBox.Show(Text.T("dialog.unsaved.switch"), Text.T("dialog.unsaved.title"), MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                        if (answer == MessageBoxResult.Cancel) return;
                        if (answer == MessageBoxResult.Yes && !SaveNow()) return;
                    }
                    Rebuild(null);
                });
                if (language != null)
                {
                    language.Margin = new Thickness(6, 0, 0, 0);
                    language.ToolTip = Text.T("cust.language");
                    leftButtons.Children.Add(language);
                }
            }
            var rightButtons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            if (customizing)
            {
                SetStatus(Text.T("status.customise"), Theme.AccentText);
                // Nothing to save in the customise mode: values are frozen, the layout file is written on every change.
                btnReset.IsEnabled = false;
                btnResetPage.IsEnabled = false;
                btnSave.Visibility = Visibility.Collapsed;
                btnSaveExit.Visibility = Visibility.Collapsed;
                btnCancel.Content = Text.T("btn.close");
            }
            rightButtons.Children.Add(btnCancel);
            rightButtons.Children.Add(btnSave);
            rightButtons.Children.Add(btnSaveExit);
            var footerDock = new DockPanel();
            DockPanel.SetDock(leftButtons, Dock.Left);
            DockPanel.SetDock(rightButtons, Dock.Right);
            footerDock.Children.Add(leftButtons);
            footerDock.Children.Add(rightButtons);
            footerDock.Children.Add(status);
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
                var bar = Customize.Bar(ctx, () => { _spec.Customizing = false; Rebuild(null); }, Export, Import);
                DockPanel.SetDock(bar, Dock.Top);
                root.Children.Add(bar);
                window.Title = _spec.Title + Text.T("title.settings") + Text.T("title.customise");
            }
            // What the schema got wrong: in Streamer.bot's log, and under the red "i" in the header (no banner, no
            // list on a page: the pages stay the user's).
            foreach (var problem in _spec.Problems)
                _log.Warn(_spec.LogPrefix + " Schema: " + problem);
            root.Children.Add(footer);
            root.Children.Add(body);
            // The window's content, and over it, while it is up, the tour or the problems panel.
            layers.Children.Add(root);
            window.Content = layers;

            // ---- The tour: each stop the spec names, resolved against this build's parts. ----
            var tourParts = new Dictionary<string, FrameworkElement>(StringComparer.Ordinal)
            {
                ["rail"] = railBorder, ["save"] = btnSave, ["saveExit"] = btnSaveExit, ["cancel"] = btnCancel, ["status"] = status,
                ["reset"] = btnReset, ["resetPage"] = btnResetPage, ["theme"] = btnTheme, ["tour"] = btnTour, ["search"] = btnSearch
            };

            // ---- The search: every page, every setting a page shows and every message, as this build has them. ----
            List<SearchEntry> SearchEntries()
            {
                var entries = new List<SearchEntry>();
                for (int i = 0; i < pages.Count; i++)
                {
                    int index = i;
                    entries.Add(new SearchEntry { Kind = SearchEntry.Page, Key = pages[i].Spec.Id, Title = pages[i].Spec.Title, Go = () => ShowPage(index) });
                }
                foreach (var setting in _spec.ResolvedSettings)
                {
                    // A note is text to read, not something to set; a field the window does not show cannot be gone to.
                    if (setting.Hidden || setting.Kind == "note" || !parts.Elements.TryGetValue(setting.Key, out var field)) continue;
                    int index = pages.FindIndex(p => Inside(field, p.Page));
                    if (index < 0) continue;
                    // Hidden itself or with a panel it sits in (the cooldown senders while a game message is picked).
                    if (!ShownOn(field, pages[index].Page)) continue;
                    var shown = field;
                    entries.Add(new SearchEntry
                    {
                        Key = setting.Key,
                        Title = string.IsNullOrWhiteSpace(setting.Label) ? Conventions.Humanize(setting.Key) : setting.Label,
                        Where = string.IsNullOrWhiteSpace(setting.Section) ? pages[index].Spec.Title : pages[index].Spec.Title + " › " + setting.Section,
                        Help = setting.Help,
                        Go = () => { ShowPage(index); SearchBar.Reveal(window, layers, shown); }
                    });
                }
                int messagesPage = pages.FindIndex(p => p.Spec.Id == "messages");
                ComboBox picker = messagesPage < 0 ? null : Named<ComboBox>(pages[messagesPage].Page, "MessagePicker");
                if (picker != null)
                    foreach (var message in _spec.Messages)
                    {
                        var item = picker.Items.OfType<ComboBoxItem>().FirstOrDefault(c => c.Tag as string == message.Key);
                        if (item == null) continue;
                        entries.Add(new SearchEntry
                        {
                            Kind = SearchEntry.Message,
                            Key = message.Key,
                            Title = message.Label,
                            Where = string.IsNullOrWhiteSpace(message.Group) ? pages[messagesPage].Spec.Title : pages[messagesPage].Spec.Title + " › " + message.Group,
                            Help = message.Help,
                            Go = () => { ShowPage(messagesPage); picker.SelectedItem = item; SearchBar.Reveal(window, layers, picker); }
                        });
                    }
                return entries;
            }
            search = new SearchBar(window, layers, btnSearch, SearchEntries);
            _search = search;
            if (!_spec.Locked) tourParts["customise"] = btnCustomize;
            List<Tour.Stop> TourStops()
            {
                var lang = _spec.Conventions?.Lang;
                string Declared(string text) => text == null || lang == null ? text : lang.Text(text);
                string Own(string key) { string text = Text.T(key); return text == key ? null : text; }
                var stops = new List<Tour.Stop>();
                foreach (var stop in _spec.Tour)
                {
                    string target = stop.Target;
                    var shown = new Tour.Stop { Title = Declared(stop.Title), Text = Declared(stop.Text), Act = stop.Act && target != null && target != "start" && target != "end" };
                    if (target == null || target == "start" || target == "end")
                    {
                        if (target != null)
                        {
                            shown.Title = shown.Title ?? Own("tour." + target + ".title");
                            shown.Text = shown.Text ?? Own("tour." + target + ".text");
                        }
                    }
                    else if (target.StartsWith("page:", StringComparison.Ordinal))
                    {
                        string id = target.Substring(5);
                        int index = pages.FindIndex(p => p.Spec.Id == id);
                        if (index < 0) continue;   // hidden by the user, or not there
                        var item = rail.Items[index];
                        shown.Target = () => item;
                        shown.Area = () => pageHost;   // the page itself stays bright beside its ringed rail entry
                        shown.Show = () => ShowPage(index);
                        shown.Title = shown.Title ?? pages[index].Spec.Title;
                        shown.Text = shown.Text ?? Own("tour.page." + id + ".text") ?? Own("tour.page.text");
                    }
                    else if (tourParts.TryGetValue(target, out var part))
                    {
                        if (part.Visibility != Visibility.Visible || !(part.Parent is FrameworkElement)) continue;
                        shown.Target = () => part;
                        shown.Title = shown.Title ?? Own("tour." + target + ".title");
                        shown.Text = shown.Text ?? Own("tour." + target + ".text");
                    }
                    else if (parts.Elements.TryGetValue(target, out var field))
                    {
                        int index = pages.FindIndex(p => Inside(field, p.Page));
                        if (index < 0) continue;
                        var setting = _spec.ResolvedSettings.FirstOrDefault(s => s.Key == target);
                        shown.Target = () => field;
                        shown.Show = () => ShowPage(index);
                        shown.Title = shown.Title ?? setting?.Label ?? Conventions.Humanize(target);
                        shown.Text = shown.Text ?? setting?.Help;
                    }
                    else
                        continue;
                    if (string.IsNullOrEmpty(shown.Title) && string.IsNullOrEmpty(shown.Text)) continue;
                    stops.Add(shown);
                }
                return stops;
            }
            startTour = () =>
            {
                tour?.End();
                var stops = TourStops();
                int back = current;
                // When it ends, the page that was in front comes back.
                tour = Tour.Start(window, layers, stops, () => { tour = null; if (back < pages.Count) ShowPage(back); });
            };

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

            if (Capture != null && !captureStarted)
            {
                captureStarted = true;
                var request = Capture;
                window.WindowStartupLocation = WindowStartupLocation.Manual;
                window.Left = -20000;
                window.Top = -20000;
                window.ShowInTaskbar = false;
                window.ContentRendered += (s, e) => window.Dispatcher.BeginInvoke(new Action(() =>
                {
                    Directory.CreateDirectory(request.Folder);
                    // Text drawn on a transparent bitmap loses its ClearType and comes out faint: the shot gets the
                    // window's background under everything.
                    if (root.Background == null) root.Background = window.Background;
                    if (request.Problems)
                    {
                        // The window with the red "i" (problems-closed.png), then with its panel open (problems.png).
                        window.UpdateLayout();
                        WindowCapture.Settle(Math.Max(300, request.SettleMs / 3));
                        string closed = Path.Combine(request.Folder, "problems-closed.png");
                        WindowCapture.Save(layers, closed, layers.ActualWidth, layers.ActualHeight);
                        request.Written.Add(closed);
                        openProblems?.Invoke();
                        window.UpdateLayout();
                        WindowCapture.Settle(Math.Max(300, request.SettleMs / 3));
                        string opened = Path.Combine(request.Folder, "problems.png");
                        WindowCapture.Save(layers, opened, layers.ActualWidth, layers.ActualHeight);
                        request.Written.Add(opened);
                        closeConfirmed = true;
                        window.Close();
                        return;
                    }
                    if (request.Search != null)
                    {
                        // The search bar with what was asked for typed in (search.png), then where its first result
                        // leads, ringed (search-found.png).
                        window.UpdateLayout();
                        search.Open();
                        search.Box.Text = request.Search;
                        window.UpdateLayout();
                        WindowCapture.Settle(Math.Max(300, request.SettleMs / 3));
                        string listed = Path.Combine(request.Folder, "search.png");
                        WindowCapture.Save(layers, listed, layers.ActualWidth, layers.ActualHeight);
                        request.Written.Add(listed);
                        search.GoToSelected();
                        window.UpdateLayout();
                        WindowCapture.Settle(600);
                        string found = Path.Combine(request.Folder, "search-found.png");
                        WindowCapture.Save(layers, found, layers.ActualWidth, layers.ActualHeight);
                        request.Written.Add(found);
                        closeConfirmed = true;
                        window.Close();
                        return;
                    }
                    if (request.Tour)
                    {
                        // The tour, stop by stop, as the user sees it: tour-01.png, tour-02.png, ...
                        startTour?.Invoke();
                        for (int stop = 0; tour != null && stop < tour.Count; stop++)
                        {
                            tour.Go(stop);
                            window.UpdateLayout();
                            WindowCapture.Settle(Math.Max(300, request.SettleMs / 3));
                            string file = Path.Combine(request.Folder, "tour-" + (stop + 1).ToString("00", System.Globalization.CultureInfo.InvariantCulture) + ".png");
                            WindowCapture.Save(layers, file, layers.ActualWidth, layers.ActualHeight);
                            request.Written.Add(file);
                        }
                        tour?.End();
                        closeConfirmed = true;
                        window.Close();
                        return;
                    }
                    for (int i = 0; i < pages.Count; i++)
                    {
                        string id = pages[i].Spec.Id;
                        if (request.Only != null && !request.Only.Contains(id)) continue;
                        ShowPage(i);
                        window.UpdateLayout();
                        WindowCapture.Settle(request.SettleMs);
                        if (request.Pick > 0)
                            foreach (var grid in WindowCapture.Descendants(window).OfType<DataGrid>())
                                if (grid.Items.Count >= request.Pick) grid.SelectedIndex = request.Pick - 1;
                        // The page at its full height: the content is laid out taller than the window by what the page
                        // would scroll (a window cannot grow past the screen), photographed, and put back.
                        var scroll = (pages[i].Page as ScrollViewer) ?? WindowCapture.Descendants(pages[i].Page).OfType<ScrollViewer>().FirstOrDefault(v => v.ScrollableHeight > 0);
                        double width = root.ActualWidth, height = root.ActualHeight;
                        if (scroll != null && scroll.ScrollableHeight > 0)
                        {
                            height = Math.Min(request.MaxHeight, height + scroll.ScrollableHeight + 2);
                            root.Measure(new Size(width, height));
                            root.Arrange(new Rect(0, 0, width, height));
                            root.UpdateLayout();
                            WindowCapture.Settle(300);
                            root.Measure(new Size(width, height));
                            root.Arrange(new Rect(0, 0, width, height));
                        }
                        string file = Path.Combine(request.Folder, (request.Names != null && request.Names.TryGetValue(id, out var name) ? name : id) + ".png");
                        WindowCapture.Save(root, file, width, height);
                        root.InvalidateMeasure();
                        window.UpdateLayout();
                        request.Written.Add(file);
                    }
                    closeConfirmed = true;
                    window.Close();
                }), DispatcherPriority.ApplicationIdle);
            }
            }

            Build();

            // The first open of a window with a tour shows it, once: the layout file remembers that it was shown.
            bool tourOffered = false;
            window.ContentRendered += (s, e) =>
            {
                if (tourOffered || Capture != null) return;
                tourOffered = true;
                if (_spec.Tour.Count == 0 || _spec.Customizing || layout.TourSeen) return;
                layout.TourSeen = true;
                try { layout.Save(); }
                catch (Exception ex) { _log.Warn(_spec.LogPrefix + " " + LayoutFile.FileName + " not written (the tour will show again): " + ex.Message); }
                window.Dispatcher.BeginInvoke(new Action(() => startTour?.Invoke()), DispatcherPriority.Loaded);
            };

            window.Closing += (s, e) =>
            {
                if (closeConfirmed || _shuttingDown)
                    return;
                int n = changes();
                if (n == 0)
                    return;
                var confirm = MessageBox.Show(
                    n == 1 ? Text.T("dialog.discard.one") : Text.T("dialog.discard.many", n),
                    Text.T("dialog.unsaved.title"), MessageBoxButton.YesNo, MessageBoxImage.Warning);
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

        private bool ApplyAndSave(JObject settings, UIElement root, WindowParts parts, Dictionary<object, string> baseline = null, ISet<string> repairs = null)
        {
            try
            {
                var invalid = FieldBinding.InvalidNumberFields(root);
                if (invalid.Count > 0)
                {
                    MessageBox.Show(Text.T("dialog.invalidNumbers.text", string.Join("\n", invalid)), Text.T("dialog.invalidNumbers.title"), MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                parts.CommitNames();
                // A draft: the window's copy takes the values only once they are written, so a save that is declined (a
                // warning, a game that did not check) leaves nothing behind that a rebuild would show as saved.
                var draft = (JObject)settings.DeepClone();
                FieldBinding.Save(draft, root);
                // After that save: a number it pulled into its min/max shows the limit now and counts as changed; so do
                // the numbers the window showed repaired since it opened, and what the window's own clamp changed.
                var changed = FieldBinding.ChangedKeys(root, baseline);
                if (repairs != null) changed.UnionWith(repairs);
                if (_spec.Clamp != null)
                {
                    var unclamped = (JObject)draft.DeepClone();
                    _spec.Clamp(draft);
                    foreach (var property in draft.Properties())
                        if (!JToken.DeepEquals(property.Value, unclamped[property.Name])) changed.Add(property.Name);
                }

                var warnings = _spec.Warnings?.Invoke(draft) ?? new List<string>();
                string fromHost = null;
                SafeHook(() => fromHost = _hooks.Validate(draft));
                // A stub from 0.2.0 on tells "the game did not answer" from "no problems"; an older one says null for both.
                const string noAnswer = "\u0001uiframework:no-answer";
                if (fromHost != null && fromHost.StartsWith(noAnswer, StringComparison.Ordinal))
                {
                    string why = fromHost.Substring(noAnswer.Length).TrimStart(':', ' ');
                    var unchecked_ = MessageBox.Show(
                        Text.Or("dialog.unchecked.text",
                            "The game did not check these settings ({0}).\n\nSave them unchecked anyway?",
                            "Das Spiel hat diese Einstellungen nicht geprüft ({0}).\n\nTrotzdem ungeprüft speichern?", why.Length > 0 ? why : "-"),
                        Text.Or("dialog.unchecked.title", "Not checked", "Nicht geprüft"), MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (unchecked_ != MessageBoxResult.Yes)
                        return false;
                    fromHost = null;
                }
                if (!string.IsNullOrWhiteSpace(fromHost))
                    warnings.AddRange(fromHost.Replace("\r", "").Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0));
                if (warnings.Count > 0)
                {
                    var proceed = MessageBox.Show(Text.T("dialog.warnings.text", string.Join("\n  • ", warnings)), Text.T("dialog.warnings.title"), MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (proceed != MessageBoxResult.Yes)
                        return false;
                }

                // What is written is the file as it is now with the window's changes laid over it, not the copy the window
                // read when it opened: a key the game wrote meanwhile (a viewer added from chat, a suggested question, a
                // status) survives a save that did not touch it, and a list file is written only when its list changed.
                // A file that cannot be opened stops the save (the dialog names why); a damaged one is written from the
                // window as before.
                JObject target = draft;
                ISet<string> only = null;
                if (baseline != null)
                {
                    var now = _store.Load();
                    if (now.Damaged == null)
                    {
                        target = now.Settings;
                        // A list file that cannot be read now keeps the window's copy of its lists, not the defaults.
                        var unreadable = new HashSet<string>(StringComparer.Ordinal);
                        Sidecars.Load(target, _spec.ResolvedSettings, _spec.SettingsPath, null, _spec.LogPrefix, unreadable);
                        foreach (var key in changed.Concat(unreadable))
                            if (draft[key] != null) target[key] = draft[key].DeepClone();
                    }
                    only = changed;
                }
                _store.Save(Sidecars.Split(target, _spec.ResolvedSettings, _spec.SettingsPath, only, _unreadableLists));
                // The window's copy is now what was written, so the next save and a rebuild start from there.
                foreach (var property in target.Properties().ToList())
                    settings[property.Name] = property.Value.DeepClone();
                SafeHook(() => _hooks.OnSaved(settings));
                _log.Info(_spec.LogPrefix + " Settings saved.");
                return true;
            }
            catch (Exception ex)
            {
                Failed("Save error: " + ex.Message);
                MessageBox.Show(Text.T("dialog.saveFailed.text", ex.Message), Text.T("dialog.saveFailed.title"), MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        /// <summary>Whether <paramref name="element"/> sits somewhere inside <paramref name="page"/>, laid out or not.</summary>
        /// <summary>The search bar of the window as it is built now, for the tests and the screenshots.</summary>
        internal SearchBar Search => _search;
        private SearchBar _search;

        // The first element of that type and name under a page, by its logical tree (a page not yet shown has no visual one).
        private static T Named<T>(DependencyObject root, string name) where T : FrameworkElement
        {
            if (root is T match && match.Name == name) return match;
            foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            {
                var found = Named<T>(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private static bool Inside(DependencyObject element, DependencyObject page)
        {
            for (var node = element; node != null; node = LogicalTreeHelper.GetParent(node) ?? (node is Visual || node is System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(node) : null))
                if (ReferenceEquals(node, page)) return true;
            return false;
        }

        /// <summary>Whether an element shows once its page is in front: neither it nor anything between it and the page
        /// is hidden. The page itself is not asked: only the one in front is visible.</summary>
        public static bool ShownOn(DependencyObject element, DependencyObject page)
        {
            for (var node = element; node != null && !ReferenceEquals(node, page); node = LogicalTreeHelper.GetParent(node) ?? (node is Visual || node is System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(node) : null))
                if (node is UIElement shown && shown.Visibility != Visibility.Visible) return false;
            return true;
        }

        private void SafeHook(Action hook)
        {
            try { hook(); }
            catch (Exception ex) { _log.Warn(_spec.LogPrefix + " Host hook failed: " + ex.Message); _problems.Add("The game's handler failed: " + ex.Message); }
        }

        private sealed class NullLog : IHostLog
        {
            public void Info(string message) { }
            public void Warn(string message) { }
            public void Error(string message) { }
        }
    }

    /// <summary>The rainbow mode: headings, rail borders and the header line cycle through the hues.</summary>
    /// <summary>What <see cref="Shell.Capture"/> asks for: the folder, which pages (null: all) and under which file
    /// names, how long each page gets for pictures to arrive, which grid row to pick, the tallest a shot may be.</summary>
    public sealed class WindowCapture
    {
        public string Folder;
        public HashSet<string> Only;
        public Dictionary<string, string> Names;
        public int SettleMs = 1500;
        public int Pick;
        public double MaxHeight = 2400;
        /// <summary>Photograph the window's tour, one shot per stop, instead of the pages.</summary>
        public bool Tour;
        /// <summary>Photograph the window with its red "i", then with the problems panel open, instead of the pages.</summary>
        public bool Problems;
        /// <summary>Photograph the search instead of the pages: the bar with this typed into it (search.png), then the
        /// window after going to the first result (search-found.png).</summary>
        public string Search;
        public readonly List<string> Written = new List<string>();

        public static void Save(FrameworkElement element, string file) => Save(element, file, element.ActualWidth, element.ActualHeight);

        public static void Save(FrameworkElement element, string file, double width, double height)
        {
            var source = PresentationSource.FromVisual(element);
            double dpiX = 96, dpiY = 96;
            if (source?.CompositionTarget != null)
            {
                dpiX = 96 * source.CompositionTarget.TransformToDevice.M11;
                dpiY = 96 * source.CompositionTarget.TransformToDevice.M22;
            }
            var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(
                (int)Math.Ceiling(width * dpiX / 96), (int)Math.Ceiling(height * dpiY / 96), dpiX, dpiY, PixelFormats.Pbgra32);
            bitmap.Render(element);
            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
            using (var stream = File.Create(file))
                encoder.Save(stream);
        }

        /// <summary>Runs the dispatcher for a while, so what arrives on other threads (emote pictures) is drawn.</summary>
        public static void Settle(int ms)
        {
            if (ms <= 0) return;
            var frame = new DispatcherFrame();
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
            timer.Tick += (s, e) => { timer.Stop(); frame.Continue = false; };
            timer.Start();
            Dispatcher.PushFrame(frame);
        }

        public static IEnumerable<DependencyObject> Descendants(DependencyObject root)
        {
            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                yield return child;
                foreach (var d in Descendants(child)) yield return d;
            }
        }
    }

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
