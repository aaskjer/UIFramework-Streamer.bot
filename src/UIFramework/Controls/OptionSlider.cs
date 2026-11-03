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

        /// <summary>The value as the settings file holds it: the stop's value (a number when it is one), or the number.</summary>
        public JToken Stored
        {
            get
            {
                if (Options != null)
                {
                    string text = Options.Count == 0 ? "" : Options[Index].Value ?? "";
                    return long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long whole) ? (JToken)whole : text;
                }
                return Whole ? (JToken)(long)Math.Round(Value) : Value;
            }
        }

        public string StoredText => Stored.ToString();

        /// <summary>Moves the slider to a stored value: a stop's value or index, or a number within the range.</summary>
        public bool Put(JToken value)
        {
            string text = value == null || value.Type == JTokenType.Null ? "" : value.Type == JTokenType.String ? (string)value : value.ToString();
            if (Options != null)
            {
                int at = Options.FindIndex(o => string.Equals(o.Value, text, StringComparison.OrdinalIgnoreCase));
                if (at < 0 && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index) && index >= 0 && index < Options.Count
                    && !Options.Any(o => long.TryParse(o.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)))
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
    }
}
