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
    /// <summary>1.10.0, the schema's pages: a field with <c>ask</c> asks with what the window shows, and its answers
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
    }
}
