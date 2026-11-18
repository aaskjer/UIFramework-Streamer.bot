using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Controls;
using Newtonsoft.Json.Linq;

namespace UIFramework
{
    /// <summary>
    /// A condition on the window's live values, behind <c>requires</c> (greys a field out) and <c>showWhen</c> (hides
    /// it): a key of a switch (<c>"freeGame"</c>), or a comparison on any field, <c>"numberOfSlots>=5"</c>,
    /// <c>"cpBetMode=fixed"</c>, <c>"cpBetMode!=free"</c>, with <c>=</c>, <c>!=</c>, <c>&lt;</c>, <c>&lt;=</c>,
    /// <c>&gt;</c>, <c>&gt;=</c>. <c>=</c> and <c>!=</c> take several values separated by <c>|</c>: one of them,
    /// none of them (<c>"questionSource=opentdb|wikidata"</c>). On a <c>multi</c> field <c>=</c> asks whether a value is
    /// ticked (<c>"platforms=steam"</c>), and <c>!platforms</c> holds while nothing is. Conditions joined by <c>&amp;&amp;</c> must all hold.
    /// The field follows its gates live; a gate the window does not show is read from the saved value, and one the
    /// settings do not know at all counts as holding.
    /// </summary>
    public static class Requirement
    {
        private static readonly string[] Operators = { "!=", ">=", "<=", "=", ">", "<" };

        /// <summary>One comparison: the gate's key, the operator (null for a switch's plain key), the wanted values.</summary>
        public sealed class Part
        {
            public string Key, Op;
            public string[] Values = new string[0];
            /// <summary>"!key": a switch that is off, a field that is empty.</summary>
            public bool Not;
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
                // The operator that comes first in the text, the longer one on a tie ("<=" before "<"), so a value
                // that contains an operator ("op=<=") does not split the key.
                int best = -1; string op = null;
                foreach (var candidate in Operators)
                {
                    int at = text.IndexOf(candidate, StringComparison.Ordinal);
                    if (at > 0 && (best < 0 || at < best || (at == best && candidate.Length > op.Length)))
                    {
                        best = at;
                        op = candidate;
                    }
                }
                if (op != null)
                {
                    part.Op = op;
                    part.Key = text.Substring(0, best).Trim();
                    part.Values = text.Substring(best + op.Length).Split('|').Select(v => v.Trim()).ToArray();
                }
                else if (text.StartsWith("!", StringComparison.Ordinal))
                {
                    part.Not = true;
                    part.Key = text.Substring(1).Trim();
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
            foreach (var c in comparisons)
                Listen(parts, c.Key, Apply);
            // Also when no gate is on the window: a hidden gate's saved value decides.
            Apply();
        }

        /// <summary>Calls <paramref name="changed"/> whenever the field of that key changes in the window. A key the
        /// window has no field for never does.</summary>
        public static void Listen(WindowParts parts, string key, Action changed)
        {
            if (parts.Switches.TryGetValue(key, out var toggle))
            {
                toggle.Checked += (sender, e) => changed();
                toggle.Unchecked += (sender, e) => changed();
            }
            else if (parts.Combos.TryGetValue(key, out var combo))
                combo.SelectionChanged += (sender, e) => changed();
            else if (parts.Boxes.TryGetValue(key, out var box))
                box.TextChanged += (sender, e) => changed();
            else if (parts.Sliders.TryGetValue(key, out var slider))
                slider.ValueChanged += (sender, e) => changed();
            else if (parts.Lists.TryGetValue(key, out var list))
                list.Changed += changed;   // "a list is empty" follows the rows as they are added and removed
        }

        /// <summary>A setting's value as the window has it now, saved or not, in the form it would be stored in (as
        /// <see cref="Holds"/> reads a gate): a switch as true or false, a list as its rows, anything else as its text.
        /// A setting the window has no field for gives its saved value; null when the settings do not know the key.</summary>
        public static JToken Live(Fields f, WindowParts parts, string key)
        {
            if (parts.Switches.ContainsKey(key)) return f.LiveSwitch(key);
            if (parts.Boxes.TryGetValue(key, out var box))
                return FieldTag.Of(box.Tag) is FieldTag tag ? FieldBinding.StoredText(tag, box.Text) : box.Text;
            if (parts.Combos.ContainsKey(key) || parts.Sliders.ContainsKey(key)) return f.LiveText(key, "");
            if (parts.Lists.TryGetValue(key, out var list)) return list.Peek;
            return f.Token(key);
        }

        /// <summary>Whether one comparison holds for the live value of its gate. A gate the window does not show (hidden
        /// in the customise mode, on no page) is read from the saved value; one the settings do not know at all holds.
        /// Either way the value compared is the one that would be stored: a percent as its fraction, a duration in
        /// seconds, an "unlimited" 0 as the large number it is saved as, so a gate compares the same shown or hidden.
        /// An empty list counts as empty.</summary>
        public static bool Holds(Fields f, WindowParts parts, Part c)
        {
            bool present = parts.Switches.ContainsKey(c.Key) || parts.Combos.ContainsKey(c.Key) || parts.Boxes.ContainsKey(c.Key) || parts.Sliders.ContainsKey(c.Key);
            // A list on the window counts by the rows it shows now, read without ending an edit in it.
            var saved = present ? null : parts.Lists.TryGetValue(c.Key, out var shown) ? shown.Peek : f.Token(c.Key);
            if (!present && (saved == null || saved.Type == JTokenType.Null)) return true;
            bool isSwitch = present ? parts.Switches.ContainsKey(c.Key) : saved.Type == JTokenType.Boolean;
            if (isSwitch)
            {
                bool on = present ? f.LiveSwitch(c.Key) : (bool)saved;
                if (c.Op == null) return c.Not ? !on : on;
                bool want = c.Values.Any(v => string.Equals(v, "true", StringComparison.OrdinalIgnoreCase) || v == "1");
                return c.Op == "!=" ? on != want : on == want;
            }
            string text;
            if (present && parts.Boxes.TryGetValue(c.Key, out var box) && FieldTag.Of(box.Tag) is FieldTag tag)
                text = FieldBinding.StoredText(tag, box.Text);
            else if (present)
                text = f.LiveText(c.Key, "");
            else
                text = saved is JContainer container && !container.HasValues ? "" : FieldBinding.Invariant(saved);
            text = text.Trim();
            // A multi field holds several values at once: "key=steam" holds while steam is ticked, "key=steam|gog" while
            // one of them is, "key!=steam|gog" while none of them is; "key" while anything is ticked, "!key" while nothing is.
            if (parts.Choices.TryGetValue(c.Key, out var choices) && (c.Op == null || c.Op == "=" || c.Op == "!="))
            {
                var ticked = FieldBinding.MultiValues(choices, present ? text : FieldBinding.MultiText(choices, saved));
                if (c.Op == null) return c.Not ? ticked.Count == 0 : ticked.Count > 0;
                bool any = c.Values.Any(wanted => ticked.Contains(wanted, StringComparer.OrdinalIgnoreCase));
                return c.Op == "=" ? any : !any;
            }
            if (c.Op == null)
                return c.Not ? text.Length == 0 : text.Length > 0;
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
    }
}
