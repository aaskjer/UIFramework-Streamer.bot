using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Newtonsoft.Json.Linq;
using Xunit;

namespace UIFramework.Tests
{
    /// <summary>What the window shows and what it saves agree: a colour with an alpha or without its #, a number
    /// without float noise, the Log page across a reload, an emote's picture beside its own code.</summary>
    public class DisplayFixTests
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

        /// <summary>Lets the thread's dispatcher run what is queued, until the condition holds or the time is up.</summary>
        private static void Pump(int milliseconds, System.Func<bool> until = null)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            do
            {
                var frame = new DispatcherFrame();
                Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new System.Action(() => frame.Continue = false));
                Dispatcher.PushFrame(frame);
                if (until != null && until()) return;
                Thread.Sleep(10);
            }
            while (watch.ElapsedMilliseconds < milliseconds);
        }

        // ---- Colours -------------------------------------------------------------------------------------

        [Theory]
        [InlineData("#F1C40F", 0xFF, 0xF1, 0xC4, 0x0F)]
        [InlineData("F1C40F", 0xFF, 0xF1, 0xC4, 0x0F)]
        [InlineData("  #f1c40f ", 0xFF, 0xF1, 0xC4, 0x0F)]
        [InlineData("#abc", 0xFF, 0xAA, 0xBB, 0xCC)]
        [InlineData("ABC", 0xFF, 0xAA, 0xBB, 0xCC)]
        [InlineData("#80FF0000", 0x80, 0xFF, 0x00, 0x00)]
        [InlineData("80ff0000", 0x80, 0xFF, 0x00, 0x00)]
        [InlineData("#00000000", 0x00, 0x00, 0x00, 0x00)]
        public void A_hex_colour_is_read_with_three_six_or_eight_digits(string text, int a, int r, int g, int b)
        {
            Assert.True(Colours.TryParseHex(text, out var colour));
            Assert.Equal(Color.FromArgb((byte)a, (byte)r, (byte)g, (byte)b), colour);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("#")]
        [InlineData("#12")]
        [InlineData("#1234")]
        [InlineData("#12345")]
        [InlineData("#1234567")]
        [InlineData("#123456789")]
        [InlineData("#GGGGGG")]
        [InlineData("#12 456")]
        [InlineData("red")]
        public void Anything_else_is_no_hex_colour(string text) => Assert.False(Colours.TryParseHex(text, out _));

        [Fact]
        public void A_colour_is_written_with_its_alpha_only_when_it_has_one()
        {
            Assert.Equal("#FF0000", Colours.ToHex(Color.FromRgb(255, 0, 0)));
            Assert.Equal("#80FF0000", Colours.ToHex(Color.FromArgb(0x80, 255, 0, 0)));
            Assert.True(Colours.TryParseHex(Colours.ToHex(Color.FromArgb(0x0A, 1, 2, 3)), out var back));
            Assert.Equal(Color.FromArgb(0x0A, 1, 2, 3), back);
        }

        [Fact]
        public void The_swatch_shows_what_the_parser_reads() => Sta(() =>
        {
            // With the parser: a code without its # was black in the swatch though the picker and the accent took it.
            Assert.Equal(Color.FromRgb(0xF1, 0xC4, 0x0F), Colours.ParseHexBrush("F1C40F").Color);
            Assert.Equal(Color.FromRgb(0xAA, 0xBB, 0xCC), Colours.ParseHexBrush("#abc").Color);
            Assert.Equal(Color.FromArgb(0x80, 255, 0, 0), Colours.ParseHexBrush("#80FF0000").Color);
            // What the parser does not read stays as WPF reads it: a name, four digits with an alpha.
            Assert.Equal(Colors.Red, Colours.ParseHexBrush("red").Color);
            Assert.Equal(Color.FromArgb(0x88, 0xFF, 0, 0), Colours.ParseHexBrush("#8F00").Color);
            Assert.Equal(Colors.Black, Colours.ParseHexBrush("no colour").Color);
            Assert.Equal(Colors.Black, Colours.ParseHexBrush("").Color);
            Assert.Equal(Colors.Black, Colours.ParseHexBrush(null).Color);

            // A colour field's swatch follows its box.
            var parts = new WindowParts();
            var fields = new Fields(parts, JObject.Parse(@"{ ""c"": ""#80FF0000"" }"), new JObject(), _ => { });
            var row = fields.ColourField("Colour", "c", "", labelled: false);
            var swatch = Find<Border>(row).First(b => b.Background is SolidColorBrush && b.Width == 28);
            Assert.Equal(Color.FromArgb(0x80, 255, 0, 0), ((SolidColorBrush)swatch.Background).Color);
            parts.Boxes["c"].Text = "2ECC71";
            Assert.Equal(Color.FromRgb(0x2E, 0xCC, 0x71), ((SolidColorBrush)swatch.Background).Color);
        });

        [Fact]
        public void An_accent_with_an_alpha_is_applied_opaque() => Sta(() =>
        {
            var was = Theme.AccentOverride;
            try
            {
                Assert.True(Colours.TryParseHex("#80FF0000", out var typed));
                Theme.AccentOverride = typed;
                Assert.Equal(Color.FromRgb(255, 0, 0), Theme.AccentOverride);
                Assert.Equal(Color.FromRgb(255, 0, 0), Theme.Accent);
                Theme.AccentOverride = null;
                Assert.Null(Theme.AccentOverride);
            }
            finally { Theme.AccentOverride = was; }
        });

        [Fact]
        public void The_picker_opens_on_the_stored_colour_and_keeps_its_alpha() => Sta(() =>
        {
            var box = new TextBox { Text = "#80FF0000" };
            var picker = new ColourPicker();
            picker.Open(box, box);
            var popup = (Popup)typeof(ColourPicker).GetField("_popup", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(picker);
            try
            {
                Assert.True(picker.IsOpen);
                Assert.Equal("#80FF0000", box.Text);   // opening writes nothing
                // The picker stands on the stored red, not on its fallback yellow.
                Assert.Contains(Find<TextBlock>(popup.Child), t => t.Text == "#80FF0000");
                Assert.DoesNotContain(Find<TextBlock>(popup.Child), t => t.Text == "#F1C40F");

                void Wheel(string slider) => Find<Canvas>(popup.Child).Single(c => c.Name == slider)
                    .RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, 0, 120) { RoutedEvent = UIElement.MouseWheelEvent });

                // One notch on the green slider: green goes from 0 to 1, the alpha stays.
                Wheel("SliderG");
                Assert.Equal("#80FF0100", box.Text);

                // An alpha typed while the picker is open is the one it writes from then on; none typed is opaque.
                box.Text = "#40112233";
                Wheel("SliderR");
                Assert.Equal("#40122233", box.Text);
                box.Text = "#112233";
                Wheel("SliderB");
                Assert.Equal("#112234", box.Text);
            }
            finally { popup.IsOpen = false; }
        });

        // ---- Numbers -------------------------------------------------------------------------------------

        [Fact]
        public void A_decimal_slider_shows_and_saves_its_stop_without_float_noise() => Sta(() =>
        {
            var parts = new WindowParts();
            var fields = new Fields(parts, JObject.Parse(@"{ ""chance"": 0.5, ""level"": 2 }"), new JObject(), _ => { });
            var page = new StackPanel();
            page.Children.Add(fields.SliderField("Chance", "chance", "", null, 0, 1, 0.1, false, null));
            page.Children.Add(fields.SliderField("Level", "level", "", null, 1, 5, 1, true, null));
            var chance = parts.Sliders["chance"];
            var level = parts.Sliders["level"];

            // The third stop, as the slider snaps to it: three steps of 0.1 are not 0.3.
            chance.Value = chance.Minimum + 3 * chance.TickFrequency;
            Assert.NotEqual(0.3, chance.Value);
            Assert.Equal(0.3, (double)chance.Stored);
            Assert.Equal("0.3", chance.StoredText);
            Assert.Contains(Find<TextBlock>(page), t => t.Text == "Chance  ·  0.3");
            Assert.Equal(0.3, fields.LiveNumber("chance", 0));

            level.Value = 3;
            var saved = new JObject();
            FieldBinding.Save(saved, page);
            Assert.Equal(@"{""chance"":0.3,""level"":3}", saved.ToString(Newtonsoft.Json.Formatting.None));
            Assert.Equal(JTokenType.Float, saved["chance"].Type);
            Assert.Equal(JTokenType.Integer, saved["level"].Type);   // a whole-number slider still saves a whole number

            // Every stop of the range reads as it is written.
            for (int stop = 0; stop <= 10; stop++)
            {
                chance.Value = chance.Minimum + stop * chance.TickFrequency;
                Assert.Equal((stop / 10m).ToString(CultureInfo.InvariantCulture), chance.StoredText);
            }
            // A whole stop of a decimal range stays a decimal: 1, saved as 1.0.
            Assert.Equal(JTokenType.Float, chance.Stored.Type);
            Assert.Equal(1.0, (double)chance.Stored);
        });

        [Theory]
        [InlineData("35", 0.35)]
        [InlineData("41", 0.41)]
        [InlineData("47", 0.47)]
        [InlineData("57", 0.57)]
        [InlineData("69", 0.69)]
        [InlineData("70", 0.7)]
        [InlineData("82", 0.82)]
        [InlineData("83", 0.83)]
        [InlineData("12.345", 0.12345)]
        [InlineData("0.5", 0.005)]
        [InlineData("100", 1.0)]
        [InlineData("0", 0.0)]
        public void A_percent_is_saved_without_float_noise(string typed, double stored) => Sta(() =>
        {
            var parts = new WindowParts();
            var fields = new Fields(parts, JObject.Parse(@"{ ""p"": 0.1 }"), new JObject(), _ => { });
            var page = new StackPanel();
            page.Children.Add(fields.PercentField("P", "p", 0.01, ""));
            parts.Boxes["p"].Text = typed;
            var saved = new JObject();
            FieldBinding.Save(saved, page);
            Assert.Equal(stored, (double)saved["p"]);   // exactly: 35 is 0.35, not 0.35000000000000003
            Assert.Equal(stored.ToString("R", CultureInfo.InvariantCulture), FieldBinding.Invariant(saved["p"]));
        });

        [Fact]
        public void Every_percent_to_a_tenth_is_stored_as_it_is_written()
        {
            // Rounding to ten decimals gives the number the text names, for every value a percent box takes.
            var percent = new FieldTag("p", FieldKind.Double) { Factor = 0.01, Min = 0, Max = 100 };
            for (int tenths = 0; tenths <= 1000; tenths++)
            {
                string typed = (tenths / 10m).ToString(CultureInfo.InvariantCulture);
                double expected = double.Parse((tenths / 1000m).ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
                Assert.Equal(expected.ToString("R", CultureInfo.InvariantCulture), FieldBinding.StoredText(percent, typed));
            }
        }

        [Fact]
        public void Whole_numbers_and_plain_decimals_are_saved_as_typed() => Sta(() =>
        {
            var parts = new WindowParts();
            var fields = new Fields(parts, JObject.Parse(@"{ ""n"": 1, ""big"": 1, ""d"": 0.5, ""far"": 0.5, ""wait"": 60 }"), new JObject(), _ => { });
            var page = new StackPanel();
            page.Children.Add(fields.IntField("N", "n", ""));
            page.Children.Add(fields.LongField("Big", "big", ""));
            page.Children.Add(fields.DoubleField("D", "d", ""));
            page.Children.Add(fields.DoubleField("Far", "far", ""));
            page.Children.Add(fields.DurationField("Wait", "wait", "", "min"));
            parts.Boxes["n"].Text = "35";
            parts.Boxes["big"].Text = "9007199254740993";       // past 2^53: a double would lose the last digit
            parts.Boxes["d"].Text = "0.1234567890123";          // more than ten decimals: no factor, no rounding
            parts.Boxes["far"].Text = "1234567.1";
            parts.Boxes["wait"].Text = "1.5";
            var saved = new JObject();
            FieldBinding.Save(saved, page);
            Assert.Equal(JTokenType.Integer, saved["n"].Type);
            Assert.Equal(35, (int)saved["n"]);
            Assert.Equal(JTokenType.Integer, saved["big"].Type);
            Assert.Equal(9007199254740993L, (long)saved["big"]);
            Assert.Equal(0.1234567890123, (double)saved["d"]);
            Assert.Equal(1234567.1, (double)saved["far"]);
            Assert.Equal(JTokenType.Integer, saved["wait"].Type);
            Assert.Equal(90, (int)saved["wait"]);
        });

        // ---- The Log page --------------------------------------------------------------------------------

        private sealed class LogHooks : NoHooks
        {
            public List<string> Lines = new List<string>();
            // New entries with every read, as a game reading its file again hands over.
            public override IEnumerable<LogEntry> ReadLog() => Lines.Select(LogEntry.Parse).ToList();
        }

        private sealed class QuietLog : IHostLog
        {
            public void Info(string message) { }
            public void Warn(string message) { }
            public void Error(string message) { }
        }

        private static string Line(int second, string result) =>
            "[2026-11-07 13:" + (second / 60).ToString("00") + ":" + (second % 60).ToString("00") + "] " + result + " | User: user" + second + " | Platform: twitch";

        private static FrameworkElement LogPageOf(LogHooks hooks, params string[] results)
        {
            var spec = new WindowSpec();
            spec.Log.Results.AddRange(results);
            var ctx = new PageContext();
            typeof(PageContext).GetProperty("Spec").SetValue(ctx, spec);
            typeof(PageContext).GetProperty("Hooks").SetValue(ctx, hooks);
            typeof(PageContext).GetProperty("Log").SetValue(ctx, new QuietLog());
            return (FrameworkElement)LogPage.Build(ctx);
        }

        private static void ClickReload(FrameworkElement page) =>
            Find<Button>(page).Single(b => AutomationProperties.GetName(b) == Text.T("log.reload")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

        private static ComboBox Combo(FrameworkElement page, string name) =>
            Find<ComboBox>(page).Single(c => AutomationProperties.GetName(c) == Text.T(name));

        [Fact]
        public void A_reload_keeps_the_picked_entry_its_detail_line_and_the_scroll_position() => Sta(() =>
        {
            var hooks = new LogHooks();
            for (int i = 0; i < 80; i++) hooks.Lines.Add(Line(i, i % 2 == 0 ? "WIN" : "LOSS"));
            var page = LogPageOf(hooks, "Win", "Loss");
            var window = new Window { Width = 700, Height = 420, Content = page, ShowInTaskbar = false, ShowActivated = false, Left = -20000, Top = -20000, WindowStartupLocation = WindowStartupLocation.Manual };
            window.Show();
            try
            {
                var grid = Find<DataGrid>(page).Single();
                var details = Find<TextBox>(page).Single(t => AutomationProperties.GetName(t) == Text.T("log.line"));
                grid.UpdateLayout();
                var viewer = (ScrollViewer)grid.Template.FindName("DG_ScrollViewer", grid);

                string raw = Line(40, "WIN");
                var picked = grid.Items.OfType<LogEntry>().Single(e => e.Raw == raw);
                grid.SelectedItem = picked;
                viewer.ScrollToVerticalOffset(30);
                Pump(200);
                Assert.Equal(30, viewer.VerticalOffset);
                Assert.Equal(raw, details.Text);

                // A round goes on in chat: a new line, and the log is read again.
                hooks.Lines.Add(Line(80, "WIN"));
                ClickReload(page);
                Pump(200);
                Assert.Equal(81, grid.Items.Count);
                var again = Assert.IsType<LogEntry>(grid.SelectedItem);
                Assert.NotSame(picked, again);
                Assert.Equal(raw, again.Raw);
                Assert.Equal(raw, details.Text);
                Assert.Equal(30, viewer.VerticalOffset);

                // With a filter that still shows the entry, the same.
                Combo(page, "log.result").SelectedItem = "Win";
                grid.SelectedItem = grid.Items.OfType<LogEntry>().Single(e => e.Raw == raw);
                ClickReload(page);
                Pump(200);
                Assert.Equal("Win", Combo(page, "log.result").SelectedItem);
                Assert.Equal(41, grid.Items.Count);
                Assert.Equal(raw, (grid.SelectedItem as LogEntry)?.Raw);
                Assert.Equal(raw, details.Text);

                // The entry is gone from the log (a new day's file): nothing is picked, the detail line is empty.
                hooks.Lines.Remove(raw);
                ClickReload(page);
                Pump(200);
                Assert.Null(grid.SelectedItem);
                Assert.Equal("", details.Text);
            }
            finally { window.Close(); }
        });

        [Fact]
        public void The_result_filter_follows_the_log_when_the_game_declares_no_results() => Sta(() =>
        {
            var hooks = new LogHooks();
            var page = LogPageOf(hooks);
            var results = Combo(page, "log.result");
            var grid = Find<DataGrid>(page).Single();
            string all = Text.T("log.allResults");
            Assert.Equal(new[] { all }, results.Items.Cast<string>().ToArray());   // an empty log when the window opens

            hooks.Lines.Add(Line(1, "WIN"));
            hooks.Lines.Add(Line(2, "LOSS"));
            hooks.Lines.Add(Line(3, "WIN"));
            ClickReload(page);
            Assert.Equal(new[] { all, "Loss", "Win" }, results.Items.Cast<string>().ToArray());
            Assert.Equal(all, results.SelectedItem);
            Assert.Equal(3, grid.Items.Count);

            // The choice is kept across a reload, and still filters.
            results.SelectedItem = "Win";
            Assert.Equal(2, grid.Items.Count);
            hooks.Lines.Add(Line(4, "PUSH"));
            hooks.Lines.Add(Line(5, "WIN"));
            ClickReload(page);
            Assert.Equal(new[] { all, "Loss", "Push", "Win" }, results.Items.Cast<string>().ToArray());
            Assert.Equal("Win", results.SelectedItem);
            Assert.Equal(3, grid.Items.Count);

            // A result the log no longer holds falls back to all results, as a day does.
            hooks.Lines.RemoveAll(l => l.Contains("] WIN"));
            ClickReload(page);
            Assert.Equal(new[] { all, "Loss", "Push" }, results.Items.Cast<string>().ToArray());
            Assert.Equal(all, results.SelectedItem);
            Assert.Equal(2, grid.Items.Count);
        });

        [Fact]
        public void Declared_results_stay_as_declared_across_a_reload() => Sta(() =>
        {
            var hooks = new LogHooks();
            hooks.Lines.Add(Line(1, "WIN"));
            var page = LogPageOf(hooks, "Win", "Loss", "Push");
            var results = Combo(page, "log.result");
            string all = Text.T("log.allResults");
            Assert.Equal(new[] { all, "Win", "Loss", "Push" }, results.Items.Cast<string>().ToArray());
            results.SelectedItem = "Loss";
            hooks.Lines.Add(Line(2, "BUST"));
            ClickReload(page);
            Assert.Equal(new[] { all, "Win", "Loss", "Push" }, results.Items.Cast<string>().ToArray());
            Assert.Equal("Loss", results.SelectedItem);
        });

        // ---- Emote pictures ------------------------------------------------------------------------------

        [Fact]
        public void An_ffz_emote_drawn_in_one_size_only_has_its_picture()
        {
            // FrankerFaceZ answers "2x": null for an emote that has no larger picture.
            const string answer = @"[
                { ""id"": 1, ""code"": ""OnlySmall"", ""images"": { ""1x"": ""https://cdn.frankerfacez.com/emote/1/1"", ""2x"": null, ""4x"": null } },
                { ""id"": 2, ""code"": ""Both"", ""images"": { ""1x"": ""https://cdn.frankerfacez.com/emote/2/1"", ""2x"": ""https://cdn.frankerfacez.com/emote/2/2"" } },
                { ""id"": 3, ""code"": ""EmptyLarge"", ""images"": { ""1x"": ""https://cdn.frankerfacez.com/emote/3/1"", ""2x"": """" } },
                { ""id"": 4, ""code"": ""NoLarge"", ""images"": { ""1x"": ""https://cdn.frankerfacez.com/emote/4/1"" } },
                { ""id"": 5, ""code"": ""Neither"", ""images"": { ""1x"": null, ""2x"": null } },
                { ""id"": 6, ""code"": ""NoImages"" }
            ]";
            var into = new Dictionary<string, (string Url, string Source)>();
            typeof(EmotePictures).GetMethod("Read", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { "ffz", answer, into });
            Assert.Equal(("https://cdn.frankerfacez.com/emote/1/1", "FFZ"), into["OnlySmall"]);
            Assert.Equal(("https://cdn.frankerfacez.com/emote/2/2", "FFZ"), into["Both"]);
            Assert.Equal(("https://cdn.frankerfacez.com/emote/3/1", "FFZ"), into["EmptyLarge"]);
            Assert.Equal(("https://cdn.frankerfacez.com/emote/4/1", "FFZ"), into["NoLarge"]);
            Assert.False(into.ContainsKey("Neither"));
            Assert.False(into.ContainsKey("NoImages"));
        }

        [Fact]
        public void A_picture_that_arrives_late_is_not_set_beside_another_code() => Sta(() =>
        {
            // The downloads are held back here instead of asked of the web: each ends when the test says so.
            var pictures = (ConcurrentDictionary<string, Task<ImageSource>>)typeof(EmotePictures).GetField("Pictures", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            const string firstCode = "990000001:first", secondCode = "[emote:990000002:second]";
            const string firstUrl = "https://files.kick.com/emotes/990000001/fullsize", secondUrl = "https://files.kick.com/emotes/990000002/fullsize";
            var first = new TaskCompletionSource<ImageSource>();
            var second = new TaskCompletionSource<ImageSource>();
            pictures[firstUrl] = first.Task;
            pictures[secondUrl] = second.Task;
            try
            {
                ImageSource Dot()
                {
                    var dot = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[4], 4);
                    dot.Freeze();
                    return dot;
                }
                ImageSource firstPicture = Dot(), secondPicture = Dot();

                var image = EmotePictures.Picture("kick", firstCode, null);
                Assert.Equal(firstCode, image.Tag);
                // The box is changed before the first picture is there.
                EmotePictures.Show(image, "kick", " " + secondCode + " ", null);
                Assert.Equal(secondCode, image.Tag);

                first.SetResult(firstPicture);
                Pump(300, () => image.Source != null);
                Assert.Null(image.Source);              // the first code's picture is late: not shown beside the second

                second.SetResult(secondPicture);
                Pump(3000, () => image.Source != null);
                Assert.Same(secondPicture, image.Source);

                // The first code again: its picture is known by now and shows at once.
                EmotePictures.Show(image, "kick", firstCode, null);
                Assert.Same(firstPicture, image.Source);

                // An emptied box clears the picture and what the image stands for.
                EmotePictures.Show(image, "kick", "", null);
                Assert.Null(image.Source);
                Assert.Equal("", image.Tag);
            }
            finally
            {
                pictures.TryRemove(firstUrl, out _);
                pictures.TryRemove(secondUrl, out _);
            }
        });
    }
}
