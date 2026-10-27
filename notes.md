### For players
- **Restart prompt after updates.** When a mod finishes updating, PrismLib offers **Restart now** or **Later**, with an option to remember your choice.
- **Windows updates no longer fail on files in use.** When the game has `PrismLib.dll` loaded, the update is saved alongside it and applied on the next launch. It is checked against the expected file before it is applied.
- **Shared PrismLib.UI copies stay up to date.** Each mod folder's copy is updated separately, so one locked file doesn't stop the others. A locked copy is updated on the next launch.
- **Mod guards.** PrismLib can switch off a mod on game builds where that mod is known to break. The first guard turns off Quartz on ADOFAI 2.x.

### For mod authors
- `PrismLib.UI.RestartPrompt.Request(mod, read, write)`: shows the restart prompt. `read`/`write` store the remembered choice and may be `null`. Store it under `PrismLib.Prism.RestartKey` through `PrismLib.Config` so every mod shares one setting.
- `PrismLib.Guards.Tick()`: call once per frame to run the guards.
- These types don't exist in 0.3.9 or earlier. If another mod might load an older copy, put the call in its own `[MethodImpl(NoInlining)]` method inside a try/catch.