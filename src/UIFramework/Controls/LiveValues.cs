using System;
using System.Globalization;
using System.Windows.Controls;
using Newtonsoft.Json.Linq;

namespace UIFramework
{
    /// <summary>Puts values into the window's controls as if the user had typed them: nothing is saved until Save.
    /// Used by an action whose answer carries <c>set</c> (a preset button filling in several fields). A control
    /// whose value changes glows for a moment (<see cref="Ui.Flash"/>).</summary>
    public static class LiveValues
    {
        public static void Apply(WindowParts parts, JObject values)
        {
            if (parts == null || values == null)
                return;
            foreach (var property in values.Properties())
                Set(parts, property.Name, property.Value);
        }

        public static bool Set(WindowParts parts, string key, JToken value)
        {
            if (parts.Switches.TryGetValue(key, out var toggle))
            {
                bool was = toggle.IsChecked == true;
                toggle.IsChecked = value != null && value.Type == JTokenType.Boolean ? (bool)value
                    : string.Equals(FieldBinding.Invariant(value), "true", StringComparison.OrdinalIgnoreCase);
                if (was != (toggle.IsChecked == true)) Ui.Flash(toggle);
                return true;
            }
            if (parts.Combos.TryGetValue(key, out var combo))
            {
                // The value as text the same on every Windows ("0.5", never "0,5"), numbers compared as numbers.
                string text = FieldBinding.Invariant(value);
                foreach (var raw in combo.Items)
                {
                    if (!(raw is ComboBoxItem item) || item.Tag == null) continue;
                    bool hit;
                    if (item.Tag is double dd)
                        hit = FieldBinding.TryParseFinite(text, out double want) && Math.Abs(dd - want) <= 1e-9 * Math.Max(1, Math.Abs(dd));
                    else if (item.Tag is int || item.Tag is long)
                        hit = FieldBinding.TryParseFinite(text, out double whole) && Convert.ToInt64(item.Tag, CultureInfo.InvariantCulture) == whole;
                    else
                    {
                        string option = item.Tag is IFormattable f ? f.ToString(null, CultureInfo.InvariantCulture) : item.Tag.ToString();
                        hit = string.Equals(option, text, StringComparison.OrdinalIgnoreCase)
                            || (FieldBinding.TryParseInvariantDouble(option, out double a) && FieldBinding.TryParseInvariantDouble(text, out double b) && Math.Abs(a - b) <= 1e-9 * Math.Max(1, Math.Abs(a)));
                    }
                    if (hit)
                    {
                        bool moved = !ReferenceEquals(combo.SelectedItem, item);
                        combo.SelectedItem = item;
                        if (moved) Ui.Flash(combo);
                        return true;
                    }
                }
                return false;
            }
            if (parts.Boxes.TryGetValue(key, out var box))
            {
                string was = box.Text;
                box.Text = box.Tag is FieldTag tag ? FieldBinding.Display(tag, value) : FieldBinding.Invariant(value);
                if (!string.Equals(was, box.Text, StringComparison.Ordinal)) Ui.Flash(box);
                return true;
            }
            if (parts.Sliders.TryGetValue(key, out var slider))
            {
                double at = slider.Value;
                if (!slider.Put(value)) return false;
                if (Math.Abs(at - slider.Value) > 1e-9) Ui.Flash(slider);
                return true;
            }
            if (parts.Lists.TryGetValue(key, out var list))
            {
                // A list takes its rows as an array, or as the JSON text of one (an action that built them as a string).
                JArray rows = value as JArray;
                if (rows == null && value?.Type == JTokenType.String)
                    try { rows = JArray.Parse((string)value); } catch (Exception) { return false; }
                if (rows == null) return false;
                string was = list.Serialized;
                list.Set(rows);
                if (!string.Equals(was, list.Serialized, StringComparison.Ordinal)) Ui.Flash(list);
                return true;
            }
            return false;
        }
    }
}
