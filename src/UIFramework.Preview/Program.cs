using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Newtonsoft.Json.Linq;

namespace UIFramework.Preview
{
    /// <summary>
    /// A host without Streamer.bot: a Blackjack-shaped spec, a settings file in the temp folder, console logging.
    /// With -shots the window is rendered off screen once per page and written as PNG, then closed; -settle <ms>
    /// gives each page that long for its emote pictures to arrive before the shot.
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
            int settle = int.TryParse(Arg(args, "-settle"), out int ms) ? ms : 0;
            Screenshots.Pick = int.TryParse(Arg(args, "-pick"), out int row) ? row : 0;

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
            if (schemaFile != null)
            {
                // The schema-driven path a stub takes: the host's describe answer, with the spec JSON of a stub
                // that carries one, or the window the schema declares (-schema alone).
                var schema = Schema.Parse(File.ReadAllText(schemaFile));
                spec = Entry.SpecFor(specFile != null ? File.ReadAllText(specFile) : null, schema, settings);
                Entry.Prepare(spec, schema);
                hooks.Answers = spec.Answers;
                Console.WriteLine($"resolved {spec.Pages.Count} pages, {spec.ResolvedSettings.Count} settings, {spec.Messages.Count} messages, {spec.Aliases.Count} aliases");
            }
            else
                spec = SampleSpec.Blackjack(settings);
            if (Array.Exists(args, a => a.Equals("-customize", StringComparison.OrdinalIgnoreCase)))
                spec.Customizing = true;

            if (shots != null)
                return Screenshots.Take(spec, log, hooks, shots, page, settle) ? 0 : 1;

            // -windowshots <folder>: the whole window per page (rail, header, footer, the page at its full height),
            // -pages a,b,c to choose them, -names id=file,... for the file names, -width for the window's width.
            string windowShots = Arg(args, "-windowshots");
            if (windowShots != null)
            {
                if (double.TryParse(Arg(args, "-width"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double width))
                    spec.Width = width;
                // -tour: the window's tour, one shot per stop (tour-01.png, ...), instead of the pages.
                var request = new WindowCapture { Folder = windowShots, SettleMs = settle > 0 ? settle : 1500, Pick = Screenshots.Pick,
                    Tour = Array.Exists(args, a => a.Equals("-tour", StringComparison.OrdinalIgnoreCase)),
                    // -problems: the window with its red "i", then with the problems panel open.
                    Problems = Array.Exists(args, a => a.Equals("-problems", StringComparison.OrdinalIgnoreCase)) };
                string only = Arg(args, "-pages");
                if (only != null) request.Only = new HashSet<string>(only.Split(',').Select(p => p.Trim()), StringComparer.Ordinal);
                string names = Arg(args, "-names");
                if (names != null)
                    request.Names = names.Split(',').Select(n => n.Split('=')).Where(p => p.Length == 2).ToDictionary(p => p[0].Trim(), p => p[1].Trim(), StringComparer.Ordinal);
                Shell.Capture = request;
                Entry.Show(spec, log, hooks, () => { });
                foreach (var file in request.Written)
                    Console.WriteLine("wrote " + file);
                return request.Written.Count > 0 ? 0 : 1;
            }

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

        /// <summary>The spec's sample answers, so a panel in the preview shows what the host would say.</summary>
        public Dictionary<string, string> Answers = new Dictionary<string, string>(StringComparer.Ordinal);

        public override string Test(string name, JObject settings) =>
            Answers.TryGetValue(name, out var answer) ? answer : name + ": the preview would run this now.";

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
        /// <summary>-pick n: the n-th row of every grid is picked before the shot (a list table shows its edit panel).</summary>
        public static int Pick;

        public static bool Take(WindowSpec spec, IHostLog log, IHostHooks hooks, string folder, string onlyPage, int settleMs = 0)
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
                // What the shell does once every page stands: requirements, live fields, pills. Here per page, so
                // a greyed-out or hidden field shows as it would in the window.
                foreach (var wire in ctx.AfterBuild)
                {
                    try { wire(); }
                    catch (Exception ex) { Console.WriteLine("wire " + pageSpec.Id + ": " + ex.Message); }
                }
                ctx.AfterBuild.Clear();
                window.Content = new System.Windows.Controls.Border { Background = new SolidColorBrush(Theme.ContentBg), Child = page };
                window.Show();
                parts.Refresh(pageSpec.Id);
                window.UpdateLayout();
                Settle(settleMs);
                window.UpdateLayout();
                if (Pick > 0)
                {
                    foreach (var grid in Descendants(window).OfType<System.Windows.Controls.DataGrid>())
                        if (grid.Items.Count >= Pick) grid.SelectedIndex = Pick - 1;
                    window.UpdateLayout();
                }
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

        private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
        {
            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                yield return child;
                foreach (var d in Descendants(child)) yield return d;
            }
        }

        /// <summary>Runs the dispatcher for a while, so what arrives on other threads (emote pictures) is drawn.</summary>
        private static void Settle(int ms)
        {
            if (ms <= 0)
                return;
            var frame = new DispatcherFrame();
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
            timer.Tick += (sender, e) => { timer.Stop(); frame.Continue = false; };
            timer.Start();
            Dispatcher.PushFrame(frame);
        }
    }

    /// <summary>PageContext has internal setters; the preview builds one through the framework's own factory.</summary>
    internal sealed class PageContextBuilder
    {
        public PageContext Build(WindowSpec spec, JObject settings, WindowParts parts, IHostLog log, IHostHooks hooks)
            => Entry.ContextFor(spec, settings, parts, log, hooks);
    }
}
