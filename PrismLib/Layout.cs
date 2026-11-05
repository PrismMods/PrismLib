using System;
using System.Collections.Generic;

namespace PrismLib
{
    /* One thing a mod lets the user move on screen, described as delegates so the shared layout
       editor (PrismLib.UI) can drive every mod's elements in one session without knowing how any
       of them draws. Plain BCL only, like everything in this assembly.

       Coordinates: screen pixels, origin bottom-left (Unity Screen space). Rotation: degrees,
       counter-clockwise. Anchor: 0-8 in reading order from the top-left (0 TL … 8 BR).
       Crop: {left, bottom, right, top}, the fraction of the source trimmed from each edge. */
    public sealed class LayoutTarget
    {
        public string Id;                          // stable within the mod: "cam"
        public string Label;                       // shown on the frame tag, already localised
        public Func<float[]> GetBox;               // {centerX, centerY, width, height}; size before rotation
        public Action<float, float> MoveTo;        // put the box centre at this screen point
        public Func<float> GetScale;
        public Action<float> SetScale;
        public float MinScale = 0.1f, MaxScale = 4f;
        public Func<float> GetRotation;            // optional pair
        public Action<float> SetRotation;
        public Func<int> GetAnchor;                // optional; SetAnchor keeps the on-screen position
        public Action<int> SetAnchor;
        public Func<bool> GetAnchorClosest;        // optional
        public Action<bool> SetAnchorClosest;
        public Func<float[]> GetCrop;              // optional
        public Action<float[]> SetCrop;
        public override string ToString() => Id;
    }

    /// Every mod's movable elements, for the one layout editor the LayoutEditor claim holder draws.
    public static class Layout
    {
        private sealed class Owned { public string Owner; public LayoutTarget Target; }
        private static readonly List<Owned> _all = new List<Owned>();
        private static readonly object _lock = new object();

        /// Any mod's target list changed, so an open editor can rebuild its frames.
        public static event Action Changed;
        /// Someone asked for the editor; the LayoutEditor claim holder opens it.
        public static event Action OpenRequested;

        // REPLACES everything this mod described, like Settings.Register.
        internal static void Register(ModHandle mod, IEnumerable<LayoutTarget> targets)
        {
            if (mod == null || targets == null) return;
            int n = 0;
            lock (_lock)
            {
                _all.RemoveAll(o => o.Owner == mod.Id);
                foreach (var t in targets)
                {
                    if (t == null || string.IsNullOrEmpty(t.Id) || t.GetBox == null || t.MoveTo == null
                        || t.GetScale == null || t.SetScale == null) continue;
                    _all.Add(new Owned { Owner = mod.Id, Target = t });
                    n++;
                }
            }
            Prism.Log("PrismLib: " + mod.Id + " described " + n + " layout target(s)");
            Raise(Changed);
        }

        public static void Unregister(string owner)
        {
            int removed;
            lock (_lock) removed = _all.RemoveAll(o => o.Owner == owner);
            if (removed > 0) Raise(Changed);
        }

        /// Owner id → target, in registration order. A snapshot: safe to enumerate while mods change.
        public static IEnumerable<KeyValuePair<string, LayoutTarget>> All()
        {
            lock (_lock)
                return _all.ConvertAll(o => new KeyValuePair<string, LayoutTarget>(o.Owner, o.Target));
        }

        public static void RequestOpen() => Raise(OpenRequested);

        private static void Raise(Action a)
        {
            if (a != null) { try { a(); } catch { } }
        }
    }
}
