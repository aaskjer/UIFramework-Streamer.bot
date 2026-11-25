using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Xunit;

namespace UIFramework.Tests
{
    /// <summary>What 2.3.0 adds: a list of two columns stored as one line of text (<c>format: "pairs"</c>), and
    /// Streamer.bot's own log as a page of the window (<c>.StreamerBotLog(...)</c>).</summary>
    public class LogAndPairsTests
    {
        private static void Sta(Action body)
        {
            Exception failure = null;
            var thread = new Thread(() => { try { body(); } catch (Exception ex) { failure = ex; } });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }

        private const string Head = @"{ ""host"": ""demo"", ""version"": ""v1"", ""settings"": [ ";

        private static WindowSpec Prepared(string schemaJson)
        {
            try
            {
                var schema = Schema.Parse(schemaJson);
                var spec = Entry.SpecFor(schema, null);
                Entry.Prepare(spec, schema);
                return spec;
            }
            finally { Text.Use(Conventions.Default.Lang); }
        }

        private static WindowSpec Settings(params string[] settings) => Prepared(Head + string.Join(", ", settings) + " ] }");

        private static WindowSpec Windowed(string window, params string[] settings) =>
            Prepared(Head + string.Join(", ", settings) + @" ], ""window"": " + window + " }");

        private static IEnumerable<T> Find<T>(object node) where T : class
        {
            if (node is T hit) yield return hit;
            if (node is DependencyObject d)
                foreach (var child in LogicalTreeHelper.GetChildren(d))
                    foreach (var found in Find<T>(child))
                        yield return found;
        }

        private static string Temp(string name)
        {
            string folder = Path.Combine(Path.GetTempPath(), "UIFramework.Tests", name + "-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            return folder;
        }

        // ---- a list stored as pairs of text ----------------------------------------------------------------------

        private const string Rewards = @"{ ""key"": ""rewards"", ""default"": ""gold:5,silver:2"", ""kind"": ""list"", ""format"": ""pairs"",
            ""columns"": [ ""name|string|Name"", ""points|int|Points"" ] }";

        private static readonly List<SettingColumn> Columns = new List<SettingColumn>
        {
            new SettingColumn { Key = "name", Kind = "string" }, new SettingColumn { Key = "points", Kind = "int" }
        };

        private static string Flat(JToken rows) => rows.ToString(Formatting.None);

        [Fact]
        public void ThePairsAreReadGenerouslyAndWrittenStrictly()
        {
            // Between commas, semicolons or lines; a key from its value by the first colon or equals sign.
            Assert.Equal(@"[{""name"":""gold"",""points"":5},{""name"":""silver"",""points"":2},{""name"":""iron"",""points"":""""},{""name"":""a"",""points"":""b:c""}]",
                Flat(PairList.Rows(" gold : 5 ; silver=2\niron,, a:b:c ", Columns)));
            Assert.Equal("[]", Flat(PairList.Rows("", Columns)));
            Assert.Equal("[]", Flat(PairList.Rows((JToken)null, Columns)));
            // Rows a file holds already stay rows.
            Assert.Equal(@"[{""name"":""x"",""points"":1}]", Flat(PairList.Rows(JArray.Parse(@"[ { ""name"": ""x"", ""points"": 1 } ]"), Columns)));

            // One line: key:value between commas; a key alone has no colon, an empty row is left out.
            Assert.Equal("gold:5,silver:2,iron", PairList.Text(JArray.Parse(@"[ { ""name"": ""gold"", ""points"": 5 }, { ""name"": "" silver "", ""points"": ""2"" },
                { ""name"": """", ""points"": """" }, { ""name"": ""iron"" } ]"), Columns));
            // The format has no escaping: what would part the pairs anew is written as a blank.
            Assert.Equal("a b:1 2", PairList.Text(JArray.Parse(@"[ { ""name"": ""a,b"", ""points"": ""1;2"" } ]"), Columns));
            Assert.Equal("a b:c:d", PairList.Text(JArray.Parse(@"[ { ""name"": ""a=b"", ""points"": ""c:d"" } ]"), Columns));
        }

        [Fact]
        public void APairListsDefaultAndStoredTextBecomeRowsForTheWindow()
        {
            var spec = Settings(Rewards);
            Assert.Empty(spec.Problems);
            Assert.Equal(@"[{""name"":""gold"",""points"":5},{""name"":""silver"",""points"":2}]", Flat(spec.Defaults["rewards"]));
            Assert.True(PairList.Applies(spec.ResolvedSettings.Single()));

            // Read: the text of the file is rows from there on. Written: the line of text again.
            var settings = JObject.Parse(@"{ ""rewards"": ""wood=1; stone:3"", ""other"": ""a:b"" }");
            Sidecars.Load(settings, spec.ResolvedSettings, Path.Combine(Temp("pairs"), "settings.json"), null, "");
            Assert.Equal(@"[{""name"":""wood"",""points"":1},{""name"":""stone"",""points"":3}]", Flat(settings["rewards"]));
            Assert.Equal("a:b", (string)settings["other"]);                 // no setting of that format: as it is
            var written = Sidecars.Split(settings, spec.ResolvedSettings, Path.Combine(Temp("pairs"), "settings.json"));
            Assert.Equal("wood:1,stone:3", (string)written["rewards"]);
            Assert.IsType<JArray>(settings["rewards"]);                     // the window keeps its rows
        }

        [Fact]
        public void APairListInAFileOfItsOwnIsThatLineOfTextThereToo()
        {
            string folder = Temp("pairs-file");
            string path = Path.Combine(folder, "settings.json");
            var spec = Settings(@"{ ""key"": ""rewards"", ""default"": """", ""kind"": ""list"", ""format"": ""pairs"", ""file"": ""rewards.json"", ""field"": ""map"",
                ""columns"": [ ""name|string"", ""points|int"" ] }");
            Assert.Empty(spec.Problems);
            File.WriteAllText(Path.Combine(folder, "rewards.json"), @"{ ""map"": ""gold:5"", ""kept"": true }");
            var settings = new JObject();
            Sidecars.Load(settings, spec.ResolvedSettings, path, null, "");
            Assert.Equal(@"[{""name"":""gold"",""points"":5}]", Flat(settings["rewards"]));
            ((JArray)settings["rewards"]).Add(new JObject { ["name"] = "silver", ["points"] = 2 });
            var main = Sidecars.Split(settings, spec.ResolvedSettings, path);
            Assert.Null(main["rewards"]);
            var file = JObject.Parse(File.ReadAllText(Path.Combine(folder, "rewards.json")));
            Assert.Equal("gold:5,silver:2", (string)file["map"]);
            Assert.True((bool)file["kept"]);
        }

        [Fact]
        public void APairListIsEditedAsRowsWhoseCellsDoNotTakeWhatPartsThePairs() => Sta(() =>
        {
            var spec = Settings(Rewards);
            var parts = new WindowParts();
            var stored = (JObject)spec.Defaults.DeepClone();
            var ctx = Entry.ContextFor(spec, stored, parts, null, null);
            var field = SchemaPage.Render(ctx, spec.ResolvedSettings.Single());
            foreach (var wire in ctx.AfterBuild) wire();
            var editor = parts.Lists["rewards"];
            Assert.Equal(@"[{""name"":""gold"",""points"":5},{""name"":""silver"",""points"":2}]", Flat(editor.Value));

            var boxes = Find<TextBox>(field).Where(b => b.IsVisible || true).ToList();
            var name = boxes.First(b => b.Text == "gold");
            name.Text = "go,ld:=;x";
            Assert.Equal("goldx", name.Text);                               // a key takes none of , ; : =
            var points = boxes.First(b => b.Text == "5");
            Assert.Equal(PairList.ValueForbidden, ",;");

            // What the project hands over as its text fills the rows.
            Assert.True(LiveValues.Set(parts, "rewards", "wood:1; stone=3"));
            Assert.Equal(@"[{""name"":""wood"",""points"":1},{""name"":""stone"",""points"":3}]", Flat(editor.Value));
            Assert.True(LiveValues.Set(parts, "rewards", JArray.Parse(@"[ { ""name"": ""x"", ""points"": 9 } ]")));
            Assert.Equal(@"[{""name"":""x"",""points"":9}]", Flat(editor.Value));
        });

        [Fact]
        public void TheProjectIsHandedAPairListAsItIsStored()
        {
            var schema = Schema.Parse(Head + Rewards + " ] }");
            var live = JObject.Parse(@"{ ""rewards"": [ { ""name"": ""gold"", ""points"": 5 }, { ""name"": ""iron"" } ], ""n"": 1 }");
            var handed = Globals.Masked(live, schema);
            Assert.Equal("gold:5,iron", (string)handed["rewards"]);
            Assert.Equal(1, (int)handed["n"]);
            Assert.IsType<JArray>(live["rewards"]);                         // a copy: the window's own values stay rows
            // The host is wrapped for it, as for a secret.
            var spec = Prepared(Head + Rewards + " ] }");
            Assert.IsNotType<NoHooks>(Globals.Guard(new NoHooks(), spec));
        }

        [Fact]
        public void APairListsDeclarationIsChecked()
        {
            const string two = @"""columns"": [ ""name|string"", ""points|int"" ]";
            Assert.Contains(Settings(@"{ ""key"": ""a"", ""default"": """", ""format"": ""pairs"" }").Problems, p => p.Contains("'a'") && p.Contains("format") && p.Contains("only a list"));
            Assert.Contains(Settings(@"{ ""key"": ""a"", ""default"": [], ""kind"": ""list"", ""format"": ""csv"", " + two + " }").Problems, p => p.Contains("'csv'") && p.Contains("pairs"));
            Assert.Contains(Settings(@"{ ""key"": ""a"", ""default"": [], ""kind"": ""list"", ""format"": ""pairs"", ""columns"": [ ""a"", ""b"", ""c"" ] }").Problems,
                p => p.Contains("two columns") && p.Contains("it has 3"));
            Assert.Contains(Settings(@"{ ""key"": ""a"", ""default"": [], ""kind"": ""list"", ""format"": ""pairs"", ""columns"": [ ""a|string"", ""b|bool"" ] }").Problems,
                p => p.Contains("'b'") && p.Contains("a switch"));
            Assert.Contains(Settings(@"{ ""key"": ""a"", ""default"": [], ""kind"": ""list"", ""format"": ""pairs"", ""rowKey"": ""name"", " + two + " }").Problems,
                p => p.Contains("rowKey") && p.Contains("pairs"));
            // Declared as rows instead of the text: fine, and the same rows.
            var asRows = Settings(@"{ ""key"": ""a"", ""default"": [ { ""name"": ""gold"", ""points"": 5 } ], ""kind"": ""list"", ""format"": ""PAIRS"", " + two + " }");
            Assert.Empty(asRows.Problems);
            Assert.Equal(@"[{""name"":""gold"",""points"":5}]", Flat(asRows.Defaults["a"]));
        }

        [Fact]
        public void TheHostBlockDeclaresAndReadsAPairList()
        {
            var declared = UiSchema.ToJson(UiSchema.S("rewards", "gold:5", kind: "list", columns: new[] { "name|string", "points|int" }, format: "pairs"));
            Assert.Equal("pairs", (string)declared["format"]);
            Assert.Null(UiSchema.ToJson(UiSchema.S("rows", new string[0], kind: "list", columns: new[] { "a" }))["format"]);
            Assert.Equal("pairs", Setting.Parse(Setting.Parse(declared).ToJson()).Format);

            string Read(List<KeyValuePair<string, string>> pairs) => string.Join(" | ", pairs.Select(p => p.Key + "=" + p.Value));
            var settings = UiSettings.Parse(@"{ ""rewards"": "" gold : 5 ; silver=2\niron,, a:b:c "", ""rows"": [ { ""name"": ""x"", ""points"": 9 }, { ""name"": """" }, 3 ],
                ""empty"": """", ""number"": 7 }");
            Assert.Equal("gold=5 | silver=2 | iron= | a=b:c", Read(settings.Pairs("rewards")));
            Assert.Equal("x=9", Read(settings.Pairs("rows")));               // rows in the file: their first two cells
            Assert.Empty(settings.Pairs("empty"));
            Assert.Empty(settings.Pairs("notThere"));
            Assert.Equal("7=", Read(settings.Pairs("number")));

            // The reader of an action without the table reads the same, and takes a fallback.
            var reader = UiReaderSettings.Parse(@"{ ""rewards"": ""gold:5,silver=2"" }");
            Assert.Equal("gold=5 | silver=2", Read(reader.Pairs("rewards")));
            Assert.Equal("a=1", Read(reader.Pairs("notThere", "a:1")));
            Assert.Empty(reader.Pairs("notThere"));
        }

        // ---- Streamer.bot's log -----------------------------------------------------------------------------------

        [Fact]
        public void ALineOfStreamerBotsLogIsReadWithItsLevelAndSource()
        {
            var own = StreamerBotLog.Parse("[2026-11-07 11:48:27.383 WRN] TokenManager :: Streamer.bot Website requested an access token, but no credentials exist.");
            Assert.Equal(new DateTime(2026, 11, 7, 11, 48, 27, 383), own.Time);
            Assert.Equal("WRN", own.Level);
            Assert.Equal("TokenManager", own.Source);
            Assert.Equal("Streamer.bot Website requested an access token, but no credentials exist.", own.Text);

            // A project's line: what it puts in brackets in front is its source.
            var project = StreamerBotLog.Parse("[2026-11-07 12:25:10.945 INF] [Demo Game/Settings] Settings described for the window.");
            Assert.Equal("Demo Game/Settings", project.Source);
            Assert.Equal("Settings described for the window.", project.Text);

            var plain = StreamerBotLog.Parse("[2026-11-07 11:48:24 INF] Streamer.bot (1.0.7)");
            Assert.Equal("", plain.Source);
            Assert.Equal("Streamer.bot (1.0.7)", plain.Text);
            Assert.Equal(new DateTime(2026, 11, 7, 11, 48, 24), plain.Time);

            Assert.Null(StreamerBotLog.Parse("   at System.Net.Http.HttpClient.Send()"));   // a line that follows another
            Assert.Null(StreamerBotLog.Parse(""));

            Assert.Equal(PillStyle.Danger, StreamerBotLogPage.StyleOf("ERR"));
            Assert.Equal(PillStyle.Warning, StreamerBotLogPage.StyleOf("wrn"));
            Assert.Equal(PillStyle.Muted, StreamerBotLogPage.StyleOf("DBG"));
            Assert.Equal(PillStyle.None, StreamerBotLogPage.StyleOf("INF"));
        }

        private const string Day7 =
            "[2026-11-07 11:48:24.048 INF] Streamer.bot (1.0.7)\r\n" +
            "[2026-11-07 12:24:46.643 ERR] [Demo Game/Settings] The window cannot open.\r\n" +
            "   at Demo.Open()\r\n" +
            "[2026-11-07 12:24:47.000 DBG] Twitch :: something else\r\n" +
            "   not of the project\r\n" +
            "[2026-11-07 12:25:10.945 INF] [Demo Game] Settings described for the window.\r\n";

        [Fact]
        public void TheReaderTakesTheProjectsLinesAndOnlyWhatIsNewAfterThat()
        {
            string folder = Temp("sblog");
            string file = Path.Combine(folder, "log_20261107.log");
            File.WriteAllText(file, Day7 + "[2026-11-07 12:26:00.000 WRN] [Demo Game] half a li");
            var reader = new StreamerBotLog.Reader(file, new[] { "[demo game" });   // whatever the case
            var read = new List<SbLogLine>();
            Assert.True(reader.ReadNew(read));
            Assert.Equal(new[] { "ERR", "INF" }, read.Select(l => l.Level).ToArray());
            Assert.Equal("The window cannot open.\n   at Demo.Open()", read[0].Text);       // its following line goes with it
            Assert.EndsWith("   at Demo.Open()", read[0].Raw);
            Assert.DoesNotContain(read, l => l.Text.Contains("not of the project"));

            // The line Streamer.bot was in the middle of is whole now; one more follows.
            File.AppendAllText(file, "ne\r\n[2026-11-07 12:27:00.000 INF] [Demo Game] later\r\n");
            var more = new List<SbLogLine>();
            Assert.True(reader.ReadNew(more));
            Assert.Equal(new[] { "half a line", "later" }, more.Select(l => l.Text).ToArray());
            var none = new List<SbLogLine>();
            Assert.True(reader.ReadNew(none));
            Assert.Empty(none);

            // Written anew, shorter: the caller starts over.
            File.WriteAllText(file, "[2026-11-07 13:00:00.000 INF] [Demo Game] new\r\n");
            Assert.False(reader.ReadNew(none));

            // Without a text to look for, every line; a file that is not there yet reads as empty.
            var all = new List<SbLogLine>();
            Assert.True(new StreamerBotLog.Reader(file, new string[0]).ReadNew(all));
            Assert.Single(all);
            Assert.True(new StreamerBotLog.Reader(Path.Combine(folder, "log_20990101.log"), null).ReadNew(all));
            Assert.Single(all);
        }

        [Fact]
        public void TheDaysAreTheLogFilesNewestFirst()
        {
            string folder = Temp("sblog-days");
            foreach (string name in new[] { "log_20261107.log", "log_20261109.log", "log_20261108.log", "other.log", "log_2026.log", "log_20261107.txt" })
                File.WriteAllText(Path.Combine(folder, name), "");
            Assert.Equal(new[] { "2026-11-09", "2026-11-08", "2026-11-07" }, StreamerBotLog.Days(folder).ToArray());
            Assert.Empty(StreamerBotLog.Days(Path.Combine(folder, "nothere")));
            Assert.Equal(Path.Combine(folder, "log_20261108.log"), StreamerBotLog.FileOf(folder, "2026-11-08"));
        }

        [Fact]
        public void TheWindowDeclaresStreamerBotsLogAndGetsItsPage()
        {
            var spec = Windowed(@"{ ""title"": ""T"", ""streamerBotLog"": { ""match"": [ ""[Demo Game"", ""'Demo'"" ] } }", @"{ ""key"": ""a"", ""default"": 1 }");
            Assert.Empty(spec.Problems);
            Assert.True(spec.SbLog.Declared);
            Assert.Equal(new[] { "[Demo Game", "'Demo'" }, spec.SbLog.Match.ToArray());
            var page = spec.Pages.Single(p => p.Id == "sblog");
            Assert.Equal("Streamer.bot log", page.Title);
            Assert.True(page.Bottom);
            Assert.True(spec.Pages.FindIndex(p => p.Id == "sblog") < spec.Pages.FindIndex(p => p.Id == "about"));   // above About

            // The texts alone, or every line; no declaration, no page.
            Assert.Equal(new[] { "[Demo" }, Windowed(@"{ ""title"": ""T"", ""streamerBotLog"": [ ""[Demo"" ] }").SbLog.Match.ToArray());
            var every = Windowed(@"{ ""title"": ""T"", ""streamerBotLog"": true }");
            Assert.True(every.SbLog.Declared);
            Assert.Empty(every.SbLog.Match);
            Assert.DoesNotContain(Windowed(@"{ ""title"": ""T"" }", @"{ ""key"": ""a"", ""default"": 1 }").Pages, p => p.Id == "sblog");
            // Named among the pages without the declaration: said, and left out.
            var named = Windowed(@"{ ""title"": ""T"", ""pages"": [ { ""id"": ""game"", ""title"": ""Game"" }, ""sblog"" ] }", @"{ ""key"": ""a"", ""default"": 1, ""page"": ""game"" }");
            Assert.Contains(named.Problems, p => p.Contains("'sblog'") && p.Contains("StreamerBotLog"));
            Assert.DoesNotContain(named.Pages, p => p.Id == "sblog");
            // With the declaration, naming it gives it a title of its own.
            var titled = Windowed(@"{ ""title"": ""T"", ""streamerBotLog"": true, ""pages"": [ { ""id"": ""sblog"", ""title"": ""What happened"" } ] }");
            Assert.Equal("What happened", titled.Pages.Single(p => p.Id == "sblog").Title);

            // From the host block.
            var json = new UiWindow("Demo").StreamerBotLog("[Demo Game", "", null, "'Demo'").ToJson();
            Assert.Equal(@"{""match"":[""[Demo Game"",""'Demo'""]}", json["streamerBotLog"].ToString(Formatting.None));
            Assert.Null(new UiWindow("Demo").ToJson()["streamerBotLog"]);
            Assert.Equal(new[] { "[Demo Game", "'Demo'" }, SpecJson.Parse(json).SbLog.Match.ToArray());
        }

        [Fact]
        public void ThePageShowsADaysLinesNewestFirstAndFiltersThem() => Sta(() =>
        {
            string folder = Temp("sblog-page");
            File.WriteAllText(Path.Combine(folder, "log_20261107.log"), Day7);
            File.WriteAllText(Path.Combine(folder, "log_20261106.log"), "[2026-11-06 09:00:00.000 WRN] [Demo Game] the day before\r\n");
            var spec = Windowed(@"{ ""title"": ""T"", ""streamerBotLog"": [ ""[Demo Game"" ] }", @"{ ""key"": ""a"", ""default"": 1 }");
            spec.SbLog.Folder = folder;
            var ctx = Entry.ContextFor(spec, (JObject)spec.Defaults.DeepClone(), new WindowParts(), null, null);
            var page = StreamerBotLogPage.Build(ctx);
            var grid = Find<DataGrid>(page).Single();
            var combos = Find<ComboBox>(page).ToList();   // level, source, day
            List<string> Shown() => grid.Items.OfType<SbLogLine>().Select(l => l.Level + " " + l.Source).ToList();

            Assert.Equal(new[] { "INF Demo Game", "ERR Demo Game/Settings" }, Shown());              // the newest day, newest first
            Assert.Equal(new[] { "2026-11-07", "2026-11-06" }, combos[2].Items.OfType<string>().ToArray());
            Assert.Equal("2026-11-07", combos[2].SelectedItem);
            Assert.Equal(new[] { "All levels", "ERR", "INF" }, combos[0].Items.OfType<string>().ToArray());   // errors first
            Assert.Equal(new[] { "All sources", "Demo Game", "Demo Game/Settings" }, combos[1].Items.OfType<string>().ToArray());

            combos[0].SelectedItem = "ERR";
            Assert.Equal(new[] { "ERR Demo Game/Settings" }, Shown());
            combos[0].SelectedItem = "All levels";
            combos[1].SelectedItem = "Demo Game";
            Assert.Equal(new[] { "INF Demo Game" }, Shown());
            combos[1].SelectedItem = "All sources";
            var search = Find<TextBox>(page).First(b => !b.IsReadOnly);
            search.Text = "cannot OPEN";
            Assert.Equal(new[] { "ERR Demo Game/Settings" }, Shown());
            search.Text = "";

            // The picked entry's whole text stands under the list.
            grid.SelectedItem = grid.Items.OfType<SbLogLine>().First(l => l.Level == "ERR");
            Assert.Contains("   at Demo.Open()", Find<TextBox>(page).Single(b => b.IsReadOnly).Text);

            // Another day.
            combos[2].SelectedItem = "2026-11-06";
            Assert.Equal(new[] { "WRN Demo Game" }, Shown());
            Assert.Equal(new[] { "All levels", "WRN" }, combos[0].Items.OfType<string>().ToArray());
        });

        [Fact]
        public void ThePageSaysSoWhenThereIsNoLogOrNoLineOfTheProject() => Sta(() =>
        {
            string Empty(string folder, string match)
            {
                var spec = Windowed(@"{ ""title"": ""T"", ""streamerBotLog"": [ """ + match + @""" ] }", @"{ ""key"": ""a"", ""default"": 1 }");
                spec.SbLog.Folder = folder;
                var page = StreamerBotLogPage.Build(Entry.ContextFor(spec, (JObject)spec.Defaults.DeepClone(), new WindowParts(), null, null));
                return string.Join(" | ", Find<TextBlock>(page).Where(t => t.TextAlignment == TextAlignment.Center && t.Visibility == Visibility.Visible).Select(t => t.Text));
            }
            Assert.Equal("Streamer.bot has written no log yet.", Empty(Path.Combine(Temp("sblog-none"), "logs"), "[Demo"));
            string folder = Temp("sblog-other");
            File.WriteAllText(Path.Combine(folder, "log_20261107.log"), Day7);
            Assert.Equal("No line of this project on that day.", Empty(folder, "[Another Project"));
        });
    }
}
