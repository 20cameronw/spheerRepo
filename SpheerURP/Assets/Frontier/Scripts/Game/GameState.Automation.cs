using System;
using System.Collections.Generic;
using Frontier.Universe;
using UnityEngine;

namespace Frontier
{
    public partial class GameState
    {
        const double AutoHarvesterAverageQuality = 0.5;

        bool AccrueOfflineHarvesters(string lastSavedUtc)
        {
            if (SaveIsReadOnly || !UtcText.TryParse(lastSavedUtc, out var lastSaved)) return false;
            double gapHours = (clock.UtcNow - lastSaved).TotalHours;
            if (gapHours <= 0.0) return false;

            bool changed = false;
            foreach (var structure in new List<PlacedStructure>(structures))
            {
                if (structure.def.kind != StructureKind.AutoHarvester || !IsOperational(structure) ||
                    !string.IsNullOrEmpty(structure.record.buildEndUtc)) continue;

                float rate = Mathf.Max(0f, structure.def.EffectAt(structure.level));
                float capHours = Mathf.Max(0f, structure.def.CapacityAt(structure.level));
                if (rate <= 0f || capHours <= 0f) continue;

                var spot = LeastStockedAutoHarvestSpot(universe.Get(structure.planetId), structure);
                if (spot == null) continue;

                double remainder = structure.record.offlineRunRemainder;
                if (double.IsNaN(remainder) || double.IsInfinity(remainder) || remainder < 0.0 || remainder >= 1.0)
                    remainder = 0.0;
                double runs = remainder + rate * Math.Min(gapHours, capHours);
                long wholeRuns = (long)Math.Floor(runs);
                structure.record.offlineRunRemainder = runs - wholeRuns;
                changed = true;

                float gatherBonus = AutoHarvestStructureBonus(universe.Get(structure.planetId), spot);
                double perRun = spot.baseYield * (1.0 + gatherBonus + TerraformerHarvestBonus) *
                    (0.4 + 1.1 * AutoHarvesterAverageQuality);
                long amount = (long)Math.Floor(wholeRuns * perRun);
                int requested = (int)Math.Min(int.MaxValue, Math.Max(0L, amount));
                int stored = AddToHarvesterHopper(structure, spot.resource, requested);
                if (stored > 0)
                {
                    RecordAtlasResourceAcquired(spot.resource.id);
                    saves.MarkDirty();
                }
            }

            if (changed) world.Touch();
            return changed;
        }

        List<GatherSpot> AutoHarvestSpots(PlanetData p)
        {
            var byResource = new Dictionary<string, GatherSpot>(StringComparer.Ordinal);
            if (p == null) return new List<GatherSpot>();
            AddAutoHarvestSpots(byResource, p.Spots);
            AddAutoHarvestSpots(byResource, p.SpotsFor(MinigameKind.Mine));
            foreach (var game in new[] { MinigameKind.Chop, MinigameKind.Salvage })
                AddAutoHarvestSpots(byResource, p.SpotsFor(game));
            return new List<GatherSpot>(byResource.Values);
        }

        static void AddAutoHarvestSpots(Dictionary<string, GatherSpot> byResource, IEnumerable<GatherSpot> spots)
        {
            foreach (var spot in spots)
            {
                if (spot == null || spot.resource == null ||
                    (spot.resource.id != "wood" && spot.resource.id != "stone" && spot.resource.id != "scrap")) continue;
                GatherSpot current;
                if (!byResource.TryGetValue(spot.resource.id, out current) || spot.baseYield > current.baseYield)
                    byResource[spot.resource.id] = spot;
            }
        }

        GatherSpot LeastStockedAutoHarvestSpot(PlanetData p, PlacedStructure harvester)
        {
            GatherSpot best = null;
            double bestValue = double.MaxValue;
            var pooled = PooledWarehouseStorages(p);
            StorageRecord hopper = CargoDepotOn(p) ? null : StorageFor(harvester);
            foreach (var spot in AutoHarvestSpots(p))
            {
                long held = backpack.Count(spot.resource.id);
                foreach (var storage in pooled) held += WarehouseOps.Held(storage, spot.resource.id);
                if (hopper != null) held += WarehouseOps.Held(hopper, spot.resource.id);
                double value = held * Math.Max(0.0, spot.resource.baseValue);
                if (value < bestValue || (Math.Abs(value - bestValue) < 0.000001 &&
                    (best == null || string.CompareOrdinal(spot.resource.id, best.resource.id) < 0)))
                {
                    best = spot;
                    bestValue = value;
                }
            }
            return best;
        }

        bool CargoDepotOn(PlanetData p)
        {
            return p != null && StructureOfKind(p, StructureKind.CargoDepot) != null;
        }

        float AutoHarvestStructureBonus(PlanetData p, GatherSpot spot)
        {
            StructureKind kind = spot.game == MinigameKind.Chop ? StructureKind.Lumber :
                spot.game == MinigameKind.Mine ? StructureKind.Mine :
                spot.game == MinigameKind.Salvage ? StructureKind.SalvageYard : StructureKind.Drill;
            return StructureBonus(StructureOfKind(p, kind));
        }

        int AddToHarvesterHopper(PlacedStructure harvester, ResourceSO resource, int amount)
        {
            if (resource == null || amount <= 0) return 0;
            var storage = StorageFor(harvester);
            int stackSize = Math.Max(1, resource.stackSize);
            int remaining = amount;
            foreach (var slot in storage.slots)
            {
                if (slot.count <= 0 || slot.resourceId != resource.id) continue;
                int moved = Math.Min(remaining, Math.Max(0, stackSize - slot.count));
                slot.count += moved;
                remaining -= moved;
                if (remaining == 0) break;
            }
            if (remaining > 0)
            {
                foreach (var slot in storage.slots)
                {
                    if (slot.count > 0) continue;
                    int moved = Math.Min(remaining, stackSize);
                    slot.resourceId = resource.id;
                    slot.count = moved;
                    remaining -= moved;
                    if (remaining == 0) break;
                }
            }
            return amount - remaining;
        }
    }
}
