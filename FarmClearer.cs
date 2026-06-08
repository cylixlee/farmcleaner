using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;

namespace Cylix.FarmCleaner;

internal class FarmClearer
{
    private readonly IModHelper _helper;
    private readonly IMonitor _monitor;
    private bool _magnetActive;

    public FarmClearer(IModHelper helper, IMonitor monitor)
    {
        _helper = helper;
        _monitor = monitor;
    }

    public void ClearFarm(bool clearFruitTrees)
    {
        if (_magnetActive)
        {
            _monitor.Log("Farm clearing already in progress.", LogLevel.Debug);
            return;
        }

        var farm = Game1.getFarm();
        if (farm is null)
            return;

        var total = ClearObjects(farm)
                  + ClearTerrainFeatures(farm, clearFruitTrees)
                  + ClearResourceClumps(farm);

        if (total == 0)
            return;

        _monitor.Log($"Cleared {total} items from the farm.", LogLevel.Debug);

        StartMagnet();
    }

    private int ClearObjects(Farm farm)
    {
        var pickaxe = new Pickaxe();
        var axe = new Axe();

        var toRemove = new List<Vector2>();

        foreach (var (tile, obj) in farm.Objects.Pairs)
        {
            if (obj is null)
                continue;

            var name = obj.Name;
            var type = obj.Type;

            if (IsStone(name, type))
                obj.performToolAction(pickaxe);
            else if (IsWeed(name, type))
                obj.performToolAction(axe);
            else if (IsTwig(name, type))
                obj.performToolAction(axe);
            else
                continue;

            toRemove.Add(tile);
        }

        foreach (var tile in toRemove)
            farm.Objects.Remove(tile);

        return toRemove.Count;
    }

    private int ClearTerrainFeatures(Farm farm, bool clearFruitTrees)
    {
        var axe = new Axe();

        var toRemove = new List<Vector2>();

        foreach (var (tile, feature) in farm.terrainFeatures.Pairs)
        {
            if (feature is null)
                continue;

            switch (feature)
            {
                case Tree tree:
                    tree.performToolAction(axe, explosion: 1, tile);
                    toRemove.Add(tile);
                    break;

                case FruitTree fruitTree when clearFruitTrees:
                    fruitTree.performToolAction(axe, explosion: 1, tile);
                    toRemove.Add(tile);
                    break;

                case Grass grass:
                    grass.performToolAction(axe, explosion: 1, tile);
                    toRemove.Add(tile);
                    break;
            }
        }

        foreach (var tile in toRemove)
            farm.terrainFeatures.Remove(tile);

        return toRemove.Count;
    }

    private int ClearResourceClumps(Farm farm)
    {
        var pickaxe = new Pickaxe();
        var count = 0;

        var clumps = farm.resourceClumps.ToList();

        foreach (var clump in clumps)
        {
            var tile = new Vector2(
                (int)(clump.Tile.X),
                (int)(clump.Tile.Y)
            );

            clump.performToolAction(pickaxe, damage: 999, tile);
            count++;
        }

        farm.resourceClumps.Clear();

        return count;
    }

    private void StartMagnet()
    {
        if (_magnetActive)
            return;

        _magnetActive = true;
        _helper.Events.GameLoop.UpdateTicked += OnMagnetTick;
    }

    private int _magnetTicks;

    private void OnMagnetTick(object? sender, UpdateTickedEventArgs e)
    {
        var farm = Game1.getFarm();
        if (farm is null)
        {
            StopMagnet();
            return;
        }

        _magnetTicks++;

        var playerPos = Game1.player.Position;

        foreach (var debris in farm.debris)
        {
            if (debris.item is null)
                continue;

            foreach (var chunk in debris.Chunks)
            {
                var pos = chunk.position.Value;
                var dir = playerPos - pos;
                var dist = dir.Length();

                if (dist < 32f)
                    continue;

                dir.Normalize();

                var speed = Math.Min(dist * 0.3f, 30f);
                chunk.xVelocity.Value = dir.X * speed;
                chunk.yVelocity.Value = dir.Y * speed;
            }
        }

        if (_magnetTicks > 180 || farm.debris.Count == 0)
            StopMagnet();
    }

    private void StopMagnet()
    {
        _helper.Events.GameLoop.UpdateTicked -= OnMagnetTick;
        _magnetTicks = 0;
        _magnetActive = false;
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
