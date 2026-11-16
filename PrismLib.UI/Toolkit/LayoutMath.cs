using System;
using System.Collections.Generic;

namespace PrismLib.UI.Toolkit
{
    /* The layout editor's geometry, kept free of Unity so tools/PrismLibTest.cs compiles this file
       directly and checks it offline. Same conventions as PrismLib.LayoutTarget: screen pixels with
       a bottom-left origin, anchors 0-8 in reading order from the top-left, crop {l, b, r, t}. */
    internal static class LayoutMath
    {
        private const float MinVisible = 0.05f;   // crop never hides more than 95% of an axis
        private const float RotationWindow = 5f;

        /// Quartz's 8 px at 1080p, scaled to the screen.
        public static float SnapThreshold(float screenH) => 8f * screenH / 1080f;

        /// The nearest guide within threshold, else the value unchanged.
        public static float Snap(float value, float[] guides, float threshold)
        {
            float best = value, bestDist = threshold;
            if (guides == null) return value;
            foreach (var g in guides)
            {
                float d = Math.Abs(value - g);
                if (d <= bestDist) { best = g; bestDist = d; }
            }
            return best;
        }

        /// Which ninth of the screen a point sits in, by thirds; row 0 is the top third.
        public static int ClosestAnchor(float cx, float cy, float screenW, float screenH)
        {
            int col = cx < screenW / 3f ? 0 : cx < screenW * 2f / 3f ? 1 : 2;
            float fromTop = screenH - cy;
            int row = fromTop < screenH / 3f ? 0 : fromTop < screenH * 2f / 3f ? 1 : 2;
            return row * 3 + col;
        }

        /// The anchor's point on screen: corners, edge centres or the middle.
        public static float[] AnchorPoint(int anchor, float screenW, float screenH)
        {
            int a = Math.Max(0, Math.Min(8, anchor));
            return new[] { (a % 3) * screenW / 2f, screenH - (a / 3) * screenH / 2f };
        }

        /// Snaps to a quarter turn within 5 degrees; always returns [0, 360).
        public static float SnapRotation(float degrees)
        {
            float d = ((degrees % 360f) + 360f) % 360f;
            float quarter = (float)Math.Round(d / 90f) * 90f;
            return Math.Abs(d - quarter) <= RotationWindow ? quarter % 360f : d;
        }

        /// Each edge in [0, 1], and at least 5% of each axis left visible (left/bottom win).
        public static float[] ClampCrop(float[] crop)
        {
            var c = new float[4];
            if (crop != null) Array.Copy(crop, c, Math.Min(4, crop.Length));
            c[0] = Clamp(c[0], 0f, 1f - MinVisible);
            c[2] = Clamp(c[2], 0f, 1f - MinVisible - c[0]);
            c[1] = Clamp(c[1], 0f, 1f - MinVisible);
            c[3] = Clamp(c[3], 0f, 1f - MinVisible - c[1]);
            return c;
        }

        /// The filter bar's owners after "All": each distinct owner in first-seen order, then
        /// `filter` if it is none of them, so a player sent to an owner with no target still sees its tab.
        public static List<string> FilterTabs(IEnumerable<string> owners, string filter)
        {
            var tabs = new List<string>();
            if (owners != null)
                foreach (var o in owners)
                    if (!string.IsNullOrEmpty(o) && !tabs.Contains(o)) tabs.Add(o);
            if (!string.IsNullOrEmpty(filter) && !tabs.Contains(filter)) tabs.Add(filter);
            return tabs;
        }

        /// A target of `owner` can be moved under `filter` (null is All).
        public static bool Movable(string owner, string filter) => filter == null || owner == filter;

        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
