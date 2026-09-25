using System;
using System.Collections.Generic;
using UnityEngine;
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

    /* Side rail + breadcrumb, with nesting.

       This is the shell the mods' settings menus get rebuilt in. Both already have a TabRail and a
       PageStack — 530 and 398 lines between them — doing a flatter version of the same job: a list
       of pages on the left, one of them showing on the right. What they do not have is depth, which
       is why Quartz's settings are hard to find: everything competes in one flat list.

       A branch expands IN PLACE in the rail rather than replacing it. Replacing the rail is how you
       lose people — the thing they clicked vanishes and the way back is a button somewhere else.
       The breadcrumb above the content says where they are and every crumb is clickable. */
    public sealed class Nav
    {
        private readonly VisualElement _rail, _crumbs, _content;
        private readonly List<NavItem> _roots;
        private readonly List<NavItem> _path = new List<NavItem>();
        private readonly HashSet<string> _expanded = new HashSet<string>();

        private string _filter = "";
        private VisualElement _railScroll;
        private bool _railOpen = true;

        /* Search shows RESULTS, not a filtered rail. Filtering the rail answers "which page might
           hold this", which is the question the user already could not answer; a result list
           answers "here is the setting, and here is where it lives". Ported from Bismuth. */
        public void Filter(string query)
        {
            _filter = (query ?? "").Trim();
            if (_filter.Length == 0) { RebuildContent(); return; }
            ShowResults();
        }

        private void ShowResults()
        {
            _crumbs.Clear();
            _crumbs.style.paddingTop = Tokens.Gap;
            _crumbs.style.paddingBottom = Tokens.Gap;
            var title = Ui.Text("Results for \u201c" + _filter + "\u201d", _crumbs, Tokens.FontSizeTitle);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;

            _content.Clear();
            var hits = SearchIndex.Find(_filter);
            if (hits.Count == 0) { Ui.Muted("Nothing matches.", _content); return; }

            foreach (var hit in hits)
            {
                var h = hit;
                var row = Ui.Row(_content);
                row.style.minHeight = 34f;
                row.style.paddingLeft = 8f;
                row.pickingMode = PickingMode.Position;
                Ui.SetRadius(row, Tokens.Radius);

                var label = Ui.Text(h.Label, row, Tokens.FontSize);
                label.style.flexGrow = 1f;
                label.pickingMode = PickingMode.Ignore;

                // Where it lives, so the result teaches the menu rather than bypassing it.
                var where = Ui.Muted(string.IsNullOrEmpty(h.Group) ? h.Page : h.Page + " · " + h.Group, row);
                where.pickingMode = PickingMode.Ignore;

                row.RegisterCallback<MouseEnterEvent>(_ => row.style.backgroundColor = Tokens.Row);
                row.RegisterCallback<MouseLeaveEvent>(_ => row.style.backgroundColor = Color.clear);
                row.RegisterCallback<ClickEvent>(_ => GoTo(h));
            }
        }

        /// Open the page a hit lives on and flash its row, so the eye lands on the right one.
        private void GoTo(SearchIndex.Hit hit)
        {
            var page = hit.PageRef as NavItem;
            if (page == null) return;
            _filter = "";
            Go(page);
            Flash(hit.Label);
        }

        private void Flash(string label)
        {
            foreach (var e in _content.Query<VisualElement>().ToList())
            {
                if (!(e.userData is string) || (string)e.userData != label) continue;
                /* Fade a highlight out rather than in: the row is already on screen by the time
                   this runs, so starting lit and settling is what draws the eye to it. */
                e.style.backgroundColor = Tokens.Accent;
                e.experimental.animation.Start(1f, 0f, 900, (el, t) =>
                {
                    var c = Tokens.Accent;
                    c.a = t * 0.5f;
                    el.style.backgroundColor = c;
                });
                return;
            }
        }

        /* Below CollapseAt the rail is worth less than the space it costs, so it snaps shut — and
           dragging back out from nothing reopens it at a usable width rather than at one pixel. */
        public const float MinRail = 120f, MaxRail = 380f, CollapseAt = 80f;

        private void MakeRailHandle(VisualElement handle, VisualElement rail)
        {
            float startWidth = 0f;
            float startX = 0f;

            handle.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button != 0) return;
                startX = e.position.x;
                startWidth = _railOpen ? rail.resolvedStyle.width : 0f;
                handle.CapturePointer(e.pointerId);
                e.StopPropagation();
            });
            handle.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (!handle.HasPointerCapture(e.pointerId)) return;
                float w = startWidth + (e.position.x - startX);
                if (w < CollapseAt)
                {
                    _railOpen = false;
                    rail.style.display = DisplayStyle.None;
                    return;
                }
                _railOpen = true;
                rail.style.display = DisplayStyle.Flex;
                _railWidth = Mathf.Clamp(w, MinRail, MaxRail);
                rail.style.width = _railWidth;
            });
            handle.RegisterCallback<PointerUpEvent>(e =>
            {
                if (handle.HasPointerCapture(e.pointerId)) handle.ReleasePointer(e.pointerId);
            });
        }

        /// Collapse the rail to give the content the whole window.
        public void ToggleRail()
        {
            _railOpen = !_railOpen;
            if (_railScroll == null) return;
            if (_railOpen)
            {
                _railScroll.style.display = DisplayStyle.Flex;
                Anim.Width(_railScroll, _railWidth, Anim.Fast);
            }
            else
            {
                // Hidden only once it has finished closing, or it vanishes and the content jumps.
                Anim.Width(_railScroll, 0f, Anim.Fast, () => _railScroll.style.display = DisplayStyle.None);
            }
        }

        private float _railWidth = 200f;

        public bool RailOpen => _railOpen;

        public Nav(VisualElement parent, IEnumerable<NavItem> items, float railWidth = 200f)
        {
            _roots = new List<NavItem>(items ?? new NavItem[0]);

            var split = Ui.Row(parent);
            split.style.flexGrow = 1f;
            split.style.flexShrink = 1f;   // absorbs slack, like Window.Body — see its comment
            split.style.minHeight = 0f;
            split.style.alignItems = Align.Stretch;

            var railScroll = new ScrollView(ScrollViewMode.Vertical);
            railScroll.style.width = railWidth;
            _railWidth = railWidth;
            railScroll.style.flexShrink = 0f;
            railScroll.style.minHeight = 0f;
            railScroll.contentContainer.style.flexShrink = 0f;
            Skin.When<ScrollView>(railScroll, Skin.Scroll);
            split.Add(railScroll);
            _railScroll = railScroll;
            _rail = railScroll.contentContainer;

            /* A drag handle instead of a toggle button: the same gesture Sapphire's timeline uses,
               where dragging narrows the pane and collapses it once it is too small to be useful.
               One gesture does what a button plus a width setting were doing, and the width it
               lands on is remembered. */
            var handle = Ui.Box(split);
            handle.style.width = 5f;
            handle.style.flexShrink = 0f;
            handle.style.backgroundColor = Tokens.PanelBorder;
            handle.pickingMode = PickingMode.Position;
            handle.RegisterCallback<MouseEnterEvent>(_ => handle.style.backgroundColor = Tokens.Accent);
            handle.RegisterCallback<MouseLeaveEvent>(_ => handle.style.backgroundColor = Tokens.PanelBorder);
            MakeRailHandle(handle, railScroll);

            var right = Ui.Box(split);
            right.style.flexGrow = 1f;
            right.style.flexShrink = 1f;
            right.style.minHeight = 0f;
            right.style.paddingLeft = Tokens.Pad;

            /* No page title. The rail already shows which page is open, and repeating it cost a
               line of height at the top of every page to say something the eye had just read.
               The element stays for search, which has nothing else to announce itself with. */
            _crumbs = Ui.Row(right);
            _crumbs.style.backgroundColor = Tokens.Panel;
            _crumbs.style.flexShrink = 0f;

            var contentScroll = new ScrollView(ScrollViewMode.Vertical);
            contentScroll.style.flexGrow = 1f;
            contentScroll.style.minHeight = 0f;
            contentScroll.contentContainer.style.flexShrink = 0f;
            Skin.When<ScrollView>(contentScroll, Skin.Scroll);
            right.Add(contentScroll);
            _content = contentScroll.contentContainer;

            /* Siblings draw in the order they were added, so the scrolling pane was painting over
               the title that is supposed to sit above it — the rows slid under it and both stayed
               readable. Raising it is what makes the header opaque in practice as well as in
               theory. */
            _crumbs.BringToFront();

            IndexPages();

            // Open the first leaf, so the pane is never blank on arrival.
            var first = FirstLeaf(_roots);
            if (first != null) Go(first);
            else RebuildRail();
        }

        /* Pages are built on demand, so nothing would know what a page contains until it is
           opened — and a search that only finds pages you have already visited is not a search.
           Each is built once into a container that is never added to the panel. */
        private void IndexPages()
        {
            SearchIndex.Clear();
            IndexInto(_roots);
            SearchIndex.EndPage();
        }

        private void IndexInto(List<NavItem> items)
        {
            foreach (var item in items)
            {
                if (item.IsBranch) { IndexInto(item.Children); continue; }
                if (item.Build == null) continue;
                SearchIndex.BeginPage(item, item.Title);
                try { item.Build(new VisualElement()); }
                catch (Exception e) { Ui.Log("Nav: indexing '" + item.Title + "' threw: " + e.Message); }
                SearchIndex.EndPage();
            }
        }

        private NavItem FirstLeaf(List<NavItem> items)
        {
            foreach (var i in items)
            {
                if (!i.IsBranch) return i;
                var deep = FirstLeaf(i.Children);
                if (deep != null) return deep;
            }
            return null;
        }

        /// Open a page, expanding whatever contains it.
        public void Go(NavItem target)
        {
            _path.Clear();
            if (!FindPath(_roots, target, _path)) return;
            foreach (var step in _path) if (step.IsBranch) _expanded.Add(step.Title);
            RebuildRail();
            RebuildCrumbs();
            RebuildContent();
        }

        private static bool FindPath(List<NavItem> items, NavItem target, List<NavItem> path)
        {
            foreach (var i in items)
            {
                path.Add(i);
                if (i == target) return true;
                if (i.IsBranch && FindPath(i.Children, target, path)) return true;
                path.RemoveAt(path.Count - 1);
            }
            return false;
        }

        private void RebuildRail()
        {
            _rail.Clear();
            foreach (var i in _roots) AddRailItem(i, 0);
        }

        private void AddRailItem(NavItem item, int depth)
        {
            if (item.Group)
            {
                /* A heading has to be obviously NOT clickable, or it reads as a page that does
                   nothing. Upper case, smaller than the items under it, and muted — the opposite
                   of the treatment a selected item gets. */
                var heading = Ui.Text(item.Title.ToUpperInvariant(), _rail,
                                      Tokens.FontSizeSmall - 2f, Tokens.TextMuted);
                heading.style.unityFontStyleAndWeight = FontStyle.Bold;
                heading.style.letterSpacing = 1.5f;
                heading.style.marginTop = depth == 0 ? Tokens.Pad + 4f : Tokens.Gap;
                heading.style.marginBottom = 4f;
                heading.style.paddingLeft = 8f + depth * 12f;
                heading.pickingMode = PickingMode.Ignore;   // a heading is not a destination
                foreach (var c in item.Children) AddRailItem(c, depth);
                return;
            }

            bool onPath = _path.Contains(item);
            bool open = item.IsBranch && _expanded.Contains(item.Title);

            var row = Ui.Row(_rail);
            row.style.minHeight = 30f;
            row.style.paddingLeft = 10f + depth * 12f;
            row.pickingMode = PickingMode.Position;
            // The selected LEAF is the highlighted one. A branch on the path is merely open, and
            // colouring it as selected too makes it unclear which page is actually showing.
            bool selected = onPath && !item.IsBranch;
            row.style.backgroundColor = selected ? Tokens.RowHover : Color.clear;
            Ui.SetRadius(row, 4f);
            if (!string.IsNullOrEmpty(item.Tooltip)) row.tooltip = item.Tooltip;

            if (item.IsBranch)
            {
                var chev = Ui.Muted(open ? "▾" : "▸", row);
                chev.style.width = 14f;
                chev.pickingMode = PickingMode.Ignore;
            }
            else
            {
                var pad = Ui.Box(row);
                pad.style.width = depth > 0 ? 14f : 0f;
            }

            var label = Ui.Text(item.Title, row, Tokens.FontSize,
                                selected ? Tokens.Text : Tokens.TextMuted);
            label.style.flexGrow = 1f;
            label.pickingMode = PickingMode.Ignore;

            // Hover feedback: without it nothing in the rail looks clickable until you click it.
            if (!selected)
            {
                row.RegisterCallback<MouseEnterEvent>(_ =>
                {
                    row.style.backgroundColor = Tokens.Row;
                    label.style.color = Tokens.Text;
                });
                row.RegisterCallback<MouseLeaveEvent>(_ =>
                {
                    row.style.backgroundColor = Color.clear;
                    label.style.color = Tokens.TextMuted;
                });
            }

            row.RegisterCallback<ClickEvent>(_ =>
            {
                if (item.IsBranch)
                {
                    // A branch toggles open. It does not steal the content pane: the page you were
                    // reading stays until you pick another one.
                    if (!_expanded.Remove(item.Title)) _expanded.Add(item.Title);
                    RebuildRail();
                }
                else Go(item);
            });

            if (open) foreach (var c in item.Children) AddRailItem(c, depth + 1);
        }

        /// Title Case for headings. Page names come from a mod and arrive in whatever case its
        /// author used; a rail of mixed cases reads as a bug.
        private static string Title(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            var words = s.Split(' ');
            for (int i = 0; i < words.Length; i++)
                if (words[i].Length > 0) words[i] = char.ToUpperInvariant(words[i][0]) + words[i].Substring(1);
            return string.Join(" ", words);
        }

        private void RebuildCrumbs()
        {
            _crumbs.Clear();
            _crumbs.style.paddingTop = 0f;
            _crumbs.style.paddingBottom = 0f;
            if (true) return;   // titles live in the rail; see the comment where _crumbs is built
            var steps = _path.FindAll(x => !x.Group);
            for (int i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                bool last = i == steps.Count - 1;
                if (i > 0)
                {
                    var sep = Ui.Muted("›", _crumbs);
                    sep.style.marginLeft = 4f;
                    sep.style.marginRight = 4f;
                }
                var crumb = Ui.Text(Title(step.Title), _crumbs,
                                    last ? Tokens.FontSizeTitle : Tokens.FontSizeSmall,
                                    last ? Tokens.Text : Tokens.TextMuted);
                if (last) crumb.style.unityFontStyleAndWeight = FontStyle.Bold;
                if (last) continue;
                // An ancestor crumb expands its branch in the rail; there is no page of its own to
                // show, which is exactly what a branch means here.
                crumb.pickingMode = PickingMode.Position;
                crumb.RegisterCallback<ClickEvent>(_ =>
                {
                    _expanded.Add(step.Title);
                    var leaf = step.IsBranch ? FirstLeaf(step.Children) : step;
                    if (leaf != null) Go(leaf);
                });
            }
        }

        private void RebuildContent()
        {
            _content.Clear();
            if (_path.Count == 0) return;
            var page = _path[_path.Count - 1];
            if (page.Build == null) return;
            try { page.Build(_content); }
            catch (Exception e)
            {
                Ui.Log("Nav: page '" + page.Title + "' failed to build: " + e.Message);
                Ui.Muted("This page failed to build — see the log.", _content);
            }
        }

        /// Rebuild the open page in place, for a host whose values changed underneath it.
        public void Refresh() => RebuildContent();

        private int _lastRevision;

        /// Called each frame by the panel. Rebuilds the open page when its data has changed.
        public void Tick()
        {
            if (_path.Count == 0) return;
            var page = _path[_path.Count - 1];
            if (page.Revision == null) return;
            int r;
            try { r = page.Revision(); } catch { return; }
            if (r == _lastRevision) return;
            _lastRevision = r;
            RebuildContent();
        }
    }
}
