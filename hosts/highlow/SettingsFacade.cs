public sealed class Settings
{
    private readonly UiSettings _s;

    private Settings(UiSettings s) { _s = s; }

    public Settings() : this(UiSettings.Parse("{}")) { }

    private static string BaseFolder
    {
        get
        {
            var folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Chat Games", "High-Low Game");
            Directory.CreateDirectory(folder);
            return folder;
        }
    }

    public static string FilePath => Path.Combine(BaseFolder, "HLG-Settings.json");

    public static Settings Load() => new Settings(UiSettings.Load(FilePath));

    /// <summary>Writes the Free mode switch into HLG-Settings.json and leaves everything else in the file as it
    /// is, so the window's pill and the next guess agree. Written beside the file and swapped in, the replaced
    /// file kept as .bak, like the window saves.</summary>
    public static void WriteFreeMode(bool on)
    {
        JObject root;
        try { root = File.Exists(FilePath) ? JObject.Parse(File.ReadAllText(FilePath)) : new JObject(); }
        catch (Exception) { root = new JObject(); }
        root["freeGame"] = on;
        string temp = FilePath + ".tmp";
        File.WriteAllText(temp, root.ToString(Newtonsoft.Json.Formatting.Indented));
        if (File.Exists(FilePath)) File.Replace(temp, FilePath, FilePath + ".bak");
        else File.Move(temp, FilePath);
    }

    public bool UseBot => _s.Bool("useBot");
    public bool SendAction => _s.Bool("sendAction");
    public bool UseBotCD => _s.Bool("cooldownUseBot");
    public bool SendActionCD => _s.Bool("cooldownSendAction");
    public string NumberSeparator => _s.String("numberSeparator");

    public bool AwardPoints => _s.Bool("awardPoints");
    public bool FreeGame => _s.Bool("freeGame");
    public long WonPoints => Math.Max(0L, _s.Long("wonPoints"));
    public double EqualChance => UiSettings.Clamp(_s.Double("equalChance"), 0.0, 100.0) / 100.0;
    public string PointsVar => _s.String("pointsVariable");
    public string PointsName => _s.String("pointsName");

    public bool IsChannelPointsTrigger => _s.Bool("isChannelPointsTrigger");
    public string RewardIdHigh => _s.String("rewardIdHigh").Trim();
    public string RewardIdLow => _s.String("rewardIdLow").Trim();
    public string RewardIdEqual => _s.String("rewardIdEqual").Trim();
    public string CpBetMode
    {
        get
        {
            string mode = _s.String("cpBetMode").Trim().ToLowerInvariant();
            return mode == "fixed" || mode == "input" || mode == "free" ? mode : "cost";
        }
    }
    public long FixedBetAmount => Math.Max(0L, _s.Long("fixedBetAmount"));
    public long MinBet => Math.Max(0L, _s.Long("minBet"));
    public long MaxBet => Math.Max(0L, _s.Long("maxBet"));
    public bool RefundFailedRedemptions => _s.Bool("refundFailedRedemptions");

    public int GlobalCooldown(string command) => Math.Max(0, _s.Int(CooldownKey(command) + "GlobalCooldown"));
    public int UserCooldown(string command) => Math.Max(0, _s.Int(CooldownKey(command) + "UserCooldown"));

    private static string CooldownKey(string command)
    {
        switch (command)
        {
            case "!high": return "high";
            case "!low": return "low";
            case "!equal": return "equal";
            case "!hlgalltime": return "alltime";
            case "!hlgsession": return "session";
            case "!hlgpersonal": return "personal";
            default: return "none";
        }
    }

    public string BlockedUsers => _s.String("blockedUsers");
    public string BlockedGroups => _s.String("blockedGroups");

    public int LeaderboardSize => UiSettings.Clamp(_s.Int("leaderboardSize"), 1, 25);
    public bool LeaderboardExcludeBroadcaster => _s.Bool("leaderboardExcludeBroadcaster");
    public bool LeaderboardExcludeBot => _s.Bool("leaderboardExcludeBot");

    public string OutcomeTemplate => _s.String("outcomeTemplate");
    public string StartMessage => _s.String("startMessage");
    public string FreeGameStartMessage => _s.String("freeGameStartMessage");
    public string FreeGameEndMessage => _s.String("freeGameEndMessage");
    public string RedemptionRefunded => _s.String("redemptionRefunded");
    public string ReasonNoInput => _s.String("reasonNoInput");
    public string ReasonNegativeAmount => _s.String("reasonNegativeAmount");
    public string ReasonInsufficientFunds => _s.String("reasonInsufficientFunds");
    public string ReasonBelowMinimum => _s.String("reasonBelowMinimum");
    public string ReasonAboveMaximum => _s.String("reasonAboveMaximum");
    public string GlobalMessage => _s.String("globalCooldownMessage");
    public string UserMessage => _s.String("userCooldownMessage");
}
