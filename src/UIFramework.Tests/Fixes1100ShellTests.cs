using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Threading;
using Newtonsoft.Json.Linq;
using Xunit;

namespace UIFramework.Tests
{
    /// <summary>2.0.0, the window: a save keeps as unsaved what changed while the game checked it, the questions after
    /// the check belong to the window, Reset all keeps the unsaved rows of a list in a file of its own, and the line for
    /// a list file that could not be read is in the window's language.</summary>
    public class Fixes1100ShellTests
    {
        private const string Head = @"{ ""host"": ""demo"", ""version"": ""v1"", ""settingsFile"": ""x"", ""settings"": [
            { ""key"": ""bet"", ""default"": 10, ""page"": ""game"", ""label"": ""Bet"" },
            { ""key"": ""preset"", ""default"": """", ""kind"": ""action"", ""page"": ""game"", ""label"": ""Preset"" },
            { ""key"": ""words"", ""default"": [ { ""word"": ""a"" } ], ""kind"": ""list"", ""file"": ""words.json"", ""page"": ""game"", ""label"": ""Words"", ""columns"": [ ""word"" ] } ],
            ""window"": { ""title"": ""Demo"", ";
        private const string Pages = @"""pages"": [ ""game"", { ""id"": ""probe"", ""title"": ""Probe"", ""custom"": true } ] } }";
        private const string Schema = Head + Pages;

        // ---- A save while the game checks -------------------------------------------------------------------

        [Fact]
        public void WhatChangesWhileTheGameChecksStaysUnsaved()
        {
            using (var w = new Opened(Schema))
            {
                var asked = new ManualResetEventSlim();
                var answer = new ManualResetEventSlim();
                w.OnValidate = settings => { asked.Set(); answer.Wait(10000); return null; };
                w.On(() => w.Box("bet").Text = "20");
                var saved = w.Later(() => w.Click(w.On(() => Text.T("btn.save"))));
                Assert.True(asked.Wait(5000));
                Thread.Sleep(Patient.QuickMs + 300);           // past the quick wait: the window waits, its dispatcher running
                w.On(() => w.Box("bet").Text = "30");          // a timer, a dropdown's popup: what reaches the window meanwhile
                answer.Set();
                Assert.True(saved.Wait(10000));

                Assert.Equal(20, (int)w.Saved()["bet"]);      // what was there when Save was pressed
                Assert.Equal("30", w.On(() => w.Box("bet").Text));
                Assert.True(w.On(() => w.Shows(Text.T("status.unsaved.one"))), "the status does not count the change that came in");
                // It is unsaved, so the next save writes it (it found nothing changed and wrote nothing).
                w.OnValidate = settings => null;
                w.On(() => w.Click(Text.T("btn.save")));
                Assert.Equal(30, (int)w.Saved()["bet"]);
                Assert.True(w.On(() => w.Shows(Text.T("status.saved"))));
            }
        }

        [Fact]
        public void ASetAnswerThatArrivesWhileTheGameChecksIsNotTakenAsSaved()
        {
            using (var w = new Opened(Schema))
            {
                var answered = new ManualResetEventSlim();
                // A preset button whose answer takes longer than the quick wait, and a check that takes longer still.
                w.OnTest = (name, settings) => { Thread.Sleep(Patient.QuickMs + 300); answered.Set(); return @"{ ""set"": { ""bet"": 25 } }"; };
                w.OnValidate = settings => { answered.Wait(5000); Thread.Sleep(500); return null; };
                w.On(() => w.Click("Preset"));                 // back after the quick wait, the answer still out
                var saved = w.Later(() => w.Click(w.On(() => Text.T("btn.save"))));
                Assert.True(saved.Wait(10000));

                Assert.Equal("25", w.On(() => w.Box("bet").Text));   // the answer arrived during the check
                Assert.Equal(10, (int)w.Saved()["bet"]);              // and was not in what the save wrote
                Assert.True(w.On(() => w.Shows(Text.T("status.unsaved.one"))), "the preset's number counts as saved");
                w.OnValidate = settings => null;
                w.On(() => w.Click(Text.T("btn.save")));
                Assert.Equal(25, (int)w.Saved()["bet"]);
            }
        }

        [Fact]
        public void SaveAndExitAsksAboutWhatCameInWhileTheGameChecked()
        {
            using (var w = new Opened(Schema))
            {
                var asked = new ManualResetEventSlim();
                var answer = new ManualResetEventSlim();
                w.OnValidate = settings => { asked.Set(); answer.Wait(10000); return null; };
                w.On(() => w.Box("bet").Text = "20");
                var done = w.Later(() => w.Click(w.On(() => Text.T("btn.saveExit"))));
                Assert.True(asked.Wait(5000));
                Thread.Sleep(Patient.QuickMs + 300);
                w.On(() => w.Box("bet").Text = "30");
                answer.Set();

                // The window does not close in silence: it asks, as for any change not saved.
                var question = w.Dialog(4000);
                Assert.NotEqual(IntPtr.Zero, question);
                Assert.Equal(w.On(() => Text.T("dialog.discard.one")), Opened.TextOf(question));
                Opened.Answer(question, Opened.IdNo);
                Assert.True(done.Wait(10000));
                Assert.True(w.On(() => w.Window.IsVisible));
                Assert.Equal(20, (int)w.Saved()["bet"]);
                Assert.Equal("30", w.On(() => w.Box("bet").Text));
            }
        }

        // ---- The questions after the check ------------------------------------------------------------------

        [Fact]
        public void TheQuestionAfterTheCheckBelongsToTheWindow()
        {
            using (var w = new Opened(Schema))
            {
                var asked = new ManualResetEventSlim();
                var answer = new ManualResetEventSlim();
                w.OnValidate = settings => { asked.Set(); answer.Wait(10000); return "The bet is odd."; };
                var done = w.Later(() => w.Click(w.On(() => Text.T("btn.save"))));
                Assert.True(asked.Wait(5000));
                Thread.Sleep(Patient.QuickMs + 300);
                // The user went to another window while the game took its time: this one is not the active one any more.
                IntPtr window = w.On(() => new WindowInteropHelper(w.Window).Handle);
                w.On(() => w.Elsewhere());
                Assert.NotEqual(window, w.On(() => Opened.ActiveWindow()));
                answer.Set();

                var question = w.Dialog(5000);
                Assert.NotEqual(IntPtr.Zero, question);
                Assert.Contains("The bet is odd.", Opened.TextOf(question));
                // Its owner is the settings window, which takes no click and no close while it is up.
                Assert.Equal(window, Opened.Owner(question));
                Assert.False(Opened.Enabled(window));
                Opened.Answer(question, Opened.IdNo);
                Assert.True(done.Wait(10000));
                Assert.False(File.Exists(w.SettingsPath));     // declined: nothing written
                Assert.True(Opened.Enabled(window));
            }
        }

        // ---- Reset all and a list in a file of its own --------------------------------------------------------

        [Fact]
        public void ResetAllKeepsTheUnsavedRowsOfAListInAFileOfItsOwn()
        {
            using (var w = new Opened(Schema, folder => File.WriteAllText(Path.Combine(folder, "words.json"), @"[ { ""word"": ""from the file"" } ]")))
            {
                Assert.Contains("from the file", w.On(() => w.Context.Parts.Lists["words"].Serialized));
                w.On(() =>
                {
                    w.Context.Parts.Lists["words"].Set(new JArray(new JObject { ["word"] = "typed" }));
                    w.Box("bet").Text = "30";
                });
                var done = w.Later(() => w.Click(w.On(() => Text.T("tip.resetAll")), byTip: true));
                var question = w.Dialog(5000);
                Assert.NotEqual(IntPtr.Zero, question);
                Opened.Answer(question, Opened.IdYes);
                Assert.True(done.Wait(10000));

                // The settings file is reset and written; the list's file is neither, and its editor keeps what it showed.
                Assert.Equal(10, (int)w.Saved()["bet"]);
                Assert.Equal("10", w.On(() => w.Box("bet").Text));
                Assert.Contains("from the file", File.ReadAllText(Path.Combine(w.Folder, "words.json")));
                Assert.Equal(@"[{""word"":""typed""}]", w.On(() => w.Context.Parts.Lists["words"].Serialized));
                // Still unsaved: the status counts it, and a save writes it.
                Assert.True(w.On(() => w.Shows(Text.T("status.unsaved.one"))), "the list's rows do not count as unsaved");
                w.On(() => w.Click(Text.T("btn.save")));
                Assert.Contains("typed", File.ReadAllText(Path.Combine(w.Folder, "words.json")));
            }
        }

        [Fact]
        public void ResetAllWithoutUnsavedRowsShowsTheListAsItsFileHasIt()
        {
            using (var w = new Opened(Schema, folder => File.WriteAllText(Path.Combine(folder, "words.json"), @"[ { ""word"": ""from the file"" } ]")))
            {
                w.On(() => w.Box("bet").Text = "30");
                var done = w.Later(() => w.Click(w.On(() => Text.T("tip.resetAll")), byTip: true));
                var question = w.Dialog(5000);
                Assert.NotEqual(IntPtr.Zero, question);
                Opened.Answer(question, Opened.IdYes);
                Assert.True(done.Wait(10000));
                Assert.Equal(@"[{""word"":""from the file""}]", w.On(() => w.Context.Parts.Lists["words"].Serialized));
                Assert.True(w.On(() => w.Shows(Text.T("status.clean"))));
            }
        }

        // ---- A list file that could not be read --------------------------------------------------------------

        [Fact]
        public void TheLineForAnUnreadableListFileIsInTheWindowsLanguage()
        {
            using (var w = new Opened(Head + @"""language"": ""de"", " + Pages, folder => File.WriteAllText(Path.Combine(folder, "words.json"), "{ not json")))
            {
                string reason = w.On(() => Text.T("list.unreadable"));
                Assert.StartsWith("Die Datei dieser Liste", reason);
                w.On(() => w.Click(Text.T("problems.title"), byName: true));
                var lines = w.On(() => w.Texts().Where(t => t.Contains("words.json")).ToList());
                var line = Assert.Single(lines);
                Assert.Equal("'Words' (words.json): " + reason, line);
            }
        }

        // ---- The window, opened as a game opens it ---------------------------------------------------------

        /// <summary>Shell.Run on a thread of its own, which starts the window's thread, as the settings action does. A
        /// custom page hands the test each build's context, and with it the window's thread and controls.</summary>
        private sealed class Opened : NoHooks, IDisposable
        {
            public readonly string Folder;
            public readonly string SettingsPath;
            public Func<JObject, string> OnValidate = settings => null;
            public Func<string, JObject, string> OnTest = (name, settings) => null;

            private readonly Thread _runner;
            private readonly ManualResetEventSlim _built = new ManualResetEventSlim();
            private volatile PageContext _context;
            private FrameworkElement _probe;
            private uint _thread;
            private Window _elsewhere;

            public Opened(string schemaJson, Action<string> files = null)
            {
                Folder = Path.Combine(Path.GetTempPath(), "UIFramework.Tests", "shell-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(Folder);
                SettingsPath = Path.Combine(Folder, "settings.json");
                files?.Invoke(Folder);
                var schema = UIFramework.Schema.Parse(schemaJson);
                var spec = Entry.SpecFor(null, schema, SettingsPath);
                _runner = new Thread(() => { try { Entry.Show(spec, schema, new QuietLog(), this); } catch (Exception) { } }) { IsBackground = true };
                _runner.Start();
                if (!_built.Wait(15000))
                    throw new TimeoutException("The window was not built.");
                // Up and idle: shown, laid out, the first status written.
                Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            }

            public PageContext Context => _context;
            public Dispatcher Dispatcher => _probe.Dispatcher;
            public Window Window => (Window)Up(_probe, n => n is Window);

            public override FrameworkElement BuildPage(string pageId, PageContext context)
            {
                _context = context;
                _probe = new TextBlock { Text = "probe" };
                _thread = GetCurrentThreadId();
                _built.Set();
                return _probe;
            }

            public override string Validate(JObject settings) => OnValidate(settings);
            public override string Test(string name, JObject settings) => OnTest(name, settings);

            public T On<T>(Func<T> body) => Dispatcher.Invoke(body);
            public void On(Action body) => Dispatcher.Invoke(body);

            /// <summary>Runs on the window's thread without waiting for it; the event is set once it has run.</summary>
            public ManualResetEventSlim Later(Action body)
            {
                var done = new ManualResetEventSlim();
                Dispatcher.BeginInvoke(new Action(() => { try { body(); } finally { done.Set(); } }));
                return done;
            }

            public JObject Saved() => JObject.Parse(File.ReadAllText(SettingsPath));

            public TextBox Box(string key) => _context.Parts.Boxes[key];

            /// <summary>A button by its text, its tooltip or its accessible name, clicked as the mouse would.</summary>
            public void Click(string text, bool byTip = false, bool byName = false)
            {
                var button = All(Window).OfType<Button>().First(b => byTip ? b.ToolTip as string == text
                    : byName ? System.Windows.Automation.AutomationProperties.GetName(b) == text : b.Content as string == text);
                button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button));
            }

            public IEnumerable<string> Texts() => All(Window).OfType<TextBlock>().Select(t => t.Text ?? "");
            public bool Shows(string text) => Texts().Contains(text);

            /// <summary>Another window of this thread becomes the active one, as when the user clicks elsewhere.</summary>
            public void Elsewhere()
            {
                _elsewhere = new Window { Width = 120, Height = 80, ShowInTaskbar = false, Left = -20000, Top = -20000, WindowStartupLocation = WindowStartupLocation.Manual };
                _elsewhere.Show();
                SetActiveWindow(new WindowInteropHelper(_elsewhere).Handle);
            }

            private static object Up(DependencyObject node, Func<DependencyObject, bool> wanted)
            {
                while (node != null && !wanted(node))
                    node = LogicalTreeHelper.GetParent(node) ?? (node is System.Windows.Media.Visual ? System.Windows.Media.VisualTreeHelper.GetParent(node) : null);
                return node;
            }

            private static IEnumerable<object> All(object node)
            {
                yield return node;
                if (node is DependencyObject d)
                    foreach (var child in LogicalTreeHelper.GetChildren(d))
                        foreach (var below in All(child))
                            yield return below;
            }

            /// <summary>A message box of the window's thread, waited for up to <paramref name="ms"/>; zero when none came.</summary>
            public IntPtr Dialog(int ms)
            {
                var watch = Stopwatch.StartNew();
                do
                {
                    IntPtr found = IntPtr.Zero;
                    EnumThreadWindows(_thread, (hwnd, l) =>
                    {
                        var name = new StringBuilder(64);
                        GetClassName(hwnd, name, name.Capacity);
                        if (name.ToString() != "#32770") return true;
                        found = hwnd;
                        return false;
                    }, IntPtr.Zero);
                    if (found != IntPtr.Zero) return found;
                    Thread.Sleep(20);
                }
                while (watch.ElapsedMilliseconds < ms);
                return IntPtr.Zero;
            }

            public const int IdYes = 6, IdNo = 7;

            public static void Answer(IntPtr dialog, int button) => PostMessage(dialog, 0x0111 /* WM_COMMAND */, (IntPtr)button, IntPtr.Zero);

            public static string TextOf(IntPtr dialog)
            {
                var text = new StringBuilder(2048);
                GetDlgItemText(dialog, 0xFFFF, text, text.Capacity);   // a message box's text is its static with id -1
                return text.ToString();
            }

            public static IntPtr Owner(IntPtr dialog) => GetWindow(dialog, 4 /* GW_OWNER */);
            public static bool Enabled(IntPtr window) => IsWindowEnabled(window);
            public static IntPtr ActiveWindow() => GetActiveWindow();

            public void Dispose()
            {
                try
                {
                    if (_elsewhere != null) On(() => _elsewhere.Close());
                    Shell.Close(SettingsPath);
                    var watch = Stopwatch.StartNew();
                    while (_runner.IsAlive && watch.ElapsedMilliseconds < 15000)
                    {
                        // A question a failed test left open is answered, so the window can go.
                        var dialog = Dialog(0);
                        if (dialog != IntPtr.Zero)
                        {
                            PostMessage(dialog, 0x0010 /* WM_CLOSE */, IntPtr.Zero, IntPtr.Zero);
                            Answer(dialog, IdNo);
                        }
                        Thread.Sleep(50);
                    }
                }
                finally
                {
                    Text.Use(Conventions.Default.Lang);
                    try { Directory.Delete(Folder, true); } catch (Exception) { }
                }
            }

            private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);
            [DllImport("user32.dll")] private static extern bool EnumThreadWindows(uint threadId, EnumWindowsProc callback, IntPtr lParam);
            [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hwnd, StringBuilder name, int max);
            [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern uint GetDlgItemText(IntPtr dialog, int id, StringBuilder text, int max);
            [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr hwnd, uint command);
            [DllImport("user32.dll")] private static extern bool IsWindowEnabled(IntPtr hwnd);
            [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
            [DllImport("user32.dll")] private static extern IntPtr GetActiveWindow();
            [DllImport("user32.dll")] private static extern IntPtr SetActiveWindow(IntPtr hwnd);
            [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
        }

        private sealed class QuietLog : IHostLog
        {
            public void Info(string message) { }
            public void Warn(string message) { }
            public void Error(string message) { }
        }
    }
}
