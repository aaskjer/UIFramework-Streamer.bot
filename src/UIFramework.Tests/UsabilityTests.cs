using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace UIFramework.Tests
{
    /// <summary>What a game's author meets with keys and kinds of their own, and a language the framework did not
    /// ship first.</summary>
    public class UsabilityTests
    {
        private static string RepoRoot()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "UIFramework.slnx")))
                dir = dir.Parent;
            return dir?.FullName ?? throw new InvalidOperationException("repo root not found");
        }

        private static WindowSpec Prepared(string schemaJson, string language)
        {
            var schema = Schema.Parse(schemaJson);
            var spec = Entry.SpecFor(null, schema, Path.GetTempFileName());
            spec.Language = language;
            Entry.Prepare(spec, schema);
            return spec;
        }

        [Fact]
        public void The_games_schemas_resolve_without_problems_in_every_language()
        {
            try
            {
                var problems = (from file in Directory.GetFiles(Path.Combine(RepoRoot(), "hosts"), "schema.json", SearchOption.AllDirectories)
                                from code in Conventions.Available()
                                from problem in Prepared(File.ReadAllText(file), code).Problems
                                select Path.GetFileName(Path.GetDirectoryName(file)) + " " + code + ": " + problem).ToList();
                Assert.True(problems.Count == 0, string.Join("\n", problems));
            }
            finally { Text.Use(Conventions.Default.Lang); }
        }

        private const string Custom = @"{ ""host"": ""custom"", ""version"": ""v0.1.0"", ""settings"": [
            { ""key"": ""spinSpeed"", ""default"": 5, ""page"": ""extras"", ""section"": ""Reels"" },
            { ""key"": ""maxBet"", ""default"": 3, ""page"": ""extras"", ""section"": ""Bonus"", ""label"": ""Max spins per round"" },
            { ""key"": ""minBet"", ""default"": ""low"", ""kind"": ""options"", ""page"": ""extras"", ""section"": ""Bonus"",
              ""options"": [ { ""value"": ""low"", ""label"": ""Low"" } ] },
            { ""key"": ""reelTheme"", ""default"": ""fruit"", ""page"": ""extras"", ""section"": ""Reels"" },
            { ""key"": ""wheel"", ""default"": """", ""kind"": ""wheel"", ""page"": ""other"" } ],
          ""lang"": { ""es"": { ""keys"": { ""spinSpeed"": ""Velocidad de giro"" } } },
          ""window"": { ""title"": ""Custom"", ""pages"": [ { ""id"": ""extras"", ""title"": ""Extras"", ""icon"": ""slots"" },
                                                       { ""id"": ""other"", ""title"": ""Other"", ""icon"": ""🎰"" }, ""about"" ] } }";

        [Fact]
        public void A_games_own_keys_and_kinds_are_shown_and_what_does_not_fit_is_said()
        {
            try
            {
                var spec = Prepared(Custom, "en");
                Assert.Contains(spec.Problems, p => p.Contains("'wheel' has the kind 'wheel'"));
                Assert.Contains(spec.Problems, p => p.StartsWith("'minBet' is a known key of the conventions") && p.Contains("'options'"));
                Assert.DoesNotContain(spec.Problems, p => p.StartsWith("'maxBet'"));               // the same kind of value: fine
                Assert.Contains(spec.Problems, p => p.Contains("The section 'Reels' on the page 'extras' is split"));
                Assert.Contains(spec.Problems, p => p.Contains("the icon 'slots'"));
                Assert.DoesNotContain(spec.Problems, p => p.Contains("🎰"));                        // an emoji is an emoji
                Assert.Equal("•", spec.Pages.First(p => p.Id == "extras").Glyph);
                Assert.Equal("🎰", spec.Pages.First(p => p.Id == "other").Glyph);
                Assert.Equal("Spin speed", spec.ResolvedSettings.First(s => s.Key == "spinSpeed").Label);
            }
            finally { Text.Use(Conventions.Default.Lang); }
        }

        [Fact]
        public void In_a_language_the_game_relabelled_key_keeps_its_wording_unless_the_conventions_translate_it()
        {
            try
            {
                // Spanish has a conventions file: the key's Spanish text is a translation and stands.
                var es = Prepared(Custom, "es");
                Assert.Equal("Velocidad de giro", es.ResolvedSettings.First(s => s.Key == "spinSpeed").Label);
                Assert.NotEqual("Maximum bet", es.ResolvedSettings.First(s => s.Key == "maxBet").Label);
                // A language with nothing but the game's own texts: the game's English stays, never the conventions' English.
                var nl = Prepared(Custom.Replace(@"""es"": {", @"""nl"": {"), "nl");
                Assert.Equal("Max spins per round", nl.ResolvedSettings.First(s => s.Key == "maxBet").Label);
                Assert.Equal("Velocidad de giro", nl.ResolvedSettings.First(s => s.Key == "spinSpeed").Label);
                Assert.Contains("nl", nl.Languages);
            }
            finally { Text.Use(Conventions.Default.Lang); }
        }

        [Fact]
        public void A_game_can_translate_the_windows_own_texts_for_a_language_the_framework_lacks()
        {
            Assert.Equal("ui", UiSchema.Ui("btn.save", "Opslaan").Section);
            Assert.Equal("placeholders", UiSchema.Ph("user", "Wie").Section);
            var schema = JObject.Parse(Custom);
            schema["lang"] = new JObject { ["nl"] = new JObject { ["ui"] = new JObject { ["btn.save"] = "Opslaan" }, ["placeholders"] = new JObject { ["user"] = "Wie er speelt" } } };
            try
            {
                var spec = Prepared(schema.ToString(), "nl");
                Assert.Equal("Opslaan", Text.T("btn.save"));
                Assert.Equal("Wie er speelt", spec.Conventions.Lang.Placeholder("user"));
                Assert.Equal("Cancel", Text.T("btn.cancel"));   // what the game did not translate stays English
            }
            finally { Text.Use(Conventions.Default.Lang); }
        }

        [Fact]
        public void Languages_are_offered_by_their_own_names()
        {
            Assert.Equal("Deutsch", Customize.LanguageName("de"));
            Assert.Equal("Español", Customize.LanguageName("es"));
            Assert.Equal("English", Customize.LanguageName("en"));
            Assert.Equal("zz-qq", Customize.LanguageName("zz-qq"));
            Assert.Equal(new[] { "en", "de", "es" }, Conventions.Available().Take(3).ToArray());
        }
    }
}
