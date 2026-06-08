using HarmonyLib;
using System.Runtime.CompilerServices;
using StardewModdingAPI;
using StardewValley;

namespace Cylix.FarmCleaner;

public static class FarmCleanerPatches
{
    internal static bool magnetBoostActive;
    internal static readonly List<Item> capturedItems = [];
    internal static bool skipIntercept;

    public static void Apply(string uniqueId, IMonitor monitor)
    {
        var harmony = new Harmony(uniqueId);

        harmony.Patch(
            original: AccessTools.Method(typeof(Farmer), "GetAppliedMagneticRadius"),
            prefix: new HarmonyMethod(typeof(FarmCleanerPatches), nameof(MagneticRadiusPrefix))
        );

        harmony.Patch(
            original: AccessTools.Method(typeof(Farmer), "addItemToInventory",
                [typeof(Item)]),
            postfix: new HarmonyMethod(typeof(FarmCleanerPatches), nameof(AddItemToInventoryPostfix))
        );

        var couldAcceptMethod =
            AccessTools.Method(typeof(Farmer), "couldInventoryAcceptThisItem",
                [typeof(Item)])
            ?? AccessTools.Method(typeof(Farmer), "couldInventoryAcceptThisItem",
                [typeof(Item), typeof(bool)]);

        var couldAcceptPrefix = couldAcceptMethod.GetParameters().Length == 1
            ? new HarmonyMethod(typeof(FarmCleanerPatches), nameof(CouldInventoryAcceptPrefix))
            : new HarmonyMethod(typeof(FarmCleanerPatches), nameof(CouldInventoryAcceptPrefixAndroid));

        harmony.Patch(
            original: couldAcceptMethod,
            prefix: couldAcceptPrefix
        );
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool MagneticRadiusPrefix(Farmer __instance, ref int __result)
    {
        if (magnetBoostActive)
        {
            __result = 500000;
            return false;
        }
        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void AddItemToInventoryPostfix(
        Farmer __instance, Item item, ref Item __result)
    {
        if (!magnetBoostActive || skipIntercept || __result is null)
            return;

        capturedItems.Add(__result);
        __result = null!;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool CouldInventoryAcceptPrefix(
        Farmer __instance, Item item, ref bool __result)
    {
        if (magnetBoostActive)
        {
            __result = true;
            return false;
        }
        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool CouldInventoryAcceptPrefixAndroid(
        Farmer __instance, Item item, bool message_if_full, ref bool __result)
    {
        return CouldInventoryAcceptPrefix(__instance, item, ref __result);
    }
}
