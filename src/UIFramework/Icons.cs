using System.Windows.Media;

namespace UIFramework
{
    /// <summary>Glyphs from Segoe Fluent Icons (Segoe MDL2 Assets on older Windows), used for the buttons the
    /// window itself owns. Page icons in the rail are emoji, chosen by the spec.</summary>
    public static class Icons
    {
        public static readonly FontFamily Font = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");

        public const string Sun = "", Moon = "", Reset = "", Refresh = "";
        public const string Remove = "", Open = "", ChevronDown = "";
        public const string Edit = "", Pin = "", Folder = "", Check = "";
    }
}
