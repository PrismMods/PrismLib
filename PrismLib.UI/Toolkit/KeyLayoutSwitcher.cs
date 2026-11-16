using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace PrismLib.UI.Toolkit
{
    /* A window of a model's layouts as cards, Hands then Feet (with a Hidden card), each with a painted preview. A
       click shows a layout as one undo step; a double-click or Edit hands it to the editor. Escape and the close
       button hide it; only Dispose ends it. */
    public sealed class KeyLayoutSwitcher<T> : IDisposable where T : class
    {
        private const float LimitFor = 4f, DimAlpha = 0.35f;

        internal sealed class Card
        {
            public string Layout;   // null: Feet's Hidden card
            public LayoutGroup Group;
            public string Key;
        }

        private readonly KeyLayoutModel<T> _model;
        private readonly Action<string> _edit;
        private readonly Action _afterApply;
        private readonly Facet.Surface _surface;
        private readonly Facet.Window _window;
        private readonly Facet.CardGrid<Card> _hands, _feet;
        // Rebuilt by Refresh, so a grid's refresh pass reads them without allocating.
        private List<Card> _handCards = new List<Card>(), _footCards = new List<Card>();
        private string _limit;
        private Facet.Role _tone = Facet.Role.Danger;
        private float _limitUntil = float.NegativeInfinity;

        private KeyLayoutSwitcher(KeyLayoutModel<T> model, Action<string> edit)
        {
            _model = model;
            _edit = edit;
            _afterApply = () => { if (Visible) Refresh(); };

            var surface = new Facet.Surface(model.Host, "KeyLayoutSwitcher." + model.Host.Name, Facet.Layer.Window);
            _surface = surface;
            FocusRing.PointerBlurs(surface.Root);
            var w = _window = new Facet.Window(surface.Root, KeyLayoutText.Switcher, L(KeyLayoutText.Switcher), new Rect(0f, 0f, 640f, 520f));
            w.Raised += () => surface.BringToFront();   // so the editor's menu keys stop while this is in front, and back
            if (!w.HasStoredGeometry)
            {
                // Centred on the first layout that knows the panel's size.
                EventCallback<GeometryChangedEvent> laid = null;
                laid = _ =>
                {
                    var size = surface.Root.layout.size;
                    if (!surface.Visible || !(size.x > 0f && size.y > 0f)) return;
                    Facet.Events.Off(surface.Root, laid);
                    Centre(size);
                };
                Facet.Events.On(surface.Root, laid);
            }

            Facet.Controls.Info(w.Body, "", () => _limit, o =>
            {
                o.Id = "kle-switcher-limit";
                o.Tone = () => _tone;
                o.VisibleWhen = () => Time.realtimeSinceStartup < _limitUntil;
            });
            var scroll = Facet.Ui.Scroll(w.Body);
            _hands = Grid(Facet.Controls.Section(scroll, L(KeyLayoutText.Hands)), LayoutGroup.Hands);
            _feet = Grid(Facet.Controls.Section(scroll, L(KeyLayoutText.Feet)), LayoutGroup.Feet);

            Facet.History.AfterApply += _afterApply;
        }

        /// Shows it; edit opens the editor on a layout.
        public static KeyLayoutSwitcher<T> Open(KeyLayoutModel<T> model, Action<string> edit)
        {
            if (model == null) throw new ArgumentNullException("model");
            var s = new KeyLayoutSwitcher<T>(model, edit);
            s.Show();
            return s;
        }

        public bool Visible => _surface.Visible;

        /// Shows it above every other window, even one raised over it while it showed (the editor's own window takes the
        /// press on its Layouts button). Shown from hidden, it opens centred over the editor.
        public void Show()
        {
            bool was = _surface.Visible;
            _surface.Visible = true;
            _surface.BringToFront();
            if (!was) Centre(_surface.Root.layout.size);
            Refresh();
        }

        // Over the last editor built on the model when it is on a panel, else over the screen (size: the switcher's panel).
        private void Centre(Vector2 size)
        {
            var r = _window.Rect;
            var ed = _model.EditorRoot;
            Vector2 c = ed != null && ed.panel != null && ed.worldBound.width > 0f ? ed.worldBound.center : size / 2f;
            if (c == Vector2.zero) return;   // no editor and no layout yet: the first GeometryChanged places it
            _window.Rect = new Rect(c.x - r.width / 2f, c.y - r.height / 2f, r.width, r.height);
        }

        public void Hide() { _surface.Visible = false; }

        public void Toggle()
        {
            if (Visible) Hide();
            else Show();
        }

        /// Re-reads layouts, marks and previews.
        public void Refresh()
        {
            _handCards = Cards(LayoutGroup.Hands);
            _footCards = Cards(LayoutGroup.Feet);
            _footCards.Add(new Card { Layout = null, Group = LayoutGroup.Feet, Key = KeyOf(LayoutGroup.Feet, null) });
            _hands.Refresh();
            _feet.Refresh();
            string editing = _model.Editing;
            if (editing == null) return;
            var c = _handCards.Concat(_footCards).FirstOrDefault(x => x.Layout == editing);
            if (c != null) (c.Group == LayoutGroup.Hands ? _hands : _feet).Redraw(c);
        }

        /// Shows a line at the top of the window for a few seconds: the model's messages while no editor shows them.
        public void Say(string text, Facet.Role role = Facet.Role.TextMuted)
        {
            _limit = text;
            _tone = role;
            _limitUntil = Time.realtimeSinceStartup + LimitFor;
        }

        public void Dispose()
        {
            Facet.History.AfterApply -= _afterApply;
            _surface.Dispose();
        }

        private string L(string id) => _model.Label(id, KeyLayoutText.English(id));

        private static string KeyOf(LayoutGroup group, string layout) => group + "/" + (layout ?? "");

        private List<Card> Cards(LayoutGroup group)
        {
            var list = new List<Card>();
            var layouts = _model.Layouts(group);
            if (layouts != null)
                foreach (var l in layouts) list.Add(new Card { Layout = l, Group = group, Key = KeyOf(group, l) });
            return list;
        }

        // One step over the model, then save and re-read.
        private void Do(string labelId, Action change)
        {
            KeyLayoutEditor<T>.Step(_model, labelId, change);
            _model.Committed();
            Refresh();
        }

        // At the layout limit: shows why for a while and refuses.
        private bool Limited()
        {
            string t = _model.LimitText();
            if (t == null) return false;
            Say(t, Facet.Role.Danger);
            return true;
        }

        private void EditLayout(string layout)
        {
            Hide();
            if (_edit != null) _edit(layout);
        }

        private Facet.CardGrid<Card> Grid(VisualElement parent, LayoutGroup group)
        {
            bool hands = group == LayoutGroup.Hands;
            // Foot counts are new empty layouts, not presets.
            string newId = hands ? KeyLayoutText.NewFromPreset : KeyLayoutText.NewLayout;
            Facet.CardGrid<Card> g = null;
            g = Facet.Controls.CardGrid<Card>(parent, o =>
            {
                o.Id = hands ? "kle-switcher-hands" : "kle-switcher-feet";
                o.Items = () => hands ? _handCards : _footCards;
                o.Key = c => c.Key;
                // A card's title is shown as returned, so the Hidden word is translated here.
                o.Title = c => c.Layout == null ? _model.Host.Translate(L(KeyLayoutText.Hidden)) : _model.Name(c.Layout);
                o.Active = c => c.Layout == _model.Shown(c.Group);
                o.Thumb = (box, c) => { if (c.Layout != null) Paint(box, c.Layout); };
                o.Click = c =>
                {
                    KeyLayoutEditor<T>.Pick(_model, KeyLayoutText.EditLayout, c.Group, c.Layout);
                    _model.Committed();
                    Refresh();
                };
                o.DoubleClick = c => { if (c.Layout != null) EditLayout(c.Layout); };
                o.Menu = c => c.Layout == null ? null : new List<Facet.MenuItem>
                {
                    Item(KeyLayoutText.EditLayout, () => EditLayout(c.Layout)),
                    Item(KeyLayoutText.Rename, () => g.BeginRename(c)),
                    Item(KeyLayoutText.Duplicate, () => { if (!Limited()) Do(KeyLayoutText.Duplicate, () => _model.Duplicate(c.Layout)); }),
                    // The model keeps at least one hand layout.
                    Item(KeyLayoutText.Delete, () => ConfirmDelete(g.Root, c.Layout), () => !(hands && _model.Layouts(LayoutGroup.Hands).Count == 1)),
                    Item(KeyLayoutText.ExportDm, () => _model.Export(c.Layout)),
                };
                o.Rename = (c, name) => { if (c.Layout != null) Do(KeyLayoutText.Rename, () => _model.Rename(c.Layout, name)); };
                o.AddLabel = L(newId);
                o.Add = () => Limited() ? null : (IList<Facet.MenuItem>)(_model.NewItems(group, _ => { }) ?? new List<Facet.MenuItem>())
                    .Select(i => Wrapped(i, newId)).ToList();
            });
            return g;
        }

        private Facet.MenuItem Item(string id, Action run, Func<bool> enabled = null)
            => new Facet.MenuItem { Id = id, Text = L(id), Run = run, Enabled = enabled };

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

        private void ConfirmDelete(VisualElement owner, string layout)
        {
            Facet.Popup.Confirm(owner, L(KeyLayoutText.DeleteConfirm), _model.Name(layout),
                ok => { if (ok) Do(KeyLayoutText.Delete, () => _model.Delete(layout)); },
                o => { o.Danger = true; o.Ok = L(KeyLayoutText.Delete); o.Cancel = L(KeyLayoutText.Cancel); });
        }

        // ---- previews: one painted element per card, the layout's boxes scaled to fit their bounds

        private void Paint(VisualElement box, string layout)
        {
            var boxes = new List<KeyLayoutBox>();
            _model.Thumb(layout, boxes);
            var el = new VisualElement { name = "kle-thumb", pickingMode = PickingMode.Ignore };
            el.style.position = Position.Absolute;
            el.style.left = 0f; el.style.top = 0f; el.style.right = 0f; el.style.bottom = 0f;
            el.generateVisualContent = mgc => Draw(mgc, el.contentRect, boxes);
            box.Add(el);
        }

        private static void Draw(MeshGenerationContext mgc, Rect area, List<KeyLayoutBox> boxes)
        {
            if (boxes.Count == 0 || !(area.width > 0f && area.height > 0f)) return;
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            foreach (var b in boxes)
            {
                minX = Mathf.Min(minX, b.Rect.xMin); minY = Mathf.Min(minY, b.Rect.yMin);
                maxX = Mathf.Max(maxX, b.Rect.xMax); maxY = Mathf.Max(maxY, b.Rect.yMax);
            }
            const float pad = 4f;
            float bw = maxX - minX, bh = maxY - minY;
            if (!(bw > 0f && bh > 0f)) return;
            float k = Mathf.Min((area.width - 2f * pad) / bw, (area.height - 2f * pad) / bh);
            if (!(k > 0f)) return;
            float ox = area.x + (area.width - bw * k) / 2f - minX * k, oy = area.y + (area.height - bh * k) / 2f - minY * k;

            var p = mgc.painter2D;
            p.lineWidth = 1f;
            foreach (var b in boxes)
            {
                var r = new Rect(ox + b.Rect.x * k, oy + b.Rect.y * k, b.Rect.width * k, b.Rect.height * k);
                Color fill = b.Fill, border = b.Border;
                if (b.Dim) { fill.a *= DimAlpha; border.a *= DimAlpha; }
                p.fillColor = fill;
                p.strokeColor = border;
                p.BeginPath();
                p.MoveTo(new Vector2(r.xMin, r.yMin));
                p.LineTo(new Vector2(r.xMax, r.yMin));
                p.LineTo(new Vector2(r.xMax, r.yMax));
                p.LineTo(new Vector2(r.xMin, r.yMax));
                p.ClosePath();
                p.Fill();
                p.Stroke();
            }
        }
    }
}
