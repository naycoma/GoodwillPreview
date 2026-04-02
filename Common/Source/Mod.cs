using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using Verse;
using RimWorld;

using RimWorld.Planet;
using System;

namespace GoodwillPreview;

public class GoodwillDropdownMenu : FloatMenu {
    public GoodwillDropdownMenu(List<FloatMenuOption> options) : base(options) {
        absorbInputAroundWindow = true;
    }

    public static GoodwillDropdownMenu FromSettlements(IEnumerable<Settlement> settlements, Action<int> action) {
        return new GoodwillDropdownMenu([.. DropdownOptions(settlements, action).Select(e => e.option)]);
    }

    private static IEnumerable<Widgets.DropdownMenuElement<int>> DropdownOptions(IEnumerable<Settlement> settlements, Action<int> action) {
        foreach ((Settlement s, int i) in settlements.Select((v, i) => (v, i))) {
            Faction f = s.Faction;
            var (current, next, delta) = Mod.TransferableGoodwillXtoY(s);

            string label = string.Concat([
                "GoodwillPreview.DropdownFactionHeader".Translate(
                    f.Named("FACTION"),
                    f.def.LabelCap.Resolve().Named("FACTION_label")
                    ),
                "\n",
                "GoodwillPreview.DropdownGoodwillXToY".Translate(
                    "GoodwillPreview.DropdownGoodwillFormat".GoodwillFormat(current).Named("CURRENT"),
                    "GoodwillPreview.DropdownGoodwillFormat".GoodwillFormat(next).Named("NEXT"),
                    delta.ToStringWithSign().Named("DELTA")
                ).Resolve()
            ]);

            yield return new Widgets.DropdownMenuElement<int> {
                option = new FloatMenuOption(
                    label,
                    () => { action(i); },
                    f.def.FactionIcon, f.Color),
                payload = i
            };
        }
    }
}

public static class Mod {
    private static Dialog_LoadTransporters dialog = null;
    private static readonly List<Settlement> settlements = [];

    private static readonly Dictionary<Faction, int> goodwillsCache = [];
    private static bool extraInfoDirty = true;
    private static TransferableUIUtility.ExtraInfo? extraInfoCache = null;

    // secondValueが空の時secondColorは描画されないので識別子として流用
    private static readonly Color OurMarker = new(0f, 1f / 255f, 2f / 255f, 3f / 255f);
    private static int selectedIndex = -1;

    public static void Open(Dialog_LoadTransporters dialog) => Mod.dialog = dialog;

    public static void Close() => dialog = null;

    public static void ReloadSettlements(IEnumerable<Settlement> newSettlements) {
        goodwillsCache.Clear();
        settlements.Clear();
        settlements.AddRange(newSettlements);
        settlements.SortBy(s => s.Faction.loadID);

        if (settlements.Empty()) Log.Warning("No settlements found within range.");

        // Clamp selectedIndex to avoid out-of-range after settlements change
        CurrentSettlement();
    }
    public static bool Empty() => dialog == null || settlements.Empty();

    public static void ChangedCount() {
        goodwillsCache.Clear();
        extraInfoDirty = true;
    }

    public static int TransferableGoodwill(Settlement settlement) {
        if (goodwillsCache.TryGetValue(settlement.Faction, out int goodwill)) return goodwill;
        goodwill = settlement.GoodwillDeltaFor(dialog.transferables);
        return goodwillsCache[settlement.Faction] = goodwill;
    }

    public struct KindAndGoodwill(int goodwill, FactionRelationKind kind) {
        public int goodwill = goodwill;
        public FactionRelationKind kind = kind;
    }

    public static (KindAndGoodwill current, KindAndGoodwill next, int delta) TransferableGoodwillXtoY(Settlement s) {
        int delta = TransferableGoodwill(s);
        Faction f = s.Faction;
        FactionRelationKind nextKind = f.NextKind(delta, out int nextGoodwill);
        return (new(f.PlayerGoodwill, f.PlayerRelationKind), new(nextGoodwill, nextKind), delta);
    }

    private static string BuildTip() {
        string tip = "GoodwillPreview.GiftGoodwillIncreaseTip".Translate();
        foreach (Settlement s in settlements) {
            var (current, next, delta) = TransferableGoodwillXtoY(s);
            Faction f = s.Faction;
            tip = string.Concat([
                tip,
                "\n\n",
                "GoodwillPreview.TipFactionHeader".Translate(f.Named("FACTION")),
                "\n   ",
                "GoodwillPreview.TipGoodwillXToY".Translate(
                    "GoodwillPreview.TipGoodwillFormat".GoodwillFormat(current).Named("CURRENT"),
                    "GoodwillPreview.TipGoodwillFormat".GoodwillFormat(next).Named("NEXT"),
                    delta.ToStringWithSign().Named("DELTA")
                ).Resolve(),
                "\n   ",
                "GoodwillPreview.Tiles".Translate(
                    Find.WorldGrid.ApproxDistanceInTiles(dialog.map.Tile, s.Tile).ToStringDecimalIfSmall().Named("TILES")
                ).Resolve(),
            ]);
        }
        return tip;
    }

    public static TransferableUIUtility.ExtraInfo? ExtraInfo(bool includeTip = true) {
        if (!extraInfoDirty && extraInfoCache != null) return extraInfoCache;
        extraInfoDirty = false;
        if (Empty()) return extraInfoCache = null;

        string tip = includeTip ? BuildTip() : "";
        var (current, next, delta) = TransferableGoodwillXtoY(CurrentSettlement());
        string value = "GoodwillPreview.LabelGoodwillXToY".Translate(
            "GoodwillPreview.LabelGoodwillFormat".GoodwillFormat(current).Named("CURRENT"),
            "GoodwillPreview.LabelGoodwillFormat".GoodwillFormat(next).Named("NEXT"),
            delta.ToStringWithSign().Named("DELTA")
        ).Resolve();
        // keyは空にしてPostfixでアイコン+Truncateテキストを自前描画
        return extraInfoCache = new("", value, Color.white, tip, -9999f) {
            // secondValueが空の場合secondColorは未使用なのでマーカーとして流用
            secondColor = OurMarker
        };
    }

    public static bool IsMarkedExtraInfo(TransferableUIUtility.ExtraInfo info) => info.secondColor == OurMarker;

    public static TaggedString GoodwillFormat(this string key, KindAndGoodwill value) {
        return key.Translate(
            value.goodwill.ToStringWithSign().Named("VALUE"),
            value.kind.GetLabelCap().Named("LABEL")
        ).Colorize(value.kind.GetColor());
    }

    public static bool ButtonGoodwillExtraInfo(Rect rect, Faction current) {
        Rect keyRect = rect.TopHalf();

        // アイコン+派閥名をkey領域に描画（バニラのkey描画はentry.key=""で無効化済み）
        const float iconSize = 16f;
        const float pad = 2f;
        Text.Font = GameFont.Tiny;
        float maxNameWidth = keyRect.width - iconSize - pad;
        string nameText = current.name.Truncate(maxNameWidth);
        float nameWidth = Mathf.Min(Text.CalcSize(nameText).x, maxNameWidth);
        float blockWidth = iconSize + pad + nameWidth;
        Rect blockRect = keyRect.LeftPartPixels(blockWidth).CenteredOnXIn(keyRect);

        Widgets.BeginGroup(blockRect);
        blockRect = blockRect.AtZero();

        Rect iconArea = blockRect.LeftPartPixels(iconSize);
        Rect iconRect = new Rect(0f, 0f, iconSize, iconSize).CenteredOnYIn(iconArea).CenteredOnXIn(iconArea);
        Rect nameRect = blockRect.RightPartPixels(nameWidth);

        GUI.color = current.Color;
        GUI.DrawTexture(iconRect, current.def.FactionIcon);
        GUI.color = Color.gray;
        Widgets.Label(nameRect, nameText);

        Widgets.EndGroup();
        GUI.color = Color.white;

        return Widgets.ButtonInvisible(rect);
    }

    public static Settlement CurrentSettlement() {
        return settlements[selectedIndex = Mathf.Clamp(selectedIndex, 0, settlements.Count - 1)];
    }

    public static GoodwillDropdownMenu CreateDropdownMenu() {
        return GoodwillDropdownMenu.FromSettlements(settlements, (idx) => {
            selectedIndex = idx;
            extraInfoDirty = true;
        });
    }
}