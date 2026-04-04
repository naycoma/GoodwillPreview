using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using Verse;
using RimWorld;
using HarmonyLib;

namespace GoodwillPreview;

[HarmonyPatch]
public static class Patch {
    public const bool DEBUG =
#if DEBUG
    true;
# else
    false;
# endif

    [HarmonyPatch(typeof(Dialog_LoadTransporters))]
    [HarmonyPatch(nameof(Dialog_LoadTransporters.PostOpen)), HarmonyPostfix]
    public static void PostOpen_Postfix(Dialog_LoadTransporters __instance) {
        if (DEBUG) Log.Message($"[{nameof(Dialog_LoadTransporters.PostOpen)}][Postfix] Dialog opened");
        Mod.Open(__instance);
        int tile = __instance.map.Tile;
        Mod.ReloadSettlements(SettlementUtility.GetSettlementsWithinRadius(tile));
        Mod.ReloadSinglePrices();
    }

    [HarmonyPatch(typeof(TransferableUIUtility))]
    [HarmonyPatch(nameof(TransferableUIUtility.DrawExtraInfo)), HarmonyPrefix]
    public static void DrawExtraInfo_Prefix(ref List<TransferableUIUtility.ExtraInfo> info, ref Rect rect) {
        // if (DEBUG) Log.Message($"[{nameof(TransferableUIUtility.DrawExtraInfo)}][Prefix] Initial info count: {info.Count}");
        if (Mod.Empty()) return;
        if (Mod.IsShuttle()) return;

        bool showFloatMenu = Find.WindowStack.Windows.Any(w => w is FloatMenu);
        if (Mod.ExtraInfo(includeTip: !showFloatMenu) is { } e)
            info.Add(e);
    }

    [HarmonyPatch(typeof(TransferableUIUtility))]
    [HarmonyPatch(nameof(TransferableUIUtility.DrawExtraInfo)), HarmonyPostfix]
    public static void DrawExtraInfo_Postfix(List<TransferableUIUtility.ExtraInfo> info, Rect rect) {
        // if (DEBUG) Log.Message($"[{nameof(TransferableUIUtility.DrawExtraInfo)}][Postfix] Info count: {info.Count}");
        if (Mod.Empty()) return;
        if (Mod.IsShuttle()) return;

        float maxWidth = info.Count * 230f;
        if (rect.width > maxWidth)
            rect = rect.MiddlePartPixels(maxWidth, rect.height);

        int markedIndex = info.FindIndex(Mod.IsMarkedExtraInfo);
        if (markedIndex == -1) return;

        Widgets.BeginGroup(rect);
        rect = rect.AtZero();

        Rect infoRect = rect.RightPart(1f - (float)markedIndex / info.Count);
        Faction displayed = Mod.CurrentSettlement().Faction;
        if (Mod.ButtonGoodwillExtraInfo(infoRect, displayed))
            Find.WindowStack.Add(Mod.CreateDropdownMenu());

        Widgets.EndGroup();
    }

    [HarmonyPatch(typeof(Dialog_LoadTransporters))]
    [HarmonyPatch(nameof(Dialog_LoadTransporters.CountToTransferChanged)), HarmonyPostfix]
    public static void CountToTransferChanged_Postfix(Dialog_LoadTransporters __instance) {
        if (DEBUG) Log.Message($"[{nameof(Dialog_LoadTransporters.CountToTransferChanged)}][Postfix] Count changed");
        Mod.ChangedCount();
    }

    [HarmonyPatch(typeof(Window), nameof(Window.Close), [typeof(bool)])]
    [HarmonyPostfix]
    public static void Close_Postfix(Window __instance, bool doCloseSound) {
        if (__instance is not Dialog_LoadTransporters) return;
        if (DEBUG) Log.Message($"[{nameof(Dialog_LoadTransporters.Close)}][Postfix] {__instance.GetHashCode()}");
        Mod.Close();
    }
}
