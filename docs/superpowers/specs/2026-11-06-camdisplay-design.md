# CamDisplay + shared layout editor — design

Date: 2026-11-06. Status: agreed in conversation, not yet built.

Two deliverables, in this order:

1. **PrismLib: a shared layout editor.** It is a cross-mod "move/resize/rotate/crop things on screen" editor in the style of Quartz's repositioning menu, built on UI Toolkit.
2. **CamDisplay: a new standalone mod.** It shows a webcam ("hand cam") feed inside ADOFAI and is the first user of that editor.

## Why

Players record clears with a hand cam to show the run was played by hand. Today that means building an OBS scene to put the camera over the game. CamDisplay draws the camera in the game itself, so any plain screen recording already includes it and no OBS setup is needed.

The editor goes in PrismLib because positioning things on screen is a shared need. Topaz, Quartz and CamDisplay all have overlays, and the user wants to line the cam up against Topaz's or Quartz's key viewer in one session. CamDisplay will later be folded into Topaz. It ships separately for now.

## Goals

- Show a live webcam feed in-game, with position, size, rotation and crop freely adjustable.
- Horizontal and vertical flip toggles, **both on by default**. Both together equal a 180° turn, which suits a camera mounted above the keyboard and pointing back at the player.
- The cam always shows while playing a level. Menus, level select and the editor each have a separate toggle, **off by default**.
- One editing session moves elements from every loaded Prism mod that has registered with the editor.
- Ship a MelonLoader build and a UMM build, built for PrismLib compatibility like Topaz.
- Works on macOS (see "macOS camera access"), Windows and Linux.
- **Supports ADOFAI 2.9 (Unity 2022) and 3.x (Unity 6)**, like Topaz (user's decision).
  - Use `Topaz/Topaz/Util/GameCompat.cs` as the reference for touching game types that differ between the two.
  - The visibility toggles' scene/state detection (gameplay, menus, level select, editor) must work on both.
  - UI Toolkit on Unity 2022 has the font-inheritance landmine described in `PrismLib/CLAUDE.md`; `Surface.HandDownFonts` handles it.
  - Confirm which module holds `WebCamTexture` on 2.9.

## Non-goals

- **Recording, saving or streaming video.** CamDisplay only displays the cam. The user still needs some screen recorder.
- IL2CPP builds. ADOFAI is Mono, and Quartz's IL2CPP configurations are unused: no code checks the `IL2CPP` flag.
- Moving Topaz's or Quartz's existing overlays onto the new editor. That's later work: Topaz has its own `GameUiEditor`/`LocationEditor`, and Quartz isn't on PrismLib yet. Until then, the editor's only target is the cam.
- Opacity, borders, rounded corners, chroma key, multiple cameras at once. None of these were asked for.

## Anti-goals

- **Costing frames during a run.** Performance is the user's main worry. Specifically:
  - no per-frame CPU pixel work: no `GetPixels`, `GetPixels32`, `SetPixels` or `Apply` on the feed. Crop and flip are UV/transform changes only.
  - no main-thread stalls during gameplay (see "Camera lifecycle").
- Any CamDisplay-specific hooks scattered outside its own code. It has to drop into Topaz in one piece later.

## Part 1 — Shared layout editor (PrismLib)

### Behavior (as the user decided: "Quartz's repositioning menu, same logic")

Reference implementation: `Quartz/Quartz/Quartz/UI/Utility/Reorganizer*.cs`, `ReorganizeHandle.Frame.cs`, `ReorganizeHistory.cs` and `ResizeHandle.cs`. It is uGUI, so port the behavior, not the code.

- **Frames:** while the editor is open, every registered element shows a faint frame with its name tag above it. The tag drops below the frame when the element is pinned to the top edge.
- **Select:** click to select. When elements overlap, the selected one keeps the press; a click without dragging moves selection to the next layer down, so a covered element is always reachable.
- **Drag:** dragging moves the element and snaps it to screen edges and centers (Quartz uses an 8px threshold scaled to screen height).
- **Resize:** corner grips resize uniformly, so the element never stretches.
- **Rotate:** a rotation knob above the selected element. My call: it snaps to multiples of 90° within a few degrees, so sideways mounts are easy.
- **Anchor:** nine-way anchor plus "Closest", which re-anchors to the nearest third of the screen while dragging. The anchor point is drawn with a line to the element.
- **Exact values:** X / Y / size / rotation fields that stay in step with dragging.
- **Crop mode:** only for elements that support it. Drag the four edges inward to trim, or type exact values. Crop changes what is shown; size then scales the cropped result.
- **Undo/redo** across moves, resizes, rotations and crops. `PrismLib.UI/Toolkit/History.cs` already coalesces per-frame changes; reuse or extend it.
- **Done:** closes the editor. Game input is blocked while the editor is open (`InputGate.Block`).

### Where the code goes (decided)

- **Registry → `PrismLib.dll`.** Every mod's elements must be visible to one editor, which makes this a single shared instance under PrismLib's own rule. It uses plain BCL types only: delegates, floats, strings, no Unity types. It is modeled on `ModHandle.DescribeSettings`, so it hangs off `ModHandle` and a second call replaces the first.
  - Each element supplies: id, display name, screen rect getter, move/position setter, size getter/setter, and optionally rotation, anchor and crop (four edges) getters/setters.
  - Each mod owns its element's persistence, as with settings.
- **Editor UI → `PrismLib.UI.dll`.** It is UI Toolkit, built on the existing `Surface`/`Window`/`Ui` pieces.
- **Two existing rules apply:**
  - `PrismLib.UI` must not reference `PrismLib.dll`, so the bridge converts registry entries to plain delegates, as `DebugPanel`/`SettingsPanel` do today.
  - When several mods are loaded, one draws the editor. Follow the `StateKey.DebugPanel` claim precedent, but with a **new** `StateKey` (for example `LayoutEditor`) that only editor-capable bridges claim. That way Sapphire and Topaz need no changes or releases for this project (my call).
- **Fallback:** when `PrismLib.dll` is unavailable, a mod can still open the editor for its own elements only. This follows the existing `Available`-guard rule.

### Left to the builder

The exact API names and signatures; coordinate units (pick one and document it); the snap set beyond edges and centers; the open keybind; the inspector layout.

## Part 2 — CamDisplay

### Settings

CamDisplay opens **its own** `PrismLib.UI` `SettingsPanel`, built from its own `SettingRow`s, using the existing public constructor (my call). It is opened by a CamDisplay keybind and, under UMM, from the mod's UMM page.

It does not rely on the shared Alt+Shift+S window. That window is drawn by whichever mod holds `StateKey.DebugPanel` (Sapphire on the user's rig), through that mod's own converter. That converter turns unknown kinds into toggles, drops `Visible`/`Enabled`, and reads values only on open. Relying on it would force Sapphire and Topaz releases for the status text, the copyable launch line and a device list that changes.

CamDisplay still calls `DescribeSettings`, so its settings show up in cross-mod search.

| Setting | Default |
|---|---|
| Enabled | on |
| Camera (from `WebCamTexture.devices`, remembered by name, falls back to the first device) | first device |
| Requested resolution / frame rate | 1280×720 @ 30 (my call) |
| Flip horizontal | on |
| Flip vertical | on |
| Show in menus / level select / editor (three toggles) | off |
| Open layout editor (action) | — |
| macOS only: Enable camera, Undo camera patch (actions), plus status text | — |

The cam's position, size, rotation, anchor and crop are edited only in the layout editor and stored in CamDisplay's own settings file. Flips are settings, not editor actions.

### Rendering and performance

- Use `WebCamTexture` (in `UnityEngine.AudioModule`). Draw it on the GPU through a UI Toolkit `Image` or a uGUI `RawImage`, whichever the builder finds cleaner next to the editor.
- Crop = UV rect. Flip = negative UV scale or element scale. Rotation = element transform.
- Draw order (my call): above the game's HUD, below mod menus and editor chrome.
- If the camera is missing, unplugged, or access is denied, hide the cam and show the reason in the settings status text. Never throw every frame.
- Measured on macOS: a FaceTime camera in low light delivered about 15 fps against the 30 requested. That's the device, not the mod.

### Camera lifecycle (my call, from a measurement)

The spike measured `WebCamTexture.Play()` **blocking the main thread for 0.7–1.3 s** on macOS. Stopping the camera whenever the cam is hidden would therefore cause a stall on every entry into a level.

So the camera runs whenever CamDisplay is enabled and access is granted, and the Enabled toggle is the off switch. Unity stops the webcam on every scene change (see "Gate result"), so CamDisplay calls `Play()` again after each scene change. That restart lands during the transition, not in gameplay. A hidden cam keeps the device open, so the macOS camera light stays on.

The builder should re-measure `Play()` on Windows and Linux. If the stall is negligible there, stopping while hidden is allowed on those platforms.

### Builds and layout

- Two loader builds from one source, modeled on Quartz's `LoaderTarget` (ML default, UMM via a compile constant that swaps the loader entry file). Note Quartz's warning: the UMM build needs a **distinct assembly identity**, or the MelonLoader→UMM bridge hands back the already-loaded ML assembly.
  - **ML:** `Mods/CamDisplay.dll`, with `PrismLib.UI.dll` in `UserLibs/`.
  - **UMM:** `UMMMods/CamDisplay/` with `Info.json`.
- PrismLib integration follows `PrismLib/CLAUDE.md` exactly:
  - a bridge class with no PrismLib types in its fields;
  - `PrismBridge.Init` called from `OnLoad` / `OnInitializeMelon`;
  - `PrismLib.bundle` (never `.dll`) shipped;
  - `release.sh` runs `lib/update-prismlib.sh` and copies `lib/PrismLib.UI.dll`;
  - `tools/check-bridge.sh` passes.
- Repo: new `PrismMods/CamDisplay/`, GPL-3.0 like the other mods (my call). Keep all cam code in one namespace/folder so Topaz can absorb it.
- **Release order:** CamDisplay's `release.sh` ships the latest *published* PrismLib, so a PrismLib release containing the editor must go out first. The user decides version numbers (PrismLib steps by 0.0.1).

## Part 3 — Advanced camera settings (follow-up release)

Added 2026-11-06 at the user's request: "advanced camera settings tweaks in its custom settings menu, sort of like cameractrls on Linux, but only the basic settings". This **ships after CamDisplay's first release** (user's decision), so it adds nothing to the macOS-gated first release.

### Controls (user's decision: all four groups)

| Group | Controls | Hardware | Software fallback |
|---|---|---|---|
| Exposure | auto/manual, exposure value, gain, anti-flicker 50/60 Hz | yes | none, hidden if unsupported |
| White balance | auto/manual, color temperature | yes | approximate tint via the game's `CameraFilterPack/Colors_Adjust_ColorRGB` |
| Focus | auto/manual, focus distance | yes | none, hidden if unsupported |
| Image | brightness, contrast, saturation, sharpness | yes | brightness/contrast/saturation via the game's `CameraFilterPack/Color_BrightContrastSaturation`; sharpness hardware only |

**Approach: hardware + software (user's decision).**
- Each control uses the camera's real control when the OS and camera expose it. Otherwise it uses the software fallback above, or is hidden if there is none.
- One slider per property, never two (my call).
- There's a "Reset to camera defaults" action.
- Settings are stored per camera name.

**Why `WebCamTexture` isn't enough:** it exposes no camera controls, so hardware controls are native, per OS.

**Linux (V4L2):**
- Use `ioctl` via P/Invoke to libc: `VIDIOC_QUERYCTRL` / `VIDIOC_G_CTRL` / `VIDIOC_S_CTRL`, as cameractrls does.
- Map the `WebCamTexture` device name to `/dev/videoN` through the `card` field from `VIDIOC_QUERYCAP`.
- Testable on the user's Arch machine.

**Windows (DirectShow COM):**
- `IAMVideoProcAmp` (brightness, contrast, saturation, sharpness, white balance, gain, power-line frequency) and `IAMCameraControl` (exposure, focus).
- Ships untested.

**macOS:**
- AVFoundation's manual exposure and white-balance APIs are iOS-only, and built-in FaceTime cameras expose essentially nothing. Those cameras get software fallbacks only.
- USB webcams (UVC) are controlled with UVC class requests sent through IOKit USB.
- Prefer P/Invoke into system frameworks. If a native dylib is unavoidable, embed it and have the mod write it to disk, like the launcher, so it isn't quarantined.

**Software fallbacks:**
- Each new camera frame (`didUpdateThisFrame`) is blitted through the game's shader into a `RenderTexture` on the GPU. That's one blit per camera frame, not per game frame.
- Mods can't compile shaders at runtime, so reusing the shaders the game already ships (confirmed in 3.4.0's `globalgamemanagers` always-included list) is what makes this possible. Confirm they exist on 2.9 too.
- When every fallback is at its neutral value, skip the blit and show the camera texture directly.

**Anti-goal:** advanced settings must not reintroduce per-frame CPU pixel work or main-thread stalls. Hardware `ioctl`/COM calls happen only when a setting changes, never per frame.

## macOS camera access (tested 2026-11-06)

A throwaway UMM spike ran on the user's Mac (ADOFAI 3.4.0, Unity 6000.3.25f1, MelonLoader + UMMBridge). Results:

| Game signature | Launched by | Result |
|---|---|---|
| 7th Beat Developer ID (stock) | Steam, through `melonloader-launch.sh` | denied instantly, no prompt, 0 frames |
| ad-hoc + camera entitlement + `NSCameraUsageDescription` | Steam, through `melonloader-launch.sh` | denied instantly, no prompt |
| stock | LaunchServices (`open --env DYLD_INSERT_LIBRARIES=… -a`) | denied instantly, no prompt |
| ad-hoc + camera entitlement + usage key | LaunchServices | **prompt shown, live 1280×720 frames** |

Two things gate the camera, and both have to be fixed:

1. **Missing entitlement.** The game is signed with the hardened runtime and has no `com.apple.security.device.camera` entitlement and no `NSCameraUsageDescription`.
2. **Steam is held responsible.** MelonLoader's `melonloader-launch.sh` `exec`s the game binary as a **direct child of `steam_osx`**, so macOS treats Steam as the responsible process. Steam declares only the microphone.

Stock entitlements to preserve: `allow-dyld-environment-variables`, `allow-unsigned-executable-memory`, `disable-executable-page-protection`, `disable-library-validation`, `network.client`.

### Chosen route: re-sign + launcher (user's decision)

**A. "Enable camera on macOS" (settings action):**

1. Back up `Contents/Info.plist`, `Contents/_CodeSignature/` and `Contents/MacOS/<binary>` into CamDisplay's data folder (about 250 KB).
2. Add `NSCameraUsageDescription` to `Info.plist`.
3. Re-sign the main bundle only (no `--deep`): `codesign --force --sign - --options runtime --entitlements <current entitlements + com.apple.security.device.camera>`. Read the current entitlements at runtime; don't hard-code them.
4. Takes effect on the next launch, so the mod says so.

Related behavior:

- **Detection:** on macOS, the mod checks whether the bundle lacks the camera entitlement or the usage key. If so, it offers the action again. That covers Steam updates and "Verify integrity", which both restore the stock signature.
- **Undo camera patch:** restores the backup. Steam's "Verify integrity" also works.
- **Re-prompts:** each re-sign changes the code hash, so macOS asks for camera permission again after every re-patch. That's expected.
- **Unverified:** whether re-signing while the game is running is safe. The spike re-signed with the game closed. If it isn't safe, defer the re-sign to a script that runs after the game exits.

**B. Launcher:**

- **What it is:** a tiny native macOS binary (C, universal arm64 + x86_64). It calls `posix_spawn` with `POSIX_SPAWN_SETEXEC` and responsibility disclaimed, so it **replaces itself** with its arguments and the game keeps the PID Steam started.
  - Disclaiming uses `responsibility_spawnattrs_setdisclaim`, a private libsystem symbol looked up with `dlsym`; terminal apps use the same technique.
  - The first version spawned a child and waited for it, and Steam's Stop then took about 36 s, because Steam asked a non-app process to quit.
- **How it gets installed:** the mod embeds the launcher, writes it into the game folder itself, makes it executable and ad-hoc signs it. Files the game process writes aren't quarantined, which avoids a Gatekeeper block on a downloaded binary.
- **Launch options:** the mod shows the exact Steam Launch Options line, with a copy button. The line puts the launcher in front of the helper the user already uses:
  - with UMMInstall's `setup_helper.sh` (the user's rig): `"<game>/CamDisplayLaunch" "<game>/setup_helper.sh" %command%`
  - with MelonLoader's own script: `"<game>/CamDisplayLaunch" "<game>/melonloader-launch.sh" %command%`
  - with neither: `"<game>/CamDisplayLaunch" %command%`
- **Untested:**
  - the launcher itself (the spike proved the idea with `open`, not with disclaim);
  - whether vanilla UMM users (no MelonLoader) need it. MelonLoader's script comment says Steam launches a plain `.app` through LaunchServices, which would already make the game its own responsible process.
- **First implementation task** for macOS is a spike that confirms all three of these through the real Steam launch: the camera works, MelonLoader still injects, and Steam still tracks the game (play time, overlay).
- **macOS blocks the release (user's decision).** CamDisplay does not ship until this works through a normal Steam launch. A non-Steam `open` launch is not an acceptable fallback.
  - Run the spike before Part 1, so a failure is known early.
  - If it fails, stop and bring it back to the user before building further. The helper-app route (a separate camera app sharing frames through the GPU) is the known alternative.
- Run the macOS patch on a 2.9 bundle too. Its entitlements may differ; they are read at runtime, but verify that.

Possible later improvement (not in scope): propose responsibility-disclaim to MelonLoader's macOS launch wrapper upstream.

### Gate result (2026-11-06): PASS

Setup: the game patched with `tools/mac-camera-patch.sh`; launch options `"<G>/CamDisplayLaunch" "<G>/setup_helper.sh" %command%`; started from Steam.

| Check | Result | Evidence |
|---|---|---|
| Camera works | pass | `hasAuth=True`, `1280x720`, `updates` rising at about 15 fps. tccd attributes the request to `com.7thbeat.adofai`. |
| MelonLoader injected | pass | `Latest.log`: `UMMBridge.dll` loaded, `[Umm] active mods: [CamProbe, Sapphire, Topaz]`. |
| Process tree | pass | The game PID equals Steam's `ProcID`, and its parent is `steam_osx`. |
| Steam tracks the game | pass | `Game process added` / `removed` for AppID 977950. The overlay is **not a requirement**: the user says players don't use it because it interferes with gameplay. |
| Stop works | pass | `TerminateApp` at 02:15:41, game removed at 02:15:44 (3 s). |

**Finding that changes Part 2:** Unity's `WebCamTexture` is **stopped by Unity itself** on **every scene change** (`AVCaptureSession stopRunning` called from `UnityPlayer`, main thread). Seen at startup → `scnLevelSelect`, → `scnEditor`, and back.
- One `Play()` afterwards resumes the feed. The restart cost 0.13–1.2 s on the main thread, so it lands during the scene transition, not in gameplay.
- CamDisplay must therefore restart the feed after every scene change.
- It is untested whether this also happens on Windows/Linux, or with no other mods loaded. The restart covers it either way.

**Windows / Linux:** no known gate. Windows has a per-user "let desktop apps access your camera" privacy toggle, and its failure should show in the status text. The user has an Arch Linux machine for testing.

**Windows ships untested (user's decision):** there is no Windows machine or tester. Windows isn't in the release criteria, and the README and release notes say it is untested. Keep the keep-camera-running default there.

### v1 verification (2026-11-06, automated while the user slept)

All on the user's Mac (ADOFAI 3.4.0, MelonLoader + UMMBridge, launched from Steam through
CamDisplayLaunch), by DEBUG-only self-tests that log PASS/FAIL:

- PrismLib layout editor (LayoutProbe autotest, synthetic UI Toolkit events): 36/36, including
  the final-review fixes (handles on screen after Reload, rotated hit-testing/snapping, field
  commits, crop edges, Ctrl+Z exempt from the input gate).
- CamDisplay (SelfTest): 25/25 — ML load, PrismLib wiring, live feed, context rules (level select
  `gameworld=False`, editor scene), placement/flips/crop reaching the RawImage, JSON save, editor
  and settings window, restart after a scene change, macOS status/launcher/launch line/clipboard,
  and the queued undo+apply running after game exit.
- p99 frame time in the level select: 15.60 ms cam hidden, 15.86 ms shown (budget 0.5 ms).

Final-review fixes, re-run 2026-11-06 09:50 with the Mac unlocked: with Topaz and Sapphire 28/29
then 29/29 after the scene-change check below; standalone (no other Prism mod) 29/29, with
CamDisplay itself installing the input gate.

**Scene-change stop is unavoidable from the mod.** Marking the texture `DontUnloadUnusedAsset`
did not stop Unity from halting the camera on a scene change. The restart lands in the same frame
the stop is seen (load transition) and costs about 130 ms on the main thread (about 1.2 s when
entering the editor). Every level retry is a scene change, so whether that 130 ms is felt at a
retry's start is part of the dense-level check below; the log line `restarted N frame(s) after it
(Play took X ms)` shows the cost.

**Not yet verified:** a dense level including retries (needs a human to play one), the UMM build
in-game, ADOFAI 2.9, Linux, Windows (ships untested by decision), and any visual check of styling.

## Testing

- **Offline:** registry rules in `PrismLib.dll` go in `tools/PrismLibTest.cs` (replace-on-second-call, removal by owner, entries with missing delegates skipped).
- **In-game:** there is no headless mode. macOS uses the rig in the user's memory (ML + UMMBridge, launch via `steam://rungameid/977950`, logs in `MelonLoader/Latest.log`). Verify by screenshot. Linux can be driven from a shell, per `PrismLib/CLAUDE.md` "Testing".
- **Performance check (my call for pass/fail):** play the same dense level with the cam on and off, and compare p99 frame time.
  - **Pass:** cam-on p99 is within 0.5 ms of cam-off.
  - **If 1280×720 @ 30 fails:** drop the default to 640×360 @ 30 and record the measured cost in the README.
  - **Also measure:** the `Play()` stall on each platform.
  - `WebCamTexture`'s own upload of each new camera frame is engine-side and can't be avoided. Resolution, frame rate and Enabled are the only levers.

## Decisions log

**User's decisions:** position, size and crop are freely customizable. Editing follows Quartz's repositioning logic, and the editor lives in PrismLib as a shared, cross-mod feature with one session showing all mods' elements. Horizontal and vertical flips default to on. The cam always shows during gameplay; menus, level select and editor are opt-in. Display only, no recording. Mono only. PrismLib-compatible like Topaz, plus ML builds. The name is CamDisplay. Ship separately, integrate into Topaz later. On macOS, re-sign + launcher, and macOS blocks the release until it works through a normal Steam launch. Supports ADOFAI 2.9 and 3.x. Windows ships untested. Advanced camera settings (exposure + gain, white balance, focus, image tweaks) use hardware plus software fallbacks and ship in a follow-up release.

**My calls (flagged above):**
- CamDisplay opens its own PrismLib.UI `SettingsPanel` (not the shared Alt+Shift+S window);
- the editor gets its own new `StateKey`, so Sapphire and Topaz are untouched;
- performance pass line: p99 within 0.5 ms, with a 640×360 fallback;
- 1280×720 @ 30 default;
- rotation snaps to 90°;
- draw order;
- the camera stays running while enabled;
- GPL-3.0;
- backup and undo for the macOS patch;
- the mod installs and signs the launcher itself;
- the spec lives in the PrismLib repo;
- advanced settings: one slider per property, hardware first then software; settings stored per camera; sharpness hardware only; the blit is skipped when all fallbacks are neutral.
