using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Newtonsoft.Json.Linq;
using Xunit;

namespace UIFramework.Tests
{
    /// <summary>A window watches actions of Streamer.bot (window.actions): a condition asks whether one is there and
    /// enabled, a switch follows one or sets it, and all of it follows what the user does in Streamer.bot while the
    /// window is open. The ids here are invented.</summary>
    public class ActionWatchTests
    {
        private const string Poster = "3f6c1a52-8b0e-4d47-9a61-c2e5d7f04b18";
        private const string Other = "a94d2e70-15c3-4f8b-b6d2-07e3c9815f6a";

        private static ActionWatch Watch(Bot host) => new ActionWatch(new[]
        {
            new WatchedAction { Name = "poster", Id = Poster, Title = "My Poster" },
            new WatchedAction { Name = "other", Id = Other }
        }, host);

        // ---- the watch -----------------------------------------------------------------------------------------

        [Fact]
        public void TheWatchTakesWhatTheHostTells()
        {
            var host = new Bot();
            var watch = Watch(host);
            Assert.True(watch.Any);
            Assert.False(watch.Known);
            Assert.Equal("on", watch.State("poster"));                              // not told yet: nothing is greyed out
            Assert.Equal("My Poster", watch.Title("poster"));
            Assert.Equal("other", watch.Title("other"));

            host.States[Poster] = Tuple.Create("off", "Poster, renamed");
            host.States[Other] = Tuple.Create("missing", (string)null);
            Assert.True(watch.Take(watch.Ask()));
            Assert.True(watch.Known);
            Assert.Equal("off", watch.State("poster"));
            Assert.Equal("Poster, renamed", watch.Title("poster"));                 // as Streamer.bot calls it now
            Assert.Equal("missing", watch.State("other"));
            Assert.Equal("other", watch.Title("other"));
            Assert.False(watch.Take(watch.Ask()));                                  // nothing new
            Assert.Equal(Poster + "," + Other, host.Asked.Last());

            host.States[Poster] = Tuple.Create("on", "Poster, renamed");
            Assert.True(watch.Take(watch.Ask()));
            Assert.Equal("on", watch.State("poster"));
            Assert.Equal("on", watch.State("nobody"));                              // a name the window does not declare

            // A host that cannot tell: nothing is taken, and what was known stays.
            host.Silent = true;
            Assert.Null(watch.Ask());
            Assert.False(watch.Take(null));
            Assert.Equal("missing", watch.State("other"));
            Assert.False(new ActionWatch(null, null).Any);
        }

        [Fact]
        public void SettingAnActionHandsItToTheHost()
        {
            var host = new Bot();
            host.States[Poster] = Tuple.Create("on", "Poster");
            host.States[Other] = Tuple.Create("missing", (string)null);
            var watch = Watch(host);
            watch.Take(watch.Ask());

            Assert.True(watch.Set("poster", false));
            Assert.Equal(new[] { Poster + " False" }, host.Set.ToArray());
            Assert.Equal("off", watch.State("poster"));                             // held for set until the host tells
            Assert.Equal("Poster", watch.Title("poster"));
            Assert.False(watch.Set("other", true));                                 // missing: nothing to enable
            Assert.False(watch.Set("nobody", true));
            host.CanSet = false;
            Assert.False(watch.Set("poster", true));
            Assert.Equal("off", watch.State("poster"));
            Assert.Single(host.Set);
        }

        // ---- a condition on an action ---------------------------------------------------------------------------

        [Fact]
        public void AConditionAsksAboutAnAction()
        {
            var host = new Bot();
            host.States[Poster] = Tuple.Create("on", (string)null);
            var watch = Watch(host);
            watch.Take(watch.Ask());
            var parts = new WindowParts { Actions = watch };
            bool Holds(string condition) => Requirement.Parse(condition).All(c => Requirement.Holds(null, parts, c));

            Assert.True(Holds("action:poster"));
            Assert.False(Holds("!action:poster"));
            Assert.True(Holds("action:poster=on"));
            Assert.False(Holds("action:poster=off|missing"));
            Assert.True(Holds("action:poster!=off"));

            host.States[Poster] = Tuple.Create("off", (string)null);
            watch.Take(watch.Ask());
            Assert.False(Holds("action:poster"));
            Assert.True(Holds("!action:poster"));
            Assert.True(Holds("action:poster=off"));
            Assert.False(Holds("action:poster=missing"));
            Assert.True(Holds("action:poster=OFF|missing"));
            Assert.False(Holds("action:poster!=off"));

            host.States[Poster] = Tuple.Create("missing", (string)null);
            watch.Take(watch.Ask());
            Assert.True(Holds("action:poster=missing"));
            Assert.True(Holds("!action:poster"));
            Assert.Equal("missing", (string)Requirement.Live(null, parts, "action:poster"));

            // What nobody watches, and a page built without a watch, greys nothing out.
            Assert.True(Holds("action:nobody"));
            Assert.True(Requirement.Parse("action:poster").All(c => Requirement.Holds(null, new WindowParts(), c)));

            int heard = 0;
            Requirement.Listen(parts, "action:poster", () => heard++);
            parts.RaiseActions();
            Assert.Equal(1, heard);
            Assert.Equal("poster", Requirement.ActionOf(" Action: poster "));
            Assert.Null(Requirement.ActionOf("poster"));
        }

        // ---- the declaration ------------------------------------------------------------------------------------

        private const string GoodSettings = @"
            { ""key"": ""enabled"", ""default"": true, ""page"": ""game"", ""label"": ""Post"", ""followsAction"": ""poster"" },
            { ""key"": ""posterOn"", ""default"": false, ""page"": ""game"", ""label"": ""Poster runs"", ""setsAction"": ""poster"" },
            { ""key"": ""text"", ""default"": ""hi"", ""page"": ""game"", ""label"": ""Text"", ""requires"": ""action:poster"" },
            { ""key"": ""why"", ""kind"": ""note"", ""page"": ""game"", ""help"": ""The poster is off."", ""showWhen"": ""action:poster=off"" }";
        private const string GoodActions = @"{ ""name"": ""poster"", ""id"": """ + Poster + @""", ""title"": ""My Poster"" }";

        private static string SchemaJson(string settings, string actions, string more = "") =>
            @"{ ""host"": ""demo"", ""version"": ""v1"", ""settingsFile"": ""x"", ""settings"": [ " + settings + @" ],
                ""window"": { ""title"": ""Demo"", ""pages"": [ ""game"", { ""id"": ""probe"", ""title"": ""Probe"", ""custom"": true } ], ""actions"": [ " + actions + " ]" + more + " } }";

        private static WindowSpec Spec(string settings, string actions, string more = "")
        {
            var schema = Schema.Parse(SchemaJson(settings, actions, more));
            var spec = Entry.SpecFor(schema, Path.Combine(Path.GetTempPath(), "UIFramework.Tests", "actions-spec", "settings.json"));
            Entry.Prepare(spec, schema);
            return spec;
        }

        [Fact]
        public void TheSchemaDeclaresTheActionsAndWhatIsBoundToThem()
        {
            var spec = Spec(GoodSettings, GoodActions + @", { ""name"": ""other"", ""id"": """ + Other + @""" }",
                @", ""headerPills"": [ { ""states"": [ { ""when"": ""action:poster=off"", ""text"": ""Poster off"", ""tone"": ""warn"" }, { ""text"": ""Poster"" } ] } ]");
            Assert.Empty(spec.Problems);
            Assert.Equal(new[] { "poster", "other" }, spec.Actions.Select(a => a.Name).ToArray());
            Assert.Equal(Poster, spec.Actions[0].Id);
            Assert.Equal("My Poster", spec.Actions[0].Title);
            Assert.Equal("", spec.Actions[1].Title);
            Assert.Equal("poster", spec.ResolvedSettings.First(s => s.Key == "enabled").FollowsAction);
            Assert.Equal("poster", spec.ResolvedSettings.First(s => s.Key == "posterOn").SetsAction);
            // And back out, as the customise mode writes the schema.
            var again = Schema.Parse(SchemaJson(GoodSettings, GoodActions)).ToJson();
            Assert.Equal("poster", (string)((JArray)again["settings"]).First(s => (string)s["key"] == "enabled")["followsAction"]);
            Assert.Equal("poster", (string)((JArray)again["settings"]).First(s => (string)s["key"] == "posterOn")["setsAction"]);
            Assert.Null(((JArray)again["settings"]).First(s => (string)s["key"] == "text")["followsAction"]);
        }

        [Fact]
        public void ADeclarationThatCannotWorkIsAProblem()
        {
            var conditions = Spec(@"
                { ""key"": ""a"", ""default"": """", ""page"": ""game"", ""label"": ""A"", ""requires"": ""action:ghost"" },
                { ""key"": ""b"", ""default"": """", ""page"": ""game"", ""label"": ""B"", ""requires"": ""action:poster>1"" },
                { ""key"": ""c"", ""default"": """", ""page"": ""game"", ""label"": ""C"", ""showWhen"": ""action:poster=sleeping"" },
                { ""key"": ""d"", ""default"": """", ""page"": ""game"", ""label"": ""D"", ""showWhen"": ""action:poster=off|missing"" }", GoodActions).Problems;
            Assert.Contains(conditions, p => p.Contains("'action:ghost'") && p.Contains("watches no action named 'ghost'"));
            Assert.Contains(conditions, p => p.Contains("'action:poster>1'") && p.Contains("an action is on, off or missing"));
            Assert.Contains(conditions, p => p.Contains("'action:poster=sleeping'") && p.Contains("never 'sleeping'"));
            Assert.Equal(3, conditions.Count);

            var bound = Spec(@"
                { ""key"": ""a"", ""default"": """", ""page"": ""game"", ""label"": ""A"", ""followsAction"": ""poster"" },
                { ""key"": ""b"", ""default"": true, ""page"": ""game"", ""label"": ""B"", ""setsAction"": ""ghost"" },
                { ""key"": ""c"", ""default"": true, ""page"": ""game"", ""label"": ""C"", ""followsAction"": ""poster"", ""setsAction"": ""poster"" }", GoodActions).Problems;
            Assert.Contains(bound, p => p.Contains("'a' has followsAction, but only a switch can be bound"));
            Assert.Contains(bound, p => p.Contains("'b' has setsAction 'ghost', but the window watches no action of that name"));
            Assert.Contains(bound, p => p.Contains("'c' has followsAction and setsAction"));
            Assert.Equal(3, bound.Count);

            var actions = Spec(GoodSettings, GoodActions + @", { ""name"": ""poster"", ""id"": """ + Other + @""" }, { ""name"": """", ""id"": """ + Other + @""" }, { ""name"": ""byName"", ""id"": ""My Action"" }").Problems;
            Assert.Contains(actions, p => p.Contains("two actions named 'poster'"));
            Assert.Contains(actions, p => p.Contains("an action without a name"));
            Assert.Contains(actions, p => p.Contains("'byName' by 'My Action', which is no action id"));
            Assert.Equal(3, actions.Count);
        }

        [Fact]
        public void TheHostBlockDeclaresThem()
        {
            var window = new UiWindow("Demo").Actions(UiSchema.Act("poster", Poster, "My Poster"), UiSchema.Act("other", Other), null);
            var json = window.ToJson();
            Assert.Equal(2, ((JArray)json["actions"]).Count);
            Assert.Equal("poster", (string)json["actions"][0]["name"]);
            Assert.Equal(Poster, (string)json["actions"][0]["id"]);
            Assert.Equal("My Poster", (string)json["actions"][0]["title"]);
            Assert.Null(json["actions"][1]["title"]);
            Assert.Null(new UiWindow("Demo").ToJson()["actions"]);                  // nothing watched, nothing in the schema

            Assert.Equal("poster", (string)UiSchema.ToJson(UiSchema.S("enabled", true, followsAction: "poster"))["followsAction"]);
            Assert.Equal("poster", (string)UiSchema.ToJson(UiSchema.S("posterOn", false, setsAction: "poster"))["setsAction"]);
            var plain = UiSchema.ToJson(UiSchema.S("plain", true));
            Assert.Null(plain["followsAction"]);
            Assert.Null(plain["setsAction"]);
        }

        // ---- the settings action --------------------------------------------------------------------------------

        [Fact]
        public void TheSettingsActionTellsAboutTheActionsAndSetsOne()
        {
            var stub = new StubUnderTest();
            // On Streamer.bot's own thread, as the run that serves the window is.
            typeof(StubUnderTest).GetField("_botThread", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(stub, Thread.CurrentThread.ManagedThreadId);
            stub.CPH.Actions.Add(new Streamer.bot.Plugin.Interface.Model.ActionData { Id = Guid.Parse(Poster), Name = "Poster", Enabled = true });
            stub.CPH.Actions.Add(new Streamer.bot.Plugin.Interface.Model.ActionData { Id = Guid.Parse(Other), Name = "Other", Enabled = false });
            var host = new StubUnderTest.Host(stub);

            const string gone = "11111111-2222-4333-8444-555555555555";
            var told = JObject.Parse(host.ActionStates(Poster.ToUpperInvariant() + ", " + Other + "," + gone + ", not-an-id"));
            Assert.Equal("on", (string)told[Poster.ToUpperInvariant()]["state"]);
            Assert.Equal("Poster", (string)told[Poster.ToUpperInvariant()]["name"]);
            Assert.Equal("off", (string)told[Other]["state"]);
            Assert.Equal("missing", (string)told[gone]["state"]);
            Assert.Null(told[gone]["name"]);
            Assert.Equal("missing", (string)told["not-an-id"]["state"]);
            Assert.Equal("{}", host.ActionStates(""));

            host.SetAction(Poster, false);
            host.SetAction(Other, true);
            Assert.False(stub.CPH.Actions[0].Enabled);
            Assert.True(stub.CPH.Actions[1].Enabled);

            // The window reaches both through the DLL's reflection host, and takes the answer as it is.
            var reflected = new ReflectionHost(host);
            var watch = new ActionWatch(new[] { new WatchedAction { Name = "poster", Id = Poster }, new WatchedAction { Name = "gone", Id = gone } }, reflected);
            Assert.True(watch.Take(watch.Ask()));
            Assert.Equal("off", watch.State("poster"));
            Assert.Equal("Poster", watch.Title("poster"));
            Assert.Equal("missing", watch.State("gone"));
            Assert.True(watch.Set("poster", true));
            Assert.True(stub.CPH.Actions[0].Enabled);
        }

        // ---- the window -----------------------------------------------------------------------------------------

        [Fact]
        public void TheWindowFollowsAnActionWhileItIsOpen()
        {
            var before = ActionWatch.Interval;
            ActionWatch.Interval = TimeSpan.FromMilliseconds(40);
            try
            {
                using (var open = new Opened(SchemaJson(GoodSettings, GoodActions,
                    @", ""headerPills"": [ { ""states"": [ { ""when"": ""action:poster=off"", ""text"": ""Poster off"", ""tone"": ""warn"" }, { ""when"": ""action:poster=missing"", ""text"": ""Poster missing"" }, { ""text"": ""Poster on"" } ] } ]")))
                {
                    // Enabled when the window opened: nothing greyed, the switch is its own, no line under it.
                    Assert.True(open.On(() => open.Box("text").IsEnabled));
                    Assert.True(open.On(() => open.Switch("enabled").IsVisible && open.Switch("enabled").IsChecked == true));
                    Assert.True(open.On(() => open.Switch("posterOn").IsChecked == true));
                    Assert.False(open.Shows("The poster is off."));
                    Assert.True(open.Shows("Poster on"));
                    Assert.Empty(open.ActionLines());

                    // The user disables the action in Streamer.bot: within a moment the window shows it.
                    open.State("off", "Poster, renamed");
                    open.Until(() => !open.Box("text").IsEnabled);
                    Assert.False(open.On(() => open.Box("text").IsEnabled));                       // greyed out, still there
                    Assert.True(open.On(() => open.Box("text").IsVisible));
                    Assert.True(open.Shows("The poster is off."));
                    Assert.True(open.Shows("Poster off"));
                    Assert.False(open.On(() => open.Switch("enabled").IsVisible));                 // its stand-in shows off
                    Assert.True(open.On(() => open.Switch("enabled").IsChecked == true));          // and its own value is kept
                    Assert.Contains("The action 'Poster, renamed' is disabled in Streamer.bot.", open.ActionLines());
                    Assert.True(open.On(() => open.Switch("posterOn").IsChecked == false));
                    Assert.Equal("No unsaved changes", open.On(() => open.Status().Text));         // none of it is a change

                    // Deleted, then back and enabled.
                    open.State("missing", null);
                    open.Until(() => open.Shows("Poster missing"));
                    Assert.Contains("The action 'My Poster' is not in this Streamer.bot.", open.ActionLines());
                    Assert.False(open.On(() => open.Switch("posterOn").IsEnabled));
                    open.State("on", "Poster");
                    open.Until(() => open.Box("text").IsEnabled);
                    Assert.True(open.On(() => open.Switch("enabled").IsVisible && open.Switch("enabled").IsChecked == true));
                    Assert.True(open.Shows("Poster on"));
                    Assert.Empty(open.ActionLines());

                    // The switch that sets the action: a click disables it in Streamer.bot at once, and the rest follows.
                    open.On(() => open.Switch("posterOn").IsChecked = false);
                    open.Until(() => !open.Box("text").IsEnabled);
                    Assert.Equal(new[] { Poster + " False" }, open.Set.ToArray());
                    Assert.Equal("No unsaved changes", open.On(() => open.Status().Text));
                    open.On(() => open.Switch("posterOn").IsChecked = true);
                    open.Until(() => open.Box("text").IsEnabled);
                    Assert.Equal(new[] { Poster + " False", Poster + " True" }, open.Set.ToArray());

                    // What is saved is the switch's own value, whatever the action did meanwhile; the one that sets is not saved.
                    open.State("off", "Poster");
                    open.Until(() => !open.Box("text").IsEnabled);
                    open.On(() => open.Box("text").Text = "hello");
                    open.State("on", "Poster");
                    open.Until(() => open.Box("text").IsEnabled);
                    open.Click("Save");
                    open.Until(() => File.Exists(open.SettingsPath));
                    var saved = open.Saved();
                    Assert.True((bool)saved["enabled"]);
                    Assert.Null(saved["posterOn"]);
                }
            }
            finally { ActionWatch.Interval = before; }
        }

        [Fact]
        public void AHostThatCannotTellGreysNothingOut()
        {
            // A settings action from before the watch, or a preview: every action counts as enabled.
            using (var open = new Opened(SchemaJson(GoodSettings, GoodActions), silent: true))
            {
                Assert.True(open.On(() => open.Box("text").IsEnabled));
                Assert.True(open.On(() => open.Switch("enabled").IsVisible));
                Assert.False(open.Shows("The poster is off."));
                Assert.False(open.On(() => open.Switch("posterOn").IsEnabled));                     // nothing to set an action with
                Assert.Empty(open.ActionLines());
            }
        }

        // ---- a Streamer.bot that tells what it is asked ----------------------------------------------------------

        private class Bot : NoHooks
        {
            public readonly Dictionary<string, Tuple<string, string>> States = new Dictionary<string, Tuple<string, string>>(StringComparer.OrdinalIgnoreCase);
            public readonly List<string> Asked = new List<string>();
            public readonly List<string> Set = new List<string>();
            public bool Silent;
            public bool CanSet = true;

            public override string ActionStates(string ids)
            {
                lock (States)
                {
                    Asked.Add(ids);
                    if (Silent) return null;
                    var told = new JObject();
                    foreach (string id in ids.Split(','))
                        told[id] = States.TryGetValue(id, out var state)
                            ? new JObject { ["state"] = state.Item1, ["name"] = state.Item2 }
                            : new JObject { ["state"] = "missing" };
                    return told.ToString(Newtonsoft.Json.Formatting.None);
                }
            }

            public override bool SetAction(string id, bool enabled)
            {
                lock (States)
                {
                    if (!CanSet) return false;
                    Set.Add(id + " " + enabled);
                    States[id] = Tuple.Create(enabled ? "on" : "off", States.TryGetValue(id, out var before) ? before.Item2 : null);
                    return true;
                }
            }
        }

        /// <summary>The window, opened as a host opens it, with a Streamer.bot whose one action the test switches.</summary>
        private sealed class Opened : Bot, IDisposable
        {
            private readonly ManualResetEventSlim _built = new ManualResetEventSlim();
            private readonly Thread _runner;
            private TextBlock _probe;
            private PageContext _context;
            public readonly string Folder, SettingsPath;

            public Opened(string schemaJson, bool silent = false)
            {
                Silent = silent;
                States[Poster] = Tuple.Create("on", "Poster");
                Folder = Path.Combine(Path.GetTempPath(), "UIFramework.Tests", "actions-window-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(Folder);
                SettingsPath = Path.Combine(Folder, "settings.json");
                var schema = UIFramework.Schema.Parse(schemaJson);
                var spec = Entry.SpecFor(schema, SettingsPath);
                _runner = new Thread(() => { try { Entry.Show(spec, schema, null, this); } catch (Exception) { } }) { IsBackground = true };
                _runner.SetApartmentState(ApartmentState.STA);
                _runner.Start();
                if (!_built.Wait(15000))
                    throw new TimeoutException("The window was not built.");
                Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            }

            private Dispatcher Dispatcher => _probe.Dispatcher;
            private Window Window => (Window)Up(_probe, n => n is Window);

            public override FrameworkElement BuildPage(string pageId, PageContext context)
            {
                _context = context;
                _probe = new TextBlock { Text = "probe" };
                _built.Set();
                return _probe;
            }

            public void State(string state, string name)
            {
                lock (States) States[Poster] = Tuple.Create(state, name);
            }

            public T On<T>(Func<T> body) => Dispatcher.Invoke(body);
            public void On(Action body) => Dispatcher.Invoke(body);

            /// <summary>Waits until the window shows it; the watch asks again every few milliseconds here.</summary>
            public void Until(Func<bool> shown)
            {
                var watch = Stopwatch.StartNew();
                while (watch.ElapsedMilliseconds < 10000)
                {
                    if (Dispatcher.Invoke(shown)) return;
                    Thread.Sleep(20);
                }
                throw new TimeoutException("The window did not follow within ten seconds.");
            }

            public JObject Saved() => JObject.Parse(File.ReadAllText(SettingsPath));
            public TextBox Box(string key) => _context.Parts.Boxes[key];
            public CheckBox Switch(string key) => _context.Parts.Switches[key];

            public void Click(string text) => Dispatcher.Invoke(() =>
            {
                var button = All(Window).OfType<Button>().First(b => b.Content as string == text);
                button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, button));
            });

            /// <summary>Whether a text block that can be seen reads so, a note with its runs included.</summary>
            public bool Shows(string text) => Dispatcher.Invoke(() => All(Window).OfType<TextBlock>().Any(t => t.IsVisible
                && (t.Text == text || new System.Windows.Documents.TextRange(t.ContentStart, t.ContentEnd).Text.Trim() == text)));

            /// <summary>The lines under the switches bound to an action that say why they cannot be used.</summary>
            public List<string> ActionLines() => Dispatcher.Invoke(() =>
                All(Window).OfType<TextBlock>().Where(t => (t.Tag as string) == SchemaPage.ActionLineTag && t.IsVisible).Select(t => t.Text).ToList());

            public TextBlock Status() => All(Window).OfType<TextBlock>().First(t => t.TextTrimming == TextTrimming.CharacterEllipsis && t.TextAlignment == TextAlignment.Right);

            private static object Up(DependencyObject node, Func<DependencyObject, bool> wanted)
            {
                while (node != null && !wanted(node))
                    node = LogicalTreeHelper.GetParent(node) ?? (node is System.Windows.Media.Visual ? System.Windows.Media.VisualTreeHelper.GetParent(node) : null);
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

            public void Dispose()
            {
                try
                {
                    Shell.Close(SettingsPath);
                    var watch = Stopwatch.StartNew();
                    while (_runner.IsAlive && watch.ElapsedMilliseconds < 15000)
                        Thread.Sleep(50);
                }
                finally
                {
                    Text.Use(Conventions.Default.Lang);
                    try { Directory.Delete(Folder, true); } catch (Exception) { }
                }
            }
        }
    }
}
