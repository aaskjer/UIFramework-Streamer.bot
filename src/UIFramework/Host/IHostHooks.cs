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

        /// <summary>The host's own log entries for the Log page, newest first. Null hides the page.</summary>
        IEnumerable<LogEntry> ReadLog();

        /// <summary>Rows for a setting of kind "table", as a JSON array of objects keyed by the schema's columns.
        /// Null or empty shows the table's empty text.</summary>
        string ReadTable(string key);
    }

    /// <summary>A hooks implementation that does nothing, for previews and hosts without extras.</summary>
    public class NoHooks : IHostHooks
    {
        public virtual void OnSaved(JObject settings) { }
        public virtual string Test(string name, JObject settings) => null;
        public virtual FrameworkElement BuildPage(string pageId, PageContext context) => null;
        public virtual IEnumerable<LogEntry> ReadLog() => null;
        public virtual string ReadTable(string key) => null;
    }
}
