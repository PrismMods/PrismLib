using UnityEngine;
using UnityEngine.UIElements;

namespace PrismLib.UI.Toolkit
{
    /* Drawn cursors for the ambiguous parts of a panel (a resize edge, a text field). The swap is
       GLOBAL, so every element that sets one clears it on leave and on detach — see Facet.Cursors. */
    public static class Cursors
    {
        public enum Kind { Default, ResizeHorizontal, ResizeCorner, Text }

        /// Show `kind` while the pointer is over `e`; cleared on leave and when `e` leaves the panel.
        public static void Set(VisualElement e, Kind kind) => Facet.Cursors.Set(e, (Facet.Cursors.Kind)kind);

        /// Swap the global cursor now (Facet.Cursors draws them).
        public static void Apply(Kind kind) => Facet.Cursors.Apply((Facet.Cursors.Kind)kind);
    }
}
