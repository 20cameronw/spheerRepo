using System;
using Frontier.Universe;

namespace Frontier.Economy
{
    /// <summary>The energy items a ship carries: fuel cells (1 energy), plasma cells (8) and antimatter pods (64).</summary>
    public readonly struct FuelStock
    {
        public readonly int fuel, plasma, antimatter;

        public FuelStock(int fuel, int plasma, int antimatter)
        {
            this.fuel = Math.Max(0, fuel);
            this.plasma = Math.Max(0, plasma);
            this.antimatter = Math.Max(0, antimatter);
        }

        public long Energy { get { return (long)fuel + (long)plasma * FuelPayment.PlasmaEnergy + (long)antimatter * FuelPayment.AntimatterEnergy; } }
    }

    /// <summary>A place a trip starts or ends: a world (or just a system) with the numbers travel needs.</summary>
    public readonly struct RouteEndpoint
    {
        /// <summary>Canonical planet id, or null when the endpoint is a bare system (a waypoint, a stepping stone).</summary>
        public readonly string PlanetId;
        public readonly SystemKey System;
        public readonly string SystemId;
        public readonly double X, Y;
        /// <summary>Orbit radius in AU-like units (0 for a bare system).</summary>
        public readonly double OrbitRadius;
        public readonly int Tier;
        public readonly Hazards Hazards;

        public RouteEndpoint(string planetId, SystemKey system, double x, double y, double orbitRadius, int tier, Hazards hazards)
        {
            PlanetId = planetId;
            System = system;
            SystemId = system.ToString();
            X = x;
            Y = y;
            OrbitRadius = orbitRadius;
            Tier = tier;
            Hazards = hazards;
        }

        public static RouteEndpoint Of(PlanetDescriptor planet, StarSystem star)
        {
            return new RouteEndpoint(planet.Id, star.Key, star.X, star.Y, planet.OrbitRadius, planet.Tier, planet.Hazards);
        }

        public static RouteEndpoint Of(StarSystem star)
        {
            return new RouteEndpoint(null, star.Key, star.X, star.Y, 0.0, star.Tier, Hazards.None);
        }

        public double DistanceLy(RouteEndpoint other)
        {
            double dx = X - other.X, dy = Y - other.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }

    /// <summary>
    /// The one door to the locked travel model for everything that is not a leg search: ranges, the landing rule, fuel payment, autopilot
    /// and the refund. Wraps <see cref="TravelModel"/> (the numbers) and reads the ship from <see cref="ShipCapabilities"/>.
    /// </summary>
    public sealed class TravelRules
    {
        public readonly TravelModel Model;

        public TravelRules(TravelModel model)
        {
            if (model == null) throw new ArgumentNullException("model");
            Model = model;
        }

        /// <summary>Builds the rules from the default tuning and catalog (a few milliseconds; keep the instance).</summary>
        public static TravelRules CreateDefault()
        {
            Tuning tuning = Tuning.CreateDefault();
            return new TravelRules(new TravelModel(tuning, CatalogBuilder.Build(tuning)));
        }

        public double JumpRangeLy(ShipCapabilities caps) { return Model.JumpRangeLy(caps.jumpLevel); }

        /// <summary>Survey radius around every visited system.</summary>
        public double ScannerRangeLy(ShipCapabilities caps) { return Model.ScannerRangeLy(caps.scannerLevel); }

        public int AutopilotLegSeconds { get { return CostMath.RoundInt(Model.AutopilotLegSeconds); } }

        /// <summary>Energy the hull plating is missing for each hazard of a world (empty when it covers all of them).</summary>
        public HazardGap[] LandingGaps(ShipCapabilities caps, Hazards hazards, int planetTier)
        {
            if (hazards == Hazards.None) return RoutePlan.NoGaps;
            int need = Model.RequiredPlatingLevel(planetTier);
            int count = 0;
            for (int i = 0; i < TravelModel.HazardCount; i++)
                if ((hazards & TravelModel.HazardOrder[i]) != 0 && caps.platingLevels[i] < need) count++;
            if (count == 0) return RoutePlan.NoGaps;
            var gaps = new HazardGap[count];
            int g = 0;
            for (int i = 0; i < TravelModel.HazardCount; i++)
                if ((hazards & TravelModel.HazardOrder[i]) != 0 && caps.platingLevels[i] < need)
                    gaps[g++] = new HazardGap { hazard = TravelModel.HazardOrder[i], have = caps.platingLevels[i], need = need };
            return gaps;
        }

        /// <summary>True when the ship may set down on a world with these hazards (always true while plating is not enforced).</summary>
        public bool CanLand(ShipCapabilities caps, Hazards hazards, int planetTier)
        {
            return !caps.enforceHazardPlating || LandingGaps(caps, hazards, planetTier).Length == 0;
        }

        /// <summary>The plating level a hazard demands on a world of this tier (1, 2 or 3).</summary>
        public int RequiredPlatingLevel(int planetTier) { return Model.RequiredPlatingLevel(planetTier); }

        /// <summary>How an energy cost is paid from the stock: densest fuel first (antimatter, plasma, fuel cells).</summary>
        public FuelPayment Pay(int energy, FuelStock stock)
        {
            return FuelPayment.Plan(energy, stock.fuel, stock.plasma, stock.antimatter);
        }

        /// <summary>Energy units given back by a leg of the given quality (0 for an autopilot leg).</summary>
        public int Refund(int legFuel, double quality, bool autopilot)
        {
            return autopilot ? 0 : Model.Refund(legFuel, quality);
        }
    }
}
