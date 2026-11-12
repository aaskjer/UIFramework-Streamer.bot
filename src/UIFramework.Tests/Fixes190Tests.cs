using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Newtonsoft.Json.Linq;
using Xunit;

namespace UIFramework.Tests
{
    /// <summary>1.9.0: the Log page's result colours are declared; the game is asked without holding the window still,
    /// and no answer is not "done"; an import is checked like a save; the declaration check says more; a schema
    /// written by hand may use the builder's short forms; the leftovers of the audit of 2026-11-08.</summary>
    public class Fixes190Tests
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

        private static void Pump(int milliseconds)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            do
            {
                var frame = new DispatcherFrame();
                Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new System.Action(() => frame.Continue = false));
                Dispatcher.PushFrame(frame);
                Thread.Sleep(10);
            }
            while (watch.ElapsedMilliseconds < milliseconds);
        }

        private static WindowSpec Prepared(string schemaJson, bool hostBuildsPages = true)
        {
            try
            {
                var schema = Schema.Parse(schemaJson);
                var spec = Entry.SpecFor(null, schema, null);
                spec.HostBuildsPages = hostBuildsPages;
                Entry.Prepare(spec, schema);
                return spec;
            }
            finally { Text.Use(Conventions.Default.Lang); }
        }

        private static WindowSpec Window(string windowJson) => SpecJson.Parse(JObject.Parse(windowJson));

        // ---- The Log page's colours, declared ---------------------------------------------------------------

        [Theory]
        [InlineData("GAME-OVER", "game-over", true)]
        [InlineData("WIN-*", "WIN-LTTR", true)]
        [InlineData("*LTTR", "Miss-Lttr", true)]
        [InlineData("WIN-*", "WIN", false)]
        [InlineData("WIN", "DUEL WIN", false)]         // the whole result, not a part of it
        [InlineData("a.b", "aXb", false)]              // a dot is a dot
        [InlineData("", "WIN", false)]
        public void AResultIsNamedAsItIsOrWithAStar(string pattern, string result, bool matches) =>
            Assert.Equal(matches, LogPage.Matches(pattern, result));

        [Fact]
        public void AGameDeclaresTheColoursOfItsResults()
        {
            var log = Window(@"{ ""title"": ""Hangman"", ""log"": { ""folder"": ""x"", ""files"": ""*.txt"",
                ""styles"": { ""GAME-OVER"": ""muted"", ""WIN-*"": ""success"", ""MISS-*"": ""warning"", ""ROUND"": ""none"" },
                ""outcome"": { ""DUEL WIN"": ""WIN"", ""CP_*"": ""Channel points"" } } }").Log;

            // Before, "Game-Over" and "Miss-Lttr" had no word the framework knew and showed in the accent colour.
            Assert.Equal(PillStyle.Accent, LogPage.DefaultStyle("GAME-OVER"));
            Assert.Equal(PillStyle.Muted, LogPage.StyleOf(log, "Game-Over"));
            Assert.Equal(PillStyle.Success, LogPage.StyleOf(log, "WIN-LTTR"));
            Assert.Equal(PillStyle.Warning, LogPage.StyleOf(log, "MISS-LTTR"));
            Assert.Equal(PillStyle.None, LogPage.StyleOf(log, "round"));
            // What no declaration names goes by the words in it, as before.
            Assert.Equal(PillStyle.Muted, LogPage.StyleOf(log, "LOSS"));
            Assert.Equal(PillStyle.Accent, LogPage.StyleOf(log, "START"));
            // The filter value: declared, else the result in capitals.
            Assert.Equal("WIN", LogPage.OutcomeOf(log, "Duel Win"));
            Assert.Equal("CHANNEL POINTS", LogPage.OutcomeOf(log, "CP_START"));
            Assert.Equal("LOSS", LogPage.OutcomeOf(log, " loss "));
            // A result is coloured by its filter value too: "DUEL WIN" is a WIN.
            Assert.Equal(PillStyle.Success, LogPage.StyleOf(log, "DUEL WIN"));
        }

        [Fact]
        public void TheFirstDeclarationThatFitsWins()
        {
            var log = Window(@"{ ""log"": { ""styles"": [ ""WIN-BIG|accent"", ""WIN-*|success"", ""*|muted"" ] } }").Log;
            Assert.Equal(PillStyle.Accent, LogPage.StyleOf(log, "WIN-BIG"));
            Assert.Equal(PillStyle.Success, LogPage.StyleOf(log, "WIN-SMALL"));
            Assert.Equal(PillStyle.Muted, LogPage.StyleOf(log, "ANYTHING"));
        }

        [Fact]
        public void TheBuilderWritesTheColoursIntoTheSchema()
        {
            var json = new UiWindow("Game").Log("Game/logs", "log*.txt", styles: new[] { "GAME-OVER|muted", "WIN-*|success" }, outcome: new[] { "DUEL WIN|WIN" }).ToJson();
            Assert.Equal("muted", (string)json["log"]["styles"]["GAME-OVER"]);
            Assert.Equal(new[] { "GAME-OVER", "WIN-*" }, ((JObject)json["log"]["styles"]).Properties().Select(p => p.Name).ToArray());   // in order
            Assert.Equal("WIN", (string)json["log"]["outcome"]["DUEL WIN"]);
            var log = SpecJson.Parse(json).Log;
            Assert.Equal(PillStyle.Muted, LogPage.StyleOf(log, "GAME-OVER"));
            // A window without them says nothing about them: an older DLL sees the schema it always saw.
            Assert.Null(new UiWindow("Game").Log("Game/logs", "log*.txt").ToJson()["log"]["styles"]);
        }

        [Fact]
        public void AStyleTheFrameworkDoesNotHaveIsNamed()
        {
            var spec = Prepared(@"{ ""host"": ""demo"", ""settings"": [ { ""key"": ""a"", ""default"": 1 } ],
                ""window"": { ""title"": ""Demo"", ""log"": { ""folder"": ""x"", ""files"": ""*.txt"", ""styles"": { ""WIN"": ""green"", ""LOSS"": ""muted"" } } } }");
            var problem = Assert.Single(spec.Problems, p => p.Contains("'green'"));
            Assert.Contains("success, warning, muted, accent, none", problem);
            Assert.Equal(PillStyle.Success, LogPage.StyleOf(spec.Log, "WIN"));   // the unknown style is not applied: the words decide
        }

        // ---- Asking the game without holding the window still -----------------------------------------------

        [Fact]
        public void AQuickAnswerIsThereBeforeTheQuestionReturns() => Sta(() =>
        {
            string got = null;
            bool waited = false;
            Patient.Ask(() => "done", (answer, error) => got = answer, () => waited = true);
            Assert.Equal("done", got);
            Assert.False(waited);
        });

        [Fact]
        public void ASlowAnswerArrivesLaterOnTheSameThread() => Sta(() =>
        {
            string got = null;
            int thread = 0;
            bool waited = false;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            Patient.Ask(() => { Thread.Sleep(Patient.QuickMs + 1500); return "late"; }, (answer, error) => { got = answer; thread = Thread.CurrentThread.ManagedThreadId; }, () => waited = true);
            // Back long before the game answered: the window's thread is free.
            Assert.True(watch.ElapsedMilliseconds < Patient.QuickMs + 1000, "the question held the thread for " + watch.ElapsedMilliseconds + " ms");
            Assert.Null(got);
            Assert.True(waited);
            Pump(2500);
            Assert.Equal("late", got);
            Assert.Equal(Thread.CurrentThread.ManagedThreadId, thread);
        });

        [Fact]
        public void WhatTheGameThrewArrivesAsItWas() => Sta(() =>
        {
            System.Exception got = null;
            Patient.Ask<string>(() => throw new System.Reflection.TargetInvocationException(new System.TimeoutException("the action did not answer")), (answer, error) => got = error);
            Assert.IsType<System.TimeoutException>(got);
            Assert.Equal("the action did not answer", got.Message);
        });

        [Fact]
        public void WaitingKeepsTheWindowAliveAndItsInputAway() => Sta(() =>
        {
            var busy = new List<bool>();
            int ticks = 0;
            var timer = new DispatcherTimer { Interval = System.TimeSpan.FromMilliseconds(40) };
            timer.Tick += (s, e) => ticks++;
            timer.Start();
            string answer = Patient.Wait(() => { Thread.Sleep(Patient.QuickMs + 900); return "checked"; }, busy.Add);
            timer.Stop();
            Assert.Equal("checked", answer);
            Assert.Equal(new[] { true, false }, busy);
            Assert.True(ticks >= 3, "the window's thread did not run while it waited (" + ticks + " ticks)");
        });

        [Fact]
        public void AQuickCheckNeverTouchesTheWindow() => Sta(() =>
        {
            var busy = new List<bool>();
            Assert.Null(Patient.Wait<string>(() => null, busy.Add));
            Assert.Empty(busy);
            Assert.Throws<System.InvalidOperationException>(() => Patient.Wait<string>(() => throw new System.InvalidOperationException("no"), busy.Add));
        });

        [Fact]
        public void NoAnswerIsToldApartFromAnEmptyOne()
        {
            Assert.True(Patient.IsNoAnswer(Patient.NoAnswer + ": the action 'Game' did not answer within five seconds."));
            Assert.Equal("the action 'Game' did not answer within five seconds.", Patient.Why(Patient.NoAnswer + ": the action 'Game' did not answer within five seconds."));
            Assert.False(Patient.IsNoAnswer(null));
            Assert.False(Patient.IsNoAnswer(""));
            Assert.False(Patient.IsNoAnswer("@alice rolls 6"));
            Assert.Equal("", Patient.Why("@alice rolls 6"));
        }

        [Fact]
        public void AFailureInAPanelShowsInTheColourOfAFailure() => Sta(() =>
        {
            var panel = new StackPanel();
            ResultPanel.Render(panel, "Roll: the game did not answer (timeout)", failed: true);
            var line = Assert.IsType<TextBlock>(Assert.Single(panel.Children));
            Assert.Equal(Theme.DangerText, ((System.Windows.Media.SolidColorBrush)line.Foreground).Color);
            // Text that only looks like an answer's JSON is shown as the failure it is, not read as an answer.
            ResultPanel.Render(panel, "{ \"lines\": [ \"x\" ] }", failed: true);
            Assert.Contains("lines", Assert.IsType<TextBlock>(Assert.Single(panel.Children)).Text);
        });

        // ---- An import is checked like a save ---------------------------------------------------------------

        private static (Fields Fields, WindowParts Parts, StackPanel Page) Page(string defaultsJson)
        {
            var parts = new WindowParts();
            var defaults = JObject.Parse(defaultsJson);
            var store = new SettingsStore(System.IO.Path.GetTempFileName(), defaults);
            return (new Fields(parts, store.Merged(new JObject()), defaults, _ => { }), parts, new StackPanel());
        }

        [Fact]
        public void AnImportedNumberIsPulledIntoItsFieldsLimits() => Sta(() =>
        {
            var (f, parts, page) = Page(@"{ ""bet"": 50 }");
            page.Children.Add(f.IntField("Bet", "bet", ""));
            var tag = (FieldTag)parts.Boxes["bet"].Tag;
            tag.Min = 10;
            tag.Max = 100;
            Assert.Null(FieldBinding.AsSaved(page, "bet", 5000, out var saved));
            Assert.Equal(100, (int)saved);
            Assert.Null(FieldBinding.AsSaved(page, "bet", "25", out saved));          // a number as text is a number
            Assert.Equal(25, (int)saved);
            Assert.NotNull(FieldBinding.AsSaved(page, "bet", "plenty", out _));       // text is not
            Assert.NotNull(FieldBinding.AsSaved(page, "bet", new JArray(1, 2), out _));
            Assert.NotNull(FieldBinding.AsSaved(page, "bet", 2.5, out _));            // nor a fraction in a whole-number field
        });

        [Fact]
        public void AnImportedSwitchDropdownAndListAreWhatTheirFieldsHold() => Sta(() =>
        {
            var (f, parts, page) = Page(@"{ ""on"": false, ""mode"": ""a"", ""name"": """" }");
            page.Children.Add(f.Toggle("On", "on", ""));
            page.Children.Add(f.StringCombo("Mode", "mode", new[] { ("a", "A"), ("b", "B") }, ""));
            page.Children.Add(f.TextField("Name", "name", ""));

            Assert.Null(FieldBinding.AsSaved(page, "on", true, out var saved));
            Assert.True((bool)saved);
            Assert.Null(FieldBinding.AsSaved(page, "on", "false", out saved));
            Assert.False((bool)saved);
            Assert.NotNull(FieldBinding.AsSaved(page, "on", "sometimes", out _));
            Assert.NotNull(FieldBinding.AsSaved(page, "on", 1, out _));

            Assert.Null(FieldBinding.AsSaved(page, "mode", "b", out saved));
            Assert.Equal("b", (string)saved);
            Assert.NotNull(FieldBinding.AsSaved(page, "mode", "c", out _));           // not one of its options

            Assert.Null(FieldBinding.AsSaved(page, "name", 7, out saved));            // a text field stores text
            Assert.Equal(JTokenType.String, saved.Type);
            Assert.NotNull(FieldBinding.AsSaved(page, "name", new JObject(), out _));

            // A key no field shows (a page the user hid) is taken as it is.
            Assert.Null(FieldBinding.AsSaved(page, "elsewhere", 12, out saved));
            Assert.Equal(12, (int)saved);
        });

        [Fact]
        public void OnlyAValueShownOtherThanTheFileHasItIsToldToTheUser() => Sta(() =>
        {
            var parts = new WindowParts();
            var defaults = JObject.Parse(@"{ ""bet"": 50, ""maxBet"": 0 }");
            var settings = new SettingsStore(System.IO.Path.GetTempFileName(), defaults).Merged(JObject.Parse(@"{ ""bet"": 5000, ""maxBet"": 0 }"));
            var f = new Fields(parts, settings, defaults, _ => { });
            var page = new StackPanel();
            page.Children.Add(f.IntField("Bet", "bet", ""));
            page.Children.Add(f.IntField("Highest bet", "maxBet", "", true));   // 0 reads "no limit" and is stored as the largest number
            ((FieldTag)parts.Boxes["bet"].Tag).Max = 100;

            // Both are written with the next save, as before...
            Assert.Equal(new[] { "bet", "maxBet" }, FieldBinding.Repaired(settings, page).OrderBy(k => k).ToArray());
            // ...but only the bet reads other than the file has it: the status names that one, not a fresh game's ceilings.
            Assert.Equal(new[] { "bet" }, FieldBinding.Repaired(settings, page, shownOtherwise: true).ToArray());
        });

        // ---- The declaration check says more ----------------------------------------------------------------

        private const string Head = @"{ ""host"": ""demo"", ""version"": ""v1"", ""settings"": [ ";

        [Fact]
        public void WhatOnlyAButtonHasIsNamedOnAnyOtherField()
        {
            var problems = Prepared(Head + @"
                { ""key"": ""odds"", ""default"": ""x"", ""panel"": true },
                { ""key"": ""sure"", ""default"": 5, ""confirm"": ""Really?"" },
                { ""key"": ""auto1"", ""default"": """", ""auto"": true },
                { ""key"": ""fine"", ""default"": """", ""kind"": ""action"", ""panel"": true, ""confirm"": ""Really?"" } ] }").Problems;
            Assert.Single(problems, p => p.StartsWith("'odds' has panel"));
            Assert.Single(problems, p => p.StartsWith("'sure' has confirm"));
            Assert.Single(problems, p => p.StartsWith("'auto1' has auto"));
            Assert.DoesNotContain(problems, p => p.Contains("'fine'"));
        }

        [Fact]
        public void PlaceholdersPlatformsAndTwitchOnlyAreChecked()
        {
            var problems = Prepared(Head + @"
                { ""key"": ""name"", ""default"": """", ""placeholders"": [ ""user"" ] },
                { ""key"": ""emote"", ""default"": ""Kappa"", ""kind"": ""emote"", ""platform"": ""twich"" },
                { ""key"": ""fineEmote"", ""default"": ""Kappa"", ""kind"": ""emote"", ""platform"": ""Kick"" },
                { ""key"": ""rows"", ""default"": [], ""kind"": ""list"", ""columns"": [ { ""key"": ""e"", ""kind"": ""emote"", ""platform"": ""youtub"" } ] },
                { ""key"": ""hint"", ""default"": ""Read this"", ""kind"": ""note"", ""twitchOnly"": true },
                { ""key"": ""reward"", ""default"": """", ""twitchOnly"": true },
                { ""key"": ""won"", ""default"": ""{user} wins {amount} {points}"", ""kind"": ""template"", ""page"": ""messages"", ""placeholders"": [ ""user"", ""amount"" ] } ] }").Problems;
            Assert.Single(problems, p => p.StartsWith("'name' has placeholders"));
            Assert.Single(problems, p => p.Contains("'twich'") && p.Contains("twitch, youtube, kick"));
            Assert.DoesNotContain(problems, p => p.Contains("'fineEmote'"));
            Assert.Single(problems, p => p.Contains("'youtub'") && p.Contains("column 'e'"));
            Assert.Single(problems, p => p.StartsWith("'hint' has twitchOnly"));
            Assert.DoesNotContain(problems, p => p.Contains("'reward'"));     // any labelled field can say "Twitch only"
            Assert.Single(problems, p => p.Contains("{points}") && p.Contains("'won'"));
            Assert.DoesNotContain(problems, p => p.Contains("{user}") || p.Contains("{amount}"));
        }

        [Fact]
        public void AnUnknownKindIsToldTheKindsThereAre()
        {
            var problem = Assert.Single(Prepared(Head + @"{ ""key"": ""wheel"", ""default"": """", ""kind"": ""wheel"" } ] }").Problems);
            Assert.Contains("The kinds are", problem);
            Assert.Contains("template", problem);
            Assert.Contains("colour", problem);
            Assert.DoesNotContain("color,", problem);       // one spelling in the list
        }

        [Fact]
        public void APageIdThatIsASlipOfThePenIsNamed()
        {
            var slip = Prepared(Head + @"{ ""key"": ""a"", ""default"": 1 } ], ""window"": { ""title"": ""Demo"", ""pages"": [ ""game"", ""mesages"", ""about"" ] } }");
            Assert.Single(slip.Problems, p => p.Contains("'mesages'") && p.Contains("messages, log, help and about"));
            var fine = Prepared(Head + @"{ ""key"": ""a"", ""default"": 1 }, { ""key"": ""b"", ""default"": 1, ""page"": ""extras"" } ],
                ""window"": { ""title"": ""Demo"", ""pages"": [ ""game"", ""extras"", ""help"", ""about"" ] } }");
            Assert.Empty(fine.Problems);
        }

        [Fact]
        public void ACustomPageBehindTheSettingsActionIsSaidToBeLeftOut()
        {
            const string schema = Head + @"{ ""key"": ""a"", ""default"": 1 }, { ""key"": ""deck"", ""default"": 1 } ],
                ""window"": { ""title"": ""Demo"", ""pages"": [ ""game"", { ""id"": ""cards"", ""title"": ""Cards"", ""custom"": true, ""fields"": [ ""deck"" ] } ] } }";
            var behindStub = Prepared(schema, hostBuildsPages: false);
            var problem = Assert.Single(behindStub.Problems, p => p.Contains("'cards'"));
            Assert.Contains("'deck'", problem);
            Assert.Empty(Prepared(schema).Problems);        // a host in code builds the page itself
        }

        [Fact]
        public void WhatTheBuildersOneLineTypeLetsThroughIsNamed()
        {
            // Tip, Cmd, Link and Note are one type in the host block, so the wrong one compiles and was dropped unsaid.
            var window = new UiWindow("Demo")
                .Tour(UiSchema.Tip("save"), UiSchema.Cmd("!roll", "rolls"))
                .Help(UiSchema.H("Commands", UiSchema.Cmd("!roll", "rolls"), UiSchema.Tip("save", "Save", "saves")))
                .About(links: new[] { UiSchema.Link("Docs", "https://example.com"), UiSchema.Note("not a link") });
            var spec = SpecJson.Parse(window.ToJson());
            Assert.Equal(3, spec.ReadProblems.Count);
            Assert.Single(spec.ReadProblems, p => p.Contains("tour"));
            Assert.Single(spec.ReadProblems, p => p.Contains("'Commands'"));
            Assert.Single(spec.ReadProblems, p => p.Contains("About"));
            Assert.Single(spec.Tour);
            Assert.Single(spec.About.Links);
            // They reach the red i with every resolve, which gathers its list anew.
            var prepared = Prepared(Head + @"{ ""key"": ""a"", ""default"": 1 } ], ""window"": " + window.ToJson().ToString(Newtonsoft.Json.Formatting.None) + " }");
            Assert.Equal(3, prepared.Problems.Count);
        }

        // ---- A schema written by hand ----------------------------------------------------------------------

        [Fact]
        public void TheBuildersShortFormsAreReadFromJson()
        {
            var s = Setting.Parse(JObject.Parse(@"{ ""key"": ""mode"", ""default"": ""a"", ""kind"": ""options"",
                ""options"": [ ""a|Label A"", ""b"", 3 ], ""shows"": [ ""a: one, two"", ""b: two"" ],
                ""aliases"": [ ""OldMode"", ""OlderMode"" ], ""aliasFactor"": 100 }"));
            Assert.Equal(new[] { "a", "b", "3" }, s.Options.Select(o => o.Value).ToArray());
            Assert.Equal(new[] { "Label A", "b", "3" }, s.Options.Select(o => o.Label).ToArray());
            Assert.Equal(new[] { "one", "two" }, s.Shows["a"].ToArray());
            Assert.Equal(new[] { "two" }, s.Shows["b"].ToArray());
            Assert.Equal(100.0, s.AliasFactors["OldMode"]);
            Assert.False(s.AliasFactors.ContainsKey("OlderMode"));

            var list = Setting.Parse(JObject.Parse(@"{ ""key"": ""rows"", ""default"": [], ""kind"": ""list"",
                ""columns"": [ ""word"", ""hint|text|A hint | or two"", ""emote|emote|Emote|kick"", ""by|string|readonly"", { ""key"": ""n"", ""kind"": ""int"" } ] }"));
            Assert.Equal(new[] { "word", "hint", "emote", "by", "n" }, list.Columns.Select(c => c.Key).ToArray());
            Assert.Equal(new[] { "string", "text", "emote", "string", "int" }, list.Columns.Select(c => c.Kind).ToArray());
            Assert.Equal("A hint | or two", list.Columns[1].Label);
            Assert.Equal("kick", list.Columns[2].Platform);
            Assert.True(list.Columns[3].ReadOnly);
            Assert.Equal("by", list.Columns[3].Label);
        }

        [Fact]
        public void AutoBringsLiveAndPanelAndAskIsItsOwnName()
        {
            var auto = Setting.Parse(JObject.Parse(@"{ ""key"": ""odds"", ""default"": """", ""kind"": ""action"", ""auto"": true }"));
            Assert.True(auto.Live);
            Assert.True(auto.Panel);
            var ask = Setting.Parse(JObject.Parse(@"{ ""key"": ""preset"", ""default"": ""easy"", ""ask"": true }"));
            Assert.True(ask.Ask);
            Assert.False(ask.Live);
            Assert.True((bool)ask.ToJson()["ask"]);
            // From the builder: the same name in the schema.
            Assert.True((bool)UiSchema.ToJson(UiSchema.S("preset", "easy", ask: true))["ask"]);
        }

        [Fact]
        public void TheBuilderCanSayWhatOnlyTheSchemaCould()
        {
            var json = new UiWindow("Demo").InLanguage("de").Pages(UiSchema.P("stats", "Stats").AtBottom(), "about").ToJson();
            Assert.Equal("de", (string)json["language"]);
            Assert.True((bool)json["pages"][0]["bottom"]);
            var spec = SpecJson.Parse(json);
            Assert.Equal("de", spec.Language);
            Assert.True(spec.SpecPages[0].Bottom);
        }

        [Fact]
        public void TheGamesNameIsStreamerBotsWhenItGivesNone()
        {
            var cph = new FakeCph();
            var args = new Dictionary<string, object> { ["uiframework.request"] = "validate", ["uiframework.values"] = "{}", ["actionName"] = "My Game" };
            Assert.True(UiHost.Handle(args, cph, null, "demo", "v1", "Demo/settings.json", validate: s => throw new System.InvalidOperationException("boom")));
            Assert.Contains(cph.Log, line => line.StartsWith("error [My Game]"));
        }

        // ---- The window ------------------------------------------------------------------------------------

        [Fact]
        public void TheHelpSwitchCountsAsAChangeAndIsTakenBackWithTheLayout()
        {
            string folder = Path.Combine(Path.GetTempPath(), "uiframework-190-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                var layout = LayoutFile.Load(Path.Combine(folder, "settings.json"));
                Assert.Equal(0, layout.ChangeCount);
                layout.HelpLines = true;
                Assert.Equal(1, layout.ChangeCount);      // the bar said "Nothing changed yet" with the help lines on
                layout.SetField("minBet", "label", "Lowest bet");
                Assert.Equal(2, layout.ChangeCount);
                layout.ResetLayout();
                Assert.Equal(0, layout.ChangeCount);
                Assert.False(layout.HelpLines);
            }
            finally { Directory.Delete(folder, true); }
        }

        [Fact]
        public void TheRailTakesTheKeyboard() => Sta(() =>
        {
            var rail = new Rail();
            var opened = new List<string>();
            var first = rail.Item("Game", "🎲", () => opened.Add("game"));
            var second = rail.Item("Chat", "💬", () => opened.Add("chat"));
            Assert.True(first.Focusable);
            var window = new Window { Width = 200, Height = 200, ShowInTaskbar = false, ShowActivated = false, Left = -20000, Top = -20000, WindowStartupLocation = WindowStartupLocation.Manual,
                Content = new StackPanel { Children = { first, second } } };
            window.Show();
            try
            {
                void Press(UIElement on, Key key) =>
                    on.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(on), 0, key) { RoutedEvent = Keyboard.KeyDownEvent });
                Press(second, Key.Enter);
                Press(first, Key.Space);
                Assert.Equal(new[] { "chat", "game" }, opened);
            }
            finally { window.Close(); }
        });

        [Fact]
        public void ASwitchsLabelWrapsInsteadOfBeingCut() => Sta(() =>
        {
            var (f, _, _) = Page(@"{ ""on"": false }");
            var toggle = f.Toggle("Den Chat auch dann benachrichtigen, wenn die Runde ohne Gewinner endet", "on", "");
            var label = Assert.IsType<TextBlock>(toggle.Content);
            Assert.Equal(TextWrapping.Wrap, label.TextWrapping);
            // In a narrow window it takes more lines, not more width than there is.
            var host = new Border { Width = 220, Child = toggle };
            host.Measure(new Size(220, double.PositiveInfinity));
            host.Arrange(new Rect(0, 0, 220, host.DesiredSize.Height));
            Assert.True(toggle.ActualWidth <= 220, "the switch is " + toggle.ActualWidth + " wide");
            Assert.True(label.ActualHeight > 30, "the label did not wrap (" + label.ActualHeight + " high)");
            // The Twitch-only pill still finds the label's text.
            Fields.TwitchOnly(toggle);
            var row = Assert.IsType<StackPanel>(toggle.Content);
            Assert.StartsWith("Den Chat", Assert.IsType<TextBlock>(row.Children[0]).Text);
        });

        [Fact]
        public void AReadOnlyValueHasItsHelpAndAnyFieldCanSayTwitchOnly() => Sta(() =>
        {
            var spec = Prepared(Head + @"
                { ""key"": ""status"", ""default"": ""idle"", ""readOnly"": true, ""help"": ""What the game is doing right now."" },
                { ""key"": ""reward"", ""default"": """", ""label"": ""Reward id"", ""twitchOnly"": true } ] }");
            try
            {
                var parts = new WindowParts();
                var ctx = Entry.ContextFor(spec, (JObject)spec.Defaults.DeepClone(), parts, null, null);
                var shown = SchemaPage.Render(ctx, spec.ResolvedSettings.First(s => s.Key == "status"));
                Assert.Equal("What the game is doing right now.", shown.ToolTip);
                var reward = SchemaPage.Render(ctx, spec.ResolvedSettings.First(s => s.Key == "reward"));
                var label = Assert.IsType<TextBlock>(((StackPanel)reward).Children[0]);
                Assert.Equal("Reward id (" + Text.T("field.twitchOnly") + ")", label.Text);
            }
            finally { Text.Use(Conventions.Default.Lang); }
        });

        [Fact]
        public void AnEmptyAccentBoxOpensThePickerOnTheColourItsSwatchShows() => Sta(() =>
        {
            // The picker's start is what Fields hands it: for a box with a swatch of its own, the swatch's colour.
            var parts = new WindowParts();
            var fields = new Fields(parts, new JObject { ["accent"] = "" }, new JObject { ["accent"] = "" }, _ => { });
            var shown = System.Windows.Media.Color.FromRgb(1, 2, 3);
            var row = fields.ColourField("Accent", "accent", "", labelled: false, swatchOf: text => new System.Windows.Media.SolidColorBrush(shown));
            var picker = new ColourPicker();
            var box = parts.Boxes["accent"];
            var window = new Window { Width = 300, Height = 100, ShowInTaskbar = false, ShowActivated = false, Left = -20000, Top = -20000, WindowStartupLocation = WindowStartupLocation.Manual, Content = row };
            window.Show();
            try
            {
                IEnumerable<string> Texts(object node)
                {
                    if (node is TextBlock block) yield return block.Text;
                    if (node is DependencyObject d)
                        foreach (var child in LogicalTreeHelper.GetChildren(d))
                            foreach (var text in Texts(child))
                                yield return text;
                }
                List<string> Channels()
                {
                    var popup = (System.Windows.Controls.Primitives.Popup)typeof(ColourPicker).GetField("_popup", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(picker);
                    return Texts(popup.Child).ToList();
                }
                picker.Open(box, box, shown);
                Assert.True(picker.IsOpen);
                var withFallback = Channels();
                picker.Open(box, box);                       // without one: the picker's yellow, #F1C40F
                var without = Channels();
                Assert.Contains("241", without);
                Assert.DoesNotContain("241", withFallback);
                Assert.Contains("1", withFallback);
                Assert.Contains("2", withFallback);
                Assert.Contains("3", withFallback);
            }
            finally { window.Close(); }
        });
    }
}
