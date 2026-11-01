using System;
using System.Collections.Generic;
using System.Windows.Media;
using Newtonsoft.Json.Linq;

namespace UIFramework
{
    /// <summary>
    /// Everything a page builder gets: the settings as loaded, the defaults, the field factory bound to this
    /// window, the parts registry for cross-page reads, and a line to the footer.
    /// </summary>
    public sealed class PageContext
    {
        public WindowSpec Spec { get; internal set; }
        public JObject Settings { get; internal set; }
        public JObject Defaults { get; internal set; }
        public WindowParts Parts { get; internal set; }
        public Fields Fields { get; internal set; }
        public IHostLog Log { get; internal set; }
        public IHostHooks Hooks { get; internal set; }

        /// <summary>Writes a line into the footer. Colour from the theme, an optional tooltip for the why.</summary>
        public Action<string, Color, string> SetStatus { get; internal set; } = (text, colour, why) => { };

        /// <summary>Redraws the refreshers registered for a page, by page id.</summary>
        public Action<string> RefreshPage { get; internal set; } = id => { };

        /// <summary>Runs once every page is built, for wiring that reaches across pages (a field that follows a
        /// switch elsewhere).</summary>
        public List<Action> AfterBuild { get; } = new List<Action>();

        /// <summary>The customise mode is on: fields are wrapped, values are not editable.</summary>
        public bool Customizing => Spec?.Customizing == true;

        /// <summary>The user's layout file, to write to in the customise mode.</summary>
        public LayoutFile Layout => Spec?.Layout;

        /// <summary>Closes and reopens the window on the given page (null: the current one), after a layout change.</summary>
        public Action<string> Reopen { get; internal set; }

        public void OpenUrl(string url) => Web.OpenUrl(url, Log, Spec?.LogPrefix);
    }
}
