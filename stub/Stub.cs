using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Globalization;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Newtonsoft.Json.Linq;

// UIFramework for streamer.bot: the settings action of a host.
//
// This is the whole action. It compiles with no reference beyond what Streamer.bot gives every action, loads
// UIFramework.dll at run time and hands it three things: the spec (how this window is called and laid out),
// the schema (what the game knows, asked from the game action) and a Host object the DLL talks back to.
//
// Between the HOST markers sits what differs per project; tools/embed-spec.ps1 splices a host's section from
// hosts/<host>/host-section.cs and its spec from hosts/<host>/spec.json into this template. Everything outside
// the markers is the same in every host; tools/check-stub.ps1 checks that.

public class CPHInline
{
    // ==== HOST ====
    private const string GameActionName = "Demo Game";
    private const string LogPrefix = "[Demo/Settings]";
    private static readonly string SettingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Demo", "settings.json");

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

        public void OnSaved(string settingsJson) { }
        public string Test(string name, string settingsJson) => null;
        public string ReadTable(string key) => null;
        public IEnumerable<string> ReadLogLines() => null;
    }
    // ==== END HOST ====

    // The spec: see docs/Spec.md. Written in by tools/embed-spec.ps1 from hosts/<host>/spec.json; edit the JSON.
    private const string Spec = @"__SPEC_JSON__";

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
