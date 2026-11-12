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

    // ---- The window: title, pages, help, about, log. Null: the settings action carries a spec of its own. ----
    public static readonly UiWindow Window = new UiWindow("RhymeTime", releases: "aaskjer/RhymeTime", logPrefix: "[RhymeTime/Settings]")
        .Pages(P("game", "Game", "🎮"), P("battle", "Battle", "⚔️"), P("words", "Word banks", "📚"), P("access", "Access", "👥"), P("overlay", "Overlay", "📺"), P("messages", "Messages", "💬"), P("log", "Log", "📜"), P("help", "How to play", "📖"), P("about", "About", "ℹ️"))
        .Senders(useBot: "useBot", sendAction: "sendAction", cooldownUseBot: "cooldownUseBot", cooldownSendAction: "cooldownSendAction", numberSeparator: "numberSeparator", cooldownGroup: "Cooldown")
        .Cooldowns(C("15e49f3a-389a-4190-ac15-c50b1fd4eee6", "globalCooldown", "userCooldown"))   // !Rhyme, where the cooldowns live
        .Pill("freeGame", "Free game on", "Free game off", tooltip: "Free mode, from the Game page or !SetFreeRhyme. While it is on, nothing is staked and nothing is paid out; a default bet of 0 is free as well.")
        .Answer("overlaySample", "Sent. If nothing shows in OBS within a few seconds, check that Streamer.bot's WebSocket server is running on the port above.")
        .Pill("overlayEnabled", "Overlay on", "Overlay off", tooltip: "Whether rounds reach the OBS overlay, from the Overlay page.")
        .Log("Chat Games/Logs", "RT-log*.txt", writeLogKey: "logToFile", writeLogHelp: "Writes every settled round and battle (win, loss, timeout) to a dated 'RT-log-yyyy-MM-dd.txt' in Chat Games\\Logs, one file per day.",
            columns: new[] { "Player|User", "Platform|Platform", "Mode|Mode", "Word|Word", "Guess|Guess", "Difficulty|Difficulty", "Bet|Bet", "Change|Change", "After|After" },
            results: new[] { "WIN", "LOSS", "TIMEOUT", "BATTLE-WIN", "BATTLE-TIMEOUT" }, platforms: new[] { "twitch", "youtube", "kick" },
            emptyText: "No rounds logged yet. Every settled round and battle lands here once 'Write a log file per day' is on.")
        // German: the game's own keys and texts; sections, pages and the shared keys come from the conventions.
        .Lang("de",
        K("timeLimit", "Sekunden zum Antworten", "Wie lange ein Chatter Zeit hat, einen Reim zu finden, bevor die Runde verloren ist."),
        K("maxAttempts", "Versuche", "Wie viele falsche Reime ein Chatter anbieten darf, bevor die Runde verloren ist. Nur Tipps, die nach einem Versuch aussehen, zählen; alles andere im Chat wird ignoriert."),
        K("difficulty", "Schwierigkeit", "Aus welcher Wortliste eine Runde zieht. Zufall nimmt eine der drei, die Wörter enthält."),
        K("checkRealWords", "Tipps gegen ein Wörterbuch prüfen", "Ein Tipp, der nur richtig endet, wird bei wordotron.com nachgeschlagen, bevor er zählt. Tipps, die du selbst als Reime gelistet hast, zählen immer; ist das aus, gewinnt alles mit der richtigen Endung. Ist die Abfrage langsam oder nicht erreichbar, wird der Tipp angenommen, ein Ausfall des Wörterbuchs lehnt also nie einen richtigen Reim ab."),
        K("freeGame", "Freier Modus", "Jede Runde ist frei: nichts wird gesetzt und nichts ausgezahlt, egal welcher Einsatz. !SetFreeRhyme und !EndFreeRhyme schalten ihn aus dem Chat (Moderatoren). Ein Standardeinsatz von 0 ist ebenfalls frei."),
        K("minBet", "Mindesteinsatz", "Einsätze darunter werden abgelehnt. 0 schaltet die Untergrenze ab."),
        K("easyMultiplier", "Leicht ×", "Der Einsatz wird bei einem Gewinn aus der leichten Liste damit multipliziert."),
        K("mediumMultiplier", "Mittel ×", "Der Einsatz wird bei einem Gewinn aus der mittleren Liste damit multipliziert."),
        K("hardMultiplier", "Schwer ×", "Der Einsatz wird bei einem Gewinn aus der schweren Liste damit multipliziert."),
        K("userCooldown", "Pro Chatter", "Gilt für den Befehl !Rhyme je Chatter, in Sekunden. 0 schaltet sie ab. Speichern schreibt sie direkt auf den Befehl."),
        K("battleReward", "Battle-Belohnung", "Wird dem Gewinner gezahlt, wenn der Moderator ein Battle ohne Einsatz startet. 0 heißt, ein Battle ohne Einsatz zahlt nichts. Mit einem Standardeinsatz über 0 setzt der Moderator stattdessen diesen, und der Gewinner bekommt ihn. Mindest- und Höchsteinsatz von der Seite Spiel gelten auch für ein Battle mit Einsatz."),
        K("wordsEasy", "Leicht", "Ein Zielwort je Zeile mit seinen Reimen daneben, durch Kommas getrennt. Ein Ziel mit Leerzeichen gilt als Zeile zum Vervollständigen statt als Wort zum Reimen: dann muss die ganze Antwort zu einer der Lösungen passen."),
        K("wordsMedium", "Mittel", "Die Liste, aus der eine Runde bei mittlerer Schwierigkeit zieht. Eine Zeile erreicht das Spiel erst, wenn sie ein Ziel und mindestens einen Reim hat."),
        K("wordsHard", "Schwer", "Die Liste, aus der eine Runde bei schwerer Schwierigkeit zieht."),
        K("overlayEnabled", "Runden ans Overlay senden", "Eine Browserquelle, die das zu reimende Wort mit seinem Countdown zeigt, dann den Reim, der gewonnen hat, und was er gezahlt hat. Sie hört auf den eigenen WebSocket-Server von Streamer.bot. Aus, und das Spiel sendet ihr nichts."),
        K("obsSceneName", "Szene", "Die Szene, in die die Quelle kommt. Sie wird angelegt, wenn es sie noch nicht gibt."),
        K("obsSourceName", "Quelle", "Die Browserquelle, die in dieser Szene angelegt wird. Nichts wird angefasst, wenn OBS schon eine Quelle dieses Namens hat."),
        K("setupObsOverlay", "Overlay in OBS einrichten", "Legt die Szene an, wenn sie fehlt, und fügt eine Browserquelle in der Größe deiner Leinwand hinzu, die auf die Overlay-URL zeigt. OBS muss in Streamer.bot unter Stream Apps > OBS Studio verbunden sein. Erst speichern: die Einrichtung liest den gespeicherten Port und die Namen."),
        K("overlaySample", "Beispielrunde zeigen", "Spielt eine erfundene Runde auf jedem offenen Overlay, dem in OBS eingeschlossen, damit du siehst, wo es sitzt und wie es aussieht. Bist du live, sehen es auch deine Zuschauer. Nutzt die gespeicherten Einstellungen; solange eine echte Runde oder ein Battle auf dem Overlay läuft, wird nichts gesendet."),
        K("copyOverlayUrl", "Overlay-URL kopieren", "Schreibt die Overlay-Seite und kopiert ihre Adresse für eine Browserquelle in OBS: 'Lokale Datei' abwählen, URL einfügen, Größe 1920 × 1080. Die URL nimmt &platform=twitch nur für die Runden einer Plattform, &lang=de für deutsche Texte, &demo=1 zum Platzieren der Quelle und &debug=1, um zu sehen, ob sie Streamer.bot erreicht."),
        K("openOverlayDemo", "Demo ausprobieren", "Öffnet das Overlay im Browser und spielt Beispielrunden ohne Streamer.bot."),
        K("openOverlayFolder", "Overlay-Ordner öffnen", "Öffnet Chat Games\\\\RhymeTime\\\\Overlay, wo die Seite geschrieben wird."),
        K("overlayPosition", "Platz", "Auf welcher Seite der Leinwand die Karte sitzt. Ein position= in der URL des Overlays überstimmt das. Alles in diesem Abschnitt reist mit der Runde, in OBS muss also nichts neu eingerichtet werden."),
        K("overlayScale", "Größe", "Skaliert die ganze Karte. 100 ist volle Größe, 10 bis 300 sind die Grenzen."),
        K("overlayResultSeconds", "Ergebnis anzeigen", "Wie lange der gewinnende Reim, oder die Beispiele nach einer verlorenen Runde, auf dem Bildschirm bleibt."),
        K("overlayShowEnding", "Die Endung zum Reimen", "Die Plakette 'alles, was auf …EAM endet' unter dem Wort. Die Endung bleibt im Wort selbst so oder so hervorgehoben. Gezeigt wird eine Runde zur Zeit, die neueste: ein Battle nimmt einer noch laufenden Einzelrunde die Karte."),
        K("overlayShowAttempts", "Verbleibende Versuche", "Die Punktereihe zu 'Versuche' auf der Seite Spiel. Ein Battle zeigt sie nie; der ganze Chat rennt um die Wette."),
        K("overlayShowStake", "Der Einsatz", "Der Chip in der Ecke mit dem Einsatz oder dem Preis des Battles. In einem Freispiel so oder so aus."),
        K("overlayShowExamples", "Reime nach einer verlorenen Runde", "Bis zu fünf der Reime, die du gelistet hast, so wie die Nachricht Beispiele sie im Chat postet."),
        K("overlayGlow", "Der warme Schein hinter der Karte", "Ein weicher Schimmer in der Akzentfarbe, der mit der Karte zur Battle- oder Gewinnfarbe wechselt."),
        K("overlayAccentColour", "Akzent", "Die Endung des Worts, die Wortmarke und der Uhrbalken. Alles Hellere wird daraus abgeleitet."),
        K("overlayBattleColour", "Battle", "Rahmen und Chip der Battle-Karte, die verbrauchten Versuchspunkte und eine verlorene Runde."),
        K("overlayWinColour", "Gewonnen", "Der gewinnende Reim und was er gezahlt hat."),
        K("overlayCardColour", "Karte", "Die Karte hinter allem. Was diese Seite nicht erreicht, gehört in das eigene Custom CSS der Browserquelle, das gewinnt: :root { --rt-width: 480px; --rt-edge: 40px; --rt-radius: 8px; }"),
        K("overlayCardOpacity", "Deckkraft der Karte", "Wie massiv die Karte ist. 0 lässt nur den Text, 100 verdeckt den Stream dahinter."),
        K("startMessage", "Runde gestartet", "Eröffnet eine Runde und nennt das Wort, auf das gereimt wird."),
        K("winMessage", "Gewinn", "Beim ersten Tipp gesendet, der als Reim zählt."),
        K("loseMessage", "Zeit abgelaufen", "Gesendet, wenn die Uhr ohne gültigen Reim abläuft."),
        K("failedAttemptMessage", "Falscher Tipp", "Für jeden Tipp gesendet, der nach einem Versuch aussieht, aber nicht reimt, bis zur Versuchsgrenze."),
        K("maxAttemptsLoseMessage", "Versuche aufgebraucht", "Statt 'Zeit abgelaufen' gesendet, wenn die Versuchsgrenze die Runde beendet hat."),
        K("educationalMessage", "Beispiele nach einer Niederlage", "Folgt auf eine verlorene Runde mit bis zu fünf der Reime, die du gelistet hast. Leer lassen, um nichts zu posten."),
        K("alreadyPlayingMessage", "Schon in einer Runde", "Gesendet, wenn ein Chatter den Befehl ausführt, während die eigene Runde noch offen ist."),
        K("noWordsMessage", "Leere Wortliste", "Gesendet, wenn die gewählte Liste kein brauchbares Wort enthält, sodass keine Runde starten kann."),
        K("battleStartMessage", "Battle eröffnet", "Eröffnet ein Battle für den ganzen Chat."),
        K("battleWinMessage", "Battle gewonnen", "An den gesendet, der den ersten gültigen Reim geschafft hat."),
        K("battleTimeoutMessage", "Battle abgelaufen", "Gesendet, wenn niemand rechtzeitig gereimt hat. Ein Einsatz des Moderators geht zurück."),
        K("freeGameStartMessage", "Gestartet", "Einmal gesendet, wenn !SetFreeRhyme den freien Modus einschaltet, in den Chat, aus dem der Befehl kam. Anders eingeschaltet, etwa per Execute C# Method auf StartFreeGame in einer eigenen Action, geht sie an jede Plattform mit einem Konto in Streamer.bot - an YouTube nur, solange eine Übertragung beobachtet wird."),
        K("freeGameEndMessage", "Beendet", "Einmal gesendet, wenn !EndFreeRhyme den freien Modus ausschaltet, in den Chat, aus dem der Befehl kam. Anders ausgeschaltet, etwa per Execute C# Method auf EndFreeGame, geht sie an jede Plattform mit einem Konto in Streamer.bot - an YouTube nur, solange eine Übertragung beobachtet wird."),
        K("battleAlreadyActiveMessage", "Battle läuft schon", "Gesendet, wenn ein Moderator ein Battle eröffnet, während auf dieser Plattform noch eines läuft."),
        K("invalidBetMessage", "Einsatz abgelehnt", "Der Rahmen um die vier Gründe darunter; {reason} wird durch den zutreffenden ersetzt."),
        K("reasonNegativeAmount", "Grund: keine Zahl", "Füllt {reason}, wenn der Betrag nicht lesbar oder nicht über null ist."),
        K("reasonInsufficientFunds", "Grund: nicht genug Punkte", "Füllt {reason}, wenn der Einsatz mehr ist, als der Chatter hat."),
        K("reasonBelowMinimum", "Grund: unter dem Minimum", "Füllt {reason}, wenn der Einsatz unter dem Minimum auf der Seite Spiel liegt."),
        K("reasonAboveMaximum", "Grund: über dem Maximum", "Füllt {reason}, wenn der Einsatz über dem Maximum auf der Seite Spiel liegt."),
        K("globalCooldownMessage", "Abklingzeit für alle", "Gesendet, wenn der Befehl für den ganzen Chat noch in der Abklingzeit ist."),
        K("userCooldownMessage", "Abklingzeit eines Chatters", "Gesendet, wenn der Befehl für diesen einen Chatter noch in der Abklingzeit ist."),
        Tx("Overlay on", "Overlay an"),
        Tx("Overlay off", "Overlay aus"),
        Tx("Whether rounds reach the OBS overlay, from the Overlay page.", "Ob Runden das OBS-Overlay erreichen, von der Seite Overlay."),
        Tx("Easy", "Leicht"),
        Tx("Medium", "Mittel"),
        Tx("Hard", "Schwer"),
        Tx("Random", "Zufall"),
        Tx("Left", "Links"),
        Tx("Centre", "Mitte"),
        Tx("Right", "Rechts"),
        Tx("Free mode", "Freier Modus"),
        Tx("Free mode, from the Game page or !SetFreeRhyme. While it is on, nothing is staked and nothing is paid out; a default bet of 0 is free as well.", "Freier Modus, von der Seite Spiel oder !SetFreeRhyme. Solange er an ist, wird nichts gesetzt und nichts ausgezahlt; ein Standardeinsatz von 0 ist ebenfalls frei."),
        Tx("Writes every settled round and battle (win, loss, timeout) to a dated 'RT-log-yyyy-MM-dd.txt' in Chat Games\\Logs, one file per day.", "Schreibt jede abgerechnete Runde und jedes Battle (Gewinn, Niederlage, Zeitablauf) in eine datierte 'RT-log-yyyy-MM-dd.txt' in Chat Games\\Logs, eine Datei pro Tag."),
        Tx("No rounds logged yet. Every settled round and battle lands here once 'Write a log file per day' is on.", "Noch keine Runden protokolliert. Jede abgerechnete Runde und jedes Battle landet hier, sobald 'Logdatei pro Tag schreiben' an ist."),
        Tx("Player", "Spieler"), Tx("Mode", "Modus"), Tx("Word", "Wort"), Tx("Guess", "Tipp"), Tx("Difficulty", "Schwierigkeit"), Tx("Bet", "Einsatz"), Tx("Change", "Änderung"), Tx("After", "Danach"),
        Tx("Round", "Runde"))
        .Samples("word", "STREAM", "guess", "DREAM", "timeLimit", "30", "winAmount", "3,000", "attempts", "1", "maxAttempts", "3", "examples", "DREAM, TEAM, THEME, CREAM, BEAM", "difficulty", "medium", "reward", "500", "winner", "bob", "seconds", "12", "reason", "minimum bet is 10 Points.")
        .Help(
            H("A single-player round",
                Note("A chatter runs !Rhyme and gets one word. They answer in ordinary chat; only the last word of their message is read, so \"hmm maybe DREAM\" works. A rhyme you listed for that word always counts; anything else counts when it ends the same way as the listed rhymes and, with the dictionary check on, is a real word. Getting it right pays the stake times the bank's multiplier; running out of time or attempts costs the stake."),
                Cmd("!Rhyme", "starts a round at the default bet."),
                Cmd("!Rhyme 250", "starts one at 250, within the minimum and maximum."),
                Cmd("!Rhyme all", "also half, third, quarter and random."),
                Cmd("!SetFreeRhyme", "moderators: turns Free mode on, every round is free. !EndFreeRhyme turns it off.")),
            H("Lines to finish",
                Note("A target with spaces in it is a line to finish rather than a word to rhyme: \"ROSES ARE RED VIOLETS ARE BLUE ...\" expects the whole answer. There the chatter's entire message is read, punctuation ignored, and it has to match one of the answers you listed.")),
            H("A battle",
                Note("A moderator runs !RhymeBattle and the whole chat races for one word. The first valid rhyme takes the prize and the battle closes; nobody else pays anything. Each platform can have one battle going at a time."),
                Cmd("!RhymeBattle", "opens a battle. Moderators only, set on the command itself."),
                Cmd("!RhymeBattle 500", "opens one and stakes 500 of your own; the winner takes the stake."),
                Note("Permissions and cooldowns belong to the commands themselves, in Streamer.bot's Commands tab. The two cooldowns on the Game page are written onto !Rhyme when you save.")),
            H("For your own actions",
                Note("Every settled round fires a custom trigger - RT: Round Won, RT: Round Lost or RT: Round Timeout, and RT: Round Settled every time (Custom > Chat Game > RhymeTime) - with the round as arguments: roundOutcome, roundUser, roundUserId, roundPlatform, roundWord, roundGuess, roundDifficulty, roundBet, roundAmount, roundAttempts, roundPreviousPoints, roundNewPoints, roundCurrency and roundFree. GetLastRoundResult, run with an Execute C# Method sub-action on RhymeTime Game, hands the same arguments to any other action."),
                Note("A battle fires RT: Battle Won or RT: Battle Timeout, and RT: Battle Settled every time, with battleOutcome, battleWinner, battleWinnerId, battlePlatform, battleWord, battleGuess, battleDifficulty, battleStake, battleReward, battleStarter, battleStarterId, battlePreviousPoints, battleNewPoints and battleCurrency; GetLastBattleResult on RhymeTime Battle hands them over again."),
                Note("RT: Free Game Start and RT: Free Game End fire when Free mode is switched from chat or by an Execute C# Method on StartFreeGame or EndFreeGame of RhymeTime Game.")),
            H("Where things are kept",
                Cmd("RT-settings.json", "everything on these pages."),
                Cmd("RT-wordbank.json", "the three word banks."),
                Cmd("uiframework.layout.json", "your layout of this window: theme, renamed or hidden pages and fields."),
                Cmd("Overlay\\", "the OBS overlay page, written when you copy its URL or open the demo."),
                Cmd("Chat Games\\Logs\\RT-log-yyyy-MM-dd.txt", "every settled round and battle, one file per day, beside the other chat games' logs; the Log page reads it."),
                Note("All of it sits in Streamer.bot's folder under Chat Games\\RhymeTime. A damaged file falls back to the .bak beside it and the broken one is kept as .broken.")))
        .About(credits: "RhymeTime by aaskjer",
            sections: new[]
            {
                H("Good to know",
                    Note("Rhyme lookups use wordotron.com. Turn the dictionary check off on the Game page if you would rather the game never called out."))
            }, links: new[] { Link("Repository", "https://github.com/aaskjer/RhymeTime"), Link("Releases", "https://github.com/aaskjer/RhymeTime/releases"), Link("StreamUP - Currency Core", "https://streamup.tips/product/currency-core"), Link("tawmae - LOYALTY POINTS", "https://tawmae.xyz/loyalty-points") })
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
        S("timeLimit", 30, page: "game", label: "Seconds to answer", help: "How long a chatter has to find a rhyme before the round is lost.", kind: "duration", section: "The round", min: 5.0, max: 600.0, aliases: new[] { "TimeLimit" }),
        S("maxAttempts", 3, page: "game", label: "Attempts", help: "How many wrong rhymes a chatter may offer before the round is lost. Only guesses that look like an attempt are counted; anything else in chat is ignored.", section: "The round", min: 1.0, max: 10.0, options: new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", "10" }, aliases: new[] { "MaxAttempts" }),
        S("difficulty", "medium", page: "game", label: "Difficulty", help: "Which word bank a round draws from. Random picks one of the three that has words in it.", kind: "options", section: "The round", options: new[] { "easy|Easy", "medium|Medium", "hard|Hard", "random|Random" }, aliases: new[] { "Difficulty" }),
        S("checkRealWords", true, page: "game", label: "Check guesses against a dictionary", help: "A guess that merely ends the right way is checked against wordotron.com before it counts. Guesses you listed as rhymes yourself always count; with this off, anything with the right ending wins. If the lookup is slow or unreachable the guess is accepted, so a dictionary outage never rejects a correct rhyme.", section: "The round", aliases: new[] { "CheckRealWords" }),
        S("pointsVariable", "points", page: "game", help: "The Streamer.bot user variable the stake is taken from and the winnings are added to. Currency Core by StreamUP and LOYALTY POINTS by tawmae are found on their own and override this.", section: "Currency", aliases: new[] { "PointsVar" }),
        S("pointsName", "Points", page: "game", help: "What your currency is called in chat; it fills the {currency} placeholder. Overridden by Currency Core or LOYALTY POINTS when one of them is installed.", section: "Currency", aliases: new[] { "PointsName" }),
        S("defaultBet", 100, page: "game", help: "Staked when a chatter runs the command without naming an amount. 0 makes the whole game free: nothing is staked and nothing is paid out. Chatters may also type all, half, third, quarter or random.", section: "Bets", min: 0.0, aliases: new[] { "DefaultBet" }),
        S("freeGame", false, page: "game", label: "Free mode", help: "Every round is free: nothing is staked and nothing is paid out, whatever the bet. !SetFreeRhyme and !EndFreeRhyme switch it from chat (moderators). A default bet of 0 is free as well.", section: "Bets", aliases: new[] { "FreeGame" }),
        S("minBet", 10, page: "game", help: "Bets below this are refused. 0 turns the floor off.", section: "Bets", min: 0.0, aliases: new[] { "MinBet" }),
        S("maxBet", 10000, page: "game", help: "Bets above this are refused. 0 turns the ceiling off.", section: "Bets", min: 0.0, aliases: new[] { "MaxBet" }),
        S("easyMultiplier", 1.5, page: "game", label: "Easy ×", help: "The stake is multiplied by this on a win from the easy bank.", section: "Payout", min: 0.0, aliases: new[] { "EasyMultiplier" }),
        S("mediumMultiplier", 2.0, page: "game", label: "Medium ×", help: "The stake is multiplied by this on a win from the medium bank.", section: "Payout", min: 0.0, aliases: new[] { "MediumMultiplier" }),
        S("hardMultiplier", 3.0, page: "game", label: "Hard ×", help: "The stake is multiplied by this on a win from the hard bank.", section: "Payout", min: 0.0, aliases: new[] { "HardMultiplier" }),
        S("globalCooldown", 10, page: "game", label: "Everyone", help: "Applied to the !Rhyme command for the whole chat, in seconds. 0 turns it off. Saving writes it straight onto the command.", section: "Cooldowns", aliases: new[] { "CooldownGlobalSeconds" }),
        S("userCooldown", 30, page: "game", label: "Per chatter", help: "Applied to the !Rhyme command per chatter, in seconds. 0 turns it off. Saving writes it straight onto the command.", section: "Cooldowns", aliases: new[] { "CooldownUserSeconds" }),
        // ---- battle ----
        S("battleReward", 500, page: "battle", label: "Battle reward", help: "Paid to the winner when the moderator starts a battle without staking anything. 0 means a battle without a stake pays nothing. With a default bet above 0 the moderator stakes that instead, and the winner takes it. Minimum and maximum bet from the Game page apply to a staked battle too.", section: "The prize", min: 0.0, aliases: new[] { "BattleReward" }),
        // ---- words ----
        S("wordsEasy", JArray.Parse("[{\"Word\":\"CAT\",\"Rhymes\":\"BAT,HAT,MAT,RAT,SAT,FAT,PAT,FLAT,CHAT,THAT\"},{\"Word\":\"GAME\",\"Rhymes\":\"FAME,NAME,SAME,BLAME,CAME,FLAME,FRAME,SHAME,TAME,CLAIM\"},{\"Word\":\"LIGHT\",\"Rhymes\":\"NIGHT,SIGHT,FIGHT,RIGHT,MIGHT,BRIGHT,FLIGHT,TIGHT,HEIGHT,SLIGHT\"},{\"Word\":\"PLAY\",\"Rhymes\":\"DAY,SAY,WAY,MAY,STAY,PAY,LAY,GRAY,PRAY,DELAY\"},{\"Word\":\"LOVE\",\"Rhymes\":\"DOVE,ABOVE,GLOVE,SHOVE,THEREOF\"},{\"Word\":\"KING\",\"Rhymes\":\"RING,SING,BRING,THING,WING,STRING,SPRING,SWING,STING,CLING\"},{\"Word\":\"TREE\",\"Rhymes\":\"SEE,BEE,FREE,THREE,KEY,AGREE,DEGREE,FLEE,KNEE,TEA\"},{\"Word\":\"STAR\",\"Rhymes\":\"CAR,FAR,BAR,JAR,SCAR,GUITAR,CIGAR,AVATAR,BIZARRE\"},{\"Word\":\"STAR LIGHT STAR BRIGHT ...\",\"Rhymes\":\"FIRST STAR I SEE TONIGHT\"},{\"Word\":\"ROSES ARE RED VIOLETS ARE BLUE ...\",\"Rhymes\":\"SUGAR IS SWEET AND SO ARE YOU\"},{\"Word\":\"JACK AND JILL WENT UP THE HILL ...\",\"Rhymes\":\"TO FETCH A PAIL OF WATER\"},{\"Word\":\"TWINKLE TWINKLE LITTLE STAR ...\",\"Rhymes\":\"HOW I WONDER WHAT YOU ARE\"},{\"Word\":\"HUMPTY DUMPTY SAT ON A WALL ...\",\"Rhymes\":\"HUMPTY DUMPTY HAD A GREAT FALL\"},{\"Word\":\"WITH GREAT POWER ...\",\"Rhymes\":\"COMES GREAT RESPONSIBILITY\"}]"), page: "words", label: "Easy", help: "One target per row with its rhymes beside it, separated by commas. A target with spaces in it is treated as a line to finish instead of a word to rhyme: then the whole reply has to match one of the answers.", kind: "list", section: "Easy bank", columns: new[] { "Word|string|Word or line", "Rhymes|text|Rhymes / answers" }, file: "RT-wordbank.json", field: "Easy"),
        S("wordsMedium", JArray.Parse("[{\"Word\":\"STREAM\",\"Rhymes\":\"DREAM,TEAM,THEME,CREAM,BEAM,GLEAM,SCHEME,SUPREME,EXTREME,REGIME\"},{\"Word\":\"VICTORY\",\"Rhymes\":\"HISTORY,MYSTERY,MISERY,DELIVERY,DISCOVERY\"},{\"Word\":\"BATTLE\",\"Rhymes\":\"CATTLE,RATTLE,SEATTLE,PRATTLE,TATTLE\"},{\"Word\":\"GAMING\",\"Rhymes\":\"NAMING,CLAIMING,FRAMING,BLAMING,SHAMING,TAMING,AIMING,FLAMING\"},{\"Word\":\"POWER\",\"Rhymes\":\"HOUR,TOWER,FLOWER,SHOWER,DEVOUR,SOUR\"},{\"Word\":\"NOTIFICATION\",\"Rhymes\":\"VACATION,STATION,NATION,CREATION,EDUCATION,FOUNDATION,POPULATION,SENSATION\"},{\"Word\":\"CONTROLLER\",\"Rhymes\":\"ROLLER,STROLLER,BOWLER,PATROLLER\"},{\"Word\":\"CHAMPION\",\"Rhymes\":\"MANSION,FASHION,PASSION,EXPANSION,COMPASSION\"},{\"Word\":\"TO BE OR NOT TO BE ...\",\"Rhymes\":\"THAT IS THE QUESTION\"},{\"Word\":\"ALL THAT GLITTERS IS NOT GOLD ...\",\"Rhymes\":\"OFTEN HAVE YOU HEARD THAT TOLD\"},{\"Word\":\"PETER PIPER PICKED A PECK OF PICKLED PEPPERS ...\",\"Rhymes\":\"A PECK OF PICKLED PEPPERS PETER PIPER PICKED\"},{\"Word\":\"SHE SELLS SEASHELLS BY THE SEASHORE ...\",\"Rhymes\":\"THE SHELLS SHE SELLS ARE SURELY SEASHELLS\"},{\"Word\":\"HOW MUCH WOOD WOULD A WOODCHUCK CHUCK ...\",\"Rhymes\":\"IF A WOODCHUCK COULD CHUCK WOOD\"}]"), page: "words", label: "Medium", help: "The bank a round at medium difficulty draws from. A row only reaches the game once it has a target and at least one rhyme.", kind: "list", section: "Medium bank", columns: new[] { "Word|string|Word or line", "Rhymes|text|Rhymes / answers" }, file: "RT-wordbank.json", field: "Medium"),
        S("wordsHard", JArray.Parse("[{\"Word\":\"SUBSCRIBE\",\"Rhymes\":\"TRIBE,BRIBE,DESCRIBE,PRESCRIBE,VIBE\"},{\"Word\":\"ALGORITHM\",\"Rhymes\":\"RHYTHM,PRISM,SCHISM\"},{\"Word\":\"MOMENTUM\",\"Rhymes\":\"CONTINUUM,SPECTRUM,QUANTUM,DRUM,SUM\"},{\"Word\":\"PHENOMENON\",\"Rhymes\":\"MARATHON,PENTAGON,PARAGON,OCTAGON\"},{\"Word\":\"ORCHESTRA\",\"Rhymes\":\"EXTRA,SPECTRA,TUNDRA,MANTRA\"},{\"Word\":\"SILHOUETTE\",\"Rhymes\":\"ROULETTE,CASSETTE,BAGUETTE,REGRET,SUNSET\"},{\"Word\":\"ENTREPRENEUR\",\"Rhymes\":\"CONNOISSEUR,SABOTEUR,AMATEUR,PROVOCATEUR\"},{\"Word\":\"LABYRINTH\",\"Rhymes\":\"ZENITH,MONOLITH,MEGALITH\"},{\"Word\":\"A JOURNEY OF A THOUSAND MILES ...\",\"Rhymes\":\"BEGINS WITH A SINGLE STEP\"},{\"Word\":\"THE EARLY BIRD CATCHES THE WORM ...\",\"Rhymes\":\"BUT THE SECOND MOUSE GETS THE CHEESE\"},{\"Word\":\"ASHES TO ASHES ...\",\"Rhymes\":\"DUST TO DUST\"},{\"Word\":\"WHAT DOESNT KILL YOU ...\",\"Rhymes\":\"MAKES YOU STRONGER\"}]"), page: "words", label: "Hard", help: "The bank a round at hard difficulty draws from.", kind: "list", section: "Hard bank", columns: new[] { "Word|string|Word or line", "Rhymes|text|Rhymes / answers" }, file: "RT-wordbank.json", field: "Hard"),
        // ---- access ----
        S("blockedUsers", "", page: "access", help: "These names are ignored when they run the command, with no reply in chat.", section: "Who may not play", aliases: new[] { "BlockedUsers" }),
        S("blockedGroups", "", page: "access", help: "Everyone in these Streamer.bot groups is ignored when they run the command (Settings > Groups). A group that does not exist is skipped and logged.", section: "Who may not play", aliases: new[] { "BlockedGroups" }),
        // ---- log ----
        S("logToFile", true, page: "log", aliases: new[] { "LogToFile" }),
        // ---- overlay ----
        S("overlayEnabled", false, page: "overlay", label: "Send rounds to the overlay", help: "A browser source that shows the word being rhymed with its countdown, then the rhyme that won it and what it paid. It listens to Streamer.bot's own WebSocket server. Off, and the game sends it nothing.", section: "OBS overlay", aliases: new[] { "OverlayEnabled" }),
        S("overlayPort", 8080, page: "overlay", label: "Port", help: "The port of Streamer.bot's WebSocket server (Servers/Clients > WebSocket Server, tick Auto Start). 8080 unless you changed it there. With its authentication on, add &password= and the password to the end of the URL in OBS.", section: "Streamer.bot's WebSocket server", min: 1.0, max: 65535.0, aliases: new[] { "OverlayPort" }),
        S("obsConnectionId", 0, page: "overlay", label: "Connection #", help: "Which of Streamer.bot's OBS connections to use, if you have more than one. 0 is the first.", section: "Set it up in OBS", min: 0.0, aliases: new[] { "ObsConnectionId" }),
        S("obsSceneName", "RhymeTime", page: "overlay", label: "Scene", help: "The scene the source is added to. It is created if it is not there yet.", section: "Set it up in OBS", aliases: new[] { "ObsSceneName" }),
        S("obsSourceName", "RhymeTime Overlay", page: "overlay", label: "Source", help: "The browser source created in that scene. Nothing is touched if OBS already has a source of this name.", section: "Set it up in OBS", aliases: new[] { "ObsSourceName" }),
        S("setupObsOverlay", null, page: "overlay", label: "Set the overlay up in OBS", help: "Creates the scene if it is missing and adds a browser source the size of your canvas pointed at the overlay URL. OBS has to be connected in Streamer.bot under Stream Apps > OBS Studio. Save first: the setup reads the saved port and names.", kind: "action", section: "Set it up in OBS", confirm: "This creates the scene if it is missing and adds a browser source pointing at the overlay URL, over the OBS connection set above. Save your changes first if you edited the port or the names. Go ahead?", row: "obsButtons"),
        S("overlaySample", null, page: "overlay", label: "Show a sample round", help: "Plays one made-up round on every overlay that is open, the one in OBS included, so you can see where it sits and how it looks. If you are live, your viewers see it too. Uses the settings as saved; nothing is sent while a real round or battle is on the overlay.", kind: "action", section: "Set it up in OBS", row: "obsButtons"),
        S("copyOverlayUrl", null, page: "overlay", label: "Copy the overlay URL", help: "Writes the overlay page and copies its address for a Browser source in OBS: untick 'Local file', paste the URL, set the size to 1920 × 1080. The URL takes &platform=twitch for one platform's rounds only, &lang=de for German wording, &demo=1 to place the source, and &debug=1 to see whether it reached Streamer.bot.", kind: "action", section: "Or add it by hand", row: "byHand"),
        S("openOverlayDemo", null, page: "overlay", label: "Try the demo", help: "Opens the overlay in your browser, playing sample rounds without Streamer.bot.", kind: "action", section: "Or add it by hand", row: "byHand"),
        S("openOverlayFolder", null, page: "overlay", label: "Open the overlay folder", help: "Opens Chat Games\\RhymeTime\\Overlay, where the page is written.", kind: "action", section: "Or add it by hand", row: "byHand"),
        S("overlayPosition", "left", page: "overlay", label: "Place", help: "Which side of the canvas the card sits on. A position= in the overlay's URL overrules this. Everything in this section travels with the round, so nothing has to be set up in OBS again.", kind: "options", section: "Size and place", options: new[] { "left|Left", "center|Centre", "right|Right" }, aliases: new[] { "OverlayPosition" }),
        S("overlayScale", 100, page: "overlay", label: "Size", help: "Scales the whole card. 100 is full size, 10 to 300 are the limits.", section: "Size and place", min: 10.0, max: 300.0, aliases: new[] { "OverlayScale" }),
        S("overlayResultSeconds", 8, page: "overlay", label: "Result shown", help: "How long the winning rhyme, or the examples after a lost round, stays on screen.", section: "Size and place", min: 1.0, max: 120.0, aliases: new[] { "OverlayResultSeconds" }),
        S("overlayShowEnding", true, page: "overlay", label: "The ending to rhyme with", help: "The 'anything ending in …EAM' pill under the word. The ending stays picked out in the word itself either way. One round at a time is shown, the newest: a battle takes the card from a single-player round that is still running.", section: "What it shows", aliases: new[] { "OverlayShowEnding" }),
        S("overlayShowAttempts", true, page: "overlay", label: "Attempts left", help: "The row of dots following 'Attempts' on the Game page. A battle never shows them; the whole chat is racing.", section: "What it shows", aliases: new[] { "OverlayShowAttempts" }),
        S("overlayShowStake", true, page: "overlay", label: "The stake", help: "The chip in the corner with the bet, or the battle's prize. Off in a free game either way.", section: "What it shows", aliases: new[] { "OverlayShowStake" }),
        S("overlayShowExamples", true, page: "overlay", label: "Rhymes after a lost round", help: "Up to five of the rhymes you listed, the way the Examples message posts them in chat.", section: "What it shows", aliases: new[] { "OverlayShowExamples" }),
        S("overlayGlow", true, page: "overlay", label: "The warm glow behind the card", help: "A soft bloom in the accent colour, turning to the battle or won colour with the card.", section: "What it shows", aliases: new[] { "OverlayGlow" }),
        S("overlayAccentColour", "#F97316", page: "overlay", label: "Accent", help: "The word's ending, the wordmark and the clock bar. Everything lighter is worked out from it.", kind: "colour", section: "Colours", aliases: new[] { "OverlayAccentColor" }),
        S("overlayBattleColour", "#FB7185", page: "overlay", label: "Battle", help: "The battle card's border and chip, the used attempt dots and a lost round.", kind: "colour", section: "Colours", aliases: new[] { "OverlayBattleColor" }),
        S("overlayWinColour", "#86EFAC", page: "overlay", label: "Won", help: "The winning rhyme and what it paid.", kind: "colour", section: "Colours", aliases: new[] { "OverlayWinColor" }),
        S("overlayCardColour", "#160E10", page: "overlay", label: "Card", help: "The card behind it all. Anything this page does not reach belongs in the Browser source's own Custom CSS, which wins: :root { --rt-width: 480px; --rt-edge: 40px; --rt-radius: 8px; }", kind: "colour", section: "Colours", aliases: new[] { "OverlayCardColor" }),
        S("overlayCardOpacity", 78, page: "overlay", label: "Card opacity", help: "How solid the card is. 0 leaves only the text, 100 hides the stream behind it.", kind: "percent", section: "Colours", min: 0.0, max: 100.0, aliases: new[] { "OverlayCardOpacity" }),
        // ---- messages ----
        Msg("Round", "startMessage", "{user}, find a rhyme for: {word}! You have {timeLimit} seconds. Bet: {amount} {currency}", "Round started", "Opens a round and names the word to rhyme with.", new[] { "user", "word", "timeLimit", "amount", "currency" }, alias: "StartMessage"),
        Msg("Round", "winMessage", "{user} rhymed '{word}' with '{guess}'! +{winAmount} {currency} [Total: {currentPoints} {currency}]", "Win", "Sent on the first guess that counts as a rhyme.", new[] { "user", "word", "guess", "winAmount", "currency", "currentPoints" }, alias: "WinMessage"),
        Msg("Round", "loseMessage", "{user} couldn't find a rhyme for '{word}' in time! -{amount} {currency} [Total: {currentPoints} {currency}]", "Out of time", "Sent when the clock runs out with no valid rhyme.", new[] { "user", "word", "amount", "currency", "currentPoints" }, alias: "LoseMessage"),
        Msg("Round", "failedAttemptMessage", "@{user}, '{guess}' doesn't rhyme with '{word}'! {attempts}/{maxAttempts} attempts used.", "Wrong guess", "Sent for each guess that looks like an attempt but does not rhyme, up to the attempt limit.", new[] { "user", "guess", "word", "attempts", "maxAttempts" }, alias: "FailedAttemptMessage"),
        Msg("Round", "maxAttemptsLoseMessage", "{user} used all {maxAttempts} attempts on '{word}'! -{amount} {currency} [Total: {currentPoints} {currency}]", "Out of attempts", "Sent instead of 'Out of time' when the attempt limit is what ended the round.", new[] { "user", "word", "amount", "currency", "currentPoints", "maxAttempts" }, alias: "MaxAttemptsLoseMessage"),
        Msg("Round", "educationalMessage", "Examples: {examples}", "Examples after a loss", "Follows a lost round with up to five of the rhymes you listed. Empty it to post nothing.", new[] { "examples" }, alias: "EducationalMessage"),
        Msg("Round", "alreadyPlayingMessage", "{user}, you already have an active round!", "Already in a round", "Sent when a chatter runs the command while their own round is still open.", new[] { "user" }, alias: "AlreadyPlayingMessage"),
        Msg("Round", "noWordsMessage", "{user}, there are no words in the {difficulty} word bank yet.", "Empty word bank", "Sent when the chosen bank has no usable word in it, so no round can start.", new[] { "user", "difficulty" }, alias: "NoWordsMessage"),
        Msg("Battle", "battleStartMessage", "⚔️ BATTLE TIME! First to rhyme with '{word}' wins {reward} {currency}! You have {timeLimit}s!", "Battle opened", "Opens a battle for the whole chat.", new[] { "word", "reward", "currency", "timeLimit", "user" }, alias: "BattleStartMessage"),
        Msg("Battle", "battleWinMessage", "🏆 {winner} won the battle by rhyming '{word}' with '{guess}'! +{reward} {currency} (Total: {currentPoints} {currency})", "Battle won", "Sent to whoever got the first valid rhyme in.", new[] { "winner", "word", "guess", "reward", "currency", "currentPoints" }, alias: "BattleWinMessage"),
        Msg("Battle", "battleTimeoutMessage", "⏰ Nobody rhymed with '{word}' in time! Some valid rhymes: {examples}", "Battle expired", "Sent when nobody rhymed in time. Any stake the moderator put up goes back.", new[] { "word", "examples", "currency" }, alias: "BattleTimeoutMessage"),
        Msg("Battle", "battleAlreadyActiveMessage", "⚔️ A battle is already in progress! Type your rhyme in chat!", "Battle already running", "Sent when a moderator opens a battle while one is still going on that platform.", new string[0], alias: "BattleAlreadyActiveMessage"),
        Msg("Free mode", "freeGameStartMessage", "🎤 Free mode is ON! Rhyme for free - no {currency} needed!", "Started", "Sent once when Free mode is turned on by !SetFreeRhyme, in the chat the command came from. Turned on another way, say by an Execute C# Method on StartFreeGame in an action of your own, it goes to every platform with an account in Streamer.bot - to YouTube only while a broadcast is monitored.", new[] { "currency" }, alias: "FreeGameStartMessage"),
        Msg("Free mode", "freeGameEndMessage", "⏹️ Free mode has ended. Rounds stake {currency} again!", "Ended", "Sent once when Free mode is turned off by !EndFreeRhyme, in the chat the command came from. Turned off another way, say by an Execute C# Method on EndFreeGame, it goes to every platform with an account in Streamer.bot - to YouTube only while a broadcast is monitored.", new[] { "currency" }, alias: "FreeGameEndMessage"),
        Msg("Bets", "invalidBetMessage", "{user}, {reason}", "Bet refused", "The frame around the four reasons below; {reason} is replaced by whichever one applies.", new[] { "user", "reason", "currency" }, alias: "InvalidBetMessage"),
        Msg("Bets", "reasonNegativeAmount", "please specify a valid bet amount greater than 0.", "Reason: not a number", "Fills {reason} when the amount could not be read, or is not above zero.", new[] { "currency" }, alias: "ReasonNegativeAmount"),
        Msg("Bets", "reasonInsufficientFunds", "you don't have enough {currency}.", "Reason: cannot afford it", "Fills {reason} when the bet is more than the chatter has.", new[] { "currency" }, alias: "ReasonInsufficientFunds"),
        Msg("Bets", "reasonBelowMinimum", "minimum bet is {min} {currency}.", "Reason: under the minimum", "Fills {reason} when the bet is below the minimum on the Game page.", new[] { "min", "currency" }, alias: "ReasonBelowMinimum"),
        Msg("Bets", "reasonAboveMaximum", "maximum bet is {max} {currency}.", "Reason: over the maximum", "Fills {reason} when the bet is above the maximum on the Game page.", new[] { "max", "currency" }, alias: "ReasonAboveMaximum"),
        Msg("Cooldown", "globalCooldownMessage", "Please wait {seconds} seconds before trying again ⏳", "Everyone's cooldown", "Sent when the command is still on cooldown for the whole chat.", new[] { "seconds" }, alias: "GlobalMessage"),
        Msg("Cooldown", "userCooldownMessage", "@{user}, please wait {seconds} seconds before trying again ⏳", "A chatter's cooldown", "Sent when the command is still on cooldown for that one chatter.", new[] { "user", "seconds" }, alias: "UserMessage"),
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

    /// <summary>A ceiling that stores "no limit" as 0: read as long.MaxValue.</summary>
    public long Ceiling(string key) { long l = Long(key); return l <= 0 ? long.MaxValue : l; }
    public int CeilingInt(string key) { long l = Long(key); return l <= 0 || l > int.MaxValue ? int.MaxValue : (int)l; }
    public static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;
    public static long Clamp(long value, long min, long max) => value < min ? min : value > max ? max : value;
    public static double Clamp(double value, double min, double max) => value < min ? min : value > max ? max : value;
}
// --- end UIFramework host block ---
