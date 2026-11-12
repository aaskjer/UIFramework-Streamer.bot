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
        }

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
            Call(null, "SkipVersion", "1.9.0");
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
    }

    [CollectionDefinition("stub", DisableParallelization = true)]
    public class StubCollection { }
}
