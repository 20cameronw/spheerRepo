using System;
using System.Collections.Generic;
using Frontier.Universe;

namespace Frontier
{
    public partial class GameState
    {
        public TravelCheckpoint PendingTravel { get; private set; }

        public bool BeginTravelCheckpoint(RoutePlan plan, out string message)
        {
            message = "";
            if (PendingTravel != null || SaveIsReadOnly || plan == null || !plan.reachable || plan.legs == null || plan.LegCount == 0)
            {
                message = UiText.Get("travel.recovery.cannot-start");
                return false;
            }
            var checkpoint = new TravelCheckpoint { plan = plan, hasDepartureCargoFill = true, departureCargoFill = CargoFill };
            foreach (var leg in plan.legs)
                checkpoint.legs.Add(new TravelLegCheckpoint
                {
                    seed = SeedText.Format(Hash.Fnv1a64(leg.fromSystemId + ">" + leg.toSystemId) ^
                        (ulong)(uint)UnityEngine.Random.Range(int.MinValue, int.MaxValue)),
                    autopilotRemaining = leg.seconds
                });
            PendingTravel = checkpoint;
            if (!ValidatePendingTravel(out _, out message))
            {
                PendingTravel = null;
                return false;
            }
            saves.MarkDirty();
            if (saves.Save()) return true;
            PendingTravel = null;
            saves.MarkDirty();
            message = UiText.Get("travel.recovery.save-failed", saves.LastError);
            return false;
        }

        public bool ValidatePendingTravel(out PlanetData target, out string message)
        {
            target = null;
            message = UiText.Get("travel.recovery.invalid");
            var trip = PendingTravel;
            if (trip == null || SaveIsReadOnly || trip.version != TravelCheckpoint.CurrentVersion ||
                trip.plan == null || trip.plan.legs == null || trip.plan.LegCount < 1 ||
                trip.plan.LegCount > Frontier.Economy.RoutePlanner.MaxExpansions ||
                trip.legs == null || trip.legs.Count != trip.plan.LegCount || trip.nextLeg < 0 || trip.nextLeg > trip.legs.Count ||
                trip.hasDepartureCargoFill && (double.IsNaN(trip.departureCargoFill) || double.IsInfinity(trip.departureCargoFill) ||
                    trip.departureCargoFill < 0 || trip.departureCargoFill > 1) ||
                trip.plan.fromPlanetId != planet.Id || string.IsNullOrEmpty(trip.plan.toPlanetId)) return false;
            target = universe.Get(trip.plan.toPlanetId);
            if (target == null || !SameTravelPlan(trip.plan, PlanRoute(target, trip.plan.autopilot))) return false;
            for (int i = 0; i < trip.legs.Count; i++)
            {
                var leg = trip.legs[i];
                if (leg == null || !SeedText.TryParse(leg.seed, out _) || leg.finished != (i < trip.nextLeg) ||
                    float.IsNaN(leg.quality) || float.IsInfinity(leg.quality) || leg.quality < 0 || leg.quality > 1 ||
                    leg.xp < 0 || leg.loot == null || leg.loot.Count > db.resources.Length ||
                    leg.freeRevives < 0 || leg.freeRevives > Math.Max(0, FrontierSettings.I.freeRevivesPerRun) ||
                    float.IsNaN(leg.autopilotRemaining) || float.IsInfinity(leg.autopilotRemaining) ||
                    leg.autopilotRemaining < 0 || leg.autopilotRemaining > trip.plan.legs[i].seconds) return false;
                foreach (var item in leg.loot)
                    if (item == null || item.count <= 0 || db.ResolveResource(item.resourceId) == null) return false;
                if (!leg.completed && (leg.xp != 0 || leg.loot.Count != 0 || leg.quality != 0)) return false;
            }
            message = "";
            return true;
        }

        static bool SameTravelPlan(RoutePlan a, RoutePlan b)
        {
            if (!a.reachable || !b.reachable || a.fromPlanetId != b.fromPlanetId || a.toPlanetId != b.toPlanetId ||
                a.totalFuel != b.totalFuel || a.totalSeconds != b.totalSeconds || a.LegCount != b.LegCount) return false;
            if (a.instant != b.instant || a.autopilot != b.autopilot) return false;
            for (int i = 0; i < a.LegCount; i++)
            {
                var x = a.legs[i]; var y = b.legs[i];
                // JsonUtility can round-trip a route double one ULP away from the planner's value.
                bool sameLength = Math.Abs(x.lengthLy - y.lengthLy) <= 1e-12 * Math.Max(1, Math.Abs(y.lengthLy));
                if (x.fromSystemId != y.fromSystemId || x.toSystemId != y.toSystemId || x.fuel != y.fuel ||
                    x.seconds != y.seconds || !sameLength || x.intraSystem != y.intraSystem) return false;
            }
            return true;
        }

        public void MarkTravelDirty()
        {
            if (PendingTravel != null) saves.MarkDirty();
        }

        public bool CompleteTravelLeg(bool completed, float quality, int xp, List<ItemAmount> loot)
        {
            var trip = PendingTravel;
            if (trip == null || trip.nextLeg >= trip.legs.Count) throw new InvalidOperationException("No travel leg is awaiting completion.");
            var leg = trip.legs[trip.nextLeg];
            leg.replay?.Capture();
            if (leg.replay != null) leg.replay.LiveRun = null;
            leg.completed = completed;
            leg.quality = completed ? quality : 0;
            leg.xp = completed ? xp : 0;
            leg.loot.Clear();
            if (completed && loot != null)
                foreach (var item in loot)
                    if (item.resource != null && item.amount > 0)
                        leg.loot.Add(new StackRecord { resourceId = item.resource.id, count = item.amount });
            leg.finished = true;
            trip.nextLeg++;
            saves.MarkDirty();
            return saves.Save();
        }

        public bool SettlePendingTravel(out int refund, out string message)
        {
            refund = 0;
            message = UiText.Get("travel.recovery.invalid");
            if (!ValidatePendingTravel(out var target, out _) || PendingTravel.nextLeg != PendingTravel.legs.Count) return false;
            var trip = PendingTravel;
            if (!TravelInternal(trip.plan, target, false)) return false;
            for (int i = 0; i < trip.legs.Count; i++)
            {
                var leg = trip.legs[i];
                if (!leg.completed) continue;
                refund += RefundLeg(trip.plan.legs[i], leg.quality, trip.plan.autopilot);
                foreach (var item in leg.loot) Add(db.ResolveResource(item.resourceId), item.count);
                if (leg.xp > 0) AddXp(leg.xp);
            }
            PendingTravel = null;
            saves.MarkDirty();
            message = saves.Save() ? "" : UiText.Get("travel.recovery.pending-save");
            return true;
        }

        public bool DiscardPendingTravel(out string message)
        {
            var trip = PendingTravel;
            PendingTravel = null;
            if (SaveIsReadOnly) { message = UiText.Get("travel.recovery.read-only"); return true; }
            saves.MarkDirty();
            if (saves.Save()) { message = ""; return true; }
            PendingTravel = trip;
            saves.MarkDirty();
            message = UiText.Get("travel.recovery.save-failed", saves.LastError);
            return false;
        }
    }
}
