using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Frontier.Services.Unity
{
    [CreateAssetMenu(menuName = "Frontier/Services/Rewarded Ads Policy")]
    public sealed class RewardedAdsPolicy : ScriptableObject
    {
        public bool enableRewardedAds;
        public string iosAppKey;
        public string iosRewardedAdUnitId;
        [FormerlySerializedAs("iosSetupVerified")]
        [Tooltip("Enable iOS gameplay rewarded ads. Requires player opt-in and a public HTTPS privacy policy; this flag does not certify device testing or compliance.")]
        public bool enableIosGameplayAds;
        [Tooltip("Expose the LevelPlay test suite in iOS development builds only. Does not enable gameplay ads.")]
        public bool enableIosDevelopmentTests;
        public RewardedAdPlacement[] placements = Array.Empty<RewardedAdPlacement>();

        public bool CanUseIos(FrontierUnityServicesConfig config)
        {
            return enableRewardedAds && enableIosGameplayAds && config != null && config.HasValidPrivacyPolicy &&
                !string.IsNullOrWhiteSpace(iosAppKey) && !string.IsNullOrWhiteSpace(iosRewardedAdUnitId);
        }

        public bool CanTestIos(FrontierUnityServicesConfig config, RuntimePlatform platform, bool developmentBuild)
        {
            return enableRewardedAds && enableIosDevelopmentTests && developmentBuild &&
                platform == RuntimePlatform.IPhonePlayer && config != null && config.HasValidPrivacyPolicy &&
                !string.IsNullOrWhiteSpace(iosAppKey) && !string.IsNullOrWhiteSpace(iosRewardedAdUnitId);
        }

        public IAdsService Wrap(IAdsService provider, IRewardedAdUsageStore store, ITimeService time)
        {
            return new RewardedAdsPolicyService(provider, store, time, enableRewardedAds, placements);
        }
    }
}
