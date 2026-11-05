using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace PrismLib.UI.Toolkit
{
    /* The shared layout editor: every registered element from every Prism mod in one full-screen
       session, Quartz's repositioning menu rebuilt on UI Toolkit.

       Targets are delegates (EditTarget), so the editor never knows how a mod draws. Boxes are
       re-read from GetBox every Tick and never cached, which is what keeps frames right through a
       resize and lets a target whose mod went away simply drop out (its delegates throw; Guard
       skips it). The host re-reads the target list with Reload when it changes.

       Selection follows Quartz: a press on an unselected frame selects it; a press on the
       selected frame drags it; a click without movement on overlapping frames moves the
       selection one layer down, so a covered element is always reachable. */
    public sealed partial class LayoutEditor : IDisposable
    {
        private readonly Func<IEnumerable<EditTarget>> _source;
        private Surface _surface;
        private VisualElement _layer, _frames;
        private Label _empty;
        private readonly List<Frame> _all = new List<Frame>();
        private Frame _selected;
        private bool _visible;
        private bool _cropMode;     // set by the inspector's Crop toggle (LayoutEditor.Crop.cs)
        private readonly HashSet<string> _reported = new HashSet<string>();

        private sealed class Frame
        {
            public EditTarget T;
            public VisualElement Box;
            public Label Tag;
            public float[] B;          // last box read this tick, screen px
            public bool Broken;
        }

        public event Action Closed;

        public LayoutEditor(Func<IEnumerable<EditTarget>> source, UnityEngine.TextCore.Text.FontAsset font = null)
        {
            _source = source ?? (() => new EditTarget[0]);
            _surface = new Surface("PrismLayoutEditor", Layer.Modal, font);
            _surface.Hidden += OnSurfaceHidden;     // Escape hides the surface directly
            Build();
            _surface.Visible = false;
        }

        public bool Visible
        {
            get { return _visible; }
            set
            {
                if (_surface == null || _visible == value) return;
                if (value) { _visible = true; _surface.Visible = true; Reload(); }
                else _surface.Visible = false;   // raises Hidden → OnSurfaceHidden clears _visible, fires Closed
            }
        }

        public void Toggle() => Visible = !Visible;

        /// Re-read the target list (call when a mod's targets change while the editor is open).
        public void Reload()
        {
            if (_frames == null) return;
            string keep = _selected?.T.Key;
            _frames.Clear();
            _all.Clear();
            _selected = null;
            IEnumerable<EditTarget> list = null;
            try { list = _source(); } catch (Exception e) { Ui.Log("LayoutEditor: source threw " + e.Message); }
            if (list != null)
                foreach (var t in list)
                {
                    if (t == null || t.GetBox == null || t.MoveTo == null) continue;
                    var f = NewFrame(t);
                    _all.Add(f);
                    if (t.Key == keep) _selected = f;
                }
            _empty.style.display = _all.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            Tick();
        }

        /// Call every frame from the host's update.
        public void Tick()
        {
            if (!_visible || _layer == null) return;
            float k = Scale();
            if (k <= 0f) return;
            foreach (var f in _all) Place(f, k);
            TickExtras(k);
        }

        public void Dispose()
        {
            if (_surface != null) { _surface.Hidden -= OnSurfaceHidden; _surface.Dispose(); }
            _surface = null; _layer = null; _frames = null;
            _all.Clear();
        }

        private void OnSurfaceHidden()
        {
            bool was = _visible;
            _visible = false;
            _selected = null;
            _drag = null;
            if (was) { var c = Closed; if (c != null) { try { c(); } catch { } } }
        }

        // ── Build ───────────────────────────────────────────────────────────

        private void Build()
        {
            var root = _surface.Root;
            if (root == null) return;
            _layer = new VisualElement { name = "layout-layer" };
            _layer.style.position = Position.Absolute;
            _layer.style.left = 0; _layer.style.top = 0; _layer.style.right = 0; _layer.style.bottom = 0;
            _layer.style.backgroundColor = new Color(0f, 0f, 0f, 0.18f);
            _layer.pickingMode = PickingMode.Position;   // the editor owns every click while open
            root.Add(_layer);

            _frames = new VisualElement { pickingMode = PickingMode.Ignore };
            _frames.style.position = Position.Absolute;
            _frames.style.left = 0; _frames.style.top = 0; _frames.style.right = 0; _frames.style.bottom = 0;
            _layer.Add(_frames);

            _empty = Ui.Text("Nothing to arrange yet", _layer, Tokens.FontSize, Tokens.TextMuted);
            _empty.pickingMode = PickingMode.Ignore;
            _empty.style.position = Position.Absolute;
            _empty.style.left = 0; _empty.style.right = 0; _empty.style.top = Length.Percent(46);
            _empty.style.unityTextAlign = TextAnchor.MiddleCenter;

            var done = Ui.Btn("Done", () => Visible = false, _layer);
            done.name = "layout-done";   // the label is translated (Words.T), the name is not
            done.style.position = Position.Absolute;
            done.style.bottom = 28; done.style.left = Length.Percent(50);
            done.style.translate = new Translate(Length.Percent(-50), 0);
            done.style.paddingLeft = 22; done.style.paddingRight = 22;
            Ui.SetRadius(done, 16f);

            _layer.RegisterCallback<PointerDownEvent>(OnDown);
            _layer.RegisterCallback<PointerMoveEvent>(OnMove);
            _layer.RegisterCallback<PointerUpEvent>(OnUp);
            BuildExtras();
        }

        private Frame NewFrame(EditTarget t)
        {
            var f = new Frame { T = t };
            f.Box = new VisualElement { pickingMode = PickingMode.Ignore };
            f.Box.style.position = Position.Absolute;
            Ui.SetBorderWidth(f.Box, 1f);
            f.Tag = Ui.Text(string.IsNullOrEmpty(t.Label) ? t.Id : t.Label, null, Tokens.FontSizeSmall, Tokens.Text);
            f.Tag.pickingMode = PickingMode.Ignore;
            f.Tag.style.position = Position.Absolute;
            f.Tag.style.backgroundColor = Tokens.Panel;
            f.Tag.style.paddingLeft = 6; f.Tag.style.paddingRight = 6;
            f.Tag.style.paddingTop = 1; f.Tag.style.paddingBottom = 1;
            Ui.SetRadius(f.Tag, Tokens.Radius);
            _frames.Add(f.Box);
            _frames.Add(f.Tag);   // outside the outline, so it never rotates or crops with it
            return f;
        }

        // ── Geometry ────────────────────────────────────────────────────────

        // Panel units per screen pixel: the surface scales to 1080p, so the two differ.
        private float Scale()
        {
            float w = _layer.layout.width;
            if (float.IsNaN(w) || w <= 0f || Screen.width <= 0) return 0f;
            return w / Screen.width;
        }

        private static Vector2 ToScreen(Vector2 panel, float k) =>
            new Vector2(panel.x / k, Screen.height - panel.y / k);

        private void Place(Frame f, float k)
        {
            float[] b = Guard(f, () => f.T.GetBox(), null);
            if (b == null || b.Length < 4) { f.Box.style.display = DisplayStyle.None; f.Tag.style.display = DisplayStyle.None; return; }
            f.B = b;
            float left = (b[0] - b[2] / 2f) * k, top = (Screen.height - b[1] - b[3] / 2f) * k;
            f.Box.style.display = DisplayStyle.Flex;
            f.Box.style.left = left; f.Box.style.top = top;
            f.Box.style.width = b[2] * k; f.Box.style.height = b[3] * k;
            float rot = f.T.GetRotation != null ? Guard(f, () => f.T.GetRotation(), 0f) : 0f;
            f.Box.style.rotate = new Rotate(new Angle(-rot, AngleUnit.Degree));   // ours is CCW, UI Toolkit's CW
            bool sel = f == _selected;
            Ui.SetBorderWidth(f.Box, sel ? 2f : 1f);
            Ui.SetBorderColor(f.Box, sel ? Tokens.Accent : new Color(Tokens.Accent.r, Tokens.Accent.g, Tokens.Accent.b, 0.45f));
            f.Box.style.backgroundColor = sel ? new Color(Tokens.Accent.r, Tokens.Accent.g, Tokens.Accent.b, 0.08f) : new Color(0, 0, 0, 0);
            // Tag above the box, or under it when the box is pinned to the top edge.
            float tagH = Tokens.FontSizeSmall + 6f;
            f.Tag.style.display = DisplayStyle.Flex;
            f.Tag.style.left = left;
            f.Tag.style.top = top - tagH - 3f < 0f ? top + b[3] * k + 3f : top - tagH - 3f;
        }

        // Frames under a screen point, topmost (last drawn) first.
        private List<Frame> StackAt(Vector2 s)
        {
            var hit = new List<Frame>();
            for (int i = _all.Count - 1; i >= 0; i--)
            {
                var f = _all[i];
                if (f.Broken || f.B == null) continue;
                if (Math.Abs(s.x - f.B[0]) <= f.B[2] / 2f && Math.Abs(s.y - f.B[1]) <= f.B[3] / 2f) hit.Add(f);
            }
            return hit;
        }

        // ── Pointer ─────────────────────────────────────────────────────────

        private sealed class Drag
        {
            public Frame F;
            public Vector2 Grab;       // box centre minus pointer, screen px
            public Vector2 Start;
            public bool Armed, Moved, CycleOnUp;
            public List<Frame> Stack;
            public Action<Vector2> Custom;   // grips/knob/crop edges (other partials) take over the drag
            public Action End;
        }
        private Drag _drag;

        private void OnDown(PointerDownEvent e)
        {
            if (e.button != 0 || e.target != _layer) return;
            float k = Scale();
            if (k <= 0f) return;
            var s = ToScreen(e.position, k);
            if (TryBeginExtra(s, e.pointerId)) { _layer.CapturePointer(e.pointerId); e.StopPropagation(); return; }
            var stack = StackAt(s);
            if (stack.Count == 0) { _selected = null; SelectionChanged(); return; }
            bool selectedHere = _selected != null && stack.Contains(_selected);
            var target = selectedHere ? _selected : stack[0];
            _drag = new Drag
            {
                F = target, Stack = stack, Start = s,
                Armed = selectedHere, CycleOnUp = selectedHere && stack.Count > 1,
                Grab = new Vector2(target.B[0], target.B[1]) - s,
            };
            if (!selectedHere) { _selected = target; SelectionChanged(); }
            else BeginGesture(target);
            _layer.CapturePointer(e.pointerId);
            e.StopPropagation();
        }

        private void OnMove(PointerMoveEvent e)
        {
            if (_drag == null || !_layer.HasPointerCapture(e.pointerId)) return;
            float k = Scale();
            if (k <= 0f) return;
            var s = ToScreen(e.position, k);
            if (_drag.Custom != null) { _drag.Custom(s); return; }
            if (!_drag.Armed) return;
            if (!_drag.Moved && (s - _drag.Start).sqrMagnitude < 9f) return;
            _drag.Moved = true;
            var f = _drag.F;
            Vector2 c = s + _drag.Grab;
            c = SnapCentre(f, c);
            Guard(f, () => { f.T.MoveTo(c.x, c.y); return true; }, false);
            DuringMove(f, c);
        }

        private void OnUp(PointerUpEvent e)
        {
            if (_drag == null) return;
            if (_layer.HasPointerCapture(e.pointerId)) _layer.ReleasePointer(e.pointerId);
            var d = _drag;
            _drag = null;
            if (d.End != null) { d.End(); return; }
            if (d.Armed) EndGesture(d.F);
            if (d.CycleOnUp && !d.Moved)
            {
                int i = d.Stack.IndexOf(d.F);
                _selected = d.Stack[(i + 1) % d.Stack.Count];
                SelectionChanged();
            }
        }

        // Snap the box's edges and centre to the screen edges and centre lines.
        private static Vector2 SnapCentre(Frame f, Vector2 c)
        {
            float t = LayoutMath.SnapThreshold(Screen.height);
            float hw = f.B[2] / 2f, hh = f.B[3] / 2f;
            c.x = SnapAxis(c.x, hw, new float[] { 0f, Screen.width / 2f, Screen.width }, t);
            c.y = SnapAxis(c.y, hh, new float[] { 0f, Screen.height / 2f, Screen.height }, t);
            return c;
        }

        private static float SnapAxis(float centre, float half, float[] guides, float t)
        {
            float best = centre, bestShift = float.MaxValue;
            foreach (float off in new[] { -half, 0f, half })   // low edge, centre, high edge
            {
                float edge = centre + off, snapped = LayoutMath.Snap(edge, guides, t);
                float shift = snapped - edge;
                if (snapped != edge && Math.Abs(shift) < Math.Abs(bestShift)) { bestShift = shift; best = centre + shift; }
            }
            return best;
        }

        // ── Guard ───────────────────────────────────────────────────────────

        /* Every call into a mod goes through here. A target whose mod went away throws; it is
           hidden for the rest of the session and reported once, never every frame. */
        private TR Guard<TR>(Frame f, Func<TR> call, TR fallback)
        {
            if (f.Broken) return fallback;
            try { return call(); }
            catch (Exception e)
            {
                f.Broken = true;
                f.Box.style.display = DisplayStyle.None;
                f.Tag.style.display = DisplayStyle.None;
                if (_selected == f) _selected = null;
                if (_reported.Add(f.T.Key)) Ui.Log("LayoutEditor: " + f.T.Key + " skipped: " + e.Message);
                return fallback;
            }
        }

        // ── Hooks for the other partials (grips, inspector, crop) ───────────
        partial void BuildExtras();
        partial void TickExtras(float k);
        partial void SelectionChangedExtras();
        partial void BeginGesture(Frame f);
        partial void EndGesture(Frame f);
        partial void DuringMove(Frame f, Vector2 centre);
        partial void BeginExtra(Vector2 s, int pointerId, ref bool handled);

        private void SelectionChanged() => SelectionChangedExtras();

        private bool TryBeginExtra(Vector2 s, int pointerId)
        {
            bool handled = false;
            BeginExtra(s, pointerId, ref handled);
            return handled;
        }
    }
}
