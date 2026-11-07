using System.Linq;
using Xunit;

namespace UIFramework.Tests
{
    /// <summary>The About page's own links come from conventions/keys.json and name nobody in particular.</summary>
    public class CreditsTests
    {
        [Fact]
        public void TheDefaultLinksAreTheFrameworkStreamerBotAndTheIssues()
        {
            var links = Conventions.Default.AboutLinks;
            Assert.Equal(new[] { "about.framework", "Streamer.bot", "about.report" }, links.Select(l => l.Text).ToArray());
            Assert.Equal("https://aaskjer.github.io/projects/uiframework/", links[0].Url);
            Assert.Equal("https://streamer.bot", links[1].Url);
            Assert.Equal("{issues}", links[2].Url);
            Assert.EndsWith("/issues", Conventions.Default.FrameworkIssues);
            Assert.DoesNotContain(links, l => l.Url.Contains("twitch.tv"));
        }

        [Fact]
        public void AWindowNamesNoCreditsOfItsOwn()
        {
            var spec = SpecJson.Parse(@"{ ""title"": ""Demo"" }", System.IO.Path.GetTempFileName());
            Assert.Null(spec.About.Credits);
            Assert.True(spec.About.ShowDefaultLinks);
            var off = SpecJson.Parse(@"{ ""title"": ""Demo"", ""about"": { ""credits"": ""By someone"", ""defaultLinks"": false } }", System.IO.Path.GetTempFileName());
            Assert.Equal("By someone", off.About.Credits);
            Assert.False(off.About.ShowDefaultLinks);
        }
    }
}
