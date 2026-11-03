/// <summary>configs.json and messages.json as the game reads them: the old property names over the canonical keys,
/// old PascalCase files read through the aliases, the window's ranges applied on read. The messages come from
/// messages.json by their field names, the file the window edits them in. The three things the chat commands
/// change (Free Game Mode, the bets around it, the addicted list) are written back by Save.</summary>
public class CCGSettings
{
    private readonly UiSettings _s;
    private readonly JObject _messages;

    private CCGSettings(UiSettings s, JObject messages) { _s = s; _messages = messages ?? new JObject(); }

    public CCGSettings() : this(UiSettings.Parse("{}"), null) { }

    public static string FilePath      => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Chat Games");
    public static string ConfigPath    => Path.Combine(FilePath, "Classic Chat Gamble", "configs.json");
    public static string MessagesPath  => Path.Combine(FilePath, "Classic Chat Gamble", "messages.json");
    public static string LogsDir => Path.Combine(FilePath, "Logs");
    public static string ActionLogPath => Path.Combine(LogsDir, $"CCG-log-{DateTime.Now:yyyy-MM-dd}.txt");
    public static readonly List<string> FreeGameNotifiedUsers = new List<string>();

    /// <summary>Never throws: a missing or damaged file gives the defaults for what it held, and the gamble goes on.</summary>
    public static CCGSettings Load()
    {
        JObject messages = null;
        try
        {
            if (File.Exists(MessagesPath))
                using (var reader = new JsonTextReader(new StringReader(ReadSettingsFile(MessagesPath))) { DateParseHandling = DateParseHandling.None })
                    messages = JObject.Load(reader);
        }
        catch (Exception) { messages = null; }
        return new CCGSettings(UiSettings.Load(ConfigPath), messages);
    }

    private static string ReadSettingsFile(string path)
    {
        for (int attempt = 1; ; attempt++)
        {
            try { return File.ReadAllText(path); }
            catch (IOException) when (attempt < 5) { System.Threading.Thread.Sleep(40); }
        }
    }

    // A field set to null in messages.json sends nothing; only a field that is missing altogether gives the default.
    private string Message(string key, string field)
    {
        var token = _messages[field];
        if (token == null)
            return (string)UiSchema.Find(key)?.Default ?? "";
        if (token.Type == JTokenType.Null)
            return "";
        return token.Type == JTokenType.String ? (string)token : token.ToString();
    }

    // ---- General ----
    public bool UseBot => _s.Bool("useBot");
    public bool SendAction => _s.Bool("sendAction");
    public bool LogToFile => _s.Bool("logToFile");
    public string BlockedGroups => _s.String("blockedGroups");
    public string BlockedUsers => _s.String("blockedUsers");
    public string NumberSeparator => _s.String("numberSeparator");
    public string PointsVar => _s.String("pointsVariable");
    public string PointsName => _s.String("pointsName");

    // What the chat commands change: read from the file, held here until Save writes them back.
    private int? _defaultBet, _previousDefaultBet;
    private bool? _freeGame;
    private string _addictedUsers;
    public int DefaultBet { get { return _defaultBet ?? Math.Max(0, _s.Int("defaultBet")); } set { _defaultBet = Math.Max(0, value); } }
    public bool FreeGameActive { get { return _freeGame ?? _s.Bool("freeGame"); } set { _freeGame = value; } }
    public int PreviousDefaultBet { get { return _previousDefaultBet ?? Math.Max(0, _s.Int("previousDefaultBet")); } set { _previousDefaultBet = Math.Max(0, value); } }
    public string AddictedUsers { get { return _addictedUsers ?? _s.String("addictedUsers"); } set { _addictedUsers = value ?? ""; } }

    public int MinBet => Math.Max(0, _s.Int("minBet"));
    public int MaxBet => Math.Max(0, _s.Int("maxBet"));
    public double JackpotPercent => _s.Double("jackpotPercent");
    public int JackpotMultiplier => _s.Int("jackpotMultiplier");
    public double WinPercent => _s.Double("winPercent");
    public double RndMin => _s.Double("rndMin");
    public double RndMax => _s.Double("rndMax");
    public bool IsChannelPointsTrigger => _s.Bool("isChannelPointsTrigger");
    public string TargetRewardId => _s.String("rewardId");
    public string CPBetMode => _s.String("cpBetMode");
    public int FixedBetAmount => Math.Max(0, _s.Int("fixedBetAmount"));
    public bool RefundFailedRedemptions => _s.Bool("refundFailedRedemptions");
    public bool UseBotCD => _s.Bool("cooldownUseBot");
    public bool SendActionCD => _s.Bool("cooldownSendAction");
    public int CooldownGlobalSeconds => Math.Max(0, _s.Int("globalCooldown"));
    public int CooldownUserSeconds => Math.Max(0, _s.Int("userCooldown"));
    public bool SubscriberBoostEnabled => _s.Bool("subscriberBoostEnabled");
    public double SubscriberBoostPercent => _s.Double("subscriberBoostPercent");
    public bool VipBoostEnabled => _s.Bool("vipBoostEnabled");
    public double VipBoostPercent => _s.Double("vipBoostPercent");
    public string AddictedGroups => _s.String("addictedGroups");
    public double AddictedPayoutCutPercent => _s.Double("addictedPayoutCut");

    // ---- Messages, from messages.json ----
    public string GlobalMessage => Message("globalCooldownMessage", "GlobalMessage");
    public string UserMessage => Message("userCooldownMessage", "UserMessage");
    public string JackpotMessage => Message("jackpotMessage", "JackpotMessage");
    public string WinMessage => Message("winMessage", "WinMessage");
    public string AddictedJackpotMessage => Message("addictedJackpotMessage", "AddictedJackpotMessage");
    public string AddictedWinMessage => Message("addictedWinMessage", "AddictedWinMessage");
    public string LossMessage => Message("lossMessage", "LossMessage");
    public string FreeGameMessage => Message("freeGameMessage", "FreeGameMessage");
    public string FreeGameWinMessage => Message("freeGameWinMessage", "FreeGameWinMessage");
    public string FreeGameJackpotMessage => Message("freeGameJackpotMessage", "FreeGameJackpotMessage");
    public string FreeGameLossMessage => Message("freeGameLossMessage", "FreeGameLossMessage");
    public string FreeGameStartMessage => Message("freeGameStartMessage", "FreeGameStartMessage");
    public string FreeGameEndMessage => Message("freeGameEndMessage", "FreeGameEndMessage");
    public string InvalidBetMessage => Message("invalidBetMessage", "InvalidBetMessage");
    public string ReasonNegativeAmount => Message("reasonNegativeAmount", "ReasonNegativeAmount");
    public string ReasonInsufficientFunds => Message("reasonInsufficientFunds", "ReasonInsufficientFunds");
    public string ReasonBelowMinimum => Message("reasonBelowMinimum", "ReasonBelowMinimum");
    public string ReasonAboveMaximum => Message("reasonAboveMaximum", "ReasonAboveMaximum");
    public string ReasonNoInput => Message("reasonNoInput", "ReasonNoInput");
    public string RedemptionRefunded => Message("redemptionRefundedMessage", "RedemptionRefunded");
    public string AddictedAlreadyOnList => Message("addictedAlreadyOnList", "AddictedAlreadyOnList");
    public string AddictedAdded => Message("addictedAdded", "AddictedAdded");
    public string AddictedNotOnList => Message("addictedNotOnList", "AddictedNotOnList");
    public string AddictedRemoved => Message("addictedRemoved", "AddictedRemoved");
    public string AddictedUsage => Message("addictedUsage", "AddictedUsage");

    /// <summary>Writes what the chat commands changed into configs.json, under the canonical keys, and leaves
    /// everything else in the file as it is. Written beside the file and swapped in, the replaced file kept as
    /// .bak when it parses, so neither a crash mid-save nor a reader at the wrong moment sees half a file.</summary>
    public void Save()
    {
        JObject root;
        try
        {
            using (var reader = new JsonTextReader(new StringReader(File.Exists(ConfigPath) ? ReadSettingsFile(ConfigPath) : "{}")) { DateParseHandling = DateParseHandling.None })
                root = JObject.Load(reader);
        }
        catch (Exception) { root = new JObject(); }
        root["defaultBet"] = DefaultBet;
        root["freeGame"] = FreeGameActive;
        root["previousDefaultBet"] = PreviousDefaultBet;
        root["addictedUsers"] = AddictedUsers;
        foreach (var old in new[] { "DefaultBet", "FreeGameActive", "PreviousDefaultBet", "AddictedUsers" })
            root.Remove(old);
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
        WriteAllTextAtomic(ConfigPath, root.ToString(Formatting.Indented));
    }

    private static void WriteAllTextAtomic(string path, string text)
    {
        string temp = path + ".tmp";
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                File.WriteAllText(temp, text);
                if (File.Exists(path))
                    File.Replace(temp, path, IsReadable(path) ? path + ".bak" : null);
                else
                    File.Move(temp, path);
                return;
            }
            catch (IOException) when (attempt < 5)
            {
                System.Threading.Thread.Sleep(40);
            }
        }
    }

    private static bool IsReadable(string path)
    {
        try { JObject.Parse(File.ReadAllText(path)); return true; }
        catch (Exception) { return false; }
    }
}
