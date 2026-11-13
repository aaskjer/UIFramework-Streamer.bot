using Newtonsoft.Json.Linq;
using Xunit;

namespace UIFramework.Tests
{
    /// <summary>The kit's contract: what a game declares in its host block (stub/HostSchema.cs, linked into this
    /// project) reaches the window, and the block answers the generic stub's requests.</summary>
    public class HostBlockTests
    {
        private static string TempSettings(string name) => Path.Combine(Path.GetTempPath(), "UIFramework.Tests", name, "settings.json");

        private static JObject SchemaWith(JObject window)
        {
            var root = JObject.Parse(@"{ ""host"": ""demo"", ""version"": ""v1.0.0"", ""settingsFile"": ""Demo/settings.json"", ""settings"": [
                { ""key"": ""useBot"", ""default"": true },
                { ""key"": ""sides"", ""default"": 6, ""page"": ""game"", ""min"": 2, ""max"": 100 },
                { ""key"": ""logToFile"", ""default"": false },
                { ""key"": ""tryRoll"", ""kind"": ""action"", ""page"": ""game"", ""live"": true, ""panel"": true },
                { ""key"": ""msgRoll"", ""default"": ""@{user} rolls {roll}"", ""kind"": ""template"", ""group"": ""Rolls"", ""label"": ""Roll"", ""placeholders"": [""user"", ""roll""] }
            ] }");
            if (window != null) root["window"] = window;
            return root;
        }

        [Fact]
        public void LoadFallsBackToTheBackupAndSaysSo()
        {
            string file = TempSettings("load-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            File.WriteAllText(file, @"{ ""sides"": 12 }");
            var fine = UiSettings.Load(file);
            Assert.Null(fine.Problem);
            Assert.Equal(12, fine.Raw.Value<int>("sides"));

            File.WriteAllText(file + ".bak", @"{ ""sides"": 8 }");
            File.WriteAllText(file, "{ damaged");
            string heard = null;
            var backup = UiSettings.Load(file, message => heard = message);
            Assert.True(backup.FromBackup);
            Assert.Equal(8, backup.Raw.Value<int>("sides"));
            Assert.Contains("backup", backup.Problem);
            Assert.Equal(backup.Problem, heard);

            File.Delete(file + ".bak");
            var none = UiSettings.Load(file);
            Assert.False(none.FromBackup);
            Assert.Contains("defaults", none.Problem);
            Assert.Empty(none.Raw.Properties());
        }

        [Fact]
        public void AFileMissingBesideItsBackupIsReadFromTheBackup()
        {
            // The window's save swaps the new file in (File.Replace, the old one becoming the .bak): for a moment, or
            // after a swap that failed halfway, there is only the .bak. A round starting then played on the defaults.
            string file = TempSettings("missing-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            File.WriteAllText(file + ".bak", @"{ ""sides"": 8 }");
            string heard = null;
            var backup = UiSettings.Load(file, message => heard = message);
            Assert.True(backup.FromBackup);
            Assert.Equal(8, backup.Raw.Value<int>("sides"));
            Assert.Contains("missing", backup.Problem);
            Assert.Equal(backup.Problem, heard);

            // A file that appears while it is waited for is read.
            File.Delete(file + ".bak");
            File.WriteAllText(file + ".tmp", @"{ ""sides"": 12 }");
            var swap = new Thread(() => { Thread.Sleep(20); File.Move(file + ".tmp", file); });
            swap.Start();
            var later = UiSettings.Load(file);
            swap.Join();
            Assert.Null(later.Problem);
            Assert.Equal(12, later.Raw.Value<int>("sides"));
        }

        [Fact]
        public void AFileNeverSavedIsTheDefaultsWithoutAWord()
        {
            string file = TempSettings("never-" + Guid.NewGuid().ToString("N"));
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var none = UiSettings.Load(file);
            Assert.Null(none.Problem);
            Assert.False(none.FromBackup);
            Assert.Empty(none.Raw.Properties());
            Assert.True(watch.ElapsedMilliseconds < 100, "a game whose settings were never saved waited " + watch.ElapsedMilliseconds + " ms");
        }

        [Fact]
        public void AHandlerThatStillWorksSaysSoWhereTheSettingsActionLooks()
        {
            var cph = new FakeCph();
            string var = "uiframework.answer.Demo." + Guid.NewGuid().ToString("N");
            string key = UiHost.WorkingKey(var);
            object during = null;
            var args = new Dictionary<string, object> { ["uiframework.request"] = "test", ["uiframework.name"] = "importWords", ["uiframework.var"] = var };
            try
            {
                Assert.True(UiHost.Handle(args, cph, "Demo", "demo", "v1.0.0", "Demo/settings.json",
                    test: (name, values) => { UiHost.StillWorking(); during = AppDomain.CurrentDomain.GetData(key); return "imported"; }));
                Assert.IsType<long>(during);
                Assert.Equal("imported", cph.Globals[var]);

                // After the request, nothing is said for it any more.
                AppDomain.CurrentDomain.SetData(key, null);
                UiHost.StillWorking();
                Assert.Null(AppDomain.CurrentDomain.GetData(key));
            }
            finally { AppDomain.CurrentDomain.SetData(key, null); }
        }

        [Fact]
        public void TheUpdateNoticeTakesTheNextOlderDllWhenTheNewestCannotBeUsed()
        {
            // A Streamer.bot of its own (an AppDomain whose folder holds UIFramework\lib), with no UIFramework loaded:
            // 3.0.0 needs a newer settings action, 2.0.0 lost its hash, 1.0.0 is fine. The block refused the newest
            // once and then had no DLL for the whole run.
            string root = Path.Combine(Path.GetTempPath(), "UIFramework.Tests", "lib-" + Guid.NewGuid().ToString("N"));
            string lib = Path.Combine(root, "UIFramework", "lib");
            foreach (var version in new[] { "1.0.0", "2.0.0", "3.0.0" }) Directory.CreateDirectory(Path.Combine(lib, version));
            File.Copy(typeof(Entry).Assembly.Location, Path.Combine(lib, "1.0.0", "UIFramework.dll"));
            File.Copy(typeof(JObject).Assembly.Location, Path.Combine(lib, "1.0.0", "Newtonsoft.Json.dll"));
            File.WriteAllText(Path.Combine(lib, "2.0.0", "UIFramework.dll"), "abc");
            File.WriteAllText(Path.Combine(lib, "2.0.0", "UIFramework.dll.sha256"), "0000");
            TooNewDll(Path.Combine(lib, "3.0.0"));

            var domain = AppDomain.CreateDomain("probe", null, new AppDomainSetup { ApplicationBase = root });
            try
            {
                var probe = (FrameworkProbe)domain.CreateInstanceFromAndUnwrap(typeof(FrameworkProbe).Assembly.Location, typeof(FrameworkProbe).FullName);
                Assert.Equal(Path.Combine(lib, "1.0.0", "UIFramework.dll"), probe.Notices(), StringComparer.OrdinalIgnoreCase);
                Assert.Equal(Path.Combine(lib, "1.0.0", "UIFramework.dll"), probe.Notices(), StringComparer.OrdinalIgnoreCase);   // and again
            }
            finally
            {
                AppDomain.Unload(domain);
                try { Directory.Delete(root, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }

        // A DLL whose product version asks for a settings action no game has: "9.0.0+stub.99.0".
        private static void TooNewDll(string folder)
        {
            var name = new System.Reflection.AssemblyName("TooNew") { Version = new Version(9, 0, 0, 0) };
            var builder = AppDomain.CurrentDomain.DefineDynamicAssembly(name, System.Reflection.Emit.AssemblyBuilderAccess.Save, folder);
            builder.DefineDynamicModule("TooNew", "UIFramework.dll");
            builder.DefineVersionInfoResource("UIFramework", "9.0.0+stub.99.0", "", "", "");
            builder.Save("UIFramework.dll");
        }

        [Fact]
        public void TheBlockKnowsTheSettingsActionItIsReleasedWith()
        {
            Assert.False(UiUpdateNotice.NeedsNewerStub(typeof(Entry).Assembly.Location));   // 1.x+stub.0.1.0 fits
            string folder = Path.Combine(Path.GetTempPath(), "UIFramework.Tests", "too-new-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            TooNewDll(folder);
            Assert.True(UiUpdateNotice.NeedsNewerStub(Path.Combine(folder, "UIFramework.dll")));
        }

        [Fact]
        public void LoadReadsARelativePathFromTheStreamerbotFolder()
        {
            string name = "uif-relative-" + Guid.NewGuid().ToString("N") + ".json";
            string full = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, name);
            File.WriteAllText(full, @"{ ""sides"": 20 }");
            string before = Environment.CurrentDirectory;
            try
            {
                Environment.CurrentDirectory = Path.GetTempPath();
                Assert.Equal(20, UiSettings.Load(name).Raw.Value<int>("sides"));
            }
            finally
            {
                Environment.CurrentDirectory = before;
                File.Delete(full);
            }
        }

        [Fact]
        public void TheWindowCarriesTheGamesOwnTranslations()
        {
            var window = new UiWindow("Demo")
                .Lang("de", UiSchema.K("numDecks", "Decks im Schuh", "Wie viele."), UiSchema.K("egg", "Ei"), UiSchema.Pg("fleet", "Flotte"), UiSchema.Tx("Shoe", "Schuh"))
                .Lang("de", UiSchema.Tx("Ships", "Schiffe"));
            var lang = window.LangMap["de"];
            Assert.Equal("Wie viele.", (string)lang["keys"]["numDecks"]["help"]);
            Assert.Equal("Ei", (string)lang["keys"]["egg"]);
            Assert.Equal("Flotte", (string)lang["pages"]["fleet"]);
            Assert.Equal("Schuh", (string)lang["texts"]["Shoe"]);
            Assert.Equal("Schiffe", (string)lang["texts"]["Ships"]);
            var parsed = Schema.Parse(JObject.Parse("{ \"host\": \"demo\", \"version\": \"1\", \"settings\": [], \"lang\": " + window.LangMap.ToString(Newtonsoft.Json.Formatting.None) + " }"));
            Assert.Equal("Schuh", (string)parsed.Lang["de"]["texts"]["Shoe"]);
            Assert.Equal("Schuh", (string)parsed.ToJson()["lang"]["de"]["texts"]["Shoe"]);
        }

        [Fact]
        public void APointsSystemIsDetectedFromItsGlobal()
        {
            var cph = new FakeCph();
            var none = UiCurrency.Detect(cph, null);
            Assert.False(none.Detected);
            Assert.Equal("points", none.Variable);

            cph.Globals["tawmae_Settings_LOYALTY POINTS"] = "{\"globalVariableName\": \"loyalty\", \"pointsName\": \"Gems\"}";
            var tawmae = UiCurrency.Detect(cph, null);
            Assert.Equal("LOYALTY POINTS", tawmae.Source);
            Assert.Equal("loyalty", tawmae.Variable);
            Assert.Equal("Gems", tawmae.Name);

            cph.Globals["currency_ProductSettings"] = "{\"pointsVar\": \"coins\", \"pointsName\": \"Coins\"}";
            var core = UiCurrency.Detect(cph, null);   // Currency Core wins when both are there
            Assert.Equal("Currency Core", core.Source);
            Assert.Equal("coins", core.Variable);

            cph.Globals["currency_ProductSettings"] = "not json";
            Assert.Equal("LOYALTY POINTS", UiCurrency.Detect(cph, null).Source);   // a broken one is skipped with a warning
            Assert.Contains(cph.Log, line => line.StartsWith("warn"));

            Assert.True(UiHost.Handle(new Dictionary<string, object> { ["uiframework.request"] = "describe" }, cph, "Demo Game", "demo", "v1.0.0", "Demo/settings.json"));
            var described = JObject.Parse((string)cph.Globals[UiSchema.Var("Demo Game")]);
            Assert.Equal("LOYALTY POINTS", (string)described["currency"]["source"]);
            ((JArray)described["settings"]).Add(new JObject { ["key"] = "pointsVariable", ["default"] = "points" });   // the template's table is empty
            var schema = Schema.Parse(described.ToString());
            var spec = Entry.SpecFor(null, schema, null);
            Entry.Prepare(spec, schema);
            Assert.Contains(spec.HeaderPills, pill => pill.Read(null).Text == "LOYALTY POINTS: loyalty");
            Assert.Contains(spec.ResolvedSettings, s => s.Key == "pointsVariable" && s.Help.Contains("LOYALTY POINTS is installed"));
        }

        [Fact]
        public void TheWindowBuilderReachesTheSpec()
        {
            var window = new UiWindow("Demo Game", releases: "aaskjer/Demo", logo: "https://example.test/logo.png", width: 700)
                .Pages(UiSchema.P("game", "Game", "dice"), "messages", "log", "help", "about")
                .Log("Demo/Logs", "demo-log*.txt", writeLogKey: "logToFile", columns: new[] { "Player|User", "Roll" }, results: new[] { "Win", "Loss" })
                .Cooldowns(UiSchema.C("cmd-1", "globalCooldown", null))
                .Pill("useBot", "Bot on", "Bot off", tooltip: "Who sends")
                .Samples("user", "alice", "roll", "6")
                .Answer("tryRoll", "@alice rolls 6")
                .Override("sides", label: "Die sides")
                .Help(UiSchema.H("Playing", UiSchema.Cmd("!roll", "Rolls."), "A note.", UiSchema.Link("Guide", "https://example.test")))
                .About(credits: "By someone", links: new[] { UiSchema.Link("Discord", "https://discord.test") }, defaultLinks: false);
            var json = window.ToJson();
            Assert.Equal("demo-log*.txt", (string)json["log"]["files"]);
            Assert.Equal("cmd-1", (string)json["cooldowns"][0]["command"]);
            Assert.Null((string)json["cooldowns"][0]["user"]);

            var schema = Schema.Parse(SchemaWith(json));
            Assert.NotNull(schema.Window);
            var spec = Entry.SpecFor(null, schema, TempSettings("builder"));
            Entry.Prepare(spec, schema);

            Assert.Equal("Demo Game", spec.Title);
            Assert.Equal("aaskjer/Demo", spec.GitHubRepo);
            Assert.Equal(700, spec.Width);
            Assert.Equal("[Demo Game/Settings]", spec.LogPrefix);
            Assert.Equal(new[] { "game", "messages", "help", "log", "about" }, spec.Pages.Select(p => p.Id).ToArray());   // How to play, Log, About at the foot
            Assert.Equal(new[] { "help", "log", "about" }, spec.Pages.Where(p => p.Bottom).Select(p => p.Id).ToArray());
            Assert.Equal("🎲", spec.Pages[0].Glyph);
            Assert.Equal("Die sides", spec.ResolvedSettings.First(s => s.Key == "sides").Label);
            Assert.Equal("Demo/Logs", spec.Log.Folder);
            Assert.Equal("logToFile", spec.Log.WriteLogKey);
            Assert.Equal("Player", spec.Log.Columns[0].Header);
            Assert.Equal("User", spec.Log.Columns[0].Field);
            Assert.Equal(new[] { "Win", "Loss" }, spec.Log.Results.ToArray());
            Assert.Equal("alice", spec.Samples["user"]);
            Assert.Equal("@alice rolls 6", spec.Answers["tryRoll"]);
            Assert.Single(spec.HeaderPills);
            Assert.Equal("Playing", spec.Help[0].Heading);
            Assert.Equal(3, spec.Help[0].Lines.Count);
            Assert.Equal("By someone", spec.About.Credits);
            Assert.False(spec.About.ShowDefaultLinks);
            Assert.Equal("Discord", spec.About.Links[0].Text);
            // The bare template's table declares nothing, so the cooldown reads no setting: that is the one problem.
            Assert.Single(spec.Problems, p => p.Contains("'globalCooldown', which is not a setting"));

            // A spec the stub carries wins over the window in the schema; a window without a title is called after the host.
            Assert.Equal("Other", Entry.SpecFor(@"{ ""title"": ""Other"" }", schema, TempSettings("builder")).Title);
            var untitled = Schema.Parse(SchemaWith(new JObject { ["pages"] = new JArray("about") }));
            Assert.Equal("Demo", Entry.SpecFor("", untitled, TempSettings("builder")).Title);
        }

        [Fact]
        public void TheBlockAnswersTheStubsRequests()
        {
            var cph = new FakeCph();
            string tested = null; int testedSides = 0; bool savedRan = false; string requestWhileSaved = null;
            bool Handle(Dictionary<string, object> args) => UiHost.Handle(args, cph, "Demo Game", "demo", "v1.0.0", "Demo/settings.json",
                test: (name, s) => { tested = name; testedSides = s.Int("sides"); return name == "tryRoll" ? "rolled" : null; },
                validate: s => s.Int("sides") < 2 ? "Too few sides." : null,
                saved: s => { savedRan = true; requestWhileSaved = cph.Arguments.TryGetValue("uiframework.request", out var r) ? r as string : null; },
                table: key => "[{\"a\":1}]");

            Assert.False(Handle(new Dictionary<string, object> { ["user"] = "alice" }));

            Assert.True(Handle(new Dictionary<string, object> { ["uiframework.request"] = "describe" }));
            var described = JObject.Parse((string)cph.Globals[UiSchema.Var("Demo Game")]);
            Assert.Equal("demo", (string)described["host"]);
            Assert.Equal("Demo/settings.json", (string)described["settingsFile"]);

            Assert.True(Handle(new Dictionary<string, object> { ["uiframework.describe"] = "1" }));   // the old flag still describes
            Assert.True(Handle(new Dictionary<string, object> { ["uiframework.request"] = "describe", ["uiframework.var"] = "uiframework.schema.Other Name" }));
            Assert.Contains("\"host\"", (string)cph.Globals["uiframework.schema.Other Name"]);   // the stub names the global; the game's own name does not matter

            Assert.True(Handle(new Dictionary<string, object> { ["uiframework.request"] = "test", ["uiframework.name"] = "tryRoll", ["uiframework.values"] = "{\"sides\": 8}" }));
            Assert.Equal("tryRoll", tested);
            Assert.Equal(8, testedSides);
            Assert.Equal("rolled", cph.Globals[UiHost.AnswerVar("Demo Game")]);

            Assert.True(Handle(new Dictionary<string, object> { ["uiframework.request"] = "test", ["uiframework.name"] = "other", ["uiframework.values"] = "" }));
            Assert.Equal("", cph.Globals[UiHost.AnswerVar("Demo Game")]);

            Assert.True(Handle(new Dictionary<string, object> { ["uiframework.request"] = "validate", ["uiframework.values"] = "{\"sides\": 1}" }));
            Assert.Equal("Too few sides.", cph.Globals[UiHost.AnswerVar("Demo Game")]);

            Assert.True(Handle(new Dictionary<string, object> { ["uiframework.request"] = "saved", ["uiframework.values"] = "{\"sides\": 6}" }));
            Assert.True(savedRan);
            // The handler runs with the window's arguments cleared, so an action it runs is not taken for a window request.
            Assert.Equal("", requestWhileSaved);
            Assert.Equal("", cph.Arguments["uiframework.values"]);

            Assert.True(Handle(new Dictionary<string, object> { ["uiframework.request"] = "table", ["uiframework.name"] = "stats" }));
            Assert.Equal("[{\"a\":1}]", cph.Globals[UiHost.AnswerVar("Demo Game")]);

            Assert.True(Handle(new Dictionary<string, object> { ["uiframework.request"] = "emotes" }));
            Assert.Equal("", cph.Globals[UiHost.AnswerVar("Demo Game")]);

            Assert.True(Handle(new Dictionary<string, object> { ["uiframework.request"] = "dance" }));
            Assert.Contains(cph.Log, l => l.StartsWith("warn") && l.Contains("'dance'"));

            Assert.Equal("uiframework.answer._SMG_ - Slot Machine Game", UiHost.AnswerVar("[SMG] - Slot Machine Game "));
        }

        [Fact]
        public void ProblemsInTheSchemaAreCollected()
        {
            var root = JObject.Parse(@"{ ""host"": ""demo"", ""version"": ""v1"", ""settingsFile"": ""x"", ""settings"": [
                { ""key"": ""a"", ""default"": 1, ""kind"": ""knob"" },
                { ""key"": ""b"", ""default"": true, ""requires"": ""nothere"" },
                { ""key"": ""c"", ""default"": ""x"", ""kind"": ""slider"" },
                { ""key"": ""d"", ""default"": 5, ""min"": 9, ""max"": 1 }
            ], ""window"": { ""title"": ""T"", ""pages"": [ { ""id"": ""game"", ""fields"": [""ghost*""] } ],
                ""log"": { ""folder"": ""L"", ""writeLogKey"": ""noSuchSwitch"" }, ""headerPills"": [ { ""key"": ""nope"", ""on"": ""On"" } ],
                ""overrides"": { ""gone"": { ""label"": ""Gone"" } } } }");
            var schema = Schema.Parse(root);
            var spec = Entry.SpecFor(null, schema, TempSettings("problems"));
            Entry.Prepare(spec, schema);
            Assert.Equal(8, spec.Problems.Count);
            Assert.Contains(spec.Problems, p => p.Contains("'a'") && p.Contains("'knob'"));
            Assert.Contains(spec.Problems, p => p.Contains("'b'") && p.Contains("'nothere'"));
            Assert.Contains(spec.Problems, p => p.Contains("'c'") && p.Contains("slider"));
            Assert.Contains(spec.Problems, p => p.Contains("'d'") && p.Contains("minimum"));
            Assert.Contains(spec.Problems, p => p.Contains("'ghost*'"));
            Assert.Contains(spec.Problems, p => p.Contains("'noSuchSwitch'"));
            Assert.Contains(spec.Problems, p => p.Contains("'nope'"));
            Assert.Contains(spec.Problems, p => p.Contains("'gone'"));

            // Resolving again starts the list over.
            Entry.Prepare(spec, schema);
            Assert.Equal(8, spec.Problems.Count);
        }
    }

    /// <summary>Asks the host block for the update notices in an AppDomain of its own, where no UIFramework is loaded.</summary>
    public sealed class FrameworkProbe : MarshalByRefObject
    {
        public string Notices() => UiUpdateNotice.FrameworkType()?.Assembly.Location;
    }
}
