using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using Newtonsoft.Json.Linq;
using Xunit;

namespace UIFramework.Tests
{
    /// <summary>2.0.0, what was left over: a settings file gone beside its .bak is read from the .bak by the window
    /// too, as the project's UiSettings.Load reads it.</summary>
    public class Fixes1100LeftoverSettingsBakTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "UIFramework.Tests", "leftover-" + Guid.NewGuid().ToString("N"));
        private string File_ => Path.Combine(_folder, "settings.json");
        private static JObject Defaults => JObject.Parse(@"{ ""a"": 1, ""b"": ""two"" }");

        public Fixes1100LeftoverSettingsBakTests() { Directory.CreateDirectory(_folder); }
        public void Dispose() { try { Directory.Delete(_folder, true); } catch (Exception) { } }

        // ---- The store ----------------------------------------------------------------------------------------

        [Fact]
        public void AFileGoneBesideItsBackupIsReadFromTheBackup()
        {
            var store = new SettingsStore(File_, Defaults);
            File.WriteAllText(store.BackupPath, @"{ ""a"": 8, ""extra"": ""kept"" }");

            // What the project runs on, not the defaults; nothing is damaged, so no .broken copy and no file written.
            var load = store.Load();
            Assert.Null(load.Damaged);
            Assert.True(load.FromBackup);
            Assert.Equal(8, (int)load.Settings["a"]);
            Assert.Equal("two", (string)load.Settings["b"]);
            Assert.Equal("kept", (string)load.Settings["extra"]);
            Assert.False(File.Exists(store.BrokenPath));
            Assert.False(File.Exists(File_));

            // The preview's load does the same and says so in the log.
            var log = new ListLog();
            Assert.Equal(8, (int)store.LoadOrDefaults(log, "[demo]")["a"]);
            Assert.Single(log.Warnings, w => w.StartsWith("[demo] settings.json is missing"));

            // A save over that load writes the file from the backup's values, and from then on the file is read.
            load.Settings["b"] = "three";
            store.Save(load.Settings);
            var written = JObject.Parse(File.ReadAllText(File_));
            Assert.Equal(8, (int)written["a"]);
            Assert.Equal("kept", (string)written["extra"]);
            Assert.Equal(8, (int)JObject.Parse(File.ReadAllText(store.BackupPath))["a"]);
            Assert.False(store.Load().FromBackup);
        }

        [Fact]
        public void AFileGoneBesideABackupThatCannotBeReadGivesTheDefaults()
        {
            var store = new SettingsStore(File_, Defaults);
            File.WriteAllText(store.BackupPath, "{ not json");
            var load = store.Load();
            Assert.Null(load.Damaged);
            Assert.False(load.FromBackup);
            Assert.Equal(1, (int)load.Settings["a"]);
            Assert.False(File.Exists(store.BrokenPath));
        }

        // ---- The window ---------------------------------------------------------------------------------------

        private const string Schema = @"{ ""host"": ""demo"", ""version"": ""v1"", ""settingsFile"": ""x"", ""settings"": [
            { ""key"": ""bet"", ""default"": 10, ""page"": ""game"", ""label"": ""Bet"" } ],
            ""window"": { ""title"": ""Demo"", ""language"": ""de"", ""pages"": [ ""game"", { ""id"": ""probe"", ""title"": ""Probe"", ""custom"": true } ] } }";

        [Fact]
        public void TheWindowShowsTheBackupOfAMissingFileSaysSoAndSavesOverIt()
        {
            File.WriteAllText(File_ + ".bak", @"{ ""bet"": 30, ""keep"": ""mine"" }");
            using (var w = new Opened(Schema, File_))
            {
                // It showed the default, 10, while the project played with 30.
                Assert.Equal("30", w.On(() => w.Context.Parts.Boxes["bet"].Text));
                Assert.Single(w.Log.Warnings, m => m.Contains("settings.json is missing"));

                // The first save writes the file from what the window shows: the backup's key it has no field for stays.
                w.On(() => w.Context.Parts.Boxes["bet"].Text = "40");
                w.On(() => w.Click(Text.T("btn.save")));
                var saved = JObject.Parse(File.ReadAllText(File_));
                Assert.Equal(40, (int)saved["bet"]);
                Assert.Equal("mine", (string)saved["keep"]);

                // Under the red "i", in the window's language.
                string line = w.On(() => Text.T("settings.fromBackup", "settings.json"));
                Assert.StartsWith("settings.json fehlt.", line);
                w.On(() => w.Click(Text.T("problems.title"), byName: true));
                Assert.Single(w.On(() => w.Texts().Where(t => t == line).ToList()));
            }
        }

        /// <summary>Shell.Run on a thread of its own, as the settings action opens it; a custom page hands the test the
        /// window's thread and controls.</summary>
        private sealed class Opened : NoHooks, IDisposable
        {
            public readonly ListLog Log = new ListLog();
            private readonly string _settingsPath;
            private readonly Thread _runner;
            private readonly ManualResetEventSlim _built = new ManualResetEventSlim();
            private volatile PageContext _context;
            private FrameworkElement _probe;

            public Opened(string schemaJson, string settingsPath)
            {
                _settingsPath = settingsPath;
                var schema = UIFramework.Schema.Parse(schemaJson);
                var spec = Entry.SpecFor(schema, settingsPath);
                _runner = new Thread(() => { try { Entry.Show(spec, schema, Log, this); } catch (Exception) { } }) { IsBackground = true };
                _runner.Start();
                if (!_built.Wait(15000))
                    throw new TimeoutException("The window was not built.");
                Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            }

            public PageContext Context => _context;
            private Dispatcher Dispatcher => _probe.Dispatcher;
            private Window Window
            {
                get
                {
                    DependencyObject node = _probe;
                    while (node != null && !(node is Window))
                        node = LogicalTreeHelper.GetParent(node) ?? (node is System.Windows.Media.Visual ? System.Windows.Media.VisualTreeHelper.GetParent(node) : null);
                    return (Window)node;
                }
            }

            public override FrameworkElement BuildPage(string pageId, PageContext context)
            {
                _context = context;
                _probe = new TextBlock { Text = "probe" };
                _built.Set();
                return _probe;
            }

            public T On<T>(Func<T> body) => Dispatcher.Invoke(body);
            public void On(Action body) => Dispatcher.Invoke(body);

            /// <summary>A button by its text or its accessible name, clicked as the mouse would.</summary>
            public void Click(string text, bool byName = false)
            {
                var button = All(Window).OfType<Button>().First(b => byName ? System.Windows.Automation.AutomationProperties.GetName(b) == text : b.Content as string == text);
                button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button));
            }

            public IEnumerable<string> Texts() => All(Window).OfType<TextBlock>().Select(t => t.Text ?? "");

            private static IEnumerable<object> All(object node)
            {
                yield return node;
                if (node is DependencyObject d)
                    foreach (var child in LogicalTreeHelper.GetChildren(d))
                        foreach (var below in All(child))
                            yield return below;
            }

            public void Dispose()
            {
                try
                {
                    Shell.Close(_settingsPath);
                    var watch = Stopwatch.StartNew();
                    while (_runner.IsAlive && watch.ElapsedMilliseconds < 15000)
                        Thread.Sleep(50);
                }
                finally
                {
                    Text.Use(Conventions.Default.Lang);
                }
            }
        }

        private sealed class ListLog : IHostLog
        {
            public readonly List<string> Warnings = new List<string>();
            public void Info(string message) { }
            public void Warn(string message) { lock (Warnings) Warnings.Add(message); }
            public void Error(string message) { }
        }
    }
}
