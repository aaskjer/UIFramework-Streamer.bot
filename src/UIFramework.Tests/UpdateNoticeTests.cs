using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using Xunit;

namespace UIFramework.Tests
{
    /// <summary>One update notice in chat per Streamer.bot start: due a few minutes after the first game of the run,
    /// once GitHub's answers are fresh, naming the games used so far and UIFramework; then nothing until the next start.</summary>
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
            UpdateNotices.Now = () => DateTime.UtcNow;
            UpdateNotices.Session = () => "tests";
        }

        private string Settings(string game, bool notices = true)
        {
            string dir = Path.Combine(_folder, game);
            Directory.CreateDirectory(dir);
            if (!notices) File.WriteAllText(Path.Combine(dir, LayoutFile.FileName), "{ \"updateNotices\": false }");
            return Path.Combine(dir, "settings.json");
        }

        private string[] Hangman() => UpdateNotices.Due("hangman", "Hangman Game", "v13.1.1", "aaskjer/Hangman-Game", Settings("hangman"));
        private string[] Trivia() => UpdateNotices.Due("trivia", "Trivia Game", "v9.1.0", "aaskjer/Trivia-Game", Settings("trivia"));

        [Fact]
        public void One_line_a_few_minutes_into_the_run_then_nothing_until_the_next_start()
        {
            Assert.Empty(Hangman());                       // the first game of the run: the notice waits
            Assert.Contains(UpdateNotices.FrameworkLatestUrl, _fetched);
            _now = _now.AddMinutes(1);
            Assert.Empty(Trivia());                        // a second game joins the run; still waiting
            _now = _now.AddMinutes(3);
            var lines = Trivia();
            Assert.Equal("🔔 Updates are out: Hangman Game v13.2.0, UIFramework v1.3.0. Details are in Streamer.bot's log.", lines[0]);
            Assert.Contains(lines, l => l.Contains("https://github.com/aaskjer/Hangman-Game/releases/latest"));
            Assert.DoesNotContain(lines, l => l.Contains("Trivia Game v"));   // Trivia runs its newest release
            Assert.Null(Hangman());                        // said: nothing more in this run
            Assert.Null(Trivia());

            _session = "run-2";                            // Streamer.bot started again
            Assert.Empty(Hangman());
            _now = _now.AddMinutes(4);
            Assert.StartsWith("🔔 Updates are out: Hangman Game v13.2.0", Hangman()[0]);
            Assert.Null(Hangman());
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
        public void A_game_with_notices_off_is_left_out_and_does_not_post_them()
        {
            Assert.Null(UpdateNotices.Due("hangman", "Hangman Game", "v13.1.1", "aaskjer/Hangman-Game", Settings("hangman", notices: false)));
            Assert.Empty(Trivia());
            _now = _now.AddMinutes(4);
            var lines = Trivia();
            Assert.Equal("🔔 Updates are out: UIFramework v1.3.0. Details are in Streamer.bot's log.", lines[0]);
        }

        [Fact]
        public void Nothing_new_says_nothing_and_ends_the_run()
        {
            Directory.CreateDirectory(Path.Combine(_folder, "lib", "1.3.0"));
            File.WriteAllText(Path.Combine(_folder, "lib", "1.3.0", "UIFramework.dll"), "");
            Assert.Empty(Trivia());
            _now = _now.AddMinutes(4);
            Assert.Null(Trivia());
        }

        [Fact]
        public void A_settings_action_too_old_for_the_newest_release_is_named_in_the_game_language()
        {
            _minHostStub = "0.3.0";
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(Settings("trivia")), LayoutFile.FileName), "{ \"language\": \"de\" }");
            UpdateNotices.Register("trivia", "Trivia Game", "v9.1.0", "aaskjer/Trivia-Game", Settings("trivia"), "0.2.0");
            Assert.Empty(Trivia());
            _now = _now.AddMinutes(4);
            Assert.Equal("🔔 Updates verfügbar: Trivia Game (neu importieren), UIFramework v1.3.0. Details stehen im Log von Streamer.bot.", Trivia()[0]);
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
            Assert.StartsWith("🔔 Updates are out: Hangman Game v13.2.0", Hangman()[0]);
        }

        [Fact]
        public void The_host_block_sends_the_line_to_the_chat_it_was_used_in_once()
        {
            var cph = new FakeCph();
            var reset = typeof(UiUpdateNotice).GetField("_next", BindingFlags.NonPublic | BindingFlags.Static);
            var done = typeof(UiUpdateNotice).GetField("_done", BindingFlags.NonPublic | BindingFlags.Static);
            done.SetValue(null, false);
            string settings = Settings("demo");
            void Run(string platform)
            {
                reset.SetValue(null, DateTime.MinValue);
                UiHost.Handle(new Dictionary<string, object> { ["commandSource"] = platform, ["user"] = "alice" }, cph, "Demo Game", "demo", "v1.0.0", settings);
            }
            Run("kick");
            Assert.Empty(cph.Sent);                       // the notice waits a few minutes into the run
            _now = _now.AddMinutes(4);
            Run("kick");
            Assert.Single(cph.Sent);
            Assert.StartsWith("kick 🔔 Updates are out: UIFramework v1.3.0", cph.Sent[0]);
            Assert.Contains(cph.Log, l => l.Contains("Update: UIFramework v1.3.0 is out"));
            Run("twitch");
            Assert.Single(cph.Sent);                      // once per Streamer.bot start
        }
    }
}
