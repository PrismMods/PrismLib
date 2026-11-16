using System.Collections.Generic;
using UnityEngine.UIElements;

namespace PrismLib.UI.Toolkit
{
    /* Facet paints its focus ring on FocusInEvent, so a clicked button or tab keeps a ring until something else takes
       focus. PointerBlurs drops the focus a pointer click left behind (a tick later, after the click lands), so only
       keyboard focus shows a ring. A text input keeps its focus and caret. (Facet workaround: Facet is frozen.) */
    public static class FocusRing
    {
        private static readonly HashSet<VisualElement> Roots = new HashSet<VisualElement>();

        /// Once per root; a root that leaves its panel can be wired again when it comes back.
        public static void PointerBlurs(VisualElement root)
        {
            if (root == null || !Roots.Add(root)) return;
            root.RegisterCallback<PointerUpEvent>(_ => root.schedule.Execute(() => Blur(root)), TrickleDown.TrickleDown);
            root.RegisterCallback<DetachFromPanelEvent>(_ => Roots.Remove(root));
        }

        private static void Blur(VisualElement root)
        {
            var focused = root.focusController?.focusedElement as VisualElement;
            if (focused == null || InTextInput(focused)) return;
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
