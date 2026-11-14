using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace PrismLib.UI.Toolkit
{
    /* The layout tabs: Hands, then Feet, each a one-line strip that scrolls sideways. A click edits and shows a layout
       as one undo step, so undo walks back through tab switches. */
    public sealed partial class KeyLayoutEditor<T>
    {
        private VisualElement _strip;
        private Facet.Reorder<string> _hands, _feet;

        private void BuildStrip()
        {
            _strip = Facet.Ui.Row(Root);
            _strip.name = "kle-strip";
            _strip.style.flexWrap = Wrap.NoWrap;
            _hands = Strip(LayoutGroup.Hands);
            _feet = Strip(LayoutGroup.Feet);
        }

        private void RefreshStrip()
        {
            if (_hands != null) _hands.Refresh();
            if (_feet != null) _feet.Refresh();
        }

        private Facet.Reorder<string> Strip(LayoutGroup group)
        {
            bool hands = group == LayoutGroup.Hands;
            Facet.Reorder<string> r = null;
            r = Facet.Controls.Reorder<string>(_strip, o =>
            {
                o.Id = hands ? "kle-hands" : "kle-feet";
                o.Title = L(hands ? KeyLayoutText.Hands : KeyLayoutText.Feet);
                if (!hands)
                    o.TitleMenu = () => new List<Facet.MenuItem>
                    {
                        Item(KeyLayoutText.HideFeet, null, () => Pick(KeyLayoutText.HideFeet, LayoutGroup.Feet, null)),
                    };
                o.Items = () => _model.Layouts(group);
                o.Text = _model.Name;
                o.Selected = id => id == _model.Editing;
                o.Active = id => id == _model.Shown(group);
                o.Select = id => Pick(KeyLayoutText.EditLayout, group, id);
                o.Move = (from, to) => Do(KeyLayoutText.MoveLayout, () => _model.Move(_model.Layouts(group)[from], to));
                o.Rename = (id, name) => Do(KeyLayoutText.Rename, () => _model.Rename(id, name));
                // Foot counts are new empty layouts, not presets.
                o.Add = () => Limited() ? null : (IList<Facet.MenuItem>)(_model.NewItems(group, _ => { }) ?? new List<Facet.MenuItem>())
                    .Select(i => Wrapped(i, hands ? KeyLayoutText.NewFromPreset : KeyLayoutText.NewLayout)).ToList();
                o.Menu = id => new List<Facet.MenuItem>
                {
                    Item(KeyLayoutText.Rename, null, () => r.BeginRename(id)),
                    Item(KeyLayoutText.Duplicate, null, () => { if (!Limited()) Do(KeyLayoutText.Duplicate, () => _model.Duplicate(id)); }),
                    // The model keeps at least one hand layout.
                    Item(KeyLayoutText.Delete, null, () => ConfirmDelete(id), () => !(hands && _model.Layouts(LayoutGroup.Hands).Count == 1)),
                };
                o.Scroll = true;
            });
            r.Root.style.flexShrink = 1f;
            r.Root.style.minWidth = 0f;
            if (hands) r.Root.style.flexGrow = 1f;
            return r;
        }

        private void ConfirmDelete(string id)
        {
            Facet.Popup.Confirm(_strip, L(KeyLayoutText.DeleteConfirm), _model.Name(id),
                ok => { if (ok) Do(KeyLayoutText.Delete, () => _model.Delete(id)); },
                o => { o.Danger = true; o.Ok = L(KeyLayoutText.Delete); o.Cancel = L(KeyLayoutText.Cancel); });
        }
    }
}
