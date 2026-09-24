using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace PrismLib.UI.Toolkit
{
    /// One tab in the debug view. Lines are pulled on refresh, so a tab costs nothing while hidden.
    public sealed class DebugTab
    {
        public string Name;
        public Func<IEnumerable<string>> Lines;
    }

    /* The in-game debug view: every mod's log and state in one window, like Bismuth's log viewer
       but shared and not per-mod.

       Takes plain strings and delegates, NOT PrismLib types. That is deliberate — PrismLib.UI must
       not reference PrismLib.dll, or shipping it inside a mod folder would drag in the assembly
       that may have failed to install, and every call site here would need an Available guard
       again. The mod's bridge already talks to PrismLib.Diagnostics; it hands the results over as
       text.

       The list is a ListView, so the row count does not matter: a 50k-line log recycles the same
       twenty rows. That is the whole reason this framework is UI Toolkit and not more uGUI. */
    public sealed class DebugPanel : IDisposable
    {
        private readonly Func<IEnumerable<DebugTab>> _tabs;
        private Surface _surface;
        private VisualElement _tabBar;
        private ListView _list;
        private TextField _filter;
        private Label _status;

        private readonly List<string> _rows = new List<string>();
        private List<DebugTab> _current = new List<DebugTab>();
        private int _active;
        private float _nextRefresh;

        /// Seconds between automatic refreshes while open. Logs move at human speed.
        public float RefreshInterval = 1f;

        public DebugPanel(string title, Func<IEnumerable<DebugTab>> tabs, UnityEngine.TextCore.Text.FontAsset font = null)
        {
            _tabs = tabs ?? (() => new DebugTab[0]);
            Build(title, font);
        }

        public bool Visible
        {
            get { return _surface != null && _surface.Visible; }
            set
            {
                if (_surface == null) return;
                _surface.Visible = value;
                if (value) { RebuildTabs(); Refresh(); }
            }
        }

        public void Toggle()
        {
            Visible = !Visible;
            Ui.Log("DebugPanel: " + (Visible ? "shown" : "hidden") + ", " + _rows.Count + " row(s), "
                   + _current.Count + " tab(s)");
        }

        /// Hosts that only have a TMP font can pass the legacy Font it was built from
        /// (TMP_FontAsset.sourceFontFile) — UI Toolkit draws through TextCore, not TMP.
        public void SetFont(UnityEngine.Font font)
        {
            if (_surface != null) _surface.SetFont(font);
        }

        /// Call every frame while the mod is alive; it does nothing while hidden.
        public void Tick()
        {
            if (!Visible) return;
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + RefreshInterval;
            Refresh();
        }

        private void Build(string title, UnityEngine.TextCore.Text.FontAsset font)
        {
            /* Above the game's own canvases. UI Toolkit panels order against uGUI by the same
               number, and at 200 this sat behind anything the game drew full-screen. Sapphire's
               update toast is at 32700, so stay under that. */
            _surface = new Surface("PrismDebugPanel", 30000f, font);
            var root = _surface.Root;
            if (root == null) return;

            var card = Ui.Card(root);
            card.pickingMode = PickingMode.Position;   // the panel catches input; the surface does not
            /* Inset from all four edges rather than a fixed width and height. An absolutely
               positioned element with only left/top is sized by its content, and a log line is as
               wide as it likes — the first build spread the card across the whole screen. Four
               offsets pin it, and a debug window wants the room anyway. */
            card.style.position = Position.Absolute;
            card.style.left = 40f; card.style.top = 40f;
            card.style.right = 40f; card.style.bottom = 40f;

            var header = Ui.Row(card);
            header.style.marginBottom = Tokens.Gap;
            var t = Ui.Text(title, header, Tokens.FontSize + 1f);
            t.style.flexGrow = 1f;
            Ui.Btn("Refresh", Refresh, header);
            Ui.Btn("Close", () => Visible = false, header);

            _tabBar = Ui.Row(card);
            _tabBar.style.marginBottom = Tokens.Gap;
            _tabBar.style.flexWrap = Wrap.Wrap;

            _filter = new TextField { value = "" };
            _filter.style.marginBottom = Tokens.Gap;
            _filter.style.fontSize = Tokens.FontSizeSmall;
            _filter.style.flexShrink = 0f;
            _filter.RegisterValueChangedCallback(_ => Refresh());
            card.Add(_filter);

            _list = Ui.List(() => _rows, (e, i) =>
            {
                var l = e as Label;
                if (l == null || i < 0 || i >= _rows.Count) return;
                l.text = _rows[i];
                l.style.color = Tint(_rows[i]);
            }, card);

            _status = Ui.Muted("", card);
            _status.style.flexShrink = 0f;
            Surface.LogGeometryOnce(card, "DebugPanel card");
            _surface.Visible = false;
        }

        // Cheap severity colouring. Substring checks, not a parser: every mod formats its log
        // differently and guessing wrong must not cost more than a wrong colour.
        private static Color Tint(string line)
        {
            if (line == null) return Tokens.Text;
            if (line.IndexOf("Exception", StringComparison.OrdinalIgnoreCase) >= 0
                || line.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0
                || line.IndexOf("FAIL", StringComparison.Ordinal) >= 0) return Tokens.Danger;
            if (line.IndexOf("[dbg]", StringComparison.Ordinal) >= 0
                || line.IndexOf("[perf]", StringComparison.Ordinal) >= 0) return Tokens.TextMuted;
            return Tokens.Text;
        }

        private void RebuildTabs()
        {
            if (_tabBar == null) return;
            _current = new List<DebugTab>(_tabs());
            _tabBar.Clear();
            if (_active >= _current.Count) _active = 0;
            for (int i = 0; i < _current.Count; i++)
            {
                int idx = i;
                var b = Ui.Btn(_current[i].Name, () => { _active = idx; RebuildTabs(); Refresh(); }, _tabBar);
                if (idx == _active)
                {
                    b.style.backgroundColor = Tokens.Accent;
                    b.style.color = new Color(0.06f, 0.06f, 0.08f, 1f);
                }
            }
        }

        public void Refresh()
        {
            if (_list == null) return;
            if (_current.Count == 0) RebuildTabs();

            _rows.Clear();
            string q = _filter != null ? (_filter.value ?? "").Trim() : "";
            if (_active >= 0 && _active < _current.Count)
            {
                var tab = _current[_active];
                IEnumerable<string> lines = null;
                // A source is somebody else's delegate reading a file: it may throw, and it must not
                // take the debug view down with it.
                try { lines = tab.Lines != null ? tab.Lines() : null; }
                catch (Exception e) { _rows.Add("<source threw: " + e.Message + ">"); }
                if (lines != null)
                    foreach (var line in lines)
                        if (q.Length == 0 || (line != null && line.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0))
                            _rows.Add(line);
            }

            _list.itemsSource = _rows;
            _list.Rebuild();
            if (_status != null)
                _status.text = _rows.Count + (q.Length > 0 ? " matching line(s)" : " line(s)");
        }

        public void Dispose()
        {
            if (_surface != null) _surface.Dispose();
            _surface = null; _tabBar = null; _list = null; _filter = null; _status = null;
        }
    }
}
