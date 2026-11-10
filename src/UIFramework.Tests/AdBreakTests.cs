using System;
using System.Collections.Generic;
using System.Threading;
using Newtonsoft.Json.Linq;
using Xunit;

namespace UIFramework.Tests
{
    /// <summary>UiAdBreak: the Twitch ad break as a window of time for a game, and the conventions for it.</summary>
    [Collection("AdBreak")]
    public class AdBreakTests : IDisposable
    {
        // The block remembers its last announcement, and the tests share it: each test's clock starts a day after
        // the one before, so none is taken for the same ad break as an earlier test's.
        private static int _day;
        private DateTime _now;
        private readonly Func<DateTime> _clock = UiAdBreak.Now;

        public AdBreakTests()
        {
            _now = new DateTime(2026, 11, 10, 20, 0, 0, DateTimeKind.Utc).AddDays(Interlocked.Increment(ref _day));
            UiAdBreak.Now = () => _now;
            UiAdBreak.End();
        }

        public void Dispose() { UiAdBreak.End(); UiAdBreak.Now = _clock; }

        private static Dictionary<string, object> Args(params object[] pairs)
        {
            var args = new Dictionary<string, object>();
            for (int i = 0; i + 1 < pairs.Length; i += 2) args[(string)pairs[i]] = pairs[i + 1];
            return args;
        }

        [Fact]
        public void An_ad_run_opens_the_window_for_its_length()
        {
            Assert.False(UiAdBreak.Active);
            Assert.Equal(90, UiAdBreak.Begin(Args("adLength", 90)));
            Assert.True(UiAdBreak.Active);
            Assert.Equal(90, UiAdBreak.SecondsLeft);
            Assert.Equal(90, UiAdBreak.Length);

            _now = _now.AddSeconds(60);
            Assert.True(UiAdBreak.Active);
            Assert.Equal(30, UiAdBreak.SecondsLeft);

            _now = _now.AddSeconds(31);
            Assert.False(UiAdBreak.Active);
            Assert.Equal(0, UiAdBreak.SecondsLeft);
        }

        [Theory]
        [InlineData(90L, 90)]
        [InlineData("120", 120)]
        [InlineData(60.0, 60)]
        [InlineData("nonsense", 0)]
        [InlineData(-5, 0)]
        [InlineData(null, 0)]
        public void The_length_is_read_whatever_type_it_arrives_as(object length, int expected)
        {
            Assert.Equal(expected, UiAdBreak.Begin(Args("adLength", length)));
            Assert.Equal(expected > 0, UiAdBreak.Active);
        }

        [Fact]
        public void No_length_no_window()
        {
            Assert.Equal(0, UiAdBreak.Begin(new Dictionary<string, object>()));
            Assert.Equal(0, UiAdBreak.Begin(null));
            Assert.False(UiAdBreak.Active);
        }

        [Fact]
        public void End_closes_it_early()
        {
            UiAdBreak.Begin(Args("adLength", 180));
            UiAdBreak.End();
            Assert.False(UiAdBreak.Active);
        }

        [Fact]
        public void The_end_is_told_once_and_not_for_a_break_that_was_overtaken()
        {
            int first = 0, second = 0;
            UiAdBreak.Begin(Args("adLength", 1), () => Interlocked.Increment(ref first));
            UiAdBreak.Begin(Args("adLength", 1), () => Interlocked.Increment(ref second));
            Thread.Sleep(2500);
            Assert.Equal(0, first);
            Assert.Equal(1, second);

            int ended = 0;
            UiAdBreak.Begin(Args("adLength", 1), () => Interlocked.Increment(ref ended));
            UiAdBreak.End();
            Thread.Sleep(2000);
            Assert.Equal(0, ended);
        }

        [Fact]
        public void An_upcoming_ad_is_announced_once_when_it_is_near_enough()
        {
            int minutes, seconds;
            Assert.False(UiAdBreak.Announce(Args("minutes", 5, "adLength", 150), 2, out minutes, out seconds));
            Assert.False(UiAdBreak.Announce(Args("minutes", 3, "adLength", 150), 2, out minutes, out seconds));
            Assert.True(UiAdBreak.Announce(Args("minutes", 2, "adLength", 150), 2, out minutes, out seconds));
            Assert.Equal(2, minutes);
            Assert.Equal(150, seconds);
            // The same ad a minute later: said already.
            _now = _now.AddMinutes(1);
            Assert.False(UiAdBreak.Announce(Args("minutes", 1, "adLength", 150), 2, out minutes, out seconds));
            // The next ad break, an hour on.
            _now = _now.AddMinutes(60);
            Assert.True(UiAdBreak.Announce(Args("minutes", 1, "adLength", 60), 2, out minutes, out seconds));
        }

        [Fact]
        public void The_minutes_before_stay_within_what_Twitch_tells()
        {
            int minutes, seconds;
            // Asked for ten minutes ahead: five is as early as it gets.
            Assert.True(UiAdBreak.Announce(Args("minutes", 5, "adLength", 60), 10, out minutes, out seconds));
            _now = _now.AddMinutes(30);
            // Asked for none: one minute ahead.
            Assert.False(UiAdBreak.Announce(Args("minutes", 2, "adLength", 60), 0, out minutes, out seconds));
            Assert.True(UiAdBreak.Announce(Args("minutes", 1, "adLength", 60), 0, out minutes, out seconds));
        }

        [Theory]
        [InlineData(180, 60, 5, 5, 2)]    // 60+5, 60+5, and 50 left: two
        [InlineData(190, 60, 5, 5, 3)]    // the last round needs no pause after it
        [InlineData(30, 60, 5, 5, 1)]     // shorter than a round: still one
        [InlineData(600, 60, 5, 3, 3)]    // capped
        [InlineData(0, 60, 5, 5, 1)]
        [InlineData(120, 0, 0, 0, 1)]
        public void Rounds_that_fit(int ad, int round, int gap, int most, int expected) => Assert.Equal(expected, UiAdBreak.RoundsThatFit(ad, round, gap, most));

        [Fact]
        public void The_bonus_is_a_percentage_on_top()
        {
            Assert.Equal(1.5, UiAdBreak.WithBonus(1.0, 50));
            Assert.Equal(2.0, UiAdBreak.WithBonus(1.0, 100));
            Assert.Equal(0.8, UiAdBreak.WithBonus(0.8, 0));
            Assert.Equal(0.8, UiAdBreak.WithBonus(0.8, -20));
        }

        [Theory]
        [InlineData("adBreakStart", "bool")]
        [InlineData("adBreakAnnounce", "bool")]
        [InlineData("adBreakAnnounceMinutes", "int")]
        [InlineData("adBreakBonusPercent", "percent")]
        [InlineData("adBreakOnly", "bool")]
        public void The_ad_break_keys_are_conventions_on_the_Automation_page_in_every_language(string key, string kind)
        {
            var asm = typeof(Conventions).Assembly;
            JObject Read(string name)
            {
                using (var stream = asm.GetManifestResourceStream("UIFramework.conventions." + name))
                using (var reader = new System.IO.StreamReader(stream))
                    return JObject.Parse(reader.ReadToEnd());
            }

            var keys = Read("keys.json");
            Assert.NotNull(keys["pages"]["automation"]);
            var declared = keys["keys"][key] as JObject;
            Assert.NotNull(declared);
            Assert.Equal(kind, (string)declared["kind"]);
            Assert.Equal("automation", (string)declared["page"]);
            foreach (string code in new[] { "en", "de", "es" })
            {
                var lang = Read("lang." + code + ".json");
                Assert.False(string.IsNullOrWhiteSpace((string)lang["pages"]["automation"]), "no title for the Automation page in " + code + ".json");
                var text = lang["keys"][key] as JObject;
                Assert.True(text != null && !string.IsNullOrWhiteSpace((string)text["label"]) && !string.IsNullOrWhiteSpace((string)text["help"]),
                    key + " has no label and help in " + code + ".json");
            }
            Assert.Equal("adBreakAnnounce", Conventions.Default.Complete(new Setting { Key = "adBreakAnnounceMinutes" }).Requires);
        }
    }
}
