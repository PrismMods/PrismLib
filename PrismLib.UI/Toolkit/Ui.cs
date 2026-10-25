using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace PrismLib.UI.Toolkit
{
    /* Element factories. Every style is set in C# — there is no runtime USS — so this is where the
       repetition goes instead of into every screen.

       Deliberately thin: these return plain VisualElements, not wrapper types, so anything the
       factories do not cover is just UI Toolkit and needs no escape hatch. */
    public static class Ui
    {
        /* Where this half reports what it did. The game prints "Mods detected! Disabling exception
           capturing", so a throw in here reaches no log at all and a broken panel is
           indistinguishable from a key that did nothing. Each mod points this at its own log. */
        public static Action<string> Log = _ => { };

        /* How many Prism windows are on screen.

           The mods need this for the same reason each of them already guards its own panels: the
           editor zooms on the scroll wheel and the game reacts to clicks, so scrolling a list or
           dragging a window inside a Prism panel would also drive the world behind it. A mod checks
           AnyWindowOpen in its own zoom/click path — it cannot be enforced from here, because the
           input belongs to the game and the patch belongs to the mod.

           A counter rather than a bool: two windows can be open, and closing one must not tell the
           editor it is free again. */
        public static int OpenWindows;

        public static bool AnyWindowOpen => OpenWindows > 0;

        /// A visible surface wants the keyboard and wheel to itself — see InputGate.
        public static bool BlocksGameInput
        {
            get
            {
                for (int i = 0; i < Stack.Count; i++)
                    if (Stack[i].Visible && Stack[i].BlocksGameInput) return true;
                return false;
            }
        }

        /* Is the pointer actually over one of our windows?

           AnyWindowOpen was too blunt for the editor's scroll-zoom: with a panel open anywhere on
           screen, the wheel stopped zooming even with the pointer out in the world. What the editor
           needs to know is whether this scroll belongs to a menu, which is a question about
           position, not about whether anything is open. */
        public static bool PointerOverWindow(Vector2 pointerScreen)
        {
            for (int i = Stack.Count - 1; i >= 0; i--)
            {
                var surface = Stack[i];
                if (!surface.Visible || surface.Root == null || surface.Root.panel == null) continue;
                var point = RuntimePanelUtils.ScreenToPanel(
                    surface.Root.panel, new Vector2(pointerScreen.x, Screen.height - pointerScreen.y));
                foreach (var e in surface.Root.Children())
                    if (e.worldBound.Contains(point)) return true;
            }
            return false;
        }

        /* Is the user typing into one of our windows?

           The game's "user is editing a field" test asks the uGUI EventSystem, which has never heard
           of a UI Toolkit TextField — so a letter typed into a Prism search box or number field also
           reached the editor and placed a tile. A mod ORs this into its own typing check; as with
           the wheel, the input belongs to the game and only the mod can patch it.

           Walks up from the focused element because the focus usually sits on the field's inner
           text input, not on the field itself. Any TextInputBaseField<T> counts, so number fields
           are covered too. */
        public static bool TextFocused
        {
            get
            {
                for (int i = Stack.Count - 1; i >= 0; i--)
                {
                    var surface = Stack[i];
                    if (!surface.Visible || surface.Root == null || surface.Root.panel == null) continue;
                    var fc = surface.Root.panel.focusController;
                    for (var e = fc != null ? fc.focusedElement as VisualElement : null; e != null; e = e.parent)
                        if (IsTextInput(e.GetType())) return true;
                }
                return false;
            }
        }

        private static bool IsTextInput(Type t)
        {
            for (; t != null && t != typeof(VisualElement); t = t.BaseType)
                if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(TextInputBaseField<>)) return true;
            return false;
        }

        /// Drive the wheel for every open window. The host calls this each frame with
        /// Input.mousePosition and Input.mouseScrollDelta.y — see Skin.Wheel for why.
        public static void TickWheel(Vector2 pointerScreen, float delta)
        {
            if (Mathf.Approximately(delta, 0f)) return;
            /* Deliberately NOT standing down when a WheelEvent arrives.

               A WheelEvent reaching the panel says only that: not that any ScrollView acted on it.
               Standing down on that basis left the debug log unscrollable while reporting nothing
               at all, because the polled path — the only thing that logs — never ran. Scrolling
               twice as fast is a visible, tunable problem; scrolling not at all while the logs stay
               silent is the one that has cost the most time here. Skin.Wheel skips a target that
               already moved this frame, which is the honest version of the same check. */
            // Topmost visible window only, so two open windows do not both scroll.
            for (int i = Stack.Count - 1; i >= 0; i--)
            {
                if (!Stack[i].Visible) continue;
                Skin.Wheel(Stack[i].Root, pointerScreen, delta);
                return;
            }
        }

        /* Open surfaces, bottom first: by layer, then by raise order (Surface.BringToFront keeps it
           so). "Which one is on top" and "which one does Escape close" both need an answer, and
           neither can come from Unity: separate UIDocument panels are ordered by sortingOrder,
           not by click. */
        internal static readonly List<Surface> Stack = new List<Surface>();

        /* Escape, answered in one place. Walks the layers top-down — a popup before a modal before
           a window — and the first visible surface takes it: a focused text field gives up focus
           first (the way any desktop app treats Escape in a field), then the surface's own handler
           runs, else it hides. Returns false when nothing was open, so the caller lets the key
           through to the game.

           At most once per frame. Every Prism mod polls Escape in its own Update and calls this, so
           with two mods installed one press used to close two windows. A mod that already spent
           this frame's Escape — cancelling a keybind capture — says so with ConsumeEscape. */
        public static bool HandleEscape()
        {
            if (_escapeFrame == Time.frameCount) return _escapeResult;
            _escapeFrame = Time.frameCount;
            _escapeResult = false;
            for (int i = Stack.Count - 1; i >= 0; i--)
            {
                var s = Stack[i];
                if (!s.Visible) continue;
                var focused = s.Root != null && s.Root.panel != null && s.Root.panel.focusController != null
                    ? s.Root.panel.focusController.focusedElement as VisualElement : null;
                // The focus controller is the shared panel's, so only a field inside THIS surface counts.
                if (focused != null && s.Root.Contains(focused))
                    for (var e = focused; e != null; e = e.parent)
                        if (IsTextInput(e.GetType())) { focused.Blur(); Log("Escape: blurred a field in " + s.Name); return _escapeResult = true; }
                if (s.EscapeHandler != null)
                {
                    try { _escapeResult = s.EscapeHandler(); Log("Escape: " + s.Name + " handler -> " + _escapeResult); }
                    catch (Exception e) { Log("Escape handler threw: " + e.Message); _escapeResult = true; }
                    return _escapeResult;
                }
                s.Visible = false;
                Log("Escape: hid " + s.Name);
                return _escapeResult = true;
            }
            return false;
        }

        /// This frame's Escape is spent (a keybind capture took it) — HandleEscape will not act on it.
        public static void ConsumeEscape()
        {
            _escapeFrame = Time.frameCount;
            _escapeResult = true;
        }

        private static int _escapeFrame = -1;
        private static bool _escapeResult;

        /// The mods' Escape poll. Same as HandleEscape — kept so their bridges need no change.
        public static bool CloseTop() => HandleEscape();
        public static VisualElement Box(VisualElement parent = null)
        {
            var e = new VisualElement();
            e.style.flexDirection = FlexDirection.Column;
            /* flexShrink 0 by default, against the flexbox norm of 1.

               The CSS default assumes a layout that would rather squash than overflow. A settings
               page wants the opposite: rows and sections have a real height, and something too tall
               for its container should SCROLL, not compress until its text prints through the row
               below. Three separate overlaps — the debug panel's header under its tab bar, a
               section's rows through the next section's heading, and labels through each other —
               were all this one default, so it is set once here. Anything that genuinely should
               absorb slack sets flexShrink back to 1 on itself. */
            e.style.flexShrink = 0f;
            parent?.Add(e);
            return e;
        }

        public static VisualElement Row(VisualElement parent = null)
        {
            var e = Box(parent);
            e.style.flexDirection = FlexDirection.Row;
            e.style.alignItems = Align.Center;
            return e;
        }

        /// A bordered, rounded container — the standard panel body.
        public static VisualElement Card(VisualElement parent = null)
        {
            var e = Box(parent);
            e.style.backgroundColor = Tokens.Panel;
            SetBorderColor(e, Tokens.PanelBorder);
            SetBorderWidth(e, 1f);
            SetRadius(e, Tokens.Radius);
            SetPadding(e, Tokens.Pad);
            return e;
        }

        public static Label Text(string text, VisualElement parent = null, float? size = null, Color? color = null)
        {
            var l = new Label(text ?? "");
            l.style.fontSize = size ?? Tokens.FontSize;
            l.style.color = color ?? Tokens.Text;
            l.style.whiteSpace = WhiteSpace.NoWrap;
            l.style.textOverflow = TextOverflow.Ellipsis;
            l.style.overflow = Overflow.Hidden;
            parent?.Add(l);
            return l;
        }

        public static Label Muted(string text, VisualElement parent = null)
            => Text(text, parent, Tokens.FontSizeSmall, Tokens.TextMuted);

        /* A button's resting and hover colours live in a holder on the element, not in the hover
           callbacks. Setting backgroundColor from outside used to work until the pointer left the
           button, at which point MouseLeave put the resting colour back — which is why a selected
           tab looked selected only until the mouse moved. Highlight() changes the resting colour
           instead, so the callbacks agree with it. */
        private sealed class BtnColors { public Color Rest, Hover, Text; }

        public static Button Btn(string text, Action onClick, VisualElement parent = null)
        {
            var b = new Button(onClick != null ? () => onClick() : (Action)null) { text = text ?? "" };
            var c = new BtnColors { Rest = Tokens.RowAlt, Hover = Tokens.RowHover, Text = Tokens.Text };
            b.userData = c;
            b.style.fontSize = Tokens.FontSizeSmall;
            b.style.color = c.Text;
            b.style.backgroundColor = c.Rest;
            SetBorderWidth(b, 0f);
            SetRadius(b, 4f);
            SetPadding(b, 5f);
            b.style.marginLeft = 0f; b.style.marginRight = Tokens.Gap;
            b.style.marginTop = 0f; b.style.marginBottom = 0f;
            b.RegisterCallback<MouseEnterEvent>(_ => b.style.backgroundColor = c.Hover);
            b.RegisterCallback<MouseLeaveEvent>(_ => b.style.backgroundColor = c.Rest);
            parent?.Add(b);
            return b;
        }

        /// Strip a button's background entirely, hover included — for chrome affordances that
        /// should read as part of the frame rather than as buttons sitting on it.
        public static void Flat(Button b)
        {
            var c = b?.userData as BtnColors;
            if (c == null) return;
            c.Rest = Color.clear;
            c.Hover = Color.clear;
            b.style.backgroundColor = Color.clear;
        }

        /// Mark a button as the selected one. Survives the pointer leaving it.
        public static void Highlight(Button b, bool on)
        {
            var c = b?.userData as BtnColors;
            if (c == null) return;
            c.Rest = on ? Tokens.Accent : Tokens.RowAlt;
            c.Hover = on ? Lighten(Tokens.Accent, 0.12f) : Tokens.RowHover;
            c.Text = on ? new Color(0.06f, 0.06f, 0.08f, 1f) : Tokens.Text;
            b.style.backgroundColor = c.Rest;
            b.style.color = c.Text;
        }

        public static Color Lighten(Color c, float amount)
            => new Color(Mathf.Min(1f, c.r + amount), Mathf.Min(1f, c.g + amount), Mathf.Min(1f, c.b + amount), c.a);

        /* A virtualized list. This is the reason for moving to UI Toolkit at all: ListView recycles
           rows, so a 50k-line log costs the same as a 50-line one. The uGUI framework had to be
           told how many rows to build. */
        public static ListView List(Func<IList> source, Action<VisualElement, int> bind, VisualElement parent = null)
        {
            var lv = new ListView
            {
                fixedItemHeight = Tokens.RowHeight,
                selectionType = SelectionType.None,
                showBorder = false,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
            };
            lv.makeItem = () =>
            {
                var l = Text("", null, Tokens.FontSizeSmall);
                l.style.paddingLeft = 6f;
                l.style.unityTextAlign = TextAnchor.MiddleLeft;
                return l;
            };
            lv.bindItem = (e, i) => bind(e, i);
            lv.itemsSource = source();
            lv.style.flexGrow = 1f;
            lv.style.minHeight = 0f;   // or the list sizes to its rows and the window clips it
            parent?.Add(lv);
            return lv;
        }

        // ── style helpers: UI Toolkit splits these across four properties each ──

        public static void SetPadding(VisualElement e, float v)
        {
            e.style.paddingLeft = v; e.style.paddingRight = v;
            e.style.paddingTop = v; e.style.paddingBottom = v;
        }

        public static void SetMargin(VisualElement e, float v)
        {
            e.style.marginLeft = v; e.style.marginRight = v;
            e.style.marginTop = v; e.style.marginBottom = v;
        }

        public static void SetRadius(VisualElement e, float v)
        {
            e.style.borderTopLeftRadius = v; e.style.borderTopRightRadius = v;
            e.style.borderBottomLeftRadius = v; e.style.borderBottomRightRadius = v;
        }

        public static void SetBorderWidth(VisualElement e, float v)
        {
            e.style.borderLeftWidth = v; e.style.borderRightWidth = v;
            e.style.borderTopWidth = v; e.style.borderBottomWidth = v;
        }

        public static void SetBorderColor(VisualElement e, Color c)
        {
            e.style.borderLeftColor = c; e.style.borderRightColor = c;
            e.style.borderTopColor = c; e.style.borderBottomColor = c;
        }
    }
}
