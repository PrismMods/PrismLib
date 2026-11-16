using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace PrismLib.UI.Toolkit
{
    /* The owner filter: a tab bar at the top centre, "All" then each owner with a target. Only the
       chosen owner's frames can be picked or selected; the rest are dimmed. With Filter null the
       editor behaves as it did before the bar. The tab names are rebuilt in Reload (or when Filter
       names an owner the bar lacks), never in Tick. */
    public sealed partial class LayoutEditor
    {
        private const float Dimmed = 0.35f;
        private VisualElement _filterBar;   // userData: the current Filter, for checks that cannot name this type
        private Tabs _filterTabs;
        private readonly List<string> _filterNames = new List<string>();   // [0] is "All"
        private readonly List<string> _owners = new List<string>();
        private string _filter;
        private bool _filterOff;

        /// The mod whose elements can be moved; null is All. The others are dimmed and ignore the pointer.
        /// Set it before showing the editor; the tab bar follows, and a player's click on a tab sets it.
        public string Filter
        {
            get { return _filter; }
            set { _filter = _filterOff ? null : value; ApplyFilter(false); }
        }

        private bool Movable(Frame f) => LayoutMath.Movable(f.T.Owner, _filter);

        partial void BuildFilter()
        {
            try
            {
                _filterBar = Ui.Card(_layer);
                _filterBar.name = "layout-filter";
                _filterBar.pickingMode = PickingMode.Ignore;   // only its tab buttons take the clicks
                _filterBar.style.position = Position.Absolute;
                _filterBar.style.top = 12;
                _filterBar.style.left = Length.Percent(50);
                _filterBar.style.translate = new Translate(Length.Percent(-50), 0);
                _filterTabs = new Tabs(_filterBar, i => Filter = i <= 0 || i >= _filterNames.Count ? null : _filterNames[i]);
                _filterTabs.Element.pickingMode = PickingMode.Ignore;
                // Facet.Tabs leaves a TabGap for a page below; here the card's padding already closes it (Facet workaround).
                _filterTabs.Element.style.marginBottom = 0;
            }
            catch (Exception e) { FilterOff(e); }
        }

        partial void ReloadFilter() => ApplyFilter(true);

        private void ApplyFilter(bool rebuild)
        {
            if (_filterBar == null || _filterOff) return;
            try
            {
                int index = _filter == null ? 0 : _filterNames.IndexOf(_filter);
                if (rebuild || index <= 0)
                {
                    _owners.Clear();
                    foreach (var f in _all) _owners.Add(f.T.Owner);
                    _filterNames.Clear();
                    _filterNames.Add("All");   // the words path the editor's "Done" takes
                    _filterNames.AddRange(LayoutMath.FilterTabs(_owners, _filter));
                    Words.As(Words.Self, () => _filterTabs.Rebuild(_filterNames));
                    // Only the buttons take the pointer: a press between or under them reaches the frame below.
                    var row = _filterTabs.Element;
                    for (int i = 0; i < row.childCount; i++) row[i].pickingMode = PickingMode.Ignore;
                    index = _filter == null ? 0 : _filterNames.IndexOf(_filter, 1);
                }
                _filterBar.userData = _filter;
                _filterTabs.Select(index);
                // Reload keeps the earlier selection by key: a frame the filter dims must not keep
                // its grips and inspector.
                if (_selected != null && !Movable(_selected)) { _selected = null; SelectionChanged(); }
            }
            catch (Exception e) { FilterOff(e); }
        }

        // The editor still opens and moves everything, as before the filter.
        private void FilterOff(Exception e)
        {
            _filterOff = true;
            _filter = null;
            if (_filterBar != null) _filterBar.style.display = DisplayStyle.None;
            if (_reported.Add("filter")) Ui.Log("LayoutEditor: filter off " + e.Message);
        }
    }
}
