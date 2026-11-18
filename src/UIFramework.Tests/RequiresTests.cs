using System.Reflection;
using Xunit;

namespace UIFramework.Tests
{
    /// <summary>The oldest UIFramework a window works with (<c>UiWindow(..., requires: "2.1.0")</c>), in the DLL: one
    /// opened by a settings action that does not check says so under the red "i".</summary>
    public class RequiresTests
    {
        private static WindowSpec Prepared(string requires)
        {
            try
            {
                var schema = Schema.Parse(@"{ ""host"": ""demo"", ""settings"": [ { ""key"": ""bet"", ""default"": 10 } ], ""window"": { ""title"": ""Demo"", ""requires"": " + requires + " } }");
                var spec = Entry.SpecFor(schema, null);
                Entry.Prepare(spec, schema);
                return spec;
            }
            finally { Text.Use(Conventions.Default.Lang); }
        }

        private static Version Own => typeof(Entry).Assembly.GetName().Version;
        private static string Quoted(string version) => "\"" + version + "\"";

        [Fact]
        public void TheHostBlockWritesWhatTheWindowAsksFor()
        {
            Assert.Equal("2.1.0", (string)new UiWindow("Demo", requires: "2.1.0").ToJson()["requires"]);
            Assert.Null(new UiWindow("Demo").ToJson()["requires"]);
            Assert.Equal("2.1.0", Prepared(Quoted("2.1.0")).Requires);
            // And a secret's global, which is what the window needs the newer UIFramework for.
            Assert.Equal("Demo_botToken", (string)UiSchema.ToJson(UiSchema.S("token", "", masked: true, global: "Demo_botToken"))["global"]);
            Assert.Null(UiSchema.ToJson(UiSchema.S("token", ""))["global"]);
        }

        [Fact]
        public void AWindowThisUIFrameworkIsNewEnoughForSaysNothing()
        {
            Assert.Empty(Prepared(Quoted(Own.ToString(3))).Problems);
            Assert.Empty(Prepared(Quoted("v" + Own.Major + "." + Own.Minor)).Problems);       // missing parts count as 0
            Assert.Empty(Prepared(Quoted("1.0")).Problems);
            Assert.Empty(Prepared(Quoted(Own.Major.ToString())).Problems);                     // "2" is 2.0
            Assert.Empty(Prepared("null").Problems);
            Assert.Empty(Prepared(Quoted("")).Problems);
        }

        [Fact]
        public void AWindowThatNeedsANewerOneSaysSo()
        {
            string newer = Own.Major + "." + (Own.Minor + 1) + ".0";
            var problem = Assert.Single(Prepared(Quoted(newer)).Problems);
            Assert.StartsWith("The window requires UIFramework " + newer + " or newer, and this is " + Own.ToString(3) + ":", problem);
            Assert.Contains("stub 0.3.3", problem);
            Assert.Single(Prepared(Quoted(Own.ToString(3) + ".1")).Problems);                  // a fourth part counts too
            Assert.Single(Prepared(Quoted((Own.Major + 1) + ".0")).Problems);
        }

        [Theory]
        [InlineData("\"soon\"", "'soon'")]
        [InlineData("\"2.1-beta\"", "'2.1-beta'")]
        [InlineData("\"2.1.0.0.0\"", "'2.1.0.0.0'")]
        public void WhatIsNoVersionIsNamed(string requires, string named)
        {
            var problem = Assert.Single(Prepared(requires).Problems);
            Assert.Equal("The window requires UIFramework " + named + ", which is no version number like 2.1.0; it is not checked.", problem);
        }

        [Fact]
        public void AVersionWithoutQuotesIsNamedToo()
        {
            // 2.10 without quotes is the number 2.1: it is not read as a version.
            var spec = Prepared("2.10");
            Assert.Null(spec.Requires);
            var problem = Assert.Single(spec.Problems);
            Assert.Contains("requires is 2.1, which is no version in quotes", problem);
        }
    }

    /// <summary>The same in the settings action (stub 0.3.3): a UIFramework older than the window asks for does not open
    /// it. The newer release is offered through the loader's own path, and where it cannot be had the window stays
    /// closed and a message says why. Each test has a Streamer.bot of its own (<see cref="ProbeStreamerbot"/>) with
    /// stand-in DLLs under lib\&lt;version&gt;\, a user who answers as the test says, and a GitHub of the test's.</summary>
    [Collection("stub")]
    public class StubRequiresTests
    {
        private const int IdYes = 6, IdNo = 7;
        private const string Need = "This extension's settings window needs UIFramework for streamer.bot 2.1.0 or newer";

        // Where a version's DLL is, whatever form the folders above it are written in.
        private static string Under(string version) => @"\UIFramework\lib\" + version + @"\UIFramework.dll";

        /// <summary>latest.json for a release whose file is the one a download brings here: the stand-in that is
        /// installed as 2.0.0.</summary>
        private static string Latest(ProbeStreamerbot streamerbot, string version, string minHostStub = null) => LatestOf(streamerbot.Dll("2.0.0"), version, minHostStub);

        private static string LatestOf(string file, string version, string minHostStub = null)
        {
            string hash;
            using (var sha = System.Security.Cryptography.SHA256.Create())
            using (var stream = File.OpenRead(file))
                hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
            var latest = new JObject { ["version"] = version, ["url"] = "https://example.invalid/UIFramework.dll", ["sha256"] = hash };
            if (minHostStub != null) latest["minHostStub"] = minHostStub;
            return latest.ToString(Formatting.None);
        }

        private static RequiresProbe Probe(ProbeStreamerbot streamerbot, string requires, string latest = null)
        {
            var probe = streamerbot.Create<RequiresProbe>();
            probe.Requires(requires);
            probe.Latest(latest);
            probe.Delivers(streamerbot.Dll("2.0.0"));
            return probe;
        }

        [Fact]
        public void AWindowThatAsksForNothingOpensAsBefore()
        {
            using (var streamerbot = new ProbeStreamerbot("2.0.0"))
            {
                var probe = Probe(streamerbot, null);
                Assert.True(probe.Run());
                Assert.Equal(1, probe.Shown);
                Assert.Empty(probe.Asked);
                Assert.Equal(0, probe.LookedUp);                                      // the release check is off here
            }
        }

        [Fact]
        public void AnInstalledUIFrameworkThatIsNewEnoughOpensTheWindow()
        {
            using (var streamerbot = new ProbeStreamerbot("2.0.0", "2.1.0"))
            {
                var probe = Probe(streamerbot, "2.1.0");
                Assert.True(probe.Run());
                Assert.Equal(1, probe.Shown);
                Assert.Empty(probe.Asked);
                Assert.EndsWith(Under("2.1.0"), probe.OpenedWith);
                Assert.DoesNotContain(probe.Log, line => line.StartsWith("warn") || line.StartsWith("error"));
            }
            // An older requirement than what is there is met as well, also in its short form.
            using (var streamerbot = new ProbeStreamerbot("2.0.0"))
            {
                var probe = Probe(streamerbot, "v2");
                Assert.True(probe.Run());
                Assert.Equal(1, probe.Shown);
                Assert.Empty(probe.Asked);
            }
        }

        [Fact]
        public void AnOlderOneIsNotUsedAndTheNewerReleaseOpensTheWindowAtOnce()
        {
            using (var streamerbot = new ProbeStreamerbot("2.0.0"))
            {
                // The release check is off and this very release was skipped: both keep a release from being offered
                // at every open, and neither keeps a window closed that cannot open without it.
                string loader = Path.Combine(streamerbot.Root, "UIFramework", "loader.json");
                File.WriteAllText(loader, @"{ ""autoUpdate"": false, ""skipVersion"": ""2.1.0"" }");
                var probe = Probe(streamerbot, "2.1.0", Latest(streamerbot, "2.1.0"));
                probe.Answers(IdYes);
                Assert.True(probe.Run());

                string question = Assert.Single(probe.Asked);
                Assert.StartsWith(Need + " (installed: 2.0.0). Version 2.1.0 is available.", question);
                Assert.Contains("Yes: download it now and open the settings with it. Streamer.bot needs no restart.", question);
                Assert.Contains("No: not now. The settings window stays closed until it is installed.", question);
                Assert.Equal(1, probe.LookedUp);
                Assert.Equal(1, probe.Fetched);
                Assert.Equal(1, probe.Shown);
                Assert.EndsWith(Under("2.1.0"), probe.OpenedWith);                        // not the 2.0.0 that was there
                Assert.Contains(probe.Log, line => line.StartsWith("info") && line.Contains("UIFramework 2.1.0 downloaded to"));
                Assert.Equal(@"{ ""autoUpdate"": false, ""skipVersion"": ""2.1.0"" }", File.ReadAllText(loader));

                // The next open finds it installed and running: no question, no look at GitHub.
                Assert.True(probe.Run());
                Assert.Single(probe.Asked);
                Assert.Equal(1, probe.LookedUp);
                Assert.Equal(2, probe.Shown);
            }
        }

        [Fact]
        public void DeclinedTheWindowStaysClosed()
        {
            using (var streamerbot = new ProbeStreamerbot("2.0.0"))
            {
                var probe = Probe(streamerbot, "2.1.0", Latest(streamerbot, "2.2.0"));
                probe.Answers(IdNo);
                Assert.False(probe.Run());
                Assert.Contains("Version 2.2.0 is available.", Assert.Single(probe.Asked));  // the question said what No means: no second box
                Assert.Equal(0, probe.Fetched);
                Assert.Equal(0, probe.Shown);
                Assert.Null(probe.OpenedWith);                                              // the older one was not even loaded
                Assert.Contains(probe.Log, line => line.StartsWith("warn") && line.Contains("UIFramework 2.2.0 not downloaded; the window needs 2.1.0 or newer and stays closed."));
                // No is "not now", not "skip this version": the next open asks again.
                Assert.DoesNotContain("skipVersion", File.ReadAllText(Path.Combine(streamerbot.Root, "UIFramework", "loader.json")));
                probe.Answers(IdNo);
                Assert.False(probe.Run());
                Assert.Equal(2, probe.Asked.Length);
            }
        }

        [Fact]
        public void ADownloadThatFailsLeavesTheWindowClosed()
        {
            using (var streamerbot = new ProbeStreamerbot("2.0.0"))
            {
                var probe = Probe(streamerbot, "2.1.0", Latest(streamerbot, "2.1.0"));
                probe.Delivers(null);
                probe.Answers(IdYes);
                Assert.False(probe.Run());
                Assert.Equal(2, probe.Asked.Length);
                Assert.StartsWith("The download failed:", probe.Asked[1]);
                Assert.Contains(Need + " (installed: 2.0.0), so it stays closed.", probe.Asked[1]);
                Assert.Equal(0, probe.Shown);
                Assert.False(File.Exists(streamerbot.Dll("2.1.0")));
                Assert.Contains(probe.Log, line => line.StartsWith("warn") && line.Contains("The download failed:"));
            }
        }

        [Fact]
        public void ANewestReleaseThatIsStillTooOldIsSaid()
        {
            using (var streamerbot = new ProbeStreamerbot("2.0.0"))
            {
                var probe = Probe(streamerbot, "2.1.0", Latest(streamerbot, "2.0.5"));
                Assert.False(probe.Run());
                string message = Assert.Single(probe.Asked);                               // told, not asked: a download would not help
                Assert.Equal(Need + " (installed: 2.0.0), and the newest release is 2.0.5.\n\nThe settings window stays closed until a newer one is out.", message);
                Assert.Equal(0, probe.Fetched);
                Assert.Equal(0, probe.Shown);
                Assert.Contains(probe.Log, line => line.StartsWith("warn") && line.Contains("the newest release is 2.0.5. The settings window stays closed"));
            }
        }

        [Fact]
        public void ARequirementNothingInstalledMeetsIsNotMetByTheFirstDownloadEither()
        {
            // Nothing installed yet: the first download brings the newest release, and that one is too old as well.
            using (var streamerbot = new ProbeStreamerbot())
            {
                string file = typeof(JObject).Assembly.Location;                           // any file: it is hashed, never loaded
                var probe = streamerbot.Create<RequiresProbe>();
                probe.Requires("2.1.0");
                probe.Latest(LatestOf(file, "2.0.5"));
                probe.Delivers(file);
                probe.Answers(IdYes);                                                       // the first install's "download it now"
                Assert.False(probe.Run());
                Assert.Equal(2, probe.Asked.Length);
                Assert.Contains("and the newest release is 2.0.5.", probe.Asked[1]);
                Assert.Equal(0, probe.Shown);
            }
        }

        [Fact]
        public void AReleaseCheckThatFailsIsSaid()
        {
            using (var streamerbot = new ProbeStreamerbot("2.0.0"))
            {
                var probe = Probe(streamerbot, "2.1.0");                                   // no latest.json to be had
                Assert.False(probe.Run());
                string message = Assert.Single(probe.Asked);
                Assert.StartsWith(Need + " (installed: 2.0.0), and the newest release could not be looked up:", message);
                Assert.Contains("no connection", message);
                Assert.Contains("The settings window stays closed.", message);
                Assert.Equal(0, probe.Shown);
            }
        }

        [Fact]
        public void AReleaseThisSettingsActionIsTooOldForIsSaid()
        {
            using (var streamerbot = new ProbeStreamerbot("2.0.0"))
            {
                var probe = Probe(streamerbot, "2.1.0", Latest(streamerbot, "2.1.0", minHostStub: "99.0.0"));
                Assert.False(probe.Run());
                string message = Assert.Single(probe.Asked);
                Assert.Contains("The newest release, 2.1.0, needs a newer settings action (stub 99.0.0, this extension's is ", message);
                Assert.Contains("Update the extension; the settings window stays closed until then.", message);
                Assert.Equal(0, probe.Fetched);
                Assert.Equal(0, probe.Shown);
            }
        }

        [Fact]
        public void BesideARunningOlderOneTheNewerIsDownloadedAndWaitsForTheRestart()
        {
            using (var streamerbot = new ProbeStreamerbot("2.0.0"))
            {
                var probe = Probe(streamerbot, "2.1.0", Latest(streamerbot, "2.1.0"));
                probe.Preload(streamerbot.Dll("2.0.0"));                                   // an extension's host block, as Streamer.bot started
                probe.Answers(IdYes);
                Assert.False(probe.Run());
                Assert.Equal(2, probe.Asked.Length);
                Assert.StartsWith(Need + " (running: 2.0.0). Version 2.1.0 is available.", probe.Asked[0]);
                Assert.Contains("The settings open with it once Streamer.bot is restarted: version 2.0.0 is running here already", probe.Asked[0]);
                Assert.StartsWith("UIFramework for streamer.bot 2.1.0 is downloaded.", probe.Asked[1]);
                Assert.Contains("Restart Streamer.bot, then open the settings again", probe.Asked[1]);
                Assert.True(File.Exists(streamerbot.Dll("2.1.0")));
                Assert.Equal(0, probe.Shown);
                Assert.Contains(probe.Log, line => line.StartsWith("warn") && line.Contains("is downloaded. Restart Streamer.bot"));

                // Until the restart every open says the same, without asking or downloading again.
                Assert.False(probe.Run());
                Assert.Equal(3, probe.Asked.Length);
                Assert.Equal(Need + ". Version 2.1.0 is installed, but 2.0.0 is running in this Streamer.bot already and stays in use until it is restarted.\n\n" +
                    "Restart Streamer.bot, then open the settings again. If this comes up again after the restart, another extension loads the older version as Streamer.bot starts: update that one too.", probe.Asked[2]);
                Assert.Equal(1, probe.LookedUp);
                Assert.Equal(1, probe.Fetched);
                Assert.Equal(0, probe.Shown);
            }
        }

        [Fact]
        public void ANewerOneThatCannotBeLoadedIsNotStoodInForByOneTooOld()
        {
            using (var streamerbot = new ProbeStreamerbot("2.0.0", "2.1.0"))
            {
                File.WriteAllText(streamerbot.Dll("2.1.0") + ".sha256", "0000");            // damaged: set aside, not loaded
                var probe = Probe(streamerbot, "2.1.0");
                Assert.False(probe.Run());
                Assert.Contains("No installed UIFramework.dll could be loaded", Assert.Single(probe.Asked));
                Assert.Equal(0, probe.Shown);
                Assert.Null(probe.OpenedWith);
                Assert.Contains(probe.Log, line => line.StartsWith("warn") && line.Contains(Under("2.0.0") + " is older than the window works with (UIFramework 2.1.0 or newer) and is not used."));
            }
            // Without the requirement the older one opens the window, as it always did.
            using (var streamerbot = new ProbeStreamerbot("2.0.0", "2.1.0"))
            {
                File.WriteAllText(streamerbot.Dll("2.1.0") + ".sha256", "0000");
                var probe = Probe(streamerbot, null);
                Assert.True(probe.Run());
                Assert.EndsWith(Under("2.0.0"), probe.OpenedWith);
            }
        }

        [Fact]
        public void WhatIsNoVersionIsSaidInTheLogAndNotChecked()
        {
            using (var streamerbot = new ProbeStreamerbot("2.0.0"))
            {
                var probe = Probe(streamerbot, "soon");
                Assert.True(probe.Run());
                Assert.Equal(1, probe.Shown);
                Assert.Empty(probe.Asked);
                Assert.Contains(probe.Log, line => line.StartsWith("warn") && line.Contains("The window requires UIFramework 'soon', which is no version number like 2.1.0; it is not checked."));
            }
        }

        [Fact]
        public void ADllOutsideAVersionFolderSaysItsOwnVersion()
        {
            // This build runs from the test's folder, which names no version: its own counts.
            var own = typeof(Entry).Assembly.GetName().Version;
            var method = typeof(StubUnderTest).GetMethod("VersionRunning", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.Equal(new Version(own.Major, own.Minor, own.Build, 0), method.Invoke(null, new object[] { typeof(Entry).Assembly }));
            Assert.Null(method.Invoke(null, new object[] { null }));
            var at = typeof(StubUnderTest).GetMethod("VersionAt", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.Equal(new Version(own.Major, own.Minor, own.Build, 0), at.Invoke(null, new object[] { typeof(Entry).Assembly.Location }));
            Assert.Equal(new Version(2, 1, 0, 0), at.Invoke(null, new object[] { @"C:\nowhere\UIFramework\lib\2.1\UIFramework.dll" }));   // the folder, as everywhere in the loader
            Assert.Null(at.Invoke(null, new object[] { @"C:\nowhere\lib\UIFramework.dll" }));
        }

        // ---- the globals a window's secret lives in ----------------------------------------------------------

        [Fact]
        public void TheSettingsActionReadsAndWritesAGlobalForTheWindow()
        {
            var stub = new StubUnderTest();
            var host = new StubUnderTest.Host(stub);
            Assert.True(new ReflectionHost(host).HasGlobals);

            // Written from the window's thread: through the queue, on Streamer.bot's thread.
            host.SetGlobal("Demo_botToken", "s3cret");
            Assert.Empty(stub.CPH.Globals);
            typeof(StubUnderTest).GetMethod("Pump", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(stub, null);
            Assert.Equal("s3cret", stub.CPH.Globals["Demo_botToken"]);

            // Read on Streamer.bot's thread; a global that is not there is null.
            typeof(StubUnderTest).GetField("_botThread", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(stub, Thread.CurrentThread.ManagedThreadId);
            Assert.Equal("s3cret", host.GetGlobal("Demo_botToken"));
            Assert.Null(host.GetGlobal("Demo_none"));

            // An empty value takes the global away rather than leaving an empty one.
            host.SetGlobal("Demo_botToken", "");
            Assert.False(stub.CPH.Globals.ContainsKey("Demo_botToken"));
            Assert.Contains("Demo_botToken", stub.CPH.Unset);
            Assert.DoesNotContain(stub.CPH.Log, line => line.Contains("s3cret"));
        }
    }

    /// <summary>A settings action inside a <see cref="ProbeStreamerbot"/> whose project asks for a UIFramework, with a
    /// user and a GitHub the test plays: every box the stub would show is kept and answered from a list, latest.json
    /// is what the test says, and a download copies the file the test names.</summary>
    public sealed class RequiresProbe : MarshalByRefObject
    {
        private const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance;
        private readonly StubUnderTest _stub = new StubUnderTest();
        private readonly List<string> _asked = new List<string>();
        private readonly Queue<int> _answers = new Queue<int>();
        private string _requires, _latest, _file;
        private int _shown, _lookedUp, _fetched;

        public RequiresProbe()
        {
            _stub.CPH.Game = cph =>
            {
                var window = new JObject();
                if (_requires != null) window["requires"] = _requires;
                cph.SetGlobalVar((string)cph.Arguments["uiframework.var"], new JObject { ["settingsFile"] = "Demo/settings.json", ["window"] = window }.ToString(Formatting.None), false);
                return true;
            };
            Set("_ask", new Func<string, uint, uint, int>((text, buttons, icon) =>
            {
                _asked.Add(text);
                return _answers.Count > 0 ? _answers.Dequeue() : 1;   // OK
            }));
            Set("_fetchLatest", new Func<JObject>(() =>
            {
                _lookedUp++;
                if (_latest == null) throw new InvalidOperationException("no connection");
                return JObject.Parse(_latest);
            }));
            Set("_fetchFile", new Action<Uri, string>((uri, target) =>
            {
                _fetched++;
                if (_file == null) throw new System.Net.WebException("The remote name could not be resolved", System.Net.WebExceptionStatus.NameResolutionFailure);
                File.Copy(_file, target);
            }));
            AppDomain.CurrentDomain.SetData("show", new Func<bool>(() => { _shown++; return true; }));
        }

        private static void Set(string name, object value) => typeof(StubUnderTest).GetField(name, Any).SetValue(null, value);

        /// <summary>What the project's window asks for; null for a window that asks for nothing.</summary>
        public void Requires(string version) => _requires = version;
        /// <summary>latest.json as GitHub has it; null when it cannot be had.</summary>
        public void Latest(string json) => _latest = json;
        /// <summary>The file a download brings; null when GitHub does not deliver.</summary>
        public void Delivers(string file) => _file = file;
        /// <summary>The buttons the user presses, one per box that comes up; a box beyond them gets its OK.</summary>
        public void Answers(params int[] buttons) { foreach (int button in buttons) _answers.Enqueue(button); }
        /// <summary>Loads a DLL the way an extension's host block does as Streamer.bot starts.</summary>
        public void Preload(string dll) => Assembly.UnsafeLoadFrom(dll);

        public bool Run() => _stub.Execute();

        public string[] Asked => _asked.ToArray();
        public string[] Log => _stub.CPH.Log.ToArray();
        /// <summary>How often a window was shown, latest.json asked for and a file downloaded.</summary>
        public int Shown => _shown;
        public int LookedUp => _lookedUp;
        public int Fetched => _fetched;
        /// <summary>The DLL the settings action bound to, or null when it bound to none.</summary>
        public string OpenedWith => ((MethodInfo)typeof(StubUnderTest).GetField("_show", Any).GetValue(null))?.DeclaringType.Assembly.Location;
    }
}
