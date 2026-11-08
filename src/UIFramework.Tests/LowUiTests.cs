using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Newtonsoft.Json.Linq;
using Xunit;

namespace UIFramework.Tests
{
    /// <summary>The window's small things: percent decimals, a kept slider value, numbers typed and set on any
    /// Windows, a row kept together in the customise mode, a layout file another program holds, its own texts.</summary>
    public class LowUiTests
    {
        private static void Sta(System.Action body, string culture = null)
        {
            System.Exception failure = null;
            var thread = new Thread(() =>
            {
                if (culture != null) Thread.CurrentThread.CurrentCulture = new CultureInfo(culture);
                try { body(); } catch (System.Exception ex) { failure = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (failure != null) throw failure;
        }

        private static string Folder()
        {
            string folder = Path.Combine(Path.GetTempPath(), "UIFramework.Tests", "lowui-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            return folder;
        }

        private static void Leave(UIElement element) => element.RaiseEvent(new RoutedEventArgs(UIElement.LostFocusEvent));

        private static IEnumerable<T> Find<T>(object node) where T : class
        {
            if (node is T hit) yield return hit;
            if (node is DependencyObject d)
                foreach (var child in LogicalTreeHelper.GetChildren(d))
                    foreach (var found in Find<T>(child))
                        yield return found;
        }

        [Fact]
        public void A_percent_keeps_its_decimals_and_a_bad_value_shows_the_default() => Sta(() =>
        {
            var parts = new WindowParts();
            var fields = new Fields(parts, JObject.Parse(@"{ ""p"": 0.12345, ""q"": ""abc"" }"), JObject.Parse(@"{ ""p"": 0.1, ""q"": 0.2 }"), _ => { });
            fields.PercentField("P", "p", 0.01, "");
            fields.PercentField("Q", "q", 0.01, "");
            var p = parts.Boxes["p"];
            Assert.Equal("12.345", p.Text);
            Leave(p);
            Assert.Equal("12.345", p.Text);              // leaving the box does not round it
            p.Text = "150";
            Leave(p);
            Assert.Equal("100", p.Text);                 // outside the range: its limit
            Assert.Equal("20", parts.Boxes["q"].Text);   // no number in the file: the default, not an empty box
        });

        [Fact]
        public void A_kept_slider_value_goes_when_the_slider_is_put_on_its_own_stop() => Sta(() =>
        {
            var parts = new WindowParts();
            var options = new List<SettingOption> { new SettingOption { Value = "off", Label = "Off" }, new SettingOption { Value = "low", Label = "Low" }, new SettingOption { Value = "high", Label = "High" } };
            var fields = new Fields(parts, JObject.Parse(@"{ ""lvl"": ""medium"" }"), JObject.Parse(@"{ ""lvl"": ""off"" }"), _ => { });
            fields.SliderField("Level", "lvl", "", options, 0, 2, 1, true, null);
            var slider = parts.Sliders["lvl"];
            Assert.True(slider.IsKept);
            int changed = 0;
            slider.ValueChanged += (s, e) => changed++;
            Assert.True(slider.Put(new JValue("off")));  // the stop it sits on already
            Assert.False(slider.IsKept);
            Assert.Equal("off", slider.StoredText);
            Assert.Equal(1, changed);                    // its label and the change count hear of it
        });

        [Fact]
        public void A_decimal_box_takes_a_minus_only_where_negatives_are_allowed()
        {
            Assert.True(Ui.DecimalText("1,5", 0, false));
            Assert.True(Ui.DecimalText("-2.5", 0, true));
            Assert.False(Ui.DecimalText("-2.5", 0, false));
            Assert.False(Ui.DecimalText("-", 3, true));   // a minus only first
            Assert.False(Ui.DecimalText("abc", 0, true));
            Assert.False(Ui.DecimalText("", 0, true));
        }

        [Fact]
        public void A_list_cell_refuses_an_ambiguous_number_and_shows_points_on_a_German_system() => Sta(() =>
        {
            var columns = new List<SettingColumn> { new SettingColumn { Key = "v", Kind = "double", Label = "Value" } };
            var list = new ListEditor("k", "K", columns, JArray.Parse(@"[ { ""v"": 2.5 } ]"), new JArray(), "");
            var cell = Find<TextBox>(list).Single(b => b.Text == "2.5");   // "2.5", not "2,5"
            cell.Text = "1,000";
            Leave(cell);
            Assert.Equal("2.5", cell.Text);              // a thousand or one? refused, back to the number it held
            cell.Text = "3,5";
            Leave(cell);
            Assert.Equal(3.5, (double)list.Value[0]["v"], 6);
        }, "de-DE");

        [Fact]
        public void A_value_an_action_sets_finds_its_option_on_a_German_system() => Sta(() =>
        {
            var parts = new WindowParts();
            var combo = new ComboBox();
            combo.Items.Add(new ComboBoxItem { Content = "half", Tag = "0.5" });
            combo.Items.Add(new ComboBoxItem { Content = "one and a half", Tag = "1.5" });
            parts.Combos["mode"] = combo;
            var numbers = new ComboBox();
            numbers.Items.Add(new ComboBoxItem { Content = "a quarter", Tag = 0.25 });
            numbers.Items.Add(new ComboBoxItem { Content = "a third", Tag = 0.33 });
            parts.Combos["share"] = numbers;
            LiveValues.Apply(parts, JObject.Parse(@"{ ""mode"": 1.5, ""share"": ""0.33"" }"));
            Assert.Equal("1.5", ((ComboBoxItem)combo.SelectedItem).Tag);
            Assert.Equal(0.33, ((ComboBoxItem)numbers.SelectedItem).Tag);
        }, "de-DE");

        [Fact]
        public void Dropping_above_or_below_a_row_member_keeps_the_row_together()
        {
            string folder = Folder();
            var spec = new WindowSpec { Layout = new LayoutFile(Path.Combine(folder, "settings.json")) };
            spec.ResolvedSettings.AddRange(new[]
            {
                new Setting { Key = "a", Page = "game", Order = 0 },
                new Setting { Key = "b", Page = "game", Order = 10, Row = "bets" },
                new Setting { Key = "c", Page = "game", Order = 20, Row = "bets" },
                new Setting { Key = "d", Page = "game", Order = 30 },
                new Setting { Key = "x", Page = "game", Order = 40 }
            });
            var ctx = new PageContext();
            typeof(PageContext).GetProperty("Spec").SetValue(ctx, spec);
            var place = typeof(Customize).GetMethod("PlaceField", BindingFlags.NonPublic | BindingFlags.Static);

            List<string> Order() => spec.ResolvedSettings.OrderBy(s => spec.Layout.Field(s.Key)?.Order ?? s.Order).Select(s => s.Key).ToList();
            place.Invoke(null, new object[] { ctx, "game", "x", "c", true, false });    // above c, the second of the row
            Assert.Equal(new[] { "a", "x", "b", "c", "d" }, Order());
            place.Invoke(null, new object[] { ctx, "game", "x", "b", false, false });   // below b, the first of the row
            Assert.Equal(new[] { "a", "b", "c", "x", "d" }, Order());
        }

        [Fact]
        public void A_layout_file_another_program_holds_is_not_written_over()
        {
            string folder = Folder();
            string settingsPath = Path.Combine(folder, "settings.json");
            string layoutPath = Path.Combine(folder, LayoutFile.FileName);
            File.WriteAllText(layoutPath, @"{ ""theme"": { ""dark"": false } }");
            LayoutFile layout;
            using (new FileStream(layoutPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                layout = LayoutFile.Load(settingsPath);
            Assert.True(layout.Unreadable);
            Assert.NotNull(layout.Problem);
            Assert.False(File.Exists(layoutPath + ".broken"));   // not broken, only held
            layout.Dark = true;
            Assert.Throws<IOException>(() => layout.Save());
            Assert.Contains("false", File.ReadAllText(layoutPath));
            Assert.False(LayoutFile.Load(settingsPath).Dark.Value);   // read as it is once it is free again
        }

        [Fact]
        public void The_window_texts_that_were_English_only_are_in_the_language_files()
        {
            var german = Conventions.Load("de").Lang;
            foreach (var key in new[] { "cust.hidden", "cust.emptyPage", "field.colour.tip", "field.path.tip", "field.path.open", "action.done", "action.failed", "action.failedWhy", "action.clipboardBusy", "table.empty", "table.reload" })
            {
                Assert.True(Conventions.Default.Lang.Ui.ContainsKey(key), key);
                Assert.True(german.Ui.ContainsKey(key), key);
            }
        }

        [Fact]
        public void Help_lines_are_kept_in_the_layout_file_and_off_by_default()
        {
            string settingsPath = Path.Combine(Folder(), "settings.json");
            var layout = LayoutFile.Load(settingsPath);
            Assert.False(layout.HelpLines);
            layout.HelpLines = true;
            layout.Save();
            Assert.True(LayoutFile.Load(settingsPath).HelpLines);
            layout.HelpLines = false;
            Assert.Null(layout.Root["helpLines"]);   // off is the default, so the key goes
        }

        [Fact]
        public void With_help_lines_the_help_is_a_line_under_the_field_and_no_tooltip() => Sta(() =>
        {
            foreach (bool lines in new[] { false, true })
            {
                var spec = new WindowSpec { Layout = new LayoutFile(Path.Combine(Folder(), "settings.json")) };
                spec.Layout.HelpLines = lines;
                var parts = new WindowParts();
                var ctx = new PageContext();
                typeof(PageContext).GetProperty("Spec").SetValue(ctx, spec);
                typeof(PageContext).GetProperty("Parts").SetValue(ctx, parts);
                typeof(PageContext).GetProperty("Fields").SetValue(ctx, new Fields(parts, new JObject { ["rounds"] = 3, ["on"] = true }, new JObject(), url => { }));
                foreach (var setting in new[] { new Setting { Key = "rounds", Kind = "int", Label = "Rounds", Help = "How many rounds." },
                                                new Setting { Key = "on", Kind = "bool", Label = "On", Help = "Turns it on." } })
                {
                    var element = SchemaPage.Render(ctx, setting);
                    var all = new List<object>();
                    void Walk(object node) { all.Add(node); if (node is DependencyObject d) foreach (var child in LogicalTreeHelper.GetChildren(d)) Walk(child); }
                    Walk(element);
                    bool line = all.OfType<TextBlock>().Any(b => b.Tag as string == SchemaPage.HelpLineTag && string.Concat(b.Inlines.OfType<System.Windows.Documents.Run>().Select(r => r.Text)) == setting.Help);
                    bool tooltip = all.OfType<FrameworkElement>().Any(e => e.ToolTip as string == setting.Help);
                    Assert.True(lines == line, setting.Key + ": help line " + line + " with lines " + lines);
                    Assert.True(lines != tooltip, setting.Key + ": tooltip " + tooltip + " with lines " + lines);
                }
            }
        });

        [Fact]
        public void The_About_page_carries_the_framework_logo_in_its_corner() => Sta(() =>
        {
            Assert.NotNull(StandardPages.FrameworkLogo(true));
            Assert.NotNull(StandardPages.FrameworkLogo(false));
            var spec = new WindowSpec { Title = "Demo", Layout = new LayoutFile(Path.Combine(Folder(), "settings.json")) };
            var parts = new WindowParts();
            var ctx = new PageContext();
            typeof(PageContext).GetProperty("Spec").SetValue(ctx, spec);
            typeof(PageContext).GetProperty("Parts").SetValue(ctx, parts);
            typeof(PageContext).GetProperty("Fields").SetValue(ctx, new Fields(parts, new JObject(), new JObject(), url => { }));
            var page = StandardPages.BuildAbout(ctx);
            Assert.IsType<ScrollViewer>(page);   // the shell keeps the scroll position of a page that is one
            var all = new List<object>();
            void Walk(object node) { all.Add(node); if (node is DependencyObject d) foreach (var child in LogicalTreeHelper.GetChildren(d)) Walk(child); }
            Walk(page);
            var logo = all.OfType<Image>().Single(i => System.Windows.Automation.AutomationProperties.GetName(i) == StandardPages.WatermarkName);
            Assert.Equal(HorizontalAlignment.Right, logo.HorizontalAlignment);
            Assert.Contains("UIFramework", logo.ToolTip as string);
            Assert.True(logo.Opacity < 0.5);
        });
    }
}
