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
using System.Windows.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace UIFramework.Tests
{
    /// <summary>A list the project keeps itself (<c>rowsFrom</c>): the window asks the project for the rows, again when
    /// the field they depend on changes, counts an edit as an unsaved change, and hands the rows back on Save with the
    /// rows it was given. Nothing of the list is in the settings file.</summary>
    public class ProjectRowsTests : IDisposable
    {
        private readonly int _delay = SchemaPage.OptionsFromDelayMs;
        public ProjectRowsTests() { SchemaPage.OptionsFromDelayMs = 1; }
        public void Dispose() { SchemaPage.OptionsFromDelayMs = _delay; }

        private const string Head = @"{ ""host"": ""demo"", ""version"": ""v1"", ""settings"": [ ";

        private static WindowSpec Settings(params string[] settings)
        {
            try
            {
                var schema = Schema.Parse(Head + string.Join(", ", settings) + " ] }");
                var spec = Entry.SpecFor(schema, null);
                Entry.Prepare(spec, schema);
                return spec;
            }
            finally { Text.Use(Conventions.Default.Lang); }
        }

        private const string Notes = @"{ ""key"": ""notes"", ""default"": [ { ""text"": ""never shown"" } ], ""kind"": ""list"", ""rowsFrom"": ""category"", ""page"": ""game"", ""label"": ""Notes"",
            ""columns"": [ ""text|string|Note"", ""author|string|By"" ] }";
        private const string Category = @"{ ""key"": ""category"", ""default"": ""zelda"", ""kind"": ""options"", ""transient"": true, ""page"": ""game"", ""label"": ""Notes of"",
            ""options"": [ ""zelda"", ""mario"" ] }";
        private const string WindowSchema = @"{ ""host"": ""demo"", ""version"": ""v1"", ""settingsFile"": ""x"", ""settings"": [ " + Category + ", " + Notes + @",
            { ""key"": ""bet"", ""default"": 10, ""page"": ""game"", ""label"": ""Bet"" } ],
            ""window"": { ""title"": ""Demo"", ""pages"": [ ""game"", { ""id"": ""probe"", ""title"": ""Probe"", ""custom"": true } ] } }";

        private static string Texts(JArray rows) => string.Join(",", rows.OfType<JObject>().Select(r => (string)r["text"]));

        // ---- the declaration ---------------------------------------------------------------------------------------

        [Fact]
        public void SuchAListHasNoDefaultIsNeverWrittenAndIsLeftByAReset()
        {
            var spec = Settings(Category, Notes);
            Assert.Empty(spec.Problems);
            var notes = spec.ResolvedSettings.Single(s => s.Key == "notes");
            Assert.True(ProjectRows.Applies(notes));
            Assert.Equal("[]", spec.Defaults["notes"].ToString(Formatting.None));   // what the declaration names as default is not shown
            Assert.True(SettingsStore.KeptByReset(notes));

            var main = JObject.Parse(@"{ ""notes"": [ { ""text"": ""a"" } ], ""bet"": 5 }");
            Sidecars.DropTransient(main, spec.ResolvedSettings);
            Assert.Null(main["notes"]);
            Assert.Equal(5, (int)main["bet"]);
        }

        [Fact]
        public void TheDeclarationIsCheckedAndTravelsFromTheHostBlock()
        {
            Assert.Contains(Settings(@"{ ""key"": ""a"", ""default"": """", ""rowsFrom"": """" }").Problems, p => p.Contains("'a'") && p.Contains("rowsFrom") && p.Contains("only a list"));
            const string list = @"""default"": [], ""kind"": ""list"", ""columns"": [ ""text"" ]";
            Assert.Contains(Settings(@"{ ""key"": ""a"", " + list + @", ""rowsFrom"": ""a"" }").Problems, p => p.Contains("names itself"));
            Assert.Contains(Settings(@"{ ""key"": ""a"", " + list + @", ""rowsFrom"": ""nothere"" }").Problems, p => p.Contains("'nothere'") && p.Contains("not a setting"));
            Assert.Contains(Settings(@"{ ""key"": ""a"", " + list + @", ""rowsFrom"": """", ""file"": ""a.json"" }").Problems, p => p.Contains("rowsFrom and a file"));
            Assert.Contains(Settings(@"{ ""key"": ""a"", " + list + @", ""rowsFrom"": """", ""transient"": true }").Problems, p => p.Contains("transient"));
            Assert.Empty(Settings(@"{ ""key"": ""a"", " + list + @", ""rowsFrom"": """" }").Problems);   // rows that depend on no field

            var declared = UiSchema.ToJson(UiSchema.S("notes", new string[0], kind: "list", columns: new[] { "text" }, rowsFrom: "category"));
            Assert.Equal("category", (string)declared["rowsFrom"]);
            Assert.Equal("", (string)UiSchema.ToJson(UiSchema.S("notes", new string[0], kind: "list", columns: new[] { "text" }, rowsFrom: ""))["rowsFrom"]);
            Assert.Null(UiSchema.ToJson(UiSchema.S("rows", new string[0], kind: "list", columns: new[] { "a" }))["rowsFrom"]);
            Assert.Equal("category", Setting.Parse(Setting.Parse(declared).ToJson()).RowsFrom);
        }

        [Fact]
        public void TheAnswerIsItsRowsOrWhyThereAreNone()
        {
            Assert.Equal(@"[{""text"":""a""}]", ProjectRows.Rows(@"{ ""rows"": [ { ""text"": ""a"" } ] }", out string why).ToString(Formatting.None));
            Assert.Null(why);
            Assert.Equal("[]", ProjectRows.Rows(@"{ ""rows"": [] }", out why).ToString(Formatting.None));
            Assert.Null(ProjectRows.Rows(@"{ ""error"": ""The file is locked."" }", out why));
            Assert.Equal("The file is locked.", why);
            Assert.Null(ProjectRows.Rows("", out why));
            Assert.Equal("the project named no rows", why);
            Assert.Null(ProjectRows.Rows("something else", out why));
            Assert.Equal("something else", why);
        }

        // ---- the host block's side ---------------------------------------------------------------------------------

        [Fact]
        public void TheBlockAnswersForTheRowsAndTakesThemBack()
        {
            var cph = new FakeCph();
            string askedKey = null, askedCategory = null, savedLoaded = null, savedNow = null;
            bool Handle(string name, string values, bool withHandlers = true) => UiHost.Handle(
                new Dictionary<string, object> { ["uiframework.request"] = "test", ["uiframework.name"] = name, ["uiframework.values"] = values },
                cph, "Demo", "demo", "v1.0.0", "Demo/settings.json",
                test: (key, s) => "the project's own test: " + key,
                rows: withHandlers ? (key, s) => { askedKey = key; askedCategory = s.String("category"); return @"[ { ""text"": ""a"" } ]"; } : (Func<string, UiSettings, string>)null,
                saveRows: withHandlers ? (key, s, loaded) =>
                {
                    savedLoaded = loaded;
                    savedNow = s.Rows(key).ToString(Formatting.None);
                    if (s.String("category") == "locked") throw new InvalidOperationException("The file is locked.");
                    return @"[ { ""text"": ""kept"" } ]";
                } : (Func<string, UiSettings, string, string>)null);
            string Answer() => cph.Globals[UiHost.AnswerVar("Demo")] as string;

            Assert.True(Handle("uiframework.rows:notes", @"{ ""category"": ""zelda"" }"));
            Assert.Equal("notes", askedKey);
            Assert.Equal("zelda", askedCategory);
            Assert.Equal(@"{""rows"":[{""text"":""a""}]}", Answer());

            Assert.True(Handle("uiframework.saveRows:notes", @"{ ""category"": ""zelda"", ""notes"": [ { ""text"": ""b"" } ], ""notes@loaded"": [ { ""text"": ""a"" } ] }"));
            Assert.Equal(@"[{""text"":""a""}]", savedLoaded);
            Assert.Equal(@"[{""text"":""b""}]", savedNow);
            Assert.Equal(@"{""rows"":[{""text"":""kept""}]}", Answer());

            // What the project throws is the reason; a project without the handlers says so; its own test stays its own.
            Assert.True(Handle("uiframework.saveRows:notes", @"{ ""category"": ""locked"", ""notes"": [] }"));
            Assert.Equal(@"{""error"":""The file is locked.""}", Answer());
            Assert.True(Handle("uiframework.rows:notes", "{}", withHandlers: false));
            Assert.Contains("names no handler", (string)JObject.Parse(Answer())["error"]);
            Assert.True(Handle("tryIt", "{}"));
            Assert.Equal("the project's own test: tryIt", Answer());
        }

        // ---- the window --------------------------------------------------------------------------------------------

        [Fact]
        public void TheRowsComeFromTheProjectAndAreNoChange()
        {
            using (var w = new Opened(WindowSchema))
            {
                w.Until(() => Texts(w.Editor.Value) == "sword,shield");
                Assert.Equal(new[] { "zelda" }, w.Asked.ToArray());
                Assert.True(w.On(() => w.Shows(Text.T("status.clean"))));

                // A save of something else: the project is not handed rows nobody changed, and the file has none.
                w.On(() => w.Box("bet").Text = "20");
                w.On(() => w.Click(Text.T("btn.save")));
                Assert.Empty(w.Saved);
                var file = JObject.Parse(File.ReadAllText(w.SettingsPath));
                Assert.Equal(20, (int)file["bet"]);
                Assert.Null(file["notes"]);
                Assert.Null(file["category"]);
            }
        }

        [Fact]
        public void ASaveHandsTheChangedRowsBackWithTheRowsTheWindowWasGiven()
        {
            using (var w = new Opened(WindowSchema))
            {
                w.Until(() => Texts(w.Editor.Value) == "sword,shield");
                w.On(() => w.Editor.Set(JArray.Parse(@"[ { ""text"": ""master sword"", ""author"": ""alice"", ""number"": 1 }, { ""text"": ""shield"", ""author"": ""bob"", ""number"": 2 } ]")));
                Assert.True(w.On(() => w.Shows(Text.T("status.unsaved.one"))));

                // Meanwhile chat added a note: the project keeps it and answers the rows as they are now.
                w.ChatAdds = "bow";
                w.On(() => w.Click(Text.T("btn.save")));
                var request = w.Saved.Single();
                Assert.Equal("master sword,shield", Texts((JArray)request["notes"]));
                Assert.Equal(new[] { 1, 2 }, ((JArray)request["notes"]).Select(r => (int)r["number"]).ToArray());   // what no column shows comes back
                Assert.Equal("sword,shield", Texts((JArray)request["notes@loaded"]));
                Assert.Equal("zelda", (string)request["category"]);                 // with the values as they stand
                Assert.Equal("master sword,shield,bow", w.On(() => Texts(w.Editor.Value)));
                Assert.True(w.On(() => w.Shows(Text.T("status.saved"))));
                // The rows the project answered are saved rows: a change elsewhere is the only one.
                w.On(() => w.Box("bet").Text = "11");
                Assert.True(w.On(() => w.Shows(Text.T("status.unsaved.one"))));
                w.On(() => w.Box("bet").Text = "10");
                Assert.Null(JObject.Parse(File.ReadAllText(w.SettingsPath))["notes"]);

                // What the window was handed is what the project answered: the next save compares with that.
                w.On(() => w.Editor.Set(JArray.Parse(@"[ { ""text"": ""master sword"" }, { ""text"": ""shield"" } ]")));
                w.ChatAdds = null;
                w.On(() => w.Click(Text.T("btn.save")));
                Assert.Equal("master sword,shield,bow", Texts((JArray)w.Saved.Last()["notes@loaded"]));
                Assert.Equal("master sword,shield", w.On(() => Texts(w.Editor.Value)));
            }
        }

        [Fact]
        public void RowsTheProjectDoesNotTakeStayUnsaved()
        {
            using (var w = new Opened(WindowSchema))
            {
                w.Until(() => Texts(w.Editor.Value) == "sword,shield");
                w.On(() => w.Editor.Set(JArray.Parse(@"[ { ""text"": ""changed"" } ]")));
                w.Refuses = "The file is locked.";
                w.On(() => w.Click(Text.T("btn.save")));
                Assert.Single(w.Saved);
                Assert.Equal("changed", w.On(() => Texts(w.Editor.Value)));
                Assert.Equal("sword,shield", Texts(w.Files["zelda"]));               // the project kept its own
                // Still an unsaved change: touching another field counts two.
                w.On(() => w.Box("bet").Text = "11");
                Assert.True(w.On(() => w.Shows(Text.T("status.unsaved.many", 2))));
                w.On(() => w.Box("bet").Text = "10");
                Assert.True(w.On(() => w.Shows(Text.T("status.unsaved.one"))));

                // Taken the next time.
                w.Refuses = null;
                w.On(() => w.Click(Text.T("btn.save")));
                Assert.Equal("changed", Texts(w.Files["zelda"]));
                Assert.True(w.On(() => w.Shows(Text.T("status.saved"))));
            }
        }

        [Fact]
        public void TheFieldTheRowsDependOnAsksAgainButNotOverUnsavedRows()
        {
            using (var w = new Opened(WindowSchema))
            {
                w.Until(() => Texts(w.Editor.Value) == "sword,shield");
                var category = w.On(() => w.Context.Parts.Combos["category"]);
                void Pick(string value) => w.On(() => category.SelectedItem = category.Items.OfType<ComboBoxItem>().Single(i => (string)i.Tag == value));
                string Picked() => w.On(() => (string)((ComboBoxItem)category.SelectedItem).Tag);

                Pick("mario");
                w.Until(() => Texts(w.Editor.Value) == "mushroom");
                Assert.Equal(new[] { "zelda", "mario" }, w.Asked.ToArray());
                Assert.True(w.On(() => w.Shows(Text.T("status.clean"))));            // the dropdown is transient, the rows were handed over

                // Unsaved rows belong to the category they were handed over for: the dropdown goes back.
                w.On(() => w.Editor.Set(JArray.Parse(@"[ { ""text"": ""fire flower"" } ]")));
                Pick("zelda");
                Assert.Equal("mario", Picked());
                w.Until(() => w.Shows(Text.T("list.rows.saveFirst", "Notes")));
                Thread.Sleep(150);
                w.On(() => { });
                Assert.Equal(new[] { "zelda", "mario" }, w.Asked.ToArray());       // and the project was not asked
                Assert.Equal("fire flower", w.On(() => Texts(w.Editor.Value)));

                // Saved, the dropdown moves on.
                w.On(() => w.Click(Text.T("btn.save")));
                Assert.Equal("fire flower", Texts(w.Files["mario"]));
                Pick("zelda");
                w.Until(() => Texts(w.Editor.Value) == "sword,shield");
            }
        }

        /// <summary>A project that answers slowly hands its rows over after the window is built. What the footer said
        /// by then stays: the note about values taken over from the version before is written once, when the window
        /// opens, and rows that are no change of the user's must not replace it with the plain count.</summary>
        [Fact]
        public void RowsThatArriveLateLeaveWhatTheFooterSays()
        {
            using (var w = new Opened(WindowSchema, slowMs: Patient.QuickMs + 300))
            {
                Assert.Equal("", w.On(() => Texts(w.Editor.Value)));                 // built, and the rows are not there yet
                w.On(() => w.Context.SetStatus("3 values taken over from the backup", Theme.UnsavedText, null));
                w.Until(() => Texts(w.Editor.Value) == "sword,shield");
                Assert.True(w.On(() => w.Shows("3 values taken over from the backup")));

                // A change of the user's still writes the count, and rows that arrive after it leave that too.
                w.On(() => w.Box("bet").Text = "20");
                Assert.True(w.On(() => w.Shows(Text.T("status.unsaved.one"))));
            }
        }

        /// <summary>The window, opened as a project opens it, with this class playing the project that keeps the notes:
        /// a list per category, handed over when asked and taken back on a save.</summary>
        private sealed class Opened : NoHooks, IDisposable
        {
            public readonly string Folder, SettingsPath;
            public readonly Dictionary<string, JArray> Files = new Dictionary<string, JArray>
            {
                ["zelda"] = JArray.Parse(@"[ { ""text"": ""sword"", ""author"": ""alice"", ""number"": 1 }, { ""text"": ""shield"", ""author"": ""bob"", ""number"": 2 } ]"),
                ["mario"] = JArray.Parse(@"[ { ""text"": ""mushroom"", ""author"": ""cleo"", ""number"": 1 } ]")
            };
            public readonly List<string> Asked = new List<string>();
            public readonly List<JObject> Saved = new List<JObject>();
            public volatile string ChatAdds, Refuses;

            private readonly int _slowMs;
            private readonly Thread _runner;
            private readonly ManualResetEventSlim _built = new ManualResetEventSlim();
            private volatile PageContext _context;
            private FrameworkElement _probe;
            private uint _thread;

            public Opened(string schemaJson, int slowMs = 0)
            {
                _slowMs = slowMs;
                Folder = Path.Combine(Path.GetTempPath(), "UIFramework.Tests", "rows-window-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(Folder);
                SettingsPath = Path.Combine(Folder, "settings.json");
                var schema = UIFramework.Schema.Parse(schemaJson);
                var spec = Entry.SpecFor(schema, SettingsPath);
                _runner = new Thread(() => { try { Entry.Show(spec, schema, new QuietLog(), this); } catch (Exception) { } }) { IsBackground = true };
                _runner.Start();
                if (!_built.Wait(15000))
                    throw new TimeoutException("The window was not built.");
                Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            }

            // The project: asked for the rows of a category, or handed rows to keep.
            public override string Test(string name, JObject settings)
            {
                if (name == ProjectRows.RowsRequest + "notes")
                {
                    string category = (string)settings["category"];
                    lock (Asked) Asked.Add(category);
                    if (_slowMs > 0) Thread.Sleep(_slowMs);
                    lock (Files) return new JObject { ["rows"] = Files[category].DeepClone() }.ToString(Formatting.None);
                }
                if (name == ProjectRows.SaveRequest + "notes")
                {
                    lock (Saved) Saved.Add((JObject)settings.DeepClone());
                    if (Refuses != null) return new JObject { ["error"] = Refuses }.ToString(Formatting.None);
                    var kept = (JArray)settings["notes"].DeepClone();
                    if (ChatAdds != null) kept.Add(new JObject { ["text"] = ChatAdds, ["author"] = "chat" });
                    lock (Files) Files[(string)settings["category"]] = kept;
                    return new JObject { ["rows"] = kept.DeepClone() }.ToString(Formatting.None);
                }
                return null;
            }

            public PageContext Context => _context;
            public Dispatcher Dispatcher => _probe.Dispatcher;
            public ListEditor Editor => _context.Parts.Lists["notes"];

            public override FrameworkElement BuildPage(string pageId, PageContext context)
            {
                _context = context;
                _probe = new TextBlock { Text = "probe" };
                _thread = GetCurrentThreadId();
                _built.Set();
                return _probe;
            }

            public T On<T>(Func<T> body) => Dispatcher.Invoke(body);
            public void On(Action body) => Dispatcher.Invoke(body);

            /// <summary>Waits until the window's thread says so; the answers of the project arrive there by themselves.</summary>
            public void Until(Func<bool> holds, int ms = 8000)
            {
                var watch = Stopwatch.StartNew();
                while (watch.ElapsedMilliseconds < ms)
                {
                    if (On(holds)) return;
                    Thread.Sleep(20);
                }
                Assert.True(On(holds), "not within " + ms + " ms");
            }

            public TextBox Box(string key) => _context.Parts.Boxes[key];

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

            public void Click(string text)
            {
                var button = All(Window).OfType<Button>().First(b => b.Content as string == text);
                button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button));
            }

            public bool Shows(string text) => All(Window).OfType<TextBlock>().Any(t => t.Text == text);

            private static IEnumerable<object> All(object node)
            {
                yield return node;
                if (node is DependencyObject d)
                    foreach (var child in LogicalTreeHelper.GetChildren(d))
                        foreach (var below in All(child))
                            yield return below;
            }

            private IntPtr Dialog()
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
                return found;
            }

            public void Dispose()
            {
                try
                {
                    Shell.Close(SettingsPath);
                    var watch = Stopwatch.StartNew();
                    while (_runner.IsAlive && watch.ElapsedMilliseconds < 15000)
                    {
                        // A question left open (unsaved rows at the close) is answered, so the window can go.
                        var dialog = Dialog();
                        if (dialog != IntPtr.Zero)
                        {
                            PostMessage(dialog, 0x0111 /* WM_COMMAND */, (IntPtr)6 /* Yes */, IntPtr.Zero);
                            PostMessage(dialog, 0x0010 /* WM_CLOSE */, IntPtr.Zero, IntPtr.Zero);
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
            [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
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
