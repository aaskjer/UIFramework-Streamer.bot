using System;
using System.IO;
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

        public bool AutoUpdate
        {
            get => !(Root["autoUpdate"]?.Type == JTokenType.Boolean) || (bool)Root["autoUpdate"];
            set => Root["autoUpdate"] = value;
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
    }
}
