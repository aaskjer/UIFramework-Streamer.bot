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
    }
}
