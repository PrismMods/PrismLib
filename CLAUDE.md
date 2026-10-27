# PrismLib

Shared library under QuartzTeam's ADOFAI mods — Topaz, Sapphire, and Quartz later. Public repo
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
- Consuming mods refresh with `lib/update-prismlib.sh`; `deploy.sh` calls it when a DLL is missing.

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
- **Before Unity 6 a font does not inherit (ADOFAI 2.9 runs Unity 2022).** A font definition set on
  the root never reached the labels: each resolved no font and laid out 0px tall, so the window drew
  shapes and no text. Font loading was fine — set per element, both the SDF asset and a legacy Font
  draw. `Surface.HandDownFonts` stamps the nearest ancestor's font onto every text element on a 200ms
  schedule there (rows recycle, popups add elements later). Unity 2022 also has no 5-arg SDF family
  overload, so on macOS the font comes from files (Arial, Menlo — which also carries the panel icons
  Arial lacks, hence it is the sans face's fallback).
- **A panel with no font draws no text.** `TMP_FontAsset.sourceFontFile` is an editor-time reference
  and is null in a player build, so mods generally cannot supply a `Font`; `Surface` falls back to
  `Font.CreateDynamicFontFromOSFont` (on demand — not safe at `Time.frameCount == 0`).
- **A scrolling container's ANCESTORS must be allowed to shrink.** `Window.Body` and `Nav`'s panes
  set `flexShrink = 1` back on themselves, against the `Ui.Box` default, because they have to become
  whatever the window is rather than whatever their content is. Left at 0, the body grew to its
  content and the window merely clipped it: the debug log's viewport came out **2700px tall inside a
  580px window**, so the list reported that everything fitted and nothing scrolled. `minHeight = 0`
  alone is not enough — both are needed.
- **Flex items shrink by default, and `Ui.Box` turns that off.** The CSS default of `flexShrink: 1`
  assumes a layout that would rather squash than overflow; a settings page wants the opposite —
  something too tall should SCROLL, not compress until its text prints through the row below. Three
  separate overlaps came from it (the debug panel's header under its tab bar, a section's rows
  through the next section's heading, keybind labels through each other), so every `Ui.Box` and
  everything built on it now defaults to `flexShrink = 0`. Anything that genuinely should absorb
  slack sets it back to 1 on itself. A `ScrollView`'s `contentContainer` is not a `Box`, so it needs
  setting by hand.
- **A flex item's minimum size is its CONTENT, which silently disables scrolling.** A list taller
  than the window makes its container taller than the window too, so the window just clips it: no
  scrollbar, no wheel, and nothing in any log. Every scroll container AND every ancestor up to the
  fixed-height window needs `minHeight = 0` — `Window.Body`, the `ScrollView`, the `ListView`. An absolutely positioned element with only `left`/`top` is sized by its
  CONTENT — pin all four offsets.

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
from stage 1 finally has a renderer and `Search` spans them. **Alt+S** opens it (**Alt+D** for the debug window — not Ctrl+Shift+S, which is the editor's Save); the mods'
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

**Escape** closes the top Prism window and is consumed only when one was open. `Ui.Stack` orders
open surfaces; clicking a window raises it by taking a `sortingOrder` above the highest used, since
separate `UIDocument` panels order by that number and nothing else.

**Undo/redo (`History`) is session-wide and by VALUE.** Every widget records how to put its change
back and how to do it again; nothing snapshots a mod's settings object, which may be large and
diffing it per keystroke is both slower and less exact than remembering the one number. Two rules
keep it honest: `History.Applying` must suppress recording while an undo runs, or the widget's own
change handler pushes a fresh entry and undo becomes inescapable; and consecutive changes to the
same key within `CoalesceSeconds` merge, keeping the FIRST undo and the LAST redo, so a slider drag
is one step instead of sixty. `History.AfterApply` is how a panel re-reads what it shows —
without it the window keeps displaying the value the control had before.

**Keybind capture is polled by the MOD, not read from a UI event** — Tab, the arrows and Escape are
all worth binding and all get eaten as navigation inside a panel. `SettingRow.Capture` is the hook;
`PrismBridge.TickCapture` is the poller.

**Alt+letter does not arrive on macOS; Alt+SHIFT+letter does.** Option+D and Option+S compose a
character (∂, ß) and Unity never reports the letter's keycode — verified in-game, where Alt+Shift+S
worked and Alt+S did not, which is also why the first diagnostic saw `LeftAlt` and the letter
together yet the chord never fired. The chords are **Alt+Shift+D** and **Alt+Shift+S**, with
Ctrl+Shift+D and Ctrl+Shift+P as fallbacks. Ctrl+Shift+S is deliberately not one: that is the
editor's Save.

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

**Topaz's key block was a separate cause of the same symptom.** The diagnostic showed `LeftAlt` and the
letter both arriving, so macOS was not swallowing anything. `KeyLimiter.GetKeyDownPostfix` returns
false for every key but `B` while Topaz's panel is open, *including for Topaz's own poll*, so
the shared windows were unreachable from the moment that panel was up. An Alt chord now reads
through the block. `Ctrl+Shift+D` / `Ctrl+Shift+P` remain as fallbacks and `PrismBridge.AltDiag`
still logs `alt+<key>` for anything that comes up later.

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
open popup. `Ui.Stack` is kept in (layer, raise) order. A uGUI canvas sorted higher than the panel
still draws on top — Topaz's uGUI menu covers popups, which is correct, not a band failure.

**Popups** (`Popup.Open`, `Popup.Menu`) share one Popup-layer surface, sit under their anchor (above
it when there is no room), take the anchor's window scale, and close on an outside click (a
transparent catcher under the card), on Escape, when their window hides, or when the anchor leaves
the panel. Place from the PANEL's size — a just-shown surface has no layout yet.

**PrismLib has settings of its own** (`PrismLib.Config`: key=value in `<mods root>/PrismLib/settings.txt`,
beside the DLL). They are described under an owner "PrismLib" that is NOT a loaded mod, at
`Order = int.MinValue`, so they come first in Prism · Settings. The first is `fps.show`: the FPS
readout moved out of Topaz (and Quartz) into `FpsCounter` — bottom-right, where Topaz's sat — on
the new `Layer.Hud`, which is never in `Ui.Stack`, never counted as open, never closed by Escape and
never blocks input. Bridges call `FpsCounter.Tick(setting)` every frame; it counts a frame once even
with two mods ticking. Topaz carries an old `ShowFps = true` across once.

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

The resolver searches beside the mod, then the shared folder, then `<game>/UserLibs`. A MelonLoader
build should ship `PrismLib.UI.dll` in **UserLibs**, not `Mods` — MelonLoader scans `Mods/*.dll` for
a `MelonMod` and warns about anything that has none.

Nothing in PrismLib or PrismLib.UI references UMM. The mod's own entry point calls
`PrismBridge.Init`, from `OnLoad` under UMM or `OnInitializeMelon` under MelonLoader.

## Testing

There is no headless mode. On macOS anything visual is verified by a human reloading in-game (UMM
Ctrl+F10) and screenshotting. On the Linux/Hyprland machine it can be driven from a shell:
`steam -applaunch 977950`, wait for the mod's log line, `ydotool key …` for chords (Alt+Shift+D is
`56:1 42:1 32:1 32:0 42:0 56:0`; needs `ydotoold` running), `hyprctl dispatch 'hl.dsp.cursor.move({x=…, y=…})'`
to place the pointer, and `grim -o <output>` to screenshot. Pointer MOVES reach UI Toolkit (hover
works); `ydotool` button presses reach the GAME (they move the level-select planet) but never
register as clicks in UI Toolkit, so trigger a widget from code to test it. Either way: put logic that can be checked offline in `PrismLib.dll` and check it in
`tools/PrismLibTest.cs`; the game also swallows exceptions silently, so instrument rather than assume.

## Conventions

- Terse "why" comments only; no banners, no narration of the diff. Keep landmine substance.
- `CONFLICTS.md` is the cross-mod audit (C1–C15). **Gitignored on purpose** — it names bugs in
  another author's mod and is not public until they agree.
- Cross-mod facts live in this project's memory (`prismlib`, `versioning`, `conventions`).
