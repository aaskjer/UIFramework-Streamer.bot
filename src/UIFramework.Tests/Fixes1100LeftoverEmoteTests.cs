using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Xunit;

namespace UIFramework.Tests
{
    /// <summary>2.0.0, what the fixes left over: the emotes of an action's answer ask the project for its emote
    /// sources without holding the window still.</summary>
    public class Fixes1100LeftoverEmoteTests
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

        /// <summary>Lets the thread's dispatcher run what is queued, until the condition holds or the time is up.</summary>
        private static void Pump(int milliseconds, Func<bool> until)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            do
            {
                var frame = new DispatcherFrame();
                Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() => frame.Continue = false));
                Dispatcher.PushFrame(frame);
                if (until()) return;
                Thread.Sleep(10);
            }
            while (watch.ElapsedMilliseconds < milliseconds);
        }

        // ---- The emotes of an answer -------------------------------------------------------------------------

        /// <summary>A project that names its emote sources only when the test lets it (or after three seconds).</summary>
        private sealed class HeldSources : NoHooks
        {
            public readonly ManualResetEventSlim Answer = new ManualResetEventSlim();
            public string Url;
            public int Asked, Thread;
            public override string EmoteSources()
            {
                Interlocked.Increment(ref Asked);
                Thread = System.Threading.Thread.CurrentThread.ManagedThreadId;
                Answer.Wait(3000);
                return "{ \"youtube\": { \":own-leftover:\": \"" + Url + "\" } }";
            }
        }

        [Fact]
        public void A_try_panels_youtube_emotes_do_not_hold_the_window_while_the_project_names_its_emote_sources() => Sta(() =>
        {
            var hooks = new HeldSources { Url = "https://example.invalid/own-" + Guid.NewGuid().ToString("N") };
            // The picture is there already, so nothing is downloaded: what matters is where it goes and when.
            var cache = (ConcurrentDictionary<string, Task<ImageSource>>)typeof(EmotePictures)
                .GetField("Pictures", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).GetValue(null);
            var picture = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[4], 4);
            picture.Freeze();
            string url = hooks.Url + "=w48-h48-c-k-nd";
            cache[url] = Task.FromResult<ImageSource>(picture);
            try
            {
                ResultPanel.Render(new StackPanel(), "{ \"lines\": [ \"warm up\" ] }");
                var panel = new StackPanel();
                var watch = System.Diagnostics.Stopwatch.StartNew();
                ResultPanel.Render(panel, @"{ ""lines"": [ { ""emotes"": ["":own-leftover:"", "":typo-leftover:"", ""🍒""], ""platform"": ""youtube"", ""text"": ""The slots"" } ] }", hooks);
                // Back before the project answered: the window's thread is free.
                Assert.True(watch.ElapsedMilliseconds < 1500, "the answer's emotes held the window for " + watch.ElapsedMilliseconds + " ms");
                var cells = ((WrapPanel)panel.Children[1]).Children.Cast<StackPanel>().ToList();
                Assert.Equal(3, cells.Count);
                // Meanwhile a shortcode holds the picture's place; a Unicode emoji needs no sources and shows itself.
                var own = Assert.IsType<Image>(cells[0].Children[0]);
                Assert.Null(own.Source);
                Assert.IsType<Image>(cells[1].Children[0]);
                Assert.Equal("🍒", Assert.IsType<TextBlock>(cells[2].Children[0]).Text);

                hooks.Answer.Set();
                Pump(3000, () => own.Source != null && cells[1].Children[0] is TextBlock);
                // The project's own emoji has its picture; the code nobody knows falls back to its text, its name beneath.
                Assert.Same(picture, own.Source);
                Assert.Equal(":typo-leftover:", Assert.IsType<TextBlock>(cells[1].Children[0]).Text);
                Assert.Equal("typo-leftover", ((TextBlock)cells[1].Children[1]).Text);
                Assert.Equal(2, cells[1].Children.Count);
                Assert.Equal(1, hooks.Asked);                      // once per window, as before
                Assert.NotEqual(Thread.CurrentThread.ManagedThreadId, hooks.Thread);
            }
            finally { cache.TryRemove(url, out _); hooks.Answer.Set(); }
        });
    }
}
