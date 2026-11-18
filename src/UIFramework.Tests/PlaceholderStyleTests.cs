using Xunit;

namespace UIFramework.Tests
{
    /// <summary>Messages that write their placeholders as %name% (placeholderStyle "percent"), for a whole window or
    /// for one message: the chips, the chat preview and the check of the default text go by the message's style.</summary>
    public class PlaceholderStyleTests
    {
        private static WindowSpec Prepared(string schemaJson)
        {
            try
            {
                var schema = Schema.Parse(schemaJson);
                var spec = Entry.SpecFor(schema, null);
                Entry.Prepare(spec, schema);
                return spec;
            }
            finally { Text.Use(Conventions.Default.Lang); }
        }

        private static string Schema2(string windowStyle, string postStyle, string postText = "%user% says %text%") =>
            @"{ ""host"": ""demo"", ""version"": ""v1"", ""settings"": [
                { ""key"": ""msgPost"", ""default"": """ + postText + @""", ""kind"": ""template"", ""page"": ""messages"", ""label"": ""Post"", ""placeholders"": [ ""user"", ""text"" ]" + postStyle + @" },
                { ""key"": ""msgWin"", ""default"": ""{user} wins"", ""kind"": ""template"", ""page"": ""messages"", ""label"": ""Win"", ""placeholders"": [ ""user"" ], ""placeholderStyle"": ""braces"" } ],
              ""window"": { ""title"": ""Demo""" + windowStyle + " } }";

        [Fact]
        public void APlaceholderIsWrittenAndFoundInItsStyle()
        {
            Assert.Equal("{user}", TokenStyle.Write("user", false));
            Assert.Equal("%user%", TokenStyle.Write("user", true));
            Assert.Equal(new[] { "user", "text" }, TokenStyle.In("%user%: %text% and %user% again, 50% off, 100%", true).ToArray());
            Assert.Equal(new[] { "user" }, TokenStyle.In("{user} at 50% of %text%", false).ToArray());
            Assert.True(TokenStyle.IsPercent(" Percent "));
            Assert.False(TokenStyle.IsPercent(null));
            Assert.True(TokenStyle.Known(null) && TokenStyle.Known("braces") && TokenStyle.Known("percent"));
            Assert.False(TokenStyle.Known("dollar"));
        }

        [Fact]
        public void TheWindowsStyleHoldsForEveryMessageThatSaysNoOther()
        {
            var spec = Prepared(Schema2(@", ""placeholderStyle"": ""percent""", ""));
            Assert.Empty(spec.Problems);
            var post = spec.Messages.Single(m => m.Key == "msgPost");
            var win = spec.Messages.Single(m => m.Key == "msgWin");
            Assert.True(post.Percent);
            Assert.Equal("%user%", post.Placeholder("user"));        // what the chip shows and inserts
            Assert.False(win.Percent);                               // the message's own style wins
            Assert.Equal("{user}", win.Placeholder("user"));
            // The preview fills each in its style and leaves the other's signs alone.
            var values = new Dictionary<string, string> { ["user"] = "alice", ["text"] = "hi" };
            Assert.Equal("alice says hi {user}", StandardPages.FillTemplate("%user% says %text% {user}", values, post.Percent));
            Assert.Equal("alice wins %user%", StandardPages.FillTemplate("{user} wins %user%", values, win.Percent));
        }

        [Fact]
        public void AMessageCanSayItsOwnStyle()
        {
            var spec = Prepared(Schema2("", @", ""placeholderStyle"": ""percent"""));
            Assert.Empty(spec.Problems);
            Assert.True(spec.Messages.Single(m => m.Key == "msgPost").Percent);
            Assert.False(spec.Messages.Single(m => m.Key == "msgWin").Percent);
        }

        [Fact]
        public void ThePlaceholdersOfTheDefaultTextAreCheckedInTheMessagesStyle()
        {
            // %who% is not listed; {text} is no placeholder in a percent message, so it is not asked after.
            var spec = Prepared(Schema2(@", ""placeholderStyle"": ""percent""", "", "%who% says %text% {other}"));
            var problem = Assert.Single(spec.Problems);
            Assert.Contains("'msgPost' uses %who%", problem);
        }

        [Fact]
        public void AStyleThereIsNoneOfIsNamed()
        {
            var window = Prepared(Schema2(@", ""placeholderStyle"": ""dollar""", "", "{user} says {text}"));
            Assert.Contains(window.Problems, p => p.Contains("The window has the placeholderStyle 'dollar'"));
            Assert.False(window.Messages.Single(m => m.Key == "msgPost").Percent);   // read as {name}

            var message = Prepared(Schema2("", @", ""placeholderStyle"": ""dollar""", "{user} says {text}"));
            Assert.Contains(message.Problems, p => p.Contains("'msgPost' has the placeholderStyle 'dollar'"));

            var field = Prepared(@"{ ""host"": ""demo"", ""version"": ""v1"", ""settings"": [
                { ""key"": ""bet"", ""default"": 5, ""page"": ""game"", ""placeholderStyle"": ""percent"" } ] }");
            Assert.Contains(field.Problems, p => p.Contains("'bet' has a placeholderStyle, which only a message has"));
        }
    }
}
