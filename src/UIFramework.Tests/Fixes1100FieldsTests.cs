using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace UIFramework.Tests
{
    /// <summary>1.10.0: what docs/Schema.md promised of two kinds and the code lacked. A path field's folder button
    /// opens a file's folder.</summary>
    public class Fixes1100FieldsTests
    {
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
    }
}
