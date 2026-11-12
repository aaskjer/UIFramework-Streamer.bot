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

        /// <summary>Writes every tagged control's value into the settings object. Transient fields are left out
        /// unless asked for: a save never carries them, a live snapshot for the host does.</summary>
        public static void Save(JObject settings, object root, bool transientToo = false)
        {
            VisitTree(root, node =>
            {
                if (node is CheckBox chk && FieldTag.Of(chk.Tag) is FieldTag ct && (transientToo || !ct.Transient))
                    settings[ct.Key] = chk.IsChecked ?? false;
                else if (node is TextBox txt && FieldTag.Of(txt.Tag) is FieldTag tt && (transientToo || !tt.Transient))
                {
                    // No number in a number box: only one hidden by a condition gets this far (the check before saving
                    // names a shown one). It keeps what is stored instead of saving 0.
                    if (!transientToo && IsNumberKind(tt) && !IsValidNumber(tt, txt.Text))
                        return;
                    settings[tt.Key] = ValueOf(tt, txt.Text, out bool clamped);
                    // A number pulled into its min/max shows what was saved, so the box and the file never differ.
                    if (clamped && !transientToo)
                        txt.Text = Display(tt, settings[tt.Key]);
                }
                else if (node is ComboBox cmb && FieldTag.Of(cmb.Tag) is FieldTag mt && (transientToo || !mt.Transient) && cmb.SelectedItem is ComboBoxItem selected)
                    settings[mt.Key] = ComboValue(mt, selected.Tag);
                else if (node is ListEditor list && FieldTag.Of(list.Tag) is FieldTag lt && (transientToo || !lt.Transient))
                    settings[lt.Key] = transientToo ? list.Peek : list.Value;   // a live snapshot leaves a cell being edited alone
                else if (node is OptionSlider slider && FieldTag.Of(slider.Tag) is FieldTag st && (transientToo || !st.Transient))
                    settings[st.Key] = slider.Stored;
            });
        }

        /// <summary>Marks every tagged control under the element as transient.</summary>
        public static void MarkTransient(object root)
        {
            VisitTree(root, node =>
            {
                var tag = node is CheckBox chk ? FieldTag.Of(chk.Tag)
                        : node is TextBox txt ? FieldTag.Of(txt.Tag)
                        : node is ComboBox cmb ? FieldTag.Of(cmb.Tag)
                        : node is OptionSlider slider ? FieldTag.Of(slider.Tag)
                        : node is ListEditor list ? FieldTag.Of(list.Tag) : null;
                if (tag != null) tag.Transient = true;
            });
        }

        private static JToken ValueOf(FieldTag tag, string text) => ValueOf(tag, text, out _);

        /// <summary>A box's text as the value it would be stored as, in invariant text: what a condition on the field
        /// compares, the same whether the field is shown or read from the file.</summary>
        public static string StoredText(FieldTag tag, string text) =>
            tag.Kind == FieldKind.Template || tag.Kind == FieldKind.String ? text ?? "" : Invariant(ValueOf(tag, text));

        /// <summary>The stored value of a box's text; <paramref name="clamped"/> says the number was pulled into the
        /// setting's min/max, so the box can show what was really saved.</summary>
        private static JToken ValueOf(FieldTag tag, string text, out bool clamped)
        {
            clamped = false;
            string trimmed = (text ?? "").Trim();
            switch (tag.Kind)
            {
                case FieldKind.Int when tag.Factor != 1.0 && tag.Factor > 0:
                {
                    // A duration typed in minutes or hours: stored as whole seconds, the limits in seconds too.
                    double typed = TryParseTyped(trimmed, out double t) ? t : 0.0;
                    if (tag.UnlimitedAtZero && typed == 0) return int.MaxValue;
                    double seconds = Math.Round(typed * tag.Factor, MidpointRounding.AwayFromZero);
                    long whole = seconds >= int.MaxValue ? int.MaxValue : seconds <= int.MinValue ? int.MinValue : (long)seconds;
                    long kept = ClampWhole(tag, whole, int.MinValue, int.MaxValue);
                    clamped = kept != whole;
                    return (int)kept;
                }
                case FieldKind.Int:
                {
                    if (!long.TryParse(trimmed, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long whole))
                        whole = IsDigits(trimmed) ? int.MaxValue : 0;          // more digits than an int holds
                    if (tag.UnlimitedAtZero && whole == 0) return int.MaxValue;
                    long kept = ClampWhole(tag, whole, int.MinValue, int.MaxValue);
                    clamped = kept != whole;
                    return (int)kept;
                }
                case FieldKind.Long:
                {
                    if (!long.TryParse(trimmed, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long whole))
                        whole = IsDigits(trimmed) ? long.MaxValue : 0L;
                    if (tag.UnlimitedAtZero && whole == 0) return long.MaxValue;
                    long kept = ClampWhole(tag, whole, long.MinValue, long.MaxValue);
                    clamped = kept != whole;
                    return kept;
                }
                case FieldKind.Double:
                {
                    double parsed = TryParseTyped(text, out double d) ? d : 0.0;
                    double kept = Math.Max(tag.Min, Math.Min(tag.Max, parsed));
                    clamped = kept != parsed;
                    // A percent typed as 35 is 35 * 0.01, which is 0.35000000000000003: ten decimals, as the box
                    // shows, save 0.35. A number without a factor is saved as typed.
                    return tag.Factor == 1.0 ? kept : Math.Round(kept * tag.Factor, 10);
                }
                case FieldKind.Bool:
                    return string.Equals(trimmed, "true", StringComparison.OrdinalIgnoreCase);
                case FieldKind.Template:
                    return text ?? "";
                default:
                    return tag.KeepSpaces ? text ?? "" : trimmed;
            }
        }

        private static long ClampWhole(FieldTag tag, long value, long typeMin, long typeMax)
        {
            double min = Math.Max(tag.Min, typeMin), max = Math.Min(tag.Max, typeMax);
            if (value < min) return min <= typeMin ? typeMin : (long)Math.Ceiling(min);
            if (value > max) return max >= typeMax ? typeMax : (long)Math.Floor(max);
            return value;
        }

        private static JToken ComboValue(FieldTag tag, object itemTag)
        {
            switch (tag.Kind)
            {
                case FieldKind.Int: return itemTag is int ii ? ii : 0;
                case FieldKind.Long: return itemTag is long ll ? ll : itemTag is int il ? il : 0L;
                case FieldKind.Double: return itemTag is double dd ? dd : 0.0;
                case FieldKind.Bool: return itemTag is bool bb && bb;
                default: return itemTag is IFormattable f ? f.ToString(null, CultureInfo.InvariantCulture) : itemTag?.ToString() ?? "";
            }
        }

        /// <summary>A value as text the same on every Windows: numbers with a point, never "0,5" on a German system.</summary>
        public static string Invariant(JToken value)
        {
            if (value == null || value.Type == JTokenType.Null) return "";
            if (value.Type == JTokenType.String) return (string)value;
            if (value is JValue v && v.Value is IFormattable f)
                return v.Type == JTokenType.Float ? Convert.ToDouble(v.Value, CultureInfo.InvariantCulture).ToString("R", CultureInfo.InvariantCulture) : f.ToString(null, CultureInfo.InvariantCulture);
            if (value is JValue b && b.Type == JTokenType.Boolean) return (bool)b ? "true" : "false";
            return value.ToString(Newtonsoft.Json.Formatting.None);
        }

        /// <summary>A stored value as a number whatever form it came in: a number, a string "0.5" or "0,5" (one from a
        /// German system), false for anything else ("", "abc", an array), so a page never breaks on a bad value.</summary>
        public static bool TryNumber(JToken value, out double number)
        {
            number = 0;
            if (value == null || value.Type == JTokenType.Null) return false;
            if (value.Type == JTokenType.Integer || value.Type == JTokenType.Float)
            {
                if (!ToDouble(((JValue)value).Value, out number)) return false;
                return !double.IsNaN(number) && !double.IsInfinity(number);
            }
            return value.Type == JTokenType.String && TryParseFinite((string)value, out number);
        }

        /// <summary>A stored number as a double; a whole number with more digits than a long (Newtonsoft reads it as a
        /// BigInteger, which converts to nothing) through its text, so a page never breaks on it.</summary>
        public static bool ToDouble(object value, out double number)
        {
            number = 0;
            if (value is IConvertible)
            {
                try { number = Convert.ToDouble(value, CultureInfo.InvariantCulture); return true; }
                catch (Exception ex) when (ex is InvalidCastException || ex is FormatException || ex is OverflowException) { }
            }
            string text = value is IFormattable f ? f.ToString(null, CultureInfo.InvariantCulture) : value?.ToString();
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number);
        }

        /// <summary>A stored whole number exactly (a number token or digits as text); false for a fraction, anything
        /// else or a number beyond a long.</summary>
        public static bool TryLong(JToken value, out long number)
        {
            number = 0;
            if (value == null) return false;
            if (value.Type == JTokenType.Integer)
            {
                try { number = (long)value; return true; }
                catch (Exception) { return false; }
            }
            return value.Type == JTokenType.String && long.TryParse(((string)value).Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out number);
        }

        /// <summary>The text a control shows for a stored value.</summary>
        public static string Display(FieldTag tag, JToken value)
        {
            switch (tag.Kind)
            {
                case FieldKind.Double:
                {
                    // A value that is no number ("", "abc", an array) shows empty instead of breaking the page; the
                    // check before saving then names the field.
                    if (!TryNumber(value, out double d)) return value == null || value.Type == JTokenType.Null ? "0" : "";
                    double factor = tag.Factor != 0 ? tag.Factor : 1.0;
                    // Up to ten decimals: a stored 0.005 shows (and saves back) as 0.005, not 0.01; float noise like
                    // 10.000000000000002 still reads 10.
                    return (d / factor).ToString("0.##########", CultureInfo.InvariantCulture);
                }
                case FieldKind.Long:
                case FieldKind.Int:
                {
                    // A whole number as it is: through a double, one past 2^53 (an id) would lose its last digits.
                    if (tag.Factor == 1.0 && TryLong(value, out long exact))
                    {
                        long top = tag.Kind == FieldKind.Int ? int.MaxValue : long.MaxValue;
                        return tag.UnlimitedAtZero && exact >= top ? "0" : exact.ToString(CultureInfo.InvariantCulture);
                    }
                    if (!TryNumber(value, out double d)) return value == null || value.Type == JTokenType.Null ? "0" : "";
                    double ceiling = tag.Kind == FieldKind.Int ? int.MaxValue : long.MaxValue;
                    if (tag.UnlimitedAtZero && d >= ceiling) return "0";
                    // A duration in minutes or hours: the seconds shown in its unit, to the second (90 s reads 1.5).
                    if (tag.Factor != 1.0 && tag.Factor > 0)
                        return (d / tag.Factor).ToString("0.####", CultureInfo.InvariantCulture);
                    // A fraction in a whole-number field is shown as it is, not cut: the check before saving names it.
                    if (d != Math.Floor(d)) return d.ToString("0.##########", CultureInfo.InvariantCulture);
                    if (d >= long.MaxValue) return long.MaxValue.ToString(CultureInfo.InvariantCulture);
                    if (d <= long.MinValue) return long.MinValue.ToString(CultureInfo.InvariantCulture);
                    return ((long)d).ToString(CultureInfo.InvariantCulture);
                }
                default:
                    return Invariant(value);
            }
        }

        public static void ResetToDefaults(UIElement page, JObject defaults) => Apply(page, defaults, false);

        /// <summary>Puts the values into the controls as if typed: every control takes its key's value, or, with
        /// <paramref name="onlyPresent"/>, only the controls whose key the object names (an import). Returns the keys
        /// a control took.</summary>
        public static HashSet<string> Apply(UIElement page, JObject values, bool onlyPresent)
        {
            var placed = new HashSet<string>(StringComparer.Ordinal);
            VisitTree(page, node =>
            {
                var tag = node is CheckBox c ? FieldTag.Of(c.Tag)
                        : node is TextBox t ? FieldTag.Of(t.Tag)
                        : node is ComboBox m ? FieldTag.Of(m.Tag)
                        : node is OptionSlider s ? FieldTag.Of(s.Tag)
                        : node is ListEditor l ? FieldTag.Of(l.Tag) : null;
                if (tag == null || (onlyPresent && (values[tag.Key] == null || tag.Transient)))
                    return;
                placed.Add(tag.Key);

                if (node is CheckBox chk && FieldTag.Of(chk.Tag) is FieldTag ct)
                {
                    var want = values[ct.Key];
                    chk.IsChecked = want != null && (want.Type == JTokenType.Boolean ? (bool)want : string.Equals(want.ToString(), "true", StringComparison.OrdinalIgnoreCase));
                    return;
                }

                if (node is ListEditor list && FieldTag.Of(list.Tag) is FieldTag lt)
                {
                    list.Set(values[lt.Key] as JArray);
                    return;
                }

                if (node is OptionSlider slider && FieldTag.Of(slider.Tag) is FieldTag st)
                {
                    slider.Put(values[st.Key]);
                    return;
                }

                if (node is TextBox txt && FieldTag.Of(txt.Tag) is FieldTag tt)
                {
                    txt.Text = Display(tt, values[tt.Key]);
                    return;
                }

                if (node is ComboBox cmb && FieldTag.Of(cmb.Tag) is FieldTag mt)
                {
                    var want = values[mt.Key];
                    foreach (var raw in cmb.Items)
                    {
                        if (!(raw is ComboBoxItem item) || item.Tag == null) continue;
                        bool hit;
                        // A value that is no number matches no entry instead of breaking the reset.
                        if (mt.Kind == FieldKind.Double)
                            hit = item.Tag is double dd && TryNumber(want, out double wd) && Math.Abs(dd - wd) < 0.001;
                        else if (mt.Kind == FieldKind.Int || mt.Kind == FieldKind.Long)
                            hit = TryNumber(want, out double wl) && Convert.ToDouble(item.Tag, CultureInfo.InvariantCulture) == wl;
                        else
                            hit = string.Equals(item.Tag is IFormattable tf ? tf.ToString(null, CultureInfo.InvariantCulture) : item.Tag.ToString(), Invariant(want), StringComparison.OrdinalIgnoreCase);
                        if (hit) { cmb.SelectedItem = item; break; }
                    }
                }
            });
            return placed;
        }

        /// <summary>What a control holds, as text, for the change count; null for furniture and transient fields.</summary>
        public static string ControlValue(object node)
        {
            if (node is CheckBox chk && chk.Tag is FieldTag ct)
                return ct.Transient ? null : (chk.IsChecked ?? false).ToString();
            if (node is TextBox txt && txt.Tag is FieldTag tt)
                return tt.Transient ? null : txt.Text ?? "";
            if (node is ComboBox cmb && cmb.Tag is FieldTag mt)
                return mt.Transient ? null : cmb.SelectedItem is ComboBoxItem item && item.Tag != null ? (item.Tag is IFormattable f ? f.ToString(null, CultureInfo.InvariantCulture) : item.Tag.ToString()) : "";
            if (node is ListEditor list && list.Tag is FieldTag lt)
                return lt.Transient ? null : list.Serialized;
            if (node is OptionSlider slider && slider.Tag is FieldTag st)
                return st.Transient ? null : slider.StoredText;
            return null;
        }

        /// <summary>The keys of the tagged controls whose value differs from the baseline, or that the baseline does
        /// not know (a field built after it): what the user changed since the window opened or last saved.</summary>
        public static HashSet<string> ChangedKeys(object root, Dictionary<object, string> baseline)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            VisitTree(root, obj =>
            {
                string v = ControlValue(obj);
                if (v == null) return;
                var tag = obj is FrameworkElement fe ? FieldTag.Of(fe.Tag) : null;
                if (tag == null) return;
                if (baseline == null || !baseline.TryGetValue(obj, out string was) || !string.Equals(was, v, StringComparison.Ordinal))
                    keys.Add(tag.Key);
            });
            return keys;
        }

        /// <summary>The keys of the number boxes whose value, as the window would save it, is not the one the settings
        /// hold: a value outside the field's limits or not a number, which the box shows as its limit or its default.
        /// The window writes these with the next save, so the box and the file agree. A key the settings do not have
        /// is left to the game's own default.</summary>
        public static HashSet<string> Repaired(JObject settings, object root) => Repaired(settings, root, false);

        /// <summary>The same; with <paramref name="shownOtherwise"/> only the keys whose box reads other than the file's
        /// value would: a number pulled into its limits, text where a number belongs. A value that is merely stored
        /// in another form (a ceiling's 0 for "no limit", written as the type's largest number) is nothing to tell the
        /// user about.</summary>
        public static HashSet<string> Repaired(JObject settings, object root, bool shownOtherwise)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            VisitTree(root, node =>
            {
                if (!(node is FrameworkElement fe) || !(FieldTag.Of(fe.Tag) is FieldTag tag) || tag.Transient) return;
                var have = settings?[tag.Key];
                if (have == null || have.Type == JTokenType.Null) return;
                JToken would;
                if (node is TextBox txt)
                {
                    // Number boxes only: a text is shown as it is stored.
                    if (tag.Kind != FieldKind.Int && tag.Kind != FieldKind.Long && tag.Kind != FieldKind.Double) return;
                    would = ValueOf(tag, txt.Text);
                    if (shownOtherwise && Display(tag, have) == Display(tag, would)) return;
                }
                else if (node is CheckBox chk) would = chk.IsChecked ?? false;                    // "yes" shown as off
                else if (node is ComboBox cmb && cmb.SelectedItem is ComboBoxItem selected) would = ComboValue(tag, selected.Tag);
                else if (node is OptionSlider slider) would = slider.Stored;                      // a scale pulls into its range
                else return;
                if (!SameValue(have, would)) keys.Add(tag.Key);
            });
            return keys;
        }

        /// <summary>Two stored values the same: numbers by value (in any form, float noise aside), switches by
        /// value, anything else by its invariant text.</summary>
        private static bool SameValue(JToken a, JToken b)
        {
            if (TryLong(a, out long la) && TryLong(b, out long lb)) return la == lb;
            if (TryNumber(a, out double x) && TryNumber(b, out double y))
                return Math.Abs(x - y) <= 1e-9 * Math.Max(1, Math.Max(Math.Abs(x), Math.Abs(y)));
            if (a.Type == JTokenType.Boolean || b.Type == JTokenType.Boolean)
                return a.Type == b.Type && (bool)a == (bool)b;
            return string.Equals(Invariant(a), Invariant(b), StringComparison.Ordinal);
        }

        /// <summary>The keys the window has a field for that holds a value (not transient, not read-only): what it shows.</summary>
        public static HashSet<string> ShownKeys(object root)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            VisitTree(root, node =>
            {
                if (ControlValue(node) != null && node is FrameworkElement fe && FieldTag.Of(fe.Tag) is FieldTag tag && !tag.Transient)
                    keys.Add(tag.Key);
            });
            return keys;
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
                else if (obj is OptionSlider slider && slider.Tag is FieldTag)
                    slider.ValueChanged += (s, e) => onChanged();
            });
        }

        /// <summary>Number fields whose text is not a usable number, as "Label: 'text'" lines.</summary>
        /// <summary>A value from outside (an import) as the window would save it: through the field that shows its key,
        /// so a number is pulled into the field's limits, a switch is a switch and a dropdown one of its options.
        /// Returns why the value cannot be taken (in the window's language), or null with what to store in
        /// <paramref name="saved"/>. A key no field shows is taken as it is.</summary>
        public static string AsSaved(object root, string key, JToken value, out JToken saved)
        {
            JToken result = value;
            string problem = null;
            bool found = false;
            VisitTree(root, node =>
            {
                if (found || !(node is FrameworkElement fe) || !(FieldTag.Of(fe.Tag) is FieldTag tag) || tag.Transient || tag.Key != key) return;
                found = true;
                if (node is CheckBox)
                {
                    if (value != null && value.Type == JTokenType.Boolean) return;
                    string text = Invariant(value).Trim().ToLowerInvariant();
                    if (text == "true" || text == "false") result = text == "true";
                    else problem = Text.T("import.why.switch");
                }
                else if (node is TextBox && IsNumberKind(tag))
                {
                    string text = Display(tag, value);
                    if (value is JContainer || !IsValidNumber(tag, text)) problem = Text.T("import.why.number");
                    else result = ValueOf(tag, text);
                }
                else if (node is TextBox)
                {
                    if (value is JContainer) problem = Text.T("import.why.text");
                    else if (value == null || value.Type != JTokenType.String) result = Invariant(value);
                }
                else if (node is ComboBox cmb)
                {
                    string wanted = Invariant(value);
                    var item = cmb.Items.OfType<ComboBoxItem>().FirstOrDefault(i => string.Equals(Invariant(ComboValue(tag, i.Tag)), wanted, StringComparison.OrdinalIgnoreCase));
                    if (item == null) problem = Text.T("import.why.option");
                    else result = ComboValue(tag, item.Tag);
                }
                else if (node is ListEditor && !(value is JArray))
                    problem = Text.T("import.why.list");
            });
            saved = result;
            return problem;
        }

        public static List<string> InvalidNumberFields(object root)
        {
            var invalid = new List<string>();
            VisitTree(root, o =>
            {
                if (!(o is TextBox box) || !(FieldTag.Of(box.Tag) is FieldTag tag)) return;
                if (tag.Kind != FieldKind.Int && tag.Kind != FieldKind.Long && tag.Kind != FieldKind.Double) return;
                // Never saved, or hidden by a showWhen or a section's condition: nothing to stop a save for.
                if (tag.Transient || HiddenOnPage(box, root)) return;
                string text = box.Text.Trim();
                if (!IsValidNumber(tag, text))
                    invalid.Add(FieldLabel(box) + (text.Length == 0 ? ": " + Text.T("field.empty") : ": '" + text + "'"));
            });
            return invalid;
        }

        private static bool IsNumberKind(FieldTag tag) => tag.Kind == FieldKind.Int || tag.Kind == FieldKind.Long || tag.Kind == FieldKind.Double;

        /// <summary>Whether a number box's text is a number its field takes: a decimal, a whole number (with a minus
        /// where allowed), a duration in its unit.</summary>
        private static bool IsValidNumber(FieldTag tag, string text)
        {
            text = (text ?? "").Trim();
            return tag.Kind == FieldKind.Double ? TryParseTyped(text, out _)
                : tag.Factor != 1.0 ? TryParseTyped(text, out double typed) && (typed >= 0 || AllowsNegative(tag))
                : IsWhole(text, AllowsNegative(tag));
        }

        /// <summary>Whether something between the element and its page is collapsed (a showWhen, a section's condition).
        /// The page itself being collapsed (another page is showing) does not count: its fields are still checked.</summary>
        private static bool HiddenOnPage(FrameworkElement element, object root)
        {
            for (DependencyObject node = element; node != null && !ReferenceEquals(node, root); )
            {
                var parent = (node as FrameworkElement)?.Parent ?? (node is System.Windows.Media.Visual ? System.Windows.Media.VisualTreeHelper.GetParent(node) : null);
                if (node is ScrollViewer || ReferenceEquals(parent, root)) return false;
                if (node is UIElement ui && ui.Visibility != Visibility.Visible) return true;
                node = parent;
            }
            return false;
        }

        public static string FieldLabel(TextBox box)
        {
            string name = AutomationProperties.GetName(box);
            return string.IsNullOrWhiteSpace(name) ? box.Tag?.ToString() ?? "" : name;
        }

        public static bool IsDigits(string text) => !string.IsNullOrEmpty(text) && text.All(c => c >= '0' && c <= '9');

        /// <summary>A whole-number field takes a minus only when its setting declares a min below 0.</summary>
        public static bool AllowsNegative(FieldTag tag) => tag != null && tag.Min < 0 && tag.Min > double.MinValue;

        /// <summary>Digits, with a leading minus where the setting allows negatives.</summary>
        public static bool IsWhole(string text, bool negative) =>
            IsDigits(text) || (negative && text != null && text.Length > 1 && text[0] == '-' && IsDigits(text.Substring(1)));

        /// <summary>A number as the user typed it: a point or a comma as the decimal mark, but "1,000" (a comma before
        /// exactly three digits) or a text with both marks is ambiguous and not taken for 1.</summary>
        public static bool TryParseTyped(string text, out double value)
        {
            value = 0;
            string t = (text ?? "").Trim().TrimEnd('%').Trim();
            if (t.Contains(",") && t.Contains(".")) return false;
            int comma = t.LastIndexOf(',');
            if (comma > 0 && t.Length - comma - 1 == 3 && t.Substring(comma + 1).All(char.IsDigit))
            {
                string before = t.Substring(0, comma).TrimStart('-');
                if (before.Length > 0 && before.All(char.IsDigit) && before.TrimStart('0').Length > 0) return false;
            }
            return TryParseFinite(t, out value);
        }

        public static bool TryParseInvariantDouble(string text, out double value)
        {
            string normalized = (text ?? "").Trim().TrimEnd('%').Trim().Replace(",", ".");
            return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        public static bool TryParseFinite(string text, out double value) =>
            TryParseInvariantDouble(text, out value) && !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
