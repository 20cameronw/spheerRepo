using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

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
        public void SessionClockProtectsConstructionFromForwardAndBackwardDeviceEdits()
        {
            var source = new FakeClock();
            var state = new GameState(db, false, new SessionClock(source));
            GiveAllResources(state);
            var refinery = state.DebugPlace(state.db.StructureOfKind(StructureKind.Refinery), 0);
            state.DebugSetBuilderJob(refinery, 120);
            DateTime started = source.UtcNow;
            source.SetUtc(started.AddDays(30));
            state.Tick();
            Assert.IsTrue(state.IsUnderConstruction(refinery));
            Assert.AreEqual(120, state.BuilderJobSecondsRemaining(refinery));
            CollectionAssert.AreEqual(new[] { new DateTimeOffset(started.AddSeconds(120)) },
                state.ActiveBuilderCompletionTimes(), "Reminder snapshots use the gameplay deadline, not edited device UTC.");
            source.Advance(60);
            state.Tick();
            Assert.IsTrue(state.IsUnderConstruction(refinery));
            Assert.AreEqual(60, state.BuilderJobSecondsRemaining(refinery));
            source.SetUtc(started.AddDays(-30));
            source.Advance(60);
            state.Tick();
            Assert.IsFalse(state.IsUnderConstruction(refinery));
            Assert.IsTrue(state.IsOperational(refinery));
            Assert.AreEqual(0, state.BusyBuilderCount);
            Assert.IsEmpty(state.ActiveBuilderCompletionTimes(), "Completed jobs must not remain in notification snapshots.");
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
                    candidate.levels[1].requiresReachTier <= state.PlayerLevel && candidate.levels[1].requiresLabLevel == 0 &&
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
        public void ShardStructurePurchaseReplacesMaterialsAndCrewAndPersistsOnlyOneCharge()
        {
            var state = CreateCrewPurchaseState(10000, false);
            state.backpack.Clear();
            var other = state.DebugPlace(db.StructureOfKind(StructureKind.Warehouse), 1);
            state.DebugSetBuilderJob(other, 600);
            Assert.AreEqual(0, state.AvailableBuilderCount);
            var definition = db.StructureOfKind(StructureKind.Refinery);
            int cost = state.StructureShardCost(definition);
            Assert.AreEqual(Math.Max(10, 2 * Economy.ShardCurve.SkipCost(db.shardRules.data,
                state.StructureJobSeconds(definition, 1))), cost);
            int before = state.ShardBalance;
            bool discovered = state.IsAtlasDiscovered("structure", definition.kind.ToString());
            Assert.AreEqual(StructurePurchaseResult.Saved,
                state.PurchaseStructure(definition, 0, cost, out var message), message);
            Assert.IsTrue(state.IsOperational(state.StructureAt(state.planet, 0)));
            Assert.IsFalse(state.IsUnderConstruction(state.StructureAt(state.planet, 0)));
            Assert.AreEqual(1, state.BusyBuilderCount, "Buying does not steal or add a crew.");
            foreach (var resource in db.resources) Assert.AreEqual(0, state.Count(resource));
            int expected = before - cost + (discovered ? 0 : db.shardRules.data.atlasStructureShards);
            Assert.AreEqual(expected, state.ShardBalance);
            Assert.AreEqual(StructurePurchaseResult.Rejected,
                state.PurchaseStructure(definition, 0, cost, out _));
            Assert.AreEqual(expected, state.ShardBalance);
            var loaded = new GameState(db, true, clock);
            Assert.AreEqual(expected, loaded.ShardBalance);
            Assert.IsTrue(loaded.IsOperational(loaded.StructureAt(loaded.planet, 0)));
        }

        [TestCase("balance")]
        [TestCase("quote")]
        [TestCase("level")]
        [TestCase("research")]
        [TestCase("lab")]
        [TestCase("slot")]
        [TestCase("planet")]
        [TestCase("limit")]
        [TestCase("read-only")]
        public void ShardStructurePurchaseRechecksGatesBeforeSpending(string reason)
        {
            var state = CreateCrewPurchaseState(reason == "balance" ? 0 : 10000, false);
            state.backpack.Clear();
            var definition = UnityEngine.Object.Instantiate(db.StructureOfKind(StructureKind.Refinery));
            var original = db.structures;
            db.structures = original.Select(d => d.id == definition.id ? definition : d).ToArray();
            try
            {
                if (reason == "level") definition.unlockTier = state.PlayerLevel + 1;
                if (reason == "research") definition.unlockResearch = "not_researched";
                if (reason == "lab") definition.Row(1).requiresLabLevel = 20;
                if (reason == "planet") definition.kind = StructureKind.SiphonRig;
                if (reason == "limit")
                {
                    definition.maxPerPlanet = 1;
                    state.DebugPlace(definition, 1);
                }
                if (reason == "read-only") state.saves.Store.MarkReadOnly("test");
                int cost = state.StructureShardCost(definition), before = state.ShardBalance;
                Assert.AreEqual(StructurePurchaseResult.Rejected,
                    state.PurchaseStructure(definition, reason == "slot" ? -1 : 0,
                        reason == "quote" ? cost - 1 : cost, out var message));
                Assert.IsNotEmpty(message);
                Assert.AreEqual(before, state.ShardBalance);
                Assert.IsNull(state.StructureAt(state.planet, 0));
            }
            finally
            {
                db.structures = original;
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void ShardStructureSaveFailureIsExplicitAndCanRetrySavingWithoutRepurchasing()
        {
            var state = CreateCrewPurchaseState(10000, false);
            var definition = db.StructureOfKind(StructureKind.Refinery);
            int cost = state.StructureShardCost(definition);
            File.Delete(sandbox.SaveFile);
            Directory.CreateDirectory(sandbox.SaveFile);
            LogAssert.Expect(LogType.Warning, new Regex("Frontier save failed:"));
            Assert.AreEqual(StructurePurchaseResult.PendingSave,
                state.PurchaseStructure(definition, 0, cost, out var message));
            StringAssert.Contains("saving failed", message);
            int balance = state.ShardBalance;
            Assert.AreEqual(StructurePurchaseResult.Rejected,
                state.PurchaseStructure(definition, 0, cost, out _));
            Assert.AreEqual(balance, state.ShardBalance);
            Directory.Delete(sandbox.SaveFile);
            Assert.IsTrue(state.Save());
            var loaded = new GameState(db, true, clock);
            Assert.AreEqual(balance, loaded.ShardBalance);
            Assert.IsTrue(loaded.IsOperational(loaded.StructureAt(loaded.planet, 0)));
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
                candidate.levels[1].buildSeconds > 0 && candidate.levels[1].requiresReachTier <= state.PlayerLevel &&
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
        public void ColonyHubClearanceDiscountUsesCatalogCapacityNotExtraBuilderCount()
        {
            var state = new GameState(db, false, clock);
            var target = state.universe.FindGenerated(p => p.Tier > 0 &&
                state.ObstaclesOn(state.universe.Get(p)).Any(o => o.seconds > 0 && o.cost.Any(line => line.amount >= 20)), 1, 0, 10000);
            Assert.IsNotNull(target);
            state.DebugTeleport(target);
            var obstacle = state.ObstaclesOn(target).First(o => o.seconds > 0 && o.cost.Any(line => line.amount >= 20));
            var originalCost = state.ObstacleClearCost(target, obstacle.pad);
            int hubPad = Enumerable.Range(0, target.BuildingSlots).First(pad => !state.ObstacleAt(target, pad).HasValue);
            var hub = state.DebugPlace(db.StructureOfKind(StructureKind.ColonyHub), hubPad);
            Assert.IsNotNull(hub);

            foreach (int level in new[] { 1, 2, 3, 7, 10 })
            {
                state.DebugSetStructureLevel(hub, level);
                var discountedCost = state.ObstacleClearCost(target, obstacle.pad);
                Assert.AreEqual(originalCost.Count, discountedCost.Count);
                for (int i = 0; i < originalCost.Count; i++)
                {
                    Assert.AreSame(originalCost[i].resource, discountedCost[i].resource);
                    Assert.AreEqual(Frontier.Economy.ObstacleRules.Discounted(originalCost[i].amount, hub.def.CapacityAt(level)),
                        discountedCost[i].amount, "Hub level " + level + " / " + originalCost[i].resource.id);
                }
            }
            Assert.AreEqual(45, hub.def.CapacityAt(10), "The runtime must preserve the authored 45-percent maximum-level discount.");
            hub.record.buildEndUtc = UtcText.Format(clock.UtcNow.AddHours(1));
            state.Notify();
            var unfinishedCost = state.ObstacleClearCost(target, obstacle.pad);
            for (int i = 0; i < originalCost.Count; i++) Assert.AreEqual(originalCost[i].amount, unfinishedCost[i].amount);
            hub.record.buildEndUtc = "";
            state.Notify();

            var paidCost = state.ObstacleClearCost(target, obstacle.pad);
            foreach (var item in paidCost) state.backpack.Set(item.resource.id, item.amount);
            Assert.IsTrue(state.StartObstacleClear(obstacle.pad));
            foreach (var item in paidCost) Assert.AreEqual(0, state.Count(item.resource), item.resource.id);
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

        [TestCase(false, 600)]
        [TestCase(true, 600)]
        [TestCase(false, 10)]
        [TestCase(true, 10)]
        public void FinishingBuilderJobsUsesSkipCurveAndPersistsCompletionOnce(bool upgrade, int seconds)
        {
            var state = CreateBuilderJob(upgrade, seconds, 10000);
            var structure = state.StructureAt(state.planet, 0);
            int price = state.BuilderSkipCost(structure);
            Assert.AreEqual(Frontier.Economy.ShardCurve.SkipCost(db.shardRules.data, seconds), price);
            bool discovered = state.IsAtlasDiscovered("structure", structure.def.kind.ToString());
            int expectedReward = !upgrade && !discovered ? db.shardRules.data.atlasStructureShards : 0;
            int balance = state.ShardBalance;
            Assert.AreEqual(BuilderFinishResult.Saved, state.FinishBuilderJob(structure, price, out var message), message);
            Assert.IsFalse(state.IsBuilderBusy(structure));
            Assert.IsTrue(state.IsOperational(structure));
            Assert.AreEqual(upgrade ? 2 : 1, structure.level);
            Assert.AreEqual(0, state.BusyBuilderCount);
            Assert.AreEqual(balance - price + expectedReward, state.ShardBalance);
            int finishedBalance = state.ShardBalance;
            Assert.AreEqual(BuilderFinishResult.Rejected, state.FinishBuilderJob(structure, price, out _));
            Assert.AreEqual(finishedBalance, state.ShardBalance);

            var restored = new GameState(db, true, clock);
            Assert.AreEqual(upgrade ? 2 : 1, restored.StructureAt(restored.planet, 0).level);
            Assert.AreEqual(0, restored.BusyBuilderCount);
            Assert.AreEqual(finishedBalance, restored.ShardBalance);
        }

        [TestCase("balance")]
        [TestCase("quote")]
        [TestCase("read-only")]
        public void BuilderFinishRejectsInvalidSpendsWithoutChangingTheJob(string reason)
        {
            var state = CreateBuilderJob(false, 600, reason == "balance" ? 0 : 10000);
            var structure = state.StructureAt(state.planet, 0);
            int price = state.BuilderSkipCost(structure);
            Assert.Greater(price, 0);
            if (reason == "read-only") state.saves.Store.MarkReadOnly("Test future save");
            int balance = state.ShardBalance;
            string end = structure.record.buildEndUtc;
            Assert.AreEqual(BuilderFinishResult.Rejected,
                state.FinishBuilderJob(structure, reason == "quote" ? price - 1 : price, out var message));
            Assert.IsNotEmpty(message);
            Assert.AreEqual(balance, state.ShardBalance);
            Assert.AreEqual(end, structure.record.buildEndUtc);
            Assert.AreEqual(1, state.BusyBuilderCount);
        }

        [Test]
        public void BuilderFinishCanCompleteAnOffworldJobWithoutTravelOrAnotherPayment()
        {
            var state = CreateBuilderJob(true, 600, 10000);
            var origin = state.planet;
            var structure = state.StructureAt(origin, 0);
            var destination = state.universe.FindGenerated(p => p.Tier >= 2, 1, 0, 10000);
            Assert.IsNotNull(destination);
            state.DebugTeleport(destination);
            int fuel = state.Count(db.fuel);
            int balance = state.ShardBalance;
            int price = state.BuilderSkipCost(structure);
            Assert.AreEqual(BuilderFinishResult.Saved, state.FinishBuilderJob(structure, price, out var message), message);
            Assert.AreEqual(destination.Id, state.planet.Id);
            Assert.AreEqual(fuel, state.Count(db.fuel));
            Assert.AreEqual(balance - price, state.ShardBalance);
            Assert.AreEqual(2, state.StructureAt(origin, 0).level);
        }

        [Test]
        public void BuilderFinishSaveFailureIsExplicitAndRetryCannotSpendAgain()
        {
            var state = CreateBuilderJob(true, 600, 10000);
            var structure = state.StructureAt(state.planet, 0);
            int price = state.BuilderSkipCost(structure);
            int balance = state.ShardBalance;
            File.Delete(sandbox.SaveFile);
            Directory.CreateDirectory(sandbox.SaveFile);
            LogAssert.Expect(LogType.Warning, new Regex("Frontier save failed:"));
            Assert.AreEqual(BuilderFinishResult.PendingSave, state.FinishBuilderJob(structure, price, out var message));
            StringAssert.Contains("saving failed", message);
            Assert.IsTrue(state.saves.IsDirty);
            Assert.AreEqual(balance - price, state.ShardBalance);
            Assert.AreEqual(2, structure.level);
            Assert.AreEqual(BuilderFinishResult.Rejected, state.FinishBuilderJob(structure, price, out _));
            Assert.AreEqual(balance - price, state.ShardBalance);
            Directory.Delete(sandbox.SaveFile);
            Assert.IsTrue(state.Save());
            var restored = new GameState(db, true, clock);
            Assert.AreEqual(balance - price, restored.ShardBalance);
            Assert.AreEqual(2, restored.StructureAt(restored.planet, 0).level);
        }

        GameState CreateBuilderJob(bool upgrade, int seconds, int shards)
        {
            var state = new GameState(db, false, clock);
            var structure = state.DebugPlace(db.StructureOfKind(StructureKind.Refinery), 0);
            string end = UtcText.Format(clock.UtcNow.AddSeconds(seconds));
            if (upgrade)
            {
                structure.record.upgradeEndUtc = end;
                structure.record.pendingUpgradeLevel = 2;
            }
            else structure.record.buildEndUtc = end;
            Assert.IsTrue(state.Save());
            var save = SaveSerializer.Instance.DeserializeV2(File.ReadAllText(sandbox.SaveFile)).Normalize();
            save.wallet.Clear();
            save.wallet.Add(new WalletEntry { currency = "shards", amount = shards });
            File.WriteAllText(sandbox.SaveFile, SaveSerializer.Instance.Serialize(save));
            return new GameState(db, true, clock);
        }

        [Test]
        public void CrewPurchasesUseAuthoredPricesPersistAndCannotExceedTheCap()
        {
            var state = CreateCrewPurchaseState(10000);
            Assert.AreEqual(2, state.BuilderCount);
            int originalBalance = state.ShardBalance;
            int thirdPrice = db.shardRules.data.thirdBuilderShards;
            int fourthPrice = db.shardRules.data.fourthBuilderShards;
            Assert.AreEqual(thirdPrice, state.NextBuilderPurchaseCost);
            Assert.AreEqual(BuilderPurchaseResult.Saved, state.PurchaseBuilder(2, thirdPrice, out var message), message);
            Assert.AreEqual(3, state.BuilderCount);
            Assert.AreEqual(1, state.PurchasedBuilderCount);
            Assert.AreEqual(originalBalance - thirdPrice, state.ShardBalance);
            Assert.AreEqual(BuilderPurchaseResult.Rejected, state.PurchaseBuilder(2, thirdPrice, out _),
                "A duplicate confirmation must not recruit the next, more expensive crew.");
            Assert.AreEqual(fourthPrice, state.NextBuilderPurchaseCost);
            Assert.AreEqual(BuilderPurchaseResult.Saved, state.PurchaseBuilder(3, fourthPrice, out message), message);
            Assert.AreEqual(4, state.BuilderCount);
            Assert.AreEqual(2, state.PurchasedBuilderCount);
            Assert.AreEqual(-1, state.NextBuilderPurchaseCost);
            Assert.AreEqual(BuilderPurchaseResult.Rejected, state.PurchaseBuilder(4, int.MaxValue, out _));
            Assert.AreEqual(originalBalance - thirdPrice - fourthPrice, state.ShardBalance);

            var loaded = new GameState(db, true, clock);
            Assert.AreEqual(4, loaded.BuilderCount);
            Assert.AreEqual(2, loaded.PurchasedBuilderCount);
            Assert.AreEqual(state.ShardBalance, loaded.ShardBalance);
            loaded.DebugPlace(db.StructureOfKind(StructureKind.ColonyHub), 0, 10);
            Assert.AreEqual(db.shardRules.data.maxBuilders, loaded.BuilderCount, "Hub and purchased crews share the same cap.");
            loaded.ResetProgress();
            Assert.AreEqual(0, loaded.PurchasedBuilderCount);
            Assert.AreEqual(db.shardRules.data.baseBuilders, loaded.BuilderCount);
        }

        [TestCase("balance")]
        [TestCase("quote")]
        [TestCase("read-only")]
        [TestCase("progression")]
        public void CrewPurchasesRejectInvalidOffersWithoutSpending(string reason)
        {
            var state = CreateCrewPurchaseState(reason == "balance" ? 0 : 10000, reason != "progression");
            int balance = state.ShardBalance;
            int owned = state.BuilderCount;
            int price = db.shardRules.data.thirdBuilderShards;
            if (reason == "read-only") state.saves.Store.MarkReadOnly("Test newer save");
            Assert.AreEqual(BuilderPurchaseResult.Rejected,
                state.PurchaseBuilder(owned, reason == "quote" ? price - 1 : price, out var message));
            Assert.IsNotEmpty(message);
            Assert.AreEqual(owned, state.BuilderCount);
            Assert.AreEqual(0, state.PurchasedBuilderCount);
            Assert.AreEqual(balance, state.ShardBalance);
        }

        [Test]
        public void CrewPurchaseSaveFailurePreservesOneGrantAndOneCharge()
        {
            var state = CreateCrewPurchaseState(10000);
            int balance = state.ShardBalance;
            int price = state.NextBuilderPurchaseCost;
            File.Delete(sandbox.SaveFile);
            Directory.CreateDirectory(sandbox.SaveFile);
            LogAssert.Expect(LogType.Warning, new Regex("Frontier save failed:"));
            Assert.AreEqual(BuilderPurchaseResult.PendingSave, state.PurchaseBuilder(2, price, out var message));
            StringAssert.Contains("saving failed", message);
            Assert.IsTrue(state.saves.IsDirty);
            Assert.AreEqual(3, state.BuilderCount);
            Assert.AreEqual(balance - price, state.ShardBalance);
            Assert.AreEqual(BuilderPurchaseResult.Rejected, state.PurchaseBuilder(2, price, out _));
            Assert.AreEqual(balance - price, state.ShardBalance);
            Directory.Delete(sandbox.SaveFile);
            Assert.IsTrue(state.Save());
            var loaded = new GameState(db, true, clock);
            Assert.AreEqual(3, loaded.BuilderCount);
            Assert.AreEqual(balance - price, loaded.ShardBalance);
        }

        [Test]
        public void HubEarnedCrewsBlockUnneededPurchasesAtTheSharedCap()
        {
            var state = CreateCrewPurchaseState(10000);
            state.DebugPlace(db.StructureOfKind(StructureKind.ColonyHub), 0, 10);
            int balance = state.ShardBalance;
            Assert.AreEqual(4, state.BuilderCount);
            Assert.AreEqual(-1, state.NextBuilderPurchaseCost);
            Assert.AreEqual(BuilderPurchaseResult.Rejected, state.PurchaseBuilder(4, int.MaxValue, out _));
            Assert.AreEqual(balance, state.ShardBalance);
        }

        GameState CreateCrewPurchaseState(int shards, bool fieldEngineering = true)
        {
            var state = new GameState(db, false, clock);
            Assert.IsTrue(state.Save());
            var save = SaveSerializer.Instance.DeserializeV2(File.ReadAllText(sandbox.SaveFile)).Normalize();
            save.wallet.Clear();
            save.wallet.Add(new WalletEntry { currency = "shards", amount = shards });
            if (fieldEngineering) save.player.researched.Add("field_engineering");
            File.WriteAllText(sandbox.SaveFile, SaveSerializer.Instance.Serialize(save));
            return new GameState(db, true, clock);
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
