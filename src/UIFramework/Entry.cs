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
