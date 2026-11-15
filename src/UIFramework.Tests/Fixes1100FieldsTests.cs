using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using Newtonsoft.Json.Linq;
using Xunit;

namespace UIFramework.Tests
{
    /// <summary>2.0.0: what docs/Schema.md promised of two kinds and the code lacked. A path field's folder button
    /// opens a file's folder, and a browse button beside it chooses a file or a folder with Windows' dialog; a grouplist
    /// field suggests Streamer.bot's groups, which the settings action hands over.</summary>
    public class Fixes1100FieldsTests
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

        private const string Head = @"{ ""host"": ""demo"", ""version"": ""v1"", ""settings"": [ ";

        private static WindowSpec Prepared(string schemaJson)
        {
            try
            {
                var schema = Schema.Parse(schemaJson);
                var spec = Entry.SpecFor(null, schema, null);
                Entry.Prepare(spec, schema);
                return spec;
            }
            finally { Text.Use(Conventions.Default.Lang); }
        }

        // ---- path: the folder button --------------------------------------------------------------------------

        private const string Bot = @"C:\Streamer.bot";

        // A Streamer.bot folder with an overlay folder in it and a words file without an extension.
        private static bool FolderThere(string path) => path == Bot || path == Bot + @"\overlay";
        private static bool FileThere(string path) => path == Bot + @"\data\WORDS";

        [Fact]
        public void TheFolderButtonOpensTheFolderAFileIsIn()
        {
            // A file the project writes, not there yet: its folder, not a folder named after the file.
            Assert.Equal(Bot + @"\overlay", PathPicker.FolderToOpen(@"overlay\state.json", Bot, FolderThere, FileThere));
            Assert.Equal(Bot + @"\overlay", PathPicker.FolderToOpen("overlay/state.json", Bot, FolderThere, FileThere));
            Assert.Equal(@"D:\games\scores", PathPicker.FolderToOpen("\"D:\\games\\scores\\today.txt\" ", Bot, FolderThere, FileThere));
            // A file that is there, whatever its name.
            Assert.Equal(Bot + @"\data", PathPicker.FolderToOpen(@"data\WORDS", Bot, FolderThere, FileThere));
            // A folder stays the folder, also one with a dot in its name that is there.
            Assert.Equal(Bot + @"\logs", PathPicker.FolderToOpen("logs", Bot, FolderThere, FileThere));
            Assert.Equal(Bot, PathPicker.FolderToOpen(Bot, @"D:\", FolderThere, FileThere));
            Assert.Null(PathPicker.FolderToOpen("  ", Bot, FolderThere, FileThere));
        }

        // ---- path: the browse button --------------------------------------------------------------------------

        private static PathPicker.Plan Plan(string value, string fallback = null) => PathPicker.For(value, fallback, Bot, FolderThere, FileThere);

        [Fact]
        public void AFileGetsAFileDialogInTheFolderItIsIn()
        {
            var plan = Plan(@"overlay\state.json");
            Assert.True(plan.File);
            Assert.Equal(Bot + @"\overlay", plan.Folder);
            Assert.Equal("state.json", plan.FileName);
            // A folder that is not there yet: the nearest one above it that is.
            plan = Plan(@"overlay\new\deeper\state.json");
            Assert.True(plan.File);
            Assert.Equal(Bot + @"\overlay", plan.Folder);
            // A drive with nothing of it there: the Streamer.bot folder.
            plan = Plan(@"Q:\x\scores.txt");
            Assert.True(plan.File);
            Assert.Equal(Bot, plan.Folder);
            Assert.Equal("scores.txt", plan.FileName);
            // A file that is there, without an extension.
            Assert.True(Plan(@"data\WORDS").File);
        }

        [Fact]
        public void AFolderGetsAFolderDialogAndTheDefaultSaysWhatTheSettingHolds()
        {
            var plan = Plan("overlay", "logs");
            Assert.False(plan.File);
            Assert.Equal(Bot + @"\overlay", plan.Folder);
            Assert.Null(plan.FileName);
            Assert.False(Plan(Bot).File);                                   // a folder that is there, a dot in its name
            Assert.Equal(Bot + @"\overlay", Plan(@"overlay\logs").Folder);  // not there yet: the one above it
            // Empty: the dialog starts where the default points.
            plan = Plan("", @"overlay\state.json");
            Assert.True(plan.File);
            Assert.Equal("state.json", plan.FileName);
            Assert.Equal(Bot + @"\overlay", plan.Folder);
            Assert.False(Plan(" ", "overlay").File);
            // A folder in a setting whose default is a file: a file dialog in that folder, the default's name filled in.
            plan = Plan("overlay", "state.json");
            Assert.True(plan.File);
            Assert.Equal(Bot + @"\overlay", plan.Folder);
            Assert.Equal("state.json", plan.FileName);
            // Nothing at all: a folder dialog in the Streamer.bot folder.
            plan = Plan("", null);
            Assert.False(plan.File);
            Assert.Equal(Bot, plan.Folder);
        }

        [Fact]
        public void AChoiceUnderTheStreamerBotFolderIsWrittenRelative()
        {
            Assert.Equal(@"overlay\state.json", PathPicker.Relative(Bot + @"\overlay\state.json", Bot + @"\"));
            Assert.Equal("logs", PathPicker.Relative(@"c:\streamer.bot\logs", Bot));          // the case of the letters does not matter
            Assert.Equal(@"C:\Streamer.botX\logs", PathPicker.Relative(@"C:\Streamer.botX\logs", Bot));   // a folder whose name only starts the same
            Assert.Equal(@"D:\games\scores.txt", PathPicker.Relative(@"D:\games\scores.txt", Bot));
            Assert.Equal(Bot, PathPicker.Relative(Bot, Bot));                                  // the folder itself stays a whole path, not ""
        }

        [Fact]
        public void APathFieldHasABrowseButtonBesideTheFolderButton() => Sta(() =>
        {
            var spec = Prepared(Head + @"{ ""key"": ""stateFile"", ""default"": ""overlay/state.json"", ""kind"": ""path"" } ] }");
            try
            {
                var ctx = Entry.ContextFor(spec, (JObject)spec.Defaults.DeepClone(), new WindowParts(), null, null);
                var field = (StackPanel)SchemaPage.Render(ctx, spec.ResolvedSettings.First(s => s.Key == "stateFile"));
                var dock = Assert.IsType<DockPanel>(field.Children[1]);
                var buttons = dock.Children.OfType<Button>().ToList();
                Assert.Equal(new[] { Text.T("field.path.open"), Text.T("field.path.browse") }, buttons.Select(b => (string)b.ToolTip));
                Assert.Equal(PathPicker.BrowseGlyph, buttons[1].Content);
                Assert.IsType<TextBox>(dock.Children[dock.Children.Count - 1]);   // the box takes the rest
                foreach (string code in new[] { "en", "de", "es" })
                {
                    var ui = Conventions.Load(code).Lang.Ui;
                    Assert.All(new[] { "field.path.browse", "field.path.pickFile", "field.path.pickFolder", "field.path.allFiles" }, key => Assert.True(ui.ContainsKey(key), code + " lacks " + key));
                }
            }
            finally { Text.Use(Conventions.Default.Lang); }
        });

        // ---- grouplist: Streamer.bot's groups suggested -------------------------------------------------------

        [Fact]
        public void AGroupFieldOffersTheGroupsNotAddedYetThatHoldWhatIsTyped()
        {
            var known = new[] { "Regulars", "VIP", "Moderators", "Top Supporters", "Supporters", "Über Fans", "vip" };
            Assert.Equal(new[] { "VIP", "Moderators", "Top Supporters", "Supporters", "Über Fans" }, Fields.Suggested(known, "regulars", ""));
            Assert.Equal(new[] { "Supporters", "Top Supporters" }, Fields.Suggested(known, "", "sup"));   // what starts with it first
            Assert.Equal(new[] { "Über Fans" }, Fields.Suggested(known, "", " uber"));                     // accents and case aside
            Assert.Equal(new[] { "VIP" }, Fields.Suggested(known, "Regulars", "@vi"));
            Assert.Empty(Fields.Suggested(known, "VIP", "vip"));
            Assert.Empty(Fields.Suggested(null, "", ""));

            // The settings action's answer: a JSON array of names, sorted, empty and repeated ones left out.
            Assert.Equal(new List<string> { "Mods", "VIP" }, SchemaPage.GroupNames(@"[ ""VIP"", "" Mods "", """", ""vip"", 3, null ]"));
            Assert.Empty(SchemaPage.GroupNames(null));
            Assert.Empty(SchemaPage.GroupNames("not json"));
            Assert.Empty(SchemaPage.GroupNames(@"{ ""groups"": [ ""VIP"" ] }"));
        }

        private sealed class GroupHooks : NoHooks
        {
            public string Answer = @"[ ""VIP"", ""Moderators"", ""Regulars"", ""Vip Plus"" ]";
            public int Asked, WaitMs;
            public override string Groups()
            {
                Interlocked.Increment(ref Asked);
                if (WaitMs > 0) Thread.Sleep(WaitMs);
                return Answer;
            }
        }

        private const string GroupSchema = Head + @"
            { ""key"": ""vipGroups"", ""default"": ""Regulars"", ""kind"": ""grouplist"" },
            { ""key"": ""mutedGroups"", ""default"": """", ""kind"": ""grouplist"" } ] }";

        /// <summary>A grouplist field of the schema as the window builds it, with its box, its store and its list.</summary>
        private sealed class GroupField
        {
            public FrameworkElement Element;
            public TextBox Input, Store;
            public Popup List;

            public GroupField(PageContext ctx, WindowSpec spec, string key)
            {
                Element = SchemaPage.Render(ctx, spec.ResolvedSettings.First(s => s.Key == key));
                var boxes = Find<TextBox>(Element).ToList();
                Input = boxes.Single(b => !(b.Tag is FieldTag));
                Store = boxes.Single(b => b.Tag is FieldTag);
                List = Find<Popup>(Element).SingleOrDefault();
            }

            public List<string> Offered => List == null || !List.IsOpen ? new List<string>()
                : Find<Border>(List.Child).Where(b => Equals(b.Tag, Fields.SuggestionTag)).Select(b => ((TextBlock)b.Child).Text).ToList();

            public void Focus() => Input.RaiseEvent(new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice, 0, null, Input) { RoutedEvent = Keyboard.GotKeyboardFocusEvent });
            public void Leave() => Input.RaiseEvent(new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice, 0, Input, null) { RoutedEvent = Keyboard.LostKeyboardFocusEvent });

            public bool Press(Key key)
            {
                var press = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(Input), 0, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
                Input.RaiseEvent(press);
                return press.Handled;
            }

            public void Click(string name)
            {
                var row = Find<Border>(List.Child).Single(b => Equals(b.Tag, Fields.SuggestionTag) && ((TextBlock)b.Child).Text == name);
                row.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent });
            }
        }

        private static IEnumerable<T> Find<T>(object node) where T : class
        {
            if (node is T hit) yield return hit;
            if (node is DependencyObject d)
                foreach (var child in LogicalTreeHelper.GetChildren(d))
                    foreach (var found in Find<T>(child))
                        yield return found;
        }

        private static Window Shown(params FrameworkElement[] fields)
        {
            var page = new StackPanel();
            foreach (var field in fields) page.Children.Add(field);
            var window = new Window { Width = 500, Height = 400, Content = page, ShowInTaskbar = false, ShowActivated = false, Left = -20000, Top = -20000, WindowStartupLocation = WindowStartupLocation.Manual };
            window.Show();
            return window;
        }

        /// <summary>Lets the thread's dispatcher run what is queued, until the condition holds or the time is up.</summary>
        private static void Pump(int milliseconds, System.Func<bool> until)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            do
            {
                var frame = new DispatcherFrame();
                Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new System.Action(() => frame.Continue = false));
                Dispatcher.PushFrame(frame);
                if (until()) return;
                Thread.Sleep(10);
            }
            while (watch.ElapsedMilliseconds < milliseconds);
        }

        [Fact]
        public void TheGroupsAreAskedOnceAWindowWhenAGroupFieldFirstGetsTheCursor() => Sta(() =>
        {
            var spec = Prepared(GroupSchema);
            var hooks = new GroupHooks();
            var ctx = Entry.ContextFor(spec, (JObject)spec.Defaults.DeepClone(), new WindowParts(), null, hooks);
            var vip = new GroupField(ctx, spec, "vipGroups");
            var muted = new GroupField(ctx, spec, "mutedGroups");
            var window = Shown(vip.Element, muted.Element);
            try
            {
                Assert.Equal(0, hooks.Asked);                                  // not while the page is built
                vip.Focus();
                Pump(2000, () => vip.List.IsOpen);
                Assert.Equal(new[] { "Moderators", "VIP", "Vip Plus" }, vip.Offered);   // sorted, without the one added
                vip.Leave();
                Assert.False(vip.List.IsOpen);
                muted.Focus();
                Pump(2000, () => muted.List.IsOpen);
                Assert.Equal(new[] { "Moderators", "Regulars", "VIP", "Vip Plus" }, muted.Offered);
                muted.Leave();

                // The window rebuilt its pages: the same answer.
                var again = new GroupField(Entry.ContextFor(spec, (JObject)spec.Defaults.DeepClone(), new WindowParts(), null, hooks), spec, "mutedGroups");
                ((StackPanel)window.Content).Children.Add(again.Element);
                again.Focus();
                Pump(2000, () => again.List.IsOpen);
                Assert.Equal(new[] { "Moderators", "Regulars", "VIP", "Vip Plus" }, again.Offered);
                again.Leave();
                Assert.Equal(1, hooks.Asked);

                // Another window asks for itself.
                var other = new GroupHooks();
                var elsewhere = new GroupField(Entry.ContextFor(spec, (JObject)spec.Defaults.DeepClone(), new WindowParts(), null, other), spec, "vipGroups");
                ((StackPanel)window.Content).Children.Add(elsewhere.Element);
                elsewhere.Focus();
                Pump(2000, () => elsewhere.List.IsOpen);
                elsewhere.Leave();
                Assert.Equal(1, other.Asked);
            }
            finally { window.Close(); }
        });

        [Fact]
        public void AGroupIsPickedWithTheKeyboardOrAClickAndTypedNamesStillGoIn() => Sta(() =>
        {
            var spec = Prepared(GroupSchema);
            var ctx = Entry.ContextFor(spec, (JObject)spec.Defaults.DeepClone(), new WindowParts(), null, new GroupHooks());
            var field = new GroupField(ctx, spec, "vipGroups");
            var window = Shown(field.Element);
            try
            {
                field.Focus();
                Pump(2000, () => field.List.IsOpen);
                field.Input.Text = "vi";
                Assert.Equal(new[] { "VIP", "Vip Plus" }, field.Offered);
                Assert.True(field.Press(Key.Down));
                Assert.True(field.Press(Key.Enter));
                Assert.Equal("Regulars,VIP", field.Store.Text);
                Assert.Equal("", field.Input.Text);
                Assert.Equal(new[] { "Moderators", "Vip Plus" }, field.Offered);

                // A name no group has: Enter adds it as typed, as in any name list.
                field.Input.Text = "mods";
                Assert.False(field.List.IsOpen);
                Assert.True(field.Press(Key.Enter));
                Assert.Equal("Regulars,VIP,mods", field.Store.Text);

                // A beginning some groups share, nothing picked: Enter adds what is typed, not the first group.
                field.Input.Text = "Mod";
                Assert.Equal(new[] { "Moderators" }, field.Offered);
                Assert.True(field.Press(Key.Down));
                Assert.True(field.Press(Key.Up));                           // back to none picked
                field.Press(Key.Enter);
                Assert.Equal("Regulars,VIP,mods,Mod", field.Store.Text);

                field.Click("Vip Plus");
                Assert.Equal("Regulars,VIP,mods,Mod,Vip Plus", field.Store.Text);
                Assert.Equal(new[] { "Moderators" }, field.Offered);

                Assert.True(field.Press(Key.Escape));
                Assert.False(field.List.IsOpen);
                Assert.False(field.Press(Key.Escape));                      // closed: Esc is the window's again
                Assert.True(field.Press(Key.Down));                         // and Down brings the list back
                Assert.True(field.List.IsOpen);
            }
            finally { window.Close(); }
        });

        [Fact]
        public void WithoutGroupsFromTheSettingsActionTheFieldIsAsBefore() => Sta(() =>
        {
            // An older settings action has no Groups; the preview's hooks answer none.
            Assert.Null(new ReflectionHost(new object()).Groups());
            Assert.Null(new NoHooks().Groups());
            var spec = Prepared(GroupSchema);
            var ctx = Entry.ContextFor(spec, (JObject)spec.Defaults.DeepClone(), new WindowParts(), null, new ReflectionHost(new object()));
            var field = new GroupField(ctx, spec, "vipGroups");
            var window = Shown(field.Element);
            try
            {
                field.Focus();
                Pump(300, () => false);
                Assert.False(field.List.IsOpen);
                field.Input.Text = "vip";
                Assert.False(field.List.IsOpen);
                Assert.False(field.Press(Key.Down));
                Assert.True(field.Press(Key.Enter));
                Assert.Equal("Regulars,vip", field.Store.Text);
            }
            finally { window.Close(); }
        });

        [Fact]
        public void AStreamerBotThatTakesItsTimeLeavesTheBoxToTypeIn() => Sta(() =>
        {
            var spec = Prepared(GroupSchema);
            var hooks = new GroupHooks { WaitMs = 1500 };
            var ctx = Entry.ContextFor(spec, (JObject)spec.Defaults.DeepClone(), new WindowParts(), null, hooks);
            var field = new GroupField(ctx, spec, "vipGroups");
            var window = Shown(field.Element);
            try
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                field.Focus();
                Assert.True(watch.ElapsedMilliseconds < 1000, "the window waited " + watch.ElapsedMilliseconds + " ms for the groups");
                Assert.False(field.List.IsOpen);
                field.Input.Text = "v";
                Pump(5000, () => field.List.IsOpen);
                Assert.Equal(new[] { "VIP", "Vip Plus" }, field.Offered);     // what was typed meanwhile counts
            }
            finally { window.Close(); }
        });
    }
}
