// stub/HostSchema.cs: the UIFramework host block, the part every game action carries. Not an action of its own:
// it goes into the game's action, which calls UiHost.Handle from its Execute().
// Starting from nothing? Take examples/DemoGame/DemoGame.cs instead, a complete game action with this block inside,
// and follow docs/Getting-Started.md.

// --- UIFramework host block (keep identical in every action of this host that reads the settings) ---
// From UIFramework/stub/HostSchema.cs (tools/update-host-block.ps1 refreshes it). The game declares its settings
// once in UiSchema.Table and its window in UiSchema.Window, reads the settings through UiSettings, and lets
// UiHost.Handle answer the settings window's requests. One table, so the game and its window can never disagree.
// Needs no usings and no reference beyond Newtonsoft.Json, which every action has: paste it as it is, right below
// the action's using lines (anywhere after them works too: at the end, or inside the CPHInline class).
//
// In Execute(), first thing:
//     if (UiHost.Handle(args, CPH, ActionName, "demo", CurrentVersion, "Demo Game/settings.json"))
//         return true;
// With a Try button, a check before saving or work after it (see UiHost.Handle):
//     if (UiHost.Handle(args, CPH, ActionName, "demo", CurrentVersion, SettingsFile,
//             test: (name, s) => name == "tryRoll" ? TryRoll(s) : null, validate: s => Problems(s), saved: s => SetupOverlay(s)))
//         return true;
public static class UiSchema
{
    public sealed class Setting
    {
        public string Key; public object Default; public string Kind, Page, Section, Group, Label, Help, Unit, Requires, Confirm, Platform, Row;
        public double? Min, Max, Factor; public bool UnlimitedAtZero, Hidden;
        public string[] Options, Placeholders, Aliases; public double? AliasFactor;
        // For a list: its columns as "key|kind|label" (kinds: string, text, int, double, bool, emote; a trailing "readonly"
        // makes it a column the game fills and the user only reads; an emote column
        // adds "|twitch", "|youtube" or "|kick" for its picture), and the file next to the settings file that holds
        // the rows, with the property in it (no field: the file is the array).
        public string[] Columns; public string File, Field;
        // Transient: shown, never saved. For an action: Live hands the host the window's current values, Panel shows
        // its answer under the button, Auto asks it on show and after every change without a button. Live on any
        // other field asks the host whenever the field changes (a preset slider whose answer fills in other fields).
        // Row: settings naming the same row, one after the other, share one line of the page.
        public bool Transient, Live, Panel, Auto;
        // TwitchOnly: a switch that only matters on Twitch carries a "Twitch only" pill.
        public bool TwitchOnly;
        // Masked: a text field shown as dots until its eye button reveals it (a token, a client secret).
        public bool Masked;
        // ShowWhen: shown only while the condition holds ("questionSource=igdb", "mode=a|b", "x!=free && useBot").
        // Shows, on a dropdown: per option, the fields it shows, "igdb: igdbClientId, igdbClientSecret".
        public string ShowWhen; public string[] Shows;
        // ReadOnly: shown, not editable (a status the game writes). Step: a slider's step.
        public bool ReadOnly; public double? Step;
    }

    /// <summary>A setting: key and default, then whatever the conventions cannot know. Keys the conventions know
    /// (useBot, globalCooldown, blockedUsers, ...) need nothing but their default. An option is "value" or
    /// "value|label". aliasFactor multiplies the first alias's value on the move (a fraction to percent: 100).
    /// requires is a switch's key, or a condition on any field: "numberOfSlots>=5", "cpBetMode=fixed", "mode!=free"; with
    /// "hide:" in front the field is hidden instead of greyed out while it does not hold. masked shows a text field as dots.
    /// showWhen shows the field only while its condition holds: "questionSource=igdb", several values "mode=a|b", several
    /// conditions "mode!=free && useBot", "!key" for a switch that is off. On a dropdown, shows names per option the fields it
    /// shows: "igdb: clientId, clientSecret". readOnly shows a value the game writes without letting the user edit it; step is
    /// a slider's step; group puts a message template under a heading of its own.</summary>
    public static Setting S(string key, object @default, string page = null, string label = null, string help = null, string kind = null,
        string section = null, double? min = null, double? max = null, string unit = null, double? factor = null, bool unlimitedAtZero = false,
        string[] options = null, string requires = null, string confirm = null, bool hidden = false, string[] aliases = null, double? aliasFactor = null,
        string[] columns = null, string file = null, string field = null, bool transient = false, bool live = false, bool panel = false, bool auto = false,
        string platform = null, bool twitchOnly = false, string row = null, bool masked = false, string showWhen = null, string[] shows = null,
        bool readOnly = false, double? step = null, string group = null)
    {
        return new Setting
        {
            Key = key, Default = @default, Page = page, Label = label, Help = help, Kind = kind, Section = section, Min = min, Max = max,
            Unit = unit, Factor = factor, UnlimitedAtZero = unlimitedAtZero, Options = options, Requires = requires, Confirm = confirm,
            Hidden = hidden, Aliases = aliases, AliasFactor = aliasFactor, Columns = columns, File = file, Field = field,
            Transient = transient, Live = live, Panel = panel, Auto = auto, Platform = platform, TwitchOnly = twitchOnly, Row = row, Masked = masked,
            ShowWhen = showWhen, Shows = shows, ReadOnly = readOnly, Step = step, Group = group
        };
    }

    /// <summary>A message template: group, key, default text, label, help, placeholders without braces, old key; file and
    /// field when the text lives in a file of its own next to the settings file (a messages.json).</summary>
    public static Setting Msg(string group, string key, string @default, string label, string help, string[] placeholders, string alias = null,
        string file = null, string field = null)
        => new Setting { Key = key, Default = @default, Kind = "template", Page = "messages", Group = group, Label = label, Help = help,
                         Placeholders = placeholders, Aliases = alias == null ? null : new[] { alias }, File = file, Field = field };

    public static string ToJson(string host, string version, string settingsFile) => ToJson(host, version, settingsFile, null);

    /// <summary>The schema with the points system the window should show as detected (UiCurrency.Detect).</summary>
    public static string ToJson(string host, string version, string settingsFile, UiCurrency currency)
    {
        var settings = new Newtonsoft.Json.Linq.JArray();
        foreach (var s in Table)
            settings.Add(ToJson(s));
        var root = new Newtonsoft.Json.Linq.JObject { ["host"] = host, ["version"] = version, ["settingsFile"] = settingsFile, ["settings"] = settings };
        if (Window != null) root["window"] = Window.ToJson();
        if (Window != null && Window.LangMap.Count > 0) root["lang"] = Window.LangMap;
        if (currency != null && currency.Detected) root["currency"] = currency.ToJson();
        return root.ToString(Newtonsoft.Json.Formatting.None);
    }

    /// <summary>One setting as the window reads it.</summary>
    public static Newtonsoft.Json.Linq.JObject ToJson(Setting s)
    {
        var item = new Newtonsoft.Json.Linq.JObject { ["key"] = s.Key, ["default"] = s.Default == null ? Newtonsoft.Json.Linq.JValue.CreateNull() : Newtonsoft.Json.Linq.JToken.FromObject(s.Default) };
        if (s.Kind != null) item["kind"] = s.Kind;
        if (s.Page != null) item["page"] = s.Page;
        if (s.Section != null) item["section"] = s.Section;
        if (s.Group != null) item["group"] = s.Group;
        if (s.Label != null) item["label"] = s.Label;
        if (s.Help != null) item["help"] = s.Help;
        if (s.Min.HasValue) item["min"] = s.Min.Value;
        if (s.Max.HasValue) item["max"] = s.Max.Value;
        if (s.Unit != null) item["unit"] = s.Unit;
        if (s.Platform != null) item["platform"] = s.Platform;
        if (s.Factor.HasValue) item["factor"] = s.Factor.Value;
        if (s.UnlimitedAtZero) item["unlimitedAtZero"] = true;
        if (s.Hidden) item["hidden"] = true;
        if (s.Requires != null) item["requires"] = s.Requires;
        if (s.Confirm != null) item["confirm"] = s.Confirm;
        if (s.Options != null)
        {
            var options = new Newtonsoft.Json.Linq.JArray();
            foreach (var option in s.Options)
            {
                int bar = option.IndexOf('|');
                options.Add(bar < 0 ? (Newtonsoft.Json.Linq.JToken)option : new Newtonsoft.Json.Linq.JObject { ["value"] = option.Substring(0, bar), ["label"] = option.Substring(bar + 1) });
            }
            item["options"] = options;
        }
        if (s.Placeholders != null) item["placeholders"] = new Newtonsoft.Json.Linq.JArray(s.Placeholders);
        if (s.Columns != null)
        {
            var columns = new Newtonsoft.Json.Linq.JArray();
            foreach (var column in s.Columns)
            {
                var parts = new System.Collections.Generic.List<string>(column.Split('|'));
                var cell = new Newtonsoft.Json.Linq.JObject { ["key"] = parts[0], ["kind"] = parts.Count > 1 ? parts[1] : "string" };
                // Flags at the end: readonly (the game fills it, the user only reads), an emote column's platform.
                // What stands between kind and flags is the label, which may itself contain a | ("a | b | c").
                // A flag right after the kind ("key|int|readonly") counts as one too; the label is then the key.
                while (parts.Count > 3 || (parts.Count == 3 && (parts[2] == "readonly" || parts[2] == "twitch" || parts[2] == "youtube" || parts[2] == "kick")))
                {
                    string last = parts[parts.Count - 1];
                    if (last == "readonly") cell["readOnly"] = true;
                    else if (last == "twitch" || last == "youtube" || last == "kick") cell["platform"] = last;
                    else break;
                    parts.RemoveAt(parts.Count - 1);
                }
                cell["label"] = parts.Count > 2 ? string.Join("|", parts.GetRange(2, parts.Count - 2).ToArray()) : parts[0];
                columns.Add(cell);
            }
            item["columns"] = columns;
        }
        if (s.File != null) item["file"] = s.File;
        if (s.Transient) item["transient"] = true;
        if (s.Live) item["live"] = true;
        if (s.Panel) item["panel"] = true;
        if (s.Auto) item["auto"] = true;
        if (s.TwitchOnly) item["twitchOnly"] = true;
        if (s.Masked) item["masked"] = true;
        if (s.ReadOnly) item["readOnly"] = true;
        if (s.Step.HasValue) item["step"] = s.Step.Value;
        if (s.ShowWhen != null) item["showWhen"] = s.ShowWhen;
        if (s.Shows != null)
        {
            var shows = new Newtonsoft.Json.Linq.JObject();
            foreach (var entry in s.Shows)
            {
                int colon = entry.IndexOf(':');
                if (colon <= 0) continue;
                var keys = shows[entry.Substring(0, colon).Trim()] as Newtonsoft.Json.Linq.JArray;
                if (keys == null) shows[entry.Substring(0, colon).Trim()] = keys = new Newtonsoft.Json.Linq.JArray();
                foreach (var key in entry.Substring(colon + 1).Split(','))
                    if (key.Trim().Length > 0) keys.Add(key.Trim());
            }
            item["shows"] = shows;
        }
        if (s.Row != null) item["row"] = s.Row;
        if (s.Field != null) item["field"] = s.Field;
        if (s.Aliases != null) item["aliases"] = new Newtonsoft.Json.Linq.JArray(s.Aliases);
        if (s.AliasFactor.HasValue && s.Aliases != null && s.Aliases.Length > 0) item["aliasFactors"] = new Newtonsoft.Json.Linq.JObject { [s.Aliases[0]] = s.AliasFactor.Value };
        return item;
    }

    // ---- Shorthands for the window: a page, a help section and its lines, a cooldown. ----
    public static UiPage P(string id, string title = null, string icon = null, params string[] fields) => new UiPage(id, title, icon, fields);
    public static UiSection H(string heading, params object[] lines) => new UiSection(heading, lines);
    public static UiLine Cmd(string command, string what) => new UiLine(new Newtonsoft.Json.Linq.JObject { ["command"] = command, ["what"] = what });
    public static UiLine Note(string note) => new UiLine(new Newtonsoft.Json.Linq.JObject { ["note"] = note });
    public static UiLine Link(string text, string url) => new UiLine(new Newtonsoft.Json.Linq.JObject { ["link"] = text, ["url"] = url });
    /// <summary>A command's cooldowns, set after every save: the command as its text ("!roll"), its name or its id in
    /// Streamer.bot, the setting of its global cooldown and the setting of its per-user cooldown (either may be null).</summary>
    public static string[] C(string command, string globalKey, string userKey) => new[] { command, globalKey, userKey };
    /// <summary>One trigger for UiWindow.Triggers: its name in Streamer.bot, its code, what it is for, and the
    /// arguments that are its own ("name" or "name=sample value").</summary>
    public static string[] Tr(string name, string code, string what = null, params string[] args)
    {
        var parts = new System.Collections.Generic.List<string> { name, code, what };
        if (args != null) parts.AddRange(args);
        return parts.ToArray();
    }
    /// <summary>A stop of the window's tour (UiWindow.Tour): what it points at, a heading and a text. The target is a
    /// setting's key, "page:id" for a page in the rail, a part of the window ("start", "end", "rail", "save",
    /// "saveExit", "cancel", "status", "reset", "resetPage", "theme", "customise", "tour", "search"), or null for a note in the
    /// middle. Left out, title and text are the framework's own for a part or a page, a setting's label and help.
    /// act: the user may use the ringed part while the stop is up (press the Try button, type into the field).</summary>
    public static UiLine Tip(string target, string title = null, string text = null, bool act = false)
    {
        var tip = new Newtonsoft.Json.Linq.JObject();
        if (target != null) tip["target"] = target;
        if (title != null) tip["title"] = title;
        if (text != null) tip["text"] = text;
        if (act) tip["act"] = true;
        return new UiLine(tip);
    }

    // ---- Shorthands for UiWindow.Lang: a key's texts, a page title, a declared text. ----
    public static UiText K(string key, string label, string help = null) => new UiText("keys", key, help == null ? (Newtonsoft.Json.Linq.JToken)label : new Newtonsoft.Json.Linq.JObject { ["label"] = label, ["help"] = help });
    public static UiText Pg(string id, string title) => new UiText("pages", id, title);
    public static UiText Tx(string declared, string translation) => new UiText("texts", declared, translation);
    /// <summary>One of the window's own texts by its key in conventions/lang/en.json's ui section ("btn.save"), for a
    /// language the framework has no file for yet.</summary>
    public static UiText Ui(string key, string text) => new UiText("ui", key, text);
    /// <summary>The help of a placeholder chip ({user}, {amount} …) in the game's language.</summary>
    public static UiText Ph(string placeholder, string help) => new UiText("placeholders", placeholder, help);

    /// <summary>The global the stub reads the schema from: the action's name, with the characters Streamer.bot's
    /// store refuses (brackets, a trailing space) replaced. Must match the stub's SchemaVar.</summary>
    public static string Var(string actionName) => "uiframework.schema." + Clean(actionName);

    /// <summary>An action's name as a global's name: letters, digits, space, dot and dash stay, the rest is "_".
    /// No Regex: a game action may ship without a reference to System.dll.</summary>
    public static string Clean(string actionName)
    {
        var chars = (actionName ?? "").Trim().ToCharArray();
        for (int i = 0; i < chars.Length; i++)
            if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '_' && chars[i] != ' ' && chars[i] != '.' && chars[i] != '-') chars[i] = '_';
        return new string(chars);
    }

    public static Setting Find(string key)
    {
        foreach (var s in Table)
            if (s.Key == key) return s;
        return null;
    }

    // ---- The window: title, pages, help, about, log. Null: a window named after the host, its pages the settings name. ----
    public static readonly UiWindow Window = null; // __WINDOW__

    // ---- The table: every setting the host reads, in page order. ----
    public static readonly Setting[] Table =
    {
        // __TABLE__
    };
}

/// <summary>How the window is called and laid out, declared beside the table: what the framework's docs call the
/// spec (docs/Schema.md, "The window"). Every part is optional; a page a setting names but the window does not is
/// made up on the way; how to play, log and about sit at the foot of the page list.</summary>
public sealed class UiWindow
{
    public string Title, Releases, Logo, LogPrefix, Language;
    public bool Locked;
    public double? Width, Height;
    public readonly System.Collections.Generic.List<object> PageList = new System.Collections.Generic.List<object>();
    public readonly Newtonsoft.Json.Linq.JObject SampleMap = new Newtonsoft.Json.Linq.JObject(), AnswerMap = new Newtonsoft.Json.Linq.JObject(), OverrideMap = new Newtonsoft.Json.Linq.JObject();
    public Newtonsoft.Json.Linq.JObject SenderMap, LogMap, AboutMap;
    /// <summary>What Updates(...) declared; null: the framework's defaults.</summary>
    public Newtonsoft.Json.Linq.JObject UpdateOptions;
    public readonly Newtonsoft.Json.Linq.JArray PillList = new Newtonsoft.Json.Linq.JArray(), CooldownList = new Newtonsoft.Json.Linq.JArray(), HelpList = new Newtonsoft.Json.Linq.JArray(), SectionList = new Newtonsoft.Json.Linq.JArray();
    public readonly Newtonsoft.Json.Linq.JObject LangMap = new Newtonsoft.Json.Linq.JObject();
    public readonly Newtonsoft.Json.Linq.JArray TourList = new Newtonsoft.Json.Linq.JArray();
    public readonly Newtonsoft.Json.Linq.JArray TriggerList = new Newtonsoft.Json.Linq.JArray(), MethodList = new Newtonsoft.Json.Linq.JArray();

    /// <summary>A custom trigger of the game, declared once: UiTriggers.Register registers every one in Init, the Help
    /// page lists it with its arguments, and its Fire button there fires it with the sample values, so a streamer
    /// can try an action of their own without playing a round. <paramref name="name"/> is what Streamer.bot shows
    /// ("Trivia: Round Won"), <paramref name="code"/> what CPH.TriggerCodeEvent takes ("trivia_onWin"),
    /// <paramref name="args"/> the arguments it carries, each "name" or "name=sample value".</summary>
    public UiWindow Trigger(string name, string code, string what = null, params string[] args)
    {
        TriggerList.Add(Declared(name, code, what, args));
        return this;
    }

    /// <summary>Several triggers that carry the same arguments (every way a round can end): <paramref name="shared"/>
    /// once, then each trigger as UiSchema.Tr(name, code, what, own arguments). An argument a trigger names itself
    /// replaces the shared one of that name (its own sample outcome), or is added after them.</summary>
    public UiWindow Triggers(string[] shared, params string[][] triggers)
    {
        foreach (var t in triggers)
        {
            if (t == null || t.Length < 2) continue;
            var args = new System.Collections.Generic.List<string>(shared ?? new string[0]);
            for (int i = 3; i < t.Length; i++)
            {
                string name = ArgName(t[i]);
                int at = args.FindIndex(a => ArgName(a) == name);
                if (at >= 0) args[at] = t[i]; else args.Add(t[i]);
            }
            TriggerList.Add(Declared(t[0], t[1], t.Length > 2 ? t[2] : null, args.ToArray()));
        }
        return this;
    }

    private static string ArgName(string arg) { int eq = (arg ?? "").IndexOf('='); return (eq < 0 ? arg ?? "" : arg.Substring(0, eq)).Trim(); }

    /// <summary>A method of the game action for an Execute C# Method sub-action in the streamer's own actions
    /// ("StartRound"): the Help page lists it with <paramref name="what"/> it does and the arguments it reads and
    /// sets, each "name" or "name=what it is".</summary>
    public UiWindow Method(string name, string what = null, params string[] args)
    {
        MethodList.Add(Declared(name, null, what, args));
        return this;
    }

    private static Newtonsoft.Json.Linq.JObject Declared(string name, string code, string what, string[] args)
    {
        var entry = new Newtonsoft.Json.Linq.JObject { ["name"] = name };
        if (code != null) entry["code"] = code;
        if (what != null) entry["what"] = what;
        if (args != null && args.Length > 0)
        {
            var list = new Newtonsoft.Json.Linq.JArray();
            foreach (var arg in args)
            {
                if (string.IsNullOrWhiteSpace(arg)) continue;
                int eq = arg.IndexOf('=');
                var item = new Newtonsoft.Json.Linq.JObject { ["name"] = (eq < 0 ? arg : arg.Substring(0, eq)).Trim() };
                if (eq >= 0) item["text"] = arg.Substring(eq + 1).Trim();
                list.Add(item);
            }
            entry["args"] = list;
        }
        return entry;
    }

    /// <param name="title">Window title and About heading.</param>
    /// <param name="releases">"owner/repo" on GitHub: the repository and releases links, the update check.</param>
    /// <param name="logo">URL of the logo, for the window icon and About.</param>
    public UiWindow(string title, string releases = null, string logo = null, string logPrefix = null, double? width = null, double? height = null)
    {
        Title = title; Releases = releases; Logo = logo; LogPrefix = logPrefix; Width = width; Height = height;
    }

    /// <summary>The game's own texts in another language, laid over the conventions' when the window is in it:
    /// UiSchema.K (a key's label and help), Pg (a page's title by id), Tx (a declared text by its English wording:
    /// a section, a pill text, a page title, an option label, a message group), Ui (one of the window's own texts by
    /// its key) and Ph (a placeholder's help). Several calls add up.</summary>
    public UiWindow Lang(string code, params UiText[] texts)
    {
        var lang = LangMap[code] as Newtonsoft.Json.Linq.JObject;
        if (lang == null) LangMap[code] = lang = new Newtonsoft.Json.Linq.JObject();
        foreach (var t in texts)
        {
            var section = lang[t.Section] as Newtonsoft.Json.Linq.JObject;
            if (section == null) lang[t.Section] = section = new Newtonsoft.Json.Linq.JObject();
            section[t.Name] = t.Value;
        }
        return this;
    }

    /// <summary>Locks the layout: the window has no customise mode, and a user's renames, moves, hidden fields and
    /// accent colour are not applied. Theme and language stay the user's to pick.</summary>
    public UiWindow Lock(bool locked = true) { Locked = locked; return this; }

    /// <summary>The pages in order: a UiPage (UiSchema.P) or the id of a standard page ("messages", "log", "help", "about").</summary>
    public UiWindow Pages(params object[] pages) { PageList.AddRange(pages); return this; }

    /// <summary>The Log page: the folder (relative to the Streamer.bot folder) and the files in it, "BJG-log*.txt";
    /// the bool setting behind "Write a log file per day"; the columns after Time and Result as "Header|field" or a
    /// field; the values of the Result filter.</summary>
    public UiWindow Log(string folder, string files, string writeLogKey = null, string writeLogHelp = null, string[] columns = null, string[] results = null,
        string[] platforms = null, string emptyText = null, string writeLogLabel = null)
    {
        LogMap = new Newtonsoft.Json.Linq.JObject { ["folder"] = folder, ["files"] = files };
        if (writeLogKey != null) LogMap["writeLogKey"] = writeLogKey;
        if (writeLogLabel != null) LogMap["writeLogLabel"] = writeLogLabel;
        if (writeLogHelp != null) LogMap["writeLogHelp"] = writeLogHelp;
        if (emptyText != null) LogMap["emptyText"] = emptyText;
        if (columns != null)
        {
            var list = new Newtonsoft.Json.Linq.JArray();
            foreach (var column in columns)
            {
                int bar = column.IndexOf('|');
                list.Add(bar < 0 ? (Newtonsoft.Json.Linq.JToken)column : new Newtonsoft.Json.Linq.JObject { ["header"] = column.Substring(0, bar), ["field"] = column.Substring(bar + 1) });
            }
            LogMap["columns"] = list;
        }
        if (results != null) LogMap["results"] = new Newtonsoft.Json.Linq.JArray(results);
        if (platforms != null) LogMap["platforms"] = new Newtonsoft.Json.Linq.JArray(platforms);
        return this;
    }

    /// <summary>Commands whose cooldowns follow settings (UiSchema.C): the settings action sets them after every save.</summary>
    public UiWindow Cooldowns(params string[][] cooldowns)
    {
        foreach (var c in cooldowns)
            CooldownList.Add(new Newtonsoft.Json.Linq.JObject { ["command"] = c[0], ["global"] = c.Length > 1 ? c[1] : null, ["user"] = c.Length > 2 ? c[2] : null });
        return this;
    }

    /// <summary>A section (by the name its settings declare) shown only while a condition holds, heading and fields
    /// together: "questionSource=igdb", "mode=a|b", "mode!=free && useBot".</summary>
    public UiWindow Section(string name, string showWhen)
    {
        SectionList.Add(new Newtonsoft.Json.Linq.JObject { ["name"] = name, ["showWhen"] = showWhen });
        return this;
    }

    /// <summary>A pill in the window's header that follows a bool setting.</summary>
    public UiWindow Pill(string key, string on, string off = null, string tooltip = null)
    {
        var pill = new Newtonsoft.Json.Linq.JObject { ["key"] = key, ["on"] = on };
        if (off != null) pill["off"] = off;
        if (tooltip != null) pill["tooltip"] = tooltip;
        PillList.Add(pill);
        return this;
    }

    /// <summary>The keys of the sender switches on the Messages page, where they differ from the conventions'
    /// (useBot, sendAction, cooldownUseBot, cooldownSendAction, numberSeparator). Null hides one.</summary>
    public UiWindow Senders(string useBot = "", string sendAction = "", string cooldownUseBot = "", string cooldownSendAction = "", string numberSeparator = "", string cooldownGroup = "")
    {
        SenderMap = SenderMap ?? new Newtonsoft.Json.Linq.JObject();
        if (useBot != "") SenderMap["useBot"] = useBot;
        if (sendAction != "") SenderMap["sendAction"] = sendAction;
        if (cooldownUseBot != "") SenderMap["cooldownUseBot"] = cooldownUseBot;
        if (cooldownSendAction != "") SenderMap["cooldownSendAction"] = cooldownSendAction;
        if (numberSeparator != "") SenderMap["numberSeparator"] = numberSeparator;
        if (cooldownGroup != "") SenderMap["cooldownGroup"] = cooldownGroup;
        return this;
    }

    /// <summary>Sample values for the chat preview's placeholders, as pairs: "user", "alice", "bet", "1,500".</summary>
    public UiWindow Samples(params string[] pairs)
    {
        for (int i = 0; i + 1 < pairs.Length; i += 2) SampleMap[pairs[i]] = pairs[i + 1];
        return this;
    }

    /// <summary>What an action would answer, for the framework's preview and screenshots: text, or a JSON object.</summary>
    public UiWindow Answer(string action, string answer)
    {
        Newtonsoft.Json.Linq.JToken value;
        try { value = answer != null && answer.TrimStart().StartsWith("{") ? Newtonsoft.Json.Linq.JObject.Parse(answer) : (Newtonsoft.Json.Linq.JToken)answer; }
        catch (System.Exception) { value = answer; }
        AnswerMap[action] = value;
        return this;
    }

    /// <summary>What the window changes about one setting of the table, where the table itself should not say it.</summary>
    public UiWindow Override(string key, string page = null, string label = null, string help = null, string section = null, bool? hidden = null, int? order = null)
    {
        var o = new Newtonsoft.Json.Linq.JObject();
        if (page != null) o["page"] = page;
        if (label != null) o["label"] = label;
        if (help != null) o["help"] = help;
        if (section != null) o["section"] = section;
        if (hidden.HasValue) o["hidden"] = hidden.Value;
        if (order.HasValue) o["order"] = order.Value;
        OverrideMap[key] = o;
        return this;
    }

    /// <summary>A tour of the window, stop by stop (UiSchema.Tip): it starts by itself on the window's first open and
    /// again from the ? in the header. Its texts translate through Lang's Tx like every declared text.</summary>
    /// <summary>How the update notice once per Streamer.bot start tells of this game (docs/Schema.md, "Update notice"):
    /// <paramref name="chat"/> a line in every connected chat, with <paramref name="chatText"/> (null: the framework's,
    /// in the window's language); <paramref name="toast"/> a Windows toast, <paramref name="toastAlways"/> or only while no
    /// broadcaster account is connected, with <paramref name="toastTitle"/> and <paramref name="toastText"/>. In the texts
    /// {updates} is the list of what is new ("Hangman Game v13.2.0, UIFramework v1.4.0"), {count} how many.</summary>
    public UiWindow Updates(bool chat = true, string chatText = null, bool toast = true, bool toastAlways = false, string toastTitle = null, string toastText = null)
    {
        UpdateOptions = new Newtonsoft.Json.Linq.JObject { ["chat"] = chat, ["toast"] = toast, ["toastAlways"] = toastAlways };
        if (chatText != null) UpdateOptions["chatText"] = chatText;
        if (toastTitle != null) UpdateOptions["toastTitle"] = toastTitle;
        if (toastText != null) UpdateOptions["toastText"] = toastText;
        return this;
    }

    public UiWindow Tour(params UiLine[] tips)
    {
        foreach (var tip in tips) TourList.Add(tip.Json);
        return this;
    }

    /// <summary>The Help page's sections (UiSchema.H).</summary>
    public UiWindow Help(params UiSection[] sections)
    {
        foreach (var section in sections) HelpList.Add(section.ToJson());
        return this;
    }

    /// <summary>The About page: the game's credits under its name, sections like help, the game's own links. The page
    /// ends with the framework's links (its page, Streamer.bot, where to report a problem); defaultLinks: false leaves
    /// them out, streamerBotLink: false only that one. authorLink is kept for older games and no longer does anything.</summary>
    public UiWindow About(string credits = null, UiSection[] sections = null, UiLine[] links = null, bool streamerBotLink = true, bool authorLink = true, bool defaultLinks = true)
    {
        AboutMap = new Newtonsoft.Json.Linq.JObject();
        if (credits != null) AboutMap["credits"] = credits;
        if (sections != null) AboutMap["sections"] = new Newtonsoft.Json.Linq.JArray(System.Linq.Enumerable.Select(sections, s => s.ToJson()));
        if (links != null) AboutMap["links"] = new Newtonsoft.Json.Linq.JArray(System.Linq.Enumerable.Select(links, l => new Newtonsoft.Json.Linq.JObject { ["text"] = l.Json["link"], ["url"] = l.Json["url"] }));
        if (!streamerBotLink) AboutMap["streamerBotLink"] = false;
        if (!defaultLinks) AboutMap["defaultLinks"] = false;
        return this;
    }

    public Newtonsoft.Json.Linq.JObject ToJson()
    {
        var root = new Newtonsoft.Json.Linq.JObject { ["title"] = Title };
        if (Releases != null) root["releases"] = Releases;
        if (Logo != null) root["logo"] = Logo;
        if (LogPrefix != null) root["logPrefix"] = LogPrefix;
        if (Language != null) root["language"] = Language;
        if (Locked) root["locked"] = true;
        if (Width.HasValue) root["width"] = Width.Value;
        if (Height.HasValue) root["height"] = Height.Value;
        if (PageList.Count > 0) root["pages"] = new Newtonsoft.Json.Linq.JArray(System.Linq.Enumerable.Select(PageList, p => p is UiPage page ? page.ToJson() : (Newtonsoft.Json.Linq.JToken)p.ToString()));
        if (OverrideMap.Count > 0) root["overrides"] = OverrideMap;
        if (SenderMap != null) root["senders"] = SenderMap;
        if (SampleMap.Count > 0) root["samples"] = SampleMap;
        if (AnswerMap.Count > 0) root["answers"] = AnswerMap;
        if (PillList.Count > 0) root["headerPills"] = PillList;
        if (SectionList.Count > 0) root["sections"] = SectionList;
        if (CooldownList.Count > 0) root["cooldowns"] = CooldownList;
        if (UpdateOptions != null) root["updates"] = UpdateOptions;
        if (LogMap != null) root["log"] = LogMap;
        if (HelpList.Count > 0) root["help"] = HelpList;
        if (AboutMap != null) root["about"] = AboutMap;
        if (TourList.Count > 0) root["tour"] = TourList;
        if (TriggerList.Count > 0) root["triggers"] = TriggerList;
        if (MethodList.Count > 0) root["methods"] = MethodList;
        return root;
    }
}

/// <summary>A page of the window: its id, title and icon (an emoji, or a conventions name like "gear"), and the
/// settings it claims ahead of their own page (keys, or patterns like "overlay*").</summary>
public sealed class UiPage
{
    public string Id, Title, Icon; public string[] Fields; public bool Custom, Bottom;
    public UiPage(string id, string title, string icon, string[] fields) { Id = id; Title = title; Icon = icon; Fields = fields; }
    public Newtonsoft.Json.Linq.JObject ToJson()
    {
        var page = new Newtonsoft.Json.Linq.JObject { ["id"] = Id };
        if (Title != null) page["title"] = Title;
        if (Icon != null) page["icon"] = Icon;
        if (Fields != null && Fields.Length > 0) page["fields"] = new Newtonsoft.Json.Linq.JArray(Fields);
        if (Custom) page["custom"] = true;
        if (Bottom) page["bottom"] = true;
        return page;
    }
}

/// <summary>A section of the Help or About page: a heading and lines, each a note (a string), UiSchema.Cmd or UiSchema.Link.</summary>
public sealed class UiSection
{
    public string Heading; public object[] Lines;
    public UiSection(string heading, object[] lines) { Heading = heading; Lines = lines; }
    public Newtonsoft.Json.Linq.JObject ToJson() => new Newtonsoft.Json.Linq.JObject { ["heading"] = Heading, ["lines"] = new Newtonsoft.Json.Linq.JArray(System.Linq.Enumerable.Select(Lines, l => l is UiLine line ? line.Json : (Newtonsoft.Json.Linq.JToken)l.ToString())) };
}

/// <summary>One translated text for UiWindow.Lang: the section of the language file it goes into, its name there, its value.</summary>
public sealed class UiText
{
    public readonly string Section, Name; public readonly Newtonsoft.Json.Linq.JToken Value;
    public UiText(string section, string name, Newtonsoft.Json.Linq.JToken value) { Section = section; Name = name; Value = value; }
}

public sealed class UiLine
{
    public readonly Newtonsoft.Json.Linq.JObject Json;
    public UiLine(Newtonsoft.Json.Linq.JObject json) { Json = json; }
}

/// <summary>Answers the settings window's requests, which its generic settings action sends through this game's
/// arguments: "describe" (the table and the window), "test" (a Try button or a live field, with the window's current
/// values), "validate" (before a save; answer the problems, one per line, or nothing), "saved" (after a save, with the
/// saved values), "table" (the rows of a table field) and "emotes" (the emote sources, as JSON). The answers go back
/// through a non-persistent global; the describe answer through UiSchema.Var, the rest through UiHost.AnswerVar.
/// Every request must leave the game as it was: no chat output, no state written.</summary>
public static class UiHost
{
    public static string AnswerVar(string actionName) => "uiframework.answer." + UiSchema.Clean(actionName);

    /// <param name="args">The action's arguments.</param>
    /// <param name="CPH">The action's CPH.</param>
    /// <param name="actionName">This action's name, as the settings action knows it.</param>
    /// <param name="host">A short name of the game for the framework's files, "blackjack".</param>
    /// <param name="version">The game's version, "v17.0.0".</param>
    /// <param name="settingsFile">The settings file, relative to the Streamer.bot folder.</param>
    /// <param name="test">Answers a Try button or a live field by its key with the window's current values: a line for the footer, or JSON (docs/Schema.md, kind action). Null for a key: "no answer".</param>
    /// <param name="validate">Looks at the values about to be saved; answers the problems, one per line, or null.</param>
    /// <param name="saved">Runs after a save with the saved values: an overlay to write, a timer to restart. The cooldowns the window declares are set by the settings action itself.</param>
    /// <param name="table">The rows of a table field by its key, as a JSON array of objects.</param>
    /// <param name="emotes">The emote sources for emote fields, as JSON (docs/Schema.md).</param>
    /// <returns>True when the action was called by the settings window and has answered; return true at once then.</returns>
    public static bool Handle(System.Collections.Generic.IDictionary<string, object> args, IInlineInvokeProxy CPH, string actionName, string host, string version, string settingsFile,
        System.Func<string, UiSettings, string> test = null, System.Func<UiSettings, string> validate = null, System.Action<UiSettings> saved = null,
        System.Func<string, string> table = null, System.Func<string> emotes = null)
    {
        string request = Arg(args, "uiframework.request");
        if (request == null && args != null && args.ContainsKey("uiframework.describe")) request = "describe";
        if (string.IsNullOrEmpty(request))
            return false;
        // The settings action names the global it will read; without that (an older stub) the name is built here.
        string var = Arg(args, "uiframework.var");
        if (request == "describe")
        {
            var currency = UiCurrency.Detect(CPH, null);   // only a detected system goes into the answer; the window shows it
            CPH.SetGlobalVar(string.IsNullOrEmpty(var) ? UiSchema.Var(actionName) : var, UiSchema.ToJson(host, version, settingsFile, currency), false);
            CPH.LogInfo("[" + actionName + "] Settings described for the window" + (currency.Detected ? " (" + currency.Source + ": '" + currency.Variable + "')." : "."));
            return true;
        }
        string name = Arg(args, "uiframework.name") ?? "";
        var values = UiSettings.Parse(Arg(args, "uiframework.values"));
        // Read, so cleared: an action the game's handler runs (CPH.RunAction) inherits this action's arguments, and one
        // with a host block of its own would otherwise take the run for a window request and do nothing else.
        foreach (var key in new[] { "uiframework.request", "uiframework.describe", "uiframework.name", "uiframework.values", "uiframework.var" })
            if (args != null && args.ContainsKey(key)) CPH.SetArgument(key, "");
        string answer = null;
        try
        {
            switch (request)
            {
                // The Fire button of a declared trigger on the Help page; everything else is the game's to answer.
                case "test": answer = name.StartsWith(UiTriggers.FireRequest) ? UiTriggers.Fire(CPH, name.Substring(UiTriggers.FireRequest.Length)) : test?.Invoke(name, values); break;
                case "validate": answer = validate?.Invoke(values); break;
                case "saved": saved?.Invoke(values); break;
                case "table": answer = table?.Invoke(name); break;
                case "emotes": answer = emotes?.Invoke(); break;
                default: CPH.LogWarn("[" + actionName + "] The settings window asked for '" + request + "', which this game does not know."); break;
            }
        }
        catch (System.Exception ex)
        {
            CPH.LogError("[" + actionName + "] The settings window's request '" + request + "' failed: " + ex.Message);
            answer = request == "saved" ? null : "The game could not answer: " + ex.Message;
        }
        CPH.SetGlobalVar(string.IsNullOrEmpty(var) ? AnswerVar(actionName) : var, answer ?? "", false);
        return true;
    }

    private static string Arg(System.Collections.Generic.IDictionary<string, object> args, string key) => args != null && args.TryGetValue(key, out var value) ? value as string : null;
}

/// <summary>The update notice, once per Streamer.bot start (docs/Schema.md, "Update notice"). Call
/// <c>UiUpdateNotice.Startup(CPH, ...)</c> from the game action's <c>Init()</c> and tick "Precompile on Application Start"
/// on its Execute Code sub-action: Streamer.bot then runs it as it starts. In the background it asks UIFramework.dll (the
/// newest under &lt;Streamer.bot&gt;\UIFramework\lib, or the one the settings loaded) every few seconds until the notice
/// is due, waits up to a minute for a broadcaster account to connect, then sends the chat line to Twitch, YouTube and
/// Kick, whichever is connected, and shows a Windows toast when no broadcaster account is (or always, as the window's
/// <c>Updates</c> says). Every game asks; one of them gets the whole notice. Without the DLL (the settings never opened)
/// nothing happens.</summary>
public static class UiUpdateNotice
{
    private static readonly object Gate = new object();
    private static bool _started;
    // How often (five seconds apart) it looks for a connected broadcaster account before it decides there is none.
    private static int _connectTries = 12;

    public static void Startup(IInlineInvokeProxy CPH, string actionName, string host, string version, string settingsFile)
    {
        lock (Gate)
        {
            if (_started) return;
            _started = true;
        }
        System.Threading.Tasks.Task.Run(() => Deliver(CPH, actionName, host, version, settingsFile));
    }

    private static void Deliver(IInlineInvokeProxy CPH, string actionName, string host, string version, string settingsFile)
    {
        try
        {
            var notices = FrameworkType();
            var due = notices == null ? null : notices.GetMethod("DueJson", new[] { typeof(string), typeof(string), typeof(string), typeof(string), typeof(string), typeof(string) });
            if (due == null) return;
            var window = UiSchema.Window;
            string title = window != null && window.Title != null ? window.Title : actionName;
            string options = window != null && window.UpdateOptions != null ? window.UpdateOptions.ToString(Newtonsoft.Json.Formatting.None) : null;
            string answer = "";
            for (int i = 0; i < 120 && answer == ""; i++)   // every five seconds, ten minutes at most
            {
                if (i > 0) System.Threading.Thread.Sleep(5000);
                answer = due.Invoke(null, new object[] { host, title, version, window != null ? window.Releases : null, settingsFile, options }) as string;
                if (answer == null) return;
            }
            if (string.IsNullOrEmpty(answer)) return;
            var notice = Newtonsoft.Json.Linq.JObject.Parse(answer);
            foreach (var line in notice["log"] as Newtonsoft.Json.Linq.JArray ?? new Newtonsoft.Json.Linq.JArray())
                CPH.LogInfo("[" + actionName + "] Update: " + (string)line);

            // Streamer.bot connects its accounts while it starts: up to a minute for the first one.
            bool twitch = false, youtube = false, kick = false;
            for (int i = 0; i < _connectTries; i++)
            {
                try { twitch = CPH.TwitchGetBroadcaster() != null; } catch (System.Exception) { twitch = false; }
                try { youtube = CPH.YouTubeGetBroadcaster() != null; } catch (System.Exception) { youtube = false; }
                try { kick = CPH.KickGetBroadcaster() != null; } catch (System.Exception) { kick = false; }
                if (twitch || youtube || kick || i + 1 >= _connectTries) break;
                System.Threading.Thread.Sleep(5000);
            }
            bool connected = twitch || youtube || kick;
            var settings = UiSettings.Load(settingsFile);
            bool bot = settings.Token("useBot") == null || settings.Bool("useBot");
            foreach (var line in notice["chat"] as Newtonsoft.Json.Linq.JArray ?? new Newtonsoft.Json.Linq.JArray())
            {
                string text = (string)line;
                if (string.IsNullOrWhiteSpace(text)) continue;
                if (twitch) try { Send(CPH, "twitch", text, bot); } catch (System.Exception) { }
                if (youtube) try { Send(CPH, "youtube", text, bot); } catch (System.Exception) { }
                if (kick) try { Send(CPH, "kick", text, bot); } catch (System.Exception) { }
            }
            foreach (var item in notice["toast"] as Newtonsoft.Json.Linq.JArray ?? new Newtonsoft.Json.Linq.JArray())
                if (item is Newtonsoft.Json.Linq.JObject toast && ((bool?)toast["always"] == true || !connected))
                    CPH.ShowToastNotification((string)toast["title"] ?? "UIFramework", (string)toast["text"] ?? "", "UIFramework", "");
        }
        catch (System.Exception ex)
        {
            CPH.LogWarn("[" + actionName + "] The update notice failed: " + (ex.InnerException ?? ex).Message);
        }
    }

    private static void Send(IInlineInvokeProxy CPH, string platform, string text, bool bot)
    {
        if (platform == "youtube") CPH.SendYouTubeMessageToLatestMonitored(text, bot, true);
        else if (platform == "kick") CPH.SendKickMessage(text, bot, true);
        else CPH.SendMessage(text, bot, true);
    }

    /// <summary>UIFramework's UpdateNotices: from the newest DLL installed when no loaded one is as new.</summary>
    internal static System.Type FrameworkType()
    {
        System.Type loaded = null;
        System.Version loadedVersion = null;
        foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            var name = assembly.GetName();
            if (name.Name != "UIFramework") continue;
            var type = assembly.GetType("UIFramework.UpdateNotices", false);
            if (type != null && (loadedVersion == null || Full(name.Version) > loadedVersion)) { loaded = type; loadedVersion = Full(name.Version); }
        }
        string lib = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "UIFramework", "lib");
        string newestFile = null;
        System.Version newest = null;
        if (System.IO.Directory.Exists(lib))
            foreach (var dir in System.IO.Directory.GetDirectories(lib))
            {
                System.Version version;
                string file = System.IO.Path.Combine(dir, "UIFramework.dll");
                // "v1.8.0" is a folder too, as it is to the settings action.
                string folder = System.IO.Path.GetFileName(dir).TrimStart('v', 'V');
                if (System.Version.TryParse(folder, out version) && System.IO.File.Exists(file) && (newest == null || Full(version) > newest))
                {
                    newest = Full(version);
                    newestFile = file;
                }
            }
        if (newestFile != null && newestFile != _refused && (loadedVersion == null || newest > loadedVersion))
        {
            try
            {
                // As the settings action loads it: not a file that no longer matches the hash it was downloaded with
                // (that one is left for the settings action to set aside), and with UnsafeLoadFrom, since LoadFrom
                // refuses a DLL saved from a browser (Windows' "downloaded from the internet" mark).
                if (!Intact(newestFile)) { _refused = newestFile; return loaded; }
                var type = System.Reflection.Assembly.UnsafeLoadFrom(newestFile).GetType("UIFramework.UpdateNotices", false);
                if (type != null) return type;
            }
            catch (System.Exception) { _refused = newestFile; }
        }
        return loaded;
    }

    // The newest DLL once it was found damaged or would not load: not hashed and tried again on every call.
    private static string _refused;

    /// <summary>Whether the DLL still is what was downloaded: its SHA-256 against the .sha256 file the settings action
    /// wrote beside it. A DLL put in by hand has no such file and counts as it is.</summary>
    internal static bool Intact(string dll)
    {
        string hashFile = dll + ".sha256";
        if (!System.IO.File.Exists(hashFile)) return true;
        string expected = System.IO.File.ReadAllText(hashFile).Trim().ToLowerInvariant();
        var text = new System.Text.StringBuilder();
        using (var sha = System.Security.Cryptography.SHA256.Create())
        using (var stream = System.IO.File.OpenRead(dll))
            foreach (byte b in sha.ComputeHash(stream))
                text.Append(b.ToString("x2"));
        return text.ToString() == expected;
    }

    private static System.Version Full(System.Version v) => new System.Version(v.Major, v.Minor, System.Math.Max(0, v.Build), System.Math.Max(0, v.Revision));
}

/// <summary>The custom triggers the window declares (UiWindow.Trigger). <see cref="Register"/> in the game action's
/// Init registers every one with Streamer.bot, so the declaration is the only list there is: what is registered, what
/// the Help page shows and what its Fire button fires cannot drift apart. The game still fires them itself with
/// CPH.TriggerCodeEvent where its rounds end.</summary>
public static class UiTriggers
{
    /// <summary>The name a Fire button asks under, followed by the trigger's code.</summary>
    public const string FireRequest = "uiframework.trigger:";

    /// <summary>Registers every declared trigger under the categories ("Chat Game", "Trivia Game"). One that fails is
    /// logged and the rest still register.</summary>
    public static void Register(IInlineInvokeProxy CPH, params string[] categories) => RegisterFrom(UiSchema.Window, CPH, categories);

    /// <summary>Register, for a window other than the game's own (the tests').</summary>
    public static void RegisterFrom(UiWindow window, IInlineInvokeProxy CPH, params string[] categories)
    {
        if (window == null) return;
        foreach (var item in window.TriggerList)
        {
            string name = (string)item["name"], code = (string)item["code"];
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(code)) continue;
            try { CPH.RegisterCustomTrigger(name, code, categories ?? new string[0]); }
            catch (System.Exception ex) { CPH.LogError("The custom trigger " + name + " could not be registered: " + ex.Message); }
        }
    }

    /// <summary>Fires a declared trigger with its sample values as arguments: "fired", or "unknown" for a code the
    /// window does not declare. A sample that reads as a whole number or as true/false is passed as one.</summary>
    public static string Fire(IInlineInvokeProxy CPH, string code) => FireFrom(UiSchema.Window, CPH, code);

    /// <summary>Fire, for a window other than the game's own (the tests').</summary>
    public static string FireFrom(UiWindow window, IInlineInvokeProxy CPH, string code)
    {
        if (window != null)
            foreach (var item in window.TriggerList)
            {
                if ((string)item["code"] != code) continue;
                var args = item["args"] as Newtonsoft.Json.Linq.JArray;
                if (args != null)
                    foreach (var arg in args)
                        CPH.SetArgument((string)arg["name"], Sample((string)arg["text"]));
                CPH.TriggerCodeEvent(code, true);
                return "fired";
            }
        return "unknown";
    }

    /// <summary>A sample as the argument it stands for: a whole number, true or false, otherwise its text ("" for none).</summary>
    public static object Sample(string text)
    {
        if (text == null) return "";
        long number;
        if (long.TryParse(text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out number)) return number;
        if (text == "true") return true;
        if (text == "false") return false;
        return text;
    }
}

/// <summary>The Twitch ad break as a window of time for the game: chat stays usable while ads run, so that is when a
/// round fits best. Streamer.bot tells of the start (the trigger Twitch, Ads, Ad Run, with the length in adLength)
/// and of an ad to come (Upcoming Ad, every minute from five minutes ahead, with minutes and adLength); it has no
/// trigger for the end, so the window closes when the length has passed. The game action needs both triggers. The
/// settings are the convention keys adBreakStart, adBreakAnnounce, adBreakAnnounceMinutes, adBreakBonusPercent and
/// adBreakOnly.
/// <para>On EventType.TwitchAdRun the game calls <see cref="Begin"/> and starts what it wants to start; on
/// EventType.TwitchUpcomingAd it calls <see cref="Announce"/> and says so in chat when that answers true. Wherever
/// it matters, <see cref="Active"/> says whether ads are running. The window lives in memory: a recompile closes it.</para></summary>
public static class UiAdBreak
{
    private static readonly object Gate = new object();
    private static System.DateTime _until = System.DateTime.MinValue, _announced = System.DateTime.MinValue;
    private static int _length, _generation;

    /// <summary>The clock, for the tests.</summary>
    public static System.Func<System.DateTime> Now = () => System.DateTime.UtcNow;

    /// <summary>An ad break started. Opens the window for its length and answers that length in seconds; 0, and no
    /// window, when the event carries none. <paramref name="ended"/> runs once when the window closes, unless
    /// another break or <see cref="End"/> came first.</summary>
    public static int Begin(System.Collections.Generic.IDictionary<string, object> args, System.Action ended = null)
    {
        int seconds = Number(args, "adLength");
        if (seconds <= 0) return 0;
        int mine;
        lock (Gate)
        {
            _length = seconds;
            _until = Now().AddSeconds(seconds);
            mine = ++_generation;
        }
        if (ended != null)
            System.Threading.Tasks.Task.Delay(System.TimeSpan.FromSeconds(seconds)).ContinueWith(t =>
            {
                lock (Gate) { if (mine != _generation) return; }
                try { ended(); } catch (System.Exception) { }
            });
        return seconds;
    }

    /// <summary>Whether ads are running now.</summary>
    public static bool Active { get { lock (Gate) return Now() < _until; } }

    /// <summary>How long the running ad break still lasts, in seconds; 0 outside one.</summary>
    public static int SecondsLeft { get { lock (Gate) return Now() < _until ? (int)System.Math.Ceiling((_until - Now()).TotalSeconds) : 0; } }

    /// <summary>The length of the last ad break, in seconds.</summary>
    public static int Length { get { lock (Gate) return _length; } }

    /// <summary>Closes the window early, and nothing runs when its time would have passed.</summary>
    public static void End() { lock (Gate) { _until = System.DateTime.MinValue; _generation++; } }

    /// <summary>An ad is coming. True once per ad break, when it is <paramref name="minutesBefore"/> minutes away or
    /// less: Streamer.bot fires every minute from five ahead, and one line in chat is enough.</summary>
    /// <param name="minutes">How many minutes until the ads.</param>
    /// <param name="seconds">How long they will run.</param>
    public static bool Announce(System.Collections.Generic.IDictionary<string, object> args, int minutesBefore, out int minutes, out int seconds)
    {
        minutes = Number(args, "minutes");
        seconds = Number(args, "adLength");
        if (minutes > System.Math.Max(1, System.Math.Min(5, minutesBefore))) return false;
        lock (Gate)
        {
            // The same ad break tells of itself for five minutes; the next one is further off than that.
            if ((Now() - _announced).TotalMinutes < 6) return false;
            _announced = Now();
            return true;
        }
    }

    /// <summary>How many rounds fit an ad break: each takes <paramref name="roundSeconds"/> and the pause after it
    /// <paramref name="gapSeconds"/>. At least one, however short the ads; at most <paramref name="most"/>.</summary>
    public static int RoundsThatFit(int adSeconds, int roundSeconds, int gapSeconds, int most)
    {
        int each = System.Math.Max(1, roundSeconds) + System.Math.Max(0, gapSeconds);
        int fit = (System.Math.Max(0, adSeconds) + System.Math.Max(0, gapSeconds)) / each;
        return System.Math.Max(1, System.Math.Min(fit, System.Math.Max(1, most)));
    }

    /// <summary>A payout with the ad break's bonus on top: <paramref name="bonusPercent"/> more, nothing for 0 or less.</summary>
    public static double WithBonus(double factor, double bonusPercent) => bonusPercent > 0 ? factor * (1.0 + bonusPercent / 100.0) : factor;

    // An argument as a whole number, whatever type Streamer.bot stored it as; 0 when it is none.
    private static int Number(System.Collections.Generic.IDictionary<string, object> args, string name)
    {
        object value;
        if (args == null || !args.TryGetValue(name, out value) || value == null) return 0;
        double number;
        if (!double.TryParse(System.Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out number)) return 0;
        return number > 0 && number < int.MaxValue ? (int)System.Math.Round(number) : 0;
    }
}

public enum UiLiveState { Unknown, Offline, Live }

/// <summary>Whether the streamer is live on a platform, for what should only happen on stream: a round that starts
/// itself. Streamer.bot has no live status to ask, so Twitch is asked through its API with Streamer.bot's own token,
/// YouTube by the broadcasts Streamer.bot monitors, and Kick through its public channel page. Twitch and Kick are
/// asked by UIFramework.dll (the block itself must compile in an action that references nothing but mscorlib), so
/// without the DLL (the settings never opened) or with one older than 1.6.0 they are Unknown. An answer is kept for
/// half a minute. Whatever cannot be told (no account, a login turned down, no answer within six seconds) is
/// Unknown, never an exception; <see cref="SkipReason"/> then lets recent chat decide.</summary>
public static class UiLive
{
    private static readonly object Gate = new object();
    private static readonly System.Collections.Generic.Dictionary<string, System.Collections.Generic.KeyValuePair<System.DateTime, UiLiveState>> Kept =
        new System.Collections.Generic.Dictionary<string, System.Collections.Generic.KeyValuePair<System.DateTime, UiLiveState>>(System.StringComparer.OrdinalIgnoreCase);

    private static bool _saidNoDll;

    /// <param name="platform">"twitch", "youtube" or "kick".</param>
    /// <param name="log">Told what went wrong, and once that the DLL is missing or too old to ask Twitch and Kick.</param>
    public static UiLiveState Of(IInlineInvokeProxy CPH, string platform, System.Action<string> log = null)
    {
        string key = (platform ?? "").Trim().ToLowerInvariant();
        lock (Gate)
        {
            System.Collections.Generic.KeyValuePair<System.DateTime, UiLiveState> kept;
            if (Kept.TryGetValue(key, out kept) && (System.DateTime.UtcNow - kept.Key).TotalSeconds < 30) return kept.Value;
        }
        UiLiveState state = UiLiveState.Unknown;
        try
        {
            if (key == "youtube")
            {
                var broadcasts = CPH.YouTubeGetMonitoredBroadcasts();
                System.Collections.Generic.List<string> statuses = null;
                if (broadcasts != null)
                {
                    statuses = new System.Collections.Generic.List<string>();
                    foreach (object broadcast in broadcasts)
                        if (broadcast != null) statuses.Add(Text(broadcast, "Status"));
                }
                state = YouTubeFromStatuses(statuses);
            }
            else if (key == "kick")
            {
                object broadcaster = CPH.KickGetBroadcaster();
                string login = Text(broadcaster, "UserLogin") ?? Text(broadcaster, "UserName");
                if (!string.IsNullOrWhiteSpace(login)) state = Ask(log, "Kick", login);
            }
            else
            {
                string id = Text(CPH.TwitchGetBroadcaster(), "UserId"), clientId = CPH.TwitchClientId, token = CPH.TwitchOAuthToken;
                if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(clientId) && !string.IsNullOrWhiteSpace(token))
                    state = Ask(log, "Twitch", id, clientId, token);
            }
        }
        catch (System.Exception ex)
        {
            if (log != null) log("Could not tell whether you are live on " + key + ": " + ex.Message);
        }
        lock (Gate) Kept[key] = new System.Collections.Generic.KeyValuePair<System.DateTime, UiLiveState>(System.DateTime.UtcNow, state);
        return state;
    }

    /// <summary>Why something automatic does not go to a platform, or null when it does. Being live is what counts; a
    /// platform that could not say goes by <paramref name="recentChat"/>, whether its chat has been heard lately.</summary>
    public static string SkipReason(UiLiveState live, bool recentChat)
    {
        if (live == UiLiveState.Offline) return "you are not live there.";
        if (live == UiLiveState.Unknown && !recentChat) return "it could not be told whether you are live there, and chat has been quiet.";
        return null;
    }

    /// <summary>The statuses of the broadcasts Streamer.bot monitors: one of them live means you are. A status never
    /// seen before leaves the question open rather than guessing; null (no list at all) too.</summary>
    public static UiLiveState YouTubeFromStatuses(System.Collections.Generic.IEnumerable<string> statuses)
    {
        if (statuses == null) return UiLiveState.Unknown;
        var offline = new System.Collections.Generic.HashSet<string>(new[] { "complete", "completed", "created", "ready", "revoked", "testing", "teststarting", "upcoming", "ended", "offline" }, System.StringComparer.OrdinalIgnoreCase);
        bool unknown = false;
        foreach (string status in statuses)
        {
            string s = (status ?? "").Trim();
            if (s.Equals("live", System.StringComparison.OrdinalIgnoreCase) || s.Equals("liveStarting", System.StringComparison.OrdinalIgnoreCase) || s.Equals("active", System.StringComparison.OrdinalIgnoreCase))
                return UiLiveState.Live;
            if (!offline.Contains(s)) unknown = true;
        }
        return unknown ? UiLiveState.Unknown : UiLiveState.Offline;
    }

    // UIFramework.dll's Live.Twitch or Live.Kick, which answer "Live", "Offline" or "Unknown".
    private static UiLiveState Ask(System.Action<string> log, string method, params string[] args)
    {
        var notices = UiUpdateNotice.FrameworkType();
        var live = notices == null ? null : notices.Assembly.GetType("UIFramework.Live", false);
        var ask = live == null ? null : live.GetMethod(method);
        if (ask == null)
        {
            // Said once: until the settings have been opened, recent chat stands in for the live check.
            bool first;
            lock (Gate) { first = !_saidNoDll; _saidNoDll = true; }
            if (first && log != null) log("Twitch and Kick cannot be asked whether you are live: that needs UIFramework.dll 1.6.0 or newer. Open the game's settings once to get it; until then recent chat decides.");
            return UiLiveState.Unknown;
        }
        object[] values = new object[args.Length];
        for (int i = 0; i < args.Length; i++) values[i] = args[i];
        string answer = ask.Invoke(null, values) as string;
        return answer == "Live" ? UiLiveState.Live : answer == "Offline" ? UiLiveState.Offline : UiLiveState.Unknown;
    }

    // A property of one of Streamer.bot's models as text, by name: the block compiles without naming their types.
    private static string Text(object model, string property)
    {
        if (model == null) return null;
        var p = model.GetType().GetProperty(property);
        object value = p == null ? null : p.GetValue(model, null);
        return value == null ? null : value.ToString();
    }
}

/// <summary>A Streamer.bot timed action run the three ways its own dialog offers: every so many minutes, after a random
/// time between two bounds, and only once chat has written so many lines. Code can set a timed action's interval and
/// switch it on and off, nothing more, so the random time and the lines are kept here. The settings are the convention
/// keys autoStartEnabled, autoStartIntervalMinutes, autoStartRandom, autoStartIntervalMaxMinutes and
/// autoStartMinChatLines.
/// <para>The game calls <see cref="Sync"/> in Init and after a save (restart: true there, so a new interval counts
/// from the save), <see cref="NoteLine"/> for every chat message it sees, and, when the timed action fires,
/// <see cref="Rearm"/> once and <see cref="TakeLines"/> for each chat it would start in: false means chat has been too
/// quiet, and the round waits for the next turn of the timer, as Streamer.bot's own lines do.</para></summary>
public static class UiTimer
{
    private static readonly object Gate = new object();
    private static readonly System.Random Rng = new System.Random();
    private static readonly System.Collections.Generic.Dictionary<string, int> Lines =
        new System.Collections.Generic.Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);

    /// <summary>One more chat line in a scope: a platform, or "" for a game that has one chat.</summary>
    public static void NoteLine(string scope)
    {
        lock (Gate)
        {
            int n;
            Lines.TryGetValue(scope ?? "", out n);
            if (n < int.MaxValue) Lines[scope ?? ""] = n + 1;
        }
    }

    /// <summary>The chat lines of a scope since the last automatic start there.</summary>
    public static int LinesSince(string scope)
    {
        lock (Gate)
        {
            int n;
            return Lines.TryGetValue(scope ?? "", out n) ? n : 0;
        }
    }

    /// <summary>Whether a scope has seen its lines: true, and the count starts over, with at least
    /// <paramref name="minLines"/> lines since the last time (always with 0 or less); false leaves the count as it is.</summary>
    public static bool TakeLines(string scope, int minLines)
    {
        lock (Gate)
        {
            int n;
            Lines.TryGetValue(scope ?? "", out n);
            if (minLines > 0 && n < minLines) return false;
            Lines[scope ?? ""] = 0;
            return true;
        }
    }

    /// <summary>The next wait in seconds: the interval, or with <paramref name="random"/> a time between it and
    /// <paramref name="maxMinutes"/>. At least a minute; a maximum below the interval is the interval.</summary>
    public static int NextSeconds(int minutes, bool random, int maxMinutes)
    {
        int low = System.Math.Max(1, System.Math.Min(minutes, 1440)) * 60;
        int high = System.Math.Max(low, System.Math.Max(1, System.Math.Min(maxMinutes, 1440)) * 60);
        if (!random || high == low) return low;
        lock (Gate) return Rng.Next(low, high + 1);
    }

    /// <summary>Brings the timed action in line with the settings. A timer that already runs keeps its countdown unless
    /// <paramref name="restart"/> says the schedule changed: switching it off and on starts the wait over. A timed action
    /// the import does not have is one line in the log, never an exception.</summary>
    public static void Sync(IInlineInvokeProxy CPH, string timerId, bool enabled, int minutes, bool random, int maxMinutes,
        bool restart = false, System.Action<string> log = null)
    {
        try
        {
            bool running = CPH.GetTimerState(timerId);
            if (!enabled)
            {
                if (running) CPH.DisableTimerById(timerId);
                return;
            }
            if (running && !restart)
            {
                // A random timer keeps the wait it drew; a fixed one is told its interval again, which costs nothing.
                if (!random) CPH.SetTimerInterval(timerId, NextSeconds(minutes, false, maxMinutes));
                return;
            }
            CPH.SetTimerInterval(timerId, NextSeconds(minutes, random, maxMinutes));
            if (running) CPH.DisableTimerById(timerId);
            CPH.EnableTimerById(timerId);
        }
        catch (System.Exception ex)
        {
            if (log != null) log("The timed action " + timerId + " could not be set: " + ex.Message);
        }
    }

    /// <summary>When the timed action has fired: a random timer draws its next wait and starts over with it. A fixed one
    /// simply runs on, and one that was switched off meanwhile (a save between the tick and this run) stays off.</summary>
    public static void Rearm(IInlineInvokeProxy CPH, string timerId, int minutes, bool random, int maxMinutes, System.Action<string> log = null)
    {
        if (!random) return;
        try
        {
            if (!CPH.GetTimerState(timerId)) return;
            CPH.SetTimerInterval(timerId, NextSeconds(minutes, true, maxMinutes));
            CPH.DisableTimerById(timerId);
            CPH.EnableTimerById(timerId);
        }
        catch (System.Exception ex)
        {
            if (log != null) log("The timed action " + timerId + " could not be set: " + ex.Message);
        }
    }
}

/// <summary>The points system of this Streamer.bot, the way the games share it. StreamUP's Currency Core and
/// Tawmae's LOYALTY POINTS each keep their settings in a persisted global that names the user variable the points
/// live in and what they are called; when one is installed, the game uses those and its own two settings
/// (pointsVariable, pointsName) are only the fallback. Detect on every run: the streamer may install one later.
/// Reading and writing a viewer's points stays the game's: CPH.GetTwitchUserVarById / SetTwitchUserVarById (and
/// the YouTube and Kick ones) on Variable.</summary>
public sealed class UiCurrency
{
    /// <summary>The user variable the points live in.</summary>
    public string Variable = "points";
    /// <summary>What the points are called in chat.</summary>
    public string Name = "Points";
    /// <summary>"Currency Core", "LOYALTY POINTS", or null when the settings are in use.</summary>
    public string Source;
    public bool Detected => Source != null;

    /// <summary>Looks for an installed system; without one, the two settings (or their defaults when settings is null).</summary>
    public static UiCurrency Detect(IInlineInvokeProxy CPH, UiSettings settings)
    {
        var currency = new UiCurrency();
        string variable = settings?.String("pointsVariable"), name = settings?.String("pointsName");
        if (!string.IsNullOrWhiteSpace(variable)) currency.Variable = variable.Trim();
        if (!string.IsNullOrWhiteSpace(name)) currency.Name = name.Trim();
        if (!currency.Read(CPH, "currency_ProductSettings", "pointsVar", "pointsName", "Currency Core"))
            currency.Read(CPH, "tawmae_Settings_LOYALTY POINTS", "globalVariableName", "pointsName", "LOYALTY POINTS");
        return currency;
    }

    private bool Read(IInlineInvokeProxy CPH, string global, string variableKey, string nameKey, string source)
    {
        string json;
        try { json = CPH.GetGlobalVar<string>(global, true); }
        catch (System.Exception) { return false; }
        if (string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            var settings = Newtonsoft.Json.Linq.JObject.Parse(json);
            string variable = (string)settings[variableKey];
            if (string.IsNullOrWhiteSpace(variable)) return false;
            Variable = variable.Trim();
            string name = (string)settings[nameKey];
            if (!string.IsNullOrWhiteSpace(name)) Name = name.Trim();
            Source = source;
            return true;
        }
        catch (System.Exception ex)
        {
            CPH.LogWarn("[UIFramework] The settings of " + source + " (" + global + ") could not be read: " + ex.Message);
            return false;
        }
    }

    public Newtonsoft.Json.Linq.JObject ToJson() => new Newtonsoft.Json.Linq.JObject { ["source"] = Source, ["variable"] = Variable, ["name"] = Name };
}

/// <summary>The settings file as the game reads it: every key through its default, old key names tolerated. Values
/// an alias stored in another scale (a fraction where percent is stored now) are converted on the way.</summary>
public sealed class UiSettings
{
    private readonly Newtonsoft.Json.Linq.JObject _root;

    private UiSettings(Newtonsoft.Json.Linq.JObject root) { _root = root; }

    /// <summary>Settings from JSON text, for tests and callers that already hold the values; empty text is empty settings.</summary>
    public static UiSettings Parse(string json)
    {
        try
        {
            using (var reader = new Newtonsoft.Json.JsonTextReader(new System.IO.StringReader(string.IsNullOrWhiteSpace(json) ? "{}" : json)) { DateParseHandling = Newtonsoft.Json.DateParseHandling.None })
                return new UiSettings(Newtonsoft.Json.Linq.JObject.Load(reader));
        }
        catch (System.Exception)
        {
            return new UiSettings(new Newtonsoft.Json.Linq.JObject());
        }
    }

    /// <summary>The values as they are, for a host that reads them its own way.</summary>
    public Newtonsoft.Json.Linq.JObject Raw => _root;

    /// <summary>Why the settings did not come from the file, or null: it could not be read, and the backup of the
    /// window's last save (FromBackup) or the defaults stand in for it.</summary>
    public string Problem { get; private set; }
    public bool FromBackup { get; private set; }

    /// <summary>Never throws: an unreadable file gives the .bak the window's last save kept, else the defaults, and
    /// the game keeps running; Problem says so, and log (CPH.LogWarn, say) hears it. A relative path starts in the
    /// Streamer.bot folder, whatever the current directory.</summary>
    public static UiSettings Load(string path, System.Action<string> log = null)
    {
        if (string.IsNullOrEmpty(path)) return new UiSettings(new Newtonsoft.Json.Linq.JObject());
        try
        {
            if (!System.IO.Path.IsPathRooted(path)) path = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, path);
            if (!System.IO.File.Exists(path)) return new UiSettings(new Newtonsoft.Json.Linq.JObject());
            return new UiSettings(Read(path));
        }
        catch (System.Exception ex)
        {
            string file;
            try { file = System.IO.Path.GetFileName(path); } catch (System.Exception) { file = path; }
            string why = file + " could not be read (" + ex.Message + ")";
            UiSettings stand;
            try { stand = System.IO.File.Exists(path + ".bak") ? new UiSettings(Read(path + ".bak")) { FromBackup = true, Problem = why + "; the backup of the last save is used." } : null; }
            catch (System.Exception) { stand = null; }
            if (stand == null) stand = new UiSettings(new Newtonsoft.Json.Linq.JObject()) { Problem = why + "; the defaults are used." };
            if (log != null) try { log(stand.Problem); } catch (System.Exception) { }
            return stand;
        }
    }

    private static Newtonsoft.Json.Linq.JObject Read(string path)
    {
        using (var reader = new Newtonsoft.Json.JsonTextReader(new System.IO.StringReader(ReadWithRetry(path))) { DateParseHandling = Newtonsoft.Json.DateParseHandling.None })
            return Newtonsoft.Json.Linq.JObject.Load(reader);
    }

    private static string ReadWithRetry(string path)
    {
        for (int attempt = 1; ; attempt++)
        {
            try { return System.IO.File.ReadAllText(path); }
            catch (System.IO.IOException) when (attempt < 5) { System.Threading.Thread.Sleep(40); }
        }
    }

    public Newtonsoft.Json.Linq.JToken Token(string key)
    {
        var token = _root[key];
        if (token != null && token.Type != Newtonsoft.Json.Linq.JTokenType.Null) return token;
        var setting = UiSchema.Find(key);
        if (setting?.Aliases != null)
            for (int i = 0; i < setting.Aliases.Length; i++)
            {
                // An alias with a dot reaches into an object of the old file ("PlatformEmotes.twitch").
                string alias = setting.Aliases[i];
                int dot = alias.IndexOf('.');
                var old = dot > 0 ? (_root[alias.Substring(0, dot)] as Newtonsoft.Json.Linq.JObject)?[alias.Substring(dot + 1)] : _root[alias];
                if (old == null || old.Type == Newtonsoft.Json.Linq.JTokenType.Null) continue;
                if (i == 0 && setting.AliasFactor.HasValue && (old.Type == Newtonsoft.Json.Linq.JTokenType.Integer || old.Type == Newtonsoft.Json.Linq.JTokenType.Float))
                    return System.Convert.ToDouble(((Newtonsoft.Json.Linq.JValue)old).Value, System.Globalization.CultureInfo.InvariantCulture) * setting.AliasFactor.Value;
                return old;
            }
        return setting?.Default == null ? null : Newtonsoft.Json.Linq.JToken.FromObject(setting.Default);
    }

    public bool Bool(string key) { var t = Token(key); return t != null && t.Type == Newtonsoft.Json.Linq.JTokenType.Boolean && (bool)t; }

    public long Long(string key)
    {
        // A whole number as it is: through a double, one past 2^53 would lose its last digits.
        var t = Token(key);
        if (t != null && t.Type == Newtonsoft.Json.Linq.JTokenType.Integer)
        {
            try { return (long)t; }
            catch (System.Exception) { return t.ToString().TrimStart().StartsWith("-") ? long.MinValue : long.MaxValue; }
        }
        double d = Double(key);
        if (d >= long.MaxValue) return long.MaxValue;
        if (d <= long.MinValue) return long.MinValue;
        return (long)System.Math.Round(d);
    }

    public int Int(string key) { long l = Long(key); return l > int.MaxValue ? int.MaxValue : l < int.MinValue ? int.MinValue : (int)l; }

    public double Double(string key)
    {
        var t = Token(key);
        if (t == null) return 0;
        double d;
        if (t.Type == Newtonsoft.Json.Linq.JTokenType.Integer || t.Type == Newtonsoft.Json.Linq.JTokenType.Float) d = System.Convert.ToDouble(((Newtonsoft.Json.Linq.JValue)t).Value, System.Globalization.CultureInfo.InvariantCulture);
        else if (!double.TryParse(t.ToString().Trim().Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out d)) return 0;
        return double.IsNaN(d) || double.IsInfinity(d) ? 0 : d;
    }

    /// <summary>The value as text, numbers with a point whatever the system's culture ("0.5", not "0,5").</summary>
    public string String(string key)
    {
        var t = Token(key);
        if (t == null || t.Type == Newtonsoft.Json.Linq.JTokenType.Null) return "";
        if (t.Type == Newtonsoft.Json.Linq.JTokenType.String) return (string)t;
        if (t.Type == Newtonsoft.Json.Linq.JTokenType.Float) return System.Convert.ToDouble(((Newtonsoft.Json.Linq.JValue)t).Value, System.Globalization.CultureInfo.InvariantCulture).ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        if (t.Type == Newtonsoft.Json.Linq.JTokenType.Integer) return System.Convert.ToString(((Newtonsoft.Json.Linq.JValue)t).Value, System.Globalization.CultureInfo.InvariantCulture);
        if (t.Type == Newtonsoft.Json.Linq.JTokenType.Boolean) return (bool)t ? "true" : "false";
        return t.ToString(Newtonsoft.Json.Formatting.None);
    }

    /// <summary>A ceiling that stores "no limit" as 0: read as long.MaxValue.</summary>
    public long Ceiling(string key) { long l = Long(key); return l <= 0 ? long.MaxValue : l; }
    public int CeilingInt(string key) { long l = Long(key); return l <= 0 || l > int.MaxValue ? int.MaxValue : (int)l; }
    public static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;
    public static long Clamp(long value, long min, long max) => value < min ? min : value > max ? max : value;
    public static double Clamp(double value, double min, double max) => value < min ? min : value > max ? max : value;
}
// --- end UIFramework host block ---
