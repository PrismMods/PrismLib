// The hotkey window's shared look on SuiteHub: the wash under it, the bare window and the centred labels; a picture of it
// for a look by eye, fading out as it hides, and fading in and out again allocating nothing.
using System;
using System.Collections;
using System.IO;
using System.Linq;
using PrismLib.UI.Toolkit;
using UnityEngine;
using UnityEngine.UIElements;

namespace LayoutProbe
{
    sealed partial class AutoTest
    {
        // The wash: the root's first child, named, over the whole root, passing clicks through, in the shared colour.
        static string WashWrong(Facet.Surface s, VisualElement window)
        {
            VisualElement root = s == null ? null : s.Root;
            VisualElement w = root != null && root.childCount > 0 ? root[0] : null;
            if (w == null || w.name != HubLook.WashName) return "first child " + (w == null ? "none" : w.name);
            Rect a = w.worldBound, b = root.worldBound;
            Color c = w.resolvedStyle.backgroundColor;
            bool ok = Near(a.x, b.x, 1f) && Near(a.y, b.y, 1f) && Near(a.width, b.width, 1f) && Near(a.height, b.height, 1f)
                      && w.pickingMode == PickingMode.Ignore && Math.Abs(c.a - Tokens.Wash.a) < 0.01f && c.r + c.g + c.b < 0.01f
                      && window != null && root.IndexOf(window) > 0;
            return ok ? null : "wash " + a + " root " + b + " colour " + c + " picking " + w.pickingMode + " window at " + (window == null ? -1 : root.IndexOf(window));
        }

        static string BareWrong(VisualElement window)
        {
            if (window == null) return "no window";
            var r = window.resolvedStyle;
            bool ok = r.backgroundColor.a < 0.01f && r.borderLeftWidth == 0f && r.borderRightWidth == 0f && r.borderTopWidth == 0f && r.borderBottomWidth == 0f;
            return ok ? null : "fill " + r.backgroundColor + " border " + r.borderLeftWidth + "/" + r.borderTopWidth + "/" + r.borderRightWidth + "/" + r.borderBottomWidth;
        }

        // A label drawn by the button itself is anchored middle-centre; one in children sits with them in the button's middle.
        static string UncentredLabels(VisualElement window)
        {
            if (window == null) return "no window";
            var bad = window.Query<Button>().ToList().Where(b => b.resolvedStyle.display != DisplayStyle.None).Where(b =>
            {
                if (b.resolvedStyle.unityTextAlign != TextAnchor.MiddleCenter) return true;
                if (b.childCount == 0) return false;
                Rect u = b[0].worldBound;
                foreach (var c in b.Children()) u = Rect.MinMaxRect(Mathf.Min(u.xMin, c.worldBound.xMin), Mathf.Min(u.yMin, c.worldBound.yMin),
                                                                     Mathf.Max(u.xMax, c.worldBound.xMax), Mathf.Max(u.yMax, c.worldBound.yMax));
                bool labels = b.Query<TextElement>().ToList().Where(t => t != b).All(t => t.resolvedStyle.unityTextAlign == TextAnchor.MiddleCenter);
                return !labels || !Near(u.center.x, b.worldBound.center.x) || !Near(u.center.y, b.worldBound.center.y);
            }).Select(b => b.name).ToArray();
            return bad.Length == 0 ? null : string.Join(",", bad);
        }

        // Every shown button at the hub button's width, at least its height, its corners and text size, on a solid fill: the
        // accent, the last one the theme's title bar; every label in the text colour for that fill.
        static string ButtonsWrong(VisualElement window)
        {
            if (window == null) return "no window";
            var shown = window.Query<Button>().ToList().Where(b => b.name != null && b.name.StartsWith("hub-") && b.resolvedStyle.display != DisplayStyle.None).ToList();
            if (shown.Count == 0) return "no buttons";
            var bad = shown.Where((b, i) =>
            {
                var r = b.resolvedStyle;
                bool last = i == shown.Count - 1;
                Color want = Facet.Theme.For(b)[last ? Facet.Role.TitleBar : Facet.Role.Accent], ink = Facet.Theme.For(b)[last ? Facet.Role.Text : Facet.Role.AccentText];
                bool text = b.Query<TextElement>().ToList().All(t => Near(t.resolvedStyle.fontSize, HubLook.ButtonTextSize, 0.5f)
                    && Near(t.resolvedStyle.color.r, ink.r, 0.01f) && Near(t.resolvedStyle.color.g, ink.g, 0.01f) && Near(t.resolvedStyle.color.b, ink.b, 0.01f));
                // Within a screen pixel: the panel snaps edges to whole pixels at its scale.
                return !Near(b.worldBound.width, HubLook.ButtonWidth, 1f) || b.worldBound.height < HubLook.ButtonHeight - 1f
                       || !Near(r.borderTopLeftRadius, HubLook.ButtonRadius, 0.1f) || !text || r.backgroundColor.a < 0.999f
                       || !Near(r.backgroundColor.r, want.r, 0.01f) || !Near(r.backgroundColor.g, want.g, 0.01f) || !Near(r.backgroundColor.b, want.b, 0.01f);
            }).Select(b => b.name + " " + b.worldBound.size + " r" + b.resolvedStyle.borderTopLeftRadius + " text " + b.resolvedStyle.fontSize + " fill " + b.resolvedStyle.backgroundColor
                + " ink " + string.Join("/", b.Query<TextElement>().ToList().Select(t => t.resolvedStyle.color.ToString()).ToArray())).ToArray();
            return bad.Length == 0 ? null : string.Join(", ", bad);
        }

        // The logo at the shared size, centred over the buttons, with the wordmark's line under it and above the first button.
        static string LogoWrong(VisualElement window, string wordmark)
        {
            VisualElement logo = window == null ? null : window.Q("hub-logo");
            Label mark = logo == null ? null : logo.Q<Label>(HubLook.WordmarkName);
            Button first = window == null ? null : window.Query<Button>().ToList().FirstOrDefault(b => b.name != null && b.name.StartsWith("hub-") && b.resolvedStyle.display != DisplayStyle.None);
            if (logo == null || mark == null || first == null) return "logo " + (logo != null) + " wordmark " + (mark != null) + " button " + (first != null);
            Rect l = logo.worldBound, m = mark.worldBound;
            bool ok = Near(l.width, HubLook.LogoSize, 1f) && Near(l.height, HubLook.LogoSize, 1f) && Near(l.center.x, window.worldBound.center.x, 1f)
                      && mark.text == wordmark && Near(mark.resolvedStyle.fontSize, HubLook.WordmarkSize, 0.5f)
                      && m.y >= l.yMax - 0.5f && m.yMax <= first.worldBound.y + 0.5f && Near(m.center.x, l.center.x, 1f);
            return ok ? null : "logo " + l + " wordmark '" + mark.text + "' " + m + " size " + mark.resolvedStyle.fontSize + " first button " + first.worldBound;
        }

        // Hover and press lay more white over a hub button than rest does; focus rings it in the text colour.
        static string StatesWrong(Button b)
        {
            if (b == null) return "no button";
            // The inline style: the resolved one follows only on the next layout pass.
            float rest = b.style.unityBackgroundImageTintColor.value.a;
            using (var e = MouseEnterEvent.GetPooled()) { e.target = b; b.SendEvent(e); }
            float hover = b.style.unityBackgroundImageTintColor.value.a;
            using (var e = MouseLeaveEvent.GetPooled()) { e.target = b; b.SendEvent(e); }
            float back = b.style.unityBackgroundImageTintColor.value.a;
            bool ok = hover > rest + 0.03f && Near(back, rest, 0.001f);
            return ok ? null : "tint rest " + rest + " hover " + hover + " back " + back;
        }

        // A pointer event through the panel, as the player's pointer arrives: picked and handled at once (outside a dispatch),
        // so it is read before the real pointer's next move can leave the button.
        static void PointerAt(VisualElement root, EventType t, Vector2 panel)
        {
            var ev = new Event { type = t, button = 0, clickCount = 1, mousePosition = panel };
            EventBase e = t == EventType.MouseDown ? PointerDownEvent.GetPooled(ev)
                        : t == EventType.MouseUp ? PointerUpEvent.GetPooled(ev) : (EventBase)PointerMoveEvent.GetPooled(ev);
            using (e) root.panel.visualTree.SendEvent(e);
        }

        // Focused (a frame after Focus, as focus events may queue): ringed in the text colour.
        static string RingWrong(Button b)
        {
            if (b == null) return "no button";
            Color ring = b.style.borderTopColor.value, text = Facet.Theme.For(b)[Facet.Role.Text];
            float width = b.style.borderTopWidth.value;
            bool ok = width >= 1f && Near(ring.r, text.r, 0.01f) && Near(ring.a, text.a, 0.01f);
            return ok ? null : "ring " + ring + " width " + width + " focused " + (b.focusController != null && b.focusController.focusedElement == b);
        }

        // Unity's own capture, through reflection: the probe does not reference its module. UnityModManager's window, drawn
        // over every panel, is closed for it and opened again.
        static IEnumerator Shot(string name)
        {
            var capture = System.Type.GetType("UnityEngine.ScreenCapture, UnityEngine.ScreenCaptureModule")?.GetMethod("CaptureScreenshot", new[] { typeof(string) });
            if (capture == null) { Main.Log("INFO hub look shot: no ScreenCapture"); yield break; }
            string file = Path.Combine(Path.GetTempPath(), name + ".png");
            var umm = System.Type.GetType("UnityModManagerNet.UnityModManager+UI, UnityModManager");
            object ui = umm == null ? null : umm.GetProperty("Instance").GetValue(null, null);
            bool open = ui != null && umm.GetProperty("Opened").GetValue(ui, null) is bool && (bool)umm.GetProperty("Opened").GetValue(ui, null);
            if (open) umm.GetMethod("ToggleWindow", new[] { typeof(bool) }).Invoke(ui, new object[] { false });
            yield return Frames(3);
            if (File.Exists(file)) File.Delete(file);
            capture.Invoke(null, new object[] { file });
            yield return Frames(3);
            if (open) umm.GetMethod("ToggleWindow", new[] { typeof(bool) }).Invoke(ui, new object[] { true });
            Main.Log("INFO hub look shot: " + file);
        }

        IEnumerator HubLookChecks()
        {
            var demo = new DemoSuite("LayoutProbeHubLook") { Id = "probe.hublook", HubIcon = true };
            var shell = new SuiteShell(demo);
            var hub = new SuiteHub(shell);
            hub.Show();
            float t0 = Time.realtimeSinceStartup;   // past the fade-in, however fast the frames come
            while (Time.realtimeSinceStartup - t0 < HubLook.FadeMs / 1000f * Facet.Anim.Speed + 0.1f) yield return null;
            var win = HubWindow(hub);
            string why = WashWrong(hub.Surface, win);
            Check(why == null, "hub look: a wash under the hub covers the whole screen in the shared wash colour", why ?? "");
            why = BareWrong(win);
            Check(why == null, "hub look: the hub's window has no fill and no border", why ?? "");
            why = UncentredLabels(win);
            Check(why == null, "hub look: every button label in the hub is centred", why ?? "");
            why = ButtonsWrong(win);
            Check(why == null, "hub look: every hub button has the old hub's size, corners and text on a solid fill, the last one neutral", why ?? "");
            why = LogoWrong(win, demo.Title.ToUpperInvariant());
            Check(why == null, "hub look: the hub leads with the logo at the shared size and the mod's wordmark under it", why ?? "");
            Check(hub.Surface != null && Near(hub.Surface.Root.resolvedStyle.opacity, 1f, 0.01f), "hub look: the hub has faded in",
                  hub.Surface == null ? "no surface" : hub.Surface.Root.resolvedStyle.opacity.ToString());
            yield return Shot("prism-suitehub-demo");
            // After the shot, so the picture shows each button at rest.
            var probed = HubButton(hub, "hub-settings");
            why = StatesWrong(probed);
            if (probed != null) probed.Focus();
            yield return null;
            why = why ?? RingWrong(probed);
            if (probed != null) probed.Blur();
            // The pointer moves onto it, presses at its centre and is released far off it, so no click runs: the press lays
            // more white than hover does, and leaving takes it all off. The panel itself sends the enter and leave.
            if (why == null && probed != null)
            {
                Vector2 at = probed.worldBound.center, off = new Vector2(-1000f, -1000f);
                PointerAt(hub.Surface.Root, EventType.MouseMove, at);
                float hover = probed.style.unityBackgroundImageTintColor.value.a;
                PointerAt(hub.Surface.Root, EventType.MouseDown, at);
                float press = probed.style.unityBackgroundImageTintColor.value.a;
                PointerAt(hub.Surface.Root, EventType.MouseUp, off);
                PointerAt(hub.Surface.Root, EventType.MouseMove, off);
                float back = probed.style.unityBackgroundImageTintColor.value.a;
                if (!(hover > 0.03f) || !(press > hover + 0.03f) || back > 0.001f || !hub.Visible) why = "tint hover " + hover + " press " + press + " back " + back + " hub shown " + hub.Visible;
            }
            Check(why == null, "hub look: hover lightens a hub button, a press lightens it more and focus rings it", why ?? "");

            // Fading in again, after the first, allocates nothing; a collection inside the loop would hide growth.
            for (int i = 0; i < 100; i++) HubLook.FadeIn(hub.Surface);
            int gcs = GC.CollectionCount(0);
            long mem = GC.GetTotalMemory(false);
            for (int i = 0; i < 10000; i++) HubLook.FadeIn(hub.Surface);
            long grew = GC.GetTotalMemory(false) - mem;
            int ran = GC.CollectionCount(0) - gcs;
            Check(grew < 4096 && ran == 0, "hub look: fading the hub in again allocates nothing", grew + " bytes, " + ran + " collections over 10000 fades");

            // Hiding fades it out: hidden at once for every check, still drawn and fading, then gone.
            yield return Frames(2);
            VisualElement root = hub.Surface.Root;
            float half = HubLook.FadeMs / 2000f * Facet.Anim.Speed;
            hub.Hide();
            t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < half) yield return null;
            float mid = root.resolvedStyle.opacity;
            Check(Facet.Anim.Speed > 0f && !hub.Visible && root.resolvedStyle.display == DisplayStyle.Flex && mid < 0.99f && mid > 0f,
                  "hub look: hiding the hub fades it out", "speed " + Facet.Anim.Speed + " visible " + hub.Visible + " display " + root.resolvedStyle.display
                  + " opacity " + mid);
            while (Time.realtimeSinceStartup - t0 < 2f * half + 0.1f) yield return null;
            yield return Frames(2);
            Check(root.resolvedStyle.display == DisplayStyle.None && Near(root.resolvedStyle.opacity, 1f, 0.01f),
                  "hub look: the faded-out hub is gone, ready to show again", "display " + root.resolvedStyle.display + " opacity " + root.resolvedStyle.opacity);

            for (int i = 0; i < 100; i++) { HubLook.FadeIn(hub.Surface); HubLook.FadeOut(hub.Surface); }
            gcs = GC.CollectionCount(0);
            mem = GC.GetTotalMemory(false);
            for (int i = 0; i < 10000; i++) { HubLook.FadeIn(hub.Surface); HubLook.FadeOut(hub.Surface); }
            grew = GC.GetTotalMemory(false) - mem;
            ran = GC.CollectionCount(0) - gcs;
            Check(grew < 4096 && ran == 0, "hub look: fading the hub in and out again allocates nothing", grew + " bytes, " + ran + " collections over 10000 pairs");
            hub.Dispose();
            shell.Dispose();
            yield return null;
        }
    }
}
