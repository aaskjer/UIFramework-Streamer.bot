using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace UIFramework
{
    /// <summary>
    /// The countries a <c>country</c> field offers: the two-letter codes of ISO 3166-1, and Kosovo (XK), which the
    /// stores use. The names are English here. Another language's come from Windows (its ICU, Windows 10 from 1703
    /// on), so the list reads in the window's language without a table per language; where Windows has none, the
    /// English name stands.
    /// </summary>
    public static class Countries
    {
        // "DE|Germany", by code.
        private static readonly string[] Table =
        {
            "AD|Andorra", "AE|United Arab Emirates", "AF|Afghanistan", "AG|Antigua and Barbuda", "AI|Anguilla", "AL|Albania",
            "AM|Armenia", "AO|Angola", "AQ|Antarctica", "AR|Argentina", "AS|American Samoa", "AT|Austria", "AU|Australia",
            "AW|Aruba", "AX|Åland Islands", "AZ|Azerbaijan", "BA|Bosnia and Herzegovina", "BB|Barbados", "BD|Bangladesh",
            "BE|Belgium", "BF|Burkina Faso", "BG|Bulgaria", "BH|Bahrain", "BI|Burundi", "BJ|Benin", "BL|Saint Barthélemy",
            "BM|Bermuda", "BN|Brunei", "BO|Bolivia", "BQ|Caribbean Netherlands", "BR|Brazil", "BS|Bahamas", "BT|Bhutan",
            "BV|Bouvet Island", "BW|Botswana", "BY|Belarus", "BZ|Belize", "CA|Canada", "CC|Cocos (Keeling) Islands",
            "CD|Congo (Kinshasa)", "CF|Central African Republic", "CG|Congo (Brazzaville)", "CH|Switzerland", "CI|Côte d'Ivoire",
            "CK|Cook Islands", "CL|Chile", "CM|Cameroon", "CN|China", "CO|Colombia", "CR|Costa Rica", "CU|Cuba", "CV|Cape Verde",
            "CW|Curaçao", "CX|Christmas Island", "CY|Cyprus", "CZ|Czechia", "DE|Germany", "DJ|Djibouti", "DK|Denmark",
            "DM|Dominica", "DO|Dominican Republic", "DZ|Algeria", "EC|Ecuador", "EE|Estonia", "EG|Egypt", "EH|Western Sahara",
            "ER|Eritrea", "ES|Spain", "ET|Ethiopia", "FI|Finland", "FJ|Fiji", "FK|Falkland Islands", "FM|Micronesia",
            "FO|Faroe Islands", "FR|France", "GA|Gabon", "GB|United Kingdom", "GD|Grenada", "GE|Georgia", "GF|French Guiana",
            "GG|Guernsey", "GH|Ghana", "GI|Gibraltar", "GL|Greenland", "GM|Gambia", "GN|Guinea", "GP|Guadeloupe",
            "GQ|Equatorial Guinea", "GR|Greece", "GS|South Georgia and the South Sandwich Islands", "GT|Guatemala", "GU|Guam",
            "GW|Guinea-Bissau", "GY|Guyana", "HK|Hong Kong", "HM|Heard Island and McDonald Islands", "HN|Honduras", "HR|Croatia",
            "HT|Haiti", "HU|Hungary", "ID|Indonesia", "IE|Ireland", "IL|Israel", "IM|Isle of Man", "IN|India",
            "IO|British Indian Ocean Territory", "IQ|Iraq", "IR|Iran", "IS|Iceland", "IT|Italy", "JE|Jersey", "JM|Jamaica",
            "JO|Jordan", "JP|Japan", "KE|Kenya", "KG|Kyrgyzstan", "KH|Cambodia", "KI|Kiribati", "KM|Comoros",
            "KN|Saint Kitts and Nevis", "KP|North Korea", "KR|South Korea", "KW|Kuwait", "KY|Cayman Islands", "KZ|Kazakhstan",
            "LA|Laos", "LB|Lebanon", "LC|Saint Lucia", "LI|Liechtenstein", "LK|Sri Lanka", "LR|Liberia", "LS|Lesotho",
            "LT|Lithuania", "LU|Luxembourg", "LV|Latvia", "LY|Libya", "MA|Morocco", "MC|Monaco", "MD|Moldova", "ME|Montenegro",
            "MF|Saint Martin", "MG|Madagascar", "MH|Marshall Islands", "MK|North Macedonia", "ML|Mali", "MM|Myanmar",
            "MN|Mongolia", "MO|Macao", "MP|Northern Mariana Islands", "MQ|Martinique", "MR|Mauritania", "MS|Montserrat",
            "MT|Malta", "MU|Mauritius", "MV|Maldives", "MW|Malawi", "MX|Mexico", "MY|Malaysia", "MZ|Mozambique", "NA|Namibia",
            "NC|New Caledonia", "NE|Niger", "NF|Norfolk Island", "NG|Nigeria", "NI|Nicaragua", "NL|Netherlands", "NO|Norway",
            "NP|Nepal", "NR|Nauru", "NU|Niue", "NZ|New Zealand", "OM|Oman", "PA|Panama", "PE|Peru", "PF|French Polynesia",
            "PG|Papua New Guinea", "PH|Philippines", "PK|Pakistan", "PL|Poland", "PM|Saint Pierre and Miquelon",
            "PN|Pitcairn Islands", "PR|Puerto Rico", "PS|Palestine", "PT|Portugal", "PW|Palau", "PY|Paraguay", "QA|Qatar",
            "RE|Réunion", "RO|Romania", "RS|Serbia", "RU|Russia", "RW|Rwanda", "SA|Saudi Arabia", "SB|Solomon Islands",
            "SC|Seychelles", "SD|Sudan", "SE|Sweden", "SG|Singapore", "SH|Saint Helena", "SI|Slovenia",
            "SJ|Svalbard and Jan Mayen", "SK|Slovakia", "SL|Sierra Leone", "SM|San Marino", "SN|Senegal", "SO|Somalia",
            "SR|Suriname", "SS|South Sudan", "ST|São Tomé and Príncipe", "SV|El Salvador", "SX|Sint Maarten", "SY|Syria",
            "SZ|Eswatini", "TC|Turks and Caicos Islands", "TD|Chad", "TF|French Southern Territories", "TG|Togo", "TH|Thailand",
            "TJ|Tajikistan", "TK|Tokelau", "TL|Timor-Leste", "TM|Turkmenistan", "TN|Tunisia", "TO|Tonga", "TR|Türkiye",
            "TT|Trinidad and Tobago", "TV|Tuvalu", "TW|Taiwan", "TZ|Tanzania", "UA|Ukraine", "UG|Uganda",
            "UM|U.S. Outlying Islands", "US|United States", "UY|Uruguay", "UZ|Uzbekistan", "VA|Vatican City",
            "VC|Saint Vincent and the Grenadines", "VE|Venezuela", "VG|British Virgin Islands", "VI|U.S. Virgin Islands",
            "VN|Vietnam", "VU|Vanuatu", "WF|Wallis and Futuna", "WS|Samoa", "XK|Kosovo", "YE|Yemen", "YT|Mayotte",
            "ZA|South Africa", "ZM|Zambia", "ZW|Zimbabwe"
        };

        private static readonly Dictionary<string, string> EnglishNames = Table
            .Select(entry => entry.Split('|'))
            .ToDictionary(pair => pair[0], pair => pair[1], StringComparer.OrdinalIgnoreCase);

        /// <summary>The codes, in upper case, in the order of the alphabet.</summary>
        public static readonly IReadOnlyList<string> Codes = Table.Select(entry => entry.Substring(0, 2)).ToArray();

        /// <summary>Whether the text is one of the codes, in either case.</summary>
        public static bool IsCode(string code) => code != null && EnglishNames.ContainsKey(code.Trim());

        /// <summary>The country's English name; null for a code that is none.</summary>
        public static string English(string code) =>
            code != null && EnglishNames.TryGetValue(code.Trim(), out string name) ? name : null;

        // language -> code -> name, as Windows gave it. A language Windows has no names in maps to an empty table.
        private static readonly Dictionary<string, Dictionary<string, string>> Translated =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>The country's name in a language (the window's: "de", "pt-BR"): Windows' where it has one, else
        /// the English name. A code that is no country gives itself.</summary>
        public static string Name(string code, string language)
        {
            code = (code ?? "").Trim().ToUpperInvariant();
            string english = English(code);
            if (english == null) return code;
            if (string.IsNullOrWhiteSpace(language) || language.Trim().StartsWith("en", StringComparison.OrdinalIgnoreCase))
                return english;
            Dictionary<string, string> names;
            lock (Translated)
                if (!Translated.TryGetValue(language, out names))
                    Translated[language] = names = Ask(language.Trim());
            return names.TryGetValue(code, out string name) ? name : english;
        }

        /// <summary>The countries as a field lists them, "Germany (DE)", sorted by name as the language sorts.</summary>
        public static List<(string Code, string Label)> Listed(string language, IEnumerable<string> codes = null)
        {
            CompareInfo compare;
            try { compare = CultureInfo.GetCultureInfo(string.IsNullOrWhiteSpace(language) ? "en" : language.Trim()).CompareInfo; }
            catch (CultureNotFoundException) { compare = CultureInfo.InvariantCulture.CompareInfo; }
            return (codes ?? Codes)
                .Select(code => (Code: code.Trim().ToUpperInvariant(), Name: Name(code, language)))
                .OrderBy(c => c.Name, Comparer<string>.Create((a, b) => compare.Compare(a, b, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace)))
                .Select(c => (c.Code, Label(c.Code, c.Name)))
                .ToList();
        }

        /// <summary>A country as a field shows it: its name with the code behind it; the code alone where it has no name.</summary>
        public static string Label(string code, string name) =>
            string.IsNullOrEmpty(name) || string.Equals(name, code, StringComparison.OrdinalIgnoreCase) ? code : name + " (" + code + ")";

        /// <summary>Whether Windows names countries in other languages on this machine. Without it every language
        /// reads the English names.</summary>
        public static bool SystemNames => Icu.Available;

        private static Dictionary<string, string> Ask(string language)
        {
            var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!Icu.Available) return names;
            string display = language.Replace('-', '_');
            foreach (var code in Codes)
            {
                string name = Icu.Country(code, display);
                // Where ICU has no name it hands the code back.
                if (!string.IsNullOrWhiteSpace(name) && !string.Equals(name, code, StringComparison.OrdinalIgnoreCase))
                    names[code] = name;
            }
            return names;
        }

        /// <summary>Windows' own copy of ICU, looked for in System32 only: icu.dll since Windows 10 1903, icuuc.dll
        /// since 1703. A Windows without either, or a call that fails, leaves the English names.</summary>
        private static class Icu
        {
            [DllImport("icu.dll", EntryPoint = "uloc_getDisplayCountry", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
            [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
            private static extern int DisplayCountryIcu([MarshalAs(UnmanagedType.LPStr)] string locale, [MarshalAs(UnmanagedType.LPStr)] string displayLocale,
                [Out] byte[] result, int capacity, ref int status);

            [DllImport("icuuc.dll", EntryPoint = "uloc_getDisplayCountry", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
            [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
            private static extern int DisplayCountryIcuuc([MarshalAs(UnmanagedType.LPStr)] string locale, [MarshalAs(UnmanagedType.LPStr)] string displayLocale,
                [Out] byte[] result, int capacity, ref int status);

            private const int Capacity = 160;   // UTF-16 units; the longest name is far shorter
            private static int _which = -1;     // -1 not looked yet, 0 none, 1 icu.dll, 2 icuuc.dll

            public static bool Available
            {
                get
                {
                    if (_which < 0)
                        _which = Try(1, "DE", "en") != null ? 1 : Try(2, "DE", "en") != null ? 2 : 0;
                    return _which > 0;
                }
            }

            public static string Country(string code, string displayLocale) => Available ? Try(_which, code, displayLocale) : null;

            private static string Try(int which, string code, string displayLocale)
            {
                try
                {
                    var buffer = new byte[Capacity * 2];
                    int status = 0;
                    int length = which == 1
                        ? DisplayCountryIcu("und_" + code, displayLocale, buffer, Capacity, ref status)
                        : DisplayCountryIcuuc("und_" + code, displayLocale, buffer, Capacity, ref status);
                    // Above zero is a failure; below it a warning (a name from a parent language), which is fine.
                    if (status > 0 || length <= 0 || length > Capacity) return null;
                    return Encoding.Unicode.GetString(buffer, 0, length * 2);
                }
                catch (Exception ex) when (ex is DllNotFoundException || ex is EntryPointNotFoundException || ex is BadImageFormatException
                    || ex is MarshalDirectiveException || ex is SEHException || ex is AccessViolationException)
                {
                    return null;
                }
            }
        }
    }
}
