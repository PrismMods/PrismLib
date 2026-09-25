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
            if (_filter.Length == 0)
            {
                // Back to the page, and drop the results heading with it.
                RebuildCrumbs();
                RebuildContent();
                return;
            }
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
                    PaintHandle();
                    ReportInset();
                    return;
                }
                _railOpen = true;
                rail.style.display = DisplayStyle.Flex;
                _railWidth = Mathf.Clamp(w, MinRail, MaxRail);
                rail.style.width = _railWidth;
                PaintHandle();
                ReportInset();
            });
            handle.RegisterCallback<PointerUpEvent>(e =>
            {
                if (handle.HasPointerCapture(e.pointerId)) handle.ReleasePointer(e.pointerId);
                // A click with no drag reopens a collapsed rail: dragging out from zero width is
                // fiddly, and the knob looks like something you click.
                if (!_railOpen && Mathf.Abs(e.position.x - startX) < 3f)
                {
                    _railOpen = true;
                    rail.style.display = DisplayStyle.Flex;
                    rail.style.width = _railWidth;
                    PaintHandle();
                }
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
        private VisualElement _railLine, _railKnob;

        /// Hairline while the rail is open, knob-with-arrow once it is collapsed.
        /// Where the content pane starts. The header lines its search box up with this, so the two
        /// stay aligned as the rail is resized or collapsed.
        public Action<float> OnInsetChanged;

        private void ReportInset()
        {
            var f = OnInsetChanged;
            if (f != null) f((_railOpen ? _railWidth : 0f) + 7f);
        }

        private void PaintHandle()
        {
            if (_railLine != null) _railLine.style.display = _railOpen ? DisplayStyle.Flex : DisplayStyle.None;
            if (_railKnob != null) _railKnob.style.display = _railOpen ? DisplayStyle.None : DisplayStyle.Flex;
        }

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

               Open, it is a hairline — a filled bar the height of the window reads as a scrollbar,
               which is what the first version looked like. Collapsed, it becomes a knob with an
               arrow, because a hairline says "boundary" and gives no hint that anything is hidden
               behind it. The hit area stays wider than the line either way. */
            var handle = Ui.Box(split);
            handle.style.width = 7f;
            handle.style.flexShrink = 0f;
            handle.style.alignItems = Align.Center;
            handle.style.justifyContent = Justify.Center;
            handle.pickingMode = PickingMode.Position;
            /* Resize cursor only while there is something to resize. Collapsed, the knob is a
               click-to-open affordance and a double arrow would promise the wrong gesture. */
            handle.RegisterCallback<MouseEnterEvent>(_ =>
                Cursors.Apply(_railOpen ? Cursors.Kind.ResizeHorizontal : Cursors.Kind.Default));
            handle.RegisterCallback<MouseLeaveEvent>(_ => Cursors.Apply(Cursors.Kind.Default));
            handle.RegisterCallback<DetachFromPanelEvent>(_ => Cursors.Apply(Cursors.Kind.Default));

            var line = Ui.Box(handle);
            line.style.position = Position.Absolute;
            line.style.top = 0f; line.style.bottom = 0f;
            line.style.width = 1f;
            line.style.backgroundColor = Tokens.PanelBorder;
            line.pickingMode = PickingMode.Ignore;

            var knob = Ui.Box(handle);
            knob.style.width = 14f;
            knob.style.height = 34f;
            knob.style.alignItems = Align.Center;
            knob.style.justifyContent = Justify.Center;
            knob.style.backgroundColor = Tokens.RowAlt;
            Ui.SetRadius(knob, Tokens.Radius);
            knob.style.display = DisplayStyle.None;
            knob.pickingMode = PickingMode.Ignore;
            var knobArrow = Ui.Muted("\u203A", knob);
            knobArrow.style.fontSize = Tokens.FontSize;
            knobArrow.pickingMode = PickingMode.Ignore;

            _railLine = line;
            _railKnob = knob;
            handle.RegisterCallback<MouseEnterEvent>(_ =>
            {
                line.style.backgroundColor = Tokens.Accent;
                knob.style.backgroundColor = Tokens.RowHover;
            });
            handle.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                line.style.backgroundColor = Tokens.PanelBorder;
                knob.style.backgroundColor = Tokens.RowAlt;
            });
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

            PaintHandle();
            ReportInset();
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
            _stack.Clear();
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

            Action<VisualElement> build;
            string what;
            if (_stack.Count > 0)
            {
                var top = _stack[_stack.Count - 1];
                what = top.Key;
                build = top.Value;

                // Back row, naming where it goes: "Back" alone leaves you guessing after two pushes.
                string home = _stack.Count > 1 ? _stack[_stack.Count - 2].Key
                            : _path.Count > 0 ? _path[_path.Count - 1].Title : "back";
                var back = Ui.Row(_content);
                back.style.minHeight = 30f;
                back.pickingMode = PickingMode.Position;
                var arrow = Ui.Text("\u2039  " + home, back, Tokens.FontSize, Tokens.Accent);
                arrow.pickingMode = PickingMode.Ignore;
                back.RegisterCallback<ClickEvent>(_ => Pop());

                var heading = Ui.Text(what, _content, Tokens.FontSizeTitle);
                heading.style.unityFontStyleAndWeight = FontStyle.Bold;
                heading.style.marginBottom = Tokens.Gap;
            }
            else
            {
                if (_path.Count == 0) return;
                var page = _path[_path.Count - 1];
                what = page.Title;
                build = page.Build;
            }
            if (build == null) return;

            var was = Active;
            Active = this;
            try { build(_content); }
            catch (Exception e)
            {
                Ui.Log("Nav: page '" + what + "' failed to build: " + e.Message);
                Ui.Muted("This page failed to build — see the log.", _content);
            }
            finally { Active = was; }
        }

        /* Drill-down pages.

           The mods' menus push a subpage for anything with its own settings — a stat's colours, a
           key's row — rather than nesting it in the rail, and that is the right shape: a subpage
           belongs to the row that opened it, not to the top-level navigation. The rail keeps
           showing the page you came from, and a back row leads home. */
        private readonly List<KeyValuePair<string, Action<VisualElement>>> _stack =
            new List<KeyValuePair<string, Action<VisualElement>>>();

        /// The Nav currently building a page, so Widgets.SubPage can reach it without plumbing.
        public static Nav Active { get; private set; }

        public void Push(string title, Action<VisualElement> build)
        {
            if (build == null) return;
            _stack.Add(new KeyValuePair<string, Action<VisualElement>>(title, build));
            RebuildContent();
        }

        public void Pop()
        {
            if (_stack.Count == 0) return;
            _stack.RemoveAt(_stack.Count - 1);
            RebuildContent();
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
