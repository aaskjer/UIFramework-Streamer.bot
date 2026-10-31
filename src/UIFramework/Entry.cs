using System;
using System.Reflection;

namespace UIFramework
{
    /// <summary>
    /// What a host calls. The stub loads the DLL, finds this type by name and calls <see cref="Show"/>; the preview
    /// calls it directly.
    /// </summary>
    public static class Entry
    {
        public static string FrameworkVersion
        {
            get
            {
                var version = typeof(Entry).Assembly.GetName().Version;
                return "v" + version.Major + "." + version.Minor + "." + version.Build;
            }
        }

        /// <summary>Opens the host's settings window and blocks until it closes. Returns false when a window for
        /// the same settings file was already open (it was brought to the front instead).</summary>
        public static bool Show(WindowSpec spec, IHostLog log, IHostHooks hooks, Action pump = null)
            => Shell.Run(spec, log, hooks, pump);

        /// <summary>
        /// The stub's entry point: spec JSON, the schema JSON the host answered describe with, the settings file
        /// and the host object (see <see cref="ReflectionHost"/>). Blocks until the window closes.
        /// </summary>
        public static bool Show(string specJson, string schemaJson, string settingsPath, object host, Action pump = null)
        {
            var bridge = new ReflectionHost(host);
            var spec = SpecJson.Parse(specJson, settingsPath);
            var schema = Schema.Parse(schemaJson);
            return Show(spec, schema, bridge, bridge, pump);
        }

        /// <summary>Resolves the schema into the spec's pages, then opens the window.</summary>
        public static bool Show(WindowSpec spec, Schema schema, IHostLog log, IHostHooks hooks, Action pump = null)
        {
            Prepare(spec, schema);
            return Shell.Run(spec, log, hooks, pump);
        }

        /// <summary>Resolve without opening: fills pages, defaults, messages and aliases on the spec.</summary>
        public static void Prepare(WindowSpec spec, Schema schema)
        {
            string language = spec.Language;
            if (language == null && !string.IsNullOrEmpty(spec.SettingsPath))
                language = LayoutFile.Load(spec.SettingsPath).Language;
            var conventions = string.IsNullOrEmpty(language) || language == "en" ? Conventions.Default : Conventions.Load(language);
            Resolve.Apply(spec, schema, conventions);
        }

        /// <summary>Closes the window for a settings file without asking, for the host's Dispose.</summary>
        public static void Close(string settingsPath) => Shell.Close(settingsPath);

        /// <summary>A page context outside the shell, for previews and tests that build single pages.</summary>
        public static PageContext ContextFor(WindowSpec spec, Newtonsoft.Json.Linq.JObject settings, WindowParts parts, IHostLog log, IHostHooks hooks)
        {
            var ctx = new PageContext
            {
                Spec = spec,
                Settings = settings,
                Defaults = spec.Defaults,
                Parts = parts,
                Log = log,
                Hooks = hooks ?? new NoHooks()
            };
            ctx.Fields = new Fields(parts, settings, spec.Defaults, url => Web.OpenUrl(url, log, spec.LogPrefix));
            ctx.RefreshPage = parts.Refresh;
            return ctx;
        }
    }
}
