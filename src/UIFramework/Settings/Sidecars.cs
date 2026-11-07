using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UIFramework
{
    /// <summary>
    /// Settings that live in a file of their own next to the settings file: a setting with <c>file</c> (and
    /// optionally <c>field</c>) in the schema is read from that file on open and written back into it on save,
    /// and never lands in the settings file itself. A host keeps its word bank or its question list in the file
    /// its game already reads, with the editor the window provides.
    /// </summary>
    public static class Sidecars
    {
        public static IEnumerable<Setting> Of(IEnumerable<Setting> settings) =>
            (settings ?? Enumerable.Empty<Setting>()).Where(s => !string.IsNullOrWhiteSpace(s.File));

        public static string PathOf(Setting setting, string settingsPath)
        {
            string file = setting.File.Trim();
            return Path.IsPathRooted(file) ? file : Path.Combine(Path.GetDirectoryName(settingsPath) ?? "", file);
        }

        /// <summary>Puts each sidecar's value into the settings object. A missing file or field leaves the default
        /// (already in the object) in place; a damaged file is logged and leaves the default too.</summary>
        public static void Load(JObject settings, IEnumerable<Setting> schema, string settingsPath, IHostLog log, string prefix, ISet<string> unreadable = null)
        {
            foreach (var setting in Of(schema))
            {
                string path = PathOf(setting, settingsPath);
                if (!File.Exists(path))
                    continue;
                try
                {
                    var root = Read(path);
                    var token = string.IsNullOrWhiteSpace(setting.Field) ? root : (root as JObject)?[setting.Field];
                    if (token != null && token.Type != JTokenType.Null)
                        settings[setting.Key] = token.DeepClone();
                }
                catch (Exception ex)
                {
                    unreadable?.Add(setting.Key);
                    log?.Warn(prefix + " " + Path.GetFileName(path) + " could not be read; the window shows the defaults for '" + setting.Key + "': " + ex.Message);
                }
            }
        }

        /// <summary>The settings without the sidecar keys, for a write of the settings file that must not touch
        /// the sidecar files (the theme moving on open, a reset of the settings alone).</summary>
        public static JObject Strip(JObject settings, IEnumerable<Setting> schema)
        {
            var main = (JObject)settings.DeepClone();
            foreach (var setting in Of(schema))
                main.Remove(setting.Key);
            DropTransient(main, schema);
            return main;
        }

        /// <summary>A transient setting is never written: it only exists while the window is open. Neither is a note.</summary>
        public static void DropTransient(JObject main, IEnumerable<Setting> schema)
        {
            foreach (var setting in (schema ?? Enumerable.Empty<Setting>()).Where(s => s.Transient || s.Kind == "note"))
                main.Remove(setting.Key);
        }

        /// <summary>Writes the sidecar values into their files (keeping whatever else a file holds) and returns the
        /// settings without those keys, for the settings file. With <paramref name="only"/>, a file is written only when
        /// one of its keys is in it (what the user changed), so a list the game filled meanwhile is not overwritten; a
        /// file that is not there yet is written anyway when the settings hold more than the default for it, so a list
        /// moving out of the settings file into its own lands there. A file that cannot be read is kept as .broken
        /// before it is written over.</summary>
        public static JObject Split(JObject settings, IEnumerable<Setting> schema, string settingsPath, ISet<string> only = null)
        {
            var main = (JObject)settings.DeepClone();
            foreach (var group in Of(schema).GroupBy(s => PathOf(s, settingsPath), StringComparer.OrdinalIgnoreCase))
            {
                string path = group.Key;
                bool moving = !File.Exists(path) && group.Any(s => settings[s.Key] is JToken v && v.Type != JTokenType.Null && !(v is JContainer c && !c.HasValues) && !JToken.DeepEquals(v, s.Default));
                if (only != null && !moving && !group.Any(s => only.Contains(s.Key)))
                {
                    foreach (var setting in group)
                        main.Remove(setting.Key);
                    continue;
                }
                JToken root = null;
                if (File.Exists(path))
                {
                    try { root = Read(path); }
                    catch (Exception)
                    {
                        // Damaged: written over from what the window holds, but a copy of it stays beside it.
                        try { File.Copy(path, path + ".broken", true); } catch (Exception) { }
                        root = null;
                    }
                }
                foreach (var setting in group)
                {
                    var value = settings[setting.Key]?.DeepClone() ?? new JArray();
                    if (string.IsNullOrWhiteSpace(setting.Field))
                        root = value;
                    else
                    {
                        var obj = root as JObject ?? new JObject();
                        obj[setting.Field] = value;
                        root = obj;
                    }
                    main.Remove(setting.Key);
                }
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                SettingsStore.WriteAllTextAtomic(path, root.ToString(Formatting.Indented));
            }
            DropTransient(main, schema);
            return main;
        }

        private static JToken Read(string path)
        {
            string json;
            for (int attempt = 1; ; attempt++)
            {
                try { json = File.ReadAllText(path); break; }
                catch (IOException) when (attempt < 5) { Thread.Sleep(40); }
            }
            using (var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None })
                return JToken.ReadFrom(reader);
        }
    }
}
