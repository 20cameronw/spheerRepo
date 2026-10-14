using System;
using System.Collections.Generic;
using Frontier.Universe;

namespace Frontier.Economy
{
    /// <summary>Result of paying a fuel cost with the densest fuels first.</summary>
    public struct FuelPayment
    {
        public int fuelCells, plasmaCells, antimatterPods;
        /// <summary>Energy paid above the cost (a denser unit had to be broken).</summary>
        public int overpay;
        public bool ok;

        public const int PlasmaEnergy = 8, AntimatterEnergy = 64;

        /// <summary>Greedy by density: antimatter first, then plasma, then fuel cells; if cells run short the smallest denser unit covers the rest.</summary>
        public static FuelPayment Plan(int cost, int fuel, int plasma, int antimatter)
        {
            var p = new FuelPayment();
            if (cost <= 0) { p.ok = true; return p; }
            long total = (long)fuel + (long)plasma * PlasmaEnergy + (long)antimatter * AntimatterEnergy;
            if (total < cost) return p;
            int r = cost;
            int a = Math.Min(antimatter, r / AntimatterEnergy);
            r -= a * AntimatterEnergy;
            int pl = Math.Min(plasma, r / PlasmaEnergy);
            r -= pl * PlasmaEnergy;
            int f = Math.Min(fuel, r);
            r -= f;
            while (r > 0)
            {
                // Fuel cells ran out: break the smallest denser unit that is still in stock.
                if (plasma - pl > 0) { pl++; r -= PlasmaEnergy; }
                else { a++; r -= AntimatterEnergy; }
            }
            if (r < 0)
            {
                // Give back cells that the extra unit made unnecessary.
                int give = Math.Min(f, -r);
                f -= give;
                r += give;
            }
            p.fuelCells = f;
            p.plasmaCells = pl;
            p.antimatterPods = a;
            p.overpay = f + pl * PlasmaEnergy + a * AntimatterEnergy - cost;
            p.ok = true;
            return p;
        }
    }

    /// <summary>One planned trip between two worlds.</summary>
    public struct TripPlan
    {
        public bool reachable;
        public int hops;
        public double legLy;
        public int fuel;
        public int seconds;
        public bool intraSystem;
    }

    /// <summary>
    /// The travel model. fuel(hop) = (base + k x d^p) x engine x (1 + cargo x fill) x (1 - discounts) x autopilot, paid in energy units;
    /// jump range (an upgrade) limits one hop, so far worlds need chained hops; landing needs hull plating for every hazard of the world.
    /// </summary>
    public sealed class TravelModel
    {
        public const int HazardCount = 5;
        public static readonly string[] HullIds = { "hull_heat", "hull_cold", "hull_toxic", "hull_rad", "hull_void" };
        public static readonly Hazards[] HazardOrder = { Hazards.Heat, Hazards.Cold, Hazards.Toxic, Hazards.Radiation, Hazards.Void };

        readonly double _base, _k, _p, _intra, _intraPerAu, _cargo, _autopilot, _refundMax, _runBase, _runLog, _runCap, _autoRun, _hopFraction, _detour, _maxDiscount, _baseScanner;
        readonly int _l2, _l3;
        readonly double[] _jump;
        readonly double[] _engine;
        readonly double[] _scanner;

        public TravelModel(Tuning t, EconomyCatalog cat)
        {
            _base = t.D("travel.base_fuel");
            _k = t.D("travel.k");
            _p = t.D("travel.p");
            _intra = t.D("travel.intra_fuel");
            _intraPerAu = t.D("travel.intra_fuel_per_au");
            _cargo = t.D("travel.cargo_factor");
            _autopilot = t.D("travel.autopilot_mult");
            _refundMax = t.D("travel.refund_max");
            _runBase = t.D("travel.run_base_seconds");
            _runLog = t.D("travel.run_log_seconds");
            _runCap = t.D("travel.run_cap_seconds");
            _autoRun = t.D("travel.autopilot_seconds");
            _hopFraction = t.D("travel.hop_fraction");
            _detour = t.D("travel.detour");
            _maxDiscount = t.D("travel.max_discount");
            _l2 = t.I("travel.plating_l2_tier");
            _l3 = t.I("travel.plating_l3_tier");
            UpgradeDef jump = cat.Upgrade("jump"), engine = cat.Upgrade("engine");
            _jump = new double[jump.levels.Length + 1];
            for (int i = 0; i < jump.levels.Length; i++) _jump[i + 1] = jump.levels[i].value;
            _engine = new double[engine.levels.Length + 1];
            _engine[0] = 1.0;
            for (int i = 0; i < engine.levels.Length; i++) _engine[i + 1] = engine.levels[i].value;
            _baseScanner = t.D("sim.base_scanner_ly");
            UpgradeDef scanner = cat.Upgrade("scanner_range");
            _scanner = new double[scanner.levels.Length + 1];
            _scanner[0] = _baseScanner;
            for (int i = 0; i < scanner.levels.Length; i++) _scanner[i + 1] = Math.Max(_baseScanner, scanner.levels[i].value);
        }

        public static void RegisterKeys(Tuning t)
        {
            t.Register("travel", "travel.base_fuel", 1.5, 0, 20, "Fixed fuel per jump leg (energy units).");
            t.Register("travel", "travel.k", 0.14, 0.01, 2, "Fuel per ly^p of route length.");
            t.Register("travel", "travel.p", 1.10, 1.0, 2.0, "Route-length exponent: above 1 so long trips cost more per light year.");
            t.Register("travel", "travel.intra_fuel", 2, 0, 20, "Fuel of a hop between two worlds of the same system.");
            t.Register("travel", "travel.intra_fuel_per_au", 0.25, 0, 5, "Extra fuel per AU of orbit difference inside a system.");
            t.Register("travel", "travel.cargo_factor", 0.20, 0, 1, "Fuel penalty at a full hold: x(1 + factor x fill).");
            t.Register("travel", "travel.autopilot_mult", 1.6, 1, 4, "Fuel multiplier of an autopilot jump (no mini-game, no refund).");
            t.Register("travel", "travel.refund_max", 0.40, 0, 1, "Share of the fuel refunded by a perfect Hyperspace Run (legacy 0.4).");
            t.Register("travel", "travel.run_base_seconds", 25, 5, 120, "Seconds of the shortest Hyperspace Run leg.");
            t.Register("travel", "travel.run_log_seconds", 12, 0, 60, "Added seconds per ln(1 + legLy/20).");
            t.Register("travel", "travel.run_cap_seconds", 90, 20, 300, "Longest Hyperspace Run leg.");
            t.Register("travel", "travel.autopilot_seconds", 12, 1, 60, "Seconds of an autopilot leg.");
            t.Register("travel", "travel.hop_fraction", 0.90, 0.5, 1.0, "The route planner keeps each leg below this share of the jump range.");
            t.Register("travel", "travel.detour", 1.15, 1.0, 2.0, "Route length over straight-line distance (stepping-stone systems are not on the line).");
            t.Register("travel", "travel.max_discount", 0.70, 0, 0.95, "Largest combined fuel discount of Hangar and Warp Gate.");
            t.Register("travel", "travel.plating_l2_tier", 5, 1, 40, "From this planet tier a hazard needs plating level 2.");
            t.Register("travel", "travel.plating_l3_tier", 9, 1, 40, "From this planet tier a hazard needs plating level 3.");
        }

        /// <summary>Jump range in ly at an upgrade level (0 means no jump drive: home system only).</summary>
        public double JumpRangeLy(int level)
        {
            if (level <= 0) return 0.0;
            return _jump[Math.Min(level, _jump.Length - 1)];
        }

        /// <summary>Fuel factor of an engine level (1 at level 0, 0.45 at the top).</summary>
        public double EngineFactor(int level)
        {
            if (level <= 0) return 1.0;
            return _engine[Math.Min(level, _engine.Length - 1)];
        }

        /// <summary>Survey radius in ly around every visited system at a scanner level (the base radius at level 0, 2,200 ly at the top).</summary>
        public double ScannerRangeLy(int level)
        {
            if (level <= 0) return _scanner[0];
            return _scanner[Math.Min(level, _scanner.Length - 1)];
        }

        public int MaxJumpLevel { get { return _jump.Length - 1; } }
        public int MaxEngineLevel { get { return _engine.Length - 1; } }
        public int MaxScannerLevel { get { return _scanner.Length - 1; } }

        /// <summary>The fixed fuel share of one leg (before modifiers).</summary>
        public double BaseFuelPerLeg { get { return _base; } }

        /// <summary>Share of the jump range a leg may use (the planner leaves some slack for stepping stones); the closed-form estimate uses it.</summary>
        public double HopFraction { get { return _hopFraction; } }

        /// <summary>Unmodified energy of a whole route of <paramref name="legs"/> legs and <paramref name="routeLy"/> total length: legs x base + k x length^p.</summary>
        public double RouteBaseFuel(int legs, double routeLy)
        {
            double distancePart = routeLy > 0.0 ? _k * DetMath.Pow(routeLy, _p) : 0.0;
            return Math.Max(0, legs) * _base + distancePart;
        }

        /// <summary>Autopilot fuel multiplier (1.6).</summary>
        public double AutopilotFuelMultiplier { get { return _autopilot; } }

        /// <summary>The largest combined discount the model honours.</summary>
        public double MaxDiscount { get { return _maxDiscount; } }

        double LegFuel(double legLy)
        {
            return _base + _k * DetMath.Pow(legLy, _p);
        }

        /// <summary>Energy of one single jump leg with every modifier; at least 1.</summary>
        public int LegFuelCost(double legLy, int engineLevel, double cargoFill, double discount, bool autopilot)
        {
            double f = LegFuel(legLy) * EngineFactor(engineLevel) * (1.0 + _cargo * Clamp01(cargoFill)) * (1.0 - Math.Min(discount, _maxDiscount)) * (autopilot ? _autopilot : 1.0);
            return Math.Max(1, (int)Math.Ceiling(f - 1e-9));
        }

        static double Clamp01(double v) { return v < 0.0 ? 0.0 : (v > 1.0 ? 1.0 : v); }

        /// <summary>Fuel of a hop inside one system (orbit difference in AU-like units).</summary>
        public int IntraSystemFuelCost(double deltaAu, int engineLevel, double discount, bool autopilot)
        {
            double f = (_intra + _intraPerAu * Math.Abs(deltaAu)) * EngineFactor(engineLevel) * (1.0 - Math.Min(discount, _maxDiscount)) * (autopilot ? _autopilot : 1.0);
            return Math.Max(1, (int)Math.Ceiling(f - 1e-9));
        }

        public int RunSeconds(double legLy)
        {
            double s = _runBase + _runLog * DetMath.Log(1.0 + legLy / 20.0);
            if (s > _runCap) s = _runCap;
            return CostMath.RoundInt(s);
        }

        /// <summary>Seconds of one autopilot leg (no mini-game, no fuel refund, a fuel premium).</summary>
        public double AutopilotLegSeconds { get { return _autoRun; } }

        /// <summary>Unmodified energy of one jump leg (the fixed part plus the distance part); a route pays the sum over its legs.</summary>
        public double LegBaseFuel(double legLy) { return LegFuel(legLy); }

        /// <summary>Energy of a whole route whose legs cost <paramref name="summedBaseFuel"/> before modifiers; at least 1.</summary>
        public int RouteFuelCost(double summedBaseFuel, int engineLevel, double cargoFill, double discount, bool autopilot)
        {
            double f = summedBaseFuel * EngineFactor(engineLevel) * (1.0 + _cargo * Clamp01(cargoFill)) * (1.0 - Math.Min(discount, _maxDiscount)) * (autopilot ? _autopilot : 1.0);
            return Math.Max(1, (int)Math.Ceiling(f - 1e-9));
        }

        /// <summary>
        /// Plans a trip between two systems <paramref name="distanceLy"/> apart: the fewest legs the jump range allows, each leg one
        /// Hyperspace Run. Fuel = (legs x base + k x routeLength^p) x modifiers, which is monotone in distance by construction.
        /// </summary>
        public TripPlan PlanInterSystem(double distanceLy, int jumpLevel, int engineLevel, double cargoFill, double discount, bool autopilot)
        {
            var plan = new TripPlan();
            double range = JumpRangeLy(jumpLevel);
            if (!(range > 0.0)) return plan;
            double d = distanceLy * _detour;
            int legs = Math.Max(1, (int)Math.Ceiling(d / (range * _hopFraction) - 1e-9));
            double leg = d / legs;
            double fuel = (legs * _base + _k * DetMath.Pow(d, _p)) * EngineFactor(engineLevel) * (1.0 + _cargo * Clamp01(cargoFill)) * (1.0 - Math.Min(discount, _maxDiscount)) * (autopilot ? _autopilot : 1.0);
            plan.reachable = true;
            plan.hops = legs;
            plan.legLy = leg;
            plan.fuel = Math.Max(1, (int)Math.Ceiling(fuel - 1e-9));
            plan.seconds = legs * (autopilot ? CostMath.RoundInt(_autoRun) : RunSeconds(leg));
            return plan;
        }

        /// <summary>Fuel refunded by a Hyperspace Run of the given quality (0..1).</summary>
        public int Refund(int fuelSpent, double quality)
        {
            return (int)Math.Floor(fuelSpent * _refundMax * Clamp01(quality) + 1e-9);
        }

        /// <summary>Plating level a hazard demands on a planet of this tier (1, 2 or 3).</summary>
        public int RequiredPlatingLevel(int planetTier)
        {
            return planetTier >= _l3 ? 3 : (planetTier >= _l2 ? 2 : 1);
        }

        public static int HazardIndex(Hazards single)
        {
            for (int i = 0; i < HazardOrder.Length; i++) if (HazardOrder[i] == single) return i;
            return -1;
        }

        /// <summary>True when the hull plating (level per hazard, indexed like <see cref="HazardOrder"/>) allows landing on a world with these hazards.</summary>
        public bool CanLand(int[] platingLevels, Hazards hazards, int planetTier)
        {
            int need = RequiredPlatingLevel(planetTier);
            for (int i = 0; i < HazardOrder.Length; i++)
                if ((hazards & HazardOrder[i]) != 0 && platingLevels[i] < need) return false;
            return true;
        }
    }

    /// <summary>Distance to tier to danger to reward, consistent with the universe generator's tier formula and danger defaults.</summary>
    public sealed class WorldRules
    {
        readonly UniverseConfig _cfg;
        readonly EconomyCatalog _cat;

        public WorldRules(EconomyCatalog cat, UniverseConfig cfg)
        {
            _cat = cat;
            _cfg = cfg ?? new UniverseConfig();
        }

        public UniverseConfig Config { get { return _cfg; } }

        /// <summary>Planet tier for a distance from home in light years: clamp(floor(1.6 x log2(1 + d / 60)), 0, TierMax) (anomalies aside).</summary>
        public int TierForDistance(double distanceLy) { return _cfg.TierFromDistance(distanceLy); }

        /// <summary>Smallest distance (ly) at which a tier starts: 60 x (2^(tier / 1.6) - 1).</summary>
        public double DistanceForTier(int tier)
        {
            if (tier <= 0) return 0.0;
            return _cfg.TierDistanceUnit * (DetMath.Pow(2.0, tier / _cfg.TierScale) - 1.0);
        }

        /// <summary>Mean danger of a world of this tier (the generator adds noise of +-0.3).</summary>
        public double DangerForTier(int tier) { return _cfg.DangerBase + _cfg.DangerPerTier * tier; }

        /// <summary>Deposit base yield the generator gives a resource on a planet of this tier.</summary>
        public int DepositBaseYield(string resourceId, int tier)
        {
            ResourceDef r = _cat.Resource(resourceId);
            return (int)((double)r.baseYield + (double)r.yieldPerTier * Math.Min(tier, 20) + 0.5);
        }

        /// <summary>Expected units of one harvest run: base yield x richness x tool x structure bonus x (0.4 + 1.1 x quality).</summary>
        public double RunYield(string resourceId, int tier, double richness, double toolMult, double structureBonus, double quality)
        {
            return DepositBaseYield(resourceId, tier) * richness * toolMult * (1.0 + structureBonus) * (0.4 + 1.1 * quality);
        }

        /// <summary>Expected value (scrap-equivalents) of one harvest run.</summary>
        public double RunValue(string resourceId, int tier, double richness, double toolMult, double structureBonus, double quality)
        {
            return RunYield(resourceId, tier, richness, toolMult, structureBonus, quality) * _cat.Resource(resourceId).baseValue;
        }
    }
}
