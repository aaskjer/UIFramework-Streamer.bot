using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using Newtonsoft.Json.Linq;
using Xunit;

namespace UIFramework.Tests
{
    /// <summary>A list the project fills as well (public holidays it downloads, birthdays a chat command adds): a
    /// save merges with the file by <c>rowKey</c>, <c>fixedRows</c> takes the buttons that add or remove rows away, a
    /// <c>filter</c> says which rows show, and an array in a read-only cell shows as text and stays an array.</summary>
    public class ProjectListTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "UIFramework.Tests", "plist-" + Guid.NewGuid().ToString("N"));
        private string SettingsPath => Path.Combine(_folder, "settings.json");
        private string ListPath => Path.Combine(_folder, "birthdays.json");

        public ProjectListTests() { Directory.CreateDirectory(_folder); }
        public void Dispose() { try { Directory.Delete(_folder, true); } catch { } }

        private static void Sta(Action body)
        {
            Exception failure = null;
            var thread = new Thread(() => { try { body(); } catch (Exception ex) { failure = ex; } });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (failure != null) throw failure;
        }

        // Three columns: the row editor. A fourth makes it the table.
        private static readonly List<SettingColumn> Columns = new List<SettingColumn>
        {
            new SettingColumn { Key = "Date", Label = "Date" },
            new SettingColumn { Key = "Name", Label = "Name" },
            new SettingColumn { Key = "Note", Label = "Note" }
        };

        private static readonly Setting Birthdays = new Setting
            { Key = "birthdays", Kind = "list", File = "birthdays.json", Field = "birthdays", RowKey = "Date,Name", Columns = Columns };

        private static JArray Rows(string json) => JArray.Parse(json);
        private static string Names(JArray rows) => string.Join(",", rows.Select(row => row is JObject ? (string)row["Name"] : row.ToString()));
        private static string Notes(JArray rows) => string.Join(",", rows.Select(row => (string)row["Note"]));

        private const string Anna = "{'Date':'05-01','Name':'anna','Note':''}", Ben = "{'Date':'06-02','Name':'ben','Note':''}",
            Cleo = "{'Date':'07-03','Name':'cleo','Note':'from chat'}";

        /// <summary>The project writes its file while the window is open; then the window saves the list it holds,
        /// which it loaded as <paramref name="loaded"/>. What the file holds afterwards.</summary>
        private JArray Save(string loaded, string mine, string fileNow, Setting setting = null)
        {
            File.WriteAllText(ListPath, JToken.Parse(fileNow).ToString());
            var settings = new JObject { ["birthdays"] = Rows(mine) };
            var main = Sidecars.Split(settings, new[] { setting ?? Birthdays }, SettingsPath, new HashSet<string> { "birthdays" }, null,
                new Dictionary<string, JArray> { { "birthdays", Rows(loaded) } });
            Assert.Null(main["birthdays"]);
            var written = (JArray)JObject.Parse(File.ReadAllText(ListPath))["birthdays"];
            Assert.True(JToken.DeepEquals(written, settings["birthdays"]), "the window does not get back what was written");
            return written;
        }

        // ---- The merge, with the file the project wrote meanwhile ------------------------------------------------

        [Fact]
        public void ARowTheProjectAddedMeanwhileStaysAndTheUsersChangeGoesIn()
        {
            var written = Save("[" + Anna + "," + Ben + "]",
                "[{'Date':'05-01','Name':'anna','Note':'cake'}," + Ben + "]",
                "{'version':3,'birthdays':[" + Anna + "," + Ben + "," + Cleo + "]}");
            Assert.Equal("anna,ben,cleo", Names(written));
            Assert.Equal("cake,,from chat", Notes(written));
            Assert.Equal(3, (int)JObject.Parse(File.ReadAllText(ListPath))["version"]);   // the rest of the file stays
            Assert.True(File.Exists(ListPath + ".bak"));
        }

        [Fact]
        public void ARowTheProjectRemovedMeanwhileStaysRemovedUnlessTheUserChangedIt()
        {
            // Untouched in the window: the project's removal holds.
            var written = Save("[" + Anna + "," + Ben + "]", "[{'Date':'05-01','Name':'anna','Note':'cake'}," + Ben + "]", "{'birthdays':[" + Anna + "]}");
            Assert.Equal("anna", Names(written));
            Assert.Equal("cake", Notes(written));

            // Changed in the window: it comes back as the user has it, behind the row it follows there.
            written = Save("[" + Anna + "," + Ben + "]", "[" + Anna + ",{'Date':'06-02','Name':'ben','Note':'new'}]", "{'birthdays':[" + Anna + "," + Cleo + "]}");
            Assert.Equal("anna,ben,cleo", Names(written));
            Assert.Equal(",new,from chat", Notes(written));
        }

        [Fact]
        public void InARowBothChangedTheUsersCellsWinAndTheOthersAreTheProjects()
        {
            // The row editor writes its columns only, so the window's row has no "Seen": that is no change.
            var written = Save("[{'Date':'05-01','Name':'anna','Note':'','Seen':1}," + Ben + "]",
                "[{'Date':'05-01','Name':'anna','Note':'cake'}," + Ben + "]",
                "{'birthdays':[{'Date':'05-01','Name':'anna','Note':'chat','Seen':5,'Gift':'book'},{'Date':'06-02','Name':'ben','Note':'by the project'}]}");
            Assert.Equal("cake", (string)written[0]["Note"]);       // the user's change over the project's
            Assert.Equal(5, (int)written[0]["Seen"]);               // what the user did not touch is the project's
            Assert.Equal("book", (string)written[0]["Gift"]);
            Assert.Equal("by the project", (string)written[1]["Note"]);   // a row the user left alone
        }

        [Fact]
        public void ARowTheUserRemovedIsRemovedAndOneTheUserAddedIsAdded()
        {
            var written = Save("[" + Anna + "," + Ben + "]", "[" + Anna + ",{'Date':'08-04','Name':'dora','Note':''}]",
                "{'birthdays':[" + Anna + ",{'Date':'06-02','Name':'ben','Note':'changed by the project'}," + Cleo + "]}");
            Assert.Equal("anna,dora,cleo", Names(written));
        }

        [Fact]
        public void ARowBothAddedIsOneRowWithTheCellsTheUserFilledIn()
        {
            var written = Save("[" + Anna + "]", "[" + Anna + ",{'Date':'07-03','Name':'cleo','Note':'mine'}]", "{'birthdays':[" + Anna + ",{'Date':'07-03','Name':'cleo','Note':'theirs','Seen':2}]}");
            Assert.Equal("anna,cleo", Names(written));
            Assert.Equal("mine", (string)written[1]["Note"]);
            Assert.Equal(2, (int)written[1]["Seen"]);

            // A cell the user left empty does not empty the project's.
            written = Save("[" + Anna + "]", "[" + Anna + ",{'Date':'07-03','Name':'cleo','Note':''}]", "{'birthdays':[" + Anna + ",{'Date':'07-03','Name':'cleo','Note':'theirs'}]}");
            Assert.Equal("theirs", (string)written[1]["Note"]);
        }

        [Fact]
        public void TheOrderIsTheFilesWithTheUsersNewRowsWhereTheUserPutThem()
        {
            string Row(string name) => "{'Date':'01-01','Name':'" + name + "','Note':''}";
            string Listed(params string[] names) => "[" + string.Join(",", names.Select(Row)) + "]";

            // Behind the row each follows in the window.
            var written = Save(Listed("a", "b", "c"), Listed("a", "u1", "b", "c", "u2"), "{'birthdays':" + Listed("c", "a", "x", "b") + "}");
            Assert.Equal("c,u2,a,u1,x,b", Names(written));
            // Nothing before it: in front of the row that follows it.
            written = Save(Listed("a", "b", "c"), Listed("u0", "u1", "a", "b", "c"), "{'birthdays':" + Listed("c", "a", "x", "b") + "}");
            Assert.Equal("c,u0,u1,a,x,b", Names(written));
            // The row before it is gone from the file: behind the nearest one that is still there.
            written = Save(Listed("a", "b", "c"), Listed("a", "b", "u", "c"), "{'birthdays':" + Listed("a", "c") + "}");
            Assert.Equal("a,u,c", Names(written));
            // No row of the window is in the file: at the end.
            written = Save(Listed("a"), Listed("a", "u"), "{'birthdays':" + Listed("x", "y") + "}");
            Assert.Equal("x,y,u", Names(written));
        }

        [Fact]
        public void ARowWithoutACompleteKeyIsToldByAllItShowsAndNeverLost()
        {
            const string who = "{'Date':'05-01','Name':'','Note':'who?','Seen':1}";
            // Untouched: it stays as the file has it now.
            var written = Save("[" + who + "," + Ben + "]", "[" + who + ",{'Date':'06-02','Name':'ben','Note':'x'}]",
                "{'birthdays':[{'Date':'05-01','Name':'','Note':'who?','Seen':2}," + Ben + "]}");
            Assert.Equal(2, written.Count);
            Assert.Equal(2, (int)written[0]["Seen"]);

            // Changed in the window: the old row goes, the new one stands where it stood.
            written = Save("[" + who + "," + Ben + "]", "[{'Date':'05-01','Name':'','Note':'aunt','Seen':1}," + Ben + "]", "{'birthdays':[" + who + "," + Ben + "]}");
            Assert.Equal("aunt,", Notes(written));

            // Changed by both: neither version is lost.
            written = Save("[" + who + "," + Ben + "]", "[{'Date':'05-01','Name':'','Note':'aunt','Seen':1}," + Ben + "]",
                "{'birthdays':[{'Date':'05-01','Name':'','Note':'uncle?','Seen':1}," + Ben + "]}");
            Assert.Equal("uncle?,aunt,", Notes(written));
        }

        [Fact]
        public void RowsOfOneKeyArePairedInTheirOrderAndNeverLost()
        {
            const string first = "{'Date':'05-01','Name':'anna','Note':'1'}", second = "{'Date':'05-01','Name':'anna','Note':'2'}";
            // Both survive a save that changed another row, beside the row the project added.
            var written = Save("[" + first + "," + second + "," + Ben + "]", "[" + first + "," + second + ",{'Date':'06-02','Name':'ben','Note':'x'}]",
                "{'birthdays':[" + first + "," + second + "," + Ben + "," + Cleo + "]}");
            Assert.Equal("1,2,x,from chat", Notes(written));

            // A change to the second lands on the second.
            written = Save("[" + first + "," + second + "]", "[" + first + ",{'Date':'05-01','Name':'anna','Note':'2b'}]", "{'birthdays':[" + first + "," + second + "," + Cleo + "]}");
            Assert.Equal("1,2b,from chat", Notes(written));

            // The project removed one of them meanwhile: one is left, as the file has it.
            written = Save("[" + first + "," + second + "," + Ben + "]", "[" + first + "," + second + ",{'Date':'06-02','Name':'ben','Note':'x'}]",
                "{'birthdays':[" + second + "," + Ben + "]}");
            Assert.Equal("2,x", Notes(written));

            // A third row of that key added in the window is added.
            written = Save("[" + first + "," + second + "]", "[" + first + "," + second + ",{'Date':'05-01','Name':'anna','Note':'3'}]", "{'birthdays':[" + first + "," + second + "]}");
            Assert.Equal("1,2,3", Notes(written));
        }

        [Fact]
        public void WhatIsNoRowOfTheWindowsStaysInTheFile()
        {
            // An entry that is no object, and a row with nothing in a column, are in no editor: they are the project's.
            var written = Save("[" + Anna + "]", "[{'Date':'05-01','Name':'anna','Note':'cake'}]", "{'birthdays':['a note'," + Anna + ",7,{'Stamp':1}]}");
            Assert.Equal(4, written.Count);
            Assert.Equal("a note", (string)written[0]);
            Assert.Equal("cake", (string)written[1]["Note"]);
            Assert.Equal(7, (int)written[2]);
            Assert.Equal(1, (int)written[3]["Stamp"]);
        }

        [Fact]
        public void WithoutARowKeyOrWithoutWhatTheWindowLoadedTheListIsPutOverTheFilesAsBefore()
        {
            string file = "{'birthdays':[" + Anna + "," + Cleo + "]}";
            var noKey = new Setting { Key = "birthdays", Kind = "list", File = "birthdays.json", Field = "birthdays", Columns = Columns };
            Assert.Equal("anna", Names(Save("[" + Anna + "]", "[" + Anna + "]", file, noKey)));

            File.WriteAllText(ListPath, JToken.Parse(file).ToString());
            Sidecars.Split(new JObject { ["birthdays"] = Rows("[" + Anna + "]") }, new[] { Birthdays }, SettingsPath, new HashSet<string> { "birthdays" });
            Assert.Equal("anna", Names((JArray)JObject.Parse(File.ReadAllText(ListPath))["birthdays"]));
        }

        [Fact]
        public void AFileOrAPropertyThatIsNotThereHasNothingToMergeWith()
        {
            var loaded = new Dictionary<string, JArray> { { "birthdays", Rows("[" + Anna + "," + Ben + "]") } };
            // No file (the project deleted it, or never wrote one): the window's rows are written.
            Sidecars.Split(new JObject { ["birthdays"] = Rows("[" + Anna + "]") }, new[] { Birthdays }, SettingsPath, new HashSet<string> { "birthdays" }, null, loaded);
            Assert.Equal("anna", Names((JArray)JObject.Parse(File.ReadAllText(ListPath))["birthdays"]));

            // A file without the property: the same, and what else it holds stays.
            File.WriteAllText(ListPath, "{ \"other\": 1 }");
            Sidecars.Split(new JObject { ["birthdays"] = Rows("[" + Ben + "]") }, new[] { Birthdays }, SettingsPath, new HashSet<string> { "birthdays" }, null, loaded);
            var root = JObject.Parse(File.ReadAllText(ListPath));
            Assert.Equal("ben", Names((JArray)root["birthdays"]));
            Assert.Equal(1, (int)root["other"]);
        }

        // ---- What the editor hands the merge ---------------------------------------------------------------------

        private static List<TextBox> Cells(ListEditor editor, string label)
        {
            var boxes = new List<TextBox>();
            FieldBinding.VisitTree(editor, node => { if (node is TextBox box && AutomationProperties.GetName(box) == label) boxes.Add(box); });
            return boxes;
        }

        private static List<Button> Buttons(DependencyObject root)
        {
            var buttons = new List<Button>();
            FieldBinding.VisitTree(root, node => { if (node is Button button) buttons.Add(button); });
            return buttons;
        }

        private static bool HasButton(DependencyObject root, string text) =>
            Buttons(root).Any(b => b.Content as string == text || b.ToolTip as string == text || AutomationProperties.GetName(b) == text);

        private static bool ShowsText(DependencyObject root, string text)
        {
            bool found = false;
            FieldBinding.VisitTree(root, node => { if (node is TextBlock block && block.Text == text && block.Visibility == Visibility.Visible) found = true; });
            return found;
        }

        [Fact]
        public void AListTheProjectFillsWritesBackWhatTheUserDidNotChangeAsItWasLoaded() => Sta(() =>
        {
            // Blanks around a text, a number as text, no Note at all, a property no column shows.
            var loaded = Rows("[{'Date':' 05-01 ','Name':'anna','Seen':3,'Tags':['a','b']},{'Date':501,'Name':'ben','Note':''}]");
            var editor = new ListEditor("birthdays", "Birthdays", Columns, loaded, null, "", null, false, true);
            Assert.True(JToken.DeepEquals(loaded, editor.Value), editor.Serialized);
            Assert.True(JToken.DeepEquals(loaded, editor.LoadedRows));

            Cells(editor, "Note")[0].Text = "cake";
            var value = editor.Value;
            Assert.Equal("cake", (string)value[0]["Note"]);
            Assert.Equal(" 05-01 ", (string)value[0]["Date"]);
            Assert.Equal(3, (int)value[0]["Seen"]);
            Assert.True(JToken.DeepEquals(loaded[1], value[1]));
            Assert.True(JToken.DeepEquals(loaded, editor.LoadedRows));   // what it loaded, not what it holds

            // So a save finds one changed cell and nothing else to put over the project's rows.
            var merged = ListMerge.Merge(editor.LoadedRows, value, Rows("[{'Date':' 05-01 ','Name':'anna','Seen':4,'Tags':['a','b','c']},{'Date':501,'Name':'ben','Note':'p'}]"), Birthdays);
            Assert.Equal("cake", (string)merged[0]["Note"]);
            Assert.Equal(4, (int)merged[0]["Seen"]);
            Assert.Equal(3, ((JArray)merged[0]["Tags"]).Count);
            Assert.Equal("p", (string)merged[1]["Note"]);

            // A list of the user's own is written as before: a cell per column, typed by its kind.
            var plain = new ListEditor("words", "Words", Columns, loaded, null, "");
            Assert.Null(plain.Value[0]["Seen"]);
            Assert.Equal("05-01", (string)plain.Value[0]["Date"]);
            Assert.Equal("", (string)plain.Value[0]["Note"]);
        });

        [Fact]
        public void AfterASaveTheEditorShowsWhatWasWrittenAndThatIsWhatItLoaded() => Sta(() =>
        {
            var editor = new ListEditor("birthdays", "Birthdays", Columns, Rows("[" + Anna + "," + Ben + "]"), null, "", null, false, true);
            int changes = 0;
            editor.Changed += () => changes++;

            // Restore defaults, a reset, an action's set: the rows change, what was loaded does not.
            editor.Set(Rows("[" + Anna + "]"));
            Assert.Equal("anna,ben", Names(editor.LoadedRows));

            editor.Reload(Rows("[" + Anna + "," + Cleo + "]"));
            Assert.Equal("anna,cleo", Names(editor.Value));
            Assert.Equal("anna,cleo", Names(editor.LoadedRows));

            // Written as it stands here: nothing is rebuilt.
            changes = 0;
            editor.Reload(editor.Value);
            Assert.Equal(0, changes);
        });

        // ---- fixedRows ---------------------------------------------------------------------------------------

        private static readonly List<SettingColumn> HolidayColumns = new List<SettingColumn>
        {
            new SettingColumn { Key = "Name", Label = "Name", ReadOnly = true },
            new SettingColumn { Key = "Global", Kind = "bool", Label = "Everywhere", ReadOnly = true },
            new SettingColumn { Key = "Counties", Label = "Regions", ReadOnly = true }
        };

        private static List<SettingColumn> With(List<SettingColumn> columns, params SettingColumn[] more) => columns.Concat(more).ToList();

        private const string Holidays = @"[
            { 'Name': 'New Year', 'Global': true, 'Counties': [] },
            { 'Name': 'Epiphany', 'Global': false, 'Counties': [ 'DE-BW', 'DE-BY', 'DE-ST' ] },
            { 'Name': 'Reformation Day', 'Global': false, 'Counties': [ 'DE-BB', 'DE-MV' ] } ]";

        [Fact]
        public void FixedRowsHaveNoButtonThatAddsOrRemovesARowInTheRowEditor() => Sta(() =>
        {
            var columns = With(HolidayColumns.Take(2).ToList(), new SettingColumn { Key = "Greeting", Label = "Greeting" });
            var rows = Rows("[{'Name':'New Year','Global':true,'Greeting':'Happy new year'},{'Name':'Epiphany','Global':false,'Greeting':''},{'Name':'','Greeting':''}]");
            var defaults = Rows("[{'Name':'New Year','Global':true,'Greeting':''}]");

            // As any list has them.
            var free = new ListEditor("holidays", "Holidays", columns, rows, defaults, "");
            Assert.True(HasButton(free, Text.T("list.add")));
            Assert.True(HasButton(free, Text.T("list.restore")));
            Assert.True(HasButton(free, Text.T("list.removeRow")));

            var editor = new ListEditor("holidays", "Holidays", columns, rows, defaults, "", null, fixedRows: true);
            Assert.Empty(Buttons(editor));
            // Every row it came with stays, the one with nothing in it too.
            Assert.Equal(3, editor.Value.Count);
            Assert.Equal(Text.T("list.count.many", 3), editor.CountText);

            // A cell of a column that is not read-only is edited; a read-only one is not.
            var greeting = Cells(editor, "Greeting")[1];
            Assert.False(greeting.IsReadOnly);
            Assert.True(Cells(editor, "Name")[1].IsReadOnly);
            int changes = 0;
            editor.Changed += () => changes++;
            greeting.Text = "Three kings";
            Assert.True(changes > 0);
            Assert.Equal("Three kings", (string)editor.Value[1]["Greeting"]);
            greeting.Text = "";
            Assert.Equal(3, editor.Value.Count);

            // Empty, it says who fills it, not "Add a row".
            var empty = new ListEditor("holidays", "Holidays", columns, new JArray(), null, "", null, fixedRows: true);
            Assert.True(ShowsText(empty, Text.T("list.fixed.empty")));
            Assert.False(ShowsText(empty, Text.T("list.empty")));
        });

        [Fact]
        public void FixedRowsHaveNoButtonThatAddsOrRemovesARowInTheTable() => Sta(() =>
        {
            var columns = With(HolidayColumns, new SettingColumn { Key = "Greeting", Label = "Greeting" });
            var defaults = Rows("[{'Name':'New Year','Global':true}]");
            var free = new ListEditor("holidays", "Holidays", columns, Rows(Holidays), defaults, "");
            Assert.True(HasButton(free, Text.T("list.add")));
            Assert.True(HasButton(free, Text.T("list.remove")));
            Assert.True(HasButton(free, Text.T("list.restore")));
            Assert.True(ShowsText(free, Text.T("list.table.hint.noSwitch")));

            var editor = new ListEditor("holidays", "Holidays", columns, Rows(Holidays), defaults, "", null, fixedRows: true);
            Assert.Empty(Buttons(editor));
            Assert.True(ShowsText(editor, Text.T("list.fixed.hint.noSwitch")));   // no word of Delete

            DataGrid grid = null;
            FieldBinding.VisitTree(editor, node => { if (node is DataGrid found) grid = found; });
            var row = grid.Items[1];
            var cell = row.GetType().GetProperty("Item");
            cell.SetValue(row, "Three kings", new object[] { "Greeting" });
            Assert.Equal("Three kings", (string)editor.Value[1]["Greeting"]);
            cell.SetValue(row, "Other", new object[] { "Name" });                  // read-only: not taken
            Assert.Equal("Epiphany", (string)editor.Value[1]["Name"]);
            Assert.Equal(3, editor.Value.Count);
        });

        [Fact]
        public void AResetLeavesAListWithFixedRowsAsItIs()
        {
            var settings = new[]
            {
                new Setting { Key = "bet", Kind = "int" },
                new Setting { Key = "holidays", Kind = "list", FixedRows = true },
                new Setting { Key = "words", Kind = "list" }
            };
            var current = JObject.Parse("{ 'bet': 50, 'holidays': [ { 'Name': 'New Year' } ], 'words': [ { 'Word': 'a' } ] }");
            var fresh = SettingsStore.ResetShown(current, JObject.Parse("{ 'bet': 10, 'holidays': [], 'words': [] }"), settings);
            Assert.Equal(10, (int)fresh["bet"]);
            Assert.Empty((JArray)fresh["words"]);
            Assert.Single((JArray)fresh["holidays"]);     // the defaults would remove the project's rows
        }

        // ---- filter ------------------------------------------------------------------------------------------

        private static bool Shows(string filter, string row, List<SettingColumn> columns = null, params (string Key, JToken Value)[] settings) =>
            ListFilter.Parse(filter).Shows(JObject.Parse(row), columns ?? HolidayColumns, key => settings.Where(s => s.Key == key).Select(s => s.Value).FirstOrDefault());

        private const string Epiphany = "{ 'Name': 'Epiphany', 'Global': false, 'Counties': [ 'DE-BW', 'DE-BY', 'DE-ST' ] }";

        [Fact]
        public void TheFilterComparesAsTextIgnoringCase()
        {
            Assert.True(Shows("Name=epiphany", Epiphany));
            Assert.False(Shows("Name=Epi", Epiphany));
            Assert.False(Shows("Name!=EPIPHANY", Epiphany));
            Assert.True(Shows("Name!=Easter", Epiphany));
            // A switch is true or false, also when the row has no value for it.
            Assert.True(Shows("Global=false", Epiphany));
            Assert.False(Shows("Global=true", Epiphany));
            Assert.True(Shows("Global=TRUE", "{ 'Name': 'New Year', 'Global': true }"));
            Assert.True(Shows("Global=false", "{ 'Name': 'Whit Monday' }"));
            Assert.True(Shows("Global!=true", "{ 'Name': 'Whit Monday' }"));
            // A number as its text.
            Assert.True(Shows("Year=2027", "{ 'Year': 2027 }", With(HolidayColumns, new SettingColumn { Key = "Year", Kind = "int" })));
        }

        [Fact]
        public void TheFilterContainsIsTextInATextAndAnElementOfAnArray()
        {
            Assert.True(Shows("Name~PIPH", Epiphany));
            Assert.False(Shows("Name~easter", Epiphany));
            // An array: one element equals the value. "DE-B" is part of two of them and equal to none.
            Assert.True(Shows("Counties~de-by", Epiphany));
            Assert.False(Shows("Counties~DE-B", Epiphany));
            Assert.False(Shows("Counties~DE-HH", Epiphany));
            // The same regions kept as one text: contained.
            Assert.True(Shows("Counties~DE-B", "{ 'Name': 'Epiphany', 'Counties': 'DE-BW, DE-BY' }"));
        }

        [Fact]
        public void AnEmptyValueComparesWithEmpty()
        {
            foreach (var none in new[] { "{ 'Name': 'x' }", "{ 'Name': 'x', 'Counties': [] }", "{ 'Name': 'x', 'Counties': '' }", "{ 'Name': 'x', 'Counties': null }" })
            {
                Assert.True(Shows("Counties=", none), none);
                Assert.True(Shows("Counties~", none), none);
                Assert.False(Shows("Counties!=", none), none);
            }
            Assert.False(Shows("Counties=", Epiphany));
            Assert.False(Shows("Counties~", Epiphany));
            Assert.True(Shows("Counties!=", Epiphany));
            // {key} of a setting that holds nothing is such an empty value.
            Assert.False(Shows("Counties~{region}", Epiphany, null, ("region", "")));
            Assert.True(Shows("Counties~{region}", "{ 'Name': 'x', 'Counties': [] }", null, ("region", "")));
        }

        [Fact]
        public void TheLeftSideIsAColumnWhenTheListHasOneOfThatNameElseASetting()
        {
            // A setting: its value now. One of them holding is enough.
            Assert.True(Shows("mode=all", Epiphany, null, ("mode", "ALL")));
            Assert.False(Shows("mode=all", Epiphany, null, ("mode", "some")));
            Assert.True(Shows("mode!=all", Epiphany, null, ("mode", "some")));
            Assert.True(Shows("showAll=true", Epiphany, null, ("showAll", true)));
            Assert.False(Shows("showAll=true", Epiphany, null, ("showAll", false)));
            Assert.True(Shows("regions~DE-BY", Epiphany, null, ("regions", new JArray("DE-BY", "DE-HH"))));
            Assert.True(Shows("mode=all || Name=Easter || Name=Epiphany", Epiphany, null, ("mode", "some")));
            Assert.False(Shows("mode=all || Name=Easter", Epiphany, null, ("mode", "some")));
            // A column of that name comes first: the row's Name, not the setting "Name".
            Assert.False(Shows("Name=Easter", Epiphany, null, ("Name", "Easter")));
            Assert.True(Shows("Name=Epiphany", Epiphany, null, ("Name", "Easter")));
        }

        [Fact]
        public void KeyInBracesStandsForThatSettingsValue()
        {
            const string filter = "region= || Global=true || Counties~{region}";
            Assert.True(Shows(filter, Epiphany, null, ("region", "")));                // no region picked: every row
            Assert.True(Shows(filter, Epiphany, null, ("region", "DE-BY")));
            Assert.False(Shows(filter, Epiphany, null, ("region", "DE-HH")));
            Assert.True(Shows(filter, "{ 'Name': 'New Year', 'Global': true, 'Counties': [] }", null, ("region", "DE-HH")));
            // Inside a literal, and more than one.
            Assert.True(Shows("Counties~DE-{state}", Epiphany, null, ("state", "by")));
            Assert.True(Shows("Name={a}{b}", Epiphany, null, ("a", "Epi"), ("b", "phany")));
            Assert.Equal(new[] { "region" }, ListFilter.Parse(filter).Settings(HolidayColumns).ToArray());
            Assert.Null(ListFilter.Parse("  "));
        }

        [Fact]
        public void AConditionThatCannotBeReadHoldsSoTheRowsShow()
        {
            Assert.True(Shows("nonsense", Epiphany));                    // no operator
            Assert.True(Shows("Nope=1", Epiphany));                      // neither a column nor a setting
            Assert.True(Shows("Name={nope}", Epiphany));                 // {key} is no setting
            Assert.False(Shows("Name=Easter", Epiphany));
        }

        [Fact]
        public void TheRowEditorShowsTheRowsTheFilterLetsThroughAndSavesTheOthersBackUnchanged() => Sta(() =>
        {
            var loaded = Rows(Holidays);
            ((JObject)loaded[2])["Source"] = "nager.date";      // a property no column shows, on a row that will not show
            string region = "";
            var filter = ListFilter.Parse("region= || Global=true || Counties~{region}");
            var editor = new ListEditor("holidays", "Holidays", HolidayColumns, loaded, null, "", null, fixedRows: true);
            int changes = 0;
            editor.Changed += () => changes++;
            editor.RowFilter = row => filter.Shows(row, HolidayColumns, key => key == "region" ? (JToken)region : null);

            Assert.Equal("New Year,Epiphany,Reformation Day", Names(editor.ShownRows));
            Assert.Equal(Text.T("list.count.many", 3), editor.CountText);

            region = "DE-BY";
            Assert.Equal(3, editor.ShownRows.Count);            // not before it is applied again
            editor.Refilter();
            Assert.Equal("New Year,Epiphany", Names(editor.ShownRows));
            Assert.Equal(Text.T("list.count.many", 2), editor.CountText);
            // The row that does not show is still in the list, as it was loaded; showing or not is no change.
            Assert.True(JToken.DeepEquals(loaded, editor.Value), editor.Serialized);
            Assert.Equal(0, changes);

            // The search and its count work on the rows shown.
            editor.SearchText = "epi";
            Assert.Equal("Epiphany", Names(editor.ShownRows));
            Assert.Equal(Text.T("list.count.shown", 1, 2), editor.CountText);
            editor.SearchText = "reformation";
            Assert.Empty(editor.ShownRows);
            Assert.Equal(Text.T("list.count.shown", 0, 2), editor.CountText);
            editor.SearchText = "";

            region = "DE-MV";
            editor.Refilter();
            Assert.Equal("New Year,Reformation Day", Names(editor.ShownRows));
            region = "DE-HH";
            editor.Refilter();
            Assert.Equal(Text.T("list.count.one"), editor.CountText);

            // No row fits: said, and nothing is gone.
            editor.RowFilter = row => false;
            Assert.Empty(editor.ShownRows);
            Assert.True(ShowsText(editor, Text.T("list.filter.none")));
            Assert.True(JToken.DeepEquals(loaded, editor.Value));
            editor.RowFilter = null;
            Assert.Equal(3, editor.ShownRows.Count);
            Assert.False(ShowsText(editor, Text.T("list.filter.none")));
        });

        [Fact]
        public void TheTableShowsTheRowsTheFilterLetsThroughAndSavesTheOthersBackUnchanged() => Sta(() =>
        {
            var columns = With(HolidayColumns, new SettingColumn { Key = "Greeting", Label = "Greeting" });
            var loaded = Rows(Holidays);
            string region = "DE-BY";
            var filter = ListFilter.Parse("region= || Global=true || Counties~{region}");
            var editor = new ListEditor("holidays", "Holidays", columns, loaded, null, "", null, fixedRows: true);
            editor.RowFilter = row => filter.Shows(row, columns, key => key == "region" ? (JToken)region : null);

            Assert.Equal("New Year,Epiphany", Names(editor.ShownRows));
            Assert.Equal(Text.T("list.count.many", 2), editor.CountText);
            Assert.True(JToken.DeepEquals(loaded, editor.Value), editor.Serialized);

            // A cell edited in a row that shows; the row that does not show is saved back with it.
            DataGrid grid = null;
            FieldBinding.VisitTree(editor, node => { if (node is DataGrid found) grid = found; });
            Assert.Equal(2, grid.Items.Count);
            var row = grid.Items[1];
            row.GetType().GetProperty("Item").SetValue(row, "Three kings", new object[] { "Greeting" });
            var value = editor.Value;
            Assert.Equal("Three kings", (string)value[1]["Greeting"]);
            Assert.True(JToken.DeepEquals(loaded[2], value[2]));

            editor.SearchText = "new";
            Assert.Equal("New Year", Names(editor.ShownRows));
            Assert.Equal(Text.T("list.count.shown", 1, 2), editor.CountText);
            editor.SearchText = "";

            region = "";
            editor.Refilter();
            Assert.Equal(3, editor.ShownRows.Count);
            Assert.Equal(Text.T("list.count.many", 3), editor.CountText);

            editor.RowFilter = r => false;
            Assert.True(ShowsText(editor, Text.T("list.filter.none")));
            Assert.Equal(3, editor.Value.Count);
        });

        [Fact]
        public void TheFilterIsAppliedAgainWhenASettingItNamesChanges() => Sta(() =>
        {
            var parts = new WindowParts();
            var defaults = JObject.Parse("{ 'region': '', 'everything': false, 'saved': 'DE-MV' }");
            var fields = new Fields(parts, (JObject)defaults.DeepClone(), defaults, _ => { });
            var page = new StackPanel();
            page.Children.Add(fields.TextField("Region", "region", ""));
            page.Children.Add(fields.Toggle("Everything", "everything", ""));

            var editor = new ListEditor("holidays", "Holidays", HolidayColumns, Rows(Holidays), null, "", null, fixedRows: true);
            parts.Lists["holidays"] = editor;
            page.Children.Add(editor);
            var baseline = new Dictionary<object, string>();
            FieldBinding.Snapshot(page, baseline);
            ListFilter.Parse("region= || everything=true || Global=true || Counties~{region}").Wire(fields, parts, HolidayColumns, editor);
            Assert.Equal(3, editor.ShownRows.Count);

            // The value as the field has it now, unsaved.
            parts.Boxes["region"].Text = "DE-BY";
            Assert.Equal("New Year,Epiphany", Names(editor.ShownRows));
            parts.Boxes["region"].Text = "DE-MV";
            Assert.Equal("New Year,Reformation Day", Names(editor.ShownRows));
            parts.Switches["everything"].IsChecked = true;
            Assert.Equal(3, editor.ShownRows.Count);
            parts.Switches["everything"].IsChecked = false;
            Assert.Equal(2, editor.ShownRows.Count);
            // The list itself did not change by any of it.
            Assert.DoesNotContain("holidays", FieldBinding.ChangedKeys(page, baseline));

            // A setting the window has no field for: its saved value.
            var other = new ListEditor("more", "More", HolidayColumns, Rows(Holidays), null, "", null, fixedRows: true);
            ListFilter.Parse("Counties~{saved}").Wire(fields, parts, HolidayColumns, other);
            Assert.Equal("Reformation Day", Names(other.ShownRows));
        });

        // ---- An array in a read-only cell ------------------------------------------------------------------------

        [Fact]
        public void AnArrayInAReadOnlyCellShowsItsElementsAndIsWrittenBackAsTheArrayItWas() => Sta(() =>
        {
            // The row editor, in a list of the user's own as well.
            var editor = new ListEditor("holidays", "Holidays", HolidayColumns, Rows(Holidays), null, "");
            var regions = Cells(editor, "Regions");
            Assert.Equal("", regions[0].Text);
            Assert.Equal("DE-BW, DE-BY, DE-ST", regions[1].Text);
            Assert.True(regions[1].IsReadOnly);
            var value = editor.Value;
            Assert.True(JToken.DeepEquals(new JArray("DE-BW", "DE-BY", "DE-ST"), value[1]["Counties"]), editor.Serialized);
            Assert.True(JToken.DeepEquals(new JArray(), value[0]["Counties"]));
            // Found by what it shows.
            editor.SearchText = "de-by";
            Assert.Equal("Epiphany", Names(editor.ShownRows));

            // The table.
            var columns = With(HolidayColumns, new SettingColumn { Key = "Greeting", Label = "Greeting" });
            var table = new ListEditor("holidays", "Holidays", columns, Rows(Holidays), null, "");
            DataGrid grid = null;
            FieldBinding.VisitTree(table, node => { if (node is DataGrid found) grid = found; });
            var row = grid.Items[1];
            var cell = row.GetType().GetProperty("Item");
            Assert.Equal("DE-BW, DE-BY, DE-ST", cell.GetValue(row, new object[] { "Counties" }));
            cell.SetValue(row, "Three kings", new object[] { "Greeting" });
            Assert.True(JToken.DeepEquals(new JArray("DE-BW", "DE-BY", "DE-ST"), table.Value[1]["Counties"]), table.Serialized);
            table.SearchText = "de-mv";
            Assert.Equal("Reformation Day", Names(table.ShownRows));
        });

        // ---- What a declaration gets wrong -----------------------------------------------------------------------

        private List<string> Problems(string settings)
        {
            try
            {
                var schema = Schema.Parse(@"{ ""host"": ""demo"", ""settings"": [ " + settings.Replace('\'', '"') + " ] }");
                var spec = Entry.SpecFor(schema, SettingsPath);
                Entry.Prepare(spec, schema);
                return spec.Problems.ToList();
            }
            finally { Text.Use(Conventions.Default.Lang); }
        }

        private const string Region = "{ 'key': 'region', 'default': '' }, ";
        private const string HolidayList = "'kind': 'list', 'columns': [ 'Date', 'Name', 'Global|bool|Everywhere|readonly', 'Counties|string|Regions|readonly' ]";

        [Fact]
        public void ADeclarationThatWorksNamesNoProblem()
        {
            var problems = Problems(Region + "{ 'key': 'officialHolidays', 'default': [ { 'Date': '01-01', 'Name': 'New Year' }, { 'Date': '01-06', 'Name': 'Epiphany' }, { 'Date': '', 'Name': '' }, { 'Date': '', 'Name': '' } ], "
                + HolidayList + ", 'file': 'holidays.json', 'field': 'officialHolidays', 'rowKey': 'Date,Name', 'fixedRows': true, 'filter': 'region= || Global=true || Counties~{region}' }");
            Assert.DoesNotContain(problems, p => p.Contains("officialHolidays"));
        }

        [Fact]
        public void RowKeyFixedRowsAndFilterBelongToAList()
        {
            var problems = Problems(Region + "{ 'key': 'luckyNumber', 'default': 5, 'rowKey': 'Date', 'fixedRows': true, 'filter': 'region=' }");
            Assert.Contains("'luckyNumber' has a rowKey, which only a list has (kind list); it does nothing on an int field.", problems);
            Assert.Contains("'luckyNumber' has fixedRows, which only a list has (kind list); it does nothing on an int field.", problems);
            Assert.Contains("'luckyNumber' has a filter, which only a list has (kind list); it does nothing on an int field.", problems);
        }

        [Fact]
        public void ARowKeyNamesColumnsOfTheListAndNoTwoDefaultRowsShareAKey()
        {
            var problems = Problems("{ 'key': 'holidays', 'default': [], " + HolidayList + ", 'rowKey': 'Date, Nmae' }");
            var line = Assert.Single(problems, p => p.Contains("holidays"));
            Assert.StartsWith("'holidays' has the rowKey 'Date, Nmae', but the list has no column 'Nmae'.", line);

            problems = Problems("{ 'key': 'holidays', 'default': [ { 'Date': '01-01', 'Name': 'New Year' }, { 'Date': '01-06', 'Name': 'Epiphany' }, { 'Date': ' 01-01', 'Name': 'New Year', 'Global': true } ], "
                + HolidayList + ", 'rowKey': 'Date,Name' }");
            line = Assert.Single(problems, p => p.Contains("holidays"));
            Assert.StartsWith("'holidays' has two default rows with the key '01-01, New Year' (rowKey 'Date,Name')", line);
        }

        [Fact]
        public void AFilterConditionHasAnOperatorAColumnOrSettingOnTheLeftAndOnlySettingsInBraces()
        {
            var problems = Problems(Region + "{ 'key': 'holidays', 'default': [], " + HolidayList + ", 'filter': 'region || Regoin=x || Counties~{regoin} || global=true || region= || Counties~{region}' }")
                .Where(p => p.Contains("holidays")).ToList();
            Assert.Equal(4, problems.Count);
            Assert.StartsWith("'holidays' has the filter condition 'region', which has no operator (=, != or ~).", problems[0]);
            Assert.StartsWith("'holidays' filters by 'Regoin' in 'Regoin=x', which is neither a column of the list nor a setting.", problems[1]);
            Assert.StartsWith("'holidays' has {regoin} in its filter condition 'Counties~{regoin}', which is not a setting.", problems[2]);
            Assert.StartsWith("'holidays' filters by 'global' in 'global=true'", problems[3]);   // a column's name as it is written: Global
        }

        // ---- The window: a save while the project writes ---------------------------------------------------------

        private const string WindowSchema = @"{ ""host"": ""demo"", ""version"": ""v1"", ""settingsFile"": ""x"", ""settings"": [
            { ""key"": ""bet"", ""default"": 10, ""page"": ""game"", ""label"": ""Bet"" },
            { ""key"": ""birthdays"", ""default"": [], ""kind"": ""list"", ""file"": ""birthdays.json"", ""field"": ""birthdays"", ""rowKey"": ""Date,Name"",
              ""page"": ""game"", ""label"": ""Birthdays"", ""columns"": [ ""Date"", ""Name"", ""Note"" ] },
            { ""key"": ""tags"", ""default"": [], ""kind"": ""list"", ""rowKey"": ""Tag"", ""page"": ""game"", ""label"": ""Tags"", ""columns"": [ ""Tag"", ""Note"" ] } ],
            ""window"": { ""title"": ""Demo"", ""pages"": [ ""game"", { ""id"": ""probe"", ""title"": ""Probe"", ""custom"": true } ] } }";

        private static void Write(string path, string json) => File.WriteAllText(path, JToken.Parse(json).ToString());
        private static JArray Read(string path, string property) => (JArray)JObject.Parse(File.ReadAllText(path))[property];

        [Fact]
        public void ASaveInTheWindowMergesWithWhatTheProjectWroteWhileItWasOpen()
        {
            using (var w = new Opened(WindowSchema, folder =>
            {
                Write(Path.Combine(folder, "birthdays.json"), "{'birthdays':[" + Anna + "," + Ben + "]}");
                Write(Path.Combine(folder, "settings.json"), "{'bet':10,'tags':[{'Tag':'a','Note':''}]}");
            }))
            {
                string file = Path.Combine(w.Folder, "birthdays.json");
                ListEditor Editor() => w.Context.Parts.Lists["birthdays"];
                Assert.Equal("anna,ben", w.On(() => Names(Editor().Value)));

                // The project adds a birthday from chat and a tag; the user changes a note and adds a tag.
                Write(file, "{'birthdays':[" + Anna + "," + Ben + "," + Cleo + "]}");
                Write(w.SettingsPath, "{'bet':10,'tags':[{'Tag':'a','Note':''},{'Tag':'b','Note':'by the project'}]}");
                w.On(() => Editor().Set(Rows("[{'Date':'05-01','Name':'anna','Note':'cake'}," + Ben + "]")));
                w.On(() => w.Context.Parts.Lists["tags"].Set(Rows("[{'Tag':'a','Note':''},{'Tag':'u','Note':'mine'}]")));
                w.On(() => w.Click(Text.T("btn.save")));

                Assert.Equal("anna,ben,cleo", Names(Read(file, "birthdays")));
                Assert.Equal("cake,,from chat", Notes(Read(file, "birthdays")));
                // A list kept in the settings file is merged the same way.
                Assert.Equal("a,u,b", string.Join(",", Read(w.SettingsPath, "tags").Select(t => (string)t["Tag"])));
                Assert.Null(JObject.Parse(File.ReadAllText(w.SettingsPath))["birthdays"]);
                // The window shows what was written, and it counts as saved.
                Assert.Equal("anna,ben,cleo", w.On(() => Names(Editor().Value)));
                Assert.Equal("a,u,b", w.On(() => string.Join(",", w.Context.Parts.Lists["tags"].Value.Select(t => (string)t["Tag"]))));
                Assert.True(w.On(() => w.Shows(Text.T("status.saved"))), "the merged lists count as unsaved");

                // Again: the project removes a row and adds one, the user changes the row the project added before.
                Write(file, "{'birthdays':[{'Date':'05-01','Name':'anna','Note':'cake'}," + Cleo + ",{'Date':'08-04','Name':'dora','Note':''}]}");
                w.On(() => Editor().Set(Rows("[{'Date':'05-01','Name':'anna','Note':'cake'}," + Ben + ",{'Date':'07-03','Name':'cleo','Note':'thanks'}]")));
                w.On(() => w.Click(Text.T("btn.save")));
                Assert.Equal("anna,cleo,dora", Names(Read(file, "birthdays")));
                Assert.Equal("cake,thanks,", Notes(Read(file, "birthdays")));
                Assert.Equal("anna,cleo,dora", w.On(() => Names(Editor().Value)));

                // A save that did not change the list leaves the file to the project.
                Write(file, "{'birthdays':[" + Ben + "]}");
                w.On(() => w.Box("bet").Text = "20");
                w.On(() => w.Click(Text.T("btn.save")));
                Assert.Equal("ben", Names(Read(file, "birthdays")));
            }
        }

        [Fact]
        public void ResetAllKeepsTheUnsavedRowsAndTheRowsTheProjectAddedMeanwhile()
        {
            using (var w = new Opened(WindowSchema, folder => Write(Path.Combine(folder, "birthdays.json"), "{'birthdays':[" + Anna + "," + Ben + "]}")))
            {
                string file = Path.Combine(w.Folder, "birthdays.json");
                Write(file, "{'birthdays':[" + Anna + "," + Ben + "," + Cleo + "]}");
                w.On(() => w.Context.Parts.Lists["birthdays"].Set(Rows("[{'Date':'05-01','Name':'anna','Note':'cake'}," + Ben + "]")));

                var done = w.Later(() => w.Click(Text.T("tip.resetAll"), byTip: true));
                var question = w.Dialog(5000);
                Assert.NotEqual(IntPtr.Zero, question);
                Opened.Answer(question, Opened.IdYes);
                Assert.True(done.Wait(10000));

                // Still unsaved, over the list as the file has it now: a save does not take the project's row away.
                Assert.Equal("anna,ben,cleo", w.On(() => Names(w.Context.Parts.Lists["birthdays"].Value)));
                Assert.Equal(",,from chat", Notes(Read(file, "birthdays")));
                w.On(() => w.Click(Text.T("btn.save")));
                Assert.Equal("anna,ben,cleo", Names(Read(file, "birthdays")));
                Assert.Equal("cake,,from chat", Notes(Read(file, "birthdays")));
            }
        }

        /// <summary>The window, opened as a project opens it (as in Fixes1100ShellTests): a custom page hands the test
        /// each build's context, and with it the window's thread and controls.</summary>
        private sealed class Opened : NoHooks, IDisposable
        {
            public readonly string Folder;
            public readonly string SettingsPath;
            private readonly Thread _runner;
            private readonly ManualResetEventSlim _built = new ManualResetEventSlim();
            private volatile PageContext _context;
            private FrameworkElement _probe;
            private uint _thread;

            public Opened(string schemaJson, Action<string> files = null)
            {
                Folder = Path.Combine(Path.GetTempPath(), "UIFramework.Tests", "plist-window-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(Folder);
                SettingsPath = Path.Combine(Folder, "settings.json");
                files?.Invoke(Folder);
                var schema = UIFramework.Schema.Parse(schemaJson);
                var spec = Entry.SpecFor(schema, SettingsPath);
                _runner = new Thread(() => { try { Entry.Show(spec, schema, new QuietLog(), this); } catch (Exception) { } }) { IsBackground = true };
                _runner.Start();
                if (!_built.Wait(15000))
                    throw new TimeoutException("The window was not built.");
                Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            }

            public PageContext Context => _context;
            public Dispatcher Dispatcher => _probe.Dispatcher;

            public override FrameworkElement BuildPage(string pageId, PageContext context)
            {
                _context = context;
                _probe = new TextBlock { Text = "probe" };
                _thread = GetCurrentThreadId();
                _built.Set();
                return _probe;
            }

            public T On<T>(Func<T> body) => Dispatcher.Invoke(body);
            public void On(Action body) => Dispatcher.Invoke(body);

            public ManualResetEventSlim Later(Action body)
            {
                var done = new ManualResetEventSlim();
                Dispatcher.BeginInvoke(new Action(() => { try { body(); } finally { done.Set(); } }));
                return done;
            }

            public TextBox Box(string key) => _context.Parts.Boxes[key];

            private Window Window
            {
                get
                {
                    DependencyObject node = _probe;
                    while (node != null && !(node is Window))
                        node = LogicalTreeHelper.GetParent(node) ?? (node is System.Windows.Media.Visual ? System.Windows.Media.VisualTreeHelper.GetParent(node) : null);
                    return (Window)node;
                }
            }

            /// <summary>A button by its text or its tooltip, clicked as the mouse would. On the window's thread.</summary>
            public void Click(string text, bool byTip = false)
            {
                var button = All(Window).OfType<Button>().First(b => byTip ? b.ToolTip as string == text : b.Content as string == text);
                button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button));
            }

            public bool Shows(string text) => All(Window).OfType<TextBlock>().Any(t => t.Text == text);

            private static IEnumerable<object> All(object node)
            {
                yield return node;
                if (node is DependencyObject d)
                    foreach (var child in LogicalTreeHelper.GetChildren(d))
                        foreach (var below in All(child))
                            yield return below;
            }

            /// <summary>A message box of the window's thread, waited for up to <paramref name="ms"/>; zero when none came.</summary>
            public IntPtr Dialog(int ms)
            {
                var watch = Stopwatch.StartNew();
                do
                {
                    IntPtr found = IntPtr.Zero;
                    EnumThreadWindows(_thread, (hwnd, l) =>
                    {
                        var name = new StringBuilder(64);
                        GetClassName(hwnd, name, name.Capacity);
                        if (name.ToString() != "#32770") return true;
                        found = hwnd;
                        return false;
                    }, IntPtr.Zero);
                    if (found != IntPtr.Zero) return found;
                    Thread.Sleep(20);
                }
                while (watch.ElapsedMilliseconds < ms);
                return IntPtr.Zero;
            }

            public const int IdYes = 6, IdNo = 7;

            public static void Answer(IntPtr dialog, int button) => PostMessage(dialog, 0x0111 /* WM_COMMAND */, (IntPtr)button, IntPtr.Zero);

            public void Dispose()
            {
                try
                {
                    Shell.Close(SettingsPath);
                    var watch = Stopwatch.StartNew();
                    while (_runner.IsAlive && watch.ElapsedMilliseconds < 15000)
                    {
                        // A question a failed test left open is answered, so the window can go.
                        var dialog = Dialog(0);
                        if (dialog != IntPtr.Zero)
                        {
                            PostMessage(dialog, 0x0010 /* WM_CLOSE */, IntPtr.Zero, IntPtr.Zero);
                            Answer(dialog, IdNo);
                        }
                        Thread.Sleep(50);
                    }
                }
                finally
                {
                    Text.Use(Conventions.Default.Lang);
                    try { Directory.Delete(Folder, true); } catch (Exception) { }
                }
            }

            private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);
            [DllImport("user32.dll")] private static extern bool EnumThreadWindows(uint threadId, EnumWindowsProc callback, IntPtr lParam);
            [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hwnd, StringBuilder name, int max);
            [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
            [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
        }

        private sealed class QuietLog : IHostLog
        {
            public void Info(string message) { }
            public void Warn(string message) { }
            public void Error(string message) { }
        }
    }
}
