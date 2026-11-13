using System.Net;
using System.Reflection;
using Xunit;

namespace UIFramework.Tests
{
    /// <summary>The settings action itself (stub/Stub.cs, compiled into this project as StubUnderTest): what it does
    /// before and around the DLL. Its private parts are reached by reflection; nothing here needs a Streamer.bot or
    /// the network.</summary>
    [Collection("stub")]
    public class StubTests : IDisposable
    {
        private const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance;
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "uiframework-stub-" + Guid.NewGuid().ToString("N"));
        private readonly string _loader;

        public StubTests()
        {
            Directory.CreateDirectory(_folder);
            Set("SettingsPath", Path.Combine(_folder, "settings.json"));
            _loader = (string)Field("LoaderPath").GetValue(null);
            if (File.Exists(_loader)) File.Delete(_loader);
        }

        public void Dispose()
        {
            try { if (File.Exists(_loader)) File.Delete(_loader); } catch (IOException) { }
            try { Directory.Delete(_folder, true); } catch (IOException) { }
            Set("_latestSeen", null);
            Set("_latestFailed", false);
            Set("_toStart", TimeSpan.FromSeconds(5));
            Set("_toFinish", TimeSpan.FromSeconds(30));
            Set("_tell", OriginalTell);
        }

        private static readonly object OriginalTell = Field("_tell").GetValue(null);

        private static FieldInfo Field(string name) => typeof(StubUnderTest).GetField(name, Any) ?? throw new MissingFieldException(name);
        private static void Set(string name, object value) => Field(name).SetValue(null, value);

        private static object Call(object target, string name, params object[] args)
        {
            var method = typeof(StubUnderTest).GetMethods(Any).Single(m => m.Name == name && m.GetParameters().Length == args.Length);
            try { return method.Invoke(target, args); }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw;
            }
        }

        private static string Constant(string name) => (string)Field(name).GetRawConstantValue();

        // ---- versions ----

        [Fact]
        public void TheStubsLineIsTheFrameworksMajorVersion()
        {
            // The stub reads lib/<line>/latest.json. A new major version of the framework has to change that one word
            // in stub/Stub.cs, or its games would go on reading the old line: this fails until it is done.
            int major = typeof(Entry).Assembly.GetName().Version.Major;
            Assert.Equal("v" + major, Constant("FrameworkLine"));
            Assert.Contains("/lib/v" + major + "/latest.json", Constant("LatestApiUrl"));
            Assert.Contains("/lib/v" + major + "/latest.json", Constant("LatestUrl"));
        }

        [Fact]
        public void TheDllAsksForNoNewerStubThanThisOne()
        {
            // MinHostStub in UIFramework.csproj above the stub's own version would lock every game out of this DLL.
            var needs = Web.ParseVersion(Entry.MinHostStub);
            var has = Web.ParseVersion(Constant("StubVersion"));
            Assert.NotNull(has);
            Assert.True(needs == null || needs <= has, "MinHostStub " + Entry.MinHostStub + " is above the stub's " + Constant("StubVersion"));
        }

        [Fact]
        public void AReleaseThatNeedsANewerStubIsToldApart()
        {
            var own = Version.Parse(Constant("StubVersion"));
            bool Newer(string minHostStub) => Call(null, "NeedsNewerStub", new JObject { ["version"] = "9.9.9", ["minHostStub"] = minHostStub }) != null;
            Assert.False(Newer(own.ToString()));
            Assert.False(Newer(own + ".0"));                      // missing parts count as 0
            Assert.False(Newer("0.0.1"));
            Assert.False(Newer(null));
            Assert.False(Newer("soon"));
            Assert.True(Newer(new Version(own.Major, own.Minor, own.Build + 1).ToString()));
            Assert.True(Newer((own.Major + 1) + ".0"));
        }

        [Fact]
        public void AFolderNamed1Point9HoldsWhatLatestJsonCalls1Point9Point0()
        {
            var full = (Version)Call(null, "Full", new Version(1, 9));
            Assert.Equal(new Version(1, 9, 0, 0), full);
            Assert.True(full <= (Version)Call(null, "Full", new Version(1, 9, 0)));   // "nothing newer", not an update
        }

        [Theory]
        [InlineData("1.9.0", "1.9.0")]
        [InlineData("v1.9.0", "1.9.0")]
        public void AVersionFromLatestJsonBecomesAFolderName(string named, string folder) =>
            Assert.Equal(folder, Call(null, "ReleaseVersion", new JObject { ["version"] = named }));

        [Theory]
        [InlineData("..\\..")]
        [InlineData("1.0-beta")]
        [InlineData("1")]
        [InlineData("")]
        public void WhatIsNoVersionNeverBecomesAFolder(string named) =>
            Assert.Throws<InvalidOperationException>(() => Call(null, "ReleaseVersion", new JObject { ["version"] = named }));

        // ---- the download ----

        [Fact]
        public void ADownloadGitHubDidNotDeliverIsTriedAgain()
        {
            Set("_retryPausesMs", new[] { 0, 0 });
            int tries = 0;
            Action<Uri, string> fetch = (uri, target) =>
            {
                if (++tries < 3) throw new WebException("The operation has timed out", WebExceptionStatus.Timeout);
                File.WriteAllText(target, "dll");
            };
            string file = Path.Combine(_folder, "UIFramework.dll.download");
            Call(null, "DownloadWithRetry", new Uri("https://example.invalid/UIFramework.dll"), file, fetch);
            Assert.Equal(3, tries);
            Assert.Equal("dll", File.ReadAllText(file));
        }

        [Fact]
        public void AfterTheLastTryTheUserReadsPlainWords()
        {
            Set("_retryPausesMs", new[] { 0, 0 });
            int tries = 0;
            Action<Uri, string> fetch = (uri, target) => { tries++; throw new WebException("The remote server returned an error: (504) Gateway Timeout.", WebExceptionStatus.Timeout); };
            var ex = Assert.Throws<InvalidOperationException>(() =>
                Call(null, "DownloadWithRetry", new Uri("https://example.invalid/UIFramework.dll"), Path.Combine(_folder, "x.download"), fetch));
            Assert.Equal(3, tries);
            Assert.Contains("GitHub did not deliver the file after 3 tries", ex.Message);
            Assert.Contains("Try again in a moment", ex.Message);
        }

        [Fact]
        public void WhatAnotherTryCannotMendIsNotTriedAgain()
        {
            Set("_retryPausesMs", new[] { 0, 0 });
            int tries = 0;
            Action<Uri, string> fetch = (uri, target) => { tries++; throw new WebException("no such host", WebExceptionStatus.NameResolutionFailure); };
            Assert.Throws<WebException>(() => Call(null, "DownloadWithRetry", new Uri("https://example.invalid/x"), Path.Combine(_folder, "x.download"), fetch));
            Assert.Equal(1, tries);
        }

        [Fact]
        public void TheExampleFolderAsksGitHubNoSecondTime()
        {
            // The check failed (offline): the hint for a DLL fetched by hand shows a placeholder at once.
            Set("_latestSeen", null);
            Set("_latestFailed", true);
            Assert.EndsWith("\\<version>\\UIFramework.dll", (string)Call(null, "ExamplePath"));
            // It is known: its version is the example, unless this settings action is too old for that release.
            Set("_latestSeen", new JObject { ["version"] = "1.9.0", ["url"] = "https://example.invalid/x" });
            Assert.EndsWith("\\1.9.0\\UIFramework.dll", (string)Call(null, "ExamplePath"));
            Set("_latestSeen", new JObject { ["version"] = "9.0.0", ["url"] = "https://example.invalid/x", ["minHostStub"] = "99.0.0" });
            Assert.EndsWith("\\<version>\\UIFramework.dll", (string)Call(null, "ExamplePath"));
        }

        // ---- what holds for every game: UIFramework\loader.json ----

        [Fact]
        public void AVersionSkippedInOneGameIsSkippedInEveryGame()
        {
            Assert.False((bool)Call(null, "Skipped", new Version(1, 9, 0)));
            Assert.Equal("UIFramework\\loader.json", Call(null, "SkipVersion", "1.9.0"));
            Assert.Equal("1.9.0", (string)JObject.Parse(File.ReadAllText(_loader))["skipVersion"]);
            Assert.True((bool)Call(null, "Skipped", new Version(1, 9, 0)));
            Assert.True((bool)Call(null, "Skipped", new Version(1, 9)));
            Assert.False((bool)Call(null, "Skipped", new Version(1, 9, 1)));   // the next release is offered again
        }

        [Fact]
        public void WhatAnOlderSettingsActionKeptInTheGamesLayoutFileStillHolds()
        {
            File.WriteAllText(Path.Combine(_folder, "uiframework.layout.json"), @"{ ""skipVersion"": ""1.8.2"", ""autoUpdate"": false }");
            Assert.True((bool)Call(null, "Skipped", new Version(1, 8, 2)));
            Assert.False((bool)Call(null, "AutoUpdateEnabled"));
        }

        [Fact]
        public void TheReleaseCheckIsOnUntilAFileSwitchesItOff()
        {
            Assert.True((bool)Call(null, "AutoUpdateEnabled"));
            Directory.CreateDirectory(Path.GetDirectoryName(_loader));
            File.WriteAllText(_loader, @"{ ""autoUpdate"": false }");
            Assert.False((bool)Call(null, "AutoUpdateEnabled"));
        }

        // ---- asking the game ----

        [Fact]
        public void EveryRequestHasAVariableOfItsOwnAndLeavesNoneBehind()
        {
            var stub = new StubUnderTest();
            var asked = new List<string>();
            stub.CPH.Game = cph =>
            {
                string var = (string)cph.Arguments["uiframework.var"];
                asked.Add(var);
                cph.SetGlobalVar(var, "answer " + asked.Count, false);
                return true;
            };
            Assert.Equal("answer 1", Call(stub, "AskGameNow", "test", "tryRoll", "{}"));
            Assert.Equal("answer 2", Call(stub, "AskGameNow", "test", "tryRoll", "{}"));
            Assert.Equal(2, asked.Distinct().Count());
            Assert.All(asked, var => Assert.StartsWith("uiframework.answer.", var));
            Assert.Empty(stub.CPH.Globals);                       // gone once read: an answer can carry a secret
            Assert.All(asked, var => Assert.Contains(var, stub.CPH.Unset));
        }

        [Fact]
        public void AHostBlockFromBeforeTheVariableWasNamedIsStillHeard()
        {
            var stub = new StubUnderTest();
            string old = (string)Call(null, "AnswerVar", Constant("GameActionName"));
            stub.CPH.Game = cph => { cph.SetGlobalVar(old, "from the old name", false); return true; };
            Assert.Equal("from the old name", Call(stub, "AskGameNow", "test", "x", "{}"));
            Assert.Empty(stub.CPH.Globals);
        }

        [Fact]
        public void AnAnswerIsUnsetAlsoWhenTheGameActionFails()
        {
            var stub = new StubUnderTest();
            string var = null;
            stub.CPH.Game = cph =>
            {
                var = (string)cph.Arguments["uiframework.var"];
                cph.SetGlobalVar(var, "half an answer", false);
                return false;                                    // "did not run"
            };
            Assert.Throws<InvalidOperationException>(() => Call(stub, "AskGameNow", "test", "x", "{}"));
            Assert.Empty(stub.CPH.Globals);
        }

        [Fact]
        public void NoAnswerIsSaidAsNoAnswerToADllThatKnowsTheMarker()
        {
            // No DLL bound in this test: the version is unknown, which counts as a current one.
            Assert.True((bool)Call(null, "KnowsNoAnswer", "test"));
            Assert.True((bool)Call(null, "KnowsNoAnswer", "table"));
            Assert.True((bool)Call(null, "KnowsNoAnswer", "validate"));
            Assert.Equal(Patient.NoAnswer, Constant("NoAnswer"));   // the stub and the DLL mean the same marker
        }

        [Fact]
        public void TheWindowsLogLinesCarryThePrefixOfTheSettingsAction()
        {
            var stub = new StubUnderTest();
            Call(stub, "ReadSchema", @"{ ""settingsFile"": ""Demo/settings.json"", ""window"": { ""title"": ""Some Title"" } }");
            string prefix = (string)Field("LogPrefix").GetValue(null);
            Assert.Equal("[" + Constant("GameActionName") + "/Settings]", prefix);   // the action's name, not the title
            var host = new StubUnderTest.Host(stub);
            Assert.Equal(prefix, new ReflectionHost(host).HostLogPrefix());
            Call(stub, "ReadSchema", @"{ ""settingsFile"": ""Demo/settings.json"", ""window"": { ""logPrefix"": ""[Mine]"" } }");
            Assert.Equal("[Mine]", new ReflectionHost(host).HostLogPrefix());
        }

        [Fact]
        public void TheUpdateDialogSaysWhenStreamerBotNeedsARestart()
        {
            string Text(string name, params object[] args) => (string)typeof(StubUnderTest).GetMethod(name, Any).Invoke(null, args);

            // Nothing loaded yet: the new version opens the window at once.
            string free = Text("UpdateQuestion", "1.9.1", "1.9.0", null);
            Assert.Contains("1.9.1 (installed: 1.9.0)", free);
            Assert.Contains("needs no restart", free);
            Assert.DoesNotContain("is restarted", free);

            // A version is running: the dialog names it and the restart, and so does the note after the download.
            string busy = Text("UpdateQuestion", "1.9.1", "1.9.0", "1.8.4");
            Assert.Contains("once Streamer.bot is restarted", busy);
            Assert.Contains("version 1.8.4 is running", busy);
            Assert.DoesNotContain("needs no restart", busy);
            foreach (string text in new[] { free, busy })
            {
                Assert.Contains("No: skip version 1.9.1", text);
                Assert.Contains("Cancel: not now", text);
            }

            string after = Text("RestartHint", "1.9.1", "1.8.4");
            Assert.Contains("1.9.1 is downloaded", after);
            Assert.Contains("Restart Streamer.bot", after);
            Assert.Contains("goes on with 1.8.4", after);
        }

        [Fact]
        public void TodaysLogIsReadWhileTheGameHoldsItOpen()
        {
            string logs = Path.Combine(_folder, "logs");
            Directory.CreateDirectory(logs);
            string file = Path.Combine(logs, "demo-log-today.txt");
            var stub = new StubUnderTest();
            Call(stub, "ReadSchema", new JObject
            {
                ["settingsFile"] = "Demo/settings.json",
                ["window"] = new JObject { ["log"] = new JObject { ["folder"] = logs, ["files"] = "demo-log*.txt" } }
            }.ToString());
            using (var writer = new FileStream(file, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes("[2026-11-12 20:00:00] WIN | User: alice\r\n");
                writer.Write(bytes, 0, bytes.Length);
                writer.Flush();
                var lines = new StubUnderTest.Host(stub).ReadLogLines().ToList();
                Assert.Single(lines);
                Assert.Contains("alice", lines[0]);
            }
        }

        // ---- the fixes of 2026-11-13 ----

        [Fact]
        public void TheHostBlockKnowsTheSettingsActionItIsReleasedWith() =>
            Assert.Equal(UiUpdateNotice.StubVersion, Constant("StubVersion"));

        [Fact]
        public void ALoaderJsonThatCannotBeReadIsNotWrittenOver()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_loader));
            const string damaged = "{ \"autoUpdate\": false, \"skipV";
            File.WriteAllText(_loader, damaged);
            Assert.Equal("this game's uiframework.layout.json", Call(null, "SkipVersion", "1.9.0"));
            Assert.Equal(damaged, File.ReadAllText(_loader));                          // the user's file, as it was
            Assert.True((bool)Call(null, "Skipped", new Version(1, 9, 0)));            // skipped for this game at least
        }

        [Fact]
        public void ARequestTheGameStillWorksOnIsWaitedFor()
        {
            Set("_toStart", TimeSpan.FromMilliseconds(500));
            Set("_toFinish", TimeSpan.FromMilliseconds(400));
            var stub = new StubUnderTest();
            bool beats = true;
            stub.CPH.Game = cph =>
            {
                // An import that takes 1.5 s, far longer than the 0.4 s a request has here.
                string var = (string)cph.Arguments["uiframework.var"];
                for (int i = 0; i < 30; i++)
                {
                    Thread.Sleep(50);
                    if (beats) AppDomain.CurrentDomain.SetData(UiHost.WorkingKey(var), DateTime.UtcNow.Ticks);
                }
                cph.SetGlobalVar(var, "imported", false);
                return true;
            };
            using (Pumping(stub))
            {
                Assert.Equal("imported", Call(stub, "AskGame", "test", "importWords", "{}", Constant("NoAnswer")));
                beats = false;
                string late = (string)Call(stub, "AskGame", "test", "importWords", "{}", Constant("NoAnswer"));
                Assert.StartsWith(Constant("NoAnswer"), late);
                Assert.Contains("did not finish", late);
            }
            Assert.Null(Field("_beatKey").GetValue(null));
        }

        [Fact]
        public void TheGameHearsOfASaveAlsoWhenItWasBusyAtFirst()
        {
            var stub = new StubUnderTest();
            var heard = new List<string>();
            stub.CPH.Game = cph =>
            {
                heard.Add((string)cph.Arguments["uiframework.request"]);
                if (heard.Count == 1) throw new TimeoutException("another request to the action has not finished.");
                return true;
            };
            new StubUnderTest.Host(stub).OnSaved("{}");
            Call(stub, "Pump");
            Assert.Equal(new[] { "saved", "saved" }, heard);
            Assert.Contains(stub.CPH.Log, line => line.StartsWith("warn") && line.Contains("telling it again"));
            Assert.DoesNotContain(stub.CPH.Log, line => line.StartsWith("error"));
        }

        [Fact]
        public void ASaveTheGameCannotHearOfIsSaidBesideTheWindow()
        {
            var stub = new StubUnderTest();
            string told = null;
            Set("_tell", new Action<string>(text => told = text));
            stub.CPH.Game = cph => false;                                          // the action is switched off
            new StubUnderTest.Host(stub).OnSaved("{}");
            Call(stub, "Pump");
            Assert.Contains("did not hear of it", told);
            Assert.Contains(stub.CPH.Log, line => line.StartsWith("error") && line.Contains("did not hear of the save"));
            Assert.DoesNotContain(stub.CPH.Log, line => line.Contains("A queued step failed"));
        }

        // Streamer.bot's thread for a test: runs the queue until the test is done with it, then waits for the job it is in.
        private static IDisposable Pumping(StubUnderTest stub)
        {
            var stop = new ManualResetEventSlim(false);
            var thread = new Thread(() => { while (!stop.IsSet) { Call(stub, "Pump"); Thread.Sleep(10); } });
            thread.Start();
            return new Done(() => { stop.Set(); thread.Join(); });
        }

        private sealed class Done : IDisposable
        {
            private readonly Action _then;
            public Done(Action then) { _then = then; }
            public void Dispose() => _then();
        }
    }

    [CollectionDefinition("stub", DisableParallelization = true)]
    public class StubCollection { }
}
