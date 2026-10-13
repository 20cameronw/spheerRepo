using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Frontier.Universe.Tests
{
    [TestFixture]
    public class StreamIndependenceTests
    {
        const int X0 = -15, Y0 = -12, X1 = 15, Y1 = 12;

        static Universe WithOverride(string salt, string replacement)
        {
            var cfg = new UniverseConfig();
            cfg.SaltOverrides = new Dictionary<string, string>();
            cfg.SaltOverrides[salt] = replacement;
            return new Universe(cfg, Landmarks.CreateDefault());
        }

        sealed class Case
        {
            public string Salt;
            public PlanetFields Fields;
            public double MinChanged;
            public Case(string salt, PlanetFields fields, double minChanged) { Salt = salt; Fields = fields; MinChanged = minChanged; }
        }

        [Test]
        public void ReSaltingOneAttribute_ChangesOnlyThatAttribute()
        {
            Universe baseline = TestUniverses.WithLandmarks();
            var cases = new[]
            {
                new Case("planet.name", PlanetFields.Name, 0.99),
                new Case("planet.weather", PlanetFields.Weather, 0.15),
                new Case("planet.rings", PlanetFields.HasRings, 0.02),
                new Case("planet.moons", PlanetFields.MoonCount, 0.30),
                new Case("planet.orbit-phase", PlanetFields.OrbitPhase, 0.99),
                new Case("planet.visual", PlanetFields.Visual, 0.90),
                new Case("planet.model", PlanetFields.ModelKey, 0.20),
                new Case("planet.danger", PlanetFields.Danger, 0.99)
            };
            foreach (Case c in cases)
            {
                Universe alt = WithOverride(c.Salt, c.Salt + "#reroll");
                int planets = 0, changed = 0, onlyTarget = 0;
                PlanetFields everythingElse = PlanetFields.All & ~c.Fields;
                for (int y = Y0; y < Y1; y++)
                {
                    for (int x = X0; x < X1; x++)
                    {
                        var sc = new SectorCoord(x, y);
                        StarSystem[] sa = baseline.SystemsInSector(sc), sb = alt.SystemsInSector(sc);
                        Assert.AreEqual(sa.Length, sb.Length);
                        for (int i = 0; i < sa.Length; i++)
                        {
                            Assert.AreEqual(UniverseDigest.OfSystem(sa[i]), UniverseDigest.OfSystem(sb[i]), "re-salting " + c.Salt + " changed system " + sa[i].Id);
                            PlanetDescriptor[] pa = baseline.PlanetsOf(sa[i]), pb = alt.PlanetsOf(sb[i]);
                            Assert.AreEqual(pa.Length, pb.Length);
                            for (int p = 0; p < pa.Length; p++)
                            {
                                planets++;
                                Assert.AreEqual(UniverseDigest.OfPlanet(pa[p], c.Fields), UniverseDigest.OfPlanet(pb[p], c.Fields),
                                    "re-salting " + c.Salt + " changed something other than " + c.Fields + " on " + pa[p].Id);
                                if (UniverseDigest.OfPlanet(pa[p]) != UniverseDigest.OfPlanet(pb[p]))
                                {
                                    changed++;
                                    if (UniverseDigest.OfPlanet(pa[p], everythingElse) != UniverseDigest.OfPlanet(pb[p], everythingElse)) onlyTarget++;
                                }
                            }
                        }
                    }
                }
                double frac = (double)changed / planets;
                TestLog.Info(c.Salt.PadRight(20) + " re-salted: " + changed + " of " + planets + " planets changed (" + (frac * 100).ToString("0.0") + " %), every other field identical");
                Assert.GreaterOrEqual(frac, c.MinChanged, c.Salt + " should actually change its own field");
                Assert.AreEqual(changed, onlyTarget, "all differences must be inside " + c.Fields);
            }
        }

        [Test]
        public void ReSaltingSystemName_ChangesOnlySystemNames()
        {
            Universe baseline = TestUniverses.WithLandmarks();
            Universe alt = WithOverride("system.name", "system.name#reroll");
            int systems = 0, renamed = 0;
            for (int y = Y0; y < Y1; y++)
            {
                for (int x = X0; x < X1; x++)
                {
                    var sc = new SectorCoord(x, y);
                    StarSystem[] sa = baseline.SystemsInSector(sc), sb = alt.SystemsInSector(sc);
                    Assert.AreEqual(sa.Length, sb.Length);
                    for (int i = 0; i < sa.Length; i++)
                    {
                        systems++;
                        string diff = Exact.Diff(sa[i], sb[i]);
                        Assert.IsTrue(diff == null || diff == "Name", "unexpected difference in " + diff);
                        if (diff == "Name") renamed++;
                        PlanetDescriptor[] pa = baseline.PlanetsOf(sa[i]), pb = alt.PlanetsOf(sb[i]);
                        for (int p = 0; p < pa.Length; p++) Assert.IsNull(Exact.Diff(pa[p], pb[p]), "planets must not notice a renamed star");
                    }
                }
            }
            TestLog.Info("system.name re-salted: " + renamed + " of " + systems + " star names changed; positions, classes and every planet identical");
            Assert.Greater(renamed, systems * 99 / 100);
        }

        [Test]
        public void ReSaltingCatalogue_ChangesOnlyDesignations()
        {
            Universe baseline = TestUniverses.WithLandmarks();
            Universe alt = WithOverride("system.catalog", "system.catalog#reroll");
            for (int y = Y0; y < Y1; y++)
            {
                for (int x = X0; x < X1; x++)
                {
                    var sc = new SectorCoord(x, y);
                    StarSystem[] sa = baseline.SystemsInSector(sc), sb = alt.SystemsInSector(sc);
                    for (int i = 0; i < sa.Length; i++)
                    {
                        string diff = Exact.Diff(sa[i], sb[i]);
                        Assert.IsTrue(diff == null || diff == "Designation", "unexpected difference in " + diff);
                        PlanetDescriptor[] pa = baseline.PlanetsOf(sa[i]), pb = alt.PlanetsOf(sb[i]);
                        for (int p = 0; p < pa.Length; p++)
                            Assert.AreEqual(UniverseDigest.OfPlanet(pa[p], PlanetFields.Designation), UniverseDigest.OfPlanet(pb[p], PlanetFields.Designation));
                    }
                }
            }
        }

        [Test]
        public void ReSaltingGalaxyName_DoesNotMoveGalaxies()
        {
            Universe baseline = TestUniverses.WithLandmarks();
            Universe alt = WithOverride("galaxy.name", "galaxy.name#reroll");
            int renamed = 0, compared = 0;
            for (int my = -10; my <= 10; my++)
            {
                for (int mx = -10; mx <= 10; mx++)
                {
                    GalaxyInfo a = baseline.Galaxies.GetMacroGalaxy(mx, my), b = alt.Galaxies.GetMacroGalaxy(mx, my);
                    Assert.AreEqual(a == null, b == null);
                    if (a == null) continue;
                    compared++;
                    Assert.AreEqual(a.Id, b.Id);
                    Assert.AreEqual(a.CenterX, b.CenterX);
                    Assert.AreEqual(a.Radius, b.Radius);
                    Assert.AreEqual(a.Arms, b.Arms);
                    Assert.AreEqual(a.Winding, b.Winding);
                    Assert.AreEqual(a.Kind, b.Kind);
                    Assert.AreEqual(baseline.Density(a.CenterX + 300, a.CenterY), alt.Density(a.CenterX + 300, a.CenterY));
                    if (a.Name != b.Name) renamed++;
                }
            }
            Assert.Greater(renamed, compared * 3 / 4);
        }

        [Test]
        public void UnknownOrIdenticalOverride_ChangesNothing()
        {
            ulong baseline = UniverseDigest.OfRegion(TestUniverses.WithLandmarks(), -10, -10, 10, 10);
            Assert.AreEqual(baseline, UniverseDigest.OfRegion(WithOverride("no.such.attribute", "anything"), -10, -10, 10, 10));
            Assert.AreEqual(baseline, UniverseDigest.OfRegion(WithOverride("planet.name", "planet.name"), -10, -10, 10, 10));
            Assert.AreNotEqual(baseline, UniverseDigest.OfRegion(WithOverride("planet.name", "planet.name2"), -10, -10, 10, 10));
        }

        [Test]
        public void AttributeSeeds_DependOnlyOnTheirOwnName()
        {
            var salts = new StreamSalts(42UL, null);
            for (int i = 0; i < (int)Attr.Count; i++)
            {
                Attr a = (Attr)i;
                Assert.AreEqual(Hash.SeedFor(42UL, Hash.Fnv1a64(StreamSalts.NameOf(a))), salts.SeedOf(a));
            }
            var overridden = new StreamSalts(42UL, new Dictionary<string, string> { { "planet.biome", "x" } });
            for (int i = 0; i < (int)Attr.Count; i++)
            {
                Attr a = (Attr)i;
                if (a == Attr.PlanetBiome) Assert.AreNotEqual(salts.SeedOf(a), overridden.SeedOf(a));
                else Assert.AreEqual(salts.SeedOf(a), overridden.SeedOf(a), "overriding one salt must not touch " + a);
            }
        }
    }
}
