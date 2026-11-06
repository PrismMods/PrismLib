# PrismLib.UI on Facet (milestone 2) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move PrismLib.UI onto Facet one layer at a time. Prism · Settings is the first screen; Topaz's menu is the second. Every public type and signature is kept, and every mod builds and passes its suites at every commit.

**Architecture:**
- PrismLib.UI references `Facet.0.1.dll`, which ships beside it in every mod.
- Its general-purpose files become thin layers over Facet, all under **one Facet host named "PrismLib"**:
  - the host's `Log` is `Ui.Log`;
  - its `Translate` is `Words.T`, so the existing owner rules still apply;
  - its `Read`/`Write` are `Window.Read`/`Window.Write`.
- The ADOFAI-specific files stay and build on those layers: `InputGate`, `MacKeys`, `FontFix`, `DebugPanel`, `LayoutEditor*`, `FpsCounter`, `RestartPrompt`.

**Deviation from the spec, on purpose:**
- The spec says "one default host per mod set up by the bridge". PrismLib.UI is a single first-loaded assembly with global statics, so it cannot tell which mod is calling.
- One "PrismLib" host keeps today's behaviour exactly.
- Per-mod hosts arrive when a mod builds pages on Facet directly (milestone 3 for Quartz).

**Tech Stack:**
- xbuild (.NET Framework 4.8) for PrismLib, referencing **`netstandard2.1`** Facet (ruling R8 in milestone 1: the Unity 2022.3 reference assemblies need 2.1) through the game's `netstandard.dll` facade, which is already referenced. Task 1 must prove xbuild accepts that reference (CS1705 risk) before anything else;
- `mcs`/`mono` offline test; LayoutProbe and CamDisplay in-game suites.

**Spec:** `PrismLib/docs/superpowers/specs/2026-11-06-facet-ui-engine-design.md` § "Milestone 2 in more detail". It depends on `2026-11-06-4-facet-engine.md` being done: Facet 0.1 builds and its autotest passes.

## Global Constraints

**API rules:**
- **PrismLib.UI keeps every public type and signature.** Append only. No mod source changes are needed except the packaging lines in Task 1 and the matcher lines in Task 4.
- No PrismLib type in a field of a mod's bridge class. `PrismLib.UI` and `Facet` types in fields are fine, because both ship beside the mod.
- `Facet.0.1.dll` ships wherever `PrismLib.UI.dll` ships: each UMM mod folder, `UserLibs/` for MelonLoader builds, and each mod's `lib/`. It is never named or placed so that `PrismLib.dll`'s shared folder could load it.

**Verification:**
- **The suite after every task:**
  - `./dev.sh` prints `ALL PASS` and installs;
  - copy `PrismLib.UI/bin/Release/PrismLib.UI.dll` and `Facet.0.1.dll` to `UserLibs/` and `CamDisplay/lib/`, and confirm with `md5` that every copy matches;
  - `tools/check-bridge.sh` passes for Topaz, Sapphire and CamDisplay;
  - `tools/LayoutProbe/build.sh` plus its autotest gives `fail=0`, and `[perf]` first open is ≤ 50 ms;
  - `CamDisplay/deploy.sh ML` plus its `selftest` passes.
- One in-game test at a time; wait 5 s between a quit and a launch.

**Commits:** Conventional Commits, split finely, **no Co-Authored-By trailer**. A change to an API the bridges call says so in the body. Push nothing. The version bump is the user's call.

## Review Focus

1. **Two mods ship different PrismLib.UI builds and only the first loads.** The loser's `Facet.0.1.dll` copy must not matter: one Facet 0.1 loads, and everything resolves to it. Pinned in Task 1's check-bridge run, with Facet present beside an older PrismLib.UI.
2. **A mod pushes `Tokens` colours after a window is built** (Topaz's `Theme.PushTokens` on accent change). Windows built afterwards wear the new colours, as today. Pinned in Task 2.
3. **Escape while a Prism window and a Facet-native window are both open.** Exactly one closes per press: the topmost. Pinned in Task 2.
4. **Undo of a Widgets toggle in Topaz's menu** updates the switch in place without a page rebuild, and redo works after it. Pinned in Task 5.
5. **Prism · Settings search in Korean**, typing "ㅍ" with the language set to "ko", finds "FPS 표시". Pinned in Task 3.

---

### Task 1: Ship Facet beside PrismLib.UI, unused

**Files:**
- Create: `PrismLib/lib/Facet.0.1.dll` (built output) and `tools/update-facet.sh`, which copies `../Facet/Facet/bin/Release/netstandard2.0/Facet.0.1.dll` into `lib/` after a `dotnet build`.
- Modify: `PrismLib.UI/PrismLib.UI.csproj`: `<Reference Include="Facet.0.1"><HintPath>../lib/Facet.0.1.dll</HintPath><Private>False</Private></Reference>`.
- Modify: `dev.sh`: wherever it copies `PrismLib.UI.dll` (mod folders, `../Sapphire/lib`, `../Topaz/lib`), copy `lib/Facet.0.1.dll` too.
- Modify: `tools/check-bridge.sh`: copy a sibling `Facet.*.dll` beside the mod as it does `PrismLib.UI.dll` (lines 86-91).
- Modify, in the mod repositories:
  - `Topaz/release.sh` and `Sapphire/release.sh`: next to `cp lib/PrismLib.UI.dll` (lines 54/55);
  - `CamDisplay/release.sh`: lines 41 and 43;
  - `CamDisplay/deploy.sh`: lines 21 and 29.
  - Each also copies `lib/Facet.0.1.dll` to the same destination.

**Interfaces:** None new. PrismLib.UI only references Facet here.

- [ ] **Step 1: Write the failing check.** Add a probe to the `check-bridge.sh` harness: with `Facet.0.1.dll` present beside the mod, `Assembly.Load("Facet.0.1")` succeeds from the mod's folder. Run it for Topaz. Expected: FAIL, because nothing copies Facet yet.
- [ ] **Step 2: Make the changes above.** Run `./dev.sh`; run `tools/check-bridge.sh` for all three mods.
- [ ] **Step 3: Check where Facet resolves from.** Under UMM, the mod folder's DLLs are registered by name. Under MelonLoader, `UserLibs` loads first.
  - If a mod's `PrismBridge` fails to resolve `Facet.0.1` from its folder in-game (an `[ui]` log line or a `TypeLoadException`), extend `bootstrap/PrismBootstrap.cs:Resolve` to answer names starting with `Facet.` from the same three directories.
  - Otherwise leave the bootstrapper alone.
- [ ] **Step 4: Run the full suite** (Global Constraints). Expected: all pass, nothing visible changes.
- [ ] **Step 5: Commit, one commit per repository:**
  - PrismLib: `build: ship Facet beside PrismLib.UI`;
  - each mod: `build: ship Facet with the mod`. The body notes that PrismLib.UI now needs it.

### Task 2: Surfaces, windows, theme and input on Facet

**Files:**
- Create: `PrismLib.UI/Toolkit/PrismHost.cs`: `internal static Facet.Host Instance`. It is created lazily with `Log = m => Ui.Log(m)`, `Translate = Words.T`, `Language = Words.Language`, and `Read`/`Write` through `Window.Read`/`Window.Write`.
- Modify into thin layers: `Surface.cs`, `Ui.cs` (stack, Escape, queries, wheel, primitives), `Window.cs`, `Theme.cs`, `Tokens.cs` (sizes copied into `Facet.Tokens` at host creation), `Anim.cs`, `Cursors.cs`, `Skin.cs`, `Popup.cs`, `Tabs.cs`, `Tex.cs`, `History.cs`.
- Modify: `SettingsPanel.cs`: `WarmUp()` calls `Facet.WarmUp.Run()`, and `WarmedUp` returns `Facet.WarmUp.Done`. LayoutProbe's `Perf.cs:23` reads `WarmedUp`.
- Modify: `InputGate.cs`: reads `Facet.Input.HeldThisFrame` and `Facet.Input.BlocksGameInput`.

**Interfaces:**
- **`PrismLib.UI.Toolkit.Surface`** wraps a `Facet.Surface` created under `PrismHost.Instance`. `Root`, `Layer`, `Name`, `Visible`, `BlocksGameInput`, `BringToFront`, `SetFont`, `Dispose`, `Mono()`, `LogGeometryOnce` and `Hidden` forward to it. Its `Theme` property maps through `Theme.Twin`.
- **`Ui`:**
  - `Stack`, `OpenWindows`, `AnyWindowOpen`, `BlocksGameInput`, `PointerOverWindow`, `TextFocused`, `TickWheel`, `HandleEscape`, `ConsumeEscape`, `CloseTop` forward to `Facet.Input`.
  - `Box`, `Row`, `Card`, `Text`, `Muted`, `Btn`, `Flat`, `Highlight`, `List`, `Set*`, `Lighten` forward to `Facet.Ui`/`Facet.Paint`.
  - `Ui.Text`'s `Color? color` argument: when non-null, it sets the colour inline after `Facet.Ui.Text`. When null, it uses `Role.Text`.
  - `Ui.Log` stays a public static field.
- **`Theme`** holds an `internal Facet.Theme Twin`. It is not named `Facet`, which would shadow the namespace inside the class.
  - `Theme.Default`'s twin is refreshed from the `Tokens` colour fields whenever a surface is created, or when `Theme.Use`/`For` returns it. That keeps "pushed `Tokens` show in windows built afterwards" (Review Focus 2).
  - `Recolour`, `CopyFrom` and each colour property write through to the twin. A live re-theme therefore happens for PrismLib themes created with `FromAccent`.
- **`Window`** wraps `Facet.Window`. Its `onClose` constructor argument subscribes to `CloseRequested`. `Read`/`Write` remain public static fields read by `PrismHost`.
- **`History`** forwards every member to `Facet.History`, so undo spans both.

- [ ] **Step 1: Write the failing in-game checks** in `tools/LayoutProbe/AutoTest.cs`.
  - `Ui.Stack[0]` is backed by a `Facet.Surface` (via `PrismHost`): `Facet.Input.AnyWindowOpen` agrees with `Ui.AnyWindowOpen` with the editor open.
  - With the layout editor open and a Facet-native test window opened above it, one `Ui.HandleEscape()` closes only the Facet window, and a second closes the editor (Review Focus 3).
  - `Tokens.Accent = red; new SettingsPanel(…)`: its title bar's accent-derived elements use red.
- [ ] **Step 2: Run the LayoutProbe autotest.** Expected: FAIL.
- [ ] **Step 3: Implement** the layers listed above. Delete each forwarded body; keep the "why" comments where the behaviour now lives in Facet as a one-line pointer: "see Facet.Surface".
- [ ] **Step 4: Run the full suite.** Expected: all pass, and `[perf]` first open ≤ 50 ms.
- [ ] **Step 5: Commit** as `refactor: stand PrismLib.UI's surfaces, windows, theme and input on Facet`. Split into one commit per layer only if each still builds.

### Task 3: Prism · Settings on Facet

**Files:**
- Modify: `PrismLib.UI/Toolkit/SettingsPanel.cs`, schema mode.
  - Each `SettingRow` is converted to a `Facet.Field` (`Kind = (FieldKind)row.Kind`, `Owner`, `Page`, `Group`, `Label`, `Tooltip`, `Get`, `Set`, `Min`, `Max`, `Options`, `ActionLabel`, `Capture`).
  - Each page draws with `Facet.Form.Build`.
  - The tabs stay, so there is no visible layout change.
  - Search uses `Facet.Search.Rate` over the label, group, page and tooltip in both languages, with initials. The result is the same flat list with "owner · page · group" headers.
  - `Matcher` stays as a public field and is consulted only when a host sets it to something non-default.
- Modify: `PrismLib/PrismLib/Search.cs`: `[Obsolete("Facet.Search replaces this; kept for API compatibility")]` on the class.
- Modify, in the mod repositories, `Topaz` and `Sapphire` `Util/PrismBridge.cs`: stop assigning `SearchIndex.Scorer` and `SettingsPanel.Matcher` from `PrismLib.Search`.

**Interfaces:**
- Consumes: `Facet.Field`, `Facet.FieldKind`, `Facet.Form.Build`, `Facet.Search`.
- `SettingControl`'s nine members map 1:1 onto the first nine `FieldKind` members.

- [ ] **Step 1: Write the failing in-game checks** in LayoutProbe.
  - A schema `SettingsPanel` with rows of every kind: each row's control is found by `Id = Label`.
  - A row whose `Get` throws shows "— unavailable", and the rows after it build.
  - Search "fps" lists "Show FPS".
  - With `Words.Language` returning "ko", search "ㅍ" lists "FPS 표시" (Review Focus 5).
  - A value changed behind the panel shows within 150 ms without `Reload()`.
- [ ] **Step 2: Run the LayoutProbe autotest.** Expected: FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run the full suite**, plus the CamDisplay self-test, which drives a schema `SettingsPanel`. Expected: all pass; `[perf]` ≤ 50 ms.
- [ ] **Step 5: Commit:**
  - PrismLib: `feat: draw Prism settings with Facet forms and search`, then `chore: mark PrismLib.Search obsolete`. The body names the bridge lines that change.
  - Topaz and Sapphire: `refactor: stop passing PrismLib's matcher to the settings window`.

### Task 4: Widgets and Nav on Facet (Topaz's menu)

**Files:**
- Modify: `PrismLib.UI/Toolkit/Widgets.cs`. Every public method keeps its signature and calls the matching `Facet.Controls` method:
  - `value` plus `onChange` becomes `Bind.Value(value, onChange)`;
  - `tooltip` becomes `o => o.Tooltip = tooltip`;
  - `Toggle` returns the switch element (`control.Input`);
  - `Slider` returns the `Slider`; `Text` returns the `TextField`, and so on, as today.
- Modify: `Nav.cs`. `PrismLib.UI.Toolkit.Nav` wraps `Facet.Nav`:
  - `NavItem`s are converted on construction and on `Refresh`; `Title`, `Tooltip`, `Build`, `Children`, `Revision` and `Group` are copied.
  - `Nav.Active` forwards to `Facet.Nav.Active`.
  - `Filter`, `Go`, `Push`, `Pop`, `Refresh`, `Tick`, `ToggleRail`, `OnInsetChanged`, `RailOpen` forward.
- Modify: `SearchIndex.cs` becomes a layer over the open `Facet.Nav`'s index.
- Modify: `ColourPicker.cs` and `GradientEditor.cs` forward to Facet's copies.

**Interfaces:**
- Consumes: `Facet.Controls.*`, `Facet.Nav`, `Facet.NavItem`, `Facet.Bind`.

- [ ] **Step 1: Write the failing in-game checks** in LayoutProbe.
  - A nav-mode `SettingsPanel` built from Topaz-shaped pages: two `NavItem.Heading`s, a `ToggleGroup`, a `ToggleSubPage`, a `Section`, and a page with a `Revision`. Each opens.
  - The SubPage pushes, and the back row returns.
  - A `Revision` change rebuilds the page within one tick.
  - Search finds a row on a page never visited.
  - Only the first page built at construction, counted in the builds.
- [ ] **Step 2: Run the LayoutProbe autotest.** Expected: FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run the full suite.** Also open Topaz's menu (Alt+T) once in-game through the autotest's `SettingsPanel` path: LayoutProbe can build `new SettingsPanel("Topaz", () => PrismMenuShape())`.
- [ ] **Step 5: Commit** as `refactor: build PrismLib.UI's widgets and nav on Facet controls`.

### Task 5: Undo refreshes in place

**Files:**
- Modify: `SettingsPanel.cs`. It stops setting `History.AfterApply = () => _nav.Refresh()` (line 155). Facet's refresher and each control's own undo apply cover it.

- [ ] **Step 1: Write the failing in-game check.**
  - Toggle a `Widgets.Toggle` in a nav-mode panel, then `History.Undo()`.
  - The same switch element shows the old state: the element reference is unchanged, so there was no page rebuild.
  - `History.Redo()` restores it (Review Focus 4).
- [ ] **Step 2: Run.** Expected: FAIL, because the element was replaced by the rebuild.
- [ ] **Step 3: Remove the rebuild.**
- [ ] **Step 4: Run the full suite.** Expected: all pass.
- [ ] **Step 5: Commit** as `perf: let undo update controls in place instead of rebuilding the page`.

### Task 6: Docs

**Files:**
- Modify `PrismLib/CLAUDE.md`:
  - a "Facet" section: what moved, the PrismLib host, the shipping rule for `Facet.0.1.dll`, and that the § UI landmines now live in `Facet/CLAUDE.md`, with the § UI text trimmed to pointers;
  - fix "ADOFAI 2.9 runs Unity 2022" (line 152) to match `docs/game-knowledge.md`.
- Modify: `PrismLib/README.md`: a dependency line naming Facet.

- [ ] **Step 1: Check.** `grep -n "2.9 runs Unity 2022" CLAUDE.md` prints nothing, and every Facet file named in the new section exists in `../Facet`.
- [ ] **Step 2: Commit** as `docs: record that PrismLib.UI stands on Facet`.

## Notes from the Facet milestone 1 final review (2026-11-06) — read before Task 1

Facet 0.1 as built differs from what this plan assumed in these places. Each task's implementer must apply the matching note.

- **Target framework** (Task 1): Facet is `netstandard2.1`. Prove the xbuild/net48 reference first.
- **Renamed or reshaped Facet API** (Tasks 2 and 4 map these):
  - `Window`'s `onClose` constructor argument is gone: subscribe to `Window.CloseRequested` (no subscriber = close hides).
  - `Surface.Mono()` is gone: `Fonts.Mono(host)`.
  - `Theme.Default` is gone: `Theme.Dark` is frozen (its indexer and `Recolour` throw). PrismLib's `Theme.Default` twin must be a `new Facet.Theme()` owned by the PrismLib host, refreshed from `Tokens`. Indexer writes raise no `Changed`; only `Recolour` re-themes live.
  - `SearchIndex` is per-Nav (`nav.Index`, null until the first `Filter`); `Hit.Group` is `Hit.Section`. PrismLib.UI's public `SearchIndex.BeginPage/EndPage/Note` and `Scorer` become no-ops kept for API compatibility (ruling R18).
  - Facet's `Words` statics (`Language`, `AddTranslator`, `Owner`) are gone; PrismLib.UI keeps its own `Words`.
  - `Look` presets are copies; a consumer's literal colour on a Facet-built element sticks only after `Paint.Forget(e)`. PrismLib's `Ui.Text(Color? color)` and `Ui.SetBorderColor` forwarders must call `Paint.Forget` before writing the colour. Audit DebugPanel, LayoutEditor*, FpsCounter and RestartPrompt for inline colour writes on Facet-built elements.
- **Per-owner translation** (Task 2 and 3, a design change to "one PrismLib host"): with one host, `Form.Build`'s `Host.Named(field.Owner)` finds nothing for mod names and a later `Surface.Relabel` translates with no owner context — the CamDisplay "Undo" class of bug comes back. Create one Facet host per row owner, named `"PrismLib/" + owner` (ruling R19: the first host per name wins, and "Facet" is reserved), whose `Translate` calls PrismLib's `Words.T` with `Words.Owner` pinned to that owner; set `Field.Owner` to that host name.
- **Task 3's unavailable check**: Facet shows a getter failure as a dimmed control with `Unavailable == true` and the tooltip "unavailable, see the log"; the "— unavailable" text row appears only when building the row throws. Assert that, not the text.
- **Distinct types need sync, not a one-time conversion** (Task 4): the gradient editor mutates its stops in place, so a wrapper must write every change back; Facet's `Nav` matches items by reference and fixes its roots at construction, so "converted on Refresh" must update the existing Facet `NavItem`s in place.
- **Compatibility rule** (all tasks; ruling R21): a Facet patch release adds no public API; a new public member bumps the minor. Within one minor, the first loaded copy wins.
- **Input gate** (Task 2): `Facet.Input.BlocksGameInput` is allocation-free as of the fix wave, so `InputGate.Blocking` may read it on the per-key path.
