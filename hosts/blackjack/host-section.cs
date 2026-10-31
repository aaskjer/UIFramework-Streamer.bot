    // ================================================================================================
    // HOST: Blackjack Game
    // ================================================================================================

    private const string GameActionName = "Blackjack Game";          // answers uiframework.describe
    private const string LogPrefix = "[Blackjack Game/Settings]";
    private static readonly string SettingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Chat Games", "Blackjack Game", "BJG-Settings.json");
    private static readonly string LogsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Chat Games", "Logs");

    // Command ids the cooldown settings are applied to, by setting key.
    private static readonly string[][] Cooldowns =
    {
        new[] { "globalCooldown", "userCooldown", "c6497fbd-c4b0-4c96-a398-e52d573cfe85" },
        new[] { "duelGlobalCooldown", "duelUserCooldown", "8ba7b2b7-f9f9-4b70-8cca-ee2aaebe4892" },
        new[] { "houseGlobalCooldown", "houseUserCooldown", "57cccfe6-3ece-428e-8567-b99823a12dac" },
    };

    // The leaderboard money totals a scoped reset clears, and every variable the game writes for the full wipe.
    private static readonly string[] LeaderboardTotalsVars =
    {
        "bjg_totalBet", "bjg_totalWon", "bjg_netProfit", "bjg_duelPointsWon", "bjg_duelPointsLost", "bjg_duelNet",
        "bjg_largestWin", "bjg_biggestLoss"
    };

    private static readonly string[] AllStatVarNames =
    {
        "bjg_userName", "bjg_firstPlayed", "bjg_lastPlayed",
        "bjg_gamesPlayed", "bjg_gamesWon", "bjg_gamesLost", "bjg_pushes", "bjg_handsPlayed",
        "bjg_totalBet", "bjg_totalWon", "bjg_netProfit", "bjg_largestWin", "bjg_biggestLoss",
        "bjg_blackjacks", "bjg_busts", "bjg_dealerBustWins", "bjg_charlieWins", "bjg_sevensWins",
        "bjg_doubleDowns", "bjg_doubleDownWins", "bjg_resplits", "bjg_surrenders",
        "bjg_insuranceTaken", "bjg_insuranceWins", "bjg_insuranceNet",
        "bjg_currentStreak", "bjg_longestWinStreak", "bjg_longestLossStreak",
        "bjg_duelsPlayed", "bjg_duelsWon", "bjg_duelsLost", "bjg_duelPushes",
        "bjg_duelPointsWon", "bjg_duelPointsLost", "bjg_duelNet", "bjg_duelHandsPlayed",
        "bjg_duelBlackjacks", "bjg_duelBusts", "bjg_duelForfeits", "bjg_duelSplits",
        "bjg_duelDoubleDowns", "bjg_duelSuddenDeaths", "bjg_duelStreak", "bjg_duelLongestWinStreak",
        "bjg_houseGamesPlayed", "bjg_houseHandsPlayed", "bjg_houseWon", "bjg_houseLost",
        "bjg_housePushes", "bjg_houseBlackjacks"
    };

    /// <summary>What the DLL calls. Public methods, string arguments, no framework types: found by reflection.
    /// Everything that needs CPH is queued as a job and run by Pump on Streamer.bot's thread.</summary>
    public sealed class Host
    {
        private readonly CPHInline _owner;
        public Host(CPHInline owner) { _owner = owner; }

        public void Log(string level, string message)
        {
            if (level == "error") _owner.CPH.LogError(message);
            else if (level == "warn") _owner.CPH.LogWarn(message);
            else _owner.CPH.LogInfo(message);
        }

        public void OnSaved(string settingsJson)
        {
            var settings = JObject.Parse(settingsJson);
            _owner.Enqueue(() =>
            {
                foreach (var row in Cooldowns)
                {
                    _owner.CPH.CommandSetGlobalCooldownDuration(row[2], Math.Max(0, (int?)settings[row[0]] ?? 0));
                    _owner.CPH.CommandSetUserCooldownDuration(row[2], Math.Max(0, (int?)settings[row[1]] ?? 0));
                }
            });
        }

        public string Test(string name, string settingsJson)
        {
            var settings = JObject.Parse(settingsJson);
            switch (name)
            {
                case "resetLeaderboard":
                    _owner.Enqueue(() => _owner.ResetLeaderboard(settings));
                    return "Leaderboard reset queued; see the Streamer.bot log for the count.";
                case "setupObsOverlay":
                    _owner.Enqueue(() => _owner.SetupObsOverlay(settings));
                    return "OBS setup queued; see the Streamer.bot log for the result.";
                default:
                    return null;
            }
        }

        public string ReadTable(string key) => null;

        public IEnumerable<string> ReadLogLines()
        {
            if (!Directory.Exists(LogsDir))
                return new string[0];
            return Directory.GetFiles(LogsDir, "BJG-log*.txt").OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .SelectMany(ReadLinesSafe).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
        }

        private static IEnumerable<string> ReadLinesSafe(string file)
        {
            try { return File.ReadAllLines(file); } catch (Exception) { return new string[0]; }
        }
    }

    private void ResetLeaderboard(JObject settings)
    {
        string scope = ((string)settings["leaderboardResetScope"] ?? "totals").Trim().ToLowerInvariant();
        bool all = scope == "all";
        int cleared = 0;
        foreach (var name in all ? AllStatVarNames : LeaderboardTotalsVars)
        {
            try { CPH.UnsetAllUsersVar(name, true); cleared++; }
            catch (Exception ex) { CPH.LogError(LogPrefix + " Reset failed on " + name + ": " + ex.Message); }
        }
        // Note where the new period starts, so an automatic reset does not fire again right away.
        string mode = ((string)settings["leaderboardAutoReset"] ?? "off").Trim().ToLowerInvariant();
        if (mode == "weekly" || mode == "monthly")
            CPH.SetGlobalVar("bjg_leaderboardPeriod", CurrentPeriodKey(mode), true);
        CPH.LogInfo(LogPrefix + " Leaderboard reset by hand (" + scope + "): cleared " + cleared + " variable(s) for all users.");
    }

    private static string CurrentPeriodKey(string mode)
    {
        var now = DateTime.Now;
        if (mode == "monthly")
            return now.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        var cal = CultureInfo.InvariantCulture.Calendar;
        int week = cal.GetWeekOfYear(now, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
        return now.ToString("yyyy", CultureInfo.InvariantCulture) + "-W" + week.ToString("00", CultureInfo.InvariantCulture);
    }

    private void SetupObsOverlay(JObject settings)
    {
        int connectionId = (int?)settings["obsConnectionId"] ?? 0;
        string sceneName = (string)settings["obsSceneName"] ?? "Blackjack";
        string sourceName = (string)settings["obsSourceName"] ?? "Blackjack Overlay";
        try
        {
            if (!CPH.ObsIsConnected(connectionId))
            {
                CPH.LogWarn(LogPrefix + " OBS is not connected on connection #" + connectionId + "; nothing was set up.");
                return;
            }
            string folder = ResolveOverlayDirectory((string)settings["overlayFolder"]);
            Directory.CreateDirectory(folder);
            string htmlPath = Path.Combine(folder, "blackjack_overlay.html");
            // The overlay is installed by the game action, which writes it on its first run.
            if (!File.Exists(htmlPath))
                CPH.RunAction(GameActionName, true);
            if (!File.Exists(htmlPath))
            {
                CPH.LogWarn(LogPrefix + " The overlay file could not be created at " + htmlPath + "; run any Blackjack command once and try again.");
                return;
            }
            var scenes = JObject.Parse(CPH.ObsSendRaw("GetSceneList", "{}", connectionId) ?? "{}").SelectToken("..scenes") as JArray;
            if (scenes == null || !scenes.Any(s => string.Equals((string)s["sceneName"], sceneName, StringComparison.OrdinalIgnoreCase)))
                CPH.ObsSendRaw("CreateScene", new JObject { ["sceneName"] = sceneName }.ToString(Newtonsoft.Json.Formatting.None), connectionId);
            var items = JObject.Parse(CPH.ObsSendRaw("GetSceneItemList", new JObject { ["sceneName"] = sceneName }.ToString(Newtonsoft.Json.Formatting.None), connectionId) ?? "{}").SelectToken("..sceneItems") as JArray;
            if (items != null && items.Any(i => string.Equals((string)i["sourceName"], sourceName, StringComparison.OrdinalIgnoreCase)))
            {
                CPH.LogInfo(LogPrefix + " A source named '" + sourceName + "' is already in scene '" + sceneName + "'; nothing was changed.");
                return;
            }
            var payload = new JObject
            {
                ["sceneName"] = sceneName,
                ["inputName"] = sourceName,
                ["inputKind"] = "browser_source",
                ["inputSettings"] = new JObject
                {
                    ["is_local_file"] = true,
                    ["local_file"] = htmlPath,
                    ["width"] = 1300,
                    ["height"] = 1080,
                    ["css"] = "body { background-color: rgba(0, 0, 0, 0); margin: 0px auto; overflow: hidden; }"
                }
            };
            CPH.ObsSendRaw("CreateInput", payload.ToString(Newtonsoft.Json.Formatting.None), connectionId);
            CPH.LogInfo(LogPrefix + " '" + sourceName + "' is in scene '" + sceneName + "', pointing at " + htmlPath);
        }
        catch (Exception ex)
        {
            CPH.LogError(LogPrefix + " OBS overlay setup failed: " + ex.Message);
        }
    }

    // Same rule as the game's Settings.ResolveOverlayDirectory: a subfolder under the game folder, or a full path.
    private static string ResolveOverlayDirectory(string folder)
    {
        string baseFolder = Path.GetDirectoryName(SettingsPath);
        string fallback = Path.Combine(baseFolder, "Overlay");
        if (string.IsNullOrWhiteSpace(folder)) return fallback;
        string cleaned = folder.Trim().Trim('"');
        if (cleaned.Length == 0) return fallback;
        try { return Path.IsPathRooted(cleaned) ? Path.GetFullPath(cleaned) : Path.Combine(baseFolder, cleaned); }
        catch (Exception) { return fallback; }
    }
