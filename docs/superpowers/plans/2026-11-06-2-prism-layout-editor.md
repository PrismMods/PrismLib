# PrismLib Shared Layout Editor — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A cross-mod "move / resize / rotate / anchor / crop things on screen" editor in PrismLib. It behaves like Quartz's repositioning menu, is built on UI Toolkit, and shows every Prism mod's registered elements in one session.

**Architecture:**
- **`PrismLib.dll`:** gets a registry (`Layout`, `LayoutTarget`, `ModHandle.DescribeLayout`) of plain-BCL delegates, plus a new `StateKey.LayoutEditor` claim.
- **`PrismLib.UI.dll`:** gets the editor (`LayoutEditor`, fed `EditTarget`s). It cannot reference `PrismLib.dll`, so each mod's bridge converts. Pure geometry lives in `LayoutMath.cs`, which is BCL-only and compiled straight into the offline test.

**Tech Stack:** C# (.NET 4.8, xbuild), Unity UI Toolkit (Unity 2022 and Unity 6), `mcs`/`mono` for the offline test.

**Spec:** `PrismLib/docs/superpowers/specs/2026-11-06-camdisplay-design.md`, "Part 1 — Shared layout editor". Behavior reference: Quartz `Quartz/Quartz/Quartz/UI/Utility/Reorganizer*.cs`, `ReorganizeHandle.Frame.cs` and `ReorganizeHistory.cs`. Port the behavior, not the uGUI code.

**Prerequisite:** Plan 1 passed (see the spec's "Gate result").

## Global Constraints

- `PrismLib.dll` stays plain BCL: no Unity, Harmony or game types. `PrismLib.UI` never references `PrismLib.dll`.
- **Coordinates everywhere in the API:** screen pixels, origin **bottom-left** (Unity `Screen`/`Input.mousePosition` space). Rotation is in degrees, counter-clockwise.
- **Anchor index:** 0–8 in reading order from the top-left: `0` TL, `1` TC, `2` TR, `3` ML, `4` C, `5` MR, `6` BL, `7` BC, `8` BR.
- **Crop:** `float[4] { left, bottom, right, top }`, the fraction of the source trimmed from each edge, each in 0..1.
- **Snap threshold:** `8 px × screenHeight / 1080` (Quartz's value). **Rotation snap:** to multiples of 90° within 5°.
- Never show or hide a UI Toolkit panel with `SetActive`; use `display` (`PrismLib/CLAUDE.md` landmine).
- Unity 2022 font inheritance: rely on `Surface` (`HandDownFonts`).
- Append `LayoutEditor` at the **end** of `StateKey`, so the existing values keep their numbers.
- Commits follow `agents/commits.md`: split finely, **no `Co-Authored-By`**. A public-API commit names the bridge methods that must follow in its body. Never push unless asked.
- Build and check: `./dev.sh` (xbuild + offline test + install). **The user decides the release version.**

## Review Focus

- **A target's mod unloads mid-session:** the delegates throw or return null. The editor must drop that frame and keep running, never throw every frame. *Test: Task 1, `a target whose GetBox throws is skipped`.*
- **Crop dragged past the opposite edge:** at least 5% of the source must stay visible on each axis. *Test: Task 2, `ClampCrop` cases.*
- **Screen resized or fullscreen toggled while the editor is open:** frames must follow on the next `Tick`, because boxes are re-read from `GetBox` every tick and never cached.
- **Two mods register the same Id:** Ids are scoped per owner, so both survive. *Test: Task 1.*
- **Editor opened with zero targets:** it shows the empty state ("Nothing to arrange yet") and a Done button, with no exceptions.

---

### Task 1: Registry and claim in `PrismLib.dll`

**Files:**
- Create: `PrismLib/PrismLib/Layout.cs`
- Modify: `PrismLib/PrismLib/Claims.cs` (append `LayoutEditor` to `StateKey`)
- Modify: `PrismLib/PrismLib/Prism.cs` (`ModHandle.DescribeLayout`)
- Modify: `PrismLib/PrismLib/PrismLib.csproj` (`<Compile Include="Layout.cs" />`)
- Test: `PrismLib/tools/PrismLibTest.cs` (new `layout` section)

**Interfaces:**
- Produces, in namespace `PrismLib`:

```csharp
public sealed class LayoutTarget {
    public string Id, Label;                       // Id stable within the mod ("cam"); Label shown on the frame tag
    public Func<float[]> GetBox;                   // {centerX, centerY, width, height}, visible size before rotation
    public Action<float, float> MoveTo;            // put the box centre at this screen point
    public Func<float> GetScale; public Action<float> SetScale;
    public float MinScale = 0.1f, MaxScale = 4f;
    public Func<float> GetRotation; public Action<float> SetRotation;          // optional pair
    public Func<int> GetAnchor; public Action<int> SetAnchor;                  // optional; SetAnchor keeps the on-screen position
    public Func<bool> GetAnchorClosest; public Action<bool> SetAnchorClosest;  // optional
    public Func<float[]> GetCrop; public Action<float[]> SetCrop;              // optional
}
public static class Layout {
    public static event Action Changed;            // any mod's target list changed
    public static event Action OpenRequested;      // someone asked for the editor; the LayoutEditor claim holder opens it
    public static IEnumerable<KeyValuePair<string, LayoutTarget>> All();   // owner id → target, registration order
    public static void Unregister(string owner);
    public static void RequestOpen();
}
// ModHandle:
public void DescribeLayout(IEnumerable<LayoutTarget> targets);   // REPLACES this mod's list, like DescribeSettings
// StateKey: ..., DebugPanel, LayoutEditor
```

- [ ] **Step 1: Write the failing tests** (append a `layout` section in `Main()` before the summary)

```csharp
Console.WriteLine("layout");
Func<float[]> box = () => new float[] { 100, 100, 50, 30 };
Func<string, LayoutTarget> T = id => new LayoutTarget { Id = id, Label = id, GetBox = box, MoveTo = (x, y) => { }, GetScale = () => 1f, SetScale = s => { } };
int changed = 0; Layout.Changed += () => changed++;
a.DescribeLayout(new[] { T("cam"), T("hud") });
b.DescribeLayout(new[] { T("cam") });
var ids = new List<string>(); foreach (var kv in Layout.All()) ids.Add(kv.Key + ":" + kv.Value.Id);
Check(string.Join(",", ids) == "Sapphire:cam,Sapphire:hud,Topaz:cam", "targets list in registration order, Ids scoped per owner");
Check(changed == 2, "each DescribeLayout raises Changed once");
a.DescribeLayout(new[] { T("cam") });
ids.Clear(); foreach (var kv in Layout.All()) ids.Add(kv.Key + ":" + kv.Value.Id);
Check(string.Join(",", ids) == "Topaz:cam,Sapphire:cam", "a second call replaces the mod's list");
a.DescribeLayout(new[] { new LayoutTarget { Id = "x", GetBox = box }, new LayoutTarget { Id = "", GetBox = box, MoveTo = (x, y) => { }, GetScale = () => 1, SetScale = s => { } } });
bool none = true; foreach (var kv in Layout.All()) if (kv.Key == "Sapphire") none = false;
Check(none, "targets missing Id, GetBox, MoveTo, GetScale or SetScale are skipped");
Layout.Unregister("Topaz");
bool empty = true; foreach (var kv in Layout.All()) empty = false;
Check(empty, "Unregister removes the owner's targets");
int opens = 0; Layout.OpenRequested += () => opens++; Layout.RequestOpen();
Check(opens == 1, "RequestOpen raises OpenRequested");
var lc = a.ClaimState(StateKey.LayoutEditor, "editor");
Check(lc != null && b.ClaimState(StateKey.LayoutEditor, "editor") == null, "LayoutEditor is an ordinary first-come claim");
lc.Release();
Check((int)StateKey.LayoutEditor == (int)StateKey.DebugPanel + 1, "LayoutEditor is appended after DebugPanel");
```

- [ ] **Step 2: Run and confirm the failure**

Run: `./dev.sh`
Expected: build FAIL with `The type or namespace name 'LayoutTarget' could not be found`.

- [ ] **Step 3: Implement `Layout.cs`, the `StateKey` addition and `ModHandle.DescribeLayout`**

Mirror `Settings.Register`:
- one lock;
- `RemoveAll` by owner, then add the valid targets;
- `Prism.Log("PrismLib: <id> described N layout target(s)")`;
- raise `Changed` outside the lock, wrapped in try/catch.

`All()` returns a snapshot list taken under the lock. A delegate that throws isn't the registry's problem; the editor guards each call.

- [ ] **Step 4: Run the tests**

Run: `./dev.sh`
Expected: the `layout` lines are all `ok`, and the final line reports 0 failures.

- [ ] **Step 5: Commit**

```bash
git add PrismLib/Layout.cs PrismLib/Claims.cs PrismLib/Prism.cs PrismLib/PrismLib.csproj tools/PrismLibTest.cs
git commit -m "feat: add the shared layout target registry and LayoutEditor claim" -m "New public API: ModHandle.DescribeLayout, Layout.All/RequestOpen/OpenRequested, StateKey.LayoutEditor. No existing bridge method has to change."
```

### Task 2: Pure layout geometry (`LayoutMath`)

**Files:**
- Create: `PrismLib/PrismLib.UI/Toolkit/LayoutMath.cs` (`internal static class LayoutMath`, BCL only, **no `using UnityEngine`**)
- Modify: `PrismLib/PrismLib.UI/PrismLib.UI.csproj` (`<Compile Include="Toolkit\LayoutMath.cs" />`)
- Modify: `PrismLib/dev.sh:45` and `PrismLib/release.sh:40`: compile the test with `tools/PrismLibTest.cs PrismLib.UI/Toolkit/LayoutMath.cs`
- Test: `PrismLib/tools/PrismLibTest.cs` (new `layout math` section)

**Interfaces:**
- Produces (internal, namespace `PrismLib.UI.Toolkit`):

```csharp
internal static class LayoutMath {
    public static float SnapThreshold(float screenH);                              // 8 * screenH / 1080
    public static float Snap(float value, float[] guides, float threshold);       // nearest guide within threshold, else value
    public static int ClosestAnchor(float cx, float cy, float screenW, float screenH); // by thirds; row 0 is the TOP third
    public static float[] AnchorPoint(int anchor, float screenW, float screenH);   // {x, y}, bottom-left origin
    public static float SnapRotation(float degrees);                               // to k*90 within 5°, result in [0, 360)
    public static float[] ClampCrop(float[] crop);                                 // each in [0,1]; left+right ≤ 0.95, bottom+top ≤ 0.95
}
```

- [ ] **Step 1: Write the failing tests**

```csharp
Console.WriteLine("layout math");
Check(LayoutMath.SnapThreshold(2160) == 16f, "threshold scales with screen height");
Check(LayoutMath.Snap(103, new float[] { 0, 100, 960 }, 8) == 100, "snaps to a guide within the threshold");
Check(LayoutMath.Snap(109, new float[] { 0, 100, 960 }, 8) == 109, "leaves a value outside the threshold alone");
Check(LayoutMath.ClosestAnchor(1800, 100, 1920, 1080) == 8, "bottom-right third is anchor 8");
Check(LayoutMath.ClosestAnchor(960, 540, 1920, 1080) == 4, "middle is anchor 4");
Check(LayoutMath.ClosestAnchor(10, 1070, 1920, 1080) == 0, "top-left third is anchor 0");
var p = LayoutMath.AnchorPoint(2, 1920, 1080); Check(p[0] == 1920 && p[1] == 1080, "anchor 2 is the top-right corner");
p = LayoutMath.AnchorPoint(7, 1920, 1080); Check(p[0] == 960 && p[1] == 0, "anchor 7 is the bottom centre");
Check(LayoutMath.SnapRotation(87) == 90 && LayoutMath.SnapRotation(-3) == 0 && LayoutMath.SnapRotation(184) == 180, "rotation snaps within 5 degrees of a quarter turn");
Check(LayoutMath.SnapRotation(80) == 80 && LayoutMath.SnapRotation(-90) == 270, "outside the window it is kept; results normalise into [0, 360)");
var c = LayoutMath.ClampCrop(new float[] { 0.7f, -0.2f, 0.6f, 0.1f });
Check(Math.Abs(c[0] - 0.7f) < 1e-5 && Math.Abs(c[2] - 0.25f) < 1e-5, "left kept, right clamped so 5% stays visible");
Check(c[1] == 0f && Math.Abs(c[3] - 0.1f) < 1e-5, "negative crop clamps to 0");
```

Add `using PrismLib.UI.Toolkit;` at the top of `PrismLibTest.cs`.

- [ ] **Step 2: Run and confirm the failure**

Run: `./dev.sh`
Expected: FAIL. `LayoutMath` doesn't exist yet, so `mcs` reports `CS0103: The name 'LayoutMath' does not exist`.

- [ ] **Step 3: Implement `LayoutMath`** with the signatures and rules above. `ClampCrop` clamps left first, then right to `0.95 - left`; bottom first, then top to `0.95 - bottom`.

- [ ] **Step 4: Run the tests**

Run: `./dev.sh`
Expected: all `layout math` lines are `ok`, and both assemblies build.

- [ ] **Step 5: Commit**

```bash
git add PrismLib.UI/Toolkit/LayoutMath.cs PrismLib.UI/PrismLib.UI.csproj dev.sh release.sh tools/PrismLibTest.cs
git commit -m "feat: add pure layout geometry for the shared editor"
```

### Task 3: Editor shell — frames, selection, drag with snapping, Done

**Files:**
- Create: `PrismLib/PrismLib.UI/Toolkit/EditTarget.cs`
- Create: `PrismLib/PrismLib.UI/Toolkit/LayoutEditor.cs`
- Modify: `PrismLib/PrismLib.UI/PrismLib.UI.csproj`
- Create: `PrismLib/tools/LayoutProbe/LayoutProbe.cs`, `Info.json`, `build.sh` (a UMM dev mod for in-game checks)

**Interfaces:**
- Consumes: `LayoutMath` (Task 2).
- Produces (namespace `PrismLib.UI.Toolkit`):

```csharp
public sealed class EditTarget {        // field-for-field copy of PrismLib.LayoutTarget, plus Owner
    public string Owner, Id, Label;
    /* same delegate and MinScale/MaxScale fields, same names and types as LayoutTarget */
}
public sealed class LayoutEditor : IDisposable {
    public LayoutEditor(Func<IEnumerable<EditTarget>> source, UnityEngine.TextCore.Text.FontAsset font = null);
    public bool Visible { get; set; }   // display-based; opening re-reads source(); closing raises Closed
    public void Toggle();
    public void Tick();                  // host calls every frame; re-reads every GetBox, never caches boxes
    public event Action Closed;
    public void Dispose();
}
```

- `LayoutProbe` (UMM, dev only):
  - registers two uGUI boxes as `EditTarget`s: "Probe A" with every optional delegate, "Probe B" with only the required ones;
  - holds the boxes' state in fields, so anchor, scale, rotation and crop all apply visibly;
  - F9 toggles a `LayoutEditor`;
  - the box state is logged on `Closed`.

Behavior to build (from the spec):
- **Surface:** full-screen, `Layer.Modal`, `BlocksGameInput = true`.
- **Frames:** each target draws a faint outline and a name tag above it. The tag moves below the outline when the box touches the top edge.
- **Select:** the selected target gets a solid outline.
- **Overlaps:** a press on overlapping frames keeps the currently selected one. A click without movement selects the next frame down the stack.
- **Drag:** dragging calls `MoveTo` with the centre snapped by `LayoutMath.Snap` to the guides `{0, W/2, W}` / `{0, H/2, H}`, tested on the box's edges and centre.
- **Done:** a floating Done pill sets `Visible = false`.
- **Errors:** every delegate call goes through one guard. A throwing target is skipped for that tick and logged once per target via `Ui.Log`.
- **Empty state:** with zero targets the editor shows "Nothing to arrange yet" plus Done.

- [ ] **Step 1: Build the probe against the current DLLs, then add the editor**

Run: `bash tools/LayoutProbe/build.sh`. It runs `mcs` against the game's `Managed/`, this repo's `PrismLib.UI/bin/Release/PrismLib.UI.dll` and `UserLibs/UnityModManager.dll`, then installs `LayoutProbe.dll`, `Info.json` and a copy of `PrismLib.UI.dll` into `UMMMods/LayoutProbe/`.
Expected: before `LayoutEditor` exists, the build fails with `LayoutEditor could not be found`. Implement `EditTarget` and `LayoutEditor`, run `./dev.sh`, then build the probe again: it succeeds.

- [ ] **Step 2: In-game check (UMM reload, Ctrl+F10)**

Press F9. Expected, confirmed by screenshot:
- both frames show with name tags;
- dragging "Probe A" moves it and it snaps to the screen centre lines;
- with A dragged onto B, clicking without moving cycles the selection;
- Done closes the editor and the log shows the new positions;
- with the probe's targets cleared (probe key F10), F9 shows the empty state.

- [ ] **Step 3: Commit**

```bash
git add PrismLib.UI/Toolkit/EditTarget.cs PrismLib.UI/Toolkit/LayoutEditor.cs PrismLib.UI/PrismLib.UI.csproj tools/LayoutProbe
git commit -m "feat: add the layout editor shell with selection, snapping drag and Done"
```

### Task 4: Resize grips, rotation knob, anchor

**Files:**
- Create: `PrismLib/PrismLib.UI/Toolkit/LayoutEditor.Grips.cs` (`partial class LayoutEditor`; make Task 3's class `partial`)

**Behavior:**
- **Resize:** four corner grips on the selected target. Dragging scales uniformly through `SetScale`, by the ratio of the pointer's distance from the box centre, clamped to `[MinScale, MaxScale]`.
- **Rotate:** a knob above the top edge that rotates with the box, shown only if `GetRotation`/`SetRotation` are set. Dragging sets `SetRotation(LayoutMath.SnapRotation(angle))`.
- **Anchor:** when `GetAnchor` is set, draw the anchor point (`LayoutMath.AnchorPoint`) with a line to the box centre. While dragging, if `GetAnchorClosest()` is true, call `SetAnchor(LayoutMath.ClosestAnchor(centre))` whenever the third changes.

- [ ] **Step 1: In-game check with LayoutProbe**

Expected:
- the corner grip scales "Probe A" without stretching it;
- the knob rotates it and clicks into 0/90/180/270;
- dragging A across the screen moves its anchor line between thirds;
- "Probe B" shows no knob and no anchor line.

- [ ] **Step 2: Commit**

```bash
git add PrismLib.UI/Toolkit/LayoutEditor.Grips.cs PrismLib.UI/Toolkit/LayoutEditor.cs PrismLib.UI/PrismLib.UI.csproj
git commit -m "feat: add resize grips, a snapping rotation knob and anchors to the layout editor"
```

### Task 5: Inspector fields and undo/redo

**Files:**
- Create: `PrismLib/PrismLib.UI/Toolkit/LayoutEditor.Inspector.cs`

**Behavior:**
- **Inspector:** a small panel docked opposite the selected box, built from `Widgets`, with these fields:
  - X / Y: centre in px;
  - Size: scale;
  - Rotation: only if supported;
  - Anchor: a nine-way `Choice` plus "Closest" as the tenth option, only if supported.
  
  Fields re-read every `Tick`, so they stay in step with dragging. Typing applies on commit.
- **Undo/redo:** each gesture (a drag, grip, knob or field commit) records **one** `History.Record(label, key: Owner+"/"+Id, undo, redo)`. The undo and redo actions restore a snapshot `{centre, scale, rotation, anchor, closest, crop}` through the setters.
- **Keys:** Cmd/Ctrl+Z undoes and Cmd/Ctrl+Shift+Z redoes while the editor is open.

- [ ] **Step 1: In-game check with LayoutProbe**

Expected:
- typing X=960 centres "Probe A";
- dragging, resizing and rotating, then pressing Ctrl+Z three times, restores each step in reverse;
- redo replays them;
- the inspector values track a drag live.

- [ ] **Step 2: Commit**

```bash
git add PrismLib.UI/Toolkit/LayoutEditor.Inspector.cs PrismLib.UI/PrismLib.UI.csproj
git commit -m "feat: add the layout editor inspector and undo/redo"
```

### Task 6: Crop mode

**Files:**
- Create: `PrismLib/PrismLib.UI/Toolkit/LayoutEditor.Crop.cs`

**Behavior:**
- **Entry:** a "Crop" toggle in the inspector, shown only when `GetCrop`/`SetCrop` are set.
- **Ghost:** in crop mode, the full uncropped source rect is drawn as a dim ghost, derived from the box and the current crop.
- **Edges:** four edge handles. Dragging an edge sets that side's fraction, passed through `LayoutMath.ClampCrop`.
- **Fields:** inspector fields for left/bottom/right/top, in %.
- **Undo:** one `History` entry per gesture.
- **Exit:** leaving crop mode returns to normal editing.

- [ ] **Step 1: In-game check with LayoutProbe**

Expected:
- dragging the right edge of "Probe A" inward trims it, and the ghost stays put;
- an edge stops when 5% remains visible;
- typing 25% left trims a quarter;
- Ctrl+Z restores the crop;
- "Probe B" has no Crop toggle.

- [ ] **Step 2: Commit**

```bash
git add PrismLib.UI/Toolkit/LayoutEditor.Crop.cs PrismLib.UI/PrismLib.UI.csproj
git commit -m "feat: add crop mode to the layout editor"
```

### Task 7: Document the editor and cut the release

**Files:**
- Modify: `PrismLib/CLAUDE.md`. Add a "Layout editor" section covering:
  - the registry vs UI split;
  - coordinates, anchor indexes and crop order (copied from Global Constraints);
  - that the `LayoutEditor` claim holder hosts the editor and listens to `Layout.OpenRequested`;
  - that bridges convert `LayoutTarget` → `EditTarget` field by field;
  - that `tools/LayoutProbe` is the in-game test rig.

- [ ] **Step 1: Write the docs and run** `./release.sh` (no version)
Expected: both projects build, the offline test passes, and the bootstrap compile check passes.

- [ ] **Step 2: Commit the docs**

```bash
git add CLAUDE.md && git commit -m "docs: document the shared layout editor"
```

- [ ] **Step 3: Release — ask the user for the version first** (the current version is 0.3.12, and PrismLib steps by 0.0.1).

Run: `./release.sh <ver>`, then `git add -A && git commit -m "release: <ver>"`.

Uploading both DLLs to the `v<ver>` GitHub release and pushing happen **only when the user asks**. CamDisplay's release (Plan 3) needs this one published first.
