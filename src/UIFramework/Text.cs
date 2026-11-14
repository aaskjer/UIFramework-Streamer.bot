using System;
using System.Globalization;

namespace UIFramework
{
    /// <summary>
    /// The window's own texts (buttons, status line, dialogs, menus, the standard pages' chrome) in the language
    /// the layout file names: the <c>ui</c> section of <c>conventions/lang/&lt;code&gt;.json</c>, English where a
    /// language has no entry. What a game declares (labels, help, the help and about pages) is shown as declared.
    /// <see cref="Entry.Prepare"/> sets the language for the window it prepares; one process shows one language.
    /// </summary>
    public static class Text
    {
        // Per window thread, like the theme: two games' windows in different languages each keep theirs. A thread that
        // set none reads the last language set anywhere.
        [ThreadStatic] private static Lang _lang;
        private static volatile Lang _shared;

        /// <summary>The texts to use from now on (the conventions' language, English merged underneath).</summary>
        public static void Use(Lang lang) { _lang = lang; _shared = lang; }

        /// <summary>The window's language code ("en", "de").</summary>
        public static string Code => (_lang ?? _shared ?? Conventions.Default.Lang).Code ?? "en";

        /// <summary>The thousands separator a reader of the window's language expects, from Windows' culture for its
        /// code: 1,500 in English, 1.500 in German, Spanish and Brazilian Portuguese, 1 500 in French. A space of any
        /// kind (French groups with a narrow no-break space, which not every font has) is the plain one: chat and a
        /// text box both show it, and it is the space a game's own separator setting offers. A code Windows does not
        /// know: the point.</summary>
        public static string GroupSeparator
        {
            get
            {
                try
                {
                    var culture = CultureInfo.GetCultureInfo(Code);
                    if (culture.EnglishName.StartsWith("Unknown", StringComparison.OrdinalIgnoreCase))
                        return ".";
                    string separator = culture.NumberFormat.NumberGroupSeparator;
                    return separator.Length > 0 && string.IsNullOrWhiteSpace(separator) ? " " : separator;
                }
                catch (Exception) { return "."; }
            }
        }

        /// <summary>The text with its placeholders filled, or as it is when its braces are broken: a language file
        /// from outside (UIFramework\lang, a game's Ui texts) with "{0" in it kept the whole window from opening.</summary>
        public static string Fill(string text, params object[] args)
        {
            try { return string.Format(CultureInfo.InvariantCulture, text ?? "", args); }
            catch (FormatException) { return text ?? ""; }
        }

        public static string T(string key)
        {
            var lang = _lang ?? _shared ?? Conventions.Default.Lang;
            if (lang.Ui.TryGetValue(key, out var text)) return text;
            if (!ReferenceEquals(lang, Conventions.Default.Lang) && Conventions.Default.Lang.Ui.TryGetValue(key, out text)) return text;
            return key;
        }

        /// <summary>The text with its {0}, {1} placeholders filled.</summary>
        public static string T(string key, params object[] args) => Fill(T(key), args);
    }
}
