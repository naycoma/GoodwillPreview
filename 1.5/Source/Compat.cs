using UnityEngine;

using RimWorld;

namespace GoodwillPreview;

internal static class Compat {
    public static float GetMaxFuelLevel(this CompLaunchable launcher) =>
        launcher.FuelingPortSource?.GetComp<CompRefuelable>()?.Props.fuelCapacity ?? 0f;

    public static int MaxLaunchDistanceAtFuelLevel(this CompLaunchable _, float fuelLevel) =>
        CompLaunchable.MaxLaunchDistanceAtFuelLevel(fuelLevel);

    public static Rect MiddlePartPixels(this Rect rect, float width, float height) =>
        new(rect.center.x - width / 2f, rect.center.y - height / 2f, width, height);
}
