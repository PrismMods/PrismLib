# PrismLib

Shared library under QuartzTeam's ADOFAI mods — Bismuth, Sapphire, and Quartz later. Public repo
`PrismMods/PrismLib`, MIT (the mods that link it are GPL-3.0). Split out of Sapphire's workspace on
2026-09-24; the conversation history that produced it is in this project's transcript.

## Why it exists

The three mods write the same globals (`RDC.auto`, no-fail, the editor camera, the path lock, HUD
visibility) and read the same keyboard, and none of them can see the others doing it. That is not a
conflict any loader detects — it surfaces as "autoplay is broken" with three suspects. PrismLib is
the arbiter, and increasingly the shared interface.

Direction: grow into a Quartz-Addons-style host plus a Creative-Cloud-style dashboard — central
management for logs, player data, save data and fields, so the mods feel like one suite.

## Two assemblies, and the rule that decides which

**Does the thing need a SINGLE SHARED INSTANCE across mods?**

- **Yes → `PrismLib.dll`.** Claims, keys, settings schema, and anything the dashboard aggregates.
  Plain BCL — no Unity, Harmony or game reference — so it loads before anything and its logic is
  testable outside the game (`tools/PrismLibTest.cs`). Auto-installed into `<mods root>/PrismLib/`
  by `bootstrap/PrismBootstrap.cs`, so **every call site needs an `Available` guard**.
- **No → `PrismLib.UI.dll`.** References Unity/TMP. **Shipped inside each mod folder** as an
  ordinary dependency, so its call sites need no guard. Two mods each drawing their own toast is
  correct; `ToastStack` coordinates them by GameObject name, not shared memory.

## Landmines

- **No PrismLib type may appear in a FIELD of a mod's bridge class.** Field types are part of the
  class layout and resolve when the type loads — before any method runs, so before `Ensure()` can
  install the library. This shipped and took both mods down with
  `OnToggle: TypeLoadException`. Hold handles as `object`, cast at use. A **`PrismLib.UI` type in a
  field is fine** — that assembly ships beside the mod, so it is always there.
  `tools/check-bridge.sh <Mod.dll> <Namespace.PrismBridge> [ungated methods...]` catches both
  failure modes: it loads a built mod with `PrismLib.dll` absent but `PrismLib.UI.dll` present (the
  real shape of a failed install), and JIT-compiles each named method that the mod calls without
  checking `Available`:

  ```
  ./tools/check-bridge.sh …/Sapphire.dll Sapphire.PrismBridge Init Shutdown TickDebug ToggleDebug DebugTabs
  ```
- **No caller outside the bridge may mention a PrismLib type** — the CLR resolves a method's types
  when that method is JITted.
- **Each mod's `release.sh` must copy `lib/PrismLib.UI.dll` into the zip.** Leaving it out breaks
  released builds the same way, on users' machines instead of yours.
- `AssemblyVersion` in both `Properties/AssemblyInfo.cs` must track `Prism.Version` — the
  bootstrapper picks a copy by assembly version read from file metadata.
- `raw.githubusercontent` caches `prismlib.json` for a few minutes, so a just-published release is
  not visible to clients or to `lib/update-prismlib.sh` straight away.

## Build & release

- `./release.sh` — builds both projects, runs `tools/PrismLibTest.cs` (offline, asserts the
  claim/key/search rules), and compile-checks the bootstrap standalone. **xbuild, never
  `dotnet build`** (no v4.8 reference assemblies in the modern SDK).
- `./release.sh <ver>` also stamps both AssemblyInfos and `Prism.Version`, and regenerates
  `prismlib.json`. Then upload BOTH DLLs to the `v<ver>` GitHub release and push.
- **Version steps by 0.0.1** unless the change is genuinely major. The user decides versions.
- Consuming mods refresh with `lib/update-prismlib.sh`; `deploy.sh` calls it when a DLL is missing.

## The shared debug window

`Ctrl+Shift+D` in every Prism mod — the same chord on purpose, because the window is shared: it
shows every registered mod's log and fields, not just the one whose key you pressed. Both mods poll
the chord and `StateKey.DebugPanel` decides which one draws, so two mods never stack identical
copies. With PrismLib absent a mod falls back to showing its own log alone.

Mods contribute through `ModHandle.AddLog(name, tail, path)` and `AddFields(name, read)`. Everything
is **pulled**: a source is a delegate the viewer calls on refresh, so a tab costs nothing while
nobody is looking. The `Prism` tab lists loaded mods, held claims and key conflicts — the answer to
"autoplay is broken" that used to need three log files and a guess.

`DebugPanel` takes plain strings and delegates, never PrismLib types, so `PrismLib.UI` keeps not
referencing `PrismLib.dll`.

## UI

Unity **6000.3.211**, and the game ships `UnityEngine.UIElementsModule.dll` — `PanelSettings`,
`UIDocument`, `ThemeStyleSheet`, `PanelTextSettings` all present. UI Toolkit is the chosen backend
for the new framework. The real constraint: **USS and UXML cannot be authored at runtime** (that
importer is editor-only), so styling is inline C# and theme values are code tokens.

Migration is additive — new APIs land here, the mods adopt them screen by screen, nothing breaks at
once. The uGUI framework (`UICore`/`UIBuilder`/`TabRail`/`PageStack`/`Theme`/`PanelKit`) still lives
in both Bismuth and Sapphire, in copies that have drifted.

## Testing

The game cannot be scripted — it is a paid Steam title with no headless mode. Anything visual is
verified by a human reloading in-game (UMM Ctrl+F10) and screenshotting. So: put logic that can be
checked offline in `PrismLib.dll` and check it in `tools/PrismLibTest.cs`; the game also swallows
exceptions silently, so instrument rather than assume.

## Conventions

- Terse "why" comments only; no banners, no narration of the diff. Keep landmine substance.
- `CONFLICTS.md` is the cross-mod audit (C1–C15). **Gitignored on purpose** — it names bugs in
  another author's mod and is not public until they agree.
- Cross-mod facts live in this project's memory (`prismlib`, `versioning`, `conventions`).
