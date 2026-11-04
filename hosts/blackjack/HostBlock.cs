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
    public static string Var(string actionName) =>
        "uiframework.schema." + System.Text.RegularExpressions.Regex.Replace((actionName ?? "").Trim(), @"[^\w .-]", "_");

    public static Setting Find(string key)
    {
        foreach (var s in Table)
            if (s.Key == key) return s;
        return null;
    }

    // ---- The window: title, pages, help, about, log. Null: the settings action carries a spec of its own. ----
    public static readonly UiWindow Window = null; // __WINDOW__

    // ---- The table: every setting the host reads, in page order. ----
    public static readonly Setting[] Table =
    {
        // ---- table ----
        S("freeGame", true, page: "table", label: "Free Game", help: "Hands are played for nothing: no stake is taken, no winnings are paid, and every message about points\nswitches to its Free Game wording. House Mode refuses to open a table while this is on.", section: "Betting", aliases: new[] { "FreeGame" }),
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
        S("setupObsOverlay", null, page: "overlay", label: "Create the browser source in OBS", help: "Creates the scene above if it is missing and adds a browser source pointing at the overlay file. Needs OBS connected in Streamer.bot.", kind: "action", section: "Set it up in OBS", confirm: "This creates the scene if it is missing and adds a browser source pointing at the overlay file, over the OBS connection set above. Go ahead?"),
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
    public static string AnswerVar(string actionName) =>
        "uiframework.answer." + System.Text.RegularExpressions.Regex.Replace((actionName ?? "").Trim(), @"[^\w .-]", "_");

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
