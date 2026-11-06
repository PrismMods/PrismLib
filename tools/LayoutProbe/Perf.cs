// What opening a Prism window costs, first time and again: constructor, show, layout and draw, the
// frames after it, and how many glyphs the shared fonts had to rasterise. Logged as [perf] lines,
// before anything else in the autotest has drawn a Prism window; one budget check on top.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using PrismLib.UI.Toolkit;
using UnityEngine;
using UnityEngine.TextCore.Text;

namespace LayoutProbe
{
    static class Perf
    {
        // A frame at 120 Hz is 8 ms; a first open used to take 90-100 ms before SettingsPanel.WarmUp.
        const float FirstOpenBudgetMs = 50f;

        internal static IEnumerator Run(Action<bool, string, string> check)
        {
            float t0 = Time.realtimeSinceStartup;
            while (!SettingsPanel.WarmedUp && Time.realtimeSinceStartup - t0 < 10f) yield return null;
            Main.Log("[perf] warm-up " + (SettingsPanel.WarmedUp ? "done" : "NOT done") + ", waited "
                     + (Time.realtimeSinceStartup - t0).ToString("0.00") + " s");
            yield return Frames("idle");
            float first = 0f;
            yield return Window("settings A", () => Settings("Perf Settings A"), ms => first = ms);
            check(first < FirstOpenBudgetMs, "the first Prism window opens without a hitch (< " + FirstOpenBudgetMs + " ms frame)",
                  first.ToString("0") + " ms");
            yield return Window("settings B", () => Settings("Perf Settings B"), null);
            yield return Window("debug", () =>
            {
                var d = new DebugPanel("Perf Debug", Tabs);
                return v => d.Visible = v;
            }, null);
        }

        static Action<bool> Settings(string title)
        {
            var p = new SettingsPanel(title, Rows);
            return v => p.Visible = v;   // Visible = true reloads the rows
        }

        static IEnumerator Window(string what, Func<Action<bool>> make, Action<float> firstFrame)
        {
            int g0 = Glyphs();
            var sw = Stopwatch.StartNew();
            var show = make();
            long ctor = sw.ElapsedMilliseconds;
            show(true);
            long open = sw.ElapsedMilliseconds - ctor;
            yield return new WaitForEndOfFrame();
            long draw = sw.ElapsedMilliseconds - ctor - open;
            var f = new List<string>();
            for (int i = 0; i < 4; i++)
            {
                yield return null;
                if (i == 0) firstFrame?.Invoke(Time.unscaledDeltaTime * 1000f);
                f.Add(Ms());
            }
            Main.Log("[perf] " + what + " first open: ctor " + ctor + " ms, show " + open + " ms, layout+draw " + draw
                     + " ms, frames " + string.Join("/", f.ToArray()) + " ms, glyphs +" + (Glyphs() - g0));
            show(false);
            for (int i = 0; i < 3; i++) yield return null;
            g0 = Glyphs();
            sw = Stopwatch.StartNew();
            show(true);
            long re = sw.ElapsedMilliseconds;
            f.Clear();
            for (int i = 0; i < 4; i++) { yield return null; f.Add(Ms()); }
            Main.Log("[perf] " + what + " reopen: show " + re + " ms, frames " + string.Join("/", f.ToArray())
                     + " ms, glyphs +" + (Glyphs() - g0));
            show(false);
            for (int i = 0; i < 3; i++) yield return null;
        }

        static IEnumerator Frames(string what)
        {
            var f = new List<string>();
            for (int i = 0; i < 4; i++) { yield return null; f.Add(Ms()); }
            Main.Log("[perf] " + what + ": frames " + string.Join("/", f.ToArray()) + " ms, glyphs " + Glyphs());
        }

        static string Ms() => (Time.unscaledDeltaTime * 1000f).ToString("0");

        // Regular + bold of the shared sans and mono faces.
        static int Glyphs()
        {
            int n = 0;
            foreach (var name in new[] { "_fallback", "_mono" })
            {
                var fa = typeof(Surface).GetField(name, BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null) as FontAsset;
                if (fa == null) continue;
                n += fa.glyphTable.Count;
                var t = fa.fontWeightTable;
                if (t != null && t.Length > 7 && t[7].regularTypeface != null) n += t[7].regularTypeface.glyphTable.Count;
            }
            return n;
        }

        static IEnumerable<SettingRow> Rows()
        {
            for (int i = 0; i < 6; i++)
            {
                string g = "Group " + i;
                yield return new SettingRow { Owner = "Perf", Page = "Perf", Group = g, Label = "Toggle setting " + i, Kind = SettingControl.Bool, Get = () => true, Set = _ => { } };
                yield return new SettingRow { Owner = "Perf", Page = "Perf", Group = g, Label = "Slider setting " + i, Kind = SettingControl.Float, Min = 0, Max = 1, Get = () => 0.5f, Set = _ => { } };
                yield return new SettingRow { Owner = "Perf", Page = "Perf", Group = g, Label = "Choice setting " + i, Kind = SettingControl.Choice, Options = new[] { "One", "Two", "Three" }, Get = () => 1, Set = _ => { } };
                yield return new SettingRow { Owner = "Perf", Page = "Perf", Group = g, Label = "Text setting " + i, Kind = SettingControl.Text, Get = () => "Some value", Set = _ => { } };
                yield return new SettingRow { Owner = "Perf", Page = "Perf", Group = g, Label = "Action " + i, Kind = SettingControl.Action, ActionLabel = "Do it", Set = _ => { } };
            }
        }

        static IEnumerable<DebugTab> Tabs()
        {
            var lines = new List<string>();
            for (int i = 0; i < 300; i++) lines.Add("[12:00:00] [prism] log line number " + i + " with some ordinary text in it");
            yield return new DebugTab { Name = "Log", Lines = () => lines };
        }
    }
}
