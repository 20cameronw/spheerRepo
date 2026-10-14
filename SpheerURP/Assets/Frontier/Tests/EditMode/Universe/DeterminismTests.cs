using System;
using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;

namespace Frontier.Universe.Tests
{
    /// <summary>Exact (bit for bit) comparison of generated objects.</summary>
    public static class Exact
    {
        public static string Diff(StarSystem a, StarSystem b)
        {
            if (a.Key != b.Key) return "Key";
            if (a.Id != b.Id) return "Id";
            if (a.Name != b.Name) return "Name";
            if (a.Designation != b.Designation) return "Designation";
            if (a.X != b.X || a.Y != b.Y) return "Position";
            if (a.StarClass != b.StarClass) return "StarClass";
            if (!a.Color.Equals(b.Color)) return "Color";
            if (a.Luminosity != b.Luminosity) return "Luminosity";
            if (a.SurfaceTemperature != b.SurfaceTemperature) return "SurfaceTemperature";
            if (a.HabitableInner != b.HabitableInner || a.HabitableOuter != b.HabitableOuter) return "HabitableBand";
            if (a.GalaxyId != b.GalaxyId) return "GalaxyId";
            if (a.Tier != b.Tier) return "Tier";
            if (a.PlanetCount != b.PlanetCount) return "PlanetCount";
            return null;
        }

        public static string Diff(PlanetDescriptor a, PlanetDescriptor b)
        {
            if (a.Key != b.Key) return "Key";
            if (a.Id != b.Id) return "Id";
            if (a.AuthoredId != b.AuthoredId) return "AuthoredId";
            if (a.Name != b.Name) return "Name";
            if (a.Designation != b.Designation) return "Designation";
            if (a.Kind != b.Kind) return "Kind";
            if (a.Biome != b.Biome) return "Biome";
            if (a.Size != b.Size) return "Size";
            if (a.WorldSize != b.WorldSize) return "WorldSize";
            if (a.BuildingSlots != b.BuildingSlots) return "BuildingSlots";
            if (a.Tier != b.Tier) return "Tier";
            if (a.Danger != b.Danger) return "Danger";
            if (a.Hazards != b.Hazards) return "Hazards";
            if (a.Weather != b.Weather) return "Weather";
            if (a.OrbitRadius != b.OrbitRadius) return "OrbitRadius";
            if (a.OrbitPhase != b.OrbitPhase) return "OrbitPhase";
            if (a.Temperature != b.Temperature) return "Temperature";
            if (a.HasRings != b.HasRings) return "HasRings";
            if (a.MoonCount != b.MoonCount) return "MoonCount";
            if (!a.Primary.Equals(b.Primary) || !a.Secondary.Equals(b.Secondary) || !a.Rim.Equals(b.Rim)) return "Colours";
            if (a.Style != b.Style) return "Style";
            if (a.SurfaceBands != b.SurfaceBands) return "SurfaceBands";
            if (a.GlowIntensity != b.GlowIntensity) return "GlowIntensity";
            if (a.AtmosphereIntensity != b.AtmosphereIntensity) return "AtmosphereIntensity";
            if (a.ModelKey != b.ModelKey) return "ModelKey";
            if (a.GeneratorVersion != b.GeneratorVersion) return "GeneratorVersion";
            if (a.Deposits.Length != b.Deposits.Length) return "Deposits.Length";
            for (int i = 0; i < a.Deposits.Length; i++) if (!a.Deposits[i].Equals(b.Deposits[i])) return "Deposits[" + i + "]";
            return null;
        }

        /// <summary>Compares two universes over a sector region; returns the number of planets compared (asserts on the first difference).</summary>
        public static int AssertSameRegion(Universe a, Universe b, int x0, int y0, int x1, int y1, out int systemsCompared)
        {
            int planets = 0;
            systemsCompared = 0;
            for (int y = y0; y < y1; y++)
            {
                for (int x = x0; x < x1; x++)
                {
                    var sc = new SectorCoord(x, y);
                    StarSystem[] sa = a.SystemsInSector(sc), sb = b.SystemsInSector(sc);
                    Assert.AreEqual(sa.Length, sb.Length, "system count in " + sc);
                    for (int i = 0; i < sa.Length; i++)
                    {
                        string d = Diff(sa[i], sb[i]);
                        Assert.IsNull(d, "system " + sa[i].Id + " differs in " + d);
                        systemsCompared++;
                        PlanetDescriptor[] pa = a.PlanetsOf(sa[i]), pb = b.PlanetsOf(sb[i]);
                        Assert.AreEqual(pa.Length, pb.Length);
                        for (int p = 0; p < pa.Length; p++)
                        {
                            d = Diff(pa[p], pb[p]);
                            Assert.IsNull(d, "planet " + pa[p].Id + " differs in " + d);
                            planets++;
                        }
                    }
                }
            }
            return planets;
        }
    }

    [TestFixture]
    public class DeterminismTests
    {
        // 50 x 40 = 2,000 sectors around the origin.
        const int X0 = -25, Y0 = -20, X1 = 25, Y1 = 20;

        [Test]
        public void SameUniverseTwice_IsIdenticalFieldByField()
        {
            Universe a = TestUniverses.WithFakeAuthored();
            Universe b = TestUniverses.WithFakeAuthored();
            int systems;
            int planets = Exact.AssertSameRegion(a, b, X0, Y0, X1, Y1, out systems);
            TestLog.Info("compared " + (X1 - X0) * (Y1 - Y0) + " sectors, " + systems + " systems and " + planets + " planets field by field between two instances");
            Assert.GreaterOrEqual((X1 - X0) * (Y1 - Y0), 2000);
            Assert.Greater(planets, 1000);

            a.ClearCaches();
            int s2;
            Exact.AssertSameRegion(a, b, X0, Y0, X1, Y1, out s2);
            Assert.AreEqual(systems, s2);
        }

        [Test]
        public void Digest_IsIndependentOfInstanceCachesAndAccessOrder()
        {
            Universe a = TestUniverses.WithFakeAuthored();
            ulong d1 = UniverseDigest.OfRegion(a, X0, Y0, X1, Y1);
            a.ClearCaches();
            ulong d2 = UniverseDigest.OfRegion(a, X0, Y0, X1, Y1);
            Assert.AreEqual(d1, d2, "after clearing the caches");

            Universe fresh = TestUniverses.WithFakeAuthored();
            Assert.AreEqual(d1, UniverseDigest.OfRegion(fresh, X0, Y0, X1, Y1), "fresh instance");

            // touch sectors in reverse and shuffled order before digesting
            Universe reversed = TestUniverses.WithFakeAuthored();
            for (int y = Y1 - 1; y >= Y0; y--)
                for (int x = X1 - 1; x >= X0; x--)
                    foreach (StarSystem s in reversed.SystemsInSector(new SectorCoord(x, y))) reversed.PlanetsOf(s);
            Assert.AreEqual(d1, UniverseDigest.OfRegion(reversed, X0, Y0, X1, Y1), "reverse order");

            var tiny = new UniverseConfig { SectorCacheCapacity = 16, BlockCacheCapacity = 4, GalaxyCacheCapacity = 16 };
            Universe small = new Universe(tiny, FakeAuthored.Create(tiny));
            Assert.AreEqual(d1, UniverseDigest.OfRegion(small, X0, Y0, X1, Y1), "tiny LRU caches that evict constantly");
            CacheStats st = small.GetCacheStats();
            TestLog.Info("tiny-cache universe: " + st.SectorMisses + " sector generations for " + (X1 - X0) * (Y1 - Y0) + " sectors; digest 0x" + d1.ToString("X16"));
        }

        [Test]
        public void ConcurrentAccess_ProducesTheSameUniverse()
        {
            Universe reference = TestUniverses.WithFakeAuthored();
            ulong expected = UniverseDigest.OfRegion(reference, X0, Y0, X1, Y1);

            Universe shared = TestUniverses.WithFakeAuthored();
            var errors = new List<string>();
            var threads = new List<Thread>();
            for (int t = 0; t < 4; t++)
            {
                int id = t;
                var th = new Thread(() =>
                {
                    try
                    {
                        var rng = new Rng((ulong)(1000 + id));
                        for (int i = 0; i < 6000; i++)
                        {
                            var sc = new SectorCoord(rng.NextInt(X0, X1), rng.NextInt(Y0, Y1));
                            foreach (StarSystem s in shared.SystemsInSector(sc)) shared.PlanetsOf(s);
                        }
                    }
                    catch (Exception e)
                    {
                        lock (errors) errors.Add(e.ToString());
                    }
                });
                threads.Add(th);
                th.Start();
            }
            foreach (Thread th in threads) th.Join();
            Assert.AreEqual(0, errors.Count, errors.Count > 0 ? errors[0] : "");
            Assert.AreEqual(expected, UniverseDigest.OfRegion(shared, X0, Y0, X1, Y1), "four threads hammering one instance");
        }

        [Test]
        public void SeedGeneratorVersionAndPlannedFlag_ChangeTheUniverse()
        {
            ulong baseline = UniverseDigest.OfRegion(TestUniverses.WithFakeAuthored(), -12, -10, 12, 10);
            var cfg = new UniverseConfig { Seed = 12345UL };
            Assert.AreNotEqual(baseline, UniverseDigest.OfRegion(new Universe(cfg, FakeAuthored.Create(cfg)), -12, -10, 12, 10));
            var planned = new UniverseConfig { EnablePlannedResources = true };
            Assert.AreNotEqual(baseline, UniverseDigest.OfRegion(new Universe(planned, FakeAuthored.Create(planned)), -12, -10, 12, 10));
            Assert.Throws<ArgumentException>(() => new Universe(new UniverseConfig { GeneratorVersion = 2 }), "V2 does not exist yet");
            Assert.Throws<NotSupportedException>(() => TestUniverses.Plain().GetPlanet(new PlanetKey(0, 0, 0, 0), 2));
        }

        [Test]
        public void ConfigIsSnapshottedByTheUniverse()
        {
            var cfg = new UniverseConfig();
            Universe u = new Universe(cfg, Landmarks.CreateDefault());
            ulong before = UniverseDigest.OfRegion(u, -5, -5, 5, 5);
            cfg.Seed = 99UL;
            cfg.HaloDensity = 0.5;
            cfg.BuildingSlotsBySize[0] = 99;
            u.ClearCaches();
            Assert.AreEqual(before, UniverseDigest.OfRegion(u, -5, -5, 5, 5));
        }

        // ---------------------------------------------------------------------------------------------------------------
        // GOLDEN: generator V1 is frozen. These constants may only change together with a new GeneratorVersion.
        // ---------------------------------------------------------------------------------------------------------------

        /// <summary>Pure generator (no authored content), 50 x 40 sectors that contain the core and arms of a generated galaxy.</summary>
        public const ulong GoldenPlain = 0x8133861D42016686UL;
        public const int PlainX0 = 55, PlainY0 = -190, PlainX1 = 105, PlainY1 = -150;

        /// <summary>Landmark galaxies plus six authored systems, 50 x 40 sectors around the origin (the Home galaxy).</summary>
        public const ulong GoldenAuthored = 0x4962F4E5D4987768UL;

        [Test]
        public void Golden_PureGenerator_Over2000Sectors()
        {
            Universe u = TestUniverses.Plain();
            int systems = 0;
            for (int y = PlainY0; y < PlainY1; y++)
                for (int x = PlainX0; x < PlainX1; x++) systems += u.SystemsInSector(new SectorCoord(x, y)).Length;
            ulong digest = UniverseDigest.OfRegion(u, PlainX0, PlainY0, PlainX1, PlainY1);
            TestLog.Info("golden (pure generator): " + (PlainX1 - PlainX0) * (PlainY1 - PlainY0) + " sectors, " + systems + " systems, digest = 0x" + digest.ToString("X16"));
            Assert.AreEqual(2000, (PlainX1 - PlainX0) * (PlainY1 - PlainY0));
            Assert.Greater(systems, 200, "the golden region must contain a real galaxy");
            Assert.AreEqual(GoldenPlain, digest, "GENERATOR V1 OUTPUT CHANGED. If this is intentional it needs a new GeneratorVersion.");
        }

        [Test]
        public void Golden_WithLandmarksAndAuthoredSystems_Over2000Sectors()
        {
            Universe u = TestUniverses.WithFakeAuthored();
            int systems = 0, planets = 0;
            for (int y = Y0; y < Y1; y++)
                for (int x = X0; x < X1; x++)
                    foreach (StarSystem s in u.SystemsInSector(new SectorCoord(x, y))) { systems++; planets += u.PlanetsOf(s).Length; }
            ulong digest = UniverseDigest.OfRegion(u, X0, Y0, X1, Y1);
            TestLog.Info("golden (landmarks + authored): " + (X1 - X0) * (Y1 - Y0) + " sectors, " + systems + " systems, " + planets + " planets, digest = 0x" + digest.ToString("X16"));
            Assert.AreEqual(GoldenAuthored, digest, "GENERATOR V1 OUTPUT CHANGED. If this is intentional it needs a new GeneratorVersion.");
        }

        [Test]
        public void Golden_StreamSaltNames_AreFrozen()
        {
            var names = new List<string>();
            var seen = new HashSet<string>();
            for (int i = 0; i < (int)Attr.Count; i++)
            {
                string n = StreamSalts.NameOf((Attr)i);
                Assert.IsTrue(seen.Add(n), "duplicate salt name " + n);
                names.Add(n);
            }
            ulong h = Hash.Fnv1a64(string.Join("|", names));
            TestLog.Info("salt names digest 0x" + h.ToString("X16") + " (" + names.Count + " attributes)");
            Assert.AreEqual(GoldenSaltNames, h, "an attribute stream was renamed or removed: that changes the universe");
        }

        public const ulong GoldenSaltNames = 0x4787A4CF60E8072CUL;
    }
}
