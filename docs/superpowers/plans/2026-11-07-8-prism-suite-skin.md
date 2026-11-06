# The PrismLib.UI suite skin (plan 8) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Dress every Prism window (Prism · Settings, the debug window, the layout editor chrome) and the update toast in the suite look, add the suite-wide `anim.duration` setting, and move PrismLib, Topaz, Sapphire and CamDisplay from `Facet.0.1.dll` to `Facet.0.2.dll` together.

**Architecture:**
- **The skin is one PrismLib.UI class, `Suite`.** It dresses a `Facet.Theme` through plan 7's hooks: the suite's neutral roles, `Theme.Sizes` and `Theme.Looks`. Every theme PrismLib.UI makes (`Theme.Default`, `new Theme()`, `Theme.FromAccent`) is dressed when it is made, so every Prism window wears the skin, and a theme another host made never does.
- **The neutrals belong to the suite.** A mod's `PushTokens` now reaches Prism windows only through `Tokens.Accent`. Sizes still come from `Tokens`, whose defaults become the suite's.
- **`anim.duration` lives in `PrismLib.dll`**, like `fps.show` (`Prism.AnimKey`, `Prism.AnimDuration`). Each bridge copies it every frame into PrismLib.UI's `Anim.Duration`, which writes Facet's shared `Anim.Speed = ms / 120f`.

**Tech Stack:** xbuild (net48) for PrismLib and the mods; `Facet.0.2.dll` (`netstandard2.1`, plan 7); `mcs` + `mono` for `tools/PrismLibTest.cs`, `tools/check-bootstrap.sh` and `tools/check-bridge.sh`; the LayoutProbe autotest and CamDisplay's DEBUG self-test in-game (macOS rig: MelonLoader + UMMBridge).

**Spec:** `PrismLib/docs/superpowers/specs/2026-11-07-prism-suite-ui-design.md`: "The suite look", "PrismLib's part", "Version", "Constraints", "Testing", and calls 1, 2 and 8. Mockups: `2026-11-07-prism-suite-ui/look-a.html`, option A. The Facet 0.2 API used here is plan 7's Interfaces blocks (`2026-11-07-7-facet-0.2.md`), with its names used verbatim. Read `PrismLib/CLAUDE.md` whole, and `Facet/CLAUDE.md` before touching layout.

## Global Constraints

- **Prerequisite: plan 7 is complete.** Facet branch `facet-0.2` must have every task committed, a clean tree, `./test.sh` at `ALL PASS` and the Gallery at `fail=0`. Its worktree is `/Users/preluminance/Coding/PrismMods/Facet-0.2`.
  - Build from it with `FACET_ROOT=../Facet-0.2`. If the user has merged it, use `FACET_ROOT=../Facet` instead.
  - Facet is read-only here.
- **Worktrees** (the user's overnight rule). Branch `suite-ui` comes from each repo's `main`, in a worktree beside that repo:
  - `/Users/preluminance/Coding/PrismMods/PrismLib-suite` (main `0b1eec0`);
  - `Topaz-suite` (`8051b93`);
  - `Sapphire-suite` (`5af1d44`);
  - `CamDisplay-suite` (`f0e92ea`).

  Never commit on `main` and never edit a main checkout; every path below is inside the worktrees. Merges, pushes and releases are the user's call.
- **`dev.sh` never writes into a main checkout.** Always run it as `REPOS="../Topaz-suite ../Sapphire-suite ../CamDisplay-suite" ./dev.sh`. Task 1 adds `REPOS`.
- **Off limits:**
  - Topaz's `AGENTS.md` and `agents/`;
  - Sapphire's `CLAUDE.md`, `.gitignore` and `agents/`;
  - Quartz (a later plan);
  - Facet.
- **Commits follow each repo's own rules.** Read them before the first commit there:
  - PrismLib `agents/commits.md`: split finely. A bridge-facing API change names the bridge methods in its body.
  - Topaz `/Users/preluminance/Coding/PrismMods/Topaz/agents/commits.md`: coarsest, one commit per area. The file is untracked, so read it from the main checkout.
  - Sapphire `/Users/preluminance/Coding/PrismMods/Sapphire/agents/commits.md`: moderate, one commit per feature. Also untracked; read it from the main checkout.
  - CamDisplay `agents/commits.md`: split finely.

  Every repo: Conventional subjects, **no Co-Authored-By trailer and no AI attribution**, and push nothing.
- **Versions are the user's call.** Every version stays as it is: `Prism.Version` and both AssemblyInfos at 0.3.12, and each mod's `Info.json` and `VERSION.txt`.
  - Never run `release.sh <ver>` or Topaz's `release.sh`, which always bumps.
  - Never run a mod's `lib/update-prismlib.sh`: it would replace `lib/` with the published, pre-Facet 0.3.12.
- **API rules.**
  - PrismLib.UI keeps every public type and signature, appends only, and never references `PrismLib.dll`.
  - No PrismLib type goes in a bridge field.
  - Every new bridge call into PrismLib, or into PrismLib.UI API newer than 0.3.12, is a `NoInlining` method behind a catch.
- **The look** (spec "The suite look"): the neutrals in Task 4's table, 2px corners, 1px lines, the accent only on state, and fades and slides only. A literal colour on a Facet-built element goes through `Paint.Forget` first; `Ui.Text(color)` and `Ui.SetBorderColor` already do that.
- **Motion.** `anim.duration` is in ms, 0–400, default 120. Facet's shared speed is `ms / 120`. It governs Facet-built windows only (decision 8): not the uGUI toast and not any uGUI menu.
- **Text.** Every new user-facing string is an English key with a Korean entry in `PrismLib.UI/Toolkit/Words.cs`. `grep -o '{ "[^"]*",' PrismLib.UI/Toolkit/Words.cs | sort | uniq -d` must print nothing.
- **Performance.**
  - Perf.cs's check (`[perf] settings A first open`) stays under 50 ms.
  - Closed windows cost nothing per frame beyond the bridges' existing ticks.
  - The anim tick allocates nothing.
- **The in-game rig** (memory `mac-ingame-test-rig`). `G` is `$HOME/Library/Application Support/Steam/steamapps/common/A Dance of Fire and Ice`.
  - One game at a time. Wait 5 s after a quit before the next launch, and never quit a game you did not launch.
  - The game install is shared. `dev.sh`, the mods' `deploy.sh` and LayoutProbe's `build.sh` all write into it, so run them only during your in-game slot.
  - **LayoutProbe run:**
    1. `tools/LayoutProbe/build.sh && touch "$G/UMMMods/LayoutProbe/autotest" && open steam://rungameid/977950`.
    2. Wait up to 400 s for `AUTOTEST DONE` in `$G/UMMMods/LayoutProbe/probe.log`.
    3. `rm "$G/UMMMods/LayoutProbe/autotest"`.

    A pass is `AUTOTEST DONE pass=N fail=0`.
  - **CamDisplay run:**
    1. `./deploy.sh ML && touch "$G/UserData/CamDisplay/selftest" && open steam://rungameid/977950`.
    2. Wait for `SELFTEST DONE` in `$G/UserData/CamDisplay/CamDisplayLog.txt`.
    3. Remove the `selftest` file.

    A pass is `fail=0`.
  - Choose LOCAL in Steam's cloud-sync dialog and press Ignore Limit on Screen Time; both are the user's standing orders (memory `autonomous-overnight-runs`). Report a launch that times out with no log instead of looping on it (R26). Take no screenshots.
  - If an existing check fails only because Facet 0.2 changed a part's structure, fix the check's selector and never its assertion.

## Review Focus

1. **A mod still pushes its whole palette through `Tokens`.** Topaz and Sapphire do today, released builds included, and Sapphire's Danger is brighter. Prism windows must keep the suite's neutrals and take only the accent. Pinned in Task 4.
2. **`settings.txt` holds garbage**, hand-edited or written by another PrismLib: `anim.duration=fast`, `-50`, `100000`, an empty value, `120.5`. The value is clamped to 0–400, otherwise it reads as 120, and nothing throws. Pinned in Task 6.
3. **A player updated only one mod.** One folder has the new PrismLib.UI with `Facet.0.2.dll`; another has an older PrismLib.UI with only `Facet.0.1.dll`. After `SyncUi`:
   - every copy has `Facet.0.2.dll`, and `Facet.0.1.dll` stays;
   - each minor resolves by its name;
   - the shared folder is untouched.

   Pinned in Task 2.
4. **Another host's window on the same Facet** (the Gallery, Quartz's Facet page) keeps Facet's own sizes and colours: the suite is PrismLib's alone. Pinned in Task 4.
5. **The loaded `PrismLib.dll` or `PrismLib.UI.dll` is older than the bridge** (another mod's copy, or a Windows file lock). The anim tick turns itself off after one miss and the mod keeps running. The tick also JITs without PrismLib. Pinned in Task 6: `TickAnim` joins check-bridge's ungated list.

---

## File structure

```
PrismLib-suite/
  PrismLib.UI/Toolkit/Suite.cs               new   the suite skin: neutral roles, Sizes, Looks, the palette
  PrismLib.UI/Toolkit/Theme.cs Tokens.cs PrismHost.cs Anim.cs SettingsPanel.cs Words.cs
      LayoutEditor.cs LayoutEditor.Grips.cs LayoutEditor.Inspector.cs                     modified
  PrismLib.UI/Toast.cs, PrismLib.UI/PrismLib.UI.csproj                                    modified
  PrismLib/Prism.cs                                                                        modified
  lib/Facet.0.1.dll -> lib/Facet.0.2.dll                                                   replaced (tracked)
  dev.sh release.sh tools/update-facet.sh tools/check-bridge.sh tools/check-bootstrap.sh   modified
  tools/PrismLibTest.cs, tools/LayoutProbe/{AutoTest.cs,build.sh}                          modified
  tools/LayoutProbe/AutoTest.Suite.cs        new   partial AutoTest: SuiteSkin, SuiteChrome, SuiteMotion
  CLAUDE.md README.md docs/superpowers/plans/roadmap.md                                    modified
  bootstrap/PrismBootstrap.cs                only if Task 2 goes red
Topaz-suite/       deploy.sh release.sh Topaz/Util/PrismBridge.cs
Sapphire-suite/    deploy.sh release.sh Sapphire/Util/PrismBridge.cs
CamDisplay-suite/  deploy.sh release.sh CamDisplay/Util/PrismBridge.cs CamDisplay/Dev/SelfTest.cs
```

## What plan 10 (the new Quartz menu shell) consumes

- **A Facet-native host wears the suite** through `PrismLib.UI.Toolkit.Suite.Wear(Facet.Theme t) -> Facet.Theme` (Task 4).
  - It sets every neutral role, `t.Sizes` and `t.Looks` to the suite's, and returns `t`. It leaves `Accent`, `Focus` and `AccentText` alone.
  - Quartz's host: `var t = Suite.Wear(new Facet.Theme()); t[Role.Accent] = c; t[Role.Focus] = c;`, then `o.Theme = t`.
  - A later accent change sets both roles, then calls `t.Notify()`.
  - Sizes and looks apply when a part builds (plan 7's ruling).
- **A PrismLib.UI window wears it with no call:**
  - under `Theme.Default`, whose accent is `Tokens.Accent`;
  - or built inside `using (Theme.Use(new Theme { Accent = c }))`.
- **The setting** (Task 6):
  - its key is `PrismLib.Prism.AnimKey` = `"anim.duration"`, in `settings.txt` beside the loaded `PrismLib.dll`;
  - its value is `PrismLib.Prism.AnimDuration`: int ms, 0–400, default 120;
  - it is applied through `PrismLib.UI.Toolkit.Anim.Duration`, an int in ms.

  A bridge copies it every frame. Topaz's `PrismBridge.TickAnim` is the model, and check-bridge lists it as ungated.
- **`tools/check-bridge.sh` loads the Facet minor that `PrismLib.UI.csproj` names**, which is 0.2. Quartz's plan-6 build (Facet 0.1) must move to 0.2 before it uses this check.
- **Not provided:** a request to open Prism · Settings. PrismLib has only `Diagnostics.RequestToggle` and `Layout.RequestOpen`. Quartz's Settings link to the animation setting will need one.

---

### Task 1: PrismLib builds and ships on Facet 0.2

**Files:**
- Modify: `PrismLib.UI/PrismLib.UI.csproj:47` → `<Reference Include="Facet.0.2"><HintPath>../lib/Facet.0.2.dll</HintPath><Private>False</Private></Reference>`.
- Replace: `git rm lib/Facet.0.1.dll`; add `lib/Facet.0.2.dll`.
- Modify: `PrismLib.UI/Toolkit/PrismHost.cs:18-24` (delete the `Facet.Tokens` copy and its comment) and the header comment in `Tokens.cs`.
- Modify: `tools/update-facet.sh`, `dev.sh`, `release.sh:7,21`, `tools/check-bridge.sh:51-53,96-99`, `tools/LayoutProbe/build.sh:15,17` and `tools/LayoutProbe/AutoTest.cs:380-382`.
- Modify: `CLAUDE.md` (the Facet section) and `README.md:135`.

**Interfaces:**
- **The csproj alone names the Facet minor.** Every PrismLib script reads it with:

  ```bash
  FACET=$(sed -n 's:.*<Reference Include="\(Facet\.[0-9]*\.[0-9]*\)".*:\1:p' PrismLib.UI/PrismLib.UI.csproj)
  ```

  This gives `Facet.0.2`. LayoutProbe's `build.sh` reads `../../PrismLib.UI/PrismLib.UI.csproj`.
- **`tools/update-facet.sh`:**
  - builds `${FACET_ROOT:-../Facet}` and copies `Facet/bin/Release/netstandard2.1/$FACET.dll` into `lib/`;
  - exits 1 with `<root> does not build $FACET.dll` when that file is absent (a 0.1 tree).
- **`dev.sh`:**
  - `REPOS` is a space-separated list of repos whose `lib/` gets the build. Its default is today's list: `../Sapphire ../Topaz ../CamDisplay ../sapphire ../topaz`.
  - `$FACET.dll` goes beside every `PrismLib.UI.dll` it installs (mod folders, `UserLibs/`, each `REPOS` `lib/`), and it still refreshes a standalone copy (`UMMMods/Facet`).
  - `PrismLib.dll` goes into every `$GAME/UMMMods/PrismLib/` and `$GAME/Mods/PrismLib/` that exists. Today the MelonLoader shared copy (`Mods/PrismLib/`, used by CamDisplay's ML build) is left stale.
- **`release.sh`:** `FACETDLL="lib/$FACET.dll"`, so the feed's `facet.name` becomes `Facet.0.2.dll`. A mod's `lib/update-prismlib.sh` already takes the name from the feed.
- **`check-bridge.sh`:**
  - copies `<mod dir>/$FACET.dll`, else `lib/$FACET.dll`;
  - the probe loads `$FACET`, passed in the environment, and prints `ok   Facet.0.2 loads from …`.
- **LayoutProbe's single-copy check** becomes:

  ```csharp
  string want = typeof(Ui).Assembly.GetReferencedAssemblies().First(n => n.Name.StartsWith("Facet.")).Name;
  var loaded = AppDomain.CurrentDomain.GetAssemblies().Where(x => Regex.IsMatch(x.GetName().Name, @"^Facet\.\d+\.\d+$")).ToList();
  Check(want == "Facet.0.2", "PrismLib.UI stands on Facet.0.2", want);
  Check(loaded.GroupBy(x => x.GetName().Name).All(g => g.Count() == 1) && loaded.Any(x => x.GetName().Name == want),
        "one copy of each loaded Facet minor, PrismLib.UI's among them",
        string.Join(" | ", loaded.Select(x => x.GetName().Name + "@" + x.Location).ToArray()));
  ```

  Quartz's Facet 0.1 page and the Gallery may load the other minor beside it.

- [ ] **Step 1: Create the worktrees.** From `/Users/preluminance/Coding/PrismMods`, run for each repo `git -C <Repo> worktree add ../<Repo>-suite -b suite-ui main`, where `<Repo>` is PrismLib, Topaz, Sapphire and CamDisplay. Each `git -C <Repo>-suite status --short` must be empty.
- [ ] **Step 2: Baseline.** In `PrismLib-suite`, run `./release.sh` with no version. Expected: `ALL PASS`, `bootstrap compiles standalone` and `bootstrap: ALL PASS`.
- [ ] **Step 3: Point the build at Facet 0.2 and turn it red.**
  - Change the csproj reference and `tools/update-facet.sh`.
  - Run `FACET_ROOT=../Facet-0.2 tools/update-facet.sh`. Expected: `lib/Facet.0.2.dll <md5>`.
  - `git rm lib/Facet.0.1.dll`.
  - Run `xbuild /p:Configuration=Release PrismLib.UI/PrismLib.UI.csproj`. Expected: error CS0234, `Tokens` does not exist in the namespace `Facet` (`PrismHost.cs:18`).
- [ ] **Step 4: Implement** the PrismHost deletion, then `dev.sh`, `release.sh`, `check-bridge.sh`, LayoutProbe's `build.sh` and its check, as above.
- [ ] **Step 5: Offline.**
  - `./release.sh`. Expected: `ALL PASS` and `bootstrap: ALL PASS`.
  - `git grep -n 'Facet\.0\.1' -- '*.sh' '*.cs' '*.csproj'`. Expected: only `tools/check-bootstrap.sh`'s fixtures.
- [ ] **Step 6: In-game baseline** (your slot).
  - Run `REPOS=… ./dev.sh`.
  - Run `tools/check-bridge.sh "$G/UMMMods/Topaz/Topaz.dll" Topaz.PrismBridge Init Shutdown TickDebug ToggleDebug DebugTabs`, and the same for `$G/UMMMods/Sapphire/Sapphire.dll` with `Sapphire.PrismBridge`. Both are main builds. Expected: `ok   Facet.0.2 loads` and `OK`.
  - Do the LayoutProbe run. Expected:
    - `fail=0`;
    - `PASS PrismLib.UI stands on Facet.0.2`;
    - `PASS one copy of each loaded Facet minor`;
    - first open under 50 ms.

    Nothing looks different: Facet 0.2's default sizes are today's `Tokens` values (Title 19, Radius 3, Body 15, Small 13, ListRowHeight 24).
- [ ] **Step 7: Docs.**
  - `CLAUDE.md`'s Facet section names `Facet.0.2.dll` wherever it names the shipped file. It also says that the csproj names the minor and the scripts read it from there, and it describes `REPOS` and the two shared folders.
  - `README.md:135` names 0.2.
- [ ] **Step 8: Commit** in PrismLib:
  - `build: build PrismLib.UI on Facet 0.2`: the csproj, the `lib/` swap and PrismHost. The body names the Facet commit (`git -C ../Facet-0.2 rev-parse --short HEAD`), says PrismLib.UI's public API is unchanged, and says the mods must ship `Facet.0.2.dll`.
  - `build: read the Facet minor from PrismLib.UI's project in every script`.
  - `build: let dev.sh skip main checkouts and refresh the MelonLoader shared copy`.
  - `test: check one copy of each loaded Facet minor`.
  - `docs: describe shipping Facet 0.2`.

### Task 2: Two Facet minors side by side in the bootstrap

**Files:**
- Modify: `tools/check-bootstrap.sh`.
- Modify only if a check goes red: `bootstrap/PrismBootstrap.cs`, plus the identical copies `Topaz/Util/PrismBootstrap.cs`, `Sapphire/Util/PrismBootstrap.cs` and `CamDisplay/Util/PrismBootstrap.cs` in their worktrees.

**Interfaces:**
- No new members. `SyncUi` spreads every `Facet.<major>.<minor>.dll` beside the newest PrismLib.UI copy, and `Resolve` answers by the requested name. These checks pin both for two minors.
- The four copies of `PrismBootstrap.cs` stay byte-identical (`diff -q`).

- [ ] **Step 1: Write the checks** (Review Focus 3).
  - Add `fake "$W/f/Facet.0.2.dll" 0.2.0.0`. Put it beside ModA's PrismLib.UI (the newest) next to its `Facet.0.1.dll`. ModB keeps its older PrismLib.UI with only `Facet.0.1.dll`.
  - The `resolve` helper takes the name.
  - Checks:
    - `Resolve("Facet.0.2")` called from ModB before the sync finds ModA's copy, and never the shared folder's.
    - After `SyncUi`, ModB, ModC and `UserLibs` each hold `Facet.0.2.dll` at 0.2.0.0 and still hold `Facet.0.1.dll`.
    - `Resolve("Facet.0.1")` and `Resolve("Facet.0.2")` return two assemblies, named `Facet.0.1` and `Facet.0.2`, from different files.
    - The existing checks still hold: the shared folder is unchanged, and no `.new` file is left.
- [ ] **Step 2: Run `tools/check-bootstrap.sh`.** Expected: `bootstrap: ALL PASS` with no code change; both functions already go by name.
  - If a check fails, fix `PrismBootstrap.cs` and copy it over the three mods' copies.
  - Then `diff -q` all four, run the script again, and commit `fix: …` in each repo.
- [ ] **Step 3: Commit** `test: pin the bootstrap with two Facet minors side by side`.

### Task 3: Topaz, Sapphire and CamDisplay ship Facet 0.2

**Files:**
- Topaz-suite: `deploy.sh:9,10,16` and `release.sh:28,56`.
- Sapphire-suite: `deploy.sh:25,26,60` and `release.sh:19,57`.
- CamDisplay-suite: `deploy.sh:11,12,22,30` and `release.sh:15,44,46`.

**Interfaces:**
- Every `Facet.0.1` in these scripts becomes `Facet.0.2`; nothing else changes.
- `lib/update-prismlib.sh` stays as it is.
- Ignore rules already cover `lib/Facet.0.2.dll`: Topaz's `.gitignore`, Sapphire's shared `.git/info/exclude`, and CamDisplay's `lib/*.dll`.

- [ ] **Step 1: The red is a grep.** In each worktree, `git grep -n 'Facet\.0\.1'` lists the lines above.
  - Do not run `deploy.sh` before the change. With `lib/Facet.0.1.dll` absent, it runs `lib/update-prismlib.sh` and replaces `lib/` with the published, pre-Facet PrismLib.
- [ ] **Step 2: Replace.** `git grep -n 'Facet\.0\.1'` now prints nothing, and `bash -n` passes on each edited script.
- [ ] **Step 3: Fill `lib/`.** Run `REPOS=… ./dev.sh` in `PrismLib-suite`. Each mod's `lib/` then holds the suite build's `PrismLib.dll`, `PrismLib.UI.dll` and `Facet.0.2.dll`, and `git -C <wt> status --short` stays empty.
- [ ] **Step 4: Build and install** (your slot), from each worktree root: Topaz `./deploy.sh`, Sapphire `./deploy.sh`, CamDisplay `./deploy.sh ML`.
  - Expected: each deploys.
  - `$G/UMMMods/Topaz`, `$G/UMMMods/Sapphire` and `$G/UserLibs` hold `Facet.0.2.dll` beside `PrismLib.UI.dll`.
  - `md5` of every copy equals `PrismLib-suite`'s build.
- [ ] **Step 5: check-bridge**, from `PrismLib-suite`.
  - Topaz and Sapphire: as in Task 1, on the deployed DLLs.
  - CamDisplay: first run `xbuild CamDisplay/CamDisplay.csproj /p:Configuration=Release /p:LoaderTarget=UMM /p:GameRoot="$G" /verbosity:quiet` in `CamDisplay-suite`. Then run `tools/check-bridge.sh <CamDisplay-suite>/CamDisplay/bin/umm/Release/CamDisplayUmm.dll CamDisplay.PrismBridge Init Tick OpenLayoutEditor Shutdown CamTarget ToggleSettings RefreshSettings`.
  - Run CamDisplay's by hand: its `release.sh` calls `../PrismLib/tools/check-bridge.sh`, which from a worktree is the main checkout's.
  - Expected for all three: `ok   Facet.0.2 loads` and `OK: … loads without PrismLib`.
- [ ] **Step 6: Commit**, one per repo: `build: ship Facet 0.2 with the mod`. The body says PrismLib.UI now stands on Facet 0.2, and `lib/update-prismlib.sh` fetches it once a PrismLib release lists it.

### Task 4: The suite skin on every Prism window

**Files:**
- Create: `PrismLib.UI/Toolkit/Suite.cs`, with its `<Compile Include="Toolkit\Suite.cs" />` line in the csproj.
- Modify: `Theme.cs`, `Tokens.cs`, `SettingsPanel.cs` (`Paint`, `Field`), and `CLAUDE.md` (the paragraph "Colours are per window (Theme); sizes stay global (Tokens)").
- Test:
  - create `tools/LayoutProbe/AutoTest.Suite.cs`: a partial `AutoTest` with `IEnumerator SuiteSkin()`;
  - in `AutoTest.cs`, the class becomes `sealed partial class`, `Start` yields `SuiteSkin()` after `SchemaSettings()`, and the check at line 485 looks for `"PRISMLIB · "`;
  - `build.sh` compiles `AutoTest.Suite.cs`.

**Interfaces:**
- **`public static class Suite`:**
  - `public static Facet.Theme Wear(Facet.Theme t)` applies the neutral roles below, then `Shape(t)`, and returns `t`. It returns null for null.
  - `internal static void Shape(Facet.Theme t)` sets the Sizes and Looks below. Each `Look` is a fresh `Facet.Look` per call.
  - `internal static readonly Color Window, TitleBar, Sidebar, RowFill, RowHover, ButtonFill, Line, Border, Text, Muted, SwitchOff, Danger` are the palette, which Toast reads in Task 5.
- **Neutral roles** (spec table; "white a" means `(1,1,1,a)`):

  | Role | Value | Spec token |
  |---|---|---|
  | Panel | `#1A1A1F` | Window |
  | TitleBar | `#141418` | Title bar |
  | Rail | `#111115` | Sidebar |
  | Row | white .025 | Row fill |
  | RowHover | white .05 | Row hover |
  | RowAlt | white .08 | button fill (Topaz's ButtonBg) |
  | Line | white .07 | Line (inside) |
  | PanelBorder | white .10 | Line (window edge) |
  | Text | `#EBEBF0` | Text |
  | TextMuted | `#94949E` | Muted text |
  | Off | white .18 | Switch off |
  | Danger | `#E2676D` | Danger |

- **Sizes:**

  | Field | Suite value | Read from |
  |---|---|---|
  | `Radius`, `SwitchRadius`, `KnobRadius` | 2 | `Tokens.Radius` |
  | `Line` | 1 | |
  | `Pad`, `Gap` | 12, 8 | `Tokens.Pad`, `Tokens.Gap` |
  | `RowHeight` | 32 | |
  | `ListRowHeight` | 24 | `Tokens.RowHeight` |
  | `NavRowHeight` | 28 | |
  | `TitleBarHeight` | 36 | |
  | `Title`, `Body`, `Small` | 17, 13, 12 | `Tokens.FontSizeTitle`, `FontSize`, `FontSizeSmall` |
  | `Caps`, `CapsSpacing` | 11, 1 | |
  | `SwitchWidth` × `SwitchHeight` | 30 × 16 | |
  | `Track` | 3 | |
  | `ThumbWidth` × `ThumbHeight` | 4 × 14 | |
  | `SliderWidth`, `NumberWidth`, `DropdownWidth` | 160, 52, 140 | |
  | `Indicator` | 2 | |

  `Tokens` defaults become `FontSizeTitle 17`, `FontSize 13`, `FontSizeSmall 12` and `Radius 2`; `Pad`, `Gap` and `RowHeight` stay.
- **Looks** that differ from Facet's defaults (every other Look keeps Facet's):

  | Field | Suite |
  |---|---|
  | `Button` | Bg RowHover, Text Text, Border Line, HoverBg RowAlt, PressedBg Row, SelectedBg Accent, SelectedText AccentText, FocusBorder Focus |
  | `DangerButton` | as Button, with Text Danger and SelectedBg Danger |
  | `Row` | Bg Row, HoverBg RowHover |
  | `RailItem` | Text TextMuted, HoverBg Row, HoverText Text, SelectedBg RowHover, SelectedText Text |
  | `Field` | Bg RowHover, Text Text, Border Line, FocusBorder Focus |
  | `Track` / `Fill` / `Thumb` | Bg Off / Bg Accent / Bg Text |
  | `Tab` | Text TextMuted, HoverText Text, SelectedText Text, FocusBorder Focus |
  | `Indicator` | SelectedBg Accent |

  `Card` keeps Facet's `{ Bg = Panel, Border = PanelBorder }`. Facet paints the window shell and popups with that same Look, so the rows carry the fill instead.
- **`Theme.cs`:**
  - `Default = new Theme(true, Suite.Wear(new Facet.Theme()))`.
  - `Refresh()` copies only `Tokens.Accent`, into Accent and Focus.
  - Default's colour getters read its twin, except `Accent`, which reads `Tokens.Accent`. Setters are unchanged.
  - `new Theme()` is `Suite.Wear` plus `CopyFrom(Default)`.
  - `FromAccent` is Quartz's derivation plus `Suite.Shape`: it gets the suite's sizes and looks but keeps its own colours.
  - `Of(t)` never dresses a scheme another host made.
  - `Recolour` is unchanged.
- **`SettingsPanel`:**
  - It builds with `Facet.Form.Build(_body, run, o => o.Cards = true)`.
  - `Field(Entry e, string group)` replaces the `bool grouped` overload. In a search, each `owner · page · group` head is the card title (`Field.Group = head`), in place of `Widgets.Header`.
  - Nav mode and `DebugPanel` need no code change; they wear the skin through their theme.

- [ ] **Step 1: Write `SuiteSkin()`** (Review Focus 1 and 4).
  - **Setup:**
    - Set `Words.Language` to `"en"`.
    - Save, then push, `Tokens.Panel = Tokens.RowHover = (1,0,1,1)`, `Tokens.Danger = (1,.48,.48,1)` and `Tokens.Accent = red`.
    - Rows: Owner `LayoutProbe`, Page `Suite`.
      - Group `Look`: `Suite switch` (Bool, false), `Suite size` (Float 0–2, 0.5f) and `Suite pick` (Dropdown, One/Two/Three, 1).
      - Group `Other`: `Suite other` (Bool, true).
    - Build `new SettingsPanel("Probe Suite", () => rows)`, make it visible, and wait 3 frames.
    - Take `win = PanelRoot("ProbeSuitePanel").Children().First()` and `th = Facet.Theme.For(win)`.
  - **Assertions:**
    - `th.Sizes`: Radius, SwitchRadius and KnobRadius are 2; Body 13, Title 17, Small 12, Caps 11, RowHeight 32, NavRowHeight 28, TitleBarHeight 36, SwitchWidth 30, SwitchHeight 16, Track 3 and DropdownWidth 140.
    - `th[Panel]` ≈ `#1A1A1F`, `th[RowHover]` is white .05, `th[Danger]` ≈ `#E2676D`, `th[Rail]` ≈ `#111115`, `th[Line].a` = .07, `th[Off].a` = .18, and `th[Accent]` is red. The pushed neutrals are ignored and the pushed accent is taken.
    - `win` has radius 2 and a background ≈ `#1A1A1F`.
    - Labels `LOOK` and `OTHER` exist. The `Suite size` row's top border is 1 wide; `Suite switch`'s is 0.
    - The `Suite switch` label's font size is 13. Its track (`Q("control").ElementAt(0)`) is 30 × 16 with radius 2, painted `th[Off]`.
    - `Q("Suite size-fill")` is red, and `Q("Suite pick-input")` has a min width ≥ 140.
    - The tab row holding the `Suite` tab has exactly one element 2 tall painted red.
    - A search for `suite size` shows the hit in a card headed `LAYOUTPROBE · SUITE · LOOK`.
    - **A mod's own scheme:** build `new SettingsPanel("Probe Mine", …)` inside `Theme.Use(new Theme { Accent = green })`. Its window has radius 2, Panel ≈ `#1A1A1F` and Accent green.
    - **Another host:** `Facet.Host.Create("LayoutProbe/plain", o => { })`, a `Facet.Surface`, then a `Facet.Window`. That window keeps radius 3, and `Facet.Theme.For(w.Root).Sizes.Body` is 15.
    - **The debug window:** build `new DebugPanel("Probe Debug", …)` with tabs `Probe A` and `Probe B`. Find the `PrismDebugPanel` document whose window holds the label `Probe Debug`.
      - Its window has radius 2.
      - Its tab row has exactly one red element 2 tall.
      - The filter field's input border is `th[Line]`.
    - **The rail:** build `new SettingsPanel("Probe Rail", () => PrismMenuShape(() => { }))`.
      - The rail column is painted `#111115`.
      - Exactly one entry carries a red indicator 2 wide.
      - Entries are ≥ 28 tall.
  - **Teardown:** restore `Tokens` and `Words`, and dispose every panel and surface.
- [ ] **Step 2: Run** `REPOS=… ./dev.sh`, then the LayoutProbe run. Expected: FAIL on the size, neutral and card checks (radius 3, Body 15, a magenta Panel).
- [ ] **Step 3: Implement** `Suite`, `Theme`, `Tokens` and `SettingsPanel`.
- [ ] **Step 4: Run** `./release.sh`, `REPOS=… ./dev.sh`, then the LayoutProbe run. Expected:
  - `ALL PASS`;
  - `fail=0`;
  - `[perf] settings A first open` under 50 ms, with the cards built.
- [ ] **Step 5: Docs.** Replace the `CLAUDE.md` paragraph:
  - every theme PrismLib.UI makes wears `Suite`;
  - the neutrals are the suite's, and only `Tokens.Accent` reaches windows;
  - sizes come from `Tokens` when a theme is made;
  - `Suite.Wear` dresses another host's theme.
- [ ] **Step 6: Commit:**
  - `feat: dress every Prism window in the suite skin`: Suite, Theme and Tokens. The body says a mod's `PushTokens` now gives Prism windows only its accent (Topaz and Sapphire `UI.Theme.PushTokens`; no change is needed there).
  - `feat: group Prism · Settings rows into cards`.
  - `test: check the suite skin on Prism windows`.
  - `docs: describe the suite skin`.

### Task 5: The layout editor's chrome and the toast

**Files:**
- Modify: `LayoutEditor.cs`, `LayoutEditor.Grips.cs`, `LayoutEditor.Inspector.cs` and `PrismLib.UI/Toast.cs`.
- Test: `AutoTest.Suite.cs` gets `IEnumerator SuiteChrome()`. `Start` yields it right after the check "first click selects A" (A selected; B and C not).

**Interfaces:**
- **The layout editor:**
  - `LayoutEditor` keeps `private Theme _th`, its surface's `Theme`, taken in the constructor. Every `Tokens.<colour>` read in `LayoutEditor*.cs` becomes `_th.<colour>`; the accent stays live through `Tokens.Accent`.
  - Frame boxes are named `layout-frame`.
    - Selected: a 2px `_th.Accent` border and an accent fill at .08.
    - Unselected: a 1px `_th.TextMuted` border and no fill. The accent marks state only.
  - The Done button loses its radius-16 override. Grips, the knob and the anchor dot take radius `Tokens.Radius`.
  - Tags take radius `Tokens.Radius`, background `_th.Panel` and text `_th.Text`.
- **The toast.** It is uGUI, so it gets the look only; its motion is unchanged (decision 8).
  - `ToastTheme` defaults:
    - `Background` = `Suite.Window` at alpha .96;
    - `BackgroundHover` = `Ui.Lighten(Suite.Window, .045f)` at .98;
    - `Border` = `Suite.Border`;
    - `Text` = `Suite.Text`;
    - `TextMuted` = `Suite.Muted`.

    `Accent` and `CloseHover` are unchanged.
  - The card and close button have radius 2. The pip is 2 wide (`offsetMax.x` 12) with radius 0, and the progress track and fill have radius 0.
  - Type sizes: the title 13, the hint 12, and × 14.
  - A mod that sets its own colours (Topaz's `UpdateToast`) keeps them.

- [ ] **Step 1: Write `SuiteChrome()`.**
  - `layout-done`, `layout-grip-0`, `layout-knob` and `layout-anchor-dot` each have `borderTopLeftRadius` 2.
  - Among `Root.Query("layout-frame")`, exactly one has a border 2 wide in the editor theme's Accent. The rest are 1 wide in its TextMuted.
  - Every frame tag has radius 2 and is painted the theme's Panel.
  - **The toast:**
    - Build `new Toast("Probe")`. Call `Set(new ToastContent { Key = "probe", Title = "Probe toast", Hint = "hint" })`, then `Tick()`.
    - The `RoundedRectGraphic` on `ProbeUpdateToastCanvas/UpdateToast` has `Radius` 2 and a colour ≈ (0.102, 0.102, 0.122, 0.96).
    - Its `Pip` rect is 2 wide.
    - `Dispose()`.
- [ ] **Step 2: Run** (after `REPOS=… ./dev.sh`). Expected: FAIL: Done radius 16, the knob 6, unselected frames in accent, and toast radius 10.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run.** Expected: `fail=0`.
- [ ] **Step 5: Commit:**
  - `feat: draw the layout editor's chrome in the suite look`;
  - `feat: draw the update toast in the suite look`;
  - `test: check the editor chrome and the toast`.

### Task 6: The animation duration setting

**Files:**
- PrismLib:
  - modify `PrismLib/Prism.cs`, `PrismLib.UI/Toolkit/Words.cs`, `PrismLib.UI/Toolkit/Anim.cs` (and its header comment);
  - modify `tools/PrismLibTest.cs`, `CLAUDE.md` ("PrismLib has settings of its own") and `README.md:119-121`;
  - test: `AutoTest.Suite.cs` gets `SuiteMotion()`, which `Start` yields after `SuiteSkin()`.
- Topaz: `Topaz/Util/PrismBridge.cs` (`TickDebug`).
- Sapphire: `Sapphire/Util/PrismBridge.cs` (`TickDebug`).
- CamDisplay: `CamDisplay/Util/PrismBridge.cs` (`Tick`), `CamDisplay/Dev/SelfTest.cs`, and `release.sh:36` (add `TickAnim` to the ungated list).

**Interfaces:**
- **`public const string Prism.AnimKey = "anim.duration";`**
- **`public static int Prism.AnimDuration { get; }`**:
  - `Config.Get(AnimKey)` through `int.TryParse(…, NumberStyles.Integer, CultureInfo.InvariantCulture)`;
  - unparsable or missing reads as 120;
  - the result is clamped to 0–400.
- **The `SettingEntry`:**
  - Id `AnimKey`, Kind `Int`, Label `"Animation duration (ms)"`, Page `"PrismLib"`, Group `"Display"`;
  - Tooltip `"Length of a standard transition in every Prism window. 0 turns animation off."`;
  - Keywords `animation, duration, speed, motion, transition, ms`;
  - Order `int.MinValue + 1`, Min 0, Max 400;
  - `Get = () => AnimDuration`;
  - `Set` = `Config.Set(AnimKey, <Convert.ToInt32(v) clamped>.ToString(CultureInfo.InvariantCulture))`.

  `RestartKey`'s Order becomes `int.MinValue + 2`, so both Display rows share one card.
- **Korean:**
  - `"Animation duration (ms)"` → `"애니메이션 길이 (ms)"`;
  - the tooltip → `"모든 Prism 창의 기본 전환 길이입니다. 0이면 애니메이션이 꺼집니다."`.
- **`public static int Anim.Duration { get; set; }`** in `PrismLib.UI.Toolkit.Anim`:
  - set clamps to 0–400 and writes `Facet.Anim.Speed = ms / 120f`, only when that differs;
  - get is `(int)Math.Round(Facet.Anim.Speed * 120)`.
- **The bridge tick.** This is Topaz's; Sapphire and CamDisplay use the same code. Topaz and Sapphire call it at the top of `TickDebug` beside `FpsCounter.Tick`; CamDisplay calls it on the first line of `Tick`.

  ```csharp
  // PrismLib's animation duration -> every Facet window's speed. Both calls are newer than a PrismLib
  // or PrismLib.UI another mod may have loaded: one miss stops it for the session.
  private static bool _animGone;
  internal static void TickAnim()
  {
      if (_animGone || !Available) return;
      try { SetAnim(AnimMs()); } catch (Exception) { _animGone = true; }
  }
  [MethodImpl(MethodImplOptions.NoInlining)] private static int AnimMs() => PrismLib.Prism.AnimDuration;
  [MethodImpl(MethodImplOptions.NoInlining)] private static void SetAnim(int ms) => PrismLib.UI.Toolkit.Anim.Duration = ms;
  ```

- [ ] **Step 1: Write the offline test** (Review Focus 2), section `animation duration` in `PrismLibTest.cs`.

  ```csharp
  string wasAnim = Config.Get(Prism.AnimKey);
  string[] input = { "", "200", "-50", "100000", "fast", "120.5" };
  int[] want =     { 120, 200,   0,     400,     120,    120 };
  for (int i = 0; i < input.Length; i++) { Config.Set(Prism.AnimKey, input[i]); Check(Prism.AnimDuration == want[i], "anim.duration '" + input[i] + "' reads " + want[i]); }
  // the entry: Settings.Search("animation") holds Id "anim.duration", owner "PrismLib", Kind Int, Min 0, Max 400, after fps.show, before update.restart
  // entry.Set(9999) -> Config.Get(AnimKey) == "400"; entry.Set(250) -> Prism.AnimDuration == 250
  Config.Set(Prism.AnimKey, wasAnim ?? "120");
  ```

  Where the test sets `Words.Language = () => "ko"`, add `Check(Words.Own("Animation duration (ms)") == "애니메이션 길이 (ms)", …)`.
- [ ] **Step 2: Run `./release.sh`.** Expected: a build failure on `Prism.AnimKey`.
- [ ] **Step 3: Implement** `Prism.cs` and `Words.cs`. Run again. Expected: `ALL PASS`, and the duplicate-key grep prints nothing.
- [ ] **Step 4: Write `SuiteMotion()`.** It is synchronous: the bridges re-apply the config every frame.
  - `Anim.Duration = 240`. Then `Anim.Duration` is 240, and `((float[])((Dictionary<string, object>)AppDomain.CurrentDomain.GetData("Facet.Shared"))["Facet.AnimSpeed"])[0]` is 2.
  - Setting 9999 reads back 400; setting -5 reads back 0.
  - At 0, `new SettingsPanel("Probe Motion", rows).Visible = true` leaves the window's `style.opacity` at 1 and its `style.translate` y at 0, in that same call.
  - Finally `Anim.Duration = 120`.
- [ ] **Step 5: Run.** Expected: a LayoutProbe build failure on `Anim.Duration`. Implement it, then run `./release.sh`, `REPOS=… ./dev.sh` and the LayoutProbe run. Expected: `fail=0`.
- [ ] **Step 6: Write CamDisplay's self-test checks.** Put them in a helper behind `PrismBridge.Available`, run after "PrismLib is available and wired".
  - `PrismLib.Settings.Search("animation")` lists `anim.duration`, owned by `PrismLib`.
  - After `Config.Set(AnimKey, "240")`, `WaitFor(() => PrismLib.UI.Toolkit.Anim.Duration == 240, 1f)` passes: "the animation setting reaches Facet within a second".
  - `"fast"` reads 120.
  - Restore the old value, or `"120"`.

  Do the CamDisplay run. Expected: FAIL on "reaches Facet". No bridge ticks the setting yet.
- [ ] **Step 7: Add `TickAnim` to the three bridges.**
  - Redeploy all three: Topaz and Sapphire with `./deploy.sh`, CamDisplay with `./deploy.sh ML`.
  - Run check-bridge as in Task 3, with `TickAnim` added to each method list. Expected: `TickAnim JITs without PrismLib`.
  - Do the CamDisplay run. Expected: `fail=0`.
- [ ] **Step 8: Docs.** In `CLAUDE.md`, the setting goes beside `fps.show`: bridges copy `Prism.AnimDuration` into `Anim.Duration` every frame, and Topaz's `TickAnim` is the model. In `README.md`, the settings sentence lists the setting.
- [ ] **Step 9: Commit:**
  - PrismLib:
    - `feat: add the animation duration setting`. The body says the bridges must copy `Prism.AnimDuration` into `Anim.Duration` every frame: Topaz and Sapphire in `PrismBridge.TickDebug`, CamDisplay in `PrismBridge.Tick`.
    - `feat: set Facet's shared animation speed from a duration in ms`.
    - `docs: describe the animation duration setting`.
  - Topaz and Sapphire: `feat: follow PrismLib's animation duration`.
  - CamDisplay:
    - `feat: follow PrismLib's animation duration`;
    - `test: check the animation setting reaches Facet`;
    - `build: check TickAnim loads without PrismLib`.

### Task 7: The whole suite, beside Topaz and Sapphire

**Files:**
- Modify: `docs/superpowers/plans/roadmap.md`. Plan 8's line says it is done on branch `suite-ui` in four repos, unmerged.
- Touch nothing else unless a check fails.

- [ ] **Step 1: Offline.**
  - `./release.sh` with no version. Expected: `ALL PASS`, `bootstrap compiles standalone` and `bootstrap: ALL PASS`.
  - The Words duplicate grep prints nothing.
  - `git grep -n 'Facet\.0\.1'` in all four worktrees finds only check-bootstrap's fixtures.
  - `diff -q` reports no difference between the four `PrismBootstrap.cs` copies.
- [ ] **Step 2: Install** (your slot).
  - Run `REPOS=… ./dev.sh`, Topaz and Sapphire `./deploy.sh`, CamDisplay `./deploy.sh ML`, then `tools/LayoutProbe/build.sh`.
  - Every `PrismLib.UI.dll` and `Facet.0.2.dll` copy (`$G/UMMMods/*/`, `$G/UserLibs/`) has the md5 of the build.
  - The two shared copies, `$G/UMMMods/PrismLib/PrismLib.dll` and `$G/Mods/PrismLib/PrismLib.dll`, have the same md5.
- [ ] **Step 3: check-bridge** for all three mods, with `TickAnim` listed. Expected: `OK` for each.
- [ ] **Step 4: The run beside Topaz and Sapphire.** Do the LayoutProbe run with Topaz, Sapphire, CamDisplay and Quartz installed as the rig has them. Expected: `fail=0`. Keep the `[perf]` lines for the report.
- [ ] **Step 5: Do the CamDisplay run.** Expected: `fail=0`, "no text in the settings window is cut off" included.
- [ ] **Step 6: Check the logs from both runs.**
  - Grep for `MissingMethod|MissingField|TypeLoad|FileNotFound` in:
    - `$G/UMMMods/Topaz/TopazLog.txt`;
    - `$G/UMMMods/Sapphire/SapphireLog.txt`;
    - `$G/MelonLoader/Latest.log`.

    Expected: none from this session.
  - Topaz and Sapphire each log their PrismLib registration.
- [ ] **Step 7: Commit** `docs: mark plan 8 done on its branch`.
- [ ] **Step 8: Hand back.**
  - Leave all four branches unmerged and unpushed.
  - Report the `[perf]` lines.
  - Report that the game still runs the `suite-ui` builds; `./dev.sh` and each mod's `deploy.sh` from the main checkouts put main's back.
