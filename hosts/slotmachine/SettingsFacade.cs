/// <summary>SMG-Settings.json as the game reads it: the old property names over the canonical keys, old
/// PascalCase files read through the aliases, the window's ranges applied on read, the emotes as lists.</summary>
public sealed class SlotMachineSettings
{
    private readonly UiSettings _s;

    private SlotMachineSettings(UiSettings s)
    {
        _s = s;
        PlatformEmotes = new Dictionary<string, List<SlotEmote>>(StringComparer.OrdinalIgnoreCase)
        {
            ["twitch"] = SlotEngine.EmotesFrom(_s.Token("emotesTwitch")),
            ["youtube"] = SlotEngine.EmotesFrom(_s.Token("emotesYoutube")),
            ["kick"] = SlotEngine.EmotesFrom(_s.Token("emotesKick"))
        };
    }

    /// <summary>The defaults, for a field initialiser; Execute loads the file before anything reads it.</summary>
    public SlotMachineSettings() : this(UiSettings.Parse("{}")) { }

    public static string FolderPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Chat Games", "Slot Machine Game");
    public static string FilePath => Path.Combine(FolderPath, "SMG-Settings.json");
    public static string BackupPath => FilePath + ".bak";
    public static string LogDirectory => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Chat Games", "Logs");
    public static string ActionLogPath => Path.Combine(LogDirectory, $"SMG-log-{DateTime.Now:yyyy-MM-dd}.txt");

    /// <summary>A missing file gives the defaults (the window writes the file). A file that cannot be opened
    /// gives null and a problem: there is no spin this time. A damaged file falls back to the .bak the last
    /// save kept, or to the defaults, and says so.</summary>
    public static SlotMachineSettings Load(out string problem)
    {
        problem = null;
        if (!File.Exists(FilePath))
            return new SlotMachineSettings();

        string json = ReadText(FilePath);
        if (json == null)
        {
            problem = "SMG-Settings.json is in use or can't be opened, so there is no spin this time.";
            return null;
        }
        var settings = Parse(json);
        if (settings != null)
            return settings;

        string backup = File.Exists(BackupPath) ? ReadText(BackupPath) : null;
        settings = backup == null ? null : Parse(backup);
        problem = settings != null
            ? "SMG-Settings.json is damaged, so the game plays with SMG-Settings.json.bak until the settings are saved again."
            : "SMG-Settings.json is damaged and there is no readable SMG-Settings.json.bak: open the settings to repair it.";
        return settings;
    }

    private static SlotMachineSettings Parse(string json)
    {
        try { return new SlotMachineSettings(UiSettings.Parse(json)); }
        catch (Exception) { return null; }
    }

    private static string ReadText(string path)
    {
        for (int attempt = 1; ; attempt++)
        {
            try { return File.ReadAllText(path); }
            catch (IOException) when (attempt < 5) { System.Threading.Thread.Sleep(40); }
            catch (Exception) { return null; }
        }
    }

    public bool UseBot => _s.Bool("useBot");
    public bool SendAction => _s.Bool("sendAction");
    public bool UseBotCD => _s.Bool("cooldownUseBot");
    public bool SendActionCD => _s.Bool("cooldownSendAction");
    public string NumberSeparator => _s.String("numberSeparator");

    public string BlockedGroups => _s.String("blockedGroups");
    public string AddictedUsers => _s.String("addictedUsers");
    public string AddictedGroups => _s.String("addictedGroups");
    /// <summary>The cut as a fraction of the win, 0 to 0.99; the file holds it in percent.</summary>
    public double AddictedPayoutCut => UiSettings.Clamp(_s.Double("addictedPayoutCut"), 0.0, 99.0) / 100.0;

    // Currency Core or LOYALTY POINTS, when installed, override these two for the run.
    private string _pointsVar, _pointsName;
    public string PointsVar { get { return _pointsVar ?? _s.String("pointsVariable"); } set { _pointsVar = value; } }
    public string PointsName { get { return _pointsName ?? _s.String("pointsName"); } set { _pointsName = value; } }
    public bool IsFree => _s.Bool("freeGame");
    public int DefaultBet => Math.Max(0, _s.Int("defaultBet"));
    public int MinBet => Math.Max(0, _s.Int("minBet"));
    public int MaxBet => Math.Max(0, _s.Int("maxBet"));

    public int NumberOfSlots => UiSettings.Clamp(_s.Int("numberOfSlots"), 3, 6);
    public int EmotePoolSize => Math.Max(0, _s.Int("emotePoolSize"));
    public int MinEmoteWeight => Math.Max(1, _s.Int("minEmoteWeight"));
    public int MaxEmoteWeight => Math.Max(1, _s.Int("maxEmoteWeight"));
    /// <summary>Chances as fractions; the file holds them in percent.</summary>
    public double AllowMatchChance => UiSettings.Clamp(_s.Double("matchChance"), 0.0, 100.0) / 100.0;
    public double AllowJackpotChance => UiSettings.Clamp(_s.Double("jackpotChance"), 0.0, 100.0) / 100.0;
    public int RerollAttempts => Math.Max(0, _s.Int("rerollAttempts"));
    public double TwoMatchPayout => Math.Max(0.0, _s.Double("twoMatchPayout"));
    public double ThreeMatchPayout => Math.Max(0.0, _s.Double("threeMatchPayout"));
    public double FourMatchPayout => Math.Max(0.0, _s.Double("fourMatchPayout"));
    public double FiveMatchPayout => Math.Max(0.0, _s.Double("fiveMatchPayout"));
    public double SixMatchPayout => Math.Max(0.0, _s.Double("sixMatchPayout"));
    public bool SubscriberBoostEnabled => _s.Bool("subscriberBoostEnabled");
    public double SubscriberBoostPercent => Math.Max(0.0, _s.Double("subscriberBoostPercent"));
    public bool VipBoostEnabled => _s.Bool("vipBoostEnabled");
    public double VipBoostPercent => Math.Max(0.0, _s.Double("vipBoostPercent"));
    public bool UseJackpotPool => _s.Bool("useJackpotPool");
    public int JackpotBet => Math.Max(0, _s.Int("jackpotBet"));

    public bool IsChannelPointsTrigger => _s.Bool("isChannelPointsTrigger");
    public string TargetRewardId => _s.String("rewardId");
    public string CPBetMode => _s.String("cpBetMode");
    public int FixedBetAmount => Math.Max(0, _s.Int("fixedBetAmount"));
    public bool RefundFailedRedemptions => _s.Bool("refundFailedRedemptions");

    public int CooldownGlobalSeconds => Math.Max(0, _s.Int("globalCooldown"));
    public int CooldownUserSeconds => Math.Max(0, _s.Int("userCooldown"));
    public bool LogToFile => _s.Bool("logToFile");

    // A file still carrying the texts of v14, which said "Jackpot pool" and "the pool", gets the current defaults.
    private const string PoolWinMessage = "@{user}, you WON! {amount} {currency}! 🎉🎉{jackpot} (Min. bet for Jackpot pool: {jackpotMinBet} {currency}){/jackpot} -- Your new balance: {newBalance} {currency}";
    private const string PoolLossMessage = "@{user}, you LOST! Better luck next time!{jackpot} 💸 {jackpotLossContribution} {currency} got added to the jackpot pool of now {jackpotPoolCurrent} {currency}!{/jackpot} -- Your new balance: {newBalance} {currency}";
    private const string PoolJackpotMessage = "🔥 J A C K P O T 🔥 @{user}, YOU WON {amount} {currency}{jackpot} + {jackpotPoolWinAmount} {currency} from the pool!{/jackpot} -- Your new balance: {newBalance} {currency}";
    private const string PoolAddictedJackpotMessage = "🔥 J A C K P O T 🔥 @{user}, YOU WON {amount} {currency}{jackpot} + {jackpotPoolWinAmount} {currency} from the pool!{/jackpot} -- Addiction Fee: your win was cut by {addictedCutPercent}% -- Your new balance: {newBalance} {currency}";
    private string Message(string key, string outdated)
    {
        string text = _s.String(key);
        return text == outdated ? (string)UiSchema.Find(key).Default : text;
    }

    public string WinMessage => Message("winMessage", PoolWinMessage);
    public string LossMessage => Message("lossMessage", PoolLossMessage);
    public string JackpotMessage => Message("jackpotMessage", PoolJackpotMessage);
    public string FreeGameWinMessage => _s.String("freeGameWinMessage");
    public string FreeGameLossMessage => _s.String("freeGameLossMessage");
    public string FreeGameJackpotMessage => _s.String("freeGameJackpotMessage");
    public string AddictedWinMessage => _s.String("addictedWinMessage");
    public string AddictedLossMessage => _s.String("addictedLossMessage");
    public string AddictedJackpotMessage => Message("addictedJackpotMessage", PoolAddictedJackpotMessage);
    public string InvalidBetMessage => _s.String("invalidBetMessage");
    public string ReasonNegativeAmount => _s.String("reasonNegativeAmount");
    public string ReasonInsufficientFunds => _s.String("reasonInsufficientFunds");
    public string ReasonBelowMinimum => _s.String("reasonBelowMinimum");
    public string ReasonAboveMaximum => _s.String("reasonAboveMaximum");
    public string GlobalMessage => _s.String("globalCooldownMessage");
    public string UserMessage => _s.String("userCooldownMessage");
    public string RedemptionRefundedMessage => _s.String("redemptionRefundedMessage");

    /// <summary>The emotes per platform (twitch, youtube, kick), rows without a code or with a weight below 1 left out.</summary>
    public Dictionary<string, List<SlotEmote>> PlatformEmotes { get; }
}
