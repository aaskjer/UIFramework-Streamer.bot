using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UIFramework
{
    /// <summary>
    /// What a host knows: its settings with defaults and hints. Read from the JSON the host answers
    /// <c>uiframework.describe</c> with (see docs/Schema.md); never holds values.
    /// </summary>
    public sealed class Schema
    {
        public string Host = "";
        public string Version = "";
        public string SettingsFile = "";
        /// <summary>The window as the host declares it beside its settings (title, pages, help, about, log): the
        /// same JSON a stub's spec carries. Null when the stub brings a spec of its own.</summary>
        public JObject Window;

        /// <summary>The points system the game found installed (<c>currency</c>: source, variable, name), or null:
        /// the window shows it as a pill and marks the two points settings as the fallback they are.</summary>
        public JObject Currency;
        public readonly List<Setting> Settings = new List<Setting>();

        public Setting Find(string key) => Settings.FirstOrDefault(s => string.Equals(s.Key, key, StringComparison.Ordinal));

        /// <summary>Every default as a settings object, for the store.</summary>
        public JObject Defaults()
        {
            var defaults = new JObject();
            foreach (var setting in Settings)
                defaults[setting.Key] = setting.Default?.DeepClone() ?? JValue.CreateNull();
            return defaults;
        }

        /// <summary>Alias to canonical key, from every setting's own alias list.</summary>
        public Dictionary<string, string> Aliases()
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var setting in Settings)
                foreach (var alias in setting.Aliases)
                    if (!map.ContainsKey(alias))
                        map[alias] = setting.Key;
            return map;
        }

        public static Schema Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("The schema is empty.");
            JObject root;
            using (var reader = new JsonTextReader(new System.IO.StringReader(json)) { DateParseHandling = DateParseHandling.None })
                root = JObject.Load(reader);
            return Parse(root);
        }

        public static Schema Parse(JObject root)
        {
            var schema = new Schema
            {
                Host = (string)root["host"] ?? "",
                Version = (string)root["version"] ?? "",
                SettingsFile = (string)root["settingsFile"] ?? "",
                Window = root["window"] as JObject,
                Currency = root["currency"] as JObject
            };
            if (!(root["settings"] is JArray list))
                throw new ArgumentException("The schema has no 'settings' array.");
            int order = 0;
            foreach (var item in list.OfType<JObject>())
            {
                var setting = Setting.Parse(item);
                setting.Order = order++;
                if (string.IsNullOrWhiteSpace(setting.Key))
                    throw new ArgumentException("A schema setting has no key.");
                if (schema.Find(setting.Key) != null)
                    throw new ArgumentException("The schema names '" + setting.Key + "' twice.");
                schema.Settings.Add(setting);
            }
            return schema;
        }

        public JObject ToJson()
        {
            var root = new JObject
            {
                ["host"] = Host,
                ["version"] = Version,
                ["settingsFile"] = SettingsFile,
                ["settings"] = new JArray(Settings.Select(s => s.ToJson()))
            };
            if (Window != null) root["window"] = Window.DeepClone();
            if (Currency != null) root["currency"] = Currency.DeepClone();
            return root;
        }
    }

    /// <summary>One setting as the host declares it. Fields left null are filled from the conventions, then
    /// derived from the default. See docs/Schema.md for what each means.</summary>
    public sealed class Setting
    {
        public string Key;
        public JToken Default;
        public string Kind;
        public string Page;
        public string Section;
        public string Group;
        public string Label;
        public string Help;
        public double? Min, Max, Step;
        public string Unit;
        /// <summary>For an <c>emote</c> field: twitch, youtube or kick, which says where its picture comes from.</summary>
        public string Platform;
        public double? Factor;
        public bool UnlimitedAtZero;
        public List<SettingOption> Options;
        public List<string> Placeholders;
        public string Requires;
        /// <summary>Settings that name the same row, one after the other, share one line of the page, whatever
        /// their kind: a bet, a role and a Try button side by side.</summary>
        public string Row;
        /// <summary>For an action: the question asked before the host is called. Null asks nothing.</summary>
        public string Confirm;
        public bool Advanced;
        public bool ReadOnly;
        public bool Hidden;
        /// <summary>Shown and taken into a live snapshot, but never written to the file and never counted as a
        /// change: the inputs of a Try button.</summary>
        public bool Transient;
        /// <summary>For a bool: the switch carries a "Twitch only" pill.</summary>
        public bool TwitchOnly;
        /// <summary>For an action: the host gets the values as they stand in the window, unsaved ones included. On
        /// any other field: the host is asked (<c>Test(key, live settings)</c>) whenever the field changes, and its
        /// answer's <c>set</c> fills in other fields, a preset slider.</summary>
        public bool Live;
        /// <summary>For an action: the host's answer is shown in a panel under the button instead of the footer.</summary>
        public bool Panel;
        /// <summary>For an action: no button; the host is asked when the page is shown and after every change.</summary>
        public bool Auto;
        public List<SettingColumn> Columns;
        /// <summary>A file of its own next to the settings file that holds this value (see Sidecars), and the
        /// property in it; no field: the file's root is the value.</summary>
        public string File, Field;
        public List<string> Aliases = new List<string>();
        public Dictionary<string, double> AliasFactors = new Dictionary<string, double>(StringComparer.Ordinal);
        public int Order;

        /// <summary>The kind a default value suggests when nothing says otherwise.</summary>
        public static string KindOf(JToken value)
        {
            if (value == null) return "string";
            switch (value.Type)
            {
                case JTokenType.Boolean: return "bool";
                case JTokenType.Integer: return "int";
                case JTokenType.Float: return "double";
                case JTokenType.Array: return "list";
                default: return "string";
            }
        }

        public static Setting Parse(JObject item)
        {
            var setting = new Setting
            {
                Key = (string)item["key"],
                Default = item["default"]?.DeepClone(),
                Kind = (string)item["kind"],
                Page = (string)item["page"],
                Section = (string)item["section"],
                Group = (string)item["group"],
                Label = (string)item["label"],
                Help = (string)item["help"],
                Min = (double?)item["min"],
                Max = (double?)item["max"],
                Step = (double?)item["step"],
                Unit = (string)item["unit"],
                Platform = (string)item["platform"],
                Factor = (double?)item["factor"],
                UnlimitedAtZero = item["unlimitedAtZero"]?.Type == JTokenType.Boolean && (bool)item["unlimitedAtZero"],
                Requires = (string)item["requires"],
                Row = (string)item["row"],
                Confirm = (string)item["confirm"],
                Advanced = item["advanced"]?.Type == JTokenType.Boolean && (bool)item["advanced"],
                ReadOnly = item["readonly"]?.Type == JTokenType.Boolean && (bool)item["readonly"],
                Hidden = item["hidden"]?.Type == JTokenType.Boolean && (bool)item["hidden"],
                Transient = item["transient"]?.Type == JTokenType.Boolean && (bool)item["transient"],
                TwitchOnly = item["twitchOnly"]?.Type == JTokenType.Boolean && (bool)item["twitchOnly"],
                Live = item["live"]?.Type == JTokenType.Boolean && (bool)item["live"],
                Panel = item["panel"]?.Type == JTokenType.Boolean && (bool)item["panel"],
                Auto = item["auto"]?.Type == JTokenType.Boolean && (bool)item["auto"],
                File = (string)item["file"],
                Field = (string)item["field"]
            };
            if (item["options"] is JArray options)
                setting.Options = options.Select(SettingOption.Parse).ToList();
            if (item["placeholders"] is JArray placeholders)
                setting.Placeholders = placeholders.Select(p => p.ToString()).ToList();
            if (item["columns"] is JArray columns)
                setting.Columns = columns.OfType<JObject>().Select(SettingColumn.Parse).ToList();
            if (item["aliases"] is JArray aliases)
                setting.Aliases = aliases.Select(a => a.ToString()).ToList();
            if (item["aliasFactors"] is JObject factors)
                foreach (var property in factors.Properties())
                    setting.AliasFactors[property.Name] = (double)property.Value;
            return setting;
        }

        public JObject ToJson()
        {
            var item = new JObject { ["key"] = Key, ["default"] = Default?.DeepClone() ?? JValue.CreateNull() };
            if (Kind != null) item["kind"] = Kind;
            if (Page != null) item["page"] = Page;
            if (Section != null) item["section"] = Section;
            if (Group != null) item["group"] = Group;
            if (Label != null) item["label"] = Label;
            if (Help != null) item["help"] = Help;
            if (Min.HasValue) item["min"] = Min.Value;
            if (Max.HasValue) item["max"] = Max.Value;
            if (Step.HasValue) item["step"] = Step.Value;
            if (Unit != null) item["unit"] = Unit;
            if (Platform != null) item["platform"] = Platform;
            if (Factor.HasValue) item["factor"] = Factor.Value;
            if (UnlimitedAtZero) item["unlimitedAtZero"] = true;
            if (Options != null) item["options"] = new JArray(Options.Select(o => o.ToJson()));
            if (Placeholders != null) item["placeholders"] = new JArray(Placeholders);
            if (Requires != null) item["requires"] = Requires;
            if (Row != null) item["row"] = Row;
            if (Confirm != null) item["confirm"] = Confirm;
            if (Advanced) item["advanced"] = true;
            if (ReadOnly) item["readonly"] = true;
            if (Hidden) item["hidden"] = true;
            if (Transient) item["transient"] = true;
            if (TwitchOnly) item["twitchOnly"] = true;
            if (Live) item["live"] = true;
            if (Panel) item["panel"] = true;
            if (Auto) item["auto"] = true;
            if (Columns != null) item["columns"] = new JArray(Columns.Select(c => c.ToJson()));
            if (File != null) item["file"] = File;
            if (Field != null) item["field"] = Field;
            if (Aliases.Count > 0) item["aliases"] = new JArray(Aliases);
            if (AliasFactors.Count > 0) item["aliasFactors"] = JObject.FromObject(AliasFactors);
            return item;
        }

        public Setting Clone()
        {
            var copy = (Setting)MemberwiseClone();
            copy.Default = Default?.DeepClone();
            copy.Options = Options?.Select(o => new SettingOption { Value = o.Value, Label = o.Label }).ToList();
            copy.Placeholders = Placeholders?.ToList();
            copy.Columns = Columns?.Select(c => new SettingColumn { Key = c.Key, Kind = c.Kind, Label = c.Label, Platform = c.Platform }).ToList();
            copy.Aliases = Aliases.ToList();
            copy.AliasFactors = new Dictionary<string, double>(AliasFactors, StringComparer.Ordinal);
            return copy;
        }
    }

    public sealed class SettingOption
    {
        public string Value, Label;

        public static SettingOption Parse(JToken token)
        {
            if (token is JObject o)
                return new SettingOption { Value = (string)o["value"] ?? "", Label = (string)o["label"] ?? (string)o["value"] ?? "" };
            string text = token.ToString();
            return new SettingOption { Value = text, Label = text };
        }

        public JToken ToJson() => Value == Label ? (JToken)Value : new JObject { ["value"] = Value, ["label"] = Label };
    }

    public sealed class SettingColumn
    {
        public string Key, Kind = "string", Label;
        /// <summary>For an <c>emote</c> column: twitch, youtube or kick, which says where its picture comes from.</summary>
        public string Platform;

        public static SettingColumn Parse(JObject o) => new SettingColumn
        {
            Key = (string)o["key"],
            Kind = (string)o["kind"] ?? "string",
            Label = (string)o["label"] ?? (string)o["key"],
            Platform = (string)o["platform"]
        };

        public JObject ToJson()
        {
            var o = new JObject { ["key"] = Key, ["kind"] = Kind, ["label"] = Label };
            if (Platform != null) o["platform"] = Platform;
            return o;
        }
    }
}
