using System;
using System.Collections.Generic;
using System.Windows.Controls;

namespace UIFramework
{
    /// <summary>
    /// Every control that something else on the window needs to read back: the message preview pulls text out
    /// of the boxes, the chat preview reads the sender switches, and a live summary redraws from whatever the
    /// other pages currently say. Keyed by setting key.
    /// </summary>
    public sealed class WindowParts
    {
        public readonly Dictionary<string, TextBox> Boxes = new Dictionary<string, TextBox>(StringComparer.Ordinal);
        public readonly Dictionary<string, CheckBox> Switches = new Dictionary<string, CheckBox>(StringComparer.Ordinal);
        public readonly Dictionary<string, ComboBox> Combos = new Dictionary<string, ComboBox>(StringComparer.Ordinal);
        public readonly Dictionary<string, OptionSlider> Sliders = new Dictionary<string, OptionSlider>(StringComparer.Ordinal);
        public readonly Dictionary<string, ListEditor> Lists = new Dictionary<string, ListEditor>(StringComparer.Ordinal);
        /// <summary>The choices of every <c>multi</c> setting, shown or not: a condition on one asks whether a value is
        /// ticked, also when the field is hidden and the saved text is read. The field itself is in <see cref="Boxes"/>,
        /// the hidden box that holds the ticked values as comma-separated text.</summary>
        public readonly Dictionary<string, string[]> Choices = new Dictionary<string, string[]>(StringComparer.Ordinal);
        /// <summary>Every setting's whole field as its page shows it (label, control, help), for the tour to point at.</summary>
        public readonly Dictionary<string, System.Windows.FrameworkElement> Elements = new Dictionary<string, System.Windows.FrameworkElement>(StringComparer.Ordinal);

        /// <summary>The actions of Streamer.bot the window watches; null where nothing is watched (a page built alone).</summary>
        public ActionWatch Actions;
        private readonly List<Action> _actionListeners = new List<Action>();

        /// <summary>Runs whenever a watched action is enabled, disabled, deleted or back.</summary>
        public void OnActions(Action changed) => _actionListeners.Add(changed);

        public void RaiseActions()
        {
            foreach (var changed in _actionListeners.ToArray())
                changed();
        }

        // Keyed by page id, so switching pages only redraws the one in front.
        private readonly Dictionary<string, List<Action>> _refreshers = new Dictionary<string, List<Action>>(StringComparer.Ordinal);
        private readonly List<Action> _nameCommits = new List<Action>();

        /// <summary>Runs when the page is shown or asked to refresh.</summary>
        public void OnShow(string pageId, Action refresh)
        {
            if (!_refreshers.TryGetValue(pageId, out var list))
                _refreshers[pageId] = list = new List<Action>();
            list.Add(refresh);
        }

        public void Refresh(string pageId)
        {
            if (_refreshers.TryGetValue(pageId, out var list))
                foreach (var refresh in list)
                    refresh();
        }

        /// <summary>A chip list commits its typed-but-unentered name here, so Save never loses one.</summary>
        public void AddNameCommit(Action commit) => _nameCommits.Add(commit);

        public void CommitNames()
        {
            foreach (var commit in _nameCommits)
                commit();
        }
    }
}
