using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using Verse;
using RimWorld;
using HarmonyLib;

using RimWorld.Planet;

namespace GoodwillPreview;

[HarmonyPatch]
public static class Patch {
    private static Dialog_LoadTransporters dialog = null;
    private static readonly List<Settlement> settlements = [];

    private static readonly Dictionary<Faction, int> goodwillsCache = [];
    private static bool extraInfoDirty = true;
    private static TransferableUIUtility.ExtraInfo? extraInfoCache = null;

    // secondValueが空の時secondColorは描画されないので識別子として流用
    private static readonly Color OurMarker = new(0f, 1f / 255f, 2f / 255f, 3f / 255f);
    private static int selectedIndex = -1; // -1 = first settlement


    [HarmonyPatch(typeof(Dialog_LoadTransporters))]
    [HarmonyPatch(nameof(Dialog_LoadTransporters.PostOpen)), HarmonyPostfix]
    public static void PostOpen_Postfix(Dialog_LoadTransporters __instance) {
        settlements.Clear();
        selectedIndex = -1;
        CompLaunchable launcher = __instance.transporters[0].Launchable;
        int distance = launcher.MaxLaunchDistanceAtFuelLevel(launcher.MaxFuelLevel);
        int tile = __instance.map.Tile;
        Log.Message($"Tile: {tile}, Radius: {distance}");
        Log.Message($"Transporters: {__instance.transporters.Count}");
        settlements.AddRange(SettlementUtility.GetSettlementsWithinRadius(tile, distance));
        if (settlements.Empty()) {
            Log.Warning("No settlements found within range.");
        }
        settlements.SortBy(s => s.Faction.loadID);
    }

    [HarmonyPatch(typeof(Dialog_LoadTransporters))]
    [HarmonyPatch(nameof(Dialog_LoadTransporters.DoWindowContents)), HarmonyPrefix]
    public static void DoWindowContents_Prefix(Dialog_LoadTransporters __instance) {
        dialog = __instance;
    }

    [HarmonyPatch(typeof(Dialog_LoadTransporters))]
    [HarmonyPatch(nameof(Dialog_LoadTransporters.DoWindowContents)), HarmonyPostfix]
    public static void DoWindowContents_Postfix(Dialog_LoadTransporters __instance) {
        dialog = null;
    }

    [HarmonyPatch(typeof(Dialog_LoadTransporters))]
    [HarmonyPatch(nameof(Dialog_LoadTransporters.CountToTransferChanged)), HarmonyPostfix]
    public static void CountToTransferChanged_Postfix(Dialog_LoadTransporters __instance) {
        goodwillsCache.Clear();
        extraInfoDirty = true;
    }

    public static int Goodwill(Settlement settlement) {
        if (goodwillsCache.TryGetValue(settlement.Faction, out int goodwill)) return goodwill;
        goodwill = TransferableFactionGiftUtility.GetGoodwillChange(dialog.transferables, settlement);
        goodwillsCache[settlement.Faction] = goodwill;
        return goodwill;
    }

    private static string BuildTip() {
        string tip = "GoodwillPreview.GiftGoodwillIncreaseTip".Translate();
        foreach (Settlement s in settlements) {
            Faction f = s.Faction;
            int current = f.PlayerGoodwill;
            int goodwillGive = Goodwill(s);
            FactionRelationKind nextKind = f.NextKind(goodwillGive, out int next);
            tip = string.Concat([
                tip,
                "\n\n",
                "GoodwillPreview.TipFactionHeader".Translate(f.Named("FACTION")),
                "\n   ",
                "GoodwillPreview.TipGoodwillXToY".Translate(
                "GoodwillPreview.TipGoodwillFormat".GoodwillFormat(f).Named("CURRENT"),
                "GoodwillPreview.TipGoodwillFormat".GoodwillFormat(nextKind, next).Named("NEXT"),
                    goodwillGive.ToStringWithSign().Named("DELTA")
                ).Resolve(),
                "\n   ",
                "GoodwillPreview.Tiles".Translate(
                    Find.WorldGrid.ApproxDistanceInTiles(dialog.map.Tile, s.Tile).ToStringDecimalIfSmall().Named("TILES")
                ).Resolve(),
            ]);
        }
        return tip;
    }

    public static TransferableUIUtility.ExtraInfo? ExtraInfo {
        get {
            if (!extraInfoDirty) return extraInfoCache;
            extraInfoDirty = false;
            if (dialog == null || settlements.Empty()) return extraInfoCache = null;

            string tip = BuildTip();
            Settlement s = settlements.ElementAtOrDefault(selectedIndex) ?? settlements[0];
            Faction f = s.Faction;
            int goodwillGive = Goodwill(s);
            FactionRelationKind nextKind = f.NextKind(goodwillGive, out int next);
            string value = "GoodwillPreview.LabelGoodwillXToY".Translate(
                "GoodwillPreview.LabelGoodwillFormat".GoodwillFormat(f).Named("CURRENT"),
                "GoodwillPreview.LabelGoodwillFormat".GoodwillFormat(nextKind, next).Named("NEXT"),
                goodwillGive.ToStringWithSign().Named("DELTA")
            ).Resolve();
            // keyは空にしてPostfixでアイコン+Truncateテキストを自前描画
            TransferableUIUtility.ExtraInfo entry = new("", value, Color.white, tip, -9999f);

            // secondValueが空の場合secondColorは未使用なのでマーカーとして流用
            if (entry.secondValue.NullOrEmpty()) entry.secondColor = OurMarker;

            return extraInfoCache = entry;
        }
    }

    public static TaggedString GoodwillFormat(string key, FactionRelationKind kind, int goodwill) {
        return key.Translate(
            kind.ColorGoodwill(goodwill).Named("VALUE"),
            kind.ColorLabel().Named("LABEL")
        ).Resolve();
    }

    private static IEnumerable<Widgets.DropdownMenuElement<int>> DropdownOptions() {
        foreach ((Settlement s, int i) in settlements.Select((v, i) => (v, i))) {
            Faction f = s.Faction;
            int goodwillGive = Goodwill(s);
            FactionRelationKind nextKind = f.NextKind(goodwillGive, out int next);
            string label = string.Concat([
                "GoodwillPreview.DropdownFactionHeader".Translate(
                    f.Named("FACTION"),
                    f.def.LabelCap.Resolve().Named("FACTION_label")
                    ),
                "\n",
                "GoodwillPreview.DropdownGoodwillXToY".Translate(
                    "GoodwillPreview.DropdownGoodwillFormat".GoodwillFormat(f).Named("CURRENT"),
                    "GoodwillPreview.DropdownGoodwillFormat".GoodwillFormat(nextKind, next).Named("NEXT"),
                    goodwillGive.ToStringWithSign().Named("DELTA")
                ).Resolve()
            ]);
            yield return new Widgets.DropdownMenuElement<int> {
                option = new FloatMenuOption(
                    label,
                    () => { selectedIndex = i; extraInfoDirty = true; },
                    f.def.FactionIcon, f.Color),
                payload = i
            };
        }
    }

    [HarmonyPatch(typeof(TransferableUIUtility))]
    [HarmonyPatch(nameof(TransferableUIUtility.DrawExtraInfo)), HarmonyPrefix]
    public static void DrawExtraInfo_Prefix(ref List<TransferableUIUtility.ExtraInfo> info, ref Rect rect) {
        if (dialog == null || settlements.Empty()) return;
        if (ExtraInfo is { } e) {
            if (Find.WindowStack.Windows.Any(w => w is FloatMenu)) e.tip = "";
            info.Add(e);
        }
    }

    [HarmonyPatch(typeof(TransferableUIUtility))]
    [HarmonyPatch(nameof(TransferableUIUtility.DrawExtraInfo)), HarmonyPostfix]
    public static void DrawExtraInfo_Postfix(List<TransferableUIUtility.ExtraInfo> info, Rect rect) {
        if (dialog == null || settlements.Empty()) return;
        if (rect.width > info.Count * 230f) {
            rect.x += Mathf.Floor((rect.width - info.Count * 230f) / 2f);
            rect.width = info.Count * 230f;
        }
        float colWidth = Mathf.Floor(rect.width / info.Count);
        float x = 0f;
        Faction displayed = (settlements.ElementAtOrDefault(selectedIndex) ?? settlements[0]).Faction;
        Widgets.BeginGroup(rect);
        foreach (var entry in info) {
            float entryX = x;
            x += colWidth;
            if (entry.secondColor != OurMarker) continue;

            Rect keyRect = new(entryX, 0f, colWidth, rect.height / 2f);

            // アイコン+派閥名をkey領域に描画（バニラのkey描画はentry.key=""で無効化済み）
            const float iconSize = 16f;
            const float pad = 2f;
            Text.Font = GameFont.Tiny;
            float maxNameWidth = keyRect.width - iconSize - pad;
            string nameText = displayed.name.Truncate(maxNameWidth);
            float nameWidth = Mathf.Min(Text.CalcSize(nameText).x, maxNameWidth);
            float blockWidth = iconSize + pad + nameWidth;
            Rect blockRect = new Rect(0f, 0f, blockWidth, keyRect.height).CenteredOnXIn(keyRect);
            Widgets.BeginGroup(blockRect);
            Rect iconArea = new(0f, 0f, iconSize, blockRect.height);
            Rect iconRect = new Rect(0f, 0f, iconSize, iconSize).CenteredOnYIn(iconArea).CenteredOnXIn(iconArea);
            Rect nameRect = new(iconArea.width + pad, 0f, nameWidth, blockRect.height);
            GUI.color = displayed.Color;
            GUI.DrawTexture(iconRect, displayed.def.FactionIcon);
            GUI.color = Color.gray;
            Widgets.Label(nameRect, nameText);
            Widgets.EndGroup();
            GUI.color = Color.white;

            // クリックでドロップダウン
            Rect clickRect = new(entryX, 0f, rect.width - entryX, rect.height);
            if (Widgets.ButtonInvisible(clickRect))
                Find.WindowStack.Add(new FloatMenu([.. DropdownOptions().Select(o => o.option)]));
        }
        Widgets.EndGroup();
    }
}
