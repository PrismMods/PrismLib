using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace PrismLib.UI.Toolkit
{
    /* The owner filter: a side rail at the left edge, vertically centred, "All" then each owner with a target, one per line
       with the selection marker at the tab's left. Only the
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
                // Done's margin from the edge (it holds the top right), centred on the screen's height.
                _filterBar.style.left = 24;
                _filterBar.style.top = Length.Percent(50);
                _filterBar.style.translate = new Translate(0, Length.Percent(-50));
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
                    Stack(row);
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

        /* Facet.Tabs lays its tabs in a row over a rule, each underlined; as a side rail they stack one per line and the
           underline becomes a bar down the tab's left side (Facet workaround: Tabs has no vertical form). Per rebuild only. */
        private static void Stack(VisualElement row)
        {
            var sz = Facet.Theme.For(row).Sizes;
            row.style.flexDirection = FlexDirection.Column;
            row.style.flexWrap = Wrap.NoWrap;
            row.style.alignItems = Align.Stretch;
            for (int i = 0; i < row.childCount; i++)
            {
                var cell = row[i];
                if (cell.name == "rule") { cell.style.display = DisplayStyle.None; continue; }
                cell.style.marginRight = 0;
                var b = cell.Q<Button>();
                if (b != null)
                {
                    b.style.unityTextAlign = TextAnchor.MiddleLeft;
                    b.style.paddingLeft = sz.ControlPad + sz.Indicator + sz.Gap / 2f;
                }
                var line = cell.Q("indicator");
                if (line == null) continue;
                line.style.right = StyleKeyword.Auto;
                line.style.top = 0;
                line.style.width = sz.Indicator;
                line.style.height = StyleKeyword.Auto;
            }
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
