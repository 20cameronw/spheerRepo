using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Purchasing;

namespace Frontier.Services.Unity
{
    /// <summary>
    /// Register an authenticated backend verifier before Bootstrap. It must validate native receipts,
    /// app/product/quantity/payment state and account ownership against an authoritative transaction ledger.
    /// Client product strings and local purchase history are not evidence of payment.
    /// </summary>
    public static class ShardPurchaseVerification
    {
        public static Func<IPurchaseGrantStore, IPurchaseReceiptVerifier> CreateVerifier { get; set; }
    }

    public sealed class UnityShardPurchaseProvider : Frontier.Services.IPurchaseService,
        IPurchaseSessionService, IPurchaseAcknowledger
    {
        readonly FrontierUnityServicesConfig config;
        readonly ShardRulesSO rules;
        readonly Dictionary<string, string> storeIds = new Dictionary<string, string>(StringComparer.Ordinal);
        readonly Dictionary<string, PendingOrder> pending = new Dictionary<string, PendingOrder>(StringComparer.Ordinal);
        readonly Dictionary<string, TaskCompletionSource<ServiceResult>> confirmations =
            new Dictionary<string, TaskCompletionSource<ServiceResult>>(StringComparer.Ordinal);
        readonly Dictionary<string, Task<ServiceResult>> fulfillmentTasks =
            new Dictionary<string, Task<ServiceResult>>(StringComparer.Ordinal);
        readonly Dictionary<string, string> fulfillmentProducts = new Dictionary<string, string>(StringComparer.Ordinal);
        SessionGrants grants;
        StoreController controller;
        PurchaseFulfillment fulfillment;
        Task<ServiceResult> initialization;
        Task<ServiceResult> storeConnection;
        Task queue = Task.CompletedTask;
        TaskCompletionSource<ServiceResult> productsFetched;
        TaskCompletionSource<ServiceResult<PurchaseReceipt>> purchase;
        TaskCompletionSource<ServiceResult<IReadOnlyList<PurchaseReceipt>>> restored;
        string purchasingId;
        bool ready, closed;
        string StoreName => Application.platform == RuntimePlatform.IPhonePlayer ? "AppleAppStore" : "GooglePlay";

        public UnityShardPurchaseProvider(FrontierUnityServicesConfig config, ShardRulesSO rules)
        {
            this.config = config;
            this.rules = rules;
        }

        public bool IsConfigured => !closed && config != null && config.enablePurchases &&
            config.HasValidPrivacyPolicy && ShardPurchaseVerification.CreateVerifier != null &&
            !Application.isEditor && (Application.platform == RuntimePlatform.Android ||
            Application.platform == RuntimePlatform.IPhonePlayer) && ValidProductMapping();

        public static bool ValidateProducts(ShardRulesSO rules, ShardStoreProduct[] mappings)
        {
            if (rules == null || rules.data == null || rules.data.packs == null ||
                rules.data.packs.Length == 0 || mappings == null) return false;
            var catalog = new HashSet<string>(StringComparer.Ordinal);
            var google = new HashSet<string>(StringComparer.Ordinal);
            var apple = new HashSet<string>(StringComparer.Ordinal);
            foreach (var pack in rules.data.packs)
            {
                if (pack == null || pack.shards <= 0 || string.IsNullOrWhiteSpace(pack.id) || !catalog.Add(pack.id))
                    return false;
                var matches = mappings.Where(m => m != null && m.catalogId == pack.id).ToArray();
                if (matches.Length != 1 || string.IsNullOrWhiteSpace(matches[0].googlePlayId) ||
                    string.IsNullOrWhiteSpace(matches[0].appleAppStoreId) ||
                    !google.Add(matches[0].googlePlayId) || !apple.Add(matches[0].appleAppStoreId)) return false;
            }
            return mappings.Length == catalog.Count;
        }

        bool ValidProductMapping() => ValidateProducts(rules, config.shardProducts);

        public void BindGrantStore(IPurchaseGrantStore store)
        {
            if (grants != null) throw new InvalidOperationException("Purchase destination is already bound.");
            grants = new SessionGrants(store ?? throw new ArgumentNullException(nameof(store)));
        }

        public Task<ServiceResult> InitializeAsync()
        {
            if (!IsConfigured || grants == null) return Task.FromResult(ServiceResult.NotConfigured("Purchases"));
            if (!grants.IsCurrent) return Task.FromResult(ServiceResult.InvalidRequest(ServiceMessage.Get("service.purchase.destination-changed")));
            if (ready) return Task.FromResult(ServiceResult.Success());
            if (initialization == null || initialization.IsCompleted) initialization = ConnectAsync();
            return initialization;
        }

        async Task<ServiceResult> ConnectAsync()
        {
            try
            {
                var verifier = ShardPurchaseVerification.CreateVerifier(grants);
                if (verifier == null) return ServiceResult.NotConfigured("Purchases");
                fulfillment = new PurchaseFulfillment(verifier, grants, this);
                if (controller == null)
                {
                    controller = UnityIAPServices.StoreController();
                    controller.ProcessPendingOrdersOnPurchasesFetched(false);
                    controller.OnProductsFetched += OnProducts;
                    controller.OnProductsFetchFailed += OnProductsFailed;
                    controller.OnStoreDisconnected += OnDisconnected;
                    controller.OnPurchasePending += OnPending;
                    controller.OnPurchaseConfirmed += OnConfirmed;
                    controller.OnPurchaseFailed += OnFailed;
                    controller.OnPurchaseDeferred += OnDeferred;
                    controller.OnPurchasesFetched += OnRestored;
                    controller.OnPurchasesFetchFailed += OnRestoreFailed;
                }
                if (storeConnection == null || storeConnection.IsCompleted) storeConnection = ConnectStore();
                var connection = await Bounded(storeConnection, ServiceResult.Failed(ServiceMessage.Get("purchase.timeout")));
                if (!connection.succeeded) return connection;
                if (closed) return ServiceResult.Failed(ServiceMessage.Get("purchase.closed"));
                storeIds.Clear();
                foreach (var mapping in config.shardProducts)
                    storeIds.Add(mapping.catalogId, StoreName == "AppleAppStore" ? mapping.appleAppStoreId : mapping.googlePlayId);
                productsFetched = new TaskCompletionSource<ServiceResult>();
                controller.FetchProductsWithNoRetries(storeIds.Select(pair =>
                    new ProductDefinition(pair.Key, pair.Value, ProductType.Consumable)).ToList());
                var result = await Bounded(productsFetched.Task, ServiceResult.Failed(ServiceMessage.Get("purchase.timeout")));
                ready = result.succeeded && !closed && grants.IsCurrent;
                return ready ? result : result.succeeded
                    ? ServiceResult.InvalidRequest(ServiceMessage.Get("service.purchase.destination-changed")) : result;
            }
            catch (Exception exception)
            {
                LogFailure("connection", exception);
                return ServiceResult.Failed(ServiceMessage.Get("purchase.connect-failed"));
            }
        }

        public async Task<ServiceResult<IReadOnlyList<StoreProduct>>> FetchProductsAsync(IReadOnlyList<string> productIds)
        {
            if (rules == null || rules.data == null || rules.data.packs == null ||
                productIds == null || productIds.Count == 0 ||
                productIds.Any(id => !rules.data.packs.Any(pack => pack.id == id)))
                return ServiceResult<IReadOnlyList<StoreProduct>>.InvalidRequest(ServiceMessage.Get("service.purchase.unsupported"));
            var result = await InitializeAsync();
            if (!result.succeeded) return ServiceResult<IReadOnlyList<StoreProduct>>.Failed(result.text);
            var products = controller.GetProducts().Where(p => p.availableToPurchase &&
                productIds.Contains(p.definition.id) && !string.IsNullOrWhiteSpace(p.metadata.localizedPriceString))
                .Select(p => new StoreProduct(p.definition.id, p.metadata.localizedPriceString, p.metadata.localizedTitle)).ToList();
            if (products.Count == 0)
                return ServiceResult<IReadOnlyList<StoreProduct>>.Failed(ServiceMessage.Get("service.purchase.unsupported"));
            return ServiceResult<IReadOnlyList<StoreProduct>>.Success(products);
        }

        async Task<ServiceResult> ConnectStore()
        {
            try
            {
                await controller.Connect();
                return ServiceResult.Success();
            }
            catch (Exception exception)
            {
                LogFailure("native connection", exception);
                return ServiceResult.Failed(ServiceMessage.Get("purchase.connect-failed"));
            }
        }

        public async Task<ServiceResult<PurchaseReceipt>> PurchaseAsync(string productId)
        {
            var initialized = await InitializeAsync();
            if (!initialized.succeeded) return ServiceResult<PurchaseReceipt>.Failed(initialized.text);
            if (purchase != null || restored != null)
                return ServiceResult<PurchaseReceipt>.InvalidRequest(ServiceMessage.Get("service.purchase.busy"));
            var product = controller.GetProducts().FirstOrDefault(p => p.definition.id == productId && p.availableToPurchase);
            if (product == null || !storeIds.ContainsKey(productId))
                return ServiceResult<PurchaseReceipt>.InvalidRequest(ServiceMessage.Get("service.purchase.unsupported"));
            var completion = new TaskCompletionSource<ServiceResult<PurchaseReceipt>>();
            purchase = completion;
            purchasingId = productId;
            try
            {
                controller.PurchaseProduct(product);
                return await Bounded(completion.Task, ServiceResult<PurchaseReceipt>.Failed(ServiceMessage.Get("purchase.waiting")), 180);
            }
            catch (Exception exception)
            {
                LogFailure("payment", exception);
                purchase = null;
                purchasingId = null;
                return ServiceResult<PurchaseReceipt>.Failed(ServiceMessage.Get("purchase.connect-failed"));
            }
        }

        public async Task<ServiceResult<IReadOnlyList<PurchaseReceipt>>> RestoreAsync()
        {
            var initialized = await InitializeAsync();
            if (!initialized.succeeded) return ServiceResult<IReadOnlyList<PurchaseReceipt>>.Failed(initialized.text);
            if (purchase != null || restored != null)
                return ServiceResult<IReadOnlyList<PurchaseReceipt>>.InvalidRequest(ServiceMessage.Get("service.purchase.busy"));
            var completion = new TaskCompletionSource<ServiceResult<IReadOnlyList<PurchaseReceipt>>>();
            restored = completion;
            try
            {
                controller.FetchPurchases();
                return await Bounded(completion.Task,
                    ServiceResult<IReadOnlyList<PurchaseReceipt>>.Failed(ServiceMessage.Get("purchase.waiting")));
            }
            catch (Exception exception)
            {
                LogFailure("recovery", exception);
                restored = null;
                return ServiceResult<IReadOnlyList<PurchaseReceipt>>.Failed(ServiceMessage.Get("purchase.connect-failed"));
            }
        }

        [Serializable]
        public sealed class ReceiptEvidence
        {
            public string store, applicationId, unifiedReceipt, appleJws;
        }

        PurchaseReceipt Receipt(PendingOrder order)
        {
            var items = order.CartOrdered.Items().ToList();
            if (items.Count != 1 || items[0].Quantity != 1 ||
                !storeIds.ContainsKey(items[0].Product.definition.id) || string.IsNullOrWhiteSpace(order.Info.TransactionID))
                throw new InvalidOperationException("Unsupported shard purchase order.");
            string jws = order.Info.Apple != null ? order.Info.Apple.jwsRepresentation : null;
            if (string.IsNullOrWhiteSpace(jws) && string.IsNullOrWhiteSpace(order.Info.Receipt))
                throw new InvalidOperationException("Store returned no receipt evidence.");
            var evidence = JsonUtility.ToJson(new ReceiptEvidence
            {
                store = StoreName, applicationId = Application.identifier,
                unifiedReceipt = order.Info.Receipt, appleJws = jws
            });
            pending[order.Info.TransactionID] = order;
            return new PurchaseReceipt(items[0].Product.definition.id, order.Info.TransactionID, evidence);
        }

        Task<ServiceResult> Enqueue(PurchaseReceipt receipt)
        {
            if (fulfillmentProducts.TryGetValue(receipt.transactionId, out var product) && product != receipt.productId)
                return Task.FromResult(ServiceResult.InvalidRequest(ServiceMessage.Get("service.purchase.mismatch")));
            fulfillmentProducts[receipt.transactionId] = receipt.productId;
            if (fulfillmentTasks.TryGetValue(receipt.transactionId, out var existing)) return existing;
            var next = FulfillAfter(queue, receipt);
            fulfillmentTasks.Add(receipt.transactionId, next);
            queue = next;
            return next;
        }

        async Task<ServiceResult> FulfillAfter(Task previous, PurchaseReceipt receipt)
        {
            await previous;
            try
            {
                if (closed || !grants.IsCurrent) return ServiceResult.InvalidRequest(ServiceMessage.Get("service.purchase.destination-changed"));
                return await fulfillment.ProcessPendingAsync(receipt);
            }
            catch (Exception exception)
            {
                LogFailure("settlement", exception);
                return ServiceResult.Failed(ServiceMessage.Get("purchase.unsettled"));
            }
        }

        async void OnPending(PendingOrder order)
        {
            if (closed) return;
            try
            {
                var receipt = Receipt(order);
                var result = await Enqueue(receipt);
                if (receipt.productId == purchasingId)
                    CompletePurchase(result.succeeded ? ServiceResult<PurchaseReceipt>.Success(receipt) :
                        ServiceResult<PurchaseReceipt>.Failed(result.text));
                if (!result.succeeded) Debug.LogWarning("Frontier: pending purchase requires recovery (" + result.status + ").");
            }
            catch (Exception exception)
            {
                LogFailure("pending order", exception);
                CompletePurchase(ServiceResult<PurchaseReceipt>.Failed(ServiceMessage.Get("purchase.unsettled")));
            }
        }

        async void OnRestored(Orders orders)
        {
            if (closed) return;
            var completion = restored;
            var receipts = new List<PurchaseReceipt>();
            try
            {
                foreach (var order in orders.PendingOrders)
                {
                    var receipt = Receipt(order);
                    // A previous verification/save/ack failure must be retryable without buying again.
                    if (fulfillmentTasks.TryGetValue(receipt.transactionId, out var prior) && prior.IsCompleted && !prior.Result.succeeded)
                        fulfillmentTasks.Remove(receipt.transactionId);
                    var result = await Enqueue(receipt);
                    if (!result.succeeded)
                    {
                        completion?.TrySetResult(ServiceResult<IReadOnlyList<PurchaseReceipt>>.Failed(result.text));
                        return;
                    }
                    receipts.Add(receipt);
                }
                completion?.TrySetResult(ServiceResult<IReadOnlyList<PurchaseReceipt>>.Success(receipts));
            }
            catch (Exception exception)
            {
                LogFailure("pending recovery", exception);
                completion?.TrySetResult(ServiceResult<IReadOnlyList<PurchaseReceipt>>.Failed(ServiceMessage.Get("purchase.unsettled")));
            }
            finally { if (restored == completion) restored = null; }
        }

        public async Task<ServiceResult> ConfirmAsync(VerifiedPurchase verified)
        {
            if (closed || verified.store != StoreName || !pending.TryGetValue(verified.transactionId, out var order))
                return ServiceResult.InvalidRequest(ServiceMessage.Get("service.purchase.mismatch"));
            var completion = new TaskCompletionSource<ServiceResult>();
            confirmations[verified.transactionId] = completion;
            controller.ConfirmPurchase(order);
            var result = await Bounded(completion.Task, ServiceResult.Failed(ServiceMessage.Get("purchase.unsettled")));
            confirmations.Remove(verified.transactionId);
            if (result.succeeded) pending.Remove(verified.transactionId);
            return result;
        }

        void OnConfirmed(Order order)
        {
            if (!confirmations.TryGetValue(order.Info.TransactionID, out var completion)) return;
            completion.TrySetResult(order is ConfirmedOrder ? ServiceResult.Success() :
                ServiceResult.Failed(ServiceMessage.Get("purchase.unsettled")));
        }

        void CompletePurchase(ServiceResult<PurchaseReceipt> result)
        {
            var completion = purchase;
            purchase = null;
            purchasingId = null;
            completion?.TrySetResult(result);
        }

        void OnProducts(List<Product> products) => productsFetched?.TrySetResult(ServiceResult.Success());
        void OnProductsFailed(ProductFetchFailed error) => productsFetched?.TrySetResult(ServiceResult.Failed(ServiceMessage.Get("purchase.connect-failed")));
        bool IsCurrentOrder(Order order) => purchasingId != null &&
            order.CartOrdered.Items().Any(item => item.Product.definition.id == purchasingId);
        void OnFailed(FailedOrder order)
        {
            if (IsCurrentOrder(order))
                CompletePurchase(ServiceResult<PurchaseReceipt>.Failed(ServiceMessage.Get(
                    order.FailureReason == PurchaseFailureReason.UserCancelled ? "purchase.cancelled" : "purchase.unsettled")));
            else Debug.LogWarning("Frontier: store reported a failed purchase outside the current payment.");
        }
        void OnDeferred(DeferredOrder order)
        {
            if (IsCurrentOrder(order))
                CompletePurchase(ServiceResult<PurchaseReceipt>.Failed(ServiceMessage.Get("purchase.deferred")));
        }
        void OnRestoreFailed(PurchasesFetchFailureDescription error)
        {
            restored?.TrySetResult(ServiceResult<IReadOnlyList<PurchaseReceipt>>.Failed(ServiceMessage.Get("purchase.connect-failed")));
            restored = null;
        }
        void OnDisconnected(StoreConnectionFailureDescription error)
        {
            ready = false;
            productsFetched?.TrySetResult(ServiceResult.Failed(ServiceMessage.Get("purchase.connect-failed")));
            CompletePurchase(ServiceResult<PurchaseReceipt>.Failed(ServiceMessage.Get("purchase.unsettled")));
            OnRestoreFailed(null);
        }

        static async Task<T> Bounded<T>(Task<T> task, T failure, int seconds = 30)
        {
            return await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(seconds))) == task ? await task : failure;
        }

        static void LogFailure(string action, Exception exception)
        {
            // Provider exception messages may contain receipt evidence; never include them in logs.
            Debug.LogWarning("Frontier: purchase " + action + " failed (" + exception.GetType().Name + ").");
        }

        public void Shutdown()
        {
            closed = true;
            if (grants != null) grants.closed = true;
            productsFetched?.TrySetResult(ServiceResult.Failed(ServiceMessage.Get("purchase.closed")));
            CompletePurchase(ServiceResult<PurchaseReceipt>.Failed(ServiceMessage.Get("purchase.closed")));
            OnRestoreFailed(null);
            foreach (var completion in confirmations.Values)
                completion.TrySetResult(ServiceResult.Failed(ServiceMessage.Get("purchase.closed")));
            if (controller == null) return;
            controller.OnProductsFetched -= OnProducts;
            controller.OnProductsFetchFailed -= OnProductsFailed;
            controller.OnStoreDisconnected -= OnDisconnected;
            controller.OnPurchasePending -= OnPending;
            controller.OnPurchaseConfirmed -= OnConfirmed;
            controller.OnPurchaseFailed -= OnFailed;
            controller.OnPurchaseDeferred -= OnDeferred;
            controller.OnPurchasesFetched -= OnRestored;
            controller.OnPurchasesFetchFailed -= OnRestoreFailed;
        }

        sealed class SessionGrants : IPurchaseGrantStore
        {
            readonly IPurchaseGrantStore destination;
            readonly string context;
            public bool closed;
            public SessionGrants(IPurchaseGrantStore destination)
            {
                this.destination = destination;
                context = destination.ContextId;
            }
            public bool IsCurrent => !closed && !string.IsNullOrWhiteSpace(context) && destination.ContextId == context;
            public string ContextId => IsCurrent ? context : "";
            public ServiceResult CommitVerifiedPurchase(string expectedContextId, VerifiedPurchase verified)
            {
                if (!IsCurrent)
                    return ServiceResult.InvalidRequest(ServiceMessage.Get("service.purchase.destination-changed"));
                if (verified.store != (Application.platform == RuntimePlatform.IPhonePlayer ? "AppleAppStore" : "GooglePlay"))
                    return ServiceResult.InvalidRequest(ServiceMessage.Get("service.purchase.mismatch"));
                return destination.CommitVerifiedPurchase(expectedContextId, verified);
            }
        }
    }
}
