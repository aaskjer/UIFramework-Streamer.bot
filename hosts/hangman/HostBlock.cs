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
    public static readonly UiWindow Window = new UiWindow("Hangman Game", releases: "aaskjer/Hangman-Game", logo: "https://github.com/aaskjer/Hangman-Game/blob/main/Assets/HM/Hangman-Game-Full2.png?raw=true", logPrefix: "[Hangman Game/Settings]")
        .Trigger("Hangman: Game Start", "hm_onStart", "A round started.")
        .Triggers(new[] { "roundOutcome=WIN", "roundWord=streamer", "roundCategory=Twitch", "roundDifficulty=2", "roundLetters=8", "roundPlatform=twitch", "roundWinner=alice", "roundWinnerId=12345", "roundPoints=500", "roundWrongGuesses=2", "roundMaxWrong=6", "roundBlitz=false", "roundFree=false", "roundCurrency=Points", "roundParticipants=alice, bob" },
            Tr("Hangman: Game Win", "hm_onWin", "Somebody solved the word."),
            Tr("Hangman: Game Loss", "hm_onLoss", "The lives ran out.", "roundOutcome=LOSS", "roundWinner=", "roundWinnerId=", "roundPoints=0", "roundWrongGuesses=6"),
            Tr("Hangman: Game Stopped", "hm_onStop", "A moderator stopped the round.", "roundOutcome=STOPPED", "roundWinner=", "roundWinnerId=", "roundPoints=0"),
            Tr("Hangman: Timeout", "hm_onTimeout", "The round's time ran out.", "roundOutcome=TIMEOUT", "roundWinner=", "roundWinnerId=", "roundPoints=0"))
        .Trigger("Hangman: Ad Break Started", "hm_onAdBreak", "A Twitch ad break started; with 'Start a round when ads start' on, a round starts with it.", "adLength=90", "adRoundStarted=true")
        .Trigger("Hangman: Ad Break Ended", "hm_onAdBreakEnd", "The ad break's length has passed.", "adLength=90")
        .Trigger("Hangman: Free Game Start", "hm_onFreeStart", "Free mode was switched on.")
        .Trigger("Hangman: Free Game End", "hm_onFreeEnd", "Free mode was switched off.")
        .Method("StartRound", "Starts a round, with no cooldown and no moderator check. Returns false when none started.", "hangmanPlatform=in: twitch, youtube, kick or all", "hangmanBlitz=in: true forces a blitz round", "hangmanStarted=out: whether a round began", "hangmanStartedOn=out: the platforms it began on")
        .Method("GetLastRoundResult", "Sets the round arguments of the last settled round, the same the triggers carry.")
        .Method("StartFreeGame", "Switches Free mode on.")
        .Method("EndFreeGame", "Switches Free mode off.")
        .Pages(P("game", "Game", "🎮"), P("automation", "Automation", "⏱️"), P("words", "Words", "🔤"), P("overlay", "Overlay", "📺"), P("access", "Access", "👥"), P("messages", "Messages", "💬"), P("board", "Board", "🏆"), P("log", "Log", "📜"), P("help", "How to play", "📖"), P("about", "About", "ℹ️"))
        .Senders(useBot: "useBot", sendAction: "sendAction", cooldownUseBot: "cooldownUseBot", cooldownSendAction: "cooldownSendAction", numberSeparator: "numberSeparator", cooldownGroup: "Cooldown")
        .Pill("overlayEnabled", "Overlay on", "Overlay off", tooltip: "Overlay, from the Overlay page. Off: the game writes no overlay files.")
        .Pill("freeGame", "Free mode on", "Free mode off", tooltip: "Free mode, from the Game page or !SetFreeHangman. While it is on, letters and the win pay nothing.")
        .Pill("requireModToStart", "Mods start", "Anyone starts", tooltip: "Who starts a round, from the Access page. Everyone can always guess.")
        .Samples("user", "alice", "targetUser", "bob", "target", "bob", "letter", "e", "word", "streamer", "guess", "streamel", "masked", "s t r e _ _ e r", "category", "Streaming",
                 "hint", "Someone who broadcasts live", "lives", "4", "maxlives", "6", "guessed", "a, e, r, s, t", "wrong", "i, o", "seconds", "30", "points", "750", "basepoints", "500",
                 "pointsName", "Points", "difficulty", "2", "length", "8", "wordbonus", "25", "bet", "100", "stake", "100", "maxguesses", "3", "window", "10", "platform", "twitch",
                 "topn", "5", "medal", "🥇", "rank", "1", "wins", "12", "winrate", "60", "played", "20", "letters", "48", "wordwins", "7", "multiplier", "2", "oldword", "overlay",
                 "streak", "3", "beststreak", "5", "streakbonus", "20", "users", "alice (4), bob (3)")
        .Answer("importWords", "{\"lines\":[\"Wikidata, countries: 25 found, 3 already in the list, 22 added to Hangman-Wordlist.json.\",\"Reopen the window to see them in the list.\"],\"status\":\"22 words imported\"}")
        .Answer("setupObsOverlay", "'Hangman Overlay' is in scene 'Hangman', pointing at the overlay file.")
        .Answer("refreshObsOverlay", "Refreshed: Hangman Overlay")
        .Answer("overlaySample", "Playing a sample round on the overlay for about 20 seconds. If you are live, your viewers see it too.")
        .Answer("overlayDemo", "The demo opened in your browser. In OBS, add &demo=1 to the end of the source's address to place it the same way.")
        .Answer("openWordlistFolder", "Opened Chat Games\\Hangman Game.")
        .Answer("openOverlayFolder", "Opened Chat Games\\Hangman Game\\Overlay.")
        .Log("Chat Games/Logs", "HM-log*.txt", writeLogKey: "logToFile", columns: new[] { "Player|User", "Platform|Platform", "Round|Round", "Word|Word,Word was", "Guess|Guess,Letter", "Lives|Lives", "Wrong|Wrong,Wrong guesses", "Reward|Reward" },
             results: new[] { "START", "HIT-LETTR", "MISS-LTTR", "DUPE-LTTR", "WIN-LETTR", "WIN-WORD", "MISS-WORD", "MISS-PART", "DUPE-WORD", "GAME-OVER", "TIMEOUT", "STOP", "EVIL-SWAP", "GLITCH-HOLD", "STREAK-END", "AWARD-WIN", "AWARD-LTER", "CP-START", "CP-BAD-INPUT", "REFUND", "BLOCKED", "COOLDOWN", "ANTICHEAT", "ADDICTED", "FREE-START", "FREE-END" },
             platforms: new[] { "twitch", "youtube", "kick" }, emptyText: "No rounds logged yet. Every round, guess and payout lands here once 'Write a log file per day' is on.",
             styles: new[] { "GAME-OVER|muted", "MISS-*|warning" })
        .Help(
            H("A round",
                Note("A moderator (or anyone, from the Access page) starts a round, a word is drawn from the list and shown masked, and chat guesses letters or the whole word. Lives, wrong guesses, hints and the round clock tick along until somebody solves it, the lives run out, time is up or a moderator stops it."),
                Cmd("!hangman", "starts a round (also !hm); !hangman blitz forces a blitz round, if allowed."),
                Cmd("!g e", "guesses a letter; !g streamer guesses the whole word (also !guess)."),
                Cmd("!hmr", "repeats the masked word, the lives and the guesses (also !hangmanrepeat)."),
                Cmd("!killhm", "moderators: stops the round and reveals the word (also !killhangman)."),
                Cmd("!hma add bob", "moderators: puts a viewer on the addicted list; !hma remove bob takes them off (also !hangmanaddict)."),
                Cmd("!SetFreeHangman", "moderators: turns Free mode on, letters and wins pay nothing. !EndFreeHangman turns it off.")),
            H("What a win pays",
                Note("'Win pays' for the whole word, scaled up for harder and longer words when word scaling is on, times the blitz multiplier in a blitz, plus a streak bonus, then the subscriber or VIP boost; an addicted viewer loses a share. Every revealed letter can pay its own reward. A channel point stake comes back times the stake payout multiplier, in your currency."),
                Note("Which words play: the difficulty range and the categories on the Words page. The hint stored with a word is revealed after enough wrong guesses.")),
            H("Blitz and Evil",
                Note("A blitz round is short, has fewer lives and multiplies every point; it comes by chance or on '!hangman blitz'. Evil Hangman swaps the word mid-round once chat has burned through part of its lives: the overlay glitches, every guess is wiped except one revealed letter, and chat only hears about it once the glitch is over.")),
            H("The scoreboard",
                Cmd("!hms", "shows the top of the board for this platform (also !hangmanscoreboard)."),
                Cmd("!hmp", "shows a viewer's own stats: wins, played, win rate, points, letters, word wins, streak (also !hangmanpersonal)."),
                Cmd("!hml bob", "looks up another viewer's stats (also !hmlookup).")),
            H("For your own actions",
                Note("Custom triggers (Custom > Chat Game > Hangman Game): Hangman: Game Start, Game Win, Game Loss, Game Stopped and Timeout fire with the round as arguments - roundOutcome, roundWord, roundCategory, roundDifficulty, roundPlatform, roundWinner, roundWinnerId, roundPoints, roundWrongGuesses, roundMaxWrong, roundBlitz, roundParticipants; Hangman: Free Game Start and Free Game End when Free mode is switched. GetLastRoundResult, run with an Execute C# Method sub-action on the game action, hands the same arguments to any other action; StartFreeGame and EndFreeGame switch Free mode from there."),
                Note("Per viewer, on the platform they played on (Streamer.bot's user variables): hm_wins, hm_losses, hm_gamesPlayed, hm_pointsEarned, hm_lettersFound, hm_wordsGuessed, hm_streak, hm_bestStreak.")),
            H("Where things are kept",
                Cmd("configs.json", "everything on these pages, in Chat Games\\Hangman Game."),
                Cmd("Hangman-Wordlist.json", "the word list, next to it; the Words page edits it."),
                Cmd("uiframework.layout.json", "your layout of this window: theme, language, renamed or hidden pages and fields."),
                Cmd("hangman_overlay.html", "the overlay, in the overlay folder, with hangman_state.json beside it."),
                Cmd("HM-log-<date>.txt", "the log, in Chat Games\\Logs.")))
        .About(credits: "nate1280 / Streamer.bot and YOU ♡",
            sections: new[] { H("Needs a points system", Note("Rounds pay out with the points of a currency system. These two are picked up without any setup:"), Link("Currency Core by StreamUP", "https://streamup.tips/product/currency-core"), Link("LOYALTY POINTS by tawmae", "https://tawmae.xyz/loyalty-points"), Note("Anything else works too - name its variable on the Access page. Twitch channel points can start a round, but a win always pays in that currency.")) },
            links: new[] { Link("Hangman Game on the Streamer.bot Discord", "https://discord.com/channels/834650675224248362/1448251872744837203/1448251872744837203"), Link("aaskjer on Twitch", "https://twitch.tv/aaskjer") })
        .Cooldowns(C("c49f751d-28cd-4c9e-87e9-6a29e843e0ae", "globalCooldown", "userCooldown"))   // !hangman
        // German: the game's own keys and texts; sections, pages and the shared keys come from the conventions.
        .Lang("de",
            Pg("automation", "Automatisierung"),
            Pg("words", "Wörter"),
            Tx("Ad breaks", "Werbepausen"),
            K("adBreakStart", "Runde starten, wenn Werbung startet", "Nur Twitch. Während Werbung läuft, sehen die Zuschauer den Stream nicht, aber der Chat funktioniert wie immer. Startet auf deinem Kanal ein Werbeblock, startet mit ihm eine Runde, ohne Abklingzeit und ohne Moderator. Eine laufende Runde bleibt unberührt. Das Spiel betrachtet den Werbeblock als beendet, sobald seine Länge um ist."),
            K("adBreakBlitz", "Als Blitz-Runde", "Die Runde zur Werbung ist eine Blitz-Runde: kurz genug für einen Werbeblock. Braucht 'Blitz-Runden' auf der Seite Spiel; sonst ist es eine normale Runde."),
            K("adBreakAnnounce", "Vor der Werbung ankündigen", "Eine Zeile im Chat vor dem Werbeblock, damit die Zuschauer wissen, dass mit ihm eine Runde kommt. Die Zeile steht auf der Seite Nachrichten."),
            K("adBreakAnnounceMinutes", "Minuten vorher", "Wie lange vor der Werbung die Zeile gesendet wird, 1 bis 5. Twitch meldet einen Werbeblock ab fünf Minuten vorher."),
            K("adBreakBonusPercent", "Bonus während der Werbung", "Wie viel mehr Buchstaben und der Gewinn einer Twitch-Runde zahlen, die während eines Werbeblocks gestartet wurde, in Prozent, egal wer sie gestartet hat. 0 für keinen."),
            K("adBreakOnly", "Runden nur während der Werbung", "Auf Twitch können Zuschauer eine Runde nur starten, während Werbung läuft; Moderatoren und der Broadcaster immer, ebenso der Timer und deine eigenen Actions. YouTube und Kick sind nicht betroffen."),
            K("msgAdBreakAnnouncement", "Werbung kommt", "Auf Twitch vor einem Werbeblock gesendet, wenn 'Vor der Werbung ankündigen' an ist. Leer lassen, um nichts zu sagen."),
            K("msgAdBreakOnly", "Nur während der Werbung", "Auf Twitch gesendet, wenn ein Zuschauer außerhalb eines Werbeblocks eine Runde starten will, während 'Runden nur während der Werbung' an ist."),
            Pg("board", "Rangliste"),
            K("maxWrongGuesses", "Leben (falsche Versuche)", "Wie viele falsche Versuche eine Runde erlaubt, bevor das letzte Leben weg ist und die Runde verloren ist."),
            K("roundDurationSeconds", "Rundendauer", "Wie lange eine Runde laufen darf, bevor das Wort aufgedeckt wird und sie endet. Praktisch in einem ruhigen Chat, damit eine Runde nie ewig läuft. 0 = keine Grenze."),
            K("timeReminderEnabled", "Vor dem Ende erinnern", "Postet kurz vor dem Ablauf der Runde eine Warnung mit dem maskierten Wort."),
            K("timeReminderSecondsLeft", "So viel Zeit vorher", "Wie lange vor dem Ablauf die Erinnerung rausgeht."),
            K("hintEnabled", "Hinweise nutzen", "Der zu jedem Wort gespeicherte Hinweis wird aufgedeckt, sobald der Chat genug falsche Versuche hat. Bis dahin bleibt er im Chat und auf dem Overlay verborgen."),
            K("hintAfterWrongGuesses", "Hinweis nach falschen Versuchen", "Wie viele falsche Versuche, bevor der Hinweis aufgedeckt wird (Wiederholungen zählen nicht). Muss unter den Leben bleiben, sonst endet die Runde, bevor der Hinweis kommt."),
            K("hintStopRevealPercent", "Kein Hinweis mehr ab", "Sobald dieser Anteil des Wortes schon aufgedeckt ist, gibt es den Hinweis nicht mehr."),
            K("blitzEnabled", "Blitzrunden", "Eine kurze, scharfe Runde: weniger Zeit, weniger Leben, und jeder Punkt zählt mehrfach. Eine Runde kann per Zufall zum Blitz werden, Mods rufen eine mit '!hangman blitz'. Das Overlay zeigt solange ein BLITZ-Abzeichen."),
            K("blitzChancePercent", "Blitz-Chance", "Chance, dass eine gestartete Runde zum Blitz wird, per Chat-Befehl wie per Kanalpunkte. 0 = Blitzrunden nur auf Befehl."),
            K("blitzCommandEnabled", "'!hangman blitz' erlauben", "Moderatoren und der Broadcaster erzwingen einen Blitz, indem sie 'blitz' an den Startbefehl hängen (!hangman blitz oder !hm blitz)."),
            K("blitzDurationSeconds", "Blitz-Dauer", "Wie lange eine Blitzrunde dauert. Gilt auch, wenn die Rundendauer 0 ist."),
            K("blitzMaxWrongGuesses", "Blitz-Leben", "Falsche Versuche, die eine Blitzrunde erlaubt, statt der Leben oben."),
            K("blitzPayoutMultiplier", "Blitz-Multiplikator", "Buchstabenbelohnungen und der Gewinn werden im Blitz damit multipliziert. Der Kanalpunkte-Einsatz nicht: er zahlt immer so zurück, wie auf der Seite Zugriff eingestellt."),
            K("evilEnabled", "Evil Hangman", "Hat der Chat einen Teil seiner Leben verbraucht, flackert das Overlay und das Wort wird gegen ein anderes getauscht. Alle Versuche werden gelöscht, bis auf einen zufällig gewählten, schon aufgedeckten Buchstaben: der wandert ins neue Wort. Höchstens einmal pro Runde."),
            K("evilTriggerPercent", "Glitch bei (Anteil der Leben)", "Der Tausch kommt mit dem falschen Versuch, der diesen Anteil der Leben verbraucht. 50 bei 6 Leben heißt beim 3. falschen Versuch. Muss unter 100 bleiben, sonst ist die Runde vorher verloren."),
            K("evilChancePercent", "Evil-Chance", "Chance, dass eine Runde überhaupt evil ist, beim Start gewürfelt. 100 = jede Runde, 25 = etwa jede vierte."),
            K("evilResetLives", "Leben beim Tausch auffüllen", "An: das neue Wort beginnt mit vollen Leben. Aus: verlorene Leben bleiben verloren, die wirklich böse Art."),
            K("evilSameCategory", "Gleiche Kategorie bevorzugen", "Tauscht zu einem Wort derselben Kategorie, wenn eines passt; sonst zu einer beliebigen Kategorie im Spiel."),
            K("evilGlitchSeconds", "Glitch-Dauer", "Wie lange das Overlay flackert: das alte Wort zerfällt, verwürfelt sich, das neue entschlüsselt sich, dann leuchtet der überlebende Buchstabe auf. Der Chat erfährt vom Tausch erst nach dem Glitch, Versuche werden solange ignoriert. 0 = ohne Effekt tauschen und sofort ansagen."),
            K("autoStartEnabled", "Runden automatisch starten", "Eine zeitgesteuerte Streamer.bot-Aktion startet nach ihrem eigenen Takt eine Runde, auf jeder Plattform, auf der du live bist und keine Runde läuft. Lässt sich nicht feststellen, ob du live bist, zählt ein Chat, der seit der letzten automatischen Runde etwas geschrieben hat. Dafür muss niemand Moderator sein, und keine Abklingzeit hält sie auf."),
            K("autoStartIntervalMinutes", "Alle (min)", "Wie lange zwischen automatischen Runden. Mit zufälliger Zeit die kürzeste Wartezeit."),
            K("autoStartMinChatLines", "Erst nach Chat-Zeilen", "Wie viele Zeilen der Chat einer Plattform seit ihrer letzten automatischen Runde geschrieben haben muss. Sind es zu wenige, wartet die Runde auf den nächsten Durchgang des Timers. 0 verlangt keine: live zu sein reicht dann."),
            K("autoStartRandom", "Zufällige Zeit", "Jede Wartezeit wird neu gezogen, zwischen Alle und Höchstens, sodass der Chat nicht weiß, wann die nächste Runde kommt."),
            K("autoStartIntervalMaxMinutes", "Höchstens (min)", "Die längste Wartezeit einer zufälligen Zeit."),
            K("rewardWinPoints", "Gewinn zahlt", "Der Grundbetrag, den ein Zuschauer für die vollständige richtige Antwort bekommt."),
            K("rewardPerLetterEnabled", "Belohnung je Buchstabe", "Belohnt einen Zuschauer für einen richtig geratenen Buchstaben."),
            K("rewardLetterPoints", "Punkte je Buchstabe", "Was ein einzelner richtiger Buchstabe zahlt."),
            K("scaleRewardByWord", "Schwere und lange Wörter zahlen mehr", "Der Bonus ist ein Prozentsatz auf 'Gewinn zahlt', vor den Rollen-Boni. Aus: jedes Wort zahlt genau 'Gewinn zahlt'."),
            K("difficultyBonusPercent", "Schwierigkeitsbonus", "Bonus je Schwierigkeitsstufe über 1. 25 heißt: ein Wort der Stufe 3 zahlt +50 %."),
            K("lengthBonusPercent", "Längenbonus", "Bonus je Buchstabe über der Schwelle darunter (Leerzeichen zählen nicht). 5 bei einer Schwelle von 4 heißt: ein Wort mit 9 Buchstaben zahlt +25 %."),
            K("lengthBonusFromLetters", "Längenbonus ab (Buchstaben)", "Wortlänge, ab der der Längenbonus zählt. Wörter bis zu dieser Länge bekommen keinen."),
            K("maxWordBonusPercent", "Höchster Wortbonus", "Obergrenze für Schwierigkeits- und Längenbonus zusammen, damit ein sehr langes schweres Wort nicht absurd zahlt. 0 = keine Grenze."),
            K("subscriberBoostEnabled", "Abonnenten-Bonus", "Gibt Twitch-Abonnenten einen größeren Gewinn, nach der Wortskalierung. Nur Twitch."),
            K("subscriberBoostPercent", "Abonnenten-Bonus", "Wie viel Abonnenten zusätzlich bekommen."),
            K("vipBoostEnabled", "VIP-Bonus", "Gibt Twitch-VIPs einen größeren Gewinn, nach der Wortskalierung. Nur Twitch."),
            K("vipBoostPercent", "VIP-Bonus", "Wie viel VIPs zusätzlich bekommen."),
            K("freeGame", "Freier Modus", "Jede Runde ist frei: Buchstaben und der Gewinn zahlen nichts, und der Chat bekommt die Nachrichten unter Freier Modus. !SetFreeHangman und !EndFreeHangman schalten ihn aus dem Chat (Moderatoren)."),
            K("minDifficulty", "Niedrigste Schwierigkeit", "Niedrigste Schwierigkeit, die das Spiel zieht. Wörter darunter kommen nie dran."),
            K("maxDifficulty", "Höchste Schwierigkeit", "Höchste Schwierigkeit, die das Spiel zieht. Wörter darüber kommen nie dran."),
            K("excludedCategories", "Ausgelassene Kategorien", "Kategorien, aus denen das Spiel nie zieht: eine so tippen, wie sie in der Liste unten steht, dann Enter. Alle anderen Kategorien sind im Spiel."),
            K("words", "Wörter", "Eine Zeile je Wort: das Wort, wie der Chat es raten muss, sein Hinweis, seine Kategorie und eine Schwierigkeit ab 1. 'Standardwerte wiederherstellen' holt die eingebaute Liste zurück; die Liste liegt in Hangman-Wordlist.json neben den Einstellungen."),
            K("openWordlistFolder", "Ordner der Wortliste öffnen", "Öffnet den Ordner mit Hangman-Wordlist.json im Explorer."),
            K("importSource", "Quelle", "Woher die Wörter kommen: Wikidata (Länder, Hauptstädte, Flüsse … oder jede Klasse per Q-ID), OdeNet (deutsche Substantive nach Thema), PokéAPI (Pokémon nach Generation) oder IGDB (Videospiele; braucht eine Twitch-Entwickler-App)."),
            K("importLanguage", "Sprache", "Sprache der Wörter und Hinweise, wo die Quelle beide hat (Wikidata, PokéAPI). OdeNet ist deutsch, IGDB englisch."),
            K("importTopic", "Thema", "Wikidata: Länder, Hauptstädte, Städte, Flüsse, Berge, Elemente, Instrumente, Sportarten, Hunde oder Eigene Klasse mit einer Q-ID darunter. OdeNet: Tiere, Pflanzen, Essen, Gegenstände, Personen, Körper, Orte, Stoffe, Natur, Gefühle."),
            K("importCustomClass", "Wikidata-Klasse (Q-ID)", "Für das Thema Eigene Klasse: die Q-ID der Klasse, deren Instanzen zu Wörtern werden, etwa Q11424 für Filme."),
            K("importGenerationFrom", "Ab Generation", "Erste Pokémon-Generation, die dabei ist, 1 bis 9."),
            K("importGenerationTo", "Bis Generation", "Letzte Pokémon-Generation, die dabei ist, 1 bis 9."),
            K("importIgdbClientId", "IGDB-Client-ID", "Client-ID deiner Twitch-Entwickler-App (dev.twitch.tv/console/apps). Nach dem ersten Import in Streamer.bots globalen Variablen abgelegt, also nur einmal zu tippen."),
            K("importIgdbClientSecret", "IGDB-Client-Secret", "Client-Secret dieser App; wird mit der ID nach dem ersten Import abgelegt."),
            K("importIgdbMinRatings", "Mindestens so viele Bewertungen auf IGDB", "Nur Spiele mit mindestens so vielen Bewertungen auf IGDB. Höher heißt bekanntere Titel."),
            K("importCategory", "Kategorie der importierten Wörter", "Jedes importierte Wort landet in dieser Kategorie; leer nimmt den Namen des Themas. Eine bestehende Kategorie nennen, um sie zu ergänzen."),
            K("importMaxCount", "Höchstens so viele Wörter", "Wie viele Wörter höchstens geholt werden."),
            K("importMaxLetters", "Höchstens so viele Buchstaben", "Längere Wörter werden übersprungen."),
            K("importMaxWords", "Höchstens so viele Wörter je Eintrag", "Einträge aus mehr Wörtern werden übersprungen."),
            K("importReplaceUmlauts", "Umlaute ersetzen", "Schreibt ae, oe, ue und ss für ä, ö, ü und ß, damit der Chat sie mit einfachen Buchstaben raten kann."),
            K("importFixedDifficulty", "Schwierigkeit für jedes importierte Wort", "0 wählt je Wort eine Schwierigkeit aus Länge und Bekanntheit."),
            K("importWords", "Jetzt importieren", "Holt die Wörter mit den Einstellungen oben aus der Quelle, überspringt die schon in der Liste und schreibt den Rest in Hangman-Wordlist.json. Fenster neu öffnen, um sie in der Liste oben zu sehen."),
            K("overlayEnabled", "Overlay", "Schreibt die Overlay-Dateien und hält sie aktuell. Das Overlay ist eine lokale HTML-Datei, die OBS als Browserquelle lädt; das Spiel schreibt den Rundenstand daneben und die Seite zeichnet sich selbst neu. Aus: gar keine Overlay-Dateien."),
            K("overlayFolder", "Overlay-Ordner", "Wo hangman_overlay.html liegt: ein einfacher Name wie 'Overlay' ist ein Unterordner von Chat Games\\Hangman Game, ein voller Pfad legt es irgendwohin. Der Ordner wird angelegt, wenn er fehlt; eine Änderung schreibt dort eine frische Overlay-Datei, also die OBS-Browserquelle danach umbiegen."),
            K("openOverlayFolder", "Overlay-Ordner öffnen", "Öffnet den Overlay-Ordner im Explorer."),
            K("overlayFigureMode", "Figur", "Proportional zeichnet die klassische Figur aus 6 Teilen, verteilt auf so viele Leben, wie du erlaubst, sodass das letzte Leben sie immer vollendet. Klassisch + Galgen nimmt die 4 Galgenteile als weitere Schritte dazu, passend zu mehr Leben. Auto: proportional bis 6 Leben, darüber mit Galgen."),
            K("overlayScalePercent", "Overlay-Größe", "Skaliert das ganze Overlay. 100 = wie entworfen, 150 = anderthalbfach, 50 = halb so groß."),
            K("overlayFontScalePercent", "Textgröße", "Skaliert nur den Text, die Figur bleibt."),
            K("overlayGallowsColour", "Galgenfarbe", "Farbe des Galgens: Sockel, Pfosten, Balken und Seil. Dieselbe wie die Figurfarbe ergibt den klassischen einfarbigen Look."),
            K("overlayFigureColour", "Figurfarbe", "Farbe des Körpers: Kopf, Rumpf, Arme und Beine."),
            K("overlayTextColour", "Textfarbe", "Farbe des Wortes und der Werte."),
            K("overlayAccentColour", "Akzentfarbe", "Farbe der Kategorie und der kleinen Beschriftungen."),
            K("overlayBackgroundColour", "Hintergrundfarbe", "Farbe der Fläche hinter dem Overlay. Erst sichtbar, wenn die Deckkraft über 0 ist."),
            K("overlayBackgroundOpacity", "Deckkraft des Hintergrunds", "0 = ganz durchsichtig (das Beste für Overlays), 100 = deckend."),
            K("overlayEndFadeSeconds", "Letztes Wort stehen lassen für", "Wie lange das letzte Wort nach einer Runde stehen bleibt, bevor es ausblendet. 0 = sofort weg."),
            K("overlayShowCategory", "Kategorie", "Zeigt die Kategorie des Wortes über dem Rätsel."),
            K("overlayShowLives", "Leben", "Zeigt die verbleibenden Leben als Herzen."),
            K("overlayShowHint", "Hinweis", "Zeigt den Hinweis auf dem Overlay, sobald das Spiel ihn im Chat verrät."),
            K("overlayShowGuessed", "Geratene Buchstaben", "Zeigt jeden bisher geratenen Buchstaben."),
            K("overlayShowWrong", "Falsche Buchstaben", "Zeigt die falschen Buchstaben, durchgestrichen."),
            K("overlayShowWrongWords", "Falsche Wörter", "Zeigt ganze Wörter, die der Chat falsch geraten hat, durchgestrichen."),
            K("obsConnectionId", "OBS-Verbindung", "Welche OBS-Verbindung in Streamer.bot (Stream Apps > OBS Studio) genommen wird; die erste ist 0."),
            K("obsSceneName", "Szene", "Die Szene, in die die Browserquelle kommt; wird angelegt, wenn es sie nicht gibt."),
            K("obsSourceName", "Quelle", "Name der Browserquelle, die angelegt wird."),
            K("setupObsOverlay", "Browserquelle in OBS anlegen", "Legt die Szene oben an, wenn sie fehlt, und fügt eine Browserquelle hinzu, die auf die Overlay-Datei zeigt. Braucht OBS verbunden in Streamer.bot. Geht auch von Hand: Browserquelle > Lokale Datei > hangman_overlay.html aus dem Ordner oben."),
            K("refreshObsOverlay", "Overlay in OBS neu laden", "Lädt jede OBS-Browserquelle neu, die hangman_overlay.html zeigt, damit OBS das neueste Overlay nimmt. Das Spiel macht das nach einem Update auch selbst, sobald OBS verbunden ist."),
            K("overlaySample", "Beispielrunde zeigen", "Spielt eine erfundene Runde auf dem offenen Overlay, dem in OBS eingeschlossen, damit du siehst, wo es sitzt und wie es aussieht, mit einem Evil-Hangman-Tausch und einem Sieg. Bist du live, sehen es deine Zuschauer auch. Nimmt die gespeicherten Einstellungen; nicht, solange eine echte Runde läuft."),
            K("overlayDemo", "Demo ansehen", "Öffnet das Overlay im Browser und spielt erfundene Runden, wie die gespeicherten Einstellungen es sagen, ohne Streamer.bot. In OBS tut &demo=1 am Ende der Adresse der Quelle dasselbe, um sie zu platzieren und ihre Größe einzustellen."),
            K("requireModToStart", "Nur Moderatoren starten eine Runde", "Nur Moderatoren und der Broadcaster starten eine Runde; raten darf in einer laufenden Runde weiter jeder."),
            K("allowChannelPointStart", "Eine Belohnung startet die Runde", "Auf Twitch startet eine Kanalpunkte-Einlösung die Runde statt des Chat-Befehls, der auf Twitch dann nur noch Moderatoren startet. Raten darf weiter jeder wie gewohnt."),
            K("channelPointRewardId", "Belohnungs-ID", "Die ID der Belohnung. Zu finden unter ayrtonalexis.com/reward_checker: autorisieren, 'Get Rewards', die ID deiner Belohnung kopieren."),
            K("channelPointFixedBet", "Fester Einsatz", "0 liest den Einsatz aus der Einlösung: eine getippte Zahl, sonst der Preis der Belohnung, sonst kein Einsatz. Über 0 setzt jede Einlösung genau so viel."),
            K("channelPointPayoutMultiplier", "Multiplikator des Einsatzes", "Streamer.bot kann keine Kanalpunkte auszahlen, ein Gewinn zahlt also immer in deiner Währung: der Einsatz mal dieser Wert, auf den Gewinn drauf. 1 zahlt ihn einmal zurück, 2 verdoppelt ihn, 0 zahlt keinen Einsatzbonus."),
            K("refundFailedRedemptions", "Einlösungen erstatten, die keine Runde starten", "Kann die Runde nicht starten (läuft schon, kein Wort, ungültiger Einsatz), wird die Einlösung abgebrochen und die Kanalpunkte gehen zurück. Geht nur, solange die Einlösung noch in der Warteschlange steht."),
            K("antiCheatMaxGuesses", "Versuche je Zeitfenster", "Wie viele Versuche ein Zuschauer im Zeitfenster darunter machen darf; Broadcaster sind ausgenommen. 0 = keine Grenze."),
            K("antiCheatWindowSeconds", "Zeitfenster", "Der Zeitraum, über den die Versuche gezählt werden."),
            K("addictedEnabled", "Süchtige behandeln", "Die Stammgäste, die jede Runde spielen, spielen weiter, bekommen aber weniger und müssen ihre Versuche strecken, damit Neue zum Zug kommen."),
            K("addictedPointsPenaltyPercent", "Punktabzug", "Wie viel von ihren Gewinnen abgezogen wird. 50 heißt, sie bekommen die Hälfte."),
            K("addictedGuessIntervalSeconds", "Pause zwischen Versuchen", "Mindestzeit, die ein süchtiger Zuschauer zwischen seinen Versuchen warten muss."),
            K("logToFile", "Protokoll je Tag schreiben", "Jede Runde, jeder Versuch, jede Auszahlung und Ablehnung landet in Chat Games\\Logs\\HM-log-<Datum>.txt, das die Seite Protokoll liest."),
            K("scoreboardTopN", "Plätze auf der Rangliste", "Wie viele Zuschauer die Rangliste zeigt, 1 bis 25."),
            K("broadcasterAllowed", "Broadcaster zeigen", "Zeigt dich, den Broadcaster, auf der Rangliste."),
            K("botAllowed", "Bot zeigen", "Zeigt dein Bot-Konto auf der Rangliste."),
            K("blockedFromBoard", "Von der Rangliste ausgeblendet", "Zuschauer, die auf der Rangliste nicht erscheinen, aber spielen dürfen; praktisch, wenn andere Bots mitspielen."),
            K("streaksEnabled", "Serien zählen", "Jedes gelöste Wort zählt eins auf die Serie eines Zuschauers; sie reißt erst, wenn eine Runde verloren geht, in der er geraten hat. Eine gestoppte Runde reißt nichts. Serien werden im Chat angesagt, stehen in den persönlichen Werten, und der automatische Reset löscht auch sie."),
            K("streakAnnounceFrom", "Ansagen ab", "Serien mindestens dieser Länge werden nach einem Gewinn gefeiert und beim Reißen genannt. Nie unter 2."),
            K("streakBonusPercent", "Serienbonus", "Kommt für jeden Gewinn in Folge nach dem ersten auf den Gewinn drauf. 10 heißt: der 3. Gewinn in Folge zahlt +20 %. 0 = kein Bonus."),
            K("streakBonusMaxPercent", "Höchster Serienbonus", "Obergrenze für den Serienbonus. 0 = keine Grenze."),
            K("resetAllMonthly", "Alle Plattformen monatlich zurücksetzen", "Leert die Ranglisten aller Plattformen am Monatsende."),
            K("resetTwitchMonthly", "Twitch: monatlich zurücksetzen", "Leert die Twitch-Rangliste zu Beginn jedes Monats."),
            K("resetTwitchDay", "Twitch: zurücksetzen am Tag", "Leert die Twitch-Rangliste jeden Monat an diesem Tag (in kürzeren Monaten am letzten). 0 = am 1., solange monatlich an ist, sonst nie."),
            K("resetYouTubeMonthly", "YouTube: monatlich zurücksetzen", "Leert die YouTube-Rangliste zu Beginn jedes Monats."),
            K("resetYouTubeDay", "YouTube: zurücksetzen am Tag", "Leert die YouTube-Rangliste jeden Monat an diesem Tag (in kürzeren Monaten am letzten). 0 = am 1., solange monatlich an ist, sonst nie."),
            K("resetKickMonthly", "Kick: monatlich zurücksetzen", "Leert die Kick-Rangliste zu Beginn jedes Monats."),
            K("resetKickDay", "Kick: zurücksetzen am Tag", "Leert die Kick-Rangliste jeden Monat an diesem Tag (in kürzeren Monaten am letzten). 0 = am 1., solange monatlich an ist, sonst nie."),
            K("msgGameStart", "Rundenstart", "Wenn eine Runde beginnt."),
            K("msgAlreadyRunning", "Läuft schon", "Wenn jemand starten will, während eine Runde läuft."),
            K("msgNoGameActive", "Keine Runde aktiv", "Wenn jemand rät, obwohl keine Runde läuft."),
            K("msgRepeat", "Wiederholung", "Wenn ein Zuschauer mit dem Wiederholungsbefehl nach dem Stand fragt."),
            K("msgTimeReminder", "Zeiterinnerung", "Kurz bevor die Runde abläuft."),
            K("msgCorrectLetter", "Richtiger Buchstabe", "Wenn ein geratener Buchstabe im Wort ist."),
            K("msgCorrectWord", "Richtiges Wort", "Wenn ein Zuschauer das ganze Wort löst."),
            K("msgLetterReward", "Buchstabenbelohnung", "Wenn ein Zuschauer Punkte für einen aufgedeckten Buchstaben bekommt."),
            K("msgAlreadyGuessed", "Schon geraten", "Wenn ein Buchstabe schon geraten war."),
            K("msgInvalidGuess", "Ungültiger Versuch", "Wenn der Versuch nicht verstanden wurde."),
            K("msgWrongLetter", "Falscher Buchstabe", "Wenn ein geratener Buchstabe nicht im Wort ist."),
            K("msgWrongWord", "Falsches Wort", "Bei einem falschen ganzen Wort, das zwei Leben kostet."),
            K("msgWrongPartial", "Falsches Teilwort", "Bei einem falschen Teil einer mehrteiligen Antwort, der ein Leben kostet."),
            K("msgDuplicateWrong", "Wiederholter Fehlversuch", "Wenn derselbe Zuschauer einen schon falschen Versuch wiederholt."),
            K("msgGameOver", "Game over", "Wenn dem Chat die Leben ausgehen."),
            K("msgGameTimeout", "Zeit abgelaufen", "Wenn die Rundendauer abläuft, ohne dass jemand löst."),
            K("msgGameStopped", "Gestoppt", "Wenn ein Moderator die Runde beendet."),
            K("msgHint", "Hinweis", "Wenn der Hinweis aufgedeckt wird."),
            K("msgBlitzStart", "Blitzstart", "Statt der normalen Startnachricht, wenn eine Runde ein Blitz ist."),
            K("msgBlitzWin", "Blitzgewinn", "Statt 'Richtiges Wort', wenn eine Blitzrunde gelöst wird."),
            K("msgEvilSwap", "Wort getauscht", "Nach dem Overlay-Glitch, wenn ein aufgedeckter Buchstabe den Tausch überlebt hat."),
            K("msgEvilSwapNoLetter", "Wort getauscht, kein Buchstabe", "Nach dem Overlay-Glitch, wenn der Chat vor dem Tausch keinen Buchstaben aufgedeckt hatte."),
            K("msgStreak", "Serie", "Nach einem Gewinn, sobald die Serie 'Ansagen ab' erreicht."),
            K("msgStreakLost", "Serie gerissen", "Wenn eine verlorene Runde Serien ab 'Ansagen ab' beendet."),
            K("msgPersonalStreak", "Persönliche Serie", "Zusatzzeile unter den persönlichen Werten, solange Serien gezählt werden."),
            K("msgModOnly", "Nur Mods", "Wenn ein Zuschauer eine Runde starten will, obwohl nur Moderatoren dürfen."),
            K("msgNotAllowed", "Nicht erlaubt", "Wenn ein gesperrter Zuschauer mitmachen will."),
            K("msgAntiCheat", "Zu schnell", "Wenn ein Zuschauer gebremst wird, weil er zu schnell rät."),
            K("globalCooldownMessage", "Globale Abklingzeit", "Wenn jemand starten will, während die globale Abklingzeit noch läuft."),
            K("userCooldownMessage", "Abklingzeit je Zuschauer", "Wenn ein Zuschauer starten will, während seine eigene Abklingzeit noch läuft."),
            K("msgAddictedSlowdown", "Langsamer", "Wenn ein süchtiger Zuschauer vor Ablauf seiner Pause wieder rät."),
            K("msgAddictedAdded", "Hinzugefügt", "Nach einem erfolgreichen !addicted add."),
            K("msgAddictedRemoved", "Entfernt", "Nach einem erfolgreichen !addicted remove."),
            K("msgAddictedAlreadyOnList", "Schon auf der Liste", "Wenn jemand hinzugefügt wird, der schon auf der Liste steht."),
            K("msgAddictedNotOnList", "Nicht auf der Liste", "Wenn jemand entfernt wird, der nicht auf der Liste steht."),
            K("msgAddictedUsage", "Benutzung", "Wenn der Befehl !addicted falsch getippt wurde."),
            K("msgChannelPointStart", "Start per Einlösung", "Wenn eine Runde aus einer Kanalpunkte-Einlösung startet."),
            K("msgChannelPointBusy", "Einlösung bei laufender Runde", "Wenn eine Einlösung kommt, während eine Runde läuft."),
            K("msgChannelPointBadInput", "Einlösung mit falscher Eingabe", "Wenn der Zuschauer etwas in die Belohnung getippt hat, das keine positive Zahl ist."),
            K("msgRedemptionRefunded", "Einlösung erstattet", "Nachdem eine Einlösung erstattet wurde."),
            K("msgScoreboardHeader", "Kopfzeile der Rangliste", "Erste Zeile der Rangliste."),
            K("msgScoreboardEntry", "Eintrag der Rangliste", "Einmal je Zuschauer auf der Rangliste."),
            K("msgScoreboardEmpty", "Rangliste leer", "Wenn es noch keine Ranglistendaten gibt."),
            K("msgPersonalStats", "Persönliche Werte", "Wenn ein Zuschauer seine eigenen Werte abfragt."),
            K("msgNoStats", "Keine Werte", "Wenn der Zuschauer noch keine Werte hat."),
            K("msgUserNotFound", "Zuschauer nicht gefunden", "Wenn ein gesuchter Zuschauer nicht existiert."),
            K("msgMonthlyReset", "Reset-Ansage", "Wenn eine Rangliste zurückgesetzt wird."),
            K("freeGameStartMessage", "Gestartet", "Einmal gesendet, wenn !SetFreeHangman den freien Modus einschaltet, in den Chat, aus dem der Befehl kam. Anders eingeschaltet, etwa per Execute C# Method auf StartFreeGame in einer eigenen Action, geht sie an jede Plattform mit einem Konto in Streamer.bot - an YouTube nur, solange eine Übertragung beobachtet wird."),
            K("freeGameEndMessage", "Beendet", "Einmal gesendet, wenn !EndFreeHangman den freien Modus ausschaltet, in den Chat, aus dem der Befehl kam. Anders ausgeschaltet, etwa per Execute C# Method auf EndFreeGame, geht sie an jede Plattform mit einem Konto in Streamer.bot - an YouTube nur, solange eine Übertragung beobachtet wird."),
            Tx("Round", "Runde"),
            Tx("Time reminder", "Zeiterinnerung"),
            Tx("Hints", "Hinweise"),
            Tx("Blitz round", "Blitzrunde"),
            Tx("Evil Hangman", "Evil Hangman"),
            Tx("Word scaling", "Wortskalierung"),
            Tx("Role boosts", "Rollen-Boni"),
            Tx("Free mode", "Freier Modus"),
            Tx("Word selection", "Wortauswahl"),
            Tx("Word list", "Wortliste"),
            Tx("Import words", "Wörter importieren"),
            Tx("Overlay file", "Overlay-Datei"),
            Tx("Hangman figure", "Hangman-Figur"),
            Tx("Appearance", "Aussehen"),
            Tx("Show on overlay", "Auf dem Overlay zeigen"),
            Tx("Who can start a round", "Wer eine Runde startet"),
            Tx("Rate limit", "Tempolimit"),
            Tx("Behaviour", "Verhalten"),
            Tx("Streaks", "Serien"),
            Tx("Auto reset", "Automatischer Reset"),
            Tx("Guessing", "Raten"),
            Tx("Wrong guesses", "Fehlversuche"),
            Tx("Ending", "Ende"),
            Tx("Blitz", "Blitz"),
            Tx("Overlay on", "Overlay an"),
            Tx("Overlay off", "Overlay aus"),
            Tx("Mods start", "Mods starten"),
            Tx("Anyone starts", "Jeder startet"),
            Tx("Wikidata", "Wikidata"),
            Tx("OdeNet (German nouns)", "OdeNet (deutsche Substantive)"),
            Tx("PokéAPI (Pokémon)", "PokéAPI (Pokémon)"),
            Tx("IGDB (video games)", "IGDB (Videospiele)"),
            Tx("German", "Deutsch"),
            Tx("English", "Englisch"),
            Tx("Countries (Wikidata)", "Länder (Wikidata)"),
            Tx("Capitals (Wikidata)", "Hauptstädte (Wikidata)"),
            Tx("Big cities (Wikidata)", "Großstädte (Wikidata)"),
            Tx("Rivers (Wikidata)", "Flüsse (Wikidata)"),
            Tx("Mountains (Wikidata)", "Berge (Wikidata)"),
            Tx("Chemical elements (Wikidata)", "Chemische Elemente (Wikidata)"),
            Tx("Musical instruments (Wikidata)", "Musikinstrumente (Wikidata)"),
            Tx("Sports (Wikidata)", "Sportarten (Wikidata)"),
            Tx("Dog breeds (Wikidata)", "Hunderassen (Wikidata)"),
            Tx("Custom class (Wikidata Q-ID)", "Eigene Klasse (Wikidata-Q-ID)"),
            Tx("Animals (OdeNet)", "Tiere (OdeNet)"),
            Tx("Plants (OdeNet)", "Pflanzen (OdeNet)"),
            Tx("Food & drink (OdeNet)", "Essen & Trinken (OdeNet)"),
            Tx("Objects & tools (OdeNet)", "Gegenstände (OdeNet)"),
            Tx("People & jobs (OdeNet)", "Personen & Berufe (OdeNet)"),
            Tx("Body (OdeNet)", "Körper (OdeNet)"),
            Tx("Places (OdeNet)", "Orte (OdeNet)"),
            Tx("Materials (OdeNet)", "Stoffe & Materialien (OdeNet)"),
            Tx("Nature (OdeNet)", "Natur (OdeNet)"),
            Tx("Feelings (OdeNet)", "Gefühle (OdeNet)"),
            Tx("Auto: proportional up to 6 lives, gallows above", "Auto: proportional bis 6 Leben, darüber Galgen"),
            Tx("Proportional: always 6 parts", "Proportional: immer 6 Teile"),
            Tx("Classic + gallows: always 10 parts", "Klassisch + Galgen: immer 10 Teile"),
            Tx("Overlay, from the Overlay page. Off: the game writes no overlay files.", "Overlay, von der Seite Overlay. Aus: das Spiel schreibt keine Overlay-Dateien."),
            Tx("Free mode, from the Game page or !SetFreeHangman. While it is on, letters and the win pay nothing.", "Freier Modus, von der Seite Spiel oder !SetFreeHangman. Solange er an ist, zahlen Buchstaben und der Gewinn nichts."),
            Tx("Who starts a round, from the Access page. Everyone can always guess.", "Wer eine Runde startet, von der Seite Zugriff. Raten darf immer jeder."),
            Tx("No rounds logged yet. Every round, guess and payout lands here once 'Write a log file per day' is on.", "Noch keine Runden protokolliert. Jede Runde, jeder Versuch und jede Auszahlung landet hier, sobald 'Protokoll je Tag schreiben' an ist."))
        // The tour on the window's first open (the ? in the header shows it again); the framework brings the texts.
        .Tour(Tip("start"), Tip("rail"), Tip("save"), Tip("customise"), Tip("end"));

    // The built-in word list, as the game writes it when Hangman-Wordlist.json is missing and as the Words page
    // restores it: Word, Hint, Category, Difficulty.
    public static readonly string[] DefaultWords =
    {
        "{\"Word\":\"streamer\",\"Hint\":\"Someone who broadcasts live\",\"Category\":\"Streaming\",\"Difficulty\":1}",
        "{\"Word\":\"emote\",\"Hint\":\"Tiny chat reaction image\",\"Category\":\"Twitch\",\"Difficulty\":1}",
        "{\"Word\":\"raid\",\"Hint\":\"Sending your viewers to another channel\",\"Category\":\"Twitch\",\"Difficulty\":1}",
        "{\"Word\":\"clip\",\"Hint\":\"Short recorded moment from a stream\",\"Category\":\"Twitch\",\"Difficulty\":1}",
        "{\"Word\":\"vod\",\"Hint\":\"Recorded version of a past stream\",\"Category\":\"Twitch\",\"Difficulty\":1}",
        "{\"Word\":\"overlay\",\"Hint\":\"Graphics shown on top of a stream\",\"Category\":\"Streaming\",\"Difficulty\":2}",
        "{\"Word\":\"moderator\",\"Hint\":\"Keeps chat clean\",\"Category\":\"Twitch\",\"Difficulty\":2}",
        "{\"Word\":\"broadcast\",\"Hint\":\"Sending out a live stream signal\",\"Category\":\"Streaming\",\"Difficulty\":2}",
        "{\"Word\":\"subscriber\",\"Hint\":\"Paying supporter of a channel\",\"Category\":\"Streaming\",\"Difficulty\":2}",
        "{\"Word\":\"hypetrain\",\"Hint\":\"Twitch engagement event in chat\",\"Category\":\"Twitch\",\"Difficulty\":2}",
        "{\"Word\":\"streamer bot\",\"Hint\":\"Best software in the world\",\"Category\":\"Streaming\",\"Difficulty\":2}",
        "{\"Word\":\"latency\",\"Hint\":\"Delay between action and reaction\",\"Category\":\"Tech\",\"Difficulty\":2}",
        "{\"Word\":\"algorithm\",\"Hint\":\"Platform decides who sees you with this\",\"Category\":\"Tech\",\"Difficulty\":3}",
        "{\"Word\":\"bandwidth\",\"Hint\":\"How much data you can push per second\",\"Category\":\"Tech\",\"Difficulty\":3}",
        "{\"Word\":\"prediction\",\"Hint\":\"Chat bets on an outcome with points\",\"Category\":\"Twitch\",\"Difficulty\":3}"
    };

    public static JArray DefaultWordsArray()
    {
        var rows = new JArray();
        foreach (var json in DefaultWords) rows.Add(JObject.Parse(json));
        return rows;
    }

    // ---- The table: every setting the host reads, in page order. ----
    public static readonly Setting[] Table =
    {
        // ---- game: the round ----
        S("maxWrongGuesses", 6, page: "game", label: "Lives (wrong guesses)", help: "How many wrong guesses a round allows before the last life is gone and the round is lost.", section: "Round", min: 1.0, aliases: new[] { "MaxWrongGuesses" }),
        S("roundDurationSeconds", 300, page: "game", label: "Round duration", help: "How long a round may run before the word is revealed and it ends. Useful in a quiet chat, so a round never runs forever. 0 = no limit.", kind: "duration", section: "Round", min: 0.0, unlimitedAtZero: true, aliases: new[] { "RoundDurationSeconds" }),
        S("timeReminderEnabled", true, page: "game", label: "Remind before the round ends", help: "Posts a warning with the masked word shortly before the round times out.", section: "Time reminder", aliases: new[] { "EnableTimeReminder" }),
        S("timeReminderSecondsLeft", 30, page: "game", label: "With this much time left", help: "How long before the timeout the reminder goes out.", kind: "duration", section: "Time reminder", min: 0.0, requires: "timeReminderEnabled", aliases: new[] { "TimeReminderSecondsLeft" }),
        // ---- game: hints ----
        S("hintEnabled", true, page: "game", label: "Use hints", help: "The hint stored with each word is revealed once chat has enough wrong guesses. Until then it stays hidden in chat and on the overlay.", section: "Hints", aliases: new[] { "HintEnabled" }),
        S("hintAfterWrongGuesses", 3, page: "game", label: "Hint after wrong guesses", help: "How many wrong guesses before the hint is revealed (repeats don't count). Must stay below the lives, or the round ends before the hint fires.", section: "Hints", min: 0.0, requires: "hintEnabled", aliases: new[] { "HintAfterWrongGuesses" }),
        S("hintStopRevealPercent", 75.0, page: "game", label: "No hint once this much is revealed", help: "Once this share of the word is already revealed, the hint is no longer given.", kind: "percent", section: "Hints", requires: "hintEnabled", aliases: new[] { "HintStopRevealPercent" }),
        // ---- game: blitz ----
        S("blitzEnabled", false, page: "game", label: "Blitz rounds", help: "A short, sharp round: less time, fewer lives, and every point it pays is multiplied. A round can turn into a blitz by chance, and mods can call one with '!hangman blitz'. The overlay shows a BLITZ badge while it runs.", section: "Blitz round", aliases: new[] { "BlitzEnabled" }),
        S("blitzChancePercent", 10.0, page: "game", label: "Blitz chance", help: "Chance that any round started turns into a blitz, chat command and channel points alike. 0 = blitz rounds only on command.", kind: "percent", section: "Blitz round", requires: "blitzEnabled", aliases: new[] { "BlitzChancePercent" }),
        S("blitzCommandEnabled", true, page: "game", label: "Allow '!hangman blitz'", help: "Moderators and the broadcaster can force a blitz by adding 'blitz' to the start command (!hangman blitz or !hm blitz).", section: "Blitz round", requires: "blitzEnabled", aliases: new[] { "BlitzCommandEnabled" }),
        S("blitzDurationSeconds", 60, page: "game", label: "Blitz duration", help: "How long a blitz round lasts. Applies even when the round duration is 0.", kind: "duration", section: "Blitz round", min: 1.0, requires: "blitzEnabled", aliases: new[] { "BlitzDurationSeconds" }),
        S("blitzMaxWrongGuesses", 3, page: "game", label: "Blitz lives", help: "Wrong guesses a blitz round allows, in place of the lives above.", section: "Blitz round", min: 1.0, requires: "blitzEnabled", aliases: new[] { "BlitzMaxWrongGuesses" }),
        S("blitzPayoutMultiplier", 2.0, page: "game", label: "Blitz payout multiplier", help: "Letter rewards and the win payout are multiplied by this during a blitz. The channel point stake is not: it always pays back as set on the Access page.", kind: "double", section: "Blitz round", min: 0.0, requires: "blitzEnabled", aliases: new[] { "BlitzPayoutMultiplier" }),
        // ---- game: evil hangman ----
        S("evilEnabled", false, page: "game", label: "Evil Hangman", help: "Once chat has burned through part of its lives, the overlay glitches and the word is swapped for another one. Every guess is wiped, except one randomly picked letter that was already revealed: it carries over into the new word. Happens at most once per round.", section: "Evil Hangman", aliases: new[] { "EvilEnabled" }),
        S("evilTriggerPercent", 50.0, page: "game", label: "Glitch at (share of the lives)", help: "The swap fires on the wrong guess that uses up this share of the round's lives. 50 with 6 lives fires on the 3rd wrong guess. Must stay below 100, or the round is lost first.", kind: "percent", section: "Evil Hangman", requires: "evilEnabled", aliases: new[] { "EvilTriggerPercent" }),
        S("evilChancePercent", 100.0, page: "game", label: "Evil chance", help: "Chance that a round is evil at all, rolled when the round starts. 100 = every round, 25 = about one in four.", kind: "percent", section: "Evil Hangman", requires: "evilEnabled", aliases: new[] { "EvilChancePercent" }),
        S("evilResetLives", false, page: "game", label: "Refill the lives on a swap", help: "On: the new word starts with full lives. Off: lives already lost stay lost, the truly evil way.", section: "Evil Hangman", requires: "evilEnabled", aliases: new[] { "EvilResetLives" }),
        S("evilSameCategory", true, page: "game", label: "Prefer the same category", help: "Swap to a word from the same category when one fits; any category in play when none does.", section: "Evil Hangman", requires: "evilEnabled", aliases: new[] { "EvilSameCategory" }),
        S("evilGlitchSeconds", 3, page: "game", label: "Glitch duration", help: "How long the overlay glitches: the old word breaks up, scrambles and the new one decodes, then the surviving letter lights up. Chat only hears about the swap once the glitch has played out, and guesses are ignored until then. 0 = swap without the effect and announce it at once.", kind: "duration", section: "Evil Hangman", min: 0.0, requires: "evilEnabled", aliases: new[] { "EvilGlitchSeconds" }),
        // ---- automation: rounds that start themselves ----
        S("autoStartEnabled", false, page: "automation", label: "Start rounds automatically", help: "A Streamer.bot timed action starts a round on its own schedule, on every platform you are live on where no round is running. Where it cannot be told whether you are live, chat that has written something since the last automatic round stands in. Nobody has to be a moderator for it, and no cooldown holds it back.", section: "Rounds that start themselves", aliases: new[] { "AutoStartEnabled" }),
        S("autoStartIntervalMinutes", 15, page: "automation", label: "Every (min)", help: "How long between automatic rounds. With a random time, the shortest wait.", section: "Rounds that start themselves", min: 1.0, max: 1440.0, unit: "min", requires: "autoStartEnabled", row: "auto", aliases: new[] { "AutoStartIntervalMinutes" }),
        S("autoStartMinChatLines", 10, page: "automation", label: "Only after chat lines", help: "How many lines a platform's chat must have written since its last automatic round. Too few, and the round waits for the next turn of the timer. 0 asks for none: being live is then enough.", section: "Rounds that start themselves", min: 0.0, requires: "autoStartEnabled", row: "auto", aliases: new[] { "AutoStartMinChatLines" }),
        S("autoStartRandom", false, page: "automation", label: "Random time", help: "Each wait is drawn anew, between Every and At most, so chat cannot tell when the next round comes.", section: "Rounds that start themselves", requires: "autoStartEnabled", aliases: new[] { "AutoStartRandom" }),
        S("autoStartIntervalMaxMinutes", 30, page: "automation", label: "At most (min)", help: "The longest wait of a random time.", section: "Rounds that start themselves", min: 1.0, max: 1440.0, unit: "min", requires: "autoStartRandom", aliases: new[] { "AutoStartIntervalMaxMinutes" }),
        // ---- automation: Twitch ad breaks ----
        S("adBreakStart", false, page: "automation", label: "Start a round when ads start", help: "Twitch only. While ads run, viewers cannot see the stream, but chat works as ever. When an ad break starts on your channel, a round starts with it, with no cooldown and no moderator. A round already running is left alone. The game takes the break as over once its length has passed.", section: "Ad breaks", aliases: new[] { "AdBreakStart" }),
        S("adBreakBlitz", true, page: "automation", label: "Make it a blitz round", help: "The round for the ads is a blitz round: short enough for an ad break. Needs 'Blitz rounds' on the Game page; otherwise it is a normal round.", section: "Ad breaks", requires: "adBreakStart", aliases: new[] { "AdBreakBlitz" }),
        S("adBreakAnnounce", true, page: "automation", label: "Announce it before the ads", help: "A line in chat ahead of the ad break, so viewers know a round comes with it. The line is on the Messages page.", section: "Ad breaks", requires: "adBreakStart", row: "adAnnounce", aliases: new[] { "AdBreakAnnounce" }),
        S("adBreakAnnounceMinutes", 1, page: "automation", label: "Minutes before", help: "How long before the ads the line is sent, 1 to 5. Twitch tells of an ad break from five minutes ahead.", section: "Ad breaks", min: 1.0, max: 5.0, unit: "min", requires: "adBreakStart && adBreakAnnounce", row: "adAnnounce", aliases: new[] { "AdBreakAnnounceMinutes" }),
        S("adBreakBonusPercent", 0.0, page: "automation", label: "Bonus during ads", help: "How much more the letters and the win of a Twitch round started during an ad break pay, as a percentage, whoever started it. 0 for none.", kind: "percent", section: "Ad breaks", min: 0.0, max: 1000.0, aliases: new[] { "AdBreakBonusPercent" }),
        S("adBreakOnly", false, page: "automation", label: "Rounds only during ads", help: "On Twitch, viewers can start a round only while ads run; moderators and the broadcaster always can, and so can the timer and your own actions. YouTube and Kick are not affected.", section: "Ad breaks", aliases: new[] { "AdBreakOnly" }),
        // ---- game: payouts ----
        S("rewardWinPoints", 500, page: "game", label: "Win pays", help: "The base amount a viewer gets for the full correct answer.", section: "Payouts", min: 0.0, aliases: new[] { "RewardWinPoints" }),
        S("rewardPerLetterEnabled", true, page: "game", label: "Reward per letter", help: "Rewards a viewer for guessing a single letter correctly.", section: "Payouts", aliases: new[] { "EnableRewardPerLetter" }),
        S("rewardLetterPoints", 50, page: "game", label: "Points per letter", help: "What a single correct letter pays.", section: "Payouts", min: 0.0, requires: "rewardPerLetterEnabled", aliases: new[] { "RewardLetterPts" }),
        S("scaleRewardByWord", true, page: "game", label: "Harder and longer words pay more", help: "The bonus is a percentage added on top of 'Win pays' before role boosts. Off: every word pays exactly 'Win pays'.", section: "Word scaling", aliases: new[] { "ScaleRewardByWord" }),
        S("difficultyBonusPercent", 25.0, page: "game", label: "Difficulty bonus", help: "Bonus per difficulty level above 1. 25 means a difficulty 3 word pays +50%.", kind: "double", section: "Word scaling", min: 0.0, unit: "%", requires: "scaleRewardByWord", aliases: new[] { "DifficultyBonusPercent" }),
        S("lengthBonusPercent", 5.0, page: "game", label: "Length bonus", help: "Bonus per letter beyond the threshold below (spaces don't count). 5 with a threshold of 4 means a 9-letter word pays +25%.", kind: "double", section: "Word scaling", min: 0.0, unit: "%", requires: "scaleRewardByWord", aliases: new[] { "LengthBonusPercent" }),
        S("lengthBonusFromLetters", 4, page: "game", label: "Length bonus from (letters)", help: "Word length at which the length bonus starts counting. Words this long or shorter get no length bonus.", section: "Word scaling", min: 0.0, requires: "scaleRewardByWord", aliases: new[] { "LengthBonusFromLetters" }),
        S("maxWordBonusPercent", 200.0, page: "game", label: "Most word bonus", help: "Upper limit for difficulty and length bonus combined, so a very long hard word can't pay out absurdly. 0 = no cap.", kind: "double", section: "Word scaling", min: 0.0, unit: "%", unlimitedAtZero: true, requires: "scaleRewardByWord", aliases: new[] { "MaxWordBonusPercent" }),
        S("subscriberBoostEnabled", false, page: "game", label: "Subscriber boost", help: "Gives Twitch subscribers a bigger win payout, applied after word scaling. Twitch only.", section: "Role boosts", aliases: new[] { "SubscriberBoostEnabled" }, twitchOnly: true),
        S("subscriberBoostPercent", 10.0, page: "game", label: "Subscriber boost", help: "How much extra subscribers earn.", kind: "double", section: "Role boosts", min: 0.0, unit: "%", requires: "subscriberBoostEnabled", aliases: new[] { "SubscriberBoostPercent" }),
        S("vipBoostEnabled", false, page: "game", label: "VIP boost", help: "Gives Twitch VIPs a bigger win payout, applied after word scaling. Twitch only.", section: "Role boosts", aliases: new[] { "VipBoostEnabled" }, twitchOnly: true),
        S("vipBoostPercent", 20.0, page: "game", label: "VIP boost", help: "How much extra VIPs earn.", kind: "double", section: "Role boosts", min: 0.0, unit: "%", requires: "vipBoostEnabled", aliases: new[] { "VipBoostPercent" }),
        S("freeGame", false, page: "game", label: "Free mode", help: "Every round is free: letters and the win pay nothing, and chat gets the Free mode messages. !SetFreeHangman and !EndFreeHangman switch it from chat (moderators).", section: "Free mode", aliases: new[] { "FreeGame" }),
        // ---- words ----
        S("minDifficulty", 1, page: "words", label: "Lowest difficulty", help: "Lowest difficulty the game picks. Words below it are never drawn.", section: "Word selection", min: 1.0, aliases: new[] { "MinDifficulty" }),
        S("maxDifficulty", 6, page: "words", label: "Highest difficulty", help: "Highest difficulty the game picks. Words above it are never drawn.", section: "Word selection", min: 1.0, aliases: new[] { "MaxDifficulty" }),
        S("excludedCategories", "", page: "words", label: "Categories left out", help: "Categories the game never draws from: type one as it is spelled in the list below and press Enter. Every other category is in play.", kind: "namelist", section: "Word selection", aliases: new[] { "ExcludedCategories" }),
        S("words", DefaultWordsArray(), page: "words", label: "Words", help: "One row per word: the word as chat has to guess it, its hint, its category and a difficulty from 1 up. 'Restore defaults' brings the built-in list back; the list lives in Hangman-Wordlist.json next to the settings.", kind: "list", section: "Word list", columns: new[] { "Word|string|Word", "Hint|string|Hint", "Category|string|Category", "Difficulty|int|Difficulty" }, file: "Hangman-Wordlist.json"),
        S("openWordlistFolder", null, page: "words", label: "Open the word list folder", help: "Opens the folder that holds Hangman-Wordlist.json in Explorer.", kind: "action", section: "Word list"),
        // ---- words: import (nothing here is saved; the imported words land in the list) ----
        S("importSource", "wikidata", page: "words", label: "Source", help: "Where the words come from: Wikidata (countries, capitals, rivers … or any class by Q-ID), OdeNet (German nouns by topic), PokéAPI (Pokémon by generation) or IGDB (video games; needs a Twitch developer app).", kind: "options", section: "Import words", options: new[] { "wikidata|Wikidata", "odenet|OdeNet (German nouns)", "pokeapi|PokéAPI (Pokémon)", "igdb|IGDB (video games)" }, transient: true),
        S("importLanguage", "en", page: "words", label: "Language", help: "Language of the words and hints, where the source has both (Wikidata, PokéAPI). OdeNet is German, IGDB English.", kind: "options", section: "Import words", options: new[] { "de|German", "en|English" }, transient: true),
        S("importTopic", "countries", page: "words", label: "Topic", help: "Wikidata: countries, capitals, cities, rivers, mountains, elements, instruments, sports, dogs, or custom with a class below. OdeNet: animals, plants, food, objects, people, body, places, materials, nature, feelings.", kind: "options", section: "Import words", options: new[] { "countries|Countries (Wikidata)", "capitals|Capitals (Wikidata)", "cities|Big cities (Wikidata)", "rivers|Rivers (Wikidata)", "mountains|Mountains (Wikidata)", "elements|Chemical elements (Wikidata)", "instruments|Musical instruments (Wikidata)", "sports|Sports (Wikidata)", "dogs|Dog breeds (Wikidata)", "custom|Custom class (Wikidata Q-ID)", "noun.animal|Animals (OdeNet)", "noun.plant|Plants (OdeNet)", "noun.food|Food & drink (OdeNet)", "noun.artifact|Objects & tools (OdeNet)", "noun.person|People & jobs (OdeNet)", "noun.body|Body (OdeNet)", "noun.location|Places (OdeNet)", "noun.substance|Materials (OdeNet)", "noun.object|Nature (OdeNet)", "noun.feeling|Feelings (OdeNet)" }, showWhen: "importSource=wikidata|odenet", transient: true),
        S("importCustomClass", "", page: "words", label: "Wikidata class (Q-ID)", help: "For the custom topic: the Q-ID of the class whose instances become words, like Q11424 for films.", section: "Import words", showWhen: "importSource=wikidata && importTopic=custom", transient: true),
        S("importGenerationFrom", 1, page: "words", label: "From generation", help: "First Pokémon generation to include, 1 to 9.", section: "Import words", min: 1.0, max: 9.0, showWhen: "importSource=pokeapi", transient: true, row: "gen"),
        S("importGenerationTo", 9, page: "words", label: "To generation", help: "Last Pokémon generation to include, 1 to 9.", section: "Import words", min: 1.0, max: 9.0, showWhen: "importSource=pokeapi", transient: true, row: "gen"),
        S("importIgdbClientId", "", page: "words", label: "IGDB client ID", help: "Client ID of your Twitch developer app (dev.twitch.tv/console/apps). Kept in Streamer.bot's global variables after the first import, so it needs typing only once.", section: "Import words", showWhen: "importSource=igdb", transient: true),
        S("importIgdbClientSecret", "", page: "words", label: "IGDB client secret", help: "Client secret of that app; kept with the ID after the first import.", section: "Import words", showWhen: "importSource=igdb", transient: true),
        S("importIgdbMinRatings", 100, page: "words", label: "At least this many ratings on IGDB", help: "Only games with at least this many ratings on IGDB. Higher means better-known titles.", section: "Import words", min: 0.0, showWhen: "importSource=igdb", transient: true),
        S("importCategory", "", page: "words", label: "Category for the imported words", help: "Every imported word lands in this category; empty takes the topic's name. Name an existing category to add to it.", section: "Import words", transient: true),
        S("importMaxCount", 100, page: "words", label: "At most this many words", help: "How many words to fetch at most.", section: "Import words", min: 1.0, transient: true, row: "limits"),
        S("importMaxLetters", 16, page: "words", label: "At most this many letters", help: "Longer words are skipped.", section: "Import words", min: 1.0, transient: true, row: "limits"),
        S("importMaxWords", 2, page: "words", label: "At most this many words per entry", help: "Entries of more words than this are skipped.", section: "Import words", min: 1.0, transient: true, row: "limits"),
        S("importReplaceUmlauts", false, page: "words", label: "Replace umlauts", help: "Writes ae, oe, ue and ss for ä, ö, ü and ß, so chat can guess them with plain letters.", section: "Import words", transient: true),
        S("importFixedDifficulty", 0, page: "words", label: "Difficulty for every imported word", help: "0 picks a difficulty per word from its length and how well known it is.", section: "Import words", min: 0.0, max: 9.0, transient: true),
        S("importWords", null, page: "words", label: "Import now", help: "Fetches the words from the source with the settings above, skips those already in the list, and writes the rest into Hangman-Wordlist.json. Reopen the window to see them in the list above.", kind: "action", section: "Import words", live: true, panel: true),
        // ---- overlay ----
        S("overlayEnabled", true, page: "overlay", label: "Overlay", help: "Writes the overlay files and keeps them updated. The overlay is a local HTML file OBS loads as a browser source; the game writes the round state next to it and the page redraws itself. Off: no overlay files at all.", section: "Overlay file", aliases: new[] { "OverlayEnabled" }),
        S("overlayFolder", "Overlay", page: "overlay", label: "Overlay folder", help: "Where hangman_overlay.html lives: a plain name like 'Overlay' is a subfolder of Chat Games\\Hangman Game, a full path puts it anywhere. The folder is created if missing; a change writes a fresh overlay file there, so repoint your OBS browser source afterwards.", section: "Overlay file", requires: "overlayEnabled", aliases: new[] { "OverlayFolder" }),
        S("openOverlayFolder", null, page: "overlay", label: "Open the overlay folder", help: "Opens the overlay folder in Explorer.", kind: "action", section: "Overlay file", requires: "overlayEnabled"),
        S("overlayFigureMode", "auto", page: "overlay", label: "Figure", help: "Proportional draws the classic 6-part figure spread across however many lives you allow, so the last life always completes it. Classic + gallows adds the 4 gallows pieces as extra steps, which suits higher life counts. Auto: proportional up to 6 lives, gallows above.", kind: "options", section: "Hangman figure", options: new[] { "auto|Auto: proportional up to 6 lives, gallows above", "proportional|Proportional: always 6 parts", "extended|Classic + gallows: always 10 parts" }, requires: "overlayEnabled", aliases: new[] { "OverlayFigureMode" }),
        S("overlayScalePercent", 100.0, page: "overlay", label: "Overlay scale", help: "Resizes the whole overlay. 100 = as designed, 150 = half again as big, 50 = half size.", kind: "double", section: "Appearance", min: 10.0, unit: "%", requires: "overlayEnabled", aliases: new[] { "OverlayScalePercent" }, row: "scale"),
        S("overlayFontScalePercent", 100.0, page: "overlay", label: "Text scale", help: "Resizes only the text, leaving the figure alone.", kind: "double", section: "Appearance", min: 10.0, unit: "%", requires: "overlayEnabled", aliases: new[] { "OverlayFontScalePercent" }, row: "scale"),
        S("overlayGallowsColour", "#f2f2f2", page: "overlay", label: "Gallows colour", help: "Colour of the gallows: base, post, beam and rope. The same as the figure colour gives the classic single-colour look.", kind: "colour", section: "Appearance", requires: "overlayEnabled", aliases: new[] { "OverlayGallowsColor" }, row: "colours1"),
        S("overlayFigureColour", "#f2f2f2", page: "overlay", label: "Figure colour", help: "Colour of the body: head, torso, arms and legs.", kind: "colour", section: "Appearance", requires: "overlayEnabled", aliases: new[] { "OverlayFigureColor" }, row: "colours1"),
        S("overlayTextColour", "#ffffff", page: "overlay", label: "Text colour", help: "Colour of the word and the stats.", kind: "colour", section: "Appearance", requires: "overlayEnabled", aliases: new[] { "OverlayTextColor" }, row: "colours2"),
        S("overlayAccentColour", "#e91e63", page: "overlay", label: "Accent colour", help: "Colour of the category and the small labels.", kind: "colour", section: "Appearance", requires: "overlayEnabled", aliases: new[] { "OverlayAccentColor" }, row: "colours2"),
        S("overlayBackgroundColour", "#000000", page: "overlay", label: "Background colour", help: "Colour of the panel behind the overlay. Only visible once the background opacity is above 0.", kind: "colour", section: "Appearance", requires: "overlayEnabled", aliases: new[] { "OverlayBackgroundColor" }, row: "colours3"),
        S("overlayBackgroundOpacity", 0.0, page: "overlay", label: "Background opacity", help: "0 = fully transparent (best for overlays), 100 = solid.", kind: "percent", section: "Appearance", requires: "overlayEnabled", aliases: new[] { "OverlayBackgroundOpacity" }, aliasFactor: 100.0, row: "colours3"),
        S("overlayEndFadeSeconds", 8, page: "overlay", label: "Keep the final word for", help: "How long the final word stays on screen after a round ends before it fades out. 0 = hide it at once.", kind: "duration", section: "Appearance", min: 0.0, requires: "overlayEnabled", aliases: new[] { "OverlayEndFadeSeconds" }),
        S("overlayShowCategory", true, page: "overlay", label: "Category", help: "Shows the word's category above the puzzle.", section: "Show on overlay", requires: "overlayEnabled", aliases: new[] { "OverlayShowCategory" }),
        S("overlayShowLives", true, page: "overlay", label: "Lives", help: "Shows the remaining lives as hearts.", section: "Show on overlay", requires: "overlayEnabled", aliases: new[] { "OverlayShowLives" }),
        S("overlayShowHint", true, page: "overlay", label: "Hint", help: "Shows the hint on the overlay once the game reveals it in chat.", section: "Show on overlay", requires: "overlayEnabled", aliases: new[] { "OverlayShowHint" }),
        S("overlayShowGuessed", true, page: "overlay", label: "Guessed letters", help: "Shows every letter guessed so far.", section: "Show on overlay", requires: "overlayEnabled", aliases: new[] { "OverlayShowGuessed" }),
        S("overlayShowWrong", true, page: "overlay", label: "Wrong letters", help: "Shows the wrong letters, struck through.", section: "Show on overlay", requires: "overlayEnabled", aliases: new[] { "OverlayShowWrong" }),
        S("overlayShowWrongWords", true, page: "overlay", label: "Wrong words", help: "Shows whole words chat guessed and got wrong, struck through.", section: "Show on overlay", requires: "overlayEnabled", aliases: new[] { "OverlayShowWrongWords" }),
        S("obsConnectionId", 0, page: "overlay", label: "OBS connection", help: "Which OBS connection in Streamer.bot (Stream Apps > OBS Studio) to use; the first one is 0.", section: "Set it up in OBS", min: 0.0, aliases: new[] { "ObsConnectionId" }),
        S("obsSceneName", "Hangman", page: "overlay", label: "Scene", help: "The scene the browser source goes into; created if it doesn't exist.", section: "Set it up in OBS", aliases: new[] { "ObsSceneName" }),
        S("obsSourceName", "Hangman Overlay", page: "overlay", label: "Source", help: "Name of the browser source to create.", section: "Set it up in OBS", aliases: new[] { "ObsSourceName" }),
        S("setupObsOverlay", null, page: "overlay", label: "Create the browser source in OBS", help: "Creates the scene above if it is missing and adds a browser source pointing at the overlay file. Needs OBS connected in Streamer.bot. You can also add it by hand: Browser Source > Local file > hangman_overlay.html from the folder above.", kind: "action", section: "Set it up in OBS", confirm: "This creates the scene if it is missing and adds a browser source pointing at the overlay file, over the OBS connection set above. Go ahead?"),
        S("refreshObsOverlay", null, page: "overlay", label: "Refresh the overlay in OBS", help: "Reloads every OBS browser source that shows hangman_overlay.html, so OBS runs the newest overlay. The game also does this by itself after an update, once OBS is connected.", kind: "action", section: "Set it up in OBS"),
        S("overlaySample", null, page: "overlay", label: "Show a sample round", help: "Plays one made-up round on the overlay that is open, the one in OBS included, so you can see where it sits and how it looks, an Evil Hangman swap and a win included. If you are live, your viewers see it too. Uses the settings as saved; not while a real round runs.", kind: "action", section: "Set it up in OBS", row: "overlayTry"),
        S("overlayDemo", null, page: "overlay", label: "Try the demo", help: "Opens the overlay in your browser, playing made-up rounds the way the saved settings say, without Streamer.bot. In OBS, &demo=1 at the end of the source's address does the same, to place and size it.", kind: "action", section: "Set it up in OBS", row: "overlayTry"),
        // ---- access ----
        S("pointsVariable", "points", page: "access", section: "Currency", aliases: new[] { "PointsVar" }),
        S("pointsName", "Points", page: "access", section: "Currency", aliases: new[] { "PointsName" }),
        S("requireModToStart", true, page: "access", label: "Only moderators start a round", help: "Only moderators and the broadcaster can start a round; everyone can still guess in a round that is running.", section: "Who can start a round", aliases: new[] { "RequireModToStart" }),
        S("blockedUsers", "", page: "access", section: "Who may not play", aliases: new[] { "BlockedUsers" }),
        S("blockedGroups", "", page: "access", section: "Who may not play", aliases: new[] { "BlockedGroups" }),
        S("allowChannelPointStart", false, page: "access", label: "A reward starts the round", help: "On Twitch a channel point redemption starts the round instead of the chat command, which then starts a round on Twitch for moderators only. Everyone still guesses as usual.", section: "Channel points (Twitch)", aliases: new[] { "AllowChannelPointStart" }, twitchOnly: true),
        S("channelPointRewardId", "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx", page: "access", label: "Reward ID", help: "The reward's id. Find it at ayrtonalexis.com/reward_checker: authorize, 'Get Rewards', copy the id of your reward.", section: "Channel points (Twitch)", requires: "allowChannelPointStart", aliases: new[] { "ChannelPointRewardId" }),
        S("channelPointFixedBet", 0, page: "access", label: "Fixed stake", help: "0 reads the stake from the redemption: a number the viewer typed, else the reward's cost, else no stake. Above 0 every redemption stakes exactly this much.", section: "Channel points (Twitch)", min: 0.0, requires: "allowChannelPointStart", aliases: new[] { "ChannelPointFixedBet" }),
        S("channelPointPayoutMultiplier", 1.0, page: "access", label: "Stake payout multiplier", help: "Streamer.bot cannot pay channel points back out, so a win always pays in your currency: the stake times this, added to the winner's payout. 1 pays it back once, 2 doubles it, 0 pays no stake bonus.", kind: "double", section: "Channel points (Twitch)", min: 0.0, requires: "allowChannelPointStart", aliases: new[] { "ChannelPointPayoutMultiplier" }),
        S("refundFailedRedemptions", true, page: "access", label: "Refund redemptions that don't start a round", help: "When the round cannot start (already running, no word, invalid stake), the redemption is cancelled and the channel points go back. Only works while the redemption is still in the request queue.", section: "Channel points (Twitch)", requires: "allowChannelPointStart", aliases: new[] { "RefundFailedRedemptions" }),
        S("globalCooldown", 0, page: "access", section: "Cooldowns", aliases: new[] { "CooldownGlobalSeconds" }),
        S("userCooldown", 0, page: "access", section: "Cooldowns", aliases: new[] { "CooldownUserSeconds" }),
        S("antiCheatMaxGuesses", 3, page: "access", label: "Guesses per window", help: "How many guesses one viewer may make inside the window below; broadcasters are exempt. 0 = no limit.", section: "Rate limit", min: 0.0, unlimitedAtZero: true, aliases: new[] { "AntiCheatMaxGuesses" }),
        S("antiCheatWindowSeconds", 10, page: "access", label: "Window", help: "The timeframe the guess count is measured over.", kind: "duration", section: "Rate limit", min: 0.0, aliases: new[] { "AntiCheatWindowSeconds" }),
        S("addictedEnabled", true, page: "access", label: "Handle addicted viewers", help: "The regulars who play every round keep playing, but earn less and have to pace their guesses so newer chatters get a look in.", section: "Addicted", aliases: new[] { "AddictedEnabled" }),
        S("addictedPointsPenaltyPercent", 50.0, page: "access", label: "Points penalty", help: "How much of their winnings is taken away. 50 means they earn half of what they otherwise would.", kind: "percent", section: "Addicted", requires: "addictedEnabled", aliases: new[] { "AddictedPointsPenaltyPercent" }),
        S("addictedGuessIntervalSeconds", 5, page: "access", label: "Pause between guesses", help: "Minimum time an addicted viewer must wait between their own guesses.", kind: "duration", section: "Addicted", min: 0.0, requires: "addictedEnabled", aliases: new[] { "AddictedGuessIntervalSeconds" }),
        S("addictedUsers", "", page: "access", section: "Addicted", aliases: new[] { "AddictedUsers" }),
        S("addictedGroups", "", page: "access", section: "Addicted", aliases: new[] { "AddictedGroups" }),
        // ---- messages: the senders (Messages page) and the log switch (Log page) ----
        S("useBot", true, aliases: new[] { "UseBot" }),
        S("sendAction", false, aliases: new[] { "SendAction" }),
        S("cooldownUseBot", true, aliases: new[] { "UseBotCD" }),
        S("cooldownSendAction", false, aliases: new[] { "SendActionCD" }),
        S("numberSeparator", ",", aliases: new[] { "NumberSeparator" }),
        S("logToFile", true, page: "log", label: "Write a log file per day", help: "Every round, guess, payout and refusal goes into Chat Games\\Logs\\HM-log-<date>.txt, which the Log page reads.", aliases: new[] { "LogToFile" }),
        // ---- board ----
        S("scoreboardTopN", 5, page: "board", label: "Places on the board", help: "How many viewers the scoreboard shows, 1 to 25.", section: "Behaviour", min: 1.0, max: 25.0, aliases: new[] { "ScoreboardTopN" }),
        S("broadcasterAllowed", false, page: "board", label: "Include the broadcaster", help: "Shows you, the broadcaster, on the scoreboard.", section: "Behaviour", aliases: new[] { "BroadcasterAllowed" }),
        S("botAllowed", false, page: "board", label: "Include the bot", help: "Shows your bot account on the scoreboard.", section: "Behaviour", aliases: new[] { "BotAllowed" }),
        S("blockedFromBoard", "", page: "board", label: "Hidden from the board", help: "Viewers hidden from the scoreboard without being blocked from playing; useful when other bots take part.", kind: "namelist", section: "Behaviour", aliases: new[] { "BlockedFromBoard" }),
        S("streaksEnabled", false, page: "board", label: "Track streaks", help: "Every word a viewer solves adds one to their streak; it only breaks when a round they guessed in is lost. A stopped round breaks nothing. Streaks are announced in chat, show in the personal stats, and the auto reset clears them too.", section: "Streaks", aliases: new[] { "StreaksEnabled" }),
        S("streakAnnounceFrom", 2, page: "board", label: "Announce from", help: "Streaks at least this long are cheered after a win and called out when they end. Never below 2.", section: "Streaks", min: 2.0, requires: "streaksEnabled", aliases: new[] { "StreakAnnounceFrom" }),
        S("streakBonusPercent", 10.0, page: "board", label: "Streak bonus", help: "Added to the win payout for every win in a row after the first. 10 means the 3rd win in a row pays +20%. 0 = no bonus.", kind: "double", section: "Streaks", min: 0.0, unit: "%", requires: "streaksEnabled", aliases: new[] { "StreakBonusPercent" }),
        S("streakBonusMaxPercent", 50.0, page: "board", label: "Most streak bonus", help: "Upper limit for the streak bonus. 0 = no cap.", kind: "double", section: "Streaks", min: 0.0, unit: "%", unlimitedAtZero: true, requires: "streaksEnabled", aliases: new[] { "StreakBonusMaxPercent" }),
        S("resetAllMonthly", false, page: "board", label: "Reset every platform monthly", help: "Clears the scoreboards of every platform at the end of the month.", section: "Auto reset", aliases: new[] { "ResetAllMonthly" }),
        S("resetTwitchMonthly", false, page: "board", label: "Twitch: reset monthly", help: "Clears the Twitch scoreboard as a new month begins.", section: "Auto reset", requires: "resetAllMonthly=false", aliases: new[] { "ResetTwitchMonthly" }, row: "resetTwitch"),
        S("resetTwitchDay", 0, page: "board", label: "Twitch: reset on day", help: "Clears the Twitch scoreboard every month on this day (the last day in a shorter month). 0 = on the 1st while monthly is on, else never.", section: "Auto reset", min: 0.0, max: 31.0, requires: "resetAllMonthly=false", aliases: new[] { "ResetTwitchDay" }, row: "resetTwitch"),
        S("resetYouTubeMonthly", false, page: "board", label: "YouTube: reset monthly", help: "Clears the YouTube scoreboard as a new month begins.", section: "Auto reset", requires: "resetAllMonthly=false", aliases: new[] { "ResetYouTubeMonthly" }, row: "resetYouTube"),
        S("resetYouTubeDay", 0, page: "board", label: "YouTube: reset on day", help: "Clears the YouTube scoreboard every month on this day (the last day in a shorter month). 0 = on the 1st while monthly is on, else never.", section: "Auto reset", min: 0.0, max: 31.0, requires: "resetAllMonthly=false", aliases: new[] { "ResetYouTubeDay" }, row: "resetYouTube"),
        S("resetKickMonthly", false, page: "board", label: "Kick: reset monthly", help: "Clears the Kick scoreboard as a new month begins.", section: "Auto reset", requires: "resetAllMonthly=false", aliases: new[] { "ResetKickMonthly" }, row: "resetKick"),
        S("resetKickDay", 0, page: "board", label: "Kick: reset on day", help: "Clears the Kick scoreboard every month on this day (the last day in a shorter month). 0 = on the 1st while monthly is on, else never.", section: "Auto reset", min: 0.0, max: 31.0, requires: "resetAllMonthly=false", aliases: new[] { "ResetKickDay" }, row: "resetKick"),
        // ---- messages ----
        Msg("Round", "msgGameStart", "🎮 Hangman started! Category: {category} | Word: {masked} | ❤️ Lives: {lives}/{maxlives} | Guess with !g <letter or word>", "Game start", "Sent when a round starts.", new[] { "category", "masked", "lives", "maxlives" }, alias: "MsgGameStart"),
        Msg("Round", "msgAlreadyRunning", "⚠️ A Hangman round is already running! Word: {masked} | ❤️ Lives: {lives}/{maxlives}", "Already running", "Sent when someone tries to start while a round is already running.", new[] { "masked", "lives", "maxlives" }, alias: "MsgAlreadyRunning"),
        Msg("Round", "msgNoGameActive", "⚠️ No Hangman round is active right now.", "No round active", "Sent when a viewer guesses while no round is active.", new string[0], alias: "MsgNoGameActive"),
        Msg("Round", "msgRepeat", "🔁 Word: {masked} | ❤️ Lives: {lives}/{maxlives} | Guessed: {guessed} | Wrong: {wrong}", "Repeat", "Sent when a viewer asks for the current state with the repeat command.", new[] { "masked", "lives", "maxlives", "guessed", "wrong" }, alias: "MsgRepeat"),
        Msg("Round", "msgTimeReminder", "⏳ {seconds}s left! Word: {masked} | ❤️ Lives: {lives}/{maxlives}", "Time reminder", "Sent shortly before the round times out.", new[] { "seconds", "masked", "lives", "maxlives" }, alias: "MsgTimeReminder"),
        Msg("Guessing", "msgCorrectLetter", "✅ @{user} found '{letter}'! Word: {masked} | ❤️ Lives: {lives}/{maxlives} | Guessed: {guessed}", "Correct letter", "Sent when a guessed letter is in the word.", new[] { "user", "letter", "masked", "lives", "maxlives", "guessed" }, alias: "MsgCorrectLetter"),
        Msg("Guessing", "msgCorrectWord", "🎉 @{user} solved it! The word was '{word}'! +{points} {pointsName}", "Correct word", "Sent when a viewer solves the whole word.", new[] { "user", "word", "points", "pointsName", "basepoints", "difficulty", "length", "wordbonus", "bet", "stake", "streak", "streakbonus" }, alias: "MsgCorrectWord"),
        Msg("Guessing", "msgLetterReward", "💰 @{user} earned {points} {pointsName} for revealing '{letter}'!", "Letter reward", "Sent when a viewer earns points for revealing a letter.", new[] { "user", "letter", "points", "pointsName" }, alias: "MsgLetterReward"),
        Msg("Guessing", "msgAlreadyGuessed", "🔁 @{user}, '{letter}' was already guessed!", "Already guessed", "Sent when a letter was already guessed.", new[] { "user", "letter" }, alias: "MsgAlreadyGuessed"),
        Msg("Guessing", "msgInvalidGuess", "❓ @{user}, use !g <letter> or !g <word> to guess.", "Invalid guess", "Sent when the guess could not be understood.", new[] { "user", "word" }, alias: "MsgInvalidGuess"),
        Msg("Wrong guesses", "msgWrongLetter", "❌ @{user} — '{letter}' is not in the word! ❤️ Lives: {lives}/{maxlives} | Wrong: {wrong}", "Wrong letter", "Sent when a guessed letter is not in the word.", new[] { "user", "letter", "lives", "maxlives", "wrong" }, alias: "MsgWrongLetter"),
        Msg("Wrong guesses", "msgWrongWord", "❌ @{user} — '{guess}' is wrong! (-2 lives) ❤️ Lives: {lives}/{maxlives}", "Wrong word", "Sent on a wrong full-length word guess, which costs two lives.", new[] { "user", "guess", "lives", "maxlives" }, alias: "MsgWrongWord"),
        Msg("Wrong guesses", "msgWrongPartial", "❌ @{user} — '{guess}' is not the word! (-1 life) ❤️ Lives: {lives}/{maxlives}", "Wrong partial word", "Sent on a wrong partial guess of a multi-word answer, which costs one life.", new[] { "user", "guess", "lives", "maxlives" }, alias: "MsgWrongPartial"),
        Msg("Wrong guesses", "msgDuplicateWrong", "🔁 @{user}, you already tried '{guess}' — don't spam!", "Repeated wrong guess", "Sent when the same viewer repeats a guess they already got wrong.", new[] { "user", "guess" }, alias: "MsgDuplicateWrong"),
        Msg("Ending", "msgGameOver", "💀 Game over! Nobody guessed it. The word was: {word}", "Game over", "Sent when chat runs out of lives.", new[] { "word" }, alias: "MsgGameOver"),
        Msg("Ending", "msgGameTimeout", "⏰ Time's up! The word was: {word}", "Time's up", "Sent when the round duration runs out with nobody solving it.", new[] { "word" }, alias: "MsgGameTimeout"),
        Msg("Ending", "msgGameStopped", "🛑 Hangman stopped by a mod. The word was: {word}", "Stopped", "Sent when a moderator ends the round.", new[] { "word" }, alias: "MsgGameStopped"),
        Msg("Hints", "msgHint", "💡 Hint: {hint}", "Hint", "Sent when the hint is revealed.", new[] { "hint" }, alias: "MsgHint"),
        Msg("Blitz", "msgBlitzStart", "⚡ BLITZ ROUND! {seconds}s, {lives} lives, every point pays x{multiplier}! Category: {category} | Word: {masked} | Guess with !g <letter or word>", "Blitz start", "Sent in place of the normal start message when a round is a blitz.", new[] { "user", "category", "masked", "lives", "maxlives", "seconds", "multiplier", "bet" }, alias: "MsgBlitzStart"),
        Msg("Blitz", "msgBlitzWin", "⚡ @{user} won the blitz! The word was '{word}'! +{points} {pointsName} (x{multiplier})", "Blitz win", "Sent in place of 'Correct word' when a blitz round is solved.", new[] { "user", "word", "points", "pointsName", "basepoints", "multiplier", "difficulty", "length", "wordbonus", "bet", "stake", "streak", "streakbonus" }, alias: "MsgBlitzWin"),
        Msg("Evil Hangman", "msgEvilSwap", "👾 GLITCH! The word was swapped, only '{letter}' survived. Category: {category} | Word: {masked} | ❤️ Lives: {lives}/{maxlives}", "Word swapped", "Sent once the overlay glitch has played out, when one revealed letter survived the swap.", new[] { "letter", "oldword", "category", "masked", "lives", "maxlives" }, alias: "MsgEvilSwap"),
        Msg("Evil Hangman", "msgEvilSwapNoLetter", "👾 GLITCH! The word was swapped. Category: {category} | Word: {masked} | ❤️ Lives: {lives}/{maxlives}", "Word swapped, no letter", "Sent once the overlay glitch has played out, when chat had revealed no letter before the swap.", new[] { "oldword", "category", "masked", "lives", "maxlives" }, alias: "MsgEvilSwapNoLetter"),
        Msg("Streaks", "msgStreak", "🔥 @{user} is on a {streak} win streak! +{streakbonus}% bonus", "Streak", "Sent after a win once the winner's streak reaches 'Announce from'.", new[] { "user", "streak", "streakbonus" }, alias: "MsgStreak"),
        Msg("Streaks", "msgStreakLost", "💔 Streak over: {users}", "Streak lost", "Sent when a lost round ends streaks of at least 'Announce from'.", new[] { "users" }, alias: "MsgStreakLost"),
        Msg("Streaks", "msgPersonalStreak", "🔥 @{user} — Streak: {streak} | Best: {beststreak}", "Personal streak", "Extra line under the personal stats while streaks are tracked.", new[] { "user", "streak", "beststreak" }, alias: "MsgPersonalStreak"),
        Msg("Access", "msgModOnly", "🚫 @{user}, only moderators and the broadcaster can start Hangman.", "Mods only", "Sent when a viewer tries to start a round while only moderators may.", new[] { "user" }, alias: "MsgModOnly"),
        Msg("Ad breaks", "msgAdBreakAnnouncement", "📺 Ads in about {minutes} min, {seconds} seconds of them. A round of Hangman starts with them, so stay in chat!", "Ads coming", "Sent on Twitch ahead of an ad break, with 'Announce it before the ads' on. Clear this box to say nothing.", new[] { "minutes", "seconds" }, alias: "MsgAdBreakAnnouncement"),
        Msg("Ad breaks", "msgAdBreakOnly", "🚫 @{user}, Hangman only starts during an ad break.", "Only during ads", "Sent on Twitch when a viewer tries to start a round outside an ad break while 'Rounds only during ads' is on.", new[] { "user" }, alias: "MsgAdBreakOnly"),
        Msg("Access", "msgNotAllowed", "🚫 @{user}, you are not allowed to participate.", "Not allowed", "Sent when a blocked viewer tries to take part.", new[] { "user" }, alias: "MsgNotAllowed"),
        Msg("Access", "msgAntiCheat", "⏳ @{user}, slow down! Max {maxguesses} guesses per {window}s.", "Rate limit", "Sent when a viewer is held back for guessing too fast.", new[] { "user", "maxguesses", "window" }, alias: "MsgAntiCheat"),
        Msg("Cooldown", "globalCooldownMessage", "Please wait {seconds} seconds before trying again ⏳", "Global cooldown", "Sent when someone tries to start while the global cooldown is still running.", new[] { "seconds", "user" }, alias: "GlobalMessage"),
        Msg("Cooldown", "userCooldownMessage", "@{user}, please wait {seconds} seconds before trying again ⏳", "Per-viewer cooldown", "Sent when a viewer tries to start while their own cooldown is still running.", new[] { "user", "seconds" }, alias: "UserMessage"),
        Msg("Addicted", "msgAddictedSlowdown", "⏳ @{user}, slow down! You're addicted — wait {seconds}s between guesses.", "Slow down", "Sent when an addicted viewer guesses again before their pause is up.", new[] { "user", "seconds" }, alias: "MsgAddictedSlowdown"),
        Msg("Addicted", "msgAddictedAdded", "{targetUser} was added to the addicted list.", "Added", "Sent after !addicted add succeeds.", new[] { "targetUser" }, alias: "MsgAddictedAdded"),
        Msg("Addicted", "msgAddictedRemoved", "{targetUser} was removed from the addicted list.", "Removed", "Sent after !addicted remove succeeds.", new[] { "targetUser" }, alias: "MsgAddictedRemoved"),
        Msg("Addicted", "msgAddictedAlreadyOnList", "{targetUser} is already on the addicted list.", "Already on the list", "Sent when adding someone who is already on the list.", new[] { "targetUser" }, alias: "MsgAddictedAlreadyOnList"),
        Msg("Addicted", "msgAddictedNotOnList", "{targetUser} is not on the addicted list.", "Not on the list", "Sent when removing someone who is not on the list.", new[] { "targetUser" }, alias: "MsgAddictedNotOnList"),
        Msg("Addicted", "msgAddictedUsage", "Usage: !addicted add <user> | !addicted remove <user>", "Usage", "Sent when the !addicted command was typed wrongly.", new string[0], alias: "MsgAddictedUsage"),
        Msg("Channel points", "msgChannelPointStart", "🎰 @{user} used Channel Points to start Hangman! Category: {category} | Word: {masked} | ❤️ Lives: {lives}/{maxlives} | Guess with !g <letter or word>", "Redemption start", "Sent when a round starts from a channel point redemption.", new[] { "user", "category", "masked", "lives", "maxlives", "bet" }, alias: "MsgChannelPointStart"),
        Msg("Channel points", "msgChannelPointBusy", "⚠️ @{user}, a round is already running! Word: {masked} | ❤️ Lives: {lives}/{maxlives}", "Redemption while running", "Sent when a redemption arrives while a round is already running.", new[] { "user", "masked", "lives", "maxlives" }, alias: "MsgChannelPointBusy"),
        Msg("Channel points", "msgChannelPointBadInput", "⚠️ @{user}, invalid bet — enter a positive number in the reward text field.", "Redemption bad input", "Sent when the viewer typed something into the reward that is not a positive number.", new[] { "user" }, alias: "MsgChannelPointBadInput"),
        Msg("Channel points", "msgRedemptionRefunded", "↩️ @{user}, your channel point redemption was refunded.", "Redemption refunded", "Sent after a redemption was refunded.", new[] { "user" }, alias: "MsgRedemptionRefunded"),
        Msg("Scoreboard", "msgScoreboardHeader", "🏆 Hangman Top {topn} — {platform}:", "Board header", "First line of the scoreboard.", new[] { "topn", "platform" }, alias: "MsgScoreboardHeader"),
        Msg("Scoreboard", "msgScoreboardEntry", "{medal}{rank}. {user} — {wins} wins | WR: {winrate}% | {points} pts", "Board entry", "Repeated once per viewer on the board.", new[] { "medal", "rank", "user", "wins", "winrate", "points", "streak", "beststreak" }, alias: "MsgScoreboardEntry"),
        Msg("Scoreboard", "msgScoreboardEmpty", "📊 No scoreboard data yet for {platform}.", "Board empty", "Sent when there is no scoreboard data yet.", new[] { "platform" }, alias: "MsgScoreboardEmpty"),
        Msg("Scoreboard", "msgPersonalStats", "📊 @{user} — 🏆 Wins: {wins} | 🎮 Played: {played} | 📈 WR: {winrate}% | 💰 Points: {points} | 🔤 Letters: {letters} | 💡 Word Wins: {wordwins}", "Personal stats", "Sent when a viewer asks for their own stats.", new[] { "user", "wins", "played", "winrate", "points", "letters", "wordwins", "streak", "beststreak" }, alias: "MsgPersonalStats"),
        Msg("Scoreboard", "msgNoStats", "📊 @{user} has no Hangman stats yet.", "No stats", "Sent when the viewer has no stats yet.", new[] { "user" }, alias: "MsgNoStats"),
        Msg("Scoreboard", "msgUserNotFound", "❓ Could not find user '{target}'.", "User not found", "Sent when a looked-up viewer does not exist.", new[] { "target" }, alias: "MsgUserNotFound"),
        Msg("Scoreboard", "msgMonthlyReset", "🔄 Hangman {platform} leaderboard has been reset for the new month!", "Reset announcement", "Sent when a scoreboard is reset.", new[] { "platform" }, alias: "MsgMonthlyReset"),
        Msg("Free mode", "freeGameStartMessage", "🆓 Free mode is ON! Guess for the fun of it - no {pointsName} change hands!", "Started", "Sent once when Free mode is turned on by !SetFreeHangman, in the chat the command came from. Turned on another way, say by an Execute C# Method on StartFreeGame in an action of your own, it goes to every platform with an account in Streamer.bot - to YouTube only while a broadcast is monitored.", new[] { "pointsName" }, alias: "FreeGameStartMessage"),
        Msg("Free mode", "freeGameEndMessage", "⏹️ Free mode has ended. Words pay {pointsName} again!", "Ended", "Sent once when Free mode is turned off by !EndFreeHangman, in the chat the command came from. Turned off another way, say by an Execute C# Method on EndFreeGame, it goes to every platform with an account in Streamer.bot - to YouTube only while a broadcast is monitored.", new[] { "pointsName" }, alias: "FreeGameEndMessage"),
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
    public readonly Newtonsoft.Json.Linq.JArray AdoptList = new Newtonsoft.Json.Linq.JArray();

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
    internal const string StubVersion = "0.3.3";

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
