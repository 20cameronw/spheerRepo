using System.Globalization;
using Frontier.Services;
using UnityEngine;

namespace Frontier
{
    public sealed class PlayerPrefsConsentStore : IConsentStore
    {
        const string PolicyVersionKey = "fr_consent_policy";
        const string HasDecisionKey = "fr_consent_decided";
        const string AnalyticsKey = "fr_consent_analytics";
        const string AdsKey = "fr_consent_ads";
        const string OnlineServicesKey = "fr_consent_online_services";
        const string DecisionUtcTicksKey = "fr_consent_utc_ticks";

        public ConsentPreferences Load()
        {
            long ticks;
            if (!long.TryParse(PlayerPrefs.GetString(DecisionUtcTicksKey, "0"), NumberStyles.None,
                    CultureInfo.InvariantCulture, out ticks))
                ticks = 0L;
            return new ConsentPreferences(
                PlayerPrefs.GetInt(PolicyVersionKey, 0),
                PlayerPrefs.GetInt(HasDecisionKey, 0) == 1,
                PlayerPrefs.GetInt(AnalyticsKey, 0) == 1,
                PlayerPrefs.GetInt(AdsKey, 0) == 1,
                PlayerPrefs.GetInt(OnlineServicesKey, 0) == 1,
                ticks);
        }

        public void Save(ConsentPreferences preferences)
        {
            PlayerPrefs.SetInt(PolicyVersionKey, preferences.policyVersion);
            PlayerPrefs.SetInt(HasDecisionKey, preferences.hasDecision ? 1 : 0);
            PlayerPrefs.SetInt(AnalyticsKey, preferences.analyticsAllowed ? 1 : 0);
            PlayerPrefs.SetInt(AdsKey, preferences.adsAllowed ? 1 : 0);
            PlayerPrefs.SetInt(OnlineServicesKey, preferences.onlineServicesAllowed ? 1 : 0);
            PlayerPrefs.SetString(DecisionUtcTicksKey, preferences.decisionUtcTicks.ToString(CultureInfo.InvariantCulture));
            PlayerPrefs.Save();
        }
    }
}
