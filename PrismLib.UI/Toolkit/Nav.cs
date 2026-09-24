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

        public NavItem(string title, Action<VisualElement> build = null) { Title = title; Build = build; }

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

        public Nav(VisualElement parent, IEnumerable<NavItem> items, float railWidth = 180f)
        {
            _roots = new List<NavItem>(items ?? new NavItem[0]);

            var split = Ui.Row(parent);
            split.style.flexGrow = 1f;
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
            bool onPath = _path.Contains(item);
            bool open = item.IsBranch && _expanded.Contains(item.Title);

            var row = Ui.Row(_rail);
            row.style.minHeight = 26f;
            row.style.paddingLeft = 6f + depth * 12f;
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

            var label = Ui.Text(item.Title, row, Tokens.FontSizeSmall,
                                selected ? Tokens.Text : Tokens.TextMuted);
            label.style.flexGrow = 1f;
            label.pickingMode = PickingMode.Ignore;

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

        private void RebuildCrumbs()
        {
            _crumbs.Clear();
            for (int i = 0; i < _path.Count; i++)
            {
                var step = _path[i];
                bool last = i == _path.Count - 1;
                if (i > 0)
                {
                    var sep = Ui.Muted("›", _crumbs);
                    sep.style.marginLeft = 4f;
                    sep.style.marginRight = 4f;
                }
                var crumb = Ui.Text(step.Title, _crumbs, Tokens.FontSizeSmall,
                                    last ? Tokens.Text : Tokens.TextMuted);
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
