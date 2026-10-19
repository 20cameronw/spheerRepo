using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Frontier.Services;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Frontier.Tests
{
    public sealed class PurchaseGrantTests
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
        public void TearDown() { sandbox.Dispose(); }

        static ServiceResult Commit(GameState state, string product = "shards_1", string transaction = "tx-1", string store = "test-store")
        {
            var grants = (IPurchaseGrantStore)state;
            return grants.CommitVerifiedPurchase(grants.ContextId, new VerifiedPurchase(store, product, transaction));
        }

        SaveV2 ReadSave()
        {
            return SaveSerializer.Instance.DeserializeV2(File.ReadAllText(sandbox.SaveFile)).Normalize();
        }

        [Test]
        public void GrantSavesWalletAndTransactionTogetherBeforeNotifyingAndSurvivesReload()
        {
            var state = new GameState(db, false, clock);
            int before = state.ShardBalance;
            int reward = db.shardRules.data.packs.Single(pack => pack.id == "shards_1").shards;
            int notifications = 0;
            state.Changed += () =>
            {
                var saved = ReadSave();
                Assert.AreEqual(1, saved.purchaseGrants.Count);
                Assert.AreEqual("tx-1", saved.purchaseGrants[0].transactionId);
                Assert.AreEqual(before + reward, saved.wallet.Where(entry => entry.currency == "shards").Sum(entry => entry.amount));
                notifications++;
            };
            Assert.IsTrue(Commit(state).succeeded);
            Assert.AreEqual(before + reward, state.ShardBalance);
            Assert.AreEqual(1, notifications);
            Assert.IsFalse(state.saves.IsDirty);
            Assert.IsTrue(Commit(state).succeeded);
            Assert.AreEqual(1, notifications, "Duplicate delivery must not publish another grant.");
            var loaded = new GameState(db, true, clock);
            Assert.IsTrue(Commit(loaded).succeeded);
            Assert.AreEqual(before + reward, loaded.ShardBalance);
            Assert.AreEqual(1, ReadSave().purchaseGrants.Count);
        }

        [Test]
        public void EveryAuthoredShardPackGrantsItsExactCatalogAmount()
        {
            var state = new GameState(db, false, clock);
            int balance = state.ShardBalance;
            var packs = db.shardRules.data.packs;
            Assert.AreEqual(5, packs.Length);
            foreach (var pack in packs)
            {
                Assert.IsTrue(Commit(state, pack.id, transaction: "tx-" + pack.id).succeeded);
                balance += pack.shards;
                Assert.AreEqual(balance, state.ShardBalance);
                Assert.IsTrue(Commit(state, pack.id, transaction: "tx-" + pack.id).succeeded);
                Assert.AreEqual(balance, state.ShardBalance);
            }
            Assert.AreEqual(5, ReadSave().purchaseGrants.Count);
        }

        [Test]
        public void FailedDiskWriteLeavesWalletAndJournalUnchangedAndRetryPaysOnce()
        {
            var state = new GameState(db, false, clock);
            int before = state.ShardBalance;
            int reward = db.shardRules.data.packs[0].shards;
            Directory.CreateDirectory(sandbox.SaveFile);
            LogAssert.Expect(LogType.Warning, new Regex("Frontier save failed:"));
            var failed = Commit(state, db.shardRules.data.packs[0].id);
            Assert.AreEqual(ServiceStatus.Failed, failed.status);
            Assert.AreEqual(before, state.ShardBalance);
            Assert.IsEmpty(state.saves.LastWrittenSnapshot == null
                ? Array.Empty<PurchaseGrantRecord>() : state.saves.LastWrittenSnapshot.purchaseGrants.ToArray());
            Directory.Delete(sandbox.SaveFile);
            Assert.IsTrue(Commit(state, db.shardRules.data.packs[0].id).succeeded);
            Assert.IsTrue(Commit(state, db.shardRules.data.packs[0].id).succeeded);
            Assert.AreEqual(before + reward, state.ShardBalance);
            Assert.AreEqual(1, ReadSave().purchaseGrants.Count);
        }

        [Test]
        public void ReadOnlyUnknownAndStaleDestinationNeverGrant()
        {
            var state = new GameState(db, false, clock);
            int before = state.ShardBalance;
            var grants = (IPurchaseGrantStore)state;
            Assert.AreEqual(ServiceStatus.InvalidRequest,
                grants.CommitVerifiedPurchase("previous-context", new VerifiedPurchase("test", "shards_1", "tx")).status);
            Assert.AreEqual(ServiceStatus.InvalidRequest, Commit(state, "unknown").status);
            Assert.AreEqual(ServiceStatus.InvalidRequest, Commit(state, transaction: "").status);
            Assert.AreEqual(ServiceStatus.InvalidRequest, Commit(state, store: " ").status);
            state.saves.Store.MarkReadOnly("Test future save");
            Assert.AreEqual(ServiceStatus.InvalidRequest, Commit(state).status);
            Assert.AreEqual(before, state.ShardBalance);
            Assert.IsFalse(File.Exists(sandbox.SaveFile));
        }

        [Test]
        public void ConflictingProductAndMalformedLoadedJournalFailClosed()
        {
            var state = new GameState(db, false, clock);
            Assert.IsTrue(Commit(state).succeeded);
            int balance = state.ShardBalance;
            Assert.AreEqual(ServiceStatus.InvalidRequest, Commit(state, "shards_2").status);
            Assert.AreEqual(balance, state.ShardBalance);
            var save = ReadSave();
            save.purchaseGrants.Add(new PurchaseGrantRecord { store = "", transactionId = "bad", productId = "shards_1", shards = 1 });
            File.WriteAllText(sandbox.SaveFile, SaveSerializer.Instance.Serialize(save));
            var loaded = new GameState(db, true, clock);
            Assert.AreEqual(ServiceStatus.InvalidRequest, Commit(loaded, transaction: "new").status);
            Assert.AreEqual(balance, loaded.ShardBalance);
        }

        [Test]
        public void FullWalletCannotConsumeAPendingTransaction()
        {
            var seed = new GameState(db, false, clock);
            Assert.IsTrue(seed.Save());
            var save = ReadSave();
            save.wallet.RemoveAll(entry => entry.currency == "shards");
            save.wallet.Add(new WalletEntry { currency = "shards", amount = int.MaxValue });
            File.WriteAllText(sandbox.SaveFile, SaveSerializer.Instance.Serialize(save));
            var loaded = new GameState(db, true, clock);
            Assert.AreEqual(ServiceStatus.InvalidRequest, Commit(loaded).status);
            Assert.AreEqual(int.MaxValue, loaded.ShardBalance);
            Assert.IsEmpty(ReadSave().purchaseGrants);
        }

        [Test]
        public void StoreNamespaceSeparatesTransactionsAndOrdinarySavesPreserveTheJournal()
        {
            var state = new GameState(db, false, clock);
            int before = state.ShardBalance;
            int reward = db.shardRules.data.packs[0].shards;
            Assert.IsTrue(Commit(state, db.shardRules.data.packs[0].id, store: "store-A").succeeded);
            Assert.IsTrue(Commit(state, db.shardRules.data.packs[0].id, store: "store-B").succeeded);
            Assert.IsTrue(state.Save());
            Assert.AreEqual(before + reward * 2, state.ShardBalance);
            Assert.AreEqual(2, ReadSave().purchaseGrants.Count);
        }

        [Test]
        public void ResetWithCommittedPurchaseRefusesWithoutDeletingAnyRecoveryFileOrChangingContext()
        {
            var state = new GameState(db, false, clock);
            Assert.IsTrue(state.TryCreateCloudSaveSnapshot(out var beforePurchase, out _));
            Assert.IsTrue(state.TryRestoreCloudSave(beforePurchase, out _));
            Assert.IsTrue(Commit(state).succeeded);
            var grants = (IPurchaseGrantStore)state;
            string context = grants.ContextId;
            string primary = File.ReadAllText(sandbox.SaveFile);
            string backupPath = state.saves.Store.CloudRestoreBackupPath;
            string backup = File.ReadAllText(backupPath);
            int balance = state.ShardBalance;
            Assert.IsFalse(state.TryResetProgress(out string message));
            StringAssert.Contains("purchase", message);
            Assert.AreEqual(context, grants.ContextId);
            Assert.AreEqual(balance, state.ShardBalance);
            Assert.AreEqual(primary, File.ReadAllText(sandbox.SaveFile));
            Assert.AreEqual(backup, File.ReadAllText(backupPath));
            var loaded = new GameState(db, true, clock);
            Assert.IsTrue(Commit(loaded).succeeded);
            Assert.AreEqual(balance, loaded.ShardBalance);
        }

        [Test]
        public void RestoreAndUndoCannotRollBackPurchasedOrSpentCurrency()
        {
            var state = new GameState(db, false, clock);
            Assert.IsTrue(state.TryCreateCloudSaveSnapshot(out var oldSnapshot, out _));
            Assert.IsTrue(state.TryRestoreCloudSave(oldSnapshot, out _));
            Assert.IsTrue(Commit(state).succeeded);
            Assert.IsTrue(state.TryCreateCloudSaveSnapshot(out var purchasedSnapshot, out _));
            var spentSnapshot = ReadSave();
            spentSnapshot.wallet.Single(entry => entry.currency == "shards").amount = 1;
            File.WriteAllText(sandbox.SaveFile, SaveSerializer.Instance.Serialize(spentSnapshot));
            state = new GameState(db, true, clock);
            var grants = (IPurchaseGrantStore)state;
            string context = grants.ContextId;
            string primary = File.ReadAllText(sandbox.SaveFile);
            string undo = File.ReadAllText(state.saves.Store.CloudRestoreBackupPath);
            Assert.IsFalse(state.CanRestoreCloudSaveSnapshot(oldSnapshot, out _));
            Assert.IsFalse(state.TryRestoreCloudSave(oldSnapshot, out string message));
            StringAssert.Contains("purchase", message);
            Assert.IsFalse(state.TryRestoreCloudSave(purchasedSnapshot, out _),
                "Even equal journals cannot authorize restoring a pre-spend wallet.");
            Assert.IsFalse(state.TryRestorePreviousLocalSave(out _));
            Assert.AreEqual(1, state.ShardBalance);
            Assert.AreEqual(context, grants.ContextId);
            Assert.AreEqual(primary, File.ReadAllText(sandbox.SaveFile));
            Assert.AreEqual(undo, File.ReadAllText(state.saves.Store.CloudRestoreBackupPath));
        }

        [Test]
        public void IncomingPurchaseHistoryCannotBeInstalledIntoANewOrResetSave()
        {
            var purchased = new GameState(db, false, clock);
            Assert.IsTrue(Commit(purchased).succeeded);
            Assert.IsTrue(purchased.TryCreateCloudSaveSnapshot(out var snapshot, out _));
            var fresh = new GameState(db, false, clock);
            Assert.IsTrue(fresh.Save());
            string primary = File.ReadAllText(sandbox.SaveFile);
            Assert.IsFalse(fresh.CanRestoreCloudSaveSnapshot(snapshot, out _));
            Assert.IsFalse(fresh.TryRestoreCloudSave(snapshot, out string message));
            StringAssert.Contains("purchase", message);
            Assert.AreEqual(primary, File.ReadAllText(sandbox.SaveFile));
            Assert.AreEqual(1, snapshot.purchaseGrants.Count, "Preview cannot modify the caller's purchase history.");
        }

        [Test]
        public void ResetInvalidatesTheInFlightPurchaseDestination()
        {
            var state = new GameState(db, false, clock);
            var grants = (IPurchaseGrantStore)state;
            string context = grants.ContextId;
            state.ResetProgress();
            Assert.AreNotEqual(context, grants.ContextId);
            Assert.AreEqual(ServiceStatus.InvalidRequest,
                grants.CommitVerifiedPurchase(context, new VerifiedPurchase("test", "shards_1", "tx")).status);
        }

        sealed class TestVerifier : IPurchaseReceiptVerifier
        {
            public Task<ServiceResult<VerifiedPurchase>> VerifyAsync(PurchaseReceipt receipt)
            {
                return Task.FromResult(ServiceResult<VerifiedPurchase>.Success(
                    new VerifiedPurchase("test-store", receipt.productId, receipt.transactionId)));
            }
        }

        sealed class TestAcknowledger : IPurchaseAcknowledger
        {
            public int confirmations;
            public Task<ServiceResult> ConfirmAsync(VerifiedPurchase purchase)
            {
                confirmations++;
                return Task.FromResult(confirmations == 1 ? ServiceResult.Failed("Test store offline") : ServiceResult.Success());
            }
        }

        [Test]
        public async Task FulfillmentRetriesConfirmationWithRealDiskJournalAfterReloadWithoutPayingTwice()
        {
            var state = new GameState(db, false, clock);
            int before = state.ShardBalance;
            int reward = db.shardRules.data.packs[0].shards;
            var receipt = new PurchaseReceipt(db.shardRules.data.packs[0].id, "tx", "test-only-receipt");
            var acknowledge = new TestAcknowledger();
            var flow = new PurchaseFulfillment(new TestVerifier(), state, acknowledge);
            Assert.AreEqual(ServiceStatus.Failed, (await flow.ProcessPendingAsync(receipt)).status);
            Assert.AreEqual(before + reward, state.ShardBalance);
            var loaded = new GameState(db, true, clock);
            flow = new PurchaseFulfillment(new TestVerifier(), loaded, acknowledge);
            Assert.IsTrue((await flow.ProcessPendingAsync(receipt)).succeeded);
            Assert.AreEqual(before + reward, loaded.ShardBalance);
            Assert.AreEqual(1, ReadSave().purchaseGrants.Count);
        }
    }
}
