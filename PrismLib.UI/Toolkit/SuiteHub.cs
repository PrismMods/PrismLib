using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace PrismLib.UI.Toolkit
{
    /* The small window a mod's hotkey opens: the adapter's buttons, then Settings and Close, and the hotkey named in
       the footer. Settings is a round trip: the suite menu it opens shows the hub again when it closes, once. Edit
       layout stepping the menu aside is no close, and the hotkey or Dispose ends the trip. A button with Showing makes
       the same trip through whatever it opened (Tick). Built on first Show, kept until Dispose. */
    public sealed class SuiteHub : IDisposable
    {
        // The buttons are the shared hub buttons (HubLook).
        private static readonly float ButtonWidth = HubLook.ButtonWidth, ButtonHeight = HubLook.ButtonHeight;
        private const float IconSize = 24f, MinWidth = 360f;

        private readonly SuiteShell _shell;
        private Facet.Surface _surface;
        private Facet.Window _window;
        // Every button in the column, with its Visible (null: always).
        private readonly List<KeyValuePair<VisualElement, Func<bool>>> _row = new List<KeyValuePair<VisualElement, Func<bool>>>();
        private VisualElement _rowElement;
        // The big logo over the column, with the mod's wordmark (null without an adapter logo).
        private VisualElement _logo;
        // Settings opened the menu: its next close brings the hub back.
        private bool _trip;
        // A clicked button whose Showing the hub waits on, with SuiteModel.StepLayout's frame count and sighting.
        private SuiteHubButton _waiting;
        private int _frames;
        private bool _seen;
        private bool _disposed;
        private bool _hintLogged;

        /// Builds nothing.
        public SuiteHub(SuiteShell shell)
        {
            if (shell == null) throw new ArgumentNullException("shell");
            _shell = shell;
            _shell.Hidden += OnShellHidden;
        }

        public Facet.Surface Surface { get { return _surface; } }
        public bool Visible { get { return _surface != null && _surface.Visible; } }

        private SuiteAdapter Adapter { get { return _shell.Adapter; } }
        private Facet.Host Host { get { return _shell.Adapter.Host; } }

        public void Show()
        {
            if (_disposed) return;
            if (_surface == null) Build();
            if (_window == null) return;
            foreach (var b in _row)
            {
                bool on = b.Value == null || Facet.Safe.Get(Host, "SuiteHubButton.Visible", b.Value, true);
                b.Key.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
            }
            Fit();
            SetHint();
            _surface.Visible = true;
            HubLook.FadeIn(_surface);
        }

        // The window's size from its button column: one button wide, as tall as the visible buttons (a label wrapped onto
        // more lines, here or after a relabel, makes it taller).
        private void Fit()
        {
            if (_window == null) return;   // a layout event after Dispose
            var sz = Facet.Theme.For(_window.Root).Sizes;
            float row = _rowElement.layout.height;
            float body = row > ButtonHeight ? row : ButtonHeight;   // NaN before the first layout
            if (_logo != null) body += _logo.style.height.value.value + _logo.style.marginBottom.value.value;
            Rect r = _window.Rect;
            // The body pads and the window's card border lines the column.
            float width = Mathf.Max(MinWidth, ButtonWidth + 2f * (sz.Pad + sz.Line));
            float height = sz.TitleBarHeight + body + 2f * (sz.Pad + sz.Line) + 12f + 1.6f * sz.Small;   // title bar, body, footer
            // As the window will clamp them, so a Fit that changes nothing sets no rect (each set is a store write).
            width = Mathf.Max(width, Facet.Window.MinWidth);
            height = Mathf.Max(height, Facet.Window.MinHeight);
            if (r.width == width && r.height == height) return;
            // A hub left centred stays centred as it grows or shrinks; one the player moved keeps its place.
            Vector2 panel = _surface.Root.layout.size;   // NaN before the first layout: nothing is centred yet
            bool cx = Mathf.Abs(r.center.x - panel.x / 2f) <= 1f, cy = Mathf.Abs(r.center.y - panel.y / 2f) <= 1f;
            _window.Rect = new Rect(r.x, r.y, width, height);
            if (!cx && !cy) return;
            Rect n = _window.Rect;   // the size as the window clamped it
            _window.Rect = new Rect(cx ? r.center.x - n.width / 2f : n.x, cy ? r.center.y - n.height / 2f : n.y, n.width, n.height);
        }

        public bool Hide()
        {
            if (!Visible) return false;
            _surface.Visible = false;
            return true;
        }

        /// The mod's hotkey: closes the hub and the menu, however the menu was opened, and ends any trip; with
        /// neither showing, shows the hub. While an editor is up (a button's Showing, or Edit layout's step-aside)
        /// it only ends the trip: nothing comes back, and the next press shows the hub.
        public void Toggle()
        {
            bool away = _waiting != null || _shell.WaitingForLayout;
            _trip = false;
            _waiting = null;
            _shell.EndLayoutWait();
            bool hid = Hide();
            hid |= _shell.Hide();
            if (!hid && !away) Show();
        }

        /// Per frame while a clicked button's Showing is waited on: when what it opened goes, the hub shows again.
        public void Tick()
        {
            var b = _waiting;
            if (b == null) return;
            if (SuiteModel.StepLayout(ref _frames, ref _seen, Facet.Safe.Get(Host, "SuiteHubButton.Showing", b.Showing, false)) == LayoutReturn.Wait)
                return;
            _waiting = null;
            Show();
        }

        private void Build()
        {
            var a = Adapter;
            string id = a.WindowId + ".hub";
            var surface = new Facet.Surface(Host, id, Facet.Layer.Window);
            _surface = surface;
            if (surface.Root == null)
            {
                Host.Log("[Menu] the hub has no surface root, so it shows nothing");
                return;
            }
            surface.Visible = false;   // shown by Show, once sized
            var w = new Facet.Window(surface.Root, id, a.Title, new Rect(0f, 0f, MinWidth, 160f));
            _window = w;
            w.Raised += () => surface.BringToFront();
            if (a.Logo.HasValue)
            {
                // The hub leads with the mod's logo at the shared size, its name under it, as the old hotkey hub did.
                _logo = new VisualElement { name = "hub-logo", pickingMode = PickingMode.Ignore };
                _logo.style.backgroundImage = new StyleBackground(a.Logo.Value);
                w.Body.Add(_logo);
                HubLook.Logo(_logo, a.Title == null ? null : a.Title.ToUpperInvariant());
            }
            // A column, as the old hub stood its buttons: the window stays one button wide however many a mod adds.
            var row = Facet.Ui.Box(w.Body);
            row.style.flexDirection = FlexDirection.Column;
            row.style.alignItems = Align.Center;
            row.style.flexShrink = 0f;
            _rowElement = row;
            Facet.Events.On(row, (GeometryChangedEvent _) => Fit());
            IList<SuiteHubButton> buttons = Facet.Safe.Get(Host, "SuiteAdapter.HubButtons", a.HubButtons, null);
            if (buttons != null)
                foreach (var b in buttons)
                {
                    if (b == null) continue;
                    var hb = b;
                    Add(row, "hub-" + b.Id, b.Label, b.Icon, () => Run(hb), b.Visible);
                }
            Add(row, "hub-settings", a.Key("suite.hub.settings", "Settings"), null, OpenSettings, null);
            Add(row, "hub-close", a.Key("suite.hub.close", "Close"), null, () => Hide(), null);
            HubLook.Buttons(row);
            Facet.Words.OnRelabel(w.Footer, SetHint);
            // Every mod's hotkey window wears the same look: the wash behind it, no chrome, centred labels.
            HubLook.Wear(surface, w.Root);
            HubLook.Bare(w.TitleBarRight.parent);
            HubLook.Bare(w.Footer);
            if (!w.HasStoredGeometry)
            {
                // Centred on the first layout that knows the panel's size.
                EventCallback<GeometryChangedEvent> laid = null;
                laid = _ =>
                {
                    var size = surface.Root.layout.size;
                    if (!surface.Visible || !(size.x > 0f && size.y > 0f)) return;
                    Facet.Events.Off(surface.Root, laid);
                    var r = w.Rect;
                    w.Rect = new Rect((size.x - r.width) / 2f, (size.y - r.height) / 2f, r.width, r.height);
                };
                Facet.Events.On(surface.Root, laid);
            }
        }

        // An empty-text button holding an icon and a label: a Button that gains a child loses its own text's measure.
        private void Add(VisualElement row, string name, string label, Background icon, Action click, Func<bool> visible)
        {
            var b = Facet.Ui.Btn(row, "", click);
            b.name = name;
            b.style.width = ButtonWidth;
            float gap = Facet.Theme.For(b).Sizes.Gap / 2f;   // even on both sides, so the centred row stays centred
            b.style.marginLeft = gap;
            b.style.marginRight = gap;
            b.style.minHeight = ButtonHeight;
            b.style.flexDirection = FlexDirection.Column;
            if (icon != null)
            {
                var image = new VisualElement { pickingMode = PickingMode.Ignore };
                image.style.width = IconSize;
                image.style.height = IconSize;
                image.style.marginBottom = 4f;
                image.style.backgroundImage = icon;
                b.Add(image);
            }
            var text = Facet.Ui.Text(b, label);
            text.pickingMode = PickingMode.Ignore;
            // A host's translation can be longer than the button is wide: it wraps, centred, instead of running out.
            text.style.whiteSpace = WhiteSpace.Normal;
            text.style.textOverflow = TextOverflow.Clip;
            text.style.alignSelf = Align.Stretch;
            text.style.flexShrink = 0f;
            text.style.unityTextAlign = TextAnchor.MiddleCenter;
            _row.Add(new KeyValuePair<VisualElement, Func<bool>>(b, visible));
        }

        private void Run(SuiteHubButton b)
        {
            Hide();
            _trip = false;
            _waiting = b.Showing == null ? null : b;
            _frames = 0;
            _seen = false;
            if (b.Click != null) b.Click();
        }

        private void OpenSettings()
        {
            Hide();
            _waiting = null;
            _shell.Open();
            _trip = _shell.Visible;   // armed after Open: a first build's own hide is no close
        }

        // The footer hint, formatted in the host's language: Show and every relabel set it, since it is no label's key.
        private void SetHint()
        {
            if (_window == null) return;
            string keys = Adapter.HotkeyText, hint = "";
            if (!string.IsNullOrEmpty(keys))
            {
                string english = Adapter.Key("suite.hub.hint", "{0} opens or closes this");
                try { hint = string.Format(Host.Translate(english), keys); }
                catch (FormatException)
                {
                    if (!_hintLogged) Host.Log("[Menu] the hub's hint does not format in this language, so it shows in English");
                    _hintLogged = true;
                    hint = string.Format(english, keys);
                }
            }
            _window.SetFooter(hint, null);
        }

        private void OnShellHidden()
        {
            if (!_trip || _shell.WaitingForLayout) return;   // Edit layout stepping aside: the trip stays armed
            _trip = false;
            Show();
        }

        /// Unhooks from the shell and drops the hub without bringing anything back.
        public void Dispose()
        {
            _disposed = true;
            _trip = false;
            _waiting = null;
            _shell.Hidden -= OnShellHidden;
            var s = _surface;
            _surface = null;
            _window = null;
            _rowElement = null;
            _logo = null;
            _row.Clear();
            if (s != null) s.Dispose();
        }
    }
}
