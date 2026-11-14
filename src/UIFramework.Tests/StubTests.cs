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
            Set("_warn", null);
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

        [Theory]
        [InlineData("reset")]
        [InlineData("socket")]
        [InlineData("connect")]
        [InlineData("send")]
        public void AConnectionThatBreaksWhileTheFileComesInIsTriedAgain(string how)
        {
            Set("_retryPausesMs", new[] { 0, 0 });
            int tries = 0;
            Action<Uri, string> fetch = (uri, target) =>
            {
                if (++tries < 3) throw Broken(how);
                File.WriteAllText(target, "dll");
            };
            string file = Path.Combine(_folder, "UIFramework.dll.download");
            Call(null, "DownloadWithRetry", new Uri("https://example.invalid/UIFramework.dll"), file, fetch);
            Assert.Equal(3, tries);
            Assert.Equal("dll", File.ReadAllText(file));
        }

        // As WebClient says it: a connection reset while the file comes in has no status of its own, only what it wraps.
        private static WebException Broken(string how)
        {
            switch (how)
            {
                case "reset": return new WebException("The underlying connection was closed: An unexpected error occurred on a receive.",
                    new IOException("Unable to read data from the transport connection: An existing connection was forcibly closed by the remote host."), WebExceptionStatus.UnknownError, null);
                case "socket": return new WebException("An exception occurred during a WebClient request.", new System.Net.Sockets.SocketException(10054), WebExceptionStatus.UnknownError, null);
                case "connect": return new WebException("Unable to connect to the remote server", WebExceptionStatus.ConnectFailure);
                default: return new WebException("The underlying connection was closed: An unexpected error occurred on a send.", WebExceptionStatus.SendFailure);
            }
        }

        [Fact]
        public void AnErrorWithoutAStatusThatIsNoBrokenConnectionIsNotTriedAgain()
        {
            // The target cannot be written: another try would fail the same way.
            Set("_retryPausesMs", new[] { 0, 0 });
            int tries = 0;
            Action<Uri, string> fetch = (uri, target) =>
            {
                tries++;
                throw new WebException("An exception occurred during a WebClient request.", new UnauthorizedAccessException("Access to the path is denied."), WebExceptionStatus.UnknownError, null);
            };
            var ex = Assert.Throws<WebException>(() => Call(null, "DownloadWithRetry", new Uri("https://example.invalid/x"), Path.Combine(_folder, "x.download"), fetch));
            Assert.IsType<UnauthorizedAccessException>(ex.InnerException);
            Assert.Equal(1, tries);
        }

        [Fact]
        public void AFileCutShortIsFetchedAgain()
        {
            // A connection closed early leaves a short file and no error; the hash check in the fetch finds it.
            Set("_retryPausesMs", new[] { 0, 0 });
            int tries = 0;
            Action<Uri, string> fetch = (uri, target) =>
            {
                Assert.False(File.Exists(target));                                  // the half file of the try before is gone
                tries++;
                File.WriteAllText(target, tries < 3 ? "dl" : "dll");
                if (tries < 3) throw new InvalidDataException("the downloaded file does not match the published hash (aa vs bb)");
            };
            string file = Path.Combine(_folder, "UIFramework.dll.download");
            Call(null, "DownloadWithRetry", new Uri("https://example.invalid/UIFramework.dll"), file, fetch);
            Assert.Equal(3, tries);
            Assert.Equal("dll", File.ReadAllText(file));
        }

        [Fact]
        public void AFileThatStillDoesNotMatchSaysSoAfterTheLastTry()
        {
            Set("_retryPausesMs", new[] { 0, 0 });
            int tries = 0;
            Action<Uri, string> fetch = (uri, target) =>
            {
                tries++;
                File.WriteAllText(target, "dl");
                throw new InvalidDataException("the downloaded file does not match the published hash (aa vs bb)");
            };
            string file = Path.Combine(_folder, "UIFramework.dll.download");
            var ex = Assert.Throws<InvalidOperationException>(() => Call(null, "DownloadWithRetry", new Uri("https://example.invalid/UIFramework.dll"), file, fetch));
            Assert.Equal(3, tries);
            Assert.Contains("does not match the published hash", ex.Message);
            Assert.Contains("also after 3 tries", ex.Message);
            Assert.DoesNotContain("GitHub did not deliver", ex.Message);
            Assert.False(File.Exists(file));
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

        [Fact]
        public void AListOrAnObjectWhereAVersionBelongsCountsAsMissing()
        {
            var heard = Hearing();
            Directory.CreateDirectory(Path.GetDirectoryName(_loader));
            File.WriteAllText(_loader, @"{ ""skipVersion"": [ ""1.9.0"" ] }");
            File.WriteAllText(Path.Combine(_folder, "uiframework.layout.json"), @"{ ""skipVersion"": { ""version"": ""1.9.0"" } }");
            for (int i = 0; i < 2; i++)
            {
                Assert.False((bool)Call(null, "Skipped", new Version(1, 9, 0)));
                Assert.Null(Call(null, "NeedsNewerStub", new JObject { ["version"] = "9.9.9", ["minHostStub"] = new JArray("99.0.0") }));
            }
            Assert.Equal(3, heard.Count);                                                    // each once in an open
            Assert.Contains(heard, m => m.Contains("skipVersion in UIFramework\\loader.json holds a list"));
            Assert.Contains(heard, m => m.Contains("skipVersion in this game's uiframework.layout.json holds an object"));
            Assert.Contains(heard, m => m.Contains("minHostStub in latest.json holds a list"));
        }

        [Fact]
        public void AnUnreadableLoaderJsonIsSaidOncePerOpen()
        {
            // The release check is off in the game's layout file, so the check reads loader.json and goes no further.
            var stub = new StubUnderTest();
            var heard = Hearing();
            const string damaged = "{ \"autoUpdate\": false, \"skipV";
            Directory.CreateDirectory(Path.GetDirectoryName(_loader));
            File.WriteAllText(_loader, damaged);
            File.WriteAllText(Path.Combine(_folder, "uiframework.layout.json"), @"{ ""autoUpdate"": false }");
            string installed = Path.Combine(_folder, "lib", "1.9.0", "UIFramework.dll");
            Assert.Equal(installed, Call(stub, "OfferUpdate", installed));
            Assert.False((bool)Call(null, "Skipped", new Version(1, 9, 0)));
            Assert.Equal("this game's uiframework.layout.json", Call(null, "SkipVersion", "1.9.0"));
            Assert.Single(heard);
            Assert.Contains("loader.json cannot be read", heard[0]);
            Assert.Equal(damaged, File.ReadAllText(_loader));
        }

        [Fact]
        public void TheReleaseCheckNamesTheInstalledVersionAsInstalled()
        {
            // What runs may be another one (a game loaded an older one as Streamer.bot started): the line says installed.
            var stub = new StubUnderTest();
            Directory.CreateDirectory(Path.GetDirectoryName(_loader));
            File.WriteAllText(_loader, @"{ ""autoUpdate"": false }");
            Call(stub, "OfferUpdate", Path.Combine(_folder, "lib", "1.9.0", "UIFramework.dll"));
            Assert.Contains(stub.CPH.Log, line => line.Contains("UIFramework 1.9.0 installed; the release check is off"));
            Assert.DoesNotContain(stub.CPH.Log, line => line.Contains(" in use"));
        }

        [Fact]
        public void ASkipThatCanBeKeptNowhereChangesNoFile()
        {
            var heard = Hearing();
            const string loader = "{ \"skipVersion\": \"1.8", layout = "{ \"columns\": ";
            string layoutFile = Path.Combine(_folder, "uiframework.layout.json");
            Directory.CreateDirectory(Path.GetDirectoryName(_loader));
            File.WriteAllText(_loader, loader);
            File.WriteAllText(layoutFile, layout);
            Assert.Null(Call(null, "SkipVersion", "1.9.0"));                                // the No button then says it could not be skipped
            Assert.Equal(loader, File.ReadAllText(_loader));
            Assert.Equal(layout, File.ReadAllText(layoutFile));
            Assert.Contains(heard, m => m.Contains("loader.json cannot be read"));
        }

        // What the stub's static readers find wrong, as an open hears it: from nothing heard yet.
        private static List<string> Hearing()
        {
            var heard = new List<string>();
            var warned = (HashSet<string>)Field("_warned").GetValue(null);
            lock (warned) warned.Clear();
            Set("_warn", new Action<string>(heard.Add));
            return heard;
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
        public void NothingIsLeftBehindWhenTheGameActionIsMissing()
        {
            var stub = new StubUnderTest();
            stub.CPH.Exists = false;
            var ex = Assert.Throws<InvalidOperationException>(() => Call(stub, "AskGameNow", "test", "x", "{}"));
            Assert.Contains("There is no action named", ex.Message);
            Assert.Empty(stub.CPH.Globals);
        }

        [Fact]
        public void AGameActionThatRunsWithoutAnsweringIsNoAnswer()
        {
            // Its Execute() never reached UiHost.Handle: no answer, not an empty one, so a Try button does not say "done".
            var stub = new StubUnderTest();
            stub.CPH.Game = cph => true;
            Assert.Null(Call(stub, "AskGameNow", "test", "tryRoll", "{}"));
            OnBotThread(stub);
            string answer = (string)Call(stub, "AskGame", "test", "tryRoll", "{}", Constant("NoAnswer"));
            Assert.StartsWith(Constant("NoAnswer"), answer);
            Assert.Contains("ran but did not answer", answer);
            Assert.Contains(stub.CPH.Log, line => line.StartsWith("error") && line.Contains("ran but did not answer"));
            Assert.Empty(stub.CPH.Globals);
        }

        [Fact]
        public void AnEmptyAnswerIsAnAnswer()
        {
            // "" as the game writes it, and null as Streamer.bot hands a global set to "" back.
            var stub = new StubUnderTest();
            OnBotThread(stub);
            foreach (string empty in new[] { "", null })
            {
                stub.CPH.Game = cph => { cph.SetGlobalVar((string)cph.Arguments["uiframework.var"], empty, false); return true; };
                Assert.Equal("", Call(stub, "AskGameNow", "test", "tryRoll", "{}"));
                Assert.Null(Call(stub, "AskGame", "test", "tryRoll", "{}", Constant("NoAnswer")));
            }
            Assert.DoesNotContain(stub.CPH.Log, line => line.StartsWith("error"));
            Assert.Empty(stub.CPH.Globals);
        }

        [Fact]
        public void ADescribeWithoutAnAnswerSaysWhatToCheck()
        {
            var stub = new StubUnderTest();
            stub.CPH.Game = cph => true;
            var ex = Assert.Throws<InvalidOperationException>(() => Call(stub, "AskSchema"));
            Assert.Contains("ran but did not answer with its settings", ex.Message);
            Assert.Contains("UiHost.Handle", ex.Message);
            Assert.Empty(stub.CPH.Globals);
        }

        [Fact]
        public void ADllInAFolderNamed1Point9GetsTheNoAnswerMarker()
        {
            // The version a loaded DLL came from is its folder's name, and "1.9" is 1.9.0, which knows the marker.
            var show = Field("_show");
            object before = show.GetValue(null);
            try
            {
                show.SetValue(null, ShowFrom("1.9"));
                Assert.True((bool)Call(null, "KnowsNoAnswer", "test"));
                Assert.True((bool)Call(null, "KnowsNoAnswer", "table"));
                show.SetValue(null, ShowFrom("1.8.4"));
                Assert.False((bool)Call(null, "KnowsNoAnswer", "test"));                  // 1.8.4 would show the marker as text
                Assert.True((bool)Call(null, "KnowsNoAnswer", "validate"));               // its check before a save knows it
            }
            finally { show.SetValue(null, before); }
        }

        // A method of an assembly loaded from a folder named after a version, as the DLL's Show is. The folder is not
        // the test's: a loaded DLL cannot be deleted.
        private static MethodInfo ShowFrom(string version)
        {
            string name = "Probe" + Guid.NewGuid().ToString("N");
            string dll = Compile(Path.Combine(Path.GetTempPath(), "UIFramework.Tests", name, version, name + ".dll"), "public static class Probe { public static void Show() { } }");
            return Assembly.LoadFrom(dll).GetType("Probe").GetMethod("Show");
        }

        /// <summary>Compiles C# into <paramref name="dll"/>, whose name the assembly takes, without loading it.</summary>
        internal static string Compile(string dll, string source)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(dll));
            using (var csc = new Microsoft.CSharp.CSharpCodeProvider())
            {
                var result = csc.CompileAssemblyFromSource(new System.CodeDom.Compiler.CompilerParameters { OutputAssembly = dll, GenerateInMemory = false }, source);
                Assert.False(result.Errors.HasErrors, string.Join("\n", result.Errors.Cast<System.CodeDom.Compiler.CompilerError>()));
            }
            return dll;
        }

        // This thread is Streamer.bot's: the stub asks the game at once instead of through the queue.
        private static void OnBotThread(StubUnderTest stub) => Field("_botThread").SetValue(stub, Thread.CurrentThread.ManagedThreadId);

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
        public void ARequestTheGameStillWorksOnIsNotTakenForAHungOne()
        {
            // An import that has run for 40 seconds and says it still works. A request from a window opened again
            // meanwhile waits for it, where after 35 seconds it went on beside it as beside one that hung, and the
            // import's next word went to the wrong request.
            var stub = new StubUnderTest();
            var importing = new ManualResetEventSlim();
            var finish = new ManualResetEventSlim();
            var order = new List<string>();
            stub.CPH.Game = cph =>
            {
                string var = (string)cph.Arguments["uiframework.var"], name = (string)cph.Arguments["uiframework.name"];
                if (name == "importWords")
                {
                    object gate = Field("_askGate").GetValue(null);
                    gate.GetType().GetField("Since").SetValue(gate, DateTime.UtcNow.AddSeconds(-40).Ticks);
                    AppDomain.CurrentDomain.SetData(UiHost.WorkingKey(var), DateTime.UtcNow.Ticks);
                    importing.Set();
                    finish.Wait(10000);
                }
                lock (order) order.Add(name);
                cph.SetGlobalVar(var, "done", false);
                return true;
            };
            Exception failed = null;
            Thread Ask(string name)
            {
                var thread = new Thread(() =>
                {
                    try { Call(stub, "AskGameNow", "test", name, "{}"); }
                    catch (Exception ex) { failed = ex; }
                });
                thread.Start();
                return thread;
            }
            var import = Ask("importWords");
            Assert.True(importing.Wait(10000));
            var next = Ask("tryRoll");
            Thread.Sleep(2500);                                    // the next request has looked at the gate twice
            finish.Set();
            import.Join();
            next.Join();
            Assert.Null(failed);
            Assert.Equal(new[] { "importWords", "tryRoll" }, order);
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
                cph.SetGlobalVar((string)cph.Arguments["uiframework.var"], "", false);   // UiHost.Handle answers "saved" too
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

        [Fact]
        public void ASaveTheGameActionRanForWithoutAnsweringIsSaidBesideTheWindow()
        {
            // Its Execute() never reached UiHost.Handle: the game did not hear of the save, and its overlay and timed
            // action go on with the old settings.
            var stub = new StubUnderTest();
            string told = null;
            int runs = 0;
            Set("_tell", new Action<string>(text => told = text));
            stub.CPH.Game = cph => { runs++; return true; };
            new StubUnderTest.Host(stub).OnSaved("{}");
            Call(stub, "Pump");
            Assert.Equal(1, runs);                                                       // another run would not answer either
            Assert.Contains("did not hear of it", told);
            Assert.Contains("ran but did not answer", told);
            Assert.Contains(stub.CPH.Log, line => line.StartsWith("error") && line.Contains("did not hear of the save"));
            Assert.Empty(stub.CPH.Globals);
        }

        // ---- one window ----

        [Fact]
        public void ARunThatALaterOneTookOverEndsThere()
        {
            // The game hung for a minute, a later run took the window over, then the game answered this run after all:
            // it ends there, without a dialog, and leaves the window to the later run.
            var stub = new StubUnderTest();
            stub.CPH.Game = cph =>
            {
                Set("_askingSince", (long)Field("_askingSince").GetValue(null) + 1);   // what TakeOver does
                cph.SetGlobalVar((string)cph.Arguments["uiframework.var"], @"{ ""settingsFile"": ""Demo/settings.json"" }", false);
                return true;
            };
            try
            {
                Assert.True(stub.Execute());
                Assert.Contains(stub.CPH.Log, line => line.StartsWith("info") && line.Contains("a later run took over"));
                Assert.DoesNotContain(stub.CPH.Log, line => line.StartsWith("error"));
                Assert.Equal(1, Field("_opening").GetValue(null));                           // still the later run's
                Assert.Null(Field("_shownSchema").GetValue(null));
            }
            finally
            {
                Set("_opening", 0);
                Set("_askingGame", 0);
                Set("_askingSince", 0L);
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void TwoRunsThatEndTogetherLeaveTheWindowFree(bool ownerFirst)
        {
            // The window closed while the run that opened it was stuck in the game, and a later run opened it again.
            // Whichever of the two ends last hands the window back: the next run asks the game anew.
            using (var streamerbot = new ProbeStreamerbot("1.0.0"))
            {
                streamerbot.Stub.TwoRuns(ownerFirst);
                Assert.Equal(0, streamerbot.Stub.Static("_opening"));
                Assert.Null(streamerbot.Stub.Static("_shownSchema"));
                Assert.Equal(0, streamerbot.Stub.Static("_serving"));
                Assert.Equal(0, streamerbot.Stub.Static("_ownerGone"));
                Assert.True(streamerbot.Stub.Run());
                Assert.Equal(2, streamerbot.Stub.Described);
                Assert.DoesNotContain(streamerbot.Stub.Log, line => line.StartsWith("error"));
            }
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

    /// <summary>A Streamer.bot of its own for the settings action: an AppDomain whose folder holds UIFramework\lib and a
    /// loader.json that switches the release check off, and where no UIFramework runs until the stub loads one (or a
    /// test does, as a game's host block would as Streamer.bot starts). Its UIFramework.dll is a stand-in whose
    /// Entry.Show runs what the test put into the AppDomain's data under "show", else returns at once.</summary>
    internal sealed class ProbeStreamerbot : IDisposable
    {
        private const string StandIn = @"namespace UIFramework
{
    public static class Entry
    {
        public static bool Show(string spec, string schema, string settingsPath, object host, System.Action pump)
        {
            var show = System.AppDomain.CurrentDomain.GetData(""show"") as System.Func<bool>;
            return show == null || show();
        }
        public static void Close(string settingsPath) { }
    }
}";
        private static readonly Lazy<byte[]> Framework = new Lazy<byte[]>(() =>
            File.ReadAllBytes(StubTests.Compile(Path.Combine(Path.GetTempPath(), "UIFramework.Tests", "stand-in-" + Guid.NewGuid().ToString("N"), "UIFramework.dll"), StandIn)));

        public readonly string Root, Lib;
        public readonly StubProbe Stub;
        private readonly AppDomain _domain;

        public ProbeStreamerbot(params string[] versions)
        {
            Root = Path.Combine(Path.GetTempPath(), "UIFramework.Tests", "streamerbot-" + Guid.NewGuid().ToString("N"));
            Lib = Path.Combine(Root, "UIFramework", "lib");
            Directory.CreateDirectory(Lib);
            foreach (string version in versions)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Dll(version)));
                File.WriteAllBytes(Dll(version), Framework.Value);
            }
            File.WriteAllText(Path.Combine(Root, "UIFramework", "loader.json"), @"{ ""autoUpdate"": false }");
            // Streamer.bot's folder has it, and the stub needs it; the test's own folder is not looked in (the runner
            // may run a shadow copy of the test assembly).
            File.Copy(typeof(JObject).Assembly.Location, Path.Combine(Root, "Newtonsoft.Json.dll"));
            _domain = AppDomain.CreateDomain("streamerbot", null, new AppDomainSetup { ApplicationBase = Root });
            Stub = (StubProbe)_domain.CreateInstanceFromAndUnwrap(typeof(StubProbe).Assembly.Location, typeof(StubProbe).FullName);
        }

        public string Dll(string version) => Path.Combine(Lib, version, "UIFramework.dll");

        public void Dispose()
        {
            AppDomain.Unload(_domain);
            try { Directory.Delete(Root, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    /// <summary>The settings action inside a <see cref="ProbeStreamerbot"/>, with a game that answers describe.</summary>
    public sealed class StubProbe : MarshalByRefObject
    {
        private readonly StubUnderTest _stub = new StubUnderTest();
        private int _described;

        public StubProbe()
        {
            _stub.CPH.Game = cph =>
            {
                _described++;
                cph.SetGlobalVar((string)cph.Arguments["uiframework.var"], @"{ ""settingsFile"": ""Demo/settings.json"" }", false);
                return true;
            };
        }

        /// <summary>How often the game was asked for its schema.</summary>
        public int Described => _described;

        public string[] Log => _stub.CPH.Log.ToArray();

        /// <summary>A static field of the stub, as the runs left it.</summary>
        public object Static(string name) => typeof(StubUnderTest).GetField(name, BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);

        /// <summary>Loads a DLL the way a game's host block does as Streamer.bot starts.</summary>
        public void Preload(string dll) => Assembly.UnsafeLoadFrom(dll);

        /// <summary>One run of the settings action.</summary>
        public bool Run() => _stub.Execute();

        /// <summary>The run that opened the window, and a later one that opened it again while the first one's window
        /// was still up. With <paramref name="ownerFirst"/> the first run ends while the later one still serves its
        /// window, else the later one ends first.</summary>
        public void TwoRuns(bool ownerFirst)
        {
            int shows = 0;
            var firstShows = new ManualResetEventSlim();
            var secondShows = new ManualResetEventSlim();
            Thread first = null, second = null;
            AppDomain.CurrentDomain.SetData("show", new Func<bool>(() =>
            {
                if (Interlocked.Increment(ref shows) == 1)
                {
                    firstShows.Set();
                    secondShows.Wait(10000);
                    if (!ownerFirst) second.Join(10000);
                }
                else
                {
                    secondShows.Set();
                    if (ownerFirst) first.Join(10000);
                }
                return true;
            }));
            first = new Thread(() => _stub.Execute());
            first.Start();
            firstShows.Wait(10000);
            second = new Thread(() => _stub.Execute());
            second.Start();
            first.Join(10000);
            second.Join(10000);
            AppDomain.CurrentDomain.SetData("show", null);
        }
    }
}
