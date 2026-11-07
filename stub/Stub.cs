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
                // Each command on its own: one that fails (a command deleted since, a value out of range) leaves the
                // others set, and the game hears of the save whatever happened here.
                try
                {
                    foreach (var cooldown in Window["cooldowns"] as JArray ?? new JArray())
                    {
                        try
                        {
                            string command = _owner.CommandId((string)cooldown["command"]), global = (string)cooldown["global"], user = (string)cooldown["user"];
                            if (string.IsNullOrEmpty(command)) continue;
                            int? seconds;
                            if (!string.IsNullOrEmpty(global))
                            {
                                if ((seconds = Seconds(settings, global, _owner)) != null) _owner.CPH.CommandSetGlobalCooldownDuration(command, seconds.Value);
                                else _owner.CPH.LogWarn(LogPrefix + " The window names '" + global + "' for a cooldown, but the settings have no such number; that cooldown was left as it is.");
                            }
                            if (!string.IsNullOrEmpty(user))
                            {
                                if ((seconds = Seconds(settings, user, _owner)) != null) _owner.CPH.CommandSetUserCooldownDuration(command, seconds.Value);
                                else _owner.CPH.LogWarn(LogPrefix + " The window names '" + user + "' for a cooldown, but the settings have no such number; that cooldown was left as it is.");
                            }
                        }
                        catch (Exception ex) { _owner.CPH.LogError(LogPrefix + " A cooldown could not be set: " + ex.Message); }
                    }
                }
                finally { _owner.AskGameNow("saved", null, settingsJson); }
            }, false);
        }

        // A cooldown in whole seconds: a number of the settings, at least 0 and at most a day (a longer one is set to a
        // day, and the log says so); null when the key is missing or not a number, so a misnamed key never zeroes a cooldown.
        private static int? Seconds(JObject settings, string key, CPHInline owner)
        {
            var token = settings[key];
            if (token == null || (token.Type != JTokenType.Integer && token.Type != JTokenType.Float)) return null;
            double value = (double)token;
            if (value > 86400)
                owner.CPH.LogWarn(LogPrefix + " The cooldown '" + key + "' is " + Math.Round(value) + " seconds; it is set to a day (86400), the longest the settings action sets.");
            return (int)Math.Max(0, Math.Min(86400, Math.Round(value)));
        }

        public string Test(string name, string settingsJson) => _owner.AskGame("test", name, settingsJson);
        // No answer is not "no problems": the window gets a marker and asks before saving unchecked.
        public string Validate(string settingsJson) => _owner.AskGame("validate", null, settingsJson, KnowsNoAnswer() ? NoAnswer : null);
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
    /// With <paramref name="wait"/> the call blocks until the job ran and rethrows what it threw: five seconds for it to
    /// start, thirty more once it has started. A job that runs longer keeps running, but the window stops waiting for it
    /// and goes on as with no answer, so a game handler that hangs never freezes the window.</summary>
    private void RunOnBot(Action job, bool wait)
    {
        if (Thread.CurrentThread.ManagedThreadId == _botThread) { job(); return; }
        if (!wait) { Enqueue(job); return; }
        var done = new ManualResetEventSlim(false);
        Exception failure = null;
        int state = 0;   // 0 waiting, 1 running, 2 given up: a request the window stopped waiting for is not run late
        Enqueue(() =>
        {
            if (Interlocked.CompareExchange(ref state, 1, 0) != 0) return;
            try { job(); } catch (Exception ex) { failure = ex; } finally { done.Set(); }
        });
        if (!done.Wait(TimeSpan.FromSeconds(5)))
        {
            if (Interlocked.CompareExchange(ref state, 2, 0) == 0)
                throw new TimeoutException("the action '" + GameActionName + "' did not answer within five seconds.");
            // It has started: its answer is on the way, unless the game's handler hangs.
            if (!done.Wait(TimeSpan.FromSeconds(30)))
                throw new TimeoutException("the action '" + GameActionName + "' started but did not finish within 30 seconds; its answer is not used.");
        }
        if (failure != null) throw failure;
    }

    // The game answers the window's requests through non-persistent globals. Streamer.bot keeps its globals in
    // LiteDB, which refuses some characters in a name (brackets, a trailing space): the variable takes the
    // action's name with those replaced. The game's UiSchema.Var and UiHost.AnswerVar build the same names.
    // The same cleaning as UiSchema.Clean in the host block, without Regex: letters, digits, space, dot and dash stay.
    private static string Clean(string actionName)
    {
        var chars = (actionName ?? "").Trim().ToCharArray();
        for (int i = 0; i < chars.Length; i++)
            if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '_' && chars[i] != ' ' && chars[i] != '.' && chars[i] != '-') chars[i] = '_';
        return new string(chars);
    }
    public static string SchemaVar(string actionName) => "uiframework.schema." + Clean(actionName);
    public static string AnswerVar(string actionName) => "uiframework.answer." + Clean(actionName);

    /// <summary>The id of a command the window names: as its id, as its text ("!roll") or as its name in Streamer.bot
    /// (the first enabled or not, case ignored). Null, with a warning, when there is no such command.</summary>
    private string CommandId(string command)
    {
        if (string.IsNullOrWhiteSpace(command)) return null;
        Guid id;
        if (Guid.TryParse(command, out id)) return command;
        foreach (var known in CPH.GetCommands() ?? new List<Streamer.bot.Plugin.Interface.Model.CommandData>())
        {
            if (string.Equals(known.Name, command, StringComparison.OrdinalIgnoreCase)) return known.Id.ToString();
            if (known.Commands != null && known.Commands.Any(text => string.Equals(text, command, StringComparison.OrdinalIgnoreCase))) return known.Id.ToString();
        }
        CPH.LogWarn(LogPrefix + " There is no command '" + command + "' in this Streamer.bot; its cooldowns were not set.");
        return null;
    }

    // One request to the game at a time: its arguments and its answer variable are the game's, and two runs whose
    // pumps overlap (a window closed and opened again quickly) must not mix them. A request that has hung inside the
    // game action for 35 seconds holds nobody up: its arguments were read long ago, so the requests after it go on
    // behind a new gate (a run taking over a window whose game never answered, a window opened again meanwhile).
    private sealed class Gate { public long Since; }
    private static Gate _askGate = new Gate();

    /// <summary>Sends a request to the game action on Streamer.bot's thread and returns its answer, null for none.</summary>
    private string AskGameNow(string request, string name, string values)
    {
        var deadline = DateTime.UtcNow.AddSeconds(35);
        while (true)
        {
            var gate = Volatile.Read(ref _askGate);
            if (Monitor.TryEnter(gate, TimeSpan.FromSeconds(1)))
            {
                try
                {
                    if (!ReferenceEquals(gate, Volatile.Read(ref _askGate))) continue;   // replaced meanwhile: the new one
                    Interlocked.Exchange(ref gate.Since, DateTime.UtcNow.Ticks);
                    return AskGameLocked(request, name, values);
                }
                finally
                {
                    Interlocked.Exchange(ref gate.Since, 0);
                    Monitor.Exit(gate);
                }
            }
            long since = Interlocked.Read(ref gate.Since);
            if (since != 0 && DateTime.UtcNow.Ticks - since > TimeSpan.FromSeconds(35).Ticks)
                Interlocked.CompareExchange(ref _askGate, new Gate(), gate);
            else if (DateTime.UtcNow >= deadline)
                throw new TimeoutException("another request to the action '" + GameActionName + "' has not finished.");
        }
    }

    private string AskGameLocked(string request, string name, string values)
    {
        string var = request == "describe" ? SchemaVar(GameActionName) : AnswerVar(GameActionName);
        CPH.UnsetGlobalVar(var, false);
        CPH.SetArgument("uiframework.request", request);
        CPH.SetArgument("uiframework.name", name ?? "");
        CPH.SetArgument("uiframework.values", values ?? "");
        CPH.SetArgument("uiframework.var", var);   // where the answer goes; the game uses it over its own name
        if (!CPH.ActionExists(GameActionName))
            throw new InvalidOperationException("There is no action named '" + GameActionName + "' in this Streamer.bot. GameActionName in the settings action must be the game action's exact name.");
        if (!CPH.RunAction(GameActionName, true))
            throw new InvalidOperationException("The action '" + GameActionName + "' did not run. Is it enabled?");
        string answer = CPH.GetGlobalVar<string>(var, false);
        // Gone once read: an answer can carry a secret (the emote sources hold the Twitch token), and a global stays
        // readable by every action and visible in Streamer.bot's variables until it is unset.
        CPH.UnsetGlobalVar(var, false);
        return string.IsNullOrEmpty(answer) ? null : answer;
    }

    /// <summary>What the window gets when the game did not answer at all (a timeout, a disabled or missing action),
    /// told apart from an answer that is empty. The window's save asks before saving what nobody checked.</summary>
    private const string NoAnswer = "\u0001uiframework:no-answer";

    // A DLL before 0.9.13 shows the marker as a warning line; it gets null for "no answer", as it always did.
    private static bool KnowsNoAnswer()
    {
        var version = ParseVersion(_show?.DeclaringType?.Assembly is Assembly a ? VersionOf(a) : null);
        return version == null || version >= new Version(0, 9, 13);
    }

    /// <summary>The same from any thread (the window's): waits for Streamer.bot's thread to run it; a failure is
    /// logged and answered with <paramref name="failed"/> (null by default), so a Try button says "no answer"
    /// instead of the window dying.</summary>
    private string AskGame(string request, string name, string values, string failed = null)
    {
        string answer = null;
        try { RunOnBot(() => answer = AskGameNow(request, name, values), true); }
        catch (Exception ex)
        {
            CPH.LogError(LogPrefix + " The game did not answer '" + request + "': " + ex.Message);
            return failed == null ? null : failed + ": " + ex.Message;
        }
        return answer;
    }

    private string AskSchema()
    {
        string schema = AskGameNow("describe", null, null);
        if (schema == null)
            throw new InvalidOperationException("The action '" + GameActionName + "' ran but did not answer with its settings.\n\n" +
                "Look in Streamer.bot's log just above this line. A game action that ran shows there as 'InlineCode :: Running default entry point' (log level debug); " +
                "nothing of the kind means the action has no enabled Execute Code sub-action with the game in it. A compile error or an exception from the game shows there too.\n" +
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
    // This stub's own version: a release whose latest.json names a higher minHostStub needs a newer settings action
    // and is not offered to this one.
    private const string StubVersion = "0.2.0";
    // The API answers with the file as it is on main right now; the raw CDN keeps a copy for up to five minutes and
    // ignores a cache-busting query, so it is the fallback only (rate limit on the API, or the API unreachable).
    private const string LatestApiUrl = "https://api.github.com/repos/aaskjer/UIFramework-Streamer.bot/contents/lib/v1/latest.json?ref=main";
    private const string LatestUrl = "https://raw.githubusercontent.com/aaskjer/UIFramework-Streamer.bot/main/lib/v1/latest.json";
    private const string FrameworkReleasesUrl = "https://github.com/aaskjer/UIFramework-Streamer.bot/releases";
    private static readonly string LibRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UIFramework", "lib");

    private static string FindFramework() => FrameworkCandidates().FirstOrDefault();

    /// <summary>Every installed UIFramework.dll, newest first: when the newest cannot be loaded (a damaged file, a
    /// build this stub does not fit), the next older one opens the window instead.</summary>
    private static List<string> FrameworkCandidates()
    {
        if (!Directory.Exists(LibRoot))
            return new List<string>();
        return Directory.GetDirectories(LibRoot)
            .Select(dir => new { Dir = dir, Version = ParseVersion(Path.GetFileName(dir)) })
            .Where(x => x.Version != null && File.Exists(Path.Combine(x.Dir, "UIFramework.dll")))
            .OrderByDescending(x => x.Version)
            .Select(x => Path.Combine(x.Dir, "UIFramework.dll"))
            .ToList();
    }

    private static Version ParseVersion(string text)
    {
        text = (text ?? "").TrimStart('v', 'V');
        return Version.TryParse(text.Contains(".") ? text : text + ".0", out var v) ? v : null;
    }

    /// <summary>The settings action a release needs (latest.json's minHostStub) when that is newer than this one,
    /// else null. Missing parts count as 0, so "0.2.0.0" asks no more than "0.2.0".</summary>
    private static Version NeedsNewerStub(JObject latest)
    {
        var needs = Full(ParseVersion((string)latest["minHostStub"]));
        return needs != null && needs > Full(ParseVersion(StubVersion)) ? needs : null;
    }

    private static Version Full(Version v) => v == null ? null : new Version(v.Major, v.Minor, Math.Max(0, v.Build), Math.Max(0, v.Revision));

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
                    "\nand put it into the folder of its version under\n" + LibRoot + "\nfor example " + ExamplePath() + ", then open the settings again.",
                    MbOk, MbIconWarning);
                OpenFolder(LibRoot);
                return null;
            }
        }
        if (answer == IdNo)
        {
            Ask("Put UIFramework.dll into the folder of its version under\n" + LibRoot + "\nfor example " + ExamplePath() + "\n\n" +
                "The folder opens now. Then open the settings again.\nDownloads: " + FrameworkReleasesUrl,
                MbOk, MbIconInformation);
            OpenFolder(LibRoot);
        }
        return null;
    }

    /// <summary>Where a DLL fetched by hand belongs: the folder of the version latest.json names, or a placeholder.
    /// Joined by hand: Path.Combine on .NET Framework throws on the placeholder's angle brackets.</summary>
    private static string ExamplePath()
    {
        string version;
        try { version = ReleaseVersion(FetchLatest()); }
        catch (Exception) { version = "<version>"; }
        return LibRoot + "\\" + version + "\\UIFramework.dll";
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

    // WebClient waits 100 s by default (and 300 s for each read of the body); a release check at window open gets five.
    private sealed class QuickWebClient : System.Net.WebClient
    {
        public int TimeoutMs = 5000;
        protected override System.Net.WebRequest GetWebRequest(Uri address)
        {
            var request = base.GetWebRequest(address);
            if (request != null) request.Timeout = TimeoutMs;
            if (request is System.Net.HttpWebRequest http) http.ReadWriteTimeout = TimeoutMs;
            return request;
        }
    }

    // The system default (0) lets Windows pick TLS 1.2 or 1.3; OR-ing Tls12 into it would pin the whole process to 1.2.
    private static void EnsureTls12()
    {
        var current = System.Net.ServicePointManager.SecurityProtocol;
        if ((int)current != 0 && (current & System.Net.SecurityProtocolType.Tls12) == 0)
            System.Net.ServicePointManager.SecurityProtocol = current | System.Net.SecurityProtocolType.Tls12;
    }

    /// <summary>What lib/v1/latest.json says: version, download url, sha256. The raw CDN is asked when the API refused
    /// (its rate limit) or answered with something that is no latest.json (a network's sign-in page), not when the
    /// network is unreachable or timed out: that would only keep the window waiting another five seconds.</summary>
    private static JObject FetchLatest()
    {
        EnsureTls12();
        using (var client = new QuickWebClient())
        {
            client.Headers.Add("User-Agent", "UIFramework-Loader/1.0");
            client.Headers.Add("Accept", "application/vnd.github.raw");
            try { return ParseLatest(client.DownloadString(LatestApiUrl)); }
            catch (System.Net.WebException ex) when (Unreachable(ex)) { throw; }
            catch (Exception)
            {
                client.Headers.Remove("Accept");
                return ParseLatest(client.DownloadString(LatestUrl + "?t=" + DateTime.UtcNow.Ticks));
            }
        }
    }

    private static JObject ParseLatest(string json)
    {
        JObject latest;
        try { latest = JObject.Parse(json); }
        catch (Newtonsoft.Json.JsonException) { throw new InvalidOperationException("the answer was no latest.json (a sign-in page of the network?)."); }
        if (string.IsNullOrWhiteSpace((string)latest["version"]) || string.IsNullOrWhiteSpace((string)latest["url"]))
            throw new InvalidOperationException("latest.json names no version or url; no release has been published yet.");
        return latest;
    }

    // No way out at all: the CDN would not answer either.
    private static bool Unreachable(System.Net.WebException ex) =>
        ex.Status == System.Net.WebExceptionStatus.ConnectFailure || ex.Status == System.Net.WebExceptionStatus.NameResolutionFailure ||
        ex.Status == System.Net.WebExceptionStatus.ProxyNameResolutionFailure || ex.Status == System.Net.WebExceptionStatus.Timeout;

    /// <summary>The version latest.json names, as the folder it is installed into: two to four numbers with dots (a
    /// leading v dropped). Anything else ("..\\..", "1.0-beta") is refused before it becomes part of a path.</summary>
    private static string ReleaseVersion(JObject latest)
    {
        string named = (string)latest["version"] ?? "", version = named.TrimStart('v', 'V');
        var parts = version.Split('.');
        if (parts.Length < 2 || parts.Length > 4 || parts.Any(p => p.Length == 0 || p.Any(c => c < '0' || c > '9')) || !Version.TryParse(version, out _))
            throw new InvalidOperationException("latest.json names version '" + named + "', which is no version number like 1.2.0; nothing is installed from it.");
        return version;
    }

    /// <summary>Downloads the release latest.json names into lib\<version>\, checks its hash, returns the path.
    /// With <paramref name="latest"/> null it is fetched first. One download at a time across every host.</summary>
    private static string Download(JObject latest)
    {
        using (var mutex = new Mutex(false, @"Global\UIFramework.Loader"))
        {
            bool owned;
            try { owned = mutex.WaitOne(TimeSpan.FromSeconds(60)); }
            catch (AbandonedMutexException) { owned = true; }   // a Streamer.bot that died mid-download left it
            if (!owned)
                throw new TimeoutException("another download of UIFramework.dll is still running; try again in a minute.");
            try
            {
                latest = latest ?? FetchLatest();
                // Nothing unchecked: the version becomes a folder name only when it is a version number, the DLL comes
                // over HTTPS only, and a release without a hash is not installed at all.
                string version = ReleaseVersion(latest);
                // Also on the first install: a release that needs a newer settings action than this game's is not
                // installed, or it would be the only DLL there and every later release check refuse to replace it.
                if (NeedsNewerStub(latest) != null)
                    throw new InvalidOperationException("UIFramework " + version + " needs a newer settings action (stub " + (string)latest["minHostStub"] +
                        ", this game's is " + StubVersion + "); update the game first, or put an older UIFramework.dll into its version's folder.");
                string url = (string)latest["url"];
                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
                    throw new InvalidOperationException("latest.json names a download that is not HTTPS (" + url + "); it is not fetched.");
                string expected = ((string)latest["sha256"] ?? "").Trim().ToLowerInvariant();
                if (expected.Length == 0)
                    throw new InvalidOperationException("latest.json names no sha256 for " + version + "; an unverified UIFramework.dll is not installed.");
                string target = Path.Combine(LibRoot, version, "UIFramework.dll");
                if (File.Exists(target))
                {
                    if (HashOf(target) == expected)
                        return target;
                    // There, but not the published file (damaged, copied in wrong): set aside and fetched again.
                    SetAside(target);
                }
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                string temp = target + ".download";
                EnsureTls12();
                using (var client = new QuickWebClient { TimeoutMs = 60000 })
                {
                    client.Headers.Add("User-Agent", "UIFramework-Loader/1.0");
                    client.DownloadFile(uri, temp);
                }
                string actual = HashOf(temp);
                if (actual != expected)
                {
                    File.Delete(temp);
                    throw new InvalidOperationException("the downloaded file does not match the published hash (" + actual + " vs " + expected + ").");
                }
                // The hash first: a game loading meanwhile never finds the DLL without it, or with half of it.
                File.WriteAllText(target + ".sha256", expected);
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

    private static JObject ReadLayout() => TryReadLayout() ?? new JObject();

    /// <summary>The layout file, an empty one when there is none, null when it cannot be read (locked, damaged):
    /// then nothing is written over it.</summary>
    private static JObject TryReadLayout()
    {
        try { return File.Exists(LayoutPath) ? JObject.Parse(File.ReadAllText(LayoutPath)) : new JObject(); }
        catch (Exception) { return null; }
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
            // A file that cannot be read is the user's customisation in a bad moment: it is not written over. A note
            // that changes nothing is not written either, and a write goes beside the file and is swapped in.
            var layout = TryReadLayout();
            if (layout == null) return;
            var before = layout.ToString(Newtonsoft.Json.Formatting.None);
            change(layout);
            if (layout.ToString(Newtonsoft.Json.Formatting.None) == before) return;
            Directory.CreateDirectory(Path.GetDirectoryName(LayoutPath));
            string temp = LayoutPath + ".tmp";
            File.WriteAllText(temp, layout.ToString(Newtonsoft.Json.Formatting.Indented));
            if (File.Exists(LayoutPath)) File.Replace(temp, LayoutPath, null);
            else File.Move(temp, LayoutPath);
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
        Version newest;
        try { newest = ParseVersion(ReleaseVersion(latest)); }
        catch (Exception ex)
        {
            CPH.LogWarn(LogPrefix + " UIFramework " + have + " in use; " + ex.Message);
            return installed;
        }
        if (newest != null && NeedsNewerStub(latest) != null)
        {
            CPH.LogWarn(LogPrefix + " UIFramework " + newest + " needs a newer settings action (stub " + (string)latest["minHostStub"] + ", this one is " + StubVersion + "); update the game to get it. " + have + " stays in use.");
            return installed;
        }
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
        lock (BridgeGate) loaded = _show != null || LoadedFramework() != null;
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

    private static string HashOf(string path)
    {
        using (var sha = System.Security.Cryptography.SHA256.Create())
        using (var stream = File.OpenRead(path))
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
    }

    /// <summary>Moves a DLL that does not match its hash out of the way (UIFramework.dll.broken), so the next open
    /// finds its version missing and offers it again instead of failing on it every time.</summary>
    private static void SetAside(string dllPath)
    {
        try
        {
            if (File.Exists(dllPath + ".broken")) File.Delete(dllPath + ".broken");
            File.Move(dllPath, dllPath + ".broken");
            if (File.Exists(dllPath + ".sha256")) File.Delete(dllPath + ".sha256");
        }
        catch (Exception) { }
    }

    /// <summary>The UIFramework another game in this Streamer.bot loaded already, or null. Every game uses that one
    /// until Streamer.bot restarts, so two versions never run side by side.</summary>
    private static Assembly LoadedFramework() =>
        AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a =>
        {
            try { return a.GetName().Name == "UIFramework" && a.GetType("UIFramework.Entry") != null; }
            catch (Exception) { return false; }
        });

    /// <summary>The version a loaded UIFramework came from: the name of its folder under lib (lib\0.9.13\...).</summary>
    private static string VersionOf(Assembly assembly)
    {
        try { return Path.GetFileName(Path.GetDirectoryName(assembly.Location)); }
        catch (Exception) { return "?"; }
    }

    private static bool Bind(string dllPath)
    {
        lock (BridgeGate)
        {
            if (_show != null) return true;
            var signature = new[] { typeof(string), typeof(string), typeof(string), typeof(object), typeof(Action) };
            // Another game's UIFramework is already running: this one uses it too, and a newer DLL waits for the next start.
            var loaded = LoadedFramework();
            var shared = loaded?.GetType("UIFramework.Entry")?.GetMethod("Show", signature);
            if (shared != null)
            {
                _show = shared;
                _close = loaded.GetType("UIFramework.Entry").GetMethod("Close", new[] { typeof(string) });
                return true;
            }
            // The hash the download checked, kept beside the file: a damaged DLL is not loaded but set aside, and its
            // version is offered again. (A file put in by hand has no hash file and is loaded as it is.)
            string hashFile = dllPath + ".sha256";
            if (File.Exists(hashFile) && HashOf(dllPath) != File.ReadAllText(hashFile).Trim().ToLowerInvariant())
            {
                SetAside(dllPath);
                throw new InvalidOperationException(dllPath + " does not match the hash it was downloaded with; it was set aside as .broken and is offered again next time.");
            }
            // UnsafeLoadFrom: a DLL saved from a browser carries Windows' "downloaded from the internet" mark, which
            // LoadFrom refuses in Streamer.bot (NotSupportedException); this loads it like any other.
            var assembly = Assembly.UnsafeLoadFrom(dllPath);
            var entry = assembly.GetType("UIFramework.Entry");
            _show = entry?.GetMethod("Show", signature);
            _close = entry?.GetMethod("Close", new[] { typeof(string) });
            return _show != null;
        }
    }

    // ================================================================================================
    // ONE WINDOW: Streamer.bot runs this action on every trigger, also while its window is open.
    // ================================================================================================

    // Held by the run that opens the window, until that run is over. Another run meanwhile asks neither the game nor
    // GitHub again and leaves _botThread and the job queue to the run whose pump serves the window.
    private static int _opening;
    // The schema of the window that run shows; null while it is still asking the game or a dialog is up.
    private static string _shownSchema;
    // Which run holds _opening (only that one hands it back), whether it is still waiting for the game's schema, and
    // since when (ticks).
    private static int _openingRun, _askingGame;
    private static long _askingSince;
    // Runs inside the DLL's Show (a window open and pumped by them), and whether the run that holds _opening ended
    // while another still served a window: then that one hands the flags back when it is done.
    private static int _serving, _ownerGone;

    public bool Execute()
    {
        if (Interlocked.CompareExchange(ref _opening, 1, 0) != 0 && !TakeOver())
            return BringToFront();
        int run = Interlocked.Increment(ref _openingRun);
        try { return Open(); }
        finally
        {
            // A run whose window was taken over by a later one leaves the flags to that one; while another run still
            // serves a window it opened again, that run hands them back when it is done.
            if (Volatile.Read(ref _openingRun) == run)
            {
                if (Volatile.Read(ref _serving) == 0) Release();
                else Volatile.Write(ref _ownerGone, 1);
            }
        }
    }

    private static void Release()
    {
        Volatile.Write(ref _ownerGone, 0);
        Volatile.Write(ref _shownSchema, null);
        Volatile.Write(ref _opening, 0);
    }

    /// <summary>The DLL's Show, counted in <see cref="_serving"/>: true when it showed a window (and pumped for it until
    /// it closed), false when one was open already and came to the front.</summary>
    private bool Serve(string schema)
    {
        Interlocked.Increment(ref _serving);
        try { return (bool)_show.Invoke(null, new object[] { null, schema, SettingsPath, new Host(this), new Action(Pump) }); }
        finally
        {
            if (Interlocked.Decrement(ref _serving) == 0 && Interlocked.CompareExchange(ref _ownerGone, 0, 1) == 1)
            {
                Pump();
                Release();
            }
        }
    }

    /// <summary>Whether this run opens the window although another holds it: only when that one has waited a minute
    /// for the game's schema (the game action hangs), so one hung answer does not lock the settings until Streamer.bot
    /// restarts. Not while a dialog is up: a user may take their time over that.</summary>
    private bool TakeOver()
    {
        long since = Interlocked.Read(ref _askingSince);
        if (Volatile.Read(ref _askingGame) == 0 || DateTime.UtcNow.Ticks - since < TimeSpan.FromSeconds(60).Ticks)
            return false;
        // One later run takes over, not every one that comes at once.
        if (Interlocked.CompareExchange(ref _askingSince, DateTime.UtcNow.Ticks, since) != since)
            return false;
        CPH.LogWarn(LogPrefix + " The action '" + GameActionName + "' has not answered for a minute; this run opens the settings window instead.");
        return true;
    }

    /// <summary>A run while another one has the window: the DLL brings that window to the front and returns at once.
    /// When the window has closed but the other run's pump is still stuck in a game action, the DLL opens it again and
    /// this run's pump serves it, so a window whose request hangs can still be closed and opened again.</summary>
    private bool BringToFront()
    {
        string schema = Volatile.Read(ref _shownSchema);
        if (schema == null)
        {
            CPH.LogInfo(LogPrefix + " The settings window is still opening (the game's answer or a dialog is pending); this run leaves it to that one.");
            return true;
        }
        try
        {
            if (Serve(schema)) Pump();
            return true;
        }
        catch (Exception ex)
        {
            CPH.LogError(LogPrefix + " The settings window could not be brought to the front: " + ((ex as TargetInvocationException)?.InnerException?.Message ?? ex.Message));
            return false;
        }
    }

    private bool Open()
    {
        _botThread = Thread.CurrentThread.ManagedThreadId;
        // The game first: without its answer there is nothing to show, and the settings file is named in it.
        string schema;
        Volatile.Write(ref _ownerGone, 0);
        Interlocked.Exchange(ref _askingSince, DateTime.UtcNow.Ticks);
        Volatile.Write(ref _askingGame, 1);
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
        finally { Volatile.Write(ref _askingGame, 0); }
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
            // The chosen DLL first, then every other installed one, newest first: one that cannot be loaded leaves the
            // window to an older one instead of locking every game out.
            bool bound = false;
            foreach (var candidate in new[] { dll }.Concat(FrameworkCandidates().Where(c => !string.Equals(c, dll, StringComparison.OrdinalIgnoreCase))))
            {
                try
                {
                    if (Bind(candidate)) { bound = true; break; }
                    CPH.LogError(LogPrefix + " " + candidate + " has no UIFramework.Entry.Show; a newer or older build than this action expects.");
                }
                catch (Exception ex)
                {
                    CPH.LogError(LogPrefix + " " + candidate + " could not be loaded: " + ((ex as TargetInvocationException)?.InnerException?.Message ?? ex.Message));
                }
            }
            if (!bound)
            {
                Ask("No installed UIFramework.dll could be loaded; the log names each one and why.\n\nReleases: " + FrameworkReleasesUrl, MbOk, MbIconWarning);
                return false;
            }
            Volatile.Write(ref _shownSchema, schema);
            // False: a window for this settings file was open already and came to the front; the run that serves it
            // pumps for it, so this one does not drain the queue beside it.
            if (LoadedFramework() is Assembly running && !string.Equals(running.Location, dll, StringComparison.OrdinalIgnoreCase) && _show.DeclaringType?.Assembly == running)
                CPH.LogInfo(LogPrefix + " UIFramework " + VersionOf(running) + " is running already in this Streamer.bot; " + Path.GetFileName(Path.GetDirectoryName(dll)) + " is used from the next start of Streamer.bot.");
            if (Serve(schema))
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
