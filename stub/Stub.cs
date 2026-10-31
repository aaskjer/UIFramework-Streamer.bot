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
// The action needs PresentationFramework, PresentationCore and WindowsBase in its references for the first-run
// dialog (an export made from a WPF settings action has them). Between the HOST markers sits what differs per project; tools/embed-spec.ps1 splices a host's section from
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
    // LOADER: finds UIFramework.dll under <Streamer.bot>\UIFramework\lib\<version>\. The first time, it asks
    // whether to download the DLL from GitHub or to show the folder it belongs in; after that, Init checks for a
    // newer release in the background and fetches it into a new version folder for the next start.
    // ================================================================================================

    private const string FrameworkName = "UIFramework for streamer.bot";
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
            System.Windows.MessageBoxButton.YesNoCancel, System.Windows.MessageBoxImage.Question);
        if (answer == System.Windows.MessageBoxResult.Yes)
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
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                OpenFolder(LibRoot);
                return null;
            }
        }
        if (answer == System.Windows.MessageBoxResult.No)
        {
            Ask("Put UIFramework.dll into a version folder under\n" + LibRoot + "\nfor example " + Path.Combine(LibRoot, "1.0.0") + "\\UIFramework.dll\n\n" +
                "The folder opens now. Then open the settings again.\nDownloads: " + FrameworkReleasesUrl,
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
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

    // A message box on a thread of its own: the action runs on Streamer.bot's thread, which is no place for a dialog.
    private static System.Windows.MessageBoxResult Ask(string text, System.Windows.MessageBoxButton buttons, System.Windows.MessageBoxImage image)
    {
        var result = System.Windows.MessageBoxResult.None;
        var thread = new Thread(() => { result = System.Windows.MessageBox.Show(text, FrameworkName, buttons, image); });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        return result;
    }

    /// <summary>What lib/v1/latest.json says: version, download url, sha256.</summary>
    private static JObject FetchLatest()
    {
        System.Net.ServicePointManager.SecurityProtocol |= System.Net.SecurityProtocolType.Tls12;
        using (var client = new System.Net.WebClient())
        {
            client.Headers.Add("User-Agent", "UIFramework-Loader/1.0");
            string json = client.DownloadString(LatestUrl + "?t=" + DateTime.UtcNow.Ticks);
            var latest = JObject.Parse(json);
            if (string.IsNullOrWhiteSpace((string)latest["version"]) || string.IsNullOrWhiteSpace((string)latest["url"]))
                throw new InvalidOperationException("latest.json names no version or url; no release has been published yet.");
            return latest;
        }
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
                using (var client = new System.Net.WebClient())
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

    private static bool AutoUpdateEnabled()
    {
        try
        {
            string layout = Path.Combine(Path.GetDirectoryName(SettingsPath), "uiframework.layout.json");
            if (!File.Exists(layout)) return true;
            var token = JObject.Parse(File.ReadAllText(layout))["autoUpdate"];
            return token == null || token.Type != JTokenType.Boolean || (bool)token;
        }
        catch (Exception) { return true; }
    }

    // Streamer.bot calls this once the action is compiled: a newer release goes into its own folder for next time.
    public void Init()
    {
        if (FindFramework() == null || !AutoUpdateEnabled())
            return;
        var check = new Thread(() =>
        {
            try
            {
                var latest = FetchLatest();
                var newest = ParseVersion((string)latest["version"]);
                var have = ParseVersion(Path.GetFileName(Path.GetDirectoryName(FindFramework())));
                if (newest != null && have != null && newest > have)
                {
                    string path = Download(latest);
                    CPH.LogInfo(LogPrefix + " UIFramework " + newest + " downloaded to " + path + "; it is used from the next start of Streamer.bot.");
                }
            }
            catch (Exception ex)
            {
                CPH.LogWarn(LogPrefix + " UIFramework update check skipped: " + ex.Message);
            }
        });
        check.IsBackground = true;
        check.Start();
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
