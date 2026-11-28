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
        // For a message whose text writes %name% where the framework writes {name}: "percent". Null takes the window's.
        public string PlaceholderStyle;
        // For a list: its columns as "key|kind|label" (kinds: string, text, int, double, bool, emote; a trailing "readonly"
        // makes it a column the game fills and the user only reads; an emote column
        // adds "|twitch", "|youtube" or "|kick" for its picture), and the file next to the settings file that holds
        // the rows, with the property in it (no field: the file is the array).
        public string[] Columns; public string File, Field;
        // Global: a secret kept in a persisted global of Streamer.bot of that name instead of the settings file (a
        // bot token another action reads from there); the settings action reads and writes it, never the file.
        // OptionsFrom: the dropdown's options come from the game (UiHost.Handle's options:), asked again whenever
        // the field of that key changes; "groups" for Streamer.bot's groups, "" to ask once.
        public string Global, OptionsFrom;
        // Every: an auto action asks again every so many seconds while its page shows (a status).
        // SaveFirst: a button saves the window's changes before the game is asked (check now, with what is typed).
        public int? Every; public bool SaveFirst;
        // For a list the game writes to as well. RowKey: the columns that name a row ("date,name"): a save merges
        // with what the file holds then, the game's rows stay and the user's changes win. FixedRows: no adding, no
        // removing. Filter: the rows shown, "region= || Global=true || Counties~{region}" ({key} is a field's value,
        // ~ is contains, || is or); the others stay in the file. RowNumbers false: the table does not count its rows in
        // a first column (they carry a number of their own).
        public string RowKey, Filter; public bool FixedRows, RowNumbers = true;
        // For a list shown as a table and for a table: true keeps the columns within the table's width (a text too
        // long ends in an ellipsis), false lets them run past it under a horizontal scrollbar. Null: as the kind
        // does by itself, a list within, a table past.
        public bool? FitWidth;
        // Transient: shown, never saved. For an action: Live hands the host the window's current values, Panel shows
        // its answer under the button, Auto asks it on show and after every change without a button (and brings Live
        // and Panel with it). Ask, on any other field, asks the host whenever the field changes (a preset slider whose
        // answer fills in other fields); Live on such a field means the same and is still read.
        // Row: settings naming the same row, one after the other, share one line of the page.
        public bool Transient, Live, Panel, Auto, Ask;
        // TwitchOnly: a switch that only matters on Twitch carries a "Twitch only" pill.
        public bool TwitchOnly;
        // Masked: a text field shown as dots until its eye button reveals it (a token, a client secret).
        public bool Masked;
        // Hint: grey text inside a one-line text field while it is empty ("not set", "optional"), masked or not.
        public string Hint;
        // Format, on a list: "pairs" stores a list of two columns as one line of text, "a:b,c:d" (UiSettings.Pairs reads it).
        public string Format;
        // RowsFrom, on a list the project keeps itself (rows: and saveRows: in UiHost.Handle): the key of the field its
        // rows depend on, "" for none.
        public string RowsFrom;
        // ShowWhen: shown only while the condition holds ("questionSource=igdb", "mode=a|b", "x!=free && useBot").
        // Shows, on a dropdown: per option, the fields it shows, "igdb: igdbClientId, igdbClientSecret".
        public string ShowWhen; public string[] Shows;
        // ReadOnly: shown, not editable (a status the game writes). Step: a slider's step.
        public bool ReadOnly; public double? Step;
    }

    /// <summary>A setting: key and default, then whatever the conventions cannot know. Keys the conventions know
    /// (useBot, globalCooldown, blockedUsers, ...) need nothing but their default. An option is "value" or
    /// "value|label". aliasFactor multiplies the first alias's value on the move (a fraction to percent: 100).
    /// requires is a switch's key, or a condition on any field: "numberOfSlots>=5", "cpBetMode=fixed", "mode!=free": the
    /// field is greyed out while it does not hold. masked shows a text field as dots.
    /// showWhen hides the field instead, and shows it only while its condition holds: "questionSource=igdb", several values "mode=a|b", several
    /// conditions "mode!=free && useBot", "!key" for a switch that is off. On a dropdown, shows names per option the fields it
    /// shows: "igdb: clientId, clientSecret". readOnly shows a value the game writes without letting the user edit it; step is
    /// a slider's step; group puts a message template under a heading of its own.</summary>
    public static Setting S(string key, object @default, string page = null, string label = null, string help = null, string kind = null,
        string section = null, double? min = null, double? max = null, string unit = null, double? factor = null, bool unlimitedAtZero = false,
        string[] options = null, string requires = null, string confirm = null, bool hidden = false, string[] aliases = null, double? aliasFactor = null,
        string[] columns = null, string file = null, string field = null, bool transient = false, bool live = false, bool panel = false, bool auto = false,
        string platform = null, bool twitchOnly = false, string row = null, bool masked = false, string showWhen = null, string[] shows = null,
        bool readOnly = false, double? step = null, string group = null, bool ask = false,
        string global = null, string optionsFrom = null, int? every = null, bool saveFirst = false,
        string rowKey = null, bool fixedRows = false, string filter = null, string hint = null, string format = null, string rowsFrom = null,
        bool rowNumbers = true, bool? fitWidth = null)
    {
        return new Setting
        {
            Key = key, Default = @default, Page = page, Label = label, Help = help, Kind = kind, Section = section, Min = min, Max = max,
            Unit = unit, Factor = factor, UnlimitedAtZero = unlimitedAtZero, Options = options, Requires = requires, Confirm = confirm,
            Hidden = hidden, Aliases = aliases, AliasFactor = aliasFactor, Columns = columns, File = file, Field = field,
            Transient = transient, Live = live, Panel = panel, Auto = auto, Platform = platform, TwitchOnly = twitchOnly, Row = row, Masked = masked,
            ShowWhen = showWhen, Shows = shows, ReadOnly = readOnly, Step = step, Group = group, Ask = ask,
            Global = global, OptionsFrom = optionsFrom, Every = every, SaveFirst = saveFirst,
            RowKey = rowKey, FixedRows = fixedRows, Filter = filter, Hint = hint, Format = format, RowsFrom = rowsFrom, RowNumbers = rowNumbers,
            FitWidth = fitWidth
        };
    }

    /// <summary>A message template: group, key, default text, label, help, placeholders without braces, old key; file and
    /// field when the text lives in a file of its own next to the settings file (a messages.json). placeholderStyle
    /// "percent" for a message that writes %user% where the others write {user} (the whole window: UiWindow).</summary>
    public static Setting Msg(string group, string key, string @default, string label, string help, string[] placeholders, string alias = null,
        string file = null, string field = null, string placeholderStyle = null)
        => new Setting { Key = key, Default = @default, Kind = "template", Page = "messages", Group = group, Label = label, Help = help,
                         Placeholders = placeholders, Aliases = alias == null ? null : new[] { alias }, File = file, Field = field,
                         PlaceholderStyle = placeholderStyle };

    public static string ToJson(string host, string version, string settingsFile) => ToJson(host, version, settingsFile, null);

    /// <summary>The schema with the points system the window should show as detected (UiCurrency.Detect).</summary>
    public static string ToJson(string host, string version, string settingsFile, UiCurrency currency) => ToJson(host, version, settingsFile, currency, null, null);

    /// <summary>The schema as UiHost.Handle answers describe with it: also the settings a Set Argument of this action
    /// sets (key and the argument's name) and the fields the game locks, one per line as "key|why".</summary>
    public static string ToJson(string host, string version, string settingsFile, UiCurrency currency,
        System.Collections.Generic.IDictionary<string, string> arguments, string locked)
    {
        var settings = new Newtonsoft.Json.Linq.JArray();
        foreach (var s in Table)
            settings.Add(ToJson(s));
        var root = new Newtonsoft.Json.Linq.JObject { ["host"] = host, ["version"] = version, ["settingsFile"] = settingsFile, ["settings"] = settings };
        if (Window != null) root["window"] = Window.ToJson();
        if (Window != null && Window.LangMap.Count > 0) root["lang"] = Window.LangMap;
        if (currency != null && currency.Detected) root["currency"] = currency.ToJson();
        if (arguments != null && arguments.Count > 0)
        {
            var map = new Newtonsoft.Json.Linq.JObject();
            foreach (var pair in arguments) map[pair.Key] = pair.Value;
            root["arguments"] = map;
        }
        if (!string.IsNullOrWhiteSpace(locked))
        {
            var map = new Newtonsoft.Json.Linq.JObject();
            foreach (var line in locked.Split('\n'))
            {
                string entry = line.Trim();
                int bar = entry.IndexOf('|');
                string key = (bar < 0 ? entry : entry.Substring(0, bar)).Trim();
                if (key.Length > 0) map[key] = bar < 0 ? "" : entry.Substring(bar + 1).Trim();
            }
            if (map.Count > 0) root["lockedFields"] = map;
        }
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
        if (s.PlaceholderStyle != null) item["placeholderStyle"] = s.PlaceholderStyle;
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
        // A field that asks is asked with what the window shows: a DLL up to 1.9.0 takes those values only for "live".
        if (s.Live || (s.Ask && s.Kind != "action")) item["live"] = true;
        if (s.Ask) item["ask"] = true;
        if (s.Panel) item["panel"] = true;
        if (s.Auto) item["auto"] = true;
        if (s.TwitchOnly) item["twitchOnly"] = true;
        if (s.Masked) item["masked"] = true;
        if (s.Hint != null) item["hint"] = s.Hint;
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
        if (s.Global != null) item["global"] = s.Global;
        if (s.OptionsFrom != null) item["optionsFrom"] = s.OptionsFrom;
        if (s.Every.HasValue) item["every"] = s.Every.Value;
        if (s.SaveFirst) item["saveFirst"] = true;
        if (s.RowKey != null) item["rowKey"] = s.RowKey;
        if (s.FixedRows) item["fixedRows"] = true;
        if (!s.RowNumbers) item["rowNumbers"] = false;
        if (s.FitWidth.HasValue) item["fitWidth"] = s.FitWidth.Value;
        if (s.Filter != null) item["filter"] = s.Filter;
        if (s.Format != null) item["format"] = s.Format;
        if (s.RowsFrom != null) item["rowsFrom"] = s.RowsFrom;
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
    /// <summary>One thing a header pill can say (UiWindow.Pill): the condition it says it under, as showWhen writes one
    /// ("botToken &amp;&amp; channelId", "mode=a|b", "!useBot"; null: always), its text, its tone ("ok" green, "warn"
    /// amber, "accent", else grey) and its tooltip.</summary>
    public static string[] State(string when, string text, string tone = null, string tooltip = null) => new[] { when, text, tone, tooltip };
    /// <summary>One trigger for UiWindow.Triggers: its name in Streamer.bot, its code, what it is for, and the
    /// arguments that are its own ("name" or "name=sample value").</summary>
    public static string[] Tr(string name, string code, string what = null, params string[] args)
    {
        var parts = new System.Collections.Generic.List<string> { name, code, what };
        if (args != null) parts.AddRange(args);
        return parts.ToArray();
    }
    /// <summary>An action an older version kept its settings in, as Set Argument sub-actions, for UiWindow.Adopt: its
    /// id in Streamer.bot, then what the table cannot tell by itself. An argument named like a setting's key or one
    /// of its aliases needs nothing; "old>new" takes the argument "old" into the setting "new", and
    /// "#subActionId>key" the text of a Send Message sub-action (Twitch, YouTube or Kick) with that sub-action id
    /// into the setting "key".</summary>
    public static string[] Old(string actionId, params string[] map)
    {
        var parts = new System.Collections.Generic.List<string> { actionId };
        if (map != null) parts.AddRange(map);
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

    // ---- The window: title, pages, help, about, log. Null: the settings action carries a spec of its own. ----
    public static readonly UiWindow Window = new UiWindow("High-Low Game", releases: "aaskjer/High-Low-Game", logPrefix: "[High-Low Game/Settings]")
        .Pages(P("game", "Game", "🎲"), P("channelPoints", "Channel points", "💜"), P("cooldowns", "Cooldowns", "⏳"), P("access", "Access", "👥"), P("leaderboard", "Scoreboard", "🏆"), P("messages", "Messages", "💬"), P("log", "Log", "📜"), P("help", "How to play", "📖"), P("about", "About", "ℹ️"))
        .Senders(useBot: "useBot", sendAction: "sendAction", cooldownUseBot: "cooldownUseBot", cooldownSendAction: "cooldownSendAction", numberSeparator: "numberSeparator", cooldownGroup: "Cooldown")
        .Pill("freeGame", "Free mode on", "Free mode off", tooltip: "Free mode, from the Game page or !SetFreeHighLow. While it is on, nobody wins or loses points.")
        .Pill("isChannelPointsTrigger", "Channel points", "Chat commands", tooltip: "How guesses come in on Twitch. YouTube and Kick always use the chat commands.")
        // German: the game's own keys and texts; the conventions already carry the shared keys, sections and pages.
        .Lang("de",
            K("awardPoints", "Punkte vergeben", "Aus, und es wird zum Spaß gespielt: niemand gewinnt oder verliert Punkte, die Tipps zählen trotzdem für die Bestenlisten."),
            K("freeGame", "Freier Modus", "Niemand gewinnt oder verliert Punkte, Kanalpunkt-Einlösungen eingeschlossen, und die Tipps zählen weiter für die Bestenlisten. !SetFreeHighLow und !EndFreeHighLow schalten ihn aus dem Chat (Moderatoren), mit den Nachrichten Gestartet und Beendet."),
            K("freeGameStartMessage", "Gestartet", "Einmal gesendet, wenn !SetFreeHighLow den freien Modus einschaltet, in den Chat, aus dem der Befehl kam. Anders eingeschaltet, etwa per Execute C# Method auf StartFreeGame in einer eigenen Action, geht sie an jede Plattform mit einem Konto in Streamer.bot - an YouTube nur, solange eine Übertragung beobachtet wird."),
            K("freeGameEndMessage", "Beendet", "Einmal gesendet, wenn !EndFreeHighLow den freien Modus ausschaltet, in den Chat, aus dem der Befehl kam. Anders ausgeschaltet, etwa per Execute C# Method auf EndFreeGame, geht sie an jede Plattform mit einem Konto in Streamer.bot - an YouTube nur, solange eine Übertragung beobachtet wird."),
            Tx("Free mode", "Freier Modus"), Tx("Free mode on", "Freier Modus an"), Tx("Free mode off", "Freier Modus aus"),
            Tx("Free mode, from the Game page or !SetFreeHighLow. While it is on, nobody wins or loses points.", "Freier Modus, von der Seite Spiel oder !SetFreeHighLow. Solange er an ist, gewinnt und verliert niemand Punkte."),
            K("wonPoints", "Gewinnbetrag", "Wird für ein richtiges !high oder !low im Chat gezahlt. Ein richtiges !equal zahlt das Doppelte, ein falsches !equal kostet diesen Betrag. Ein falsches !high oder !low kostet nichts."),
            K("equalChance", "Chance auf Gleichstand", "Wie oft ein !equal-Tipp dieselbe Zahl noch einmal zieht. Jede andere Ziehung ist eine freie Wahl von 1 bis 100."),
            K("rewardIdHigh", "Belohnungs-ID für höher", "Die ID der Belohnung, die auf höher tippt. Einlösungen anderer Belohnungen bleiben unberührt."),
            K("rewardIdLow", "Belohnungs-ID für niedriger", "Die ID der Belohnung, die auf niedriger tippt."),
            K("rewardIdEqual", "Belohnungs-ID für gleich", "Die ID der Belohnung, die auf dieselbe Zahl tippt."),
            K("highGlobalCooldown", "Alle", "Wird beim Speichern auf den Befehl geschrieben und für die passende Belohnung übernommen. 0 schaltet sie ab."),
            K("highUserCooldown", "Pro Zuschauer"),
            K("lowGlobalCooldown", "Alle", "Wird beim Speichern auf den Befehl geschrieben und für die passende Belohnung übernommen. 0 schaltet sie ab."),
            K("lowUserCooldown", "Pro Zuschauer"),
            K("equalGlobalCooldown", "Alle", "Wird beim Speichern auf den Befehl geschrieben und für die passende Belohnung übernommen. 0 schaltet sie ab."),
            K("equalUserCooldown", "Pro Zuschauer"),
            K("alltimeGlobalCooldown", "Alle", "Wird beim Speichern auf den Befehl geschrieben. 0 schaltet sie ab."),
            K("alltimeUserCooldown", "Pro Zuschauer"),
            K("sessionGlobalCooldown", "Alle", "Wird beim Speichern auf den Befehl geschrieben. 0 schaltet sie ab."),
            K("sessionUserCooldown", "Pro Zuschauer"),
            K("personalGlobalCooldown", "Alle", "Wird beim Speichern auf den Befehl geschrieben. 0 schaltet sie ab."),
            K("personalUserCooldown", "Pro Zuschauer"),
            K("fixedBetAmount", "Fester Einsatz", "Der Einsatz im Modus Fest, was auch immer die Belohnung kostet."),
            K("minBet", "Mindesteinsatz", "Nur bei Eingabe und Fest: Einsätze darunter werden abgelehnt und erstattet. 0 schaltet die Untergrenze ab."),
            K("leaderboardSize", "Angezeigte Plätze", "Wie viele Spieler !hlgsession und !hlgalltime auflisten."),
            K("leaderboardExcludeBroadcaster", "Broadcaster auslassen", "Deine eigenen Tipps zählen weiterhin; du wirst nur nicht gelistet."),
            K("isChannelPointsTrigger", "Mit Kanalpunkten tippen", "Nur Twitch. Tipps kommen aus den drei Belohnungen unten, und auf Twitch tun die Chat-Befehle !high, !low und !equal nichts. YouTube und Kick spielen weiter im Chat."),
            K("cpBetMode", "Einsatz", "Kosten: der Preis der Belohnung ist der Einsatz. Fest: der feste Einsatz unten. In beiden Fällen sind die Kanalpunkte ausgegeben, ein richtiger Tipp zahlt den Einsatz in Punkten (doppelt bei !equal), ein falscher kostet nichts weiter. Eingabe: der Zuschauer tippt in die Einlösung einen Einsatz in eigenen Punkten (eine Zahl, all, half, third, quarter oder random); nur ein falsches !equal nimmt ihn. Frei: spielt wie die Chat-Befehle mit dem Gewinnbetrag."),
            K("maxBet", "Höchsteinsatz", "Nur bei Eingabe und Fest: Einsätze darüber werden abgelehnt und erstattet. 0 schaltet die Obergrenze ab."),
            K("refundFailedRedemptions", "Einlösungen ohne Tipp erstatten", "Eine Einlösung, die ohne Tipp endet (blockierter Zuschauer, Abklingzeit, abgelehnter Einsatz, Fehler), bekommt ihre Kanalpunkte zurück. Twitch erstattet nur, solange die Einlösung in der Warteschlange steht; die Belohnungen dürfen sie also nicht überspringen."),
            Tx("Cost of the reward", "Kosten der Belohnung"), Tx("Fixed bet", "Fester Einsatz"), Tx("Viewer types a bet", "Zuschauer tippt einen Einsatz"), Tx("Free, like chat", "Frei, wie im Chat"),
            K("logToFile", "Protokolldatei pro Tag schreiben", "Schreibt jeden entschiedenen Tipp, jede neue Zahl und jede erstattete Einlösung in eine datierte 'HLG-log-yyyy-MM-dd.txt' im Ordner Chat Games\\Logs, eine Datei pro Tag."),
            K("wordCorrect", "Richtiger Tipp", "Was {result} nach einem richtigen Tipp sagt."),
            K("wordIncorrect", "Falscher Tipp", "Was {result} nach einem falschen Tipp sagt."),
            K("emojiCorrect", "Emoji, richtig", "Was {emoji} nach einem richtigen Tipp zeigt."),
            K("emojiIncorrect", "Emoji, falsch", "Was {emoji} nach einem falschen Tipp zeigt."),
            K("wordWon", "Punkte gewonnen", "Was {sign} sagt, wenn der Tipp Punkte gewonnen hat."),
            K("wordLost", "Punkte verloren", "Was {sign} sagt, wenn der Tipp Punkte gekostet hat."),
            K("pointsSectionTemplate", "Punkte-Teil", "Was {pointsSection} liest, wenn sich Punkte geändert haben; ohne Änderung bleibt es leer."),
            Tx("Outcome words", "Wörter im Ergebnis"),
            Tx("Player", "Spieler"), Tx("Guess", "Tipp"), Tx("Previous", "Vorher"), Tx("New", "Neu"), Tx("Change", "Änderung"),
            Tx("How guesses come in on Twitch. YouTube and Kick always use the chat commands.", "Wie Tipps auf Twitch ankommen. YouTube und Kick nutzen immer die Chat-Befehle."))
        .Samples("guess", "!high", "result", "correct", "emoji", "🙂", "previousNumber", "42", "newNumber", "77", "number", "42", "sessionGuesses", "3", "alltimeGuesses", "57", "sign", "won", "pointsDelta", "1.234", "pointsSection", " won 1.234 Points", "currency", "Points", "seconds", "12", "amount", "500")
        .Cooldowns(
            C("dc2d4f17-d5f1-4228-923e-5c03652bb30f", "highGlobalCooldown", "highUserCooldown"),
            C("2b7f7ba1-b8a6-4b45-b2ef-31b1ee61f7be", "lowGlobalCooldown", "lowUserCooldown"),
            C("36a22b8f-5e63-4df7-ae8f-3439dd7d6475", "equalGlobalCooldown", "equalUserCooldown"),
            C("68f97444-4f77-4bab-8277-2a920fcdfddf", "alltimeGlobalCooldown", "alltimeUserCooldown"),
            C("5911cfbc-32cb-4749-bb40-3fce8eb76a1a", "sessionGlobalCooldown", "sessionUserCooldown"),
            C("df94341a-8b15-4c6c-9e1a-6af383915f8b", "personalGlobalCooldown", "personalUserCooldown"))
        .Log("Chat Games/Logs", "HLG-log*.txt", writeLogKey: "logToFile",
            columns: new[] { "Player|User", "Guess|Guess", "Previous|Previous", "New|New", "Change|Change" },
            results: new[] { "WIN", "LOSS", "REFUND", "NEW-NUMBER" }, platforms: new[] { "twitch", "youtube", "kick" })
        .Help(
            H("Guessing",
                Note("There is always a number from 1 to 100 waiting; it is drawn once and kept, also when Streamer.bot restarts. Viewers guess whether the next number will be higher, lower or the same. The answer names both numbers, and the new one is what the next guess is measured against."),
                Cmd("!high", "the next number is higher. Right pays the win amount, wrong costs nothing."),
                Cmd("!low", "the next number is lower. Same payout as !high."),
                Cmd("!equal", "the next number is the same. Right pays double, wrong costs the win amount."),
                Cmd("!highlowsetup", "draws a fresh number and announces it. Optional; moderators only, set on the command itself."),
                Cmd("!SetFreeHighLow", "moderators: turns Free mode on, nobody wins or loses points. !EndFreeHighLow turns it off.")),
            H("Scoreboards",
                Cmd("!hlgsession", "the best players of this Streamer.bot session."),
                Cmd("!hlgalltime", "the best players of all time."),
                Cmd("!hlgpersonal", "your own session and all-time stats.")),
            H("Channel points (Twitch)",
                Note("Create one reward each for high, low and equal and paste their IDs on the Channel points page. The game listens to every redemption and plays only those three. Leave 'Skip Reward Requests Queue' off on the rewards, or Twitch cannot refund a redemption that does not play."),
                Note("Cost and Fixed spend the channel points as the stake: a correct guess pays the stake in points, double on equal. Input lets the viewer type a bet in their own points; only a wrong equal costs it. Free plays like the chat commands.")),
            H("For your own actions",
                Note("Every settled guess fires a custom trigger - HLG: Guess Won or HLG: Guess Lost, and HLG: Guess Settled always (Custom > Chat Game > High-Low Game) - with the guess as arguments: guessOutcome, guessUser, guessUserId, guessPlatform, guessGuess, guessPreviousNumber, guessNewNumber, guessMode, guessStake, guessAmount, guessPreviousPoints, guessNewPoints, guessCurrency, guessFree, guessSessionGuesses and guessAlltimeGuesses. GetLastGuessResult, run with an Execute C# Method sub-action on the game action, hands the same arguments to any other action."),
                Note("HLG: New Number fires when !highlowsetup draws one (numberNew, numberUser, numberPlatform). HLG: Free Game Start and HLG: Free Game End fire when Free mode is switched from chat or by an Execute C# Method on StartFreeGame or EndFreeGame.")),
            H("Where things are kept",
                Cmd("HLG-Settings.json", "everything on these pages."),
                Cmd("uiframework.layout.json", "your layout of this window: theme, renamed or hidden pages and fields."),
                Cmd("HLG-log-yyyy-MM-dd.txt", "one file per day in Chat Games\\Logs: every settled guess, new number and refunded redemption, what the Log page shows."),
                Note("The first two sit in Streamer.bot's folder under Chat Games\\High-Low Game. Permissions and the cooldowns themselves live on the commands; saving writes the Cooldowns page onto them.")))
        .About(credits: "High-Low Game by aaskjer", links: new[] { Link("Repository", "https://github.com/aaskjer/High-Low-Game"), Link("Releases", "https://github.com/aaskjer/High-Low-Game/releases"), Link("Extension page", "https://extensions.streamer.bot/t/high-low-game/2781"), Link("StreamUP - Currency Core", "https://streamup.tips/product/currency-core"), Link("aaskjer on Twitch", "https://twitch.tv/aaskjer") })
        // The tour on the window's first open (the ? in the header shows it again); the framework brings the texts.
        .Tour(Tip("start"), Tip("rail"), Tip("save"), Tip("customise"), Tip("end"));

    // ---- The table: every setting the host reads, in page order. ----
    public static readonly Setting[] Table =
    {
        // ---- (conventions) ----
        S("useBot", true, aliases: new[] { "UseBot" }),
        S("sendAction", false, aliases: new[] { "SendAction" }),
        S("cooldownUseBot", true, aliases: new[] { "UseBotCD" }),
        S("cooldownSendAction", false, aliases: new[] { "SendActionCD" }),
        S("numberSeparator", ".", aliases: new[] { "NumberSeparator" }),
        // ---- game ----
        S("awardPoints", true, page: "game", label: "Award points", help: "Off, and the game is played for fun: nobody wins or loses points, the guesses still count for the scoreboards.", section: "Points", aliases: new[] { "AwardPoints" }),
        S("freeGame", false, page: "game", label: "Free mode", help: "Nobody wins or loses points, channel point redemptions included, and the guesses still count for the scoreboards. !SetFreeHighLow and !EndFreeHighLow switch it from chat (moderators), with the Started and Ended messages.", section: "Points"),
        S("wonPoints", 1234, page: "game", label: "Win amount", help: "Paid for a correct !high or !low in chat. A correct !equal pays double and a wrong !equal costs this amount. A wrong !high or !low costs nothing.", section: "Points", min: 0.0, requires: "awardPoints", aliases: new[] { "WonPoints" }),
        S("equalChance", 10, page: "game", label: "Equal chance", help: "How often an !equal guess draws the same number again. Every other draw is a free pick from 1 to 100.", kind: "percent", section: "Numbers", min: 0.0, max: 100.0),
        S("pointsVariable", "points", page: "game", help: "The Streamer.bot user variable that winnings are added to and losses taken from. Currency Core by StreamUP and LOYALTY POINTS by tawmae are found on their own and override this; the window shows which one is installed.", section: "Currency", aliases: new[] { "PointsVar" }),
        S("pointsName", "Points", page: "game", help: "What your currency is called in chat; it fills {currency} and {pointsName}. Overridden by Currency Core or LOYALTY POINTS when one of them is installed.", section: "Currency", aliases: new[] { "PointsName" }),
        // ---- channel points ----
        S("isChannelPointsTrigger", false, page: "channelPoints", label: "Guess with channel points", help: "Twitch only. Guesses come from the three rewards below, and on Twitch the !high, !low and !equal chat commands do nothing. YouTube and Kick keep playing in chat.", section: "Rewards", aliases: new[] { "IsChannelPointsTrigger" }),
        S("rewardIdHigh", "", page: "channelPoints", label: "Reward ID for high", help: "The ID of the custom reward that guesses higher. Redemptions of any other reward are left alone.", section: "Rewards", requires: "isChannelPointsTrigger", aliases: new[] { "TargetRewardIdHigh" }),
        S("rewardIdLow", "", page: "channelPoints", label: "Reward ID for low", help: "The ID of the custom reward that guesses lower.", section: "Rewards", requires: "isChannelPointsTrigger", aliases: new[] { "TargetRewardIdLow" }),
        S("rewardIdEqual", "", page: "channelPoints", label: "Reward ID for equal", help: "The ID of the custom reward that guesses the same number.", section: "Rewards", requires: "isChannelPointsTrigger", aliases: new[] { "TargetRewardIdEqual" }),
        S("cpBetMode", "cost", page: "channelPoints", label: "Stake", help: "Cost: the reward's price is the stake. Fixed: the fixed bet below is. Either way the channel points are spent, a correct guess pays the stake in points (double on !equal) and a wrong one costs nothing more. Input: the viewer types a bet in their own points into the redemption (a number, all, half, third, quarter or random); only a wrong !equal takes it. Free: plays like the chat commands with the win amount.", kind: "options", section: "Bets", requires: "isChannelPointsTrigger", options: new[] { "cost|Cost of the reward", "fixed|Fixed bet", "input|Viewer types a bet", "free|Free, like chat" }, aliases: new[] { "ChannelPointsBetArg", "CPBetMode" }),
        S("fixedBetAmount", 100, page: "channelPoints", label: "Fixed bet", help: "The stake in Fixed mode, whatever the reward costs.", section: "Bets", min: 0.0, requires: "isChannelPointsTrigger", aliases: new[] { "FixedBetAmount" }),
        S("minBet", 0, page: "channelPoints", help: "Input and Fixed only: bets below this are refused and refunded. 0 turns the floor off.", section: "Bets", min: 0.0, requires: "isChannelPointsTrigger", aliases: new[] { "MinBet" }),
        S("maxBet", 0, page: "channelPoints", help: "Input and Fixed only: bets above this are refused and refunded. 0 turns the ceiling off.", section: "Bets", min: 0.0, requires: "isChannelPointsTrigger", aliases: new[] { "MaxBet" }),
        S("refundFailedRedemptions", true, page: "channelPoints", label: "Refund redemptions that don't play", help: "A redemption that ends without a guess (blocked viewer, cooldown, refused bet, error) gets its channel points back. Twitch only refunds while the redemption waits in the request queue, so the rewards must not skip it.", section: "Refunds", requires: "isChannelPointsTrigger", aliases: new[] { "RefundFailedRedemptions" }),
        // ---- cooldowns ----
        S("highGlobalCooldown", 0, page: "cooldowns", label: "Everyone", help: "Written onto the command when you save, and kept for the matching reward. 0 turns it off.", kind: "duration", section: "!high", min: 0.0, aliases: new[] { "CooldownHighGlobalSeconds" }),
        S("highUserCooldown", 0, page: "cooldowns", label: "Per viewer", kind: "duration", section: "!high", min: 0.0, aliases: new[] { "CooldownHighUserSeconds" }),
        S("lowGlobalCooldown", 0, page: "cooldowns", label: "Everyone", help: "Written onto the command when you save, and kept for the matching reward. 0 turns it off.", kind: "duration", section: "!low", min: 0.0, aliases: new[] { "CooldownLowGlobalSeconds" }),
        S("lowUserCooldown", 0, page: "cooldowns", label: "Per viewer", kind: "duration", section: "!low", min: 0.0, aliases: new[] { "CooldownLowUserSeconds" }),
        S("equalGlobalCooldown", 0, page: "cooldowns", label: "Everyone", help: "Written onto the command when you save, and kept for the matching reward. 0 turns it off.", kind: "duration", section: "!equal", min: 0.0, aliases: new[] { "CooldownEqualGlobalSeconds" }),
        S("equalUserCooldown", 0, page: "cooldowns", label: "Per viewer", kind: "duration", section: "!equal", min: 0.0, aliases: new[] { "CooldownEqualUserSeconds" }),
        S("alltimeGlobalCooldown", 0, page: "cooldowns", label: "Everyone", help: "Written onto the command when you save. 0 turns it off.", kind: "duration", section: "!hlgalltime", min: 0.0, aliases: new[] { "CooldownAlltimeLBGlobalSeconds" }),
        S("alltimeUserCooldown", 0, page: "cooldowns", label: "Per viewer", kind: "duration", section: "!hlgalltime", min: 0.0, aliases: new[] { "CooldownAlltimeLBUserSeconds" }),
        S("sessionGlobalCooldown", 0, page: "cooldowns", label: "Everyone", help: "Written onto the command when you save. 0 turns it off.", kind: "duration", section: "!hlgsession", min: 0.0, aliases: new[] { "CooldownSessionLBGlobalSeconds" }),
        S("sessionUserCooldown", 0, page: "cooldowns", label: "Per viewer", kind: "duration", section: "!hlgsession", min: 0.0, aliases: new[] { "CooldownSessionLBUserSeconds" }),
        S("personalGlobalCooldown", 0, page: "cooldowns", label: "Everyone", help: "Written onto the command when you save. 0 turns it off.", kind: "duration", section: "!hlgpersonal", min: 0.0, aliases: new[] { "CooldownPersonalLBGlobalSeconds" }),
        S("personalUserCooldown", 0, page: "cooldowns", label: "Per viewer", kind: "duration", section: "!hlgpersonal", min: 0.0, aliases: new[] { "CooldownPersonalLBUserSeconds" }),
        // ---- access ----
        S("blockedUsers", "", page: "access", help: "These names are ignored when they guess or start a round, with no reply in chat. A blocked redemption is refunded.", section: "Who may not play", aliases: new[] { "BlockedUsers" }),
        S("blockedGroups", "", page: "access", help: "Everyone in these Streamer.bot groups is ignored the same way (Settings > Groups). A group that does not exist is skipped and logged.", section: "Who may not play", aliases: new[] { "BlockedGroups" }),
        // ---- leaderboard ----
        S("leaderboardSize", 5, page: "leaderboard", label: "Places shown", help: "How many players !hlgsession and !hlgalltime list.", section: "Scoreboards", min: 1.0, max: 25.0),
        S("leaderboardExcludeBroadcaster", true, page: "leaderboard", label: "Leave the broadcaster out", help: "Your own guesses still count; you are just not ranked.", section: "Scoreboards"),
        S("leaderboardExcludeBot", true, page: "leaderboard", label: "Leave the bot account out", section: "Scoreboards"),
        // ---- messages ----
        Msg("Game", "outcomeTemplate", "Hey {user}! Previous: {previousNumber}, New: {newNumber}. Your guess '{guess}' was {result} {emoji} {pointsSection}. Session: {sessionGuesses} guesses, All-time: {alltimeGuesses}.", "Outcome", "Sent after every guess. {pointsSection} reads like ' won 1.234 Points' or ' lost 1.234 Points' and stays empty when no points changed; {result}, {emoji}, {sign} and {pointsSection} come from the Outcome words below.", new[] { "user", "guess", "result", "emoji", "previousNumber", "newNumber", "sessionGuesses", "alltimeGuesses", "pointsSection", "sign", "pointsDelta", "currency" }, alias: "OutcomeTemplate"),
        Msg("Outcome words", "wordCorrect", "correct", "Right guess", "What {result} says after a right guess.", new string[0]),
        Msg("Outcome words", "wordIncorrect", "incorrect", "Wrong guess", "What {result} says after a wrong guess.", new string[0]),
        Msg("Outcome words", "emojiCorrect", "🙂", "Emoji, right", "What {emoji} shows after a right guess.", new string[0]),
        Msg("Outcome words", "emojiIncorrect", "😥", "Emoji, wrong", "What {emoji} shows after a wrong guess.", new string[0]),
        Msg("Outcome words", "wordWon", "won", "Points won", "What {sign} says when the guess won points.", new string[0]),
        Msg("Outcome words", "wordLost", "lost", "Points lost", "What {sign} says when the guess cost points.", new string[0]),
        Msg("Outcome words", "pointsSectionTemplate", "{sign} {pointsDelta} {currency}", "Points part", "What {pointsSection} reads when points changed; it stays empty when none did.", new[] { "sign", "pointsDelta", "currency", "pointsName" }),
        Msg("Game", "startMessage", "🎲 High-Low game started! The current number is {number}. Please type '!high', '!low' or '!equal' to guess whether the next number will be higher, lower, or equal.", "New round", "Sent when a moderator runs !highlowsetup. A number is ready from the start, so the game also runs without it.", new[] { "number", "user" }),
        Msg("Free mode", "freeGameStartMessage", "🎲 Free mode is ON! Guess for free - no {currency} at stake!", "Started", "Sent once when Free mode is turned on by !SetFreeHighLow, in the chat the command came from. Turned on another way, say by an Execute C# Method on StartFreeGame in an action of your own, it goes to every platform with an account in Streamer.bot - to YouTube only while a broadcast is monitored.", new[] { "currency" }),
        Msg("Free mode", "freeGameEndMessage", "⏹️ Free mode has ended. Guesses count for {currency} again!", "Ended", "Sent once when Free mode is turned off by !EndFreeHighLow, in the chat the command came from. Turned off another way, say by an Execute C# Method on EndFreeGame, it goes to every platform with an account in Streamer.bot - to YouTube only while a broadcast is monitored.", new[] { "currency" }),
        Msg("Channel points", "redemptionRefunded", "↩️ @{user}, your channel point redemption was refunded.", "Redemption refunded", "Sent when a redemption gets its channel points back. Empty it to refund quietly.", new[] { "user" }),
        Msg("Channel points", "reasonNoInput", "@{user}, type how many {pointsName} you want to bet into the redemption.", "Bet: nothing typed", "Input mode: the redemption came without a bet.", new[] { "user", "pointsName" }),
        Msg("Channel points", "reasonNegativeAmount", "@{user}, '{amount}' is not a bet. Use a number above 0, all, half, third, quarter or random.", "Bet: not a number", "Input mode: the typed bet could not be read or is not above zero.", new[] { "user", "amount", "pointsName" }),
        Msg("Channel points", "reasonInsufficientFunds", "@{user}, you only have {currentPoints} {pointsName}.", "Bet: not enough points", "Input mode: the bet is more than the viewer has.", new[] { "user", "amount", "currentPoints", "pointsName" }),
        Msg("Channel points", "reasonBelowMinimum", "@{user}, the minimum bet is {min} {pointsName}.", "Bet: under the minimum", "Input and Fixed mode: the bet is below the minimum.", new[] { "user", "amount", "min", "pointsName" }),
        Msg("Channel points", "reasonAboveMaximum", "@{user}, the maximum bet is {max} {pointsName}.", "Bet: over the maximum", "Input and Fixed mode: the bet is above the maximum.", new[] { "user", "amount", "max", "pointsName" }),
        Msg("Cooldown", "globalCooldownMessage", "Please wait {seconds} seconds before trying again ⏳", "Everyone's cooldown", "Sent while a command or reward is on cooldown for the whole chat.", new[] { "user", "seconds" }, alias: "GlobalMessage"),
        Msg("Cooldown", "userCooldownMessage", "@{user}, please wait {seconds} seconds before trying again ⏳", "A viewer's cooldown", "Sent while a command or reward is on cooldown for that one viewer.", new[] { "user", "seconds" }, alias: "UserMessage"),
        // ---- log ----
        S("logToFile", true, page: "log", label: "Write a log file per day", help: "Writes every settled guess (win or loss), every new number and every refunded redemption to a dated 'HLG-log-yyyy-MM-dd.txt' in Chat Games\\Logs, one file per day.", aliases: new[] { "LogToFile" }),
    };
}

/// <summary>How the window is called and laid out, declared beside the table: what the framework's docs call the
/// spec (docs/Schema.md, "The window"). Every part is optional; a page a setting names but the window does not is
/// made up on the way; how to play, log and about sit at the foot of the page list.</summary>
public sealed class UiWindow
{
    public string Title, Releases, Logo, LogPrefix, Language, PlaceholderStyle, Requires;
    public bool Locked;
    public readonly Newtonsoft.Json.Linq.JArray GroupList = new Newtonsoft.Json.Linq.JArray();
    public double? Width, Height;
    public readonly System.Collections.Generic.List<object> PageList = new System.Collections.Generic.List<object>();
    public readonly Newtonsoft.Json.Linq.JObject SampleMap = new Newtonsoft.Json.Linq.JObject(), AnswerMap = new Newtonsoft.Json.Linq.JObject();
    public Newtonsoft.Json.Linq.JObject SenderMap, LogMap, AboutMap, StreamerBotLogMap;
    /// <summary>What Updates(...) declared; null: the framework's defaults.</summary>
    public Newtonsoft.Json.Linq.JObject UpdateOptions;
    public readonly Newtonsoft.Json.Linq.JArray PillList = new Newtonsoft.Json.Linq.JArray(), CooldownList = new Newtonsoft.Json.Linq.JArray(), HelpList = new Newtonsoft.Json.Linq.JArray(), SectionList = new Newtonsoft.Json.Linq.JArray();
    public readonly Newtonsoft.Json.Linq.JObject LangMap = new Newtonsoft.Json.Linq.JObject();
    public readonly Newtonsoft.Json.Linq.JArray TourList = new Newtonsoft.Json.Linq.JArray();
    public readonly Newtonsoft.Json.Linq.JArray TriggerList = new Newtonsoft.Json.Linq.JArray(), MethodList = new Newtonsoft.Json.Linq.JArray();
    public readonly Newtonsoft.Json.Linq.JArray AdoptList = new Newtonsoft.Json.Linq.JArray(), AdoptExceptList = new Newtonsoft.Json.Linq.JArray();

    /// <summary>Takes over the values of a version that kept its settings as Set Argument sub-actions, each old action
    /// as UiSchema.Old(actionId, ...). It happens once, while there is no settings file yet: the window looks for the
    /// action in Streamer.bot's actions, then in Streamer.bot's last backups, the newest first, and shows the values
    /// of its active Set Arguments as unsaved changes for the user to save. A UIFramework.dll that does not know
    /// this yet opens the window without them.</summary>
    public UiWindow Adopt(params string[][] old)
    {
        if (old == null) return this;
        foreach (var o in old)
        {
            if (o == null) continue;
            var entry = new Newtonsoft.Json.Linq.JObject { ["action"] = o.Length > 0 ? o[0] : null };
            if (o.Length > 1)
            {
                var map = new Newtonsoft.Json.Linq.JArray();
                for (int i = 1; i < o.Length; i++) map.Add(o[i]);
                entry["map"] = map;
            }
            AdoptList.Add(entry);
        }
        return this;
    }

    /// <summary>What the old version shipped where the user was to put a value of their own, as in
    /// AdoptExcept("&lt;&lt; YOUR_* &gt;&gt;"): Adopt leaves out a value that still reads so, and the field keeps
    /// its default. * stands for any text; neither capitals nor the spaces around the value count. A
    /// UIFramework.dll before 2.6.0 does not know this and takes such a value over.</summary>
    public UiWindow AdoptExcept(params string[] values)
    {
        if (values != null)
            foreach (var value in values)
                if (value != null) AdoptExceptList.Add(value);
        return this;
    }

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
    /// <param name="placeholderStyle">"percent" when the messages write their placeholders as %user% instead of {user}:
    /// the chips insert them so, and the chat preview fills them. A single message can say its own (UiSchema.Msg).</param>
    /// <param name="requires">The oldest UIFramework the window works with, "2.1.0": a settings action from stub 0.3.3
    /// on offers the update instead of opening the window with an older one (a secret kept in a global needs it).</param>
    public UiWindow(string title, string releases = null, string logo = null, string logPrefix = null, double? width = null, double? height = null,
        string placeholderStyle = null, string requires = null)
    {
        Title = title; Releases = releases; Logo = logo; LogPrefix = logPrefix; Width = width; Height = height; PlaceholderStyle = placeholderStyle;
        Requires = requires;
    }

    /// <summary>A group of pages in the rail, under a heading the user can fold: its id, its title and the ids of
    /// its pages, in the order the rail shows them. A page no group names stays where Pages puts it. An older
    /// UIFramework shows the pages without the headings.</summary>
    public UiWindow Group(string id, string title, params string[] pages)
    {
        GroupList.Add(new Newtonsoft.Json.Linq.JObject { ["id"] = id, ["title"] = title, ["pages"] = new Newtonsoft.Json.Linq.JArray(pages ?? new string[0]) });
        return this;
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

    /// <summary>The window's language, whatever the user's is ("de"): the window then offers no language to pick.
    /// Left out, the user picks among the languages there are texts for.</summary>
    public UiWindow InLanguage(string code) { Language = code; return this; }

    /// <summary>The pages in order: a UiPage (UiSchema.P) or the id of a standard page ("messages", "log", "help", "about").</summary>
    public UiWindow Pages(params object[] pages) { PageList.AddRange(pages); return this; }

    /// <summary>The Log page: the folder (relative to the Streamer.bot folder) and the files in it, "BJG-log*.txt";
    /// the bool setting behind "Write a log file per day"; the columns after Time and Result as "Header|field" or a
    /// field; the values of the Result filter. <paramref name="styles"/> colours results, "GAME-OVER|muted" or with *
    /// "WIN-*|success" (success, warning, muted, accent, none; the first that fits wins, the rest go by the words in
    /// them); <paramref name="outcome"/> names what a result is filtered by, "DUEL WIN|WIN". <paramref name="fitWidth"/>
    /// true keeps the columns within the table's width, a value too long ending in an ellipsis; without it they are
    /// as wide as their contents, under a horizontal scrollbar.</summary>
    public UiWindow Log(string folder, string files, string writeLogKey = null, string writeLogHelp = null, string[] columns = null, string[] results = null,
        string[] platforms = null, string emptyText = null, string writeLogLabel = null, string[] styles = null, string[] outcome = null,
        bool? fitWidth = null)
    {
        LogMap = new Newtonsoft.Json.Linq.JObject { ["folder"] = folder, ["files"] = files };
        if (fitWidth.HasValue) LogMap["fitWidth"] = fitWidth.Value;
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
        if (styles != null) LogMap["styles"] = LogPairs(styles);
        if (outcome != null) LogMap["outcome"] = LogPairs(outcome);
        return this;
    }

    // "WIN-*|success" as the schema has it: { "WIN-*": "success" }, in the order given.
    private static Newtonsoft.Json.Linq.JObject LogPairs(string[] pairs)
    {
        var map = new Newtonsoft.Json.Linq.JObject();
        foreach (var pair in pairs)
        {
            int bar = (pair ?? "").LastIndexOf('|');
            if (bar > 0) map[pair.Substring(0, bar).Trim()] = pair.Substring(bar + 1).Trim();
        }
        return map;
    }

    /// <summary>Streamer.bot's own log as a page, for a project that logs with CPH.LogInfo and writes no file of its
    /// own: the lines of a day that hold one of <paramref name="match"/> ("[My Project", "'My Action'"), whatever the
    /// case; without one, every line. The page comes by itself, at the foot of the rail; naming "sblog" among the
    /// pages gives it a title or an icon of its own.</summary>
    public UiWindow StreamerBotLog(params string[] match)
    {
        var texts = new Newtonsoft.Json.Linq.JArray();
        if (match != null)
            foreach (var text in match)
                if (!string.IsNullOrEmpty(text)) texts.Add(text);
        StreamerBotLogMap = new Newtonsoft.Json.Linq.JObject { ["match"] = texts };
        return this;
    }

    /// <summary>The same, saying how the page's table meets its width. <paramref name="fitWidth"/> true keeps the
    /// columns within it, as the page does by itself: a line too long ends in an ellipsis, and the box under the list
    /// has all of it. False lets a line run on, under a horizontal scrollbar.</summary>
    public UiWindow StreamerBotLog(bool fitWidth, params string[] match)
    {
        StreamerBotLog(match);
        StreamerBotLogMap["fitWidth"] = fitWidth;
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
    /// <remarks>title and titleWhen: the heading reads as title while that condition holds ("Free Game Check and Game
    /// Sale Check" while one switch joins the two). page: only the section of that name on that page.</remarks>
    public UiWindow Section(string name, string showWhen = null, string title = null, string titleWhen = null, string page = null)
    {
        var section = new Newtonsoft.Json.Linq.JObject { ["name"] = name };
        if (showWhen != null) section["showWhen"] = showWhen;
        if (title != null) section["title"] = title;
        if (titleWhen != null) section["titleWhen"] = titleWhen;
        if (page != null) section["page"] = page;
        SectionList.Add(section);
        return this;
    }

    /// <summary>A pill in the window's header that says one of several things (UiSchema.State): the first state whose
    /// condition holds is shown, none where none holds. It follows the window as it is typed in.</summary>
    public UiWindow Pill(params string[][] states)
    {
        var list = new Newtonsoft.Json.Linq.JArray();
        foreach (var state in states)
        {
            if (state == null || state.Length < 2 || state[1] == null) continue;
            var item = new Newtonsoft.Json.Linq.JObject { ["text"] = state[1] };
            if (state[0] != null) item["when"] = state[0];
            if (state.Length > 2 && state[2] != null) item["tone"] = state[2];
            if (state.Length > 3 && state[3] != null) item["tooltip"] = state[3];
            list.Add(item);
        }
        PillList.Add(new Newtonsoft.Json.Linq.JObject { ["states"] = list });
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

    /// <summary>A tour of the window, stop by stop (UiSchema.Tip): it starts by itself on the window's first open and
    /// again from the ? in the header. Its texts translate through Lang's Tx like every declared text.</summary>
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
    /// them out, streamerBotLink: false only that one.</summary>
    public UiWindow About(string credits = null, UiSection[] sections = null, UiLine[] links = null, bool streamerBotLink = true, bool defaultLinks = true)
    {
        AboutMap = new Newtonsoft.Json.Linq.JObject();
        if (credits != null) AboutMap["credits"] = credits;
        // Loops, not LINQ: the block compiles in an action that has no reference to System.Core.
        if (sections != null)
        {
            var list = new Newtonsoft.Json.Linq.JArray();
            foreach (var s in sections) list.Add(s.ToJson());
            AboutMap["sections"] = list;
        }
        if (links != null)
        {
            var list = new Newtonsoft.Json.Linq.JArray();
            foreach (var l in links) list.Add(new Newtonsoft.Json.Linq.JObject { ["text"] = l.Json["link"], ["url"] = l.Json["url"] });
            AboutMap["links"] = list;
        }
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
        if (PlaceholderStyle != null) root["placeholderStyle"] = PlaceholderStyle;
        if (Requires != null) root["requires"] = Requires;
        if (GroupList.Count > 0) root["groups"] = GroupList;
        if (Width.HasValue) root["width"] = Width.Value;
        if (Height.HasValue) root["height"] = Height.Value;
        if (PageList.Count > 0)
        {
            var pages = new Newtonsoft.Json.Linq.JArray();
            foreach (var p in PageList) pages.Add(p is UiPage ? ((UiPage)p).ToJson() : (Newtonsoft.Json.Linq.JToken)p.ToString());
            root["pages"] = pages;
        }
        if (SenderMap != null) root["senders"] = SenderMap;
        if (SampleMap.Count > 0) root["samples"] = SampleMap;
        if (AnswerMap.Count > 0) root["answers"] = AnswerMap;
        if (PillList.Count > 0) root["headerPills"] = PillList;
        if (SectionList.Count > 0) root["sections"] = SectionList;
        if (CooldownList.Count > 0) root["cooldowns"] = CooldownList;
        if (UpdateOptions != null) root["updates"] = UpdateOptions;
        if (LogMap != null) root["log"] = LogMap;
        if (StreamerBotLogMap != null) root["streamerBotLog"] = StreamerBotLogMap;
        if (HelpList.Count > 0) root["help"] = HelpList;
        if (AboutMap != null) root["about"] = AboutMap;
        if (TourList.Count > 0) root["tour"] = TourList;
        if (TriggerList.Count > 0) root["triggers"] = TriggerList;
        if (MethodList.Count > 0) root["methods"] = MethodList;
        if (AdoptList.Count > 0) root["adopt"] = AdoptList;
        if (AdoptExceptList.Count > 0) root["adoptExcept"] = AdoptExceptList;
        return root;
    }
}

/// <summary>A page of the window: its id, title and icon (an emoji, or a conventions name like "gear"), and the
/// settings it claims ahead of their own page (keys, or patterns like "overlay*").</summary>
public sealed class UiPage
{
    public string Id, Title, Icon; public string[] Fields; public bool Bottom;
    public UiPage(string id, string title, string icon, string[] fields) { Id = id; Title = title; Icon = icon; Fields = fields; }
    /// <summary>Puts the page at the foot of the rail, above How to play, Log and About: UiSchema.P("stats", "Stats").AtBottom().</summary>
    public UiPage AtBottom(bool bottom = true) { Bottom = bottom; return this; }
    public Newtonsoft.Json.Linq.JObject ToJson()
    {
        var page = new Newtonsoft.Json.Linq.JObject { ["id"] = Id };
        if (Title != null) page["title"] = Title;
        if (Icon != null) page["icon"] = Icon;
        if (Fields != null && Fields.Length > 0) page["fields"] = new Newtonsoft.Json.Linq.JArray(Fields);
        if (Bottom) page["bottom"] = true;
        return page;
    }
}

/// <summary>A section of the Help or About page: a heading and lines, each a note (a string), UiSchema.Cmd or UiSchema.Link.</summary>
public sealed class UiSection
{
    public string Heading; public object[] Lines;
    public UiSection(string heading, object[] lines) { Heading = heading; Lines = lines; }
    public Newtonsoft.Json.Linq.JObject ToJson()
    {
        var lines = new Newtonsoft.Json.Linq.JArray();
        foreach (var l in Lines) lines.Add(l is UiLine ? ((UiLine)l).Json : (Newtonsoft.Json.Linq.JToken)l.ToString());
        return new Newtonsoft.Json.Linq.JObject { ["heading"] = Heading, ["lines"] = lines };
    }
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
    /// <param name="actionName">This action's name, as the settings action knows it: the prefix of its log lines.
    /// Null takes the name Streamer.bot gives the run (its argument actionName).</param>
    /// <param name="host">A short name of the game for the framework's files, "blackjack".</param>
    /// <param name="version">The game's version, "v17.0.0".</param>
    /// <param name="settingsFile">The settings file, relative to the Streamer.bot folder.</param>
    /// <param name="test">Answers a Try button or a live field by its key with the window's current values: a line for the footer, or JSON (docs/Schema.md, kind action). Null for a key: "no answer".</param>
    /// <param name="validate">Looks at the values about to be saved; answers the problems, one per line, or null.</param>
    /// <param name="saved">Runs after a save with the saved values: an overlay to write, a timer to restart. The cooldowns the window declares are set by the settings action itself.</param>
    /// <param name="table">The rows of a table field by its key, as a JSON array of objects.</param>
    /// <param name="emotes">The emote sources for emote fields, as JSON (docs/Schema.md).</param>
    /// <param name="arguments">True for an action that reads its settings with UiSettings.With(args): a setting a Set
    /// Argument sub-action of this action sets is then shown locked in the window, with the argument's name.</param>
    /// <param name="locked">The fields that are locked now, one per line as "key|why" (a status the game holds, a value
    /// another program manages): shown with the reason, not edited. It gets the settings as the file has them.</param>
    /// <param name="options">The options of a dropdown declared with optionsFrom, by its key, with the values as they
    /// stand in the window: each "value" or "value|Label" (the regions of the country picked). Null: none.</param>
    /// <param name="rows">The rows of a list the project keeps itself (rowsFrom on the list), by its key, with the
    /// values as they stand in the window: a JSON array of objects, a property per column (others are kept and come
    /// back). Asked when the window opens and again when the field named in rowsFrom changes.</param>
    /// <param name="saveRows">Keeps such a list when the window saves: its key, the values being saved (the list's rows
    /// under its key, UiSettings.Rows), and the rows the window was handed, as a JSON array, to tell what the user
    /// changed from what changed here meanwhile. Answers the rows as they are kept now, a JSON array again. What it
    /// throws is shown as the reason the list could not be saved, and the list stays unsaved in the window.</param>
    /// <returns>True when the action was called by the settings window and has answered; return true at once then.</returns>
    // In front of a list's key in a test request: the window asks for the rows of a list the project keeps, or hands
    // them back on Save (with the rows it was handed under "<key>@loaded").
    internal const string RowsRequest = "uiframework.rows:";
    internal const string SaveRowsRequest = "uiframework.saveRows:";

    // { "rows": [ ... ] } as the handler names them, or { "error": "why" }: no handler, not an array, or what it threw.
    private static string RowsAnswer(string key, UiSettings values, System.Func<string, UiSettings, string> rows, System.Func<string, UiSettings, string, string> saveRows)
    {
        var answer = new Newtonsoft.Json.Linq.JObject();
        try
        {
            string named;
            if (saveRows != null)
            {
                var loaded = values.Token(key + "@loaded") as Newtonsoft.Json.Linq.JArray ?? new Newtonsoft.Json.Linq.JArray();
                named = saveRows(key, values, loaded.ToString(Newtonsoft.Json.Formatting.None));
            }
            else if (rows != null) named = rows(key, values);
            else { answer["error"] = "This project names no handler for the rows of '" + key + "' (rows: and saveRows: in UiHost.Handle)."; return answer.ToString(Newtonsoft.Json.Formatting.None); }
            var array = string.IsNullOrWhiteSpace(named) ? new Newtonsoft.Json.Linq.JArray() : Newtonsoft.Json.Linq.JToken.Parse(named) as Newtonsoft.Json.Linq.JArray;
            if (array == null) answer["error"] = "The rows of '" + key + "' are no JSON array.";
            else answer["rows"] = array;
        }
        catch (System.Exception ex) { answer["error"] = ex.Message; }
        return answer.ToString(Newtonsoft.Json.Formatting.None);
    }

    public static bool Handle(System.Collections.Generic.IDictionary<string, object> args, IInlineInvokeProxy CPH, string actionName, string host, string version, string settingsFile,
        System.Func<string, UiSettings, string> test = null, System.Func<UiSettings, string> validate = null, System.Action<UiSettings> saved = null,
        System.Func<string, string> table = null, System.Func<string> emotes = null, bool arguments = false, System.Func<UiSettings, string> locked = null,
        System.Func<string, UiSettings, string[]> options = null,
        System.Func<string, UiSettings, string> rows = null, System.Func<string, UiSettings, string, string> saveRows = null)
    {
        string request = Arg(args, "uiframework.request");
        if (request == null && args != null && args.ContainsKey("uiframework.describe")) request = "describe";
        if (string.IsNullOrEmpty(request))
            return false;
        if (string.IsNullOrEmpty(actionName)) actionName = Arg(args, "actionName") ?? "";
        // The settings action names the global it will read; without that (an older stub) the name is built here.
        string var = Arg(args, "uiframework.var");
        if (request == "describe")
        {
            var currency = UiCurrency.Detect(CPH, null);   // only a detected system goes into the answer; the window shows it
            // The Set Arguments of this action that set a setting: its sub-actions ran before this code, so they are in
            // args. What the settings action itself was run with (uiframework.callerArgs, stub 0.3.3) is not counted.
            var set = arguments ? UiSettings.Arguments(args, Arg(args, "uiframework.callerArgs")) : null;
            string lockedNow = null;
            if (locked != null)
                try { lockedNow = locked(UiSettings.Load(settingsFile)); }
                catch (System.Exception ex) { CPH.LogError("[" + actionName + "] The locked fields could not be told: " + ex.Message); }
            CPH.SetGlobalVar(string.IsNullOrEmpty(var) ? UiSchema.Var(actionName) : var, UiSchema.ToJson(host, version, settingsFile, currency, set, lockedNow), false);
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
        System.Threading.Volatile.Write(ref _answering, var);
        try
        {
            switch (request)
            {
                // The Fire button of a declared trigger on the Help page; everything else is the game's to answer.
                // And the rows of a list the project keeps: asked for, or handed back to be kept.
                case "test":
                    if (name.StartsWith(UiTriggers.FireRequest)) answer = UiTriggers.Fire(CPH, name.Substring(UiTriggers.FireRequest.Length));
                    else if (name.StartsWith(RowsRequest)) answer = RowsAnswer(name.Substring(RowsRequest.Length), values, rows, null);
                    else if (name.StartsWith(SaveRowsRequest)) answer = RowsAnswer(name.Substring(SaveRowsRequest.Length), values, null, saveRows);
                    else answer = test?.Invoke(name, values);
                    break;
                case "validate": answer = validate?.Invoke(values); break;
                case "saved": saved?.Invoke(values); break;
                case "table": answer = table?.Invoke(name); break;
                case "emotes": answer = emotes?.Invoke(); break;
                // The options of a dropdown that asks for them (optionsFrom), as a JSON array of "value" or "value|Label".
                case "options":
                {
                    var offered = options?.Invoke(name, values);
                    answer = offered == null ? null : new Newtonsoft.Json.Linq.JArray(offered).ToString(Newtonsoft.Json.Formatting.None);
                    break;
                }
                default: CPH.LogWarn("[" + actionName + "] The settings window asked for '" + request + "', which this game does not know."); break;
            }
        }
        catch (System.Exception ex)
        {
            CPH.LogError("[" + actionName + "] The settings window's request '" + request + "' failed: " + ex.Message);
            answer = request == "saved" ? null : "The game could not answer: " + ex.Message;
        }
        // Only its own: a request that hung and ends while the settings action has gone on with the next one leaves
        // that one's StillWorking in place.
        finally { System.Threading.Interlocked.CompareExchange(ref _answering, null, var); }
        CPH.SetGlobalVar(string.IsNullOrEmpty(var) ? AnswerVar(actionName) : var, answer ?? "", false);
        return true;
    }

    // The variable of the request being answered now, which StillWorking names to the settings action.
    private static string _answering;

    /// <summary>Says, from inside a request (a Try button that imports a list, say), that the game is still working on
    /// it: the settings action waits 30 seconds from now for the answer, where it otherwise gives up 30 seconds after the
    /// request started. Call it every few seconds while the work goes on. Outside a request, and with a settings action
    /// from before stub 0.3.2, it does nothing.</summary>
    public static void StillWorking()
    {
        string var = System.Threading.Volatile.Read(ref _answering);
        if (!string.IsNullOrEmpty(var)) System.AppDomain.CurrentDomain.SetData(WorkingKey(var), System.DateTime.UtcNow.Ticks);
    }

    /// <summary>Where StillWorking leaves the time for the settings action: this AppDomain's data, under the request's
    /// own variable. Not a global: one that changes every few seconds would only fill Streamer.bot's variables.</summary>
    public static string WorkingKey(string var) => var + ".working";

    private static string Arg(System.Collections.Generic.IDictionary<string, object> args, string key) => args != null && args.TryGetValue(key, out var value) ? value as string : null;
}

/// <summary>The update notice, once per Streamer.bot start (docs/Schema.md, "Update notice"). Call
/// <c>UiUpdateNotice.Startup(CPH, ...)</c> from the game action's <c>Init()</c> and tick "Precompile on Application Start"
/// on its Execute Code sub-action: Streamer.bot then runs it as it starts. In the background it asks UIFramework.dll (the
/// newest under &lt;Streamer.bot&gt;\UIFramework\lib that this game's settings action fits, or the one the settings
/// loaded) every few seconds until the notice is due, waits up to a minute for a broadcaster account to connect, then
/// sends the chat line to Twitch, YouTube and Kick, whichever is connected, and shows a Windows toast when no
/// broadcaster account is (or always, as the window's <c>Updates</c> says). Every game asks; one of them gets the whole
/// notice. Without the DLL (the settings never opened) nothing happens.</summary>
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

    // The settings action released with this block (StubVersion in stub/Stub.cs, kept equal). One UIFramework runs per
    // Streamer.bot: a DLL loaded here that needs a newer one would keep this game's settings window from opening.
    internal const string StubVersion = "0.3.4";

    /// <summary>UIFramework's UpdateNotices: from the UIFramework loaded already, else from the newest DLL installed that
    /// this game's settings action fits. One that is damaged or will not load leaves it to the next older one.</summary>
    internal static System.Type FrameworkType()
    {
        // One UIFramework per Streamer.bot: once one is loaded, .NET answers every later load of a UIFramework.dll, of
        // whichever version, with that one.
        bool anyLoaded = false;
        System.Type loaded = null;
        System.Version loadedVersion = null;
        foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            var name = assembly.GetName();
            if (name.Name != "UIFramework") continue;
            anyLoaded = true;
            var type = assembly.GetType("UIFramework.UpdateNotices", false);
            if (type != null && (loadedVersion == null || Full(name.Version) > loadedVersion)) { loaded = type; loadedVersion = Full(name.Version); }
        }
        if (anyLoaded) return loaded;
        string lib = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "UIFramework", "lib");
        var installed = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<System.Version, string>>();
        if (System.IO.Directory.Exists(lib))
            foreach (var dir in System.IO.Directory.GetDirectories(lib))
            {
                System.Version version;
                string file = System.IO.Path.Combine(dir, "UIFramework.dll");
                // "v1.8.0" is a folder too, as it is to the settings action.
                string folder = System.IO.Path.GetFileName(dir).TrimStart('v', 'V');
                if (System.Version.TryParse(folder, out version) && System.IO.File.Exists(file))
                    installed.Add(new System.Collections.Generic.KeyValuePair<System.Version, string>(Full(version), file));
            }
        installed.Sort((a, b) => b.Key.CompareTo(a.Key));
        foreach (var candidate in installed)
        {
            string file = candidate.Value, seen;
            try { seen = file + "|" + System.IO.File.GetLastWriteTimeUtc(file).Ticks; }
            catch (System.Exception) { continue; }
            lock (Refused) if (Refused.ContainsKey(seen)) continue;
            try
            {
                // As the settings action loads it: not one that asks for a newer settings action than this game's, not a
                // file that no longer matches the hash it was downloaded with (that one is left for the settings action
                // to set aside), and with UnsafeLoadFrom, since LoadFrom refuses a DLL saved from a browser (Windows'
                // "downloaded from the internet" mark).
                if (NeedsNewerStub(file) || !Intact(file)) { Refuse(seen); continue; }
                // The one this Streamer.bot runs from now on, also when it is too old for notices.
                return System.Reflection.Assembly.UnsafeLoadFrom(file).GetType("UIFramework.UpdateNotices", false);
            }
            catch (System.Exception) { Refuse(seen); }
        }
        return null;
    }

    // The DLLs found damaged, not fitting or not loading, by path and time written: not hashed and tried again on every
    // call. A file downloaded again (the settings action sets a damaged one aside) is written anew and tried anew.
    // A dictionary where a set would do: HashSet lives in System.Core, which an action need not reference.
    private static readonly System.Collections.Generic.Dictionary<string, bool> Refused = new System.Collections.Generic.Dictionary<string, bool>(System.StringComparer.OrdinalIgnoreCase);

    private static void Refuse(string seen) { lock (Refused) Refused[seen] = true; }

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

    /// <summary>Whether a DLL's product version ("1.10.0+stub.0.4.0", read without loading it) asks for a newer settings
    /// action than <see cref="StubVersion"/>; one that names none fits. FileVersionInfo is reached by name: it lives in
    /// System.dll, and a game action may ship without a reference to it (Streamer.bot compiles with the ones it lists).</summary>
    internal static bool NeedsNewerStub(string dll)
    {
        try
        {
            var info = System.Type.GetType("System.Diagnostics.FileVersionInfo, System, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", true)
                .GetMethod("GetVersionInfo", new[] { typeof(string) }).Invoke(null, new object[] { dll });
            string product = info.GetType().GetProperty("ProductVersion").GetValue(info, null) as string ?? "";
            int at = product.IndexOf("+stub.", System.StringComparison.Ordinal);
            if (at < 0) return false;
            string named = product.Substring(at + 6).Trim().TrimStart('v', 'V');
            System.Version needs;
            return System.Version.TryParse(named.Contains(".") ? named : named + ".0", out needs) && Full(needs) > Full(System.Version.Parse(StubVersion));
        }
        catch (System.Exception) { return false; }
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
        var offline = new[] { "complete", "completed", "created", "ready", "revoked", "testing", "teststarting", "upcoming", "ended", "offline" };
        bool unknown = false;
        foreach (string status in statuses)
        {
            string s = (status ?? "").Trim();
            if (s.Equals("live", System.StringComparison.OrdinalIgnoreCase) || s.Equals("liveStarting", System.StringComparison.OrdinalIgnoreCase) || s.Equals("active", System.StringComparison.OrdinalIgnoreCase))
                return UiLiveState.Live;
            if (System.Array.IndexOf(offline, (s ?? "").ToLowerInvariant()) < 0) unknown = true;
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
        lock (Switching) SyncLocked(CPH, timerId, enabled, minutes, random, maxMinutes, restart, log);
    }

    // Sync and Rearm look at the timer, then switch it: one at a time, or a save that switches it off between Rearm's
    // look and its switching on would find it on again.
    private static readonly object Switching = new object();

    private static void SyncLocked(IInlineInvokeProxy CPH, string timerId, bool enabled, int minutes, bool random, int maxMinutes,
        bool restart, System.Action<string> log)
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
    /// simply runs on, and one that was switched off meanwhile (a save between the tick and this run, or during it)
    /// stays off.</summary>
    public static void Rearm(IInlineInvokeProxy CPH, string timerId, int minutes, bool random, int maxMinutes, System.Action<string> log = null)
    {
        if (!random) return;
        try
        {
            lock (Switching)
            {
                if (!CPH.GetTimerState(timerId)) return;
                CPH.SetTimerInterval(timerId, NextSeconds(minutes, true, maxMinutes));
                CPH.DisableTimerById(timerId);
                CPH.EnableTimerById(timerId);
            }
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
    /// the game keeps running; Problem says so, and log (CPH.LogWarn, say) hears it. A file that is missing while a
    /// save is under way is waited for a moment, and one still missing beside its .bak is read from that; only a file
    /// never saved gives the defaults without a word. A relative path starts in the Streamer.bot folder, whatever the
    /// current directory.</summary>
    public static UiSettings Load(string path, System.Action<string> log = null)
    {
        if (string.IsNullOrEmpty(path)) return new UiSettings(new Newtonsoft.Json.Linq.JObject());
        try
        {
            if (!System.IO.Path.IsPathRooted(path)) path = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, path);
            // The window saves beside the file and swaps it in (File.Replace, the old one becoming the .bak): for a
            // moment there is no file, and a swap that failed halfway leaves only the .bak.
            for (int attempt = 1; !System.IO.File.Exists(path); attempt++)
            {
                bool backup = System.IO.File.Exists(path + ".bak"), saving = System.IO.File.Exists(path + ".tmp");
                if (!backup && (attempt >= 5 || !saving)) return new UiSettings(new Newtonsoft.Json.Linq.JObject());
                // Only a save under way (its .tmp) is waited for. Without one the file is looked for once more, as a
                // swap may have ended between the looks; still missing, it is read from the .bak at once, not after
                // 160 ms on every load.
                if (attempt >= 5 || (!saving && !System.IO.File.Exists(path))) throw new System.IO.FileNotFoundException("it is missing", path);
                if (saving) System.Threading.Thread.Sleep(40);
            }
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

    /// <summary>The settings with this run's arguments over them: a Set Argument sub-action named like a key of the
    /// table, or like an old key of one, wins over the file while it holds something (an empty one does not), turned
    /// into what the setting holds ("yes", "1", "on" for a switch; "2,5" for a number). So a value can differ per
    /// action, and an action that was configured with Set Arguments keeps working: UiSettings.Load(file).With(args).
    /// Pass arguments: true to UiHost.Handle, and the window shows such a setting locked. An argument that does not
    /// fit its setting (text for a number) and a list are left as the file has them.</summary>
    public UiSettings With(System.Collections.Generic.IDictionary<string, object> args)
    {
        if (args == null || args.Count == 0) return this;
        Newtonsoft.Json.Linq.JObject root = null;
        foreach (var s in UiSchema.Table)
        {
            string name; Newtonsoft.Json.Linq.JToken value;
            if (!Argument(args, s, out name, out value)) continue;
            if (root == null) root = (Newtonsoft.Json.Linq.JObject)_root.DeepClone();
            root[s.Key] = value;
        }
        return root == null ? this : new UiSettings(root) { Problem = Problem, FromBackup = FromBackup };
    }

    /// <summary>The settings an argument sets, by key, with the argument's name (the key, or an old key).
    /// <paramref name="callerArgs"/> names the arguments that are not this action's own, one per line: those the
    /// settings action was run with and handed on. They do not count.</summary>
    public static System.Collections.Generic.Dictionary<string, string> Arguments(System.Collections.Generic.IDictionary<string, object> args, string callerArgs = null)
    {
        var found = new System.Collections.Generic.Dictionary<string, string>();
        if (args == null) return found;
        var handedOn = new System.Collections.Generic.Dictionary<string, bool>();
        foreach (var handed in (callerArgs ?? "").Split('\n')) handedOn[handed] = true;
        foreach (var s in UiSchema.Table)
        {
            string name; Newtonsoft.Json.Linq.JToken value;
            if (Argument(args, s, out name, out value) && !handedOn.ContainsKey(name)) found[s.Key] = name;
        }
        return found;
    }

    // The argument that sets a setting, under its key or the first old key that is no setting of its own, and its
    // value as the setting holds it. False without one, with an empty one and with one that does not fit.
    private static bool Argument(System.Collections.Generic.IDictionary<string, object> args, UiSchema.Setting s, out string name, out Newtonsoft.Json.Linq.JToken value)
    {
        name = null; value = null;
        if (s.Transient || s.Kind == "action" || s.Kind == "note" || s.Kind == "table" || s.Kind == "list") return false;
        var names = new System.Collections.Generic.List<string> { s.Key };
        if (s.Aliases != null)
            foreach (var alias in s.Aliases)
                if (UiSchema.Find(alias) == null) names.Add(alias);
        foreach (var candidate in names)
        {
            object raw;
            if (!args.TryGetValue(candidate, out raw) || raw == null) continue;
            if (raw is string && ((string)raw).Trim().Length == 0) continue;
            value = FromArgument(raw, s.Default);
            if (value == null) continue;
            name = candidate;
            return true;
        }
        return false;
    }

    // An argument's value in the shape of the setting's default: a switch, a whole number, a number, else text.
    private static Newtonsoft.Json.Linq.JToken FromArgument(object raw, object shape)
    {
        string text = raw is bool ? ((bool)raw ? "true" : "false") : System.Convert.ToString(raw, System.Globalization.CultureInfo.InvariantCulture).Trim();
        if (shape is bool)
        {
            switch (text.ToLowerInvariant())
            {
                case "true": case "yes": case "on": case "1": case "ja": return true;
                case "false": case "no": case "off": case "0": case "nein": return false;
                default: return null;
            }
        }
        bool whole = shape is int || shape is long || shape is short || shape is byte;
        if (whole || shape is double || shape is float || shape is decimal)
        {
            double number;
            if (raw is bool || !double.TryParse(text.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out number)
                || double.IsNaN(number) || double.IsInfinity(number)) return null;
            if (!whole) return number;
            number = System.Math.Round(number);
            return number >= long.MaxValue ? long.MaxValue : number <= long.MinValue ? long.MinValue : (long)number;
        }
        return shape == null || shape is string ? (Newtonsoft.Json.Linq.JToken)text : null;
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
                // An alias with a dot reaches into an object of the old file ("PlatformEmotes.twitch"). One that is a
                // setting of its own again holds that setting's value: the window does not move it either.
                string alias = setting.Aliases[i];
                if (UiSchema.Find(alias) != null) continue;
                int dot = alias.IndexOf('.');
                var old = dot > 0 ? (_root[alias.Substring(0, dot)] as Newtonsoft.Json.Linq.JObject)?[alias.Substring(dot + 1)] : _root[alias];
                if (old == null || old.Type == Newtonsoft.Json.Linq.JTokenType.Null) continue;
                // Rounded as the window's store rounds it on the move: 0.07 * 100 is 7.000000000000001 to a double.
                if (i == 0 && setting.AliasFactor.HasValue && (old.Type == Newtonsoft.Json.Linq.JTokenType.Integer || old.Type == Newtonsoft.Json.Linq.JTokenType.Float))
                    return System.Math.Round(System.Convert.ToDouble(((Newtonsoft.Json.Linq.JValue)old).Value, System.Globalization.CultureInfo.InvariantCulture) * setting.AliasFactor.Value, 10);
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
        // A whole number beyond long is a BigInteger to Json.NET, which Convert cannot take: read from its digits.
        var number = (t.Type == Newtonsoft.Json.Linq.JTokenType.Integer || t.Type == Newtonsoft.Json.Linq.JTokenType.Float) ? ((Newtonsoft.Json.Linq.JValue)t).Value : null;
        if (number is System.IConvertible) d = System.Convert.ToDouble(number, System.Globalization.CultureInfo.InvariantCulture);
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

    /// <summary>The pairs of a list stored as one line of text (format: "pairs"), "a:b,c:d": every key with its value,
    /// in the order they stand in. Entries stand between commas, semicolons or lines, and the first colon or equals
    /// sign parts a key from its value; a key alone has the value "". Rows in the file (an array of objects) are read
    /// by their first two properties.</summary>
    /// <summary>The rows of a list as the file or the window holds them: an array of objects, a property per column.
    /// An empty array where there is none.</summary>
    public Newtonsoft.Json.Linq.JArray Rows(string key)
    {
        return Token(key) as Newtonsoft.Json.Linq.JArray ?? new Newtonsoft.Json.Linq.JArray();
    }

    public System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, string>> Pairs(string key)
    {
        var pairs = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, string>>();
        var t = Token(key);
        if (t is Newtonsoft.Json.Linq.JArray)
        {
            foreach (var row in (Newtonsoft.Json.Linq.JArray)t)
            {
                var cells = row as Newtonsoft.Json.Linq.JObject;
                if (cells == null) continue;
                string name = "", value = "";
                int cellAt = 0;
                foreach (var cell in cells.Properties())
                {
                    string text = cell.Value == null || cell.Value.Type == Newtonsoft.Json.Linq.JTokenType.Null ? ""
                        : cell.Value.Type == Newtonsoft.Json.Linq.JTokenType.String ? (string)cell.Value : cell.Value.ToString(Newtonsoft.Json.Formatting.None);
                    if (cellAt == 0) name = text.Trim(); else if (cellAt == 1) value = text.Trim();
                    cellAt++;
                }
                if (name.Length > 0 || value.Length > 0) pairs.Add(new System.Collections.Generic.KeyValuePair<string, string>(name, value));
            }
            return pairs;
        }
        if (t is Newtonsoft.Json.Linq.JContainer) return pairs;
        foreach (string entry in String(key).Split(new[] { ',', ';', '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries))
        {
            string pair = entry.Trim();
            if (pair.Length == 0) continue;
            int at = pair.IndexOfAny(new[] { ':', '=' });
            pairs.Add(new System.Collections.Generic.KeyValuePair<string, string>(at < 0 ? pair : pair.Substring(0, at).Trim(), at < 0 ? "" : pair.Substring(at + 1).Trim()));
        }
        return pairs;
    }

    /// <summary>The values of a setting that holds several, stored as text with commas between them: the ticked
    /// options of a multi field ("steam,gog" is steam and gog), the names of a namelist or a grouplist. Without the
    /// blanks around a value, without empty and repeated ones; none is an empty array. Where the setting has options,
    /// a value comes in its option's own spelling whatever its case in the file, the options' values first and in
    /// their order; a value no option has (an older list, a typo in the file) follows as the file holds it.</summary>
    public string[] Values(string key)
    {
        var named = new System.Collections.Generic.List<string>();
        var t = Token(key);
        if (t == null) return named.ToArray();
        // An array in the file (written by hand) is read as its values.
        var parts = new System.Collections.Generic.List<string>();
        if (t is Newtonsoft.Json.Linq.JArray)
        {
            foreach (var item in (Newtonsoft.Json.Linq.JArray)t)
                if (item is Newtonsoft.Json.Linq.JValue && item.Type != Newtonsoft.Json.Linq.JTokenType.Null)
                    parts.Add(item.Type == Newtonsoft.Json.Linq.JTokenType.String ? (string)item : item.ToString(Newtonsoft.Json.Formatting.None));
        }
        else if (!(t is Newtonsoft.Json.Linq.JContainer)) parts.AddRange(String(key).Split(','));
        foreach (var part in parts)
        {
            string value = part.Trim();
            if (value.Length > 0 && Named(named, value) == null) named.Add(value);
        }
        var options = UiSchema.Find(key)?.Options;
        if (options == null || options.Length == 0) return named.ToArray();
        var values = new System.Collections.Generic.List<string>();
        foreach (var option in options)
        {
            int bar = option.IndexOf('|');
            string own = (bar < 0 ? option : option.Substring(0, bar)).Trim();
            if (own.Length > 0 && Named(named, own) != null && Named(values, own) == null) values.Add(own);
        }
        foreach (var value in named)
            if (Named(values, value) == null) values.Add(value);
        return values.ToArray();
    }

    // The value among them that is this one, whatever its case; null when none is.
    private static string Named(System.Collections.Generic.List<string> values, string value)
    {
        foreach (var have in values)
            if (string.Equals(have, value, System.StringComparison.OrdinalIgnoreCase)) return have;
        return null;
    }

    /// <summary>A ceiling that stores "no limit" as 0: read as long.MaxValue.</summary>
    public long Ceiling(string key) { long l = Long(key); return l <= 0 ? long.MaxValue : l; }
    public int CeilingInt(string key) { long l = Long(key); return l <= 0 || l > int.MaxValue ? int.MaxValue : (int)l; }
    public static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;
    public static long Clamp(long value, long min, long max) => value < min ? min : value > max ? max : value;
    public static double Clamp(double value, double min, double max) => value < min ? min : value > max ? max : value;
}
// --- end UIFramework host block ---
