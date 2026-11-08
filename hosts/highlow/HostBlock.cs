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
    /// <summary>A stop of the window's tour (UiWindow.Tour): what it points at, a heading and a text. The target is a
    /// setting's key, "page:id" for a page in the rail, a part of the window ("start", "end", "rail", "save",
    /// "saveExit", "cancel", "status", "reset", "resetPage", "theme", "customise", "tour"), or null for a note in the
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
                Note("There is always a number from 1 to 100 waiting; it is drawn when Streamer.bot starts. Viewers guess whether the next number will be higher, lower or the same. The answer names both numbers, and the new one is what the next guess is measured against."),
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
                Note("Every settled guess fires a custom trigger - HLG: Guess Won or HLG: Guess Lost, and HLG: Guess Settled always (Custom > Chat Game > High-Low Game) - with the guess as arguments: guessOutcome, guessUser, guessUserId, guessPlatform, guessGuess, guessPreviousNumber, guessNewNumber, guessMode, guessStake, guessAmount, guessPreviousPoints, guessNewPoints, guessCurrency and guessFree. GetLastGuessResult, run with an Execute C# Method sub-action on the game action, hands the same arguments to any other action."),
                Note("HLG: New Number fires when !highlowsetup draws one (numberNew, numberUser, numberPlatform). HLG: Free Game Start and HLG: Free Game End fire when Free mode is switched from chat or by an Execute C# Method on StartFreeGame or EndFreeGame.")),
            H("Where things are kept",
                Cmd("HLG-Settings.json", "everything on these pages."),
                Cmd("uiframework.layout.json", "your layout of this window: theme, renamed or hidden pages and fields."),
                Cmd("HLG-log-yyyy-MM-dd.txt", "one file per day in Chat Games\\Logs: every settled guess, new number and refunded redemption, what the Log page shows."),
                Note("The first two sit in Streamer.bot's folder under Chat Games\\High-Low Game. Permissions and the cooldowns themselves live on the commands; saving writes the Cooldowns page onto them.")))
        .About(credits: "High-Low Game by aaskjer", links: new[] { Link("Repository", "https://github.com/aaskjer/High-Low-Game"), Link("Releases", "https://github.com/aaskjer/High-Low-Game/releases"), Link("Extension page", "https://extensions.streamer.bot/t/high-low-game/2781"), Link("StreamUP - Currency Core", "https://streamup.tips/product/currency-core") })
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
        Msg("Game", "outcomeTemplate", "Hey {user}! Previous: {previousNumber}, New: {newNumber}. Your guess '{guess}' was {result} {emoji} {pointsSection}. Session: {sessionGuesses} guesses, All-time: {alltimeGuesses}.", "Outcome", "Sent after every guess. {pointsSection} reads like ' won 1.234 Points' or ' lost 1.234 Points' and stays empty when no points changed.", new[] { "user", "guess", "result", "emoji", "previousNumber", "newNumber", "sessionGuesses", "alltimeGuesses", "pointsSection", "sign", "pointsDelta", "currency" }, alias: "OutcomeTemplate"),
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
                case "test": answer = test?.Invoke(name, values); break;
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
    private static System.Type FrameworkType()
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
                if (System.Version.TryParse(System.IO.Path.GetFileName(dir), out version) && System.IO.File.Exists(file) && (newest == null || Full(version) > newest))
                {
                    newest = Full(version);
                    newestFile = file;
                }
            }
        if (newestFile != null && (loadedVersion == null || newest > loadedVersion))
        {
            try
            {
                var type = System.Reflection.Assembly.LoadFrom(newestFile).GetType("UIFramework.UpdateNotices", false);
                if (type != null) return type;
            }
            catch (System.Exception) { }
        }
        return loaded;
    }

    private static System.Version Full(System.Version v) => new System.Version(v.Major, v.Minor, System.Math.Max(0, v.Build), System.Math.Max(0, v.Revision));
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
