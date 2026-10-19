using System;
using System.Collections.Generic;
using Frontier.Services;

namespace Frontier
{
    public partial class GameState : IPurchaseGrantStore
    {
        bool HasPurchaseHistory { get { return purchaseGrants != null && purchaseGrants.Count != 0; } }

        string IPurchaseGrantStore.ContextId { get { return purchaseContextId; } }

        /// <summary>
        /// Only an actual receipt verifier may supply this call. The offline factory never creates a fulfillment
        /// flow; reset/cloud recovery still require store reconciliation before purchases can be enabled.
        /// </summary>
        ServiceResult IPurchaseGrantStore.CommitVerifiedPurchase(string expectedContextId, VerifiedPurchase purchase)
        {
            if (SaveIsReadOnly)
                return ServiceResult.InvalidRequest("Purchases cannot be granted while this save is read-only.");
            if (!string.Equals(expectedContextId, purchaseContextId, StringComparison.Ordinal))
                return ServiceResult.InvalidRequest("The purchase destination changed. Retry this pending purchase.");
            if (string.IsNullOrWhiteSpace(purchase.store) || string.IsNullOrWhiteSpace(purchase.transactionId) ||
                string.IsNullOrWhiteSpace(purchase.productId))
                return ServiceResult.InvalidRequest("The verified purchase identity is incomplete.");

            PurchaseGrantRecord existing = null;
            foreach (var grant in purchaseGrants)
            {
                if (grant == null || string.IsNullOrWhiteSpace(grant.store) ||
                    string.IsNullOrWhiteSpace(grant.transactionId) || string.IsNullOrWhiteSpace(grant.productId) ||
                    grant.shards <= 0)
                    return ServiceResult.InvalidRequest("The purchase journal is invalid. Store reconciliation is required.");
                if (grant.store != purchase.store || grant.transactionId != purchase.transactionId) continue;
                if (existing != null || grant.productId != purchase.productId)
                    return ServiceResult.InvalidRequest("The purchase journal conflicts with this transaction. Store reconciliation is required.");
                existing = grant;
            }
            if (existing != null)
                return ServiceResult.Success();

            var packs = db.shardRules != null && db.shardRules.data != null ? db.shardRules.data.packs : null;
            int reward = 0;
            if (packs != null)
                foreach (var pack in packs)
                {
                    if (pack == null || pack.id != purchase.productId) continue;
                    if (reward != 0 || pack.shards <= 0)
                        return ServiceResult.InvalidRequest("The purchased shard pack has invalid catalog rewards.");
                    reward = pack.shards;
                }
            if (reward <= 0)
                return ServiceResult.InvalidRequest("This store product has no supported catalog reward.");
            if ((long)ShardBalance + reward > int.MaxValue)
                return ServiceResult.InvalidRequest("The shard wallet is full. Keep the purchase pending until there is room.");

            // Detach every field: a failed write must not grant spendable shards or mark a transaction fulfilled.
            var candidate = SaveSerializer.Instance.DeserializeV2(SaveSerializer.Instance.Serialize(BuildSave())).Normalize();
            candidate.purchaseGrants.Add(new PurchaseGrantRecord
            {
                store = purchase.store, productId = purchase.productId,
                transactionId = purchase.transactionId, shards = reward
            });
            WalletEntry destination = null;
            foreach (var entry in candidate.wallet)
                if (entry != null && entry.currency == ShardCurrency) { destination = entry; break; }
            if (destination == null)
            {
                destination = new WalletEntry { currency = ShardCurrency };
                candidate.wallet.Add(destination);
            }
            destination.amount = Math.Max(0, destination.amount) + reward;
            if (!saves.CommitSnapshot(candidate))
                return ServiceResult.Failed("The purchase reward was not granted because its save failed. Keep the store transaction pending. " +
                    saves.LastError);

            wallet = new List<WalletEntry>();
            foreach (var entry in candidate.wallet)
                wallet.Add(entry == null ? null : new WalletEntry { currency = entry.currency, amount = entry.amount });
            purchaseGrants = new List<PurchaseGrantRecord>();
            foreach (var grant in candidate.purchaseGrants)
                purchaseGrants.Add(new PurchaseGrantRecord
                {
                    store = grant.store, productId = grant.productId,
                    transactionId = grant.transactionId, shards = grant.shards
                });
            if (Changed != null) Changed();
            return ServiceResult.Success();
        }
    }
}
