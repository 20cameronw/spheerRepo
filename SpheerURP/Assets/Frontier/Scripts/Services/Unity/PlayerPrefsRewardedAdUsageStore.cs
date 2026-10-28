using System;
using UnityEngine;

namespace Frontier.Services.Unity
{
    public sealed class PlayerPrefsRewardedAdUsageStore : IRewardedAdUsageStore
    {
        readonly string prefix;

        public PlayerPrefsRewardedAdUsageStore(string prefix = "fr_rewarded_usage_")
        {
            if (string.IsNullOrWhiteSpace(prefix)) throw new ArgumentException("A preference prefix is required.", nameof(prefix));
            this.prefix = prefix;
        }

        public bool TryLoad(string placementId, out RewardedAdUsage usage, out string error)
        {
            usage = null;
            error = null;
            string key = prefix + placementId;
            if (!PlayerPrefs.HasKey(key))
            {
                usage = new RewardedAdUsage();
                return true;
            }
            try
            {
                string json = PlayerPrefs.GetString(key);
                if (string.IsNullOrWhiteSpace(json))
                {
                    error = "Stored rewarded-ad usage is empty; rewards remain unavailable.";
                    return false;
                }
                usage = new RewardedAdUsage
                {
                    version = 0, rewards = -1, dayUtcTicks = -1,
                    lastObservedUtcTicks = -1, lastRewardUtcTicks = -1
                };
                JsonUtility.FromJsonOverwrite(json, usage);
                if (usage.version == 1 && usage.rewards >= 0 && usage.dayUtcTicks >= 0 &&
                    usage.lastObservedUtcTicks >= 0 && usage.lastRewardUtcTicks >= 0) return true;
                error = "Stored rewarded-ad usage is incomplete; rewards remain unavailable.";
                return false;
            }
            catch (ArgumentException exception)
            {
                error = "Stored rewarded-ad usage is invalid: " + exception.Message;
                return false;
            }
        }

        public bool TrySave(string placementId, RewardedAdUsage usage, out string error)
        {
            error = null;
            try
            {
                PlayerPrefs.SetString(prefix + placementId, JsonUtility.ToJson(usage));
                PlayerPrefs.Save();
                return true;
            }
            catch (PlayerPrefsException exception)
            {
                error = "Rewarded-ad usage could not be saved: " + exception.Message;
                return false;
            }
        }
    }
}
