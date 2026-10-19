using Frontier.EditorTools;
using Frontier.Universe;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public class UniverseViewerTests
    {
        UniverseViewerWindow window;
        [SetUp] public void SetUp() { window = ScriptableObject.CreateInstance<UniverseViewerWindow>(); }
        [TearDown] public void TearDown() { if (window != null) Object.DestroyImmediate(window); }

        [Test]
        public void SeedAndCoordinateJump_RenderBoundedPreview_AndInspectTheCorrectSector()
        {
            Assert.IsTrue(window.TryRefresh("1", -0.001, -0.001, 30000, out var error), error);
            Assert.AreEqual(1UL, window.CurrentSeed);
            Assert.AreEqual(new SectorCoord(-1, -1), window.InspectedSector);
            Assert.AreEqual(256, window.Preview.width);
            Assert.AreEqual(256, window.Preview.height);
            var old = window.Preview;
            Assert.IsTrue(window.TryRefresh("2", 0, 0, 30000, out error), error);
            Assert.IsTrue(old == null, "Replaced preview textures must be destroyed.");
            Assert.AreEqual(2UL, window.CurrentSeed);
        }

        [Test]
        public void InvalidInputs_ReportProblems_AndPreserveTheLastGoodPreview()
        {
            Assert.IsTrue(window.TryRefresh("1", 0, 0, 30000, out _));
            var before = window.Preview;
            Assert.IsFalse(window.TryRefresh("not-hex", 0, 0, 30000, out var message));
            StringAssert.Contains("seed", message);
            Assert.IsFalse(window.TryRefresh("00000000000000001", 0, 0, 30000, out message));
            Assert.IsFalse(window.TryRefresh("2", double.NaN, 0, 30000, out message));
            Assert.IsFalse(window.TryRefresh("2", 0, double.PositiveInfinity, 30000, out message));
            Assert.IsFalse(window.TryRefresh("2", 0, 0, 0, out message));
            Assert.IsFalse(window.TryRefresh("2", double.MaxValue, 0, 30000, out message));
            Assert.AreSame(before, window.Preview);
            Assert.AreEqual(1UL, window.CurrentSeed);
        }

        [Test]
        public void PlanetInspector_UsesTheAuthoredRuntimeAdapter_WithoutChangingTheLiveSession()
        {
            var previous = GameState.I;
            using (var sandbox = new SaveSandbox())
            {
                var state = new GameState(GameDatabase.Load(), false);
                var planet = state.planet;
                Assert.IsTrue(window.TryRefresh("1234", 0, 0, 30000, out _));
                Assert.IsTrue(window.TrySelectPlanet(planet.Id, out var error), error);
                Assert.AreEqual(planet.Id, window.SelectedPlanetId);
                StringAssert.Contains(planet.Id, window.SelectedDescriptor);
                Assert.AreSame(state, GameState.I);
                Assert.AreSame(planet, state.planet);
                Assert.AreEqual(UniverseService.DefaultSeed, state.universe.Seed);
                Assert.IsFalse(window.TrySelectPlanet("no-such-world", out error));
                StringAssert.Contains("No planet", error);
            }
            Assert.AreSame(previous, GameState.I);
        }

        [Test]
        public void ClosingWindow_ReleasesItsOwnedPreview()
        {
            Assert.IsTrue(window.TryRefresh("1", 0, 0, 30000, out _));
            var texture = window.Preview;
            Object.DestroyImmediate(window);
            window = null;
            Assert.IsTrue(texture == null);
        }
    }
}
