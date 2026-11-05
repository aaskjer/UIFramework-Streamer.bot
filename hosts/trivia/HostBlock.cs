// --- UIFramework host block (keep identical in every action of this host that reads the settings) ---
// From UIFramework/stub/HostSchema.cs (tools/update-host-block.ps1 refreshes it). The game declares its settings
// once in UiSchema.Table and its window in UiSchema.Window, reads the settings through UiSettings, and lets
// UiHost.Handle answer the settings window's requests. One table, so the game and its window can never disagree.
// Compiles in any Streamer.bot action with no reference beyond Newtonsoft.Json.
//
// In Execute(), first thing:
//     if (UiHost.Handle(args, CPH, GameActionName, "trivia", CurrentVersion, SettingsFileRelative, test: WindowTest, validate: WindowAnswers.Validate, saved: WindowSaved))
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
    public static readonly UiWindow Window = new UiWindow("Trivia Game", releases: "aaskjer/Trivia-Game", logo: "https://github.com/aaskjer/Trivia-Game/blob/main/Assets/TG/Trivia-Game.png?raw=true", logPrefix: "[Trivia Game/Settings]", width: 860.0, height: 720.0)
        .Pages(P("game", "Game", "❓"), P("questions", "Questions", "📝"), P("sources", "Sources", "🌐"), P("suggestions", "Suggestions", "💡"), P("access", "Access", "👥"), P("messages", "Messages", "💬"), P("overlay", "Overlay", "📺"), P("log", "Log", "📜"), P("help", "How to play", "📖"), P("about", "About", "ℹ️"))
        .Senders(cooldownGroup: "Cooldown")
        .Pill("overlayEnabled", "Overlay on", "Overlay off", tooltip: "Whether rounds reach the OBS overlay, from the Overlay page.")
        .Pill("autoStartEnabled", "Auto start on", "Auto start off", tooltip: "Whether rounds start themselves on Streamer.bot's timed action, from the Game page.")
        .Pill("isChannelPointsTrigger", "Channel points", "Chat command", tooltip: "How rounds start on Twitch, from the Access page. YouTube and Kick always use the chat command.")
        .Samples("userName", "mirabel_tv", "targetUser", "kjell_plays", "winners", "@mirabel_tv, @nova_rae", "answer", "Ada Lovelace", "correctAnswer", "Ada Lovelace", "index", "2",
                 "difficulty", "MEDIUM", "category", "Science: Computers", "categories", "history, music, film", "seconds", "20", "minutes", "15", "points", "2.159", "pointsName", "Points",
                 "total", "18.420", "rounds", "3", "current", "2", "remaining", "2", "requested", "9", "count", "3", "max", "200", "answerMax", "60",
                 "rank", "1", "user", "mirabel_tv", "wins", "42", "blitzwins", "17", "blitzpoints", "9.300", "topn", "5", "platform", "Twitch", "target", "kjell_plays", "medal", "🥇")
        .Answer("checkSource", "opentdb answered - questions can be fetched.")
        .Answer("difficulty", "{\"set\":{\"answerTimeSeconds\":60,\"normalCountdownStartSeconds\":30,\"blitzAnswerTimeSeconds\":20,\"blitzCountdownStartSeconds\":10,\"fuzzyTolerance\":2,\"minLengthForFuzzy\":4},\"status\":\"Standard filled in; save to keep it.\"}")
        .Answer("setupObsOverlay", "OBS is not connected on connection #0. Connect it in Streamer.bot under Stream Apps, OBS Studio, then try again.")
        .Answer("overlaySample", "Sent. If nothing shows in OBS within a few seconds, check that Streamer.bot's WebSocket server is running on the port above.")
        .Answer("copyOverlayUrl", "Copied. Paste it into the URL of a Browser source in OBS.")
        .Answer("overlayDemo", "The demo opened in your browser. In OBS, add &demo=1 to the URL to place the source the same way.")
        .Answer("openOverlayFolder", "Opened Chat Games\\Trivia Game\\Overlay.")
        .Answer("openQuestionsFolder", "Opened Chat Games\\Trivia Game.")
        .Log("Chat Games/Logs", "Trivia-log*.txt",
            columns: new[] { "User|User", "Platform|Platform", "Round|RoundId", "Blitz|Blitz", "Source|Source", "Answer|Answer", "Match|Match", "Correct|CorrectAnswer", "Change|Change", "Total|Total", "Reason|Reason" },
            results: new[] { "AUTO-START", "ROUND-START", "ROUND-END", "ANSWER", "ANSWER-DUP", "WIN", "BLITZ-WIN", "NO-WINNER", "AWARD", "SERIES", "FORCE-END", "SUGGEST", "ADDICTED", "REFUND", "STALE-TIMEOUT", "CACHE", "SOURCE-LOAD" },
            platforms: new[] { "twitch", "youtube", "kick" },
            emptyText: "No rounds logged yet. Every round, answer, payout and suggestion lands here; one Trivia-log-YYYY-MM-DD.txt per day, kept until you delete it.")
        .Help(
            H("A round",
                Note("A question is posted with its answer options numbered. Viewers answer in chat before the timer runs out. In a normal round everyone who got it right wins; in a blitz round the first correct answer ends it immediately."),
                Cmd("!trivia", "starts a round. Restricted to moderators while 'Only moderators may start' is on, and replaced by a channel point redemption while rounds start from channel points."),
                Cmd("!trivia <category>", "starts a round on a named category: '!trivia history', '!trivia video games'. Short forms work too: tv, movies, anime, maths. With Wikidata as the source the topics are geography, science, art, literature, film and music. Needs 'Let chat pick the category' on the Game page."),
                Cmd("!trivia <n> <category>", "both at once: '!trivia 3 science' plays three science rounds. The category sticks for the whole run."),
                Cmd("<answer>", "just type in chat: the option number, the option text, a near-enough spelling, whichever of those you allow on the Game page."),
                Cmd("!suggest q | correct | wrong | wrong", "adds a question of the viewer's own to the pool, guarded by a channel point reward or a Streamer.bot group. Set it up on the Suggestions page."),
                Cmd("!skiptrivia", "throws the current question away without scoring it. The answer is not revealed."),
                Cmd("!forceendtrivia", "ends the round early but still scores it: answers already submitted count, and the answer is revealed. Stops a back-to-back series too. Always mod-only."),
                Cmd("!ta add|remove <user>", "adds or removes a viewer from the half-payout list. Writes straight to the Access page."),
                Cmd("!triviascoreboard", "posts the top players for the platform the command came from."),
                Cmd("!tstats  ·  !tss <user>", "your own trivia stats, or somebody else's if you name them."),
                Note("These are the commands' default names; you may have bound them to other words in Streamer.bot.")),
            H("How answers are matched",
                Note("Each answer is checked against the options in a fixed order, and the first rule that matches wins. Only a viewer's first answer counts; answering again just gets a reminder of what they already submitted."),
                Cmd("1. Exact text", "the option typed out, ignoring capitalisation."),
                Cmd("2. Option number", "1, 2, 3 or 4. Skipped when an option is itself that number, so a question whose answers are numbers still behaves."),
                Cmd("3. Normalized text", "the same answer ignoring spacing, punctuation and case."),
                Cmd("4. Close spelling", "within the Fuzzy tolerance. The closest option wins, and a genuine tie is thrown away rather than guessed at. Numbers never count as close: 1995 is not 1994.")),
            H("For your own actions",
                Note("Every round fires a custom trigger (Custom > Chat Game > Trivia Game): Trivia: Round Started when the question goes out, then Trivia: Round Won, Trivia: Round Timeout, Trivia: Round Skipped or Trivia: Round ForceEnd when it ends, with the round as arguments: roundOutcome, roundPlatform, roundId, roundBlitz, roundQuestion, roundCorrectAnswer, roundCategory, roundDifficulty, roundSource, roundAnswers, roundWinners, roundWinnerCount, roundPoints and roundCurrency. GetLastRoundResult, run with an Execute C# Method sub-action on the game action, hands the same arguments to any other action.")),
            H("Where things are kept",
                Cmd("configs.json", "everything on these pages, the messages included, under Chat Games\\Trivia Game."),
                Cmd("Trivia-Questions.json", "your own questions, next to it; the Questions page edits it."),
                Cmd("Sources\\", "what Wikidata, OdeNet, PokeAPI and IGDB answered, kept so a round needs no new request."),
                Cmd("Overlay\\trivia-overlay.html", "the overlay page OBS loads, written whenever you copy its URL or save with the overlay on."),
                Cmd("Logs\\Trivia-log-YYYY-MM-DD.txt", "the action log the Log page reads, one file per day."),
                Cmd("uiframework.layout.json", "your layout of this window: theme, language, renamed or hidden pages and fields.")))
        .About(credits: "Responsible: aaskjer\nCredits: nate1280 / Streamer.bot, opentdb, Wikidata, OdeNet, PokeAPI, IGDB and YOU ♡",
            sections: new[]
            {
                H("Needs a points system",
                    Note("Winners are paid with the points of a currency system. These two are picked up without any setup:"),
                    Link("Currency Core by StreamUP", "https://streamup.tips/product/currency-core"),
                    Link("LOYALTY POINTS by tawmae", "https://tawmae.xyz/loyalty-points"),
                    Note("Anything else works too: name its variable on the Game page. With 'Pay out points' off, rounds are played for the answer alone.")),
                H("Question sources",
                    Link("opentdb, the Open Trivia Database", "https://opentdb.com/"),
                    Link("Wikidata", "https://www.wikidata.org/"),
                    Link("OdeNet, the Open German WordNet", "https://github.com/hdaSprachtechnologie/odenet"),
                    Link("PokeAPI", "https://pokeapi.co/"),
                    Link("IGDB", "https://www.igdb.com/"),
                    Note("opentdb is an independent open-source project used through its free API; if you like it, support it or contribute questions. Wikidata's facts are free for any use (CC0). OdeNet by Melanie Siegel and Francis Bond is licensed CC BY-SA 4.0, and the definitions shown in chat are taken from it. Pokémon data comes from PokeAPI; Pokémon and its names are trademarks of Nintendo, Creatures and GAME FREAK. Video game data is provided by IGDB.com. None of these projects is affiliated with or endorses Trivia Game."))
            },
            links: new[] { Link("Trivia Game on the Streamer.bot Discord", "https://discord.com/channels/834650675224248362/1519697548791644241/1519697548791644241") })
        .Cooldowns(C("0137a7a6-79b0-46a2-937f-71e23479c140", "globalCooldown", "userCooldown"))   // !trivia
        // German: the game's own keys and texts; sections, pages and the shared keys come from the conventions.
        .Lang("de",
                Pg("questions", "Fragen"),
                Pg("sources", "Quellen"),
                Pg("suggestions", "Vorschläge"),
                K("difficulty", "Schwierigkeit", "Setzt Antwortzeiten, Countdown-Erinnerungen und Schreibtoleranz auf einmal, von Entspannt (zwei Minuten und die lockerste Schreibprüfung) bis Brutal (eine halbe Minute, keine unscharfe Zuordnung). Jede dieser Zahlen lässt sich unten weiter von Hand ändern; Eigene sind deine eigenen Zahlen."),
                K("difficultyNote", null, "Eine Stufe des Reglers füllt die sechs Zahlen aus, für die sie steht; gespeichert wird erst, wenn du speicherst. Das Spiel liest nur die Zahlen, du kannst sie danach also feinjustieren."),
                K("answerTimeSeconds", "Antwortzeit", "Wie lange eine normale Runde Antworten annimmt, in Sekunden."),
                K("normalCountdownStartSeconds", "Erste Erinnerung bei", "Verbleibende Sekunden, bei denen der Chat die erste Countdown-Erinnerung bekommt. Die zweite kommt bei der Hälfte davon, die dritte bei der Hälfte davon, höchstens 5. Nur die Rundenlänge begrenzt das."),
                K("delayBetweenRoundsSeconds", "Pause zwischen Runden", "Die Pause zwischen dem Ergebnis einer Runde und der Frage der nächsten, in einer Serie."),
                K("defaultDifficulty", "Schwierigkeit", "Wie schwer die Fragen von opentdb, Wikidata, OdeNet, PokeAPI und IGDB sind. Deine eigenen Fragen werden auf der Seite Fragen gefiltert."),
                K("defaultQuestionType", "Fragetyp", "Welche Form von Frage von opentdb, Wikidata, OdeNet, PokeAPI und IGDB gezogen werden darf."),
                K("defaultCategoryId", "Kategorie-ID", "Die opentdb-Kategorie jeder Runde. 0 heißt jede Kategorie. Namen und Nummern stehen auf opentdb.com/api_config.php. Die anderen Quellen ignorieren sie."),
                K("allowCategoryChoice", "Chat darf die Kategorie wählen", "Zuschauer dürfen beim Start eine Kategorie nennen, etwa '!trivia history'. Angeboten werden nur Kategorien, aus denen eine Runde gezogen werden könnte."),
                K("showDetails", "Schwierigkeit und Kategorie zur Frage zeigen", "Aus, und die Fußzeile der Frage nennt nur die Antwortzeit."),
                K("autoStartNextRoundAfterSkip", "Nach einem Skip die nächste Runde starten", "Ein Skip holt eine neue Frage, statt den Lauf zu beenden. Eine gewählte Kategorie bleibt."),
                K("allowMultipleRounds", "Mehrere Runden nacheinander erlauben", "'!trivia 3' stellt drei Runden in die Reihe. Aus, und jeder Start spielt genau eine."),
                K("maxRoundsPerCommand", "Höchstens je Befehl", "Die Obergrenze für eine in den Startbefehl getippte Zahl."),
                K("blitzNote", null, "Eine Blitzrunde endet mit der ersten richtigen Antwort, statt die Uhr ablaufen zu lassen. Welche Runden eine werden, ist Zufall."),
                K("enableBlitzTrivia", "Blitzrunden erlauben", "Aus, und jede Runde läuft ihre volle Antwortzeit."),
                K("blitzChance", "Chance", "Wie oft eine Runde zur Blitzrunde wird, in Prozent."),
                K("blitzAnswerTimeSeconds", "Antwortzeit", "Wie lange eine Blitzrunde Antworten annimmt, in Sekunden."),
                K("blitzCountdownStartSeconds", "Erste Erinnerung bei", "Verbleibende Sekunden, bei denen eine Blitzrunde ihre erste Erinnerung bekommt."),
                K("matchingNote", null, "Alle vier aus, und keine Antwort kann je angenommen werden."),
                K("allowNumericAnswers", "Per Optionsnummer", "'2' tippen wählt die zweite Option."),
                K("allowExactTextAnswers", "Genau wie geschrieben", "Der Antworttext, ohne Groß- und Kleinschreibung."),
                K("allowNormalizedTextAnswers", "Ohne Satzzeichen und Akzente", "'Paris.' und 'paris' zählen beide."),
                K("allowFuzzyTextAnswers", "Ähnliche Schreibweisen", "Ein echter Tippfehler zählt noch, innerhalb der Toleranz darunter."),
                K("fuzzyTolerance", "Unschärfe-Toleranz", "Wie viele Zeichen abweichen dürfen, bevor eine Antwort abgelehnt wird."),
                K("minLengthForFuzzy", "Mindestlänge für Unschärfe", "Kurze Antworten werden nie unscharf zugeordnet. Halte das über der Toleranz, sonst zählen wirklich verschiedene Wörter."),
                K("autoStartNote", null, "Eine zeitgesteuerte Streamer.bot-Aktion bietet jeder Plattform nach ihrem eigenen Takt eine Runde an, jede beurteilt nach ihrem eigenen Chat und ihrer eigenen laufenden Runde."),
                K("autoStartEnabled", "Runden automatisch starten", "Schaltet die zeitgesteuerte Aktion in Streamer.bot von hier aus an und aus."),
                K("autoStartIntervalMinutes", "Alle (Minuten)", "Wie lange zwischen automatischen Runden. Ein laufender Timer behält sein altes Intervall, bis er aus- und wieder eingeschaltet wird."),
                K("autoStartRounds", "Runden je Durchgang", "Wie viele Runden ein Durchgang des Timers spielt. Immer eine, solange 'Mehrere Runden nacheinander erlauben' aus ist."),
                K("autoStartChatActivityMinutes", "Chat innerhalb (Minuten)", "Wie frisch der Chat sein muss, damit die automatische Runde stattfindet."),
                K("autoStartRequireChatActivity", "Nur wenn der Chat aktiv war", "Aus, und eine Runde wird nach Plan angeboten, ob jemand da ist oder nicht."),
                K("awardPoints", "Punkte auszahlen", "Aus, und Runden werden um nichts als die Antwort gespielt."),
                K("baseWinPoints", "Basisgewinn", "Was ein Gewinn vor den Multiplikatoren darunter zahlt."),
                K("pointsVariable", "Punkte-Variable", "Die Variable, in der deine Punkte liegen. Currency Core von StreamUP und LOYALTY POINTS von tawmae werden von selbst gefunden; ändere das nur für ein eigenes Punktesystem."),
                K("pointsName", "Im Chat genannt als", "Was {pointsName} im Chat sagt, etwa 'Coins' oder 'Gems'. Nur die Wortwahl ändert sich."),
                K("payoutNote", null, "Auf den Basisgewinn angewandt, je nach Frage. Die Online-Quellen kennen nur leicht, mittel und schwer; Sehr schwer gibt es für Fragen, die du selbst schreibst."),
                K("easyMultiplier", "Leicht", "Multiplikator für eine leichte Frage."),
                K("mediumMultiplier", "Mittel", "Multiplikator für eine mittlere Frage."),
                K("hardMultiplier", "Schwer", "Multiplikator für eine schwere Frage."),
                K("veryHardMultiplier", "Sehr schwer", "Multiplikator für eine sehr schwere eigene Frage."),
                K("blitzMultiplier", "Blitz", "Kommt bei einer Blitzrunde obendrauf."),
                K("subscriberBoostEnabled", "Abonnenten-Bonus", "Zahlt Abonnenten mehr. Boni addieren sich und gelten nach den Multiplikatoren oben."),
                K("subscriberBoostPercent", "Abonnenten", "Wie viel mehr ein Abonnent gewinnt, in Prozent."),
                K("vipBoostEnabled", "VIP-Bonus", "Zahlt VIPs mehr. Boni addieren sich und gelten nach den Multiplikatoren oben."),
                K("vipBoostPercent", "VIP", "Wie viel mehr ein VIP gewinnt, in Prozent."),
                K("addictedNote", null, "Zuschauer auf der Süchtigen-Liste bekommen die Hälfte dessen, was oben herauskommt. Die Liste ist auf der Seite Zugriff."),
                K("scoreboardTopN", "Angezeigte Plätze", "Wie viele Zuschauer die Bestenliste nennt."),
                K("broadcasterAllowed", "Den Broadcaster aufnehmen", "Aus, und dein eigenes Konto erscheint nie auf der Liste."),
                K("botAllowed", "Den Bot aufnehmen", "Aus, und dein Bot-Konto erscheint nie auf der Liste."),
                K("blockedFromBoard", "Von der Liste ausgeschlossen", "Konten, die von der Bestenliste bleiben, was die beiden Schalter oben auch sagen; andere Bots zum Beispiel."),
                K("resetNote", null, "Einen Reset einzuschalten scharft ihn nur: der erste Wisch kommt an der nächsten Monatsgrenze, nie mitten im Monat. Einen Tag, den der Monat nicht hat, den 31. im Februar, nimmt sein letzter Tag."),
                K("resetAllMonthly", "Jede Plattform monatlich zurücksetzen", "Setzt die Statistik jedes Zuschauers einmal im Monat auf allen drei Plattformen auf null."),
                K("resetTwitchMonthly", "Twitch", "Die Twitch-Liste monatlich zurücksetzen."),
                K("resetTwitchDay", "Tag", "Tag des Monats, an dem der Twitch-Reset kommt."),
                K("resetYouTubeMonthly", "YouTube", "Die YouTube-Liste monatlich zurücksetzen."),
                K("resetYouTubeDay", "Tag", "Tag des Monats, an dem der YouTube-Reset kommt."),
                K("resetKickMonthly", "Kick", "Die Kick-Liste monatlich zurücksetzen."),
                K("resetKickDay", "Tag", "Tag des Monats, an dem der Kick-Reset kommt."),
                K("questionsNote", null, "Eine Zeile je Frage: die Frage, ihre richtige Antwort, die falschen Antworten durch | getrennt (der Chat sieht sie gemischt mit der richtigen), eine Kategorie und eine Schwierigkeit (easy, medium, hard oder very hard). Vorgeschlagen von sagt, wer eine Frage aus dem Chat geschickt hat. Leere Zeilen fallen beim Speichern weg; Standardwerte wiederherstellen bringt die Beispielfragen zurück."),
                K("customQuestions", "Fragen", "Liegen in Trivia-Questions.json neben der Einstellungsdatei. Welche davon gezogen werden dürfen, steht darunter; die Fragenquelle auf der Seite Quellen entscheidet, ob sie überhaupt genutzt werden."),
                K("openQuestionsFolder", "Fragen-Ordner öffnen", "Öffnet den Ordner mit Trivia-Questions.json im Explorer."),
                K("ownFiltersNote", null, "Getrennt von den Filtern auf der Seite Spiel, damit das Einengen des einen den anderen nicht still leert."),
                K("customDifficulty", "Schwierigkeit", "Nur deine Fragen dieser Schwierigkeit werden gezogen."),
                K("customQuestionType", "Fragetyp", "Eine Frage zählt als Wahr/Falsch, wenn ihre einzigen zwei Optionen 'True' und 'False' sind."),
                K("customCategories", "Kategorien im Spiel", "Nur deine Fragen unter diesen Kategorien werden gezogen. Leer heißt alle."),
                K("questionSource", "Fragenquelle", "Woher die Frage jeder Runde kommt. Jede Quelle außer 'Meine + opentdb' ist die einzige, die eine Runde nutzt. opentdb: tausende englische Fragen in zwei Dutzend Kategorien. Wikidata: Fragen aus Wikidatas Fakten, Hauptstädte, Währungen, Elemente, Gemälde, Filme. OdeNet: deutscher Wortschatz, immer auf Deutsch. PokeAPI: Pokémon. IGDB: Videospiele, braucht eine eigene Twitch-Entwickleranwendung."),
                K("sourcesNote", null, "opentdb: tausende englische Fragen in zwei Dutzend Kategorien, gefiltert auf der Seite Spiel. Wikidata: Fragen aus Wikidatas Fakten (Hauptstädte, Währungen, Elemente, Monde, Gemälde, Bücher, Filme); der Chat kann ein Thema wählen. OdeNet: deutscher Wortschatz aus dem Open German WordNet, immer auf Deutsch; die Wortliste (etwa 4 MB) wird einmal heruntergeladen. PokeAPI: Pokémon-Typen, Generationen, Pokédex-Einträge und Entwicklungen. IGDB: Erscheinungsjahre, Studios und Plattformen von Videospielen; braucht eine eigene Twitch-Entwickleranwendung, siehe unten."),
                K("customQuestionChance", "Meine", "Nur bei 'Meine + opentdb': wie wahrscheinlich jede Runde aus deiner eigenen Liste kommt. 50 ist etwa jede zweite Runde."),
                K("cacheSize", "Vorrat", "Wie viele opentdb-Fragen auf einmal geholt und für spätere Runden behalten werden. Mehr heißt weniger API-Aufrufe. Die anderen Quellen behalten ohnehin auf der Platte, was sie holen, unter Chat Games\\Trivia Game\\Sources."),
                K("questionLanguage", "Fragensprache", "Die Sprache, in der Wikidata-, PokeAPI- und IGDB-Fragen geschrieben sind, Namen eingeschlossen, wo die Quelle sie hat. opentdb ist englisch und OdeNet deutsch, was auch hier steht; deine eigenen Fragen sind, wie du sie geschrieben hast."),
                K("customFallback", "Auf die andere Quelle ausweichen", "Kann die gewählte Quelle keine Frage liefern, eine leere Liste, Filter, die nichts treffen, oder ein opentdb, das nicht antwortet, wird die andere probiert, statt die Runde scheitern zu lassen. Nur bei 'Nur meine Fragen' und 'Meine + opentdb'."),
                K("igdbNote", null, "IGDB gehört Twitch und antwortet nur einer bei Twitch registrierten Anwendung. Registriere eine auf https://dev.twitch.tv/console/apps: beliebiger Name, http://localhost als OAuth-Redirect-URL, Kategorie Application Integration, Client-Typ Confidential. Dann die Client-ID hierher kopieren und mit New Secret ein Client Secret anlegen."),
                K("igdbClientId", "Client-ID", "Die Client-ID deiner Twitch-Entwickleranwendung."),
                K("igdbClientSecret", "Client Secret", "Das Client Secret dieser Anwendung. Es liegt in configs.json auf diesem PC und geht nur an Twitch, für IGDBs Zugangstoken."),
                K("checkSource", "Prüfen", "Fragt die oben gewählte Quelle, ob sie gerade antwortet: opentdb, Wikidata, PokeAPI, GitHub für OdeNets Wortliste, oder Twitch für die IGDB-Schlüssel. Nichts wird gespeichert."),
                K("suggestNote", null, "Zuschauer schreiben dir Fragen mit !suggest. Eine Nachricht trägt die ganze Frage, getrennt durch senkrechte Striche: !suggest What year did Twitch launch? | 2011 | 2007 | 2014, erst die Frage, dann die richtige Antwort, dann bis zu drei falsche."),
                K("allowQuestionSuggestions", "Fragenvorschläge erlauben", "Aus, und !suggest antwortet mit der Nachricht 'Vorschläge aus'."),
                K("whoSuggestsNote", null, "Ein Vorschlag wird angenommen von jemandem, der die Belohnung eingelöst hat, zu einer der Gruppen gehört oder Moderator ist, solange das an ist. Alle anderen bekommen die Nachricht 'Keine Berechtigung'."),
                K("suggestRewardId", "Belohnungs-ID", "Eine Kanalpunkte-Belohnung, mit der der Einlösende eine Frage vorschlagen darf. Nur Twitch. Leer lassen, um dich auf Gruppen und Moderatoren zu verlassen."),
                K("suggestAllowModerators", "Moderatoren dürfen immer vorschlagen", "Moderatoren und der Broadcaster können ohne Belohnung oder Gruppe vorschlagen."),
                K("suggestAllowedGroups", "Erlaubte Gruppen", "Mitglieder dieser Streamer.bot-Gruppen dürfen Fragen vorschlagen."),
                K("suggestCategory", "Abgelegt unter", "Die Kategorie, unter der vorgeschlagene Fragen auf der Seite Fragen abgelegt werden."),
                K("suggestDifficulty", "Schwierigkeit", "Die Schwierigkeit, mit der vorgeschlagene Fragen ankommen. Ändere sie später auf der Seite Fragen."),
                K("suggestMaxPerUser", "Höchstens je Zuschauer", "Wie viele Fragen eines Zuschauers zugleich im Pool liegen dürfen. Eine zu löschen macht einen Platz frei."),
                K("suggestMaxQuestionLength", "Fragenlänge", "Wie lang eine vorgeschlagene Frage höchstens sein darf, in Zeichen."),
                K("suggestMaxAnswerLength", "Antwortlänge", "Wie lang jede einzelne Antwort höchstens sein darf, in Zeichen."),
                K("suggestEnabledOnArrival", "Sofort spielbar", "Aus, und sie landen ausgeschaltet auf der Seite Fragen, damit du sie erst ansiehst."),
                K("arriveNote", null, "Was keine spielbare Frage ergäbe, wird abgewiesen statt gespeichert: eine fehlende Antwort, eine falsche Antwort gleich der richtigen, alles über den Längen oben und jede Frage, die schon im Pool ist. Was der Chat erfährt, steht auf der Seite Nachrichten."),
                K("platformsNote", null, "Twitch, YouTube und Kick spielen alle, und jede Antwort geht in den Chat, aus dem der Befehl oder die Einlösung kam. Um eine Plattform auszulassen, hake sie bei den Trivia-Befehlen in Streamer.bot ab, etwa 'Kick Message'."),
                K("isChannelPointsTrigger", "Kanalpunkte starten eine Runde", "Auf Twitch startet eine Einlösung der Belohnung darunter eine Runde statt !trivia, das dann auf Twitch aus ist. Einlösungen kommen nur von Twitch, auf YouTube und Kick starten Zuschauer Runden also weiter mit !trivia. Preis, Abklingzeiten und Texteingabe der Belohnung werden bei Twitch eingestellt, nicht hier."),
                K("targetRewardId", "Belohnungs-ID (eine Runde)", "Die ID der Kanalpunkte-Belohnung, die eine Runde spielt. Andere Belohnungen werden ignoriert. Ohne eine startet keine Einlösung eine Runde. https://ayrtonalexis.com/reward_checker/reward_check.html listet deine Belohnungen mit ihren IDs."),
                K("multiRoundRewardId", "Belohnungs-ID (mehrere Runden)", "Eine optionale zweite Belohnung, die mehrere Runden aus einer Einlösung spielt. Leer lassen, wenn du nur einzelne Runden willst. Sie wird ignoriert, solange 'Mehrere Runden nacheinander erlauben' auf der Seite Spiel aus ist."),
                K("maxRoundsPerRedeem", "Höchstens je Einlösung", "Die Obergrenze für diese Belohnung. Schalte bei der Twitch-Belohnung 'Require Viewer to Enter Text' ein, und Zuschauer tippen die Zahl selbst, begrenzt hierauf. Lass es aus, und jede Einlösung spielt die Obergrenze."),
                K("refundFailedRedemptions", "Einlösungen erstatten, die keine Runde starten", "Eine laufende Runde, eine Abklingzeit oder keine Frage zu haben: die Einlösung wird abgebrochen und die Kanalpunkte gehen zurück. Geht nur, solange die Einlösung noch in der Warteschlange steht; eine Belohnung, die die Warteschlange überspringt, lässt sich nicht erstatten."),
                K("requireModeratorForStart", "Nur Moderatoren dürfen starten", "Aus, und jeder im Chat kann eine Runde starten. Der Broadcaster kann immer starten, was auch hier steht."),
                K("requireModeratorForSkip", "Nur Moderatoren dürfen überspringen", "Aus, und jeder im Chat kann die laufende Frage überspringen. Das erzwungene Ende bleibt immer Moderatoren vorbehalten."),
                K("globalCooldown", "Alle", "Wie lange alle warten, nachdem eine Runde gestartet wurde, etwa '60'. 0 schaltet es aus. Es bremst den Befehl, der einen Lauf startet; die Runden einer Serie laufen glatt durch."),
                K("userCooldown", "Je Zuschauer", "Wie lange ein Zuschauer wartet, bevor er eine weitere Runde startet, etwa '60'. 0 schaltet es aus."),
                K("cooldownNote", null, "0 schaltet eine Abklingzeit aus. Speichern schreibt beide auf den Befehl !trivia in Streamer.bot. Was der Chat derweil erfährt, steht auf der Seite Nachrichten."),
                K("blockedUsers", "Gesperrte Zuschauer", "Zuschauer, deren Antworten und Befehle ignoriert werden, ohne ein Wort zurück."),
                K("blockedGroups", "Gesperrte Gruppen", "Streamer.bot-Gruppen, deren Mitglieder ignoriert werden. Die Namen müssen den Gruppen genau entsprechen."),
                K("addictedUsers", "Süchtige Zuschauer", "Sie spielen weiter, bekommen aber nur je die Hälfte dessen, was sie gewonnen hätten. Der Befehl !TA bearbeitet diese Liste aus dem Chat."),
                K("addictedGroups", "Süchtige Gruppen", "Mitglieder dieser Streamer.bot-Gruppen zählen auch als süchtig."),
                K("announceEachAnswer", "Jede Antwort im Chat bestätigen", "Aus, und die Nachrichten unter Antworten werden nie gesendet. In einem vollen Chat ist das eine Zeile je Antwort."),
                K("overlayNote", null, "Eine Browserquelle, die die laufende Frage mit ihrem Countdown zeigt, dann die richtige Antwort, die Gewinner und die Bestenliste. Sie hört auf Streamer.bots eigenen WebSocket-Server, es ist also nichts weiter zu installieren."),
                K("overlayEnabled", "Runden ans Overlay senden", "Aus, und das Spiel sendet dem Overlay nichts, es bleibt also leer."),
                K("websocketNote", null, "Schalte ihn in Streamer.bot unter Servers/Clients, WebSocket Server, ein und hake Auto Start an. Port und Passwort müssen die dort gezeigten sein."),
                K("overlayPort", "Port", "Der Port von Streamer.bots WebSocket-Server. 8080, sofern du ihn dort nicht geändert hast."),
                K("overlayPassword", "Passwort", "Nur, wenn du beim WebSocket-Server die Authentifizierung eingeschaltet hast; sonst leer lassen. Es kommt in die URL des Overlays in OBS, weil das Overlay sich damit anmeldet."),
                K("obsNote", null, "Fügt der Szene darunter eine Browserquelle in Leinwandgröße hinzu, die aufs Overlay zeigt. OBS muss in Streamer.bot unter Stream Apps, OBS Studio verbunden sein. Lass die Szene leer, um die zu nehmen, die in OBS gerade live ist. Eine Quelle gleichen Namens wird wieder aufs Overlay gerichtet statt verdoppelt."),
                K("overlayObsConnection", "Verbindung Nr.", "Welche der OBS-Verbindungen von Streamer.bot genutzt wird, falls du mehr als eine hast. 0 ist die erste."),
                K("overlayObsScene", "Szene", "Die Szene, der die Quelle hinzugefügt wird. Sie wird angelegt, falls es sie noch nicht gibt. Leer: die Szene, die in OBS live ist, wenn du den Knopf drückst."),
                K("overlayObsSource", "Quelle", "Der Name der Browserquelle. Hat OBS schon eine Quelle dieses Namens, wird sie wieder aufs Overlay gerichtet."),
                K("setupObsOverlay", "In OBS einrichten", "Schreibt die Overlay-Seite und fügt dann die Browserquelle in OBS hinzu, oder richtet eine Quelle dieses Namens wieder aufs Overlay. Nutzt die gespeicherten Einstellungen, also erst speichern."),
                K("overlaySample", "Beispielrunde zeigen", "Spielt eine erfundene Runde auf jedem offenen Overlay, dem in OBS eingeschlossen, damit du siehst, wo es sitzt und wie es aussieht. Bist du live, sehen es auch deine Zuschauer. Nutzt die gespeicherten Einstellungen."),
                K("byHandNote", null, "Oder von Hand: eine Browserquelle anlegen, 'Lokale Datei' abhaken, die URL einfügen, die URL kopieren in die Zwischenablage legt, und die Größe auf deine Leinwand setzen, meist 1920 x 1080. Die Seite dahinter wird nach Chat Games\\Trivia Game\\Overlay geschrieben, wann immer du die URL kopierst oder mit eingeschaltetem Overlay speicherst."),
                K("copyOverlayUrl", "URL kopieren", "Schreibt die Overlay-Seite und kopiert ihre Adresse, Port und Passwort eingeschlossen, für eine Browserquelle in OBS."),
                K("overlayDemo", "Demo ansehen", "Öffnet das Overlay im Browser und spielt Beispielrunden, wie die gespeicherten Einstellungen es sagen, ohne Streamer.bot."),
                K("openOverlayFolder", "Overlay-Ordner öffnen", "Öffnet den Ordner mit der Overlay-Seite im Explorer."),
                K("overlayShowAnswerCount", "Wie viele geantwortet haben", "Zählt die Antworten, solange eine Runde läuft. Nur die Zahl, nie welche Option jemand gewählt hat."),
                K("overlayShowDetails", "Kategorie und Schwierigkeit", "Die Chips über der Frage. Getrennt von 'Schwierigkeit und Kategorie zur Frage zeigen' auf der Seite Spiel, das für den Chat ist, sodass jedes ohne das andere gehen kann."),
                K("overlayShowTimer", "Der Countdown", "Die Uhr und der ablaufende Balken. Aus, und die Runde endet trotzdem pünktlich, es wird nur nicht gezeigt."),
                K("overlayShowWinners", "Wer gewonnen hat", "Die Namen der Gewinner mit ihren Punkten unter dem Ergebnis. Aus, und das Ergebnis sagt nur, wie viele richtig lagen."),
                K("overlayShowLeaderboard", "Die Bestenliste nach einer Runde", "Lässt auf das Ergebnis die Spitze der Bestenliste folgen, gereiht wie !TL und mit denselben Ausnahmen."),
                K("overlayResultSeconds", "Ergebnis anzeigen für", "Wie lange Antwort und Gewinner stehen bleiben, und die Bestenliste danach."),
                K("overlayLeaderboardPlaces", "Plätze der Bestenliste", "Wie viele Zuschauer die Bestenliste des Overlays nennt, bis zu 10."),
                K("overlayPosition", "Frage", "Wo die Frage auf der Leinwand sitzt. Die Bestenliste erscheint immer oben rechts, nachdem die Frage weg ist."),
                K("overlayScale", "Größe", "Skaliert Frage und Bestenliste zusammen. 100 ist die normale Größe, von 50 bis 150."),
                K("overlayAnimations", "Animationen", "Das Ein- und Ausblenden von Frage, Ergebnis und Bestenliste. Aus, und sie erscheinen einfach."),
                K("overlayAccentColor", "Akzent", "Die Optionsnummern und der Countdown-Balken."),
                K("overlayCorrectColor", "Richtige Antwort", "Die richtige Option, die Gewinner und der Rand einer gewonnenen Runde."),
                K("overlayBlitzColor", "Blitz", "Rand, Chip und Countdown-Balken einer Blitzrunde."),
                K("overlayTextColor", "Text", "Der ganze Text. Chips, Optionskacheln und Linien werden auch darin gezeichnet, blass."),
                K("overlayPanelColor", "Fläche", "Der Hintergrund hinter Frage und Bestenliste."),
                K("overlayPanelOpacity", "Deckkraft der Fläche", "0 lässt deine Szene ganz durchscheinen, 100 ist deckend."),
                K("lookNote", null, "Gespeicherte Änderungen erreichen ein in OBS offenes Overlay sofort. Zeig eine Beispielrunde, um sie dort zu sehen."),
                K("cssNote", null, "Alles andere kommt ins Custom CSS der Browserquelle in OBS, das auch über die Farben oben gewinnt, etwa :root { --tg-width: 980px; --tg-radius: 8px; }. Häng &platform=twitch an die URL, um nur die Runden einer Plattform zu zeigen, und &debug=1, um zu sehen, ob es Streamer.bot erreicht hat."),
                K("questionPrefixNormal", "Präfix (normal)", "Erste Zeile der Fragenachricht einer normalen Runde."),
                K("questionPrefixBlitz", "Präfix (Blitz)", "Erste Zeile der Fragenachricht einer Blitzrunde. Sollte auffallen: es ist die einzige Warnung, dass die Runde mit der ersten richtigen Antwort endet."),
                K("questionOptionFormat", "Optionsformat", "Aufbau jeder Antwortoption, einmal je Option wiederholt."),
                K("questionFooterDetailed", "Fußzeile (ausführlich)", "Letzte Zeile der Fragenachricht, solange 'Schwierigkeit und Kategorie zur Frage zeigen' an ist."),
                K("categoryChosen", "Kategorie gewählt", "Einmal gesendet, wenn ein Zuschauer beim Start eine Kategorie genannt hat. Leer lassen, um still zu starten."),
                K("questionFooterSimple", "Fußzeile (einfach)", "Letzte Zeile der Fragenachricht, solange 'Schwierigkeit und Kategorie zur Frage zeigen' aus ist."),
                K("firstCountdownReminder", "Countdown 1", "Erste Zeit-Erinnerung, beim Wert 'Erste Erinnerung bei' des Rundentyps."),
                K("secondCountdownReminder", "Countdown 2", "Zweite Erinnerung, bei der Hälfte der ersten."),
                K("lastCountdownReminder", "Countdown 3", "Letzte Erinnerung, bei der Hälfte der zweiten, höchstens 5 Sekunden."),
                K("seriesStarted", "Serie gestartet", "Einmal gesendet, wenn ein Startbefehl mehrere Runden einreiht."),
                K("seriesRoundHeader", "Rundenkopf", "Direkt vor jeder Frage einer Serie gesendet, damit der Chat sieht, wie weit sie ist. Nie bei einer einzelnen Runde."),
                K("seriesFinished", "Serie beendet", "Einmal gesendet, nachdem die letzte Runde einer Serie aufgelöst ist."),
                K("seriesCancelled", "Serie abgebrochen", "Gesendet, wenn eingereihte Runden wegfallen: ein Moderator hat die Serie erzwungen beendet, oder es gab keine Frage."),
                K("seriesClamped", "Zu viele angefragt", "Gesendet, wenn jemand mehr Runden will, als 'Höchstens je Befehl' erlaubt. Die Serie läuft trotzdem, an der Grenze."),
                K("skipNotice", "Skip-Hinweis", "Gesendet, wenn eine Runde übersprungen wird. Die Frage wird ohne Wertung verworfen, die Antwort nicht verraten."),
                K("forceEndNotice", "Hinweis erzwungenes Ende", "Gesendet, wenn ein Moderator eine Runde erzwungen beendet. Anders als beim Skip zählen abgegebene Antworten, und die richtige wird verraten."),
                K("fetchFailed", "Abruf fehlgeschlagen", "Gesendet, wenn keine Frage zu haben war: Quelle nicht erreichbar oder gedrosselt, IGDB ohne Schlüssel, oder nichts passt zu Kategorie und Schwierigkeit. Das Streamer.bot-Protokoll sagt, was."),
                K("answerAccepted", "Antwort angenommen", "Gesendet, wenn eine Antwort normal notiert wird."),
                K("answerRecognizedVariation", "Abweichung erkannt", "Ersetzt 'Antwort angenommen', wenn die Antwort erst ohne Groß-/Kleinschreibung, Leerzeichen und Satzzeichen passte. Braucht 'Ohne Satzzeichen und Akzente' auf der Seite Spiel."),
                K("answerAcceptedFuzzy", "Angenommen (unscharf)", "Ersetzt 'Antwort angenommen', wenn die Antwort über eine ähnliche Schreibweise passte. Braucht 'Ähnliche Schreibweisen' auf der Seite Spiel."),
                K("alreadyAnswered", "Schon geantwortet", "Gesendet, wenn ein Zuschauer in einer Runde zweimal antwortet. Nur die erste Antwort zählt."),
                K("winners", "Gewinner (normale Runde)", "Am Ende einer normalen Runde gesendet, wenn mindestens ein Zuschauer richtig lag. Alle Richtigen gewinnen, das deckt also einen oder viele ab."),
                K("blitzWinner", "Gewinner (Blitzrunde)", "Gesendet, wenn eine Blitzrunde gewonnen wird. Blitz endet, sobald jemand richtig liegt, es gibt also nur je einen Gewinner."),
                K("noCorrectAnswer", "Niemand richtig", "Gesendet, wenn eine Runde endet und niemand richtig lag."),
                K("pointsAwarded", "Punkte vergeben", "Gesendet, wenn genau ein Zuschauer ausgezahlt wird."),
                K("pointsAwardedGrouped", "Punkte vergeben (mehrere)", "Statt 'Punkte vergeben', wenn eine Runde mehrere Gewinner hat, damit der Bot unter seinem Chat-Limit bleibt. Gewinner werden nach Betrag gruppiert, ein Abo- oder VIP-Bonus ergibt also eine zweite Zeile statt einer falschen Zahl."),
                K("scoreboardHeader", "Kopfzeile", "Titelzeile der Bestenliste. Zeilen werden in so wenige Chat-Nachrichten gepackt wie möglich, sie kann sich also eine Nachricht mit den ersten Einträgen teilen."),
                K("scoreboardEntry", "Eintrag", "Eine Zeile der Bestenliste, je gereihtem Zuschauer wiederholt."),
                K("scoreboardEntryBlitz", "Eintrag (Blitz)", "An eine Zeile der Bestenliste angehängt, aber nur bei Zuschauern mit mindestens einem Blitz-Sieg oder Blitz-Punkt."),
                K("scoreboardEmpty", "Liste leer", "Gesendet, wenn noch niemand eine Statistik hat oder alle mit Statistik von der Liste ausgeschlossen sind."),
                K("personalStats", "Eigene Statistik", "Antwort, wenn ein Zuschauer die eigene Statistik abfragt."),
                K("targetStats", "Statistik eines anderen", "Antwort, wenn ein Zuschauer die Statistik eines anderen abfragt."),
                K("noStats", "Noch keine Statistik", "Antwort, wenn der abgefragte Zuschauer existiert, aber nie gewonnen oder gepunktet hat."),
                K("userNotFound", "Nutzer nicht gefunden", "Antwort, wenn der abgefragte Name zu niemandem mit Trivia-Statistik passt."),
                K("noInputUsage", "Kein Name angegeben", "Antwort, wenn der Statistik-Befehl ohne Nutzernamen benutzt wird."),
                K("callerNotOnBoard", "Nicht auf der Liste", "Nach der Bestenliste gesendet, wenn der Fragende eine Statistik hat, aber nicht unter den ersten N ist."),
                K("callerFooter", "Seine eigenen Zahlen", "Folgt auf 'Nicht auf der Liste' mit den eigenen Zahlen dieses Zuschauers."),
                K("monthlyReset", "Monatlicher Reset", "Einmal angekündigt, wenn der automatische Reset die Bestenliste wischt."),
                K("addictedUsage", "Verwendung", "Gesendet, wenn der Süchtigen-Befehl ohne Ziel benutzt wird."),
                K("addictedAdded", "Hinzugefügt", "Gesendet, wenn ein Zuschauer auf die Süchtigen-Liste kommt. Er gewinnt dann nur die Hälfte seiner normalen Punkte."),
                K("addictedAlreadyOnList", "Schon auf der Liste", "Gesendet, wenn ein Zuschauer hinzugefügt wird, der schon auf der Süchtigen-Liste steht."),
                K("addictedRemoved", "Entfernt", "Gesendet, wenn ein Zuschauer von der Süchtigen-Liste genommen wird."),
                K("addictedNotOnList", "Nicht auf der Liste", "Gesendet, wenn ein Zuschauer entfernt wird, der nicht auf der Süchtigen-Liste stand."),
                K("suggestAccepted", "Angenommen", "Gesendet, wenn ein Vorschlag gespeichert wurde."),
                K("suggestNotAllowed", "Keine Berechtigung", "Gesendet, wenn jemand ohne Belohnung, Gruppe oder Moderatorstatus vorschlagen will."),
                K("suggestDisabled", "Vorschläge aus", "Gesendet, wenn der Befehl benutzt wird, während 'Fragenvorschläge erlauben' aus ist."),
                K("suggestTooMany", "Schon zu viele", "Gesendet, wenn der Zuschauer schon an der Grenze 'Höchstens je Zuschauer' ist."),
                K("suggestUsage", "Verwendung", "Gesendet, wenn die Nachricht keine Frage, keine richtige Antwort und mindestens eine falsche trug."),
                K("suggestBadAnswers", "Braucht bessere Antworten", "Gesendet, wenn nach dem Streichen falscher Antworten, die der richtigen gleichen, nichts Brauchbares blieb."),
                K("suggestTooLong", "Zu lang", "Gesendet, wenn die Frage oder eine der Antworten über den Längenbegrenzungen liegt."),
                K("suggestDuplicate", "Schon im Pool", "Gesendet, wenn dieselbe Frage schon in der Liste ist, ohne Groß-/Kleinschreibung und Satzzeichen."),
                K("suggestFailed", "Konnte nicht speichern", "Gesendet, wenn die Fragendatei nicht geschrieben werden konnte. Das Protokoll nennt den Grund."),
                K("globalCooldownMessage", "Globale Abklingzeit", "Gesendet, wenn der Startbefehl benutzt wird, während die globale Abklingzeit noch läuft."),
                K("userCooldownMessage", "Nutzer-Abklingzeit", "Gesendet, wenn ein Zuschauer den Startbefehl benutzt, während seine eigene Abklingzeit noch läuft."),
                K("redemptionRefunded", "Einlösung erstattet", "Gesendet, wenn eine Einlösung keine Runde starten konnte und die Kanalpunkte zurückgingen. Leer lassen, um still zu erstatten."),
                K("autoStartAnnouncement", "Hinweis Autostart", "Direkt vor der Frage einer automatischen Runde gesendet, damit der Chat sie von einer angefragten unterscheidet. Leer lassen, um still zu starten."),
                K("categoryUnknown", "Unbekannte Kategorie", "Gesendet, wenn ein Zuschauer eine Kategorie nennt, die nicht spielbar ist. Die Runde fällt so oder so aus, statt still etwas anderes zu fragen; leer lassen macht das nur still."),
                K("modOnlyStart", "Nur Mods: Start", "Gesendet, wenn ein Zuschauer eine Runde starten will, während 'Nur Moderatoren dürfen starten' an ist."),
                K("modOnlySkip", "Nur Mods: Skip", "Gesendet, wenn ein Zuschauer überspringen will, während 'Nur Moderatoren dürfen überspringen' an ist."),
                K("modOnlyForceEnd", "Nur Mods: erzwungenes Ende", "Gesendet, wenn ein Zuschauer eine Runde erzwungen beenden will. Das ist immer Moderatoren vorbehalten."),
                K("alreadyActive", "Schon aktiv", "Gesendet, wenn jemand eine Runde startet, während schon eine läuft."),
                K("cannotSkip", "Kein Skip möglich", "Gesendet, wenn Skip benutzt wird, aber keine Runde gerade Antworten annimmt."),
                K("cannotForceEnd", "Kein erzwungenes Ende möglich", "Gesendet, wenn das erzwungene Ende benutzt wird, aber keine Runde gerade Antworten annimmt."),
                Tx("Add it to OBS", "In OBS einbauen"),
                Tx("Addicted", "Süchtige"),
                Tx("Answer matching", "Antwortzuordnung"),
                Tx("Answers", "Antworten"),
                Tx("Any", "Beliebig"),
                Tx("Auto start", "Automatischer Start"),
                Tx("Auto start off", "Autostart aus"),
                Tx("Auto start on", "Autostart an"),
                Tx("Blitz rounds", "Blitzrunden"),
                Tx("Bottom centre", "Unten mittig"),
                Tx("Brutal", "Brutal"),
                Tx("Channel points", "Kanalpunkte"),
                Tx("Chat command", "Chat-Befehl"),
                Tx("Cooldown", "Abklingzeit"),
                Tx("Cooldowns", "Abklingzeiten"),
                Tx("Custom", "Eigene"),
                Tx("Deutsch", "Deutsch"),
                Tx("Difficulty", "Schwierigkeit"),
                Tx("Easy", "Leicht"),
                Tx("English", "Englisch"),
                Tx("Errors", "Fehler"),
                Tx("Gentle", "Sanft"),
                Tx("Hard", "Schwer"),
                Tx("How rounds start on Twitch, from the Access page. YouTube and Kick always use the chat command.", "Wie Runden auf Twitch starten, von der Seite Zugriff. YouTube und Kick nutzen immer den Chat-Befehl."),
                Tx("How viewers play", "Wie Zuschauer spielen"),
                Tx("IGDB", "IGDB"),
                Tx("IGDB access", "IGDB-Zugang"),
                Tx("Is the source reachable?", "Ist die Quelle erreichbar?"),
                Tx("Kept out", "Ausgeschlossen"),
                Tx("Look", "Aussehen"),
                Tx("Make it your own", "Anpassen"),
                Tx("Medium", "Mittel"),
                Tx("Mine + opentdb", "Meine + opentdb"),
                Tx("Monthly reset", "Monatlicher Reset"),
                Tx("Multiple Choice only", "Nur Multiple Choice"),
                Tx("My questions only", "Nur meine Fragen"),
                Tx("OBS overlay", "OBS-Overlay"),
                Tx("OdeNet (German WordNet)", "OdeNet (deutsches WordNet)"),
                Tx("Overlay off", "Overlay aus"),
                Tx("Overlay on", "Overlay an"),
                Tx("Payouts", "Auszahlungen"),
                Tx("Platforms", "Plattformen"),
                Tx("Points", "Punkte"),
                Tx("PokeAPI", "PokeAPI"),
                Tx("Questions from chat", "Fragen aus dem Chat"),
                Tx("Relaxed", "Entspannt"),
                Tx("Results", "Ergebnisse"),
                Tx("Round", "Runde"),
                Tx("Rounds", "Runden"),
                Tx("Rounds that start themselves", "Runden, die sich selbst starten"),
                Tx("Scoreboard", "Bestenliste"),
                Tx("Sharp", "Scharf"),
                Tx("Standard", "Standard"),
                Tx("Streamer.bot's WebSocket server", "Streamer.bots WebSocket-Server"),
                Tx("Suggestions", "Vorschläge"),
                Tx("Top centre", "Oben mittig"),
                Tx("True/False + Multiple Choice", "Wahr/Falsch + Multiple Choice"),
                Tx("True/False only", "Nur Wahr/Falsch"),
                Tx("Very hard", "Sehr schwer"),
                Tx("What arrives", "Was ankommt"),
                Tx("What it shows", "Was es zeigt"),
                Tx("Where questions come from", "Woher die Fragen kommen"),
                Tx("Whether rounds reach the OBS overlay, from the Overlay page.", "Ob Runden das OBS-Overlay erreichen, von der Seite Overlay."),
                Tx("Whether rounds start themselves on Streamer.bot's timed action, from the Game page.", "Ob Runden sich über Streamer.bots zeitgesteuerte Aktion selbst starten, von der Seite Spiel."),
                Tx("Which of your own questions may be drawn", "Welche deiner Fragen gezogen werden dürfen"),
                Tx("Who may run a round", "Wer eine Runde leiten darf"),
                Tx("Who may suggest", "Wer vorschlagen darf"),
                Tx("Wikidata (SPARQL)", "Wikidata (SPARQL)"),
                Tx("Your own questions", "Deine eigenen Fragen"),
                Tx("opentdb only", "Nur opentdb")
            );

    // ---- The table: every setting the host reads, in page order. ----
    public static readonly Setting[] Table =
    {
        S("difficulty", "standard", page: "game", label: "Difficulty", help: "Sets the answer times, the countdown reminders and the spelling tolerance in one go, from Relaxed (two minutes and the loosest spelling check) to Brutal (half a minute, no fuzzy matching). Every one of those numbers can still be changed by hand below; Custom is your own numbers.", kind: "slider", section: "Difficulty", options: new[] { "relaxed|Relaxed", "gentle|Gentle", "standard|Standard", "sharp|Sharp", "brutal|Brutal", "custom|Custom" }, transient: true, live: true),
        S("difficultyNote", null, page: "game", help: "A stop of the slider fills in the six numbers it stands for; nothing is saved until you save. The game only reads the numbers, so you can fine-tune them afterwards.", kind: "note", section: "Difficulty"),
        S("answerTimeSeconds", 60, page: "game", label: "Answer time", help: "How long a normal round accepts answers, in seconds.", section: "Rounds", min: 1.0, unit: "s", row: "roundTimes", aliases: new[] { "AnswerTimeSeconds" }),
        S("normalCountdownStartSeconds", 30, page: "game", label: "First reminder at", help: "Seconds left when chat gets the first countdown reminder. The second lands at half of it, the third at half again, capped at 5. Only the round's own length caps this.", section: "Rounds", min: 0.0, unit: "s", row: "roundTimes", aliases: new[] { "NormalCountdownStartSeconds" }),
        S("delayBetweenRoundsSeconds", 5, page: "game", label: "Delay between rounds", help: "The pause between the result of one round and the question of the next, in a back-to-back run.", section: "Rounds", min: 0.0, unit: "s", row: "roundTimes", aliases: new[] { "DelayBetweenRoundsSeconds" }),
        S("defaultDifficulty", "any", page: "game", label: "Difficulty", help: "How hard the questions drawn from opentdb, Wikidata, OdeNet, PokeAPI and IGDB are. Your own questions are filtered on the Questions page instead.", kind: "options", section: "Rounds", options: new[] { "any|Any", "easy|Easy", "medium|Medium", "hard|Hard" }, row: "roundDefaults", aliases: new[] { "DefaultDifficulty" }),
        S("defaultQuestionType", "both", page: "game", label: "Question type", help: "Which shape of question may be drawn from opentdb, Wikidata, OdeNet, PokeAPI and IGDB.", kind: "options", section: "Rounds", options: new[] { "both|True/False + Multiple Choice", "boolean|True/False only", "multiple|Multiple Choice only" }, row: "roundDefaults", aliases: new[] { "DefaultQuestionType" }),
        S("defaultCategoryId", "0", page: "game", label: "Category ID", help: "The opentdb category every round uses. 0 means any category. The names and their numbers are on opentdb.com/api_config.php. The other sources ignore it.", section: "Rounds", row: "roundDefaults", aliases: new[] { "DefaultCategoryId" }),
        S("allowCategoryChoice", true, page: "game", label: "Let chat pick the category", help: "Viewers may name a category when they start a round, like '!trivia history'. Only categories a round could actually be drawn from are offered.", section: "Rounds", aliases: new[] { "AllowCategoryChoice" }),
        S("showDetails", true, page: "game", label: "Show difficulty and category with the question", help: "Off, and the question's footer only says how long there is to answer.", section: "Rounds", aliases: new[] { "ShowDetails" }),
        S("autoStartNextRoundAfterSkip", true, page: "game", label: "Start the next round after a skip", help: "A skip asks for another question rather than ending the run. A chosen category carries over.", section: "Rounds", aliases: new[] { "AutoStartNextRoundAfterSkip" }),
        S("allowMultipleRounds", true, page: "game", label: "Allow several rounds in a row", help: "'!trivia 3' queues three rounds. Off, and every start plays exactly one.", section: "Rounds", row: "series", aliases: new[] { "AllowMultipleRounds" }),
        S("maxRoundsPerCommand", 10, page: "game", label: "Max per command", help: "The ceiling for a number typed into the start command.", section: "Rounds", min: 1.0, row: "series", aliases: new[] { "MaxRoundsPerCommand" }),
        S("blitzNote", null, page: "game", help: "A blitz round ends on the first correct answer instead of running out the clock. Which rounds turn into one is down to chance.", kind: "note", section: "Blitz rounds"),
        S("enableBlitzTrivia", true, page: "game", label: "Enable blitz rounds", help: "Off, and every round runs its full answer time.", section: "Blitz rounds", aliases: new[] { "EnableBlitzTrivia" }),
        S("blitzChance", 10, page: "game", label: "Chance (%)", help: "How often a round becomes a blitz round, as a percentage.", kind: "percent", section: "Blitz rounds", min: 0.0, max: 100.0, row: "blitz", aliases: new[] { "BlitzChance" }),
        S("blitzAnswerTimeSeconds", 20, page: "game", label: "Answer time", help: "How long a blitz round accepts answers, in seconds.", section: "Blitz rounds", min: 1.0, unit: "s", row: "blitz", aliases: new[] { "BlitzAnswerTimeSeconds" }),
        S("blitzCountdownStartSeconds", 10, page: "game", label: "First reminder at", help: "Seconds left when a blitz round gets its first reminder.", section: "Blitz rounds", min: 0.0, unit: "s", row: "blitz", aliases: new[] { "BlitzCountdownStartSeconds" }),
        S("matchingNote", null, page: "game", help: "Switch all four off and no answer can ever be accepted.", kind: "note", section: "Answer matching"),
        S("allowNumericAnswers", true, page: "game", label: "By option number", help: "Typing '2' picks the second option.", section: "Answer matching", aliases: new[] { "AllowNumericAnswers" }),
        S("allowExactTextAnswers", true, page: "game", label: "Exactly as written", help: "The answer text, ignoring case.", section: "Answer matching", aliases: new[] { "AllowExactTextAnswers" }),
        S("allowNormalizedTextAnswers", true, page: "game", label: "Ignoring punctuation and accents", help: "'Paris.' and 'paris' both count.", section: "Answer matching", aliases: new[] { "AllowNormalizedTextAnswers" }),
        S("allowFuzzyTextAnswers", true, page: "game", label: "Close spellings", help: "A genuine typo still counts, within the tolerance below.", section: "Answer matching", aliases: new[] { "AllowFuzzyTextAnswers" }),
        S("fuzzyTolerance", 2, page: "game", label: "Fuzzy tolerance", help: "How many characters may differ before an answer is turned away.", section: "Answer matching", min: 0.0, row: "fuzzy", aliases: new[] { "FuzzyTolerance" }),
        S("minLengthForFuzzy", 4, page: "game", label: "Min. length for fuzzy", help: "Short answers are never matched loosely. Keep this above the tolerance, or genuinely different words start counting.", section: "Answer matching", min: 1.0, row: "fuzzy", aliases: new[] { "MinLengthForFuzzy" }),
        S("autoStartNote", null, page: "game", help: "A Streamer.bot timed action offers a round to every platform on its own schedule, each judged on its own chat and its own running round.", kind: "note", section: "Rounds that start themselves"),
        S("autoStartEnabled", false, page: "game", label: "Start rounds automatically", help: "Switches the Streamer.bot timed action on and off from here.", section: "Rounds that start themselves", aliases: new[] { "AutoStartEnabled" }),
        S("autoStartIntervalMinutes", 15, page: "game", label: "Every (minutes)", help: "How long between automatic rounds. A running timer keeps its old interval until it is switched off and on again.", section: "Rounds that start themselves", min: 1.0, unit: "min", row: "auto", aliases: new[] { "AutoStartIntervalMinutes" }),
        S("autoStartRounds", 1, page: "game", label: "Rounds each time", help: "How many rounds one turn of the timer plays. Always one while 'Allow several rounds in a row' is off.", section: "Rounds that start themselves", min: 1.0, row: "auto", aliases: new[] { "AutoStartRounds" }),
        S("autoStartChatActivityMinutes", 10, page: "game", label: "Chat within (minutes)", help: "How recent chat has to be for the automatic round to go ahead.", section: "Rounds that start themselves", min: 1.0, unit: "min", row: "auto", aliases: new[] { "AutoStartChatActivityMinutes" }),
        S("autoStartRequireChatActivity", true, page: "game", label: "Only when chat has been active", help: "Off, and a round is offered on the schedule whether or not anybody is there.", section: "Rounds that start themselves", aliases: new[] { "AutoStartRequireChatActivity" }),
        S("awardPoints", true, page: "game", label: "Pay out points", help: "Off, and rounds are played for nothing but the answer.", section: "Points", aliases: new[] { "AwardPoints" }),
        S("baseWinPoints", 1234, page: "game", label: "Base win", help: "What a win pays before the multipliers below.", section: "Points", min: 0.0, row: "points", aliases: new[] { "BaseWinPoints" }),
        S("pointsVariable", "points", page: "game", label: "Points variable", help: "The variable your points live in. Currency Core by StreamUP and LOYALTY POINTS by tawmae are found on their own; only change this for a points system of your own.", section: "Points", row: "points", aliases: new[] { "VarName" }),
        S("pointsName", "Points", page: "game", label: "Shown in chat as", help: "What {pointsName} says in chat, like 'Coins' or 'Gems'. Only the wording changes.", section: "Points", row: "points", aliases: new[] { "PointsName" }),
        S("payoutNote", null, page: "game", help: "Applied to the base win according to the question. The online sources only report easy, medium and hard; Very hard is there for questions you write yourself.", kind: "note", section: "Payouts"),
        S("easyMultiplier", 1.0, page: "game", label: "Easy", help: "Multiplier for an easy question.", section: "Payouts", min: 0.0, row: "mult1", aliases: new[] { "EasyMultiplier" }),
        S("mediumMultiplier", 1.75, page: "game", label: "Medium", help: "Multiplier for a medium question.", section: "Payouts", min: 0.0, row: "mult1", aliases: new[] { "MediumMultiplier" }),
        S("hardMultiplier", 2.5, page: "game", label: "Hard", help: "Multiplier for a hard question.", section: "Payouts", min: 0.0, row: "mult1", aliases: new[] { "HardMultiplier" }),
        S("veryHardMultiplier", 3.0, page: "game", label: "Very hard", help: "Multiplier for a very hard question of your own.", section: "Payouts", min: 0.0, row: "mult2", aliases: new[] { "VeryHardMultiplier" }),
        S("blitzMultiplier", 2.0, page: "game", label: "Blitz", help: "Stacked on top for a blitz round.", section: "Payouts", min: 0.0, row: "mult2", aliases: new[] { "BlitzMultiplier" }),
        S("subscriberBoostEnabled", false, page: "game", label: "Subscriber boost", help: "Pays subscribers more. Boosts stack with each other and apply after the multipliers above.", section: "Payouts", twitchOnly: true, row: "sub", aliases: new[] { "SubscriberBoostEnabled" }),
        S("subscriberBoostPercent", 10.0, page: "game", label: "Subscriber (%)", help: "How much more a subscriber wins, as a percentage.", kind: "percent", section: "Payouts", min: 0.0, row: "sub", aliases: new[] { "SubscriberBoostPercent" }),
        S("vipBoostEnabled", false, page: "game", label: "VIP boost", help: "Pays VIPs more. Boosts stack with each other and apply after the multipliers above.", section: "Payouts", twitchOnly: true, row: "vip", aliases: new[] { "VipBoostEnabled" }),
        S("vipBoostPercent", 20.0, page: "game", label: "VIP (%)", help: "How much more a VIP wins, as a percentage.", kind: "percent", section: "Payouts", min: 0.0, row: "vip", aliases: new[] { "VipBoostPercent" }),
        S("addictedNote", null, page: "game", help: "Viewers on the addicted list collect half of whatever the above works out to. That list is on the Access page.", kind: "note", section: "Payouts"),
        S("scoreboardTopN", 5, page: "game", label: "Places shown", help: "How many viewers the leaderboard lists.", section: "Scoreboard", min: 1.0, aliases: new[] { "ScoreboardTopN" }),
        S("broadcasterAllowed", false, page: "game", label: "Include the broadcaster", help: "Off, and your own account never appears on the board.", section: "Scoreboard", aliases: new[] { "BroadcasterAllowed" }),
        S("botAllowed", false, page: "game", label: "Include the bot", help: "Off, and your bot account never appears on the board.", section: "Scoreboard", aliases: new[] { "BotAllowed" }),
        S("blockedFromBoard", "", page: "game", label: "Blocked from the board", help: "Accounts kept off the leaderboard whatever the two switches above say , other bots, for example.", kind: "namelist", section: "Scoreboard", aliases: new[] { "BlockedFromBoard" }),
        S("resetNote", null, page: "game", help: "Switching a reset on only arms it: the first wipe lands at the next month boundary, never mid-month. A day the month does not have, 31 in February, falls on that month's last day.", kind: "note", section: "Monthly reset"),
        S("resetAllMonthly", false, page: "game", label: "Reset every platform monthly", help: "Zeroes every viewer's stats once a month on all three platforms.", section: "Monthly reset", aliases: new[] { "ResetAllMonthly" }),
        S("resetTwitchMonthly", false, page: "game", label: "Twitch", help: "Reset the Twitch board monthly.", section: "Monthly reset", row: "resetTwitch", aliases: new[] { "ResetTwitchMonthly" }),
        S("resetTwitchDay", 0, page: "game", label: "Day", help: "Day of the month the Twitch reset lands on.", section: "Monthly reset", min: 0.0, max: 31.0, row: "resetTwitch", aliases: new[] { "ResetTwitchDay" }),
        S("resetYouTubeMonthly", false, page: "game", label: "YouTube", help: "Reset the YouTube board monthly.", section: "Monthly reset", row: "resetYouTube", aliases: new[] { "ResetYouTubeMonthly" }),
        S("resetYouTubeDay", 0, page: "game", label: "Day", help: "Day of the month the YouTube reset lands on.", section: "Monthly reset", min: 0.0, max: 31.0, row: "resetYouTube", aliases: new[] { "ResetYouTubeDay" }),
        S("resetKickMonthly", false, page: "game", label: "Kick", help: "Reset the Kick board monthly.", section: "Monthly reset", row: "resetKick", aliases: new[] { "ResetKickMonthly" }),
        S("resetKickDay", 0, page: "game", label: "Day", help: "Day of the month the Kick reset lands on.", section: "Monthly reset", min: 0.0, max: 31.0, row: "resetKick", aliases: new[] { "ResetKickDay" }),
        S("questionsNote", null, page: "questions", help: "One row per question: the question, its correct answer, the wrong answers separated by | (chat sees them shuffled together with the correct one), a category and a difficulty (easy, medium, hard or very hard). Suggested by says who sent a question in from chat. Rows with nothing in them are dropped when you save; Restore defaults puts the example questions back.", kind: "note", section: "Your own questions"),
        S("customQuestions", JArray.Parse("[{\"Enabled\":true,\"Question\":\"What is the name of this channel's chat bot?\",\"CorrectAnswer\":\"Streamer.bot\",\"WrongAnswers\":\"Nightbot | Moobot | Fossabot\",\"Category\":\"This Stream\",\"Difficulty\":\"easy\"},{\"Enabled\":true,\"Question\":\"What do you call the short recorded moment taken from a live stream?\",\"CorrectAnswer\":\"A clip\",\"WrongAnswers\":\"A snip | A frame | A cut\",\"Category\":\"Streaming\",\"Difficulty\":\"easy\"},{\"Enabled\":true,\"Question\":\"On Twitch, what is a raid?\",\"CorrectAnswer\":\"Sending your viewers to another channel\",\"WrongAnswers\":\"Banning a group of users | Starting a subathon | Hosting a giveaway\",\"Category\":\"Streaming\",\"Difficulty\":\"easy\"},{\"Enabled\":true,\"Question\":\"True or False: a VOD is the recording of a past stream.\",\"CorrectAnswer\":\"True\",\"WrongAnswers\":\"False\",\"Category\":\"Streaming\",\"Difficulty\":\"easy\"},{\"Enabled\":true,\"Question\":\"Which of these is NOT a Twitch chat badge?\",\"CorrectAnswer\":\"Curator\",\"WrongAnswers\":\"Moderator | VIP | Subscriber\",\"Category\":\"Streaming\",\"Difficulty\":\"medium\"},{\"Enabled\":true,\"Question\":\"What does the abbreviation 'OBS' stand for?\",\"CorrectAnswer\":\"Open Broadcaster Software\",\"WrongAnswers\":\"Online Broadcast System | Optical Bitrate Streaming | Overlay Build Studio\",\"Category\":\"Tech\",\"Difficulty\":\"medium\"},{\"Enabled\":true,\"Question\":\"Which unit measures how much data a stream sends per second?\",\"CorrectAnswer\":\"Bitrate\",\"WrongAnswers\":\"Framerate | Latency | Resolution\",\"Category\":\"Tech\",\"Difficulty\":\"medium\"},{\"Enabled\":true,\"Question\":\"In video, what does 60fps stand for?\",\"CorrectAnswer\":\"60 frames per second\",\"WrongAnswers\":\"60 files per stream | 60 fades per scene | 60 filters per source\",\"Category\":\"Tech\",\"Difficulty\":\"easy\"},{\"Enabled\":true,\"Question\":\"What is the delay between something happening and a viewer seeing it called?\",\"CorrectAnswer\":\"Latency\",\"WrongAnswers\":\"Buffering | Throttling | Drift\",\"Category\":\"Tech\",\"Difficulty\":\"medium\"},{\"Enabled\":true,\"Question\":\"True or False: dropped frames are usually caused by the viewer's connection, never the streamer's.\",\"CorrectAnswer\":\"False\",\"WrongAnswers\":\"True\",\"Category\":\"Tech\",\"Difficulty\":\"hard\"},{\"Enabled\":true,\"Question\":\"Which video codec is most commonly used for live streaming to Twitch?\",\"CorrectAnswer\":\"H.264\",\"WrongAnswers\":\"ProRes | VP9 | AV1\",\"Category\":\"Tech\",\"Difficulty\":\"hard\"},{\"Enabled\":true,\"Question\":\"What is the maximum number of characters a single Twitch chat message may contain?\",\"CorrectAnswer\":\"500\",\"WrongAnswers\":\"200 | 280 | 1000\",\"Category\":\"Streaming\",\"Difficulty\":\"very hard\"}]"), page: "questions", label: "Questions", help: "Kept in Trivia-Questions.json next to the settings file. Which of them may be drawn is set below; the question source on the Sources page decides whether they are used at all.", kind: "list", section: "Your own questions", file: "Trivia-Questions.json", columns: new[] { "Enabled|bool|On", "Question|text|Question", "CorrectAnswer|string|Correct answer", "WrongAnswers|string|Wrong answers (a | b | c)", "Category|string|Category", "Difficulty|string|Difficulty", "SuggestedBy|string|Suggested by" }),
        S("openQuestionsFolder", null, page: "questions", label: "Open the questions folder", help: "Opens the folder holding Trivia-Questions.json in Explorer.", kind: "action", section: "Your own questions"),
        S("ownFiltersNote", null, page: "questions", help: "Kept apart from the filters on the Game page, so narrowing one cannot silently empty the other.", kind: "note", section: "Which of your own questions may be drawn"),
        S("customDifficulty", "any", page: "questions", label: "Difficulty", kind: "options", section: "Which of your own questions may be drawn", options: new[] { "any|Any", "easy|Easy", "medium|Medium", "hard|Hard", "very hard|Very hard" }, row: "ownFilters", aliases: new[] { "CustomDifficulty" }),
        S("customQuestionType", "both", page: "questions", label: "Question type", help: "A question counts as True/False when its only two options are 'True' and 'False'.", kind: "options", section: "Which of your own questions may be drawn", options: new[] { "both|True/False + Multiple Choice", "boolean|True/False only", "multiple|Multiple Choice only" }, row: "ownFilters", aliases: new[] { "CustomQuestionType" }),
        S("customCategories", "", page: "questions", label: "Categories in play", help: "Only your questions filed under these categories are drawn. Empty means all of them.", kind: "namelist", section: "Which of your own questions may be drawn", aliases: new[] { "CustomCategories" }),
        S("questionSource", "opentdb", page: "sources", label: "Question source", help: "Where each round's question comes from. Every source but 'Mine + opentdb' is the only one a round uses.", kind: "options", section: "Where questions come from", options: new[] { "opentdb|opentdb only", "wikidata|Wikidata (SPARQL)", "odenet|OdeNet (German WordNet)", "pokeapi|PokeAPI", "igdb|IGDB", "custom|My questions only", "mixed|Mine + opentdb" }, aliases: new[] { "QuestionSource" }),
        S("sourcesNote", null, page: "sources", help: "opentdb: thousands of English questions in two dozen categories, filtered on the Game page. Wikidata: questions built from Wikidata's facts (capitals, currencies, elements, moons, paintings, books, films); chat can pick a topic. OdeNet: German vocabulary from the Open German WordNet, always German; the word list (about 4 MB) is downloaded once. PokeAPI: Pokémon types, generations, Pokédex entries and evolutions. IGDB: video game release years, studios and platforms; needs a Twitch developer application of your own, see below.", kind: "note", section: "Where questions come from"),
        S("customQuestionChance", 50, page: "sources", label: "Mine (%)", help: "Only used by 'Mine + opentdb': how likely each round is to come from your own list. 50 is roughly every other round.", kind: "percent", section: "Where questions come from", min: 0.0, max: 100.0, requires: "questionSource=mixed", row: "sourceNumbers", aliases: new[] { "CustomQuestionChance" }),
        S("cacheSize", 5, page: "sources", label: "Fetch ahead", help: "How many opentdb questions are fetched at once and kept for later rounds. Larger means fewer API calls. The other sources keep what they fetch on disk anyway, under Chat Games\\Trivia Game\\Sources.", section: "Where questions come from", min: 1.0, row: "sourceNumbers", aliases: new[] { "CacheSize" }),
        S("questionLanguage", "en", page: "sources", label: "Question language", kind: "options", section: "Where questions come from", options: new[] { "en|English", "de|Deutsch" }, row: "sourceNumbers", aliases: new[] { "QuestionLanguage" }),
        S("customFallback", true, page: "sources", label: "Fall back to the other source", help: "If the chosen source cannot produce a question , an empty list, filters that match nothing, or opentdb being down , try the other one instead of failing the round. Only used by 'My questions only' and 'Mine + opentdb'.", section: "Where questions come from", aliases: new[] { "CustomFallback" }),
        S("igdbNote", null, page: "sources", help: "IGDB is run by Twitch and only answers an application registered with Twitch. Register one at https://dev.twitch.tv/console/apps: any name, http://localhost as the OAuth redirect URL, category Application Integration, client type Confidential. Then copy its Client ID here and create a Client Secret with New Secret.", kind: "note", section: "IGDB access"),
        S("igdbClientId", "", page: "sources", label: "Client ID", help: "The Client ID of your Twitch developer application.", section: "IGDB access", requires: "questionSource=igdb", row: "igdb", aliases: new[] { "IgdbClientId" }),
        S("igdbClientSecret", "", page: "sources", label: "Client Secret", help: "The Client Secret of that application. It is stored in configs.json on this PC and sent to Twitch only, to get IGDB's access token.", section: "IGDB access", requires: "questionSource=igdb", row: "igdb", aliases: new[] { "IgdbClientSecret" }),
        S("checkSource", null, page: "sources", label: "Check", help: "Asks the source picked above whether it answers right now: opentdb, Wikidata, PokeAPI, GitHub for OdeNet's word list, or Twitch for the IGDB keys. Nothing is saved.", kind: "action", section: "Is the source reachable?", live: true, panel: true),
        S("suggestNote", null, page: "suggestions", help: "Viewers write trivia for you with !suggest. One message carries the whole question, separated by pipes: !suggest What year did Twitch launch? | 2011 | 2007 | 2014, the question first, then the correct answer, then up to three wrong ones.", kind: "note", section: "Questions from chat"),
        S("allowQuestionSuggestions", false, page: "suggestions", label: "Allow question suggestions", help: "Off, and !suggest answers with the 'Suggestions off' message.", section: "Questions from chat", aliases: new[] { "AllowQuestionSuggestions" }),
        S("whoSuggestsNote", null, page: "suggestions", help: "A suggestion is accepted from somebody who redeemed the reward, belongs to one of the groups, or is a moderator while that is on. Anyone else gets the 'No permission' message.", kind: "note", section: "Who may suggest"),
        S("suggestRewardId", "", page: "suggestions", label: "Reward ID", help: "A channel point reward that lets the redeemer suggest a question. Twitch only. Leave it blank to rely on groups and moderators.", section: "Who may suggest", twitchOnly: true, aliases: new[] { "SuggestRewardId" }),
        S("suggestAllowModerators", true, page: "suggestions", label: "Moderators may always suggest", help: "Moderators and the broadcaster can suggest without the reward or a group.", section: "Who may suggest", aliases: new[] { "SuggestAllowModerators" }),
        S("suggestAllowedGroups", "", page: "suggestions", label: "Allowed groups", help: "Members of these Streamer.bot groups may suggest questions.", kind: "grouplist", section: "Who may suggest", aliases: new[] { "SuggestAllowedGroups" }),
        S("suggestCategory", "Suggested", page: "suggestions", label: "Filed under", help: "The category suggested questions are filed under on the Custom List tab.", section: "What arrives", row: "arrive", aliases: new[] { "SuggestCategory" }),
        S("suggestDifficulty", "medium", page: "suggestions", label: "Difficulty", kind: "options", section: "What arrives", options: new[] { "easy|Easy", "medium|Medium", "hard|Hard", "very hard|Very hard" }, row: "arrive", aliases: new[] { "SuggestDifficulty" }),
        S("suggestMaxPerUser", 3, page: "suggestions", label: "Max per viewer", help: "How many of one viewer's questions may sit in the pool at once. Deleting one frees a slot.", section: "What arrives", min: 0.0, row: "limits", aliases: new[] { "SuggestMaxPerUser" }),
        S("suggestMaxQuestionLength", 200, page: "suggestions", label: "Question length", help: "Longest a suggested question may be, in characters.", section: "What arrives", min: 1.0, row: "limits", aliases: new[] { "SuggestMaxQuestionLength" }),
        S("suggestMaxAnswerLength", 60, page: "suggestions", label: "Answer length", help: "Longest each individual answer may be, in characters.", section: "What arrives", min: 1.0, row: "limits", aliases: new[] { "SuggestMaxAnswerLength" }),
        S("suggestEnabledOnArrival", true, page: "suggestions", label: "Playable the moment they arrive", help: "Off, and they land switched off on the Custom List tab for you to look over first.", section: "What arrives", aliases: new[] { "SuggestEnabledOnArrival" }),
        S("arriveNote", null, page: "suggestions", help: "Anything that would not make a playable question is turned away rather than stored: a missing answer, a wrong answer identical to the correct one, anything over the lengths above, and any question already in the pool. What chat is told is on the Messages page.", kind: "note", section: "What arrives"),
        S("platformsNote", null, page: "access", help: "Twitch, YouTube and Kick all play, and every reply goes to the chat the command or redemption came from. To leave a platform out, untick it on the Trivia commands in Streamer.bot, for example 'Kick Message'.", kind: "note", section: "Platforms"),
        S("isChannelPointsTrigger", false, page: "access", label: "Channel points start a round", help: "On Twitch, a redemption of the reward below starts a round instead of !trivia, which is then off on Twitch. Redemptions only come from Twitch, so on YouTube and Kick viewers keep starting rounds with !trivia. The reward's cost, cooldowns and text prompt are all configured on Twitch, not here.", section: "How viewers play", twitchOnly: true, aliases: new[] { "IsChannelPointsTrigger" }),
        S("targetRewardId", "", page: "access", label: "Reward ID (single round)", help: "The ID of the channel point reward that plays one round. Other rewards are ignored. Without one, no redemption starts a round.", section: "How viewers play", requires: "isChannelPointsTrigger", twitchOnly: true, aliases: new[] { "TargetRewardId" }),
        S("multiRoundRewardId", "", page: "access", label: "Reward ID (multi round)", help: "An optional second reward that plays several rounds from one redemption. Leave it blank if you only want single rounds. It is ignored while 'Allow several rounds in a row' is off on the Game page.", section: "How viewers play", requires: "isChannelPointsTrigger", twitchOnly: true, row: "multi", aliases: new[] { "MultiRoundRewardId" }),
        S("maxRoundsPerRedeem", 3, page: "access", label: "Max per redeem", help: "The ceiling for that reward. Turn on 'Require Viewer to Enter Text' on the Twitch reward and viewers type the number themselves, clamped to this. Leave it off and every redemption plays the ceiling.", section: "How viewers play", min: 1.0, requires: "isChannelPointsTrigger", row: "multi", aliases: new[] { "MaxRoundsPerRedeem" }),
        S("refundFailedRedemptions", true, page: "access", label: "Refund redemptions that don't start a round", help: "A round already running, a cooldown, or no question to be had: the redemption is cancelled and the channel points go back. Only works while the redemption is still queued, a reward that skips the request queue can't be refunded.", section: "How viewers play", requires: "isChannelPointsTrigger", twitchOnly: true, aliases: new[] { "RefundFailedRedemptions" }),
        S("requireModeratorForStart", true, page: "access", label: "Only moderators may start", help: "Off, and anybody in chat can start a round. The broadcaster can always start, whatever this says.", section: "Who may run a round", aliases: new[] { "RequireModeratorForStart" }),
        S("requireModeratorForSkip", true, page: "access", label: "Only moderators may skip", help: "Off, and anybody in chat can skip the running question. Force end is always moderators only.", section: "Who may run a round", aliases: new[] { "RequireModeratorForSkip" }),
        S("globalCooldown", 0, page: "access", label: "Everyone", help: "How long everyone waits after a round is started, for example '60'. 0 turns it off. It gates the command that starts a run, the rounds of a back-to-back series run straight through it.", section: "Cooldowns", min: 0.0, unit: "s", row: "cooldowns", aliases: new[] { "CooldownGlobalSeconds" }),
        S("userCooldown", 0, page: "access", label: "Per viewer", help: "How long one viewer waits before starting another round, for example '60'. 0 turns it off.", section: "Cooldowns", min: 0.0, unit: "s", row: "cooldowns", aliases: new[] { "CooldownUserSeconds" }),
        S("cooldownNote", null, page: "access", help: "0 turns a cooldown off. Saving writes both onto the !trivia command in Streamer.bot. What chat is told meanwhile is on the Messages page.", kind: "note", section: "Cooldowns"),
        S("blockedUsers", "", page: "access", label: "Blocked viewers", help: "Viewers whose answers and commands are ignored, without a word back.", kind: "namelist", section: "Kept out", aliases: new[] { "BlockedUsers" }),
        S("blockedGroups", "", page: "access", label: "Blocked groups", help: "Streamer.bot user groups whose members are ignored. Names must match the groups exactly.", kind: "grouplist", section: "Kept out", aliases: new[] { "BlockedGroups" }),
        S("addictedUsers", "", page: "access", label: "Addicted viewers", help: "They still play, but only ever collect half of what they would have won. The addicted command edits this list from chat.", kind: "namelist", section: "Addicted", aliases: new[] { "AddictedUsers" }),
        S("addictedGroups", "", page: "access", label: "Addicted groups", help: "Members of these Streamer.bot groups count as addicted too.", kind: "grouplist", section: "Addicted", aliases: new[] { "AddictedGroups" }),
        S("useBot", true, aliases: new[] { "UseBot" }),
        S("sendAction", false, aliases: new[] { "SendAction" }),
        S("cooldownUseBot", true, aliases: new[] { "UseBotCD" }),
        S("cooldownSendAction", false, aliases: new[] { "SendActionCD" }),
        S("numberSeparator", ".", aliases: new[] { "NumberSeparator" }),
        S("announceEachAnswer", true, page: "messages", label: "Confirm every answer in chat", help: "Off, and the Answers messages are never sent. On a busy channel that is one line per answer.", aliases: new[] { "AnnounceEachAnswer" }),
        S("overlayNote", null, page: "overlay", help: "A browser source that shows the running question with its countdown, then the right answer, the winners and the leaderboard. It listens to Streamer.bot's own WebSocket server, so there is nothing else to install.", kind: "note", section: "OBS overlay"),
        S("overlayEnabled", false, page: "overlay", label: "Send rounds to the overlay", help: "Off, and the game sends the overlay nothing, so it stays empty.", section: "OBS overlay", aliases: new[] { "OverlayEnabled" }),
        S("websocketNote", null, page: "overlay", help: "Switch it on in Streamer.bot under Servers/Clients, WebSocket Server, and tick Auto Start. Port and password have to be the ones shown there.", kind: "note", section: "Streamer.bot's WebSocket server"),
        S("overlayPort", 8080, page: "overlay", label: "Port", help: "The port of Streamer.bot's WebSocket server. 8080 unless you changed it there.", section: "Streamer.bot's WebSocket server", min: 1.0, max: 65535.0, row: "server", aliases: new[] { "OverlayPort" }),
        S("overlayPassword", "", page: "overlay", label: "Password", help: "Only if you switched on authentication for the WebSocket server; leave it empty otherwise. It goes into the overlay's URL in OBS, because the overlay signs in with it.", section: "Streamer.bot's WebSocket server", row: "server", aliases: new[] { "OverlayPassword" }),
        S("obsNote", null, page: "overlay", help: "Adds a browser source the size of your canvas to the scene below, pointed at the overlay. OBS has to be connected in Streamer.bot under Stream Apps, OBS Studio. Leave the scene empty to use the one that is live in OBS right now. A source of the same name is pointed at the overlay again instead of doubled up.", kind: "note", section: "Add it to OBS"),
        S("overlayObsConnection", 0, page: "overlay", label: "Connection #", help: "Which of Streamer.bot's OBS connections to use, if you have more than one. 0 is the first.", section: "Add it to OBS", min: 0.0, row: "obs", aliases: new[] { "OverlayObsConnection" }),
        S("overlayObsScene", "Trivia", page: "overlay", label: "Scene", help: "The scene the source is added to. It is created if it doesn't exist yet. Empty: the scene that is live in OBS when you press the button.", section: "Add it to OBS", row: "obs", aliases: new[] { "OverlayObsScene" }),
        S("overlayObsSource", "Trivia Overlay", page: "overlay", label: "Source", help: "The browser source's name. If OBS already has a source of this name, you are asked whether to point it at the overlay again instead.", section: "Add it to OBS", row: "obs", aliases: new[] { "OverlayObsSource" }),
        S("setupObsOverlay", null, page: "overlay", label: "Set it up in OBS", help: "Writes the overlay page, then adds the browser source in OBS, or points a source of that name at the overlay again. Uses the settings as saved, so save first.", kind: "action", section: "Add it to OBS", confirm: "This adds a browser source to OBS, covering the whole canvas, on the OBS connection, scene and source named above. Go ahead?", row: "obsButtons"),
        S("overlaySample", null, page: "overlay", label: "Show a sample round", help: "Plays one made-up round on every overlay that is open, the one in OBS included, so you can see where it sits and how it looks. If you are live, your viewers see it too. Uses the settings as saved.", kind: "action", section: "Add it to OBS", row: "obsButtons"),
        S("byHandNote", null, page: "overlay", help: "Or by hand: add a Browser source, untick 'Local file', paste the URL that Copy URL puts on the clipboard and set the size to your canvas, usually 1920 x 1080. The page behind it is written to Chat Games\\Trivia Game\\Overlay whenever you copy the URL or save with the overlay on.", kind: "note", section: "Add it to OBS"),
        S("copyOverlayUrl", null, page: "overlay", label: "Copy URL", help: "Writes the overlay page and copies its address, port and password included, for a Browser source in OBS.", kind: "action", section: "Add it to OBS", row: "urlButtons"),
        S("overlayDemo", null, page: "overlay", label: "Try the demo", help: "Opens the overlay in your browser, playing sample rounds the way the saved settings say, without Streamer.bot.", kind: "action", section: "Add it to OBS", row: "urlButtons"),
        S("openOverlayFolder", null, page: "overlay", label: "Open the overlay folder", help: "Opens the folder holding the overlay page in Explorer.", kind: "action", section: "Add it to OBS", row: "urlButtons"),
        S("overlayShowAnswerCount", true, page: "overlay", label: "How many have answered", help: "Counts the answers while a round runs. Only the number, never which option anybody picked.", section: "What it shows", aliases: new[] { "OverlayShowAnswerCount" }),
        S("overlayShowDetails", true, page: "overlay", label: "Category and difficulty", help: "The chips above the question. Separate from 'Show difficulty and category with the question' on the Game page, which is for chat, so either can go without the other.", section: "What it shows", aliases: new[] { "OverlayShowDetails" }),
        S("overlayShowTimer", true, page: "overlay", label: "The countdown", help: "The clock and the bar running down. Off, the round still ends on time, it just isn't shown.", section: "What it shows", aliases: new[] { "OverlayShowTimer" }),
        S("overlayShowWinners", true, page: "overlay", label: "Who won", help: "The winners' names with their points under the result. Off, the result only says how many were right.", section: "What it shows", aliases: new[] { "OverlayShowWinners" }),
        S("overlayShowLeaderboard", true, page: "overlay", label: "The leaderboard after a round", help: "Follows the result with the top of the leaderboard, ranked like !TL and leaving out the same viewers.", section: "What it shows", aliases: new[] { "OverlayShowLeaderboard" }),
        S("overlayResultSeconds", 10, page: "overlay", label: "Result shown for (s)", help: "How long the answer and the winners stay up, and the leaderboard after them.", section: "What it shows", min: 1.0, unit: "s", row: "overlayTiming", aliases: new[] { "OverlayResultSeconds" }),
        S("overlayLeaderboardPlaces", 5, page: "overlay", label: "Leaderboard places", help: "How many viewers the overlay's leaderboard lists, up to 10.", section: "What it shows", min: 1.0, max: 10.0, row: "overlayTiming", aliases: new[] { "OverlayLeaderboardPlaces" }),
        S("overlayPosition", "bottom", page: "overlay", label: "Question", kind: "options", section: "Look", options: new[] { "bottom|Bottom centre", "top|Top centre" }, row: "placing", aliases: new[] { "OverlayPosition" }),
        S("overlayScale", 100, page: "overlay", label: "Size %", help: "Scales the question and the leaderboard together. 100 is the normal size, from 50 to 150.", kind: "percent", section: "Look", min: 50.0, max: 150.0, row: "placing", aliases: new[] { "OverlayScale" }),
        S("overlayAnimations", true, page: "overlay", label: "Animations", help: "The fade and slide when the question, the result and the leaderboard come and go. Off, they simply appear.", section: "Look", aliases: new[] { "OverlayAnimations" }),
        S("overlayAccentColor", "#9B8CFF", page: "overlay", label: "Accent", help: "The option numbers and the countdown bar.", kind: "colour", section: "Look", row: "colours1", aliases: new[] { "OverlayAccentColor" }),
        S("overlayCorrectColor", "#3DDC97", page: "overlay", label: "Right answer", help: "The right option, the winners and the border of a won round.", kind: "colour", section: "Look", row: "colours1", aliases: new[] { "OverlayCorrectColor" }),
        S("overlayBlitzColor", "#FF5C5C", page: "overlay", label: "Blitz", help: "A blitz round's border, chip and countdown bar.", kind: "colour", section: "Look", row: "colours1", aliases: new[] { "OverlayBlitzColor" }),
        S("overlayTextColor", "#FFFFFF", page: "overlay", label: "Text", help: "All the text. The chips, the option tiles and the lines are drawn in it too, faintly.", kind: "colour", section: "Look", row: "colours2", aliases: new[] { "OverlayTextColor" }),
        S("overlayPanelColor", "#101016", page: "overlay", label: "Panel", help: "The background behind the question and the leaderboard.", kind: "colour", section: "Look", row: "colours2", aliases: new[] { "OverlayPanelColor" }),
        S("overlayPanelOpacity", 90, page: "overlay", label: "Panel opacity %", help: "0 lets your scene show through completely, 100 is solid.", kind: "percent", section: "Look", min: 0.0, max: 100.0, row: "colours2", aliases: new[] { "OverlayPanelOpacity" }),
        S("lookNote", null, page: "overlay", help: "Saved changes reach an overlay that is open in OBS straight away. Show a sample round to see them there.", kind: "note", section: "Look"),
        S("cssNote", null, page: "overlay", help: "Anything else goes into the Browser source's Custom CSS in OBS, which also wins over the colours above, for example :root { --tg-width: 980px; --tg-radius: 8px; }. Add &platform=twitch to the URL to show only one platform's rounds, and &debug=1 to see whether it reached Streamer.bot.", kind: "note", section: "Make it your own"),
        Msg("Round", "questionPrefixNormal", "❓", "Prefix (Normal)", "First line of a normal round's question message.", new string[0], alias: "QuestionPrefixNormal"),
        Msg("Round", "questionPrefixBlitz", "⚡ Blitz Trivia ⚡", "Prefix (Blitz)", "First line of a blitz round's question message. Worth making visually distinct , it is the only warning viewers get that the round ends on the first correct answer.", new string[0], alias: "QuestionPrefixBlitz"),
        Msg("Round", "questionOptionFormat", "▶ {index}. {answer}", "Option Format", "Layout of each answer option, repeated once per option.", new[] { "index", "answer" }, alias: "QuestionOptionFormat"),
        Msg("Round", "questionFooterDetailed", "| 🌟 {difficulty} | 📚 {category} | ⏳ {seconds}s to answer!", "Footer (Detailed)", "Last line of the question message while 'Show difficulty and category with the question' is on.", new[] { "difficulty", "category", "seconds" }, alias: "QuestionFooterDetailed"),
        Msg("Round", "categoryChosen", "📚 Category for this round: {category}", "Category Chosen", "Sent once when a viewer named a category while starting a round. Clear this box to start silently.", new[] { "category" }, alias: "CategoryChosen"),
        Msg("Round", "questionFooterSimple", "| ⏳ {seconds}s to answer!", "Footer (Simple)", "Last line of the question message while 'Show difficulty and category with the question' is off.", new[] { "seconds" }, alias: "QuestionFooterSimple"),
        Msg("Round", "firstCountdownReminder", "⏰ {seconds} seconds left!", "Countdown 1", "First time reminder, fired at the 'First reminder at' value for that round type.", new[] { "seconds" }, alias: "FirstCountdownReminder"),
        Msg("Round", "secondCountdownReminder", "⏰ {seconds} seconds left!", "Countdown 2", "Second reminder, fired at half of the first one.", new[] { "seconds" }, alias: "SecondCountdownReminder"),
        Msg("Round", "lastCountdownReminder", "⏰ Hurry! {seconds} seconds left!", "Countdown 3", "Final reminder, fired at half of the second one and capped at 5 seconds.", new[] { "seconds" }, alias: "LastCountdownReminder"),
        Msg("Round", "seriesStarted", "🎬 Starting {rounds} trivia rounds back to back!", "Series Started", "Sent once when a start command queues several rounds.", new[] { "rounds" }, alias: "SeriesStarted"),
        Msg("Round", "seriesRoundHeader", "📍 Round {current} of {total}", "Round Header", "Sent immediately before each question in a series, so chat can see how far along it is. Never used for a single round.", new[] { "current", "total" }, alias: "SeriesRoundHeader"),
        Msg("Round", "seriesFinished", "🏁 That's all {total} rounds — thanks for playing!", "Series Finished", "Sent once after the last round of a series resolves.", new[] { "total" }, alias: "SeriesFinished"),
        Msg("Round", "seriesCancelled", "🛑 The remaining {remaining} round(s) were cancelled.", "Series Cancelled", "Sent when queued rounds are dropped , a moderator force ended the series, or no question could be fetched.", new[] { "remaining" }, alias: "SeriesCancelled"),
        Msg("Round", "seriesClamped", "@{userName}, {requested} is above the limit — running {rounds} rounds instead.", "Too Many Requested", "Sent when somebody asks for more rounds than Max per command allows. The series still runs, just at the limit.", new[] { "userName", "requested", "rounds" }, alias: "SeriesClamped"),
        Msg("Round", "skipNotice", "⏭️ Question skipped!", "Skip Notice", "Sent when a round is skipped. The question is discarded without being scored, and no answer is revealed.", new string[0], alias: "SkipNotice"),
        Msg("Round", "forceEndNotice", "🛑 Forcefully ending the current question!", "Force End Notice", "Sent when a moderator force ends a round. Unlike skip, answers already submitted are still scored and the correct answer is revealed.", new string[0], alias: "ForceEndNotice"),
        Msg("Round", "fetchFailed", "❌ Failed to fetch a trivia question. Please try again later.", "Fetch Failed", "Sent when no question could be had , the source was unreachable or rate limited, IGDB is missing its keys, or nothing matches the category and difficulty. The Streamer.bot log says which.", new string[0], alias: "FetchFailed"),
        Msg("Answers", "answerAccepted", "@{userName}, your answer ‘{answer}’ is noted! ✅", "Answer Accepted", "Sent when an answer is recorded normally.", new[] { "userName", "answer" }, alias: "AnswerAccepted"),
        Msg("Answers", "answerRecognizedVariation", "@{userName}, answer recognized with slight variation: ‘{answer}’.", "Recognized Variation", "Replaces 'Answer Accepted' when the answer only matched after ignoring case, spacing and punctuation. Needs 'Ignoring punctuation and accents' on the Game page.", new[] { "userName", "answer" }, alias: "AnswerRecognizedVariation"),
        Msg("Answers", "answerAcceptedFuzzy", "@{userName}, ‘{answer}’ is close enough to ‘{correctAnswer}’ — recorded! ✅", "Accepted (Fuzzy)", "Replaces 'Answer Accepted' when the answer matched by close spelling. Needs 'Close spellings' on the Game page.", new[] { "userName", "answer", "correctAnswer" }, alias: "AnswerAcceptedFuzzy"),
        Msg("Answers", "alreadyAnswered", "@{userName}, you already submitted: ‘{answer}’ ⚠️", "Already Answered", "Sent when a viewer answers twice in one round. Only their first answer counts.", new[] { "userName", "answer" }, alias: "AlreadyAnswered"),
        Msg("Results", "winners", "🎉 Winners: {winners}! The correct answer was: ‘{correctAnswer}’. 🏆", "Winners (Normal Round)", "Sent at the end of a normal round when at least one viewer was right. Everyone correct wins, so this covers one winner or many.", new[] { "winners", "correctAnswer" }, alias: "Winners"),
        Msg("Results", "blitzWinner", "🎉 Winner: @{userName}! The correct answer was: ‘{correctAnswer}’. 🏆", "Winner (Blitz Round)", "Sent when a blitz round is won. Blitz ends the instant somebody is right, so there is only ever one winner.", new[] { "userName", "correctAnswer" }, alias: "BlitzWinner"),
        Msg("Results", "noCorrectAnswer", "❌ Nobody answered correctly. The answer was: ‘{correctAnswer}’.", "Nobody Correct", "Sent when a round ends with nobody correct.", new[] { "correctAnswer" }, alias: "NoCorrectAnswer"),
        Msg("Results", "pointsAwarded", "🏅 @{userName} earned {points} {pointsName}! Total: {total} {pointsName}.", "Points Awarded", "Sent when exactly one viewer is paid out.", new[] { "userName", "points", "pointsName", "total" }, alias: "PointsAwarded"),
        Msg("Results", "pointsAwardedGrouped", "🏅 {winners} each earned {points} {pointsName}!", "Points Awarded (Multiple)", "Used instead of 'Points Awarded' when a round has several winners, to keep the bot under its chat rate limit. Winners are grouped by amount, so a sub or VIP boost produces a second line rather than a wrong figure.", new[] { "winners", "points", "pointsName" }, alias: "PointsAwardedGrouped"),
        Msg("Scoreboard", "scoreboardHeader", "🏆 {platform} Trivia Leaderboard 🏆", "Header", "Title line of the leaderboard. Rows are packed into as few chat messages as fit, so this may share a message with the first entries.", new[] { "platform" }, alias: "ScoreboardHeader"),
        Msg("Scoreboard", "scoreboardEntry", " {medal}{rank}. @{user} — Wins: {wins}, Points: {points}", "Entry", "One leaderboard row, repeated for each ranked viewer.", new[] { "medal", "rank", "user", "wins", "points" }, alias: "ScoreboardEntry"),
        Msg("Scoreboard", "scoreboardEntryBlitz", "  ⚡ Blitz: {blitzwins} wins, {blitzpoints} pts", "Entry (Blitz)", "Appended to a leaderboard row, but only for viewers with at least one blitz win or blitz point.", new[] { "blitzwins", "blitzpoints" }, alias: "ScoreboardEntryBlitz"),
        Msg("Scoreboard", "scoreboardEmpty", "The leaderboard is currently empty. Be the first to appear! 🌟", "Board Empty", "Sent when nobody has any stats yet, or everyone with stats is excluded from the board.", new string[0], alias: "ScoreboardEmpty"),
        Msg("Scoreboard", "personalStats", "@{user}, your {platform} Trivia stats: {wins} wins, {points} pts. ⚡ Blitz: {blitzwins} wins, {blitzpoints} pts.", "Own Stats", "Reply when a viewer looks up their own stats.", new[] { "user", "platform", "wins", "points", "blitzwins", "blitzpoints" }, alias: "PersonalStats"),
        Msg("Scoreboard", "targetStats", "@{user}'s {platform} Trivia stats: {wins} wins, {points} pts. ⚡ Blitz: {blitzwins} wins, {blitzpoints} pts.", "Someone Else's Stats", "Reply when a viewer looks up somebody else's stats.", new[] { "user", "platform", "wins", "points", "blitzwins", "blitzpoints" }, alias: "TargetStats"),
        Msg("Scoreboard", "noStats", "@{user} has no Trivia stats yet. 📭", "No Stats Yet", "Reply when the viewer looked up exists but has never won or scored.", new[] { "user" }, alias: "NoStats"),
        Msg("Scoreboard", "userNotFound", "❓ Could not find user '{target}'.", "User Not Found", "Reply when the name looked up matches nobody with trivia stats.", new[] { "target" }, alias: "UserNotFound"),
        Msg("Scoreboard", "noInputUsage", "@{user}, please specify a username. Usage: !TSS @Username", "No Username Given", "Reply when the stats lookup command is used without a username.", new[] { "user" }, alias: "NoInputUsage"),
        Msg("Scoreboard", "callerNotOnBoard", "@{user}, you are not in the top {topn}.", "Not On The Board", "Sent after the leaderboard when the viewer who asked for it has stats but did not make the top N.", new[] { "user", "topn" }, alias: "CallerNotOnBoard"),
        Msg("Scoreboard", "callerFooter", "🌟 Your stats: {wins} wins, {points} pts  ⚡ Blitz: {blitzwins} wins, {blitzpoints} pts", "Their Own Numbers", "Follows 'Not On The Board' with that viewer's own figures.", new[] { "wins", "points", "blitzwins", "blitzpoints" }, alias: "CallerFooter"),
        Msg("Scoreboard", "monthlyReset", "🔄 Trivia {platform} leaderboard has been reset for the new month!", "Monthly Reset", "Announced once when the leaderboard is wiped by the auto reset.", new[] { "platform" }, alias: "MonthlyReset"),
        Msg("Addicted", "addictedUsage", "@{userName}, usage: !addicted add <user>  or  !addicted remove <user>", "Usage", "Sent when the addicted command is used with no target.", new[] { "userName" }, alias: "AddictedUsage"),
        Msg("Addicted", "addictedAdded", "@{userName}, {targetUser} added to the addicted list — 50% win penalty applied. 🙏", "Added", "Sent when a viewer is added to the addicted list. They then win only half their normal points.", new[] { "userName", "targetUser" }, alias: "AddictedAdded"),
        Msg("Addicted", "addictedAlreadyOnList", "@{userName}, {targetUser} is already on the addicted list.", "Already On List", "Sent when adding a viewer who is already on the addicted list.", new[] { "userName", "targetUser" }, alias: "AddictedAlreadyOnList"),
        Msg("Addicted", "addictedRemoved", "@{userName}, {targetUser} removed from the addicted list.", "Removed", "Sent when a viewer is taken off the addicted list.", new[] { "userName", "targetUser" }, alias: "AddictedRemoved"),
        Msg("Addicted", "addictedNotOnList", "@{userName}, {targetUser} is not on the addicted list.", "Not On List", "Sent when removing a viewer who was not on the addicted list.", new[] { "userName", "targetUser" }, alias: "AddictedNotOnList"),
        Msg("Suggestions", "suggestAccepted", "✅ Thanks @{userName} — your question joined the {category} pool!", "Accepted", "Sent when a suggestion has been stored.", new[] { "userName", "category" }, alias: "SuggestAccepted"),
        Msg("Suggestions", "suggestNotAllowed", "@{userName}, you cannot suggest questions — that needs the channel point reward or the right group.", "No Permission", "Sent when somebody without the reward, the group or moderator status tries to suggest.", new[] { "userName" }, alias: "SuggestNotAllowed"),
        Msg("Suggestions", "suggestDisabled", "@{userName}, question suggestions are switched off right now.", "Suggestions Off", "Sent when the command is used while 'Allow question suggestions' is off.", new[] { "userName" }, alias: "SuggestDisabled"),
        Msg("Suggestions", "suggestTooMany", "@{userName}, you already have {count} question(s) in the pool — that is the limit.", "Too Many Already", "Sent when the viewer is already at the 'Max per viewer' limit.", new[] { "userName", "count" }, alias: "SuggestTooMany"),
        Msg("Suggestions", "suggestUsage", "@{userName}, usage: !suggest <question> | <correct answer> | <wrong answer> | <wrong answer>", "Usage", "Sent when the message did not carry a question, a correct answer and at least one wrong answer.", new[] { "userName" }, alias: "SuggestUsage"),
        Msg("Suggestions", "suggestBadAnswers", "@{userName}, a suggestion needs a correct answer plus at least one different wrong answer.", "Needs Better Answers", "Sent when nothing usable was left after dropping wrong answers that repeat the correct one.", new[] { "userName" }, alias: "SuggestBadAnswers"),
        Msg("Suggestions", "suggestTooLong", "@{userName}, keep the question under {max} characters and every answer under {answerMax}.", "Too Long", "Sent when the question or one of the answers is over the length limits.", new[] { "userName", "max", "answerMax" }, alias: "SuggestTooLong"),
        Msg("Suggestions", "suggestDuplicate", "@{userName}, that question is already in the pool.", "Already In The Pool", "Sent when the same question is already in the list, ignoring case and punctuation.", new[] { "userName" }, alias: "SuggestDuplicate"),
        Msg("Suggestions", "suggestFailed", "@{userName}, your suggestion could not be saved — please try again later.", "Could Not Save", "Sent when the question file could not be written. The action log carries the reason.", new[] { "userName" }, alias: "SuggestFailed"),
        Msg("Cooldown", "globalCooldownMessage", "Please wait {seconds} seconds before trying again ⏳", "Global Cooldown", "Sent when the start command is used while the global cooldown is still running.", new[] { "seconds" }, alias: "GlobalCooldownMessage"),
        Msg("Cooldown", "userCooldownMessage", "@{userName}, please wait {seconds} seconds before trying again ⏳", "User Cooldown", "Sent when a viewer uses the start command while their personal cooldown is still running.", new[] { "userName", "seconds" }, alias: "UserCooldownMessage"),
        Msg("Channel points", "redemptionRefunded", "↩️ @{userName}, your channel point redemption was refunded.", "Redemption Refunded", "Sent when a redemption could not start a round and the viewer's channel points were given back. Clear this box to refund silently.", new[] { "userName" }, alias: "RedemptionRefunded"),
        Msg("Auto start", "autoStartAnnouncement", "🎲 Trivia time! Here comes a question — next one in about {minutes} minutes.", "Auto Start Notice", "Sent just before the question of an automatic round, so chat can tell it apart from one somebody asked for. Clear this box to start silently.", new[] { "minutes", "rounds" }, alias: "AutoStartAnnouncement"),
        Msg("Errors", "categoryUnknown", "@{userName}, I have no category called ‘{category}’. Available: {categories}", "Unknown Category", "Sent when a viewer names a category that cannot be played. The round is cancelled either way rather than quietly asking about something else; clearing this box only makes that happen silently.", new[] { "userName", "category", "categories" }, alias: "CategoryUnknown"),
        Msg("Errors", "modOnlyStart", "@{userName}, only moderators or the broadcaster can start a new game.", "Mod Only — Start", "Sent when a viewer tries to start a round while 'Only moderators may start' is on.", new[] { "userName" }, alias: "ModOnlyStart"),
        Msg("Errors", "modOnlySkip", "@{userName}, only moderators or the broadcaster can skip the running game.", "Mod Only — Skip", "Sent when a viewer tries to skip while 'Only moderators may skip' is on.", new[] { "userName" }, alias: "ModOnlySkip"),
        Msg("Errors", "modOnlyForceEnd", "@{userName}, only moderators or the broadcaster can use this command.", "Mod Only — Force End", "Sent when a viewer tries to force end a round. Force end is always mod-only.", new[] { "userName" }, alias: "ModOnlyForceEnd"),
        Msg("Errors", "alreadyActive", "⚠️ A trivia round is already active.", "Already Active", "Sent when someone starts a round while one is already running.", new string[0], alias: "AlreadyActive"),
        Msg("Errors", "cannotSkip", "⚠️ Cannot skip — no round is currently accepting answers.", "Cannot Skip", "Sent when skip is used but no round is currently accepting answers.", new string[0], alias: "CannotSkip"),
        Msg("Errors", "cannotForceEnd", "⚠️ Cannot force end — no round is currently accepting answers.", "Cannot Force End", "Sent when force end is used but no round is currently accepting answers.", new string[0], alias: "CannotForceEnd"),
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
