using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Newtonsoft.Json.Linq;
using Xunit;

namespace UIFramework.Tests
{
    /// <summary>What the hosts asked for after their move: an action's set fills a list, a list has a search box and
    /// read-only columns, a text field can be masked, a requirement can hide, and the window's language reaches
    /// column headers, the log switch and the help page.</summary>
    public class GapTests
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

        private static readonly List<SettingColumn> Columns = new List<SettingColumn>
        {
            new SettingColumn { Key = "Word", Kind = "string", Label = "Word" },
            new SettingColumn { Key = "By", Kind = "string", Label = "Suggested by", ReadOnly = true }
        };

        [Fact]
        public void A_set_in_an_answer_fills_a_list() => Sta(() =>
        {
            var parts = new WindowParts();
            var editor = new ListEditor("words", "Words", Columns, new JArray(), null, "");
            parts.Lists["words"] = editor;
            Assert.True(LiveValues.Set(parts, "words", JArray.Parse(@"[ { ""Word"": ""apple"" }, { ""Word"": ""pear"" } ]")));
            Assert.Equal(2, editor.Value.Count);
            Assert.True(LiveValues.Set(parts, "words", new JValue(@"[ { ""Word"": ""plum"" } ]")));
            Assert.Equal("plum", (string)editor.Value[0]["Word"]);
            Assert.False(LiveValues.Set(parts, "words", new JValue("not json")));
            Assert.False(LiveValues.Set(parts, "nothere", new JArray()));
        });

        [Fact]
        public void A_read_only_column_keeps_its_value_but_takes_no_typing() => Sta(() =>
        {
            var editor = new ListEditor("words", "Words", Columns, JArray.Parse(@"[ { ""Word"": ""apple"", ""By"": ""alice"" } ]"), null, "");
            var boxes = Walk(editor).OfType<TextBox>().Where(b => b.IsReadOnly).ToList();
            Assert.Single(boxes);
            Assert.Equal("alice", boxes[0].Text);
            Assert.Equal("alice", (string)editor.Value[0]["By"]);
        });

        [Fact]
        public void The_column_spec_carries_read_only_through_json()
        {
            var column = SettingColumn.Parse(JObject.Parse(@"{ ""key"": ""By"", ""kind"": ""string"", ""label"": ""By"", ""readOnly"": true }"));
            Assert.True(column.ReadOnly);
            Assert.True((bool)column.ToJson()["readOnly"]);
            Assert.Null(SettingColumn.Parse(JObject.Parse(@"{ ""key"": ""Word"" }")).ToJson()["readOnly"]);
        }

        [Fact]
        public void The_search_box_shows_from_eight_rows_and_hides_what_does_not_match() => Sta(() =>
        {
            var rows = new JArray();
            for (int i = 0; i < 9; i++) rows.Add(new JObject { ["Word"] = "word" + i });
            var editor = new ListEditor("words", "Words", Columns, rows, null, "");
            var search = Walk(editor).OfType<TextBox>().First(b => AutomationNameOf(b).StartsWith("Search"));
            Assert.Equal(Visibility.Visible, ((FrameworkElement)Parent(search)).Visibility);
            search.Text = "word7";
            var numbered = Walk(editor).OfType<Grid>().Where(g => g.Children.OfType<TextBlock>().Any()).ToList();
            Assert.Contains(numbered, g => g.Visibility == Visibility.Visible && g.Children.OfType<TextBlock>().Any(t => t.Text == "8"));
            Assert.Contains(numbered, g => g.Visibility == Visibility.Collapsed && g.Children.OfType<TextBlock>().Any(t => t.Text == "3"));
            Assert.Equal(9, editor.Value.Count);   // hidden rows are still saved

            var few = new ListEditor("words", "Words", Columns, JArray.Parse(@"[ { ""Word"": ""one"" } ]"), null, "");
            var hidden = Walk(few).OfType<TextBox>().First(b => AutomationNameOf(b).StartsWith("Search"));
            Assert.Equal(Visibility.Collapsed, ((FrameworkElement)Parent(hidden)).Visibility);
        });

        [Fact]
        public void A_masked_field_mirrors_its_box_both_ways() => Sta(() =>
        {
            var parts = new WindowParts();
            var defaults = JObject.Parse(@"{ ""token"": """" }");
            var store = new SettingsStore(System.IO.Path.GetTempFileName(), defaults);
            var settings = store.Merged(JObject.Parse(@"{ ""token"": ""s3cret"" }"));
            var fields = new Fields(parts, settings, defaults, _ => { });
            var element = fields.MaskedField("Token", "token", "");
            var box = parts.Boxes["token"];
            var masked = Walk(element).OfType<PasswordBox>().Single();
            Assert.Equal("s3cret", masked.Password);
            Assert.Equal(Visibility.Collapsed, box.Visibility);
            masked.Password = "typed";
            Assert.Equal("typed", box.Text);
            LiveValues.Set(parts, "token", new JValue("fromSet"));
            Assert.Equal("fromSet", masked.Password);
            var eye = Walk(element).OfType<Button>().Single();
            eye.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            Assert.Equal(Visibility.Visible, box.Visibility);
            Assert.Equal(Visibility.Collapsed, masked.Visibility);
        });

        [Fact]
        public void Masked_and_hide_travel_through_the_schema()
        {
            var root = JObject.Parse(@"{ ""host"": ""demo"", ""version"": ""v1"", ""settingsFile"": ""x"", ""settings"": [
                { ""key"": ""source"", ""default"": ""a"", ""kind"": ""options"", ""options"": [ { ""value"": ""a"", ""label"": ""A"" }, { ""value"": ""b"", ""label"": ""B"" } ] },
                { ""key"": ""secret"", ""default"": """", ""masked"": true, ""requires"": ""hide:source=b"" }
            ], ""window"": { ""title"": ""T"", ""pages"": [ { ""id"": ""game"", ""fields"": [""*""] } ] } }");
            var schema = Schema.Parse(root);
            Assert.True(schema.Settings.Single(s => s.Key == "secret").Masked);
            Assert.True((bool)schema.Settings.Single(s => s.Key == "secret").ToJson()["masked"]);
            var spec = Entry.SpecFor(null, schema, System.IO.Path.Combine(System.IO.Path.GetTempPath(), "UIFramework.Tests", "gaps", "settings.json"));
            Entry.Prepare(spec, schema);
            Assert.Empty(spec.Problems);   // hide: in front of a condition is understood by the check
        }

        [Fact]
        public void German_reaches_column_headers_the_log_switch_and_the_help_page()
        {
            var german = Conventions.Load("de").Lang;
            german.Overlay(JObject.Parse(@"{ ""texts"": { ""Suggested by"": ""Vorgeschlagen von"", ""How a round plays"": ""So läuft eine Runde"", ""starts a round."": ""startet eine Runde."" } }"));
            Assert.Equal("Vorgeschlagen von", german.Text("Suggested by"));
            Assert.Equal("Täglich eine Protokolldatei schreiben", german.Text("Write a log file per day"));
            Assert.Equal("So läuft eine Runde", german.Text("How a round plays"));
            Assert.Equal("startet eine Runde.", german.Text("starts a round."));
            Assert.Equal("Unknown stays", german.Text("Unknown stays"));
        }

        [Fact]
        public void A_wide_list_is_a_table_that_keeps_what_no_column_shows() => Sta(() =>
        {
            var columns = new List<SettingColumn>
            {
                new SettingColumn { Key = "Question", Kind = "text", Label = "Question" },
                new SettingColumn { Key = "Answer", Kind = "string", Label = "Answer" }
            };
            var rows = JArray.Parse(@"[ { ""Question"": ""Q1"", ""Answer"": ""A1"", ""SuggestedAtUtc"": ""2026-11-06"" }, { ""Question"": ""Q2"", ""Answer"": ""A2"" } ]");
            var editor = new ListEditor("q", "Questions", columns, rows, null, "");
            var grid = Walk(editor).OfType<DataGrid>().Single();
            Assert.Equal(2, grid.Items.Count);
            Assert.Equal("2026-11-06", (string)editor.Value[0]["SuggestedAtUtc"]);   // kept though no column shows it

            grid.SelectedIndex = 0;
            var answer = Walk(editor).OfType<TextBox>().Single(b => b.Text == "A1");
            answer.Text = "A1 changed";
            Assert.Equal("A1 changed", (string)editor.Value[0]["Answer"]);
            Assert.Equal("2026-11-06", (string)editor.Value[0]["SuggestedAtUtc"]);

            var search = Walk(editor).OfType<TextBox>().First(b => AutomationNameOf(b).StartsWith("Search"));
            search.Text = "Q2";
            Assert.Equal(1, grid.Items.Count);
            Assert.Equal(2, editor.Value.Count);   // a filtered-out row is still saved

            Assert.True(ListTable.Suits(columns));
            Assert.False(ListTable.Suits(new List<SettingColumn> { new SettingColumn { Key = "Code", Kind = "emote" }, new SettingColumn { Key = "Weight", Kind = "int" } }));
        });

        /// <summary>Every element under a root, through the logical tree (nothing here is rendered).</summary>
        private static IEnumerable<DependencyObject> Walk(DependencyObject root)
        {
            yield return root;
            foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
                foreach (var d in Walk(child))
                    yield return d;
        }

        private static string AutomationNameOf(DependencyObject d) => System.Windows.Automation.AutomationProperties.GetName(d) ?? "";

        private static DependencyObject Parent(DependencyObject d) => LogicalTreeHelper.GetParent(d);
    }
}
