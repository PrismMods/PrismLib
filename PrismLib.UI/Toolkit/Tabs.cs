using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace PrismLib.UI.Toolkit
{
    /* A tab strip. Both mods carry a TabRail (530 lines between them, 94 of which differ) and the
       debug panel had hand-rolled a third; this is the one that replaces all three.

       Selection state lives here rather than in the caller's index variable, because the bug it
       keeps causing is a rebuild that leaves the highlight on a tab that has moved. Rebuild() takes
       the names and re-applies selection by NAME where it can, so a log source appearing or going
       away does not silently switch which tab you are reading. */
    public sealed class Tabs
    {
        private readonly VisualElement _bar;
        private readonly Action<int> _onSelect;
        private readonly List<string> _names = new List<string>();
        private readonly List<Button> _buttons = new List<Button>();

        public int Selected { get; private set; }
        public string SelectedName => Selected >= 0 && Selected < _names.Count ? _names[Selected] : null;

        public Tabs(VisualElement parent, Action<int> onSelect)
        {
            _onSelect = onSelect;
            _bar = Ui.Row(parent);
            _bar.style.flexWrap = Wrap.Wrap;
            _bar.style.marginBottom = Tokens.Gap;
        }

        public VisualElement Element => _bar;

        public void Rebuild(IEnumerable<string> names)
        {
            string wasSelected = SelectedName;
            _names.Clear();
            _names.AddRange(names ?? new string[0]);
            _bar.Clear();
            _buttons.Clear();

            // Keep reading the same tab across a rebuild when it is still there.
            int keep = wasSelected != null ? _names.IndexOf(wasSelected) : -1;
            Selected = keep >= 0 ? keep : (_names.Count > 0 ? 0 : -1);

            for (int i = 0; i < _names.Count; i++)
            {
                int idx = i;
                var b = Ui.Btn(_names[i], () => Select(idx), _bar);
                _buttons.Add(b);
            }
            Paint();
        }

        public void Select(int index)
        {
            if (index < 0 || index >= _names.Count || index == Selected) return;
            Selected = index;
            Paint();
            Anim.Pop(_buttons[index]);
            if (_onSelect != null) _onSelect(index);
        }

        private void Paint()
        {
            for (int i = 0; i < _buttons.Count; i++) Ui.Highlight(_buttons[i], i == Selected);
        }
    }
}
