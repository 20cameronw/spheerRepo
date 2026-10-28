using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Frontier.Economy;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Frontier.Tests
{
    public class ObjectiveProgressionTests
    {
        SaveSandbox sandbox;
        GameDatabase db;
        [SetUp] public void SetUp() { sandbox = new SaveSandbox(); db = GameDatabase.Load(); }
        [TearDown] public void TearDown() { sandbox.Dispose(); }

        [Test]
        public void AuthoredCatalog_IsValid_Unique_AndBoundedByExistingMilestoneBudget()
        {
            var catalog = Resources.Load<ObjectiveCatalogSO>("Frontier/ObjectiveCatalog");
            Assert.IsNotNull(catalog);
            CollectionAssert.IsEmpty(catalog.Validate(db));
            Assert.AreEqual(26, catalog.entries.Length);
            Assert.AreEqual(26, catalog.entries.Select(x => x.id).Distinct().Count());
            Assert.AreEqual(275, catalog.entries.Sum(x => AchievementRewards.Amount(x.id, db.shardRules.data)));
            Assert.AreEqual(12, catalog.entries.Count(x => AchievementRewards.TrySize(x.id, out var size) && size == AchievementSize.Small));
            Assert.AreEqual(9, catalog.entries.Count(x => AchievementRewards.TrySize(x.id, out var size) && size == AchievementSize.Medium));
            Assert.AreEqual(5, catalog.entries.Count(x => AchievementRewards.TrySize(x.id, out var size) && size == AchievementSize.Large));
            Assert.AreEqual(ObjectiveRoute.Ship, catalog.entries.Single(x => x.id == "jump_range_1").route);
            Assert.AreEqual(ObjectiveRoute.Ship, catalog.entries.Single(x => x.id == "first_hazard_plating").route);
        }

        [Test]
        public void HarvestCompletion_ClaimsOnce_AndCurrencyAndLedgerPersistTogether()
        {
            var state = new GameState(db, false);
            int before = state.ShardBalance;
            Assert.AreEqual(ObjectiveClaimResult.Rejected, state.ClaimObjective("first_harvest", out _));
            state.RecordHarvestRunCompleted();
            Assert.IsTrue(state.ObjectiveCompleted("first_harvest"));
            Assert.AreEqual(before, state.ShardBalance, "Completion alone must not pay.");
            Assert.AreEqual(ObjectiveClaimResult.Saved, state.ClaimObjective("first_harvest", out _));
            Assert.AreEqual(before + 5, state.ShardBalance);
            Assert.AreEqual(ObjectiveClaimResult.Rejected, state.ClaimObjective("first_harvest", out _));
            var restored = new GameState(db, true);
            Assert.IsTrue(restored.ObjectiveClaimed("first_harvest"));
            Assert.AreEqual(before + 5, restored.ShardBalance);
            Assert.AreEqual(ObjectiveClaimResult.Rejected, restored.ClaimObjective("first_harvest", out _));
            restored.ResetProgress();
            Assert.IsFalse(restored.ObjectiveCompleted("first_harvest"));
            Assert.IsFalse(restored.ObjectiveClaimed("first_harvest"));
        }

        [Test]
        public void UnclaimedCompletion_SurvivesLossOfItsEvidence_AndReload()
        {
            var state = new GameState(db, false);
            var project = state.DebugPlace(db.StructureOfKind(StructureKind.Terraformer), 0, 3);
            Assert.IsTrue(state.ObjectiveCompleted("mega_stage_3"));
            state.world.Remove(state.planet.Id, project.record);
            state.Notify();
            state.Save();
            var restored = new GameState(db, true);
            Assert.IsTrue(restored.ObjectiveCompleted("mega_stage_3"));
            Assert.IsFalse(restored.ObjectiveClaimed("mega_stage_3"));
            int before = restored.ShardBalance;
            Assert.AreEqual(ObjectiveClaimResult.Saved, restored.ClaimObjective("mega_stage_3", out _));
            Assert.AreEqual(before + 5, restored.ShardBalance);
        }

        [Test]
        public void OlderSave_BaselinesProvenGoals_WithoutRetroactiveShards_AndFutureGoalsStillPay()
        {
            var state = new GameState(db, false);
            state.RecordHarvestRunCompleted();
            state.DebugTeleport(state.universe.Get(db.planets[1].id));
            state.Save();
            var save = JsonUtility.FromJson<SaveV2>(File.ReadAllText(sandbox.SaveFile));
            save.objectivesInitialized = false;
            save.objectives.Clear();
            File.WriteAllText(sandbox.SaveFile, JsonUtility.ToJson(save));
            var restored = new GameState(db, true);
            Assert.IsTrue(restored.ObjectiveClaimed("first_harvest"));
            Assert.IsTrue(restored.ObjectiveClaimed("first_hop"));
            Assert.AreEqual(state.ShardBalance, restored.ShardBalance);
            Assert.AreEqual(ObjectiveClaimResult.Rejected, restored.ClaimObjective("first_harvest", out _));
            restored.stats.RecordWaveCleared();
            restored.Notify();
            int before = restored.ShardBalance;
            Assert.AreEqual(ObjectiveClaimResult.Saved, restored.ClaimObjective("first_wave", out _));
            Assert.AreEqual(before + 5, restored.ShardBalance);
            Assert.IsTrue(new GameState(db, true).ObjectiveClaimed("first_wave"));
        }

        [Test]
        public void Facts_RequireCompletedConstruction_ActualLandings_AndCraftHistory_NotInventory()
        {
            var state = new GameState(db, false);
            state.stats.RecordWaveCleared();
            state.Add(db.FindResource("steel"), 1);
            Assert.IsFalse(state.ObjectiveCompleted("first_steel"), "Owning a resource cannot prove it was crafted.");
            Assert.IsTrue(state.ObjectiveCompleted("first_wave"));
            state.stats.worldsLanded = 100;
            state.Notify();
            Assert.IsFalse(state.ObjectiveCompleted("first_hop"), "Landing count cannot replace distinct-world evidence.");
            var lab = state.DebugPlace(db.FindStructure("research_lab"), 0);
            Assert.IsTrue(state.ObjectiveCompleted("research_lab"));
            Assert.IsTrue(state.ObjectiveCompleted("first_structure"));
            Assert.IsNotNull(lab);
            state.DebugSetUpgrade(db.FindUpgrade("jump"), 1);
            Assert.IsTrue(state.ObjectiveCompleted("era_settlement"));
            Assert.IsFalse(state.ObjectiveCompleted("era_foothold"), "Skipping an era gate cannot award its achievement.");
        }

        [Test]
        public void ReadOnlySave_RejectsClaimsWithoutChangingCurrencyOrLedger()
        {
            var state = new GameState(db, false);
            state.RecordHarvestRunCompleted();
            int before = state.ShardBalance;
            state.saves.Store.MarkReadOnly("Test newer save");
            Assert.AreEqual(ObjectiveClaimResult.Rejected, state.ClaimObjective("first_harvest", out var message));
            StringAssert.Contains("read-only", message);
            Assert.AreEqual(before, state.ShardBalance);
            Assert.IsFalse(state.ObjectiveClaimed("first_harvest"));
        }

        [Test]
        public void FailedClaimSave_ReportsPending_PreservesDirtyState_AndRetryDoesNotDuplicate()
        {
            var state = new GameState(db, false);
            state.RecordHarvestRunCompleted();
            int before = state.ShardBalance;
            Directory.CreateDirectory(sandbox.SaveFile);
            LogAssert.Expect(LogType.Warning, new Regex("Frontier save failed:"));
            Assert.AreEqual(ObjectiveClaimResult.PendingSave, state.ClaimObjective("first_harvest", out var message));
            StringAssert.Contains("save failed", message);
            Assert.IsTrue(state.saves.IsDirty);
            Assert.AreEqual(before + 5, state.ShardBalance);
            Assert.IsTrue(state.ObjectiveClaimed("first_harvest"));
            Assert.AreEqual(ObjectiveClaimResult.Rejected, state.ClaimObjective("first_harvest", out _));
            Directory.Delete(sandbox.SaveFile);
            Assert.IsTrue(state.Save());
            var restored = new GameState(db, true);
            Assert.AreEqual(before + 5, restored.ShardBalance);
            Assert.IsTrue(restored.ObjectiveClaimed("first_harvest"));
        }

        [Test]
        public void UnfinishedConstruction_CannotCompleteGoals_UntilTheTimerFinishes()
        {
            var clock = new FakeClock();
            var state = new GameState(db, false, clock);
            foreach (var resource in db.resources) state.backpack.Set(resource.id, 100000);
            var refinery = db.FindStructure("refinery");
            int originalDuration = refinery.Row(1).buildSeconds;
            refinery.Row(1).buildSeconds = 5;
            try
            {
                Assert.IsTrue(state.Place(refinery, 0));
                Assert.IsFalse(state.ObjectiveCompleted("first_structure"));
                state.Save();
                var restored = new GameState(db, true, clock);
                Assert.IsFalse(restored.ObjectiveCompleted("first_structure"));
                clock.Advance(5);
                restored.Tick();
                Assert.IsTrue(restored.ObjectiveCompleted("first_structure"));
            }
            finally { refinery.Row(1).buildSeconds = originalDuration; }
        }

        [Test]
        public void FullWallet_RejectsRewardWithoutConsumingTheClaim()
        {
            var state = new GameState(db, false);
            state.RecordHarvestRunCompleted();
            state.Save();
            var save = JsonUtility.FromJson<SaveV2>(File.ReadAllText(sandbox.SaveFile));
            save.wallet.Clear();
            save.wallet.Add(new WalletEntry { currency = "shards", amount = int.MaxValue });
            File.WriteAllText(sandbox.SaveFile, JsonUtility.ToJson(save));
            var restored = new GameState(db, true);
            Assert.AreEqual(ObjectiveClaimResult.Rejected, restored.ClaimObjective("first_harvest", out var message));
            StringAssert.Contains("balance is full", message);
            Assert.AreEqual(int.MaxValue, restored.ShardBalance);
            Assert.IsFalse(restored.ObjectiveClaimed("first_harvest"));
        }

        [Test]
        public void NormalizedUnknownRecords_SurviveRoundTrip()
        {
            var state = new GameState(db, false);
            state.Save();
            var save = JsonUtility.FromJson<SaveV2>(File.ReadAllText(sandbox.SaveFile));
            save.objectives.Add(new ObjectiveRecord { id = "future_goal", claimed = true });
            save.objectives.Add(new ObjectiveRecord { id = "future_goal", completed = true });
            File.WriteAllText(sandbox.SaveFile, JsonUtility.ToJson(save));
            var restored = new GameState(db, true);
            restored.Save();
            var roundTrip = JsonUtility.FromJson<SaveV2>(File.ReadAllText(sandbox.SaveFile));
            var record = roundTrip.objectives.Single(x => x.id == "future_goal");
            Assert.IsTrue(record.completed);
            Assert.IsTrue(record.claimed);
        }
    }
}
