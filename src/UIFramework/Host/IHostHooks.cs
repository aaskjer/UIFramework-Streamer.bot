using System.Collections.Generic;
using System.Windows;
using Newtonsoft.Json.Linq;

namespace UIFramework
{
    /// <summary>
    /// What the host does that the framework cannot know about: applying saved settings to Streamer.bot
    /// (cooldowns, timers, OBS), answering a Test button, building a page of its own, reading its log.
    /// Every method has a default so a host implements only what it needs.
    /// </summary>
    public interface IHostHooks
    {
        /// <summary>Called on the window's thread after a successful save with the settings as written.
        /// Anything that has to run on Streamer.bot's thread is queued here and applied by the stub once the
        /// window closes; see <see cref="Shell.Run"/>.</summary>
        void OnSaved(JObject settings);

        /// <summary>A Test button or an action field. Returns a short status line for the footer, or null.</summary>
        string Test(string name, JObject settings);

        /// <summary>A page the spec marks as custom. Returns null to leave the page out.</summary>
        FrameworkElement BuildPage(string pageId, PageContext context);

        /// <summary>The host's own log entries for the Log page, in the order they were written (oldest first): the page
        /// puts the newest first, and of the lines of one second the last written. Null hides the page.</summary>
        IEnumerable<LogEntry> ReadLog();

        /// <summary>Rows for a setting of kind "table", as a JSON array of objects keyed by the schema's columns.
        /// Null or empty shows the table's empty text.</summary>
        string ReadTable(string key);

        /// <summary>What the emote pictures of Twitch emote cells are looked up with, as JSON: <c>twitchUserId</c>
        /// (the broadcaster, for the channel's 7TV, BTTV, FFZ and Twitch emotes), <c>clientId</c> and <c>token</c>
        /// (for the Twitch API). Null asks only the global 7TV, BTTV and FFZ sets.</summary>
        string EmoteSources();

        /// <summary>Looks at the settings about to be saved and names what would not do what it looks like, one
        /// problem per line; the window shows them and asks whether to save anyway. Null or empty: nothing to say.</summary>
        string Validate(JObject settings);

        /// <summary>The names of Streamer.bot's user groups as a JSON array, for the chips of a grouplist field to
        /// suggest. Asked once per window, off the window's thread. Null: nothing to suggest.</summary>
        string Groups();

        /// <summary>The options of a dropdown that takes them from the project (<c>optionsFrom</c>), by its key, with
        /// the values as they stand in the window: a JSON array of "value", "value|Label" or
        /// <c>{ "value", "label" }</c>. Null: the project names none.</summary>
        string Options(string key, JObject settings);

        /// <summary>Whether the host reads and writes Streamer.bot's persisted globals for the window (a setting with
        /// <c>global</c>): a settings action from stub 0.3.3 on does. Without it such a field is shown locked.</summary>
        bool HasGlobals { get; }

        /// <summary>The text of a persisted global, or null when there is none.</summary>
        string GetGlobal(string name);

        /// <summary>Writes a persisted global; an empty text clears it. False when it could not be handed over.</summary>
        bool SetGlobal(string name, string value);
    }

    /// <summary>A hooks implementation that does nothing, for previews and hosts without extras.</summary>
    public class NoHooks : IHostHooks
    {
        public virtual void OnSaved(JObject settings) { }
        public virtual string Test(string name, JObject settings) => null;
        public virtual FrameworkElement BuildPage(string pageId, PageContext context) => null;
        public virtual IEnumerable<LogEntry> ReadLog() => null;
        public virtual string ReadTable(string key) => null;
        public virtual string EmoteSources() => null;
        public virtual string Validate(JObject settings) => null;
        public virtual string Groups() => null;
        public virtual string Options(string key, JObject settings) => null;
        public virtual bool HasGlobals => false;
        public virtual string GetGlobal(string name) => null;
        public virtual bool SetGlobal(string name, string value) => false;
    }
}
