using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using Newtonsoft.Json.Linq;

namespace UIFramework
{
    /// <summary>
    /// Settings that live in a persisted global of Streamer.bot instead of the settings file: a setting with
    /// <c>global</c> in the schema (a bot token, a webhook the project's other actions read from there). The window
    /// asks the host for the value when it opens, shows it in the setting's own field and hands it back after a save.
    /// It is never in the settings file, an export or the log, and a request to the project carries only whether
    /// there is one.
    /// </summary>
    public static class Globals
    {
        /// <summary>What a request to the project carries in place of a value that is there; an empty one is "".</summary>
        public const string Held = "*";

        /// <summary>The settings declared with a global, whether the window can use it or not: none of them is ever
        /// written to a file.</summary>
        public static IEnumerable<Setting> Of(IEnumerable<Setting> settings) =>
            (settings ?? Enumerable.Empty<Setting>()).Where(s => s.Global != null);

        /// <summary>Whether the window reads and writes the setting's global: not one declared so that it cannot work,
        /// and none while the host has no globals (<see cref="Setting.GlobalOff"/>).</summary>
        public static bool Used(Setting setting) => setting != null && setting.Global != null && !setting.GlobalOff;

        /// <summary>As the window opens: each global's value into the settings object, where the fields read it. A
        /// setting whose global the window does not use is taken out instead: what an older file holds under its key
        /// is not its value.</summary>
        public static void Load(JObject settings, IEnumerable<Setting> schema, IHostHooks hooks)
        {
            foreach (var setting in Of(schema))
            {
                if (Used(setting)) settings[setting.Key] = Value(setting, hooks.GetGlobal(setting.Global));
                else settings.Remove(setting.Key);
            }
        }

        /// <summary>A global's text as the setting's value: a switch or a number where the default is one and the text
        /// reads as one, else the text. No global at all is the default, as a key the file lacks is.</summary>
        public static JToken Value(Setting setting, string text)
        {
            if (text == null) return setting.Default?.DeepClone() ?? "";
            switch (setting.Default?.Type)
            {
                case JTokenType.Boolean:
                    if (bool.TryParse(text.Trim(), out bool on)) return on;
                    break;
                case JTokenType.Integer:
                    if (long.TryParse(text.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long whole)) return whole;
                    break;
                case JTokenType.Float:
                    if (FieldBinding.TryParseFinite(text, out double number)) return number;
                    break;
            }
            return text;
        }

        /// <summary>Lays the window's own copy of the globals over settings read from the file. A save, an import and
        /// Reset all start from the file as it is now, which never holds these values (and an older file's value under
        /// such a key is not one): what the window has stays, a setting it has nothing for is taken out.</summary>
        public static void Keep(JObject target, JObject window, IEnumerable<Setting> schema)
        {
            foreach (var setting in Of(schema))
            {
                var mine = Used(setting) ? window?[setting.Key] : null;
                if (mine != null) target[setting.Key] = mine.DeepClone();
                else target.Remove(setting.Key);
            }
        }

        /// <summary>After the settings file is written: the globals whose fields the user changed go to the host, as
        /// text; an empty one clears its global. One nobody changed is not written again: an action that renewed it
        /// meanwhile (a token) keeps its value. Returns the settings whose global the host did not take.</summary>
        public static List<Setting> Write(JObject values, ISet<string> changed, IEnumerable<Setting> schema, IHostHooks hooks)
        {
            var refused = new List<Setting>();
            foreach (var setting in Of(schema))
            {
                if (!Used(setting) || setting.ReadOnly || setting.Transient || values[setting.Key] == null) continue;
                if (changed != null && !changed.Contains(setting.Key)) continue;
                if (!hooks.SetGlobal(setting.Global, FieldBinding.Invariant(values[setting.Key])))
                    refused.Add(setting);
            }
            return refused;
        }

        /// <summary>The values as a request to the project carries them: for a setting kept in a global "" when it is
        /// empty and "*" when it holds something, so a check can still say "neither a token nor a webhook is set"; the
        /// project reads the value itself from the global. The object as it is when no setting has a global.</summary>
        /// <remarks>By the declaration, not the resolved settings: those are gathered anew with every rebuild of the
        /// window, and a request is put together on another thread.</remarks>
        public static JObject Masked(JObject values, Schema schema)
        {
            if (values == null || schema == null) return values;
            JObject masked = null;
            foreach (var declared in schema.Settings)
            {
                if (declared.Global == null || values[declared.Key] == null) continue;
                masked = masked ?? (JObject)values.DeepClone();
                masked[declared.Key] = FieldBinding.Invariant(masked[declared.Key]).Length > 0 ? Held : "";
            }
            return masked ?? values;
        }

        /// <summary>The host as the window talks to it once a setting is kept in a global: see <see cref="GlobalsGuard"/>.
        /// A window without such a setting keeps the host as it is.</summary>
        public static IHostHooks Guard(IHostHooks hooks, WindowSpec spec) =>
            hooks != null && spec?.Schema != null && spec.Schema.Settings.Any(s => s.Global != null) ? new GlobalsGuard(hooks, spec.Schema) : hooks;
    }

    /// <summary>The host of a window that keeps a setting in a global: every request that carries the window's values
    /// (a Try button, the check before a save, the word of a save, a dropdown's options) goes through here and carries
    /// such a value as <see cref="Globals.Masked"/> has it. In one place, so that no request lets a secret out.</summary>
    internal sealed class GlobalsGuard : IHostHooks
    {
        private readonly IHostHooks _host;
        private readonly Schema _schema;

        public GlobalsGuard(IHostHooks host, Schema schema) { _host = host; _schema = schema; }

        public void OnSaved(JObject settings) => _host.OnSaved(Globals.Masked(settings, _schema));
        public string Test(string name, JObject settings) => _host.Test(name, Globals.Masked(settings, _schema));
        public string Validate(JObject settings) => _host.Validate(Globals.Masked(settings, _schema));
        public string Options(string key, JObject settings) => _host.Options(key, Globals.Masked(settings, _schema));

        public FrameworkElement BuildPage(string pageId, PageContext context) => _host.BuildPage(pageId, context);
        public IEnumerable<LogEntry> ReadLog() => _host.ReadLog();
        public string ReadTable(string key) => _host.ReadTable(key);
        public string EmoteSources() => _host.EmoteSources();
        public string Groups() => _host.Groups();
        public bool HasGlobals => _host.HasGlobals;
        public string GetGlobal(string name) => _host.GetGlobal(name);
        public bool SetGlobal(string name, string value) => _host.SetGlobal(name, value);
    }
}
