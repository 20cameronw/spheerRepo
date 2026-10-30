using System;
using System.Linq;
using UnityEngine;

namespace Frontier
{
    [Serializable]
    public sealed class GameplayRewardTicket
    {
        public string id, placement, state, resourceId, planetId, endUtc, changedEndUtc;
        public int amount, slot;
    }

    public partial class GameState
    {
        const string RewardTicketKey = "player.reward-ticket";
        string rewardRequestContext = Guid.NewGuid().ToString("N");
        string reportedInvalidReward;
        public string RewardRequestContext { get { return rewardRequestContext; } }

        public GameplayRewardTicket GameplayReward
        {
            get
            {
                var entry = settings.FirstOrDefault(s => s != null && s.key == RewardTicketKey);
                if (entry == null || string.IsNullOrEmpty(entry.value)) return null;
                return ReadGameplayReward(entry.value);
            }
        }

        GameplayRewardTicket ReadGameplayReward(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                var ticket = JsonUtility.FromJson<GameplayRewardTicket>(json);
                bool final = ticket != null && (ticket.state == "settled" || ticket.state == "cancelled");
                if (ticket != null && !string.IsNullOrEmpty(ticket.id) && (final ||
                    (ticket.state == "pending" || ticket.state == "earned") && ticket.amount > 0 &&
                    (ticket.placement == "double-yield" && !string.IsNullOrEmpty(ticket.resourceId) ||
                    ticket.placement == "free-shards" && string.IsNullOrEmpty(ticket.resourceId) &&
                    string.IsNullOrEmpty(ticket.planetId) ||
                    ticket.placement == "timer-skip" && !string.IsNullOrEmpty(ticket.planetId) && ticket.slot >= 0 &&
                    UtcText.TryParse(ticket.endUtc, out var end) && UtcText.TryParse(ticket.changedEndUtc, out var changed) &&
                    (end - changed).TotalSeconds == ticket.amount))) return ticket;
                if (reportedInvalidReward != json)
                {
                    reportedInvalidReward = json;
                    Debug.LogWarning("Frontier: unreadable gameplay reward journal: invalid fields.");
                }
            }
            catch (ArgumentException error)
            {
                if (reportedInvalidReward != json)
                {
                    reportedInvalidReward = json;
                    Debug.LogWarning("Frontier: unreadable gameplay reward journal: " + error.Message);
                }
            }
            return new GameplayRewardTicket { state = "invalid" };
        }

        bool RewardRestoreBlocked(SaveV2 snapshot)
        {
            bool Outstanding(GameplayRewardTicket ticket) =>
                ticket != null && ticket.state != "settled" && ticket.state != "cancelled";
            if (Outstanding(GameplayReward)) return true;
            var incoming = snapshot.settings.FirstOrDefault(s => s != null && s.key == RewardTicketKey);
            return incoming != null && Outstanding(ReadGameplayReward(incoming.value));
        }

        bool CommitRewardJournal(SaveV2 candidate, GameplayRewardTicket ticket, out string error)
        {
            error = null;
            var entry = candidate.settings.FirstOrDefault(s => s != null && s.key == RewardTicketKey);
            if (entry == null) { entry = new SettingEntry { key = RewardTicketKey }; candidate.settings.Add(entry); }
            entry.value = JsonUtility.ToJson(ticket);
            if (!saves.CommitSnapshot(candidate)) { error = UiText.Get("reward.save-failed", saves.LastError); return false; }
            SetSetting(RewardTicketKey, entry.value);
            return true;
        }

        public bool PrepareGameplayReward(string placement, ResourceSO resource, int amount, PlacedStructure job,
            out GameplayRewardTicket ticket, out string error)
        {
            ticket = null;
            error = UiText.Get("reward.changed");
            var previous = GameplayReward;
            if (SaveIsReadOnly || PendingTravel != null || ClockRewindDetected ||
                previous != null && previous.state != "settled" && previous.state != "cancelled") return false;
            var quote = new GameplayRewardTicket { id = Guid.NewGuid().ToString("N"),
                placement = placement, amount = amount, state = "pending" };
            if (placement == "double-yield")
            {
                if (resource == null || db.FindResource(resource.id) != resource || IsTankFuel(resource.id) || amount <= 0 ||
                    (long)FreeSpace(resource) < (long)amount * 2) return false;
                quote.resourceId = resource.id;
            }
            else if (placement == "timer-skip")
            {
                if (!IsBuilderBusy(job) || amount <= 0 || amount > BuilderJobSecondsRemaining(job)) return false;
                quote.planetId = job.planetId; quote.slot = job.slot;
                quote.endUtc = IsUnderConstruction(job) ? job.record.buildEndUtc : job.record.upgradeEndUtc;
                if (!UtcText.TryParse(quote.endUtc, out var end)) return false;
                quote.changedEndUtc = UtcText.Format(end.AddSeconds(-amount));
            }
            else if (placement == "free-shards")
            {
                if (resource != null || job != null || db.shardRules == null || db.shardRules.data == null ||
                    amount != db.shardRules.data.adReward || amount <= 0 ||
                    (long)ShardBalance + amount > int.MaxValue) return false;
            }
            else return false;
            var candidate = SaveSerializer.Instance.DeserializeV2(SaveSerializer.Instance.Serialize(BuildSave())).Normalize();
            if (placement == "double-yield")
            {
                AddRewardInventory(candidate, resource.id, amount);
                candidate.stats.RecordHarvestRunCompleted();
            }
            if (!CommitRewardJournal(candidate, quote, out error)) return false;
            if (placement == "double-yield")
            {
                int added = backpack.Add(resource.id, amount);
                if (added != amount) throw new InvalidOperationException("Persisted base harvest no longer fits.");
                stats.RecordHarvestRunCompleted();
                RecordAtlasResourceAcquired(resource.id);
            }
            ticket = quote;
            Notify();
            return true;
        }

        void AddRewardInventory(SaveV2 candidate, string resourceId, int amount)
        {
            var record = candidate.player.inventory.FirstOrDefault(s => s.resourceId == resourceId);
            if (record == null) candidate.player.inventory.Add(new StackRecord { resourceId = resourceId, count = amount });
            else record.count = checked(record.count + amount);
            string key = AtlasKey(AtlasResourceType, resourceId);
            if (candidate.atlas.Contains(key)) return;
            candidate.atlas.Add(key);
            int shards = AtlasReward(AtlasResourceType);
            if (shards <= 0) return;
            var walletEntry = candidate.wallet.FirstOrDefault(e => e.currency == ShardCurrency);
            if (walletEntry == null) { walletEntry = new WalletEntry { currency = ShardCurrency }; candidate.wallet.Add(walletEntry); }
            walletEntry.amount = (int)Math.Min(int.MaxValue, (long)Math.Max(0, walletEntry.amount) + shards);
        }

        public bool ResolveGameplayReward(string context, string id, bool earned, out string error)
        {
            error = UiText.Get("reward.changed");
            var ticket = GameplayReward;
            if (context != rewardRequestContext || ticket == null || ticket.id != id || SaveIsReadOnly) return false;
            if (ticket.state == "settled") return true;
            if (ticket.state == "cancelled") return !earned;
            if (ticket.state != "pending") return false;
            ticket.state = earned ? "earned" : "cancelled";
            var candidate = SaveSerializer.Instance.DeserializeV2(SaveSerializer.Instance.Serialize(BuildSave())).Normalize();
            if (!CommitRewardJournal(candidate, ticket, out error)) return false;
            return !earned || SettleGameplayReward(out error);
        }

        public bool SettleGameplayReward(out string error)
        {
            error = UiText.Get("reward.changed");
            var ticket = GameplayReward;
            if (SaveIsReadOnly || ticket == null || ticket.state != "earned") return false;
            var candidate = SaveSerializer.Instance.DeserializeV2(SaveSerializer.Instance.Serialize(BuildSave())).Normalize();
            PlacedStructure job = null;
            if (ticket.placement == "double-yield")
            {
                var resource = db.FindResource(ticket.resourceId);
                if (resource == null || ticket.amount <= 0 || IsTankFuel(resource.id) || FreeSpace(resource) < ticket.amount)
                { error = UiText.Get("reward.space"); return false; }
                AddRewardInventory(candidate, resource.id, ticket.amount);
            }
            else if (ticket.placement == "free-shards")
            {
                if (db.shardRules == null || db.shardRules.data == null ||
                    ticket.amount != db.shardRules.data.adReward || !AddRewardShards(candidate, ticket.amount))
                { error = UiText.Get("shards.ad.wallet-full"); return false; }
            }
            else if (ticket.placement == "timer-skip")
            {
                job = structures.FirstOrDefault(s => s.planetId == ticket.planetId && s.slot == ticket.slot);
                if (job == null || job.record.buildEndUtc != ticket.endUtc && job.record.upgradeEndUtc != ticket.endUtc)
                { error = UiText.Get("reward.job-changed"); return false; }
                var delta = candidate.worlds.First(w => w.planetId == ticket.planetId);
                var record = delta.structures.First(s => s.slot == ticket.slot);
                if (record.buildEndUtc == ticket.endUtc) record.buildEndUtc = ticket.changedEndUtc;
                else record.upgradeEndUtc = ticket.changedEndUtc;
                var assignment = candidate.builders.FirstOrDefault(b => b.target == BuilderTarget(ticket.planetId, ticket.slot));
                if (assignment == null) return false;
                assignment.busyUntilUtc = ticket.changedEndUtc;
            }
            else return false;
            ticket.state = "settled";
            if (!CommitRewardJournal(candidate, ticket, out error)) return false;
            if (job != null)
            {
                if (job.record.buildEndUtc == ticket.endUtc) job.record.buildEndUtc = ticket.changedEndUtc;
                else job.record.upgradeEndUtc = ticket.changedEndUtc;
                builders.First(b => b.target == BuilderTarget(job.planetId, job.slot)).busyUntilUtc = ticket.changedEndUtc;
                world.Touch();
                CompleteBuilderJobs();
            }
            else if (ticket.placement == "double-yield")
            {
                int granted = backpack.Add(ticket.resourceId, ticket.amount);
                if (granted != ticket.amount) throw new InvalidOperationException("Persisted ad reward no longer fits.");
                RecordAtlasResourceAcquired(ticket.resourceId);
            }
            else AddShards(ticket.amount);
            Notify();
            return true;
        }

        static bool AddRewardShards(SaveV2 candidate, int amount)
        {
            if (candidate == null || candidate.wallet == null || amount <= 0) return false;
            long total = 0;
            WalletEntry destination = null;
            foreach (var entry in candidate.wallet)
            {
                if (entry == null || entry.currency != ShardCurrency) continue;
                if (entry.amount > 0) total += entry.amount;
                if (destination == null) destination = entry;
            }
            if (total + amount > int.MaxValue) return false;
            if (destination == null)
            {
                destination = new WalletEntry { currency = ShardCurrency };
                candidate.wallet.Add(destination);
            }
            destination.amount = checked(Math.Max(0, destination.amount) + amount);
            return true;
        }

        public bool CancelUnverifiedGameplayReward(out string error)
        {
            error = UiText.Get("reward.changed");
            var ticket = GameplayReward;
            if (SaveIsReadOnly || ticket == null || ticket.state != "pending" && ticket.state != "earned" && ticket.state != "invalid") return false;
            // A killed request is never assumed earned. A stale completed job can be explicitly dismissed.
            ticket.state = "cancelled";
            if (string.IsNullOrEmpty(ticket.id)) ticket.id = Guid.NewGuid().ToString("N");
            var candidate = SaveSerializer.Instance.DeserializeV2(SaveSerializer.Instance.Serialize(BuildSave())).Normalize();
            if (!CommitRewardJournal(candidate, ticket, out error)) return false;
            Notify();
            return true;
        }
    }
}
