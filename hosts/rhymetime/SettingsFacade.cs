public sealed class Settings
{
    private readonly UiSettings _s;

    private Settings(UiSettings s) { _s = s; }

    /// <summary>The defaults, for a field initialiser; Execute loads the file before anything reads it.</summary>
    public Settings() : this(UiSettings.Parse("{}")) { }

    private static string BaseFolder
    {
        get
        {
            var folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Chat Games", "RhymeTime");
            Directory.CreateDirectory(folder);
            return folder;
        }
    }

    public static string FilePath => Path.Combine(BaseFolder, "RT-settings.json");

    /// <summary>Never throws: a missing or damaged RT-settings.json gives the defaults, and the round goes on.</summary>
    public static Settings Load() => new Settings(UiSettings.Load(FilePath));

    // Read-only view of RT-settings.json through the schema: the old property names, canonical keys underneath,
    // old PascalCase files read through the aliases, and the window's ranges applied on read.

    public bool UseBot => _s.Bool("useBot");
    public bool SendAction => _s.Bool("sendAction");
    public bool UseBotCD => _s.Bool("cooldownUseBot");
    public bool SendActionCD => _s.Bool("cooldownSendAction");
    public string NumberSeparator => _s.String("numberSeparator");

    public string BlockedUsers => _s.String("blockedUsers");
    public string BlockedGroups => _s.String("blockedGroups");

    public string PointsVar => _s.String("pointsVariable");
    public string PointsName => _s.String("pointsName");

    public int DefaultBet => Math.Max(0, _s.Int("defaultBet"));
    public int MinBet => Math.Max(0, _s.Int("minBet"));
    public int MaxBet => Math.Max(0, _s.Int("maxBet"));
    public int TimeLimit => Math.Max(1, _s.Int("timeLimit"));
    public int MaxAttempts => UiSettings.Clamp(_s.Int("maxAttempts"), 1, 10);
    public string Difficulty => _s.String("difficulty");
    public double EasyMultiplier => Math.Max(0.0, _s.Double("easyMultiplier"));
    public double MediumMultiplier => Math.Max(0.0, _s.Double("mediumMultiplier"));
    public double HardMultiplier => Math.Max(0.0, _s.Double("hardMultiplier"));
    public bool CheckRealWords => _s.Bool("checkRealWords");

    public int BattleReward => Math.Max(0, _s.Int("battleReward"));

    public bool OverlayEnabled => _s.Bool("overlayEnabled");
    public int OverlayResultSeconds => Math.Max(1, _s.Int("overlayResultSeconds"));
    public string OverlayPosition => _s.String("overlayPosition");
    public int OverlayScale => UiSettings.Clamp(_s.Int("overlayScale"), 10, 300);
    public bool OverlayShowEnding => _s.Bool("overlayShowEnding");
    public bool OverlayShowAttempts => _s.Bool("overlayShowAttempts");
    public bool OverlayShowStake => _s.Bool("overlayShowStake");
    public bool OverlayShowExamples => _s.Bool("overlayShowExamples");
    public bool OverlayGlow => _s.Bool("overlayGlow");
    public string OverlayAccentColor => _s.String("overlayAccentColour");
    public string OverlayBattleColor => _s.String("overlayBattleColour");
    public string OverlayWinColor => _s.String("overlayWinColour");
    public string OverlayCardColor => _s.String("overlayCardColour");
    public int OverlayCardOpacity => UiSettings.Clamp(_s.Int("overlayCardOpacity"), 0, 100);
    public int OverlayPort { get { int p = _s.Int("overlayPort"); return p >= 1 && p <= 65535 ? p : 8080; } }
    public int ObsConnectionId => Math.Max(0, _s.Int("obsConnectionId"));
    public string ObsSceneName => _s.String("obsSceneName");
    public string ObsSourceName => _s.String("obsSourceName");

    public int CooldownGlobalSeconds => Math.Max(0, _s.Int("globalCooldown"));
    public int CooldownUserSeconds => Math.Max(0, _s.Int("userCooldown"));

    public string GlobalMessage => _s.String("globalCooldownMessage");
    public string UserMessage => _s.String("userCooldownMessage");
    public string StartMessage => _s.String("startMessage");
    public string WinMessage => _s.String("winMessage");
    public string LoseMessage => _s.String("loseMessage");
    public string FailedAttemptMessage => _s.String("failedAttemptMessage");
    public string MaxAttemptsLoseMessage => _s.String("maxAttemptsLoseMessage");
    public string EducationalMessage => _s.String("educationalMessage");
    public string InvalidBetMessage => _s.String("invalidBetMessage");
    public string AlreadyPlayingMessage => _s.String("alreadyPlayingMessage");
    public string NoWordsMessage => _s.String("noWordsMessage");
    public string ReasonNegativeAmount => _s.String("reasonNegativeAmount");
    public string ReasonInsufficientFunds => _s.String("reasonInsufficientFunds");
    public string ReasonBelowMinimum => _s.String("reasonBelowMinimum");
    public string ReasonAboveMaximum => _s.String("reasonAboveMaximum");
    public string BattleStartMessage => _s.String("battleStartMessage");
    public string BattleWinMessage => _s.String("battleWinMessage");
    public string BattleTimeoutMessage => _s.String("battleTimeoutMessage");
    public string BattleAlreadyActiveMessage => _s.String("battleAlreadyActiveMessage");
}
