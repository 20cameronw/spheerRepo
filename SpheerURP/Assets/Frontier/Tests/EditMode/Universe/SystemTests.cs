using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Frontier.Universe.Tests
{
    /// <summary>A large generated sample shared by the statistical tests.</summary>
    public sealed class Dataset
    {
        public Universe Universe;
        public List<StarSystem> Systems = new List<StarSystem>();
        public List<PlanetDescriptor> Planets = new List<PlanetDescriptor>();
        public Dictionary<ulong, StarSystem> SystemOfPlanet = new Dictionary<ulong, StarSystem>();
        public int Sectors;

        static Dataset _shared;

        public static Dataset Shared
        {
            get
            {
                if (_shared == null) _shared = Build(TestUniverses.WithLandmarks(), -100, -100, 100, 100);
                return _shared;
            }
        }

        public static Dataset Build(Universe u, int sx0, int sy0, int sx1, int sy1)
        {
            var d = new Dataset { Universe = u };
            for (int sy = sy0; sy < sy1; sy++)
            {
                for (int sx = sx0; sx < sx1; sx++)
                {
                    d.Sectors++;
                    StarSystem[] sys = u.SystemsInSector(new SectorCoord(sx, sy));
                    for (int i = 0; i < sys.Length; i++)
                    {
                        d.Systems.Add(sys[i]);
                        PlanetDescriptor[] ps = u.PlanetsOf(sys[i]);
                        for (int p = 0; p < ps.Length; p++)
                        {
                            d.Planets.Add(ps[p]);
                            d.SystemOfPlanet[ps[p].Key.Id] = sys[i];
                        }
                    }
                }
            }
            return d;
        }
    }

    [TestFixture]
    public class SystemTests
    {
        Dataset _d;

        [OneTimeSetUp]
        public void Setup()
        {
            _d = Dataset.Shared;
            TestLog.Info("dataset: " + _d.Sectors + " sectors, " + _d.Systems.Count + " systems, " + _d.Planets.Count + " planets");
        }

        [Test]
        public void SystemKeys_AreValidUniqueAndInsideTheirSector()
        {
            var cfg = _d.Universe.Config;
            var keys = new HashSet<SystemKey>();
            foreach (StarSystem s in _d.Systems)
            {
                Assert.IsTrue(s.Key.IsValid);
                Assert.IsTrue(keys.Add(s.Key), "duplicate " + s.Key);
                Assert.Less(s.Key.Index, SystemKey.AuthoredBase);
                Assert.AreEqual(s.Key.Sector, SectorCoord.FromLy(s.X, s.Y, cfg.SectorSize));
                Assert.AreEqual(s.Key.ToString(), s.Id);
                Assert.IsFalse(string.IsNullOrEmpty(s.Name));
                Assert.IsTrue(char.IsUpper(s.Name[0]), s.Name);
                Assert.IsTrue(s.Designation.Length == 10 && s.Designation[2] == '-', s.Designation);
                Assert.GreaterOrEqual(s.PlanetCount, cfg.MinPlanetsPerSystem);
                Assert.LessOrEqual(s.PlanetCount, cfg.MaxPlanetsPerSystem);
                Assert.Greater(s.Luminosity, 0f);
                Assert.Less(s.HabitableInner, s.HabitableOuter);
            }
        }

        [Test]
        public void SystemCountPerSector_FollowsTheDensityField()
        {
            var u = _d.Universe;
            var cfg = u.Config;
            double expected = 0.0;
            long actual = 0;
            int maxPerSector = 0;
            for (int sy = -60; sy < 60; sy++)
            {
                for (int sx = -60; sx < 60; sx++)
                {
                    var sc = new SectorCoord(sx, sy);
                    expected += u.Density(sc.CenterX(cfg.SectorSize), sc.CenterY(cfg.SectorSize)) * cfg.SystemsPerSectorMax;
                    int n = u.SystemsInSector(sc).Length;
                    actual += n;
                    maxPerSector = Math.Max(maxPerSector, n);
                }
            }
            double err = Math.Abs(actual - expected) / expected;
            TestLog.Info("systems in 120x120 sectors: expected " + expected.ToString("0") + " from the density field, generated " + actual + " (" + (err * 100).ToString("0.0") + " % off), max per sector " + maxPerSector);
            Assert.Less(err, 0.04);
            Assert.LessOrEqual(maxPerSector, cfg.MaxSystemsPerSector);
        }

        [Test]
        public void RandomSystems_KeepTheirMinimumSeparationAcrossSectorBorders()
        {
            var u = _d.Universe;
            double sep = u.Config.MinSystemSeparation;
            double worst = double.MaxValue;
            long pairs = 0;
            for (int sy = -20; sy < 20; sy++)
            {
                for (int sx = -20; sx < 20; sx++)
                {
                    StarSystem[] here = u.SystemsInSector(new SectorCoord(sx, sy));
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            StarSystem[] there = u.SystemsInSector(new SectorCoord(sx + dx, sy + dy));
                            for (int i = 0; i < here.Length; i++)
                            {
                                for (int j = 0; j < there.Length; j++)
                                {
                                    if (ReferenceEquals(here[i], there[j])) continue;
                                    double ddx = here[i].X - there[j].X, ddy = here[i].Y - there[j].Y;
                                    double d = Math.Sqrt(ddx * ddx + ddy * ddy);
                                    pairs++;
                                    worst = Math.Min(worst, d);
                                }
                            }
                        }
                    }
                }
            }
            TestLog.Info("closest pair among " + pairs + " neighbouring pairs: " + worst.ToString("0.000") + " ly (minimum " + sep + ")");
            Assert.GreaterOrEqual(worst, sep - 1e-9);
        }

        [Test]
        public void StarClasses_FollowTheGalacticRegion()
        {
            var u = _d.Universe;
            int armTotal = 0, armHot = 0, armM = 0, bulgeTotal = 0, bulgeHot = 0, bulgeM = 0, haloTotal = 0, haloHot = 0, haloM = 0, haloOB = 0;
            foreach (StarSystem s in _d.Systems)
            {
                DensitySample ds;
                u.SampleDensity(s.X, s.Y, out ds);
                bool hot = s.StarClass == StarClass.O || s.StarClass == StarClass.B || s.StarClass == StarClass.A;
                bool m = s.StarClass == StarClass.M;
                if (ds.Galaxy == null) { haloTotal++; if (hot) haloHot++; if (m) haloM++; if (s.StarClass == StarClass.O || s.StarClass == StarClass.B) haloOB++; }
                else if (ds.BulgeShare > 0.7) { bulgeTotal++; if (hot) bulgeHot++; if (m) bulgeM++; }
                else if (ds.ArmStrength > 0.4) { armTotal++; if (hot) armHot++; if (m) armM++; }
            }
            double armHotF = (double)armHot / armTotal, bulgeHotF = (double)bulgeHot / bulgeTotal;
            double armMF = (double)armM / armTotal, bulgeMF = (double)bulgeM / bulgeTotal;
            TestLog.Info("hot (O/B/A) share: arms " + (armHotF * 100).ToString("0.0") + " % (n=" + armTotal + "), bulge " + (bulgeHotF * 100).ToString("0.0") + " % (n=" + bulgeTotal + "), halo " + haloHot + "/" + haloTotal);
            TestLog.Info("M-dwarf share: arms " + (armMF * 100).ToString("0.0") + " %, bulge " + (bulgeMF * 100).ToString("0.0") + " %, halo " + (100.0 * haloM / haloTotal).ToString("0.0") + " %");
            Assert.Greater(armTotal, 200);
            Assert.Greater(bulgeTotal, 100);
            Assert.Greater(armHotF, bulgeHotF * 2.0, "arms must be bluer than the bulge");
            Assert.Greater(bulgeMF, armMF, "the bulge must be redder than the arms");
            Assert.AreEqual(0, haloOB, "intergalactic outposts are old stars: no O or B");
            Assert.Less(haloHot, haloTotal * 0.005 + 1, "A stars are very rare in the halo");
            Assert.Greater((double)haloM / haloTotal, 0.4, "the halo is mostly red dwarfs");
        }

        [Test]
        public void BlackHolesAndNeutronStars_AreRareAndMostlyNearTheCore()
        {
            var u = _d.Universe;
            int remnants = 0, total = _d.Systems.Count;
            var radii = new List<double>();
            var allRadii = new List<double>();
            foreach (StarSystem s in _d.Systems)
            {
                DensitySample ds;
                u.SampleDensity(s.X, s.Y, out ds);
                if (ds.Galaxy != null) allRadii.Add(ds.RadiusNorm);
                if (s.StarClass == StarClass.Neutron || s.StarClass == StarClass.BlackHole)
                {
                    remnants++;
                    Assert.GreaterOrEqual(s.Tier, u.Config.RemnantMinTier, "remnants keep away from the friendly start area");
                    if (ds.Galaxy != null) radii.Add(ds.RadiusNorm);
                }
            }
            radii.Sort();
            allRadii.Sort();
            double medianRemnant = radii.Count > 0 ? radii[radii.Count / 2] : 1.0;
            double medianAll = allRadii[allRadii.Count / 2];
            TestLog.Info(remnants + " neutron stars / black holes among " + total + " systems (" + (100.0 * remnants / total).ToString("0.00") + " %); median galactic radius " + medianRemnant.ToString("0.00") + " vs " + medianAll.ToString("0.00") + " for all systems");
            Assert.GreaterOrEqual(remnants, 8, "need a meaningful sample");
            Assert.Less(100.0 * remnants / total, 2.5);
            Assert.Less(medianRemnant, medianAll * 0.75, "remnants cluster near the galactic core");
        }

        [Test]
        public void StarClassMix_IsBelievable()
        {
            var counts = new int[9];
            foreach (StarSystem s in _d.Systems) counts[(int)s.StarClass]++;
            double n = _d.Systems.Count;
            TestLog.Info("star classes: O " + counts[0] + ", B " + counts[1] + ", A " + counts[2] + ", F " + counts[3] + ", G " + counts[4] + ", K " + counts[5] + ", M " + counts[6] + ", neutron " + counts[7] + ", black hole " + counts[8]);
            Assert.Greater(counts[6] / n, 0.2);
            Assert.Greater(counts[5] / n, 0.15);
            Assert.Greater(counts[4] / n, 0.10);
            Assert.Less(counts[0] / n, 0.02);
            Assert.Greater(counts[0] + counts[1], 0);
        }

        [Test]
        public void Systems_AreAssignedToGalaxiesConsistently()
        {
            var u = _d.Universe;
            int inGalaxy = 0, halo = 0;
            foreach (StarSystem s in _d.Systems)
            {
                GalaxyInfo g = u.GalaxyOf(s);
                GalaxyInfo at = u.GalaxyAt(s.X, s.Y);
                if (s.GalaxyId == 0UL) { Assert.IsNull(g); Assert.IsNull(at); halo++; }
                else { Assert.IsNotNull(g); Assert.AreEqual(g.Id, at.Id); inGalaxy++; }
            }
            TestLog.Info(inGalaxy + " systems inside galaxies, " + halo + " deep outposts in the halo");
            Assert.Greater(halo, 0, "rare deep outposts must exist");
            Assert.Greater(inGalaxy, 1000);
        }

        [Test]
        public void DeepOutposts_AreRareComparedToGalaxies()
        {
            var u = _d.Universe;
            var cfg = u.Config;
            long haloSectors = 0, haloSystems = 0, galaxySectors = 0, galaxySystems = 0;
            for (int sy = -100; sy < 100; sy++)
            {
                for (int sx = -100; sx < 100; sx++)
                {
                    var sc = new SectorCoord(sx, sy);
                    double d = u.Density(sc.CenterX(cfg.SectorSize), sc.CenterY(cfg.SectorSize));
                    int n = u.SystemsInSector(sc).Length;
                    if (d <= cfg.HaloDensity + 1e-12) { haloSectors++; haloSystems += n; }
                    else if (d >= 0.25) { galaxySectors++; galaxySystems += n; }
                }
            }
            double rateHalo = (double)haloSystems / haloSectors, rateGalaxy = (double)galaxySystems / galaxySectors;
            TestLog.Info("systems per sector: intergalactic " + rateHalo.ToString("0.000") + " (about one deep outpost per " + (1.0 / rateHalo).ToString("0") + " sectors), inside galaxies (density >= 0.25) " + rateGalaxy.ToString("0.00"));
            Assert.AreEqual(cfg.HaloDensity * cfg.SystemsPerSectorMax, rateHalo, 0.012);
            Assert.Greater(rateGalaxy, 8.0 * rateHalo);
        }

        [Test]
        public void Designations_AreMostlyUniqueAndPrefixedByGalaxy()
        {
            var seen = new HashSet<string>();
            int dup = 0;
            foreach (StarSystem s in _d.Systems) if (!seen.Add(s.Designation)) dup++;
            Assert.Less(dup, 3, "catalogue designations collide only by chance");
            var u = _d.Universe;
            foreach (StarSystem s in _d.Systems)
            {
                GalaxyInfo g = u.GalaxyOf(s);
                Assert.AreEqual(g == null ? "IG" : g.CataloguePrefix, s.Designation.Substring(0, 2));
            }
        }
    }
}
