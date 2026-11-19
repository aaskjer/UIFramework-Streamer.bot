using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Newtonsoft.Json.Linq;

namespace UIFramework
{
    /// <summary>
    /// Makes the controls that hold settings. Each one reads its starting value from the loaded settings (or
    /// the default), carries a <see cref="FieldTag"/> for saving, and registers with the window's parts so
    /// previews and summaries can read it live. One instance per window.
    /// </summary>
    public sealed class Fields
    {
        private readonly WindowParts _parts;
        private readonly JObject _settings;
        private readonly JObject _defaults;
        private readonly Action<string> _openUrl;

        public Fields(WindowParts parts, JObject settings, JObject defaults, Action<string> openUrl)
        {
            _parts = parts;
            _settings = settings;
            _defaults = defaults;
            _openUrl = openUrl ?? (url => { });
        }

        // ---- Reading values ----------------------------------------------------------------------------

        public JToken Token(string key) => _settings[key] ?? _defaults[key];

        public bool Bool(string key) => Token(key)?.Type == JTokenType.Boolean && (bool)Token(key);

        public long Long(string key)
        {
            if (FieldBinding.TryLong(Token(key), out long exact)) return exact;
            if (!FieldBinding.TryNumber(Token(key), out double d)) return 0;
            if (d >= long.MaxValue) return long.MaxValue;
            if (d <= long.MinValue) return long.MinValue;
            return (long)Math.Round(d);
        }

        public int Int(string key)
        {
            long l = Long(key);
            return l > int.MaxValue ? int.MaxValue : l < int.MinValue ? int.MinValue : (int)l;
        }

        public double Double(string key) => FieldBinding.TryNumber(Token(key), out double d) ? d : 0;

        /// <summary>The value a number box starts from: the stored one, or the default when the file holds something
        /// that is no number ("", "abc", an array), so the page builds and the bad value is not saved back.</summary>
        private JToken NumberToken(string key)
        {
            var t = Token(key);
            if (t == null || t.Type == JTokenType.Null || FieldBinding.TryNumber(t, out _)) return t;
            return _defaults[key];
        }

        public string String(string key) => FieldBinding.Invariant(Token(key));

        // ---- Boxes ---------------------------------------------------------------------------------------

        public TextBox SettingBox(string label, string key, string tooltip, FieldKind kind = FieldKind.String)
        {
            var tag = new FieldTag(key, kind);
            var token = kind == FieldKind.Int || kind == FieldKind.Long || kind == FieldKind.Double ? NumberToken(key) : Token(key);
            var box = new TextBox { Text = FieldBinding.Display(tag, token), Tag = tag, ToolTip = tooltip };
            Ui.StyleInput(box);
            AutomationProperties.SetName(box, label);
            _parts.Boxes[key] = box;
            return box;
        }

        public TextBox IntBox(string label, string key, string tooltip, bool unlimitedAtZero = false)
        {
            var box = SettingBox(label, key, tooltip, FieldKind.Int);
            ((FieldTag)box.Tag).UnlimitedAtZero = unlimitedAtZero;
            box.Text = FieldBinding.Display((FieldTag)box.Tag, NumberToken(key));
            box.MaxLength = 11;
            WholeOnly(box);
            return box;
        }

        /// <summary>A whole-number field for the money settings. With unlimitedAtZero the ceiling settings show
        /// their Text.T("field.noLimit") value as 0, which is what gets typed back in to lift it.</summary>
        public TextBox LongBox(string label, string key, string tooltip, bool unlimitedAtZero = false)
        {
            var box = SettingBox(label, key, tooltip, FieldKind.Long);
            ((FieldTag)box.Tag).UnlimitedAtZero = unlimitedAtZero;
            box.Text = FieldBinding.Display((FieldTag)box.Tag, NumberToken(key));
            box.MaxLength = 20;
            WholeOnly(box);
            return box;
        }

        public TextBox DoubleBox(string label, string key, string tooltip)
        {
            var box = SettingBox(label, key, tooltip, FieldKind.Double);
            box.MaxLength = 12;
            Ui.DecimalsOnly(box);
            return box;
        }

        /// <summary>Digits only, and a leading minus once the setting allows negatives (its tag's Min, set after the
        /// box is built, is read when typing). A duration typed in minutes or hours (a Factor) takes a decimal mark.</summary>
        private static void WholeOnly(TextBox box)
        {
            bool Allowed(string text, int at)
            {
                var tag = FieldTag.Of(box.Tag);
                bool negative = FieldBinding.AllowsNegative(tag);
                bool decimals = tag != null && tag.Factor != 1.0;
                for (int k = 0; k < text.Length; k++)
                {
                    char c = text[k];
                    if (c >= '0' && c <= '9') continue;
                    if (c == '-' && negative && at + k == 0) continue;
                    if ((c == '.' || c == ',') && decimals) continue;
                    return false;
                }
                return true;
            }
            box.PreviewTextInput += (s, e) => e.Handled = !Allowed(e.Text, box.SelectionStart);
            box.PreviewKeyDown += (s, e) => { if (e.Key == Key.Space) e.Handled = true; };
            DataObject.AddPastingHandler(box, (s, e) =>
            {
                if (!e.DataObject.GetDataPresent(DataFormats.Text) || !Allowed(((string)e.DataObject.GetData(DataFormats.Text)).Trim(), box.SelectionStart))
                    e.CancelCommand();
            });
        }

        public FrameworkElement IntField(string label, string key, string tooltip, bool unlimitedAtZero = false) =>
            Ui.Labeled(label, IntBox(label, key, tooltip, unlimitedAtZero));

        /// <summary>A duration stored in whole seconds, typed and shown in its unit: "min" and "h" take minutes and
        /// hours (1.5 min is saved as 90), "s" or none seconds. The label carries the unit.</summary>
        public FrameworkElement DurationField(string label, string key, string tooltip, string unit, bool unlimitedAtZero = false)
        {
            var box = IntBox(label, key, tooltip, unlimitedAtZero);
            var tag = (FieldTag)box.Tag;
            tag.Factor = DurationFactor(unit);
            if (tag.Factor != 1.0)
            {
                box.MaxLength = 14;
                box.Text = FieldBinding.Display(tag, NumberToken(key));
            }
            return Ui.Labeled(label + " (" + (unit ?? "s") + ")", box);
        }

        /// <summary>Seconds per typed unit of a duration: 60 for "min", 3600 for "h", else 1.</summary>
        public static double DurationFactor(string unit)
        {
            switch ((unit ?? "").Trim().ToLowerInvariant())
            {
                case "min": case "mins": case "minute": case "minutes": return 60.0;
                case "h": case "hr": case "hrs": case "hour": case "hours": return 3600.0;
                default: return 1.0;
            }
        }

        public FrameworkElement LongField(string label, string key, string tooltip, bool unlimitedAtZero = false) =>
            Ui.Labeled(label, LongBox(label, key, tooltip, unlimitedAtZero));

        public FrameworkElement DoubleField(string label, string key, string tooltip) =>
            Ui.Labeled(label, DoubleBox(label, key, tooltip));

        /// <summary>A time of day, stored as "HH:mm" on the 24-hour clock. The box takes "9:5", "0905" or "9" (see
        /// <see cref="FieldBinding.TryTime"/>) and shows "09:05" once the cursor leaves it; text that is no time of day
        /// stays, for the check before saving to name, as in a number box. It may be empty only where the default is.
        /// A stored value the field cannot take ("abc", nothing where a time is due) shows the default, as a number
        /// box does, and is not saved back.</summary>
        public FrameworkElement TimeField(string label, string key, string tooltip)
        {
            var tag = new FieldTag(key, FieldKind.Time) { AllowEmpty = FieldBinding.Invariant(_defaults[key]).Trim().Length == 0 };
            var stored = Token(key);
            bool usable = !(stored is JContainer) && FieldBinding.IsValidTime(tag, FieldBinding.Invariant(stored));
            var box = new TextBox
            {
                Text = FieldBinding.Display(tag, usable ? stored : _defaults[key]),
                Tag = tag,
                MaxLength = 5,
                ToolTip = (string.IsNullOrEmpty(tooltip) ? "" : tooltip + "\n") + Text.T("field.time.tip")
            };
            Ui.StyleInput(box);
            AutomationProperties.SetName(box, label);
            _parts.Boxes[key] = box;
            // Digits and what may stand between hours and minutes; anything else is not typed and not pasted.
            bool Allowed(string text) => text.Length > 0 && text.All(c => (c >= '0' && c <= '9') || c == ':' || c == '.' || c == 'h' || c == 'H');
            box.PreviewTextInput += (s, e) => e.Handled = !Allowed(e.Text);
            box.PreviewKeyDown += (s, e) => { if (e.Key == Key.Space) e.Handled = true; };
            DataObject.AddPastingHandler(box, (s, e) =>
            {
                if (!e.DataObject.GetDataPresent(DataFormats.Text) || !Allowed(((string)e.DataObject.GetData(DataFormats.Text)).Trim()))
                    e.CancelCommand();
            });
            box.LostFocus += (s, e) =>
            {
                if (FieldBinding.TryTime(box.Text, out string time) && time != box.Text)
                    box.Text = time;
            };
            return Ui.Labeled(label, box);
        }

        /// <summary>A plain text setting, saved as typed: spaces at either end stay (see <see cref="FieldTag.KeepSpaces"/>).</summary>
        /// <param name="hint">Grey text inside the box while it is empty ("not set"); null for none.</param>
        public FrameworkElement TextField(string label, string key, string tooltip, string hint = null)
        {
            var box = SettingBox(label, key, tooltip);
            ((FieldTag)box.Tag).KeepSpaces = true;
            return Ui.Labeled(label, string.IsNullOrEmpty(hint) ? (FrameworkElement)box : Ui.WithHint(box, hint));
        }

        /// <summary>A text field shown as dots (a token, a client secret). The real value lives in the setting's box,
        /// which carries the tag, so saving, resetting, change tracking and <c>set</c> work on it unchanged; a
        /// PasswordBox in front of it mirrors it both ways, and the eye button swaps the two. Both sit in one cell, so
        /// the field is as wide revealed as it is masked.</summary>
        /// <param name="hint">Grey text inside the field while it is empty, masked or revealed; null for none.</param>
        public FrameworkElement MaskedField(string label, string key, string tooltip, string hint = null)
        {
            var box = SettingBox(label, key, tooltip);
            box.Visibility = Visibility.Collapsed;
            var masked = new PasswordBox { Password = box.Text, ToolTip = tooltip, PasswordChar = '•' };
            Ui.StyleInput(masked);
            AutomationProperties.SetName(masked, label);
            bool syncing = false;
            masked.PasswordChanged += (s, e) => { if (syncing) return; syncing = true; box.Text = masked.Password; syncing = false; };
            box.TextChanged += (s, e) => { if (syncing) return; syncing = true; masked.Password = box.Text; syncing = false; };
            var eye = Ui.IconButton(Icons.Eye, Icons.Font, Text.T("field.masked.show"));
            eye.Margin = new Thickness(6, 0, 0, 0);
            eye.Click += (s, e) =>
            {
                bool reveal = box.Visibility != Visibility.Visible;
                box.Visibility = reveal ? Visibility.Visible : Visibility.Collapsed;
                masked.Visibility = reveal ? Visibility.Collapsed : Visibility.Visible;
                eye.ToolTip = Text.T(reveal ? "field.masked.hide" : "field.masked.show");
                if (reveal) box.Focus(); else masked.Focus();
            };
            // One cell for the two: a DockPanel gave the revealed box only the width of its text, since it fills with
            // its last child alone. The hint lies over both and shows through neither once there is text.
            var both = Ui.WithHint(box, hint);
            both.Children.Insert(1, masked);
            var dock = new DockPanel();
            DockPanel.SetDock(eye, Dock.Right);
            dock.Children.Add(eye);
            dock.Children.Add(both);
            return Ui.Labeled(label, dock);
        }

        /// <summary>A setting stored as a fraction but typed as a percentage. The factor converts one into the
        /// other on save (0.01 for a fraction, 1 for a value already in percent), and the field clamps itself as
        /// soon as it loses focus so a typo cannot reach the saved file.</summary>
        public FrameworkElement PercentField(string label, string key, double factor, string tooltip, double min = 0, double max = 100)
        {
            var tag = new FieldTag(key, FieldKind.Double) { Factor = factor, Min = min, Max = max };
            var box = new TextBox
            {
                Text = FieldBinding.Display(tag, NumberToken(key)),
                Tag = tag,
                MaxLength = 6,
                ToolTip = (tooltip ?? "") + Text.T("field.percentRange", min.ToString("0.##", CultureInfo.InvariantCulture), max.ToString("0.##", CultureInfo.InvariantCulture)).TrimStart(string.IsNullOrEmpty(tooltip) ? new[] { '\n' } : new char[0])
            };
            Ui.StyleInput(box);
            AutomationProperties.SetName(box, label);
            _parts.Boxes[key] = box;
            Ui.DecimalsOnly(box);
            box.LostFocus += (s, e) =>
            {
                // Only a number outside the range is rewritten, to its limit with every decimal it has; one within it
                // stays as typed, and text that is no number stays for the save to name.
                if (!FieldBinding.TryParseTyped(box.Text, out double pct)) return;
                double clamped = Math.Max(min, Math.Min(max, pct));
                if (clamped != pct)
                    box.Text = clamped.ToString("0.##########", CultureInfo.InvariantCulture);
            };
            return Ui.Labeled(label + " (%)", box);
        }

        /// <summary>A multi-line template box, as the Messages page uses. Whitespace is kept as typed.</summary>
        public TextBox TemplateBox(string label, string key)
        {
            var tag = new FieldTag(key, FieldKind.Template);
            var box = new TextBox
            {
                Text = String(key),
                Tag = tag,
                Height = 76,
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(4)
            };
            Ui.StyleColors(box);
            AutomationProperties.SetName(box, label);
            _parts.Boxes[key] = box;
            return box;
        }

        // ---- Combos --------------------------------------------------------------------------------------

        public ComboBox NewCombo(string key, FieldKind kind, string tooltip)
        {
            var combo = new ComboBox
            {
                Tag = new FieldTag(key, kind),
                Height = 28,
                MaxDropDownHeight = 360,
                ToolTip = tooltip,
                VerticalContentAlignment = VerticalAlignment.Center,
                Template = Ui.ComboTemplate(),
                ItemContainerStyle = Ui.ComboItemStyle(),
                Foreground = new SolidColorBrush(Theme.InputFg)
            };
            return Ui.WheelToPage(combo);
        }

        public ComboBox StringCombo(string label, string key, (string Value, string Label)[] options, string tooltip)
        {
            var combo = NewCombo(key, FieldKind.String, tooltip);
            string current = String(key);
            foreach (var option in options)
                combo.Items.Add(new ComboBoxItem { Content = option.Label, Tag = option.Value });
            combo.SelectedItem = combo.Items.OfType<ComboBoxItem>()
                .FirstOrDefault(i => string.Equals(i.Tag as string, current ?? "", StringComparison.OrdinalIgnoreCase))
                ?? Kept(combo, current, current)
                ?? combo.Items.OfType<ComboBoxItem>().FirstOrDefault();
            AutomationProperties.SetName(combo, label);
            _parts.Combos[key] = combo;
            return combo;
        }

        /// <summary>A stored value the options do not offer (an older version's, one typed into the file) is kept as an
        /// extra entry and selected, so a save that does not touch the dropdown writes it back unchanged.</summary>
        private static ComboBoxItem Kept(ComboBox combo, object value, string shown)
        {
            if (value == null || (value is string s && s.Length == 0)) return null;
            var item = new ComboBoxItem { Content = shown, Tag = value };
            combo.Items.Add(item);
            return item;
        }

        public FrameworkElement StringComboField(string label, string key, (string Value, string Label)[] options, string tooltip) =>
            Ui.Labeled(label, StringCombo(label, key, options, tooltip));

        /// <summary>A dropdown whose options come from the project (<c>optionsFrom</c>) and change under it. It offers
        /// the options last handed to <paramref name="offer"/>; the <paramref name="declared"/> ones until then, and
        /// again when it is handed none. The stored value and the setting's default are entries of their own where no
        /// option has them (an empty one as a blank entry at the top), so a save that does not touch the dropdown
        /// writes back what the file holds, and a reset finds the default. What is selected stays selected when the
        /// new entries have it, and the field does not count as changed by that; a pick they no longer have goes back
        /// to the stored value. The dropdown never picks an option by itself.</summary>
        public FrameworkElement OfferedComboField(string label, string key, IList<(string Value, string Label)> declared, string tooltip,
            out Action<IList<(string Value, string Label)>> offer)
        {
            var combo = NewCombo(key, FieldKind.String, tooltip);
            ((FieldTag)combo.Tag).OptionsAsked = true;
            // The label each value was last offered with: an entry the options no longer have keeps reading as it did.
            var labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

            void Offer(IList<(string Value, string Label)> options)
            {
                var entries = new List<(string Value, string Label)>();
                bool Has(string value) => entries.Any(e => Same(e.Value, value));
                foreach (var option in options != null && options.Count > 0 ? options : declared ?? new List<(string Value, string Label)>())
                {
                    string value = option.Value ?? "";
                    if (Has(value)) continue;
                    labels[value] = string.IsNullOrEmpty(option.Label) ? value : option.Label;
                    entries.Add((value, labels[value]));
                }
                void Keep(string value)
                {
                    if (Has(value)) return;
                    if (value.Length == 0) entries.Insert(0, ("", ""));
                    else entries.Add((value, labels.TryGetValue(value, out string known) ? known : value));
                }
                // The file's value as it stands now: a save since the page was built has put the window's into it.
                string stored = String(key);
                Keep(FieldBinding.Invariant(_defaults[key]));
                Keep(stored);

                var selected = combo.SelectedItem as ComboBoxItem;
                string held = selected?.Tag as string;
                bool stays = held != null && Has(held);
                var fresh = new List<ComboBoxItem>();
                ComboBoxItem chosen = null;
                foreach (var entry in entries)
                {
                    // The selected entry stays the object it is, with the text the file has it in: nothing is selected
                    // anew, so nothing that follows the field hears of a change that is none.
                    bool same = stays && Same(entry.Value, held);
                    var item = same ? selected : new ComboBoxItem { Tag = entry.Value };
                    item.Content = entry.Label;
                    if (entry.Label.Length == 0) item.MinHeight = 24;
                    if (same || (!stays && chosen == null && Same(entry.Value, stored))) chosen = item;
                    fresh.Add(item);
                }

                if (stays)
                {
                    for (int i = combo.Items.Count - 1; i >= 0; i--)
                        if (!ReferenceEquals(combo.Items[i], selected))
                            combo.Items.RemoveAt(i);
                    int at = fresh.IndexOf(selected);
                    for (int i = 0; i < at; i++) combo.Items.Insert(i, fresh[i]);
                    for (int i = at + 1; i < fresh.Count; i++) combo.Items.Add(fresh[i]);
                }
                else
                {
                    // The new entries first, then the selection, then the old ones go: selected once, not emptied in between.
                    var old = combo.Items.Cast<object>().ToList();
                    foreach (var item in fresh) combo.Items.Add(item);
                    combo.SelectedItem = chosen;
                    foreach (var item in old) combo.Items.Remove(item);
                }
            }

            Offer(null);
            AutomationProperties.SetName(combo, label);
            _parts.Combos[key] = combo;
            offer = Offer;
            return Ui.Labeled(label, combo);
        }

        public FrameworkElement IntCombo(string label, string key, (int Value, string Label)[] options, string tooltip)
        {
            var combo = NewCombo(key, FieldKind.Int, tooltip);
            int value = Int(key);
            foreach (var option in options)
                combo.Items.Add(new ComboBoxItem { Content = option.Label, Tag = option.Value });
            combo.SelectedItem = combo.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (int)i.Tag == value)
                ?? (FieldBinding.TryNumber(Token(key), out _) ? Kept(combo, value, value.ToString(CultureInfo.InvariantCulture)) : null)
                ?? combo.Items.OfType<ComboBoxItem>().FirstOrDefault();
            AutomationProperties.SetName(combo, label);
            _parts.Combos[key] = combo;
            return Ui.Labeled(label, combo);
        }

        public FrameworkElement DoubleCombo(string label, string key, (double Value, string Label)[] options, string tooltip)
        {
            var combo = NewCombo(key, FieldKind.Double, tooltip);
            double value = Double(key);
            foreach (var option in options)
                combo.Items.Add(new ComboBoxItem { Content = option.Label, Tag = option.Value });
            combo.SelectedItem = combo.Items.OfType<ComboBoxItem>().FirstOrDefault(i => Math.Abs((double)i.Tag - value) < 0.001)
                ?? (FieldBinding.TryNumber(Token(key), out _) ? Kept(combo, value, value.ToString("0.##########", CultureInfo.InvariantCulture)) : null)
                ?? combo.Items.OfType<ComboBoxItem>().FirstOrDefault();
            AutomationProperties.SetName(combo, label);
            _parts.Combos[key] = combo;
            return Ui.Labeled(label, combo);
        }

        // ---- Switches ------------------------------------------------------------------------------------

        /// <summary>A slider over stops (<paramref name="options"/>: the stored value is the stop's value, the stops
        /// are captioned under the track) or over a number range, with the number beside the label.</summary>
        public FrameworkElement SliderField(string label, string key, string tooltip, List<SettingOption> options, double min, double max, double step, bool whole, string unit)
        {
            bool stops = options != null && options.Count > 0;
            var kind = stops
                ? (options.All(o => long.TryParse(o.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)) ? FieldKind.Int : FieldKind.String)
                : whole ? FieldKind.Int : FieldKind.Double;
            var slider = new OptionSlider
            {
                Options = stops ? options : null,
                Whole = whole,
                Minimum = stops ? 0 : min,
                Maximum = stops ? options.Count - 1 : Math.Max(min, max),
                TickFrequency = stops ? 1 : Math.Max(step, 1e-9),
                Tag = new FieldTag(key, kind),
                ToolTip = tooltip,
                Margin = new Thickness(0, 4, 0, 0)
            };
            // A range starts on its default when the file holds something that is no number ("", "abc"), as a number box
            // does; it started on its minimum, which the next save then wrote.
            if (!slider.Put(stops ? Token(key) : NumberToken(key)))
            {
                slider.Value = slider.Minimum;
                // A stored value the stops do not offer stays what is saved until the slider is moved.
                var stored = Token(key);
                if (stops && stored != null && stored.Type != JTokenType.Null && FieldBinding.Invariant(stored).Length > 0)
                    slider.Keep(stored);
            }
            AutomationProperties.SetName(slider, label);
            _parts.Sliders[key] = slider;

            var panel = new StackPanel();
            var head = Ui.SmallLabel(label, 0);
            panel.Children.Add(head);
            panel.Children.Add(slider);
            if (stops)
            {
                // Each caption is centred under its stop: the thumb's centre runs from half the thumb's width to the
                // slider's width less that, and the outermost captions stay inside the edges.
                var captions = new Canvas { Height = 16, Margin = new Thickness(0, 2, 0, 0) };
                var labels = new List<TextBlock>();
                for (int i = 0; i < options.Count; i++)
                {
                    var caption = new TextBlock { Text = options[i].Label, FontSize = 11, Foreground = Theme.SubFg, Cursor = Cursors.Hand };
                    int stop = i;
                    // The stop the slider sits on lets go of a kept value; another one is a move, which lets go of it too.
                    caption.MouseLeftButtonDown += (sender, e) => { if (slider.Index == stop) slider.LetGo(); else slider.Value = stop; Paint(); };
                    captions.Children.Add(caption);
                    labels.Add(caption);
                }
                void Place()
                {
                    double width = captions.ActualWidth;
                    if (width <= 0) return;
                    double half = Ui.SliderThumb / 2.0, inner = width - Ui.SliderThumb;
                    for (int i = 0; i < labels.Count; i++)
                    {
                        labels[i].Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                        double w = labels[i].DesiredSize.Width;
                        double centre = labels.Count == 1 ? half : half + i * inner / (labels.Count - 1);
                        Canvas.SetLeft(labels[i], Math.Max(0, Math.Min(width - w, centre - w / 2)));
                    }
                }
                void Paint()
                {
                    // A kept value the stops do not offer: named beside the label, no stop lit until one is chosen.
                    head.Text = slider.IsKept ? label + "  ·  " + slider.StoredText : label;
                    int at = slider.IsKept ? -1 : slider.Index;
                    for (int i = 0; i < labels.Count; i++)
                    {
                        labels[i].Foreground = i == at ? new SolidColorBrush(Theme.AccentText) : Theme.SubFg;
                        labels[i].FontWeight = i == at ? FontWeights.SemiBold : FontWeights.Normal;
                    }
                    Place();
                }
                captions.SizeChanged += (sender, e) => Place();
                slider.ValueChanged += (sender, e) => Paint();
                // A click on the stop the slider already sits on chooses it too.
                slider.PreviewMouseLeftButtonUp += (sender, e) => { if (slider.IsKept) { slider.LetGo(); Paint(); } };
                Paint();
                panel.Children.Add(captions);
            }
            else
            {
                void Paint() => head.Text = label + "  ·  " + slider.StoredText + (string.IsNullOrEmpty(unit) ? "" : " " + unit);
                slider.ValueChanged += (sender, e) => Paint();
                Paint();
            }
            return panel;
        }

        public CheckBox Toggle(string label, string key, string tooltip)
        {
            var toggle = new CheckBox
            {
                // A text block of its own, so a label longer than the window is wide wraps instead of being cut.
                Content = new TextBlock { Text = label, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center },
                IsChecked = Bool(key),
                Tag = new FieldTag(key, FieldKind.Bool),
                ToolTip = tooltip,
                Margin = new Thickness(0, 4, 0, 4),
                Template = Ui.SwitchTemplate(),
                Foreground = Theme.LabelFg,
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center
            };
            AutomationProperties.SetName(toggle, label);
            _parts.Switches[key] = toggle;
            return toggle;
        }

        public static void TwitchOnly(CheckBox toggle)
        {
            var label = new StackPanel { Orientation = Orientation.Horizontal };
            label.Children.Add(new TextBlock { Text = (toggle.Content as TextBlock)?.Text ?? toggle.Content as string, VerticalAlignment = VerticalAlignment.Center });
            label.Children.Add(Ui.Pill(Text.T("field.twitchOnly"), Theme.AccentSoft, Theme.AccentText));
            toggle.Content = label;
            AutomationProperties.SetName(toggle, AutomationProperties.GetName(toggle) + " (" + Text.T("field.twitchOnly") + ")");
        }

        // ---- Name lists ----------------------------------------------------------------------------------

        public static List<string> SplitNames(string list) =>
            (list ?? "").Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToList();

        public static bool ContainsName(List<string> names, string name) =>
            names.Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));

        /// <summary>A comma-separated setting shown as removable chips. The real value lives in a hidden box
        /// carrying the tag, so saving, resetting and change tracking all work on it unchanged.</summary>
        /// <param name="suggest">Names to offer under the box (a grouplist's Streamer.bot groups): called once, when the
        /// box first gets the cursor, with where the names go, at once or when they are there. Null: none, and the
        /// field is a plain name list.</param>
        public FrameworkElement NameList(string label, string key, string hint, string tooltip, Action<Action<List<string>>> suggest = null)
        {
            var store = new TextBox { Text = String(key), Tag = new FieldTag(key, FieldKind.String), Visibility = Visibility.Collapsed };
            _parts.Boxes[key] = store;

            var input = new TextBox { MinWidth = 180, ToolTip = tooltip };
            Ui.StyleInput(input);
            input.Height = 26;
            AutomationProperties.SetName(input, label);
            var inputHost = Ui.WithHint(input, hint);
            inputHost.Margin = new Thickness(0, 0, 0, 6);

            var wrap = new WrapPanel();
            wrap.Children.Add(inputHost);

            void Draw()
            {
                for (int i = wrap.Children.Count - 1; i >= 0; i--)
                    if (wrap.Children[i] != inputHost)
                        wrap.Children.RemoveAt(i);
                int at = 0;
                foreach (var name in SplitNames(store.Text))
                    wrap.Children.Insert(at++, NameChip(name, () =>
                    {
                        store.Text = string.Join(",", SplitNames(store.Text).Where(n => !string.Equals(n, name, StringComparison.OrdinalIgnoreCase)));
                        input.Focus();
                    }));
            }

            void Commit()
            {
                var added = SplitNames(input.Text.Replace(';', ',')).Select(n => n.TrimStart('@').Trim()).Where(n => n.Length > 0).ToList();
                if (input.Text.Length > 0)
                    input.Text = "";
                if (added.Count == 0)
                    return;
                var names = SplitNames(store.Text);
                foreach (var n in added)
                    if (!ContainsName(names, n))
                        names.Add(n);
                store.Text = string.Join(",", names);
            }

            var offered = suggest == null ? null : new NameSuggestions(input, inputHost, store, name => { input.Text = name; Commit(); }, suggest);
            // The list takes the keys it uses while it is open; Enter without a name picked in it adds what is typed.
            input.PreviewKeyDown += (sender, e) =>
            {
                if (offered != null && offered.Press(e.Key)) e.Handled = true;
                else if (e.Key == Key.Enter) { Commit(); e.Handled = true; }
            };
            input.PreviewTextInput += (sender, e) => { if (e.Text == ",") { Commit(); e.Handled = true; } };
            input.LostKeyboardFocus += (sender, e) => Commit();
            store.TextChanged += (sender, e) => Draw();
            _parts.AddNameCommit(Commit);
            Draw();

            var panel = new StackPanel();
            panel.Children.Add(wrap);
            panel.Children.Add(store);
            if (offered != null)
                panel.Children.Add(offered.Popup);
            return panel;
        }

        /// <summary>What a name list offers under its box: the names of <paramref name="known"/> not added yet (those in
        /// <paramref name="chosen"/>, comma-separated, case ignored) that hold what is typed, whatever its case and
        /// accents; those starting with it first, the rest in the order given.</summary>
        public static List<string> Suggested(IEnumerable<string> known, string chosen, string typed)
        {
            var have = SplitNames(chosen);
            string word = SearchIndex.Fold((typed ?? "").Trim().TrimStart('@').Trim());
            var found = new List<(string Name, bool Starts, int Order)>();
            int order = 0;
            foreach (string name in (known ?? Enumerable.Empty<string>()).Select(n => (n ?? "").Trim()))
            {
                order++;
                if (name.Length == 0 || ContainsName(have, name) || found.Any(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase)))
                    continue;
                int at = SearchIndex.Fold(name).IndexOf(word, StringComparison.Ordinal);
                if (at >= 0)
                    found.Add((name, at == 0, order));
            }
            return found.OrderBy(f => f.Starts ? 0 : 1).ThenBy(f => f.Order).Select(f => f.Name).ToList();
        }

        /// <summary>The Tag of a name the list under a name list's box offers, for tests.</summary>
        public const string SuggestionTag = "uiframework.namelist.suggestion";

        /// <summary>The list under a name list's box, in the look of a dropdown: the names not added yet, narrowed by
        /// what is typed. Up and Down pick one, Enter or a click adds it, Esc closes the list; what is typed is added as
        /// before. It shows while the box has the cursor and closes when the window moves or loses the keyboard.</summary>
        private sealed class NameSuggestions
        {
            private readonly TextBox _input, _store;
            private readonly Action<string> _add;
            private readonly StackPanel _rows = new StackPanel();
            private readonly EventHandler _hide;
            private List<string> _known, _shown = new List<string>();
            private int _selected = -1;
            private bool _asked, _active;
            private Window _window;
            // Where the pointer was on the screen when it last counted: a row drawn or moved under a pointer at rest (the
            // list redrawn, the box moved on by a new chip) gets a mouse move too, and must not take the pick, or Enter
            // adds that name instead of what is typed.
            private Point _pointerAt;

            public readonly Popup Popup;

            public NameSuggestions(TextBox input, FrameworkElement anchor, TextBox store, Action<string> add, Action<Action<List<string>>> ask)
            {
                _input = input;
                _store = store;
                _add = add;
                _hide = (sender, e) => Close();
                AutomationProperties.SetName(_rows, AutomationProperties.GetName(input));
                Popup = new Popup
                {
                    PlacementTarget = anchor,
                    Placement = PlacementMode.Bottom,
                    VerticalOffset = 2,
                    AllowsTransparency = true,
                    Focusable = false,
                    Child = new Border
                    {
                        Background = new SolidColorBrush(Theme.PopupBg),
                        BorderBrush = new SolidColorBrush(Theme.LineStrong),
                        BorderThickness = new Thickness(1),
                        Padding = new Thickness(0, 3, 0, 3),
                        MaxHeight = 220,
                        MaxWidth = 360,
                        Child = new ScrollViewer { Content = _rows, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Focusable = false }
                    }
                };
                ((Border)Popup.Child).SetBinding(FrameworkElement.MinWidthProperty, new System.Windows.Data.Binding("ActualWidth") { Source = anchor });
                Popup.Closed += (sender, e) => Unwatch();
                input.GotKeyboardFocus += (sender, e) =>
                {
                    _active = true;
                    if (_asked) { Refresh(); return; }
                    _asked = true;
                    ask(names => { _known = names; Refresh(); });
                };
                input.LostKeyboardFocus += (sender, e) => { _active = false; Close(); };
                input.TextChanged += (sender, e) => Refresh();
                store.TextChanged += (sender, e) => Refresh();
            }

            /// <summary>Lists what fits what is typed, or closes the list when nothing does.</summary>
            public void Refresh()
            {
                _shown = _active && _known != null ? Suggested(_known, _store.Text, _input.Text) : new List<string>();
                _rows.Children.Clear();
                _selected = -1;
                _pointerAt = OnScreen(Mouse.GetPosition(_input));
                for (int i = 0; i < _shown.Count; i++)
                    _rows.Children.Add(Row(_shown[i], i));
                if (_shown.Count == 0) Close();
                else if (!Popup.IsOpen) Open();
            }

            /// <summary>A key pressed in the box: true when the list took it.</summary>
            public bool Press(Key key)
            {
                if (!Popup.IsOpen)
                {
                    // Down brings back a list Esc closed.
                    if (key != Key.Down || !_active) return false;
                    Refresh();
                    return Popup.IsOpen;
                }
                switch (key)
                {
                    case Key.Down: Select(Math.Min(_selected + 1, _shown.Count - 1)); return true;
                    // Up from the first name picks none again: Enter then adds what is typed.
                    case Key.Up: Select(Math.Max(_selected - 1, -1)); return true;
                    case Key.Escape: Close(); return true;
                    case Key.Enter:
                        if (_selected < 0) return false;
                        _add(_shown[_selected]);
                        return true;
                    default: return false;
                }
            }

            private Border Row(string name, int index)
            {
                var row = new Border
                {
                    Tag = SuggestionTag,
                    Padding = new Thickness(8, 4, 8, 4),
                    Background = Brushes.Transparent,
                    Cursor = Cursors.Hand,
                    Child = new TextBlock { Text = name, FontSize = 12, Foreground = Theme.LabelFg, TextTrimming = TextTrimming.CharacterEllipsis }
                };
                AutomationProperties.SetName(row, name);
                row.MouseMove += (sender, e) =>
                {
                    var at = OnScreen(e.GetPosition(_input));
                    if (at == _pointerAt) return;
                    _pointerAt = at;
                    Select(index);
                };
                // Before the box could lose the keyboard to the click: the name goes in, the cursor stays in the box.
                row.PreviewMouseLeftButtonDown += (sender, e) =>
                {
                    e.Handled = true;
                    _add(name);
                };
                return row;
            }

            private Point OnScreen(Point inBox)
            {
                try { return _input.PointToScreen(inBox); }
                catch (InvalidOperationException) { return inBox; }   // not on a window (yet)
            }

            private void Select(int index)
            {
                if (_selected >= 0 && _selected < _rows.Children.Count)
                    ((Border)_rows.Children[_selected]).Background = Brushes.Transparent;
                _selected = index;
                if (index < 0 || index >= _rows.Children.Count) return;
                var row = (Border)_rows.Children[index];
                row.Background = new SolidColorBrush(Theme.Hover);
                row.BringIntoView();
            }

            private void Open()
            {
                // A popup stays where it opened when the window moves under it: the list closes instead.
                if (_window == null && (_window = Window.GetWindow(_input)) != null)
                {
                    _window.LocationChanged += _hide;
                    _window.Deactivated += _hide;
                }
                Popup.IsOpen = true;
            }

            private void Close()
            {
                Popup.IsOpen = false;
                Unwatch();
            }

            private void Unwatch()
            {
                if (_window == null) return;
                _window.LocationChanged -= _hide;
                _window.Deactivated -= _hide;
                _window = null;
            }
        }

        public static Border NameChip(string name, Action remove)
        {
            var x = new Button
            {
                Content = Icons.Remove,
                FontFamily = Icons.Font,
                FontSize = 8,
                Width = 18,
                Height = 18,
                Margin = new Thickness(4, 0, 0, 0),
                Foreground = Theme.SubFg,
                Cursor = Cursors.Hand,
                ToolTip = Text.T("field.remove", name),
                Template = Ui.ButtonTemplate(Colors.Transparent, Theme.Hover, Colors.Transparent, 9, new Thickness(0))
            };
            AutomationProperties.SetName(x, Text.T("field.remove", name));
            x.PreviewMouseLeftButtonDown += (sender, e) =>
            {
                e.Handled = true;
                remove();
            };
            x.Click += (sender, e) => remove();

            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(new TextBlock { Text = name, FontSize = 12, Foreground = Theme.LabelFg, VerticalAlignment = VerticalAlignment.Center });
            row.Children.Add(x);
            return new Border
            {
                Background = new SolidColorBrush(Theme.Surface),
                BorderBrush = new SolidColorBrush(Theme.Line),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(10, 2, 3, 2),
                Margin = new Thickness(0, 0, 6, 6),
                VerticalAlignment = VerticalAlignment.Center,
                Child = row
            };
        }

        // ---- Several of a set ---------------------------------------------------------------------------

        /// <summary>The Tag of the chip a multi field shows for a stored value none of its choices has, for tests.</summary>
        public const string MultiKeptTag = "uiframework.multi.kept";

        /// <summary>The options a multi field can offer. Its values are stored between commas, so an option whose value
        /// is empty or holds a comma is left out (the check names it), and so is one that repeats another's value.</summary>
        public static List<SettingOption> MultiChoices(List<SettingOption> options)
        {
            var offered = new List<SettingOption>();
            foreach (var option in options ?? new List<SettingOption>())
            {
                string value = (option.Value ?? "").Trim();
                if (value.Length == 0 || value.Contains(",") || offered.Any(o => string.Equals(o.Value, value, StringComparison.OrdinalIgnoreCase)))
                    continue;
                offered.Add(new SettingOption { Value = value, Label = string.IsNullOrWhiteSpace(option.Label) ? value : option.Label });
            }
            return offered;
        }

        /// <summary>What a multi field that is only shown says: the labels of the ticked choices, then any stored value
        /// that is none of them, as it is stored.</summary>
        public static string MultiLabels(List<SettingOption> options, JToken value)
        {
            var offered = MultiChoices(options);
            return string.Join(", ", FieldBinding.MultiValues(offered.Select(o => o.Value).ToArray(), FieldBinding.MultiText(null, value))
                .Select(v => offered.FirstOrDefault(o => string.Equals(o.Value, v, StringComparison.OrdinalIgnoreCase))?.Label ?? v));
        }

        /// <summary>Several of a fixed set of choices, as check boxes that wrap to several per line, each as wide as the
        /// widest, so they stand in columns. The ticked values
        /// live as comma-separated text, in the order of the choices and in their spelling, in a hidden box carrying the
        /// tag, as a name list's do: saving, resetting, change tracking and conditions work on it unchanged. A stored
        /// value that is none of the choices (an older list, a typo in the file) stays stored and shows as a chip behind
        /// the boxes, where it can be taken out.</summary>
        public FrameworkElement MultiField(string label, string key, List<SettingOption> options, string tooltip)
        {
            var offered = MultiChoices(options);
            var tag = new FieldTag(key, FieldKind.String) { Choices = offered.Select(o => o.Value).ToArray() };
            var store = new TextBox { Text = FieldBinding.Display(tag, Token(key)), Tag = tag, Visibility = Visibility.Collapsed };
            _parts.Boxes[key] = store;
            _parts.Choices[key] = tag.Choices;

            bool IsChoice(string value) => tag.Choices.Contains(value, StringComparer.OrdinalIgnoreCase);
            var wrap = new WrapPanel();
            AutomationProperties.SetName(wrap, label);
            var boxes = new List<CheckBox>();
            // While the boxes are set from the text, they do not write it back.
            bool drawing = false;
            void Write()
            {
                if (drawing) return;
                var values = boxes.Where(b => b.IsChecked == true).Select(b => (string)b.Tag)
                    .Concat(FieldBinding.MultiValues(tag.Choices, store.Text).Where(v => !IsChoice(v)));
                store.Text = string.Join(",", values);
            }
            foreach (var option in offered)
            {
                var box = new CheckBox
                {
                    // A text block of its own, so a long choice wraps instead of being cut. The Tag is the value it
                    // stores, not a FieldTag: the boxes are the field's furniture, the hidden box is the setting.
                    Content = new TextBlock { Text = option.Label, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center },
                    Tag = option.Value,
                    ToolTip = string.IsNullOrEmpty(tooltip) ? null : tooltip,
                    Margin = new Thickness(0, 2, 18, 6),
                    Template = Ui.CheckTemplate(),
                    Foreground = Theme.LabelFg,
                    Cursor = Cursors.Hand,
                    VerticalAlignment = VerticalAlignment.Center
                };
                AutomationProperties.SetName(box, option.Label);
                box.Checked += (sender, e) => Write();
                box.Unchecked += (sender, e) => Write();
                boxes.Add(box);
                wrap.Children.Add(box);
            }

            // Every box as wide as the widest (a chip for a kept value too), so they line up under each other however
            // many fit a line; never wider than the field, so a long choice still wraps.
            void Align()
            {
                if (!wrap.IsLoaded) return;
                double widest = 0;
                foreach (FrameworkElement child in wrap.Children)
                {
                    child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    widest = Math.Max(widest, child.DesiredSize.Width);
                }
                if (widest <= 0) return;
                double room = wrap.ActualWidth;
                double width = Math.Ceiling(room > 0 ? Math.Min(widest, room) : widest);
                if (double.IsNaN(wrap.ItemWidth) || Math.Abs(wrap.ItemWidth - width) > 0.5)
                    wrap.ItemWidth = width;
            }
            wrap.Loaded += (sender, e) => Align();
            wrap.SizeChanged += (sender, e) => { if (e.WidthChanged) Align(); };

            void Draw()
            {
                var values = FieldBinding.MultiValues(tag.Choices, store.Text);
                drawing = true;
                try
                {
                    foreach (var box in boxes)
                        box.IsChecked = values.Contains((string)box.Tag, StringComparer.OrdinalIgnoreCase);
                }
                finally { drawing = false; }
                for (int i = wrap.Children.Count - 1; i >= 0; i--)
                    if (!(wrap.Children[i] is CheckBox))
                        wrap.Children.RemoveAt(i);
                foreach (var kept in values.Where(v => !IsChoice(v)))
                {
                    string value = kept;
                    var chip = NameChip(value, () => store.Text = string.Join(",",
                        FieldBinding.MultiValues(tag.Choices, store.Text).Where(v => !string.Equals(v, value, StringComparison.OrdinalIgnoreCase))));
                    chip.Tag = MultiKeptTag;
                    chip.ToolTip = Text.T("field.multi.unknown");
                    chip.HorizontalAlignment = HorizontalAlignment.Left;
                    wrap.Children.Add(chip);
                }
                Align();
            }
            store.TextChanged += (sender, e) => Draw();
            Draw();

            var panel = new StackPanel();
            panel.Children.Add(wrap);
            panel.Children.Add(store);
            return panel;
        }

        // ---- Colours -------------------------------------------------------------------------------------

        private readonly ColourPicker _picker = new ColourPicker();

        /// <summary>A colour picker of these fields is open and still writing into its box.</summary>
        public bool PickerOpen => _picker.IsOpen;

        /// <param name="labelled">false: the row alone (box, swatch, picker), for a caller that labels it inline.</param>
        /// <param name="swatchOf">What the swatch shows for the box's text, for a field that reads its colour another
        /// way than <see cref="Colours.ParseHexBrush"/> (the accent takes solid colours only).</param>
        public FrameworkElement ColourField(string label, string key, string tooltip, bool labelled = true, Func<string, SolidColorBrush> swatchOf = null)
        {
            // A box with a swatch of its own (the accent) starts its picker on what that swatch shows when the box
            // holds no colour: an empty accent box opened the picker on yellow, not on the accent in use.
            bool ownSwatch = swatchOf != null;
            swatchOf = swatchOf ?? Colours.ParseHexBrush;
            var box = SettingBox(label, key, (string.IsNullOrEmpty(tooltip) ? "" : tooltip + "\n") + Text.T("field.colour.tip"));
            box.Width = 92;
            box.HorizontalAlignment = HorizontalAlignment.Left;

            var swatch = new Border
            {
                Width = 28,
                Height = 28,
                Margin = new Thickness(6, 0, 0, 0),
                CornerRadius = new CornerRadius(5),
                BorderBrush = new SolidColorBrush(Theme.InputBorder),
                BorderThickness = new Thickness(1),
                Background = swatchOf(box.Text),
                Cursor = Cursors.Hand,
                ToolTip = Text.T("colour.pick.tip")
            };
            AutomationProperties.SetName(swatch, Text.T("colour.pickFor", label));
            var pick = Ui.IconButton("🎨", null, Text.T("colour.pick", label.ToLowerInvariant()));
            pick.Margin = new Thickness(6, 0, 0, 0);

            box.TextChanged += (sender, e) => swatch.Background = swatchOf(box.Text);
            Color? Shown() => ownSwatch ? swatchOf(box.Text)?.Color : (Color?)null;
            swatch.MouseLeftButtonUp += (sender, e) => _picker.Open(box, swatch, Shown());
            pick.Click += (sender, e) => _picker.Open(box, swatch, Shown());

            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(box);
            row.Children.Add(swatch);
            row.Children.Add(pick);
            if (!labelled) { AutomationProperties.SetName(box, label); return row; }
            return Ui.Labeled(label, row);
        }

        // ---- Links and live reads ----------------------------------------------------------------------

        public TextBlock LinkLine(string text, string url) => Ui.LinkLine(text, url, _openUrl);

        /// <summary>Reads a number straight off the live control, so a preview or a summary shows what the
        /// window currently says rather than what was last saved.</summary>
        public double LiveNumber(string key, double fallback)
        {
            if (_parts.Boxes.TryGetValue(key, out var box) && FieldBinding.TryParseInvariantDouble(box.Text, out double typed))
                return typed;
            if (_parts.Combos.TryGetValue(key, out var combo) && combo.SelectedItem is ComboBoxItem item)
            {
                if (item.Tag is int whole) return whole;
                if (item.Tag is double exact) return exact;
                if (FieldBinding.TryParseInvariantDouble(item.Tag as string, out double picked)) return picked;
            }
            if (_parts.Sliders.TryGetValue(key, out var slider) && FieldBinding.TryParseInvariantDouble(slider.StoredText, out double stop))
                return stop;
            return fallback;
        }

        public bool LiveSwitch(string key) => _parts.Switches.TryGetValue(key, out var toggle) && toggle.IsChecked == true;

        public string LiveText(string key, string fallback)
        {
            if (_parts.Boxes.TryGetValue(key, out var box))
                return box.Text;
            if (_parts.Combos.TryGetValue(key, out var combo) && combo.SelectedItem is ComboBoxItem item)
                return item.Tag is IFormattable f ? f.ToString(null, CultureInfo.InvariantCulture) : item.Tag?.ToString() ?? fallback;
            if (_parts.Sliders.TryGetValue(key, out var slider))
                return slider.StoredText;
            return fallback;
        }

        /// <summary>A number grouped with the live thousands separator, as chat would show it.</summary>
        public string Amount(double value, string separatorKey = "numberSeparator")
        {
            // A game without the setting: the separator of the window's language (1,500 in English, 1 500 in French).
            string separator = LiveText(separatorKey, Text.GroupSeparator);
            string text = Math.Round(value).ToString("N0", CultureInfo.InvariantCulture);
            return separator.Length == 0 ? text.Replace(",", "") : text.Replace(",", separator);
        }
    }
}
