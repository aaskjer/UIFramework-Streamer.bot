using System;

namespace UIFramework
{
    public enum FieldKind { Bool, Int, Long, Double, String, Template, List }

    /// <summary>
    /// What a control's Tag carries: the setting key and how its text becomes a value. Saving, resetting and
    /// change tracking all go through the tag, so a control with a tag is a setting and one without is
    /// furniture. ToString returns the key, for the places that only want the name.
    /// </summary>
    public sealed class FieldTag
    {
        public string Key;
        public FieldKind Kind = FieldKind.String;

        /// <summary>Typed value times Factor is the stored value: a percent field over a fraction has 0.01.</summary>
        public double Factor = 1.0;
        public double Min = double.MinValue;
        public double Max = double.MaxValue;

        /// <summary>The ceiling settings show "no limit" as 0 and store it as the type's MaxValue.</summary>
        public bool UnlimitedAtZero;

        /// <summary>Never saved and never counted as a change; only a live snapshot carries it.</summary>
        public bool Transient;

        /// <summary>A plain text setting: saved as typed, spaces at either end included (a separator " | ", a prefix
        /// "🎲 "). Without it a text is trimmed, as names, paths, colours and tokens want.</summary>
        public bool KeepSpaces;

        public FieldTag(string key, FieldKind kind) { Key = key; Kind = kind; }

        public override string ToString() => Key;

        public static FieldTag Of(object tag) => tag as FieldTag;
    }
}
