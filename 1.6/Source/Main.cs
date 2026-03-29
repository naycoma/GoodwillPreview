using HarmonyLib;
using Verse;

namespace GoodwillPreview;

[StaticConstructorOnStartup]
public static class Main
{
    static Main() => new Harmony("bluebird.GoodwillPreview").PatchAll();
}