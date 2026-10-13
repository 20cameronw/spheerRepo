using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Frontier.Universe.Tests
{
    [TestFixture]
    public class AuthoredMergeTests
    {
        static StarSystem[] RandomOnly(StarSystem[] all)
        {
            var list = new List<StarSystem>();
            foreach (StarSystem s in all) if (!s.IsAuthored) list.Add(s);
            return list.ToArray();
        }

        static double Dist(StarSystem a, StarSystem b)
        {
            double dx = a.X - b.X, dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        [Test]
        public void SixAuthoredSystems_AppearInTheRightSectorsWithTheirPlanets()
        {
            var cfg = new UniverseConfig();
            AuthoredContent content = FakeAuthored.Create(cfg);
            var u = new Universe(cfg, content);
            Assert.AreEqual(6, u.AuthoredSystems.Count);
            for (int i = 0; i < 6; i++)
            {
                double x = FakeAuthored.Positions[i][0], y = FakeAuthored.Positions[i][1];
                SectorCoord sc = u.SectorOf(x, y);
                StarSystem[] inSector = u.SystemsInSector(sc);
                StarSystem found = null;
                foreach (StarSystem s in inSector) if (s.IsAuthored && s.Key.Index == SystemKey.AuthoredBase + i) found = s;
                Assert.IsNotNull(found, "authored system " + i + " missing from sector " + sc);
                Assert.AreEqual(x, found.X);
                Assert.AreEqual(y, found.Y);
                Assert.AreEqual(sc, found.Key.Sector);
                Assert.AreSame(found, u.GetSystem(found.Key));
                Assert.AreSame(found, u.AuthoredSystems[i]);

                PlanetDescriptor[] planets = u.PlanetsOf(found);
                Assert.AreSame(content.Systems[i].Planets, planets, "the host's planets are returned as supplied");
                Assert.AreEqual(found.PlanetCount, planets.Length);
                for (int p = 0; p < planets.Length; p++)
                {
                    Assert.AreSame(planets[p], u.GetPlanet(new PlanetKey(found.Key, p)));
                    PlanetDescriptor byId;
                    Assert.IsTrue(u.TryGetPlanetById(planets[p].Id, out byId));
                    Assert.AreSame(planets[p], byId);
                    Assert.IsTrue(u.TryGetPlanetById(planets[p].AuthoredId, out byId), "legacy alias " + planets[p].AuthoredId);
                    Assert.AreSame(planets[p], byId);
                }
                // authored systems come after the generated ones, sorted by index
                for (int k = 0; k < inSector.Length - 1; k++) Assert.Less(inSector[k].Key.Index, inSector[k + 1].Key.Index);
                Assert.IsTrue(inSector[inSector.Length - 1].IsAuthored);
            }
            Assert.IsTrue(u.AuthoredSystems[3].Key.Sector == new SectorCoord(0, 1), "the system at (63.99, 64.0) belongs to sector (0,1)");
        }

        [Test]
        public void AuthoredSystems_GetAutomaticTierAndGalaxy()
        {
            var u = TestUniverses.WithFakeAuthored();
            GalaxyInfo home = u.GalaxyAt(0.0, 0.0);
            foreach (StarSystem s in u.AuthoredSystems)
            {
                Assert.AreEqual(u.TierAt(s.X, s.Y), s.Tier);
                GalaxyInfo g = u.GalaxyAt(s.X, s.Y);
                Assert.AreEqual(g == null ? 0UL : g.Id, s.GalaxyId);
            }
            Assert.AreEqual(home.Id, u.AuthoredSystems[0].GalaxyId);
            Assert.AreEqual(0, u.AuthoredSystems[0].Tier);
        }

        [Test]
        public void RandomSystems_NeverOverlapAuthoredOnes()
        {
            var u = TestUniverses.WithFakeAuthored();
            double sep = u.Config.MinSystemSeparation;
            double worst = double.MaxValue;
            int checkedPairs = 0;
            foreach (StarSystem a in u.AuthoredSystems)
            {
                for (int dy = -2; dy <= 2; dy++)
                {
                    for (int dx = -2; dx <= 2; dx++)
                    {
                        foreach (StarSystem r in RandomOnly(u.SystemsInSector(a.Key.Sector.Offset(dx, dy))))
                        {
                            double d = Dist(a, r);
                            worst = Math.Min(worst, d);
                            checkedPairs++;
                            Assert.GreaterOrEqual(d, sep - 1e-9, a.Name + " vs " + r.Id);
                        }
                    }
                }
            }
            TestLog.Info("closest random system to any authored system: " + worst.ToString("0.00") + " ly over " + checkedPairs + " neighbours (minimum " + sep + ")");
            Assert.Greater(checkedPairs, 6);
        }

        [Test]
        public void AuthoredSystemOnTopOfARandomOne_DisplacesIt_AlsoAcrossSectorBorders()
        {
            var cfg = new UniverseConfig();
            Universe plain = new Universe(cfg, Landmarks.CreateDefault());
            double sep = cfg.MinSystemSeparation;

            // 1) a random system in the dense core, authored system 2 ly away in the same sector
            StarSystem victim = null;
            for (int sy = -3; sy < 3 && victim == null; sy++)
                for (int sx = -3; sx < 3 && victim == null; sx++)
                    foreach (StarSystem s in plain.SystemsInSector(new SectorCoord(sx, sy)))
                    {
                        double rx = s.X - sx * 64.0, ry = s.Y - sy * 64.0;
                        if (rx < 40 && rx > 10 && ry > 10 && ry < 50) { victim = s; break; }
                    }
            Assert.IsNotNull(victim, "need a random system to collide with");

            // 2) a random system close to a sector border, authored system just across the border
            StarSystem borderVictim = null;
            for (int sy = -6; sy < 6 && borderVictim == null; sy++)
                for (int sx = -6; sx < 6 && borderVictim == null; sx++)
                    foreach (StarSystem s in plain.SystemsInSector(new SectorCoord(sx, sy)))
                    {
                        if (ReferenceEquals(s, victim)) continue;
                        double rx = s.X - sx * 64.0;
                        if (rx > 59.5 && rx < 60.5) { borderVictim = s; break; }
                    }
            Assert.IsNotNull(borderVictim, "need a random system near a sector border");

            var content = Landmarks.CreateDefault();
            double[][] spots =
            {
                new[] { victim.X + 2.0, victim.Y },
                new[] { Math.Floor(borderVictim.X / 64.0 + 1.0) * 64.0 + 1.0, borderVictim.Y }
            };
            for (int i = 0; i < spots.Length; i++)
            {
                var sys = new SystemSpec();
                sys.Key = new SystemKey(SectorCoord.FromLy(spots[i][0], spots[i][1], 64.0), SystemKey.AuthoredBase + i);
                sys.Name = "Intruder " + i;
                sys.Designation = "T-" + i;
                sys.X = spots[i][0];
                sys.Y = spots[i][1];
                sys.StarClass = StarClass.K;
                sys.Luminosity = 0.3f;
                sys.HabitableInner = 0.5f;
                sys.HabitableOuter = 0.8f;
                sys.GalaxyId = SystemSpec.AutoGalaxy;
                sys.Tier = SystemSpec.AutoTier;
                content.AddSystem(sys, new PlanetDescriptor[0]);
            }
            Universe merged = new Universe(cfg, content);

            Assert.AreEqual(2, merged.AuthoredSystems.Count);
            foreach (StarSystem a in merged.AuthoredSystems)
            {
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                        foreach (StarSystem r in RandomOnly(merged.SystemsInSector(a.Key.Sector.Offset(dx, dy))))
                            Assert.GreaterOrEqual(Dist(a, r), sep - 1e-9, a.Name + " too close to " + r.Id);
            }
            TestLog.Info("random systems " + victim.Id + " and " + borderVictim.Id + " sat within " + 2.0.ToString("0.0") + " ly of the authored systems and were displaced");
            // the two random systems that sat on top of the authored ones must be gone from their old positions
            foreach (StarSystem r in RandomOnly(merged.SystemsInSector(victim.Key.Sector)))
                Assert.IsFalse(r.X == victim.X && r.Y == victim.Y);
            foreach (StarSystem r in RandomOnly(merged.SystemsInSector(borderVictim.Key.Sector)))
                Assert.IsFalse(r.X == borderVictim.X && r.Y == borderVictim.Y);
        }

        [Test]
        public void AuthoredGalaxy_ReplacesRandomGalaxiesAroundIt()
        {
            var cfg = new UniverseConfig();
            Universe plain = new Universe(cfg);
            GalaxyInfo target = null;
            for (int my = -6; my <= 6 && target == null; my++)
                for (int mx = -6; mx <= 6 && target == null; mx++)
                {
                    GalaxyInfo g = plain.Galaxies.GetMacroGalaxy(mx, my);
                    if (g != null && g.Radius > 1400 && g.Kind != GalaxyKind.Elliptical) target = g;
                }
            Assert.IsNotNull(target);

            var injected = new GalaxyInfo("Test Spiral", target.CenterX + 50.0, target.CenterY - 30.0, 1200.0, 3, kind: GalaxyKind.Spiral);
            var content = new AuthoredContent().AddGalaxy(injected);
            Universe merged = new Universe(cfg, content);

            GalaxyInfo at = merged.GalaxyAt(injected.CenterX, injected.CenterY);
            Assert.IsNotNull(at);
            Assert.AreEqual("Test Spiral", at.Name);
            Assert.IsTrue(at.Authored);
            Assert.AreSame(injected, at);
            Assert.AreEqual(1, merged.Galaxies.Authored.Count);
            Assert.IsNull(merged.Galaxies.GetMacroGalaxy(target.MacroX, target.MacroY), "the random galaxy of that cell must give way");
            Assert.Greater(merged.Density(injected.CenterX, injected.CenterY), 0.9);

            int checkedGalaxies = 0;
            for (int my = target.MacroY - 3; my <= target.MacroY + 3; my++)
            {
                for (int mx = target.MacroX - 3; mx <= target.MacroX + 3; mx++)
                {
                    GalaxyInfo g = merged.Galaxies.GetMacroGalaxy(mx, my);
                    if (g == null) continue;
                    double dx = g.CenterX - injected.CenterX, dy = g.CenterY - injected.CenterY;
                    double d = Math.Sqrt(dx * dx + dy * dy);
                    Assert.LessOrEqual(g.Radius + 0.9 * injected.Radius, d + 1e-6, g.Name + " overlaps the authored galaxy");
                    checkedGalaxies++;
                }
            }
            TestLog.Info("authored galaxy displaced the random one in its cell; " + checkedGalaxies + " neighbours keep clear of it");

            // far away galaxies are unaffected
            GalaxyInfo far = null;
            for (int mx = target.MacroX + 6; mx < target.MacroX + 30 && far == null; mx++)
            {
                GalaxyInfo g = plain.Galaxies.GetMacroGalaxy(mx, target.MacroY);
                if (g != null) far = g;
            }
            Assert.IsNotNull(far);
            GalaxyInfo farMerged = merged.Galaxies.GetMacroGalaxy(far.MacroX, far.MacroY);
            Assert.IsNotNull(farMerged);
            Assert.AreEqual(far.Radius, farMerged.Radius);
            Assert.AreEqual(far.Name, farMerged.Name);

            // systems inside the authored galaxy belong to it
            int inside = 0;
            for (int sy = -8; sy <= 8; sy++)
                for (int sx = -8; sx <= 8; sx++)
                {
                    double cx = injected.CenterX + sx * 64.0, cy = injected.CenterY + sy * 64.0;
                    foreach (StarSystem s in merged.SystemsInSector(merged.SectorOf(cx, cy)))
                    {
                        if (merged.GalaxyAt(s.X, s.Y) == null) continue;
                        GalaxyInfo owner = merged.GalaxyOf(s);
                        if (owner != null && owner.Authored) { inside++; Assert.AreEqual(injected.Id, owner.Id); }
                    }
                }
            Assert.Greater(inside, 20);
        }

        [Test]
        public void InvalidAuthoredContent_IsRejectedWithClearErrors()
        {
            var cfg = new UniverseConfig();
            Func<SystemSpec> spec = () =>
            {
                var s = new SystemSpec();
                s.Key = new SystemKey(SectorCoord.FromLy(10, 10, 64.0), SystemKey.AuthoredBase);
                s.Name = "Bad";
                s.X = 10; s.Y = 10;
                s.GalaxyId = SystemSpec.AutoGalaxy;
                s.Tier = SystemSpec.AutoTier;
                s.Luminosity = 1f;
                return s;
            };

            var wrongSector = spec();
            wrongSector.X = 500;
            Assert.Throws<ArgumentException>(() => new Universe(cfg, new AuthoredContent().AddSystem(wrongSector, new PlanetDescriptor[0])));

            var lowIndex = spec();
            lowIndex.Key = new SystemKey(lowIndex.Key.Sector, 5);
            Assert.Throws<ArgumentException>(() => new Universe(cfg, new AuthoredContent().AddSystem(lowIndex, new PlanetDescriptor[0])));

            var dup = new AuthoredContent().AddSystem(spec(), new PlanetDescriptor[0]).AddSystem(spec(), new PlanetDescriptor[0]);
            Assert.Throws<ArgumentException>(() => new Universe(cfg, dup));

            var wrongPlanet = new PlanetSpec();
            wrongPlanet.Key = new PlanetKey(new SystemKey(0, 0, SystemKey.AuthoredBase + 7), 0);
            wrongPlanet.Deposits = new Deposit[0];
            Assert.Throws<ArgumentException>(() => new Universe(cfg, new AuthoredContent().AddSystem(spec(), new[] { new PlanetDescriptor(wrongPlanet) })));

            var galaxyDup = new AuthoredContent().AddGalaxy(new GalaxyInfo("A", 0, 0, 1000, 2)).AddGalaxy(new GalaxyInfo("A", 5000, 0, 1000, 2));
            Assert.Throws<ArgumentException>(() => new Universe(cfg, galaxyDup));
        }

        [Test]
        public void AuthoredPlanets_CountTowardResourceCoverage()
        {
            // an authored planet that carries a resource satisfies it for its block, so no anchor is injected for it
            var u = TestUniverses.WithFakeAuthored();
            PlanetDescriptor sylva;
            Assert.IsTrue(u.TryGetPlanetById("sylva", out sylva));
            Assert.IsTrue(sylva.HasResource("wood"));
            Assert.AreEqual(new PlanetKey(0, 0, SystemKey.AuthoredBase, 0), sylva.Key);
            Assert.AreEqual("p:0:0:1000:0", sylva.Id);
        }
    }
}
