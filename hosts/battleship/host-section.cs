    // ================================================================================================
    // HOST: Battleship Game
    // ================================================================================================

    private const string GameActionName = "Battleship Game";          // answers uiframework.describe, writes the overlay and the heat config
    private const string LogPrefix = "[Battleship Game/Settings]";
    private const string BattleshipCommandId = "33042222-a6c0-439d-8a50-e0033bfe692e";   // !battleship, where the cooldowns live
    private const string HeatMarkersUrl = "https://heat.j38.net/markers/?channel=";
    private const string ObsTransparentCss = "body { background-color: rgba(0, 0, 0, 0); margin: 0px auto; overflow: hidden; }";
    private static readonly string SettingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Chat Games", "Battleship Game", "BSG-Settings.json");
    private static readonly string LogsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Chat Games", "Logs");

    // The totals a scoped reset clears, and every variable the game writes for the full wipe.
    private static readonly string[] LeaderboardTotalsVars = { "bsg_pointsWon", "bsg_pointsLost", "bsg_netProfit" };
    private static readonly string[] AllStatVarNames =
    {
        "bsg_userName", "bsg_firstPlayed", "bsg_lastPlayed", "bsg_gamesPlayed", "bsg_gamesWon",
        "bsg_gamesLost", "bsg_gamesAbandoned", "bsg_shotsFired", "bsg_hits", "bsg_misses",
        "bsg_shipsSunk", "bsg_shipsLost", "bsg_perfectGames", "bsg_fewestShotsWin",
        "bsg_pointsWon", "bsg_pointsLost", "bsg_netProfit", "bsg_currentStreak", "bsg_bestStreak",
        "bsg_dailyWon", "bsg_dailyWonDate"
    };

    // The ships as the Fleet page knows them: the keys of the switch, the length and the value.
    private static readonly string[][] Ships =
    {
        new[] { "Carrier", "shipCarrierEnabled", "shipCarrierLength", "shipCarrierValue" },
        new[] { "Battleship", "shipBattleshipEnabled", "shipBattleshipLength", "shipBattleshipValue" },
        new[] { "Cruiser", "shipCruiserEnabled", "shipCruiserLength", "shipCruiserValue" },
        new[] { "Submarine", "shipSubmarineEnabled", "shipSubmarineLength", "shipSubmarineValue" },
        new[] { "Destroyer", "shipDestroyerEnabled", "shipDestroyerLength", "shipDestroyerValue" }
    };

    /// <summary>What the DLL calls. Public methods, string arguments, no framework types: found by reflection.
    /// Everything that needs CPH is queued as a job and run by Pump on Streamer.bot's thread; the summaries and
    /// the presets need nothing but the values, so they answer at once.</summary>
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

        // The cooldowns live on the !battleship command; the overlay reads the Click to Fire settings from
        // battleship_heat.json, which the game action writes on every run, so one run puts a change on a live
        // overlay straight away instead of at the next command.
        public void OnSaved(string settingsJson)
        {
            var settings = JObject.Parse(settingsJson);
            _owner.Enqueue(() =>
            {
                _owner.CPH.CommandSetGlobalCooldownDuration(BattleshipCommandId, Math.Max(0, (int?)settings["globalCooldown"] ?? 0));
                _owner.CPH.CommandSetUserCooldownDuration(BattleshipCommandId, Math.Max(0, (int?)settings["userCooldown"] ?? 0));
                _owner.RefreshOverlayHeatConfig();
            });
        }

        public string Test(string name, string settingsJson)
        {
            var settings = JObject.Parse(settingsJson);
            switch (name)
            {
                case "clockSummary": return ClockSummary(settings);
                case "fleetSummary": return FleetSummary(settings);
                case "presetBlitz": return Preset(6, "shipCarrierEnabled=4", "shipCruiserEnabled=3", "shipDestroyerEnabled=2");
                case "presetQuick": return Preset(8, "shipCarrierEnabled=5", "shipCruiserEnabled=3", "shipDestroyerEnabled=2");
                case "presetClassic": return Preset(10, "shipCarrierEnabled=5", "shipBattleshipEnabled=4", "shipCruiserEnabled=3", "shipSubmarineEnabled=3", "shipDestroyerEnabled=2");
                case "broadcasterFound":
                {
                    string found = _owner.DetectBroadcasterId();
                    return found.Length > 0
                        ? "Found in Streamer.bot: " + found + ". Used whenever the channel ID box is empty."
                        : "No Twitch broadcaster account is connected to Streamer.bot right now. Leave the channel ID empty only if one will be when you go live.";
                }
                case "resetLeaderboard":
                    _owner.Enqueue(() => _owner.ResetLeaderboard(settings));
                    return "Leaderboard reset queued; see the Streamer.bot log for the count.";
                case "setupObsOverlay":
                    _owner.Enqueue(() => _owner.SetupObsOverlay(settings));
                    return "OBS setup queued; see the Streamer.bot log for the result.";
                case "addHeatMarkers":
                    _owner.Enqueue(() => _owner.SetupHeatMarkers(settings));
                    return "Heat markers setup queued; see the Streamer.bot log for the result.";
                default:
                    return null;
            }
        }

        public string ReadTable(string key) => null;

        public string EmoteSources() => null;

        // What would not do what it looks like, one problem per line; the window asks before saving anyway.
        public string Validate(string settingsJson)
        {
            var s = JObject.Parse(settingsJson);
            var problems = new List<string>();
            if (!((bool?)s["freeGame"] ?? false) && string.IsNullOrWhiteSpace((string)s["pointsVariable"]))
                problems.Add("Free game is off but the points variable is empty, so nothing can be paid or charged.");
            int size = (int?)s["gridSize"] ?? 8, ships = 0, longest = 0;
            foreach (var ship in Ships)
            {
                if (!((bool?)s[ship[1]] ?? false)) continue;
                ships++;
                longest = Math.Max(longest, (int?)s[ship[2]] ?? 0);
            }
            if (ships == 0)
                problems.Add("No ship sails, so no battle can start. Switch at least one ship on, on the Fleet page.");
            else if (longest > size)
                problems.Add("A ship of length " + longest + " doesn't fit on a " + size + "×" + size + " board.");
            int turn = (int?)s["turnTimeout"] ?? 90, first = (int?)s["turnWarnFirstSec"] ?? 0, second = (int?)s["turnWarnSecondSec"] ?? 0;
            if (first > 0 && first >= turn)
                problems.Add("The first warning (" + first + "s) is not shorter than a turn (" + turn + "s), so it is never posted.");
            if (second > 0 && second >= turn)
                problems.Add("The final warning (" + second + "s) is not shorter than a turn (" + turn + "s), so it is never posted.");
            if (first > 0 && second > 0 && second >= first)
                problems.Add("The final warning (" + second + "s) comes before the first (" + first + "s); swap them.");
            if (((bool?)s["heatEnabled"] ?? false) && string.IsNullOrWhiteSpace((string)s["heatStreamerBotUrl"]))
                problems.Add("Click to Fire is on but the Streamer.bot WebSocket address is empty, so no click can reach the game.");
            return problems.Count == 0 ? null : string.Join("\n", problems);
        }

        public IEnumerable<string> ReadLogLines()
        {
            if (!Directory.Exists(LogsDir))
                return new string[0];
            return Directory.GetFiles(LogsDir, "BSG-log*.txt").OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .SelectMany(ReadLinesSafe).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
        }

        private static IEnumerable<string> ReadLinesSafe(string file)
        {
            try { return File.ReadAllLines(file); } catch (Exception) { return new string[0]; }
        }
    }

    // ---- The summaries and the presets, from the values as they stand in the window ----

    private static string ClockSummary(JObject s)
    {
        int deploy = Math.Max(5, (int?)s["deployTimeoutSec"] ?? 60);
        int turn = Math.Max(5, (int?)s["turnTimeout"] ?? 90);
        var warned = new List<string>();
        var skipped = new List<string>();
        foreach (var pair in new[] { new[] { "first", "turnWarnFirstSec" }, new[] { "final", "turnWarnSecondSec" } })
        {
            int at = (int?)s[pair[1]] ?? 0;
            if (at <= 0) continue;
            if (at < turn) warned.Add(at + "s");
            else skipped.Add(pair[0] + " (" + at + "s)");
        }
        string text = deploy + "s to open fire, then " + turn + "s per turn, "
                    + (warned.Count == 0 ? "with no warnings." : "warned at " + string.Join(" and ", warned) + " left.");
        if (skipped.Count > 0)
            text += " Skipped because they aren't shorter than a turn: " + string.Join(", ", skipped) + ".";
        return text;
    }

    private static string FleetSummary(JObject s)
    {
        int size = (int?)s["gridSize"] ?? 8;
        int cells = size * size, ships = 0, shipCells = 0, longest = 0;
        foreach (var ship in Ships)
        {
            int length = (int?)s[ship[2]] ?? 0;
            if (!((bool?)s[ship[1]] ?? false) || length <= 0) continue;
            ships++;
            shipCells += length;
            longest = Math.Max(longest, length);
        }
        if (ships == 0)
            return "No ship sails, so no battle can start.";
        if (longest > size)
            return "A ship of length " + longest + " doesn't fit on a " + size + "×" + size + " board.";
        // Fitted from 10,000-game runs of the Captain: ~36 shots on 8×8, ~62 on 10×10.
        int estimate = Math.Max(shipCells, (int)Math.Round(0.72 * cells - 10));
        return new JObject
        {
            ["lines"] = new JArray
            {
                new JObject { ["text"] = size + "×" + size + " · " + ships + " ships · about " + estimate + " shots per battle", ["big"] = true },
                shipCells + " of " + cells + " squares taken by ships."
            }
        }.ToString(Newtonsoft.Json.Formatting.None);
    }

    // A preset fills in the board and the ships: "switchKey=length" per ship that sails, every other ship off.
    private static string Preset(int size, params string[] sailing)
    {
        var set = new JObject { ["gridSize"] = size };
        foreach (var ship in Ships)
        {
            string picked = sailing.FirstOrDefault(x => x.StartsWith(ship[1] + "="));
            set[ship[1]] = picked != null;
            if (picked != null)
                set[ship[2]] = int.Parse(picked.Substring(ship[1].Length + 1), CultureInfo.InvariantCulture);
        }
        return new JObject { ["set"] = set, ["status"] = size + "×" + size + " with " + sailing.Length + " ships filled in; save to keep it." }.ToString(Newtonsoft.Json.Formatting.None);
    }

    private string DetectBroadcasterId()
    {
        try { return CPH.TwitchGetBroadcaster()?.UserId ?? ""; }
        catch (Exception) { return ""; }
    }

    // The game action writes the overlay's battleship_heat.json every time it runs. RunAction hands back the
    // action's own result, and the game ends a run with no command in it as false, so that is no sign of failure.
    private void RefreshOverlayHeatConfig()
    {
        try
        {
            if (CPH.ActionExists(GameActionName)) CPH.RunAction(GameActionName, true);
            else CPH.LogWarn(LogPrefix + " No action called '" + GameActionName + "' to pass the Click to Fire settings to the overlay. They arrive with the next command instead.");
        }
        catch (Exception ex) { CPH.LogError(LogPrefix + " Running " + GameActionName + " failed: " + ex.Message); }
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
            CPH.SetGlobalVar("bsg_leaderboardPeriod", CurrentPeriodKey(mode), true);
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

    // The overlay is laid out in vw/vh, so it looks the same at any source size - but clicks from Heat only line
    // up when the source covers the whole canvas, so the source is made exactly that size.
    private (int Width, int Height) ReadObsCanvasSize(int connectionId)
    {
        try
        {
            var reply = JObject.Parse(CPH.ObsSendRaw("GetVideoSettings", "{}", connectionId) ?? "{}");
            int width = reply.SelectTokens("..baseWidth").Select(t => (int?)t).FirstOrDefault() ?? 0;
            int height = reply.SelectTokens("..baseHeight").Select(t => (int?)t).FirstOrDefault() ?? 0;
            if (width > 0 && height > 0) return (width, height);
        }
        catch (Exception ex) { CPH.LogWarn(LogPrefix + " Could not read the OBS canvas size, using 1920x1080: " + ex.Message); }
        return (1920, 1080);
    }

    // Source names are global in OBS, not per scene, so a name already taken anywhere is left alone.
    private bool AddObsBrowserSource(int connectionId, string sceneName, string sourceName, JObject inputSettings)
    {
        var inputs = ObsNames(CPH.ObsSendRaw("GetInputList", "{}", connectionId), "inputs", "inputName");
        if (inputs.Any(n => string.Equals(n, sourceName, StringComparison.OrdinalIgnoreCase)))
        {
            CPH.LogInfo(LogPrefix + " OBS already has a source called '" + sourceName + "'; nothing was changed. Rename it in the window first for a fresh one.");
            return false;
        }
        var scenes = ObsNames(CPH.ObsSendRaw("GetSceneList", "{}", connectionId), "scenes", "sceneName");
        if (!scenes.Any(n => string.Equals(n, sceneName, StringComparison.OrdinalIgnoreCase)))
            CPH.ObsSendRaw("CreateScene", new JObject { ["sceneName"] = sceneName }.ToString(Newtonsoft.Json.Formatting.None), connectionId);
        var payload = new JObject { ["sceneName"] = sceneName, ["inputName"] = sourceName, ["inputKind"] = "browser_source", ["inputSettings"] = inputSettings };
        CPH.ObsSendRaw("CreateInput", payload.ToString(Newtonsoft.Json.Formatting.None), connectionId);
        return true;
    }

    private List<string> ObsNames(string rawJson, string arrayKey, string nameKey)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(rawJson)) return result;
        try
        {
            if (JObject.Parse(rawJson).SelectToken(".." + arrayKey) is JArray array)
                foreach (var item in array)
                {
                    string name = item[nameKey]?.ToString();
                    if (!string.IsNullOrEmpty(name)) result.Add(name);
                }
        }
        catch (Exception ex) { CPH.LogError(LogPrefix + " Could not read the OBS reply: " + ex.Message); }
        return result;
    }

    private void SetupObsOverlay(JObject settings)
    {
        int connectionId = Math.Max(0, (int?)settings["obsConnectionId"] ?? 0);
        string sceneName = ((string)settings["obsSceneName"] ?? "").Trim();
        string sourceName = ((string)settings["obsSourceName"] ?? "").Trim();
        if (sceneName.Length == 0) sceneName = "Battleship";
        if (sourceName.Length == 0) sourceName = "Battleship Overlay";
        try
        {
            if (!CPH.ObsIsConnected(connectionId))
            {
                CPH.LogWarn(LogPrefix + " OBS is not connected on connection #" + connectionId + "; nothing was set up. Check Stream Apps > OBS Studio in Streamer.bot.");
                return;
            }
            string folder = ResolveOverlayDirectory((string)settings["overlayFolder"]);
            Directory.CreateDirectory(folder);
            string htmlPath = Path.Combine(folder, "battleship_overlay.html");
            // The game action installs the overlay on any run, so a chatless first-time setup works.
            if (!File.Exists(htmlPath))
            {
                try { CPH.RunAction(GameActionName, true); }
                catch (Exception runEx) { CPH.LogError(LogPrefix + " Overlay install via " + GameActionName + " failed: " + runEx.Message); }
            }
            if (!File.Exists(htmlPath))
            {
                CPH.LogWarn(LogPrefix + " The overlay file is not there yet: " + htmlPath + ". It is written by the '" + GameActionName + "' action; check that the action exists under exactly that name, or run any Battleship command once in chat, then try again.");
                return;
            }
            var canvas = ReadObsCanvasSize(connectionId);
            if (!AddObsBrowserSource(connectionId, sceneName, sourceName, new JObject
                {
                    ["is_local_file"] = true,
                    ["local_file"] = htmlPath,
                    ["width"] = canvas.Width,
                    ["height"] = canvas.Height,
                    ["css"] = ObsTransparentCss
                }))
                return;
            CPH.LogInfo(LogPrefix + " '" + sourceName + "' is in scene '" + sceneName + "' at " + canvas.Width + "x" + canvas.Height + ", the size of your canvas, pointing at " + htmlPath);
        }
        catch (Exception ex)
        {
            CPH.LogError(LogPrefix + " OBS overlay setup failed: " + ex.Message + " (usually the connection # not matching what Streamer.bot has configured)");
        }
    }

    // Heat's own Markers page maps clicks across its whole page just as the overlay does, so it gets the canvas size too.
    private void SetupHeatMarkers(JObject settings)
    {
        int connectionId = Math.Max(0, (int?)settings["obsConnectionId"] ?? 0);
        string sceneName = ((string)settings["obsSceneName"] ?? "").Trim();
        string sourceName = ((string)settings["heatObsSourceName"] ?? "").Trim();
        if (sceneName.Length == 0) sceneName = "Battleship";
        if (sourceName.Length == 0) sourceName = "Heat Click Markers";
        string channel = ((string)settings["heatChannelId"] ?? "").Trim();
        if (channel.Length == 0) channel = DetectBroadcasterId();
        try
        {
            if (channel.Length == 0)
            {
                CPH.LogWarn(LogPrefix + " There is no Twitch channel ID to show markers for. Enter it on the Clicks page, or connect your broadcaster account in Streamer.bot, then try again.");
                return;
            }
            if (!CPH.ObsIsConnected(connectionId))
            {
                CPH.LogWarn(LogPrefix + " OBS is not connected on connection #" + connectionId + "; nothing was set up. Check Stream Apps > OBS Studio in Streamer.bot.");
                return;
            }
            string url = HeatMarkersUrl + channel;
            var canvas = ReadObsCanvasSize(connectionId);
            if (!AddObsBrowserSource(connectionId, sceneName, sourceName, new JObject
                {
                    ["is_local_file"] = false,
                    ["url"] = url,
                    ["width"] = canvas.Width,
                    ["height"] = canvas.Height,
                    ["css"] = ObsTransparentCss
                }))
                return;
            CPH.LogInfo(LogPrefix + " '" + sourceName + "' is in scene '" + sceneName + "' at " + canvas.Width + "x" + canvas.Height + ", showing " + url);
        }
        catch (Exception ex)
        {
            CPH.LogError(LogPrefix + " Heat markers setup failed: " + ex.Message + " (usually the connection # not matching what Streamer.bot has configured)");
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
