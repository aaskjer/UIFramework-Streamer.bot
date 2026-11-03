using System.Collections.Generic;

// The host block (stub/HostSchema.cs, linked into this project) compiles against Streamer.bot's CPH interface; the
// tests stand in for it with the few members the block calls.
namespace Streamer.bot.Plugin.Interface
{
    public interface IInlineInvokeProxy
    {
        void SetGlobalVar(string name, object value, bool persisted = true);
        void LogInfo(string message);
        void LogWarn(string message);
        void LogError(string message);
    }
}

namespace UIFramework.Tests
{
    public sealed class FakeCph : Streamer.bot.Plugin.Interface.IInlineInvokeProxy
    {
        public readonly Dictionary<string, object> Globals = new Dictionary<string, object>();
        public readonly List<string> Log = new List<string>();
        public void SetGlobalVar(string name, object value, bool persisted = true) => Globals[name] = value;
        public void LogInfo(string message) => Log.Add("info " + message);
        public void LogWarn(string message) => Log.Add("warn " + message);
        public void LogError(string message) => Log.Add("error " + message);
    }
}
