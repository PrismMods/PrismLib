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

        /* How many Prism windows are on screen: a mirror of Facet.Ui.OpenWindows, kept for the field's
           old readers. Ask AnyWindowOpen instead — it counts every loaded Facet's windows. */
        public static int OpenWindows;

        /* The mods' zoom and click paths stand down while this holds (Sapphire's ZoomCamera prefix):
           scrolling a list inside a window would otherwise also drive the world behind it. Any
           Facet window counts, a mod's Facet-native one too. */
        public static bool AnyWindowOpen => Facet.Input.AnyWindowOpen;

        /// A visible surface wants the keyboard and wheel to itself — see InputGate.
        public static bool BlocksGameInput => Facet.Input.BlocksGameInput;

        /// The pointer is over a window: the editor's scroll-zoom asks this, not AnyWindowOpen.
        public static bool PointerOverWindow(Vector2 pointerScreen) => Facet.Input.PointerOverWindow(pointerScreen);

        /// The user is typing into a window; a mod ORs this into its own "editing a field" test.
        public static bool TextFocused => Facet.Input.TextFocused;

        /// Drive the wheel for the topmost window. The host calls this each frame with
        /// Input.mousePosition and Input.mouseScrollDelta.y — see Facet.Skin.Wheel for why.
        public static void TickWheel(Vector2 pointerScreen, float delta) => Facet.Input.TickWheel(pointerScreen, delta);

        /* Escape, answered once per frame across every loaded Facet: the topmost window takes it, a
           Prism one or a Facet-native one (see Facet.Input.HandleEscape). Returns false when nothing
           was open, so the caller lets the key through. */
        public static bool HandleEscape() => Facet.Input.HandleEscape();

        /// This frame's Escape is spent (a keybind capture took it) — HandleEscape will not act on it.
        public static void ConsumeEscape() => Facet.Input.ConsumeEscape();

        /// The mods' Escape poll. Same as HandleEscape — kept so their bridges need no change.
        public static bool CloseTop() => HandleEscape();

        public static VisualElement Box(VisualElement parent = null) => Facet.Ui.Box(parent);

        public static VisualElement Row(VisualElement parent = null) => Facet.Ui.Row(parent);

        /// A bordered, rounded container — the standard panel body.
        public static VisualElement Card(VisualElement parent = null) => Facet.Ui.Card(parent);

        /* Text is drawn in a role, so a re-theme recolours it (Facet.Paint). A literal colour opts the
           label out of that first, or the next attach, hover or re-theme would put the role back. */
        public static Label Text(string text, VisualElement parent = null, float? size = null, Color? color = null)
        {
            Label l;
            using (PrismHost.Use()) l = Facet.Ui.Text(text, parent, size);
            if (color.HasValue) { Facet.Paint.Forget(l); l.style.color = color.Value; }
            return l;
        }

        public static Label Muted(string text, VisualElement parent = null)
        {
            using (PrismHost.Use()) return Facet.Ui.Muted(text, parent);
        }

        /// Hover, pressed and selected (Highlight) are Facet.Paint's, not the caller's.
        public static Button Btn(string text, Action onClick, VisualElement parent = null)
        {
            using (PrismHost.Use()) return Facet.Ui.Btn(text, onClick, parent);
        }

        /// Strip a button's background entirely, hover included — for chrome affordances that
        /// should read as part of the frame rather than as buttons sitting on it.
        public static void Flat(Button b) { if (b != null) Facet.Paint.Look(b, Facet.Look.FlatButton); }

        /// Mark a button as the selected one. Survives the pointer leaving it.
        public static void Highlight(Button b, bool on) => Facet.Paint.Select(b, on);

        public static Color Lighten(Color c, float amount)
            => new Color(Mathf.Min(1f, c.r + amount), Mathf.Min(1f, c.g + amount), Mathf.Min(1f, c.b + amount), c.a);

        /// A virtualized list: ListView recycles rows, so a 50k-line log costs what a 50-line one does.
        public static ListView List(Func<IList> source, Action<VisualElement, int> bind, VisualElement parent = null)
        {
            using (PrismHost.Use()) return Facet.Ui.List(source, bind, parent);
        }

        // ── style helpers: UI Toolkit splits these across four properties each ──

        public static void SetPadding(VisualElement e, float v) => Facet.Ui.SetPadding(e, v);

        public static void SetMargin(VisualElement e, float v) => Facet.Ui.SetMargin(e, v);

        public static void SetRadius(VisualElement e, float v) => Facet.Ui.SetRadius(e, v);

        public static void SetBorderWidth(VisualElement e, float v) => Facet.Ui.SetBorderWidth(e, v);

        /// A literal colour: the element stops being painted by role first (see Text).
        public static void SetBorderColor(VisualElement e, Color c)
        {
            Facet.Paint.Forget(e);
            e.style.borderLeftColor = c; e.style.borderRightColor = c;
            e.style.borderTopColor = c; e.style.borderBottomColor = c;
        }
    }
}
