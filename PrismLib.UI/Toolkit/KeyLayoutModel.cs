using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace PrismLib.UI.Toolkit
{
    public enum LayoutGroup { Hands, Feet }

    /// One rectangle of a layout card's preview, in canvas units.
    public struct KeyLayoutBox { public Rect Rect; public Color Fill; public Color Border; public bool Dim; }

    /* A mod's key viewer layouts and the elements of the one being edited. Every method is called on the main thread,
       through Facet's Safe guard. Mutating methods change the model only: the editor wraps each in one undo step.

       An abstract class, not an interface: one PrismLib.UI loads per process, so a mod built against this version may
       meet a newer copy. A member added later is virtual with a default body, so an older model still loads. */
    public abstract class KeyLayoutModel<T> where T : class
    {
        /// Owns the switcher window and translates every label.
        public abstract Facet.Host Host { get; }

        // The last editor built over this model: the switcher opens centred over it.
        internal VisualElement EditorRoot;

        /// What Facet is handed for this id (a host key).
        public abstract string Label(string id, string english);

        /// A step stays live while this returns the reference it had when recorded.
        public abstract object UndoDocument();

        /// The whole model. Snapshot calls it before and after each change, before recording, so a model whose
        /// UndoDocument tracks structure re-owns it here.
        public abstract string Capture();

        /// Replaces the model with a capture; live press counts are kept.
        public abstract void Restore(string snapshot);

        /// After every change, undo or redo: save, ask the overlay to rebuild. The editor calls it after its own steps
        /// and canvas gestures; an undo or redo made anywhere is the model's to catch (History.AfterApply, for the
        /// model's lifetime), because the editor may not be on a panel when it happens.
        public abstract void Committed();

        /// KeyLayoutText.Snap, Grid and Guides.
        public abstract bool Pref(string id);
        public abstract void SetPref(string id, bool on);

        // ---- layouts

        public abstract IReadOnlyList<string> Layouts(LayoutGroup group);
        public abstract string Name(string layout);

        /// On the canvas.
        public abstract string Editing { get; }

        /// On screen; null when the group shows nothing.
        public abstract string Shown(LayoutGroup group);

        /// Null hides the group (Feet); a layout also becomes Editing.
        public abstract void Show(LayoutGroup group, string layout);

        /// Null while one more layout fits, else the status to show. New and Duplicate do nothing at the limit.
        public abstract string LimitText();

        /// Presets (Hands; one of Id "empty" makes an empty layout) or foot counts (Feet). The editor runs each inside a step.
        public abstract IList<Facet.MenuItem> NewItems(LayoutGroup group, Action<string> created);

        public abstract string Duplicate(string layout);
        public abstract void Delete(string layout);
        public abstract void Rename(string layout, string name);
        public abstract void Move(string layout, int indexInGroup);

        /// The switcher's "Export as DM Note Config".
        public abstract void Export(string layout);

        /// A card's preview, canvas units.
        public abstract void Thumb(string layout, List<KeyLayoutBox> into);

        // ---- the edited layout

        public abstract Vector2 CanvasSize { get; }

        /// Draw order.
        public abstract IReadOnlyList<T> Elements();

        /// The companion layout's elements: drawn faint, never hit.
        public abstract IReadOnlyList<T> Ghosts();

        /// Stable across restores and edits of fields and order.
        public abstract string Key(T e);

        /// The element with this key in any layout, or null: an undo of a gesture made on another layout finds it here.
        public abstract T Find(string key);

        public abstract Rect GetRect(T e);
        public abstract void SetRect(T e, Rect r);
        public abstract bool Hidden(T e);

        /// Fills a box the editor sizes and places. Called again on the same box to redraw it: replace what was drawn.
        public abstract void Draw(VisualElement box, T e);

        /// After New Layout Tab and Presets.
        public abstract IList<Facet.MenuItem> FileItems(KeyLayoutEditor<T> editor);
        public abstract IList<Facet.MenuItem> InsertItems(KeyLayoutEditor<T> editor);

        public abstract IReadOnlyList<T> Duplicate(IReadOnlyList<T> items);
        public abstract void Delete(IReadOnlyList<T> items);
        public abstract string Copy(IReadOnlyList<T> items);
        public abstract IReadOnlyList<T> Paste(string clip);
        public abstract bool CopyStyle(T item);
        public abstract bool CanPasteStyle { get; }
        public abstract void PasteStyle(IReadOnlyList<T> items);
        public abstract void SetOrder(IReadOnlyList<T> drawOrder);
        public abstract void SetHidden(IReadOnlyList<T> items, bool hidden);

        public abstract bool CanRebind(T e);

        /// Captures one key per item, in this order; Escape stops.
        public abstract void Rebind(KeyLayoutEditor<T> editor, IReadOnlyList<T> inOrder);

        public abstract string StatusText();
        public abstract string InspectorTitle(IReadOnlyList<T> selection);

        /// Its tabs for this selection, an empty selection included.
        public abstract IList<Facet.NavItem> Inspector(IReadOnlyList<T> selection);
    }
}
