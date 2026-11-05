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
    public static readonly UiWindow Window = new UiWindow("Hangman Game", releases: "aaskjer/Hangman-Game", logo: "https://github.com/aaskjer/Hangman-Game/blob/main/Assets/HM/Hangman-Game-Full2.png?raw=true", logPrefix: "[Hangman Game/Settings]")
        .Pages(P("game", "Game", "🎮"), P("words", "Words", "🔤"), P("overlay", "Overlay", "📺"), P("access", "Access", "👥"), P("messages", "Messages", "💬"), P("board", "Board", "🏆"), P("log", "Log", "📜"), P("help", "How to play", "📖"), P("about", "About", "ℹ️"))
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
        .Answer("openWordlistFolder", "Opened Chat Games\\Hangman Game.")
        .Answer("openOverlayFolder", "Opened Chat Games\\Hangman Game\\Overlay.")
        .Log("Chat Games/Logs", "HM-log*.txt", writeLogKey: "logToFile", columns: new[] { "Player|User", "Platform|Platform", "Round|Round", "Word|Word", "Guess|Guess", "Lives|Lives", "Wrong|Wrong", "Reward|Reward" },
             results: new[] { "START", "HIT-LETTR", "MISS-LTTR", "DUPE-LTTR", "WIN-LETTR", "WIN-WORD", "MISS-WORD", "MISS-PART", "DUPE-WORD", "GAME-OVER", "TIMEOUT", "STOP", "EVIL-SWAP", "GLITCH-HOLD", "STREAK-END", "AWARD-WIN", "AWARD-LTER", "CP-START", "CP-BAD-INPUT", "REFUND", "BLOCKED", "COOLDOWN", "ANTICHEAT", "ADDICTED", "FREE-START", "FREE-END" },
             platforms: new[] { "twitch", "youtube", "kick" }, emptyText: "No rounds logged yet. Every round, guess and payout lands here once 'Write a log file per day' is on.")
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
                Note("Per viewer, on the platform they played on (Streamer.bot's user variables): hm_wins, hm_losses, hm_played, hm_pointsEarned, hm_lettersGuessed, hm_wordsGuessed, hm_streak, hm_bestStreak.")),
            H("Where things are kept",
                Cmd("configs.json", "everything on these pages, in Chat Games\\Hangman Game."),
                Cmd("Hangman-Wordlist.json", "the word list, next to it; the Words page edits it."),
                Cmd("uiframework.layout.json", "your layout of this window: theme, language, renamed or hidden pages and fields."),
                Cmd("hangman_overlay.html", "the overlay, in the overlay folder, with hangman_state.json beside it."),
                Cmd("HM-log-<date>.txt", "the log, in Chat Games\\Logs.")))
        .About(credits: "nate1280 / Streamer.bot and YOU ♡",
            sections: new[] { H("Needs a points system", Note("Rounds pay out with the points of a currency system. These two are picked up without any setup:"), Link("Currency Core by StreamUP", "https://streamup.tips/product/currency-core"), Link("LOYALTY POINTS by tawmae", "https://tawmae.xyz/loyalty-points"), Note("Anything else works too - name its variable on the Access page. Twitch channel points can start a round, but a win always pays in that currency.")) },
            links: new[] { Link("Hangman Game on the Streamer.bot Discord", "https://discord.com/channels/834650675224248362/1448251872744837203/1448251872744837203") })
        .Cooldowns(C("c49f751d-28cd-4c9e-87e9-6a29e843e0ae", "globalCooldown", "userCooldown"))   // !hangman
        // German: the game's own keys and texts; sections, pages and the shared keys come from the conventions.
        .Lang("de",
            Pg("words", "Wörter"),
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
            K("requireModToStart", "Nur Moderatoren starten eine Runde", "Nur Moderatoren und der Broadcaster starten eine Runde; raten darf in einer laufenden Runde weiter jeder."),
            K("allowChannelPointStart", "Eine Belohnung startet die Runde", "Auf Twitch startet eine Kanalpunkte-Einlösung die Runde statt des Chat-Befehls, der auf Twitch dann aus ist. Raten darf weiter jeder wie gewohnt."),
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
            K("resetTwitchMonthly", "Twitch: monatlich zurücksetzen", "Leert die Twitch-Rangliste am Monatsende."),
            K("resetTwitchDay", "Twitch: nach Tagen zurücksetzen", "Leert die Twitch-Rangliste alle so viele Tage. 0 = nie."),
            K("resetYouTubeMonthly", "YouTube: monatlich zurücksetzen", "Leert die YouTube-Rangliste am Monatsende."),
            K("resetYouTubeDay", "YouTube: nach Tagen zurücksetzen", "Leert die YouTube-Rangliste alle so viele Tage. 0 = nie."),
            K("resetKickMonthly", "Kick: monatlich zurücksetzen", "Leert die Kick-Rangliste am Monatsende."),
            K("resetKickDay", "Kick: nach Tagen zurücksetzen", "Leert die Kick-Rangliste alle so viele Tage. 0 = nie."),
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
            Tx("No rounds logged yet. Every round, guess and payout lands here once 'Write a log file per day' is on.", "Noch keine Runden protokolliert. Jede Runde, jeder Versuch und jede Auszahlung landet hier, sobald 'Protokoll je Tag schreiben' an ist."));

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
        S("importTopic", "countries", page: "words", label: "Topic", help: "Wikidata: countries, capitals, cities, rivers, mountains, elements, instruments, sports, dogs, or custom with a class below. OdeNet: animals, plants, food, objects, people, body, places, materials, nature, feelings.", kind: "options", section: "Import words", options: new[] { "countries|Countries (Wikidata)", "capitals|Capitals (Wikidata)", "cities|Big cities (Wikidata)", "rivers|Rivers (Wikidata)", "mountains|Mountains (Wikidata)", "elements|Chemical elements (Wikidata)", "instruments|Musical instruments (Wikidata)", "sports|Sports (Wikidata)", "dogs|Dog breeds (Wikidata)", "custom|Custom class (Wikidata Q-ID)", "noun.animal|Animals (OdeNet)", "noun.plant|Plants (OdeNet)", "noun.food|Food & drink (OdeNet)", "noun.artifact|Objects & tools (OdeNet)", "noun.person|People & jobs (OdeNet)", "noun.body|Body (OdeNet)", "noun.location|Places (OdeNet)", "noun.substance|Materials (OdeNet)", "noun.object|Nature (OdeNet)", "noun.feeling|Feelings (OdeNet)" }, requires: "importSource!=pokeapi", transient: true),
        S("importCustomClass", "", page: "words", label: "Wikidata class (Q-ID)", help: "For the custom topic: the Q-ID of the class whose instances become words, like Q11424 for films.", section: "Import words", requires: "importSource=wikidata", transient: true),
        S("importGenerationFrom", 1, page: "words", label: "From generation", help: "First Pokémon generation to include, 1 to 9.", section: "Import words", min: 1.0, max: 9.0, requires: "importSource=pokeapi", transient: true, row: "gen"),
        S("importGenerationTo", 9, page: "words", label: "To generation", help: "Last Pokémon generation to include, 1 to 9.", section: "Import words", min: 1.0, max: 9.0, requires: "importSource=pokeapi", transient: true, row: "gen"),
        S("importIgdbClientId", "", page: "words", label: "IGDB client ID", help: "Client ID of your Twitch developer app (dev.twitch.tv/console/apps). Kept in Streamer.bot's global variables after the first import, so it needs typing only once.", section: "Import words", requires: "importSource=igdb", transient: true),
        S("importIgdbClientSecret", "", page: "words", label: "IGDB client secret", help: "Client secret of that app; kept with the ID after the first import.", section: "Import words", requires: "importSource=igdb", transient: true),
        S("importIgdbMinRatings", 100, page: "words", label: "At least this many ratings on IGDB", help: "Only games with at least this many ratings on IGDB. Higher means better-known titles.", section: "Import words", min: 0.0, requires: "importSource=igdb", transient: true),
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
        // ---- access ----
        S("pointsVariable", "points", page: "access", section: "Currency", aliases: new[] { "PointsVar" }),
        S("pointsName", "Points", page: "access", section: "Currency", aliases: new[] { "PointsName" }),
        S("requireModToStart", true, page: "access", label: "Only moderators start a round", help: "Only moderators and the broadcaster can start a round; everyone can still guess in a round that is running.", section: "Who can start a round", aliases: new[] { "RequireModToStart" }),
        S("blockedUsers", "", page: "access", section: "Who may not play", aliases: new[] { "BlockedUsers" }),
        S("blockedGroups", "", page: "access", section: "Who may not play", aliases: new[] { "BlockedGroups" }),
        S("allowChannelPointStart", false, page: "access", label: "A reward starts the round", help: "On Twitch a channel point redemption starts the round instead of the chat command, which is then off on Twitch. Everyone still guesses as usual.", section: "Channel points (Twitch)", aliases: new[] { "AllowChannelPointStart" }, twitchOnly: true),
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
        S("resetTwitchMonthly", false, page: "board", label: "Twitch: reset monthly", help: "Clears the Twitch scoreboard at the end of the month.", section: "Auto reset", requires: "resetAllMonthly=false", aliases: new[] { "ResetTwitchMonthly" }, row: "resetTwitch"),
        S("resetTwitchDay", 0, page: "board", label: "Twitch: reset after days", help: "Clears the Twitch scoreboard every so many days. 0 = never.", section: "Auto reset", min: 0.0, max: 31.0, unlimitedAtZero: true, requires: "resetAllMonthly=false", aliases: new[] { "ResetTwitchDay" }, row: "resetTwitch"),
        S("resetYouTubeMonthly", false, page: "board", label: "YouTube: reset monthly", help: "Clears the YouTube scoreboard at the end of the month.", section: "Auto reset", requires: "resetAllMonthly=false", aliases: new[] { "ResetYouTubeMonthly" }, row: "resetYouTube"),
        S("resetYouTubeDay", 0, page: "board", label: "YouTube: reset after days", help: "Clears the YouTube scoreboard every so many days. 0 = never.", section: "Auto reset", min: 0.0, max: 31.0, unlimitedAtZero: true, requires: "resetAllMonthly=false", aliases: new[] { "ResetYouTubeDay" }, row: "resetYouTube"),
        S("resetKickMonthly", false, page: "board", label: "Kick: reset monthly", help: "Clears the Kick scoreboard at the end of the month.", section: "Auto reset", requires: "resetAllMonthly=false", aliases: new[] { "ResetKickMonthly" }, row: "resetKick"),
        S("resetKickDay", 0, page: "board", label: "Kick: reset after days", help: "Clears the Kick scoreboard every so many days. 0 = never.", section: "Auto reset", min: 0.0, max: 31.0, unlimitedAtZero: true, requires: "resetAllMonthly=false", aliases: new[] { "ResetKickDay" }, row: "resetKick"),
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
