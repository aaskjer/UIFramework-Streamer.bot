using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Globalization;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Newtonsoft.Json.Linq;

// UIFramework for streamer.bot: the settings action of a host that carries its spec and host code itself. Hosts
// not yet moved to the generic stub/Stub.cs use this template through tools/embed-spec.ps1.
//
// This is the whole action. It compiles with no reference beyond what Streamer.bot gives every action, loads
// UIFramework.dll at run time and hands it three things: the spec (how this window is called and laid out),
// the schema (what the game knows, asked from the game action) and a Host object the DLL talks back to.
//
// No references to add: the dialogs are Win32 message boxes.
// Between the HOST markers sits what differs per project; tools/embed-spec.ps1 splices a host's section from
// hosts/<host>/host-section.cs and its spec from hosts/<host>/spec.json into this template. Everything outside
// the markers is the same in every host; tools/check-stub.ps1 checks that.

public class CPHInline
{
    // ==== HOST ====
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

        // What would not do what it looks like, one problem per line; the window asks before saving anyway.
        public string Validate(string settingsJson)
        {
            var s = JObject.Parse(settingsJson);
            var problems = new List<string>();
            if (!((bool?)s["freeGame"] ?? false) && string.IsNullOrWhiteSpace((string)s["pointsVariable"]))
                problems.Add("Free Game is off but the points variable is empty, so no stake can ever be taken.");
            Stakes(s, "minBet", "maxBet", "defaultBet", "the table", problems);
            Stakes(s, "duelMinBet", "duelMaxBet", null, "a duel", problems);
            Stakes(s, "houseMinBet", "houseMaxBet", null, "a House seat", problems);
            return problems.Count == 0 ? null : string.Join("\n", problems);
        }

        // A minimum above the maximum refuses every stake; a default outside them is reshaped on every hand.
        private static void Stakes(JObject s, string minKey, string maxKey, string defaultKey, string what, List<string> problems)
        {
            long min = (long?)s[minKey] ?? 0, max = (long?)s[maxKey] ?? 0;
            if (max > 0 && min > max)
                problems.Add("The minimum stake of " + what + " (" + min + ") is above its maximum (" + max + "), so every stake is refused.");
            if (defaultKey == null) return;
            long fallback = (long?)s[defaultKey] ?? 0;
            if (fallback > 0 && (fallback < min || (max > 0 && fallback > max)))
                problems.Add("The default bet (" + fallback + ") is outside the minimum and maximum of " + what + ", so a hand without an amount is reshaped every time.");
        }

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
    // ==== END HOST ====

    // The spec: the same parts as the window declaration in docs/Schema.md, as JSON. Written in by
    // tools/embed-spec.ps1 from hosts/<host>/spec.json; edit the JSON.
    private const string Spec = @"{""title"":""Blackjack Game"",""version"":""v17.0.0"",""releases"":""aaskjer/Blackjack-Game"",""logo"":""https://github.com/aaskjer/Blackjack-Game/blob/main/Assets/logo.png?raw=true"",""logPrefix"":""[Blackjack Game/Settings]"",""pages"":[{""id"":""table"",""title"":""Table"",""icon"":""🃏""},{""id"":""duel"",""title"":""Duel"",""icon"":""⚔️""},{""id"":""house"",""title"":""House"",""icon"":""🏠""},{""id"":""access"",""title"":""Access"",""icon"":""👥""},{""id"":""overlay"",""title"":""Overlay"",""icon"":""📺""},{""id"":""messages"",""title"":""Messages"",""icon"":""💬""},{""id"":""stats"",""title"":""Stats"",""icon"":""🏆"",""fields"":[""leaderboard*"",""statsTable"",""resetLeaderboard""]},{""id"":""log"",""title"":""Log"",""icon"":""📜""},{""id"":""help"",""title"":""How to play"",""icon"":""📖""},{""id"":""about"",""title"":""About"",""icon"":""ℹ️""}],""senders"":{""useBot"":""useBot"",""sendAction"":""sendAction"",""cooldownUseBot"":""cooldownUseBot"",""cooldownSendAction"":""cooldownSendAction"",""numberSeparator"":""numberSeparator"",""cooldownGroup"":""Cooldown""},""samples"":{""cards"":""🂫(10) 🂺(10)"",""card"":""🂺(10)"",""value"":""20"",""total"":""22"",""handnum"":""2"",""hands"":""Tie on Hand 1!"",""card1"":""🂫(10)"",""card2"":""🂺(10)"",""challenger"":""alice"",""opponent"":""bob"",""challengercards"":""🂫(10) 🂺(10) (20)"",""opponentcards"":""🂱(11) 🂸(8) (19)"",""challengervalue"":""20"",""opponentvalue"":""19"",""winner"":""alice"",""loser"":""bob"",""pot"":""3,000"",""bet"":""1,500"",""newbet"":""3,000"",""raise"":""doubles down"",""turn"":""bob"",""round"":""1"",""seconds"":""60"",""required"":""1,500"",""available"":""820"",""var"":""points"",""action"":""insurance"",""extra"":"", double or split"",""addictedCutPercent"":""50"",""menu"":""1) Vegas Strip | 2) Atlantic City | 3) European"",""rules"":""Vegas Strip"",""rulesdetail"":""4 deck shoe, dealer stands on soft 17, insurance offered, blackjack pays 3:2"",""seats"":""alice: 20 win | bob: 19 lose"",""seatnum"":""2"",""dealer"":""🂱(11) 🂸(8)"",""dealervalue"":""19"",""playercards"":""🂫(10) 🂺(10)"",""playervalue"":""20"",""FormatNumber(bet)"":""1,500"",""0"":""alice"",""1"":""12"",""balance"":""11,500""},""headerPills"":[{""key"":""freeGame"",""on"":""Free game on"",""off"":""Free game off"",""tooltip"":""Free Game, from the Table page. While it is on, nobody stakes or wins anything.""}],""log"":{""folder"":""Chat Games/Logs"",""writeLogKey"":""logToFile"",""writeLogHelp"":""Writes every settled hand to a dated \u0027BJG-log-yyyy-MM-dd.txt\u0027 in the Logs folder, one file per day."",""columns"":[{""header"":""Player"",""field"":""User""},{""header"":""Bet"",""field"":""Bet""},{""header"":""Change"",""field"":""Change""},{""header"":""After"",""field"":""After""}],""results"":[""Win"",""Blackjack"",""Loss"",""Bust"",""Push"",""Charlie"",""Three 7s"",""Insurance"",""Surrender"",""Quit"",""Timeout""]},""help"":[{""heading"":""The idea"",""lines"":[{""note"":""Get closer to 21 than the dealer without going past it. Number cards count their face value, Jack, Queen and King count 10, and an Ace counts 11 until that would bust the hand, at which point it drops to 1 on its own.""}]},{""heading"":""Playing a hand"",""lines"":[{""command"":""!blackjack \u003cbet\u003e"",""what"":""Deals in. The bet takes a number, one of the shortcuts below, or nothing at all to use the Default bet.""},{""command"":""hit"",""what"":""Draw another card.""},{""command"":""stand"",""what"":""Stop and keep the total you have.""},{""command"":""double"",""what"":""Double the bet for exactly one more card, then stand. First move on a two-card hand only.""},{""command"":""split"",""what"":""Split a starting pair of equal-value cards into two hands, each played out for the same bet.""},{""command"":""insurance"",""what"":""Stake half the bet against a dealer blackjack, paying 2:1. Only against an Ace, and only before anything else.""},{""command"":""surrender"",""what"":""Fold a fresh two-card hand and keep half the bet.""},{""command"":""quit"",""what"":""Cancel and take the bet back, before hit, stand, double or split.""},{""command"":""repeat"",""what"":""Post the last game message again. In a duel it posts both hands, the pot and whose turn it is.""},{""command"":""stats / allstats"",""what"":""Your own record, or the leaderboard.""},{""note"":""Those are the default trigger words; your streamer may have bound them to something else.""}]},{""heading"":""Bet shortcuts"",""lines"":[{""command"":""all / allin"",""what"":""The whole balance.""},{""command"":""half"",""what"":""Half the balance.""},{""command"":""quarter / 25%"",""what"":""A quarter of it.""},{""command"":""50%, any percentage"",""what"":""That much of the balance.""},{""command"":""double / 2x"",""what"":""Twice the minimum bet.""},{""command"":""random / rand"",""what"":""Somewhere between a tenth of the balance and all of it.""},{""command"":""max / maximum"",""what"":""As much as the balance and the Maximum bet allow.""},{""command"":""1.5k / 2m"",""what"":""1,500 and 2,000,000 - k for thousand, m for million.""}]},{""heading"":""Bonus hands"",""lines"":[{""note"":""Both settle the moment they happen, whatever the dealer is holding.""},{""command"":""5-card Charlie"",""what"":""Five cards without busting. Pays the bet back plus a 1.5x bonus.""},{""command"":""Three 7s"",""what"":""Three or more sevens without busting. Pays the bet back plus a 2x bonus.""}]},{""heading"":""Insurance, in full"",""lines"":[{""note"":""Insurance is only offered when the dealer\u0027s upcard is an Ace, because that is the only time a dealer blackjack - Ace plus a ten-value card - is one card away. It costs half the original bet.""},{""command"":""Dealer has blackjack"",""what"":""Insurance pays 2:1, so three times the insurance stake comes back. That roughly covers losing the main hand.""},{""command"":""Dealer does not"",""what"":""The insurance stake is gone and the hand plays on as normal.""},{""note"":""It has to be decided before hitting, standing, doubling or splitting. Over many hands it loses the player money, which is why plenty of streamers leave it off.""}]},{""heading"":""Surrender, in full"",""lines"":[{""note"":""Surrender is a way out of a hand that looks lost before any of it has been played. It is available on the first two cards only, before hitting, doubling, splitting or insuring, and returns half the bet.""},{""note"":""A 16 against a dealer\u0027s 10 is the usual case: almost any card busts it, and standing on 16 rarely beats a ten either. Half the bet back is the better of two bad outcomes.""}]}],""about"":{""credits"":""Responsible: aaskjer\nCredits: nate1280 / Streamer.bot and YOU ♡"",""sections"":[{""heading"":""Needs a points system"",""lines"":[{""note"":""Hands are played with the points of a currency system. These two are picked up without any setup:""},{""link"":""Currency Core by StreamUP"",""url"":""https://streamup.tips/product/currency-core""},{""link"":""LOYALTY POINTS by tawmae"",""url"":""https://tawmae.xyz/loyalty-points""},{""note"":""Anything else works too - name its variable on the Access page.""}]},{""heading"":null,""lines"":[{""command"":null,""what"":null},{""link"":null,""url"":""string url""}]}],""links"":[{""text"":""Blackjack Game on the Streamer.bot Discord"",""url"":""https://discord.com/channels/834650675224248362/1447648588694818836/1447648588694818836""}]}}";

    // ================================================================================================
    // JOBS: the window runs on its own thread; anything that needs CPH is queued here and run by Pump,
    // which the DLL calls every 100 ms on Streamer.bot's thread while the window is open, and once after.
    // ================================================================================================

    private readonly ConcurrentQueue<Action> _jobs = new ConcurrentQueue<Action>();

    private void Enqueue(Action job) => _jobs.Enqueue(job);

    private void Pump()
    {
        while (_jobs.TryDequeue(out var job))
        {
            try { job(); }
            catch (Exception ex) { CPH.LogError(LogPrefix + " A queued step failed: " + ex.Message); }
        }
    }

    // The game describes itself: the stub asks, the game answers through a non-persistent global.
    // Streamer.bot keeps its globals in LiteDB, which refuses some characters in a name (brackets, a trailing
    // space): the variable takes the action's name with those replaced. The game builds the same name.
    public static string SchemaVar(string actionName) =>
        "uiframework.schema." + System.Text.RegularExpressions.Regex.Replace((actionName ?? "").Trim(), @"[^\w .-]", "_");

    private string AskSchema()
    {
        string var = SchemaVar(GameActionName);
        CPH.UnsetGlobalVar(var, false);
        CPH.SetArgument("uiframework.describe", "1");
        if (!CPH.RunAction(GameActionName, true))
            throw new InvalidOperationException("The action '" + GameActionName + "' did not run, so there is no schema to show.");
        string schema = CPH.GetGlobalVar<string>(var, false);
        if (string.IsNullOrWhiteSpace(schema))
            throw new InvalidOperationException("The action '" + GameActionName + "' did not describe its settings. Is it the current version?");
        return schema;
    }

    // ================================================================================================
    // LOADER: finds UIFramework.dll under <Streamer.bot>\UIFramework\lib\<version>\. The first time, it asks
    // whether to download the DLL from GitHub or to show the folder it belongs in. Every later open looks for a
    // newer release and asks whether to fetch it; "Cancel" skips that version until the next one.
    // ================================================================================================

    private const string FrameworkName = "UIFramework for streamer.bot";
    // The API answers with the file as it is on main right now; the raw CDN keeps a copy for up to five minutes and
    // ignores a cache-busting query, so it is the fallback only (rate limit on the API, or the API unreachable).
    private const string LatestApiUrl = "https://api.github.com/repos/aaskjer/UIFramework-Streamer.bot/contents/lib/v1/latest.json?ref=main";
    private const string LatestUrl = "https://raw.githubusercontent.com/aaskjer/UIFramework-Streamer.bot/main/lib/v1/latest.json";
    private const string FrameworkReleasesUrl = "https://github.com/aaskjer/UIFramework-Streamer.bot/releases";
    private static readonly string LibRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UIFramework", "lib");

    private static string FindFramework()
    {
        if (!Directory.Exists(LibRoot))
            return null;
        return Directory.GetDirectories(LibRoot)
            .Select(dir => new { Dir = dir, Version = ParseVersion(Path.GetFileName(dir)) })
            .Where(x => x.Version != null && File.Exists(Path.Combine(x.Dir, "UIFramework.dll")))
            .OrderByDescending(x => x.Version)
            .Select(x => Path.Combine(x.Dir, "UIFramework.dll"))
            .FirstOrDefault();
    }

    private static Version ParseVersion(string text)
    {
        text = (text ?? "").TrimStart('v', 'V');
        return Version.TryParse(text.Contains(".") ? text : text + ".0", out var v) ? v : null;
    }

    /// <summary>The DLL, or null after the user was told what to do.</summary>
    private string EnsureFramework()
    {
        string dll = FindFramework();
        if (dll != null)
            return dll;

        var answer = Ask(
            "The settings window is built by UIFramework.dll, which is not installed in this Streamer.bot yet.\n\n" +
            "Yes: download it now from GitHub into\n" + LibRoot + "\n\n" +
            "No: I already have the file; show me the folder it belongs in.",
            MbYesNoCancel, MbIconQuestion);
        if (answer == IdYes)
        {
            try
            {
                dll = Download(null);
                CPH.LogInfo(LogPrefix + " Downloaded " + dll);
                return dll;
            }
            catch (Exception ex)
            {
                CPH.LogError(LogPrefix + " UIFramework.dll could not be downloaded: " + ex.Message);
                Ask("The download failed:\n\n" + ex.Message + "\n\nGet UIFramework.dll by hand from\n" + FrameworkReleasesUrl +
                    "\nand put it into a version folder under\n" + LibRoot + "\nfor example " + Path.Combine(LibRoot, "1.0.0") + "\\UIFramework.dll, then open the settings again.",
                    MbOk, MbIconWarning);
                OpenFolder(LibRoot);
                return null;
            }
        }
        if (answer == IdNo)
        {
            Ask("Put UIFramework.dll into a version folder under\n" + LibRoot + "\nfor example " + Path.Combine(LibRoot, "1.0.0") + "\\UIFramework.dll\n\n" +
                "The folder opens now. Then open the settings again.\nDownloads: " + FrameworkReleasesUrl,
                MbOk, MbIconInformation);
            OpenFolder(LibRoot);
        }
        return null;
    }

    private static void OpenFolder(string folder)
    {
        try
        {
            Directory.CreateDirectory(folder);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(folder) { UseShellExecute = true });
        }
        catch (Exception) { }
    }

    // The dialogs are Win32 message boxes (user32), so the action needs no WPF references. Each runs on a thread
    // of its own: the action runs on Streamer.bot's thread, which is no place for a dialog.
    private const uint MbOk = 0x0, MbYesNoCancel = 0x3, MbIconQuestion = 0x20, MbIconWarning = 0x30, MbIconInformation = 0x40;
    private const int IdCancel = 2, IdYes = 6, IdNo = 7;

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr owner, string text, string caption, uint type);

    private static int Ask(string text, uint buttons, uint icon)
    {
        int result = 0;
        var thread = new Thread(() =>
        {
            try { result = MessageBoxW(IntPtr.Zero, text, FrameworkName, buttons | icon | 0x10000 /* MB_SETFOREGROUND */ | 0x40000 /* MB_TOPMOST */); }
            catch (Exception) { }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        return result;
    }

    // WebClient waits 100 s by default; a release check at window open gets five.
    private sealed class QuickWebClient : System.Net.WebClient
    {
        public int TimeoutMs = 5000;
        protected override System.Net.WebRequest GetWebRequest(Uri address)
        {
            var request = base.GetWebRequest(address);
            if (request != null) request.Timeout = TimeoutMs;
            return request;
        }
    }

    /// <summary>What lib/v1/latest.json says: version, download url, sha256.</summary>
    private static JObject FetchLatest()
    {
        System.Net.ServicePointManager.SecurityProtocol |= System.Net.SecurityProtocolType.Tls12;
        string json;
        using (var client = new QuickWebClient())
        {
            client.Headers.Add("User-Agent", "UIFramework-Loader/1.0");
            client.Headers.Add("Accept", "application/vnd.github.raw");
            try { json = client.DownloadString(LatestApiUrl); }
            catch (Exception)
            {
                client.Headers.Remove("Accept");
                json = client.DownloadString(LatestUrl + "?t=" + DateTime.UtcNow.Ticks);
            }
        }
        var latest = JObject.Parse(json);
        if (string.IsNullOrWhiteSpace((string)latest["version"]) || string.IsNullOrWhiteSpace((string)latest["url"]))
            throw new InvalidOperationException("latest.json names no version or url; no release has been published yet.");
        return latest;
    }

    /// <summary>Downloads the release latest.json names into lib\<version>\, checks its hash, returns the path.
    /// With <paramref name="latest"/> null it is fetched first. One download at a time across every host.</summary>
    private static string Download(JObject latest)
    {
        using (var mutex = new Mutex(false, @"Global\UIFramework.Loader"))
        {
            mutex.WaitOne(TimeSpan.FromSeconds(60));
            try
            {
                latest = latest ?? FetchLatest();
                string version = ((string)latest["version"]).TrimStart('v', 'V');
                string target = Path.Combine(LibRoot, version, "UIFramework.dll");
                if (File.Exists(target))
                    return target;
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                string temp = target + ".download";
                System.Net.ServicePointManager.SecurityProtocol |= System.Net.SecurityProtocolType.Tls12;
                using (var client = new QuickWebClient { TimeoutMs = 60000 })
                {
                    client.Headers.Add("User-Agent", "UIFramework-Loader/1.0");
                    client.DownloadFile((string)latest["url"], temp);
                }
                string expected = ((string)latest["sha256"] ?? "").Trim().ToLowerInvariant();
                if (expected.Length > 0)
                {
                    string actual;
                    using (var sha = System.Security.Cryptography.SHA256.Create())
                    using (var stream = File.OpenRead(temp))
                        actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                    if (actual != expected)
                    {
                        File.Delete(temp);
                        throw new InvalidOperationException("the downloaded file does not match the published hash (" + actual + " vs " + expected + ").");
                    }
                }
                File.Move(temp, target);
                return target;
            }
            finally
            {
                try { mutex.ReleaseMutex(); } catch (Exception) { }
            }
        }
    }

    private static string LayoutPath => Path.Combine(Path.GetDirectoryName(SettingsPath), "uiframework.layout.json");

    private static JObject ReadLayout()
    {
        try { return File.Exists(LayoutPath) ? JObject.Parse(File.ReadAllText(LayoutPath)) : new JObject(); }
        catch (Exception) { return new JObject(); }
    }

    private static bool AutoUpdateEnabled()
    {
        var token = ReadLayout()["autoUpdate"];
        return token == null || token.Type != JTokenType.Boolean || (bool)token;
    }

    // "Cancel" in the update dialog: that version is not offered again.
    private static string SkippedVersion() => (string)ReadLayout()["skipVersion"];

    private static void SkipVersion(string version) => WriteLayout(layout => layout["skipVersion"] = version);

    // The note for the window's header pill: a release the user answered "No" to; removed again otherwise.
    private static void NoteLayout(string key, string value) => WriteLayout(layout => { if (value == null) layout.Remove(key); else layout[key] = value; });

    private static void WriteLayout(Action<JObject> change)
    {
        try
        {
            var layout = ReadLayout();
            change(layout);
            Directory.CreateDirectory(Path.GetDirectoryName(LayoutPath));
            File.WriteAllText(LayoutPath, layout.ToString(Newtonsoft.Json.Formatting.Indented));
        }
        catch (Exception) { }
    }

    /// <summary>Looks for a newer release than the one installed and offers to fetch it. Returns the DLL to use:
    /// the new one when it could be loaded now, else the one that is there.</summary>
    private string OfferUpdate(string installed)
    {
        var have = ParseVersion(Path.GetFileName(Path.GetDirectoryName(installed)));
        if (!AutoUpdateEnabled())
        {
            CPH.LogInfo(LogPrefix + " UIFramework " + have + " in use; the release check is off (autoUpdate: false in uiframework.layout.json).");
            return installed;
        }
        JObject latest;
        try { latest = FetchLatest(); }
        catch (Exception ex)
        {
            CPH.LogWarn(LogPrefix + " UIFramework " + have + " in use; the release check failed: " + ex.Message);
            return installed;
        }
        var newest = ParseVersion((string)latest["version"]);
        if (newest == null || have == null || newest <= have)
        {
            CPH.LogInfo(LogPrefix + " UIFramework " + have + " in use; latest.json says " + (string)latest["version"] + ", nothing newer.");
            NoteLayout("frameworkAvailable", null);
            return installed;
        }
        if (string.Equals(SkippedVersion(), newest.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            CPH.LogInfo(LogPrefix + " UIFramework " + have + " in use; " + newest + " is available but was skipped (skipVersion in uiframework.layout.json).");
            NoteLayout("frameworkAvailable", null);
            return installed;
        }
        CPH.LogInfo(LogPrefix + " UIFramework " + have + " in use; " + newest + " is available, asking.");

        bool loaded;
        lock (BridgeGate) loaded = _show != null;
        var answer = Ask(
            "A newer version of " + FrameworkName + " is available: " + newest + " (installed: " + have + ").\n\n" +
            "Yes: download it now" + (loaded ? "; it is used from the next start of Streamer.bot." : " and open the settings with it.") + "\n" +
            "No: ask again next time.\n" +
            "Cancel: skip version " + newest + ".",
            MbYesNoCancel, MbIconInformation);
        if (answer == IdCancel)
        {
            SkipVersion(newest.ToString());
            NoteLayout("frameworkAvailable", null);
            CPH.LogInfo(LogPrefix + " UIFramework " + newest + " skipped; the next release will be offered again.");
            return installed;
        }
        if (answer != IdYes)
        {
            NoteLayout("frameworkAvailable", newest.ToString());   // the window shows a pill until it is taken
            CPH.LogInfo(LogPrefix + " UIFramework " + newest + " not downloaded now; asked again next time.");
            return installed;
        }
        try
        {
            string path = Download(latest);
            NoteLayout("frameworkAvailable", null);
            CPH.LogInfo(LogPrefix + " UIFramework " + newest + " downloaded to " + path + (loaded ? "; it is used from the next start of Streamer.bot." : "."));
            return loaded ? installed : path;
        }
        catch (Exception ex)
        {
            NoteLayout("frameworkAvailable", newest.ToString());
            CPH.LogError(LogPrefix + " UIFramework " + newest + " could not be downloaded: " + ex.Message);
            Ask("The download failed:\n\n" + ex.Message + "\n\nThe installed version " + have + " is used. Releases: " + FrameworkReleasesUrl,
                MbOk, MbIconWarning);
            return installed;
        }
    }

    // ================================================================================================
    // BRIDGE: reflection into the DLL. LoadFrom, so every host in this Streamer.bot shares one instance.
    // ================================================================================================

    private static readonly object BridgeGate = new object();
    private static MethodInfo _show, _close;

    private static bool Bind(string dllPath)
    {
        lock (BridgeGate)
        {
            if (_show != null) return true;
            var assembly = Assembly.LoadFrom(dllPath);
            var entry = assembly.GetType("UIFramework.Entry");
            _show = entry?.GetMethod("Show", new[] { typeof(string), typeof(string), typeof(string), typeof(object), typeof(Action) });
            _close = entry?.GetMethod("Close", new[] { typeof(string) });
            return _show != null;
        }
    }

    public bool Execute()
    {
        string dll;
        try { dll = EnsureFramework(); }
        catch (Exception ex)
        {
            CPH.LogError(LogPrefix + " UIFramework.dll could not be set up: " + ex.Message);
            return false;
        }
        if (dll == null)
        {
            CPH.LogWarn(LogPrefix + " UIFramework.dll is not installed yet; the settings window stays closed until it is.");
            return false;
        }
        try
        {
            dll = OfferUpdate(dll);
            if (!Bind(dll))
            {
                CPH.LogError(LogPrefix + " " + dll + " has no UIFramework.Entry.Show; a newer or older build than this action expects.");
                return false;
            }
            string schema = AskSchema();
            _show.Invoke(null, new object[] { Spec, schema, SettingsPath, new Host(this), new Action(Pump) });
            Pump();
            return true;
        }
        catch (TargetInvocationException ex)
        {
            CPH.LogError(LogPrefix + " The settings window failed: " + (ex.InnerException ?? ex).Message);
            return false;
        }
        catch (Exception ex)
        {
            CPH.LogError(LogPrefix + " The settings window failed: " + ex.Message);
            return false;
        }
    }

    // Streamer.bot recompiles or shuts down: close the window without asking.
    public void Dispose()
    {
        try { _close?.Invoke(null, new object[] { SettingsPath }); } catch (Exception) { }
    }
}
