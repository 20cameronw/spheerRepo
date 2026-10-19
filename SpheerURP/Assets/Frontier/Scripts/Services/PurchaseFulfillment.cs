using System;
using System.Threading.Tasks;

namespace Frontier.Services
{
    public readonly struct VerifiedPurchase
    {
        public readonly string store;
        public readonly string productId;
        public readonly string transactionId;

        public VerifiedPurchase(string store, string productId, string transactionId)
        {
            this.store = store;
            this.productId = productId;
            this.transactionId = transactionId;
        }
    }

    public interface IPurchaseReceiptVerifier
    {
        Task<ServiceResult<VerifiedPurchase>> VerifyAsync(PurchaseReceipt receipt);
    }

    public interface IPurchaseGrantStore
    {
        /// <summary>
        /// The identity of the destination wallet/save. Must change when it is reset, restored or replaced.
        /// </summary>
        string ContextId { get; }

        /// <summary>
        /// Atomically persists the catalog-owned reward and the store/transaction journal key. Existing matching
        /// transactions return success without granting again. Mismatched products or failed writes return failure.
        /// Success must mean durable storage, not an in-memory mutation awaiting an autosave.
        /// </summary>
        ServiceResult CommitVerifiedPurchase(string expectedContextId, VerifiedPurchase purchase);
    }

    public interface IPurchaseAcknowledger
    {
        Task<ServiceResult> ConfirmAsync(VerifiedPurchase purchase);
    }

    /// <summary>
    /// The pending-purchase protocol. Store adapters supply verification, atomic reward persistence and
    /// acknowledgement; none of these dependencies is configured by the offline services factory.
    /// </summary>
    public sealed class PurchaseFulfillment
    {
        readonly IPurchaseReceiptVerifier verifier;
        readonly IPurchaseGrantStore grants;
        readonly IPurchaseAcknowledger acknowledger;
        bool processing;

        public PurchaseFulfillment(IPurchaseReceiptVerifier verifier, IPurchaseGrantStore grants,
            IPurchaseAcknowledger acknowledger)
        {
            this.verifier = verifier ?? throw new ArgumentNullException(nameof(verifier));
            this.grants = grants ?? throw new ArgumentNullException(nameof(grants));
            this.acknowledger = acknowledger ?? throw new ArgumentNullException(nameof(acknowledger));
        }

        public async Task<ServiceResult> ProcessPendingAsync(PurchaseReceipt receipt)
        {
            if (string.IsNullOrWhiteSpace(receipt.productId) || string.IsNullOrWhiteSpace(receipt.transactionId) ||
                string.IsNullOrWhiteSpace(receipt.receiptPayload))
                return ServiceResult.InvalidRequest("A purchase product, transaction ID and receipt payload are required.");
            if (processing)
                return ServiceResult.InvalidRequest("A purchase is already being processed. Keep this purchase pending and retry.");
            string contextId = grants.ContextId;
            if (string.IsNullOrWhiteSpace(contextId))
                return ServiceResult.InvalidRequest("A persistent purchase destination is required.");

            processing = true;
            try
            {
                var verified = await verifier.VerifyAsync(receipt);
                if (!verified.succeeded)
                    return ServiceResult.Failed("Purchase verification did not complete: " + verified.message);
                var purchase = verified.value;
                if (string.IsNullOrWhiteSpace(purchase.store) ||
                    !string.Equals(receipt.productId, purchase.productId, StringComparison.Ordinal) ||
                    !string.Equals(receipt.transactionId, purchase.transactionId, StringComparison.Ordinal))
                    return ServiceResult.InvalidRequest("The verified purchase does not match the pending store transaction.");
                if (!string.Equals(contextId, grants.ContextId, StringComparison.Ordinal))
                    return ServiceResult.InvalidRequest("The purchase destination changed. Keep this purchase pending and retry.");

                var committed = grants.CommitVerifiedPurchase(contextId, purchase);
                if (!committed.succeeded) return committed;
                var confirmed = await acknowledger.ConfirmAsync(purchase);
                if (!confirmed.succeeded)
                    return ServiceResult.Failed("The purchase reward is saved, but store confirmation is pending. Retry without granting again. " +
                        confirmed.message);
                return ServiceResult.Success();
            }
            finally { processing = false; }
        }
    }
}
