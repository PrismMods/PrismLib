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

        /// Set by the panel that owns this window, to raise its surface. Window has no Surface of
        /// its own — it is just the element tree inside one.
        public Action OnRaise;

        private readonly string _id;
        private Rect _rect;
        private float _scale = 1f;
        private Vector2 _dragFrom;
        private Rect _rectFrom;

        /* Where each window was last left, by id.

           Kept in memory AND written through Store when a host provides one. PrismLib.UI cannot own
           a settings file — it must not reference PrismLib.dll, and it has no idea where a given
           mod keeps its data — so the host supplies two delegates and gets to decide the format.
           Without them the geometry still survives a reopen, just not a restart. */
        private static readonly Dictionary<string, Rect> _saved = new Dictionary<string, Rect>();
        private static readonly Dictionary<string, float> _savedScale = new Dictionary<string, float>();

        /// Host-provided persistence. Read returns null for an unknown key.
        public static Func<string, string> Read;
        public static Action<string, string> Write;

        private static string Load(string key)
        {
            try { return Read != null ? Read(key) : null; } catch { return null; }
        }

        private static void Save(string key, string value)
        {
            try { if (Write != null) Write(key, value); } catch { }
        }

        /* "x,y,w,h,scale" — invariant culture, because a machine with a comma decimal separator
           would otherwise write a string it cannot read back. */
        private static bool TryParse(string s, out Rect r, out float scale)
        {
            r = default(Rect); scale = 1f;
            if (string.IsNullOrEmpty(s)) return false;
            var p = s.Split(',');
            if (p.Length < 5) return false;
            float x, y, w, h, sc;
            var c = System.Globalization.CultureInfo.InvariantCulture;
            if (!float.TryParse(p[0], System.Globalization.NumberStyles.Float, c, out x)) return false;
            if (!float.TryParse(p[1], System.Globalization.NumberStyles.Float, c, out y)) return false;
            if (!float.TryParse(p[2], System.Globalization.NumberStyles.Float, c, out w)) return false;
            if (!float.TryParse(p[3], System.Globalization.NumberStyles.Float, c, out h)) return false;
            if (!float.TryParse(p[4], System.Globalization.NumberStyles.Float, c, out sc)) return false;
            r = new Rect(x, y, w, h); scale = sc;
            return true;
        }

        public Window(VisualElement parent, string id, string title, Rect initial, Action onClose = null)
        {
            _id = id ?? title ?? "window";
            Rect stored; float storedScale;
            if (_saved.ContainsKey(_id))
            {
                _rect = _saved[_id];
                _scale = _savedScale.ContainsKey(_id) ? _savedScale[_id] : 1f;
            }
            else if (TryParse(Load("window." + _id), out stored, out storedScale))
            {
                _rect = stored; _scale = storedScale;
            }
            else _rect = initial;

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
            if (onClose != null) Ui.Btn("Close", onClose, TitleBarRight);

            Body = Ui.Box(Root);
            Body.style.flexGrow = 1f;
            Body.style.overflow = Overflow.Hidden;
            Ui.SetPadding(Body, Tokens.Pad);

            Footer = Ui.Row(Root);
            Footer.style.backgroundColor = Tokens.TitleBar;
            // Scale lives with the other view controls, not in the title bar: it adjusts how the
            // contents read, which is the same kind of thing as the line-number toggle beside it.
            var zoomOut = Ui.Btn("−", () => Scale = _scale - 0.1f, Footer);
            zoomOut.tooltip = "Smaller";
            var zoomIn = Ui.Btn("+", () => Scale = _scale + 0.1f, Footer);
            zoomIn.tooltip = "Larger";
            Footer.style.paddingLeft = Tokens.Pad;
            Footer.style.paddingRight = Tokens.Pad;
            Footer.style.paddingTop = 3f;
            Footer.style.paddingBottom = 3f;

            // Click anywhere in the window to raise it above the other one.
            Root.RegisterCallback<PointerDownEvent>(_ => { if (OnRaise != null) OnRaise(); });

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

        /* Scale magnifies the CONTENTS, not the window. The transform would otherwise blow the
           whole box up and push it off screen, when what scaling is for is reading the text — so
           the layout size is divided by the scale that multiplies it, leaving the rendered
           footprint exactly the rect the user dragged out. Scaling up therefore shows less, larger,
           in the same space. */
        private void Apply()
        {
            Root.style.left = _rect.x;
            Root.style.top = _rect.y;
            Root.style.width = _rect.width / _scale;
            Root.style.height = _rect.height / _scale;
            Root.style.scale = new Scale(new Vector2(_scale, _scale));
            _saved[_id] = _rect;
            _savedScale[_id] = _scale;
            var c = System.Globalization.CultureInfo.InvariantCulture;
            Save("window." + _id, _rect.x.ToString(c) + "," + _rect.y.ToString(c) + ","
                                + _rect.width.ToString(c) + "," + _rect.height.ToString(c) + ","
                                + _scale.ToString(c));
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

            _rect.width = Mathf.Clamp(_rect.width, MinWidth, Mathf.Max(MinWidth, size.x));
            _rect.height = Mathf.Clamp(_rect.height, MinHeight, Mathf.Max(MinHeight, size.y));
            // At least the title bar stays on screen, horizontally by a good grab's worth.
            float maxX = size.x - 80f;
            float maxY = size.y - TitleHeight * _scale;
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
                var d = (Vector2)e.position - _dragFrom;
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
                var d = (Vector2)e.position - _dragFrom;
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
