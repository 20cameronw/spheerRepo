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
    }
}
