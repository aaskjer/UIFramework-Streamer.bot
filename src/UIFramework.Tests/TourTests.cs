using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Newtonsoft.Json.Linq;
using Xunit;

namespace UIFramework.Tests
{
    /// <summary>The window's tour: declared in the host block, read from the spec, checked against the settings and
    /// pages, remembered in the layout file, and stepped through on the window.</summary>
    public class TourTests
    {
        private static string TempSettings(string name) => Path.Combine(Path.GetTempPath(), "UIFramework.Tests", name + "-" + Guid.NewGuid().ToString("N"), "settings.json");

        private static void Sta(System.Action body)
        {
            System.Exception failure = null;
            var thread = new Thread(() => { try { body(); } catch (System.Exception ex) { failure = ex; } });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (failure != null) throw failure;
        }

        private static Schema SchemaWithTour(JArray tour) => Schema.Parse(new JObject
        {
            ["host"] = "demo",
            ["settings"] = JArray.Parse(@"[ { ""key"": ""useBot"", ""default"": true }, { ""key"": ""sides"", ""default"": 6, ""page"": ""game"" } ]"),
            ["window"] = new JObject { ["title"] = "Demo", ["pages"] = new JArray("game", "messages", "help", "about"), ["tour"] = tour }
        }.ToString());

        [Fact]
        public void TheHostBlockDeclaresTheTour()
        {
            var json = new UiWindow("Demo")
                .Tour(UiSchema.Tip("start"), UiSchema.Tip("sides", "A setting", "Type a number."), UiSchema.Tip(null, null, "A note."))
                .ToJson();
            var tour = (JArray)json["tour"];
            Assert.Equal(3, tour.Count);
            Assert.Equal("start", (string)tour[0]["target"]);
            Assert.Null(tour[0]["title"]);
            Assert.Equal("A setting", (string)tour[1]["title"]);
            Assert.Equal("Type a number.", (string)tour[1]["text"]);
            Assert.Null(tour[2]["target"]);
            Assert.Null(new UiWindow("Demo").ToJson()["tour"]);   // no tour, no key
        }

        [Fact]
        public void TheSpecReadsStopsAsObjectsOrNames()
        {
            var spec = SpecJson.Parse(@"{ ""tour"": [ ""save"", { ""target"": ""sides"", ""title"": ""A setting"" }, { ""text"": ""A note"" }, { }, { ""target"": "" "" } ] }");
            Assert.Equal(3, spec.Tour.Count);
            Assert.Equal("save", spec.Tour[0].Target);
            Assert.Null(spec.Tour[0].Title);
            Assert.Equal("sides", spec.Tour[1].Target);
            Assert.Equal("A setting", spec.Tour[1].Title);
            Assert.Null(spec.Tour[2].Target);
            Assert.Equal("A note", spec.Tour[2].Text);
        }

        [Fact]
        public void AStopNamingNothingKnownIsAProblem()
        {
            var schema = SchemaWithTour(JArray.Parse(@"[ ""start"", ""sides"", ""page:game"", ""page:messages"", ""customise"", { ""text"": ""note"" },
                ""nosuchkey"", ""page:nosuchpage"" ]"));
            var spec = Entry.SpecFor(null, schema, TempSettings("tour-check"));
            Entry.Prepare(spec, schema);
            Assert.Equal(8, spec.Tour.Count);
            Assert.Equal(2, spec.Problems.Count);
            Assert.Contains(spec.Problems, p => p.Contains("'nosuchkey'"));
            Assert.Contains(spec.Problems, p => p.Contains("'nosuchpage'"));
        }

        [Fact]
        public void TheLayoutFileRemembersTheTourWasShown()
        {
            string settings = TempSettings("tour-seen");
            Directory.CreateDirectory(Path.GetDirectoryName(settings));
            var layout = LayoutFile.Load(settings);
            Assert.False(layout.TourSeen);
            layout.TourSeen = true;
            layout.Save();
            Assert.True(LayoutFile.Load(settings).TourSeen);
            layout.TourSeen = false;
            layout.Save();
            Assert.Null(JObject.Parse(File.ReadAllText(Path.Combine(Path.GetDirectoryName(settings), LayoutFile.FileName)))["tourSeen"]);
        }

        [Fact]
        public void TheTourStepsThroughItsStopsAndLeavesNothingBehind()
        {
            Sta(() =>
            {
                var button = new Button { Content = "Save", Width = 80, Height = 28 };
                var host = new Grid();
                host.Children.Add(new StackPanel { Children = { button } });
                var window = new Window { Width = 500, Height = 400, Content = host, ShowInTaskbar = false, Left = -20000, Top = -20000, WindowStartupLocation = WindowStartupLocation.Manual };
                window.Show();
                int shown = 0, ended = 0;
                var stops = new List<Tour.Stop>
                {
                    new Tour.Stop { Title = "Hello", Text = "A note in the middle." },
                    new Tour.Stop { Title = "Save", Text = "Writes the settings.", Target = () => button, Show = () => shown++ }
                };
                var tour = Tour.Start(window, host, stops, () => ended++);
                Assert.True(tour.Open);
                Assert.Equal(2, host.Children.Count);   // the overlay over the content
                Assert.Equal(0, tour.Index);
                tour.Go(1);
                Assert.Equal(1, tour.Index);
                Assert.Equal(1, shown);
                tour.Go(-5);
                Assert.Equal(0, tour.Index);
                tour.Go(2);                             // past the last stop: the tour ends
                Assert.False(tour.Open);
                Assert.Single(host.Children);
                tour.End();                             // a second end does nothing
                Assert.Equal(1, ended);
                Assert.Null(Tour.Start(window, host, new List<Tour.Stop>(), () => ended++));
                window.Close();
            });
        }
    }
}
