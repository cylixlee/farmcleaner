using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;

namespace Cylix.FarmCleaner;

internal class FarmClearer
{
    private readonly IModHelper modHelper;
    private readonly IMonitor modMonitor;
    private bool magnetActive;

    public FarmClearer(IModHelper helper, IMonitor monitor)
    {
        modHelper = helper;
        modMonitor = monitor;
    }

    public void ClearFarm(bool clearFruitTrees, float dropMultiplier, bool enableExperience, bool clearTappedTrees, bool clearGrowingTrees)
    {
        if (magnetActive)
            return;

        var farm = Game1.getFarm();
        if (farm is null)
            return;

        FarmCleanerPatches.blockExperience = !enableExperience;
        try
        {
            var total = ClearObjects(farm)
                      + ClearTerrainFeatures(farm, clearFruitTrees, clearTappedTrees, clearGrowingTrees)
                      + ClearResourceClumps(farm);

            if (total == 0)
                return;

            modMonitor.Log($"Cleared {total} items from the farm.", LogLevel.Debug);

            if (Math.Abs(dropMultiplier - 1.0f) > 0.01f)
            {
                foreach (var debris in farm.debris)
                {
                    if (debris.item is not null)
                        debris.item.Stack = (int)(debris.item.Stack * dropMultiplier);
                }
            }

            var debrisWithItems = farm.debris.Count(d => d.item is not null);
            modMonitor.Log($"Debris count: {farm.debris.Count}, with items: {debrisWithItems}", LogLevel.Debug);

            magnetActive = true;
            FarmCleanerPatches.magnetBoostActive = true;
            FarmCleanerPatches.capturedItems.Clear();
            magnetTicks = 0;
            modHelper.Events.GameLoop.UpdateTicked += OnMagnetTick;
        }
        finally
        {
            FarmCleanerPatches.blockExperience = false;
        }
    }

    private int magnetTicks;

    private void OnMagnetTick(object? sender, UpdateTickedEventArgs e)
    {
        var farm = Game1.getFarm();
        if (farm is null)
        {
            StopMagnet();
            return;
        }

        magnetTicks++;

        FarmCleanerPatches.skipIntercept = true;
        var overflow = new List<Item>();
        for (int i = FarmCleanerPatches.capturedItems.Count - 1; i >= 0; i--)
        {
            var leftover = Game1.player.addItemToInventory(FarmCleanerPatches.capturedItems[i]);
            if (leftover is null)
                FarmCleanerPatches.capturedItems.RemoveAt(i);
            else
            {
                overflow.Add(leftover);
                FarmCleanerPatches.capturedItems.RemoveAt(i);
            }
        }
        FarmCleanerPatches.skipIntercept = false;

        foreach (var item in overflow)
        {
            var offset = new Vector2(
                (Random.Shared.NextSingle() - 0.5f) * 80f,
                (Random.Shared.NextSingle() - 0.5f) * 80f - 64f);
            var pos = Game1.player.Position + offset;
            Game1.createItemDebris(item, pos, Game1.player.FacingDirection);
        }

        var hasItems = FarmCleanerPatches.capturedItems.Count > 0;
        if (!hasItems)
        {
            foreach (var debris in farm.debris)
            {
                if (debris.item is not null)
                {
                    hasItems = true;
                    break;
                }
            }
        }

        if (!hasItems || magnetTicks > 600)
            StopMagnet();
    }

    private void StopMagnet()
    {
        modHelper.Events.GameLoop.UpdateTicked -= OnMagnetTick;
        magnetTicks = 0;
        magnetActive = false;
        FarmCleanerPatches.magnetBoostActive = false;
        FarmCleanerPatches.capturedItems.Clear();
    }

    private int ClearObjects(Farm farm)
    {
        var player = Game1.player;
        var pickaxe = new Pickaxe
        {
            lastUser = player,
            UpgradeLevel = 4
        };
        var axe = new Axe
        {
            lastUser = player,
            UpgradeLevel = 4
        };

        var toRemove = new List<Vector2>();

        foreach (var (tile, obj) in farm.Objects.Pairs)
        {
            if (obj is null)
                continue;

            var name = obj.Name;
            var type = obj.Type;

            if (IsStone(name, type))
            {
                modHelper.Reflection.GetField<int>(obj, "health").SetValue(1);
                obj.performToolAction(pickaxe);
            }
            else if (IsWeed(name, type))
            {
                modHelper.Reflection.GetField<int>(obj, "health").SetValue(1);
                obj.performToolAction(axe);
            }
            else if (IsTwig(name, type))
            {
                modHelper.Reflection.GetField<int>(obj, "health").SetValue(1);
                obj.performToolAction(axe);
            }
            else
                continue;

            toRemove.Add(tile);
        }

        foreach (var tile in toRemove)
            farm.Objects.Remove(tile);

        return toRemove.Count;
    }

    private static int ClearTerrainFeatures(Farm farm, bool clearFruitTrees, bool clearTappedTrees, bool clearGrowingTrees)
    {
        var axe = new Axe
        {
            lastUser = Game1.player,
            UpgradeLevel = 4
        };

        var scythe = ItemRegistry.Create<MeleeWeapon>("(W)47");

        var toRemove = new List<Vector2>();

        foreach (var (tile, feature) in farm.terrainFeatures.Pairs)
        {
            if (feature is null)
                continue;

            switch (feature)
            {
                case Tree tree:
                    if (!clearTappedTrees && HasTapper(farm, tile))
                        break;
                    if (!clearGrowingTrees && tree.growthStage.Value < 5)
                        break;
                    tree.health.Value = 1;
                    tree.stump.Value = false;
                    tree.performToolAction(axe, explosion: 0, tile);
                    toRemove.Add(tile);
                    break;

                case FruitTree fruitTree when clearFruitTrees:
                    fruitTree.health.Value = 1;
                    fruitTree.stump.Value = false;
                    fruitTree.performToolAction(axe, explosion: 0, tile);
                    toRemove.Add(tile);
                    break;

                case Grass grass:
                    int weeds = grass.numberOfWeeds.Value;
                    for (int i = 0; i < weeds; i++)
                        grass.performToolAction(scythe, explosion: 0, tile);
                    toRemove.Add(tile);
                    break;
            }
        }

        foreach (var tile in toRemove)
            farm.terrainFeatures.Remove(tile);

        return toRemove.Count;
    }

    private static int ClearResourceClumps(Farm farm)
    {
        var pickaxe = new Pickaxe
        {
            lastUser = Game1.player,
            UpgradeLevel = 4
        };
        var count = 0;

        var clumps = farm.resourceClumps.ToList();

        foreach (var clump in clumps)
        {
            var tile = new Vector2(
                (int)(clump.Tile.X),
                (int)(clump.Tile.Y)
            );

            clump.health.Value = 1;
            clump.performToolAction(pickaxe, damage: 1, tile);
            count++;
        }

        farm.resourceClumps.Clear();

        return count;
    }

    private static bool IsStone(string name, string type)
    {
        return type is "Stone" or "Basic"
            || name.Contains("Stone", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Boulder", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsWeed(string name, string type)
    {
        return type == "Weeds"
            || name.Contains("Weed", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTwig(string name, string type)
    {
        return type == "Twig"
            || name.Contains("Twig", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Branch", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasTapper(Farm farm, Vector2 tile)
    {
        if (!farm.Objects.TryGetValue(tile, out var obj) || obj is null)
            return false;
        return obj.QualifiedItemId is "(BC)105" or "(BC)264";
    }
}
