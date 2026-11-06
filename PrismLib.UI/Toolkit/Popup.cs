using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace PrismLib.UI.Toolkit
{
    /* A floating card anchored to the control that opened it, on one shared Popup-layer surface:
       closes on an outside click, on Escape (newest first), when its window hides or its anchor
       leaves the panel — see Facet.Popup. */
    public sealed class Popup
    {
        private readonly Facet.Popup _popup;

        private Popup(Facet.Popup p) { _popup = p; }

        private static Popup Wrap(Facet.Popup p) => p != null ? new Popup(p) : null;

        public VisualElement Content => _popup.Content;

        public bool IsOpen => _popup.IsOpen;

        /// Open a popup under anchor (above it when there is no room below). build fills Content.
        public static Popup Open(VisualElement anchor, Action<VisualElement> build, Action onClose = null)
            => Wrap(Facet.Popup.Open(anchor, build, onClose));

        /// A list of choices. The current one is marked; picking closes the menu, then reports.
        public static Popup Menu(VisualElement anchor, IList<string> items, int selected, Action<int> onPick)
            => Wrap(Facet.Popup.Menu(anchor, items, selected, onPick));

        public void Close() => _popup.Close();

        /// Close every open popup — for a host tearing its UI down.
        public static void CloseAll() => Facet.Popup.CloseAll();
    }
}
