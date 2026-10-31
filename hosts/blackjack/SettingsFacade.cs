public sealed class Settings
{
    private readonly UiSettings _s;

    private Settings(UiSettings s) { _s = s; }

    // Read-only view of BJG-Settings.json through the schema: every old property name, the window's old clamps
    // applied on read, so a hand-edited file cannot push the game out of range.

    public bool UseBot => _s.Bool("useBot");
    public bool SendAction => _s.Bool("sendAction");
    public string NumberSeparator => _s.String("numberSeparator");
    public string CardDisplayMode => _s.String("cardDisplayMode");
    public string BlockedUsers => _s.String("blockedUsers");
    public string BlockedGroups => _s.String("blockedGroups");
    public string AddictedUsers => _s.String("addictedUsers");
    public string AddictedGroups => _s.String("addictedGroups");
    public double AddictedPayoutCut => UiSettings.Clamp(_s.Double("addictedPayoutCut") / 100.0, 0.0, 0.99);
    // The game overrides these in memory when Currency Core or LOYALTY POINTS is detected; the file is not touched.
    private string _pointsVariable, _pointsVarName;
    public string PointsVariable { get => _pointsVariable ?? _s.String("pointsVariable"); set => _pointsVariable = value; }
    public string PointsVarName { get => _pointsVarName ?? _s.String("pointsName"); set => _pointsVarName = value; }
    public long DefaultBet => Math.Max(0L, _s.Long("defaultBet"));
    public bool FreeGame => _s.Bool("freeGame");
    public long MinBet => Math.Max(1L, _s.Long("minBet"));
    public long MaxBet => _s.Ceiling("maxBet");
    public int DealerStandsOn => UiSettings.Clamp(_s.Int("dealerStandsOn"), 16, 21);
    public double BlackjackPayout => _s.Double("blackjackPayout");
    public int NumDecks => UiSettings.Clamp(_s.Int("numDecks"), 1, 8);
    public double ReshuffleThreshold => UiSettings.Clamp(_s.Double("reshuffleThreshold"), 0.01, 0.9);
    public bool ShowAceMsg => _s.Bool("showAceMsg");
    public int MaxGamesPerHour => _s.CeilingInt("maxGamesPerHour");
    public bool TieDrawOnlyOneCard => _s.Bool("tieDrawOnlyOneCard");
    public int DealerDrawDelayMs => Math.Max(0, _s.Int("dealerDrawDelayMs"));
    public bool AllowDoubleDown => _s.Bool("allowDoubleDown");
    public bool AllowSplit => _s.Bool("allowSplit");
    public bool AllowResplit => _s.Bool("allowResplit");
    public int MaxSplitHands => UiSettings.Clamp(_s.Int("maxSplitHands"), 2, 8);
    public bool AllowInsurance => _s.Bool("allowInsurance");
    public bool AllowSurrender => _s.Bool("allowSurrender");
    public bool DealerPeeks => _s.Bool("dealerPeeks");
    public int TurnTimeoutSec => Math.Max(0, _s.Int("turnTimeout"));
    public bool TimeoutRefundIfNoAction => _s.Bool("timeoutRefundIfNoAction");
    public string OverlayFolder => _s.String("overlayFolder");
    public double OverlayScale => UiSettings.Clamp(_s.Double("overlayScale") / 100.0, 0.25, 4.0);
    public bool OverlayShowPoints => _s.Bool("overlayShowPoints");
    public string OverlayDealerName => _s.String("overlayDealerName");
    public bool OverlayShowHandValues => _s.Bool("overlayShowHandValues");
    public bool OverlayShowOutcomeBadges => _s.Bool("overlayShowOutcomeBadges");
    public bool OverlayShowInsurance => _s.Bool("overlayShowInsurance");
    public bool OverlayHideOnFreeGame => _s.Bool("overlayHideOnFreeGame");
    public string OverlayCardRedColor => _s.String("overlayCardRedColor");
    public string OverlayCardBlackColor => _s.String("overlayCardBlackColor");
    public string OverlayBackgroundColor => _s.String("overlayBackgroundColour");
    public double OverlayBackgroundOpacity => UiSettings.Clamp(_s.Double("overlayBackgroundOpacity"), 0.0, 100.0);
    public bool OverlayAnimationsEnabled => _s.Bool("overlayAnimations");
    public double OverlayFontScale => UiSettings.Clamp(_s.Double("overlayFontScale") / 100.0, 0.25, 4.0);
    public int OverlayGameOverFadeSec => Math.Max(1, _s.Int("overlayResultSeconds"));
    public bool DuelEnabled => _s.Bool("duelEnabled");
    public int DuelAcceptTimeoutSec => Math.Max(10, _s.Int("duelAcceptTimeoutSec"));
    public int DuelTurnTimeoutSec => Math.Max(0, _s.Int("duelTurnTimeoutSec"));
    public int DuelConsentTimeoutSec => Math.Max(5, _s.Int("duelConsentTimeoutSec"));
    public long DuelMinBet => Math.Max(1L, _s.Long("duelMinBet"));
    public long DuelMaxBet => _s.Ceiling("duelMaxBet");
    public bool DuelAllowDoubleDown => _s.Bool("duelAllowDoubleDown");
    public bool DuelAllowSplit => _s.Bool("duelAllowSplit");
    public bool DuelSuddenDeathEnabled => _s.Bool("duelSuddenDeathEnabled");
    public int DuelMaxSuddenDeathRounds => UiSettings.Clamp(_s.Int("duelMaxSuddenDeathRounds"), 1, 10);
    public bool DuelApplyAddictedCut => _s.Bool("duelApplyAddictedCut");
    public bool DuelCountsTowardGamesPerHour => _s.Bool("duelCountsTowardGamesPerHour");
    public bool UseBotCD => _s.Bool("cooldownUseBot");
    public bool SendActionCD => _s.Bool("cooldownSendAction");
    public int GlobalCooldown => Math.Max(0, _s.Int("globalCooldown"));
    public int UserCooldown => Math.Max(0, _s.Int("userCooldown"));
    public int DuelGlobalCooldown => Math.Max(0, _s.Int("duelGlobalCooldown"));
    public int DuelUserCooldown => Math.Max(0, _s.Int("duelUserCooldown"));
    public bool HouseEnabled => _s.Bool("houseEnabled");
    public bool HouseRuleVegasEnabled => _s.Bool("houseRuleVegasEnabled");
    public bool HouseRuleAtlanticEnabled => _s.Bool("houseRuleAtlanticEnabled");
    public bool HouseRuleEuropeanEnabled => _s.Bool("houseRuleEuropeanEnabled");
    public int HouseRulePickTimeoutSec => Math.Max(10, _s.Int("houseRulePickTimeoutSec"));
    public int HouseMaxSeats => UiSettings.Clamp(_s.Int("houseMaxSeats"), 2, 6);
    public int HouseJoinWindowSec => Math.Max(15, _s.Int("houseJoinWindowSec"));
    public int HouseInsuranceWindowSec => Math.Max(5, _s.Int("houseInsuranceWindowSec"));
    public int HouseTurnTimeoutSec => Math.Max(0, _s.Int("houseTurnTimeoutSec"));
    public long HouseMinBet => Math.Max(1L, _s.Long("houseMinBet"));
    public long HouseMaxBet => _s.Ceiling("houseMaxBet");
    public bool HouseCountsTowardGamesPerHour => _s.Bool("houseCountsTowardGamesPerHour");
    public int HouseGlobalCooldown => Math.Max(0, _s.Int("houseGlobalCooldown"));
    public int HouseUserCooldown => Math.Max(0, _s.Int("houseUserCooldown"));
    public string GlobalCooldownMsg => _s.String("globalCooldownMessage");
    public string UserCooldownMsg => _s.String("userCooldownMessage");
    public string MsgGameStart => _s.String("msgGameStart");
    public string MsgBlackjackWin => _s.String("msgBlackjackWin");
    public string MsgRegularWin => _s.String("msgRegularWin");
    public string MsgLoss => _s.String("msgLoss");
    public string MsgBlackjackWinFree => _s.String("msgBlackjackWinFree");
    public string MsgRegularWinFree => _s.String("msgRegularWinFree");
    public string MsgLossFree => _s.String("msgLossFree");
    public string MsgBust => _s.String("msgBust");
    public string MsgSplit => _s.String("msgSplit");
    public string MsgDoubleDown => _s.String("msgDoubleDown");
    public string MsgDrawCard => _s.String("msgDrawCard");
    public string MsgAceReduced => _s.String("msgAceReduced");
    public string MsgBalance => _s.String("msgBalance");
    public string MsgQuitRefund => _s.String("msgQuitRefund");
    public string MsgQuitRefundFree => _s.String("msgQuitRefundFree");
    public string MsgSpecialWin5Card => _s.String("msgSpecialWin5Card");
    public string MsgSpecialWin3Sevens => _s.String("msgSpecialWin3Sevens");
    public string MsgMovingToHand => _s.String("msgMovingToHand");
    public string MsgBettingAll => _s.String("msgBettingAll");
    public string MsgBettingHalf => _s.String("msgBettingHalf");
    public string MsgBettingRandom => _s.String("msgBettingRandom");
    public string MsgBettingQuarter => _s.String("msgBettingQuarter");
    public string MsgBettingDouble => _s.String("msgBettingDouble");
    public string MsgBettingMax => _s.String("msgBettingMax");
    public string MsgNotEnoughPoints => _s.String("reasonInsufficientFunds");
    public string MsgInsufficientSplit => _s.String("msgInsufficientSplit");
    public string MsgInsufficientDouble => _s.String("msgInsufficientDouble");
    public string MsgAlreadyActiveGame => _s.String("msgAlreadyActiveGame");
    public string MsgNoActiveGame => _s.String("msgNoActiveGame");
    public string MsgGameAlreadyOver => _s.String("msgGameAlreadyOver");
    public string MsgMaxGamesReached => _s.String("msgMaxGamesReached");
    public string MsgPointsVarNotFound => _s.String("msgPointsVarNotFound");
    public string MsgNoStatsFound => _s.String("leaderboardEmpty");
    public int LeaderboardSize => UiSettings.Clamp(_s.Int("leaderboardSize"), 1, 10);
    public bool LeaderboardShowMedals => _s.Bool("leaderboardShowMedals");
    public bool LeaderboardExcludeBroadcaster => _s.Bool("leaderboardExcludeBroadcaster");
    public bool LeaderboardExcludeBot => _s.Bool("leaderboardExcludeBot");
    public bool LeaderboardUseBot => _s.Bool("leaderboardUseBot");
    public bool LeaderboardSendAction => _s.Bool("leaderboardSendAction");
    public string LeaderboardAutoReset => _s.String("leaderboardAutoReset");
    public string LeaderboardResetScope => _s.String("leaderboardResetScope");
    public string MsgPointsNotRefunded => _s.String("msgPointsNotRefunded");
    public string MsgCannotSplitValue => _s.String("msgCannotSplitValue");
    public string MsgCannotSplitCount => _s.String("msgCannotSplitCount");
    public string MsgAlreadySplit => _s.String("msgAlreadySplit");
    public string MsgCannotDoubleCount => _s.String("msgCannotDoubleCount");
    public string MsgMustDoubleFirst => _s.String("msgMustDoubleFirst");
    public string MsgAlreadyDoubledDown => _s.String("msgAlreadyDoubledDown");
    public string MsgQuitOnlyBefore => _s.String("msgQuitOnlyBefore");
    public string MsgTurnTimeoutStand => _s.String("msgTurnTimeoutStand");
    public string MsgTurnTimeoutRefund => _s.String("msgTurnTimeoutRefund");
    public string MsgTurnTimeoutRefundFree => _s.String("msgTurnTimeoutRefundFree");
    public string MsgBetMinApplied => _s.String("msgBetMinApplied");
    public string MsgTutorial => _s.String("msgTutorial");
    public string MsgRepeatNothing => _s.String("msgRepeatNothing");
    public string MsgCardError => _s.String("msgCardError");
    public string MsgDealerBusts => _s.String("msgDealerBusts");
    public string MsgDealerWins => _s.String("msgDealerWins");
    public string MsgDealerDraws => _s.String("msgDealerDraws");
    public string MsgDealerReveal => _s.String("msgDealerReveal");
    public string MsgDealerPeekBlackjack => _s.String("msgDealerPeekBlackjack");
    public string MsgDealerBlackjackPush => _s.String("msgDealerBlackjackPush");
    public string MsgFinalSummary => _s.String("msgFinalSummary");
    public string MsgDealerTieWin => _s.String("msgDealerTieWin");
    public string MsgDealerTieWinFree => _s.String("msgDealerTieWinFree");
    public string MsgDealerBeatsYou => _s.String("msgDealerBeatsYou");
    public string MsgDealerBeatsYouFree => _s.String("msgDealerBeatsYouFree");
    public string MsgDealerDrawCard => _s.String("msgDealerDrawCard");
    public string MsgHandBust => _s.String("msgHandBust");
    public string MsgHandWin => _s.String("msgHandWin");
    public string MsgHandLost => _s.String("msgHandLost");
    public string MsgHandTie => _s.String("msgHandTie");
    public string MsgDoubleDownDisabled => _s.String("msgDoubleDownDisabled");
    public string MsgSplitDisabled => _s.String("msgSplitDisabled");
    public string MsgMaxSplitHandsReached => _s.String("msgMaxSplitHandsReached");
    public string MsgInsuranceOffer => _s.String("msgInsuranceOffer");
    public string MsgInsuranceTaken => _s.String("msgInsuranceTaken");
    public string MsgInsuranceWin => _s.String("msgInsuranceWin");
    public string MsgInsuranceLose => _s.String("msgInsuranceLose");
    public string MsgInsuranceNotAvailable => _s.String("msgInsuranceNotAvailable");
    public string MsgInsuranceInsufficientFunds => _s.String("msgInsuranceInsufficientFunds");
    public string MsgSurrenderResult => _s.String("msgSurrenderResult");
    public string MsgSurrenderResultFree => _s.String("msgSurrenderResultFree");
    public string MsgSurrenderNotAvailable => _s.String("msgSurrenderNotAvailable");
    public string MsgAddictedFee => _s.String("msgAddictedFee");
    public string MsgDuelAddictedFee => _s.String("msgDuelAddictedFee");
    public string MsgDuelChallenge => _s.String("msgDuelChallenge");
    public string MsgDuelDisabled => _s.String("msgDuelDisabled");
    public string MsgDuelNoTarget => _s.String("msgDuelNoTarget");
    public string MsgDuelSelfChallenge => _s.String("msgDuelSelfChallenge");
    public string MsgDuelChallengerBusy => _s.String("msgDuelChallengerBusy");
    public string MsgDuelTargetBusy => _s.String("msgDuelTargetBusy");
    public string MsgDuelAlreadyPending => _s.String("msgDuelAlreadyPending");
    public string MsgDuelTargetInsufficient => _s.String("msgDuelTargetInsufficient");
    public string MsgDuelNoPending => _s.String("msgDuelNoPending");
    public string MsgDuelDeclined => _s.String("msgDuelDeclined");
    public string MsgDuelDeclinedFree => _s.String("msgDuelDeclinedFree");
    public string MsgDuelExpired => _s.String("msgDuelExpired");
    public string MsgDuelAccepted => _s.String("msgDuelAccepted");
    public string MsgDuelAcceptedFree => _s.String("msgDuelAcceptedFree");
    public string MsgDuelExpiredFree => _s.String("msgDuelExpiredFree");
    public string MsgDuelSuddenDeathStartFree => _s.String("msgDuelSuddenDeathStartFree");
    public string MsgDuelHandDealt => _s.String("msgDuelHandDealt");
    public string MsgDuelTurn => _s.String("msgDuelTurn");
    public string MsgDuelNotYourTurn => _s.String("msgDuelNotYourTurn");
    public string MsgDuelDrawCard => _s.String("msgDuelDrawCard");
    public string MsgDuelBust => _s.String("msgDuelBust");
    public string MsgDuelStand => _s.String("msgDuelStand");
    public string MsgDuelTurnTimeout => _s.String("msgDuelTurnTimeout");
    public string MsgDuelForfeit => _s.String("msgDuelForfeit");
    public string MsgDuelShowdown => _s.String("msgDuelShowdown");
    public string MsgDuelWin => _s.String("msgDuelWin");
    public string MsgDuelWinFree => _s.String("msgDuelWinFree");
    public string MsgDuelPush => _s.String("msgDuelPush");
    public string MsgDuelPushFree => _s.String("msgDuelPushFree");
    public string MsgDuelRaiseOffer => _s.String("msgDuelRaiseOffer");
    public string MsgDuelRaiseAccepted => _s.String("msgDuelRaiseAccepted");
    public string MsgDuelRaiseDeclined => _s.String("msgDuelRaiseDeclined");
    public string MsgDuelRaiseTimeout => _s.String("msgDuelRaiseTimeout");
    public string MsgDuelRaiseUnaffordable => _s.String("msgDuelRaiseUnaffordable");
    public string MsgDuelSuddenDeathOffer => _s.String("msgDuelSuddenDeathOffer");
    public string MsgDuelSuddenDeathWaiting => _s.String("msgDuelSuddenDeathWaiting");
    public string MsgDuelSuddenDeathDeclined => _s.String("msgDuelSuddenDeathDeclined");
    public string MsgDuelSuddenDeathTimeout => _s.String("msgDuelSuddenDeathTimeout");
    public string MsgDuelSuddenDeathStart => _s.String("msgDuelSuddenDeathStart");
    public string MsgDuelSuddenDeathCard => _s.String("msgDuelSuddenDeathCard");
    public string MsgDuelSuddenDeathMaxRounds => _s.String("msgDuelSuddenDeathMaxRounds");
    public string MsgDuelSuddenDeathBothBust => _s.String("msgDuelSuddenDeathBothBust");
    public string MsgDuelSplitDisabled => _s.String("msgDuelSplitDisabled");
    public string MsgDuelDoubleDisabled => _s.String("msgDuelDoubleDisabled");
    public string MsgDuelActionNotAvailable => _s.String("msgDuelActionNotAvailable");
    public string MsgDuelHandLabel => _s.String("msgDuelHandLabel");
    public string MsgDuelRepeat => _s.String("msgDuelRepeat");
    public string MsgDuelRepeatFree => _s.String("msgDuelRepeatFree");
    public string MsgDuelPotResult => _s.String("msgDuelPotResult");
    public string MsgDuelPotPush => _s.String("msgDuelPotPush");
    public string MsgHouseDisabled => _s.String("msgHouseDisabled");
    public string MsgHouseNeedsCurrency => _s.String("msgHouseNeedsCurrency");
    public string MsgHouseNoRuleSets => _s.String("msgHouseNoRuleSets");
    public string MsgHouseAlreadyRunning => _s.String("msgHouseAlreadyRunning");
    public string MsgHouseRulePrompt => _s.String("msgHouseRulePrompt");
    public string MsgHouseRuleTimeout => _s.String("msgHouseRuleTimeout");
    public string MsgHouseTableOpen => _s.String("msgHouseTableOpen");
    public string MsgHouseSeatTaken => _s.String("msgHouseSeatTaken");
    public string MsgHouseSeatLeft => _s.String("msgHouseSeatLeft");
    public string MsgHouseNoPlayers => _s.String("msgHouseNoPlayers");
    public string MsgHouseTableFull => _s.String("msgHouseTableFull");
    public string MsgHouseAlreadySeated => _s.String("msgHouseAlreadySeated");
    public string MsgHouseNoTable => _s.String("msgHouseNoTable");
    public string MsgHouseJoinClosed => _s.String("msgHouseJoinClosed");
    public string MsgHouseDealt => _s.String("msgHouseDealt");
    public string MsgHouseHandDealt => _s.String("msgHouseHandDealt");
    public string MsgHouseTurn => _s.String("msgHouseTurn");
    public string MsgHouseNotYourTurn => _s.String("msgHouseNotYourTurn");
    public string MsgHouseNotPlaying => _s.String("msgHouseNotPlaying");
    public string MsgHouseNotSeated => _s.String("msgHouseNotSeated");
    public string MsgHouseDrawCard => _s.String("msgHouseDrawCard");
    public string MsgHouseStand => _s.String("msgHouseStand");
    public string MsgHouseBust => _s.String("msgHouseBust");
    public string MsgHouseNextHand => _s.String("msgHouseNextHand");
    public string MsgHouseTurnTimeout => _s.String("msgHouseTurnTimeout");
    public string MsgHouseSeatBlackjack => _s.String("msgHouseSeatBlackjack");
    public string MsgHouseInsuranceOffer => _s.String("msgHouseInsuranceOffer");
    public string MsgHouseInsuranceTaken => _s.String("msgHouseInsuranceTaken");
    public string MsgHouseInsuranceWin => _s.String("msgHouseInsuranceWin");
    public string MsgHouseInsuranceLose => _s.String("msgHouseInsuranceLose");
    public string MsgHousePeekBlackjack => _s.String("msgHousePeekBlackjack");
    public string MsgHouseDealerBlackjack => _s.String("msgHouseDealerBlackjack");
    public string MsgHouseDealerReveal => _s.String("msgHouseDealerReveal");
    public string MsgHouseSeatWin => _s.String("msgHouseSeatWin");
    public string MsgHouseSeatPush => _s.String("msgHouseSeatPush");
    public string MsgHouseSeatLoss => _s.String("msgHouseSeatLoss");
    public string MsgHouseTableSummary => _s.String("msgHouseTableSummary");
    public string MsgHouseSurrender => _s.String("msgHouseSurrender");
    public string MsgHouseActionNotAvailable => _s.String("msgHouseActionNotAvailable");
    public bool LogToFile => _s.Bool("logToFile");

    private static string BaseFolder
    {
        get
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var folder = Path.Combine(baseDir, "Chat Games", "Blackjack Game");
            Directory.CreateDirectory(folder);
            return folder;
        }
    }

    public static string FilePath => Path.Combine(BaseFolder, "BJG-Settings.json");

    public static string LogDirectory => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Chat Games", "Logs");

    public static string ActionLogPath => Path.Combine(LogDirectory, $"BJG-log-{DateTime.Now:yyyy-MM-dd}.txt");

    public string GetOverlayDirectory()
    {
        var dir = ResolveOverlayDirectory(OverlayFolder);
        Directory.CreateDirectory(dir);
        return dir;
    }

    public static string ResolveOverlayDirectory(string folder)
    {
        var fallback = Path.Combine(BaseFolder, "Overlay");
        if (string.IsNullOrWhiteSpace(folder)) return fallback;

        var cleaned = folder.Trim().Trim('"');
        if (cleaned.Length == 0) return fallback;

        try
        {
            return Path.IsPathRooted(cleaned)
                ? Path.GetFullPath(cleaned)
                : Path.Combine(BaseFolder, cleaned);
        }
        catch (Exception)
        {
            return fallback;
        }
    }

    public string GetOverlayHtmlPath() => Path.Combine(GetOverlayDirectory(), "blackjack_overlay.html");
    public string GetOverlayStatePath() => Path.Combine(GetOverlayDirectory(), "blackjack_state.json");

    public static Settings Load() => new Settings(UiSettings.Load(FilePath));

    /// <summary>Settings over JSON text instead of the file, for the engine tests.</summary>
    public static Settings FromJson(string json) => new Settings(UiSettings.Parse(json));
}
