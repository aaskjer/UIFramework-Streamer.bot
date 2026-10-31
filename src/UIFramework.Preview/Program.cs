using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Newtonsoft.Json.Linq;

namespace UIFramework.Preview
{
    /// <summary>
    /// A host without Streamer.bot: a Blackjack-shaped spec, a settings file in the temp folder, console logging.
    /// With -shots the window is rendered off screen once per page and written as PNG, then closed.
    /// </summary>
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            string settings = Arg(args, "-settings") ?? Path.Combine(Path.GetTempPath(), "UIFramework.Preview", "Blackjack Game", "BJG-Settings.json");
            string shots = Arg(args, "-shots");
            string theme = Arg(args, "-theme");
            string page = Arg(args, "-page");

            Directory.CreateDirectory(Path.GetDirectoryName(settings));
            if (theme != null)
            {
                var layout = LayoutFile.Load(settings);
                layout.Dark = theme.Equals("dark", StringComparison.OrdinalIgnoreCase);
                layout.Save();
            }

            var log = new ConsoleLog();
            var hooks = new SampleHooks(settings);
            WindowSpec spec;
            string specFile = Arg(args, "-spec"), schemaFile = Arg(args, "-schema");
            if (specFile != null && schemaFile != null)
            {
                // The schema-driven path a stub takes: spec JSON plus the host's describe answer.
                spec = SpecJson.Parse(File.ReadAllText(specFile), settings);
                Entry.Prepare(spec, Schema.Parse(File.ReadAllText(schemaFile)));
                Console.WriteLine($"resolved {spec.Pages.Count} pages, {spec.ResolvedSettings.Count} settings, {spec.Messages.Count} messages, {spec.Aliases.Count} aliases");
            }
            else
                spec = SampleSpec.Blackjack(settings);

            if (shots != null)
                return Screenshots.Take(spec, log, hooks, shots, page) ? 0 : 1;

            Entry.Show(spec, log, hooks, () => hooks.Pump(log));
            return 0;
        }

        private static string Arg(string[] args, string name)
        {
            int at = Array.FindIndex(args, a => a.Equals(name, StringComparison.OrdinalIgnoreCase));
            return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
        }
    }

    internal sealed class ConsoleLog : IHostLog
    {
        public void Info(string message) => Console.WriteLine("INFO  " + message);
        public void Warn(string message) => Console.WriteLine("WARN  " + message);
        public void Error(string message) => Console.WriteLine("ERROR " + message);
    }

    /// <summary>Plays the stub: queues what a save would hand to Streamer.bot and applies it from the pump.</summary>
    internal sealed class SampleHooks : NoHooks
    {
        private readonly string _logFolder;
        private JObject _pending;

        public SampleHooks(string settingsPath)
        {
            _logFolder = Path.Combine(Path.GetDirectoryName(settingsPath), "Logs");
        }

        public override void OnSaved(JObject settings) => Interlocked.Exchange(ref _pending, settings);

        public override string Test(string name, JObject settings) => name + ": the preview would run this now.";

        public override string ReadTable(string key) =>
            "[{\"rank\":\"1\",\"user\":\"alice\",\"net\":\"12,500\"},{\"rank\":\"2\",\"user\":\"bob\",\"net\":\"3,000\"}]";

        public void Pump(IHostLog log)
        {
            var pending = Interlocked.Exchange(ref _pending, null);
            if (pending != null)
                log.Info("[Preview] would set cooldowns: global=" + pending["GlobalCooldown"] + " user=" + pending["UserCooldown"]);
        }

        public override IEnumerable<LogEntry> ReadLog()
        {
            if (!Directory.Exists(_logFolder))
                return SampleLog();
            return Directory.GetFiles(_logFolder, "*.txt").SelectMany(File.ReadLines).Where(l => !string.IsNullOrWhiteSpace(l)).Select(LogEntry.Parse);
        }

        private static IEnumerable<LogEntry> SampleLog()
        {
            var now = DateTime.Now;
            string[] lines =
            {
                $"[{now.AddMinutes(-3):yyyy-MM-dd HH:mm:ss}] WIN | User: alice | Platform: twitch | Trigger: !blackjack | Bet: 500 | Before: 12,000 | Change: +500 | After: 12,500 | Currency: Points",
                $"[{now.AddMinutes(-9):yyyy-MM-dd HH:mm:ss}] BUST | User: bob | Platform: youtube | Trigger: !bj | Bet: 1,000 | Before: 4,000 | Change: -1,000 | After: 3,000 | Currency: Points",
                $"[{now.AddHours(-26):yyyy-MM-dd HH:mm:ss}] BLACKJACK | User: carol | Platform: kick | Trigger: !blackjack | Bet: 200 | Before: 900 | Change: +300 | After: 1,200 | Currency: Points",
                $"[{now.AddHours(-27):yyyy-MM-dd HH:mm:ss}] PUSH | User: dave | Platform: twitch | Trigger: !blackjack | Bet: 50 | Before: 100 | Change: 0 | After: 100 | Currency: Points",
            };
            return lines.Select(LogEntry.Parse);
        }
    }

    internal static class Screenshots
    {
        public static bool Take(WindowSpec spec, IHostLog log, IHostHooks hooks, string folder, string onlyPage)
        {
            Directory.CreateDirectory(folder);
            var layout = LayoutFile.Load(spec.SettingsPath);
            layout.ApplyTheme();
            var store = new SettingsStore(spec.SettingsPath, spec.Defaults);
            var settings = store.LoadOrDefaults(log, spec.LogPrefix);
            var parts = new WindowParts();
            var ctx = new PageContextBuilder().Build(spec, settings, parts, log, hooks);

            var window = new Window { Width = spec.Width, Height = spec.Height, Background = new SolidColorBrush(Theme.ContentBg), WindowStartupLocation = WindowStartupLocation.Manual, Left = -10000, Top = -10000, ShowInTaskbar = false };
            bool ok = true;
            foreach (var pageSpec in spec.Pages)
            {
                if (onlyPage != null && !pageSpec.Id.Equals(onlyPage, StringComparison.OrdinalIgnoreCase))
                    continue;
                UIElement page;
                try { page = pageSpec.Build != null ? pageSpec.Build(ctx) : hooks.BuildPage(pageSpec.Id, ctx); }
                catch (Exception ex) { Console.WriteLine("FAIL " + pageSpec.Id + ": " + ex); ok = false; continue; }
                if (page == null) { Console.WriteLine("skip " + pageSpec.Id + " (no content)"); continue; }
                window.Content = new System.Windows.Controls.Border { Background = new SolidColorBrush(Theme.ContentBg), Child = page };
                window.Show();
                parts.Refresh(pageSpec.Id);
                window.UpdateLayout();
                var bitmap = new RenderTargetBitmap((int)window.Width, (int)window.Height, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render((Visual)window.Content);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                string file = Path.Combine(folder, pageSpec.Id + (Theme.IsDark ? "-dark" : "-light") + ".png");
                using (var stream = File.Create(file))
                    encoder.Save(stream);
                Console.WriteLine("wrote " + file);
                window.Hide();
            }
            window.Close();
            return ok;
        }
    }

    /// <summary>PageContext has internal setters; the preview builds one through the framework's own factory.</summary>
    internal sealed class PageContextBuilder
    {
        public PageContext Build(WindowSpec spec, JObject settings, WindowParts parts, IHostLog log, IHostHooks hooks)
            => Entry.ContextFor(spec, settings, parts, log, hooks);
    }
}
