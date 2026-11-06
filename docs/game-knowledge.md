# ADOFAI facts for Prism mod development

Verified facts about the game, Unity as this game uses it, the two loaders and macOS. Every
line says where it was verified. Unless noted: ADOFAI 3.4.0 (r150), Unity 6000.3.25f1, macOS on
Apple silicon, MelonLoader 0.7.3 with UMMBridge hosting Topaz and Sapphire. "Unverified" means
seen in the code or IL but never observed in a running game.

Library mechanics (claims, InputGate, fonts, UI Toolkit landmines) stay in `../CLAUDE.md`. This
file is about the game around it.

## Scenes

Build scenes (from `globalgamemanagers`): `scnSplash`, `scnLevelSelect`, `scnCLS`, `scnGame`,
`scnEditor`, `scnLoading`, `scnCalibration`, `scnMinesweeper`, and one scene per official world:
`1-X` … `12-X`, `AB-X`, `B-X`, `ML-X`, `MN-X`, `RJ-X`, `XC-X`, `XF-X`, `XH-X`, `XN-X`, `XO-X`,
`XR-X`, `XT-X`. Topaz's key viewer also names `scnLevelSelectTaro`, `scnTaroMenu`, `scnVegaMenu`,
`scnMobileMenu` (DLC and platform menus; unverified here).

- **Boot:** `scnSplash` then `scnLevelSelect` about two seconds later.
- **Custom levels opened from the level select are played INSIDE `scnEditor`, in play mode.** A
  rule like "scnEditor means the editor" hides things during every custom level. Verified
  2026-11-06 (CamDisplay context log: `scene=scnEditor … editorPlaying=True` while the user
  played). Official levels play in their world scene (`N-X`).
- **Restarting a level does not change scene.** `scrController.Awake_Rewind` fires on every
  start and retry; `SceneManager.activeSceneChanged` does not. Verified 2026-11-06 (Topaz hook log
  plus CamDisplay's scene-change log over four retries).

## Game state flags

Read by name (`Type.GetType("scrController, Assembly-CSharp")`, fields or properties) to stay
binary-compatible across 2.9 and 3.x. CamDisplay's `Cam/SceneWatch.cs` does this.

| | `scnSplash` | `scnLevelSelect` | `scnEditor`, editing | `scnEditor`, playing |
|---|---|---|---|---|
| `scrController.instance` | null | set | set | set |
| `scrController.gameworld` | false | false | **true** | true |
| `scrController.paused` | false | false | true | false |
| `scnEditor.instance.playMode` | n/a | n/a | false | **true** |

Verified 2026-11-06 (CamDisplay log, real play). Consequences:

- `gameworld` cannot tell editing from playing in the editor scene; `scnEditor.playMode` can
  (Topaz's key viewer relies on it too).
- A working "is a level being played" test: `gameworld && !paused`, and in `scnEditor` also
  `playMode`. Paused in a level reads as `paused == true`.
- `ADOBase` has bool properties `isLevelEditor`, `isCLSLevel`, `isEditingLevel`, `isCLS`,
  `isLevelSelect`, `isOfficialLevel`, `isDLCLevel`, `isBossLevel` and more (3.4.0 IL). Present,
  semantics unverified.
- Official levels: `scrController.levelName` is the internal name; for custom levels it is
  `"scnGame"` and `scnGame.instance.levelPath` holds the file (Topaz `Util/LevelKey.cs`).

## Unity behaviour in this game

- **The UI font PrismLib.UI picks on macOS has no Hangul.** On Unity 6, PrismLib.UI's OS-family font
  (Helvetica Neue, with Menlo as its fallback) has no glyphs for 크 or 기, so Korean text in Prism
  windows has no glyph to draw. Facet fixes it by appending the OS CJK face
  (`/System/Library/Fonts/AppleSDGothicNeo.ttc`) to the regular and bold fallback tables. Its
  autotest checks glyph presence, not label width. Verified 2026-11-06 (Facet Task 8, FontAsset
  HasCharacters in game). PrismLib.UI gets the fix when it moves onto Facet.
- **ADOFAI 2.9.x builds do not run Unity 2022**, so the "2.9 = Unity 2022" pairing in
  `../CLAUDE.md` is wrong, and Prism's UI Toolkit windows support 3.x (Unity 6) only. Stated by the
  user 2026-11-06; expected, and part of the plan. The Unity 2022 font notes stay, unverified, for
  other games.
- **Unity stops a `WebCamTexture` on every scene change** (`AVCaptureSession stopRunning` called
  from `UnityPlayer` on the main thread). Marking it `HideFlags.DontUnloadUnusedAsset` does not
  prevent it. `Play()` is synchronous on the main thread: about 0.9–1.2 s for the first start,
  about 130 ms for a restart after a scene change, about 1.2 s when entering the editor.
  Verified 2026-11-06 (CamDisplay).
- **The game loop pauses while the window is unfocused**, and stays paused while the Mac is locked
  even with `Application.runInBackground = true` (no frames, coroutines stall). Unattended tests
  need an unlocked session. Verified 2026-11-06.
- `Application.dataPath` on macOS is `…/ADanceOfFireAndIce.app/Contents` (not `…/Resources/Data`).
  Walk up to the `.app` instead of counting levels. Verified 2026-11-06.
- A runtime UI Toolkit panel accepts synthetic input: `PointerDownEvent.GetPooled(new Event {…})`
  with panel coordinates, sent through `root.panel.visualTree.SendEvent`. Buttons answer
  `NavigationSubmitEvent`. The real mouse still sends its own move events in between, so a still
  click must send down and up in the same frame. Verified 2026-11-06 (LayoutProbe autotest).

## Loaders

- **Load order under MelonLoader:** `UserLibs/*.dll` (PrismLib.UI included), then `Plugins/`
  (UMMBridge), then `Mods/`; ML mods initialise before the UMM mods UMMBridge hosts. So an ML mod
  is first to call `InputGate.EnsureInstalled` (log: `installed by CamDisplay`). Verified
  2026-11-06.
- MelonLoader warns `'<Mod>' is missing the following dependencies: 'PrismLib'` for a mod that
  references `PrismLib.dll`: the check runs before PrismBootstrap installs the shared copy. Harmless,
  but players will see it. Verified 2026-11-06.
- MelonLoader's assembly has a `Harmony` namespace, so `Harmony` alone does not compile in an ML
  build; write `HarmonyLib.Harmony`. Harmony versions differ between the loaders: use only APIs
  both have (`CreateProcessor`, `AddPrefix/Postfix/Transpiler/Finalizer`, `Patch`, `GetPatchInfo`).
  `UnpatchAll(string)` is an obsolete error in the ML copy.
- **UMM's on/off switch calls `OnToggle`; a mod that does not set it keeps running** (camera,
  overlays, registrations) while "off". `OnUpdate` stops while the mod is inactive. Verified
  2026-11-06 by toggling `ModEntry.Active` in CamDisplay's self-test.
- MelonLoader attribute for this game: `[assembly: MelonGame("7th Beat Games", "A Dance of Fire and Ice")]`.
- `xbuild` silently drops an `EmbeddedResource` whose file has no extension; copy it to a `.bin`
  in a pre-build step and give it a `LogicalName`. Verified 2026-11-06.

## Keybinds

Prism chords are plain **Alt+key** (2026-11-06): **Alt+D** debug window, **Alt+P** Prism
settings, **Alt+T** Topaz's menu, **Alt+C** CamDisplay's settings. The game and its editor own the
Ctrl chords (Ctrl+S saves the level, Ctrl+Z undoes, Ctrl+C/V copy and paste), and never use Alt.
On macOS read the letter through `MacKeys` (Unity drops letters under Option; see `../CLAUDE.md`).
Topaz's key limiter lets Alt chords through while its menu is open.

## macOS

- **The camera needs two things the stock game lacks** (CamDisplay spec, "macOS camera access"):
  the camera entitlement plus `NSCameraUsageDescription` (re-sign ad-hoc, keeping the stock
  entitlements), and the game being its own TCC "responsible process". Steam starts the game as
  its child, so macOS asks Steam, which only declares the microphone. CamDisplay's launcher calls
  `posix_spawn` with `responsibility_spawnattrs_setdisclaim` and `POSIX_SPAWN_SETEXEC`. A spawned
  child made Steam's Stop take 36 s; exec in place makes it 3 s.
- Stock signature: Developer ID `7th Beat Games S.A.C. (V44H323E4G)`, hardened runtime,
  entitlements `allow-dyld-environment-variables`, `allow-unsigned-executable-memory`,
  `disable-executable-page-protection`, `disable-library-validation`, `network.client`.
  `codesign -d --entitlements :-` is deprecated; use `--entitlements - --xml`.
- Re-signing or overwriting the executable of a RUNNING game is unproven; CamDisplay queues it for
  after the game exits.
- **Re-signing loses Input Monitoring, and with it async input** (seen 2026-11-06): the ad-hoc
  re-signed game, launched as its own responsible process, logs `SkyHookException: NOT_PERMITTED`
  in `~/Library/Logs/7th Beat Games/A Dance of Fire and Ice/Player.log` and falls back to Unity's
  keyboard (`RDInputType_Keyboard`; Topaz's limiter log shows `direct kc=` instead of
  `async label=`). That path drops every `SpecialKeys` press before counting a hit: Tab, F1-F12,
  End, Print, SysReq, both Alt, Command and Windows keys. The async path's `SpecialKeys` is only
  three labels (F12 among them), so Tab and F1-F11 hit fine there. Same table in the June 2026
  build. Fix: grant the game Input Monitoring again (Privacy & Security); an ad-hoc grant is per
  signature, so every re-sign or game update needs it redone.
- **A self-signed certificate keeps TCC grants across re-signs; ad-hoc cannot** (tested 2026-11-06
  on a probe app that opens an IOHIDManager the way SkyHook does). macOS stores a grant with the
  app's designated requirement: ad-hoc's is `cdhash H"…"`, new on every build, while a cert's is
  `identifier "…" and certificate leaf = H"…"`, the same for every build signed with it. Granted
  once, three different builds swapped in at the same path stayed granted; an ad-hoc build there
  got `NOT_PERMITTED`, and the cert-signed build was granted again after it. The cert needs no
  trust setting (`CSSMERR_TP_NOT_TRUSTED` signs and runs fine with hardened runtime), but
  `codesign` only finds an identity in a keychain on the user's search list, even with
  `--keychain`. **Verified on the game the same day:** CamDisplay's patch signs with a local
  "CamDisplay Local Signing" cert. Input Monitoring was granted once, then the game was re-signed
  with a changed Info.plist (new cdhash, same requirement), and it still hooked the keyboard with
  Tab and F-keys hitting. The Camera grant across a re-sign is still untested (the cam was off in
  that run), though it is the same mechanism. Re-signing identical content gives the identical
  cdhash. CamDisplay's DEBUG self-test re-patches (undo + apply) on every run; under ad
  hoc that silently dropped the grant each time.
- UMMInstall sets Steam's launch options to `"<game>/setup_helper.sh" %command%` (an arm64
  re-exec plus the MelonLoader `DYLD_INSERT_LIBRARIES`), not MelonLoader's own
  `melonloader-launch.sh`. Anything that wraps the launch goes in front of whichever is there.
- A player's macOS Screen Time limit can cover the game with a shield while it keeps running;
  never press "Ignore Limit" in automation.

## Testing without a human

- **DEBUG-only self-tests** that log PASS/FAIL and then call `Application.Quit()`: CamDisplay's
  `Dev/SelfTest.cs` (armed by a `selftest` file in its data folder) and PrismLib's
  `tools/LayoutProbe/AutoTest.cs` (an `autotest` file). Launch with `open steam://rungameid/977950`,
  wait for the DONE line, read the log.
- To see what the game did with the camera or other OS services: `/usr/bin/log show --predicate
  'processID == <pid>'`. In zsh, bare `log` is a shell builtin and prints nothing useful.
- `screencapture` raises a screen-recording consent prompt on recent macOS; unattended runs should
  rely on logs, not screenshots.
