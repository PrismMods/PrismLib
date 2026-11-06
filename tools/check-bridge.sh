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
# Methods the mod calls WITHOUT checking Available. Each is IL-scanned (every token resolved with
# PrismLib.dll absent) and JIT-compiled here: if one mentions a
# PrismLib.dll type it fails now instead of taking the mod down on a machine that never installed
# the library. Methods that legitimately touch PrismLib are guarded and must NOT be listed.
UNGATED="$*"

# macOS, then Linux's two common Steam library locations — whichever exists. ADOFAI_ROOT
# overrides all of them.
if [ -z "${ADOFAI_ROOT:-}" ]; then
    for p in \
        "$HOME/Library/Application Support/Steam/steamapps/common/A Dance of Fire and Ice" \
        "$HOME/.local/share/Steam/steamapps/common/A Dance of Fire and Ice" \
        "$HOME/.steam/steam/steamapps/common/A Dance of Fire and Ice"
    do [ -d "$p" ] && { ADOFAI_ROOT="$p"; break; }; done
fi
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
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
static class Probe {
    const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    // Mono 6.14.1's PrepareMethod defers missing types to run time, so the JIT step above passes
    // methods that name a PrismLib type. Resolve every token in the IL instead: PrismLib.dll is
    // absent here, so anything that touches it throws. Returns null if clean, else the culprit.
    static string IlScan(MethodBase m, string dll, HashSet<MethodBase> seen = null) {
        int tok = 0;
        seen = seen ?? new HashSet<MethodBase>();
        if (!seen.Add(m)) return null;
        try {
            foreach (var p in m.GetParameters()) { var _ = p.ParameterType.FullName; }
            if (m is MethodInfo mi) { var _ = mi.ReturnType.FullName; }
            var body = m.GetMethodBody(); if (body == null) return null;
            foreach (var l in body.LocalVariables) { var _ = l.LocalType.FullName; }
            var ops = new Dictionary<short, OpCode>();
            foreach (var f in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)) { var o = (OpCode)f.GetValue(null); ops[o.Value] = o; }
            byte[] il = body.GetILAsByteArray();
            for (int pc = 0; pc < il.Length;) {
                short v = il[pc] == 0xFE ? (short)(0xFE00 | il[pc + 1]) : il[pc];
                OpCode op = ops[v]; pc += op.Size;
                switch (op.OperandType) {
                    case OperandType.InlineMethod: case OperandType.InlineField: case OperandType.InlineType: case OperandType.InlineTok: {
                        tok = BitConverter.ToInt32(il, pc);
                        var r = m.Module.ResolveMember(tok, null, null);
                        if (r is MethodBase rm) { foreach (var p in rm.GetParameters()) { var _ = p.ParameterType.FullName; } if (rm is MethodInfo rmi) { var _ = rmi.ReturnType.FullName; } }
                        if (r.DeclaringType != null && r.DeclaringType.IsNested && Outer(r.DeclaringType) == Outer(m.DeclaringType)) Used.Add(r.DeclaringType);
                        // Lambda bodies live in compiler-made nested types: follow calls into them.
                        if (r is MethodBase nm && nm.DeclaringType != null && (nm.DeclaringType.IsNested || nm.Name[0] == '<') && Outer(nm.DeclaringType) == Outer(m.DeclaringType)) {
                            var inner = IlScan(nm, dll, seen);
                            if (inner != null) return "(via " + nm.DeclaringType.Name + "." + nm.Name + ") " + inner;
                        }
                        if (r is FieldInfo rf) { var _ = rf.FieldType.FullName; }
                        pc += 4; break; }
                    case OperandType.InlineSwitch: pc += 4 + 4 * BitConverter.ToInt32(il, pc); break;
                    case OperandType.InlineNone: break;
                    case OperandType.ShortInlineBrTarget: case OperandType.ShortInlineI: case OperandType.ShortInlineVar: pc += 1; break;
                    case OperandType.InlineVar: pc += 2; break;
                    case OperandType.InlineI8: case OperandType.InlineR: pc += 8; break;
                    default: pc += 4; break;
                }
            }
            return null;
        } catch (Exception e) {
            var x = e.InnerException ?? e;
            return Name(dll, tok) + " (" + x.Message.Split('\n')[0] + ")";
        }
    }
    static readonly HashSet<Type> Used = new HashSet<Type>();   // nested types the ungated methods touch
    static Type Outer(Type t) { while (t.DeclaringType != null) t = t.DeclaringType; return t; }
    // Name a memberref/typeref token the way monodis prints it; falls back to the raw token.
    static string Name(string dll, int tok) {
        if (tok == 0) return "a parameter, return or local type";
        string hex = "token 0x" + tok.ToString("X8");
        int table = (int)((uint)tok >> 24), row = tok & 0xFFFFFF;
        if (table != 0x0A && table != 0x01) return hex;
        try {
            var psi = new System.Diagnostics.ProcessStartInfo("monodis", (table == 0x0A ? "--memberref " : "--typeref ") + "\"" + dll + "\"") { RedirectStandardOutput = true, UseShellExecute = false };
            using (var p = System.Diagnostics.Process.Start(psi)) {
                string o = p.StandardOutput.ReadToEnd(); p.WaitForExit();
                var lines = o.Split('\n');
                for (int k = 0; k < lines.Length; k++)
                    if (lines[k].StartsWith(row + ": "))
                        return table == 0x0A ? lines[k + 1].Trim().Replace("Resolved: ", "") : lines[k].Substring((row + ": ").Length).Trim();
            }
        } catch (Exception) { }
        return hex;
    }
    static int Main(string[] a) {
        try {
            // Facet SHIPS beside the mod like PrismLib.UI.dll: it must load by name from there.
            var fn = Environment.GetEnvironmentVariable("FACET");
            var fa = Assembly.Load(fn);
            fa.GetType("Facet.Theme", true);
            Console.WriteLine("  ok   " + fn + " loads from " + System.IO.Path.GetDirectoryName(fa.Location));
            var t = Assembly.LoadFrom(a[0]).GetType(a[1], true);
            var p = t.GetProperty("Available", Any);
            object v = p != null ? p.GetValue(null, null) : "<none>";
            foreach (var f in t.GetFields(Any)) f.GetValue(null);   // forces the full layout

            int bad = 0;
            for (int i = 2; i < a.Length; i++)
            {
                var m = t.GetMethod(a[i], Any);
                if (m == null) { Console.WriteLine("  ?  no such method: " + a[i]); bad++; continue; }
                string il = IlScan(m, a[0]);
                if (il != null) { Console.WriteLine("  FAIL " + a[i] + " references PrismLib at the IL level: " + il); bad++; continue; }
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
            // Compiler-made nested types the ungated methods use (<>c lambda caches, display
            // classes): a PrismLib-typed field there is the same landmine as one on the bridge
            // itself. Gated helpers' display classes legitimately hold PrismLib types, so only the
            // touched ones count.
            foreach (var n in Used)
                foreach (var f in n.GetFields(Any | BindingFlags.Instance))
                    try { var _ = f.FieldType.FullName; }
                    catch (Exception fe) {
                        Console.WriteLine("  FAIL " + n.Name + "." + f.Name + " has a type that needs PrismLib: " + (fe.InnerException ?? fe).Message.Split('\n')[0]);
                        bad++;
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
# The Facet minor PrismLib.UI is built on: the mod folder's copy if it has one, else lib/'s.
FACET=$(sed -n 's:.*<Reference Include="\(Facet\.[0-9]*\.[0-9]*\)".*:\1:p' PrismLib.UI/PrismLib.UI.csproj)
[ -n "$FACET" ] || { echo "no Facet reference in PrismLib.UI/PrismLib.UI.csproj" >&2; exit 1; }
for f in "$(dirname "$DLL")/$FACET.dll" "lib/$FACET.dll"; do
    [ -f "$f" ] && { cp "$f" "$WORK/"; break; }
done
FACET="$FACET" MONO_PATH="$WORK/refs" mono "$WORK/probe.exe" "$WORK/mod.dll" "$TYPE" $UNGATED
