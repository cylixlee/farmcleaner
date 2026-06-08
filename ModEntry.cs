using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace Cylix.FarmCleaner;

internal sealed class ModEntry : Mod
{
    private ModConfig _config = null!;
    private FarmClearer _farmClearer = null!;

    public override void Entry(IModHelper helper)
    {
        _config = Helper.ReadConfig<ModConfig>();
        _farmClearer = new FarmClearer(Helper, Monitor);

        helper.Events.Input.ButtonsChanged += OnButtonsChanged;
        helper.Events.GameLoop.GameLaunched += OnGameLaunched;

        helper.ConsoleCommands.Add("clearfarm",
            "Clears all trees, stones, grass, and debris from your farm.\n\nUsage: clearfarm",
            (_, _) => _farmClearer.ClearFarm(_config.ClearFruitTrees));
    }

    private void OnButtonsChanged(object? sender, ButtonsChangedEventArgs e)
    {
        if (!Context.IsWorldReady)
            return;

        if (!_config.ClearKey.JustPressed())
            return;

        if (Game1.currentLocation is not Farm)
        {
            Monitor.Log("You must be on your farm to use this.", LogLevel.Info);
            return;
        }

        _farmClearer.ClearFarm(_config.ClearFruitTrees);
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        var gmcm = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>(
            "spacechase0.GenericModConfigMenu");
        if (gmcm is null)
            return;

        gmcm.Register(ModManifest, () => _config = new ModConfig(), () => Helper.WriteConfig(_config));

        gmcm.AddKeybind(
            mod: ModManifest,
            name: () => "Clear Key",
            tooltip: () => "Key to clear all trees, stones, grass, and debris from your farm.",
            getValue: () => _config.ClearKey,
            setValue: val => _config.ClearKey = val
        );

        gmcm.AddBoolOption(
            mod: ModManifest,
            name: () => "Clear Fruit Trees",
            tooltip: () => "If enabled, fruit trees will also be cleared.",
            getValue: () => _config.ClearFruitTrees,
            setValue: val => _config.ClearFruitTrees = val
        );
    }
}
