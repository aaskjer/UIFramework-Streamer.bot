using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Newtonsoft.Json.Linq;

namespace UIFramework
{
    /// <summary>
    /// The <c>slider</c> kind: a slider over <c>options</c> (one stop per option, its value is the stop's value)
    /// or over a number range (<c>min</c>, <c>max</c>, <c>step</c>). Takes part in saving, resetting, change
    /// counting and live reads through its <see cref="FieldTag"/> like a box does.
    /// </summary>
    public sealed class OptionSlider : Slider
    {
        /// <summary>The stops; null for a number range.</summary>
        public List<SettingOption> Options;
        /// <summary>A number range of whole numbers.</summary>
        public bool Whole = true;

        public OptionSlider()
        {
            IsSnapToTickEnabled = true;
            IsMoveToPointEnabled = true;
            Height = 22;
            Template = Ui.SliderTemplate();
        }

        /// <summary>The index of the stop the slider sits on; -1 for a number range.</summary>
        public int Index => Options == null ? -1 : Math.Max(0, Math.Min(Options.Count - 1, (int)Math.Round(Value)));

        private JToken _kept;

        /// <summary>Whether the slider still holds a kept value no stop offers.</summary>
        public bool IsKept => _kept != null;

        /// <summary>Keeps a stored value the stops do not offer: <see cref="Stored"/> returns it until the slider moves.</summary>
        public void Keep(JToken value)
        {
            _kept = value?.DeepClone();
            // Cleared before the other ValueChanged handlers run: they see the stop that was chosen.
            ValueChanged += (s, e) => _kept = null;
        }

        /// <summary>Lets go of a kept value: the slider's own stop is the value from now on. The value changes without the
        /// slider moving, so its ValueChanged fires here, as in <see cref="Put"/>: the label, the change count, a condition
        /// on the slider and an <c>ask</c> follow.</summary>
        public void LetGo()
        {
            if (_kept == null) return;
            _kept = null;
            OnValueChanged(Value, Value);
        }

        /// <summary>The value as the settings file holds it: the stop's value (a number when it is one), or the number.</summary>
        public JToken Stored
        {
            get
            {
                if (_kept != null) return _kept;
                if (Options != null)
                {
                    string text = Options.Count == 0 ? "" : Options[Index].Value ?? "";
                    return long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long whole) ? (JToken)whole : text;
                }
                // A stop of a decimal range is a multiple of the step, with its float noise: three steps of 0.1 are
                // 0.30000000000000004. Ten decimals, as a number box shows, leave 0.3 beside the label and in the file.
                return Whole ? (JToken)(long)Math.Round(Value) : Math.Round(Value, 10);
            }
        }

        public string StoredText => FieldBinding.Invariant(Stored);

        /// <summary>Moves the slider to a stored value: a stop's value or index, or a number within the range. A kept
        /// value goes; when the slider already sat on that stop, its ValueChanged still fires, so its label and the
        /// change count follow.</summary>
        public bool Put(JToken value)
        {
            if (!PutValue(value)) return false;
            if (_kept != null)
            {
                _kept = null;
                OnValueChanged(Value, Value);
            }
            return true;
        }

        private bool PutValue(JToken value)
        {
            string text = FieldBinding.Invariant(value);
            if (Options != null)
            {
                int at = Options.FindIndex(o => string.Equals(o.Value, text, StringComparison.OrdinalIgnoreCase));
                // A number finds the stop of the same number however either is written: a project's default 2.0 arrives
                // as "2" and is the stop "2.0". It was taken for the index 2 when no stop was a whole number, the stop "5.0".
                if (at < 0 && IsNumber(text, out double wanted))
                    at = Options.FindIndex(o => IsNumber(o.Value, out double stop) && stop == wanted);
                if (at < 0 && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index) && index >= 0 && index < Options.Count
                    && !Options.Any(o => IsNumber(o.Value, out _)))
                    at = index;
                if (at < 0)
                    return false;
                Value = at;
                return true;
            }
            if (!FieldBinding.TryParseFinite(text, out double number))
                return false;
            Value = Math.Max(Minimum, Math.Min(Maximum, number));
            return true;
        }

        /// <summary>A stop's value or a stored one as a number, with a point: "1,000" is none.</summary>
        private static bool IsNumber(string text, out double number) =>
            double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number) && !double.IsNaN(number);
    }
}
