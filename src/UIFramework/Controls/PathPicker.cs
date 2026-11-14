using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace UIFramework
{
    /// <summary>
    /// What the buttons beside a path field do. The folder button opens the folder the value names, or the folder a
    /// named file is in; the browse button lets the user choose with Windows' own dialog, a file dialog for a file and a
    /// folder dialog for a folder, starting where the value points. A relative value counts from the Streamer.bot
    /// folder, and a choice under that folder is written relative to it.
    /// </summary>
    public static class PathPicker
    {
        /// <summary>The glyph of the browse button: three dots, as Windows draws "choose...".</summary>
        public const string BrowseGlyph = "";

        /// <summary>What the browse button shows: a file or a folder dialog, the folder it starts in and, for a file,
        /// the name filled in.</summary>
        public sealed class Plan
        {
            public bool File;
            public string Folder;
            public string FileName;
        }

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

        /// <summary>What the browse button shows for a field's value and its default: a file dialog when either names a
        /// file (the default says what the setting holds while the value is empty or names a folder), else a folder
        /// dialog. The dialog starts where the value points, while it is empty where the default does: in the folder the
        /// path names or is in, or the nearest one above it that is there, else in <paramref name="baseFolder"/>. A
        /// file dialog has the file's name filled in.</summary>
        public static Plan For(string value, string fallback, string baseFolder) => For(value, fallback, baseFolder, Directory.Exists, File.Exists);

        public static Plan For(string value, string fallback, string baseFolder, Func<string, bool> folderExists, Func<string, bool> fileExists)
        {
            string path = Absolute(value, baseFolder), declared = Absolute(fallback, baseFolder);
            string start = path ?? declared;
            bool startIsFile = NamesFile(start, folderExists, fileExists);
            var plan = new Plan { File = startIsFile || NamesFile(declared, folderExists, fileExists) };
            string folder = start;
            if (startIsFile)
            {
                plan.FileName = Path.GetFileName(start);
                folder = Path.GetDirectoryName(start);
            }
            else if (plan.File)
                plan.FileName = Path.GetFileName(declared);   // a folder in a file's setting: the default's file in it
            while (!string.IsNullOrEmpty(folder) && !folderExists(folder))
                folder = Path.GetDirectoryName(folder);
            plan.Folder = string.IsNullOrEmpty(folder) ? baseFolder : folder;
            return plan;
        }

        /// <summary>A chosen path as the field stores it: relative to <paramref name="baseFolder"/> when it lies under
        /// it ("overlay\state.json"), else absolute. The folder itself stays absolute: an empty value would mean the
        /// default.</summary>
        public static string Relative(string chosen, string baseFolder)
        {
            if (string.IsNullOrEmpty(chosen) || string.IsNullOrEmpty(baseFolder)) return chosen;
            string full = Path.GetFullPath(chosen);
            string root = Path.GetFullPath(baseFolder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return full.Length > root.Length && full.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? full.Substring(root.Length) : full;
        }

        /// <summary>Shows the dialog the plan names over <paramref name="owner"/>: the chosen path, or null when the user
        /// cancelled. <paramref name="allFiles"/> names the file dialog's filter for every file.</summary>
        public static string Choose(Plan plan, Window owner, string title, string allFiles)
        {
            if (!plan.File)
                return ChooseFolder(plan.Folder, owner, title);
            string extension = Path.GetExtension(plan.FileName ?? "");
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = title,
                // A file the project writes itself need not be there yet.
                CheckFileExists = false,
                InitialDirectory = plan.Folder ?? "",
                FileName = plan.FileName ?? "",
                Filter = (extension.Length > 1 ? extension.Substring(1).ToUpperInvariant() + " (*" + extension + ")|*" + extension + "|" : "") + allFiles + " (*.*)|*.*"
            };
            bool? chosen = owner != null ? dialog.ShowDialog(owner) : dialog.ShowDialog();
            return chosen == true ? dialog.FileName : null;
        }

        // The folder dialog is the shell's own (IFileOpenDialog with FOS_PICKFOLDERS): WPF has none, and Windows Forms'
        // FolderBrowserDialog would bring System.Windows.Forms into the DLL.
        private static string ChooseFolder(string start, Window owner, string title)
        {
            var dialog = (IFileOpenDialog)new FileOpenDialog();
            try
            {
                dialog.GetOptions(out uint options);
                dialog.SetOptions(options | FosPickFolders | FosForceFileSystem | FosPathMustExist);
                if (!string.IsNullOrEmpty(title))
                    dialog.SetTitle(title);
                if (!string.IsNullOrEmpty(start) && SHCreateItemFromParsingName(start, IntPtr.Zero, typeof(IShellItem).GUID, out IShellItem folder) == 0)
                    dialog.SetFolder(folder);
                IntPtr parent = owner != null ? new WindowInteropHelper(owner).Handle : IntPtr.Zero;
                if (dialog.Show(parent) != 0)
                    return null;   // cancelled
                dialog.GetResult(out IShellItem chosen);
                chosen.GetDisplayName(SigdnFileSysPath, out string path);
                return path;
            }
            finally { Marshal.ReleaseComObject(dialog); }
        }

        private const uint FosPickFolders = 0x20, FosForceFileSystem = 0x40, FosPathMustExist = 0x800;
        private const uint SigdnFileSysPath = 0x80058000;

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHCreateItemFromParsingName(string path, IntPtr bindContext, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IShellItem item);

        [ComImport, Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7")]
        private class FileOpenDialog { }

        // IModalWindow's Show, then IFileDialog's methods and IFileOpenDialog's own, in the order of their vtable.
        [ComImport, Guid("D57C7288-D4AD-4768-BE02-9D969532D960"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IFileOpenDialog
        {
            [PreserveSig] int Show(IntPtr parent);
            void SetFileTypes(uint count, IntPtr filters);
            void SetFileTypeIndex(uint index);
            void GetFileTypeIndex(out uint index);
            void Advise(IntPtr events, out uint cookie);
            void Unadvise(uint cookie);
            void SetOptions(uint options);
            void GetOptions(out uint options);
            void SetDefaultFolder(IShellItem folder);
            void SetFolder(IShellItem folder);
            void GetFolder(out IShellItem folder);
            void GetCurrentSelection(out IShellItem item);
            void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string name);
            void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string name);
            void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string title);
            void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string text);
            void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string label);
            void GetResult(out IShellItem item);
            void AddPlace(IShellItem item, int place);
            void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string extension);
            void Close(int result);
            void SetClientGuid([In] ref Guid guid);
            void ClearClientData();
            void SetFilter(IntPtr filter);
            void GetResults(out IntPtr items);
            void GetSelectedItems(out IntPtr items);
        }

        [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItem
        {
            void BindToHandler(IntPtr bindContext, [In] ref Guid handler, [In] ref Guid riid, out IntPtr result);
            void GetParent(out IShellItem parent);
            void GetDisplayName(uint form, [MarshalAs(UnmanagedType.LPWStr)] out string name);
            void GetAttributes(uint mask, out uint attributes);
            void Compare(IShellItem other, uint hint, out int order);
        }
    }
}
