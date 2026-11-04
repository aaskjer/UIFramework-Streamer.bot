using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Globalization;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Newtonsoft.Json.Linq;

// UIFramework for streamer.bot: the settings action of a host. The same file for every project.
//
// This is the whole action. It compiles with no reference beyond what Streamer.bot gives every action, loads
// UIFramework.dll at run time and hands it the schema (the settings and the window, asked from the game action
// through uiframework.request = describe) and a Host object the DLL talks back to. Everything the window needs
// from the game (a Try button's answer, a check before saving, work after it, the log) goes back to the game
// action the same way, so this file never changes per project: UiHost.Handle in the game answers.
//
// To use it: paste this file as an action's Execute Code, put the name of the game action into GameActionName,
// and trigger the action however you like. No references to add: the dialogs are Win32 message boxes.

public class CPHInline
{
    // ==== The one line to fill in: the action that holds the game and answers the settings window. ====
    private const string GameActionName = "Demo Game";

    // Known once the game has described itself: the log prefix the window asked for, the settings file, the
    // window declaration (the generic host reads its log folder and cooldowns from it).
    private static string LogPrefix = "[" + GameActionName + "/Settings]";
    private static string SettingsPath;
    private static JObject Window = new JObject();

    private void ReadSchema(string schemaJson)
    {
        var schema = JObject.Parse(schemaJson);
        Window = schema["window"] as JObject ?? new JObject();
        string file = (string)schema["settingsFile"];
        if (string.IsNullOrWhiteSpace(file))
            throw new InvalidOperationException("The action '" + GameActionName + "' names no settings file (UiHost.Handle's settingsFile).");
        SettingsPath = Path.IsPathRooted(file) ? file : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, file);
        LogPrefix = (string)Window["logPrefix"] ?? "[" + ((string)Window["title"] ?? GameActionName) + "/Settings]";
    }

    // ================================================================================================
    // HOST: what the DLL calls back. Every answer comes from the game action, asked on Streamer.bot's thread.
    // ================================================================================================

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

        // The cooldowns the window declares are set here; the game then hears of the save.
        public void OnSaved(string settingsJson)
        {
            var settings = JObject.Parse(settingsJson);
            _owner.RunOnBot(() =>
            {
                foreach (var cooldown in Window["cooldowns"] as JArray ?? new JArray())
                {
                    string command = (string)cooldown["command"], global = (string)cooldown["global"], user = (string)cooldown["user"];
                    if (string.IsNullOrEmpty(command)) continue;
                    if (!string.IsNullOrEmpty(global)) _owner.CPH.CommandSetGlobalCooldownDuration(command, Math.Max(0, (int?)settings[global] ?? 0));
                    if (!string.IsNullOrEmpty(user)) _owner.CPH.CommandSetUserCooldownDuration(command, Math.Max(0, (int?)settings[user] ?? 0));
                }
                _owner.AskGameNow("saved", null, settingsJson);
            }, false);
        }

        public string Test(string name, string settingsJson) => _owner.AskGame("test", name, settingsJson);
        public string Validate(string settingsJson) => _owner.AskGame("validate", null, settingsJson);
        public string ReadTable(string key) => _owner.AskGame("table", key, null);
        public string EmoteSources() => _owner.AskGame("emotes", null, null);

        // The log as the window declares it: every file matching the pattern in the folder, oldest file first.
        public IEnumerable<string> ReadLogLines()
        {
            var log = Window["log"] as JObject;
            string folder = (string)log?["folder"], files = (string)log?["files"];
            if (string.IsNullOrWhiteSpace(folder) || string.IsNullOrWhiteSpace(files))
                return null;
            if (!Path.IsPathRooted(folder)) folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, folder);
            if (!Directory.Exists(folder))
                return new string[0];
            return Directory.GetFiles(folder, files).OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .SelectMany(ReadLinesSafe).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
        }

        private static IEnumerable<string> ReadLinesSafe(string file)
        {
            try { return File.ReadAllLines(file); } catch (Exception) { return new string[0]; }
        }
    }

    // ================================================================================================
    // JOBS: the window runs on its own thread; anything that needs CPH is queued here and run by Pump,
    // which the DLL calls every 100 ms on Streamer.bot's thread while the window is open, and once after.
    // ================================================================================================

    private readonly ConcurrentQueue<Action> _jobs = new ConcurrentQueue<Action>();
    private int _botThread;

    private void Enqueue(Action job) => _jobs.Enqueue(job);

    private void Pump()
    {
        while (_jobs.TryDequeue(out var job))
        {
            try { job(); }
            catch (Exception ex) { CPH.LogError(LogPrefix + " A queued step failed: " + ex.Message); }
        }
    }

    /// <summary>Runs a job on Streamer.bot's thread: at once when this is that thread, else through the queue.
    /// With <paramref name="wait"/> the call blocks until the job ran (five seconds at most) and rethrows what it threw.</summary>
    private void RunOnBot(Action job, bool wait)
    {
        if (Thread.CurrentThread.ManagedThreadId == _botThread) { job(); return; }
        if (!wait) { Enqueue(job); return; }
        using (var done = new ManualResetEventSlim(false))
        {
            Exception failure = null;
            Enqueue(() => { try { job(); } catch (Exception ex) { failure = ex; } finally { done.Set(); } });
            if (!done.Wait(TimeSpan.FromSeconds(5)))
                throw new TimeoutException("the action '" + GameActionName + "' did not answer within five seconds.");
            if (failure != null) throw failure;
        }
    }

    // The game answers the window's requests through non-persistent globals. Streamer.bot keeps its globals in
    // LiteDB, which refuses some characters in a name (brackets, a trailing space): the variable takes the
    // action's name with those replaced. The game's UiSchema.Var and UiHost.AnswerVar build the same names.
    private static string Clean(string actionName) => System.Text.RegularExpressions.Regex.Replace((actionName ?? "").Trim(), @"[^\w .-]", "_");
    public static string SchemaVar(string actionName) => "uiframework.schema." + Clean(actionName);
    public static string AnswerVar(string actionName) => "uiframework.answer." + Clean(actionName);

    /// <summary>Sends a request to the game action on Streamer.bot's thread and returns its answer, null for none.</summary>
    private string AskGameNow(string request, string name, string values)
    {
        string var = request == "describe" ? SchemaVar(GameActionName) : AnswerVar(GameActionName);
        CPH.UnsetGlobalVar(var, false);
        CPH.SetArgument("uiframework.request", request);
        CPH.SetArgument("uiframework.name", name ?? "");
        CPH.SetArgument("uiframework.values", values ?? "");
        CPH.SetArgument("uiframework.var", var);   // where the answer goes; the game uses it over its own name
        if (!CPH.RunAction(GameActionName, true))
            throw new InvalidOperationException("The action '" + GameActionName + "' did not run. Is it enabled, and is GameActionName in the settings action its exact name?");
        string answer = CPH.GetGlobalVar<string>(var, false);
        return string.IsNullOrEmpty(answer) ? null : answer;
    }

    /// <summary>The same from any thread (the window's): waits for Streamer.bot's thread to run it; a failure is
    /// logged and answered with null, so a Try button says "no answer" instead of the window dying.</summary>
    private string AskGame(string request, string name, string values)
    {
        string answer = null;
        try { RunOnBot(() => answer = AskGameNow(request, name, values), true); }
        catch (Exception ex) { CPH.LogError(LogPrefix + " The game did not answer '" + request + "': " + ex.Message); }
        return answer;
    }

    private string AskSchema()
    {
        string schema = AskGameNow("describe", null, null);
        if (schema == null)
            throw new InvalidOperationException("The action '" + GameActionName + "' ran but did not answer with its settings.\n\n" +
                "Look in Streamer.bot's log just above this line: a compile error in '" + GameActionName + "' or an exception from it shows there.\n" +
                "Then check the action's code: Execute() must call UiHost.Handle(args, CPH, ...) before anything else, and the host block " +
                "at its end must be the current one (UIFramework's stub/HostSchema.cs; tools/update-host-block.ps1 refreshes it).");
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
        _botThread = Thread.CurrentThread.ManagedThreadId;
        // The game first: without its answer there is nothing to show, and the settings file is named in it.
        string schema;
        try
        {
            schema = AskSchema();
            ReadSchema(schema);
        }
        catch (Exception ex)
        {
            CPH.LogError(LogPrefix + " The settings window cannot open: " + ex.Message);
            Ask("The settings window cannot open.\n\n" + ex.Message, MbOk, MbIconWarning);
            return false;
        }
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
            _show.Invoke(null, new object[] { null, schema, SettingsPath, new Host(this), new Action(Pump) });
            Pump();
            return true;
        }
        catch (Exception ex)
        {
            string why = (ex as TargetInvocationException)?.InnerException?.Message ?? ex.Message;
            CPH.LogError(LogPrefix + " The settings window failed: " + why);
            Ask("The settings window failed.\n\n" + why, MbOk, MbIconWarning);
            return false;
        }
    }

    // Streamer.bot recompiles or shuts down: close the window without asking.
    public void Dispose()
    {
        try { if (SettingsPath != null) _close?.Invoke(null, new object[] { SettingsPath }); } catch (Exception) { }
    }
}
