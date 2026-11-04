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
                .About(credits: "By someone", links: new[] { UiSchema.Link("Discord", "https://discord.test") }, authorLink: false);
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
            Assert.Equal(new[] { "game", "messages", "log", "help", "about" }, spec.Pages.Select(p => p.Id).ToArray());
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
            Assert.False(spec.About.ShowAuthorLink);
            Assert.Equal("Discord", spec.About.Links[0].Text);
            Assert.Empty(spec.Problems);

            // A spec the stub carries wins over the window in the schema; a window without a title is called after the host.
            Assert.Equal("Other", Entry.SpecFor(@"{ ""title"": ""Other"" }", schema, TempSettings("builder")).Title);
            var untitled = Schema.Parse(SchemaWith(new JObject { ["pages"] = new JArray("about") }));
            Assert.Equal("Demo", Entry.SpecFor("", untitled, TempSettings("builder")).Title);
        }

        [Fact]
        public void TheBlockAnswersTheStubsRequests()
        {
            var cph = new FakeCph();
            string tested = null; int testedSides = 0; bool savedRan = false;
            bool Handle(Dictionary<string, object> args) => UiHost.Handle(args, cph, "Demo Game", "demo", "v1.0.0", "Demo/settings.json",
                test: (name, s) => { tested = name; testedSides = s.Int("sides"); return name == "tryRoll" ? "rolled" : null; },
                validate: s => s.Int("sides") < 2 ? "Too few sides." : null,
                saved: s => savedRan = true,
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
}
