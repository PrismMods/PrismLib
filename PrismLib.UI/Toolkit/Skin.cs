using UnityEngine;
using UnityEngine.UIElements;

namespace PrismLib.UI.Toolkit
{
    /* Inline styling for UI Toolkit's built-in controls, which a runtime mod gets unstyled (no theme
       style sheet), plus the polled wheel a runtime panel may never receive — see Facet.Skin. */
    public static class Skin
    {
        public static void Slider(VisualElement slider) => Facet.Skin.Slider(slider);

        public static void Field(VisualElement field) => Facet.Skin.Field(field);

        /// Scrollbars, and the viewport clipping only the theme would give. Build every ScrollView through it.
        public static void Scroll(ScrollView view) => Facet.Skin.Scroll(view);

        /// Whether a real WheelEvent arrived THIS frame — the answer differs between panels.
        public static bool WheelHandledThisFrame => Facet.Skin.WheelHandledThisFrame;

        public static void WatchWheel(VisualElement root) => Facet.Skin.WatchWheel(root);

        public static void Wheel(VisualElement root, Vector2 pointerScreen, float delta) => Facet.Skin.Wheel(root, pointerScreen, delta);

        /// Run `apply` once `e` is in a panel: a control's named parts do not exist before that.
        public static void When<T>(VisualElement e, System.Action<T> apply) where T : VisualElement => Facet.Skin.When(e, apply);
    }
}
