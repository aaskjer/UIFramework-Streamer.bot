using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Newtonsoft.Json.Linq;
using Xunit;

namespace UIFramework.Tests
{
    /// <summary>Fields bound to a dropdown's options: showWhen on a field, a section's showWhen, a dropdown's shows.</summary>
    public class ShowWhenTests
    {
        private static void Sta(System.Action body)
        {
            System.Exception failure = null;
            var thread = new Thread(() => { try { body(); } catch (System.Exception ex) { failure = ex; } });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (failure != null) throw failure;
        }

        private const string Settings = @"
            { ""key"": ""source"", ""default"": ""a"", ""kind"": ""options"", ""page"": ""game"", ""section"": ""Where"",
              ""options"": [ { ""value"": ""a"", ""label"": ""A"" }, { ""value"": ""b"", ""label"": ""B"" }, { ""value"": ""igdb"", ""label"": ""IGDB"" } ],
              ""shows"": { ""a"": [ ""fetchAhead"" ], ""b"": [ ""fetchAhead"", ""bOnly"" ] } },
            { ""key"": ""fetchAhead"", ""default"": 5, ""page"": ""game"", ""section"": ""Where"" },
            { ""key"": ""bOnly"", ""default"": false, ""page"": ""game"", ""section"": ""Where"", ""showWhen"": ""free"" },
            { ""key"": ""free"", ""default"": true, ""page"": ""game"", ""section"": ""Where"" },
            { ""key"": ""notIgdb"", ""default"": """", ""page"": ""game"", ""section"": ""Where"", ""showWhen"": ""source!=igdb"" },
            { ""key"": ""clientId"", ""default"": """", ""page"": ""game"", ""section"": ""IGDB access"" },
            { ""key"": ""secret"", ""default"": """", ""page"": ""game"", ""section"": ""IGDB access"", ""masked"": true }";

        private static WindowSpec Spec(string settings, string sections, string name)
        {
            var schema = Schema.Parse(JObject.Parse(@"{ ""host"": ""demo"", ""version"": ""v1"", ""settingsFile"": ""x"", ""settings"": [ " + settings + @" ],
                ""window"": { ""title"": ""T"", ""pages"": [ { ""id"": ""game"", ""fields"": [""*""] } ], ""sections"": [ " + sections + @" ] } }"));
            var spec = Entry.SpecFor(null, schema, Path.Combine(Path.GetTempPath(), "UIFramework.Tests", name, "settings.json"));
            Entry.Prepare(spec, schema);
            return spec;
        }

        [Fact]
        public void A_condition_takes_several_values_and_several_parts()
        {
            var parts = Requirement.Parse("source=a|b && free && n>=3");
            Assert.Equal(3, parts.Count);
            Assert.Equal(new[] { "a", "b" }, parts[0].Values);
            Assert.Null(parts[1].Op);
            Assert.Equal(">=", parts[2].Op);
            Assert.Empty(Requirement.Parse(null));
        }

        [Fact]
        public void Shows_and_sections_become_conditions_and_bad_names_are_problems()
        {
            var spec = Spec(Settings, @"{ ""name"": ""IGDB access"", ""showWhen"": ""source=igdb"" }", "showwhen");
            Assert.Empty(spec.Problems);
            var byKey = spec.ResolvedSettings.ToDictionary(s => s.Key);
            Assert.Equal("source=a|b", byKey["fetchAhead"].ShowWhen);
            Assert.Equal("free && source=b", byKey["bOnly"].ShowWhen);   // its own condition, and the dropdown's on top
            Assert.Equal("source=igdb", byKey["clientId"].SectionShowWhen);
            Assert.Equal("source=igdb", byKey["secret"].SectionShowWhen);

            var bad = Spec(@"{ ""key"": ""source"", ""default"": ""a"", ""kind"": ""options"", ""options"": [ { ""value"": ""a"", ""label"": ""A"" } ], ""shows"": { ""a"": [ ""ghost"" ] } },
                             { ""key"": ""x"", ""default"": """", ""section"": ""S"", ""showWhen"": ""source=IGBD"" }",
                           @"{ ""name"": ""S"", ""showWhen"": ""nothere=1"" }", "showwhen-bad");
            Assert.Contains(bad.Problems, p => p.Contains("'ghost'"));
            Assert.Contains(bad.Problems, p => p.Contains("no option 'IGBD'"));
            Assert.Contains(bad.Problems, p => p.Contains("'nothere'"));
        }

        [Fact]
        public void The_users_layout_puts_fields_beside_each_other_or_on_a_line_of_their_own()
        {
            string folder = Path.Combine(Path.GetTempPath(), "UIFramework.Tests", "rows-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "uiframework.layout.json"),
                @"{ ""fields"": { ""a"": { ""row"": ""row-a"" }, ""b"": { ""row"": ""row-a"" }, ""c"": { ""row"": """" } } }");
            var schema = Schema.Parse(JObject.Parse(@"{ ""host"": ""demo"", ""version"": ""v1"", ""settingsFile"": ""x"", ""settings"": [
                { ""key"": ""a"", ""default"": """", ""page"": ""game"" }, { ""key"": ""b"", ""default"": """", ""page"": ""game"" },
                { ""key"": ""c"", ""default"": 1, ""page"": ""game"", ""row"": ""declared"" }, { ""key"": ""d"", ""default"": 2, ""page"": ""game"", ""row"": ""declared"" } ],
                ""window"": { ""title"": ""T"", ""pages"": [ { ""id"": ""game"", ""fields"": [""*""] } ] } }"));
            var spec = Entry.SpecFor(null, schema, Path.Combine(folder, "settings.json"));
            Entry.Prepare(spec, schema);
            var byKey = spec.ResolvedSettings.ToDictionary(s => s.Key);
            Assert.Equal("row-a", byKey["a"].Row);
            Assert.Equal("row-a", byKey["b"].Row);
            Assert.Equal("~c", byKey["c"].Row);          // out of the row the game declared, on a line of its own
            Assert.Equal("declared", byKey["d"].Row);
        }

        [Fact]
        public void The_host_block_carries_showWhen_shows_and_sections()
        {
            var s = UiSchema.S("source", "a", kind: "options", options: new[] { "a|A", "igdb|IGDB" }, shows: new[] { "igdb: clientId, secret", "a: fetchAhead" });
            var t = UiSchema.S("notIgdb", "", showWhen: "source!=igdb");
            Assert.Equal(new[] { "igdb: clientId, secret", "a: fetchAhead" }, s.Shows);
            Assert.Equal("source!=igdb", t.ShowWhen);
            var window = new UiWindow("T").Section("IGDB access", "source=igdb").ToJson();
            Assert.Equal("IGDB access", (string)window["sections"][0]["name"]);
            Assert.Equal("source=igdb", (string)window["sections"][0]["showWhen"]);
        }
    }
}
