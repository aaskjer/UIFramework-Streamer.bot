using System;
using System.IO;
using Newtonsoft.Json.Linq;
using Xunit;

namespace UIFramework.Tests
{
    public class ModelTests
    {
        [Fact]
        public void LogEntryParsesTheGamesFormat()
        {
            var entry = LogEntry.Parse("[2026-10-31 19:05:39] DUEL WIN | User: alice | Platform: twitch | Bet: 500 | Change: +500");
            Assert.Equal(new DateTime(2026, 10, 31, 19, 5, 39), entry.Timestamp);
            Assert.Equal("DUEL WIN", entry.Result);
            Assert.Equal("alice", entry.User);
            Assert.Equal("twitch", entry.Platform);
            Assert.Equal("500", entry.Field("Bet"));
            Assert.Equal("+500", entry.Field("change"));
            Assert.Contains("alice", entry.SearchBlob);
        }

        [Fact]
        public void LogEntryKeepsUnreadableLinesRaw()
        {
            var entry = LogEntry.Parse("garbage without a stamp");
            Assert.Equal(DateTime.MinValue, entry.Timestamp);
            Assert.Equal("garbage without a stamp", entry.Raw);
            Assert.Equal("", entry.Result);
        }

        [Theory]
        [InlineData("v1.2.3", "v1.2.2", true)]
        [InlineData("v1.2.3", "v1.2.3", false)]
        [InlineData("v1.2.3-beta.1", "v1.2.3", false)]
        [InlineData("v2", "v1.9.9", true)]
        [InlineData("not a version", "v1.0.0", false)]
        [InlineData("v16.3.1", "v16.10.0", false)]
        public void NewerVersionComparesNumbersNotStrings(string latest, string current, bool newer)
        {
            Assert.Equal(newer, Web.IsNewerVersion(latest, current));
        }

        [Fact]
        public void LayoutFileAdoptsThemeFromHostOnce()
        {
            string folder = Path.Combine(Path.GetTempPath(), "UIFramework.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                string settingsPath = Path.Combine(folder, "host.json");
                var layout = LayoutFile.Load(settingsPath);
                var host = JObject.Parse(@"{ ""DarkMode"": false, ""Egg"": true, ""other"": 1 }");
                Assert.True(layout.AdoptFromHost(host, "darkMode", "DarkMode"));
                Assert.False(layout.Dark);
                Assert.Null(host["DarkMode"]);
                Assert.Null(host["Egg"]);
                Assert.Equal(1, (int)host["other"]);

                layout.Accent = "#7C3AED";
                layout.Save();
                var again = LayoutFile.Load(settingsPath);
                Assert.False(again.Dark);
                Assert.Equal("#7C3AED", again.Accent);
                Assert.True(again.AutoUpdate);

                // A later host file with the key again does not override what the user set since.
                var host2 = JObject.Parse(@"{ ""DarkMode"": true }");
                again.AdoptFromHost(host2, "DarkMode");
                Assert.False(again.Dark);
            }
            finally { try { Directory.Delete(folder, true); } catch { } }
        }

        [Fact]
        public void ColoursRoundTrip()
        {
            Assert.True(Colours.TryParseHex("#1E90FF", out var c));
            Assert.Equal("#1E90FF", Colours.ToHex(c));
            Colours.RgbToHsv(c, out double h, out double s, out double v);
            var back = Colours.HsvToRgb(h, s, v);
            Assert.InRange(Math.Abs(back.R - c.R), 0, 1);
            Assert.InRange(Math.Abs(back.G - c.G), 0, 1);
            Assert.InRange(Math.Abs(back.B - c.B), 0, 1);
            Assert.False(Colours.TryParseHex("#12", out _));
        }

        [Fact]
        public void FillTemplateReplacesOnlyKnownTokens()
        {
            var filled = StandardPages.FillTemplate("@{user} wins {amount} {points} {unknown}",
                new System.Collections.Generic.Dictionary<string, string> { { "user", "alice" }, { "amount", "5" }, { "points", "P" } });
            Assert.Equal("@alice wins 5 P {unknown}", filled);
        }
    }
}
