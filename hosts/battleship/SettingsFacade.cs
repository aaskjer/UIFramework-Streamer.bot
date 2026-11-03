/// <summary>BSG-Settings.json as the game reads it: the old property names over the canonical keys, old PascalCase
/// files read through the aliases, the window's ranges applied on read, percentages back as the fractions the
/// engine works with.</summary>
public sealed class Settings
{
    private readonly UiSettings _s;

    private Settings(UiSettings s) { _s = s; }

    /// <summary>The defaults, for a field initialiser; Execute loads the file before anything reads it.</summary>
    public Settings() : this(UiSettings.Parse("{}")) { }

    public static string BaseFolder
    {
        get
        {
            var folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Chat Games", "Battleship Game");
            Directory.CreateDirectory(folder);
            return folder;
        }
    }

    public static string FilePath => Path.Combine(BaseFolder, "BSG-Settings.json");
    public static string LogDirectory => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Chat Games", "Logs");
    public static string ActionLogPath => Path.Combine(LogDirectory, $"BSG-log-{DateTime.Now:yyyy-MM-dd}.txt");

    /// <summary>Never throws: a missing or damaged BSG-Settings.json gives the defaults, and the battle goes on.</summary>
    public static Settings Load() => new Settings(UiSettings.Load(FilePath));

    public string GetOverlayDirectory()
    {
        var dir = ResolveOverlayDirectory(OverlayFolder);
        Directory.CreateDirectory(dir);
        return dir;
    }

    public static string ResolveOverlayDirectory(string configured)
    {
        var fallback = Path.Combine(BaseFolder, "Overlay");
        try
        {
            if (string.IsNullOrWhiteSpace(configured)) return fallback;
            var trimmed = configured.Trim();
            return Path.IsPathRooted(trimmed) ? trimmed : Path.Combine(BaseFolder, trimmed);
        }
        catch (Exception)
        {
            return fallback;
        }
    }

    public string GetOverlayHtmlPath() => Path.Combine(GetOverlayDirectory(), "battleship_overlay.html");
    public string GetOverlayStatePath() => Path.Combine(GetOverlayDirectory(), "battleship_state.json");
    public string GetOverlayHeatPath() => Path.Combine(GetOverlayDirectory(), "battleship_heat.json");

    // ---------- General ----------
    public bool UseBot => _s.Bool("useBot");
    public bool SendAction => _s.Bool("sendAction");
    public string NumberSeparator => _s.String("numberSeparator");
    public string BlockedUsers => _s.String("blockedUsers");
    public string BlockedGroups => _s.String("blockedGroups");
    public string AddictedUsers => _s.String("addictedUsers");
    public string AddictedGroups => _s.String("addictedGroups");
    /// <summary>The cut as a fraction of the winnings, 0 to 1; the file holds it in percent.</summary>
    public double AddictedPayoutCut => UiSettings.Clamp(_s.Double("addictedPayoutCut"), 0.0, 100.0) / 100.0;
    // Currency Core or LOYALTY POINTS, when installed, override these two for the run.
    private string _pointsVariable, _pointsVarName;
    public string PointsVariable { get { return _pointsVariable ?? _s.String("pointsVariable"); } set { _pointsVariable = value; } }
    public string PointsVarName { get { return _pointsVarName ?? _s.String("pointsName"); } set { _pointsVarName = value; } }
    public bool FreeGame => _s.Bool("freeGame");

    // ---------- Board and fleet ----------
    public int GridSize => UiSettings.Clamp(_s.Int("gridSize"), 5, 12);
    public bool NoTouchingRule => _s.Bool("noTouchingRule");
    public bool ShipCarrierEnabled => _s.Bool("shipCarrierEnabled");
    public int ShipCarrierLength => Math.Max(1, _s.Int("shipCarrierLength"));
    public long ShipCarrierValue => Math.Max(0, _s.Long("shipCarrierValue"));
    public bool ShipBattleshipEnabled => _s.Bool("shipBattleshipEnabled");
    public int ShipBattleshipLength => Math.Max(1, _s.Int("shipBattleshipLength"));
    public long ShipBattleshipValue => Math.Max(0, _s.Long("shipBattleshipValue"));
    public bool ShipCruiserEnabled => _s.Bool("shipCruiserEnabled");
    public int ShipCruiserLength => Math.Max(1, _s.Int("shipCruiserLength"));
    public long ShipCruiserValue => Math.Max(0, _s.Long("shipCruiserValue"));
    public bool ShipSubmarineEnabled => _s.Bool("shipSubmarineEnabled");
    public int ShipSubmarineLength => Math.Max(1, _s.Int("shipSubmarineLength"));
    public long ShipSubmarineValue => Math.Max(0, _s.Long("shipSubmarineValue"));
    public bool ShipDestroyerEnabled => _s.Bool("shipDestroyerEnabled");
    public int ShipDestroyerLength => Math.Max(1, _s.Int("shipDestroyerLength"));
    public long ShipDestroyerValue => Math.Max(0, _s.Long("shipDestroyerValue"));

    // ---------- The Admiral ----------
    public string AdmiralDifficulty => _s.String("admiralDifficulty");
    /// <summary>The blunder chance as a fraction, 0 to 1; the file holds it in percent.</summary>
    public double AdmiralBlunderChance => UiSettings.Clamp(_s.Double("admiralBlunderChance"), 0.0, 100.0) / 100.0;
    public int AdmiralDelayMs => UiSettings.Clamp(_s.Int("admiralDelayMs"), 0, 5000);

    // ---------- Economy ----------
    public long WinBonus => Math.Max(0, _s.Long("winBonus"));
    public bool ChargeDamagedOnAbandon => _s.Bool("chargeDamagedOnAbandon");
    public bool ClampBalanceAtZero => _s.Bool("clampBalanceAtZero");
    public long MinBalanceToPlay => Math.Max(0, _s.Long("minBalanceToPlay"));
    public long DailyWinCap => Math.Max(0, _s.Long("dailyWinCap"));

    // ---------- Battle clock ----------
    public int DeployTimeoutSec => Math.Max(5, _s.Int("deployTimeoutSec"));
    public int TurnTimeoutSec => Math.Max(5, _s.Int("turnTimeout"));
    public int TurnWarnFirstSec => Math.Max(0, _s.Int("turnWarnFirstSec"));
    public int TurnWarnSecondSec => Math.Max(0, _s.Int("turnWarnSecondSec"));

    // ---------- Whisper ----------
    public bool WhisperOwnFleet => _s.Bool("whisperOwnFleet");

    // ---------- Cooldowns ----------
    public bool UseBotCD => _s.Bool("cooldownUseBot");
    public bool SendActionCD => _s.Bool("cooldownSendAction");
    public int GlobalCooldown => Math.Max(0, _s.Int("globalCooldown"));
    public int UserCooldown => Math.Max(0, _s.Int("userCooldown"));
    /// <summary>0 in the file means no limit, read as int.MaxValue as before.</summary>
    public int MaxGamesPerHour => _s.CeilingInt("maxGamesPerHour");

    // ---------- Overlay ----------
    public string OverlayFolder => _s.String("overlayFolder");
    /// <summary>Scales as fractions (1.0 = full size); the file holds them in percent.</summary>
    public double OverlayScale => UiSettings.Clamp(_s.Double("overlayScale"), 10.0, 300.0) / 100.0;
    public double OverlayFontScale => UiSettings.Clamp(_s.Double("overlayFontScale"), 10.0, 300.0) / 100.0;
    public string OverlayAdmiralName => _s.String("overlayAdmiralName");
    public bool OverlayShowPlayerRoster => _s.Bool("overlayShowPlayerRoster");
    public bool OverlayShowEnemyRoster => _s.Bool("overlayShowEnemyRoster");
    public bool OverlayShowCoords => _s.Bool("overlayShowCoords");
    public bool OverlayShowTurnTimer => _s.Bool("overlayShowTurnTimer");
    public bool OverlayShowScore => _s.Bool("overlayShowScore");
    public bool OverlayAnimationsEnabled => _s.Bool("overlayAnimations");
    public bool OverlayHideOnFreeGame => _s.Bool("overlayHideOnFreeGame");
    public string OverlayHitColor => _s.String("overlayHitColour");
    public string OverlayMissColor => _s.String("overlayMissColour");
    public string OverlaySunkColor => _s.String("overlaySunkColour");
    public string OverlayShipColor => _s.String("overlayShipColour");
    public string OverlayGridColor => _s.String("overlayGridColour");
    public string OverlayWaterColor => _s.String("overlayWaterColour");
    public string OverlayBackgroundColor => _s.String("overlayBackgroundColour");
    /// <summary>The opacity as a fraction, 0 to 1; the file holds it in percent.</summary>
    public double OverlayBackgroundOpacity => UiSettings.Clamp(_s.Double("overlayBackgroundOpacity"), 0.0, 100.0) / 100.0;
    public int OverlayGameOverFadeSec => Math.Max(1, _s.Int("overlayResultSeconds"));
    public int ObsConnectionId => Math.Max(0, _s.Int("obsConnectionId"));
    public string ObsSceneName => _s.String("obsSceneName");
    public string ObsSourceName => _s.String("obsSourceName");
    public string HeatObsSourceName => _s.String("heatObsSourceName");

    // ---------- Click to Fire ----------
    public bool HeatEnabled => _s.Bool("heatEnabled");
    public string HeatChannelId => _s.String("heatChannelId");
    public string HeatStreamerBotUrl => _s.String("heatStreamerBotUrl");
    public string HeatStreamerBotPassword => _s.String("heatStreamerBotPassword");
    public bool HeatShowClicks => _s.Bool("heatShowClicks");
    public bool HeatAutoLink => _s.Bool("heatAutoLink");
    /// <summary>Where the browser source sits on the canvas, as fractions; the file holds them in percent.</summary>
    public double HeatAreaLeft => UiSettings.Clamp(_s.Double("heatAreaLeft"), 0.0, 100.0) / 100.0;
    public double HeatAreaTop => UiSettings.Clamp(_s.Double("heatAreaTop"), 0.0, 100.0) / 100.0;
    public double HeatAreaWidth => UiSettings.Clamp(_s.Double("heatAreaWidth"), 1.0, 400.0) / 100.0;
    public double HeatAreaHeight => UiSettings.Clamp(_s.Double("heatAreaHeight"), 1.0, 400.0) / 100.0;

    // ---------- Modes ----------
    public bool SoloEnabled => _s.Bool("soloEnabled");
    public bool ChatBattleEnabled => _s.Bool("chatBattleEnabled");
    public string ChatTeamName => _s.String("chatTeamName");
    public int ChatRoundSec => Math.Max(5, _s.Int("chatRoundSec"));
    public bool DuelEnabled => _s.Bool("duelEnabled");
    public long DuelEntryFee => Math.Max(0, _s.Long("duelEntryFee"));
    public int DuelAcceptSec => Math.Max(10, _s.Int("duelAcceptSec"));
    public int DuelJoinSec => Math.Max(10, _s.Int("duelJoinSec"));
    public int DuelRoundSec => Math.Max(5, _s.Int("duelRoundSec"));
    public int DuelMaxTeamGap => Math.Max(0, _s.Int("duelMaxTeamGap"));
    public int DuelMaxMissedTurns => Math.Max(1, _s.Int("duelMaxMissedTurns"));

    // ---------- Action log ----------
    public bool LogToFile => _s.Bool("logToFile");

    // ---------- Messages ----------
    public string MsgGameStart => _s.String("msgGameStart");
    public string MsgFleetRerolled => _s.String("msgFleetRerolled");
    public string MsgFleetTooLate => _s.String("msgFleetTooLate");
    public string MsgPlayerMiss => _s.String("msgPlayerMiss");
    public string MsgPlayerHit => _s.String("msgPlayerHit");
    public string MsgPlayerSunk => _s.String("msgPlayerSunk");
    public string MsgPlayerSunkFree => _s.String("msgPlayerSunkFree");
    public string MsgAdmiralMiss => _s.String("msgAdmiralMiss");
    public string MsgAdmiralHit => _s.String("msgAdmiralHit");
    public string MsgAdmiralSunk => _s.String("msgAdmiralSunk");
    public string MsgAdmiralSunkFree => _s.String("msgAdmiralSunkFree");
    public string MsgBoardStatus => _s.String("msgBoardStatus");
    public string MsgHeatLinkHint => _s.String("msgHeatLinkHint");
    public string MsgHeatLinked => _s.String("msgHeatLinked");
    public string MsgHeatAutoHint => _s.String("msgHeatAutoHint");
    public string MsgChatStarted => _s.String("msgChatStarted");
    public string MsgChatSinks => _s.String("msgChatSinks");
    public string MsgChatLosesShip => _s.String("msgChatLosesShip");
    public string MsgDuelChallenge => _s.String("msgDuelChallenge");
    public string MsgDuelOpenChallenge => _s.String("msgDuelOpenChallenge");
    public string MsgDuelAccepted => _s.String("msgDuelAccepted");
    public string MsgDuelJoined => _s.String("msgDuelJoined");
    public string MsgDuelStart => _s.String("msgDuelStart");
    public string MsgDuelTurn => _s.String("msgDuelTurn");
    public string MsgDuelTurnPassed => _s.String("msgDuelTurnPassed");
    public string MsgDuelSunk => _s.String("msgDuelSunk");
    public string MsgDuelStatus => _s.String("msgDuelStatus");
    public string MsgWin => _s.String("msgWin");
    public string MsgWinFree => _s.String("msgWinFree");
    public string MsgLoss => _s.String("msgLoss");
    public string MsgLossFree => _s.String("msgLossFree");
    public string MsgTurnTimeout => _s.String("msgTurnTimeout");
    public string MsgTurnTimeoutFree => _s.String("msgTurnTimeoutFree");
    public string MsgDeployTimeout => _s.String("msgDeployTimeout");
    public string MsgChatWins => _s.String("msgChatWins");
    public string MsgChatLoses => _s.String("msgChatLoses");
    public string MsgChatAbandoned => _s.String("msgChatAbandoned");
    public string MsgDuelWin => _s.String("msgDuelWin");
    public string MsgDuelWinFree => _s.String("msgDuelWinFree");
    public string MsgDuelForfeit => _s.String("msgDuelForfeit");
    public string MsgDuelExpired => _s.String("msgDuelExpired");
    public string MsgDuelCancelled => _s.String("msgDuelCancelled");
    public string MsgModStop => _s.String("msgModStop");
    public string MsgModStopDuel => _s.String("msgModStopDuel");
    public string MsgTurnWarnFirst => _s.String("msgTurnWarnFirst");
    public string MsgTurnWarnSecond => _s.String("msgTurnWarnSecond");
    public string MsgNoGame => _s.String("msgNoGame");
    public string MsgAlreadyInGame => _s.String("msgAlreadyInGame");
    public string MsgBadCoord => _s.String("msgBadCoord");
    public string MsgNoCoord => _s.String("msgNoCoord");
    public string MsgAlreadyFired => _s.String("msgAlreadyFired");
    public string MsgNotEnoughPoints => _s.String("msgNotEnoughPoints");
    public string MsgMaxGamesPerHour => _s.String("msgMaxGamesPerHour");
    public string MsgDailyCapReached => _s.String("msgDailyCapReached");
    public string MsgHeatLinkNoClick => _s.String("msgHeatLinkNoClick");
    public string MsgHeatLinkAmbiguous => _s.String("msgHeatLinkAmbiguous");
    public string MsgChatBusy => _s.String("msgChatBusy");
    public string MsgModeDisabled => _s.String("msgModeDisabled");
    public string MsgDuelBusy => _s.String("msgDuelBusy");
    public string MsgDuelNoChallenge => _s.String("msgDuelNoChallenge");
    public string MsgDuelNotJoining => _s.String("msgDuelNotJoining");
    public string MsgDuelAlreadyJoined => _s.String("msgDuelAlreadyJoined");
    public string MsgDuelTeamFull => _s.String("msgDuelTeamFull");
    public string MsgDuelBadTeam => _s.String("msgDuelBadTeam");
    public string MsgDuelNotInDuel => _s.String("msgDuelNotInDuel");
    public string MsgDuelNotYourTurn => _s.String("msgDuelNotYourTurn");
    public string MsgModStopNothing => _s.String("msgModStopNothing");
    public string GlobalCooldownMsg => _s.String("globalCooldownMessage");
    public string UserCooldownMsg => _s.String("userCooldownMessage");
    public string MsgWhisperFleet => _s.String("msgWhisperFleet");
}
