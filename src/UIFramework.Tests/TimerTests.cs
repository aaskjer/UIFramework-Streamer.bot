using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace UIFramework.Tests
{
    /// <summary>UiTimer: a Streamer.bot timed action on a fixed interval, a random one, and only after so many chat
    /// lines, and the convention keys a game declares for it.</summary>
    public class TimerTests
    {
        [Fact]
        public void A_fixed_interval_is_its_minutes_and_never_under_one()
        {
            Assert.Equal(900, UiTimer.NextSeconds(15, false, 60));
            Assert.Equal(60, UiTimer.NextSeconds(0, false, 0));
            Assert.Equal(60, UiTimer.NextSeconds(-5, false, 0));
        }

        [Fact]
        public void A_random_interval_stays_between_its_bounds_and_varies()
        {
            var drawn = Enumerable.Range(0, 300).Select(i => UiTimer.NextSeconds(5, true, 10)).ToList();
            Assert.All(drawn, s => Assert.InRange(s, 300, 600));
            Assert.True(drawn.Distinct().Count() > 20, "300 draws gave " + drawn.Distinct().Count() + " different waits");
        }

        [Fact]
        public void A_maximum_below_the_interval_is_the_interval()
        {
            Assert.Equal(600, UiTimer.NextSeconds(10, true, 3));
        }

        [Fact]
        public void Lines_are_counted_per_scope_and_start_over_once_taken()
        {
            string a = "lines-" + Guid.NewGuid(), b = "lines-" + Guid.NewGuid();
            for (int i = 0; i < 4; i++) UiTimer.NoteLine(a);
            UiTimer.NoteLine(b);

            Assert.Equal(4, UiTimer.LinesSince(a));
            Assert.False(UiTimer.TakeLines(a, 5));
            Assert.Equal(4, UiTimer.LinesSince(a));     // a refusal keeps the count: the next turn of the timer adds to it
            UiTimer.NoteLine(a);
            Assert.True(UiTimer.TakeLines(a, 5));
            Assert.Equal(0, UiTimer.LinesSince(a));
            Assert.Equal(1, UiTimer.LinesSince(b));
        }

        [Fact]
        public void No_minimum_always_passes()
        {
            string scope = "lines-" + Guid.NewGuid();
            Assert.True(UiTimer.TakeLines(scope, 0));
            Assert.True(UiTimer.TakeLines(null, 0));
        }

        [Fact]
        public void Sync_switches_a_stopped_timer_on_with_its_interval()
        {
            var cph = new FakeCph();
            UiTimer.Sync(cph, "t", true, 15, false, 0);
            Assert.True(cph.TimerRunning);
            Assert.Equal(900, cph.TimerInterval);
        }

        [Fact]
        public void Sync_leaves_a_running_timer_its_countdown()
        {
            var cph = new FakeCph { TimerRunning = true };
            UiTimer.Sync(cph, "t", true, 15, false, 0);
            Assert.DoesNotContain("disable", cph.TimerCalls);
            Assert.DoesNotContain("enable", cph.TimerCalls);

            // A random timer keeps the wait it drew, too.
            cph.TimerCalls.Clear();
            UiTimer.Sync(cph, "t", true, 15, true, 30);
            Assert.Empty(cph.TimerCalls);
        }

        [Fact]
        public void Sync_with_restart_starts_the_wait_over()
        {
            var cph = new FakeCph { TimerRunning = true };
            UiTimer.Sync(cph, "t", true, 5, true, 10, restart: true);
            Assert.Equal(new[] { "disable", "enable" }, cph.TimerCalls.Skip(1));
            Assert.InRange(cph.TimerInterval, 300, 600);
            Assert.True(cph.TimerRunning);
        }

        [Fact]
        public void Sync_switches_it_off()
        {
            var cph = new FakeCph { TimerRunning = true };
            UiTimer.Sync(cph, "t", false, 15, false, 0);
            Assert.False(cph.TimerRunning);
        }

        [Fact]
        public void A_timer_the_import_does_not_have_is_a_log_line()
        {
            var cph = new FakeCph { TimerRunning = null };
            string said = null;
            UiTimer.Sync(cph, "t", true, 15, false, 0, log: m => said = m);
            Assert.Contains("could not be set", said);
            UiTimer.Rearm(cph, "t", 15, true, 30);      // and without a log, nothing at all
        }

        [Fact]
        public void Rearm_draws_a_new_wait_for_a_random_timer_only()
        {
            var cph = new FakeCph { TimerRunning = true };
            UiTimer.Rearm(cph, "t", 15, false, 30);
            Assert.Empty(cph.TimerCalls);

            UiTimer.Rearm(cph, "t", 15, true, 30);
            Assert.InRange(cph.TimerInterval, 900, 1800);
            Assert.Equal(new[] { "disable", "enable" }, cph.TimerCalls.Skip(1));
            Assert.True(cph.TimerRunning);
        }

        [Fact]
        public void Rearm_leaves_a_timer_alone_that_was_switched_off_meanwhile()
        {
            // The tick's run was queued, a save switched the timer off, then the run arrived: it stays off.
            var cph = new FakeCph { TimerRunning = false };
            UiTimer.Rearm(cph, "t", 15, true, 30);
            Assert.Empty(cph.TimerCalls);
            Assert.False(cph.TimerRunning);
        }

        [Fact]
        public void A_save_that_switches_the_timer_off_while_Rearm_runs_leaves_it_off()
        {
            // Rearm has seen the timer on; the save turns automatic rounds off right then. Without a lock between the
            // two, Rearm switched the timer on again after the save.
            var cph = new FakeCph { TimerRunning = true };
            System.Threading.Thread save = null;
            cph.OnTimerState = () =>
            {
                if (save != null) return;
                save = new System.Threading.Thread(() => UiTimer.Sync(cph, "t", false, 15, true, 30));
                save.Start();
                save.Join(300);
            };
            UiTimer.Rearm(cph, "t", 15, true, 30);
            save.Join();
            Assert.False(cph.TimerRunning);
            Assert.Equal("disable", cph.TimerCalls.Last());
        }

        [Fact]
        public void Declared_with_key_and_default_alone_the_intervals_get_row_unit_and_switch()
        {
            var max = Conventions.Default.Complete(new Setting { Key = "autoStartIntervalMaxMinutes" });
            Assert.Equal("int", max.Kind);
            Assert.Equal("automation", max.Page);
            Assert.Equal("autoStart", max.Row);
            Assert.Equal("min", max.Unit);
            Assert.Equal("autoStartRandom", max.Requires);
            Assert.Equal(30, (int)max.Default);
            Assert.Equal("autoStartEnabled", Conventions.Default.Complete(new Setting { Key = "autoStartMinChatLines" }).Requires);

            // What the game says itself stays.
            Assert.Equal("mine", Conventions.Default.Complete(new Setting { Key = "autoStartIntervalMaxMinutes", Requires = "mine" }).Requires);
        }

        [Theory]
        [InlineData("autoStartEnabled", "bool")]
        [InlineData("autoStartIntervalMinutes", "int")]
        [InlineData("autoStartRandom", "bool")]
        [InlineData("autoStartIntervalMaxMinutes", "int")]
        [InlineData("autoStartMinChatLines", "int")]
        public void The_timer_keys_are_conventions_in_every_language(string key, string kind)
        {
            var asm = typeof(Conventions).Assembly;
            JObject Read(string name)
            {
                using (var stream = asm.GetManifestResourceStream("UIFramework.conventions." + name))
                using (var reader = new System.IO.StreamReader(stream))
                    return JObject.Parse(reader.ReadToEnd());
            }

            var declared = Read("keys.json")["keys"][key] as JObject;
            Assert.NotNull(declared);
            Assert.Equal(kind, (string)declared["kind"]);
            Assert.Equal("automation", (string)declared["page"]);
            foreach (string code in new[] { "en", "de", "es" })
            {
                var text = Read("lang." + code + ".json")["keys"][key] as JObject;
                Assert.True(text != null && !string.IsNullOrWhiteSpace((string)text["label"]) && !string.IsNullOrWhiteSpace((string)text["help"]),
                    key + " has no label and help in " + code + ".json");
            }
        }
    }
}
