using HarmonyLib;
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

    public static bool MagnetBoostActive;

    public FarmClearer(IModHelper helper, IMonitor monitor)
    {
        modHelper = helper;
        modMonitor = monitor;
    }

    public static void ApplyHarmonyPatches(string uniqueId)
    {
        var harmony = new Harmony(uniqueId);

        harmony.Patch(
            original: AccessTools.Method(typeof(Farmer), "GetAppliedMagneticRadius"),
            prefix: new HarmonyMethod(typeof(FarmClearer), nameof(MagneticRadiusPrefix))
        );
    }

    public void ClearFarm(bool clearFruitTrees)
    {
        if (magnetActive)
            return;

        var farm = Game1.getFarm();
        if (farm is null)
            return;

        var total = ClearObjects(farm)
                  + ClearTerrainFeatures(farm, clearFruitTrees)
                  + ClearResourceClumps(farm);

        if (total == 0)
            return;

        modMonitor.Log($"Cleared {total} items from the farm.", LogLevel.Debug);

        var debrisWithItems = farm.debris.Count(d => d.item is not null);
        modMonitor.Log($"Debris count: {farm.debris.Count}, with items: {debrisWithItems}", LogLevel.Debug);

        magnetActive = true;
        MagnetBoostActive = true;
        magnetTicks = 0;
        modHelper.Events.GameLoop.UpdateTicked += OnMagnetTick;
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

        var hasItems = false;
        foreach (var debris in farm.debris)
        {
            if (debris.item is not null)
            {
                hasItems = true;
                break;
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
        MagnetBoostActive = false;
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

    private static int ClearTerrainFeatures(Farm farm, bool clearFruitTrees)
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

    private static bool MagneticRadiusPrefix(Farmer __instance, ref int __result)
    {
        if (MagnetBoostActive)
        {
            __result = 100000;
            return false;
        }
        return true;
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
}
