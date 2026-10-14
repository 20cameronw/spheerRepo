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
        public void CloudRestore_PreservesAndCanUndoThePreviousDeviceSave()
        {
            var state = new GameState(db, false);
            state.AddXp(20);
            Assert.IsTrue(state.Save());
            Assert.IsTrue(state.TryCreateCloudSaveSnapshot(out SaveV2 cloud, out string snapshotError), snapshotError);
            cloud.player.xp = 7;
            cloud.worlds.Add(new PlanetDelta
            {
                planetId = state.planet.Id,
                structures = new System.Collections.Generic.List<StructureRecord> { null }
            });

            Assert.IsTrue(state.TryRestoreCloudSave(cloud, out string restoreMessage), restoreMessage);
            Assert.AreEqual(7, state.xp);
            Assert.IsTrue(state.HasCloudRestoreBackup);
            CollectionAssert.Contains(state.LoadReport.warnings,
                "dropped an empty structure record on " + state.planet.Id);

            string backupPath = sandbox.SaveFile + ".restore.bak";
            SaveV2 retained = SaveSerializer.Instance.DeserializeV2(
                System.IO.File.ReadAllText(backupPath)).Normalize();
            Assert.AreEqual(20, retained.player.xp);

            state.AddXp(1);
            Assert.IsTrue(state.Save());
            retained = SaveSerializer.Instance.DeserializeV2(
                System.IO.File.ReadAllText(backupPath)).Normalize();
            Assert.AreEqual(20, retained.player.xp, "ordinary writes must keep the explicit pre-restore backup");

            Assert.IsTrue(state.TryRestorePreviousLocalSave(out string undoMessage), undoMessage);
            Assert.AreEqual(20, state.xp);
            retained = SaveSerializer.Instance.DeserializeV2(
                System.IO.File.ReadAllText(backupPath)).Normalize();
            Assert.AreEqual(8, retained.player.xp, "undo retains the save it replaced as the next one-deep restore point");
        }

        [Test]
        public void CloudRestore_RejectsFutureSchemaWithoutChangingTheDeviceSave()
        {
            var state = new GameState(db, false);
            state.AddXp(31);
            Assert.IsTrue(state.Save());
            string before = System.IO.File.ReadAllText(sandbox.SaveFile);
            var future = new SaveV2 { schemaVersion = SaveSchema.Current + 1 };

            Assert.IsFalse(state.TryRestoreCloudSave(future, out string message));
            StringAssert.Contains("schema", message);
            Assert.AreEqual(before, System.IO.File.ReadAllText(sandbox.SaveFile));
            Assert.IsFalse(state.HasCloudRestoreBackup);
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
        public void AtlasResourceDiscovery_AwardsOnceAndSurvivesSaveReload()
        {
            if (!db.UsesImportedEconomy) Assert.Ignore("Imported shard rules are unavailable");
            var state = new GameState(db, false);
            var resource = db.FindResource("steel");
            Assert.IsNotNull(resource);
            int before = state.ShardBalance;

            Assert.AreEqual(1, state.Add(resource, 1, false));
            Assert.IsTrue(state.IsAtlasDiscovered("resource", resource.id));
            Assert.AreEqual(before + db.shardRules.data.atlasResourceShards, state.ShardBalance);
            Assert.IsFalse(state.IsAtlasDiscovered("world", state.planet.Id), "visiting a world is not a catalog discovery");
            Assert.IsTrue(state.Save());

            var loaded = new GameState(db, true);
            Assert.IsTrue(loaded.IsAtlasDiscovered("resource", resource.id));
            Assert.AreEqual(state.ShardBalance, loaded.ShardBalance);
            Assert.AreEqual(1, loaded.Add(resource, 1, false));
            Assert.AreEqual(state.ShardBalance, loaded.ShardBalance, "reacquiring a discovered resource does not pay twice");
        }

        [Test]
        public void AtlasBackfill_MarksPreviouslyOwnedResourcesWithoutRetroactiveReward()
        {
            var state = new GameState(db, false);
            var resource = db.FindResource("steel");
            Assert.IsNotNull(resource);
            state.backpack.Set(resource.id, 2);
            int balance = state.ShardBalance;
            Assert.IsTrue(state.Save());

            var loaded = new GameState(db, true);
            Assert.IsTrue(loaded.IsAtlasDiscovered("resource", resource.id));
            Assert.AreEqual(balance, loaded.ShardBalance);
            loaded.Add(resource, 1, false);
            Assert.AreEqual(balance, loaded.ShardBalance, "an existing save's known inventory is not paid retroactively");
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
