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
    private static readonly List<Item> capturedItems = [];
    private static bool skipIntercept;

    public FarmClearer(IModHelper helper, IMonitor monitor)
    {
        modHelper = helper;
        modMonitor = monitor;
    }

    public static void ApplyHarmonyPatches(string uniqueId)
    {
        var harmony = new Harmony(uniqueId);

        harmony.Patch(
            original: AccessTools.Method(typeof(Debris), "playerInRange"),
            prefix: new HarmonyMethod(typeof(FarmClearer), nameof(PlayerInRangePrefix))
        );

        harmony.Patch(
            original: AccessTools.Method(typeof(Farmer), "GetAppliedMagneticRadius"),
            prefix: new HarmonyMethod(typeof(FarmClearer), nameof(MagneticRadiusPrefix))
        );

        harmony.Patch(
            original: AccessTools.Method(typeof(Farmer), "addItemToInventory",
                [typeof(Item)]),
            postfix: new HarmonyMethod(typeof(FarmClearer), nameof(AddItemToInventoryPostfix))
        );

        harmony.Patch(
            original: AccessTools.Method(typeof(Farmer), "couldInventoryAcceptThisItem",
                [typeof(Item)]),
            prefix: new HarmonyMethod(typeof(FarmClearer), nameof(CouldInventoryAcceptPrefix))
        );
    }

    public void ClearFarm(bool clearFruitTrees, float dropMultiplier)
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
        MagnetBoostActive = true;
        capturedItems.Clear();
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

        skipIntercept = true;
        for (int i = capturedItems.Count - 1; i >= 0; i--)
        {
            var leftover = Game1.player.addItemToInventory(capturedItems[i]);
            if (leftover is null)
                capturedItems.RemoveAt(i);
            else
                capturedItems[i] = leftover;
        }
        skipIntercept = false;

        var hasItems = capturedItems.Count > 0;
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
        MagnetBoostActive = false;

        foreach (var item in capturedItems)
        {
            var offset = new Vector2(
                (Random.Shared.NextSingle() - 0.5f) * 80f,
                (Random.Shared.NextSingle() - 0.5f) * 80f - 64f);
            var pos = Game1.player.Position + offset;

            var debris = Game1.createItemDebris(item, pos, Game1.player.FacingDirection);
            if (debris is not null)
            {
                foreach (var chunk in debris.Chunks)
                {
                    chunk.xVelocity.Value = (Random.Shared.NextSingle() - 0.5f) * 4f;
                    chunk.yVelocity.Value = (Random.Shared.NextSingle() - 0.5f) * 4f;
                }
            }
        }
        capturedItems.Clear();
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

    private static bool PlayerInRangePrefix(
        Debris __instance, Vector2 position, Farmer farmer, ref bool __result)
    {
        if (!MagnetBoostActive)
            return true;

        __result = true;
        return false;
    }

    private static bool MagneticRadiusPrefix(Farmer __instance, ref int __result)
    {
        if (MagnetBoostActive)
        {
            __result = 500;
            return false;
        }
        return true;
    }

    private static void AddItemToInventoryPostfix(
        Farmer __instance, Item item, ref Item __result)
    {
        if (!MagnetBoostActive || skipIntercept || __result is null)
            return;

        capturedItems.Add(__result);
        __result = null!;
    }

    private static bool CouldInventoryAcceptPrefix(Farmer __instance, Item item, ref bool __result)
    {
        if (MagnetBoostActive)
        {
            __result = true;
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
