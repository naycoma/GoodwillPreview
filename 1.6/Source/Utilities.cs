using System.Linq;
using System.Collections.Generic;

using UnityEngine;
using Verse;
using RimWorld;

using RimWorld.Planet;

namespace GoodwillPreview;

public static class FactionUtility {
    public static string ColorGoodwill(this FactionRelationKind kind, int goodwill) {
        return goodwill.ToStringWithSign().Colorize(kind.GetColor());
    }
    public static string ColorLabel(this FactionRelationKind kind) {
        return kind.GetLabelCap().Colorize(kind.GetColor());
    }
}

public static class TransferableFactionGiftUtility {
    public static int GetGoodwillChange(List<TransferableOneWay> transferables, Settlement giveTo) {
        int goodwillChange = CalculateGoodwillChange(transferables, giveTo);
        int current = giveTo.Faction.PlayerGoodwill;
        int next = Mathf.Clamp(current + goodwillChange, -100, 100);
        return next - current;
    }

    private static int CalculateGoodwillChange(List<TransferableOneWay> transferables, Settlement giveTo) {
        float num = 0f;
        foreach (TransferableOneWay transferable in transferables) {
            TransferableUtility.TransferNoSplit(transferable.things, transferable.CountToTransfer, delegate (Thing originalThing, int toTake) {
                float singlePrice;
                if (originalThing.def == ThingDefOf.Silver) {
                    singlePrice = originalThing.MarketValue;
                } else {
                    float priceFactorSell_TraderPriceType = (giveTo.TraderKind != null) ? giveTo.TraderKind.PriceTypeFor(originalThing.def, TradeAction.PlayerSells).PriceMultiplier() : 1f;
                    singlePrice = TradeUtility.GetPricePlayerSell(originalThing, priceFactorSell_TraderPriceType, 1f, 0f, giveTo.TradePriceImprovementOffsetForPlayer, 0f, 0f);
                }

                num += FactionGiftUtility.GetBaseGoodwillChange(originalThing, toTake, singlePrice, giveTo.Faction);
            }, removeIfTakingEntireThing: false, errorIfNotEnoughThings: false);
        }
        return FactionGiftUtility.PostProcessedGoodwillChange(num, giveTo.Faction);
    }

    public static FactionRelationKind NextKind(this Faction faction, int giveTo, out int newGoodwill) {
        newGoodwill = faction.PlayerGoodwill;
        if (faction.def.permanentEnemy) return faction.PlayerRelationKind;
        newGoodwill += giveTo;
        FactionRelationKind newKind = FactionUIUtility.GetRelationKindForGoodwill(newGoodwill);
        if (newKind != FactionRelationKind.Neutral) return newKind;
        if (faction.PlayerRelationKind == FactionRelationKind.Hostile && newGoodwill < 0) return FactionRelationKind.Hostile;
        if (faction.PlayerRelationKind == FactionRelationKind.Ally && newGoodwill > 0) return FactionRelationKind.Ally;
        return FactionRelationKind.Neutral;
    }
}

public static class SettlementUtility {
    public static IEnumerable<Settlement> GetSettlementsWithinRadius(int centerTile, float radius) {
        return Find.WorldObjects.Settlements
            .Where(CanGiveGiftTo)
            .GroupBy(settlement => settlement.Faction)
            .Select(group => group.MinBy(s => Find.WorldGrid.ApproxDistanceInTiles(centerTile, s.Tile)))
            .Where(s => s != null);
    }
    public static bool CanGiveGiftTo(Settlement settlement) {
        if (settlement?.Faction == null) return false;
        return settlement.Spawned && settlement.Faction != Faction.OfPlayer && !settlement.Faction.def.permanentEnemy && !settlement.HasMap;
    }
}

public static class TranslateUtility {
    public static TaggedString GoodwillFormat(this string key, Faction f) {
        FactionRelationKind kind = f.PlayerRelationKind;
        return key.Translate(
            f.PlayerGoodwill.ToStringWithSign().Named("VALUE"),
            kind.GetLabelCap().Named("LABEL")
        ).Colorize(kind.GetColor());
    }
    public static TaggedString GoodwillFormat(this string key, FactionRelationKind kind, int goodwill) {
        return key.Translate(
            goodwill.ToStringWithSign().Named("VALUE"),
            kind.GetLabelCap().Named("LABEL")
        ).Colorize(kind.GetColor());
    }
}