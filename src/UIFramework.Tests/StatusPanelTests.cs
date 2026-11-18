using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Newtonsoft.Json.Linq;
using Xunit;

namespace UIFramework.Tests
{
    /// <summary>Status panels: an auto action that asks again every so many seconds while its page shows, the two
    /// lines an answer draws for it (a labelled value with its copy button, a pill), a button that saves the window's
    /// changes before it asks, and what the red "i" says of a declaration of these that cannot work.</summary>
    public class StatusPanelTests
    {
        private static void Sta(Action body)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    // This thread's own language and theme: what another test sets meanwhile is not read here.
                    Text.Use(Conventions.Default.Lang);
                    Theme.IsDark = Theme.IsDark;
                    body();
                }
                catch (Exception ex) { failure = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (failure != null) throw failure;
        }

        private static void Pump(int milliseconds)
        {
            var watch = Stopwatch.StartNew();
            do
            {
                var frame = new DispatcherFrame();
                Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() => frame.Continue = false));
                Dispatcher.PushFrame(frame);
                Thread.Sleep(10);
            }
            while (watch.ElapsedMilliseconds < milliseconds);
        }

        /// <summary>Runs this thread's dispatcher until the condition holds: an answer that was not there in a moment
        /// (a busy machine) arrives through it.</summary>
        private static void PumpUntil(Func<bool> holds, int milliseconds = 5000)
        {
            var watch = Stopwatch.StartNew();
            while (!holds() && watch.ElapsedMilliseconds < milliseconds)
                Pump(20);
        }

        private static bool Until(Func<bool> holds, int milliseconds)
        {
            var watch = Stopwatch.StartNew();
            while (!holds())
            {
                if (watch.ElapsedMilliseconds > milliseconds) return false;
                Thread.Sleep(20);
            }
            return true;
        }

        private const string Head = @"{ ""host"": ""demo"", ""version"": ""v1"", ""settings"": [ ";

        private static WindowSpec Prepared(string schemaJson)
        {
            try
            {
                var schema = Schema.Parse(schemaJson);
                var spec = Entry.SpecFor(schema, null);
                Entry.Prepare(spec, schema);
                return spec;
            }
            finally { Text.Use(Conventions.Default.Lang); }
        }

        /// <summary>The project behind the window: answers <c>test</c> as told.</summary>
        private sealed class Project : NoHooks
        {
            private readonly Func<string, JObject, string> _answer;
            public Project(Func<string, JObject, string> answer) { _answer = answer; }
            public override string Test(string name, JObject settings) => _answer(name, settings);
        }

        // What the shell gives a page and a test cannot: the setters are the framework's own.
        private static void Set(PageContext ctx, string property, object value) =>
            typeof(PageContext).GetProperty(property).SetValue(ctx, value);

        /// <summary>The settings as fields on one page, wired as the shell wires them.</summary>
        private static (PageContext Ctx, WindowParts Parts, StackPanel Page) Built(WindowSpec spec, IHostHooks hooks)
        {
            var saved = (JObject)spec.Defaults.DeepClone();
            var parts = new WindowParts();
            var ctx = Entry.ContextFor(spec, saved, parts, null, hooks);
            var page = new StackPanel();
            foreach (var setting in spec.ResolvedSettings)
                page.Children.Add(SchemaPage.Render(ctx, setting));
            foreach (var wire in ctx.AfterBuild)
                wire();
            return (ctx, parts, page);
        }

        private static IEnumerable<T> Find<T>(DependencyObject root) where T : DependencyObject
        {
            if (root is T hit) yield return hit;
            foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
                foreach (var found in Find<T>(child))
                    yield return found;
        }

        private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

        private static Border PillOf(DependencyObject root, string text) =>
            Find<Border>(root).Single(b => b.Child is TextBlock label && label.Text == text);

        private static Color Fill(Border pill) => ((SolidColorBrush)pill.Background).Color;
        private static Color Ink(Border pill) => ((SolidColorBrush)((TextBlock)pill.Child).Foreground).Color;

        // ---- The lines of an answer ----------------------------------------------------------------------------

        private const string Status = @"{ ""lines"": [
            { ""pill"": ""Running"", ""tone"": ""ok"" },
            { ""label"": ""Action id"", ""text"": ""4f1c-22aa"", ""copy"": true },
            { ""label"": ""Last post"", ""text"": ""14:02"" } ] }";

        [Fact]
        public void ALabelledValueIsDrawnWithItsLabelItsTextAndACopyButton() => Sta(() =>
        {
            var panel = new StackPanel();
            ResultPanel.Render(panel, Status);
            var texts = Find<TextBlock>(panel).Select(t => t.Text).ToList();
            Assert.Contains("Action id", texts);
            Assert.Contains("4f1c-22aa", texts);
            Assert.Contains("Last post", texts);
            Assert.Contains("14:02", texts);
            // Only the value that asks for it has the button, and it says what it is for.
            var copy = Assert.Single(Find<Button>(panel));
            Assert.Equal(Text.T("panel.copy"), copy.ToolTip);
            Assert.Equal(Text.T("panel.copy") + ": Action id", System.Windows.Automation.AutomationProperties.GetName(copy));
            // The labels of a box stand in one column: each row's first, sized together.
            var rows = panel.Children.OfType<Grid>().Where(g => g.Children.OfType<TextBlock>().Any()).ToList();
            Assert.Equal(2, rows.Count);
            Assert.True(Grid.GetIsSharedSizeScope(panel));
            Assert.All(rows, row => Assert.Equal(rows[0].ColumnDefinitions[0].SharedSizeGroup, row.ColumnDefinitions[0].SharedSizeGroup));
            Assert.False(string.IsNullOrEmpty(rows[0].ColumnDefinitions[0].SharedSizeGroup));
        });

        [Fact]
        public void TheCopyButtonCopiesTheTextAndSaysSoForAMoment() => Sta(() =>
        {
            var copied = new List<string>();
            var real = ResultPanel.Copy;
            ResultPanel.Copy = copied.Add;
            try
            {
                var panel = new StackPanel();
                ResultPanel.Render(panel, Status);
                var copy = Find<Button>(panel).Single();
                string Name() => System.Windows.Automation.AutomationProperties.GetName(copy);
                string Said() => copy.ToolTip is ToolTip tip ? ((TextBlock)tip.Content).Text : copy.ToolTip as string;
                string rest = Name();
                Assert.Equal(Icons.Copy, copy.Content);

                Click(copy);
                Assert.Equal(new[] { "4f1c-22aa" }, copied);
                // A tick in place of the two sheets, and the word as what the button says.
                Assert.Equal(Icons.Check, copy.Content);
                Assert.Equal(Theme.SuccessText, ((SolidColorBrush)copy.Foreground).Color);
                Assert.Equal(Text.T("panel.copied"), Said());
                Assert.Equal(Text.T("panel.copied"), Name());
                // The same answer again (the interval's next one) leaves the box as it is: the button goes on saying it.
                ResultPanel.Render(panel, Status);
                Assert.Same(copy, Find<Button>(panel).Single());
                Assert.Equal(Icons.Check, copy.Content);
                // Briefly: the button is itself again after a moment.
                Pump(ResultPanel.SaidMs + 700);
                Assert.Equal(Icons.Copy, copy.Content);
                Assert.Equal(Text.T("panel.copy"), Said());
                Assert.Equal(rest, Name());

                // A clipboard another program holds: nothing copied, and that is said instead.
                ResultPanel.Copy = text => throw new InvalidOperationException("held by another program");
                Click(copy);
                Assert.Equal(Icons.Copy, copy.Content);
                Assert.Equal(Theme.DangerText, ((SolidColorBrush)copy.Foreground).Color);
                Assert.Equal(Text.T("panel.notCopied", "held by another program"), Said());

                // Another answer draws the box anew.
                ResultPanel.Render(panel, Running);
                Assert.Empty(Find<Button>(panel));
            }
            finally { ResultPanel.Copy = real; }
        });

        [Fact]
        public void APillIsDrawnInItsTone() => Sta(() =>
        {
            var panel = new StackPanel();
            ResultPanel.Render(panel, @"{ ""lines"": [ { ""pill"": ""Running"", ""tone"": ""ok"" }, { ""pill"": ""Paused"", ""tone"": ""warn"" },
                { ""pill"": ""Off"", ""tone"": ""muted"" }, { ""pill"": ""Odd"", ""tone"": ""purple"" }, { ""pill"": ""Bare"" },
                { ""label"": ""State"", ""pill"": ""Idle"", ""tone"": ""WARN"", ""text"": ""since 14:02"" } ] }");
            // Green, amber, grey: the colours of the Log page's results.
            Assert.Equal(Theme.SuccessSoft, Fill(PillOf(panel, "Running")));
            Assert.Equal(Theme.SuccessText, Ink(PillOf(panel, "Running")));
            Assert.Equal(Theme.WarningSoft, Fill(PillOf(panel, "Paused")));
            Assert.Equal(Theme.WarningText, Ink(PillOf(panel, "Paused")));
            var muted = Ui.PillColours(PillStyle.Muted);
            foreach (string grey in new[] { "Off", "Odd", "Bare" })   // muted, a tone there is none of, no tone
            {
                Assert.Equal(muted.Background, Fill(PillOf(panel, grey)));
                Assert.Equal(muted.Foreground, Ink(PillOf(panel, grey)));
            }
            // A pill with a label in front and a text behind it is one line.
            var line = (Grid)panel.Children[5];
            Assert.Equal(new[] { "State", "Idle", "since 14:02" }, Find<TextBlock>(line).Select(t => t.Text).ToArray());
            Assert.Equal(Theme.WarningSoft, Fill(PillOf(line, "Idle")));
            Assert.Equal(PillStyle.Muted, ResultPanel.Tone(null));
        });

        [Fact]
        public void TheLinesAnAnswerHadBeforeAreDrawnAsBefore() => Sta(() =>
        {
            var panel = new StackPanel();
            ResultPanel.Render(panel, @"{ ""lines"": [ ""Plain"", { ""text"": ""27 %"", ""big"": true }, { ""text"": ""No button"", ""copy"": false },
                { ""copy"": true }, { ""label"": 7, ""text"": 4.5 }, { ""label"": ""Chance"", ""text"": ""31 %"", ""big"": true } ] }");
            Assert.Equal(new[] { "Plain", "27 %", "No button", "", "7", "4.5", "Chance", "31 %" }, Find<TextBlock>(panel).Select(t => t.Text).ToArray());
            Assert.Empty(Find<Button>(panel));   // nothing to copy without a text
            Assert.Equal(18, ((TextBlock)panel.Children[1]).FontSize);
            // A labelled value is big as a line of its own is.
            Assert.Equal(18, Find<TextBlock>(panel).Single(t => t.Text == "31 %").FontSize);
            Assert.Equal(12.5, Find<TextBlock>(panel).Single(t => t.Text == "4.5").FontSize);
        });

        [Fact]
        public void ThePanelReadsInTheDarkAndTheLightTheme() => Sta(() =>
        {
            bool was = Theme.IsDark;
            var real = ResultPanel.Copy;
            ResultPanel.Copy = text => { };
            try
            {
                foreach (bool dark in new[] { true, false })
                {
                    Theme.IsDark = dark;
                    var panel = new StackPanel();
                    ResultPanel.Render(panel, @"{ ""lines"": [ { ""label"": ""Action id"", ""text"": ""4f1c"", ""copy"": true },
                        { ""pill"": ""Running"", ""tone"": ""ok"" }, { ""pill"": ""Paused"", ""tone"": ""warn"" }, { ""pill"": ""Off"", ""tone"": ""muted"" } ] }");
                    Color box = Theme.Surface;   // what an action's answer sits on
                    string theme = dark ? "dark" : "light";
                    foreach (string text in new[] { "Running", "Paused", "Off" })
                    {
                        var pill = PillOf(panel, text);
                        Assert.True(Contrast(Ink(pill), Fill(pill)) >= 4.5, text + " is hard to read in the " + theme + " theme");
                        // The grey pill has the box's own grey, the light theme's green and amber its brightness: the
                        // line around a pill is what shows it as one.
                        Assert.True(pill.BorderThickness.Left >= 1 && Contrast(((SolidColorBrush)pill.BorderBrush).Color, box) >= 1.5,
                            text + " does not show as a pill in the " + theme + " theme");
                    }
                    var read = Find<TextBlock>(panel).Where(t => t.Text == "Action id" || t.Text == "4f1c").ToList();
                    Assert.Equal(2, read.Count);
                    foreach (var line in read)
                        Assert.True(Contrast(((SolidColorBrush)line.Foreground).Color, box) >= 4.5, line.Text + " is hard to read in the " + theme + " theme");
                    // The copy button's sheets, and the tick it shows once it copied.
                    var copy = Find<Button>(panel).Single();
                    Assert.True(Contrast(((SolidColorBrush)copy.Foreground).Color, box) >= 4.5, "the copy button is hard to see in the " + theme + " theme");
                    Click(copy);
                    Assert.True(Contrast(((SolidColorBrush)copy.Foreground).Color, box) >= 4.5, "the copy button's tick is hard to see in the " + theme + " theme");
                }
            }
            finally { Theme.IsDark = was; ResultPanel.Copy = real; }
        });

        private static double Contrast(Color a, Color b)
        {
            double Channel(byte c) { double v = c / 255.0; return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4); }
            double Luminance(Color c) => 0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);
            double la = Luminance(a), lb = Luminance(b);
            return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
        }

        [Fact]
        public void AnAutoActionDrawsTheLinesWhenItsPageShows() => Sta(() =>
        {
            var spec = Prepared(Head + @"{ ""key"": ""state"", ""default"": """", ""kind"": ""action"", ""auto"": true, ""every"": 10 } ] }");
            Assert.Empty(spec.Problems);
            var (_, parts, page) = Built(spec, new Project((name, values) => Status));
            Assert.DoesNotContain(Find<TextBlock>(page), t => t.Text == "Action id");
            parts.Refresh(spec.ResolvedSettings.Single().Page);
            PumpUntil(() => Find<TextBlock>(page).Any(t => t.Text == "Action id"));
            Assert.Equal(Theme.SuccessSoft, Fill(PillOf(page, "Running")));
            Assert.Contains(Find<TextBlock>(page), t => t.Text == "Action id");
            Assert.Single(Find<Button>(page));   // the copy button: an auto action has none of its own
        });

        // ---- saveFirst, on a page ------------------------------------------------------------------------------

        [Fact]
        public void AButtonWithSaveFirstAsksOnlyAfterTheSave() => Sta(() =>
        {
            var spec = Prepared(Head + @"{ ""key"": ""check"", ""default"": """", ""kind"": ""action"", ""saveFirst"": true },
                { ""key"": ""plain"", ""default"": """", ""kind"": ""action"" } ] }");
            Assert.Empty(spec.Problems);
            var steps = new List<string>();
            var (ctx, _, page) = Built(spec, new Project((name, values) => { lock (steps) steps.Add("asked " + name); return null; }));
            bool saves = false;
            Set(ctx, "SaveChanges", (Func<bool>)(() => { lock (steps) steps.Add("save"); return saves; }));
            var buttons = Find<Button>(page).ToList();

            int Steps() { lock (steps) return steps.Count; }

            Click(buttons[0]);                       // the save does not happen: the project is not asked
            Pump(Patient.QuickMs + 200);
            Assert.Equal(new[] { "save" }, steps);
            saves = true;
            Click(buttons[0]);                       // it does: first the save, then the question
            PumpUntil(() => Steps() == 3);
            Assert.Equal(new[] { "save", "save", "asked check" }, steps);
            Click(buttons[1]);                       // a button without saveFirst saves nothing
            PumpUntil(() => Steps() == 4);
            Assert.Equal(new[] { "save", "save", "asked check", "asked plain" }, steps);
        });

        // ---- What cannot work ----------------------------------------------------------------------------------

        [Fact]
        public void DeclarationsThatCannotWorkAreNamed()
        {
            var problems = Prepared(Head + @"
                { ""key"": ""state"", ""default"": """", ""kind"": ""action"", ""auto"": true, ""every"": 10 },
                { ""key"": ""atTheLimit"", ""default"": """", ""kind"": ""action"", ""auto"": true, ""every"": 5 },
                { ""key"": ""check"", ""default"": """", ""kind"": ""action"", ""saveFirst"": true },
                { ""key"": ""nick"", ""default"": ""x"", ""every"": 10 },
                { ""key"": ""press"", ""default"": """", ""kind"": ""action"", ""every"": 10 },
                { ""key"": ""fast"", ""default"": """", ""kind"": ""action"", ""auto"": true, ""every"": 2 },
                { ""key"": ""never"", ""default"": """", ""kind"": ""action"", ""auto"": true, ""every"": 0 },
                { ""key"": ""stake"", ""default"": 5, ""saveFirst"": true },
                { ""key"": ""follows"", ""default"": """", ""kind"": ""action"", ""auto"": true, ""saveFirst"": true } ] }").Problems;
            Assert.Equal(6, problems.Count);
            Assert.Single(problems, p => p.StartsWith("'nick' has every, which only an auto action has") && p.Contains("a string field"));
            Assert.Single(problems, p => p.StartsWith("'press' has every, which only an auto action has"));
            Assert.Single(problems, p => p == "'fast' asks every 2 seconds; the shortest is 5, which it takes instead.");
            Assert.Single(problems, p => p.StartsWith("'never' asks every 0 seconds"));
            Assert.Single(problems, p => p.StartsWith("'stake' has saveFirst, which only a button has") && p.Contains("an int field"));
            Assert.Single(problems, p => p.StartsWith("'follows' has saveFirst, which only a button has"));
        }

        [Fact]
        public void AnIntervalIsNeverShorterThanTheShortest()
        {
            Assert.Equal(5, SchemaPage.ShortestEvery);
            TimeSpan Seconds(long n) => TimeSpan.FromMilliseconds(n * SchemaPage.EverySecondMs);
            Assert.Equal(Seconds(10), SchemaPage.EveryInterval(10));
            Assert.Equal(Seconds(5), SchemaPage.EveryInterval(5));
            Assert.Equal(Seconds(5), SchemaPage.EveryInterval(2));
            Assert.Equal(Seconds(5), SchemaPage.EveryInterval(-3));
            // No longer than a timer runs: a declaration of years does not keep the page from being built.
            Assert.True(SchemaPage.EveryInterval(int.MaxValue).TotalMilliseconds <= int.MaxValue);
        }

        // ---- The interval and saveFirst, in the window ----------------------------------------------------------

        private const string Running = @"{ ""lines"": [ { ""pill"": ""Running"", ""tone"": ""ok"" } ] }";

        private const string Window = @"{ ""host"": ""demo"", ""version"": ""v1"", ""settingsFile"": ""x"", ""settings"": [
            { ""key"": ""bet"", ""default"": 10, ""page"": ""game"", ""label"": ""Bet"" },
            { ""key"": ""check"", ""default"": """", ""kind"": ""action"", ""saveFirst"": true, ""page"": ""game"", ""label"": ""Check now"" },
            { ""key"": ""state"", ""default"": """", ""kind"": ""action"", ""auto"": true, ""every"": 5, ""page"": ""status"", ""label"": ""State"" } ],
            ""window"": { ""title"": ""Demo"", ""pages"": [ { ""id"": ""game"", ""title"": ""Game"" }, { ""id"": ""status"", ""title"": ""Status"" },
                { ""id"": ""probe"", ""title"": ""Probe"", ""custom"": true } ] } }";

        /// <summary>The window with a second of <c>every</c> as short as given, so five of them pass in a test's time.</summary>
        private static void WithSeconds(int milliseconds, Action body)
        {
            int real = SchemaPage.EverySecondMs;
            SchemaPage.EverySecondMs = milliseconds;
            try { body(); }
            finally { SchemaPage.EverySecondMs = real; }
        }

        [Fact]
        public void TheIntervalAsksOnlyWhileItsPageShowsAndTheWindowIsOpen() => WithSeconds(40, () =>   // every 5: 200 ms
        {
            int asked = 0;
            var w = new Opened(Window, (name, settings) => { if (name == "state") Interlocked.Increment(ref asked); return Running; });
            try
            {
                // The window opens on another page: the panel costs nothing.
                Thread.Sleep(800);
                Assert.Equal(0, Volatile.Read(ref asked));

                w.On(() => w.Show("Status"));
                Assert.True(Until(() => Volatile.Read(ref asked) >= 3, 15000), "the interval does not ask while the page shows");
                Assert.True(w.On(() => w.Shows("Running")));

                // Another page in front: the interval stops.
                w.On(() => w.Show("Game"));
                int hidden = Volatile.Read(ref asked);
                Thread.Sleep(1000);
                Assert.Equal(hidden, Volatile.Read(ref asked));

                // Back on the page it runs again.
                w.On(() => w.Show("Status"));
                Assert.True(Until(() => Volatile.Read(ref asked) >= hidden + 3, 15000), "the interval does not start again");

                // The window's content built anew on another page (a theme switch, a reset): the page that was is gone,
                // and its interval with it.
                w.On(() => w.Rebuild("game"));
                int rebuilt = Volatile.Read(ref asked);
                Thread.Sleep(1000);
                Assert.Equal(rebuilt, Volatile.Read(ref asked));
                w.On(() => w.Show("Status"));
                Assert.True(Until(() => Volatile.Read(ref asked) >= rebuilt + 3, 15000), "the interval does not run after a rebuild");

                // The window closed: nothing asks any more.
                w.Dispose();
                int closed = Volatile.Read(ref asked);
                Thread.Sleep(1000);
                Assert.Equal(closed, Volatile.Read(ref asked));
            }
            finally { w.Dispose(); }
        });

        [Fact]
        public void ComingBackToThePageAsksAtOnce()
        {
            // A real five seconds between two questions: what is asked within three of them is asked by the page.
            int asked = 0;
            using (var w = new Opened(Window, (name, settings) => { if (name == "state") Interlocked.Increment(ref asked); return Running; }))
            {
                w.On(() => w.Show("Status"));
                Assert.True(Until(() => Volatile.Read(ref asked) == 1, 3000));
                w.On(() => w.Show("Game"));
                w.On(() => w.Show("Status"));
                Assert.True(Until(() => Volatile.Read(ref asked) == 2, 3000));
                Thread.Sleep(600);
                Assert.Equal(2, Volatile.Read(ref asked));   // and once
            }
        }

        [Fact]
        public void ARequestStillUnderWayIsNotDoubled() => WithSeconds(40, () =>
        {
            int asked = 0;
            var answer = new ManualResetEventSlim();
            try
            {
                using (var w = new Opened(Window, (name, settings) =>
                {
                    if (name != "state") return null;
                    Interlocked.Increment(ref asked);
                    answer.Wait(20000);                       // a project that takes its time
                    return Running;
                }))
                {
                    w.On(() => w.Show("Status"));              // back after the quick wait, the answer still out
                    Thread.Sleep(1300);                        // six intervals
                    Assert.Equal(1, Volatile.Read(ref asked));
                    // Nor by leaving the page and coming back: the question that is out is the one to wait for.
                    w.On(() => w.Show("Game"));
                    w.On(() => w.Show("Status"));
                    Thread.Sleep(500);
                    Assert.Equal(1, Volatile.Read(ref asked));

                    answer.Set();
                    Assert.True(Until(() => Volatile.Read(ref asked) >= 2, 15000), "the interval does not go on once the answer is there");
                    Assert.True(Until(() => w.On(() => w.Shows("Running")), 5000));
                }
            }
            finally { answer.Set(); }
        });

        [Fact]
        public void InTheWindowTheCopyButtonSaysItOverItself()
        {
            var copied = new List<string>();
            var real = ResultPanel.Copy;
            ResultPanel.Copy = text => { lock (copied) copied.Add(text); };
            try
            {
                using (var w = new Opened(Window, (name, settings) => name == "state" ? Status : null))
                {
                    w.On(() => w.Show("Status"));
                    Assert.True(Until(() => w.On(() => w.Shows("Action id")), 5000));
                    var copy = w.On(() => w.Buttons().Single(b => b.ToolTip as string == Text.T("panel.copy")));
                    w.On(() => copy.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, copy)));
                    Assert.Equal(new[] { "4f1c-22aa" }, copied);
                    // The word opens over the button by itself, and goes by itself.
                    Assert.True(w.On(() => copy.ToolTip is ToolTip said && said.IsOpen && ((TextBlock)said.Content).Text == Text.T("panel.copied")));
                    Assert.True(Until(() => w.On(() => copy.ToolTip is string), ResultPanel.SaidMs + 5000));
                }
            }
            finally { ResultPanel.Copy = real; }
        }

        [Fact]
        public void SaveFirstSavesTheChangesBeforeTheProjectIsAsked()
        {
            using (var w = new Opened(Window))
            {
                var asked = new List<(int? InFile, int With)>();
                int validated = 0;
                w.OnValidate = settings => { Interlocked.Increment(ref validated); return null; };
                w.OnTest = (name, settings) =>
                {
                    if (name != "check") return null;
                    int? inFile = File.Exists(w.SettingsPath) ? (int?)(int)JObject.Parse(File.ReadAllText(w.SettingsPath))["bet"] : null;
                    lock (asked) asked.Add((inFile, (int)settings["bet"]));
                    return "Checked.";
                };

                // Nothing unsaved: it just asks, and nothing is checked or written.
                w.On(() => w.Click("Check now"));
                Assert.True(Until(() => asked.Count == 1, 5000));
                Assert.Equal(0, validated);
                Assert.False(File.Exists(w.SettingsPath));

                // A change: saved as Save saves it (the project's check included), then the question, with what was saved.
                w.On(() => w.Box("bet").Text = "20");
                w.On(() => w.Click("Check now"));
                Assert.True(Until(() => asked.Count == 2, 5000));
                Assert.Equal(1, validated);
                Assert.Equal((20, 20), (asked[1].InFile ?? -1, asked[1].With));
                Assert.Equal(20, (int)w.Saved()["bet"]);
                Assert.True(Until(() => w.On(() => w.Shows("Checked.")), 5000));   // the answer, over "Saved"
                Assert.False(w.On(() => w.Shows(Text.T("status.unsaved.one"))));
            }
        }

        [Fact]
        public void SaveFirstDoesNotAskWhenTheSaveIsRefused()
        {
            using (var w = new Opened(Window))
            {
                int asked = 0;
                w.OnTest = (name, settings) => { if (name == "check") Interlocked.Increment(ref asked); return "Checked."; };
                w.OnValidate = settings => "The bet is odd.";
                w.On(() => w.Box("bet").Text = "21");

                // The check's question, as after Save; answered No, nothing is written and the project is not asked.
                var pressed = w.Later(() => w.Click("Check now"));
                var question = w.Dialog(8000);
                Assert.NotEqual(IntPtr.Zero, question);
                Assert.Contains("The bet is odd.", Opened.TextOf(question));
                Opened.Answer(question, Opened.IdNo);
                Assert.True(pressed.Wait(10000));
                Thread.Sleep(Patient.QuickMs + 300);
                Assert.Equal(0, Volatile.Read(ref asked));
                Assert.False(File.Exists(w.SettingsPath));
                Assert.True(w.On(() => w.Shows(Text.T("status.notSaved"))));
                Assert.Equal("21", w.On(() => w.Box("bet").Text));

                // Answered Yes, the save happens and the project is asked.
                pressed = w.Later(() => w.Click("Check now"));
                question = w.Dialog(8000);
                Assert.NotEqual(IntPtr.Zero, question);
                Opened.Answer(question, Opened.IdYes);
                Assert.True(pressed.Wait(10000));
                Assert.True(Until(() => Volatile.Read(ref asked) == 1, 5000));
                Assert.Equal(21, (int)w.Saved()["bet"]);
            }
        }

        // ---- The window, opened as a project opens it ------------------------------------------------------

        /// <summary>Shell.Run on a thread of its own, which starts the window's thread, as the settings action does. A
        /// custom page hands the test each build's context, and with it the window's thread and controls.</summary>
        private sealed class Opened : NoHooks, IDisposable
        {
            public readonly string Folder;
            public readonly string SettingsPath;
            public Func<JObject, string> OnValidate = settings => null;
            public Func<string, JObject, string> OnTest;

            private readonly Thread _runner;
            private readonly ManualResetEventSlim _built = new ManualResetEventSlim();
            private volatile PageContext _context;
            private FrameworkElement _probe;
            private uint _thread;
            private bool _disposed;

            public Opened(string schemaJson, Func<string, JObject, string> test = null)
            {
                OnTest = test ?? ((name, settings) => null);
                Folder = Path.Combine(Path.GetTempPath(), "UIFramework.Tests", "status-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(Folder);
                SettingsPath = Path.Combine(Folder, "settings.json");
                var schema = Schema.Parse(schemaJson);
                var spec = Entry.SpecFor(schema, SettingsPath);
                _runner = new Thread(() => { try { Entry.Show(spec, schema, new QuietLog(), this); } catch (Exception) { } }) { IsBackground = true };
                _runner.Start();
                if (!_built.Wait(15000))
                    throw new TimeoutException("The window was not built.");
                // Up and idle: shown, laid out, the first status written.
                Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            }

            public Dispatcher Dispatcher => _probe.Dispatcher;
            public Window Window => (Window)Up(_probe, n => n is Window);

            public override FrameworkElement BuildPage(string pageId, PageContext context)
            {
                _context = context;
                _probe = new TextBlock { Text = "probe" };
                _thread = GetCurrentThreadId();
                _built.Set();
                return _probe;
            }

            public override string Validate(JObject settings) => OnValidate(settings);
            public override string Test(string name, JObject settings) => OnTest(name, settings);

            public T On<T>(Func<T> body) => Dispatcher.Invoke(body);
            public void On(Action body) => Dispatcher.Invoke(body);

            /// <summary>Runs on the window's thread without waiting for it; the event is set once it has run.</summary>
            public ManualResetEventSlim Later(Action body)
            {
                var done = new ManualResetEventSlim();
                Dispatcher.BeginInvoke(new Action(() => { try { body(); } finally { done.Set(); } }));
                return done;
            }

            public JObject Saved() => JObject.Parse(File.ReadAllText(SettingsPath));

            public TextBox Box(string key) => _context.Parts.Boxes[key];

            /// <summary>A button by its text, clicked as the mouse would.</summary>
            public void Click(string text)
            {
                var button = All(Window).OfType<Button>().First(b => b.Content as string == text);
                button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button));
            }

            /// <summary>A page by its title, opened from the rail as a click on it does.</summary>
            public void Show(string title)
            {
                var item = All(Window).OfType<Border>().First(b => (b.Name ?? "").StartsWith("Rail") && System.Windows.Automation.AutomationProperties.GetName(b) == title);
                item.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonUpEvent });
            }

            /// <summary>The window's content built anew on a page, as after a theme switch.</summary>
            public void Rebuild(string pageId) => _context.Reopen(pageId);

            public IEnumerable<Button> Buttons() => All(Window).OfType<Button>();
            public IEnumerable<string> Texts() => All(Window).OfType<TextBlock>().Select(t => t.Text ?? "");
            public bool Shows(string text) => Texts().Contains(text);

            private static object Up(DependencyObject node, Func<DependencyObject, bool> wanted)
            {
                while (node != null && !wanted(node))
                    node = LogicalTreeHelper.GetParent(node) ?? (node is Visual ? VisualTreeHelper.GetParent(node) : null);
                return node;
            }

            private static IEnumerable<object> All(object node)
            {
                yield return node;
                if (node is DependencyObject d)
                    foreach (var child in LogicalTreeHelper.GetChildren(d))
                        foreach (var below in All(child))
                            yield return below;
            }

            /// <summary>A message box of the window's thread, waited for up to <paramref name="ms"/>; zero when none came.</summary>
            public IntPtr Dialog(int ms)
            {
                var watch = Stopwatch.StartNew();
                do
                {
                    IntPtr found = IntPtr.Zero;
                    EnumThreadWindows(_thread, (hwnd, l) =>
                    {
                        var name = new StringBuilder(64);
                        GetClassName(hwnd, name, name.Capacity);
                        if (name.ToString() != "#32770") return true;
                        found = hwnd;
                        return false;
                    }, IntPtr.Zero);
                    if (found != IntPtr.Zero) return found;
                    Thread.Sleep(20);
                }
                while (watch.ElapsedMilliseconds < ms);
                return IntPtr.Zero;
            }

            public const int IdYes = 6, IdNo = 7;

            public static void Answer(IntPtr dialog, int button) => PostMessage(dialog, 0x0111 /* WM_COMMAND */, (IntPtr)button, IntPtr.Zero);

            public static string TextOf(IntPtr dialog)
            {
                var text = new StringBuilder(2048);
                GetDlgItemText(dialog, 0xFFFF, text, text.Capacity);   // a message box's text is its static with id -1
                return text.ToString();
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                try
                {
                    Shell.Close(SettingsPath);
                    var watch = Stopwatch.StartNew();
                    while (_runner.IsAlive && watch.ElapsedMilliseconds < 15000)
                    {
                        // A question a failed test left open is answered, so the window can go.
                        var dialog = Dialog(0);
                        if (dialog != IntPtr.Zero)
                        {
                            PostMessage(dialog, 0x0010 /* WM_CLOSE */, IntPtr.Zero, IntPtr.Zero);
                            Answer(dialog, IdNo);
                        }
                        Thread.Sleep(50);
                    }
                }
                finally
                {
                    Text.Use(Conventions.Default.Lang);
                    try { Directory.Delete(Folder, true); } catch (Exception) { }
                }
            }

            private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);
            [DllImport("user32.dll")] private static extern bool EnumThreadWindows(uint threadId, EnumWindowsProc callback, IntPtr lParam);
            [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hwnd, StringBuilder name, int max);
            [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern uint GetDlgItemText(IntPtr dialog, int id, StringBuilder text, int max);
            [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
            [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
        }

        private sealed class QuietLog : IHostLog
        {
            public void Info(string message) { }
            public void Warn(string message) { }
            public void Error(string message) { }
        }
    }
}
