using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Frontier.Universe.Tests
{
    [TestFixture]
    public class PlanetTests
    {
        Dataset _d;

        [OneTimeSetUp]
        public void Setup()
        {
            _d = Dataset.Shared;
        }

        [Test]
        public void Planets_HaveValidIdentityAndCountsPerSystem()
        {
            var u = _d.Universe;
            var cfg = u.Config;
            var ids = new HashSet<string>();
            foreach (StarSystem s in _d.Systems)
            {
                PlanetDescriptor[] ps = u.PlanetsOf(s);
                Assert.AreEqual(s.PlanetCount, ps.Length);
                Assert.GreaterOrEqual(ps.Length, 1);
                Assert.LessOrEqual(ps.Length, 7);
                float lastOrbit = 0f;
                for (int i = 0; i < ps.Length; i++)
                {
                    PlanetDescriptor p = ps[i];
                    Assert.AreEqual(new PlanetKey(s.Key, i), p.Key);
                    Assert.AreEqual(p.Key.ToString(), p.Id);
                    Assert.IsTrue(ids.Add(p.Id), "duplicate id " + p.Id);
                    Assert.IsFalse(string.IsNullOrEmpty(p.Name));
                    Assert.AreEqual(s.Designation + " " + (char)('b' + i), p.Designation);
                    Assert.Greater(p.OrbitRadius, lastOrbit, "orbits must grow outward");
                    lastOrbit = p.OrbitRadius;
                    Assert.IsTrue(p.OrbitPhase >= 0f && p.OrbitPhase < 6.2832f);
                    Assert.AreEqual(1, p.GeneratorVersion);
                    PlanetDescriptor found;
                    Assert.IsTrue(u.TryGetPlanetById(p.Id, out found));
                    Assert.AreSame(p, found);
                    Assert.AreSame(p, u.GetPlanet(p.Key));
                }
                Assert.IsNull(u.GetPlanet(new PlanetKey(s.Key, ps.Length)));
            }
            TestLog.Info("verified identity of " + ids.Count + " planets");
        }

        [Test]
        public void BuildingSlotsAndWorldSize_FollowTheSizeClass()
        {
            var cfg = _d.Universe.Config;
            var seenSizes = new HashSet<SizeClass>();
            foreach (PlanetDescriptor p in _d.Planets)
            {
                Assert.GreaterOrEqual(p.BuildingSlots, 12);
                Assert.AreEqual(cfg.BuildingSlotsBySize[(int)p.Size], p.BuildingSlots);
                Assert.AreEqual(cfg.WorldSizeBySize[(int)p.Size], p.WorldSize);
                seenSizes.Add(p.Size);
            }
            Assert.AreEqual(6, seenSizes.Count, "all six size classes must occur");
            int[] expected = { 12, 16, 24, 36, 48, 64 };
            for (int i = 0; i < 6; i++) Assert.AreEqual(expected[i], cfg.BuildingSlotsBySize[i]);
        }

        [Test]
        public void Tier_RisesWithDistanceFromHome()
        {
            var u = _d.Universe;
            double[] edges = { 0, 50, 100, 200, 400, 800, 1500, 2300, 4000, 6000 };
            var sum = new double[edges.Length - 1];
            var cnt = new int[edges.Length - 1];
            var dist = new List<double>();
            var tier = new List<double>();
            foreach (PlanetDescriptor p in _d.Planets)
            {
                StarSystem s = _d.SystemOfPlanet[p.Key.Id];
                double d = u.DistanceFromHome(s.X, s.Y);
                int baseTier = u.TierAt(s.X, s.Y);
                Assert.AreEqual(baseTier, s.Tier);
                Assert.LessOrEqual(Math.Abs(p.Tier - baseTier), 1, "anomalies are at most +-1");
                Assert.GreaterOrEqual(p.Tier, 0);
                Assert.LessOrEqual(p.Tier, 40);
                for (int b = 0; b < sum.Length; b++)
                    if (d >= edges[b] && d < edges[b + 1]) { sum[b] += p.Tier; cnt[b]++; }
                dist.Add(d);
                tier.Add(p.Tier);
            }
            double last = -1.0;
            var line = new System.Text.StringBuilder();
            for (int b = 0; b < sum.Length; b++)
            {
                if (cnt[b] < 15) continue;
                double avg = sum[b] / cnt[b];
                line.Append("[" + edges[b] + "-" + edges[b + 1] + " ly: " + avg.ToString("0.00") + " n=" + cnt[b] + "] ");
                Assert.GreaterOrEqual(avg, last, "average tier must not decrease with distance (bin " + b + ")");
                last = avg;
            }
            TestLog.Info("mean planet tier by distance: " + line);
            Assert.Greater(Spearman(dist, tier), 0.8);

            // the base tier itself is an exactly monotone function of the distance
            int previous = 0;
            for (double d = 0.0; d < 3.0e8; d = d < 1.0 ? d + 0.25 : d * 1.01)
            {
                int t = u.Config.TierFromDistance(d);
                Assert.GreaterOrEqual(t, previous, "tier must never decrease with distance (d = " + d + ")");
                previous = t;
            }
            Assert.AreEqual(0, u.TierAt(0.0, 0.0));
            Assert.AreEqual(40, u.TierAt(1.0e12, 0.0));
            Assert.AreEqual(0, u.Config.TierFromDistance(31.9));
            Assert.AreEqual(1, u.Config.TierFromDistance(33.0));
        }

        static double Spearman(List<double> a, List<double> b)
        {
            double[] ra = Ranks(a), rb = Ranks(b);
            double n = a.Count, ma = 0, mb = 0;
            for (int i = 0; i < a.Count; i++) { ma += ra[i]; mb += rb[i]; }
            ma /= n; mb /= n;
            double sab = 0, saa = 0, sbb = 0;
            for (int i = 0; i < a.Count; i++)
            {
                sab += (ra[i] - ma) * (rb[i] - mb);
                saa += (ra[i] - ma) * (ra[i] - ma);
                sbb += (rb[i] - mb) * (rb[i] - mb);
            }
            return sab / Math.Sqrt(saa * sbb);
        }

        static double[] Ranks(List<double> v)
        {
            var idx = new int[v.Count];
            for (int i = 0; i < idx.Length; i++) idx[i] = i;
            Array.Sort(idx, (x, y) => v[x].CompareTo(v[y]));
            var r = new double[v.Count];
            int pos = 0;
            while (pos < idx.Length)
            {
                int end = pos;
                while (end + 1 < idx.Length && v[idx[end + 1]] == v[idx[pos]]) end++;
                double avg = (pos + end) / 2.0;
                for (int k = pos; k <= end; k++) r[idx[k]] = avg;
                pos = end + 1;
            }
            return r;
        }

        [Test]
        public void TierAnomalies_AreOccasionalPlusOrMinusOne()
        {
            int shifted = 0, up = 0, down = 0, eligible = 0;
            foreach (PlanetDescriptor p in _d.Planets)
            {
                StarSystem s = _d.SystemOfPlanet[p.Key.Id];
                if (s.Tier == 0) continue;
                eligible++;
                if (p.Tier > s.Tier) { shifted++; up++; }
                else if (p.Tier < s.Tier) { shifted++; down++; }
            }
            double frac = (double)shifted / eligible;
            TestLog.Info("tier anomalies: " + (frac * 100).ToString("0.0") + " % of planets (+1: " + up + ", -1: " + down + ")");
            Assert.AreEqual(0.12, frac, 0.02);
            Assert.AreEqual(up, down, eligible * 0.02);
        }

        [Test]
        public void Danger_IsBaseTimesTierPlusNoise()
        {
            var cfg = _d.Universe.Config;
            double maxDev = 0.0;
            foreach (PlanetDescriptor p in _d.Planets)
            {
                double expected = cfg.DangerBase + cfg.DangerPerTier * p.Tier;
                maxDev = Math.Max(maxDev, Math.Abs(p.Danger - expected));
                Assert.GreaterOrEqual(p.Danger, 0.1f);
            }
            TestLog.Info("largest deviation of danger from 0.6 + 1.1 x tier: " + maxDev.ToString("0.000") + " (noise allows +-" + cfg.DangerNoise / 2 + ")");
            Assert.LessOrEqual(maxDev, cfg.DangerNoise / 2.0 + 0.001);
        }

        [Test]
        public void Hazards_CorrelateWithBiomeStarAndDepth()
        {
            var u = _d.Universe;
            var n = new int[12];
            var heat = new int[12];
            var cold = new int[12];
            var toxic = new int[12];
            var rad = new int[12];
            int lushEarly = 0, lushEarlySafe = 0;
            foreach (PlanetDescriptor p in _d.Planets)
            {
                int b = (int)p.Biome;
                n[b]++;
                if ((p.Hazards & Hazards.Heat) != 0) heat[b]++;
                if ((p.Hazards & Hazards.Cold) != 0) cold[b]++;
                if ((p.Hazards & Hazards.Toxic) != 0) toxic[b]++;
                if ((p.Hazards & Hazards.Radiation) != 0) rad[b]++;
                if (p.Biome == Biome.Lush && p.Tier <= 2)
                {
                    lushEarly++;
                    if (p.Hazards == Hazards.None) lushEarlySafe++;
                }
                if ((p.Hazards & Hazards.Void) != 0)
                {
                    StarSystem s = _d.SystemOfPlanet[p.Key.Id];
                    bool deepSpace = p.Tier >= u.Config.VoidMinTier && (s.GalaxyId == 0UL || s.StarClass == StarClass.BlackHole);
                    bool shattered = p.Biome == Biome.Shattered && p.Tier >= 6;
                    Assert.IsTrue(deepSpace || shattered, "Void hazard needs depth: " + p);
                }
            }
            Assert.GreaterOrEqual(Share(heat, n, Biome.Volcanic), 0.99);
            Assert.GreaterOrEqual(Share(heat, n, Biome.Desert), 0.99);
            Assert.GreaterOrEqual(Share(cold, n, Biome.Glacial), 0.99);
            Assert.GreaterOrEqual(Share(toxic, n, Biome.Fungal), 0.99);
            Assert.GreaterOrEqual(Share(toxic, n, Biome.GasGiant), 0.99);
            Assert.GreaterOrEqual(Share(rad, n, Biome.Obsidian), 0.99);
            Assert.GreaterOrEqual(Share(rad, n, Biome.Shattered), 0.99);
            Assert.Less(Share(heat, n, Biome.Glacial), 0.05);
            Assert.Less(Share(cold, n, Biome.Volcanic), 0.05);
            Assert.Less(Share(heat, n, Biome.Lush), 0.05);
            Assert.Less(Share(rad, n, Biome.Lush), 0.15);
            double safe = (double)lushEarlySafe / lushEarly;
            TestLog.Info("hazard share: heat|volcanic " + Share(heat, n, Biome.Volcanic).ToString("0.000") + ", cold|glacial " + Share(cold, n, Biome.Glacial).ToString("0.000")
                + ", toxic|fungal " + Share(toxic, n, Biome.Fungal).ToString("0.000") + ", radiation|obsidian " + Share(rad, n, Biome.Obsidian).ToString("0.000")
                + "; hazard-free early lush worlds " + (safe * 100).ToString("0.0") + " % of " + lushEarly);
            Assert.Greater(safe, 0.85);
        }

        static double Share(int[] hits, int[] total, Biome b)
        {
            Assert.Greater(total[(int)b], 20, "need samples of " + b);
            return (double)hits[(int)b] / total[(int)b];
        }

        [Test]
        public void StarClass_DrivesRadiationHazard()
        {
            int hotPlanets = 0, hotRad = 0;
            foreach (PlanetDescriptor p in _d.Planets)
            {
                StarClass c = _d.SystemOfPlanet[p.Key.Id].StarClass;
                if (c == StarClass.O || c == StarClass.B || c == StarClass.Neutron || c == StarClass.BlackHole)
                {
                    hotPlanets++;
                    if ((p.Hazards & Hazards.Radiation) != 0) hotRad++;
                }
            }
            TestLog.Info("planets of O/B/neutron/black-hole stars with radiation: " + hotRad + "/" + hotPlanets);
            Assert.Greater(hotPlanets, 20);
            Assert.AreEqual(hotPlanets, hotRad);
        }

        [Test]
        public void Biomes_FollowTemperatureAndAllAppear()
        {
            var sum = new double[12];
            var n = new int[12];
            foreach (PlanetDescriptor p in _d.Planets)
            {
                sum[(int)p.Biome] += p.Temperature;
                n[(int)p.Biome]++;
            }
            var line = new System.Text.StringBuilder();
            for (int b = 0; b < 12; b++)
            {
                Assert.Greater(n[b], 0, "biome " + (Biome)b + " never generated");
                line.Append((Biome)b + " " + n[b] + " (" + (sum[b] / n[b]).ToString("0") + " K)  ");
            }
            TestLog.Info("planets per biome with mean temperature: " + line);
            double Mean(Biome b) { return sum[(int)b] / n[(int)b]; }
            Assert.Less(Mean(Biome.Glacial), Mean(Biome.Lush) - 40.0);
            Assert.Less(Mean(Biome.Lush), Mean(Biome.Desert));
            Assert.Less(Mean(Biome.Desert), Mean(Biome.Volcanic));
            Assert.Greater(Mean(Biome.Volcanic), 380.0);
            Assert.Greater(Mean(Biome.Volcanic), Mean(Biome.Desert) + 15.0);
            Assert.Less(Mean(Biome.Glacial), 230.0);
            Assert.AreEqual(285.0, Mean(Biome.Lush), 45.0);
        }

        [Test]
        public void PlanetKinds_AreConsistentWithBiomeAndSize()
        {
            int gas = 0, belts = 0, rocky = 0, ringedGas = 0;
            double gasMoons = 0, rockyMoons = 0;
            foreach (PlanetDescriptor p in _d.Planets)
            {
                switch (p.Kind)
                {
                    case PlanetKind.GasGiant:
                        gas++;
                        Assert.AreEqual(Biome.GasGiant, p.Biome);
                        Assert.IsTrue(p.Size >= SizeClass.Large);
                        gasMoons += p.MoonCount;
                        if (p.HasRings) ringedGas++;
                        break;
                    case PlanetKind.AsteroidBelt:
                        belts++;
                        Assert.AreEqual(Biome.Barren, p.Biome);
                        Assert.AreEqual(SizeClass.Dwarf, p.Size);
                        Assert.AreEqual(0, p.MoonCount);
                        Assert.IsFalse(p.HasRings);
                        break;
                    default:
                        rocky++;
                        Assert.AreNotEqual(Biome.GasGiant, p.Biome);
                        rockyMoons += p.MoonCount;
                        break;
                }
            }
            TestLog.Info("rocky " + rocky + ", gas giants " + gas + " (" + ringedGas + " ringed, " + (gasMoons / gas).ToString("0.0") + " moons avg), asteroid belts " + belts);
            Assert.Greater(gas, 100);
            Assert.Greater(belts, 50);
            Assert.Greater(gasMoons / gas, rockyMoons / rocky + 1.0);
            Assert.Greater((double)ringedGas / gas, 0.3);
        }

        [Test]
        public void VisualParameters_AreInRange()
        {
            var cfg = _d.Universe.Config;
            foreach (PlanetDescriptor p in _d.Planets)
            {
                foreach (Rgb c in new[] { p.Primary, p.Secondary, p.Rim })
                {
                    Assert.IsTrue(c.R >= 0f && c.G >= 0f && c.B >= 0f, p.Id);
                    Assert.IsTrue(c.R <= 3.6f && c.G <= 3.6f && c.B <= 3.6f, p.Id);
                    Assert.IsFalse(float.IsNaN(c.R + c.G + c.B));
                }
                Assert.IsTrue(p.Primary.R <= 1f && p.Primary.G <= 1f && p.Primary.B <= 1f, "primary is LDR");
                Assert.IsTrue(p.Style >= 0 && p.Style <= 4);
                Assert.IsTrue(p.SurfaceBands >= 2f && p.SurfaceBands <= 20f, "bands " + p.SurfaceBands);
                Assert.IsTrue(p.GlowIntensity >= 0f && p.GlowIntensity <= 2f);
                Assert.IsTrue(p.AtmosphereIntensity >= 0f && p.AtmosphereIntensity <= 2f);
                Assert.IsTrue(Array.IndexOf(cfg.ModelKeys, p.ModelKey) >= 0, "model " + p.ModelKey);
                Assert.IsTrue(Array.IndexOf(cfg.BiomeModelKeys[(int)p.Biome], p.ModelKey) >= 0, p.Biome + " uses " + p.ModelKey);
                Assert.IsTrue(p.Temperature > 0f);
            }
        }

        [Test]
        public void ModelKeys_UseAllEighteenCalibratedModels()
        {
            var seen = new HashSet<string>();
            foreach (PlanetDescriptor p in _d.Planets) seen.Add(p.ModelKey);
            TestLog.Info("model keys used: " + seen.Count + " of " + _d.Universe.Config.ModelKeys.Length);
            Assert.AreEqual(18, _d.Universe.Config.ModelKeys.Length);
            Assert.AreEqual(18, seen.Count);
        }

        [Test]
        public void Weather_MatchesTheBiome()
        {
            int clearVolcanic = 0, embers = 0, volcanic = 0, snowGlacial = 0, glacial = 0, spores = 0, fungal = 0;
            foreach (PlanetDescriptor p in _d.Planets)
            {
                if (p.Biome == Biome.Volcanic) { volcanic++; if (p.Weather == Weather.Embers) embers++; if (p.Weather == Weather.Clear) clearVolcanic++; }
                if (p.Biome == Biome.Glacial) { glacial++; if (p.Weather == Weather.Snow) snowGlacial++; }
                if (p.Biome == Biome.Fungal) { fungal++; if (p.Weather == Weather.Spores) spores++; }
                if (p.Weather == Weather.Embers) Assert.AreEqual(Biome.Volcanic, p.Biome);
                if (p.Weather == Weather.Spores) Assert.AreEqual(Biome.Fungal, p.Biome);
                if (p.Weather == Weather.Snow) Assert.IsTrue(p.Biome == Biome.Glacial || p.Biome == Biome.Crystal);
            }
            Assert.AreEqual(0, clearVolcanic);
            Assert.Greater((double)embers / volcanic, 0.7);
            Assert.Greater((double)snowGlacial / glacial, 0.7);
            Assert.Greater((double)spores / fungal, 0.7);
        }
    }
}
