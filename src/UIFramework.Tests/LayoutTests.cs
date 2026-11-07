using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace UIFramework.Tests
{
    public class LayoutTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "UIFramework.Tests", Guid.NewGuid().ToString("N"));
        private string SettingsPath => Path.Combine(_folder, "settings.json");

        public LayoutTests() { Directory.CreateDirectory(_folder); }
        public void Dispose() { try { Directory.Delete(_folder, true); } catch { } }

        private const string SchemaJson = @"{ ""host"": ""demo"", ""settings"": [
            { ""key"": ""a"", ""default"": 1, ""page"": ""one"" },
            { ""key"": ""b"", ""default"": 2, ""page"": ""one"" },
            { ""key"": ""c"", ""default"": 3, ""page"": ""two"" },
            { ""key"": ""d"", ""default"": true, ""page"": ""two"" } ] }";

        private WindowSpec Resolve(string specJson = @"{ ""pages"": [""one"", ""two""] }", bool customizing = false)
        {
            var spec = SpecJson.Parse(specJson, SettingsPath);
            spec.Customizing = customizing;
            Entry.Prepare(spec, Schema.Parse(SchemaJson));
            return spec;
        }

        [Fact]
        public void LayoutFileWritesAndPrunesOverrides()
        {
            var layout = LayoutFile.Load(SettingsPath);
            Assert.Equal(0, layout.ChangeCount);
            layout.SetPage("one", "title", "First");
            layout.SetField("a", "hidden", true);
            layout.SetField("b", "order", 5);
            Assert.Equal(3, layout.ChangeCount);
            layout.Save();

            var again = LayoutFile.Load(SettingsPath);
            Assert.Equal("First", again.Page("one").Title);
            Assert.True(again.Field("a").Hidden);
            Assert.Equal(5, again.Field("b").Order);

            again.SetField("a", "hidden", null);
            Assert.Null(again.Field("a"));          // an empty override is pruned
            again.ResetLayout();
            Assert.Equal(0, again.ChangeCount);
            again.Accent = "#123456";
            again.ResetLayout();
            Assert.Equal("#123456", again.Accent);  // the theme survives a layout reset
        }

        [Fact]
        public void FrameworkNotesRoundTripAndParse()
        {
            var layout = LayoutFile.Load(SettingsPath);
            Assert.Null(layout.FrameworkAvailable);
            layout.FrameworkAvailable = "v0.3.1";
            layout.Save();
            var again = LayoutFile.Load(SettingsPath);
            Assert.Equal("v0.3.1", again.FrameworkAvailable);
            Assert.Equal(new Version(0, 3, 1), LayoutFile.ParseVersion(again.FrameworkAvailable));
            Assert.True(LayoutFile.ParseVersion(again.FrameworkAvailable) > LayoutFile.ParseVersion("0.3.0"));
            again.FrameworkAvailable = " ";
            again.Save();
            Assert.Null(LayoutFile.Load(SettingsPath).FrameworkAvailable);
            Assert.Null(LayoutFile.ParseVersion("later"));
        }

        [Fact]
        public void LayoutRenamesMovesHidesAndOrders()
        {
            var layout = LayoutFile.Load(SettingsPath);
            layout.SetPage("one", "title", "Erste");
            layout.SetPage("one", "icon", "dice");
            layout.SetField("c", "page", "one");
            layout.SetField("c", "label", "See");
            layout.SetField("a", "hidden", true);
            layout.SetPage("two", "order", 0);
            layout.SetPage("one", "order", 10);
            layout.Save();

            var spec = Resolve();
            Assert.Equal(new[] { "two", "one", "about" }, spec.Pages.Select(p => p.Id).ToArray());
            var one = spec.Pages.First(p => p.Id == "one");
            Assert.Equal("Erste", one.Title);
            Assert.Equal("🎲", one.Glyph);
            var c = spec.ResolvedSettings.First(s => s.Key == "c");
            Assert.Equal("one", c.Page);
            Assert.Equal("See", c.Label);
            Assert.True(spec.ResolvedSettings.First(s => s.Key == "a").Hidden);
        }

        [Fact]
        public void APagePatternLeavesAFieldTheUserMovedWhereTheUserPutIt()
        {
            var layout = LayoutFile.Load(SettingsPath);
            layout.SetField("c", "page", "two");
            layout.Save();

            var spec = Resolve(@"{ ""pages"": [ { ""id"": ""one"", ""fields"": [""*""] }, ""two"" ] }");
            var byKey = spec.ResolvedSettings.ToDictionary(s => s.Key);
            Assert.Equal("two", byKey["c"].Page);   // the user's move wins over the pattern
            Assert.Equal("one", byKey["d"].Page);   // claimed: it sits on the claiming page and says so
            Assert.Contains(spec.Pages, p => p.Id == "two");
        }

        [Fact]
        public void HiddenPagesStayOutUnlessCustomising()
        {
            var layout = LayoutFile.Load(SettingsPath);
            layout.SetPage("two", "hidden", true);
            layout.SetPage("about", "hidden", true);   // never honoured
            layout.Save();

            var normal = Resolve();
            Assert.DoesNotContain(normal.Pages, p => p.Id == "two");
            Assert.Contains(normal.Pages, p => p.Id == "about");

            var customising = Resolve(customizing: true);
            var two = customising.Pages.First(p => p.Id == "two");
            Assert.True(two.Hidden);
            Assert.False(customising.Pages.First(p => p.Id == "about").Hidden);
        }

        [Fact]
        public void PrepareKeepsSchemaAndLayoutOnTheSpec()
        {
            var spec = Resolve();
            Assert.NotNull(spec.Schema);
            Assert.NotNull(spec.Layout);
            Assert.Equal(Path.Combine(_folder, LayoutFile.FileName), spec.Layout.Path);
            // Resolving again after a layout change picks it up.
            spec.Layout.SetField("b", "hidden", true);
            spec.Layout.Save();
            Entry.Prepare(spec, spec.Schema);
            Assert.True(spec.ResolvedSettings.First(s => s.Key == "b").Hidden);
        }
    }
}
