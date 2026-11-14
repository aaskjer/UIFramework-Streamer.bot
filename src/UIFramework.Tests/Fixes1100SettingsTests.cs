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

        // ---- A null in the file is read as the game reads it ------------------------------------------------

        [Fact]
        public void ANullInTheFileStandsForTheDefaultAsItDoesForTheGame()
        {
            var defaults = JObject.Parse(@"{ ""bet"": 50, ""useBot"": true, ""prefix"": ""> "", ""words"": [ ""apple"" ], ""none"": null }");
            var store = new SettingsStore(System.IO.Path.GetTempFileName(), defaults)
                .WithAliases(new Dictionary<string, string> { ["OldBet"] = "bet" });
            var provided = new HashSet<string>(System.StringComparer.Ordinal);
            var merged = store.Merged(JObject.Parse(@"{ ""bet"": null, ""OldBet"": 30, ""useBot"": null, ""prefix"": null, ""words"": null,
                ""none"": null, ""state"": null }"), provided);

            // UiSettings goes past a null to the old key, then to the default: so does the window.
            Assert.Equal(30, (int)merged["bet"]);
            Assert.True((bool)merged["useBot"]);
            Assert.Equal("> ", (string)merged["prefix"]);
            Assert.Equal("apple", (string)merged["words"][0]);
            // A setting without a default and a key of the game's own keep their null.
            Assert.Equal(JTokenType.Null, merged["none"].Type);
            Assert.Equal(JTokenType.Null, merged["state"].Type);
            // An import that names a setting with null brings nothing for it.
            Assert.Equal(new[] { "bet", "none", "state" }, System.Linq.Enumerable.ToArray(System.Linq.Enumerable.OrderBy(provided, k => k, System.StringComparer.Ordinal)));
        }

        [Fact]
        public void AFieldOverANullShowsTheDefault() => Sta(() =>
        {
            var (f, parts, page) = Page(@"{ ""bet"": 50, ""useBot"": true }", @"{ ""bet"": null, ""useBot"": null }");
            page.Children.Add(f.IntField("Bet", "bet", ""));
            page.Children.Add(f.Toggle("Send as bot", "useBot", ""));
            Assert.Equal("50", parts.Boxes["bet"].Text);          // it showed 0 while the game played with 50
            Assert.True(parts.Switches["useBot"].IsChecked);       // and off while the game sent as the bot
        });

        // ---- An old key's factor leaves no float noise ------------------------------------------------------

        [Fact]
        public void AnOldFractionBecomesTheRoundPercentTheBoxShows()
        {
            // Hangman's overlayBackgroundOpacity: a percent with a decimal default, read from the old fraction with 100.
            var defaults = JObject.Parse(@"{ ""overlayBackgroundOpacity"": 0.0, ""matchChance"": 18 }");
            var store = new SettingsStore(System.IO.Path.GetTempFileName(), defaults).WithAliases(
                new Dictionary<string, string> { ["OverlayBackgroundOpacity"] = "overlayBackgroundOpacity", ["Old.match"] = "matchChance" },
                new Dictionary<string, double> { ["OverlayBackgroundOpacity"] = 100, ["Old.match"] = 100 });
            var merged = store.Merged(JObject.Parse(@"{ ""OverlayBackgroundOpacity"": 0.29, ""Old"": { ""match"": 0.57 } }"));

            Assert.Equal(29.0, (double)merged["overlayBackgroundOpacity"]);   // not 28.999999999999996, which the next save wrote
            Assert.Equal(57L, (long)merged["matchChance"]);                   // a whole-number default takes a whole number, as before
            Assert.Contains("\"overlayBackgroundOpacity\": 29.0", merged.ToString());
        }
    }
}
