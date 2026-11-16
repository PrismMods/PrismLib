// The suite skin (plan 8): what every Prism window wears, and what a window another host made keeps.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using PrismLib.UI.Toolkit;
using UnityEngine;
using UnityEngine.UIElements;

namespace LayoutProbe
{
    sealed partial class AutoTest
    {
        static bool Same(Color a, Color b, float tol = 0.01f)
            => Math.Abs(a.r - b.r) <= tol && Math.Abs(a.g - b.g) <= tol && Math.Abs(a.b - b.b) <= tol && Math.Abs(a.a - b.a) <= tol;

        /* Layout snaps each edge to a device pixel, so a size is off by up to one: at a panel scale of 1.4
           a 1px line lays out 0.71 wide and 30 as 30.55. Set from the panel under test. */
        static float Snap = 1f;
        static void SnapTo(VisualElement e) { Snap = e?.panel != null ? 1.01f / e.panel.scaledPixelsPerPoint : 1f; }

        // Elements in `e` (itself included) whose `size` is `want` and whose background is `c`: a tab's underline, a rail entry's edge.
        static int Bars(VisualElement e, Func<VisualElement, float> size, float want, Color c)
            => e == null ? -1 : e.Query<VisualElement>().ToList().Count(x => Near(size(x), want, Snap) && Same(x.resolvedStyle.backgroundColor, c));

        // Every element named `name` under `e` has the inline size `want`, exactly (at least one).
        static bool Inline(VisualElement e, string name, Func<VisualElement, float> size, float want)
        {
            var l = e == null ? null : e.Query(name).ToList();
            return l != null && l.Count > 0 && l.All(x => size(x) == want);
        }

        static float Radius(VisualElement e) => e == null ? -1f : e.resolvedStyle.borderTopLeftRadius;

        // The tabs in a tab row narrower than their name or taller than a line: a tab that lost its text measure.
        static string Squashed(VisualElement bar)
        {
            var bad = bar == null ? null : bar.Query<Button>().ToList().Where(b =>
            {
                var m = b.MeasureTextSize(b.text, 0f, VisualElement.MeasureMode.Undefined, 0f, VisualElement.MeasureMode.Undefined);
                return b.contentRect.width + 0.5f < m.x || b.contentRect.height > m.y * 1.5f;
            }).Select(b => b.text + " " + b.contentRect.size);
            return bad == null ? "no row" : string.Join(", ", bad);
        }

        // The layout editor's chrome and the update toast in the suite look (A is selected; B and C are not).
        // Widths are read from the inline style: layout snaps resolved ones to device pixels.
        IEnumerator SuiteChrome()
        {
            var th = Theme.For(Root);
            foreach (var n in new[] { "layout-done", "layout-grip-0", "layout-knob", "layout-anchor-dot", "layout-crop-edge-0" })
                Check(Radius(Root.Q(n)) == 2f, n + " has the suite's 2px corner", n + " radius " + Radius(Root.Q(n)));
            var doneB = Root.Q<Button>("layout-done");
            var layerB = Root.Q("layout-layer");
            Check(doneB != null && layerB != null && Same(doneB.resolvedStyle.backgroundColor, th.Accent)
                  && doneB.worldBound.yMin - layerB.worldBound.yMin < 40f && layerB.worldBound.xMax - doneB.worldBound.xMax < 40f
                  && doneB.parent.IndexOf(doneB) > doneB.parent.IndexOf(Root.Q("layout-frame")?.parent),
                  "Done is a solid accent button at the top right, over the frames",
                  doneB == null ? "no Done" : "bg " + doneB.resolvedStyle.backgroundColor + " at " + doneB.worldBound + " in " + layerB?.worldBound);
            var frames = Root.Query("layout-frame").ToList();
            var sel = frames.Where(f => f.style.borderTopWidth.value == 2f && Same(f.resolvedStyle.borderTopColor, th.Accent)).ToList();
            var rest = frames.Except(sel).ToList();
            Check(frames.Count >= 3 && sel.Count == 1 && rest.All(f => f.style.borderTopWidth.value == 1f && Same(f.resolvedStyle.borderTopColor, th.TextMuted)),
                  "one frame wears the accent at 2px, the rest the muted text colour at 1px",
                  frames.Count + " frames, " + sel.Count + " selected; others " + string.Join(",", rest.Select(f => f.style.borderTopWidth.value + "/" + f.resolvedStyle.borderTopColor).ToArray()));
            var tags = Root.Query<Label>().ToList().Where(l => frames.Count > 0 && l.parent == frames[0].parent && l.text.StartsWith("Probe ")).ToList();
            Check(tags.Count >= 3 && tags.All(t => Radius(t) == 2f && Same(t.resolvedStyle.backgroundColor, th.Panel)),
                  "every frame tag has a 2px corner and the theme's panel colour", tags.Count + " tags; " + string.Join(",", tags.Select(t => Radius(t) + "/" + t.resolvedStyle.backgroundColor).ToArray()));

            var toast = new PrismLib.UI.Toast("Probe");
            toast.Set(new PrismLib.UI.ToastContent { Key = "probe", Title = "Probe toast", Hint = "hint" });
            toast.Tick();
            var cv = GameObject.Find("ProbeUpdateToastCanvas");
            var card = cv != null ? cv.transform.Find("UpdateToast") : null;
            var bg = card != null ? card.GetComponent<PrismLib.UI.RoundedRectGraphic>() : null;
            var pip = card != null ? card.Find("Pip") as RectTransform : null;
            var win = Suite.Window; win.a = 0.96f;
            Check(bg != null && bg.Radius == 2f && Same(bg.color, win, 0f), "the toast card is 2px round on the suite's window colour",
                  bg == null ? "no card" : bg.Radius + " / " + bg.color);
            Check(bg != null && Same(bg.BorderColor, Suite.Border, 0f), "its border is the suite's", bg == null ? "no card" : "" + bg.BorderColor);
            var texts = card != null ? card.GetComponentsInChildren<TMPro.TMP_Text>(true) : new TMPro.TMP_Text[0];
            var tt = texts.FirstOrDefault(t => t.text == "Probe toast"); var th2 = texts.FirstOrDefault(t => t.text == "hint");
            Check(tt != null && Same(tt.color, Suite.Text, 0f) && th2 != null && Same(th2.color, Suite.Muted, 0f), "its title is the suite's text colour and its hint the muted one",
                  tt == null || th2 == null ? "no labels" : tt.color + " / " + th2.color);
            Check(pip != null && Mathf.Approximately(pip.offsetMax.x - pip.offsetMin.x, 2f), "its pip is 2 wide", pip == null ? "no pip" : pip.offsetMin + " / " + pip.offsetMax);
            toast.Dispose();

            // a mod that sets its own colour keeps it
            var own = new PrismLib.UI.ToastTheme(); own.Background = new Color(1f, 0f, 1f, 1f);
            var toast2 = new PrismLib.UI.Toast("Probe2", own);
            toast2.Set(new PrismLib.UI.ToastContent { Key = "probe", Title = "Own toast" });
            toast2.Tick();
            var cv2 = GameObject.Find("Probe2UpdateToastCanvas");
            var card2 = cv2 != null ? cv2.transform.Find("UpdateToast") : null;
            var bg2 = card2 != null ? card2.GetComponent<PrismLib.UI.RoundedRectGraphic>() : null;
            Check(bg2 != null && Same(bg2.color, new Color(1f, 0f, 1f, 1f), 0f), "a toast theme with its own background keeps it", bg2 == null ? "no card" : "" + bg2.color);
            toast2.Dispose();
            yield return null;
        }

        // The animation duration: Anim.Duration is Facet's shared speed in ms. Synchronous - the bridges re-apply the config every frame, so a yield could see theirs.
        IEnumerator SuiteMotion()
        {
            Anim.Duration = 240;
            var speed = (float[])((System.Collections.Generic.Dictionary<string, object>)AppDomain.CurrentDomain.GetData("Facet.Shared"))["Facet.AnimSpeed"];
            Check(Anim.Duration == 240 && speed[0] == 2f, "Anim.Duration 240 reads back 240 and is Facet's shared speed 2", Anim.Duration + " / " + speed[0]);
            Anim.Duration = 9999; int hi = Anim.Duration;
            Anim.Duration = -5; int lo = Anim.Duration;
            Check(hi == 400 && lo == 0, "it clamps to 0-400", hi + " / " + lo);

            // the tick runs every frame with an unchanged value: it must not allocate
            Anim.Duration = 120;
            for (int i = 0; i < 1000; i++) Anim.Duration = 120;   // warm up
            long mem = GC.GetTotalMemory(false);
            for (int i = 0; i < 300000; i++) Anim.Duration = 120;
            long grew = GC.GetTotalMemory(false) - mem;
            Check(grew < 4096, "Facet setter allocates nothing", grew + " bytes over 300000 sets");

            // at 0 a window is shown at once: no fade, no slide, in the same call
            Anim.Duration = 0;
            var sp = new SettingsPanel("Probe Motion", () => new[]
            {
                new SettingRow { Owner = "LayoutProbe", Page = "Motion", Group = "Look", Label = "Motion switch", Kind = SettingControl.Bool, Get = () => false, Set = _ => { } },
            });
            sp.Visible = true;
            var win = PanelRoot("ProbeMotionPanel")?.Children().FirstOrDefault();
            Check(win != null && win.style.opacity.value == 1f && win.style.translate.value.y.value == 0f, "at 0 ms the window opens at full opacity and in place, in the same call",
                  win == null ? "no window" : "opacity " + win.style.opacity.value + ", translate y " + win.style.translate.value.y.value);
            sp.Dispose();
            Anim.Duration = 120;
            yield return null;
        }

        // PrismLib.dll, which the probe does not reference: the copy the mods loaded, through reflection.
        static Type Lib(string name) => AppDomain.CurrentDomain.GetAssemblies().Where(x => x.GetName().Name == "PrismLib")
            .Select(x => x.GetType("PrismLib." + name)).FirstOrDefault(t => t != null);

        static bool? SharedSmooth()
        {
            var d = AppDomain.CurrentDomain.GetData("Facet.Shared") as Dictionary<string, object>;
            object v;
            return d != null && d.TryGetValue("Facet.SmoothScroll", out v) && v is bool[] && ((bool[])v).Length > 0 ? ((bool[])v)[0] : (bool?)null;
        }

        /* Smooth scrolling: Anim.SmoothScroll is Facet's shared switch, and PrismLib's scroll.smooth is the player's.
           Every bridge copies the setting into the switch each frame, so the check sets scroll.smooth through Config
           and makes a bridge's copy in the frame it steps, never Facet's flag alone. The settings file is put back
           byte for byte, a missing key still missing. */
        IEnumerator SuiteSmooth()
        {
            bool was = Anim.SmoothScroll;
            Anim.SmoothScroll = true; var on = SharedSmooth();
            Anim.SmoothScroll = false; var off = SharedSmooth();
            Check(on == true && off == false, "Anim.SmoothScroll writes Facet's shared switch", on + " / " + off);
            for (int i = 0; i < 1000; i++) Anim.SmoothScroll = false;   // warm up
            long mem = GC.GetTotalMemory(false);
            for (int i = 0; i < 300000; i++) Anim.SmoothScroll = false;
            long grew = GC.GetTotalMemory(false) - mem;
            Check(grew < 4096, "...and allocates nothing when unchanged", grew + " bytes over 300000 sets");

            var config = Lib("Config");
            var smooth = Lib("Prism")?.GetProperty("SmoothScroll");
            Check(config != null && smooth != null, "the loaded PrismLib has scroll.smooth", config == null ? "no PrismLib loaded" : "no Prism.SmoothScroll");
            if (config == null || smooth == null) { Anim.SmoothScroll = was; yield break; }
            Func<string> stored = () => (string)config.GetMethod("Get").Invoke(null, new object[] { "scroll.smooth", null });
            Action<string> setting = v => config.GetMethod("Set").Invoke(null, new object[] { "scroll.smooth", v });
            Action copy = () => Anim.SmoothScroll = (bool)smooth.GetValue(null, null);   // what a bridge does each frame
            string path = (string)config.GetProperty("FilePath").GetValue(null, null), wasStored = stored();
            byte[] file = path != null && File.Exists(path) ? File.ReadAllBytes(path) : null;

            var rows = Enumerable.Range(0, 40).Select(i => new SettingRow
                { Owner = "LayoutProbe", Page = "Smooth", Group = "Rows", Label = "Smooth row " + i, Kind = SettingControl.Bool, Get = () => false, Set = _ => { } }).ToArray();
            var sp = new SettingsPanel("Probe Smooth", () => rows);
            try
            {
                sp.Visible = true;
                for (int i = 0; i < 5; i++) yield return null;
                var root = PanelRoot("ProbeSmoothPanel");
                var sv = root?.Q<ScrollView>();
                Check(sv != null && sv.contentContainer.layout.height > sv.contentViewport.layout.height + 120f, "a SettingsPanel of 40 rows scrolls",
                      sv == null ? "no ScrollView" : sv.contentContainer.layout.height + " in " + sv.contentViewport.layout.height);
                if (sv == null) yield break;
                float k = root.layout.width / Screen.width;
                var c = new Vector2(sv.worldBound.center.x / k, Screen.height - sv.worldBound.center.y / k);

                // off: the notch lands in the frame it arrives, set and stepped in that one frame
                float y0 = sv.scrollOffset.y;
                setting("0"); copy(); Facet.Skin.Wheel(root, c, -1f);
                float step = sv.scrollOffset.y - y0;
                Check(step > 0f && !Anim.SmoothScroll, "scroll.smooth off: a wheel notch lands in the frame it arrives", step + ", switch " + Anim.SmoothScroll);
                yield return null;

                // on: nothing in that frame, a quarter in between start and target, then exactly the same step
                float same = 0f, mid = 0f, end = 0f; bool late = false;
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    y0 = sv.scrollOffset.y;
                    float t0 = Time.realtimeSinceStartup;
                    setting("1"); copy(); Anim.Duration = 120; Facet.Skin.Wheel(root, c, -1f);
                    same = sv.scrollOffset.y;
                    while (Time.realtimeSinceStartup - t0 < 0.05f) yield return null;
                    mid = sv.scrollOffset.y;
                    late = Time.realtimeSinceStartup - t0 >= 0.2f;
                    while (Time.realtimeSinceStartup - t0 < 0.2f) yield return null;
                    yield return null; yield return null;
                    end = sv.scrollOffset.y;
                    if (!late) break;
                    Main.Log("INFO smooth sample ran past the glide's end; re-running the step once");
                }
                Check(Mathf.Approximately(same, y0), "scroll.smooth on: the notch moves nothing in the frame it arrives", (same - y0).ToString());
                Check(mid > y0 && mid < y0 + step, "...a quarter into the glide the offset is between start and target", y0 + " < " + mid + " < " + (y0 + step) + (late ? " (late)" : ""));
                Check(Math.Abs(end - y0 - step) <= 0.5f, "...and it settles exactly the off step further", (end - y0) + " vs " + step);
            }
            finally
            {
                sp.Dispose();
                if (file != null) File.WriteAllBytes(path, file); else if (path != null) File.Delete(path);
                config.GetField("_values", BindingFlags.NonPublic | BindingFlags.Static)?.SetValue(null, null);   // reloads from the file
                Anim.SmoothScroll = was;
            }
            Check(stored() == wasStored, "scroll.smooth is put back as it was", "'" + stored() + "' vs '" + wasStored + "'");
        }

        IEnumerator SuiteSkin()
        {
            Color red = Color.red, green = Color.green, magenta = new Color(1f, 0f, 1f, 1f);
            Color window = new Color32(0x1A, 0x1A, 0x1F, 0xFF), sidebar = new Color32(0x11, 0x11, 0x15, 0xFF), danger = new Color32(0xE2, 0x67, 0x6D, 0xFF);

            // Every field Wear owns is set by it, none left to Dark: blank them all, wear, and look.
            var probe = new Facet.Theme();
            var looks = typeof(Facet.Looks).GetFields(BindingFlags.Public | BindingFlags.Instance).Where(f => f.FieldType == typeof(Facet.Look)).ToArray();
            var sizes = typeof(Facet.Sizes).GetFields(BindingFlags.Public | BindingFlags.Instance).Where(f => f.FieldType == typeof(float)).ToArray();
            foreach (var f in looks) f.SetValue(probe.Looks, null);
            foreach (var f in sizes) f.SetValue(probe.Sizes, float.NaN);
            var roles = (Facet.Role[])Enum.GetValues(typeof(Facet.Role));
            foreach (var r in roles) probe[r] = magenta;
            Suite.Wear(probe);
            var worn = looks.Select(f => f.GetValue(probe.Looks)).ToList();
            Check(looks.Length == 23 && worn.All(l => l != null) && worn.Distinct().Count() == worn.Count, "Suite.Wear sets all 23 Looks, each its own Look",
                  looks.Length + " fields; unset: " + string.Join(", ", looks.Where(f => f.GetValue(probe.Looks) == null).Select(f => f.Name).ToArray()));
            Check(sizes.Length == 38 && sizes.All(f => !float.IsNaN((float)f.GetValue(probe.Sizes))), "Suite.Wear sets all 38 Sizes",
                  sizes.Length + " fields; unset: " + string.Join(", ", sizes.Where(f => float.IsNaN((float)f.GetValue(probe.Sizes))).Select(f => f.Name).ToArray()));
            Check(probe.Sizes.ControlPad == 5f && probe.Sizes.FieldPad == 4f && probe.Sizes.ItemHeight == 38f, "with ControlPad 5, FieldPad 4 and ItemHeight 38",
                  probe.Sizes.ControlPad + "/" + probe.Sizes.FieldPad + "/" + probe.Sizes.ItemHeight);
            // Guide (the canvas's snap guides) is a fixed accent like the selection colour, not a neutral.
            var mine = new[] { Facet.Role.Accent, Facet.Role.Focus, Facet.Role.AccentText, Facet.Role.Guide };
            Check(roles.All(r => (probe[r] == magenta) == mine.Contains(r)), "it sets every neutral role and leaves Accent, Focus, AccentText and Guide alone",
                  string.Join(", ", roles.Where(r => (probe[r] == magenta) != mine.Contains(r)).Select(r => r.ToString()).ToArray()));
            Facet.Theme dark = null; string threw = "";
            try { dark = Suite.Wear(Facet.Theme.Dark); } catch (Exception e) { threw = e.Message; }
            Check(dark == Facet.Theme.Dark && Facet.Theme.Dark.Sizes.Radius == 3f && Suite.Wear(null) == null, "Wear(Dark) hands Dark back untouched, Wear(null) null", threw);

            // Recolour on a suite theme: the accent and Focus change, the neutrals are the suite's again, Changed fires once; Default also sets Tokens.Accent.
            {
                var rc = new Theme();
                int fired = 0;
                rc.Twin.Changed += () => fired++;
                rc.Twin[Facet.Role.Panel] = magenta;
                rc.Recolour(green);
                Check(rc.Twin[Facet.Role.Accent] == green && rc.Twin[Facet.Role.Focus] == green && Same(rc.Twin[Facet.Role.Panel], window) && Same(rc.Twin[Facet.Role.Rail], sidebar)
                      && rc.Twin.Looks.Group != null && fired == 1, "Recolour on a suite theme sets accent and Focus, keeps the suite's neutrals, and fires Changed",
                      "accent " + rc.Twin[Facet.Role.Accent] + ", focus " + rc.Twin[Facet.Role.Focus] + ", panel " + rc.Twin[Facet.Role.Panel] + ", fired " + fired);
                // AccentText follows: a light accent and a dark one each get the text Facet derives for them.
                var navy = new Color(0.05f, 0.08f, 0.3f, 1f);
                var onGreen = rc.Twin[Facet.Role.AccentText];
                rc.Recolour(navy);
                var onNavy = rc.Twin[Facet.Role.AccentText];
                Check(Same(onGreen, Facet.Theme.FromAccent(green)[Facet.Role.AccentText]) && Same(onNavy, Facet.Theme.FromAccent(navy)[Facet.Role.AccentText]),
                      "Recolour on a suite theme sets AccentText to the text that reads on the new accent",
                      "on green " + onGreen + ", on navy " + onNavy);
                var tokAccent = Tokens.Accent;
                Theme.Default.Recolour(green);
                Check(Tokens.Accent == green && Theme.Default.Twin[Facet.Role.Accent] == green && Same(Theme.Default.Twin[Facet.Role.Panel], window),
                      "Recolour on Default also sets Tokens.Accent", Tokens.Accent + " / " + Theme.Default.Twin[Facet.Role.Accent]);
                // A mod's pushed accent (Tokens.Accent, read on For) and the Accent setter carry AccentText too.
                Tokens.Accent = navy;
                var forDefault = Theme.For(null);
                var own = new Theme();
                own.Accent = navy;   // a dark accent: Facet's default AccentText is dark, so only a derived one reads
                Check(forDefault == Theme.Default && Same(Theme.Default.Twin[Facet.Role.AccentText], Facet.Theme.FromAccent(navy)[Facet.Role.AccentText])
                      && Same(own.Twin[Facet.Role.AccentText], Facet.Theme.FromAccent(navy)[Facet.Role.AccentText]),
                      "A pushed accent and the Accent setter set AccentText to the text that reads on the accent",
                      "Default on navy " + Theme.Default.Twin[Facet.Role.AccentText] + ", set to navy " + own.Twin[Facet.Role.AccentText]);
                Tokens.Accent = tokAccent; Theme.Default.Refresh();
            }

            // A mod that pushes its whole palette: Prism windows take the accent and nothing else.
            var wasLanguage = Words.Language;
            Words.Language = () => "en";
            Color wasPanel = Tokens.Panel, wasRowHover = Tokens.RowHover, wasDanger = Tokens.Danger, wasAccent = Tokens.Accent;
            Tokens.Panel = Tokens.RowHover = magenta;
            Tokens.Danger = new Color(1f, 0.48f, 0.48f, 1f);
            Tokens.Accent = red;
            Action<object> set = _ => { };
            var rows = new[]
            {
                new SettingRow { Owner = "LayoutProbe", Page = "Suite", Group = "Look", Label = "Suite switch", Kind = SettingControl.Bool, Get = () => false, Set = set },
                new SettingRow { Owner = "LayoutProbe", Page = "Suite", Group = "Look", Label = "Suite size", Kind = SettingControl.Float, Min = 0f, Max = 2f, Get = () => 0.5f, Set = set },
                new SettingRow { Owner = "LayoutProbe", Page = "Suite", Group = "Look", Label = "Suite pick", Kind = SettingControl.Dropdown,
                                 Options = new[] { "One", "Two", "Three" }, Get = () => 1, Set = set },
                new SettingRow { Owner = "LayoutProbe", Page = "Suite", Group = "Other", Label = "Suite other", Kind = SettingControl.Bool, Get = () => true, Set = set },
            };
            var sp = new SettingsPanel("Probe Suite", () => rows);
            sp.Visible = true;
            for (int i = 0; i < 3; i++) yield return null;
            var root = PanelRoot("ProbeSuitePanel");
            var win = root?.Children().FirstOrDefault();
            var th = Facet.Theme.For(win);
            SnapTo(win);
            Main.Log("[dbg] suite: one device pixel is " + Snap.ToString("0.000") + " panel px");
            Func<string> labels = () => root == null ? "no panel" : string.Join(" | ", root.Query<Label>().ToList().Select(l => l.text).Where(s => !string.IsNullOrEmpty(s)).Take(14).ToArray());
            var sz = th.Sizes;
            // Pill switches: a fully round track and a round knob; every other corner stays 2.
            Check(win != null && sz.Radius == 2f && sz.SwitchRadius == sz.SwitchHeight / 2f && sz.KnobRadius == (sz.SwitchHeight - 4f) / 2f && sz.Body == 13f && sz.Title == 17f && sz.Small == 12f
                  && sz.Caps == 11f && sz.RowHeight == 38f && sz.NavRowHeight == 28f && sz.TitleBarHeight == 36f && sz.SwitchWidth == 30f
                  && sz.SwitchHeight == 16f && sz.Track == 3f && sz.DropdownWidth == 140f, "Prism · Settings builds with the suite's sizes",
                  "radius " + sz.Radius + "/" + sz.SwitchRadius + "/" + sz.KnobRadius + ", type " + sz.Title + "/" + sz.Body + "/" + sz.Small + "/" + sz.Caps
                  + ", rows " + sz.RowHeight + "/" + sz.NavRowHeight + "/" + sz.TitleBarHeight + ", switch " + sz.SwitchWidth + "x" + sz.SwitchHeight
                  + ", track " + sz.Track + ", dropdown " + sz.DropdownWidth);
            // The rest of Sizes, exact: layout snaps to device pixels, so only the values themselves can tell a 1px line from a 2px one.
            Check(sz.Line == 1f && sz.Indicator == 2f && sz.Pad == 12f && sz.Gap == 8f && sz.ListRowHeight == 24f && sz.CapsSpacing == 1f
                  && sz.ThumbWidth == 4f && sz.ThumbHeight == 14f && sz.SliderWidth == 160f && sz.NumberWidth == 64f && sz.Icon == 16f && sz.RowPadY == 6f && sz.TabGap == 20f,
                  "and the rest of its sizes: line 1, indicator 2, pad 12, gap 8, list row 24, caps spacing 1, thumb 4x14, slider 160, number 64, icon 16, row text pad 6, tab gap 20",
                  "line " + sz.Line + ", indicator " + sz.Indicator + ", pad/gap " + sz.Pad + "/" + sz.Gap + ", list row " + sz.ListRowHeight + ", spacing " + sz.CapsSpacing
                  + ", thumb " + sz.ThumbWidth + "x" + sz.ThumbHeight + ", slider " + sz.SliderWidth + ", number " + sz.NumberWidth + ", icon " + sz.Icon
                  + ", row text pad " + sz.RowPadY + ", tab gap " + sz.TabGap);
            Check(Same(th[Facet.Role.Panel], window) && Same(th[Facet.Role.RowHover], new Color(1f, 1f, 1f, 0.05f)) && Same(th[Facet.Role.Danger], danger)
                  && Same(th[Facet.Role.Rail], sidebar) && Near(th[Facet.Role.Line].a, 0.07f, 0.001f) && Near(th[Facet.Role.Off].a, 0.18f, 0.001f),
                  "its neutrals are the suite's, not the ones pushed into Tokens",
                  "panel " + th[Facet.Role.Panel] + ", hover " + th[Facet.Role.RowHover] + ", danger " + th[Facet.Role.Danger] + ", rail " + th[Facet.Role.Rail]
                  + ", line " + th[Facet.Role.Line] + ", off " + th[Facet.Role.Off]);
            Check(th[Facet.Role.Accent] == red, "and its accent is the pushed one", th[Facet.Role.Accent].ToString());
            Check(Near(Radius(win), 2f, 0.01f) && win != null && Same(win.resolvedStyle.backgroundColor, window), "the window has 2px corners on the suite's window colour",
                  win == null ? "no window" : "radius " + Radius(win) + ", bg " + win.resolvedStyle.backgroundColor);

            // Cards: the group's name in capitals, the row fill on the card, rows split by a line
            var lookCap = LabelIn(root, "LOOK");
            var card = lookCap?.parent?.parent;   // caps, its head, the card
            var swRow = root?.Q("Suite switch");
            var sizeRow = root?.Q("Suite size");
            Check(lookCap != null && LabelIn(root, "OTHER") != null, "each group is a card headed by its name in capitals", labels());
            Check(card != null && swRow != null && Same(card.resolvedStyle.backgroundColor, th[Facet.Role.Row]) && (swRow.resolvedStyle.backgroundColor.a == 0f || Same(swRow.resolvedStyle.backgroundColor, th[Facet.Role.RowHover])),   // RowHover: the real pointer may rest on the row
                  "the card carries the row fill and its rows none of their own",
                  card == null || swRow == null ? "no card or row" : "card " + card.resolvedStyle.backgroundColor + ", row " + swRow.resolvedStyle.backgroundColor);
            Check(sizeRow != null && swRow != null && sizeRow.style.borderTopWidth.value == 1f && Near(sizeRow.resolvedStyle.borderTopWidth, 1f, Snap) && swRow.resolvedStyle.borderTopWidth == 0f
                  && Same(sizeRow.resolvedStyle.borderTopColor, th[Facet.Role.Line]), "a 1px line splits a card's rows, none above the first",
                  sizeRow == null || swRow == null ? "no rows" : "size " + sizeRow.resolvedStyle.borderTopWidth + " " + sizeRow.resolvedStyle.borderTopColor + ", switch " + swRow.resolvedStyle.borderTopWidth);
            Check(swRow != null && Near(swRow.resolvedStyle.height, 38f, Snap), "a one-line row is 38 tall", swRow == null ? "no row" : swRow.resolvedStyle.height.ToString("0.00"));
            var swLabel = LabelIn(swRow, "Suite switch");
            Check(swLabel != null && swLabel.resolvedStyle.fontSize == 13f, "a row label is 13px", swLabel == null ? "no label" : swLabel.resolvedStyle.fontSize.ToString());
            var control = swRow?.Q("control");
            var track = control != null && control.childCount > 0 ? control.ElementAt(0) : null;
            var knob = track != null && track.childCount > 0 ? track.ElementAt(0) : null;
            Check(track != null && knob != null && Near(track.resolvedStyle.width, 30f, Snap) && Near(track.resolvedStyle.height, 16f, Snap) && Near(Radius(track), 8f, 0.01f)
                  && Near(Radius(knob), 6f, 0.01f) && Near(knob.resolvedStyle.width, 12f, Snap)
                  && Same(track.resolvedStyle.backgroundColor, th[Facet.Role.Off]), "an off switch is a 30x16 pill (radius 8) with a round 12px knob, in the off colour",
                  track == null || knob == null ? "no track or knob" : track.resolvedStyle.width + "x" + track.resolvedStyle.height + " r" + Radius(track)
                  + ", knob " + knob.resolvedStyle.width + " r" + Radius(knob) + " " + track.resolvedStyle.backgroundColor);
            var fill = root?.Q("Suite size-fill");
            Check(fill != null && Same(fill.resolvedStyle.backgroundColor, red), "a slider's filled part is the accent", fill == null ? "no fill" : fill.resolvedStyle.backgroundColor.ToString());
            // The knob's round corner would be 6 on a 4 px wide thumb; UI Toolkit only hides that by clamping.
            var thumb = sizeRow?.Q(className: "unity-base-slider__dragger");
            Check(thumb != null && thumb.style.borderTopLeftRadius.value.value == 2f, "a slider thumb's corner is 2, half its 4 px width",
                  thumb == null ? "no thumb" : thumb.style.borderTopLeftRadius.value.value.ToString());
            var pick = root?.Q("Suite pick-input");
            Check(pick != null && pick.style.minWidth.value.value >= 140f, "a dropdown is at least 140 wide", pick == null ? "no dropdown" : pick.style.minWidth.value.value.ToString());
            var tab = root?.Query<Button>().ToList().FirstOrDefault(b => b.text == "Suite");
            var tabBar = tab?.parent?.parent;   // each tab sits in a cell with its underline
            int under = Bars(tabBar, x => x.resolvedStyle.height, 2f, red);
            var tabRule = tabBar?.Q("rule");
            Check(under == 1 && Inline(tabBar, "indicator", x => x.style.height.value.value, 2f) && tabRule != null && tabRule.style.height.value.value == 1f,
                  "the selected tab, and only it, has a 2px accent underline (a 2px bar in every tab, a 1px rule under them)", under + " found");
            var page = root?.Q<ScrollView>();
            float tabGap = page == null || tabBar == null || page.parent != tabBar.parent ? -1f : page.layout.yMin - tabBar.layout.yMax;
            Check(Near(tabGap, 20f, Snap), "the page starts 20 under its tab row, a Pad more than Facet's Gap", tabGap.ToString("0.00"));
            string sq = Squashed(tabBar);
            Check(sq == "", "every settings tab is as wide as its name and one line tall", sq);

            // A theme of the caller's own: Ui.Flat paints its FlatButton look, not Facet's preset
            var custom = Suite.Wear(new Facet.Theme());
            custom.Looks.FlatButton = new Facet.Look { Bg = Facet.Role.Danger };
            var holder = new VisualElement();
            win?.Add(holder);
            Facet.Theme.Set(holder, custom);
            var flat = Ui.Btn("Probe flat", null, holder);
            Ui.Flat(flat);
            yield return null;
            Check(win != null && Same(flat.resolvedStyle.backgroundColor, custom[Facet.Role.Danger]), "Ui.Flat paints the theme's own Looks.FlatButton",
                  flat.resolvedStyle.backgroundColor.ToString());
            holder.RemoveFromHierarchy();

            // A search: each hit in a card headed owner · page · group
            var sv = root?.Q<ScrollView>();
            var search = root?.Query<TextField>().ToList().FirstOrDefault(f => sv != null && f.parent == sv.parent);
            if (search != null) { search.value = "suite size"; yield return null; }
            var hitCap = LabelIn(root, "LAYOUTPROBE · SUITE · LOOK");
            Check(search != null && hitCap?.parent?.parent?.Q("Suite size") != null, "a search hit sits in a card headed LAYOUTPROBE · SUITE · LOOK",
                  search == null ? "no search box" : labels());
            if (search != null) search.value = "";

            // A mod's own scheme: the suite, with that mod's accent
            SettingsPanel minePanel;
            using (Theme.Use(new Theme { Accent = green })) minePanel = new SettingsPanel("Probe Mine", () => rows);
            minePanel.Visible = true;
            for (int i = 0; i < 3; i++) yield return null;
            var mwin = PanelRoot("ProbeMinePanel")?.Children().FirstOrDefault();
            var mth = Facet.Theme.For(mwin);
            Check(mwin != null && Near(Radius(mwin), 2f, 0.01f) && Same(mth[Facet.Role.Panel], window) && mth[Facet.Role.Accent] == green,
                  "a mod's own scheme wears the suite with that mod's accent",
                  mwin == null ? "no window" : "radius " + Radius(mwin) + ", panel " + mth[Facet.Role.Panel] + ", accent " + mth[Facet.Role.Accent]);

            // Another host's window on the same Facet keeps Facet's own look
            var plainHost = Facet.Host.Create("LayoutProbe/plain", o => { });
            var plain = new Facet.Surface(plainHost, "ProbePlain", Facet.Layer.Window);
            plain.Visible = true;
            var pw = plain.Root == null ? null : new Facet.Window(plain.Root, "ProbePlain", "Probe Plain", new Rect(200f, 120f, 420f, 300f));
            for (int i = 0; i < 3; i++) yield return null;
            Check(pw != null && Near(Radius(pw.Root), 3f, 0.01f) && Facet.Theme.For(pw.Root).Sizes.Body == 15f, "another host's window keeps Facet's radius 3 and body 15",
                  pw == null ? "no window" : "radius " + Radius(pw.Root) + ", body " + Facet.Theme.For(pw.Root).Sizes.Body);

            // The debug window
            var dp = new DebugPanel("Probe Debug", () => new[]
            {
                new DebugTab { Name = "Probe A", Lines = () => new[] { "probe a" } },
                new DebugTab { Name = "Probe B", Lines = () => new[] { "probe b" } },
            });
            dp.Visible = true;
            for (int i = 0; i < 3; i++) yield return null;
            var droot = FindObjectsByType<UIDocument>(FindObjectsSortMode.None).Where(d => d.gameObject.name == "PrismDebugPanel")
                .Select(d => d.rootVisualElement).FirstOrDefault(r => LabelIn(r, "Probe Debug") != null);
            var dwin = droot?.Children().FirstOrDefault();
            var dth = Facet.Theme.For(dwin);
            SnapTo(dwin);
            var dtab = droot?.Query<Button>().ToList().FirstOrDefault(b => b.text == "Probe A");
            var dbar = dtab?.parent?.parent;   // the tab's cell, then the row
            var filter = droot?.Query<TextField>().ToList().FirstOrDefault(f => dbar != null && f.parent == dbar.parent);
            var input = filter?.Q(className: "unity-base-text-field__input");
            Check(dwin != null && Near(Radius(dwin), 2f, 0.01f), "the debug window has 2px corners", dwin == null ? "no window" : Radius(dwin).ToString());
            int dunder = Bars(dbar, x => x.resolvedStyle.height, 2f, red);
            Check(dunder == 1 && Inline(dbar, "indicator", x => x.style.height.value.value, 2f), "its selected tab, and only it, has a 2px accent underline", dunder + " found");
            string dsq = Squashed(dbar);
            Check(dsq == "", "every debug tab (Probe A, Probe B) is as wide as its name and one line tall", dsq);
            Check(input != null && Same(input.resolvedStyle.borderTopColor, dth[Facet.Role.Line]) && Near(input.resolvedStyle.borderTopColor.a, 0.07f, 0.002f),
                  "its filter box is outlined in the suite's line", input == null ? "no input" : input.resolvedStyle.borderTopColor.ToString());

            // The rail of a mod's own menu
            var rp = new SettingsPanel("Probe Rail", () => PrismMenuShape(() => { }));
            rp.Visible = true;
            for (int i = 0; i < 3; i++) yield return null;
            var views = PanelRoot("ProbeRailPanel")?.Query<ScrollView>().ToList();
            var rail = views != null && views.Count > 1 ? views[0] : null;
            SnapTo(rail);
            var bars = rail == null ? new System.Collections.Generic.List<VisualElement>() : rail.Query("indicator").ToList();
            Check(rail != null && Same(rail.resolvedStyle.backgroundColor, sidebar), "the rail is the suite's sidebar colour",
                  rail == null ? "no rail" : rail.resolvedStyle.backgroundColor.ToString());
            int lit = bars.Count(b => Near(b.resolvedStyle.width, 2f, Snap) && Same(b.resolvedStyle.backgroundColor, red));
            Check(lit == 1 && bars.Count > 0 && bars.All(b => b.style.width.value.value == 2f), "exactly one rail entry carries a 2px accent bar (every bar 2px inline)", lit + " of " + bars.Count);
            Check(bars.Count > 0 && bars.All(b => b.parent.layout.height >= 28f - Snap), "rail entries are at least 28 tall",
                  string.Join(",", bars.Select(b => b.parent.layout.height.ToString("0.#")).Take(6).ToArray()));

            rp.Dispose(); dp.Dispose(); plain.Dispose(); minePanel.Dispose(); sp.Dispose();
            Tokens.Panel = wasPanel; Tokens.RowHover = wasRowHover; Tokens.Danger = wasDanger; Tokens.Accent = wasAccent;
            Words.Language = wasLanguage;
        }
    }
}
