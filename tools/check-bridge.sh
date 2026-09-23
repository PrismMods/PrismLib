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
DLL="${1:?usage: check-bridge.sh <Mod.dll> <Namespace.PrismBridge>}"
TYPE="${2:?usage: check-bridge.sh <Mod.dll> <Namespace.PrismBridge>}"

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
            Console.WriteLine("OK: " + a[1] + " loads without PrismLib (Available = " + v + ")");
            return 0;
        } catch (Exception e) {
            var i = e.InnerException ?? e;
            Console.WriteLine("FAIL: " + i.GetType().Name + " - " + i.Message.Split('\n')[0]);
            Console.WriteLine("      A PrismLib type is reachable from the type's own layout — check for a typed field.");
            return 1;
        }
    }
}
CS
mcs -r:System.dll -out:"$WORK/probe.exe" "$WORK/probe.cs"
cp "$DLL" "$WORK/mod.dll"
MONO_PATH="$WORK/refs" mono "$WORK/probe.exe" "$WORK/mod.dll" "$TYPE"
