using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Newtonsoft.Json.Linq;
using Xunit;

namespace UIFramework.Tests
{
    /// <summary>Custom triggers and methods declared once in the window: registered from the declaration, listed on the
    /// Help page, fired with their sample values; and UiLive, which tells whether the streamer is live.</summary>
    public class TriggerTests
    {
        private static UiWindow Demo() => new UiWindow("Demo Game")
            .Trigger("Demo: Round Won", "demo_onWin", "A round ended with a winner.", "roundWinner=alice", "roundPoints=500", "roundBlitz=true", "roundNote")
            .Trigger("Demo: Round Lost", "demo_onLoss")
            .Method("StartRound", "Starts a round.", "demoPlatform=twitch, youtube or kick", "demoStarted=set: whether a round began");

        [Fact]
        public void The_declaration_reaches_the_schema()
        {
            var json = Demo().ToJson();
            Assert.Equal("demo_onWin", (string)json["triggers"][0]["code"]);
            Assert.Equal("roundPoints", (string)json["triggers"][0]["args"][1]["name"]);
            Assert.Equal("500", (string)json["triggers"][0]["args"][1]["text"]);
            Assert.Null(json["triggers"][0]["args"][3]["text"]);
            Assert.Null(json["triggers"][1]["args"]);
            Assert.Equal("StartRound", (string)json["methods"][0]["name"]);
            Assert.Equal("twitch, youtube or kick", (string)json["methods"][0]["args"][0]["text"]);
            Assert.Null(new UiWindow("Plain").ToJson()["triggers"]);
        }

        [Fact]
        public void Triggers_that_share_their_arguments_declare_them_once()
        {
            var json = new UiWindow("Demo")
                .Triggers(new[] { "roundOutcome=WIN", "roundPoints=500" },
                    UiSchema.Tr("Demo: Won", "demo_onWin", "Somebody won."),
                    UiSchema.Tr("Demo: Timeout", "demo_onTimeout", null, "roundOutcome=TIMEOUT", "roundLate=true"))
                .ToJson();
            var won = (JArray)json["triggers"][0]["args"];
            var late = (JArray)json["triggers"][1]["args"];
            Assert.Equal(new[] { "roundOutcome=WIN", "roundPoints=500" }, won.Select(a => (string)a["name"] + "=" + (string)a["text"]));
            // Its own outcome in the shared one's place, its own extra after them.
            Assert.Equal(new[] { "roundOutcome=TIMEOUT", "roundPoints=500", "roundLate=true" }, late.Select(a => (string)a["name"] + "=" + (string)a["text"]));
            Assert.Null(json["triggers"][1]["what"]);
        }

        [Fact]
        public void Register_registers_every_declared_trigger()
        {
            var cph = new FakeCph();
            UiTriggers.RegisterFrom(Demo(), cph, "Chat Game", "Demo Game");
            Assert.Equal(new[] { "Demo: Round Won|demo_onWin|Chat Game/Demo Game", "Demo: Round Lost|demo_onLoss|Chat Game/Demo Game" }, cph.Registered);

            UiTriggers.RegisterFrom(null, cph, "Chat Game");      // no window: nothing, and no exception
            Assert.Equal(2, cph.Registered.Count);
        }

        [Fact]
        public void Fire_sets_the_samples_as_typed_arguments_and_fires()
        {
            var cph = new FakeCph();
            Assert.Equal("fired", UiTriggers.FireFrom(Demo(), cph, "demo_onWin"));
            Assert.Equal(new[] { "demo_onWin" }, cph.Fired);
            Assert.Equal("alice", cph.Arguments["roundWinner"]);
            Assert.Equal(500L, cph.Arguments["roundPoints"]);
            Assert.Equal(true, cph.Arguments["roundBlitz"]);
            Assert.Equal("", cph.Arguments["roundNote"]);

            Assert.Equal("unknown", UiTriggers.FireFrom(Demo(), cph, "demo_onNothing"));
            Assert.Single(cph.Fired);
        }

        [Fact]
        public void The_window_s_Fire_button_goes_through_the_block_not_the_game()
        {
            // The bare template declares no trigger, so the block answers "unknown" itself; the game is never asked.
            var cph = new FakeCph();
            bool asked = false;
            var args = new Dictionary<string, object>
            {
                ["uiframework.request"] = "test", ["uiframework.name"] = UiTriggers.FireRequest + "demo_onLoss", ["uiframework.values"] = "{}", ["uiframework.var"] = "answer"
            };
            Assert.True(UiHost.Handle(args, cph, "Demo Game", "demo", "v1.0.0", "x.json", test: (name, values) => { asked = true; return "the game's"; }));
            Assert.False(asked);
            Assert.Equal("unknown", cph.Globals["answer"]);
            Assert.Empty(cph.Fired);
            // The block's request name is the one the window sends.
            Assert.Equal(StandardPages.FireRequest, UiTriggers.FireRequest);
        }

        [Fact]
        public void The_spec_reads_them_and_leaves_out_what_cannot_be_shown_or_fired()
        {
            var root = Demo().ToJson();
            ((JArray)root["triggers"]).Add(new JObject { ["name"] = "No code" });
            ((JArray)root["triggers"]).Add(new JObject { ["code"] = "no_name" });
            var spec = SpecJson.Parse(root);
            Assert.Equal(new[] { "Demo: Round Won", "Demo: Round Lost" }, spec.Triggers.Select(t => t.Name));
            Assert.Equal(("roundPoints", "500"), spec.Triggers[0].Args[1]);
            Assert.Single(spec.Methods);
            Assert.Equal(2, spec.Methods[0].Args.Count);
        }

        [Fact]
        public void The_Help_page_lists_them_and_its_button_asks_the_game_to_fire() => Sta(() =>
        {
            var spec = SpecJson.Parse(Demo().ToJson());
            var hooks = new Recorder();
            string status = null;
            var parts = new WindowParts();
            var ctx = new PageContext();
            typeof(PageContext).GetProperty("Spec").SetValue(ctx, spec);
            typeof(PageContext).GetProperty("Parts").SetValue(ctx, parts);
            typeof(PageContext).GetProperty("Settings").SetValue(ctx, new JObject());
            typeof(PageContext).GetProperty("Hooks").SetValue(ctx, hooks);
            typeof(PageContext).GetProperty("SetStatus").SetValue(ctx, (Action<string, System.Windows.Media.Color, string>)((text, colour, why) => status = text));
            typeof(PageContext).GetProperty("Fields").SetValue(ctx, new Fields(parts, new JObject(), new JObject(), url => { }));

            // A game with no help of its own still gets the page.
            var page = StandardPages.BuildHelp(ctx);
            Assert.NotNull(page);
            var all = new List<object>();
            void Walk(object node) { all.Add(node); if (node is DependencyObject d) foreach (var child in LogicalTreeHelper.GetChildren(d)) Walk(child); }
            Walk(page);
            string text = string.Join("\n", all.OfType<TextBlock>().Select(b => b.Inlines.Count > 0 ? string.Concat(b.Inlines.OfType<System.Windows.Documents.Run>().Select(r => r.Text)) : b.Text));
            Assert.Contains("Custom triggers", text);
            Assert.Contains("Demo: Round Won", text);
            Assert.Contains("roundWinner (alice), roundPoints (500), roundBlitz (true), roundNote", text);
            Assert.Contains("Methods for your own actions", text);
            Assert.Contains("StartRound", text);
            Assert.Contains("demoPlatform", text);

            var buttons = all.OfType<Button>().Where(b => b.Tag as string == StandardPages.FireButtonTag).ToList();
            Assert.Equal(2, buttons.Count);
            buttons[1].RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            Assert.Equal(new[] { "uiframework.trigger:demo_onLoss" }, hooks.Asked);
            Assert.Contains("fired", status);

            // A game that does not answer (an older host block) is said so, not passed off as fired.
            hooks.Answer = null;
            buttons[0].RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            Assert.Contains("was not fired", status);
        });

        [Fact]
        public void A_window_with_neither_help_nor_declarations_has_no_Help_page() => Sta(() =>
        {
            var ctx = new PageContext();
            typeof(PageContext).GetProperty("Spec").SetValue(ctx, SpecJson.Parse(new UiWindow("Plain").ToJson()));
            Assert.Null(StandardPages.BuildHelp(ctx));
        });

        // ---- UiLive ----

        [Theory]
        [InlineData(@"{""data"":[{""id"":""1"",""type"":""live""}]}", "Live")]
        [InlineData(@"{""data"":[]}", "Offline")]
        [InlineData(@"{""error"":""Unauthorized"",""status"":401}", "Unknown")]
        [InlineData("<html>", "Unknown")]
        [InlineData(null, "Unknown")]
        public void Twitch_is_live_while_Helix_lists_a_stream(string body, string expected) => Assert.Equal(expected, Live.TwitchFromApi(body));

        [Theory]
        [InlineData(@"{""slug"":""x"",""livestream"":{""id"":5,""is_live"":true}}", "Live")]
        [InlineData(@"{""slug"":""x"",""livestream"":null}", "Offline")]
        [InlineData(@"{""slug"":""x"",""livestream"":{""id"":5,""is_live"":false}}", "Offline")]
        [InlineData(@"{""message"":""Not found""}", "Unknown")]
        [InlineData(null, "Unknown")]
        public void Kick_is_live_while_the_channel_has_a_livestream(string body, string expected) => Assert.Equal(expected, Live.KickFromApi(body));

        [Fact]
        public void The_DLL_asks_Twitch_with_the_bare_token_and_Kick_by_its_slug()
        {
            var fetch = Live.Fetch;
            var asked = new List<string>();
            try
            {
                Live.Fetch = (url, clientId, token) => { asked.Add(url + "|" + clientId + "|" + token); return url.Contains("my-channel") ? @"{""livestream"":{""is_live"":true}}" : url.Contains("helix") ? @"{""data"":[]}" : null; };
                Assert.Equal("Offline", Live.Twitch("42", "client", "oauth:secret"));
                Assert.Equal("https://api.twitch.tv/helix/streams?user_id=42|client|secret", asked[0]);
                // The login as it is finds nothing; with the underscore as a hyphen it is the channel.
                Assert.Equal("Live", Live.Kick("My_Channel"));
                Assert.Equal(new[] { "https://kick.com/api/v2/channels/my_channel||", "https://kick.com/api/v2/channels/my-channel||" }, asked.Skip(1));
                // Nothing to ask with: no request at all.
                Assert.Equal("Unknown", Live.Twitch("42", null, "t"));
                Assert.Equal("Unknown", Live.Kick(" "));
                Assert.Equal(3, asked.Count);
                // A fetch that throws is an open question, not an exception.
                Live.Fetch = (url, clientId, token) => throw new InvalidOperationException("down");
                Assert.Equal("Unknown", Live.Twitch("42", "client", "t"));
            }
            finally { Live.Fetch = fetch; }
        }

        [Fact]
        public void YouTube_goes_by_the_broadcasts_Streamer_bot_monitors()
        {
            Assert.Equal(UiLiveState.Live, UiLive.YouTubeFromStatuses(new[] { "complete", "live" }));
            Assert.Equal(UiLiveState.Offline, UiLive.YouTubeFromStatuses(new[] { "complete", "upcoming" }));
            Assert.Equal(UiLiveState.Offline, UiLive.YouTubeFromStatuses(new string[0]));
            Assert.Equal(UiLiveState.Unknown, UiLive.YouTubeFromStatuses(new[] { "complete", "somethingNew" }));
            Assert.Equal(UiLiveState.Unknown, UiLive.YouTubeFromStatuses(null));
        }

        [Fact]
        public void Of_reads_Streamer_bot_s_models_by_property_and_never_throws()
        {
            var cph = new FakeCph { Broadcasts = new List<object> { new { Status = "complete" }, new { Status = "live" } } };
            Assert.Equal(UiLiveState.Live, UiLive.Of(cph, "youtube"));
            // No Twitch account, no client id: the question stays open, without a request and without an exception.
            Assert.Equal(UiLiveState.Unknown, UiLive.Of(cph, "twitch"));
            Assert.Equal(UiLiveState.Unknown, UiLive.Of(cph, "kick"));
        }

        [Fact]
        public void Only_being_offline_or_a_quiet_unknown_keeps_something_automatic_away()
        {
            Assert.Null(UiLive.SkipReason(UiLiveState.Live, recentChat: false));
            Assert.Null(UiLive.SkipReason(UiLiveState.Unknown, recentChat: true));
            Assert.Contains("not live", UiLive.SkipReason(UiLiveState.Offline, recentChat: true));
            Assert.Contains("could not be told", UiLive.SkipReason(UiLiveState.Unknown, recentChat: false));
        }

        private sealed class Recorder : NoHooks
        {
            public readonly List<string> Asked = new List<string>();
            public string Answer = "fired";
            public override string Test(string name, JObject settings) { Asked.Add(name); return Answer; }
        }

        private static void Sta(Action body)
        {
            Exception failure = null;
            var thread = new System.Threading.Thread(() => { try { body(); } catch (Exception ex) { failure = ex; } });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
