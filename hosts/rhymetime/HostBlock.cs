// --- UIFramework host block (keep identical in every action of this host that reads the settings) ---
// From UIFramework/stub/HostSchema.cs. The game declares its settings once in UiSchema.Table, reads them through
// UiSettings, and answers the window's describe request with UiSchema.ToJson(). One table, so the game and its
// window can never disagree. Compiles in any Streamer.bot action with no reference beyond Newtonsoft.Json.
//
// In Execute(), first thing:
//     if (args.ContainsKey("uiframework.describe"))
//     {
//         CPH.SetGlobalVar("uiframework.schema." + ActionName, UiSchema.ToJson("rhymetime", CurrentVersion, "Chat Games/RhymeTime/RT-settings.json"), false);
//         return true;
//     }
public static class UiSchema
{
    public sealed class Setting
    {
        public string Key; public object Default; public string Kind, Page, Section, Group, Label, Help, Unit, Requires, Confirm;
        public double? Min, Max, Factor; public bool UnlimitedAtZero, Hidden;
        public string[] Options, Placeholders, Aliases; public double? AliasFactor;
        // For a list: its columns as "key|kind|label" (kinds: string, text, int, double, bool), and the file next
        // to the settings file that holds the rows, with the property in it (no field: the file is the array).
        public string[] Columns; public string File, Field;
    }

    /// <summary>A setting: key and default, then whatever the conventions cannot know. Keys the conventions know
    /// (useBot, globalCooldown, blockedUsers, ...) need nothing but their default. An option is "value" or
    /// "value|label". aliasFactor multiplies the first alias's value on the move (a fraction to percent: 100).</summary>
    public static Setting S(string key, object @default, string page = null, string label = null, string help = null, string kind = null,
        string section = null, double? min = null, double? max = null, string unit = null, double? factor = null, bool unlimitedAtZero = false,
        string[] options = null, string requires = null, string confirm = null, bool hidden = false, string[] aliases = null, double? aliasFactor = null,
        string[] columns = null, string file = null, string field = null)
    {
        return new Setting
        {
            Key = key, Default = @default, Page = page, Label = label, Help = help, Kind = kind, Section = section, Min = min, Max = max,
            Unit = unit, Factor = factor, UnlimitedAtZero = unlimitedAtZero, Options = options, Requires = requires, Confirm = confirm,
            Hidden = hidden, Aliases = aliases, AliasFactor = aliasFactor, Columns = columns, File = file, Field = field
        };
    }

    /// <summary>A message template: group, key, default text, label, help, placeholders without braces, old key.</summary>
    public static Setting Msg(string group, string key, string @default, string label, string help, string[] placeholders, string alias = null)
        => new Setting { Key = key, Default = @default, Kind = "template", Page = "messages", Group = group, Label = label, Help = help,
                         Placeholders = placeholders, Aliases = alias == null ? null : new[] { alias } };

    public static string ToJson(string host, string version, string settingsFile)
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
                    columns.Add(new JObject { ["key"] = parts[0], ["kind"] = parts.Length > 1 ? parts[1] : "string", ["label"] = parts.Length > 2 ? parts[2] : parts[0] });
                }
                item["columns"] = columns;
            }
            if (s.File != null) item["file"] = s.File;
            if (s.Field != null) item["field"] = s.Field;
            if (s.Aliases != null) item["aliases"] = new JArray(s.Aliases);
            if (s.AliasFactor.HasValue && s.Aliases != null && s.Aliases.Length > 0) item["aliasFactors"] = new JObject { [s.Aliases[0]] = s.AliasFactor.Value };
            settings.Add(item);
        }
        return new JObject { ["host"] = host, ["version"] = version, ["settingsFile"] = settingsFile, ["settings"] = settings }.ToString(Formatting.None);
    }

    public static Setting Find(string key)
    {
        foreach (var s in Table)
            if (s.Key == key) return s;
        return null;
    }

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
        // ---- overlay ----
        S("overlayEnabled", false, page: "overlay", label: "Send rounds to the overlay", help: "A browser source that shows the word being rhymed with its countdown, then the rhyme that won it and what it paid. It listens to Streamer.bot's own WebSocket server. Off, and the game sends it nothing.", section: "OBS overlay", aliases: new[] { "OverlayEnabled" }),
        S("overlayPort", 8080, page: "overlay", label: "Port", help: "The port of Streamer.bot's WebSocket server (Servers/Clients > WebSocket Server, tick Auto Start). 8080 unless you changed it there. With its authentication on, add &password= and the password to the end of the URL in OBS.", section: "Streamer.bot's WebSocket server", min: 1.0, max: 65535.0, aliases: new[] { "OverlayPort" }),
        S("obsConnectionId", 0, page: "overlay", label: "Connection #", help: "Which of Streamer.bot's OBS connections to use, if you have more than one. 0 is the first.", section: "Set it up in OBS", min: 0.0, aliases: new[] { "ObsConnectionId" }),
        S("obsSceneName", "RhymeTime", page: "overlay", label: "Scene", help: "The scene the source is added to. It is created if it is not there yet.", section: "Set it up in OBS", aliases: new[] { "ObsSceneName" }),
        S("obsSourceName", "RhymeTime Overlay", page: "overlay", label: "Source", help: "The browser source created in that scene. Nothing is touched if OBS already has a source of this name.", section: "Set it up in OBS", aliases: new[] { "ObsSourceName" }),
        S("setupObsOverlay", null, page: "overlay", label: "Set the overlay up in OBS", help: "Creates the scene if it is missing and adds a browser source the size of your canvas pointed at the overlay URL. OBS has to be connected in Streamer.bot under Stream Apps > OBS Studio. Save first: the setup reads the saved port and names.", kind: "action", section: "Set it up in OBS", confirm: "This creates the scene if it is missing and adds a browser source pointing at the overlay URL, over the OBS connection set above. Save your changes first if you edited the port or the names. Go ahead?"),
        S("copyOverlayUrl", null, page: "overlay", label: "Copy the overlay URL", help: "Writes the overlay page and copies its address for a Browser source in OBS: untick 'Local file', paste the URL, set the size to 1920 × 1080. The URL takes &platform=twitch for one platform's rounds only, &lang=de for German wording, &demo=1 to place the source, and &debug=1 to see whether it reached Streamer.bot.", kind: "action", section: "Or add it by hand"),
        S("openOverlayDemo", null, page: "overlay", label: "Try the demo", help: "Opens the overlay in your browser, playing sample rounds without Streamer.bot.", kind: "action", section: "Or add it by hand"),
        S("openOverlayFolder", null, page: "overlay", label: "Open the overlay folder", help: "Opens Chat Games\\RhymeTime\\Overlay, where the page is written.", kind: "action", section: "Or add it by hand"),
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
        Msg("Bets", "invalidBetMessage", "{user}, {reason}", "Bet refused", "The frame around the four reasons below; {reason} is replaced by whichever one applies.", new[] { "user", "reason", "currency" }, alias: "InvalidBetMessage"),
        Msg("Bets", "reasonNegativeAmount", "please specify a valid bet amount greater than 0.", "Reason: not a number", "Fills {reason} when the amount could not be read, or is not above zero.", new[] { "currency" }, alias: "ReasonNegativeAmount"),
        Msg("Bets", "reasonInsufficientFunds", "you don't have enough {currency}.", "Reason: cannot afford it", "Fills {reason} when the bet is more than the chatter has.", new[] { "currency" }, alias: "ReasonInsufficientFunds"),
        Msg("Bets", "reasonBelowMinimum", "minimum bet is {min} {currency}.", "Reason: under the minimum", "Fills {reason} when the bet is below the minimum on the Game page.", new[] { "min", "currency" }, alias: "ReasonBelowMinimum"),
        Msg("Bets", "reasonAboveMaximum", "maximum bet is {max} {currency}.", "Reason: over the maximum", "Fills {reason} when the bet is above the maximum on the Game page.", new[] { "max", "currency" }, alias: "ReasonAboveMaximum"),
        Msg("Cooldown", "globalCooldownMessage", "Please wait {seconds} seconds before trying again ⏳", "Everyone's cooldown", "Sent when the command is still on cooldown for the whole chat.", new[] { "seconds" }, alias: "GlobalMessage"),
        Msg("Cooldown", "userCooldownMessage", "@{user}, please wait {seconds} seconds before trying again ⏳", "A chatter's cooldown", "Sent when the command is still on cooldown for that one chatter.", new[] { "user", "seconds" }, alias: "UserMessage"),
    };
}

/// <summary>The settings file as the game reads it: every key through its default, old key names tolerated. Values
/// an alias stored in another scale (a fraction where percent is stored now) are converted on the way.</summary>
public sealed class UiSettings
{
    private readonly JObject _root;

    private UiSettings(JObject root) { _root = root; }

    /// <summary>Settings from JSON text, for tests and callers that already hold the file.</summary>
    public static UiSettings Parse(string json)
    {
        using (var reader = new JsonTextReader(new StringReader(json ?? "{}")) { DateParseHandling = DateParseHandling.None })
            return new UiSettings(JObject.Load(reader));
    }

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
                var old = _root[setting.Aliases[i]];
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
