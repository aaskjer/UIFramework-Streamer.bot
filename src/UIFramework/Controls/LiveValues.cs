using System;
using System.Globalization;
using System.Windows.Controls;
using Newtonsoft.Json.Linq;

namespace UIFramework
{
    /// <summary>Puts values into the window's controls as if the user had typed them: nothing is saved until Save.
    /// Used by an action whose answer carries <c>set</c> (a preset button filling in several fields).</summary>
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
                toggle.IsChecked = value != null && value.Type == JTokenType.Boolean ? (bool)value
                    : string.Equals(value?.ToString(), "true", StringComparison.OrdinalIgnoreCase);
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
                    if (hit) { combo.SelectedItem = item; return true; }
                }
                return false;
            }
            if (parts.Boxes.TryGetValue(key, out var box))
            {
                box.Text = box.Tag is FieldTag tag ? FieldBinding.Display(tag, value) : value?.ToString() ?? "";
                return true;
            }
            return false;
        }
    }
}
