using System;
using System.Collections.Generic;
using NUnit.Framework;
using Frontier.Universe;
using Uni = Frontier.Universe.Universe;

namespace Frontier.Economy.Tests
{
    /// <summary>Route planner on the real universe with the locked travel model.</summary>
    [TestFixture]
    public class RoutePlannerTests
    {
        Uni _u;
        TravelRules _rules;
        RoutePlanner _planner;
        StarSystem _home;

        [OneTimeSetUp]
        public void Setup()
        {
            _u = new Uni(new UniverseConfig(), Landmarks.CreateDefault());
            _rules = TravelRules.CreateDefault();
            _planner = new RoutePlanner(_u, _rules);
            _home = NearestSystem(0.0, 0.0, 1.0);
        }

        StarSystem NearestSystem(double x, double y, double minDist)
        {
            StarSystem best = null;
            double bestD = double.MaxValue;
            foreach (StarSystem s in _u.SystemsInRect(x - 300, y - 300, x + 300, y + 300))
            {
                double d = Math.Sqrt((s.X - x) * (s.X - x) + (s.Y - y) * (s.Y - y));
                if (d >= minDist && d < bestD) { bestD = d; best = s; }
            }
            return best;
        }

        static ShipCapabilities Ship(int jump, int engine = 0, double fill = 0.0, bool enforce = true)
        {
            return new ShipCapabilities { jumpLevel = jump, engineLevel = engine, cargoFill = fill, enforceHazardPlating = enforce };
        }

        /// <summary>Systems at least <paramref name="minLy"/> and at most <paramref name="maxLy"/> from the home system, nearest first.</summary>
        List<StarSystem> Targets(double minLy, double maxLy)
        {
            var list = new List<StarSystem>();
            foreach (StarSystem s in _u.SystemsInRect(-maxLy, -maxLy, maxLy, maxLy))
            {
                double d = Math.Sqrt(s.X * s.X + s.Y * s.Y);
                if (d >= minLy && d <= maxLy && s.Key != _home.Key) list.Add(s);
            }
            list.Sort((a, b) => (a.X * a.X + a.Y * a.Y).CompareTo(b.X * b.X + b.Y * b.Y));
            return list;
        }

        RoutePlan PlanTo(StarSystem target, ShipCapabilities caps, bool autopilot = false, FuelStock? stock = null)
        {
            return _planner.Plan(RouteEndpoint.Of(_home), RouteEndpoint.Of(target), caps, autopilot, stock);
        }

        [Test]
        public void GoldenNumbers_MatchTheEconomyDocument()
        {
            TravelModel m = _rules.Model;
            Assert.AreEqual(8, m.LegFuelCost(30.0, 0, 0.0, 0.0, false));
            Assert.AreEqual(24, m.LegFuelCost(100.0, 0, 0.0, 0.0, false));
            // docs/economy/data/travel.json, "fuelToReachItsStart": tier 1 (32.5 ly), tier 3 (160.1 ly), tier 11 (6,982.6 ly)
            AssertTripRow(m, 32.5, 1, 0, 2, 12, 66);
            AssertTripRow(m, 160.1, 5, 5, 2, 34, 92);
            AssertTripRow(m, 6982.6, 10, 10, 5, 1372, 390);
            // The planner prices a chain of the same legs and length exactly like the closed form of the document.
            double len = 32.5 * 1.15;
            Assert.AreEqual(12, m.RouteFuelCost(m.RouteBaseFuel(2, len), 0, 0.5, 0.0, false));
            Assert.AreEqual(3, m.IntraSystemFuelCost(1.0, 0, 0.0, false), "2 + 0.25 per AU, rounded up");
            Assert.AreEqual(4, m.IntraSystemFuelCost(1.0, 0, 0.0, true), "autopilot x1.6");
        }

        static void AssertTripRow(TravelModel m, double ly, int jump, int engine, int legs, int fuel, int seconds)
        {
            TripPlan p = m.PlanInterSystem(ly, jump, engine, 0.5, 0.0, false);
            Assert.AreEqual(legs, p.hops, ly + " ly legs");
            Assert.AreEqual(fuel, p.fuel, ly + " ly fuel");
            Assert.AreEqual(seconds, p.seconds, ly + " ly seconds");
        }

        [Test]
        public void SameWorld_IsAlreadyThere_AndSameSystemIsOneCheapHop()
        {
            RouteEndpoint here = RouteEndpoint.Of(_home);
            RoutePlan same = _planner.Plan(here, here, Ship(5), false, null);
            Assert.IsFalse(same.reachable);
            Assert.IsTrue(same.Has(RouteReasons.AlreadyHere));

            var a = new RouteEndpoint("p:0:0:0:0", _home.Key, _home.X, _home.Y, 1.0, 0, Hazards.None);
            var b = new RouteEndpoint("p:0:0:0:1", _home.Key, _home.X, _home.Y, 3.0, 0, Hazards.None);
            RoutePlan hop = _planner.Plan(a, b, Ship(0), false, null);
            Assert.IsTrue(hop.reachable, "no jump drive still allows a hop inside the system");
            Assert.IsTrue(hop.intraSystem);
            Assert.AreEqual(1, hop.legs.Length);
            Assert.AreEqual(_rules.Model.IntraSystemFuelCost(2.0, 0, 0.0, false), hop.totalFuel);
            Assert.AreEqual(hop.totalFuel, hop.legs[0].fuel);
            RoutePlan auto = _planner.Plan(a, b, Ship(0), true, null);
            Assert.AreEqual(_rules.Model.IntraSystemFuelCost(2.0, 0, 0.0, true), auto.totalFuel);
            Assert.AreEqual(_rules.AutopilotLegSeconds, auto.totalSeconds);
        }

        [Test]
        public void NoJumpDrive_BlocksEveryInterSystemTrip()
        {
            StarSystem target = Targets(5, 120)[0];
            RoutePlan p = PlanTo(target, Ship(0));
            Assert.IsFalse(p.reachable);
            Assert.IsTrue(p.Has(RouteReasons.NoJumpDrive));
            Assert.AreEqual(0, p.legs.Length);
        }

        [Test]
        public void Legs_StayWithinJumpRange_AreContiguousAndSumUp()
        {
            foreach (int jump in new[] { 2, 3, 5 })
            {
                double range = _rules.Model.JumpRangeLy(jump);
                int checkedRoutes = 0;
                foreach (StarSystem target in Targets(range * 0.5, range * 3.0))
                {
                    if (checkedRoutes >= 6) break;
                    RoutePlan p = PlanTo(target, Ship(jump, 2, 0.4));
                    if (!p.reachable) continue;
                    checkedRoutes++;
                    int fuelSum = 0, seconds = 0;
                    double length = 0.0;
                    for (int i = 0; i < p.legs.Length; i++)
                    {
                        RouteLeg leg = p.legs[i];
                        Assert.LessOrEqual(leg.lengthLy, range + 1e-9, "jump " + jump + " leg " + i);
                        Assert.GreaterOrEqual(leg.fuel, 1);
                        if (i > 0)
                        {
                            Assert.AreEqual(p.legs[i - 1].x1, leg.x0, 1e-9);
                            Assert.AreEqual(p.legs[i - 1].y1, leg.y0, 1e-9);
                            Assert.AreEqual(p.legs[i - 1].toSystemId, leg.fromSystemId);
                        }
                        fuelSum += leg.fuel; seconds += leg.seconds; length += leg.lengthLy;
                    }
                    Assert.AreEqual(_home.Id, p.legs[0].fromSystemId);
                    Assert.AreEqual(target.Id, p.legs[p.legs.Length - 1].toSystemId);
                    Assert.AreEqual(p.totalFuel, fuelSum, "the legs add up to the route fuel");
                    Assert.AreEqual(p.totalSeconds, seconds);
                    Assert.AreEqual(p.totalLengthLy, length, 1e-6);
                    Assert.GreaterOrEqual(p.totalLengthLy, p.straightLy - 1e-6, "a route is never shorter than the straight line");
                    Assert.GreaterOrEqual(p.legs.Length, (int)Math.Ceiling(p.straightLy / range - 1e-9), "fewer legs than the range allows");
                }
                Assert.Greater(checkedRoutes, 0, "no target found for jump level " + jump);
            }
        }

        [Test]
        public void ATargetInRange_IsOneLeg()
        {
            foreach (StarSystem target in Targets(10, 30))
            {
                RoutePlan p = PlanTo(target, Ship(2));
                Assert.IsTrue(p.reachable);
                Assert.AreEqual(1, p.legs.Length);
                Assert.AreEqual(p.straightLy, p.legs[0].lengthLy, 1e-9);
                int expected = _rules.Model.RouteFuelCost(_rules.Model.RouteBaseFuel(1, p.straightLy), 0, 0.0, 0.0, false);
                Assert.AreEqual(expected, p.totalFuel);
            }
        }

        [Test]
        public void Planning_IsDeterministic()
        {
            StarSystem target = Targets(150, 400)[2];
            RoutePlan a = PlanTo(target, Ship(3, 4, 0.5));
            RoutePlan b = new RoutePlanner(_u, _rules).Plan(RouteEndpoint.Of(_home), RouteEndpoint.Of(target), Ship(3, 4, 0.5), false, null);
            Assert.AreEqual(a.reachable, b.reachable);
            Assert.AreEqual(a.legs.Length, b.legs.Length);
            Assert.AreEqual(a.totalFuel, b.totalFuel);
            for (int i = 0; i < a.legs.Length; i++)
            {
                Assert.AreEqual(a.legs[i].toSystemId, b.legs[i].toSystemId);
                Assert.AreEqual(a.legs[i].fuel, b.legs[i].fuel);
            }
        }

        [Test]
        public void Fuel_NeverDecreasesAlongARoute_AndFallsWithTheEngine()
        {
            StarSystem target = Targets(180, 400)[3];
            RoutePlan full = PlanTo(target, Ship(3));
            Assert.IsTrue(full.reachable);
            Assert.Greater(full.legs.Length, 1);
            int previous = 0;
            foreach (RouteLeg leg in full.legs)
            {
                StarSystem stop;
                Assert.IsTrue(_u.TryGetSystemById(leg.toSystemId, out stop));
                RoutePlan prefix = PlanTo(stop, Ship(3));
                Assert.GreaterOrEqual(prefix.totalFuel, previous, "fuel to " + stop.Id);
                previous = prefix.totalFuel;
            }
            Assert.AreEqual(full.totalFuel, previous);

            int lastFuel = int.MaxValue;
            for (int engine = 0; engine <= 10; engine++)
            {
                int fuel = PlanTo(target, Ship(3, engine)).totalFuel;
                Assert.LessOrEqual(fuel, lastFuel, "engine level " + engine);
                lastFuel = fuel;
            }
            int empty = PlanTo(target, Ship(3, 2, 0.0)).totalFuel, full2 = PlanTo(target, Ship(3, 2, 1.0)).totalFuel;
            Assert.LessOrEqual(empty, full2, "a full hold burns more");
        }

        [Test]
        public void Autopilot_CostsTheMultiplier_PaysNoRefund_AndTakesTwelveSecondsPerLeg()
        {
            StarSystem target = Targets(150, 400)[1];
            RoutePlan manual = PlanTo(target, Ship(3, 3, 0.5)), auto = PlanTo(target, Ship(3, 3, 0.5), true);
            Assert.IsTrue(manual.reachable && auto.reachable);
            Assert.AreEqual(manual.legs.Length, auto.legs.Length);
            Assert.Greater(auto.totalFuel, manual.totalFuel);
            Assert.AreEqual(Math.Ceiling(manual.totalFuel * 1.6), auto.totalFuel, manual.legs.Length + 1.0, "about x1.6 (each side rounds up once)");
            Assert.AreEqual(auto.legs.Length * _rules.AutopilotLegSeconds, auto.totalSeconds);
            Assert.AreEqual(0, _rules.Refund(auto.legs[0].fuel, 1.0, true));
            Assert.Greater(_rules.Refund(manual.legs[0].fuel + 20, 1.0, false), 0);
        }

        [Test]
        public void FuelStock_DecidesAffordability_AndDenseFuelsPayDeepTrips()
        {
            StarSystem target = Targets(150, 400)[1];
            RoutePlan priced = PlanTo(target, Ship(3));
            RoutePlan poor = PlanTo(target, Ship(3), false, new FuelStock(priced.totalFuel - 1, 0, 0));
            Assert.IsFalse(poor.reachable);
            Assert.IsTrue(poor.Has(RouteReasons.NotEnoughFuel));
            Assert.AreEqual(1, poor.fuelShort);
            Assert.IsFalse(poor.Has(RouteReasons.OutOfRange));
            int plasma = (priced.totalFuel + 7) / 8;
            RoutePlan dense = PlanTo(target, Ship(3), false, new FuelStock(0, plasma, 0));
            Assert.IsTrue(dense.reachable, "plasma cells are 8 energy each");
            FuelPayment pay = _rules.Pay(dense.totalFuel, new FuelStock(0, plasma, 0));
            Assert.IsTrue(pay.ok);
            Assert.GreaterOrEqual(pay.plasmaCells * 8, dense.totalFuel);
        }

        [Test]
        public void HazardPlating_BlocksLandingOnlyWhenEnforced_AndSaysWhatIsMissing()
        {
            var lethal = new RouteEndpoint("p:9:9:0:0", _home.Key, _home.X, _home.Y, 5.0, 6, Hazards.Heat | Hazards.Toxic);
            var here = new RouteEndpoint("p:0:0:0:0", _home.Key, _home.X, _home.Y, 1.0, 0, Hazards.None);
            RoutePlan enforced = _planner.Plan(here, lethal, Ship(5, 0, 0.0, true), false, null);
            Assert.IsFalse(enforced.reachable);
            Assert.IsTrue(enforced.Has(RouteReasons.Hazard));
            Assert.AreEqual(2, enforced.hazardGaps.Length);
            Assert.AreEqual(2, enforced.hazardGaps[0].need, "tier 6 needs plating level 2");
            Assert.AreEqual(0, enforced.hazardGaps[0].have);
            Assert.Greater(enforced.totalFuel, 0, "the price is shown even when the hull rules the trip out");

            RoutePlan relaxed = _planner.Plan(here, lethal, Ship(5, 0, 0.0, false), false, null);
            Assert.IsTrue(relaxed.reachable);
            Assert.AreEqual(2, relaxed.hazardGaps.Length, "the gaps are still reported so the card can show them");

            ShipCapabilities plated = Ship(5, 0, 0.0, true);
            plated.platingLevels[0] = 2; plated.platingLevels[2] = 3;
            Assert.IsTrue(_planner.Plan(here, lethal, plated, false, null).reachable);
            plated.platingLevels[2] = 1;
            RoutePlan partial = _planner.Plan(here, lethal, plated, false, null);
            Assert.IsFalse(partial.reachable);
            Assert.AreEqual(1, partial.hazardGaps.Length);
            Assert.AreEqual(Hazards.Toxic, partial.hazardGaps[0].hazard);
            Assert.AreEqual(1, partial.hazardGaps[0].have);
        }

        [Test]
        public void TheVoid_IsOutOfRange_AndNextStopMovesTowardIt()
        {
            // A point far outside every galaxy: no chain of 36 ly jumps can get there.
            var far = new RouteEndpoint(null, new SystemKey(900, 900, 0), 57600.0, 57600.0, 0.0, 20, Hazards.None);
            RoutePlan p = _planner.Plan(RouteEndpoint.Of(_home), far, Ship(1), false, null);
            Assert.IsFalse(p.reachable);
            Assert.IsTrue(p.Has(RouteReasons.OutOfRange) || p.Has(RouteReasons.Unknown));
            Assert.AreEqual(0, p.legs.Length);

            StarSystem stop = _planner.NextStopToward(_home, 4000.0, 4000.0, Ship(3));
            Assert.IsNotNull(stop);
            double range = _rules.Model.JumpRangeLy(3);
            Assert.LessOrEqual(Math.Sqrt((stop.X - _home.X) * (stop.X - _home.X) + (stop.Y - _home.Y) * (stop.Y - _home.Y)), range + 1e-9);
            Assert.Less(Math.Sqrt((4000 - stop.X) * (4000 - stop.X) + (4000 - stop.Y) * (4000 - stop.Y)), Math.Sqrt(2.0) * 4000.0, "the next stop is closer to the target");
            StarSystem again = _planner.NextStopToward(_home, 4000.0, 4000.0, Ship(3));
            Assert.AreEqual(stop.Key, again.Key, "deterministic");
            Assert.IsNull(_planner.NextStopToward(_home, 4000.0, 4000.0, Ship(0)), "no drive, no next stop");
        }

        [Test]
        public void ShipCapabilities_MapUpgradeIdsToLevels_WithInterimFloors()
        {
            var levels = new Dictionary<string, int> { { "engine", 4 }, { "hull_heat", 2 }, { "hull_void", 9 }, { "scanner_range", 3 } };
            Func<string, int> read = id => { int v; return levels.TryGetValue(id, out v) ? v : 0; };
            ShipCapabilities caps = ShipCapabilities.FromUpgradeLevels(read, 0.5, 5, 0, false);
            Assert.AreEqual(5, caps.jumpLevel, "no jump upgrade yet: the interim floor");
            Assert.AreEqual(4, caps.engineLevel);
            Assert.AreEqual(3, caps.scannerLevel, "a real level above the floor wins");
            Assert.AreEqual(2, caps.Plating(Hazards.Heat));
            Assert.AreEqual(3, caps.Plating(Hazards.Void), "plating is capped at level 3");
            Assert.AreEqual(0, caps.Plating(Hazards.Cold));
            Assert.AreEqual(0.5, caps.cargoFill);
            Assert.IsFalse(caps.enforceHazardPlating);
            levels["jump"] = 8;
            Assert.AreEqual(8, ShipCapabilities.FromUpgradeLevels(read, 2.0, 5, 0, true).jumpLevel, "the floor never lowers a real level");
            Assert.AreEqual(1.0, ShipCapabilities.FromUpgradeLevels(read, 2.0, 5, 0, true).cargoFill, "fill is clamped");
            Assert.AreEqual(200.0, _rules.Model.JumpRangeLy(5));
            Assert.AreEqual(40.0, _rules.Model.ScannerRangeLy(0));
            Assert.AreEqual(2200.0, _rules.Model.ScannerRangeLy(8));
            Assert.AreEqual(caps.jumpLevel, caps.Clone().jumpLevel);
        }
    }
}
