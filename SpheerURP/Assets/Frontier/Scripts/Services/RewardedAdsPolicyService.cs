using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Frontier.Services
{
    [Serializable]
    public sealed class RewardedAdPlacement
    {
        public string id;
        public bool unlimitedDaily;
        public int dailyCap;
        public int cooldownSeconds;
    }

    [Serializable]
    public sealed class RewardedAdUsage
    {
        public int version = 1;
        public long dayUtcTicks;
        public long lastObservedUtcTicks;
        public long lastRewardUtcTicks;
        public int rewards;
    }

    public interface IRewardedAdUsageStore
    {
        bool TryLoad(string placementId, out RewardedAdUsage usage, out string error);
        bool TrySave(string placementId, RewardedAdUsage usage, out string error);
    }

    /// <summary>Local placement limits, not authoritative cross-device ad or currency verification.</summary>
    public sealed class RewardedAdsPolicyService : IAdsService, IRewardedAdAvailability
    {
        readonly IAdsService inner;
        readonly IRewardedAdUsageStore store;
        readonly ITimeService time;
        readonly bool enabled;
        readonly Dictionary<string, RewardedAdPlacement> placements =
            new Dictionary<string, RewardedAdPlacement>(StringComparer.Ordinal);
        bool showing;

        public RewardedAdsPolicyService(IAdsService inner, IRewardedAdUsageStore store, ITimeService time,
            bool enabled, IEnumerable<RewardedAdPlacement> placements)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.time = time ?? throw new ArgumentNullException(nameof(time));
            this.enabled = enabled;
            if (placements == null) throw new ArgumentNullException(nameof(placements));
            foreach (var row in placements)
            {
                if (row == null || string.IsNullOrWhiteSpace(row.id) || row.dailyCap < 0 ||
                    row.cooldownSeconds < 0 || this.placements.ContainsKey(row.id))
                    throw new ArgumentException("Ad placements require unique IDs and non-negative limits.", nameof(placements));
                this.placements.Add(row.id, new RewardedAdPlacement
                {
                    id = row.id, unlimitedDaily = row.unlimitedDaily, dailyCap = row.dailyCap,
                    cooldownSeconds = row.cooldownSeconds
                });
            }
        }

        public bool IsConfigured { get { return enabled && inner.IsConfigured; } }

        public Task<ServiceResult> InitializeAsync()
        {
            return IsConfigured ? inner.InitializeAsync()
                : Task.FromResult(ServiceResult.NotConfigured("Rewarded ads"));
        }

        public async Task<ServiceResult<RewardedAdResult>> ShowRewardedAsync(string placementId)
        {
            var available = CheckAvailability(placementId);
            if (!available.succeeded)
            {
                if (available.status == ServiceStatus.NotConfigured)
                    return ServiceResult<RewardedAdResult>.NotConfigured("Rewarded ads");
                return available.status == ServiceStatus.Failed ? ServiceResult<RewardedAdResult>.Failed(available.text) :
                    ServiceResult<RewardedAdResult>.InvalidRequest(available.text);
            }
            showing = true;
            try
            {
                var usage = available.value;
                long now = time.UtcNow.UtcDateTime.Ticks;
                long day = new DateTime(now, DateTimeKind.Utc).Date.Ticks;
                int rewards = day == usage.dayUtcTicks ? usage.rewards : 0;

                var observed = new RewardedAdUsage
                {
                    dayUtcTicks = day, lastObservedUtcTicks = now,
                    lastRewardUtcTicks = usage.lastRewardUtcTicks, rewards = rewards
                };
                if (!store.TrySave(placementId, observed, out string error))
                    return ServiceResult<RewardedAdResult>.Failed(error);

                var result = await inner.ShowRewardedAsync(placementId);
                if (!result.succeeded) return result;
                if (result.value.placementId != placementId)
                    return ServiceResult<RewardedAdResult>.InvalidRequest(ServiceMessage.Get("service.ads.mismatch"));
                if (!result.value.rewardEarned) return result;
                long completed = time.UtcNow.UtcDateTime.Ticks;
                if (completed < now)
                    return ServiceResult<RewardedAdResult>.InvalidRequest(ServiceMessage.Get("service.ads.rewound"));
                long completedDay = new DateTime(completed, DateTimeKind.Utc).Date.Ticks;
                var earned = new RewardedAdUsage
                {
                    dayUtcTicks = completedDay, lastObservedUtcTicks = completed,
                    lastRewardUtcTicks = completed,
                    rewards = completedDay == day ? (int)Math.Min(int.MaxValue, (long)rewards + 1) : 1
                };
                if (!store.TrySave(placementId, earned, out error))
                    return ServiceResult<RewardedAdResult>.Failed(error);
                return result;
            }
            finally { showing = false; }
        }

        public ServiceResult RewardedAvailability(string placementId)
        {
            var result = CheckAvailability(placementId);
            if (result.succeeded) return ServiceResult.Success();
            if (result.status == ServiceStatus.NotConfigured) return ServiceResult.NotConfigured("Rewarded ads");
            return result.status == ServiceStatus.Failed ? ServiceResult.Failed(result.text) :
                ServiceResult.InvalidRequest(result.text);
        }

        ServiceResult<RewardedAdUsage> CheckAvailability(string placementId)
        {
            if (!IsConfigured) return ServiceResult<RewardedAdUsage>.NotConfigured("Rewarded ads");
            if (placementId == null || !placements.TryGetValue(placementId, out var placement))
                return ServiceResult<RewardedAdUsage>.InvalidRequest(ServiceMessage.Get("service.ads.unknown"));
            if (showing) return ServiceResult<RewardedAdUsage>.InvalidRequest(ServiceMessage.Get("service.ads.busy"));
            if (!placement.unlimitedDaily && placement.dailyCap == 0)
                return ServiceResult<RewardedAdUsage>.InvalidRequest(ServiceMessage.Get("service.ads.disabled"));
            if (!store.TryLoad(placementId, out var usage, out string error)) return ServiceResult<RewardedAdUsage>.Failed(error);
            long now = time.UtcNow.UtcDateTime.Ticks;
            if (!ValidUsage(usage) || now < usage.lastObservedUtcTicks)
                return ServiceResult<RewardedAdUsage>.InvalidRequest(ServiceMessage.Get("service.ads.invalid-time"));
            long day = new DateTime(now, DateTimeKind.Utc).Date.Ticks;
            if (!placement.unlimitedDaily && day == usage.dayUtcTicks && usage.rewards >= placement.dailyCap)
                return ServiceResult<RewardedAdUsage>.InvalidRequest(ServiceMessage.Get("service.ads.limit"));
            if (usage.lastRewardUtcTicks > 0 &&
                now - usage.lastRewardUtcTicks < (long)placement.cooldownSeconds * TimeSpan.TicksPerSecond)
                return ServiceResult<RewardedAdUsage>.InvalidRequest(ServiceMessage.Get("service.ads.cooldown"));
            return ServiceResult<RewardedAdUsage>.Success(usage);
        }

        static bool ValidUsage(RewardedAdUsage usage)
        {
            return usage != null && usage.version == 1 && usage.rewards >= 0 &&
                usage.dayUtcTicks >= 0 && usage.dayUtcTicks <= DateTime.MaxValue.Ticks &&
                usage.dayUtcTicks % TimeSpan.TicksPerDay == 0 &&
                usage.lastRewardUtcTicks >= 0 && usage.lastRewardUtcTicks <= usage.lastObservedUtcTicks &&
                usage.lastObservedUtcTicks <= DateTime.MaxValue.Ticks &&
                usage.dayUtcTicks == new DateTime(usage.lastObservedUtcTicks, DateTimeKind.Utc).Date.Ticks &&
                (usage.rewards == 0 || usage.lastRewardUtcTicks >= usage.dayUtcTicks);
        }
    }
}
