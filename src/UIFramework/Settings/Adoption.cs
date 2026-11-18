using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UIFramework
{
    /// <summary>An action an older version of the project kept its settings in, as the window declares it
    /// (<c>window.adopt</c>, UiWindow.Adopt): the action's id in Streamer.bot and the mappings as written,
    /// <c>"old>new"</c> or <c>"#subActionId>key"</c>.</summary>
    public sealed class AdoptSource
    {
        public string ActionId;
        public readonly List<string> Map = new List<string>();
    }

    /// <summary>Where the values of one old action were found.</summary>
    public sealed class AdoptedFrom
    {
        public string ActionId;
        /// <summary>The file, as the Streamer.bot folder has it: data\actions.json or backup\backup-….zip.</summary>
        public string File;
        public bool Backup;
        /// <summary>When the backup was made; not set for Streamer.bot's own actions.</summary>
        public DateTime Date;
    }

    /// <summary>What <see cref="Adoption.Read"/> found: a value per setting, in the setting's kind, and where each
    /// came from.</summary>
    public sealed class Adopted
    {
        public readonly JObject Values = new JObject();
        public readonly Dictionary<string, AdoptedFrom> From = new Dictionary<string, AdoptedFrom>(StringComparer.Ordinal);

        /// <summary>The line for the window's status, for the values its fields took (their keys): how many, and from
        /// Streamer.bot's actions or from the backup of which day. <paramref name="why"/> names the files and says
        /// that nothing is saved yet.</summary>
        public string Note(IEnumerable<string> taken, out string why)
        {
            var places = taken.Where(From.ContainsKey).GroupBy(key => From[key].File, StringComparer.OrdinalIgnoreCase)
                .Select(g => (Place: From[g.First()], Count: g.Count()))
                .OrderBy(g => g.Place.Backup).ThenByDescending(g => g.Place.Date).ToList();
            why = places.Count == 0 ? null : Text.T("adopt.why", string.Join(", ", places.Select(g => g.Place.File)));
            return string.Join(" · ", places.Select(g =>
            {
                string day = g.Place.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                if (g.Place.Backup) return g.Count == 1 ? Text.T("adopt.backup.one", day) : Text.T("adopt.backup.many", g.Count, day);
                return g.Count == 1 ? Text.T("adopt.actions.one") : Text.T("adopt.actions.many", g.Count);
            }));
        }
    }

    /// <summary>
    /// Gets back the settings a version before this window kept in its action, as Set Argument sub-actions: an import
    /// of the new version replaces that action, and the values with it. The window declares the old action
    /// (<see cref="WindowSpec.Adopt"/>); while nothing has been saved yet, the shell reads the values here and puts
    /// them into its fields as unsaved changes. The action is looked for in Streamer.bot's actions, then in its
    /// last backups, the newest first. A value never reaches the log: it may be a webhook or a token.
    /// </summary>
    public static class Adoption
    {
        /// <summary>The Streamer.bot folder, with <c>data\actions.json</c> and <c>backup\backup-*.zip</c> in it.</summary>
        public static string Folder = AppDomain.CurrentDomain.BaseDirectory;

        private const int SetArgument = 123;
        // Send Message to Twitch, YouTube and Kick.
        private static readonly long[] SendMessage = { 10, 4001, 35001 };

        /// <summary>A mapping as the window declares it: <c>"old>new"</c>, an argument's name and the setting it goes
        /// to, or <c>"#subActionId>key"</c>, a Send Message sub-action whose text goes to the setting. False for
        /// anything else.</summary>
        public static bool TryMapping(string declared, out bool text, out string old, out string key)
        {
            text = false;
            old = key = null;
            string mapping = (declared ?? "").Trim();
            int at = mapping.LastIndexOf('>');
            if (at <= 0 || at == mapping.Length - 1) return false;
            old = mapping.Substring(0, at).Trim();
            key = mapping.Substring(at + 1).Trim();
            if (old.StartsWith("#", StringComparison.Ordinal))
            {
                text = true;
                old = old.Substring(1).Trim();
            }
            return old.Length > 0 && key.Length > 0;
        }

        /// <summary>Whether a setting of this kind takes a value from an old action: a button, a note and a table
        /// hold none, and a list's rows are no single value.</summary>
        public static bool TakesValue(Setting setting) =>
            setting != null && setting.Kind != "action" && setting.Kind != "note" && setting.Kind != "table" && setting.Kind != "list";

        /// <summary>The old values for the settings of a resolved window, or null when there are none. Each declared
        /// action is taken from the first place that has it with a value for a setting: an action the new version's
        /// import replaced is still there, without its Set Arguments, and so is it in every backup made since.
        /// Never throws; what goes wrong is one line in the log.</summary>
        public static Adopted Read(WindowSpec spec, string folder, IHostLog log)
        {
            string prefix = spec.LogPrefix + " Old values: ";
            try
            {
                var sources = spec.Adopt.Where(s => !string.IsNullOrWhiteSpace(s.ActionId)).ToList();
                if (sources.Count == 0) return null;
                var ids = sources.Select(s => Id(s.ActionId)).ToList();
                var found = new JObject[sources.Count];
                var origin = new AdoptedFrom[sources.Count];
                var places = Places(folder);
                foreach (var place in places)
                {
                    if (found.All(f => f != null)) break;
                    Dictionary<string, JObject> actions;
                    try
                    {
                        string json = place.Read();
                        // The file holds every action with its compiled code: it is read as JSON only when it names one
                        // of the actions still looked for.
                        var wanted = new HashSet<string>(ids.Where((id, i) => found[i] == null && json != null && json.IndexOf(id, StringComparison.OrdinalIgnoreCase) >= 0), StringComparer.OrdinalIgnoreCase);
                        if (wanted.Count == 0) continue;
                        actions = Actions(json, wanted);
                    }
                    catch (Exception ex)
                    {
                        // The kind of failure only: a parser's message may quote what it stumbled over.
                        log?.Warn(prefix + place.Name + " could not be read (" + ex.GetType().Name + ").");
                        continue;
                    }
                    for (int i = 0; i < sources.Count; i++)
                    {
                        if (found[i] != null || !actions.TryGetValue(ids[i], out var action)) continue;
                        var values = Values(action, sources[i], spec);
                        if (values.Count == 0) continue;
                        found[i] = values;
                        origin[i] = new AdoptedFrom { ActionId = ids[i], File = place.Name, Backup = place.Backup, Date = place.Date };
                        log?.Info(prefix + values.Count + " found for the action " + ids[i] + " in " + place.Name + ".");
                    }
                }
                var adopted = new Adopted();
                for (int i = 0; i < sources.Count; i++)
                {
                    if (found[i] == null)
                    {
                        log?.Info(prefix + "none found for the action " + ids[i] + " in data\\actions.json or in " + places.Count(p => p.Backup) + " backup(s).");
                        continue;
                    }
                    // Two old actions with a value for the same setting: the one declared first has it.
                    foreach (var value in found[i].Properties())
                        if (adopted.Values[value.Name] == null)
                        {
                            adopted.Values[value.Name] = value.Value;
                            adopted.From[value.Name] = origin[i];
                        }
                }
                return adopted.Values.Count == 0 ? null : adopted;
            }
            catch (Exception ex)
            {
                log?.Warn(prefix + "not looked for (" + ex.GetType().Name + ").");
                return null;
            }
        }

        // ---- Where the actions are -------------------------------------------------------------------------

        private sealed class Place
        {
            public string Name;
            public bool Backup;
            public DateTime Date;
            /// <summary>The actions.json there as text, or null when there is none.</summary>
            public Func<string> Read;
        }

        /// <summary>How many backups are looked at, from the newest back. The one that still has the old action is
        /// among the last few; and a window that finds nothing looks again at every open until its first save, which
        /// must not take longer with every backup there is.</summary>
        public const int BackupsLookedAt = 10;

        private static List<Place> Places(string folder)
        {
            string live = Path.Combine(folder ?? "", "data", "actions.json");
            var places = new List<Place> { new Place { Name = @"data\actions.json", Read = () => File.Exists(live) ? ReadWithRetry(live) : null } };
            string backups = Path.Combine(folder ?? "", "backup");
            if (Directory.Exists(backups))
                foreach (var zip in Directory.GetFiles(backups, "backup-*.zip").Select(path => (Path: path, Date: BackupDate(path)))
                    .OrderByDescending(z => z.Date).ThenByDescending(z => System.IO.Path.GetFileName(z.Path), StringComparer.OrdinalIgnoreCase).Take(BackupsLookedAt))
                {
                    string path = zip.Path;
                    places.Add(new Place { Name = @"backup\" + Path.GetFileName(path), Backup = true, Date = zip.Date, Read = () => FromZip(path) });
                }
            return places;
        }

        /// <summary>When a backup was made: Streamer.bot names it backup-yyyyMMdd-HHmmss…-version.zip. A file named
        /// otherwise goes by when it was written.</summary>
        public static DateTime BackupDate(string path)
        {
            var parts = Path.GetFileNameWithoutExtension(path).Split('-');
            if (parts.Length >= 3 && parts[1].Length == 8 && parts[2].Length >= 6 && DateTime.TryParseExact(parts[1] + parts[2].Substring(0, 6), "yyyyMMddHHmmss",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime stamp))
                return stamp;
            if (parts.Length >= 2 && DateTime.TryParseExact(parts[1], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out stamp))
                return stamp;
            try { return File.GetLastWriteTime(path); }
            catch (Exception) { return DateTime.MinValue; }
        }

        // Streamer.bot writes the file whenever an action changes: a locked file is tried again for a moment.
        private static string ReadWithRetry(string path)
        {
            for (int attempt = 1; ; attempt++)
            {
                try { return File.ReadAllText(path); }
                catch (IOException) when (attempt < 5) { Thread.Sleep(40); }
            }
        }

        private static string FromZip(string path)
        {
            using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var zip = new ZipArchive(file, ZipArchiveMode.Read))
            {
                var entry = zip.Entries.FirstOrDefault(e => string.Equals(e.FullName, "actions.json", StringComparison.OrdinalIgnoreCase))
                    ?? zip.Entries.FirstOrDefault(e => string.Equals(e.Name, "actions.json", StringComparison.OrdinalIgnoreCase));
                if (entry == null) return null;
                using (var reader = new StreamReader(entry.Open()))
                    return reader.ReadToEnd();
            }
        }

        /// <summary>An action's id as actions.json writes it, whatever form the declaration has it in (capitals, braces).</summary>
        private static string Id(string id) => Guid.TryParse((id ?? "").Trim(), out Guid guid) ? guid.ToString() : (id ?? "").Trim();

        /// <summary>The wanted actions of an actions.json by id, read one action at a time.</summary>
        private static Dictionary<string, JObject> Actions(string json, ISet<string> wanted)
        {
            var found = new Dictionary<string, JObject>(StringComparer.OrdinalIgnoreCase);
            using (var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None })
            {
                if (!reader.Read() || reader.TokenType != JsonToken.StartObject) return found;
                while (reader.Read() && reader.TokenType == JsonToken.PropertyName)
                {
                    bool actions = (string)reader.Value == "actions";
                    if (!reader.Read()) break;
                    if (!actions || reader.TokenType != JsonToken.StartArray) { reader.Skip(); continue; }
                    while (reader.Read() && reader.TokenType != JsonToken.EndArray)
                    {
                        if (reader.TokenType != JsonToken.StartObject) { reader.Skip(); continue; }
                        var action = JObject.Load(reader);
                        string id = Id(Scalar(action["id"]));
                        if (wanted.Contains(id) && !found.ContainsKey(id)) found[id] = action;
                    }
                }
            }
            return found;
        }

        // ---- What an action holds --------------------------------------------------------------------------

        /// <summary>The values an old action gives the window's settings: its active Set Arguments by their name (the
        /// last one of a name counts, as when the action ran) and the texts the mappings name.</summary>
        private static JObject Values(JObject action, AdoptSource source, WindowSpec spec)
        {
            var args = new Dictionary<string, JToken>(StringComparer.Ordinal);
            var texts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Walk(action["subActions"] as JArray, args, texts, 0);

            var settings = spec.ResolvedSettings.GroupBy(s => s.Key, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
            var renamed = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            var messages = new List<(string SubActionId, string Key)>();
            foreach (var declared in source.Map)
            {
                if (!TryMapping(declared, out bool text, out string old, out string key)) continue;
                if (text) { messages.Add((Id(old), key)); continue; }
                if (!renamed.TryGetValue(old, out var keys)) renamed[old] = keys = new List<string>();
                keys.Add(key);
            }

            // What the window says outright counts before a name that is a key, and that before an old key of one.
            var values = new JObject();
            var rank = new Dictionary<string, int>(StringComparer.Ordinal);
            void Put(string key, JToken value, double factor, int by)
            {
                if (!settings.TryGetValue(key, out var setting) || (rank.TryGetValue(key, out int have) && have >= by)) return;
                var converted = ToKind(setting, value, factor);
                if (converted == null) return;
                values[key] = converted;
                rank[key] = by;
            }
            foreach (var arg in args)
            {
                if (renamed.TryGetValue(arg.Key, out var keys))
                    foreach (var key in keys) Put(key, arg.Value, 1.0, 3);
                else if (settings.ContainsKey(arg.Key))
                    Put(arg.Key, arg.Value, 1.0, 2);
                else if (spec.Aliases.TryGetValue(arg.Key, out string canonical))
                {
                    // As the settings file's old keys are moved: with the factor, and to every setting that shares it.
                    double factor = spec.AliasFactors.TryGetValue(arg.Key, out double f) ? f : 1.0;
                    Put(canonical, arg.Value, factor, 1);
                    if (spec.AliasesShared.TryGetValue(arg.Key, out var more))
                        foreach (var other in more) Put(other, arg.Value, factor, 1);
                }
            }
            foreach (var message in messages)
                if (texts.TryGetValue(message.SubActionId, out string text))
                    Put(message.Key, text, 1.0, 3);
            return values;
        }

        private static void Walk(JArray subActions, Dictionary<string, JToken> args, Dictionary<string, string> texts, int depth)
        {
            if (subActions == null || depth > 32) return;
            foreach (var sub in subActions.OfType<JObject>())
            {
                // Switched off, it did nothing in the old version either; and neither did what a group that is off holds.
                if (sub["enabled"]?.Type == JTokenType.Boolean && !(bool)sub["enabled"]) continue;
                FieldBinding.TryLong(sub["type"], out long type);
                if (type == SetArgument)
                {
                    string name = Scalar(sub["variableName"]);
                    if (!string.IsNullOrEmpty(name) && sub["value"] != null) args[name] = sub["value"];
                }
                else if (Array.IndexOf(SendMessage, type) >= 0)
                {
                    string id = Scalar(sub["id"]);
                    if (!string.IsNullOrEmpty(id) && sub["text"]?.Type == JTokenType.String) texts[Id(id)] = (string)sub["text"];
                }
                if (sub["subActions"] is JArray nested) Walk(nested, args, texts, depth + 1);
            }
        }

        private static string Scalar(JToken token) => token is JValue value && value.Type != JTokenType.Null ? FieldBinding.Invariant(value) : null;

        // ---- A value in the setting's kind -----------------------------------------------------------------

        /// <summary>An old value as the setting's kind holds it, or null when it is none and is left out. A switch is
        /// true/false, yes/no, on/off, ja/nein or 1/0; a whole number is digits; a decimal has a point or a comma; text is taken as it
        /// is; a dropdown's or a slider's value is one of its options. <paramref name="factor"/> is an old key's
        /// factor (a fraction that is a percent now: 100). A setting the window never saves takes none.</summary>
        public static JToken ToKind(Setting setting, JToken value, double factor = 1.0)
        {
            if (!TakesValue(setting) || setting.Transient || setting.ReadOnly) return null;
            if (value == null || value.Type == JTokenType.Null || value is JContainer) return null;
            bool options = setting.Options != null && setting.Options.Count > 0;
            switch (setting.Kind)
            {
                case "bool":
                    return ToBool(value);
                case "int":
                case "long":
                case "duration":
                    return ToWhole(value, factor) is JToken whole && (!options || setting.Kind != "int" || Offers(setting, whole)) ? whole : null;
                case "double":
                case "percent":
                    return ToDecimal(value, factor) is JToken number && (!options || setting.Kind != "double" || Offers(setting, number)) ? number : null;
                case "slider":
                    if (options) return Option(setting, value);
                    // A slider over a range holds what its default is: whole numbers unless that is a decimal.
                    return setting.Default != null && setting.Default.Type == JTokenType.Float ? ToDecimal(value, factor) : ToWhole(value, factor);
                case "options":
                    return options ? Option(setting, value) : FieldBinding.Invariant(value);
                case "time":
                    // Written "HH:mm" as the field stores it; what is no time of day is left out.
                    return FieldBinding.TryTime(FieldBinding.Invariant(value), out string time) ? (JToken)time : null;
                default:
                    return FieldBinding.Invariant(value);
            }
        }

        private static JToken ToBool(JToken value)
        {
            if (value.Type == JTokenType.Boolean) return (bool)value;
            // The words UiSettings.With(args) takes for a switch in the host block: one old value, read one way.
            switch (FieldBinding.Invariant(value).Trim().ToLowerInvariant())
            {
                case "true": case "yes": case "on": case "1": case "ja": return true;
                case "false": case "no": case "off": case "0": case "nein": return false;
                default: return null;
            }
        }

        private static JToken ToWhole(JToken value, double factor)
        {
            if (factor == 1.0 && value.Type == JTokenType.Integer)
                return FieldBinding.TryLong(value, out long stored) ? (JToken)stored : null;
            // Digits only: "1.000" may be a thousand, and 2.5 is no whole number.
            if (factor == 1.0 && value.Type == JTokenType.String)
                return long.TryParse(((string)value).Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long typed) ? (JToken)typed : null;
            // A decimal that is a whole number (2.0), or becomes one with the old key's factor (1.5 minutes, 90 seconds).
            if (!Number(value, out double number)) return null;
            double scaled = Math.Round(number * factor, 10);
            if (Math.Abs(scaled - Math.Round(scaled)) > 1e-9 || Math.Abs(scaled) >= long.MaxValue) return null;
            return (long)Math.Round(scaled);
        }

        private static JToken ToDecimal(JToken value, double factor)
        {
            if (!Number(value, out double number)) return null;
            // Ten decimals, as the settings file's old keys are moved: 0.29 times 100 is 28.999999999999996.
            return factor == 1.0 ? number : Math.Round(number * factor, 10);
        }

        /// <summary>A number as it is stored, or as it was typed into the old action: with a point or a comma, never
        /// "1,000", which is a thousand to one reader and a one to the next.</summary>
        private static bool Number(JToken value, out double number)
        {
            number = 0;
            if (value.Type == JTokenType.Integer || value.Type == JTokenType.Float) return FieldBinding.TryNumber(value, out number);
            return value.Type == JTokenType.String && FieldBinding.TryParseTyped((string)value, out number);
        }

        /// <summary>The option the value names, in the option's own spelling: by its text whatever the case, or as the
        /// same number.</summary>
        private static JToken Option(Setting setting, JToken value)
        {
            string text = FieldBinding.Invariant(value).Trim();
            var option = setting.Options.FirstOrDefault(o => string.Equals(o.Value, text, StringComparison.OrdinalIgnoreCase))
                ?? (Invariant(text, out double wanted) ? setting.Options.FirstOrDefault(o => Invariant(o.Value, out double stop) && stop == wanted) : null);
            return option == null ? null : (JToken)(option.Value ?? "");
        }

        private static bool Offers(Setting setting, JToken number) =>
            FieldBinding.TryNumber(number, out double wanted) && setting.Options.Any(o => Invariant(o.Value, out double stop) && Math.Abs(stop - wanted) <= 1e-9 * Math.Max(1, Math.Abs(stop)));

        private static bool Invariant(string text, out double number) =>
            double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number) && !double.IsNaN(number) && !double.IsInfinity(number);
    }
}
