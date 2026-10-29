using System.Collections.Generic;
using NUnit.Framework;
using Frontier.Universe;
using Uni = Frontier.Universe.Universe;

namespace Frontier.Tests
{
    public class MapQueryTests
    {
        Uni universe;
        [OneTimeSetUp] public void Setup() { universe = new Uni(new UniverseConfig(), Landmarks.CreateDefault()); }

        [Test]
        public void GalaxyView_HasFixedDensityBudget_AndNeverEnumeratesItsSectors()
        {
            var before = universe.GetCacheStats();
            var snapshot = MapQuery.Build(universe, -50000, -50000, 50000, 50000, "", new MapPoint[0], 40);
            Assert.IsTrue(snapshot.densityOnly);
            Assert.AreEqual(MapQuery.Columns * MapQuery.Rows, snapshot.density.Length);
            Assert.AreEqual(before.SectorMisses, universe.GetCacheStats().SectorMisses);
            Assert.LessOrEqual(snapshot.systems.Length, MapQuery.MaxSystems);
        }

        [Test]
        public void SectorView_IsBounded_Deterministic_AndFilteredOnlyFromSurveyedWorlds()
        {
            var centers = new[] { new MapPoint(0, 0) };
            var a = MapQuery.Build(universe, -600, -600, 600, 600, "", centers, 40);
            var b = MapQuery.Build(universe, -600, -600, 600, 600, "", centers, 40);
            Assert.IsFalse(a.densityOnly);
            Assert.LessOrEqual(a.systems.Length, MapQuery.MaxSystems);
            Assert.Greater(a.systems.Length, 0);
            Assert.AreEqual(a.systems.Length, b.systems.Length);
            for (int i = 0; i < a.systems.Length; i++) Assert.AreEqual(a.systems[i].Id, b.systems[i].Id);
            var filtered = MapQuery.Build(universe, -600, -600, 600, 600, "wood", centers, 40);
            foreach (var star in filtered.systems)
            {
                Assert.IsTrue(MapQuery.Surveyed(star, centers, 40));
                bool found = false;
                foreach (var planet in universe.PlanetsOf(star)) found |= planet.HasResource("wood");
                Assert.IsTrue(found);
            }
            Assert.AreEqual(0, MapQuery.Build(universe, -100, -100, 100, 100, "not-a-resource", centers, 40).systems.Length);
        }

        [Test]
        public void SurveyRadius_RevealsWorldsFromEveryVisitedSystem()
        {
            var star = universe.SystemsInRect(-200, -200, 200, 200)[0];
            Assert.IsFalse(MapQuery.Surveyed(star, new MapPoint[0], 40));
            Assert.IsTrue(MapQuery.Surveyed(star, new[] { new MapPoint(star.X, star.Y) }, 40));
            Assert.IsFalse(MapQuery.Surveyed(star, new[] { new MapPoint(star.X + 41, star.Y) }, 40));
        }

        [Test]
        public void SurveyRadius_UsesTowerCoverageAtItsVisitedSystem()
        {
            var star = universe.SystemsInRect(-200, -200, 200, 200)[0];
            var center = new[] { new MapSurveyPoint(star.X - 50, star.Y, 60) };
            Assert.IsTrue(MapQuery.Surveyed(star, center));
            center[0] = new MapSurveyPoint(star.X - 50, star.Y, 49);
            Assert.IsFalse(MapQuery.Surveyed(star, center));
        }

        [Test]
        public void PreviouslyDiscoveredSystemsRemainSurveyedAndResourceFilterable()
        {
            StarSystem target = null;
            foreach (var star in universe.SystemsInRect(-600, -600, 600, 600))
            {
                if (star.IsAuthored) continue;
                foreach (var planet in universe.PlanetsOf(star))
                    if (planet.HasResource("wood")) { target = star; break; }
                if (target != null) break;
            }
            Assert.IsNotNull(target, "fixture must include a generated wood system");
            var discovered = new[] { target.Id };
            Assert.IsTrue(MapQuery.Surveyed(target, new MapSurveyPoint[0], new HashSet<string>(discovered)));
            var filtered = MapQuery.Build(universe, -600, -600, 600, 600, "wood", new MapSurveyPoint[0], discovered);
            CollectionAssert.Contains(SystemIds(filtered.systems), target.Id);
            var hidden = MapQuery.Build(universe, -600, -600, 600, 600, "wood", new MapSurveyPoint[0], new string[0]);
            CollectionAssert.DoesNotContain(SystemIds(hidden.systems), target.Id);
        }

        [Test]
        public void MapNameIndex_FindsOnlyDiscoveredAndPinnedNamesWithoutRevealingAuthoredSystems()
        {
            var candidates = universe.SystemsInRect(-600, -600, 600, 600);
            StarSystem target = null;
            foreach (var star in candidates)
                if (!star.IsAuthored && universe.PlanetsOf(star).Length > 0) { target = star; break; }
            Assert.IsNotNull(target, "fixture must include a generated system");
            var targetWorld = universe.PlanetsOf(target)[0];

            var hidden = MapNameIndex.Build(universe, new string[0]);
            string planetId;
            Assert.IsFalse(hidden.TryFind(target.Id, out planetId));
            if (universe.AuthoredSystems.Count > 0)
                Assert.IsFalse(hidden.TryFind(universe.AuthoredSystems[0].Name, out planetId));

            var known = MapNameIndex.Build(universe, new[] { target.Id });
            Assert.IsTrue(known.TryFind(target.Id, out planetId));
            Assert.AreEqual(targetWorld.Id, planetId);
            Assert.IsTrue(known.TryFind(targetWorld.Name.ToUpperInvariant(), out planetId));
            Assert.AreEqual(targetWorld.Id, planetId);

            var pinned = MapNameIndex.Build(universe, new string[0], new[] { targetWorld.Id });
            Assert.IsTrue(pinned.TryFind(targetWorld.Name, out planetId));
            Assert.AreEqual(targetWorld.Id, planetId);
            Assert.IsTrue(pinned.TryFind(target.Id, out planetId));
            Assert.AreEqual(targetWorld.Id, planetId);
            foreach (var other in candidates)
            {
                if (other.IsAuthored || other.Id == target.Id) continue;
                Assert.IsFalse(pinned.TryFind(other.Id, out planetId), "pinning a planet must not reveal unrelated systems");
                break;
            }
        }

        [Test]
        public void UnknownSystemsAreHiddenEvenWithoutResourceFiltersAndAuthoredWorldsNeedScanning()
        {
            var empty = MapQuery.Build(universe, -600, -600, 600, 600, "", new MapSurveyPoint[0]);
            Assert.AreEqual(0, empty.systems.Length);
            Assert.AreEqual(0, empty.surveyedSystems.Length);
            var config = new UniverseConfig();
            var catalog = new Uni(config, Frontier.Universe.Tests.FakeAuthored.Create(config));
            var target = catalog.AuthoredSystems[0];
            Assert.IsFalse(MapQuery.Surveyed(target, new MapSurveyPoint[0]));
            Assert.IsFalse(MapQuery.Surveyed(target, new MapPoint[0], 40));
            var centers = new[] { new MapSurveyPoint(target.X, target.Y, 40) };
            var scanned = MapQuery.Build(catalog, target.X - 100, target.Y - 100, target.X + 100, target.Y + 100, "", centers);
            CollectionAssert.Contains(SystemIds(scanned.systems), target.Id);
            foreach (var star in scanned.systems) Assert.IsTrue(MapQuery.Surveyed(star, centers));
            var remembered = MapQuery.Build(catalog, -50000, -50000, 50000, 50000, "", new MapSurveyPoint[0], new[] { target.Id });
            CollectionAssert.Contains(SystemIds(remembered.systems), target.Id);
            Assert.AreEqual(1, remembered.systems.Length);
            Assert.AreEqual(0, MapQuery.Build(catalog, -50000, -50000, 50000, 50000, "", new MapSurveyPoint[0]).systems.Length);
            Assert.IsFalse(MapNameIndex.Build(catalog, new string[0]).TryFind(target.Name, out _));
            Assert.IsTrue(MapNameIndex.Build(catalog, new[] { target.Id }).TryFind(target.Name, out _));
        }

        [Test]
        public void MapNameIndex_AddsNewlySurveyedSystems()
        {
            StarSystem target = null;
            foreach (var star in universe.SystemsInRect(-600, -600, 600, 600))
                if (!star.IsAuthored && universe.PlanetsOf(star).Length > 0) { target = star; break; }
            Assert.IsNotNull(target, "fixture must include a generated system with a planet");
            var index = MapNameIndex.Build(universe, new string[0]);
            string planetId;
            Assert.IsFalse(index.TryFind(target.Id, out planetId));

            index.AddSystems(universe, new[] { target });
            Assert.IsTrue(index.TryFind(target.Id, out planetId));
            Assert.AreEqual(universe.PlanetsOf(target)[0].Id, planetId);
        }

        static string[] SystemIds(StarSystem[] systems)
        {
            var ids = new string[systems.Length];
            for (int i = 0; i < systems.Length; i++) ids[i] = systems[i].Id;
            return ids;
        }
    }
}
