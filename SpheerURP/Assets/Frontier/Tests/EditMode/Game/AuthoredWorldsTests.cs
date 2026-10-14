using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Frontier.Universe;
using Uni = Frontier.Universe.Universe;
using TestLog = Frontier.Universe.Tests.TestLog;

namespace Frontier.Tests
{
    /// <summary>The 30 hand made worlds as universe content: conversion, canonical keys, placement, consistency with the generator.</summary>
    [TestFixture]
    public class AuthoredWorldsTests
    {
        GameDatabase db;
        UniverseService service;
        AuthoredWorlds authored;

        [OneTimeSetUp]
        public void Build()
        {
            db = GameDatabase.Load();
            service = new UniverseService(db);
            authored = service.Authored;
        }

        // The frozen id -> canonical key table. Saves reference planets by these ids; changing any entry orphans player data.
        // Systems: index 1000 + the system's position in PlanetSO.Systems; planets: position in the canonical orbit order.
        static readonly string[][] KeyTable =
        {
            new[] { "sylva", "p:0:0:1000:0" }, new[] { "glacia", "p:0:0:1000:1" }, new[] { "ferrum", "p:0:0:1000:2" }, new[] { "veridian", "p:0:0:1000:3" }, new[] { "calcite", "p:0:0:1000:4" },
            new[] { "cinder", "p:3:0:1001:0" }, new[] { "aurum", "p:3:0:1001:1" }, new[] { "pyre", "p:3:0:1001:2" }, new[] { "sulfur", "p:3:0:1001:3" }, new[] { "magma", "p:3:0:1001:4" },
            new[] { "arbor", "p:0:-6:1002:0" }, new[] { "coral", "p:0:-6:1002:1" }, new[] { "mire", "p:0:-6:1002:2" }, new[] { "tide", "p:0:-6:1002:3" }, new[] { "lotus", "p:0:-6:1002:4" }, new[] { "spore", "p:0:-6:1002:5" },
            new[] { "nyx", "p:9:-2:1003:0" }, new[] { "obsidian", "p:9:-2:1003:1" }, new[] { "vesper", "p:9:-2:1003:2" }, new[] { "eclipse", "p:9:-2:1003:3" }, new[] { "phantom", "p:9:-2:1003:4" },
            new[] { "halcyon", "p:3:-5:1004:0" }, new[] { "zephyr", "p:3:-5:1004:1" }, new[] { "tundra", "p:3:-5:1004:2" }, new[] { "borealis", "p:3:-5:1004:3" }, new[] { "cryo", "p:3:-5:1004:4" },
            new[] { "prism", "p:14:-15:1005:0" }, new[] { "kaos", "p:14:-15:1005:1" }, new[] { "aether", "p:14:-15:1005:2" }, new[] { "genesis", "p:14:-15:1005:3" }
        };

        [Test]
        public void KeyTable_IsFrozen_ForAllThirtyWorlds()
        {
            Assert.AreEqual(30, KeyTable.Length);
            Assert.AreEqual(30, db.planets.Length);
            foreach (var entry in KeyTable)
            {
                var d = authored.DescriptorOf(entry[0]);
                Assert.IsNotNull(d, entry[0] + " was not converted");
                Assert.AreEqual(entry[1], d.Id, "canonical id of " + entry[0]);
                Assert.AreEqual(entry[0], d.AuthoredId);
                PlanetDescriptor viaUniverse;
                Assert.IsTrue(service.Universe.TryGetPlanetById(entry[1], out viaUniverse), entry[1]);
                Assert.AreSame(d, viaUniverse, "the universe serves the authored descriptor");
                Assert.IsTrue(service.Universe.TryGetPlanetById(entry[0], out viaUniverse), "legacy alias " + entry[0]);
                Assert.AreSame(d, viaUniverse);
            }
        }

        [Test]
        public void EveryPlanetAsset_IsConverted_WithUniqueIdsAndKeys()
        {
            var ids = new HashSet<string>();
            var keys = new HashSet<ulong>();
            foreach (var so in db.planets)
            {
                var d = authored.DescriptorOf(so.id);
                Assert.IsNotNull(d, so.id);
                Assert.IsTrue(ids.Add(d.Id), "duplicate canonical id " + d.Id);
                Assert.IsTrue(keys.Add(d.Key.Id), "duplicate key " + d.Key);
                Assert.AreSame(so, authored.SourceOf(so.id));
            }
            Assert.AreEqual(30, authored.Descriptors.Count);
            Assert.AreEqual(30, ids.Count);
            Assert.AreEqual(6, service.Universe.AuthoredSystems.Count);
            foreach (var s in service.Universe.AuthoredSystems)
            {
                Assert.IsTrue(s.Key.IsAuthored);
                Assert.GreaterOrEqual(s.Key.Index, SystemKey.AuthoredBase);
            }
        }

        [Test]
        public void SystemKeys_AreThePositionInThePlanetSOSystemList()
        {
            for (int k = 0; k < PlanetSO.Systems.Length; k++)
            {
                var placement = authored.Placements[k];
                Assert.AreEqual(PlanetSO.Systems[k].id, placement.Id);
                Assert.AreEqual(SystemKey.AuthoredBase + k, placement.Key.Index);
                Assert.AreEqual(SectorCoord.FromLy(placement.X, placement.Y, 64.0), placement.Key.Sector);
            }
            Assert.AreEqual("s:0:0:1000", authored.Placements[0].Key.ToString(), "Sylva is the home system");
        }

        [Test]
        public void HomeSystem_SitsAtTheOrigin_AndTheOthersFollowTheOriginalMapDirection()
        {
            var home = authored.Placements[0];
            Assert.AreEqual(0.0, home.X);
            Assert.AreEqual(0.0, home.Y);
            Assert.AreEqual(0, home.TierAtPosition);
            var sylvaCenter = PlanetSO.Systems[0].center;
            for (int k = 1; k < authored.Placements.Count; k++)
            {
                var p = authored.Placements[k];
                var c = PlanetSO.Systems[k].center - sylvaCenter;
                double len = Math.Sqrt(p.X * p.X + p.Y * p.Y);
                double cos = (p.X * c.x + p.Y * c.y) / (len * c.magnitude);
                Assert.Greater(cos, 0.9999, p.Id + " must lie along the original map direction (cos " + cos + ")");
            }
        }

        [Test]
        public void PlacementTable_MatchesTheTierRule_AndStaysInsideTheHomeGalaxy()
        {
            var u = service.Universe;
            GalaxyInfo galaxy = u.GalaxyAt(0.0, 0.0);
            Assert.IsNotNull(galaxy);
            Assert.AreEqual("The Verdant Spiral", galaxy.Name);
            TestLog.Info("authored system placement (home galaxy " + galaxy.Name + ", radius " + galaxy.Radius + " ly):");
            TestLog.Info("  system     key            x ly      y ly    distance   tier@pos  authored tiers (avg)");
            foreach (var p in authored.Placements)
            {
                TestLog.Info(string.Format(System.Globalization.CultureInfo.InvariantCulture, "  {0,-9} {1,-13} {2,8:0.00} {3,9:0.00} {4,9:0.0} {5,6}     {6}..{7} ({8:0.00}){9}",
                    p.Id, p.Key, p.X, p.Y, p.DistanceFromHomeLy, p.TierAtPosition, p.MinTier, p.MaxTier, p.AverageTier, p.PulledInsideGalaxy ? "  (pulled inside the galaxy)" : ""));
                Assert.AreEqual(u.TierAt(p.X, p.Y), p.TierAtPosition, p.Id + " tier at its position");
                StarSystem sys;
                Assert.IsTrue(u.TryGetSystem(p.Key, out sys));
                Assert.AreEqual(p.TierAtPosition, sys.Tier, "the universe computes the same tier for the authored system");
                Assert.AreEqual(galaxy.Id, sys.GalaxyId, p.Id + " belongs to the home galaxy");
                Assert.IsTrue(galaxy.Contains(p.X, p.Y), p.Id + " is inside the home galaxy");
                Assert.LessOrEqual(galaxy.NormalizedRadius(p.X, p.Y), 0.9201, p.Id + " keeps clear of the rim");
                if (p.Id == "sylva") continue;
                Assert.AreEqual(Math.Floor(p.AverageTier + 0.5), p.TierAtPosition, p.Id + ": tier at the position is the rounded average authored tier");
                Assert.GreaterOrEqual(p.TierAtPosition, p.MinTier - 1);
                Assert.LessOrEqual(p.TierAtPosition, p.MaxTier + 1);
            }
            var edge = authored.Placements[5];
            Assert.AreEqual("edge", edge.Id);
            Assert.GreaterOrEqual(edge.DistanceFromHomeLy, 1200.0);
            Assert.LessOrEqual(edge.DistanceFromHomeLy, 1800.0);
            Assert.IsTrue(edge.PulledInsideGalaxy, "the tier rule alone would put 'the edge' past the rim");
            double previous = -1.0;
            foreach (var p in authored.Placements)
            {
                if (p.Id == "frost") continue;   // frost and verdance share a tier band, so equal distances are expected
                Assert.GreaterOrEqual(p.DistanceFromHomeLy, previous - 1e-9, "deeper systems are further away");
                previous = p.DistanceFromHomeLy;
            }
        }

        [Test]
        public void Descriptors_MirrorTheAssets()
        {
            foreach (var so in db.planets)
            {
                var d = authored.DescriptorOf(so.id);
                Assert.AreEqual(so.displayName, d.Name);
                Assert.AreEqual(so.tier, d.Tier);
                Assert.AreEqual(so.danger, d.Danger);
                Assert.AreEqual(so.worldSize, d.WorldSize);
                Assert.AreEqual(so.BuildingSlots, d.BuildingSlots);
                Assert.AreEqual((int)so.weather, (int)d.Weather);
                Assert.AreEqual(so.hasRings, d.HasRings);
                Assert.AreEqual(so.style, d.Style);
                Assert.AreEqual(so.ModelKey, d.ModelKey);
                Assert.AreEqual(so.primary.r, d.Primary.R, 1e-6f);
                Assert.AreEqual(so.rim.g, d.Rim.G, 1e-6f);
                Assert.AreEqual(so.tier / 2, d.MoonCount, "the planet view always built tier / 2 moons");
                Assert.IsNotNull(authored.PlacementOf(so.id));
                Assert.IsTrue(d.IsAuthored);
                Assert.AreEqual(UniverseConfig.CurrentGeneratorVersion, d.GeneratorVersion);
                Assert.IsNotNull(d.Designation);
            }
        }

        [Test]
        public void Deposits_AreValid_AndComeFromTheGatherSpotsWithRichnessOne()
        {
            var table = service.Universe.Resources;
            foreach (var so in db.planets)
            {
                var d = authored.DescriptorOf(so.id);
                Assert.AreEqual(so.spots.Count, d.Deposits.Length, so.id);
                Assert.Greater(d.Deposits.Length, 0, so.id + " offers nothing");
                for (int i = 0; i < d.Deposits.Length; i++)
                {
                    var dep = d.Deposits[i];
                    var spot = so.spots[i];
                    Assert.IsTrue(table.Contains(dep.ResourceId), so.id + ": '" + dep.ResourceId + "' is not a universe resource");
                    Assert.IsNotNull(db.FindResource(dep.ResourceId), so.id + ": '" + dep.ResourceId + "' is not a ResourceSO");
                    Assert.AreEqual(1f, dep.Richness);
                    Assert.Greater(dep.BaseYield, 0);
                    Assert.GreaterOrEqual(dep.Difficulty, 0f);
                    Assert.LessOrEqual(dep.Difficulty, 1f);
                    Assert.AreEqual(spot.resource.id, dep.ResourceId);
                    Assert.AreEqual(spot.baseYield, dep.BaseYield);
                    Assert.AreEqual((int)spot.game, (int)dep.Method, "harvest methods 0..2 are the old mini-games");
                    Assert.AreEqual(spot.game, HarvestMapping.ToMinigame(dep.Method));
                }
            }
        }

        [Test]
        public void OrbitsInEachSystem_AreOrderedInnerToOuter_WithValidPhases()
        {
            foreach (var placement in authored.Placements)
            {
                double previous = 0.0;
                foreach (string id in placement.PlanetIds)
                {
                    var d = authored.DescriptorOf(id);
                    Assert.Greater(d.OrbitRadius, previous, id + " must orbit outside its inner neighbour (canonical order = orbit order)");
                    Assert.GreaterOrEqual(d.OrbitRadius, 0.4f - 1e-6f);
                    Assert.Less(d.OrbitRadius, 4.0f);
                    Assert.GreaterOrEqual(d.OrbitPhase, 0f);
                    Assert.Less(d.OrbitPhase, 6.2832f);
                    previous = d.OrbitRadius;
                }
            }
        }

        [Test]
        public void OrbitPhases_KeepTheOldMapAngle_ForPlanetsThatHadAPosition()
        {
            // Sylva's old map offset from its system centre was (0.0017, 0.0550): straight "up", about 88 degrees.
            var sylva = authored.DescriptorOf("sylva");
            Assert.AreEqual(88.2 * Math.PI / 180.0, sylva.OrbitPhase, 0.01);
            Assert.AreEqual(0.4f, sylva.OrbitRadius, 0.01f, "the innermost orbit");
            var glacia = authored.DescriptorOf("glacia");
            Assert.AreEqual(328.2 * Math.PI / 180.0, glacia.OrbitPhase, 0.01);
        }

        [Test]
        public void StarsAndPlanetKinds_AreDerivedFromColoursAndTerrain()
        {
            var u = service.Universe;
            foreach (var s in u.AuthoredSystems)
            {
                Assert.Greater(s.Luminosity, 0f);
                Assert.Greater(s.SurfaceTemperature, 1000f);
                Assert.Less(s.HabitableInner, s.HabitableOuter);
            }
            Assert.AreEqual(StarClass.G, u.AuthoredSystems[0].StarClass, "Sylva's yellow-white star");
            Assert.AreEqual(StarClass.M, u.AuthoredSystems[1].StarClass, "Emberfall's red-orange star");
            Assert.AreEqual(PlanetKind.GasGiant, authored.DescriptorOf("zephyr").Kind);
            Assert.AreEqual(Biome.GasGiant, authored.DescriptorOf("zephyr").Biome);
            Assert.AreEqual(Biome.Lush, authored.DescriptorOf("sylva").Biome);
            Assert.AreEqual(Biome.Oceanic, authored.DescriptorOf("tide").Biome);
            Assert.AreEqual(Biome.Desert, authored.DescriptorOf("aurum").Biome);
            Assert.AreEqual(SizeClass.Dwarf, authored.DescriptorOf("sylva").Size);
            Assert.AreEqual(SizeClass.Giant, authored.DescriptorOf("genesis").Size);
            foreach (var d in authored.Descriptors) Assert.AreNotEqual(PlanetKind.AsteroidBelt, d.Kind);
        }

        [Test]
        public void BuildingTwice_GivesIdenticalContent()
        {
            var again = AuthoredWorlds.Build(db, new UniverseConfig());
            Assert.AreEqual(authored.Placements.Count, again.Placements.Count);
            for (int i = 0; i < again.Placements.Count; i++)
            {
                Assert.AreEqual(authored.Placements[i].Key, again.Placements[i].Key);
                Assert.AreEqual(authored.Placements[i].X, again.Placements[i].X);
                Assert.AreEqual(authored.Placements[i].Y, again.Placements[i].Y);
            }
            for (int i = 0; i < again.Descriptors.Count; i++)
            {
                Assert.AreEqual(authored.Descriptors[i].Id, again.Descriptors[i].Id);
                Assert.AreEqual(authored.Descriptors[i].OrbitRadius, again.Descriptors[i].OrbitRadius);
                Assert.AreEqual(authored.Descriptors[i].OrbitPhase, again.Descriptors[i].OrbitPhase);
            }
        }

        [Test]
        public void NoRandomSystem_OverlapsAnAuthoredOne()
        {
            var u = service.Universe;
            double sep = u.Config.MinSystemSeparation;
            double closest = double.MaxValue;
            foreach (var a in u.AuthoredSystems)
                for (int dy = -2; dy <= 2; dy++)
                    for (int dx = -2; dx <= 2; dx++)
                        foreach (var r in u.SystemsInSector(a.Key.Sector.Offset(dx, dy)))
                        {
                            if (r.IsAuthored) continue;
                            double d = Math.Sqrt((a.X - r.X) * (a.X - r.X) + (a.Y - r.Y) * (a.Y - r.Y));
                            closest = Math.Min(closest, d);
                            Assert.GreaterOrEqual(d, sep - 1e-9, a.Name + " vs " + r.Id);
                        }
            TestLog.Info("closest generated system to any Core World system: " + closest.ToString("0.0") + " ly (minimum " + sep + ")");
        }

        [Test]
        public void AuthoredSystemsThemselves_KeepTheirSeparation()
        {
            var systems = service.Universe.AuthoredSystems;
            for (int i = 0; i < systems.Count; i++)
                for (int j = i + 1; j < systems.Count; j++)
                {
                    double d = Math.Sqrt((systems[i].X - systems[j].X) * (systems[i].X - systems[j].X) + (systems[i].Y - systems[j].Y) * (systems[i].Y - systems[j].Y));
                    Assert.Greater(d, 100.0, systems[i].Name + " vs " + systems[j].Name);
                }
        }

        [Test]
        public void ResourceCoverage_StillHolds_WithTheRealCoreWorlds()
        {
            // Authored planets count toward the coverage guarantee of their block; the generator must still satisfy every resource there.
            var u = service.Universe;
            foreach (var resource in u.Resources.Defs)
            {
                if (resource.MinTier > 3) continue; // The bounded home region need not contain late-game resources.
                string id = resource.Id;
                int planetsWith = 0;
                for (int sy = -8; sy <= 8; sy++)
                    for (int sx = -8; sx <= 8; sx++)
                        foreach (var s in u.SystemsInSector(new SectorCoord(sx, sy)))
                            foreach (var p in u.PlanetsOf(s)) if (p.HasResource(id)) planetsWith++;
                Assert.Greater(planetsWith, 0, id + " must exist around home");
            }
        }
    }
}
