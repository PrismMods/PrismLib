using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace PrismLib.UI.Toolkit
{
    /// One stop. The host keeps its own model and maps to this — PrismLib.UI knows nothing about
    /// Bismuth's ColorStop or anyone else's.
    public sealed class GradientStop
    {
        public float Position;
        public Color Colour = Color.white;
    }

    /* The gradient editor, ported from Bismuth's.

       A baked strip with a draggable marker per stop, and the selected stop's colour and position
       edited below it. Clicking empty strip adds a stop there; that is the interaction worth
       keeping, because the alternative — an Add button that drops a stop at some default position
       you then drag — is two steps for the common case.

       The strip is a generated texture re-baked on every change. 256 pixels, so re-baking is cheap
       enough to do inline while a marker is being dragged; the alternative of interpolating with
       elements cannot show a multi-stop ramp at all. */
    public sealed class GradientEditor
    {
        private const int TexWidth = 256;
        private const float StripHeight = 26f, MarkerWidth = 12f;

        private readonly IList<GradientStop> _stops;
        private readonly Action _onChange;
        private readonly bool _hasAlpha;

        private VisualElement _strip, _markerLayer, _editor;
        private Texture2D _tex;
        private GradientStop _selected;

        public VisualElement Root { get; private set; }

        public GradientEditor(VisualElement parent, string label, IList<GradientStop> stops,
                              Action onChange, bool hasAlpha = true)
        {
            _stops = stops ?? new List<GradientStop>();
            _onChange = onChange;
            _hasAlpha = hasAlpha;

            Root = Ui.Box(parent);
            if (!string.IsNullOrEmpty(label)) Widgets.Header(Root, label);

            _strip = new VisualElement();
            _strip.style.height = StripHeight;
            // Inset by half a marker so the end stops do not clip at the edges.
            _strip.style.marginLeft = MarkerWidth / 2f;
            _strip.style.marginRight = MarkerWidth / 2f;
            Ui.SetRadius(_strip, 3f);
            Ui.SetBorderWidth(_strip, 1f);
            Ui.SetBorderColor(_strip, Tokens.PanelBorder);
            _strip.pickingMode = PickingMode.Position;
            Root.Add(_strip);

            _markerLayer = new VisualElement();
            _markerLayer.style.position = Position.Absolute;
            _markerLayer.style.left = 0f; _markerLayer.style.right = 0f;
            _markerLayer.style.bottom = -10f; _markerLayer.style.height = 16f;
            _markerLayer.pickingMode = PickingMode.Ignore;
            _strip.Add(_markerLayer);

            // Click empty strip to add a stop at that position, coloured as the gradient already
            // is there — so the ramp does not jump when you add one.
            _strip.RegisterCallback<ClickEvent>(e =>
            {
                var r = _strip.contentRect;
                if (r.width <= 0f) return;
                float t = Mathf.Clamp01(e.localPosition.x / r.width);
                var stop = new GradientStop { Position = t, Colour = Evaluate(t) };
                _stops.Add(stop);
                Sort();
                _selected = stop;
                Changed();
            });

            _editor = Ui.Box(Root);
            _editor.style.marginTop = 14f;

            if (_stops.Count > 0) _selected = _stops[0];
            Rebuild();
        }

        private void Sort()
        {
            var list = new List<GradientStop>(_stops);
            list.Sort((a, b) => a.Position.CompareTo(b.Position));
            _stops.Clear();
            foreach (var s in list) _stops.Add(s);
        }

        /* Same evaluation the host's own gradient does: clamp outside the ends, lerp between the
           two stops either side. Duplicated rather than delegated because the strip has to be baked
           before the host has any idea a change happened. */
        public Color Evaluate(float t)
        {
            if (_stops.Count == 0) return Color.white;
            if (_stops.Count == 1) return _stops[0].Colour;
            if (t <= _stops[0].Position) return _stops[0].Colour;
            var last = _stops[_stops.Count - 1];
            if (t >= last.Position) return last.Colour;
            for (int i = 0; i < _stops.Count - 1; i++)
            {
                var a = _stops[i];
                var b = _stops[i + 1];
                if (t < a.Position || t > b.Position) continue;
                float span = b.Position - a.Position;
                return span <= 0.0001f ? b.Colour : Color.Lerp(a.Colour, b.Colour, (t - a.Position) / span);
            }
            return last.Colour;
        }

        private void Bake()
        {
            if (_tex == null)
            {
                _tex = new Texture2D(TexWidth, 1, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.HideAndDontSave,
                };
                _strip.style.backgroundImage = new StyleBackground(_tex);
            }
            var px = new Color[TexWidth];
            for (int i = 0; i < TexWidth; i++) px[i] = Evaluate(i / (float)(TexWidth - 1));
            _tex.SetPixels(px);
            _tex.Apply(false, false);
        }

        private void Rebuild()
        {
            Bake();
            RebuildMarkers();
            RebuildEditor();
        }

        private void RebuildMarkers()
        {
            _markerLayer.Clear();
            foreach (var stop in _stops)
            {
                var s = stop;
                var m = new VisualElement();
                m.style.position = Position.Absolute;
                m.style.width = MarkerWidth;
                m.style.height = MarkerWidth;
                m.style.left = Length.Percent(s.Position * 100f);
                m.style.marginLeft = -MarkerWidth / 2f;
                Ui.SetRadius(m, MarkerWidth / 2f);
                m.style.backgroundColor = s.Colour;
                Ui.SetBorderWidth(m, 2f);
                Ui.SetBorderColor(m, s == _selected ? Tokens.Accent : Color.white);
                m.pickingMode = PickingMode.Position;
                _markerLayer.Add(m);

                m.RegisterCallback<PointerDownEvent>(e =>
                {
                    if (e.button != 0) return;
                    _selected = s;
                    m.CapturePointer(e.pointerId);
                    // Stops the strip's own click from adding another stop underneath this one.
                    e.StopPropagation();
                    RebuildMarkers();
                    RebuildEditor();
                });
                m.RegisterCallback<PointerMoveEvent>(e =>
                {
                    if (!m.HasPointerCapture(e.pointerId)) return;
                    var r = _strip.contentRect;
                    if (r.width <= 0f) return;
                    var local = _strip.WorldToLocal(e.position);
                    s.Position = Mathf.Clamp01(local.x / r.width);
                    m.style.left = Length.Percent(s.Position * 100f);
                    Bake();
                    Notify();
                });
                m.RegisterCallback<PointerUpEvent>(e =>
                {
                    if (!m.HasPointerCapture(e.pointerId)) return;
                    m.ReleasePointer(e.pointerId);
                    // Sorted only on release: reordering mid-drag swaps the marker under the
                    // pointer and the drag jumps to a different stop.
                    Sort();
                    Rebuild();
                });
                m.RegisterCallback<PointerLeaveEvent>(_ => { });
            }
        }

        private void RebuildEditor()
        {
            _editor.Clear();
            if (_selected == null)
            {
                Ui.Muted("Click the strip to add a stop.", _editor);
                return;
            }

            var sel = _selected;
            new ColourPicker(_editor, "Stop colour", sel.Colour, _hasAlpha, c =>
            {
                sel.Colour = c;
                Bake();
                RebuildMarkers();
                Notify();
            });

            Widgets.Slider(_editor, "Position", sel.Position, 0f, 1f, v =>
            {
                sel.Position = v;
                Bake();
                RebuildMarkers();
                Notify();
            }, null, "0.00");

            var row = Ui.Row(_editor);
            Ui.Btn("Add stop", () =>
            {
                float t = Mathf.Clamp01(sel.Position + 0.1f);
                var stop = new GradientStop { Position = t, Colour = Evaluate(t) };
                _stops.Add(stop);
                Sort();
                _selected = stop;
                Changed();
            }, row);

            // The last stop stays: a gradient with none evaluates to white and the strip vanishes,
            // which reads as a bug rather than as an empty state.
            if (_stops.Count > 1)
                Ui.Btn("Remove stop", () =>
                {
                    _stops.Remove(sel);
                    _selected = _stops.Count > 0 ? _stops[0] : null;
                    Changed();
                }, row);
        }

        private void Changed()
        {
            Rebuild();
            Notify();
        }

        private void Notify()
        {
            try { if (_onChange != null) _onChange(); }
            catch (Exception e) { Ui.Log("GradientEditor handler threw: " + e.Message); }
        }
    }
}
