# The new Quartz menu shell (plan 10) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Behind "Experimental: new menu", Quartz's menu hotkey opens one Facet 0.2 window in the Prism suite look: a labelled sidebar, tab rows, search, Home, the Features grid and the regrouped Settings, with Effect Remover as the first real page and every other page handed to the classic menu and back.

**Architecture:**
- **One window, built from Quartz's own registries.** `QuartzShell` turns `NavRegistry` categories into a Facet `Nav` rail (three ruled groups) and each category's `NavPage`s into a `Tabs.Pages` row. A page is drawn by a builder in `QuartzShell.Moved` (Home, Features, Settings, Effect Remover in this plan), as a "feature is off" page for a stub, or as a classic card whose "Open it" hands off to the uGUI menu. Plan 11 moves a page by adding one `Moved` entry.
- **Hand-off is one field.** `MenuHandOff.ReturnTo` says which menu a user's close returns to. Opening one menu from the other sets it, so at most one hand-back is ever pending.
- **The skin comes through the bridges.** `QuartzHost` dresses its theme with `Suite.Wear` through `PrismBridge`, and falls back to Facet's own look when PrismLib.UI is unusable. `PrismBridge` also ticks `anim.duration` into Facet.

**Tech Stack:** C# `netstandard2.1` (Quartz, `dotnet build`, `TreatWarningsAsErrors`), Facet 0.2 (plan 7), PrismLib.UI with the suite skin (plan 8), Quartz's PrismLib bridge (plan 9), Unity 6 runtime UI Toolkit, the DEBUG in-game self-test (macOS rig: MelonLoader + UMMBridge).

**Spec:** `/Users/preluminance/Coding/PrismMods/PrismLib/docs/superpowers/specs/2026-11-07-prism-suite-ui-design.md`: "The new Quartz menu" (all of it), decisions 3–8 and 11–13, "Constraints", "Testing", "Calls the writer made". Mockups `2026-11-07-prism-suite-ui/{home,features-grid-a,look-a}.html`, option A; Home without "Your screen". Facet API: plan 7's Interfaces (`2026-11-07-7-facet-0.2.md`) and ruling X5 (`Looks.Group`, `Looks.Stage`). Suite API: plan 8's "What plan 10 consumes". Quartz bridge: plan 9's "What plan 10 consumes". Cross-plan rulings X1–X8 (`…/side-session-notes/local_6fbd84b6-…/suite-ui-program.md`) and plan 6 rulings P0–P27 (`…/quartz-facet-page-ledger.md`). Read Quartz's `AGENTS.md`, `agents/project.md`, `agents/commits.md` and `agents/i18n.md` before the first change.

Paths: **Q** = `/Users/preluminance/Coding/PrismMods/Quartz/Quartz-facet` (all paths below are relative to it), **G** = `$HOME/Library/Application Support/Steam/steamapps/common/A Dance of Fire and Ice`, **D** = `$G/UserData/Quartz`.

## Global Constraints

- **Prerequisites.** Plans 7, 8 and 9 are complete (X1). Facet 0.2 is the tree at `/Users/preluminance/Coding/PrismMods/Facet-0.2` (branch `facet-0.2`), or `../../Facet` once the user has merged it. PrismLib with plans 8–9 is `/Users/preluminance/Coding/PrismMods/PrismLib-suite` (branch `suite-ui`), or the main checkout once merged. Refresh `lib/` before any build: run `./release.sh` (no version) in that PrismLib tree, then `FACET_ROOT=<Facet tree> tools/update-facet.sh && PRISMLIB_ROOT=<PrismLib tree> DEV=1 tools/update-prismlib.sh`.
- **Repository.** Quartz only, on branch `facet-preview` in Q. Never touch the main checkout `/Users/preluminance/Coding/PrismMods/Quartz/Quartz`, PrismLib, Facet, Topaz, Sapphire or CamDisplay. **Push nothing.**
- **Commits** follow Q's `agents/commits.md`: Conventional subjects, split finely, every commit builds. **No `Co-Authored-By` trailer and no AI attribution anywhere.**
- **Versions and releases are the user's call.** Do not touch `Quartz/Core/Info.cs` or `build.json`, and do not run `tools/release.sh`.
- **Lang keys.** Every new user-facing string is a Lang key with en-US and ko-KR authored together. Core keys go in `Quartz/Resource/Export/Lang/`, Effect Remover's in `Quartz/Features/EffectRemover/Lang/`. `zh-CN.json` is untouched. Each key is hunk-split into its feature's commit. The new prefix `MENU_` goes in `KEY_PREFIXES.md`. Labels are keys translated live by `QuartzHost` (P16). Tell the user the Lang commits are pending i18n sync.
- **Back up the runtime store before every AutoInstall build:** `cp -R "$D/Runtime" "$HOME/QuartzRuntime-backup-$(date +%Y%m%d-%H%M%S)"`.
- **One in-game test at a time.** Never quit a game you did not launch. Follow the rig memory `mac-ingame-test-rig` for Steam's cloud-sync dialog. Report a launch that times out with no log rather than looping on it. The only screenshots are the tour's in-game `ScreenCapture` (Task 11).
- **The old menu keeps working unchanged when the switch is off.** The one exception, required by the spec, is that Effect Remover's "Open in the new menu (preview)" button appears only with the switch on. Both menus edit the same settings objects, so switching menus never loses a setting.
- **Doors.**
  - Outside `Quartz/UI/FacetUi/`, Facet is reached only through `FacetBridge` (P6).
  - Outside `Quartz/Prism/`, PrismLib and PrismLib.UI are reached only through `PrismBridge`, and no PrismLib or PrismLib.UI type appears in a field of either bridge (plan 9).
  - `FacetUi` code calls `PrismBridge` wrappers and never names a PrismLib type.
- **Quartz conventions:** files ≤ 500 lines, no silent catch (log it or `Diag.Ignore(e)`), namespace = folder, zero build warnings, and `python3 scripts/check_conventions.py` passes.
- **CI stubs.** Release code avoids `RegisterCallback<GeometryChangedEvent>` and `schedule.Execute` (`stubs/README.md` "Known gaps"). Stubs are regenerated in Tasks 1 and 11 only.
- **Builds that ship no Facet.** IL2CPP configs and the KeyViewer flavour still build and ship no Facet or PrismLib (P14). Everything in `FacetUi/` stays `#if !IL2CPP`, and the new menu exists only where `FacetBridge.Available()`.
- **Performance.**
  - The first open of the new menu after Facet's warm-up is ≤ **50 ms** on a Quartz-only install.
  - Nothing runs per frame while it is closed: `FacetBridge.TickNewMenu` averages ≤ **2 µs** with the window hidden.
  - A page change in the window is a Facet page build, with no uGUI work.
- **Self-test rule.** If a plan 6 or plan 9 check fails only because the page now lives in the shell, fix its selector and never its assertion.

## Review Focus

1. **The switch is on but the new menu cannot run**: Unity 2022, the KeyViewer flavour, a missing `Facet.0.2.dll`, or an older PrismLib.UI loaded first. The hotkey opens the classic menu, the switch row and the preview button are absent, and nothing throws per frame. Without PrismLib.UI, the new menu opens in Facet's own look with Edit layout and the Prism link hidden. Pinned in Task 6 (run D in Task 11) and Task 2 (check-bridge).
2. **Menus opened from each other over and over** (Effect Remover's button, then "Open it", then the opener over the new menu), or rebuilt behind. Exactly one menu shows after every step, a close never reopens the menu just closed, and the chain ends with none. Pinned in Tasks 4 and 6.
3. **A feature switched off or on while its page is open**, from the page header, a Features tile or the classic menu. Within 3 frames the same tab shows the stub page or the real one, with no failed-to-build card, and search finds the new state. Pinned in Tasks 4 and 7.
4. **The language changes while the new menu is open** (en → ko → zh). The rail, tabs and cards relabel in the same frame, Chinese draws, and Korean initials find rows. Pinned in Tasks 1, 4 and 7.
5. **Stale or junk saved state.** `menu.page` names a page that is gone. `home.switches` holds unknown ids, duplicates, the empty string, or nothing at all. In order: the window opens on Home, unknown ids drop, empty means "none chosen", and a missing key means the defaults. Pinned in Tasks 3 and 4.

---

## File structure

```
Quartz/UI/MenuHandOff.cs                         new   ReturnTo: which menu a user's close goes back to
Quartz/UI/FacetUi/ShellModel.cs                  new   BCL-only rules: rail groups, switches, last page, tab index, initial rect
Quartz/UI/FacetUi/QuartzShell.cs                 new   the window: surface, title bar, Nav, show/hide, hand-off, tick, Edit layout
Quartz/UI/FacetUi/QuartzShell.Pages.cs           new   rail items, tab rows, Moved map, classic card, stub page, labels
Quartz/UI/FacetUi/ShellHome.cs ShellFeatures.cs ShellSettings.cs     new   the three moved core pages
Quartz/UI/FacetUi/FacetSelfTestChecks.{Suite,Shell,HandOff,Search,Pages}.cs   new   partial self-test sections
Quartz/UI/FacetUi/FacetBridge.cs QuartzHost.cs FacetEffectRemover.cs FacetSelfTest.cs FacetSelfTestChecks*.cs   modified
Quartz/UI/UICore.cs UICore.Visibility.cs UICore.Shell.cs                                      modified
Quartz/UI/Factory/Page/PageSettings.Sections.cs PageFeatures.cs PageHome.cs                    modified
Quartz/UI/Home/HomeCard.cs HomeRegistry.cs                                                      modified
Quartz/Features/{KeyViewer,KeyLimiter,Tuf,Optimizer}/*Module.cs, Features/FeaturePages.cs     modified
Quartz/Features/EffectRemover/PageEffectRemover.cs, Features/EffectRemover/Lang/{en-US,ko-KR}.json   modified
Quartz/Prism/PrismBridge.cs PrismWindows.cs PrismSelfTestChecks.cs                             modified
Quartz/Quartz.csproj tools/update-facet.sh tools/release.sh stubs/ THIRD-PARTY-NOTICES.md agents/project.md   modified
Quartz/Resource/Export/Lang/{en-US,ko-KR}.json KEY_PREFIXES.md                                 modified
Quartz.Tests/UI/ShellModelTests.cs                new; Quartz.Tests/Program.cs, Quartz.Tests.csproj modified
```

## In-game self-test run (Tasks 1–11)

`M` is `facet-selftest` (plan 6's runner, extended here) unless a step says `prism-selftest` (plan 9's). Only one marker per launch, because each runner quits the game.

```bash
pgrep -f ADanceOfFireAndIce >/dev/null && { echo "game running: wait, do not quit it"; exit 1; }
cp -R "$D/Runtime" "$HOME/QuartzRuntime-backup-$(date +%Y%m%d-%H%M%S)"
scripts/build.sh Debug ML
rm -f "$D/$M.log"; touch "$D/$M"
open steam://rungameid/977950
# poll every 10 s, up to 5 min, until the log holds DONE; the game quits itself
grep -E '^(FAIL|INFO|DONE)' "$D/$M.log"; rm -f "$D/$M"
```

Expected: no `FAIL` line, and a last line of `DONE pass=<n> fail=0`. Keep the game window focused.

---

### Task 1: Quartz builds and ships on Facet 0.2

**Files:**
- Modify: `Quartz/Quartz.csproj` (Facet reference and `FacetDll`), `tools/update-facet.sh`, `tools/release.sh` (comment at line 193), `THIRD-PARTY-NOTICES.md`, `agents/project.md` (the `lib/Facet.0.1.dll` line).
- Modify: `Quartz/UI/FacetUi/FacetBridge.cs`, `QuartzHost.cs`, `FacetEffectRemover.cs` (`FilterLabel`), `FacetSelfTest.cs`, `FacetSelfTestChecks.cs`.
- Regenerate: `stubs/` (`Facet.0.2` in, `Facet.0.1` out).

**Interfaces:**
- **The csproj names the minor once:** `<FacetName>Facet.0.2</FacetName>`, `<Reference Include="$(FacetName)">` and `FacetDll = $(LibDir)/$(FacetName).dll`.
- **`tools/update-facet.sh`** reads `FacetName` from `Quartz/Quartz.csproj` and copies `<Facet tree>/Facet/bin/Release/netstandard2.1/$FacetName.dll` into `lib/`. When that file is absent (a 0.1 tree), it exits 1 with `<tree> does not build Facet.0.2.dll`.
- **`internal static string FacetBridge.FacetName`** is the `Facet.*` name in `typeof(FacetBridge).Assembly.GetReferencedAssemblies()`. `Probe()` is available when the loaded Facet's name equals it, so a future minor needs no literal edit.
- **`internal static bool QuartzHost.Drawable(string code)`** is true for `en`, `ko`, `zh` and `ja`: Latin, plus the three scripts Facet 0.2 falls back for.
  - `Translate(s)` translates when `Drawable(LanguageCode(Tr.Language))`, and otherwise gives English, as before.
  - `internal static string QuartzHost.Text(string key, string english)` follows the same rule for text Quartz formats itself.
- **`FilterLabel(f)`** shows the game's own name (`RDString.GetEnumValue`) when the game's language is English, or when its script matches Quartz's language code. Korean is `ko`, ChineseSimplified and ChineseTraditional are `zh`, and Japanese is `ja`. Otherwise it shows the split enum name.
- **`FacetBridge.TickWheelImpl`** calls `Facet.Input.TickWheel(pointer, delta)` with no "own window" gate, because Facet 0.2 counts a notch once per frame across mods (plan 7 Task 3).

- [ ] **Step 1: Baseline.** Run `scripts/test.sh` and the self-test run. Expected: all tests pass, and `DONE … fail=0` on 0.1. Note the pass count.
- [ ] **Step 2: Write the failing checks.**
  - `FacetSelfTest.Run` (facet present):
    - `Check("quartz is built on Facet 0.2", FacetBridge.FacetName == "Facet.0.2")`;
    - `"exactly one " + FacetBridge.FacetName + " assembly loaded"`, replacing the `"Facet.0.1"` count.
  - Replace `UndrawableShowsEnglish` with `ChineseDraws`. Under `Tr.Language = "zh-CN"`, all of these hold, and the language is restored in a `finally`:
    - `QuartzHost.Host.Translate("FXRM_MODE_SIMPLE") == MainCore.Tr.Get("FXRM_MODE_SIMPLE", …)`;
    - that text differs from English;
    - `Fonts.Has(Fonts.Regular(QuartzHost.Host), <that text>)`;
    - `QuartzHost.Drawable("ja") && !QuartzHost.Drawable("pt")`;
    - with the game in English, `FilterLabel(Filter.NightVision) == RDString.GetEnumValue(Filter.NightVision)`.
- [ ] **Step 3: Run the self-test.** Expected: it builds on 0.1, with FAIL on `quartz is built on Facet 0.2` and `ChineseDraws`.
- [ ] **Step 4: Implement** the csproj, script, probe, `Drawable` and `Text`, `FilterLabel` and the wheel change.
  - Run `FACET_ROOT=<Facet tree> tools/update-facet.sh`. Expected: `lib/Facet.0.2.dll <- … md5 …`. Then `rm lib/Facet.0.1.dll`.
  - Update the notices, the release comment and `project.md`.
- [ ] **Step 5: Stubs.** Run `tools/gen-stubs.sh` and `git rm -r stubs/Facet.0.1` if it is left behind. Stage only the Facet entries in `MANIFEST.json`, `README.md` and `Stubs.proj`; the 0Harmony drift is pre-existing (P15).
- [ ] **Step 6: Verify.**
  - `scripts/test.sh` passes.
  - The self-test run ends `fail=0`, with the two checks PASS.
  - `git grep -n 'Facet\.0\.1'` prints nothing.
  - `unzip -l dist/Quartz.zip | grep -c 'Runtime/versions/[^/]*/Facet\.0\.2\.dll$'` prints `1`.
- [ ] **Step 7: Commit**, in this order:
  - `build: move Quartz to Facet 0.2` (csproj, script, probe, self-test names, notices, release comment, `project.md`);
  - `chore: regenerate stubs for Facet 0.2`;
  - `feat: show Chinese and Japanese in Quartz's Facet windows`;
  - `refactor: let Facet count the wheel once per frame`.

### Task 2: Quartz's Facet windows wear the suite, and Quartz follows `anim.duration`

**Files:**
- Modify: `Quartz/Prism/PrismBridge.cs`, `Quartz/Prism/PrismSelfTestChecks.cs`, `Quartz/UI/FacetUi/QuartzHost.cs`, `FacetSelfTestChecks.cs` (run order), `FacetSelfTestChecks.EffectRemover.cs` (delete `AccentFollows`).
- Create: `Quartz/UI/FacetUi/FacetSelfTestChecks.Suite.cs`.

**Interfaces:**
- **`internal static bool PrismBridge.WearSuite(object facetTheme)`** is `NoInlining`. Its `…Impl` calls `PrismLib.UI.Toolkit.Suite.Wear((Facet.Theme)facetTheme)`.
  - It returns false when `!UiAvailable`, when the argument is null, or after a miss.
  - A miss sets its own `suiteGone` flag and logs `PrismLib.UI suite skin unavailable: <e>` once. It never turns `UiAvailable` off.
- **`internal static void PrismBridge.TickAnim()`** is plan 8 Task 6's Topaz model, verbatim. `PrismBridge.Tick()` calls it first, outside any other guard.
- **The `QuartzHost` theme:**
  - `t = new Theme()`. If `PrismBridge.WearSuite(t)` succeeds, set `t[Role.Accent] = t[Role.Focus] = Conf.GetAccentColor()`. Otherwise `t = Theme.FromAccent(accent)`, plan 6's look.
  - `internal static bool QuartzHost.Suited` records which.
  - `Recolour(accent)`: when suited, set Accent and Focus, then `t.Notify()`. Otherwise `t.Recolour(accent)`.

- [ ] **Step 1: Write `FacetSelfTestChecks.Suite`**, run first in `FacetSelfTestChecks.Run`.
  - If `!PrismBridge.UiAvailable`: `info("suite: PrismLib.UI unavailable, Facet's own look")`, `check("suite: unsuited without PrismLib.UI", !QuartzHost.Suited)`, then stop.
  - Otherwise, with `th = QuartzHost.Host.Theme`:
    - `th.Sizes`: Radius 2, Body 13, Title 17, SwitchWidth 30;
    - `th[Panel]` ≈ `#1A1A1F`, `th[TitleBar]` ≈ `#141418`;
    - `th[Accent]` and `th[Focus]` ≈ `Conf.GetAccentColor()`.
  - A throwaway `Window` on the host (plan 6's `Window(name, out w)` helper) has root radius 2 and a background ≈ `#1A1A1F`.
  - `QuartzHost.Recolour(Color.red)` gives `th[Accent] == th[Focus] == red` in the same frame, and `th[TitleBar]` stays `#141418`. Restoring the accent puts Accent back.
- [ ] **Step 2: Write `PrismSelfTestChecks.Suite`**, yielded last in `PrismSelfTest.Run`.
  - `Config.Set(Prism.AnimKey, "240")` gives `PrismLib.UI.Toolkit.Anim.Duration == 240` within 1 s: "suite: the animation setting reaches Facet within a second".
  - `"fast"` reads back 120.
  - Restore the old value, or `"120"`.
  - `info` the MODE line, because beside Topaz the duration may arrive through Topaz's tick.
- [ ] **Step 3: Run** the self-test run, then the run with `M=prism-selftest`. Expected: FAIL on the suite sizes, and on "reaches Facet" in a Quartz-only session; beside Topaz it may already pass.
- [ ] **Step 4: Implement** `WearSuite`, `TickAnim` and `QuartzHost`.
- [ ] **Step 5: Verify.**
  - Both runs end `fail=0`.
  - `dotnet build Quartz/Quartz.csproj -c Release -p:AutoInstall=false`, then in the PrismLib tree run `./tools/check-bridge.sh <Q>/Quartz/bin/Release/netstandard2.1/Quartz.dll Quartz.Prism.PrismBridge Init Register Tick Shutdown Exempt TryPhysical ToggleDebug TogglePrismSettings OpenLayoutEditor TouchLayout get_UiAvailable TickAnim WearSuite`. Expected: `OK: … loads without PrismLib`.
- [ ] **Step 6: Commit:**
  - `feat: dress Quartz's Facet windows in the Prism suite look` (bridge `WearSuite`, `QuartzHost`, Suite checks, `AccentFollows` removed);
  - `feat: follow PrismLib's animation duration` (`TickAnim` and its check).

### Task 3: The shell's pure rules, offline

**Files:**
- Create: `Quartz/UI/FacetUi/ShellModel.cs` (BCL only, no `#if`), `Quartz.Tests/UI/ShellModelTests.cs`.
- Modify: `Quartz.Tests/Quartz.Tests.csproj` (link `../Quartz/UI/FacetUi/ShellModel.cs`), `Quartz.Tests/Program.cs`.

**Interfaces:** `internal static class ShellModel` (namespace `Quartz.UI.FacetUi`):
- **`int RailGroup(string categoryKey)`:**

  | Category key | Group |
  |---|---|
  | `home`, `features` | 0 |
  | `profiles`, `import`, `settings`, `developer` | 2 |
  | `credits`, null | −1 (not in the rail) |
  | anything else (feature, TUF and addon categories) | 1 |

- **`static readonly string[] DefaultSwitches`** = `keyviewer, combo, keylimiter, effectremover, autodeafen, progressbar` (the mockup's six).
- **`List<string> ParseSwitches(string stored, ICollection<string> known)`:**
  - null gives `DefaultSwitches` filtered by `known`, in that order;
  - `""` gives an empty list;
  - otherwise split on `,` and trim, then drop unknown ids and repeats, keeping first-seen order.
- **`string FormatSwitches(IEnumerable<string> ids)`** joins with `,`.
- **`string LastPage(string stored, Func<string, bool> exists, string home)`** returns `stored` when it is non-empty and `exists(stored)`, else `home`.
- **`int TabIndex(IReadOnlyList<KeyValuePair<string, string>> pages, string currentKey, string currentFeature)`.** `pages` holds (page key, owning feature id or null).
  - It returns the index whose key equals `currentKey`.
  - Failing that, the index whose feature equals a non-null `currentFeature`. This is how a feature's real page and its stub find the same tab.
  - Otherwise −1, which leaves Facet's remembered tab.
- **`(float X, float Y, float W, float H) InitialRect(float panelW, float panelH)`** = `(0.1w, 0.1h, 0.8w, 0.8h)`.
- **`bool HasLayout(string featureId)`** is true for `keyviewer, combo, judgement, songtitle, progressbar, practice, calibration, panels`: the features plan 9 tagged with `LayoutIds`.

- [ ] **Step 1: Write the failing tests.**

```csharp
public static void TestRailGroups() {
    Assert(ShellModel.RailGroup("home") == 0 && ShellModel.RailGroup("features") == 0, "home and features lead");
    Assert(ShellModel.RailGroup("overlay") == 1 && ShellModel.RailGroup("tuf") == 1 && ShellModel.RailGroup("myaddon") == 1, "feature and addon categories in the middle");
    Assert(ShellModel.RailGroup("settings") == 2 && ShellModel.RailGroup("developer") == 2, "app pages last");
    Assert(ShellModel.RailGroup("credits") == -1 && ShellModel.RailGroup(null) == -1, "credits leaves the rail");
}
public static void TestSwitches() {
    var known = new HashSet<string> { "keyviewer", "combo", "keylimiter", "effectremover", "progressbar", "ottoicon" };
    Assert(ShellModel.ParseSwitches(null, known).SequenceEqual(new[] { "keyviewer", "combo", "keylimiter", "effectremover", "progressbar" }), "missing: the defaults that exist");
    Assert(ShellModel.ParseSwitches("", known).Count == 0, "empty: none chosen");
    Assert(ShellModel.ParseSwitches(" ottoicon,nope,combo,ottoicon ", known).SequenceEqual(new[] { "ottoicon", "combo" }), "unknown and repeats drop, order kept");
    Assert(ShellModel.FormatSwitches(new[] { "a", "b" }) == "a,b", "comma-joined");
}
public static void TestLastPageAndTabs() {
    Func<string, bool> has = k => k == "visuals.effectremover";
    Assert(ShellModel.LastPage("visuals.effectremover", has, "home.overview") == "visuals.effectremover", "a known page reopens");
    Assert(ShellModel.LastPage("nope.page", has, "home.overview") == "home.overview" && ShellModel.LastPage(null, has, "home.overview") == "home.overview", "gone or unset: Home");
    var tabs = new[] { Kv("visuals.hidejudgements", "hidejudgements"), Kv("feature-off.effectremover", "effectremover") };
    Assert(ShellModel.TabIndex(tabs, "visuals.effectremover", "effectremover") == 1, "a stub takes its feature's tab");
    Assert(ShellModel.TabIndex(tabs, "visuals.hidejudgements", null) == 0 && ShellModel.TabIndex(tabs, "x", null) == -1, "by key, else Facet's memory");
}
public static void TestInitialRectAndLayout() {
    Assert(ShellModel.InitialRect(1920, 1080) == (192f, 108f, 1536f, 864f), "80% of the panel, centred");
    Assert(ShellModel.HasLayout("combo") && ShellModel.HasLayout("panels") && !ShellModel.HasLayout("effectremover"), "on-screen features only");
}
```

  `Kv` is a local `KeyValuePair` helper. Register all four in `Program.cs`.
- [ ] **Step 2: Run `scripts/test.sh`.** Expected: the build fails with `ShellModel` not found.
- [ ] **Step 3: Implement `ShellModel`.**
- [ ] **Step 4: Run `scripts/test.sh`.** Expected: every test passes, the four new ones included.
- [ ] **Step 5: Commit** `feat: add the new menu's layout and saved-state rules`.

### Task 4: The shell window, and the hand-off both ways

**Files:**
- Create: `Quartz/UI/MenuHandOff.cs`, `Quartz/UI/FacetUi/QuartzShell.cs`, `QuartzShell.Pages.cs`, `FacetSelfTestChecks.Shell.cs`.
- Modify:
  - `FacetBridge.cs`: the new-menu wrappers replace the three Effect Remover ones;
  - `QuartzHost.cs`: `Shutdown` calls `QuartzShell.Shutdown()`;
  - `FacetEffectRemover.cs`: page only;
  - `FacetSelfTestChecks.EffectRemover.cs`;
  - `Quartz/UI/UICore.Visibility.cs`, `UICore.cs` (line 111);
  - `Quartz/Features/EffectRemover/PageEffectRemover.cs`: the button calls `FacetBridge.OpenNewMenu("visuals.effectremover", fromClassic: true)`, still gated on `FacetBridge.Available()` only;
  - `Quartz/Features/FeaturePages.cs`: `Blocker` becomes `internal`;
  - Lang: core en/ko, and `KEY_PREFIXES.md`.

**Interfaces:**
- **`internal static class MenuHandOff`** (namespace `Quartz.UI`) has `internal enum Menu { None, Classic, New }` and `internal static Menu ReturnTo`: where a user's close of the menu now showing goes back to. Setting it replaces any earlier value.
- **`FacetBridge` wrappers** (P6 shape):

  | Wrapper | Probes? | Notes |
  |---|---|---|
  | `void OpenNewMenu(string pageKey = null, bool fromClassic = false)` | yes | |
  | `bool HideNewMenu()` | no | true when the window was showing; its `Hidden` does any hand-back |
  | `bool NewMenuVisible()` | no | |
  | `void PrebuildNewMenu()` | yes | |
  | `void TickNewMenu()` | no | |

  `OpenEffectRemover`, `HideEffectRemover` and `EffectRemoverVisible` are deleted.
- **`internal static class QuartzShell`** (`#if !IL2CPP`):
  - `const string WindowId = "quartz.menu"`, `PageStore = "menu.page"`;
  - `Surface Surface`, `Window Window`, `Nav Nav`, `string CurrentPage` (getters);
  - `void Prebuild()`;
  - `void Open(string pageKey = null, bool fromClassic = false)`;
  - `bool Hide()`;
  - `void Go(string pageKey)`;
  - `void OpenClassic(int state)`;
  - `void Changed()`;
  - `void ApplyLook()`;
  - `void Tick()`;
  - `void Shutdown()`;
  - `Bind<bool> FeatureSwitch(string featureId)` = `Bind.To(() => FeatureService.IsEnabled(id), v => FeatureService.SetEnabled(id, v))`;
  - `string Label(string localeKey, string title)` = `localeKey` when `MainCore.Tr.HasKeyForLanguage(localeKey, "en-US")`, else `title`;
  - `static readonly Dictionary<string, Action<VisualElement>> Moved`. In this task it holds only `"visuals.effectremover" → FacetEffectRemover.Page`, which is now `internal`.
- **The window:**
  - It is `QuartzHost.NewSurface("QuartzMenu", Layer.Window)` and `new Window(s.Root, WindowId, "QUARTZ", rect)`.
  - Logo: an 18×18 element named `menu-logo`, with the embedded `UISprite.QuartzLogo` sprite as its background, inserted at index 0 of `Window.TitleBarLeft`.
  - `Nav = new Nav(w.Body, roots, 160f)` with `Nav.OnInsetChanged = w.SetContentInset`.
  - Rect: on the first visible tick when `QuartzHost.Host.Read("window." + WindowId)` was null at build, `Window.Rect = ShellModel.InitialRect(Surface.Root.layout.width, Surface.Root.layout.height)` (the panel's size, which is known by then). Otherwise Facet's saved geometry applies.
  - `ApplyLook()` sets `Window.Scale = Conf.UIScale / 0.85f` (the default 0.85 is 1×) and the root's opacity to `Conf.PanelOpacity`. `Open` calls it.
- **The rail.**
  - Three `NavItem.Heading(null, …)` groups. Each holds `NavRegistry.AllCategories` with that `ShellModel.RailGroup`, in registry order; −1 is skipped.
  - Each category item:
    - Title is `Label(c.LocaleKey, c.Title)`;
    - `VisibleWhen = () => c.IsVisible && NavRegistry.PagesIn(c.Key).Count > 0`;
    - `Revision` is a function of `NavRegistry.Version` and the shell's `Changed()` counter, so `Nav.Tick()` rebuilds the open category (keeping scroll) and the search index goes stale when either moves.
  - `Open` rebuilds the whole `Nav` only when the ordered list of `AllCategories` keys differs from the one it was built with (an addon added a category).
- **A category's page.**
  - When `!NavRegistry.ShowsSubmenu(c)`: build its one page.
  - Otherwise: `Tabs.Pages(p, items, ShellModel.TabIndex(pages, CurrentPage, FeaturePages.FeatureOf(<current page>)))`. Each item is `new NavItem(Label(pg.LocaleKey, pg.Title), b => BuildPage(b, pg)) { VisibleWhen = pg.Visible }`.
- **`BuildPage(p, page)`.** When `p.panel != null` (a live build, not the search index's throwaway), record `CurrentPage = page.Key` and `Host.Write(PageStore, page.Key)`. Then, first match wins:

  | Case | What it draws |
  |---|---|
  | `Moved[page.Key]` exists | that builder |
  | `FeaturePages.IsStub(page)` | **stub:** `PageHeader(Label(…), FeatureSwitch(id), o => o.DescriptionText = <FEATURE_OFF_NEEDS with the blocker's name, or FEATURE_OFF_HINT, via QuartzHost.Text>)`; with `handle.Error`, a `Card(null)` holding `Controls.Info(card, "", () => error)` |
  | otherwise | **classic:** `PageHeader(Label(…), <FeatureSwitch(FeatureOf(page)) or null>, o => o.Id = "menu-header")`, then `Card(null)` holding `Controls.Action(card, "MENU_CLASSIC_PAGE", "MENU_CLASSIC_OPEN", () => OpenClassic(page.State), o => o.Id = "menu-classic")` |

  Addon pages, the Key Viewer page and decision 11's pages all land in "classic".
- **Show and hide.**
  - `Prebuild()` builds the window hidden (idempotent), then goes to `ShellModel.LastPage(Host.Read(PageStore), k => NavRegistry.ByKey(k) != null, CorePages.HomePageKey)`.
  - `Open(key, fromClassic)`:
    1. `Prebuild()`. A build that throws disposes the half-built surface and leaves the classic menu as it was.
    2. With `fromClassic`: `ReturnTo = None`, `UICore.HandToPreview()`, `ReturnTo = Classic`.
    3. `Go(key)` when given; otherwise the page is where it was left.
    4. `ApplyLook()`, `Surface.BlocksGameInput = Conf.BlockInputsWhileMenuOpen`, `Visible = true`.
  - `Surface.Hidden`: if `ReturnTo == Classic`, set `None` and call `UICore.BackFromPreview()`.
  - `OpenClassic(state)`: `ReturnTo = None`, hide, then `UICore.OpenAt(state)`.
  - `Tick()` returns at once while hidden with nothing pending. While visible it calls `Nav.Tick()` and the first-show rect.
  - `Shutdown()` unhooks `Hidden` before `QuartzHost` disposes the surface, as plan 6 did.
- **`UICore`:**
  - `MenuShowing => isOpen || FacetBridge.NewMenuVisible()`;
  - `HandleUpdate` calls `FacetBridge.TickNewMenu()` before its `canvasObj == null` return;
  - line 111 guards with `NewMenuVisible()`;
  - `internal static void OpenAt(int state)` = `Open()`, then `MenuFactory.SetState(state)` when `state >= 0`, then `ReturnTo = New`.
- **The hand-off rules.** Rows marked 6 are Task 6's.

  | Event | Showing before | Result |
  |---|---|---|
  | "Open it" on a classic card | new | new hides; classic opens on that page (not the hub) |
  | user closes classic (X, Escape, hotkey) | classic, `ReturnTo == New` | classic closes; new reopens on the page it showed |
  | Effect Remover's button in classic | classic | classic closes (remembering hub and panel, plan 6); new opens on Effect Remover |
  | user closes new (X, Escape, hotkey) | new, `ReturnTo == Classic` | new hides; classic comes back where it was |
  | any classic opener (`UICore.Open`) | new | new hides; classic opens. If new had come from classic, classic just comes back (plan 6). Otherwise `ReturnTo = New` |
  | `UICore.Rebuild` | either | what showed still shows and the other stays shut. Close and Open inside `Rebuild` run with a `rebuilding` flag, so no hand-back fires |
  | 6: hotkey, switch off | none | classic opens to the hub, as today |
  | 6: hotkey, switch on and `FacetBridge.Available()` | none | new opens on the page left (Home the first time) |
  | 6: Show at startup, switch on | startup | new, one pump after the first `Initialize`; `Rebuild` never opens it |

- **Lang (core):**

  | Key | en-US | ko-KR |
  |---|---|---|
  | `MENU_CLASSIC_PAGE` | This page is still in the classic menu | 이 페이지는 아직 기존 메뉴에 있습니다 |
  | `MENU_CLASSIC_OPEN` | Open it | 열기 |

  `KEY_PREFIXES.md` gets a row: `MENU_` · the new menu (Facet) · `Quartz/UI/FacetUi/`.

- [ ] **Step 1: Write `FacetSelfTestChecks.Shell`**, run after Suite. Clear `window.quartz.menu` first; `Run`'s `finally` already restores `FacetWindows`. Then wait for `WarmUp.Done`.
  - **Perf.**
    - Call `FacetBridge.PrebuildNewMenu()`; `info` the cold build's ms.
    - Wait 3 frames. Time `FacetBridge.OpenNewMenu()` to the content `ScrollView`'s first `generateVisualContent`, as plan 6's `FirstOpen` does.
    - Check `[perf] new menu first open` ≤ 50 ms.
    - Also check: visible, `UICore.MenuShowing`, and `!UICore.IsOpen`.
  - **Window.**
    - `Window.Rect` ≈ `InitialRect(<panel size>)` ± 2.
    - `TitleBarLeft[0].name == "menu-logo"` and it has a background image.
    - The rail's entry labels equal, in order, the translated labels of the visible categories grouped by `RailGroup`. The rail holds exactly 2 rule elements and no `CREDITS` entry.
  - **Pages.**
    - `Go("visuals.effectremover")`: `CurrentPage` is that key, the selected tab reads `Tr("SECTION_EFFECT_REMOVER")`, and `FXRM_MODE` is in the shell's content.
    - For every page in `NavRegistry.AllVisible()`, `Go(key)` leaves no label containing "failed to build". `info` the moved, classic and stub counts.
    - `Go("profiles.main")`: `menu-classic` is present, and the header title reads `Tr("PROFILES")`.
  - **Hand-off.**
    - Click `menu-classic`. Within 2 frames: the shell is hidden, `UICore.IsOpen`, `!UICore.IsReorganizing`, and `CurrentMenuState == NavRegistry.StateFor("profiles.main")`.
    - `UICore.Close()`: within 2 frames the shell is visible on `profiles.main`.
    - The same again with `UICore.EscapeDown()` in place of `Close()`.
    - The same again with `UICore.Toggle()`.
  - **Feature toggle** (Review Focus 3). `Go(<ottoicon's page>)`, then `FeatureService.SetEnabled("ottoicon", false)`. Within 3 frames:
    - the selected tab is still Otto Icon's;
    - `menu-header-input` reads off;
    - there is no failed card.

    `SetEnabled(true)`: within 3 frames `menu-classic` is back on the same tab. Restore the original state in a `finally`.
  - **Last page** (Review Focus 5).
    - Hide, then Open: the same page, and `Host.Read("menu.page") == CurrentPage`.
    - `Host.Write("menu.page", "nope.page")`, `QuartzShell.Shutdown()`, then `OpenNewMenu()`: `CurrentPage == "home.overview"`. Until Task 8 that page is a classic card.
  - **Language** (Review Focus 4).
    - `Tr.Language = "ko-KR"`: in the same frame, the Overlay rail label equals `Tr.Get("OVERLAY")`.
    - `zh-CN`: likewise, and `Fonts.Has(Fonts.Regular(QuartzHost.Host), <label>)`.
    - Restore the language.
  - **Wheel.** On Effect Remover, two `FacetBridge.TickWheel(<content centre>, -1f)` calls in one frame grow the content scroll's `scrollOffset.y` by exactly one notch (60).
  - **Closed cost.** Hide the window, then make 600 `FacetBridge.TickNewMenu()` calls. `[perf] closed tick mean` ≤ 2 µs.
- [ ] **Step 2: Adapt the plan 6 Effect Remover checks.**
  - `Body` is the shell's content with Effect Remover open.
  - `FacetBridge.OpenNewMenu("visuals.effectremover", true)` replaces `OpenEffectRemover`, and `QuartzShell.Surface` or `Window` replaces `FacetEffectRemover.Surface` or `Window`.
  - `FirstOpen` is deleted; Step 1's perf check replaces it.
  - `MenuHandOff` keeps every assertion: close button, Escape, hotkey, opener, rebuild behind, and the hub return.
  - `Teardown`: open from classic, then `QuartzHost.Shutdown()`; no menu comes back.
- [ ] **Step 3: Run the self-test.** Expected: build errors (`QuartzShell` and the wrappers are not defined).
- [ ] **Step 4: Implement** `MenuHandOff`, `QuartzShell`, `QuartzShell.Pages`, the wrappers, the `UICore` changes, the `PageEffectRemover` call, `Blocker` made internal, the Lang keys and `KEY_PREFIXES.md`.
- [ ] **Step 5: Run the self-test.** Expected: `fail=0`, with the `[perf]` line ≤ 50 ms.
- [ ] **Step 6: Commit**, so that each commit builds:
  - `feat: add the new menu's window with a sidebar, tab rows and classic-menu cards`: everything but the Effect Remover move, with the old wrappers kept;
  - `refactor: build Effect Remover's Facet page inside the new menu`: page only, `PageEffectRemover`, the old wrappers deleted;
  - `test: check the new menu's window and hand-off in game`.

### Task 5: Effect Remover in the new menu's look

**Files:**
- Modify: `Quartz/UI/FacetUi/FacetEffectRemover.cs`, `Quartz/Features/EffectRemover/Lang/en-US.json` and `ko-KR.json`, `FacetSelfTestChecks.EffectRemover.cs`.

**Interfaces:**
- **Header.** `Page(p)` opens with `Controls.PageHeader(p, "SECTION_EFFECT_REMOVER", QuartzShell.FeatureSwitch("effectremover"), o => { o.Id = "FXRM_HEADER"; o.Description = "FXRM_DESC"; })`. It replaces the menu's "Use this feature" row.
- **Cards.** Then `Card(null)` holding the Mode dropdown, and the mode-dependent body after it:
  - Simple: one `Card(null)` with today's rows.
  - Enhanced: `Card(null)` holding "Allow saving", then `Card("HEADING_NON_DLC_EVENTS")`, `Card("HEADING_DLC_EVENTS")` and `Card("HEADING_MISC")`. Each replaces the `Controls.Section` of that title.
  - Nested sections (excluded filters, planet, track, decoration types) stay `Controls.Section`s. Every row Id is unchanged. No row is marked advanced; plan 11's Visuals pass decides that.
- **Lang (Effect Remover):** `FXRM_DESC` = "Removes the effects you pick from levels as they load" / "레벨을 불러올 때 고른 효과를 제거합니다".

- [ ] **Step 1: Write the checks** in `FacetSelfTestChecks.EffectRemover`:
  - `FXRM_HEADER`'s title reads `Tr("SECTION_EFFECT_REMOVER")` and is bold, with fontSize `QuartzHost.Host.Theme.Sizes.Title`;
  - `FXRM_HEADER-input` matches `FeatureService.IsEnabled("effectremover")`;
  - in Enhanced, labels reading `Tr(HEADING_NON_DLC_EVENTS)`, `Tr(HEADING_DLC_EVENTS)` and `Tr(HEADING_MISC)`, upper-cased, exist;
  - plan 6's row-parity and mode-rebuild checks still pass.
- [ ] **Step 2: Run the self-test.** Expected: FAIL on the header and card checks.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run.** Expected: `fail=0`.
- [ ] **Step 5: Commit** `feat: give Effect Remover a page header and cards in the new menu`, with the Lang hunks.

### Task 6: The experimental switch

**Files:**
- Modify: `Quartz/UI/UICore.Visibility.cs`, `UICore.cs`, `Quartz/UI/Factory/Page/PageSettings.Sections.cs`, `Quartz/Features/EffectRemover/PageEffectRemover.cs`, `Quartz/UI/FacetUi/FacetBridge.cs` (`WarmUp`), `FacetSelfTest.cs`.
- Create: `Quartz/UI/FacetUi/FacetSelfTestChecks.HandOff.cs`.

**Interfaces:**
- **`internal static bool UICore.NewMenuWanted => MainCore.Conf.ExperimentalNewMenu && FacetBridge.Available();`** (plan 9's field).
- **`Toggle()`**, in order:
  1. `if (FacetBridge.HideNewMenu()) return;`
  2. `if (isOpen) { Close(noAnimate); return; }`. `Close` hands back to the new menu when `ReturnTo == New`.
  3. `if (NewMenuWanted) { FacetBridge.OpenNewMenu(); return; }`
  4. `OpenToHub();`

  `HandleUpdate` and `ToggleHotkeyAllowed` are unchanged (plan 9 branches inside `Toggle`).
- **Startup.** `Initialize`'s Show-at-startup open goes to `MainThread.Enqueue(() => FacetBridge.OpenNewMenu())` when `NewMenuWanted` on the first `Initialize` of the session (a static flag). Otherwise it stays as today.
- **Prebuild.** `FacetBridge.WarmUp` schedules `PrebuildNewMenu` 1 s after the warm-up when the switch is on, so the first open is warm (P27's fix).
- **The classic Settings page** (X3). At the end of `CreateBehaviourSection`, only when `FacetBridge.Available()`:
  - `GenerateUI.Toggle(row, false, Conf.ExperimentalNewMenu, v => { Conf.ExperimentalNewMenu = v; MainCore.ConfMgr.RequestSave(); if (v) FacetBridge.PrebuildNewMenu(); }, "Experimental: new menu", "prism_new_menu")`. The id gives the key `PRISM_NEW_MENU`, which is plan 9's.
  - `.Rect.AddToolTip("PRISM_NEW_MENU_TIP", <plan 9's English>)`.
- **The Effect Remover page's button** shows only when `FacetBridge.Available() && MainCore.Conf.ExperimentalNewMenu`.

- [ ] **Step 1: Write `FacetSelfTestChecks.HandOff`.** Save `ExperimentalNewMenu` and every feature switch it touches, and restore them in a `finally`. Every step waits 2 frames, and every check also asserts that at most one of `UICore.IsOpen` and `FacetBridge.NewMenuVisible()` holds.
  - **Switch off.**
    - `Toggle()` gives the classic menu open in the hub, with the shell hidden; `Toggle()` again gives both shut.
    - `!PreviewButtonOnPage()`.
    - `SwitchRowOnSettingsPage()` is true: the classic Settings page, built into a throwaway host, has a label reading `Tr("PRISM_NEW_MENU")`.
  - **Switch on.**
    - `Toggle()` shows the shell, and `MenuShowing`. `Toggle()` hides it, and nothing is open.
    - `PreviewButtonOnPage()`.
  - **Opener over the new menu.** Shell up, then `UICore.Open()`: shell hidden and classic open. `UICore.Close()`: the shell is back.
  - **Ping-pong** (Review Focus 2):
    1. Classic open.
    2. `OpenNewMenu("visuals.effectremover", true)`.
    3. `Go("profiles.main")`, then click `menu-classic`.
    4. `UICore.Close()`: the shell is back.
    5. The shell's close button: nothing reopens, and both are shut after 4 more frames.
  - **Rebuild behind.**
    - Shell up with `ShowOnStartup` true, then `UICore.Rebuild()`: the shell still shows and classic stays shut.
    - Classic opened from the shell, then `UICore.Rebuild()`: classic is still open and the shell stays hidden.
  - **Absent mode** (`FacetSelfTest.Run`, facet-absent branch; Review Focus 1): with `ExperimentalNewMenu = true`, `UICore.Toggle()` opens the classic menu, `!FacetBridge.NewMenuVisible()`, `!SwitchRowOnSettingsPage()` and `!PreviewButtonOnPage()`. Restore. This branch runs in Task 11's run D.
- [ ] **Step 2: Run the self-test.** Expected: FAIL on switch-on `Toggle` and on the switch-off settings row.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run.** Expected: `fail=0`.
- [ ] **Step 5: Commit:**
  - `feat: open the new menu from the menu hotkey behind the experimental switch` (`UICore`, startup, prebuild);
  - `feat: add the new-menu switch to the classic Settings page`;
  - `feat: show Effect Remover's new-menu button only with the switch on`;
  - `test: check the experimental switch and the menu hand-offs in game`.

### Task 7: Search over moved and classic pages

**Files:**
- Modify: `Quartz/UI/FacetUi/QuartzShell.cs`.
- Create: `FacetSelfTestChecks.Search.cs`.

**Interfaces:**
- **The box.** `Window.ShowSearch("SEARCH", q => Nav.Filter(q))`: Facet's search over every page each category builds. Moved pages index their rows. Classic and stub pages index their `PageHeader` title, so a page that has not moved is found by name and lands on its classic card, where "Open it" is one click.
- **The ceiling, in a comment** on the `ShowSearch` line: rows inside classic pages are found only by the classic menu's own search. A Facet hook for a result landing on a page would make a classic result jump straight to the classic menu.

- [ ] **Step 1: Write `FacetSelfTestChecks.Search`.**
  - **A moved page's row.**
    - `Nav.Filter("allow saving")`: `Nav.Index.Find("allow saving")` holds a hit with `Key == "FXRM_ENABLE_SAVE"` and `Tab == "SECTION_EFFECT_REMOVER"`.
    - Clicking that result row opens Visuals on Effect Remover, with `FXRM_ENABLE_SAVE` inside the viewport.
  - **A classic page.**
    - `Nav.Filter("profiles")`: a hit with `Key == "PROFILES"`.
    - Clicking it shows `menu-classic` within 3 frames, with `CurrentPage == "profiles.main"`.
    - Clicking `menu-classic` opens classic on that state, and `UICore.Close()` brings the shell back.
  - **Korean initials.** With `Tr.Language = "ko-KR"`, `q = Hangul.Initials(Tr.Get("FXRM_ENABLE_SAVE"))`. `Find(q)` contains `Key == "FXRM_ENABLE_SAVE"`. Restore the language.
  - **A fresh index** (Review Focus 3). `Find("otto icon")` hits Key `SECTION_OTTO_ICON`. After `SetEnabled("ottoicon", false)` and 2 frames, `Find("otto icon")` hits the stub's header (Key `"Otto Icon"`). Restore.
  - **Typing.** With `Window.Search` focused, `!UICore.ToggleHotkeyAllowed(true, true)`.
  - **Clearing.** `Nav.Filter("")` returns to the page.
- [ ] **Step 2: Run.** Expected: FAIL: no index yet, because no search box is wired.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run.** Expected: `fail=0`, and the log shows Facet's `[perf] search index N pages M ms`.
- [ ] **Step 5: Commit:**
  - `feat: search the new menu, pages still in the classic menu included`;
  - `test: check the new menu's search in game`.

### Task 8: Home, and Edit layout

**Files:**
- Create: `Quartz/UI/FacetUi/ShellHome.cs`, `FacetSelfTestChecks.Pages.cs`.
- Modify:
  - `Quartz/UI/Home/HomeCard.cs`, `HomeRegistry.cs`, `Quartz/UI/Factory/Page/PageHome.cs`;
  - `Quartz/Features/KeyViewer/KeyViewerModule.cs`, `KeyLimiter/KeyLimiterModule.cs`, `Tuf/TufModule.cs`, `Optimizer/OptimizerModule.cs`;
  - `QuartzShell.cs` (`EditLayout`, the layout return in `Tick`) and `QuartzShell.Pages.cs` (the `Moved` entry, and the classic card's layout row);
  - Lang: core.

**Interfaces:**
- **Plain status lines.**
  - `public Func<IEnumerable<string>> HomeCard.Lines`. `HomeRegistry.Add` needs `Build` or `Lines`.
  - `PageHome` draws `HomeUI.Line` per line for a card with no `Build`, so the classic Home looks the same.
  - The four modules give `Lines` with today's exact strings in place of `Build`.
- **`internal static void ShellHome.Build(VisualElement p)`**, as `Moved["home.overview"]`:
  - **Header.** `PageHeader(p, "HOME", null, o => o.DescriptionText = …)`, formatted from `MENU_HOME_LINE` with:
    - `{0}` = `Info.DisplayName + " " + Info.DisplayVersion`;
    - `{1}` = features on;
    - `{2}` = `FeatureService.Features.Count`;
    - `{3}` = `ProfileManager.Active`.
  - **Quick switches.** `Card(p, "MENU_QUICK_SWITCHES", o => o.Actions = [("MENU_EDIT", EditSwitches)])` holds `Tiles(…, o => o.Columns = 3)`. There is one `Tile` per id in `ShellModel.ParseSwitches(Host.Read("home.switches"), <feature ids>)`:
    - label `QuartzShell.Label(NameKey, Name)`;
    - `QuartzShell.FeatureSwitch(id)`;
    - `Open` goes to that feature's first page (`FeaturePages.StateFor`).

    `EditSwitches` is `Popup.Open(<the card body>, …)` with every feature as a `Tile` (`Switch = false`) that is on when chosen. A toggle writes `home.switches` through `FormatSwitches` and calls `QuartzShell.Changed()`.
  - **Status.** `Card(p, "MENU_STATUS")` holds `Controls.Info(card, Label(c.LocaleKey, c.Title), () => string.Join(" · ", c.Lines()))` per `HomeRegistry.Visible()` card with `Lines`. A throwing `Lines` shows `HOME_CARD_FAILED`.
  - **Layout.** `Card(p, "MENU_LAYOUT")` holds `Controls.Action(card, "MENU_LAYOUT_HINT", "MENU_EDIT_LAYOUT", QuartzShell.EditLayout, o => { o.Id = "menu-edit-layout"; o.VisibleWhen = () => PrismBridge.UiAvailable; })`.
- **`internal static void QuartzShell.EditLayout()`**: `ReturnTo = None`, hide, `PrismBridge.OpenLayoutEditor()`. `Tick` reopens the shell when `UICore.IsArranging` has gone true and then false, or after 120 frames with no session (logged once).
- **The classic card's layout row.** When `ShellModel.HasLayout(FeatureOf(page))`, it gains the same Action row (Id `menu-classic-layout`), with the same `VisibleWhen`: decision 3's "Edit layout →" on feature pages with an on-screen element.
- **Lang (core):**

  | Key | en-US | ko-KR |
  |---|---|---|
  | `MENU_HOME_LINE` | {0} · {1} of {2} features on · profile {3} | {0} · 기능 {2}개 중 {1}개 켜짐 · 프로필 {3} |
  | `MENU_QUICK_SWITCHES` | Quick switches | 빠른 전환 |
  | `MENU_EDIT` | Edit… | 편집… |
  | `MENU_STATUS` | Status | 상태 |
  | `MENU_LAYOUT` | Layout | 레이아웃 |
  | `MENU_LAYOUT_HINT` | Move Quartz's on-screen elements, and other Prism mods' | Quartz와 다른 Prism 모드의 화면 요소를 옮깁니다 |
  | `MENU_EDIT_LAYOUT` | Edit layout | 레이아웃 편집 |

- [ ] **Step 1: Write the Home checks** in `FacetSelfTestChecks.Pages`. Save and restore `home.switches` (`Run`'s map restore covers it) and the feature states.
  - **Header.** The header's description contains `Info.DisplayVersion`, `ProfileManager.Active`, and `"{on} of {total}"` from `FeatureService`.
  - **Tiles.**
    - With `home.switches` removed: the tiles are `ParseSwitches(null, ids)`, labelled in order.
    - With `"ottoicon,nope"`, after `Changed()` and 3 frames: exactly one tile, Otto Icon.
    - Its switch flips `FeatureService.IsEnabled("ottoicon")`; restore. Its label opens a page whose `FeatureOf == "ottoicon"`.
  - **Edit…** Clicking the link opens a popup. Toggling the Combo tile there adds `combo` to `home.switches`, and Home shows the Combo tile within 150 ms.
  - **Status.** The Key Limiter row's value equals `string.Join(" · ", <its card>.Lines())`. Built into a throwaway host, the classic `PageHome` shows the same line.
  - **Edit layout.**
    - `menu-edit-layout` is displayed iff `PrismBridge.UiAvailable`.
    - When it is, a click hides the shell, and `UICore.SharedLayoutOpen` holds within 60 frames.
    - `FacetBridge.HandleEscape()` in a later frame closes the editor, and the shell is back within 10 frames.
  - **The classic layout row.** `Go(<combo's page>)` shows `menu-classic-layout`; `Go("profiles.main")` does not.
- [ ] **Step 2: Run.** Expected: FAIL (Home is still a classic card).
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run.** Expected: `fail=0`, and `scripts/test.sh` passes.
- [ ] **Step 5: Commit:**
  - `refactor: let home cards give plain status lines` (`HomeCard`, `HomeRegistry`, `PageHome`, the four modules);
  - `feat: add the new menu's Home with quick switches, status and Edit layout` (with the Lang hunks);
  - `feat: link classic pages with an on-screen element to the layout editor`;
  - `test: check the new menu's Home in game`.

### Task 9: The Features grid

**Files:**
- Create: `Quartz/UI/FacetUi/ShellFeatures.cs`.
- Modify: `Quartz/UI/Factory/Page/PageFeatures.cs` (`Norm` becomes `internal`), `QuartzShell.Pages.cs` (`Moved["features.main"]`), Lang (core), `FacetSelfTestChecks.Pages.cs`.

**Interfaces:**
- **`internal static void ShellFeatures.Build(VisualElement p)`:**
  - **Header.** `PageHeader(p, "FEATURES", null, o => o.DescriptionText = …)`, formatted from `MENU_FEATURES_LINE` with `{0}` on and `{1}` total.
  - **Filter.** `Controls.Text(p, "FEATURES_FILTER", Bind.To(() => query, v => query = v))`, then `Controls.Choice(p, "", Bind.To(() => chip, v => chip = v), o => o.Items = ["MENU_CHIP_ALL", "MENU_CHIP_ON", "MENU_CHIP_OFF"])`. `query` and `chip` are statics, kept for the session.
  - **One `Card` per `FeatureCategories.Togglable()` category with features:**
    - title `Label(c.LocaleKey, c.Title)`;
    - `Note = () => on + " / " + n`;
    - `Actions = [(FeatureCategories.IsEnabled(c.Key) ? "MENU_TAB_HIDE" : "MENU_TAB_SHOW", () => { FeatureCategories.SetEnabled(c.Key, !enabled); QuartzShell.Changed(); })]`. This header action is the spec's category-header menu; its one item is a link;
    - `VisibleWhen` = any of its features matches.
  - **Tiles.** `Tiles(card, o => o.Columns = 5)`, one `Tile` per `FeatureCategories.In(c.Key)` handle:
    - label `Label(NameKey, Name)`;
    - `QuartzShell.FeatureSwitch(id)`;
    - `Open` goes to `FeaturePages.StateFor(id)`'s page;
    - `TooltipText = () => handle.Error`;
    - `VisibleWhen = () => Matches(handle)`.
  - **`Matches`** is `(Norm(query)` empty, or `PageFeatures.Norm(catLabel + " " + featureLabel + " " + id)` contains it`)`, and `(chip == 0 || (chip == 1) == handle.Enabled)`. `Norm` adds the Korean initials form under ko-KR, as the classic page does.
- **Lang (core):**

  | Key | en-US | ko-KR |
  |---|---|---|
  | `MENU_FEATURES_LINE` | {0} of {1} on · a feature that's off is fully unloaded | {1}개 중 {0}개 켜짐 · 꺼진 기능은 완전히 언로드됩니다 |
  | `MENU_CHIP_ALL` / `_ON` / `_OFF` | All / On / Off | 전체 / 켜짐 / 꺼짐 |
  | `MENU_TAB_HIDE` | Hide from sidebar | 사이드바에서 숨기기 |
  | `MENU_TAB_SHOW` | Show in sidebar | 사이드바에 표시 |

- [ ] **Step 1: Write the Features checks.** Restore the feature states, the categories, `query` and `chip`.
  - **Header.** The description holds the counts from `FeatureService`.
  - **Cards and tiles.** There is one card per togglable category with features, and each card's tile count equals `In(key).Count`. In Overlay, which has 8 features, the first five tiles share a `worldBound.y` and a width (± 1), and the sixth wraps.
  - **A tile.**
    - Clicking Otto Icon's switch flips the feature, and the Visuals card's note shows the new count within 150 ms.
    - Clicking its label opens its page in the shell.
  - **Filter.**
    - `query = "key"`: every displayed tile's haystack contains "key", Key Viewer and Key Limiter are displayed, and a category card with no match is hidden, all within 150 ms.
    - `chip = 2`: only disabled tiles show.
    - ko-KR with `query = Hangul.Initials(Tr.Get("SECTION_KEY_LIMITER"))`: the Key Limiter tile shows.
  - **Header action.** "Hide from sidebar" on Tweaks: within 3 frames the rail has no Tweaks entry. "Show in sidebar": Tweaks is back.
- [ ] **Step 2: Run.** Expected: FAIL (Features is still a classic card).
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run.** Expected: `fail=0`.
- [ ] **Step 5: Commit:**
  - `feat: add the new menu's Features grid` (with the Lang hunks);
  - `test: check the new menu's Features grid in game`.

### Task 10: The regrouped Settings page

**Files:**
- Create: `Quartz/UI/FacetUi/ShellSettings.cs`.
- Modify:
  - `Quartz/UI/Factory/Page/PageSettings.Sections.cs` (extract helpers) and `Quartz/UI/UICore.Shell.cs` (`ApplyUIScale`);
  - `Quartz/Prism/PrismBridge.cs`, `PrismWindows.cs`, `PrismSelfTestChecks.cs`;
  - `QuartzShell.Pages.cs` (`Moved["settings.main"]`);
  - Lang (core), `FacetSelfTestChecks.Pages.cs`.

**Interfaces:**
- **Extracted.** The classic page calls these too, with unchanged behaviour:
  - `internal static string[] PageSettings.Languages()` (sorted);
  - `string LanguageName(string lang)`;
  - `void ApplyLanguage(string lang)`;
  - `void ReloadLanguages(Action done)`;
  - `internal static void UICore.ApplyUIScale(float value, bool animate)`: the classic slider's `OnComplete` body.
- **Prism.**
  - `internal static void PrismBridge.OpenPrismSettings()` (NoInlining, `UiAvailable`-gated) calls `PrismWindows.ShowSettings()`, which toggles only when Prism · Settings is not already visible.
  - `PrismBridge.Tick()` calls `BindKey("Menu", …)` again whenever `(Conf.ToggleModifier, Conf.ToggleKey)` differs from the pair last bound, so any writer, classic or new, keeps PrismLib's key registry true.
- **`ShellSettings.Build(p)`:** `PageHeader(p, "SETTINGS", null)`, then these cards in order. Each row writes the same field the classic page writes.

  | Card | Row: label key, control, effect |
  |---|---|
  | `PRISM_GROUP_MENU` | `PRISM_MENU_HOTKEY`, Key: `KeyOptions.Capture = done => KeyCaptureService.Start(k => done(k == KeyCode.None ? null : k.ToString()), null)`, `CancelCapture = KeyCaptureService.Cancel`; Set parses the KeyCode, refuses (one log line, value kept) when `PrismRules.HotkeyRefused(Conf.ToggleModifier, key)`, else writes `ToggleKey` and saves |
  | | `PRISM_MENU_MODIFIER`, Dropdown with `PRISM_MODIFIER_NONE` and `Keybind.ModifierName(Ctrl/Alt/Shift/Cmd)`; refused like the key |
  | | `SHOW_OVERLAYER_PANEL_AT_STARTUP`, Toggle → `ShowOnStartup` |
  | | `BLOCK_INPUTS_WHILE_MENU_OPEN`, Toggle → `BlockInputsWhileMenuOpen`, and live on the shell's surface |
  | `MENU_GROUP_APPEARANCE` | `ACCENT_COLOR` (tooltip `DESC_ACCENT_COLOR`), Colour → `UICore.SetAccentColor(c, false)` and `RequestSave()` |
  | | `UI_SCALE`, Slider 0.8–1.6, step 0.01, `"0.00x"` → `UIScale`, save, `UICore.ApplyUIScale(v, false)`, `QuartzShell.ApplyLook()` |
  | | `WINDOW_OPACITY` (tooltip `DESC_WINDOW_OPACITY`), Slider 20–100, `"0'%'"` → `UICore.SetPanelOpacity(v / 100f, true)`, `ApplyLook()` |
  | | `COLOR_PICKER_HORIZONTAL` (tooltip `DESC_…`), Toggle → field, save, `UICore.Rebuild()` |
  | | `MENU_ANIM`, Action `MENU_OPEN_PRISM` → `PrismBridge.OpenPrismSettings`; `VisibleWhen = () => PrismBridge.UiAvailable` |
  | `MENU_GROUP_BEHAVIOUR` | `SHOW_TOOLTIP` (`DESC_SHOW_TOOLTIP`) → `Tooltip.Hide()` and the field; `MIDDLE_CLICK_TO_SET_AS_DEFAULT` (`DESC_…`); `SCROLL_SPEED`, Slider 20–300, step 1, `"0 px"` |
  | `LANGUAGE` | `LANGUAGE`, Dropdown of `LanguageName(l)` over `Languages()` → `ApplyLanguage` |
  | `MENU_GROUP_MAINTENANCE` | `HOME_RESET_ALL`, Danger button `HOME_RESET` (Help `HOME_RESET_DESC`), and `HOME_RESET_CLEAN`, Danger button `HOME_RESET_CLEAN`. Each → `MainThread.Enqueue(() => { if (ProfileManager.ResetLive(clean)) UICore.Rebuild(true); else <log> })` |
  | | `MENU_RELOAD_LANG`, Action `RELOAD` → `ReloadLanguages(null)` |
  | `PRISM_GROUP_EXPERIMENTAL` | `PRISM_NEW_MENU` (tooltip `PRISM_NEW_MENU_TIP`), Toggle → `ExperimentalNewMenu`, save |
  | | `MENU_CLASSIC_MENU`, Action `MENU_CLASSIC_OPEN` → `QuartzShell.OpenClassic(-1)` |
  | `Card(null)` | `MENU_SETTINGS_CLASSIC`, Action `MENU_CLASSIC_OPEN` → `OpenClassic(StateFor("settings.main"))`: the classic page's Updates and Font sections, which the spec's regroup leaves out |
  | `CREDITS` | `Note = CREDITS_LICENSE`; `Controls.Info` rows `CREDITS_TEAM`, `CREDITS_TESTERS`, `CREDITS_PORTS`, `CREDITS_DONORS`, each showing its `…_BODY` |

  Every row's Id is its label key. UI scale and window opacity now also apply to the new window; tooltips, middle-click and scroll speed act on the classic menu, as they always have.
- **Lang (core):**

  | Key | en-US | ko-KR |
  |---|---|---|
  | `MENU_GROUP_APPEARANCE` | Appearance | 모양 |
  | `MENU_GROUP_BEHAVIOUR` | Behaviour | 동작 |
  | `MENU_GROUP_MAINTENANCE` | Maintenance | 관리 |
  | `MENU_RELOAD_LANG` | Reload language files | 언어 파일 다시 불러오기 |
  | `MENU_ANIM` | Animation duration | 애니메이션 길이 |
  | `MENU_OPEN_PRISM` | Open Prism · Settings | Prism · 설정 열기 |
  | `MENU_CLASSIC_MENU` | Classic menu | 기존 메뉴 |
  | `MENU_SETTINGS_CLASSIC` | Updates and fonts are still in the classic menu | 업데이트와 폰트 설정은 아직 기존 메뉴에 있습니다 |

- [ ] **Step 1: Write the Settings checks.** Restore every field touched.
  - **Cards.** The upper-case card titles appear in the table's order.
  - **Toggles.** For each Toggle row (`SHOW_OVERLAYER_PANEL_AT_STARTUP`, `BLOCK_INPUTS_WHILE_MENU_OPEN`, `COLOR_PICKER_HORIZONTAL`, `SHOW_TOOLTIP`, `MIDDLE_CLICK_TO_SET_AS_DEFAULT`, `PRISM_NEW_MENU`):
    - a click on `<Id>-input` flips the `CoreSettings` field;
    - a classic Settings page built into a throwaway host shows the new value (the anti-goal "losing a setting by switching");
    - a second click restores it.
  - **Sliders and language.**
    - `UI_SCALE` set to 1.2 gives `Conf.UIScale == 1.2` and `Window.Scale ≈ 1.2 / 0.85`.
    - `WINDOW_OPACITY` set to 70 gives `Conf.PanelOpacity == 0.7`.
    - `SCROLL_SPEED` set to 120 gives `Conf.ScrollSpeed == 120`.
    - `LANGUAGE` set to ko-KR gives `Tr.Language == Conf.Language == "ko-KR"`, and the card titles relabel.
  - **Hotkey.**
    - The modifier set to Alt, then the key set to `"J"`, gives `ToggleKey == J`.
    - `"D"` is refused and `"LeftShift"` is refused; the key stays J.
    - Restore.
  - **Classic hand-offs.**
    - `MENU_CLASSIC_MENU` opens classic on its current page, and `Close` brings the shell back.
    - `MENU_SETTINGS_CLASSIC` opens classic on `settings.main`.
  - **The rest.**
    - The two Danger rows exist; they are not clicked.
    - `MENU_ANIM` is displayed iff `PrismBridge.UiAvailable`.
    - The `Credits` card holds 4 Info rows.
  - **Prism, in `PrismSelfTestChecks.NewMenu`:**
    - `PrismBridge.OpenPrismSettings()` shows Prism · Settings, and a second call leaves it showing;
    - setting `Conf.ToggleKey` to J directly, then 2 frames, rebinds `Keys.All`' Quartz `Menu` to J;
    - restore, and close Prism · Settings.
- [ ] **Step 2: Run both markers.** Expected: FAIL (Settings is still a classic card; `OpenPrismSettings` does not exist).
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run both markers.** Expected: `fail=0`, `scripts/test.sh` passes, and check-bridge with `OpenPrismSettings` added ends `OK`.
- [ ] **Step 5: Commit:**
  - `refactor: share the language and UI-scale actions of the Settings page`;
  - `feat: add the new menu's Settings page` (with the Lang hunks);
  - `feat: open Prism · Settings from Quartz and keep PrismLib's menu key in step`;
  - `test: check the new menu's Settings page in game`.

### Task 11: CI, the three runs, the tour and the docs

**Files:**
- Modify: `stubs/` (regenerated), `agents/project.md`.
- Throwaway, never committed: `Quartz/UI/FacetUi/UiTour.cs`, and its registration line in `Quartz/Core/QuartzRuntime.cs`.

- [ ] **Step 1: Stubs and CI.**
  - Run `tools/gen-stubs.sh`. Stage the Facet and UIElements entries only (P15).
  - Then:

    ```bash
    dotnet build stubs/Stubs.proj -c Release
    dotnet build Quartz/Quartz.csproj -c Release -p:UseStubs=true -p:GamePath= -p:GameData=
    dotnet build Quartz/Quartz.csproj -c Release -p:LoaderTarget=UMM -p:UseStubs=true -p:GamePath= -p:GameData=
    dotnet build Quartz/Quartz.csproj -c Release -p:Flavor=KeyViewer -p:UseStubs=true -p:GamePath= -p:GameData= -p:AutoInstall=false
    scripts/test.sh && python3 scripts/check_conventions.py
    python3 scripts/validate_i18n.py Quartz/Resource/Export/Lang Quartz/Resource/Export/Lang/en-US.json
    ```

    Expected: four `0 Error(s)`, all tests passing, every conventions line `ok:`, and validator exit 0.
  - Commit `chore: regenerate stubs for the new menu`.
- [ ] **Step 2: check-bridge on both loaders.** Build Release ML and UMM (`-p:AutoInstall=false`), then run plan 9 Task 8 Step 3's two `check-bridge.sh` lines with `M="… TouchLayout get_UiAvailable TickAnim WearSuite OpenPrismSettings"`. Expected: both end `OK: … loads without PrismLib (Available = False)`.
- [ ] **Step 3: Run A, MelonLoader beside Topaz and Sapphire** (the rig as it is). Run both markers. Expected: `fail=0` twice. Keep the `[perf]` lines.
- [ ] **Step 4: Run C, MelonLoader with Quartz alone.** Use plan 9 Task 8 Step 7's move-aside recipe, recording md5s first. Run the facet marker. Expected:
  - `MODE` quartz-only;
  - `fail=0`;
  - `[perf] new menu first open` ≤ 50 ms: the spec's budget.
- [ ] **Step 5: Run D, Quartz alone without Facet.** Keep run C's layout. Also move `$D/Runtime/versions/<current, from state.json>/Facet.0.2.dll` aside. Run the facet marker. Expected:
  - `MODE facet-absent`;
  - Task 6's absent checks PASS;
  - `fail=0`;
  - one `Facet unavailable` line.

  Then put everything back and compare md5s: every line is identical.
- [ ] **Step 6: The tour.**
  - Copy `/private/tmp/claude-501/-Users-preluminance-Coding-PrismMods/4d035537-c954-4404-aa08-bf522ef1f88b/scratchpad/UiTour.cs.bak` to `Quartz/UI/FacetUi/UiTour.cs` (namespace `Quartz.UI.FacetUi`) and register it as a DEBUG tick in `QuartzRuntime`, without committing either.
  - Changes to the tour:
    - `Out` is the same scratchpad's `quartz-tour-new/`;
    - it sets `ExperimentalNewMenu = true` and restores it;
    - it opens the new menu and shoots Home, then every rail entry and every tab through `QuartzShell.Go(key)`, scrolling long pages a viewport at a time (6 shots at most);
    - it also shoots the Features grid filtered by "key", the search results for "key", and the Edit… popup.
  - Run it with marker `ui-tour` (the tour's own marker, not the self-test's), beside Topaz and Sapphire.
  - Afterwards: `git checkout Quartz/Core/QuartzRuntime.cs && rm Quartz/UI/FacetUi/UiTour.cs`, then rebuild `Debug ML` (back up the store first) so the install carries no tour.
  - `git status --short` must be clean.
  - Tell the user where the before (`…/scratchpad/quartz-tour/`) and after (`…/quartz-tour-new/`) pictures are.
- [ ] **Step 7: Docs.** In `agents/project.md`, replace the "Facet (UI Toolkit) preview windows" block with "The new menu":
  - the switch and the hotkey branch;
  - `QuartzShell` built from `NavRegistry`;
  - plan 11 moves a page by adding a `QuartzShell.Moved` entry;
  - the classic card and `MenuHandOff`;
  - host store keys `menu.page` and `home.switches`;
  - `HomeCard.Lines`;
  - the `MENU_` prefix;
  - the self-test sections.

  Commit `docs: describe the new menu`.
- [ ] **Step 8: Hand back.**
  - Leave `facet-preview` unpushed.
  - Report the DONE lines of runs A, C and D, every `[perf]` line, and the tour folders.
  - List the Lang commits pending i18n sync.
  - Note that the PrismLib roadmap's plan 10 line is for whoever merges (this plan commits nothing outside Quartz).
