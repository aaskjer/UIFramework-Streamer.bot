using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Xunit;

namespace UIFramework.Tests
{
    /// <summary>The search (Ctrl+F): what it finds and in which order, and the bar that lists it.</summary>
    public class SearchTests
    {
        private static List<SearchEntry> Entries(List<string> went = null) => new List<SearchEntry>
        {
            new SearchEntry { Kind = SearchEntry.Page, Key = "game", Title = "Game", Go = () => went?.Add("page:game") },
            new SearchEntry { Kind = SearchEntry.Page, Key = "access", Title = "Access", Go = () => went?.Add("page:access") },
            new SearchEntry { Key = "answerTimeSeconds", Title = "Answer time", Where = "Game › Rounds", Help = "How long a normal round accepts answers, in seconds.", Go = () => went?.Add("answerTimeSeconds") },
            new SearchEntry { Key = "blitzAnswerTimeSeconds", Title = "Answer time", Where = "Game › Blitz rounds", Help = "How long a blitz round accepts answers.", Go = () => went?.Add("blitzAnswerTimeSeconds") },
            new SearchEntry { Key = "userCooldown", Title = "Per viewer", Where = "Access › Cooldowns", Help = "How long one viewer waits before starting another round.", Go = () => went?.Add("userCooldown") },
            new SearchEntry { Key = "pointsVariable", Title = "Punkte-Variable", Where = "Spiel › Punkte", Help = "Die Variable für die Auszahlung, größer als nichts.", Go = () => went?.Add("pointsVariable") },
            new SearchEntry { Key = "overlayEnabled", Title = "Über dem Bild zeigen", Where = "Overlay", Go = () => went?.Add("overlayEnabled") },
            new SearchEntry { Kind = SearchEntry.Message, Key = "roundWon", Title = "Round won", Where = "Messages › Rounds", Help = "Sent when a round has a winner.", Go = () => went?.Add("roundWon") }
        };

        private static string[] Keys(string query) => SearchIndex.Find(Entries(), query).Select(e => e.Key).ToArray();

        [Fact]
        public void A_word_finds_by_title_key_place_or_help()
        {
            Assert.Equal(new[] { "answerTimeSeconds", "blitzAnswerTimeSeconds" }, Keys("answer"));
            Assert.Equal(new[] { "userCooldown" }, Keys("cooldown"));          // the key and the place
            Assert.Equal(new[] { "userCooldown" }, Keys("waits"));             // only the help has it
            Assert.Equal(new[] { "roundWon" }, Keys("winner"));
            Assert.Empty(Keys("zebra"));
            Assert.Empty(Keys("   "));
            Assert.Empty(SearchIndex.Find(null, "answer"));
        }

        [Fact]
        public void Every_word_has_to_occur_in_any_order()
        {
            Assert.Equal(new[] { "blitzAnswerTimeSeconds" }, Keys("blitz answer"));
            Assert.Equal(new[] { "blitzAnswerTimeSeconds" }, Keys("time BLITZ"));
            Assert.Empty(Keys("blitz cooldown"));
        }

        [Fact]
        public void The_title_counts_before_the_place_and_the_help()
        {
            // "Game" the page, then the settings that merely sit on it.
            Assert.Equal("game", Keys("game")[0]);
            // "round": a title that starts with it, then titles and places that hold it, then help alone.
            var round = Keys("round");
            Assert.Equal("roundWon", round[0]);
            Assert.True(Array.IndexOf(round, "answerTimeSeconds") < Array.IndexOf(round, "userCooldown"), string.Join(",", round));
        }

        [Fact]
        public void Case_accents_and_sharp_s_do_not_matter()
        {
            Assert.Equal(new[] { "overlayEnabled" }, Keys("uber"));
            Assert.Equal(new[] { "overlayEnabled" }, Keys("ÜBER"));
            Assert.Equal(new[] { "pointsVariable" }, Keys("grosser"));
            Assert.Equal(new[] { "pointsVariable" }, Keys("größer"));
            Assert.Equal("strasse uber cafe", SearchIndex.Fold("Straße Über Café"));
        }

        [Fact]
        public void No_more_than_asked_for()
        {
            var many = Enumerable.Range(0, 100).Select(i => new SearchEntry { Key = "k" + i, Title = "Setting " + i }).ToList();
            Assert.Equal(40, SearchIndex.Find(many, "setting").Count);
            Assert.Equal(5, SearchIndex.Find(many, "setting", 5).Count);
            Assert.Equal("k0", SearchIndex.Find(many, "setting")[0].Key);     // alike in rank: the window's own order
        }

        [Fact]
        public void The_bar_lists_as_you_type_and_Enter_goes_there() => Sta(() =>
        {
            var went = new List<string>();
            var layers = new Grid { Width = 700, Height = 500 };
            var window = new Window { Content = layers, Width = 700, Height = 500, ShowInTaskbar = false, WindowStyle = WindowStyle.None, Left = -20000, Top = -20000 };
            var bar = new SearchBar(window, layers, null, () => Entries(went));
            Assert.False(bar.IsOpen);

            bar.Open();
            Assert.True(bar.IsOpen);
            Assert.Equal(2, layers.Children.Count);       // the panel, and what catches a click beside it
            Assert.Empty(bar.Results);

            bar.Box.Text = "answer";
            Assert.Equal(new[] { "answerTimeSeconds", "blitzAnswerTimeSeconds" }, bar.Results.Select(r => r.Key));
            Assert.Equal(0, bar.Selected);
            var rows = Walk(layers).OfType<Border>().Where(b => b.Tag as string == SearchBar.RowTag).ToList();
            Assert.Equal(2, rows.Count);
            Assert.Contains("Game › Blitz rounds", Walk(rows[1]).OfType<TextBlock>().Select(t => t.Text));

            bar.Move(1);
            Assert.Equal(1, bar.Selected);
            bar.Move(5);                                   // stops at the last
            Assert.Equal(1, bar.Selected);
            bar.GoToSelected();
            Assert.Equal(new[] { "blitzAnswerTimeSeconds" }, went);
            Assert.False(bar.IsOpen);
            Assert.Empty(layers.Children);

            // Opened again it starts empty; a message says what kind it is, and nothing found says so.
            bar.Open();
            Assert.Equal("", bar.Box.Text);
            bar.Box.Text = "winner";
            Assert.Contains(Walk(layers).OfType<TextBlock>(), t => t.Text.StartsWith("Message"));
            bar.Box.Text = "zebra";
            Assert.Empty(bar.Results);
            Assert.Contains(Walk(layers).OfType<TextBlock>(), t => t.Text.Contains("zebra"));
            bar.GoToSelected();                            // nothing to go to: stays open, goes nowhere
            Assert.True(bar.IsOpen);
            Assert.Single(went);
            bar.Close();
            Assert.False(bar.IsOpen);
            window.Close();
        });

        [Fact]
        public void The_search_texts_are_there_in_every_language()
        {
            foreach (string code in new[] { "en", "de", "es" })
            {
                using (var stream = typeof(Conventions).Assembly.GetManifestResourceStream("UIFramework.conventions.lang." + code + ".json"))
                using (var reader = new System.IO.StreamReader(stream))
                {
                    var ui = Newtonsoft.Json.Linq.JObject.Parse(reader.ReadToEnd())["ui"];
                    foreach (string key in new[] { "tip.search", "search.placeholder", "search.hint", "search.keys", "search.none", "search.kind.page", "search.kind.message", "tour.search.title", "tour.search.text" })
                        Assert.False(string.IsNullOrWhiteSpace((string)ui[key]), key + " is missing in " + code + ".json");
                    Assert.Contains("{0}", (string)ui["search.none"]);
                }
            }
        }

        private static List<object> Walk(object root)
        {
            var all = new List<object>();
            void Visit(object node) { all.Add(node); if (node is DependencyObject d) foreach (var child in LogicalTreeHelper.GetChildren(d)) Visit(child); }
            Visit(root);
            return all;
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
