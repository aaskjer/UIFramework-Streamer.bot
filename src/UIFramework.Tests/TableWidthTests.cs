using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace UIFramework.Tests
{
    /// <summary>How a table meets its width (<c>fitWidth</c>): kept within it, where a column the user drags and
    /// contents that grow must not push the rest past the table's edge, or free to run past it under a horizontal
    /// scrollbar. For the table of a list, a read-only table and the two log pages.</summary>
    public class TableWidthTests
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

        // Everything the window's thread has queued, layout and what follows it included.
        private static void Settle() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

        private static Window Show(UIElement content, double width)
        {
            var window = new Window { Width = width, Height = 600, Content = content, ShowInTaskbar = false, WindowStyle = WindowStyle.None, Left = -5000, Top = -5000 };
            window.Show();
            Settle();
            return window;
        }

        private static IEnumerable<T> Find<T>(DependencyObject node) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
            {
                var child = VisualTreeHelper.GetChild(node, i);
                if (child is T hit) yield return hit;
                foreach (var deeper in Find<T>(child)) yield return deeper;
            }
        }

        private static double Room(DataGrid grid) => TableWidth.Viewer(grid).ViewportWidth;
        private static double Columns(DataGrid grid) => grid.Columns.Sum(c => c.ActualWidth);
        private static bool Scrolls(DataGrid grid) => TableWidth.Viewer(grid).ComputedHorizontalScrollBarVisibility == Visibility.Visible;

        // The edge of a column, dragged in the header: the grip's own events around the width it leaves behind. The
        // grid's header handles them and marks them handled, as it does for the user's drag.
        private static void Drag(DataGrid grid, int column, double by)
        {
            var header = Find<DataGridColumnHeader>(grid).First(h => h.Column == grid.Columns[column]);
            var grip = Find<Thumb>(header).First(t => t.Name == "PART_RightHeaderGripper");
            grip.RaiseEvent(new DragStartedEventArgs(0, 0));
            grid.Columns[column].Width = new DataGridLength(grid.Columns[column].ActualWidth + by);
            Settle();
            grip.RaiseEvent(new DragCompletedEventArgs(by, 0, false));
            Settle();
        }

        // The notes of OBS Notes: a number, a long text and two short columns, without the counting column.
        private static readonly List<SettingColumn> Notes = new List<SettingColumn>
        {
            new SettingColumn { Key = "number", Kind = "int", Label = "#", ReadOnly = true },
            new SettingColumn { Key = "text", Kind = "text", Label = "Note" },
            new SettingColumn { Key = "author", Label = "By", ReadOnly = true },
            new SettingColumn { Key = "written", Label = "Written", ReadOnly = true }
        };

        private static JArray NoteRows(string longText = "Lever order: left, right, middle") => new JArray(
            new JObject { ["number"] = 1, ["text"] = "Safe code: 4 - 7 - 1", ["author"] = "alice", ["written"] = "2026-11-22 20:14" },
            new JObject { ["number"] = 2, ["text"] = longText, ["author"] = "bob", ["written"] = "2026-11-22 20:31" });

        private static DataGrid GridOf(DependencyObject root) => Find<DataGrid>(root).First();

        // ---- the table of a list ---------------------------------------------------------------------------------

        [Fact]
        public void AListsTableStaysWithinItsWidthWhenAColumnIsDragged() => Sta(() =>
        {
            var editor = new ListEditor("notes", "Notes", Notes, NoteRows(), null, "", rowNumbers: false);
            var window = Show(editor, 606);
            try
            {
                var grid = GridOf(editor);
                Assert.InRange(Room(grid) - Columns(grid), 0, 1);
                double note = grid.Columns[1].ActualWidth, by = grid.Columns[2].ActualWidth, written = grid.Columns[3].ActualWidth;

                // The Note column made 80 wider: it keeps that, and the two to its right share what is left, alike as before.
                Drag(grid, 1, 80);
                Assert.Equal(note + 80, grid.Columns[1].ActualWidth, 0);
                Assert.InRange(Room(grid) - Columns(grid), 0, 1);
                Assert.True(grid.Columns[2].ActualWidth < by && grid.Columns[3].ActualWidth < written);
                Assert.InRange(grid.Columns[2].ActualWidth - grid.Columns[3].ActualWidth, -1, 1);
                Assert.False(Scrolls(grid));

                // Dragged further than the table has room for: the columns to its right keep a width one can still grip.
                Drag(grid, 1, 2000);
                Assert.InRange(Room(grid) - Columns(grid), 0, 1);
                Assert.True(grid.Columns[2].ActualWidth >= 30 && grid.Columns[3].ActualWidth >= 30);

                // A wider table keeps what the user set: the Note column is still by far the widest.
                Drag(grid, 1, -(grid.Columns[1].ActualWidth - note - 80));
                double share = grid.Columns[1].ActualWidth / Room(grid);
                window.Width = 900;
                Settle();
                Assert.InRange(Room(grid) - Columns(grid), 0, 1);
                Assert.InRange(grid.Columns[1].ActualWidth / Room(grid) - share, -0.01, 0.01);

                // The last column ends at the table's edge: dragged, it goes back.
                double last = grid.Columns[3].ActualWidth;
                Drag(grid, 3, 120);
                Assert.Equal(last, grid.Columns[3].ActualWidth, 0);
                Assert.InRange(Room(grid) - Columns(grid), 0, 1);
            }
            finally { window.Close(); }
        });

        [Fact]
        public void TheColumnThatCountsTheRowsIsNotDragged() => Sta(() =>
        {
            var editor = new ListEditor("notes", "Notes", Notes, NoteRows(), null, "");
            var window = Show(editor, 606);
            try
            {
                var grid = GridOf(editor);
                Assert.False(grid.Columns[0].CanUserResize);
                Drag(grid, 2, 60);                                  // with the counting column in front, the same holds
                Assert.InRange(Room(grid) - Columns(grid), 0, 1);
            }
            finally { window.Close(); }
        });

        [Fact]
        public void AListsTableMayRunPastItsWidthUnderAScrollbar() => Sta(() =>
        {
            string longText = string.Join(" ", Enumerable.Repeat("a note that goes on and on", 12));
            var kept = new ListEditor("notes", "Notes", Notes, NoteRows(longText), null, "", rowNumbers: false);
            var free = new ListEditor("notes", "Notes", Notes, NoteRows(longText), null, "", rowNumbers: false, fitWidth: false);
            var short1 = new ListEditor("notes", "Notes", Notes, NoteRows(), null, "", rowNumbers: false, fitWidth: false);
            var panel = new StackPanel();
            panel.Children.Add(kept);
            panel.Children.Add(free);
            panel.Children.Add(short1);
            var window = Show(panel, 606);
            try
            {
                var within = GridOf(kept);
                Assert.False(Scrolls(within));
                Assert.InRange(Room(within) - Columns(within), 0, 1);

                // Free to run on, the long note has the width it needs and the table a scrollbar to reach its end.
                var past = GridOf(free);
                Assert.True(Scrolls(past));
                Assert.True(Columns(past) > Room(past) + 200);

                // With nothing long in it, such a table still fills its width and shows no scrollbar.
                var filled = GridOf(short1);
                Assert.False(Scrolls(filled));
                Assert.InRange(Room(filled) - Columns(filled), 0, 1);
            }
            finally { window.Close(); }
        });

        // ---- a table whose columns size themselves: a read-only table, the log pages ------------------------------

        private sealed class Line
        {
            public string Time { get; set; }
            public string Source { get; set; }
            public string Text { get; set; }
        }

        private static DataGrid LogLike(bool fit, params Line[] lines)
        {
            var grid = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true, HeadersVisibility = DataGridHeadersVisibility.Column, RowHeaderWidth = 0, ItemsSource = lines.ToList() };
            var cell = new Style(typeof(TextBlock));
            if (fit) TableWidth.Trim(cell);
            grid.Columns.Add(new DataGridTextColumn { Header = "Time", Binding = new Binding("Time"), Width = DataGridLength.Auto, MinWidth = 60, ElementStyle = cell });
            grid.Columns.Add(new DataGridTextColumn { Header = "Source", Binding = new Binding("Source"), Width = DataGridLength.Auto, MinWidth = 60, ElementStyle = cell });
            var text = new DataGridTextColumn { Header = "Line", Binding = new Binding("Text"), Width = new DataGridLength(1, DataGridLengthUnitType.Star), MinWidth = 120, ElementStyle = cell };
            grid.Columns.Add(text);
            if (fit) TableWidth.KeepWithin(grid);
            else TableWidth.RunOn(grid, text, 120);
            return grid;
        }

        private static readonly string LongLine = string.Join(" ", Enumerable.Repeat("browser source 'OBS Notes' (600 x 800) refreshed in scene", 6));

        [Fact]
        public void ATableKeptWithinItsWidthTakesBackWhatDoesNotFit() => Sta(() =>
        {
            var grid = LogLike(true, new Line { Time = "20:01:10", Source = "OBS Notes", Text = LongLine },
                new Line { Time = "20:01:44", Source = string.Join(" ", Enumerable.Repeat("a source with a very long name", 5)), Text = "Settings saved." });
            var window = Show(grid, 500);
            try
            {
                // The long source would leave the line no room: the widest column gives, the line keeps its least width.
                Assert.False(Scrolls(grid));
                Assert.InRange(Room(grid) - Columns(grid), -0.5, 1);
                Assert.True(grid.Columns[2].ActualWidth >= 120);
                Assert.Equal(TextTrimming.CharacterEllipsis, Find<TextBlock>(Find<DataGridCell>(grid).First()).First().TextTrimming);
                double squeezed = grid.Columns[1].ActualWidth;

                // A column the user drags past the table's room is taken back as well.
                Drag(grid, 0, 600);
                Assert.InRange(Room(grid) - Columns(grid), -0.5, 1);
                Assert.True(grid.Columns[0].ActualWidth > 60);

                // A wider table gives a column its own width back.
                window.Width = 2400;
                Settle();
                Assert.True(grid.Columns[1].ActualWidth > squeezed + 100);
                Assert.InRange(Room(grid) - Columns(grid), -0.5, 1);
            }
            finally { window.Close(); }
        });

        [Fact]
        public void ATableFreeToRunOnHasAScrollbarAndStillFillsItsWidth() => Sta(() =>
        {
            var longOne = LogLike(false, new Line { Time = "20:01:10", Source = "OBS Notes", Text = LongLine });
            var shortOne = LogLike(false, new Line { Time = "20:01:44", Source = "OBS Notes", Text = "Settings saved." });
            var panel = new StackPanel();
            panel.Children.Add(longOne);
            panel.Children.Add(shortOne);
            var window = Show(panel, 500);
            try
            {
                Assert.True(Scrolls(longOne));
                Assert.True(Columns(longOne) > Room(longOne) + 200);
                Assert.False(Scrolls(shortOne));
                Assert.InRange(Room(shortOne) - Columns(shortOne), -0.5, 1);       // the line's column reaches the table's edge
            }
            finally { window.Close(); }
        });

        // ---- the declaration -------------------------------------------------------------------------------------

        private const string Head = @"{ ""host"": ""demo"", ""version"": ""v1"", ""settings"": [ ";

        private static WindowSpec Prepared(string schemaJson)
        {
            try
            {
                var schema = Schema.Parse(schemaJson);
                var spec = Entry.SpecFor(schema, null);
                Entry.Prepare(spec, schema);
                return spec;
            }
            finally { Text.Use(Conventions.Default.Lang); }
        }

        [Fact]
        public void FitWidthIsReadAsDeclaredAndBelongsToAListOrATable()
        {
            Setting Read(string json) => Setting.Parse(Setting.Parse(JObject.Parse(json)).ToJson());
            Assert.True(Read(@"{ ""key"": ""notes"", ""default"": [], ""kind"": ""list"", ""fitWidth"": true }").FitWidth);
            Assert.False(Read(@"{ ""key"": ""notes"", ""default"": [], ""kind"": ""list"", ""fitWidth"": false }").FitWidth);
            Assert.Null(Read(@"{ ""key"": ""notes"", ""default"": [], ""kind"": ""list"" }").FitWidth);
            Assert.Null(Setting.Parse(JObject.Parse(@"{ ""key"": ""notes"", ""default"": [], ""kind"": ""list"" }")).ToJson()["fitWidth"]);

            const string columns = @"""columns"": [ ""a"", ""b"" ]";
            var spec = Prepared(Head + @"{ ""key"": ""bet"", ""default"": 5, ""fitWidth"": true },
                { ""key"": ""notes"", ""default"": [], ""kind"": ""list"", " + columns + @", ""fitWidth"": false },
                { ""key"": ""stats"", ""default"": [], ""kind"": ""table"", " + columns + @", ""fitWidth"": true } ] }");
            Assert.Contains("'bet' has fitWidth, which only a list or a table has; it does nothing on an int field.", spec.Problems);
            Assert.DoesNotContain(spec.Problems, p => p.Contains("'notes'") || p.Contains("'stats'"));
            Assert.False(spec.ResolvedSettings.Single(s => s.Key == "notes").FitWidth);
            Assert.True(spec.ResolvedSettings.Single(s => s.Key == "stats").FitWidth);
        }

        [Fact]
        public void TheLogPagesReadFitWidthFromTheWindow()
        {
            const string setting = @"{ ""key"": ""a"", ""default"": 1 } ], ""window"": ";
            var plain = Prepared(Head + setting + @"{ ""title"": ""T"", ""streamerBotLog"": { ""match"": [ ""[Demo"" ] }, ""log"": { ""folder"": ""Logs"", ""files"": ""*.txt"" } } }");
            Assert.Null(plain.SbLog.FitWidth);
            Assert.Null(plain.Log.FitWidth);
            var said = Prepared(Head + setting + @"{ ""title"": ""T"", ""streamerBotLog"": { ""match"": [ ""[Demo"" ], ""fitWidth"": false }, ""log"": { ""folder"": ""Logs"", ""files"": ""*.txt"", ""fitWidth"": true } } }");
            Assert.False(said.SbLog.FitWidth);
            Assert.Equal(new[] { "[Demo" }, said.SbLog.Match.ToArray());
            Assert.True(said.Log.FitWidth);
        }

        [Fact]
        public void TheHostBlockDeclaresFitWidth()
        {
            var list = UiSchema.ToJson(UiSchema.S("notes", new string[0], kind: "list", columns: new[] { "text|text|Note" }, fitWidth: true));
            Assert.True((bool)list["fitWidth"]);
            Assert.False((bool)UiSchema.ToJson(UiSchema.S("stats", new string[0], kind: "table", columns: new[] { "a" }, fitWidth: false))["fitWidth"]);
            Assert.Null(UiSchema.ToJson(UiSchema.S("notes", new string[0], kind: "list", columns: new[] { "a" }))["fitWidth"]);

            var window = new UiWindow("Demo").StreamerBotLog(false, "[Demo Game").Log("Logs", "*.txt", fitWidth: true).ToJson();
            Assert.Equal(@"{""match"":[""[Demo Game""],""fitWidth"":false}", window["streamerBotLog"].ToString(Formatting.None));
            Assert.True((bool)window["log"]["fitWidth"]);

            // Left out, the schema says nothing, and an older window reads what it always read.
            var silent = new UiWindow("Demo").StreamerBotLog("[Demo Game").Log("Logs", "*.txt").ToJson();
            Assert.Equal(@"{""match"":[""[Demo Game""]}", silent["streamerBotLog"].ToString(Formatting.None));
            Assert.Null(silent["log"]["fitWidth"]);
        }
    }
}
