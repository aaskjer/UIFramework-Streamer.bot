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
        /// <summary>The window as the host declares it beside its settings (title, pages, help, about, log). Null
        /// when it declares none: the window is then named after the host, its pages the ones the settings name.</summary>
        public JObject Window;

        /// <summary>The points system the game found installed (<c>currency</c>: source, variable, name), or null:
        /// the window shows it as a pill and marks the two points settings as the fallback they are.</summary>
        public JObject Currency;
        /// <summary>The game's own translations by language code, in the shape of a conventions language file
        /// (<c>keys</c>, <c>pages</c>, <c>texts</c>), laid over the conventions' when the window is in that language.</summary>
        public JObject Lang;
        public readonly List<Setting> Settings = new List<Setting>();

        /// <summary>The settings a Set Argument sub-action of the project's own action sets (<c>arguments</c>: the
        /// setting's key and the argument's name, which may be an old key). Such an argument wins over the file
        /// while it is there, so the window shows the field locked and says where the value comes from.</summary>
        public readonly Dictionary<string, string> Arguments = new Dictionary<string, string>(StringComparer.Ordinal);
        /// <summary>The fields the project locks itself (<c>lockedFields</c>: key and why): shown, not edited.</summary>
        public readonly Dictionary<string, string> LockedFields = new Dictionary<string, string>(StringComparer.Ordinal);

        public Setting Find(string key) => Settings.FirstOrDefault(s => string.Equals(s.Key, key, StringComparison.Ordinal));

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
                Currency = root["currency"] as JObject,
                Lang = root["lang"] as JObject
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
            // "arguments": { key: argument name }, or a list of keys whose arguments carry the key's own name.
            if (root["arguments"] is JObject arguments)
                foreach (var property in arguments.Properties())
                    schema.Arguments[property.Name] = property.Value.Type == JTokenType.String && !string.IsNullOrWhiteSpace((string)property.Value) ? (string)property.Value : property.Name;
            else if (root["arguments"] is JArray argumentKeys)
                foreach (var key in argumentKeys.Where(k => k.Type == JTokenType.String))
                    schema.Arguments[(string)key] = (string)key;
            if (root["lockedFields"] is JObject locked)
                foreach (var property in locked.Properties())
                    schema.LockedFields[property.Name] = property.Value.Type == JTokenType.String ? (string)property.Value : "";
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
            if (Lang != null) root["lang"] = Lang.DeepClone();
            if (Arguments.Count > 0) root["arguments"] = JObject.FromObject(Arguments);
            if (LockedFields.Count > 0) root["lockedFields"] = JObject.FromObject(LockedFields);
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
        /// <summary>For a message: "percent" when its text writes %name% instead of {name}; null takes the window's.</summary>
        public string PlaceholderStyle;
        /// <summary>A secret that lives in a persisted global of Streamer.bot of this name, not in the settings file:
        /// the settings action reads it for the window and writes it after a save. Never in the file, an export or the
        /// log; a reset leaves it. Needs a settings action that knows globals (stub 0.3.3).</summary>
        public string Global;
        /// <summary>The setting names a global the window neither reads nor writes: declared so that it cannot work,
        /// or the host has no globals (<see cref="IHostHooks.HasGlobals"/>). Filled in by <see cref="Resolve"/>, which
        /// locks the field; not part of a setting's own declaration.</summary>
        public bool GlobalOff;
        /// <summary>For a dropdown: its options come from the project (<see cref="IHostHooks.Options"/>), asked when the
        /// window opens and whenever the field of this key changes. "groups" offers Streamer.bot's groups; an empty
        /// text asks once.</summary>
        public string OptionsFrom;
        /// <summary>For an <c>auto</c> action: asked again every so many seconds while its page shows.</summary>
        public int? Every;
        /// <summary>For an action: the window's changes are saved before the project is asked.</summary>
        public bool SaveFirst;
        /// <summary>For a list the project writes to as well: the columns that name a row ("date,name"). A save then
        /// merges with what the file holds at that moment: the project's rows stay, the user's changes win.</summary>
        public string RowKey;
        /// <summary>For a list: rows are neither added nor removed, only edited.</summary>
        public bool FixedRows;
        /// <summary>For a list: the rows shown, "region= || Global=true || Counties~{region}": conditions joined by
        /// <c>||</c>, <c>{key}</c> a field's value in the window, <c>~</c> contains. The others stay in the file.</summary>
        public string Filter;
        public string Requires;
        /// <summary>Settings that name the same row, one after the other, share one line of the page, whatever
        /// their kind: a bet, a role and a Try button side by side.</summary>
        public string Row;
        /// <summary>For an action: the question asked before the host is called. Null asks nothing.</summary>
        public string Confirm;
        public bool ReadOnly;
        /// <summary>The Set Argument that sets this value in the project's action (the schema's <c>arguments</c>), or
        /// null; and why the project locks the field (its <c>lockedFields</c>), or null. Filled in by
        /// <see cref="Resolve"/>, which makes either field read-only; not part of a setting's own declaration.</summary>
        public string Argument, LockReason;
        /// <summary>Locked by a Set Argument or by the project: shown with a pill and the reason, not edited.</summary>
        public bool Locked => Argument != null || LockReason != null;
        public bool Hidden;
        /// <summary>Shown and taken into a live snapshot, but never written to the file and never counted as a
        /// change: the inputs of a Try button.</summary>
        public bool Transient;
        /// <summary>For a bool: the switch carries a "Twitch only" pill.</summary>
        public bool TwitchOnly;
        /// <summary>A text field whose value is shown as dots until its eye button reveals it (a token, a secret).</summary>
        public bool Masked;
        /// <summary>Grey text inside a one-line text field while it is empty ("not set", "optional"), masked or not.</summary>
        public string Hint;
        /// <summary>The field is shown only while this condition holds (<see cref="Requirement"/>):
        /// <c>"questionSource=igdb"</c>, <c>"questionSource=opentdb|wikidata"</c>, <c>"mode!=free &amp;&amp; useBot"</c>.</summary>
        public string ShowWhen;
        /// <summary>On a dropdown: which fields each of its options shows, by option value; a field named here is
        /// shown only while one of the options naming it is picked.</summary>
        public Dictionary<string, List<string>> Shows;
        /// <summary>The condition of the field's section (<c>.Section(name, showWhen)</c> of the window), filled in by
        /// <see cref="Resolve"/>; not part of the schema.</summary>
        public string SectionShowWhen;
        /// <summary>For an action: the host gets the values as they stand in the window, unsaved ones included. On
        /// any other field: the host is asked (<c>Test(key, live settings)</c>) whenever the field changes, and its
        /// answer's <c>set</c> fills in other fields, a preset slider.</summary>
        public bool Live;
        /// <summary>On a field that is no button: the host is asked (<c>Test(key, live settings)</c>) whenever the field
        /// changes, and its answer's <c>set</c> fills in other fields, a preset slider. What <see cref="Live"/> on such
        /// a field also means, under a name of its own.</summary>
        public bool Ask;
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
        /// <summary>The section the field sits in, by the name the game declared it (or the user's layout gave it),
        /// untranslated; filled in by Resolve. <see cref="Section"/> is what the window shows.</summary>
        public string SectionId;

        /// <summary>The kind a default value suggests when nothing says otherwise.</summary>
        public static string KindOf(JToken value)
        {
            if (value == null) return "string";
            switch (value.Type)
            {
                case JTokenType.Boolean: return "bool";
                case JTokenType.Integer: return FitsInt(value) ? "int" : "long";
                case JTokenType.Float: return "double";
                case JTokenType.Array: return "list";
                default: return "string";
            }
        }

        /// <summary>A whole number that an int holds; a bigger one needs a long field.</summary>
        public static bool FitsInt(JToken value)
        {
            if (value == null || value.Type != JTokenType.Integer) return true;
            // A number with more digits than a long is a BigInteger, which converts to nothing: it fits no int either.
            try { long l = Convert.ToInt64(((JValue)value).Value, System.Globalization.CultureInfo.InvariantCulture); return l >= int.MinValue && l <= int.MaxValue; }
            catch (Exception ex) when (ex is OverflowException || ex is InvalidCastException || ex is FormatException) { return false; }
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
                ReadOnly = (item["readOnly"] ?? item["readonly"])?.Type == JTokenType.Boolean && (bool)(item["readOnly"] ?? item["readonly"]),
                Hidden = item["hidden"]?.Type == JTokenType.Boolean && (bool)item["hidden"],
                Transient = item["transient"]?.Type == JTokenType.Boolean && (bool)item["transient"],
                TwitchOnly = item["twitchOnly"]?.Type == JTokenType.Boolean && (bool)item["twitchOnly"],
                Masked = item["masked"]?.Type == JTokenType.Boolean && (bool)item["masked"],
                Hint = item["hint"]?.Type == JTokenType.String ? (string)item["hint"] : null,
                ShowWhen = (string)item["showWhen"],
                Live = item["live"]?.Type == JTokenType.Boolean && (bool)item["live"],
                Panel = item["panel"]?.Type == JTokenType.Boolean && (bool)item["panel"],
                Auto = item["auto"]?.Type == JTokenType.Boolean && (bool)item["auto"],
                File = (string)item["file"],
                Field = (string)item["field"],
                Global = (string)item["global"],
                OptionsFrom = (string)item["optionsFrom"],
                Every = item["every"]?.Type == JTokenType.Integer ? (int?)(int)item["every"] : null,
                SaveFirst = item["saveFirst"]?.Type == JTokenType.Boolean && (bool)item["saveFirst"],
                RowKey = (string)item["rowKey"],
                FixedRows = item["fixedRows"]?.Type == JTokenType.Boolean && (bool)item["fixedRows"],
                Filter = (string)item["filter"]
            };
            // `ask` is the name for a field that asks the game whenever it changes; `live` on anything but a button
            // meant the same and is still read. `auto` (a button's answer without the button) always follows what is
            // typed and has its own box, so it brings `live` and `panel` with it.
            setting.Ask = item["ask"]?.Type == JTokenType.Boolean && (bool)item["ask"];
            if (setting.Auto) { setting.Live = true; setting.Panel = true; }
            // A schema written by hand may use the builder's short forms, which docs/Schema.md shows beside the JSON:
            // an option "value|Label", a column "key|kind|label", shows as "option: key, key", one aliasFactor for the
            // first old key.
            if (item["options"] is JArray options)
                setting.Options = options.Select(SettingOption.Parse).ToList();
            if (item["shows"] is JObject shows)
                setting.Shows = shows.Properties().ToDictionary(p => p.Name, p => (p.Value as JArray ?? new JArray()).Select(k => k.ToString()).ToList(), StringComparer.Ordinal);
            else if (item["shows"] is JArray showList)
            {
                setting.Shows = new Dictionary<string, List<string>>(StringComparer.Ordinal);
                foreach (string entry in showList.Select(e => e.ToString()))
                {
                    int colon = entry.IndexOf(':');
                    if (colon <= 0) continue;
                    string option = entry.Substring(0, colon).Trim();
                    if (!setting.Shows.TryGetValue(option, out var keys)) setting.Shows[option] = keys = new List<string>();
                    keys.AddRange(entry.Substring(colon + 1).Split(',').Select(k => k.Trim()).Where(k => k.Length > 0));
                }
            }
            if (item["placeholders"] is JArray placeholders)
                setting.Placeholders = placeholders.Select(p => p.ToString()).ToList();
            if (item["placeholderStyle"]?.Type == JTokenType.String)
                setting.PlaceholderStyle = (string)item["placeholderStyle"];
            if (item["columns"] is JArray columns)
                setting.Columns = columns.Where(c => c is JObject || c.Type == JTokenType.String)
                    .Select(c => c is JObject o ? SettingColumn.Parse(o) : SettingColumn.Parse(c.ToString())).ToList();
            if (item["aliases"] is JArray aliases)
                setting.Aliases = aliases.Select(a => a.ToString()).ToList();
            if (item["aliasFactors"] is JObject factors)
                foreach (var property in factors.Properties())
                    setting.AliasFactors[property.Name] = (double)property.Value;
            if (setting.Aliases.Count > 0 && (item["aliasFactor"]?.Type == JTokenType.Integer || item["aliasFactor"]?.Type == JTokenType.Float)
                && !setting.AliasFactors.ContainsKey(setting.Aliases[0]))
                setting.AliasFactors[setting.Aliases[0]] = (double)item["aliasFactor"];
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
            if (PlaceholderStyle != null) item["placeholderStyle"] = PlaceholderStyle;
            if (Requires != null) item["requires"] = Requires;
            if (Row != null) item["row"] = Row;
            if (Global != null) item["global"] = Global;
            if (OptionsFrom != null) item["optionsFrom"] = OptionsFrom;
            if (Every.HasValue) item["every"] = Every.Value;
            if (SaveFirst) item["saveFirst"] = true;
            if (RowKey != null) item["rowKey"] = RowKey;
            if (FixedRows) item["fixedRows"] = true;
            if (Filter != null) item["filter"] = Filter;
            if (Confirm != null) item["confirm"] = Confirm;
            if (ReadOnly) item["readOnly"] = true;
            if (Hidden) item["hidden"] = true;
            if (Transient) item["transient"] = true;
            if (TwitchOnly) item["twitchOnly"] = true;
            if (Masked) item["masked"] = true;
            if (Hint != null) item["hint"] = Hint;
            if (ShowWhen != null) item["showWhen"] = ShowWhen;
            if (Shows != null) item["shows"] = new JObject(Shows.Select(p => new JProperty(p.Key, new JArray(p.Value))));
            if (Live) item["live"] = true;
            if (Ask) item["ask"] = true;
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
            copy.Shows = Shows?.ToDictionary(p => p.Key, p => p.Value.ToList(), StringComparer.Ordinal);
            copy.Columns = Columns?.Select(c => new SettingColumn { Key = c.Key, Kind = c.Kind, Label = c.Label, Platform = c.Platform, ReadOnly = c.ReadOnly }).ToList();
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
            // A number option is read without the system's decimal mark: 0.5 stays "0.5" on a German Windows.
            string text = FieldBinding.Invariant(token);
            // The builder's short form, "value|Label", in a schema written by hand.
            int bar = token.Type == JTokenType.String ? text.IndexOf('|') : -1;
            if (bar >= 0)
                return new SettingOption { Value = text.Substring(0, bar), Label = text.Substring(bar + 1) };
            return new SettingOption { Value = text, Label = text };
        }

        public JToken ToJson() => Value == Label ? (JToken)Value : new JObject { ["value"] = Value, ["label"] = Label };
    }

    public sealed class SettingColumn
    {
        public string Key, Kind = "string", Label;
        /// <summary>For an <c>emote</c> column: twitch, youtube or kick, which says where its picture comes from.</summary>
        public string Platform;
        /// <summary>A column the game fills and the user only reads (who suggested a question, when it arrived).</summary>
        public bool ReadOnly;

        public static SettingColumn Parse(JObject o) => new SettingColumn
        {
            Key = (string)o["key"],
            Kind = (string)o["kind"] ?? "string",
            Label = (string)o["label"] ?? (string)o["key"],
            Platform = (string)o["platform"],
            ReadOnly = (o["readOnly"] ?? o["readonly"])?.Type == JTokenType.Boolean && (bool)(o["readOnly"] ?? o["readonly"])
        };

        /// <summary>The builder's short form, "key|kind|label", with "readonly" and an emote column's platform as flags
        /// at the end; the label may itself contain a | and is the key when left out.</summary>
        public static SettingColumn Parse(string text)
        {
            var parts = (text ?? "").Split('|').ToList();
            var column = new SettingColumn { Key = parts[0].Trim(), Kind = parts.Count > 1 && parts[1].Trim().Length > 0 ? parts[1].Trim() : "string" };
            bool Flag(string part) => part == "readonly" || part == "twitch" || part == "youtube" || part == "kick";
            while (parts.Count > 3 || (parts.Count == 3 && Flag(parts[2])))
            {
                string last = parts[parts.Count - 1];
                if (last == "readonly") column.ReadOnly = true;
                else if (Flag(last)) column.Platform = last;
                else break;
                parts.RemoveAt(parts.Count - 1);
            }
            column.Label = parts.Count > 2 ? string.Join("|", parts.Skip(2)) : column.Key;
            return column;
        }

        public JObject ToJson()
        {
            var o = new JObject { ["key"] = Key, ["kind"] = Kind, ["label"] = Label };
            if (Platform != null) o["platform"] = Platform;
            if (ReadOnly) o["readOnly"] = true;
            return o;
        }
    }
}
