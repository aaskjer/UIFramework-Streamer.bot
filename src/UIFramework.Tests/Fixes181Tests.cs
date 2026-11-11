using System.Linq;
using System.Windows.Input;
using Newtonsoft.Json.Linq;
using Xunit;

namespace UIFramework.Tests
{
    /// <summary>What the review of 2026-11-11 found and 1.8.1 put right.</summary>
    public class Fixes181Tests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "UIFramework.Tests", "fixes181-" + Guid.NewGuid().ToString("N"));

        public Fixes181Tests() { Directory.CreateDirectory(_folder); }

        public void Dispose()
        {
            UpdateNotices.Folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UIFramework");
            UpdateNotices.InBackground = true;
            UpdateNotices.Now = () => DateTime.UtcNow;
            UpdateNotices.Session = () => "tests";
            Text.Use(Conventions.Default.Lang);
        }

        [Fact]
        public void AltGrIsNotAShortcut()
        {
            Assert.True(Shell.IsShortcut(ModifierKeys.Control));
            Assert.True(Shell.IsShortcut(ModifierKeys.Control | ModifierKeys.Shift));
            // AltGr+7 is "{" on a German keyboard and arrives as Ctrl+Alt+7: typed, not "page 7".
            Assert.False(Shell.IsShortcut(ModifierKeys.Control | ModifierKeys.Alt));
            Assert.False(Shell.IsShortcut(ModifierKeys.None));
            Assert.False(Shell.IsShortcut(ModifierKeys.Alt));
        }

        [Fact]
        public void TheTourMayPointAtTheSearch()
        {
            var schema = Schema.Parse(new JObject
            {
                ["host"] = "demo",
                ["settings"] = JArray.Parse(@"[ { ""key"": ""sides"", ""default"": 6, ""page"": ""game"" } ]"),
                ["window"] = new JObject { ["title"] = "Demo", ["pages"] = new JArray("game"), ["tour"] = new JArray("search", "save") }
            }.ToString());
            var spec = Entry.SpecFor(null, schema, Path.Combine(_folder, "tour", "settings.json"));
            Entry.Prepare(spec, schema);
            Assert.Equal(2, spec.Tour.Count);
            Assert.Empty(spec.Problems);
        }

        [Fact]
        public void AGameWhoseReleaseCheckFailsDoesNotSilenceTheOthers()
        {
            var now = new DateTime(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);
            Directory.CreateDirectory(Path.Combine(_folder, "lib", "1.2.0"));
            File.WriteAllText(Path.Combine(_folder, "lib", "1.2.0", "UIFramework.dll"), "");
            UpdateNotices.Folder = _folder;
            UpdateNotices.InBackground = false;
            UpdateNotices.Now = () => now;
            UpdateNotices.Session = () => "run-1";
            bool online = true;
            UpdateNotices.Fetch = url =>
            {
                if (!online) throw new InvalidOperationException("offline");
                if (url == UpdateNotices.FrameworkLatestUrl) return "{ \"version\": \"1.3.0\", \"minHostStub\": \"0.1.0\" }";
                if (url.Contains("aaskjer/Hangman-Game")) return "[ { \"tag_name\": \"v13.2.0\" } ]";
                throw new InvalidOperationException("404");   // a repository without a release, renamed, or the rate limit
            };
            string Settings(string game) { Directory.CreateDirectory(Path.Combine(_folder, game)); return Path.Combine(_folder, game, "settings.json"); }
            string Hangman() => UpdateNotices.DueJson("hangman", "Hangman Game", "v13.1.1", "aaskjer/Hangman-Game", Settings("hangman"), null);
            string Broken() => UpdateNotices.DueJson("broken", "Broken Game", "v1.0.0", "you/your-repo", Settings("broken"), null);

            Assert.Equal("", Hangman());
            Assert.Equal("", Broken());
            now = now.AddSeconds(30);
            string notice = Hangman();
            Assert.False(string.IsNullOrEmpty(notice));   // before: "" for good, and nobody heard of anything
            string said = (string)((JArray)JObject.Parse(notice)["chat"])[0];
            Assert.Contains("Hangman Game v13.2.0", said);
            Assert.Contains("UIFramework v1.3.0", said);
            Assert.DoesNotContain("Broken Game", said);

            // Without a network UIFramework's own check fails too, and the notice still waits for it.
            UpdateNotices.Session = () => "run-2";
            online = false;
            now = now.AddHours(13);
            Assert.Equal("", Hangman());
            now = now.AddSeconds(30);
            Assert.Equal("", Hangman());
        }

        [Fact]
        public void TurnTimeoutHasNoFloorOfItsOwn()
        {
            // Blackjack's "0 = no limit" was saved as 5: the conventions' minimum went to a game that declared none.
            var schema = Schema.Parse(@"{ ""host"": ""demo"", ""version"": ""1"", ""settings"": [
                { ""key"": ""turnTimeout"", ""default"": 180, ""kind"": ""int"", ""page"": ""game"" } ] }");
            var spec = SpecJson.Parse(@"{ ""title"": ""Demo"" }", Path.Combine(_folder, "turn.json"));
            Entry.Prepare(spec, schema);
            Assert.Null(spec.ResolvedSettings.Single(s => s.Key == "turnTimeout").Min);
        }

        [Fact]
        public void TheGamesOwnPageTitleComesBeforeATranslationOfItsWording()
        {
            // "Board" is the grid in one game ("Spielfeld" in the conventions' texts) and the scoreboard in another,
            // which says so with Pg("board", "Rangliste").
            var schema = Schema.Parse(@"{ ""host"": ""demo"", ""version"": ""1"",
                ""settings"": [ { ""key"": ""scoreboardTopN"", ""default"": 5, ""page"": ""board"" }, { ""key"": ""gridSize"", ""default"": 10, ""page"": ""grid"" } ],
                ""lang"": { ""de"": { ""pages"": { ""board"": ""Rangliste"" } } } }");
            var spec = SpecJson.Parse(@"{ ""title"": ""Demo"", ""language"": ""de"",
                ""pages"": [ { ""id"": ""board"", ""title"": ""Board"" }, { ""id"": ""grid"", ""title"": ""Board"" } ] }", Path.Combine(_folder, "pages.json"));
            Entry.Prepare(spec, schema);
            Assert.Equal("Rangliste", spec.Pages.First(p => p.Id == "board").Title);
            Assert.Equal("Spielfeld", spec.Pages.First(p => p.Id == "grid").Title);   // no title of its own: the wording's translation
        }

        [Fact]
        public void TheHostBlockLoadsNoDllThatLostItsHash()
        {
            string dll = Path.Combine(_folder, "UIFramework.dll");
            File.WriteAllText(dll, "abc");
            Assert.True(UiUpdateNotice.Intact(dll));                       // put in by hand: no hash file, taken as it is
            // SHA-256 of "abc", as the settings action writes it beside the download.
            File.WriteAllText(dll + ".sha256", "BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD\n");
            Assert.True(UiUpdateNotice.Intact(dll));
            File.WriteAllText(dll, "abd");                                 // changed since
            Assert.False(UiUpdateNotice.Intact(dll));
        }
    }
}
