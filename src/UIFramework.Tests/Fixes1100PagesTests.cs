using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using Newtonsoft.Json.Linq;
using Xunit;

namespace UIFramework.Tests
{
    /// <summary>2.0.0, the standard pages: the Log page's folder button opens the folder beside Streamer.bot, lines of
    /// one second stand newest first, a line without a time has no time; the Help page's Fire button and the emote
    /// pictures ask the game without holding the window still; the About page's credits and links are translated.</summary>
    public class Fixes1100PagesTests
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

        private static IEnumerable<T> Find<T>(object node) where T : class
        {
            if (node is T hit) yield return hit;
            if (node is DependencyObject d)
                foreach (var child in LogicalTreeHelper.GetChildren(d))
                    foreach (var found in Find<T>(child))
                        yield return found;
        }

        private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

        // ---- The Log page ------------------------------------------------------------------------------------

        private sealed class LogHooks : NoHooks
        {
            public List<string> Lines = new List<string>();
            public override IEnumerable<LogEntry> ReadLog() => Lines.Select(LogEntry.Parse).ToList();
        }

        private sealed class QuietLog : IHostLog
        {
            public readonly List<string> Lines = new List<string>();
            public void Info(string message) => Lines.Add(message);
            public void Warn(string message) => Lines.Add(message);
            public void Error(string message) => Lines.Add(message);
        }

        private static FrameworkElement LogPageOf(LogHooks hooks, WindowSpec spec = null)
        {
            var ctx = new PageContext();
            typeof(PageContext).GetProperty("Spec").SetValue(ctx, spec ?? new WindowSpec());
            typeof(PageContext).GetProperty("Hooks").SetValue(ctx, hooks);
            typeof(PageContext).GetProperty("Log").SetValue(ctx, new QuietLog());
            return (FrameworkElement)LogPage.Build(ctx);
        }

        [Fact]
        public void The_folder_button_opens_the_log_folder_beside_Streamer_bot_wherever_the_process_stands() => Sta(() =>
        {
            string folder = "uiframework-1100-logs-" + Guid.NewGuid().ToString("N");
            string full = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, folder);
            string elsewhere = Path.Combine(Path.GetTempPath(), "uiframework-1100-cwd-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(elsewhere);
            var opened = new List<string>();
            var show = Web.ShowFolder;
            string current = Environment.CurrentDirectory;
            try
            {
                Web.ShowFolder = opened.Add;
                // A file dialog (the window's own Export or Import) left the process in another folder.
                Environment.CurrentDirectory = elsewhere;
                var spec = new WindowSpec();
                spec.Log.Folder = folder;
                var page = LogPageOf(new LogHooks(), spec);
                Click(Find<Button>(page).Single(b => AutomationProperties.GetName(b) == Text.T("log.openFolder")));
                Assert.Equal(new[] { full }, opened);
                Assert.True(Directory.Exists(full));
                Assert.False(Directory.Exists(Path.Combine(elsewhere, folder)));   // no empty folder where the dialog was
            }
            finally
            {
                Web.ShowFolder = show;
                Environment.CurrentDirectory = current;
                if (Directory.Exists(full)) Directory.Delete(full, true);
                Directory.Delete(elsewhere, true);
            }
        });

        [Fact]
        public void Lines_of_the_same_second_stand_newest_first_like_all_the_others() => Sta(() =>
        {
            // As the settings action hands them over: oldest first, a round's end three lines in one second.
            var hooks = new LogHooks();
            hooks.Lines.Add("[2026-11-14 20:04:58] START | User: alice | Platform: twitch");
            hooks.Lines.Add("[2026-11-14 20:05:10] ROUND-END | User: alice | Platform: twitch");
            hooks.Lines.Add("[2026-11-14 20:05:10] WIN | User: alice | Platform: twitch");
            hooks.Lines.Add("[2026-11-14 20:05:10] AWARD | User: alice | Platform: twitch | Change: +300");
            var page = LogPageOf(hooks);
            var grid = Find<DataGrid>(page).Single();
            string[] Results() => grid.Items.OfType<LogEntry>().Select(e => e.Result).ToArray();
            Assert.Equal(new[] { "AWARD", "WIN", "ROUND-END", "START" }, Results());

            // The reload sorts them the same way.
            hooks.Lines.Add("[2026-11-14 20:05:10] STATS | User: alice | Platform: twitch");
            Click(Find<Button>(page).Single(b => AutomationProperties.GetName(b) == Text.T("log.reload")));
            Assert.Equal(new[] { "STATS", "AWARD", "WIN", "ROUND-END", "START" }, Results());
        });

        [Fact]
        public void A_line_without_a_readable_time_has_an_empty_time_cell() => Sta(() =>
        {
            var hooks = new LogHooks();
            hooks.Lines.Add("[2026-11-14 20:05:10] WIN | User: alice | Platform: twitch");
            hooks.Lines.Add("[not a time] WIN | User: bob");
            hooks.Lines.Add("hello there");
            var page = LogPageOf(hooks);
            var grid = Find<DataGrid>(page).Single();
            var time = (DataGridTextColumn)grid.Columns[0];
            string Shown(LogEntry entry)
            {
                var cell = new TextBlock { DataContext = entry };
                cell.SetBinding(TextBlock.TextProperty, time.Binding);
                return cell.Text;
            }
            var entries = grid.Items.OfType<LogEntry>().ToList();
            Assert.Equal("2026-11-14 20:05:10", Shown(entries[0]));
            Assert.Equal("", Shown(entries[1]));                  // was 0001-01-01 00:00:00
            Assert.Equal("", Shown(entries[2]));
            Assert.Equal("Timestamp", time.SortMemberPath);       // a click on the header still sorts by the time
        });

        // ---- The Help page's Fire button ---------------------------------------------------------------------

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

        private sealed class SlowGame : NoHooks
        {
            public string Answer;
            public int Thread;
            public override string Test(string name, JObject settings)
            {
                Thread = System.Threading.Thread.CurrentThread.ManagedThreadId;
                System.Threading.Thread.Sleep(Patient.QuickMs + 1500);
                return Answer;
            }
        }

        [Fact]
        public void The_Fire_button_leaves_the_window_free_and_says_why_the_game_did_not_answer() => Sta(() =>
        {
            const string why = "the action 'Demo Game' did not answer within five seconds.";
            var hooks = new SlowGame { Answer = Patient.NoAnswer + ": " + why };
            var said = new List<(string Text, string Why)>();
            var parts = new WindowParts();
            var ctx = new PageContext();
            typeof(PageContext).GetProperty("Spec").SetValue(ctx, SpecJson.Parse(new UiWindow("Demo Game").Trigger("Demo: Round Won", "demo_onWin").ToJson()));
            typeof(PageContext).GetProperty("Parts").SetValue(ctx, parts);
            typeof(PageContext).GetProperty("Settings").SetValue(ctx, new JObject());
            typeof(PageContext).GetProperty("Hooks").SetValue(ctx, hooks);
            typeof(PageContext).GetProperty("SetStatus").SetValue(ctx, (Action<string, System.Windows.Media.Color, string>)((text, colour, reason) => said.Add((text, reason))));
            typeof(PageContext).GetProperty("Fields").SetValue(ctx, new Fields(parts, new JObject(), new JObject(), url => { }));
            var fire = Find<Button>(StandardPages.BuildHelp(ctx)).Single(b => b.Tag as string == StandardPages.FireButtonTag);

            var watch = System.Diagnostics.Stopwatch.StartNew();
            Click(fire);
            // Back long before the game answered: the window's thread is free, the button waits greyed.
            Assert.True(watch.ElapsedMilliseconds < Patient.QuickMs + 1000, "the click held the window for " + watch.ElapsedMilliseconds + " ms");
            Assert.NotEqual(Thread.CurrentThread.ManagedThreadId, hooks.Thread);
            Assert.False(fire.IsEnabled);
            Assert.Equal(Text.T("action.waiting", "Demo: Round Won"), said.Last().Text);

            Pump(2500);
            Assert.True(fire.IsEnabled);
            Assert.Equal(Text.T("help.trigger.notFired", "Demo: Round Won"), said.Last().Text);
            Assert.Equal(why, said.Last().Why);                   // the settings action's reason, not a guess

            // An answer that is no answer at all (an older host block) keeps the hint at an older version.
            hooks.Answer = null;
            Click(fire);
            Pump(2500);
            Assert.Equal(Text.T("help.trigger.notFired.why"), said.Last().Why);
        });

        // ---- Emote pictures ----------------------------------------------------------------------------------

        private sealed class SlowSources : NoHooks
        {
            public string Url;
            public int Asked, Thread;
            public override string EmoteSources()
            {
                Asked++;
                Thread = System.Threading.Thread.CurrentThread.ManagedThreadId;
                System.Threading.Thread.Sleep(Patient.QuickMs + 1500);
                return "{ \"youtube\": { \":own-1100:\": \"" + Url + "\" } }";
            }
        }

        [Fact]
        public void An_emote_field_does_not_hold_the_window_while_the_game_names_its_emote_sources() => Sta(() =>
        {
            var hooks = new SlowSources { Url = "https://example.invalid/own-" + Guid.NewGuid().ToString("N") };
            // The picture is there already, so nothing is downloaded: what matters is that it is shown once the game answered.
            var cache = (System.Collections.Concurrent.ConcurrentDictionary<string, System.Threading.Tasks.Task<System.Windows.Media.ImageSource>>)typeof(EmotePictures)
                .GetField("Pictures", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).GetValue(null);
            var picture = System.Windows.Media.Imaging.BitmapSource.Create(1, 1, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null, new byte[4], 4);
            picture.Freeze();
            string url = hooks.Url + "=w48-h48-c-k-nd";
            cache[url] = System.Threading.Tasks.Task.FromResult<System.Windows.Media.ImageSource>(picture);
            try
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                var image = EmotePictures.Picture("youtube", ":own-1100:", hooks);
                var second = EmotePictures.Picture("youtube", ":own-1100:", hooks);
                Assert.True(watch.ElapsedMilliseconds < Patient.QuickMs + 1000, "the emote cells held the window for " + watch.ElapsedMilliseconds + " ms");
                Assert.Null(image.Source);

                Pump(2500);
                Assert.Same(picture, image.Source);
                Assert.Same(picture, second.Source);
                Assert.Equal(1, hooks.Asked);                     // once per window, as before
                Assert.NotEqual(Thread.CurrentThread.ManagedThreadId, hooks.Thread);
            }
            finally { cache.TryRemove(url, out _); }
        });

        // ---- The About page ----------------------------------------------------------------------------------

        [Fact]
        public void The_About_pages_credits_and_the_games_own_links_are_translated_like_its_sections() => Sta(() =>
        {
            var schema = Schema.Parse(@"{ ""host"": ""demo"", ""version"": ""v1"", ""settings"": [ { ""key"": ""a"", ""default"": 1 } ],
                ""window"": { ""title"": ""Demo"", ""language"": ""de"",
                    ""about"": { ""credits"": ""Responsible: aaskjer"", ""links"": [ { ""text"": ""Repository"", ""url"": ""https://example.com/repo"" } ],
                        ""sections"": [ { ""heading"": ""Thanks"", ""lines"": [ ""Made with care."" ] } ] } },
                ""lang"": { ""de"": { ""texts"": { ""Responsible: aaskjer"": ""Verantwortlich: aaskjer"", ""Repository"": ""Quellcode"", ""Thanks"": ""Danke"" } } } }");
            try
            {
                var spec = Entry.SpecFor(schema, null);
                Entry.Prepare(spec, schema);
                var parts = new WindowParts();
                var ctx = Entry.ContextFor(spec, (JObject)spec.Defaults.DeepClone(), parts, null, null);
                var blocks = Find<TextBlock>(StandardPages.BuildAbout(ctx)).ToList();
                Assert.Contains(blocks, b => b.Text == "Danke");                    // a section's heading, as before
                Assert.Contains(blocks, b => b.Text == "Verantwortlich: aaskjer");
                Assert.DoesNotContain(blocks, b => b.Text == "Responsible: aaskjer");
                var link = Assert.Single(blocks, b => b.ToolTip as string == "https://example.com/repo");
                Assert.Equal("Quellcode", AutomationProperties.GetName(link));
            }
            finally { Text.Use(Conventions.Default.Lang); }
        });
    }

    /// <summary>2.0.0, the update notice: a text with broken braces does not cost the notice; UIFramework is named only
    /// when the settings of a game would offer it; a game that fixes its window's language gets its notice in it.</summary>
    public class Fixes1100NoticeTests : IDisposable
    {
        private readonly string _folder;
        private DateTime _now = new DateTime(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc);
        private string _minHostStub = "0.1.0";
        private string _session = "run-0";
        private int _runs;

        public Fixes1100NoticeTests()
        {
            _folder = Path.Combine(Path.GetTempPath(), "UIFramework.Tests", "notices-1100-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(_folder, "lib", "1.2.0"));
            File.WriteAllText(Path.Combine(_folder, "lib", "1.2.0", "UIFramework.dll"), "");
            UpdateNotices.Folder = _folder;
            UpdateNotices.InBackground = false;
            UpdateNotices.Delay = TimeSpan.FromSeconds(20);
            UpdateNotices.Now = () => _now;
            UpdateNotices.Session = () => _session;
            UpdateNotices.Fetch = url =>
            {
                if (url == UpdateNotices.FrameworkLatestUrl) return "{ \"version\": \"1.3.0\", \"minHostStub\": \"" + _minHostStub + "\" }";
                if (url.Contains("aaskjer/Hangman-Game")) return "[ { \"tag_name\": \"v13.2.0\" } ]";
                if (url.Contains("aaskjer/Trivia-Game")) return "[ { \"tag_name\": \"v9.1.0\" } ]";
                throw new InvalidOperationException("offline");
            };
        }

        public void Dispose()
        {
            UpdateNotices.Folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UIFramework");
            UpdateNotices.InBackground = true;
            UpdateNotices.Delay = TimeSpan.FromSeconds(20);
            UpdateNotices.Now = () => DateTime.UtcNow;
            UpdateNotices.Session = () => "tests";
            try { Directory.Delete(_folder, true); } catch (Exception) { }
        }

        private string Settings(string game, string layout = null)
        {
            string dir = Path.Combine(_folder, game);
            Directory.CreateDirectory(dir);
            if (layout != null) File.WriteAllText(Path.Combine(dir, LayoutFile.FileName), layout);
            return Path.Combine(dir, "settings.json");
        }

        private string Due(string game, string title, string version, string repo) => UpdateNotices.DueJson(game, title, version, repo, Settings(game), null);
        private string Hangman() => Due("hangman", "Hangman Game", "v13.1.1", "aaskjer/Hangman-Game");
        private string Trivia() => Due("trivia", "Trivia Game", "v9.1.0", "aaskjer/Trivia-Game");

        // Null: stop asking; "": not yet; else the notice's chat lines.
        private static string[] Chat(string answer) => answer == null ? null : answer.Length == 0 ? new string[0] : ((JArray)JObject.Parse(answer)["chat"]).Select(l => (string)l).ToArray();

        [Fact]
        public void A_language_file_with_broken_braces_does_not_cost_the_notice()
        {
            // A language file beside Streamer.bot whose "import it again" lost a brace.
            string lang = Conventions.ExternalFolder;
            bool made = !Directory.Exists(lang);
            string file = Path.Combine(lang, "x1100.json");
            Directory.CreateDirectory(lang);
            File.WriteAllText(file, "{ \"ui\": { \"update.chat.reimport\": \"{0 (import it again\" } }");
            try
            {
                _minHostStub = "0.3.0";
                Settings("trivia", "{ \"language\": \"x1100\" }");
                UpdateNotices.Register("trivia", "Trivia Game", "v9.1.0", "aaskjer/Trivia-Game", Settings("trivia"), "0.2.0");
                Assert.Empty(Chat(Trivia()));
                _now = _now.AddSeconds(30);
                var chat = Chat(Trivia());
                Assert.NotNull(chat);                             // was null: the whole notice of the run was lost
                Assert.Contains("{0 (import it again", Assert.Single(chat));
                Assert.Contains("UIFramework v1.3.0", chat[0]);
            }
            finally
            {
                File.Delete(file);
                if (made) Directory.Delete(lang);
            }
        }

        // One Streamer.bot start with the given games, as many seconds after the first as the notice waits.
        private string[] Run(params Func<string>[] games)
        {
            _session = "run-" + (++_runs);
            foreach (var game in games) Assert.Empty(Chat(game()));
            _now = _now.AddSeconds(30);
            return Chat(games.Last()());
        }

        [Theory]
        [InlineData(null, true)]
        [InlineData("{ \"skipVersion\": \"1.3.0\" }", false)]            // "No" in the update dialog, for every game
        [InlineData("{ \"skipVersion\": \"v1.3\" }", false)]
        [InlineData("{ \"skipVersion\": \"1.2.5\" }", true)]             // another version skipped: this one is offered
        [InlineData("{ \"autoUpdate\": false }", false)]                 // the release check off, for every game
        [InlineData("{ \"autoUpdate\": \"no\", \"skipVersion\": [ \"1.3.0\" ] }", true)]   // what is no switch or version counts as missing
        [InlineData("{ \"skipVersion\": \"1.3.0\"", true)]               // an unreadable loader.json counts as empty
        public void UIFramework_is_named_only_when_the_settings_would_offer_it(string loader, bool named)
        {
            if (loader != null) File.WriteAllText(Path.Combine(_folder, "loader.json"), loader);
            var chat = Run(Hangman);
            Assert.Equal(new[] { "🔔 Updates are out: Hangman Game v13.2.0" + (named ? ", UIFramework v1.3.0" : "") + ". Details are in Streamer.bot's log." }, chat);
        }

        [Fact]
        public void A_games_own_layout_file_declines_it_for_that_game_alone()
        {
            Settings("hangman", "{ \"autoUpdate\": false }");
            Settings("trivia", "{ \"skipVersion\": \"1.3.0\" }");
            // Neither game's settings would offer it: nothing to say, Hangman's own release aside.
            Assert.Equal(new[] { "🔔 Updates are out: Hangman Game v13.2.0. Details are in Streamer.bot's log." }, Run(Trivia, Hangman));
            // A third game of the run would: it is named, whichever game delivers the notice.
            Assert.Contains("UIFramework v1.3.0", Run(() => Due("demo", "Demo Game", "v1.0.0", null), Trivia, Hangman).Single());
            // Nothing declined anywhere: the notice is as it always was.
            Settings("hangman", "{ }");
            Assert.Contains("UIFramework v1.3.0", Run(Hangman).Single());
        }

        [Fact]
        public void The_notice_speaks_the_language_a_game_fixes_for_its_window()
        {
            // The game fixes German for its window; its layout file still names the Spanish of an earlier version.
            Settings("hangman", "{ \"language\": \"es\" }");
            UpdateNotices.Register("hangman", "Hangman Game", "v13.1.1", "aaskjer/Hangman-Game", Settings("hangman"), "0.3.2", null, "de");
            Assert.Equal(new[] { "🔔 Updates verfügbar: Hangman Game v13.2.0, UIFramework v1.3.0. Details stehen im Log von Streamer.bot." }, Run(Hangman));
            // Kept for the starts that follow, where only the game's Init() asks.
            Assert.StartsWith("🔔 Updates verfügbar", Run(Hangman).Single());
            // A window that fixes no language any more: the layout file's again.
            UpdateNotices.Register("hangman", "Hangman Game", "v13.1.1", "aaskjer/Hangman-Game", Settings("hangman"), "0.3.2");
            Assert.StartsWith("🔔 Hay actualizaciones", Run(Hangman).Single());
        }
    }
}
