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
        /// <summary>The links every About page ends with (keys.json's <c>about.links</c>): a text (a ui text key or
        /// plain text) and an address, where <c>{issues}</c> stands for the game's issue tracker, else <see cref="FrameworkIssues"/>.</summary>
        public readonly List<Link> AboutLinks = new List<Link>();
        /// <summary>The framework's issue tracker: where <c>{issues}</c> points for a game that names no repository.</summary>
        public string FrameworkIssues = "https://github.com/aaskjer/UIFramework-Streamer.bot/issues";

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
                {
                    conventions.Lang.Overlay(JObject.Parse(other));
                    conventions.Lang.Code = languageCode;
                }
                // A file of the same shape in UIFramework\lang beside Streamer.bot: a language the DLL does not
                // carry, or corrections to one it does, without waiting for a release. It is laid over the DLL's.
                string external = ExternalFile(languageCode);
                if (external != null && File.Exists(external))
                {
                    try
                    {
                        conventions.Lang.Overlay(JObject.Parse(File.ReadAllText(external)));
                        conventions.Lang.Code = languageCode;
                    }
                    catch (Exception ex) { conventions.LoadProblem = Path.GetFileName(external) + " could not be read: " + ex.Message; }
                }
            }
            return conventions;
        }

        /// <summary>Why a language file beside Streamer.bot was not used, or null; the window lists it with the
        /// schema's problems.</summary>
        public string LoadProblem;

        /// <summary>The folder for language files beside Streamer.bot: UIFramework\lang, next to UIFramework\lib.</summary>
        public static string ExternalFolder => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UIFramework", "lang");

        private static string ExternalFile(string code) =>
            code.All(c => char.IsLetterOrDigit(c) || c == '-') ? Path.Combine(ExternalFolder, code + ".json") : null;

        /// <summary>Every language there are texts for: English, the files the DLL carries, the files in
        /// <see cref="ExternalFolder"/>. A window offers all of them.</summary>
        public static List<string> Available()
        {
            var codes = new List<string> { "en" };
            const string prefix = "conventions.lang.";
            foreach (var name in typeof(Conventions).Assembly.GetManifestResourceNames())
            {
                int at = name.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
                if (at < 0 || !name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) continue;
                string code = name.Substring(at + prefix.Length, name.Length - at - prefix.Length - ".json".Length);
                if (code.Length > 0 && !codes.Contains(code)) codes.Add(code);
            }
            try
            {
                if (Directory.Exists(ExternalFolder))
                    foreach (var file in Directory.GetFiles(ExternalFolder, "*.json"))
                    {
                        string code = Path.GetFileNameWithoutExtension(file);
                        if (code.Length > 0 && code.All(c => char.IsLetterOrDigit(c) || c == '-') && !codes.Contains(code)) codes.Add(code);
                    }
            }
            catch (Exception) { }
            return codes;
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
            if (root["about"] is JObject about)
            {
                foreach (var link in (about["links"] as JArray ?? new JArray()).OfType<JObject>())
                    if (!string.IsNullOrWhiteSpace((string)link["url"]))
                        conventions.AboutLinks.Add(new Link((string)link["text"] ?? (string)link["url"], (string)link["url"]));
                if (!string.IsNullOrWhiteSpace((string)about["issues"]))
                    conventions.FrameworkIssues = (string)about["issues"];
            }
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
                // A key that belongs on a line with others (OBS connection, scene, source) sits there unless the game says otherwise.
                setting.Row = setting.Row ?? known.Row;
                // And one that only means something while a switch is on (the timer's intervals) is greyed out with it.
                setting.Requires = setting.Requires ?? known.Requires;
                if (setting.Default == null || setting.Default.Type == JTokenType.Null)
                    setting.Default = known.Default?.DeepClone();
                // An old name the game lists itself is read the way the game reads it: its own reader (UiSettings) knows
                // only the game's factor, so the conventions' factor on it made the window show another number than the
                // game used (an old "AddictedPayoutCut": 50 as 5000 percent).
                var listedByGame = new HashSet<string>(setting.Aliases, StringComparer.Ordinal);
                foreach (var alias in known.Aliases)
                    if (!setting.Aliases.Contains(alias))
                        setting.Aliases.Add(alias);
                foreach (var factor in known.AliasFactors)
                    if (!setting.AliasFactors.ContainsKey(factor.Key) && !listedByGame.Contains(factor.Key))
                        setting.AliasFactors[factor.Key] = factor.Value;
            }
            // The texts the conventions have for the key fill what the game left out, known key or not.
            var text = Lang.Key(setting.Key);
            setting.Label = setting.Label ?? text.Label;
            setting.Help = setting.Help ?? text.Help;
            if (setting.Kind == null)
                setting.Kind = Setting.KindOf(setting.Default);
            // A conventions "int" whose default is beyond an int (a maxBet of 5 billion) becomes a long field, so the
            // value is not cut to 2147483647 on save.
            if (setting.Kind == "int" && !Setting.FitsInt(setting.Default))
                setting.Kind = "long";
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
        /// <summary>The game's own page titles for this language (UiWindow.Lang's Pg), by page id: they come before a
        /// translation of the declared title's wording, which may mean another thing ("Board": a scoreboard in one
        /// game, the grid in another).</summary>
        public readonly Dictionary<string, string> OwnPages = new Dictionary<string, string>(StringComparer.Ordinal);
        /// <summary>The game's own key texts for this language (UiWindow.Lang's K): they replace whatever the game
        /// declared for the key, where the conventions' replace only what it left out or took from them.</summary>
        public readonly Dictionary<string, (string Label, string Help)> OwnKeys = new Dictionary<string, (string, string)>(StringComparer.Ordinal);
        /// <summary>The key texts in English, the language the games declare in: a declared label equal to one is the
        /// conventions' own wording, and another language translates it.</summary>
        public readonly Dictionary<string, (string Label, string Help)> EnglishKeys = new Dictionary<string, (string, string)>(StringComparer.Ordinal);
        public readonly Dictionary<string, (string Label, string Help)> Framework = new Dictionary<string, (string, string)>(StringComparer.Ordinal);
        public readonly Dictionary<string, string> Placeholders = new Dictionary<string, string>(StringComparer.Ordinal);
        /// <summary>The window's own texts by key (<see cref="Text"/>).</summary>
        public readonly Dictionary<string, string> Ui = new Dictionary<string, string>(StringComparer.Ordinal);
        /// <summary>Short declared texts by their English wording: section headings, pill texts, page titles,
        /// option labels, message groups. Read when <see cref="Translates"/>.</summary>
        public readonly Dictionary<string, string> Texts = new Dictionary<string, string>(StringComparer.Ordinal);
        /// <summary>The language these texts are in; "en" is the language the games declare their texts in.</summary>
        public string Code = "en";
        /// <summary>Another language than the games': what they declared is translated where these texts know it.</summary>
        public bool Translates => Code != "en";

        public static Lang Parse(JObject root)
        {
            var lang = new Lang();
            lang.Overlay(root);
            foreach (var pair in lang.Keys)
                lang.EnglishKeys[pair.Key] = pair.Value;
            return lang;
        }

        /// <summary>Lays another language's texts over these. <paramref name="own"/>: the game's own translations,
        /// whose key texts also go into <see cref="OwnKeys"/>.</summary>
        public void Overlay(JObject root, bool own = false)
        {
            if (root["pages"] is JObject pages)
                foreach (var p in pages.Properties())
                {
                    Pages[p.Name] = p.Value.ToString();
                    if (own) OwnPages[p.Name] = p.Value.ToString();
                }
            Read(root["keys"] as JObject, Keys);
            if (own)
                Read(root["keys"] as JObject, OwnKeys);
            Read(root["framework"] as JObject, Framework);
            if (root["placeholders"] is JObject placeholders)
                foreach (var p in placeholders.Properties())
                    Placeholders[p.Name] = p.Value.ToString();
            if (root["ui"] is JObject ui)
                foreach (var p in ui.Properties())
                    Ui[p.Name] = p.Value.ToString();
            if (root["texts"] is JObject texts)
                foreach (var p in texts.Properties())
                    Texts[p.Name] = p.Value.ToString();
        }

        /// <summary>The declared text in this language where the texts know it, else as declared (null stays null).</summary>
        public string Text(string declared) => declared != null && Texts.TryGetValue(declared, out var text) ? text : declared;
        /// <summary>The declared text in this language, or null where the texts do not know it.</summary>
        public string TextOrNull(string declared) => declared != null && Texts.TryGetValue(declared, out var text) ? text : null;

        /// <summary>Label and help each over what is there: a K(key, label) without help keeps the help the
        /// conventions have for the key.</summary>
        private static void Read(JObject section, Dictionary<string, (string Label, string Help)> into)
        {
            if (section == null) return;
            foreach (var p in section.Properties())
            {
                into.TryGetValue(p.Name, out var was);
                if (p.Value is JObject o)
                    into[p.Name] = ((string)o["label"] ?? was.Label, (string)o["help"] ?? was.Help);
                else
                    into[p.Name] = (p.Value.ToString(), was.Help);
            }
        }

        public (string Label, string Help) Key(string key) => Keys.TryGetValue(key, out var text) ? text : (null, null);
        public (string Label, string Help) OwnKey(string key) => OwnKeys.TryGetValue(key, out var text) ? text : (null, null);
        public (string Label, string Help) EnglishKey(string key) => EnglishKeys.TryGetValue(key, out var text) ? text : (null, null);
        public string Page(string id) => Pages.TryGetValue(id, out var title) ? title : null;
        public string OwnPage(string id) => id != null && OwnPages.TryGetValue(id, out var title) ? title : null;
        public string Placeholder(string name) => Placeholders.TryGetValue(name, out var help) ? help : null;
    }
}
