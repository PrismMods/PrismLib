# Facet engine (milestone 1) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build Facet, a game-agnostic UI Toolkit engine for Unity mods. It is a new repository with a Gallery mod and both test suites, seeded from PrismLib.UI's general-purpose files.

**Architecture:**
- **One shipped DLL**, `Facet.0.1.dll` (namespace `Facet`).
- **`Facet/Core/`** holds the plain-BCL logic: matcher, Hangul initials, history, throttle, value scales, the cross-version dictionary and key capture. The offline test compiles it directly.
- **Everything else is UI Toolkit code**, verified in-game by the Gallery mod's autotest.
- **Each consumer creates a `Host`.** Colours are theme roles applied by `Paint`. Controls are bound to live values through `Bind<T>` and re-read on a timer.

**Tech Stack:**
- C#, `netstandard2.0`, built with `dotnet build` (SDK 10 is installed);
- UnityEngine reference assemblies from `UnityEngine.Modules` 2022.3.0 (`nuget.bepinex.dev`);
- `mcs` and `mono` for the offline test;
- UMM through UMMBridge, then MelonLoader, for the Gallery mod.

**Spec:** `PrismLib/docs/superpowers/specs/2026-11-06-facet-ui-engine-design.md`. Read it first; this plan argues from it.

## Global Constraints

**Repository and build:**
- The repository is `/Users/preluminance/Coding/PrismMods/Facet`, licensed MIT. Local only: creating the GitHub repo `PrismMods/Facet` waits for the user.
- `netstandard2.0`. Compile against `UnityEngine.Modules` **2022.3.0**. Unity 6 (ADOFAI 3.x) is the only verified version.
- The assembly name is `Facet.<major>.<minor>`, derived from `<Version>`: **0.1.0 gives `Facet.0.1.dll`**. The namespace is always `Facet`. Changes within a minor are append-only: enum members go last, signatures never change.

**What Facet must never contain:**
- No `RDInput`, scene names, `Assembly-CSharp`, `UnityModManager`, `MelonLoader` or `Harmony` under `Facet/`. Only `Facet.Mod/` may name a loader.
- No code from Overlayer, O5Kit or the Overlayer-derived parts of Quartz.

**Runtime rules:**
- **Every call into consumer code goes through `Safe`.** Log at most **once per host and site per 10 s**.
- **A hidden window does no per-frame work. Nothing logs per frame.**
- **Performance budgets:**
  - first window opened after warm-up: **≤ 50 ms**;
  - a refresh pass with 100 bound controls visible: **≤ 0.2 ms**;
  - re-theming an open window: **≤ 16 ms**;
  - building the first search index: **≤ 100 ms**.
- **UI Toolkit landmines** (`PrismLib/CLAUDE.md` § UI):
  - show and hide with `display`, never `SetActive`;
  - before Unity 6, fonts are handed down per element;
  - build every `ScrollView` through `Skin.Scroll`;
  - `Box` defaults to `flexShrink = 0`; every scroll container and every ancestor up to the fixed-height window needs `minHeight = 0`;
  - an absolutely positioned element pins all four offsets.

**IL2CPP rules:**
- No public Facet type derives from a Unity type.
- Every `RegisterCallback` goes through `Events.On`.
- Per-element data goes through `Data`. No other `ConditionalWeakTable`.

**Commits:** Conventional Commits, split finely, with **no Co-Authored-By trailer** (`agents/commits.md`, copied from PrismLib in Task 1).

**In-game testing (rig in memory `mac-ingame-test-rig`):**
- One in-game test at a time.
- Wait about 5 s after a quit before launching.
- Never press Screen Time's "Ignore Limit".
- Never take screenshots unattended.

## Review Focus

1. **A mod replaces its settings object wholesale** (a profile switch assigns a new instance). A `Bind.To(() => Main.Settings.X, …)` must show the new object's value at the next refresh. Pinned in Task 9.
2. **A callback rebuilds or disposes the window it runs in**: a button that calls `nav.Rebuild()` on its own page, or a close handler that disposes the surface. There must be no exception, and the refresher must stop for the dead surface. Pinned in Tasks 9 and 16.
3. **Escape with a text field focused inside a popup over a window** must only blur the field: the popup and window stay. The next Escape closes the popup, the one after that the window. Pinned in Task 12.
4. **A `Field` whose getter returns null or an unconvertible object** shows an unavailable row. It must not throw out of `Form.Build`. Pinned in Task 15.
5. **A very long or Korean label in a row** keeps the row height. The label ellipsises before the control instead of pushing it out of the window. Pinned in Task 10.

---

## File structure (Facet repository)

```
Facet/
  LICENSE  README.md  CLAUDE.md  .gitignore  nuget.config  agents/commits.md
  build.sh            # dotnet build Facet + Facet.Mod, run test.sh, install the Gallery (UMM) into the game
  test.sh             # offline: mcs tests/FacetTest.cs + Facet/Core/*.cs, then a guard grep; prints ALL PASS
  tools/ingame.sh     # quit game, launch, wait for AUTOTEST DONE in gallery.log, print the summary
  Facet/Facet.csproj
  Facet/Core/         # BCL only: Throttle, Shared, Safe, Hangul, Search, ValueScale, History, KeyCapture, Words
  Facet/              # UI Toolkit: Host, Data, Events, Surface, Fonts, Window, Input, Theme, Paint, Ui, Anim,
                      # Skin, Cursors, Tex, Popup, Tabs, Bind, Control, Options, Refresher, Controls.*.cs,
                      # Nav, SearchIndex, Form, ColourPicker, GradientEditor, WarmUp
  Facet.Mod/Facet.Mod.csproj  Main.cs  Gallery.cs  AutoTest.cs  Info.json
  tests/FacetTest.cs
```

---

### Task 1: Repository scaffold, build and offline runner

**Files:**
- Create: `Facet/LICENSE` (MIT, "Copyright (c) 2026 PrismMods"), `README.md` (one paragraph from the spec's "What it is"), `.gitignore` (`bin/ obj/ dist/ *.user`), `agents/commits.md` (PrismLib's file; its **This repo** section says "split finely, like PrismLib")
- Create: `nuget.config` (sources `nuget.org` and `https://nuget.bepinex.dev/v3/index.json`)
- Create: `Facet/Facet.csproj`, `Facet/Core/Throttle.cs`, `tests/FacetTest.cs`, `test.sh`, `build.sh`

**Interfaces:**
- Produces: `Facet.Throttle.Allow(string key, double nowSeconds, double windowSeconds = 10) -> bool` and `Throttle.Reset()`; the `Check(bool ok, string what)` helper in `tests/FacetTest.cs`; `./test.sh` and `./build.sh`.

- [ ] **Step 1: Create the repository**

`git init` in `/Users/preluminance/Coding/PrismMods/Facet`. Write the files above, then commit as `chore: start the Facet repository`.

- [ ] **Step 2: Write the csproj**

`Facet.csproj` settings:
- `TargetFramework netstandard2.0`, `Version 0.1.0`, `LangVersion latest`, `Nullable disable`;
- `AssemblyName` = `Facet.$([System.Version]::Parse($(Version)).ToString(2))`;
- `<PackageReference Include="UnityEngine.Modules" Version="2022.3.0" IncludeAssets="compile" PrivateAssets="all" />`.

- [ ] **Step 3: Write the failing test**

```csharp
// tests/FacetTest.cs — same shape as PrismLib/tools/PrismLibTest.cs
static void Main() {
    Throttle.Reset();
    Check(Throttle.Allow("h/site", 0), "the first message for a host+site is logged");
    Check(!Throttle.Allow("h/site", 9.9), "a repeat inside 10 s is dropped");
    Check(Throttle.Allow("h/other", 9.9), "another site is independent");
    Check(Throttle.Allow("h/site", 10.1), "after 10 s it is logged again");
    Console.WriteLine(_fail == 0 ? "ALL PASS" : _fail + " FAILED");
}
```

`test.sh` does three things:
1. `mcs -out:"$TMPDIR/facettest.exe" tests/FacetTest.cs Facet/Core/*.cs`;
2. runs it with `mono`;
3. prints the last line, and exits non-zero unless that line is `ALL PASS`.

- [ ] **Step 4: Run `./test.sh`.** Expected: compile error, `Throttle` not defined.
- [ ] **Step 5: Implement `Throttle`** in `Facet/Core/Throttle.cs`: a dictionary from key to last-allowed time.
- [ ] **Step 6: Run `./test.sh` and `dotnet build Facet/Facet.csproj -c Release`.** Expected: `ALL PASS`, and `Facet/bin/Release/netstandard2.0/Facet.0.1.dll` exists. `build.sh` runs both and fails if that file name is missing.
- [ ] **Step 7: Commit** as `feat: add the offline test runner and the log throttle`.

### Task 2: Seed Facet from PrismLib.UI

**Files:**
- Copy from `PrismLib/PrismLib.UI/Toolkit/` into `Facet/Facet/`, changing the namespace to `Facet`: `Anim, ColourPicker, Cursors, GradientEditor, Nav, Popup, SearchIndex, Skin, Surface, Tabs, Tex, Theme, Tokens, Ui, Widgets, Window`.
- Copy into `Facet/Facet/Core/`: `History.cs` (with the clock made injectable), `Words.cs`.
- Copy the warm-up out of `SettingsPanel.cs` into `Facet/Facet/WarmUp.cs`, with its own rows.
- Copy `PrismLib/CLAUDE.md` § UI into `Facet/CLAUDE.md`. These are the landmines; trim the ADOFAI-only sentences.
- Modify `test.sh` to add the guard below.

**Interfaces:**
- Produces: the namespace `Facet` with every type above under the same names.
- `History.Clock` (`Func<double>`) defaults to `() => UnityEngine.Time.unscaledTime`, assigned from the Unity side in `Ui`'s static constructor. The offline test sets its own clock.

**Cutting the ties to Prism-specific code:**
- `Surface` stops calling `InputGate.HoldThisFrame` and calls `Input.HoldThisFrame()` instead. Create a minimal `Facet/Input.cs` with `HoldThisFrame()` and `HeldThisFrame`.
- `Surface` schedules `WarmUp.Run` instead of `SettingsPanel.WarmUp`.
- Delete the comment-only mentions of Prism types in `Theme`, `Ui` and `SearchIndex`.
- `Words` loses the Prism window titles from its `Shared` set. PrismLib's own Korean entries stay in PrismLib; Facet's table keeps only the words Facet's own code uses: Undo, Redo, Sure?, Run, unbound, "press a key…", "Nothing matches.", the "failed to build" message, and the gradient editor's labels.

- [ ] **Step 1: Add the failing guard to `test.sh`**

```bash
if grep -rnE 'RDInput|Assembly-CSharp|UnityModManager|MelonLoader|Harmony|scnEditor|ADOFAI' Facet/; then
    echo "game knowledge in Facet/"; exit 1; fi
```

Also add an offline check of `History` with an injected clock: two records with the same key 0.5 s apart coalesce into one undo step. That is ported from PrismLib's behaviour; `CoalesceSeconds` is 0.9.

- [ ] **Step 2: Run `./test.sh`.** Expected: FAIL (`History` not found).
- [ ] **Step 3: Copy the files and cut the ties.** Build. Fix each compile error against the 2022.3 references by reflection, the way `Window.ShowSearch` reaches the placeholder. Note each fix in a one-line comment.
- [ ] **Step 4: Run `./test.sh` and `./build.sh`.** Expected: `ALL PASS`, the guard is silent, and `Facet.0.1.dll` builds.
- [ ] **Step 5: Commit** as `feat: seed Facet from PrismLib.UI's general-purpose UI Toolkit code`. Name PrismLib commit `db70474` in the body as the source.

### Task 3: Gallery mod and the in-game test rig

**Files:**
- Create: `Facet.Mod/Facet.Mod.csproj`:
  - `netstandard2.0`, the same Unity package reference as Facet;
  - `<ProjectReference>` to Facet;
  - property `LoaderTarget` (default `UMM`);
  - a reference to `$(GamePath)/UserLibs/UnityModManager.dll`, where `GamePath` defaults to the macOS Steam path.
- Create: `Facet.Mod/Info.json`: Id `Facet`, `AssemblyName` `Facet.Mod.dll`, `EntryMethod` `Facet.Mod.Main.Load`.
- Create: `Facet.Mod/Main.cs`, `Gallery.cs`, `AutoTest.cs`; `tools/ingame.sh`.
- Modify: `build.sh` installs `Facet.Mod.dll`, `Info.json` and `Facet.0.1.dll` into `$G/UMMMods/Facet/` and removes the old `gallery.log`.

**Interfaces:**
- Produces:
  - `Gallery.Open() -> Window` (idempotent), plus `Gallery.Surface`, `Gallery.Model` and `Gallery.Close()`.
  - `Model` is a plain class: `bool Flag; float Size = 1f; int Count = 3; string Name = "Facet"; int Pick; Color Tint = Color.cyan; string Key = "F8"`.
  - `AutoTest` helpers: `Check(bool, string, string detail = "")`, `Pointer(EventType, Vector2 screen)`, `Click(Vector2)`, `Drag(Vector2, Vector2, int steps = 6)`, `Submit(Button)`, `Type(TextField, string)`, `Key(VisualElement, KeyCode)`, `Ctr(VisualElement) -> Vector2 screen`. Port these from `PrismLib/tools/LayoutProbe/AutoTest.cs:22-70`; `K()` reads the Gallery surface's root.
  - `AutoTest` runs the sections in order. Each later task appends one `IEnumerator Section…()`, then the test logs `AUTOTEST DONE pass=N fail=M` and calls `Application.Quit()`.

`Main.Load`:
- touches Facet types only inside `[MethodImpl(NoInlining)]` methods;
- logs to `<mod>/gallery.log`;
- with an `autotest` file present, starts `AutoTest`;
- otherwise F8 toggles the Gallery.

UMM registers each mod folder's DLLs by name (`PrismLib/bootstrap/PrismBootstrap.cs:134-140`). If `Facet.0.1.dll` still fails to resolve, add an `AppDomain.AssemblyResolve` handler for `Facet.*` from the mod folder in `Load`.

`tools/ingame.sh` steps:
1. `osascript -e 'quit app "ADanceOfFireAndIce"'`;
2. wait 5 s;
3. `caffeinate -dimsu -t 300 &`;
4. `touch "$G/UMMMods/Facet/autotest"`;
5. `open steam://rungameid/977950`;
6. poll `gallery.log` every 2 s until a line contains `AUTOTEST DONE`;
7. fail if the log has not changed for 90 s (stall guard) or after 300 s in total;
8. remove the `autotest` file;
9. print every `FAIL` line and the `DONE` line; exit non-zero on `fail=` other than 0.

**Run it with `run_in_background: true`** and wait for the notification.

- [ ] **Step 1: Write the first section.**

```csharp
IEnumerator SectionOpen() {
    var sw = Stopwatch.StartNew();
    var w = Gallery.Open(); yield return null; yield return null;
    Check(w.Root.layout.width > 300f, "the Gallery window lays out", "w=" + w.Root.layout.width);
    Main.Log("[perf] gallery first open " + sw.ElapsedMilliseconds + " ms");
}
```

The Gallery's first page shows one of each control as it exists after Task 2.

- [ ] **Step 2: Run `./build.sh && tools/ingame.sh`.** Expected: the `DONE` line with `fail=0`. Before `Gallery` exists, the build fails; that is this task's red.
- [ ] **Step 3: Commit** as `feat: add the Gallery mod and an in-game autotest rig`.

### Task 4: Cross-version shared state

**Files:**
- Create: `Facet/Core/Shared.cs`.
- Modify: `Surface.cs` (bands), `Input.cs` (Escape, the open/focus/pointer/blocks queries), `Ui.cs` (move the stack and Escape code into `Input`; `Ui` keeps thin forwarders).
- Test: `tests/FacetTest.cs`; `tests/shared2.sh` (below).

**Interfaces:**
- `Shared.Get<T>(string key, Func<T> create) where T : class`. Backed by one `Dictionary<string, object>` stored under `AppDomain.CurrentDomain` data key `"Facet.Shared"`. Values are BCL types only.
- `Input`:
  - `static bool HandleEscape()` and `ConsumeEscape()`;
  - `static bool AnyWindowOpen`, `TextFocused` (query form for now; Task 12 makes it event-driven) and `BlocksGameInput`;
  - `static bool PointerOverWindow(Vector2 screen)`;
  - `static void TickWheel(Vector2 screen, float delta)`;
  - `static void HoldThisFrame()` and `HeldThisFrame`.
- **Each loaded Facet registers one entry in `Shared`.** An entry carries:
  - `Func<float>`: the topmost visible escapable sorting order, or −1;
  - `Func<bool>`: take Escape;
  - `Func<bool>` delegates for open, text focused and blocks input;
  - `Func<float, float, bool>`: pointer over a window.
- Escape keeps a `[frame, result]` stamp in `Shared`.
- Each `sortingOrder` band's next value is a `float[5]` in `Shared`.
- Exact key names are the builder's call.

- [ ] **Step 1: Write the failing tests.**
  - Offline: `Shared.Get("t", () => new List<int>())` returns the same instance twice. The instance is visible through `AppDomain.CurrentDomain.GetData("Facet.Shared")`.
  - `tests/shared2.sh` compiles `Facet/Core/Shared.cs` into two libraries with different assembly names (`-out:A.dll`, `-out:B.dll`) plus a driver that loads both. It checks that a value stored through A is read back through B. `test.sh` runs it.
- [ ] **Step 2: Run `./test.sh`.** Expected: FAIL.
- [ ] **Step 3: Implement `Shared`** and move the band counters, the Escape stamp and the queries onto it.
- [ ] **Step 4: Add the in-game section.**
  - Open the Gallery.
  - Call `Input.HandleEscape()` twice in one frame. The first returns true and hides the Gallery. The second returns the same cached true and hides nothing else.
  - `Input.AnyWindowOpen` is false afterwards.
- [ ] **Step 5: Run `./test.sh && ./build.sh && tools/ingame.sh`.** Expected: `ALL PASS`, `fail=0`.
- [ ] **Step 6: Commit** as `feat: coordinate window order and Escape across loaded Facet versions`.

### Task 5: Host, Safe, Data and Events

**Files:**
- Create: `Facet/Host.cs`, `Facet/Core/Safe.cs`, `Facet/Data.cs`, `Facet/Events.cs`.
- Modify: every file that calls `RegisterCallback` (switch to `Events.On`); `Theme.cs`, `Ui.cs` and `Cursors.cs`, which use a `ConditionalWeakTable` (switch to `Data`).
- Modify: `Ui.Log` and `Window.Read`/`Window.Write` are deleted; callers use the host.
- Modify: `Surface`'s constructor becomes `Surface(Host host, string name, Layer layer, FontAsset font = null)`.

**Interfaces:**
- `Host.Create(string name, Action<HostOptions> configure = null) -> Host`, `Host.For(VisualElement) -> Host`, `Host.Use(Host) -> IDisposable`, `Host.Fallback` (named "Facet", logs through `UnityEngine.Debug.Log`).
- Instance members: `Name`, `Log(string)`, `Translate(string) -> string`, `Language -> string`, `Read(string) -> string`, `Write(string, string)`, `Theme`, `RefreshMs`.
- `HostOptions` fields:
  - `Action<string> Log`, `Func<string, string> Translate`, `Func<string> Language`;
  - `Func<string, string> Read`, `Action<string, string> Write`;
  - `Theme Theme`, `int RefreshMs = 100`;
  - `string[][] SansFonts, MonoFonts` (null keeps Facet's lists).
- `Safe` (Core):
  - `Run(string owner, string site, Action fn, Action<string> log) -> bool`;
  - `Get<T>(string owner, string site, Func<T> fn, T fallback, Action<string> log) -> T`.
  - A throw logs `"[" + owner + "] " + site + ": " + message + " @ " + firstStackLine` through `Throttle` and returns false or the fallback.
  - Unity-side overloads take a `Host`.
- `Data.Get<T>(VisualElement) where T : class, new()`, `Data.Find<T>(VisualElement)`, `Data.Clear<T>(VisualElement)`.
- `Events.On<T>(VisualElement, EventCallback<T>, TrickleDown = NoTrickleDown) where T : EventBase<T>, new()` and `Events.Off<T>(…)`.

- [ ] **Step 1: Write the failing offline tests.**
  - `Safe.Run` with a throwing action returns false and logs once.
  - A second throw from the same site within 10 s logs nothing.
  - `Safe.Get` returns the fallback.
  - Grep guard in `test.sh`: `grep -rn "RegisterCallback\|ConditionalWeakTable" Facet/ | grep -v "Events.cs\|Data.cs"` must print nothing.
  - Grep guard in `test.sh`: `grep -rnE "class \w+ *: *(VisualElement|Button|Label|TextField|ScrollView|MonoBehaviour)" Facet/` must print nothing. That is IL2CPP rule 1; `Facet.Mod/` is exempt.
- [ ] **Step 2: Run `./test.sh`.** Expected: FAIL.
- [ ] **Step 3: Implement**, routing every consumer delegate in the seeded code through `Safe`:
  - page builds, `Revision`, escape handlers, setters, getters, tooltips and translators;
  - geometry persistence through `host.Read`/`host.Write`.
- [ ] **Step 4: Add the in-game section.**
  - Create two hosts, `A` and `B`, each logging into its own list.
  - A Nav page whose build throws, under host `A`: the content shows the "failed to build" text; `A`'s list has one entry naming the page; `B`'s list stays empty.
  - Move the Gallery window by dragging its title bar: `host.Write("window.Gallery", …)` received an `x,y,w,h,scale` string.
- [ ] **Step 5: Run `./test.sh && ./build.sh && tools/ingame.sh`.** Expected: `ALL PASS`, `fail=0`.
- [ ] **Step 6: Commit** as `feat: give each consumer a Host and guard every call into its code`.

### Task 6: Theme roles, Paint and Looks for the primitives and the window chrome

**Files:**
- Create: `Facet/Paint.cs`, `Facet/Core/Palette.cs` (the accent derivation over `float r, g, b`).
- Modify: `Theme.cs`, `Ui.cs`, `Window.cs`, `Surface.cs`.
- Test: `tests/FacetTest.cs`.

**Interfaces:**
- `enum Role { Panel, PanelBorder, TitleBar, Row, RowAlt, RowHover, Text, TextMuted, Accent, Danger, AccentText, Focus }`. Append-only.
- `Theme`:
  - `Color this[Role]` with get and set;
  - `static Theme Dark` (today's `Tokens` colours), and `new Theme()` as a copy of `Dark`;
  - `static Theme FromAccent(Color, float tint = 1f)` and `void Recolour(Color, float tint = 1f)`;
  - `event Action Changed`, which `Recolour` raises once;
  - `static Theme For(VisualElement)`, `static void Set(VisualElement, Theme)`, `static IDisposable Use(Theme)`.
  - `AccentText` = `(0.06, 0.06, 0.08, 1)`. `Focus` = `Accent`.
- `Paint`:
  - `Bg(VisualElement, Role)`, `Fg(VisualElement, Role)`, `Border(VisualElement, Role)`;
  - `Look(VisualElement, Look)`, `Select(VisualElement, bool)`, `Enable(VisualElement, bool)`;
  - `Apply(VisualElement root)`, which re-applies every recorded role and look under `root` with `Theme.For`.
- `Look` is a class with nullable `Role?` fields `Bg, Text, Border, HoverBg, PressedBg, SelectedBg, SelectedText, FocusBorder`, and `float DisabledOpacity = 0.35f`.
- Static instances: `Look.Button, FlatButton, AccentButton, DangerButton, Row, RailItem, Field, Card, Chrome`.
- `Paint` tracks hover, pressed and focus itself, registering through `Events.On` once per element.
- When a theme's `Changed` fires, each surface wearing that theme calls `Paint.Apply(Root)` inside `Safe`, so one surface's failure leaves the others re-themed. It is timed into a `[perf] retheme` log line.

**Derivation.** `Palette.FromAccent` is the formula in `PrismLib/PrismLib.UI/Toolkit/Theme.cs:63-82`, unchanged:
- `Lean(g, a, t) = lerp(grey g, accent, t)`;
- Panel = `Lean(.037, .059·tint)` with alpha .98;
- TitleBar = `Lean(.061, .102·tint)`;
- control = `Lean(.173, .146·tint)`; RowAlt = control; RowHover = `lerp(control, white, .08)`;
- Row = `lerp(TitleBar, control, .5)`;
- PanelBorder = white with alpha .09;
- Text = `Lean(.92, .04·tint)`; TextMuted = `Lean(.745, .17·tint)`;
- Danger = `#ff7a45` when the accent's saturation > .25 and its hue < .06 or > .94; otherwise `(.886, .404, .427)`.

- [ ] **Step 1: Write the failing offline tests.**
  - `Palette.FromAccent(1, 0, 0, 1)`: Danger = (1, .478, .271).
  - Blue `(.2, .4, 1)`: Danger = (.886, .404, .427).
  - Panel = lerp((.037, .037, .037), accent, .059), within 1e-4.
  - `tint 0` gives a pure grey Panel.
- [ ] **Step 2: Run `./test.sh`.** Expected: FAIL.
- [ ] **Step 3: Implement.** Convert `Ui.Card/Text/Muted/Btn/Flat/Highlight` and `Window`'s chrome to roles and looks:
  - `Ui.Text` takes `Role role = Role.Text` in place of `Color?`;
  - `Highlight` becomes `Paint.Select`;
  - `Window.Enable`'s fade becomes `Paint.Enable`;
  - every `MouseEnterEvent`/`MouseLeaveEvent` colour lambda in these files is deleted.
- [ ] **Step 4: Add the in-game section.**
  - Open the Gallery and record `w.Body`'s background.
  - `Gallery.Surface.Theme.Recolour(new Color(.9f, .2f, .2f))`, then wait one frame.
  - The title bar's resolved background equals the theme's TitleBar. The close button hovered (`MouseEnterEvent` sent) shows Danger.
  - A button after `Paint.Enable(b, false)` has opacity .35.
  - The `[perf] retheme` line is ≤ 16 ms.
- [ ] **Step 5: Run `./test.sh && ./build.sh && tools/ingame.sh`.** Expected: `ALL PASS`, `fail=0`.
- [ ] **Step 6: Commit** as `feat: paint by theme role and re-theme open windows live`.

### Task 7: Every remaining widget paints by role

**Files:**
- Modify: `Nav.cs`, `Widgets.cs`, `Popup.cs`, `Tabs.cs`, `ColourPicker.cs`, `GradientEditor.cs`, `Skin.cs`.

**Interfaces:**
- Consumes `Paint` and `Look` from Task 6. Produces no new API.

- [ ] **Step 1: Write the failing check.**
  - Add `test.sh` guard: `grep -rnE "style\.(backgroundColor|color|border[A-Za-z]*Color) *=" Facet/ | grep -v "Paint.cs\|ColourPicker.cs\|GradientEditor.cs"` prints nothing. The pickers keep literal colours for their swatches only.
  - Add in-game section `SectionNoStale`. Open every Gallery page once, recolour to red, then walk the whole tree. For every element with an inline background or text colour, the colour must be one of the new theme's 12 role values, or a picker swatch. Anything else fails, naming the element and its colour.
- [ ] **Step 2: Run both.** Expected: FAIL. The grep lists today's direct colour writes.
- [ ] **Step 3: Convert the files.** Rail rows use `Look.RailItem` and `Paint.Select` for the selected leaf; segmented choices use `Paint.Select`; the search-hit flash animates from `Role.Accent`.
- [ ] **Step 4: Run `./test.sh && ./build.sh && tools/ingame.sh`.** Expected: `ALL PASS`, `fail=0`.
- [ ] **Step 5: Commit** as `refactor: paint every widget by theme role`.

### Task 8: Host-scoped text, live relabel, and fonts

**Files:**
- Create: `Facet/Fonts.cs`, taking `Surface.cs:147-398` (file lists, fallback, mono, bold attach, `HandDownFonts`).
- Modify: `Core/Words.cs`, `Ui.cs`, `Window.cs`, `Surface.cs`.

**Interfaces:**
- `Words`:
  - `Words.Label(TextElement, string english, bool own = false)` stores the key in `Data` and sets the translated text;
  - `Words.Own(string english, Host host)` returns Facet's own table entry, or `english`;
  - `Words.T(string english, Host host)`: the host's translator, else English. Translators only ever see their own host's strings.
- `Surface.Relabel()` re-translates every keyed text element under `Root` with `Host.For(element)`.
- `Fonts.Regular(Host) -> FontAsset`, `Fonts.Mono(Host) -> FontAsset`, `Fonts.Apply(VisualElement root, FontAsset)`. Candidates come from `HostOptions.SansFonts`/`MonoFonts` when set, else the copied lists. 64 px sampling and 6 px padding stay.

- [ ] **Step 1: Write the failing offline tests.**
  - A translator for host `A` (`"Undo" -> "실행 취소"`) is not consulted by `Words.T("Undo", B)`.
  - `Words.Own("Undo", hostWithKorean)` returns Facet's own entry, whatever `A` maps "Undo" to.
- [ ] **Step 2: Run `./test.sh`.** Expected: FAIL.
- [ ] **Step 3: Implement.** Every `Ui.Text`, `Ui.Btn`, row label, tooltip and window title stores its key.
- [ ] **Step 4: Add the in-game section.**
  - Gallery host language `"en"`, then switch it to `"ko"` (the Gallery host translates "Size" to "크기"), then `Gallery.Surface.Relabel()`.
  - The Size row's label text is `크기` with no rebuild: the same `Label` instance as before.
  - Its `layout.width` is > 0, so the Korean glyphs rendered.
- [ ] **Step 5: Run `./test.sh && ./build.sh && tools/ingame.sh`.** Expected: `ALL PASS`, `fail=0`.
- [ ] **Step 6: Commit** as `feat: translate text per host and relabel open windows in place`.

### Task 9: Bind, Control and in-place refresh

**Files:**
- Create: `Facet/Bind.cs`, `Facet/Control.cs`, `Facet/Options.cs`, `Facet/Refresher.cs`.
- Modify: `Surface.cs` (owns a `Refresher`), `Core/History.cs` (after apply, refresh every visible surface).

**Interfaces:**
- `Bind.To<T>(Func<T> get, Action<T> set) -> Bind<T>` and `Bind.Value<T>(T initial, Action<T> onChange = null) -> Bind<T>`. `Bind<T>` has `T Get()` and `void Set(T)`.
- `Control<T>`:
  - `VisualElement Root`, `VisualElement Input`, `T Value`;
  - `void Set(T v, bool notify = true)`, `void Refresh()`;
  - `event Action<T> Changed, Committed`;
  - `bool Unavailable`.
- `ControlOptions`:
  - `string Id` (sets `Root.name = Id` and `Input.name = Id + "-input"`);
  - `string Tooltip`, `Func<string> TooltipText`;
  - `Func<bool> EnabledWhen, VisibleWhen`;
  - `List<KeyValuePair<string, Action>> ContextMenu`.
- `Options<T> : ControlOptions` adds `bool HasDefault` and `T Default`; the `Default` setter sets `HasDefault`.
- `Refresher` runs per surface on `Root.schedule.Execute(...).Every(host.RefreshMs)` and does nothing while the surface is hidden. Each surface's tick runs inside `Safe`, so one surface's failure leaves the others ticking. Controls register on `AttachToPanelEvent` and unregister on `DetachFromPanelEvent`.
- **`ContextMenu`:** a right-click on the row opens `Popup.Menu` with the item names; picking one runs its action through `Safe`.
- **On a tick, for each control:**
  - **skip it while it is being edited**, meaning its input holds focus or it holds pointer capture;
  - read through `Safe`;
  - when the read differs from the shown value (`EqualityComparer<T>.Default`), update the display without notifying;
  - after **3 failed reads in a row**, mark it unavailable (dim it; the tooltip says "unavailable, see the log") and stop polling it.
  - `EnabledWhen`/`VisibleWhen` are evaluated on the same tick.
- **Changed dot:** while `Value` differs from `Default`, a 6 px `Role.Accent` dot shows before the label. A middle-click on the row calls `Set(Default)`.

- [ ] **Step 1: Write the in-game section**, using a temporary Toggle built on `Control<bool>` in the Gallery.
  - `Model.Flag = true` behind the UI shows as on within 150 ms (poll 10 frames).
  - With `Gallery.Model` replaced by a new instance whose `Flag = false`, the toggle shows off within 150 ms (Review Focus 1). The bind reads `() => Gallery.Model.Flag`.
  - A middle-click on the row with `Default = false` sets `Model.Flag` false and hides the dot.
  - A getter that throws makes `Unavailable` true after 3 ticks. `gallery.log` holds exactly one "[Gallery] …" line for it.
  - While the Name field holds focus, a behind-the-UI write to `Model.Name` does not overwrite the typed text.
  - A button whose click calls `nav.Rebuild()` on its own page raises nothing (Review Focus 2). Afterwards the old page's controls are unregistered: the refresher count equals the new page's control count.
  - With the Gallery hidden for 30 frames, the refresher reads nothing (count of getter calls = 0).
  - A right-click on a row with `ContextMenu = { ("Reset all", act) }` opens a menu, and picking the item runs `act` once.
- [ ] **Step 2: Run `./build.sh && tools/ingame.sh`.** Expected: FAIL. `Control` is not defined, so the build fails.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run `./build.sh && tools/ingame.sh`.** Expected: `fail=0`.
- [ ] **Step 5: Commit** as `feat: bind controls to live values and refresh them in place`.

### Task 10: Value controls

**Files:**
- Create: `Facet/Core/ValueScale.cs`, `Facet/Controls.Values.cs` (partial `Controls`).
- Delete: from `Widgets.cs`, the value rows it replaces. `Widgets` is gone after Task 11.

**Interfaces:**
- `ValueScale`:
  - `interface IValueScale { float ToT(float v, float min, float max); float FromT(float t, float min, float max); }`;
  - `ValueScale.Linear` and `ValueScale.Log` (log requires min > 0);
  - `static float Snap(float v, float step, float min)` (step ≤ 0 means no snapping).
- Option classes:
  - `SliderOptions : Options<float>` { `Min = 0, Max = 1, Step = 0`, `IValueScale Scale = ValueScale.Linear`, `Format = "0.##"` };
  - `IntSliderOptions : Options<int>` { `Min = 0, Max = 10` };
  - `ChoiceOptions : Options<int>` { `IList<string> Items` };
  - `DropdownOptions : Options<int>` { `IList<string> Items`, `Func<int, Font> ItemFont` };
  - `TextOptions : Options<string>` { `Width = 200` };
  - `ColourOptions : Options<Color>` { `Alpha` };
  - `KeyOptions : Options<string>` { `Action<Action<string>> Capture` }.
- `Controls` methods. Every one builds a labelled row and records undo through `History` with key `host.Name + "/" + page + "/" + label + "#" + id`:
  - `Toggle(VisualElement parent, string label, Bind<bool>, Action<Options<bool>> configure = null) -> Control<bool>`;
  - `Slider(…, Bind<float>, Action<SliderOptions>) -> Control<float>`;
  - `IntSlider(…, Bind<int>, Action<IntSliderOptions>) -> Control<int>`;
  - `Number(…, Bind<float>, Action<SliderOptions>) -> Control<float>`;
  - `Choice(…, Bind<int>, Action<ChoiceOptions>) -> Control<int>`;
  - `Dropdown(…, Bind<int>, Action<DropdownOptions>) -> Control<int>`, a button that opens `Popup.Menu`;
  - `Text(…, Bind<string>, Action<TextOptions>) -> Control<string>`, committed on Enter or blur;
  - `Colour(…, Bind<Color>, Action<ColourOptions>) -> Control<Color>`;
  - `Key(…, Bind<string>, Action<KeyOptions>) -> Control<string>`;
  - `Info(VisualElement parent, string label, Func<string> value, Action<ControlOptions> configure = null) -> Control<string>`;
  - `Action(parent, label, string button, Action onClick, Action<ControlOptions>) -> Button`;
  - `Danger(parent, label, string button, Action onConfirm, Action<ControlOptions>) -> Button`;
  - `DangerButton(parent, string text, Action onConfirm, Action<ControlOptions>) -> Button`.
- The Gallery's first page is rebuilt on these, each bound to a `Model` field with `Id` set (`"flag"`, `"size"`, …).

- [ ] **Step 1: Write the failing offline tests.**
  - `Log.FromT(Log.ToT(10, 1, 100), 1, 100) == 10` within 1e-3.
  - `Linear.ToT(5, 0, 10) == .5`.
  - `Snap(0.37, 0.25, 0) == 0.25`.
  - `Snap(0.38, 0.25, 0) == 0.5`.
  - `Snap(3, 0, 0) == 3`.
- [ ] **Step 2: Write the in-game section.**
  - Clicking `flag-input` flips `Model.Flag`, and `History.Undo()` puts it back.
  - Dragging `size-input` from its left to its right end sets `Model.Size` to `Max`.
  - Typing `"1.5"` + Enter into the size number sets 1.5. Typing `"abc"` + Enter restores the previous text and leaves the value alone.
  - With `Step = 0.25` the dragged value is a multiple of 0.25.
  - Choosing item 2 in the Dropdown menu sets `Model.Pick = 2`.
  - Escape on the open menu closes only the menu.
  - Review Focus 5: a Toggle labelled with 200 characters, and one labelled "아주 긴 한국어 라벨 …" (80 Hangul characters). Each row's `layout.height` is ≤ 34, and the switch's `worldBound.xMax` is ≤ the window body's `worldBound.xMax`.
- [ ] **Step 3: Run `./test.sh && ./build.sh && tools/ingame.sh`.** Expected: FAIL.
- [ ] **Step 4: Implement.** Port each row from `Widgets` onto `Control<T>`, keeping their recorded "why" comments.
- [ ] **Step 5: Run again.** Expected: `ALL PASS`, `fail=0`.
- [ ] **Step 6: Commit** as `feat: add bound value controls with options, snapping and log scales`.

### Task 11: Structure controls

**Files:**
- Create: `Facet/Controls.Structure.cs`.
- Delete: `Facet/Widgets.cs`.

**Interfaces:**
- `Controls.Section(VisualElement parent, string title, bool defaultOpen = true) -> VisualElement body`. Fold state is stored through `host.Write("fold." + pageTitle + "/" + title, "0"|"1")`.
- `Controls.Header(parent, string text) -> Label`, `Controls.Spacer(parent, float height = 8) -> VisualElement`.
- `Controls.ToggleGroup(parent, string label, Bind<bool>, Action<Options<bool>>) -> VisualElement body`. The body shows only while the value is on, and follows refreshes.
- `Controls.SubPage(parent, string label, Action<VisualElement> build, Action<ControlOptions>) -> VisualElement`.
- `Controls.ToggleSubPage(parent, string label, Bind<bool>, Action<VisualElement> build, Action<Options<bool>>) -> Control<bool>`.
- `Controls.Item(parent, string title, string note = null) -> VisualElement actions`, `Controls.MarkActive(VisualElement actions, string label = null)`.

- [ ] **Step 1: Write the in-game section.**
  - Fold a section: `host.Write` received `fold.…=0`. A rebuilt page shows it folded.
  - A ToggleGroup's body hides within 150 ms after `Model.Flag = false` behind the UI.
  - Clicking a SubPage row pushes a page whose back row names the parent page.
  - Clicking the switch inside a ToggleSubPage toggles it without navigating.
- [ ] **Step 2: Run.** Expected: FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run `./build.sh && tools/ingame.sh`.** Expected: `fail=0`.
- [ ] **Step 5: Commit** as `feat: add sections, groups and subpages on bound controls`.

### Task 12: Input — focus, key capture, WantsInput

**Files:**
- Create: `Facet/Core/KeyCapture.cs`.
- Modify: `Input.cs`, `Controls.Values.cs` (Key uses the coordinator).

**Interfaces:**
- `KeyCapture` (Core, stored in `Shared` so it holds across versions):
  - `static void Claim(string owner, Action cancel)`: cancels the previous owner through `Safe`;
  - `static void Release(string owner)`;
  - `static string Owner`.
- `Input.TextFocused` becomes a count kept by `FocusInEvent`/`FocusOutEvent` (trickle-down on each surface root) on text inputs, and stays a union across versions.
- `Input.WantsInput(Vector2 pointerScreen) -> bool` = `BlocksGameInput || PointerOverWindow(p) || TextFocused`.

- [ ] **Step 1: Write the failing offline tests.**
  - `Claim("a", cancelA); Claim("b", cancelB)` calls `cancelA` once, and `Owner == "b"`.
  - `Release("a")` while `b` owns it changes nothing.
  - `Release("b")` leaves `Owner` null.
- [ ] **Step 2: Write the in-game section** (Review Focus 3):
  - Open a `Popup` over the Gallery holding a focused `TextField`. `Input.TextFocused` is true.
  - First `HandleEscape`: the field blurs, the popup and window are still open, and `TextFocused` is false.
  - Second `HandleEscape`, on a later frame: the popup closes.
  - Third: the window hides.
  - Clicking the Key control and then the second Key control cancels the first: its button text returns to the bound value.
  - With `flag-input` focused, a `NavigationMoveEvent` (Next) moves focus to the next control in build order. That control's border shows `Role.Focus`. Controls are `focusable = true` and draw the ring through `Look.FocusBorder`.
- [ ] **Step 3: Run.** Expected: FAIL.
- [ ] **Step 4: Implement.**
- [ ] **Step 5: Run `./test.sh && ./build.sh && tools/ingame.sh`.** Expected: `ALL PASS`, `fail=0`.
- [ ] **Step 6: Commit** as `feat: track text focus by events and give key capture one owner`.

### Task 13: Matcher with Hangul initials

**Files:**
- Create: `Facet/Core/Hangul.cs`, `Facet/Core/Search.cs` (port of `PrismLib/PrismLib/Search.cs`, namespace `Facet`).

**Interfaces:**
- `Hangul.Initials(string) -> string`.
- `Search` keeps PrismLib's API: `NoMatch`, `Text`, `Query`, `Normalize`, `Rate(Query, Text[] label, Text[] context, Text detail, bool typos)`, `Score`, `ScoreAny`, `Rank<T>`, `Filter`.
- Added: a `Text` also holds its initials form, and a query token made only of compatibility jamo (U+3131–U+314E) is matched against it.

**Initials algorithm:**
- For each `c` in U+AC00–U+D7A3, emit `"ㄱㄲㄴㄷㄸㄹㅁㅂㅃㅅㅆㅇㅈㅉㅊㅋㅌㅍㅎ"[(c - 0xAC00) / 588]`.
- Every other character passes through unchanged.

- [ ] **Step 1: Write the failing offline tests.**
  - Port every `Search` check from `PrismLib/tools/PrismLibTest.cs` as it stands, renamed to `Facet.Search`.
  - Add:
    - `Hangul.Initials("노트") == "ㄴㅌ"`;
    - `Hangul.Initials("A가") == "Aㄱ"`;
    - `Search.Score("노트 크기", "ㄴㅌ") != NoMatch`;
    - `Search.Score("노트", "ㄱㄴ") == NoMatch`;
    - `Rank` over `{"Show FPS", "Sound offset"}` for `"fps"` puts "Show FPS" first;
    - `"sohw"` finds "Show FPS" only through the typo pass, and only when no exact hit exists.
- [ ] **Step 2: Run `./test.sh`.** Expected: FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run `./test.sh`.** Expected: `ALL PASS`.
- [ ] **Step 5: Commit** as `feat: port the ranked matcher and add Korean initial-consonant search`.

### Task 14: Nav — owners, lazy search index, reveal

**Files:**
- Modify: `Nav.cs`, `SearchIndex.cs` (one index per `Nav` instance, not static; `Find` uses `Search.Rate`).

**Interfaces:**
- `NavItem` adds `string Owner` and `Func<bool> VisibleWhen`.
- `Nav` adds:
  - `RemoveOwner(string owner)`;
  - `Rebuild()`: rebuilds the open page and keeps its scroll offset;
  - `SearchIndex Index`, built on the first non-empty `Filter`;
  - a `[perf] search index N pages M ms` log line;
  - per-language caching, dropped by `RemoveOwner` or a `Revision` change.
- Each branch remembers the last leaf opened in it, and reopens it when the branch is clicked.
- `SearchIndex.Hit` adds `string Section` and keeps the English key, so a hit matches in both languages.
- A hit:
  1. opens the page;
  2. unfolds every `Section` on the way to the row;
  3. calls `ScrollTo(row)` on each enclosing `ScrollView`;
  4. flashes the row.
- **No page builds at construction**: `IndexPages` is deleted from the constructor.

- [ ] **Step 1: Write the in-game section.**
  - Build a Nav with 3 pages. A counter in each build proves only the first page built at construction.
  - `nav.Filter("tint")` finds the Tint row on page 3. After the click: page 3 is open; the Tint row's section, folded beforehand, is unfolded; the row's `worldBound` lies inside the content viewport.
  - `Filter("ㅌ")` with the Gallery host in Korean finds "틴트".
  - `RemoveOwner("B")` drops B's pages from the rail.
  - Open the second leaf of a branch, go elsewhere, then click the branch: the second leaf reopens.
  - The `[perf] search index` line is ≤ 100 ms.
- [ ] **Step 2: Run.** Expected: FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run `./build.sh && tools/ingame.sh`.** Expected: `fail=0`.
- [ ] **Step 5: Commit** as `feat: index pages on the first search and reveal hits in folded sections`.

### Task 15: Form and Field

**Files:**
- Create: `Facet/Form.cs`.

**Interfaces:**
- `enum FieldKind { Bool, Int, Float, Choice, Text, Action, Colour, Key, Info, Dropdown, Number }`. The first nine are in `PrismLib.UI.SettingControl`'s order; append-only.
- `sealed class Field` with:
  - `string Owner, Page, Group, Label, Tooltip`;
  - `FieldKind Kind`;
  - `Func<object> Get`, `Action<object> Set`;
  - `float Min, Max, Step`;
  - `string[] Options`;
  - `string ActionLabel`;
  - `object Default`;
  - `Action<Action<string>> Capture`.
- `Form.Build(VisualElement parent, IEnumerable<Field> fields)`:
  - groups consecutive fields by `Group` into `Controls.Section`s;
  - builds each field under `Host.Named(field.Owner) ?? Host.For(parent)`;
  - converts values with `Convert`, and accepts `Color` or `"#rrggbb"` for `Colour`, as `SettingsPanel.ToColour` does;
  - turns any build failure into a muted "`<label>` — unavailable" row.
- `Host.Named(string) -> Host`: the hosts registry kept by `Host.Create`.

- [ ] **Step 1: Write the in-game section.**
  - One field of each kind builds the matching control, found by `Id = Label`.
  - A `Float` field whose `Get` returns `null`, and an `Int` field whose `Get` returns `"x"`, each produce an unavailable row. `Form.Build` returns normally and the rows after them still build (Review Focus 4).
  - A field owned by host `A` translates its label with `A`'s table, not the Gallery's.
- [ ] **Step 2: Run.** Expected: FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run `./build.sh && tools/ingame.sh`.** Expected: `fail=0`.
- [ ] **Step 5: Commit** as `feat: build forms from field descriptors`.

### Task 16: CloseRequested, warm-up, and the performance budgets

**Files:**
- Modify: `Window.cs`, `WarmUp.cs`, `Facet.Mod/AutoTest.cs`.

**Interfaces:**
- `Window.CloseRequested` is an `event Action<Window>`. With no subscriber, close hides the surface. With subscribers, only they decide.
- `WarmUp.Run()`, scheduled 500 ms after the first surface: an off-screen window with one control of each kind for 100 ms, under `Host.Fallback`, writing no geometry. `static bool WarmUp.Done` is set when it finishes.
- `Anim.Speed` (`float`, default 1): every `Anim` duration is multiplied by it, and `0` applies the end state in the same call. The spec's animation-speed multiplier.

- [ ] **Step 1: Write the in-game section.**
  - With `Anim.Speed = 0`, `Anim.Move(e, 40)` leaves `e.style.left == 40` before the next frame. Restore it to 1 afterwards.
  - A `CloseRequested` subscriber that calls `surface.Dispose()` raises nothing, and the refresher stops (Review Focus 2).
  - After the warm-up, opening a second fresh window of the Gallery's size logs `[perf] first open N ms` with N ≤ 50.
  - 100 bound Toggles on one page: the mean of 20 refresher ticks is ≤ 0.2 ms, logged as `[perf] refresh`.
  - `[perf] retheme` ≤ 16 ms on that page.
- [ ] **Step 2: Run.** Expected: FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run `./build.sh && tools/ingame.sh`.** Expected: `fail=0`, and every budget line passes.
- [ ] **Step 5: Commit** as `feat: let owners handle close and check the performance budgets in the autotest`.

### Task 17: MelonLoader entry, packaging and docs

**Files:**
- Modify: `Facet.Mod/Facet.Mod.csproj`:
  - `LoaderTarget=ML` defines `ML`;
  - references `$(GamePath)/MelonLoader/net35/MelonLoader.dll`;
  - outputs `Facet.Mod.dll`.
- Modify: `Main.cs` adds a `MelonMod` under `#if ML` with `[assembly: MelonInfo]`.
- Create: `release.sh`. It builds both targets and stages `dist/Facet-ML.zip` (`UserLibs/Facet.0.1.dll`, `Mods/Facet.Mod.dll`) and `dist/Facet-UMM.zip` (`Facet/Facet.Mod.dll`, `Facet/Facet.0.1.dll`, `Facet/Info.json`). It publishes nothing.
- Modify: `README.md` (install, a minimal `Host.Create` + `Controls.Toggle` example, the coexistence rules) and `CLAUDE.md` (the dev loop, the rig, the packaging rule).

- [ ] **Step 1: Write the check.**
  - `release.sh` lists each zip's entries and fails unless they match exactly the names above.
  - In-game: install the ML zip contents, remove the UMM copy, run the autotest through the ML entry (`tools/ingame.sh --ml` watches `UserData/Facet/gallery.log`). Expected: `fail=0`.
- [ ] **Step 2: Run `./release.sh`, then `tools/ingame.sh --ml`.** Expected: both zips listed, `fail=0`.
- [ ] **Step 3: Restore the UMM install** with `./build.sh`, so later work uses the default rig.
- [ ] **Step 4: Commit** as `feat: add a MelonLoader entry and package the standalone mod`.
