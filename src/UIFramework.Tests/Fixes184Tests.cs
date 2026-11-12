using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using Newtonsoft.Json.Linq;
using Xunit;

namespace UIFramework.Tests
{
    /// <summary>1.8.4: a DLL is not used by a settings action older than it asks for; the Log page keeps the row
    /// being read at the top when lines come in; a table list's search and the row being edited.</summary>
    public class Fixes184Tests
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

        private static IEnumerable<T> Find<T>(object node) where T : class
        {
            if (node is T hit) yield return hit;
            if (node is DependencyObject d)
                foreach (var child in LogicalTreeHelper.GetChildren(d))
                    foreach (var found in Find<T>(child))
                        yield return found;
        }

        private static IEnumerable<T> Visual<T>(DependencyObject node) where T : class
        {
            if (node is T hit) yield return hit;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
                foreach (var found in Visual<T>(VisualTreeHelper.GetChild(node, i)))
                    yield return found;
        }

        private static void Pump(int milliseconds)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            do
            {
                var frame = new DispatcherFrame();
                Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new System.Action(() => frame.Continue = false));
                Dispatcher.PushFrame(frame);
                Thread.Sleep(10);
            }
            while (watch.ElapsedMilliseconds < milliseconds);
        }

        private static Window Hidden(FrameworkElement content) =>
            new Window { Width = 700, Height = 420, Content = content, ShowInTaskbar = false, ShowActivated = false, Left = -20000, Top = -20000, WindowStartupLocation = WindowStartupLocation.Manual };

        private static string RepoRoot()
        {
            string root = System.AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
            while (root != null && !File.Exists(Path.Combine(root, "PLAN.md"))) root = Path.GetDirectoryName(root);
            return root ?? throw new System.InvalidOperationException("repo root not found");
        }

        // ---- The settings action a DLL needs --------------------------------------------------------------

        [Theory]
        [InlineData("0.2.0", "0.3.0", true)]
        [InlineData("0.2.0", "0.2.1", true)]
        [InlineData("0.2", "0.2.0.1", true)]
        [InlineData("0.2.0", "0.2.0", false)]
        [InlineData("0.2.0", "0.2", false)]         // missing parts count as 0
        [InlineData("0.2.1", "0.2.0", false)]
        [InlineData("1.0.0", "0.9.9", false)]
        [InlineData(null, "0.3.0", false)]          // no stub named: the preview, a test
        [InlineData("0.2.0", null, false)]          // the DLL asks for none
        [InlineData("0.2.0", "soon", false)]
        public void A_settings_action_older_than_the_dll_asks_for_is_refused(string stub, string needs, bool refused)
        {
            string refusal = Entry.StubRefusal(stub, needs);
            Assert.Equal(refused, refusal != null);
            if (refused)
            {
                Assert.Contains("stub " + needs, refusal);
                Assert.Contains("this game's is " + stub, refusal);
                Assert.Contains(Entry.FrameworkVersion, refusal);
            }
        }

        [Fact]
        public void The_dll_names_the_settings_action_it_needs_and_the_template_stub_fits()
        {
            string root = RepoRoot();
            string csproj = File.ReadAllText(Path.Combine(root, "src", "UIFramework", "UIFramework.csproj"));
            string declared = Regex.Match(csproj, "<MinHostStub>([^<]+)</MinHostStub>").Groups[1].Value;
            Assert.NotEqual("", declared);
            // The build wrote it into the DLL, where the stub (the file's product version) and the DLL itself read it.
            Assert.Equal(declared, Entry.MinHostStub);
            string product = System.Diagnostics.FileVersionInfo.GetVersionInfo(typeof(Entry).Assembly.Location).ProductVersion;
            Assert.EndsWith("+stub." + declared, product);

            string stub = File.ReadAllText(Path.Combine(root, "stub", "Stub.cs"));
            string stubVersion = Regex.Match(stub, "private const string StubVersion = \"([^\"]+)\"").Groups[1].Value;
            Assert.NotEqual("", stubVersion);
            Assert.Null(Entry.StubRefusal(stubVersion, Entry.MinHostStub));
            // And the stubs the games carry today.
            Assert.Null(Entry.StubRefusal("0.2.0", Entry.MinHostStub));
            // The stub leaves out an installed DLL that asks for more than it is, before loading it.
            Assert.Contains("InstalledFrameworks().Where(dll => StubNeededBy(dll) == null)", stub);
        }

        [Fact]
        public void A_window_is_not_opened_for_a_settings_action_that_is_too_old()
        {
            // The stub's Host is a nested class of the action; the DLL reads StubVersion off the class around it.
            var refused = Assert.Throws<System.InvalidOperationException>(() => Entry.Show(null, "{}", null, new OldAction.Host()));
            Assert.Contains("needs a newer settings action", refused.Message);
            Assert.Contains("this game's is 0.0.1", refused.Message);
        }

        private sealed class OldAction
        {
#pragma warning disable CS0414
            private const string StubVersion = "0.0.1";
#pragma warning restore CS0414
            public sealed class Host { }
        }

        [Fact]
        public void The_release_workflow_takes_minHostStub_from_the_csproj()
        {
            string workflow = File.ReadAllText(Path.Combine(RepoRoot(), ".github", "workflows", "release.yml"));
            Assert.Contains("<MinHostStub>([^<]+)</MinHostStub>", workflow);
            Assert.Contains("steps.v.outputs.stub", workflow);
        }

        // ---- The Log page: the row being read ---------------------------------------------------------------

        private sealed class LogHooks : NoHooks
        {
            public List<string> Lines = new List<string>();
            public override IEnumerable<LogEntry> ReadLog() => Lines.Select(LogEntry.Parse).ToList();
        }

        private sealed class QuietLog : IHostLog
        {
            public void Info(string message) { }
            public void Warn(string message) { }
            public void Error(string message) { }
        }

        private static string Line(int second) =>
            "[2026-11-07 13:" + (second / 60).ToString("00") + ":" + (second % 60).ToString("00") + "] WIN | User: user" + second + " | Platform: twitch";

        private static FrameworkElement LogPageOf(LogHooks hooks)
        {
            var ctx = new PageContext();
            typeof(PageContext).GetProperty("Spec").SetValue(ctx, new WindowSpec());
            typeof(PageContext).GetProperty("Hooks").SetValue(ctx, hooks);
            typeof(PageContext).GetProperty("Log").SetValue(ctx, new QuietLog());
            return (FrameworkElement)LogPage.Build(ctx);
        }

        private static void ClickReload(FrameworkElement page) =>
            Find<Button>(page).Single(b => AutomationProperties.GetName(b) == Text.T("log.reload")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

        [Fact]
        public void Lines_coming_in_leave_the_row_being_read_at_the_top() => Sta(() =>
        {
            var hooks = new LogHooks();
            for (int i = 0; i < 80; i++) hooks.Lines.Add(Line(i));
            var page = LogPageOf(hooks);
            var window = Hidden(page);
            window.Show();
            try
            {
                var grid = Find<DataGrid>(page).Single();
                grid.UpdateLayout();
                var viewer = (ScrollViewer)grid.Template.FindName("DG_ScrollViewer", grid);
                string RawAtTop() => ((LogEntry)grid.Items[(int)viewer.VerticalOffset]).Raw;

                viewer.ScrollToVerticalOffset(30);
                Pump(200);
                string reading = RawAtTop();

                // Three rounds in chat: the newest line is the first row, so the rows move down by three.
                for (int i = 80; i < 83; i++) hooks.Lines.Add(Line(i));
                ClickReload(page);
                Pump(200);
                Assert.Equal(83, grid.Items.Count);
                Assert.Equal(33, viewer.VerticalOffset);
                Assert.Equal(reading, RawAtTop());

                // Lines gone from the top (a new day's file): the row is found again further up.
                hooks.Lines.RemoveRange(78, 5);
                ClickReload(page);
                Pump(200);
                Assert.Equal(28, viewer.VerticalOffset);
                Assert.Equal(reading, RawAtTop());

                // The row itself is gone: the grid stays where it is.
                hooks.Lines.Remove(reading);
                ClickReload(page);
                Pump(200);
                Assert.Equal(28, viewer.VerticalOffset);

                // At the very top nothing is held: the new line shows.
                viewer.ScrollToVerticalOffset(0);
                Pump(200);
                hooks.Lines.Add(Line(90));
                ClickReload(page);
                Pump(200);
                Assert.Equal(0, viewer.VerticalOffset);
                Assert.Equal(Line(90), ((LogEntry)grid.Items[0]).Raw);
            }
            finally { window.Close(); }
        });

        [Fact]
        public void The_row_of_an_entry_is_the_one_nearest_to_where_it_was()
        {
            var rows = new List<object> { LogEntry.Parse("a"), LogEntry.Parse("same"), LogEntry.Parse("b"), LogEntry.Parse("same"), "no entry" };
            Assert.Equal(0, LogPage.RowOf(rows, "a", 3));
            Assert.Equal(1, LogPage.RowOf(rows, "same", 0));
            Assert.Equal(3, LogPage.RowOf(rows, "same", 4));
            Assert.Equal(-1, LogPage.RowOf(rows, "gone", 1));
            Assert.Equal(-1, LogPage.RowOf(rows, null, 1));
            Assert.Equal(-1, LogPage.RowOf(rows, "", 1));
        }

        // ---- A table list's search and the row being edited ------------------------------------------------

        [Fact]
        public void A_table_lists_search_leaves_the_row_being_edited_where_it_is() => Sta(() =>
        {
            var columns = new List<SettingColumn>
            {
                new SettingColumn { Key = "Word", Kind = "string", Label = "Word" },
                new SettingColumn { Key = "Hint", Kind = "text", Label = "Hint" }
            };
            Assert.True(ListTable.Suits(columns));
            var table = new ListTable("Words", columns, new JArray(), () => { });
            table.Set(JArray.Parse(@"[ { ""Word"": ""apple"", ""Hint"": ""a fruit"" }, { ""Word"": ""apricot"", ""Hint"": """" }, { ""Word"": ""banana"", ""Hint"": """" } ]"));
            var window = Hidden(table.Element);
            window.Show();
            try
            {
                var grid = Find<DataGrid>(table.Element).Single();
                var search = Find<TextBox>(table.Element).First();
                grid.UpdateLayout();
                search.Text = "app";
                Pump(100);
                Assert.Single(grid.Items);

                // Deleting a "p" from "apple" in its cell: the row no longer matches "app".
                grid.CurrentCell = new DataGridCellInfo(grid.Items[0], grid.Columns[1]);
                Assert.True(grid.BeginEdit(), "the cell goes into edit mode");
                Pump(100);
                var box = Visual<TextBox>(grid).Single();
                box.Text = "aple";
                Pump(100);
                Assert.Single(grid.Items);      // still there while it is typed in

                // On to the next cell of the same row: still there.
                grid.CommitEdit(DataGridEditingUnit.Cell, true);   // the cell takes the focus and is edited again at once, so this says false
                Pump(100);
                Assert.Single(grid.Items);
                Assert.Equal("aple", (string)table.Peek[0]["Word"]);

                // The row is left: only now does the search take it, and what was typed is kept.
                grid.CommitEdit(DataGridEditingUnit.Row, true);
                Pump(100);
                Assert.Empty(grid.Items);
                Assert.Equal("aple", (string)table.Value[0]["Word"]);
            }
            finally { window.Close(); }
        });
    }
}
