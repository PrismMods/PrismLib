# PrismLib

The shared layer under [Bismuth](https://github.com/PrismMods/Bismuth),
[Sapphire](https://github.com/PrismMods/Sapphire) and Quartz — three ADOFAI mods by QuartzTeam
that, run together, fight over the same handful of globals.

Autoplay stops working. The editor camera stops following. A hotkey does two things or nothing.
None of that is a Harmony conflict a loader can detect: it is two mods writing `RDC.auto` every
frame, each certain it owns it. PrismLib is the arbiter — **claims** say who owns a piece of game
state, a **key registry** says who wants a hotkey, and a **settings schema** lets all three describe
their settings once so they can be rendered, searched and conflict-checked together.

It is deliberately not a framework the mods live inside. Every call degrades: a mod that cannot load
PrismLib, or is denied a claim, still works on its own.

## Installing

You don't. Any Prism mod carries [`bootstrap/PrismBootstrap.cs`](bootstrap/PrismBootstrap.cs), which
installs PrismLib beside the mod folders on first run, verifies the download's SHA-256 before it ever
loads it, and updates it in the background. Whichever Prism mod loads first wins; the rest find it
already in memory.

## Usage

Add `PrismBootstrap.cs` to the mod's `<Compile>` list and call it first thing in `OnLoad`:

```csharp
public static bool Load(UnityModManager.ModEntry entry)
{
    if (PrismLib.Bootstrap.PrismBootstrap.Ensure(entry.Logger.Log)) UsePrism();
    // …the mod's own setup, which must not depend on the line above
}

// Separate, non-inlined: the CLR resolves a method's types when that method is JITted, so PrismLib
// types may not appear in a method that could run before the resolver is installed.
[MethodImpl(MethodImplOptions.NoInlining)]
private static void UsePrism()
{
    var me = Prism.Register("Sapphire", typeof(MainClass).Assembly.GetName().Version);
    Prism.Log = SapphireLog.Log;
    _autoplay = me.ClaimState(StateKey.Autoplay, "editor autoplay toggle");
    if (_autoplay == null) { /* someone else owns it — leave RDC.auto alone */ }
}
```

**Two rules for the bridge class, both learned the hard way.** A PrismLib type may appear in a
method BODY, never in a FIELD: field types are part of the class layout and are resolved when the
type itself loads, before any method runs. A `private static ModHandle _me;` made the bridge
unloadable, so `Ensure()` never ran, so the library it would have installed stayed missing — and
UMM reported the whole mod as `OnToggle: TypeLoadException` and skipped it. Hold them as `object`
and cast at use. Second, no caller outside the bridge may mention a PrismLib type, and any bridge
method whose body touches one must be called behind `if (PrismBridge.Available)`.

`tools/check-bridge.sh <Mod.dll> <Namespace.PrismBridge>` verifies both on a built mod, by loading
it with PrismLib absent. Run it before shipping; the failure is invisible in-game.

### Claims

```csharp
using (var c = me.ClaimState(StateKey.EditorCamera, "camera path preview"))
    if (c != null) { /* write the camera */ }
```

One owner at a time, first come first served. A denied claim returns `null` and is **not** an error —
the caller carries on without touching that state, which is what a second editor suite should do
rather than fighting the first every frame. Claims describe intent, they do not enforce it: nothing
can stop a mod writing `RDC.auto` directly. What they buy is that a mod can ask, and that when a user
reports "autoplay is broken" the log names the owner instead of leaving three suspects.

`StateKey`: `Autoplay`, `NoFail`, `EditorCamera`, `PathLock`, `EditorHud`, `GamePanels`, `GameHud`,
`InputCapture`.

`InputCapture` is the one worth calling out. Bismuth blocks the keyboard game-wide while its panel
is open, so every Sapphire hotkey silently dies for as long as that panel is up — a mod cannot see
that from its own side. The holder takes the claim while it swallows keys; everyone else checks
`Claims.OwnerOf(StateKey.InputCapture)` once, in whatever function reads their hotkeys, and stands
down.

### Keys

```csharp
me.BindKey("quickchart.edit", (int)KeyCode.U, KeyMods.None, "Edit event parameters");
foreach (var other in Keys.Wanting((int)KeyCode.U, KeyMods.None, "Sapphire")) { /* warn */ }
```

Registration never blocks — two mods may hold one key, and the user might want that. It guarantees
only that the clash is visible, to the log and to a settings page.

### Settings

```csharp
me.DescribeSettings(new[] {
    new SettingEntry {
        Id = "editor.effectiveBpm", Kind = SettingKind.Bool,
        Label = "Effective BPM on the angle readout", Page = "Features", Group = "Editor",
        Keywords = new[] { "bpm", "tempo" },
        Get = () => Settings.Instance.effectiveBpm,
        Set = v => Settings.Instance.effectiveBpm = (bool)v,
    },
});
```

A mod declares what a setting *is* and how to read and write it; the mod keeps its own storage and
its own look. `Settings.Search("bpm")` then answers across every loaded mod — which is Quartz's real
problem, hundreds of settings and no way to find one.

## PrismLib.UI

A second assembly, for the parts of the interface the mods were copying between each other. It
references Unity; `PrismLib.dll` stays plain BCL so it can load before anything and be tested
outside the game.

**Mods ship it, the bootstrapper does not install it.** Claims need a single shared instance across
mods — UI does not. Two mods each drawing their own toast is correct, and `ToastStack` keeps them
from overlapping by GameObject name rather than shared memory. So it is an ordinary dependency
beside the mod's own DLL, which is what lets its call sites skip the `Available` guard that every
`PrismLib` call needs. `lib/update-prismlib.sh` fetches both; the mod's deploy and release copy
`PrismLib.UI.dll` into the mod folder.

```csharp
_toast = new Toast("Sapphire");              // canvas becomes "SapphireUpdateToastCanvas"
_toast.Theme.Accent = Theme.Accent;          // mutable, so a runtime re-skin follows
_toast.Set(new ToastContent {                // null hides; call it every tick
    Key = "avail:" + tag,                    // identity: dismissing this one doesn't suppress the next
    Title = "Update available: " + tag,
    Hint = "Click to update",
    AutoHide = true,
    OnClick = () => UpdateService.Install(),
});
_toast.Tick();
```

`Toast` owns the card — building, slotting, sliding, hover, the × that fades in on hover, the
progress bar, the dismiss-key bookkeeping that stops a timed-out card sliding straight back in. The
host owns the meaning and pushes it in as plain strings, which also keeps Unity objects on the main
thread when the update check runs on a worker.

`ToastStack` is the cross-mod convention: canvas `<Mod>UpdateToastCanvas`, card `UpdateToast`,
measured in screen pixels so a foreign canvas with a different scaler still reports the box it
covers. Quartz already follows it, so its toast and ours stack instead of overlapping.

## Status

Stage 1, arbitration (claims, keys, settings schema): done, adopted by Sapphire and Bismuth. Quartz
is waiting.

PrismLib.UI has the update toast. More of the shared interface follows the same rule: it belongs
here when both mods would otherwise keep their own copy, and it stays in the mod when it needs that
mod's own state. A shared settings *renderer* is deliberately not planned — Quartz already generates
settings pages from a settings class, and a third one would be the duplication this is meant to
remove.

## Licence

MIT — see [LICENSE](LICENSE). The mods that link it are GPL-3.0; a permissive library keeps that
choice theirs.
