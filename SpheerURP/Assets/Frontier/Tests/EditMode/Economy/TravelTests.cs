using System;
using System.Collections.Generic;
using NUnit.Framework;
using Frontier.Economy;
using Frontier.Universe;
using FUniverse = Frontier.Universe.Universe;

namespace Frontier.Economy.Tests
{
    [TestFixture]
    public class TravelTests
    {
        Tuning _t;
        EconomyCatalog _c;
        TravelModel _m;

        [OneTimeSetUp]
        public void Setup() { _t = Fx.NewTuning(); _c = CatalogBuilder.Build(_t); _m = new TravelModel(_t, _c); }

        [Test]
        public void JumpRange_GrowsWithLevel_AndEngineFactorFalls()
        {
            double prev = 0;
            for (int l = 1; l <= 10; l++) { Assert.Greater(_m.JumpRangeLy(l), prev); prev = _m.JumpRangeLy(l); }
            Assert.AreEqual(0.0, _m.JumpRangeLy(0), "no jump drive: home system only");
            double f = 1.0;
            for (int l = 1; l <= 10; l++) { Assert.Less(_m.EngineFactor(l), f); f = _m.EngineFactor(l); }
            Assert.GreaterOrEqual(f, 0.4, "engine factor never reaches free travel");
            TestLog.Info("jump range tiers (ly): " + string.Join(", ", Ranges()));
        }

        IEnumerable<string> Ranges() { for (int l = 1; l <= 10; l++) yield return "L" + l + "=" + _m.JumpRangeLy(l).ToString("0"); }

        [Test]
        public void FartherCostsMore_ForEveryJumpLevel()
        {
            foreach (int jump in new[] { 1, 2, 3, 5, 8, 10 })
            {
                int prevFuel = 0;
                for (double d = 1; d <= 6000; d += 3.7)
                {
                    TripPlan p = _m.PlanInterSystem(d, jump, 0, 0.5, 0.0, false);
                    Assert.IsTrue(p.reachable, "chained hops reach everything at jump level " + jump);
                    Assert.GreaterOrEqual(p.fuel, prevFuel, "fuel dropped at d=" + d + " ly (jump level " + jump + ")");
                    Assert.LessOrEqual(p.legLy, _m.JumpRangeLy(jump) + 1e-6, "a leg exceeds the jump range");
                    prevFuel = p.fuel;
                }
            }
        }

        [Test]
        public void BetterEngine_CostsLess_AndEmptyHoldCostsLess_AndDiscountsHelp()
        {
            foreach (double d in new[] { 20.0, 80.0, 300.0, 1200.0, 4500.0 })
            {
                int prev = int.MaxValue;
                for (int e = 0; e <= 10; e++)
                {
                    int fuel = _m.PlanInterSystem(d, 6, e, 0.5, 0.0, false).fuel;
                    Assert.LessOrEqual(fuel, prev, "engine level " + e + " at " + d + " ly");
                    prev = fuel;
                }
                int empty = _m.PlanInterSystem(d, 6, 3, 0.0, 0.0, false).fuel, full = _m.PlanInterSystem(d, 6, 3, 1.0, 0.0, false).fuel;
                Assert.LessOrEqual(empty, full);
                int discounted = _m.PlanInterSystem(d, 6, 3, 0.5, 0.3, false).fuel, plain = _m.PlanInterSystem(d, 6, 3, 0.5, 0.0, false).fuel;
                Assert.LessOrEqual(discounted, plain);
                Assert.Greater(_m.PlanInterSystem(d, 6, 3, 0.5, 0.0, true).fuel, plain, "autopilot costs a premium");
            }
            Assert.IsFalse(_m.PlanInterSystem(50, 0, 0, 0, 0, false).reachable, "no jump drive: no inter-system travel");
        }

        [Test]
        public void LongerTripsCostMorePerLightYear_AndEveryLegStaysInRange()
        {
            // The exponent p > 1 applies to the whole route, so depth is expensive even with a long jump range.
            double prev = 0;
            foreach (double d in new[] { 40.0, 160.0, 640.0, 2560.0 })
            {
                TripPlan p = _m.PlanInterSystem(d, 10, 0, 0, 0, false);
                double perLy = p.fuel / d;
                Assert.Greater(perLy, prev * 0.999, "fuel per ly at " + d);
                prev = perLy;
            }
            TripPlan far = _m.PlanInterSystem(900, 3, 0, 0, 0, false);
            Assert.Greater(far.hops, 5);
            Assert.LessOrEqual(far.legLy, _m.JumpRangeLy(3));
        }

        [Test]
        public void EarlyGame_FirstHopsAreAffordable_AndDeepTripsNeedBetterShips()
        {
            int firstHop = _m.IntraSystemFuelCost(1.0, 0, 0, false);
            Assert.LessOrEqual(firstHop, 10, "the starting 10 fuel pays for the first hop (to a neighbour in the home system)");
            int nearSystem = _m.PlanInterSystem(30, 1, 0, 0.5, 0, false).fuel;
            int farSystem = _m.PlanInterSystem(160, 1, 0, 0.5, 0, false).fuel;
            int deep = _m.PlanInterSystem(1200, 1, 0, 0.5, 0, false).fuel;
            int deepBest = _m.PlanInterSystem(1200, 10, 10, 0.5, 0.0, false).fuel;
            TestLog.Info("first intra-system hop " + firstHop + " fuel; 30 ly " + nearSystem + "; 160 ly " + farSystem + "; 1200 ly with starter ship " + deep + " vs end-game ship " + deepBest);
            Assert.LessOrEqual(nearSystem, 20);
            Assert.Greater(deep, farSystem * 5);
            Assert.Less(deepBest, deep / 2, "late game: a better engine and range make depth much cheaper");
        }

        [Test]
        public void EnergyDenseFuels_PayDeepTripsWithFewItems()
        {
            // A 1,000 energy trip must not need a thousand cells: plasma and antimatter compress it into a few slots.
            FuelPayment p = FuelPayment.Plan(1000, 20, 30, 12);
            Assert.IsTrue(p.ok);
            Assert.GreaterOrEqual(p.fuelCells + p.plasmaCells * 8 + p.antimatterPods * 64, 1000);
            Assert.LessOrEqual(p.fuelCells + p.plasmaCells + p.antimatterPods, 50);
            Assert.IsFalse(FuelPayment.Plan(100, 5, 1, 0).ok, "13 energy cannot pay 100");
        }

        [Test]
        public void FuelPayment_IsGreedyByDensity_AndNeverOverspends()
        {
            var rng = new Rng(77UL);
            for (int i = 0; i < 4000; i++)
            {
                int cost = rng.NextInt(1, 700), fuel = rng.NextInt(0, 120), plasma = rng.NextInt(0, 30), anti = rng.NextInt(0, 8);
                FuelPayment p = FuelPayment.Plan(cost, fuel, plasma, anti);
                long total = fuel + plasma * 8L + anti * 64L;
                Assert.AreEqual(total >= cost, p.ok, "cost " + cost + " fuel " + fuel + " plasma " + plasma + " anti " + anti);
                if (!p.ok) continue;
                Assert.LessOrEqual(p.fuelCells, fuel);
                Assert.LessOrEqual(p.plasmaCells, plasma);
                Assert.LessOrEqual(p.antimatterPods, anti);
                int paid = p.fuelCells + p.plasmaCells * 8 + p.antimatterPods * 64;
                Assert.GreaterOrEqual(paid, cost);
                Assert.AreEqual(paid - cost, p.overpay);
                Assert.Less(p.overpay, 64, "never overpays more than one pod");
                Assert.IsTrue(p.antimatterPods <= anti && p.plasmaCells <= plasma);
            }
            FuelPayment exact = FuelPayment.Plan(137, 100, 100, 100);
            Assert.AreEqual(2, exact.antimatterPods, "antimatter first");
            Assert.AreEqual(1, exact.plasmaCells);
            Assert.AreEqual(1, exact.fuelCells);
            Assert.AreEqual(0, exact.overpay);
        }

        [Test]
        public void Landing_RequiresPlatingForEveryHazard_AndDepthRaisesTheBar()
        {
            int[] none = new int[5];
            Assert.IsTrue(_m.CanLand(none, Hazards.None, 7));
            Assert.IsFalse(_m.CanLand(none, Hazards.Heat, 1));
            Assert.IsFalse(_m.CanLand(none, Hazards.Toxic | Hazards.Cold, 2));
            var heat = new[] { 1, 0, 0, 0, 0 };
            Assert.IsTrue(_m.CanLand(heat, Hazards.Heat, 3));
            Assert.IsFalse(_m.CanLand(heat, Hazards.Heat, 6), "deeper worlds need level 2 plating");
            Assert.IsFalse(_m.CanLand(heat, Hazards.Heat | Hazards.Cold, 2));
            var full = new[] { 3, 3, 3, 3, 3 };
            Assert.IsTrue(_m.CanLand(full, Hazards.Heat | Hazards.Cold | Hazards.Toxic | Hazards.Radiation | Hazards.Void, 30));
            int prev = 0;
            for (int tier = 0; tier <= 20; tier++) { int need = _m.RequiredPlatingLevel(tier); Assert.GreaterOrEqual(need, prev); prev = need; }
            Assert.AreEqual(1, _m.RequiredPlatingLevel(0));
            Assert.AreEqual(3, _m.RequiredPlatingLevel(12));
            // Plating items are the alloys named in the plan.
            Assert.AreEqual(Hazards.Heat, _c.Upgrade("hull_heat").hazard);
            Assert.AreEqual("thermal_alloy", _c.Upgrade("hull_heat").levels[0].cost[0].resourceId);
            Assert.AreEqual("cryo_composite", _c.Upgrade("hull_cold").levels[0].cost[0].resourceId);
            Assert.AreEqual("biocomposite", _c.Upgrade("hull_toxic").levels[0].cost[0].resourceId);
            Assert.AreEqual("rad_shield_alloy", _c.Upgrade("hull_rad").levels[0].cost[0].resourceId);
            Assert.AreEqual("void_alloy", _c.Upgrade("hull_void").levels[0].cost[0].resourceId);
        }

        [Test]
        public void HyperspaceRun_RefundGrowsWithPerformance_AndRunsAreShort()
        {
            int prev = -1;
            for (double q = 0; q <= 1.0001; q += 0.05)
            {
                int r = _m.Refund(100, q);
                Assert.GreaterOrEqual(r, prev);
                prev = r;
            }
            Assert.AreEqual(40, _m.Refund(100, 1.0), "legacy: a perfect run refunds 40%");
            Assert.AreEqual(0, _m.Refund(100, 0.0));
            for (double leg = 1; leg < 4000; leg *= 1.7)
            {
                int s = _m.RunSeconds(leg);
                Assert.GreaterOrEqual(s, 25);
                Assert.LessOrEqual(s, 90);
            }
        }

        [Test]
        public void DistanceTierDangerReward_MatchesTheUniverseGenerator()
        {
            FUniverse u = StarterSystem.CreateUniverse(_c, 0x46524F4E54494552UL);
            var rules = new WorldRules(_c, u.Config);
            var rng = new Rng(5UL);
            for (int i = 0; i < 3000; i++)
            {
                double x = rng.Range(-6000, 6000), y = rng.Range(-6000, 6000);
                Assert.AreEqual(u.TierAt(x, y), rules.TierForDistance(u.DistanceFromHome(x, y)));
            }
            for (int tier = 1; tier <= 12; tier++)
            {
                double d = rules.DistanceForTier(tier);
                Assert.AreEqual(tier, rules.TierForDistance(d + 1e-6), "tier " + tier + " starts at " + d.ToString("0.0") + " ly");
                Assert.AreEqual(tier - 1, rules.TierForDistance(d - 1e-3));
            }
            Assert.AreEqual(1, rules.TierForDistance(33), "tier 1 starts near 33 ly");
            Assert.AreEqual(5, rules.TierForDistance(500), "tier 5 near 460-750 ly");
            Assert.AreEqual(10, rules.TierForDistance(4600), "tier 10 near 4,500 ly");
            Assert.Greater(rules.DangerForTier(5), rules.DangerForTier(4));
            // The yield curve is the game's 0.4 + 1.1 x quality, scaled by tier, richness, tool and structure.
            double y1 = rules.RunYield("wood", 0, 1.0, 1.0, 0.0, 1.0), y0 = rules.RunYield("wood", 0, 1.0, 1.0, 0.0, 0.0);
            Assert.AreEqual(10 * 1.5, y1, 1e-9);
            Assert.AreEqual(10 * 0.4, y0, 1e-9);
            Assert.Greater(rules.RunValue("iron", 5, 1.0, 1.0, 0.0, 0.8), rules.RunValue("iron", 1, 1.0, 1.0, 0.0, 0.8), "deeper deposits pay more per run");
            double[] landmarks = { rules.DistanceForTier(1), rules.DistanceForTier(5), rules.DistanceForTier(10) };
            TestLog.Info("tier starts: T1 " + landmarks[0].ToString("0") + " ly, T5 " + landmarks[1].ToString("0") + " ly, T10 " + landmarks[2].ToString("0") + " ly (home galaxy radius ~1700 ly = tier 7-8)");
        }

        [Test]
        public void JumpRangeTiers_OpenTheMapGradually_OnTheRealUniverse()
        {
            // With the starter jump range only nearby systems are one hop away; each range tier opens more of the real map.
            FUniverse u = StarterSystem.CreateUniverse(_c, 0x46524F4E54494552UL);
            var rules = new WorldRules(_c, u.Config);
            var counts = new int[11];
            var reachTier = new int[11];
            for (int l = 1; l <= 10; l++)
            {
                double range = _m.JumpRangeLy(l);
                int n = 0, maxTier = 0;
                int sr = (int)Math.Min(Math.Ceiling(range / 64.0), 40);
                foreach (StarSystem s in u.SystemsInRect(-range, -range, range, range))
                {
                    if (Math.Sqrt(s.X * s.X + s.Y * s.Y) > range * _t.D("travel.hop_fraction")) continue;
                    n++;
                    maxTier = Math.Max(maxTier, s.Tier);
                }
                counts[l] = n;
                reachTier[l] = maxTier;
            }
            TestLog.Info("systems one hop from home per jump level: " + string.Join(", ", Counts(counts)));
            Assert.GreaterOrEqual(counts[1], 1, "at least one neighbouring system is one hop away at jump level 1");
            Assert.LessOrEqual(counts[1], 12, "jump level 1 opens only the local neighbourhood");
            for (int l = 2; l <= 10; l++) Assert.GreaterOrEqual(counts[l], counts[l - 1]);
            Assert.Greater(counts[5], counts[1] * 8);
            Assert.GreaterOrEqual(reachTier[5], 3);
        }

        IEnumerable<string> Counts(int[] counts) { for (int l = 1; l <= 10; l++) yield return "L" + l + "=" + counts[l]; }
    }
}
