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
    private const string Spec = @"__SPEC_JSON__";

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
