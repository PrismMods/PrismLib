// The layout editor's owner filter: B belongs to a second owner ("Gamma") for this section, so the
// tab bar has two owners to switch between. Every box's centre and the filter are put back at the end.
using System.Collections;
using System.Linq;
using PrismLib.UI.Toolkit;
using UnityEngine;
using UnityEngine.UIElements;

namespace LayoutProbe
{
    sealed partial class AutoTest
    {
        VisualElement FilterBar => Root?.Q("layout-filter");

        string[] FilterTabTexts()
        {
            var bar = FilterBar;
            return bar == null ? new string[0] : bar.Query<Button>().ToList().Select(b => b.text).ToArray();
        }

        Button FilterTab(string text) => FilterBar?.Query<Button>().ToList().FirstOrDefault(b => b.text == text);

        // The editor's "All" is PrismLib's word, whoever else translates "All".
        static string AllText() => Words.Own("All");

        // Stands in for a mod whose table maps the editor's words to another sense (Quartz's "Undo", "All").
        static bool _wrongWords;
        static bool _wrongAdded;

        static string ButtonText(VisualElement root, string name) => root?.Q<Button>(name)?.text;

        IEnumerator EditorWordsChecks()
        {
            if (!_wrongAdded)
            {
                _wrongAdded = true;
                Words.AddTranslator(s => _wrongWords && (s == "Undo" || s == "Redo" || s == "All") ? "WRONG" : null);
            }
            var wasLanguage = Words.Language;
            var host = Facet.Host.Named(PrismHost.Owner(Words.Self));
            _wrongWords = true;
            try
            {
                foreach (var lang in new[] { "en", "ko" })
                {
                    Words.Language = () => lang;
                    host?.RelabelSurfaces();   // what a language switch does to an open window
                    Editor.Reload();
                    yield return Frames(2);
                    var tabs = FilterTabTexts();
                    string got = ButtonText(Root, "layout-undo") + " | " + ButtonText(Root, "layout-redo") + " | " + tabs.FirstOrDefault();
                    string want = lang == "en" ? "Undo | Redo | All" : "실행 취소 | 다시 실행 | 전체";
                    Check(got == want, lang == "en" ? "layout editor: Undo, Redo and All stay in PrismLib's words"
                                                    : "and in PrismLib's Korean after a language flip", got);
                }
            }
            finally { _wrongWords = false; Words.Language = wasLanguage; }
            host?.RelabelSurfaces();
            Editor.Reload();
            yield return Frames(2);

            // Bottom gap equals top gap: Facet.Tabs' TabGap below the row is gone.
            var bar = FilterBar;
            var row = FilterTab(AllText())?.parent;
            float top = 0f, bottom = 0f;
            if (bar != null && row != null) { top = row.worldBound.yMin - bar.worldBound.yMin; bottom = bar.worldBound.yMax - row.worldBound.yMax; }
            Check(row != null && Mathf.Abs(bottom - top) <= 1f, "layout editor: no gap under the filter tabs", "top=" + top.ToString("0.0") + " bottom=" + bottom.ToString("0.0"));
        }

        float[] FrameOpacities() => Root.Query("layout-frame").ToList().Select(f => f.resolvedStyle.opacity).ToArray();

        bool NothingSelected() => Root.Q<TextField>("layout-x") == null;

        // A press on the tab button's screen centre, through the panel like the player's mouse.
        IEnumerator ClickTab(string text)
        {
            var b = FilterTab(text);
            float k = K();
            if (b == null || k <= 0f) yield break;
            yield return Click(new Vector2(b.worldBound.center.x / k, Screen.height - b.worldBound.center.y / k));
        }

        // Every point of the bar outside its buttons picks the editor's layer, so the press reaches what lies under it.
        string BarPickLeaks()
        {
            var bar = FilterBar;
            if (bar == null) return "no bar";
            var layer = Root.Q("layout-layer");
            Rect r = bar.worldBound;
            for (float y = r.yMin + 1f; y < r.yMax; y += 3f)
                for (float x = r.xMin + 1f; x < r.xMax; x += 3f)
                {
                    var p = bar.panel.Pick(new Vector2(x, y));
                    if (p == layer) continue;
                    var e = p;
                    while (e != null && !(e is Button)) e = e.parent;
                    if (e == null) return (p == null ? "null" : p.GetType().Name + " " + p.name) + " at " + x.ToString("0") + "," + y.ToString("0");
                }
            return null;
        }

        static string Joined(float[] v) => string.Join(",", v.Select(x => x.ToString("0.00")).ToArray());

        IEnumerator FilterChecks()
        {
            float W = Screen.width, H = Screen.height;
            Vector2 a0 = Ctr(A), b0 = Ctr(B), c0 = Ctr(C);
            string filter0 = Editor.Filter;
            BOwner = "Gamma";
            Editor.Visible = false; yield return null;
            A.Cx = W * 0.3f; A.Cy = H * 0.5f; B.Cx = W * 0.7f; B.Cy = H * 0.5f; C.Cx = W * 0.5f; C.Cy = H * 0.2f;
            Editor.Reload();
            Editor.Visible = true;
            yield return Frames(3);

            var bBefore = Ctr(B);
            yield return Click(Ctr(B));
            yield return Drag(Ctr(B), Ctr(B) + new Vector2(60, 0));
            Check(filter0 == null && Editor.Filter == null && Ctr(B) != bBefore, "filter: the editor opens on All by default", "filter0=" + filter0 + " filter=" + Editor.Filter + " B " + B);
            B.Cx = W * 0.7f; B.Cy = H * 0.5f;

            var tabs = FilterTabTexts();
            Check(string.Join(",", tabs) == AllText() + ",LayoutProbe,Gamma", "filter: the bar lists All, then each owner once", string.Join(",", tabs));
            string leak = BarPickLeaks();
            Check(leak == null, "filter: only the bar's tab buttons take the pointer", leak);

            Editor.Visible = false; yield return null;
            Editor.Filter = "Gamma";
            Editor.Visible = true;
            yield return Frames(3);
            Check(FilterBar != null && (FilterBar.userData as string) == "Gamma", "filter: a preset filter opens on that owner's tab", "userData=" + FilterBar?.userData);

            var op = FrameOpacities();
            Check(op.Length == 3 && Near(op[0], 0.35f, 0.01f) && Near(op[1], 1f, 0.01f), "filter: other owners' elements are dimmed", Joined(op));

            var aAt = Ctr(A);
            yield return Drag(aAt, aAt + new Vector2(80, 40));
            yield return Frames(3);
            Check(Ctr(A) == aAt && NothingSelected(), "filter: a dimmed element does not move", A.ToString());

            // C is drawn last, so on top of B; dimmed, it must let the press through to B.
            C.Cx = B.Cx; C.Cy = B.Cy; yield return null;
            bBefore = Ctr(B); var cBefore = Ctr(C);
            yield return Click(Ctr(B));
            yield return Drag(Ctr(B), Ctr(B) + new Vector2(60, 0));
            Check(Ctr(B) != bBefore && Ctr(C) == cBefore, "filter: an element under a dimmed one can still be moved", "B " + B + " | C " + C);
            C.Cx = W * 0.5f; C.Cy = H * 0.2f; B.Cx = W * 0.7f; B.Cy = H * 0.5f; yield return null;

            yield return ClickTab("LayoutProbe");
            yield return Frames(2);
            op = FrameOpacities();
            Check(Editor.Filter == "LayoutProbe" && (FilterBar.userData as string) == "LayoutProbe" && op.Length > 0 && Near(op[0], 1f, 0.01f),
                  "filter: clicking a tab switches the filter", "filter=" + Editor.Filter + " userData=" + FilterBar?.userData + " op=" + Joined(op));

            yield return Click(Ctr(A));
            bool aSelected = !NothingSelected();
            yield return ClickTab("Gamma");
            yield return Frames(2);
            Check(aSelected && NothingSelected(), "filter: switching tabs drops a selection the tab cannot move", "A was selected=" + aSelected);

            Editor.Filter = "Nobody"; Editor.Reload();
            yield return Frames(3);
            tabs = FilterTabTexts(); op = FrameOpacities();
            aAt = Ctr(A); var bAt = Ctr(B);
            yield return Drag(aAt, aAt + new Vector2(60, 0));
            yield return Drag(bAt, bAt + new Vector2(60, 0));
            Check(tabs.LastOrDefault() == "Nobody" && op.Length == 3 && op.All(o => Near(o, 0.35f, 0.01f)) && Ctr(A) == aAt && Ctr(B) == bAt,
                  "filter: a filter naming an owner with no element keeps its tab", string.Join(",", tabs) + " op=" + Joined(op) + " A " + A + " | B " + B);

            OnlyA = true; Editor.Filter = "Gamma"; Editor.Reload();
            yield return Frames(3);
            bool none = NothingSelected();
            aAt = Ctr(A);
            yield return Drag(aAt, aAt + new Vector2(60, 0));
            Check(none && Ctr(A) == aAt, "filter: a lone element of another owner is not selected", "nothing selected=" + none + " A " + A);
            OnlyA = false; Editor.Reload();
            yield return Frames(2);

            yield return ClickTab(AllText());
            yield return Frames(2);
            op = FrameOpacities();
            Check(Editor.Filter == null && op.Length == 3 && op.All(o => Near(o, 1f, 0.01f)), "filter: All moves everything again", "filter=" + Editor.Filter + " op=" + Joined(op));

            yield return EditorWordsChecks();

            Editor.Visible = false; yield return null;
            A.Cx = a0.x; A.Cy = a0.y; B.Cx = b0.x; B.Cy = b0.y; C.Cx = c0.x; C.Cy = c0.y;
            Editor.Filter = filter0;
            BOwner = null; Editor.Reload();
            yield return null;
        }
    }
}
