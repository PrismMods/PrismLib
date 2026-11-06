using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace PrismLib.UI.Toolkit
{
    /* A draggable, resizable, scalable window inside a Surface: a Facet.Window (drag, resize, scale,
       clamp, the undo/redo and close buttons, geometry kept in points) — see Facet.Window. */
    public sealed class Window
    {
        public const float MinWidth = 320f, MinHeight = 200f;

        /// The Facet window this is. Not named Facet: that would hide the namespace here.
        internal readonly Facet.Window Inner;

        /// The outer element. Style it as little as possible from outside; use Body.
        public VisualElement Root => Inner.Root;

        /// Where content goes. Clips at the window edge rather than painting over the chrome.
        public VisualElement Body => Inner.Body;

        /// Right-hand side of the title bar. Put toolbar buttons here.
        public VisualElement TitleBarRight => Inner.TitleBarRight;

        /// Search box in the header. Hidden until a panel calls ShowSearch.
        public TextField Search => Inner.Search;

        /// Where the content pane starts, so the header can line up with it.
        public const float RailAlign = 200f;

        /// Keep the search box lined up with a content pane whose left edge moves.
        public void SetContentInset(float inset) => Inner.SetContentInset(inset);

        public void ShowSearch(string placeholder, Action<string> onChange) => Inner.ShowSearch(placeholder, onChange);

        /// Footer strip along the bottom, above the resize grip. Always on top of Body's content.
        public VisualElement Footer => Inner.Footer;

        /// version on the left, hint on the right next to the resize grip.
        public void SetFooter(string hint, string version) => Inner.SetFooter(hint, version);

        /// Set by the panel that owns this window, to raise its surface. Read at each click.
        public Action OnRaise;

        /* Where each window was last left, through the host (PrismHost reads these): PrismLib.UI
           cannot own a settings file, so the mod supplies both. Read returns null for an unknown key. */
        public static Func<string, string> Read;
        public static Action<string, string> Write;

        /// onClose runs on the title bar's ×; without one, the × hides the window's surface (Facet's default).
        public Window(VisualElement parent, string id, string title, Rect initial, Action onClose = null)
        {
            Inner = new Facet.Window(parent, id, title, initial);
            Inner.OnRaise = () => { var r = OnRaise; if (r != null) r(); };
            if (onClose != null) Inner.CloseRequested += _ => onClose();
        }

        public float Scale
        {
            get { return Inner.Scale; }
            set { Inner.Scale = value; }
        }

        public Rect Rect
        {
            get { return Inner.Rect; }
            set { Inner.Rect = value; }
        }

        /// Re-clamp after the screen size changes. Cheap; call it when a panel becomes visible.
        public void Reclamp() => Inner.Reclamp();
    }
}
