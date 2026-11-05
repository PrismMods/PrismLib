using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace PrismLib.UI.Toolkit
{
    /* Crop mode: the full, uncropped source is drawn as a dim ghost and the four visible edges get
       handles. The ghost stays put while an edge moves: after each SetCrop the box is moved so its
       uncropped source lands where it was, whatever the mod does with its centre when cropped. */
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
            c[side] = value;
            if (side == 0) c[2] = Math.Min(c[2], 0.95f - Math.Max(0f, value));     // the dragged side wins
            if (side == 2) c[0] = Math.Min(c[0], 0.95f - Math.Max(0f, value));
            if (side == 1) c[3] = Math.Min(c[3], 0.95f - Math.Max(0f, value));
            if (side == 3) c[1] = Math.Min(c[1], 0.95f - Math.Max(0f, value));
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
            var mids = EdgeMids(f);
            for (int i = 0; i < 4; i++) Put(_edges[i], mids[i], k);
        }

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
            var mids = EdgeMids(f);
            for (int i = 0; i < 4; i++)
            {
                if ((s - mids[i]).magnitude > HitRadius) continue;
                int side = i;
                var g = GhostOf(f);    // fixed for the whole drag
                BeginGesture(f);
                _drag = new Drag
                {
                    F = f,
                    Custom = p =>
                    {
                        // The pointer in the ghost's own frame, then as a fraction trimmed from that side.
                        Vector2 local = Rotate(p - g.C, -g.Rot);
                        float v = side == 0 ? (local.x + g.W / 2f) / g.W
                                : side == 2 ? (g.W / 2f - local.x) / g.W
                                : side == 1 ? (local.y + g.H / 2f) / g.H
                                : (g.H / 2f - local.y) / g.H;
                        SetCropSide(f, g, side, v);
                    },
                    End = () => EndGesture(f),
                };
                handled = true;
                return;
            }
        }
    }
}
