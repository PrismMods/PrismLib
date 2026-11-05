# macOS Steam-Launch Camera Gate — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Prove or disprove that, on macOS, ADOFAI can use a webcam when launched normally through Steam, using a re-signed game plus a launcher that disclaims Steam's responsibility for the process.

**Architecture:**
- A tiny C launcher (`CamDisplayLaunch`) `posix_spawn`s its arguments with responsibility disclaimed, waits, and passes the exit status through.
- A shell script re-signs the game with the camera entitlement and can undo it.
- The throwaway camera probe from the 2026-11-06 spike, kept as a dev tool, reports whether frames arrive.

The result is a go/no-go recorded in the spec. **macOS blocks CamDisplay's release, so this plan runs first.**

**Tech Stack:** C (clang, universal arm64 + x86_64), bash, `codesign`/`plutil`, a UMM probe mod compiled with `mcs`.

**Spec:** `PrismLib/docs/superpowers/specs/2026-11-06-camdisplay-design.md`, sections "macOS camera access" and "Chosen route".

## Global Constraints

- The game is at `~/Library/Application Support/Steam/steamapps/common/A Dance of Fire and Ice`. The app is `ADanceOfFireAndIce.app` (bundle id `com.7thbeat.adofai`) and the Steam app id is `977950`.
- Preserve the stock entitlements; read them at runtime, never hard-code them. On 3.4.0 they are `allow-dyld-environment-variables`, `allow-unsigned-executable-memory`, `disable-executable-page-protection`, `disable-library-validation` and `network.client`. Add only `com.apple.security.device.camera`.
- Re-sign the main bundle only: `codesign --force --sign - --options runtime --entitlements <file> <app>`. No `--deep`.
- Usage string: `NSCameraUsageDescription` = `CamDisplay shows your hand cam in-game.`
- Commits follow `agents/commits.md`: Conventional Commits, split finely, **no `Co-Authored-By` trailer**. Commit only; never push unless the user asks.
- Changing the Steam Launch Options is the **user's** action. Ask them, and record the old value first.

## Review Focus

- **Launcher gets a script, not a binary:** `melonloader-launch.sh` is a `#!` script. `posix_spawn` must run it, so test with a script argument.
- **Exit status:** Steam reads the game's exit. The launcher must return the child's status and the signal-adjusted code (`128 + signo`).
- **Signals:** Steam's "Stop" sends SIGTERM to the process it started, which is the launcher. Forward SIGTERM/SIGINT/SIGHUP to the child, or the game keeps running after Stop.
- **Symbol missing:** on an older macOS without `responsibility_spawnattrs_setdisclaim`, the launcher must still launch the game and print a warning to stderr, not fail.
- **Patch run twice:** applying the patch to an already-patched app must not stack a second backup over the original. Keep the first backup.

---

### Task 1: CamDisplay repo + responsibility-disclaiming launcher

**Files:**
- Create: `CamDisplay/.gitignore`, `CamDisplay/LICENSE` (copy of `Topaz/LICENSE`, GPL-3.0), `CamDisplay/agents/commits.md` (copy of `PrismLib/agents/commits.md` with its **This repo** section replaced, see Step 1)
- Create: `CamDisplay/native/launch/launch.c`
- Create: `CamDisplay/native/launch/build.sh`
- Test: `CamDisplay/native/launch/test.sh`

**Interfaces:**
- Produces: `CamDisplay/native/launch/CamDisplayLaunch`, a universal ad-hoc-signed binary. Usage: `CamDisplayLaunch <program> [args...]`. Its exit status is the child's. Later tasks and Plan 3 embed this binary.

- [ ] **Step 1: Create the repo**

```bash
mkdir -p /Users/preluminance/Coding/PrismMods/CamDisplay && cd $_ && git init -b main
cp ../Topaz/LICENSE LICENSE
printf 'bin/\nobj/\n*.zip\nnative/launch/CamDisplayLaunch\n' > .gitignore
mkdir -p agents && cp ../PrismLib/agents/commits.md agents/commits.md
```

Replace the text under `## This repo` in `agents/commits.md` with:

`**Split finely**, like PrismLib. Releases: \`./release.sh <ver>\` → \`release: <ver>\`; the user decides versions.`

- [ ] **Step 2: Write the failing test** `native/launch/test.sh`

```bash
#!/bin/bash
set -u; cd "$(dirname "$0")"; L=./CamDisplayLaunch; fail=0
check() { if [ "$1" = "$2" ]; then echo "  ok   $3"; else echo "  FAIL $3 (got '$1', want '$2')"; fail=1; fi; }
check "$($L /bin/echo a 'b c')" "a b c" "passes arguments through unchanged"
$L /bin/sh -c 'exit 7'; check "$?" "7" "returns the child's exit code"
printf '#!/bin/sh\necho script-ran\n' > /tmp/cdl-script.sh; chmod +x /tmp/cdl-script.sh
check "$($L /tmp/cdl-script.sh)" "script-ran" "runs a #! script"
$L /bin/sh -c 'kill -TERM $$'; check "$?" "143" "reports a signalled child as 128+signo"
$L /bin/sleep 30 & lp=$!; sleep 0.5; kill -TERM $lp; wait $lp; check "$?" "143" "forwards SIGTERM to the child"
pgrep -f '^/bin/sleep 30$' >/dev/null && { echo "  FAIL child survived SIGTERM"; fail=1; } || echo "  ok   child gone after SIGTERM"
$L /nonexistent 2>/dev/null; check "$?" "127" "a missing program exits 127"
exit $fail
```

- [ ] **Step 3: Run the test and confirm it fails**

Run: `bash native/launch/test.sh`
Expected: FAIL. `./CamDisplayLaunch` does not exist.

- [ ] **Step 4: Implement the launcher in `native/launch/launch.c` and the build in `native/launch/build.sh`**

`main(argc, argv)`:
- Usage error: with `argc < 2`, print usage and exit 64.
- Disclaim: `posix_spawnattr_init`. Look up `int responsibility_spawnattrs_setdisclaim(posix_spawnattr_t*, int)` with `dlsym(RTLD_DEFAULT, …)`. If it's found, call it with `1`; otherwise print `CamDisplayLaunch: responsibility disclaim unavailable; camera may be denied` to stderr and continue.
- Spawn: `posix_spawn(&pid, argv[1], NULL, &attr, argv + 1, environ)`. On failure, print `strerror` and exit 127.
- Signals: install handlers for SIGTERM, SIGINT and SIGHUP that `kill(pid, sig)`.
- Wait: `waitpid`, retrying on `EINTR`.
- Exit: `WEXITSTATUS` for a normal exit, `128 + WTERMSIG` for a signalled one.

`build.sh`: `clang -arch arm64 -arch x86_64 -O2 -Wall -o CamDisplayLaunch launch.c && codesign --force --sign - CamDisplayLaunch`.

- [ ] **Step 5: Build and run the test**

Run: `bash native/launch/build.sh && bash native/launch/test.sh`
Expected: every line `ok`, exit 0. `lipo -archs native/launch/CamDisplayLaunch` prints `x86_64 arm64`.

- [ ] **Step 6: Commit**

```bash
git add .gitignore LICENSE agents/commits.md native/launch/launch.c native/launch/build.sh native/launch/test.sh
git commit -m "feat: add a launcher that disclaims Steam's responsibility for the game"
```

### Task 2: Camera patch script + probe dev tool

**Files:**
- Create: `CamDisplay/tools/mac-camera-patch.sh`
- Create: `CamDisplay/tools/CamProbe/CamProbe.cs`, `CamDisplay/tools/CamProbe/Info.json`, `CamDisplay/tools/CamProbe/build.sh`
  - Source for the probe: the spike at `/private/tmp/claude-501/-Users-preluminance-Coding-PrismMods/d60ffb24-3ea6-4ae2-ad4a-0a18d6b9a4a7/scratchpad/camspike/`. Copy `CamSpike.cs` and `Info.json`, renaming `CamSpike` to `CamProbe` throughout. If the scratchpad is gone, rewrite it from the description in Step 3.

**Interfaces:**
- Produces: `tools/mac-camera-patch.sh apply|undo|status [app-path]`. The default app path is the Global Constraints path. Backups go to `~/Library/Application Support/CamDisplay/sig-backup/`.
- Produces: `tools/CamProbe/build.sh [--install]`, which builds `CamProbe.dll` and with `--install` copies it plus `Info.json` to `<game>/UMMMods/CamProbe/`. The probe logs to `<game>/UMMMods/CamProbe/probe.log` with lines matching `hasAuth=(True|False)` and `size=WxH updates=N`.

- [ ] **Step 1: Write the patch script** `tools/mac-camera-patch.sh`

- `status`: print `patched` or `stock`. Patched means `codesign -d --entitlements - <app>` contains `com.apple.security.device.camera` **and** `plutil -extract NSCameraUsageDescription raw <app>/Contents/Info.plist` succeeds. Exit 0.
- `apply`, steps in order:
  1. If `status` is already `patched`, print `already patched` and exit 0.
  2. If no backup exists, copy `Contents/Info.plist`, `Contents/_CodeSignature/` and `Contents/MacOS/<CFBundleExecutable>` into the backup dir. Keep the first backup forever.
  3. Write the current entitlements to a temp plist and add `com.apple.security.device.camera` (bool true) with `plutil -insert`.
  4. Insert the usage string with `plutil -insert NSCameraUsageDescription -string …`.
  5. Re-sign per Global Constraints.
  6. Run `codesign --verify`.
- `undo`: copy the three backed-up items back, run `codesign --verify`, and print the `Authority=` line. With no backup, print `no backup; use Steam > Verify integrity` and exit 1.

- [ ] **Step 2: Check the script against the real game (with the game closed)**

Run: `tools/mac-camera-patch.sh status` → `stock`
Run: `tools/mac-camera-patch.sh apply && tools/mac-camera-patch.sh status` → `patched`
Run: `tools/mac-camera-patch.sh apply` → `already patched`
Run: `tools/mac-camera-patch.sh undo && codesign -dvv "<app>" 2>&1 | grep 'Authority=Developer ID Application'`
Expected: `Authority=Developer ID Application: 7th Beat Games S.A.C. (V44H323E4G)`

- [ ] **Step 3: Add the probe**

Copy the spike into `tools/CamProbe/`, renaming the namespace, class, log file and `Info.json` Id to `CamProbe`.

`build.sh` runs:

```bash
mcs -target:library -out:CamProbe.dll -nostdlib -r:"$M/mscorlib.dll" -r:"$M/System.dll" -r:"$M/System.Core.dll" -r:"$M/netstandard.dll" -r:"$M/UnityEngine.CoreModule.dll" -r:"$M/UnityEngine.AudioModule.dll" -r:"$M/UnityEngine.IMGUIModule.dll" -r:"$G/UserLibs/UnityModManager.dll" CamProbe.cs
```

- `$G` is the game dir and `$M` is `$G/ADanceOfFireAndIce.app/Contents/Resources/Data/Managed`.
- `--install` copies `CamProbe.dll` and `Info.json` into `$G/UMMMods/CamProbe/` and deletes any old `probe.log`.

The probe behaves exactly as the spike did:
- log `hasAuth` and the devices;
- call `RequestUserAuthorization(WebCam)`;
- on completion, `Play` the first device at 1280×720 @ 30;
- every 120 frames, 12 times, log `isPlaying`, `size`, `updates` and the center pixel;
- draw the texture top-left in `OnGUI`.

Run: `bash tools/CamProbe/build.sh`
Expected: `CamProbe.dll` builds with no errors.

- [ ] **Step 4: Commit**

```bash
git add tools/mac-camera-patch.sh tools/CamProbe
git commit -m "chore: add the macOS camera patch script and the camera probe dev tool"
```

### Task 3: Run the gate through a real Steam launch

No code. This task produces the go/no-go.

- [ ] **Step 1: Prepare the rig (game closed)**

```bash
tools/mac-camera-patch.sh apply
cp native/launch/CamDisplayLaunch "$G/CamDisplayLaunch" && codesign --force --sign - "$G/CamDisplayLaunch"
bash tools/CamProbe/build.sh --install
```

- [ ] **Step 2: Ask the user to change the Steam Launch Options**

Steam › ADOFAI › Properties › Launch Options. Ask the user to first paste the current value back to you, so it can be restored. Then have them set:

```
"<G>/CamDisplayLaunch" "<G>/melonloader-launch.sh" %command%
```

Fill in `<G>` with the absolute game path, quoted.

- [ ] **Step 3: Launch through Steam and collect the evidence**

Run: `open steam://rungameid/977950`. Ask the user to click **Allow** on the camera prompt. Wait until `probe.log` has 5 `size=` lines.

Expected, all of these:
- **Camera works:** `probe.log` has `hasAuth=True`, `size=1280x720`, and `updates=` rising between lines.
- **MelonLoader injected:** `MelonLoader/Latest.log` has fresh `[UMMBridge]` lines with today's timestamps.
- **Process tree:** for the game PID, `ps -o pid=,ppid=,comm=` shows the parent is `CamDisplayLaunch`, whose parent is `steam_osx`.
- **Steam tracks the game:** ask the user to confirm the Steam library shows ADOFAI as running (a Stop button) and that Shift+Tab opens the overlay in-game.
- **Stop works:** ask the user to press Stop in Steam. The game exits and `pgrep -f ADanceOfFireAndIce.app/Contents/MacOS` is empty within 10 s.

- [ ] **Step 4: Record the result in the spec**

Append a `### Gate result (YYYY-MM-DD)` section under "Chosen route" in the spec. It lists each expected item above as pass or fail, with the evidence line.

- **Any fail:** stop here and bring it to the user. Per the spec, macOS blocks the release, and the helper-app route is the alternative. Do not start Plan 2 or Plan 3 until the user decides.
- **All pass:** leave the rig patched, with the launcher and launch options in place, because CamDisplay development needs a working camera. Remove the probe: `rm -r "$G/UMMMods/CamProbe"`.

- [ ] **Step 5: Commit the spec update (PrismLib repo)**

```bash
cd ../PrismLib && git add docs/superpowers/specs/2026-11-06-camdisplay-design.md && git commit -m "docs: record the macOS Steam-launch camera gate result"
```
