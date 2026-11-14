using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace PrismLib.UI.Toolkit
{
    /* The suite menu: one Facet SettingsWindow whose rail is the adapter's categories in three ruled groups and whose
       tab rows are their pages (SuiteShell.Rail). The shell owns the frame, the sidebar modes, search, the last page,
       Escape and the layout editor's round trip; the adapter owns the pages and the words. Built hidden by Prebuild,
       shown by Open, kept until Dispose. */
    public sealed partial class SuiteShell : IDisposable
    {
        public const string PageStore = "menu.page", SidebarStore = "menu.sidebar", CompactStore = "menu.compact";

        private readonly SuiteAdapter _adapter;
        private readonly Func<bool> _editorShowing;
        private Facet.SettingsWindow _sw;
        // Edit layout's wait: what it watches, and the frames and sighting SuiteModel.StepLayout keeps.
        private Func<bool> _waitShowing;
        private int _frames;
        private bool _seen;
        // The window was on screen when Edit layout began: only then does the wait's end bring it back.
        private bool _reopen;
        // One bit per adapter member that has thrown from Tick or Escape: each is logged once.
        private int _threw;

        /// Builds nothing; the host's theme takes the suite look.
        public SuiteShell(SuiteAdapter adapter)
        {
            if (adapter == null) throw new ArgumentNullException("adapter");
            _adapter = adapter;
            _editorShowing = () => _adapter.LayoutEditorShowing;   // once: the wait reads it every frame
            Suite.Wear(adapter.Host.Theme);
        }

        public SuiteAdapter Adapter { get { return _adapter; } }
        public Facet.Surface Surface { get { return _sw == null ? null : _sw.Surface; } }
        public Facet.Window Window { get { return _sw == null ? null : _sw.Window; } }
        public Facet.Nav Nav { get { return _sw == null ? null : _sw.Nav; } }
        /// The page the window last built on screen.
        public string CurrentPage { get; private set; }
        /// No rect was stored for the window when it was built.
        public bool FreshGeometry { get; private set; }
        public bool Visible { get { return _sw != null && _sw.Surface.Visible; } }
        public bool WaitingForLayout { get; private set; }

        /// Every hide but Dispose's.
        public event Action Hidden;

        private Facet.Host Host { get { return _adapter.Host; } }

        /// Builds the window hidden, on the page left last time (home when that is gone). Once.
        public void Prebuild()
        {
            if (_sw != null) return;
            var host = Host;
            // Read before the Nav opens its first page, which stores itself, and lays out the rail.
            string last = SuiteModel.LastPage(host.Read(PageStore), k => OnRail(k) != null, _adapter.HomePage);
            bool compact = SuiteModel.Compact(SuiteModel.ParseSidebar(host.Read(SidebarStore)), host.Read(CompactStore));
            var groups = BuildGroups();
            float rail = _adapter.RailWidth;
            var sw = Facet.SettingsWindow.Open(host, _adapter.WindowId, _adapter.Title, groups, o =>
            {
                o.Search = false;   // the shell shows it, for the adapter's word
                o.RailWidth = rail;
                o.CompactRail = compact;
            });
            sw.Surface.Visible = false;   // before anything listens: this hide is no one's close
            if (sw.Window == null)
            {
                sw.Dispose();   // SettingsWindow logged why
                return;
            }
            // The window's own word: it also places from the rect this session kept, which the store may not hold.
            FreshGeometry = !sw.Window.HasStoredGeometry;
            _sw = sw;
            try
            {
                Background? logo = _adapter.Logo;
                if (logo.HasValue)
                {
                    var e = new VisualElement { name = "menu-logo", pickingMode = PickingMode.Ignore };
                    e.style.width = 18f;
                    e.style.height = 18f;
                    e.style.marginRight = 6f;
                    e.style.alignSelf = Align.Center;
                    e.style.flexShrink = 0f;
                    e.style.backgroundImage = new StyleBackground(logo.Value);
                    sw.Window.TitleBarLeft.Insert(0, e);
                }
                var nav = sw.Nav;
                sw.Window.ShowSearch(_adapter.Key("suite.search", "Search"), q => nav.Filter(q));
                nav.AddSearchSource(_adapter.SearchRows);
                nav.OnCompactChanged = c => Host.Write(CompactStore, c ? "1" : null);
                nav.Collapsible = false;   // the sidebar mode alone decides whether the rail shows
                ApplySidebar();
                sw.Surface.Hidden += OnHidden;
                sw.Surface.EscapeHandler = OnEscape;
            }
            catch
            {
                sw.Dispose();   // a half-built window is never kept or shown; the next open builds afresh
                _sw = null;
                CurrentPage = null;
                TabRow = null;
                _currentFeature = null;
                _items.Clear();   // they belong to the dead Nav
                _builtCategories = null;
                throw;
            }
            if (CurrentPage != last) Go(last);
        }

        /// Shows the window, on pageKey when given. A category added or removed since the last open joins the rail first.
        public void Open(string pageKey = null)
        {
            Prebuild();
            if (_sw == null) return;
            if (CategoryKeys() != _builtCategories) RebuildRail();
            if (pageKey != null) Go(pageKey);
            ApplyLook();
            _sw.Surface.Visible = true;
        }

        public bool Hide()
        {
            if (!Visible) return false;
            _sw.Surface.Visible = false;
            return true;
        }

        /// The adapter's scale, opacity and input blocking: call it when one of them changes.
        public void ApplyLook()
        {
            if (Window == null) return;
            Window.Scale = _adapter.Scale;
            Window.Root.style.opacity = _adapter.Opacity;
            _sw.Surface.BlocksGameInput = _adapter.BlockInput;
        }

        /// "Edit layout": the window steps aside for the adapter's layout editor, and comes back when that session ends
        /// if it was showing.
        public void EditLayout() { EditLayout(_adapter.OpenLayoutEditor, _editorShowing); }

        /// The same round trip for another editor: `open` starts it and `showing` says whether it is still up.
        public void EditLayout(Action open, Func<bool> showing)
        {
            _adapter.OnLeaving();
            _waitShowing = showing ?? _editorShowing;
            _frames = 0;
            _seen = false;
            bool again = WaitingForLayout && _reopen;
            WaitingForLayout = true;   // before Hide: a Hidden listener can tell this hide apart
            _reopen = Hide() || again;   // a shut (or never-built) window stays shut when the wait ends
            if (open != null) open();
        }

        /// Per frame. Nothing while hidden, unless Edit layout is waiting to bring it back.
        public void Tick()
        {
            if (WaitingForLayout) StepLayout();
            if (_sw == null || !_sw.Surface.Visible) return;
            _sw.Tick();
        }

        // Only the session Edit layout started brings the window back: one that never appears brings it back late.
        private void StepLayout()
        {
            LayoutReturn r;
            try { r = SuiteModel.StepLayout(ref _frames, ref _seen, _waitShowing()); }
            catch (Exception e)
            {
                Threw(1, "LayoutEditorShowing", e);
                r = LayoutReturn.Back;
            }
            if (r == LayoutReturn.Wait) return;
            WaitingForLayout = false;
            _frames = 0;
            _seen = false;
            if (r == LayoutReturn.BackLate)
                Host.Log("[Menu] the layout editor did not appear within " + SuiteModel.LayoutWaitFrames + " frames of Edit layout, so the wait ends");
            if (!_reopen) return;
            bool back = true;
            try { back = _adapter.ReturnFromLayout; }
            catch (Exception e) { Threw(2, "ReturnFromLayout", e); }
            if (!back) return;
            try { _adapter.OnReturning(); }
            catch (Exception e) { Threw(4, "OnReturning", e); }
            Open();
        }

        // Facet's Escape: kept while a key capture owns the keyboard, else the window hides as it would without a handler.
        private bool OnEscape()
        {
            bool keep = false;
            try { keep = _adapter.KeepEscape; }
            catch (Exception e) { Threw(8, "KeepEscape", e); }
            if (!keep) Hide();
            return true;
        }

        private void Threw(int bit, string member, Exception e)
        {
            if ((_threw & bit) != 0) return;
            _threw |= bit;
            Host.Log("[Menu] SuiteAdapter." + member + " threw, so the shell carries on without it: " + e);
        }

        private void OnHidden()
        {
            var h = Hidden;
            if (h == null) return;
            foreach (Action a in h.GetInvocationList()) Facet.Safe.Run(Host, "SuiteShell.Hidden", a);
        }

        /// Raises no Hidden: a dying mod's window must not bring anything back behind it.
        public void Dispose()
        {
            WaitingForLayout = false;
            var sw = _sw;
            if (sw == null) return;
            sw.Surface.Hidden -= OnHidden;
            _sw = null;
            sw.Dispose();
            TabRow = null;
            CurrentPage = null;
            _currentFeature = null;
            _items.Clear();
            _builtCategories = null;
        }
    }
}
