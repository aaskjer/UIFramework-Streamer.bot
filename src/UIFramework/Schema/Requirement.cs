using System;
using System.Globalization;
using System.Windows.Controls;

namespace UIFramework
{
    /// <summary>
    /// The <c>requires</c> of a setting: a key of a switch (<c>"freeGame"</c>), or a condition on any field,
    /// <c>"numberOfSlots>=5"</c>, <c>"cpBetMode=fixed"</c>, <c>"cpBetMode!=free"</c>, with <c>=</c>, <c>!=</c>,
    /// <c>&lt;</c>, <c>&lt;=</c>, <c>&gt;</c>, <c>&gt;=</c>. The field is enabled while the condition holds and follows
    /// the gate live. A gate the window does not have leaves the field enabled.
    /// </summary>
    public static class Requirement
    {
        private static readonly string[] Operators = { "!=", ">=", "<=", "=", ">", "<" };

        public static void Wire(Fields f, WindowParts parts, string requires, Action<bool> apply)
        {
            string key = requires.Trim(), op = null, wanted = null;
            foreach (var candidate in Operators)
            {
                int at = key.IndexOf(candidate, StringComparison.Ordinal);
                if (at > 0)
                {
                    op = candidate;
                    wanted = key.Substring(at + candidate.Length).Trim();
                    key = key.Substring(0, at).Trim();
                    break;
                }
            }

            void Apply() => apply(Holds(f, parts, key, op, wanted));
            if (parts.Switches.TryGetValue(key, out var toggle))
            {
                toggle.Checked += (sender, e) => Apply();
                toggle.Unchecked += (sender, e) => Apply();
            }
            else if (parts.Combos.TryGetValue(key, out var combo))
                combo.SelectionChanged += (sender, e) => Apply();
            else if (parts.Boxes.TryGetValue(key, out var box))
                box.TextChanged += (sender, e) => Apply();
            else
                return;
            Apply();
        }

        /// <summary>Whether the condition holds for the live value of the gate.</summary>
        public static bool Holds(Fields f, WindowParts parts, string key, string op, string wanted)
        {
            if (parts.Switches.ContainsKey(key))
            {
                bool on = f.LiveSwitch(key);
                if (op == null) return on;
                bool want = string.Equals(wanted, "true", StringComparison.OrdinalIgnoreCase) || wanted == "1";
                return op == "!=" ? on != want : on == want;
            }
            string text = f.LiveText(key, "");
            if (op == null)
                return !string.IsNullOrWhiteSpace(text);
            double have = 0, want2 = 0;
            bool numeric = FieldBinding.TryParseInvariantDouble(text, out have) && FieldBinding.TryParseInvariantDouble(wanted, out want2);
            switch (op)
            {
                case "=": return numeric ? Math.Abs(have - want2) < 1e-9 : string.Equals(text.Trim(), wanted, StringComparison.OrdinalIgnoreCase);
                case "!=": return numeric ? Math.Abs(have - want2) >= 1e-9 : !string.Equals(text.Trim(), wanted, StringComparison.OrdinalIgnoreCase);
                case ">": return numeric && have > want2;
                case ">=": return numeric && have >= want2;
                case "<": return numeric && have < want2;
                case "<=": return numeric && have <= want2;
                default: return true;
            }
        }
    }
}
