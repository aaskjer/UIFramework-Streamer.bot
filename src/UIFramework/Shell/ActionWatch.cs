using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace UIFramework
{
    /// <summary>An action of Streamer.bot the window watches (<c>window.actions</c>): the name conditions and switches
    /// call it by, its id in Streamer.bot, and what it is called while Streamer.bot does not have it.</summary>
    public sealed class WatchedAction
    {
        public string Name, Id, Title;
    }

    /// <summary>
    /// Whether the actions a window declares are in Streamer.bot and enabled. The host is asked when the window opens
    /// and about once a second while it is open, so a condition on an action (<c>action:name</c> in <c>requires</c>,
    /// <c>showWhen</c> and a pill's <c>when</c>) and a switch bound to one (<c>followsAction</c>, <c>setsAction</c>)
    /// follow what the user does in Streamer.bot without the window being opened again. While the host cannot tell
    /// (a settings action before stub 0.3.5, a preview) every action counts as enabled.
    /// </summary>
    public sealed class ActionWatch
    {
        public const string On = "on", Off = "off", Missing = "missing";

        /// <summary>How long the watch waits between two questions to the host.</summary>
        public static TimeSpan Interval = TimeSpan.FromSeconds(1);

        private readonly List<WatchedAction> _actions;
        private readonly IHostHooks _hooks;
        private readonly object _gate = new object();
        // By the declared name: the state, and what Streamer.bot calls the action now.
        private Dictionary<string, KeyValuePair<string, string>> _now = new Dictionary<string, KeyValuePair<string, string>>(StringComparer.Ordinal);

        public ActionWatch(IEnumerable<WatchedAction> actions, IHostHooks hooks)
        {
            _actions = (actions ?? Enumerable.Empty<WatchedAction>()).Where(a => a != null && !string.IsNullOrWhiteSpace(a.Name) && !string.IsNullOrWhiteSpace(a.Id)).ToList();
            _hooks = hooks ?? new NoHooks();
        }

        /// <summary>Whether the window watches any action at all.</summary>
        public bool Any => _actions.Count > 0;

        /// <summary>Whether the host has told the state of the actions at least once.</summary>
        public bool Known { get; private set; }

        private WatchedAction Declared(string name) => _actions.FirstOrDefault(a => string.Equals(a.Name, name, StringComparison.Ordinal));

        /// <summary>"on", "off" or "missing". "on" also while the host has not told, and for a name the window does
        /// not declare: what is not known does not grey anything out.</summary>
        public string State(string name)
        {
            lock (_gate)
                return name != null && _now.TryGetValue(name, out var told) ? told.Key : On;
        }

        /// <summary>What to call the action in a text: its name in Streamer.bot now, else the title it was declared
        /// with, else the name the window calls it by.</summary>
        public string Title(string name)
        {
            lock (_gate)
                if (name != null && _now.TryGetValue(name, out var told) && !string.IsNullOrWhiteSpace(told.Value))
                    return told.Value;
            var declared = Declared(name);
            return declared == null ? name : string.IsNullOrWhiteSpace(declared.Title) ? declared.Name : declared.Title;
        }

        /// <summary>Asks the host about every watched action. Null when it cannot tell. The host answers on
        /// Streamer.bot's thread, so this is called off the window's.</summary>
        public Dictionary<string, KeyValuePair<string, string>> Ask()
        {
            if (_actions.Count == 0) return null;
            string answer;
            try { answer = _hooks.ActionStates(string.Join(",", _actions.Select(a => a.Id.Trim()))); }
            catch (Exception) { return null; }
            if (string.IsNullOrWhiteSpace(answer)) return null;
            JObject told;
            try { told = JObject.Parse(answer); }
            catch (Exception) { return null; }
            var states = new Dictionary<string, KeyValuePair<string, string>>(StringComparer.Ordinal);
            foreach (var action in _actions)
            {
                var entry = told.Properties().FirstOrDefault(p => string.Equals(p.Name.Trim(), action.Id.Trim(), StringComparison.OrdinalIgnoreCase))?.Value;
                string state = entry is JObject detailed ? detailed["state"]?.ToString() : entry?.Type == JTokenType.String ? (string)entry : null;
                if (state != On && state != Off && state != Missing) continue;
                states[action.Name] = new KeyValuePair<string, string>(state, entry is JObject named ? named["name"]?.ToString() : null);
            }
            return states;
        }

        /// <summary>Takes what the host told. True when a state or a name is another than before.</summary>
        public bool Take(Dictionary<string, KeyValuePair<string, string>> states)
        {
            if (states == null) return false;
            lock (_gate)
            {
                bool changed = !Known || states.Count != _now.Count
                    || states.Any(s => !_now.TryGetValue(s.Key, out var before) || before.Key != s.Value.Key || before.Value != s.Value.Value);
                _now = states;
                Known = true;
                return changed;
            }
        }

        /// <summary>For a switch that sets an action: hands the wish to the host, and holds the action for set until
        /// the host tells. False when the action is not declared, is missing, or the host cannot set one.</summary>
        public bool Set(string name, bool enabled)
        {
            var declared = Declared(name);
            if (declared == null || State(name) == Missing) return false;
            bool handed;
            try { handed = _hooks.SetAction(declared.Id.Trim(), enabled); }
            catch (Exception) { handed = false; }
            if (!handed) return false;
            lock (_gate)
            {
                var next = new Dictionary<string, KeyValuePair<string, string>>(_now, StringComparer.Ordinal);
                next[name] = new KeyValuePair<string, string>(enabled ? On : Off, _now.TryGetValue(name, out var before) ? before.Value : null);
                _now = next;
            }
            return true;
        }
    }
}
