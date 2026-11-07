using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Xunit;

namespace UIFramework.Tests
{
    /// <summary>The red "i": what went wrong, in one list behind one button in the header, instead of a banner and a
    /// list on the About or Log page.</summary>
    public class ProblemsTests
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

        [Fact]
        public void TheListHoldsTheDeclaredAndWhatOccurred()
        {
            var declared = new List<string> { "'x' is not a setting." };
            var list = new ProblemList(() => declared);
            int changed = 0;
            list.Changed += () => changed++;
            Assert.Equal(1, list.Count);
            list.Add("Save error: disk full");
            list.Add("Save error: disk full");   // the same twice in a row counts once
            list.Add("  ");                      // nothing
            Assert.Equal(2, list.Count);
            Assert.Equal(1, changed);
            declared.Add("'y' is not a setting.");   // a rebuild finds the declared ones again
            Assert.Equal(3, list.Count);
            string text = list.AsText("Problems (3)");
            Assert.Contains("- 'x' is not a setting.", text);
            Assert.Contains("Save error: disk full", text);
            for (int i = 0; i < 150; i++) list.Add("failure " + i);
            Assert.Equal(100, list.Occurred.Count);   // the newest hundred
            Assert.Equal("failure 149", list.Occurred.Last().Text);
        }

        [Fact]
        public void TheButtonShowsOnlyWhileSomethingWentWrong()
        {
            Sta(() =>
            {
                var declared = new List<string>();
                var list = new ProblemList(() => declared);
                var button = ProblemsView.Button();
                ProblemsView.Update(button, list);
                Assert.Equal(Visibility.Collapsed, button.Visibility);
                declared.Add("'x' is not a setting.");
                list.Add("Page 'game' could not be built: boom");
                ProblemsView.Update(button, list);
                Assert.Equal(Visibility.Visible, button.Visibility);
                Assert.Contains("2", button.ToolTip.ToString());
            });
        }

        [Fact]
        public void ThePanelOpensOverTheWindowAndClosesWithoutTrace()
        {
            Sta(() =>
            {
                var list = new ProblemList(() => new[] { "'x' is not a setting." });
                list.Add("Save error: disk full");
                var button = ProblemsView.Button();
                ProblemsView.Update(button, list);
                var layers = new Grid();
                layers.Children.Add(new DockPanel { Children = { button } });
                var window = new Window { Width = 500, Height = 400, Content = layers, ShowInTaskbar = false, Left = -20000, Top = -20000, WindowStartupLocation = WindowStartupLocation.Manual };
                window.Show();
                window.UpdateLayout();
                var close = ProblemsView.Open(window, layers, button, list);
                Assert.Equal(3, layers.Children.Count);   // content, the click catcher, the panel
                var texts = LogicalTexts(layers).ToList();
                Assert.Contains("'x' is not a setting.", texts);
                Assert.Contains("Save error: disk full", texts);
                close();
                Assert.Single(layers.Children);
                close();                                  // twice does nothing
                Assert.Single(layers.Children);
                window.Close();
            });
        }

        [Fact]
        public void NoPageListsTheProblemsAnyMore()
        {
            var schema = Schema.Parse(@"{ ""host"": ""demo"", ""settings"": [ { ""key"": ""odd"", ""default"": 1, ""kind"": ""colr"", ""page"": ""game"" } ],
                ""window"": { ""title"": ""Demo"", ""pages"": [ ""game"", ""log"", ""about"" ], ""log"": { ""folder"": ""Demo/Logs"" } } }");
            var spec = Entry.SpecFor(null, schema, Path.Combine(Path.GetTempPath(), "UIFramework.Tests", "problems-" + System.Guid.NewGuid().ToString("N"), "settings.json"));
            Entry.Prepare(spec, schema);
            Assert.Single(spec.Problems);
            Sta(() =>
            {
                var ctx = Entry.ContextFor(spec, new Newtonsoft.Json.Linq.JObject(), new WindowParts(), new QuietLog(), null);
                foreach (var build in new System.Func<PageContext, UIElement>[] { StandardPages.BuildAbout, StandardPages.BuildLog })
                {
                    var page = build(ctx);
                    if (page == null) continue;   // a page the window would not show here
                    var window = new Window { Width = 600, Height = 500, Content = page, ShowInTaskbar = false, Left = -20000, Top = -20000, WindowStartupLocation = WindowStartupLocation.Manual };
                    window.Show();
                    window.UpdateLayout();
                    var texts = WindowCapture.Descendants(window).OfType<TextBlock>().Select(t => t.Text ?? "").ToList();
                    window.Close();
                    Assert.NotEmpty(texts);
                    Assert.DoesNotContain(texts, t => t.Contains("colr"));
                }
            });
        }

        private sealed class QuietLog : IHostLog
        {
            public void Info(string message) { }
            public void Warn(string message) { }
            public void Error(string message) { }
        }

        private static IEnumerable<string> LogicalTexts(object node)
        {
            if (node is TextBlock block) yield return block.Text ?? "";
            if (node is DependencyObject d)
                foreach (var child in LogicalTreeHelper.GetChildren(d))
                    foreach (var text in LogicalTexts(child)) yield return text;
        }
    }
}
