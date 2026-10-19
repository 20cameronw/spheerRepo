using System;
using System.Collections.Generic;
using UnityEngine;

namespace Frontier.Hyperspace.View
{
    /// <summary>The single table that maps the simulation's loot kinds to the game's resource ids.</summary>
    public static class HyperspaceLoot
    {
        /// <summary>Indexed by <see cref="LootKind"/>: Salvage is scrap, Component is alloy (the nearest component resource that exists today), Core is the Xeno Core.</summary>
        public static readonly string[] ResourceIds = { "scrap", "alloy", "core" };

        public static string IdFor(LootKind kind)
        {
            int i = (int)kind;
            return i >= 0 && i < ResourceIds.Length ? ResourceIds[i] : null;
        }

        /// <summary>Amount credited for a raw loot count: scaled by the run's yield multiplier (0.4 + 1.1 x quality), rounded, never below 1 for a drop.</summary>
        public static int Scale(int rawCount, float yieldMultiplier)
        {
            if (rawCount <= 0) return 0;
            return Mathf.Max(1, Mathf.RoundToInt(rawCount * Mathf.Max(0f, yieldMultiplier)));
        }

        public static List<ItemAmount> Build(LootDrop[] drops, float yieldMultiplier, Func<string, ResourceSO> find)
        {
            var list = new List<ItemAmount>();
            if (drops == null || find == null) return list;
            for (int i = 0; i < drops.Length; i++)
            {
                var res = find(IdFor(drops[i].kind));
                int n = Scale(drops[i].count, yieldMultiplier);
                if (res != null && n > 0) list.Add(new ItemAmount(res, n));
            }
            return list;
        }
    }

    /// <summary>Converts a simulation <see cref="RunResult"/> into what the travel flow understands.</summary>
    public static class HyperspaceResults
    {
        public static TravelRunResult ToTravelResult(RunResult r, Func<string, ResourceSO> find)
        {
            bool completed = r.reachedEnd && !r.defeated;
            return new TravelRunResult
            {
                // The flow credits floor(fuelCost x travelRefundMax x quality); the simulation's refund fraction already contains the fuel canister bonus.
                quality = completed ? r.fuelRefundFraction : 0f,
                completed = completed,
                revivesUsed = r.revivesUsed,
                kills = r.killCount,
                cleanRun = r.cleanRun,
                grade = r.grade.ToString(),
                loot = completed ? HyperspaceLoot.Build(r.lootList, r.yieldMultiplier, find) : new List<ItemAmount>(),
                xp = completed ? r.xp : 0
            };
        }

        /// <summary>Fuel the flow will refund for this result (the same formula as UIRoot.TravelSequence).</summary>
        public static int FuelRefund(int fuelCost, float travelRefundMax, TravelRunResult result)
        {
            if (!result.completed) return 0;
            return Mathf.FloorToInt(fuelCost * travelRefundMax * result.quality);
        }
    }

    /// <summary>Builds the simulation inputs of a leg.</summary>
    public static class HyperspaceSetup
    {
        static EncounterLibrary library;

        /// <summary>The encounter library is immutable: built once and shared by every run.</summary>
        public static EncounterLibrary Library
        {
            get
            {
                if (library != null) return library;
                var catalog = Resources.Load<EncounterCatalogSO>("Frontier/EncounterCatalog");
                if (catalog == null) throw new InvalidOperationException("Hyperspace encounter catalog is missing.");
                library = catalog.BuildLibrary();
                return library;
            }
        }

        public static RunParams BuildParams(TravelRunRequest request, ShipStats ship, HyperspaceTuning tuning, bool assist)
        {
            // Same-system hops report a distance of zero light years: the simulation expects at least 1.
            float ly = (float)Math.Max(1.0, double.IsNaN(request.distanceLy) ? 1.0 : request.distanceLy);
            float plannedDuration = request.seconds > 0 ? request.seconds : 0f;
            return new RunParams(request.seed, ly, request.danger, Mathf.Clamp(request.tier, 0, 12), ship.Power(tuning), assist ? RunMode.Assist : RunMode.Normal, plannedDuration);
        }

        public static HyperspaceRun CreateRun(TravelRunRequest request, ShipStats ship, HyperspaceTuning tuning, bool assist)
        {
            return HyperspaceRun.Create(BuildParams(request, ship, tuning, assist), ship, tuning, Library);
        }
    }
}
