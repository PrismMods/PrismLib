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
trap 'chmod -R u+rwx "$W" 2>/dev/null; rm -rf "$W"' EXIT

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
shared_md5() { (cd "$1" && { md5 -r * 2>/dev/null || md5sum *; }); }
shared_md5 "$S" > "$W/shared.before"

cat > "$W/t.cs" <<'CS'
using System;
using System.IO;
using System.Reflection;
static class T {
    const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    static int bad;
    static void Check(bool ok, string what) { Console.WriteLine((ok ? "  ok   " : "  FAIL ") + what); if (!ok) bad++; }
    static string Ver(string p) { return File.Exists(p) ? AssemblyName.GetAssemblyName(p).Version.ToString() : "missing"; }
    static string Ver(string root, string rel) { return Ver(Path.Combine(root, rel)); }

    // Runtime stores (Quartz): <mods>/<Mod>/Runtime/versions/<ver>/ and <game>/UserData/<Mod>/Runtime/versions/<ver>/,
    // under both mods roots. Its newest copy carries only Facet.0.2, as the real store does.
    static int Store(string root) {
        var boot = Assembly.LoadFrom(Path.Combine(root, "UMMMods", "ModA", "ModA.dll")).GetType("PrismLib.Bootstrap.PrismBootstrap", true);
        var got = (Assembly)boot.GetMethod("Resolve", Any).Invoke(null, new object[] { null,
            new ResolveEventArgs("Facet.0.2, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null") });
        Check(got != null && got.Location.Contains("Runtime"), "Resolve finds Facet.0.2 in a runtime store (" + (got == null ? "null" : got.Location) + ")");
        boot.GetMethod("SyncUi", Any).Invoke(null, null);
        Check(Ver(root, "UMMMods/ModA/PrismLib.UI.dll") == "0.3.14.0", "a mod folder copy is upgraded from a runtime store");
        Check(Ver(root, "UMMMods/ModA/Facet.0.2.dll") == "0.2.0.0", "and gets the store's Facet");
        Check(Ver(root, "UMMMods/Q/Runtime/versions/1.0.0/PrismLib.UI.dll") == "0.3.14.0", "a UMM-side store copy is upgraded");
        Check(Ver(root, "UMMMods/Q/Runtime/versions/1.0.0/Facet.0.2.dll") == "0.2.0.0", "and gets Facet");
        Check(Ver(root, "UserData/Q/Runtime/versions/2.0.0/PrismLib.UI.dll") == "0.3.14.0", "the newest store copy is untouched");
        string empty = Path.Combine(root, "UserData/Q/Runtime/versions/0.9.0");
        Check(Ver(empty, "PrismLib.UI.dll") == "missing" && Ver(empty, "Facet.0.2.dll") == "missing" && Directory.GetFileSystemEntries(empty).Length == 0,
              "a store version without PrismLib.UI is left alone (nothing written into it)");
        Check(Ver(root, "Mods/MlMod/PrismLib.UI.dll") == "0.3.14.0" && Ver(root, "Mods/MlMod/Facet.0.2.dll") == "0.2.0.0",
              "the sibling Mods root's copies meet the UMMMods ones");
        Check(Ver(root, "Mods/Q/Runtime/versions/1.0.0/PrismLib.UI.dll") == "0.3.14.0", "so does a store under the sibling root");
        Check(Directory.GetFiles(root, "*.new", SearchOption.AllDirectories).Length == 0, "no .new file is left behind");
        return bad;
    }

    // A sibling mods root that cannot be listed must not take the primary root down with it.
    static int Unreadable(string root) {
        var boot = Assembly.LoadFrom(Path.Combine(root, "UMMMods", "ModA", "ModA.dll")).GetType("PrismLib.Bootstrap.PrismBootstrap", true);
        var got = (Assembly)boot.GetMethod("Resolve", Any).Invoke(null, new object[] { null,
            new ResolveEventArgs("Facet.0.2, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null") });
        Check(got != null && got.Location.Contains("ModX"), "Resolve still finds the primary root's Facet.0.2 (" + (got == null ? "null" : got.Location) + ")");
        boot.GetMethod("SyncUi", Any).Invoke(null, null);
        Check(Ver(root, "UMMMods/ModB/PrismLib.UI.dll") == "0.3.14.0", "SyncUi still syncs the primary root");
        var early = (System.Collections.Generic.List<string>)boot.GetField("_early", Any).GetValue(null);
        int said = 0; foreach (var m in early) if (m.Contains("cannot list")) said++;
        Check(said == 1, "the bad root is logged once (" + said + ")");
        return bad;
    }

    static int Main(string[] a) {
        if (a[0] == "store") return Store(a[1]);
        if (a[0] == "bad") return Unreadable(a[1]);
        string root = a[1], userLibs = a[2], facet = a[3], newer = a[4], facet2 = a[5];
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
mono "$W/t.exe" base "$R" "$U" "$W/f/Facet.0.1.dll" "$W/f/newer/Facet.0.1.dll" "$W/f/Facet.0.2.dll"
BAD=$?
set -e
shared_md5 "$S" > "$W/shared.after"
if cmp -s "$W/shared.before" "$W/shared.after"; then echo "  ok   nothing is written to the shared PrismLib folder"
else echo "  FAIL the shared PrismLib folder changed:"; diff "$W/shared.before" "$W/shared.after" || true; BAD=$((BAD + 1)); fi

# Scenario store: runtime stores in both layouts, under both mods roots.
G="$W/game2"
V=Runtime/versions
mkdir -p "$G/UMMMods/ModA" "$G/UMMMods/PrismLib" "$G/UMMMods/Q/$V/1.0.0" "$G/UserData/Q/$V/2.0.0" "$G/UserData/Q/$V/0.9.0" \
         "$G/Mods/MlMod" "$G/Mods/Q/$V/1.0.0"
cp "$W/f/old/PrismLib.UI.dll" "$G/UMMMods/ModA/"
cp "$W/f/old/PrismLib.UI.dll" "$G/UMMMods/Q/$V/1.0.0/"
cp "$W/f/new/PrismLib.UI.dll" "$W/f/Facet.0.2.dll" "$G/UserData/Q/$V/2.0.0/"
cp "$W/f/old/PrismLib.UI.dll" "$G/Mods/MlMod/"
cp "$W/f/old/PrismLib.UI.dll" "$G/Mods/Q/$V/1.0.0/"
cp "$W/f/PrismLib.dll" "$G/UMMMods/PrismLib/"
mcs -target:library -r:System.dll -out:"$G/UMMMods/ModA/ModA.dll" "$BOOT"
shared_md5 "$G/UMMMods/PrismLib" > "$W/shared2.before"
set +e
mono "$W/t.exe" store "$G"
B2=$?
set -e
BAD=$((BAD + B2))
shared_md5 "$G/UMMMods/PrismLib" > "$W/shared2.after"
if cmp -s "$W/shared2.before" "$W/shared2.after"; then echo "  ok   the store scenario leaves the shared PrismLib folder alone"
else echo "  FAIL the store scenario changed the shared PrismLib folder"; BAD=$((BAD + 1)); fi

# Scenario bad: the sibling Mods root cannot be listed (chmod 000, restored after).
G="$W/game3"
mkdir -p "$G/UMMMods/ModA" "$G/UMMMods/ModB" "$G/UMMMods/ModX" "$G/Mods/MlMod"
cp "$W/f/old/PrismLib.UI.dll" "$G/UMMMods/ModB/"
cp "$W/f/new/PrismLib.UI.dll" "$W/f/Facet.0.2.dll" "$G/UMMMods/ModX/"
mcs -target:library -r:System.dll -out:"$G/UMMMods/ModA/ModA.dll" "$BOOT"
chmod 000 "$G/Mods"
set +e
mono "$W/t.exe" bad "$G"
B3=$?
set -e
chmod 755 "$G/Mods"
BAD=$((BAD + B3))
[ "$BAD" -eq 0 ] && echo "bootstrap: ALL PASS" || { echo "bootstrap: $BAD FAILED"; exit 1; }
