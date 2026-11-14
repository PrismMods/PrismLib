using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.UIElements;

namespace PrismLib.UI.Toolkit
{
    /* The rail and its pages. Three ruled groups built once for the shell's life, so whoever holds Nav reads live
       state; a category added or removed refills the groups in place. A category with Tabs is a tab row of its pages,
       one without is its first shown page. A Visible that throws hides its category or page, wherever it is asked. */
    public sealed partial class SuiteShell
    {
        /// The open category's tab row, or null.
        public Facet.Tabs TabRow { get; private set; }

        private Facet.NavItem[] _groups;
        // Rail items by category key, kept while the category stays so the Nav's path to it survives a refill.
        private readonly Dictionary<string, Facet.NavItem> _items = new Dictionary<string, Facet.NavItem>();
        // The latest category behind each key: an item's closures look it up, as the adapter may hand out new objects.
        private readonly Dictionary<string, SuiteCategory> _cats = new Dictionary<string, SuiteCategory>();
        private string _builtCategories;
        // The open page's feature, kept beside it: a tab row without the page still finds the feature's tab.
        private string _currentFeature;
        // The page Go asked for, while the Nav builds its category.
        private SuitePage _going;
        private int _changes;

        private static readonly IList<SuitePage> NoPages = new SuitePage[0];

        /// Something the pages show changed that the adapter's Revision does not count: the open page rebuilds.
        public void Changed() { _changes++; }

        /// Opens a page: its category in the rail, and its tab there. Nothing for an unknown key or an off-rail page.
        public void Go(string pageKey) { GoTo(pageKey); }

        private bool GoTo(string pageKey)
        {
            SuiteCategory c;
            SuitePage page = OnRail(pageKey, out c);
            Facet.NavItem item;
            if (Nav == null || page == null || !_items.TryGetValue(c.Key, out item)) return false;
            _going = page;
            try { Nav.Go(item); }
            finally { _going = null; }
            return true;
        }

        /// Refills the rail from the adapter's categories on the same Nav, on the page it showed (home when that went).
        public void RebuildRail()
        {
            if (Nav == null) return;
            string kept = CurrentPage;
            FillGroups();
            _changes++;   // every item's Revision moves, so the search index rebuilds
            if (kept == null || OnRail(kept) == null) kept = _adapter.HomePage;
            if (!GoTo(kept)) Nav.Rebuild();
        }

        /// The Sidebar setting: 0 can be folded (a button folds the rail to its icons, and the choice is kept),
        /// 1 always shown, 2 icons only. Stored, and applied at once.
        public int SidebarMode
        {
            get { return SuiteModel.ParseSidebar(Host.Read(SidebarStore)); }
            set
            {
                Host.Write(SidebarStore, SuiteModel.FormatSidebar(value));
                ApplySidebar();
            }
        }

        public void ApplySidebar()
        {
            if (Nav == null) return;
            int mode = SidebarMode;
            Nav.CompactButton = mode == 0;
            Nav.Compact = SuiteModel.Compact(mode, Host.Read(CompactStore));
        }

        private Facet.NavItem[] BuildGroups()
        {
            _groups = new Facet.NavItem[3];
            for (int i = 0; i < 3; i++)
            {
                var g = Facet.NavItem.Heading(null);
                g.VisibleWhen = () => g.Children.Count > 0;   // an empty group is no leaf to open
                _groups[i] = g;
            }
            FillGroups();
            return _groups;
        }

        private void FillGroups()
        {
            foreach (var g in _groups) g.Children.Clear();
            _cats.Clear();
            var keep = new Dictionary<string, Facet.NavItem>();
            foreach (var c in Categories())
            {
                if (c == null || c.Key == null || c.Group < 0 || c.Group > 2 || _cats.ContainsKey(c.Key)) continue;
                _cats[c.Key] = c;
                Facet.NavItem item;
                if (!_items.TryGetValue(c.Key, out item)) item = CategoryItem(c.Key);
                item.Title = c.Label;
                item.Icon = c.Icon;
                keep[c.Key] = item;
                _groups[c.Group].Children.Add(item);
            }
            _items.Clear();
            foreach (var kv in keep) _items[kv.Key] = kv.Value;
            _builtCategories = CategoryKeys();
        }

        private IList<SuiteCategory> Categories()
        {
            return _adapter.Categories() ?? new SuiteCategory[0];
        }

        private string CategoryKeys()
        {
            var b = new StringBuilder();
            foreach (var c in Categories())
                if (c != null) b.Append(c.Key).Append(':').Append(c.Group).Append(',');
            return b.ToString();
        }

        private static IList<SuitePage> Pages(SuiteCategory c)
        {
            return c == null || c.Pages == null ? NoPages : c.Pages() ?? NoPages;
        }

        private SuitePage OnRail(string key)
        {
            SuiteCategory c;
            return OnRail(key, out c);
        }

        // A live search of the adapter's categories: the page under that key whose category is on the rail.
        private SuitePage OnRail(string key, out SuiteCategory category)
        {
            category = null;
            if (key == null) return null;
            foreach (var c in Categories())
            {
                if (c == null || c.Group < 0 || c.Group > 2) continue;
                foreach (var p in Pages(c))
                    if (p != null && p.Key == key && Shown(p)) { category = c; return p; }
            }
            return null;
        }

        private bool Shown(SuitePage p)
        {
            return p != null && (p.Visible == null || Facet.Safe.Get(Host, "SuitePage.Visible", p.Visible, false));
        }

        private SuitePage FirstShown(SuiteCategory c)
        {
            foreach (var p in Pages(c)) if (Shown(p)) return p;
            return null;
        }

        private Facet.NavItem CategoryItem(string key)
        {
            var item = new Facet.NavItem(null, p => BuildCategory(p, Cat(key)));
            item.VisibleWhen = () =>
            {
                var c = Cat(key);
                return c != null && (c.Visible == null || Facet.Safe.Get(Host, "SuiteCategory.Visible", c.Visible, false)) && FirstShown(c) != null;
            };
            item.Revision = () => _adapter.Revision + _changes;
            // A category without tabs is its one page, which can change under it: asked when the hit lands.
            item.OnHit = h =>
            {
                var c = Cat(key);
                if (c == null) return;
                if (c.OnHit != null) { c.OnHit(h); return; }
                var first = c.Tabs ? null : FirstShown(c);
                if (first != null && first.OnHit != null) first.OnHit(h);
            };
            return item;
        }

        private SuiteCategory Cat(string key)
        {
            SuiteCategory c;
            return _cats.TryGetValue(key, out c) ? c : null;
        }

        private void BuildCategory(VisualElement p, SuiteCategory c)
        {
            if (c == null || !c.Tabs)
            {
                if (p.panel != null) TabRow = null;
                var first = FirstShown(c);
                if (first != null) BuildPage(p, first);
                return;
            }
            var pages = Pages(c);
            var keyed = new List<KeyValuePair<string, string>>(pages.Count);
            var items = new List<Facet.NavItem>(pages.Count);
            foreach (var pg in pages)
            {
                if (pg == null) continue;
                var page = pg;
                keyed.Add(new KeyValuePair<string, string>(page.Key, page.Feature));
                items.Add(new Facet.NavItem(page.Label, b => BuildPage(b, page)) { VisibleWhen = page.Visible == null ? null : (Func<bool>)(() => Shown(page)), OnHit = page.OnHit });
            }
            int tab = _going != null
                ? SuiteModel.TabIndex(keyed, _going.Key, _going.Feature)
                : SuiteModel.TabIndex(keyed, CurrentPage, _currentFeature);
            var row = Facet.Tabs.Pages(p, items, tab);
            if (p.panel != null) TabRow = row;
        }

        private void BuildPage(VisualElement p, SuitePage page)
        {
            if (p.panel != null)   // on screen, not the search index's throwaway
            {
                CurrentPage = page.Key;
                _currentFeature = page.Feature;
                Host.Write(PageStore, page.Key);
            }
            _adapter.BuildPage(p, page);
        }
    }
}
