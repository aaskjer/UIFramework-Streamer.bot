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

    public class ActionData
    {
        public Guid Id;
        public string Name;
        public bool Enabled = true;
    }
}

namespace UIFramework.Tests
{
    public class StubBase
    {
        public StubCph CPH = new StubCph();
        /// <summary>What the action runs with, as Streamer.bot's base class holds it: its trigger's arguments.</summary>
        public Dictionary<string, object> args = new Dictionary<string, object>();
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
        public bool RunAction(string name, bool runImmediately = true) { Ran.Add("name " + name); return Game(this); }
        /// <summary>The actions this Streamer.bot has, for a settings action that finds its game by id.</summary>
        public readonly List<Streamer.bot.Plugin.Interface.Model.ActionData> Actions = new List<Streamer.bot.Plugin.Interface.Model.ActionData>();
        public List<Streamer.bot.Plugin.Interface.Model.ActionData> GetActions() => Actions;
        public void EnableActionById(string id) => Switch(id, true);
        public void DisableActionById(string id) => Switch(id, false);
        private void Switch(string id, bool enabled)
        {
            foreach (var action in Actions)
                if (string.Equals(action.Id.ToString(), id, StringComparison.OrdinalIgnoreCase)) action.Enabled = enabled;
        }
        public bool RunActionById(string id, bool runImmediately = true) { Ran.Add("id " + id); return Game(this); }
        /// <summary>How the game action was run, in order: "name &lt;name&gt;" or "id &lt;id&gt;".</summary>
        public readonly List<string> Ran = new List<string>();
        public List<Streamer.bot.Plugin.Interface.Model.CommandData> GetCommands() => new List<Streamer.bot.Plugin.Interface.Model.CommandData>();
        public void CommandSetGlobalCooldownDuration(string id, int seconds) { }
        public void CommandSetUserCooldownDuration(string id, int seconds) { }
    }
}
