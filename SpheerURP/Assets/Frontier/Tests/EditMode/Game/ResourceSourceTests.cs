using System;
using System.Linq;
using NUnit.Framework;

namespace Frontier.Tests
{
    public sealed class ResourceSourceTests
    {
        SaveSandbox sandbox;
        GameDatabase db;
        GameState state;

        [SetUp]
        public void SetUp()
        {
            sandbox = new SaveSandbox();
            db = GameDatabase.Load();
            state = new GameState(db, false);
        }

        [TearDown] public void TearDown() { sandbox.Dispose(); }

        [Test]
        public void MissingLocalGathererRoutesToItsBuildEntryThenItsExistingStructure()
        {
            var wood = db.FindResource("wood");
            var first = state.FindResourceSource(wood);
            Assert.AreEqual(ResourceSourceKind.Build, first.kind);
            Assert.AreEqual(StructureKind.Lumber, first.build.kind);
            state.DebugPlace(first.build, 0);
            var placed = state.FindResourceSource(wood);
            Assert.AreEqual(ResourceSourceKind.Structure, placed.kind);
            Assert.AreEqual(first.build, placed.structure.def);
            Assert.AreEqual(state.planet.Id, placed.structure.planetId);
        }

        [Test]
        public void ProcessedOutputRoutesToStationEvenWhenItsIngredientsAreMissing()
        {
            var station = db.StructureOfKind(StructureKind.Refinery);
            var recipe = db.recipes.First(r => r.stationId == station.id &&
                string.IsNullOrEmpty(r.unlockResearch) && r.stationLevel == 1 && r.tier <= state.PlayerLevel);
            var output = recipe.outputs[0].resource;
            Assert.AreEqual(ResourceSourceKind.Build, state.FindResourceSource(output).kind);
            state.DebugPlace(station, 0);
            state.backpack.Clear();
            var source = state.FindResourceSource(output);
            Assert.AreEqual(ResourceSourceKind.Structure, source.kind);
            Assert.AreEqual(station, source.structure.def);
            Assert.IsTrue(source.recipe.outputs.Any(o => o.resource == output));
        }

        [Test]
        public void OffWorldResourceSelectsARealSupplyingPlanetInsteadOfUnrelatedLocalMine()
        {
            var offWorld = db.resources.First(resource => resource.IsRaw &&
                !Enum.GetValues(typeof(MinigameKind)).Cast<MinigameKind>().Any(game =>
                    state.planet.SpotsFor(game).Any(spot => spot.resource == resource)) &&
                state.universe.AuthoredPlanets.Any(planet => planet.Resources.Contains(resource)));
            var source = state.FindResourceSource(offWorld);
            Assert.AreEqual(ResourceSourceKind.Planet, source.kind, offWorld.id);
            Assert.AreNotEqual(state.planet.Id, source.planet.Id);
            Assert.IsTrue(source.planet.Resources.Contains(offWorld));
        }

        [Test]
        public void ProducersOnOtherWorldsDoNotReplaceTheLocalBuildRoute()
        {
            var home = state.planet;
            var lumber = db.StructureOfKind(StructureKind.Lumber);
            var other = state.universe.AuthoredPlanets.First(p => p.Id != home.Id);
            state.DebugTeleport(other);
            state.DebugPlace(lumber, 0);
            state.DebugTeleport(home);
            Assert.AreEqual(ResourceSourceKind.Build, state.FindResourceSource(db.FindResource("wood")).kind);
        }
    }
}
