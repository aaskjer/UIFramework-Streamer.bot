using System.IO;
using System.Linq;
using Xunit;

namespace UIFramework.Tests
{
    /// <summary>The window's own texts: every language file carries every key, and a window in German reads them.</summary>
    public class LangTests
    {
        [Fact]
        public void GermanCarriesEveryTextTheWindowHas()
        {
            var english = Conventions.Default.Lang;
            var german = Conventions.Load("de").Lang;
            Assert.True(english.Ui.Count > 150, "the English ui section is missing or thin");
            var missing = english.Ui.Keys.Where(k => !german.Ui.TryGetValue(k, out var text) || text == english.Ui[k] && !k.StartsWith("btn.ok") && !k.StartsWith("about.version") && !k.StartsWith("about.links") && !k.StartsWith("about.releases") && !k.StartsWith("pill.update")).ToList();
            Assert.True(missing.Count == 0, "not translated: " + string.Join(", ", missing));
            var extra = german.Ui.Keys.Where(k => !english.Ui.ContainsKey(k)).ToList();
            Assert.True(extra.Count == 0, "German has keys English lacks: " + string.Join(", ", extra));
            foreach (var key in english.Ui.Keys)
            {
                // The same placeholders in both, so string.Format cannot fail in one language only.
                var mine = System.Text.RegularExpressions.Regex.Matches(english.Ui[key], @"\{\d\}").Cast<System.Text.RegularExpressions.Match>().Select(m => m.Value).OrderBy(v => v);
                var theirs = System.Text.RegularExpressions.Regex.Matches(german.Ui[key], @"\{\d\}").Cast<System.Text.RegularExpressions.Match>().Select(m => m.Value).OrderBy(v => v);
                Assert.True(mine.SequenceEqual(theirs), key + " has other placeholders in German");
            }
        }

        [Fact]
        public void EveryConventionsPageHasAnIconAndAnOrder()
        {
            var missing = Conventions.Default.Lang.Pages.Keys.Where(id => !Conventions.Default.Pages.ContainsKey(id)).ToList();
            Assert.True(missing.Count == 0, "pages with a title but no icon and order: " + string.Join(", ", missing));
            Assert.Equal("🎁", Resolve.IconGlyph(Conventions.Default.Pages["channelPoints"].Icon));
            Assert.Equal("📊", Resolve.IconGlyph(Conventions.Default.Pages["stats"].Icon));
        }

        [Fact]
        public void GermanCarriesEveryKeyPageAndPlaceholder()
        {
            var english = Conventions.Default.Lang;
            var german = Conventions.Load("de").Lang;
            Assert.Equal("de", german.Code);
            Assert.True(german.Translates && !english.Translates);
            Assert.Empty(english.Keys.Keys.Where(k => !german.Keys.ContainsKey(k)));
            Assert.Empty(english.Keys.Where(k => k.Value.Help != null && german.Keys[k.Key].Help == null).Select(k => k.Key));
            Assert.Empty(english.Pages.Keys.Where(k => !german.Pages.ContainsKey(k)));
            Assert.Empty(english.Framework.Keys.Where(k => !german.Framework.ContainsKey(k)));
            Assert.Empty(english.Placeholders.Keys.Where(k => !german.Placeholders.ContainsKey(k)));
            Assert.True(german.Texts.Count > 80, "the German texts (sections, pills, page titles) are missing or thin");
            Assert.Equal("Punkte", german.Text("Points"));
            Assert.Equal("Shoe", german.Text("Shoe"));
            Assert.Null(german.TextOrNull("Shoe"));
        }

        private const string DeclaredSchema = @"{
          ""host"": ""demo"", ""version"": ""1"",
          ""settings"": [
            { ""key"": ""globalCooldown"", ""default"": 10, ""page"": ""game"", ""section"": ""Cooldowns"", ""label"": ""Everyone"", ""help"": ""Seconds."" },
            { ""key"": ""numDecks"", ""default"": 6, ""page"": ""game"", ""section"": ""Shoe"", ""label"": ""Decks"" },
            { ""key"": ""freeGame"", ""default"": false, ""page"": ""game"" },
            { ""key"": ""resetScope"", ""default"": ""all"", ""page"": ""game"", ""options"": [ { ""value"": ""all"", ""label"": ""Everything"" }, { ""value"": ""wins"", ""label"": ""Wins only"" } ] },
            { ""key"": ""msgWin"", ""default"": ""{user} wins"", ""kind"": ""template"", ""group"": ""Wins"", ""label"": ""Win"" },
            { ""key"": ""userCooldown"", ""default"": 30, ""page"": ""game"", ""label"": ""Per-user cooldown"" },
            { ""key"": ""defaultBet"", ""default"": 100, ""page"": ""game"" },
            { ""key"": ""pointsName"", ""default"": ""Points"", ""page"": ""game"", ""label"": ""Currency"" }
          ],
          ""lang"": { ""de"": { ""keys"": { ""numDecks"": { ""label"": ""Decks im Schuh"", ""help"": ""Wie viele."" }, ""defaultBet"": ""Grundeinsatz"" },
                              ""texts"": { ""Shoe"": ""Schuh"", ""Wins only"": ""Nur Gewinne"", ""Currency"": ""Währung"" } } } }";
        private const string DeclaredWindow = @"{ ""title"": ""Demo"", ""language"": ""LANG"",
          ""pages"": [ { ""id"": ""game"", ""title"": ""Game"" }, { ""id"": ""help"", ""title"": ""How to play"" } ],
          ""headerPills"": [ { ""key"": ""freeGame"", ""on"": ""Free game on"", ""off"": ""Free game off"", ""tooltip"": ""Free game, from the Game page."" } ] }";

        [Fact]
        public void GermanTranslatesWhatTheGameDeclaredAndTheGamesOwnTextsComeFirst()
        {
            var spec = SpecJson.Parse(DeclaredWindow.Replace("LANG", "de"), Path.GetTempFileName());
            var schema = Schema.Parse(DeclaredSchema);
            try
            {
                Entry.Prepare(spec, schema);
                var cooldown = spec.ResolvedSettings.First(s => s.Key == "globalCooldown");
                Assert.Equal("Globale Abklingzeit", cooldown.Label);           // the game's own wording, untranslated: the conventions' text
                Assert.Equal("Abklingzeiten", cooldown.Section);              // a section by its wording
                Assert.StartsWith("Zeit", cooldown.Help);                      // the same for the help
                Assert.Equal("Abklingzeit pro Nutzer", spec.ResolvedSettings.First(s => s.Key == "userCooldown").Label);   // the conventions' own English wording
                Assert.Equal("Währung", spec.ResolvedSettings.First(s => s.Key == "pointsName").Label);   // the game's own wording, translated by it
                var bet = spec.ResolvedSettings.First(s => s.Key == "defaultBet");
                Assert.Equal("Grundeinsatz", bet.Label);                       // K(key, label): the game's label ...
                Assert.Equal("Wenn ein Spieler keinen Betrag nennt.", bet.Help); // ... and the conventions' help it left alone
                var decks = spec.ResolvedSettings.First(s => s.Key == "numDecks");
                Assert.Equal("Decks im Schuh", decks.Label);                   // the game's own translation of its own key
                Assert.Equal("Wie viele.", decks.Help);
                Assert.Equal("Schuh", decks.Section);                          // the game's own text
                Assert.Equal("Freispiel", spec.ResolvedSettings.First(s => s.Key == "freeGame").Label);
                var scope = spec.ResolvedSettings.First(s => s.Key == "resetScope");
                Assert.Equal(new[] { "Alles", "Nur Gewinne" }, scope.Options.Select(o => o.Label));
                Assert.Equal("Gewinne", spec.Messages.Single().Group);
                Assert.Equal("Spiel", spec.Pages.First(p => p.Id == "game").Title);
                Assert.Equal("Anleitung", spec.Pages.First(p => p.Id == "help").Title);
                Assert.Equal("Free game, from the Game page.", spec.HeaderPills.Single().Tooltip);   // no translation known: as declared
                Assert.Equal("Freispiel an", spec.Conventions.Lang.Text("Free game on"));

                // Back in English nothing of that sticks: the schema's own texts were never changed.
                var english = SpecJson.Parse(DeclaredWindow.Replace("LANG", "en"), Path.GetTempFileName());
                Entry.Prepare(english, schema);
                Assert.Equal("Everyone", english.ResolvedSettings.First(s => s.Key == "globalCooldown").Label);
                Assert.Equal("Shoe", english.ResolvedSettings.First(s => s.Key == "numDecks").Section);
                Assert.Equal(new[] { "Everything", "Wins only" }, english.ResolvedSettings.First(s => s.Key == "resetScope").Options.Select(o => o.Label));
                Assert.Equal("How to play", english.Pages.First(p => p.Id == "help").Title);
                Assert.Equal("Wins", english.Messages.Single().Group);
            }
            finally { Text.Use(Conventions.Default.Lang); }
        }

        [Fact]
        public void A_game_without_German_stays_English_whatever_the_layout_file_asks()
        {
            string folder = Path.Combine(Path.GetTempPath(), "UIFramework.Tests", "noGerman-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            string settings = Path.Combine(folder, "settings.json");
            File.WriteAllText(Path.Combine(folder, LayoutFile.FileName), @"{ ""language"": ""de"" }");
            var withoutLang = Newtonsoft.Json.Linq.JObject.Parse(DeclaredSchema);
            withoutLang.Remove("lang");
            var schema = Schema.Parse(withoutLang.ToString());
            try
            {
                var spec = SpecJson.Parse(DeclaredWindow.Replace(@"""language"": ""LANG"",", ""), settings);
                Entry.Prepare(spec, schema);
                Assert.Equal(new[] { "en" }, spec.Languages);                                       // nothing else to offer
                Assert.Equal("Everyone", spec.ResolvedSettings.First(s => s.Key == "globalCooldown").Label);
                Assert.Equal("Game", spec.Pages.First(p => p.Id == "game").Title);

                // The same game with Lang("de"), even an empty one, offers German and is shown in it.
                var withLang = Schema.Parse(DeclaredSchema);
                var german = SpecJson.Parse(DeclaredWindow.Replace(@"""language"": ""LANG"",", ""), settings);
                Entry.Prepare(german, withLang);
                Assert.Equal(new[] { "en", "de" }, german.Languages);
                Assert.Equal("Abklingzeit pro Nutzer", german.ResolvedSettings.First(s => s.Key == "userCooldown").Label);
            }
            finally { Text.Use(Conventions.Default.Lang); }
        }

        [Fact]
        public void A_locked_window_ignores_the_users_layout()
        {
            string folder = Path.Combine(Path.GetTempPath(), "UIFramework.Tests", "locked-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            string settings = Path.Combine(folder, "settings.json");
            File.WriteAllText(Path.Combine(folder, LayoutFile.FileName), @"{ ""fields"": { ""numDecks"": { ""label"": ""My decks"", ""hidden"": true } } }");
            var schema = Schema.Parse(DeclaredSchema);
            try
            {
                var open = SpecJson.Parse(DeclaredWindow.Replace("LANG", "en"), settings);
                Entry.Prepare(open, schema);
                Assert.Equal("My decks", open.ResolvedSettings.First(s => s.Key == "numDecks").Label);
                Assert.False(open.Locked);

                var locked = SpecJson.Parse(DeclaredWindow.Replace("LANG", "en").Replace(@"""title"": ""Demo"",", @"""title"": ""Demo"", ""locked"": true,"), settings);
                Entry.Prepare(locked, schema);
                Assert.True(locked.Locked);
                var decks = locked.ResolvedSettings.First(s => s.Key == "numDecks");
                Assert.NotEqual("My decks", decks.Label);
                Assert.False(decks.Hidden);
            }
            finally { Text.Use(Conventions.Default.Lang); }
        }

        [Fact]
        public void TextFollowsTheLanguageAndFallsBackToEnglish()
        {
            try
            {
                Text.Use(Conventions.Load("de").Lang);
                Assert.Equal("Speichern", Text.T("btn.save"));
                Assert.Equal("3 Änderungen offen", Text.T("status.unsaved.many", 3));
                Text.Use(Conventions.Load("xx").Lang);   // no such file: English
                Assert.Equal("Save", Text.T("btn.save"));
                Assert.Equal("no.such.key", Text.T("no.such.key"));
            }
            finally { Text.Use(Conventions.Default.Lang); }
        }
    }
}
