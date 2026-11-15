using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Newtonsoft.Json.Linq;
using Xunit;

namespace UIFramework.Tests
{
    /// <summary>2.0.0, the schema's pages: a field with <c>ask</c> asks with what the window shows, and its answers
    /// land in the order it asked them.</summary>
    public class Fixes1100SchemaTests
    {
        private static void Sta(Action body)
        {
            Exception failure = null;
            var thread = new Thread(() => { try { body(); } catch (Exception ex) { failure = ex; } });
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
                Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() => frame.Continue = false));
                Dispatcher.PushFrame(frame);
                Thread.Sleep(10);
            }
            while (watch.ElapsedMilliseconds < milliseconds);
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

        /// <summary>The game behind the window: answers <c>test</c> as told and keeps what it was asked with.</summary>
        private sealed class Game : NoHooks
        {
            private readonly Func<string, JObject, string> _answer;
            public readonly List<JObject> Asked = new List<JObject>();
            public Game(Func<string, JObject, string> answer) { _answer = answer; }
            public override string Test(string name, JObject settings)
            {
                lock (Asked) Asked.Add(settings);
                return _answer(name, settings);
            }
        }

        // What the shell gives a page and a test cannot: the setters are the framework's own.
        private static void Set(PageContext ctx, string property, object value) =>
            typeof(PageContext).GetProperty(property).SetValue(ctx, value);

        /// <summary>The settings as fields on one page, wired as the shell wires them: the saved copy in
        /// <see cref="PageContext.Settings"/>, what the fields show in <see cref="PageContext.LiveSettings"/>.</summary>
        private static (PageContext Ctx, WindowParts Parts, StackPanel Page) Built(WindowSpec spec, IHostHooks hooks, List<string> status = null)
        {
            var saved = (JObject)spec.Defaults.DeepClone();
            var parts = new WindowParts();
            var ctx = Entry.ContextFor(spec, saved, parts, null, hooks);
            var page = new StackPanel();
            foreach (var setting in spec.ResolvedSettings)
                page.Children.Add(SchemaPage.Render(ctx, setting));
            Set(ctx, "LiveSettings", (Func<JObject>)(() =>
            {
                var live = (JObject)saved.DeepClone();
                FieldBinding.Save(live, page, transientToo: true);
                return live;
            }));
            if (status != null)
                Set(ctx, "SetStatus", (Action<string, System.Windows.Media.Color, string>)((text, colour, why) => status.Add(text)));
            foreach (var wire in ctx.AfterBuild)
                wire();
            return (ctx, parts, page);
        }

        // ---- A field with `ask` ---------------------------------------------------------------------------

        [Fact]
        public void AnAskFieldAsksWithWhatTheWindowShows() => Sta(() =>
        {
            var spec = Prepared(Head + @"{ ""key"": ""preset"", ""default"": ""easy"", ""ask"": true }, { ""key"": ""bet"", ""default"": 10 } ] }");
            var game = new Game((name, values) => null);
            var (_, parts, _) = Built(spec, game);
            parts.Boxes["bet"].Text = "25";
            parts.Boxes["preset"].Text = "hard";
            // The 1.9.0 name for what `live` meant on such a field: the game got the saved values, "easy" and 10.
            var asked = Assert.Single(game.Asked);
            Assert.Equal("hard", (string)asked["preset"]);
            Assert.Equal(25, (int)asked["bet"]);
        });

        [Fact]
        public void AnAskFieldsEarlierAnswerDoesNotLandOverALaterOne() => Sta(() =>
        {
            var spec = Prepared(Head + @"{ ""key"": ""preset"", ""default"": ""easy"", ""ask"": true }, { ""key"": ""bet"", ""default"": 10 } ] }");
            int calls = 0;
            var game = new Game((name, values) =>
            {
                // The first question waits behind another request of the settings action; the second is answered at once.
                if (Interlocked.Increment(ref calls) == 1) { Thread.Sleep(Patient.QuickMs + 300); return @"{ ""set"": { ""bet"": 1 } }"; }
                return @"{ ""set"": { ""bet"": 2 } }";
            });
            var (_, parts, _) = Built(spec, game);
            parts.Boxes["preset"].Text = "medium";
            parts.Boxes["preset"].Text = "hard";
            Assert.Equal("2", parts.Boxes["bet"].Text);
            Pump(Patient.QuickMs + 900);
            Assert.Equal(2, game.Asked.Count);
            // The answer for "medium" came last and was put over the one for "hard", which the field shows.
            Assert.Equal("2", parts.Boxes["bet"].Text);
        });

        // ---- An action's answer -------------------------------------------------------------------------------

        private static IEnumerable<T> Find<T>(DependencyObject root) where T : DependencyObject
        {
            if (root is T hit) yield return hit;
            foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
                foreach (var found in Find<T>(child))
                    yield return found;
        }

        private static void Click(Button button) =>
            button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));

        [Fact]
        public void AnActionsStatusReachesTheFooterBesideItsPanel() => Sta(() =>
        {
            var spec = Prepared(Head + @"{ ""key"": ""fleet"", ""default"": """", ""kind"": ""action"", ""panel"": true } ] }");
            var game = new Game((name, values) => @"{ ""lines"": [ ""Five ships."" ], ""status"": ""Fleet preset applied."" }");
            var status = new List<string>();
            var (_, _, page) = Built(spec, game, status);
            Click(Find<Button>(page).Single());
            // The panel showed the lines and the status went nowhere: docs/Schema.md has both from one answer.
            Assert.Equal(new[] { "Fleet preset applied." }, status);
            Assert.Contains(Find<TextBlock>(page), line => line.Text == "Five ships.");
        });

        // ---- An emote field ---------------------------------------------------------------------------------

        [Fact]
        public void AYouTubeEmoteFieldHasItsPictureAsAListColumnDoes() => Sta(() =>
        {
            // A code the table does not know: the picture stays empty and nothing is downloaded.
            var spec = Prepared(Head + @"{ ""key"": ""reel"", ""default"": "":not-an-emoji:"", ""kind"": ""emote"", ""platform"": ""youtube"" } ] }");
            var (_, _, page) = Built(spec, new NoHooks());
            var picture = Assert.Single(Find<System.Windows.Controls.Image>(page));
            Assert.Equal(":not-an-emoji:", picture.Tag);
        });

        // ---- The standard pages ---------------------------------------------------------------------------

        private const string OneSetting = Head + @"{ ""key"": ""a"", ""default"": 1 } ], ""window"": { ""title"": ""Demo"", ";

        [Theory]
        [InlineData(@"""help"": [ { ""heading"": ""Playing"", ""lines"": [ ""Type !play."" ] } ]")]
        [InlineData(@"""triggers"": [ { ""name"": ""Demo: Won"", ""code"": ""demo_onWin"", ""what"": ""Somebody won."" } ]")]
        [InlineData(@"""methods"": [ { ""name"": ""StartRound"", ""what"": ""Starts a round."" } ]")]
        public void TheHelpPageComesByItselfWithWhatGoesOnIt(string declared)
        {
            // Only a window that named "help" among its pages had one; the sections, triggers and methods went unseen.
            var spec = Prepared(OneSetting + declared + " } }");
            Assert.Equal(new[] { "game", "help", "about" }, spec.Pages.Select(p => p.Id).ToArray());
            Assert.Empty(spec.Problems);
            Assert.DoesNotContain(Prepared(OneSetting + @"""about"": { ""credits"": ""Me"" } } }").Pages, p => p.Id == "help");
        }

        [Fact]
        public void ALogWithoutItsPageIsNamed()
        {
            const string log = @"""log"": { ""folder"": ""Demo/Logs"", ""files"": ""log*.txt"", ""writeLogKey"": ""logToFile"" }";
            const string settings = Head + @"{ ""key"": ""a"", ""default"": 1 }, { ""key"": ""logToFile"", ""default"": true, ""page"": ""log"" } ], ""window"": { ""title"": ""Demo"", ";
            var problem = Assert.Single(Prepared(settings + log + " } }").Problems);
            Assert.Contains("'log'", problem);
            Assert.Contains("'logToFile'", problem);
            Assert.Empty(Prepared(settings + log + @", ""pages"": [ ""game"", ""log"", ""about"" ] } }").Problems);
        }

        // ---- What a standard page draws itself ----------------------------------------------------------------

        /// <summary>Every page built as the shell builds it: the keys each one shows a field for, null for a page
        /// that is left out.</summary>
        private static Dictionary<string, List<string>> Shown(WindowSpec spec, IHostHooks hooks = null)
        {
            var shown = new Dictionary<string, List<string>>();
            foreach (var page in spec.Pages)
            {
                var parts = new WindowParts();
                var ctx = Entry.ContextFor(spec, (JObject)spec.Defaults.DeepClone(), parts, null, hooks ?? new NoHooks());
                shown[page.Id] = page.Build?.Invoke(ctx) == null ? null : parts.Elements.Keys.ToList();
            }
            return shown;
        }

        private const string Senders = @"{ ""key"": ""useBot"", ""default"": true }, { ""key"": ""sendAction"", ""default"": false }, { ""key"": ""numberSeparator"", ""default"": "","" },
            { ""key"": ""cooldownUseBot"", ""default"": true }, { ""key"": ""cooldownSendAction"", ""default"": false }, ";

        [Fact]
        public void SenderSwitchesWithoutMessagesAreFieldsOnTheirPages() => Sta(() =>
        {
            // No message to draw them with: no Messages page, and the five had no field anywhere.
            var spec = Prepared(Head + Senders + @"{ ""key"": ""a"", ""default"": 1 } ] }");
            var shown = Shown(spec);
            Assert.False(shown.ContainsKey("messages"));
            Assert.Equal(new[] { "useBot", "sendAction", "numberSeparator" }, shown["general"].OrderBy(k => k == "useBot" ? 0 : k == "sendAction" ? 1 : 2).ToArray());
            Assert.Equal(new[] { "cooldownUseBot", "cooldownSendAction" }, shown["cooldowns"].ToArray());
            Assert.Empty(spec.Problems);
        });

        [Fact]
        public void CooldownSwitchesWithoutACooldownMessageAreFieldsOnTheirPage() => Sta(() =>
        {
            // Blackjack's cooldown messages have no group: the Messages page drew the game's senders and not the pair.
            var spec = Prepared(Head + Senders.Replace(@"""cooldownUseBot"", ""default"": true", @"""cooldownUseBot"", ""default"": true, ""page"": ""messages""")
                .Replace(@"""cooldownSendAction"", ""default"": false", @"""cooldownSendAction"", ""default"": false, ""page"": ""messages""") + @"
                { ""key"": ""msgWin"", ""default"": ""{user} wins"", ""kind"": ""template"", ""page"": ""messages"", ""group"": ""Game"" },
                { ""key"": ""msgWait"", ""default"": ""Wait, {user}"", ""kind"": ""template"", ""page"": ""messages"" } ],
                ""window"": { ""title"": ""Demo"", ""pages"": [ ""messages"", ""about"" ], ""senders"": { ""cooldownGroup"": ""Cooldown"" } } }");
            var shown = Shown(spec);
            foreach (var key in new[] { "useBot", "sendAction", "numberSeparator", "cooldownUseBot", "cooldownSendAction" })
                Assert.Single(shown["messages"], key);
            Assert.Empty(spec.Problems);
            // With a message in the group the page draws the pair itself, and a page naming one is told so.
            var grouped = Prepared(Head + Senders + @"
                { ""key"": ""msgWait"", ""default"": ""Wait, {user}"", ""kind"": ""template"", ""page"": ""messages"", ""group"": ""Cooldown"" } ],
                ""window"": { ""title"": ""Demo"", ""pages"": [ { ""id"": ""chat"", ""fields"": [ ""cooldownUseBot"" ] }, ""messages"", ""about"" ] } }");
            Assert.Single(grouped.Problems, p => p.Contains("'cooldownUseBot', which the Messages page shows"));
            Assert.DoesNotContain("cooldownUseBot", Shown(grouped)["chat"] ?? new List<string>());
        });

        [Fact]
        public void APageThatNamesASenderSwitchTheMessagesPageDoesNotDrawShowsIt() => Sta(() =>
        {
            var spec = Prepared(Head + Senders + @"{ ""key"": ""a"", ""default"": 1 } ],
                ""window"": { ""title"": ""Demo"", ""pages"": [ ""game"", { ""id"": ""chat"", ""title"": ""Chat"", ""fields"": [ ""useBot"", ""cooldown*"" ] }, ""about"" ] } }");
            var shown = Shown(spec);
            Assert.Equal(new[] { "useBot", "cooldownUseBot", "cooldownSendAction" }, shown["chat"].ToArray());
            Assert.Empty(spec.Problems);
        });

        [Fact]
        public void FieldsOnAStandardPageThatHasNothingOfItsOwnStay() => Sta(() =>
        {
            // The Messages page without messages and the Help page without help were left out, and the fields with them.
            var spec = Prepared(Head + @"{ ""key"": ""useBot"", ""default"": true, ""page"": ""messages"" },
                { ""key"": ""tips"", ""default"": true, ""page"": ""help"" }, { ""key"": ""a"", ""default"": 1 } ] }");
            var shown = Shown(spec);
            Assert.Equal(new[] { "useBot" }, shown["messages"].ToArray());
            Assert.Equal(new[] { "tips" }, shown["help"].ToArray());
        });

        /// <summary>A game with a log and nothing in it yet.</summary>
        private sealed class EmptyLog : NoHooks
        {
            public override IEnumerable<LogEntry> ReadLog() => new List<LogEntry>();
        }

        [Fact]
        public void AHiddenSenderSwitchNumberFormatOrLogSwitchHasNoField() => Sta(() =>
        {
            // The Messages and the Log page drew them from the window's keys, hidden or not.
            var spec = Prepared(Head + @"{ ""key"": ""useBot"", ""default"": true }, { ""key"": ""sendAction"", ""default"": false, ""hidden"": true },
                { ""key"": ""numberSeparator"", ""default"": "","" }, { ""key"": ""logToFile"", ""default"": true, ""page"": ""log"", ""hidden"": true },
                { ""key"": ""msgWin"", ""default"": ""{user} wins"", ""kind"": ""template"", ""page"": ""messages"" } ],
                ""window"": { ""title"": ""Demo"", ""pages"": [ ""messages"", ""log"", ""about"" ], ""overrides"": { ""numberSeparator"": { ""hidden"": true } },
                    ""log"": { ""folder"": ""Demo/Logs"", ""files"": ""log*.txt"", ""writeLogKey"": ""logToFile"" } } }");
            var shown = Shown(spec, new EmptyLog());
            Assert.Contains("useBot", shown["messages"]);
            Assert.DoesNotContain("sendAction", shown["messages"]);
            Assert.DoesNotContain("numberSeparator", shown["messages"]);
            Assert.DoesNotContain("logToFile", shown["log"]);
            Assert.DoesNotContain(shown.Values.Where(keys => keys != null), keys => keys.Contains("sendAction") || keys.Contains("numberSeparator") || keys.Contains("logToFile"));
            Assert.Empty(spec.Problems);
            // The values stay in the file.
            Assert.False((bool)spec.Defaults["sendAction"]);
            Assert.True((bool)spec.Defaults["logToFile"]);
        });

        // ---- A row with an action -----------------------------------------------------------------------------

        [Fact]
        public void AnAnswerUnderARowHidesWithItsActionAlsoWithTheHelpUnderTheFields() => Sta(() =>
        {
            var spec = Prepared(Head + @"{ ""key"": ""sw"", ""default"": true },
                { ""key"": ""tryBet"", ""default"": 10, ""row"": ""try"", ""transient"": true },
                { ""key"": ""tryIt"", ""default"": """", ""kind"": ""action"", ""panel"": true, ""row"": ""try"", ""showWhen"": ""sw"", ""help"": ""Rolls once with the bet."" } ] }");
            spec.Layout.HelpLines = true;
            var parts = new WindowParts();
            var ctx = Entry.ContextFor(spec, (JObject)spec.Defaults.DeepClone(), parts, null, new Game((name, values) => "Rolled a 6."));
            var page = SchemaPage.Build(ctx, spec.ResolvedSettings, "game");
            foreach (var wire in ctx.AfterBuild)
                wire();
            Click(Find<Button>(page).Single());
            var answer = Find<Border>(page).Single(b => Equals(b.Tag, "action-answer"));
            var holder = (Border)answer.Parent;
            Assert.Equal(Visibility.Visible, answer.Visibility);
            Assert.Equal(Visibility.Visible, holder.Visibility);
            parts.Switches["sw"].IsChecked = false;
            // The condition hides the action's help-line wrapper; the box under the row followed the button inside it.
            Assert.Equal(Visibility.Collapsed, holder.Visibility);
        });

        // ---- The check of split sections ------------------------------------------------------------------

        [Fact]
        public void ASplitSectionIsLookedForWhereTheWindowsPagesPutTheFields()
        {
            // statsTop goes to the stats page: S is one run of fields on the game page, though it was said to be split.
            var claimedAway = Prepared(Head + @"{ ""key"": ""a"", ""default"": 1, ""section"": ""S"" }, { ""key"": ""statsTop"", ""default"": 1, ""section"": ""T"" },
                { ""key"": ""b"", ""default"": 1, ""section"": ""S"" } ],
                ""window"": { ""title"": ""Demo"", ""pages"": [ ""game"", { ""id"": ""stats"", ""title"": ""Stats"", ""fields"": [ ""stats*"" ] }, ""about"" ] } }");
            Assert.Empty(claimedAway.Problems);
            // One page takes the fields of two: S shows its heading twice there, and nothing said so.
            var claimedTogether = Prepared(Head + @"{ ""key"": ""a"", ""default"": 1, ""page"": ""one"", ""section"": ""S"" }, { ""key"": ""x"", ""default"": 1, ""page"": ""two"", ""section"": ""T"" },
                { ""key"": ""b"", ""default"": 1, ""page"": ""one"", ""section"": ""S"" } ],
                ""window"": { ""title"": ""Demo"", ""pages"": [ { ""id"": ""all"", ""title"": ""All"", ""fields"": [ ""*"" ] }, ""about"" ] } }");
            Assert.Contains(claimedTogether.Problems, p => p.Contains("The section 'S' on the page 'all' is split"));
        }

        // ---- Page titles in another language -------------------------------------------------------------

        [Fact]
        public void APagesOwnTitleStaysInALanguageThatDoesNotTitleItsId()
        {
            const string settings = Head + @"{ ""key"": ""a"", ""default"": 1 }, { ""key"": ""b"", ""default"": 1, ""page"": ""leaderboard"" } ],
                ""lang"": { ""nl"": { ""keys"": { ""a"": ""Een"" } } }, ""window"": { ""title"": ""Demo"", ";
            const string pages = @"""pages"": [ { ""id"": ""game"", ""title"": ""Round"" }, { ""id"": ""leaderboard"", ""title"": ""Scoreboard"" },
                { ""id"": ""help"", ""title"": ""How to play"" }, ""about"" ], ""help"": [ { ""heading"": ""Playing"", ""lines"": [ ""Type !play."" ] } ] } }";
            string[] Titles(string language) => Prepared(settings + @"""language"": """ + language + @""", " + pages).Pages.Select(p => p.Title).Take(3).ToArray();
            // Dutch, with nothing but the game's own texts: the conventions' English titles replaced the game's.
            Assert.Equal(new[] { "Round", "Scoreboard", "How to play" }, Titles("nl"));
            // German titles the ids, and translates a declared wording it knows.
            Assert.Equal(new[] { "Spiel", "Bestenliste", "Anleitung" }, Titles("de"));
        }
    }
}
