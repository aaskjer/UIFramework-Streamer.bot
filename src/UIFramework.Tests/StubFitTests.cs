using System.Reflection;
using System.Reflection.Emit;
using Xunit;

namespace UIFramework.Tests
{
    /// <summary>The host block and the settings action load the same UIFramework: one that needs a newer settings
    /// action (its product version, "1.10.0+stub.0.4.0") is left out by both. Only one UIFramework runs in a
    /// Streamer.bot, so a host block that loaded such a DLL for the update notice or UiLive as Streamer.bot started
    /// kept the game's settings window from opening until a restart. Which installed DLL the host block then takes is
    /// in HostBlockTests (TheUpdateNoticeTakesTheNextOlderDllWhenTheNewestCannotBeUsed), in an AppDomain of its own.</summary>
    [Collection("stub")]
    public class StubFitTests : IDisposable
    {
        private const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance;
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "UIFramework.Tests", "stubfit-" + Guid.NewGuid().ToString("N"));

        public StubFitTests() { Directory.CreateDirectory(_folder); }

        public void Dispose()
        {
            try { Directory.Delete(_folder, true); } catch (Exception) { }
        }

        private static string StubVersion => (string)typeof(StubUnderTest).GetField("StubVersion", Any).GetRawConstantValue();

        private static object Call(string name, params object[] args)
        {
            var method = typeof(StubUnderTest).GetMethods(Any).Single(m => m.Name == name && m.GetParameters().Length == args.Length);
            try { return method.Invoke(null, args); }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw;
            }
        }

        /// <summary>A DLL whose product version is <paramref name="product"/>, as the build writes it, saved to
        /// <paramref name="folder"/>\UIFramework.dll and not loaded.</summary>
        private static string Dll(string folder, string product)
        {
            Directory.CreateDirectory(folder);
            var name = new AssemblyName("StubFitProbe" + Guid.NewGuid().ToString("N")) { Version = new Version(1, 0, 0, 0) };
            var builder = AppDomain.CurrentDomain.DefineDynamicAssembly(name, AssemblyBuilderAccess.Save, folder);
            builder.SetCustomAttribute(new CustomAttributeBuilder(typeof(AssemblyInformationalVersionAttribute).GetConstructor(new[] { typeof(string) }), new object[] { product }));
            builder.DefineVersionInfoResource();
            builder.DefineDynamicModule(name.Name, "UIFramework.dll");
            builder.Save("UIFramework.dll");
            string dll = Path.Combine(folder, "UIFramework.dll");
            Assert.Equal(product, System.Diagnostics.FileVersionInfo.GetVersionInfo(dll).ProductVersion);
            return dll;
        }

        /// <summary>A running assembly whose product version is <paramref name="product"/>, as a loaded UIFramework's.</summary>
        private static Assembly Running(string product)
        {
            var builder = AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName("StubFitRunning" + Guid.NewGuid().ToString("N")), AssemblyBuilderAccess.Run);
            builder.SetCustomAttribute(new CustomAttributeBuilder(typeof(AssemblyInformationalVersionAttribute).GetConstructor(new[] { typeof(string) }), new object[] { product }));
            return builder;
        }

        private static string Newer(string version)
        {
            var v = Version.Parse(version);
            return v.Major + "." + v.Minor + "." + (Math.Max(0, v.Build) + 1);
        }

        // ---- the host block ---------------------------------------------------------------------------------

        [Fact]
        public void The_host_block_tells_a_DLL_that_needs_a_newer_settings_action_without_loading_it()
        {
            Assert.True(UiUpdateNotice.NeedsNewerStub(Dll(Path.Combine(_folder, "a"), "1.10.0+stub." + Newer(StubVersion))));
            Assert.True(UiUpdateNotice.NeedsNewerStub(Dll(Path.Combine(_folder, "b"), "1.10.0+stub." + StubVersion + ".1")));
            Assert.False(UiUpdateNotice.NeedsNewerStub(Dll(Path.Combine(_folder, "c"), "1.10.0+stub." + StubVersion)));
            Assert.False(UiUpdateNotice.NeedsNewerStub(Dll(Path.Combine(_folder, "d"), "1.10.0+stub.0.1")));      // missing parts count as 0
            Assert.False(UiUpdateNotice.NeedsNewerStub(Dll(Path.Combine(_folder, "e"), "1.8.3")));              // names none: every DLL before 1.8.4
            Assert.False(UiUpdateNotice.NeedsNewerStub(Dll(Path.Combine(_folder, "f"), "1.10.0+stub.soon")));
            Assert.False(UiUpdateNotice.NeedsNewerStub(typeof(Entry).Assembly.Location));                       // this build's own
            string empty = Path.Combine(_folder, "empty.dll");
            File.WriteAllText(empty, "");
            Assert.False(UiUpdateNotice.NeedsNewerStub(empty));
            Assert.False(UiUpdateNotice.NeedsNewerStub(Path.Combine(_folder, "missing.dll")));
            // The stub reads the same files the same way.
            foreach (string dll in Directory.GetFiles(_folder, "UIFramework.dll", SearchOption.AllDirectories))
                Assert.Equal(UiUpdateNotice.NeedsNewerStub(dll), Call("StubNeededBy", dll) != null);
        }

        [Fact]
        public void The_host_block_names_no_type_from_System_dll()
        {
            // Streamer.bot compiles a sub-action with exactly the references it lists, and a game's second action (a
            // scoreboard) may list mscorlib, System.Core, Newtonsoft.Json and Streamer.bot's interface only. The block
            // names every type in full, so a full name of a System.dll type in its code (not in a comment or a string)
            // is a compile error there. tools\check-stub.ps1 cannot see it: its compiler adds System.dll on its own.
            string root = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
            while (root != null && !File.Exists(Path.Combine(root, "UIFramework.slnx"))) root = Path.GetDirectoryName(root);
            Assert.NotNull(root);
            string code = System.Text.RegularExpressions.Regex.Replace(File.ReadAllText(Path.Combine(root, "stub", "HostSchema.cs")),
                @"//[^\n]*|@""(?:[^""]|"""")*""|""(?:[^""\\\n]|\\.)*""|'(?:[^'\\\n]|\\.)+'", "");
            var named = new SortedSet<string>();
            foreach (var type in typeof(Uri).Assembly.GetExportedTypes().Where(t => !t.IsNested && t.Namespace != null))
            {
                // A type of the same full name in mscorlib or System.Core is fine: the compiler takes that one.
                if (typeof(object).Assembly.GetType(type.FullName) != null || typeof(Enumerable).Assembly.GetType(type.FullName) != null) continue;
                string name = type.Namespace + "." + type.Name.Split('`')[0];
                if (System.Text.RegularExpressions.Regex.IsMatch(code, @"(?<![\w.])" + System.Text.RegularExpressions.Regex.Escape(name) + @"(?![\w])"))
                    named.Add(name);
            }
            Assert.Empty(named);
        }

        // ---- the settings action ----------------------------------------------------------------------------

        [Theory]
        [InlineData("1.10.0+stub.9.0.0", "9.0.0.0")]
        [InlineData("1.10.0+stub.v9.1", "9.1.0.0")]
        [InlineData("1.9.0+stub.0.1.0", null)]
        [InlineData("1.8.3", null)]
        [InlineData("1.10.0+stub.soon", null)]
        [InlineData("", null)]
        [InlineData(null, null)]
        public void A_product_version_names_the_settings_action_it_needs(string product, string needs)
        {
            Assert.Equal(needs, Call("StubNeededFor", new object[] { product })?.ToString());
        }

        [Fact]
        public void A_running_UIFramework_that_needs_a_newer_settings_action_is_not_shared()
        {
            Assert.Null(Call("SharedRefusal", typeof(Entry).Assembly));                       // this build: asks for an older stub
            Assert.Null(Call("SharedRefusal", Running("1.10.0+stub." + StubVersion)));
            Assert.Null(Call("SharedRefusal", Running("1.8.3")));                             // names none
            Assert.Null(Call("SharedRefusal", new object[] { null }));

            string newer = Newer(StubVersion);
            string refusal = (string)Call("SharedRefusal", Running("1.10.0+stub." + newer));
            Assert.Contains("needs a newer settings action (stub " + newer + ", this game's is " + StubVersion + ")", refusal);
            Assert.Contains("update this game", refusal);
        }

        [Fact]
        public void Bind_asks_before_it_shares_and_the_open_stops_at_its_refusal()
        {
            // Bind shares whatever UIFramework runs already; the test run has only this build loaded, which fits, so
            // the order is read off the source: the refusal comes before the shared Show is taken.
            string root = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
            while (root != null && !File.Exists(Path.Combine(root, "UIFramework.slnx"))) root = Path.GetDirectoryName(root);
            Assert.NotNull(root);
            string stub = File.ReadAllText(Path.Combine(root, "stub", "Stub.cs"));
            int bind = stub.IndexOf("private static bool Bind(string dllPath)", StringComparison.Ordinal);
            Assert.True(bind > 0);
            int refusal = stub.IndexOf("if (refusal != null) throw new SharedFrameworkRefused(refusal);", bind, StringComparison.Ordinal);
            int shared = stub.IndexOf("var shared = loaded?.GetType(\"UIFramework.Entry\")", bind, StringComparison.Ordinal);
            Assert.True(refusal > bind && refusal < shared, "Bind must refuse a running UIFramework that is too new before it shares it");
            Assert.Contains("catch (SharedFrameworkRefused ex)", stub);
            Assert.Contains("Ask(refused ?? ", stub);

            // A UIFramework that fits is shared as before.
            var show = typeof(StubUnderTest).GetField("_show", Any);
            var close = typeof(StubUnderTest).GetField("_close", Any);
            object before = show.GetValue(null), closeBefore = close.GetValue(null);
            try
            {
                show.SetValue(null, null);
                Assert.True((bool)Call("Bind", Path.Combine(_folder, "never-loaded.dll")));
                Assert.Same(typeof(Entry).Assembly, ((MethodInfo)show.GetValue(null)).DeclaringType.Assembly);
            }
            finally
            {
                show.SetValue(null, before);
                close.SetValue(null, closeBefore);
            }
        }

        [Fact]
        public void An_older_DLL_loaded_because_the_chosen_one_failed_is_not_said_to_run_already()
        {
            // 2.0.0 no longer matches its hash: it is set aside and 1.0.0 opens the window. This open loaded 1.0.0, and
            // 2.0.0 is offered again, not used from the next start.
            using (var streamerbot = new ProbeStreamerbot("1.0.0", "2.0.0"))
            {
                File.WriteAllText(streamerbot.Dll("2.0.0") + ".sha256", "0000");
                Assert.True(streamerbot.Stub.Run());
                Assert.True(File.Exists(streamerbot.Dll("2.0.0") + ".broken"));
                Assert.Contains(streamerbot.Stub.Log, line => line.StartsWith("error") && line.Contains("does not match the hash"));
                Assert.DoesNotContain(streamerbot.Stub.Log, line => line.Contains("running already") || line.Contains("from the next start"));
            }
        }

        [Fact]
        public void A_DLL_installed_beside_a_running_one_waits_for_the_next_start()
        {
            using (var streamerbot = new ProbeStreamerbot("1.0.0", "2.0.0"))
            {
                streamerbot.Stub.Preload(streamerbot.Dll("1.0.0"));                  // a game's host block, as Streamer.bot started
                Assert.True(streamerbot.Stub.Run());
                Assert.True(File.Exists(streamerbot.Dll("2.0.0")));
                Assert.Contains(streamerbot.Stub.Log, line => line.Contains("UIFramework 1.0.0 is running already in this Streamer.bot; 2.0.0 is used from the next start"));
            }
        }
    }
}
