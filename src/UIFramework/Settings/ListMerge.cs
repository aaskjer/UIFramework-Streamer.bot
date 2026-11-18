using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UIFramework
{
    /// <summary>
    /// A list the project writes to as well (<c>rowKey</c>: public holidays it downloads, birthdays a chat command
    /// adds). A save does not put the window's rows over the file's: it merges three ways, row by row, between what
    /// the window loaded, what it holds now and what the file holds at that moment. A row the project added stays, one
    /// it removed stays removed unless the user changed it, a row the user removed is removed, one the user added is
    /// added, and in a row both have, the cells the user changed win while the others take the project's.
    /// <para>A row is named by its key columns. One without a complete key (a part missing or empty) is named by all
    /// the window shows of it instead, so changing it there counts as that row removed and another added. Rows of the
    /// same name are paired in the order they stand in: the first with the first. Nothing of the kind is dropped.</para>
    /// </summary>
    public static class ListMerge
    {
        private const string Sep = "\u001f";

        /// <summary>The columns a <c>rowKey</c> names: "Date,Name".</summary>
        public static List<string> KeyColumns(string rowKey) =>
            (rowKey ?? "").Split(',').Select(part => part.Trim()).Where(part => part.Length > 0).ToList();

        /// <summary>Whether a save merges this setting's list instead of replacing it.</summary>
        public static bool Applies(Setting setting) => setting != null && KeyColumns(setting.RowKey).Count > 0;

        public static JArray Merge(JArray loaded, JArray mine, JArray theirs, Setting setting) =>
            Merge(loaded, mine, theirs, KeyColumns(setting.RowKey), setting.Columns);

        /// <summary>The list to write: the file's rows in the file's order with the window's changes laid into them.
        /// A row only the window has goes behind the nearest row before it there that the file has too; with none
        /// before it, in front of the nearest one after it; with neither, to the end.</summary>
        public static JArray Merge(JArray loaded, JArray mine, JArray theirs, IList<string> keys, IList<SettingColumn> columns)
        {
            var b = Rows(loaded, keys, columns);
            var m = Rows(mine, keys, columns);
            var t = Rows(theirs, keys, columns);
            var was = b.ToDictionary(row => row.Name, row => row.Item, StringComparer.Ordinal);
            var now = m.ToDictionary(row => row.Name, row => row.Item, StringComparer.Ordinal);
            var there = new HashSet<string>(t.Select(row => row.Name), StringComparer.Ordinal);

            var result = new List<Row>();
            foreach (var row in t)
            {
                bool had = was.TryGetValue(row.Name, out var old);
                if (!now.TryGetValue(row.Name, out var own))
                {
                    // Not in the window: the project added it meanwhile, or the user removed it.
                    if (!had) result.Add(new Row { Name = row.Name, Item = row.Item.DeepClone() });
                    continue;
                }
                result.Add(new Row { Name = row.Name, Item = Cells(row.Item, had ? old : null, own, columns) });
            }

            // What the file does not have: a row the user added, or one the project removed meanwhile. That one stays
            // removed unless the user changed it.
            string after = null;
            for (int i = 0; i < m.Count; i++)
            {
                var row = m[i];
                if (there.Contains(row.Name)) { after = row.Name; continue; }
                JToken item;
                if (!was.TryGetValue(row.Name, out var old)) item = row.Item.DeepClone();
                else if (Changed(old, row.Item, columns)) item = Cells(old, old, row.Item, columns);
                else continue;
                int at;
                if (after != null)
                    at = result.FindIndex(placed => placed.Name == after) + 1;
                else
                {
                    var next = m.Skip(i + 1).FirstOrDefault(later => there.Contains(later.Name));
                    at = next == null ? result.Count : result.FindIndex(placed => placed.Name == next.Name);
                }
                result.Insert(at, new Row { Name = row.Name, Item = item });
                after = row.Name;
            }
            return new JArray(result.Select(row => row.Item));
        }

        /// <summary>The keys that more than one row of a list has, as text ("2026-12-25, Christmas").</summary>
        public static List<string> Duplicates(JArray rows, IList<string> keys, IList<SettingColumn> columns)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var twice = new List<string>();
            foreach (var row in (rows ?? new JArray()).OfType<JObject>())
            {
                string key = KeyOf(row, keys, columns);
                if (key == null || seen.Add(key)) continue;
                string text = string.Join(", ", keys.Select(k => FieldBinding.Invariant(row[k]).Trim()));
                if (!twice.Contains(text)) twice.Add(text);
            }
            return twice;
        }

        /// <summary>A cell as the window tells it from another: its text without the blanks around it, a number as
        /// that number, and nothing for all that an empty cell is saved as (no value, "", 0, false, an empty array).
        /// So a row the window read and wrote back untouched compares equal, whatever form the file had it in.</summary>
        public static string Canon(string kind, JToken value)
        {
            switch (kind)
            {
                case "bool":
                    return value != null && value.Type == JTokenType.Boolean && (bool)value ? "true" : "";
                case "int":
                case "long":
                    return long.TryParse(FieldBinding.Invariant(value).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long whole) && whole != 0
                        ? whole.ToString(CultureInfo.InvariantCulture) : "";
                case "double":
                    return FieldBinding.TryParseTyped(FieldBinding.Invariant(value), out double number) && number != 0
                        ? number.ToString("R", CultureInfo.InvariantCulture) : "";
                default:
                    if (value is JContainer container) return container.HasValues ? container.ToString(Formatting.None) : "";
                    return FieldBinding.Invariant(value).Trim();
            }
        }

        /// <summary>A JSON array as a read-only cell shows it: "DE-BY, DE-BW".</summary>
        public static string Shown(JArray array) =>
            string.Join(", ", array.Select(element => FieldBinding.Invariant(element)));

        private sealed class Row
        {
            public string Name;
            public JToken Item;
        }

        /// <summary>Each row with its name; the second row of a name is told from the first by its place among them.</summary>
        private static List<Row> Rows(JArray list, IList<string> keys, IList<SettingColumn> columns)
        {
            var rows = new List<Row>();
            var seen = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var item in list ?? new JArray())
            {
                string name = NameOf(item, keys, columns);
                seen.TryGetValue(name, out int count);
                seen[name] = count + 1;
                rows.Add(new Row { Name = name + Sep + count.ToString(CultureInfo.InvariantCulture), Item = item });
            }
            return rows;
        }

        private static string NameOf(JToken item, IList<string> keys, IList<SettingColumn> columns)
        {
            if (!(item is JObject row)) return "v" + Sep + item.ToString(Formatting.None);
            string key = KeyOf(row, keys, columns);
            if (key != null) return "k" + Sep + key;
            // No complete key: all the window shows of the row (every property where no columns are known).
            var cells = columns != null && columns.Count > 0
                ? columns.Select(column => Canon(column.Kind, row[column.Key]))
                : row.Properties().OrderBy(p => p.Name, StringComparer.Ordinal).Select(p => p.Name + "=" + Canon(null, p.Value));
            return "c" + Sep + string.Join(Sep, cells);
        }

        /// <summary>The row's key, or null when a part of it is missing or empty.</summary>
        private static string KeyOf(JObject row, IList<string> keys, IList<SettingColumn> columns)
        {
            if (keys == null || keys.Count == 0) return null;
            var parts = new List<string>();
            foreach (string key in keys)
            {
                string part = Canon(KindOf(columns, key), row[key]);
                if (part.Length == 0) return null;
                parts.Add(part);
            }
            return string.Join(Sep, parts);
        }

        private static string KindOf(IList<SettingColumn> columns, string key) => columns?.FirstOrDefault(column => column.Key == key)?.Kind;

        private static bool IsColumn(IList<SettingColumn> columns, string key) => columns != null && columns.Any(column => column.Key == key);

        /// <summary>Whether the window's cell differs from the one it loaded. A property the window does not carry (the
        /// row editor writes its columns only) is no change, and a property no column shows compares as it is.</summary>
        private static bool Differs(IList<SettingColumn> columns, string name, JToken old, JToken own) =>
            IsColumn(columns, name) ? Canon(KindOf(columns, name), old) != Canon(KindOf(columns, name), own) : !JToken.DeepEquals(old, own);

        private static bool Changed(JToken old, JToken own, IList<SettingColumn> columns) =>
            old is JObject was && own is JObject mine
                ? mine.Properties().Any(property => Differs(columns, property.Name, was[property.Name], property.Value))
                : !JToken.DeepEquals(old, own);

        /// <summary>The project's row with the cells the user changed laid over it. Without a row the window loaded
        /// (both added it), every cell the user filled in counts as changed.</summary>
        private static JToken Cells(JToken theirs, JToken old, JToken own, IList<SettingColumn> columns)
        {
            if (!(theirs is JObject there) || !(own is JObject mine)) return theirs.DeepClone();
            var was = old as JObject;
            var row = (JObject)there.DeepClone();
            foreach (var property in mine.Properties())
                if (Differs(columns, property.Name, was?[property.Name], property.Value))
                    row[property.Name] = property.Value.DeepClone();
            return row;
        }
    }
}
