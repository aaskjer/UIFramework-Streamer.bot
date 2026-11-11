using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Newtonsoft.Json.Linq;
using Xunit;

namespace UIFramework.Tests
{
    /// <summary>What the mouse and the keyboard do in the customise mode, the search and the row editor: showing a
    /// field the game hides, dropping a page or a field where it was aimed, a search that keeps its selection and its
    /// keyboard, finds what the standard pages draw, and a row that stays while it is typed in.</summary>
    public class InteractionFixTests
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
            string folder = Path.Combine(Path.GetTempPath(), "UIFramework.Tests", "interaction-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            return folder;
        }

        private static List<object> Walk(object root)
        {
            var all = new List<object>();
            void Visit(object node) { all.Add(node); if (node is DependencyObject d) foreach (var child in LogicalTreeHelper.GetChildren(d)) Visit(child); }
            Visit(root);
            return all;
        }

        /// <summary>Lets everything the window queued (a focus set "once the bar is up") run.</summary>
        private static void Settle() => Dispatcher.CurrentDispatcher.Invoke(new Action(() => { }), DispatcherPriority.ApplicationIdle);

        private static Window Shown(UIElement content)
        {
            var window = new Window { Width = 700, Height = 500, Content = content, ShowInTaskbar = false, WindowStyle = WindowStyle.None, Left = -20000, Top = -20000, WindowStartupLocation = WindowStartupLocation.Manual };
            window.Show();
            window.UpdateLayout();
            return window;
        }

        /// <summary>Puts the keyboard into <paramref name="element"/>. False where Windows gives this process no
        /// keyboard (a build agent without a desktop in front): what depends on it cannot be checked there.</summary>
        private static bool Focus(Window window, UIElement element)
        {
            window.Activate();
            element.Focus();
            Settle();
            return ReferenceEquals(Keyboard.FocusedElement, element);
        }

        private sealed class QuietLog : IHostLog
        {
            public void Info(string message) { }
            public void Warn(string message) { }
            public void Error(string message) { }
        }

        /// <summary>A game with a log, empty so far: the Log page is built only for a game that has one.</summary>
        private sealed class EmptyLog : NoHooks
        {
            public override IEnumerable<LogEntry> ReadLog() => new List<LogEntry>();
        }

        // ---- 1. Show on a field the game hides -----------------------------------------------------------

        [Fact]
        public void Show_and_Hide_write_only_what_the_game_does_not_say_already()
        {
            Assert.True((bool)Customize.HiddenOverride(true, false));            // hide a field the game shows
            Assert.Null(Customize.HiddenOverride(false, false));                 // show it again: the layout's word goes
            Assert.False((bool)Customize.HiddenOverride(false, true));           // show a field the game hides
            Assert.Null(Customize.HiddenOverride(true, true));                   // hide it again: the game's word is enough
        }

        [Fact]
        public void Show_in_the_field_menu_shows_a_field_the_game_hides() => Sta(() =>
        {
            var schema = Schema.Parse(@"{ ""host"": ""demo"", ""settings"": [
                { ""key"": ""plain"", ""default"": 1, ""page"": ""game"" },
                { ""key"": ""secret"", ""default"": 1, ""page"": ""game"", ""hidden"": true },
                { ""key"": ""veiled"", ""default"": 1, ""page"": ""game"" } ],
                ""window"": { ""title"": ""Demo"", ""pages"": [ ""game"" ], ""overrides"": { ""veiled"": { ""hidden"": true } } } }");
            var spec = Entry.SpecFor(null, schema, Path.Combine(Folder(), "settings.json"));
            spec.Customizing = true;
            Entry.Prepare(spec, schema);
            bool Hidden(string key) => spec.ResolvedSettings.First(s => s.Key == key).Hidden;

            // The second entry of a field's menu, Show or Hide, and the window as it is built after it.
            void ShowOrHide(string key, string expected)
            {
                var ctx = Entry.ContextFor(spec, new JObject(), new WindowParts(), new QuietLog(), null);
                var box = (Border)Customize.Wrap(ctx, spec.ResolvedSettings.First(s => s.Key == key), new TextBlock(), "game");
                var item = box.ContextMenu.Items.OfType<MenuItem>().ElementAt(1);
                Assert.Equal(Text.T(expected), item.Header);
                item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                Entry.Prepare(spec, schema);
            }

            foreach (string key in new[] { "secret", "veiled" })   // hidden by the schema, hidden by the spec's override
            {
                Assert.True(Hidden(key));
                ShowOrHide(key, "cust.show");
                Assert.False(Hidden(key));
                Assert.False(spec.Layout.Field(key).Hidden.Value);
                ShowOrHide(key, "cust.hide");
                Assert.True(Hidden(key));
                Assert.Null(spec.Layout.Field(key));                // as the game has it: nothing of the user's left
            }

            // A field the game shows: as before, hiding writes the override and showing takes it away.
            ShowOrHide("plain", "cust.hide");
            Assert.True(Hidden("plain"));
            Assert.True(spec.Layout.Field("plain").Hidden.Value);
            ShowOrHide("plain", "cust.show");
            Assert.False(Hidden("plain"));
            Assert.Null(spec.Layout.Field("plain"));
        });

        // ---- 2. A page dragged in the rail ---------------------------------------------------------------

        [Fact]
        public void A_dragged_page_takes_the_place_of_the_page_it_is_dropped_on()
        {
            var pages = new List<string> { "a", "b", "c" };
            Assert.Equal(new[] { "b", "a", "c" }, Customize.MovePage(pages, "a", "b"));   // down one: after the target
            Assert.Equal(new[] { "b", "c", "a" }, Customize.MovePage(pages, "a", "c"));   // into last place
            Assert.Equal(new[] { "c", "a", "b" }, Customize.MovePage(pages, "c", "a"));   // up: before the target
            Assert.Equal(new[] { "a", "c", "b" }, Customize.MovePage(pages, "c", "b"));
            Assert.Equal(new[] { "a", "c", "b" }, Customize.MovePage(pages, "b", "gone"));   // no such target: to the end
            Assert.Equal(new[] { "a", "b", "c" }, pages);                                 // the list given is left alone
        }

        // ---- 3. A field dropped above or below a line the page formed itself -----------------------------

        [Fact]
        public void The_lines_of_a_page_are_counted_as_the_page_draws_them()
        {
            var fields = new List<Setting>
            {
                new Setting { Key = "name", Kind = "string" },
                new Setting { Key = "n1", Kind = "int" }, new Setting { Key = "n2", Kind = "duration" }, new Setting { Key = "n3", Kind = "percent" },
                new Setting { Key = "n4", Kind = "long" },                                    // a fourth number starts a new line
                new Setting { Key = "b1", Kind = "bool" }, new Setting { Key = "b2", Kind = "bool" },
                new Setting { Key = "b3", Kind = "bool" },                                    // and a third switch
                new Setting { Key = "x1", Kind = "int", Section = "X" }, new Setting { Key = "x2", Kind = "int", Section = "X" },
                new Setting { Key = "y1", Kind = "int", Section = "Y" },                      // a heading ends the line
                new Setting { Key = "r1", Kind = "int", Section = "Y", Row = "pair" }, new Setting { Key = "r2", Kind = "string", Section = "Y", Row = "pair" },
                new Setting { Key = "own", Kind = "int", Section = "Y", Row = "~own" },       // a line of its own
                new Setting { Key = "y2", Kind = "int", Section = "Y" }
            };
            Assert.Equal(new[] { 0, 1, 1, 1, 2, 3, 3, 4, 5, 5, 6, 7, 7, 8, 9 }, Customize.Lines(fields));
            Assert.Empty(Customize.Lines(new List<Setting>()));
        }

        [Fact]
        public void Dropping_above_or_below_a_field_of_an_automatic_row_lands_above_or_below_the_whole_line()
        {
            var place = typeof(Customize).GetMethod("PlaceField", BindingFlags.NonPublic | BindingFlags.Static);
            // name / s t v / d / on off: the three numbers and the two switches are lines the page forms by itself.
            List<string> After(string dragged, string target, bool before)
            {
                var spec = new WindowSpec { Layout = new LayoutFile(Path.Combine(Folder(), "settings.json")) };
                spec.ResolvedSettings.AddRange(new[]
                {
                    new Setting { Key = "name", Kind = "string", Page = "game", Order = 0 },
                    new Setting { Key = "s", Kind = "int", Page = "game", Order = 10 },
                    new Setting { Key = "t", Kind = "int", Page = "game", Order = 20 },
                    new Setting { Key = "v", Kind = "int", Page = "game", Order = 30 },
                    new Setting { Key = "d", Kind = "int", Page = "game", Order = 40 },
                    new Setting { Key = "on", Kind = "bool", Page = "game", Order = 50 },
                    new Setting { Key = "off", Kind = "bool", Page = "game", Order = 60 }
                });
                var ctx = new PageContext();
                typeof(PageContext).GetProperty("Spec").SetValue(ctx, spec);
                place.Invoke(null, new object[] { ctx, "game", dragged, target, before, false });
                Assert.Equal("", spec.Layout.Field(dragged).Row);   // on a line of its own, not a fourth of the row
                return spec.ResolvedSettings.OrderBy(s => spec.Layout.Field(s.Key)?.Order ?? s.Order).Select(s => s.Key).ToList();
            }

            Assert.Equal(new[] { "name", "d", "s", "t", "v", "on", "off" }, After("d", "t", true));     // above the middle one
            Assert.Equal(new[] { "name", "s", "t", "v", "d", "on", "off" }, After("d", "s", false));    // below the first
            Assert.Equal(new[] { "s", "t", "v", "d", "on", "off", "name" }, After("name", "on", false)); // below a pair of switches
            Assert.Equal(new[] { "name", "t", "s", "v", "d", "on", "off" }, After("t", "v", true));     // one of the row itself, above it
            Assert.Equal(new[] { "s", "t", "v", "d", "name", "on", "off" }, After("name", "d", false)); // a line of one stays as it was
        }

        // ---- 4. The search and a pointer at rest ---------------------------------------------------------

        private static List<SearchEntry> Entries(List<string> went) => new List<SearchEntry>
        {
            new SearchEntry { Key = "answerTimeSeconds", Title = "Answer time", Where = "Game › Rounds", Go = () => went.Add("answerTimeSeconds") },
            new SearchEntry { Key = "blitzAnswerTimeSeconds", Title = "Answer time", Where = "Game › Blitz rounds", Go = () => went.Add("blitzAnswerTimeSeconds") },
            new SearchEntry { Key = "userCooldown", Title = "Per viewer", Where = "Access › Cooldowns", Go = () => went.Add("userCooldown") }
        };

        [Fact]
        public void A_row_drawn_under_a_pointer_at_rest_does_not_take_the_selection() => Sta(() =>
        {
            var went = new List<string>();
            var layers = new Grid { Width = 700, Height = 500 };
            var window = new Window { Content = layers, Width = 700, Height = 500, ShowInTaskbar = false, WindowStyle = WindowStyle.None, Left = -20000, Top = -20000 };
            var bar = new SearchBar(window, layers, null, () => Entries(went));
            bar.Open();
            bar.Box.Text = "answer";
            var rows = Walk(layers).OfType<Border>().Where(b => b.Tag as string == SearchBar.RowTag).ToList();
            Assert.Equal(2, rows.Count);
            Assert.Equal(0, bar.Selected);

            // What WPF raises when the second row is laid out under the pointer: the pointer is where it was.
            rows[1].RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = UIElement.MouseEnterEvent });
            Assert.Equal(0, bar.Selected);

            // A pointer that moved selects; the same place again (the list scrolled under it) does not.
            var here = Mouse.GetPosition(layers);
            var moved = new Point(here.X + 5, here.Y + 5);
            bar.Hover(1, moved);
            Assert.Equal(1, bar.Selected);
            bar.Move(-1);
            bar.Hover(1, moved);
            Assert.Equal(0, bar.Selected);

            // Typing draws the list again: the best match is selected, whatever the pointer rests on.
            bar.Hover(1, new Point(moved.X + 5, moved.Y));
            Assert.Equal(1, bar.Selected);
            bar.Box.Text = "answer t";
            Assert.Equal(0, bar.Selected);
            rows = Walk(layers).OfType<Border>().Where(b => b.Tag as string == SearchBar.RowTag).ToList();
            rows[1].RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = UIElement.MouseEnterEvent });
            bar.GoToSelected();
            Assert.Equal(new[] { "answerTimeSeconds" }, went);
            window.Close();
        });

        // ---- 5. What the standard pages draw themselves --------------------------------------------------

        [Fact]
        public void An_element_in_a_collapsed_panel_does_not_show_on_its_page() => Sta(() =>
        {
            var toggle = new CheckBox();
            var group = new StackPanel { Children = { toggle } };
            var page = new ScrollViewer { Content = new StackPanel { Children = { group } } };
            Assert.True(Shell.ShownOn(toggle, page));
            group.Visibility = Visibility.Collapsed;
            Assert.False(Shell.ShownOn(toggle, page));      // hidden with its panel, though its own Visibility says Visible
            group.Visibility = Visibility.Visible;
            page.Visibility = Visibility.Collapsed;
            Assert.True(Shell.ShownOn(toggle, page));       // the page is another page's turn: it shows once it is in front
            toggle.Visibility = Visibility.Hidden;
            Assert.False(Shell.ShownOn(toggle, page));
        });

        [Fact]
        public void The_Messages_and_Log_pages_name_the_settings_they_draw() => Sta(() =>
        {
            var schema = Schema.Parse(@"{ ""host"": ""demo"", ""settings"": [
                { ""key"": ""useBot"", ""default"": true },
                { ""key"": ""sendAction"", ""default"": false },
                { ""key"": ""cooldownUseBot"", ""default"": true },
                { ""key"": ""cooldownSendAction"", ""default"": false },
                { ""key"": ""numberSeparator"", ""default"": ""."" },
                { ""key"": ""logToFile"", ""default"": true, ""page"": ""log"" },
                { ""key"": ""msgWin"", ""default"": ""{user} wins"", ""kind"": ""template"", ""group"": ""Game"", ""label"": ""Win"" },
                { ""key"": ""msgWait"", ""default"": ""{user} waits"", ""kind"": ""template"", ""group"": ""Cooldown"", ""label"": ""Wait"" } ],
                ""window"": { ""title"": ""Demo"", ""pages"": [ ""messages"", ""log"", ""about"" ], ""log"": { ""folder"": ""Demo/Logs"", ""writeLogKey"": ""logToFile"" } } }");
            var spec = Entry.SpecFor(null, schema, Path.Combine(Folder(), "settings.json"));
            Entry.Prepare(spec, schema);
            var parts = new WindowParts();
            var ctx = Entry.ContextFor(spec, (JObject)spec.Defaults.DeepClone(), parts, new QuietLog(), new EmptyLog());

            var messages = StandardPages.BuildMessages(ctx);
            foreach (string key in new[] { "useBot", "sendAction", "cooldownUseBot", "cooldownSendAction", "numberSeparator" })
                Assert.True(parts.Elements.ContainsKey(key), key);
            Assert.Same(parts.Switches["useBot"], parts.Elements["useBot"]);
            Assert.Contains(parts.Combos["numberSeparator"], Walk(parts.Elements["numberSeparator"]));   // the label with it

            // A game message is picked: its senders show, the cooldown pair is collapsed with its panel.
            bool Shows(string key) => Shell.ShownOn(parts.Elements[key], messages);
            Assert.True(Shows("numberSeparator"));
            Assert.True(Shows("useBot") && Shows("sendAction"));
            Assert.False(Shows("cooldownUseBot") || Shows("cooldownSendAction"));
            Assert.Equal(Visibility.Visible, parts.Elements["cooldownUseBot"].Visibility);   // which its own Visibility does not tell
            var picker = Walk(messages).OfType<ComboBox>().Single(c => c.Name == "MessagePicker");
            picker.SelectedItem = picker.Items.OfType<ComboBoxItem>().Single(i => i.Tag as string == "msgWait");
            Assert.False(Shows("useBot") || Shows("sendAction"));
            Assert.True(Shows("cooldownUseBot") && Shows("cooldownSendAction"));

            var log = StandardPages.BuildLog(ctx);
            Assert.Same(parts.Switches["logToFile"], parts.Elements["logToFile"]);
            Assert.True(Shell.ShownOn(parts.Elements["logToFile"], log));
        });

        // ---- 6. The search bar and the keyboard ----------------------------------------------------------

        [Fact]
        public void Tab_does_not_leave_the_search_box_while_the_bar_is_up() => Sta(() =>
        {
            var layers = new Grid();
            layers.Children.Add(new StackPanel { Children = { new Button { Content = "Save" } } });
            var window = Shown(layers);
            var bar = new SearchBar(window, layers, null, () => Entries(new List<string>()));
            bar.Open();
            KeyEventArgs Press(Key key)
            {
                var press = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window), 0, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
                bar.Box.RaiseEvent(press);
                return press;
            }
            Assert.True(Press(Key.Tab).Handled);       // taken, so the keyboard stays in the box
            Assert.True(bar.IsOpen);
            Assert.False(Press(Key.A).Handled);        // typing is the box's own
            Assert.True(Press(Key.Escape).Handled);
            Assert.False(bar.IsOpen);
            window.Close();
        });

        [Fact]
        public void Closing_the_search_without_a_result_gives_the_keyboard_back() => Sta(() =>
        {
            var field = new TextBox();
            var other = new TextBox();
            var layers = new Grid();
            layers.Children.Add(new StackPanel { Children = { field, other } });
            var window = Shown(layers);
            try
            {
                if (!Focus(window, field)) return;     // no keyboard for this process: nothing to check
                var went = new List<string>();
                var bar = new SearchBar(window, layers, null, () => Entries(went));

                bar.Open();
                Settle();
                Assert.Same(bar.Box, Keyboard.FocusedElement);
                bar.Close();                           // Esc, or a click beside the bar
                Assert.Same(field, Keyboard.FocusedElement);

                // A result chosen: the keyboard is the result's (the found field takes it), not the field's from before.
                bar.Open();
                Settle();
                bar.Box.Text = "viewer";
                bar.GoToSelected();
                Settle();
                Assert.Equal(new[] { "userCooldown" }, went);
                Assert.NotSame(field, Keyboard.FocusedElement);

                // And with the keyboard elsewhere when it opened, that is where it goes back to.
                Assert.True(Focus(window, other));
                bar.Open();
                Settle();
                bar.Close();
                Assert.Same(other, Keyboard.FocusedElement);
            }
            finally { window.Close(); }
        });

        // ---- 7. The row editor's search and the row being typed in ---------------------------------------

        [Fact]
        public void A_row_shows_under_the_search_while_it_holds_the_text_or_is_typed_in()
        {
            Assert.True(ListEditor.Shows("app", new[] { "Apple", "3" }, false));
            Assert.False(ListEditor.Shows("app", new[] { "aple", "3" }, false));
            Assert.True(ListEditor.Shows("app", new[] { "aple", "3" }, true));     // being typed in: it stays
            Assert.True(ListEditor.Shows("", new[] { "aple" }, false));            // no search: every row
            Assert.False(ListEditor.Shows("app", new string[] { null }, false));
        }

        [Fact]
        public void The_row_being_typed_in_stays_under_an_active_search() => Sta(() =>
        {
            var columns = new List<SettingColumn> { new SettingColumn { Key = "Word", Kind = "string", Label = "Word" } };
            var rows = new JArray();
            foreach (string word in new[] { "apple", "apply", "banana", "cherry", "grape", "lemon", "melon", "peach", "plum" })
                rows.Add(new JObject { ["Word"] = word });
            var editor = new ListEditor("words", "Words", columns, rows, null, "");
            var window = Shown(editor);
            try
            {
                var search = Walk(editor).OfType<TextBox>().First(b => System.Windows.Automation.AutomationProperties.GetName(b).StartsWith("Search"));
                TextBox Cell(string word) => Walk(editor).OfType<TextBox>().First(b => b.Text == word && !ReferenceEquals(b, search));
                Visibility Row(TextBox cell) => ((FrameworkElement)cell.Parent).Visibility;
                var apple = Cell("apple");
                var apply = Cell("apply");
                search.Text = "app";
                Assert.Equal(Visibility.Visible, Row(apple));
                Assert.Equal(Visibility.Collapsed, Row(Cell("banana")));

                // A row nobody types in is filtered as before, whatever changes its text.
                apply.Text = "aply";
                Assert.Equal(Visibility.Collapsed, Row(apply));

                if (!Focus(window, apple)) return;     // no keyboard for this process: nothing more to check
                apple.Text = "aple";                   // a "p" deleted while typing
                Assert.Equal(Visibility.Visible, Row(apple));
                Assert.Same(apple, Keyboard.FocusedElement);
                apple.Text = "apple pie";
                Assert.Equal(Visibility.Visible, Row(apple));
                apple.Text = "aple";

                // The keyboard leaves the row: now the search has its say again.
                Assert.True(Focus(window, search));
                Assert.Equal(Visibility.Collapsed, Row(apple));
                Assert.Equal(9, editor.Value.Count);   // hidden or not, every row is still saved
            }
            finally { window.Close(); }
        });
    }
}
