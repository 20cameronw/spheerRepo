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
            a.planet = db.planets[2];
            var warehouse = new PlacedStructure { planetId = a.planet.id, def = db.StructureOfKind(StructureKind.Warehouse), slot = 4, level = 2 };
            a.structures.Add(warehouse);
            a.Deposit(warehouse, 0, db.FindResource("wood"), 2, false);
            a.WaveCleared(db.planets[2]);
            a.Save();
            Assert.IsTrue(System.IO.File.Exists(sandbox.SaveFile), "the save file was not written to the isolated folder");

            var b = new GameState(db, true);
            Assert.AreSame(a.planet, b.planet);
            Assert.AreEqual(a.xp, b.xp);
            Assert.AreEqual(a.totalWavesCleared, b.totalWavesCleared);
            Assert.AreEqual(a.NextWave(db.planets[2]), b.NextWave(db.planets[2]));
            Assert.AreEqual(a.Level(pack), b.Level(pack));
            foreach (var resource in db.resources) Assert.AreEqual(a.Count(resource), b.Count(resource), resource.id);
            Assert.AreEqual(1, b.structures.Count);
            Assert.AreSame(warehouse.def, b.structures[0].def);
            Assert.AreEqual(warehouse.slot, b.structures[0].slot);
            Assert.AreEqual(warehouse.level, b.structures[0].level);
            var restored = b.StorageFor(b.structures[0]);
            Assert.AreEqual(2, restored.slots[0].count);
            Assert.AreEqual("wood", restored.slots[0].resourceId);
        }

        [Test]
        public void StartingANewStateWithoutASave_UsesTheStartingContent()
        {
            var state = new GameState(db, true);
            Assert.AreSame(db.startPlanet, state.planet);
            foreach (var start in db.startingItems) Assert.AreEqual(start.amount, state.Count(start.resource), start.resource.id);
            Assert.AreEqual(0, state.structures.Count);
        }

        [Test]
        public void ResetProgress_RestoresTheStartingContent()
        {
            var state = new GameState(db, false);
            state.AddXp(500);
            state.planet = db.planets[3];
            state.ResetProgress();
            Assert.AreEqual(0, state.xp);
            Assert.AreSame(db.startPlanet, state.planet);
            foreach (var start in db.startingItems) Assert.AreEqual(start.amount, state.Count(start.resource), start.resource.id);
        }
    }
}
