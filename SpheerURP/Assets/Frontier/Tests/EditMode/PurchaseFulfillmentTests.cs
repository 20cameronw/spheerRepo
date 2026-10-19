using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Frontier.Services;
using NUnit.Framework;

namespace Frontier.Tests
{
    public sealed class PurchaseFulfillmentTests
    {
        sealed class Verifier : IPurchaseReceiptVerifier
        {
            public int calls;
            public Func<PurchaseReceipt, Task<ServiceResult<VerifiedPurchase>>> verify;
            public Task<ServiceResult<VerifiedPurchase>> VerifyAsync(PurchaseReceipt receipt)
            {
                calls++;
                Assert.AreEqual("test-signed-receipt", receipt.receiptPayload);
                return verify != null ? verify(receipt) : Task.FromResult(
                    ServiceResult<VerifiedPurchase>.Success(
                        new VerifiedPurchase("test-store", receipt.productId, receipt.transactionId)));
            }
        }

        sealed class GrantStore : IPurchaseGrantStore
        {
            public string context = "save-A";
            public string ContextId { get { return context; } }
            public bool failWrite;
            public int commits, grants;
            public readonly Dictionary<string, string> journal = new Dictionary<string, string>();

            public ServiceResult CommitVerifiedPurchase(string expectedContextId, VerifiedPurchase purchase)
            {
                commits++;
                if (context != expectedContextId) return ServiceResult.InvalidRequest("Destination changed");
                if (failWrite) return ServiceResult.Failed("Test write failed");
                string key = purchase.store + ":" + purchase.transactionId;
                if (journal.TryGetValue(key, out string product))
                    return product == purchase.productId ? ServiceResult.Success()
                        : ServiceResult.InvalidRequest("Transaction product mismatch");
                journal.Add(key, purchase.productId);
                grants++;
                return ServiceResult.Success();
            }
        }

        sealed class Acknowledger : IPurchaseAcknowledger
        {
            public int calls;
            public Func<VerifiedPurchase, Task<ServiceResult>> confirm;
            public Task<ServiceResult> ConfirmAsync(VerifiedPurchase purchase)
            {
                calls++;
                return confirm != null ? confirm(purchase) : Task.FromResult(ServiceResult.Success());
            }
        }

        [Test]
        public async Task VerificationAndDurableGrantPrecedeAcknowledgement()
        {
            var verifier = new Verifier();
            var grants = new GrantStore();
            var acknowledger = new Acknowledger
            {
                confirm = purchase =>
                {
                    Assert.AreEqual(1, verifier.calls);
                    Assert.AreEqual(1, grants.grants);
                    Assert.AreEqual("shards_1", grants.journal["test-store:tx-1"]);
                    return Task.FromResult(ServiceResult.Success());
                }
            };
            var flow = new PurchaseFulfillment(verifier, grants, acknowledger);
            Assert.IsTrue((await flow.ProcessPendingAsync(Receipt("shards_1", "tx-1"))).succeeded);
            Assert.AreEqual(1, acknowledger.calls);
        }

        [TestCase(null, "tx")]
        [TestCase("", "tx")]
        [TestCase("shards_1", null)]
        [TestCase("shards_1", " ")]
        public async Task IncompleteReceiptsNeverReachVerificationOrGrant(string productId, string transactionId)
        {
            var verifier = new Verifier();
            var grants = new GrantStore();
            var acknowledger = new Acknowledger();
            var flow = new PurchaseFulfillment(verifier, grants, acknowledger);
            Assert.AreEqual(ServiceStatus.InvalidRequest,
                (await flow.ProcessPendingAsync(Receipt(productId, transactionId))).status);
            Assert.AreEqual(0, verifier.calls + grants.commits + acknowledger.calls);
        }

        [Test]
        public async Task RejectedVerificationCannotGrantOrConfirm()
        {
            var verifier = new Verifier
            {
                verify = _ => Task.FromResult(ServiceResult<VerifiedPurchase>.InvalidRequest("Invalid signature"))
            };
            var grants = new GrantStore();
            var acknowledger = new Acknowledger();
            var flow = new PurchaseFulfillment(verifier, grants, acknowledger);
            var result = await flow.ProcessPendingAsync(Receipt("shards_1", "tx-1"));
            Assert.AreEqual(ServiceStatus.Failed, result.status);
            StringAssert.Contains("Invalid signature", result.message);
            Assert.AreEqual(0, grants.commits + acknowledger.calls);
        }

        [TestCase("", "shards_1", "tx-1")]
        [TestCase("test-store", "shards_2", "tx-1")]
        [TestCase("test-store", "shards_1", "tx-2")]
        public async Task VerifiedIdentityMustMatchPendingTransaction(string store, string product, string transaction)
        {
            var verifier = new Verifier
            {
                verify = _ => Task.FromResult(ServiceResult<VerifiedPurchase>.Success(
                    new VerifiedPurchase(store, product, transaction)))
            };
            var grants = new GrantStore();
            var acknowledger = new Acknowledger();
            var flow = new PurchaseFulfillment(verifier, grants, acknowledger);
            Assert.AreEqual(ServiceStatus.InvalidRequest,
                (await flow.ProcessPendingAsync(Receipt("shards_1", "tx-1"))).status);
            Assert.AreEqual(0, grants.commits + acknowledger.calls);
        }

        [Test]
        public async Task FailedSaveStaysPendingAndRetryGrantsOnce()
        {
            var verifier = new Verifier();
            var grants = new GrantStore { failWrite = true };
            var acknowledger = new Acknowledger();
            var flow = new PurchaseFulfillment(verifier, grants, acknowledger);
            var receipt = Receipt("shards_1", "tx-1");
            Assert.AreEqual(ServiceStatus.Failed, (await flow.ProcessPendingAsync(receipt)).status);
            Assert.AreEqual(0, grants.grants + acknowledger.calls);
            grants.failWrite = false;
            Assert.IsTrue((await flow.ProcessPendingAsync(receipt)).succeeded);
            Assert.IsTrue((await flow.ProcessPendingAsync(receipt)).succeeded);
            Assert.AreEqual(1, grants.grants);
        }

        [Test]
        public async Task ConfirmationFailureAndProcessRestartRetryWithoutAnotherGrant()
        {
            var verifier = new Verifier();
            var grants = new GrantStore();
            var acknowledger = new Acknowledger
            {
                confirm = _ => Task.FromResult(ServiceResult.Failed("Store offline"))
            };
            var receipt = Receipt("shards_1", "tx-1");
            var result = await new PurchaseFulfillment(verifier, grants, acknowledger).ProcessPendingAsync(receipt);
            Assert.AreEqual(ServiceStatus.Failed, result.status);
            StringAssert.Contains("reward is saved", result.message);
            Assert.AreEqual(1, grants.grants);
            acknowledger.confirm = null;
            Assert.IsTrue((await new PurchaseFulfillment(verifier, grants, acknowledger).ProcessPendingAsync(receipt)).succeeded);
            Assert.AreEqual(1, grants.grants);
            Assert.AreEqual(2, acknowledger.calls);
        }

        [Test]
        public async Task PendingRequestsAreExclusiveAndDestinationChangesPreventGranting()
        {
            var pending = new TaskCompletionSource<ServiceResult<VerifiedPurchase>>();
            var verifier = new Verifier { verify = _ => pending.Task };
            var grants = new GrantStore();
            var acknowledger = new Acknowledger();
            var flow = new PurchaseFulfillment(verifier, grants, acknowledger);
            var receipt = Receipt("shards_1", "tx-1");
            var first = flow.ProcessPendingAsync(receipt);
            Assert.AreEqual(ServiceStatus.InvalidRequest, (await flow.ProcessPendingAsync(receipt)).status);
            grants.context = "save-B";
            pending.SetResult(ServiceResult<VerifiedPurchase>.Success(new VerifiedPurchase("test-store", "shards_1", "tx-1")));
            Assert.AreEqual(ServiceStatus.InvalidRequest, (await first).status);
            Assert.AreEqual(0, grants.commits + acknowledger.calls);
        }

        [Test]
        public async Task TransactionCannotBeReusedForAnotherProduct()
        {
            var grants = new GrantStore();
            var acknowledger = new Acknowledger();
            var flow = new PurchaseFulfillment(new Verifier(), grants, acknowledger);
            Assert.IsTrue((await flow.ProcessPendingAsync(Receipt("shards_1", "tx-1"))).succeeded);
            Assert.AreEqual(ServiceStatus.InvalidRequest,
                (await flow.ProcessPendingAsync(Receipt("shards_2", "tx-1"))).status);
            Assert.AreEqual(1, grants.grants);
            Assert.AreEqual(1, acknowledger.calls);
        }

        [Test]
        public async Task ExceptionsReleaseThePendingGateWithoutPrematureAcknowledgement()
        {
            var verifier = new Verifier
            {
                verify = _ => throw new InvalidOperationException("Test verification exception")
            };
            var grants = new GrantStore();
            var acknowledger = new Acknowledger();
            var flow = new PurchaseFulfillment(verifier, grants, acknowledger);
            var receipt = Receipt("shards_1", "tx-1");
            Assert.ThrowsAsync<InvalidOperationException>(async () => await flow.ProcessPendingAsync(receipt));
            Assert.AreEqual(0, grants.commits + acknowledger.calls);
            verifier.verify = null;
            Assert.IsTrue((await flow.ProcessPendingAsync(receipt)).succeeded);
        }

        static PurchaseReceipt Receipt(string product, string transaction)
        {
            return new PurchaseReceipt(product, transaction, "test-signed-receipt");
        }

        [Test]
        public async Task ReceiptIdentifiersAloneAreNotVerificationEvidence()
        {
            var verifier = new Verifier();
            var grants = new GrantStore();
            var acknowledger = new Acknowledger();
            var result = await new PurchaseFulfillment(verifier, grants, acknowledger)
                .ProcessPendingAsync(new PurchaseReceipt("shards_1", "tx-1"));
            Assert.AreEqual(ServiceStatus.InvalidRequest, result.status);
            Assert.AreEqual(0, verifier.calls + grants.commits + acknowledger.calls);
        }

        [Test]
        public async Task MissingDestinationDoesNotBeginVerification()
        {
            var verifier = new Verifier();
            var grants = new GrantStore { context = "" };
            var acknowledger = new Acknowledger();
            var result = await new PurchaseFulfillment(verifier, grants, acknowledger)
                .ProcessPendingAsync(Receipt("shards_1", "tx-1"));
            Assert.AreEqual(ServiceStatus.InvalidRequest, result.status);
            Assert.AreEqual(0, verifier.calls + grants.commits + acknowledger.calls);
        }

        [Test]
        public async Task AcknowledgementExceptionCanRetryWithoutAnotherGrant()
        {
            var grants = new GrantStore();
            var acknowledger = new Acknowledger
            {
                confirm = _ => throw new InvalidOperationException("Test confirmation exception")
            };
            var flow = new PurchaseFulfillment(new Verifier(), grants, acknowledger);
            var receipt = Receipt("shards_1", "tx-1");
            Assert.ThrowsAsync<InvalidOperationException>(async () => await flow.ProcessPendingAsync(receipt));
            Assert.AreEqual(1, grants.grants);
            acknowledger.confirm = null;
            Assert.IsTrue((await flow.ProcessPendingAsync(receipt)).succeeded);
            Assert.AreEqual(1, grants.grants);
        }
    }
}
