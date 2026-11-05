// --- UIFramework host block (keep identical in every action of this host that reads the settings) ---
// From UIFramework/stub/HostSchema.cs (tools/update-host-block.ps1 refreshes it). The game declares its settings
// once in UiSchema.Table and its window in UiSchema.Window, reads the settings through UiSettings, and lets
// UiHost.Handle answer the settings window's requests. One table, so the game and its window can never disagree.
// Compiles in any Streamer.bot action with no reference beyond Newtonsoft.Json.
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
        // For a list: its columns as "key|kind|label" (kinds: string, text, int, double, bool, emote; an emote column
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
    }

    /// <summary>A setting: key and default, then whatever the conventions cannot know. Keys the conventions know
    /// (useBot, globalCooldown, blockedUsers, ...) need nothing but their default. An option is "value" or
    /// "value|label". aliasFactor multiplies the first alias's value on the move (a fraction to percent: 100).
    /// requires is a switch's key, or a condition on any field: "numberOfSlots>=5", "cpBetMode=fixed", "mode!=free".</summary>
    public static Setting S(string key, object @default, string page = null, string label = null, string help = null, string kind = null,
        string section = null, double? min = null, double? max = null, string unit = null, double? factor = null, bool unlimitedAtZero = false,
        string[] options = null, string requires = null, string confirm = null, bool hidden = false, string[] aliases = null, double? aliasFactor = null,
        string[] columns = null, string file = null, string field = null, bool transient = false, bool live = false, bool panel = false, bool auto = false,
        string platform = null, bool twitchOnly = false, string row = null)
    {
        return new Setting
        {
            Key = key, Default = @default, Page = page, Label = label, Help = help, Kind = kind, Section = section, Min = min, Max = max,
            Unit = unit, Factor = factor, UnlimitedAtZero = unlimitedAtZero, Options = options, Requires = requires, Confirm = confirm,
            Hidden = hidden, Aliases = aliases, AliasFactor = aliasFactor, Columns = columns, File = file, Field = field,
            Transient = transient, Live = live, Panel = panel, Auto = auto, Platform = platform, TwitchOnly = twitchOnly, Row = row
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
            if (s.Unit != null) item["unit"] = s.Unit;
            if (s.Platform != null) item["platform"] = s.Platform;
            if (s.Factor.HasValue) item["factor"] = s.Factor.Value;
            if (s.UnlimitedAtZero) item["unlimitedAtZero"] = true;
            if (s.Hidden) item["hidden"] = true;
            if (s.Requires != null) item["requires"] = s.Requires;
            if (s.Confirm != null) item["confirm"] = s.Confirm;
            if (s.Options != null)
            {
                var options = new JArray();
                foreach (var option in s.Options)
                {
                    int bar = option.IndexOf('|');
                    options.Add(bar < 0 ? (JToken)option : new JObject { ["value"] = option.Substring(0, bar), ["label"] = option.Substring(bar + 1) });
                }
                item["options"] = options;
            }
            if (s.Placeholders != null) item["placeholders"] = new JArray(s.Placeholders);
            if (s.Columns != null)
            {
                var columns = new JArray();
                foreach (var column in s.Columns)
                {
                    var parts = column.Split('|');
                    var cell = new JObject { ["key"] = parts[0], ["kind"] = parts.Length > 1 ? parts[1] : "string", ["label"] = parts.Length > 2 ? parts[2] : parts[0] };
                    if (parts.Length > 3) cell["platform"] = parts[3];
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
            if (s.Row != null) item["row"] = s.Row;
            if (s.Field != null) item["field"] = s.Field;
            if (s.Aliases != null) item["aliases"] = new JArray(s.Aliases);
            if (s.AliasFactor.HasValue && s.Aliases != null && s.Aliases.Length > 0) item["aliasFactors"] = new JObject { [s.Aliases[0]] = s.AliasFactor.Value };
            settings.Add(item);
        }
        var root = new JObject { ["host"] = host, ["version"] = version, ["settingsFile"] = settingsFile, ["settings"] = settings };
        if (Window != null) root["window"] = Window.ToJson();
        if (Window != null && Window.LangMap.Count > 0) root["lang"] = Window.LangMap;
        if (currency != null && currency.Detected) root["currency"] = currency.ToJson();
        return root.ToString(Formatting.None);
    }

    // ---- Shorthands for the window: a page, a help section and its lines, a cooldown. ----
    public static UiPage P(string id, string title = null, string icon = null, params string[] fields) => new UiPage(id, title, icon, fields);
    public static UiSection H(string heading, params object[] lines) => new UiSection(heading, lines);
    public static UiLine Cmd(string command, string what) => new UiLine(new JObject { ["command"] = command, ["what"] = what });
    public static UiLine Note(string note) => new UiLine(new JObject { ["note"] = note });
    public static UiLine Link(string text, string url) => new UiLine(new JObject { ["link"] = text, ["url"] = url });
    /// <summary>A command's cooldowns, set after every save: the command as its text ("!roll"), its name or its id in
    /// Streamer.bot, the setting of its global cooldown and the setting of its per-user cooldown (either may be null).</summary>
    public static string[] C(string command, string globalKey, string userKey) => new[] { command, globalKey, userKey };

    // ---- Shorthands for UiWindow.Lang: a key's texts, a page title, a declared text. ----
    public static UiText K(string key, string label, string help = null) => new UiText("keys", key, help == null ? (JToken)label : new JObject { ["label"] = label, ["help"] = help });
    public static UiText Pg(string id, string title) => new UiText("pages", id, title);
    public static UiText Tx(string declared, string translation) => new UiText("texts", declared, translation);

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
    public static readonly UiWindow Window = new UiWindow("Battleship Game", releases: "aaskjer/Battleship-Game", logo: "https://github.com/aaskjer/Battleship-Game/blob/main/Assets/logo_full.png?raw=true", logPrefix: "[Battleship Game/Settings]")
        .Pages(P("game", "Game", "🎮"), P("fleet", "Fleet", "🚢"), P("modes", "Modes", "⚔️"), P("overlay", "Overlay", "📺"), P("clicks", "Clicks", "🖱️"), P("access", "Access", "👥"), P("messages", "Messages", "💬"), P("stats", "Stats", "🏆"), P("log", "Log", "📜"), P("help", "How to play", "📖"), P("about", "About", "ℹ️"))
        .Senders(useBot: "useBot", sendAction: "sendAction", cooldownUseBot: "cooldownUseBot", cooldownSendAction: "cooldownSendAction", numberSeparator: "numberSeparator", cooldownGroup: "Cooldown")
        .Cooldowns(C("33042222-a6c0-439d-8a50-e0033bfe692e", "globalCooldown", "userCooldown"))   // !battleship
        // German: the game's own keys and texts; sections, pages and the shared keys come from the conventions.
        .Lang("de",
            Pg("fleet", "Flotte"),
            Pg("modes", "Modi"),
            Pg("clicks", "Klicks"),
            K("winBonus", "Siegbonus", "Wird obendrauf gezahlt, wenn ein Spieler die ganze gegnerische Flotte versenkt. 0 für keinen: ein Sieg zahlt ohnehin jedes versenkte Schiff. Die Schlacht des Chats wird zum Spaß gespielt und zahlt nichts."),
            K("minBalanceToPlay", "Mindestguthaben", "Was ein Spieler braucht, um eine Schlacht zu beginnen. 0 verlangt den Wert der eigenen Flotte, damit niemand eine Schlacht spielt, deren Verlust er nicht bezahlen könnte."),
            K("dailyWinCap", "Tagesgewinn-Obergrenze", "Das Meiste, was ein Spieler pro Tag gewinnen kann. 0 für keine Grenze. Ist sie erreicht, zahlen weitere Siege bis zum nächsten Tag nichts."),
            K("chargeDamagedOnAbandon", "Beschädigte Schiffe berechnen, wenn die Zeit abläuft", "Ohne das spart, wer einen Treffer vor dem Untergang eines Trägers aufhört, dessen ganzen Wert. Ein Träger mit 4 von 5 Treffern kostet 4/5 davon. Gilt nie im normalen Spiel."),
            K("clampBalanceAtZero", "Guthaben nie unter null fallen lassen", "Ein Spieler, der einen Verlust nicht decken kann, zahlt nur, was er hat."),
            K("economyNote", null, "Nichts wird vorab gesetzt: ein versenktes Schiff des Admirals zahlt sofort seinen Wert, ein eigenes verlorenes kostet seinen Wert. Die Schiffswerte stehen auf der Seite Flotte."),
            K("admiralDifficulty", "Können", "Fähnrich feuert zufällig und wiederholt nie ein Feld, etwa 59 Schüsse für ein Quick-Spielfeld. Kapitän feuert zufällig, bis er trifft, arbeitet dann die Felder ringsum ab und folgt einer Trefferlinie bis an beide Enden, etwa 36 Schüsse. Admiral jagt wie der Kapitän und prüft zwischen Treffern nur Felder im Abstand des kürzesten noch schwimmenden Schiffs, sodass sich nichts dazwischen verstecken kann, etwa 30 Schüsse."),
            K("admiralBlunderChance", "Patzer-Chance", "Wie oft der Admiral seinen Plan über Bord wirft und blind feuert, von 0 (nie) bis 100 (immer). Ein Handicap, das auf jeder Stufe wirkt, ohne ihn schummeln zu lassen."),
            K("admiralDelayMs", "Verzögerung der Erwiderung", "Pause zwischen einem Schuss und der Antwort des Admirals, in Millisekunden, damit das Overlay einen Schlagabtausch zeigt statt eines Aufblitzens. 0 bis 5000."),
            K("admiralNote", null, "Der Admiral sieht dein Spielfeld nie. Er weiß nur, wohin er gefeuert hat, was er dort fand und welche Schiffslängen noch schwimmen - dasselbe, was du über seine Flotte weißt."),
            K("deployTimeoutSec", "Bis zum ersten Schuss", "Vom Start der Schlacht bis zum ersten Schuss. Noch ist nichts gesunken, also kostet ein Ablauf hier nichts. Mindestens 5."),
            K("turnWarnFirstSec", "Erste Warnung", "Restsekunden des Zuges, bei denen der Chat gewarnt wird und der Countdown-Ring gelb wird. 0 schaltet sie ab."),
            K("turnWarnSecondSec", "Letzte Warnung", "Restsekunden des Zuges, bei denen der Chat noch einmal gewarnt wird und der Ring rot pulsiert. 0 schaltet sie ab."),
            K("clockSummary", "Die Uhr in Kürze", "Was die vier Werte darüber ergeben, so wie sie stehen."),
            K("presetBlitz", "Blitz · 6×6 · 3 Schiffe", "Ein 6×6-Spielfeld mit einem Träger der Länge 4, einem Kreuzer der Länge 3 und einem Zerstörer der Länge 2. Füllt Spielfeld und Schiffe aus; gespeichert wird erst, wenn du speicherst."),
            K("presetQuick", "Quick · 8×8 · 3 Schiffe", "Ein 8×8-Spielfeld mit einem Träger der Länge 5, einem Kreuzer der Länge 3 und einem Zerstörer der Länge 2: der Standard, etwa 36 Schüsse pro Schlacht."),
            K("presetClassic", "Klassisch · 10×10 · 5 Schiffe", "Das klassische 10×10-Spielfeld mit allen fünf Schiffen."),
            K("presetNote", null, "Eine Vorlage füllt die Spielfeldgröße und die segelnden Schiffe mit ihren Längen aus. Danach kannst du alles nach Belieben ändern."),
            K("gridSize", "Spielfeldgröße", "Felder je Seite, von 5 (A1 bis E5) bis 12 (A1 bis L12). Größere Spielfelder machen längere Schlachten."),
            K("noTouchingRule", "Schiffe dürfen sich nicht berühren", "Hält um jedes Schiff ein Feld Abstand, damit zwei Schiffe nie wie ein langes aussehen und die Jagd fairer ist."),
            K("shipCarrierEnabled", "Träger", "Mit einem Träger segeln. Beide Flotten haben immer dieselben Schiffe."),
            K("shipCarrierLength", "Länge des Trägers", "Wie viele Felder der Träger belegt."),
            K("shipCarrierValue", "Wert des Trägers", "Was das Versenken des Trägers zahlt und was sein Verlust kostet."),
            K("shipBattleshipEnabled", "Schlachtschiff", "Mit einem Schlachtschiff segeln."),
            K("shipBattleshipLength", "Länge des Schlachtschiffs", "Wie viele Felder das Schlachtschiff belegt."),
            K("shipBattleshipValue", "Wert des Schlachtschiffs", "Was das Versenken des Schlachtschiffs zahlt und was sein Verlust kostet."),
            K("shipCruiserEnabled", "Kreuzer", "Mit einem Kreuzer segeln."),
            K("shipCruiserLength", "Länge des Kreuzers", "Wie viele Felder der Kreuzer belegt."),
            K("shipCruiserValue", "Wert des Kreuzers", "Was das Versenken des Kreuzers zahlt und was sein Verlust kostet."),
            K("shipSubmarineEnabled", "U-Boot", "Mit einem U-Boot segeln."),
            K("shipSubmarineLength", "Länge des U-Boots", "Wie viele Felder das U-Boot belegt."),
            K("shipSubmarineValue", "Wert des U-Boots", "Was das Versenken des U-Boots zahlt und was sein Verlust kostet."),
            K("shipDestroyerEnabled", "Zerstörer", "Mit einem Zerstörer segeln."),
            K("shipDestroyerLength", "Länge des Zerstörers", "Wie viele Felder der Zerstörer belegt."),
            K("shipDestroyerValue", "Wert des Zerstörers", "Was das Versenken des Zerstörers zahlt und was sein Verlust kostet."),
            K("fleetSummary", "Die Flotte in Kürze", "Das Spielfeld, die segelnden Schiffe und etwa wie viele Schüsse eine Schlacht braucht, so wie die Werte stehen."),
            K("fleetNote", null, "Beide Seiten segeln immer mit derselben Flotte. Der Wert eines Schiffs ist, was sein Versenken zahlt und was sein Verlust kostet, abgerechnet in dem Moment, in dem es sinkt."),
            K("soloEnabled", "Solo: ein Zuschauer gegen den Admiral", "Gestartet mit !battleship. Jeder Spieler hat seine eigene Schlacht und rechnet jedes Versenken sofort ab."),
            K("chatBattleEnabled", "Chat gegen den Admiral: der ganze Chat stimmt über jeden Schuss ab", "Gestartet mit !seeschlacht. Jeder kann mitmachen, per Klick auf den Stream oder mit !fire. Zum Spaß gespielt: keine Punkte."),
            K("chatNote", null, "Der ganze Chat ist eine Flotte, zum Spaß gespielt: niemand zahlt, niemand gewinnt Punkte. In jeder Runde hat jeder Zuschauer eine Stimme, ein Klick auf gegnerische Gewässer oder !fire B7, und am Ende der Runde wird auf das Feld mit den meisten Stimmen gefeuert."),
            K("chatTeamName", "Teamname", "Wie die Flotte des Chats auf dem Overlay und im Chat heißt, als {team}."),
            K("chatRoundSec", "Sekunden je Runde", "Länger als deine Stream-Verzögerung, sonst stimmen Zuschauer noch über ein Bild von vor dem letzten Schuss ab. Mindestens 5."),
            K("chatNote2", null, "Jeder Klick eines Zuschauers zählt, bei Twitch angemeldet oder nicht - nichts muss verknüpft werden. Wer abgemeldet ist, kann durch Neuladen der Seite noch einmal abstimmen, was in einer Schlacht zum Spaß nicht schadet. Eine Chat-Schlacht beginnt nur, wenn keine andere läuft, und stimmt so lange niemand ab, wie die Zuguhr erlaubt, bricht der Chat sie ab."),
            K("duelNote", null, "Ein Zuschauer fordert einen anderen mit Namen heraus, oder ohne Namen jeden, und führt Team A; wer annimmt, führt Team B. Alle anderen wählen dann eine Seite, und jedes Mitglied zahlt den Einsatz. Die Seiten sind abwechselnd dran, je eine Abstimmungsrunde, und die Seite, die die Flotte der anderen versenkt, teilt sich den Topf. Jeder Gewinner bekommt seinen eigenen Einsatz ganz zurück; nur der Gewinn obendrauf unterliegt dem Abzug für Süchtige und der Tagesgrenze."),
            K("duelEntryFee", "Einsatz", "Was jedes Mitglied zahlt, um einer Seite beizutreten. 0 für ein Duell nur um die Bilanz. Mit Freispiel an zahlt niemand."),
            K("duelAcceptSec", "Sekunden zum Annehmen", "Eine Herausforderung, die niemand rechtzeitig annimmt, verfällt, und der Einsatz geht zurück. Mindestens 10."),
            K("duelJoinSec", "Sekunden zum Beitreten", "Ab dem Moment, in dem die Herausforderung angenommen ist. Mindestens 10."),
            K("duelRoundSec", "Sekunden je Runde", "Eine Runde ist der Schuss einer Seite. Länger als deine Stream-Verzögerung, wie bei der Schlacht des Chats. Mindestens 5."),
            K("duelMaxTeamGap", "Vorsprungsgrenze (0 = keine)", "Wie viele Spieler mehr eine Seite haben darf als die andere. Mit 1 ist eine Seite nie mehr als einen Spieler voraus. 0 lässt jeden jeder Seite beitreten."),
            K("duelMaxMissedTurns", "Züge, die eine Seite verstreichen lassen darf", "Eine Seite, die so viele Züge hintereinander ohne Stimme verstreichen lässt, gibt auf, und die andere gewinnt. Hat überhaupt niemand gefeuert, wird das Duell abgesagt und jeder Einsatz erstattet. Mindestens 1."),
            K("duelNote2", null, "Ein Duell kann nur herausgefordert werden, solange nichts anderes läuft, und bis zu seinem letzten Schuss beginnt nichts anderes. Anonyme Heat-Klicks zählen im Duell nie, Mitglieder stimmen also mit !fire ab oder verknüpfen ihre Klicks einmal mit !fire link."),
            K("stopNote", null, "Der Stopp-Befehl beendet, was gerade läuft - Solo-Schlachten, die Schlacht des Chats oder ein Duell - und zahlt nichts mehr aus: keinen Siegbonus, keinen Topf, nichts für beschädigte Schiffe. Die Einsätze eines Duells werden erstattet. Wer ihn nutzen darf, wird am Befehl in Streamer.bot eingestellt."),
            K("overlayNote", null, "Das Overlay ist battleship_overlay.html im Ordner unten. Das Spiel schreibt es dorthin und hält es aktuell, deine vorherige Kopie bleibt als .bak erhalten. Sein Hintergrund ist transparent, es liegt also über jeder Szene."),
            K("obsNote", null, "Erstellt die Szene und darin eine Browserquelle in der Größe deiner Leinwand, die auf die Overlay-Datei zeigt. OBS muss in Streamer.bot unter Stream Apps > OBS Studio verbunden sein."),
            K("overlayAdmiralName", "Name des Gegners", "Wie die CPU auf dem Overlay und im Chat heißt, als {admiral}."),
            K("overlayShowPlayerRoster", "Die Flottenliste des Spielers", "Die Schiffsliste mit Schadenspunkten unter dem eigenen Spielfeld des Spielers."),
            K("overlayShowEnemyRoster", "Die Flottenliste des Gegners", "Dieselbe Liste unter den gegnerischen Gewässern. Ihre Punkte zeigen, welche seiner Schiffe deine Treffer getroffen haben; ausschalten, um das zu verbergen. Welche Liste auch verborgen ist, die zwei Spielfelder bleiben auf gleicher Höhe."),
            K("overlayShowCoords", "Koordinaten", "Die Buchstaben und Zahlen entlang der Spielfelder."),
            K("overlayShowTurnTimer", "Countdown-Ring des Zuges", "Wird bei der ersten Warnung gelb und pulsiert bei der letzten rot."),
            K("overlayShowScore", "Laufender Stand und Trefferquote", "Schüsse, Treffer und die laufende Bilanz der Schlacht."),
            K("overlayHitColour", "Treffer", "Ein Feld, auf dem ein Schuss ein Schiff traf."),
            K("overlayMissColour", "Fehlschuss", "Ein Feld, auf dem ein Schuss nur Wasser fand."),
            K("overlaySunkColour", "Versenkt", "Ein Schiff, das untergegangen ist."),
            K("overlayShipColour", "Schiffsrumpf", "Deine eigenen Schiffe auf dem linken Spielfeld."),
            K("overlayGridColour", "Gitterlinien", "Die Linien zwischen den Feldern."),
            K("overlayWaterColour", "Wasser", "Die Felder, auf die niemand gefeuert hat."),
            K("coloursNote", null, "Deine eigene Flotte wird ganz gezeichnet, die Gewässer des Admirals bleiben vernebelt, bis etwas getroffen wird. Der Admiral liest weder Chat noch Overlay, deine Schiffe zu zeigen verrät also nichts."),
            K("clicksNote", null, "Heat ist eine kostenlose Twitch-Erweiterung, die die Klicks der Zuschauer auf dein Video an das Overlay weitergibt. Ist das an, kann der Spieler, dessen Schlacht auf dem Bildschirm ist, per Klick auf ein Feld in gegnerischen Gewässern feuern, und !fire funktioniert weiter wie bisher."),
            K("heatEnabled", "Per Klick auf den Stream feuern", "Nur Twitch: Heat ist eine Twitch-Erweiterung. Braucht Heat auf deinem Kanal und einen laufenden WebSocket-Server von Streamer.bot, siehe Einrichtung unten."),
            K("heatAutoLink", "Der erste Klick übernimmt", "Der erste Klick in der Schlacht eines Spielers übernimmt die Kontrolle: Heat sagt nicht, wer geklickt hat, also gilt der erste Zuschauer, der in der Schlacht eines Spielers auf gegnerische Gewässer klickt, für diese Schlacht als dieser Spieler. Es hält sich zurück, wenn in den letzten 20 Sekunden ein anderer Zuschauer dort geklickt hat oder wenn der Klicker mit einem anderen Spieler verknüpft ist. Ein Spieler, dessen Schlacht übernommen wurde, klickt einmal und tippt !fire link, was auch eine dauerhafte Verknüpfung anlegt. Aus: jeder Spieler verknüpft sich mit !fire link."),
            K("setupNote", null, "1. Installiere Heat auf deinem Kanal und aktiviere es als Overlay-Erweiterung: https://dashboard.twitch.tv/extensions/cr20njfkgll4okyrhag7xxph270sqk\n2. Starte den WebSocket-Server von Streamer.bot: Servers/Clients > WebSocket Server, Auto Start anhaken, dann Start Server. Das Overlay reicht jeden Klick darüber an die Action Battleship Game weiter.\n3. Spieler klicken einfach. Kam der Klick eines anderen zuerst, oder ist der Schalter oben aus, klickt der Spieler einmal und tippt !fire link; die Verknüpfung bleibt für spätere Schlachten erhalten.\nHeat läuft nur in Desktop-Browsern: Twitch zeigt in seiner Handy-App keine Overlay-Erweiterungen, Zuschauer am Telefon nutzen also weiter !fire."),
            K("heatChannelId", "Twitch-Kanal-ID", "Die Nummer, unter der Heat die Klicks deiner Zuschauer ablegt: die Benutzer-ID deines Kanals, nicht sein Name. Leer lassen, um das in Streamer.bot verbundene Broadcaster-Konto zu nehmen."),
            K("heatStreamerBotUrl", "Streamer.bot-WebSocket", "Wo das Overlay Streamer.bot erreicht: Adresse und Port unter Servers/Clients > WebSocket Server. Meist ws://127.0.0.1:8080/"),
            K("broadcasterFound", "Das Broadcaster-Konto", "Das mit Streamer.bot verbundene Twitch-Konto, genutzt, wann immer das Feld Kanal-ID leer ist."),
            K("heatStreamerBotPassword", "WebSocket-Passwort", "Nur, wenn du die Authentifizierung für den WebSocket-Server eingeschaltet hast. Es wird im Klartext neben dem Overlay gespeichert, weil das Overlay es lesen muss."),
            K("alignNote", null, "Heat meldet jeden Klick als Punkt auf dem ganzen Streambild. Das passt von selbst, wenn die Browserquelle des Overlays die ganze Leinwand bedeckt - so legt die OBS-Einrichtung sie an. Hast du die Quelle verschoben oder verkleinert, sag, wo sie liegt, in Prozent der Leinwand: eine auf halbe Größe geschrumpfte Quelle in der rechten unteren Ecke ist 50 / 50 / 50 / 50."),
            K("heatAreaLeft", "Links", "Wie weit vom linken Rand der Leinwand die Quelle beginnt."),
            K("heatAreaTop", "Oben", "Wie weit vom oberen Rand der Leinwand die Quelle beginnt."),
            K("heatAreaWidth", "Breite", "Wie viel der Leinwandbreite die Quelle bedeckt. 100 ist alles."),
            K("heatAreaHeight", "Höhe", "Wie viel der Leinwandhöhe die Quelle bedeckt. 100 ist alles."),
            K("heatShowClicks", "Jeden Klick auf dem Overlay zeigen (Ausrichtungstest)", "Zeichnet eine Markierung, wo immer jemand klickt, mit dem Verbindungsstatus oben links. Zuschauer sehen sie auch, also ausschalten, sobald die Markierungen dort landen, wo du klickst."),
            K("alignNote2", null, "Zum Prüfen den Test einschalten und speichern, dann, während du live bist, deinen Kanal im Browser öffnen und im Video herumklicken. Jede Markierung sollte unter dem Zeiger landen. Die eigenen Klicks des Spielers erscheinen in Gold, benannt nach dem Feld, auf das sie feuern."),
            K("markersNote", null, "Optional. Heats eigenes Markers-Overlay setzt eine Markierung dorthin, wo jeder Zuschauer zuletzt geklickt hat, damit alle die Klicks landen sehen. Der Button fügt es der auf der Seite Overlay genannten Szene hinzu, in der Größe deiner Leinwand, über dieselbe OBS-Verbindung."),
            K("heatObsSourceName", "Marker-Quelle", "Die Browserquelle, die für Heats Marker angelegt wird. Hat OBS schon eine Quelle dieses Namens, wird nichts angerührt."),
            K("addHeatMarkers", "Zu OBS hinzufügen", "Fügt der Szene des Overlays eine Browserquelle hinzu, die https://heat.j38.net/markers/?channel=<deine Kanal-ID> zeigt. Erst speichern: es liest die gespeicherte Kanal-ID und die Namen."),
            K("markersNote2", null, "Was der Chat über das Verknüpfen erfährt, steht auf der Seite Nachrichten unter Klicken zum Feuern."),
            K("platformsNote", null, "Twitch, YouTube und Kick spielen alle mit, und jede Antwort geht in den Chat, aus dem der Befehl kam. Um eine Plattform auszulassen, hake sie an den Befehlen des Spiels in Streamer.bot ab, zum Beispiel 'Kick Message'. Klicken zum Feuern und das Flüstern der Flotte gehen nur auf Twitch."),
            K("whisperOwnFleet", "Jedem Spieler seine Flotte flüstern", "Nur Twitch: Streamer.bot kann auf YouTube oder Kick nicht flüstern. Twitch verwirft Flüsternachrichten von Konten ohne bestätigte Telefonnummer; ein Fehlschlag wird protokolliert, und die Schlacht geht weiter. Das Overlay zeigt dem Spieler seine Flotte ohnehin, das hier ist eine private zweite Kopie."),
            K("cooldownNote", null, "Was der Chat erfährt, solange eine Abklingzeit läuft, steht auf der Seite Nachrichten unter Abklingzeit."),
            K("leaderboardRankBy", "Spieler sortieren nach", "Wonach !ballstats sortiert. Netto ist das faire Maß, da verlorene Schiffe Punkte kosten."),
            K("statsNote", null, "Jeder erfasste Wert liegt in einer dauerhaften bsg_*-Benutzervariable, sodass jede andere Streamer.bot-Action die Bilanz eines Spielers lesen kann."),
            K("sentNote", null, "Die Antworten auf !bstats und !ballstats stehen auf der Seite Nachrichten unter Statistik und gehen wie die Nachrichten des Spiels hinaus."),
            K("msgGameStart", "Schlacht begonnen", "Gesendet, wenn ein Spieler mit !battleship eine Schlacht beginnt."),
            K("msgFleetRerolled", "Flotte neu aufgestellt", "Bestätigt !fleet, das die Schiffe vor dem ersten Schuss neu aufstellt."),
            K("msgPlayerMiss", "Fehlschuss", "Der Schuss des Spielers fand nur Wasser."),
            K("msgPlayerHit", "Treffer", "Der Schuss des Spielers traf ein Schiff."),
            K("msgPlayerSunk", "Gegnerisches Schiff versenkt", "Der Spieler hat eines der Schiffe des Admirals versenkt. Die Punkte sind gezahlt, bevor das gesendet wird."),
            K("msgPlayerSunkFree", "Gegnerisches Schiff versenkt (Freispiel)", "Stattdessen genutzt, solange das Freispiel an ist, damit nichts Punkte erwähnt."),
            K("msgBoardStatus", "Stand des Spielfelds", "Gesendet von !board während einer Solo-Schlacht."),
            K("msgAdmiralMiss", "Fehlschuss", "Der Schuss des Admirals fand nur Wasser."),
            K("msgAdmiralHit", "Treffer", "Der Admiral hat eines der Schiffe des Spielers getroffen."),
            K("msgAdmiralSunk", "Dein Schiff versenkt", "Der Admiral hat eines der Schiffe des Spielers versenkt. Die Punkte sind abgezogen, bevor das gesendet wird."),
            K("msgAdmiralSunkFree", "Dein Schiff versenkt (Freispiel)", "Stattdessen genutzt, solange das Freispiel an ist."),
            K("msgWin", "Sieg", "{net} ist die Bilanz der Schlacht, alles Versenkte minus alles Verlorene, mit Vorzeichen."),
            K("msgWinFree", "Sieg (Freispiel)", "Stattdessen genutzt, solange das Freispiel an ist."),
            K("msgLoss", "Niederlage", "{net} ist die Bilanz der Schlacht, mit Vorzeichen."),
            K("msgLossFree", "Niederlage (Freispiel)", "Stattdessen genutzt, solange das Freispiel an ist."),
            K("msgTurnTimeout", "Zeit abgelaufen", "Die Zuguhr lief mitten in der Schlacht ab. Was gesunken war, wird abgerechnet."),
            K("msgTurnTimeoutFree", "Zeit abgelaufen (Freispiel)", "Stattdessen genutzt, solange das Freispiel an ist."),
            K("msgDeployTimeout", "Nie das Feuer eröffnet", "Nichts war gesunken, also wird nichts berechnet, und es zählt nicht als Schlacht."),
            K("msgTurnWarnFirst", "Erste Warnung", "Gesendet, wenn die Zuguhr die erste Warnung erreicht. Der Countdown-Ring wird gelb."),
            K("msgTurnWarnSecond", "Letzte Warnung", "Gesendet, wenn die Zuguhr die letzte Warnung erreicht. Der Ring pulsiert rot."),
            K("msgChatStarted", "Schlacht begonnen", "Gesendet, wenn !seeschlacht die Schlacht des Chats beginnt. Fehlschüsse und Treffer nutzen die Solo-Nachrichten, mit dem Teamnamen als {user}."),
            K("msgChatSinks", "Chat versenkt ein Schiff", "Zum Spaß gespielt: nichts wird gezahlt."),
            K("msgChatLosesShip", "Chat verliert ein Schiff", "Zum Spaß gespielt: nichts wird berechnet."),
            K("msgChatWins", "Chat gewinnt", "{voters} ist, wie viele Zuschauer über die Schlacht abgestimmt haben."),
            K("msgChatLoses", "Chat verliert", "Der Admiral hat die Flotte des Chats versenkt."),
            K("msgChatAbandoned", "Chat stimmt nicht mehr ab", "So lange, wie die Zuguhr erlaubt, hat niemand abgestimmt."),
            K("msgDuelChallenge", "Herausforderung", "!duel mit Namen. Schreib deine eigenen Befehlsnamen in die Duell-Nachrichten."),
            K("msgDuelOpenChallenge", "Offene Herausforderung", "!duel ohne Namen: jeder darf annehmen."),
            K("msgDuelAccepted", "Herausforderung angenommen", "Der Beitritt öffnet. {target} ist, wer angenommen hat."),
            K("msgDuelJoined", "Spieler tritt einer Seite bei", "Leer lassen, damit ein voller Beitritt den Chat nicht flutet."),
            K("msgDuelStart", "Duell beginnt", "{team} ist die Seite, die zuerst feuert, zufällig gewählt."),
            K("msgDuelTurn", "Nächste Seite ist dran", "Gesendet zu Beginn jeder Runde nach der ersten. Fehlschüsse und Treffer nutzen die Solo-Nachrichten, mit den Seiten als {user} und {admiral}."),
            K("msgDuelTurnPassed", "Seite lässt ihren Zug verstreichen", "Niemand auf dieser Seite hat abgestimmt."),
            K("msgDuelSunk", "Seite versenkt ein Schiff", "Gesendet, wenn eine Seite eines der Schiffe der anderen versenkt."),
            K("msgDuelStatus", "Stand des Duells", "Gesendet von !board während eines Duells."),
            K("msgDuelWin", "Seite gewinnt", "{share} ist der Anteil jedes Gewinners, den eigenen Einsatz eingeschlossen, vor dem Abzug für Süchtige und der Tagesgrenze."),
            K("msgDuelWinFree", "Seite gewinnt (nichts gesetzt)", "Freispiel, oder ein Einsatz von 0."),
            K("msgDuelForfeit", "Seite gibt auf", "Sie hat zu viele Züge hintereinander verstreichen lassen. Die Siegnachricht folgt."),
            K("msgDuelExpired", "Herausforderung nicht angenommen", "Der Einsatz des Herausforderers wird erstattet."),
            K("msgDuelCancelled", "Duell abgesagt", "Keine Seite hat je abgestimmt. Jeder Einsatz wird erstattet."),
            K("msgModStop", "Schlacht gestoppt", "{user} ist der Moderator, {game} der Spieler oder der Teamname des Chats. Nichts mehr wird ausgezahlt."),
            K("msgModStopDuel", "Duell gestoppt", "Jeder Einsatz wird erstattet."),
            K("msgModStopNothing", "Nichts zu stoppen", "Der Stopp-Befehl, ohne dass eine Schlacht oder ein Duell läuft."),
            K("msgHeatAutoHint", "So feuert man per Klick", "Gesendet an einen Twitch-Spieler ohne Verknüpfung, wenn seine Schlacht auf den Bildschirm kommt, solange der erste Klick übernimmt. Leer lassen, um nichts zu senden."),
            K("msgHeatLinkHint", "So verknüpft man sich", "Im selben Moment gesendet, wenn der erste Klick nicht übernimmt, sodass jeder Spieler sich mit !fire link verknüpft."),
            K("msgHeatLinked", "Verknüpft", "Bestätigt !fire link. Der Klick, der die Verknüpfung angelegt hat, feuert direkt danach."),
            K("msgHeatLinkNoClick", "Verknüpfung: kein Klick gesehen", "!fire link fand in den letzten Sekunden keinen Klick auf gegnerische Gewässer."),
            K("msgHeatLinkAmbiguous", "Verknüpfung: mehr als ein Klicker", "Mehrere Zuschauer haben in dieser Zeit geklickt, die Verknüpfung wird also abgelehnt statt geraten."),
            K("msgBadCoord", "Kein Feld auf dem Spielfeld", "!fire mit etwas, das kein Feld ist, etwa 'Z9'."),
            K("msgNoCoord", "Kein Feld angegeben", "!fire allein."),
            K("msgAlreadyFired", "Dort schon gefeuert", "Ein wiederholter Schuss kostet nie den Zug oder die Uhr."),
            K("msgNoGame", "Keine Schlacht läuft", "!fire, !fleet oder !board von einem Zuschauer ohne Schlacht."),
            K("msgAlreadyInGame", "Schon in einer Schlacht", "!battleship von einem Zuschauer, dessen Schlacht noch läuft."),
            K("msgFleetTooLate", "Zu spät zum Neuaufstellen", "!fleet nach dem ersten Schuss."),
            K("msgNotEnoughPoints", "Nicht genug Punkte", "Unter dem Mindestguthaben der Seite Spiel, oder zu wenig für den Einsatz eines Duells."),
            K("msgModeDisabled", "Spielmodus abgeschaltet", "Ein auf der Seite Modi abgeschalteter Modus."),
            K("msgChatBusy", "Chat-Schlacht muss warten", "Die Schlacht des Chats beginnt nur, wenn keine andere Schlacht läuft."),
            K("msgMaxGamesPerHour", "Zu viele Schlachten in dieser Stunde", "Die Grenze auf der Seite Zugriff."),
            K("msgDailyCapReached", "Tagesgewinn-Obergrenze erreicht", "Die Grenze auf der Seite Spiel."),
            K("msgDuelBusy", "Duell muss warten", "Ein Duell wird nur herausgefordert, solange nichts anderes läuft, und nichts anderes beginnt, solange eines läuft."),
            K("msgDuelNoChallenge", "Keine Herausforderung anzunehmen", "Nichts wartet, es ist die Herausforderung eines anderen, die eigene, oder ihre Zeit ist abgelaufen."),
            K("msgDuelNotJoining", "Beitritt nicht offen", "Der Beitritts-Befehl, während kein Duell Spieler aufnimmt."),
            K("msgDuelAlreadyJoined", "Schon auf einer Seite", "Der Beitritts-Befehl von jemandem, der schon beigetreten ist."),
            K("msgDuelTeamFull", "Seite zu weit voraus", "Nur mit einer Vorsprungsgrenze auf der Seite Modi."),
            K("msgDuelBadTeam", "Keine solche Seite", "Der Beitritts-Befehl nannte weder A noch B noch jemanden auf einer Seite."),
            K("msgDuelNotInDuel", "Nicht im Duell", "Eine !fire-Stimme von jemandem auf keiner Seite."),
            K("msgDuelNotYourTurn", "Diese Seite ist nicht dran", "{team} ist die Seite, die dran ist."),
            K("msgWhisperFleet", "Deine Flotte", "Einem Solo-Spieler geflüstert, wenn seine Schlacht beginnt, solange das Flüstern an ist."),
            K("msgNoStatsFound", "Noch keine Bilanz", "!bstats für einen Spieler ohne Schlachten."),
            K("msgNoLeaderboard", "Bestenliste leer", "!ballstats, bevor irgendjemand eine Bilanz hat."),
            Tx("Solo battle", "Solo-Schlacht"),
            Tx("Return fire", "Erwiderung"),
            Tx("Battle result", "Ergebnis der Schlacht"),
            Tx("Timer warnings", "Zeitwarnungen"),
            Tx("Moderator stop", "Stopp durch Moderator"),
            Tx("Errors", "Fehler"),
            Tx("Duel errors", "Duell-Fehler"),
            Tx("Whisper", "Flüstern"),
            Tx("Ensign", "Fähnrich"),
            Tx("Captain", "Kapitän"),
            Tx("Admiral", "Admiral"),
            Tx("Net: everything won minus everything lost", "Netto: alles Gewonnene minus alles Verlorene"),
            Tx("Points won, ignoring losses", "Gewonnene Punkte, ohne Verluste"),
            Tx("Ships sunk", "Versenkte Schiffe"),
            Tx("Accuracy, from 25 shots on", "Trefferquote, ab 25 Schüssen"),
            Tx("Free game, from the Game page. While it is on, ships still sink but no points are won or lost.", "Freispiel, von der Seite Spiel. Solange es an ist, sinken Schiffe weiterhin, aber keine Punkte werden gewonnen oder verloren."),
            Tx("Fire by clicking the stream, from the Clicks page. Twitch only, through the Heat extension.", "Per Klick auf den Stream feuern, von der Seite Klicks. Nur Twitch, über die Erweiterung Heat."),
            Tx("Player", "Spieler"),
            Tx("Platform", "Plattform"),
            Tx("Trigger", "Auslöser"),
            Tx("Shots", "Schüsse"),
            Tx("Hits", "Treffer"),
            Tx("Accuracy", "Trefferquote"),
            Tx("Change", "Änderung"),
            Tx("No battles logged yet. Every finished battle lands here once 'Write a log file per day' is on.", "Noch keine Schlachten protokolliert. Jede beendete Schlacht landet hier, sobald 'Eine Protokolldatei pro Tag schreiben' an ist."))
        .Pill("freeGame", "Free game on", "Free game off", tooltip: "Free game, from the Game page. While it is on, ships still sink but no points are won or lost.")
        .Pill("heatEnabled", "Click to Fire on", "Click to Fire off", tooltip: "Fire by clicking the stream, from the Clicks page. Twitch only, through the Heat extension.")
        .Samples("user", "alice", "admiral", "The Admiral", "ships", "3", "size", "8", "deploy", "60", "coord", "B7", "ship", "Cruiser", "amount", "300", "points", "Points", "enemyleft", "2", "yourleft", "3", "shots", "21", "hits", "9", "accuracy", "43", "net", "+600", "team", "Chat", "round", "15", "voters", "14", "seconds", "30", "challenger", "alice", "target", "bob", "fee", "100", "count", "4", "a", "4", "b", "3", "pot", "700", "missed", "1", "max", "3", "enemy", "Team B", "teama", "Team A", "teamb", "Team B", "aleft", "2", "bleft", "1", "turn", "Team A", "players", "4", "share", "175", "winner", "Team A", "turns", "3", "game", "alice", "input", "Z9", "required", "1,000", "balance", "250", "cap", "5,000", "other", "Team B", "gap", "1", "fleet", "Carrier A1-A5, Cruiser C3-E3, Destroyer G7-G8")
        .Answer("clockSummary", "60s to open fire, then 90s per turn, warned at 30s and 15s left.")
        .Answer("fleetSummary", "{\"lines\":[{\"text\":\"8×8 · 3 ships · about 36 shots per battle\",\"big\":true},\"10 of 64 squares taken by ships.\"]}")
        .Answer("broadcasterFound", "Found in Streamer.bot: 123456789. Used whenever the channel ID box is empty.")
        .Answer("presetQuick", "{\"status\":\"8×8 with 3 ships filled in; save to keep it.\"}")
        .Log("Chat Games/Logs", "BSG-log*.txt", writeLogKey: "logToFile", columns: new[] { "Player|User", "Platform|Platform", "Trigger|Trigger", "Board|Board", "Shots|Shots", "Hits|Hits", "Accuracy|Accuracy", "Change|Change" }, results: new[] { "WIN", "LOSS", "TIMEOUT", "ABANDONED", "STOPPED", "CHAT WIN", "CHAT LOSS", "DUEL" }, platforms: new[] { "twitch", "youtube", "kick" }, emptyText: "No battles logged yet. Every finished battle lands here once 'Write a log file per day' is on.")
        .Help(
            H("How a battle plays",
                Note("A viewer takes command of a fleet against a CPU opponent, the Admiral. Both fleets are placed at random and hidden from the other side. Each shot is a full exchange: the player fires, then the Admiral fires back. Sink the whole enemy fleet to win; lose yours and the battle is over. Nothing is staked up front: sinking one of the Admiral's ships pays its value at once, and losing one of your own costs its value.")),
            H("Commands",
                Cmd("!battleship", "puts a fleet to sea and starts a solo battle."),
                Cmd("!fire B7", "fires at a square, then the Admiral fires back. A square fired at before is refused and costs nothing."),
                Cmd("!fleet", "redeploys your ships at random, before your first shot only."),
                Cmd("!board", "shots, accuracy, ships left and the running total."),
                Cmd("!bstats", "a player's record. Add a name to look someone else up."),
                Cmd("!ballstats", "the leaderboard."),
                Cmd("!seeschlacht", "starts Chat vs the Admiral: the whole chat votes each round, just for fun."),
                Cmd("!duel @name", "challenges a viewer to a team duel, or anybody with no name."),
                Cmd("!accept", "takes up a challenge and captains Team B."),
                Cmd("!team A", "joins a side during sign-up: A or B, a player's name, or nothing for the smaller side."),
                Cmd("!stopbattle", "moderators: ends whatever is running and pays nothing more out. A duel's entries are refunded."),
                Cmd("!fire link", "with Click to Fire on: links your clicks on stream to you, once. Twitch only."),
                Note("These are the command names the Setup Guide uses. The game knows its commands by their IDs, so you can rename any of them in Streamer.bot.")),
            H("For your own actions",
                Note("Every finished battle fires a custom trigger (Custom > Chat Game > Battleship Game): BSG: Battle Won, BSG: Battle Lost, BSG: Battle Timed Out or BSG: Battle Stopped for a solo battle, BSG: Chat Battle Settled for the chat's battle, BSG: Duel Settled for a team duel, and BSG: Battle Settled for every one of them. The trigger carries the battle as arguments: battleOutcome (the log's result: WIN, LOSS, TIMEOUT, NO-SHOW, STOPPED), battleMode (solo, chat or duel), battleUser, battleUserId, battlePlatform, battleTrigger (fire, timeout or stop), battleBoard, battleShots, battleHits, battleAccuracy, battleShipsSunk, battleShipsLost, battleNet (the battle's total, or a duel's pot), battleWinner (a duel's winning side) and battleCurrency. GetLastBattleResult, run with an Execute C# Method sub-action on the game action, hands the same arguments to any other action.")),
            H("Where things are kept",
                Cmd("BSG-Settings.json", "everything on these pages."),
                Cmd("uiframework.layout.json", "your layout of this window: theme, renamed or hidden pages and fields."),
                Cmd("Overlay\\battleship_overlay.html", "the browser source, written by the game and kept up to date."),
                Cmd("Overlay\\battleship_state.json", "written as the battle plays; battleship_heat.json carries the Click to Fire settings for the overlay."),
                Cmd("Logs\\BSG-log-yyyy-MM-dd.txt", "one file per day, while 'Write a log file per day' is on."),
                Note("All of it sits in Streamer.bot's folder under Chat Games. A damaged settings file falls back to the .bak beside it and the broken one is kept as .broken.")))
        .About(credits: "Battleship Game by aaskjer",
            sections: new[]
            {
                H("Needs a points system",
                    Note("Battles are paid in the points of a currency system. These two are found on their own:"),
                    Link("Currency Core by StreamUP", "https://streamup.tips/product/currency-core"),
                    Link("LOYALTY POINTS by tawmae", "https://tawmae.xyz/loyalty-points"))
            }, links: new[] { Link("Battleship Game on the Streamer.bot Discord", "https://discord.com/channels/834650675224248362/1447648588694818836/1447648588694818836") });

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
        S("freeGame", false, page: "game", label: "Free game", help: "Battles for bragging rights: ships still sink and the overlay still runs, but no points are won or lost. Chat gets the Free game messages instead, so nothing mentions points.", section: "Points", aliases: new[] { "FreeGame" }),
        S("pointsVariable", "points", page: "game", label: "Points variable", help: "The Streamer.bot user variable that holds each viewer's points. Currency Core by StreamUP and LOYALTY POINTS by tawmae are found on their own and win over this box; only change it for a points system of your own.", section: "Points", aliases: new[] { "PointsVariable" }),
        S("pointsName", "Points", page: "game", label: "Shown in chat as", help: "What {points} says in chat, like 'Coins' or 'Doubloons'. Only the wording changes; the points variable stays the same.", section: "Points", aliases: new[] { "PointsVarName" }),
        S("winBonus", 0L, page: "game", label: "Win bonus", help: "Paid on top when a player sinks the whole enemy fleet. 0 for none: a win already pays every ship it sank. The chat's battle is played for fun and pays nothing.", kind: "long", section: "Economy", min: 0.0, aliases: new[] { "WinBonus" }),
        S("minBalanceToPlay", 0L, page: "game", label: "Minimum balance", help: "What a player needs to start a battle. 0 asks for the value of their own fleet, so nobody plays a battle they couldn't pay for losing.", kind: "long", section: "Economy", min: 0.0, aliases: new[] { "MinBalanceToPlay" }),
        S("dailyWinCap", 0L, page: "game", label: "Daily winnings cap", help: "The most a player can win per day. 0 for no cap. Once it is reached, further wins pay nothing until the next day.", kind: "long", section: "Economy", min: 0.0, aliases: new[] { "DailyWinCap" }),
        S("chargeDamagedOnAbandon", true, page: "game", label: "Charge for damaged ships when a player runs out of time", help: "Without this, walking away one hit before a Carrier sinks dodges its whole value. A Carrier hit 4 times out of 5 costs 4/5 of it. Never applies during normal play.", section: "Economy", aliases: new[] { "ChargeDamagedOnAbandon" }),
        S("clampBalanceAtZero", true, page: "game", label: "Never let a balance go below zero", help: "A player who can't cover a loss is charged only what they have.", section: "Economy", aliases: new[] { "ClampBalanceAtZero" }),
        S("economyNote", null, page: "game", help: "Nothing is staked up front: sinking one of the Admiral's ships pays its value at once, and losing one of your own costs its value. The ship values are on the Fleet page.", kind: "note", section: "Economy"),
        S("admiralDifficulty", "captain", page: "game", label: "Skill", help: "Ensign fires at random and never repeats a square, about 59 shots to clear a Quick board. Captain fires at random until it lands a hit, then works the squares around it and follows a line of hits to both ends, about 36 shots. Admiral hunts like the Captain, and between hits only checks squares spaced to the shortest ship still afloat, so nothing can hide between them, about 30 shots.", kind: "options", section: "The Admiral", options: new[] { "ensign|Ensign", "captain|Captain", "admiral|Admiral" }, aliases: new[] { "AdmiralDifficulty" }),
        S("admiralBlunderChance", 0, page: "game", label: "Blunder chance", help: "How often the Admiral throws away its plan and fires blind, from 0 (never) to 100 (always). A handicap that works on any skill level without letting it cheat.", kind: "percent", section: "The Admiral", min: 0.0, max: 100.0, aliases: new[] { "AdmiralBlunderChance" }, aliasFactor: 100.0),
        S("admiralDelayMs", 700, page: "game", label: "Return fire delay", help: "Pause between a shot and the Admiral's reply, in milliseconds, so the overlay reads as an exchange rather than one flash. 0 to 5000.", section: "The Admiral", min: 0.0, max: 5000.0, unit: "ms", aliases: new[] { "AdmiralDelayMs" }),
        S("admiralNote", null, page: "game", help: "The Admiral never sees your board. It only knows where it has fired, what it found there and which ship lengths are still afloat, the same things you know about its fleet.", kind: "note", section: "The Admiral"),
        S("deployTimeoutSec", 60, page: "game", label: "To open fire", help: "From starting a battle to the first shot. Nothing has sunk yet, so running out here costs nothing. At least 5.", kind: "duration", section: "Battle clock", min: 5.0, aliases: new[] { "DeployTimeoutSec" }),
        S("turnTimeout", 90, page: "game", label: "Per turn", help: "From the Admiral's reply to the next shot. Running out ends the battle and settles whatever has sunk. There is no command to leave a battle: this clock is the way out. At least 5.", kind: "duration", section: "Battle clock", min: 5.0, aliases: new[] { "TurnTimeoutSec" }),
        S("turnWarnFirstSec", 30, page: "game", label: "First warning", help: "Seconds left on the turn when chat is warned and the countdown ring turns amber. 0 turns it off.", kind: "duration", section: "Battle clock", min: 0.0, aliases: new[] { "TurnWarnFirstSec" }),
        S("turnWarnSecondSec", 15, page: "game", label: "Final warning", help: "Seconds left on the turn when chat is warned again and the ring pulses red. 0 turns it off.", kind: "duration", section: "Battle clock", min: 0.0, aliases: new[] { "TurnWarnSecondSec" }),
        S("clockSummary", null, page: "game", label: "The clock in short", help: "What the four values above add up to, as they stand.", kind: "action", section: "Battle clock", live: true, panel: true, auto: true),
        // ---- fleet ----
        S("presetBlitz", null, page: "fleet", label: "Blitz · 6×6 · 3 ships", help: "A 6×6 board with a Carrier of 4, a Cruiser of 3 and a Destroyer of 2. Fills in the board and the ships; nothing is saved until you save.", kind: "action", section: "Presets"),
        S("presetQuick", null, page: "fleet", label: "Quick · 8×8 · 3 ships", help: "An 8×8 board with a Carrier of 5, a Cruiser of 3 and a Destroyer of 2: the default, about 36 shots per battle.", kind: "action", section: "Presets"),
        S("presetClassic", null, page: "fleet", label: "Classic · 10×10 · 5 ships", help: "The classic 10×10 board with all five ships.", kind: "action", section: "Presets"),
        S("presetNote", null, page: "fleet", help: "A preset fills in the board size and which ships sail, with their lengths. Change anything afterwards as you like.", kind: "note", section: "Presets"),
        S("gridSize", 8, page: "fleet", label: "Grid size", help: "Squares per side, from 5 (A1 to E5) to 12 (A1 to L12). Bigger boards make longer battles.", section: "Board", options: new[] { "5|5 × 5", "6|6 × 6", "7|7 × 7", "8|8 × 8", "9|9 × 9", "10|10 × 10", "11|11 × 11", "12|12 × 12" }, aliases: new[] { "GridSize" }),
        S("noTouchingRule", true, page: "fleet", label: "Ships may not touch each other", help: "Keeps a one-square gap around every ship, so two ships never read as one long one and hunting is fairer.", section: "Board", aliases: new[] { "NoTouchingRule" }),
        S("shipCarrierEnabled", true, page: "fleet", label: "Carrier", help: "Sail with a Carrier. Both fleets always have the same ships.", section: "Ships", aliases: new[] { "ShipCarrierEnabled" }),
        S("shipCarrierLength", 5, page: "fleet", label: "Carrier length", help: "How many squares the Carrier takes up.", section: "Ships", min: 1.0, max: 12.0, requires: "shipCarrierEnabled", aliases: new[] { "ShipCarrierLength" }),
        S("shipCarrierValue", 500L, page: "fleet", label: "Carrier value", help: "What sinking the Carrier pays, and what losing it costs.", kind: "long", section: "Ships", min: 0.0, requires: "shipCarrierEnabled", aliases: new[] { "ShipCarrierValue" }),
        S("shipBattleshipEnabled", false, page: "fleet", label: "Battleship", help: "Sail with a Battleship.", section: "Ships", aliases: new[] { "ShipBattleshipEnabled" }),
        S("shipBattleshipLength", 4, page: "fleet", label: "Battleship length", help: "How many squares the Battleship takes up.", section: "Ships", min: 1.0, max: 12.0, requires: "shipBattleshipEnabled", aliases: new[] { "ShipBattleshipLength" }),
        S("shipBattleshipValue", 400L, page: "fleet", label: "Battleship value", help: "What sinking the Battleship pays, and what losing it costs.", kind: "long", section: "Ships", min: 0.0, requires: "shipBattleshipEnabled", aliases: new[] { "ShipBattleshipValue" }),
        S("shipCruiserEnabled", true, page: "fleet", label: "Cruiser", help: "Sail with a Cruiser.", section: "Ships", aliases: new[] { "ShipCruiserEnabled" }),
        S("shipCruiserLength", 3, page: "fleet", label: "Cruiser length", help: "How many squares the Cruiser takes up.", section: "Ships", min: 1.0, max: 12.0, requires: "shipCruiserEnabled", aliases: new[] { "ShipCruiserLength" }),
        S("shipCruiserValue", 300L, page: "fleet", label: "Cruiser value", help: "What sinking the Cruiser pays, and what losing it costs.", kind: "long", section: "Ships", min: 0.0, requires: "shipCruiserEnabled", aliases: new[] { "ShipCruiserValue" }),
        S("shipSubmarineEnabled", false, page: "fleet", label: "Submarine", help: "Sail with a Submarine.", section: "Ships", aliases: new[] { "ShipSubmarineEnabled" }),
        S("shipSubmarineLength", 3, page: "fleet", label: "Submarine length", help: "How many squares the Submarine takes up.", section: "Ships", min: 1.0, max: 12.0, requires: "shipSubmarineEnabled", aliases: new[] { "ShipSubmarineLength" }),
        S("shipSubmarineValue", 300L, page: "fleet", label: "Submarine value", help: "What sinking the Submarine pays, and what losing it costs.", kind: "long", section: "Ships", min: 0.0, requires: "shipSubmarineEnabled", aliases: new[] { "ShipSubmarineValue" }),
        S("shipDestroyerEnabled", true, page: "fleet", label: "Destroyer", help: "Sail with a Destroyer.", section: "Ships", aliases: new[] { "ShipDestroyerEnabled" }),
        S("shipDestroyerLength", 2, page: "fleet", label: "Destroyer length", help: "How many squares the Destroyer takes up.", section: "Ships", min: 1.0, max: 12.0, requires: "shipDestroyerEnabled", aliases: new[] { "ShipDestroyerLength" }),
        S("shipDestroyerValue", 200L, page: "fleet", label: "Destroyer value", help: "What sinking the Destroyer pays, and what losing it costs.", kind: "long", section: "Ships", min: 0.0, requires: "shipDestroyerEnabled", aliases: new[] { "ShipDestroyerValue" }),
        S("fleetSummary", null, page: "fleet", label: "The fleet in short", help: "The board, the ships that sail and about how many shots a battle takes, as the values stand.", kind: "action", section: "Ships", live: true, panel: true, auto: true),
        S("fleetNote", null, page: "fleet", help: "Both sides always sail the same fleet. A ship's value is what sinking it pays and what losing it costs, settled the moment it goes down.", kind: "note", section: "Ships"),
        // ---- modes ----
        S("soloEnabled", true, page: "modes", label: "Solo: one viewer against the Admiral", help: "Started with !battleship. Each player has their own battle and settles every sinking as it happens.", section: "Game modes", aliases: new[] { "SoloEnabled" }),
        S("chatBattleEnabled", true, page: "modes", label: "Chat vs the Admiral: the whole chat votes on every shot", help: "Started with !seeschlacht. Anybody can take part, by clicking on stream or with !fire. Played for fun: no points.", section: "Game modes", aliases: new[] { "ChatBattleEnabled" }),
        S("duelEnabled", true, page: "modes", label: "Team duel: one chat team against another", help: "A viewer challenges another, both sides take on players, and the winning side shares everyone's entry.", section: "Game modes", aliases: new[] { "DuelEnabled" }),
        S("chatNote", null, page: "modes", help: "The whole chat is one fleet, played for fun: nobody pays and nobody wins points. Each round every viewer has one vote, a click on enemy waters or !fire B7, and when the round ends the square with the most votes is fired at.", kind: "note", section: "Chat vs the Admiral"),
        S("chatTeamName", "Chat", page: "modes", label: "Team name", help: "What the chat's fleet is called on the overlay and in chat, as {team}.", section: "Chat vs the Admiral", aliases: new[] { "ChatTeamName" }),
        S("chatRoundSec", 15, page: "modes", label: "Seconds per round", help: "Make it longer than your stream delay, or viewers are still voting on a picture from before the last shot. At least 5.", kind: "duration", section: "Chat vs the Admiral", min: 5.0, aliases: new[] { "ChatRoundSec" }),
        S("chatNote2", null, page: "modes", help: "Every viewer's click counts, logged in to Twitch or not - nothing needs linking. Somebody logged out can vote again by reloading the page, which does no harm in a battle played for fun. A chat battle starts only when no other battle is running, and if nobody votes for as long as the turn clock allows, the chat breaks it off.", kind: "note", section: "Chat vs the Admiral"),
        S("duelNote", null, page: "modes", help: "A viewer challenges another by name, or anybody with no name, and captains Team A; whoever accepts captains Team B. Everyone else then picks a side, and every member pays the entry fee. The sides take turns, one voting round each, and the side that sinks the other's fleet shares the pot. Each winner gets their own entry back whole; only the winnings on top take the Addicted cut and the daily cap.", kind: "note", section: "Team duel"),
        S("duelEntryFee", 100L, page: "modes", label: "Entry fee", help: "What every member pays to join a side. 0 for a duel played for the record alone. With Free game on, nobody pays.", kind: "long", section: "Team duel", min: 0.0, aliases: new[] { "DuelEntryFee" }),
        S("duelAcceptSec", 60, page: "modes", label: "Seconds to accept", help: "A challenge nobody takes up in time is dropped and the entry refunded. At least 10.", kind: "duration", section: "Team duel", min: 10.0, aliases: new[] { "DuelAcceptSec" }),
        S("duelJoinSec", 60, page: "modes", label: "Seconds to sign up", help: "From the moment the challenge is accepted. At least 10.", kind: "duration", section: "Team duel", min: 10.0, aliases: new[] { "DuelJoinSec" }),
        S("duelRoundSec", 20, page: "modes", label: "Seconds per round", help: "One round is one side's shot. Longer than your stream delay, as for the chat's battle. At least 5.", kind: "duration", section: "Team duel", min: 5.0, aliases: new[] { "DuelRoundSec" }),
        S("duelMaxTeamGap", 0, page: "modes", label: "Lead limit (0 = none)", help: "How many more players one side may have than the other. With 1, a side is never more than one player ahead. 0 lets anybody join either side.", section: "Team duel", min: 0.0, aliases: new[] { "DuelMaxTeamGap" }),
        S("duelMaxMissedTurns", 3, page: "modes", label: "Turns a side may pass", help: "A side that lets this many turns in a row pass without a vote gives up, and the other side wins. If nobody fired at all, the duel is called off and every entry refunded. At least 1.", section: "Team duel", min: 1.0, aliases: new[] { "DuelMaxMissedTurns" }),
        S("duelNote2", null, page: "modes", help: "A duel can be challenged only while nothing else runs, and nothing else starts until its last shot. Anonymous Heat clicks never count in a duel, so members vote with !fire or link their clicks once with !fire link.", kind: "note", section: "Team duel"),
        S("stopNote", null, page: "modes", help: "The stop command ends whatever is running, solo battles, the chat's battle or a duel, and pays nothing more out: no win bonus, no pot, no charge for damaged ships. A duel's entries are refunded. Who may use it is set on the command in Streamer.bot.", kind: "note", section: "Stopping a game"),
        // ---- overlay ----
        S("overlayNote", null, page: "overlay", help: "The overlay is battleship_overlay.html in the folder below. The game writes it there and keeps it up to date, saving your previous copy as a .bak. Its background is transparent, so it layers over any scene.", kind: "note", section: "Browser source"),
        S("overlayFolder", "Overlay", page: "overlay", label: "Overlay folder", help: "Relative to the Battleship Game folder, or a full path on any drive. Empty uses 'Overlay'.", kind: "path", section: "Browser source", aliases: new[] { "OverlayFolder" }),
        S("obsNote", null, page: "overlay", help: "Creates the scene, and in it a browser source the size of your canvas pointed at the overlay file. OBS has to be connected in Streamer.bot under Stream Apps > OBS Studio.", kind: "note", section: "OBS auto-setup"),
        S("obsConnectionId", 0, page: "overlay", label: "Connection #", help: "Which of Streamer.bot's OBS connections to use, if you have more than one. 0 is the first.", section: "OBS auto-setup", min: 0.0, aliases: new[] { "ObsConnectionId" }),
        S("obsSceneName", "Battleship", page: "overlay", label: "Scene", help: "The scene the source is added to. It is created if it doesn't exist yet.", section: "OBS auto-setup", aliases: new[] { "ObsSceneName" }),
        S("obsSourceName", "Battleship Overlay", page: "overlay", label: "Source", help: "The browser source created in that scene. Nothing is touched if OBS already has a source of this name.", section: "OBS auto-setup", aliases: new[] { "ObsSourceName" }),
        S("setupObsOverlay", null, page: "overlay", label: "Set the overlay up in OBS", help: "Writes the overlay file if it is missing, then creates the scene and the browser source, the size of your canvas. Save first: the setup reads the saved folder and names.", kind: "action", section: "OBS auto-setup", confirm: "This creates the scene if it is missing and adds a browser source the size of your canvas pointing at the overlay file, over the OBS connection set above. Save your changes first if you edited the folder or the names. Go ahead?"),
        S("overlayScale", 100, page: "overlay", label: "Scale", help: "Scales the whole overlay. 100 is full size.", kind: "percent", section: "Size", min: 10.0, max: 300.0, aliases: new[] { "OverlayScale" }, aliasFactor: 100.0),
        S("overlayFontScale", 100, page: "overlay", label: "Text size", help: "Scales the text only, on top of the overall scale.", kind: "percent", section: "Size", min: 10.0, max: 300.0, aliases: new[] { "OverlayFontScale" }, aliasFactor: 100.0),
        S("overlayResultSeconds", 8, page: "overlay", label: "Result shown", help: "How long a finished battle stays up before it fades out. At least 1.", kind: "duration", section: "Size", min: 1.0, aliases: new[] { "OverlayGameOverFadeSec" }),
        S("overlayAdmiralName", "The Admiral", page: "overlay", label: "Opponent name", help: "What the CPU is called on the overlay and in chat, as {admiral}.", section: "Content", aliases: new[] { "OverlayAdmiralName" }),
        S("overlayShowPlayerRoster", true, page: "overlay", label: "The player's fleet roster", help: "The ship list with damage pips under the player's own board.", section: "Content", aliases: new[] { "OverlayShowPlayerRoster", "OverlayShowRoster" }),
        S("overlayShowEnemyRoster", true, page: "overlay", label: "The opponent's fleet roster", help: "The same list under enemy waters. Its pips show which of their ships your hits landed on; switch it off to keep that hidden. Whichever roster is hidden, the two boards stay level.", section: "Content", aliases: new[] { "OverlayShowEnemyRoster" }),
        S("overlayShowCoords", true, page: "overlay", label: "Coordinates", help: "The letters and numbers along the boards.", section: "Content", aliases: new[] { "OverlayShowCoords" }),
        S("overlayShowTurnTimer", true, page: "overlay", label: "Turn countdown ring", help: "Turns amber at the first warning and pulses red at the final one.", section: "Content", aliases: new[] { "OverlayShowTurnTimer" }),
        S("overlayShowScore", true, page: "overlay", label: "Running score and accuracy", help: "Shots, hits and the battle's running total.", section: "Content", aliases: new[] { "OverlayShowScore" }),
        S("overlayAnimations", true, page: "overlay", label: "Animations", help: "Impact bursts, the marker on the last shot and the pulsing final-warning ring.", section: "Content", aliases: new[] { "OverlayAnimationsEnabled" }),
        S("overlayHideOnFreeGame", false, page: "overlay", label: "Hide the overlay during Free game", help: "Nothing is drawn while Free game is on.", section: "Content", aliases: new[] { "OverlayHideOnFreeGame" }),
        S("overlayHitColour", "#ff5a3c", page: "overlay", label: "Hit", help: "A square where a shot hit a ship.", kind: "colour", section: "Colours", aliases: new[] { "OverlayHitColor" }),
        S("overlayMissColour", "#8fb8d8", page: "overlay", label: "Miss", help: "A square where a shot found only water.", kind: "colour", section: "Colours", aliases: new[] { "OverlayMissColor" }),
        S("overlaySunkColour", "#d3283a", page: "overlay", label: "Sunk", help: "A ship that has gone down.", kind: "colour", section: "Colours", aliases: new[] { "OverlaySunkColor" }),
        S("overlayShipColour", "#5b6b7c", page: "overlay", label: "Ship hull", help: "Your own ships on the left-hand board.", kind: "colour", section: "Colours", aliases: new[] { "OverlayShipColor" }),
        S("overlayGridColour", "#4a7fa5", page: "overlay", label: "Grid lines", help: "The lines between the squares.", kind: "colour", section: "Colours", aliases: new[] { "OverlayGridColor" }),
        S("overlayWaterColour", "#0d2438", page: "overlay", label: "Water", help: "The squares nobody has fired at.", kind: "colour", section: "Colours", aliases: new[] { "OverlayWaterColor" }),
        S("overlayBackgroundColour", "#000000", page: "overlay", label: "Background", help: "The panel behind the boards, as solid as the opacity below makes it.", kind: "colour", section: "Colours", aliases: new[] { "OverlayBackgroundColor" }),
        S("overlayBackgroundOpacity", 0, page: "overlay", label: "Background opacity", help: "0 keeps the overlay fully transparent, which is what most scenes want.", kind: "percent", section: "Colours", min: 0.0, max: 100.0, factor: 1.0, aliases: new[] { "OverlayBackgroundOpacity" }, aliasFactor: 100.0),
        S("coloursNote", null, page: "overlay", help: "Your own fleet is drawn in full and the Admiral's waters stay fogged until something is hit. The Admiral reads neither chat nor the overlay, so showing your ships gives nothing away.", kind: "note", section: "Colours"),
        // ---- clicks ----
        S("clicksNote", null, page: "clicks", help: "Heat is a free Twitch extension that passes viewers' clicks on your video to the overlay. With this on, the player whose battle is on screen can fire by clicking a square in enemy waters, and !fire keeps working as before.", kind: "note", section: "Click to Fire"),
        S("heatEnabled", false, page: "clicks", label: "Fire by clicking the stream", help: "Twitch only: Heat is a Twitch extension. Needs Heat on your channel and Streamer.bot's WebSocket server running, see Setup below.", section: "Click to Fire", aliases: new[] { "HeatEnabled" }, twitchOnly: true),
        S("heatAutoLink", true, page: "clicks", label: "First click takes control", help: "The first click in a player's battle takes control: Heat doesn't say who clicked, so the first viewer to click enemy waters in a player's battle is taken to be that player, for that battle. It holds back when another viewer clicked there in the last 20 seconds, or when the clicker is linked to a different player. A player whose battle was taken over clicks once and types !fire link, which also makes a link that lasts. Off, every player links with !fire link.", section: "Click to Fire", aliases: new[] { "HeatAutoLink" }),
        S("setupNote", null, page: "clicks", help: "1. Install Heat on your channel and activate it as an Overlay extension: https://dashboard.twitch.tv/extensions/cr20njfkgll4okyrhag7xxph270sqk\n2. Start Streamer.bot's WebSocket server: Servers/Clients > WebSocket Server, tick Auto Start, then Start Server. The overlay hands every click to the Battleship Game action through it.\n3. Players just click. If someone else's click got in first, or the switch above is off, the player clicks once and types !fire link; that link is remembered for later battles.\nHeat works in desktop browsers only: Twitch shows no overlay extensions in its mobile app, so phone viewers keep using !fire.", kind: "note", section: "Setup"),
        S("heatChannelId", "", page: "clicks", label: "Twitch channel ID", help: "The number Heat files your viewers' clicks under: your channel's user ID, not its name. Leave it empty to use the broadcaster account connected to Streamer.bot.", section: "Connection", aliases: new[] { "HeatChannelId" }),
        S("heatStreamerBotUrl", "ws://127.0.0.1:8080/", page: "clicks", label: "Streamer.bot WebSocket", help: "Where the overlay reaches Streamer.bot: the address and port under Servers/Clients > WebSocket Server. Usually ws://127.0.0.1:8080/", section: "Connection", aliases: new[] { "HeatStreamerBotUrl" }),
        S("broadcasterFound", null, page: "clicks", label: "The broadcaster account", help: "The Twitch account connected to Streamer.bot, used whenever the channel ID box is empty.", kind: "action", section: "Connection", live: true, panel: true, auto: true),
        S("heatStreamerBotPassword", "", page: "clicks", label: "WebSocket password", help: "Only if you switched on authentication for the WebSocket server. It is stored in plain text next to the overlay, because the overlay has to read it.", section: "Connection", aliases: new[] { "HeatStreamerBotPassword" }),
        S("alignNote", null, page: "clicks", help: "Heat reports each click as a spot on the whole stream picture. That lines up by itself when the overlay's browser source covers the whole canvas, which is how OBS auto-setup creates it. If you moved or resized the source, say where it sits in percent of the canvas: a source shrunk to half size in the bottom-right corner is 50 / 50 / 50 / 50.", kind: "note", section: "Lining up the clicks"),
        S("heatAreaLeft", 0, page: "clicks", label: "Left", help: "How far from the left edge of the canvas the source starts.", kind: "percent", section: "Lining up the clicks", min: 0.0, max: 100.0, aliases: new[] { "HeatAreaLeft" }, aliasFactor: 100.0),
        S("heatAreaTop", 0, page: "clicks", label: "Top", help: "How far from the top of the canvas the source starts.", kind: "percent", section: "Lining up the clicks", min: 0.0, max: 100.0, aliases: new[] { "HeatAreaTop" }, aliasFactor: 100.0),
        S("heatAreaWidth", 100, page: "clicks", label: "Width", help: "How much of the canvas width the source covers. 100 is all of it.", kind: "percent", section: "Lining up the clicks", min: 1.0, max: 400.0, aliases: new[] { "HeatAreaWidth" }, aliasFactor: 100.0),
        S("heatAreaHeight", 100, page: "clicks", label: "Height", help: "How much of the canvas height the source covers. 100 is all of it.", kind: "percent", section: "Lining up the clicks", min: 1.0, max: 400.0, aliases: new[] { "HeatAreaHeight" }, aliasFactor: 100.0),
        S("heatShowClicks", false, page: "clicks", label: "Show every click on the overlay (alignment test)", help: "Draws a marker wherever anyone clicks, with the connection status in the top-left corner. Viewers see it too, so switch it off once the markers land where you click.", section: "Lining up the clicks", aliases: new[] { "HeatShowClicks" }),
        S("alignNote2", null, page: "clicks", help: "To check, switch the test on and save, then while you are live open your channel in a browser and click around the video. Each marker should land under the pointer. The player's own clicks show in gold, named after the square they fire at.", kind: "note", section: "Lining up the clicks"),
        S("markersNote", null, page: "clicks", help: "Optional. Heat's own Markers overlay pins a marker where each viewer last clicked, so everyone sees the clicks land. The button adds it to the scene named on the Overlay page, at the size of your canvas, over the same OBS connection.", kind: "note", section: "Heat's click markers"),
        S("heatObsSourceName", "Heat Click Markers", page: "clicks", label: "Markers source", help: "The browser source created for Heat's markers. Nothing is touched if OBS already has a source of this name.", section: "Heat's click markers", aliases: new[] { "HeatObsSourceName" }),
        S("addHeatMarkers", null, page: "clicks", label: "Add to OBS", help: "Adds a browser source showing https://heat.j38.net/markers/?channel=<your channel ID> to the overlay's scene. Save first: it reads the saved channel ID and names.", kind: "action", section: "Heat's click markers", confirm: "This adds a browser source showing Heat's click markers for your channel to the overlay's scene (created if it is missing), at the size of your canvas, over the OBS connection on the Overlay page. Save first if you changed the channel ID or the names. Go ahead?"),
        S("markersNote2", null, page: "clicks", help: "What chat is told about linking is on the Messages page, under Click to Fire.", kind: "note", section: "Heat's click markers"),
        // ---- access ----
        S("platformsNote", null, page: "access", help: "Twitch, YouTube and Kick all play, and every reply goes to the chat the command came from. To leave a platform out, untick it on the game's commands in Streamer.bot, for example 'Kick Message'. Click to Fire and the fleet whisper work on Twitch only.", kind: "note", section: "Platforms"),
        S("whisperOwnFleet", true, page: "access", label: "Whisper each player their fleet", help: "Twitch only: Streamer.bot can't whisper on YouTube or Kick. Twitch drops whispers from accounts without a verified phone number; a failure is logged and the battle goes on. The overlay already shows the player their fleet, so this is a private second copy.", section: "Platforms", aliases: new[] { "WhisperOwnFleet" }, twitchOnly: true),
        S("globalCooldown", 0, page: "access", label: "Everyone", help: "How long everyone waits after anybody starts a battle with !battleship, in seconds. 0 turns it off. Saving writes it onto the command in Streamer.bot.", section: "Cooldowns", min: 0.0, aliases: new[] { "GlobalCooldown" }),
        S("userCooldown", 0, page: "access", label: "Per viewer", help: "How long one viewer waits before starting their next battle, in seconds. 0 turns it off. Saving writes it onto the command in Streamer.bot.", section: "Cooldowns", min: 0.0, aliases: new[] { "UserCooldown" }),
        S("maxGamesPerHour", 0, page: "access", label: "Battles per hour (0 = none)", help: "How many battles one viewer may start per hour. 0 for no limit.", section: "Cooldowns", min: 0.0, unlimitedAtZero: true, aliases: new[] { "MaxGamesPerHour" }),
        S("cooldownNote", null, page: "access", help: "What chat is told while a cooldown runs is on the Messages page, under Cooldown.", kind: "note", section: "Cooldowns"),
        S("blockedUsers", "", page: "access", label: "Blocked viewers", help: "Viewers who can't play at all, by their user name.", section: "Who may not play", aliases: new[] { "BlockedUsers" }),
        S("blockedGroups", "ExampleGroup, TooAddictedToPlay, YourCustomBlockedGroup", page: "access", label: "Blocked groups", help: "Streamer.bot user groups whose members can't play. Names must match the groups exactly.", section: "Who may not play", aliases: new[] { "BlockedGroups" }),
        S("addictedUsers", "", page: "access", label: "Addicted viewers", help: "Viewers who play as usual but win less: their winnings are cut by the Addicted cut below.", section: "Addicted", aliases: new[] { "AddictedUsers" }),
        S("addictedGroups", "", page: "access", label: "Addicted groups", help: "Members of these Streamer.bot groups count as addicted too.", section: "Addicted", aliases: new[] { "AddictedGroups" }),
        S("addictedPayoutCut", 50, page: "access", label: "Addicted cut", help: "How much of an addicted viewer's winnings is held back, from 0 (nothing) to 100 (all of it). 50 means they earn half. Losses are unaffected.", kind: "percent", section: "Addicted", min: 0.0, max: 100.0, aliases: new[] { "AddictedPayoutCut" }, aliasFactor: 100.0),
        // ---- stats ----
        S("leaderboardRankBy", "net", page: "stats", label: "Rank players by", help: "What !ballstats ranks by. Net is the fair one, since losing ships costs points.", kind: "options", section: "Ranking", options: new[] { "net|Net: everything won minus everything lost", "won|Points won, ignoring losses", "sunk|Ships sunk", "accuracy|Accuracy, from 25 shots on" }, aliases: new[] { "LeaderboardRankBy" }),
        S("leaderboardSize", 5, page: "stats", label: "Places shown", help: "How many players !ballstats lists.", section: "Ranking", options: new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", "10" }, aliases: new[] { "LeaderboardSize" }),
        S("leaderboardShowMedals", true, page: "stats", label: "Medals for the top three", help: "🥇🥈🥉 in front of the first three places.", section: "Ranking", aliases: new[] { "LeaderboardShowMedals" }),
        S("statsNote", null, page: "stats", help: "Every tracked value is kept in a persisted bsg_* user variable, so any other Streamer.bot action can read a player's record.", kind: "note", section: "Ranking"),
        S("leaderboardExcludeBroadcaster", true, page: "stats", label: "Hide the broadcaster", help: "Matched by user ID, so a name change can't put them back on the board.", section: "Who appears", aliases: new[] { "LeaderboardExcludeBroadcaster" }),
        S("leaderboardExcludeBot", true, page: "stats", label: "Hide the bot account", help: "Matched by user ID.", section: "Who appears", aliases: new[] { "LeaderboardExcludeBot" }),
        S("leaderboardUseBot", true, page: "stats", label: "Bot account", help: "Send the leaderboard from your Streamer.bot bot account instead of the broadcaster account.", section: "Leaderboard sent as", aliases: new[] { "LeaderboardUseBot" }),
        S("leaderboardSendAction", false, page: "stats", label: "Send as /me", help: "Twitch only: YouTube and Kick have no /me, so there the leaderboard always goes out as plain chat. On Twitch, sends it as a /me action.", section: "Leaderboard sent as", aliases: new[] { "LeaderboardSendAction" }, twitchOnly: true),
        S("sentNote", null, page: "stats", help: "The !bstats and !ballstats replies are on the Messages page, under Stats, and go out like the game's messages.", kind: "note", section: "Leaderboard sent as"),
        S("leaderboardAutoReset", "off", page: "stats", label: "Automatic reset", help: "Clears the leaderboard on its own when the period rolls over, using the scope below. Switching it on never wipes anything at once: the current period is recorded first, and the reset happens at the next boundary.", kind: "options", section: "Seasons", options: new[] { "off|Off", "weekly|Weekly, on Mondays", "monthly|Monthly, on the 1st" }, aliases: new[] { "LeaderboardAutoReset" }),
        S("leaderboardResetScope", "totals", page: "stats", label: "What a reset clears", help: "Totals only: points won, points lost and net. Battles, streaks and records are kept. Everything: every statistic of every player, a clean slate.", kind: "options", section: "Seasons", options: new[] { "totals|Totals only", "all|Everything" }, aliases: new[] { "LeaderboardResetScope" }),
        S("resetLeaderboard", null, page: "stats", label: "Reset the leaderboard now", help: "Clears what 'What a reset clears' says, for every player, straight away. Save first: the reset reads the saved scope.", kind: "action", section: "Seasons", confirm: "This permanently clears the leaderboard for every player according to the reset scope above (saved). It cannot be undone. Continue?"),
        // ---- log ----
        S("logToFile", true, page: "log", label: "Write a log file per day", help: "Writes every finished battle - win, loss, timeout, the chat's battle and the duel - to a dated 'BSG-log-yyyy-MM-dd.txt' in the Logs folder, one file per day.", aliases: new[] { "LogToFile" }),
        // ---- messages ----
        Msg("Solo battle", "msgGameStart", "🚢 {user} puts to sea against {admiral}! {ships} ships on a {size}x{size} grid. Fire with !fire B7 — {deploy}s to open fire.", "Battle started", "Posted when a player starts a battle with !battleship.", new[] { "user", "admiral", "ships", "size", "deploy" }, alias: "MsgGameStart"),
        Msg("Solo battle", "msgFleetRerolled", "🔄 {user}, your fleet has been redeployed.", "Fleet redeployed", "Confirms !fleet, which redeploys the ships before the first shot.", new[] { "user" }, alias: "MsgFleetRerolled"),
        Msg("Solo battle", "msgPlayerMiss", "🌊 {user} fires at {coord} — splash, nothing there.", "Miss", "The player's shot found only water.", new[] { "user", "coord", "admiral" }, alias: "MsgPlayerMiss"),
        Msg("Solo battle", "msgPlayerHit", "💥 {user} fires at {coord} — HIT!", "Hit", "The player's shot hit a ship.", new[] { "user", "coord", "admiral" }, alias: "MsgPlayerHit"),
        Msg("Solo battle", "msgPlayerSunk", "🔥 {user} SINKS the {admiral}'s {ship} at {coord}! +{amount} {points} — {enemyleft} enemy ships left.", "Enemy ship sunk", "The player sank one of the Admiral's ships. The points are paid before this is posted.", new[] { "user", "coord", "ship", "admiral", "amount", "points", "enemyleft" }, alias: "MsgPlayerSunk"),
        Msg("Solo battle", "msgPlayerSunkFree", "🔥 {user} SINKS the {admiral}'s {ship} at {coord}! {enemyleft} enemy ships left.", "Enemy ship sunk (Free game)", "Used instead while Free game is on, so nothing mentions points.", new[] { "user", "coord", "ship", "admiral", "enemyleft" }, alias: "MsgPlayerSunkFree"),
        Msg("Solo battle", "msgBoardStatus", "🎯 {user}: {shots} shots, {hits} hits ({accuracy}%). Your fleet: {yourleft} — {admiral}'s fleet: {enemyleft}. Running total: {net} {points}.", "Board status", "Posted by !board during a solo battle.", new[] { "user", "admiral", "shots", "hits", "accuracy", "yourleft", "enemyleft", "net", "points" }, alias: "MsgBoardStatus"),
        Msg("Return fire", "msgAdmiralMiss", "🌊 {admiral} returns fire at {coord} — miss.", "Miss", "The Admiral's shot found only water.", new[] { "user", "coord", "admiral" }, alias: "MsgAdmiralMiss"),
        Msg("Return fire", "msgAdmiralHit", "💢 {admiral} returns fire at {coord} — your ship is hit!", "Hit", "The Admiral hit one of the player's ships.", new[] { "user", "coord", "admiral" }, alias: "MsgAdmiralHit"),
        Msg("Return fire", "msgAdmiralSunk", "☠️ {admiral} sinks your {ship} at {coord}! −{amount} {points} — {yourleft} of your ships left.", "Your ship sunk", "The Admiral sank one of the player's ships. The points are taken before this is posted.", new[] { "user", "coord", "ship", "admiral", "amount", "points", "yourleft" }, alias: "MsgAdmiralSunk"),
        Msg("Return fire", "msgAdmiralSunkFree", "☠️ {admiral} sinks your {ship} at {coord}! {yourleft} of your ships left.", "Your ship sunk (Free game)", "Used instead while Free game is on.", new[] { "user", "coord", "ship", "admiral", "yourleft" }, alias: "MsgAdmiralSunkFree"),
        Msg("Battle result", "msgWin", "🏆 {user} has sunk the {admiral}'s entire fleet in {shots} shots! Net for the battle: {net} {points}.", "Win", "{net} is the battle's total, everything sunk minus everything lost, with its sign.", new[] { "user", "admiral", "shots", "net", "points" }, alias: "MsgWin"),
        Msg("Battle result", "msgWinFree", "🏆 {user} has sunk the {admiral}'s entire fleet in {shots} shots!", "Win (Free game)", "Used instead while Free game is on.", new[] { "user", "admiral", "shots" }, alias: "MsgWinFree"),
        Msg("Battle result", "msgLoss", "💀 {admiral} has sunk {user}'s fleet. Net for the battle: {net} {points}.", "Loss", "{net} is the battle's total, with its sign.", new[] { "user", "admiral", "shots", "net", "points" }, alias: "MsgLoss"),
        Msg("Battle result", "msgLossFree", "💀 {admiral} has sunk {user}'s fleet. Better luck next time.", "Loss (Free game)", "Used instead while Free game is on.", new[] { "user", "admiral", "shots" }, alias: "MsgLossFree"),
        Msg("Battle result", "msgTurnTimeout", "⏰ {user} abandoned the battle. Net for the battle: {net} {points}.", "Ran out of time", "The turn clock ran out mid-battle. Whatever had sunk is settled.", new[] { "user", "admiral", "shots", "net", "points" }, alias: "MsgTurnTimeout"),
        Msg("Battle result", "msgTurnTimeoutFree", "⏰ {user} abandoned the battle.", "Ran out of time (Free game)", "Used instead while Free game is on.", new[] { "user", "admiral", "shots" }, alias: "MsgTurnTimeoutFree"),
        Msg("Battle result", "msgDeployTimeout", "⚓ {user} never gave the order to fire — the fleet returns to port. Nothing lost.", "Never opened fire", "Nothing had sunk, so nothing is charged and it doesn't count as a battle.", new[] { "user" }, alias: "MsgDeployTimeout"),
        Msg("Timer warnings", "msgTurnWarnFirst", "⏳ @{user}, {seconds} seconds to fire before you forfeit the battle!", "First warning", "Posted when the turn clock reaches the first warning. The countdown ring turns amber.", new[] { "user", "seconds" }, alias: "MsgTurnWarnFirst"),
        Msg("Timer warnings", "msgTurnWarnSecond", "⚠️ @{user}, {seconds} SECONDS — fire now!", "Final warning", "Posted when the turn clock reaches the final warning. The ring pulses red.", new[] { "user", "seconds" }, alias: "MsgTurnWarnSecond"),
        Msg("Chat vs the Admiral", "msgChatStarted", "⚓ {team} takes on {admiral}! Click enemy waters on stream or vote with !fire B7 — every {round}s the square with the most votes is fired at. Just for fun: no points to win or lose.", "Battle started", "Posted when !seeschlacht starts the chat's battle. Misses and hits use the solo messages, with the team name as {user}.", new[] { "team", "admiral", "round", "ships", "size" }, alias: "MsgChatStarted"),
        Msg("Chat vs the Admiral", "msgChatSinks", "🔥 {team} SINKS {admiral}'s {ship} at {coord}! {enemyleft} enemy ships left.", "Chat sinks a ship", "Played for fun: nothing is paid.", new[] { "team", "admiral", "ship", "coord", "enemyleft" }, alias: "MsgChatSinks"),
        Msg("Chat vs the Admiral", "msgChatLosesShip", "☠️ {admiral} sinks {team}'s {ship} at {coord}! {yourleft} ships left.", "Chat loses a ship", "Played for fun: nothing is charged.", new[] { "team", "admiral", "ship", "coord", "yourleft" }, alias: "MsgChatLosesShip"),
        Msg("Chat vs the Admiral", "msgChatWins", "🏆 {team} has sunk {admiral}'s entire fleet in {shots} shots — {voters} of you voted. Well played!", "Chat wins", "{voters} is how many viewers voted over the battle.", new[] { "team", "admiral", "shots", "voters" }, alias: "MsgChatWins"),
        Msg("Chat vs the Admiral", "msgChatLoses", "💀 {admiral} has sunk {team}'s fleet after {shots} shots. Better luck next time!", "Chat loses", "The Admiral sank the chat's fleet.", new[] { "team", "admiral", "shots", "voters" }, alias: "MsgChatLoses"),
        Msg("Chat vs the Admiral", "msgChatAbandoned", "⏰ Nobody voted for {seconds}s — {team} breaks off the battle against {admiral}.", "Chat stops voting", "Nobody voted for as long as the battle clock allows.", new[] { "team", "admiral", "seconds" }, alias: "MsgChatAbandoned"),
        Msg("Team duel", "msgDuelChallenge", "⚔️ {challenger} challenges {target} to a team duel! {target}, accept with !accept within {seconds}s. Entry: {fee} {points}.", "Challenge", "!duel with a name. Write your own command names into the duel messages.", new[] { "challenger", "target", "seconds", "fee", "points" }, alias: "MsgDuelChallenge"),
        Msg("Team duel", "msgDuelOpenChallenge", "⚔️ {challenger} calls for a team duel! Anyone can accept with !accept within {seconds}s. Entry: {fee} {points}.", "Open challenge", "!duel with no name: anybody may accept.", new[] { "challenger", "seconds", "fee", "points" }, alias: "MsgDuelOpenChallenge"),
        Msg("Team duel", "msgDuelAccepted", "⚔️ {target} accepts! Team A ({challenger}) against Team B ({target}). Join a side with !team A or !team B — {seconds}s to sign up. Entry: {fee} {points}.", "Challenge accepted", "Sign-up opens. {target} is whoever accepted.", new[] { "challenger", "target", "seconds", "fee", "points" }, alias: "MsgDuelAccepted"),
        Msg("Team duel", "msgDuelJoined", "➕ {user} joins {team} ({count} aboard).", "Player joins a side", "Leave it empty to keep a busy sign-up quiet.", new[] { "user", "team", "count" }, alias: "MsgDuelJoined"),
        Msg("Team duel", "msgDuelStart", "🚢 The duel begins: Team A ({a} players) against Team B ({b} players), pot {pot} {points}. {team} fires first — vote with !fire B7, one round of {round}s per shot.", "Duel begins", "{team} is the side that fires first, picked at random.", new[] { "team", "a", "b", "pot", "points", "round", "ships", "size" }, alias: "MsgDuelStart"),
        Msg("Team duel", "msgDuelTurn", "🎯 {team}'s turn — vote with !fire B7.", "Next side's turn", "Posted at the start of every round after the first. Misses and hits use the solo messages, with the sides as {user} and {admiral}.", new[] { "team", "round" }, alias: "MsgDuelTurn"),
        Msg("Team duel", "msgDuelTurnPassed", "⌛ {team} lets its turn pass ({missed} of {max}).", "Side lets its turn pass", "Nobody on that side voted.", new[] { "team", "missed", "max" }, alias: "MsgDuelTurnPassed"),
        Msg("Team duel", "msgDuelSunk", "🔥 {team} SINKS {enemy}'s {ship} at {coord}! {enemyleft} of their ships left.", "Side sinks a ship", "Posted when a side sinks one of the other side's ships.", new[] { "team", "enemy", "ship", "coord", "enemyleft" }, alias: "MsgDuelSunk"),
        Msg("Team duel", "msgDuelStatus", "🎯 {teama}: {aleft} ships afloat — {teamb}: {bleft} ships afloat. {turn}'s turn, {shots} shots so far, pot {pot} {points}.", "Duel status", "Posted by !board during a duel.", new[] { "teama", "teamb", "aleft", "bleft", "turn", "shots", "pot", "points" }, alias: "MsgDuelStatus"),
        Msg("Team duel", "msgDuelWin", "🏆 {team} wins the duel in {shots} shots! {pot} {points} shared between {players} players — {share} each.", "Side wins", "{share} is each winner's share, their own entry included, before the Addicted cut and the daily cap.", new[] { "team", "shots", "pot", "points", "players", "share" }, alias: "MsgDuelWin"),
        Msg("Team duel", "msgDuelWinFree", "🏆 {team} wins the duel in {shots} shots!", "Side wins (nothing staked)", "Free game, or an entry fee of 0.", new[] { "team", "shots" }, alias: "MsgDuelWinFree"),
        Msg("Team duel", "msgDuelForfeit", "🏳️ {team} let {turns} turns pass in a row and gives up the duel.", "Side gives up", "It let too many turns pass in a row. The win message follows.", new[] { "team", "winner", "turns" }, alias: "MsgDuelForfeit"),
        Msg("Team duel", "msgDuelExpired", "⌛ Nobody took up {challenger}'s challenge — the entry is refunded.", "Challenge not taken up", "The challenger's entry is refunded.", new[] { "challenger", "target" }, alias: "MsgDuelExpired"),
        Msg("Team duel", "msgDuelCancelled", "⌛ Neither side fired a shot — the duel is called off and every entry is refunded.", "Duel called off", "Neither side ever voted. Every entry is refunded.", new string[0], alias: "MsgDuelCancelled"),
        Msg("Moderator stop", "msgModStop", "🛑 {user} has stopped {game}'s battle — nothing more is paid out.", "Battle stopped", "{user} is the moderator, {game} the player or the chat's team name. Nothing more is paid out.", new[] { "user", "game", "admiral" }, alias: "MsgModStop"),
        Msg("Moderator stop", "msgModStopDuel", "🛑 {user} has stopped the duel — nothing is paid out, every entry is refunded.", "Duel stopped", "Every entry is refunded.", new[] { "user" }, alias: "MsgModStopDuel"),
        Msg("Moderator stop", "msgModStopNothing", "@{user}, there is no battle or duel to stop.", "Nothing to stop", "The stop command with no battle or duel running.", new[] { "user" }, alias: "MsgModStopNothing"),
        Msg("Click to Fire", "msgHeatAutoHint", "🖱️ {user}, you can also fire by clicking enemy waters on stream — your first click there takes control. Did someone else's click get in first? Click once more, then type !fire link.", "How to fire by clicking", "Sent to a Twitch player without a link when their battle comes on screen, while the first click takes control. Leave it empty to send nothing.", new[] { "user", "admiral" }, alias: "MsgHeatAutoHint"),
        Msg("Click to Fire", "msgHeatLinkHint", "🖱️ {user}, you can also fire by clicking enemy waters on stream: click any square, then type !fire link. Only once — after that your clicks fire on their own.", "How to link", "Sent at the same moment when the first click doesn't take control, so every player links with !fire link.", new[] { "user", "admiral" }, alias: "MsgHeatLinkHint"),
        Msg("Click to Fire", "msgHeatLinked", "🖱️ {user}, your clicks are linked — from now on just click enemy waters on stream to fire.", "Linked", "Confirms !fire link. The click that made the link fires straight after.", new[] { "user", "admiral" }, alias: "MsgHeatLinked"),
        Msg("Click to Fire", "msgHeatLinkNoClick", "@{user}, no click on enemy waters reached me in the last {seconds} seconds. Click a square on stream first, then type !fire link.", "Link: no click seen", "!fire link found no click on enemy waters in the last seconds.", new[] { "user", "seconds" }, alias: "MsgHeatLinkNoClick"),
        Msg("Click to Fire", "msgHeatLinkAmbiguous", "@{user}, more than one person clicked enemy waters just now, so I can't tell which clicks are yours. Click again in a moment, then type !fire link.", "Link: more than one clicker", "Several viewers clicked in that time, so the link is refused rather than guessed.", new[] { "user", "seconds" }, alias: "MsgHeatLinkAmbiguous"),
        Msg("Errors", "msgBadCoord", "@{user}, '{input}' is not a square on this board. Use a letter and a number, like B7 (A1 to {max}).", "Not a square on the board", "!fire with something that isn't a square, like 'Z9'.", new[] { "user", "input", "max" }, alias: "MsgBadCoord"),
        Msg("Errors", "msgNoCoord", "@{user}, name a square to fire at, like !fire B7", "No square given", "!fire on its own.", new[] { "user" }, alias: "MsgNoCoord"),
        Msg("Errors", "msgAlreadyFired", "@{user}, you have already fired at {coord}. Pick another square — this one is free.", "Already fired there", "A repeat shot never costs the turn or the clock.", new[] { "user", "coord" }, alias: "MsgAlreadyFired"),
        Msg("Errors", "msgNoGame", "@{user}, you have no battle running. Start one with !battleship", "No battle running", "!fire, !fleet or !board from a viewer with no battle.", new[] { "user" }, alias: "MsgNoGame"),
        Msg("Errors", "msgAlreadyInGame", "@{user}, you are already at sea. Fire with !fire B7.", "Already in a battle", "!battleship from a viewer whose battle is still running.", new[] { "user" }, alias: "MsgAlreadyInGame"),
        Msg("Errors", "msgFleetTooLate", "@{user}, the battle has already started — you can only redeploy before your first shot.", "Too late to redeploy", "!fleet after the first shot.", new[] { "user" }, alias: "MsgFleetTooLate"),
        Msg("Errors", "msgNotEnoughPoints", "@{user}, you need at least {required} {points} to put a fleet to sea. You have {balance}.", "Not enough points", "Below the minimum balance on the Game page, or short of a duel's entry fee.", new[] { "user", "required", "balance", "points" }, alias: "MsgNotEnoughPoints"),
        Msg("Errors", "msgModeDisabled", "@{user}, that game mode is switched off.", "Game mode switched off", "A mode switched off on the Modes page.", new[] { "user" }, alias: "MsgModeDisabled"),
        Msg("Errors", "msgChatBusy", "@{user}, a battle is already running — {team} can take on {admiral} once it is over.", "Chat battle has to wait", "The chat's battle starts only when no other battle is running.", new[] { "user", "team", "admiral" }, alias: "MsgChatBusy"),
        Msg("Errors", "msgMaxGamesPerHour", "@{user}, you have hit the limit of {max} battles per hour.", "Too many battles this hour", "The limit on the Access page.", new[] { "user", "max" }, alias: "MsgMaxGamesPerHour"),
        Msg("Errors", "msgDailyCapReached", "@{user}, you have reached today's winnings cap of {cap} {points}. Further wins pay nothing until tomorrow.", "Daily winnings cap reached", "The cap on the Game page.", new[] { "user", "cap", "points" }, alias: "MsgDailyCapReached"),
        Msg("Duel errors", "msgDuelBusy", "@{user}, a battle or a duel is already on — try again once it is over.", "Duel has to wait", "A duel is challenged only while nothing else runs, and nothing else starts while one does.", new[] { "user" }, alias: "MsgDuelBusy"),
        Msg("Duel errors", "msgDuelNoChallenge", "@{user}, there is no challenge waiting for you.", "No challenge to accept", "Nothing is waiting, it is somebody else's challenge, it is the caller's own, or its time has run out.", new[] { "user" }, alias: "MsgDuelNoChallenge"),
        Msg("Duel errors", "msgDuelNotJoining", "@{user}, no duel is taking players right now.", "Sign-up isn't open", "The join command while no duel takes players.", new[] { "user" }, alias: "MsgDuelNotJoining"),
        Msg("Duel errors", "msgDuelAlreadyJoined", "@{user}, you are already in {team}.", "Already on a side", "The join command from somebody who already joined.", new[] { "user", "team" }, alias: "MsgDuelAlreadyJoined"),
        Msg("Duel errors", "msgDuelTeamFull", "@{user}, {team} already has {gap} more players than {other} — join {other} instead.", "Side too far ahead", "Only with a lead limit on the Modes page.", new[] { "user", "team", "other", "gap" }, alias: "MsgDuelTeamFull"),
        Msg("Duel errors", "msgDuelBadTeam", "@{user}, pick a side: !team A or !team B — or just !team to fill the smaller one.", "No such side", "The join command named neither A nor B nor anybody on a side.", new[] { "user" }, alias: "MsgDuelBadTeam"),
        Msg("Duel errors", "msgDuelNotInDuel", "@{user}, only the two teams vote in this duel.", "Not in the duel", "A !fire vote from somebody on neither side.", new[] { "user" }, alias: "MsgDuelNotInDuel"),
        Msg("Duel errors", "msgDuelNotYourTurn", "@{user}, it is {team}'s turn.", "Not this side's turn", "{team} is the side whose turn it is.", new[] { "user", "team" }, alias: "MsgDuelNotYourTurn"),
        Msg("Whisper", "msgWhisperFleet", "Your fleet is deployed: {fleet}. The Admiral cannot read this — or your chat. Good hunting!", "Your fleet", "Whispered to a solo player when their battle starts, while the whisper is on.", new[] { "user", "fleet" }, alias: "MsgWhisperFleet"),
        Msg("Cooldown", "globalCooldownMessage", "Battleship is on global cooldown. Try again in {seconds} seconds.", "Everyone", "Sent while the cooldown for everyone runs.", new[] { "seconds", "user" }, alias: "GlobalCooldownMsg"),
        Msg("Cooldown", "userCooldownMessage", "@{user}, you're on cooldown. Try again in {seconds} seconds.", "Per viewer", "Sent while this viewer's own cooldown runs.", new[] { "user", "seconds" }, alias: "UserCooldownMsg"),
        Msg("Stats", "msgNoStatsFound", "@{user}: no battles fought yet. Start one with !battleship", "No record yet", "!bstats for a player with no battles.", new[] { "user" }, alias: "MsgNoStatsFound"),
        Msg("Stats", "msgNoLeaderboard", "No battle records yet.", "Leaderboard empty", "!ballstats before anybody has a record.", new string[0], alias: "MsgNoLeaderboard"),
    };
}

/// <summary>How the window is called and laid out, declared beside the table: what the framework's docs call the
/// spec (docs/Schema.md, "The window"). Every part is optional; a page a setting names but the window does not is
/// made up on the way, help and about always sit at the bottom.</summary>
public sealed class UiWindow
{
    public string Title, Releases, Logo, LogPrefix, Language;
    public double? Width, Height;
    public readonly List<object> PageList = new List<object>();
    public readonly JObject SampleMap = new JObject(), AnswerMap = new JObject(), OverrideMap = new JObject();
    public JObject SenderMap, LogMap, AboutMap;
    public readonly JArray PillList = new JArray(), CooldownList = new JArray(), HelpList = new JArray();
    public readonly JObject LangMap = new JObject();

    /// <param name="title">Window title and About heading.</param>
    /// <param name="releases">"owner/repo" on GitHub: the repository and releases links, the update check.</param>
    /// <param name="logo">URL of the logo, for the window icon and About.</param>
    public UiWindow(string title, string releases = null, string logo = null, string logPrefix = null, double? width = null, double? height = null)
    {
        Title = title; Releases = releases; Logo = logo; LogPrefix = logPrefix; Width = width; Height = height;
    }

    /// <summary>The game's own texts in another language, laid over the conventions' when the window is in it:
    /// UiSchema.K (a key's label and help), Pg (a page's title by id), Tx (a declared text by its English wording:
    /// a section, a pill text, a page title, an option label, a message group). Several calls add up.</summary>
    public UiWindow Lang(string code, params UiText[] texts)
    {
        var lang = LangMap[code] as JObject;
        if (lang == null) LangMap[code] = lang = new JObject();
        foreach (var t in texts)
        {
            var section = lang[t.Section] as JObject;
            if (section == null) lang[t.Section] = section = new JObject();
            section[t.Name] = t.Value;
        }
        return this;
    }

    /// <summary>The pages in order: a UiPage (UiSchema.P) or the id of a standard page ("messages", "log", "help", "about").</summary>
    public UiWindow Pages(params object[] pages) { PageList.AddRange(pages); return this; }

    /// <summary>The Log page: the folder (relative to the Streamer.bot folder) and the files in it, "BJG-log*.txt";
    /// the bool setting behind "Write a log file per day"; the columns after Time and Result as "Header|field" or a
    /// field; the values of the Result filter.</summary>
    public UiWindow Log(string folder, string files, string writeLogKey = null, string writeLogHelp = null, string[] columns = null, string[] results = null,
        string[] platforms = null, string emptyText = null, string writeLogLabel = null)
    {
        LogMap = new JObject { ["folder"] = folder, ["files"] = files };
        if (writeLogKey != null) LogMap["writeLogKey"] = writeLogKey;
        if (writeLogLabel != null) LogMap["writeLogLabel"] = writeLogLabel;
        if (writeLogHelp != null) LogMap["writeLogHelp"] = writeLogHelp;
        if (emptyText != null) LogMap["emptyText"] = emptyText;
        if (columns != null)
        {
            var list = new JArray();
            foreach (var column in columns)
            {
                int bar = column.IndexOf('|');
                list.Add(bar < 0 ? (JToken)column : new JObject { ["header"] = column.Substring(0, bar), ["field"] = column.Substring(bar + 1) });
            }
            LogMap["columns"] = list;
        }
        if (results != null) LogMap["results"] = new JArray(results);
        if (platforms != null) LogMap["platforms"] = new JArray(platforms);
        return this;
    }

    /// <summary>Commands whose cooldowns follow settings (UiSchema.C): the settings action sets them after every save.</summary>
    public UiWindow Cooldowns(params string[][] cooldowns)
    {
        foreach (var c in cooldowns)
            CooldownList.Add(new JObject { ["command"] = c[0], ["global"] = c.Length > 1 ? c[1] : null, ["user"] = c.Length > 2 ? c[2] : null });
        return this;
    }

    /// <summary>A pill in the window's header that follows a bool setting.</summary>
    public UiWindow Pill(string key, string on, string off = null, string tooltip = null)
    {
        var pill = new JObject { ["key"] = key, ["on"] = on };
        if (off != null) pill["off"] = off;
        if (tooltip != null) pill["tooltip"] = tooltip;
        PillList.Add(pill);
        return this;
    }

    /// <summary>The keys of the sender switches on the Messages page, where they differ from the conventions'
    /// (useBot, sendAction, cooldownUseBot, cooldownSendAction, numberSeparator). Null hides one.</summary>
    public UiWindow Senders(string useBot = "", string sendAction = "", string cooldownUseBot = "", string cooldownSendAction = "", string numberSeparator = "", string cooldownGroup = "")
    {
        SenderMap = SenderMap ?? new JObject();
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
        JToken value;
        try { value = answer != null && answer.TrimStart().StartsWith("{") ? JObject.Parse(answer) : (JToken)answer; }
        catch (Exception) { value = answer; }
        AnswerMap[action] = value;
        return this;
    }

    /// <summary>What the window changes about one setting of the table, where the table itself should not say it.</summary>
    public UiWindow Override(string key, string page = null, string label = null, string help = null, string section = null, bool? hidden = null, int? order = null)
    {
        var o = new JObject();
        if (page != null) o["page"] = page;
        if (label != null) o["label"] = label;
        if (help != null) o["help"] = help;
        if (section != null) o["section"] = section;
        if (hidden.HasValue) o["hidden"] = hidden.Value;
        if (order.HasValue) o["order"] = order.Value;
        OverrideMap[key] = o;
        return this;
    }

    /// <summary>The Help page's sections (UiSchema.H).</summary>
    public UiWindow Help(params UiSection[] sections)
    {
        foreach (var section in sections) HelpList.Add(section.ToJson());
        return this;
    }

    /// <summary>The About page: the credits line, sections like help, links; the Streamer.bot and author links are on unless turned off.</summary>
    public UiWindow About(string credits = null, UiSection[] sections = null, UiLine[] links = null, bool streamerBotLink = true, bool authorLink = true)
    {
        AboutMap = new JObject();
        if (credits != null) AboutMap["credits"] = credits;
        if (sections != null) AboutMap["sections"] = new JArray(sections.Select(s => s.ToJson()));
        if (links != null) AboutMap["links"] = new JArray(links.Select(l => new JObject { ["text"] = l.Json["link"], ["url"] = l.Json["url"] }));
        if (!streamerBotLink) AboutMap["streamerBotLink"] = false;
        if (!authorLink) AboutMap["authorLink"] = false;
        return this;
    }

    public JObject ToJson()
    {
        var root = new JObject { ["title"] = Title };
        if (Releases != null) root["releases"] = Releases;
        if (Logo != null) root["logo"] = Logo;
        if (LogPrefix != null) root["logPrefix"] = LogPrefix;
        if (Language != null) root["language"] = Language;
        if (Width.HasValue) root["width"] = Width.Value;
        if (Height.HasValue) root["height"] = Height.Value;
        if (PageList.Count > 0) root["pages"] = new JArray(PageList.Select(p => p is UiPage page ? page.ToJson() : (JToken)p.ToString()));
        if (OverrideMap.Count > 0) root["overrides"] = OverrideMap;
        if (SenderMap != null) root["senders"] = SenderMap;
        if (SampleMap.Count > 0) root["samples"] = SampleMap;
        if (AnswerMap.Count > 0) root["answers"] = AnswerMap;
        if (PillList.Count > 0) root["headerPills"] = PillList;
        if (CooldownList.Count > 0) root["cooldowns"] = CooldownList;
        if (LogMap != null) root["log"] = LogMap;
        if (HelpList.Count > 0) root["help"] = HelpList;
        if (AboutMap != null) root["about"] = AboutMap;
        return root;
    }
}

/// <summary>A page of the window: its id, title and icon (an emoji, or a conventions name like "gear"), and the
/// settings it claims ahead of their own page (keys, or patterns like "overlay*").</summary>
public sealed class UiPage
{
    public string Id, Title, Icon; public string[] Fields; public bool Custom, Bottom;
    public UiPage(string id, string title, string icon, string[] fields) { Id = id; Title = title; Icon = icon; Fields = fields; }
    public JObject ToJson()
    {
        var page = new JObject { ["id"] = Id };
        if (Title != null) page["title"] = Title;
        if (Icon != null) page["icon"] = Icon;
        if (Fields != null && Fields.Length > 0) page["fields"] = new JArray(Fields);
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
    public JObject ToJson() => new JObject { ["heading"] = Heading, ["lines"] = new JArray(Lines.Select(l => l is UiLine line ? line.Json : (JToken)l.ToString())) };
}

/// <summary>One translated text for UiWindow.Lang: the section of the language file it goes into, its name there, its value.</summary>
public sealed class UiText
{
    public readonly string Section, Name; public readonly JToken Value;
    public UiText(string section, string name, JToken value) { Section = section; Name = name; Value = value; }
}

public sealed class UiLine
{
    public readonly JObject Json;
    public UiLine(JObject json) { Json = json; }
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
    public static bool Handle(IDictionary<string, object> args, IInlineInvokeProxy CPH, string actionName, string host, string version, string settingsFile,
        Func<string, UiSettings, string> test = null, Func<UiSettings, string> validate = null, Action<UiSettings> saved = null,
        Func<string, string> table = null, Func<string> emotes = null)
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
        catch (Exception ex)
        {
            CPH.LogError("[" + actionName + "] The settings window's request '" + request + "' failed: " + ex.Message);
            answer = request == "saved" ? null : "The game could not answer: " + ex.Message;
        }
        CPH.SetGlobalVar(string.IsNullOrEmpty(var) ? AnswerVar(actionName) : var, answer ?? "", false);
        return true;
    }

    private static string Arg(IDictionary<string, object> args, string key) => args != null && args.TryGetValue(key, out var value) ? value as string : null;
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
        catch (Exception) { return false; }
        if (string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            var settings = JObject.Parse(json);
            string variable = (string)settings[variableKey];
            if (string.IsNullOrWhiteSpace(variable)) return false;
            Variable = variable.Trim();
            string name = (string)settings[nameKey];
            if (!string.IsNullOrWhiteSpace(name)) Name = name.Trim();
            Source = source;
            return true;
        }
        catch (Exception ex)
        {
            CPH.LogWarn("[UIFramework] The settings of " + source + " (" + global + ") could not be read: " + ex.Message);
            return false;
        }
    }

    public JObject ToJson() => new JObject { ["source"] = Source, ["variable"] = Variable, ["name"] = Name };
}

/// <summary>The settings file as the game reads it: every key through its default, old key names tolerated. Values
/// an alias stored in another scale (a fraction where percent is stored now) are converted on the way.</summary>
public sealed class UiSettings
{
    private readonly JObject _root;

    private UiSettings(JObject root) { _root = root; }

    /// <summary>Settings from JSON text, for tests and callers that already hold the values; empty text is empty settings.</summary>
    public static UiSettings Parse(string json)
    {
        try
        {
            using (var reader = new JsonTextReader(new StringReader(string.IsNullOrWhiteSpace(json) ? "{}" : json)) { DateParseHandling = DateParseHandling.None })
                return new UiSettings(JObject.Load(reader));
        }
        catch (Exception)
        {
            return new UiSettings(new JObject());
        }
    }

    /// <summary>The values as they are, for a host that reads them its own way.</summary>
    public JObject Raw => _root;

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
            catch (IOException) when (attempt < 5) { Thread.Sleep(40); }
        }
    }

    public JToken Token(string key)
    {
        var token = _root[key];
        if (token != null && token.Type != JTokenType.Null) return token;
        var setting = UiSchema.Find(key);
        if (setting?.Aliases != null)
            for (int i = 0; i < setting.Aliases.Length; i++)
            {
                // An alias with a dot reaches into an object of the old file ("PlatformEmotes.twitch").
                string alias = setting.Aliases[i];
                int dot = alias.IndexOf('.');
                var old = dot > 0 ? (_root[alias.Substring(0, dot)] as JObject)?[alias.Substring(dot + 1)] : _root[alias];
                if (old == null || old.Type == JTokenType.Null) continue;
                if (i == 0 && setting.AliasFactor.HasValue && (old.Type == JTokenType.Integer || old.Type == JTokenType.Float))
                    return Convert.ToDouble(((JValue)old).Value, CultureInfo.InvariantCulture) * setting.AliasFactor.Value;
                return old;
            }
        return setting?.Default == null ? null : JToken.FromObject(setting.Default);
    }

    public bool Bool(string key) { var t = Token(key); return t != null && t.Type == JTokenType.Boolean && (bool)t; }

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

    /// <summary>A ceiling that stores "no limit" as 0: read as long.MaxValue.</summary>
    public long Ceiling(string key) { long l = Long(key); return l <= 0 ? long.MaxValue : l; }
    public int CeilingInt(string key) { long l = Long(key); return l <= 0 || l > int.MaxValue ? int.MaxValue : (int)l; }
    public static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;
    public static long Clamp(long value, long min, long max) => value < min ? min : value > max ? max : value;
    public static double Clamp(double value, double min, double max) => value < min ? min : value > max ? max : value;
}
// --- end UIFramework host block ---
