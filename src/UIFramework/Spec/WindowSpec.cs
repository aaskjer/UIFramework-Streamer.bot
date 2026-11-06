using System;
using System.Collections.Generic;
using System.Windows;
using Newtonsoft.Json.Linq;

namespace UIFramework
{
    /// <summary>
    /// How a host's window is put together: what it is called, where its settings live, which pages it shows.
    /// Phase 1: pages are built by code (the host's or the framework's standard pages). Phase 2 adds pages built
    /// from the host's schema; this class stays the single description the shell reads.
    /// </summary>
    public sealed class WindowSpec
    {
        /// <summary>Shown in the title bar and on the About page: "Blackjack Game".</summary>
        public string Title;

        /// <summary>The host's release tag, "v16.3.1". Compared against GitHub's latest release.</summary>
        public string Version;

        /// <summary>"owner/repo" on GitHub. Gives the repository link, the releases link and the update check.</summary>
        public string GitHubRepo;

        public string LogoUrl;

        /// <summary>Prefix for every log line the framework writes for this host: "[Blackjack Game/Settings]".</summary>
        public string LogPrefix = "[UIFramework]";

        /// <summary>Full path of the host's settings file. The layout file lives next to it.</summary>
        public string SettingsPath;

        /// <summary>Every setting with its default. The window reads a missing key from here, Reset writes these.</summary>
        public JObject Defaults = new JObject();

        public double Width = 620, Height = 720, MinWidth = 560, MinHeight = 560;

        public readonly List<PageSpec> Pages = new List<PageSpec>();

        /// <summary>Pages as the spec JSON names them; <see cref="Resolve"/> turns them into <see cref="Pages"/>.</summary>
        public readonly List<SpecPage> SpecPages = new List<SpecPage>();

        /// <summary>Sections shown only while a condition holds, by their declared (English) name.</summary>
        public readonly Dictionary<string, string> SectionConditions = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>What the spec changes about single settings, by key.</summary>
        public readonly Dictionary<string, FieldOverride> Overrides = new Dictionary<string, FieldOverride>(StringComparer.Ordinal);

        /// <summary>Fixed sample values for the chat preview, by placeholder. Live ones (points name, amounts)
        /// are added by <see cref="Resolve"/>.</summary>
        public readonly Dictionary<string, string> Samples = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>Language code for the conventions' labels; null takes the layout file's, then "en".</summary>
        public string Language;

        /// <summary>Every setting after <see cref="Resolve"/>, completed and overridden.</summary>
        public readonly List<Setting> ResolvedSettings = new List<Setting>();

        /// <summary>Old key to canonical key, for the store's migration on load.</summary>
        public readonly Dictionary<string, string> Aliases = new Dictionary<string, string>(StringComparer.Ordinal);
        public readonly Dictionary<string, double> AliasFactors = new Dictionary<string, double>(StringComparer.Ordinal);
        /// <summary>An old key two or more settings share (one old switch that became two): the further canonical keys.</summary>
        public readonly Dictionary<string, List<string>> AliasesShared = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        /// <summary>Sample answers for the preview: action key to what the host would answer (text or JSON).</summary>
        public readonly Dictionary<string, string> Answers = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>The message templates for the Messages page, in picker order. Empty: no Messages page.</summary>
        public readonly List<MessageDef> Messages = new List<MessageDef>();

        /// <summary>Sample values for the chat preview, read live from the window (points name, separator).</summary>
        public Func<PageContext, Dictionary<string, string>> SampleValues;

        /// <summary>Tooltip for a placeholder chip: (message key, token) to text, or null for none.</summary>
        public Func<string, string, string> TokenHelp;

        /// <summary>The keys of the four sender switches on the Messages page. Defaults are the canonical names;
        /// a host still on its old names sets its own.</summary>
        public SenderKeys Senders = new SenderKeys();

        public AboutSpec About = new AboutSpec();
        public LogSpec Log = new LogSpec();
        public readonly List<HelpSection> Help = new List<HelpSection>();

        /// <summary>What <see cref="Resolve"/> found wrong with the schema or the spec (an unknown kind, a
        /// requirement on a key that is not there): shown in a banner and listed on the About page, so the
        /// host's author sees it without reading Streamer.bot's log.</summary>
        public readonly List<string> Problems = new List<string>();

        /// <summary>The settings behind the header pills, for the checks.</summary>
        public readonly List<string> HeaderPillKeys = new List<string>();

        /// <summary>Pills shown in the header next to the page title, redrawn when any control changes.</summary>
        public readonly List<HeaderPill> HeaderPills = new List<HeaderPill>();
        /// <summary>The pill for a detected points system, so a second resolve replaces it instead of adding one.</summary>
        public HeaderPill CurrencyPill;

        /// <summary>Combinations the window should warn about before saving. Returns the problems, or empty.</summary>
        public Func<JObject, List<string>> Warnings;

        /// <summary>Pulls saved values into range before the file is written.</summary>
        public Action<JObject> Clamp;

        /// <summary>Kept after <see cref="Entry.Prepare"/>, so the shell can resolve again after a layout change.</summary>
        public Schema Schema;
        public Conventions Conventions;
        public LayoutFile Layout;

        /// <summary>The customise mode is on; survives the reopen that every layout change asks for.</summary>
        public bool Customizing;

        /// <summary>The languages the window offers: English, and every language the game brings texts for
        /// (Lang("de") in its block, even with no texts, offers German from the conventions alone).</summary>
        public List<string> Languages = new List<string> { "en" };

        /// <summary>The game locked its layout: no customise mode, and the user's renames, moves, hidden fields and
        /// accent colour from uiframework.layout.json are not applied. Theme and language stay the user's.</summary>
        public bool Locked;

        /// <summary>The page to show after a reopen, by id.</summary>
        public string ReopenPageId;

        public string RepoUrl => string.IsNullOrEmpty(GitHubRepo) ? null : "https://github.com/" + GitHubRepo;
        public string ReleasesUrl => RepoUrl == null ? null : RepoUrl + "/releases";
    }

    public sealed class PageSpec
    {
        public string Id;
        public string Title;
        /// <summary>An emoji for the rail.</summary>
        public string Glyph;
        /// <summary>Builds the page's content. Standard pages set this to the framework's builder.</summary>
        public Func<PageContext, UIElement> Build;
        /// <summary>Docked at the bottom of the rail, like About.</summary>
        public bool Bottom;
        /// <summary>Hidden by the user's layout; shown dimmed in the customise mode only.</summary>
        public bool Hidden;

        public PageSpec() { }

        public PageSpec(string id, string title, string glyph, Func<PageContext, UIElement> build, bool bottom = false)
        {
            Id = id; Title = title; Glyph = glyph; Build = build; Bottom = bottom;
        }
    }

    public sealed class SenderKeys
    {
        public string UseBot = "useBot";
        public string SendAction = "sendAction";
        public string CooldownUseBot = "cooldownUseBot";
        public string CooldownSendAction = "cooldownSendAction";
        /// <summary>The group name in <see cref="MessageDef.Group"/> whose messages use the cooldown senders.</summary>
        public string CooldownGroup = "Cooldown";
        /// <summary>The key of the thousands separator setting, shown beside the message picker. Null hides it.</summary>
        public string NumberSeparator = "numberSeparator";
    }

    public sealed class MessageDef
    {
        public string Group, Key, Label, Help;
        public string[] Tokens = new string[0];

        public MessageDef() { }

        public MessageDef(string group, string key, string label, string help, params string[] tokens)
        {
            Group = group; Key = key; Label = label; Help = help; Tokens = tokens ?? new string[0];
        }
    }

    public sealed class AboutSpec
    {
        /// <summary>"Responsible: aaskjer\nCredits: ..."</summary>
        public string Credits = "Responsible: aaskjer\nCredits: nate1280 / Streamer.bot and YOU ♡";
        /// <summary>Sections before the links: a heading, a note, then links.</summary>
        public readonly List<HelpSection> Sections = new List<HelpSection>();
        /// <summary>Extra links under "Links", after the repository and releases links the spec supplies.</summary>
        public readonly List<Link> Links = new List<Link>();
        public bool ShowStreamerBotLink = true;
        public bool ShowAuthorLink = true;
    }

    public sealed class Link
    {
        public string Text, Url;
        public Link(string text, string url) { Text = text; Url = url; }
    }

    /// <summary>A heading with notes, command rows and links, for the Help and About pages.</summary>
    public sealed class HelpSection
    {
        public string Heading;
        public readonly List<HelpLine> Lines = new List<HelpLine>();

        public HelpSection(string heading) { Heading = heading; }

        public HelpSection Note(string text) { Lines.Add(new HelpLine { Note = text }); return this; }
        public HelpSection Command(string command, string what) { Lines.Add(new HelpLine { Command = command, What = what }); return this; }
        public HelpSection Link(string text, string url) { Lines.Add(new HelpLine { LinkText = text, Url = url }); return this; }
        /// <summary>Text redrawn from the live controls whenever the page is shown.</summary>
        public HelpSection Live(Func<PageContext, IEnumerable<string>> notes) { Lines.Add(new HelpLine { Live = notes }); return this; }
    }

    public sealed class HelpLine
    {
        public string Note, Command, What, LinkText, Url;
        public Func<PageContext, IEnumerable<string>> Live;
    }

    public sealed class LogSpec
    {
        /// <summary>Opened by the folder button, watched for new entries. Null hides the button. Relative to the
        /// Streamer.bot folder (<see cref="FullFolder"/>).</summary>
        public string Folder;
        /// <summary>The log files in the folder, "BJG-log*.txt": what the generic stub reads and the watcher
        /// follows. Null watches every file in the folder.</summary>
        public string Files;

        public string FullFolder
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Folder)) return null;
                try { return System.IO.Path.IsPathRooted(Folder) ? Folder : System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Folder); }
                catch (Exception) { return null; }
            }
        }
        /// <summary>Columns after Time and Result: header and the field of <see cref="LogEntry.Fields"/> to show.</summary>
        public readonly List<LogColumn> Columns = new List<LogColumn>();
        /// <summary>Values for the Result filter, in order. Empty: built from the entries.</summary>
        public readonly List<string> Results = new List<string>();
        public readonly List<string> Platforms = new List<string> { "twitch", "youtube", "kick" };
        /// <summary>The bool setting behind "Write a log file per day". Null hides the switch.</summary>
        public string WriteLogKey;
        public string WriteLogLabel = "Write a log file per day";
        public string WriteLogHelp;
        /// <summary>Null: the framework's own text, in its language.</summary>
        public string EmptyText;
        /// <summary>Colours a result: success, warning, muted or accent. Null uses <see cref="LogPage.DefaultStyle"/>.</summary>
        public Func<string, PillStyle> ResultStyle;
        /// <summary>Shortens a raw result to the filter value and the pill text, e.g. "DUEL WIN" to "WIN". Null keeps it.</summary>
        public Func<string, string> Outcome;
    }

    public sealed class LogColumn
    {
        public string Header, Field;
        public LogColumn(string header, string field) { Header = header; Field = field; }
    }

    public enum PillStyle { Success, Warning, Muted, Accent, None }

    /// <summary>One line of the host's log. <see cref="LogEntry.Parse"/> reads the games' format:
    /// "[yyyy-MM-dd HH:mm:ss] RESULT | User: name | Platform: twitch | Bet: 100 | ...".</summary>
    public sealed class LogEntry
    {
        // Properties, not fields: the Log page's grid binds to them.
        public DateTime Timestamp { get; set; }
        public string Result { get; set; } = "";
        public string User { get; set; } = "";
        public string Platform { get; set; } = "";
        public string Raw { get; set; } = "";
        public Dictionary<string, string> Fields { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public string Field(string name)
        {
            if (string.Equals(name, "User", StringComparison.OrdinalIgnoreCase)) return User;
            if (string.Equals(name, "Platform", StringComparison.OrdinalIgnoreCase)) return Platform;
            if (string.Equals(name, "Result", StringComparison.OrdinalIgnoreCase)) return Result;
            return Fields.TryGetValue(name, out var value) ? value : "";
        }

        public string SearchBlob
        {
            get
            {
                var parts = new List<string> { Result, User, Platform, Raw };
                parts.AddRange(Fields.Values);
                return string.Join(" ", parts).ToLowerInvariant();
            }
        }

        public static LogEntry Parse(string line)
        {
            int closeIdx = string.IsNullOrEmpty(line) || line[0] != '[' ? -1 : line.IndexOf(']');
            string payload = closeIdx >= 0 ? line.Substring(closeIdx + 1).TrimStart() : "";
            var entry = new LogEntry { Timestamp = DateTime.MinValue, Raw = line ?? "" };
            if (closeIdx < 0 || payload.Length == 0)
                return entry;

            if (DateTime.TryParseExact(line.Substring(1, closeIdx - 1), "yyyy-MM-dd HH:mm:ss",
                System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTime stamp))
                entry.Timestamp = stamp;

            var parts = payload.Split(new[] { " | " }, StringSplitOptions.None);
            entry.Result = parts[0].Trim();
            for (int i = 1; i < parts.Length; i++)
            {
                int colon = parts[i].IndexOf(':');
                if (colon <= 0) continue;
                string key = parts[i].Substring(0, colon).Trim();
                string value = parts[i].Substring(colon + 1).Trim();
                if (key.Equals("User", StringComparison.OrdinalIgnoreCase)) entry.User = value;
                else if (key.Equals("Platform", StringComparison.OrdinalIgnoreCase)) entry.Platform = value;
                else entry.Fields[key] = value;
            }
            return entry;
        }
    }

    /// <summary>A pill in the header, redrawn from the live controls on every change.</summary>
    public sealed class HeaderPill
    {
        public string Tooltip;
        /// <summary>The tooltip as declared; <see cref="Tooltip"/> is it in the window's language.</summary>
        public readonly string Declared;
        /// <summary>Returns text and style; null text hides the pill.</summary>
        public Func<PageContext, (string Text, PillStyle Style)> Read;
        public HeaderPill(string tooltip, Func<PageContext, (string, PillStyle)> read) { Tooltip = tooltip; Declared = tooltip; Read = read; }
    }
}
