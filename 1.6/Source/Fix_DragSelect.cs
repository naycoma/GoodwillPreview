using System.Linq;
using System.Reflection;

using UnityEngine;
using Verse;
using RimWorld;
using HarmonyLib;

namespace GoodwillPreview;

// DragSelectのFloatMenu貫通バグ対応
// DragSelectはEvent.currentではなくInput.GetMouseButtonUp(0)を直接使うため
// absorbInputAroundWindowが効かず、FloatMenu選択時に背後のボタンも発火する。
// FloatMenu表示中にDraggingUtility.lastPressFrameをリセットして抑制する（lastPressFrame > 5 ガード）
[HarmonyPatch]
public static class Fix_DragSelect {
    private static FieldInfo lastPressFrame = null;
    private static bool searched = false;

    [HarmonyPatch(typeof(Dialog_LoadTransporters))]
    [HarmonyPatch(nameof(Dialog_LoadTransporters.DoWindowContents)), HarmonyPrefix]
    public static void DoWindowContents_Prefix() {
        if (!Find.WindowStack.Windows.Any(w => w is GoodwillDropdownMenu)) return;

        if (!searched) {
            searched = true;
            lastPressFrame = AccessTools.TypeByName("DragSelect.DraggingUtility") is { } t
                ? AccessTools.Field(t, "lastPressFrame") : null;
        }
        lastPressFrame?.SetValue(null, Time.frameCount);
    }
}