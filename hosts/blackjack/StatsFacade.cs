public sealed class StatsSettings
{
    private readonly UiSettings _s;

    private StatsSettings(UiSettings s) { _s = s; }

    public bool UseBot => _s.Bool("useBot");
    public bool SendAction => _s.Bool("sendAction");
    public string NumberSeparator => _s.String("numberSeparator");
    public string PointsVarName => _s.String("pointsName");
    public string MsgNoStatsFound => _s.String("leaderboardEmpty");
    public int LeaderboardSize => UiSettings.Clamp(_s.Int("leaderboardSize"), 1, 10);
    public bool LeaderboardShowMedals => _s.Bool("leaderboardShowMedals");
    public bool LeaderboardExcludeBroadcaster => _s.Bool("leaderboardExcludeBroadcaster");
    public bool LeaderboardExcludeBot => _s.Bool("leaderboardExcludeBot");
    public bool LeaderboardUseBot => _s.Bool("leaderboardUseBot");
    public bool LeaderboardSendAction => _s.Bool("leaderboardSendAction");
    public string LeaderboardAutoReset => _s.String("leaderboardAutoReset");
    public string LeaderboardResetScope => _s.String("leaderboardResetScope");

    private static string BaseFolder
    {
        get
        {
            var folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Chat Games", "Blackjack Game");
            Directory.CreateDirectory(folder);
            return folder;
        }
    }

    private static string FilePath => Path.Combine(BaseFolder, "BJG-Settings.json");
    public static string LegacyStatsPath => Path.Combine(BaseFolder, "CBJG-PlayerStats.json");

    public static StatsSettings Load() => new StatsSettings(UiSettings.Load(FilePath));
}
