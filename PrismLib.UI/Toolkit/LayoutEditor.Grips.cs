using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace PrismLib.UI.Toolkit
{
    // Resize grips, the rotation knob and the anchor point/line of the selected target.
    public sealed partial class LayoutEditor
    {
        private VisualElement[] _grips;
        private VisualElement _knob, _anchorDot, _anchorLine;
        private const float KnobGap = 26f;           // screen px above the top edge, at 1080p

        private static float HitRadius => 12f * Screen.height / 1080f;

        // Filled in by LayoutEditor.Inspector.cs and LayoutEditor.Crop.cs.
        partial void BuildInspector();
        partial void TickInspector(float k);
        partial void TickCrop(float k);
        partial void BeginCropExtra(Vector2 s, ref bool handled);

        partial void BuildExtras()
        {
            _grips = new VisualElement[4];
            for (int i = 0; i < 4; i++) _grips[i] = Handle(10f, 2f);
            _knob = Handle(12f, 6f);
            _anchorLine = new VisualElement { pickingMode = PickingMode.Ignore };
            _anchorLine.style.position = Position.Absolute;
            _anchorLine.style.height = 1f;
            _anchorLine.style.backgroundColor = new Color(Tokens.Accent.r, Tokens.Accent.g, Tokens.Accent.b, 0.7f);
            _anchorLine.style.transformOrigin = new TransformOrigin(0, Length.Percent(50));
            _frames.Add(_anchorLine);
            _anchorDot = Handle(8f, 4f);
            BuildInspector();
        }

        private VisualElement Handle(float size, float radius)
        {
            var h = new VisualElement { pickingMode = PickingMode.Ignore };
            h.style.position = Position.Absolute;
            h.style.width = size; h.style.height = size;
            h.style.backgroundColor = Tokens.Accent;
            Ui.SetRadius(h, radius);
            Ui.SetBorderWidth(h, 1f);
            Ui.SetBorderColor(h, Tokens.Panel);
            h.style.display = DisplayStyle.None;
            _frames.Add(h);
            return h;
        }

        partial void TickExtras(float k)
        {
            var f = _selected;
            bool on = f != null && !f.Broken && f.B != null && !_cropMode;
            foreach (var g in _grips) g.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
            _knob.style.display = on && f.T.SetRotation != null ? DisplayStyle.Flex : DisplayStyle.None;
            bool anchored = on && f.T.GetAnchor != null;
            _anchorDot.style.display = anchored ? DisplayStyle.Flex : DisplayStyle.None;
            _anchorLine.style.display = anchored ? DisplayStyle.Flex : DisplayStyle.None;
            if (on)
            {
                var corners = Corners(f);
                for (int i = 0; i < 4; i++) Put(_grips[i], corners[i], k);
                if (f.T.SetRotation != null) Put(_knob, KnobPoint(f), k);
                if (anchored)
                {
                    int a = Guard(f, () => f.T.GetAnchor(), 4);
                    var ap = LayoutMath.AnchorPoint(a, Screen.width, Screen.height);
                    Vector2 pa = ToPanel(new Vector2(ap[0], ap[1]), k), pc = ToPanel(new Vector2(f.B[0], f.B[1]), k);
                    Put(_anchorDot, new Vector2(ap[0], ap[1]), k);
                    Vector2 d = pc - pa;
                    _anchorLine.style.left = pa.x; _anchorLine.style.top = pa.y;
                    _anchorLine.style.width = d.magnitude;
                    _anchorLine.style.rotate = new Rotate(new Angle(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, AngleUnit.Degree));
                }
            }
            TickInspector(k);
            TickCrop(k);
        }

        private static Vector2 ToPanel(Vector2 s, float k) => new Vector2(s.x * k, (Screen.height - s.y) * k);

        // Centre a handle on a screen point.
        private static void Put(VisualElement h, Vector2 s, float k)
        {
            var p = ToPanel(s, k);
            float half = h.resolvedStyle.width / 2f;
            if (float.IsNaN(half)) half = 5f;
            h.style.left = p.x - half; h.style.top = p.y - half;
        }

        private float RotationOf(Frame f) => f.T.GetRotation != null ? Guard(f, () => f.T.GetRotation(), 0f) : 0f;

        // A point in the box's own frame (offset from centre, y up), rotated onto the screen.
        private Vector2 Local(Frame f, float x, float y)
        {
            float r = RotationOf(f) * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(f.B[0] + x * c - y * s, f.B[1] + x * s + y * c);
        }

        private Vector2[] Corners(Frame f)
        {
            float hw = f.B[2] / 2f, hh = f.B[3] / 2f;
            return new[] { Local(f, -hw, hh), Local(f, hw, hh), Local(f, hw, -hh), Local(f, -hw, -hh) };
        }

        private Vector2 KnobPoint(Frame f) => Local(f, 0f, f.B[3] / 2f + KnobGap * Screen.height / 1080f);

        partial void BeginExtra(Vector2 s, int pointerId, ref bool handled)
        {
            if (handled) return;
            BeginCropExtra(s, ref handled);
            if (handled) return;
            var f = _selected;
            if (f == null || f.Broken || f.B == null || _cropMode) return;
            Vector2 c = new Vector2(f.B[0], f.B[1]);

            if (f.T.SetRotation != null && (s - KnobPoint(f)).magnitude <= HitRadius)
            {
                BeginGesture(f);
                _drag = new Drag
                {
                    F = f,
                    Custom = p =>
                    {
                        float deg = Mathf.Atan2(p.y - c.y, p.x - c.x) * Mathf.Rad2Deg - 90f;   // knob sits at +90°
                        float snapped = LayoutMath.SnapRotation(deg);
                        Guard(f, () => { f.T.SetRotation(snapped); return true; }, false);
                    },
                    End = () => EndGesture(f),
                };
                handled = true;
                return;
            }

            foreach (var corner in Corners(f))
            {
                if ((s - corner).magnitude > HitRadius) continue;
                float start = Guard(f, () => f.T.GetScale(), 1f);
                float startDist = Math.Max(1f, (s - c).magnitude);
                BeginGesture(f);
                _drag = new Drag
                {
                    F = f,
                    Custom = p =>
                    {
                        float next = Mathf.Clamp(start * (p - c).magnitude / startDist, f.T.MinScale, f.T.MaxScale);
                        Guard(f, () => { f.T.SetScale(next); return true; }, false);
                    },
                    End = () => EndGesture(f),
                };
                handled = true;
                return;
            }
        }

        // While dragging, an element in "Closest" mode follows whichever third of the screen it is in.
        partial void DuringMove(Frame f, Vector2 centre)
        {
            if (f.T.GetAnchor == null || f.T.SetAnchor == null || f.T.GetAnchorClosest == null) return;
            if (!Guard(f, () => f.T.GetAnchorClosest(), false)) return;
            int want = LayoutMath.ClosestAnchor(centre.x, centre.y, Screen.width, Screen.height);
            if (want != Guard(f, () => f.T.GetAnchor(), want)) Guard(f, () => { f.T.SetAnchor(want); return true; }, false);
        }
    }
}
