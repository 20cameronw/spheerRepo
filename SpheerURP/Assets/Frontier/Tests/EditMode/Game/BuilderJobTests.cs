using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    [TestFixture]
    public class BuilderJobTests
    {
        SaveSandbox sandbox;
        GameDatabase db;
        FakeClock clock;

        [SetUp]
        public void SetUp()
        {
            sandbox = new SaveSandbox();
            db = GameDatabase.Load();
            clock = new FakeClock();
        }

        [TearDown]
        public void TearDown()
        {
            sandbox.Dispose();
        }

        [Test]
        public void ConstructionOccupiesCrewAndActivatesAfterPersistedTimer()
        {
            if (!db.UsesImportedEconomy) Assert.Ignore("Imported build-time content is unavailable");
            var state = new GameState(db, false, clock);
            GiveAllResources(state);
            StructureSO refinery = state.db.StructureOfKind(StructureKind.Refinery);
            int originalDuration = refinery.Row(1).buildSeconds;
            refinery.Row(1).buildSeconds = 5;
            try
            {
                int duration = state.StructureJobSeconds(refinery, 1);
                Assert.Greater(duration, 0, "the timer uses the authored catalog field");
                Assert.IsTrue(state.Place(refinery, 0));
                var placed = state.StructureAt(state.planet, 0);
                Assert.IsTrue(state.IsUnderConstruction(placed));
                Assert.IsFalse(state.IsOperational(placed));
                Assert.IsNull(state.StructureOfKind(state.planet, refinery.kind));
                Assert.AreEqual(1, state.BusyBuilderCount);
                Assert.AreEqual(0, state.AvailableBuilderCount);

                var second = state.db.structures.First(candidate => candidate != null && candidate != refinery &&
                    candidate.levels != null && candidate.levels.Count > 1 && candidate.levels[1].buildSeconds > 0 &&
                    candidate.levels[1].requiresReachTier <= state.ReachTier && candidate.levels[1].requiresLabLevel == 0 &&
                    string.IsNullOrEmpty(candidate.unlockResearch));
                var secondPlaced = state.DebugPlace(second, 1);
                Assert.IsNotNull(secondPlaced);
                Assert.AreEqual("All builder crews are busy", state.StructureUpgradeLockReason(secondPlaced),
                    "the sole builder cannot start an upgrade while constructing another structure");

                Assert.IsTrue(state.Save());
                var loaded = new GameState(db, true, clock);
                placed = loaded.StructureAt(loaded.planet, 0);
                Assert.IsTrue(loaded.IsUnderConstruction(placed), "the remaining job is restored from the save");

                clock.Advance(duration);
                loaded.Tick();
                Assert.IsFalse(loaded.IsUnderConstruction(placed));
                Assert.IsTrue(loaded.IsOperational(placed));
                Assert.AreEqual(0, loaded.BusyBuilderCount);
                Assert.AreSame(placed, loaded.StructureOfKind(loaded.planet, refinery.kind));
            }
            finally
            {
                refinery.Row(1).buildSeconds = originalDuration;
            }
        }

        [Test]
        public void AtlasStructureDiscovery_IsRewardedOnlyWhenConstructionCompletes()
        {
            if (!db.UsesImportedEconomy) Assert.Ignore("Imported build-time content is unavailable");
            var state = new GameState(db, false, clock);
            GiveAllResources(state);
            StructureSO refinery = db.FindStructure("refinery");
            int originalDuration = refinery.Row(1).buildSeconds;
            refinery.Row(1).buildSeconds = 5;
            try
            {
                int before = state.ShardBalance;
                Assert.IsTrue(state.Place(refinery, 0));
                Assert.IsFalse(state.IsAtlasDiscovered("structure", refinery.kind.ToString()));
                Assert.AreEqual(before, state.ShardBalance, "an unfinished structure is not a discovery");

                Assert.IsTrue(state.Save());
                clock.Advance(5);
                var loaded = new GameState(db, true, clock);
                Assert.IsTrue(loaded.IsAtlasDiscovered("structure", refinery.kind.ToString()));
                Assert.AreEqual(before + db.shardRules.data.atlasStructureShards, loaded.ShardBalance,
                    "an offline construction completion is rewarded once when the save recovers it");
            }
            finally
            {
                refinery.Row(1).buildSeconds = originalDuration;
            }
        }

        [Test]
        public void MegaprojectStages_AreCountedOnRecoveryAndNotCountedTwice()
        {
            var state = new GameState(db, false, clock);
            StructureSO terraformer = db.StructureOfKind(StructureKind.Terraformer);
            Assert.IsNotNull(terraformer);
            var placed = state.DebugPlace(terraformer, 0);
            placed.record.buildEndUtc = UtcText.Format(clock.UtcNow.AddSeconds(-1));
            Assert.IsTrue(state.Save());

            var built = new GameState(db, true, clock);
            Assert.AreEqual(1, built.stats.megaprojectStages);
            var recoveredSave = JsonUtility.FromJson<SaveV2>(File.ReadAllText(sandbox.SaveFile));
            Assert.AreEqual(1, recoveredSave.stats.megaprojectStages,
                "offline completion and its score are persisted immediately");
            placed = built.StructureAt(built.planet, 0);
            Assert.IsFalse(built.IsUnderConstruction(placed));

            placed.record.upgradeEndUtc = UtcText.Format(clock.UtcNow.AddSeconds(-1));
            placed.record.pendingUpgradeLevel = 2;
            Assert.IsTrue(built.Save());

            var upgraded = new GameState(db, true, clock);
            Assert.AreEqual(2, upgraded.stats.megaprojectStages);
            Assert.AreEqual(2, upgraded.StructureAt(upgraded.planet, 0).level);
            Assert.IsTrue(upgraded.Save());

            var reloaded = new GameState(db, true, clock);
            Assert.AreEqual(2, reloaded.stats.megaprojectStages,
                "a completed construction or upgrade is counted only once");
        }

        [Test]
        public void UpgradeKeepsCurrentLevelUntilTimerCompletes()
        {
            if (!db.UsesImportedEconomy) Assert.Ignore("Imported upgrade-time content is unavailable");
            var state = new GameState(db, false, clock);
            GiveAllResources(state);
            StructureSO definition = db.structures.FirstOrDefault(candidate => candidate != null &&
                candidate.Implemented && candidate.levels != null && candidate.levels.Count > 1 &&
                candidate.levels[1].buildSeconds > 0 && candidate.levels[1].requiresReachTier <= state.ReachTier &&
                candidate.levels[1].requiresLabLevel == 0 && string.IsNullOrEmpty(candidate.unlockResearch));
            Assert.IsNotNull(definition, "catalog contains an available timed upgrade");
            var placed = state.DebugPlace(definition, 0);
            Assert.IsNotNull(placed);
            Assert.IsTrue(state.CanUpgradeStructure(placed), state.StructureUpgradeLockReason(placed));
            int duration = state.StructureJobSeconds(definition, 2);

            Assert.IsTrue(state.UpgradeStructure(placed));
            Assert.AreEqual(1, placed.level, "upgrade benefits do not apply until completion");
            Assert.IsTrue(state.IsUpgradeInProgress(placed));
            Assert.IsTrue(state.IsOperational(placed), "the existing structure remains usable during an upgrade");
            Assert.AreEqual(2, placed.record.pendingUpgradeLevel);

            clock.Advance(duration);
            state.Tick();
            Assert.AreEqual(2, placed.level);
            Assert.IsFalse(state.IsUpgradeInProgress(placed));
            Assert.AreEqual(0, placed.record.pendingUpgradeLevel);
            Assert.AreEqual(0, state.BusyBuilderCount);
        }

        [Test]
        public void InvalidSavedBuilderTimerIsClearedWithWarning()
        {
            var state = new GameState(db, false, clock);
            var placed = state.DebugPlace(db.StructureOfKind(StructureKind.Refinery), 0);
            placed.record.buildEndUtc = "not-a-utc-timestamp";
            Assert.IsTrue(state.Save());

            var loaded = new GameState(db, true, clock);
            var restored = loaded.StructureAt(loaded.planet, 0);
            Assert.IsFalse(loaded.IsUnderConstruction(restored));
            Assert.AreEqual(0, loaded.BusyBuilderCount);
            CollectionAssert.Contains(loaded.LoadReport.warnings,
                "cleared an invalid builder timer for '" + restored.def.id + "' on " + loaded.planet.Id);
        }

        [Test]
        public void LegacyContentBuildsRemainImmediate()
        {
            var legacy = GameDatabase.LoadLegacy();
            var state = new GameState(legacy, false, clock);
            GiveAllResources(state);
            var definition = legacy.StructureOfKind(StructureKind.Refinery);
            Assert.AreEqual(0, state.StructureJobSeconds(definition, 1));
            Assert.IsTrue(state.Place(definition, 0));
            var placed = state.StructureAt(state.planet, 0);
            Assert.IsTrue(state.IsOperational(placed));
            Assert.AreEqual(0, state.BusyBuilderCount);
        }

        [Test]
        public void ColonyHubAddsBuildersAtCatalogMilestonesAndStaysResearchGated()
        {
            if (!db.UsesImportedEconomy) Assert.Ignore("Imported builder content is unavailable");
            var state = new GameState(db, false, clock);
            var hub = db.StructureOfKind(StructureKind.ColonyHub);
            Assert.IsNotNull(hub);
            Assert.IsTrue(hub.Implemented);

            var tierThreeWorld = state.universe.FindGenerated(planet => planet.Tier >= 3, 1, 0, 10000);
            Assert.IsNotNull(tierThreeWorld);
            state.DebugTeleport(tierThreeWorld);
            Assert.AreEqual("Requires research: colony charter", state.BuildLockReason(hub));

            var placed = state.DebugPlace(hub, 0);
            Assert.IsNotNull(placed);
            int baseline = state.BuilderCount;
            state.DebugSetStructureLevel(placed, 3);
            Assert.AreEqual(Math.Min(db.shardRules.data.maxBuilders, baseline + 1), state.BuilderCount);
            state.DebugSetStructureLevel(placed, 7);
            Assert.AreEqual(Math.Min(db.shardRules.data.maxBuilders, baseline + 2), state.BuilderCount);
        }

        [Test]
        public void ObstacleClearJobsRecoverAndPayXpOnce_OverflowSalvageCanBeClaimed()
        {
            if (!db.UsesImportedEconomy) Assert.Ignore("Imported obstacle content is unavailable");
            var state = new GameState(db, false, clock);
            GiveAllResources(state);
            PlanetData target = state.universe.FindGenerated(p => p.Tier > 0 &&
                state.ObstaclesOn(state.universe.Get(p)).Any(o => o.seconds > 0), 1, 0, 10000);
            Assert.IsNotNull(target, "the generated universe contains timed surface obstructions");
            state.DebugTeleport(target);
            var obstacle = state.ObstaclesOn(target).First(o => o.seconds > 0);
            int duration = obstacle.seconds;
            int originalXp = state.xp;
            Assert.IsTrue(state.StartObstacleClear(obstacle.pad));
            Assert.AreEqual(1, state.BusyBuilderCount);
            Assert.AreEqual(0, state.AvailableBuilderCount);
            Assert.IsFalse(state.Place(db.StructureOfKind(StructureKind.Refinery), obstacle.pad),
                "a structure cannot be placed on a pad until its obstruction is removed");
            Assert.IsTrue(state.Save());

            var loaded = new GameState(db, true, clock);
            Assert.AreEqual(1, loaded.BusyBuilderCount, "the crew is restored with the paid job");
            clock.Advance(duration);
            loaded.Tick();
            Assert.IsNull(loaded.ObstacleAt(target, obstacle.pad));
            Assert.AreEqual(originalXp + obstacle.xp, loaded.xp);
            Assert.AreEqual(0, loaded.BusyBuilderCount);
            Assert.AreEqual(1, loaded.ObstacleRewardsOn(target).Count,
                "backpack overflow is retained as a claimable planet reward");
            int shards = loaded.ShardBalance;
            loaded.Tick();
            Assert.AreEqual(originalXp + obstacle.xp, loaded.xp, "repeated ticks never pay the same obstacle twice");
            Assert.AreEqual(shards, loaded.ShardBalance, "pod shards are paid at most once");
            Assert.IsTrue(loaded.Save());

            var restored = new GameState(db, true, clock);
            Assert.AreEqual(1, restored.ObstacleRewardsOn(target).Count, "overflow survives save/load");
            restored.backpack.Clear();
            Assert.IsTrue(restored.ClaimObstacleReward(target, obstacle.pad));
            Assert.AreEqual(obstacle.reward.amount, restored.Count(db.FindResource(obstacle.reward.resourceId)));
            Assert.AreEqual(0, restored.ObstacleRewardsOn(target).Count);
        }

        [Test]
        public void ObstacleTimerCanBeSkippedWithPersistedShards()
        {
            if (!db.UsesImportedEconomy) Assert.Ignore("Imported obstacle content is unavailable");
            var state = new GameState(db, false, clock);
            GiveAllResources(state);
            PlanetData target = state.universe.FindGenerated(p => p.Tier >= 4 &&
                state.ObstaclesOn(state.universe.Get(p)).Any(o => o.seconds > 60), 1, 0, 10000);
            Assert.IsNotNull(target, "the generated universe contains a skippable clearance timer");
            state.DebugTeleport(target);
            Assert.IsTrue(state.Save());

            SaveV2 save = SaveSerializer.Instance.DeserializeV2(File.ReadAllText(sandbox.SaveFile)).Normalize();
            save.wallet.Add(new WalletEntry { currency = "shards", amount = 10000 });
            File.WriteAllText(sandbox.SaveFile, SaveSerializer.Instance.Serialize(save));
            var loaded = new GameState(db, true, clock);
            var obstacle = loaded.ObstaclesOn(target).First(o => o.seconds > 60);
            Assert.IsTrue(loaded.StartObstacleClear(obstacle.pad));
            int before = loaded.ShardBalance;
            int price = loaded.ObstacleSkipCost(obstacle.pad);
            Assert.Greater(price, 0);
            Assert.IsTrue(loaded.SkipObstacleClear(obstacle.pad));
            Assert.AreEqual(before - price, loaded.ShardBalance);
            Assert.IsNull(loaded.ObstacleAt(target, obstacle.pad));
            Assert.AreEqual(0, loaded.BusyBuilderCount);
        }

        static void GiveAllResources(GameState state)
        {
            foreach (var resource in state.db.resources)
                if (resource != null) state.backpack.Set(resource.id, 10000000);
        }

    }
}
