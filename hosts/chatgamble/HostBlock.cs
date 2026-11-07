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
    public static readonly UiWindow Window = new UiWindow("Classic Chat Gamble", releases: "aaskjer/Classic-Chat-Gamble", logo: "https://github.com/aaskjer/Classic-Chat-Gamble/blob/main/Assets/CCG/Classic-Chat-Gamble-Logo.png?raw=true", logPrefix: "[Classic Chat Gamble/Settings]")
        .Pages(P("game", "Game", "🎲"), P("access", "Access", "👥"), P("messages", "Messages", "💬"), P("log", "Log", "📜"), P("help", "How to play", "📖"), P("about", "About", "ℹ️"))
        .Senders(useBot: "useBot", sendAction: "sendAction", cooldownUseBot: "cooldownUseBot", cooldownSendAction: "cooldownSendAction", numberSeparator: "numberSeparator", cooldownGroup: "Cooldown")
        .Pill("freeGame", "Free game on", "Free game off", tooltip: "Free Game Mode, from the Game page or !SetFreeGamble. While it is on, nobody pays or wins anything.")
        .Pill("isChannelPointsTrigger", "Channel points", "!gamble in chat", tooltip: "How viewers play on Twitch, from the Access page. YouTube and Kick always use !gamble.")
        .Cooldowns(C("df4181fc-651f-4fc3-98ea-4dec9b242836", "globalCooldown", "userCooldown"))   // !gamble, where the cooldowns live
        // German: the game's own keys and texts; sections, pages and the shared keys come from the conventions.
        .Lang("de",
            K("fairness", "Voreinstellung", "Ein Regler für die Chancen, von Streng, wo das Haus gewinnt und Punkte versickern, bis Klassisch, dem Standard mit seltenen, riesigen Jackpots, bei dem Punkte schnell wachsen. Eine Stufe füllt die fünf Zahlen darunter aus; gespeichert wird erst, wenn du speicherst. Eigene zeigt der Regler, wenn die Zahlen deine eigenen sind."),
            K("presetNote", null, "Eine Stufe des Reglers setzt Jackpot- und Gewinnchance, den Jackpot-Multiplikator und den Bereich des Gewinn-Multiplikators auf einmal. Das Spiel liest nur diese fünf Zahlen, du kannst sie also weiter feinjustieren: der Regler zeigt dann Eigene oder die Stufe, zu der sie passen."),
            K("jackpotPercent", "Jackpot-Chance", "Chance in Prozent, von 0 (nie) bis 100 (immer), dass ein Gamble den Jackpot trifft. Klein halten, damit Jackpots selten bleiben: '1' ist 1 von 100."),
            K("jackpotMultiplier", "Jackpot ×", "Multiplikator, der bei einem Jackpot auf den Gewinnwurf draufkommt. Mit '43' zahlt ein 2×-Wurf 2 × 43 = das 86-Fache des Einsatzes."),
            K("winPercent", "Gewinnchance", "Chance in Prozent, dass ein Gamble ein normaler Gewinn ist, zusätzlich zur Jackpot-Chance. Jackpot '1' und Gewinn '30' lassen 69 % Verlustchance."),
            K("rndMin", "Min ×", "Der kleinste Gewinn-Multiplikator. Jeder Gewinn würfelt einen Multiplikator zwischen Min und Max, ein Jackpot kommt auf diesen Wurf drauf."),
            K("rndMax", "Max ×", "Der größte Gewinn-Multiplikator."),
            K("odds", "Was wirklich rauskommt", "Die Chance auf Jackpot, Gewinn und Verlust und was ein Einsatz von 100 im Schnitt zurückbringt, aus den fünf Zahlen, wie sie stehen, für einen Zuschauer ohne Abonnenten-/VIP-Bonus oder Süchtigen-Kürzung."),
            K("previousDefaultBet", null, "Der Standardeinsatz von vor !SetFreeGamble, den !EndFreeGamble zurückbringt."),
            K("tryRoll", "Würfeln", "Spielt ein Gamble mit den Einstellungen, wie sie auf dieser Seite stehen, gespeichert oder nicht, und zeigt den Wurf und die Nachricht, die der Chat bekäme. Nichts wird gesendet, nichts gespeichert."),
            K("platformsNote", null, "Twitch, YouTube und Kick spielen alle, und jede Antwort geht in den Chat, aus dem der Befehl oder die Einlösung kam. Um eine Plattform auszulassen, entferne den Haken am Befehl !gamble in Streamer.bot, zum Beispiel 'Kick Message'."),
            K("cooldownNote", null, "Was der Chat während einer Abklingzeit erfährt, steht auf der Seite Nachrichten unter Abklingzeit."),
            K("minBet", "Mindesteinsatz", "Der kleinste erlaubte Einsatz. 0 schaltet die Grenze ab. Gilt auch für Kanalpunkte in den Modi 'Eingegebener Betrag' und 'Fest'."),
            K("subscriberBoostEnabled", "Abonnenten-Bonus", "Gibt Twitch-Abonnenten eine höhere Auszahlung bei Gewinnen und Jackpots. Nur Twitch: auf YouTube und Kick prüft das Spiel nicht auf Abonnenten."),
            K("vipBoostEnabled", "VIP-Bonus", "Gibt Twitch-VIPs eine höhere Auszahlung bei Gewinnen und Jackpots. Nur Twitch: auf YouTube und Kick prüft das Spiel nicht auf VIPs."),
            K("subscriberBoostPercent", "Abonnenten-Bonus", "Um wie viel größer der Gewinn eines Abonnenten ist: '10' macht aus 100 110. Addiert sich mit dem VIP-Bonus, wenn jemand beides ist."),
            K("vipBoostPercent", "VIP-Bonus", "Um wie viel größer der Gewinn eines VIPs ist: '20' macht aus 100 120. Addiert sich mit dem Abonnenten-Bonus, wenn jemand beides ist."),
            K("tryBet", "Einsatz zum Ausprobieren", "Ein Einsatz, um Chancen und Nachrichten auszuprobieren. Wird nicht gespeichert."),
            K("tryRole", "Spielen als", "Wer spielt: Boni und die Süchtigen-Kürzung ändern, was ein Gewinn zahlt."),
            K("cpBetMode", "Was gesetzt wird", "Preis der Belohnung: der Preis ist der Einsatz, Zuschauer tippen nichts; ein Verlust rührt ihre Punkte nicht an, ein Gewinn wird in Punkten gezahlt. Eingegebener Betrag: Zuschauer tippen einen Einsatz in die Belohnung, gezahlt aus eigenen Punkten und gegen den Kontostand geprüft; ohne genug Punkte wird die Einlösung erstattet. Fest: jede Einlösung setzt den festen Betrag unten, Preis und Text werden ignoriert, ein Verlust rührt die Punkte nicht an. Frei: nichts wird gewonnen oder verloren. Mindest- und Höchsteinsatz gelten für Eingegebener Betrag und Fest."),
            K("fixedBetAmount", "Fester Einsatz", "Was jede Einlösung im Modus Fest setzt. Muss mehr als 0 sein."),
            K("userCooldown", "Pro Zuschauer", "Wie lange ein Zuschauer bis zum nächsten Gamble wartet, in Sekunden. 0 schaltet sie ab. Speichern schreibt sie auf den Befehl !gamble. Eine Kanalpunkte-Einlösung während der Abklingzeit wird erstattet."),
            K("addictedGroups", "Süchtigen-Gruppen", "Mitglieder dieser Streamer.bot-Gruppen zählen auch als süchtig."),
            K("winMessage", "Gewinn", "Bei einem normalen Gewinn gesendet."),
            K("lossMessage", "Verlust", "Bei einem Verlust gesendet."),
            K("jackpotMessage", "Jackpot", "Bei einem Jackpot gesendet."),
            K("freeGameMessage", "Hinweis, einmal je Zuschauer", "Einmal an jeden Zuschauer gesendet, der im Freispiel-Modus spielt."),
            K("freeGameWinMessage", "Gewinn", "Bei einem Gewinn im Freispiel-Modus gesendet."),
            K("freeGameLossMessage", "Verlust", "Bei einem Verlust im Freispiel-Modus gesendet. Nichts wird abgezogen."),
            K("freeGameJackpotMessage", "Jackpot", "Bei einem Jackpot im Freispiel-Modus gesendet."),
            K("freeGameStartMessage", "Gestartet", "Einmal gesendet, wenn der Freispiel-Modus eingeschaltet wird, in den Chat, aus dem der Befehl oder die Einlösung kam. Anders gestartet, etwa per Execute C# Method auf StartFreeGame in einer eigenen Action, geht sie an jede Plattform mit Konto in Streamer.bot, an YouTube nur während ein Stream überwacht wird."),
            K("freeGameEndMessage", "Beendet", "Einmal gesendet, wenn der Freispiel-Modus endet und der Standardeinsatz zurück ist, in den Chat, aus dem der Befehl oder die Einlösung kam. Anders beendet, etwa per Execute C# Method auf EndFreeGame in einer eigenen Action, geht sie an jede Plattform mit Konto in Streamer.bot, an YouTube nur während ein Stream überwacht wird."),
            K("addictedWinMessage", "Gewinn", "Statt der Gewinn-Nachricht an Zuschauer auf der Süchtigen-Liste gesendet."),
            K("addictedJackpotMessage", "Jackpot", "Statt der Jackpot-Nachricht an Zuschauer auf der Süchtigen-Liste gesendet."),
            K("addictedAdded", "Hinzugefügt", "Gesendet, wenn !gambleaddict jemanden auf die Liste setzt."),
            K("addictedRemoved", "Entfernt", "Gesendet, wenn !gambleaddict remove jemanden von der Liste nimmt."),
            K("addictedAlreadyOnList", "Schon auf der Liste", "Gesendet, wenn !gambleaddict jemanden nennt, der schon auf der Liste steht."),
            K("addictedNotOnList", "Nicht auf der Liste", "Gesendet, wenn !gambleaddict remove jemanden nennt, der nicht auf der Liste steht."),
            K("addictedUsage", "Verwendung", "Gesendet, wenn !gambleaddict ohne Namen kommt."),
            K("invalidBetMessage", "Einsatz abgelehnt", "Der Rahmen um die fünf Gründe darunter; {reason} wird durch den zutreffenden ersetzt."),
            K("reasonNegativeAmount", "Grund: kein gültiger Betrag", "Füllt {reason}, wenn der Betrag negativ, keine Zahl, mit Nachkommastellen oder zu groß ist."),
            K("reasonInsufficientFunds", "Grund: nicht genug Punkte", "Füllt {reason}, wenn der Einsatz mehr ist, als der Zuschauer hat."),
            K("reasonBelowMinimum", "Grund: unter dem Minimum", "Füllt {reason}, wenn der Einsatz unter dem Minimum auf der Seite Spiel liegt."),
            K("reasonAboveMaximum", "Grund: über dem Maximum", "Füllt {reason}, wenn der Einsatz über dem Maximum auf der Seite Spiel liegt."),
            K("reasonNoInput", "Grund: kein Betrag", "Füllt {reason}, wenn kein Betrag angegeben wurde: Standardeinsatz ist 0, oder eine Einlösung mit eingegebenem Betrag kam ohne einen."),
            K("globalCooldownMessage", "Alle", "Gesendet, während die Abklingzeit für alle läuft."),
            K("userCooldownMessage", "Pro Zuschauer", "Gesendet, während die eigene Abklingzeit dieses Zuschauers läuft."),
            K("redemptionRefundedMessage", "Erstattet", "Gesendet, wenn eine Einlösung erstattet wird."),
            Tx("Strict", "Streng"),
            Tx("House Edge", "Hausvorteil"),
            Tx("Generous", "Großzügig"),
            Tx("Classic", "Klassisch"),
            Tx("Custom", "Eigene"),
            Tx("Viewer", "Zuschauer"),
            Tx("Subscriber", "Abonnent"),
            Tx("Addicted", "Süchtig"),
            Tx("Reward cost", "Preis der Belohnung"),
            Tx("Typed amount", "Eingegebener Betrag"),
            Tx("Fixed", "Fest"),
            Tx("Free", "Frei"),
            Tx("Results", "Ergebnisse"),
            Tx("Free game", "Freispiel"),
            Tx("Free Game Mode, from the Game page or !SetFreeGamble. While it is on, nobody pays or wins anything.", "Freispiel-Modus, von der Seite Spiel oder !SetFreeGamble. Solange er an ist, zahlt und gewinnt niemand etwas."),
            Tx("How viewers play on Twitch, from the Access page. YouTube and Kick always use !gamble.", "Wie Zuschauer auf Twitch spielen, von der Seite Zugriff. YouTube und Kick nutzen immer !gamble."))
        .Samples("user", "alice", "amount", "2,150", "currentPoints", "12,150", "addictedCut", "50", "target", "bob", "reason", "your bet is below the allowed minimum (10 Points).", "seconds", "12", "min", "10", "max", "9,999,999")
        .Answer("odds", "{\"set\":{\"fairness\":\"classic\"},\"lines\":[{\"text\":\"Classic · A bet of 100 comes back as about 188\",\"big\":true},\"Jackpot 1 %, win 30 %, so a 69 % chance to lose; a win pays ×1.5 to ×2.5, a jackpot 43 times that.\"],\"bar\":[{\"label\":\"Jackpot\",\"value\":1,\"text\":\"1 %\",\"colour\":\"#F59E0B\"},{\"label\":\"Win\",\"value\":30,\"text\":\"30 %\",\"colour\":\"#22C55E\"},{\"label\":\"Loss\",\"value\":69,\"text\":\"69 %\",\"colour\":\"#6B7280\"}]}")
        .Answer("tryRoll", "alice has 12,340 Points and bets 1,000 Points.\nRoll 4,812 of 10,000 · @alice, unlucky! You LOST 1,000 Points. New balance: 11,340 Points.")
        .Log("Chat Games/Logs", "CCG-log*.txt", writeLogKey: "logToFile", columns: new[] { "Player|User", "Platform|Platform", "Trigger|Trigger", "Bet|Bet", "Change|Change", "After|After" }, results: new[] { "WIN", "LOSS", "JACKPOT", "REFUND" }, platforms: new[] { "twitch", "youtube", "kick" }, emptyText: "No gambles logged yet. Every gamble lands here once 'Write a log file per day' is on.")
        .Help(
            H("A gamble",
                Note("A viewer runs !gamble with a bet, and the bet rolls against the odds on the Game page: jackpot, win or loss. Wins and jackpots are paid on top of the bet; a loss takes it."),
                Cmd("!gamble", "plays for the Default bet. With a Default bet of 0, viewers always have to name an amount."),
                Cmd("!gamble 500", "plays for 500, within the minimum and maximum. Also 50%, 10k, 2.5m, 1b, all, half, third, quarter and random."),
                Cmd("!SetFreeGamble", "moderators: turns Free Game Mode on. !EndFreeGamble turns it off and brings the Default bet back."),
                Cmd("!gambleaddict add <user>", "moderators: puts a viewer on the addicted list; remove takes them off.")),
            H("How a gamble is decided",
                Note("The game rolls 1 to 10,000. A roll within the jackpot chance is a jackpot: the bet times a multiplier between Min × and Max ×, times the Jackpot ×. A roll within the win chance on top of that is a win: the bet times the multiplier. Everything else loses the bet. Twitch subscribers and VIPs can get a boost on wins and jackpots; viewers on the Addicted list get a cut.")),
            H("After a gamble",
                Note("GetLastGambleResult, run with an Execute C# Method sub-action, hands your own actions the outcome, the amounts and the balances as arguments. The custom triggers CCG: Gamble Win, Gamble Loss, Gamble Jackpot, Free Game Start and Free Game End start your actions at the right moment.")),
            H("Where things are kept",
                Cmd("configs.json", "everything on these pages but the messages."),
                Cmd("messages.json", "every chat message."),
                Cmd("uiframework.layout.json", "your layout of this window: theme, renamed or hidden pages and fields."),
                Cmd("Logs\\CCG-log-yyyy-MM-dd.txt", "every gamble and refund, one file per day, while 'Write a log file per day' is on."),
                Note("All of it sits in Streamer.bot's folder under Chat Games. A damaged file falls back to the .bak beside it and the broken one is kept as .broken.")))
        .About(credits: "Classic Chat Gamble by aaskjer / yBO",
            sections: new[]
            {
                H("Needs a points system",
                    Note("!gamble plays with the points of a currency system; Twitch channel points work without one. These two are found on their own:"),
                    Link("Currency Core by StreamUP", "https://streamup.tips/product/currency-core"),
                    Link("LOYALTY POINTS by tawmae", "https://tawmae.xyz/loyalty-points"))
            }, links: new[] { Link("Classic Chat Gamble on the Streamer.bot Discord", "https://discord.com/channels/834650675224248362/1446874050310836396/1446874050310836396") })
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
        S("fairness", "classic", page: "game", label: "Preset", help: "One slider for the odds, from Strict, where the house wins and points drain away, to Classic, the default with rare, huge jackpots where points grow fast. A stop fills in the five numbers below; nothing is saved until you save. Custom is where the slider goes when the numbers are your own.", kind: "slider", section: "Fairness", options: new[] { "strict|Strict", "houseEdge|House Edge", "fair|Fair", "generous|Generous", "classic|Classic", "custom|Custom" }, transient: true, live: true),
        S("presetNote", null, page: "game", help: "A stop of the slider sets the jackpot and win chance, the jackpot multiplier and the win multiplier range in one go. The game only ever reads those five numbers, so you can still fine-tune them: the slider then shows Custom, or the stop they match.", kind: "note", section: "Fairness"),
        S("jackpotPercent", 1.0, page: "game", label: "Jackpot chance", help: "Chance in percent, from 0 (never) to 100 (always), that a gamble hits the jackpot. Keep it small so jackpots stay rare: '1' is 1 in 100.", kind: "percent", section: "Fairness", min: 0.0, max: 100.0, aliases: new[] { "JackpotPercent" }),
        S("jackpotMultiplier", 43, page: "game", label: "Jackpot ×", help: "Multiplier stacked on top of the win roll when a jackpot hits. With '43', a 2× roll pays 2 × 43 = 86 times the bet.", section: "Fairness", min: 1.0, aliases: new[] { "JackpotMultiplier" }),
        S("winPercent", 30.0, page: "game", label: "Win chance", help: "Chance in percent that a gamble is a normal win, on top of the jackpot chance. Jackpot '1' and Win '30' leave a 69 % chance to lose.", kind: "percent", section: "Fairness", min: 0.0, max: 100.0, aliases: new[] { "WinPercent" }),
        S("rndMin", 1.5, page: "game", label: "Min ×", help: "The smallest win multiplier. Every win rolls a multiplier between Min and Max, and a jackpot stacks on that roll.", kind: "double", section: "Fairness", min: 0.0, aliases: new[] { "RndMin" }),
        S("rndMax", 2.5, page: "game", label: "Max ×", help: "The largest win multiplier.", kind: "double", section: "Fairness", min: 0.0, aliases: new[] { "RndMax" }),
        S("odds", null, page: "game", label: "What really comes out", help: "The chance of a jackpot, a win and a loss, and what a bet of 100 comes back as on average, from the five numbers as they stand, for a viewer without Subscriber/VIP boost or Addicted cut.", kind: "action", section: "Fairness", live: true, panel: true, auto: true),
        S("defaultBet", 1234, page: "game", label: "Default bet", help: "The bet for a plain !gamble with no amount. 0 means viewers always have to name one. Viewers can also bet a number, a percentage, shorthand like 10k, or all, half, third, quarter and random.", section: "Bets and points", min: 0.0, aliases: new[] { "DefaultBet" }),
        S("minBet", 0, page: "game", label: "Minimum (0 = none)", help: "The smallest bet allowed. 0 turns the limit off. Also applies to channel points in 'Typed amount' and 'Fixed' mode.", section: "Bets and points", min: 0.0, aliases: new[] { "MinBet" }),
        S("maxBet", 9999999, page: "game", label: "Maximum (0 = none)", help: "The largest bet allowed. 0 turns the limit off. Also applies to channel points in 'Typed amount' and 'Fixed' mode.", section: "Bets and points", min: 0.0, aliases: new[] { "MaxBet" }),
        S("freeGame", false, page: "game", label: "Free Game Mode", help: "Nobody pays or wins points, channel point redemptions included, and chat gets the Free game messages. !SetFreeGamble and !EndFreeGamble switch it from chat; starting it there puts the Default bet to 0 and ending it brings the previous one back.", section: "Bets and points", aliases: new[] { "FreeGameActive" }),
        S("previousDefaultBet", 1234, page: "game", help: "The Default bet from before !SetFreeGamble, brought back by !EndFreeGamble.", section: "Bets and points", min: 0.0, hidden: true, aliases: new[] { "PreviousDefaultBet" }),
        S("pointsVariable", "points", page: "game", label: "Points variable", help: "The variable your points live in. Currency Core by StreamUP and LOYALTY POINTS by tawmae are found on their own; only change this for a points system of your own.", section: "Bets and points", aliases: new[] { "PointsVar" }),
        S("pointsName", "Points", page: "game", label: "Shown in chat as", help: "The name {pointsName} puts in chat, like 'Coins' or 'Gems'. Currency Core and LOYALTY POINTS bring their own name, which is used instead.", section: "Bets and points", aliases: new[] { "PointsName" }),
        S("subscriberBoostEnabled", false, page: "game", label: "Subscriber boost", help: "Gives Twitch subscribers a bigger payout on wins and jackpots. Twitch only: on YouTube and Kick the game doesn't check for subscribers.", section: "Payouts", aliases: new[] { "SubscriberBoostEnabled" }, twitchOnly: true),
        S("vipBoostEnabled", false, page: "game", label: "VIP boost", help: "Gives Twitch VIPs a bigger payout on wins and jackpots. Twitch only: on YouTube and Kick the game doesn't check for VIPs.", section: "Payouts", aliases: new[] { "VipBoostEnabled" }, twitchOnly: true),
        S("subscriberBoostPercent", 10.0, page: "game", label: "Subscriber boost", help: "How much bigger a subscriber's win is: '10' turns 100 into 110. Adds up with the VIP boost for someone who is both.", kind: "double", section: "Payouts", min: 0.0, unit: "%", requires: "subscriberBoostEnabled", aliases: new[] { "SubscriberBoostPercent" }),
        S("vipBoostPercent", 20.0, page: "game", label: "VIP boost", help: "How much bigger a VIP's win is: '20' turns 100 into 120. Adds up with the Subscriber boost for someone who is both.", kind: "double", section: "Payouts", min: 0.0, unit: "%", requires: "vipBoostEnabled", aliases: new[] { "VipBoostPercent" }),
        S("addictedPayoutCut", 50, page: "game", label: "Addicted cut", help: "How much of an addicted viewer's win is cut, from 0 (nothing) to 100 (all of it). Applied last, so it cuts a boosted win too - part of the boost is kept. Who counts as addicted is set on the Access page.", kind: "percent", section: "Payouts", min: 0.0, max: 100.0, aliases: new[] { "AddictedPayoutCutPercent" }),
        S("tryBet", 1000, page: "game", label: "Bet to try", help: "A bet to try the odds and messages with. It isn't saved.", section: "Try a bet", min: 1.0, transient: true, row: "try"),
        S("tryRole", "viewer", page: "game", label: "Play as", help: "Who plays: boosts and the Addicted cut change what a win pays.", kind: "options", section: "Try a bet", options: new[] { "viewer|Viewer", "subscriber|Subscriber", "vip|VIP", "addicted|Addicted" }, transient: true, row: "try"),
        S("tryRoll", null, page: "game", label: "Roll", help: "Plays one gamble with the settings as they stand on this page, saved or not, and shows the roll and the message chat would get. Nothing is sent and nothing is saved.", kind: "action", section: "Try a bet", live: true, panel: true),
        // ---- access ----
        S("platformsNote", null, page: "access", help: "Twitch, YouTube and Kick all play, and every reply goes to the chat the command or redemption came from. To leave a platform out, untick it on the !gamble command in Streamer.bot, for example 'Kick Message'.", kind: "note", section: "Platforms"),
        S("isChannelPointsTrigger", false, page: "access", label: "Channel points start a gamble", help: "On Twitch, a redemption of the reward below plays instead of !gamble, which is then off on Twitch. Redemptions only come from Twitch, so on YouTube and Kick viewers keep playing with !gamble.", section: "Channel points (Twitch)", aliases: new[] { "IsChannelPointsTrigger" }, twitchOnly: true),
        S("rewardId", "", page: "access", label: "Reward ID", help: "The ID of the channel point reward that starts a gamble. Other rewards are ignored; without one, no redemption starts a gamble. https://ayrtonalexis.com/reward_checker/reward_check.html lists your rewards with their IDs.", section: "Channel points (Twitch)", requires: "isChannelPointsTrigger", aliases: new[] { "TargetRewardId" }),
        S("cpBetMode", "cost", page: "access", label: "What is bet", help: "Reward cost: the reward's price is the bet, so viewers don't type anything; a loss doesn't touch their points, a win is paid in points. Typed amount: viewers type a bet into the reward, paid from their own points, checked against their balance; without enough points the redemption is refunded. Fixed: every redemption bets the fixed amount below, the price and the text are ignored, a loss doesn't touch the viewer's points. Free: nothing is won or lost. Minimum and Maximum bet apply to Typed amount and Fixed.", kind: "options", section: "Channel points (Twitch)", options: new[] { "cost|Reward cost", "input|Typed amount", "fixed|Fixed", "free|Free" }, requires: "isChannelPointsTrigger", aliases: new[] { "CPBetMode" }),
        S("fixedBetAmount", 0, page: "access", label: "Fixed bet", help: "What every redemption bets in Fixed mode. Must be more than 0.", section: "Channel points (Twitch)", min: 0.0, requires: "cpBetMode=fixed", aliases: new[] { "FixedBetAmount" }),
        S("refundFailedRedemptions", true, page: "access", label: "Refund redemptions that don't end in a gamble", help: "Viewer blocked, cooldown running or a bet that doesn't work: the redemption is cancelled and the channel points go back. Only works while the redemption is still queued - a reward that skips the request queue can't be refunded.", section: "Channel points (Twitch)", requires: "isChannelPointsTrigger", aliases: new[] { "RefundFailedRedemptions" }),
        S("globalCooldown", 0, page: "access", label: "Everyone", help: "How long everyone waits after any gamble, in seconds. 0 turns it off. Saving writes it onto the !gamble command. A channel point redemption during the cooldown is refunded; redemptions keep a clock of their own, so they don't hold up !gamble on YouTube or Kick, nor the other way round.", section: "Cooldowns", min: 0.0, aliases: new[] { "CooldownGlobalSeconds" }),
        S("userCooldown", 0, page: "access", label: "Per viewer", help: "How long one viewer waits before their next gamble, in seconds. 0 turns it off. Saving writes it onto the !gamble command. A channel point redemption during the cooldown is refunded.", section: "Cooldowns", min: 0.0, aliases: new[] { "CooldownUserSeconds" }),
        S("cooldownNote", null, page: "access", help: "What chat is told while a cooldown runs is on the Messages page, under Cooldown.", kind: "note", section: "Cooldowns"),
        S("blockedUsers", "", page: "access", label: "Blocked viewers", help: "Viewers who can't play. A name or a platform user ID - with the ID, a name change doesn't get around it.", section: "Who may not play", aliases: new[] { "BlockedUsers" }),
        S("blockedGroups", "", page: "access", label: "Blocked groups", help: "Streamer.bot user groups whose members can't play. Names must match the groups exactly.", section: "Who may not play", aliases: new[] { "BlockedGroups" }),
        S("addictedUsers", "", page: "access", label: "Addicted viewers", help: "Their wins are cut by the Addicted cut on the Game page, and they get the Addicted messages. !gambleaddict adds and removes them from chat too.", section: "Addicted", aliases: new[] { "AddictedUsers" }),
        S("addictedGroups", "", page: "access", label: "Addicted groups", help: "Members of these Streamer.bot groups count as addicted too.", section: "Addicted", aliases: new[] { "AddictedGroups" }),
        // ---- log ----
        S("logToFile", true, page: "log", label: "Write a log file per day", help: "Writes every gamble and refund - win, loss, jackpot - with the bet and the balance to a dated 'CCG-log-yyyy-MM-dd.txt' in the Logs folder, one file per day.", aliases: new[] { "LogToFile" }),
        // ---- messages, kept in messages.json next to configs.json ----
        Msg("Results", "winMessage", "@{user}, WINS! 🎉 You got {amount} {pointsName}! New balance: {currentPoints} {pointsName}.", "Win", "Sent on a normal win.", new[] { "user", "amount", "pointsName", "currentPoints" }, file: "messages.json", field: "WinMessage"),
        Msg("Results", "lossMessage", "@{user}, unlucky! You LOST {amount} {pointsName}. New balance: {currentPoints} {pointsName}.", "Loss", "Sent on a loss.", new[] { "user", "amount", "pointsName", "currentPoints" }, file: "messages.json", field: "LossMessage"),
        Msg("Results", "jackpotMessage", "🎰 J A C K P O T 🎰 @{user} you WON the JACKPOT of: {amount} {pointsName}! New balance: {currentPoints} {pointsName}.", "Jackpot", "Sent on a jackpot.", new[] { "user", "amount", "pointsName", "currentPoints" }, file: "messages.json", field: "JackpotMessage"),
        Msg("Free game", "freeGameMessage", "@{user}, gamble runs in 'Free Mode'! Enjoy playing without spending any {pointsName}.", "Notice, once per viewer", "Sent once to each viewer who plays while Free Game Mode is on.", new[] { "user", "pointsName" }, file: "messages.json", field: "FreeGameMessage"),
        Msg("Free game", "freeGameWinMessage", "🎉 @{user}, you WON! Lucky you! 🎉", "Win", "Sent on a win while Free Game Mode is on.", new[] { "user" }, file: "messages.json", field: "FreeGameWinMessage"),
        Msg("Free game", "freeGameLossMessage", "@{user}, unlucky! Luckily it's Free Mode, so you lost nothing. 🍀", "Loss", "Sent on a loss while Free Game Mode is on. Nothing is taken.", new[] { "user", "pointsName" }, file: "messages.json", field: "FreeGameLossMessage"),
        Msg("Free game", "freeGameJackpotMessage", "🎰 J A C K P O T 🎰 @{user}, you hit the JACKPOT! Incredible! 🎰", "Jackpot", "Sent on a jackpot while Free Game Mode is on.", new[] { "user" }, file: "messages.json", field: "FreeGameJackpotMessage"),
        Msg("Free game", "freeGameStartMessage", "🎲 Free Gamble Mode is now ACTIVE! Gamble for free — no points needed!", "Started", "Sent once when Free Game Mode is turned on, in the chat the command or redemption came from. Started any other way, say by an Execute C# Method on StartFreeGame in an action of your own, it goes to every platform with an account in Streamer.bot - to YouTube only while a broadcast is monitored.", new string[0], file: "messages.json", field: "FreeGameStartMessage"),
        Msg("Free game", "freeGameEndMessage", "⏹️ Free Gamble Mode has ended. Normal betting is back, time to waste points!", "Ended", "Sent once when Free Game Mode ends and the Default bet is back, in the chat the command or redemption came from. Ended any other way, say by an Execute C# Method on EndFreeGame in an action of your own, it goes to every platform with an account in Streamer.bot - to YouTube only while a broadcast is monitored.", new string[0], file: "messages.json", field: "FreeGameEndMessage"),
        Msg("Addicted", "addictedWinMessage", "@{user}, WINS! 🎉 You got {amount} {pointsName}! -- Addiction Fee: your win was cut by {addictedCut}% -- New balance: {currentPoints} {pointsName}.", "Win", "Sent instead of the Win message to viewers on the Addicted list.", new[] { "user", "amount", "pointsName", "currentPoints", "addictedCut" }, file: "messages.json", field: "AddictedWinMessage"),
        Msg("Addicted", "addictedJackpotMessage", "🎰 J A C K P O T 🎰 @{user} you WON the JACKPOT of: {amount} {pointsName}! -- Addiction Fee: your win was cut by {addictedCut}% -- New balance: {currentPoints} {pointsName}.", "Jackpot", "Sent instead of the Jackpot message to viewers on the Addicted list.", new[] { "user", "amount", "pointsName", "currentPoints", "addictedCut" }, file: "messages.json", field: "AddictedJackpotMessage"),
        Msg("Addicted", "addictedAdded", "@{user}, {target} has been added to the addicted list.", "Added", "Sent when !gambleaddict puts someone on the list.", new[] { "user", "target" }, file: "messages.json", field: "AddictedAdded"),
        Msg("Addicted", "addictedRemoved", "@{user}, {target} has been removed from the addicted list.", "Removed", "Sent when !gambleaddict remove takes someone off the list.", new[] { "user", "target" }, file: "messages.json", field: "AddictedRemoved"),
        Msg("Addicted", "addictedAlreadyOnList", "@{user}, {target} is already on the addicted list.", "Already on the list", "Sent when !gambleaddict names someone who is already on the list.", new[] { "user", "target" }, file: "messages.json", field: "AddictedAlreadyOnList"),
        Msg("Addicted", "addictedNotOnList", "@{user}, {target} is not on the addicted list.", "Not on the list", "Sent when !gambleaddict remove names someone who isn't on the list.", new[] { "user", "target" }, file: "messages.json", field: "AddictedNotOnList"),
        Msg("Addicted", "addictedUsage", "@{user}, usage: !gambleaddict add <user> or !gambleaddict remove <user>", "Usage", "Sent when !gambleaddict comes without a name.", new[] { "user" }, file: "messages.json", field: "AddictedUsage"),
        Msg("Bets", "invalidBetMessage", "❌ @{user}, {reason}", "Bet refused", "The frame around the five reasons below; {reason} is replaced by whichever one applies.", new[] { "user", "reason" }, file: "messages.json", field: "InvalidBetMessage"),
        Msg("Bets", "reasonNegativeAmount", "that's not a valid amount: no decimals, nothing negative and nothing too large.", "Reason: not a valid amount", "Fills {reason} when the amount is negative, not a number, has decimals, or is too large.", new string[0], file: "messages.json", field: "ReasonNegativeAmount"),
        Msg("Bets", "reasonInsufficientFunds", "you don't have enough {pointsName}.", "Reason: not enough points", "Fills {reason} when the bet is more than the viewer has.", new[] { "pointsName" }, file: "messages.json", field: "ReasonInsufficientFunds"),
        Msg("Bets", "reasonBelowMinimum", "your bet is below the allowed minimum ({min} {pointsName}).", "Reason: below the minimum", "Fills {reason} when the bet is below the minimum on the Game page.", new[] { "min", "pointsName" }, file: "messages.json", field: "ReasonBelowMinimum"),
        Msg("Bets", "reasonAboveMaximum", "your bet is above the allowed maximum ({max} {pointsName}).", "Reason: above the maximum", "Fills {reason} when the bet is above the maximum on the Game page.", new[] { "max", "pointsName" }, file: "messages.json", field: "ReasonAboveMaximum"),
        Msg("Bets", "reasonNoInput", "please name an amount, or ask the streamer to set a Default bet.", "Reason: no amount", "Fills {reason} when no amount was given: Default bet is 0, or a Typed amount redemption came without one.", new string[0], file: "messages.json", field: "ReasonNoInput"),
        Msg("Cooldown", "globalCooldownMessage", "Please wait '{seconds}' seconds before trying again ⏳", "Everyone", "Sent while the cooldown for everyone runs.", new[] { "seconds", "user" }, file: "messages.json", field: "GlobalMessage"),
        Msg("Cooldown", "userCooldownMessage", "@{user}, please wait '{seconds}' seconds before trying again ⏳", "Per viewer", "Sent while this viewer's own cooldown runs.", new[] { "user", "seconds" }, file: "messages.json", field: "UserMessage"),
        Msg("Channel points", "redemptionRefundedMessage", "↩️ @{user}, your channel point redemption was refunded.", "Refunded", "Sent when a redemption is refunded.", new[] { "user" }, file: "messages.json", field: "RedemptionRefunded"),
    };
}

/// <summary>How the window is called and laid out, declared beside the table: what the framework's docs call the
/// spec (docs/Schema.md, "The window"). Every part is optional; a page a setting names but the window does not is
/// made up on the way, help and about always sit at the bottom.</summary>
public sealed class UiWindow
{
    public string Title, Releases, Logo, LogPrefix, Language;
    public bool Locked;
    public double? Width, Height;
    public readonly System.Collections.Generic.List<object> PageList = new System.Collections.Generic.List<object>();
    public readonly Newtonsoft.Json.Linq.JObject SampleMap = new Newtonsoft.Json.Linq.JObject(), AnswerMap = new Newtonsoft.Json.Linq.JObject(), OverrideMap = new Newtonsoft.Json.Linq.JObject();
    public Newtonsoft.Json.Linq.JObject SenderMap, LogMap, AboutMap;
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
        if (request == null && args.ContainsKey("uiframework.describe")) request = "describe";
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
