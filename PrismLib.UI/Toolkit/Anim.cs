using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace PrismLib.UI.Toolkit
{
    /* Shared motion.

       Sapphire grew a UiAnim that drives lerps from its per-frame Tick; Bismuth animates by hand in
       a few places and not at all in others, so the same action feels different depending on which
       mod drew the panel. UI Toolkit already has a scheduler and an animation system, so this is a
       thin set of named motions over it rather than another tween engine — and being named is the
       point: "Fast" means the same 120ms everywhere.

       Durations are short deliberately. This is a tool, not a title screen: motion is here to show
       WHERE something came from, and anything past ~200ms is in the way of the next click. */
    public static class Anim
    {
        public const int Instant = 0, Fast = 120, Normal = 180, Slow = 300;

        /// Fade to an opacity. Starts from wherever it is, so interrupting mid-fade does not jump.
        public static void Fade(VisualElement e, float to, int ms = Normal, Action done = null)
        {
            if (e == null) return;
            float from = e.resolvedStyle.opacity;
            if (float.IsNaN(from)) from = e.style.opacity.value;
            Run(e, from, to, ms, (el, v) => el.style.opacity = v, done);
        }

        /// Fade in from transparent, having made sure the element is displayed first.
        public static void FadeIn(VisualElement e, int ms = Normal)
        {
            if (e == null) return;
            e.style.display = DisplayStyle.Flex;
            e.style.opacity = 0f;
            Fade(e, 1f, ms);
        }

        /// Fade out and then hide, so the hidden element stops costing layout.
        public static void FadeOut(VisualElement e, int ms = Normal)
        {
            if (e == null) return;
            Fade(e, 0f, ms, () => e.style.display = DisplayStyle.None);
        }

        /// Slide in along Y while fading — the motion for something arriving from an edge.
        public static void SlideIn(VisualElement e, float fromOffset = -8f, int ms = Normal)
        {
            if (e == null) return;
            e.style.display = DisplayStyle.Flex;
            e.style.opacity = 0f;
            Run(e, 0f, 1f, ms, (el, t) =>
            {
                el.style.opacity = t;
                el.style.translate = new Translate(0f, Mathf.Lerp(fromOffset, 0f, Ease(t)), 0f);
            });
        }

        /// Animate an absolute `left` — the knob of a switch, a sliding indicator.
        public static void Move(VisualElement e, float toLeft, int ms = Fast)
        {
            if (e == null) return;
            float from = e.resolvedStyle.left;
            if (float.IsNaN(from)) from = toLeft;
            Run(e, from, toLeft, ms, (el, v) => el.style.left = v);
        }

        /// Animate a width — a sidebar folding away, a panel opening.
        public static void Width(VisualElement e, float to, int ms = Normal, Action done = null)
        {
            if (e == null) return;
            float from = e.resolvedStyle.width;
            if (float.IsNaN(from)) from = to;
            Run(e, from, to, ms, (el, v) => el.style.width = v, done);
        }

        /// A small scale bump — acknowledgement for a click that has no other visible result.
        public static void Pop(VisualElement e, float from = 0.96f, int ms = Fast)
        {
            if (e == null) return;
            Run(e, 0f, 1f, ms, (el, t) =>
            {
                float s = Mathf.Lerp(from, 1f, Ease(t));
                el.style.scale = new Scale(new Vector2(s, s));
            });
        }

        /* Everything routes through here so one place decides what happens when the element is not
           in a panel yet: UI Toolkit's animation needs a panel to schedule against, and starting
           one on a detached element silently never runs. Setting the final value first means a
           caller always gets the end state even when the motion is skipped. */
        private static void Run(VisualElement e, float from, float to, int ms,
                                Action<VisualElement, float> apply, Action done = null)
        {
            if (ms <= 0 || e.panel == null)
            {
                apply(e, to);
                if (done != null) done();
                return;
            }
            var anim = e.experimental.animation.Start(from, to, ms, (el, v) => apply(el, v));
            if (done != null) anim.OnCompleted(() => done());
        }

        /// Ease-out cubic. Fast at the start, settles at the end — reads as responsive rather than
        /// floaty, which matters when the motion is this short.
        public static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            float u = 1f - t;
            return 1f - u * u * u;
        }
    }
}
