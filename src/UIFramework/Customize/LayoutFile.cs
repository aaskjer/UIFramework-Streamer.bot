using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UIFramework
{
    /// <summary>
    /// uiframework.layout.json next to the host's settings file: what the user changed about the window. Phase 1
    /// holds only the theme (dark, rainbow, accent); the page and field overrides of the customise mode come in
    /// phase 4 and live in the same file. Theme values that still sit in a host's settings file (DarkMode, Egg)
    /// are taken over once and removed there by the shell.
    /// </summary>
    public sealed class LayoutFile
    {
        public const string FileName = "uiframework.layout.json";

        public string Path { get; }
        public JObject Root { get; private set; } = new JObject();

        public LayoutFile(string settingsPath)
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(settingsPath) ?? ".", FileName);
        }

        public static LayoutFile Load(string settingsPath)
        {
            var file = new LayoutFile(settingsPath);
            try
            {
                if (File.Exists(file.Path))
                    file.Root = JObject.Parse(File.ReadAllText(file.Path));
            }
            catch (Exception)
            {
                file.Root = new JObject();
            }
            return file;
        }

        public void Save()
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
            SettingsStore.WriteAllTextAtomic(Path, Root.ToString(Formatting.Indented));
        }

        private JObject ThemeObject => Root["theme"] as JObject ?? (JObject)(Root["theme"] = new JObject());

        public bool? Dark
        {
            get => Root["theme"]?["dark"]?.Type == JTokenType.Boolean ? (bool?)(bool)Root["theme"]["dark"] : null;
            set { if (value.HasValue) ThemeObject["dark"] = value.Value; else ThemeObject.Remove("dark"); }
        }

        public bool Rainbow
        {
            get => Root["theme"]?["rainbow"]?.Type == JTokenType.Boolean && (bool)Root["theme"]["rainbow"];
            set => ThemeObject["rainbow"] = value;
        }

        public string Accent
        {
            get => Root["theme"]?["accent"]?.Type == JTokenType.String ? (string)Root["theme"]["accent"] : "";
            set { if (string.IsNullOrWhiteSpace(value)) ThemeObject.Remove("accent"); else ThemeObject["accent"] = value.Trim(); }
        }

        public string Language
        {
            get => Root["language"]?.Type == JTokenType.String ? (string)Root["language"] : null;
            set { if (string.IsNullOrWhiteSpace(value)) Root.Remove("language"); else Root["language"] = value.Trim(); }
        }

        public bool AutoUpdate
        {
            get => !(Root["autoUpdate"]?.Type == JTokenType.Boolean) || (bool)Root["autoUpdate"];
            set => Root["autoUpdate"] = value;
        }

        // The loader's note for the window's header pill: a framework release the user answered "No" to in the
        // update dialog. Set by the stub on "No", removed once the release is taken, skipped or overtaken.
        public string FrameworkAvailable { get => Text("frameworkAvailable"); set => SetText("frameworkAvailable", value); }

        private string Text(string key) => Root[key]?.Type == JTokenType.String ? (string)Root[key] : null;
        private void SetText(string key, string value) { if (string.IsNullOrWhiteSpace(value)) Root.Remove(key); else Root[key] = value.Trim(); }

        /// <summary>"0.3.0" or "v0.3.0" as a Version; null when it is neither.</summary>
        public static Version ParseVersion(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            Version v;
            return Version.TryParse(text.Trim().TrimStart('v', 'V'), out v) ? v : null;
        }

        /// <summary>Takes the theme from a host's settings file when the layout has none yet (the first open after
        /// the host moved to the framework). Returns true when the host settings changed and should be saved.</summary>
        public bool AdoptFromHost(JObject hostSettings, params string[] darkKeys)
        {
            bool changed = false;
            foreach (var key in darkKeys)
            {
                if (hostSettings[key]?.Type != JTokenType.Boolean) continue;
                if (!Dark.HasValue)
                    Dark = (bool)hostSettings[key];
                hostSettings.Remove(key);
                changed = true;
            }
            foreach (var key in new[] { "Egg", "egg", "rainbow" })
            {
                if (hostSettings[key]?.Type != JTokenType.Boolean) continue;
                hostSettings.Remove(key);
                changed = true;
            }
            return changed;
        }

        /// <summary>Puts the stored theme into <see cref="Theme"/>.</summary>
        public void ApplyTheme()
        {
            Theme.IsDark = Dark ?? true;
            Theme.AccentOverride = Colours.TryParseHex(Accent, out var accent) ? accent : (System.Windows.Media.Color?)null;
        }

        // ---- The user's layout: what the customise mode writes ------------------------------------------

        private JObject Section(string name) => Root[name] as JObject ?? (JObject)(Root[name] = new JObject());

        private JObject Entry(string section, string id, bool create)
        {
            var list = Root[section] as JObject;
            var entry = list?[id] as JObject;
            if (entry == null && create)
            {
                entry = new JObject();
                Section(section)[id] = entry;
            }
            return entry;
        }

        private void Prune(string section, string id)
        {
            var list = Root[section] as JObject;
            if (list?[id] is JObject entry && !entry.HasValues)
                list.Remove(id);
            if (list != null && !list.HasValues)
                Root.Remove(section);
        }

        public LayoutPage Page(string id)
        {
            var e = Entry("pages", id, false);
            if (e == null) return null;
            return new LayoutPage
            {
                Title = (string)e["title"],
                Icon = (string)e["icon"],
                Order = (int?)e["order"],
                Hidden = e["hidden"]?.Type == JTokenType.Boolean ? (bool?)(bool)e["hidden"] : null
            };
        }

        public LayoutField Field(string key)
        {
            var e = Entry("fields", key, false);
            if (e == null) return null;
            return new LayoutField
            {
                Page = (string)e["page"],
                Label = (string)e["label"],
                Section = (string)e["section"],
                Order = (int?)e["order"],
                Row = (string)e["row"],
                Hidden = e["hidden"]?.Type == JTokenType.Boolean ? (bool?)(bool)e["hidden"] : null
            };
        }

        /// <summary>Sets one property of a page override; null removes it. The file is not written here.</summary>
        public void SetPage(string id, string property, JToken value)
        {
            var e = Entry("pages", id, value != null);
            if (e == null) return;
            if (value == null || value.Type == JTokenType.Null) e.Remove(property); else e[property] = value;
            Prune("pages", id);
        }

        public void SetField(string key, string property, JToken value)
        {
            var e = Entry("fields", key, value != null);
            if (e == null) return;
            if (value == null || value.Type == JTokenType.Null) e.Remove(property); else e[property] = value;
            Prune("fields", key);
        }

        public void ResetPage(string id) { (Root["pages"] as JObject)?.Remove(id); Prune("pages", id); }
        public void ResetField(string key) { (Root["fields"] as JObject)?.Remove(key); Prune("fields", key); }

        /// <summary>Drops every page and field override; the theme and the update preference stay.</summary>
        public void ResetLayout()
        {
            Root.Remove("pages");
            Root.Remove("fields");
        }

        /// <summary>How many pages and fields carry an override.</summary>
        public int ChangeCount =>
            ((Root["pages"] as JObject)?.Properties().Count(p => p.Value is JObject o && o.HasValues) ?? 0) +
            ((Root["fields"] as JObject)?.Properties().Count(p => p.Value is JObject o && o.HasValues) ?? 0);

        public IEnumerable<string> OverriddenPages => (Root["pages"] as JObject)?.Properties().Select(p => p.Name) ?? Enumerable.Empty<string>();
        public IEnumerable<string> OverriddenFields => (Root["fields"] as JObject)?.Properties().Select(p => p.Name) ?? Enumerable.Empty<string>();
    }

    public sealed class LayoutPage
    {
        public string Title, Icon;
        public int? Order;
        public bool? Hidden;
    }

    public sealed class LayoutField
    {
        /// <summary>Row: the line the user put the field on, beside others with the same name; "" puts it on a line
        /// of its own, out of a row the game declared. Null leaves the game's layout.</summary>
        public string Page, Label, Section, Row;
        public int? Order;
        public bool? Hidden;
    }
}
