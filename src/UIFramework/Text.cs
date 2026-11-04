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
        private static Lang _lang;

        /// <summary>The texts to use from now on (the conventions' language, English merged underneath).</summary>
        public static void Use(Lang lang) => _lang = lang;

        public static string T(string key)
        {
            var lang = _lang ?? Conventions.Default.Lang;
            if (lang.Ui.TryGetValue(key, out var text)) return text;
            if (!ReferenceEquals(lang, Conventions.Default.Lang) && Conventions.Default.Lang.Ui.TryGetValue(key, out text)) return text;
            return key;
        }

        /// <summary>The text with its {0}, {1} placeholders filled.</summary>
        public static string T(string key, params object[] args) => string.Format(CultureInfo.InvariantCulture, T(key), args);
    }
}
