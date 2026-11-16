// Dev tool: an in-game rig for the shared LayoutEditor (PrismLib.UI), no real mod required.
// Two uGUI boxes register as EditTargets: "Probe A" supports every optional delegate, "Probe B"
// only the required ones; "Probe C" starts throwing (a mod that went away) during the autotest.
// F9 toggles the editor, F10 toggles an empty target list. An "autotest" file in the mod folder
// runs AutoTest.cs instead and quits the game when done.
using System;
using System.Collections.Generic;
using System.IO;
using PrismLib.UI.Toolkit;
using UnityEngine;
using UnityEngine.UI;
using UnityModManagerNet;

namespace LayoutProbe
{
    public static class Main
    {
        static string _log;
        internal static void Log(string s)
        {
            try { File.AppendAllText(_log, DateTime.Now.ToString("HH:mm:ss.fff") + " " + s + "\n"); } catch { }
        }

        internal static bool Auto;

        public static bool Load(UnityModManager.ModEntry e)
        {
            _log = Path.Combine(e.Path, "probe.log");
            Auto = File.Exists(Path.Combine(e.Path, "autotest"));
            e.OnUpdate = (m, dt) => Runner.Tick();
            Ui.Log = m => Log("[ui] " + m);
            Log("LayoutProbe loaded");
            return true;
        }
    }

    sealed class Box
    {
        public string Name;
        public float Cx, Cy, Scale = 1f, Rot, W = 320f, H = 180f;
        public int Anchor = 4;
        public bool Closest = true;
        public float[] Crop = new float[4];
        public RectTransform Rt;

        public float[] Get() => new[] { Cx, Cy, W * Scale * (1 - Crop[0] - Crop[2]), H * Scale * (1 - Crop[1] - Crop[3]) };

        public void Apply()
        {
            var b = Get();
            Rt.anchoredPosition = new Vector2(Cx, Cy);
            Rt.sizeDelta = new Vector2(b[2], b[3]);
            Rt.localEulerAngles = new Vector3(0, 0, Rot);
        }

        public override string ToString() =>
            Name + " c=(" + Cx.ToString("0") + "," + Cy.ToString("0") + ") scale=" + Scale.ToString("0.00") + " rot=" + Rot.ToString("0")
            + " anchor=" + Anchor + (Closest ? "(closest)" : "") + " crop=" + string.Join("/", Array.ConvertAll(Crop, c => c.ToString("0.00")));
    }

    static class Runner
    {
        static LayoutEditor _editor;
        static Box _a, _b, _c;
        static bool _empty;

        public static void Tick()
        {
            if (_a == null && Time.frameCount > 60)
            {
                Build();
                if (Main.Auto)
                {
                    _editor = new LayoutEditor(Targets);
                    AutoTest.Editor = _editor; AutoTest.A = _a; AutoTest.B = _b; AutoTest.C = _c;
                    AutoTest.SetEmpty = v => _empty = v;
                    _a.Rt.parent.gameObject.AddComponent<AutoTest>();
                    Main.Log("autotest armed");
                }
            }
            if (_a == null) return;
            if (!Main.Auto && Input.GetKeyDown(KeyCode.F9))
            {
                if (_editor == null)
                {
                    _editor = new LayoutEditor(Targets);
                    _editor.Closed += () => { Main.Log("closed: " + _a); Main.Log("closed: " + _b); };
                }
                _editor.Toggle();
                Main.Log("editor visible=" + _editor.Visible);
            }
            if (!Main.Auto && Input.GetKeyDown(KeyCode.F10) && _editor != null)
            {
                _empty = !_empty;
                _editor.Reload();
                Main.Log("empty=" + _empty);
            }
            _a.Apply(); _b.Apply(); _c.Apply();
            if (_editor != null) _editor.Tick();
        }

        static IEnumerable<EditTarget> Targets()
        {
            if (_empty) yield break;
            var a = _a;
            yield return new EditTarget
            {
                Owner = "LayoutProbe", Id = "a", Label = "Probe A",
                GetBox = a.Get, MoveTo = (x, y) => { a.Cx = x; a.Cy = y; },
                GetScale = () => a.Scale, SetScale = s => a.Scale = s,
                GetRotation = () => a.Rot, SetRotation = r => a.Rot = r,
                GetAnchor = () => a.Anchor, SetAnchor = i => a.Anchor = i,   // centre is stored directly, so a re-anchor never moves it
                GetAnchorClosest = () => a.Closest, SetAnchorClosest = c => a.Closest = c,
                GetCrop = () => (float[])a.Crop.Clone(), SetCrop = c => a.Crop = (float[])c.Clone(),
            };
            if (AutoTest.OnlyA) yield break;
            var b = _b;
            yield return new EditTarget
            {
                Owner = AutoTest.BOwner ?? "LayoutProbe", Id = "b", Label = "Probe B",
                GetBox = b.Get, MoveTo = (x, y) => { b.Cx = x; b.Cy = y; },
                GetScale = () => b.Scale, SetScale = s => b.Scale = s,
            };
            // Stands in for a mod that went away mid-session: its delegates start throwing.
            var pc = _c;
            yield return new EditTarget
            {
                Owner = "LayoutProbe", Id = "c", Label = "Probe C",
                GetBox = () => { if (AutoTest.CThrows) throw new ObjectDisposedException("Probe C"); return AutoTest.CNull ? null : pc.Get(); },
                MoveTo = (x, y) => { pc.Cx = x; pc.Cy = y; },
                GetScale = () => pc.Scale, SetScale = s => pc.Scale = s,
            };
        }

        static void Build()
        {
            var go = new GameObject("LayoutProbeCanvas");
            UnityEngine.Object.DontDestroyOnLoad(go);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            _a = NewBox(go, "Probe A", new Color(0.95f, 0.55f, 0.2f, 0.85f), Screen.width * 0.35f, Screen.height * 0.5f);
            _b = NewBox(go, "Probe B", new Color(0.3f, 0.75f, 0.95f, 0.85f), Screen.width * 0.65f, Screen.height * 0.5f);
            _c = NewBox(go, "Probe C", new Color(0.6f, 0.9f, 0.4f, 0.85f), Screen.width * 0.5f, Screen.height * 0.2f);
            _c.W = 200f; _c.H = 110f;
            Main.Log("boxes built; screen " + Screen.width + "x" + Screen.height);
        }

        static Box NewBox(GameObject parent, string name, Color c, float x, float y)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.AddComponent<Image>().color = c;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = Vector2.zero;   // bottom-left: anchoredPosition == screen px
            rt.pivot = new Vector2(0.5f, 0.5f);
            return new Box { Name = name, Cx = x, Cy = y, Rt = rt };
        }
    }
}
