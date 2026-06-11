using StardewModdingAPI.Utilities;

namespace Cylix.FarmCleaner;

public sealed class ModConfig
{
    public KeybindList ClearKey { get; set; } = KeybindList.Parse("K");
    public bool ClearFruitTrees { get; set; } = false;
    public float DropMultiplier { get; set; } = 1.0f;
    public bool EnableExperience { get; set; } = true;
    public bool ClearTappedTrees { get; set; } = false;
    public bool ClearGrowingTrees { get; set; } = false;
}
