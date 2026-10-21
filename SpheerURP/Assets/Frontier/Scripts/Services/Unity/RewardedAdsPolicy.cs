using System;
using UnityEngine;

namespace Frontier.Services.Unity
{
    [CreateAssetMenu(menuName = "Frontier/Services/Rewarded Ads Policy")]
    public sealed class RewardedAdsPolicy : ScriptableObject
    {
        public bool enableRewardedAds;
        public string iosAppKey;
        public string iosRewardedAdUnitId;
        [Tooltip("Enable only after audience/privacy review and physical iOS test-device verification. Requires a public HTTPS privacy policy.")]
        public bool iosSetupVerified;
        public RewardedAdPlacement[] placements = Array.Empty<RewardedAdPlacement>();

        public bool CanUseIos(FrontierUnityServicesConfig config)
        {
            return enableRewardedAds && iosSetupVerified && config != null && config.HasValidPrivacyPolicy &&
                !string.IsNullOrWhiteSpace(iosAppKey) && !string.IsNullOrWhiteSpace(iosRewardedAdUnitId);
        }

        public IAdsService Wrap(IAdsService provider, IRewardedAdUsageStore store, ITimeService time)
        {
            return new RewardedAdsPolicyService(provider, store, time, enableRewardedAds, placements);
        }
    }
}
