using System;
using System.Collections.Generic;
using System.Reflection;
using Frontier.Economy;
using Frontier.Universe;
using NUnit.Framework;

namespace Frontier.Economy.Tests
{
    [TestFixture, Category("Slow")]
    public class ShardTests
    {
        EconomyCatalog _c;
        ShardRulesDef _r;

        [SetUp]
        public void Setup() { _c = Fx.NewCatalog(); _r = _c.shards; }

        [Test]
        public void SkipCurve_FollowsThePlanFormula_AndEveryPaidSkipCostsAtLeastTheFloor()
        {
            // plan 8.2: shards = ceil(1.2 x minutes^0.75)
            foreach (double minutes in new[] { 1.0, 2.0, 5.0, 10.0, 30.0, 60.0, 180.0, 600.0, 1440.0, 2880.0, 12960.0 })
            {
                int expected = (int)Math.Ceiling(1.2 * Math.Pow(minutes, 0.75) - 1e-9);
                Assert.AreEqual(expected, ShardCurve.SkipCost(_r, minutes * 60.0), "minutes " + minutes);
            }
            Assert.AreEqual(0, ShardCurve.SkipCost(_r, 0));
            Assert.AreEqual(0, ShardCurve.SkipCost(_r, _r.freeFinishSeconds), "the last seconds of any job finish for free");
            Assert.GreaterOrEqual(ShardCurve.SkipCost(_r, _r.freeFinishSeconds + 1), _r.minSkipCost);
            double s = _r.freeFinishSeconds + 1;
            int checkedPoints = 0;
            while (s < 11 * 86400.0)
            {
                int cost = ShardCurve.SkipCost(_r, s);
                Assert.GreaterOrEqual(cost, _r.minSkipCost, "floor at " + s + " s");
                Assert.GreaterOrEqual(cost, Math.Floor(_r.skipMultiplier * Math.Pow(s / 60.0, _r.skipExponent)), "never below the tuned curve at " + s + " s");
                s = s * 1.013 + 1.0;
                checkedPoints++;
            }
            TestLog.Info(checkedPoints + " points checked; 10 min " + ShardCurve.SkipCost(_r, 600) + ", 1 h " + ShardCurve.SkipCost(_r, 3600) + ", 8 h " + ShardCurve.SkipCost(_r, 28800)
                + ", 24 h " + ShardCurve.SkipCost(_r, 86400) + ", 5 days " + ShardCurve.SkipCost(_r, 5 * 86400) + " shards");
            Assert.That(ShardCurve.SkipCost(_r, 86400), Is.InRange(250, 320), "a whole day is a few hundred shards");
        }

        [Test]
        public void SkipCurve_IsMonotone_AndSplittingASkipNeverPaysLess()
        {
            int prev = 0;
            for (double s = 0.0; s < 12 * 86400.0; s = s * 1.01 + 0.5)
            {
                int cost = ShardCurve.SkipCost(_r, s);
                Assert.GreaterOrEqual(cost, prev, "monotone at " + s);
                prev = cost;
            }
            // concave: finishing one job costs no more than finishing it in two paid halves
            for (double a = _r.freeFinishSeconds + 1; a < 4 * 86400.0; a *= 1.7)
                for (double b = _r.freeFinishSeconds + 1; b < 4 * 86400.0; b *= 1.9)
                    Assert.LessOrEqual(ShardCurve.SkipCost(_r, a + b), ShardCurve.SkipCost(_r, a) + ShardCurve.SkipCost(_r, b), a + " + " + b);
        }

        [Test]
        public void ShardsBuyTimeConvenienceAndCosmeticsOnly_NeverResourcesOrPower()
        {
            var names = new List<string>(Enum.GetNames(typeof(ShardSpendKind)));
            CollectionAssert.AreEquivalent(new[] { "SkipTimer", "BuilderSlot", "Revive", "Cosmetic" }, names, "the only things shards can be spent on");
            foreach (Type t in new[] { typeof(ShardRulesDef), typeof(ShardCurve), typeof(BuilderPlan), typeof(ShardLedger), typeof(ShardEarnings), typeof(ShardWeek) })
            {
                foreach (FieldInfo f in t.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
                    Assert.IsFalse(Mentions(f.FieldType), t.Name + "." + f.Name + " deals in resources");
                foreach (MethodInfo m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    Assert.IsFalse(Mentions(m.ReturnType), t.Name + "." + m.Name + " returns resources");
                    foreach (ParameterInfo p in m.GetParameters()) Assert.IsFalse(Mentions(p.ParameterType), t.Name + "." + m.Name + " takes resources");
                }
            }
            // The price of a skip is a function of seconds only: changing every resource price changes nothing.
            Tuning dear = Tuning.CreateDefault();
            foreach (ResourceDef r in _c.resources) if (r.category == ResourceCategory.Raw) dear.Set("value.raw." + r.id, dear.D("value.raw." + r.id) * 3.0);
            ShardRulesDef other = CatalogBuilder.Build(dear).shards;
            foreach (double seconds in new[] { 45.0, 600.0, 3600.0, 86400.0, 604800.0 }) Assert.AreEqual(ShardCurve.SkipCost(_r, seconds), ShardCurve.SkipCost(other, seconds));
        }

        static bool Mentions(Type t)
        {
            if (t.IsArray) t = t.GetElementType();
            return t == typeof(ItemAmount) || t == typeof(ResourceDef) || t == typeof(RecipeDef);
        }

        [Test]
        public void Builders_OneAtStart_TwoByResearch_ThreeAndFourEarnedOrBought_NeverMoreThanTheCap()
        {
            Assert.AreEqual(1, BuilderPlan.Count(_r, false, 0, 0));
            Assert.AreEqual(2, BuilderPlan.Count(_r, true, 0, 0));
            Assert.AreEqual(3, BuilderPlan.Count(_r, true, 1, 0));
            Assert.AreEqual(4, BuilderPlan.Count(_r, true, 2, 0));
            Assert.AreEqual(4, BuilderPlan.Count(_r, true, 2, 2), "bought slots never stack past the cap");
            Assert.AreEqual(3, BuilderPlan.Count(_r, true, 0, 1), "a bought builder is the shortcut to the Colony Hub's third slot");
            Assert.AreEqual(250, BuilderPlan.NextBuilderPrice(_r, 2));
            Assert.AreEqual(600, BuilderPlan.NextBuilderPrice(_r, 3));
            Assert.AreEqual(-1, BuilderPlan.NextBuilderPrice(_r, 4));
            Assert.AreEqual(-1, BuilderPlan.NextBuilderPrice(_r, 1), "the second builder is earned by research, never sold");
            // the earned route in the catalog matches: research node + Colony Hub levels 3 and 7
            Assert.IsNotNull(_c.Research("field_engineering"));
            StructureDef hub = _c.Structure("colony_hub");
            Assert.AreEqual(0.0, hub.levels[1].effect, 1e-9);
            Assert.AreEqual(1.0, hub.levels[2].effect, 1e-9, "level 3 adds the third builder");
            Assert.AreEqual(2.0, hub.levels[6].effect, 1e-9, "level 7 adds the fourth builder");
            Assert.AreEqual(4, BuilderPlan.Count(_r, true, (int)hub.levels[9].effect, 0));
        }

        [Test]
        public void ThePacksAreOrdered_AndBiggerPacksAreBetterValue()
        {
            double prevRate = 0.0;
            int prevShards = 0;
            foreach (ShardPackDef p in _r.packs)
            {
                double rate = p.shards / (double)p.priceTier;
                Assert.Greater(p.shards, prevShards);
                Assert.GreaterOrEqual(rate, prevRate, p.id + " must not be worse value than a smaller pack");
                prevRate = rate; prevShards = p.shards;
            }
            Assert.AreEqual(5, _r.packs.Length, "five consumable tiers (plan 8.3)");
            Assert.Less(_r.packs[0].shards, 150, "the smallest pack cannot skip a day by itself");
        }

        [Test]
        public void TheFaucets_AreCapped_AndNeverPayTwiceForTheSameDiscovery()
        {
            var ledger = new ShardLedger(_c);
            long atlas = 0;
            for (int w = 0; w < 200; w++)
            {
                ShardEarnings e = ledger.Earn(_r, new ShardWeek { levelFrom = 1, levelTo = 1, newResources = 50, newRecipes = 50, newStructureKinds = 50, daysPlayed = 70, adShare = 5.0, boardEntered = true, objectivesDone = 99 });
                atlas += e.atlas;
                Assert.LessOrEqual(e.ads, 7 * _r.adsPerDay * _r.adReward, "the ad cap holds even for absurd input");
                Assert.AreEqual(ShardLedger.StreakShards(_r, 7), e.streak, "a week pays at most one full streak");
                Assert.LessOrEqual(e.achievements, _r.weeklyObjectives * _r.weeklyObjectiveShards, "weekly objectives are capped");
            }
            long maxAtlas = _c.resources.Length * _r.atlasResourceShards + _c.recipes.Length * _r.atlasRecipeShards + _c.structures.Length * _r.atlasStructureShards;
            Assert.AreEqual(maxAtlas, atlas, "Atlas firsts pay exactly once per catalog entry");
            Assert.AreEqual(_c.resources.Length, ledger.resourcesPaid);
            var empty = new ShardLedger(_c).Earn(_r, new ShardWeek { levelFrom = 5, levelTo = 3, newResources = -4, daysPlayed = -2, obstaclesCleared = -9, adShare = -1.0 });
            Assert.AreEqual(0, empty.levelUp + empty.atlas + empty.ads + empty.pods, "negative input pays nothing");
            Assert.Less(ShardLedger.LevelUpShards(_r, 2), ShardLedger.LevelUpShards(_r, 60) + 1);
        }

        [Test]
        public void AFreePlayer_EarnsASensibleAmountPerWeek()
        {
            foreach (string name in new[] { "casual", "regular", "hardcore" })
            {
                SimResult r = SimFx.Run(name, 1UL, 730.0);
                ShardSummary free = ShardEconomy.FreePlayer(_c, r, 0.0, 0.35);
                ShardSummary ads = ShardEconomy.FreePlayer(_c, r, 1.0, 0.35);
                TestLog.Info(name + ": free player " + free.totalEarned + " shards in " + free.weeks.Count + " weeks (mean " + free.meanPerWeek.ToString("0") + ", median " + free.medianPerWeek.ToString("0")
                    + "/week); with every shard ad " + ads.totalEarned + " (mean " + ads.meanPerWeek.ToString("0") + "/week)");
                Assert.That(free.medianPerWeek, Is.InRange(50.0, 100.0), name + " median week");
                Assert.That(free.meanPerWeek, Is.InRange(55.0, 110.0), name + " mean week");
                Assert.That(free.weeks[0].earned.Total, Is.InRange(150, 450), name + ": the first week is the most generous");
                // the first month funds a builder or a cosmetic, the long run a skip or two a week
                long month = free.weeks[3].cumulative;
                Assert.That(month, Is.InRange(350L, 900L), name + " first month");
                Assert.Less(ads.totalEarned, free.totalEarned * 2.0, "opt-in ads supplement the faucets, they never dominate them");
                Assert.Greater(ads.totalEarned, free.totalEarned);
                // free income is flat in play volume: a hardcore player does not out-earn a casual one by more than a third
            }
            long casual = ShardEconomy.FreePlayer(_c, SimFx.Run("casual", 1UL, 730.0), 0.0, 0.35).totalEarned;
            long hardcore = ShardEconomy.FreePlayer(_c, SimFx.Run("hardcore", 1UL, 730.0), 0.0, 0.35).totalEarned;
            Assert.Less(hardcore, casual * 1.4, "the faucets reward showing up, not grinding");
        }

        [Test]
        public void APayingPlayerIsNeverRequired_TimersNeverGateProgressAndShardsNeverEnterTheSimulator()
        {
            // The simulator takes no shard input at all: every era band is met by a player who never spends one.
            foreach (FieldInfo f in typeof(SimSettings).GetFields(BindingFlags.Public | BindingFlags.Instance)) StringAssert.DoesNotContain("shard", f.Name.ToLowerInvariant());
            foreach (string name in new[] { "casual", "regular", "hardcore" })
            {
                SimResult r = SimFx.Run(name, 1UL, 730.0);
                Assert.IsEmpty(SimBands.Check(r), name + " misses an era band with no shards");
                double offered = SimFx.Archetype(name).DailyMinutes * 730.0 * 60.0;
                double waiting = r.idleSeconds;
                TestLog.Info(name + ": " + (waiting / 3600.0).ToString("0.0") + " h of " + (offered / 3600.0).ToString("0") + " offered hours ended early for lack of anything to do (" + (waiting / offered * 100.0).ToString("0.0") + " %)");
            }
            // What shards would save: the regular player's whole two years of ordinary timers cost only a few weeks of free income, so skipping is a convenience;
            // the multi-day megaproject timers are the real time sink.
            SimResult reg = SimFx.Run("regular", 1UL, 730.0);
            ShardSummary s = ShardEconomy.FreePlayer(_c, reg, 0.0, 0.35);
            TestLog.Info("regular: skipping all " + s.ordinaryJobs + " ordinary jobs would cost " + s.skipAllOrdinaryJobs + " shards, all " + s.megaTimers + " megaproject timers " + s.skipAllMegaTimers + " shards, against " + s.totalEarned + " shards of free income");
            Assert.Greater(s.skipAllMegaTimers, s.skipAllOrdinaryJobs * 3, "megaproject timers are where shards buy the most time");
            Assert.Greater(s.skipAllOrdinaryJobs + s.skipAllMegaTimers, s.totalEarned, "free income never covers skipping everything");
        }
    }
}
