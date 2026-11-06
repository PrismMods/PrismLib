using System;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.TextCore.Text;

namespace PrismLib.UI.Toolkit
{
    /* A UI Toolkit panel created at runtime: a Facet.Surface made for PrismLib's host (PrismHost).
       Panels, fonts, layers, stacking, display-not-SetActive and the warm-up all live there — see
       Facet.Surface and Facet.Fonts. */
    /* Hud draws under every window and is not a window: never in Facet's window stack, never
       counted as open, never closed by Escape, never withholding input — a readout like the FPS
       counter. */
    public enum Layer { Hud, Window, Popup, Modal, Tooltip }

    public sealed class Surface : IDisposable
    {
        /// The Facet surface this is. Not named Facet: that would hide the namespace here.
        internal readonly Facet.Surface Inner;

        public VisualElement Root => Inner.Root;

        public Layer Layer { get; }

        public string Name => Inner.Name;

        /// This surface's colour scheme. Replacing it re-themes what is already built.
        public Theme Theme
        {
            get { return Theme.Of(Inner.Theme); }
            set { if (value == Theme.Default) value.Refresh(); Inner.Theme = value != null ? value.Twin : null; }
        }

        /// While visible, keep keys and the wheel away from the game (InputGate). On by default:
        /// a window you are typing or scrolling in should not also drive the world behind it.
        public bool BlocksGameInput
        {
            get { return Inner.BlocksGameInput; }
            set { Inner.BlocksGameInput = value; }
        }

        /// Called instead of hiding when Escape reaches this surface; true when consumed.
        internal Func<bool> EscapeHandler
        {
            get { return Inner.EscapeHandler; }
            set { Inner.EscapeHandler = value; }
        }

        /// Raised when the surface hides or is disposed, whoever hid it (any host's Escape, a ×).
        internal event Action Hidden
        {
            add { Inner.Hidden += value; }
            remove { Inner.Hidden -= value; }
        }

        /// The old signature. sortingOrder is ignored now: a window's order comes from its band.
        public Surface(string name, float sortingOrder = 100f, FontAsset font = null)
            : this(name, Layer.Window, font) { }

        /// name is used for the GameObject, so it shows up in a hierarchy dump as "<Mod>Prism…".
        public Surface(string name, Layer layer, FontAsset font = null)
        {
            Layer = layer;
            Theme.Default.Refresh();   // a surface built outside Theme.Use wears the accent pushed so far
            Inner = new Facet.Surface(PrismHost.Instance, name, (Facet.Layer)layer, font);
            Ui.OpenWindows = Facet.Ui.OpenWindows;
        }

        public void SetFont(Font font) => Inner.SetFont(font);

        public bool Visible
        {
            get { return Inner.Visible; }
            set { Inner.Visible = value; Ui.OpenWindows = Facet.Ui.OpenWindows; }
        }

        public static void LogGeometryOnce(VisualElement e, string label) => Facet.Surface.LogGeometryOnce(e, label);

        public void BringToFront() => Inner.BringToFront();

        /// The shared monospace face, for anything columnar (a log); see Facet.Fonts.Mono.
        public static FontAsset Mono() => Facet.Fonts.Mono(PrismHost.Instance);

        public void Dispose()
        {
            Inner.Dispose();
            Ui.OpenWindows = Facet.Ui.OpenWindows;
        }
    }
}
