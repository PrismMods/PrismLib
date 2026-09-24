using UnityEngine;
using UnityEngine.UIElements;

namespace PrismLib.UI.Toolkit
{
    /* Styling for UI Toolkit's built-in controls.

       Everything a runtime mod can build is unstyled. UI Toolkit's controls get their appearance
       from the default theme style sheet, which is an ASSET — and USS cannot be imported at
       runtime, so a mod has none. Toggle and the dropdown were cheap enough to draw by hand;
       Slider, TextField and ScrollView are not, because their dragging, focus and scrolling logic
       is worth far more than their looks.

       So they are skinned instead: the parts are named children ("unity-tracker", "unity-dragger",
       "unity-text-input"), and setting inline styles on those is the same thing the theme sheet
       would have done. Names are Unity's, not ours — if a Unity upgrade renames one, that control
       goes back to looking like nothing, which is why each lookup is null-checked rather than
       assumed. */
    public static class Skin
    {
        public static void Slider(VisualElement slider)
        {
            if (slider == null) return;
            var tracker = slider.Q(className: "unity-base-slider__tracker");
            if (tracker != null)
            {
                tracker.style.backgroundColor = Tokens.RowAlt;
                tracker.style.height = 4f;
                Ui.SetRadius(tracker, 2f);
                Ui.SetBorderWidth(tracker, 0f);
            }
            var dragger = slider.Q(className: "unity-base-slider__dragger");
            if (dragger != null)
            {
                dragger.style.backgroundColor = Tokens.Accent;
                dragger.style.width = 12f;
                dragger.style.height = 12f;
                Ui.SetRadius(dragger, 6f);
                Ui.SetBorderWidth(dragger, 0f);
                dragger.style.marginTop = -4f;
            }
            var label = slider.Q<Label>(className: "unity-base-field__label");
            if (label != null) label.style.display = DisplayStyle.None;   // the row already has one
        }

        public static void Field(VisualElement field)
        {
            if (field == null) return;
            var input = field.Q(className: "unity-base-text-field__input");
            if (input == null) input = field.Q("unity-text-input");
            if (input != null)
            {
                input.style.backgroundColor = Tokens.Row;
                input.style.color = Tokens.Text;
                Ui.SetBorderColor(input, Tokens.PanelBorder);
                Ui.SetBorderWidth(input, 1f);
                Ui.SetRadius(input, 4f);
                Ui.SetPadding(input, 4f);
                input.style.unityTextAlign = TextAnchor.MiddleLeft;
                // Without this the caret is drawn in the theme's colour, which is nothing.
                input.style.unityBackgroundImageTintColor = Tokens.Text;
            }
            var label = field.Q<Label>(className: "unity-base-field__label");
            if (label != null) label.style.display = DisplayStyle.None;
        }

        /* Scrollbars. A ScrollView with no theme still scrolls — the wheel works — but nothing
           shows how far down you are, which on a log is most of the information. */
        public static void Scroll(ScrollView view)
        {
            if (view == null) return;
            Scroller(view.verticalScroller);
            Scroller(view.horizontalScroller);
            view.style.backgroundColor = Color.clear;
        }

        private static void Scroller(Scroller s)
        {
            if (s == null) return;
            s.style.width = 10f;
            s.style.backgroundColor = Color.clear;
            var slider = s.slider;
            if (slider != null)
            {
                var tracker = slider.Q(className: "unity-base-slider__tracker");
                if (tracker != null)
                {
                    tracker.style.backgroundColor = Color.clear;
                    Ui.SetBorderWidth(tracker, 0f);
                }
                var dragger = slider.Q(className: "unity-base-slider__dragger");
                if (dragger != null)
                {
                    dragger.style.backgroundColor = Tokens.RowHover;
                    dragger.style.width = 6f;
                    Ui.SetRadius(dragger, 3f);
                    Ui.SetBorderWidth(dragger, 0f);
                }
            }
            // The arrow buttons are pure theme: with none, they are invisible dead space.
            if (s.lowButton != null) s.lowButton.style.display = DisplayStyle.None;
            if (s.highButton != null) s.highButton.style.display = DisplayStyle.None;
        }

        /* Styling has to wait for the control to be attached: the parts are created when it joins a
           panel, so a Q() before that finds nothing. */
        public static void When<T>(VisualElement e, System.Action<T> apply) where T : VisualElement
        {
            if (e == null) return;
            var t = e as T;
            if (t == null) return;
            if (e.panel != null) { apply(t); return; }
            EventCallback<AttachToPanelEvent> cb = null;
            cb = _ => { apply(t); e.UnregisterCallback(cb); };
            e.RegisterCallback(cb);
        }
    }
}
