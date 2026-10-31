using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// UIFramework for streamer.bot: the schema block for a host's game action.
//
// Paste into the game action (the one that reads the settings), next to CPHInline. The game reads its settings
// through UiSettings, declares them once in UiSchema.Table, and answers the window's describe request with
// UiSchema.ToJson(). One table, so the game and its window can never disagree.
//
// In Execute(), first thing:
//
//     if (args.ContainsKey("uiframework.describe"))
//     {
//         CPH.SetGlobalVar("uiframework.schema." + ActionName, UiSchema.ToJson("blackjack", "v16.3.1", "Chat Games/Blackjack Game/BJG-Settings.json"), false);
//         return true;
//     }
//
// Reading a setting:   var s = UiSettings.Load(path);   bool useBot = s.Bool("useBot");   int decks = s.Int("numDecks");

public static class UiSchema
{
    public sealed class Setting
    {
        public string Key; public object Default; public string Kind, Page, Section, Group, Label, Help, Unit, Requires;
        public double? Min, Max, Step, Factor; public bool UnlimitedAtZero, Hidden, Advanced;
        public string[] Options, Placeholders, Aliases;
        public Dictionary<string, double> AliasFactors;
    }

    /// <summary>A setting: key and default, then whatever the conventions cannot know. Keys the conventions
    /// know (useBot, globalCooldown, blockedUsers, ...) need nothing but their default.</summary>
    public static Setting S(string key, object @default, string page = null, string label = null, string help = null, string kind = null,
        string section = null, string group = null, double? min = null, double? max = null, string unit = null, double? factor = null,
        bool unlimitedAtZero = false, string[] options = null, string[] placeholders = null, string requires = null, bool hidden = false,
        string[] aliases = null)
    {
        return new Setting
        {
            Key = key, Default = @default, Page = page, Label = label, Help = help, Kind = kind, Section = section, Group = group,
            Min = min, Max = max, Unit = unit, Factor = factor, UnlimitedAtZero = unlimitedAtZero, Options = options,
            Placeholders = placeholders, Requires = requires, Hidden = hidden, Aliases = aliases
        };
    }

    /// <summary>A message template: group, key, default text, label, help, placeholders without braces.</summary>
    public static Setting Msg(string group, string key, string @default, string label, string help, params string[] placeholders)
        => S(key, @default, kind: "template", page: "messages", group: group, label: label, help: help, placeholders: placeholders);

    // ---- The table: every setting the game reads. Order is the order on the page. ------------------
    public static readonly Setting[] Table =
    {
        S("useBot", true),
        S("sendAction", false),
        S("numberSeparator", "."),
        S("freeGame", false, page: "table", section: "Betting", label: "Free Game", help: "Nothing is staked and nothing is won."),
        S("minBet", 1L, page: "table", section: "Betting", label: "Minimum bet", kind: "long"),
        S("maxBet", 0L, page: "table", section: "Betting", label: "Maximum bet", kind: "long", unlimitedAtZero: true, help: "0 means no ceiling."),
        S("globalCooldown", 0),
        S("userCooldown", 30),
        S("blockedUsers", ""),
        S("blockedGroups", ""),
        Msg("Playing a hand", "msgGameStart", "@{user} is dealt {cards} ({value}).", "Hand dealt", "Opens a round.", "user", "cards", "value"),
    };

    public static string ToJson(string host, string version, string settingsFile)
    {
        var settings = new JArray();
        foreach (var s in Table)
        {
            var item = new JObject { ["key"] = s.Key, ["default"] = s.Default == null ? JValue.CreateNull() : JToken.FromObject(s.Default) };
            if (s.Kind != null) item["kind"] = s.Kind;
            if (s.Page != null) item["page"] = s.Page;
            if (s.Section != null) item["section"] = s.Section;
            if (s.Group != null) item["group"] = s.Group;
            if (s.Label != null) item["label"] = s.Label;
            if (s.Help != null) item["help"] = s.Help;
            if (s.Min.HasValue) item["min"] = s.Min.Value;
            if (s.Max.HasValue) item["max"] = s.Max.Value;
            if (s.Step.HasValue) item["step"] = s.Step.Value;
            if (s.Unit != null) item["unit"] = s.Unit;
            if (s.Factor.HasValue) item["factor"] = s.Factor.Value;
            if (s.UnlimitedAtZero) item["unlimitedAtZero"] = true;
            if (s.Hidden) item["hidden"] = true;
            if (s.Advanced) item["advanced"] = true;
            if (s.Requires != null) item["requires"] = s.Requires;
            if (s.Options != null) item["options"] = new JArray(s.Options);
            if (s.Placeholders != null) item["placeholders"] = new JArray(s.Placeholders);
            if (s.Aliases != null) item["aliases"] = new JArray(s.Aliases);
            if (s.AliasFactors != null) item["aliasFactors"] = JObject.FromObject(s.AliasFactors);
            settings.Add(item);
        }
        return new JObject { ["host"] = host, ["version"] = version, ["settingsFile"] = settingsFile, ["settings"] = settings }.ToString(Formatting.None);
    }

    public static Setting Find(string key) => Table.FirstOrDefault(s => s.Key == key);
}

/// <summary>The settings file as the game reads it: every key through its default, old key names tolerated.</summary>
public sealed class UiSettings
{
    private readonly JObject _root;

    private UiSettings(JObject root) { _root = root; }

    /// <summary>Never throws: an unreadable file gives the defaults, and the game keeps running.</summary>
    public static UiSettings Load(string path)
    {
        try
        {
            if (!File.Exists(path)) return new UiSettings(new JObject());
            using (var reader = new JsonTextReader(new StringReader(ReadWithRetry(path))) { DateParseHandling = DateParseHandling.None })
                return new UiSettings(JObject.Load(reader));
        }
        catch (Exception)
        {
            return new UiSettings(new JObject());
        }
    }

    private static string ReadWithRetry(string path)
    {
        for (int attempt = 1; ; attempt++)
        {
            try { return File.ReadAllText(path); }
            catch (IOException) when (attempt < 5) { System.Threading.Thread.Sleep(40); }
        }
    }

    public JToken Token(string key)
    {
        var token = _root[key];
        if (token != null && token.Type != JTokenType.Null) return token;
        var setting = UiSchema.Find(key);
        if (setting?.Aliases != null)
            foreach (var alias in setting.Aliases)
            {
                var old = _root[alias];
                if (old != null && old.Type != JTokenType.Null) return old;
            }
        return setting?.Default == null ? null : JToken.FromObject(setting.Default);
    }

    public bool Bool(string key) => Token(key)?.Type == JTokenType.Boolean && (bool)Token(key);

    public long Long(string key)
    {
        var t = Token(key);
        if (t == null) return 0;
        if (t.Type == JTokenType.Integer || t.Type == JTokenType.Float) return Convert.ToInt64(((JValue)t).Value, CultureInfo.InvariantCulture);
        return long.TryParse(t.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long l) ? l : 0;
    }

    public int Int(string key) { long l = Long(key); return l > int.MaxValue ? int.MaxValue : l < int.MinValue ? int.MinValue : (int)l; }

    public double Double(string key)
    {
        var t = Token(key);
        if (t == null) return 0;
        if (t.Type == JTokenType.Integer || t.Type == JTokenType.Float) return Convert.ToDouble(((JValue)t).Value, CultureInfo.InvariantCulture);
        return double.TryParse(t.ToString().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double d) ? d : 0;
    }

    public string String(string key)
    {
        var t = Token(key);
        return t == null ? "" : t.Type == JTokenType.String ? (string)t : t.ToString();
    }

    /// <summary>A long that stores "no limit" as long.MaxValue when the file says 0.</summary>
    public long Ceiling(string key) { long l = Long(key); return l <= 0 ? long.MaxValue : l; }

    public List<string> Names(string key) => String(key).Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
}
