using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Newtonsoft.Json.Linq;
using Xunit;

namespace UIFramework.Tests
{
    /// <summary>One update notice per Streamer.bot start: due shortly after the first game starts, once GitHub's
    /// answers are fresh, naming the games of the run and UIFramework; each game's texts and ways (chat, toast) as its
    /// window declares them; then nothing until the next start.</summary>
    public class UpdateNoticeTests : IDisposable
    {
        private readonly string _folder;
        private DateTime _now = new DateTime(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc);
        private string _session = "run-1";
        private readonly List<string> _fetched = new List<string>();
        private string _minHostStub = "0.1.0";

        public UpdateNoticeTests()
        {
            _folder = Path.Combine(Path.GetTempPath(), "UIFramework.Tests", "notices-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(_folder, "lib", "1.2.0"));
            File.WriteAllText(Path.Combine(_folder, "lib", "1.2.0", "UIFramework.dll"), "");
            UpdateNotices.Folder = _folder;
            UpdateNotices.InBackground = false;
            UpdateNotices.Delay = TimeSpan.FromSeconds(20);
            UpdateNotices.Now = () => _now;
            UpdateNotices.Session = () => _session;
            UpdateNotices.Fetch = url =>
            {
                _fetched.Add(url);
                if (url == UpdateNotices.FrameworkLatestUrl) return "{ \"version\": \"1.3.0\", \"minHostStub\": \"" + _minHostStub + "\" }";
                if (url.Contains("aaskjer/Hangman-Game")) return "{ \"tag_name\": \"v13.2.0\" }";
                if (url.Contains("aaskjer/Trivia-Game")) return "{ \"tag_name\": \"v9.1.0\" }";
                throw new InvalidOperationException("offline");
            };
        }

        public void Dispose()
        {
            UpdateNotices.Folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UIFramework");
            UpdateNotices.InBackground = true;
            UpdateNotices.Delay = TimeSpan.FromSeconds(20);
            UpdateNotices.Now = () => DateTime.UtcNow;
            UpdateNotices.Session = () => "tests";
        }

        private string Settings(string game, string layout = null)
        {
            string dir = Path.Combine(_folder, game);
            Directory.CreateDirectory(dir);
            if (layout != null) File.WriteAllText(Path.Combine(dir, LayoutFile.FileName), layout);
            return Path.Combine(dir, "settings.json");
        }

        private JObject Hangman(string options = null) => Answer(UpdateNotices.DueJson("hangman", "Hangman Game", "v13.1.1", "aaskjer/Hangman-Game", Settings("hangman"), options));
        private JObject Trivia(string options = null) => Answer(UpdateNotices.DueJson("trivia", "Trivia Game", "v9.1.0", "aaskjer/Trivia-Game", Settings("trivia"), options));

        // Null: stop asking; an empty object: not yet; else the notice.
        private static JObject Answer(string json) => json == null ? null : json.Length == 0 ? new JObject() : JObject.Parse(json);
        private static List<string> Chat(JObject notice) => ((JArray)notice["chat"]).Select(l => (string)l).ToList();

        [Fact]
        public void One_notice_shortly_after_the_start_then_nothing_until_the_next_start()
        {
            Assert.Empty(Hangman());                       // the first game of the run: the notice waits for the others
            Assert.Contains(UpdateNotices.FrameworkLatestUrl, _fetched);
            _now = _now.AddSeconds(5);
            Assert.Empty(Trivia());                        // a second game starts with Streamer.bot; still waiting
            _now = _now.AddSeconds(20);
            var notice = Trivia();
            Assert.Equal(new[] { "🔔 Updates are out: Hangman Game v13.2.0, UIFramework v1.3.0. Details are in Streamer.bot's log." }, Chat(notice));
            Assert.Contains(notice["log"], l => ((string)l).Contains("https://github.com/aaskjer/Hangman-Game/releases/latest"));
            var toast = (JObject)notice["toast"].Single();
            Assert.Equal("Updates for Streamer.bot", (string)toast["title"]);
            Assert.Equal("Hangman Game v13.2.0, UIFramework v1.3.0. Details are in Streamer.bot's log.", (string)toast["text"]);
            Assert.False((bool)toast["always"]);           // by default only while no broadcaster account is connected
            Assert.Null(Hangman());                        // said: nothing more in this run
            Assert.Null(Trivia());

            _session = "run-2";                            // Streamer.bot started again
            Assert.Empty(Hangman());
            _now = _now.AddSeconds(30);
            Assert.StartsWith("🔔 Updates are out: Hangman Game v13.2.0", Chat(Hangman())[0]);
        }

        [Fact]
        public void Each_game_says_it_its_own_way_and_games_with_the_same_texts_share_a_message()
        {
            Hangman("{ \"chat\": true, \"chatText\": \"Hangman news: {updates} ({count})\", \"toast\": true, \"toastAlways\": true, \"toastTitle\": \"Hangman\", \"toastText\": \"{updates}\" }");
            _now = _now.AddSeconds(30);
            var notice = Trivia("{ \"chat\": false, \"toast\": false }");
            // Trivia delivers: UIFramework goes with its texts, which say no chat and no toast; Hangman's own update its way.
            Assert.Equal(new[] { "Hangman news: Hangman Game v13.2.0 (1)" }, Chat(notice));
            var toast = (JObject)notice["toast"].Single();
            Assert.Equal("Hangman", (string)toast["title"]);
            Assert.Equal("Hangman Game v13.2.0", (string)toast["text"]);
            Assert.True((bool)toast["always"]);
        }

        [Fact]
        public void The_checks_are_shared_and_kept_for_twelve_hours()
        {
            Hangman();
            int asked = _fetched.Count;
            _session = "run-2";
            Hangman();
            Assert.Equal(asked, _fetched.Count);           // fresh: GitHub is not asked again
            _now = _now.AddHours(13);
            _session = "run-3";
            Hangman();
            Assert.True(_fetched.Count > asked);
        }

        [Fact]
        public void A_game_with_notices_off_is_left_out_and_does_not_deliver_them()
        {
            Assert.Null(Answer(UpdateNotices.DueJson("hangman", "Hangman Game", "v13.1.1", "aaskjer/Hangman-Game", Settings("hangman", "{ \"updateNotices\": false }"), null)));
            Assert.Empty(Trivia());
            _now = _now.AddSeconds(30);
            Assert.Equal(new[] { "🔔 Updates are out: UIFramework v1.3.0. Details are in Streamer.bot's log." }, Chat(Trivia()));
        }

        [Fact]
        public void Nothing_new_says_nothing_and_ends_the_run()
        {
            Directory.CreateDirectory(Path.Combine(_folder, "lib", "1.3.0"));
            File.WriteAllText(Path.Combine(_folder, "lib", "1.3.0", "UIFramework.dll"), "");
            Assert.Empty(Trivia());
            _now = _now.AddSeconds(30);
            Assert.Null(Trivia());
        }

        [Fact]
        public void A_settings_action_too_old_for_the_newest_release_is_named_in_the_game_language()
        {
            _minHostStub = "0.3.0";
            Settings("trivia", "{ \"language\": \"de\" }");
            UpdateNotices.Register("trivia", "Trivia Game", "v9.1.0", "aaskjer/Trivia-Game", Settings("trivia"), "0.2.0");
            Assert.Empty(Trivia());
            _now = _now.AddSeconds(30);
            Assert.Equal(new[] { "🔔 Updates verfügbar: Trivia Game (neu importieren), UIFramework v1.3.0. Details stehen im Log von Streamer.bot." }, Chat(Trivia()));
        }

        [Fact]
        public void Offline_the_notice_waits_and_a_failed_check_is_tried_again_after_an_hour()
        {
            UpdateNotices.Fetch = url => throw new InvalidOperationException("offline");
            Assert.Empty(Hangman());
            _now = _now.AddMinutes(10);
            Assert.Empty(Hangman());                       // no good answer yet: the notice waits
            UpdateNotices.Fetch = url => url == UpdateNotices.FrameworkLatestUrl ? "{ \"version\": \"1.3.0\" }" : "{ \"tag_name\": \"v13.2.0\" }";
            _now = _now.AddMinutes(50);                    // an hour after the failure it is asked again
            Assert.StartsWith("🔔 Updates are out: Hangman Game v13.2.0", Chat(Hangman())[0]);
        }

        [Fact]
        public void The_1_3_0_host_block_still_gets_its_lines()
        {
            Assert.Empty(UpdateNotices.Due("hangman", "Hangman Game", "v13.1.1", "aaskjer/Hangman-Game", Settings("hangman")));
            _now = _now.AddSeconds(30);
            var lines = UpdateNotices.Due("hangman", "Hangman Game", "v13.1.1", "aaskjer/Hangman-Game", Settings("hangman"));
            Assert.StartsWith("🔔 Updates are out: Hangman Game v13.2.0", lines[0]);
            Assert.Contains(lines, l => l.Contains("releases/latest"));
        }

        private static FakeCph Started(FakeCph cph, string settings)
        {
            typeof(UiUpdateNotice).GetField("_started", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, false);
            typeof(UiUpdateNotice).GetField("_connectTries", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, 1);
            UiUpdateNotice.Startup(cph, "Demo Game", "demo", "v1.0.0", settings);
            for (int i = 0; i < 100 && cph.Sent.Count == 0 && cph.Toasts.Count == 0; i++)
                Thread.Sleep(50);
            Thread.Sleep(100);
            return cph;
        }

        [Fact]
        public void From_Init_the_host_block_sends_the_line_to_every_connected_chat()
        {
            UpdateNotices.Delay = TimeSpan.Zero;
            var cph = Started(new FakeCph { Twitch = new object(), Kick = new object() }, Settings("demo"));
            Assert.Equal(new[] { "twitch 🔔 Updates are out: UIFramework v1.3.0. Details are in Streamer.bot's log.", "kick 🔔 Updates are out: UIFramework v1.3.0. Details are in Streamer.bot's log." }, cph.Sent);
            Assert.Empty(cph.Toasts);                      // a broadcaster account is connected: no toast
            Assert.Contains(cph.Log, l => l.Contains("Update: UIFramework v1.3.0 is out"));
        }

        [Fact]
        public void Without_a_broadcaster_account_the_notice_is_a_Windows_toast()
        {
            UpdateNotices.Delay = TimeSpan.Zero;
            var cph = Started(new FakeCph(), Settings("demo"));
            Assert.Empty(cph.Sent);
            Assert.Equal(new[] { "Updates for Streamer.bot | UIFramework v1.3.0. Details are in Streamer.bot's log." }, cph.Toasts);
        }
    }
}
