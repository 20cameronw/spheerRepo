using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Frontier.Universe;
using TestLog = Frontier.Universe.Tests.TestLog;

namespace Frontier.Tests
{
    /// <summary>The game's door to the universe: resource validation, id lookup, caching, searching, distances and travel prices.</summary>
    [TestFixture]
    public class UniverseServiceTests
    {
        GameDatabase db;
        UniverseService service;

        [OneTimeSetUp]
        public void Build()
        {
            db = GameDatabase.Load();
            service = new UniverseService(db);
        }

        [Test]
        public void TheShippedSeed_IsAConstant_AndPlannedResourcesAreOff()
        {
            Assert.AreEqual(0x46524F4E54494552UL, UniverseService.DefaultSeed, "changing the seed makes every saved generated world a different planet");
            Assert.AreEqual(UniverseService.DefaultSeed, service.Seed);
            Assert.IsFalse(service.Universe.Config.EnablePlannedResources);
            foreach (var def in service.Universe.Resources.Defs) Assert.IsFalse(def.Planned, def.Id + " is a planned resource");
        }

        [Test]
        public void EveryResourceTheGeneratorCanPlace_IsAResourceSO()
        {
            var table = UniverseService.BuildResourceTable(db, false);
            foreach (var def in table.Defs) Assert.IsNotNull(db.FindResource(def.Id), def.Id);
            for (int b = 0; b < table.Profiles.Length; b++)
                foreach (var entry in table.Profiles[b].Entries) Assert.IsNotNull(db.FindResource(entry.ResourceId), (Biome)b + " offers " + entry.ResourceId);
            foreach (string id in table.AlwaysPresent) Assert.IsNotNull(db.FindResource(id), id);
        }

        [Test]
        public void ADatabaseMissingAResource_IsRejected_WithTheNameOfTheResource()
        {
            var broken = UnityEngine.Object.Instantiate(db);
            try
            {
                broken.resources = db.resources.Where(r => r.id != "crystal").ToArray();
                var error = Assert.Throws<InvalidOperationException>(() => UniverseService.BuildResourceTable(broken, false));
                StringAssert.Contains("crystal", error.Message);
                Assert.Throws<InvalidOperationException>(() => new UniverseService(broken));
            }
            finally { UnityEngine.Object.DestroyImmediate(broken); }
        }

        [Test]
        public void Get_AcceptsCanonicalIdsAndLegacyIds_AndReturnsTheSameObject()
        {
            var byAlias = service.Get("sylva");
            var byCanonical = service.Get("p:0:0:1000:0");
            Assert.IsNotNull(byAlias);
            Assert.AreSame(byAlias, byCanonical);
            Assert.AreSame(byAlias, service.Home);
            Assert.AreEqual("p:0:0:1000:0", byAlias.Id);
            Assert.AreEqual("sylva", byAlias.AuthoredId);
            Assert.AreSame(service.Get("glacia"), service.Get("glacia"), "authored worlds are cached for good");
        }

        [Test]
        public void Get_UnknownOrMalformedIds_GiveNull()
        {
            Assert.IsNull(service.Get((string)null));
            Assert.IsNull(service.Get(""));
            Assert.IsNull(service.Get("no-such-world"));
            Assert.IsNull(service.Get("p:1:2:3"));
            Assert.IsNull(service.Get("p:0:0:1000:99"), "a planet index the system does not have");
        }

        [Test]
        public void TryGet_ByKey_WorksForAuthoredAndGeneratedWorlds()
        {
            PlanetData planet;
            Assert.IsTrue(service.TryGet(new PlanetKey(0, 0, 1000, 1), out planet));
            Assert.AreEqual("glacia", planet.AuthoredId);
            var generated = service.FindGenerated(Biome.Lush, null);
            Assert.IsTrue(service.TryGet(generated.Key, out planet));
            Assert.AreEqual(generated.Id, planet.Id);
            Assert.IsFalse(service.TryGet(new PlanetKey(0, 0, 1000, 77), out planet), "the system has no such planet");
            Assert.IsNull(planet);
        }

        [Test]
        public void PlanetsOfASystem_ComeInOrbitOrder()
        {
            var sylvaSystem = service.Home.Star;
            var planets = service.PlanetsOf(sylvaSystem);
            CollectionAssert.AreEqual(new[] { "sylva", "glacia", "ferrum", "veridian", "calcite" }, planets.Select(p => p.AuthoredId).ToArray());
            for (int i = 1; i < planets.Count; i++) Assert.Greater(planets[i].Descriptor.OrbitRadius, planets[i - 1].Descriptor.OrbitRadius);
            foreach (var p in planets) Assert.AreSame(sylvaSystem, p.Star);
        }

        [Test]
        public void AuthoredPlanets_FollowTheDatabaseOrder()
        {
            Assert.AreEqual(db.planets.Length, service.AuthoredPlanets.Count);
            for (int i = 0; i < db.planets.Length; i++)
            {
                Assert.AreEqual(db.planets[i].id, service.AuthoredPlanets[i].AuthoredId);
                Assert.AreSame(db.planets[i], service.AuthoredPlanets[i].Authored);
            }
        }

        [Test]
        public void GeneratedWorlds_AreCached_ButTheCacheIsBounded_AndEvictionChangesNothing()
        {
            var own = new UniverseService(db);
            var descriptors = new List<PlanetDescriptor>();
            for (int ring = 0; ring < 30 && descriptors.Count < 220; ring++)
                for (int dx = -ring; dx <= ring && descriptors.Count < 220; dx++)
                    for (int dy = -ring; dy <= ring && descriptors.Count < 220; dy++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != ring) continue;
                        foreach (var system in own.Universe.SystemsInSector(new SectorCoord(dx, dy)))
                            if (!system.IsAuthored) descriptors.AddRange(own.Universe.PlanetsOf(system));
                    }
            Assert.Greater(descriptors.Count, 200, "enough generated worlds to overflow the 128 entry cache");
            var first = own.Get(descriptors[0]);
            Assert.AreSame(first, own.Get(descriptors[0]), "a recent world is cached");
            foreach (var d in descriptors) own.Get(d);
            var again = own.Get(descriptors[0].Id);
            Assert.AreNotSame(first, again, "the oldest entry was evicted");
            Assert.AreEqual(first.Id, again.Id);
            Assert.AreEqual(first.Seed, again.Seed, "evicted and rebuilt: the same world");
            Assert.AreEqual(first.Name, again.Name);
            Assert.AreEqual(first.Description, again.Description);
            Assert.AreEqual(first, again, "equal by id even though the object is new");
            Assert.AreEqual(first.GetHashCode(), again.GetHashCode());
            var last = own.Get(descriptors[descriptors.Count - 1]);
            Assert.AreSame(last, own.Get(descriptors[descriptors.Count - 1]), "the newest entries stay cached");
        }

        [Test]
        public void FindGenerated_IsDeterministic_AndHonoursTheQuery()
        {
            var a = service.FindGenerated(Biome.Volcanic, null, 3);
            var b = service.FindGenerated(Biome.Volcanic, null, 3);
            Assert.IsNotNull(a);
            Assert.AreEqual(a.Id, b.Id);
            Assert.IsFalse(a.IsAuthored);
            Assert.AreEqual(Biome.Volcanic, a.Biome);
            Assert.GreaterOrEqual(a.Tier, 3);
            var next = service.FindGenerated(Biome.Volcanic, null, 3, 1);
            Assert.AreNotEqual(a.Id, next.Id, "skip returns the next match");
            var sized = service.FindGenerated(null, SizeClass.Colossal, 0);
            Assert.AreEqual(SizeClass.Colossal, sized.Size);
            Assert.AreEqual(2.5f, sized.WorldSize, 0.001f);
            Assert.AreEqual(64, sized.BuildingSlots);
            var custom = service.FindGenerated(d => d.HasRings && d.Biome == Biome.GasGiant, 2);
            Assert.IsNotNull(custom);
            Assert.IsTrue(custom.HasRings);
        }

        [Test]
        public void EveryBiomeAndSizeClass_CanBeFound_QuickEnoughForAMenu()
        {
            var watch = Stopwatch.StartNew();
            foreach (Biome biome in Enum.GetValues(typeof(Biome)))
            {
                var found = service.FindGenerated(biome, null, 0, 0, 3000);
                Assert.IsNotNull(found, biome + " must be reachable from the debug menu");
                Assert.AreEqual(biome, found.Biome);
            }
            foreach (SizeClass size in Enum.GetValues(typeof(SizeClass)))
            {
                var found = service.FindGenerated(null, size, 0, 0, 3000);
                Assert.IsNotNull(found, size + " must be reachable from the debug menu");
                Assert.AreEqual(size, found.Size);
            }
            TestLog.Info("finding one world of each of the 12 biomes and 6 size classes took " + watch.ElapsedMilliseconds + " ms");
            Assert.Less(watch.ElapsedMilliseconds, 20000, "a debug menu search must not freeze the game");
        }

        [Test]
        public void Planner_UsesTheLockedEconomyRules_ForAuthoredWorlds()
        {
            var sylva = service.Get("sylva");
            var glacia = service.Get("glacia");
            var ferrum = service.Get("ferrum");
            var ship = new Frontier.Economy.ShipCapabilities { jumpLevel = 5, enforceHazardPlating = false };
            Func<PlanetData, PlanetData, RoutePlan> plan = (a, b) => service.Planner.Plan(service.EndpointOf(a), service.EndpointOf(b), ship, false, null);
            RoutePlan hop = plan(sylva, glacia);
            Assert.IsTrue(hop.reachable);
            Assert.IsTrue(hop.intraSystem);
            Assert.AreEqual(hop.totalFuel, plan(glacia, sylva).totalFuel, "symmetric");
            Assert.IsTrue(plan(sylva, ferrum).reachable);
            Assert.IsTrue(plan(sylva, sylva).Has(RouteReasons.AlreadyHere));
            int previous = 0;
            foreach (string system in new[] { "cinder", "arbor", "nyx", "prism" })
            {
                RoutePlan route = plan(sylva, service.Get(system));
                Assert.IsTrue(route.reachable, system);
                int cost = route.totalFuel;
                Assert.Greater(cost, hop.totalFuel, system + " is another star system");
                Assert.GreaterOrEqual(cost, previous, "deeper systems are further away");
                previous = cost;
            }
            int baseline = plan(sylva, service.Get("nyx")).totalFuel;
            ship.engineLevel = 5;
            Assert.Less(plan(sylva, service.Get("nyx")).totalFuel, baseline, "efficiency upgrades pay off");
            Assert.GreaterOrEqual(plan(sylva, glacia).totalFuel, 1, "never free");
        }

        [Test]
        public void DistancesFromHome_AreInLightYears()
        {
            Assert.AreEqual(0.0, service.DistanceFromHomeLy(service.Home));
            Assert.AreEqual(0.0, service.DistanceFromHomeLy(service.Get("glacia")), "same system as home");
            Assert.AreEqual(213.3, service.DistanceFromHomeLy(service.Get("cinder")), 0.1);
            Assert.AreEqual(1297.7, service.DistanceFromHomeLy(service.Get("prism")), 0.1);
            var far = service.FindGenerated(null, null, 8);
            Assert.Greater(service.DistanceFromHomeLy(far), 500.0, "tier 8 lies far out");
        }

        [Test]
        public void TheServiceIsTheWorldIdMap_OfTheMigrator()
        {
            IWorldIdMap map = service;
            Assert.AreEqual("p:0:0:1000:0", map.HomeId);
            string canonical;
            Assert.IsTrue(map.TryCanonical("zephyr", out canonical));
            Assert.AreEqual("p:3:-5:1004:1", canonical);
            Assert.IsTrue(map.TryCanonical("p:3:-5:1004:1", out canonical), "already canonical");
            Assert.IsFalse(map.TryCanonical("nowhere", out canonical));
            Assert.AreEqual(0.0, map.DistanceFromHomeLy("p:0:0:1000:2"), "ferrum is in the home system");
            Assert.Greater(map.DistanceFromHomeLy("p:14:-15:1005:0"), 1000.0);
            Assert.Less(map.DistanceFromHomeLy("p:9:9:9:9"), 0.0, "unknown worlds have no distance");
        }

        [Test]
        public void ForADatabase_OneServiceIsShared()
        {
            Assert.AreSame(UniverseService.For(db), UniverseService.For(db));
        }

        [Test]
        public void GeneratedWorldPositions_AreFinite()
        {
            foreach (Biome biome in Enum.GetValues(typeof(Biome)))
            {
                var p = service.FindGenerated(biome, null, 0, 0, 3000);
                Assert.IsFalse(double.IsNaN(p.Star.X) || double.IsNaN(p.Star.Y) || double.IsInfinity(p.Star.X) || double.IsInfinity(p.Star.Y));
            }
        }
    }
}
