using System;
using UnityEngine.UIElements;

namespace PrismLib.UI.Toolkit
{
    /* Shared motion: named, short, the same everywhere ("Fast" is 120 ms in every mod) — see
       Facet.Anim, which also decides what happens to an element not in a panel yet.
       Duration is the user's setting (PrismLib "anim.duration", 0-400 ms, 120 = the names below as
       written): it scales every Facet window through Facet.Anim.Speed. PrismLib.dll cannot reference
       Facet, so each mod's bridge copies Prism.AnimDuration in every frame, and scroll.smooth into SmoothScroll. */
    public static class Anim
    {
        public const int Instant = 0, Fast = 120, Normal = 180, Slow = 300;

        /// The standard transition length in ms (0-400; 120 is Facet's own speed 1). Writes Facet's shared speed, only when it changes.
        public static int Duration
        {
            get { return (int)Math.Round(Facet.Anim.Speed * 120f); }
            set
            {
                float speed = (value < 0 ? 0 : value > 400 ? 400 : value) / 120f;
                if (Facet.Anim.Speed != speed) Facet.Anim.Speed = speed;
            }
        }

        /// Whether Facet windows glide to where the mouse wheel sends them (PrismLib "scroll.smooth"). Each bridge copies
        /// the setting in every frame, so this writes Facet's shared switch only when it changes.
        public static bool SmoothScroll
        {
            get { return Facet.Anim.SmoothScroll; }
            set { if (Facet.Anim.SmoothScroll != value) Facet.Anim.SmoothScroll = value; }
        }

        /// Fade to an opacity. Starts from wherever it is, so interrupting mid-fade does not jump.
        public static void Fade(VisualElement e, float to, int ms = Normal, Action done = null) => Facet.Anim.Fade(e, to, ms, done);

        /// Fade in from transparent, having made sure the element is displayed first.
        public static void FadeIn(VisualElement e, int ms = Normal) => Facet.Anim.FadeIn(e, ms);

        /// Fade out and then hide, so the hidden element stops costing layout.
        public static void FadeOut(VisualElement e, int ms = Normal) => Facet.Anim.FadeOut(e, ms);

        /// Slide in along Y while fading — the motion for something arriving from an edge.
        public static void SlideIn(VisualElement e, float fromOffset = -8f, int ms = Normal) => Facet.Anim.SlideIn(e, fromOffset, ms);

        /// Animate an absolute `left` — the knob of a switch, a sliding indicator.
        public static void Move(VisualElement e, float toLeft, int ms = Fast) => Facet.Anim.Move(e, toLeft, ms);

        /// Animate a width — a sidebar folding away, a panel opening.
        public static void Width(VisualElement e, float to, int ms = Normal, Action done = null) => Facet.Anim.Width(e, to, ms, done);

        /// A small scale bump — acknowledgement for a click that has no other visible result.
        public static void Pop(VisualElement e, float from = 0.96f, int ms = Fast) => Facet.Anim.Pop(e, from, ms);

        /// Ease-out cubic.
        public static float Ease(float t) => Facet.Anim.Ease(t);
    }
}
