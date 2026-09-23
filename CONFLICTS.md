# Cross-mod conflicts: Sapphire, Bismuth, Quartz

Audit of where the three mods collide when installed together. Each entry has an ID and an empty
**Priority** cell for you to fill.

**Method.** Every `[HarmonyPatch(typeof(X), "Y")]` in the three trees was extracted and intersected
— 31 game methods are patched by two or more mods — then each overlapping patch class was read to
see whether its prefix can actually block. Writes to the game's global toggles (`RDC.*`, `GCS.*`,
`scrController.noFail`) were enumerated separately.

**Known gap in the method.** The extractor only sees attribute-style targets. Patches declared as
bare `[HarmonyPatch]` with a `TargetMethod()` are invisible to it, and that is exactly where
Bismuth's input gates live — those were found by hand. Assume the machine-generated list undercounts.

Quartz was pulled to `03d7a65` (2026-09-23) before this pass. It has since been restructured into 36
optional modules under `modules/` plus an addon SDK under `sdk/`, so most Quartz-side conflicts below
only exist **when that module is enabled**. That matters for triage: several of these are "two
features that do the same job", not "two mods that break each other".

| ID | Conflict | Mods | Severity | Priority |
| --- | --- | --- | --- | --- |
| C1 | Two key limiters gate the same six `RDInput` methods | B + Q | Critical | |
| C2 | Quartz flips `RDC.auto` globally to hide the autoplay label | Q + S + B | Critical | |
| C3 | Bismuth blocks the keyboard game-wide while its panel is open | B + S + Q | High | partly fixed |
| C4 | `RDC.noHud` written by both hide-UI features, one guarded, one not | B + Q | High | |
| C5 | Quartz forces no-fail and difficulty; Sapphire toggles them | Q + S | High | |
| C6 | Two key viewers, two overlay stacks | B + Q | High, product-level | |
| C7 | Blocking prefixes on `scnEditor.Play` | Q + S | Medium | |
| C8 | Quartz blocks the editor's Save while Effect Remover is on | Q + S | Medium | |
| C9 | Quartz replaces the game's required-mods check outright | Q + S | Medium | |
| C10 | Same screen-filter optimization implemented twice | B + Q | Medium | |
| C11 | The leak guard is ported into both mods | B + Q | Low | |
| C12 | Two texture caches on `TextureManager.LoadTexture` | B + Q | Low | |
| C13 | Sapphire transpiles `scnEditor.Update` | S (+ B, Q) | Low, latent | |
| C14 | PrismLib installs to a different folder per loader | all | Low | |
| C15 | Hotkeys | all | Low — checked, mostly clear | |

---

## C1 — Two key limiters gate the same six `RDInput` methods

Both mods ship a key limiter, and both implement it by patching the game's input aggregator:

| | Bismuth | Quartz |
| --- | --- | --- |
| `RDInput.GetMain` | ✓ `KeyViewer/KeyLimiter.cs:752` | ✓ `modules/KeyLimiter/KeyLimiterPatches.cs:28` |
| `RDInput.WentDown` | ✓ `:775` | ✓ `:10` |
| `RDInput.IsDown` | ✓ `:790` | ✓ `:16` |
| `RDInput.GetState` | ✓ `:806` | ✓ `:4` |
| `RDInput.GetStateKeys` | via reflection, `:157` | ✓ `:34` |
| `RDInput.WentUp` | — | ✓ `:22` |
| `Input.GetKeyDown` | ✓ `:829` | — |
| `SkyHookManager.HookCallback` | — | ✓ `Quartz/Game/HookInput.cs:286`, blocking prefix |
| chatter filter | `KeyLimiter.cs`, + blocking prefix on `scrMarginTracker.AddHit:852` | `modules/KeyLimiter/ChatterBlocker.cs` |

With both enabled the two allow-lists intersect: a key either one denies is denied, so the effective
limiter is the union of both restrictions and neither settings page describes what the user is
actually getting. The chatter filters compound the same way — one keystroke filtered twice against
two independent thresholds, and a key Quartz drops at the native tap never reaches Bismuth's
counters, so the key viewer and hit stats undercount silently.

Quartz's own comment on the SkyHook prefix records the worst case: *"ANY exception here propagates
into the tap and makes it SWALLOW the event — the keyboard dies game-wide."*

Fix: one owner, via a claim. Whichever mod loses it leaves its limiter off and says so in its
settings row, instead of both filtering and neither knowing.

## C2 — Quartz flips `RDC.auto` globally to hide the autoplay label

All three mods patch `scrShowIfDebug.Update`, which draws the flashing AUTOPLAY text. Two of them
learned not to do it by faking the autoplay flag; the third still does.

- Quartz — `modules/UiHider/UiHiderPatches.cs:24`:
  `Prefix(out bool __state) { __state = RDC.auto; if (ShouldHideOtto()) RDC.auto = false; }`,
  `Postfix { if (RDC.auto != __state) RDC.auto = __state; }`
- Bismuth — `Patches/Patches.cs:317`, postfix. Uses `RDC.noAutoHud`, with the comment *"Set it here
  instead of flipping RDC.auto, which corrupted the autoplay toggle."* (`Overlay/Overlay.Apply.cs:352`)
- Sapphire — `Patches/Patches.cs:385`, postfix. Disables the Text component, with the comment
  *"Bismuth's approach of flipping RDC.auto around the update made autoplay turn itself off in the
  editor."*

The `out __state` form is tidier than the old static field, but the exposure is unchanged: every
postfix on that method — Bismuth's and Sapphire's included — runs inside the window where `RDC.auto`
reads false. Anything sampling autoplay there gets the wrong answer, and if any patch in the stack
throws before Quartz's postfix, autoplay stays off. This is the shape of the original "autoplay is
broken" report, and the two other mods each hit it independently and wrote it down.

Fix: `RDC.noAutoHud`, the game's own flag for this, as Bismuth already uses. No coordination needed —
it stops being a global write.

## C3 — Bismuth blocks the keyboard game-wide while its panel is open

`KeyLimiter.BlockInputs` is true whenever Bismuth's panel is open and "block inputs while menu open"
is on. It gates the four `RDInput` entry points and postfixes `Input.GetKeyDown` itself, exempting
only `KeyCode.B` (`KeyViewer/KeyLimiter.cs:844`).

Every Sapphire hotkey, and every Quartz bind that reads through those paths, is dead while that panel
is up, with no error anywhere.

**Partly fixed.** Bismuth now claims `StateKey.InputCapture` while blocking and Sapphire stands down
in `Keybinds.Down` — one guard in the single function every Sapphire hotkey reads through. Quartz is
not covered until it adopts PrismLib.

## C4 — `RDC.noHud` written by both hide-UI features, one guarded, one not

- Bismuth: `Overlay/Overlay.Apply.cs:349` — `RDC.noHud = hideAll`, unconditional; and
  `Overlay/Overlay.cs:266` — `RDC.noHud = false`
- Quartz: `modules/UiHider/UiHider.cs:70` — `if (RDC.noHud != hideEverything) RDC.noHud = hideEverything`

Quartz's guard makes this worse, not better, when the two disagree: Bismuth writes its value, Quartz
sees a mismatch on its next apply and writes back, and the HUD flickers between them instead of one
simply winning. Turning off one mod's hide-UI does not bring the HUD back while the other's is on,
and nothing tells the user which mod is deciding.

`StateKey.GameHud` covers this. Sapphire already claims it in Editor Mode and Bismuth already honours
the claim; Quartz joining makes it three-way.

## C5 — Quartz forces no-fail and difficulty; Sapphire toggles them

Quartz's Nostalgia module writes these as a mode, not a one-off:

- `modules/Nostalgia/NostalgiaTweaks.cs:40,54` — `GCS.difficulty = Difficulty.Strict`
- `modules/Nostalgia/NostalgiaTweaks.cs:69,71` — `GCS.useNoFail = false`, `scrController.instance.noFail = false`
- `modules/Practice/PracticeDifficulty.cs:57` — `GCS.difficulty = (Difficulty)index`

Sapphire's editor mode cluster offers the user the same three controls:

- `Editor/Timeline/EditorEvents.cs:3104,3106` — toggles `GCS.useNoFail` and `scrController.noFail`
- `Editor/Timeline/EditorEvents.cs:3187` — sets `GCS.difficulty`
- `Util/Tweaks.cs:312,314` — forces no-fail on in Editor Mode

A user with Nostalgia or Practice on who presses Sapphire's no-fail button gets it reverted, silently,
on the next apply. `StateKey.NoFail` exists for this; difficulty needs a key adding.

## C6 — Two key viewers, two overlay stacks

Not a crash, a product decision, which is why it needs your priority more than the others. Quartz now
ships `modules/KeyViewer`, `modules/Overlay`, `modules/Panels`, `modules/Accuracy`, `modules/Combo`,
`modules/Judgement`, `modules/Countdown`, `modules/Calibration`, `modules/SongTitle` and
`modules/ProgressBar` — which is, feature for feature, most of what Bismuth is.

Enable both and the player gets two key viewers and two overlay stacks on screen, each with its own
settings page, its own layout editor and its own import path. They do not corrupt each other; they
just both draw.

Quartz's `Features/Interop/SettingsImporter.cs` already imports from AdofaiTweaks, KeyboardChatterBlocker,
Jipper's key viewer and resource packs, EnhancedEffectRemover and koren's resource packs — but not from
Bismuth. Whatever you decide here (split the surface, import Bismuth's layout, or leave both and let
users pick) is a bigger call than any single patch conflict.

## C7 — Blocking prefixes on `scnEditor.Play`

Two Quartz prefixes can cancel the original:

- `modules/NostalgiaEditor/NostalgiaEditorPatches.cs:12` — Space-360-tile, returns false when Space is
  pressed with a single tile selected
- `Quartz/Features/AprilFools/QuizGate.cs:165` — returns false to gate play behind a quiz

Sapphire patches the same method (`Patches/Patches.cs:63` prefix + postfix, plus a passive prefix at
`Editor/ModTools/EditorMagicShape.cs:434`). When Quartz cancels, Sapphire's postfix never runs, so
whatever it sets up on play-start is skipped for that keystroke only — an intermittent failure that
will be reported as a Sapphire bug.

`QuizGate` also has a blocking prefix on `scnGame.Play` (`:204`).

## C8 — Quartz blocks the editor's Save while Effect Remover is on

`modules/EffectRemover/EffectRemover.cs:218` strips events from the decoded level in a
`LevelData.Decode` postfix, and because the in-memory level is now the stripped copy, saving is
blocked outright: `SaveLevelEditorActionPatch.Prefix() => EditorSaveEnabled`.

Sapphire is an editor suite. With Effect Remover on, Ctrl+S does nothing, and Sapphire's level
variables — also a `LevelData.Decode` postfix, `Editor/Level/LevelVars.cs:379` — are written into a
level the user cannot save. Two postfixes on one method also means the order in which one strips and
the other reads is undefined.

Minimum fix: don't arm Effect Remover while `scnEditor.instance != null`, or say loudly in the editor
that saving is off.

## C9 — Quartz replaces the game's required-mods check outright

`Features/Interop/RequiredModsGate.cs:8` patches `RDEditorUtils.CheckModsDependency` with a prefix
that always returns false, substituting Quartz's own `provided` set for the game's answer. Only one
caller registers into that set today (`modules/KeyLimiter/ChartKeyLimiter.cs:56`).

Any level whose required-mods list names something Quartz does not know about now gets Quartz's
verdict rather than the game's. For a chart that requires an editor mod this changes what the editor
tells the user. Worth a look from the Sapphire side before it surprises someone.

## C10 — Same screen-filter optimization implemented twice

Bismuth's optimizer is an acknowledged port of Quartz's — `Patches/Optimizations.cs:66`: *"Quartz
optimizer port (NoOpScreenTile/ScrollPatch) … Thresholds match Quartz exactly."* Both ship blocking
prefixes on `ScreenTile.OnRenderImage` and `ScreenScroll.OnRenderImage`, and passive prefix+postfix
pairs on `VideoBloom.OnRenderImage`.

Harmony skips the remaining prefixes once one returns false, so this is not a double blit. The
user-visible problem is that the toggles lie: turning Bismuth's optimizer off does nothing while
Quartz's is on, and vice versa, because whichever prefix runs first decides.

## C11 — The leak guard is ported into both mods

Same story as C10. Bismuth's header says so outright: *"Quartz optimizer port (LeakGuardPatches) …
the ownership tests are the whole safety story, kept exactly as Quartz has them."* Both patch
`scrVisualDecoration.OnDestroy`, `scrCamera.SetCustomFrameRate`, `WorkshopLevelList.SelectLevel` and
`PracticeTimeline.Init`, and every one of those patches destroys a texture.

Likely benign — Unity's `!= null` is false for an already-destroyed object, so the second mod's
ownership test fails and it skips — but it is duplicated work on a path whose own comment says
loosening the ownership tests shows as pink or missing visuals, and the two have never been tested
running together.

## C12 — Two texture caches on `TextureManager.LoadTexture`

Quartz takes over the plain file-loading branch in a blocking prefix and returns a compressed texture
(`modules/Optimizer/OptimizerPatches.cs:9`). Bismuth caches in a postfix (`Patches/Optimizations.cs:39`).
Postfixes still run when a prefix blocks, so Bismuth ends up caching Quartz's compressed result —
harmless in itself, but Bismuth's cache contents then depend on a Quartz setting, and the memory is
spent twice.

## C13 — Sapphire transpiles `scnEditor.Update`

Sapphire is the only transpiler on a method all three mods patch (`Patches/Patches.cs:175`; Bismuth
postfixes at `Patches/Patches.cs:433`, Quartz at `modules/OttoIcon/OttoIcon.cs:283`). Transpilers
coexist with postfixes, so nothing is broken today. Listed because a second transpiler on that method
from any mod breaks one of them, and because it is tied to the method's IL shape — first thing to
re-verify after a game update.

## C14 — PrismLib installs to a different folder per loader

`PrismBootstrap` derives its install directory from the calling assembly: `<mods root>/PrismLib/`. On
this machine Sapphire and Bismuth sit at `UMMMods/<Mod>/`, giving `UMMMods/PrismLib/`, while Quartz
loads from `Mods/Quartz.Bootstrap.dll`, which would give `<game>/PrismLib/`.

Not a break — whichever loads first wins and the second resolves the already-loaded assembly out of
the AppDomain — but it means two copies on disk and two update checks. Worth special-casing when
Quartz adopts.

## C15 — Hotkeys

Checked, and mostly clear:

- Quartz's menu toggle defaults to **Alt+K** (`Quartz/IO/CoreSettings.cs:40-41`), Bismuth's to
  **Ctrl+B**, Sapphire's to **Ctrl+E**. No collision, and Sapphire's `Keybinds.Down` matches Shift and
  Alt exactly, so Alt+K cannot trigger its Shift+K.
- Sapphire binds 15 editor hotkeys, several unmodified: `I`, `O`, `U`, `,`, `.`, `[`, `]`. Quartz's
  per-module toggle binds are user-configured with modifiers (`UI/Utility/ToggleBinds.cs`) and ship no
  letter defaults, so a collision only appears if a user makes one.
- `PrismLib.Keys` reports collisions automatically once all three register. Sapphire and Bismuth do.

---

## Not audited

Other mods installed alongside these on this machine, none of them read: `EnhancedEffectRemover`
(Quartz both duplicates it as a module and imports its settings), `MacQOL`, `TUFHelper` (Quartz has a
`modules/Tuf`), `XPerfect` (both Bismuth and Quartz ship an `XPerfectBridge`), `BetterCountdown`
(Quartz has a `modules/Countdown`), `YouTubeStream`.

Both loaders are installed here — MelonLoader with `Mods/`, koren's UMM with `UMMMods/`. Running
Bismuth and Sapphire in the same session is fine; they were split from one codebase and are tested
together.
