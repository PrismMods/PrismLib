# Plan 9: Quartz joins PrismLib — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Quartz becomes a full Prism mod (bridge, shipped PrismLib, debug window, Prism · Settings, MacKeys, one layout target per on-screen element) while its own menus look and behave as before.

**Architecture:** PrismLib's bootstrap learns that a versioned runtime store (`…/Runtime/versions/<ver>/`) holds a PrismLib.UI copy like a mod folder does, so "first loaded wins" stays "newest wins" with Quartz installed. Quartz ships `PrismLib.bundle` + `PrismLib.UI.dll` beside `Quartz.dll` in that store and reaches both through one door, `Quartz/Prism/PrismBridge.cs` (FacetBridge's NoInlining shape). Every element the old reorganizer moves is tagged with a layout id; one generic adapter turns each tagged `ReorganizeHandle` into a `LayoutTarget`, and a new "arranging" state shows the overlays while any shared-editor session reads their boxes.

**Tech Stack:** C# `netstandard2.1` (Quartz, `dotnet build`, `TreatWarningsAsErrors`, `AnalysisLevel latest`), PrismLib (`xbuild`/`mcs`/`mono`), Unity 6 Mono, MelonLoader + UMM (UMMBridge on the rig), HarmonyX, UI Toolkit through PrismLib.UI on `Facet.0.1.dll`, bash.

**Spec:** `/Users/preluminance/Coding/PrismMods/PrismLib/docs/superpowers/specs/2026-11-07-prism-suite-ui-design.md` ("Quartz joining PrismLib", decisions 9, 12, 13, "Constraints", "Testing", calls 6 and 11). Rules: `PrismLib/CLAUDE.md`; Quartz's `AGENTS.md` + `agents/*.md`; plan 6 rulings P0–P27 (`…/side-session-notes/local_6fbd84b6-…/quartz-facet-page-ledger.md`).

Paths: **Q** = `/Users/preluminance/Coding/PrismMods/Quartz/Quartz-facet` (branch `facet-preview`), **P** = `/Users/preluminance/Coding/PrismMods/PrismLib` (branch `main`), **G** = `$HOME/Library/Application Support/Steam/steamapps/common/A Dance of Fire and Ice`.

## Global Constraints

- Quartz commits follow Q's `agents/commits.md`: Conventional, split finely, Lang keys hunk-split into their feature's commit. **No `Co-Authored-By` trailer, no AI attribution anywhere.** Lang commits stay local: tell the user they are pending i18n sync.
- Push nothing, anywhere. Quartz work happens only in Q on `facet-preview`; never touch the main checkout `/Users/preluminance/Coding/PrismMods/Quartz/Quartz`.
- PrismLib changes are commits on P `main`, same rules (P's `agents/commits.md`). Generic PrismLib code only: no Quartz code goes into PrismLib or Facet (GPL-3.0 vs MIT).
- Versions and releases are the user's call: do not bump `Prism.Version`, either `AssemblyInfo`, Quartz's `Info.cs` or `build.json`; do not run either `release.sh`. A Quartz release with this work needs a PrismLib release (Layout + Facet in the feed) first.
- Every user-facing string goes through Quartz's Lang JSON: `Quartz/Resource/Export/Lang/en-US.json` and `ko-KR.json` authored together, `zh-CN.json` untouched, new prefix `PRISM_` listed in `KEY_PREFIXES.md`. Debug-window field names and values are diagnostics (like the log lines) and stay raw.
- Before any AutoInstall build: `cp -R "$G/UserData/Quartz/Runtime" "$HOME/QuartzRuntime-backup-$(date +%Y%m%d-%H%M%S)"` (and `UMMMods/Quartz` when a UMM build installs).
- One in-game test at a time. Never answer Steam's cloud-sync dialog (the user clicks Play), never take screenshots, never quit a game you did not launch (other sessions use this rig).
- Bridge landmines (PrismLib/CLAUDE.md, made stricter because Quartz's PrismLib.UI is optional): no `PrismLib` **or** `PrismLib.UI` type in any field of `PrismBridge`; PrismLib types appear only in method bodies under `Quartz/Prism/`, run only after `Available` (PrismLib.dll) or `UiAvailable` (PrismLib.UI); everything else in Quartz calls `PrismBridge`'s wrappers.
- PrismLib.dll ships as `PrismLib.bundle`, never `.dll`, in the store (PayloadLoader resolves any `<name>.dll` there). PrismLib.UI and Facet ship only in the store: never in `UserLibs/` or Quartz's mod folder (P3).
- Quartz conventions: files ≤ 500 lines, no silent catch (log it or `Diag.Ignore(e)`), namespace = folder, `python3 scripts/check_conventions.py` passes, zero build warnings.
- IL2CPP configs and the KeyViewer flavour still build and ship no PrismLib (P14). Prism's windows stay Unity 6 only (`FacetBridge.IsUnity6`).
- No plan 7/8 API; Facet stays `Facet.0.1`. Nothing in Quartz's menus changes for players; the only new menu behaviour is "the hotkey is ignored while a shared layout session is open" (Task 6).
- Commands: `scripts/test.sh`, `scripts/build.sh <Config> <ML|UMM>` (P2), PrismLib `./dev.sh`, `tools/check-bridge.sh`, `tools/check-bootstrap.sh`.

## Review Focus

1. **PrismLib.UI absent or unloadable** (KeyViewer flavour, a missing file, a Unity 2022 build, an older copy loaded first): Quartz loads, its menu and hotkey work, one "PrismLib.UI unavailable" line, no per-frame exception. Pinned by `check-bridge.sh` with both PrismLib DLLs absent (Task 3 Step 6, Task 8 Step 4).
2. **Rebinding the menu hotkey onto a Prism chord (Alt+D/P/T/C) or a bare modifier** from Prism · Settings: refused, old binding kept, row shows the old value. Pinned by `PrismRulesTests.TestHotkeyRefusals` and the self-test (Task 5).
3. **An element that cannot move right now** (overlay not built, ProgressBar in Line style, a Panel deleted mid-session): its frame is hidden (`GetBox` null), never reported "skipped". Pinned by self-test (Task 7 Step 6).
4. **A shared-editor move followed at once by the session ending**: the move is still in the settings field the reorganizer reads. Pinned by self-test (Task 7 Step 6).
5. **Two editors at once** (reorganize mode on when a shared session starts; the hotkey pressed during one): only one is ever live. Pinned by self-test (Task 6 Step 6).

## What plan 10 consumes

- **Experimental switch:** `CoreSettings.ExperimentalNewMenu` (`bool`, default `false`, JSON key `"ExperimentalNewMenu"` in Quartz's core settings). Read `MainCore.Conf.ExperimentalNewMenu`; write it, then `MainCore.ConfMgr.RequestSave()`. No change event: read it when the hotkey fires. Prism · Settings row id `quartz.experimental.newMenu`; Lang keys `PRISM_NEW_MENU` / `PRISM_NEW_MENU_TIP` for plan 10's row in the old Settings page (call 6).
- **Hotkey:** still `Conf.ToggleModifier` + `Conf.ToggleKey`, decided in `UICore.ToggleHotkeyAllowed(bool modHeld, bool down)` (now `internal`) inside `UICore.HandleUpdate`, which runs under `InputGate.Exempt` and reads Option chords through MacKeys. Plan 10 branches there.
- **`Quartz.Prism.PrismBridge`** (internal static): `bool Available`, `bool UiAvailable`, `void OpenLayoutEditor()`, `void TogglePrismSettings()`, `void ToggleDebug()`, `void Exempt(Action)`, `bool TryPhysical(KeyCode, out bool)`.
- **Arranging state:** `UICore.SharedLayoutOpen`, `UICore.IsArranging`, `event Action<bool> UICore.OnArrangingChanged`.
- **Layout ids:** `Quartz.UI.Utility.LayoutIds` constants and `ReorganizeHandle.FindLayout(string)`, to decide which feature pages get "Edit layout →".
- **Self-test runner:** `new FacetSelfTest(string name, Func<Action<string, bool, string>, Action<string>, IEnumerator> body)`; marker `<data root>/<name>`, log `<data root>/<name>.log`.

## In-game self-test run (used by Tasks 3–8)

```bash
G="$HOME/Library/Application Support/Steam/steamapps/common/A Dance of Fire and Ice"; D="$G/UserData/Quartz"
pgrep -f ADanceOfFireAndIce >/dev/null && { echo "game running: wait, do not quit it"; exit 1; }
cp -R "$D/Runtime" "$HOME/QuartzRuntime-backup-$(date +%Y%m%d-%H%M%S)"
scripts/build.sh Debug ML
rm -f "$D/prism-selftest.log"; touch "$D/prism-selftest"
open steam://rungameid/977950        # a cloud-sync dialog is the user's to answer
# poll every 10 s, up to 5 min, until the log holds DONE; the game quits itself
grep -E '^(FAIL|INFO|DONE)' "$D/prism-selftest.log"; rm -f "$D/prism-selftest"
```

Expected: no `FAIL` line; last line `DONE pass=<n> fail=0`. Keep the game window focused (Key Viewer captures only while focused).

---

### Task 1: The bootstrap treats runtime stores as PrismLib.UI copies (PrismLib)

**Files:**
- Modify: `P/bootstrap/PrismBootstrap.cs` (`SyncUi`, new private `StoreCopies`)
- Modify: `P/tools/check-bootstrap.sh`
- Modify: `P/dev.sh`, `P/CLAUDE.md`

**Interfaces:**
- Produces: `PrismBootstrap.SyncUi()` also lists every `<modsRoot>/<Mod>/Runtime/versions/<ver>/PrismLib.UI.dll` and `<modsRoot>/../UserData/<Mod>/Runtime/versions/<ver>/PrismLib.UI.dll` as copies, with today's rules unchanged: the newest replaces every older copy, never a downgrade or an equal version, each copy's folder gets every newer `Facet.<maj>.<min>.dll` from beside the newest, the shared `PrismLib` folder is never read or written. `Ensure`/`Resolve` signatures unchanged. Task 2 vendors this exact file.

- [ ] **Step 1: Add a second scenario to `check-bootstrap.sh`**

The C# test program takes a scenario name as `args[0]`; `base` is today's run, unchanged. Scenario `store` builds a fresh root `$W/game2` and compiles the bootstrap into `UMMMods/ModA/ModA.dll`:

| Path under `$W/game2` | PrismLib.UI | Facet beside |
|---|---|---|
| `UMMMods/ModA/` | 0.3.12 | none |
| `UMMMods/Q/Runtime/versions/1.0.0/` | 0.3.12 | none |
| `UserData/Q/Runtime/versions/2.0.0/` | 0.3.14 (newest) | `Facet.0.1.dll` 0.1.0.0 |
| `UserData/Q/Runtime/versions/0.9.0/` | none (empty dir) | none |
| `UMMMods/PrismLib/` | `PrismLib.dll` 0.3.14 (md5 before/after) | — |

```csharp
Check(Ver(root, "UMMMods/ModA/PrismLib.UI.dll") == "0.3.14.0", "a mod folder copy is upgraded from a runtime store");
Check(Ver(root, "UMMMods/ModA/Facet.0.1.dll") == "0.1.0.0", "and gets the store's Facet");
Check(Ver(root, "UMMMods/Q/Runtime/versions/1.0.0/PrismLib.UI.dll") == "0.3.14.0", "a UMM-side store copy is upgraded");
Check(Ver(root, "UMMMods/Q/Runtime/versions/1.0.0/Facet.0.1.dll") == "0.1.0.0", "and gets Facet");
Check(Ver(root, "UserData/Q/Runtime/versions/0.9.0/PrismLib.UI.dll") == "missing", "a store version without PrismLib.UI is left alone");
Check(Directory.GetFiles(root, "*.new", SearchOption.AllDirectories).Length == 0, "no .new file is left behind");
```
plus the shell's md5 comparison of `UMMMods/PrismLib/` for this root.

- [ ] **Step 2: Run it to see it fail**

Run: `cd P && ./tools/check-bootstrap.sh`
Expected: the `base` checks pass; `store` prints `FAIL a mod folder copy is upgraded from a runtime store`; last line `bootstrap: N FAILED`.

- [ ] **Step 3: Implement `StoreCopies()` and add its result to `SyncUi`'s `copies`**

Glob the two patterns above (skip `IsShared` folders, skip a missing `UserData`), each wrapped like the rest of `SyncUi` (no throw). Comment: why stores count, and that the store layout is Quartz's but the rule is generic.

- [ ] **Step 4: Run it to see it pass**

Run: `cd P && ./tools/check-bootstrap.sh`
Expected: every line `ok`, last line `bootstrap: ALL PASS`.

- [ ] **Step 5: `dev.sh` refreshes store copies**

After the mod-folder loop, copy `$UIDLL` and `lib/Facet.0.1.dll` over every existing `"$MODS"/*/Runtime/versions/*/PrismLib.UI.dll` and `"$GAME"/UserData/*/Runtime/versions/*/PrismLib.UI.dll` (only where one exists), printing `installed <path>`.
Run: `bash -n dev.sh && ./dev.sh`
Expected: today's output plus no error (no store copy exists until Task 2 installs one).

- [ ] **Step 6: Document it in `P/CLAUDE.md`**

In "Both loaders" and the Facet "Shipping"/"The bootstrap carries Facet along" paragraphs: runtime stores count as copy folders (both layouts); Quartz ships `PrismLib.bundle` + `PrismLib.UI.dll` + `Facet.0.1.dll` only in its store and refreshes `lib/` with `tools/update-prismlib.sh` (its `lib/` is gitignored); `dev.sh` refreshes store copies. Topaz, Sapphire and CamDisplay see stores once they next copy this bootstrap (not in this plan; until then Quartz's own sync converges every copy one launch later).

- [ ] **Step 7: Commit (P main)**

```bash
git add bootstrap/PrismBootstrap.cs tools/check-bootstrap.sh && git commit -m "feat: sync PrismLib.UI into versioned runtime stores"
git add dev.sh && git commit -m "chore: refresh runtime-store copies in dev.sh"
git add CLAUDE.md && git commit -m "docs: note runtime stores and Quartz in the copy rules"
```

---

### Task 2: Quartz ships PrismLib in its runtime store

**Files:**
- Create: `Q/tools/update-prismlib.sh`, `Q/vendor/PrismBootstrap.cs`
- Modify: `Q/Quartz/Quartz.csproj`, `Q/tools/release.sh`, `Q/THIRD-PARTY-NOTICES.md`

**Interfaces:**
- Consumes: Task 1's `bootstrap/PrismBootstrap.cs`.
- Produces: `lib/PrismLib.dll`, `lib/PrismLib.UI.dll` (compile references, `Private=false`); `PrismLib.Bootstrap.PrismBootstrap` compiled into Quartz; every Full non-IL2CPP package holds `Runtime/versions/<ver>/{Quartz.dll|QuartzUmm.dll, PrismLib.UI.dll, PrismLib.bundle, Facet.0.1.dll}`.

- [ ] **Step 1: Write `tools/update-prismlib.sh`**

`/lib/*` is gitignored in Quartz, so the script lives in `tools/` beside `update-facet.sh` and writes into `lib/`. Default mode is Topaz's `lib/update-prismlib.sh` verbatim in behaviour (feed `prismlib.json`, SHA-256 per file, Facet when the feed lists one, all-or-nothing swap). `DEV=1` instead copies `PrismLib/bin/Release/PrismLib.dll` and `PrismLib.UI/bin/Release/PrismLib.UI.dll` from `${PRISMLIB_ROOT:-<main checkout>/../../PrismLib}` (main checkout found as `update-facet.sh` finds it), refuses with "run ./dev.sh in PrismLib first" when either is missing, leaves Facet to `update-facet.sh`, prints each file's md5.
Run: `bash -n tools/update-prismlib.sh && DEV=1 tools/update-prismlib.sh && cmp lib/PrismLib.UI.dll ../../PrismLib/PrismLib.UI/bin/Release/PrismLib.UI.dll && echo same`
Expected: two md5 lines, then `same`. (Feed mode is not run here: it would put the published 0.3.12, which lacks `Layout`, into `lib/`.)

- [ ] **Step 2: Vendor the bootstrap**

`mkdir -p vendor && cp ../../PrismLib/bootstrap/PrismBootstrap.cs vendor/` — never edited here. It lives outside `Quartz/` because Quartz's conventions check (500 lines, no silent catch) would reject it.
Run: `cmp vendor/PrismBootstrap.cs ../../PrismLib/bootstrap/PrismBootstrap.cs && echo same` → `same`.

- [ ] **Step 3: `Quartz.csproj`**

- In the `!IL2CPP` reference group: `PrismLib` and `PrismLib.UI` with HintPath `$(LibDir)/…`, beside `Facet.0.1`.
- `<Compile Include="../vendor/PrismBootstrap.cs" Link="Prism/PrismBootstrap.cs" />` under `!IL2CPP`. If it trips a warning under `TreatWarningsAsErrors`, add that ID to `<NoWarn>` with a comment naming the vendored file; never edit the file.
- Properties `PrismLibUiDll=$(LibDir)/PrismLib.UI.dll` and `PrismLibDll=$(LibDir)/PrismLib.dll`, both emptied under IL2CPP or `Flavor=KeyViewer`, exactly like `FacetDll`.
- Every `Copy` that lists `$(FacetDll)` (ML AutoInstall, ML stage, UMM stage, UMM AutoInstall) also lists `$(PrismLibUiDll)`; beside each, `<Copy Condition="'$(PrismLibDll)' != '' And …" SourceFiles="$(PrismLibDll)" DestinationFiles="<same version dir>/PrismLib.bundle" />`.
- Target `CheckPrismLib` (`BeforeTargets="ResolveAssemblyReferences"`, skipped when `UseStubs` or IL2CPP): `<Error>` "lib/PrismLib.dll or lib/PrismLib.UI.dll is missing: run tools/update-prismlib.sh (DEV=1 for a local PrismLib build)".

- [ ] **Step 4: Build and inspect the packages (no AutoInstall)**

```bash
dotnet build Quartz/Quartz.csproj -c Debug -p:AutoInstall=false
dotnet build Quartz/Quartz.csproj -c Debug -p:LoaderTarget=UMM -p:AutoInstall=false
dotnet build Quartz/Quartz.csproj -c Debug -p:Flavor=KeyViewer -p:AutoInstall=false
dotnet build Quartz/Quartz.csproj -c Debug_IL2CPP -p:AutoInstall=false
for z in dist/Quartz.zip dist/QuartzUmm.zip; do unzip -l $z | grep -E 'Runtime/versions/[^/]+/(PrismLib\.UI\.dll|PrismLib\.bundle|Facet\.0\.1\.dll)$' | wc -l; unzip -l $z | grep -c 'PrismLib\.dll$'; done
unzip -l dist/QuartzKeyViewer.zip | grep -c PrismLib
```
Expected: every build `0 Error(s)`; then `3`, `0`, `3`, `0`, `0`. (If the IL2CPP config cannot build on this machine at all, say so; P14 built it.)

- [ ] **Step 5: Release script and notices**

`tools/release.sh`: run `tools/update-prismlib.sh` right after `tools/update-facet.sh` (feed mode: ships the published PrismLib and the feed's Facet when it lists one; a build needing an unreleased PrismLib API fails there, which is the rule). `THIRD-PARTY-NOTICES.md`: a "PrismLib" entry (MIT, PrismMods: vendored `PrismBootstrap.cs`, shipped `PrismLib.dll` as `PrismLib.bundle` and `PrismLib.UI.dll`) and a "Facet" entry (MIT, PrismMods: shipped `Facet.0.1.dll`), in the file's existing format.
Run: `bash -n tools/release.sh && python3 scripts/check_conventions.py` → no output from bash, every line `ok:`.

- [ ] **Step 6: Commit (Q)**

```bash
git add tools/update-prismlib.sh && git commit -m "chore: add a PrismLib update script"
git add vendor/PrismBootstrap.cs Quartz/Quartz.csproj THIRD-PARTY-NOTICES.md && git commit -m "build: ship PrismLib with the runtime payload"
git add tools/release.sh && git commit -m "chore: refresh PrismLib before a release build"
```

---

### Task 3: PrismBridge loads PrismLib and registers Quartz

**Files:**
- Create: `Q/Quartz/Prism/PrismBridge.cs`, `PrismRules.cs`, `PrismGate.cs`, `PrismSelfTest.cs`, `PrismSelfTestChecks.cs`
- Modify: `Q/Quartz/Core/QuartzRuntime.cs`, `Q/Quartz/Core/Service/UIService.cs`, `Q/Quartz/Compat/QuartzLogger.cs`, `Q/Quartz/UI/FacetUi/FacetSelfTest.cs`, `Q/Quartz/Quartz.csproj` (stubs build also removes `Prism/PrismSelfTest*.cs`)
- Test: `Q/Quartz.Tests/Core/PrismRulesTests.cs`, `Q/Quartz.Tests/Compat/QuartzLoggerTests.cs`, `Q/Quartz.Tests/Program.cs`, `Q/Quartz.Tests/Quartz.Tests.csproj` (link `../Quartz/Prism/PrismRules.cs`)

**Interfaces:**
- Consumes: Task 2's references and vendored `PrismBootstrap.Ensure(Action<string>, Version, string modsRoot, string bundle)`.
- Produces (namespace `Quartz.Prism`, all `internal static`):
  - `PrismBridge`: `bool Available { get; private set; }` (PrismLib.dll wired), `bool UiAvailable { get; }` (probed once: `FacetBridge.Available()` and PrismLib.UI loads), `void Init(IQuartzHost host)`, `void Register()`, `void Tick()`, `void Shutdown()`, `void Exempt(Action reads)`, `bool TryPhysical(KeyCode key, out bool held)`.
  - `PrismRules` (BCL only): `string ModsRoot(string hostModsPath, bool umm)`, `IReadOnlyList<string> UiCopies(string gameRoot)`, `int ToPrismMods(int keybindModifier)`.
  - `PrismGate`: `void Tick()`, `void Shutdown()`.
  - `QuartzLogger.Recent()` → `string[]` (newest last, at most `QuartzLogger.RecentMax = 1000`), `QuartzLogger.ClearRecent()`.
  - `FacetSelfTest(string name = "facet-selftest", Func<Action<string, bool, string>, Action<string>, IEnumerator> body = null)`; `PrismSelfTest.Run(check, info)`.

- [ ] **Step 1: Write the failing offline tests**

```csharp
static class PrismRulesTests {
    public static void TestModsRoot() {
        string g = Path.Combine(Path.GetTempPath(), "g");
        Assert(PrismRules.ModsRoot(Path.Combine(g, "UMMMods", "Quartz") + Path.DirectorySeparatorChar, umm: true) == Path.Combine(g, "UMMMods"), "UMM: the mod folder's parent");
        Assert(PrismRules.ModsRoot(Path.Combine(g, "UMMMods", "Quartz"), umm: true) == Path.Combine(g, "UMMMods"), "UMM without a trailing separator");
        Assert(PrismRules.ModsRoot(Path.Combine(g, "Mods"), umm: false) == Path.Combine(g, "Mods"), "ML: the Mods folder itself");
    }
    public static void TestUiCopies() {
        // temp game root with these files (each a zero-byte stand-in):
        //   Mods/PrismLib/PrismLib.UI.dll                       shared folder: excluded
        //   UMMMods/Topaz/PrismLib.UI.dll                       included
        //   UMMMods/Quartz/Runtime/versions/3.0.0/PrismLib.UI.dll included
        //   UserLibs/PrismLib.UI.dll                            included
        //   UserData/Quartz/Runtime/versions/3.0.0/PrismLib.UI.dll included
        //   UserData/Quartz/PrismLib.UI.dll                     not a store: excluded
        Assert(new HashSet<string>(PrismRules.UiCopies(root)).SetEquals(expectedFour), "every copy a loader could load, nothing else");
    }
    public static void TestPrismMods() {
        Assert(PrismRules.ToPrismMods(0) == 0 && PrismRules.ToPrismMods(1) == 2 && PrismRules.ToPrismMods(2) == 4
            && PrismRules.ToPrismMods(3) == 1 && PrismRules.ToPrismMods(4) == 8, "Keybind None/Ctrl/Alt/Shift/Cmd -> PrismLib KeyMods bits");
    }
}
static class QuartzLoggerTests {
    public static void TestRecentKeepsTheNewestThousand() {
        QuartzLogger.ClearRecent();
        var log = new QuartzLogger(new NullLogger());
        for(int i = 0; i < 1100; i++) log.Msg("line " + i);
        string[] r = QuartzLogger.Recent();
        Assert(r.Length == 1000 && r[0].EndsWith("line 100") && r[999].EndsWith("line 1099"), "capped, oldest dropped, order kept");
    }
}
```
`NullLogger` is a do-nothing `IQuartzLogger` in the test file. Register four entries in `Program.cs`; link `PrismRules.cs` in the test csproj.

- [ ] **Step 2: Run them to see them fail**

Run: `scripts/test.sh`
Expected: build fails, `PrismRules` / `QuartzLogger.Recent` not found.

- [ ] **Step 3: Implement `PrismRules` and the log ring**

`UiCopies` scans `{Mods,UMMMods}/*/PrismLib.UI.dll` (skipping a folder named `PrismLib`), `{Mods,UMMMods}/*/Runtime/versions/*/PrismLib.UI.dll`, `UserLibs/PrismLib.UI.dll`, `UserData/*/Runtime/versions/*/PrismLib.UI.dll`; missing folders are fine. The ring is a lock-guarded `Queue<string>` shared by every `QuartzLogger`; `Wrn`/`Err` lines are stored with `[warn] `/`[error] ` in front.

- [ ] **Step 4: Run them to see them pass**

Run: `scripts/test.sh` → every test passes, including the four new ones.

- [ ] **Step 5: Implement the bridge, the gate and the wiring**

`PrismBridge` mirrors `FacetBridge`: each entry is `[MethodImpl(NoInlining)]`, checks its state, calls a `NoInlining` `…Impl` inside `try`, and on a load-type failure (`TypeLoadException`, `MissingMethodException`, `MissingFieldException`, `FileNotFoundException`, `TypeInitializationException`) turns that half off and logs once (`"PrismLib unavailable: …"` / `"PrismLib.UI unavailable: …"`). Under `IL2CPP` every `…Impl` is compiled out and entries return their defaults.
- `Init(host)`: stays off with one log line when `IL2CPP`, `Info.KeyViewerOnly`, or `!FacetBridge.IsUnity6(Application.unityVersion)`. Otherwise `PrismBootstrap.Ensure(m => MainCore.Log.Msg(m), new Version(0, 3, 12), PrismRules.ModsRoot(host.ModsPath, umm), bundle: null)` (`umm` = `QUARTZ_UMM`; the default bundle is `PrismLib.bundle` beside `Quartz.dll`, i.e. the store). `Available` = its result. Nothing else here: `Init` must run before anything loads PrismLib.UI or Facet.
- `Register()` (after services exist): `Prism.Register("Quartz", <Info.Version before '-'>)`; `Keys.KeyName = i => ((KeyCode)i).ToString()`; `AddLog(MainCore.Tr.Get("PRISM_DEBUG_LOG", "log"), QuartzLogger.Recent, null, QuartzLogger.ClearRecent)`; `AddFields(MainCore.Tr.Get("PRISM_DEBUG_STATE", "State"), …)` with keys `version, loader, enabled, menu, arranging, newMenu, hotkey, facet, prismlib.ui, scene` (`arranging`/`newMenu` wired in Tasks 5–6, `"n/a"` until then); `BindKey("Menu", Conf.ToggleKey, (KeyMods)PrismRules.ToPrismMods(Conf.ToggleModifier), MainCore.Tr.Get("PRISM_KEY_MENU", "Open the Quartz menu"))` (the three keys arrive in Task 5; until then `Tr.Get` returns the English default, and the names are read once, since re-adding a log under a new name would duplicate its tab); `ReportPointerOverUi(() => UICore.IsOpen)`. When `UiAvailable`: `InputGate.AddMouseExemption(() => UICore.IsOpen)`, `InputGate.AddBypass(() => RDC.auto)` (each wrapped to return false on throw).
- `PrismGate` is CamDisplay's `Util/Gate.cs` in Quartz's shape: own Harmony id `"Quartz.PrismGate"`, `InputGate.EnsureInstalled("Quartz", Patch)` and `FontFix.EnsureInstalled("Quartz", Patch)` every frame, the mouse-redirect transpiler, `Uninstalled("Quartz")` for both on `Shutdown`.
- `Tick()`: when `UiAvailable`, runs `PrismGate.Tick()` (later tasks add more) inside `InputGate.Exempt`.
- `Exempt(a)`: `InputGate.Exempt(a)` when `UiAvailable`, else `a()`. `a` runs exactly once either way; an exception thrown by `a` propagates unchanged and never turns PrismLib.UI off.
- `TryPhysical(key, out held)`: `MacKeys.TryPhysical`, false when unavailable.
- `Shutdown()`: `PrismGate.Shutdown()`, then (when `Available`) `Claims.ReleaseAll`, `Keys.Unregister`, `Layout.Unregister`, `Settings.Unregister`, `Diagnostics.Unregister` for `"Quartz"`; `Available = false`.
- `QuartzRuntime.Initialize`: `PrismBridge.Init(Host)` right after `Paths.Initialize()`; `PrismBridge.Register()` right after `services.Initialize(Logger)`, before `FacetBridge.WarmUp()`; under `DEBUG && !IL2CPP && !QUARTZ_STUBS` also `ticks.Add(new FacetSelfTest("prism-selftest", Quartz.Prism.PrismSelfTest.Run))`. `Dispose`: `Safe(PrismBridge.Shutdown)` right after `FacetBridge.Shutdown`.
- `UIService.Tick()`: `PrismBridge.Exempt(UICore.HandleUpdate); PrismBridge.Tick();`.
- `FacetSelfTest`: marker and log names come from `name`; a null `body` runs today's Facet checks.

- [ ] **Step 6: The bridge loads without PrismLib**

```bash
dotnet build Quartz/Quartz.csproj -c Release -p:AutoInstall=false
cd ../../PrismLib && ./tools/check-bridge.sh ../Quartz/Quartz-facet/Quartz/bin/Release/netstandard2.1/Quartz.dll \
  Quartz.Prism.PrismBridge Init Register Tick Shutdown Exempt TryPhysical get_UiAvailable
```
Expected: an `ok … JITs without PrismLib` line per method, then `OK: Quartz.Prism.PrismBridge loads without PrismLib (Available = False)`. (Quartz's bin has no PrismLib.UI beside it, so this also proves Review Focus 1's "UI absent".)

- [ ] **Step 7: Self-test checks**

`PrismSelfTest.Run` names no PrismLib type: if `!PrismBridge.Available` it fails `"prism: PrismLib wired"` and stops; else it yields `PrismSelfTestChecks.Loading(check, info)`:
```csharp
var ui = AppDomain.CurrentDomain.GetAssemblies().Where(a => a.GetName().Name == "PrismLib.UI").ToArray();
string game = Path.GetDirectoryName(PrismRules.ModsRoot(MainCore.Host.ModsPath, <QUARTZ_UMM>)), store = Path.GetDirectoryName(typeof(PrismBridge).Assembly.Location);
check("prism: PrismLib wired", PrismBridge.Available && PrismBridge.UiAvailable, $"lib={PrismBridge.Available} ui={PrismBridge.UiAvailable}");
check("prism: exactly one PrismLib.UI loaded", ui.Length == 1, string.Join(", ", ui.Select(a => a.Location)));
check("prism: the loaded PrismLib.UI is the newest copy on disk",
    PrismRules.UiCopies(game).All(p => AssemblyName.GetAssemblyName(p).Version <= ui[0].GetName().Version), <each copy: path=version>);
check("prism: PrismLib.dll came from a shared PrismLib folder", Path.GetFileName(Path.GetDirectoryName(typeof(PrismLib.Prism).Assembly.Location)) == "PrismLib");
check("prism: the store holds the bundle, never PrismLib.dll", File.Exists(Path.Combine(store, "PrismLib.bundle")) && !File.Exists(Path.Combine(store, "PrismLib.dll")));
check("prism: Quartz is registered", PrismLib.Prism.IsLoaded("Quartz", out _));
string logName = MainCore.Tr.Get("PRISM_DEBUG_LOG", "log"), stateName = MainCore.Tr.Get("PRISM_DEBUG_STATE", "State");
check("prism: Quartz's log is in the debug registry", Diagnostics.Logs.Any(l => l.Owner == "Quartz" && l.Name == logName && l.Tail().Any(s => s.Contains("Hello"))));
check("prism: Quartz's fields are in the debug registry", Diagnostics.Fields.Any(f => f.Owner == "Quartz" && f.Name == stateName && f.Read().Any(kv => kv.Key == "version")));
check("prism: the menu key is registered", Keys.All.Any(k => k.Owner == "Quartz" && k.Id == "Menu" && k.KeyCode == MainCore.Conf.ToggleKey));
check("prism: the input gate is patched", HarmonyLib.Harmony.GetPatchInfo(typeof(Input).GetMethod("GetKey", new[] { typeof(KeyCode) }))
    ?.Postfixes.Any(p => p.PatchMethod.DeclaringType?.FullName?.StartsWith("PrismLib.UI.Toolkit.InputGate") == true) == true);
int ran = 0; PrismBridge.Exempt(() => ran++);
check("prism: Exempt runs its action once", ran == 1);
info($"MODE {(PrismLib.Prism.LoadedMods.Count() == 1 ? "quartz-only" : "beside " + string.Join(",", PrismLib.Prism.LoadedMods))} loader={<ML|UMM>}");
info("[perf] PrismBridge.Tick mean over 600 frames, nothing open: <µs>");
```

- [ ] **Step 8: Run the in-game self-test** (recipe above; refresh `lib/` first with `cd ../../PrismLib && ./dev.sh && cd - && DEV=1 tools/update-prismlib.sh`)
Expected: the twelve checks PASS, `DONE … fail=0`, and `MelonLoader/Latest.log` shows `PrismLib bootstrap: already loaded` or `loaded … from …/Mods/PrismLib/PrismLib.dll`.

- [ ] **Step 9: Commit (Q)**, in this order so each commit builds

```bash
git commit -m "feat: keep Quartz's recent log lines for the debug window"     # QuartzLogger + its test
git commit -m "feat: add the bridge's pure rules"                             # PrismRules + its tests + test csproj/Program
git commit -m "test: let the self-test runner take a name and a body"         # FacetSelfTest
git commit -m "feat: join PrismLib through a guarded bridge"                  # PrismBridge, PrismGate, QuartzRuntime, UIService, csproj, PrismSelfTest*
```

---

### Task 4: Quartz hosts the shared debug and settings windows

**Files:**
- Create: `Q/Quartz/Prism/PrismWindows.cs`
- Modify: `Q/Quartz/Prism/PrismBridge.cs`, `Q/Quartz/Prism/PrismSelfTestChecks.cs`

**Interfaces:**
- Consumes: Task 3's `PrismBridge`, `Exempt`, `QuartzLogger.Recent`.
- Produces: `PrismBridge.ToggleDebug()`, `PrismBridge.TogglePrismSettings()`; `PrismWindows` (internal static, PrismLib.UI-typed fields allowed, reached only after `UiAvailable`): `Tick()`, `ToggleDebug()`, `ToggleSettings()`, `IEnumerable<DebugTab> DebugTabs()`, `IEnumerable<SettingRow> SettingRows()`, `bool OwnsDebug`, `Shutdown()`.

- [ ] **Step 1: Write the failing self-test checks** (`PrismSelfTestChecks.Windows`, yielded after `Loading`; `Frames(n)` yields `n` frames)

```csharp
bool quartzOnly = PrismLib.Prism.LoadedMods.Count() == 1;
string owner = Claims.OwnerOf(StateKey.DebugPanel);
check("windows: the debug window has an owner", quartzOnly ? owner == "Quartz" : owner != null, $"owner={owner}");
PrismBridge.ToggleDebug(); yield return Frames(2);
check("windows: the Alt+D path opens the debug window", Ui.AnyWindowOpen);
check("windows: an open Prism window withholds game input", InputGate.Blocking);   // self-test ticks outside Exempt
PrismBridge.ToggleDebug(); yield return Frames(2);
check("windows: and closes it", !Ui.AnyWindowOpen);
if(owner == "Quartz")
    check("windows: debug tabs list Quartz", new[] { "Quartz · " + MainCore.Tr.Get("PRISM_DEBUG_LOG", "log"), "Quartz · " + MainCore.Tr.Get("PRISM_DEBUG_STATE", "State"), "Prism" }
        .All(n => PrismWindows.DebugTabs().Any(t => t.Name == n)));
PrismBridge.TogglePrismSettings(); yield return Frames(2);
check("windows: Prism · Settings opens", Ui.AnyWindowOpen);
check("windows: it lists PrismLib's own rows", PrismWindows.SettingRows().Any(r => r.Owner == "PrismLib" && r.Label == "Show FPS"));
PrismBridge.TogglePrismSettings(); yield return Frames(2);
check("windows: and closes", !Ui.AnyWindowOpen);
check("windows: PrismLib's words follow Quartz's language", Words.Language() == (QuartzHost.LanguageCode(MainCore.Tr.Language) == "ko" ? "ko" : "en"));
Window.Write("prism-selftest", "1");
check("windows: geometry lands in Quartz's settings", MainCore.Conf.FacetWindows.TryGetValue("prism-selftest", out var v) && v == "1");
Window.Write("prism-selftest", null);   // and remove it again (the FacetWindows writer drops null)
```

- [ ] **Step 2: Build to see them fail**: `dotnet build Quartz/Quartz.csproj -c Debug -p:AutoInstall=false` → error, `PrismBridge` has no `ToggleDebug`.

- [ ] **Step 3: Implement `PrismWindows`**

Port these pieces of `Topaz/Topaz/Util/PrismBridge.cs` (read only) with Quartz's names: `TickDebug` (chord polls through `MacKeys.Down/Held` every frame, `FpsCounter.Tick(Available && Config.GetBool(Prism.FpsKey))` always, `Ui.TickWheel` only while `OwnsDebug`, Ctrl/Cmd+Z → `History.Undo/Redo` only while `Ui.AnyWindowOpen && !UICore.IsOpen`, Escape → `Ui.CloseTop()` only while `Ui.AnyWindowOpen`, Alt+D/Alt+P only while `OwnsDebug`), `ToggleDebug`/`BuildDebug`, `TickCapture`, `ToggleSettings`/`BuildSettings`, `SettingRows`/`Capturer`/`Kind`, `DebugTabs`/`AddPrismTabs`/`PrismState`/`Pairs`, `TakeDebugClaim`/`RequestToggle`, `Wearing`. Quartz specifics:
- Theme: build each window inside `Theme.Use(Theme.FromAccent(MainCore.Conf.GetAccentColor()))`.
- Geometry: `Window.Read/Write` over `MainCore.Conf.FacetWindows`, writes go through `QuartzHost`'s existing debounce rule (only a changed value calls `RequestSave`; `null` removes).
- Language: in `Register` (UI half) `Words.Language = () => QuartzHost.LanguageCode(MainCore.Tr.Language) == "ko" ? "ko" : "en"`; `Ui.Log = m => MainCore.Log.Msg("[prism] " + m)` when a window is first built.
- Debug tab for a Quartz-only, PrismLib-less session: `"Quartz · log"` from `QuartzLogger.Recent`.
- Left out on purpose: Topaz's frame-300/420 pre-build (add it if first opens hitch), `AltDiag`, `UiOnFacet`/matchers, its own menu, restart prompt, guards, the window-store file.
`PrismBridge.Tick` calls `PrismWindows.Tick()` inside the same `Exempt`; `Shutdown` disposes both windows before the bridge unregisters (Dispose writes geometry; it already runs before `Config.Save`).

- [ ] **Step 4: Run the in-game self-test** → the eleven `windows:` checks PASS, `fail=0`.

- [ ] **Step 5: Commit (Q)** `git add Quartz/Prism && git commit -m "feat: host Prism's debug and settings windows"`

---

### Task 5: Prism · Settings rows, the experimental switch, the hotkey through MacKeys

**Files:**
- Modify: `Q/Quartz/Prism/PrismBridge.cs` (rows), `Q/Quartz/Prism/PrismRules.cs`, `Q/Quartz/IO/CoreSettings.cs`, `Q/Quartz/Core/Keybind.cs`, `Q/Quartz/Resource/Export/Lang/en-US.json`, `ko-KR.json`, `KEY_PREFIXES.md`, `Q/Quartz/Prism/PrismSelfTestChecks.cs`
- Test: `Q/Quartz.Tests/Core/PrismRulesTests.cs`, `Program.cs`

**Interfaces:**
- Consumes: Task 3's `ToPrismMods`, Task 4's host (Topaz's or Quartz's capture calls `SettingEntry.Set(string keyName)`).
- Produces: `CoreSettings.ExperimentalNewMenu`; rows `quartz.menu.key` (Key), `quartz.menu.modifier` (Enum, options index = `Keybind.KeyModifier`), `quartz.experimental.newMenu` (Bool), page `"Quartz"`, groups `"Menu"` / `"Experimental"`; `PrismRules.HotkeyRefused(int modifier, int keyCode)`, `PrismRules.EnglishToKey` (`IReadOnlyDictionary<string, string>`), `PrismRules.Translate(string english, Func<string, string, string> get)` (null when unknown); `Keybind.MacKeysInUse` (`internal static bool`).

- [ ] **Step 1: Write the failing offline tests**

```csharp
public static void TestHotkeyRefusals() {
    const int None = 0, Ctrl = 1, Alt = 2;
    Assert(!PrismRules.HotkeyRefused(Alt, 107), "Alt+K is Quartz's default and allowed");
    foreach(int k in new[] { 100, 112, 116, 99 }) Assert(PrismRules.HotkeyRefused(Alt, k), "Alt+D/P/T/C are Prism's chords");
    Assert(!PrismRules.HotkeyRefused(Ctrl, 100) && !PrismRules.HotkeyRefused(None, 100), "D without Alt is not a Prism chord");
    foreach(int k in new[] { 0, 303, 304, 305, 306, 307, 308, 309, 310, 311, 312, 313, 323 })
        Assert(PrismRules.HotkeyRefused(Alt, k), "no key, a modifier key or a mouse button is never a hotkey");
}
public static void TestPrismWordsMatchLang() {
    var en = <en-US.json "en-US" object>; var ko = <ko-KR.json "ko-KR" object>;   // path as LocalizationParityTests finds it
    foreach(var (english, key) in PrismRules.EnglishToKey) {
        Assert(en[key] == english, $"{key}: en-US value is the English the bridge hands PrismLib");
        Assert(!string.IsNullOrEmpty(ko[key]), $"{key}: authored in ko-KR");
    }
    Assert(PrismRules.Translate("Menu hotkey", (k, d) => k) == "PRISM_MENU_HOTKEY" && PrismRules.Translate("Undo", (k, d) => k) == null,
        "known English maps to its key; anything else is not ours");
}
```

- [ ] **Step 2: Run `scripts/test.sh`** → build fails on the missing members.

- [ ] **Step 3: Implement**

- `PrismRules.HotkeyRefused`: true for key 0, Unity modifier codes 303–313, mouse 323–329, or `modifier == 2` with key `d`(100)/`p`(112)/`t`(116)/`c`(99).
- Lang keys (en / ko; grep ko-KR for an existing term first and keep it if it differs):

| Key | en-US | ko-KR |
|---|---|---|
| `PRISM_GROUP_MENU` | Menu | 메뉴 |
| `PRISM_GROUP_EXPERIMENTAL` | Experimental | 실험적 기능 |
| `PRISM_MENU_HOTKEY` | Menu hotkey | 메뉴 단축키 |
| `PRISM_MENU_HOTKEY_TIP` | The key that opens Quartz's menu, with the modifier below | Quartz 메뉴를 여는 키 (아래 보조 키와 함께) |
| `PRISM_MENU_MODIFIER` | Menu hotkey modifier | 메뉴 단축키 보조 키 |
| `PRISM_MODIFIER_NONE` | None | 없음 |
| `PRISM_NEW_MENU` | Experimental: new menu | 실험적: 새 메뉴 |
| `PRISM_NEW_MENU_TIP` | The menu hotkey opens the new menu. It is still being built; the classic menu stays available | 메뉴 단축키로 새 메뉴를 엽니다. 아직 만드는 중이며 기존 메뉴도 계속 쓸 수 있습니다 |
| `PRISM_KEY_MENU` | Open the Quartz menu | Quartz 메뉴 열기 |
| `PRISM_DEBUG_LOG` | log | 로그 |
| `PRISM_DEBUG_STATE` | State | 상태 |

  `EnglishToKey` maps the en-US value of every `PRISM_GROUP_*`, `PRISM_MENU_*`, `PRISM_MODIFIER_NONE` and `PRISM_NEW_MENU*` key to that key: rows hand PrismLib English and `Words` translates at draw time through `Words.AddTranslator(s => PrismRules.Translate(s, (k, d) => MainCore.Tr.Get(k, d)))` (a lambda compiled in Quartz, so Words ties it to owner "Quartz"). `PRISM_KEY_MENU` and `PRISM_DEBUG_*` are already read with `Tr.Get` in `Register` (Task 3).
- `CoreSettings`: `public bool ExperimentalNewMenu;` serialized/deserialized under `nameof(ExperimentalNewMenu)` like `ToggleKey`.
- Rows (one `DescribeSettings` call; `Order` 0–2):
  - `quartz.menu.key`: `Get` = `Keybind.Format(mod, key)`; `Set(object v)` parses a `KeyCode` name; refused when unparseable or `HotkeyRefused(Conf.ToggleModifier, key)` (one log line, value kept); else `Conf.ToggleKey = key`, `RequestSave()`, `BindKey("Menu", …)` again (same id replaces).
  - `quartz.menu.modifier`: Options `{ "None", Keybind.ModifierName(Ctrl), …(Alt), …(Shift), …(Cmd) }`; `Set(int i)` refused when `HotkeyRefused(i, Conf.ToggleKey)`; else write, save, rebind.
  - `quartz.experimental.newMenu`: `Get`/`Set` `Conf.ExperimentalNewMenu`, save on change. `State` field `newMenu` shows it.
- `Keybind`: `PollMac` and `MacHeld` ask `PrismBridge.TryPhysical(key, out held)` first and set `MacKeysInUse = true` when it answers; only when it returns false do they call `HookInput.TryMacKeyState` as today. Edge logic, focus check and the Alt-only mac edge stay as they are.

- [ ] **Step 4: Run `scripts/test.sh` and `python3 scripts/validate_i18n.py Quartz/Resource/Export/Lang Quartz/Resource/Export/Lang/en-US.json`** → all tests pass, validator exits 0.

- [ ] **Step 5: Self-test checks** (`PrismSelfTestChecks.Settings`)

```csharp
var rows = Settings.Of("Quartz").ToDictionary(kv => kv.Value.Id, kv => kv.Value);
check("settings: Quartz describes three rows", rows.Keys.OrderBy(k => k).SequenceEqual(new[] { "quartz.experimental.newMenu", "quartz.menu.key", "quartz.menu.modifier" }));
check("settings: the new-menu switch defaults off", !new CoreSettings().ExperimentalNewMenu);
bool was = MainCore.Conf.ExperimentalNewMenu; var nm = rows["quartz.experimental.newMenu"];
nm.Set(true);
check("settings: the switch writes Quartz's settings", MainCore.Conf.ExperimentalNewMenu && (bool)nm.Get());
check("settings: the switch is saved", MainCore.Conf.Serialize()["ExperimentalNewMenu"]?.Value<bool>() == true);
nm.Set(was);
int k0 = MainCore.Conf.ToggleKey, m0 = MainCore.Conf.ToggleModifier; var key = rows["quartz.menu.key"]; var mod = rows["quartz.menu.modifier"];
mod.Set(2); key.Set("J");
check("settings: the hotkey row rebinds", MainCore.Conf.ToggleKey == (int)KeyCode.J && Keys.All.Any(b => b.Owner == "Quartz" && b.Id == "Menu" && b.KeyCode == (int)KeyCode.J));
key.Set("D");         check("settings: a Prism chord is refused", MainCore.Conf.ToggleKey == (int)KeyCode.J);
key.Set("LeftShift"); check("settings: a bare modifier is refused", MainCore.Conf.ToggleKey == (int)KeyCode.J);
mod.Set(1);           check("settings: the modifier row writes Ctrl", MainCore.Conf.ToggleModifier == 1);
mod.Set(m0); key.Set(((KeyCode)k0).ToString());
check("settings: the hotkey is restored", MainCore.Conf.ToggleKey == k0 && MainCore.Conf.ToggleModifier == m0);
check("keys: Alt+K is wanted by no other mod", !Keys.Wanting((int)KeyCode.K, KeyMods.Alt, "Quartz").Any(), <owners>);
check("settings: every Prism label has a Lang key", PrismRules.EnglishToKey.Values.All(MainCore.Tr.HasKey));
if(Keybind.IsMac) { Keybind.ModifierHeld(Keybind.KeyModifier.Alt); check("keys: the Mac path reads PrismLib's MacKeys", Keybind.MacKeysInUse); }
else info("keys: not a Mac, MacKeys not exercised");
```

- [ ] **Step 6: Run the in-game self-test** → these fourteen checks PASS, `fail=0`.

- [ ] **Step 7: Commit (Q)** (hunk-split the Lang pair into the rows commit)

```bash
git commit -m "feat: add the experimental new-menu setting"                 # CoreSettings
git commit -m "feat: describe Quartz's menu settings in Prism · Settings"   # rows, PrismRules words/refusals, Lang keys, KEY_PREFIXES, tests
git commit -m "feat: read the menu hotkey through PrismLib's MacKeys on macOS"   # Keybind
git commit -m "test: check Quartz's Prism settings in game"                # self-test
```

---

### Task 6: The arranging state (overlays show while any editor arranges them; one editor at a time)

**Files:**
- Modify: `Q/Quartz/UI/UICore.Reorganize.cs`, `UICore.Visibility.cs`, `Q/Quartz/UI/Utility/Reorganizer.cs` (`internal static int LiveHandleCount => handles.Count;`)
- Modify (visibility/capture switch): `Features/Combo/ComboOverlay.cs`, `Features/Judgement/JudgementOverlay.cs`, `Features/SongTitle/SongTitleOverlay.cs`, `Features/Practice/PracticeOverlay.cs`, `Features/ProgressBar/ProgressBar.cs`, `Features/Panels/PanelsOverlay.cs`, `PanelsOverlay.LivePanel.cs`, `Features/KeyViewer/KeyViewerOverlay.Update.cs`, `Features/Overlay/OverlayMotion.cs`, `Features/Calibration/CalibrationModule.cs`
- Modify: `Q/Quartz/Prism/PrismRules.cs`, `PrismBridge.cs`; Create `Q/Quartz/Prism/PrismSelfTestChecks.Layout.cs`
- Test: `Q/Quartz.Tests/Core/PrismRulesTests.cs`, `Program.cs`

**Interfaces:**
- Produces: `UICore.SharedLayoutOpen` (`bool`, private set), `UICore.IsArranging => IsReorganizing || SharedLayoutOpen`, `event Action<bool> UICore.OnArrangingChanged` (raised only when `IsArranging` changes), `internal static void UICore.SetSharedLayout(bool open)`, `internal static bool UICore.ToggleHotkeyAllowed(bool modHeld, bool down)`; `PrismRules.LayoutSession` (`void Touch(int frame)`, `bool Open(int frame)`, `const int Grace = 2`); `PrismBridge.TouchLayout()` (marks this frame as one in which an editor read Quartz's boxes).

- [ ] **Step 1: Write the failing offline test**

```csharp
public static void TestLayoutSession() {
    var s = new PrismRules.LayoutSession();
    Assert(!s.Open(0), "closed before any read");
    s.Touch(10);
    Assert(s.Open(10) && s.Open(12), "open on the read frame and for Grace frames after");
    Assert(!s.Open(13), "closed once Grace frames pass with no read");
}
```

- [ ] **Step 2: Run `scripts/test.sh`** → build fails on `LayoutSession`.

- [ ] **Step 3: Implement**

- `SetSharedLayout(true)`: sets the flag **before** closing the reorganize hub with `Close(noAnimate: true)` when reorganizing (`Close` already calls `ExitReorganize`, and the hub's Close button does the same; setting the flag first keeps `IsArranging` true across the hand-over); `SetSharedLayout` with an unchanged value does nothing. `EnterReorganize` returns early while `SharedLayoutOpen`; `ToggleHotkeyAllowed` adds `&& !SharedLayoutOpen`. Enter/Exit/SetSharedLayout raise `OnArrangingChanged` through one helper that compares the old and new `IsArranging`.
- Rule for every listed overlay: **drag surfaces and raycasters follow `UICore.IsReorganizing`; visibility (`ShouldShow`, the gate's `want`), placeholder content (e.g. Combo's 42), position capture and motion suppression follow `UICore.IsArranging`.** ProgressBar's `movable` keeps `Style != Line` and uses `IsReorganizing` for its drag surface. Key Viewer refits its drag bounds (`Canvas.ForceUpdateCanvases(); RefreshDragBounds();`) when arranging starts, as it does today when its drag surface turns on. Practice's shortcut guard and OverlayMotion's stand-down use `IsArranging`.
- `CalibrationModule`: subscribe to `OnArrangingChanged` instead of `OnReorganizeChanged` (begin/end the popup the same way; check `IsArranging` on enable). `OnReorganizeChanged` stays (public API) and keeps firing as before.
- `PrismBridge`: a `PrismRules.LayoutSession` field (Quartz type, allowed); `TouchLayout()` → `Touch(Time.frameCount)`; `Tick()` calls `UICore.SetSharedLayout(session.Open(Time.frameCount))` every frame, outside any PrismLib guard (works with PrismLib absent: it then never opens).

- [ ] **Step 4: Run `scripts/test.sh`** → passes.

- [ ] **Step 5: Self-test checks** (`PrismSelfTestChecks.Arrange`; `Session(n)` = `n` frames each calling `PrismBridge.TouchLayout()`; the run is outside gameplay)

```csharp
var flips = new List<bool>(); Action<bool> rec = flips.Add; UICore.OnArrangingChanged += rec;
check("arrange: the hotkey works outside a session", UICore.ToggleHotkeyAllowed(true, true));
UICore.EnterReorganize(); yield return Frames(3);
check("arrange: reorganize mode is unchanged", UICore.IsArranging && Reorganizer.LiveHandleCount > 0);
UICore.ExitReorganize(); yield return Frames(3);
yield return Session(3);
check("arrange: a shared session shows the overlays", UICore.SharedLayoutOpen && GameObject.Find("ComboRoot") != null);
check("arrange: but leaves every drag surface off", Reorganizer.LiveHandleCount == 0);
check("arrange: the hotkey is ignored during a session", !UICore.ToggleHotkeyAllowed(true, true));
UICore.EnterReorganize();
check("arrange: reorganize is refused during a session", !UICore.IsReorganizing);
yield return Frames(4);
check("arrange: the session ends Grace frames after the last read", !UICore.SharedLayoutOpen);
check("arrange: overlays hide again outside gameplay", GameObject.Find("ComboRoot") == null);
UICore.EnterReorganize(); yield return Frames(2); yield return Session(3);
check("arrange: a session ends reorganize mode and closes the hub", !UICore.IsReorganizing && !UICore.IsOpen && UICore.SharedLayoutOpen && Reorganizer.LiveHandleCount == 0);
yield return Frames(4); UICore.OnArrangingChanged -= rec;
check("arrange: OnArrangingChanged fires only on a change", flips.Count >= 4 && flips.Zip(flips.Skip(1), (a, b) => a != b).All(x => x), string.Join(",", flips));
```

- [ ] **Step 6: Run the in-game self-test** → the eleven `arrange:` checks PASS, `fail=0`. Then open the old menu by hand once (hotkey, reorganize hub, drag one overlay, close) if the user wants to see it; not required.

- [ ] **Step 7: Commit (Q)**

```bash
git commit -m "feat: add an arranging state shared by the reorganizer and PrismLib's editor"   # UICore, Reorganizer, PrismRules, PrismBridge, tests
git commit -m "feat: show overlays while any layout editor arranges them"                    # the overlay files + CalibrationModule
git commit -m "test: check the arranging state in game"
```

---

### Task 7: A layout target for every element the reorganizer moves

**Files:**
- Create: `Q/Quartz/UI/Utility/ReorganizeHandle.Layout.cs`, `Q/Quartz/Prism/QuartzLayout.cs`
- Modify (tag each handle): `Features/KeyViewer/KeyViewerOverlay.Boxes.cs`, `KeyViewerOverlay.Tabub.cs`, `Features/Combo/ComboOverlay.cs`, `Features/Judgement/JudgementOverlay.cs`, `Features/SongTitle/SongTitleOverlay.cs`, `Features/ProgressBar/ProgressBar.cs`, `Features/Practice/PracticeOverlay.cs`, `Features/Calibration/CalibrationPopupUI.cs`, `Features/Panels/PanelsOverlay.cs`
- Modify: `Q/Quartz/Prism/PrismBridge.cs`, `PrismSelfTestChecks.Layout.cs`

**Interfaces:**
- Consumes: Task 6's `TouchLayout`, `IsArranging`; existing `ReorganizeHandle` (`Target`, `MeasureRect`, `GetName`, `OnMoved`, `GetSize/SetSize/MinSize/MaxSize/CanResize`, `GetRotation/SetRotation/CanRotate`, `AnchorClosest`, `SetAnchor`, `HasAnchor`, `CurrentAnchor`), `Reorganizer.SnapAnchor`.
- Produces:
  - `internal static class LayoutIds` (namespace `Quartz.UI.Utility`): `KeyViewer = "keyviewer"`, `KeyViewerFoot = "keyviewer.foot"`, `Tabub = "keyviewer.tabub"`, `Combo = "combo"`, `Judgement = "judgement"`, `SongTitle = "songtitle"`, `ProgressBar = "progressbar"`, `Practice = "practice"`, `Calibration = "calibration"`, `static string Panel(int index) => "panel." + index`.
  - On `ReorganizeHandle` (partial, all `internal`): `string LayoutId`, `Func<bool> LayoutMovable`, `Action<bool> SetClosest`, `static int LayoutVersion` (bumped by every tag), `static void SetLayoutTarget(GameObject drag, string id, Func<bool> movable = null)`, `static ReorganizeHandle FindLayout(string id)` (null when missing or destroyed), `static List<ReorganizeHandle> LayoutHandles()` (live ones, in `LayoutIds` order, panels last by index), `static void SetAnchorSource(GameObject drag, Func<bool> closest, Action<OverlayAnchor> setAnchor, Action<bool> setClosest)` (new overload; the public 3-argument one stays as it is).
  - `QuartzLayout` (internal static, PrismLib.UI types allowed): `IEnumerable<EditTarget> Targets()`, `void Open(bool all)`, `bool OwnsEditor`, `void Tick()`, `void Shutdown()`.
  - `PrismBridge.OpenLayoutEditor()`.

The elements (every `CreateDragSurface`/`BuildReorganizeHandle` site): Key Viewer (anchor, scale `DmScale` 0.2–4, rotation), Key Viewer foot (scale, rotation; only when foot keys exist), "This Tabub Is Mine" (scale, rotation), Combo, Judgement, Song Title (anchor, scale, rotation), Progress Bar (anchor, rotation; not movable in Line style), Practice Difficulty (font size 8–120 px as scale, rotation), Calibration popup (position only), each Panel (anchor, font size 12–48 px as scale, rotation). The spec's "attempts" is a Panels stat, covered by the panel targets. Per-element font and Panels' grouping stay on their pages.

- [ ] **Step 1: Write the failing self-test checks** (`PrismSelfTestChecks.Layout`; `Session(n)` as in Task 6; `Near(b, x, y)` = `b != null && |b[0]-x| <= 1.5 && |b[1]-y| <= 1.5`)

```csharp
List<LayoutTarget> Mine() => PrismLib.Layout.All().Where(kv => kv.Key == "Quartz").Select(kv => kv.Value).ToList();
string[] required = { "keyviewer", "combo", "judgement", "songtitle", "practice", "calibration", "panel.0" };
yield return Session(3);
check("layout: every reorganizer element is a target", required.All(id => Mine().Any(t => t.Id == id)), string.Join(",", Mine().Select(t => t.Id)));
info("layout: progressbar " + (Mine().Any(t => t.Id == "progressbar") ? "present" : "absent"));
foreach(var t in Mine()) yield return BothWays(t, check);
```
`BothWays(t)`: snapshot `{centre, scale, rotation, anchor, closest}` through `t`; then
```csharp
// shared editor -> the settings the reorganizer reads
yield return Session(2); var b0 = t.GetBox();
check($"layout {t.Id}: visible during a session", b0 != null); if(b0 == null) yield break;
t.MoveTo(b0[0] + 40, b0[1] + 30); yield return Session(2); Reapply(t.Id); yield return Session(2);
var b1 = t.GetBox();
check($"layout {t.Id}: a shared-editor move is saved where the reorganizer reads it", Near(b1, b0[0] + 40, b0[1] + 30), <b0,b1>);
// reorganizer -> shared editor
yield return Frames(4); UICore.EnterReorganize(); yield return Frames(2);
var h = ReorganizeHandle.FindLayout(t.Id); h.Target.position += new Vector3(-40, -30, 0); ReorganizeHistory.Moved(h); yield return Frames(2);
UICore.ExitReorganize(); yield return Session(2); Reapply(t.Id); yield return Session(2);
check($"layout {t.Id}: a reorganizer move shows in the shared editor", Near(t.GetBox(), b1[0] - 40, b1[1] - 30));
// size, rotation, anchor, both ways (values, not positions)
float tol = h.SizeFormat.EndsWith("px") ? 0.5f : 0.01f;   // Panels and Practice round font sizes
float s1 = h.MinSize + (h.MaxSize - h.MinSize) * 0.25f, s2 = h.MinSize + (h.MaxSize - h.MinSize) * 0.75f;
if(h.CanResize) { t.SetScale(s1); check($"layout {t.Id}: shared scale reaches the reorganizer", Mathf.Abs(h.GetSize() - s1) <= tol);
                  h.SetSize(s2); check($"layout {t.Id}: reorganizer size reaches the shared editor", Mathf.Abs(t.GetScale() - s2) <= tol); }
if(h.CanRotate) { t.SetRotation(30); check(…, h.GetRotation() == 30); h.SetRotation(-15); check(…, t.GetRotation() == -15); }
if(t.SetAnchor != null) { var c = t.GetBox(); t.SetAnchor(t.GetAnchor() == 0 ? 8 : 0); yield return Session(2);
                  check($"layout {t.Id}: a new anchor keeps the element in place", Near(t.GetBox(), c[0], c[1]) && (int)h.CurrentAnchor == t.GetAnchor()); }
// restore: rotation, scale, anchor, closest, then MoveTo(saved centre); check centre ±1.5 and every value equal
```
`Reapply(id)` re-reads the settings: `keyviewer`/`keyviewer.foot` → `KeyViewerOverlay.Apply()`, `keyviewer.tabub` → `KeyViewerOverlay.ApplyTabub()`, `combo`/`judgement`/`songtitle`/`progressbar`/`practice` → that overlay's `Apply()`, `calibration` → `CalibrationPopupUI.EndReorganize(); CalibrationPopupUI.BeginReorganize();`, `panel.N` → `PanelsOverlay.Rebuild(PanelsOverlay.Conf.Panels[N])` (skips syncing that panel, so its position comes from its config).

- [ ] **Step 2: Run the in-game self-test to see it fail** → `FAIL layout: every reorganizer element is a target` (no targets yet).

- [ ] **Step 3: Tag the handles**

At each site call `ReorganizeHandle.SetLayoutTarget(dragObj, LayoutIds.<X>)` after creating it; ProgressBar passes `movable: () => Conf.Style != ProgressBarStyle.Line`; Panels tag in `BuildPanels`' loop with `LayoutIds.Panel(i)`. Every site that calls the 3-argument `SetAnchorSource` (Key Viewer, Combo, Judgement, Song Title, Progress Bar, Panels) switches to the 4-argument overload with `v => { Conf.<…>AnchorClosest = v; Save(); }` (Key Viewer: `Conf.DmAnchorClosest`; Panels: `config.AnchorClosest`).

- [ ] **Step 4: Implement the adapter and the host**

`QuartzLayout.Targets()` builds one `EditTarget` per `LayoutHandles()` entry (`Owner = "Quartz"`, `Id = LayoutId`, `Label = GetName()`); every delegate looks the handle up by id on each call, so rebuilt overlays keep working:
- `GetBox`: first `PrismBridge.TouchLayout()`; null when the handle is missing, `!Target.gameObject.activeInHierarchy`, or `LayoutMovable?.Invoke() == false`; else `MeasureRect`'s centre `TransformPoint(rect.center)` and size `rect.size × |lossyScale|` (overlay canvases are screen-space, so world = screen px, origin bottom-left).
- `MoveTo(x, y)`: shift `Target.position` by `(x, y) − centre` (what `Reorganizer.MoveSelected` does), then `OnMoved?.Invoke()`; the overlay's own capture saves it (Task 6).
- Scale: `GetSize/SetSize`, `MinSize/MaxSize` when `CanResize`; else `() => 1f`, a no-op, `1..1`.
- Rotation pair only when `CanRotate` (degrees CCW, as Quartz stores them). Anchor pair only when `SetAnchor != null`: `GetAnchor = (int)CurrentAnchor` (OverlayAnchor's order is PrismLib's 0–8 reading order), `SetAnchor = a => SetAnchor((OverlayAnchor)a)` (Quartz's rebase keeps the on-screen spot). Closest pair only when `AnchorClosest` and `SetClosest` are set; turning it on also runs `Reorganizer.SnapAnchor(h)`. No crop.

Host and bridge (CamDisplay's `PrismBridge.cs` is the template):
- `Register`: claim `StateKey.LayoutEditor` ("shared layout editor"); when held, `Layout.OpenRequested += () => QuartzLayout.Open(all: true)` and `Layout.Changed += reload if visible` (subscribed once).
- `Tick`: when `ReorganizeHandle.LayoutVersion` differs from the last described one, or after `MainCore.Tr.OnLanguageChanged` (labels), call `DescribeLayout` with `Targets()` converted field for field (CamDisplay's `ToLayoutTarget`), filtering destroyed handles; then `QuartzLayout.Tick()`.
- `OpenLayoutEditor()`: `Layout.RequestOpen()` when `Available` and another mod holds the claim; else `QuartzLayout.Open(all: Available)` (Quartz's own targets only when PrismLib.dll is absent).
- `Shutdown`: dispose the editor, release the claim, unsubscribe; `Layout.Unregister("Quartz")` is already in Task 3's list.

- [ ] **Step 5: Run the in-game self-test** → every `layout <id>:` check PASS for each present id, `fail=0`.

- [ ] **Step 6: Edge checks** (append to `PrismSelfTestChecks.Layout`; `seen` captures `Ui.Log` lines for the duration and forwards them)

```csharp
// Review Focus 3: not movable right now
var style = ProgressBarOverlay.Conf.Style; ProgressBarOverlay.Conf.Style = ProgressBarStyle.Line; ProgressBarOverlay.Apply(); yield return Session(2);
check("layout: a line-style progress bar is hidden, not broken", Mine().First(t => t.Id == "progressbar").GetBox() == null);
ProgressBarOverlay.Conf.Style = style; ProgressBarOverlay.Apply();
PrismBridge.OpenLayoutEditor(); yield return Frames(3);
check("layout: Edit layout opens the shared editor", UICore.SharedLayoutOpen, $"host={Claims.OwnerOf(StateKey.LayoutEditor)}");
PanelsOverlay.Conf.Panels.Add(new PanelConfig { Name = "selftest" }); PanelsOverlay.Rebuild(); yield return Frames(3);
string extra = LayoutIds.Panel(PanelsOverlay.Conf.Panels.Count - 1);
check("layout: a new panel becomes a target", Mine().Any(t => t.Id == extra));
PanelsOverlay.Conf.Panels.RemoveAt(PanelsOverlay.Conf.Panels.Count - 1); PanelsOverlay.Rebuild(); yield return Frames(3);
check("layout: a removed panel drops out", !Mine().Any(t => t.Id == extra));
check("layout: nothing was reported as skipped", !seen.Any(l => l.Contains("skipped")), string.Join(" | ", seen));
Ui.CloseTop(); yield return Frames(4);
check("layout: closing the editor ends the session", !UICore.SharedLayoutOpen);
// Review Focus 4: move, then the session ends at once
var combo = Mine().First(t => t.Id == "combo"); yield return Session(2); var c0 = combo.GetBox();
combo.MoveTo(c0[0] + 25, c0[1] + 25); yield return Frames(5);   // no more reads: the session closes inside Grace
yield return Session(2); ComboOverlay.Apply(); yield return Session(2);
check("layout: a move right before the session ends is kept", Near(combo.GetBox(), c0[0] + 25, c0[1] + 25));
combo.MoveTo(c0[0], c0[1]); yield return Session(2);
```
Run the in-game self-test again → these seven PASS, `fail=0`.

- [ ] **Step 7: Commit (Q)**

```bash
git commit -m "feat: tag reorganizer handles with layout ids"          # ReorganizeHandle.Layout.cs + the feature sites
git commit -m "feat: offer Quartz's overlays to PrismLib's layout editor"   # QuartzLayout, PrismBridge
git commit -m "test: check every layout target both ways in game"
```

---

### Task 8: CI stubs, conventions, docs, and the three in-game runs

**Files:**
- Modify (generated): `Q/stubs/PrismLib/`, `Q/stubs/PrismLib.UI/`, `Q/stubs/MANIFEST.json`, `Q/stubs/README.md`, `Q/stubs/Stubs.proj`
- Modify: `Q/agents/project.md`, `P/docs/superpowers/plans/roadmap.md`

**Interfaces:**
- Consumes: everything above. Produces: a tree CI can build, and the run results the user reads.

- [ ] **Step 1: Regenerate the stubs**

Run: `tools/gen-stubs.sh` then `git status --short stubs`.
Commit only the new `stubs/PrismLib*` projects and the PrismLib/PrismLib.UI entries in `MANIFEST.json`, `README.md`, `Stubs.proj` (hunk-split); leave the pre-existing 0Harmony drift alone (P15).

- [ ] **Step 2: The CI build against stubs**

```bash
dotnet build stubs/Stubs.proj -c Release
dotnet build Quartz/Quartz.csproj -c Release -p:UseStubs=true -p:GamePath= -p:GameData=
dotnet build Quartz/Quartz.csproj -c Release -p:LoaderTarget=UMM -p:UseStubs=true -p:GamePath= -p:GameData=
dotnet build Quartz/Quartz.csproj -c Release -p:Flavor=KeyViewer -p:UseStubs=true -p:GamePath= -p:GameData= -p:AutoInstall=false
scripts/test.sh && python3 scripts/check_conventions.py
```
Expected: four `0 Error(s)`, every test passes, every conventions line `ok:`.

- [ ] **Step 3: Bootstrap copy and bridge checks on both loaders**

```bash
cmp vendor/PrismBootstrap.cs ../../PrismLib/bootstrap/PrismBootstrap.cs && echo same
dotnet build Quartz/Quartz.csproj -c Release -p:AutoInstall=false && dotnet build Quartz/Quartz.csproj -c Release -p:LoaderTarget=UMM -p:AutoInstall=false
cd ../../PrismLib && ./tools/check-bootstrap.sh ../Quartz/Quartz-facet/vendor/PrismBootstrap.cs
M="Init Register Tick Shutdown Exempt TryPhysical ToggleDebug TogglePrismSettings OpenLayoutEditor TouchLayout get_UiAvailable"
./tools/check-bridge.sh ../Quartz/Quartz-facet/Quartz/bin/Release/netstandard2.1/Quartz.dll Quartz.Prism.PrismBridge $M
./tools/check-bridge.sh ../Quartz/Quartz-facet/Quartz/bin/umm/Release/netstandard2.1/QuartzUmm.dll Quartz.Prism.PrismBridge $M
```
Expected: `same`, `bootstrap: ALL PASS`, and both probes end `OK: Quartz.Prism.PrismBridge loads without PrismLib (Available = False)`.

- [ ] **Step 4: Docs**

`Q/agents/project.md`: under "UI system" a "PrismLib" block: `Quartz/Prism/` holds every PrismLib-touching file and `PrismBridge` is the only door (field rule, `Available`/`UiAvailable`); PrismLib.UI + bundle + Facet ship in the store only; `tools/update-prismlib.sh` (`DEV=1`) before builds; `vendor/PrismBootstrap.cs` is a verbatim copy of PrismLib's; layout ids and the arranging state; the `prism-selftest` marker and log. Fix the "Builds also need lib/Facet.0.1.dll" line to name the PrismLib DLLs. `P/…/roadmap.md`: tick plan 9 with "Quartz branch `facet-preview`, unpushed".
Commit: Q `docs: describe Quartz's PrismLib bridge`; P `docs: mark plan 9 done in the roadmap`.

- [ ] **Step 5: Run A, MelonLoader beside Topaz and Sapphire** (the rig as it is; refresh first: PrismLib `./dev.sh`, then `DEV=1 tools/update-prismlib.sh`)
Recipe above. Expected: `MODE beside …Topaz…Sapphire… loader=ML`, `fail=0`.

- [ ] **Step 6: Run B, UMM (under UMMBridge) beside Topaz and Sapphire**
Move `$G/Mods/Quartz.Bootstrap.dll` into a backup dir (FlavorGuard allows one Quartz). Back up an existing `UMMMods/Quartz` too. `dotnet build Quartz/Quartz.csproj -c Debug -p:LoaderTarget=UMM -p:AutoInstall=true -p:UmmAutoInstall=true`; the build prints `UMM installed: <dir>`; the marker is `<dir>/UserData/prism-selftest`, the log `<dir>/UserData/prism-selftest.log`. Launch, wait for DONE as in the recipe.
Expected: `MODE … loader=UMM`, `fail=0`. Then move `<dir>` into the backup dir and put `Quartz.Bootstrap.dll` back in `Mods/`.

- [ ] **Step 7: Run C, MelonLoader with Quartz alone**
Move into a backup dir, recording `md5 -r` of every file first: `Mods/CamDisplay.dll`, `Mods/PrismLib/`, `UMMMods/{Topaz,Sapphire,LayoutProbe,Facet,PrismLib}`, `UserLibs/PrismLib.UI.dll`, `UserLibs/Facet.0.1.dll` (whatever of these exists). Recipe above.
Expected: `MODE quartz-only loader=ML`, the debug-owner and editor-host checks name `Quartz`, `fail=0`, and the log ring shows `PrismLib bootstrap: installed … (bundled)`. Then remove the `Mods/PrismLib/` the bundle created, move everything back, and compare md5s: every line identical. Report all three DONE lines and any INFO `[perf]` line to the user.
