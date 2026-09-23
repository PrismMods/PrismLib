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

## Status

Stage 1: arbitration (claims, keys, settings schema). The shared settings renderer is stage 2, and
adoption goes Sapphire first, then Quartz, then Bismuth.

## Licence

MIT — see [LICENSE](LICENSE). The mods that link it are GPL-3.0; a permissive library keeps that
choice theirs.
