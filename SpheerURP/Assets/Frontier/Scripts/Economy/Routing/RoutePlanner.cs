using System;
using System.Collections.Generic;
using Frontier.Universe;
using Uni = Frontier.Universe.Universe;

namespace Frontier.Economy
{
    /// <summary>
    /// Plans trips on the real universe with the locked travel model: a hop inside one system costs the intra-system fuel; a trip between
    /// systems is the chain of stepping-stone systems with the fewest legs the jump range allows (shortest in light-years among those), each
    /// leg one travel mini-game run, priced as (legs x base + k x routeLy^p) x engine x cargo x discount x autopilot and paid in energy
    /// units. Deterministic: the same inputs always give the same plan. Not thread safe: use one planner per thread (the universe is).
    /// </summary>
    public sealed partial class RoutePlanner
    {
        readonly Uni _universe;
        readonly TravelRules _rules;
        readonly List<StarSystem> _scratch = new List<StarSystem>();

        public RoutePlanner(Uni universe, TravelRules rules)
        {
            if (universe == null) throw new ArgumentNullException("universe");
            if (rules == null) throw new ArgumentNullException("rules");
            _universe = universe;
            _rules = rules;
        }

        public TravelRules Rules { get { return _rules; } }

        public Uni Universe { get { return _universe; } }

        static bool SameWorld(RouteEndpoint a, RouteEndpoint b)
        {
            if (a.PlanetId != null && b.PlanetId != null) return string.Equals(a.PlanetId, b.PlanetId, StringComparison.Ordinal);
            return a.PlanetId == null && b.PlanetId == null && a.System == b.System;
        }

        /// <summary>
        /// Plans a trip from one world to another. The plan is never null: when the trip cannot be taken it says why (<see cref="RoutePlan.reasons"/>);
        /// legs and fuel are filled in whenever a route exists, even if the hull or the fuel stock rules it out, so the UI can show what is missing.
        /// </summary>
        /// <param name="stock">The energy items on board, or null when the caller only wants prices (no fuel check).</param>
        public RoutePlan Plan(RouteEndpoint from, RouteEndpoint to, ShipCapabilities caps, bool autopilot, FuelStock? stock)
        {
            var plan = new RoutePlan { autopilot = autopilot, fromPlanetId = from.PlanetId, toPlanetId = to.PlanetId, straightLy = from.DistanceLy(to) };
            if (SameWorld(from, to)) { plan.reasons = RouteReasons.AlreadyHere; return plan; }

            plan.hazardGaps = _rules.LandingGaps(caps, to.Hazards, to.Tier);
            RouteReasons reasons = RouteReasons.None;
            if (plan.hazardGaps.Length > 0 && caps.enforceHazardPlating) reasons |= RouteReasons.Hazard;

            if (from.System == to.System) BuildIntra(plan, from, to, caps, autopilot);
            else
            {
                double range = _rules.JumpRangeLy(caps);
                StarSystem a, b;
                if (!(range > 0.0)) reasons |= RouteReasons.NoJumpDrive;
                else if (!_universe.TryGetSystem(from.System, out a) || !_universe.TryGetSystem(to.System, out b)) reasons |= RouteReasons.Unknown;
                else
                {
                    List<StarSystem> chain = FindChain(a, b, range);
                    if (chain == null) { reasons |= searchLimited ? RouteReasons.SearchLimit : RouteReasons.OutOfRange; plan.reachLy = range; }
                    else BuildJumps(plan, chain, caps, autopilot);
                }
            }

            if (stock.HasValue && plan.totalFuel > 0)
            {
                long energy = stock.Value.Energy;
                if (!_rules.Pay(plan.totalFuel, stock.Value).ok)
                {
                    reasons |= RouteReasons.NotEnoughFuel;
                    plan.fuelShort = (int)Math.Min(int.MaxValue, plan.totalFuel - energy);
                }
            }
            plan.reasons = reasons;
            plan.reachable = reasons == RouteReasons.None && plan.legs.Length > 0;
            return plan;
        }

        void BuildIntra(RoutePlan plan, RouteEndpoint from, RouteEndpoint to, ShipCapabilities caps, bool autopilot)
        {
            double delta = Math.Abs(from.OrbitRadius - to.OrbitRadius);
            int fuel = _rules.Model.IntraSystemFuelCost(delta, caps.engineLevel, caps.discount, autopilot);
            int seconds = autopilot ? _rules.AutopilotLegSeconds : _rules.Model.RunSeconds(0.0);
            plan.legs = new[]
            {
                new RouteLeg
                {
                    fromSystemId = from.SystemId, toSystemId = to.SystemId, x0 = from.X, y0 = from.Y, x1 = to.X, y1 = to.Y,
                    lengthLy = 0.0, deltaAu = delta, intraSystem = true, fuel = fuel, seconds = seconds
                }
            };
            plan.intraSystem = true;
            plan.totalFuel = fuel;
            plan.totalSeconds = seconds;
        }

        void BuildJumps(RoutePlan plan, List<StarSystem> chain, ShipCapabilities caps, bool autopilot)
        {
            int n = chain.Count - 1;
            var legs = new RouteLeg[n];
            double total = 0.0;
            var weights = new double[n];
            double weightSum = 0.0;
            int seconds = 0;
            for (int i = 0; i < n; i++)
            {
                StarSystem p = chain[i], q = chain[i + 1];
                double dx = q.X - p.X, dy = q.Y - p.Y;
                double len = Math.Sqrt(dx * dx + dy * dy);
                int leg = autopilot ? _rules.AutopilotLegSeconds : _rules.Model.RunSeconds(len);
                legs[i] = new RouteLeg { fromSystemId = p.Id, toSystemId = q.Id, x0 = p.X, y0 = p.Y, x1 = q.X, y1 = q.Y, lengthLy = len, seconds = leg };
                weights[i] = _rules.Model.LegBaseFuel(len);
                weightSum += weights[i];
                total += len;
                seconds += leg;
            }
            double baseFuel = _rules.Model.RouteBaseFuel(n, total);
            int fuel = _rules.Model.RouteFuelCost(baseFuel, caps.engineLevel, caps.cargoFill, caps.discount, autopilot);
            fuel = Math.Max(fuel, n);
            Apportion(legs, weights, weightSum, fuel);
            plan.legs = legs;
            plan.totalFuel = fuel;
            plan.totalSeconds = seconds;
            plan.totalLengthLy = total;
        }

        // Splits the route's fuel over its legs in proportion to each leg's base fuel (largest remainder, ties to the earlier leg), at least 1 each.
        static void Apportion(RouteLeg[] legs, double[] weights, double weightSum, int totalFuel)
        {
            int n = legs.Length;
            var share = new int[n];
            var fraction = new double[n];
            int given = 0;
            for (int i = 0; i < n; i++)
            {
                double exact = weightSum > 0.0 ? totalFuel * weights[i] / weightSum : totalFuel / (double)n;
                int whole = Math.Max(1, (int)Math.Floor(exact + 1e-9));
                share[i] = whole;
                fraction[i] = exact - Math.Floor(exact);
                given += whole;
            }
            while (given < totalFuel)
            {
                int best = 0;
                for (int i = 1; i < n; i++) if (fraction[i] > fraction[best] + 1e-12) best = i;
                share[best]++;
                fraction[best] = -1.0;
                given++;
            }
            while (given > totalFuel)
            {
                int worst = -1;
                for (int i = n - 1; i >= 0; i--) if (share[i] > 1 && (worst < 0 || share[i] > share[worst])) worst = i;
                if (worst < 0) break;
                share[worst]--;
                given--;
            }
            for (int i = 0; i < n; i++) legs[i].fuel = share[i];
        }

        /// <summary>
        /// The "next stop" mode for targets the ship cannot reach in one go: the system within one jump of <paramref name="from"/> that gets
        /// closest to the point (x, y). Null when the ship has no jump drive or no system within range gets any closer.
        /// </summary>
        public StarSystem NextStopToward(StarSystem from, double x, double y, ShipCapabilities caps)
        {
            double range = _rules.JumpRangeLy(caps);
            if (!(range > 0.0) || from == null) return null;
            double here = Dist(from.X, from.Y, x, y);
            _scratch.Clear();
            _universe.SystemsInRect(from.X - range, from.Y - range, from.X + range, from.Y + range, _scratch);
            StarSystem best = null;
            double bestD = here;
            for (int i = 0; i < _scratch.Count; i++)
            {
                StarSystem s = _scratch[i];
                if (s.Key == from.Key || Dist(s.X, s.Y, from.X, from.Y) > range) continue;
                double d = Dist(s.X, s.Y, x, y);
                if (d < bestD - 1e-9 || (best != null && Math.Abs(d - bestD) <= 1e-9 && s.Key.Id < best.Key.Id)) { best = s; bestD = d; }
            }
            _scratch.Clear();
            return best;
        }

        static double Dist(double x0, double y0, double x1, double y1)
        {
            double dx = x1 - x0, dy = y1 - y0;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
