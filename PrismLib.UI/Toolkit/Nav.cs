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

        /// Show only pages matching a query. An empty query restores the whole rail.
        public void Filter(string query)
        {
            _filter = (query ?? "").Trim();
            RebuildRail();
        }

        private bool Matches(NavItem item)
        {
            if (_filter.Length == 0) return true;
            if (item.Title != null && item.Title.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (item.Children != null) foreach (var c in item.Children) if (Matches(c)) return true;
            return false;
        }

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
            railScroll.style.flexShrink = 0f;
            railScroll.style.minHeight = 0f;
            railScroll.contentContainer.style.flexShrink = 0f;
            Skin.When<ScrollView>(railScroll, Skin.Scroll);
            split.Add(railScroll);
            _rail = railScroll.contentContainer;

            var right = Ui.Box(split);
            right.style.flexGrow = 1f;
            right.style.flexShrink = 1f;
            right.style.minHeight = 0f;
            right.style.paddingLeft = Tokens.Pad;

            _crumbs = Ui.Row(right);
            _crumbs.style.marginBottom = Tokens.Gap;

            var contentScroll = new ScrollView(ScrollViewMode.Vertical);
            contentScroll.style.flexGrow = 1f;
            contentScroll.style.minHeight = 0f;
            contentScroll.contentContainer.style.flexShrink = 0f;
            Skin.When<ScrollView>(contentScroll, Skin.Scroll);
            right.Add(contentScroll);
            _content = contentScroll.contentContainer;

            // Open the first leaf, so the pane is never blank on arrival.
            var first = FirstLeaf(_roots);
            if (first != null) Go(first);
            else RebuildRail();
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
            // A heading with no surviving children would otherwise sit over an empty gap.
            if (!Matches(item)) return;

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
    }
}
