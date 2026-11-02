using System;
using System.Reflection;
using System.Threading.Tasks;
using Frontier.Economy;
using Frontier.Services;
using Frontier.Services.Unity;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class ShardStoreIntegrationTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void BothStoreMappingsMustCoverTheRewardCatalogExactlyWithoutDuplicateProducts()
        {
            var rules = ScriptableObject.CreateInstance<ShardRulesSO>();
            try
            {
                rules.data = new ShardRulesDef { packs = new[]
                {
                    new ShardPackDef { id = "shards_1", shards = 80 },
                    new ShardPackDef { id = "shards_2", shards = 500 }
                }};
                var mappings = new[]
                {
                    new ShardStoreProduct { catalogId = "shards_1", googlePlayId = "shards.small", appleAppStoreId = "shards.small" },
                    new ShardStoreProduct { catalogId = "shards_2", googlePlayId = "shards.large", appleAppStoreId = "shards.large" }
                };
                Assert.IsTrue(UnityShardPurchaseProvider.ValidateProducts(rules, mappings));
                mappings[1].googlePlayId = "shards.small";
                Assert.IsFalse(UnityShardPurchaseProvider.ValidateProducts(rules, mappings));
                mappings[1].googlePlayId = "shards.large";
                mappings[1].appleAppStoreId = "";
                Assert.IsFalse(UnityShardPurchaseProvider.ValidateProducts(rules, mappings));
                mappings[1].appleAppStoreId = "shards.large";
                mappings[1].catalogId = "shards_1";
                Assert.IsFalse(UnityShardPurchaseProvider.ValidateProducts(rules, mappings));
                Assert.IsFalse(UnityShardPurchaseProvider.ValidateProducts(rules, Array.Empty<ShardStoreProduct>()));
            }
            finally { UnityEngine.Object.DestroyImmediate(rules); }
        }

        [Test]
        public async Task DisabledOrEditorPurchasesNeverConnectAndDoNotInventLocalPricesOrReceipts()
        {
            var provider = new UnityShardPurchaseProvider(null, null);
            Assert.IsFalse(provider.IsConfigured);
            Assert.AreEqual(ServiceStatus.NotConfigured, (await provider.InitializeAsync()).status);
            Assert.IsFalse((await provider.FetchProductsAsync(new[] { "shards_1" })).succeeded);
            Assert.IsFalse((await provider.PurchaseAsync("shards_1")).succeeded);
            Assert.IsFalse((await provider.RestoreAsync()).succeeded);
            provider.Shutdown();
        }

        sealed class Grants : IPurchaseGrantStore
        {
            public string context = "save-A";
            public int commits;
            public string ContextId => context;
            public ServiceResult CommitVerifiedPurchase(string expectedContextId, VerifiedPurchase purchase)
            {
                Assert.AreEqual(context, expectedContextId);
                commits++;
                return ServiceResult.Success();
            }
        }

        sealed class Verifier : IPurchaseReceiptVerifier
        {
            public int calls;
            public readonly TaskCompletionSource<ServiceResult<VerifiedPurchase>> completion =
                new TaskCompletionSource<ServiceResult<VerifiedPurchase>>();
            public Task<ServiceResult<VerifiedPurchase>> VerifyAsync(PurchaseReceipt receipt)
            {
                calls++;
                return completion.Task;
            }
        }

        sealed class Acknowledger : IPurchaseAcknowledger
        {
            public int calls;
            public Task<ServiceResult> ConfirmAsync(VerifiedPurchase purchase)
            {
                calls++;
                return Task.FromResult(ServiceResult.Success());
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task LateVerificationCannotGrantAfterShutdownOrSaveReplacement(bool shutdown)
        {
            var provider = new UnityShardPurchaseProvider(null, null);
            var destination = new Grants();
            provider.BindGrantStore(destination);
            var session = (IPurchaseGrantStore)typeof(UnityShardPurchaseProvider).GetField("grants", Private).GetValue(provider);
            var verifier = new Verifier();
            var acknowledger = new Acknowledger();
            typeof(UnityShardPurchaseProvider).GetField("fulfillment", Private).SetValue(provider,
                new PurchaseFulfillment(verifier, session, acknowledger));
            var enqueue = typeof(UnityShardPurchaseProvider).GetMethod("Enqueue", Private);
            var receipt = new PurchaseReceipt("shards_1", "tx-1", "signed-test-evidence");
            var first = (Task<ServiceResult>)enqueue.Invoke(provider, new object[] { receipt });
            var duplicate = (Task<ServiceResult>)enqueue.Invoke(provider, new object[] { receipt });
            Assert.AreSame(first, duplicate);
            Assert.AreEqual(1, verifier.calls);
            var mismatched = (Task<ServiceResult>)enqueue.Invoke(provider, new object[]
                { new PurchaseReceipt("shards_2", receipt.transactionId, receipt.receiptPayload) });
            Assert.AreEqual(ServiceStatus.InvalidRequest, (await mismatched).status);
            Assert.AreEqual(1, verifier.calls);
            if (shutdown) provider.Shutdown();
            else destination.context = "save-B";
            verifier.completion.SetResult(ServiceResult<VerifiedPurchase>.Success(
                new VerifiedPurchase("GooglePlay", receipt.productId, receipt.transactionId)));
            Assert.IsFalse((await first).succeeded);
            Assert.AreEqual(0, destination.commits);
            Assert.AreEqual(0, acknowledger.calls);
            provider.Shutdown();
        }

        [Test]
        public void AnalyticsWithdrawalDoesNotResolveTheSdkSingletonAfterTeardown()
        {
            var assembly = typeof(FrontierUnityServicesFactory).Assembly;
            var type = assembly.GetType("Frontier.Services.Unity.UnityAnalyticsProvider", true);
            var provider = Activator.CreateInstance(type, new object[] { null, null });
            type.GetField("initialized", Private).SetValue(provider, true);
            Assert.DoesNotThrow(() => ((IAnalyticsService)provider).SetDataCollectionAllowed(false));
            Assert.IsFalse((bool)type.GetField("initialized", Private).GetValue(provider));
        }
    }
}
