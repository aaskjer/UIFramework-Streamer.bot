using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Newtonsoft.Json.Linq;
using Xunit;

namespace UIFramework.Tests
{
    /// <summary>2.0.0: the customise mode moves what the page shows, one place at a time.</summary>
    public class Fixes1100CustomizeTests
    {
        private static void Sta(Action body)
        {
            Exception failure = null;
            var thread = new Thread(() => { try { body(); } catch (Exception ex) { failure = ex; } });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }

        private static string Folder()
        {
            string folder = Path.Combine(Path.GetTempPath(), "UIFramework.Tests", "customize-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            return folder;
        }

        private sealed class QuietLog : IHostLog
        {
            public void Info(string message) { }
            public void Warn(string message) { }
            public void Error(string message) { }
        }

        /// <summary>A window in the customise mode, its settings file in a folder of its own.</summary>
        private static WindowSpec Customising(Schema schema)
        {
            var spec = Entry.SpecFor(schema, Path.Combine(Folder(), "settings.json"));
            spec.Customizing = true;
            Entry.Prepare(spec, schema);
            return spec;
        }

        /// <summary>An entry of a field's menu in the customise mode, by its text.</summary>
        private static MenuItem MenuEntry(WindowSpec spec, string key, string pageId, string text)
        {
            var ctx = Entry.ContextFor(spec, new JObject(), new WindowParts(), new QuietLog(), null);
            var box = (Border)Customize.Wrap(ctx, spec.ResolvedSettings.First(s => s.Key == key), new TextBlock(), pageId);
            return box.ContextMenu.Items.OfType<MenuItem>().First(i => (string)i.Header == text);
        }

        private static void Click(MenuItem item) => item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

        // ---- What another page draws is not a field of this one ---------------------------------------------

        [Fact]
        public void Move_up_and_down_pass_over_what_the_Messages_page_draws_elsewhere() => Sta(() =>
        {
            // Blackjack's Messages page: its sender switches are declared among the templates, and the Messages page
            // draws them under the picker, not in the customise mode's list.
            var messages = Schema.Parse(@"{ ""host"": ""demo"", ""settings"": [
                { ""key"": ""m1"", ""kind"": ""template"", ""page"": ""messages"", ""default"": ""one"", ""group"": ""Game"", ""label"": ""One"" },
                { ""key"": ""m2"", ""kind"": ""template"", ""page"": ""messages"", ""default"": ""two"", ""group"": ""Game"", ""label"": ""Two"" },
                { ""key"": ""useBot"", ""default"": true, ""page"": ""messages"" },
                { ""key"": ""sendAction"", ""default"": false, ""page"": ""messages"" },
                { ""key"": ""m3"", ""kind"": ""template"", ""page"": ""messages"", ""default"": ""three"", ""group"": ""Game"", ""label"": ""Three"" } ],
                ""window"": { ""title"": ""Demo"" } }");
            var spec = Customising(messages);
            Click(MenuEntry(spec, "m2", "messages", Text.T("cust.moveDown")));
            Entry.Prepare(spec, messages);
            // One press, one place: before, it went past useBot and needed two more to pass m3.
            Assert.Equal(new[] { "m1", "m3", "m2" }, spec.Messages.Select(m => m.Key).ToArray());

            // High-Low's Cooldowns page: the cooldown senders are on it by the conventions, drawn on the Messages page.
            var cooldowns = Schema.Parse(@"{ ""host"": ""demo"", ""settings"": [
                { ""key"": ""cooldownUseBot"", ""default"": true },
                { ""key"": ""a"", ""default"": 1, ""page"": ""cooldowns"" },
                { ""key"": ""b"", ""default"": 2, ""page"": ""cooldowns"" },
                { ""key"": ""cooldownSendAction"", ""default"": false, ""section"": ""Chat"" },
                { ""key"": ""msgWait"", ""kind"": ""template"", ""page"": ""messages"", ""default"": ""wait"", ""group"": ""Cooldown"" } ],
                ""window"": { ""title"": ""Demo"" } }");
            spec = Customising(cooldowns);
            Assert.Equal("cooldowns", spec.ResolvedSettings.First(s => s.Key == "cooldownUseBot").Page);
            Assert.False(MenuEntry(spec, "a", "cooldowns", Text.T("cust.moveUp")).IsEnabled);     // the first the page shows
            var down = MenuEntry(spec, "b", "cooldowns", Text.T("cust.moveDown"));
            Assert.False(down.IsEnabled);                                                        // and the last
            Click(down);
            // Nothing to move past: nothing written, and no section taken from a switch the page does not show.
            Assert.Null(LayoutFile.Load(spec.SettingsPath).Field("b"));
        });

        // ---- A heading is a place of its own ------------------------------------------------------------

        /// <summary>A page's fields as it draws them, each with the section it sits in: "b1:A".</summary>
        private static string[] Drawn(WindowSpec spec, string pageId) =>
            spec.ResolvedSettings.Where(s => s.Page == pageId).OrderBy(s => s.Order).Select(s => s.Key + ":" + s.Section).ToArray();

        [Fact]
        public void One_press_takes_a_field_across_a_heading_and_no_further() => Sta(() =>
        {
            var schema = Schema.Parse(@"{ ""host"": ""demo"", ""settings"": [
                { ""key"": ""a1"", ""default"": ""x"", ""page"": ""game"", ""section"": ""A"" },
                { ""key"": ""a2"", ""default"": ""x"", ""page"": ""game"", ""section"": ""A"" },
                { ""key"": ""b1"", ""default"": ""x"", ""page"": ""game"", ""section"": ""B"" },
                { ""key"": ""b2"", ""default"": ""x"", ""page"": ""game"", ""section"": ""B"" } ],
                ""window"": { ""title"": ""Demo"", ""pages"": [ ""game"" ] } }");
            var spec = Customising(schema);
            void Press(string key, string text)
            {
                Click(MenuEntry(spec, key, "game", Text.T(text)));
                Entry.Prepare(spec, schema);
            }

            // The first field under B goes up: the last of A, right where it was. Before, it went between a1 and a2,
            // and the last place of A could not be reached from the keyboard.
            Press("b1", "cust.moveUp");
            Assert.Equal(new[] { "a1:A", "a2:A", "b1:A", "b2:B" }, Drawn(spec, "game"));
            Press("b1", "cust.moveUp");
            Assert.Equal(new[] { "a1:A", "b1:A", "a2:A", "b2:B" }, Drawn(spec, "game"));
            Press("b1", "cust.moveDown");
            Assert.Equal(new[] { "a1:A", "a2:A", "b1:A", "b2:B" }, Drawn(spec, "game"));
            // The last field of A goes down: the first under B, which a press could not reach either.
            Press("b1", "cust.moveDown");
            Assert.Equal(new[] { "a1:A", "a2:A", "b1:B", "b2:B" }, Drawn(spec, "game"));
            Press("a2", "cust.moveDown");
            Assert.Equal(new[] { "a1:A", "a2:B", "b1:B", "b2:B" }, Drawn(spec, "game"));
            Press("a2", "cust.moveDown");
            Assert.Equal(new[] { "a1:A", "b1:B", "a2:B", "b2:B" }, Drawn(spec, "game"));
        });

        // ---- A field moved to another page ------------------------------------------------------------------

        /// <summary>Whether a section's heading shows more than once on the page.</summary>
        private static bool SplitsASection(WindowSpec spec, string pageId)
        {
            var headings = new List<string>();
            string section = null;
            foreach (var s in spec.ResolvedSettings.Where(x => x.Page == pageId).OrderBy(x => x.Order))
                if (s.Section != null && s.Section != section)
                    headings.Add(section = s.Section);
            return headings.Count != headings.Distinct().Count();
        }

        [Fact]
        public void A_field_moved_to_a_page_the_user_sorted_lands_at_its_end_or_in_its_own_section() => Sta(() =>
        {
            var schema = Schema.Parse(@"{ ""host"": ""demo"", ""settings"": [
                { ""key"": ""p1"", ""default"": ""x"", ""page"": ""one"", ""section"": ""S1"" },
                { ""key"": ""p2"", ""default"": ""x"", ""page"": ""one"", ""section"": ""S1"" },
                { ""key"": ""p3"", ""default"": ""x"", ""page"": ""one"", ""section"": ""S2"" },
                { ""key"": ""q1"", ""default"": ""x"", ""page"": ""two"" },
                { ""key"": ""q2"", ""default"": ""x"", ""page"": ""two"" },
                { ""key"": ""g"", ""default"": ""x"", ""page"": ""two"", ""section"": ""G"" },
                { ""key"": ""h"", ""default"": ""x"", ""page"": ""two"", ""section"": ""S1"" },
                { ""key"": ""r"", ""default"": ""x"", ""page"": ""two"", ""row"": ""pair"" },
                { ""key"": ""r2"", ""default"": ""x"", ""page"": ""two"", ""row"": ""pair"" } ],
                ""window"": { ""title"": ""Demo"", ""pages"": [ ""one"", ""two"" ] } }");
            var spec = Customising(schema);
            // Page one as sorting it leaves it: an order for every field.
            spec.Layout.SetField("p1", "order", 0);
            spec.Layout.SetField("p2", "order", 10);
            spec.Layout.SetField("p3", "order", 20);
            spec.Layout.Save();
            Entry.Prepare(spec, schema);
            var ctx = Entry.ContextFor(spec, new JObject(), new WindowParts(), new QuietLog(), null);
            string one = spec.Pages.First(p => p.Id == "one").Title;

            // Its menu: g was declared sixth, and the sixth place fell between p1 (0) and p2 (10): S1 showed twice.
            void MoveByMenu(string key)
            {
                var box = (Border)Customize.Wrap(ctx, spec.ResolvedSettings.First(s => s.Key == key), new TextBlock(), "two");
                var moveTo = box.ContextMenu.Items.OfType<MenuItem>().First(i => (string)i.Header == Text.T("cust.moveTo"));
                Click(moveTo.Items.OfType<MenuItem>().First(i => (string)i.Header == one));
                Entry.Prepare(spec, schema);
            }
            MoveByMenu("g");
            Assert.Equal(new[] { "p1:S1", "p2:S1", "p3:S2", "g:G" }, Drawn(spec, "one"));
            Assert.False(SplitsASection(spec, "one"));

            // Dropped on the page in the rail: h is in S1, which page one has, and goes to its end.
            var rail = new Border();
            Customize.AttachRail(ctx, spec.Pages.First(p => p.Id == "one"), rail, () => spec.Pages.Where(p => !p.Bottom).ToList());
            var data = new DataObject("UIFramework.Field", "h");
            data.SetData("UIFramework.Layout", spec.Layout.Path);
            var drop = (DragEventArgs)typeof(DragEventArgs).GetConstructors(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .First(c => c.GetParameters().Length == 5)
                .Invoke(new object[] { data, DragDropKeyStates.None, DragDropEffects.Move, rail, new Point() });
            drop.RoutedEvent = DragDrop.DropEvent;
            rail.RaiseEvent(drop);
            Entry.Prepare(spec, schema);
            Assert.Equal(new[] { "p1:S1", "p2:S1", "h:S1", "p3:S2", "g:G" }, Drawn(spec, "one"));
            Assert.False(SplitsASection(spec, "one"));

            // A row is a line of the page it was on, by the menu as by a drop.
            MoveByMenu("r");
            Assert.Equal("", spec.Layout.Field("r").Row);
            Assert.Equal("r:", Drawn(spec, "one").Last());
        });

        // ---- The layout file edited by hand ---------------------------------------------------------------

        [Fact]
        public void An_order_with_more_digits_than_a_long_reads_as_the_largest_and_the_window_opens()
        {
            string folder = Folder(), settingsPath = Path.Combine(folder, "settings.json");
            File.WriteAllText(Path.Combine(folder, LayoutFile.FileName), @"{
                ""pages"": { ""two"": { ""order"": 123456789012345678901234 } },
                ""fields"": { ""a"": { ""order"": 99999999999999999999 }, ""b"": { ""order"": -99999999999999999999 } } }");
            var layout = LayoutFile.Load(settingsPath);
            Assert.Null(layout.Problem);
            Assert.Equal(int.MaxValue, layout.Field("a").Order);
            Assert.Equal(int.MinValue, layout.Field("b").Order);
            Assert.Equal(int.MaxValue, layout.Page("two").Order);

            // The window resolves with it: before, (long) threw on the number and every open failed.
            var schema = Schema.Parse(@"{ ""host"": ""demo"", ""settings"": [
                { ""key"": ""a"", ""default"": 1, ""page"": ""one"" }, { ""key"": ""b"", ""default"": 2, ""page"": ""one"" },
                { ""key"": ""c"", ""default"": 3, ""page"": ""two"" } ],
                ""window"": { ""title"": ""Demo"", ""pages"": [ ""one"", ""two"" ] } }");
            var spec = Entry.SpecFor(schema, settingsPath);
            Entry.Prepare(spec, schema);
            Assert.Equal(new[] { "b:", "a:" }, Drawn(spec, "one"));
            Assert.Equal(new[] { "two", "one", "about" }, spec.Pages.Select(p => p.Id).ToArray());
        }
    }
}
