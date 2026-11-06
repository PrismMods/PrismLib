using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine.UIElements;

namespace PrismLib.UI.Toolkit
{
    /// A page in a rail. Children make it a nested rail; a leaf carries the content builder.
    public sealed class NavItem
    {
        public string Title;
        public string Tooltip;
        /// Fills the content area. Called on every visit — build fresh rather than cache, so a page
        /// cannot show a value that changed while it was hidden.
        public Action<VisualElement> Build;
        public List<NavItem> Children;

        /* For a page whose content arrives after it is built — a fetched list, a folder scan. The
           panel polls this while the page is open and rebuilds when the value changes, which is
           why the font list used to need a tab switch to appear: Refresh() returns long before the
           fetch does, and nothing asked again. */
        public Func<int> Revision;

        /* A GROUP is a heading whose children are always visible — no chevron, nothing to expand,
           the heading itself is not a page. It buys the organisation of a tree without the cost of
           one: nothing hides, so nothing has to be found twice. Prefer it. Reach for a real
           collapsible branch only when a group is long enough that showing it all is worse. */
        public bool Group;

        public NavItem(string title, Action<VisualElement> build = null) { Title = title; Build = build; }

        public static NavItem Heading(string title, params NavItem[] children)
            => new NavItem(title) { Group = true, Children = new List<NavItem>(children) };

        public NavItem With(params NavItem[] children)
        {
            Children = new List<NavItem>(children);
            return this;
        }

        public bool IsBranch => Children != null && Children.Count > 0;
    }

    /* Side rail with nesting, search and drill-down pages: Facet's Nav, fed from these NavItems.

       Facet's Nav knows its items by reference and fixes its roots when it is built, so each NavItem
       here has ONE Facet item for the Nav's life, and Refresh writes what changed into it in place (a
       child added since gets a new one). A page is built when it is opened; the first search builds
       each page once, for the index. */
    public sealed class Nav
    {
        private readonly Facet.Nav _f;
        private readonly List<NavItem> _roots;
        private readonly Dictionary<NavItem, Facet.NavItem> _mirror = new Dictionary<NavItem, Facet.NavItem>();
        private readonly Dictionary<Facet.NavItem, NavItem> _source = new Dictionary<Facet.NavItem, NavItem>();

        // Which Nav wraps a Facet one; weak, so a closed menu's Nav can go.
        private static readonly ConditionalWeakTable<Facet.Nav, Nav> Wrapping = new ConditionalWeakTable<Facet.Nav, Nav>();
        // Facet builds the first page inside its constructor, before Wrapping knows the pair.
        private static Nav _constructing;

        /// The Nav whose index SearchIndex.Find answers from: the last one built or searched.
        internal static Nav Last;

        public const float MinRail = Facet.Nav.MinRail, MaxRail = Facet.Nav.MaxRail, CollapseAt = Facet.Nav.CollapseAt;

        /// Where the content pane starts. The header lines its search box up with this, so the two
        /// stay aligned as the rail is resized or collapsed.
        public Action<float> OnInsetChanged;

        public bool RailOpen => _f.RailOpen;

        public Nav(VisualElement parent, IEnumerable<NavItem> items, float railWidth = 200f)
        {
            _roots = new List<NavItem>(items ?? new NavItem[0]);
            var was = _constructing;
            _constructing = this;
            try { using (PrismHost.Use()) _f = new Facet.Nav(parent, _roots.ConvertAll(Mirror), railWidth); }
            finally { _constructing = was; }
            Wrapping.Add(_f, this);
            _f.OnInsetChanged = inset => { var h = OnInsetChanged; if (h != null) h(inset); };
            Last = this;
        }

        /// The Nav currently building a page (Facet's Nav.Active, as this Nav), else null.
        public static Nav Active
        {
            get
            {
                var f = Facet.Nav.Active;
                Nav n;
                return f == null ? null : Wrapping.TryGetValue(f, out n) ? n : _constructing;
            }
        }

        /* The one Facet item for `p`, made on first sight and brought up to date on every call. Build
           and Revision are the caller's own delegates: Facet calls them, so a page reads Nav.Active
           and builds its rows exactly as it did. */
        private Facet.NavItem Mirror(NavItem p)
        {
            Facet.NavItem f;
            if (!_mirror.TryGetValue(p, out f)) { _mirror[p] = f = new Facet.NavItem(p.Title); _source[f] = p; }
            f.Title = p.Title; f.Tooltip = p.Tooltip; f.Build = p.Build; f.Revision = p.Revision; f.Group = p.Group;
            f.Children = p.Children == null ? null : p.Children.ConvertAll(Mirror);
            return f;
        }

        private void Sync() { foreach (var r in _roots) Mirror(r); }

        /// Search shows results, each naming the page it lives on; an empty query goes back to the page.
        public void Filter(string query)
        {
            Last = this;
            _f.Filter(query);
        }

        /// Collapse the rail to give the content the whole window.
        public void ToggleRail() => _f.ToggleRail();

        /// Open a page, expanding whatever contains it.
        public void Go(NavItem target)
        {
            if (target == null) return;
            Sync();   // a page added since the last Refresh is reachable too
            Facet.NavItem f;
            if (_mirror.TryGetValue(target, out f)) _f.Go(f);
        }

        public void Push(string title, Action<VisualElement> build) => _f.Push(title, build);

        public void Pop() => _f.Pop();

        /// Rebuild the open page and the rail in place, for a host whose values or pages changed.
        public void Refresh()
        {
            Sync();
            _f.Refresh();
        }

        /// Called each frame by the panel. Rebuilds the open page when its Revision has changed.
        public void Tick() => _f.Tick();

        internal Facet.SearchIndex Index => _f.Index;

        /// The page a search hit points at, as the caller's NavItem.
        internal object Source(object pageRef)
        {
            var f = pageRef as Facet.NavItem;
            NavItem p;
            return f != null && _source.TryGetValue(f, out p) ? p : pageRef;
        }
    }
}
