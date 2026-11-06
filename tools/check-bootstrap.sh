#!/usr/bin/env bash
# PrismBootstrap's file half, offline: SyncUi and Resolve against a throwaway mods root.
#
#   ./tools/check-bootstrap.sh [path/to/PrismBootstrap.cs]
#
# PrismLib.UI stands on Facet, which ships beside it. A Facet-based PrismLib.UI that SyncUi copies
# into a folder without Facet, or that the loader cannot find Facet for, throws for every Prism
# window. Fake assemblies stand in for the real ones: only names and versions matter here.
set -euo pipefail
cd "$(dirname "$0")/.."
BOOT="${1:-bootstrap/PrismBootstrap.cs}"
W=$(mktemp -d)
trap 'rm -rf "$W"' EXIT

fake() { # out version  (mcs names the assembly after the file)
    mkdir -p "$(dirname "$1")"
    echo "[assembly: System.Reflection.AssemblyVersion(\"$2\")]" > "$W/v.cs"
    mcs -target:library -out:"$1" "$W/v.cs" >/dev/null
}
fake "$W/f/new/PrismLib.UI.dll" 0.3.14.0
fake "$W/f/old/PrismLib.UI.dll" 0.3.12.0
fake "$W/f/Facet.0.1.dll" 0.1.0.0
fake "$W/f/newer/Facet.0.1.dll" 0.1.1.0
fake "$W/f/Facet.0.2.dll" 0.2.0.0
fake "$W/f/PrismLib.dll" 0.3.14.0

R="$W/game/UMMMods"; U="$W/game/UserLibs"; S="$R/PrismLib"
mkdir -p "$R/ModA" "$R/ModB" "$R/ModC" "$R/ModD" "$S" "$U"
cp "$W/f/new/PrismLib.UI.dll" "$R/ModA/"                                  # the newest copy; its Facet arrives in step 2
cp "$W/f/old/PrismLib.UI.dll" "$R/ModB/"                                  # older, no Facet: upgraded
cp "$W/f/new/PrismLib.UI.dll" "$R/ModC/"                                  # same version, no Facet: repaired
cp "$W/f/old/PrismLib.UI.dll" "$R/ModD/"                                  # older UI; a newer Facet arrives in step 3
cp "$W/f/old/PrismLib.UI.dll" "$U/"                                       # a MelonLoader build's copy
cp "$W/f/PrismLib.dll" "$W/f/old/PrismLib.UI.dll" "$W/f/Facet.0.1.dll" "$W/f/Facet.0.2.dll" "$S/"   # shared folder: a stray UI, two Facets it must never serve
mcs -target:library -r:System.dll -out:"$R/ModB/ModB.dll" "$BOOT"
(cd "$S" && md5 -r * 2>/dev/null || md5sum *) > "$W/shared.before"

cat > "$W/t.cs" <<'CS'
using System;
using System.IO;
using System.Reflection;
static class T {
    const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    static int bad;
    static void Check(bool ok, string what) { Console.WriteLine((ok ? "  ok   " : "  FAIL ") + what); if (!ok) bad++; }
    static string Ver(string p) { return File.Exists(p) ? AssemblyName.GetAssemblyName(p).Version.ToString() : "missing"; }
    static int Main(string[] a) {
        string root = a[0], userLibs = a[1], facet = a[2], newer = a[3], facet2 = a[4];
        var boot = Assembly.LoadFrom(Path.Combine(root, "ModB", "ModB.dll")).GetType("PrismLib.Bootstrap.PrismBootstrap", true);
        Func<string, Assembly> resolve = n => (Assembly)boot.GetMethod("Resolve", Any)
            .Invoke(null, new object[] { null, new ResolveEventArgs(n + ", Version=0.0.0.0, Culture=neutral, PublicKeyToken=null") });

        Check(resolve("Facet.0.1") == null && resolve("Facet.0.2") == null, "Resolve never serves Facet from the shared PrismLib folder");
        File.Copy(facet, Path.Combine(root, "ModA", "Facet.0.1.dll"));
        var got = resolve("Facet.0.1");
        Check(got != null && Path.GetFileName(Path.GetDirectoryName(got.Location)) == "ModA",
              "Resolve finds Facet.0.1 in a sibling mod folder (" + (got == null ? "null" : got.Location) + ")");
        File.Copy(facet2, Path.Combine(root, "ModA", "Facet.0.2.dll"));   // ModB still has no Facet
        var got2 = resolve("Facet.0.2");
        Check(got2 != null && got2.GetName().Name == "Facet.0.2" && Path.GetFileName(Path.GetDirectoryName(got2.Location)) == "ModA",
              "Resolve finds Facet.0.2 in ModA, never the shared folder (" + (got2 == null ? "null" : got2.Location) + ")");
        Check(got.GetName().Name == "Facet.0.1" && got.Location != got2.Location,
              "Facet.0.1 and Facet.0.2 resolve to two assemblies from different files");
        File.Copy(newer, Path.Combine(root, "ModD", "Facet.0.1.dll"));

        boot.GetMethod("SyncUi", Any).Invoke(null, null);
        Check(Ver(Path.Combine(root, "ModB", "PrismLib.UI.dll")) == "0.3.14.0", "an older PrismLib.UI is upgraded");
        Check(Ver(Path.Combine(root, "ModB", "Facet.0.1.dll")) == "0.1.0.0", "the upgraded copy gets Facet beside it");
        Check(Ver(Path.Combine(root, "ModC", "Facet.0.1.dll")) == "0.1.0.0", "a same-version copy lacking Facet gets it");
        Check(Ver(Path.Combine(userLibs, "Facet.0.1.dll")) == "0.1.0.0", "so does UserLibs");
        Check(Ver(Path.Combine(root, "ModD", "Facet.0.1.dll")) == "0.1.1.0", "a newer Facet already there is kept");
        foreach (var d in new[] { Path.Combine(root, "ModB"), Path.Combine(root, "ModC"), userLibs })
            Check(Ver(Path.Combine(d, "Facet.0.2.dll")) == "0.2.0.0" && Ver(Path.Combine(d, "Facet.0.1.dll")) == "0.1.0.0",
                  "after the sync " + Path.GetFileName(d) + " holds Facet.0.1 and Facet.0.2");
        Check(Directory.GetFiles(Path.GetDirectoryName(root), "*.new", SearchOption.AllDirectories).Length == 0, "no .new file is left behind");

        var early = (System.Collections.Generic.List<string>)boot.GetField("_early", Any).GetValue(null);
        foreach (var m in early) Console.WriteLine("       log: " + m);
        return bad;
    }
}
CS
mcs -out:"$W/t.exe" "$W/t.cs" >/dev/null
set +e
mono "$W/t.exe" "$R" "$U" "$W/f/Facet.0.1.dll" "$W/f/newer/Facet.0.1.dll" "$W/f/Facet.0.2.dll"
BAD=$?
set -e
(cd "$S" && md5 -r * 2>/dev/null || md5sum *) > "$W/shared.after"
if cmp -s "$W/shared.before" "$W/shared.after"; then echo "  ok   nothing is written to the shared PrismLib folder"
else echo "  FAIL the shared PrismLib folder changed:"; diff "$W/shared.before" "$W/shared.after" || true; BAD=$((BAD + 1)); fi
[ "$BAD" -eq 0 ] && echo "bootstrap: ALL PASS" || { echo "bootstrap: $BAD FAILED"; exit 1; }
