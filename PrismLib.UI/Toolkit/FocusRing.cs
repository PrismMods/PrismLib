using System.Collections.Generic;
using UnityEngine.UIElements;

namespace PrismLib.UI.Toolkit
{
    /* Facet paints its focus ring on FocusInEvent, so a clicked button, tab, tile or switch keeps a ring until something
       else takes focus. PointerBlurs drops any focus that arrives while the pointer was the last input: the press's own,
       the panel handing focus back to its last element when it is clicked again, a page that rebuilds under the click.
       Only keyboard focus (Tab, arrows, any key) shows a ring. A text input keeps its focus and caret.
       (Facet workaround: the ring is Facet's.) */
    public static class FocusRing
    {
        private static readonly HashSet<VisualElement> Roots = new HashSet<VisualElement>();

        /// Once per root; a root that leaves its panel can be wired again when it comes back.
        public static void PointerBlurs(VisualElement root)
        {
            if (root == null || !Roots.Add(root)) return;
            bool pointer = false;
            root.RegisterCallback<PointerDownEvent>(_ => pointer = true, TrickleDown.TrickleDown);
            // Keys go to the focused element or, with none, the panel's own root above this one: listen there.
            EventCallback<KeyDownEvent> key = _ => pointer = false;
            EventCallback<NavigationMoveEvent> nav = _ => pointer = false;
            VisualElement tree = null;
            System.Action wire = () =>
            {
                var t = root.panel?.visualTree;
                if (t == null || t == tree) return;
                tree?.UnregisterCallback(key, TrickleDown.TrickleDown);
                tree?.UnregisterCallback(nav, TrickleDown.TrickleDown);
                tree = t;
                t.RegisterCallback(key, TrickleDown.TrickleDown);
                t.RegisterCallback(nav, TrickleDown.TrickleDown);
            };
            wire();
            root.RegisterCallback<AttachToPanelEvent>(_ => wire());
            // A tick later, after the click lands: blurring inside the focus change would fight the controller.
            root.RegisterCallback<FocusInEvent>(e =>
            {
                if (!pointer) return;
                var target = e.target as VisualElement;
                root.schedule.Execute(() => Blur(root, target));
            }, TrickleDown.TrickleDown);
            root.RegisterCallback<PointerUpEvent>(_ => root.schedule.Execute(() => Blur(root, null)), TrickleDown.TrickleDown);
            root.RegisterCallback<DetachFromPanelEvent>(_ => Roots.Remove(root));
        }

        // only: blur only while that element still has focus; null blurs whatever has it.
        private static void Blur(VisualElement root, VisualElement only)
        {
            var focused = root.focusController?.focusedElement as VisualElement;
            if (focused == null || (only != null && focused != only) || InTextInput(focused)) return;
            focused.Blur();
        }

        private static bool InTextInput(VisualElement e)
        {
            for (; e != null; e = e.parent)
                for (var t = e.GetType(); t != null; t = t.BaseType)
                    if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(TextInputBaseField<>)) return true;
            return false;
        }
    }
}
