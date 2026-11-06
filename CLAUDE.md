# PrismLib

Shared library under QuartzTeam's ADOFAI mods — Topaz, Sapphire, and Quartz later. Public repo
`PrismMods/PrismLib`, MIT (the mods that link it are GPL-3.0). Split out of Sapphire's workspace on
2026-09-24; the conversation history that produced it is in this project's transcript.

**Game facts** (scenes, state flags, Unity quirks, loader order, macOS signing and camera, how to
test unattended) live in `docs/game-knowledge.md`, shared by every Prism mod. Add to it whenever a
session verifies something about the game; say where and when it was verified.

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
- **`PrismLib.dll` ships too, as `PrismLib.bundle` — never as a `.dll`.** `Ensure` installs it
  into the shared folder when it is newer, so a first launch needs no network; the feed keeps
  updating it in the background. Named `.dll` it would sit where MelonLoader's UserLibs or
  UMMBridge load DLLs from, and a second PrismLib would load beside the shared one. A mod's
  `release.sh` runs `lib/update-prismlib.sh` first (SHA-checked against the feed), so it ships the
  latest PUBLISHED PrismLib and fails to build if it needs an unreleased API — release PrismLib
  first. Mods pass `modsRoot` and the bundle path to `Ensure` from their UMM `modEntry.Path`.
- `AssemblyVersion` in both `Properties/AssemblyInfo.cs` must track `Prism.Version` — the
  bootstrapper picks a copy by assembly version read from file metadata.
- `raw.githubusercontent` caches `prismlib.json` for a few minutes, so a just-published release is
  not visible to clients or to `lib/update-prismlib.sh` straight away.

## Dev loop

`./dev.sh` — build both assemblies, run the offline test, and install straight into the game:
`PrismLib.dll` where the bootstrapper would have put it, `PrismLib.UI.dll` into every mod folder
that already has a copy, and both into `../Sapphire/lib` and `../Topaz/lib`. Then reload in-game
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
- `./release.sh <ver>` also uploads `lib/Facet.0.2.dll` as a release asset and writes a `facet` entry
  (name, url, sha256) into `prismlib.json`.
- Consuming mods refresh with `lib/update-prismlib.sh` (which fetches `facet` when the feed has one);
  `deploy.sh` calls it when a DLL is missing, and fails if `lib/Facet.0.2.dll` is still missing.

## The shared debug window

`Alt+D` in every Prism mod — the same chord on purpose, because the window is shared: it
shows every registered mod's log and fields, not just the one whose key you pressed. Both mods poll
the chord and `StateKey.DebugPanel` decides which one draws, so two mods never stack identical
copies. With PrismLib absent a mod falls back to showing its own log alone.

Mods contribute through `ModHandle.AddLog(name, tail, path)` and `AddFields(name, read)`. Everything
is **pulled**: a source is a delegate the viewer calls on refresh, so a tab costs nothing while
nobody is looking. The `Prism` tab lists loaded mods, held claims and key conflicts — the answer to
"autoplay is broken" that used to need three log files and a guess.

`DebugPanel` takes plain strings and delegates, never PrismLib types, so `PrismLib.UI` keeps not
referencing `PrismLib.dll`.

**Fonts:** panels use `Surface`'s OS font and the log list a monospace. Do NOT feed them a mod's
`TMP_FontAsset.sourceFontFile` — that is the GAME's display face, which is unreadable as a wall of
log text and does not line its columns up.

**`Ui.AnyWindowOpen`** is how a mod knows to stand down: the editor zooms on the scroll wheel, so
scrolling a Prism list would also zoom the world behind it. Sapphire's `ZoomCamera` prefix checks
it. It cannot be enforced from the library — the input belongs to the game and the patch belongs to
the mod.

## The shared layout editor

One full-screen session for moving every Prism mod's on-screen elements: Quartz's repositioning
menu rebuilt on UI Toolkit (select, drag with edge/centre snapping, corner resize, rotation knob
snapping to quarter turns, nine-way anchor with Closest, X/Y/size/rotation fields, crop mode,
undo/redo). Spec: `docs/superpowers/specs/2026-11-06-camdisplay-design.md` Part 1.

- **Registry in `PrismLib.dll`** (`Layout`, `LayoutTarget`, `ModHandle.DescribeLayout`): every mod's
  targets must reach one editor, so it is a single shared instance. Plain delegates; a second
  `DescribeLayout` replaces the mod's list. A bridge calls `Layout.Unregister(<id>)` in Shutdown,
  next to `Diagnostics.Unregister`.
- **Editor in `PrismLib.UI`** (`LayoutEditor`, fed `EditTarget`s, which copy `LayoutTarget` field
  for field plus `Owner`). The `StateKey.LayoutEditor` claim holder builds it over a source that
  converts `Layout.All()`, opens it on `Layout.OpenRequested`, calls `Reload()` on `Layout.Changed`
  and `Tick()` every frame. Without PrismLib.dll a mod can still feed its own targets directly.
- **Conventions**: screen pixels, origin bottom-left; degrees counter-clockwise; anchors 0-8 in
  reading order from the top-left; crop `{left, bottom, right, top}` as fractions trimmed. The
  pure geometry is `LayoutMath` (BCL only, compiled straight into `tools/PrismLibTest.cs`).
- Boxes are re-read from `GetBox` every tick, never cached. Every call into a mod goes through one
  guard: a target whose mod went away is hidden and reported once, not every frame.
- History keys are per gesture (`Owner/Id#n`), never per label: `History` coalesces same-key
  entries within 0.9 s, and a label key would undo into whichever target is selected later. For the
  same reason the inspector does not use `Widgets.Text`/`Choice`, whose keys are `host/page/label#n`.
- **Test rig**: `tools/LayoutProbe` (UMM dev mod; `build.sh` after `./dev.sh`). F9 opens the editor
  over three boxes. An `autotest` file in its mod folder runs `AutoTest.cs` instead: synthetic
  UI Toolkit events against the real panel, PASS/FAIL lines in its `probe.log`, then the game
  quits. A still click must send down and up in the same frame, or the real mouse's own move event
  lands between them and turns it into a drag.

## Facet

`PrismLib.UI` stands on **Facet** (`../Facet`, its own repo; read `../Facet/README.md` and
`../Facet/CLAUDE.md`), a game-agnostic UI engine, `netstandard2.1`, shipped as `Facet.0.2.dll`.

- **What moved.** Surfaces and windows (`../Facet/Facet/Surface.cs`, `Window.cs`), theme and tokens
  (`Theme.cs`, `Tokens.cs`), input (`Input.cs`), key capture (`Core/KeyCapture.cs`),
  `Paint`, popups, the undo history, `Nav`, and the settings `Form` (`Form.cs`) now live in
  Facet. PrismLib.UI keeps every public type and signature and forwards to it; the Prism-only
  pieces stay here: `Words`, `Tokens` pushes from mods, `InputGate`, `DebugPanel`, the layout
  editor, `Toast`, `FpsCounter`, `RestartPrompt`.
- **Hosts.** `Toolkit/PrismHost.cs` makes the default host, named `PrismLib`, which wires Facet's log,
  language, geometry persistence and theme to PrismLib.UI's own `Ui.Log`, `Words`, `Window.Read/Write`
  and `Theme.Default`. A settings row is built under a per-owner host named `PrismLib/<owner>`,
  whose `Translate` calls `Words.T` with `Words.Owner` pinned to that owner (`Field.Owner` carries
  the name). One host per owner is what keeps `Surface.Relabel` and undo tooltips translating in the
  owner's table. `Host.Named` keeps the FIRST host of a name, and `Facet` is reserved.
- **Colours.** `Look` presets are copies; a literal colour written onto a Facet-built element sticks
  only after `Paint.Forget(e)`. `Ui.Text(color)` and `Ui.SetBorderColor` do that; do the same for
  any inline colour write on a Facet-built element.
- **Shipping.** `Facet.0.2.dll` ships wherever `PrismLib.UI.dll` ships: each UMM mod folder,
  `UserLibs/` for MelonLoader builds, and each mod's `lib/`. Never name or place it so that
  `PrismLib.dll`'s shared folder could load it. `./dev.sh` refreshes every copy it finds (the
  mod folders, `UserLibs/`, the standalone `UMMMods/Facet`, and each repo in `REPOS`, default Sapphire,
  Topaz and CamDisplay, space-separated, e.g. `REPOS="../Topaz-suite" ./dev.sh`; `REPOS=""` skips them all; a run from a worktree must pass REPOS explicitly) from PrismLib's
  `lib/`. It also puts `PrismLib.dll` into both shared folders that exist: `UMMMods/PrismLib/` and
  MelonLoader's `Mods/PrismLib/`. **The csproj alone names the Facet minor** (its `Reference Include`);
  every script reads it from there, so a new minor is a csproj edit plus `tools/update-facet.sh`.
  A stale `Facet.0.2.dll` elsewhere in the game folder loads first, so after `dev.sh` compare md5s.
  `tools/update-facet.sh` (`FACET_ROOT` names the tree, default `../Facet`) rebuilds Facet and refreshes PrismLib's own `lib/Facet.0.2.dll` only (dev
  machines; `dev.sh` calls it when the file is missing; it refuses a Facet tree with uncommitted
  changes unless `FORCE=1`). `release.sh <ver>` uploads it with the release and lists it
  in `prismlib.json` as `facet`, so a mod's `lib/update-prismlib.sh` fetches it with a SHA-256 check;
  until a release carries it, a mod's `lib/` gets it from PrismLib's `./dev.sh` on a dev machine.
- **The bootstrap carries Facet along.** `PrismBootstrap.SyncUi` gives every PrismLib.UI copy's
  folder (mod folders and `UserLibs/`) each `Facet.<major>.<minor>.dll` beside the newest copy that
  it lacks or has older, and `Resolve` answers `Facet.<major>.<minor>` from the mod's own folder,
  `UserLibs/`, then any sibling mod folder. Neither reads from or writes to the shared PrismLib
  folder. `tools/check-bootstrap.sh` (run by `release.sh`) pins both against a throwaway mods root.
- **Compatibility (R21).** A Facet patch release adds no public API; any new public member bumps the
  minor (a new file name). Within one minor the first loaded copy wins, so a Facet copy beside an
  older PrismLib.UI must be harmless.
- **Landmines.** The UI Toolkit landmines (SetActive, fonts, shrinking and scrolling containers,
  theme clipping, cursors, wheel, undo, Escape) now live in `../Facet/CLAUDE.md`, where Facet's
  rules keep them. Read it before touching a window's layout, fonts, scrolling or input.

## UI

Runtime UI Toolkit is Unity 6 only (3.x builds; see `docs/game-knowledge.md` for the 2.9 note), and
styling is inline C#, never USS/UXML. Everything about how Facet-built windows behave is in
`../Facet/CLAUDE.md`. Landmines specific to PrismLib.UI:

- **The first Prism window drawn in a session costs ~85 ms more than any after it** — first run of
  the window, widget and UI Toolkit code, not glyphs (with every glyph pre-rasterised it was still
  91 ms; a second settings window took 17). So Facet's first `Surface` schedules
  `Facet.WarmUp.Run`: one throwaway window drawn off screen for 100 ms, half a second after load.
  The debug window's ListView still pays ~25–40 ms of its own on first open. Measured by
  LayoutProbe's `Perf.cs` ([perf] lines), 2026-11-06.
- **Long read-only text (a status, a path)** goes in a `SettingControl.Info` row, a wrapped label: a
  single-line `TextField` cuts text off at its width.

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
6. Mod-specific screens (Sapphire's editor palettes, Topaz's `GameUiEditor`/`LocationEditor`) stay
   in their mods, built on the shared widgets.

Done: step 1 (both Themes push their palette into `Tokens`, so a shared window wears the colours of
the mod that opened it); Topaz's `LogViewer` (268 lines) deleted — the shared debug window
replaced it.

Step 3 is under way: `Widgets` (labelled rows — toggle, slider, int slider, choice, text, action,
danger, header) and `SettingsPanel`, which renders whatever the mods DESCRIBE through
`ModHandle.DescribeSettings`. Both mods now declare a real slice of their settings, so the schema
from stage 1 finally has a renderer and `Search` spans them. **Alt+P** opens it (**Alt+D** for the debug window; Prism chords are Alt+key because the editor owns Ctrl chords — Ctrl+S saves the level); the mods'
own uGUI panels are untouched, so this is additive until each screen moves across.

`SettingsPanel` takes its own `SettingRow`, not `PrismLib.SettingEntry`, for the same reason
`DebugPanel` takes `DebugTab`: PrismLib.UI must not reference PrismLib.dll. The bridge converts.

Widgets so far: `Toggle`, `Slider`, `IntSlider`, `Choice`, `Text`, `Action`, `Danger`, `Colour`,
`Key`, `Section`, `Header`, `Spacer`. Still to port from `UIBuilder`: `NavRow`/`NavCard`/`CardGrid`
(navigation shells — they belong with step 2), `Segmented`, `CycleSelector`, `AccentSwatches`.

Drawn by hand rather than using UI Toolkit's own: `Toggle` (its `Toggle` is a tick box), `Choice`
(its `DropdownField` menu) and `Colour`. The first two are styled by the theme style sheet a runtime
mod does not have, so they would come out unstyled; `Colour` is three channel sliders instead of
Sapphire's 485-line procedural wheel, because typing an exact value is easier than aiming at one.

**Cursors are drawn, and the swap is GLOBAL.** USS `cursor` keywords are editor-only and a runtime
mod has no cursor assets, so `Cursors` draws them in code and swaps through
`UnityEngine.Cursor.SetCursor` on pointer enter. There is no per-element cursor at runtime, so every
element that sets one must clear it on exit AND on `DetachFromPanelEvent` — a panel hidden while the
pointer is over it would otherwise strand the cursor in that shape. Only the ambiguous ones get one:
a resize edge and a text field look like anything else until the cursor says otherwise, while a
button already looks like a button and the pointing hand is a web convention, not a desktop one.

**Built-in controls have NO appearance.** UI Toolkit styles `Slider`, `TextField` and `ScrollView`
from the default theme style sheet, which is an asset a runtime mod cannot have. `Toggle` and the
dropdown were cheap to draw by hand; these are not — their dragging, focus and scrolling logic is
worth more than their looks — so `Skin` sets inline styles on their named parts
(`unity-base-slider__tracker`, `unity-base-slider__dragger`, `unity-base-text-field__input`). Those
names are Unity's: a rename makes a control invisible again, so every lookup is null-checked, and
`Skin.When` waits for `AttachToPanelEvent` because the parts do not exist until the control joins a
panel.

**Window geometry** persists through `Window.Read`/`Window.Write`, host-supplied delegates —
PrismLib.UI cannot own a file. Each mod writes `PrismWindows.txt` beside its own data, flat
`key=value`, flushed at most once a second because a drag writes on every frame. Deliberately not
the mod's XML settings: a mid-write crash must not be able to corrupt real settings.

**Escape** closes the top Prism window and is consumed only when one was open. Facet's internal
window stack (`Facet.Ui.Stack`) orders open surfaces; clicking a window raises it by taking a
`sortingOrder` above the highest used, since separate `UIDocument` panels order by that number and
nothing else.

**Undo/redo (`History`) is session-wide and by VALUE.** Every widget records how to put its change
back and how to do it again; nothing snapshots a mod's settings object, which may be large and
diffing it per keystroke is both slower and less exact than remembering the one number. Two rules
keep it honest: `History.Applying` must suppress recording while an undo runs, or the widget's own
change handler pushes a fresh entry and undo becomes inescapable; and consecutive changes to the
same key within `CoalesceSeconds` merge, keeping the FIRST undo and the LAST redo, so a slider drag
is one step instead of sixty. After an undo or redo Facet re-reads every visible surface's
controls in place (its `Refresher`), so a window shows the restored value without a rebuild;
`History.AfterApply` is only an extra hook for a mod's own panel.

**Keybind capture is polled by the MOD, not read from a UI event** — Tab, the arrows and Escape are
all worth binding and all get eaten as navigation inside a panel. `SettingRow.Capture` is the hook;
`PrismBridge.TickCapture` is the poller.

**On macOS Unity's `Input` drops some letters while Option is held, and which depends on the key.**
In-game on Unity 6 (2026-11-05), `AltDiag` (which scans every `GetKeyDown`) saw D and S arrive under
Option+Shift, but for Option+Shift+K and Option+Shift+P only `LeftAlt` and `LeftShift`. Plain
Option+D / Option+S never fired either. So `MacKeys` (PrismLib.UI)
reads the window server on a Mac (`CGEventSourceKeyState`, focus-gated, InputGate-respecting):
`Down` is a rising edge and must be polled **every frame**, `Held` ORs in the window server's
modifier flags so a chord landing in one frame lines up. Never read an Alt chord's letter through
`Input.GetKeyDown`. The chords (2026-11-06) are plain **Alt+key**: **Alt+D** debug, **Alt+P** Prism
settings, and each mod's own menu (**Alt+T** Topaz, **Alt+C** CamDisplay). No Ctrl fallbacks: the
game's editor owns the Ctrl chords (Ctrl+S saves the level).
`MacKeys` is newer than 0.3.10, so bridges call it behind a NoInlining wrapper and a catch (another
mod's older PrismLib.UI may be the loaded one), falling back to `Input`.

**`WheelEvent` delivery differs BETWEEN panels, so the question is per-frame, not ever.** The
settings page receives them and the debug list does not. A single "the host sends wheel events"
flag, set by the first panel to see one, therefore switched polling off for the list and stopped the
log scrolling. `Ui.TickWheel` now stands down only on frames where one actually arrived.
`Skin.WatchWheel` registers with `TrickleDown` because a `ScrollView` that handles the wheel stops
it bubbling — a bubbling handler would never hear about the case it exists to detect.

Three separate causes hid behind one symptom, which is why "scrolling is broken" took so many
passes. All were found by logging numbers, none by reading code:

- **A trackpad reports fractions.** The measured delta was `-0.05`; at 40px per unit that moved the
  page two pixels, which looks exactly like nothing.
- **`RuntimePanelUtils.ScreenToPanel` divides by the panel scale and does NOT flip y.** Measured:
  screen `(1606,300)` came back as panel `(1141,213)`, exactly `/1.405`. The flip belongs to the
  caller.
- **A virtualised `ListView` never grows its content container** — that is what virtualising means —
  so measuring `contentContainer.height` says a 53-row log has nothing to scroll. Ask the list for
  its item count instead.

**Nothing in a built-in control is positioned without the theme.** Both a slider's tracker AND its
dragger are laid out by the default style sheet, so each has to be placed by hand against the drag
container's midline. Fixing only the dragger moves the mismatch instead of removing it — the track
was not centred either.

**Topaz's key block was a separate cause of the same symptom.** The diagnostic showed `LeftAlt` and
the letter both arriving for D and S (not for every letter; see above). `KeyLimiter.GetKeyDownPostfix` returns
false for every key but `T` while Topaz's panel is open, *including for Topaz's own poll*, so
the shared windows were unreachable from the moment that panel was up. An Alt chord now reads
through the block (Topaz's own Alt+T included). `PrismBridge.AltDiag` still logs `alt+<key>` for
anything that comes up later.

**`DescribeSettings` REPLACES** everything a mod described; it does not append. Build the list and
hand it over once.

**Fonts load from FILES, then by family name** (`Surface.FileFontAsset`). A family+style FontAsset only
works while TextCore can find that family again — it reloads the face by name whenever it adds
glyphs, and on Steam Linux Runtime that reload asks for "DejaVu Sans" / "Regular", which
fontconfig's "Book"-tagged DejaVu does not answer to: the asset built fine, then every label drew
nothing. Inside the runtime container the game sees its own DejaVu under `/usr/share/fonts` and the
host's fonts under `/run/host/usr/share/fonts`. Every panel also gets one shared `PanelTextSettings`
whose default font is ours and whose fallback list is empty — left null, UI Toolkit falls back to a
built-in default that does the same failing OS lookup.

**Colours are per window (`Theme`); sizes stay global (`Tokens`).** Widgets read
`Theme.For(element)`: the nearest themed ancestor (a Surface, a popup card that took its anchor's),
else the `Theme.Use` scope being built, else `Theme.Default`, which IS the Tokens fields — so a
bridge's `PushTokens` still works. Panels build their window in the constructor, so bridges build
them inside their own scheme (`Wearing(() => new SettingsPanel(…))`); setting `.Theme` afterwards
misses the chrome. `Theme.FromAccent` is Quartz's derivation. Each mod keeps its palette in a
`Theme` refreshed by `PushTokens` (Topaz amber, Sapphire blue, Quartz red).

**Text goes through `Words.T`** — `Ui.Text`, `Ui.Btn`, row labels and tooltips, setting rows on
intake — so page code never calls it. English is the key. **A string is translated only by the
table of whoever wrote it**: English keys carry no context, so CamDisplay's "Undo" (remove the
camera patch) once came out as the editor's 실행 취소 from PrismLib's table. PrismLib.UI's own
Korean table answers only `Words.Own`/`OwnF`, which PrismLib's code calls for its own literals
(write new ones that way), plus rows PrismLib registers itself and the shared "Prism · Settings"/
"Prism · Debug" titles. A settings row is built with `Words.Owner` set to its mod, so only that
mod's translator answers (a translator belongs to the assembly its delegate was compiled in, which
is named after the mod), and a mod with no table shows English. Outside rows, every mod's
translator (`Words.AddTranslator(Loc.T)`) answers in order. `Words.Language` comes from the
bridge's language setting. A
duplicate key in that table's initializer would throw from the static constructor and take every
string with it — check with `grep -o '{ "[^"]*",' Toolkit/Words.cs | sort | uniq -d`.

`Theme` and `Words` are newer than 0.3.10: bridges reach them behind NoInlining + catch and hold
a scheme as `object`, or an older copy loaded by another mod would stop the class loading.

**Fonts are sampled at 64px, not at the size they draw.** 14–16px SDF masters with 9px padding were
magnified by the panel scale (1.4 at 1440p) and a Retina backing scale, and every Prism window's text
came out blurry; 64/6 fixed it (confirmed in-game 2026-11-05). Keep padding near a tenth of the size.

**Bold is a real face, not faux bold.** The bold file is attached as weight 700
(`fontWeightTable[7].regularTypeface`); without it UI Toolkit dilates the regular SDF glyphs, which
came out smeared on every header.

**UMM's window showed blank or borrowed text on Linux — fixed by `FontFix`.** UMM's IMGUI font is a
dynamic OS font ("DejaVu Sans"); TextCore builds a FontAsset for it with the style hard-coded to
"Regular", and fontconfig tags DejaVu's regular weight "Book" (often the only family in the Steam
runtime). Every label failed every frame — ~10,000 "Unable to find a font file" lines a second — and
the window, with nothing of its own to draw, showed whatever TextCore generated last (a Prism label,
then the FPS counter on every label). `FontFix` patches `FontAsset.CreateFontAsset(family, style,
size)` (through the mod's Harmony, like InputGate): a "Regular" miss retries "Book"/"Normal"/…, and
every result, misses included, is cached. Measured: errors 10k/s → 0, UMM text back.

**Layers.** `Surface` takes a `Layer` (Window, Popup, Modal, Tooltip); each owns a band of
`PanelSettings.sortingOrder` (30000/31000/31500/32000), so raising a window never lifts it over an
open popup. Facet's window stack is kept in (layer, raise) order. A uGUI canvas sorted higher than
the panel still draws on top — Topaz's uGUI menu covers popups, which is correct, not a band failure.

**Popups** (`Popup.Open`, `Popup.Menu`) share one Popup-layer surface, sit under their anchor (above
it when there is no room), take the anchor's window scale, and close on an outside click (a
transparent catcher under the card), on Escape, when their window hides, or when the anchor leaves
the panel. Place from the PANEL's size — a just-shown surface has no layout yet.

**PrismLib has settings of its own** (`PrismLib.Config`: key=value in `<mods root>/PrismLib/settings.txt`,
beside the DLL). They are described under an owner "PrismLib" that is NOT a loaded mod, at
`Order = int.MinValue`, so they come first in Prism · Settings. The first is `fps.show`: the FPS
readout moved out of Topaz (and Quartz) into `FpsCounter` — bottom-right, where Topaz's sat — on
the new `Layer.Hud`, which is never in Facet's window stack, never counted as open, never closed by
Escape and never blocks input. Bridges call `FpsCounter.Tick(setting)` every frame; it counts a frame
once even with two mods ticking. Topaz carries an old `ShowFps = true` across once.

**Performance, measured 2026-10-25** (frame-time probe, Linux, fast CPU): windows add nothing per
frame once built (10.0 ms avg / 12–14 ms worst at 100 FPS, open or closed); first open is one
80–100 ms spike. The debug window used to re-read, re-split and `Rebuild()` its list every second —
now it skips while the tab's file and the view are unchanged and `RefreshItems()` in place, with line
numbers formatted only for bound rows. The big Linux lag source was the font failure above (UMM's
window, and PrismLib's own panels before fonts were loaded from files) — ~10,000 log lines a second.
Changing UMM's `UIFont` does not help (in the runtime UMM resets it to the only family it can see),
and neither does `GUI.skin.font` (UMM overwrites it in ScaleGUI).

**Windows keep keys and the wheel from the game** (`InputGate`, `Surface.BlocksGameInput`, on by
default; `DebugPanel`/`SettingsPanel` pass it through). While any visible surface has it, Unity's
`GetKey`/`GetKeyDown`/`GetKeyUp`, `mouseScrollDelta` and scroll axes, and RDInput's
`GetMain`/`WentDown`/`IsDown`/`GetState` read as nothing pressed. PrismLib.UI has no Harmony, so each
mod calls `InputGate.EnsureInstalled(id, postfix)` every frame with a delegate that patches through
its own Harmony — the first wins, and `Uninstalled(id)` on master-off lets the other take over. Each
mod runs its own UI update and the bridge tick inside `InputGate.Exempt`, or the Escape/chord/wheel
polls that drive Prism would be blocked too; autoplay is a registered bypass. Verified in-game: with
a window open, presses neither move the level-select planet nor open the pause menu.

- **A mod's own uGUI menu holds the same gate with `InputGate.Block(id, active)`** (Quartz's menu,
  for "block inputs while the menu is open"). Same id replaces; `Unblock` removes. It still needs
  `AddMouseExemption` for its own clicks and `Exempt` around its own key polls.
- **The key that closes a window is still down for the rest of that frame.** A hiding surface holds
  the gate shut until the frame ends, or Escape closed the window and then paused the game behind it.
- **Engine calls (icalls) cannot be patched** on the game's Mono + UMM Harmony 2.3.6 ("Invalid IL
  code"). `GetKey(KeyCode)` & co. are managed wrappers and patch fine; `GetMouseButton*` and
  `anyKeyDown` ARE icalls. So mouse buttons are gated at the CALLERS: an IL scan finds every
  Assembly-CSharp method calling them (18 on the 2026-10-22 build, incl. Rewired's
  `UnityInputSource`, which feeds uGUI) and the mod transpiles each call to `InputGate.MouseButton*`
  (patch kind `"redirect"`). `anyKeyDown` stays ungated.
- **UI Toolkit reads the same `Input` calls**, but only through
  `DefaultEventSystem.LegacyInputProcessor.Input`; every method of that wrapper runs exempt
  (prefix + finalizer), so windows keep their clicks and wheel. A mod's own uGUI panels stay
  clickable through `AddMouseExemption` (Sapphire: `PanelKit.AnyPanelHovered`, Topaz: its menu).
- Verified in-game: with a window open, clicks no longer move the level-select planet. NOT verified:
  real clicks on Prism controls — `ydotool` presses never reach UI Toolkit, gate or no gate.
- A mod's own postfix on a gated method may run BEFORE the gate's and still see the key (Topaz's
  `KeyLimiter press` log does) — that is not the game seeing it.

**Escape is answered once** (`Ui.HandleEscape`; `CloseTop` routes to it): the topmost visible
surface takes it — a focused text field in it blurs first, then its `EscapeHandler` (the popup host
closes only its newest popup), else it hides. At most once per frame, because every mod's bridge
polls Escape and calls it; a mod that spent the key on a keybind capture says so with
`Ui.ConsumeEscape`. The game still sees Escape (pause menu) — the key is the game's.

**Steps 2, 4, 5 and 6 are blocked behind 3**, and doing them early breaks things rather than
helping: a page shell has nothing to put on it before the widgets exist, and `DragHandle`,
`ResizeHandle`, `RoundedRectGraphic`, `PolyGraphic` and `FieldNav` are still what every uGUI panel
in both mods is built from. Step 3 first, widget by widget.

**The update toast stays uGUI on purpose.** `ToastStack` is a cross-mod convention keyed on a
`Canvas` NAMED `<Mod>UpdateToastCanvas`, and Quartz already follows it. A UI Toolkit panel has no
Canvas, so porting the toast would make our card invisible to Quartz's scan and the two would
overlap again — the exact bug the convention exists to prevent. That is also why PrismLib.UI still
carries `RoundedRectGraphic`.

## Both loaders

UMM gives each mod a folder (`<game>/UMMMods/<Mod>/<Mod>.dll`); **MelonLoader mods are flat DLLs in
`<game>/Mods`**. `SharedDir` decides the mods root by NAME (`Mods`, `UMMMods`, `Plugins`) rather
than by walking up a fixed number of levels, which is what it used to do — that put PrismLib in the
game root for a MelonLoader mod. Verified by running `Ensure` from a DLL placed in each layout
(`/tmp/loadercheck`), not by reading the code.

A mod whose code does not run from its own folder passes the root: `Ensure(log, minimum, modsRoot)`.
Quartz loads from a versioned runtime store (`…/Runtime/versions/<ver>/`), where the guess put
PrismLib at `…/Runtime/versions/PrismLib` — reproduced, and fixed by the anchor, in the same kind of
layout check (2026-11-05). `UserLibs` is `<modsRoot>/../UserLibs` either way.

The resolver searches beside the mod, then the shared folder, then `<game>/UserLibs`. A MelonLoader
build should ship `PrismLib.UI.dll` in **UserLibs**, not `Mods` — MelonLoader scans `Mods/*.dll` for
a `MelonMod` and warns about anything that has none.

Nothing in PrismLib or PrismLib.UI references UMM. The mod's own entry point calls
`PrismBridge.Init`, from `OnLoad` under UMM or `OnInitializeMelon` under MelonLoader.

## Testing

There is no headless mode. On macOS anything visual is verified by a human reloading in-game (UMM
Ctrl+F10) and screenshotting. On the Linux/Hyprland machine it can be driven from a shell:
`steam -applaunch 977950`, wait for the mod's log line, `ydotool key …` for chords (Alt+D is
`56:1 32:1 32:0 56:0`; needs `ydotoold` running), `hyprctl dispatch 'hl.dsp.cursor.move({x=…, y=…})'`
to place the pointer, and `grim -o <output>` to screenshot. Pointer MOVES reach UI Toolkit (hover
works); `ydotool` button presses reach the GAME (they move the level-select planet) but never
register as clicks in UI Toolkit, so trigger a widget from code to test it. Either way: put logic that can be checked offline in `PrismLib.dll` and check it in
`tools/PrismLibTest.cs`; the game also swallows exceptions silently, so instrument rather than assume.

## Conventions

- Terse "why" comments only; no banners, no narration of the diff. Keep landmine substance.
- `CONFLICTS.md` is the cross-mod audit (C1–C15). **Gitignored on purpose** — it names bugs in
  another author's mod and is not public until they agree.
- Cross-mod facts live in this project's memory (`prismlib`, `versioning`, `conventions`).

## Commits

Read `agents/commits.md` before any commit or push (format, splitting, no AI co-author lines).
