using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace PrismLib.UI.Toolkit
{
    /* The colour picker, ported from Bismuth's.

       Bismuth's is the one worth keeping of the three: a collapsed row showing the swatch and hex,
       which opens into a saturation/value square, a hue strip, an optional alpha strip, numeric
       channels and a hex field. Sapphire's wheel and the three bare sliders that were here first
       both lose to it — the square is how people actually pick a colour, and the hex field is how
       they paste one someone gave them.

       Every gradient is a generated texture (see Tex); a mod has no image assets and UI Toolkit
       will not compile a USS gradient at runtime. */
    public sealed class ColourPicker
    {
        private const float SquareSize = 132f, StripWidth = 14f;

        private readonly Action<Color> _onChange;
        private readonly bool _hasAlpha;
        private Color _colour;
        private float _h, _s, _v;

        private VisualElement _swatch, _square, _squareKnob, _hueKnob, _alphaKnob, _alphaFade, _body;
        private Label _hexLabel;
        private TextField _hexField;
        private Slider _r, _g, _b, _a;
        private bool _writing;      // guards the channel sliders against their own updates

        public VisualElement Root { get; private set; }

        public ColourPicker(VisualElement parent, string label, Color initial, bool hasAlpha,
                            Action<Color> onChange)
        {
            _onChange = onChange;
            _hasAlpha = hasAlpha;
            _colour = initial;
            Color.RGBToHSV(initial, out _h, out _s, out _v);

            Root = Ui.Box(parent);
            BuildHeader(label);
            BuildBody();
            Sync(true);
        }

        private void BuildHeader(string label)
        {
            var head = Ui.Row(Root);
            head.style.minHeight = Widgets.RowHeight;
            head.pickingMode = PickingMode.Position;

            var chevron = Ui.Muted("▸", head);
            chevron.style.width = 18f;
            chevron.style.fontSize = Tokens.FontSize + 3f;

            var title = Ui.Text(label, head);
            title.style.flexGrow = 1f;
            title.pickingMode = PickingMode.Ignore;

            _hexLabel = Ui.Muted("", head);
            _hexLabel.style.marginRight = Tokens.Gap;
            _hexLabel.pickingMode = PickingMode.Ignore;

            // Checkerboard behind the swatch, so a transparent colour looks transparent.
            var swatchBg = new VisualElement();
            swatchBg.style.width = 44f;
            swatchBg.style.height = 18f;
            Ui.SetRadius(swatchBg, 4f);
            swatchBg.style.backgroundImage = new StyleBackground(Tex.Checker());
            swatchBg.style.backgroundRepeat = new BackgroundRepeat(Repeat.Repeat, Repeat.Repeat);
            head.Add(swatchBg);

            _swatch = new VisualElement();
            _swatch.style.position = Position.Absolute;
            _swatch.style.left = 0f; _swatch.style.top = 0f;
            _swatch.style.right = 0f; _swatch.style.bottom = 0f;
            Ui.SetRadius(_swatch, 4f);
            Ui.SetBorderWidth(_swatch, 1f);
            Ui.SetBorderColor(_swatch, Tokens.PanelBorder);
            swatchBg.Add(_swatch);

            head.RegisterCallback<ClickEvent>(_ =>
            {
                bool open = _body.style.display == DisplayStyle.None;
                chevron.text = open ? "▾" : "▸";
                if (open) Anim.SlideIn(_body, -6f, Anim.Fast);
                else _body.style.display = DisplayStyle.None;
            });
        }

        private void BuildBody()
        {
            _body = Ui.Box(Root);
            _body.style.display = DisplayStyle.None;
            Ui.SetPadding(_body, Tokens.Gap);

            var top = Ui.Row(_body);
            top.style.alignItems = Align.FlexStart;

            // ── saturation / value square ──────────────────────────────────
            _square = new VisualElement();
            _square.style.width = SquareSize;
            _square.style.height = SquareSize;
            Ui.SetRadius(_square, 4f);
            _square.style.backgroundColor = Color.HSVToRGB(_h, 1f, 1f);
            _square.pickingMode = PickingMode.Position;
            top.Add(_square);

            var sat = Fill(_square);
            sat.style.backgroundImage = new StyleBackground(Tex.Sat());
            var val = Fill(_square);
            val.style.backgroundImage = new StyleBackground(Tex.Val());

            _squareKnob = Knob(_square, 10f);
            Drag(_square, (nx, ny) =>
            {
                _s = Mathf.Clamp01(nx);
                _v = Mathf.Clamp01(1f - ny);
                FromHsv();
            });

            // ── hue, and alpha when the caller wants it ────────────────────
            _hueKnob = Strip(top, Tex.Hue(), null, ny => { _h = Mathf.Clamp01(1f - ny); FromHsv(); });

            if (_hasAlpha)
            {
                _alphaFade = null;
                _alphaKnob = Strip(top, Tex.Checker(), e => _alphaFade = e,
                                   ny => { _colour.a = Mathf.Clamp01(1f - ny); Sync(false); });
            }

            // ── channels and hex ───────────────────────────────────────────
            _r = Channel("R", v => { _colour.r = v; FromRgb(); });
            _g = Channel("G", v => { _colour.g = v; FromRgb(); });
            _b = Channel("B", v => { _colour.b = v; FromRgb(); });
            if (_hasAlpha) _a = Channel("A", v => { _colour.a = v; Sync(false); });

            var hexRow = Ui.Row(_body);
            hexRow.style.minHeight = 26f;
            hexRow.style.marginTop = Tokens.Gap;
            var hl = Ui.Muted("Hex", hexRow);
            hl.style.width = 32f;
            _hexField = new TextField { value = "" };
            _hexField.style.width = 110f;
            _hexField.style.fontSize = Tokens.FontSizeSmall;
            Skin.When<TextField>(_hexField, Skin.Field);
            Cursors.Set(_hexField, Cursors.Kind.Text);
            // Commit on Enter or focus loss, never per keystroke: "#1a" is not a colour yet.
            _hexField.RegisterCallback<BlurEvent>(_ => ParseHex());
            _hexField.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) ParseHex();
            });
            hexRow.Add(_hexField);
        }

        private static VisualElement Fill(VisualElement parent)
        {
            var e = new VisualElement();
            e.style.position = Position.Absolute;
            e.style.left = 0f; e.style.top = 0f; e.style.right = 0f; e.style.bottom = 0f;
            e.pickingMode = PickingMode.Ignore;
            parent.Add(e);
            return e;
        }

        private static VisualElement Knob(VisualElement parent, float size)
        {
            var k = new VisualElement();
            k.style.position = Position.Absolute;
            k.style.width = size; k.style.height = size;
            Ui.SetRadius(k, size / 2f);
            Ui.SetBorderWidth(k, 2f);
            Ui.SetBorderColor(k, Color.white);
            k.style.backgroundColor = Color.clear;
            /* Centre the knob on its own position with a percentage translate, rather than a
               negative margin computed from resolvedStyle — which is NaN until layout has run, so
               the margin came out as zero and every knob sat half its own width right and low of
               where it was pointing. */
            k.style.translate = new Translate(Length.Percent(-50f), Length.Percent(-50f));
            k.pickingMode = PickingMode.Ignore;
            parent.Add(k);
            return k;
        }

        private VisualElement Strip(VisualElement parent, Texture2D bg, Action<VisualElement> fade,
                                    Action<float> onDrag)
        {
            var strip = new VisualElement();
            strip.style.width = StripWidth;
            strip.style.height = SquareSize;
            strip.style.marginLeft = Tokens.Gap;
            Ui.SetRadius(strip, 3f);
            strip.style.backgroundImage = new StyleBackground(bg);
            strip.style.backgroundRepeat = new BackgroundRepeat(Repeat.Repeat, Repeat.Repeat);
            strip.pickingMode = PickingMode.Position;
            parent.Add(strip);

            if (fade != null) fade(Fill(strip));
            var knob = Knob(strip, StripWidth - 2f);
            Drag(strip, (nx, ny) => onDrag(ny));
            return knob;
        }

        /* Pointer capture, so a drag that leaves the element keeps working — picking a colour by
           sliding off the edge and back is normal, and without capture the value sticks. */
        private void Drag(VisualElement e, Action<float, float> onMove)
        {
            Action<Vector2> apply = pos =>
            {
                var r = e.contentRect;
                if (r.width <= 0f || r.height <= 0f) return;
                onMove(pos.x / r.width, pos.y / r.height);
            };
            e.RegisterCallback<PointerDownEvent>(ev =>
            {
                if (ev.button != 0) return;
                e.CapturePointer(ev.pointerId);
                apply(ev.localPosition);
                ev.StopPropagation();
            });
            e.RegisterCallback<PointerMoveEvent>(ev =>
            {
                if (!e.HasPointerCapture(ev.pointerId)) return;
                apply(ev.localPosition);
            });
            e.RegisterCallback<PointerUpEvent>(ev =>
            {
                if (e.HasPointerCapture(ev.pointerId)) e.ReleasePointer(ev.pointerId);
            });
        }

        private Slider Channel(string name, Action<float> set)
        {
            var row = Ui.Row(_body);
            row.style.minHeight = 24f;
            row.style.marginTop = 3f;
            var l = Ui.Muted(name, row);
            l.style.width = 16f;
            var s = new Slider(0f, 1f);
            s.style.flexGrow = 1f;
            Skin.When<Slider>(s, Skin.Slider);
            var readout = Ui.Muted("", row);
            readout.style.minWidth = 34f;
            readout.style.unityTextAlign = TextAnchor.MiddleRight;
            s.RegisterValueChangedCallback(e =>
            {
                if (_writing) return;
                readout.text = Mathf.RoundToInt(e.newValue * 255f).ToString();
                set(e.newValue);
            });
            s.userData = readout;
            row.Insert(1, s);
            return s;
        }

        private void FromHsv()
        {
            float a = _colour.a;
            _colour = Color.HSVToRGB(_h, _s, _v);
            _colour.a = a;
            Sync(false);
        }

        private void FromRgb()
        {
            Color.RGBToHSV(_colour, out _h, out _s, out _v);
            Sync(false);
        }

        private void ParseHex()
        {
            Color c;
            var text = (_hexField.value ?? "").Trim();
            if (!text.StartsWith("#")) text = "#" + text;
            if (!ColorUtility.TryParseHtmlString(text, out c)) { Sync(false); return; }
            if (!_hasAlpha) c.a = _colour.a;
            _colour = c;
            Color.RGBToHSV(_colour, out _h, out _s, out _v);
            Sync(false);
        }

        /* One place writes every control from _colour, so the square, strips, channels, hex and
           swatch cannot disagree. _writing stops a slider's own update from feeding back. */
        private void Sync(bool initial)
        {
            _writing = true;
            try
            {
                _swatch.style.backgroundColor = _colour;
                _square.style.backgroundColor = Color.HSVToRGB(_h, 1f, 1f);
                if (_alphaFade != null)
                {
                    var opaque = _colour; opaque.a = 1f;
                    _alphaFade.style.backgroundImage = new StyleBackground(Tex.Val());
                    _alphaFade.style.unityBackgroundImageTintColor = opaque;
                }

                Place(_squareKnob, _square, _s, 1f - _v);
                Place(_hueKnob, _hueKnob.parent, 0.5f, 1f - _h);
                if (_alphaKnob != null) Place(_alphaKnob, _alphaKnob.parent, 0.5f, 1f - _colour.a);

                Set(_r, _colour.r); Set(_g, _colour.g); Set(_b, _colour.b);
                if (_a != null) Set(_a, _colour.a);

                string hex = "#" + (_hasAlpha ? ColorUtility.ToHtmlStringRGBA(_colour)
                                              : ColorUtility.ToHtmlStringRGB(_colour));
                _hexLabel.text = hex;
                if (!_hexField.focusController?.focusedElement?.Equals(_hexField) ?? true)
                    _hexField.SetValueWithoutNotify(hex);
            }
            finally { _writing = false; }

            if (!initial && _onChange != null)
            {
                try { _onChange(_colour); }
                catch (Exception e) { Ui.Log("ColourPicker handler threw: " + e.Message); }
            }
        }

        private static void Set(Slider s, float v)
        {
            if (s == null) return;
            s.SetValueWithoutNotify(v);
            var readout = s.userData as Label;
            if (readout != null) readout.text = Mathf.RoundToInt(v * 255f).ToString();
        }

        /* Knobs are positioned in PERCENT, because the square and strips are laid out by flexbox
           and their pixel size is not known until layout has run. Centring is the translate set in
           Knob(), for the same reason. */
        private static void Place(VisualElement knob, VisualElement area, float nx, float ny)
        {
            if (knob == null || area == null) return;
            knob.style.left = Length.Percent(nx * 100f);
            knob.style.top = Length.Percent(ny * 100f);
        }
    }
}
