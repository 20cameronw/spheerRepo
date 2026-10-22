using System;
using System.Collections.Generic;
using System.Reflection;
using Frontier.Economy;
using Frontier.Universe;
using NUnit.Framework;

namespace Frontier.Economy.Tests
{
    [TestFixture, Category("Slow")]
    public class ScoreTests
    {
        ScoreFormula _f;

        [SetUp]
        public void Setup() { _f = ScoreFormula.From(Tuning.CreateDefault()); }

        static ScoreInputs MidGame()
        {
            return new ScoreInputs
            {
                maxDistanceFromHomeLy = 420.0,
                worldsLandedByTier = new[] { 1, 6, 9, 7, 4, 2 },
                structureLevels = new[] { 5, 4, 6, 3, 2, 1, 7 },
                structureTiers = new[] { 0, 0, 1, 1, 2, 3, 0 },
                upgradeLevelsTotal = 21,
                toolTierPurchases = 9,
                waveWorldDanger = new[] { 2.0, 3.5 },
                waveWorldCleared = new[] { 8, 3 },
                resourcesDiscovered = 19,
                recipesCrafted = 12,
                megaprojectStages = 0,
                ascensionRank = 0
            };
        }

        static ScoreInputs Copy(ScoreInputs s)
        {
            return new ScoreInputs
            {
                maxDistanceFromHomeLy = s.maxDistanceFromHomeLy, worldsLandedByTier = (int[])s.worldsLandedByTier.Clone(), structureLevels = (int[])s.structureLevels.Clone(),
                structureTiers = (int[])s.structureTiers.Clone(), upgradeLevelsTotal = s.upgradeLevelsTotal, toolTierPurchases = s.toolTierPurchases,
                waveWorldDanger = (double[])s.waveWorldDanger.Clone(), waveWorldCleared = (int[])s.waveWorldCleared.Clone(), resourcesDiscovered = s.resourcesDiscovered,
                recipesCrafted = s.recipesCrafted, megaprojectStages = s.megaprojectStages, ascensionRank = s.ascensionRank
            };
        }

        static int[] Grow(int[] a, int index, int by)
        {
            var b = (int[])a.Clone();
            if (index >= b.Length) Array.Resize(ref b, index + 1);
            b[index] += by;
            return b;
        }

        /// <summary>Every way an input can increase, as a mutation of a copy.</summary>
        static readonly Action<ScoreInputs, int>[] Increases =
        {
            (s, k) => s.maxDistanceFromHomeLy += 5.0 + k,
            (s, k) => s.worldsLandedByTier = Grow(s.worldsLandedByTier, k % 12, 1 + k % 7),
            (s, k) => { if (s.structureLevels.Length > 0) s.structureLevels[k % s.structureLevels.Length] += 1; },
            (s, k) => { var l = new List<int>(s.structureLevels); var t = new List<int>(s.structureTiers); l.Add(1 + k % 3); t.Add(k % 7); s.structureLevels = l.ToArray(); s.structureTiers = t.ToArray(); },
            (s, k) => s.upgradeLevelsTotal += 1 + k % 3,
            (s, k) => s.toolTierPurchases += 1 + k % 2,
            (s, k) => { if (s.waveWorldCleared.Length > 0) s.waveWorldCleared[k % s.waveWorldCleared.Length] += 1 + k % 40; },
            (s, k) => { var d = new List<double>(s.waveWorldDanger); var c = new List<int>(s.waveWorldCleared); d.Add(1.0 + k % 9); c.Add(1 + k % 30); s.waveWorldDanger = d.ToArray(); s.waveWorldCleared = c.ToArray(); },
            (s, k) => s.resourcesDiscovered += 1,
            (s, k) => s.recipesCrafted += 1,
            (s, k) => s.megaprojectStages += 1,
            (s, k) => s.ascensionRank += 1
        };

        [Test]
        public void TheScore_NeverDecreasesWhenAnyInputIncreases()
        {
            for (int m = 0; m < Increases.Length; m++)
                for (int k = 0; k < 25; k++)
                {
                    ScoreInputs a = MidGame();
                    long before = ScoreCalculator.Compute(a, _f).total;
                    ScoreInputs b = Copy(a);
                    Increases[m](b, k);
                    long after = ScoreCalculator.Compute(b, _f).total;
                    Assert.GreaterOrEqual(after, before, "mutation " + m + " step " + k);
                }
            // Fuzz: random states, a random increase each.
            var rng = new Rng(0xC0FFEEUL);
            for (int i = 0; i < 4000; i++)
            {
                var s = new ScoreInputs
                {
                    maxDistanceFromHomeLy = rng.Range(0, 5000),
                    worldsLandedByTier = new int[1 + rng.NextInt(12)],
                    structureLevels = new int[rng.NextInt(40)],
                    upgradeLevelsTotal = rng.NextInt(150),
                    toolTierPurchases = rng.NextInt(40),
                    resourcesDiscovered = rng.NextInt(35),
                    recipesCrafted = rng.NextInt(25),
                    megaprojectStages = rng.NextInt(16),
                    ascensionRank = rng.NextInt(6)
                };
                for (int t = 0; t < s.worldsLandedByTier.Length; t++) s.worldsLandedByTier[t] = rng.NextInt(300);
                s.structureTiers = new int[s.structureLevels.Length];
                for (int t = 0; t < s.structureLevels.Length; t++) { s.structureLevels[t] = rng.NextInt(11); s.structureTiers[t] = rng.NextInt(8); }
                int wv = rng.NextInt(6);
                s.waveWorldDanger = new double[wv];
                s.waveWorldCleared = new int[wv];
                for (int t = 0; t < wv; t++) { s.waveWorldDanger[t] = rng.Range(0, 12); s.waveWorldCleared[t] = rng.NextInt(200); }
                long before = ScoreCalculator.Compute(s, _f).total;
                ScoreInputs b = Copy(s);
                Increases[rng.NextInt(Increases.Length)](b, rng.NextInt(1000));
                Assert.GreaterOrEqual(ScoreCalculator.Compute(b, _f).total, before, "fuzz " + i);
            }
        }

        [Test]
        public void EveryUncappedInput_StrictlyRaisesTheScore()
        {
            long baseline = ScoreCalculator.Compute(MidGame(), _f).total;
            string[] names = { "reach", "worlds", "structure level", "new structure", "upgrades", "tools", "wave", "new wave world", "resources", "recipes", "mega stage", "ascension" };
            for (int m = 0; m < Increases.Length; m++)
            {
                ScoreInputs b = MidGame();
                Increases[m](b, 3);
                Assert.Greater(ScoreCalculator.Compute(b, _f).total, baseline, names[m] + " must be worth something");
            }
        }

        [Test]
        public void AntiFarm_WavesOnOneWorldStopPayingAtTheCap_AndWorldsBeyondFiveHundredPayAFifth()
        {
            ScoreInputs a = MidGame();
            a.waveWorldDanger = new[] { 4.0 };
            a.waveWorldCleared = new[] { _f.defenseWaveCap };
            ScoreInputs b = Copy(a);
            b.waveWorldCleared = new[] { 100000 };
            Assert.AreEqual(ScoreCalculator.Compute(a, _f).total, ScoreCalculator.Compute(b, _f).total, "farming one world adds nothing past the cap");
            ScoreBreakdown capped = ScoreCalculator.Compute(b, _f);
            Assert.AreEqual(_f.defenseBase * Math.Sqrt(4.0) * _f.defenseWaveCap, capped.defense, 1e-6);
            // worlds: 500 at tier 5 pay in full, the 501st pays the excess share
            ScoreInputs w = MidGame();
            w.worldsLandedByTier = new[] { 0, 0, 0, 0, 0, 500 };
            double full = ScoreCalculator.Compute(w, _f).worlds;
            w.worldsLandedByTier = new[] { 0, 0, 0, 0, 0, 501 };
            double marginal = ScoreCalculator.Compute(w, _f).worlds - full;
            Assert.AreEqual(_f.worldBase * 6 * _f.worldExcessShare, marginal, 1e-6, "the 501st world counts at 20 percent");
            Assert.AreEqual(_f.worldFullValueCount, 500);
            // a better world still helps once the cap is reached (it takes a full slot and pushes a weaker one to the excess rate)
            w.worldsLandedByTier = new[] { 0, 0, 0, 0, 0, 500, 0, 0, 0, 1 };
            Assert.Greater(ScoreCalculator.Compute(w, _f).worlds, full + marginal * 2);
        }

        [Test]
        public void TheScore_IsDeterministic_PureAndIndependentOfOrder()
        {
            ScoreInputs a = MidGame();
            string before = Dump(a);
            ScoreBreakdown x = ScoreCalculator.Compute(a, _f), y = ScoreCalculator.Compute(a, _f);
            Assert.AreEqual(x.total, y.total);
            Assert.AreEqual(before, Dump(a), "computing the score does not touch the input");
            ScoreInputs p = Copy(a);
            Array.Reverse(p.structureLevels);
            Array.Reverse(p.structureTiers);
            Array.Reverse(p.waveWorldDanger);
            Array.Reverse(p.waveWorldCleared);
            Assert.AreEqual(x.total, ScoreCalculator.Compute(p, _f).total, "the order of the lists does not matter");
            Assert.AreEqual(x.total, (long)Math.Floor((x.reach + x.worlds + x.industry + x.tech + x.defense + x.atlas) * x.multiplier), "the breakdown explains the total");
            ScoreInputs asc = Copy(a);
            asc.ascensionRank = 3;
            Assert.AreEqual(Math.Floor((x.reach + x.worlds + x.industry + x.tech + x.defense + x.atlas) * 1.3), ScoreCalculator.Compute(asc, _f).total, "ascension multiplies by 1 + 0.10 x rank");
        }

        static string Dump(ScoreInputs s)
        {
            return s.maxDistanceFromHomeLy + "|" + string.Join(",", s.worldsLandedByTier) + "|" + string.Join(",", s.structureLevels) + "|" + string.Join(",", s.structureTiers) + "|" + s.upgradeLevelsTotal + "|" + s.toolTierPurchases
                + "|" + string.Join(",", s.waveWorldDanger) + "|" + string.Join(",", s.waveWorldCleared) + "|" + s.resourcesDiscovered + "|" + s.recipesCrafted + "|" + s.megaprojectStages + "|" + s.ascensionRank;
        }

        [Test]
        public void TheScore_CannotBeBought_NoPremiumInputExists()
        {
            string[] banned = { "shard", "premium", "iap", "gem", "coin", "paid", "money", "currency", "ad" };
            foreach (Type t in new[] { typeof(ScoreInputs), typeof(ScoreFormula), typeof(ScoreBreakdown) })
            {
                foreach (FieldInfo f in t.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
                    foreach (string b in banned)
                    {
                        string name = f.Name.ToLowerInvariant();
                        // "ad" is only forbidden as a whole word start ("ads", "advert"); no field may begin with a premium word
                        if (b == "ad") Assert.IsFalse(name == "ad" || name.StartsWith("ads") || name.StartsWith("advert"), t.Name + "." + f.Name);
                        else StringAssert.DoesNotContain(b, name, t.Name + "." + f.Name + " looks like a premium input");
                    }
                foreach (MethodInfo m in t.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                    foreach (ParameterInfo p in m.GetParameters())
                        Assert.IsFalse(p.ParameterType == typeof(ShardLedger) || p.ParameterType == typeof(ShardWeek) || p.ParameterType == typeof(ShardRulesDef), t.Name + "." + m.Name + " takes a shard type");
            }
            // The fields are exactly the plan 8.1 inputs.
            var names = new List<string>();
            foreach (FieldInfo f in typeof(ScoreInputs).GetFields(BindingFlags.Public | BindingFlags.Instance)) names.Add(f.Name);
            CollectionAssert.AreEquivalent(new[]
            {
                "maxDistanceFromHomeLy", "worldsLandedByTier", "structureLevels", "structureTiers", "upgradeLevelsTotal", "toolTierPurchases", "waveWorldDanger", "waveWorldCleared",
                "resourcesDiscovered", "recipesCrafted", "megaprojectStages", "ascensionRank"
            }, names);
        }

        [Test]
        public void TheSimulatedTrajectory_LandsInThePlanTargets()
        {
            // plan 8.1: early hours 5-10 k, mid about 100 k, end game 1 M+
            foreach (string name in new[] { "casual", "regular", "hardcore" })
            {
                SimResult r = SimFx.Run(name, 1UL, 730.0);
                long h1 = r.ScoreAtPlayHours(1.0), h3 = r.ScoreAtPlayHours(3.0);
                TestLog.Info(name + ": " + r.ScoreAtPlayHours(0.25) + " after 15 min, " + h1 + " after 1 h, " + h3 + " after 3 h, " + r.ScoreAtPlayHours(10.0) + " after 10 h; Industry entry "
                    + r.ScoreAtDay(r.Days("era_industry")) + ", Interstellar entry " + r.ScoreAtDay(r.Days("era_interstellar")) + ", Ascension entry " + r.ScoreAtDay(r.Days("era_ascension")) + ", day 730: " + r.finalScore);
                Assert.That(h1, Is.InRange(5000L, 11000L), name + ": the first hour is worth 5-10 k");
                Assert.That(h3, Is.InRange(9000L, 22000L), name + ": three hours");
                Assert.That(r.ScoreAtDay(r.Days("era_industry")), Is.InRange(20000L, 45000L), name + " Industry entry");
                Assert.That(r.ScoreAtDay(r.Days("era_interstellar")), Is.InRange(55000L, 100000L), name + " Interstellar entry is the mid game, about 100 k within the era");
                Assert.That(r.ScoreAtDay(r.Days("era_ascension")), Is.InRange(100000L, 250000L), name + " Ascension entry");
            }
            SimResult reg = SimFx.Run("regular", 1UL, 730.0), hard = SimFx.Run("hardcore", 1UL, 730.0), cas = SimFx.Run("casual", 1UL, 730.0);
            Assert.GreaterOrEqual(hard.finalScore, 1000000L, "a hardcore profile that has finished the content passes one million");
            Assert.GreaterOrEqual(reg.finalScore, 400000L, "a regular player passes 400 k within two years");
            Assert.Less(cas.finalScore, reg.finalScore);
            Assert.Less(reg.finalScore, hard.finalScore);
            // the 100 k mark: reached inside the Interstellar era by the regular player
            double day100k = 0.0;
            foreach (SimSample s in reg.samples) if (s.score >= 100000) { day100k = s.days; break; }
            Assert.That(day100k, Is.InRange(reg.Days("era_interstellar"), reg.Days("era_ascension") + 1.0));
        }

        [Test]
        public void TheWeightsAreData_AndEveryKeyIsInTheTuningSurface()
        {
            Tuning t = Tuning.CreateDefault();
            foreach (string key in new[] { "score.reach_coefficient", "score.world_base", "score.industry_base", "score.industry_exponent", "score.tech_per_upgrade_level", "score.tech_per_tool_tier", "score.defense_base",
                "score.defense_wave_cap", "score.atlas_per_resource", "score.atlas_per_recipe", "score.atlas_per_mega_stage", "score.ascension_step", "score.world_full_count", "score.world_excess_share", "score.reach_divisor_ly" })
                Assert.IsTrue(t.Has(key), key);
            t.Set("score.world_base", 260);
            ScoreFormula doubled = ScoreFormula.From(t);
            Assert.AreEqual(2.0 * _f.worldBase, doubled.worldBase, 1e-9);
            Assert.Greater(ScoreCalculator.Compute(MidGame(), doubled).worlds, ScoreCalculator.Compute(MidGame(), _f).worlds * 1.99);
        }
    }
}
