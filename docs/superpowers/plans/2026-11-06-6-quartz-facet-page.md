# Quartz page on Facet (milestone 3) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

> **NOT CONFIRMED.** The user planned this milestone but has not confirmed it (2026-11-06). Do not execute any task until the user says so in their own words. It also needs `2026-11-06-4-facet-engine.md` done. It does not need plan 5.

**Goal:** Quartz's Effect Remover page runs as a Facet window beside Quartz's uGUI menu. It is bound live to the same `EffectRemoverSettings`, and re-themes live with Quartz's accent.

**Architecture:**
- Quartz references `Facet.0.1.dll` directly and ships it, with no bridge.
- One Facet host, "Quartz", maps Quartz's log, translator, language and config.
- A new `FacetEffectRemover` page builds the same rows as `PageEffectRemover`, using `Facet.Controls` with `Bind.To` over `MainCore.Conf`'s Effect Remover settings.
- A button on the uGUI page opens it. **Quartz's uGUI menu hides while the Facet window is visible**, because its canvas draws above every UI Toolkit panel. It comes back on close.

**Tech Stack:** Quartz (`netstandard2.1`, `dotnet build`, `LoaderTarget` ML or UMM, `stubs/` for CI) and Facet 0.1.

**Spec:** `PrismLib/docs/superpowers/specs/2026-11-06-facet-ui-engine-design.md` § "Milestone 3 in more detail".

## Global Constraints

- Quartz is GPL-3.0 and Facet is MIT. Quartz may use Facet; **no Quartz code goes into Facet**.
- Read Quartz's `agents/project.md`, `agents/commits.md` and `agents/i18n.md` before touching it. User-facing strings go through Quartz's Lang JSON per `i18n.md`.
- The uGUI page stays and stays the default. Both pages edit the same settings object, so nothing is stranded.
- Building with `-p:AutoInstall=true` overwrites the current runtime version directory: **back up `UserData/Quartz/Runtime` first** (memory `mac-ingame-test-rig`).
- One in-game test at a time; wait 5 s after a quit before launching.
- Commits follow Quartz's `agents/commits.md`, with **no Co-Authored-By trailer**. Push nothing.

## Review Focus

1. **The user switches profile while the Facet window is open.** `MainCore.Conf` swaps in a new settings object, and the window shows the new profile's values within 150 ms, with no rebuild. Pinned in Task 3.
2. **The accent picker is dragged while the Facet window is open.** The window recolours on every drag step, not only on commit. Pinned in Task 3.
3. **Escape in the Facet window** closes it and brings back the uGUI menu. It does not also close the uGUI menu behind it. Pinned in Task 2.
4. **Typing in a Facet text field** does not trigger Quartz's hotkeys or the game's. Pinned in Task 2.
5. **The Facet DLL is missing** from a broken install. The button reports "Facet unavailable" in the log and the uGUI page keeps working. Pinned in Task 1.

---

### Task 1: Reference, stub and ship Facet

**Files:**
- Create: `lib/Facet.0.1.dll`, and `tools/update-facet.sh` (copies `../../Facet/Facet/bin/Release/netstandard2.0/Facet.0.1.dll`).
- Modify: `Quartz/Quartz.csproj`:
  - `<Reference Include="Facet.0.1"><HintPath>$(LibDir)/Facet.0.1.dll</HintPath></Reference>`;
  - `UnityEngine.UIElementsModule` and `UnityEngine.TextCoreTextEngineModule` references in the shared block, in the style of lines 179-232.
- Modify: the packaging in `tools/release.sh` and the AutoInstall targets:
  - ML: `UserLibs/Facet.0.1.dll`;
  - UMM: `Quartz/Facet.0.1.dll`;
  - the update engine's runtime store carries it beside the versioned runtime.
- Regenerate: `tools/gen-stubs.sh`, so that CI compiles against the new references. `tools/gen-stubs.sh --check` must pass afterwards.
- Create: `Quartz/UI/FacetUi/FacetBridge.cs`. It is the only Quartz file that names a Facet type outside `FacetUi/`. Its `[MethodImpl(NoInlining)]` entry points catch `FileNotFoundException`/`TypeLoadException` and log "Facet unavailable" once (Review Focus 5).

- [ ] **Step 1: Write the failing check.** `dotnet build Quartz/Quartz.csproj -c Debug -p:LoaderTarget=ML` with a call to `FacetBridge.Available()` from `PageEffectRemover`. Expected: compile error, `FacetBridge` not defined.
- [ ] **Step 2: Implement.** Build both loader targets. Run `tools/gen-stubs.sh` then `tools/gen-stubs.sh --check`, and `./test.sh`.
- [ ] **Step 3: Check the packaging.** The ML and UMM dist zips each contain `Facet.0.1.dll` at the paths above, listed with `unzip -l`. Delete `Facet.0.1.dll` from an installed copy, open the uGUI Effect Remover page in-game, and confirm there is one "Facet unavailable" log line and no exception.
- [ ] **Step 4: Commit:**
  - `build: reference and ship Facet`;
  - `chore: regenerate stubs for Facet and UI Toolkit`.

### Task 2: The Quartz host and input

**Files:**
- Create: `Quartz/UI/FacetUi/QuartzHost.cs`, which builds the host:
  - `Host.Create("Quartz", o => …)`;
  - `Log` → `MainCore.Log`;
  - `Translate` → `MainCore.Tr` (English key in, translation out);
  - `Language` → Quartz's language code;
  - `Read`/`Write` → a `FacetWindows` string map persisted with Quartz's settings;
  - `Theme` → `Theme.FromAccent(UIColors.ObjectActive)`.
- Modify: `Quartz/UI/UICore.Visibility.cs`:
  - the menu hotkey and shortcut checks (around lines 107-127) also skip while `Facet.Input.TextFocused` (Review Focus 4);
  - the deferred Escape (around lines 141-167) calls `Facet.Input.HandleEscape()` first and stops if it returns true (Review Focus 3).
- Modify: the `KeyLimiter` input blocking also blocks while `Facet.Input.BlocksGameInput`.

- [ ] **Step 1: Write the failing in-game checks** in a DEBUG-only self-test, `Quartz/UI/FacetUi/FacetSelfTest.cs`. It runs when `UserData/Quartz/facet-selftest` exists, logs PASS/FAIL to `UserData/Quartz/facet-selftest.log`, then quits.
  - Open an empty Facet window under `QuartzHost`, then one Escape: the Facet window closes, and `UICore.IsOpen` is unchanged.
  - Focus a Facet `TextField` and simulate the menu hotkey's check: the menu does not toggle.
- [ ] **Step 2: Run.** Back up the runtime store, then `dotnet build Quartz/Quartz.csproj -c Debug -p:LoaderTarget=ML -p:AutoInstall=true`. Create the selftest file, launch, and wait for the log's `DONE` line. Expected: FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run again.** Expected: `fail=0`.
- [ ] **Step 5: Commit** as `feat: give Quartz a Facet host that shares Escape, hotkeys and input blocking`.

### Task 3: Effect Remover on Facet

**Files:**
- Create: `Quartz/UI/FacetUi/FacetEffectRemover.cs`:
  - one `Facet.Surface` and `Window` titled with the page's localized title;
  - the rows of `Quartz/Features/EffectRemover/PageEffectRemover.cs` (325 lines): mode `Dropdown`, a `Section` per heading, a `Toggle` per effect;
  - each bound with `Bind.To(() => Conf.X, v => { Conf.X = v; MainCore.RequestSave(); })` through the live `MainCore.Conf` reference, never a captured settings object (Review Focus 1);
  - each with `Default` set from a fresh `EffectRemoverSettings`, so the changed-dot and middle-click reset work.
- Modify: `PageEffectRemover.cs` adds a button, "Open in the new menu (preview)", with a Lang key per `i18n.md`. It hides the uGUI menu, opens the Facet window, and shows the menu again from `CloseRequested`.
- Modify: wherever `UIColors.ApplyAccent` runs during a picker drag (`UIColors.cs:64` and `UICore.Theme.cs:24-33`), also call `QuartzHost.Theme.Recolour(accent)` (Review Focus 2).

- [ ] **Step 1: Write the failing self-test checks.**
  - The Facet page has one toggle per effect row of the uGUI page; compare the counts.
  - Clicking a toggle flips the setting, and the uGUI page shows it after a rebuild.
  - Setting the value behind the window shows within 150 ms.
  - A profile switch through `PageProfiles`' apply path shows the new profile's value within 150 ms, with the same `Control` element (Review Focus 1).
  - `ApplyAccent(red)` mid-drag recolours the Facet title bar in the same frame (Review Focus 2).
  - The `[perf]` first open is ≤ 50 ms after Facet's warm-up.
- [ ] **Step 2: Run.** Expected: FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run.** Expected: `fail=0`. Restore the runtime store backup if the build is not to be kept.
- [ ] **Step 5: Commit** as `feat: open Effect Remover in a Facet window as a preview`.
