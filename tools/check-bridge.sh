#!/usr/bin/env bash
# Does a mod's PrismLib bridge still load when PrismLib is NOT installed?
#
#   ./tools/check-bridge.sh <path/to/Mod.dll> <Namespace.PrismBridge>
#
# Run it on the BUILT mod before shipping. The failure this catches is silent to the player and
# near-silent in the log: UMM reports it as "OnToggle: TypeLoadException" and the whole mod is
# skipped. It happened for real — a `PrismLib.ModHandle _me` field is part of the class layout, so
# the type could not load, so Ensure() never got the chance to install the library it needed.
set -euo pipefail
cd "$(dirname "$0")/.."
DLL="${1:?usage: check-bridge.sh <Mod.dll> <Namespace.PrismBridge> [Method ...]}"
TYPE="${2:?usage: check-bridge.sh <Mod.dll> <Namespace.PrismBridge> [Method ...]}"
shift 2
# Methods the mod calls WITHOUT checking Available. Each is JIT-compiled here: if one mentions a
# PrismLib.dll type it fails now instead of taking the mod down on a machine that never installed
# the library. Methods that legitimately touch PrismLib are guarded and must NOT be listed.
UNGATED="$*"

GAME="${ADOFAI_ROOT:-$HOME/Library/Application Support/Steam/steamapps/common/A Dance of Fire and Ice}"
MANAGED="$GAME/ADanceOfFireAndIce.app/Contents/Resources/Data/Managed"
[ -d "$MANAGED" ] || MANAGED="$GAME/ADanceOfFireAndIce_Data/Managed"

WORK=$(mktemp -d)
trap 'rm -rf "$WORK"' EXIT
# Unity and game assemblies only. Putting Managed/ itself on MONO_PATH swaps in the game's
# mscorlib and the runtime dies before the test runs.
mkdir -p "$WORK/refs"
for f in "$MANAGED"/Unity*.dll "$MANAGED"/Assembly-CSharp*.dll "$MANAGED"/netstandard.dll; do
    [ -f "$f" ] && ln -sf "$f" "$WORK/refs/"
done
[ -f "$GAME/MelonLoader/net472/0Harmony.dll" ] && ln -sf "$GAME/MelonLoader/net472/0Harmony.dll" "$WORK/refs/"

cat > "$WORK/probe.cs" <<'CS'
using System;
using System.Reflection;
static class Probe {
    const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    static int Main(string[] a) {
        try {
            var t = Assembly.LoadFrom(a[0]).GetType(a[1], true);
            var p = t.GetProperty("Available", Any);
            object v = p != null ? p.GetValue(null, null) : "<none>";
            foreach (var f in t.GetFields(Any)) f.GetValue(null);   // forces the full layout

            int bad = 0;
            for (int i = 2; i < a.Length; i++)
            {
                var m = t.GetMethod(a[i], Any);
                if (m == null) { Console.WriteLine("  ?  no such method: " + a[i]); bad++; continue; }
                try
                {
                    System.Runtime.CompilerServices.RuntimeHelpers.PrepareMethod(m.MethodHandle);
                    Console.WriteLine("  ok   " + a[i] + " JITs without PrismLib");
                }
                catch (Exception me)
                {
                    var mi = me.InnerException ?? me;
                    Console.WriteLine("  FAIL " + a[i] + " needs PrismLib to JIT: " + mi.Message.Split('\n')[0]);
                    bad++;
                }
            }
            if (bad > 0) return 1;
            Console.WriteLine("OK: " + a[1] + " loads without PrismLib (Available = " + v + ")");
            return 0;
        } catch (Exception e) {
            var i = e.InnerException ?? e;
            Console.WriteLine("FAIL: " + i.GetType().Name + " - " + i.Message.Split('\n')[0]);
            Console.WriteLine("      A PrismLib.dll type is reachable from the type's own layout — check for a typed field.");
            return 1;
        }
    }
}
CS
mcs -r:System.dll -out:"$WORK/probe.exe" "$WORK/probe.cs"
cp "$DLL" "$WORK/mod.dll"
# Model the real mod folder: PrismLib.UI SHIPS beside the mod, PrismLib.dll does not (the
# bootstrapper installs that one, and may fail to). Copying the UI half keeps this a test of the
# guard rules rather than a false alarm about a dependency that is always present.
for sib in "$(dirname "$DLL")"/PrismLib.UI.dll; do
    [ -f "$sib" ] && cp "$sib" "$WORK/"
done
MONO_PATH="$WORK/refs" mono "$WORK/probe.exe" "$WORK/mod.dll" "$TYPE" $UNGATED
