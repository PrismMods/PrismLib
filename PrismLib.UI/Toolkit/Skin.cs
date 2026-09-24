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
                tracker.style.position = Position.Absolute;
                tracker.style.left = 0f; tracker.style.right = 0f;
                tracker.style.top = Length.Percent(50f);
                tracker.style.height = 4f;
                tracker.style.marginTop = -2f;
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
                /* Centre it on the track. Both the tracker and the dragger are laid out by the
                   theme — which a runtime mod does not get — so each is placed here against the
                   drag container's midline. Positioning only the dragger just moved the mismatch:
                   the track was not centred either. */
                dragger.style.top = Length.Percent(50f);
                dragger.style.marginTop = -6f;
            }
            // The drag container is what gives the row its height; the default theme sizes it.
            var drag = slider.Q(className: "unity-base-slider__drag-container");
            if (drag != null) drag.style.height = 20f;
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

        /* A wheel path that does not rely on the host forwarding scroll.

           Clicks reach these panels, so pointer events are delivered — but a runtime UI Toolkit
           panel only receives WheelEvent if the game's input module forwards it, and this game's
           does not appear to. Rather than leave a log you cannot scroll, the window polls the wheel
           itself and drives whichever ScrollView is under the pointer.

           Reports once whether the event ever arrived on its own, so the workaround can be dropped
           if a future game or Unity version starts delivering it. */
        private static bool _sawWheelEvent, _reported;
        private static int _lastWheelFrame = -99;

        /* Whether a real WheelEvent arrived THIS frame, rather than whether one has ever arrived.

           A single "the host sends wheel events" flag was wrong: it was set by the settings panel,
           which does get them, and that switched polling off for the debug list, which does not —
           so the log stopped scrolling. Per-frame is the honest question, because the answer
           differs between panels and between the elements inside one. */
        public static bool WheelHandledThisFrame => Time.frameCount - _lastWheelFrame <= 1;

        public static void WatchWheel(VisualElement root)
        {
            if (root == null) return;
            /* TrickleDown, so this is seen on the way IN — a ScrollView that handles the wheel
               stops it bubbling, and a bubbling handler would therefore never hear about exactly
               the case it needs to know about. */
            root.RegisterCallback<WheelEvent>(_ =>
            {
                _lastWheelFrame = Time.frameCount;
                if (_sawWheelEvent) return;
                _sawWheelEvent = true;
                Ui.Log("Skin: host delivers WheelEvent; polling stands down on frames it arrives");
            }, TrickleDown.TrickleDown);
        }

        /// Scroll whatever is under the pointer. `delta` is Input.mouseScrollDelta.y.
        public static void Wheel(VisualElement root, Vector2 pointerScreen, float delta)
        {
            if (root == null || root.panel == null || Mathf.Approximately(delta, 0f)) return;

            var lists = root.Query<ScrollView>().ToList();
            var point = RuntimePanelUtils.ScreenToPanel(root.panel, pointerScreen);

            ScrollView target = null, biggest = null;
            foreach (var sv in lists)
            {
                var b = sv.worldBound;
                if (biggest == null || b.width * b.height > biggest.worldBound.width * biggest.worldBound.height)
                    biggest = sv;
                if (!b.Contains(point)) continue;
                // Innermost wins: a list inside a scrolling page should take the wheel.
                if (target == null || b.width * b.height < target.worldBound.width * target.worldBound.height)
                    target = sv;
            }

            /* Falling back to the biggest scroller when the point misses everything is deliberate.
               Getting the pointer into panel space depends on the panel's scale mode and on which
               screen coordinates the host reports, and a window you cannot scroll is a worse
               outcome than one that scrolls its main list when the pointer is over its chrome. */
            bool missed = target == null;
            if (missed) target = biggest;

            if (!_reported)
            {
                _reported = true;
                Ui.Log("Skin wheel: delta=" + delta.ToString("0.##")
                       + " screen=" + pointerScreen.x.ToString("0") + "," + pointerScreen.y.ToString("0")
                       + " panel=" + point.x.ToString("0") + "," + point.y.ToString("0")
                       + " scrollviews=" + lists.Count
                       + (target == null ? " NONE FOUND"
                          : " target=" + target.worldBound
                            + " content=" + target.contentContainer.layout.height.ToString("0")
                            + " view=" + target.contentViewport.layout.height.ToString("0")
                            + " list=" + (target.GetFirstAncestorOfType<ListView>() != null ? "yes" : "no")
                            + (missed ? " (fallback, point missed all)" : ""))
                       + (_sawWheelEvent ? "" : " [host sends no WheelEvent]"));
            }
            if (target == null) return;

            float view = target.contentViewport.layout.height;
            float max = Mathf.Max(0f, target.contentContainer.layout.height - view);

            /* A virtualised ListView does not grow its content container — that is the whole point
               of virtualising — so measuring it says a 53-row log has nothing to scroll. Ask the
               list how many rows it has instead. */
            var list = target.GetFirstAncestorOfType<ListView>();
            if (list != null && list.itemsSource != null && list.fixedItemHeight > 0f)
                max = Mathf.Max(max, list.itemsSource.Count * list.fixedItemHeight - view);

            if (max <= 0f) return;

            /* A wheel notch reports ±1; a trackpad reports fractions — the measured value here was
               -0.05, which at the 40px-per-unit this used to apply moved the page TWO pixels and
               looked exactly like nothing happening. Scale the two separately: a notch is worth
               about three rows, and a trackpad's continuous stream is worth enough per event to
               keep up with the finger. */
            float px = Mathf.Abs(delta) < 0.6f ? delta * 400f : delta * 60f;
            var o = target.scrollOffset;
            o.y = Mathf.Clamp(o.y - px, 0f, max);
            target.scrollOffset = o;
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
