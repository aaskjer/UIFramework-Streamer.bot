using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

// UIFramework for streamer.bot: the settings action of a host.
//
// This is the whole action. It compiles with no reference beyond what Streamer.bot gives every action, loads
// UIFramework.dll at run time and hands it three things: the spec (how this window is called and laid out),
// the schema (what the game knows, asked from the game action) and a Host object the DLL talks back to.
//
// Fill in the HOST section. Everything under LOADER and BRIDGE is the same in every host; keep it identical
// to stub/Stub.cs in the UIFramework repository.

public class CPHInline
{
    // ================================================================================================
    // HOST: what differs per project
    // ================================================================================================

    private const string HostName = "Blackjack Game";
    private const string GameActionName = "Blackjack Game";          // answers uiframework.describe
    private const string LogPrefix = "[Blackjack Game/Settings]";
    private static readonly string SettingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Chat Games", "Blackjack Game", "BJG-Settings.json");
    private static readonly string LogsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Chat Games", "Logs");

    // Command ids the cooldown settings are applied to, by setting key.
    private static readonly (string GlobalKey, string UserKey, string CommandId)[] Cooldowns =
    {
        ("globalCooldown", "userCooldown", "c6497fbd-c4b0-4c96-a398-e52d573cfe85"),
        ("duelGlobalCooldown", "duelUserCooldown", "8ba7b2b7-f9f9-4b70-8cca-ee2aaebe4892"),
        ("houseGlobalCooldown", "houseUserCooldown", "57cccfe6-3ece-428e-8567-b99823a12dac"),
    };

    // The spec: see docs/Spec.md. Generated from hosts/<host>/spec.json by tools/embed-spec.ps1; edit the JSON.
    private const string Spec = @"{""title"":""Blackjack Game"",""version"":""v16.3.1"",""releases"":""aaskjer/Blackjack-Game"",""logo"":""https://github.com/aaskjer/Blackjack-Game/blob/main/Assets/logo.png?raw=true"",""logPrefix"":""[Blackjack Game/Settings]"",""pages"":[{""id"":""table"",""title"":""Table"",""icon"":""🃏""},{""id"":""duel"",""title"":""Duel"",""icon"":""⚔️""},{""id"":""house"",""title"":""House"",""icon"":""🏠""},{""id"":""access"",""title"":""Access"",""icon"":""👥""},{""id"":""overlay"",""title"":""Overlay"",""icon"":""📺""},{""id"":""messages"",""title"":""Messages"",""icon"":""💬""},{""id"":""stats"",""title"":""Stats"",""icon"":""🏆"",""fields"":[""leaderboard*"",""statsTable"",""resetLeaderboard""]},{""id"":""log"",""title"":""Log"",""icon"":""📜""},{""id"":""help"",""title"":""How to play"",""icon"":""📖""},{""id"":""about"",""title"":""About"",""icon"":""ℹ️""}],""senders"":{""useBot"":""useBot"",""sendAction"":""sendAction"",""cooldownUseBot"":""cooldownUseBot"",""cooldownSendAction"":""cooldownSendAction"",""numberSeparator"":""numberSeparator"",""cooldownGroup"":""Cooldown""},""samples"":{""cards"":""🂫(10) 🂺(10)"",""card"":""🂺(10)"",""value"":""20"",""total"":""22"",""handnum"":""2"",""hands"":""Tie on Hand 1!"",""card1"":""🂫(10)"",""card2"":""🂺(10)"",""challenger"":""alice"",""opponent"":""bob"",""challengercards"":""🂫(10) 🂺(10) (20)"",""opponentcards"":""🂱(11) 🂸(8) (19)"",""challengervalue"":""20"",""opponentvalue"":""19"",""winner"":""alice"",""loser"":""bob"",""pot"":""3,000"",""bet"":""1,500"",""newbet"":""3,000"",""raise"":""doubles down"",""turn"":""bob"",""round"":""1"",""seconds"":""60"",""required"":""1,500"",""available"":""820"",""var"":""points"",""action"":""insurance"",""extra"":"", double or split"",""addictedCutPercent"":""50"",""menu"":""1) Vegas Strip | 2) Atlantic City | 3) European"",""rules"":""Vegas Strip"",""rulesdetail"":""4 deck shoe, dealer stands on soft 17, insurance offered, blackjack pays 3:2"",""seats"":""alice: 20 win | bob: 19 lose"",""seatnum"":""2"",""dealer"":""🂱(11) 🂸(8)"",""dealervalue"":""19"",""playercards"":""🂫(10) 🂺(10)"",""playervalue"":""20"",""FormatNumber(bet)"":""1,500"",""0"":""alice"",""1"":""12"",""balance"":""11,500""},""headerPills"":[{""key"":""freeGame"",""on"":""Free game on"",""off"":""Free game off"",""tooltip"":""Free Game, from the Table page. While it is on, nobody stakes or wins anything.""}],""log"":{""folder"":""Chat Games/Logs"",""writeLogKey"":""logToFile"",""writeLogHelp"":""Writes every settled hand to a dated \u0027BJG-log-yyyy-MM-dd.txt\u0027 in the Logs folder, one file per day."",""columns"":[{""header"":""Player"",""field"":""User""},{""header"":""Bet"",""field"":""Bet""},{""header"":""Change"",""field"":""Change""},{""header"":""After"",""field"":""After""}],""results"":[""Win"",""Blackjack"",""Loss"",""Bust"",""Push"",""Charlie"",""Three 7s"",""Insurance"",""Surrender"",""Quit"",""Timeout""]},""help"":[{""heading"":""The idea"",""lines"":[{""note"":""Get closer to 21 than the dealer without going past it. Number cards count their face value, Jack, Queen and King count 10, and an Ace counts 11 until that would bust the hand, at which point it drops to 1 on its own.""}]},{""heading"":""Playing a hand"",""lines"":[{""command"":""!blackjack \u003cbet\u003e"",""what"":""Deals in. The bet takes a number, one of the shortcuts below, or nothing at all to use the Default bet.""},{""command"":""hit"",""what"":""Draw another card.""},{""command"":""stand"",""what"":""Stop and keep the total you have.""},{""command"":""double"",""what"":""Double the bet for exactly one more card, then stand. First move on a two-card hand only.""},{""command"":""split"",""what"":""Split a starting pair of equal-value cards into two hands, each played out for the same bet.""},{""command"":""insurance"",""what"":""Stake half the bet against a dealer blackjack, paying 2:1. Only against an Ace, and only before anything else.""},{""command"":""surrender"",""what"":""Fold a fresh two-card hand and keep half the bet.""},{""command"":""quit"",""what"":""Cancel and take the bet back, before hit, stand, double or split.""},{""command"":""repeat"",""what"":""Post the last game message again. In a duel it posts both hands, the pot and whose turn it is.""},{""command"":""stats / allstats"",""what"":""Your own record, or the leaderboard.""},{""note"":""Those are the default trigger words; your streamer may have bound them to something else.""}]},{""heading"":""Bet shortcuts"",""lines"":[{""command"":""all / allin"",""what"":""The whole balance.""},{""command"":""half"",""what"":""Half the balance.""},{""command"":""quarter / 25%"",""what"":""A quarter of it.""},{""command"":""50%, any percentage"",""what"":""That much of the balance.""},{""command"":""double / 2x"",""what"":""Twice the minimum bet.""},{""command"":""random / rand"",""what"":""Somewhere between a tenth of the balance and all of it.""},{""command"":""max / maximum"",""what"":""As much as the balance and the Maximum bet allow.""},{""command"":""1.5k / 2m"",""what"":""1,500 and 2,000,000 - k for thousand, m for million.""}]},{""heading"":""Bonus hands"",""lines"":[{""note"":""Both settle the moment they happen, whatever the dealer is holding.""},{""command"":""5-card Charlie"",""what"":""Five cards without busting. Pays the bet back plus a 1.5x bonus.""},{""command"":""Three 7s"",""what"":""Three or more sevens without busting. Pays the bet back plus a 2x bonus.""}]},{""heading"":""Insurance, in full"",""lines"":[{""note"":""Insurance is only offered when the dealer\u0027s upcard is an Ace, because that is the only time a dealer blackjack - Ace plus a ten-value card - is one card away. It costs half the original bet.""},{""command"":""Dealer has blackjack"",""what"":""Insurance pays 2:1, so three times the insurance stake comes back. That roughly covers losing the main hand.""},{""command"":""Dealer does not"",""what"":""The insurance stake is gone and the hand plays on as normal.""},{""note"":""It has to be decided before hitting, standing, doubling or splitting. Over many hands it loses the player money, which is why plenty of streamers leave it off.""}]},{""heading"":""Surrender, in full"",""lines"":[{""note"":""Surrender is a way out of a hand that looks lost before any of it has been played. It is available on the first two cards only, before hitting, doubling, splitting or insuring, and returns half the bet.""},{""note"":""A 16 against a dealer\u0027s 10 is the usual case: almost any card busts it, and standing on 16 rarely beats a ten either. Half the bet back is the better of two bad outcomes.""}]}],""about"":{""credits"":""Responsible: aaskjer\nCredits: nate1280 / Streamer.bot and YOU ♡"",""sections"":[{""heading"":""Needs a points system"",""lines"":[{""note"":""Hands are played with the points of a currency system. These two are picked up without any setup:""},{""link"":""Currency Core by StreamUP"",""url"":""https://streamup.tips/product/currency-core""},{""link"":""LOYALTY POINTS by tawmae"",""url"":""https://tawmae.xyz/loyalty-points""},{""note"":""Anything else works too - name its variable on the Access page.""}]},{""heading"":null,""lines"":[{""command"":null,""what"":null},{""link"":null,""url"":""string url""}]}],""links"":[{""text"":""Blackjack Game on the Streamer.bot Discord"",""url"":""https://discord.com/channels/834650675224248362/1447648588694818836/1447648588694818836""}]}}";

    /// <summary>What the DLL calls. Public methods, string arguments, no framework types: found by reflection.</summary>
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

        // Runs on the window's thread. Queue what needs Streamer.bot's thread; Pump applies it.
        public void OnSaved(string settingsJson)
        {
            var settings = JObject.Parse(settingsJson);
            var pending = new List<(string, int, int)>();
            foreach (var (globalKey, userKey, commandId) in Cooldowns)
                pending.Add((commandId, Math.Max(0, (int?)settings[globalKey] ?? 0), Math.Max(0, (int?)settings[userKey] ?? 0)));
            Interlocked.Exchange(ref _owner._pendingCooldowns, pending);
        }

        public string Test(string name, string settingsJson)
        {
            if (name == "resetLeaderboard")
            {
                _owner.CPH.SetGlobalVar("bjg_ResetLeaderboard", "True", false);
                return "Leaderboard reset queued.";
            }
            return null;
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

    private List<(string CommandId, int Global, int User)> _pendingCooldowns;

    // Called every 100 ms on Streamer.bot's thread while the window is open, and once after it closed.
    private void Pump()
    {
        var pending = Interlocked.Exchange(ref _pendingCooldowns, null);
        if (pending == null) return;
        foreach (var (commandId, global, user) in pending)
        {
            CPH.CommandSetGlobalCooldownDuration(commandId, global);
            CPH.CommandSetUserCooldownDuration(commandId, user);
        }
    }

    // The game describes itself: the stub asks, the game answers through a non-persistent global.
    private string AskSchema()
    {
        string var = "uiframework.schema." + GameActionName;
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
    // LOADER: finds UIFramework.dll. Phase 3 adds the download; until then the DLL has to be present.
    // ================================================================================================

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
        string dll = FindFramework();
        if (dll == null)
        {
            CPH.LogError(LogPrefix + " UIFramework.dll was not found under " + LibRoot + ". The settings window needs it; see the README.");
            return false;
        }
        try
        {
            if (!Bind(dll))
            {
                CPH.LogError(LogPrefix + " " + dll + " has no UIFramework.Entry.Show; a newer or older build than this action expects.");
                return false;
            }
            string schema = AskSchema();
            _show.Invoke(null, new object[] { Spec, schema, SettingsPath, new Host(this), new Action(Pump) });
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
