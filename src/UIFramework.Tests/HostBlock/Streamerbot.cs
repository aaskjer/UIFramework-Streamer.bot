using System.Collections.Generic;

// The host block (stub/HostSchema.cs, linked into this project) compiles against Streamer.bot's CPH interface; the
// tests stand in for it with the few members the block calls.
namespace Streamer.bot.Plugin.Interface
{
    public interface IInlineInvokeProxy
    {
        void SetGlobalVar(string name, object value, bool persisted = true);
        T GetGlobalVar<T>(string name, bool persisted = true);
        void LogInfo(string message);
        void LogWarn(string message);
        void LogError(string message);
        void SetArgument(string variableName, object value);
        void SendMessage(string message, bool bot = true, bool fallback = true);
        void SendYouTubeMessageToLatestMonitored(string message, bool useBot = true, bool fallback = true);
        void SendKickMessage(string message, bool useBot = true, bool fallback = true);
        object TwitchGetBroadcaster();
        object YouTubeGetLatestMonitoredBroadcast();
        object KickGetBroadcaster();
        object YouTubeGetBroadcaster();
        void ShowToastNotification(string title, string message, string attribution = "", string iconPath = "");
        void SetTimerInterval(string timerId, int interval);
        bool GetTimerState(string timerId);
        void EnableTimerById(string timerId);
        void DisableTimerById(string timerId);
        bool RegisterCustomTrigger(string triggerName, string eventName, string[] categories);
        void TriggerCodeEvent(string eventName, bool useArgs);
        string TwitchClientId { get; }
        string TwitchOAuthToken { get; }
        System.Collections.Generic.List<object> YouTubeGetMonitoredBroadcasts();
    }
}

namespace UIFramework.Tests
{
    public sealed class FakeCph : Streamer.bot.Plugin.Interface.IInlineInvokeProxy
    {
        public readonly Dictionary<string, object> Globals = new Dictionary<string, object>();
        public readonly List<string> Log = new List<string>();
        public void SetGlobalVar(string name, object value, bool persisted = true) => Globals[name] = value;
        public T GetGlobalVar<T>(string name, bool persisted = true) => Globals.TryGetValue(name, out var value) && value is T typed ? typed : default(T);
        public void LogInfo(string message) => Log.Add("info " + message);
        public void LogWarn(string message) => Log.Add("warn " + message);
        public void LogError(string message) => Log.Add("error " + message);
        public readonly Dictionary<string, object> Arguments = new Dictionary<string, object>();
        public void SetArgument(string variableName, object value) => Arguments[variableName] = value;
        public readonly List<string> Sent = new List<string>();
        public void SendMessage(string message, bool bot = true, bool fallback = true) => Sent.Add("twitch " + message);
        public void SendYouTubeMessageToLatestMonitored(string message, bool useBot = true, bool fallback = true) => Sent.Add("youtube " + message);
        public void SendKickMessage(string message, bool useBot = true, bool fallback = true) => Sent.Add("kick " + message);
        public object Twitch, YouTube, Kick;
        public object TwitchGetBroadcaster() => Twitch;
        public object YouTubeGetLatestMonitoredBroadcast() => YouTube;
        public object KickGetBroadcaster() => Kick;
        public object YouTubeGetBroadcaster() => YouTube;
        public readonly List<string> Toasts = new List<string>();
        public void ShowToastNotification(string title, string message, string attribution = "", string iconPath = "") => Toasts.Add(title + " | " + message);
        // A timed action: null while the import has none, which throws the way Streamer.bot does.
        public bool? TimerRunning = false;
        public int TimerInterval;
        public readonly List<string> TimerCalls = new List<string>();
        private void NeedTimer() { if (TimerRunning == null) throw new System.InvalidOperationException("no such timer"); }
        public void SetTimerInterval(string timerId, int interval) { NeedTimer(); TimerInterval = interval; TimerCalls.Add("interval " + interval); }
        // Runs as the timer's state is read: a test lets a save land just then.
        public System.Action OnTimerState;
        public bool GetTimerState(string timerId) { NeedTimer(); bool running = TimerRunning.Value; OnTimerState?.Invoke(); return running; }
        public void EnableTimerById(string timerId) { NeedTimer(); TimerRunning = true; TimerCalls.Add("enable"); }
        public void DisableTimerById(string timerId) { NeedTimer(); TimerRunning = false; TimerCalls.Add("disable"); }
        public readonly List<string> Registered = new List<string>(), Fired = new List<string>();
        public bool RegisterCustomTrigger(string triggerName, string eventName, string[] categories) { Registered.Add(triggerName + "|" + eventName + "|" + string.Join("/", categories)); return true; }
        public void TriggerCodeEvent(string eventName, bool useArgs) => Fired.Add(eventName);
        public string TwitchClientId => null;
        public string TwitchOAuthToken => null;
        public List<object> Broadcasts;
        public List<object> YouTubeGetMonitoredBroadcasts() => Broadcasts;
    }
}
