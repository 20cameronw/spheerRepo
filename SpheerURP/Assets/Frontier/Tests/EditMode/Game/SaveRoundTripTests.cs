using NUnit.Framework;

namespace Frontier.Tests
{
    /// <summary>GameState persistence (JSON file in an isolated folder): a reloaded state matches the saved one.</summary>
    [TestFixture]
    public class SaveRoundTripTests
    {
        SaveSandbox sandbox;
        GameDatabase db;

        [SetUp]
        public void SetUp()
        {
            sandbox = new SaveSandbox();
            db = GameDatabase.Load();
        }

        [TearDown]
        public void TearDown()
        {
            sandbox.Dispose();
        }

        [Test]
        public void SavedState_ReloadsIdentically()
        {
            var a = new GameState(db, false);
            foreach (var resource in db.resources) a.Add(resource, 3, false);
            var pack = db.FindUpgrade("pack");
            Assert.IsNotNull(pack);
            a.DebugSetUpgrade(pack, 2);
            a.AddXp(77);
            var visited = a.universe.Get(db.planets[2].id);
            a.DebugTeleport(visited);
            var warehouse = a.DebugPlace(db.StructureOfKind(StructureKind.Warehouse), 4, 2);
            a.Deposit(warehouse, 0, db.FindResource("wood"), 2, false);
            a.WaveCleared(visited);
            a.Save();
            Assert.IsTrue(System.IO.File.Exists(sandbox.SaveFile), "the save file was not written to the isolated folder");

            var b = new GameState(db, true);
            Assert.AreSame(a.planet, b.planet);
            Assert.AreEqual(a.xp, b.xp);
            Assert.AreEqual(a.totalWavesCleared, b.totalWavesCleared);
            Assert.AreEqual(a.NextWave(visited), b.NextWave(visited));
            Assert.AreEqual(a.Level(pack), b.Level(pack));
            foreach (var resource in db.resources) Assert.AreEqual(a.Count(resource), b.Count(resource), resource.id);
            Assert.AreEqual(1, b.structures.Count);
            Assert.AreSame(warehouse.def, b.structures[0].def);
            Assert.AreEqual(warehouse.slot, b.structures[0].slot);
            Assert.AreEqual(warehouse.level, b.structures[0].level);
            Assert.AreEqual(visited.Id, b.structures[0].planetId, "structures are stored under the canonical planet id");
            var restored = b.StorageFor(b.structures[0]);
            Assert.AreEqual(2, restored.slots[0].count);
            Assert.AreEqual("wood", restored.slots[0].resourceId);
            Assert.AreEqual(SaveSource.Primary, b.LoadReport.source);
            Assert.AreEqual(0, b.LoadReport.migratedFrom);
            Assert.AreEqual(a.stats.worldsLanded, b.stats.worldsLanded);
        }

        [Test]
        public void StartingANewStateWithoutASave_UsesTheStartingContent()
        {
            var state = new GameState(db, true);
            Assert.AreSame(state.universe.Home, state.planet);
            Assert.AreEqual(db.startPlanet.id, state.planet.AuthoredId);
            Assert.AreEqual(SaveSource.NewGame, state.LoadReport.source);
            foreach (var start in db.startingItems) Assert.AreEqual(start.amount, state.Count(start.resource), start.resource.id);
            Assert.AreEqual(0, state.structures.Count);
        }

        [Test]
        public void ResetProgress_RestoresTheStartingContent()
        {
            var state = new GameState(db, false);
            state.AddXp(500);
            state.DebugTeleport(state.universe.Get(db.planets[3].id));
            state.ResetProgress();
            Assert.AreEqual(0, state.xp);
            Assert.AreSame(state.universe.Home, state.planet);
            foreach (var start in db.startingItems) Assert.AreEqual(start.amount, state.Count(start.resource), start.resource.id);
            Assert.IsTrue(System.IO.File.Exists(sandbox.SaveFile), "a reset writes the fresh save right away");
        }
    }
}
