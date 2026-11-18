using System.Windows.Media;

namespace UIFramework
{
    /// <summary>Glyphs from Segoe Fluent Icons (Segoe MDL2 Assets on older Windows), used for the buttons the
    /// window itself owns. Page icons in the rail are emoji, chosen by the spec.</summary>
    public static class Icons
    {
        public static readonly FontFamily Font = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");

        /// <summary>The magnifier of the search button.</summary>
        public const string Search = "";

        /// <summary>The question mark of the tour button.</summary>
        public const string Help = "";

        public const string Sun = "", Moon = "", Reset = "", Refresh = "";
        public const string Remove = "", Open = "", ChevronDown = "", ChevronRight = "";
        public const string Edit = "", Pin = "", Folder = "", Check = "";
        /// <summary>The two sheets of a copy button (an answer's value with <c>copy</c>).</summary>
        public const string Copy = "";
        public const string Undo = "";
        public const string Eye = "";
        public const string Export = "", Import = "";
    }
}
