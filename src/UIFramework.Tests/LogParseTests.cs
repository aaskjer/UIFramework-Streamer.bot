using Xunit;

namespace UIFramework.Tests
{
    /// <summary>The Log page's reading of a game's log lines: the current format, older lines in columns, fields with
    /// a fallback key, quoted values.</summary>
    public class LogParseTests
    {
        [Fact]
        public void TheCurrentFormatReads()
        {
            var e = LogEntry.Parse("[2026-11-07 13:35:50] WIN | User: alice | Platform: twitch | Guess: high | Word: 'apfel' | Change: +1234");
            Assert.Equal(new System.DateTime(2026, 11, 7, 13, 35, 50), e.Timestamp);
            Assert.Equal("WIN", e.Result);
            Assert.Equal("alice", e.User);
            Assert.Equal("twitch", e.Platform);
            Assert.Equal("high", e.Field("Guess"));
            Assert.Equal("apfel", e.Field("Word"));   // one pair of quotes is dropped
            Assert.Equal("+1234", e.Field("change"));  // keys ignore case
        }

        [Fact]
        public void AnOlderLineInColumnsReads()
        {
            var e = LogEntry.Parse("2026-10-25 11:26:50 START       mod                         [R1|5c3fdcdc66ed4ee89648d69281406ed0] Word: 'apfel' | Category: Obst | Difficulty: 1 | Length: 5 letters");
            Assert.Equal(new System.DateTime(2026, 10, 25, 11, 26, 50), e.Timestamp);
            Assert.Equal("START", e.Result);
            Assert.Equal("mod", e.User);
            Assert.Equal("1", e.Field("Round"));
            Assert.Equal("apfel", e.Field("Word"));
            Assert.Equal("Obst", e.Field("Category"));
            Assert.Equal("5 letters", e.Field("Length"));

            var plain = LogEntry.Parse("2026-10-25 11:27:02 MISS-LTTR   alice                       Letter: 'x' | Lives: 5/6");
            Assert.Equal("MISS-LTTR", plain.Result);
            Assert.Equal("alice", plain.User);
            Assert.Equal("5/6", plain.Field("Lives"));
        }

        [Fact]
        public void AFieldCanNameSeveralKeys()
        {
            var e = LogEntry.Parse("[2026-11-07 13:35:50] GAME-OVER | User: bob | Platform: kick | Word was: apfel");
            Assert.Equal("apfel", e.Field("Word, Word was"));
            Assert.Equal("", e.Field("Nothing,Neither"));
        }

        [Fact]
        public void WhatIsNoLogLineStaysRaw()
        {
            foreach (var line in new[] { "", "hello there", "2026-13-45 99:99:99 X", "[not a time] WIN | User: a" })
            {
                var e = LogEntry.Parse(line);
                Assert.Equal(line, e.Raw);
                if (!line.StartsWith("[")) Assert.Equal(System.DateTime.MinValue, e.Timestamp);
            }
        }
    }
}
