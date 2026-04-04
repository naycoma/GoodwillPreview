using System.Linq;
using System.Collections.Generic;

using UnityEngine;
using Verse;
using RimWorld;

using RimWorld.Planet;
using System.Diagnostics;

namespace GoodwillPreview;

public static class FactionExtensions {
    public static string ColorGoodwill(this FactionRelationKind kind, int goodwill) {
        return goodwill.ToStringWithSign().Colorize(kind.GetColor());
    }

    public static string ColorLabel(this FactionRelationKind kind) {
        return kind.GetLabelCap().Colorize(kind.GetColor());
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

public static class TransferableFactionGiftUtility {
    // ref FactionGiftUtility.GetGoodwillChange
    public static int GetGoodwillChange(IEnumerable<ThingCount> thingCounts, Settlement giveTo, Dictionary<Thing, float> singlePricesCache) {
        float num = 0f;
        foreach (var (thing, count) in thingCounts.Select(tc => (tc.thing, tc.Count))) {
            if (!singlePricesCache.TryGetValue(thing, out float singlePrice))
                singlePricesCache[thing] = singlePrice = GetSinglePrice(thing, giveTo);
            num += FactionGiftUtility.GetBaseGoodwillChange(thing, count, singlePrice, giveTo.Faction);
        }
        return FactionGiftUtility.PostProcessedGoodwillChange(num, giveTo.Faction);
    }
    
    // ref FactionGiftUtility.GetGoodwillChange
    public static float GetSinglePrice(Thing thing, Settlement giveTo) {
        if (thing.def == ThingDefOf.Silver) return thing.MarketValue;

        float priceFactorSell_TraderPriceType = (giveTo.TraderKind != null) ? giveTo.TraderKind.PriceTypeFor(thing.def, TradeAction.PlayerSells).PriceMultiplier() : 1f;
        return TradeUtility.GetPricePlayerSell(thing, priceFactorSell_TraderPriceType, 1f, 0f, giveTo.TradePriceImprovementOffsetForPlayer, 0f, 0f);
    }

    private static readonly List<ThingCount> tmpThingCounts = [];

    public static int CalculateGoodwillChange(List<TransferableOneWay> transferables, Settlement giveTo, Dictionary<Thing, float> singlePricesCache) {
        Stopwatch sw = Stopwatch.StartNew();
        tmpThingCounts.Clear();
        foreach (TransferableOneWay transferable in transferables) {
            TransferableUtility.TransferNoSplit(transferable.things, transferable.CountToTransfer, delegate (Thing originalThing, int toTake) {
                if (toTake <= 0) return;
                tmpThingCounts.Add(new ThingCount(originalThing, toTake));
            }, removeIfTakingEntireThing: false, errorIfNotEnoughThings: false);
        }
        foreach (Thing thing in tmpThingCounts.Select(tc => tc.thing)) {
            if (singlePricesCache.ContainsKey(thing)) continue;
            singlePricesCache[thing] = GetSinglePrice(thing, giveTo);
        }
        int result = GetGoodwillChange(tmpThingCounts, giveTo, singlePricesCache);
        sw.Stop();
        if (Patch.DEBUG) Log.Message($"[{nameof(CalculateGoodwillChange)}] Calculated goodwill change: ${result} in {sw.ElapsedMilliseconds} ms");
        tmpThingCounts.Clear();
        return result;
    }
}

public static class SettlementUtility {
    public static int GoodwillDeltaFor(this Settlement giveTo, List<TransferableOneWay> gifts, Dictionary<Thing, float> singlePricesCache) {
        int goodwillChange = TransferableFactionGiftUtility.CalculateGoodwillChange(gifts, giveTo, singlePricesCache);
        int current = giveTo.Faction.PlayerGoodwill;
        int next = Mathf.Clamp(current + goodwillChange, -100, 100);
        return next - current;
    }

    public static IEnumerable<Settlement> GetSettlementsWithinRadius(int centerTile) {
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

public static class EnumerableExtensions {
    public static IEnumerable<(T item, int index)> Index<T>(this IEnumerable<T> source) {
        int i = 0;
        foreach (var item in source) yield return (item, i++);
    }
}