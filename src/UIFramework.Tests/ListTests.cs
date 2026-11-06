using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Controls;
using Newtonsoft.Json.Linq;
using Xunit;

namespace UIFramework.Tests
{
    /// <summary>The list kind: its editor binds like any field, and a list with a file of its own stays out of
    /// the settings file.</summary>
    public class ListTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "UIFramework.Tests", Guid.NewGuid().ToString("N"));
        private string SettingsPath => Path.Combine(_folder, "settings.json");

        public ListTests() { Directory.CreateDirectory(_folder); }
        public void Dispose() { try { Directory.Delete(_folder, true); } catch { } }

        private static void Sta(Action body)
        {
            Exception failure = null;
            var thread = new Thread(() => { try { body(); } catch (Exception ex) { failure = ex; } });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (failure != null) throw failure;
        }

        private static readonly List<SettingColumn> Columns = new List<SettingColumn>
        {
            new SettingColumn { Key = "Word", Kind = "string", Label = "Word" },
            new SettingColumn { Key = "Rhymes", Kind = "text", Label = "Rhymes" },
            new SettingColumn { Key = "Weight", Kind = "int", Label = "Weight" },
            new SettingColumn { Key = "On", Kind = "bool", Label = "On" }
        };

        [Fact]
        public void EmoteColumnsReadAndWriteLikeText() => Sta(() =>
        {
            var columns = new List<SettingColumn>
            {
                new SettingColumn { Key = "Code", Kind = "emote", Label = "Emote", Platform = "kick" },
                new SettingColumn { Key = "Weight", Kind = "int", Label = "Weight" }
            };
            var editor = new ListEditor("emotes", "Kick", columns, JArray.Parse(@"[ { ""Code"": ""[emote:1730803:emojiRich]"", ""Weight"": 3 } ]"), null, "");
            Assert.Equal("[emote:1730803:emojiRich]", (string)editor.Value[0]["Code"]);
            Assert.True(EmotePictures.KickCode.IsMatch("[emote:1730803:emojiRich]"));
            Assert.False(EmotePictures.KickCode.IsMatch("Kappa"));
            editor.Set(JArray.Parse(@"[ { ""Code"": ""[emote:1:x]"", ""Weight"": 1 }, { ""Code"": "" "" } ]"));
            Assert.Single(editor.Value);                       // a row with nothing typed in is dropped
        });

        [Fact]
        public void EditorRoundTripsRowsDropsEmptyOnesAndBinds() => Sta(() =>
        {
            var defaults = JObject.Parse(@"{ ""words"": [ { ""Word"": ""CAT"", ""Rhymes"": ""BAT,HAT"", ""Weight"": 1, ""On"": true } ] }");
            var value = JArray.Parse(@"[ { ""Word"": ""STAR"", ""Rhymes"": ""CAR"", ""Weight"": 2, ""On"": false }, { ""Word"": """", ""Rhymes"": """" } ]");
            var editor = new ListEditor("words", "Words", Columns, value, (JArray)defaults["words"], "");

            // The empty second row is not part of the value.
            Assert.Single(editor.Value);
            Assert.Equal("STAR", (string)editor.Value[0]["Word"]);
            Assert.Equal(2, (int)editor.Value[0]["Weight"]);
            Assert.False((bool)editor.Value[0]["On"]);

            var page = new StackPanel();
            page.Children.Add(editor);
            var baseline = new Dictionary<object, string>();
            FieldBinding.Snapshot(page, baseline);
            int changes = 0;
            FieldBinding.HookChangeTracking(page, () => changes++);

            // A list with a text column is a table edited in place: a cell takes a new value, which changes the
            // list's value and counts as one changed field.
            DataGrid grid = null;
            FieldBinding.VisitTree(page, n => { if (n is DataGrid g) grid = g; });
            Assert.NotNull(grid);
            var row = grid.Items[0];
            row.GetType().GetProperty("Item").SetValue(row, "CAR, FAR", new object[] { "Rhymes" });
            Assert.True(changes > 0);
            Assert.Equal(1, FieldBinding.CountChanged(page, baseline));

            var settings = new JObject();
            FieldBinding.Save(settings, page);
            Assert.Equal("CAR, FAR", (string)settings["words"][0]["Rhymes"]);

            // Reset puts the defaults back, and the change count follows.
            FieldBinding.ResetToDefaults(page, defaults);
            Assert.Equal("CAT", (string)editor.Value[0]["Word"]);
            FieldBinding.Save(settings, page);
            Assert.Equal("BAT,HAT", (string)settings["words"][0]["Rhymes"]);
        });

        [Fact]
        public void SidecarLoadsFromItsFileAndSaveKeepsItOutOfTheSettingsFile()
        {
            var schema = Schema.Parse(@"{ ""host"": ""demo"", ""settings"": [
                { ""key"": ""minBet"", ""default"": 10 },
                { ""key"": ""wordsEasy"", ""default"": [ { ""Word"": ""CAT"", ""Rhymes"": ""BAT"" } ], ""kind"": ""list"", ""file"": ""RT-wordbank.json"", ""field"": ""Easy"",
                  ""columns"": [ { ""key"": ""Word"" }, { ""key"": ""Rhymes"", ""kind"": ""text"" } ] },
                { ""key"": ""wordsHard"", ""default"": [], ""kind"": ""list"", ""file"": ""RT-wordbank.json"", ""field"": ""Hard"", ""columns"": [ { ""key"": ""Word"" } ] } ] }");
            Assert.Equal("RT-wordbank.json", schema.Find("wordsEasy").File);
            Assert.Equal("Easy", schema.Find("wordsEasy").Field);
            Assert.Equal("text", schema.Find("wordsEasy").Columns[1].Kind);

            string bank = Path.Combine(_folder, "RT-wordbank.json");
            File.WriteAllText(bank, @"{ ""Easy"": [ { ""Word"": ""STAR"", ""Rhymes"": ""CAR"" } ], ""Medium"": [ { ""Word"": ""KEPT"" } ] }");

            var store = new SettingsStore(SettingsPath, schema.Defaults());
            var settings = store.Load().Settings;
            Sidecars.Load(settings, schema.Settings, SettingsPath, null, "[t]");
            Assert.Equal("STAR", (string)settings["wordsEasy"][0]["Word"]);   // from the file
            Assert.Empty((JArray)settings["wordsHard"]);                      // not in the file: the default

            settings["wordsHard"] = JArray.Parse(@"[ { ""Word"": ""ALGORITHM"" } ]");
            settings["minBet"] = 25;
            store.Save(Sidecars.Split(settings, schema.Settings, SettingsPath));

            var main = JObject.Parse(File.ReadAllText(SettingsPath));
            Assert.Equal(25, (int)main["minBet"]);
            Assert.Null(main["wordsEasy"]);
            Assert.Null(main["wordsHard"]);
            var written = JObject.Parse(File.ReadAllText(bank));
            Assert.Equal("STAR", (string)written["Easy"][0]["Word"]);
            Assert.Equal("ALGORITHM", (string)written["Hard"][0]["Word"]);
            Assert.Equal("KEPT", (string)written["Medium"][0]["Word"]);       // untouched property survives
            Assert.True(File.Exists(bank + ".bak"));
        }

        [Fact]
        public void StripLeavesTheSidecarFilesAlone()
        {
            var schema = Schema.Parse(@"{ ""host"": ""demo"", ""settings"": [
                { ""key"": ""a"", ""default"": 1 },
                { ""key"": ""rows"", ""default"": [], ""kind"": ""list"", ""file"": ""rows.json"", ""columns"": [ { ""key"": ""q"" } ] } ] }");
            var settings = schema.Defaults();
            var main = Sidecars.Strip(settings, schema.Settings);
            Assert.Equal(1, (int)main["a"]);
            Assert.Null(main["rows"]);
            Assert.NotNull(settings["rows"]);                       // the original is untouched
            Assert.False(File.Exists(Path.Combine(_folder, "rows.json")));
        }

        [Fact]
        public void SidecarWithoutFieldIsTheWholeFile()
        {
            var schema = Schema.Parse(@"{ ""host"": ""demo"", ""settings"": [
                { ""key"": ""questions"", ""default"": [], ""kind"": ""list"", ""file"": ""questions.json"", ""columns"": [ { ""key"": ""q"" } ] } ] }");
            var settings = schema.Defaults();
            settings["questions"] = JArray.Parse(@"[ { ""q"": ""why"" } ]");
            var main = Sidecars.Split(settings, schema.Settings, SettingsPath);
            Assert.Null(main["questions"]);
            var written = JArray.Parse(File.ReadAllText(Path.Combine(_folder, "questions.json")));
            Assert.Equal("why", (string)written[0]["q"]);

            var again = schema.Defaults();
            Sidecars.Load(again, schema.Settings, SettingsPath, null, "[t]");
            Assert.Equal("why", (string)again["questions"][0]["q"]);
        }
    }
}
