using System;
using System.Linq;
using System.Windows.Media;

namespace UIFramework
{
    /// <summary>
    /// The palette, in one place for every host. Light and dark are the two sets the games shipped with;
    /// an accent override and the rainbow mode come from the layout file. Brushes are created on each read
    /// so a window built after a theme switch gets the new colours without anything being frozen.
    /// </summary>
    public static class Theme
    {
        public static bool IsDark;
        public static bool IsRainbow;

        /// <summary>An accent the user picked, or null for the default blue.</summary>
        public static Color? AccentOverride;

        private static double _rainbowHue;

        public static Color ContentBg => IsDark ? Color.FromRgb(36, 36, 36) : Colors.White;
        public static Color Surface => IsDark ? Color.FromRgb(52, 52, 52) : Color.FromRgb(243, 243, 243);
        public static Color Hover => IsDark ? Color.FromRgb(60, 60, 60) : Color.FromRgb(236, 236, 236);
        public static Color Line => IsDark ? Color.FromRgb(60, 60, 60) : Color.FromRgb(226, 226, 226);
        public static Color LineStrong => IsDark ? Color.FromRgb(92, 92, 92) : Color.FromRgb(196, 196, 196);
        public static Color TextColor => IsDark ? Color.FromRgb(235, 235, 235) : Color.FromRgb(28, 28, 28);
        public static Color SubColor => IsDark ? Color.FromRgb(178, 178, 178) : Color.FromRgb(86, 86, 86);
        public static Color MutedColor => IsDark ? Color.FromRgb(145, 145, 145) : Color.FromRgb(112, 112, 112);
        public static Brush LabelFg => new SolidColorBrush(TextColor);
        public static Brush SubFg => new SolidColorBrush(SubColor);
        public static Brush MutedFg => new SolidColorBrush(MutedColor);

        public static Color Accent => AccentOverride ?? (IsDark ? Color.FromRgb(96, 165, 250) : Color.FromRgb(37, 99, 235));
        public static Color AccentText => AccentOverride.HasValue
            ? (IsDark ? Colours.Lighten(AccentOverride.Value, 0.35) : Colours.Darken(AccentOverride.Value, 0.15))
            : (IsDark ? Color.FromRgb(147, 197, 253) : Color.FromRgb(29, 78, 216));
        public static Color AccentSoft => AccentOverride.HasValue
            ? Colours.Mix(AccentOverride.Value, ContentBg, IsDark ? 0.72 : 0.82)
            : (IsDark ? Color.FromRgb(30, 58, 95) : Color.FromRgb(219, 234, 254));
        public static Color Primary => AccentOverride ?? Color.FromRgb(37, 99, 235);
        public static Color PrimaryHover => AccentOverride.HasValue ? Colours.Darken(AccentOverride.Value, 0.12) : Color.FromRgb(29, 78, 216);
        public static Color ToggleOff => IsDark ? Color.FromRgb(98, 98, 98) : Color.FromRgb(189, 189, 189);
        public static Color SuccessSoft => IsDark ? Color.FromRgb(20, 83, 45) : Color.FromRgb(220, 252, 231);
        public static Color SuccessText => IsDark ? Color.FromRgb(134, 239, 172) : Color.FromRgb(21, 128, 61);
        public static Color WarningSoft => IsDark ? Color.FromRgb(113, 63, 18) : Color.FromRgb(254, 243, 199);
        public static Color WarningText => IsDark ? Color.FromRgb(253, 230, 138) : Color.FromRgb(146, 64, 14);
        public static Color UnsavedText => IsDark ? Color.FromRgb(251, 146, 60) : Color.FromRgb(194, 65, 12);
        public static Color DangerText => IsDark ? Color.FromRgb(252, 165, 165) : Color.FromRgb(185, 28, 28);
        public static Color Danger => Color.FromRgb(220, 38, 38);

        public static Color InputBg => IsDark ? Color.FromRgb(48, 48, 48) : Colors.White;
        public static Color InputFg => IsDark ? Colors.White : Colors.Black;
        public static Color PopupBg => IsDark ? Color.FromRgb(44, 44, 44) : Colors.White;
        public static Color InputBorder => IsDark ? Color.FromRgb(86, 86, 86) : Color.FromRgb(190, 190, 190);

        public static void RainbowTick()
        {
            _rainbowHue += 2.0;
            if (_rainbowHue >= 360.0)
                _rainbowHue -= 360.0;
        }

        public static SolidColorBrush RainbowBrush(double offset)
        {
            double hue = (_rainbowHue + offset) % 360.0;
            return new SolidColorBrush(Colours.HsvToRgb(hue, 0.85, 1.0));
        }
    }

    /// <summary>Colour arithmetic shared by the theme, the colour picker and the colour fields.</summary>
    public static class Colours
    {
        public static Color HsvToRgb(double h, double s, double v)
        {
            h = ((h % 360) + 360) % 360;
            double c = v * s;
            double x = c * (1 - Math.Abs((h / 60.0) % 2 - 1));
            double m = v - c;
            double r1, g1, b1;
            if (h < 60) { r1 = c; g1 = x; b1 = 0; }
            else if (h < 120) { r1 = x; g1 = c; b1 = 0; }
            else if (h < 180) { r1 = 0; g1 = c; b1 = x; }
            else if (h < 240) { r1 = 0; g1 = x; b1 = c; }
            else if (h < 300) { r1 = x; g1 = 0; b1 = c; }
            else { r1 = c; g1 = 0; b1 = x; }
            return Color.FromRgb((byte)Math.Round((r1 + m) * 255), (byte)Math.Round((g1 + m) * 255), (byte)Math.Round((b1 + m) * 255));
        }

        public static void RgbToHsv(Color c, out double h, out double s, out double v)
        {
            double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double delta = max - min;
            v = max;
            s = max == 0 ? 0 : delta / max;
            if (delta == 0) h = 0;
            else if (max == r) h = 60 * (((g - b) / delta) % 6);
            else if (max == g) h = 60 * (((b - r) / delta) + 2);
            else h = 60 * (((r - g) / delta) + 4);
            if (h < 0) h += 360;
        }

        public static string ToHex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

        /// <summary>#RRGGBB or RRGGBB.</summary>
        public static bool TryParseHex(string text, out Color colour)
        {
            colour = Colors.Transparent;
            string hex = (text ?? "").Trim().TrimStart('#');
            if (hex.Length != 6 || !hex.All(Uri.IsHexDigit))
                return false;
            colour = Color.FromRgb(Convert.ToByte(hex.Substring(0, 2), 16), Convert.ToByte(hex.Substring(2, 2), 16), Convert.ToByte(hex.Substring(4, 2), 16));
            return true;
        }

        public static SolidColorBrush ParseHexBrush(string hex)
        {
            try
            {
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString(string.IsNullOrWhiteSpace(hex) ? "#000000" : hex));
            }
            catch
            {
                return new SolidColorBrush(Colors.Black);
            }
        }

        public static int ChannelOf(Color colour, int channel) => channel == 0 ? colour.R : channel == 1 ? colour.G : colour.B;

        public static Color WithChannel(Color colour, int channel, int value)
        {
            byte b = (byte)Math.Max(0, Math.Min(255, value));
            return Color.FromRgb(channel == 0 ? b : colour.R, channel == 1 ? b : colour.G, channel == 2 ? b : colour.B);
        }

        public static Color Mix(Color a, Color b, double towardsB)
        {
            towardsB = Math.Max(0, Math.Min(1, towardsB));
            return Color.FromRgb(
                (byte)Math.Round(a.R + (b.R - a.R) * towardsB),
                (byte)Math.Round(a.G + (b.G - a.G) * towardsB),
                (byte)Math.Round(a.B + (b.B - a.B) * towardsB));
        }

        public static Color Lighten(Color c, double amount) => Mix(c, Colors.White, amount);
        public static Color Darken(Color c, double amount) => Mix(c, Colors.Black, amount);
    }
}
