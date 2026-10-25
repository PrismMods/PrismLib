using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace PrismLib.UI.Toolkit
{
    /* A floating card anchored to the control that opened it — what a dropdown, context menu or
       picker needs, since a Window's Body clips everything inside it at the window edge.

       Every popup lives on one shared Surface in the Popup layer, so it draws above all windows
       whichever mod opened it. Each popup brings a transparent full-screen catcher under its card:
       a click outside closes the popup and goes no further, the way a desktop menu behaves, and
       Ui.PointerOverWindow sees the catcher, so the editor behind does not take that click either.
       Popups stack in open order; Escape and an outside click close the newest first.

       A popup also closes when the window it came from hides, or when its anchor leaves the panel
       (the page it sat on was rebuilt) — otherwise it would float over nothing. */
    public sealed class Popup
    {
        public VisualElement Content { get; }

        public bool IsOpen => _open.Contains(this);

        private readonly VisualElement _catcher;
        private readonly VisualElement _anchor;
        private readonly Action _onClose;
        private Surface _owner;
        private EventCallback<DetachFromPanelEvent> _onDetach;

        private static Surface _host;
        private static readonly List<Popup> _open = new List<Popup>();

        private const float Gap = 4f;      // between anchor and card
        private const float Margin = 8f;   // kept clear of the screen edge

        /// Open a popup under anchor (above it when there is no room below). build fills Content.
        public static Popup Open(VisualElement anchor, Action<VisualElement> build, Action onClose = null)
        {
            if (anchor == null || anchor.panel == null) { Ui.Log("Popup: anchor is not on a panel"); return null; }
            var host = Host();
            if (host == null || host.Root == null) return null;
            var p = new Popup(anchor, onClose, host.Root);
            try { build?.Invoke(p.Content); }
            catch (Exception e) { Ui.Log("Popup build threw: " + e.Message); }
            return p;
        }

        /// A list of choices. The current one is marked; picking closes the menu, then reports.
        public static Popup Menu(VisualElement anchor, IList<string> items, int selected, Action<int> onPick)
        {
            Popup menu = null;
            menu = Open(anchor, content =>
            {
                content.style.minWidth = anchor.layout.width;
                for (int i = 0; i < items.Count; i++)
                {
                    int index = i;
                    var row = Ui.Row(content);
                    row.pickingMode = PickingMode.Position;
                    row.style.height = Tokens.RowHeight;
                    row.style.paddingLeft = 8f; row.style.paddingRight = 8f;
                    Ui.SetRadius(row, 4f);
                    var label = Ui.Text(items[i], row, Tokens.FontSizeSmall, i == selected ? Tokens.Accent : Tokens.Text);
                    label.pickingMode = PickingMode.Ignore;
                    row.RegisterCallback<MouseEnterEvent>(_ => row.style.backgroundColor = Tokens.RowHover);
                    row.RegisterCallback<MouseLeaveEvent>(_ => row.style.backgroundColor = Color.clear);
                    row.RegisterCallback<PointerUpEvent>(e =>
                    {
                        if (e.button != 0) return;
                        menu?.Close();
                        try { onPick?.Invoke(index); }
                        catch (Exception ex) { Ui.Log("Popup.Menu handler threw: " + ex.Message); }
                    });
                }
            });
            return menu;
        }

        private Popup(VisualElement anchor, Action onClose, VisualElement root)
        {
            _anchor = anchor;
            _onClose = onClose;

            _catcher = new VisualElement();
            _catcher.style.position = Position.Absolute;
            _catcher.style.left = 0f; _catcher.style.top = 0f;
            _catcher.style.right = 0f; _catcher.style.bottom = 0f;   // all four, or it sizes to nothing
            _catcher.pickingMode = PickingMode.Position;
            _catcher.RegisterCallback<PointerDownEvent>(e => { e.StopPropagation(); Close(); });
            root.Add(_catcher);

            Content = Ui.Card();
            Content.style.position = Position.Absolute;
            Content.style.paddingLeft = 4f; Content.style.paddingRight = 4f;
            Content.style.paddingTop = 4f; Content.style.paddingBottom = 4f;
            Content.pickingMode = PickingMode.Position;
            // Placed once its size is known; until then it would flash at the wrong spot.
            Content.style.visibility = Visibility.Hidden;
            root.Add(Content);

            _owner = OwnerOf(anchor);
            if (_owner != null)
            {
                _owner.Hidden += Close;
                // Same face as the window it came from, which may be a mod font rather than ours.
                if (_owner.Root != null) Content.style.unityFontDefinition = _owner.Root.style.unityFontDefinition;
            }
            _onDetach = _ => Close();
            anchor.RegisterCallback(_onDetach);

            /* Match the window's scale: Window magnifies its contents with a transform, which
               worldBound includes and layout does not, so their ratio is the scale in effect. */
            float s = anchor.layout.height > 0f ? anchor.worldBound.height / anchor.layout.height : 1f;
            if (s > 0f && Mathf.Abs(s - 1f) > 0.001f)
            {
                Content.style.transformOrigin = new TransformOrigin(0f, 0f, 0f);
                Content.style.scale = new Scale(new Vector3(s, s, 1f));
            }

            EventCallback<GeometryChangedEvent> place = null;
            place = _ =>
            {
                Content.UnregisterCallback(place);
                Place(s);
            };
            Content.RegisterCallback(place);

            _open.Add(this);
            _host.Visible = true;
        }

        /* Every Prism surface shares one PanelSettings scale, so the anchor's panel coordinates are
           this surface's coordinates too — no screen round trip needed. */
        private void Place(float scale)
        {
            if (!IsOpen) return;
            var a = _anchor.worldBound;
            // The panel's own size: the host surface was only just attached and has no layout yet,
            // which clamped every first popup to the top margin.
            var screen = Content.panel != null ? Content.panel.visualTree.layout : _host.Root.layout;
            float w = Content.layout.width * scale, h = Content.layout.height * scale;

            float top = a.yMax + Gap;
            if (top + h > screen.height - Margin && a.yMin - Gap - h >= Margin) top = a.yMin - Gap - h;
            top = Mathf.Clamp(top, Margin, Mathf.Max(Margin, screen.height - h - Margin));
            float left = Mathf.Clamp(a.xMin, Margin, Mathf.Max(Margin, screen.width - w - Margin));

            Content.style.left = left;
            Content.style.top = top;
            Content.style.visibility = Visibility.Visible;
        }

        public void Close()
        {
            if (!_open.Remove(this)) return;
            if (_owner != null) _owner.Hidden -= Close;
            _anchor.UnregisterCallback(_onDetach);
            _catcher.RemoveFromHierarchy();
            Content.RemoveFromHierarchy();
            if (_open.Count == 0 && _host != null) _host.Visible = false;
            try { _onClose?.Invoke(); }
            catch (Exception e) { Ui.Log("Popup onClose threw: " + e.Message); }
        }

        /// Close every open popup — for a host tearing its UI down.
        public static void CloseAll()
        {
            for (int i = _open.Count - 1; i >= 0; i--) _open[i].Close();
        }

        // Every surface shares one panel, so ownership is ancestry, not which panel the anchor is in.
        private static Surface OwnerOf(VisualElement anchor)
        {
            for (var e = anchor; e != null; e = e.parent)
                foreach (var s in Ui.Stack)
                    if (s != _host && s.Root == e) return s;
            return null;
        }

        private static Surface Host()
        {
            if (_host != null && _host.Root != null) return _host;
            _host = new Surface("PrismPopups", Layer.Popup);
            if (_host.Root == null) return _host;
            _host.EscapeHandler = () =>
            {
                if (_open.Count == 0) return false;
                _open[_open.Count - 1].Close();
                return true;
            };
            _host.Visible = false;
            return _host;
        }
    }
}
