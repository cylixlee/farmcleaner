using StardewModdingAPI.Utilities;

namespace Cylix.FarmCleaner;

public sealed class ModConfig
{
    public KeybindList ClearKey { get; set; } = KeybindList.Parse("K");
    public bool ClearFruitTrees { get; set; } = false;
}
