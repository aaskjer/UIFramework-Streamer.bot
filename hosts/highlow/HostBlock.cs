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
    public static readonly UiWindow Window = new UiWindow("High-Low Game", releases: "aaskjer/High-Low-Game", logPrefix: "[High-Low Game/Settings]")
        .Pages(P("game", "Game", "🎲"), P("channelPoints", "Channel points", "💜"), P("cooldowns", "Cooldowns", "⏳"), P("access", "Access", "👥"), P("leaderboard", "Scoreboard", "🏆"), P("messages", "Messages", "💬"), P("help", "How to play", "📖"), P("about", "About", "ℹ️"))
        .Senders(useBot: "useBot", sendAction: "sendAction", cooldownUseBot: "cooldownUseBot", cooldownSendAction: "cooldownSendAction", numberSeparator: "numberSeparator", cooldownGroup: "Cooldown")
        .Pill("isChannelPointsTrigger", "Channel points", "Chat commands", tooltip: "How guesses come in on Twitch. YouTube and Kick always use the chat commands.")
        .Samples("guess", "!high", "result", "correct", "emoji", "🙂", "previousNumber", "42", "newNumber", "77", "number", "42", "sessionGuesses", "3", "alltimeGuesses", "57", "sign", "won", "pointsDelta", "1.234", "pointsSection", " won 1.234 Points", "currency", "Points", "seconds", "12", "amount", "500")
        .Cooldowns(
            C("dc2d4f17-d5f1-4228-923e-5c03652bb30f", "highGlobalCooldown", "highUserCooldown"),
            C("2b7f7ba1-b8a6-4b45-b2ef-31b1ee61f7be", "lowGlobalCooldown", "lowUserCooldown"),
            C("36a22b8f-5e63-4df7-ae8f-3439dd7d6475", "equalGlobalCooldown", "equalUserCooldown"),
            C("68f97444-4f77-4bab-8277-2a920fcdfddf", "alltimeGlobalCooldown", "alltimeUserCooldown"),
            C("5911cfbc-32cb-4749-bb40-3fce8eb76a1a", "sessionGlobalCooldown", "sessionUserCooldown"),
            C("df94341a-8b15-4c6c-9e1a-6af383915f8b", "personalGlobalCooldown", "personalUserCooldown"))
        .Help(
            H("Guessing",
                Note("There is always a number from 1 to 100 waiting; it is drawn when Streamer.bot starts. Viewers guess whether the next number will be higher, lower or the same. The answer names both numbers, and the new one is what the next guess is measured against."),
                Cmd("!high", "the next number is higher. Right pays the win amount, wrong costs nothing."),
                Cmd("!low", "the next number is lower. Same payout as !high."),
                Cmd("!equal", "the next number is the same. Right pays double, wrong costs the win amount."),
                Cmd("!highlowsetup", "draws a fresh number and announces it. Optional; moderators only, set on the command itself.")),
            H("Scoreboards",
                Cmd("!hlgsession", "the best players of this Streamer.bot session."),
                Cmd("!hlgalltime", "the best players of all time."),
                Cmd("!hlgpersonal", "your own session and all-time stats.")),
            H("Channel points (Twitch)",
                Note("Create one reward each for high, low and equal and paste their IDs on the Channel points page. The game listens to every redemption and plays only those three. Leave 'Skip Reward Requests Queue' off on the rewards, or Twitch cannot refund a redemption that does not play."),
                Note("Cost and Fixed spend the channel points as the stake: a correct guess pays the stake in points, double on equal. Input lets the viewer type a bet in their own points; only a wrong equal costs it. Free plays like the chat commands.")),
            H("Where things are kept",
                Cmd("HLG-Settings.json", "everything on these pages."),
                Cmd("uiframework.layout.json", "your layout of this window: theme, renamed or hidden pages and fields."),
                Note("Both sit in Streamer.bot's folder under Chat Games\\High-Low Game. Permissions and the cooldowns themselves live on the commands; saving writes the Cooldowns page onto them.")))
        .About(credits: "High-Low Game by aaskjer", links: new[] { Link("Repository", "https://github.com/aaskjer/High-Low-Game"), Link("Releases", "https://github.com/aaskjer/High-Low-Game/releases"), Link("Extension page", "https://extensions.streamer.bot/t/high-low-game/2781"), Link("StreamUP - Currency Core", "https://streamup.tips/product/currency-core") });

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
        Msg("Channel points", "redemptionRefunded", "↩️ @{user}, your channel point redemption was refunded.", "Redemption refunded", "Sent when a redemption gets its channel points back. Empty it to refund quietly.", new[] { "user" }),
        Msg("Channel points", "reasonNoInput", "@{user}, type how many {pointsName} you want to bet into the redemption.", "Bet: nothing typed", "Input mode: the redemption came without a bet.", new[] { "user", "pointsName" }),
        Msg("Channel points", "reasonNegativeAmount", "@{user}, '{amount}' is not a bet. Use a number above 0, all, half, third, quarter or random.", "Bet: not a number", "Input mode: the typed bet could not be read or is not above zero.", new[] { "user", "amount", "pointsName" }),
        Msg("Channel points", "reasonInsufficientFunds", "@{user}, you only have {currentPoints} {pointsName}.", "Bet: not enough points", "Input mode: the bet is more than the viewer has.", new[] { "user", "amount", "currentPoints", "pointsName" }),
        Msg("Channel points", "reasonBelowMinimum", "@{user}, the minimum bet is {min} {pointsName}.", "Bet: under the minimum", "Input and Fixed mode: the bet is below the minimum.", new[] { "user", "amount", "min", "pointsName" }),
        Msg("Channel points", "reasonAboveMaximum", "@{user}, the maximum bet is {max} {pointsName}.", "Bet: over the maximum", "Input and Fixed mode: the bet is above the maximum.", new[] { "user", "amount", "max", "pointsName" }),
        Msg("Cooldown", "globalCooldownMessage", "Please wait {seconds} seconds before trying again ⏳", "Everyone's cooldown", "Sent while a command or reward is on cooldown for the whole chat.", new[] { "user", "seconds" }, alias: "GlobalMessage"),
        Msg("Cooldown", "userCooldownMessage", "@{user}, please wait {seconds} seconds before trying again ⏳", "A viewer's cooldown", "Sent while a command or reward is on cooldown for that one viewer.", new[] { "user", "seconds" }, alias: "UserMessage"),
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

    /// <param name="title">Window title and About heading.</param>
    /// <param name="releases">"owner/repo" on GitHub: the repository and releases links, the update check.</param>
    /// <param name="logo">URL of the logo, for the window icon and About.</param>
    public UiWindow(string title, string releases = null, string logo = null, string logPrefix = null, double? width = null, double? height = null)
    {
        Title = title; Releases = releases; Logo = logo; LogPrefix = logPrefix; Width = width; Height = height;
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
