using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace PrismLib
{
    /* Stands down a mod that is known to break the game, or the other mods, on the running build.

       Quartz targets ADOFAI 3.x. On 2.x its patches fail to build (scnGame.Play's "remakeFloors"
       parameter), and a Harmony replacement that fails to build takes every mod's patches on that
       method with it — on 2.9.8 nothing Bismuth had on scnGame.Play ever ran. So on 2.x Quartz is
       shut down through whichever loader runs it: MelonLoader's Unregister when MelonLoader is
       loaded (OnDeinitializeMelon → its runtime's Unload, and its ticks stop), otherwise UMM's
       own OnUnload (MainCore.Dispose) and Active = false — runtime only, nothing persisted, so a
       later 3.x launch is untouched. Then any patch it still owns is removed, which rebuilds those
       methods with everyone else's patches intact.

       Reflection only: PrismLib references neither Unity, MelonLoader nor Harmony. The bridges call
       Tick every frame; it decides once, on the first frame the game version is readable. Quartz
       registers during MelonLoader's mod init, which is long before the first scene any bridge
       ticks in, so one look is enough. */
    public static class Guards
    {
        private static bool _done;

        public static void Tick()
        {
            if (_done) return;
            string version = GameVersion();
            if (version == null) return;          // Unity not readable yet; try next frame
            _done = true;
            if (Major(version) >= 3) return;

            string why = "Quartz supports ADOFAI 3.x only; stood down by PrismLib on " + version;
            string via = null;
            var melon = FindMelon("Quartz");          // null whenever MelonLoader isn't loaded
            if (melon != null)
            {
                via = "MelonLoader";
                try { melon.GetType().GetMethod("Unregister", new[] { typeof(string), typeof(bool) }).Invoke(melon, new object[] { why, false }); }
                catch (Exception e) { Prism.Log("PrismLib: could not unregister Quartz: " + Inner(e).Message); }
            }
            else
            {
                var entry = FindUmmMod("Quartz");
                if (entry != null) { via = "UMM"; UnloadUmm(entry); }
            }
            int removed = UnpatchOwners("Quartz");   // whatever its own shutdown left behind
            if (via == null && removed == 0) return;
            Prism.Log("PrismLib: " + why + " (" + (via ?? "no loader entry") + ", " + removed + " patch(es) removed)");
        }

        /// The leading integer of a version string ("2.9.8" → 2); -1 when there is none.
        public static int Major(string version)
        {
            if (string.IsNullOrEmpty(version)) return -1;
            int i = 0, n = 0;
            while (i < version.Length && char.IsDigit(version[i])) n = n * 10 + (version[i++] - '0');
            return i == 0 ? -1 : n;
        }

        private static string GameVersion()
        {
            try
            {
                var app = Type.GetType("UnityEngine.Application, UnityEngine.CoreModule");
                return app != null ? app.GetProperty("version").GetValue(null, null) as string : null;
            }
            catch { return null; }
        }

        private static object FindMelon(string name)
        {
            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var t = asm.GetType("MelonLoader.MelonBase", false);
                    if (t == null) continue;
                    var all = t.GetProperty("RegisteredMelons", BindingFlags.Public | BindingFlags.Static).GetValue(null, null) as IEnumerable;
                    if (all == null) continue;
                    foreach (var m in all)
                    {
                        var info = m.GetType().GetProperty("Info").GetValue(m, null);
                        var n = info != null ? info.GetType().GetProperty("Name").GetValue(info, null) as string : null;
                        if (string.Equals(n, name, StringComparison.OrdinalIgnoreCase)) return m;
                    }
                }
            }
            catch (Exception e) { Prism.Log("PrismLib: melon scan failed: " + Inner(e).Message); }
            return null;
        }

        private static object FindUmmMod(string id)
        {
            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var umm = asm.GetType("UnityModManagerNet.UnityModManager", false);
                    if (umm == null) continue;
                    var entries = umm.GetField("modEntries", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as IEnumerable;
                    if (entries == null) continue;
                    foreach (var e in entries)
                    {
                        var info = e.GetType().GetField("Info")?.GetValue(e);
                        var eid = info != null ? info.GetType().GetField("Id")?.GetValue(info) as string : null;
                        if (string.Equals(eid, id, StringComparison.OrdinalIgnoreCase)) return e;
                    }
                }
            }
            catch (Exception e) { Prism.Log("PrismLib: UMM scan failed: " + Inner(e).Message); }
            return null;
        }

        // The mod's own OnUnload (its full teardown), then inactive so UMM stops ticking it.
        private static void UnloadUmm(object entry)
        {
            var t = entry.GetType();
            try
            {
                var onUnload = t.GetField("OnUnload")?.GetValue(entry) as Delegate;
                if (onUnload != null) onUnload.DynamicInvoke(entry);
            }
            catch (Exception e) { Prism.Log("PrismLib: Quartz OnUnload threw: " + Inner(e).Message); }
            try { t.GetProperty("Active")?.SetValue(entry, false, null); }
            catch (Exception e) { Prism.Log("PrismLib: could not deactivate Quartz: " + Inner(e).Message); }
        }

        // Every patch whose Harmony owner id starts with `prefix`, through each loaded Harmony.
        private static int UnpatchOwners(string prefix)
        {
            int removed = 0;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type harmony, kind;
                try { harmony = asm.GetType("HarmonyLib.Harmony", false); kind = asm.GetType("HarmonyLib.HarmonyPatchType", false); }
                catch { continue; }
                if (harmony == null || kind == null) continue;
                try
                {
                    var all = Enum.Parse(kind, "All");
                    var getAll = harmony.GetMethod("GetAllPatchedMethods", BindingFlags.Public | BindingFlags.Static);
                    var getInfo = harmony.GetMethod("GetPatchInfo", BindingFlags.Public | BindingFlags.Static);
                    var unpatch = harmony.GetMethod("Unpatch", new[] { typeof(MethodBase), kind, typeof(string) });
                    var ctor = harmony.GetConstructor(new[] { typeof(string) });
                    var methods = new List<MethodBase>();
                    foreach (MethodBase m in (IEnumerable)getAll.Invoke(null, null)) methods.Add(m);
                    foreach (var m in methods)
                    {
                        var info = getInfo.Invoke(null, new object[] { m });
                        var owners = info != null ? info.GetType().GetProperty("Owners").GetValue(info, null) as IEnumerable : null;
                        if (owners == null) continue;
                        var mine = new List<string>();
                        foreach (string o in owners)
                            if (o != null && o.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) mine.Add(o);
                        foreach (var o in mine)
                        {
                            try { unpatch.Invoke(ctor.Invoke(new object[] { o }), new object[] { m, all, o }); removed++; }
                            catch (Exception e) { Prism.Log("PrismLib: unpatch " + o + " from " + m.Name + " failed: " + Inner(e).Message); }
                        }
                    }
                }
                catch (Exception e) { Prism.Log("PrismLib: Harmony scan failed: " + Inner(e).Message); }
            }
            return removed;
        }

        private static Exception Inner(Exception e) => e is TargetInvocationException && e.InnerException != null ? e.InnerException : e;
    }
}
