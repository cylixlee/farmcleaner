using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace Cylix.FarmCleaner;

internal sealed class ModEntry : Mod
{
    private ModConfig config = null!;
    private FarmClearer farmClearer = null!;

    public override void Entry(IModHelper helper)
    {
        config = Helper.ReadConfig<ModConfig>();
        farmClearer = new FarmClearer(Helper, Monitor);

        FarmClearer.ApplyHarmonyPatches(ModManifest.UniqueID);

        helper.Events.Input.ButtonsChanged += OnButtonsChanged;
        helper.Events.GameLoop.GameLaunched += OnGameLaunched;

        helper.ConsoleCommands.Add("clearfarm",
            "Clears all trees, stones, grass, and debris from your farm.\n\nUsage: clearfarm",
            (_, _) => farmClearer.ClearFarm(config.ClearFruitTrees));
    }

    private void OnButtonsChanged(object? sender, ButtonsChangedEventArgs e)
    {
        if (!Context.IsWorldReady)
            return;

        if (!config.ClearKey.JustPressed())
            return;

        if (Game1.currentLocation is not Farm)
        {
            Monitor.Log("You must be on your farm to use this.", LogLevel.Info);
            return;
        }

        farmClearer.ClearFarm(config.ClearFruitTrees);
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        var gmcm = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>(
            "spacechase0.GenericModConfigMenu");
        if (gmcm is null)
            return;

        gmcm.Register(
            mod: ModManifest,
            reset: () => config = new ModConfig(),
            save: () => Helper.WriteConfig(config)
        );

        gmcm.AddKeybindList(
            mod: ModManifest,
            getValue: () => config.ClearKey,
            setValue: val => config.ClearKey = val,
            name: () => "Clear Key",
            tooltip: () => "Key to clear all trees, stones, grass, and debris from your farm."
        );

        gmcm.AddBoolOption(
            mod: ModManifest,
            getValue: () => config.ClearFruitTrees,
            setValue: val => config.ClearFruitTrees = val,
            name: () => "Clear Fruit Trees",
            tooltip: () => "If enabled, fruit trees will also be cleared."
        );
    }
}
