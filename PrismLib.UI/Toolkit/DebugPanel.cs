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
        /// Optional. On disk — enables an "Open folder" button for this tab.
        public string Path;
        /// Optional. Enables a "Clear" button for this tab.
        public Action Clear;
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
        private Window _window;
        private Tabs _tabs2;
        private ListView _list;
        private TextField _filter;
        private Label _status;

        private readonly List<string> _rows = new List<string>();
        private VisualElement _tabActions;
        private Button _numbersBtn, _debugBtn;
        private bool _lineNumbers = true;
        private bool _showDebug = true;
        private List<DebugTab> _current = new List<DebugTab>();
        private int _active;
        private float _nextRefresh;

        /// Seconds between automatic refreshes while open. Logs move at human speed.
        public float RefreshInterval = 1f;

        /* How the filter box decides whether a line matches. Defaults to case-insensitive
           substring; a host with PrismLib.dll loaded swaps in the shared matcher so the debug
           window ranks the way every other search in the suite does. Injected rather than
           referenced — PrismLib.UI must keep working when PrismLib.dll never installed. */
        public Func<string, string, bool> Matcher =
            (line, query) => line != null && line.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;

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
                if (value)
                {
                    if (_window != null) { _window.Reclamp(); Anim.SlideIn(_window.Root, -10f, Anim.Fast); }
                    RebuildTabs();
                    Refresh();
                }
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

            _window = new Window(root, "PrismDebug", title,
                                 new Rect(60f, 50f, 980f, 560f), () => Visible = false);
            _window.OnRaise = () => _surface.BringToFront();
            var card = _window.Body;

            _tabs2 = new Tabs(card, i => { _active = i; RefreshFooter(); Refresh(); });

            _filter = new TextField { value = "" };
            _filter.style.marginBottom = Tokens.Gap;
            _filter.style.fontSize = Tokens.FontSizeSmall;
            _filter.style.flexShrink = 0f;
            Skin.When<TextField>(_filter, Skin.Field);
            _filter.RegisterValueChangedCallback(_ => Refresh());
            card.Add(_filter);

            // Before the window's own −/+/Close, which the constructor already added.
            _window.TitleBarRight.Insert(0, Ui.Btn("Refresh", Refresh));

            _list = Ui.List(() => _rows, (e, i) =>
            {
                var l = e as Label;
                if (l == null || i < 0 || i >= _rows.Count) return;
                l.text = _rows[i];
                l.style.color = Tint(_rows[i]);
            }, card);
            _list.style.overflow = Overflow.Hidden;
            // Logs are columnar: timestamps and tags only line up in a fixed pitch.
            var mono = Surface.Mono();
            if (mono != null) _list.style.unityFontDefinition = FontDefinition.FromFont(mono);

            _status = Ui.Muted("", _window.Footer);
            _status.style.flexShrink = 0f;
            _status.style.flexGrow = 1f;

            /* Footer controls: view options first, then whatever the active source offers. Both
               toggles default on — a debug view that hides things by default sends people looking
               for a bug in the log instead of in the mod. */
            var numbers = Ui.Btn("#", () => { _lineNumbers = !_lineNumbers; RefreshFooter(); Refresh(); }, _window.Footer);
            numbers.tooltip = "Line numbers";
            var dbg = Ui.Btn("dbg", () => { _showDebug = !_showDebug; RefreshFooter(); Refresh(); }, _window.Footer);
            dbg.tooltip = "Show [dbg] and [perf] lines";
            _numbersBtn = numbers; _debugBtn = dbg;
            _tabActions = Ui.Row(_window.Footer);
            RefreshFooter();
            Surface.LogGeometryOnce(_window.Root, "DebugPanel window");
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
            if (_tabs2 == null) return;
            _current = new List<DebugTab>(_tabs());
            var names = new List<string>();
            foreach (var t in _current) names.Add(t.Name);
            _tabs2.Rebuild(names);
            _active = Mathf.Max(0, _tabs2.Selected);
            RefreshFooter();
        }

        public void Refresh()
        {
            if (_list == null) return;
            if (_current.Count == 0) RebuildTabs();

            _rows.Clear();
            string q = _filter != null ? (_filter.value ?? "").Trim() : "";
            int total = 0, hidden = 0;
            if (_active >= 0 && _active < _current.Count)
            {
                var tab = _current[_active];
                IEnumerable<string> lines = null;
                // A source is somebody else's delegate reading a file: it may throw, and it must not
                // take the debug view down with it.
                try { lines = tab.Lines != null ? tab.Lines() : null; }
                catch (Exception e) { _rows.Add("<source threw: " + e.Message + ">"); }
                if (lines != null)
                {
                    int n = 0;
                    foreach (var line in lines)
                    {
                        n++; total++;
                        if (!_showDebug && IsDebug(line)) { hidden++; continue; }
                        if (q.Length > 0 && !Match(line, q)) continue;
                        // Numbered by position in the SOURCE, not in the filtered view — a line
                        // number that shifts when you type in the filter is worse than none.
                        _rows.Add(_lineNumbers ? n.ToString().PadLeft(5) + "  " + line : line);
                    }
                }
            }

            _list.itemsSource = _rows;
            _list.Rebuild();
            if (_status != null)
                _status.text = _rows.Count + " of " + total + " line(s)"
                             + (hidden > 0 ? ", " + hidden + " debug hidden" : "");
        }

        private bool Match(string line, string query)
        {
            try { return Matcher != null && Matcher(line, query); }
            catch { return false; }
        }

        private static bool IsDebug(string line)
            => line != null && (line.IndexOf("[dbg]", StringComparison.Ordinal) >= 0
                             || line.IndexOf("[perf]", StringComparison.Ordinal) >= 0);

        /// Footer buttons: the two view toggles keep their state, and the per-source actions are
        /// rebuilt because they belong to whichever tab is open.
        private void RefreshFooter()
        {
            Ui.Highlight(_numbersBtn, _lineNumbers);
            Ui.Highlight(_debugBtn, _showDebug);
            if (_tabActions == null) return;
            _tabActions.Clear();
            if (_active < 0 || _active >= _current.Count) return;
            var tab = _current[_active];
            if (tab.Clear != null)
                Ui.Btn("Clear", () => { try { tab.Clear(); } catch { } Refresh(); }, _tabActions);
            if (!string.IsNullOrEmpty(tab.Path))
                Ui.Btn("Open folder", () => Reveal(tab.Path), _tabActions);
        }

        /* Application.OpenURL on the containing directory. Deliberately not a per-mod delegate:
           every mod would implement the same Process.Start and get the quoting wrong differently,
           and this needs no shell at all. */
        private static void Reveal(string path)
        {
            try
            {
                var dir = System.IO.Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Application.OpenURL("file://" + dir);
            }
            catch (Exception e) { Ui.Log("DebugPanel: could not open folder: " + e.Message); }
        }

        public void Dispose()
        {
            if (_surface != null) _surface.Dispose();
            _surface = null; _tabs2 = null; _window = null; _list = null; _filter = null; _status = null;
        }
    }
}
