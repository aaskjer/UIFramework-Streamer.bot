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
