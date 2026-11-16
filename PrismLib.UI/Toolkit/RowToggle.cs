using UnityEngine.UIElements;

namespace PrismLib.UI.Toolkit
{
    /* A click on a Facet row's background or label flips the row's switch when that switch is all the row holds: one
       switch in its "control" slot, nothing else focusable on the row, no sub-page arrow. The switch gets a submit, the
       same path the keyboard takes, so undo, Changed and the row's refresh run as for a click on the switch itself.
       (Facet workaround: Facet's rows only take clicks on the switch.) */
    public static class RowToggle
    {
        public static void Wire(VisualElement root)
        {
            root?.RegisterCallback<ClickEvent>(e => Flip(e.target as VisualElement, root));
        }

        private static void Flip(VisualElement target, VisualElement root)
        {
            VisualElement row = null, control = null;
            for (var e = target; e != null && e != root; e = e.parent)
            {
                if (e.name == "control" || e.focusable) return;   // a click on (or in) a control is the control's own
                control = Slot(e);
                if (control != null) { row = e; break; }
            }
            if (row == null || control.childCount != 1) return;
            var sw = control[0];
            if (!IsSwitch(sw) || !sw.enabledInHierarchy) return;
            var arrow = row.Q<Label>("arrow");
            if (arrow != null && !string.IsNullOrEmpty(arrow.text)) return;   // a sub-page row opens its page instead
            foreach (var x in row.Query<VisualElement>().Build())
                if (x != sw && x.focusable && x.resolvedStyle.display != DisplayStyle.None) return;
            // A submit goes to the focused element whatever its target says (NavigationEventBase dispatches to focus), so the
            // switch takes focus first; FocusRing drops that focus again a tick later, as for any pointer focus.
            sw.Focus();
            using (var submit = NavigationSubmitEvent.GetPooled()) sw.SendEvent(submit);
        }

        private static VisualElement Slot(VisualElement row)
        {
            foreach (var c in row.Children())
                if (c.name == "control") return c;
            return null;
        }

        // Facet's switch: a focusable track whose one child is its unpickable knob.
        private static bool IsSwitch(VisualElement e)
        {
            return e.focusable && e.childCount == 1 && e[0].pickingMode == PickingMode.Ignore;
        }
    }
}
