using System.Reflection;
using System.Reflection.Emit;
using Xunit;

namespace UIFramework.Tests
{
    /// <summary>The host block and the settings action load the same UIFramework: one that needs a newer settings
    /// action (its product version, "1.10.0+stub.0.4.0") is left out by both. Only one UIFramework runs in a
    /// Streamer.bot, so a host block that loaded such a DLL for the update notice or UiLive as Streamer.bot started
    /// kept the game's settings window from opening until a restart.</summary>
    [Collection("stub")]
    public class StubFitTests : IDisposable
    {
        private const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance;
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "UIFramework.Tests", "stubfit-" + Guid.NewGuid().ToString("N"));
        private static readonly string Lib = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UIFramework", "lib");
        private readonly List<string> _made = new List<string>();

        public StubFitTests() { Directory.CreateDirectory(_folder); }

        public void Dispose()
        {
            RefusedByHostBlock().SetValue(null, null);
            foreach (string dir in _made)
                try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch (Exception) { }
            try { Directory.Delete(_folder, true); } catch (Exception) { }
        }

        private static string StubVersion => (string)typeof(StubUnderTest).GetField("StubVersion", Any).GetRawConstantValue();

        private static FieldInfo RefusedByHostBlock() => typeof(UiUpdateNotice).GetField("_refused", Any);

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
        public void The_host_block_knows_the_settings_action_released_with_it()
        {
            // Kept equal by hand: a host block that allows less than its stub leaves out DLLs the window could use, one
            // that allows more loads a DLL that locks the window.
            Assert.Equal(UiUpdateNotice.StubVersion, StubVersion);
        }

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
        public void The_update_notice_passes_over_the_newest_DLL_when_it_needs_a_newer_settings_action()
        {
            // As Streamer.bot starts: the newest DLL installed was put there by a game with a newer settings action,
            // the one below it fits this game. The host block must not load the newest (only one UIFramework runs, and
            // this game's window could not open); it takes the one below, as the settings action would. That one is
            // an empty file here, so it fails to load and is remembered as refused, and nothing gets loaded at all.
            bool hadLib = Directory.Exists(Lib);
            string tooNew = Path.Combine(Lib, "99.0.0"), fits = Path.Combine(Lib, "98.0.0");
            foreach (string left in new[] { tooNew, fits })   // from a run that failed: a DLL it loaded stays locked until the run ends
                try { if (Directory.Exists(left)) Directory.Delete(left, true); } catch (Exception) { }
            Assert.False(Directory.Exists(tooNew) || Directory.Exists(fits));
            _made.Add(tooNew);
            _made.Add(fits);
            if (!hadLib) _made.Add(Lib);
            Dll(tooNew, "99.0.0+stub." + Newer(StubVersion));
            Directory.CreateDirectory(fits);
            File.WriteAllText(Path.Combine(fits, "UIFramework.dll"), "");
            RefusedByHostBlock().SetValue(null, null);

            var notices = UiUpdateNotice.FrameworkType();

            Assert.Same(typeof(UpdateNotices), notices);                                      // the one running already
            Assert.Equal(Path.Combine(fits, "UIFramework.dll"), RefusedByHostBlock().GetValue(null));
            Assert.DoesNotContain(AppDomain.CurrentDomain.GetAssemblies(), a => !a.IsDynamic && SameFile(a, tooNew));
        }

        private static bool SameFile(Assembly assembly, string folder)
        {
            try { return string.Equals(Path.GetDirectoryName(assembly.Location), folder, StringComparison.OrdinalIgnoreCase); }
            catch (Exception) { return false; }
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
    }
}
