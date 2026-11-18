using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Threading;
using Xunit;

namespace UIFramework.Tests
{
    /// <summary>A setting kept in a persisted global of Streamer.bot (<c>global</c>): the window opens with the global's
    /// value, a save hands it back to the host and never writes it to the settings file, an export, an import and a
    /// reset leave it alone, the requests to the project carry "" or "*" in its place, a host without globals shows it
    /// locked, a declaration that cannot work is named, and no log line carries the value.</summary>
    public class GlobalSettingTests
    {
        private const string Head = @"{ ""host"": ""demo"", ""version"": ""v1"", ""settingsFile"": ""x"", ""settings"": [
            { ""key"": ""bet"", ""default"": 10, ""page"": ""game"", ""label"": ""Bet"" },
            { ""key"": ""token"", ""default"": """", ""page"": ""game"", ""label"": ""Bot token"", ""masked"": true, ""global"": ""Demo_botToken"" },
            { ""key"": ""webhook"", ""default"": """", ""page"": ""game"", ""label"": ""Webhook"", ""global"": ""Demo_webhook"" },
            { ""key"": ""check"", ""default"": """", ""kind"": ""action"", ""page"": ""game"", ""label"": ""Check"", ""live"": true }";
        private const string Window = @" ], ""window"": { ""title"": ""Demo"", ""pages"": [ ""game"", { ""id"": ""probe"", ""title"": ""Probe"", ""custom"": true } ] } }";
        private const string Schema = Head + Window;

        // What an older version of the project left in the settings file under the key, and what the global holds.
        private const string InFile = "left-in-the-file", InGlobal = "s3cret-from-the-global", Typed = "typed-n3w-secret";

        private static void OldFile(string folder) =>
            File.WriteAllText(Path.Combine(folder, "settings.json"), @"{ ""bet"": 20, ""token"": """ + InFile + @""" }");

        // ---- Opened from the global ---------------------------------------------------------------------------

        [Fact]
        public void TheFieldOpensWithTheGlobalsValueAndTheWindowIsClean()
        {
            using (var w = new Opened(Schema, OldFile, host => host.Globals["Demo_botToken"] = InGlobal))
            {
                Assert.Equal(new[] { "Demo_botToken", "Demo_webhook" }, w.Read.ToArray());   // asked once each, as the window opened
                Assert.Equal(InGlobal, w.On(() => w.Box("token").Text));                     // not what the file holds
                Assert.Equal("", w.On(() => w.Box("webhook").Text));                         // no such global: the default
                // The secret's field is the masked one: dots in front, the value behind them.
                Assert.Contains(InGlobal, w.On(() => w.All<PasswordBox>().Select(p => p.Password).ToList()));
                Assert.Equal(Visibility.Collapsed, w.On(() => w.Box("token").Visibility));
                Assert.True(w.On(() => w.Shows(Text.T("status.clean"))));
                Assert.Empty(w.Written);
                w.NowhereButTheGlobal(InGlobal);
            }
        }

        // ---- Saved to the global, never to the file ------------------------------------------------------------

        [Fact]
        public void ASaveWritesTheGlobalAndTakesTheKeyOutOfTheFile()
        {
            using (var w = new Opened(Schema, OldFile, host => host.Globals["Demo_botToken"] = InGlobal))
            {
                w.On(() => w.Box("token").Text = Typed);
                Assert.True(w.On(() => w.Shows(Text.T("status.unsaved.one"))), "a changed global does not count as an unsaved change");
                w.On(() => w.Click(Text.T("btn.save")));

                Assert.Equal(new[] { "Demo_botToken=" + Typed }, w.Written.ToArray());   // the one that changed, once
                Assert.Equal(Typed, w.Globals["Demo_botToken"]);
                var file = w.Saved();
                Assert.Null(file.Property("token"));                                     // the older file's value went with the save
                Assert.Null(file.Property("webhook"));
                Assert.Equal(20, (int)file["bet"]);
                Assert.True(w.On(() => w.Shows(Text.T("status.saved"))));
                w.NowhereButTheGlobal(Typed);

                // Clean now: another save writes no global again, and the field still shows what was saved.
                w.On(() => w.Box("bet").Text = "30");
                w.On(() => w.Click(Text.T("btn.save")));
                Assert.Single(w.Written);
                Assert.Equal(30, (int)w.Saved()["bet"]);
                Assert.Null(w.Saved().Property("token"));
                Assert.Equal(Typed, w.On(() => w.Box("token").Text));
                w.NowhereButTheGlobal(Typed);
            }
        }

        [Fact]
        public void ASaveThatDidNotTouchTheFieldStillTakesAnOldValueOutOfTheFile()
        {
            using (var w = new Opened(Schema, OldFile, host => host.Globals["Demo_botToken"] = InGlobal))
            {
                w.On(() => w.Box("bet").Text = "30");
                // An action renewed the token while the window was open: a save that did not change it leaves it alone.
                w.Globals["Demo_botToken"] = "renewed-meanwhile";
                w.On(() => w.Click(Text.T("btn.save")));
                Assert.Empty(w.Written);
                Assert.Equal("renewed-meanwhile", w.Globals["Demo_botToken"]);
                Assert.Null(w.Saved().Property("token"));
                Assert.DoesNotContain(InFile, File.ReadAllText(w.SettingsPath));
            }
        }

        [Fact]
        public void AnEmptiedFieldClearsTheGlobal()
        {
            using (var w = new Opened(Schema, null, host => host.Globals["Demo_botToken"] = InGlobal))
            {
                w.On(() => w.Box("token").Text = "");
                w.On(() => w.Click(Text.T("btn.save")));
                Assert.Equal(new[] { "Demo_botToken=" }, w.Written.ToArray());
                Assert.False(w.Globals.ContainsKey("Demo_botToken"));
                Assert.Null(w.Saved().Property("token"));
            }
        }

        [Fact]
        public void AGlobalTheHostDidNotTakeStaysUnsaved()
        {
            using (var w = new Opened(Schema, null, host => host.Globals["Demo_botToken"] = InGlobal))
            {
                w.Refuses = true;
                w.On(() => { w.Box("token").Text = Typed; w.Box("bet").Text = "30"; });
                w.On(() => w.Click(Text.T("btn.save")));
                Assert.Equal(30, (int)w.Saved()["bet"]);                                  // the rest is saved
                Assert.Equal(InGlobal, w.Globals["Demo_botToken"]);
                Assert.True(w.On(() => w.Shows(Text.T("status.unsaved.one"))), "a global that was not written counts as saved");
                Assert.Contains(w.Log, line => line.StartsWith("error") && line.Contains("Demo_botToken") && line.Contains("still unsaved"));
                // The next save, with a host that takes it, writes it.
                w.Refuses = false;
                w.On(() => w.Click(Text.T("btn.save")));
                Assert.Equal(Typed, w.Globals["Demo_botToken"]);
                Assert.True(w.On(() => w.Shows(Text.T("status.saved"))));
                w.NowhereButTheGlobal(Typed);
            }
        }

        // ---- What the project is told ------------------------------------------------------------------------

        [Fact]
        public void TheRequestsCarryOnlyWhetherThereIsAValue()
        {
            using (var w = new Opened(Schema, OldFile, host => host.Globals["Demo_botToken"] = InGlobal))
            {
                // A Try button with the window's values as they stand: the token is there, the webhook is not.
                w.On(() => w.Click("Check"));
                var tried = w.Wait(() => w.Tested.LastOrDefault());
                Assert.Equal("*", (string)tried["token"]);
                Assert.Equal("", (string)tried["webhook"]);
                Assert.Equal(20, (int)tried["bet"]);                                      // everything else as it is

                // Typed and not saved yet: the check before the save says there is one, though the global has none.
                w.On(() => { w.Box("webhook").Text = "https://hooks.example/" + Typed; w.Box("token").Text = ""; });
                w.On(() => w.Click(Text.T("btn.save")));
                var validated = Assert.Single(w.Validated);
                Assert.Equal("", (string)validated["token"]);
                Assert.Equal("*", (string)validated["webhook"]);
                var saved = Assert.Single(w.SavedTold);
                Assert.Equal("", (string)saved["token"]);
                Assert.Equal("*", (string)saved["webhook"]);
                Assert.Equal(new[] { "Demo_botToken=", "Demo_webhook=https://hooks.example/" + Typed }, w.Written.OrderBy(x => x).ToArray());

                foreach (string secret in new[] { InGlobal, Typed, InFile })
                    Assert.DoesNotContain(w.Requests, json => json.Contains(secret));
                w.NowhereButTheGlobal(Typed);
            }
        }

        [Fact]
        public void AWindowWithoutSuchASettingTalksToItsHostAsBefore()
        {
            // No copy, no guard: the host gets the object the window holds, as it always did.
            var schema = UIFramework.Schema.Parse(@"{ ""host"": ""demo"", ""settings"": [ { ""key"": ""bet"", ""default"": 10 } ] }");
            var values = new JObject { ["bet"] = 3 };
            Assert.Same(values, Globals.Masked(values, schema));
            var hooks = new NoHooks();
            var spec = Entry.SpecFor(schema, null);
            try { Entry.Prepare(spec, schema); } finally { Text.Use(Conventions.Default.Lang); }
            Assert.Same(hooks, Globals.Guard(hooks, spec));

            var secret = UIFramework.Schema.Parse(Schema);
            var masked = Globals.Masked(new JObject { ["bet"] = 3, ["token"] = "x", ["webhook"] = "" }, secret);
            Assert.Equal(@"{""bet"":3,""token"":""*"",""webhook"":""""}", masked.ToString(Formatting.None));
            Assert.Null(Globals.Masked(new JObject { ["bet"] = 3 }, secret).Property("token"));   // a value the window does not have is not made up
        }

        // ---- Reset, export, import ------------------------------------------------------------------------------

        [Fact]
        public void ResetAllAndResetPageLeaveItAsItIs()
        {
            using (var w = new Opened(Schema, OldFile, host => host.Globals["Demo_botToken"] = InGlobal))
            {
                w.On(() => w.Box("bet").Text = "30");
                w.Answering(Opened.IdYes, () => w.Click(Text.T("tip.resetAll"), byTip: true));
                Assert.Equal("10", w.On(() => w.Box("bet").Text));
                Assert.Equal(InGlobal, w.On(() => w.Box("token").Text));                  // neither the default nor the file's
                Assert.True(w.On(() => w.Shows(Text.T("status.clean"))));
                Assert.Empty(w.Written);
                Assert.Null(w.Saved().Property("token"));                                 // Reset all writes the file: without the key
                Assert.Equal("*", (string)w.SavedTold.Last()["token"]);

                // Typed and not saved: both resets keep what the field shows, still unsaved.
                w.On(() => { w.Box("token").Text = Typed; w.Box("bet").Text = "40"; });
                w.Answering(Opened.IdYes, () => w.Click(Text.T("tip.resetPage"), byTip: true));
                Assert.Equal("10", w.On(() => w.Box("bet").Text));
                Assert.Equal(Typed, w.On(() => w.Box("token").Text));
                w.Answering(Opened.IdYes, () => w.Click(Text.T("tip.resetAll"), byTip: true));
                Assert.Equal(Typed, w.On(() => w.Box("token").Text));
                Assert.True(w.On(() => w.Shows(Text.T("status.unsaved.one"))), "the typed value does not count as unsaved after Reset all");
                Assert.Empty(w.Written);
                Assert.Equal(InGlobal, w.Globals["Demo_botToken"]);
                w.NowhereButTheGlobal(Typed);
                // A save then writes it.
                w.On(() => w.Click(Text.T("btn.save")));
                Assert.Equal(Typed, w.Globals["Demo_botToken"]);
                w.NowhereButTheGlobal(Typed);
            }
        }

        [Fact]
        public void AnExportLeavesItOutAndAnImportBringsNone()
        {
            using (var w = new Opened(Schema, OldFile, host => host.Globals["Demo_botToken"] = InGlobal))
            {
                string export = Path.Combine(w.Folder, "export.json");
                w.On(() => w.Export(export));
                var exported = (JObject)JObject.Parse(File.ReadAllText(export))["settings"];
                Assert.Equal(20, (int)exported["bet"]);
                Assert.Null(exported.Property("token"));
                Assert.Null(exported.Property("webhook"));
                Assert.DoesNotContain(InGlobal, File.ReadAllText(export));

                // A file that names the key (an export made by hand, an older settings file): its value is not taken.
                string import = Path.Combine(w.Folder, "import.json");
                File.WriteAllText(import, @"{ ""uiframework"": ""settings"", ""host"": ""demo"", ""settings"": { ""bet"": 55, ""token"": ""from-the-import"" } }");
                string asked = w.Answering(Opened.IdYes, () => w.Import(import));
                Assert.Contains("Bot token (" + w.On(() => Text.T("import.why.global")) + ")", asked);
                Assert.Equal(55, (int)w.Saved()["bet"]);
                Assert.Null(w.Saved().Property("token"));
                Assert.Equal(InGlobal, w.On(() => w.Box("token").Text));
                Assert.Equal(InGlobal, w.Globals["Demo_botToken"]);
                Assert.Empty(w.Written);
                Assert.Equal("*", (string)w.SavedTold.Last()["token"]);
                Assert.DoesNotContain(w.Requests, json => json.Contains("from-the-import") || json.Contains(InGlobal));
                Assert.DoesNotContain(w.Log, line => line.Contains("from-the-import"));
                w.NowhereButTheGlobal(InGlobal);
            }
        }

        // ---- A host without globals -----------------------------------------------------------------------------

        [Fact]
        public void WithoutGlobalsTheFieldIsLockedAndNothingOfItIsWritten()
        {
            using (var w = new Opened(Schema, OldFile, host => host.Globals["Demo_botToken"] = InGlobal, hasGlobals: false))
            {
                Assert.Empty(w.Read);
                string text = w.On(() => w.PageText());
                Assert.Contains("Bot token", text);
                Assert.Contains(w.On(() => Text.T("field.locked")), text);
                Assert.Contains(w.On(() => Text.T("field.global.locked")), text);
                Assert.DoesNotContain(InFile, text);                                      // what the file holds is not its value
                Assert.DoesNotContain(new string('•', 8), text);                          // nor dots, as for a secret that is there
                Assert.False(w.On(() => w.Context.Parts.Boxes.ContainsKey("token")));     // nothing to type into
                Assert.Empty(w.On(() => w.All<PasswordBox>().ToList()));

                w.On(() => w.Box("bet").Text = "30");
                w.On(() => w.Click(Text.T("btn.save")));
                Assert.Equal(30, (int)w.Saved()["bet"]);
                Assert.Null(w.Saved().Property("token"));
                Assert.Null(w.Saved().Property("webhook"));
                Assert.Empty(w.Written);
                // The window does not know whether there is a value: it says nothing about it.
                Assert.Null(Assert.Single(w.Validated).Property("token"));
                Assert.Null(Assert.Single(w.SavedTold).Property("token"));
                Assert.DoesNotContain(w.Requests, json => json.Contains(InFile));
            }
        }

        [Fact]
        public void ThePreviewAndAnOlderSettingsActionHaveNoGlobals()
        {
            Assert.False(new NoHooks().HasGlobals);
            Assert.False(new ReflectionHost(new OlderHost()).HasGlobals);
            Assert.True(new ReflectionHost(new NewerHost()).HasGlobals);
            // Resolved without a host that has them, every such setting is locked with the reason in the window's language.
            var spec = Prepared(Schema, hostHasGlobals: false, language: "de");
            Assert.Empty(spec.Problems);
            var token = spec.ResolvedSettings.First(s => s.Key == "token");
            Assert.True(token.Locked && token.ReadOnly && token.GlobalOff);
            Assert.StartsWith("Dieser Wert liegt in den globalen Variablen", token.LockReason);
            Assert.False(Prepared(Schema, hostHasGlobals: true).ResolvedSettings.First(s => s.Key == "token").ReadOnly);
        }

        public sealed class OlderHost { public void OnSaved(string settingsJson) { } }
        public sealed class NewerHost
        {
            public string GetGlobal(string name) => null;
            public void SetGlobal(string name, string value) { }
        }

        // ---- Declarations that cannot work ----------------------------------------------------------------------

        [Fact]
        public void ADeclarationThatCannotWorkIsNamedAndItsFieldLocked()
        {
            var spec = Prepared(@"{ ""host"": ""demo"", ""settings"": [
                { ""key"": ""fine"", ""default"": """", ""global"": ""Demo_fine"" },
                { ""key"": ""button"", ""kind"": ""action"", ""global"": ""Demo_button"" },
                { ""key"": ""hint"", ""kind"": ""note"", ""default"": ""Read me"", ""global"": ""Demo_hint"" },
                { ""key"": ""scores"", ""kind"": ""table"", ""columns"": [ ""name"" ], ""global"": ""Demo_scores"" },
                { ""key"": ""words"", ""kind"": ""list"", ""default"": [], ""columns"": [ ""word"" ], ""global"": ""Demo_words"" },
                { ""key"": ""mods"", ""kind"": ""namelist"", ""default"": [], ""global"": ""Demo_mods"" },
                { ""key"": ""unnamed"", ""default"": """", ""global"": "" "" },
                { ""key"": ""twice"", ""default"": """", ""global"": ""Demo_fine"" },
                { ""key"": ""filed"", ""default"": """", ""global"": ""Demo_filed"", ""file"": ""secret.json"" } ] }", hostHasGlobals: true);

            Assert.Equal(new[]
            {
                // In the order of the table; 'fine' is not among them.
                "'button' has global, which only a field that holds a value has; it does nothing on a button.",
                "'hint' has global, which only a field that holds a value has; it does nothing on a note.",
                "'scores' has global, which only a field that holds a value has; it does nothing on a table.",
                "'words' has global, which a list field cannot have: a global holds one text, not rows. The field is locked, and its value is neither read nor saved.",
                "'mods' has global, which a namelist field cannot have: a global holds one text, not rows. The field is locked, and its value is neither read nor saved.",
                "'unnamed' has global without a name; write the global's name, global: \"MyExtension_token\". The field is locked, and its value is neither read nor saved.",
                "'twice' names the global 'Demo_fine', which 'fine' names already: two fields cannot share one. The field is locked, and its value is neither read nor saved.",
                "'filed' has global and file: a value kept in a global is in no file, so one of the two has to go. The field is locked, and its value is neither read nor saved."
            }, spec.Problems.Where(problem => problem.Contains(" global")).ToArray());

            Setting Of(string key) => spec.ResolvedSettings.First(s => s.Key == key);
            Assert.True(Globals.Used(Of("fine")) && !Of("fine").ReadOnly);
            foreach (string key in new[] { "words", "mods", "unnamed", "twice", "filed" })
                Assert.True(Of(key).GlobalOff && Of(key).ReadOnly && Of(key).Locked, key);
            Assert.True(Of("button").GlobalOff && !Of("button").Locked);                  // a button holds no value to lock

            // Not one of them falls back to a file: neither the settings file nor the one a setting names.
            string folder = Path.Combine(Path.GetTempPath(), "UIFramework.Tests", "global-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                var values = new JObject { ["fine"] = "a", ["words"] = new JArray(new JObject { ["word"] = "b" }), ["mods"] = new JArray("c"), ["unnamed"] = "d", ["twice"] = "e", ["filed"] = "f", ["other"] = 1 };
                var main = Sidecars.Split(values, spec.ResolvedSettings, Path.Combine(folder, "settings.json"));
                Assert.Equal(@"{""other"":1}", main.ToString(Formatting.None));
                Assert.False(File.Exists(Path.Combine(folder, "secret.json")));
            }
            finally { Directory.Delete(folder, true); }
        }

        // ---- The pieces -----------------------------------------------------------------------------------------

        [Fact]
        public void AGlobalsTextBecomesWhatTheFieldHolds()
        {
            Assert.Equal("abc", (string)Globals.Value(new Setting { Key = "t", Default = "" }, "abc"));
            Assert.Equal(" abc ", (string)Globals.Value(new Setting { Key = "t" }, " abc "));
            Assert.Equal(JTokenType.Boolean, Globals.Value(new Setting { Key = "b", Default = false }, "True").Type);
            Assert.Equal(7, (long)Globals.Value(new Setting { Key = "n", Default = 1 }, " 7 "));
            Assert.Equal(0.5, (double)Globals.Value(new Setting { Key = "d", Default = 1.5 }, "0.5"));
            Assert.Equal("seven", (string)Globals.Value(new Setting { Key = "n", Default = 1 }, "seven"));   // no number: as it is
            // No global at all is the default, as a key the file lacks is.
            Assert.Equal("fallback", (string)Globals.Value(new Setting { Key = "t", Default = "fallback" }, null));
            Assert.Equal("", (string)Globals.Value(new Setting { Key = "t" }, null));
        }

        [Fact]
        public void EveryWriteOfTheSettingsFileAndTheExportLeaveItOut()
        {
            var settings = new[] { new Setting { Key = "token", Kind = "string", Global = "Demo_botToken" }, new Setting { Key = "bet", Kind = "int" } };
            var main = new JObject { ["token"] = "x", ["bet"] = 1 };
            Sidecars.DropTransient(main, settings);
            Assert.Equal(@"{""bet"":1}", main.ToString(Formatting.None));
            // Reset all starts from the file: a value an older file holds there is not reset to the default either.
            var fresh = SettingsStore.ResetShown(new JObject { ["token"] = "old", ["bet"] = 5 }, new JObject { ["token"] = "", ["bet"] = 1 }, settings);
            Assert.Equal("old", (string)fresh["token"]);
            Assert.Equal(1, (int)fresh["bet"]);
            // The window's copy, not the file's, is what goes on: laid over, or taken out where the window has none.
            Globals.Keep(fresh, new JObject { ["token"] = "mine" }, settings);
            Assert.Equal("mine", (string)fresh["token"]);
            Globals.Keep(fresh, new JObject(), settings);
            Assert.Null(fresh.Property("token"));
        }

        private static WindowSpec Prepared(string schemaJson, bool hostHasGlobals, string language = null)
        {
            try
            {
                var schema = UIFramework.Schema.Parse(schemaJson);
                var spec = Entry.SpecFor(schema, null);
                spec.HostHasGlobals = hostHasGlobals;
                if (language != null) spec.Language = language;
                Entry.Prepare(spec, schema);
                return spec;
            }
            finally { Text.Use(Conventions.Default.Lang); }
        }

        // ---- The window, opened as a project opens it, by a host that keeps globals ---------------------------

        /// <summary>Shell.Run on a thread of its own, as the settings action opens the window. The host keeps its
        /// globals in a dictionary and remembers every request and every log line, so a test can look for a value
        /// where it must not be.</summary>
        private sealed class Opened : NoHooks, IHostLog, IDisposable
        {
            public readonly string Folder, SettingsPath;
            public readonly Dictionary<string, string> Globals = new Dictionary<string, string>();
            /// <summary>The globals the window asked for, and those it wrote as "name=value", in order.</summary>
            public readonly List<string> Read = new List<string>(), Written = new List<string>();
            public readonly List<JObject> Validated = new List<JObject>(), SavedTold = new List<JObject>(), Tested = new List<JObject>();
            /// <summary>Every request's values as the settings action would send them, and every log line.</summary>
            public readonly List<string> Requests = new List<string>(), Log = new List<string>();
            public bool Refuses;

            private readonly bool _hasGlobals;
            private readonly Thread _runner;
            private readonly ManualResetEventSlim _built = new ManualResetEventSlim();
            private volatile PageContext _context;
            private FrameworkElement _probe;
            private uint _thread;

            public Opened(string schemaJson, Action<string> files = null, Action<Opened> before = null, bool hasGlobals = true)
            {
                _hasGlobals = hasGlobals;
                Folder = Path.Combine(Path.GetTempPath(), "UIFramework.Tests", "global-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(Folder);
                SettingsPath = Path.Combine(Folder, "settings.json");
                files?.Invoke(Folder);
                before?.Invoke(this);
                var schema = UIFramework.Schema.Parse(schemaJson);
                var spec = Entry.SpecFor(schema, SettingsPath);
                _runner = new Thread(() => { try { Entry.Show(spec, schema, this, this); } catch (Exception) { } }) { IsBackground = true };
                _runner.Start();
                if (!_built.Wait(15000))
                    throw new TimeoutException("The window was not built.");
                Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            }

            public PageContext Context => _context;
            public Dispatcher Dispatcher => _probe.Dispatcher;
            public Window Window => (Window)Up(_probe, n => n is Window);

            // ---- the host ----
            public override bool HasGlobals => _hasGlobals;
            public override string GetGlobal(string name)
            {
                lock (Read) Read.Add(name);
                return Globals.TryGetValue(name, out var value) ? value : null;
            }
            public override bool SetGlobal(string name, string value)
            {
                if (Refuses) return false;
                lock (Read) Written.Add(name + "=" + value);
                if (string.IsNullOrEmpty(value)) Globals.Remove(name); else Globals[name] = value;
                return true;
            }
            public override FrameworkElement BuildPage(string pageId, PageContext context)
            {
                _context = context;
                _probe = new TextBlock { Text = "probe" };
                _thread = GetCurrentThreadId();
                _built.Set();
                return _probe;
            }
            public override string Validate(JObject settings) { Told(Validated, settings); return null; }
            public override void OnSaved(JObject settings) => Told(SavedTold, settings);
            public override string Test(string name, JObject settings) { Told(Tested, settings); return "ok"; }
            private void Told(List<JObject> list, JObject settings)
            {
                lock (Requests)
                {
                    list.Add((JObject)settings.DeepClone());
                    Requests.Add(settings.ToString(Formatting.None));
                }
            }
            public void Info(string message) { lock (Log) Log.Add("info " + message); }
            public void Warn(string message) { lock (Log) Log.Add("warn " + message); }
            public void Error(string message) { lock (Log) Log.Add("error " + message); }

            // ---- the window ----
            public T On<T>(Func<T> body) => Dispatcher.Invoke(body);
            public void On(Action body) => Dispatcher.Invoke(body);
            public JObject Saved() => JObject.Parse(File.ReadAllText(SettingsPath));
            public TextBox Box(string key) => _context.Parts.Boxes[key];

            public void Click(string text, bool byTip = false)
            {
                var button = All<Button>().First(b => byTip ? b.ToolTip as string == text : b.Content as string == text);
                button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button));
            }

            public List<T> All<T>() => All(Window).OfType<T>().ToList();
            public bool Shows(string text) => All<TextBlock>().Any(t => t.Text == text);

            /// <summary>Every text on the pages, a line of runs as one.</summary>
            public string PageText() => string.Join(" | ", All<TextBlock>()
                .Select(t => t.Inlines.Count > 0 ? string.Concat(t.Inlines.OfType<Run>().Select(r => r.Text)) : t.Text).Where(t => !string.IsNullOrEmpty(t)));

            /// <summary>The customise bar's Export and Import behind their file dialogs.</summary>
            public void Export(string file) => Behind("ExportTo")(file);
            public void Import(string file) => Behind("ImportFrom")(file);
            private Action<string> Behind(string name) =>
                (Action<string>)typeof(PageContext).GetProperty(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(_context);

            /// <summary>Runs what asks a question on the window's thread, answers the question, and returns its text.</summary>
            public string Answering(int button, Action body)
            {
                var done = new ManualResetEventSlim();
                Dispatcher.BeginInvoke(new Action(() => { try { body(); } finally { done.Set(); } }));
                var question = Dialog(5000);
                Assert.NotEqual(IntPtr.Zero, question);
                var text = new StringBuilder(4096);
                GetDlgItemText(question, 0xFFFF, text, text.Capacity);   // a message box's text is its static with id -1
                PostMessage(question, 0x0111 /* WM_COMMAND */, (IntPtr)button, IntPtr.Zero);
                Assert.True(done.Wait(15000), "the window did not go on after the answer");
                Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                return text.ToString();
            }

            /// <summary>What a request leaves behind on another thread, waited for.</summary>
            public T Wait<T>(Func<T> read) where T : class
            {
                var watch = Stopwatch.StartNew();
                while (watch.ElapsedMilliseconds < 10000)
                {
                    T found;
                    lock (Requests) found = read();
                    if (found != null) return found;
                    Thread.Sleep(20);
                }
                throw new TimeoutException("The host was not asked.");
            }

            /// <summary>The value is in no file of the folder, in no request and in no log line.</summary>
            public void NowhereButTheGlobal(string secret)
            {
                foreach (string file in Directory.GetFiles(Folder, "*", SearchOption.AllDirectories))
                    Assert.False(File.ReadAllText(file).Contains(secret), Path.GetFileName(file) + " holds the value");
                lock (Requests) Assert.DoesNotContain(Requests, json => json.Contains(secret));
                lock (Log) Assert.DoesNotContain(Log, line => line.Contains(secret));
            }

            public const int IdYes = 6, IdNo = 7;

            private IntPtr Dialog(int ms)
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

            public void Dispose()
            {
                try
                {
                    Shell.Close(SettingsPath);
                    var watch = Stopwatch.StartNew();
                    while (_runner.IsAlive && watch.ElapsedMilliseconds < 15000)
                    {
                        // A question a failed test left open is answered, so the window can go.
                        var dialog = Dialog(0);
                        if (dialog != IntPtr.Zero)
                        {
                            PostMessage(dialog, 0x0010 /* WM_CLOSE */, IntPtr.Zero, IntPtr.Zero);
                            PostMessage(dialog, 0x0111, (IntPtr)IdNo, IntPtr.Zero);
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
            [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
            [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
        }
    }
}
