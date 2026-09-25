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
        private const float TitleHeight = 40f;

        /// The outer element. Style it as little as possible from outside; use Body.
        public VisualElement Root { get; private set; }

        /// Where content goes. Clips: anything inside is cut at the window edge rather than
        /// painting over the chrome, which is what a long log line did to the footer.
        public VisualElement Body { get; private set; }

        /// Right-hand side of the title bar. Put toolbar buttons here.
        public VisualElement TitleBarRight { get; private set; }

        /// Search box in the header. Hidden until a panel calls ShowSearch.
        public TextField Search { get; private set; }

        /// The ≡ button. A panel with a rail wires it; others leave it hidden.
        public Button RailToggle { get; private set; }

        /// Where the content pane starts, so the header can line up with it.
        public const float RailAlign = 200f;

        public void ShowSearch(string placeholder, Action<string> onChange)
        {
            if (Search == null) return;
            Search.style.display = DisplayStyle.Flex;
            Search.textEdition.placeholder = placeholder;
            Search.RegisterValueChangedCallback(e => { if (onChange != null) onChange(e.newValue); });
        }

        /// Footer strip along the bottom, above the resize grip. Always on top of Body's content.
        public VisualElement Footer { get; private set; }

        /* Hint on the left, version on the right — the shape both mods' panels already use, so it
           belongs to the window rather than to each panel that wants one. */
        public void SetFooter(string hint, string right)
        {
            if (_footerHint != null) _footerHint.text = hint ?? "";
            if (_footerRight != null) _footerRight.text = right ?? "";
        }

        private Label _footerHint, _footerRight;

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
            /* A rail toggle sits where the rail is, and the title takes the rail's width, so the
               search box that follows starts exactly where the content pane does instead of
               floating in the middle of the header. */
            RailToggle = Ui.Btn("\u2261", null, bar);
            RailToggle.style.width = 34f;
            RailToggle.style.height = 28f;
            RailToggle.style.fontSize = Tokens.FontSize + 7f;   // it is an icon, not a letter
            Ui.SetPadding(RailToggle, 0f);
            RailToggle.style.backgroundColor = Color.clear;
            /* Lined up with the rail's labels beneath it, not centred in its own button: the
               window body pads by Tokens.Pad and a rail row by 10 more, so the glyph starts where
               "Interface" and "Hide UI" do. */
            RailToggle.style.marginLeft = 10f;
            RailToggle.style.unityTextAlign = TextAnchor.MiddleLeft;
            // No resting background: it is a chrome affordance, not a button competing with the
            // title beside it. It still lights on hover, which is what says it is clickable.
            var railColours = RailToggle.userData;
            RailToggle.RegisterCallback<MouseLeaveEvent>(_ => RailToggle.style.backgroundColor = Color.clear);
            RailToggle.tooltip = "Show or hide the sidebar";

            var label = Ui.Text(title, bar, Tokens.FontSize);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.width = RailAlign - 28f;
            label.style.flexShrink = 0f;
            label.pickingMode = PickingMode.Ignore;     // clicks on the text still drag the bar
            /* A search box in the header, where the mods already put theirs. Empty and hidden
               until a panel fills it — a window with nothing to search should not show one. */
            Search = new TextField { value = "" };
            Search.style.flexGrow = 1f;
            Search.style.maxWidth = 420f;
            Search.style.display = DisplayStyle.None;
            Search.style.marginRight = Tokens.Gap;
            Skin.When<TextField>(Search, Skin.Field);
            bar.Add(Search);

            var spacer = Ui.Box(bar);
            spacer.style.flexGrow = 1f;   // pins the controls right whatever the search box does

            TitleBarRight = Ui.Row(bar);

            /* Undo and redo in the header, where they apply to whatever the window is showing.
               Text rather than ↶ ↷: the panel draws in an OS font chosen at runtime and there is
               no guarantee it carries those glyphs, whereas a missing glyph is a blank button. */
            var undo = Glyph("\u21B6", "Undo", () => History.Undo());
            var redo = Glyph("\u21B7", "Redo", () => History.Redo());
            TitleBarRight.Add(undo);
            TitleBarRight.Add(redo);
            Action refreshHistory = () =>
            {
                Enable(undo, History.CanUndo, "Undo " + (History.NextUndo ?? ""));
                Enable(redo, History.CanRedo, "Redo " + (History.NextRedo ?? ""));
            };
            History.Changed += refreshHistory;
            refreshHistory();

            /* × rather than a "Close" button, matching the mods' own panels. Sized as a square so
               it reads as a window control instead of as another toolbar button. */
            if (onClose != null)
            {
                var close = Ui.Btn("\u2715", onClose, TitleBarRight);
                close.style.width = 30f;
                close.style.height = 26f;
                close.style.backgroundColor = Color.clear;
                close.style.marginRight = 0f;
                /* Centre the glyph by hand. A Button's text is laid out by the theme, so without
                   one it sits wherever the default padding leaves it — which is what made the ✕
                   look off-centre. */
                Ui.SetPadding(close, 0f);
                close.style.unityTextAlign = TextAnchor.MiddleCenter;
                close.style.fontSize = Tokens.FontSize;
                close.RegisterCallback<MouseEnterEvent>(_ => close.style.backgroundColor = Tokens.Danger);
                close.RegisterCallback<MouseLeaveEvent>(_ => close.style.backgroundColor = Color.clear);
            }

            Body = Ui.Box(Root);
            Body.style.flexGrow = 1f;
            /* The one element that MUST absorb slack. Ui.Box defaults to flexShrink 0 so rows keep
               their height, but the window body is the opposite case: it has to shrink to whatever
               the window is, or it grows to its content and the window merely clips it. That is
               what made the debug log unscrollable — the list's viewport came out 2700px tall
               inside a 580px window, so the list reported that everything fitted. */
            Body.style.flexShrink = 1f;
            Body.style.minHeight = 0f;
            Body.style.overflow = Overflow.Hidden;
            Ui.SetPadding(Body, Tokens.Pad);

            Footer = Ui.Row(Root);
            Footer.style.backgroundColor = Tokens.TitleBar;
            // No scale buttons on the chrome: panel size is a setting, and a control that lives on
            // every window competes with the content for a job done once.
            Footer.style.paddingLeft = Tokens.Pad;
            Footer.style.paddingRight = Tokens.Pad;
            Footer.style.paddingTop = 4f;
            Footer.style.paddingBottom = 4f;
            _footerHint = Ui.Muted("", Footer);
            _footerHint.style.flexGrow = 1f;
            _footerRight = Ui.Muted("", Footer);

            // Click anywhere in the window to raise it above the other one.
            Root.RegisterCallback<PointerDownEvent>(_ => { if (OnRaise != null) OnRaise(); });

            MakeDraggable(bar);
            MakeResizable();
            Apply();
        }

        /* ↶ and ↷. These live in Arrows, which every OS UI font carries — unlike the pictographic
           blocks, where a miss is a blank button rather than a wrong-looking one. */
        private static Button Glyph(string glyph, string tip, Action onClick)
        {
            var b = Ui.Btn(glyph, onClick);
            b.style.width = 30f;
            b.style.height = 26f;
            b.style.fontSize = Tokens.FontSize + 2f;
            Ui.SetPadding(b, 0f);
            b.style.unityTextAlign = TextAnchor.MiddleCenter;
            b.tooltip = tip;
            return b;
        }

        /* Disabled buttons fade rather than vanish, so the header does not reflow every time the
           first change of a session is made. SetEnabled alone leaves them looking active — the
           theme that would grey them out is the one a runtime mod does not have. */
        private static void Enable(Button b, bool on, string tip)
        {
            b.SetEnabled(on);
            b.style.opacity = on ? 1f : 0.35f;
            b.tooltip = on ? tip : null;
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
            grip.style.backgroundColor = Color.clear;
            grip.pickingMode = PickingMode.Position;
            Root.Add(grip);

            /* A 3x3 dot grid with the top-left corner dropped, which is the grip Bismuth's panels
               already use. Dots rather than rotated bars: a rotation about a percentage origin
               moves the box as well as turning it, which is why the earlier staircase never lined
               up. */
            for (int row = 0; row < 3; row++)
                for (int col = 0; col < 3; col++)
                {
                    if (row + col < 2) continue;
                    var dot = new VisualElement();
                    dot.style.position = Position.Absolute;
                    dot.style.right = 3f + (2 - col) * 4f;
                    dot.style.bottom = 3f + (2 - row) * 4f;
                    dot.style.width = 2f;
                    dot.style.height = 2f;
                    dot.style.backgroundColor = Tokens.TextMuted;
                    dot.pickingMode = PickingMode.Ignore;
                    grip.Add(dot);
                }

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
