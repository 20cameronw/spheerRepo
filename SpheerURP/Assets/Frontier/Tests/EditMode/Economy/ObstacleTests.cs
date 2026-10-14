using System;
using System.Collections.Generic;
using Frontier.Economy;
using Frontier.Universe;
using NUnit.Framework;

namespace Frontier.Economy.Tests
{
    [TestFixture]
    public class ObstacleTests
    {
        const ulong Seed = 0x46524F4E54494552UL;
        EconomyCatalog _c;
        ObstacleRulesDef _r;

        [SetUp]
        public void Setup() { _c = Fx.NewCatalog(); _r = _c.obstacleRules; }

        static readonly Biome[] Solid = { Biome.Lush, Biome.Mineral, Biome.Glacial, Biome.Volcanic, Biome.Fungal, Biome.Crystal, Biome.Obsidian, Biome.Shattered, Biome.Desert, Biome.Oceanic, Biome.Barren };

        ObstacleInstance[] Gen(ulong id, SizeClass size, Biome biome, int tier, bool home = false, Hazards hz = Hazards.None, ulong seed = Seed)
        {
            return ObstacleRules.Generate(_c, seed, id, _r.padsPerSize[(int)size], size, biome, hz, tier, home);
        }

        static string Fingerprint(ObstacleInstance[] a)
        {
            var parts = new List<string>();
            for (int i = 0; i < a.Length; i++)
            {
                var items = new List<string>();
                foreach (ItemAmount c in a[i].cost) items.Add(c.ToString());
                parts.Add(a[i].pad + ":" + a[i].kind + ":" + string.Join("+", items) + ":" + a[i].seconds + ":" + a[i].podShards + ":" + a[i].xp + ":" + a[i].reward);
            }
            return string.Join("|", parts);
        }

        [Test]
        public void Generation_IsDeterministic_PerPlanetAndUniverse()
        {
            for (ulong id = 1; id <= 300; id++)
            {
                SizeClass size = (SizeClass)(id % 6);
                Biome biome = Solid[(int)(id % (ulong)Solid.Length)];
                int tier = (int)(id % 12);
                Assert.AreEqual(Fingerprint(Gen(id, size, biome, tier)), Fingerprint(Gen(id, size, biome, tier)), "planet " + id);
            }
            int differs = 0;
            for (ulong id = 1; id <= 100; id++)
                if (Fingerprint(Gen(id, SizeClass.Large, Biome.Mineral, 3)) != Fingerprint(Gen(id, SizeClass.Large, Biome.Mineral, 3, false, Hazards.None, Seed + 1))) differs++;
            Assert.Greater(differs, 90, "another universe seed lays the clutter out differently");
            Assert.AreNotEqual(Fingerprint(Gen(1, SizeClass.Large, Biome.Mineral, 3)), Fingerprint(Gen(2, SizeClass.Large, Biome.Mineral, 3)));
        }

        [Test]
        public void TheFirstPadsAreAlwaysFree_AndPadsAreUniqueAndInRange()
        {
            int planets = 0, obstacles = 0;
            for (ulong id = 1; id <= 3000; id++)
            {
                SizeClass size = (SizeClass)(id % 6);
                Biome biome = Solid[(int)(id % (ulong)Solid.Length)];
                bool home = id % 17 == 0;
                ObstacleInstance[] list = Gen(id, size, biome, (int)(id % 13), home);
                int pads = _r.padsPerSize[(int)size];
                var seen = new HashSet<int>();
                foreach (ObstacleInstance o in list)
                {
                    Assert.GreaterOrEqual(o.pad, _r.freePads, "planet " + id + " blocks a free pad");
                    Assert.Less(o.pad, pads);
                    Assert.IsTrue(seen.Add(o.pad), "planet " + id + " puts two obstacles on pad " + o.pad);
                    obstacles++;
                }
                Assert.LessOrEqual(list.Length, pads - _r.freePads, "never more obstacles than blockable pads");
                planets++;
            }
            TestLog.Info(planets + " planets, " + obstacles + " obstacles, " + _r.freePads + " free pads each");
            Assert.AreEqual(4, _r.freePads, "the first four pads (the starter structures) are free");
        }

        [Test]
        public void CountsGrowWithTheSizeClass_AndFollowTheDensity()
        {
            double prevMean = 0.0;
            var parts = new List<string>();
            for (int s = 0; s < 6; s++)
            {
                double total = 0.0;
                const int n = 600;
                for (ulong id = 1; id <= n; id++) total += Gen(id * 7919UL + (ulong)s, (SizeClass)s, Biome.Mineral, 3).Length;
                double mean = total / n;
                double expected = _r.padsPerSize[s] * _r.density;
                parts.Add(((SizeClass)s) + " " + _r.padsPerSize[s] + " pads: " + mean.ToString("0.0") + " obstacles (expected " + expected.ToString("0.0") + ")");
                Assert.AreEqual(expected, mean, expected * 0.12 + 0.3, "size " + s);
                Assert.Greater(mean, prevMean, "bigger worlds carry more clutter");
                prevMean = mean;
            }
            TestLog.Info(string.Join("; ", parts));
        }

        [Test]
        public void TheHomeWorldIsMostlyClear()
        {
            foreach (SizeClass size in new[] { SizeClass.Medium, SizeClass.Large, SizeClass.Giant })
            {
                double total = 0.0;
                const int n = 500;
                for (ulong id = 1; id <= n; id++) total += Gen(id, size, Biome.Lush, 0, true).Length;
                double mean = total / n, pads = _r.padsPerSize[(int)size];
                Assert.LessOrEqual(mean / pads, 0.12, size + " home world should stay mostly clear");
                Assert.Less(mean, pads * _r.density * 0.5, "far clearer than an ordinary world");
            }
            Assert.LessOrEqual(_r.homeDensity, 0.12f);
        }

        [Test]
        public void AboutFivePercentOfObstaclesAreShardPods_AndPodsCarryShards()
        {
            int total = 0, pods = 0;
            for (ulong id = 1; id <= 5000; id++)
            {
                foreach (ObstacleInstance o in Gen(id, (SizeClass)(id % 6), Solid[(int)(id % (ulong)Solid.Length)], (int)(id % 12)))
                {
                    total++;
                    if (o.podShards > 0) { pods++; Assert.GreaterOrEqual(o.podShards, _c.shards.podBase); }
                }
            }
            double share = pods / (double)total;
            TestLog.Info(pods + " pods in " + total + " obstacles (" + (share * 100.0).ToString("0.00") + " %)");
            Assert.AreEqual(0.05, share, 0.008);
            Assert.AreEqual(0.05f, _r.shardPodChance, 1e-6);
            Assert.Less(ShardLedger.PodShards(_c.shards, 0), ShardLedger.PodShards(_c.shards, 9), "deeper worlds hide bigger pods");
        }

        [Test]
        public void ClearingCosts_UseOwnedResources_RiseWithTier_AndSalvageStaysBelowTheCost()
        {
            foreach (ObstacleKindDef k in _c.obstacles)
            {
                double prevValue = 0.0;
                for (int tier = 0; tier <= _r.maxTier; tier++)
                {
                    ObstacleTierCost tc = k.byTier[tier];
                    double value = _c.Prices.Value(tc.cost);
                    Assert.Greater(tc.cost.Length, 0, k.id + " T" + tier);
                    foreach (ItemAmount a in tc.cost)
                        Assert.LessOrEqual(_c.Resource(a.resourceId).tier, Math.Max(tier, k.minTier) + 1, k.id + " T" + tier + " asks for " + a.resourceId + " before the player can have it");
                    if (tier > k.minTier) Assert.GreaterOrEqual(value, prevValue * 0.95, k.id + " cost must not fall with the tier (T" + tier + ")");
                    // rows below the kind's first tier never spawn; from there on the salvage is a fraction of the cost
                    double reward = _c.Prices.Value(new[] { tc.reward });
                    if (tier >= k.minTier) Assert.LessOrEqual(reward, value * 0.6 + 1.0, k.id + " T" + tier + ": salvage must not out-earn the cost");
                    prevValue = value;
                }
            }
            // The tax is small next to what a structure costs on the same tier: level 1 of every structure unlocked at tier 3 costs thousands, a tier 3 clearing a few hundred.
            double clear3 = _c.Prices.Value(_c.obstacles[0].byTier[3].cost);
            Assert.Less(clear3, 600.0);
            Assert.Greater(_r.valueGrowth, 1.0f);
        }

        [Test]
        public void ClearingTimes_AreShort_Monotone_AndWithinTheCap()
        {
            foreach (ObstacleKindDef k in _c.obstacles)
            {
                int prev = -1;
                for (int tier = 0; tier <= _r.maxTier; tier++)
                {
                    int s = ObstacleRules.ClearSeconds(_r, k, tier);
                    Assert.LessOrEqual(s, _r.timeCapSeconds, k.id + " T" + tier);
                    Assert.LessOrEqual(s, 5 * 3600, "clearing stays a short job");
                    Assert.GreaterOrEqual(s, prev, k.id + " time must not fall with the tier");
                    prev = s;
                }
                Assert.AreEqual(0, ObstacleRules.ClearSeconds(_r, k, 0), "tier 0 clutter clears at once");
            }
        }

        [Test]
        public void InstantClearPrice_IsTheSkipPriceOfItsTimer()
        {
            for (ulong id = 1; id <= 400; id++)
                foreach (ObstacleInstance o in Gen(id, (SizeClass)(id % 6), Solid[(int)(id % (ulong)Solid.Length)], (int)(id % 12)))
                {
                    Assert.AreEqual(ShardCurve.SkipCost(_c.shards, o.seconds), o.instantShards);
                    if (o.seconds == 0) Assert.AreEqual(0, o.instantShards, "no timer, no price");
                }
        }

        [Test]
        public void EveryKindLivesOnItsBiomes_AndEveryWorldKindCanHostClutter()
        {
            foreach (Biome b in Solid)
                for (int tier = 0; tier <= _r.maxTier; tier++)
                {
                    bool any = false;
                    foreach (ObstacleKindDef k in _c.obstacles) if (tier >= k.minTier && BiomeMask.Has(k.biomeMask, b) && !k.needsVoid) any = true;
                    Assert.IsTrue(any, b + " tier " + tier + " has no clutter kind");
                }
            // Every generated obstacle respects its kind's biome and tier rules.
            for (ulong id = 1; id <= 2000; id++)
            {
                Biome biome = Solid[(int)(id % (ulong)Solid.Length)];
                int tier = (int)(id % 13);
                Hazards hz = id % 5 == 0 ? Hazards.Void : Hazards.None;
                foreach (ObstacleInstance o in Gen(id, SizeClass.Giant, biome, tier, false, hz))
                {
                    ObstacleKindDef k = _c.obstacles[o.kind];
                    Assert.IsTrue(BiomeMask.Has(k.biomeMask, biome), k.id + " on " + biome);
                    Assert.GreaterOrEqual(tier, k.minTier, k.id);
                    if (k.needsVoid) Assert.IsTrue((hz & Hazards.Void) != 0, k.id + " needs the Void hazard");
                }
            }
            Assert.AreEqual(0, Gen(5, SizeClass.Giant, Biome.GasGiant, 4).Length, "gas giants carry no clutter");
        }

        [Test]
        public void ClearingCosts_FitComfortablyInTheBags_AndNeverAskForALotOfOneThing()
        {
            foreach (ObstacleKindDef k in _c.obstacles)
                for (int tier = k.minTier; tier <= _r.maxTier; tier++)
                {
                    int slots = 0;
                    foreach (ItemAmount a in k.byTier[tier].cost)
                    {
                        int stack = _c.Resource(a.resourceId).stackSize;
                        int lineSlots = (a.amount + stack - 1) / stack;
                        slots += lineSlots;
                        Assert.LessOrEqual(lineSlots, 3 + (int)Math.Ceiling(1.2 * tier) + 1, k.id + " T" + tier + " asks for a whole bag of " + a.resourceId);
                    }
                    Assert.LessOrEqual(slots, 16 + 3 * tier, k.id + " T" + tier + " needs " + slots + " slots of items for one clearing");
                }
        }

        [Test]
        public void TheColonyHubDiscount_CutsTheCost_ButNeverBelowOne()
        {
            StructureDef hub = _c.Structure("colony_hub");
            Assert.AreEqual(0, hub.levels[0].capacity);
            Assert.AreEqual(45, hub.levels[9].capacity, "level 10 takes 45 percent off every clearing");
            Assert.AreEqual(55, ObstacleRules.Discounted(100, hub.levels[9].capacity));
            int prev = int.MaxValue;
            for (int pct = 0; pct <= 100; pct += 5) { int v = ObstacleRules.Discounted(40, pct); Assert.LessOrEqual(v, prev); Assert.GreaterOrEqual(v, 1); prev = v; }
            Assert.AreEqual(100, ObstacleRules.Discounted(100, -10));
        }

        [Test]
        public void ObstacleCatalog_ResolvesEveryResource_AndTheKindsAreUnique()
        {
            Assert.IsEmpty(CatalogValidator.Validate(_c), string.Join("\n", CatalogValidator.Validate(_c)));
            var ids = new HashSet<string>();
            var icons = new HashSet<string>();
            foreach (ObstacleKindDef k in _c.obstacles) { Assert.IsTrue(ids.Add(k.id)); Assert.IsTrue(icons.Add(k.iconConcept), k.id + " shares a silhouette"); }
            Assert.GreaterOrEqual(_c.obstacles.Length, 6);
        }
    }
}
