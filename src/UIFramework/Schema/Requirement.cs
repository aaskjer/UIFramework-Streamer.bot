using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Controls;

namespace UIFramework
{
    /// <summary>
    /// A condition on the window's live values, behind <c>requires</c> (greys a field out) and <c>showWhen</c> (hides
    /// it): a key of a switch (<c>"freeGame"</c>), or a comparison on any field, <c>"numberOfSlots>=5"</c>,
    /// <c>"cpBetMode=fixed"</c>, <c>"cpBetMode!=free"</c>, with <c>=</c>, <c>!=</c>, <c>&lt;</c>, <c>&lt;=</c>,
    /// <c>&gt;</c>, <c>&gt;=</c>. <c>=</c> and <c>!=</c> take several values separated by <c>|</c>: one of them,
    /// none of them (<c>"questionSource=opentdb|wikidata"</c>). Conditions joined by <c>&amp;&amp;</c> must all hold.
    /// The field follows its gates live; a gate the window does not have counts as holding.
    /// </summary>
    public static class Requirement
    {
        private static readonly string[] Operators = { "!=", ">=", "<=", "=", ">", "<" };

        /// <summary>One comparison: the gate's key, the operator (null for a switch's plain key), the wanted values.</summary>
        public sealed class Part
        {
            public string Key, Op;
            public string[] Values = new string[0];
        }

        /// <summary>The comparisons of a condition, in order; an empty or null condition has none.</summary>
        public static List<Part> Parse(string condition)
        {
            var parts = new List<Part>();
            foreach (var raw in (condition ?? "").Split(new[] { "&&" }, StringSplitOptions.RemoveEmptyEntries))
            {
                string text = raw.Trim();
                if (text.Length == 0) continue;
                var part = new Part { Key = text };
                foreach (var candidate in Operators)
                {
                    int at = text.IndexOf(candidate, StringComparison.Ordinal);
                    if (at > 0)
                    {
                        part.Op = candidate;
                        part.Key = text.Substring(0, at).Trim();
                        part.Values = text.Substring(at + candidate.Length).Split('|').Select(v => v.Trim()).ToArray();
                        break;
                    }
                }
                parts.Add(part);
            }
            return parts;
        }

        /// <summary>Calls <paramref name="apply"/> with whether the condition holds, now and whenever a gate changes.</summary>
        public static void Wire(Fields f, WindowParts parts, string condition, Action<bool> apply)
        {
            var comparisons = Parse(condition);
            if (comparisons.Count == 0) return;
            void Apply() => apply(comparisons.All(c => Holds(f, parts, c)));
            bool any = false;
            foreach (var c in comparisons)
            {
                if (parts.Switches.TryGetValue(c.Key, out var toggle))
                {
                    toggle.Checked += (sender, e) => Apply();
                    toggle.Unchecked += (sender, e) => Apply();
                }
                else if (parts.Combos.TryGetValue(c.Key, out var combo))
                    combo.SelectionChanged += (sender, e) => Apply();
                else if (parts.Boxes.TryGetValue(c.Key, out var box))
                    box.TextChanged += (sender, e) => Apply();
                else if (parts.Sliders.TryGetValue(c.Key, out var slider))
                    slider.ValueChanged += (sender, e) => Apply();
                else
                    continue;
                any = true;
            }
            if (any) Apply();
        }

        /// <summary>Whether one comparison holds for the live value of its gate. A gate the window lacks holds.</summary>
        public static bool Holds(Fields f, WindowParts parts, Part c)
        {
            bool present = parts.Switches.ContainsKey(c.Key) || parts.Combos.ContainsKey(c.Key) || parts.Boxes.ContainsKey(c.Key) || parts.Sliders.ContainsKey(c.Key);
            if (!present) return true;
            if (parts.Switches.ContainsKey(c.Key))
            {
                bool on = f.LiveSwitch(c.Key);
                if (c.Op == null) return on;
                bool want = c.Values.Any(v => string.Equals(v, "true", StringComparison.OrdinalIgnoreCase) || v == "1");
                return c.Op == "!=" ? on != want : on == want;
            }
            string text = f.LiveText(c.Key, "").Trim();
            if (c.Op == null)
                return text.Length > 0;
            bool Same(string wanted) =>
                FieldBinding.TryParseInvariantDouble(text, out double a) && FieldBinding.TryParseInvariantDouble(wanted, out double b)
                    ? Math.Abs(a - b) < 1e-9
                    : string.Equals(text, wanted, StringComparison.OrdinalIgnoreCase);
            switch (c.Op)
            {
                case "=": return c.Values.Any(Same);
                case "!=": return !c.Values.Any(Same);
            }
            if (!FieldBinding.TryParseInvariantDouble(text, out double have) || c.Values.Length == 0 || !FieldBinding.TryParseInvariantDouble(c.Values[0], out double limit))
                return false;
            switch (c.Op)
            {
                case ">": return have > limit;
                case ">=": return have >= limit;
                case "<": return have < limit;
                case "<=": return have <= limit;
                default: return true;
            }
        }

        /// <summary>Kept for callers that pass a single comparison as key, operator and wanted text.</summary>
        public static bool Holds(Fields f, WindowParts parts, string key, string op, string wanted) =>
            Holds(f, parts, new Part { Key = key, Op = op, Values = (wanted ?? "").Split('|').Select(v => v.Trim()).ToArray() });
    }
}
