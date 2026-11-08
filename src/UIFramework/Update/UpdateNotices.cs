using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UIFramework
{
    /// <summary>
    /// One update notice in chat per Streamer.bot start, for streamers who do not open the settings every day.
    ///
    /// The host block calls <see cref="Due"/> on an ordinary run of a game action (never on a window request), at most
    /// every two minutes, and the window calls <see cref="Register"/> when it opens: each tells which game it is and
    /// which version runs. A few minutes after the first game of this Streamer.bot run, once GitHub's answers are fresh,
    /// one game gets one line for chat that names everything new: the games used so far whose release is newer than
    /// the version they run, UIFramework when its newest release is newer than the newest DLL installed, and a game
    /// whose settings action is older than that release needs (latest.json's minHostStub). Then nothing more until
    /// Streamer.bot starts again. The links go to Streamer.bot's log.
    ///
    /// Due never waits for the network: GitHub's answers sit in <c>&lt;Streamer.bot&gt;\UIFramework\updates.json</c>,
    /// shared by every game; an answer older than twelve hours starts a new check in the background. A game whose
    /// layout file says <c>"updateNotices": false</c> (the switch on its About page) is left out and does not post it.
    /// </summary>
    public static class UpdateNotices
    {
        public static readonly TimeSpan CheckEvery = TimeSpan.FromHours(12);
        public static readonly TimeSpan RetryAfterFailure = TimeSpan.FromHours(1);
        /// <summary>How long after the first game of a Streamer.bot run the notice waits, so the games used at the start
        /// of a stream are in it.</summary>
        public static TimeSpan Delay = TimeSpan.FromMinutes(3);
        public const string FrameworkLatestUrl = "https://raw.githubusercontent.com/aaskjer/UIFramework-Streamer.bot/main/lib/v1/latest.json";

        // ---- What tests change: the shared file's folder, how a URL is fetched, the clock, the run, background checks. ----
        public static string Folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UIFramework");
        public static Func<string, string> Fetch = FetchFromWeb;
        public static Func<DateTime> Now = () => DateTime.UtcNow;
        public static Func<string> Session = ProcessStart;
        public static bool InBackground = true;

        private static readonly object Gate = new object();
        private static readonly HashSet<string> Running = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public static string StatePath => Path.Combine(Folder, "updates.json");

        /// <summary>
        /// Null: nothing more this Streamer.bot run (said already, or this game does not want notices); stop asking.
        /// Empty: not yet; ask again later. Otherwise the line for chat, in this game's window language, followed by the
        /// lines for Streamer.bot's log (one per update, with its link).
        /// </summary>
        /// <param name="host">The game's host id ("hangman").</param>
        /// <param name="title">The game's name as chat should read it.</param>
        /// <param name="version">The version that runs ("v13.1.1").</param>
        /// <param name="releases">The game's GitHub repository ("aaskjer/Hangman-Game"); null: no notice for the game itself.</param>
        /// <param name="settingsFile">The settings file, relative to the Streamer.bot folder: its layout file sits beside it.</param>
        public static string[] Due(string host, string title, string version, string releases, string settingsFile)
        {
            try
            {
                var layout = LayoutOf(settingsFile);
                bool wants = layout == null || layout.UpdateNotices;
                var now = Now();
                lock (Gate)
                {
                    var state = Read();
                    var run = ThisRun(state, now);
                    Enter(run, host, title, version, releases, wants);
                    Write(state);
                    if ((bool?)run["notified"] == true || !wants)
                        return null;
                    if (now - Time(run["first"]) < Delay)
                    {
                        StartChecks(state, run);
                        return new string[0];
                    }
                }

                lock (Gate)
                {
                    var state = Read();
                    var run = ThisRun(state, now);
                    if ((bool?)run["notified"] == true)
                        return null;
                    if (StartChecks(state, run))
                        return new string[0];   // an answer is not fresh yet: the notice waits for it

                    var checks = Obj(state, "checks");
                    var framework = checks["framework"] as JObject;
                    var latest = Web.ParseVersion((string)framework?["version"]);
                    var needs = Web.ParseVersion((string)framework?["minHostStub"]);
                    var installed = NewestInstalled();
                    var items = new List<string>();
                    var log = new List<string>();
                    var lang = Conventions.Load(layout?.Language ?? "en").Lang;

                    foreach (var game in Obj(run, "games").Properties().Select(p => p.Value).OfType<JObject>().Where(g => (bool?)g["wants"] != false))
                    {
                        string name = (string)game["title"] ?? "?", running = (string)game["version"], repo = (string)game["releases"];
                        string tag = string.IsNullOrWhiteSpace(repo) ? null : (string)(checks["game:" + repo] as JObject)?["tag"];
                        if (tag != null && Web.IsNewerVersion(tag, running))
                        {
                            items.Add(name + " " + tag);
                            log.Add(name + " " + tag + " is out (this Streamer.bot runs " + running + "): https://github.com/" + repo + "/releases/latest");
                        }
                        var stub = Web.ParseVersion((string)Obj(state, "stubs")[(string)game["host"] ?? ""]);
                        if (latest != null && needs != null && stub != null && needs > stub)
                        {
                            items.Add(Format(lang, "update.chat.reimport", name));
                            log.Add(name + ": its settings action is older than UIFramework " + Tag(latest) + " needs (" + Tag(needs) + "); import the latest " + name + " release.");
                        }
                    }
                    if (latest != null && installed != null && latest > installed)
                    {
                        items.Add("UIFramework " + Tag(latest));
                        log.Add("UIFramework " + Tag(latest) + " is out (installed: " + Tag(installed) + "); open the settings of any game to update it.");
                    }

                    run["notified"] = true;
                    Write(state);
                    if (items.Count == 0)
                        return null;
                    var lines = new List<string> { Format(lang, "update.chat", string.Join(", ", items)) };
                    lines.AddRange(log);
                    return lines.ToArray();
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The window, when it opens: the game and the version that runs, for this run's notice, and the
        /// version of its settings action, so a release that needs a newer one can be said.</summary>
        public static void Register(string host, string title, string version, string releases, string settingsFile, string stubVersion)
        {
            if (string.IsNullOrWhiteSpace(host)) return;
            try
            {
                var layout = LayoutOf(settingsFile);
                lock (Gate)
                {
                    var state = Read();
                    Enter(ThisRun(state, Now()), host, title, version, releases, layout == null || layout.UpdateNotices);
                    if (!string.IsNullOrWhiteSpace(stubVersion))
                        Obj(state, "stubs")[host] = stubVersion;
                    Write(state);
                }
            }
            catch (Exception) { }
        }

        /// <summary>The newest UIFramework.dll under lib\&lt;version&gt;\, or this one's version when none is there.</summary>
        public static Version NewestInstalled()
        {
            Version newest = null;
            string lib = Path.Combine(Folder, "lib");
            if (Directory.Exists(lib))
                foreach (var dir in Directory.GetDirectories(lib))
                {
                    var version = Web.ParseVersion(Path.GetFileName(dir));
                    if (version != null && File.Exists(Path.Combine(dir, "UIFramework.dll")) && (newest == null || version > newest))
                        newest = version;
                }
            return newest ?? Web.ParseVersion(typeof(UpdateNotices).Assembly.GetName().Version.ToString(3));
        }

        // ---- This Streamer.bot run ---------------------------------------------------------------------------------

        /// <summary>The part of the file about this Streamer.bot run; a new run starts it afresh.</summary>
        private static JObject ThisRun(JObject state, DateTime now)
        {
            string id = Session();
            var run = state["run"] as JObject;
            if (run == null || (string)run["id"] != id)
            {
                run = new JObject { ["id"] = id, ["first"] = now.ToString("o", CultureInfo.InvariantCulture), ["games"] = new JObject() };
                state["run"] = run;
            }
            return run;
        }

        private static void Enter(JObject run, string host, string title, string version, string releases, bool wants)
        {
            if (string.IsNullOrWhiteSpace(host)) return;
            Obj(run, "games")[host] = new JObject
            {
                ["host"] = host, ["title"] = string.IsNullOrWhiteSpace(title) ? host : title, ["version"] = version,
                ["releases"] = string.IsNullOrWhiteSpace(releases) ? null : releases.Trim(), ["wants"] = wants
            };
        }

        private static string ProcessStart()
        {
            using (var process = Process.GetCurrentProcess())
                return process.Id.ToString(CultureInfo.InvariantCulture) + "@" + process.StartTime.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
        }

        private static LayoutFile LayoutOf(string settingsFile)
        {
            if (string.IsNullOrWhiteSpace(settingsFile)) return null;
            string path = Path.IsPathRooted(settingsFile) ? settingsFile : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, settingsFile);
            return LayoutFile.Load(path);
        }

        // ---- The checks --------------------------------------------------------------------------------------------

        /// <summary>Starts the checks this run's notice needs whose answer is older than <see cref="CheckEvery"/> (an
        /// hour after a failure), each once at a time. True while any of them has no good answer yet: offline, the notice
        /// waits and the check is tried again an hour later.</summary>
        private static bool StartChecks(JObject state, JObject run)
        {
            var checks = Obj(state, "checks");
            var wanted = new Dictionary<string, Func<JObject>>(StringComparer.OrdinalIgnoreCase) { ["framework"] = CheckFramework };
            foreach (var game in Obj(run, "games").Properties().Select(p => p.Value).OfType<JObject>())
            {
                string repo = (string)game["releases"];
                if (!string.IsNullOrWhiteSpace(repo) && (bool?)game["wants"] != false)
                    wanted["game:" + repo] = () => CheckGame(repo);
            }
            bool waiting = false;
            foreach (var check in wanted)
            {
                var answer0 = checks[check.Key] as JObject;
                if (answer0 != null && answer0["failed"] != null) waiting = true;
                if (!Stale(answer0)) continue;
                waiting = true;
                if (!Running.Add(check.Key)) continue;
                var job = check;
                Action work = () =>
                {
                    JObject answer;
                    try { answer = job.Value(); }
                    catch (Exception ex) { answer = new JObject { ["failed"] = ex.Message }; }
                    answer["at"] = Now().ToString("o", CultureInfo.InvariantCulture);
                    try
                    {
                        lock (Gate)
                        {
                            var fresh = Read();
                            Obj(fresh, "checks")[job.Key] = answer;
                            Write(fresh);
                        }
                    }
                    catch (Exception) { }
                    finally { lock (Gate) Running.Remove(job.Key); }
                };
                if (InBackground) Task.Run(work);
                else
                {
                    // Tests: the check runs here, and its answer is in the file before the caller reads it again.
                    System.Threading.Monitor.Exit(Gate);
                    try { work(); }
                    finally { System.Threading.Monitor.Enter(Gate); }
                }
            }
            if (!InBackground && wanted.Count > 0)
            {
                var fresh = Obj(Read(), "checks");
                waiting = wanted.Keys.Any(k => !(fresh[k] is JObject a) || a["failed"] != null || Stale(a));
                foreach (var p in fresh.Properties().ToList()) checks[p.Name] = p.Value.DeepClone();
            }
            return waiting;
        }

        private static bool Stale(JObject check)
        {
            if (check == null) return true;
            return Now() - Time(check["at"]) >= (check["failed"] != null ? RetryAfterFailure : CheckEvery);
        }

        private static DateTime Time(JToken value) =>
            DateTime.TryParse((string)value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at) ? at.ToUniversalTime() : DateTime.MinValue;

        private static JObject CheckFramework()
        {
            var latest = JObject.Parse(Fetch(FrameworkLatestUrl));
            return new JObject { ["version"] = (string)latest["version"], ["minHostStub"] = (string)latest["minHostStub"] };
        }

        private static JObject CheckGame(string releases)
        {
            var release = JObject.Parse(Fetch("https://api.github.com/repos/" + releases.Trim() + "/releases/latest"));
            return new JObject { ["tag"] = (string)release["tag_name"] };
        }

        private static string FetchFromWeb(string url)
        {
            using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) })
            {
                client.DefaultRequestHeaders.Add("User-Agent", "UIFramework-UpdateNotices");
                if (url.StartsWith("https://api.github.com/", StringComparison.OrdinalIgnoreCase))
                    client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
                return client.GetStringAsync(url).GetAwaiter().GetResult();
            }
        }

        // ---- The shared file ---------------------------------------------------------------------------------------

        private static JObject Read()
        {
            try
            {
                if (!File.Exists(StatePath)) return new JObject();
                using (var reader = new JsonTextReader(new StringReader(File.ReadAllText(StatePath))) { DateParseHandling = DateParseHandling.None })
                    return JObject.Load(reader);
            }
            catch (Exception) { return new JObject(); }
        }

        private static void Write(JObject state)
        {
            Directory.CreateDirectory(Folder);
            SettingsStore.WriteAllTextAtomic(StatePath, state.ToString(Formatting.Indented));
        }

        private static JObject Obj(JObject parent, string key)
        {
            if (parent[key] is JObject child) return child;
            child = new JObject();
            parent[key] = child;
            return child;
        }

        private static string Tag(Version version) => "v" + version.Major + "." + version.Minor + "." + Math.Max(0, version.Build);

        private static string Format(Lang lang, string key, params object[] args)
        {
            string text = lang.Ui.TryGetValue(key, out var own) ? own : Conventions.Default.Lang.Ui.TryGetValue(key, out var english) ? english : key;
            return string.Format(CultureInfo.InvariantCulture, text, args);
        }
    }
}
