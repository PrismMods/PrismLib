// Scripted in-game test of the LayoutEditor: synthetic UI Toolkit events against the real panel,
// assertions on the probe boxes' state. Runs when an "autotest" file sits in the probe's folder,
// logs PASS/FAIL lines to probe.log, then quits the game.
using System;
using System.Collections;
using System.Linq;
using PrismLib.UI.Toolkit;
using UnityEngine;
using UnityEngine.UIElements;

namespace LayoutProbe
{
    sealed class AutoTest : MonoBehaviour
    {
        internal static LayoutEditor Editor;
        internal static Box A, B, C;
        internal static Action<bool> SetEmpty;
        internal static bool CThrows, CNull;
        int _pass, _fail;
        bool _closed;

        void Check(bool ok, string what, string detail = "")
        {
            if (ok) _pass++; else _fail++;
            Main.Log((ok ? "PASS " : "FAIL ") + what + (ok || detail == "" ? "" : "  [" + detail + "]"));
        }

        static bool Near(float a, float b, float tol = 1.5f) => Math.Abs(a - b) <= tol;

        VisualElement Root => FindObjectsByType<UIDocument>(FindObjectsSortMode.None)
            .FirstOrDefault(d => d.gameObject.name == "PrismLayoutEditor")?.rootVisualElement;

        float K()
        {
            var layer = Root?.Q("layout-layer");
            return layer == null ? 0f : layer.layout.width / Screen.width;
        }

        void Pointer(EventType type, Vector2 screen)
        {
            float k = K();
            var ev = new Event { type = type, button = 0, clickCount = 1,
                mousePosition = new Vector2(screen.x * k, (Screen.height - screen.y) * k) };
            EventBase e = type == EventType.MouseDown ? PointerDownEvent.GetPooled(ev)
                        : type == EventType.MouseUp ? PointerUpEvent.GetPooled(ev)
                        : (EventBase)PointerMoveEvent.GetPooled(ev);
            using (e) Root.panel.visualTree.SendEvent(e);
        }

        IEnumerator Drag(Vector2 from, Vector2 to, int steps = 6)
        {
            Pointer(EventType.MouseDown, from); yield return null;
            for (int i = 1; i <= steps; i++) { Pointer(EventType.MouseDrag, Vector2.Lerp(from, to, i / (float)steps)); yield return null; }
            Pointer(EventType.MouseUp, to); yield return null; yield return null;
        }

        // Down and up in the same frame: the real mouse sends its own move events, and one landing
        // between them would turn a still click into a drag to wherever the real pointer rests.
        IEnumerator Click(Vector2 at)
        {
            Pointer(EventType.MouseDown, at);
            Pointer(EventType.MouseUp, at); yield return null; yield return null;
        }

        void Submit(Button b)
        {
            using (var e = NavigationSubmitEvent.GetPooled()) { e.target = b; b.SendEvent(e); }
        }

        Button ButtonNamed(string name) => Root.Q<Button>(name);

        static Vector2 Ctr(Box b) => new Vector2(b.Cx, b.Cy);


        IEnumerator Start()
        {
            for (int i = 0; i < 30; i++) yield return null;
            float W = Screen.width, H = Screen.height;
            Main.Log("AUTOTEST start screen " + W + "x" + H);
            yield return Perf.Run((ok, what, detail) => Check(ok, what, detail));

            // Open
            Editor.Closed += () => _closed = true;
            Editor.Visible = true;
            for (int i = 0; i < 5; i++) yield return null;
            Check(Editor.Visible && Root != null && K() > 0f, "editor opens with a laid-out panel", "k=" + K());

            // Select without moving
            A.Cx = W * 0.3f; A.Cy = H * 0.62f; B.Cx = W * 0.7f; B.Cy = H * 0.62f;
            yield return null;
            var a0 = Ctr(A);
            yield return Click(a0 + new Vector2(10, 7));
            var fx = Root.Q<TextField>("layout-x");
            Check(fx != null && Near(float.Parse(fx.value, System.Globalization.CultureInfo.InvariantCulture), A.Cx), "first click selects A (inspector shows its X)", fx == null ? "no field" : fx.value);
            Check(Ctr(A) == a0, "the selecting click does not move it");

            // Drag the selected box
            yield return Drag(a0, a0 + new Vector2(90, -55));
            Check(Near(A.Cx, a0.x + 90) && Near(A.Cy, a0.y - 55), "dragging the selected box moves it", A.ToString());

            // Snap to the vertical centre line
            var before = Ctr(A);
            yield return Drag(before, new Vector2(W / 2f + 4f, before.y));
            Check(A.Cx == W / 2f, "the centre snaps to the screen centre line", "Cx=" + A.Cx);

            // Undo / redo of that drag
            History.Undo(); yield return null;
            Check(Near(A.Cx, before.x) && Near(A.Cy, before.y), "undo restores the position", A.ToString());
            History.Redo(); yield return null;
            Check(A.Cx == W / 2f, "redo re-applies it", "Cx=" + A.Cx);

            // Move clear of the centre line for the grip tests
            yield return Drag(Ctr(A), new Vector2(W * 0.3f, H * 0.6f));
            float w = A.W * A.Scale, h = A.H * A.Scale;
            var c = Ctr(A);
            var corner = c + new Vector2(w / 2f, h / 2f);
            yield return Drag(corner, c + (corner - c) * 1.5f);
            Check(Near(A.Scale, 1.5f, 0.03f), "a corner grip scales evenly", "scale=" + A.Scale);
            History.Undo(); yield return null;
            Check(Near(A.Scale, 1f, 0.001f), "undo restores the scale", "scale=" + A.Scale);

            // Rotation knob: drag it to the right of the centre → -90° → 270
            c = Ctr(A); h = A.H * A.Scale;
            var knob = c + new Vector2(0, h / 2f + 26f * H / 1080f);
            yield return Drag(knob, c + new Vector2(150f, 2f));
            Check(A.Rot == 270f, "the knob rotates and snaps to a quarter turn", "rot=" + A.Rot);
            History.Undo(); yield return null;
            Check(A.Rot == 0f, "undo restores the rotation", "rot=" + A.Rot);


            // Closest anchor follows the third the box is in
            A.Closest = true;
            yield return Drag(Ctr(A), new Vector2(W * 0.85f, H * 0.18f));
            Check(A.Anchor == 8, "Closest re-anchors to the bottom-right", "anchor=" + A.Anchor);
            yield return Drag(Ctr(A), new Vector2(W * 0.15f, H * 0.82f));
            Check(A.Anchor == 0, "and to the top-left", "anchor=" + A.Anchor);

            // Crop: right edge inward by a quarter, the left edge stays put
            A.Cx = W * 0.35f; A.Cy = H * 0.5f; yield return null; yield return null;
            var crop = ButtonNamed("layout-crop");
            Check(crop != null, "the crop toggle exists for a target that supports crop");
            if (crop != null) { Submit(crop); yield return null; yield return null; }
            float leftEdge = A.Cx - A.W * A.Scale / 2f;
            var rightMid = new Vector2(A.Cx + A.W * A.Scale / 2f, A.Cy);
            yield return Drag(rightMid, rightMid - new Vector2(A.W * A.Scale * 0.25f, 0));
            Check(Near(A.Crop[2], 0.25f, 0.01f), "dragging the right edge crops a quarter", "crop=" + string.Join("/", A.Crop));
            Check(Near(A.Cx - A.Get()[2] / 2f, leftEdge), "the left edge stays where it was", A.ToString());
            yield return Drag(new Vector2(A.Cx + A.Get()[2] / 2f, A.Cy), new Vector2(A.Cx - A.W, A.Cy));
            Check(A.Crop[0] + A.Crop[2] <= 0.9501f, "an edge stops with 5% left visible", "crop=" + string.Join("/", A.Crop));
            History.Undo(); History.Undo(); yield return null;
            Check(A.Crop[2] == 0f, "undo restores the crop", "crop=" + string.Join("/", A.Crop));
            if (crop != null) { Submit(crop); yield return null; }

            // Typed X
            fx = Root.Q<TextField>("layout-x");
            fx.SetValueWithoutNotify("500");
            using (var ke = KeyDownEvent.GetPooled('\n', KeyCode.Return, EventModifiers.None)) { ke.target = fx; fx.SendEvent(ke); }
            yield return null;
            Check(Near(A.Cx, 500f), "typing X and pressing Return moves the box", "Cx=" + A.Cx);

            // Overlap: select B, drop it on A, then a still click on the overlap selects A
            Main.Log("  overlap: start A " + A + " | B " + B);
            yield return Click(Ctr(B));
            Main.Log("  overlap: after click B, inspector X=" + Root.Q<TextField>("layout-x")?.value);
            yield return Drag(Ctr(B), Ctr(A));
            Main.Log("  overlap: after dragging B onto A: A " + A + " | B " + B);
            var spot = Ctr(A);
            yield return Click(spot);
            Main.Log("  overlap: after still click: A " + A + " | B " + B);
            var aBefore = Ctr(A); var bBefore = Ctr(B);
            yield return Drag(spot, spot + new Vector2(0, -120));
            Check(Ctr(A) != aBefore && Ctr(B) == bBefore, "a still click on overlapping boxes cycles down to A", "A " + A + " | B " + B);

            // B has no rotation, anchor or crop: no knob hit, no crop toggle
            yield return Click(Ctr(B) + new Vector2(30, 0));
            yield return null;
            Check(ButtonNamed("layout-crop") == null, "B (required delegates only) has no crop toggle");
            var bPos = Ctr(B);
            var kb = Ctr(B) + new Vector2(0, B.H / 2f + 26f * H / 1080f);
            yield return Drag(kb, Ctr(B) + new Vector2(150f, 0));
            Check(Ctr(B) == bPos, "B has no knob: dragging where one would be leaves B alone", B.ToString());

            // A target whose mod went away: its delegates throw; the editor keeps working
            CThrows = true;
            for (int i = 0; i < 5; i++) yield return null;
            var aNow = Ctr(A);
            yield return Click(aNow);
            yield return Drag(aNow, aNow + new Vector2(40, 0));
            Check(Near(A.Cx, aNow.x + 40), "a throwing target is skipped and the editor still works", A.ToString());

            // Empty list
            SetEmpty(true); Editor.Reload(); yield return null; yield return null;
            var empty = Root.Query<Label>().ToList().FirstOrDefault(l => l.text == "Nothing to arrange yet");
            Check(empty != null && empty.resolvedStyle.display == DisplayStyle.Flex, "an empty target list shows the empty state");
            SetEmpty(false); Editor.Reload(); yield return null;

            // ── Review fixes ────────────────────────────────────────────────
            CThrows = false; CNull = false;
            Editor.Reload(); for (int i = 0; i < 3; i++) yield return null;
            A.Cx = W * 0.3f; A.Cy = H * 0.65f; A.Rot = 0f; A.Scale = 1f; A.Crop = new float[4];
            B.Cx = W * 0.8f; B.Cy = H * 0.8f; C.Cx = W * 0.6f; C.Cy = H * 0.25f;
            yield return null; yield return null;
            yield return Click(Ctr(A));

            // The inspector's anchor picker is a real 3x3: three rows, three columns
            {
                var cells = new Rect[9]; bool all = true;
                for (int i = 0; i < 9; i++) { var c9 = Root.Q("layout-anchor-" + i); if (c9 == null) { all = false; break; } cells[i] = c9.worldBound; }
                bool grid = all;
                for (int row = 0; grid && row < 3; row++)
                    for (int col = 0; col < 3; col++)
                    {
                        var cell = cells[row * 3 + col];
                        if (Math.Abs(cell.center.y - cells[row * 3].center.y) > 1f) grid = false;           // same row
                        if (Math.Abs(cell.center.x - cells[col].center.x) > 1f) grid = false;               // same column
                        if (col > 0 && cell.center.x <= cells[row * 3 + col - 1].center.x) grid = false;    // left to right
                        if (row > 0 && cell.center.y <= cells[(row - 1) * 3 + col].center.y) grid = false;  // top to bottom
                    }
                Check(grid, "the anchor picker lays out as a 3x3 grid",
                      all ? string.Join(" ", Array.ConvertAll(cells, r9 => "(" + r9.center.x.ToString("0") + "," + r9.center.y.ToString("0") + ")")) : "cells missing");
            }

            // Grips, knob and anchor are on screen, also after a Reload
            for (int round = 0; round < 2; round++)
            {
                for (int i = 0; i < 3; i++) yield return null;
                var g0 = Root.Q("layout-grip-0"); var kn = Root.Q("layout-knob"); var ad = Root.Q("layout-anchor-dot");
                bool on = g0 != null && kn != null && ad != null && g0.panel != null && kn.panel != null && ad.panel != null
                          && g0.resolvedStyle.display == DisplayStyle.Flex && kn.resolvedStyle.display == DisplayStyle.Flex;
                Check(on, round == 0 ? "grips, knob and anchor dot are on screen for the selected box" : "and still after a Reload",
                      "grip " + (g0?.panel != null) + " knob " + (kn?.panel != null) + " dot " + (ad?.panel != null));
                Editor.Reload();
            }

            // A target whose GetBox returns null is not clickable
            yield return Click(Ctr(C));
            CNull = true;
            for (int i = 0; i < 3; i++) yield return null;
            yield return Click(new Vector2(W * 0.95f, H * 0.05f));   // empty: deselect
            yield return Click(Ctr(C));
            Check(Root.Q<TextField>("layout-x") == null, "a target whose box went null cannot be selected", "inspector X " + Root.Q<TextField>("layout-x")?.value);
            CNull = false;

            // Hit-testing and snapping follow rotation
            A.Rot = 90f; yield return null; yield return null;
            yield return Click(new Vector2(W * 0.95f, H * 0.05f));
            yield return Click(Ctr(A) + new Vector2(0, 140f));        // inside the turned box only
            var fx2 = Root.Q<TextField>("layout-x");
            Check(fx2 != null && Near(float.Parse(fx2.value, System.Globalization.CultureInfo.InvariantCulture), A.Cx), "a click inside the rotated box selects it");
            yield return Click(new Vector2(W * 0.95f, H * 0.05f));
            yield return Click(Ctr(A) + new Vector2(140f, 0));        // inside the unrotated box only
            Check(Root.Q<TextField>("layout-x") == null, "a click beside the rotated box misses it");
            yield return Click(Ctr(A));
            yield return Drag(Ctr(A), new Vector2(94f, A.Cy));       // turned: left edge 4 px from the screen edge
            Check(Near(A.Cx, 90f, 0.01f), "the rotated box's own edge snaps to the screen edge", "Cx=" + A.Cx);
            A.Rot = 0f; A.Cx = W * 0.3f; yield return null; yield return null;

            // Leaving a field without typing changes nothing; Escape cancels what was typed
            yield return Click(Ctr(A));
            A.Scale = 1.2345f;
            for (int i = 0; i < 3; i++) yield return null;
            var fs = Root.Q<TextField>("layout-size");
            fs.Focus(); yield return null; fs.Blur(); yield return null; yield return null;
            Check(A.Scale == 1.2345f, "focusing and leaving Size without typing keeps the exact value", "scale=" + A.Scale);
            fs.Focus(); yield return null;
            fs.SetValueWithoutNotify("3");
            using (var ke = KeyDownEvent.GetPooled('\0', KeyCode.Escape, EventModifiers.None)) { ke.target = fs; fs.SendEvent(ke); }
            fs.Blur(); yield return null; yield return null;
            Check(A.Scale == 1.2345f, "Escape in a field cancels the typed value", "scale=" + A.Scale);
            A.Scale = 1f;

            // Dragging a crop edge past the other stops at 5% and leaves the other edge alone
            A.Crop = new[] { 0.1f, 0f, 0f, 0f };
            yield return null; yield return null;
            var crop2 = ButtonNamed("layout-crop");
            if (crop2 != null) { Submit(crop2); yield return null; yield return null; }
            var rm = new Vector2(A.Cx + A.Get()[2] / 2f, A.Cy);
            yield return Drag(rm, new Vector2(A.Cx - A.W * 2f, A.Cy));
            Check(Near(A.Crop[0], 0.1f, 0.001f) && Near(A.Crop[2], 0.85f, 0.01f), "the dragged edge stops; the opposite one stays", "crop=" + string.Join("/", A.Crop));
            if (crop2 != null) { Submit(crop2); yield return null; }
            A.Crop = new float[4];

            // Ctrl+Z reads its keys exempt from the input gate the editor itself raises
            yield return Drag(Ctr(A), Ctr(A) + new Vector2(60f, 0));
            float movedX = A.Cx;
            bool sawBlocking = false, fired = false;
            LayoutEditor.KeyHeld = k => { sawBlocking |= InputGate.Blocking; return k == KeyCode.LeftControl; };
            LayoutEditor.KeyPressed = k => { sawBlocking |= InputGate.Blocking; if (k == KeyCode.Z && !fired) { fired = true; return true; } return false; };
            for (int i = 0; i < 4; i++) yield return null;
            LayoutEditor.KeyHeld = Input.GetKey; LayoutEditor.KeyPressed = Input.GetKeyDown;
            Check(!sawBlocking, "the editor reads its undo keys exempt from InputGate");
            Check(Near(A.Cx, movedX - 60f), "Ctrl+Z undoes the last gesture", "Cx=" + A.Cx + " was " + movedX);

            // Done
            var done = ButtonNamed("layout-done");
            if (done != null) Submit(done);
            yield return null; yield return null;
            Check(!Editor.Visible && _closed, "Done closes the editor and raises Closed");

            // ── PrismLib.UI on Facet ────────────────────────────────────────
            Editor.Visible = true;
            for (int i = 0; i < 5; i++) yield return null;
            Check(Ui.AnyWindowOpen && Facet.Input.AnyWindowOpen, "Facet counts the open editor as a window, as Ui does",
                  "Ui " + Ui.AnyWindowOpen + ", Facet " + Facet.Input.AnyWindowOpen);
            Check(Root != null && Facet.Host.For(Root).Name == "PrismLib", "the editor is a Facet surface of the PrismLib host",
                  Root == null ? "no root" : Facet.Host.For(Root).Name);

            // An Escape answered by Facet alone (a Facet-native host's poll) still closes the editor properly
            _closed = false;
            Facet.Input.HandleEscape(); yield return null;
            Check(!Editor.Visible && _closed, "Facet's own Escape closes the editor and raises Closed",
                  "visible " + Editor.Visible + ", closed " + _closed);
            Editor.Visible = true;
            for (int i = 0; i < 3; i++) yield return null;
            Check(Editor.Visible && Ui.AnyWindowOpen, "and the editor opens again after it", "any window open " + Ui.AnyWindowOpen);

            // Escape with a Facet-native window above a Prism one: exactly the topmost closes per press
            var probeHost = Facet.Host.Create("LayoutProbe");
            var facetWin = new Facet.Surface(probeHost, "ProbeFacetWindow", Facet.Layer.Modal);
            new Facet.Window(facetWin.Root, "probe-facet", "Facet window", new Rect(200f, 150f, 420f, 260f));
            for (int i = 0; i < 3; i++) yield return null;
            Ui.HandleEscape(); yield return null;
            Check(!facetWin.Visible && Editor.Visible, "one Escape closes only the Facet window above the editor",
                  "facet " + facetWin.Visible + ", editor " + Editor.Visible);
            Ui.HandleEscape(); yield return null;
            Check(!Editor.Visible, "a second Escape closes the editor under it", "editor " + Editor.Visible);
            facetWin.Dispose();

            // Tokens pushed after other windows exist reach the next window built (Topaz's PushTokens)
            var wasAccent = Tokens.Accent;
            var red = new Color(1f, 0f, 0f, 1f);
            Tokens.Accent = red;
            var sp = new SettingsPanel("Probe Accent", () => new SettingRow[0]);
            sp.Visible = true;
            for (int i = 0; i < 3; i++) yield return null;
            var spDoc = FindObjectsByType<UIDocument>(FindObjectsSortMode.None).FirstOrDefault(d => d.gameObject.name == "ProbeAccentPanel");
            var bar = spDoc?.rootVisualElement.Children().FirstOrDefault()?.Children().FirstOrDefault();
            var undoBtn = bar?.Query<Button>().ToList().FirstOrDefault(b => b.text == "↶");
            Check(bar != null && Facet.Theme.For(bar)[Facet.Role.Accent] == red, "the title bar of a window built after Tokens.Accent = red wears red",
                  bar == null ? "no title bar" : Facet.Theme.For(bar)[Facet.Role.Accent].ToString());
            if (undoBtn != null) Ui.Highlight(undoBtn, true);
            Check(undoBtn != null && undoBtn.style.backgroundColor.value == red, "a highlighted title-bar button draws in that red",
                  undoBtn == null ? "no undo button" : undoBtn.style.backgroundColor.value.ToString());
            sp.Dispose();
            Tokens.Accent = wasAccent;

            yield return SchemaSettings();
            yield return NavMenu();
            yield return TopazMenu();
            yield return GradientSync();

            Main.Log("AUTOTEST DONE pass=" + _pass + " fail=" + _fail);
            yield return null;
            Application.Quit();
        }

        // Prism · Settings in schema mode: rows described as data, drawn as Facet forms, searched by Facet.
        IEnumerator SchemaSettings()
        {
            string live = "before";
            System.Func<string, SettingControl, SettingRow> row = (label, kind) => new SettingRow
                { Owner = "LayoutProbe", Page = "Probe", Group = "Kinds", Label = label, Kind = kind, Set = _ => { } };
            var rows = new[]
            {
                row("Probe bool", SettingControl.Bool), row("Probe int", SettingControl.Int), row("Probe float", SettingControl.Float),
                row("Probe choice", SettingControl.Choice), row("Probe text", SettingControl.Text), row("Probe action", SettingControl.Action),
                row("Probe colour", SettingControl.Colour), row("Probe key", SettingControl.Key), row("Probe info", SettingControl.Info),
                row("Probe broken", SettingControl.Bool), row("Probe after", SettingControl.Bool),
                new SettingRow { Owner = "PrismLib", Page = "Display", Group = "Display", Label = "Show FPS", Kind = SettingControl.Bool,
                                 Tooltip = "Frame rate in the bottom-right corner, for every Prism mod", Get = () => false, Set = _ => { } },
                row("Probe colour string", SettingControl.Colour), row("Probe fixed key", SettingControl.Key),
                row("Effective BPM", SettingControl.Bool), row("Arrow -> target", SettingControl.Bool),
            };
            rows[0].Get = () => true;
            rows[1].Get = () => 3; rows[1].Max = 10f;
            rows[2].Get = () => 0.5f; rows[2].Max = 1f;
            rows[3].Get = () => 1; rows[3].Options = new[] { "One", "Two" };
            rows[4].Get = () => live; rows[4].Set = v => live = (string)v;
            rows[5].ActionLabel = "Go";
            rows[6].Get = () => "#ff8000";
            rows[7].Get = () => "F9"; rows[7].Capture = done => done("F9");
            rows[8].Get = () => "some information";
            rows[9].Get = () => { throw new ObjectDisposedException("Probe broken"); };
            rows[10].Get = () => false;
            object colourSet = null;
            rows[12].Get = () => "#336699"; rows[12].Set = v => colourSet = (Color)v;   // a mod that stores hex and casts on the way back
            rows[13].Get = () => "F10";   // no Capture
            rows[14].Get = () => false; rows[15].Get = () => false;

            // The installed mods point Words.Language at the game's language; English first, Korean below.
            var wasLanguage = Words.Language;
            Words.Language = () => "en";
            var sp = new SettingsPanel("Probe Schema", () => rows);
            sp.Visible = true;
            for (int i = 0; i < 3; i++) yield return null;
            var root = FindObjectsByType<UIDocument>(FindObjectsSortMode.None).FirstOrDefault(d => d.gameObject.name == "ProbeSchemaPanel")?.rootVisualElement;
            var missing = rows.Take(9).Where(r => root?.Q(r.Label) == null).Select(r => r.Label).ToArray();
            Check(root != null && missing.Length == 0, "every kind of settings row is a control found by Id = its label", "missing " + string.Join(", ", missing));
            var broken = root?.Q("Probe broken");
            Check(broken != null && !broken.enabledSelf && broken.tooltip == "unavailable, see the log",
                  "a row whose getter throws is a dimmed control pointing at the log",
                  broken == null ? "no control" : "enabled " + broken.enabledSelf + ", tooltip '" + broken.tooltip + "'");
            Check(root?.Q("Probe after") != null, "and the row after it still builds");

            // Changed behind the panel: the open page shows it on its own, no Reload
            live = "after";
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 0.15f) yield return null;
            var tf = root?.Q("Probe text")?.Q<TextField>();
            Check(tf != null && tf.value == "after", "a value changed behind the panel shows within 150 ms without Reload",
                  tf == null ? "no field" : "'" + tf.value + "'");

            // A colour read as "#rrggbb" still reaches its setter as a Color, and offers no alpha
            var cs = root?.Q("Probe colour string");
            var hex = cs?.Q<TextField>();
            if (hex != null)
            {
                hex.SetValueWithoutNotify("#00ff00");
                using (var ke = KeyDownEvent.GetPooled('\n', KeyCode.Return, EventModifiers.None)) { ke.target = hex; hex.SendEvent(ke); }
                yield return null;
            }
            Check(colourSet is Color && (Color)colourSet == Color.green, "editing a colour whose getter gives \"#rrggbb\" hands the setter a Color",
                  hex == null ? "no hex field" : colourSet == null ? "setter not reached or threw" : colourSet.ToString());
            Check(cs != null && cs.Query<Label>().ToList().Any(l => l.text == "R") && !cs.Query<Label>().ToList().Any(l => l.text == "A"),
                  "and the picker offers no alpha channel");

            // A key with no Capture: the old explanation in a read-only row, not a button that ignores clicks
            var fixedKey = root?.Q("Probe fixed key — not rebindable here");
            Check(fixedKey != null && fixedKey.Q<Button>() == null, "a key nothing can capture says it is not rebindable here",
                  fixedKey == null ? "no row" : "has a button");

            // Search: the box sits beside the tabs and the scrolling rows
            var sv = root?.Q<ScrollView>();
            var search = root?.Query<TextField>().ToList().FirstOrDefault(f => sv != null && f.parent == sv.parent);
            Func<string, bool> shows = text => root.Query<Label>().ToList().Any(l => l.text == text);
            Func<string> labels = () => string.Join(" | ", root.Query<Label>().ToList().Select(l => l.text).Where(s => !string.IsNullOrEmpty(s)).Take(12));
            if (search != null) { search.value = "fps"; yield return null; }
            Check(search != null && shows("Show FPS") && !shows("Probe bool"), "searching 'fps' lists Show FPS and nothing else",
                  search == null ? "no search box" : labels());
            Check(search != null && root.Query<Label>().ToList().Any(l => l.text.StartsWith("PrismLib · ")), "under an owner · page · group header");
            // What the old matcher also found: letters in order, and punctuation as a plain substring
            if (search != null) { search.value = "efctbpm"; yield return null; }
            Check(search != null && shows("Effective BPM"), "an abbreviation ('efctbpm') finds Effective BPM", search == null ? "no search box" : labels());
            if (search != null) { search.value = "->"; yield return null; }
            Check(search != null && shows("Arrow -> target"), "a query of punctuation alone ('->') finds the label holding it", search == null ? "no search box" : labels());
            if (search != null) { search.value = "fps"; yield return null; }
            var fps = root?.Q("Show FPS");
            Check(fps != null && Facet.Host.For(fps).Name == "PrismLib/PrismLib", "a row is built under its owner's host",
                  fps == null ? "no control" : Facet.Host.For(fps).Name);

            Words.Language = () => "ko";
            sp.Reload();
            if (search != null) { search.value = "ㅍ"; yield return null; }
            Check(search != null && shows("FPS 표시"), "in Korean, searching 'ㅍ' finds FPS 표시",
                  search == null ? "no search box" : labels());
            Words.Language = wasLanguage;
            sp.Dispose();
        }

        static Label LabelIn(VisualElement e, string text) => e?.Query<Label>().ToList().FirstOrDefault(l => l.text == text);

        static void Tap(VisualElement e)
        {
            if (e == null) return;
            using (var ev = ClickEvent.GetPooled()) { ev.target = e; e.SendEvent(ev); }
        }

        static VisualElement PanelRoot(string name) => FindObjectsByType<UIDocument>(FindObjectsSortMode.None)
            .FirstOrDefault(d => d.gameObject.name == name)?.rootVisualElement;

        // A nav-mode SettingsPanel shaped like Topaz's menu: Widgets and Nav standing on Facet.
        IEnumerator NavMenu()
        {
            var builds = new System.Collections.Generic.Dictionary<string, int>();
            Action<string> built = p => { int n; builds.TryGetValue(p, out n); builds[p] = n + 1; };
            Func<string, int> count = p => { int n; builds.TryGetValue(p, out n); return n; };
            int rev = 0;
            bool facetActive = false, statOn = false;
            var pages = new System.Collections.Generic.List<NavItem>
            {
                NavItem.Heading("Probe Overlay",
                    new NavItem("Probe readouts", body =>
                    {
                        built("readouts");
                        facetActive = Nav.Active != null && Facet.Nav.Active != null;
                        var on = Widgets.ToggleGroup(body, "Probe overlay on", true, _ => { }, "Master switch");
                        var score = Widgets.Section(on, "Probe score");
                        Widgets.ToggleSubPage(score, "Probe progress", statOn, v => statOn = v, sub =>
                        {
                            built("stat");
                            Widgets.Colour(sub, "Probe label colour", Color.white, _ => { }, null, true);
                        }, "Colours and label for Probe progress");
                        Widgets.Toggle(score, "Probe accuracy", false, _ => { });
                    }),
                    new NavItem("Probe style", body => { built("style"); Widgets.Slider(body, "Probe shadow", 0.5f, 0f, 1f, _ => { }); })),
                NavItem.Heading("Probe Interface",
                    new NavItem("Probe fonts", body => { built("fonts"); Ui.Muted("revision " + rev, body); }) { Revision = () => rev },
                    new NavItem("Probe far page", body => { built("far"); Widgets.Toggle(body, "Probe far-away switch", false, _ => { }); })),
            };

            var wasLanguage = Words.Language;
            Words.Language = () => "en";
            var sp = new SettingsPanel("Probe Menu", () => pages);
            Check(count("readouts") == 1 && count("style") + count("fonts") + count("far") == 0, "a nav panel builds only its first page at construction",
                  string.Join(", ", builds.Select(kv => kv.Key + "=" + kv.Value).ToArray()));
            sp.Visible = true;
            for (int i = 0; i < 3; i++) yield return null;
            var root = PanelRoot("ProbeMenuPanel");
            var views = root?.Query<ScrollView>().ToList();
            var rail = views != null && views.Count > 1 ? views[0] : null;
            var content = views != null && views.Count > 1 ? views[1] : null;
            Check(facetActive, "a page builds with Facet's Nav.Active set (Widgets.SubPage reaches it)");
            Check(LabelIn(rail, "PROBE OVERLAY") != null && LabelIn(rail, "PROBE INTERFACE") != null, "both NavItem.Headings head the rail");

            // ToggleGroup: its switch hides the rows it governs
            var onRow = LabelIn(content, "Probe overlay on")?.parent;
            var onBody = onRow?.parent?.ElementAt(onRow.parent.IndexOf(onRow) + 1);
            Check(onBody != null && LabelIn(onBody, "Probe score") != null && LabelIn(onBody, "Probe progress") != null,
                  "the ToggleGroup's body holds the Section and its rows");
            Tap(onRow?.Q("control")?.ElementAt(0)); yield return null;
            Check(onBody != null && onBody.style.display == DisplayStyle.None, "switching the ToggleGroup off hides its rows");
            Tap(onRow?.Q("control")?.ElementAt(0)); yield return null;

            // ToggleSubPage: the switch switches without navigating; the rest of the row pushes, the back row returns
            var statRow = LabelIn(content, "Probe progress")?.parent;
            Tap(statRow?.Q("control")?.ElementAt(0)); yield return null;
            Check(statOn && LabelIn(content, "Probe label colour") == null, "a ToggleSubPage's switch switches and stays on the page",
                  "on " + statOn);
            Tap(statRow); yield return null;
            var back = content?.Query<Label>().ToList().FirstOrDefault(l => l.text == "‹  Probe readouts");
            Check(back != null && LabelIn(content, "Probe label colour") != null && count("stat") == 1, "the rest of its row pushes its subpage",
                  "back row " + (back != null) + ", stat builds " + count("stat"));
            Tap(back?.parent); yield return null;
            Check(LabelIn(content, "Probe progress") != null && LabelIn(content, "Probe label colour") == null, "and the back row returns to the page");

            // Search finds a row on a page nobody has opened
            var search = root?.Query<TextField>().First();
            int farBefore = count("far");
            if (search != null) { search.value = "far-away"; yield return null; yield return null; }
            var hit = LabelIn(content, "Probe far-away switch");
            Check(search != null && hit != null && farBefore == 0, "search finds a row on a never-visited page",
                  search == null ? "no search box" : "far builds before " + farBefore + ", labels " + string.Join(" | ", content.Query<Label>().ToList().Select(l => l.text).Take(8).ToArray()));
            Tap(hit?.parent); yield return null;
            Check(LabelIn(content, "Probe far-away switch") != null && LabelIn(content, "Probe far page") == null && count("far") >= 1,
                  "clicking the hit opens its page");
            if (search != null) { search.value = ""; yield return null; }

            // Revision: a change rebuilds the open page on the next Tick
            Tap(LabelIn(rail, "Probe fonts")?.parent); yield return null;
            int fontsBuilt = count("fonts");
            Check(LabelIn(content, "revision 0") != null, "the Revision page opens");
            rev = 1;
            sp.Tick();
            Check(LabelIn(content, "revision 1") != null && count("fonts") == fontsBuilt + 1, "a Revision change rebuilds the page within one tick",
                  "fonts builds " + fontsBuilt + " -> " + count("fonts"));
            sp.Tick();
            Check(count("fonts") == fontsBuilt + 1, "and an unchanged Revision does not rebuild it again");

            // Reload brings the same items up to date in place: a renamed page, and a page added since
            pages[1].Children[1].Title = "Probe far page renamed";
            pages[1].Children.Add(new NavItem("Probe late page", body => { built("late"); Widgets.Toggle(body, "Probe late switch", false, _ => { }); }));
            sp.Reload();
            Check(LabelIn(rail, "Probe far page renamed") != null && LabelIn(rail, "Probe far page") == null, "Reload renames a page in the rail");
            Tap(LabelIn(rail, "Probe late page")?.parent); yield return null;
            Check(count("late") == 1 && LabelIn(content, "Probe late switch") != null, "and a page added since then opens", "late builds " + count("late"));

            Words.Language = wasLanguage;
            sp.Dispose();
        }

        // Topaz's real menu shape (PrismMenu.Build), opened the way Alt+T does: every page builds.
        // Rows are found by position, not text: the installed Topaz translates its own labels.
        IEnumerator TopazMenu()
        {
            var wasLanguage = Words.Language;
            var wasLog = Ui.Log;
            var errors = new System.Collections.Generic.List<string>();
            var opened = new System.Collections.Generic.List<string>();
            int stats = 0;
            var shape = PrismMenuShape(() => stats++);
            var leaves = shape.SelectMany(h => h.Children).ToList();
            foreach (var leaf in leaves)
            {
                var inner = leaf.Build; var title = leaf.Title;
                leaf.Build = body => { opened.Add(title); inner(body); };
            }
            Words.Language = () => "en";
            Ui.Log = m => { if (m.Contains("Nav.build") || m.Contains(" threw")) errors.Add(m); wasLog(m); };
            var sp = new SettingsPanel("Topaz", () => shape);
            sp.Visible = true;
            for (int i = 0; i < 3; i++) yield return null;
            var views = PanelRoot("TopazPanel")?.Query<ScrollView>().ToList();
            var rail = views != null && views.Count > 1 ? views[0] : null;
            var content = views != null && views.Count > 1 ? views[1] : null;
            // Re-read every time: opening a page rebuilds the rail.
            Func<System.Collections.Generic.List<VisualElement>> rows = () => rail == null ? new System.Collections.Generic.List<VisualElement>()
                : rail.contentContainer.Children().Where(e => !(e is Label)).ToList();
            int railRows = rows().Count;
            var bad = new System.Collections.Generic.List<string>();
            for (int i = 0; i < leaves.Count; i++)
            {
                var now = rows();
                Tap(i < now.Count ? now[i] : null); yield return null;
                bool failed = content == null || content.Query<Label>().ToList().Any(l => l.text.Contains("failed to build"));
                if (failed || opened.Count == 0 || opened[opened.Count - 1] != leaves[i].Title) bad.Add(leaves[i].Title);
                if (i == 0)
                {
                    int before = stats;
                    Tap(content?.Query<Label>("arrow").ToList().FirstOrDefault(l => l.text == "\u203A")?.parent); yield return null;
                    if (stats != before + 1) bad.Add(leaves[i].Title + " \u203a stat subpage");
                }
            }
            Check(railRows == 13 && bad.Count == 0 && errors.Count == 0, "every page of Topaz's menu shape opens, Stat subpage included",
                  railRows + " rail rows; not opened: " + string.Join(", ", bad.ToArray()) + "; log: " + string.Join(" | ", errors.ToArray()));
            Ui.Log = wasLog;
            Words.Language = wasLanguage;
            sp.Dispose();
        }

        // GradientEditor over the caller's own stops: the editor's edits reach them, and theirs reach it.
        IEnumerator GradientSync()
        {
            var wasLanguage = Words.Language;
            Words.Language = () => "en";
            var stops = new System.Collections.Generic.List<GradientStop>
            {
                new GradientStop { Position = 0f, Colour = Color.black },
                new GradientStop { Position = 1f, Colour = Color.white },
            };
            int changes = 0;
            var surface = new Surface("ProbeGradient", Layer.Window);
            surface.Visible = true;
            var ed = new GradientEditor(surface.Root, "Probe gradient", stops, () => changes++);
            yield return null; yield return null;
            var add = ed.Root.Query<Button>().ToList().FirstOrDefault(b => b.text == "Add stop");
            if (add != null) { Submit(add); yield return null; }
            Check(stops.Count == 3 && Near(stops[1].Position, 0.1f, 0.001f) && stops[2].Position == 1f && changes == 1,
                  "the gradient editor's Add stop lands in the caller's list, sorted",
                  add == null ? "no Add stop button" : string.Join(" ", stops.Select(st => st.Position.ToString("0.00")).ToArray()) + ", changes " + changes);
            var hex = ed.Root.Query<TextField>().First();
            if (hex != null)
            {
                hex.SetValueWithoutNotify("#FF0000");
                using (var ke = KeyDownEvent.GetPooled('\n', KeyCode.Return, EventModifiers.None)) { ke.target = hex; hex.SendEvent(ke); }
                yield return null;
            }
            Check(stops.Count == 3 && stops[1].Colour == Color.red, "a colour typed into the selected stop is written to the caller's stop",
                  stops.Count > 1 ? stops[1].Colour.ToString() : "stops " + stops.Count);
            stops[2].Colour = Color.blue;
            Check(ed.Evaluate(1f) == Color.blue, "a colour the caller sets on its own stop is what the editor evaluates", ed.Evaluate(1f).ToString());
            stops.RemoveAt(1);
            Check(ed.Evaluate(0.5f) == Color.Lerp(Color.black, Color.blue, 0.5f), "and so is a stop the caller removed", ed.Evaluate(0.5f).ToString());
            surface.Dispose();
            Words.Language = wasLanguage;
        }

        /* Topaz's PrismMenu.Build() as it stands (Topaz 824a8ab..f826cab): the same rail and, page by
           page, the same Widgets and Ui calls, with local values in place of Topaz's settings. */
        static System.Collections.Generic.List<NavItem> PrismMenuShape(Action onStat)
        {
            Action<bool> b = _ => { };
            Action<VisualElement, string> stat = (parent, label) => Widgets.ToggleSubPage(parent, label, true, b, body =>
            {
                onStat();
                Ui.Muted("Position and size are set by dragging, under Interface › Positions.", body);
                Widgets.Colour(body, "Label colour", Color.white, _ => { }, null, true);
                Widgets.Colour(body, "Value colour", Color.white, _ => { }, null, true);
            }, "Colours and label for " + label);
            Action<VisualElement, string[]> toggles = (parent, labels) => { foreach (var l in labels) Widgets.Toggle(parent, l, false, b); };
            int packs = 2;
            return new System.Collections.Generic.List<NavItem>
            {
                NavItem.Heading("Overlay",
                    new NavItem("Readouts", body =>
                    {
                        var on = Widgets.ToggleGroup(body, "Enable", true, b, "Master switch for every overlay readout");
                        var score = Widgets.Section(on, "Score");
                        stat(score, "Progress"); stat(score, "Best progress");
                        Widgets.Toggle(score, "Progress bar", true, b);
                        stat(score, "Accuracy"); stat(score, "XAccuracy"); stat(score, "XScore");
                        var tempo = Widgets.Section(on, "Tempo");
                        stat(tempo, "BPM"); stat(tempo, "Tile BPM"); stat(tempo, "Keys per second");
                        var session = Widgets.Section(on, "Session");
                        Widgets.Toggle(session, "Attempts", true, b);
                        Widgets.Toggle(session, "Full attempts only", false, b, "Count only attempts that started from the beginning");
                        toggles(session, new[] { "Song duration", "Level duration" });
                    }),
                    new NavItem("Judgement", body => toggles(body, new[] { "Judgements", "Hand viewer", "Foot viewer" })),
                    new NavItem("Timing", body =>
                    {
                        toggles(body, new[] { "Hit error", "Timing scale" });
                        Widgets.ToggleGroup(body, "Timing graph", false, b, "A histogram of how early or late each hit was");
                    }),
                    new NavItem("Style", body =>
                    {
                        var shadow = Widgets.ToggleGroup(body, "Text shadow", true, b);
                        Widgets.Colour(shadow, "Shadow colour", new Color(0f, 0f, 0f, 0.5f), _ => { }, null, true);
                        Widgets.Toggle(body, "Apply the master font to all overlays", false, b, "Off: each overlay part keeps its own font");
                        Ui.Muted("Position and size are set by dragging, under Interface › Positions.", body);
                    })),
                NavItem.Heading("Interface",
                    new NavItem("Hide UI", body =>
                    {
                        var on = Widgets.ToggleGroup(body, "Enable", true, b, "Master switch for everything on this page");
                        Widgets.Toggle(on, "Hide all UI", false, b, "RDC.noHud — hides the game's entire HUD");
                        toggles(Widgets.Section(on, "Elements"), new[] { "Hit error meter", "Autoplay text", "Autoplay icon", "No-fail badge",
                                                                         "Difficulty", "Level name", "Controls tip", "Beta build label" });
                        var judging = Widgets.ToggleGroup(Widgets.Section(on, "Judgements"), "Hide judgements", true, b);
                        toggles(judging, new[] { "All", "Perfect", "Pure perfect", "Early / late perfect", "Early / late", "Miss", "Death" });
                    }),
                    new NavItem("Appearance", body =>
                    {
                        Widgets.Choice(body, "Language", new[] { "Follow game", "English", "한국어" }, 0, _ => { });
                        Widgets.Slider(body, "UI scale", 1f, 0.6f, 1.6f, _ => { }, "Size of Topaz's own panels");
                        Widgets.Toggle(body, "Include system fonts", false, b, "Offer every font installed on this machine, not just the bundled ones");
                        var accent = Widgets.Section(body, "Accent");
                        var custom = Widgets.ToggleGroup(accent, "Use a custom colour", true, b);
                        Widgets.Colour(custom, "Accent colour", new Color(1f, 0.7f, 0.2f, 1f), _ => { });
                        Widgets.Toggle(accent, "Apply accent as theme colour", false, b, "Tints the panels themselves, not just the highlights");
                    }),
                    new NavItem("Fonts", body =>
                    {
                        Ui.Muted("Font packs add families Topaz can use for its panels and overlays.", body);
                        var list = Ui.Box(body);
                        for (int i = 0; i < packs; i++)
                        {
                            var actions = Widgets.Item(list, "Pack " + i, i == 0 ? "Installed" : null);
                            if (i == 0) { Ui.Btn("Open folder", () => { }, actions); Ui.Btn("Remove", () => { }, actions); }
                            else Ui.Btn("Install", () => { }, actions);
                        }
                        Ui.Btn("Refresh list", () => { }, Ui.Row(body));
                    }) { Revision = () => packs },
                    new NavItem("Profiles", body =>
                    {
                        var list = Ui.Box(body);
                        foreach (var n in new[] { "Default", "Mine" })
                        {
                            var actions = Widgets.Item(list, n, n == "Default" ? "Built in" : null);
                            if (n == "Default") Widgets.MarkActive(actions);
                            Ui.Btn("Load", () => { }, actions);
                            if (n != "Default") Ui.Btn("Delete", () => { }, actions);
                        }
                        Widgets.Text(body, "New profile name", "", _ => { }, "Saves every current setting under this name");
                        var row = Ui.Row(body);
                        Ui.Btn("Save current settings", () => { }, row);
                        Ui.Btn("Open folder", () => { }, row);
                    }),
                    new NavItem("Game UI", body =>
                    {
                        var text = Widgets.Section(body, "Text");
                        foreach (var l in new[] { "Game text size", "Line spacing", "Level stats size", "Judgement size" })
                            Widgets.Slider(text, l, 1f, 0.5f, 2f, _ => { });
                        Widgets.Toggle(Widgets.Section(body, "Font"), "Use the overlay's font", false, b, "Off: the game's own font is left alone");
                        var over = Widgets.ToggleGroup(Widgets.Section(body, "Error meter"), "Override the game's meter", true, b);
                        Widgets.Slider(over, "Scale", 1f, 0.25f, 3f, _ => { });
                        Ui.Muted("Positions are set by dragging, under Interface › Positions.", body);
                    }),
                    new NavItem("Positions", body =>
                    {
                        Ui.Muted("Drag elements where you want them, on the real screen.", body);
                        Widgets.Action(body, "Edit positions", "Open editor", () => { }, "Topaz's overlays and the game's own HUD, in one editor");
                        Widgets.DangerButton(body, "Reset all positions", () => { }, "Puts every element back where the game or Topaz had it");
                    })),
                NavItem.Heading("Gameplay",
                    new NavItem("Input", body =>
                    {
                        Widgets.Toggle(body, "Block game input while the menu is open", true, b, "Keys typed at Topaz do not reach the game. Prism windows are exempt.");
                        var limiter = Widgets.Section(body, "Key limiter");
                        Widgets.Toggle(limiter, "Enable", false, b);
                        Widgets.Toggle(limiter, "Use the key viewer's keys", false, b, "Takes the allowed list from the active key viewer preset");
                        var chatter = Widgets.Section(body, "Chatter blocker");
                        Widgets.Toggle(chatter, "Enable", false, b);
                        Widgets.IntSlider(chatter, "Threshold (ms)", 30, 0, 60, _ => { }, "Repeats of one key closer together than this are dropped");
                    }),
                    new NavItem("Tweaks", body =>
                    {
                        Widgets.Slider(body, "CLS preview volume", 70f, 0f, 100f, _ => { }, "Volume of the custom level select's song preview", "0");
                        Widgets.Toggle(Widgets.Section(body, "Gameplay"), "Hide planet orbit rings", false, b, "Hides the dotted circle drawn around each planet");
                    })),
                NavItem.Heading("System",
                    new NavItem("Diagnostics", body =>
                    {
                        Widgets.Action(body, "Log and mod state", "Open", () => { }, "The shared Prism window: every mod's log and state");
                        Widgets.Action(body, "Reload Topaz", "Reload", () => { }, "Rebuilds everything without restarting the game");
                        var updates = Widgets.Section(body, "Updates");
                        Widgets.Choice(updates, "Channel", new[] { "Stable", "Beta" }, 0, _ => { }, "Beta offers prereleases as well");
                        Widgets.Action(updates, "Check for updates", "Check now", () => { });
                        Widgets.Action(updates, "Releases page", "Open", () => { });
                        var dev = Widgets.ToggleGroup(body, "Debug mode", true, b, "Extra logging, and the tools below");
                        Widgets.Toggle(dev, "Trace font sweep", false, b, "Logs every text object the font sweep touches");
                        Widgets.Text(dev, "Filter", "", _ => { }, "Only dump objects whose name contains this");
                        var dumps = Widgets.Section(dev, "Dump");
                        foreach (var l in new[] { "Texts", "Images", "Assets" }) Widgets.Action(dumps, l, "Dump", () => { });
                    })),
            };
        }
    }
}
