using System.Collections.Generic;
using System.Threading;
using System.Windows.Controls;
using Newtonsoft.Json.Linq;
using Xunit;

namespace UIFramework.Tests
{
    /// <summary>1.10.0: reading and writing the settings file, and an import going through the fields.</summary>
    public class Fixes1100SettingsTests
    {
        private static void Sta(System.Action body)
        {
            System.Exception failure = null;
            var thread = new Thread(() => { try { body(); } catch (System.Exception ex) { failure = ex; } });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (failure != null) throw failure;
        }

        private static (Fields Fields, WindowParts Parts, StackPanel Page) Page(string defaultsJson, string fileJson = "{}")
        {
            var parts = new WindowParts();
            var defaults = JObject.Parse(defaultsJson);
            var store = new SettingsStore(System.IO.Path.GetTempFileName(), defaults);
            return (new Fields(parts, store.Merged(JObject.Parse(fileJson)), defaults, _ => { }), parts, new StackPanel());
        }

        // ---- An import goes through a slider too ------------------------------------------------------------

        [Fact]
        public void AnImportedSliderValueIsWhatTheSliderHolds() => Sta(() =>
        {
            var (f, parts, page) = Page(@"{ ""level"": 3, ""fairness"": ""fair"", ""mood"": ""calm"" }", @"{ ""mood"": ""legacy"" }");
            var stops = new List<SettingOption>
            {
                new SettingOption { Value = "strict", Label = "Strict" },
                new SettingOption { Value = "fair", Label = "Fair" },
                new SettingOption { Value = "custom", Label = "Custom" }
            };
            var moods = new List<SettingOption> { new SettingOption { Value = "calm", Label = "Calm" }, new SettingOption { Value = "wild", Label = "Wild" } };
            page.Children.Add(f.SliderField("Level", "level", "", null, 1, 5, 1, true, null));
            page.Children.Add(f.SliderField("Fairness", "fairness", "", stops, 0, 0, 1, true, null));
            page.Children.Add(f.SliderField("Mood", "mood", "", moods, 0, 0, 1, true, null));
            Assert.True(parts.Sliders["mood"].IsKept);

            // A number beyond the range is pulled into it, as the slider shows and saves it; a fraction on a whole range
            // is its nearest stop.
            Assert.Null(FieldBinding.AsSaved(page, "level", 9, out var saved));
            Assert.Equal(5L, (long)saved);
            Assert.Null(FieldBinding.AsSaved(page, "level", "2.6", out saved));
            Assert.Equal(3L, (long)saved);
            Assert.NotNull(FieldBinding.AsSaved(page, "level", "plenty", out _));
            Assert.NotNull(FieldBinding.AsSaved(page, "level", new JArray(1, 2), out _));

            // A stop is one of the stops, whatever its case; another is refused like an option a dropdown lacks.
            Assert.Null(FieldBinding.AsSaved(page, "fairness", "Custom", out saved));
            Assert.Equal("custom", (string)saved);
            Assert.NotNull(FieldBinding.AsSaved(page, "fairness", "lenient", out _));

            // The value the slider keeps from the file is what the window saves back: an export of it imports again.
            Assert.Null(FieldBinding.AsSaved(page, "mood", "legacy", out saved));
            Assert.Equal("legacy", (string)saved);
            Assert.NotNull(FieldBinding.AsSaved(page, "mood", "furious", out _));
            Assert.Equal(1.0, parts.Sliders["fairness"].Value);   // the sliders themselves are not moved
            Assert.Equal(3.0, parts.Sliders["level"].Value);
        });
    }
}
