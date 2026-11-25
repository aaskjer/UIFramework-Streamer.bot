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

        /// <summary>Rebuilds the window's content in place on the given page (null: the current one), after a layout change.</summary>
        public Action<string> Reopen { get; internal set; }

        /// <summary>The settings as the window shows them right now, unsaved and transient values included; the
        /// loaded settings until the window is wired.</summary>
        public Func<JObject> LiveSettings { get; internal set; }

        /// <summary>Takes a list as it is now for saved: rows the project handed over (<see cref="ProjectRows"/>) are no
        /// change of the user's. Null until the window is wired.</summary>
        public Action<ListEditor, Newtonsoft.Json.Linq.JArray, bool> Rebase { get; internal set; }

        /// <summary>Runs the action after every change to any field of the window (debounce it yourself).</summary>
        public Action<Action> OnAnyChange { get; internal set; } = listener => { };

        /// <summary>Saves what the window shows unsaved, as its Save button does, the check before saving and its
        /// question included. True when it is saved or there was nothing to save; false when that save did not happen
        /// (a number that is none, a warning the user declined). A button with <c>saveFirst</c> asks only after a true.</summary>
        public Func<bool> SaveChanges { get; internal set; } = () => true;

        /// <summary>The customise bar's Export and Import behind their file dialogs, by the file's path: what the
        /// tests drive, since a file dialog cannot be.</summary>
        internal Action<string> ExportTo { get; set; }
        internal Action<string> ImportFrom { get; set; }

        public void OpenUrl(string url) => Web.OpenUrl(url, Log, Spec?.LogPrefix);
    }
}
