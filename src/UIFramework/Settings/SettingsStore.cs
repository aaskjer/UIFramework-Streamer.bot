using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UIFramework
{
    /// <summary>What a load produced: the settings over the defaults, and how it went.</summary>
    public sealed class SettingsLoad
    {
        public JObject Settings;
        /// <summary>Why the live file could not be read, or null.</summary>
        public string Damaged;
        /// <summary>The settings came from the .bak the last save kept.</summary>
        public bool FromBackup;
    }

    /// <summary>
    /// Reads and writes a host's settings file. Every value in the file is kept, missing keys come from the
    /// defaults. A write goes beside the live file and is swapped in, with the replaced file kept as .bak when it
    /// parses, so a crash halfway through cannot leave a truncated file. Reads retry briefly: the host's game
    /// action reads the file while the window is open, so a locked file is normal rather than an error.
    /// </summary>
    public sealed class SettingsStore
    {
        public string Path { get; }
        public JObject Defaults { get; }
        public string BackupPath => Path + ".bak";
        public string BrokenPath => Path + ".broken";

        /// <summary>Old key to canonical key. A file still carrying an old key has it moved on load; the next
        /// save writes the canonical one. Nobody has to touch a file by hand.</summary>
        public Dictionary<string, string> Aliases { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>Old key to the factor its value is multiplied with on the move: a fraction becomes a percent
        /// with 100.</summary>
        public Dictionary<string, double> AliasFactors { get; } = new Dictionary<string, double>(StringComparer.Ordinal);

        /// <summary>Old key to the further canonical keys that take its value too (one old switch that became two).</summary>
        public Dictionary<string, List<string>> AliasesShared { get; } = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        public SettingsStore(string path, JObject defaults)
        {
            Path = path ?? throw new ArgumentNullException(nameof(path));
            Defaults = defaults ?? new JObject();
        }

        public SettingsStore WithAliases(IDictionary<string, string> aliases, IDictionary<string, double> factors = null, IDictionary<string, List<string>> shared = null)
        {
            if (aliases != null)
                foreach (var pair in aliases)
                    Aliases[pair.Key] = pair.Value;
            if (factors != null)
                foreach (var pair in factors)
                    AliasFactors[pair.Key] = pair.Value;
            if (shared != null)
                foreach (var pair in shared)
                    AliasesShared[pair.Key] = new List<string>(pair.Value);
            return this;
        }

        /// <summary>Throws IOException/UnauthorizedAccessException when the file cannot be opened, so the caller
        /// leaves it alone; falls back to the backup, then the defaults, when its contents are damaged.</summary>
        public SettingsLoad Load()
        {
            var result = new SettingsLoad();
            try
            {
                result.Settings = LoadFile(Path);
                return result;
            }
            catch (InvalidDataException ex)
            {
                result.Damaged = ex.Message;
                KeepDamagedCopy();
                var backup = LoadBackup();
                result.FromBackup = backup != null;
                result.Settings = backup ?? Merged(new JObject());
                return result;
            }
        }

        /// <summary>Never throws; a damaged file falls back to the backup, then the defaults.</summary>
        public JObject LoadOrDefaults(IHostLog log, string prefix)
        {
            try { return LoadFile(Path); }
            catch (Exception ex)
            {
                log?.Warn(prefix + " " + System.IO.Path.GetFileName(Path) + " could not be read: " + ex.Message);
                return LoadBackup() ?? Merged(new JObject());
            }
        }

        public JObject LoadBackup()
        {
            try { return File.Exists(BackupPath) ? LoadFile(BackupPath) : null; }
            catch (Exception) { return null; }
        }

        private JObject LoadFile(string path)
        {
            if (!File.Exists(path))
                return Merged(new JObject());
            string json = ReadWithRetry(path);
            JObject parsed;
            try
            {
                using (var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None })
                    parsed = JObject.Load(reader);
            }
            catch (Exception ex)
            {
                // Told apart from an IOException on purpose: one means the file is unreadable and nothing should be
                // touched, the other that its contents are rubbish and the window can fall back to the backup.
                throw new InvalidDataException(System.IO.Path.GetFileName(path) + ": " + ex.Message, ex);
            }
            return Merged(parsed);
        }

        /// <summary>The defaults with the file's values on top. Keys the defaults do not know are kept, so a
        /// setting the window has no field for survives a save.</summary>
        public JObject Merged(JObject fromFile) => Merged(fromFile, null);

        /// <summary>The same, and the keys that got their value from the file (through an old key too) go into
        /// <paramref name="provided"/>: what an import brings.</summary>
        public JObject Merged(JObject fromFile, ISet<string> provided)
        {
            var merged = (JObject)Defaults.DeepClone();
            var moved = new HashSet<string>(StringComparer.Ordinal);
            fromFile = (JObject)fromFile.DeepClone();
            // An alias with a dot names a value inside an object of the old file ("PlatformEmotes.twitch"): it is
            // moved out, to every key that takes it, and the object is dropped once it is empty. The old value goes
            // also when the canonical key is in the file already, so it does not linger there forever.
            foreach (var pair in Aliases)
            {
                int dot = pair.Key.IndexOf('.');
                if (dot <= 0)
                    continue;
                string outer = pair.Key.Substring(0, dot), name = pair.Key.Substring(dot + 1);
                if (!(fromFile[outer] is JObject holder) || !(holder[name] is JToken inner))
                    continue;
                double factor = AliasFactors.TryGetValue(pair.Key, out double f) ? f : 1.0;
                var targets = new List<string> { pair.Value };
                if (AliasesShared.TryGetValue(pair.Key, out var also))
                    targets.AddRange(also);
                foreach (var target in targets)
                    if (fromFile[target] == null && !moved.Contains(target))
                    {
                        merged[target] = Convert(inner, factor, Defaults[target]);
                        moved.Add(target);
                    }
                holder.Remove(name);
                if (!holder.HasValues && Defaults.Property(outer) == null)
                    fromFile.Remove(outer);
            }
            foreach (var property in fromFile.Properties())
            {
                // An old key that is a setting of its own again is that setting's value, not one to move.
                if (Aliases.TryGetValue(property.Name, out string canonical) && Defaults.Property(property.Name) == null)
                {
                    double factor = AliasFactors.TryGetValue(property.Name, out double f2) ? f2 : 1.0;
                    // The old key may feed further settings too (one switch that became two).
                    if (AliasesShared.TryGetValue(property.Name, out var more))
                        foreach (var other in more)
                            if (fromFile[other] == null && !moved.Contains(other))
                            {
                                merged[other] = Convert(property.Value, factor, Defaults[other]);
                                moved.Add(other);
                            }
                    // The canonical key in the file wins over an old one left behind.
                    if (fromFile[canonical] != null || moved.Contains(canonical))
                        continue;
                    merged[canonical] = Convert(property.Value, factor, Defaults[canonical]);
                    moved.Add(canonical);
                    continue;
                }
                merged[property.Name] = property.Value?.DeepClone();
                provided?.Add(property.Name);
            }
            provided?.UnionWith(moved);
            return merged;
        }

        /// <summary>"Reset all": a copy of the settings with the default of every setting the window shows. Keys it
        /// has no field for (a game's own state in the file), hidden, transient and read-only settings keep their
        /// values. With <paramref name="shown"/> (the keys the window has a field for), so do settings on a page the
        /// user hid or that no page shows.</summary>
        public static JObject ResetShown(JObject current, JObject defaults, IEnumerable<Setting> settings, ISet<string> shown = null)
        {
            var fresh = (JObject)(current ?? new JObject()).DeepClone();
            foreach (var setting in settings)
                if (!setting.Hidden && !setting.Transient && !setting.ReadOnly && defaults.Property(setting.Key) != null && (shown == null || shown.Contains(setting.Key)))
                    fresh[setting.Key] = defaults[setting.Key].DeepClone();
            return fresh;
        }

        private static JToken Convert(JToken value, double factor, JToken shape)
        {
            if (value == null) return JValue.CreateNull();
            if (factor != 1.0 && (value.Type == JTokenType.Integer || value.Type == JTokenType.Float))
            {
                double scaled = System.Convert.ToDouble(((JValue)value).Value, System.Globalization.CultureInfo.InvariantCulture) * factor;
                if (shape?.Type == JTokenType.Integer && Math.Abs(scaled - Math.Round(scaled)) < 1e-9)
                    return (long)Math.Round(scaled);
                return scaled;
            }
            return value.DeepClone();
        }

        public void Save(JObject settings)
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
            WriteAllTextAtomic(Path, settings.ToString(Formatting.Indented));
        }

        public void KeepDamagedCopy()
        {
            try
            {
                if (File.Exists(Path))
                    File.Copy(Path, BrokenPath, true);
            }
            catch (Exception) { }
        }

        private static string ReadWithRetry(string path)
        {
            for (int attempt = 1; ; attempt++)
            {
                try { return File.ReadAllText(path); }
                catch (IOException) when (attempt < 5) { Thread.Sleep(40); }
            }
        }

        public static void WriteAllTextAtomic(string path, string text)
        {
            string temp = path + ".tmp";
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    File.WriteAllText(temp, text);
                    if (File.Exists(path))
                        File.Replace(temp, path, IsReadable(path) ? path + ".bak" : null);
                    else
                        File.Move(temp, path);
                    return;
                }
                catch (IOException) when (attempt < 5)
                {
                    Thread.Sleep(40);
                }
            }
        }

        public static bool IsReadable(string path)
        {
            try
            {
                // Any JSON: a sidecar list file is an array, and it deserves its .bak as much as the settings file.
                JToken.Parse(File.ReadAllText(path));
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
