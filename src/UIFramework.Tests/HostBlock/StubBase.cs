using System;
using System.Collections.Generic;

// The settings action (stub/Stub.cs) is compiled into this project as StubUnderTest (see the project file). In
// Streamer.bot its class gets a base with CPH in it; these stand in for that base and for the few members of CPH
// the stub calls, so the stub is tested without a Streamer.bot.
namespace Streamer.bot.Plugin.Interface.Model
{
    public class CommandData
    {
        public Guid Id;
        public string Name;
        public List<string> Commands = new List<string>();
    }
}

namespace UIFramework.Tests
{
    public class StubBase
    {
        public StubCph CPH = new StubCph();
    }

    public sealed class StubCph
    {
        public readonly List<string> Log = new List<string>();
        public readonly Dictionary<string, object> Globals = new Dictionary<string, object>();
        public readonly Dictionary<string, object> Arguments = new Dictionary<string, object>();
        /// <summary>Every global the stub unset, in order.</summary>
        public readonly List<string> Unset = new List<string>();
        /// <summary>What the game action does when the stub runs it; it sees the arguments and answers into a global.</summary>
        public Func<StubCph, bool> Game = cph => true;
        public bool Exists = true;

        public void LogInfo(string message) => Log.Add("info " + message);
        public void LogWarn(string message) => Log.Add("warn " + message);
        public void LogError(string message) => Log.Add("error " + message);
        public void SetArgument(string name, object value) => Arguments[name] = value;
        public void SetGlobalVar(string name, object value, bool persisted = true) => Globals[name] = value;
        public T GetGlobalVar<T>(string name, bool persisted = true) => Globals.TryGetValue(name, out var value) && value is T typed ? typed : default(T);
        public void UnsetGlobalVar(string name, bool persisted = true) { Unset.Add(name); Globals.Remove(name); }
        public bool ActionExists(string name) => Exists;
        public bool RunAction(string name, bool runImmediately = true) => Game(this);
        public List<Streamer.bot.Plugin.Interface.Model.CommandData> GetCommands() => new List<Streamer.bot.Plugin.Interface.Model.CommandData>();
        public void CommandSetGlobalCooldownDuration(string id, int seconds) { }
        public void CommandSetUserCooldownDuration(string id, int seconds) { }
    }
}
