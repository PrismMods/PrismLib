using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace PrismLib.UI.Toolkit
{
    /* A draggable, resizable, scalable window inside a Surface.

       Every panel in these mods needs the same handful of behaviours — move it, resize it, keep it
       on screen when the resolution changes, remember where it was — and the uGUI framework grew a
       DragHandle and a ResizeHandle per mod to do it. One implementation here instead.

       Geometry is kept in POINTS, not style values read back: resolvedStyle is NaN until layout
       runs, and reading a position back after every drag frame accumulates rounding. The window
       owns its rect and writes it down. */
    public sealed class Window
    {
        public const float MinWidth = 320f, MinHeight = 200f;
        private const float GripSize = 16f;
        private const float TitleHeight = 28f;

        /// The outer element. Style it as little as possible from outside; use Body.
        public VisualElement Root { get; private set; }

        /// Where content goes. Clips: anything inside is cut at the window edge rather than
        /// painting over the chrome, which is what a long log line did to the footer.
        public VisualElement Body { get; private set; }

        /// Right-hand side of the title bar. Put toolbar buttons here.
        public VisualElement TitleBarRight { get; private set; }

        /// Footer strip along the bottom, above the resize grip. Always on top of Body's content.
        public VisualElement Footer { get; private set; }

        private readonly string _id;
        private Rect _rect;
        private float _scale = 1f;
        private Vector2 _dragFrom;
        private Rect _rectFrom;

        /* Remembered per window id for the session, so closing and reopening does not send the
           window back to its default corner.
           ponytail: in memory only — survives a reopen, not a restart. Persisting means a file
           PrismLib owns, which is worth doing once more than one window exists. */
        private static readonly Dictionary<string, Rect> _saved = new Dictionary<string, Rect>();
        private static readonly Dictionary<string, float> _savedScale = new Dictionary<string, float>();

        public Window(VisualElement parent, string id, string title, Rect initial, Action onClose = null)
        {
            _id = id ?? title ?? "window";
            _rect = _saved.ContainsKey(_id) ? _saved[_id] : initial;
            _scale = _savedScale.ContainsKey(_id) ? _savedScale[_id] : 1f;

            Root = Ui.Card(parent);
            Root.pickingMode = PickingMode.Position;
            Root.style.position = Position.Absolute;
            Root.style.overflow = Overflow.Hidden;      // chrome is never painted over
            Ui.SetPadding(Root, 0f);                    // the parts do their own padding
            // Scale about the top-left, so the stored rect still describes where it is.
            Root.style.transformOrigin = new TransformOrigin(0f, 0f, 0f);

            var bar = Ui.Row(Root);
            bar.style.height = TitleHeight;
            bar.style.backgroundColor = Tokens.TitleBar;
            bar.style.paddingLeft = Tokens.Pad;
            bar.style.paddingRight = Tokens.Gap;
            var label = Ui.Text(title, bar);
            label.style.flexGrow = 1f;
            label.pickingMode = PickingMode.Ignore;     // clicks on the text still drag the bar
            TitleBarRight = Ui.Row(bar);
            Ui.Btn("−", () => Scale = _scale - 0.1f, TitleBarRight);
            Ui.Btn("+", () => Scale = _scale + 0.1f, TitleBarRight);
            if (onClose != null) Ui.Btn("Close", onClose, TitleBarRight);

            Body = Ui.Box(Root);
            Body.style.flexGrow = 1f;
            Body.style.overflow = Overflow.Hidden;
            Ui.SetPadding(Body, Tokens.Pad);

            Footer = Ui.Row(Root);
            Footer.style.backgroundColor = Tokens.TitleBar;
            Footer.style.paddingLeft = Tokens.Pad;
            Footer.style.paddingRight = Tokens.Pad;
            Footer.style.paddingTop = 3f;
            Footer.style.paddingBottom = 3f;

            MakeDraggable(bar);
            MakeResizable();
            Apply();
        }

        public float Scale
        {
            get { return _scale; }
            set
            {
                _scale = Mathf.Clamp(value, 0.6f, 2f);
                _savedScale[_id] = _scale;
                Apply();
            }
        }

        public Rect Rect
        {
            get { return _rect; }
            set { _rect = value; Clamp(); Apply(); }
        }

        private void Apply()
        {
            Root.style.left = _rect.x;
            Root.style.top = _rect.y;
            Root.style.width = _rect.width;
            Root.style.height = _rect.height;
            Root.style.scale = new Scale(new Vector2(_scale, _scale));
            _saved[_id] = _rect;
        }

        /* Keep the window reachable. A resolution change or a scale bump can leave it mostly off
           screen, and a title bar you cannot reach is a window you cannot move back. Everything is
           in panel points, which is what left/top are in — Screen.width is PIXELS, so it has to be
           converted through the panel's own scale or the clamp is wrong at every resolution but
           the reference one. */
        private void Clamp()
        {
            var panel = Root.panel;
            if (panel == null) return;
            var size = Root.parent != null ? Root.parent.layout.size : Vector2.zero;
            if (size.x <= 0f || size.y <= 0f) return;

            _rect.width = Mathf.Clamp(_rect.width, MinWidth, Mathf.Max(MinWidth, size.x / _scale));
            _rect.height = Mathf.Clamp(_rect.height, MinHeight, Mathf.Max(MinHeight, size.y / _scale));
            // At least the title bar stays on screen, horizontally by a good grab's worth.
            float maxX = size.x / _scale - 80f;
            float maxY = size.y / _scale - TitleHeight;
            _rect.x = Mathf.Clamp(_rect.x, -_rect.width + 80f, Mathf.Max(0f, maxX));
            _rect.y = Mathf.Clamp(_rect.y, 0f, Mathf.Max(0f, maxY));
        }

        private void MakeDraggable(VisualElement bar)
        {
            bar.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button != 0) return;
                _dragFrom = e.position;
                _rectFrom = _rect;
                bar.CapturePointer(e.pointerId);
                e.StopPropagation();
            });
            bar.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (!bar.HasPointerCapture(e.pointerId)) return;
                var d = ((Vector2)e.position - _dragFrom) / _scale;
                _rect.x = _rectFrom.x + d.x;
                _rect.y = _rectFrom.y + d.y;
                Clamp();
                Apply();
            });
            bar.RegisterCallback<PointerUpEvent>(e =>
            {
                if (!bar.HasPointerCapture(e.pointerId)) return;
                bar.ReleasePointer(e.pointerId);
            });
        }

        private void MakeResizable()
        {
            var grip = new VisualElement();
            grip.style.position = Position.Absolute;
            grip.style.right = 0f; grip.style.bottom = 0f;
            grip.style.width = GripSize; grip.style.height = GripSize;
            grip.style.backgroundColor = Tokens.RowHover;
            grip.pickingMode = PickingMode.Position;
            Root.Add(grip);

            grip.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button != 0) return;
                _dragFrom = e.position;
                _rectFrom = _rect;
                grip.CapturePointer(e.pointerId);
                e.StopPropagation();
            });
            grip.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (!grip.HasPointerCapture(e.pointerId)) return;
                var d = ((Vector2)e.position - _dragFrom) / _scale;
                _rect.width = _rectFrom.width + d.x;
                _rect.height = _rectFrom.height + d.y;
                Clamp();
                Apply();
            });
            grip.RegisterCallback<PointerUpEvent>(e =>
            {
                if (!grip.HasPointerCapture(e.pointerId)) return;
                grip.ReleasePointer(e.pointerId);
            });
        }

        /// Re-clamp after the screen size changes. Cheap; call it when a panel becomes visible.
        public void Reclamp() { Clamp(); Apply(); }
    }
}
