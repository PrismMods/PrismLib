# CamDisplay v1 — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A standalone ADOFAI mod that shows a webcam hand cam in-game with these features:
- freely placed, sized, rotated and cropped through the shared PrismLib layout editor;
- horizontal and vertical flips, on by default;
- per-context visibility;
- a macOS "Enable camera" flow;
- MelonLoader and UMM builds.

**Architecture:**
- **Pure core (`Core/`):** settings, context rules, geometry/UV and macOS patch decisions, in BCL-only files compiled straight into an offline `mono` test.
- **Unity side:** `CamFeed` wraps `WebCamTexture`, `CamView` is a uGUI `RawImage`, and `SceneWatch` reads game state through reflection that is safe on 2.9 and 3.x.
- **Single PrismLib file:** every PrismLib type stays in `PrismBridge.cs`, per PrismLib's landmines.
- **Entry points:** two thin loader files, picked at build time.

**Tech Stack:** C# .NET 4.8 (xbuild, old-style csproj like Topaz), Unity uGUI + PrismLib.UI (UI Toolkit), MelonLoader 0.7.x (`net35/MelonLoader.dll`), UnityModManager 0.32, Harmony `AccessTools` for reflection.

**Spec:** `PrismLib/docs/superpowers/specs/2026-11-06-camdisplay-design.md`, "Part 2 — CamDisplay" and "macOS camera access". Part 3 (advanced camera settings) is **out of scope** here and gets its own plan after v1 ships.

**Prerequisites:**
- Plan 1 passed. The rig stays patched, with the launcher and launch options in place.
- Plan 2 is built; its release has to be **published** before Task 7's release step.

## Global Constraints

- **Repo:** `/Users/preluminance/Coding/PrismMods/CamDisplay` (created in Plan 1). GPL-3.0.
- **Supported games:** ADOFAI 2.9 (Unity 2022) and 3.x (Unity 6). Never bind directly to a game member that differs between the two. Use `AccessTools` lookups that are cached and null-tolerant; `Topaz/Topaz/Util/GameCompat.cs` is the reference.
- **Defaults:**

  | Setting | Default |
  |---|---|
  | Enabled | true |
  | Camera | "" (first device) |
  | Resolution / frame rate | 1280×720 @ 30 |
  | FlipH / FlipV | true / true |
  | ShowInMenus / ShowInLevelSelect / ShowInEditor | false |
  | Anchor | 8 (bottom-right), Closest on |
  | Offset | (−260, 160) reference px |
  | BaseHeight | 270 reference px |
  | Scale | 1 |
  | Rotation | 0 |
  | Crop | 0,0,0,0 |

- **Reference pixels:** 1080p. Screen px = reference px × `screenH / 1080`.
- **Coordinates, anchor indexes and crop order** are identical to Plan 2's Global Constraints: bottom-left origin; anchors 0–8 from the top-left in reading order; crop `{left, bottom, right, top}`.
- **No per-frame CPU pixel work.** Never call `GetPixel(s)`/`SetPixels`/`Apply` on the feed. Crop and flip go through `RawImage.uvRect` only.
- **Camera lifecycle:** `Play()` stalls the main thread for 0.7–1.3 s, so the camera runs whenever Enabled and access is granted. It is not stopped when hidden; it restarts only on Enabled, camera or resolution changes.
- **Draw order:** a `ScreenSpaceOverlay` canvas at `sortingOrder = 90`. That's below Topaz's key viewer (100), overlay (999), editor (31000) and menu (32000).
- **PrismLib landmines** (`PrismLib/CLAUDE.md`):
  - no PrismLib type in any field, and none outside `PrismBridge.cs`;
  - every PrismLib.dll call goes behind `PrismBridge.Available`;
  - ship `PrismLib.bundle`, never `PrismLib.dll`;
  - copy `lib/PrismLib.UI.dll` into both zips;
  - `release.sh` runs `lib/update-prismlib.sh` first;
  - `PrismLib.Bootstrap.PrismBootstrap.SyncUi()` runs before anything names a PrismLib.UI type.
- **UMM build:** `AssemblyName` is `CamDisplayUmm` and the constant `CAMDISPLAY_UMM` is defined. **ML build:** `AssemblyName` is `CamDisplay`, with `[assembly: MelonGame("7th Beat Games", "A Dance of Fire and Ice")]`.
- **Commits** follow `CamDisplay/agents/commits.md`: split finely, **no `Co-Authored-By`**. Never push unless asked. **The user decides the version.**

## Review Focus

- **The saved camera is unplugged or renamed:** fall back to the first device and say so in the status. Never loop on `Play()`. *Test: Task 2, `PickDevice` cases.*
- **No camera at all, or access denied:** the cam stays hidden, the status shows the reason, and nothing throws per frame. *Checked in Task 3, in-game, with Enabled on and the camera unplugged.*
- **The camera reports a different size than requested** (it starts at 16×16 until the first frame): geometry uses the live `texture.width/height` and must not divide by zero. *Test: Task 2, `Box` with a 0×0 source falls back to 16:9.*
- **Settings file missing, corrupt or from an older version:** load the defaults, keep a `.bak` of the bad file, and never crash the mod. *Test: Task 2, `CamSettings.FromJson` cases.*
- **The game updates on macOS and Steam restores the stock signature:** the status flips to "Camera blocked by macOS", and the Enable action is offered again. *Test: Task 2, `NeedsPatch` cases. Checked in Task 6 with the `undo` script.*

---

### Task 1: Dual-loader build skeleton

**Files:**
- Create: `CamDisplay/CamDisplay/CamDisplay.csproj`. Old-style, `v4.8`, like `Topaz/Topaz/Topaz.csproj`, with `GameRoot`/`AdofaiManaged` properties pointing at the rig.
  - `LoaderTarget` property, default `ML`. When it's `UMM`: define `CAMDISPLAY_UMM`, set `AssemblyName=CamDisplayUmm`, and use `obj/umm`/`bin/umm`.
  - References: `MelonLoader/net35/MelonLoader.dll` for ML builds only, `UserLibs/UnityModManager.dll` for UMM builds only, `0Harmony`, `UnityEngine.CoreModule`, `UnityEngine.AudioModule`, `UnityEngine.UIModule`, `UnityEngine.UI`, `UnityEngine.UIElementsModule`, `UnityEngine.TextCoreTextEngineModule`, `UnityEngine.InputLegacyModule`, `UnityEngine.IMGUIModule`, `Assembly-CSharp`, `../lib/PrismLib.dll`, `../lib/PrismLib.UI.dll`.
- Create: `CamDisplay/CamDisplay/Properties/AssemblyInfo.cs`
- Create: `CamDisplay/CamDisplay/Main.cs`, `CamDisplay/CamDisplay/LoaderMl.cs` (`#if !CAMDISPLAY_UMM`), `CamDisplay/CamDisplay/LoaderUmm.cs` (`#if CAMDISPLAY_UMM`), `CamDisplay/CamDisplay/Util/Log.cs`
- Create: `CamDisplay/CamDisplay/Util/PrismBootstrap.cs` (copy of `PrismLib/bootstrap/PrismBootstrap.cs`, as Topaz does)
- Create: `CamDisplay/lib/update-prismlib.sh` (copy of Topaz's), then run it to fetch `lib/PrismLib.dll` and `lib/PrismLib.UI.dll`
- Create: `CamDisplay/Info.json` (`"Id": "CamDisplay"`, `"AssemblyName": "CamDisplayUmm.dll"`, `"EntryMethod": "CamDisplay.LoaderUmm.Load"`, `"ManagerVersion": "0.32.4.0"`, `"Author": "PrismMods"`, `"Version": "0.1.0"`)
- Create: `CamDisplay/deploy.sh`

**Interfaces:**
- Produces, `Main.cs` (loader-agnostic):

```csharp
internal static class Main {
    internal static string ModDir, DataDir, Version;            // DataDir holds CamDisplay.json
    internal static void Setup(string modDir, string dataDir, string modsRoot, string bundlePath, string version);
    internal static void Update();                               // every frame
    internal static void OnGui();                                // UMM page only (button "Open CamDisplay settings")
    internal static void Shutdown();
}
```

- `Log.cs`: `internal static class Log { static void Info(string m); static void Error(string m, Exception e = null); static string Path; }`. It writes to `<DataDir>/CamDisplayLog.txt` and to the loader's logger.
- **ML paths:** `modDir = MelonEnvironment.ModsDirectory`, `dataDir = UserData/CamDisplay`, `modsRoot = ModsDirectory`, `bundlePath = <dataDir>/PrismLib.bundle`.
- **UMM paths:** `modDir = dataDir = modEntry.Path`, `modsRoot` = parent of `modEntry.Path`, `bundlePath = <modDir>/PrismLib.bundle`.
- **`deploy.sh [ML|UMM]`:** defaults to `ML` when `<game>/MelonLoader/net35/MelonLoader.dll` exists.
  - It never installs both. It removes the other loader's copy if one is present.
  - ML: builds, copies `CamDisplay.dll` to `<game>/Mods/`, `lib/PrismLib.UI.dll` to `<game>/UserLibs/`, and `lib/PrismLib.dll` to `<game>/UserData/CamDisplay/PrismLib.bundle`.
  - UMM: copies the dll, `Info.json`, `PrismLib.UI.dll` and `PrismLib.bundle` into `<game>/UMMMods/CamDisplay/`.
  - It finishes with a `cmp -s` check, like Topaz's `deploy.sh`.

- [ ] **Step 1: Write the loaders and `Main` with `Setup` logging** `CamDisplay <version> loaded via <ML|UMM>`

- [ ] **Step 2: Build both**

Run: `cd CamDisplay && xbuild CamDisplay/CamDisplay.csproj /p:LoaderTarget=ML && xbuild CamDisplay/CamDisplay.csproj /p:LoaderTarget=UMM`
Expected: 0 errors. `bin/Debug/CamDisplay.dll` and `bin/umm/Debug/CamDisplayUmm.dll` exist.

- [ ] **Step 3: In-game check (rig = ML)**

Run: `./deploy.sh`, quit the game, then launch it through Steam.
Expected: `MelonLoader/Latest.log` contains `CamDisplay 0.1.0 loaded via ML`.

- [ ] **Step 4: Commit**

```bash
printf 'lib/*.dll\n' >> .gitignore    # fetched by update-prismlib.sh, never committed
git add .gitignore CamDisplay Info.json deploy.sh lib/update-prismlib.sh
git commit -m "build: add the CamDisplay project with MelonLoader and UMM builds"
```

### Task 2: Pure core and offline tests

**Files:**
- Create: `CamDisplay/CamDisplay/Core/CamSettings.cs`, `Core/CamContext.cs`, `Core/CamGeometry.cs`, `Core/MacPatchPlan.cs` (BCL only, **no `using UnityEngine`**)
- Create: `CamDisplay/tools/CamDisplayTest.cs`, `CamDisplay/tools/test.sh` (`mcs -out:/tmp/camdisplaytest.exe tools/CamDisplayTest.cs CamDisplay/Core/*.cs && mono /tmp/camdisplaytest.exe`)

**Interfaces:**
- Produces:

```csharp
[Serializable] internal sealed class CamSettings {          // public fields, JsonUtility-compatible; defaults = Global Constraints
    public int SchemaVersion = 1;
    public bool Enabled; public string Camera; public int Width, Height, Fps;
    public bool FlipH, FlipV, ShowInMenus, ShowInLevelSelect, ShowInEditor;
    public int Anchor; public bool AnchorClosest; public float OffsetX, OffsetY, BaseHeight, Scale, Rotation;
    public float CropL, CropB, CropR, CropT;
    public static CamSettings FromJson(string json, Func<string, CamSettings> parse, out bool wasBad); // parse = JsonUtility.FromJson in game, a stub in tests
}
internal enum CamContextKind { Gameplay, Menu, LevelSelect, Editor }
internal static class CamContext {
    public static CamContextKind Classify(string scene, bool gameworld, bool paused);
    public static bool ShouldShow(CamContextKind k, CamSettings s);
    public static string PickDevice(string saved, string[] devices, out bool fellBack);  // null when devices is empty
}
internal static class CamGeometry {
    public static float[] AnchorPoint(int anchor, float screenW, float screenH);           // same rule as LayoutMath.AnchorPoint
    public static float[] Box(CamSettings s, float screenW, float screenH, float srcW, float srcH); // {cx, cy, w, h}
    public static void MoveTo(CamSettings s, float cx, float cy, float screenW, float screenH);
    public static void Reanchor(CamSettings s, int anchor, float screenW, float screenH); // keeps the centre on screen
    public static float[] Uv(CamSettings s);                                             // {x, y, w, h} for RawImage.uvRect
}
internal static class MacPatchPlan {
    public const string UsageText = "CamDisplay shows your hand cam in-game.";
    public static bool NeedsPatch(string entitlementsXml, string infoPlistXml);
    public static string LaunchLine(string gameDir, bool hasMelonLoaderScript);
}
```

**Rules:**
- **`Classify`:**
  - `scene == "scnEditor"` → Editor. This includes editor playtest (my call).
  - Otherwise, `gameworld && !paused` → Gameplay.
  - Otherwise, `scene == "scnLevelSelect" || scene == "scnCLS"` → LevelSelect.
  - Everything else → Menu, including pause.
- **`Box`:**
  - visible source aspect = `srcW*(1-CropL-CropR) / (srcH*(1-CropB-CropT))`; if `srcW` or `srcH` is ≤ 16, use 16:9;
  - `h = BaseHeight*Scale*screenH/1080`, `w = h*aspect`;
  - centre = `AnchorPoint + (OffsetX, OffsetY)*screenH/1080`.
- **`Uv`:**
  - `x = CropL`, `y = CropB`, `w = 1-CropL-CropR`, `h = 1-CropB-CropT`;
  - FlipH → `x += w, w = -w`; FlipV → `y += h, h = -h`.

- [ ] **Step 1: Write the failing tests** in `tools/CamDisplayTest.cs`. Use the same `Check(bool, string)` / exit-code pattern as `PrismLib/tools/PrismLibTest.cs`.

```csharp
var d = new CamSettings();
Check(d.Enabled && d.FlipH && d.FlipV && !d.ShowInMenus && !d.ShowInLevelSelect && !d.ShowInEditor, "defaults: on, both flips, contexts off");
Check(d.Width == 1280 && d.Height == 720 && d.Fps == 30 && d.Anchor == 8 && d.AnchorClosest, "defaults: 720p30, bottom-right, closest");
Check(CamContext.Classify("scnEditor", true, false) == CamContextKind.Editor, "editor playtest counts as editor");
Check(CamContext.Classify("scnGame", true, false) == CamContextKind.Gameplay, "playing a level is gameplay");
Check(CamContext.Classify("scnGame", true, true) == CamContextKind.Menu, "pause is a menu");
Check(CamContext.Classify("scnCLS", false, false) == CamContextKind.LevelSelect, "custom level select");
Check(CamContext.Classify("scnSplash", false, false) == CamContextKind.Menu, "anything else is a menu");
Check(CamContext.ShouldShow(CamContextKind.Gameplay, d) && !CamContext.ShouldShow(CamContextKind.Editor, d), "gameplay always, editor opt-in");
d.Enabled = false; Check(!CamContext.ShouldShow(CamContextKind.Gameplay, d), "disabled hides everywhere"); d.Enabled = true;
bool fb;
Check(CamContext.PickDevice("Logi C920", new[] { "FaceTime HD Camera", "Logi C920" }, out fb) == "Logi C920" && !fb, "keeps the saved camera");
Check(CamContext.PickDevice("Gone", new[] { "FaceTime HD Camera" }, out fb) == "FaceTime HD Camera" && fb, "falls back to the first device");
Check(CamContext.PickDevice("x", new string[0], out fb) == null, "no devices gives null");
var b = CamGeometry.Box(d, 1920, 1080, 1280, 720);
Check(Near(b[3], 270) && Near(b[2], 480) && Near(b[0], 1660) && Near(b[1], 160), "default box: 480x270, 20px from the right, 25px up");
b = CamGeometry.Box(d, 3840, 2160, 1280, 720); Check(Near(b[3], 540) && Near(b[0], 3320), "scales with screen height");
b = CamGeometry.Box(d, 1920, 1080, 0, 0); Check(Near(b[2], 480), "unknown source size falls back to 16:9");
d.CropL = 0.25f; d.CropR = 0.25f; b = CamGeometry.Box(d, 1920, 1080, 1280, 720); Check(Near(b[2], 240), "crop narrows the box, height kept");
var uv = CamGeometry.Uv(d); Check(Near(uv[0], 0.75f) && Near(uv[2], -0.5f) && Near(uv[1], 1f) && Near(uv[3], -1f), "uv: crop then both flips");
d.CropL = d.CropR = 0;
CamGeometry.MoveTo(d, 960, 540, 1920, 1080); b = CamGeometry.Box(d, 1920, 1080, 1280, 720); Check(Near(b[0], 960) && Near(b[1], 540), "MoveTo places the centre");
CamGeometry.Reanchor(d, 0, 1920, 1080); b = CamGeometry.Box(d, 1920, 1080, 1280, 720); Check(d.Anchor == 0 && Near(b[0], 960) && Near(b[1], 540), "Reanchor keeps the centre");
bool bad; var s = CamSettings.FromJson("{not json", j => { throw new Exception(); }, out bad); Check(bad && s.Enabled && s.FlipH, "corrupt file loads defaults and reports bad");
s = CamSettings.FromJson(null, j => null, out bad); Check(!bad && s.Enabled, "missing file loads defaults quietly");
Check(MacPatchPlan.NeedsPatch("<key>com.apple.security.cs.disable-library-validation</key>", "<key>NSCameraUsageDescription</key>"), "no camera entitlement needs the patch");
Check(!MacPatchPlan.NeedsPatch("<key>com.apple.security.device.camera</key><true/>", "<key>NSCameraUsageDescription</key>"), "entitlement + usage key is patched");
Check(MacPatchPlan.NeedsPatch("<key>com.apple.security.device.camera</key><true/>", "<dict></dict>"), "missing usage key needs the patch");
Check(MacPatchPlan.LaunchLine("/G a", true) == "\"/G a/CamDisplayLaunch\" \"/G a/melonloader-launch.sh\" %command%", "launch line with MelonLoader");
Check(MacPatchPlan.LaunchLine("/G a", false) == "\"/G a/CamDisplayLaunch\" %command%", "launch line without MelonLoader");
```

`Near(a, b)` is `Math.Abs(a - b) < 0.01`.

Check the default box: the anchor-8 point is (1920, 0); the offset (−260, 160) puts the centre at (1660, 160); a 480×270 box then has its right edge at 1900 (20 px margin) and its bottom edge at 25.

- [ ] **Step 2: Run and confirm the failure**

Run: `bash tools/test.sh`
Expected: `mcs` errors about the missing `CamSettings`/`CamContext`/`CamGeometry`/`MacPatchPlan` types.

- [ ] **Step 3: Implement the four files** with the signatures and rules above

- [ ] **Step 4: Run the tests**

Run: `bash tools/test.sh`
Expected: every line `ok`, exit 0.

- [ ] **Step 5: Commit**

```bash
git add CamDisplay/Core tools/CamDisplayTest.cs tools/test.sh CamDisplay/CamDisplay.csproj
git commit -m "feat: add CamDisplay's settings, context rules and geometry with offline tests"
```

### Task 3: Camera feed, view and scene watch

**Files:**
- Create: `CamDisplay/CamDisplay/Cam/CamFeed.cs`, `Cam/CamView.cs`, `Cam/SceneWatch.cs`
- Modify: `CamDisplay/CamDisplay/Main.cs`. It loads and saves settings at `<DataDir>/CamDisplay.json`, and owns one `CamFeed` and one `CamView`.
  - Load: `parse = j => { var s = new CamSettings(); JsonUtility.FromJsonOverwrite(j, s); return s; }`, so fields missing from older files keep their defaults.
  - A bad file is renamed to `.bak`.
  - Saves are debounced to at most 1/s and flushed on `Shutdown`.
  - It adds `internal static CamSettings Settings;` and `internal static void SaveSoon();` to the Task 1 interface.

**Interfaces:**
- Consumes: Task 2 core.
- Produces:

```csharp
internal sealed class CamFeed {
    public Texture Texture { get; }              // null until playing
    public string Status { get; }                // "" when fine; else "No camera found" / "Camera access denied" / "Using <name> (saved camera not found)" / "Camera blocked by macOS"
    public string[] Devices();                   // WebCamTexture.devices names
    public void Restart(CamSettings s);          // RequestUserAuthorization once, then PickDevice + new WebCamTexture(name, Width, Height, Fps).Play()
    public void Stop();
    public void Tick();                          // detects denial / loss (isPlaying went false) → Status, no retry loop
}
internal sealed class CamView {
    public CamView();                            // DontDestroyOnLoad canvas, ScreenSpaceOverlay, sortingOrder 90, one RawImage, raycastTarget false
    public void Apply(CamSettings s, Texture tex, bool visible);  // size/pos from CamGeometry.Box, uvRect from Uv, localEulerAngles.z = Rotation
    public float[] Box(CamSettings s);           // current screen box, for PrismBridge's GetBox
}
internal static class SceneWatch {
    public static CamContextKind Current();      // active scene name + scrController.instance gameworld/paused via cached AccessTools fields; missing member → false
}
```

- `Main.Update` per frame: `feed.Tick()`, then `view.Apply(settings, feed.Texture, CamContext.ShouldShow(SceneWatch.Current(), settings) && feed.Texture != null)`.
- **macOS status:** when access is denied, `CamFeed` sets "Camera blocked by macOS" if `MacPatchPlan.NeedsPatch` is true for the running bundle (the check itself lives in Task 6; until then, use "Camera access denied").

- [ ] **Step 1: Implement, deploy, and check in-game on 3.4.0**

Expected, by screenshot:
- **Placement:** the cam shows bottom-right at 480×270 (on 1080p) while playing a level.
- **Default flips:** the image is upside-down and mirrored compared with a raw camera view.
- **Contexts:** the cam is hidden in level select and the editor, and visible there once `ShowInLevelSelect`/`ShowInEditor` are set to true in `CamDisplay.json` and the game is reloaded.
- **Unplugged:** with an external camera unplugged mid-run, the cam hides, the log shows the status once, and there are no repeated exceptions in `CamDisplayLog.txt`.

- [ ] **Step 2: Confirm the scene names**

Temporarily log `scene/gameworld/paused` on each scene change. Visit the title, level select, CLS, an official level, a custom level, the editor, editor playtest and the pause menu. If any name differs from Task 2's rules, update the `Classify` test and code together, then remove the temporary log.

- [ ] **Step 3: Commit**

```bash
git add CamDisplay/Cam CamDisplay/Main.cs CamDisplay/CamDisplay.csproj
git commit -m "feat: show the camera feed in-game with flips, crop and per-context visibility"
```

### Task 4: PrismLib bridge and layout editor hosting

**Files:**
- Create: `CamDisplay/CamDisplay/Util/PrismBridge.cs` (the only file that names PrismLib or PrismLib.UI types)
- Modify: `CamDisplay/CamDisplay/Main.cs` (call `PrismBootstrap.SyncUi()` first in `Setup`, then `PrismBridge.Init(version)`, then `PrismBridge.Tick()` per frame)

**Interfaces:**
- Consumes: Plan 2's `ModHandle.DescribeLayout`, `LayoutTarget`, `Layout.OpenRequested`/`RequestOpen`, `StateKey.LayoutEditor`, `EditTarget`, `LayoutEditor`. Also Task 3's `CamView.Box` and Task 2's `CamGeometry`.
- Produces:

```csharp
internal static class PrismBridge {
    internal static bool Available { get; }
    internal static void Init(string version);   // Ensure(Log.Info, new Version(<Plan 2 release>), modsRoot, bundlePath) → Wire → Available
    internal static void Tick();                  // editor/menu Tick; works with Available false (own-targets fallback)
    internal static void OpenLayoutEditor();      // Available ? Layout.RequestOpen() : open a local LayoutEditor over CamTargets()
    internal static void RefreshSettings();       // re-describe settings + Reload() the open settings window
}
```

- `Wire`:
  - `Prism.Register("CamDisplay", v)`;
  - `DescribeLayout(new[] { CamTarget() })`;
  - take the `StateKey.LayoutEditor` claim;
  - if it's granted, subscribe to `Layout.OpenRequested` to open a `LayoutEditor` whose source converts **every** `Layout.All()` entry to an `EditTarget`.
- `CamTarget()` is the "Hand cam" target: `Id = "cam"`, `Label = "Hand cam"`. All delegates map to `Main.Settings` through `CamGeometry` and then call `Main.SaveSoon()`:
  - box, move and scale (0.1–4);
  - rotation;
  - anchor, through `CamGeometry.Reanchor`;
  - closest;
  - crop: `{CropL, CropB, CropR, CropT}`.

- [ ] **Step 1: Implement, then run the bridge check**

Run: `../PrismLib/tools/check-bridge.sh CamDisplay/bin/Debug/CamDisplay.dll CamDisplay.PrismBridge Init Tick OpenLayoutEditor`
Expected: the tool reports both failure modes clean: the type loads, and the named methods JIT, with `PrismLib.dll` absent.

- [ ] **Step 2: In-game check**

Open the editor (temporarily bound to F8 until Task 5's settings action exists). Expected:
- a "Hand cam" frame sits over the cam;
- dragging, resizing, rotating (snapping to 90°) and cropping change the live cam;
- after a game restart the layout persists;
- with Topaz loaded alongside, the editor still opens: CamDisplay holds `LayoutEditor`, and Topaz doesn't claim it.

- [ ] **Step 3: Commit**

```bash
git add CamDisplay/Util/PrismBridge.cs CamDisplay/Main.cs CamDisplay/CamDisplay.csproj
git commit -m "feat: register the hand cam with PrismLib's shared layout editor"
```

### Task 5: Settings window

**Files:**
- Create: `CamDisplay/CamDisplay/UI/SettingsRows.cs` (builds the `SettingRow`s; PrismLib.UI types are fine here because that assembly always ships with the mod)
- Modify: `CamDisplay/CamDisplay/Util/PrismBridge.cs`. Add `ToggleSettings()`, owning a `SettingsPanel("CamDisplay", SettingsRows.Build)`, plus the `DescribeSettings` mirror for cross-mod search.
- Modify: `CamDisplay/CamDisplay/Main.cs`. The keybind **Ctrl+Shift+C** toggles settings (registered through `BindKey` when `Available`). The UMM page shows an "Open CamDisplay settings" button.

**Rows (page "CamDisplay"), in this order:**

| Group | Row | Kind |
|---|---|---|
| Camera | Enabled | Bool |
| Camera | Camera | Choice: `CamFeed.Devices()` |
| Camera | Resolution | Choice: `640×360 @ 30`, `1280×720 @ 30`, `1920×1080 @ 30`, `1280×720 @ 60` |
| Camera | Status | Text: `CamFeed.Status`, or "OK"; edits ignored |
| Image | Flip horizontal | Bool |
| Image | Flip vertical | Bool |
| Show in | Menus, Level select, Editor | Bool ×3 |
| Layout | Open layout editor | Action → `OpenLayoutEditor()` |

The `Camera` and `Resolution` rows call `CamFeed.Restart`. A device-list or status change calls `PrismBridge.RefreshSettings()`.

- [ ] **Step 1: Implement, deploy and check in-game**

Expected:
- Ctrl+Shift+C opens the window;
- toggling the flips updates the cam live;
- switching the camera restarts the feed to the chosen device;
- "Open layout editor" opens the editor;
- unplugging the camera updates the status text without reopening the window;
- game input is blocked while the window is open.

- [ ] **Step 2: Commit**

```bash
git add CamDisplay/UI/SettingsRows.cs CamDisplay/Util/PrismBridge.cs CamDisplay/Main.cs CamDisplay/CamDisplay.csproj
git commit -m "feat: add the CamDisplay settings window"
```

### Task 6: macOS "Enable camera" flow

**Files:**
- Create: `CamDisplay/CamDisplay/Mac/MacPatch.cs`, `Mac/Launcher.cs`
- Modify: `CamDisplay/CamDisplay/CamDisplay.csproj`: embed `../native/launch/CamDisplayLaunch` as `EmbeddedResource` `CamDisplay.CamDisplayLaunch`. Have the build fail if the file is missing, with the message "run native/launch/build.sh".
- Modify: `UI/SettingsRows.cs`. Add a "macOS" group, shown only on `Application.platform == OSXPlayer`.

**Interfaces:**

```csharp
internal static class MacPatch {
    public static string AppPath { get; }        // the running .app: Application.dataPath's parent of "Contents"
    public static bool NeedsPatch();             // reads `codesign -d --entitlements :- <app>` and Info.plist → MacPatchPlan.NeedsPatch
    public static string Apply();                // backup (first time only) → plist → re-sign; returns "" or an error; never throws
    public static string Undo();                 // restore the backup; "" or an error
}
internal static class Launcher {
    public static string Install();              // write the resource to <game>/CamDisplayLaunch, chmod 755, codesign -s -; "" or an error
    public static string Line();                 // MacPatchPlan.LaunchLine(gameDir, File.Exists(gameDir + "/melonloader-launch.sh"))
}
```

- **Shared rules:** these follow `tools/mac-camera-patch.sh` (Plan 1) step for step, with the same backup dir `~/Library/Application Support/CamDisplay/sig-backup/`. They run `/usr/bin/codesign`, `/usr/bin/plutil` and `/bin/chmod` through `Process`, with a 15 s timeout each.
- **Rows:**
  - Status text;
  - "Enable camera on macOS" → `Launcher.Install()`, then `MacPatch.Apply()`, then set the status to "Restart the game through Steam to finish";
  - Launch options, as Text showing `Launcher.Line()`;
  - "Copy launch options" → `GUIUtility.systemCopyBuffer = Launcher.Line()`;
  - "Undo camera patch" → `MacPatch.Undo()`.
- **Re-signing a running app:** Plan 1 only proved re-signing with the game closed. Test it here. If `codesign` fails, or the game misbehaves after re-signing while it runs, switch `Apply` to write a one-shot script that waits for the game PID to exit and then re-signs. Record which way it went in a code comment.

- [ ] **Step 1: In-game check on the rig**

Run `tools/mac-camera-patch.sh undo` (stock signature), then launch. Expected:
- the status shows "Camera blocked by macOS";
- **Enable** finishes and the status asks for a restart;
- after a restart through Steam, the camera prompt appears, then frames arrive;
- **Undo** followed by a restart returns the status to "Camera blocked by macOS";
- **Copy** puts the exact `Line()` on the clipboard (check with `pbpaste`).

- [ ] **Step 2: Commit**

```bash
git add CamDisplay/Mac CamDisplay/UI/SettingsRows.cs CamDisplay/CamDisplay.csproj
git commit -m "feat: add the macOS enable-camera flow with launcher install and undo"
```

### Task 7: Cross-version and performance checks, release packaging

**Files:**
- Create: `CamDisplay/release.sh`. It runs `lib/update-prismlib.sh`, then `native/launch/build.sh`, then `tools/test.sh`, then both xbuilds, then `check-bridge.sh`. It produces:
  - `CamDisplay.zip`: `Mods/CamDisplay.dll`, `UserLibs/PrismLib.UI.dll`, `UserData/CamDisplay/PrismLib.bundle`;
  - `CamDisplayUmm.zip`: `CamDisplay/{CamDisplayUmm.dll, Info.json, PrismLib.UI.dll, PrismLib.bundle}`.
  
  `release.sh <ver>` also stamps `AssemblyInfo`, `MelonInfo` and `Info.json`.
- Create: `CamDisplay/README.md`. It covers installing both loaders, the macOS "Enable camera" steps including the Steam launch options, the note **"Windows: untested"**, and the measured performance cost.

- [ ] **Step 1: Performance check (Global Constraints / spec pass line)**

On the Mac rig, play the same dense level twice, cam off then on, with PrismLib's FPS counter on. Record p99 frame time.
- **Pass:** cam-on p99 is within 0.5 ms of cam-off.
- **Fail:** change the default resolution to `640×360 @ 30` (both the `CamSettings` default and its Task 2 test), re-measure, and record both numbers in the README.

- [ ] **Step 2: Cross-version and Linux checks**

- **ADOFAI 2.9:** use the Steam beta branch if available; ask the user how they switch versions.
  - First confirm that `WebCamTexture` resolves. The build binds it to 3.4's `UnityEngine.AudioModule`; if 2.9 keeps it elsewhere, reference the `UnityEngine.dll` facade instead.
  - Confirm that the CameraFilterPack shaders exist (needed for Part 3 later).
  - Then repeat Task 3 Step 1, Task 4 Step 2 and Task 6 Step 1.
- **Linux:** on the Arch machine, repeat Task 3 Step 1, driving it per `PrismLib/CLAUDE.md` "Testing", and measure `Play()` time. If the stall is under 50 ms there, note in the README that Linux could stop the camera while hidden, but don't change behavior in v1.

Record the results in the spec under a `### v1 verification (date)` section.

- [ ] **Step 3: Commit**

```bash
git add release.sh README.md && git commit -m "build: add the release script and README"
```

- [ ] **Step 4: Release — ask the user for the version first**

Confirm Plan 2's PrismLib release is published. `lib/update-prismlib.sh` must report that version.

Run: `./release.sh <ver>`, then `git add -A && git commit -m "release: <ver>"`.

Creating the GitHub repo or release and pushing happen **only when the user asks**.
