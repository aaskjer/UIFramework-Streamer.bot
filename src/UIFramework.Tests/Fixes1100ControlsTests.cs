using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Newtonsoft.Json.Linq;
using Xunit;

namespace UIFramework.Tests
{
    /// <summary>1.10.0, the controls: a slider's kept value let go on the stop it sits on, decimal stops found by their
    /// number, a range slider with a decimal step, an answer panel after a failure, a paste into a list's number cell.</summary>
    public class Fixes1100ControlsTests
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

        private static IEnumerable<T> Find<T>(object node) where T : class
        {
            if (node is T hit) yield return hit;
            if (node is DependencyObject d)
                foreach (var child in LogicalTreeHelper.GetChildren(d))
                    foreach (var found in Find<T>(child))
                        yield return found;
        }

        private static List<SettingOption> Stops(params string[] values) =>
            values.Select(v => { int bar = v.IndexOf('|'); return new SettingOption { Value = bar < 0 ? v : v.Substring(0, bar), Label = bar < 0 ? v : v.Substring(bar + 1) }; }).ToList();

        // ---- A kept slider value let go on the stop it sits on --------------------------------------------------

        private static void Click(TextBlock caption) =>
            caption.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonDownEvent });

        [Fact]
        public void A_click_on_the_stop_a_kept_slider_sits_on_is_a_change() => Sta(() =>
        {
            // "medium" is no stop: the slider keeps it and sits on the first stop, unlit.
            var parts = new WindowParts();
            var fields = new Fields(parts, JObject.Parse(@"{ ""lvl"": ""medium"" }"), JObject.Parse(@"{ ""lvl"": ""off"" }"), _ => { });
            var page = new StackPanel();
            page.Children.Add(fields.SliderField("Level", "lvl", "", Stops("off|Off", "low|Low", "high|High"), 0, 2, 1, true, null));
            var slider = parts.Sliders["lvl"];
            Assert.True(slider.IsKept);
            var baseline = new Dictionary<object, string>();
            FieldBinding.Snapshot(page, baseline);
            int heard = 0;
            FieldBinding.HookChangeTracking(page, () => heard++);

            // Its caption chooses the stop: the change count, a condition and a field that asks the game hear of it.
            Click(Find<TextBlock>(page).First(t => t.Text == "Off"));
            Assert.False(slider.IsKept);
            Assert.Equal("off", slider.StoredText);
            Assert.Equal(1, heard);
            Assert.Equal(1, FieldBinding.CountChanged(page, baseline));
            // Once chosen, a second click is nothing new.
            Click(Find<TextBlock>(page).First(t => t.Text == "Off"));
            Assert.Equal(1, heard);
        });

        [Fact]
        public void A_kept_slider_moved_to_another_stop_is_one_change() => Sta(() =>
        {
            var parts = new WindowParts();
            var fields = new Fields(parts, JObject.Parse(@"{ ""lvl"": ""medium"" }"), JObject.Parse(@"{ ""lvl"": ""off"" }"), _ => { });
            var page = new StackPanel();
            page.Children.Add(fields.SliderField("Level", "lvl", "", Stops("off|Off", "low|Low", "high|High"), 0, 2, 1, true, null));
            var slider = parts.Sliders["lvl"];
            int heard = 0;
            FieldBinding.HookChangeTracking(page, () => heard++);
            Click(Find<TextBlock>(page).First(t => t.Text == "High"));
            Assert.Equal("high", slider.StoredText);
            Assert.Equal(1, heard);                      // not once for letting go and once for the move: an ask goes out once
        });

        [Fact]
        public void A_click_on_the_thumb_of_a_kept_slider_is_a_change() => Sta(() =>
        {
            var parts = new WindowParts();
            var fields = new Fields(parts, JObject.Parse(@"{ ""lvl"": ""medium"" }"), JObject.Parse(@"{ ""lvl"": ""off"" }"), _ => { });
            var page = new StackPanel();
            page.Children.Add(fields.SliderField("Level", "lvl", "", Stops("off|Off", "low|Low", "high|High"), 0, 2, 1, true, null));
            var slider = parts.Sliders["lvl"];
            int heard = 0;
            FieldBinding.HookChangeTracking(page, () => heard++);
            // A click where the thumb is moves nothing; letting go of the button chooses the stop.
            slider.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseLeftButtonUpEvent });
            Assert.False(slider.IsKept);
            Assert.Equal("off", slider.StoredText);
            Assert.Equal(1, heard);
        });

        // ---- Decimal stops found by their number ----------------------------------------------------------------

        [Fact]
        public void A_number_finds_the_stop_of_that_number_not_the_stop_at_that_index() => Sta(() =>
        {
            // The default 2.0 is a double in the schema, "2" as text: it was taken for the index 2, the stop "5.0".
            var parts = new WindowParts();
            var fields = new Fields(parts, new JObject(), JObject.Parse(@"{ ""speed"": 2.0 }"), _ => { });
            var page = new StackPanel();
            page.Children.Add(fields.SliderField("Speed", "speed", "", Stops("0.5|Slow", "2.0|Normal", "5.0|Fast"), 0, 2, 1, true, null));
            var slider = parts.Sliders["speed"];
            Assert.False(slider.IsKept);
            Assert.Equal(1, slider.Index);
            Assert.Equal("2.0", slider.StoredText);
            Assert.Empty(FieldBinding.Repaired(JObject.Parse(@"{ ""speed"": 2.0 }"), page));   // nothing for the next save to write

            // A preset's set: "0.50" is the stop "0.5"; 1 is no stop of these, and not taken for the index 1 either.
            Assert.True(slider.Put(new JValue("0.50")));
            Assert.Equal("0.5", slider.StoredText);
            Assert.False(slider.Put(new JValue(1)));
            Assert.Equal("0.5", slider.StoredText);
        });

        [Fact]
        public void Stops_that_are_no_numbers_are_still_found_by_their_index() => Sta(() =>
        {
            var slider = new OptionSlider { Options = Stops("off|Off", "low|Low", "high|High"), Minimum = 0, Maximum = 2, TickFrequency = 1 };
            Assert.True(slider.Put(new JValue(1)));
            Assert.Equal("low", slider.StoredText);
            var whole = new OptionSlider { Options = Stops("1", "2", "3"), Minimum = 0, Maximum = 2, TickFrequency = 1 };
            Assert.True(whole.Put(new JValue(2.0)));
            Assert.Equal("2", whole.StoredText);
            Assert.False(whole.Put(new JValue(0)));      // a whole-number stop: 0 is a value none of them has
        });

        // ---- A range slider over a value that is no number ------------------------------------------------------

        [Fact]
        public void A_range_slider_over_a_value_that_is_no_number_starts_on_its_default() => Sta(() =>
        {
            foreach (var bad in new[] { @"""""", @"""abc""", "[1]" })
            {
                var parts = new WindowParts();
                var settings = JObject.Parse(@"{ ""volume"": " + bad + " }");
                var fields = new Fields(parts, settings, JObject.Parse(@"{ ""volume"": 50 }"), _ => { });
                var page = new StackPanel();
                page.Children.Add(fields.SliderField("Volume", "volume", "", null, 0, 100, 1, true, null));
                Assert.Equal("50", parts.Sliders["volume"].StoredText);
                // The next save writes the default, as it does for a number box; it wrote 0.
                var saved = new JObject();
                FieldBinding.Save(saved, page);
                Assert.Equal(50L, (long)saved["volume"]);
            }

            // A number stored as text is still read, and one outside the range still pulled into it.
            var mixed = new WindowParts();
            var read = new Fields(mixed, JObject.Parse(@"{ ""a"": ""0,5"", ""b"": 7 }"), JObject.Parse(@"{ ""a"": 0.2, ""b"": 1 }"), _ => { });
            read.SliderField("A", "a", "", null, 0, 1, 0.1, false, null);
            read.SliderField("B", "b", "", null, 0, 5, 1, true, null);
            Assert.Equal("0.5", mixed.Sliders["a"].StoredText);
            Assert.Equal("5", mixed.Sliders["b"].StoredText);
        });

        // ---- An answer panel after a failure --------------------------------------------------------------------

        private static string[] Lines(StackPanel panel) => panel.Children.OfType<TextBlock>().Select(t => t.Text).ToArray();

        [Fact]
        public void An_answer_that_only_sets_fields_does_not_keep_the_failure_before_it() => Sta(() =>
        {
            // The first ask timed out, the second came back with a status: the panel said "failed" under it.
            var panel = new StackPanel();
            ResultPanel.Render(panel, "Preset: the game did not answer (timeout)", failed: true);
            ResultPanel.Render(panel, @"{ ""set"": { ""gridSize"": 8 }, ""status"": ""Preset applied."" }");
            Assert.Equal(new[] { "Preset applied." }, Lines(panel));
            Assert.NotEqual(Theme.DangerText, ((System.Windows.Media.SolidColorBrush)((TextBlock)panel.Children[0]).Foreground).Color);

            // Without a status the panel is empty, so its box hides as before the first click.
            ResultPanel.Render(panel, "Preset failed: boom", failed: true);
            ResultPanel.Render(panel, @"{ ""set"": { ""gridSize"": 8 } }");
            Assert.Empty(panel.Children);

            // An answer's own lines still stay under one that only sets fields.
            ResultPanel.Render(panel, @"{ ""lines"": [ ""27 %"" ] }");
            ResultPanel.Render(panel, @"{ ""set"": { ""gridSize"": 8 }, ""status"": ""Preset applied."" }");
            Assert.Equal(new[] { "27 %" }, Lines(panel));
        });
    }
}
