using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Newtonsoft.Json.Linq;

namespace UIFramework
{
    /// <summary>
    /// Moves values between the controls and the settings object. A control takes part when its Tag is a
    /// <see cref="FieldTag"/>; the tag says which key and how the text is read. The same walk serves saving,
    /// resetting to defaults, counting unsaved changes and validating the numbers.
    /// </summary>
    public static class FieldBinding
    {
        public static void VisitTree(object node, Action<object> visitor)
        {
            if (node == null)
                return;
            visitor(node);

            if (node is Decorator d)
                VisitTree(d.Child, visitor);
            else if (node is Panel p)
            {
                foreach (var child in p.Children)
                    VisitTree(child, visitor);
            }
            else if (node is ContentControl cc && !(node is ComboBoxItem))
                VisitTree(cc.Content, visitor);
        }

        /// <summary>Writes every tagged control's value into the settings object.</summary>
        public static void Save(JObject settings, object root)
        {
            VisitTree(root, node =>
            {
                if (node is CheckBox chk && FieldTag.Of(chk.Tag) is FieldTag ct)
                    settings[ct.Key] = chk.IsChecked ?? false;
                else if (node is TextBox txt && FieldTag.Of(txt.Tag) is FieldTag tt)
                    settings[tt.Key] = ValueOf(tt, txt.Text);
                else if (node is ComboBox cmb && FieldTag.Of(cmb.Tag) is FieldTag mt && cmb.SelectedItem is ComboBoxItem selected)
                    settings[mt.Key] = ComboValue(mt, selected.Tag);
                else if (node is ListEditor list && FieldTag.Of(list.Tag) is FieldTag lt)
                    settings[lt.Key] = list.Value;
            });
        }

        private static JToken ValueOf(FieldTag tag, string text)
        {
            string trimmed = (text ?? "").Trim();
            switch (tag.Kind)
            {
                case FieldKind.Int:
                    if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i))
                        return tag.UnlimitedAtZero && i == 0 ? int.MaxValue : i;
                    return IsDigits(trimmed) ? int.MaxValue : 0;
                case FieldKind.Long:
                    if (long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out long l))
                        return tag.UnlimitedAtZero && l == 0 ? long.MaxValue : l;
                    return IsDigits(trimmed) ? long.MaxValue : 0L;
                case FieldKind.Double:
                    double parsed = TryParseFinite(text, out double d) ? d : 0.0;
                    parsed = Math.Max(tag.Min, Math.Min(tag.Max, parsed)) * tag.Factor;
                    return parsed;
                case FieldKind.Bool:
                    return string.Equals(trimmed, "true", StringComparison.OrdinalIgnoreCase);
                case FieldKind.Template:
                    return text ?? "";
                default:
                    return trimmed;
            }
        }

        private static JToken ComboValue(FieldTag tag, object itemTag)
        {
            switch (tag.Kind)
            {
                case FieldKind.Int: return itemTag is int ii ? ii : 0;
                case FieldKind.Long: return itemTag is long ll ? ll : itemTag is int il ? il : 0L;
                case FieldKind.Double: return itemTag is double dd ? dd : 0.0;
                case FieldKind.Bool: return itemTag is bool bb && bb;
                default: return itemTag?.ToString() ?? "";
            }
        }

        /// <summary>The text a control shows for a stored value.</summary>
        public static string Display(FieldTag tag, JToken value)
        {
            switch (tag.Kind)
            {
                case FieldKind.Double:
                    double d = value == null || value.Type == JTokenType.Null ? 0.0 : Convert.ToDouble(((JValue)value).Value, CultureInfo.InvariantCulture);
                    double factor = tag.Factor != 0 ? tag.Factor : 1.0;
                    return (d / factor).ToString("0.##", CultureInfo.InvariantCulture);
                case FieldKind.Long:
                    long l = value == null || value.Type == JTokenType.Null ? 0L : Convert.ToInt64(((JValue)value).Value, CultureInfo.InvariantCulture);
                    return (tag.UnlimitedAtZero && l == long.MaxValue ? 0L : l).ToString(CultureInfo.InvariantCulture);
                case FieldKind.Int:
                    long i = value == null || value.Type == JTokenType.Null ? 0L : Convert.ToInt64(((JValue)value).Value, CultureInfo.InvariantCulture);
                    return (tag.UnlimitedAtZero && i == int.MaxValue ? 0L : i).ToString(CultureInfo.InvariantCulture);
                default:
                    return value == null || value.Type == JTokenType.Null ? "" : value.Type == JTokenType.String ? (string)value : value.ToString();
            }
        }

        public static void ResetToDefaults(UIElement page, JObject defaults)
        {
            VisitTree(page, node =>
            {
                if (node is CheckBox chk && FieldTag.Of(chk.Tag) is FieldTag ct)
                {
                    var want = defaults[ct.Key];
                    chk.IsChecked = want != null && want.Type == JTokenType.Boolean && (bool)want;
                    return;
                }

                if (node is ListEditor list && FieldTag.Of(list.Tag) is FieldTag lt)
                {
                    list.Set(defaults[lt.Key] as JArray);
                    return;
                }

                if (node is TextBox txt && FieldTag.Of(txt.Tag) is FieldTag tt)
                {
                    txt.Text = Display(tt, defaults[tt.Key]);
                    return;
                }

                if (node is ComboBox cmb && FieldTag.Of(cmb.Tag) is FieldTag mt)
                {
                    var want = defaults[mt.Key];
                    foreach (var raw in cmb.Items)
                    {
                        if (!(raw is ComboBoxItem item) || item.Tag == null) continue;
                        bool hit;
                        if (mt.Kind == FieldKind.Double)
                            hit = item.Tag is double dd && want != null && Math.Abs(dd - Convert.ToDouble(((JValue)want).Value, CultureInfo.InvariantCulture)) < 0.001;
                        else if (mt.Kind == FieldKind.Int || mt.Kind == FieldKind.Long)
                            hit = want != null && Convert.ToInt64(item.Tag, CultureInfo.InvariantCulture) == Convert.ToInt64(((JValue)want).Value, CultureInfo.InvariantCulture);
                        else
                            hit = string.Equals(item.Tag.ToString(), want == null ? "" : want.ToString(), StringComparison.OrdinalIgnoreCase);
                        if (hit) { cmb.SelectedItem = item; break; }
                    }
                }
            });
        }

        public static string ControlValue(object node)
        {
            if (node is CheckBox chk && chk.Tag is FieldTag)
                return (chk.IsChecked ?? false).ToString();
            if (node is TextBox txt && txt.Tag is FieldTag)
                return txt.Text ?? "";
            if (node is ComboBox cmb && cmb.Tag is FieldTag)
                return cmb.SelectedItem is ComboBoxItem item && item.Tag != null ? item.Tag.ToString() : "";
            if (node is ListEditor list && list.Tag is FieldTag)
                return list.Serialized;
            return null;
        }

        public static void Snapshot(UIElement root, Dictionary<object, string> into)
        {
            VisitTree(root, obj =>
            {
                string v = ControlValue(obj);
                if (v != null)
                    into[obj] = v;
            });
        }

        public static int CountChanged(UIElement root, Dictionary<object, string> baseline)
        {
            int changed = 0;
            VisitTree(root, obj =>
            {
                string v = ControlValue(obj);
                if (v == null) return;
                if (baseline.TryGetValue(obj, out string was) && !string.Equals(was, v, StringComparison.Ordinal))
                    changed++;
            });
            return changed;
        }

        public static void HookChangeTracking(UIElement root, Action onChanged)
        {
            VisitTree(root, obj =>
            {
                if (obj is CheckBox chk && chk.Tag is FieldTag)
                {
                    chk.Checked += (s, e) => onChanged();
                    chk.Unchecked += (s, e) => onChanged();
                }
                else if (obj is TextBox txt && txt.Tag is FieldTag)
                    txt.TextChanged += (s, e) => onChanged();
                else if (obj is ComboBox cmb && cmb.Tag is FieldTag)
                    cmb.SelectionChanged += (s, e) => onChanged();
                else if (obj is ListEditor list && list.Tag is FieldTag)
                    list.Changed += onChanged;
            });
        }

        /// <summary>Number fields whose text is not a usable number, as "Label: 'text'" lines.</summary>
        public static List<string> InvalidNumberFields(object root)
        {
            var invalid = new List<string>();
            VisitTree(root, o =>
            {
                if (!(o is TextBox box) || !(FieldTag.Of(box.Tag) is FieldTag tag)) return;
                if (tag.Kind != FieldKind.Int && tag.Kind != FieldKind.Long && tag.Kind != FieldKind.Double) return;
                string text = box.Text.Trim();
                bool valid = tag.Kind == FieldKind.Double ? TryParseFinite(text, out _) : IsDigits(text);
                if (!valid)
                    invalid.Add(FieldLabel(box) + (text.Length == 0 ? ": empty" : ": '" + text + "'"));
            });
            return invalid;
        }

        public static string FieldLabel(TextBox box)
        {
            string name = AutomationProperties.GetName(box);
            return string.IsNullOrWhiteSpace(name) ? box.Tag?.ToString() ?? "" : name;
        }

        public static bool IsDigits(string text) => !string.IsNullOrEmpty(text) && text.All(c => c >= '0' && c <= '9');

        public static bool TryParseInvariantDouble(string text, out double value)
        {
            string normalized = (text ?? "").Trim().TrimEnd('%').Trim().Replace(",", ".");
            return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        public static bool TryParseFinite(string text, out double value) =>
            TryParseInvariantDouble(text, out value) && !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
