using System;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.TextCore.Text;

namespace PrismLib.UI.Toolkit
{
    /* A UI Toolkit panel created at runtime: a Facet.Surface made for PrismLib's host (PrismHost).
       Panels, fonts, layers, stacking, display-not-SetActive and the warm-up all live there — see
       Facet.Surface and Facet.Fonts. */
    /* Hud draws under every window and is not a window: never in Ui.Stack, never counted as open,
       never closed by Escape, never withholding input — a readout like the FPS counter. */
    public enum Layer { Hud, Window, Popup, Modal, Tooltip }

    public sealed class Surface : IDisposable
    {
        /// The Facet surface this is. Not named Facet: that would hide the namespace here.
        internal readonly Facet.Surface Inner;

        // Visible as last seen here, so a hide Facet did on its own (Escape) still raises Hidden.
        private bool _shown = true;

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

        /* Escape is Facet's (Facet.Input), so this cannot run INSTEAD of the hide: it runs once a
           Facet Escape has hidden the surface (RestartPrompt counts that as "Later"). */
        internal Func<bool> EscapeHandler;

        /// Raised when the surface hides or is disposed, by this wrapper or by Facet's Escape.
        internal event Action Hidden;

        /// The old signature. sortingOrder is ignored now: a window's order comes from its band.
        public Surface(string name, float sortingOrder = 100f, FontAsset font = null)
            : this(name, Layer.Window, font) { }

        /// name is used for the GameObject, so it shows up in a hierarchy dump as "<Mod>Prism…".
        public Surface(string name, Layer layer, FontAsset font = null)
        {
            Layer = layer;
            Theme.Default.Refresh();   // a surface built outside Theme.Use wears the Tokens pushed so far
            Inner = new Facet.Surface(PrismHost.Instance, name, (Facet.Layer)layer, font);
            Ui.Stack.Add(this);
            Ui.OpenWindows = Facet.Ui.OpenWindows;
        }

        public void SetFont(Font font) => Inner.SetFont(font);

        public bool Visible
        {
            get { return Inner.Visible; }
            set
            {
                bool was = _shown;
                Inner.Visible = value;
                _shown = Inner.Visible;
                Ui.OpenWindows = Facet.Ui.OpenWindows;
                if (was && !_shown) RaiseHidden();
            }
        }

        // ponytail: polled after Ui.HandleEscape, not every frame; a host calling only Facet.Input's
        // Escape raises Hidden at the next Prism Escape poll (every bridge polls on the same key).
        internal void Sync()
        {
            if (!_shown || Inner.Visible) return;
            _shown = false;
            Ui.OpenWindows = Facet.Ui.OpenWindows;
            var h = EscapeHandler;
            if (h != null) { try { h(); } catch (Exception e) { Ui.Log("Escape handler threw: " + e.Message); } }
            RaiseHidden();
        }

        private void RaiseHidden()
        {
            var h = Hidden;
            if (h == null) return;
            try { h(); } catch (Exception e) { Ui.Log("Surface Hidden handler threw: " + e.Message); }
        }

        public static void LogGeometryOnce(VisualElement e, string label) => Facet.Surface.LogGeometryOnce(e, label);

        public void BringToFront() => Inner.BringToFront();

        /// The shared monospace face, for anything columnar (a log); see Facet.Fonts.Mono.
        public static FontAsset Mono() => Facet.Fonts.Mono(PrismHost.Instance);

        public void Dispose()
        {
            Ui.Stack.Remove(this);
            bool was = _shown;
            _shown = false;
            Inner.Dispose();
            Ui.OpenWindows = Facet.Ui.OpenWindows;
            if (was) RaiseHidden();
        }
    }
}
