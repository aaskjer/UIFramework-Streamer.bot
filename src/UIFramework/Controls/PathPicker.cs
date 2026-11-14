using System;
using System.IO;

namespace UIFramework
{
    /// <summary>
    /// What the button beside a path field does: it opens the folder the value names, or the folder a named file is
    /// in. A relative value counts from the Streamer.bot folder.
    /// </summary>
    public static class PathPicker
    {
        /// <summary>The value made absolute: without the spaces and quotes around it, a relative one joined to
        /// <paramref name="baseFolder"/>. Null for an empty value or one that is no path.</summary>
        public static string Absolute(string value, string baseFolder)
        {
            string path = (value ?? "").Trim().Trim('"').Trim();
            if (path.Length == 0) return null;
            try { return Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(baseFolder ?? "", path)); }
            catch (Exception) { return null; }   // characters no path may have
        }

        /// <summary>Whether an absolute path names a file: a folder that is there is a folder, a file that is there a
        /// file; what is not there yet is a file when its name has an extension ("state.json").</summary>
        public static bool NamesFile(string path, Func<string, bool> folderExists, Func<string, bool> fileExists)
        {
            if (string.IsNullOrEmpty(path) || folderExists(path)) return false;
            return fileExists(path) || Path.HasExtension(path);
        }

        /// <summary>The folder the folder button opens: the one the value names, or for a value that names a file the
        /// one the file is in. Null for an empty value.</summary>
        public static string FolderToOpen(string value, string baseFolder) => FolderToOpen(value, baseFolder, Directory.Exists, File.Exists);

        public static string FolderToOpen(string value, string baseFolder, Func<string, bool> folderExists, Func<string, bool> fileExists)
        {
            string path = Absolute(value, baseFolder);
            if (path == null) return null;
            return NamesFile(path, folderExists, fileExists) ? Path.GetDirectoryName(path) ?? path : path;
        }
    }
}
