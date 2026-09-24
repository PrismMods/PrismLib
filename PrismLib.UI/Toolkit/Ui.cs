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

        public static Button Btn(string text, Action onClick, VisualElement parent = null)
        {
            var b = new Button(onClick != null ? () => onClick() : (Action)null) { text = text ?? "" };
            b.style.fontSize = Tokens.FontSizeSmall;
            b.style.color = Tokens.Text;
            b.style.backgroundColor = Tokens.RowAlt;
            SetBorderWidth(b, 0f);
            SetRadius(b, 4f);
            SetPadding(b, 5f);
            b.style.marginLeft = 0f; b.style.marginRight = Tokens.Gap;
            b.style.marginTop = 0f; b.style.marginBottom = 0f;
            b.RegisterCallback<MouseEnterEvent>(_ => b.style.backgroundColor = Tokens.RowHover);
            b.RegisterCallback<MouseLeaveEvent>(_ => b.style.backgroundColor = Tokens.RowAlt);
            parent?.Add(b);
            return b;
        }

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
