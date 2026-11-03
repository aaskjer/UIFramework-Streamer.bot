// --- UIFramework host block (keep identical in every action of this host that reads the settings) ---
// From UIFramework/stub/HostSchema.cs. The game declares its settings once in UiSchema.Table, reads them through
// UiSettings, and answers the window's describe request with UiSchema.ToJson(). One table, so the game and its
// window can never disagree. Compiles in any Streamer.bot action with no reference beyond Newtonsoft.Json.
//
// In Execute(), first thing:
//     if (args.ContainsKey("uiframework.describe"))
//     {
//         CPH.SetGlobalVar("uiframework.schema." + ActionName, UiSchema.ToJson("highlow", CurrentVersion, "Chat Games/High-Low Game/HLG-Settings.json"), false);
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
        S("awardPoints", true, page: "game", label: "Award points", help: "Off, and the game is played for fun: nobody wins or loses points, the guesses still count for the scoreboards.", section: "Points", aliases: new[] { "AwardPoints" }),
        S("wonPoints", 1234, page: "game", label: "Win amount", help: "Paid for a correct !high or !low in chat. A correct !equal pays double and a wrong !equal costs this amount. A wrong !high or !low costs nothing.", section: "Points", min: 0.0, requires: "awardPoints", aliases: new[] { "WonPoints" }),
        S("equalChance", 10, page: "game", label: "Equal chance", help: "How often an !equal guess draws the same number again. Every other draw is a free pick from 1 to 100.", kind: "percent", section: "Numbers", min: 0.0, max: 100.0),
        S("pointsVariable", "points", page: "game", help: "The Streamer.bot user variable that winnings are added to and losses taken from. Currency Core by StreamUP is found on its own and overrides this.", section: "Currency", aliases: new[] { "PointsVar" }),
        S("pointsName", "Points", page: "game", help: "What your currency is called in chat; it fills {currency} and {pointsName}. Overridden by Currency Core when it is installed.", section: "Currency", aliases: new[] { "PointsName" }),
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
