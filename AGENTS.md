# AGENTS.md

## Project

Stardew Valley SMAPI mod — one-key farm debris cleanup. Single `.csproj`, no solution file.

## Build & Run

```bash
dotnet build
```

`Pathoschild.Stardew.ModBuildConfig` (v4.4.0) auto-deploys to the Stardew Valley `Mods/` folder on successful build. SMAPI must be installed on the machine.

- Target framework: `net6.0` (not .NET 8+ — this is a SMAPI constraint)
- LangVersion: `Latest`, Nullable: `enable`, ImplicitUsings: `enable`
- Mod output folder name: `FarmCleaner` (set via `<ModFolderName>`)

No test project. Testing is manual: build, launch Stardew Valley, press `K` (default hotkey) on the farm.

## Architecture

| File                          | Role                                                                                           |
| ----------------------------- | ---------------------------------------------------------------------------------------------- |
| `ModEntry.cs`                 | SMAPI entry point. Registers hotkey, console command (`clearfarm`), GMCM config                |
| `FarmClearer.cs`              | Core cleanup logic: removes objects, terrain features, resource clumps; runs magnet loop       |
| `FarmCleanerPatches.cs`       | Harmony patches on `Farmer` methods for infinite magnet radius, item interception, XP blocking |
| `ModConfig.cs`                | All settings; persisted as `config.json` by SMAPI                                              |
| `IGenericModConfigMenuApi.cs` | GMCM API interface (partial — only methods used by this mod)                                   |
| `manifest.json`               | SMAPI metadata: `UniqueID = Cylix.FarmCleaner`, `MinimumApiVersion = 4.0.0`                    |

## Key Design Details

- **Magnet flow**: Debris items are intercepted via `addItemToInventory` postfix, collected into `capturedItems`, then re-added one per tick in `OnMagnetTick`. This avoids crash-on-overflow when the player's inventory is full.
- **Overflow**: Items that don't fit are spawned as new debris near the player.
- **Magnet radius**: Harmony-prefixed `GetAppliedMagneticRadius` returns `500000` (effectively infinite) during cleanup.
- **Android compatibility**: `couldInventoryAcceptThisItem` has two overloads (1 param vs 2 params). The correct one is detected at patch time via reflection. The 2-param overload is for Android.
- **Experience blocking**: `gainExperience` is Harmony-prefixed to return `false` when `Config.EnableExperience` is off. The `blockExperience` flag is set in `ClearFarm` and wrapped in a `try/finally` to guarantee it resets.
- **Debris item resolution**: SMAPI sometimes leaves `debris.item` as null but `debris.itemId` populated — `FarmClearer.ClearFarm` resolves these via `ItemRegistry.Create` (lines 44-52).

## Multiplayer Warning

This mod has **no multiplayer safety checks**. It clears all objects/features/clumps from the farm without checking `Game1.IsMultiplayer` or using SMAPI's multiplayer utilities. This is intentional for now — the author has decided to avoid multiplayer compatibility.

## SMAPI-Specific Conventions

- Config is read/written via `Helper.ReadConfig<ModConfig>()` / `Helper.WriteConfig(config)`. Do not manually read/write `config.json`.
- Harmony patches must use a unique ID (typically `ModManifest.UniqueID`), applied from `Entry()`, preferably before any events fire.
- `Context.IsWorldReady` must be checked before accessing game state in hotkey handlers.
- The `.csproj` `<EnableHarmony>true</EnableHarmony>` property is required for SMAPI's build config to detect Harmony usage.

## Files Not to Touch

- `manifest.json` — auto-generated/managed by SMAPI build config during publish (`dotnet build` syncs version from csproj).
- Images in `img/` are tracked with Git LFS (`.gitattributes`).
