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

            Main.Log("AUTOTEST DONE pass=" + _pass + " fail=" + _fail);
            yield return null;
            Application.Quit();
        }
    }
}
