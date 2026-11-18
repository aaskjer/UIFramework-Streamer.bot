using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json.Linq;

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
        /// The stub's entry point: the schema JSON the host answered describe with, the settings file and the host
        /// object (see <see cref="ReflectionHost"/>). Blocks until the window closes. <paramref name="specJson"/> was
        /// a window the settings action carried itself; every stub finds this method by its five parameters, so it
        /// stays, but it is no longer read: the window is the one the schema declares.
        /// </summary>
        public static bool Show(string specJson, string schemaJson, string settingsPath, object host, Action pump = null)
        {
            string stubVersion = null;
            try
            {
                var stub = host?.GetType().DeclaringType?.GetField("StubVersion", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                stubVersion = stub?.GetRawConstantValue() as string ?? stub?.GetValue(null) as string;
            }
            catch (Exception) { }
            // A settings action older than this DLL works with gets no window: latest.json's minHostStub kept such a
            // DLL from being downloaded by it, but one another game had installed or loaded was used all the same.
            string refusal = StubRefusal(stubVersion, MinHostStub);
            if (refusal != null)
                throw new InvalidOperationException(refusal);
            var bridge = new ReflectionHost(host);
            var schema = Schema.Parse(schemaJson);
            var spec = SpecFor(schema, settingsPath);
            spec.HostBuildsPages = false;   // a settings action hands over a schema, never a page of its own
            if (!string.IsNullOrWhiteSpace(specJson) && SpecJson.Root(specJson).Count > 0)
                spec.ReadProblems.Add("The settings action passed a window of its own (a spec). That is no longer read: the window is the one the project declares in UiSchema.Window.");
            // A window that declares no log prefix writes with the settings action's, so a game's lines read the same
            // from the first ("the window cannot open") to the last.
            if (schema.Window?["logPrefix"] == null && !string.IsNullOrWhiteSpace(bridge.HostLogPrefix()))
                spec.LogPrefix = bridge.HostLogPrefix();
            // For the update notice in chat: this game and its version are in use, and so is its settings action.
            try
            {
                UpdateNotices.Register(schema.Host, spec.Title, schema.Version, (string)schema.Window?["releases"], settingsPath, stubVersion,
                    schema.Window?["updates"]?.ToString(Newtonsoft.Json.Formatting.None), spec.Language);
            }
            catch (Exception) { }
            return Show(spec, schema, bridge, bridge, pump);
        }

        /// <summary>The oldest settings action (its StubVersion) this DLL works with, from the product version the
        /// build wrote ("1.8.4+stub.0.1.0", MinHostStub in UIFramework.csproj); null when the build named none.</summary>
        public static string MinHostStub
        {
            get
            {
                string product = typeof(Entry).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "";
                int at = product.IndexOf("+stub.", StringComparison.Ordinal);
                return at < 0 ? null : product.Substring(at + 6);
            }
        }

        /// <summary>What a settings action older than <paramref name="minHostStub"/> is told instead of getting a
        /// window, or null when it fits. A stub that names no version (the preview, a test) and a DLL that asks for
        /// none always fit; missing parts of a version count as 0, as in the stub.</summary>
        public static string StubRefusal(string stubVersion, string minHostStub)
        {
            Version has = Web.ParseVersion(stubVersion), needs = Web.ParseVersion(minHostStub);
            if (has == null || needs == null)
                return null;
            Version Full(Version v) => new Version(v.Major, v.Minor, Math.Max(0, v.Build), Math.Max(0, v.Revision));
            if (Full(has) >= Full(needs))
                return null;
            return "UIFramework " + FrameworkVersion + " needs a newer settings action (stub " + minHostStub + ", this game's is " + stubVersion +
                "). Update the game, or remove this version's folder under <Streamer.bot>\\UIFramework\\lib and restart Streamer.bot, so that an older UIFramework.dll is used.";
        }

        /// <summary>The window the schema declares; one without a title is called after the host.</summary>
        public static WindowSpec SpecFor(Schema schema, string settingsPath)
        {
            var root = schema.Window ?? new JObject();
            var spec = SpecJson.Parse(root, settingsPath);
            if (root["title"] == null && !string.IsNullOrWhiteSpace(schema.Host))
                spec.Title = Conventions.Humanize(schema.Host);
            if (root["logPrefix"] == null)
                spec.LogPrefix = "[" + spec.Title + "/Settings]";
            return spec;
        }

        /// <summary>Resolves the schema into the spec's pages, then opens the window.</summary>
        public static bool Show(WindowSpec spec, Schema schema, IHostLog log, IHostHooks hooks, Action pump = null)
        {
            // Known before the first resolve: without globals a setting kept in one is shown locked.
            spec.HostHasGlobals = hooks != null && hooks.HasGlobals;
            Prepare(spec, schema);
            return Shell.Run(spec, log, hooks, pump);
        }

        /// <summary>Resolve without opening: fills pages, defaults, messages and aliases on the spec.</summary>
        public static void Prepare(WindowSpec spec, Schema schema)
        {
            var layout = !string.IsNullOrEmpty(spec.SettingsPath) ? LayoutFile.Load(spec.SettingsPath) : new LayoutFile("layout");
            // Every language there are texts for: the conventions' (in the DLL or beside Streamer.bot) and the game's
            // own. Where the game brought none, its own wording stays English and the rest is translated.
            // A code stands for the language it names, spelt as that language is here: a game's Lang("pt-br"), or
            // Lang("pt") where the conventions have only pt-BR, is pt-BR, one entry in the picker, with the game's words
            // over the conventions' pt-BR. Two codes of the game's own that differ only in case are one language.
            var available = Conventions.Available();
            spec.Languages = available.ToList();
            string Offered(string code) => Conventions.Match(code, available) ?? spec.Languages.FirstOrDefault(c => string.Equals(c, code, StringComparison.OrdinalIgnoreCase));
            if (schema.Lang != null)
                foreach (var code in schema.Lang.Properties().Select(p => p.Name))
                    if (Offered(code) == null) spec.Languages.Add(code);
            if (!string.IsNullOrEmpty(spec.Language))
            {
                spec.Language = Conventions.Match(spec.Language, spec.Languages) ?? spec.Language;
                if (!spec.Languages.Contains(spec.Language))
                    spec.Languages.Add(spec.Language);
            }
            // The layout file's code likewise ("pt-br", "pt", "de-AT"); one that stands for none of them is English.
            string language = !string.IsNullOrEmpty(spec.Language) ? spec.Language : Conventions.Match(layout.Language, spec.Languages) ?? "en";
            // English is the DLL's, loaded once, unless UIFramework\lang has an en.json correcting it: that is read at
            // every open, as any other language's file is.
            var conventions = language == "en" && !System.IO.File.Exists(System.IO.Path.Combine(Conventions.ExternalFolder, "en.json"))
                ? Conventions.Default : Conventions.Load(language);
            if (language != "en" && schema.Lang != null)
                // The game's own translations over the conventions', its code in this very spelling last.
                foreach (var mine in schema.Lang.Properties().Where(p => p.Value is JObject && Offered(p.Name) == language).OrderBy(p => p.Name == language))
                {
                    conventions.Lang.Overlay((JObject)mine.Value, own: true);
                    conventions.Lang.Code = language;
                }
            spec.Schema = schema;
            spec.Conventions = conventions;
            spec.Layout = layout;
            Text.Use(conventions.Lang);
            Resolve.Apply(spec, schema, conventions, spec.Locked ? null : layout);
            if (conventions.LoadProblem != null)
                spec.Problems.Add(conventions.LoadProblem);
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
