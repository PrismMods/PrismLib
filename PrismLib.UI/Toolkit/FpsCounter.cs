using UnityEngine;
using UnityEngine.UIElements;

namespace PrismLib.UI.Toolkit
{
    /* The frame-rate readout, shared by every Prism mod (Topaz and Quartz each drew their own).
       Bottom-right, 10px in, white with a shadow — where Topaz's sat, so it does not move for
       anyone switching over.

       Driven, not self-running: each mod's bridge calls Tick every frame with the PrismLib setting,
       because this half cannot read PrismLib.dll. Two mods both ticking is expected, so a frame is
       counted once. Built on first show and only hidden after that. */
    public static class FpsCounter
    {
        private const float Interval = 0.2f;

        private static Surface _surface;
        private static Label _label;
        private static int _lastFrame = -1;
        private static float _accum;
        private static int _frames;

        public static void Tick(bool show)
        {
            if (Time.frameCount == _lastFrame) return;
            _lastFrame = Time.frameCount;

            if (!show)
            {
                if (_surface != null && _surface.Visible) _surface.Visible = false;
                _accum = 0f; _frames = 0;
                return;
            }
            if (_surface == null && !Build()) return;
            if (!_surface.Visible) _surface.Visible = true;

            _accum += Time.unscaledDeltaTime;
            _frames++;
            if (_accum < Interval) return;
            _label.text = Mathf.RoundToInt(_frames / _accum) + " FPS";
            _accum = 0f; _frames = 0;
        }

        private static bool Build()
        {
            _surface = new Surface("PrismFps", Layer.Hud);
            if (_surface.Root == null) return false;
            _label = Ui.Text("-- FPS", _surface.Root, 22f, Color.white);
            _label.pickingMode = PickingMode.Ignore;
            _label.style.position = Position.Absolute;
            _label.style.right = 10f;
            _label.style.bottom = 10f;
            _label.style.unityTextAlign = TextAnchor.LowerRight;
            _label.style.overflow = Overflow.Visible;
            _label.style.textShadow = new TextShadow
            {
                offset = new Vector2(1.5f, 1.5f), blurRadius = 2f, color = new Color(0f, 0f, 0f, 0.75f),
            };
            return true;
        }
    }
}
