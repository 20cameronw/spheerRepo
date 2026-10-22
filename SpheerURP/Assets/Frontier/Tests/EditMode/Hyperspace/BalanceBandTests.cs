using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Frontier.Hyperspace.Tests
{
    /// <summary>
    /// Statistical balance bands, measured with the deterministic bots (novice / average / expert) over seeded routes. The ship is the one a player is expected to have for the tier
    /// (HyperspaceTuning.ExpectedPower). Results are averaged over route lengths 5 / 50 / 200 / 800 / 3000 ly. Because every run is seeded the numbers are exactly reproducible; the
    /// bands document the design intent and leave room for small retunes. The measured tables are in reports\balance_report.txt.
    /// </summary>
    [TestFixture]
    [Category("Heavy")]
    public class BalanceBandTests
    {
        static readonly float[] Dists = { 5f, 50f, 200f, 800f, 3000f };
        static readonly Dictionary<string, Agg> Cache = new Dictionary<string, Agg>();

        public sealed class Agg
        {
            public double survival, goodShare, bPlusShare, hits, killRatio;
            public int cells;
            public override string ToString()
            {
                return "survival " + (survival * 100).ToString("0") + "%  S/A " + (goodShare * 100).ToString("0") + "%  B+ " + (bPlusShare * 100).ToString("0") + "%  hits " + hits.ToString("0.0") + "  kills " + (killRatio * 100).ToString("0") + "%";
            }
        }

        static Agg Measure(string skill, int[] tiers, float[] dists, int runs = 32, float shipProgress = -1f)
        {
            string key = skill + "|" + string.Join(",", tiers) + "|" + string.Join(",", dists) + "|" + runs + "|" + shipProgress;
            lock (Cache)
            {
                Agg cached;
                if (Cache.TryGetValue(key, out cached)) return cached;
            }
            HyperspaceTuning t = TestEnv.Tuning();
            var a = new Agg();
            foreach (int tier in tiers)
            {
                float prog = shipProgress >= 0f ? shipProgress : Balance.ProgressForPower(t, t.ExpectedPower(tier));
                foreach (float d in dists)
                {
                    ScenarioStats s = Balance.RunScenario(t, TestEnv.Lib, d, tier, Balance.DangerFor(tier), prog, BotSkill.ByName(skill), runs, 91UL);
                    a.survival += s.Survival;
                    a.goodShare += s.GoodShare;
                    a.bPlusShare += s.BPlusShare;
                    a.hits += s.HitsPerRun;
                    a.killRatio += s.killRatio / s.runs;
                    a.cells++;
                }
            }
            a.survival /= a.cells; a.goodShare /= a.cells; a.bPlusShare /= a.cells; a.hits /= a.cells; a.killRatio /= a.cells;
            lock (Cache) Cache[key] = a;
            return a;
        }

        static readonly int[] G0 = { 0, 1, 2 }, G1 = { 3, 4, 5 }, G2 = { 6, 7, 8 }, G3 = { 9, 10, 12 };

        [Test]
        public void FirstHopsAreGentleForNovices()
        {
            Agg a = Measure("novice", G0, new[] { 5f, 50f });
            Console.WriteLine("    novice, tiers 0-2, 5/50 ly: " + a);
            Assert.GreaterOrEqual(a.survival, 0.97, "novices must survive first hops");
            Assert.GreaterOrEqual(a.bPlusShare, 0.93, "and usually earn at least a B");
            Assert.GreaterOrEqual(a.goodShare, 0.55, "with S/A common");
            Assert.LessOrEqual(a.goodShare, 0.97, "but not a free S every time");
        }

        [Test]
        public void SurvivalAndGradeBandsPerSkillAndTier()
        {
            var table = new[]
            {
                // skill, tiers, min survival, S/A min, S/A max
                new { skill = "novice", tiers = G0, surv = 0.97, lo = 0.55, hi = 0.95 },
                new { skill = "average", tiers = G0, surv = 0.99, lo = 0.92, hi = 1.00 },
                new { skill = "expert", tiers = G0, surv = 0.99, lo = 0.95, hi = 1.00 },
                new { skill = "novice", tiers = G1, surv = 0.95, lo = 0.25, hi = 0.65 },
                new { skill = "average", tiers = G1, surv = 0.98, lo = 0.60, hi = 0.95 },
                new { skill = "expert", tiers = G1, surv = 0.99, lo = 0.80, hi = 1.00 },
                new { skill = "novice", tiers = G2, surv = 0.93, lo = 0.00, hi = 0.35 },
                new { skill = "average", tiers = G2, surv = 0.97, lo = 0.35, hi = 0.80 },
                new { skill = "expert", tiers = G2, surv = 0.99, lo = 0.65, hi = 0.98 },
                new { skill = "novice", tiers = G3, surv = 0.75, lo = 0.00, hi = 0.15 },
                new { skill = "average", tiers = G3, surv = 0.90, lo = 0.00, hi = 0.40 },
                new { skill = "expert", tiers = G3, surv = 0.97, lo = 0.25, hi = 0.75 },
            };
            foreach (var row in table)
            {
                Agg a = Measure(row.skill, row.tiers, Dists);
                string label = row.skill.PadRight(8) + "tiers " + string.Join("/", row.tiers);
                Console.WriteLine("    " + label + ": " + a + "   [band survival >= " + (row.surv * 100).ToString("0") + "%, S/A " + (row.lo * 100).ToString("0") + "-" + (row.hi * 100).ToString("0") + "%]");
                Assert.GreaterOrEqual(a.survival, row.surv, label + " survival");
                Assert.GreaterOrEqual(a.goodShare, row.lo, label + " S/A share too low");
                Assert.LessOrEqual(a.goodShare, row.hi, label + " S/A share too high");
            }
        }

        [Test]
        public void SkillMattersFromTierThreeOn()
        {
            foreach (int[] g in new[] { G1, G2, G3 })
            {
                Agg n = Measure("novice", g, Dists), a = Measure("average", g, Dists), e = Measure("expert", g, Dists);
                string label = "tiers " + string.Join("/", g);
                Assert.LessOrEqual(n.goodShare, a.goodShare + 0.03, label + ": novice must not beat average");
                Assert.LessOrEqual(a.goodShare, e.goodShare + 0.03, label + ": average must not beat expert");
                Assert.GreaterOrEqual(n.hits, e.hits + 0.5, label + ": novices take clearly more hits (" + n.hits.ToString("0.0") + " vs " + e.hits.ToString("0.0") + ")");
                if (g != G3) Assert.GreaterOrEqual(e.goodShare - n.goodShare, 0.30, label + ": the expert-novice S/A gap must be visible");
            }
        }

        [Test]
        public void HitsGrowWithTierAndWithRouteLength()
        {
            double previous = -1;
            foreach (int tier in new[] { 0, 3, 6, 9, 12 })
            {
                Agg a = Measure("average", new[] { tier }, new[] { 50f, 200f });
                Assert.GreaterOrEqual(a.hits, previous - 0.15, "average-bot hits should not fall as the tier rises (tier " + tier + ")");
                previous = a.hits;
            }
            previous = -1;
            foreach (float d in Dists)
            {
                Agg a = Measure("novice", new[] { 4 }, new[] { d });
                Assert.GreaterOrEqual(a.hits, previous - 0.25, "novice hits should not fall as the route gets longer (" + d + " ly)");
                previous = a.hits;
            }
            Agg low = Measure("novice", new[] { 0 }, Dists), high = Measure("novice", new[] { 12 }, Dists);
            Assert.Greater(high.hits, low.hits * 3, "tier 12 is much harder than tier 0");
        }

        [Test]
        public void ShipPowerMatters()
        {
            Agg starter = Measure("novice", new[] { 6 }, new[] { 50f }, 48, 0f);
            Agg mid = Measure("novice", new[] { 6 }, new[] { 50f }, 48, 0.5f);
            Agg end = Measure("novice", new[] { 6 }, new[] { 50f }, 48, 1f);
            Console.WriteLine("    novice tier 6, 50 ly - starter: " + starter + " | mid: " + mid + " | endgame: " + end);
            Assert.LessOrEqual(starter.survival, 0.70, "a starter ship on a tier-6 route is underpowered for a novice");
            Assert.GreaterOrEqual(mid.survival, 0.95);
            Assert.GreaterOrEqual(end.survival, mid.survival - 0.03);
            Assert.Less(starter.killRatio, mid.killRatio);
            Assert.LessOrEqual(starter.goodShare, mid.goodShare);
        }

        [Test]
        public void EndgameShipOnTierZeroIsAPowerTripButNotAFreePass()
        {
            Agg starter = Measure("novice", new[] { 0 }, new[] { 5f, 50f, 200f }, 48, 0f);
            Agg power = Measure("novice", new[] { 0 }, new[] { 5f, 50f, 200f }, 48, 1f);
            Console.WriteLine("    novice tier 0 - starter: " + starter + " | endgame ship: " + power);
            Assert.GreaterOrEqual(power.survival, 0.99);
            Assert.GreaterOrEqual(power.goodShare, 0.85, "the endgame ship dominates easy routes");
            Assert.GreaterOrEqual(power.killRatio, 0.97, "everything dies");
            Assert.GreaterOrEqual(power.goodShare, starter.goodShare - 0.02);
            Agg longRoute = Measure("novice", new[] { 0 }, new[] { 3000f }, 48, 1f);
            Console.WriteLine("    novice tier 0, 3000 ly, endgame ship: " + longRoute);
            Assert.Greater(longRoute.hits, 0.2, "even a maxed ship has to dodge on the longest routes (Guardian)");
            Assert.Less(longRoute.goodShare, 1.0, "not every run is an S");
            Agg expert = Measure("expert", new[] { 0 }, new[] { 3000f }, 48, 1f);
            Assert.GreaterOrEqual(expert.goodShare, 0.97, "skilled pilots still clean it up");
        }

        [Test]
        public void ReviveNeverLeavesARunStuck()
        {
            HyperspaceTuning t = TestEnv.Tuning();
            for (int tier = 6; tier <= 12; tier += 3)
            {
                for (ulong seed = 1; seed <= 6; seed++)
                {
                    HyperspaceRun run = TestEnv.PlayOut(TestEnv.Route(seed, 3000f, tier, 1f), ShipStats.Starter(), BotSkill.Novice(), t, true);
                    Assert.AreEqual(RunState.Ended, run.State, "the run always ends (tier " + tier + " seed " + seed + ")");
                    Assert.LessOrEqual(run.RevivesUsed, t.maxRevives);
                    RunResult r = run.BuildResult();
                    if (r.defeated) { Assert.AreEqual(0f, r.fuelRefundFraction); Assert.AreEqual(0, r.lootList.Length); }
                    else Assert.IsTrue(r.reachedEnd);
                }
            }
        }
    }
}
