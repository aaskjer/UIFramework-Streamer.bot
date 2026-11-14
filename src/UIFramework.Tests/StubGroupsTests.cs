using System.Reflection;
using Xunit;

/// <summary>Streamer.bot's <c>CPH.GetGroups()</c> for the settings action under test. The stand-in CPH
/// (HostBlock/StubBase.cs) has no such member, so the stub's call reaches this one: what it answers, and on which
/// thread it was asked.</summary>
public static class StubCphGroups
{
    public static Func<List<string>> Answer = () => new List<string>();
    public static int AskedOn;

    public static List<string> GetGroups(this UIFramework.Tests.StubCph cph)
    {
        AskedOn = Thread.CurrentThread.ManagedThreadId;
        return Answer();
    }
}

namespace UIFramework.Tests
{
    /// <summary>The settings action hands the window Streamer.bot's groups (Host.Groups, stub 0.3.2), for the chips of
    /// a grouplist field to suggest.</summary>
    [Collection("stub")]
    public class StubGroupsTests : IDisposable
    {
        private const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance;

        private static void Set(string name, object value) =>
            (typeof(StubUnderTest).GetField(name, Any) ?? throw new MissingFieldException(name)).SetValue(null, value);

        public void Dispose()
        {
            StubCphGroups.Answer = () => new List<string>();
            Set("_toStart", TimeSpan.FromSeconds(5));
        }

        [Fact]
        public void TheGroupsAreReadOnStreamerBotsThreadAndReachTheWindowAsJson()
        {
            var stub = new StubUnderTest();
            StubCphGroups.Answer = () => new List<string> { "VIP", "Moderators" };
            var host = new ReflectionHost(new StubUnderTest.Host(stub));
            string json;
            int pumpedOn;
            using (Pumping(stub, out pumpedOn))
                json = host.Groups();
            Assert.Equal(new[] { "VIP", "Moderators" }, JArray.Parse(json).Select(t => (string)t));
            Assert.Equal(pumpedOn, StubCphGroups.AskedOn);                 // through the queue, not on the window's thread
            Assert.Equal(new List<string> { "Moderators", "VIP" }, SchemaPage.GroupNames(json));
        }

        [Fact]
        public void GroupsThatCannotBeReadAreNoSuggestionsAndALogLine()
        {
            var stub = new StubUnderTest();
            StubCphGroups.Answer = () => throw new InvalidOperationException("the groups are busy");
            var host = new ReflectionHost(new StubUnderTest.Host(stub));
            using (Pumping(stub, out _))
                Assert.Null(host.Groups());
            Assert.Contains(stub.CPH.Log, line => line.StartsWith("warn") && line.Contains("groups could not be read") && line.Contains("the groups are busy"));

            // Nobody runs Streamer.bot's thread: the window is not held up for longer than a request may take to start.
            Set("_toStart", TimeSpan.FromMilliseconds(200));
            StubCphGroups.Answer = () => new List<string> { "VIP" };
            Assert.Null(host.Groups());
            Assert.Contains(stub.CPH.Log, line => line.StartsWith("warn") && line.Contains("did not answer within five seconds"));
        }

        // Streamer.bot's thread for a test: runs the queue until the test is done with it.
        private static IDisposable Pumping(StubUnderTest stub, out int threadId)
        {
            var pump = typeof(StubUnderTest).GetMethod("Pump", Any);
            var stop = new ManualResetEventSlim(false);
            var thread = new Thread(() => { while (!stop.IsSet) { pump.Invoke(stub, null); Thread.Sleep(10); } });
            thread.Start();
            threadId = thread.ManagedThreadId;
            return new Done(() => { stop.Set(); thread.Join(); });
        }

        private sealed class Done : IDisposable
        {
            private readonly Action _then;
            public Done(Action then) { _then = then; }
            public void Dispose() => _then();
        }
    }
}
