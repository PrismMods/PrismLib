using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace PrismLib.UI.Toolkit
{
    /* Crop mode: the full, uncropped source is drawn as a dim ghost and each visible edge is a bar
       that drags anywhere along its length, the way a window edge resizes. The ghost stays put while
       an edge moves: after each SetCrop the box is moved so its uncropped source lands where it
       was, whatever the mod does with its centre when cropped. */
    public sealed partial class LayoutEditor
    {
        private VisualElement _ghost;
        private VisualElement[] _edges;
        private Button _cropBtn;
        private readonly TextField[] _fcrop = new TextField[4];

        // The uncropped source in screen px: centre, full size, rotation.
        private struct Ghost { public Vector2 C; public float W, H, Rot; }

        partial void BuildCropRows(Frame f)
        {
            for (int i = 0; i < 4; i++) _fcrop[i] = null;
            _cropBtn = null;
            if (f.T.SetCrop == null || f.T.GetCrop == null) return;
            _cropBtn = Ui.Btn("Crop", () => { _cropMode = !_cropMode; Ui.Highlight(_cropBtn, _cropMode); }, _inspectorBody);
            _cropBtn.name = "layout-crop";
            _cropBtn.style.marginTop = 6f;
            string[] names = { "Crop left %", "Crop bottom %", "Crop right %", "Crop top %" };
            for (int i = 0; i < 4; i++)
            {
                int side = i;
                _fcrop[i] = Field(names[i], v => SetCropSide(f, GhostOf(f), side, v / 100f));
            }
        }

        private Ghost GhostOf(Frame f)
        {
            var c = Guard(f, () => f.T.GetCrop(), null) ?? new float[4];
            float rot = RotationOf(f);
            float fw = f.B[2] / Math.Max(0.01f, 1f - c[0] - c[2]);
            float fh = f.B[3] / Math.Max(0.01f, 1f - c[1] - c[3]);
            // The visible box sits ((l-r)/2, (b-t)/2) of the full size off the source's centre.
            Vector2 off = Rotate(new Vector2((c[0] - c[2]) * fw / 2f, (c[1] - c[3]) * fh / 2f), rot);
            return new Ghost { C = new Vector2(f.B[0], f.B[1]) - off, W = fw, H = fh, Rot = rot };
        }

        private static Vector2 Rotate(Vector2 v, float deg)
        {
            float r = deg * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        // Set one side (0 left, 1 bottom, 2 right, 3 top) and keep the ghost where it was.
        private void SetCropSide(Frame f, Ghost g, int side, float value)
        {
            var c = Guard(f, () => (float[])f.T.GetCrop().Clone(), null);
            if (c == null) return;
            // The dragged edge stops with 5% left; the opposite edge is never pushed.
            int opposite = (side + 2) % 4;
            c[side] = Math.Max(0f, Math.Min(value, 0.95f - c[opposite]));
            c = LayoutMath.ClampCrop(c);
            Vector2 centre = g.C + Rotate(new Vector2((c[0] - c[2]) * g.W / 2f, (c[1] - c[3]) * g.H / 2f), g.Rot);
            Guard(f, () => { f.T.SetCrop(c); f.T.MoveTo(centre.x, centre.y); return true; }, false);
        }

        partial void TickCrop(float k)
        {
            if (_ghost == null) BuildCropChrome();
            var f = _selected;
            bool on = _cropMode && f != null && !f.Broken && f.B != null && f.T.GetCrop != null;
            _ghost.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
            foreach (var e in _edges) e.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
            if (f != null && _fcrop[0] != null)
            {
                var c = Guard(f, () => f.T.GetCrop(), null);
                if (c != null) for (int i = 0; i < 4; i++) Show(_fcrop[i], c[i] * 100f);
            }
            if (!on) return;
            var g = GhostOf(f);
            var p = ToPanel(g.C, k);
            _ghost.style.left = p.x - g.W * k / 2f; _ghost.style.top = p.y - g.H * k / 2f;
            _ghost.style.width = g.W * k; _ghost.style.height = g.H * k;
            _ghost.style.rotate = new Rotate(new Angle(-g.Rot, AngleUnit.Degree));
            // Each bar runs the full length of its edge, turned with the box.
            var mids = EdgeMids(f);
            for (int i = 0; i < 4; i++)
            {
                bool across = i == 0 || i == 2;   // left and right edges run up and down
                float w = across ? EdgeBar : f.B[2] * k, h = across ? f.B[3] * k : EdgeBar;
                var pm = ToPanel(mids[i], k);
                _edges[i].style.width = w; _edges[i].style.height = h;
                _edges[i].style.left = pm.x - w / 2f; _edges[i].style.top = pm.y - h / 2f;
                _edges[i].style.rotate = new Rotate(new Angle(-g.Rot, AngleUnit.Degree));
            }
        }

        private const float EdgeBar = 4f;   // panel px

        private void BuildCropChrome()
        {
            _ghost = new VisualElement { pickingMode = PickingMode.Ignore };
            _ghost.style.position = Position.Absolute;
            _ghost.style.backgroundColor = new Color(1f, 1f, 1f, 0.06f);
            Ui.SetBorderWidth(_ghost, 1f);
            Ui.SetBorderColor(_ghost, new Color(1f, 1f, 1f, 0.35f));
            _ghost.style.display = DisplayStyle.None;
            _under.Add(_ghost);   // under every frame
            _edges = new VisualElement[4];
            for (int i = 0; i < 4; i++) _edges[i] = Handle("layout-crop-edge-" + i, 12f, 3f);
        }

        /* The visible edge under a screen point (0 left, 1 bottom, 2 right, 3 top), or -1: anywhere
           along an edge within HitRadius, the nearest edge where two meet at a corner. */
        private int EdgeAt(Frame f, Vector2 s)
        {
            Vector2 p = Rotate(s - new Vector2(f.B[0], f.B[1]), -RotationOf(f));
            float hw = f.B[2] / 2f, hh = f.B[3] / 2f, r = HitRadius;
            float[] gap = { Math.Abs(p.x + hw), Math.Abs(p.y + hh), Math.Abs(p.x - hw), Math.Abs(p.y - hh) };
            bool alongY = Math.Abs(p.y) <= hh + r, alongX = Math.Abs(p.x) <= hw + r;
            int best = -1;
            for (int i = 0; i < 4; i++)
            {
                bool along = i == 0 || i == 2 ? alongY : alongX;
                if (along && gap[i] <= r && (best < 0 || gap[i] < gap[best])) best = i;
            }
            return best;
        }

        // Midpoints of the visible box's left, bottom, right, top edges (the side order of crop).
        private Vector2[] EdgeMids(Frame f)
        {
            float hw = f.B[2] / 2f, hh = f.B[3] / 2f;
            return new[] { Local(f, -hw, 0f), Local(f, 0f, -hh), Local(f, hw, 0f), Local(f, 0f, hh) };
        }

        partial void BeginCropExtra(Vector2 s, ref bool handled)
        {
            var f = _selected;
            if (!_cropMode || f == null || f.Broken || f.B == null || f.T.GetCrop == null) return;
            int side = EdgeAt(f, s);
            if (side < 0) return;
            var g = GhostOf(f);    // fixed for the whole drag
            // The pointer in the ghost's own frame, as a fraction trimmed from that side.
            Func<Vector2, float> at = p =>
            {
                Vector2 local = Rotate(p - g.C, -g.Rot);
                return side == 0 ? (local.x + g.W / 2f) / g.W
                     : side == 2 ? (g.W / 2f - local.x) / g.W
                     : side == 1 ? (local.y + g.H / 2f) / g.H
                     : (g.H / 2f - local.y) / g.H;
            };
            // Grabbed a little off the edge: the edge keeps that offset instead of jumping to the pointer.
            var c0 = Guard(f, () => f.T.GetCrop(), null);
            float grab = c0 != null ? c0[side] - at(s) : 0f;
            BeginGesture(f);
            _drag = new Drag
            {
                F = f,
                Custom = p => SetCropSide(f, g, side, at(p) + grab),
                End = () => EndGesture(f),
            };
            handled = true;
        }
    }
}
