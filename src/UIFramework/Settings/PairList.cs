using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace UIFramework
{
    /// <summary>
    /// A list of two columns that is stored as one line of text, <c>a:b,c:d</c>, for a project that has always read
    /// its pairs so (<c>format: "pairs"</c> on a <c>list</c>). The window works on rows like any list: the text
    /// becomes rows where the settings are read (<see cref="ToRows"/>) and text again where they are written or handed
    /// to the project (<see cref="ToText"/>), so the editor, the search, a reset and the conditions know only rows.
    /// <para>Reading is generous: entries stand between commas, semicolons or lines, and a key is parted from its
    /// value by the first colon or equals sign. Writing is strict: <c>key:value</c> between commas. The format has no
    /// escaping, so a key cannot hold <c>: = , ;</c> and a value cannot hold <c>, ;</c>; the cells do not take them,
    /// and one that arrives another way is written as a blank.</para>
    /// </summary>
    public static class PairList
    {
        public const string Format = "pairs";

        /// <summary>What a key's cell and a value's cell do not take.</summary>
        public const string KeyForbidden = ":=,;";
        public const string ValueForbidden = ",;";

        private static readonly char[] EntryEnds = { ',', ';', '\n', '\r' };
        private static readonly char[] KeyEnds = { ':', '=' };

        /// <summary>Whether a setting is such a list: a <c>list</c> of exactly two columns that names the format.</summary>
        public static bool Applies(Setting setting) =>
            setting != null && setting.Kind == "list" && Named(setting.Format) && setting.Columns != null && setting.Columns.Count == 2;

        /// <summary>Whether a declaration's <c>format</c> names this one, whatever its case.</summary>
        public static bool Named(string format) => string.Equals((format ?? "").Trim(), Format, StringComparison.OrdinalIgnoreCase);

        /// <summary>The rows a stored value stands for: the text's pairs; an array as it is (a file that holds rows
        /// already, a default declared as rows); nothing for anything else.</summary>
        public static JArray Rows(JToken stored, IList<SettingColumn> columns)
        {
            if (stored is JArray rows) return (JArray)rows.DeepClone();
            if (stored == null || stored.Type == JTokenType.Null || stored is JContainer) return new JArray();
            return Rows(FieldBinding.Invariant(stored), columns);
        }

        public static JArray Rows(string text, IList<SettingColumn> columns)
        {
            var rows = new JArray();
            foreach (var entry in (text ?? "").Split(EntryEnds, StringSplitOptions.RemoveEmptyEntries))
            {
                string pair = entry.Trim();
                if (pair.Length == 0) continue;
                int at = pair.IndexOfAny(KeyEnds);
                string key = at < 0 ? pair : pair.Substring(0, at).Trim();
                string value = at < 0 ? "" : pair.Substring(at + 1).Trim();
                rows.Add(new JObject { [columns[0].Key] = Cell(columns[0], key), [columns[1].Key] = Cell(columns[1], value) });
            }
            return rows;
        }

        // A number column holds a number where the text is one, as a row typed into the editor does.
        private static JToken Cell(SettingColumn column, string text)
        {
            if ((column.Kind == "int" || column.Kind == "long") && long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long whole))
                return whole;
            if (column.Kind == "double" && FieldBinding.TryParseInvariantDouble(text, out double number))
                return number;
            return text;
        }

        /// <summary>The rows as the text that is stored. A row without a key and without a value is left out; a key
        /// alone is written without a colon.</summary>
        public static string Text(JArray rows, IList<SettingColumn> columns)
        {
            var entries = new List<string>();
            foreach (var row in (rows ?? new JArray()).OfType<JObject>())
            {
                string key = Clean(FieldBinding.Invariant(row[columns[0].Key]), KeyForbidden);
                string value = Clean(FieldBinding.Invariant(row[columns[1].Key]), ValueForbidden);
                if (key.Length == 0 && value.Length == 0) continue;
                entries.Add(value.Length > 0 ? key + ":" + value : key);
            }
            return string.Join(",", entries);
        }

        private static string Clean(string text, string forbidden)
        {
            var chars = (text ?? "").ToCharArray();
            for (int i = 0; i < chars.Length; i++)
                if (forbidden.IndexOf(chars[i]) >= 0 || chars[i] == '\n' || chars[i] == '\r') chars[i] = ' ';
            return new string(chars).Trim();
        }

        /// <summary>Turns every such setting's stored text into rows, in place: where the settings are read.</summary>
        public static void ToRows(JObject settings, IEnumerable<Setting> schema)
        {
            if (settings == null) return;
            foreach (var setting in (schema ?? Enumerable.Empty<Setting>()).Where(Applies))
                if (settings[setting.Key] is JToken stored && !(stored is JArray))
                    settings[setting.Key] = Rows(stored, setting.Columns);
        }

        /// <summary>Turns every such setting's rows into the text that is stored, in place: where the settings are
        /// written or handed to the project.</summary>
        public static void ToText(JObject settings, IEnumerable<Setting> schema)
        {
            if (settings == null) return;
            foreach (var setting in (schema ?? Enumerable.Empty<Setting>()).Where(Applies))
                if (settings[setting.Key] is JArray rows)
                    settings[setting.Key] = Text(rows, setting.Columns);
        }
    }
}
