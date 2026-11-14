using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace PrismLib.UI.Toolkit
{
    /* The canvas host: one box per element the model draws, the companion layout's ghosts, the toolbar's view tools
       and the inspector under the canvas. */
    public sealed partial class KeyLayoutEditor<T>
    {
        private const float HiddenOpacity = 0.35f, GhostOpacity = 0.18f, InspectorShare = 45f;

        private VisualElement _ghostLayer;
        private readonly List<Rect> _ghostRects = new List<Rect>();
        // Each element's box by key (the Repeat owns them) and by the element object, so a drag frame finds boxes
        // without asking the model for keys.
        private readonly Dictionary<string, VisualElement> _boxes = new Dictionary<string, VisualElement>();
        private readonly Dictionary<T, VisualElement> _byItem = new Dictionary<T, VisualElement>();
        private int _inspected;   // the inspector rebuilds when this changes: a new selection, or Refresh
        private bool _dirty;      // the selection's boxes redraw on the next Tick

        private void BuildCanvas()
        {
            Canvas = Facet.Controls.Canvas<T>(Root, "", o =>
            {
                o.Id = "kle-canvas";   // the canvas's step keys and its commit on undo go by this name
                o.Tooltip = L(KeyLayoutText.CanvasHint);
                o.Items = _model.Elements;
                o.Key = _model.Key;
                o.Find = _model.Find;
                o.GetRect = _model.GetRect;
                o.SetRect = _model.SetRect;
                o.Size = _model.CanvasSize;
                o.Ghosts = () => _ghostRects;
                o.UndoDocument = _model.UndoDocument;
                o.Snap = _model.Pref(KeyLayoutText.Snap);
                o.ShowGrid = _model.Pref(KeyLayoutText.Grid);
                o.ShowGuides = _model.Pref(KeyLayoutText.Guides);
                o.Menu = ContextItems;
                // Inside the canvas's own step: the selection is cleared, or it would move onto the next elements. The
                // boxes are re-keyed too: keys can be positions, so the deleted element's box may now hold another key.
                o.Delete = items => { Step(_model, KeyLayoutText.Delete, () => _model.Delete(items)); Canvas.Clear(); Refresh(); };
                o.ZOrder = order => Step(_model, KeyLayoutText.ToFront, () => _model.SetOrder(order));
            });
            // The pane's caption would be empty: the menu bar and the strip already name what is shown.
            var caption = Canvas.Root.childCount > 0 ? Canvas.Root[0] as Label : null;
            if (caption != null) caption.style.display = DisplayStyle.None;

            Canvas.Activated += e => { if (_model.CanRebind(e)) _model.Rebind(this, new[] { e }); };
            Canvas.Changed += PlaceSelection;
            // An undo raises it inside the apply; the model commits undos itself, so only a gesture commits here.
            Canvas.Committed += () => { if (!Facet.History.Applying) _model.Committed(); };
            Canvas.SelectionChanged += () => _inspected++;

            _ghostLayer = Layer(Canvas.Content, "kle-ghosts");
            var items = Facet.Controls.Repeat<T>(Canvas.Content, _model.Elements, _model.Key, (box, e) =>
            {
                box.style.position = Position.Absolute;
                _boxes[_model.Key(e)] = box;
                _byItem[e] = box;
                Place(box, e);
                Draw(box, e);
            });
            items.style.position = Position.Absolute;
            items.style.left = 0f;
            items.style.top = 0f;
            items.pickingMode = PickingMode.Ignore;
        }

        private static VisualElement Layer(VisualElement parent, string name)
        {
            var e = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            e.style.position = Position.Absolute;
            e.style.left = 0f;
            e.style.top = 0f;
            parent.Add(e);
            return e;
        }

        private void BuildTools()
        {
            var t = Menu.Tools;
            t.UndoRedo();
            t.Toggle(L(KeyLayoutText.Snap), Facet.Bind.To(() => Canvas.Snap, v => { Canvas.Snap = v; _model.SetPref(KeyLayoutText.Snap, v); }));
            t.Glyph("−", L(KeyLayoutText.ZoomOut), () => Canvas.ZoomBy(-ZoomStep));
            _zoom = t.Label(ZoomText);
            t.Glyph("+", L(KeyLayoutText.ZoomIn), () => Canvas.ZoomBy(ZoomStep));
            // The toolbar re-reads its label only on a refresh pass; a zoom shows at once.
            Canvas.ViewChanged += () => _zoom.text = ZoomText();
        }

        private string ZoomText() => Mathf.RoundToInt(Canvas.Zoom * 100f) + "%";

        private void BuildInspector()
        {
            var scroll = Facet.Ui.Scroll(Root);
            scroll.style.maxHeight = Length.Percent(InspectorShare);
            var container = new VisualElement { name = "kle-inspector" };
            scroll.Add(container);
            Facet.Controls.Live(container, (Func<int>)(() => _inspected), body =>
            {
                var sel = Sel();
                Facet.Ui.Text(body, _model.InspectorTitle(sel));
                Facet.Tabs.Pages(body, _model.Inspector(sel));
                Facet.Controls.AfterChange(body, Changed);   // on the body, so rows of a later build are covered too
            });
        }

        // ---- boxes

        private void Place(VisualElement box, T e)
        {
            var r = _model.GetRect(e);
            box.style.left = r.x;
            box.style.top = r.y;
            box.style.width = r.width;
            box.style.height = r.height;
            box.style.opacity = _model.Hidden(e) ? HiddenOpacity : 1f;
        }

        private void Draw(VisualElement box, T e)
        {
            _model.Draw(box, e);
            Unpickable(box);   // the canvas hit-tests by rect; a drawn label must not take the pointer
        }

        private static void Unpickable(VisualElement e)
        {
            e.pickingMode = PickingMode.Ignore;
            for (int i = 0; i < e.childCount; i++) Unpickable(e[i]);
        }

        private VisualElement BoxOf(T e)
        {
            VisualElement b;
            if (e != null && _byItem.TryGetValue(e, out b) && b.parent != null) return b;
            return null;
        }

        // Every frame of a drag: where the selection's boxes are, nothing redrawn.
        private void PlaceSelection()
        {
            var sel = Canvas.Selection;
            for (int i = 0; i < sel.Count; i++)
            {
                var b = BoxOf(sel[i]);
                if (b != null) Place(b, sel[i]);
            }
        }

        private void RedrawSelection()
        {
            var sel = Canvas.Selection;
            for (int i = 0; i < sel.Count; i++)
            {
                var b = BoxOf(sel[i]);
                if (b != null) { Place(b, sel[i]); Draw(b, sel[i]); }
            }
        }

        /// A row changed the selection's look while it is being dragged: redraw its boxes on the next Tick, with no commit.
        public void MarkDirty() { _dirty = true; }

        /// After an inspector edit: redraw the selection's boxes, then Committed.
        public void Changed()
        {
            _dirty = false;
            RedrawSelection();
            if (!Facet.History.Applying) _model.Committed();
        }

        /// Strip, items, boxes, ghosts, inspector and status.
        public void Refresh()
        {
            RefreshStrip();
            Canvas.Refresh();
            // Elements may be new objects after a restore: re-key the boxes by the objects now listed.
            _byItem.Clear();
            var items = _model.Elements();
            for (int i = 0; items != null && i < items.Count; i++)
            {
                VisualElement b;
                if (!_boxes.TryGetValue(_model.Key(items[i]), out b) || b.parent == null) continue;
                _byItem[items[i]] = b;
                Place(b, items[i]);
                Draw(b, items[i]);
            }
            var gone = new List<string>();
            foreach (var kv in _boxes) if (kv.Value.parent == null) gone.Add(kv.Key);
            foreach (var k in gone) _boxes.Remove(k);

            _ghostLayer.Clear();
            _ghostRects.Clear();
            var ghosts = _model.Ghosts();
            for (int i = 0; ghosts != null && i < ghosts.Count; i++)
            {
                var g = ghosts[i];
                var box = new VisualElement();
                box.style.position = Position.Absolute;
                _ghostLayer.Add(box);
                Place(box, g);
                box.style.opacity = GhostOpacity;
                Draw(box, g);
                _ghostRects.Add(_model.GetRect(g));
            }
            _inspected++;
        }
    }
}
