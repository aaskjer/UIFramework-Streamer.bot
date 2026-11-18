using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace UIFramework
{
    /// <summary>
    /// Which rows of a list the window shows (<c>filter</c>): conditions joined by <c>||</c>, of which one must hold.
    /// A condition is <c>left=value</c>, <c>left!=value</c> or <c>left~value</c>. <c>left</c> is a column of the list
    /// when it has one of that name, else the key of a setting, read as the window has it now. <c>value</c> is a
    /// literal in which <c>{key}</c> stands for that setting's value. <c>=</c> and <c>!=</c> compare as text ignoring
    /// case (true or false for a switch); <c>~</c> is "contains": the text holds the value, or one element of a JSON
    /// array equals it. An empty value compares with empty: <c>region=</c> holds while no region is picked, and
    /// <c>Counties~</c> for a row that names none. A condition that cannot be read (no operator, a left side or a
    /// <c>{key}</c> that is nothing known; <see cref="Resolve"/> names them) holds, so the rows show rather than vanish.
    /// <para>"region= || Global=true || Counties~{region}": every row while no region is picked, otherwise the rows
    /// that hold everywhere or name the region. A row that does not show stays in the list and is saved back.</para>
    /// </summary>
    public sealed class ListFilter
    {
        private static readonly string[] Operators = { "!=", "=", "~" };
        private static readonly Regex Placeholder = new Regex(@"\{([^{}]+)\}", RegexOptions.Compiled);

        public sealed class Condition
        {
            /// <summary>The condition as declared; its left side, operator (null when it has none) and value.</summary>
            public string Text, Left, Op, Value;
        }

        public readonly List<Condition> Conditions = new List<Condition>();

        /// <summary>The conditions of a filter, or null when it declares none.</summary>
        public static ListFilter Parse(string filter)
        {
            var parsed = new ListFilter();
            foreach (var raw in (filter ?? "").Split(new[] { "||" }, StringSplitOptions.None))
            {
                string text = raw.Trim();
                if (text.Length == 0) continue;
                var condition = new Condition { Text = text, Left = text, Value = "" };
                // The operator that comes first, "!=" before the "=" inside it.
                int best = -1;
                foreach (var candidate in Operators)
                {
                    int at = text.IndexOf(candidate, StringComparison.Ordinal);
                    if (at >= 0 && (best < 0 || at < best))
                    {
                        best = at;
                        condition.Op = candidate;
                    }
                }
                if (condition.Op != null)
                {
                    condition.Left = text.Substring(0, best).Trim();
                    condition.Value = text.Substring(best + condition.Op.Length).Trim();
                }
                parsed.Conditions.Add(condition);
            }
            return parsed.Conditions.Count == 0 ? null : parsed;
        }

        /// <summary>The settings a value names as <c>{key}</c>.</summary>
        public static IEnumerable<string> Keys(string value) =>
            Placeholder.Matches(value ?? "").Cast<Match>().Select(match => match.Groups[1].Value.Trim());

        /// <summary>The settings the filter reads: left sides that are no column, and every <c>{key}</c>.</summary>
        public IEnumerable<string> Settings(IList<SettingColumn> columns) =>
            Conditions.Where(c => c.Op != null)
                .SelectMany(c => (IsColumn(columns, c.Left) ? Enumerable.Empty<string>() : new[] { c.Left }).Concat(Keys(c.Value)))
                .Where(key => key.Length > 0).Distinct(StringComparer.Ordinal);

        /// <summary>Whether a row shows. <paramref name="setting"/> gives a setting's value as the window has it, null
        /// for a key that is no setting.</summary>
        public bool Shows(JObject row, IList<SettingColumn> columns, Func<string, JToken> setting) =>
            Conditions.Any(condition => Holds(condition, row, columns, setting));

        private static bool Holds(Condition c, JObject row, IList<SettingColumn> columns, Func<string, JToken> setting)
        {
            if (c.Op == null) return true;
            JToken left;
            var column = columns?.FirstOrDefault(x => x.Key == c.Left);
            if (column != null)
                left = row?[c.Left] ?? (column.Kind == "bool" ? (JToken)false : null);
            else if ((left = setting?.Invoke(c.Left)) == null)
                return true;

            bool unknown = false;
            string want = Placeholder.Replace(c.Value, match =>
            {
                var value = setting?.Invoke(match.Groups[1].Value.Trim());
                if (value == null) unknown = true;
                return TextOf(value);
            }).Trim();
            if (unknown) return true;

            string have = TextOf(left);
            switch (c.Op)
            {
                case "=": return string.Equals(have, want, StringComparison.OrdinalIgnoreCase);
                case "!=": return !string.Equals(have, want, StringComparison.OrdinalIgnoreCase);
                default:
                    if (want.Length == 0) return have.Length == 0;
                    if (left is JArray array) return array.Any(element => string.Equals(TextOf(element), want, StringComparison.OrdinalIgnoreCase));
                    return have.IndexOf(want, StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }

        private static bool IsColumn(IList<SettingColumn> columns, string key) => columns != null && columns.Any(column => column.Key == key);

        /// <summary>A value as the conditions compare it: text without the blanks around it, true or false, an array
        /// as its elements separated by commas.</summary>
        private static string TextOf(JToken value) =>
            value is JArray array ? ListMerge.Shown(array).Trim() : FieldBinding.Invariant(value).Trim();

        /// <summary>Gives the list its filter and applies it again whenever a setting it reads changes in the window.</summary>
        public void Wire(Fields f, WindowParts parts, IList<SettingColumn> columns, ListEditor editor)
        {
            editor.RowFilter = row => Shows(row, columns, key => Requirement.Live(f, parts, key));
            string own = FieldTag.Of(editor.Tag)?.Key;
            foreach (string key in Settings(columns))
                if (key != own)
                    Requirement.Listen(parts, key, editor.Refilter);
        }
    }
}
