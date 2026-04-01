using RimWorld;

namespace GoodwillPreview;

internal static class Compat {
    public static float GetMaxFuelLevel(this CompLaunchable launcher) =>
        launcher.FuelingPortSource?.GetComp<CompRefuelable>()?.Props.fuelCapacity ?? 0f;

    public static int MaxLaunchDistanceAtFuelLevel(this CompLaunchable _, float fuelLevel) =>
        CompLaunchable.MaxLaunchDistanceAtFuelLevel(fuelLevel);
}
