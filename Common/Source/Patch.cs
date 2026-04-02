using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using Verse;
using RimWorld;
using HarmonyLib;

namespace GoodwillPreview;

[HarmonyPatch]
public static class Patch {
    [HarmonyPatch(typeof(Dialog_LoadTransporters))]
    [HarmonyPatch(nameof(Dialog_LoadTransporters.PostOpen)), HarmonyPostfix]
    public static void PostOpen_Postfix(Dialog_LoadTransporters __instance) {
        // CompLaunchable launcher = __instance.transporters[0].Launchable;
        // int distance = launcher.MaxLaunchDistanceAtFuelLevel(launcher.GetMaxFuelLevel());
        int tile = __instance.map.Tile;
        // Log.Message($"Tile: {tile}, Radius: {distance}");
        // Log.Message($"Transporters: {__instance.transporters.Count}");
        Mod.ReloadSettlements(SettlementUtility.GetSettlementsWithinRadius(tile));
    }

    [HarmonyPatch(typeof(Dialog_LoadTransporters))]
    [HarmonyPatch(nameof(Dialog_LoadTransporters.DoWindowContents)), HarmonyPrefix]
    public static void DoWindowContents_Prefix(Dialog_LoadTransporters __instance) {
        Mod.Open(__instance);
    }

    [HarmonyPatch(typeof(TransferableUIUtility))]
    [HarmonyPatch(nameof(TransferableUIUtility.DrawExtraInfo)), HarmonyPrefix]
    public static void DrawExtraInfo_Prefix(ref List<TransferableUIUtility.ExtraInfo> info, ref Rect rect) {
        if (Mod.Empty()) return;
        bool showFloatMenu = Find.WindowStack.Windows.Any(w => w is FloatMenu);
        if (Mod.ExtraInfo(includeTip: !showFloatMenu) is { } e)
            info.Add(e);
    }

    [HarmonyPatch(typeof(TransferableUIUtility))]
    [HarmonyPatch(nameof(TransferableUIUtility.DrawExtraInfo)), HarmonyPostfix]
    public static void DrawExtraInfo_Postfix(List<TransferableUIUtility.ExtraInfo> info, Rect rect) {
        if (Mod.Empty()) return;
        float maxWidth = info.Count * 230f;
        if (rect.width > maxWidth)
            rect = rect.MiddlePartPixels(maxWidth, rect.height);

        Widgets.BeginGroup(rect);
        rect = rect.AtZero();

        int markedIndex = info.FindIndex(Mod.IsMarkedExtraInfo);
        if (markedIndex >= 0) {
            Faction displayed = Mod.CurrentSettlement().Faction;
            
            Rect infoRect = rect.RightPart(1f - (float)markedIndex / info.Count);
            
            if (Mod.ButtonGoodwillExtraInfo(infoRect, displayed))
                Find.WindowStack.Add(Mod.CreateDropdownMenu());
        }

        Widgets.EndGroup();
    }

    [HarmonyPatch(typeof(Dialog_LoadTransporters))]
    [HarmonyPatch(nameof(Dialog_LoadTransporters.CountToTransferChanged)), HarmonyPostfix]
    public static void CountToTransferChanged_Postfix(Dialog_LoadTransporters __instance) {
        Mod.ChangedCount();
    }

    [HarmonyPatch(typeof(Dialog_LoadTransporters))]
    [HarmonyPatch(nameof(Dialog_LoadTransporters.DoWindowContents)), HarmonyPostfix]
    public static void DoWindowContents_Postfix(Dialog_LoadTransporters __instance) {
        Mod.Close();
    }
}
