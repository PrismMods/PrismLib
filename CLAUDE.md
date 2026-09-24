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

## Dev loop

`./dev.sh` — build both assemblies, run the offline test, and install straight into the game:
`PrismLib.dll` where the bootstrapper would have put it, `PrismLib.UI.dll` into every mod folder
that already has a copy, and both into `../Sapphire/lib` and `../Bismuth/lib`. Then reload in-game
(Ctrl+F10).

**No release for a test.** The release path exists so end users get the library; it has no business
in the edit-test loop. Leave the version alone while iterating — the bootstrapper only replaces its
copy when the feed's version is strictly newer, so an unbumped dev build survives the next launch.
Cut a release when something is ready to ship, not to try it.

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

Unity **6000.3.211**, and the game ships `UnityEngine.UIElementsModule.dll`. Runtime UI Toolkit is
**verified working in-game** (2026-09-24): `ScriptableObject.CreateInstance<PanelSettings>()` + an
empty `ThemeStyleSheet` + `UIDocument` gives a live `rootVisualElement`, and the debug panel renders
with a working `ListView`. The real constraint: **USS and UXML cannot be authored at runtime** (that
importer is editor-only), so styling is inline C# and theme values are code tokens.

Three landmines, all found by the first panel that opened:

- **Never show/hide a panel with `SetActive`.** `UIDocument` rebuilds its visual tree in `OnEnable`,
  so the second show hands back a different `rootVisualElement` and everything built into the old
  one is orphaned — the panel comes back empty with no error. Use `display`.
- **A panel with no font draws no text.** `TMP_FontAsset.sourceFontFile` is an editor-time reference
  and is null in a player build, so mods generally cannot supply a `Font`; `Surface` falls back to
  `Font.CreateDynamicFontFromOSFont` (on demand — not safe at `Time.frameCount == 0`).
- **Flex items shrink by default.** Chrome next to something with `flexGrow` gets squeezed toward
  zero and its labels spill out and overlap. Rows set `flexShrink = 0`; so must any other fixed
  element. An absolutely positioned element with only `left`/`top` is sized by its CONTENT — pin all
  four offsets.

Migration is additive — new APIs land here, the mods adopt them screen by screen, nothing breaks at
once.

**Measured 2026-09-24:** ~13,000 lines of uGUI across the two mods, and the drift between their
copies is small — `UIBuilder` 615 differing lines of 4503, `UICore` 152 of 1312, `PageStack` 42 of
398, `Theme` 38 of 284, `DragHandle` 10 of 74. It is one framework copied twice, which is what makes
centralising worth it.

Ported so far: `Surface`, `Window` (drag/resize/scale/clamp), `Ui` primitives, `Tabs`, `Anim`,
`DebugPanel`, `Toast`. `Search` is in PrismLib.dll, not .UI — it is pure logic, so it is testable
offline and usable by the settings registry and dashboard too.

Order for the rest, each step leaving both mods building:

1. `Theme` → `Tokens`: mods push their accent in; delete two near-identical Theme files.
2. `PageStack` + the settings shell → a `Page`/`Nav` API over `Window`.
3. `UIBuilder` rows (toggle, slider, dropdown, colour, keybind) → `Ui` widgets. The big one; do it
   widget by widget, with Sapphire's settings page as the first screen to move.
4. `DragHandle` / `ResizeHandle` / `RoundedRectGraphic` / `PolyGraphic` → delete; `Window` and UI
   Toolkit's own styling replace them.
5. `FieldNav` → delete; UI Toolkit has focus navigation.
6. Mod-specific screens (Sapphire's editor palettes, Bismuth's `GameUiEditor`/`LocationEditor`) stay
   in their mods, built on the shared widgets.

Done: step 1 (both Themes push their palette into `Tokens`, so a shared window wears the colours of
the mod that opened it); Bismuth's `LogViewer` (268 lines) deleted — the shared debug window
replaced it.

Step 3 is under way: `Widgets` (labelled rows — toggle, slider, int slider, choice, text, action,
danger, header) and `SettingsPanel`, which renders whatever the mods DESCRIBE through
`ModHandle.DescribeSettings`. Both mods now declare a real slice of their settings, so the schema
from stage 1 finally has a renderer and `Search` spans them. **Ctrl+Shift+S** opens it; the mods'
own uGUI panels are untouched, so this is additive until each screen moves across.

`SettingsPanel` takes its own `SettingRow`, not `PrismLib.SettingEntry`, for the same reason
`DebugPanel` takes `DebugTab`: PrismLib.UI must not reference PrismLib.dll. The bridge converts.

Two widgets are drawn by hand rather than using UI Toolkit's own: `Toggle` (its `Toggle` is a tick
box) and `Choice` (its `DropdownField` menu). Both are styled by the theme style sheet a runtime mod
does not have, so they would come out unstyled.

**Steps 2, 4, 5 and 6 are blocked behind 3**, and doing them early breaks things rather than
helping: a page shell has nothing to put on it before the widgets exist, and `DragHandle`,
`ResizeHandle`, `RoundedRectGraphic`, `PolyGraphic` and `FieldNav` are still what every uGUI panel
in both mods is built from. Step 3 first, widget by widget.

**The update toast stays uGUI on purpose.** `ToastStack` is a cross-mod convention keyed on a
`Canvas` NAMED `<Mod>UpdateToastCanvas`, and Quartz already follows it. A UI Toolkit panel has no
Canvas, so porting the toast would make our card invisible to Quartz's scan and the two would
overlap again — the exact bug the convention exists to prevent. That is also why PrismLib.UI still
carries `RoundedRectGraphic`.

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
