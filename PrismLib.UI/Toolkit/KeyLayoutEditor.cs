using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace PrismLib.UI.Toolkit
{
    /* A key viewer layout editor over a KeyLayoutModel: a menu bar with a Layouts button, a Hands and a Feet tab strip,
       the canvas, the inspector and a status line, stacked in Root. Every change is one undo step over the model's
       Capture (Do); the canvas's own gestures record their own steps. */
    public sealed partial class KeyLayoutEditor<T> where T : class
    {
        private const float ZoomStep = 0.1f, SayFor = 4f;

        // Copy keeps its text here, not on the system clipboard, so Paste outlives a rebuilt page within the session.
        private static string _clip;

        private readonly KeyLayoutModel<T> _model;
        private readonly Action _openSwitcher;
        private readonly Action _afterApply;
        private Facet.MenuItem[] _menus;
        // The chorded items Tick runs; bare keys stay the canvas's, and Undo/Redo the host's undo chord.
        private readonly List<Facet.MenuItem> _keyMenus = new List<Facet.MenuItem>();
        private List<Facet.MenuItem> _onItem, _onStage;
        private Label _zoom;

        public VisualElement Root { get; private set; }
        public Facet.Canvas<T> Canvas { get; private set; }
        public Facet.MenuBar Menu { get; private set; }
        public Facet.StatusLine Status { get; private set; }

        /// True flips the wheel's pan on both axes (Facet's Canvas.InvertWheelPan, read at each step); null or false
        /// keeps Facet's direction.
        public Func<bool> InvertPan { get => Canvas.InvertWheelPan; set => Canvas.InvertWheelPan = value; }

        /// The bar's five top items, for checks.
        public IReadOnlyList<Facet.MenuItem> Menus => _menus;

        private KeyLayoutEditor(KeyLayoutModel<T> model, Action openSwitcher)
        {
            _model = model;
            _openSwitcher = openSwitcher;
            _afterApply = Refresh;
        }

        public static KeyLayoutEditor<T> Build(VisualElement parent, KeyLayoutModel<T> model, Action openSwitcher)
        {
            if (parent == null) throw new ArgumentNullException("parent");
            if (model == null) throw new ArgumentNullException("model");
            var ed = new KeyLayoutEditor<T>(model, openSwitcher);
            ed.Frame(parent);
            return ed;
        }

        private void Frame(VisualElement parent)
        {
            Root = new VisualElement { name = "kle-root" };
            Root.style.flexDirection = FlexDirection.Column;
            Root.style.flexGrow = 1f;
            Root.style.flexShrink = 1f;   // in a filled column the inspector's scroll gets only what is left
            Root.style.minHeight = 0f;
            parent.Add(Root);
            _model.EditorRoot = Root;

            _menus = BuildMenus();
            Menu = Facet.Controls.MenuBar(Root, _menus);
            Menu.Root.name = "kle-menubar";
            var layouts = Facet.Ui.Btn(Menu.Left, L(KeyLayoutText.Layouts), () => { if (_openSwitcher != null) _openSwitcher(); });
            layouts.name = "kle-layouts";

            BuildStrip();
            BuildCanvas();
            BuildTools();
            BuildInspector();
            Status = Facet.Controls.Status(Root);
            Status.Root.name = "kle-status";
            Status.Show(() => _model.StatusText());

            // The model commits every undo itself; the editor only has to show it, and catch up on what it missed off a panel.
            Facet.Ui.WhileAttached(Root, () => { Facet.History.AfterApply += _afterApply; Refresh(); }, () => Facet.History.AfterApply -= _afterApply);
            Refresh();
        }

        private string L(string id) => _model.Label(id, KeyLayoutText.English(id));

        // ---- steps

        /// One step: Step(model, …), then Committed and Refresh.
        public void Do(string labelId, Action change)
        {
            Step(_model, labelId, change);
            _model.Committed();
            Refresh();
        }

        /// One undo step over the model's capture, bound to its UndoDocument.
        public static void Step(KeyLayoutModel<T> model, string labelId, Action change)
        {
            Facet.History.Snapshot<string>(model.Label(labelId, KeyLayoutText.English(labelId)), null, model.Capture, model.Restore,
                change, Facet.History.BoundTo(model.UndoDocument), model.Host.Translate);
        }

        /// A tab or card click as one undo step by value: what each group shows and which layout is edited, not a capture of
        /// the whole model (a layout with embedded images would copy megabytes per click). Show(group, layout) inside it.
        public static void Pick(KeyLayoutModel<T> model, string labelId, LayoutGroup group, string layout)
        {
            Facet.History.Snapshot<Picked>(model.Label(labelId, KeyLayoutText.English(labelId)), null, () => Picked.Of(model),
                p => p.Apply(model), () => model.Show(group, layout), Facet.History.BoundTo(model.UndoDocument), model.Host.Translate);
        }

        private void Pick(string labelId, LayoutGroup group, string layout)
        {
            Pick(_model, labelId, group, layout);
            _model.Committed();
            Refresh();
        }

        private struct Picked : IEquatable<Picked>
        {
            private string _hands, _feet, _editing;

            public static Picked Of(KeyLayoutModel<T> m)
            {
                return new Picked { _hands = m.Shown(LayoutGroup.Hands), _feet = m.Shown(LayoutGroup.Feet), _editing = m.Editing };
            }

            // Show also makes its layout the edited one, so the group holding the edited layout goes last.
            public void Apply(KeyLayoutModel<T> m)
            {
                bool feetEdited = _editing != null && _editing == _feet;
                if (feetEdited) m.Show(LayoutGroup.Hands, _hands);
                m.Show(LayoutGroup.Feet, _feet);
                if (!feetEdited) m.Show(LayoutGroup.Hands, _hands);
            }

            public bool Equals(Picked o) { return _hands == o._hands && _feet == o._feet && _editing == o._editing; }
        }

        /// A 4 s status over the resting line; text is already translated.
        public void Say(string text, Facet.Role role = Facet.Role.TextMuted)
        {
            string shown = text;
            Status.Show(() => shown, role, SayFor);
        }

        public void Select(IEnumerable<T> items)
        {
            if (items == null) return;
            Canvas.Select(items);
        }

        // At the layout limit: says why and refuses.
        private bool Limited()
        {
            string t = _model.LimitText();
            if (t == null) return false;
            Say(t, Facet.Role.Danger);
            return true;
        }

        private T[] Sel() => Canvas.Selection.ToArray();

        // The canvas captures whichever pointer pressed it (mouse, pen or touch).
        private bool CanvasHeld()
        {
            for (int id = 0; id < PointerId.maxPointers; id++)
                if (Canvas.Stage.HasPointerCapture(id)) return true;
            return false;
        }

        /// Every frame. Keys only while Root is shown: the canvas's own keys first, then the chorded menu items.
        public bool Tick(Func<KeyCode, bool> down, Func<KeyCode, bool> held, bool ctrlOrCmd, bool shift, bool alt)
        {
            if (Root == null || Root.panel == null || Root.resolvedStyle.display == DisplayStyle.None) return false;
            if (_dirty) { _dirty = false; RedrawSelection(); }
            if (Canvas.PollKeys(down, held)) return true;
            // Fit has no canvas key of its own; like the canvas's keys it needs the canvas focused, no key capture and no
            // drag in progress (a canvas drag holds the pointer on its stage).
            if (!ctrlOrCmd && !shift && !alt && down != null && Canvas.Focused && !Facet.Input.TextFocused
                && Facet.KeyCapture.Owner == null && !CanvasHeld() && down(KeyCode.F))
            {
                Canvas.Fit();
                return true;
            }
            return Facet.Menus.HandleKeys(Root, _keyMenus, down, ctrlOrCmd, shift, alt);
        }

        // ---- menus

        private Facet.MenuItem Item(string id, string shortcut, Action run, Func<bool> enabled = null)
            => new Facet.MenuItem { Id = id, Text = L(id), Shortcut = shortcut, Run = run, Enabled = enabled };

        // A model's New item, run as one step and refused at the layout limit.
        private Facet.MenuItem Wrapped(Facet.MenuItem it, string labelId)
        {
            if (it == null || it.IsSeparator) return it;
            var run = it.Run;
            return new Facet.MenuItem
            {
                Id = it.Id, Text = it.Text, TextOf = it.TextOf, Shortcut = it.Shortcut, Enabled = it.Enabled, Checked = it.Checked,
                Danger = it.Danger, Items = it.Items == null ? null : it.Items.Select(s => Wrapped(s, labelId)).ToList(),
                Run = run == null ? null : (Action)(() => { if (!Limited()) Do(labelId, run); }),
            };
        }

        private bool Any() => Canvas.Selection.Count > 0;

        private Facet.MenuItem[] BuildMenus()
        {
            Func<bool> any = Any, two = () => Canvas.Selection.Count >= 2, three = () => Canvas.Selection.Count >= 3;

            var hands = _model.NewItems(LayoutGroup.Hands, _ => { }) ?? new List<Facet.MenuItem>();
            var empty = hands.FirstOrDefault(i => i != null && i.Id == "empty");
            var file = new List<Facet.MenuItem>();
            var newLayout = Wrapped(empty ?? new Facet.MenuItem(), KeyLayoutText.NewLayout);
            newLayout.Id = KeyLayoutText.NewLayout;
            newLayout.Text = L(KeyLayoutText.NewLayout);
            newLayout.TextOf = null;
            file.Add(newLayout);
            file.Add(new Facet.MenuItem
            {
                Id = KeyLayoutText.Presets, Text = L(KeyLayoutText.Presets),
                Items = hands.Where(i => i == null || i.Id != "empty").Select(i => Wrapped(i, KeyLayoutText.NewFromPreset)).ToList(),
            });
            file.Add(Facet.MenuItem.Separator);
            AddAll(file, _model.FileItems(this));

            var cut = Item(KeyLayoutText.Cut, "Ctrl+X", () =>
            {
                var sel = Sel();
                _clip = _model.Copy(sel);
                Do(KeyLayoutText.Cut, () => { _model.Delete(sel); Canvas.Clear(); });
            }, any);
            var copy = Item(KeyLayoutText.Copy, "Ctrl+C", () => _clip = _model.Copy(Sel()), any);
            var paste = Item(KeyLayoutText.Paste, "Ctrl+V", () =>
            {
                string clip = _clip;
                IReadOnlyList<T> made = null;
                Do(KeyLayoutText.Paste, () => made = _model.Paste(clip));
                Select(made);
            }, () => _clip != null);
            var duplicate = Item(KeyLayoutText.Duplicate, "Ctrl+D", () =>
            {
                var sel = Sel();
                IReadOnlyList<T> made = null;
                Do(KeyLayoutText.Duplicate, () => made = _model.Duplicate(sel));
                Select(made);
            }, any);
            var delete = Item(KeyLayoutText.Delete, "Delete", () =>
            {
                var sel = Sel();
                Do(KeyLayoutText.Delete, () => { _model.Delete(sel); Canvas.Clear(); });
            }, any);
            var copyStyle = Item(KeyLayoutText.CopyStyle, "Ctrl+Shift+C", () => _model.CopyStyle(Canvas.Primary), () => Canvas.Primary != null);
            var pasteStyle = Item(KeyLayoutText.PasteStyle, "Ctrl+Shift+V", () =>
            {
                var sel = Sel();
                Do(KeyLayoutText.PasteStyle, () => _model.PasteStyle(sel));
            }, () => _model.CanPasteStyle && Any());
            var rebind = Item(KeyLayoutText.Rebind, null, () => _model.Rebind(this, Sel()),
                () => Canvas.Selection.Count == 1 && _model.CanRebind(Canvas.Selection[0]));
            var edit = new List<Facet.MenuItem>
            {
                Item(KeyLayoutText.Undo, "Ctrl+Z", () => Facet.History.Undo(), () => Facet.History.CanUndo),
                Item(KeyLayoutText.Redo, "Ctrl+Shift+Z", () => Facet.History.Redo(), () => Facet.History.CanRedo),
                Facet.MenuItem.Separator, cut, copy, paste, duplicate, delete,
                Facet.MenuItem.Separator, copyStyle, pasteStyle,
                Facet.MenuItem.Separator, Item(KeyLayoutText.SelectAll, "Ctrl+A", () => Canvas.SelectAll()),
                Facet.MenuItem.Separator, rebind,
                Item(KeyLayoutText.RebindInOrder, null, RebindInOrder, () => Canvas.Selection.Any(_model.CanRebind)),
            };

            var insert = new List<Facet.MenuItem>();
            AddAll(insert, _model.InsertItems(this));

            var arrange = new List<Facet.MenuItem>
            {
                Item(KeyLayoutText.ToFront, "Ctrl+]", () => ToEnd(KeyLayoutText.ToFront, true), any),
                Item(KeyLayoutText.ToBack, "Ctrl+[", () => ToEnd(KeyLayoutText.ToBack, false), any),
                Facet.MenuItem.Separator,
                Item(KeyLayoutText.AlignLeft, null, () => Align(KeyLayoutText.AlignLeft, KeyLayoutMath.Edge.Left), two),
                Item(KeyLayoutText.AlignRight, null, () => Align(KeyLayoutText.AlignRight, KeyLayoutMath.Edge.Right), two),
                Item(KeyLayoutText.AlignTop, null, () => Align(KeyLayoutText.AlignTop, KeyLayoutMath.Edge.Top), two),
                Item(KeyLayoutText.AlignBottom, null, () => Align(KeyLayoutText.AlignBottom, KeyLayoutMath.Edge.Bottom), two),
                Item(KeyLayoutText.SpreadH, null, () => Arrange(KeyLayoutText.SpreadH, b => KeyLayoutMath.Spread(b, true)), three),
                Item(KeyLayoutText.SpreadV, null, () => Arrange(KeyLayoutText.SpreadV, b => KeyLayoutMath.Spread(b, false)), three),
                Facet.MenuItem.Separator,
                Item(KeyLayoutText.Hide, null, () => { var sel = Sel(); Do(KeyLayoutText.Hide, () => _model.SetHidden(sel, true)); }, any),
                Item(KeyLayoutText.Show, null, () => { var sel = Sel(); Do(KeyLayoutText.Show, () => _model.SetHidden(sel, false)); }, any),
            };

            var view = new List<Facet.MenuItem>
            {
                Item(KeyLayoutText.ZoomIn, "+", () => Canvas.ZoomBy(ZoomStep)),
                Item(KeyLayoutText.ZoomOut, "-", () => Canvas.ZoomBy(-ZoomStep)),
                Item(KeyLayoutText.ZoomReset, "0", () => Canvas.ResetView()),
                Item(KeyLayoutText.Fit, "F", () => Canvas.Fit()),
                Facet.MenuItem.Separator,
                PrefItem(KeyLayoutText.Snap, v => Canvas.Snap = v),
                PrefItem(KeyLayoutText.Grid, v => Canvas.ShowGrid = v),
                PrefItem(KeyLayoutText.Guides, v => Canvas.ShowGuides = v),
            };

            // The right-click menus: on an item, Rebind, Edit's Cut to Paste Style, then Arrange; on the stage, Paste and Select All.
            _onItem = new List<Facet.MenuItem>();
            for (int i = edit.IndexOf(cut); i <= edit.IndexOf(pasteStyle); i++) _onItem.Add(edit[i]);
            _onItem.Add(Facet.MenuItem.Separator);
            _onItem.AddRange(arrange);
            _onStage = new List<Facet.MenuItem> { paste, Item(KeyLayoutText.SelectAll, null, () => Canvas.SelectAll()) };

            var menus = new[]
            {
                Top(KeyLayoutText.File, file), Top(KeyLayoutText.Edit, edit), Top(KeyLayoutText.Insert, insert),
                Top(KeyLayoutText.Arrange, arrange), Top(KeyLayoutText.View, view),
            };
            for (int i = 0; i < menus.Length; i++) Chorded(menus[i].Items);
            return menus;
        }

        private Facet.MenuItem Top(string id, IList<Facet.MenuItem> items) => new Facet.MenuItem { Id = id, Text = L(id), Items = items };

        private static void AddAll(List<Facet.MenuItem> to, IList<Facet.MenuItem> items)
        {
            if (items == null) return;
            for (int i = 0; i < items.Count; i++) to.Add(items[i]);
        }

        private Facet.MenuItem PrefItem(string id, Action<bool> apply)
        {
            var it = Item(id, null, () => { bool v = !_model.Pref(id); _model.SetPref(id, v); apply(v); });
            it.Checked = () => _model.Pref(id);
            return it;
        }

        // Ctrl chords only: a bare Delete or F must not act while the player works a slider elsewhere on the page.
        private void Chorded(IList<Facet.MenuItem> items)
        {
            if (items == null) return;
            foreach (var it in items)
            {
                if (it == null) continue;
                if (it.Items != null) { Chorded(it.Items); continue; }
                if (it.Id == KeyLayoutText.Undo || it.Id == KeyLayoutText.Redo) continue;
                string s = it.Shortcut;
                if (s != null && (s.StartsWith("Ctrl+", StringComparison.OrdinalIgnoreCase) || s.StartsWith("Cmd+", StringComparison.OrdinalIgnoreCase)
                                  || s.StartsWith("Alt+", StringComparison.OrdinalIgnoreCase)))
                    _keyMenus.Add(it);
            }
        }

        private IList<Facet.MenuItem> ContextItems(T e)
        {
            if (e == null) return _onStage;
            var list = new List<Facet.MenuItem>();
            if (_model.CanRebind(e)) { list.Add(_menus[1].Items.First(i => i.Id == KeyLayoutText.Rebind)); list.Add(Facet.MenuItem.Separator); }
            list.AddRange(_onItem);
            return list;
        }

        // ---- commands

        private static KeyLayoutMath.Box Box(Rect r) => new KeyLayoutMath.Box(r.x, r.y, r.width, r.height);

        private void RebindInOrder()
        {
            var can = Canvas.Selection.Where(_model.CanRebind).ToArray();
            if (can.Length == 0) return;
            var order = KeyLayoutMath.ReadingOrder(can.Select(e => Box(_model.GetRect(e))).ToArray());
            _model.Rebind(this, order.Select(i => can[i]).ToArray());
        }

        // Bring to Front and Send to Back: the selection to the very top or bottom, as one draw-order change.
        private void ToEnd(string labelId, bool front)
        {
            var items = _model.Elements();
            var picked = new HashSet<T>(Canvas.Selection);
            var rest = items.Where(e => !picked.Contains(e));
            var mine = items.Where(e => picked.Contains(e));
            var order = (front ? rest.Concat(mine) : mine.Concat(rest)).ToArray();
            Do(labelId, () => _model.SetOrder(order));
        }

        private void Align(string labelId, KeyLayoutMath.Edge edge) => Arrange(labelId, b => KeyLayoutMath.Align(b, edge));

        private void Arrange(string labelId, Func<KeyLayoutMath.Box[], KeyLayoutMath.Box[]> place)
        {
            var sel = Sel();
            var to = place(sel.Select(e => Box(_model.GetRect(e))).ToArray());
            Do(labelId, () =>
            {
                for (int i = 0; i < sel.Length; i++) _model.SetRect(sel[i], new Rect(to[i].X, to[i].Y, to[i].W, to[i].H));
            });
        }
    }
}
