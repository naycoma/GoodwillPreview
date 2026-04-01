using RimWorld;

namespace GoodwillPreview;

internal static class Compat {
    public static float GetMaxFuelLevel(this CompLaunchable launcher) =>
        launcher.MaxFuelLevel;
}
