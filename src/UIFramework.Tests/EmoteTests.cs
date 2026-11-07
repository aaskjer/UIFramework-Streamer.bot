using Xunit;

namespace UIFramework.Tests
{
    /// <summary>Emote codes as the platforms spell them: what a picture is looked up for and what is named beneath.</summary>
    public class EmoteTests
    {
        [Theory]
        [InlineData("[emote:1730803:emojiRich]", "1730803", "emojiRich")]
        [InlineData("1730803:emojiRich", "1730803", "emojiRich")]
        public void A_kick_code_is_read_in_both_spellings(string code, string id, string name)
        {
            var m = EmotePictures.KickCode.Match(code);
            Assert.True(m.Success);
            Assert.Equal(id, m.Groups[1].Value);
            Assert.Equal(name, m.Groups[2].Value);
        }

        [Theory]
        [InlineData("Kappa")]
        [InlineData(":face-blue-smiling:")]
        [InlineData("[emote:x:emojiRich]")]
        public void Anything_else_is_no_kick_code(string code) => Assert.False(EmotePictures.KickCode.IsMatch(code));

        [Fact]
        public void Names_come_from_the_code()
        {
            Assert.Equal("emojiRich", EmotePictures.NameOf("[emote:1730803:emojiRich]"));
            Assert.Equal("emojiRich", EmotePictures.NameOf("1730803:emojiRich"));
            Assert.Equal("face-purple-rain-drops", EmotePictures.NameOf(":face-purple-rain-drops:"));
            Assert.Equal("Kappa", EmotePictures.NameOf("Kappa"));
            Assert.Equal("🍒", EmotePictures.NameOf("🍒"));
        }

        [Fact]
        public void Youtubes_own_emojis_have_a_picture_in_the_embedded_table()
        {
            string url = EmotePictures.YouTubeUrl(":face-purple-rain-drops:", null);
            Assert.NotNull(url);
            Assert.StartsWith("https://yt3.ggpht.com/", url);
            Assert.EndsWith("=w48-h48-c-k-nd", url);
            Assert.Null(EmotePictures.YouTubeUrl("🍒", null));
            Assert.Null(EmotePictures.YouTubeUrl(":no-such-emoji:", null));
        }

        [Theory]
        [InlineData("Kappa", "25")]
        [InlineData("PogChamp", "305954156")]
        [InlineData("LUL", "425618")]
        [InlineData("FailFish", "360")]
        public void Twitchs_global_emotes_have_their_id_in_the_embedded_table(string name, string id)
        {
            Assert.Equal(id, EmotePictures.TwitchGlobalTable()[name]);
        }

        [Fact]
        public void The_twitch_table_tells_names_apart_by_case()
        {
            var table = EmotePictures.TwitchGlobalTable();
            Assert.True(table.Count > 200);
            Assert.NotEqual(table[":p"], table[":P"]);
            Assert.False(table.ContainsKey("kappa"));
        }

        [Fact]
        public void Without_a_twitch_token_a_global_emote_still_has_its_picture()
        {
            // A game whose Streamer.bot has no Twitch login hands over no token: Kappa comes from the embedded list.
            var lookup = EmotePictures.TwitchLookup(new SourcesWithoutToken()).GetAwaiter().GetResult();
            Assert.True(lookup.TryGetValue("Kappa", out var kappa));
            Assert.Equal("https://static-cdn.jtvnw.net/emoticons/v2/25/default/{theme}/2.0", kappa.Url);
            Assert.Null(kappa.Source);
        }

        private sealed class SourcesWithoutToken : NoHooks
        {
            public override string EmoteSources() => "{\"twitchUserId\":null,\"clientId\":null,\"token\":null}";
        }
    }
}
