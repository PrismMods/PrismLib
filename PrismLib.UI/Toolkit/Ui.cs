using System;
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
        public static VisualElement Box(VisualElement parent = null)
        {
            var e = new VisualElement();
            e.style.flexDirection = FlexDirection.Column;
            parent?.Add(e);
            return e;
        }

        public static VisualElement Row(VisualElement parent = null)
        {
            var e = Box(parent);
            e.style.flexDirection = FlexDirection.Row;
            e.style.alignItems = Align.Center;
            /* Flex items shrink by default. In a fixed-height column next to something with
               flexGrow (a list), a row gets squeezed toward zero height and its labels spill out
               and overlap whatever is above — which is exactly how the debug panel first drew, with
               the tab bar sitting on top of the title. A row is chrome: it keeps its size. */
            e.style.flexShrink = 0f;
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
