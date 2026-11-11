using System.Linq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace UIFramework.Tests
{
    /// <summary>The data side of what the review of 2026-11-11 found and 1.8.2 put right.</summary>
    public class DataFixTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "UIFramework.Tests", "datafix-" + Guid.NewGuid().ToString("N"));

        public DataFixTests() { Directory.CreateDirectory(_folder); }
        public void Dispose() { Text.Use(Conventions.Default.Lang); }

        [Fact]
        public void AWholeNumbersLabelNamesItsUnit()
        {
            Assert.Equal("Every (min)", SchemaPage.WithUnit("Every", "min"));
            Assert.Equal("Every (min)", SchemaPage.WithUnit("Every (min)", "min"));   // the game named it already
            Assert.Equal("Every", SchemaPage.WithUnit("Every", null));
            Assert.Equal("Every", SchemaPage.WithUnit("Every", " "));
        }

        [Fact]
        public void ATextWithBrokenBracesIsShownAsItIs()
        {
            Assert.Equal("Page 3 (Ctrl+4)", Text.Fill("Page {0} (Ctrl+{1})", 3, 4));
            // A language file from outside with "{1" in it kept the window from opening (FormatException).
            Assert.Equal("{0} (Strg+{1", Text.Fill("{0} (Strg+{1", 3, 4));
            Assert.Equal("", Text.Fill(null, 1));
        }

        [Fact]
        public void AnOldNameTheGameListsItselfIsReadTheGamesWay()
        {
            // Trivia lists "AddictedPayoutCut" without a factor, and its own reader applies none; the conventions'
            // factor of 100 on it made the window show 5000 for a 50 the game used.
            var listed = Schema.Parse(@"{ ""host"": ""demo"", ""version"": ""1"", ""settings"": [
                { ""key"": ""addictedPayoutCut"", ""default"": 50.0, ""page"": ""access"", ""aliases"": [ ""AddictedPayoutCut"" ] } ] }");
            var spec = SpecJson.Parse(@"{ ""title"": ""Demo"" }", Path.Combine(_folder, "listed.json"));
            Entry.Prepare(spec, listed);
            Assert.False(spec.ResolvedSettings.Single(s => s.Key == "addictedPayoutCut").AliasFactors.ContainsKey("AddictedPayoutCut"));

            // A game that says nothing about old names gets the conventions' ones whole, factor included.
            var plain = Schema.Parse(@"{ ""host"": ""demo"", ""version"": ""1"", ""settings"": [
                { ""key"": ""addictedPayoutCut"", ""default"": 50.0, ""page"": ""access"" } ] }");
            spec = SpecJson.Parse(@"{ ""title"": ""Demo"" }", Path.Combine(_folder, "plain.json"));
            Entry.Prepare(spec, plain);
            Assert.Equal(100, spec.ResolvedSettings.Single(s => s.Key == "addictedPayoutCut").AliasFactors["AddictedPayoutCut"]);
        }

        [Fact]
        public void AnAnswersDateStaysTheTextItIs()
        {
            var rows = (JArray)JsonText.Parse(@"[ { ""when"": ""2026-11-10T20:15:00"", ""n"": 3 } ]");
            Assert.Equal(JTokenType.String, rows[0]["when"].Type);
            Assert.Equal("2026-11-10T20:15:00", (string)rows[0]["when"]);
            Assert.Equal(3, (int)rows[0]["n"]);
        }

        [Fact]
        public void TheHostBlockReadsANumberBeyondLongAsADouble()
        {
            var host = UiSettings.Parse(@"{ ""maxBet"": 99999999999999999999, ""rate"": 0.5, ""n"": 7 }");
            Assert.Equal(1e20, host.Double("maxBet"), 5);   // was an InvalidCastException inside the game's run
            Assert.Equal(0.5, host.Double("rate"));
            Assert.Equal(7.0, host.Double("n"));
        }

        [Fact]
        public void TheCurrencyNoteIsInEveryLanguage()
        {
            foreach (var code in new[] { "en", "de", "es" })
            {
                var ui = Conventions.Load(code).Lang.Ui;
                Assert.Contains("{1}", ui["currency.note"]);
                Assert.Contains("{2}", ui["currency.pill"]);
            }
            Assert.StartsWith("{0} ist installiert", Conventions.Load("de").Lang.Ui["currency.note"]);
        }
    }
}
