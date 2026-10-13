using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Frontier.Universe.Tests
{
    /// <summary>
    /// The coverage guarantee: in populated space every neighbourhood contains each raw resource its tier allows. The mechanism is
    /// deterministic anchoring per block of 3 x 3 sectors (192 ly); these tests prove it by exact block checks and by a Monte-Carlo
    /// sample of random points inside galaxies.
    /// </summary>
    [TestFixture]
    public class CoverageTests
    {
        /// <summary>The stated guarantee radius in light-years around any populated point (three blocks).</summary>
        public const double StatedRadius = 576.0;

        /// <summary>Density at or above which space counts as populated: at least 0.26 systems per sector on average.</summary>
        public const double PopulatedDensity = 0.10;

        sealed class Sampler
        {
            public Universe U;
            public List<GalaxyInfo> Galaxies = new List<GalaxyInfo>();
            public Rng Rng;

            public Sampler(Universe u, ulong seed)
            {
                U = u;
                Rng = new Rng(seed);
                u.Galaxies.GalaxiesInRect(-16000, -16000, 16000, 16000, Galaxies);
            }

            /// <summary>A random point inside a random galaxy's disk where the density marks populated space.</summary>
            public void NextPoint(out double x, out double y, out GalaxyInfo galaxy)
            {
                while (true)
                {
                    GalaxyInfo g = Galaxies[Rng.NextInt(Galaxies.Count)];
                    double a = Rng.NextDouble() * DetMath.TwoPi, r = Math.Sqrt(Rng.NextDouble()) * g.Radius;
                    g.FromLocal(Math.Cos(a) * r, Math.Sin(a) * r * g.AxisRatio, out x, out y);
                    if (U.Density(x, y) >= PopulatedDensity) { galaxy = g; return; }
                }
            }
        }

        static bool CarriesNearby(Universe u, double x, double y, double radius, string resourceId)
        {
            double r2 = radius * radius;
            int s0x = (int)Math.Floor((x - radius) / 64.0), s1x = (int)Math.Floor((x + radius) / 64.0);
            int s0y = (int)Math.Floor((y - radius) / 64.0), s1y = (int)Math.Floor((y + radius) / 64.0);
            for (int sy = s0y; sy <= s1y; sy++)
            {
                for (int sx = s0x; sx <= s1x; sx++)
                {
                    foreach (StarSystem s in u.SystemsInSector(new SectorCoord(sx, sy)))
                    {
                        double dx = s.X - x, dy = s.Y - y;
                        if (dx * dx + dy * dy > r2) continue;
                        foreach (PlanetDescriptor p in u.PlanetsOf(s)) if (p.HasResource(resourceId)) return true;
                    }
                }
            }
            return false;
        }

        static void RunMonteCarlo(bool planned, int samples, ulong seed, string label)
        {
            var cfg = new UniverseConfig { EnablePlannedResources = planned };
            var u = new Universe(cfg, Landmarks.CreateDefault());
            var sampler = new Sampler(u, seed);
            double[] radii = { 272.0, 400.0, StatedRadius };
            var failures = new int[radii.Length];
            var perResource = new Dictionary<string, int>();
            int required = 0, tierMin = int.MaxValue, tierMax = 0;
            var firstFailure = new List<string>();
            for (int i = 0; i < samples; i++)
            {
                double x, y;
                GalaxyInfo g;
                sampler.NextPoint(out x, out y, out g);
                int tier = u.TierAt(x, y);
                tierMin = Math.Min(tierMin, tier);
                tierMax = Math.Max(tierMax, tier);
                foreach (ResourceDef def in u.Resources.Defs)
                {
                    if (def.MinTier > tier) continue;
                    required++;
                    for (int r = 0; r < radii.Length; r++)
                    {
                        if (CarriesNearby(u, x, y, radii[r], def.Id)) continue;
                        failures[r]++;
                        if (r == radii.Length - 1)
                        {
                            int c;
                            perResource.TryGetValue(def.Id, out c);
                            perResource[def.Id] = c + 1;
                            if (firstFailure.Count < 5) firstFailure.Add(def.Id + " missing within " + radii[r] + " ly of (" + x.ToString("0") + ", " + y.ToString("0") + ") tier " + tier + " in " + g.Name);
                        }
                    }
                }
            }
            string line = "";
            for (int r = 0; r < radii.Length; r++) line += radii[r].ToString("0") + " ly: " + failures[r] + " misses (" + (100.0 * failures[r] / required).ToString("0.000") + " %)   ";
            TestLog.Info(label + ": " + samples + " random populated points in " + sampler.Galaxies.Count + " galaxies (tiers " + tierMin + ".." + tierMax + "), " + required + " (point, resource) requirements with " + u.Resources.Defs.Count + " resource ids");
            TestLog.Info(label + ": failure rate by search radius -> " + line);
            foreach (string f in firstFailure) TestLog.Info("   FAILURE " + f);
            Assert.AreEqual(0, failures[radii.Length - 1], "coverage guarantee violated within " + StatedRadius + " ly: " + string.Join(", ", firstFailure));
        }

        [Test]
        public void MonteCarlo_EveryTierAllowedResourceIsNearby_CurrentResources()
        {
            RunMonteCarlo(false, 6000, 0xC0FFEEUL, "current ids");
        }

        [Test]
        public void MonteCarlo_EveryTierAllowedResourceIsNearby_WithPlannedResources()
        {
            RunMonteCarlo(true, 6000, 0xBEEF01UL, "planned ids enabled");
        }

        [Test]
        public void MonteCarlo_HoldsInAnotherUniverseSeed()
        {
            var cfg = new UniverseConfig { Seed = 987654321UL, EnablePlannedResources = true };
            var u = new Universe(cfg, Landmarks.CreateDefault());
            var sampler = new Sampler(u, 99UL);
            int required = 0, failures = 0;
            for (int i = 0; i < 1500; i++)
            {
                double x, y;
                GalaxyInfo g;
                sampler.NextPoint(out x, out y, out g);
                int tier = u.TierAt(x, y);
                foreach (ResourceDef def in u.Resources.Defs)
                {
                    if (def.MinTier > tier) continue;
                    required++;
                    if (!CarriesNearby(u, x, y, StatedRadius, def.Id)) failures++;
                }
            }
            TestLog.Info("seed 987654321: " + required + " requirements, " + failures + " failures");
            Assert.AreEqual(0, failures);
        }

        [Test]
        public void BlockLevel_EveryBlockWithAnEligiblePlanetCarriesEveryResource()
        {
            foreach (bool planned in new[] { false, true })
            {
                var cfg = new UniverseConfig { EnablePlannedResources = planned };
                var u = new Universe(cfg, Landmarks.CreateDefault());
                var sampler = new Sampler(u, 4242UL);
                int B = cfg.AnchorBlockSectors;
                var seenBlocks = new HashSet<long>();
                int blocks = 0, injectedBlocks = 0, anchors = 0, checkedPairs = 0, skippedNoHost = 0;
                while (blocks < 1500)
                {
                    double x, y;
                    GalaxyInfo g;
                    sampler.NextPoint(out x, out y, out g);
                    SectorCoord sc = u.SectorOf(x, y);
                    int bx = DetMath.FloorDiv(sc.X, B), by = DetMath.FloorDiv(sc.Y, B);
                    if (!seenBlocks.Add(((long)bx << 32) ^ (uint)by)) continue;
                    blocks++;

                    var planets = new List<PlanetDescriptor>();
                    for (int ly = 0; ly < B; ly++)
                        for (int lx = 0; lx < B; lx++)
                            foreach (StarSystem s in u.SystemsInSector(new SectorCoord(bx * B + lx, by * B + ly)))
                                planets.AddRange(u.PlanetsOf(s));
                    int inj = u.AnchorsInjectedFor(sc);
                    if (inj > 0) injectedBlocks++;
                    anchors += inj;

                    foreach (ResourceDef def in u.Resources.Defs)
                    {
                        bool present = false, host = false;
                        foreach (PlanetDescriptor p in planets)
                        {
                            if (p.HasResource(def.Id)) present = true;
                            int effMin = def.MinTier;
                            foreach (ResourceEntry e in u.Resources.ProfileFor(p.Biome, p.Kind).Entries) if (e.ResourceId == def.Id) effMin = e.MinTier;
                            bool strictGas = p.Kind == PlanetKind.GasGiant && BiomeMask.Has(def.AnchorBiomes, Biome.GasGiant);
                            if (p.Tier >= effMin && (p.Kind != PlanetKind.GasGiant || strictGas)) host = true;
                        }
                        if (!host) { skippedNoHost++; continue; }
                        checkedPairs++;
                        Assert.IsTrue(present, "block (" + bx + "," + by + ") has an eligible planet but no " + def.Id + " (planned=" + planned + ")");
                    }
                }
                TestLog.Info((planned ? "planned ids" : "current ids") + ": " + blocks + " populated blocks, " + checkedPairs + " (block, resource) pairs all satisfied; " + skippedNoHost
                    + " pairs had no eligible planet (resource tier too high for the block); " + injectedBlocks + " blocks needed " + anchors + " anchor injections in total");
            }
        }

        [Test]
        public void Anchors_AreDeterministicAndStableUnderCacheEviction()
        {
            var a = new Universe(new UniverseConfig(), Landmarks.CreateDefault());
            var tiny = new UniverseConfig { BlockCacheCapacity = 4, SectorCacheCapacity = 16 };
            var b = new Universe(tiny, Landmarks.CreateDefault());
            for (int y = -20; y < 20; y += 3)
                for (int x = -20; x < 20; x += 3)
                    Assert.AreEqual(a.AnchorsInjectedFor(new SectorCoord(x, y)), b.AnchorsInjectedFor(new SectorCoord(x, y)));
        }
    }
}
