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
    public static readonly UiWindow Window = new UiWindow("Blackjack Game", releases: "aaskjer/Blackjack-Game", logo: "https://github.com/aaskjer/Blackjack-Game/blob/main/Assets/logo.png?raw=true", logPrefix: "[Blackjack Game/Settings]")
        .Pages(P("table", "Table", "🃏"), P("duel", "Duel", "⚔️"), P("house", "House", "🏠"), P("access", "Access", "👥"), P("overlay", "Overlay", "📺"), P("messages", "Messages", "💬"), P("stats", "Stats", "🏆", "leaderboard*", "resetLeaderboard"), P("log", "Log", "📜"), P("help", "How to play", "📖"), P("about", "About", "ℹ️"))
        .Senders(useBot: "useBot", sendAction: "sendAction", cooldownUseBot: "cooldownUseBot", cooldownSendAction: "cooldownSendAction", numberSeparator: "numberSeparator", cooldownGroup: "Cooldown")
        .Cooldowns(
            C("c6497fbd-c4b0-4c96-a398-e52d573cfe85", "globalCooldown", "userCooldown"),             // !blackjack
            C("8ba7b2b7-f9f9-4b70-8cca-ee2aaebe4892", "duelGlobalCooldown", "duelUserCooldown"),     // !duel
            C("57cccfe6-3ece-428e-8567-b99823a12dac", "houseGlobalCooldown", "houseUserCooldown"))   // !house
        // German: the game's own keys and texts; sections, pages and the shared keys come from the conventions.
        .Lang("de",
            K("overlaySample", "Beispielrunde zeigen", "Spielt etwa zehn Sekunden lang eine erfundene Hand auf dem Overlay, dem in OBS eingeschlossen, damit du siehst, wo es sitzt und wie es aussieht. Bist du live, sehen es auch deine Zuschauer. Nutzt die gespeicherten Einstellungen; niemandes Punkte ändern sich. Läuft gerade eine Hand, ein Duell oder ein House-Tisch, bleibt das Overlay bei ihm."),
            K("overlayDemo", "Demo ansehen", "Öffnet das Overlay im Browser und spielt erfundene Hände, von der Solo-Hand bis zum Duell und zum House-Tisch, im Aussehen der gespeicherten Einstellungen, ohne Streamer.bot. Mit &demo=1 an der URL der Quelle in OBS lässt sie sich genauso platzieren."),
            Pg("table", "Tisch"),
            Pg("duel", "Duell"),
            Pg("house", "Haus"),
            K("dealerStandsOn", "Bleibt stehen bei", "Der Dealer zieht, bis die Hand diesen Wert erreicht, dann hört er auf. 17 ist der Casino-Standard."),
            K("numDecks", "Decks im Schuh", "Wie viele Decks zu 52 Karten zusammengemischt werden."),
            K("reshuffleThreshold", "Neu mischen bei", "Wie wenig vom Schuh übrig sein darf, bevor die gespielten Karten wieder hineingemischt werden.\nNiedrig heißt lange Strecken zwischen den Mischungen und ein Zählen, das sich lohnt; hoch heißt, es wird so oft gemischt, dass Zählen niemandem etwas bringt."),
            K("dealerDrawDelayMs", "Pause beim Aufdecken", "Pause zwischen den Karten des Dealers, in Millisekunden, damit der Chat mitkommt. 0 = alles auf einmal."),
            K("tieDrawOnlyOneCard", "Gleichstand mit nur einer Karte brechen", "An: der Dealer zieht bei Gleichstand genau eine Karte, und ein Gleichstand, der sie überlebt, zählt als Sieg des Dealers.\nAus: der Dealer zieht weiter, bis der Gleichstand in die eine oder andere Richtung bricht."),
            K("showAceMsg", "Sagen, wenn ein Ass auf 1 fällt", "Postet eine Zeile, wann immer ein Ass als 1 statt 11 zählt, um eine Hand unter 22 zu halten."),
            K("allowDoubleDown", "Verdoppeln", "Verdoppelt den Einsatz für genau eine weitere Karte, dann wird gehalten. Nur auf einer frischen Hand aus zwei Karten, vor allem anderen."),
            K("allowSplit", "Teilen", "Teilt ein Startpaar gleichwertiger Karten in zwei Hände, jede für denselben Einsatz ausgespielt."),
            K("allowResplit", "Erneut teilen", "Lässt eine geteilte Hand noch einmal teilen, wenn sie wieder ein passendes Paar zieht. Gilt auch für geteilte Asse."),
            K("maxSplitHands", "Hände durch Teilen", "Wie viele Hände eine Runde durch Teilen höchstens erreicht. Vier ist der Casino-Standard."),
            K("allowInsurance", "Versicherung", "Zeigt der Dealer ein Ass, darf ein Spieler die Hälfte seines Einsatzes gegen einen Blackjack des Dealers setzen. Zahlt 2:1.\nAuf Dauer ist es eine Nebenwette, die der Spieler verliert, deshalb ist sie standardmäßig aus."),
            K("allowSurrender", "Aufgeben", "Lässt einen Spieler eine frische Hand aus zwei Karten aufgeben und die Hälfte des Einsatzes behalten, vor Ziehen, Verdoppeln, Teilen oder Versichern."),
            K("dealerPeeks", "Dealer prüft auf Blackjack", "An: der Dealer nimmt beim Geben eine verdeckte Karte und prüft sie, sobald die offene Karte ein Ass oder eine Zehn ist."),
            K("timeoutRefundIfNoAction", "Ungespielte Hand erstatten", "An: ein Spieler, der nie gezogen, gehalten, verdoppelt oder geteilt hat, bekommt den Einsatz zurück, genau als hätte er aufgehört."),
            K("blackjackPayout", "Blackjack zahlt", "Der Bonus auf einen echten Blackjack - 21 mit den ersten zwei Karten - zusätzlich zum zurückgezahlten Einsatz.\nEin normaler Gewinn zahlt immer 1:1."),
            K("duelCountsTowardGamesPerHour", "Duelle zählen zu den Spielen pro Stunde", "Zählt ein Duell für beide Spieler als Spiel gegen die Grenze Spiele pro Stunde auf der Seite Tisch."),
            K("duelApplyAddictedCut", "Duellgewinne Süchtiger kürzen", "Standardmäßig aus. Die Punkte in einem Duell kommen vom anderen Spieler, nicht vom Haus, der Abzug würde sie also aus dem Umlauf löschen statt sie zu bewegen."),
            K("duelMinBet", "Mindesteinsatz im Duell", "Alles darunter wird darauf angehoben. Getrennt vom Tisch-Minimum, damit Duelle eine eigene Untergrenze haben können."),
            K("duelMaxBet", "Höchsteinsatz im Duell", "Alles darüber wird darauf gekürzt.\n0 = keine Obergrenze."),
            K("duelAcceptTimeoutSec", "Auf eine Herausforderung antworten", "Wie lange der Herausgeforderte Zeit hat, bevor die Herausforderung verfällt und der Einsatz zurückgeht, in Sekunden.\nMindestens 10."),
            K("duelTurnTimeoutSec", "Am Zug sein", "Wie lange ein Spieler handeln kann, bevor er automatisch hält, in Sekunden. 0 = keine Grenze."),
            K("duelConsentTimeoutSec", "Auf eine Erhöhung antworten", "Wie lange der Gegner Zeit hat, auf ein Teilen oder Verdoppeln zu antworten, und wie lange beide für ein Sudden-Death-Angebot haben.\nMindestens 5."),
            K("duelAllowDoubleDown", "Verdoppeln", "Lässt einen Spieler seinen Einsatz für eine weitere Karte verdoppeln, sobald der Gegner zustimmt."),
            K("duelAllowSplit", "Teilen", "Lässt einen Spieler ein Paar teilen, sobald der Gegner zustimmt. Jede Hand spielt dann um einen eigenen, gleich gefüllten Nebentopf, und ein Spieler mit weniger Händen verteidigt jeden Topf mit der einen, die er hat."),
            K("duelSuddenDeathEnabled", "Bei Gleichstand anbieten", "Nur bei einem ungeteilten Duell - ein einziger Topf - das in einem echten Gleichstand endet. Duelle mit Teilungen enden immer unentschieden."),
            K("duelMaxSuddenDeathRounds", "Höchstens Runden", "Wie oft Sudden Death sich wiederholen darf, solange die Spieler weiter gleichziehen. Nach der letzten wird der Topf geteilt."),
            K("houseEnabled", "Haus-Modus", "Aus: der Haus-Befehl antwortet nur noch, dass der Haus-Modus abgeschaltet ist."),
            K("houseCountsTowardGamesPerHour", "Plätze zählen zu den Spielen pro Stunde", "Zählt das Platznehmen für diesen Spieler als Spiel gegen die Grenze Spiele pro Stunde auf der Seite Tisch."),
            K("houseMaxSeats", "Plätze", "Wie viele Spieler sich setzen können. Der Tisch gibt, sobald der letzte Platz belegt ist, ohne das Beitrittsfenster abzuwarten."),
            K("houseRulePickTimeoutSec", "Regelwahl", "Wie lange der Moderator Zeit hat, auf das Regelsatz-Menü zu antworten, in Sekunden. Noch ist nichts gesetzt, der Tisch wird einfach fallen gelassen.\nMindestens 10."),
            K("houseJoinWindowSec", "Beitrittsfenster", "Wie lange Zuschauer Zeit haben, Platz zu nehmen, bevor der Tisch von selbst gibt, in Sekunden. Ohne Besetzung schließt er wieder.\nMindestens 15."),
            K("houseInsuranceWindowSec", "Versicherungsfenster", "Wie lange die Plätze zum Versichern haben, wenn der Dealer ein Ass zeigt, bevor der Dealer die verdeckte Karte ansieht, in Sekunden.\nNur bei Regelsätzen mit Versicherung. Mindestens 5."),
            K("houseTurnTimeoutSec", "Bedenkzeit je Platz", "Wie lange ein Platz überlegen darf, bevor er automatisch hält und das Spiel weitergeht, in Sekunden. 0 = keine Grenze."),
            K("houseMinBet", "Mindesteinsatz je Platz", "Alles darunter wird darauf angehoben. Der Spieler braucht trotzdem das Guthaben dafür."),
            K("houseMaxBet", "Höchsteinsatz je Platz", "Alles darüber wird darauf gekürzt.\n0 = keine Obergrenze."),
            K("houseRuleVegasEnabled", "Vegas Strip", "4 Decks im Schuh, Dealer hält bei Soft 17, Versicherung angeboten, Blackjack zahlt 3:2."),
            K("houseRuleAtlanticEnabled", "Atlantic City", "8 Decks im Schuh, Dealer hält bei Soft 17, spätes Aufgeben, erneut teilen bis drei Hände."),
            K("houseRuleEuropeanEnabled", "Europäisch", "2 Decks im Schuh, keine verdeckte Karte, Verdoppeln nur auf 9 bis 11, kein Aufgeben."),
            K("cardDisplayMode", "Karten im Chat", "Wie Karten im Chat geschrieben werden, in jedem Modus. Lohnt sich weg von den Kartenzeichen, wenn dein Chat sie als Kästchen zeigt."),
            K("duelGlobalCooldown", "Duell, alle", "Wie lange nach einer Herausforderung niemand eine weitere aussprechen kann, in Sekunden."),
            K("duelUserCooldown", "Duell, je Spieler", "Wie lange ein Spieler zwischen zwei Herausforderungen wartet, in Sekunden.\nEine Herausforderung anzunehmen wird nie blockiert, ein Duell lässt sich also immer zu Ende spielen."),
            K("houseGlobalCooldown", "Haus, alle", "Wie lange nach dem Öffnen eines Tisches niemand einen weiteren öffnen kann, in Sekunden."),
            K("houseUserCooldown", "Haus, je Spieler", "Wie lange ein Spieler zwischen dem Öffnen zweier Tische wartet, in Sekunden.\nAm Tisch eines anderen Platz zu nehmen wird nie blockiert."),
            K("overlayShowPoints", "Punkte und Einsätze", "Zeigt Einsatz und Versicherungsbeträge. Ausschalten, um Zahlen aus dem Stream zu halten."),
            K("overlayShowHandValues", "Handwerte", "Zeigt den laufenden Wert neben jeder Hand und der des Dealers."),
            K("overlayShowOutcomeBadges", "Ergebnis-Plaketten", "Zeigt BLACKJACK, BUST, WIN, LOSE und PUSH neben einer Hand, sobald sie abgerechnet ist."),
            K("overlayShowInsurance", "Versicherungszeile", "Zeigt eine Zeile unter den Karten des Dealers, solange ein Spieler versichert ist."),
            K("overlayDealerName", "Der Dealer heißt", "Die Beschriftung über den Karten des Dealers."),
            K("overlayCardRedColor", "Rote Farben", "Herz und Karo."),
            K("overlayCardBlackColor", "Schwarze Farben", "Pik und Kreuz."),
            K("msgGameStart", "Hand gegeben", "Eröffnet eine Runde. Die Hand und die offene Karte des Dealers werden angehängt."),
            K("msgDrawCard", "Karte", "Jedes Mal gesendet, wenn der Spieler zieht, Bust oder nicht."),
            K("msgDoubleDown", "Verdoppelt", "Gesendet, wenn der Einsatz für genau eine weitere Karte verdoppelt wird."),
            K("msgSplit", "Geteilt", "Gesendet bei einem Teilen oder erneuten Teilen, beschreibt die erste der zwei Hände."),
            K("msgMovingToHand", "Nächste geteilte Hand", "Gesendet, wenn das Spiel zur nächsten Hand weitergeht. Hände, die von selbst halten, wie geteilte Asse, werden übersprungen."),
            K("msgAceReduced", "Ass auf 1 gefallen", "Nur gesendet, solange 'Sagen, wenn ein Ass auf 1 fällt' an ist, und nur, wenn es wirklich passiert ist."),
            K("msgQuitRefund", "Aufgehört", "Gesendet, wenn ein Spieler aufhört, bevor er gehandelt hat. Alles Gesetzte kommt zurück, geteilte Einsätze eingeschlossen."),
            K("msgBlackjackWin", "Blackjack", "Gesendet bei einem echten Blackjack - 21 mit den ersten zwei Karten - bezahlt zum Verhältnis von Blackjack zahlt statt 1:1."),
            K("msgRegularWin", "Gewinn", "Gesendet bei einem normalen 1:1-Gewinn: die Hand des Spielers schlägt die des Dealers, keine davon ein Blackjack."),
            K("msgHandWin", "Geteilte Hand gewonnen", "Angehängt, wenn eine Hand aus einer Teilung ihren eigenen Vergleich gegen den Dealer gewinnt."),
            K("msgSpecialWin5Card", "5-Karten-Charlie", "Gesendet, sobald eine Hand fünf Karten erreicht, ohne zu busten. Zahlt den Einsatz zurück plus einen 1,5-fachen Bonus, egal was der Dealer hält."),
            K("msgSpecialWin3Sevens", "Drei 7er", "Gesendet, sobald eine Hand drei 7er hält, ohne zu busten. Zahlt den Einsatz zurück plus einen 2-fachen Bonus, egal was der Dealer hält."),
            K("msgLoss", "Verlust", "Gesendet, wenn die Hand des Spielers schlicht nicht reicht - kein Bust, kein Gleichstand."),
            K("msgBust", "Bust", "Gesendet, sobald eine Hand bei einem Zug oder Verdoppeln über 21 geht. Der Einsatz ist weg, bevor der Dealer spielt."),
            K("msgHandBust", "Geteilte Hand gebustet", "Statt Bust gesendet, wenn die Hand, die über 21 ging, Teil einer Teilung war."),
            K("msgHandLost", "Geteilte Hand verloren", "Angehängt, wenn eine Hand aus einer Teilung ihren eigenen Vergleich gegen den Dealer verliert."),
            K("msgHandTie", "Geteilte Hand im Gleichstand", "Geht als {hands} in die Ansage des Stechens ein und nennt die Hand, die gleichgezogen hat."),
            K("msgDealerDraws", "Dealer zieht", "Für jede Karte des Dealers gesendet, beim Ziehen bis zum Haltewert wie beim Brechen eines Gleichstands."),
            K("msgDealerReveal", "Verdeckte Karte aufgedeckt", "Nur mit 'Dealer prüft auf Blackjack' an zu sehen, denn nur dann gibt es eine verdeckte Karte."),
            K("msgDealerBusts", "Dealer bustet", "Gesendet, wenn eine Stechkarte den Dealer über 21 bringt, sodass die Hand, die gleichgezogen hatte, doch gewinnt."),
            K("msgDealerWins", "Dealer gewinnt unberührt", "Gesendet, wenn jede Spielerhand schon gebustet war, sodass der Dealer nie ziehen muss."),
            K("msgDealerDrawCard", "Gleichstand brechen", "Kündigt die zusätzliche Karte an. {hands} ist die Zeile Geteilte Hand im Gleichstand darüber."),
            K("msgDealerTieWin", "Gleichstand bleibt", "Gesendet, wenn das Stechen den Dealer auf Augenhöhe mit dem Spieler lässt, was der Dealer gewinnt."),
            K("msgDealerBeatsYou", "Gleichstand für den Dealer gebrochen", "Gesendet, wenn das Stechen den Dealer wirklich nach vorn bringt."),
            K("msgDealerPeekBlackjack", "Prüfung fand einen Blackjack", "Gesendet, wenn der Dealer auf ein Ass oder eine Zehn die verdeckte Karte prüft und Blackjack hält, was die Hand dort beendet."),
            K("msgDealerBlackjackPush", "Beide haben Blackjack", "Gesendet, wenn Dealer und Spieler beide Blackjack halten: unentschieden, der Einsatz kommt direkt zurück."),
            K("msgFinalSummary", "Schlussbilanz", "Kurz vor den meisten Ergebnissen gesendet, beide Endhände nebeneinander."),
            K("msgInsuranceOffer", "Versicherung angeboten", "Direkt nach dem Geben gesendet, nur mit Versicherung an und einem Ass beim Dealer. Die einzige Gelegenheit, sie zu nehmen."),
            K("msgInsuranceTaken", "Versicherung genommen", "Gesendet, wenn der Spieler sich versichert. Die Hälfte des ursprünglichen Einsatzes wird beiseitegelegt."),
            K("msgInsuranceWin", "Versicherung zahlt", "Gesendet, wenn der Dealer tatsächlich Blackjack hält. Zahlt 2:1 auf den Versicherungseinsatz, egal was die Haupthand macht."),
            K("msgInsuranceLose", "Versicherung verloren", "Gesendet, wenn der Dealer doch keinen Blackjack hat. Nur der Versicherungseinsatz ist weg; die Hand spielt weiter."),
            K("msgInsuranceNotAvailable", "Versicherung abgelehnt", "Gesendet, wenn Versicherung nicht gilt: die Regel ist aus, sie wurde nicht angeboten, sie ist schon genommen, oder der Spieler hat gehandelt."),
            K("msgInsuranceInsufficientFunds", "Versicherung zu teuer", "Gesendet, wenn der Spieler die Hälfte seines Einsatzes nicht decken kann."),
            K("msgSurrenderResult", "Aufgegeben", "Gesendet, wenn ein Spieler eine frische Hand aufgibt und die Hälfte des Einsatzes behält."),
            K("msgSurrenderNotAvailable", "Aufgeben abgelehnt", "Gesendet, wenn Aufgeben nicht gilt: die Regel ist aus, die Versicherung ist schon genommen, oder die Hand hat nicht mehr zwei Karten."),
            K("msgBettingAll", "all / allin", "Bestätigt einen Einsatz des ganzen Guthabens. Die Zahl ist schon auf den Höchsteinsatz gekürzt."),
            K("msgBettingHalf", "half", "Bestätigt einen Einsatz der Hälfte des Guthabens."),
            K("msgBettingQuarter", "quarter / 25%", "Bestätigt einen Einsatz eines Viertels des Guthabens."),
            K("msgBettingRandom", "random / rand", "Bestätigt einen Zufallseinsatz zwischen einem Zehntel des Guthabens und dem, was der Spieler sich leisten kann."),
            K("msgBettingDouble", "double / 2x", "Bestätigt einen Einsatz des doppelten Minimums."),
            K("msgBettingMax", "max / maximum", "Bestätigt den größten erlaubten Einsatz, begrenzt durch Guthaben und Höchsteinsatz."),
            K("msgBetMinApplied", "Stattdessen Minimum", "Gesendet, wenn ein Kürzel unter dem Mindesteinsatz landet, sodass das Minimum gespielt wird."),
            K("msgBlackjackWinFree", "Blackjack", "Ersetzt die Zeile Blackjack, solange das Freispiel an ist."),
            K("msgRegularWinFree", "Gewinn", "Ersetzt die Zeile Gewinn, solange das Freispiel an ist."),
            K("msgLossFree", "Verlust", "Ersetzt die Zeile Verlust, solange das Freispiel an ist, denn nichts war gesetzt."),
            K("msgQuitRefundFree", "Aufgehört", "Ersetzt die Zeile Aufgehört, solange das Freispiel an ist - es gibt nichts zurückzugeben."),
            K("msgSurrenderResultFree", "Aufgegeben", "Ersetzt die Zeile Aufgegeben, solange das Freispiel an ist."),
            K("msgDealerTieWinFree", "Gleichstand bleibt", "Ersetzt die Zeile Gleichstand bleibt, solange das Freispiel an ist."),
            K("msgDealerBeatsYouFree", "Gleichstand für den Dealer gebrochen", "Ersetzt die Zeile Gleichstand gebrochen, solange das Freispiel an ist."),
            K("msgTurnTimeoutRefundFree", "Zeit abgelaufen", "Ersetzt die Erstattungszeile, solange das Freispiel an ist."),
            K("freeGame", "Freispiel", "Hände werden um nichts gespielt: kein Einsatz wird genommen, kein Gewinn gezahlt, und jede Nachricht über Punkte wechselt auf ihre Freispiel-Fassung. Der Hausmodus öffnet keinen Tisch, solange es an ist. !SetFreeBlackjack und !EndFreeBlackjack schalten es aus dem Chat (Moderatoren)."),
            K("msgFreeGameStart", "Gestartet", "Einmal gesendet, wenn !SetFreeBlackjack das Freispiel einschaltet, in den Chat, aus dem der Befehl kam. Anders eingeschaltet, etwa per Execute C# Method auf StartFreeGame in einer eigenen Action, geht sie an jede Plattform mit einem Konto in Streamer.bot - an YouTube nur, solange eine Übertragung beobachtet wird."),
            K("msgFreeGameEnd", "Beendet", "Einmal gesendet, wenn !EndFreeBlackjack das Freispiel ausschaltet, in den Chat, aus dem der Befehl kam. Anders ausgeschaltet, etwa per Execute C# Method auf EndFreeGame, geht sie an jede Plattform mit einem Konto in Streamer.bot - an YouTube nur, solange eine Übertragung beobachtet wird."),
            K("msgInsufficientSplit", "Teilen nicht leistbar", "Gesendet, wenn ein Teilen mehr bräuchte, als der Spieler noch hat."),
            K("msgInsufficientDouble", "Verdoppeln nicht leistbar", "Gesendet, wenn ein Verdoppeln mehr bräuchte, als der Spieler noch hat."),
            K("msgAlreadyActiveGame", "Schon in einer Hand", "Gesendet, wenn ein Spieler eine Hand beginnt, während er noch eine hält."),
            K("msgNoActiveGame", "Keine Hand läuft", "Gesendet, wenn hit, stand, double, split, insurance, surrender, repeat oder quit ohne Hand auf dem Tisch ankommt."),
            K("msgGameAlreadyOver", "Hand schon vorbei", "Gesendet, wenn ein Spieler auf eine Hand handelt, die schon abgerechnet ist."),
            K("msgQuitOnlyBefore", "Zu spät zum Aufhören", "Gesendet, wenn quit nach Ziehen, Halten oder Verdoppeln ankommt. Dann liegt eine echte Hand auf dem Tisch."),
            K("msgCannotSplitValue", "Karten passen nicht", "Gesendet, wenn die zwei Karten nicht gleich viel wert sind."),
            K("msgCannotSplitCount", "Nicht zwei Karten", "Gesendet, wenn ein Teilen auf einer Hand versucht wird, die nicht mehr zwei Karten hat, etwa nach einem Zug."),
            K("msgAlreadySplit", "Schon geteilt", "Gesendet, wenn noch einmal geteilt wird, während Erneut teilen aus ist - standardmäßig ein Teilen pro Runde."),
            K("msgSplitDisabled", "Teilen aus", "Gesendet, wenn split überhaupt genutzt wird, während die Regel auf der Seite Tisch aus ist."),
            K("msgMaxSplitHandsReached", "Keine weiteren Hände", "Gesendet, wenn ein erneutes Teilen die Grenze Hände durch Teilen überschreiten würde."),
            K("msgCannotDoubleCount", "Nicht zwei Karten", "Gesendet, wenn ein Verdoppeln auf einer Hand versucht wird, die nicht mehr zwei Karten hat."),
            K("msgMustDoubleFirst", "Zu spät zum Verdoppeln", "Gesendet, wenn nach Ziehen oder Halten auf dieser Hand verdoppelt werden soll. Verdoppeln geht nur als erster Zug."),
            K("msgAlreadyDoubledDown", "Schon verdoppelt", "Gesendet, wenn dieselbe Hand zweimal verdoppelt wird."),
            K("msgDoubleDownDisabled", "Verdoppeln aus", "Gesendet, wenn double überhaupt genutzt wird, während die Regel auf der Seite Tisch aus ist."),
            K("msgMaxGamesReached", "Stundengrenze erreicht", "Gesendet, wenn ein Spieler die Grenze Spiele pro Stunde überschreitet."),
            K("msgPointsVarNotFound", "Punkte-Variable fehlt", "Gesendet, wenn das Freispiel aus ist und die Punkte-Variable für diesen Zuschauer nicht lesbar ist - meist ein falscher Name, oder noch keine Währung eingerichtet."),
            K("msgPointsNotRefunded", "Erstattung fehlgeschlagen", "Gesendet, wenn eine Erstattung nicht gezahlt werden kann, weil die Punkte-Variable unlesbar ist. Der Einsatz wird dann nicht wiederhergestellt."),
            K("msgCardError", "Deck-Problem", "Gesendet im seltenen Fall, dass eine Starthand gar nicht gegeben werden kann. Wert, gemeldet zu werden, falls du es je siehst."),
            K("msgTurnTimeoutStand", "Zeit abgelaufen, Hand hält", "Gesendet, wenn die Zugzeit abläuft, nachdem der Spieler gehandelt hat: die Hand hält und der Dealer spielt sie aus."),
            K("msgTurnTimeoutRefund", "Zeit abgelaufen, Einsatz zurück", "Gesendet, wenn die Zugzeit abläuft, bevor der Spieler überhaupt gehandelt hat, und 'Ungespielte Hand erstatten' an ist."),
            K("msgBalance", "Guthaben", "Nach den meisten Ergebnissen angehängt, um zu zeigen, wo der Zuschauer jetzt steht."),
            K("msgTutorial", "Anleitung", "Einmal je Streamer.bot-Sitzung gesendet, beim ersten Mal, dass jemand spielt."),
            K("msgRepeatNothing", "Nichts zu wiederholen", "Gesendet, wenn repeat genutzt wird, bevor die Runde etwas gesagt hat."),
            K("msgAddictedFee", "Suchtgebühr", "An einen Gewinn angehängt, wenn der Gewinner auf der Süchtigen-Liste steht."),
            K("msgDuelChallenge", "Herausforderung ausgesprochen", "Eröffnet ein Duell und fordert den Herausgeforderten zur Antwort auf."),
            K("msgDuelAccepted", "Angenommen", "Gesendet, wenn die Herausforderung angenommen wird und die Karten rausgehen."),
            K("msgDuelDeclined", "Abgelehnt", "Gesendet, wenn der Herausgeforderte Nein sagt. Der Einsatz des Herausforderers geht zurück."),
            K("msgDuelExpired", "Keine Antwort", "Gesendet, wenn niemand innerhalb der Annahmefrist geantwortet hat."),
            K("msgDuelAcceptedFree", "Angenommen (Freispiel)", "Ersetzt Angenommen, solange das Freispiel an ist."),
            K("msgDuelDeclinedFree", "Abgelehnt (Freispiel)", "Ersetzt Abgelehnt, solange das Freispiel an ist."),
            K("msgDuelExpiredFree", "Keine Antwort (Freispiel)", "Ersetzt Keine Antwort, solange das Freispiel an ist - es gibt keinen Einsatz zurückzugeben."),
            K("msgDuelHandDealt", "Hand gegeben", "Einmal je Spieler direkt nach dem Geben gesendet."),
            K("msgDuelTurn", "Du bist dran", "Gesendet, wenn ein Spieler an den Zug kommt."),
            K("msgDuelDrawCard", "Karte", "Gesendet, wenn ein Duellant zieht."),
            K("msgDuelStand", "Halten", "Gesendet, wenn ein Duellant aufhört."),
            K("msgDuelBust", "Bust", "Gesendet, wenn ein Duellant über 21 geht."),
            K("msgDuelTurnTimeout", "Zug abgelaufen", "Gesendet, wenn ein Spieler die Zugzeit verstreichen lässt und automatisch hält."),
            K("msgDuelForfeit", "Aufgabe", "Gesendet, wenn ein Duellant mitten im Duell aufhört. Der Gegner nimmt den ganzen Topf."),
            K("msgDuelHandLabel", "Nächste geteilte Hand", "Als Präfix genutzt, wenn das Spiel zur nächsten geteilten Hand eines Spielers geht; die Karten folgen darauf."),
            K("msgDuelRepeat", "Wiederholung", "Gesendet, wenn einer der Duellanten repeat nutzt. Nur lesend, und es rührt die Zuguhr nie an."),
            K("msgDuelRepeatFree", "Wiederholung (Freispiel)", "Ersetzt Wiederholung, solange das Freispiel an ist - es gibt keinen Topf zu melden."),
            K("msgDuelRaiseOffer", "Erhöhung angeboten", "Gesendet, wenn ein Spieler teilt oder verdoppelt, und bittet den Gegner, dem höheren Einsatz zuzustimmen."),
            K("msgDuelRaiseAccepted", "Erhöhung angenommen", "Gesendet, wenn der Gegner zustimmt und beide Einsätze steigen."),
            K("msgDuelRaiseDeclined", "Erhöhung abgelehnt", "Gesendet, wenn der Gegner Nein sagt. Die Erhöhung fällt weg und der Erhöhende spielt die Hand, wie sie liegt."),
            K("msgDuelRaiseTimeout", "Erhöhung unbeantwortet", "Gesendet, wenn der Gegner die Erhöhung nie beantwortet hat."),
            K("msgDuelRaiseUnaffordable", "Erhöhung zu teuer", "Gesendet, wenn der Gegner zustimmen wollte, aber das Guthaben dafür nicht mehr hat."),
            K("msgDuelShowdown", "Showdown", "Kurz vor dem Ergebnis gesendet, mit beiden Werten."),
            K("msgDuelWin", "Gewinn", "Gesendet, wenn ein Duellant es holt."),
            K("msgDuelPush", "Unentschieden", "Gesendet, wenn das Duell gleich endet und beide Einsätze zurückgehen."),
            K("msgDuelPotResult", "Nebentopf gewonnen", "Einmal je Nebentopf gesendet, wenn Teilungen im Spiel waren."),
            K("msgDuelPotPush", "Nebentopf unentschieden", "Gesendet für einen Nebentopf, der gleich endete."),
            K("msgDuelAddictedFee", "Suchtgebühr", "Nach einem Duellergebnis gesendet, wenn der Gewinner auf der Süchtigen-Liste steht und der Duell-Abzug an ist. Ohne sie sähe der Gewinner nur eine kleinere Zahl als den Topf."),
            K("msgDuelWinFree", "Gewinn (Freispiel)", "Ersetzt Gewinn, solange das Freispiel an ist."),
            K("msgDuelPushFree", "Unentschieden (Freispiel)", "Ersetzt Unentschieden, solange das Freispiel an ist."),
            K("msgDuelSuddenDeathOffer", "Angeboten", "Gesendet bei Gleichstand mit Sudden Death an. Beide Spieler müssen annehmen."),
            K("msgDuelSuddenDeathWaiting", "Einer dabei, einer fehlt", "Gesendet, wenn der erste Spieler annimmt und der andere noch antworten muss."),
            K("msgDuelSuddenDeathDeclined", "Abgelehnt", "Gesendet, wenn ein Spieler Nein sagt. Alles geht zurück."),
            K("msgDuelSuddenDeathTimeout", "Unbeantwortet", "Gesendet, wenn niemand auf das Angebot geantwortet hat. Alles geht zurück."),
            K("msgDuelSuddenDeathStart", "Runde beginnt", "Gesendet, sobald beide angenommen haben und der zusätzliche Einsatz genommen ist."),
            K("msgDuelSuddenDeathCard", "Die Zusatzkarte", "Einmal je Spieler für die Zusatzkarte gesendet."),
            K("msgDuelSuddenDeathMaxRounds", "Runden aufgebraucht", "Gesendet, wenn die Spieler bis zur Rundengrenze weiter gleichgezogen haben. Alles geht zurück."),
            K("msgDuelSuddenDeathBothBust", "Beide gebustet", "Gesendet, wenn die Zusatzkarte beide über 21 bringt. Keiner kann von da aus gewinnen, also stoppt das Duell, statt weiterzuziehen, und alles geht zurück."),
            K("msgDuelSuddenDeathStartFree", "Runde beginnt (Freispiel)", "Ersetzt Runde beginnt, solange das Freispiel an ist."),
            K("msgDuelDisabled", "Duelle aus", "Gesendet, wenn der Duell-Befehl genutzt wird, während Duelle aus sind."),
            K("msgDuelNoTarget", "Niemand markiert", "Gesendet, wenn der Duell-Befehl ohne Namen ankommt."),
            K("msgDuelSelfChallenge", "Dich selbst herausgefordert", "Gesendet, wenn jemand den eigenen Namen markiert."),
            K("msgDuelChallengerBusy", "Du bist beschäftigt", "Gesendet, wenn der Herausforderer schon eine Hand, ein Duell, einen Platz oder eine offene Herausforderung hat."),
            K("msgDuelTargetBusy", "Der andere ist beschäftigt", "Gesendet, wenn der markierte Spieler schon in einer Hand, einem Duell oder auf einem Platz ist."),
            K("msgDuelAlreadyPending", "Schon herausgefordert", "Gesendet, wenn beim markierten Spieler schon eine Herausforderung wartet."),
            K("msgDuelTargetInsufficient", "Nicht genug Punkte", "Gesendet, wenn eine Seite den Einsatz nicht decken kann."),
            K("msgDuelNoPending", "Nichts zu beantworten", "Gesendet, wenn accept oder decline ankommt, ohne dass auf diesen Spieler eine Frage wartet."),
            K("msgDuelNotYourTurn", "Nicht dein Zug", "Gesendet, wenn ein Duellant außer der Reihe handelt."),
            K("msgDuelSplitDisabled", "Teilen aus in Duellen", "Gesendet, wenn split in einem Duell genutzt wird, während die Duell-Regel aus ist."),
            K("msgDuelDoubleDisabled", "Verdoppeln aus in Duellen", "Gesendet, wenn double in einem Duell genutzt wird, während die Duell-Regel aus ist."),
            K("msgDuelActionNotAvailable", "Kein Duell-Befehl", "Gesendet, wenn insurance oder surrender während eines Duells genutzt wird."),
            K("msgHouseRulePrompt", "Regelsatz-Menü", "Gesendet, wenn ein Tisch ohne Regelsatz geöffnet wird, mit den auf der Seite Haus angehakten."),
            K("msgHouseRuleTimeout", "Keine Regeln gewählt", "Gesendet, wenn niemand auf das Menü geantwortet hat. Nichts war gesetzt, nichts muss zurück."),
            K("msgHouseTableOpen", "Tisch offen", "Gesendet, wenn der Tisch öffnet und das Beitrittsfenster beginnt."),
            K("msgHouseSeatTaken", "Platz genommen", "Jedes Mal gesendet, wenn sich jemand setzt."),
            K("msgHouseSeatLeft", "Platz verlassen", "Gesendet, wenn ein sitzender Spieler aufhört, solange sein Einsatz noch zurückkann."),
            K("msgHouseNoPlayers", "Niemand hat sich gesetzt", "Gesendet, wenn das Beitrittsfenster mit lauter leeren Plätzen abgelaufen ist, sodass der Tisch wieder schließt."),
            K("msgHouseDealt", "Karten raus", "Gesendet, wenn die Karten rausgehen, vor den einzelnen Händen."),
            K("msgHouseHandDealt", "Hand gegeben", "Einmal je Platz direkt nach dem Geben gesendet."),
            K("msgHouseSeatBlackjack", "Platz hat Blackjack", "Gesendet, wenn einem Platz ein echter Blackjack gegeben wird."),
            K("msgHouseTurn", "Du bist dran", "Gesendet, wenn der Tisch zu einem Platz weitergeht."),
            K("msgHouseDrawCard", "Karte", "Gesendet, wenn ein Platz zieht."),
            K("msgHouseStand", "Halten", "Gesendet, wenn ein Platz aufhört."),
            K("msgHouseBust", "Bust", "Gesendet, wenn ein Platz über 21 geht."),
            K("msgHouseNextHand", "Nächste geteilte Hand", "Gesendet, wenn das Spiel zur nächsten geteilten Hand eines Platzes geht."),
            K("msgHouseTurnTimeout", "Zug abgelaufen", "Gesendet, wenn ein Platz die Zugzeit verstreichen lässt und automatisch hält."),
            K("msgHouseSurrender", "Aufgegeben", "Gesendet, wenn ein Platz aufgibt, bei einem Regelsatz, der es erlaubt."),
            K("msgHouseInsuranceOffer", "Versicherung angeboten", "An den ganzen Tisch gesendet, wenn der Dealer ein Ass zeigt, bevor die verdeckte Karte geprüft wird."),
            K("msgHouseInsuranceTaken", "Versicherung genommen", "Gesendet, wenn ein Platz sich versichert."),
            K("msgHouseInsuranceWin", "Versicherung zahlt", "Gesendet, wenn der Dealer tatsächlich Blackjack hält und die Versicherung dieses Platzes zahlt."),
            K("msgHouseInsuranceLose", "Versicherung verloren", "Gesendet, wenn der Dealer keinen Blackjack hat und der Versicherungseinsatz weg ist."),
            K("msgHouseDealerReveal", "Verdeckte Karte aufgedeckt", "Gesendet, wenn der Zug des Dealers beginnt."),
            K("msgHousePeekBlackjack", "Prüfung fand einen Blackjack", "Gesendet, wenn der Dealer auf ein Ass oder eine Zehn prüft und Blackjack hält, was den Tisch dort beendet."),
            K("msgHouseDealerBlackjack", "Dealer-Blackjack", "Gesendet an einem Tisch, an dem der Dealer nicht prüft und einen aufdeckt, der alles Gesetzte an jedem Platz nimmt."),
            K("msgHouseSeatWin", "Platz gewinnt", "Für jeden gewinnenden Platz gesendet, wenn der Tisch abgerechnet wird."),
            K("msgHouseSeatPush", "Platz unentschieden", "Für jeden Platz auf Augenhöhe mit dem Dealer gesendet, der seinen Einsatz zurückbekommt."),
            K("msgHouseSeatLoss", "Platz verliert", "Für jeden verlierenden Platz gesendet, wenn der Tisch abgerechnet wird."),
            K("msgHouseTableSummary", "Tischbilanz", "Einmal am Ende gesendet, und noch einmal bei repeat: die Hand des Dealers und das Ergebnis jedes Platzes."),
            K("msgHouseDisabled", "Haus-Modus aus", "Gesendet, wenn der Haus-Befehl genutzt wird, während der Haus-Modus aus ist."),
            K("msgHouseNeedsCurrency", "Braucht echte Währung", "Gesendet, wenn ein Tisch geöffnet wird, während das Freispiel an ist."),
            K("msgHouseNoRuleSets", "Keine Regelsätze", "Gesendet, wenn auf der Seite Haus nichts angehakt ist, sodass es nichts gibt, womit sich ein Tisch öffnen ließe."),
            K("msgHouseAlreadyRunning", "Tisch läuft schon", "Gesendet, wenn jemand einen zweiten Tisch öffnet, während einer läuft."),
            K("msgHouseTableFull", "Tisch voll", "Gesendet, wenn jeder Platz belegt ist."),
            K("msgHouseAlreadySeated", "Schon gesetzt", "Gesendet, wenn ein Spieler einen zweiten Platz nimmt."),
            K("msgHouseNoTable", "Kein Tisch offen", "Gesendet, wenn ein Tisch-Befehl ankommt, ohne dass ein Tisch offen ist."),
            K("msgHouseJoinClosed", "Beitrittsfenster zu", "Gesendet, wenn jemand beitritt, nachdem die Karten raus sind."),
            K("msgHouseNotYourTurn", "Nicht dein Zug", "Gesendet, wenn ein Platz außer der Reihe handelt."),
            K("msgHouseNotPlaying", "Es wird nicht gespielt", "Gesendet, wenn ein Spielbefehl ankommt, während der Tisch nicht spielt - etwa im Beitrittsfenster."),
            K("msgHouseNotSeated", "Nicht gesetzt", "Geschrieben für jemanden, der nie Platz genommen hat und die Hand des Tisches spielen will. Das Spiel antwortet darauf derzeit mit 'Keine Hand läuft', diese Zeile wird also nicht gesendet."),
            K("msgHouseActionNotAvailable", "Hier nicht erlaubt", "Gesendet, wenn ein Befehl nicht zum Regelsatz des Tisches gehört - Aufgeben an einem europäischen Tisch zum Beispiel."),
            Tx("Playing a hand", "Eine Hand spielen"),
            Tx("Losses", "Verluste"),
            Tx("Insurance and surrender", "Versicherung und Aufgeben"),
            Tx("Bet shortcuts", "Einsatz-Kürzel"),
            Tx("Free Game", "Freispiel"),
            Tx("Table refusals", "Ablehnungen am Tisch"),
            Tx("System", "System"),
            Tx("Duel: the challenge", "Duell: die Herausforderung"),
            Tx("Duel: play", "Duell: Spiel"),
            Tx("Duel: raises", "Duell: Erhöhungen"),
            Tx("Duel: result", "Duell: Ergebnis"),
            Tx("Duel: sudden death", "Duell: Sudden Death"),
            Tx("Duel: refusals", "Duell: Ablehnungen"),
            Tx("House: the table", "Haus: der Tisch"),
            Tx("House: play", "Haus: Spiel"),
            Tx("House: result", "Haus: Ergebnis"),
            Tx("House: refusals", "Haus: Ablehnungen"),
            Tx("3:2 - casino standard", "3:2 - Casino-Standard"),
            Tx("6:5 - house edge", "6:5 - Hausvorteil"),
            Tx("2:1 - generous", "2:1 - großzügig"),
            Tx("1:1 - no bonus", "1:1 - kein Bonus"),
            Tx("Card and number - 🂡(11)", "Karte und Zahl - 🂡(11)"),
            Tx("Card only - 🂡", "Nur Karte - 🂡"),
            Tx("Number only - 11", "Nur Zahl - 11"),
            Tx("Never", "Nie"),
            Tx("Every new ISO week", "Jede neue ISO-Woche"),
            Tx("On the 1st of the month", "Am 1. des Monats"),
            Tx("The leaderboard totals only", "Nur die Summen der Bestenliste"),
            Tx("Every stat there is", "Jede Statistik, die es gibt"),
            Tx("Free Game, from the Table page or !SetFreeBlackjack. While it is on, nobody stakes or wins anything.", "Freispiel, von der Seite Tisch oder !SetFreeBlackjack. Solange es an ist, setzt und gewinnt niemand etwas."))
        .Pill("freeGame", "Free game on", "Free game off", tooltip: "Free Game, from the Table page or !SetFreeBlackjack. While it is on, nobody stakes or wins anything.")
        .Samples("cards", "🂫(10) 🂺(10)", "card", "🂺(10)", "value", "20", "total", "22", "handnum", "2", "hands", "Tie on Hand 1!", "card1", "🂫(10)", "card2", "🂺(10)", "challenger", "alice", "opponent", "bob", "challengercards", "🂫(10) 🂺(10) (20)", "opponentcards", "🂱(11) 🂸(8) (19)", "challengervalue", "20", "opponentvalue", "19", "winner", "alice", "loser", "bob", "pot", "3,000", "bet", "1,500", "newbet", "3,000", "raise", "doubles down", "turn", "bob", "round", "1", "seconds", "60", "required", "1,500", "available", "820", "var", "points", "action", "insurance", "extra", ", double or split", "addictedCutPercent", "50", "menu", "1) Vegas Strip | 2) Atlantic City | 3) European", "rules", "Vegas Strip", "rulesdetail", "4 deck shoe, dealer stands on soft 17, insurance offered, blackjack pays 3:2", "seats", "alice: 20 win | bob: 19 lose", "seatnum", "2", "dealer", "🂱(11) 🂸(8)", "dealervalue", "19", "playercards", "🂫(10) 🂺(10)", "playervalue", "20", "FormatNumber(bet)", "1,500", "0", "alice", "1", "12", "balance", "11,500")
        .Log("Chat Games/Logs", "BJG-log*.txt", writeLogKey: "logToFile", writeLogHelp: "Writes every settled hand to a dated 'BJG-log-yyyy-MM-dd.txt' in the Logs folder, one file per day.", columns: new[] { "Player|User", "Bet|Bet", "Change|Change", "After|After" }, results: new[] { "Win", "Blackjack", "Loss", "Bust", "Push", "Charlie", "Three 7s", "Insurance", "Surrender", "Quit", "Timeout" })
        .Answer("overlaySample", "A sample hand is playing on the overlay for about ten seconds, in OBS too if the source is there. Nobody's points move.")
        .Answer("overlayDemo", "The demo opened in your browser. In OBS, add &demo=1 to the source's URL to place it the same way.")
        .Help(
            H("The idea",
                Note("Get closer to 21 than the dealer without going past it. Number cards count their face value, Jack, Queen and King count 10, and an Ace counts 11 until that would bust the hand, at which point it drops to 1 on its own.")),
            H("Playing a hand",
                Cmd("!blackjack <bet>", "Deals in. The bet takes a number, one of the shortcuts below, or nothing at all to use the Default bet."),
                Cmd("hit", "Draw another card."),
                Cmd("stand", "Stop and keep the total you have."),
                Cmd("double", "Double the bet for exactly one more card, then stand. First move on a two-card hand only."),
                Cmd("split", "Split a starting pair of equal-value cards into two hands, each played out for the same bet."),
                Cmd("insurance", "Stake half the bet against a dealer blackjack, paying 2:1. Only against an Ace, and only before anything else."),
                Cmd("surrender", "Fold a fresh two-card hand and keep half the bet."),
                Cmd("quit", "Cancel and take the bet back, before hit, stand, double or split."),
                Cmd("repeat", "Post the last game message again. In a duel it posts both hands, the pot and whose turn it is."),
                Cmd("stats / allstats", "Your own record, or the leaderboard."),
                Cmd("!SetFreeBlackjack", "moderators: turns Free Game on, every hand is played for nothing. !EndFreeBlackjack turns it off."),
                Note("Those are the default trigger words; your streamer may have bound them to something else.")),
            H("For your own actions",
                Note("Every settled hand fires a custom trigger (Custom > Chat Game > Blackjack Game): BJG: Hand Won, BJG: Hand Lost or BJG: Hand Push for a hand at the table, BJG: Blackjack on top of Hand Won for a natural, BJG: Duel Settled once per duellist, BJG: House Hand Settled once per seat, and BJG: Hand Settled for every one of them, insurance, surrender, quit and timeout included. The trigger carries the hand as arguments: handOutcome (the log's result, WIN, BUST, DUEL PUSH, HOUSE BLACKJACK, ...), handUser, handPlatform, handMode (table, duel or house), handTrigger, handBet, handChange, handPreviousPoints, handNewPoints and handCurrency. GetLastHandResult, run with an Execute C# Method sub-action on the game action, hands the same arguments to any other action. BJG: Free Game Start and BJG: Free Game End fire when Free Game is switched from chat (!SetFreeBlackjack, !EndFreeBlackjack) or by an Execute C# Method on StartFreeGame or EndFreeGame.")),
            H("Bet shortcuts",
                Cmd("all / allin", "The whole balance."),
                Cmd("half", "Half the balance."),
                Cmd("quarter / 25%", "A quarter of it."),
                Cmd("50%, any percentage", "That much of the balance."),
                Cmd("double / 2x", "Twice the minimum bet."),
                Cmd("random / rand", "Somewhere between a tenth of the balance and all of it."),
                Cmd("max / maximum", "As much as the balance and the Maximum bet allow."),
                Cmd("1.5k / 2m", "1,500 and 2,000,000 - k for thousand, m for million.")),
            H("Bonus hands",
                Note("Both settle the moment they happen, whatever the dealer is holding."),
                Cmd("5-card Charlie", "Five cards without busting. Pays the bet back plus a 1.5x bonus."),
                Cmd("Three 7s", "Three or more sevens without busting. Pays the bet back plus a 2x bonus.")),
            H("Insurance, in full",
                Note("Insurance is only offered when the dealer's upcard is an Ace, because that is the only time a dealer blackjack - Ace plus a ten-value card - is one card away. It costs half the original bet."),
                Cmd("Dealer has blackjack", "Insurance pays 2:1, so three times the insurance stake comes back. That roughly covers losing the main hand."),
                Cmd("Dealer does not", "The insurance stake is gone and the hand plays on as normal."),
                Note("It has to be decided before hitting, standing, doubling or splitting. Over many hands it loses the player money, which is why plenty of streamers leave it off.")),
            H("Surrender, in full",
                Note("Surrender is a way out of a hand that looks lost before any of it has been played. It is available on the first two cards only, before hitting, doubling, splitting or insuring, and returns half the bet."),
                Note("A 16 against a dealer's 10 is the usual case: almost any card busts it, and standing on 16 rarely beats a ten either. Half the bet back is the better of two bad outcomes.")))
        .About(credits: "Responsible: aaskjer\nCredits: nate1280 / Streamer.bot and YOU ♡",
            sections: new[]
            {
                H("Needs a points system",
                    Note("Hands are played with the points of a currency system. These two are picked up without any setup:"),
                    Link("Currency Core by StreamUP", "https://streamup.tips/product/currency-core"),
                    Link("LOYALTY POINTS by tawmae", "https://tawmae.xyz/loyalty-points"),
                    Note("Anything else works too - name its variable on the Access page."))
            }, links: new[] { Link("Blackjack Game on the Streamer.bot Discord", "https://discord.com/channels/834650675224248362/1447648588694818836/1447648588694818836") })
        // The tour on the window's first open (the ? in the header shows it again); the framework brings the texts.
        .Tour(Tip("start"), Tip("rail"), Tip("save"), Tip("customise"), Tip("end"));

    // ---- The table: every setting the host reads, in page order. ----
    public static readonly Setting[] Table =
    {
        // ---- table ----
        S("freeGame", true, page: "table", label: "Free Game", help: "Hands are played for nothing: no stake is taken, no winnings are paid, and every message about points\nswitches to its Free Game wording. House Mode refuses to open a table while this is on. !SetFreeBlackjack and !EndFreeBlackjack switch it from chat (moderators).", section: "Betting", aliases: new[] { "FreeGame" }),
        S("defaultBet", 1234L, page: "table", label: "Default bet", help: "Used when the blackjack command arrives without an amount, or with a word that is not one of the shortcuts.", kind: "long", section: "Betting", aliases: new[] { "DefaultBet" }),
        S("minBet", 1L, page: "table", label: "Minimum bet", help: "Anything lower is raised to this. The player still needs the balance to cover it.", kind: "long", section: "Betting", aliases: new[] { "MinBet" }),
        S("maxBet", 0L, page: "table", label: "Maximum bet", help: "Anything higher is cut down to this.\n0 = no ceiling.", kind: "long", section: "Betting", unlimitedAtZero: true, aliases: new[] { "MaxBet" }),
        S("dealerStandsOn", 17, page: "table", label: "Stands on", help: "The dealer keeps drawing until the hand reaches this total, then stops. 17 is the casino standard.", kind: "int", section: "The dealer", options: new[] { "16", "17", "18", "19", "20", "21" }, aliases: new[] { "DealerStandsOn" }),
        S("numDecks", 1, page: "table", label: "Decks in the shoe", help: "How many 52-card decks are shuffled together.", kind: "int", section: "The dealer", options: new[] { "1", "2", "3", "4", "5", "6", "7", "8" }, aliases: new[] { "NumDecks" }),
        S("reshuffleThreshold", 0.25, page: "table", label: "Reshuffle at", help: "How little of the shoe may be left before the played cards are shuffled back in.\nLow means long runs between shuffles and a count worth keeping; high means it is reshuffled so often\nthat counting gets nobody anywhere.", kind: "percent", section: "The dealer", min: 1.0, max: 90.0, factor: 0.01, aliases: new[] { "ReshuffleThreshold" }),
        S("dealerDrawDelayMs", 500, page: "table", label: "Reveal delay", help: "Pause between the dealer's cards, in milliseconds, so chat can follow along. 0 = all at once.", kind: "int", section: "The dealer", aliases: new[] { "DealerDrawDelayMs" }),
        S("tieDrawOnlyOneCard", false, page: "table", label: "Break a tie with one card only", help: "On: the dealer draws exactly one card on a tie, and a tie that survives it counts as a dealer win.\nOff: the dealer keeps drawing until the tie breaks one way or the other.", kind: "bool", section: "The dealer", aliases: new[] { "TieDrawOnlyOneCard" }),
        S("showAceMsg", true, page: "table", label: "Say when an Ace drops to 1", help: "Posts a line whenever an Ace counts as 1 instead of 11 to keep a hand under 22.", kind: "bool", section: "The dealer", aliases: new[] { "ShowAceMsg" }),
        S("allowDoubleDown", true, page: "table", label: "Double down", help: "Doubles the stake for exactly one more card, then stands. Only on a fresh two-card hand, before anything else.", kind: "bool", section: "Optional rules", aliases: new[] { "AllowDoubleDown" }),
        S("allowSplit", true, page: "table", label: "Split", help: "Splits a starting pair of equal-value cards into two hands, each played out for the same stake.", kind: "bool", section: "Optional rules", aliases: new[] { "AllowSplit" }),
        S("allowResplit", false, page: "table", label: "Resplit", help: "Lets a split hand split again when it draws another matching pair. Applies to split Aces too.", kind: "bool", section: "Optional rules", aliases: new[] { "AllowResplit" }),
        S("maxSplitHands", 4, page: "table", label: "Hands from splitting", help: "The total number of hands one round may reach through splitting. Four is the casino standard.", kind: "int", section: "Optional rules", options: new[] { "2", "3", "4", "5", "6", "7", "8" }, aliases: new[] { "MaxSplitHands" }),
        S("allowInsurance", false, page: "table", label: "Insurance", help: "When the dealer shows an Ace, a player may stake half their bet against a dealer blackjack. Pays 2:1.\nOver time it is a losing side bet for the player, which is why it is off by default.", kind: "bool", section: "Optional rules", aliases: new[] { "AllowInsurance" }),
        S("allowSurrender", false, page: "table", label: "Surrender", help: "Lets a player fold a fresh two-card hand and keep half the stake, before hitting, doubling, splitting or insuring.", kind: "bool", section: "Optional rules", aliases: new[] { "AllowSurrender" }),
        S("dealerPeeks", false, page: "table", label: "Dealer peeks for blackjack", help: "On: the dealer takes a face-down card at the deal and checks it as soon as the upcard is an Ace or a ten.", kind: "bool", section: "Optional rules", aliases: new[] { "DealerPeeks" }),
        S("turnTimeout", 180, page: "table", label: "Turn timeout", help: "How long a player has to act before the hand is closed out for them, in seconds. 0 = no limit.\nThe clock restarts on every action, so it only catches someone who really walked away.", kind: "int", section: "Pace", aliases: new[] { "TurnTimeoutSec" }),
        S("maxGamesPerHour", 0, page: "table", label: "Games per hour", help: "How many hands one player may start within a rolling 60 minutes. 0 = no limit.", section: "Pace", aliases: new[] { "MaxGamesPerHour" }),
        S("timeoutRefundIfNoAction", true, page: "table", label: "Refund a hand nobody played", help: "On: a player who never hit, stood, doubled or split gets the stake back, exactly as if they had quit.", kind: "bool", section: "Pace", aliases: new[] { "TimeoutRefundIfNoAction" }),
        S("blackjackPayout", 1.5, page: "table", label: "Blackjack pays", help: "The bonus on a natural blackjack - 21 on the first two cards - paid on top of the returned stake.\nA regular win always pays 1:1.", kind: "double", section: "Commands", options: new[] { "1.5|3:2 - casino standard", "1.2|6:5 - house edge", "2.0|2:1 - generous", "1.0|1:1 - no bonus" }, aliases: new[] { "BlackjackPayout" }),
        // ---- duel ----
        S("duelEnabled", true, page: "duel", label: "Duels", help: "Off: the duel command only ever replies that duels are disabled.", kind: "bool", section: "Duel Mode", aliases: new[] { "DuelEnabled" }),
        S("duelCountsTowardGamesPerHour", true, page: "duel", label: "Duels count towards games per hour", help: "Counts a duel as a game for both players against the Games per hour limit on the Table page.", kind: "bool", section: "Duel Mode", aliases: new[] { "DuelCountsTowardGamesPerHour" }),
        S("duelApplyAddictedCut", false, page: "duel", label: "Cut an addicted player's duel winnings", help: "Off by default. The points in a duel come from the other player, not the house, so the cut would delete them\nfrom circulation rather than move them.", kind: "bool", section: "Duel Mode", aliases: new[] { "DuelApplyAddictedCut" }),
        S("duelMinBet", 1L, page: "duel", label: "Minimum duel stake", help: "Anything lower is raised to this. Separate from the table minimum, so duels can have a floor of their own.", kind: "long", section: "Stakes", aliases: new[] { "DuelMinBet" }),
        S("duelMaxBet", 0L, page: "duel", label: "Maximum duel stake", help: "Anything higher is cut down to this.\n0 = no ceiling.", kind: "long", section: "Stakes", unlimitedAtZero: true, aliases: new[] { "DuelMaxBet" }),
        S("duelAcceptTimeoutSec", 60, page: "duel", label: "Answer a challenge", help: "How long the challenged player has before the challenge expires and the stake goes back, in seconds.\nAt least 10.", kind: "int", section: "Deadlines", aliases: new[] { "DuelAcceptTimeoutSec" }),
        S("duelTurnTimeoutSec", 90, page: "duel", label: "Take a turn", help: "How long a player has to act before they stand automatically, in seconds. 0 = no limit.", kind: "int", section: "Deadlines", aliases: new[] { "DuelTurnTimeoutSec" }),
        S("duelConsentTimeoutSec", 45, page: "duel", label: "Answer a raise", help: "How long the opponent has to answer a split or double, and how long both have to answer a sudden-death offer.\nAt least 5.", kind: "int", section: "Deadlines", aliases: new[] { "DuelConsentTimeoutSec" }),
        S("duelAllowDoubleDown", true, page: "duel", label: "Double down", help: "Lets a player double their stake for one more card, once the opponent agrees.", kind: "bool", section: "Raises", aliases: new[] { "DuelAllowDoubleDown" }),
        S("duelAllowSplit", true, page: "duel", label: "Split", help: "Lets a player split a pair, once the opponent agrees. Each hand then plays for its own equally funded side pot,\nand a player with fewer hands defends every pot with the single one they have.", kind: "bool", section: "Raises", aliases: new[] { "DuelAllowSplit" }),
        S("duelSuddenDeathEnabled", false, page: "duel", label: "Offer it on a tie", help: "Only on an unsplit duel - a single pot - that ends in a genuine tie. Duels with splits always push.", kind: "bool", section: "Sudden death", aliases: new[] { "DuelSuddenDeathEnabled" }),
        S("duelMaxSuddenDeathRounds", 3, page: "duel", label: "Rounds at most", help: "How often sudden death may repeat while the players keep tying. After the last one the pot is split.", kind: "int", section: "Sudden death", options: new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", "10" }, aliases: new[] { "DuelMaxSuddenDeathRounds" }),
        // ---- house ----
        S("houseEnabled", true, page: "house", label: "House Mode", help: "Off: the house command only ever replies that House Mode is disabled.", kind: "bool", section: "House Mode", aliases: new[] { "HouseEnabled" }),
        S("houseCountsTowardGamesPerHour", true, page: "house", label: "Seats count towards games per hour", help: "Counts taking a seat as a game for that player against the Games per hour limit on the Table page.", kind: "bool", section: "House Mode", aliases: new[] { "HouseCountsTowardGamesPerHour" }),
        S("houseMaxSeats", 6, page: "house", label: "Seats", help: "How many players can sit down. The table deals as soon as the last seat is taken, without waiting out the join window.", kind: "int", section: "The table", options: new[] { "2", "3", "4", "5", "6" }, aliases: new[] { "HouseMaxSeats" }),
        S("houseRulePickTimeoutSec", 30, page: "house", label: "Rule pick", help: "How long the moderator has to answer the rule set menu, in seconds. Nothing is staked yet, so the table is simply dropped.\nAt least 10.", kind: "int", section: "The table", aliases: new[] { "HouseRulePickTimeoutSec" }),
        S("houseJoinWindowSec", 60, page: "house", label: "Join window", help: "How long viewers have to take a seat before the table deals itself, in seconds. With nobody seated it closes again.\nAt least 15.", kind: "int", section: "The table", aliases: new[] { "HouseJoinWindowSec" }),
        S("houseInsuranceWindowSec", 20, page: "house", label: "Insurance window", help: "How long seats have to insure when the dealer shows an Ace, before the dealer looks at the hole card, in seconds.\nOnly used by rule sets that offer insurance. At least 5.", kind: "int", section: "The table", aliases: new[] { "HouseInsuranceWindowSec" }),
        S("houseTurnTimeoutSec", 60, page: "house", label: "Seat turn timeout", help: "How long one seat may think before it stands automatically and play moves on, in seconds. 0 = no limit.", kind: "int", section: "The table", aliases: new[] { "HouseTurnTimeoutSec" }),
        S("houseMinBet", 1L, page: "house", label: "Minimum seat stake", help: "Anything lower is raised to this. The player still needs the balance to cover it.", kind: "long", section: "Stakes", aliases: new[] { "HouseMinBet" }),
        S("houseMaxBet", 0L, page: "house", label: "Maximum seat stake", help: "Anything higher is cut down to this.\n0 = no ceiling.", kind: "long", section: "Stakes", unlimitedAtZero: true, aliases: new[] { "HouseMaxBet" }),
        S("houseRuleVegasEnabled", true, page: "house", label: "Vegas Strip", help: "4 deck shoe, dealer stands on soft 17, insurance offered, blackjack pays 3:2.", kind: "bool", section: "Rule sets on offer", aliases: new[] { "HouseRuleVegasEnabled" }),
        S("houseRuleAtlanticEnabled", true, page: "house", label: "Atlantic City", help: "8 deck shoe, dealer stands on soft 17, late surrender, resplit to three hands.", kind: "bool", section: "Rule sets on offer", aliases: new[] { "HouseRuleAtlanticEnabled" }),
        S("houseRuleEuropeanEnabled", true, page: "house", label: "European", help: "2 deck shoe, no hole card, double on 9 to 11 only, no surrender.", kind: "bool", section: "Rule sets on offer", aliases: new[] { "HouseRuleEuropeanEnabled" }),
        // ---- access ----
        S("pointsVariable", "points", page: "access", label: "Points variable", help: "The name of the Streamer.bot user variable your currency is kept in.", section: "Currency", aliases: new[] { "PointsVariable" }),
        S("pointsName", "Points", page: "access", label: "Points name", help: "What the currency is called in chat messages. Overwritten automatically when Currency Core or LOYALTY POINTS is detected.", section: "Currency", aliases: new[] { "PointsVarName" }),
        S("cardDisplayMode", "emojivalue", page: "access", label: "Cards in chat", help: "How cards are written in chat, in every mode. Worth moving off the card characters if your chat renders them as boxes.", kind: "options", section: "Currency", options: new[] { "emojivalue|Card and number - 🂡(11)", "emoji|Card only - 🂡", "value|Number only - 11" }, aliases: new[] { "CardDisplayMode" }),
        // ---- (conventions) ----
        S("blockedUsers", "", label: "Blocked viewers", help: "Names matched without regard to case, and a leading @ is ignored. A blocked viewer's commands are dropped\nwithout a reply, so retrying cannot spam chat.", section: "Kept out", aliases: new[] { "BlockedUsers" }),
        S("blockedGroups", "ExampleGroup, TooAddictedToPlay, YourCustomBlockedGroup", label: "Blocked groups", help: "Streamer.bot user groups, matched exactly. Being in any one of them is enough.", section: "Kept out", aliases: new[] { "BlockedGroups" }),
        S("addictedUsers", "", label: "Addicted viewers", help: "Names matched without regard to case. Being on this list or in one of the groups below is enough.", section: "Addicted list", aliases: new[] { "AddictedUsers" }),
        S("addictedGroups", "", label: "Addicted groups", help: "Streamer.bot user groups, matched exactly.", section: "Addicted list", aliases: new[] { "AddictedGroups" }),
        S("addictedPayoutCut", 50, label: "Payout cut", help: "How much of an addicted player's winnings is withheld. Capped at 99%, so a win can never turn into a loss.", section: "Addicted list", aliases: new[] { "AddictedPayoutCut" }, aliasFactor: 100),
        // ---- access ----
        S("globalCooldown", 0, page: "access", label: "Table, everyone", help: "How long after any hand before anyone can start another, in seconds.", section: "Cooldowns", aliases: new[] { "GlobalCooldown" }),
        S("userCooldown", 0, page: "access", label: "Table, per player", help: "How long one player waits between their own hands, in seconds.", section: "Cooldowns", aliases: new[] { "UserCooldown" }),
        S("duelGlobalCooldown", 0, page: "access", label: "Duel, everyone", help: "How long after any challenge before anyone can issue another, in seconds.", kind: "int", section: "Cooldowns", aliases: new[] { "DuelGlobalCooldown" }),
        S("duelUserCooldown", 0, page: "access", label: "Duel, per player", help: "How long one player waits between issuing challenges, in seconds.\nAnswering a challenge is never blocked, so a duel can always be finished.", kind: "int", section: "Cooldowns", aliases: new[] { "DuelUserCooldown" }),
        S("houseGlobalCooldown", 0, page: "access", label: "House, everyone", help: "How long after a table is opened before anyone can open another, in seconds.", kind: "int", section: "Cooldowns", aliases: new[] { "HouseGlobalCooldown" }),
        S("houseUserCooldown", 0, page: "access", label: "House, per player", help: "How long one player waits between opening tables, in seconds.\nTaking a seat at someone else's table is never blocked.", kind: "int", section: "Cooldowns", aliases: new[] { "HouseUserCooldown" }),
        // ---- (conventions) ----
        S("overlayScale", 100, label: "Overlay", help: "Resizes cards, text and layout together. 100 is the built-in size, 150 is half again as big.", section: "Size", min: 25.0, max: 400.0, aliases: new[] { "OverlayScale" }, aliasFactor: 100),
        S("overlayFontScale", 100, label: "Text", help: "Resizes names, totals and badges on their own, leaving the cards as they are.", section: "Size", min: 25.0, max: 400.0, aliases: new[] { "OverlayFontScale" }, aliasFactor: 100),
        // ---- overlay ----
        S("overlayShowPoints", true, page: "overlay", label: "Points and stakes", help: "Shows the stake and insurance amounts. Turn it off to keep numbers off stream.", kind: "bool", section: "What is shown", aliases: new[] { "OverlayShowPoints" }),
        S("overlayShowHandValues", true, page: "overlay", label: "Hand totals", help: "Shows the running total beside each hand and the dealer's.", kind: "bool", section: "What is shown", aliases: new[] { "OverlayShowHandValues" }),
        S("overlayShowOutcomeBadges", true, page: "overlay", label: "Outcome badges", help: "Shows BLACKJACK, BUST, WIN, LOSE and PUSH beside a hand once it has been settled.", kind: "bool", section: "What is shown", aliases: new[] { "OverlayShowOutcomeBadges" }),
        S("overlayShowInsurance", true, page: "overlay", label: "Insurance line", help: "Shows a line under the dealer's cards while a player holds insurance.", kind: "bool", section: "What is shown", aliases: new[] { "OverlayShowInsurance" }),
        // ---- (conventions) ----
        S("overlayAnimations", true, label: "Deal animation", help: "Slides each card in as it is dealt. Off, the cards simply appear.", section: "What is shown", aliases: new[] { "OverlayAnimationsEnabled" }),
        S("overlayHideOnFreeGame", false, label: "Hide during Free Game", help: "Keeps the overlay off screen entirely while Free Game is on.", section: "What is shown", aliases: new[] { "OverlayHideOnFreeGame" }),
        // ---- overlay ----
        S("overlayDealerName", "Dealer", page: "overlay", label: "Dealer is called", help: "The label above the dealer's cards.", kind: "string", section: "Labels and timing", aliases: new[] { "OverlayDealerName" }),
        // ---- (conventions) ----
        S("overlayResultSeconds", 8, label: "Hold the result", help: "How long a finished hand stays on screen before it fades, in seconds.\nWorth raising if a full table settles faster than viewers can read it. At least 1.", kind: "int", section: "Labels and timing", aliases: new[] { "OverlayGameOverFadeSec" }),
        // ---- overlay ----
        S("overlayCardRedColor", "#d3283a", page: "overlay", label: "Red suits", help: "Hearts and diamonds.", kind: "colour", section: "Colours", aliases: new[] { "OverlayCardRedColor" }),
        S("overlayCardBlackColor", "#1a1a1a", page: "overlay", label: "Black suits", help: "Spades and clubs.", kind: "colour", section: "Colours", aliases: new[] { "OverlayCardBlackColor" }),
        // ---- (conventions) ----
        S("overlayBackgroundColour", "#000000", label: "Backdrop", help: "A panel behind the table. Only visible once the opacity below is above 0.", section: "Colours", aliases: new[] { "OverlayBackgroundColor" }),
        S("overlayBackgroundOpacity", 0.0, label: "Backdrop opacity", help: "0 leaves the cards floating on your scene, 100 paints a solid panel behind them.", section: "Colours", min: 0.0, max: 100.0, factor: 1.0, aliases: new[] { "OverlayBackgroundOpacity" }),
        // ---- overlay ----
        S("obsConnectionId", 0, page: "overlay", label: "Connection", help: "Which Streamer.bot OBS connection to use. 0 is the first one.", kind: "int", section: "Set it up in OBS", aliases: new[] { "ObsConnectionId" }),
        S("obsSceneName", "Blackjack", page: "overlay", label: "Scene", help: "The scene the source is added to. Created if it does not exist yet.", kind: "string", section: "Set it up in OBS", aliases: new[] { "ObsSceneName" }),
        S("obsSourceName", "Blackjack Overlay", page: "overlay", label: "Source", help: "The browser source created inside that scene. Nothing is changed if a source of this name is already there.", kind: "string", section: "Set it up in OBS", aliases: new[] { "ObsSourceName" }),
        S("setupObsOverlay", null, page: "overlay", label: "Create the browser source in OBS", help: "Creates the scene above if it is missing and adds a browser source pointing at the overlay file. Needs OBS connected in Streamer.bot.", kind: "action", section: "Set it up in OBS", confirm: "This creates the scene if it is missing and adds a browser source pointing at the overlay file, over the OBS connection set above. Go ahead?", row: "obsButtons"),
        S("overlaySample", null, page: "overlay", label: "Show a sample round", help: "Plays one made-up hand on the overlay for about ten seconds, the one in OBS included, so you can see where it sits and how it looks. If you are live, your viewers see it too. Uses the settings as saved; nobody's points move. While a hand, duel or House table is running the overlay is left to it.", kind: "action", section: "Set it up in OBS", row: "obsButtons"),
        S("overlayDemo", null, page: "overlay", label: "Try the demo", help: "Opens the overlay in your browser, playing made-up hands from a solo hand to a duel and a House table in the look the saved settings give it, without Streamer.bot. Add &demo=1 to the source's URL in OBS to place it the same way.", kind: "action", section: "Set it up in OBS", row: "obsButtons"),
        S("overlayFolder", "Overlay", page: "overlay", label: "Overlay folder", help: "A subfolder name kept under Chat Games\\Blackjack Game, or a full path, so the overlay can live anywhere. Empty uses 'Overlay'.", kind: "path", section: "Files", aliases: new[] { "OverlayFolder" }),
        // ---- messages ----
        Msg("Playing a hand", "msgGameStart", "🃏 {user} is dealt in. ", "Hand dealt", "Opens a round. The hand and the dealer's upcard are appended to it.", new[] { "user", "cards", "value" }, alias: "MsgGameStart"),
        Msg("Playing a hand", "msgDrawCard", "{user} draws {card}. Hand: {cards} ({value})", "Hit", "Sent every time the player draws, bust or not.", new[] { "user", "card", "cards", "value" }, alias: "MsgDrawCard"),
        Msg("Playing a hand", "msgDoubleDown", "{user} doubles down and draws {card}. Hand: {cards} ({value})", "Double down", "Sent when the stake is doubled for exactly one more card.", new[] { "user", "card", "cards", "value" }, alias: "MsgDoubleDown"),
        Msg("Playing a hand", "msgSplit", "{user} splits. Hand 1: {cards} ({value}) - hit, double or stand.", "Split", "Sent on a split or resplit, describing the first of the two hands.", new[] { "user", "cards", "value" }, alias: "MsgSplit"),
        Msg("Playing a hand", "msgMovingToHand", "On to hand {handnum}: {cards} ({value}) - hit, double or stand.", "Next split hand", "Sent when play moves on to the next hand. Hands that stand on their own, like split Aces, are skipped.", new[] { "handnum", "cards", "value" }, alias: "MsgMovingToHand"),
        Msg("Playing a hand", "msgAceReduced", "An Ace drops to 1 to keep that under 22.", "Ace dropped to 1", "Only sent while 'Say when an Ace drops to 1' is on, and only when it actually happened.", new string[0], alias: "MsgAceReduced"),
        Msg("Playing a hand", "msgQuitRefund", "Hand cancelled, {amount} {points} back in your pocket.", "Quit", "Sent when a player quits before acting. Everything staked comes back, split bets included.", new[] { "amount", "points" }, alias: "MsgQuitRefund"),
        Msg("Wins", "msgBlackjackWin", " 🃏 B L A C K J A C K 🃏 - {amount} {points} to you!", "Blackjack", "Sent on a natural blackjack - 21 on the first two cards - paid at the Blackjack pays ratio rather than 1:1.", new[] { "user", "amount", "points" }, alias: "MsgBlackjackWin"),
        Msg("Wins", "msgRegularWin", "You take {amount} {points}.", "Win", "Sent on an ordinary 1:1 win: the player's hand beats the dealer's, neither being a blackjack.", new[] { "user", "amount", "points" }, alias: "MsgRegularWin"),
        Msg("Wins", "msgHandWin", "Hand {handnum} wins!", "Split hand won", "Added when one hand out of a split wins its own comparison against the dealer.", new[] { "handnum" }, alias: "MsgHandWin"),
        Msg("Wins", "msgSpecialWin5Card", "Five cards without busting - 5-card Charlie!", "5-card Charlie", "Sent the moment a hand reaches five cards without busting. Pays the stake back plus a 1.5x bonus, whatever the dealer holds.", new string[0], alias: "MsgSpecialWin5Card"),
        Msg("Wins", "msgSpecialWin3Sevens", "Three sevens on the table!", "Three 7s", "Sent the moment a hand holds three 7s without busting. Pays the stake back plus a 2x bonus, whatever the dealer holds.", new string[0], alias: "MsgSpecialWin3Sevens"),
        Msg("Losses", "msgLoss", "{amount} {points} to the house.", "Loss", "Sent when the player's hand simply comes up short - no bust, no tie.", new[] { "user", "amount", "points" }, alias: "MsgLoss"),
        Msg("Losses", "msgBust", "Bust! 💔", "Bust", "Sent the moment a hand goes over 21 on a hit or a double. The stake is gone before the dealer plays.", new string[0], alias: "MsgBust"),
        Msg("Losses", "msgHandBust", "Hand {handnum} busts. 💔", "Split hand busted", "Sent instead of Bust when the hand that went over was part of a split.", new[] { "handnum" }, alias: "MsgHandBust"),
        Msg("Losses", "msgHandLost", "{user}, hand {handnum} goes to the dealer.", "Split hand lost", "Added when one hand out of a split loses its own comparison against the dealer.", new[] { "user", "handnum" }, alias: "MsgHandLost"),
        Msg("Losses", "msgHandTie", "Hand {handnum} is level.", "Split hand tied", "Goes into the tie-break announcement as the {hands} part, naming the hand that tied.", new[] { "handnum" }, alias: "MsgHandTie"),
        Msg("The dealer", "msgDealerDraws", "Dealer draws {card} → {cards}", "Dealer draws", "Sent for each dealer card, both while drawing to the stand total and while breaking a tie.", new[] { "card", "cards" }, alias: "MsgDealerDraws"),
        Msg("The dealer", "msgDealerReveal", "🃏 Dealer turns over {card} → {cards} ({value})", "Hole card turned over", "Only ever seen with 'Dealer peeks for blackjack' on, since that is the only time a face-down card exists.", new[] { "card", "cards", "value" }, alias: "MsgDealerReveal"),
        Msg("The dealer", "msgDealerBusts", "Dealer busts on {total} with {cards}.", "Dealer busts", "Sent when a tie-break card puts the dealer over 21, so the hand that had tied wins after all.", new[] { "cards", "total" }, alias: "MsgDealerBusts"),
        Msg("The dealer", "msgDealerWins", "Dealer stands on {total} with {cards} and takes it.", "Dealer wins untouched", "Sent when every player hand had already busted, so the dealer never needs to draw.", new[] { "cards", "total" }, alias: "MsgDealerWins"),
        Msg("The dealer", "msgDealerDrawCard", "{hands} One more card for the dealer...", "Breaking a tie", "Announces the extra card. {hands} is the Split hand tied line above.", new[] { "hands" }, alias: "MsgDealerDrawCard"),
        Msg("The dealer", "msgDealerTieWin", "Still level, so the dealer takes it.", "Tie survived", "Sent when the tie-break leaves the dealer level with the player, which the dealer wins.", new string[0], alias: "MsgDealerTieWin"),
        Msg("The dealer", "msgDealerBeatsYou", "The dealer has it.", "Tie broken for the dealer", "Sent when the tie-break puts the dealer genuinely ahead.", new string[0], alias: "MsgDealerBeatsYou"),
        Msg("The dealer", "msgDealerPeekBlackjack", "🃏 Dealer checks and has Blackjack: {cards}.", "Peek found a blackjack", "Sent when the dealer checks the hole card on an Ace or a ten and holds blackjack, which ends the hand there.", new[] { "cards" }, alias: "MsgDealerPeekBlackjack"),
        Msg("The dealer", "msgDealerBlackjackPush", "🤝 Blackjack both ways - push. {amount} {points} returned.", "Both have blackjack", "Sent when dealer and player both hold blackjack: a push, and the stake comes straight back.", new[] { "amount", "points" }, alias: "MsgDealerBlackjackPush"),
        Msg("The dealer", "msgFinalSummary", "Final - {user}: {playercards} ({playervalue}) vs dealer {dealercards} ({dealervalue})", "Final summary", "Sent just ahead of most results, laying both final hands side by side.", new[] { "user", "playercards", "playervalue", "dealercards", "dealervalue" }, alias: "MsgFinalSummary"),
        Msg("Insurance and surrender", "msgInsuranceOffer", "{user}, the dealer shows an Ace. Type insurance to cover half your bet against a dealer blackjack - it pays 2:1.", "Insurance offered", "Sent right after the deal, only with Insurance on and the dealer showing an Ace. The one chance to take it.", new[] { "user" }, alias: "MsgInsuranceOffer"),
        Msg("Insurance and surrender", "msgInsuranceTaken", "{user} insures for {amount} {points}.", "Insurance taken", "Sent when the player insures. Half the original stake is set aside.", new[] { "user", "amount", "points" }, alias: "MsgInsuranceTaken"),
        Msg("Insurance and surrender", "msgInsuranceWin", "🛡️ Dealer had blackjack after all - insurance pays {amount} {points}.", "Insurance paid", "Sent when the dealer does hold blackjack. Pays 2:1 on the insurance stake, whatever the main hand does.", new[] { "amount", "points" }, alias: "MsgInsuranceWin"),
        Msg("Insurance and surrender", "msgInsuranceLose", "No dealer blackjack, so the insurance goes.", "Insurance lost", "Sent when the dealer has no blackjack after all. Only the insurance stake is gone; the hand plays on.", new string[0], alias: "MsgInsuranceLose"),
        Msg("Insurance and surrender", "msgInsuranceNotAvailable", "{user}, insurance isn't available right now.", "Insurance refused", "Sent when insurance does not apply: the rule is off, it was not offered, it is already taken, or the player has acted.", new[] { "user" }, alias: "MsgInsuranceNotAvailable"),
        Msg("Insurance and surrender", "msgInsuranceInsufficientFunds", "{user}, insurance needs {amount} more {points}.", "Insurance too dear", "Sent when the player cannot cover half their stake.", new[] { "user", "points", "amount" }, alias: "MsgInsuranceInsufficientFunds"),
        Msg("Insurance and surrender", "msgSurrenderResult", "{user} folds and keeps half the bet - {amount} {points} back. Balance: {balance} {points}.", "Surrendered", "Sent when a player folds a fresh hand and keeps half the stake.", new[] { "user", "amount", "points", "balance" }, alias: "MsgSurrenderResult"),
        Msg("Insurance and surrender", "msgSurrenderNotAvailable", "{user}, you can't surrender right now.", "Surrender refused", "Sent when surrender does not apply: the rule is off, insurance is already taken, or the hand is no longer two cards.", new[] { "user" }, alias: "MsgSurrenderNotAvailable"),
        Msg("Bet shortcuts", "msgBettingAll", "{user} shoves the lot in: {FormatNumber(bet)} {pointsVarName}.", "all / allin", "Confirms a bet of the whole balance. The figure is already cut to the Maximum bet.", new[] { "user", "FormatNumber(bet)", "pointsVarName" }, alias: "MsgBettingAll"),
        Msg("Bet shortcuts", "msgBettingHalf", "{user} puts up half the stack: {FormatNumber(bet)} {pointsVarName}.", "half", "Confirms a bet of half the balance.", new[] { "user", "FormatNumber(bet)", "pointsVarName" }, alias: "MsgBettingHalf"),
        Msg("Bet shortcuts", "msgBettingQuarter", "{user} puts up a quarter: {FormatNumber(bet)} {pointsVarName}.", "quarter / 25%", "Confirms a bet of a quarter of the balance.", new[] { "user", "FormatNumber(bet)", "pointsVarName" }, alias: "MsgBettingQuarter"),
        Msg("Bet shortcuts", "msgBettingRandom", "{user} leaves it to chance: {FormatNumber(bet)} {pointsVarName}.", "random / rand", "Confirms a random bet between a tenth of the balance and whatever the player can afford.", new[] { "user", "FormatNumber(bet)", "pointsVarName" }, alias: "MsgBettingRandom"),
        Msg("Bet shortcuts", "msgBettingDouble", "{user} doubles the minimum: {FormatNumber(bet)} {pointsVarName}.", "double / 2x", "Confirms a bet of twice the minimum.", new[] { "user", "FormatNumber(bet)", "pointsVarName" }, alias: "MsgBettingDouble"),
        Msg("Bet shortcuts", "msgBettingMax", "{user} goes to the limit: {FormatNumber(bet)} {pointsVarName}.", "max / maximum", "Confirms the largest bet allowed, held down by the balance and the Maximum bet.", new[] { "user", "FormatNumber(bet)", "pointsVarName" }, alias: "MsgBettingMax"),
        Msg("Bet shortcuts", "msgBetMinApplied", "{user}, that works out below the minimum, so {min} it is.", "Minimum used instead", "Sent when a shortcut works out below the Minimum bet, so the minimum is played.", new[] { "user", "min" }, alias: "MsgBetMinApplied"),
        Msg("Free Game", "msgBlackjackWinFree", " 🃏 B L A C K J A C K 🃏 - that's the hand!", "Blackjack", "Replaces the Blackjack line while Free Game is on.", new[] { "user" }, alias: "MsgBlackjackWinFree"),
        Msg("Free Game", "msgRegularWinFree", "You take it.", "Win", "Replaces the Win line while Free Game is on.", new[] { "user" }, alias: "MsgRegularWinFree"),
        Msg("Free Game", "msgLossFree", "The dealer takes it. 💔", "Loss", "Replaces the Loss line while Free Game is on, since nothing was staked.", new[] { "user" }, alias: "MsgLossFree"),
        Msg("Free Game", "msgQuitRefundFree", "Hand cancelled.", "Quit", "Replaces the Quit line while Free Game is on - there is nothing to give back.", new[] { "user" }, alias: "MsgQuitRefundFree"),
        Msg("Free Game", "msgSurrenderResultFree", "{user} folds.", "Surrendered", "Replaces the Surrendered line while Free Game is on.", new[] { "user" }, alias: "MsgSurrenderResultFree"),
        Msg("Free Game", "msgDealerTieWinFree", "Still level, so the dealer takes it.", "Tie survived", "Replaces the Tie survived line while Free Game is on.", new string[0], alias: "MsgDealerTieWinFree"),
        Msg("Free Game", "msgDealerBeatsYouFree", "The dealer has it.", "Tie broken for the dealer", "Replaces the Tie broken line while Free Game is on.", new string[0], alias: "MsgDealerBeatsYouFree"),
        Msg("Free Game", "msgTurnTimeoutRefundFree", "⏰ @{user} ran out of time. Hand cancelled.", "Timed out", "Replaces the refund line while Free Game is on.", new[] { "user" }, alias: "MsgTurnTimeoutRefundFree"),
        Msg("Free Game", "msgFreeGameStart", "🃏 Free Game is ON! Hands are played for nothing - no points needed!", "Started", "Sent once when Free Game is turned on by !SetFreeBlackjack, in the chat the command came from. Turned on another way, say by an Execute C# Method on StartFreeGame in an action of your own, it goes to every platform with an account in Streamer.bot - to YouTube only while a broadcast is monitored.", new string[0], alias: "MsgFreeGameStart"),
        Msg("Free Game", "msgFreeGameEnd", "⏹️ Free Game has ended. Hands are played for points again!", "Ended", "Sent once when Free Game is turned off by !EndFreeBlackjack, in the chat the command came from. Turned off another way, say by an Execute C# Method on EndFreeGame, it goes to every platform with an account in Streamer.bot - to YouTube only while a broadcast is monitored.", new string[0], alias: "MsgFreeGameEnd"),
        Msg("Table refusals", "msgInsufficientSplit", "Splitting needs {amount} more {points}.", "Cannot afford a split", "Sent when a split would need more than the player has left.", new[] { "points", "amount" }, alias: "MsgInsufficientSplit"),
        Msg("Table refusals", "msgInsufficientDouble", "Doubling needs {amount} more {points}.", "Cannot afford a double", "Sent when a double would need more than the player has left.", new[] { "points", "amount" }, alias: "MsgInsufficientDouble"),
        Msg("Table refusals", "msgAlreadyActiveGame", "You have a hand on the table - hit, stand, double or split.", "Already in a hand", "Sent when a player starts a hand while still holding one.", new string[0], alias: "MsgAlreadyActiveGame"),
        Msg("Table refusals", "msgNoActiveGame", "{user}, nothing on the table. Deal in with !blackjack <bet>.", "No hand running", "Sent when hit, stand, double, split, insurance, surrender, repeat or quit arrives with no hand on the table.", new[] { "user" }, alias: "MsgNoActiveGame"),
        Msg("Table refusals", "msgGameAlreadyOver", "{user}, that hand is already settled.", "Hand already over", "Sent when a player acts on a hand that has already been settled.", new[] { "user" }, alias: "MsgGameAlreadyOver"),
        Msg("Table refusals", "msgQuitOnlyBefore", "You can only quit before your first move.", "Too late to quit", "Sent when quit arrives after hitting, standing or doubling. By then there is a real hand on the table.", new string[0], alias: "MsgQuitOnlyBefore"),
        Msg("Table refusals", "msgCannotSplitValue", "{card1} and {card2} aren't worth the same, so they don't split.", "Cards do not match", "Sent when the two cards are not worth the same.", new[] { "card1", "card2" }, alias: "MsgCannotSplitValue"),
        Msg("Table refusals", "msgCannotSplitCount", "Splitting needs exactly two cards, and you hold {cards}.", "Not two cards", "Sent when a split is tried on a hand that is no longer two cards, after a hit for instance.", new[] { "cards" }, alias: "MsgCannotSplitCount"),
        Msg("Table refusals", "msgAlreadySplit", "One split per hand.", "Already split", "Sent when a split is tried again while Resplit is off - one split per round by default.", new string[0], alias: "MsgAlreadySplit"),
        Msg("Table refusals", "msgSplitDisabled", "Splitting is off at this table.", "Splitting off", "Sent when split is used at all while the rule is off on the Table page.", new string[0], alias: "MsgSplitDisabled"),
        Msg("Table refusals", "msgMaxSplitHandsReached", "{max} hands is the limit.", "No more hands", "Sent when a resplit would pass the Hands from splitting limit.", new[] { "max" }, alias: "MsgMaxSplitHandsReached"),
        Msg("Table refusals", "msgCannotDoubleCount", "Doubling needs exactly two cards, and you hold {cards}.", "Not two cards", "Sent when a double is tried on a hand that is no longer two cards.", new[] { "cards" }, alias: "MsgCannotDoubleCount"),
        Msg("Table refusals", "msgMustDoubleFirst", "Too late to double on {cards} - it has to be your first move.", "Too late to double", "Sent when a double is tried after hitting or standing on that hand. Doubling is a first move only.", new[] { "cards" }, alias: "MsgMustDoubleFirst"),
        Msg("Table refusals", "msgAlreadyDoubledDown", "This hand is already doubled.", "Already doubled", "Sent when the same hand is doubled twice.", new string[0], alias: "MsgAlreadyDoubledDown"),
        Msg("Table refusals", "msgDoubleDownDisabled", "Doubling is off at this table.", "Doubling off", "Sent when double is used at all while the rule is off on the Table page.", new string[0], alias: "MsgDoubleDownDisabled"),
        Msg("Table refusals", "msgMaxGamesReached", "That's your {max} hands for this hour.", "Hourly limit reached", "Sent when a player passes the Games per hour limit.", new[] { "max" }, alias: "MsgMaxGamesReached"),
        Msg("Table refusals", "msgPointsVarNotFound", "Couldn't read the points variable '{var}'. Check the name, or turn Free Game on.", "Points variable missing", "Sent when Free Game is off and the points variable cannot be read for that viewer - usually a wrong name, or no currency set up yet.", new[] { "var" }, alias: "MsgPointsVarNotFound"),
        Msg("Table refusals", "msgPointsNotRefunded", "{user}, {amount} could not be returned: the points variable '{var}' is unreadable.", "Refund failed", "Sent when a refund cannot be paid because the points variable is unreadable. The stake is not restored in that case.", new[] { "user", "amount", "var" }, alias: "MsgPointsNotRefunded"),
        Msg("Table refusals", "msgCardError", "{user}, the deck couldn't deal a hand. Try again.", "Deck problem", "Sent in the rare case a starting hand cannot be dealt at all. Worth reporting if you ever see it.", new[] { "user" }, alias: "MsgCardError"),
        Msg("Table refusals", "msgTurnTimeoutStand", "⏰ @{user} ran out of time and stands on {value}.", "Timed out, hand stands", "Sent when the Turn timeout runs out after the player has acted: the hand stands and the dealer plays it out.", new[] { "user", "value" }, alias: "MsgTurnTimeoutStand"),
        Msg("Table refusals", "msgTurnTimeoutRefund", "⏰ @{user} ran out of time. Hand cancelled, {amount} {points} returned.", "Timed out, stake back", "Sent when the Turn timeout runs out before the player acted at all and 'Refund a hand nobody played' is on.", new[] { "user", "amount", "points" }, alias: "MsgTurnTimeoutRefund"),
        Msg("System", "msgBalance", "Balance: {balance} {points}.", "Balance", "Added after most results to show where the viewer now stands.", new[] { "balance", "points" }, alias: "MsgBalance"),
        Msg("System", "msgTutorial", "Blackjack: !blackjack <bet> to deal in, then hit, stand, double or split.", "How to play", "Sent once per Streamer.bot session, the first time anyone plays.", new string[0], alias: "MsgTutorial"),
        Msg("System", "msgRepeatNothing", "There's nothing to repeat.", "Nothing to repeat", "Sent when repeat is used before the round has said anything.", new[] { "user" }, alias: "MsgRepeatNothing"),
        Msg("System", "msgAddictedFee", "⚠️ Addiction fee: {addictedCutPercent}% of that win was withheld.", "Addiction fee", "Added to a win when the winner is on the Addicted list.", new[] { "addictedCutPercent" }, alias: "MsgAddictedFee"),
        Msg("Duel: the challenge", "msgDuelChallenge", "⚔️ {challenger} calls out @{opponent} for {bet} {points}. @{opponent}, accept or decline within {seconds}s.", "Challenge issued", "Opens a duel and tells the challenged player to answer.", new[] { "challenger", "opponent", "bet", "points", "seconds" }, alias: "MsgDuelChallenge"),
        Msg("Duel: the challenge", "msgDuelAccepted", "⚔️ {opponent} is in. {challenger} vs {opponent} for {pot} {points} - {challenger} plays first.", "Accepted", "Sent when the challenge is taken and the cards go out.", new[] { "challenger", "opponent", "pot", "points" }, alias: "MsgDuelAccepted"),
        Msg("Duel: the challenge", "msgDuelDeclined", "🚫 {opponent} passes. {challenger} gets the {bet} {points} back.", "Declined", "Sent when the challenged player says no. The challenger's stake goes back.", new[] { "challenger", "opponent", "bet", "points" }, alias: "MsgDuelDeclined"),
        Msg("Duel: the challenge", "msgDuelExpired", "⏰ No word from {opponent}. {challenger} gets the {bet} {points} back.", "No answer", "Sent when nobody answered inside the accept deadline.", new[] { "challenger", "opponent", "bet", "points" }, alias: "MsgDuelExpired"),
        Msg("Duel: the challenge", "msgDuelAcceptedFree", "⚔️ {opponent} is in. {challenger} vs {opponent} - {challenger} plays first.", "Accepted (Free Game)", "Replaces Accepted while Free Game is on.", new[] { "challenger", "opponent" }, alias: "MsgDuelAcceptedFree"),
        Msg("Duel: the challenge", "msgDuelDeclinedFree", "🚫 {opponent} passes.", "Declined (Free Game)", "Replaces Declined while Free Game is on.", new[] { "challenger", "opponent" }, alias: "MsgDuelDeclinedFree"),
        Msg("Duel: the challenge", "msgDuelExpiredFree", "⏰ No word from {opponent}, so the duel is off.", "No answer (Free Game)", "Replaces No answer while Free Game is on - there is no stake to return.", new[] { "challenger", "opponent" }, alias: "MsgDuelExpiredFree"),
        Msg("Duel: play", "msgDuelHandDealt", "{user}: {cards} ({value})", "Hand dealt", "Sent once per player right after the deal.", new[] { "user", "cards", "value" }, alias: "MsgDuelHandDealt"),
        Msg("Duel: play", "msgDuelTurn", "{user}, you're up - hit, stand{extra}.", "Your turn", "Sent when it becomes a player's turn.", new[] { "user", "extra" }, alias: "MsgDuelTurn"),
        Msg("Duel: play", "msgDuelDrawCard", "{user} draws {card}. Hand: {cards} ({value})", "Hit", "Sent when a duellist draws.", new[] { "user", "card", "cards", "value" }, alias: "MsgDuelDrawCard"),
        Msg("Duel: play", "msgDuelStand", "🛑 {user} stands on {value}.", "Stand", "Sent when a duellist stops.", new[] { "user", "value" }, alias: "MsgDuelStand"),
        Msg("Duel: play", "msgDuelBust", "💥 {user} busts on {value}.", "Bust", "Sent when a duellist goes over 21.", new[] { "user", "value" }, alias: "MsgDuelBust"),
        Msg("Duel: play", "msgDuelTurnTimeout", "⏰ {user} ran out of time and stands.", "Turn timed out", "Sent when a player runs out the turn clock and stands automatically.", new[] { "user" }, alias: "MsgDuelTurnTimeout"),
        Msg("Duel: play", "msgDuelForfeit", "🏳️ {user} walks away from it.", "Forfeit", "Sent when a duellist quits mid-duel. The opponent takes the whole pot.", new[] { "user" }, alias: "MsgDuelForfeit"),
        Msg("Duel: play", "msgDuelHandLabel", "{user} - Hand {handnum}", "Next split hand", "Used as the prefix when play moves to a player's next split hand; the cards follow it.", new[] { "user", "handnum" }, alias: "MsgDuelHandLabel"),
        Msg("Duel: play", "msgDuelRepeat", "⚔️ {challenger}: {challengercards} | {opponent}: {opponentcards} | Pot: {pot} {points} - waiting on {turn}.", "Repeat", "Sent when either duellist uses repeat. Read-only, and it never touches the turn clock.", new[] { "challenger", "opponent", "challengercards", "opponentcards", "pot", "points", "turn" }, alias: "MsgDuelRepeat"),
        Msg("Duel: play", "msgDuelRepeatFree", "⚔️ {challenger}: {challengercards} | {opponent}: {opponentcards} - waiting on {turn}.", "Repeat (Free Game)", "Replaces Repeat while Free Game is on - there is no pot to report.", new[] { "challenger", "opponent", "challengercards", "opponentcards", "turn" }, alias: "MsgDuelRepeatFree"),
        Msg("Duel: raises", "msgDuelRaiseOffer", "💰 {user} {raise}, which puts {newbet} {points} on each side. @{opponent}, accept or decline within {seconds}s.", "Raise offered", "Sent when a player splits or doubles, asking the opponent to agree to the higher stake.", new[] { "user", "raise", "opponent", "newbet", "points", "seconds" }, alias: "MsgDuelRaiseOffer"),
        Msg("Duel: raises", "msgDuelRaiseAccepted", "✅ {opponent} is good for it. {newbet} {points} a side.", "Raise accepted", "Sent when the opponent agrees and both stakes go up.", new[] { "opponent", "newbet", "points" }, alias: "MsgDuelRaiseAccepted"),
        Msg("Duel: raises", "msgDuelRaiseDeclined", "❌ {opponent} won't go up. {user} plays the hand as it stands.", "Raise declined", "Sent when the opponent says no. The raise is dropped and the raiser plays the hand as it stands.", new[] { "user", "opponent" }, alias: "MsgDuelRaiseDeclined"),
        Msg("Duel: raises", "msgDuelRaiseTimeout", "⏰ No answer, so {user} plays the hand as it stands.", "Raise unanswered", "Sent when the opponent never answered the raise.", new[] { "user" }, alias: "MsgDuelRaiseTimeout"),
        Msg("Duel: raises", "msgDuelRaiseUnaffordable", "❌ {opponent} can't cover it. {user} plays the hand as it stands.", "Raise too dear", "Sent when the opponent wanted to agree but no longer has the balance for it.", new[] { "user", "opponent" }, alias: "MsgDuelRaiseUnaffordable"),
        Msg("Duel: result", "msgDuelShowdown", "🃏 Showdown - {challenger}: {challengervalue} vs {opponent}: {opponentvalue}", "Showdown", "Sent just ahead of the result, with both totals.", new[] { "challenger", "challengervalue", "opponent", "opponentvalue" }, alias: "MsgDuelShowdown"),
        Msg("Duel: result", "msgDuelWin", "🏆 {winner} takes the {pot} {points} pot off {loser} - {amount} up. Balance: {balance} {points}.", "Win", "Sent when one duellist takes it.", new[] { "winner", "loser", "pot", "amount", "points", "balance" }, alias: "MsgDuelWin"),
        Msg("Duel: result", "msgDuelPush", "🤝 Level at {value} - both stakes back.", "Draw", "Sent when the duel ends level and both stakes go back.", new[] { "value" }, alias: "MsgDuelPush"),
        Msg("Duel: result", "msgDuelPotResult", "Hand {handnum}: {winner} takes {pot} {points}, {amount} up.", "Side pot won", "Sent once per side pot when splits were involved.", new[] { "handnum", "winner", "pot", "amount", "points" }, alias: "MsgDuelPotResult"),
        Msg("Duel: result", "msgDuelPotPush", "Hand {handnum}: level, both stakes back.", "Side pot drawn", "Sent for a side pot that ended level.", new[] { "handnum" }, alias: "MsgDuelPotPush"),
        Msg("Duel: result", "msgDuelAddictedFee", "⚠️ Addiction fee: {addictedCutPercent}% of {user}'s payout stayed behind - {amount} {points}.", "Addiction fee", "Sent after a duel result when the winner is on the Addicted list and the duel cut is on. Without it the winner would just see a smaller number than the pot.", new[] { "user", "addictedCutPercent", "amount", "points" }, alias: "MsgDuelAddictedFee"),
        Msg("Duel: result", "msgDuelWinFree", "🏆 {winner} beats {loser}!", "Win (Free Game)", "Replaces Win while Free Game is on.", new[] { "winner", "loser" }, alias: "MsgDuelWinFree"),
        Msg("Duel: result", "msgDuelPushFree", "🤝 Level at {value}.", "Draw (Free Game)", "Replaces Draw while Free Game is on.", new[] { "value" }, alias: "MsgDuelPushFree"),
        Msg("Duel: sudden death", "msgDuelSuddenDeathOffer", "⚡ Level at {value}. Double or nothing: both accept for one more card each, or decline and split the pot. ({seconds}s)", "Offered", "Sent on a tie with sudden death on. Both players have to accept.", new[] { "value", "seconds" }, alias: "MsgDuelSuddenDeathOffer"),
        Msg("Duel: sudden death", "msgDuelSuddenDeathWaiting", "⚡ {user} is in. Waiting on {opponent}.", "One in, one to go", "Sent when the first player accepts and the other still has to answer.", new[] { "user", "opponent" }, alias: "MsgDuelSuddenDeathWaiting"),
        Msg("Duel: sudden death", "msgDuelSuddenDeathDeclined", "🤝 {user} passes. Pot split, both stakes back.", "Declined", "Sent when a player says no. Everything goes back.", new[] { "user" }, alias: "MsgDuelSuddenDeathDeclined"),
        Msg("Duel: sudden death", "msgDuelSuddenDeathTimeout", "⏰ No answer. Pot split, both stakes back.", "Unanswered", "Sent when nobody answered the offer. Everything goes back.", new string[0], alias: "MsgDuelSuddenDeathTimeout"),
        Msg("Duel: sudden death", "msgDuelSuddenDeathStart", "⚡ SUDDEN DEATH round {round}! One card each, stakes doubled to {newbet} {points}.", "Round starts", "Sent once both have accepted and the extra stake is taken.", new[] { "round", "newbet", "points" }, alias: "MsgDuelSuddenDeathStart"),
        Msg("Duel: sudden death", "msgDuelSuddenDeathCard", "{user} draws {card} → {value}", "The extra card", "Sent once per player for the extra card.", new[] { "user", "card", "value" }, alias: "MsgDuelSuddenDeathCard"),
        Msg("Duel: sudden death", "msgDuelSuddenDeathMaxRounds", "🤝 Still level after {max} rounds. Pot split, everything back.", "Rounds used up", "Sent when the players kept tying to the round limit. Everything goes back.", new[] { "max" }, alias: "MsgDuelSuddenDeathMaxRounds"),
        Msg("Duel: sudden death", "msgDuelSuddenDeathBothBust", "💥 Both over - {challenger} {challengervalue}, {opponent} {opponentvalue}. Nothing left to settle it with, so the pot is split and everything goes back.", "Both busted", "Sent when the extra card puts both over 21. Neither can win from there, so the duel stops rather than drawing on, and everything goes back.", new[] { "challenger", "challengervalue", "opponent", "opponentvalue" }, alias: "MsgDuelSuddenDeathBothBust"),
        Msg("Duel: sudden death", "msgDuelSuddenDeathStartFree", "⚡ SUDDEN DEATH round {round}! One card each.", "Round starts (Free Game)", "Replaces Round starts while Free Game is on.", new[] { "round" }, alias: "MsgDuelSuddenDeathStartFree"),
        Msg("Duel: refusals", "msgDuelDisabled", "{user}, duels are off right now.", "Duels off", "Sent when the duel command is used while Duels is off.", new[] { "user" }, alias: "MsgDuelDisabled"),
        Msg("Duel: refusals", "msgDuelNoTarget", "{user}, tag who you want: !bjduel @username <bet>", "Nobody tagged", "Sent when the duel command arrives without a name.", new[] { "user" }, alias: "MsgDuelNoTarget"),
        Msg("Duel: refusals", "msgDuelSelfChallenge", "{user}, you can't duel yourself.", "Challenged yourself", "Sent when someone tags their own name.", new[] { "user" }, alias: "MsgDuelSelfChallenge"),
        Msg("Duel: refusals", "msgDuelChallengerBusy", "{user}, finish what you're in first.", "You are busy", "Sent when the challenger already has a hand, a duel, a seat or an outstanding challenge.", new[] { "user" }, alias: "MsgDuelChallengerBusy"),
        Msg("Duel: refusals", "msgDuelTargetBusy", "{user}, {opponent} is mid-hand already.", "They are busy", "Sent when the tagged player is already in a hand, a duel or a seat.", new[] { "user", "opponent" }, alias: "MsgDuelTargetBusy"),
        Msg("Duel: refusals", "msgDuelAlreadyPending", "{user}, {opponent} already has a challenge waiting.", "They were already challenged", "Sent when the tagged player already has a challenge waiting.", new[] { "user", "opponent" }, alias: "MsgDuelAlreadyPending"),
        Msg("Duel: refusals", "msgDuelTargetInsufficient", "{user}, that duel needs {required} {points} and you have {available}.", "Not enough points", "Sent when either side cannot cover the stake.", new[] { "user", "points", "required", "available" }, alias: "MsgDuelTargetInsufficient"),
        Msg("Duel: refusals", "msgDuelNoPending", "{user}, nothing is waiting on you.", "Nothing to answer", "Sent when accept or decline arrives with no prompt waiting on that player.", new[] { "user" }, alias: "MsgDuelNoPending"),
        Msg("Duel: refusals", "msgDuelNotYourTurn", "{user}, it's {opponent}'s turn.", "Not your turn", "Sent when a duellist acts out of turn.", new[] { "user", "opponent" }, alias: "MsgDuelNotYourTurn"),
        Msg("Duel: refusals", "msgDuelSplitDisabled", "{user}, duels don't allow splitting.", "Splitting off in duels", "Sent when split is used in a duel while the duel rule is off.", new[] { "user" }, alias: "MsgDuelSplitDisabled"),
        Msg("Duel: refusals", "msgDuelDoubleDisabled", "{user}, duels don't allow doubling.", "Doubling off in duels", "Sent when double is used in a duel while the duel rule is off.", new[] { "user" }, alias: "MsgDuelDoubleDisabled"),
        Msg("Duel: refusals", "msgDuelActionNotAvailable", "{user}, there's no {action} in a duel.", "Not a duel command", "Sent when insurance or surrender is used during a duel.", new[] { "user", "action" }, alias: "MsgDuelActionNotAvailable"),
        Msg("House: the table", "msgHouseRulePrompt", "🏠 @{user}, pick the rules: {menu}. Run the house command again with the number - !house 1 - within {seconds}s.", "Rule set menu", "Sent when a table is opened without naming a rule set, listing the ones ticked on the House page.", new[] { "user", "menu", "seconds" }, alias: "MsgHouseRulePrompt"),
        Msg("House: the table", "msgHouseRuleTimeout", "⏰ No rules picked, so the table closes.", "No rules picked", "Sent when nobody answered the menu. Nothing was staked, so nothing needs returning.", new string[0], alias: "MsgHouseRuleTimeout"),
        Msg("House: the table", "msgHouseTableOpen", "🏠 {user} opens a {rules} table, {seats} seats. Type join <bet> within {seconds}s. ({rulesdetail})", "Table open", "Sent when the table opens and the join window starts.", new[] { "user", "rules", "seats", "seconds", "rulesdetail" }, alias: "MsgHouseTableOpen"),
        Msg("House: the table", "msgHouseSeatTaken", "💺 {user} takes seat {seatnum}/{seats} for {bet} {points}.", "Seat taken", "Sent each time someone sits down.", new[] { "user", "seatnum", "seats", "bet", "points" }, alias: "MsgHouseSeatTaken"),
        Msg("House: the table", "msgHouseSeatLeft", "🚪 {user} leaves the table with {amount} {points} back.", "Seat left", "Sent when a seated player quits while their stake can still come back.", new[] { "user", "amount", "points" }, alias: "MsgHouseSeatLeft"),
        Msg("House: the table", "msgHouseNoPlayers", "🏠 Nobody sat down, so the table closes.", "Nobody sat down", "Sent when the join window ran out with every seat empty, so the table closes again.", new string[0], alias: "MsgHouseNoPlayers"),
        Msg("House: the table", "msgHouseDealt", "🏠 {rules} table, {seated} playing. Dealer shows {dealer}.", "Cards out", "Sent as the cards go out, ahead of the individual hands.", new[] { "rules", "seated", "dealer" }, alias: "MsgHouseDealt"),
        Msg("House: the table", "msgHouseHandDealt", "{user}: {cards} ({value}) on {bet} {points}", "Hand dealt", "Sent once per seat right after the deal.", new[] { "user", "cards", "value", "bet", "points" }, alias: "MsgHouseHandDealt"),
        Msg("House: the table", "msgHouseSeatBlackjack", "🃏 {user} has Blackjack!", "Seat has blackjack", "Sent when a seat is dealt a natural blackjack.", new[] { "user" }, alias: "MsgHouseSeatBlackjack"),
        Msg("House: play", "msgHouseTurn", "▶️ {user}, you're up on {cards} ({value}) - hit or stand{extra}.", "Your turn", "Sent when the table moves on to a seat.", new[] { "user", "cards", "value", "extra" }, alias: "MsgHouseTurn"),
        Msg("House: play", "msgHouseDrawCard", "{user} draws {card}. Hand: {cards} ({value})", "Hit", "Sent when a seat draws.", new[] { "user", "card", "cards", "value" }, alias: "MsgHouseDrawCard"),
        Msg("House: play", "msgHouseStand", "🛑 {user} stands on {value}.", "Stand", "Sent when a seat stops.", new[] { "user", "value" }, alias: "MsgHouseStand"),
        Msg("House: play", "msgHouseBust", "💥 {user} busts on {value}.", "Bust", "Sent when a seat goes over 21.", new[] { "user", "value" }, alias: "MsgHouseBust"),
        Msg("House: play", "msgHouseNextHand", "{user} - hand {handnum}: {cards} ({value})", "Next split hand", "Sent when play moves on to a seat's next split hand.", new[] { "user", "handnum", "cards", "value" }, alias: "MsgHouseNextHand"),
        Msg("House: play", "msgHouseTurnTimeout", "⏰ {user} ran out of time and stands.", "Turn timed out", "Sent when a seat runs out the turn clock and stands automatically.", new[] { "user" }, alias: "MsgHouseTurnTimeout"),
        Msg("House: play", "msgHouseSurrender", "🏳️ {user} folds for {amount} {points} back.", "Surrendered", "Sent when a seat folds, at a rule set that allows it.", new[] { "user", "amount", "points" }, alias: "MsgHouseSurrender"),
        Msg("House: play", "msgHouseInsuranceOffer", "🛡️ Dealer shows an Ace. Type insurance within {seconds}s to cover your hand.", "Insurance offered", "Sent to the whole table when the dealer shows an Ace, before the hole card is checked.", new[] { "seconds" }, alias: "MsgHouseInsuranceOffer"),
        Msg("House: play", "msgHouseInsuranceTaken", "🛡️ {user} takes insurance for {amount} {points}.", "Insurance taken", "Sent when a seat insures.", new[] { "user", "amount", "points" }, alias: "MsgHouseInsuranceTaken"),
        Msg("House: play", "msgHouseInsuranceWin", "🛡️ {user}'s insurance pays {amount} {points}.", "Insurance paid", "Sent when the dealer does hold blackjack and that seat's insurance pays.", new[] { "user", "amount", "points" }, alias: "MsgHouseInsuranceWin"),
        Msg("House: play", "msgHouseInsuranceLose", "🛡️ {user}'s insurance goes.", "Insurance lost", "Sent when the dealer has no blackjack and the insurance stake is gone.", new[] { "user" }, alias: "MsgHouseInsuranceLose"),
        Msg("House: result", "msgHouseDealerReveal", "🃏 Dealer turns over {card} → {cards} ({value})", "Hole card turned over", "Sent as the dealer's turn begins.", new[] { "card", "cards", "value" }, alias: "MsgHouseDealerReveal"),
        Msg("House: result", "msgHousePeekBlackjack", "🃏 Dealer checks and has Blackjack: {cards}.", "Peek found a blackjack", "Sent when the dealer peeks on an Ace or a ten and holds blackjack, which ends the table there.", new[] { "cards" }, alias: "MsgHousePeekBlackjack"),
        Msg("House: result", "msgHouseDealerBlackjack", "🃏 Dealer turns over Blackjack: {cards}. The whole table loses what it staked.", "Dealer blackjack", "Sent at a table where the dealer does not peek and turns one over, taking everything staked at every seat.", new[] { "cards" }, alias: "MsgHouseDealerBlackjack"),
        Msg("House: result", "msgHouseSeatWin", "🏆 {user} takes {amount} {points} on {value}. Balance: {balance} {points}.", "Seat wins", "Sent for each winning seat as the table is settled.", new[] { "user", "amount", "points", "value", "balance" }, alias: "MsgHouseSeatWin"),
        Msg("House: result", "msgHouseSeatPush", "🤝 {user} is level on {value} - {amount} {points} back.", "Seat pushes", "Sent for each seat level with the dealer, which gets its stake back.", new[] { "user", "value", "amount", "points" }, alias: "MsgHouseSeatPush"),
        Msg("House: result", "msgHouseSeatLoss", "❌ {user} drops {amount} {points}: {value} against {dealervalue}. Balance: {balance} {points}.", "Seat loses", "Sent for each losing seat as the table is settled.", new[] { "user", "amount", "points", "value", "dealervalue", "balance" }, alias: "MsgHouseSeatLoss"),
        Msg("House: result", "msgHouseTableSummary", "🏠 Dealer {dealer} ({dealervalue}) - {seats}", "Table summary", "Sent once at the end, and again on repeat: the dealer's hand and every seat's result.", new[] { "dealer", "dealervalue", "seats" }, alias: "MsgHouseTableSummary"),
        Msg("House: refusals", "msgHouseDisabled", "@{user}, House Mode is off.", "House Mode off", "Sent when the house command is used while House Mode is off.", new[] { "user" }, alias: "MsgHouseDisabled"),
        Msg("House: refusals", "msgHouseNeedsCurrency", "@{user}, House Mode is played for real {points} - turn Free Game off first.", "Needs real currency", "Sent when a table is opened while Free Game is on.", new[] { "user", "points" }, alias: "MsgHouseNeedsCurrency"),
        Msg("House: refusals", "msgHouseNoRuleSets", "@{user}, no rule set is on offer.", "No rule sets", "Sent when nothing is ticked on the House page, so there is nothing to open a table with.", new[] { "user" }, alias: "MsgHouseNoRuleSets"),
        Msg("House: refusals", "msgHouseAlreadyRunning", "@{user}, a table is already running.", "Table already running", "Sent when someone opens a second table while one is going.", new[] { "user" }, alias: "MsgHouseAlreadyRunning"),
        Msg("House: refusals", "msgHouseTableFull", "@{user}, the table is full.", "Table full", "Sent when every seat is taken.", new[] { "user" }, alias: "MsgHouseTableFull"),
        Msg("House: refusals", "msgHouseAlreadySeated", "@{user}, you already have a seat.", "Already seated", "Sent when a player takes a second seat.", new[] { "user" }, alias: "MsgHouseAlreadySeated"),
        Msg("House: refusals", "msgHouseNoTable", "@{user}, there's no table open right now.", "No table open", "Sent when a table command arrives with no table open.", new[] { "user" }, alias: "MsgHouseNoTable"),
        Msg("House: refusals", "msgHouseJoinClosed", "@{user}, the cards are already dealt.", "Join window closed", "Sent when someone joins after the cards are out.", new[] { "user" }, alias: "MsgHouseJoinClosed"),
        Msg("House: refusals", "msgHouseNotYourTurn", "@{user}, it's {opponent}'s turn.", "Not your turn", "Sent when a seat acts out of turn.", new[] { "user", "opponent" }, alias: "MsgHouseNotYourTurn"),
        Msg("House: refusals", "msgHouseNotPlaying", "@{user}, the table isn't playing hands right now.", "Not playing hands", "Sent when a play command arrives while the table is not playing - during the join window, say.", new[] { "user" }, alias: "MsgHouseNotPlaying"),
        Msg("House: refusals", "msgHouseNotSeated", "@{user}, you're not seated at this table.", "Not seated", "Written for someone who never took a seat trying to play the table's hand. The game currently answers that with 'No hand running' instead, so this line is not sent.", new[] { "user" }, alias: "MsgHouseNotSeated"),
        Msg("House: refusals", "msgHouseActionNotAvailable", "@{user}, {action} isn't allowed at a {rules} table.", "Not allowed here", "Sent when a command is not part of the table's rule set - surrender at a European table, for instance.", new[] { "user", "action", "rules" }, alias: "MsgHouseActionNotAvailable"),
        S("useBot", true, page: "messages", label: "Bot account", help: "Sends the game's messages from your Streamer.bot bot account instead of the broadcaster account.\n(Platforms > Any platform > Accounts > Bot)", aliases: new[] { "UseBot" }),
        S("sendAction", false, page: "messages", label: "Send as /me", help: "Twitch only: YouTube and Kick have no /me, so there the messages always go out as plain chat.", aliases: new[] { "SendAction" }),
        S("cooldownUseBot", true, page: "messages", label: "Bot account", help: "Sends the cooldown replies from your bot account, separately from the game's own messages.", aliases: new[] { "UseBotCD" }),
        S("cooldownSendAction", false, page: "messages", label: "Send as /me", help: "Twitch only, and separate from the game's messages.", aliases: new[] { "SendActionCD" }),
        S("numberSeparator", ".", page: "messages", label: "Numbers", help: "The thousands separator used in every amount the game posts.", options: new[] { ".|12.345", ",|12,345", " |12 345", "|12345" }, aliases: new[] { "NumberSeparator" }),
        S("globalCooldownMessage", "On cooldown for everyone - {0} seconds to go.", page: "messages", label: "Everyone", help: "Sent while the cooldown for everyone is still running. This one numbers its placeholder instead of naming it.", aliases: new[] { "GlobalCooldownMsg" }),
        S("userCooldownMessage", "{0}, your cooldown has {1} seconds left.", page: "messages", label: "Per player", help: "Sent while that viewer's own cooldown is still running. Numbered placeholders, and the order matters.", aliases: new[] { "UserCooldownMsg" }),
        // ---- (conventions) ----
        S("reasonInsufficientFunds", "Not enough {points} - you have {available} and need {required}.", label: "Not enough points", help: "Sent when the bet is bigger than the balance, typed out or from a shortcut.", aliases: new[] { "MsgNotEnoughPoints" }),
        // ---- messages ----
        S("leaderboardEmpty", "{user}, nothing on record yet - play a hand first.", page: "messages", label: "No stats yet", help: "Sent by the Stats action when a viewer checks their record before playing a hand.", aliases: new[] { "MsgNoStatsFound" }),
        // ---- stats ----
        S("leaderboardSize", 5, page: "stats", label: "Places", help: "How many players the allstats command lists.", section: "The leaderboard", options: new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", "10" }, aliases: new[] { "LeaderboardSize" }),
        S("leaderboardShowMedals", true, page: "stats", label: "Medals for the top three", help: "🥇 🥈 🥉 for the first three places instead of 1. 2. 3. Fourth place down always gets numbers.", section: "The leaderboard", aliases: new[] { "LeaderboardShowMedals" }),
        S("leaderboardExcludeBroadcaster", true, page: "stats", label: "Hide the broadcaster", help: "Keeps your own account off the board. Matched by user id, so renaming yourself does not put you back on it.\nThe stats are still tracked - only the listing leaves you out.", section: "Who shows up", aliases: new[] { "LeaderboardExcludeBroadcaster" }),
        S("leaderboardExcludeBot", true, page: "stats", label: "Hide the bot account", help: "Keeps your Streamer.bot bot account off the board.\n(Platforms > Any platform > Accounts > Bot)", section: "Who shows up", aliases: new[] { "LeaderboardExcludeBot" }),
        S("leaderboardUseBot", true, page: "stats", label: "Bot account", help: "Sends the leaderboard from your bot account instead of the broadcaster account.", section: "How the leaderboard is sent", aliases: new[] { "LeaderboardUseBot" }),
        S("leaderboardSendAction", false, page: "stats", label: "Send as /me", help: "Twitch only: YouTube and Kick have no /me.", section: "How the leaderboard is sent", aliases: new[] { "LeaderboardSendAction" }),
        S("leaderboardResetScope", "totals", page: "stats", label: "A reset clears", help: "The totals only: the money the ranking runs on - bets, winnings, net, duel points, biggest win and loss. Games played, blackjacks, busts, streaks and duel records stay. Right for a monthly season.", section: "Starting a new season", options: new[] { "totals|The leaderboard totals only", "all|Every stat there is" }, aliases: new[] { "LeaderboardResetScope" }),
        S("leaderboardAutoReset", "off", page: "stats", label: "Automatically", help: "Clears the leaderboard on its own when the period rolls over, using the scope above.", section: "Starting a new season", options: new[] { "off|Never", "weekly|Every new ISO week", "monthly|On the 1st of the month" }, aliases: new[] { "LeaderboardAutoReset" }),
        S("resetLeaderboard", null, page: "stats", label: "Reset the leaderboard now", help: "Applies the reset scope to every viewer on every platform, and asks first.", kind: "action", section: "Starting a new season", confirm: "This permanently clears the leaderboard for every viewer according to the reset scope above. It cannot be undone. Go ahead?"),
        // ---- log ----
        S("logToFile", true, page: "log", label: "Write a log file per day", help: "Writes every settled hand - win, loss, blackjack, bust, surrender, insurance, quit, timeout, and the duel and House results - to a dated 'BJG-log-yyyy-MM-dd.txt' in the Logs folder, one file per day.", kind: "bool", aliases: new[] { "LogToFile" }),
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
