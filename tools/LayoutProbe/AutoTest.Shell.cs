// The suite shell (SuiteShell over a SuiteAdapter): window, rail, tabs, search, stored state, Escape and the
// layout editor's round trip, driven through DemoSuite.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using PrismLib.UI.Toolkit;
using UnityEngine;
using UnityEngine.UIElements;

namespace LayoutProbe
{
    sealed partial class AutoTest
    {
        static string Placeholder(TextField f)
        {
            var p = typeof(ITextEdition).GetProperty("placeholder");
            return f == null || p == null ? null : p.GetValue(f.textEdition) as string;
        }

        static List<string> Texts(VisualElement e) =>
            e == null ? new List<string>() : e.Query<TextElement>().ToList().Select(t => t.text).ToList();

        // The rail sits beside the "rail-space" spacer; a compact rail is moved to the end of that row, so order says nothing.
        static bool IsRail(ScrollView v) => v.parent != null && v.parent.Q("rail-space") != null && v.parent.IndexOf(v) >= 0;
        static ScrollView Rail(SuiteShell s) => s.Window?.Body.Query<ScrollView>().ToList().FirstOrDefault(IsRail);
        static ScrollView Content(SuiteShell s) => s.Window?.Body.Query<ScrollView>().ToList().FirstOrDefault(v => !IsRail(v));

        static int Count(List<string> lines, string part) => lines.Count(l => l.Contains(part));

        static float Order(Facet.Surface s) => FindObjectsByType<UIDocument>(FindObjectsSortMode.None)
            .Where(d => s.Root != null && d.rootVisualElement != null && d.rootVisualElement.panel == s.Root.panel)
            .Select(d => d.panelSettings.sortingOrder).DefaultIfEmpty(-1f).First();

        static void PressAt(VisualElement root, Vector2 panel)
        {
            foreach (var t in new[] { EventType.MouseDown, EventType.MouseUp })
            {
                var ev = new Event { type = t, button = 0, clickCount = 1, mousePosition = panel };
                EventBase e = t == EventType.MouseDown ? PointerDownEvent.GetPooled(ev) : (EventBase)PointerUpEvent.GetPooled(ev);
                using (e) root.panel.visualTree.SendEvent(e);
            }
        }

        // A text on screen under root: no element between them is display:none (a hidden tab keeps its cell).
        static bool ShownText(VisualElement root, string text) => root != null && root.Query<TextElement>().ToList().Any(t =>
        {
            if (t.text != text) return false;
            for (VisualElement e = t; e != null && e != root; e = e.parent)
                if (e.style.display.value == DisplayStyle.None) return false;
            return true;
        });

        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

        IEnumerator SuiteShellChecks()
        {
            var shells = new List<SuiteShell>();
            int hidden = 0;
            Action countHidden = () => hidden++;

            var demo = new DemoSuite();
            demo.Store[SuiteShell.PageStore] = "options.main";
            var shell = new SuiteShell(demo);
            shells.Add(shell);
            demo.Shell = shell;   // its pages use the shell's rows, built before the shell holds its window
            shell.Hidden += countHidden;
            shell.Prebuild();
            Check(!shell.Visible && !demo.Store.ContainsKey("window.probe.menu"), "shell: a shell built ahead and not shown stores no window rect",
                  string.Join(",", demo.Store.Keys.ToArray()));
            Check(hidden == 0, "shell: Prebuild raises no Hidden", hidden.ToString());
            Check(shell.CurrentPage == "options.main" && demo.Store[SuiteShell.PageStore] == "options.main",
                  "shell: Prebuild opens the stored page, read before the first page stores itself", shell.CurrentPage);
            Check(demo.Built.Contains("start.home") && LabelIn(Content(shell), "options.main") != null,
                  "shell: the page built during Prebuild has its rows", string.Join(",", demo.Built.ToArray()));
            Check(demo.StartLayout != null && demo.StartLayout.style.display != DisplayStyle.None,
                  "shell: the page built during Prebuild has its shell-made rows", demo.StartLayout == null ? "no row" : "hidden");

            shell.Open();
            Rect want = default(Rect), got = default(Rect);
            bool placed = false;
            for (int i = 0; i < 10 && !placed; i++)
            {
                yield return null;
                var panel = shell.Surface.Root.layout.size;
                want = new Rect(panel.x * 0.1f, panel.y * 0.1f, panel.x * 0.8f, panel.y * 0.8f);
                got = shell.Window.Rect;
                placed = panel.x > 0f && Near(got.x, want.x, 2f) && Near(got.y, want.y, 2f) && Near(got.width, want.width, 2f) && Near(got.height, want.height, 2f);
            }
            Check(placed && shell.FreshGeometry, "shell: opens at 80% of the panel, centred", got + " want " + want + " fresh " + shell.FreshGeometry);

            var logo = shell.Window.TitleBarLeft.childCount > 0 ? shell.Window.TitleBarLeft[0] : null;
            Check(logo != null && logo.name == "menu-logo" && logo.style.backgroundImage.value.texture != null,
                  "shell: the title bar leads with the logo", logo == null ? "none" : logo.name);

            var rail = Texts(Rail(shell));
            int iStart = rail.IndexOf("Start"), iThings = rail.IndexOf("Things"), iOptions = rail.IndexOf("Options");
            int rules = Rail(shell)?.Query("rule").ToList().Count ?? -1;
            Check(iStart >= 0 && iStart < iThings && iThings < iOptions && rules == 2 && !rail.Contains("Aside") && !rail.Contains("More")
                  && shell.Nav.Inset == demo.RailWidth + 7f,
                  "shell: three ruled groups at the adapter's rail width", string.Join("|", rail.ToArray()) + " rules " + rules + " inset " + shell.Nav.Inset);

            var sz = Facet.Theme.For(shell.Window.Root).Sizes;
            Check(sz.TabGap == sz.Gap + sz.Pad && sz.RowPadY == 6f, "shell: the window wears the suite look", sz.TabGap + " / " + sz.RowPadY);

            var bar = Texts(shell.Window.TitleBarRight);
            Check(bar.Any(t => t.Contains("↶")) && bar.Any(t => t.Contains("↷")), "shell: the title bar keeps undo and redo", string.Join("|", bar.ToArray()));

            var other = new Facet.Surface(demo.Host, "ProbeShellOther", Facet.Layer.Window);
            new Facet.Window(other.Root, "probeshellother", "Other", new Rect(100, 100, 300, 200));
            yield return null;
            bool under = Order(other) > Order(shell.Surface);
            PressAt(shell.Surface.Root, shell.Window.TitleBarLeft.worldBound.center);
            Check(under && Order(shell.Surface) > Order(other), "shell: a press on the menu raises it over a later window",
                  Order(shell.Surface) + " / " + Order(other));
            other.Dispose();
            yield return null;

            shell.Go("things.beta");
            Check(shell.CurrentPage == "things.beta" && shell.TabRow != null && shell.TabRow.Selected == 1 && demo.Store[SuiteShell.PageStore] == "things.beta",
                  "shell: Go opens a page on its tab", shell.CurrentPage + " tab " + (shell.TabRow == null ? -9 : shell.TabRow.Selected));
            shell.Go("things.off");
            bool offTab = shell.TabRow != null && shell.TabRow.Selected == 2;
            shell.Go("start.home");
            Check(offTab && shell.TabRow == null && shell.CurrentPage == "start.home", "shell: Go selects the third tab and leaves tabs for a plain page");
            shell.Go("aside.page");
            Check(shell.CurrentPage == "start.home", "shell: an off-rail page key does nothing", shell.CurrentPage);

            Check(Placeholder(shell.Window.Search) == demo.Host.Translate("Find"), "shell: search shows the adapter's word",
                  Placeholder(shell.Window.Search));
            shell.Window.Search.value = "Gizmo";
            yield return null;
            bool found = LabelIn(Content(shell), "Gizmo") != null;
            shell.Window.Search.value = "";
            yield return null;
            Check(found && LabelIn(Content(shell), "start.home") != null, "shell: search finds the adapter's rows and clearing it returns to the page");

            shell.Go("things.alpha");
            demo.Lang = "ko";
            demo.Host.RelabelSurfaces();
            yield return null;
            Check(Placeholder(shell.Window.Search) == "검색" && Texts(shell.TabRow?.Element).Contains("알파"), "shell: relabels with the host",
                  Placeholder(shell.Window.Search) + " / " + string.Join("|", Texts(shell.TabRow?.Element).ToArray()));
            demo.Lang = "en";
            demo.Host.RelabelSurfaces();
            yield return null;

            Func<VisualElement> railButton = () => { var b = shell.Window?.Root.Q("sidebar-toggle"); return b != null && b.resolvedStyle.display != DisplayStyle.None ? b : null; };
            shell.SidebarMode = 1;
            yield return null;
            Check(!shell.Nav.CompactButton && railButton() == null && !shell.Nav.Compact && demo.Store[SuiteShell.SidebarStore] == "1",
                  "shell: sidebar mode 1 is always shown");
            shell.SidebarMode = 2;
            yield return null;
            Check(shell.Nav.Compact && demo.Store[SuiteShell.SidebarStore] == "2", "shell: sidebar mode 2 is icons only");
            shell.SidebarMode = 0;
            yield return null;
            Check(!demo.Store.ContainsKey(SuiteShell.SidebarStore) && shell.Nav.CompactButton && railButton() != null && !shell.Nav.Compact,
                  "shell: sidebar mode 0 removes the key and shows the button");
            Tap(railButton());
            yield return null;
            string folded;
            demo.Store.TryGetValue(SuiteShell.CompactStore, out folded);
            bool compactNow = shell.Nav.Compact;
            Tap(railButton());
            yield return null;
            Check(folded == "1" && compactNow && !shell.Nav.Compact && !demo.Store.ContainsKey(SuiteShell.CompactStore),
                  "shell: folding the rail with the button stores it, unfolding removes it", folded);

            var navBefore = shell.Nav;
            shell.Go("things.beta");
            demo.Extra = true;
            shell.Open();
            yield return null;
            rail = Texts(Rail(shell));
            Check(rail.IndexOf("Things") < rail.IndexOf("More") && rail.IndexOf("More") < rail.IndexOf("Options") && shell.CurrentPage == "things.beta"
                  && ReferenceEquals(shell.Nav, navBefore),
                  "shell: a category added after the build joins its group on the same Nav, on the open page", string.Join("|", rail.ToArray()) + " at " + shell.CurrentPage);
            shell.Window.Search.value = "More";
            yield return null;
            found = LabelIn(Content(shell), "More") != null;
            shell.Window.Search.value = "";
            yield return null;
            Check(found, "shell: search finds a page added after the build");
            shell.Go("more.page");
            demo.Extra = false;
            shell.Open();
            yield return null;
            rail = Texts(Rail(shell));
            Check(!rail.Contains("More") && shell.CurrentPage == "start.home" && ReferenceEquals(shell.Nav, navBefore),
                  "shell: removing the open page's category goes home", string.Join("|", rail.ToArray()) + " at " + shell.CurrentPage);

            shell.Go("things.alpha");
            shell.Go("things.secret");
            rail = Texts(Rail(shell));
            Check(shell.CurrentPage == "things.alpha" && !rail.Contains("Vault") && !ShownText(shell.TabRow?.Element, "Secret"),
                  "shell: a hidden page is no Go target, and a category of hidden pages stays off the rail",
                  string.Join("|", rail.ToArray()) + " at " + shell.CurrentPage);
            demo.SecretShown = true;
            shell.RebuildRail();
            yield return null;
            rail = Texts(Rail(shell));
            Check(rail.Contains("Vault") && ShownText(shell.TabRow?.Element, "Secret") && shell.CurrentPage == "things.alpha" && ReferenceEquals(shell.Nav, navBefore),
                  "shell: a page that shows in the open category joins its rail and tab row on a rebuild",
                  string.Join("|", rail.ToArray()) + " at " + shell.CurrentPage);
            demo.SecretShown = false;
            shell.RebuildRail();
            yield return null;

            // Stored state: a later build (another host name, so the rect comes from the store) opens as this one was left.
            shell.Go("things.alpha");
            shell.Window.Rect = new Rect(100f, 80f, 900f, 600f);
            shell.SidebarMode = 2;
            shell.Dispose();
            Check(demo.Store.ContainsKey("window.probe.menu"), "shell: a shown window stores its rect");
            // The same host again, its stored rect deleted: the window still places from the rect this session kept.
            string keptRect = demo.Store["window.probe.menu"];
            demo.Store.Remove("window.probe.menu");
            var again = new SuiteShell(demo);
            shells.Add(again);
            again.Prebuild();
            bool freshAgain = again.FreshGeometry;
            again.Open();
            yield return Frames(3);
            var ra = again.Window.Rect;
            bool atKept = Near(ra.x, 100f, 1f) && Near(ra.y, 80f, 1f) && Near(ra.width, 900f, 1f) && Near(ra.height, 600f, 1f);
            Check(freshAgain != atKept, "shell: FreshGeometry says where a rebuilt window opens", ra + " fresh " + freshAgain);
            again.Dispose();
            demo.Store["window.probe.menu"] = keptRect;
            var demo2 = new DemoSuite("LayoutProbeSuite2", demo.Store);
            var s = new SuiteShell(demo2);
            shells.Add(s);
            string rectBefore = demo.Store["window.probe.menu"];
            s.Prebuild();
            yield return null;
            yield return null;
            Check(demo.Store["window.probe.menu"] == rectBefore, "shell: a prebuild with stored geometry leaves the stored rect string as it was",
                  rectBefore + " -> " + demo.Store["window.probe.menu"]);
            s.Open();
            yield return Frames(3);
            var r = s.Window.Rect;
            Check(Near(r.x, 100f, 1f) && Near(r.y, 80f, 1f) && Near(r.width, 900f, 1f) && Near(r.height, 600f, 1f) && !s.FreshGeometry
                  && s.CurrentPage == "things.alpha" && s.Nav.Compact,
                  "shell: stored state from an earlier build", r + " fresh " + s.FreshGeometry + " at " + s.CurrentPage + " compact " + s.Nav.Compact);
            s.SidebarMode = 0;
            yield return null;
            Tap(s.Window?.Root.Q("sidebar-toggle"));
            yield return null;
            s.Dispose();
            s = new SuiteShell(demo2);
            shells.Add(s);
            s.Open();
            yield return null;
            Check(s.Nav.Compact && s.Nav.CompactButton && s.Window?.Root.Q("sidebar-toggle") != null, "shell: a rail folded with the button stays folded in a later build");
            s.Dispose();
            demo.Store[SuiteShell.PageStore] = "gone.page";
            s = new SuiteShell(demo2);
            shells.Add(s);
            s.Hidden += countHidden;
            s.Open();
            yield return null;
            Check(s.CurrentPage == "start.home", "shell: a stored page that is gone opens home", s.CurrentPage);

            demo2.Capturing = true;
            Facet.Input.HandleEscape();
            bool kept = s.Visible;
            yield return null;
            demo2.Capturing = false;
            hidden = 0;
            Facet.Input.HandleEscape();
            Check(kept && !s.Visible && hidden == 1, "shell: Escape keeps the window during a capture", kept + " / " + s.Visible + " / " + hidden);

            s.Open();
            yield return null;
            hidden = 0;
            s.Dispose();
            Check(hidden == 0 && s.Surface == null && s.Nav == null && s.CurrentPage == null, "shell: Dispose raises no Hidden", hidden.ToString());

            s = new SuiteShell(demo2);
            shells.Add(s);
            s.Open();
            yield return null;
            demo2.Calls.Clear();
            s.EditLayout();
            Check(demo2.Calls.SequenceEqual(new[] { "leaving", "open-editor" }) && !s.Visible && s.WaitingForLayout,
                  "shell: Edit layout steps aside", string.Join(",", demo2.Calls.ToArray()));
            yield return null;
            Facet.Input.HandleEscape();
            Check(s.WaitingForLayout && !s.Visible, "shell: Escape during Edit layout's wait leaves it waiting");
            demo2.Showing = true;
            for (int i = 0; i < 3; i++) s.Tick();
            bool stillAway = !s.Visible;
            demo2.Showing = false;
            s.Tick();
            Check(stillAway && s.Visible && !s.WaitingForLayout && demo2.Calls.SequenceEqual(new[] { "leaving", "open-editor", "returning" }),
                  "shell: Edit layout comes back when the editor closes", string.Join(",", demo2.Calls.ToArray()));
            yield return null;
            demo2.Calls.Clear();
            demo2.ReturnOk = false;
            s.EditLayout();
            demo2.Showing = true; s.Tick();
            demo2.Showing = false; s.Tick();
            Check(!s.Visible && !s.WaitingForLayout && !demo2.Calls.Contains("returning"), "shell: a session that should not return leaves the window shut");
            demo2.ReturnOk = true;

            demo2.Calls.Clear();
            s.EditLayout();   // s is shut
            demo2.Showing = true; s.Tick();
            demo2.Showing = false; s.Tick();
            var never = new SuiteShell(demo2);
            shells.Add(never);
            never.EditLayout();
            demo2.Showing = true; never.Tick();
            demo2.Showing = false; never.Tick();
            Check(!s.Visible && !s.WaitingForLayout && never.Window == null && !never.Visible && !never.WaitingForLayout && !demo2.Calls.Contains("returning"),
                  "shell: Edit layout on a shut or never-built window returns to nothing",
                  s.Visible + " / " + (never.Window != null) + " / " + string.Join(",", demo2.Calls.ToArray()));

            s.Open();
            yield return null;
            bool other2 = false;
            int opened = 0;
            demo2.Showing = true;   // the adapter's own editor is no part of this trip
            s.EditLayout(() => opened++, () => other2);
            other2 = true; s.Tick();
            other2 = false; s.Tick();
            Check(opened == 1 && s.Visible, "shell: Edit layout with another editor waits on that editor", opened + " / " + s.Visible);
            demo2.Showing = false;
            yield return null;

            int late0 = Count(demo2.Logged, "120 frames");
            s.EditLayout();
            bool early = false;
            for (int i = 0; i < 119; i++) { s.Tick(); early |= s.Visible; }
            s.Tick();
            Check(!early && s.Visible && Count(demo2.Logged, "120 frames") == late0 + 1, "shell: never shown: back on the 120th tick, logged once",
                  early + " / " + s.Visible + " / " + (Count(demo2.Logged, "120 frames") - late0));
            yield return null;

            demo2.ThrowShowing = true;
            s.EditLayout();
            s.Tick();
            bool back1 = s.Visible;
            for (int i = 0; i < 10; i++) s.Tick();
            yield return null;
            s.EditLayout();
            s.Tick();
            Check(back1 && s.Visible && Count(demo2.Logged, "LayoutEditorShowing") == 1, "shell: a throwing adapter in the wait",
                  back1 + " / " + s.Visible + " / " + Count(demo2.Logged, "LayoutEditorShowing"));
            demo2.ThrowShowing = false;
            yield return null;
            demo2.ThrowKeep = true;
            Facet.Input.HandleEscape();
            bool hid1 = !s.Visible;
            s.Open();
            yield return null;
            Facet.Input.HandleEscape();
            Check(hid1 && !s.Visible && Count(demo2.Logged, "KeepEscape") == 1, "shell: a throwing KeepEscape hides the window and logs once",
                  hid1 + " / " + s.Visible + " / " + Count(demo2.Logged, "KeepEscape"));
            demo2.ThrowKeep = false;
            yield return null;

            float t0 = Time.realtimeSinceStartup;
            while (!Facet.WarmUp.Done && Time.realtimeSinceStartup - t0 < 10f) yield return null;
            var pd = new DemoSuite("LayoutProbeSuitePerf") { Id = "probe.perf" };
            var ps = new SuiteShell(pd);
            shells.Add(ps);
            var sw = Stopwatch.StartNew();
            ps.Prebuild();
            ps.Open();
            yield return null;
            yield return new WaitForEndOfFrame();
            double first = sw.Elapsed.TotalMilliseconds;
            Main.Log("[perf] suite shell first open " + first.ToString("0.0") + " ms");
            Check(first <= 50.0, "shell: [perf] suite shell first open within 50 ms", first.ToString("0.0") + " ms");
            ps.Hide();
            yield return null;
            ps.Tick();
            sw = Stopwatch.StartNew();
            for (int i = 0; i < 10000; i++) ps.Tick();
            double us = sw.Elapsed.TotalMilliseconds * 1000.0 / 10000.0;
            Main.Log("[perf] suite shell hidden tick " + us.ToString("0.000") + " us");
            Check(us <= 2.0, "shell: [perf] suite shell hidden tick within 2 us", us.ToString("0.000") + " us");

            foreach (var x in shells) x.Dispose();
            yield return null;
        }

        // Up to a second for the surface's refresher to re-read a row's conditions.
        static IEnumerator Until(Func<bool> ok) { for (int i = 0; i < 60 && !ok(); i++) yield return null; }

        static bool Shown(VisualElement e) => e != null && e.style.display.value != DisplayStyle.None;

        static Button Glyph(SuiteShell s, string glyph) =>
            s.Window.TitleBarRight.Query<Button>().ToList().FirstOrDefault(b => b.text != null && b.text.Contains(glyph));

        IEnumerator Type(TextField f, string text)
        {
            f.Focus();
            yield return null;
            f.value = text;
            f.Blur();
            yield return Frames(2);
        }

        // The page frames, the classic card, the Settings rows and the master switch, on a shell of their own.
        IEnumerator SuitePageChecks()
        {
            var demo = new DemoSuite("LayoutProbeSuitePages") { Id = "probe.pages" };
            var shell = new SuiteShell(demo);
            demo.Shell = shell;
            shell.Open("options.main");
            yield return Frames(3);
            var content = Content(shell);

            var cards = content.Query<VisualElement>().ToList().Where(e => e.name != null && e.name.StartsWith("menu-settings-")).Select(e => e.name).ToList();
            Check(cards.SequenceEqual(new[] { "menu-settings-Look", "menu-settings-Motion", "menu-settings-Behaviour", "menu-settings-Other" }),
                  "pages: Settings cards carry their ids", string.Join(",", cards.ToArray()));

            demo.SidebarControl.Set(2);
            yield return null;
            Check(shell.SidebarMode == 2 && shell.Nav.Compact, "pages: the sidebar row picks a mode", shell.SidebarMode + " / " + shell.Nav.Compact);
            demo.SidebarControl.Set(0);
            yield return null;

            var number = content.Q<TextField>("Scroll speed-number");
            var slider = content.Q("Scroll speed")?.Q<Slider>();
            yield return Type(number, "500");
            bool hi = demo.Speed == 300f && number.value == "300 px";
            yield return Type(number, "10");
            bool lo = demo.Speed == 20f;
            yield return Type(number, "120.4");
            bool mid = demo.Speed == 120f;
            demo.SpeedControl.Set(500f);
            bool unclamped = demo.Speed == 500f;
            demo.SpeedControl.Set(120f);
            Check(hi && lo && mid && unclamped && slider != null && slider.lowValue == 0f && slider.highValue == 1f, "pages: scroll speed is 20–300 px",
                  hi + " " + lo + " " + mid + " " + unclamped + " / " + demo.Speed + " / " + (slider == null ? "no slider" : slider.lowValue + ".." + slider.highValue));

            Tap(content.Q("Smooth Scrolling-input"));
            bool on = demo.Smooth;
            Tap(content.Q("Smooth Scrolling-input"));
            Check(on && !demo.Smooth, "pages: smooth switch writes its bind");

            var link = content.Q("Animation duration");
            bool hiddenLink = !Shown(link);
            demo.PrismShown = true;
            yield return Until(() => Shown(link));
            demo.Calls.Clear();
            var linkButton = link?.Q<Button>();
            if (linkButton != null) Submit(linkButton);
            Check(hiddenLink && Shown(link) && demo.Calls.Contains("prism"), "pages: the Prism link shows only when it can open",
                  hiddenLink + " / " + Shown(link) + " / " + string.Join(",", demo.Calls.ToArray()));

            var classicButton = content.Q("Classic menu")?.Q<Button>();
            if (classicButton != null) Submit(classicButton);
            Check(demo.Calls.Contains("classic"), "pages: the classic-menu link opens", string.Join(",", demo.Calls.ToArray()));

            // Undo from the title bar: a value unlike the current one, from an empty history.
            Facet.History.Clear();
            float before = demo.Speed, after = before == 200f ? 180f : 200f;
            demo.SpeedControl.Set(after);
            yield return null;
            Submit(Glyph(shell, "↶"));
            yield return Until(() => number.value == before.ToString("0") + " px");
            bool undone = demo.Speed == before && number.value == before.ToString("0") + " px";
            Submit(Glyph(shell, "↷"));
            yield return Until(() => number.value == after.ToString("0") + " px");
            Check(undone && demo.Speed == after && number.value == after.ToString("0") + " px", "pages: a scroll-speed change undoes from the title bar",
                  undone + " / " + demo.Speed + " / " + number.value);

            var bar = shell.Window.TitleBarRight;
            var master = bar.Q("menu-master");
            int undoAt = bar.IndexOf(Glyph(shell, "↶"));
            Tap(master?.Q("menu-master-input"));
            bool flipped = demo.MasterOn;
            Check(master != null && bar.IndexOf(master) == 0 && undoAt > 0 && flipped, "pages: the master switch is pinned in the title bar",
                  (master == null ? "none" : bar.IndexOf(master) + " before ↶ at " + undoAt) + " / " + flipped);
            Facet.History.Clear();
            Tap(master?.Q("menu-master-input"));
            bool off = !demo.MasterOn;
            Submit(Glyph(shell, "↶"));
            yield return null;
            Check(off && !demo.MasterOn && !Facet.History.CanUndo, "pages: the master switch is no undo step", off + " / " + demo.MasterOn);
            var bare = new DemoSuite("LayoutProbeSuiteNoMaster") { Id = "probe.nomaster", HasMaster = false };
            var plain = new SuiteShell(bare);
            plain.Prebuild();
            Check(plain.Window != null && plain.Window.TitleBarRight.Q("menu-master") == null, "pages: a shell without a master switch pins none");
            plain.Dispose();

            shell.Window.Rect = new Rect(10f, 10f, 1400f, 800f);
            shell.Go("things.alpha");
            yield return Frames(4);
            content = Content(shell);
            var header = content.Q("Alpha");
            var left = header?.parent;
            var cols = left?.parent;
            var right = cols != null && cols.childCount > 1 ? cols[1] : null;
            float wide = cols == null ? -1f : cols.layout.width;
            bool sides = right != null && LabelIn(left, "cards") != null && LabelIn(right, "preview") != null
                         && right.worldBound.x >= left.worldBound.xMax - 1f;
            float ratio = right == null || right.layout.width <= 0f ? -1f : left.layout.width / right.layout.width;
            Check(wide >= 800f && sides && Near(ratio, 1.25f, 0.05f), "pages: a feature page has its two columns",
                  "width " + wide + " ratio " + ratio + " sides " + sides);
            for (float w = 1100f; w >= 500f && cols != null && !(cols.layout.width > 0f && cols.layout.width < 760f); w -= 100f)
            {
                shell.Window.Rect = new Rect(10f, 10f, w, 800f);
                yield return Frames(3);
            }
            yield return Frames(3);
            bool stacked = right != null && cols.layout.width < 760f && right.worldBound.y >= left.worldBound.yMax - 1f;
            Check(stacked, "pages: a narrow feature page stacks its columns", "width " + (cols == null ? -1f : cols.layout.width));

            shell.Go("things.off");
            yield return Frames(2);
            content = Content(shell);
            var head = content.Q("menu-header");
            demo.Calls.Clear();
            var open = content.Q("menu-classic")?.Q<Button>();
            if (open != null) Submit(open);
            Check(Texts(head).Contains("off") && content.Q("menu-classic") != null && Shown(content.Q("menu-classic-layout")) && demo.Calls.Contains("open"),
                  "pages: a classic page shows its card and layout row", string.Join("|", Texts(head).ToArray()) + " / " + string.Join(",", demo.Calls.ToArray()));
            demo.EditOk = false;
            yield return Until(() => !Shown(content.Q("menu-classic-layout")));
            Check(content.Q("menu-classic-layout") != null && !Shown(content.Q("menu-classic-layout")), "pages: the layout row hides while layout cannot be edited");
            demo.EditOk = true;

            shell.Dispose();
            yield return null;
        }

        static Button HubButton(SuiteHub h, string name) => h.Surface?.Root?.Q<Button>(name);

        // The hub's window: the element under the surface root that holds its buttons.
        static VisualElement HubWindow(SuiteHub h)
        {
            VisualElement e = HubButton(h, "hub-close");
            while (e != null && e.parent != h.Surface.Root) e = e.parent;
            return e;
        }

        // The window's body holds the button row; the footer is the body's next sibling.
        static VisualElement HubBody(SuiteHub h) => HubButton(h, "hub-close")?.parent?.parent;
        static VisualElement HubFooter(SuiteHub h)
        {
            var body = HubBody(h);
            var w = body?.parent;
            int i = w == null ? -1 : w.IndexOf(body);
            return i >= 0 && i + 1 < w.childCount ? w[i + 1] : null;
        }

        // Every shown button at its full width (none squeezed), margins included, inside the body's padding box; the
        // footer inside the window's border.
        static bool HubFits(SuiteHub h, out string why)
        {
            var win = HubWindow(h);
            var body = HubBody(h);
            var foot = HubFooter(h);
            why = "no window";
            if (win == null || body == null || foot == null) return false;
            var p = body.resolvedStyle;
            Rect c = body.worldBound;
            float l = c.x + p.paddingLeft, r = c.xMax - p.paddingRight, t = c.y + p.paddingTop, b = c.yMax - p.paddingBottom;
            bool ok = true;
            var spans = new List<string>();
            foreach (var x in body.Query<Button>().ToList())
            {
                if (x.resolvedStyle.display == DisplayStyle.None) continue;
                var m = x.resolvedStyle;
                Rect e = x.worldBound;
                spans.Add((e.x - m.marginLeft).ToString("0.0") + ".." + (e.xMax + m.marginRight).ToString("0.0") + " w" + e.width.ToString("0.0") + "/" + e.yMax.ToString("0.0"));
                if (e.width < 103.5f || e.x - m.marginLeft < l - 0.5f || e.xMax + m.marginRight > r + 0.5f || e.y < t - 0.5f || e.yMax > b + 0.5f) ok = false;
            }
            float bottom = win.worldBound.yMax - win.resolvedStyle.borderBottomWidth;
            ok &= spans.Count > 0 && foot.worldBound.height > 0f && foot.worldBound.yMax <= bottom + 0.5f;
            why = "box " + l.ToString("0.0") + ".." + r.ToString("0.0") + "/" + b.ToString("0.0") + " buttons " + string.Join(" ", spans.ToArray())
                  + " footer " + foot.worldBound.yMax.ToString("0.0") + " in " + bottom.ToString("0.0");
            return ok;
        }

        // Out of scope: a trip whose Edit layout ends without a return stays armed (no mod builds that today).
        IEnumerator SuiteHubChecks()
        {
            var demo = new DemoSuite("LayoutProbeHub") { Id = "probe.hubmenu" };
            var shell = new SuiteShell(demo);
            var hub = new SuiteHub(shell);
            bool fresh = !demo.Store.ContainsKey("window.probe.hubmenu.hub");
            hub.Toggle();
            yield return Frames(3);
            var names = hub.Surface == null ? new List<string>()
                : hub.Surface.Root.Query<Button>().ToList().Where(b => b.name != null && b.name.StartsWith("hub-")).Select(b => b.name).ToList();
            var footer = HubFooter(hub);
            bool hint = footer != null && Texts(footer).Any(t => t.Contains("Alt+X"));
            Check(hub.Visible && names.SequenceEqual(new[] { "hub-ping", "hub-settings", "hub-close" }) && hint, "hub: Toggle shows the hub with its buttons",
                  hub.Visible + " / " + string.Join(",", names.ToArray()) + " / footer hint " + hint);
            var win = HubWindow(hub);
            var first = HubButton(hub, "hub-ping");
            string fitWhy;
            bool fits = HubFits(hub, out fitWhy);
            // Centred on its first layout, with no rect stored for it.
            Vector2 mid = win == null ? Vector2.zero : win.worldBound.center, panel = hub.Surface?.Root == null ? Vector2.zero : hub.Surface.Root.worldBound.center;
            bool centred = win != null && Mathf.Abs(mid.x - panel.x) <= 1f && Mathf.Abs(mid.y - panel.y) <= 1f;
            Check(fresh && fits && centred, "hub: the window fits its buttons, centred",
                  fresh + " / centre " + mid + " panel " + panel + " / " + fitWhy);

            demo.Calls.Clear();
            Submit(first);
            Check(!hub.Visible && demo.Calls.SequenceEqual(new[] { "ping" }), "hub: an adapter button hides the hub and runs",
                  hub.Visible + " / " + string.Join(",", demo.Calls.ToArray()));
            yield return null;

            // The first Settings click builds the menu: its own hide while building is no close.
            hub.Show();
            yield return null;
            Submit(HubButton(hub, "hub-settings"));
            bool opened = !hub.Visible && shell.Visible;
            yield return null;
            shell.Hide();
            bool back = hub.Visible;
            hub.Hide();
            yield return null;
            shell.Open();
            yield return null;
            shell.Hide();
            yield return null;
            Check(opened && back && !hub.Visible, "hub: Settings opens the menu and its close returns to the hub",
                  opened + " / " + back + " / " + hub.Visible);

            hub.Show();
            yield return null;
            Submit(HubButton(hub, "hub-settings"));
            yield return null;
            shell.EditLayout();
            bool away = !hub.Visible && !shell.Visible;
            demo.Showing = true; shell.Tick();
            demo.Showing = false; shell.Tick();
            bool menuBack = shell.Visible && !hub.Visible;
            yield return null;
            shell.Hide();
            Check(away && menuBack && hub.Visible, "hub: Edit layout from the hub keeps the hub hidden until the menu closes",
                  away + " / " + menuBack + " / " + hub.Visible);
            yield return null;

            Submit(HubButton(hub, "hub-settings"));
            yield return null;
            hub.Toggle();
            bool both = !hub.Visible && !shell.Visible;
            yield return Frames(3);
            Check(both && !hub.Visible && !shell.Visible, "hub: the hotkey over the menu from the hub closes everything", both + " / " + hub.Visible);

            shell.Open();
            yield return null;
            hub.Toggle();
            bool shut = !shell.Visible && !hub.Visible;
            yield return Frames(3);
            Check(shut && !hub.Visible && !shell.Visible, "hub: the hotkey over a menu opened directly closes it and shows no hub", shut + " / " + hub.Visible);

            hub.Show();
            yield return null;
            Facet.Input.HandleEscape();
            bool escaped = !hub.Visible;
            yield return null;
            hub.Show();
            yield return null;
            Submit(HubButton(hub, "hub-close"));
            Check(escaped && !hub.Visible, "hub: Escape and Close hide the hub", escaped + " / " + hub.Visible);
            yield return null;

            hub.Show();
            yield return null;
            demo.Lang = "ko";
            demo.Host.RelabelSurfaces();
            yield return null;
            bool korean = Texts(hub.Surface.Root).Contains("Alt+X 키로 열고 닫아요");
            demo.Lang = "en";
            demo.Host.RelabelSurfaces();
            Check(korean && Texts(hub.Surface.Root).Contains("Alt+X opens or closes this"), "hub: the hint relabels with the host",
                  string.Join("|", Texts(hub.Surface.Root).ToArray()));

            var other = new Facet.Surface(demo.Host, "ProbeHubOther", Facet.Layer.Window);
            new Facet.Window(other.Root, "probehubother", "Other", new Rect(100, 100, 300, 200));
            yield return null;
            bool under = Order(other) > Order(hub.Surface);
            win = HubWindow(hub);
            if (win != null) PressAt(hub.Surface.Root, new Vector2(win.worldBound.x + 20f, win.worldBound.y + 10f));
            Check(under && Order(hub.Surface) > Order(other), "hub: a press on the hub raises it over a later window",
                  Order(hub.Surface) + " / " + Order(other));
            other.Dispose();
            hub.Hide();
            yield return null;

            var demo2 = new DemoSuite("LayoutProbeHub2") { Id = "probe.hubmenu2", HubEdit = true };
            var shell2 = new SuiteShell(demo2);
            demo2.Shell = shell2;
            var hub2 = new SuiteHub(shell2);
            hub2.Show();
            yield return Frames(3);
            string wideWhy;
            bool wideFits = HubFits(hub2, out wideWhy);
            var win2 = HubWindow(hub2);
            Check(wideFits && win2 != null && win2.worldBound.width > 361f, "hub: a hub wider than its minimum fits its buttons and footer",
                  (win2 == null ? "no window" : win2.worldBound.ToString()) + " / " + wideWhy);
            Submit(HubButton(hub2, "hub-edit"));
            bool aside = !hub2.Visible && !shell2.Visible && shell2.WaitingForLayout && demo2.Calls.Contains("open-editor");
            demo2.Showing = true; shell2.Tick(); hub2.Tick();
            bool waits = !hub2.Visible;
            demo2.Showing = false; shell2.Tick(); hub2.Tick();
            Check(aside && waits && hub2.Visible && !shell2.Visible && !shell2.WaitingForLayout,
                  "hub: a button's editor brings the hub back when it closes, the menu stays shut",
                  aside + " / " + waits + " / " + hub2.Visible + " / " + shell2.Visible);

            // The hotkey while an editor is up ends the trip: nothing shows, then or when the editor closes, and the
            // next press shows the hub. Once from a hub button's editor, once from the menu's Edit layout.
            Submit(HubButton(hub2, "hub-edit"));
            demo2.Showing = true; shell2.Tick(); hub2.Tick();
            hub2.Toggle();
            bool quiet = !hub2.Visible && !shell2.Visible && !shell2.WaitingForLayout;
            demo2.Showing = false; shell2.Tick(); hub2.Tick();
            bool stays = !hub2.Visible && !shell2.Visible;
            hub2.Toggle();
            bool next = hub2.Visible;
            yield return null;
            Submit(HubButton(hub2, "hub-settings"));
            yield return null;
            shell2.EditLayout();
            demo2.Showing = true; shell2.Tick(); hub2.Tick();
            hub2.Toggle();
            bool quiet2 = !hub2.Visible && !shell2.Visible && !shell2.WaitingForLayout;
            demo2.Showing = false; shell2.Tick(); hub2.Tick();
            bool stays2 = !hub2.Visible && !shell2.Visible;
            hub2.Toggle();
            bool next2 = hub2.Visible;
            hub2.Hide();
            Check(quiet && stays && next && quiet2 && stays2 && next2, "hub: the hotkey while an editor is up ends the trip and shows nothing",
                  quiet + " " + stays + " " + next + " / " + quiet2 + " " + stays2 + " " + next2);
            yield return null;
            hub2.Dispose();
            shell2.Dispose();
            yield return null;

            // A long label with an icon, as a host's translation can be: it wraps inside its button, and the window
            // grows to hold it.
            var demo3 = new DemoSuite("LayoutProbeHub3") { Id = "probe.hubmenu3", PingLabel = "Edit positions on screen", HubIcon = true };
            var shell3 = new SuiteShell(demo3);
            var hub3 = new SuiteHub(shell3);
            hub3.Show();
            yield return Frames(3);
            var longB = HubButton(hub3, "hub-ping");
            var label = longB?.Query<Label>().ToList().FirstOrDefault(l => l.text == demo3.PingLabel);
            var win3 = HubWindow(hub3);
            bool inside = label != null && win3 != null && longB.worldBound.Contains(label.worldBound.min - new Vector2(-0.5f, -0.5f))
                          && longB.worldBound.Contains(label.worldBound.max - new Vector2(0.5f, 0.5f))
                          && longB.worldBound.yMax <= win3.worldBound.yMax + 0.5f;
            // The whole text shows: a text wider than its box must take more than one line, and all its lines fit.
            Vector2 one = Vector2.zero, line = Vector2.zero, need = Vector2.zero;
            if (label != null)
            {
                var U = VisualElement.MeasureMode.Undefined;
                one = label.MeasureTextSize(label.text, 0f, U, 0f, U);
                line = label.MeasureTextSize("Ag", 0f, U, 0f, U);
                need = label.MeasureTextSize(label.text, label.layout.width, VisualElement.MeasureMode.Exactly, 0f, U);
            }
            bool whole = label != null && !label.isElided && need.y <= label.layout.height + 0.5f
                         && (one.x <= label.layout.width + 0.5f || label.layout.height >= 1.5f * line.y);
            Main.Log("INFO hub long label " + (label == null ? "none" : label.worldBound + " one line " + one + " wrapped " + need + " in " + longB.worldBound));
            Check(inside && whole, "hub: a long label wraps inside its button and the window holds it",
                  label == null ? "no label" : label.worldBound + " in " + longB.worldBound + " / win " + (win3 == null ? "no window" : win3.worldBound.ToString()) + " / needs " + need);
            hub3.Dispose();
            shell3.Dispose();
            yield return null;

            hub.Show();
            yield return null;
            Submit(HubButton(hub, "hub-settings"));
            yield return null;
            var hs = hub.Surface;
            hub.Dispose();
            shell.Hide();
            hub.Show();
            yield return Frames(2);
            bool dropped = hs != null && hs.Root == null && !hs.Visible;
            bool none = !FindObjectsByType<UIDocument>(FindObjectsSortMode.None).Any(d => d != null && d.gameObject.name == "probe.hubmenu.hub"
                         && d.rootVisualElement != null && d.rootVisualElement.resolvedStyle.display != DisplayStyle.None);
            Check(dropped && none && hub.Surface == null && !hub.Visible && !shell.Visible, "hub: Dispose mid-trip reopens nothing",
                  dropped + " / shown hub panel " + !none + " / " + (hub.Surface != null) + " / " + shell.Visible);
            shell.Dispose();
            yield return null;

            float t0 = Time.realtimeSinceStartup;
            while (!Facet.WarmUp.Done && Time.realtimeSinceStartup - t0 < 10f) yield return null;
            var pd = new DemoSuite("LayoutProbeHubPerf") { Id = "probe.hubperf" };
            var ps = new SuiteShell(pd);
            var ph = new SuiteHub(ps);
            var sw = Stopwatch.StartNew();
            ph.Show();
            yield return null;
            yield return new WaitForEndOfFrame();
            double ms = sw.Elapsed.TotalMilliseconds;
            Main.Log("[perf] suite hub first show " + ms.ToString("0.0") + " ms");
            Check(ph.Visible && ms <= 50.0, "hub: [perf] suite hub first show within 50 ms", ms.ToString("0.0") + " ms");
            ph.Dispose();
            ps.Dispose();
            yield return null;
        }
    }
}
