#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Threading.Tasks;

namespace Frontier.Services
{
    public sealed class DevelopmentAdsPreview : IAdsService, IRewardedAdPreview
    {
        public const int DurationSeconds = 5;
        public int PreviewSeconds { get { return DurationSeconds; } }
        public bool IsConfigured { get { return true; } }
        public Task<ServiceResult> InitializeAsync() { return Task.FromResult(ServiceResult.Success()); }
        public async Task<ServiceResult<RewardedAdResult>> ShowRewardedAsync(string placementId)
        {
            if (placementId != "double-yield" && placementId != "timer-skip" && placementId != "free-shards")
                return ServiceResult<RewardedAdResult>.InvalidRequest(ServiceMessage.Get("service.ads.unknown"));
            await Task.Delay(TimeSpan.FromSeconds(DurationSeconds));
            return ServiceResult<RewardedAdResult>.Success(new RewardedAdResult(true, placementId));
        }
    }
}
#endif
