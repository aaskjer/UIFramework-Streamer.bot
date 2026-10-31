using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
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
            var t = Token(key);
            if (t == null || t.Type == JTokenType.Null) return 0;
            if (t.Type == JTokenType.Integer || t.Type == JTokenType.Float) return Convert.ToInt64(((JValue)t).Value, CultureInfo.InvariantCulture);
            return long.TryParse(t.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long l) ? l : 0;
        }

        public int Int(string key)
        {
            long l = Long(key);
            return l > int.MaxValue ? int.MaxValue : l < int.MinValue ? int.MinValue : (int)l;
        }

        public double Double(string key)
        {
            var t = Token(key);
            if (t == null || t.Type == JTokenType.Null) return 0;
            if (t.Type == JTokenType.Integer || t.Type == JTokenType.Float) return Convert.ToDouble(((JValue)t).Value, CultureInfo.InvariantCulture);
            return FieldBinding.TryParseInvariantDouble(t.ToString(), out double d) ? d : 0;
        }

        public string String(string key)
        {
            var t = Token(key);
            return t == null || t.Type == JTokenType.Null ? "" : t.Type == JTokenType.String ? (string)t : t.ToString();
        }

        // ---- Boxes ---------------------------------------------------------------------------------------

        public TextBox SettingBox(string label, string key, string tooltip, FieldKind kind = FieldKind.String)
        {
            var tag = new FieldTag(key, kind);
            var box = new TextBox { Text = FieldBinding.Display(tag, Token(key)), Tag = tag, ToolTip = tooltip };
            Ui.StyleInput(box);
            AutomationProperties.SetName(box, label);
            _parts.Boxes[key] = box;
            return box;
        }

        public TextBox IntBox(string label, string key, string tooltip, bool unlimitedAtZero = false)
        {
            var box = SettingBox(label, key, tooltip, FieldKind.Int);
            ((FieldTag)box.Tag).UnlimitedAtZero = unlimitedAtZero;
            box.Text = FieldBinding.Display((FieldTag)box.Tag, Token(key));
            box.MaxLength = 9;
            Ui.DigitsOnly(box);
            return box;
        }

        /// <summary>A whole-number field for the money settings. With unlimitedAtZero the ceiling settings show
        /// their "no limit" value as 0, which is what gets typed back in to lift it.</summary>
        public TextBox LongBox(string label, string key, string tooltip, bool unlimitedAtZero = false)
        {
            var box = SettingBox(label, key, tooltip, FieldKind.Long);
            ((FieldTag)box.Tag).UnlimitedAtZero = unlimitedAtZero;
            box.Text = FieldBinding.Display((FieldTag)box.Tag, Token(key));
            box.MaxLength = 18;
            Ui.DigitsOnly(box);
            return box;
        }

        public TextBox DoubleBox(string label, string key, string tooltip)
        {
            var box = SettingBox(label, key, tooltip, FieldKind.Double);
            box.MaxLength = 12;
            Ui.DecimalsOnly(box);
            return box;
        }

        public FrameworkElement IntField(string label, string key, string tooltip, bool unlimitedAtZero = false) =>
            Ui.Labeled(label, IntBox(label, key, tooltip, unlimitedAtZero));

        public FrameworkElement LongField(string label, string key, string tooltip, bool unlimitedAtZero = false) =>
            Ui.Labeled(label, LongBox(label, key, tooltip, unlimitedAtZero));

        public FrameworkElement DoubleField(string label, string key, string tooltip) =>
            Ui.Labeled(label, DoubleBox(label, key, tooltip));

        public FrameworkElement TextField(string label, string key, string tooltip) =>
            Ui.Labeled(label, SettingBox(label, key, tooltip));

        /// <summary>A setting stored as a fraction but typed as a percentage. The factor converts one into the
        /// other on save (0.01 for a fraction, 1 for a value already in percent), and the field clamps itself as
        /// soon as it loses focus so a typo cannot reach the saved file.</summary>
        public FrameworkElement PercentField(string label, string key, double factor, string tooltip, double min = 0, double max = 100)
        {
            var tag = new FieldTag(key, FieldKind.Double) { Factor = factor, Min = min, Max = max };
            var box = new TextBox
            {
                Text = FieldBinding.Display(tag, Token(key)),
                Tag = tag,
                MaxLength = 6,
                ToolTip = tooltip + "\nBetween " + min.ToString("0.##", CultureInfo.InvariantCulture) +
                          " and " + max.ToString("0.##", CultureInfo.InvariantCulture) + "%."
            };
            Ui.StyleInput(box);
            AutomationProperties.SetName(box, label);
            _parts.Boxes[key] = box;
            Ui.DecimalsOnly(box);
            box.LostFocus += (s, e) =>
            {
                double clamped = FieldBinding.TryParseFinite(box.Text, out double pct) ? Math.Max(min, Math.Min(max, pct)) : min;
                box.Text = clamped.ToString("0.##", CultureInfo.InvariantCulture);
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
            return combo;
        }

        public ComboBox StringCombo(string label, string key, (string Value, string Label)[] options, string tooltip)
        {
            var combo = NewCombo(key, FieldKind.String, tooltip);
            string current = String(key);
            foreach (var option in options)
                combo.Items.Add(new ComboBoxItem { Content = option.Label, Tag = option.Value });
            combo.SelectedItem = combo.Items.OfType<ComboBoxItem>()
                .FirstOrDefault(i => string.Equals(i.Tag as string, current ?? "", StringComparison.OrdinalIgnoreCase))
                ?? combo.Items.OfType<ComboBoxItem>().FirstOrDefault();
            AutomationProperties.SetName(combo, label);
            _parts.Combos[key] = combo;
            return combo;
        }

        public FrameworkElement StringComboField(string label, string key, (string Value, string Label)[] options, string tooltip) =>
            Ui.Labeled(label, StringCombo(label, key, options, tooltip));

        public FrameworkElement IntCombo(string label, string key, (int Value, string Label)[] options, string tooltip)
        {
            var combo = NewCombo(key, FieldKind.Int, tooltip);
            int value = Int(key);
            foreach (var option in options)
                combo.Items.Add(new ComboBoxItem { Content = option.Label, Tag = option.Value });
            combo.SelectedItem = combo.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (int)i.Tag == value)
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
                ?? combo.Items.OfType<ComboBoxItem>().FirstOrDefault();
            AutomationProperties.SetName(combo, label);
            _parts.Combos[key] = combo;
            return Ui.Labeled(label, combo);
        }

        // ---- Switches ------------------------------------------------------------------------------------

        public CheckBox Toggle(string label, string key, string tooltip)
        {
            var toggle = new CheckBox
            {
                Content = label,
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
            label.Children.Add(new TextBlock { Text = toggle.Content as string, VerticalAlignment = VerticalAlignment.Center });
            label.Children.Add(Ui.Pill("Twitch only", Theme.AccentSoft, Theme.AccentText));
            toggle.Content = label;
            AutomationProperties.SetName(toggle, AutomationProperties.GetName(toggle) + " (Twitch only)");
        }

        // ---- Name lists ----------------------------------------------------------------------------------

        public static List<string> SplitNames(string list) =>
            (list ?? "").Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToList();

        public static bool ContainsName(List<string> names, string name) =>
            names.Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));

        /// <summary>A comma-separated setting shown as removable chips. The real value lives in a hidden box
        /// carrying the tag, so saving, resetting and change tracking all work on it unchanged.</summary>
        public FrameworkElement NameList(string label, string key, string hint, string tooltip)
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

            input.PreviewKeyDown += (sender, e) => { if (e.Key == Key.Enter) { Commit(); e.Handled = true; } };
            input.PreviewTextInput += (sender, e) => { if (e.Text == ",") { Commit(); e.Handled = true; } };
            input.LostKeyboardFocus += (sender, e) => Commit();
            store.TextChanged += (sender, e) => Draw();
            _parts.AddNameCommit(Commit);
            Draw();

            var panel = new StackPanel();
            panel.Children.Add(wrap);
            panel.Children.Add(store);
            return panel;
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
                ToolTip = "Remove " + name,
                Template = Ui.ButtonTemplate(Colors.Transparent, Theme.Hover, Colors.Transparent, 9, new Thickness(0))
            };
            AutomationProperties.SetName(x, "Remove " + name);
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

        // ---- Colours -------------------------------------------------------------------------------------

        private readonly ColourPicker _picker = new ColourPicker();

        public FrameworkElement ColourField(string label, string key, string tooltip)
        {
            var box = SettingBox(label, key, tooltip + "\nType a hex code, or click the swatch to pick one.");
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
                Background = Colours.ParseHexBrush(box.Text),
                Cursor = Cursors.Hand,
                ToolTip = "Click to pick a colour"
            };
            AutomationProperties.SetName(swatch, "Pick a colour for " + label);
            var pick = Ui.IconButton("🎨", null, "Pick " + label.ToLowerInvariant());
            pick.Margin = new Thickness(6, 0, 0, 0);

            box.TextChanged += (sender, e) => swatch.Background = Colours.ParseHexBrush(box.Text);
            swatch.MouseLeftButtonUp += (sender, e) => _picker.Open(box, swatch);
            pick.Click += (sender, e) => _picker.Open(box, swatch);

            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(box);
            row.Children.Add(swatch);
            row.Children.Add(pick);
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
            return fallback;
        }

        public bool LiveSwitch(string key) => _parts.Switches.TryGetValue(key, out var toggle) && toggle.IsChecked == true;

        public string LiveText(string key, string fallback)
        {
            if (_parts.Boxes.TryGetValue(key, out var box))
                return box.Text;
            if (_parts.Combos.TryGetValue(key, out var combo) && combo.SelectedItem is ComboBoxItem item)
                return item.Tag?.ToString() ?? fallback;
            return fallback;
        }

        /// <summary>A number grouped with the live thousands separator, as chat would show it.</summary>
        public string Amount(double value, string separatorKey = "numberSeparator")
        {
            string separator = LiveText(separatorKey, ".");
            string text = Math.Round(value).ToString("N0", CultureInfo.InvariantCulture);
            return separator.Length == 0 ? text.Replace(",", "") : text.Replace(",", separator);
        }
    }
}
