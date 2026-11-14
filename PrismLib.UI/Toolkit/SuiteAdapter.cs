using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace PrismLib.UI.Toolkit
{
    /// One page of the suite menu: a tab of its category, or the category itself when it has no tabs.
    public sealed class SuitePage
    {
        /// Stable page key, also the value stored under SuiteShell.PageStore.
        public string Key { get; set; }
        /// What the adapter's host translates.
        public string Label { get; set; }
        /// The owning feature's id, or null: a page without its own key finds its feature's tab.
        public string Feature { get; set; }
        /// Null: always.
        public Func<bool> Visible { get; set; }
        /// A search hit that lands on this tab.
        public Action<Facet.SearchIndex.Hit> OnHit { get; set; }
    }

    /// One entry of the rail.
    public sealed class SuiteCategory
    {
        public string Key { get; set; }
        public string Label { get; set; }
        /// Default: none (as NavItem.Icon).
        public Background Icon { get; set; }
        /// 0, 1 or 2 is a rail group; anything else keeps it off the rail, and Go ignores its pages.
        public int Group { get; set; }
        /// Its pages are a tab row; false: its first page is the category.
        public bool Tabs { get; set; }
        public Func<bool> Visible { get; set; }
        /// Asked on each build, never cached across one.
        public Func<IList<SuitePage>> Pages { get; set; }
        public Action<Facet.SearchIndex.Hit> OnHit { get; set; }
    }

    /* What a mod gives the suite shell: its host, pages, words and switches. The shell owns the frame. Every label is
       a stable id with an English fallback that Key turns into whatever the mod's own host translates. A member the
       shell reads from Tick or Escape (LayoutEditorShowing, KeepEscape, ReturnFromLayout, OnReturning) may throw: it
       is logged once and the shell carries on. */
    public abstract class SuiteAdapter
    {
        public abstract Facet.Host Host { get; }
        /// The geometry key is "window." + WindowId.
        public abstract string WindowId { get; }
        public abstract string Title { get; }
        public abstract string HomePage { get; }
        /// Asked at build and on every Open.
        public abstract IList<SuiteCategory> Categories();
        /// Also called on a detached panel (panel.panel == null) when the search index reads the page.
        public abstract void BuildPage(VisualElement panel, SuitePage page);
        /// The 18x18 "menu-logo" leading the title bar.
        public virtual Background? Logo { get { return null; } }
        public virtual float RailWidth { get { return 160f; } }
        /// Grows when the mod's pages change; the shell adds its own Changed count.
        public virtual int Revision { get { return 0; } }
        public virtual IEnumerable<Facet.SearchIndex.Entry> SearchRows() { yield break; }
        public virtual string Key(string id, string english) { return english; }
        public virtual float Scale { get { return 1f; } }
        public virtual float Opacity { get { return 1f; } }
        public virtual bool BlockInput { get { return true; } }
        /// A key capture owns the keyboard: Escape keeps the window.
        public virtual bool KeepEscape { get { return false; } }
        public virtual bool CanEditLayout { get { return false; } }
        /// A master switch pinned first in the title bar's right block ("menu-master"), or none. Not undoable.
        public virtual Facet.Bind<bool> Master { get { return null; } }
        /// The master switch's word, which the host translates.
        public virtual string MasterLabel { get { return null; } }
        /// Before the window hides for Edit layout.
        public virtual void OnLeaving() { }
        /// After it hides.
        public virtual void OpenLayoutEditor() { }
        public virtual bool LayoutEditorShowing { get { return false; } }
        /// Read when the layout session ends.
        public virtual bool ReturnFromLayout { get { return true; } }
        /// Just before the shell reopens after the layout wait.
        public virtual void OnReturning() { }
    }
}
