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
                SettingsFile = (string)root["settingsFile"] ?? ""
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
        public double? Factor;
        public bool UnlimitedAtZero;
        public List<SettingOption> Options;
        public List<string> Placeholders;
        public string Requires;
        public bool Advanced;
        public bool ReadOnly;
        public bool Hidden;
        public List<SettingColumn> Columns;
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
                Factor = (double?)item["factor"],
                UnlimitedAtZero = item["unlimitedAtZero"]?.Type == JTokenType.Boolean && (bool)item["unlimitedAtZero"],
                Requires = (string)item["requires"],
                Advanced = item["advanced"]?.Type == JTokenType.Boolean && (bool)item["advanced"],
                ReadOnly = item["readonly"]?.Type == JTokenType.Boolean && (bool)item["readonly"],
                Hidden = item["hidden"]?.Type == JTokenType.Boolean && (bool)item["hidden"]
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
            if (Factor.HasValue) item["factor"] = Factor.Value;
            if (UnlimitedAtZero) item["unlimitedAtZero"] = true;
            if (Options != null) item["options"] = new JArray(Options.Select(o => o.ToJson()));
            if (Placeholders != null) item["placeholders"] = new JArray(Placeholders);
            if (Requires != null) item["requires"] = Requires;
            if (Advanced) item["advanced"] = true;
            if (ReadOnly) item["readonly"] = true;
            if (Hidden) item["hidden"] = true;
            if (Columns != null) item["columns"] = new JArray(Columns.Select(c => c.ToJson()));
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
            copy.Columns = Columns?.Select(c => new SettingColumn { Key = c.Key, Kind = c.Kind, Label = c.Label }).ToList();
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

        public static SettingColumn Parse(JObject o) => new SettingColumn
        {
            Key = (string)o["key"],
            Kind = (string)o["kind"] ?? "string",
            Label = (string)o["label"] ?? (string)o["key"]
        };

        public JObject ToJson() => new JObject { ["key"] = Key, ["kind"] = Kind, ["label"] = Label };
    }
}
