using System;
using UnityEngine;

namespace Frontier.Services.Unity
{
    [CreateAssetMenu(menuName = "Frontier/Services/Rewarded Ads Policy")]
    public sealed class RewardedAdsPolicy : ScriptableObject
    {
        public bool enableRewardedAds;
        public RewardedAdPlacement[] placements = Array.Empty<RewardedAdPlacement>();

        public IAdsService Wrap(IAdsService provider, IRewardedAdUsageStore store, ITimeService time)
        {
            return new RewardedAdsPolicyService(provider, store, time, enableRewardedAds, placements);
        }
    }
}
