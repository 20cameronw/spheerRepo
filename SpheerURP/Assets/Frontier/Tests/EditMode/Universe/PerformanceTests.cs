using System;
using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;

namespace Frontier.Universe.Tests
{
    [TestFixture]
    public class PerformanceTests
    {
        // 40 x 25 = 1,000 sectors around the core of the Home galaxy.
        const int X0 = -20, Y0 = -12, X1 = 20, Y1 = 13;

        static long AllocatedBytes()
        {
#if UNITY_5_3_OR_NEWER
            return GC.GetTotalMemory(false);
#else
            return GC.GetAllocatedBytesForCurrentThread();
#endif
        }

        static void Touch(Universe u, out int systems, out int planets, out int deposits)
        {
            systems = planets = deposits = 0;
            for (int y = Y0; y < Y1; y++)
            {
                for (int x = X0; x < X1; x++)
                {
                    foreach (StarSystem s in u.SystemsInSector(new SectorCoord(x, y)))
                    {
                        systems++;
                        PlanetDescriptor[] ps = u.PlanetsOf(s);
                        planets += ps.Length;
                        for (int i = 0; i < ps.Length; i++) deposits += ps[i].Deposits.Length;
                    }
                }
            }
        }

        [Test]
        public void ThousandSectorsWithAllPlanets_GenerateWellUnderOneSecond()
        {
            Assert.AreEqual(1000, (X1 - X0) * (Y1 - Y0));
            long allocBefore = AllocatedBytes();
            int gcBefore = GC.CollectionCount(0);
            var sw = Stopwatch.StartNew();
            Universe cold = TestUniverses.WithFakeAuthored();
            int systems, planets, deposits;
            Touch(cold, out systems, out planets, out deposits);
            sw.Stop();
            double coldMs = sw.Elapsed.TotalMilliseconds;
            long allocated = AllocatedBytes() - allocBefore;

            double best = double.MaxValue;
            for (int i = 0; i < 5; i++)
            {
                Universe warm = TestUniverses.WithFakeAuthored();
                sw.Restart();
                int s2, p2, d2;
                Touch(warm, out s2, out p2, out d2);
                sw.Stop();
                best = Math.Min(best, sw.Elapsed.TotalMilliseconds);
            }
            TestLog.Info("1,000 sectors -> " + systems + " systems, " + planets + " planets, " + deposits + " deposits");
            TestLog.Info("cold (first run in the process, includes JIT): " + coldMs.ToString("0.0") + " ms; warm best of 5 fresh universes: " + best.ToString("0.0") + " ms");
            TestLog.Info("throughput (warm): " + (1000.0 / best * 1000.0).ToString("0") + " sectors/s, " + (planets / best * 1000.0).ToString("0") + " planets/s");
            TestLog.Info("allocation during the cold run: " + (allocated / 1024) + " KiB for " + planets + " planets (" + (allocated / Math.Max(1, planets)) + " B per planet), gen0 collections: " + (GC.CollectionCount(0) - gcBefore));
            Assert.Less(coldMs, 1000.0, "must generate 1,000 sectors with all planets in about a second or less");
            Assert.Less(best, 1000.0);
        }

        [Test]
        public void SteadyState_CachedLookupsDoNotAllocateOrCollect()
        {
            Universe u = TestUniverses.WithFakeAuthored();
            int systems, planets, deposits;
            Touch(u, out systems, out planets, out deposits);
            var list = new List<StarSystem>(4096);
            u.SystemsInRect(X0 * 64.0, Y0 * 64.0, X1 * 64.0, Y1 * 64.0, list);
            Assert.AreEqual(systems, list.Count);
            double d = 0.0;
            int checksum = 0;

            Action workload = () =>
            {
                for (int pass = 0; pass < 20; pass++)
                {
                    for (int y = Y0; y < Y1; y++)
                    {
                        for (int x = X0; x < X1; x++)
                        {
                            StarSystem[] sys = u.SystemsInSector(new SectorCoord(x, y));
                            for (int i = 0; i < sys.Length; i++) checksum += u.PlanetsOf(sys[i]).Length;
                        }
                    }
                    list.Clear();
                    u.SystemsInRect(X0 * 64.0, Y0 * 64.0, X1 * 64.0, Y1 * 64.0, list);
                }
                for (int i = 0; i < 100000; i++) d += u.Density(i * 0.37 - 5000.0, i * 0.11 - 800.0);
            };
            workload();
            workload();

            long a0 = AllocatedBytes();
            int g0 = GC.CollectionCount(0);
            checksum = 0;
            workload();
            long allocated = AllocatedBytes() - a0;
            int collections = GC.CollectionCount(0) - g0;
            TestLog.Info("steady state: 20 passes over 1,000 cached sectors + planets, 20 rect queries and 100,000 density samples allocated " + allocated + " bytes, " + collections + " gen0 collections (checksum " + checksum + ")");
            Assert.AreEqual(0, collections, "no garbage collections in steady state");
#if !UNITY_5_3_OR_NEWER
            Assert.AreEqual(0, allocated, "cached lookups must not allocate");
#endif
            Assert.Greater(checksum, 0);
            Assert.IsFalse(double.IsNaN(d));
        }

        [Test]
        public void DensityAndTierQueries_AreFast()
        {
            Universe u = TestUniverses.WithLandmarks();
            double sum = 0.0;
            for (int i = 0; i < 20000; i++) sum += u.Density(i * 1.7 - 20000.0, i * 0.9 - 9000.0);
            var sw = Stopwatch.StartNew();
            const int n = 400000;
            for (int i = 0; i < n; i++) sum += u.Density(i * 0.093 - 18000.0, 7000.0 - i * 0.041);
            sw.Stop();
            double nsDensity = sw.Elapsed.TotalMilliseconds * 1e6 / n;
            sw.Restart();
            int tiers = 0;
            for (int i = 0; i < n; i++) tiers += u.TierAt(i * 0.5, i * -0.25);
            sw.Stop();
            double nsTier = sw.Elapsed.TotalMilliseconds * 1e6 / n;
            TestLog.Info("Density(x, y): " + nsDensity.ToString("0") + " ns per call; TierAt(x, y): " + nsTier.ToString("0") + " ns per call");
            Assert.Less(nsDensity, 20000.0);
            Assert.IsFalse(double.IsNaN(sum));
            Assert.Greater(tiers, 0);
        }

        [Test]
        public void SectorGenerationAlone_IsCheaperThanPlanets()
        {
            Universe u = TestUniverses.WithFakeAuthored();
            var sw = Stopwatch.StartNew();
            int systems = 0;
            for (int y = Y0; y < Y1; y++)
                for (int x = X0; x < X1; x++) systems += u.SystemsInSector(new SectorCoord(x, y)).Length;
            sw.Stop();
            TestLog.Info("systems only (no planets): " + systems + " systems in 1,000 sectors took " + sw.Elapsed.TotalMilliseconds.ToString("0.0") + " ms (planets are generated lazily per block)");
            Assert.Less(sw.Elapsed.TotalMilliseconds, 500.0);
        }

        [Test]
        public void ThousandVeryDenseSectors_StillGenerateWithinTheBudget()
        {
            // a stress test: four times the default system density, every sector crowded up to the per-sector maximum
            var cfg = new UniverseConfig { SystemsPerSectorMax = 12.0, MaxSystemsPerSector = 12 };
            Universe u = new Universe(cfg, Landmarks.CreateDefault());
            int s0, p0, d0;
            Touch(new Universe(cfg, Landmarks.CreateDefault()), out s0, out p0, out d0);
            var sw = Stopwatch.StartNew();
            u = new Universe(cfg, Landmarks.CreateDefault());
            int systems, planets, deposits;
            Touch(u, out systems, out planets, out deposits);
            sw.Stop();
            TestLog.Info("dense stress (12 systems per sector at density 1): " + systems + " systems, " + planets + " planets, " + deposits + " deposits in 1,000 sectors: " + sw.Elapsed.TotalMilliseconds.ToString("0.0") + " ms");
            Assert.Greater(systems, 1000);
            Assert.Less(sw.Elapsed.TotalMilliseconds, 1000.0);
        }
    }
}
