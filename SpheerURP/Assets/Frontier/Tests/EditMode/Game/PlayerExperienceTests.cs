using System;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Frontier.Tests
{
    public sealed class PlayerExperienceTests
    {
        SaveSandbox sandbox;
        GameDatabase db;
        GameState state;
        FakeClock clock;

        [SetUp]
        public void SetUp()
        {
            sandbox = new SaveSandbox();
            db = GameDatabase.Load();
            clock = new FakeClock();
            state = new GameState(db, false, clock);
        }

        [TearDown] public void TearDown() { sandbox.Dispose(); }

        [Test]
        public void StarterShardsDoNotRepeatWhenLoadingAndEssentialsStayInstant()
        {
            Assert.GreaterOrEqual(state.ShardBalance, 20);
            int balance = state.ShardBalance;
            Assert.IsTrue(state.Save());
            Assert.AreEqual(balance, new GameState(db, true, clock).ShardBalance);
            foreach (var kind in new[] { StructureKind.Mine, StructureKind.Lumber, StructureKind.Refinery, StructureKind.Warehouse })
                Assert.AreEqual(0, state.StructureJobSeconds(db.StructureOfKind(kind), 1));
            var lab = state.DebugPlace(db.StructureOfKind(StructureKind.ResearchLab), 0);
            state.DebugSetBuilderJob(lab, state.StructureJobSeconds(lab.def, 1));
            int price = state.BuilderSkipCost(lab);
            Assert.Greater(price, 0);
            Assert.Less(price, 20);
            Assert.AreEqual(BuilderFinishResult.Saved, state.FinishBuilderJob(lab, price, out _));
            Assert.IsTrue(state.IsOperational(lab));
        }

        [Test]
        public void CosmeticPurchaseAndOwnershipPersistAndReequippingIsFree()
        {
            state.DebugSetShards(10000);
            int price = state.BuildingThemePrice("galaxy");
            Assert.Greater(price, 0);
            Assert.IsFalse(state.PurchaseBuildingTheme("galaxy", price - 1, out _));
            Assert.AreEqual(10000, state.ShardBalance);
            Assert.IsTrue(state.PurchaseBuildingTheme("galaxy", price, out _));
            var loaded = new GameState(db, true, clock);
            Assert.AreEqual("galaxy", loaded.BuildingTheme);
            Assert.IsTrue(loaded.OwnsBuildingTheme("galaxy"));
            Assert.AreEqual(10000 - price, loaded.ShardBalance);
            Assert.IsTrue(loaded.PurchaseBuildingTheme("original", 0, out _));
            Assert.IsTrue(loaded.PurchaseBuildingTheme("galaxy", 0, out _));
            Assert.AreEqual(10000 - price, loaded.ShardBalance);
            Assert.IsFalse(loaded.PurchaseBuildingTheme("missing", 10000, out _));
        }

        [Test]
        public void CosmeticsLeaveCombinedCombatAndHarvestStatsUnchanged()
        {
            state.DebugSetShards(10000);
            var turret = state.DebugPlace(db.StructureOfKind(StructureKind.Gunner), 0, 5);
            var stats = new[] { state.TurretPower(turret), state.TurretFireRate, state.TapDamage,
                state.ToolMultiplier(MinigameKind.Mine), state.ReactorMax };
            Assert.IsTrue(state.PurchaseBuildingTheme("cyan", 10000, out _));
            CollectionAssert.AreEqual(stats, new[] { state.TurretPower(turret), state.TurretFireRate,
                state.TapDamage, state.ToolMultiplier(MinigameKind.Mine), state.ReactorMax });
        }

        [Test]
        public void CosmeticFailuresNeverSpendShardsOrEquipUnownedPaint()
        {
            state.DebugSetShards(0);
            Assert.IsFalse(state.PurchaseBuildingTheme("galaxy", 10000, out _));
            state.DebugSetShards(10000);
            Directory.CreateDirectory(sandbox.SaveFile);
            LogAssert.Expect(LogType.Warning, new Regex("Frontier save failed:"));
            Assert.IsFalse(state.PurchaseBuildingTheme("galaxy", 10000, out _));
            Assert.AreEqual(10000, state.ShardBalance);
            Assert.AreEqual("original", state.BuildingTheme);
            Assert.IsFalse(state.OwnsBuildingTheme("galaxy"));
            Directory.Delete(sandbox.SaveFile);
            state.saves.Store.MarkReadOnly("Test future save");
            Assert.IsFalse(state.PurchaseBuildingTheme("galaxy", 10000, out _));
            Assert.AreEqual(10000, state.ShardBalance);
        }

        [Test]
        public void DebugControlsPersistIndividualResearchToolInventoryAndUpgradeJobs()
        {
            var node = db.research.Last();
            state.DebugSetResearch(node, true);
            var ladder = db.toolLadders.First(item => item.data.kind == Frontier.Economy.ToolKind.Pick);
            int max = ladder.data.tiers.Max(row => row.tier);
            state.DebugSetToolTier(ladder, max);
            state.DebugSetResource(db.scrap, 12345);
            state.DebugSetResource(db.fuel, 456);
            state.DebugSetShards(987);
            state.DebugSetXp(12345);
            var turret = state.DebugPlace(db.StructureOfKind(StructureKind.Gunner), 0, 2);
            state.DebugSetBuilderJob(turret, 3600, 3);
            Assert.IsTrue(state.Save());
            var loaded = new GameState(db, true, clock);
            Assert.IsTrue(loaded.IsResearched(node.data.id));
            Assert.AreEqual(max, loaded.ToolTier(ladder));
            Assert.AreEqual(12345, loaded.Count(db.scrap));
            Assert.AreEqual(456, loaded.Count(db.fuel));
            Assert.AreEqual(987, loaded.ShardBalance);
            Assert.AreEqual(12345, loaded.xp);
            var job = loaded.StructureAt(loaded.planet, 0);
            Assert.IsTrue(loaded.IsUpgradeInProgress(job));
            Assert.AreEqual(3600, loaded.BuilderJobSecondsRemaining(job));
            loaded.DebugFinishAllJobs();
            Assert.AreEqual(3, job.level);
            Assert.AreEqual(0, loaded.BusyBuilderCount);
        }

        [Test]
        public void DebugClearanceTimersKeepJobAndBuilderAssignmentInSync()
        {
            var target = state.universe.FindGenerated(planet => planet.Tier > 0 &&
                state.ObstaclesOn(state.universe.Get(planet)).Any(item => item.seconds > 0), 1, 0, 10000);
            Assert.IsNotNull(target);
            state.DebugTeleport(target);
            var obstacle = state.ObstaclesOn(state.planet).First(item => item.seconds > 0);
            foreach (var cost in state.ObstacleClearCost(state.planet, obstacle.pad))
                state.DebugSetResource(cost.resource, cost.amount);
            Assert.IsTrue(state.StartObstacleClear(obstacle.pad));
            state.DebugSetObstacleTimer(state.planet, obstacle.pad, 3600);
            Assert.IsTrue(state.Save());
            var loaded = new GameState(db, true, clock);
            Assert.AreEqual(3600, loaded.ObstacleClearSecondsRemaining(obstacle.pad));
            Assert.AreEqual(1, loaded.BusyBuilderCount);
            loaded.DebugFinishAllJobs();
            Assert.AreEqual(0, loaded.ObstacleClearSecondsRemaining(obstacle.pad));
            Assert.IsFalse(loaded.ObstacleAt(loaded.planet, obstacle.pad).HasValue);
            Assert.AreEqual(0, loaded.BusyBuilderCount);
        }

        [Test]
        public void DebugCanLowerSharedMiningTierEvenWithHigherLegacyDrillProgress()
        {
            var pick = db.toolLadders.First(item => item.data.kind == Frontier.Economy.ToolKind.Pick);
            var drill = db.toolLadders.First(item => item.data.kind == Frontier.Economy.ToolKind.Drill);
            Assert.IsTrue(state.TryCreateCloudSaveSnapshot(out var save, out _));
            save.player.toolTiers.Add(new IdLevel { id = drill.data.id, level = 4 });
            Assert.IsTrue(state.TryRestoreCloudSave(save, out _));
            Assert.AreEqual(4, state.ToolTier(pick));
            state.DebugSetToolTier(pick, 2);
            Assert.AreEqual(2, state.ToolTier(pick));
            Assert.AreEqual(2, state.ToolTier(drill));
        }
    }
}
