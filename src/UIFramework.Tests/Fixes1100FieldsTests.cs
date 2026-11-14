using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Controls;
using Newtonsoft.Json.Linq;
using Xunit;

namespace UIFramework.Tests
{
    /// <summary>1.10.0: what docs/Schema.md promised of two kinds and the code lacked. A path field's folder button
    /// opens a file's folder, and a browse button beside it chooses a file or a folder with Windows' dialog.</summary>
    public class Fixes1100FieldsTests
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

        // ---- path: the folder button --------------------------------------------------------------------------

        private const string Bot = @"C:\Streamer.bot";

        // A Streamer.bot folder with an overlay folder in it and a words file without an extension.
        private static bool FolderThere(string path) => path == Bot || path == Bot + @"\overlay";
        private static bool FileThere(string path) => path == Bot + @"\data\WORDS";

        [Fact]
        public void TheFolderButtonOpensTheFolderAFileIsIn()
        {
            // A file the project writes, not there yet: its folder, not a folder named after the file.
            Assert.Equal(Bot + @"\overlay", PathPicker.FolderToOpen(@"overlay\state.json", Bot, FolderThere, FileThere));
            Assert.Equal(Bot + @"\overlay", PathPicker.FolderToOpen("overlay/state.json", Bot, FolderThere, FileThere));
            Assert.Equal(@"D:\games\scores", PathPicker.FolderToOpen("\"D:\\games\\scores\\today.txt\" ", Bot, FolderThere, FileThere));
            // A file that is there, whatever its name.
            Assert.Equal(Bot + @"\data", PathPicker.FolderToOpen(@"data\WORDS", Bot, FolderThere, FileThere));
            // A folder stays the folder, also one with a dot in its name that is there.
            Assert.Equal(Bot + @"\logs", PathPicker.FolderToOpen("logs", Bot, FolderThere, FileThere));
            Assert.Equal(Bot, PathPicker.FolderToOpen(Bot, @"D:\", FolderThere, FileThere));
            Assert.Null(PathPicker.FolderToOpen("  ", Bot, FolderThere, FileThere));
        }

        // ---- path: the browse button --------------------------------------------------------------------------

        private static PathPicker.Plan Plan(string value, string fallback = null) => PathPicker.For(value, fallback, Bot, FolderThere, FileThere);

        [Fact]
        public void AFileGetsAFileDialogInTheFolderItIsIn()
        {
            var plan = Plan(@"overlay\state.json");
            Assert.True(plan.File);
            Assert.Equal(Bot + @"\overlay", plan.Folder);
            Assert.Equal("state.json", plan.FileName);
            // A folder that is not there yet: the nearest one above it that is.
            plan = Plan(@"overlay\new\deeper\state.json");
            Assert.True(plan.File);
            Assert.Equal(Bot + @"\overlay", plan.Folder);
            // A drive with nothing of it there: the Streamer.bot folder.
            plan = Plan(@"Q:\x\scores.txt");
            Assert.True(plan.File);
            Assert.Equal(Bot, plan.Folder);
            Assert.Equal("scores.txt", plan.FileName);
            // A file that is there, without an extension.
            Assert.True(Plan(@"data\WORDS").File);
        }

        [Fact]
        public void AFolderGetsAFolderDialogAndTheDefaultSaysWhatTheSettingHolds()
        {
            var plan = Plan("overlay", "logs");
            Assert.False(plan.File);
            Assert.Equal(Bot + @"\overlay", plan.Folder);
            Assert.Null(plan.FileName);
            Assert.False(Plan(Bot).File);                                   // a folder that is there, a dot in its name
            Assert.Equal(Bot + @"\overlay", Plan(@"overlay\logs").Folder);  // not there yet: the one above it
            // Empty: the dialog starts where the default points.
            plan = Plan("", @"overlay\state.json");
            Assert.True(plan.File);
            Assert.Equal("state.json", plan.FileName);
            Assert.Equal(Bot + @"\overlay", plan.Folder);
            Assert.False(Plan(" ", "overlay").File);
            // A folder in a setting whose default is a file: a file dialog in that folder, the default's name filled in.
            plan = Plan("overlay", "state.json");
            Assert.True(plan.File);
            Assert.Equal(Bot + @"\overlay", plan.Folder);
            Assert.Equal("state.json", plan.FileName);
            // Nothing at all: a folder dialog in the Streamer.bot folder.
            plan = Plan("", null);
            Assert.False(plan.File);
            Assert.Equal(Bot, plan.Folder);
        }

        [Fact]
        public void AChoiceUnderTheStreamerBotFolderIsWrittenRelative()
        {
            Assert.Equal(@"overlay\state.json", PathPicker.Relative(Bot + @"\overlay\state.json", Bot + @"\"));
            Assert.Equal("logs", PathPicker.Relative(@"c:\streamer.bot\logs", Bot));          // the case of the letters does not matter
            Assert.Equal(@"C:\Streamer.botX\logs", PathPicker.Relative(@"C:\Streamer.botX\logs", Bot));   // a folder whose name only starts the same
            Assert.Equal(@"D:\games\scores.txt", PathPicker.Relative(@"D:\games\scores.txt", Bot));
            Assert.Equal(Bot, PathPicker.Relative(Bot, Bot));                                  // the folder itself stays a whole path, not ""
        }

        [Fact]
        public void APathFieldHasABrowseButtonBesideTheFolderButton() => Sta(() =>
        {
            var spec = Prepared(Head + @"{ ""key"": ""stateFile"", ""default"": ""overlay/state.json"", ""kind"": ""path"" } ] }");
            try
            {
                var ctx = Entry.ContextFor(spec, (JObject)spec.Defaults.DeepClone(), new WindowParts(), null, null);
                var field = (StackPanel)SchemaPage.Render(ctx, spec.ResolvedSettings.First(s => s.Key == "stateFile"));
                var dock = Assert.IsType<DockPanel>(field.Children[1]);
                var buttons = dock.Children.OfType<Button>().ToList();
                Assert.Equal(new[] { Text.T("field.path.open"), Text.T("field.path.browse") }, buttons.Select(b => (string)b.ToolTip));
                Assert.Equal(PathPicker.BrowseGlyph, buttons[1].Content);
                Assert.IsType<TextBox>(dock.Children[dock.Children.Count - 1]);   // the box takes the rest
                foreach (string code in new[] { "en", "de", "es" })
                {
                    var ui = Conventions.Load(code).Lang.Ui;
                    Assert.All(new[] { "field.path.browse", "field.path.pickFile", "field.path.pickFolder", "field.path.allFiles" }, key => Assert.True(ui.ContainsKey(key), code + " lacks " + key));
                }
            }
            finally { Text.Use(Conventions.Default.Lang); }
        });
    }
}
