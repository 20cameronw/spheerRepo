using System;
using System.Collections.Generic;
using UnityEngine;
using Frontier.Economy;

namespace Frontier
{
    // The ship as the travel rules see it, and the route-planning front door of the session.
    public partial class GameState
    {
        /// <summary>Resource ids of the dense fuels (fuel cell = the database's fuel, plasma cell = 8 energy, antimatter pod = 64 energy).</summary>
        public const string PlasmaId = "plasma", AntimatterId = "antimatter";

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Development only: upgrade levels the catalog will own once the content package imports them. Never saved.
        readonly Dictionary<string, int> shipLevelOverrides = new Dictionary<string, int>(StringComparer.Ordinal);
#endif

        /// <summary>The locked travel numbers (jump range, fuel, plating, autopilot); built once per game database.</summary>
        public TravelRules Rules { get { return universe.Rules; } }

        /// <summary>The route planner of the main thread (the map worker has its own).</summary>
        public RoutePlanner Planner { get { return universe.Planner; } }

        /// <summary>Level of a travel upgrade by catalog id (jump, engine, scanner_range, hull_*), 0 while the upgrade does not exist yet.</summary>
        public int ShipLevel(string catalogId)
        {
            int level = progression.UpgradeLevel(catalogId);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            int forced;
            if (shipLevelOverrides.TryGetValue(catalogId, out forced)) level = forced;
#endif
            return level;
        }

        /// <summary>How full the backpack is, 0 to 1 (a full hold burns up to 20% more fuel).</summary>
        public double CargoFill { get { return Slots <= 0 ? 0.0 : Math.Min(1.0, SlotsUsed / (double)Slots); } }

        /// <summary>The ship's capabilities right now, computed from the upgrade levels, the hold and the interim settings.</summary>
        public ShipCapabilities Ship
        {
            get { return ShipFor(planet); }
        }

        ShipCapabilities ShipFor(PlanetData departure)
        {
            var settings = FrontierSettings.I;
            var capabilities = ShipCapabilities.FromUpgradeLevels(ShipLevel, CargoFill, settings.interimJumpLevel,
                settings.interimScannerLevel, settings.enforceHazardPlating);
            double baseRange = Rules.Model.JumpRangeLy(capabilities.jumpLevel);
            capabilities.jumpRangeBonusLy = baseRange * WarpGateEffect(departure);
            capabilities.discount = TravelFuelDiscount(departure);
            return capabilities;
        }

        public float WarpGateEffect(PlanetData departure)
        {
            var gate = departure == null ? null : StructureOfKind(departure, StructureKind.WarpGate);
            return gate != null && gate.def.effectKind == "gateFuelDiscount"
                ? Mathf.Max(0f, gate.def.EffectAt(gate.level))
                : 0f;
        }

        public float TravelFuelDiscount(PlanetData departure)
        {
            var hangar = departure == null ? null : StructureOfKind(departure, StructureKind.Hangar);
            float hangarDiscount = hangar != null && hangar.def.effectKind == "travelFuelDiscount"
                ? Mathf.Max(0f, hangar.def.EffectAt(hangar.level))
                : 0f;
            return Mathf.Min((float)Rules.Model.MaxDiscount, hangarDiscount + WarpGateEffect(departure));
        }

        /// <summary>Radius around every visited system inside which worlds are discovered and surveyed.</summary>
        public double ScannerRangeLy { get { return Rules.ScannerRangeLy(Ship); } }

        /// <summary>The energy items in the backpack (fuel cells, plasma cells, antimatter pods when the game has them).</summary>
        public FuelStock FuelOnBoard
        {
            get
            {
                return new FuelStock(Count(db.fuel), CountById(PlasmaId), CountById(AntimatterId));
            }
        }

        int CountById(string id)
        {
            ResourceSO r;
            return resourceById.TryGetValue(id, out r) ? backpack.Count(id) : 0;
        }

        /// <summary>Plans a trip from the current world. Never null; see <see cref="RoutePlan.reasons"/> when it cannot be taken.</summary>
        public RoutePlan PlanRoute(PlanetData to, bool autopilot = false)
        {
            return PlanRoute(planet, to, autopilot);
        }

        public RoutePlan PlanRoute(PlanetData from, PlanetData to, bool autopilot)
        {
            if (from == null || to == null) return RoutePlan.Blocked(RouteReasons.Unknown);
            return Planner.Plan(universe.EndpointOf(from), universe.EndpointOf(to), ShipFor(from), autopilot, FuelOnBoard);
        }

        /// <summary>Energy units a trip from the current world costs (0 for the world the player is on, or when there is no way there).</summary>
        public int FuelCost(PlanetData to) { return PlanRoute(to).totalFuel; }

        /// <summary>
        /// Takes a planned trip: pays its energy (densest fuel first), moves the player and records the arrival. Returns false, changing
        /// nothing, when the plan is not reachable or the fuel on board no longer covers it. Refunds from the travel mini-game are added by the
        /// caller afterwards (<see cref="Rules"/>.Refund).
        /// </summary>
        public bool Travel(RoutePlan plan, PlanetData to)
        {
            if (plan == null || to == null || !plan.reachable || to.Id == planet.Id) return false;
            if (plan.fromPlanetId != planet.Id || plan.toPlanetId != to.Id) return false;
            // A preview is not authority: inventory, hull and upgrades may have changed while the player was flying.
            RoutePlan current = PlanRoute(to, plan.autopilot);
            if (!current.reachable || current.totalFuel != plan.totalFuel || current.LegCount != plan.LegCount) return false;
            for (int i = 0; i < current.LegCount; i++)
                if (current.legs[i].toSystemId != plan.legs[i].toSystemId || current.legs[i].fuel != plan.legs[i].fuel) return false;
            FuelPayment pay = Rules.Pay(plan.totalFuel, FuelOnBoard);
            if (!pay.ok) return false;
            SpendEnergy(pay);
            planet = to;
            RecordArrival(to);
            Notify();
            Flush();
            return true;
        }

        /// <summary>Plans and takes a trip in one step (tests, debug, scripted travel).</summary>
        public bool Travel(PlanetData to, bool autopilot = false)
        {
            if (to == null || to.Id == planet.Id) return false;
            return Travel(PlanRoute(to, autopilot), to);
        }

        void SpendEnergy(FuelPayment pay)
        {
            var cost = new List<ItemAmount>(3);
            if (pay.fuelCells > 0) cost.Add(new ItemAmount(db.fuel, pay.fuelCells));
            ResourceSO plasma, antimatter;
            if (pay.plasmaCells > 0 && resourceById.TryGetValue(PlasmaId, out plasma)) cost.Add(new ItemAmount(plasma, pay.plasmaCells));
            if (pay.antimatterPods > 0 && resourceById.TryGetValue(AntimatterId, out antimatter)) cost.Add(new ItemAmount(antimatter, pay.antimatterPods));
            SpendQuietly(cost);
        }

        /// <summary>Energy units given back for a leg flown with a given mini-game quality, added to the backpack as fuel cells. Returns what was added.</summary>
        public int RefundLeg(RouteLeg leg, double quality, bool autopilot)
        {
            int refund = Rules.Refund(leg.fuel, quality, autopilot);
            return refund > 0 ? Add(db.fuel, refund) : 0;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Development only: sets a travel upgrade level by catalog id (jump, engine, scanner_range, hull_heat, ...) for this session. Not saved.</summary>
        public void DebugSetShipLevel(string catalogId, int level)
        {
            if (string.IsNullOrEmpty(catalogId)) return;
            shipLevelOverrides[catalogId] = Math.Max(0, level);
            Notify();
        }

        public void DebugClearShipLevels()
        {
            shipLevelOverrides.Clear();
            Notify();
        }
#endif
    }
}
