using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Frontier.Universe.Tests
{
    [TestFixture]
    public class GalaxyTests
    {
        Universe _home;

        [OneTimeSetUp]
        public void Setup()
        {
            _home = TestUniverses.WithLandmarks();
        }

        [Test]
        public void Density_IsBoundedAndFinite()
        {
            var cfg = _home.Config;
            var r = new Rng(11UL);
            double min = double.MaxValue, max = 0.0, sum = 0.0;
            const int n = 300000;
            for (int i = 0; i < n; i++)
            {
                double x = r.Range(-22000.0, 22000.0), y = r.Range(-22000.0, 22000.0);
                double d = _home.Density(x, y);
                Assert.IsFalse(double.IsNaN(d) || double.IsInfinity(d), "density at " + x + "," + y);
                Assert.GreaterOrEqual(d, cfg.HaloDensity);
                Assert.LessOrEqual(d, cfg.DensityMax);
                min = Math.Min(min, d); max = Math.Max(max, d); sum += d;
            }
            TestLog.Info("density over " + n + " random points in a 44,000 ly square: min " + min.ToString("0.0000") + ", mean " + (sum / n).ToString("0.0000") + ", max " + max.ToString("0.000"));
            Assert.AreEqual(cfg.HaloDensity, min, 1e-12);
        }

        [Test]
        public void Density_IsContinuous()
        {
            var r = new Rng(12UL);
            double worst = 0.0;
            for (int i = 0; i < 120000; i++)
            {
                double x = r.Range(-6000.0, 6000.0), y = r.Range(-6000.0, 6000.0);
                double a = _home.Density(x, y), b = _home.Density(x + 0.5, y);
                double c = _home.Density(x, y + 0.5);
                worst = Math.Max(worst, Math.Max(Math.Abs(a - b), Math.Abs(a - c)));
            }
            TestLog.Info("largest density change over 0.5 ly: " + worst.ToString("0.00000"));
            Assert.Less(worst, 0.03);
        }

        [Test]
        public void Density_HasNoJumpsAcrossGalaxyEdgesAndCellBoundaries()
        {
            var cfg = _home.Config;
            double worst = 0.0;
            // march along lines that leave the Home galaxy and cross several macro-cell boundaries
            for (int line = 0; line < 12; line++)
            {
                double ang = line * 0.5236 + 0.1;
                double prev = _home.Density(0.0, 0.0);
                for (double t = 0.25; t < 9000.0; t += 0.25)
                {
                    double d = _home.Density(Math.Cos(ang) * t, Math.Sin(ang) * t);
                    worst = Math.Max(worst, Math.Abs(d - prev));
                    prev = d;
                }
            }
            TestLog.Info("largest step between samples 0.25 ly apart along 12 radial lines of 9,000 ly: " + worst.ToString("0.00000"));
            Assert.Less(worst, 0.02);
        }

        [Test]
        public void IntergalacticSpace_IsTheHaloDensity()
        {
            var cfg = _home.Config;
            var r = new Rng(13UL);
            int halo = 0, total = 0;
            for (int i = 0; i < 40000; i++)
            {
                double x = r.Range(-25000.0, 25000.0), y = r.Range(-25000.0, 25000.0);
                DensitySample s;
                _home.SampleDensity(x, y, out s);
                total++;
                if (s.Galaxy == null)
                {
                    halo++;
                    Assert.AreEqual(cfg.HaloDensity, s.Density, 1e-12);
                    Assert.IsTrue(s.GalaxyDensity <= cfg.HaloDensity);
                }
                else
                {
                    Assert.GreaterOrEqual(s.GalaxyDensity, cfg.HaloDensity);
                }
            }
            double frac = (double)halo / total;
            TestLog.Info("share of space that is intergalactic halo: " + (frac * 100).ToString("0.0") + " %");
            Assert.Greater(frac, 0.80);
        }

        [Test]
        public void HomeGalaxy_IsDenseAtTheOriginAndReadsAsASpiral()
        {
            var home = _home.GalaxyAt(0.0, 0.0);
            Assert.IsNotNull(home);
            Assert.AreEqual("The Verdant Spiral", home.Name);
            Assert.IsTrue(home.Authored);
            double d0 = _home.Density(0.0, 0.0);
            TestLog.Info("density at the origin: " + d0.ToString("0.000") + " (max " + _home.Config.DensityMax + ")");
            Assert.Greater(d0, 0.9);

            // along a circle at 40 % of the radius the arms must show: strong contrast between arm and inter-arm density
            double rr = 0.4 * home.Radius, lo = double.MaxValue, hi = 0.0;
            for (int i = 0; i < 720; i++)
            {
                double a = i * DetMath.TwoPi / 720.0;
                double u = Math.Cos(a) * rr, v = Math.Sin(a) * rr * home.AxisRatio, x, y;
                home.FromLocal(u, v, out x, out y);
                double d = _home.Density(x, y);
                lo = Math.Min(lo, d); hi = Math.Max(hi, d);
            }
            TestLog.Info("density around the 0.4R circle: min " + lo.ToString("0.000") + ", max " + hi.ToString("0.000") + " (arm contrast " + (hi / lo).ToString("0.0") + "x)");
            Assert.Greater(hi / lo, 4.0);
            Assert.Greater(d0, hi);
        }

        [Test]
        public void GeneratedGalaxies_RespectTheConfiguredRanges()
        {
            var cfg = _home.Config;
            var ids = new HashSet<ulong>();
            int count = 0, cells = 0, elliptical = 0, barred = 0;
            for (int my = -30; my <= 30; my++)
            {
                for (int mx = -30; mx <= 30; mx++)
                {
                    cells++;
                    GalaxyInfo g = _home.Galaxies.GetMacroGalaxy(mx, my);
                    if (g == null) continue;
                    count++;
                    Assert.IsTrue(ids.Add(g.Id), "duplicate galaxy id");
                    Assert.IsFalse(g.Authored);
                    Assert.GreaterOrEqual(g.Radius, 0.75 * cfg.GalaxyRadiusMin - 1e-9);
                    Assert.LessOrEqual(g.Radius, cfg.GalaxyRadiusMax + 1e-9);
                    if (g.Kind == GalaxyKind.Elliptical) { elliptical++; Assert.AreEqual(0, g.Arms); }
                    else { Assert.GreaterOrEqual(g.Arms, cfg.ArmsMin); Assert.LessOrEqual(g.Arms, cfg.ArmsMax); }
                    if (g.Kind == GalaxyKind.BarredSpiral) barred++;
                    Assert.GreaterOrEqual(g.Ellipticity, 0.0);
                    Assert.Less(g.Ellipticity, 0.6);
                    Assert.IsFalse(string.IsNullOrEmpty(g.Name));
                    Assert.AreEqual(2, g.CataloguePrefix.Length);
                    Assert.AreEqual(mx, g.MacroX);
                    Assert.AreEqual(my, g.MacroY);
                    // centre stays inside its cell's jitter window
                    double cx = g.CenterX / cfg.MacroCellSize - mx, cy = g.CenterY / cfg.MacroCellSize - my;
                    Assert.GreaterOrEqual(cx, cfg.GalaxyJitterMin - 1e-9); Assert.LessOrEqual(cx, cfg.GalaxyJitterMax + 1e-9);
                    Assert.GreaterOrEqual(cy, cfg.GalaxyJitterMin - 1e-9); Assert.LessOrEqual(cy, cfg.GalaxyJitterMax + 1e-9);
                    GalaxyInfo byId;
                    Assert.IsTrue(_home.Galaxies.TryGetGalaxyById(g.Id, out byId));
                    Assert.AreEqual(g.Id, byId.Id);
                }
            }
            double share = (double)count / cells;
            TestLog.Info(count + " galaxies in " + cells + " macro cells (" + (share * 100).ToString("0.0") + " %), " + elliptical + " elliptical, " + barred + " barred spirals");
            Assert.Greater(share, 0.40);
            Assert.Less(share, 0.62);
            Assert.Greater(elliptical, 0);
            Assert.Greater(barred, 0);
        }

        [Test]
        public void NeighbouringGalaxies_DoNotOverlapMuch()
        {
            var list = new List<GalaxyInfo>();
            _home.Galaxies.GalaxiesInRect(-60000, -60000, 60000, 60000, list);
            int pairs = 0;
            for (int i = 0; i < list.Count; i++)
            {
                for (int j = i + 1; j < list.Count; j++)
                {
                    double dx = list[i].CenterX - list[j].CenterX, dy = list[i].CenterY - list[j].CenterY;
                    double d = Math.Sqrt(dx * dx + dy * dy);
                    pairs++;
                    Assert.LessOrEqual(list[i].Radius + list[j].Radius, 1.15 * d + 1e-6, list[i].Name + " vs " + list[j].Name);
                }
            }
            TestLog.Info("checked " + pairs + " pairs among " + list.Count + " galaxies");
        }

        [Test]
        public void GalaxyFrame_Helpers_AreConsistent()
        {
            var g = _home.GalaxyAt(0.0, 0.0);
            Assert.AreEqual(0.0, g.NormalizedRadius(0.0, 0.0), 1e-12);
            double x, y;
            g.FromLocal(g.Radius, 0.0, out x, out y);
            Assert.AreEqual(1.0, g.NormalizedRadius(x, y), 1e-9);
            g.FromLocal(0.0, g.Radius * g.AxisRatio, out x, out y);
            Assert.AreEqual(1.0, g.NormalizedRadius(x, y), 1e-9);
            double u, v;
            g.ToLocal(x, y, out u, out v);
            double x2, y2;
            g.FromLocal(u, v, out x2, out y2);
            Assert.AreEqual(x, x2, 1e-9);
            Assert.AreEqual(y, y2, 1e-9);
            Assert.IsTrue(g.Contains(10.0, 10.0));
            Assert.IsFalse(g.Contains(g.Radius * 2.0, 0.0));
            double ang = g.AngleOf(g.CenterX + Math.Cos(g.Rotation) * 100.0, g.CenterY + Math.Sin(g.Rotation) * 100.0);
            Assert.AreEqual(0.0, ang, 1e-9);
        }

        [Test]
        public void Galaxies_AreDeterministicAcrossInstances()
        {
            var a = new GalaxyField(new UniverseConfig(), null);
            var b = new GalaxyField(new UniverseConfig(), null);
            int compared = 0;
            for (int my = -15; my <= 15; my++)
            {
                for (int mx = -15; mx <= 15; mx++)
                {
                    GalaxyInfo ga = a.GetMacroGalaxy(mx, my), gb = b.GetMacroGalaxy(mx, my);
                    Assert.AreEqual(ga == null, gb == null);
                    if (ga == null) continue;
                    compared++;
                    Assert.AreEqual(ga.Id, gb.Id);
                    Assert.AreEqual(ga.Name, gb.Name);
                    Assert.AreEqual(ga.CenterX, gb.CenterX);
                    Assert.AreEqual(ga.CenterY, gb.CenterY);
                    Assert.AreEqual(ga.Radius, gb.Radius);
                    Assert.AreEqual(ga.Arms, gb.Arms);
                    Assert.AreEqual(ga.Winding, gb.Winding);
                    Assert.AreEqual(ga.Ellipticity, gb.Ellipticity);
                    Assert.AreEqual(ga.DensityAt(ga.CenterX + 100.0, ga.CenterY - 50.0), gb.DensityAt(gb.CenterX + 100.0, gb.CenterY - 50.0));
                }
            }
            Assert.Greater(compared, 100);
        }

        [Test]
        public void GalaxiesNear_FindsGalaxiesInRangeOnly()
        {
            var list = new List<GalaxyInfo>();
            int n = _home.Galaxies.GalaxiesNear(0.0, 0.0, 10.0, list);
            Assert.GreaterOrEqual(n, 1);
            bool foundHome = false;
            foreach (var g in list) if (g.Name == "The Verdant Spiral") foundHome = true;
            Assert.IsTrue(foundHome);
            list.Clear();
            _home.Galaxies.GalaxiesNear(0.0, 0.0, 30000.0, list);
            Assert.Greater(list.Count, 10);
            double dist;
            var nearest = _home.Galaxies.NearestGalaxy(5.0, 5.0, out dist);
            Assert.AreEqual("The Verdant Spiral", nearest.Name);
            Assert.Less(dist, 10.0);
        }
    }
}
