using System.IO;
using NUnit.Framework;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine.TestTools;

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
            a.RecordHarvestRunCompleted();
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
            Assert.AreEqual(a.stats.harvestRunsCompleted, b.stats.harvestRunsCompleted);
        }

        [Test]
        public void RewardedHarvestBonusIsSavedBeforeInventoryChangesAndSurvivesReload()
        {
            var state = new GameState(db, false);
            var wood = db.FindResource("wood");
            Assert.IsNotNull(wood);
            int before = state.Count(wood);
            Assert.AreEqual(3, state.Add(wood, 3, false));

            Assert.IsTrue(state.TryCommitHarvestBonus(wood, 3, out int granted, out string message), message);
            Assert.AreEqual(3, granted);
            Assert.AreEqual(before + 6, state.Count(wood));
            Assert.IsFalse(state.saves.IsDirty);
            var saved = SaveSerializer.Instance.DeserializeV2(File.ReadAllText(sandbox.SaveFile)).Normalize();
            Assert.AreEqual(before + 6, saved.player.inventory.Single(stack => stack.resourceId == wood.id).count);
            Assert.AreEqual(before + 6, new GameState(db, true).Count(wood));
        }

        [Test]
        public void FailedRewardedHarvestBonusWriteLeavesBaseHarvestUntouchedAndCanRetry()
        {
            var state = new GameState(db, false);
            var wood = db.FindResource("wood");
            Assert.IsNotNull(wood);
            int before = state.Count(wood);
            Assert.AreEqual(2, state.Add(wood, 2, false));
            Directory.CreateDirectory(sandbox.SaveFile);
            LogAssert.Expect(UnityEngine.LogType.Warning, new Regex("Frontier save failed:"));

            Assert.IsFalse(state.TryCommitHarvestBonus(wood, 2, out int failedGrant, out string error));
            Assert.AreEqual(0, failedGrant);
            Assert.IsNotEmpty(error);
            Assert.AreEqual(before + 2, state.Count(wood), "A failed bonus commit must not publish the extra items.");

            Directory.Delete(sandbox.SaveFile);
            Assert.IsTrue(state.TryCommitHarvestBonus(wood, 2, out int granted, out string message), message);
            Assert.AreEqual(2, granted);
            Assert.AreEqual(before + 4, state.Count(wood));
            Assert.AreEqual(before + 4, SaveSerializer.Instance.DeserializeV2(File.ReadAllText(sandbox.SaveFile))
                .Normalize().player.inventory.Single(stack => stack.resourceId == wood.id).count);
        }

        [Test]
        public void MapPins_ToggleAndPersistAcrossSaveReload()
        {
            var state = new GameState(db, false);
            PlanetData destination = state.universe.Get(db.planets[2].id);
            Assert.IsNotNull(destination);
            Assert.IsTrue(state.ToggleMapPin(destination));
            Assert.IsTrue(state.RecordMapDiscoveries(new[] { destination.Star }));
            Assert.IsTrue(state.IsMapPinned(destination.Id));
            Assert.IsTrue(state.IsMapDiscovered(destination.Star.Id));
            Assert.IsTrue(state.Save());
            SaveV2 compact = SaveSerializer.Instance.DeserializeV2(System.IO.File.ReadAllText(sandbox.SaveFile));
            Assert.AreEqual(0, compact.mapDiscoveries.Count, "new saves should not repeat individual system IDs");
            Assert.AreEqual(1, compact.mapDiscoverySectors.Count);
            CollectionAssert.Contains(MapDiscoveryBits.Decode(compact.mapDiscoverySectors), destination.Star.Id);

            var loaded = new GameState(db, true);
            Assert.IsTrue(loaded.IsMapPinned(destination.Id));
            Assert.IsTrue(loaded.IsMapDiscovered(destination.Star.Id));
            Assert.IsFalse(loaded.ToggleMapPin(loaded.universe.Get(destination.Id)));
            Assert.IsFalse(loaded.IsMapPinned(destination.Id));
            Assert.IsTrue(loaded.Save());
            Assert.IsFalse(new GameState(db, true).IsMapPinned(destination.Id));
        }

        [Test]
        public void MapDiscoveryBits_MigrateLegacyIdsAndRoundTripSectorIndices()
        {
            var ids = new[]
            {
                new Frontier.Universe.SystemKey(-3, 7, 0).ToString(),
                new Frontier.Universe.SystemKey(-3, 7, 37).ToString(),
                new Frontier.Universe.SystemKey(2, -1, Frontier.Universe.SystemKey.MaxIndex).ToString()
            };
            var encoded = MapDiscoveryBits.Encode(ids);
            Assert.AreEqual(2, encoded.Count, "systems in the same sector share one bitset");
            CollectionAssert.AreEqual(ids.OrderBy(id => id, System.StringComparer.Ordinal), MapDiscoveryBits.Decode(encoded));

            var legacy = new SaveV2 { mapDiscoveries = new System.Collections.Generic.List<string>(ids) }.Normalize();
            Assert.AreEqual(0, legacy.mapDiscoveries.Count);
            CollectionAssert.AreEqual(MapDiscoveryBits.Decode(encoded), MapDiscoveryBits.Decode(legacy.mapDiscoverySectors));
        }

        [Test]
        public void FirstSessionObjectives_AdvanceOnCompletedGameActions()
        {
            var state = new GameState(db, false);
            Assert.IsTrue(state.FirstSessionGuideAvailable);
            Assert.AreEqual(0, FirstSessionObjectives.CurrentStep(state));

            state.DebugPlace(db.StructureOfKind(StructureKind.Lumber), 0);
            Assert.AreEqual(1, FirstSessionObjectives.CurrentStep(state));

            state.RecordHarvestRunCompleted();
            Assert.AreEqual(2, FirstSessionObjectives.CurrentStep(state));

            state.DebugPlace(db.StructureOfKind(StructureKind.Warehouse), 1);
            Assert.AreEqual(3, FirstSessionObjectives.CurrentStep(state));

            state.DebugPlace(db.FindStructure("refinery"), 2);
            state.backpack.Set("wood", 8);
            Assert.IsTrue(state.Craft(db.recipes.Single(recipe => recipe.id == "distill_wood")));
            Assert.AreEqual(4, FirstSessionObjectives.CurrentStep(state));

            state.WaveCleared(state.planet);
            Assert.AreEqual(5, FirstSessionObjectives.CurrentStep(state));

            state.stats.RecordLanding(1.0, true);
            Assert.AreEqual(6, FirstSessionObjectives.CurrentStep(state), "Travel alone no longer completes the game-loop tutorial.");
            state.DebugPlace(db.StructureOfKind(StructureKind.ResearchLab), 3);
            foreach (var resource in db.resources) state.backpack.Set(resource.id, 10000);
            var research = db.research.First(node => node.data.tier == 1 &&
                (node.data.prereqs == null || node.data.prereqs.Length == 0) && node.data.labLevel <= 1);
            Assert.IsTrue(state.Research(research), state.ResearchLockReason(research));
            Assert.AreEqual(7, FirstSessionObjectives.CurrentStep(state));
            state.DebugSetUpgrade(db.FindUpgrade("pack"), 1);
            Assert.AreEqual(FirstSessionObjectives.Count, FirstSessionObjectives.CurrentStep(state));
            state.Save();
            Assert.AreEqual(FirstSessionObjectives.Count, FirstSessionObjectives.CurrentStep(new GameState(db, true)));
        }

        [Test]
        public void FirstSessionGuideDismissal_PersistsAndResetEnablesItAgain()
        {
            var state = new GameState(db, false);
            Assert.IsTrue(state.FirstSessionGuideAvailable);
            state.DismissFirstSessionGuide();
            Assert.IsFalse(state.FirstSessionGuideAvailable);
            Assert.IsTrue(state.Save());

            var loaded = new GameState(db, true);
            Assert.IsFalse(loaded.FirstSessionGuideAvailable);

            loaded.ResetProgress();
            Assert.IsTrue(loaded.FirstSessionGuideAvailable);
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
