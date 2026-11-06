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
                    : string.Equals(value?.ToString(), "true", StringComparison.OrdinalIgnoreCase);
                if (was != (toggle.IsChecked == true)) Ui.Flash(toggle);
                return true;
            }
            if (parts.Combos.TryGetValue(key, out var combo))
            {
                foreach (var raw in combo.Items)
                {
                    if (!(raw is ComboBoxItem item) || item.Tag == null) continue;
                    bool hit;
                    if (item.Tag is double dd)
                        hit = value != null && double.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double want) && Math.Abs(dd - want) < 0.001;
                    else if (item.Tag is int || item.Tag is long)
                        hit = value != null && long.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long whole) && Convert.ToInt64(item.Tag, CultureInfo.InvariantCulture) == whole;
                    else
                        hit = string.Equals(item.Tag.ToString(), value?.ToString() ?? "", StringComparison.OrdinalIgnoreCase);
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
                box.Text = box.Tag is FieldTag tag ? FieldBinding.Display(tag, value) : value?.ToString() ?? "";
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
