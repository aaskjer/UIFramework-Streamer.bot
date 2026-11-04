using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace UIFramework
{
    /// <summary>
    /// The framework's knowledge of well-known keys, standard pages and placeholder texts, read from
    /// conventions/keys.json and conventions/lang/*.json, which are embedded into the DLL at build time. A host
    /// that declares a known key gets page, kind, label and help from here without saying anything.
    /// </summary>
    public sealed class Conventions
    {
        public readonly Dictionary<string, PageConvention> Pages = new Dictionary<string, PageConvention>(StringComparer.Ordinal);
        public readonly Dictionary<string, Setting> Keys = new Dictionary<string, Setting>(StringComparer.Ordinal);
        public readonly Dictionary<string, Setting> FrameworkKeys = new Dictionary<string, Setting>(StringComparer.Ordinal);
        public readonly List<string> KnownPlaceholders = new List<string>();
        public Lang Lang { get; private set; } = new Lang();

        private static Conventions _default;

        /// <summary>The embedded conventions with the English texts. Loaded once.</summary>
        public static Conventions Default
        {
            get
            {
                if (_default == null)
                    _default = Load("en");
                return _default;
            }
        }

        public static Conventions Load(string languageCode)
        {
            var conventions = Parse(JObject.Parse(ReadEmbedded("conventions.keys.json")));
            conventions.Lang = Lang.Parse(JObject.Parse(ReadEmbedded("conventions.lang.en.json")));
            if (!string.IsNullOrEmpty(languageCode) && languageCode != "en")
            {
                string other = TryReadEmbedded("conventions.lang." + languageCode + ".json");
                if (other != null)
                    conventions.Lang.Overlay(JObject.Parse(other));
            }
            return conventions;
        }

        public static Conventions Parse(JObject root)
        {
            var conventions = new Conventions();
            if (root["pages"] is JObject pages)
                foreach (var property in pages.Properties().Where(p => !p.Name.StartsWith("$")))
                    conventions.Pages[property.Name] = new PageConvention
                    {
                        Id = property.Name,
                        Order = (int?)property.Value["order"] ?? 50,
                        Icon = (string)property.Value["icon"]
                    };
            if (root["keys"] is JObject keys)
                foreach (var property in keys.Properties().Where(p => !p.Name.StartsWith("$")))
                    conventions.Keys[property.Name] = ParseKey(property.Name, (JObject)property.Value);
            if (root["framework"] is JObject framework)
                foreach (var property in framework.Properties().Where(p => !p.Name.StartsWith("$")))
                    conventions.FrameworkKeys[property.Name] = ParseKey(property.Name, (JObject)property.Value);
            if (root["placeholders"]?["known"] is JArray known)
                conventions.KnownPlaceholders.AddRange(known.Select(k => k.ToString()));
            return conventions;
        }

        private static Setting ParseKey(string key, JObject item)
        {
            var copy = (JObject)item.DeepClone();
            copy["key"] = key;
            return Setting.Parse(copy);
        }

        /// <summary>Alias to canonical key over every known key.</summary>
        public Dictionary<string, string> Aliases()
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in Keys)
                foreach (var alias in pair.Value.Aliases)
                    if (!map.ContainsKey(alias))
                        map[alias] = pair.Key;
            return map;
        }

        /// <summary>Fills what the host left out from the convention for the key, then from the default.</summary>
        public Setting Complete(Setting declared)
        {
            var setting = declared.Clone();
            if (Keys.TryGetValue(setting.Key, out var known))
            {
                setting.Kind = setting.Kind ?? known.Kind;
                setting.Page = setting.Page ?? known.Page;
                setting.Min = setting.Min ?? known.Min;
                setting.Max = setting.Max ?? known.Max;
                setting.Step = setting.Step ?? known.Step;
                setting.Unit = setting.Unit ?? known.Unit;
                setting.Factor = setting.Factor ?? known.Factor;
                setting.Options = setting.Options ?? known.Options;
                setting.Placeholders = setting.Placeholders ?? known.Placeholders;
                setting.UnlimitedAtZero = setting.UnlimitedAtZero || known.UnlimitedAtZero;
                if (setting.Default == null || setting.Default.Type == JTokenType.Null)
                    setting.Default = known.Default?.DeepClone();
                foreach (var alias in known.Aliases)
                    if (!setting.Aliases.Contains(alias))
                        setting.Aliases.Add(alias);
                foreach (var factor in known.AliasFactors)
                    if (!setting.AliasFactors.ContainsKey(factor.Key))
                        setting.AliasFactors[factor.Key] = factor.Value;
                var text = Lang.Key(setting.Key);
                setting.Label = setting.Label ?? text.Label;
                setting.Help = setting.Help ?? text.Help;
            }
            if (setting.Kind == null)
                setting.Kind = Setting.KindOf(setting.Default);
            if (setting.Kind == "template" && setting.Page == null)
                setting.Page = "messages";
            setting.Page = setting.Page ?? "game";
            setting.Label = setting.Label ?? Humanize(setting.Key);
            return setting;
        }

        /// <summary>"maxGamesPerHour" to "Max games per hour".</summary>
        public static string Humanize(string key)
        {
            if (string.IsNullOrEmpty(key)) return "";
            var chars = new List<char>();
            for (int i = 0; i < key.Length; i++)
            {
                char c = key[i];
                if (i > 0 && char.IsUpper(c) && !char.IsUpper(key[i - 1]))
                {
                    chars.Add(' ');
                    chars.Add(char.ToLowerInvariant(c));
                }
                else if (c == '_' || c == '-')
                    chars.Add(' ');
                else
                    chars.Add(i == 0 ? char.ToUpperInvariant(c) : c);
            }
            return new string(chars.ToArray());
        }

        private static string ReadEmbedded(string suffix) =>
            TryReadEmbedded(suffix) ?? throw new FileNotFoundException("Embedded resource missing: " + suffix);

        private static string TryReadEmbedded(string suffix)
        {
            var assembly = typeof(Conventions).Assembly;
            string name = assembly.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
            if (name == null)
                return null;
            using (var stream = assembly.GetManifestResourceStream(name))
            using (var reader = new StreamReader(stream))
                return reader.ReadToEnd();
        }
    }

    public sealed class PageConvention
    {
        public string Id;
        public int Order;
        public string Icon;
    }

    /// <summary>Labels and help texts: pages, keys, framework keys, placeholders. English as the base, another
    /// language laid over it.</summary>
    public sealed class Lang
    {
        public readonly Dictionary<string, string> Pages = new Dictionary<string, string>(StringComparer.Ordinal);
        public readonly Dictionary<string, (string Label, string Help)> Keys = new Dictionary<string, (string, string)>(StringComparer.Ordinal);
        public readonly Dictionary<string, (string Label, string Help)> Framework = new Dictionary<string, (string, string)>(StringComparer.Ordinal);
        public readonly Dictionary<string, string> Placeholders = new Dictionary<string, string>(StringComparer.Ordinal);
        /// <summary>The window's own texts by key (<see cref="Text"/>).</summary>
        public readonly Dictionary<string, string> Ui = new Dictionary<string, string>(StringComparer.Ordinal);

        public static Lang Parse(JObject root)
        {
            var lang = new Lang();
            lang.Overlay(root);
            return lang;
        }

        public void Overlay(JObject root)
        {
            if (root["pages"] is JObject pages)
                foreach (var p in pages.Properties())
                    Pages[p.Name] = p.Value.ToString();
            Read(root["keys"] as JObject, Keys);
            Read(root["framework"] as JObject, Framework);
            if (root["placeholders"] is JObject placeholders)
                foreach (var p in placeholders.Properties())
                    Placeholders[p.Name] = p.Value.ToString();
            if (root["ui"] is JObject ui)
                foreach (var p in ui.Properties())
                    Ui[p.Name] = p.Value.ToString();
        }

        private static void Read(JObject section, Dictionary<string, (string, string)> into)
        {
            if (section == null) return;
            foreach (var p in section.Properties())
            {
                if (p.Value is JObject o)
                    into[p.Name] = ((string)o["label"], (string)o["help"]);
                else
                    into[p.Name] = (p.Value.ToString(), null);
            }
        }

        public (string Label, string Help) Key(string key) => Keys.TryGetValue(key, out var text) ? text : (null, null);
        public string Page(string id) => Pages.TryGetValue(id, out var title) ? title : null;
        public string Placeholder(string name) => Placeholders.TryGetValue(name, out var help) ? help : null;
    }
}
