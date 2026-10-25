# PrismLib

Shared library for the ADOFAI mods [Bismuth](https://github.com/PrismMods/Bismuth),
[Sapphire](https://github.com/PrismMods/Sapphire) and Quartz.

When several mods are installed together, they can write the same game state — `RDC.auto`, the
editor camera, HUD visibility — or bind the same keys. Mod loaders do not detect this kind of
conflict. PrismLib gives the mods a common place to coordinate:

- **Claims** record which mod owns a piece of game state.
- **The key registry** records which mods want a hotkey, so clashes can be reported.
- **The settings schema** lets each mod describe its settings so they can be rendered and searched
  together.

PrismLib is optional for the mods that use it. If it cannot be loaded, or a claim is denied, a mod
continues to work on its own.

## Installation

End users do not install PrismLib directly. Each Prism mod includes
[`bootstrap/PrismBootstrap.cs`](bootstrap/PrismBootstrap.cs), which on first run downloads
`PrismLib.dll` into a shared folder beside the mod folders and verifies its SHA-256 against the
release feed before loading it. Newer versions are downloaded in the background and used from the
next launch. If several Prism mods are installed, the first one to load installs the library and the
others use the copy already in memory.

## Usage

Add `PrismBootstrap.cs` to the mod's `<Compile>` list and call it at the start of `OnLoad`:

```csharp
public static bool Load(UnityModManager.ModEntry entry)
{
    // Before any code that refers to a PrismLib.UI type: only one copy of PrismLib.UI loads per
    // session, so bring every mod folder's copy up to the newest version first.
    PrismLib.Bootstrap.PrismBootstrap.SyncUi();
    if (PrismLib.Bootstrap.PrismBootstrap.Ensure(entry.Logger.Log, new Version(0, 3, 9))) UsePrism();
    // ...the mod's own setup, which must not depend on the line above
}

// Kept in a separate, non-inlined method: the runtime resolves a method's types when the method is
// compiled, so PrismLib types must not appear in code that can run before Ensure().
[MethodImpl(MethodImplOptions.NoInlining)]
private static void UsePrism()
{
    var me = Prism.Register("Sapphire", typeof(MainClass).Assembly.GetName().Version);
    Prism.Log = SapphireLog.Log;
    _autoplay = me.ClaimState(StateKey.Autoplay, "editor autoplay toggle");
    if (_autoplay == null) { /* another mod owns it; leave RDC.auto alone */ }
}
```

The second argument to `Ensure` is the minimum PrismLib version the mod needs. An older installed
copy is replaced from the release feed.

### Rules for the bridge class

Keep every reference to `PrismLib.dll` in one class (conventionally `PrismBridge`) and follow two
rules:

1. **No PrismLib type in a field.** Field types are resolved when the class loads, before any of its
   methods run. If `PrismLib.dll` is missing, the class fails to load, `Ensure()` never runs, and
   UMM disables the mod with a `TypeLoadException`. Store handles as `object` and cast at the point
   of use.
2. **No PrismLib type outside the bridge**, and every bridge method that uses one is called only
   when `PrismBridge.Available` is true.

`tools/check-bridge.sh <Mod.dll> <Namespace.PrismBridge>` checks both rules against a built mod by
loading it with `PrismLib.dll` absent. Run it before a release; these failures do not show up when
the library is installed.

### Claims

```csharp
using (var c = me.ClaimState(StateKey.EditorCamera, "camera path preview"))
    if (c != null) { /* write the camera */ }
```

A piece of state has one owner at a time, granted on a first-come basis. A denied claim returns
`null`; the caller should then leave that state alone. Claims are not enforced — a mod can still
write the state directly — but they let mods cooperate, and the log records the owner, which makes
conflicts easier to diagnose.

Available keys: `Autoplay`, `NoFail`, `EditorCamera`, `PathLock`, `EditorHud`, `GamePanels`,
`GameHud`, `InputCapture`, `DebugPanel`.

`InputCapture` is held by a mod while it intercepts keyboard input game-wide (for example, while a
settings panel is open). Other mods can check `Claims.OwnerOf(StateKey.InputCapture)` in their
hotkey handling and skip their hotkeys while another mod holds it.

### Keys

```csharp
me.BindKey("quickchart.edit", (int)KeyCode.U, KeyMods.None, "Edit event parameters");
foreach (var other in Keys.Wanting((int)KeyCode.U, KeyMods.None, "Sapphire")) { /* warn */ }
```

Registering a key never blocks another mod from using it; two mods may intentionally share a key.
The registry makes overlaps visible in the log and in settings.

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

A mod describes each setting and how to read and write it; storage stays with the mod.
`Settings.Search("bpm")` searches the settings of every loaded mod. `DescribeSettings` replaces the
mod's previous description rather than adding to it.

PrismLib also has a few settings of its own, stored in `settings.txt` beside `PrismLib.dll`
(`PrismLib.Config`) and listed first in the settings window. Currently this is **Show FPS**, a
frame-rate display shared by all Prism mods.

## PrismLib.UI

A second assembly containing interface code shared between the mods. It references Unity, while
`PrismLib.dll` depends only on the .NET base library so it can load early and be tested outside the
game (`tools/PrismLibTest.cs`).

Mods ship `PrismLib.UI.dll` in their own folder; the bootstrapper does not install it. Interface
code does not need one shared instance across mods, so it can be an ordinary dependency, and calls
into it do not need the `Available` check. `lib/update-prismlib.sh` fetches both assemblies for a
mod's build.

It contains:

- **Windows on UI Toolkit** — `Surface`, `Window` (drag, resize, scale, saved position), `Nav`,
  `Tabs` and a set of widgets (toggle, slider, choice, text, colour, keybind).
- **Layers** — each surface belongs to a layer (`Hud`, `Window`, `Popup`, `Modal`, `Tooltip`), so
  popups always draw above windows.
- **Popups** — `Popup.Open` and `Popup.Menu` for anchored menus that close on an outside click or
  Escape.
- **Escape handling** — `Ui.HandleEscape` closes the topmost popup or window once per key press,
  even when several mods poll Escape.
- **Input gate** — `InputGate` stops keys, the scroll wheel and mouse clicks reaching the game while
  a Prism window is open (`Surface.BlocksGameInput`, on by default). Each mod applies the required
  Harmony patches with its own Harmony instance.
- **Shared panels** — `DebugPanel` (logs and fields from every mod), `SettingsPanel` (renders the
  settings schema) and `FpsCounter`.
- **Font handling** — fonts are loaded from files, and `FontFix` works around OS fonts whose regular
  weight is not named "Regular", which otherwise fail to load on the Steam Linux runtime.
- **Update toast** — `Toast` and `ToastStack`, below.

### Update toast

```csharp
_toast = new Toast("Sapphire");              // canvas is named "SapphireUpdateToastCanvas"
_toast.Theme.Accent = Theme.Accent;          // theme values can change at runtime
_toast.Set(new ToastContent {                // null hides the card; call every tick
    Key = "avail:" + tag,                    // dismissing one key does not suppress another
    Title = "Update available: " + tag,
    Hint = "Click to update",
    AutoHide = true,
    OnClick = () => UpdateService.Install(),
});
_toast.Tick();
```

`Toast` handles the card itself: layout, positioning, animation, hover, the close button, the
progress bar, and remembering dismissed messages. The mod supplies the content as plain values, so
an update check running on a worker thread does not need to touch Unity objects.

`ToastStack` keeps toasts from different mods from overlapping. Each mod names its toast canvas
`<Mod>UpdateToastCanvas` and its card `UpdateToast`; cards are measured in screen pixels, so canvases
with different scalers stack correctly. Quartz follows the same convention.

## Status

- Claims, the key registry and the settings schema are used by Bismuth and Sapphire. Quartz has not
  adopted them yet.
- PrismLib.UI is being adopted screen by screen. Interface code moves here when more than one mod
  would otherwise keep its own copy; code that depends on one mod's state stays in that mod.

## License

MIT — see [LICENSE](LICENSE). The mods that use PrismLib are licensed under GPL-3.0; a permissive
license for the library leaves that choice to each mod.
