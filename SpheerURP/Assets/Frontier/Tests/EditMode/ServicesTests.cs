using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Frontier.Services;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class ServicesTests
    {
        sealed class MemoryConsentStore : IConsentStore
        {
            public ConsentPreferences saved;
            public ConsentPreferences Load() { return saved; }
            public void Save(ConsentPreferences preferences) { saved = preferences; }
        }

        sealed class TestAdsService : IAdsService
        {
            public bool IsConfigured { get { return true; } }
            public int initializeCount;
            public int showCount;
            public Task<ServiceResult> InitializeAsync()
            {
                initializeCount++;
                return Task.FromResult(ServiceResult.Success());
            }
            public Task<ServiceResult<RewardedAdResult>> ShowRewardedAsync(string placementId)
            {
                showCount++;
                return Task.FromResult(ServiceResult<RewardedAdResult>.Success(new RewardedAdResult(true, placementId)));
            }
        }

        sealed class TestAnalyticsService : IAnalyticsService
        {
            public bool IsConfigured { get { return true; } }
            public int initializeCount;
            public int eventCount;
            public Task<ServiceResult> InitializeAsync()
            {
                initializeCount++;
                return Task.FromResult(ServiceResult.Success());
            }
            public ServiceResult RecordEvent(string eventName, IReadOnlyDictionary<string, string> parameters)
            {
                eventCount++;
                return ServiceResult.Success();
            }
        }

        sealed class TestAuthService : IAuthService
        {
            public bool IsConfigured { get { return true; } }
            public int signInCount;
            public Task<ServiceResult> InitializeAsync() { return Task.FromResult(ServiceResult.Success()); }
            public Task<ServiceResult<AuthIdentity>> SignInAnonymouslyAsync()
            {
                signInCount++;
                return Task.FromResult(ServiceResult<AuthIdentity>.Success(new AuthIdentity("player", true)));
            }
            public Task<ServiceResult> DeleteAccountAsync() { return Task.FromResult(ServiceResult.Success()); }
        }

        sealed class TestLeaderboardService : ILeaderboardService
        {
            public bool IsConfigured { get { return true; } }
            public int readCount;
            public int submitCount;
            public Task<ServiceResult> InitializeAsync() { return Task.FromResult(ServiceResult.Success()); }
            public Task<ServiceResult<IReadOnlyList<LeaderboardEntry>>> GetScoresAsync(string boardId, int limit)
            {
                readCount++;
                return Task.FromResult(ServiceResult<IReadOnlyList<LeaderboardEntry>>.Success(
                    new List<LeaderboardEntry>()));
            }
            public Task<ServiceResult> SubmitScoreAsync(string boardId, long score)
            {
                submitCount++;
                return Task.FromResult(ServiceResult.Success());
            }
        }

        static FrontierServices Services(IConsentService consent, IAdsService ads, IAnalyticsService analytics)
        {
            var unavailable = new UnavailableFrontierProvider();
            return new FrontierServices(consent, ads, unavailable, unavailable, unavailable, unavailable,
                analytics, unavailable, new LocalTimeService(), unavailable);
        }

        [Test]
        public void Consent_DefaultsToOptOut_AndPersistsTimestampedChoices()
        {
            var store = new MemoryConsentStore();
            var fixedTime = new DateTimeOffset(2026, 10, 14, 12, 30, 0, TimeSpan.Zero);
            var consent = new ConsentService(store, () => fixedTime);

            Assert.IsFalse(consent.Current.hasDecision);
            Assert.IsFalse(consent.Current.analyticsAllowed);
            Assert.IsFalse(consent.Current.adsAllowed);

            consent.SavePreferences(true, false, false);
            Assert.IsTrue(consent.Current.hasDecision);
            Assert.IsTrue(consent.Current.analyticsAllowed);
            Assert.IsFalse(consent.Current.adsAllowed);
            Assert.IsFalse(consent.Current.onlineServicesAllowed);
            Assert.AreEqual(fixedTime.UtcDateTime.Ticks, store.saved.decisionUtcTicks);

            var restored = new ConsentService(store, () => fixedTime.AddDays(1));
            Assert.IsTrue(restored.Current.analyticsAllowed);
            Assert.IsFalse(restored.Current.adsAllowed);
            Assert.IsFalse(restored.Current.onlineServicesAllowed);
            restored.Reset();
            Assert.IsFalse(restored.Current.hasDecision);
            Assert.IsFalse(store.saved.analyticsAllowed);
            Assert.IsFalse(store.saved.onlineServicesAllowed);
        }

        [Test]
        public void Consent_PolicyVersionChangeRequiresNewChoice()
        {
            var store = new MemoryConsentStore();
            store.saved = new ConsentPreferences(1, true, true, true, true, 1);

            var consent = new ConsentService(store);

            Assert.AreEqual(ConsentService.CurrentPolicyVersion, consent.Current.policyVersion);
            Assert.IsFalse(consent.Current.hasDecision);
            Assert.IsFalse(consent.Current.analyticsAllowed);
            Assert.IsFalse(consent.Current.adsAllowed);
            Assert.IsFalse(consent.Current.onlineServicesAllowed);
        }

        [Test]
        public void PlayerPrefsConsentStore_RoundTripsOnlineServicesChoice()
        {
            const string policyKey = "fr_consent_policy";
            const string decisionKey = "fr_consent_decided";
            const string analyticsKey = "fr_consent_analytics";
            const string adsKey = "fr_consent_ads";
            const string onlineKey = "fr_consent_online_services";
            const string ticksKey = "fr_consent_utc_ticks";

            string[] keys = { policyKey, decisionKey, analyticsKey, adsKey, onlineKey, ticksKey };
            var existed = new bool[keys.Length];
            var ints = new int[keys.Length];
            var strings = new string[keys.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                existed[i] = PlayerPrefs.HasKey(keys[i]);
                ints[i] = PlayerPrefs.GetInt(keys[i]);
                strings[i] = PlayerPrefs.GetString(keys[i]);
            }

            try
            {
                var expected = new ConsentPreferences(ConsentService.CurrentPolicyVersion, true,
                    analyticsAllowed: true, adsAllowed: false, onlineServicesAllowed: true, decisionUtcTicks: 638960000000000000L);
                var store = new PlayerPrefsConsentStore();
                store.Save(expected);
                var actual = store.Load();

                Assert.IsTrue(actual.hasDecision);
                Assert.IsTrue(actual.analyticsAllowed);
                Assert.IsFalse(actual.adsAllowed);
                Assert.IsTrue(actual.onlineServicesAllowed);
                Assert.AreEqual(expected.policyVersion, actual.policyVersion);
                Assert.AreEqual(expected.decisionUtcTicks, actual.decisionUtcTicks);
            }
            finally
            {
                for (int i = 0; i < keys.Length; i++)
                {
                    if (!existed[i])
                    {
                        PlayerPrefs.DeleteKey(keys[i]);
                    }
                    else if (i == keys.Length - 1)
                    {
                        PlayerPrefs.SetString(keys[i], strings[i]);
                    }
                    else
                    {
                        PlayerPrefs.SetInt(keys[i], ints[i]);
                    }
                }
                PlayerPrefs.Save();
            }
        }

        [Test]
        public void AdsAndAnalytics_CannotInitializeOrSendUntilOptedIn()
        {
            var consent = new ConsentService(new MemoryConsentStore());
            var ads = new TestAdsService();
            var analytics = new TestAnalyticsService();
            var services = Services(consent, ads, analytics);

            Assert.AreEqual(ServiceStatus.ConsentRequired, services.Ads.InitializeAsync().Result.status);
            Assert.AreEqual(ServiceStatus.ConsentRequired, services.Ads.ShowRewardedAsync("free-shards").Result.status);
            Assert.AreEqual(ServiceStatus.ConsentRequired, services.Analytics.InitializeAsync().Result.status);
            Assert.AreEqual(ServiceStatus.ConsentRequired, services.Analytics.RecordEvent("session_start", null).status);
            Assert.AreEqual(0, ads.initializeCount);
            Assert.AreEqual(0, ads.showCount);
            Assert.AreEqual(0, analytics.initializeCount);
            Assert.AreEqual(0, analytics.eventCount);

            consent.SavePreferences(true, false, false);
            Assert.AreEqual(ServiceStatus.Success, services.Analytics.InitializeAsync().Result.status);
            Assert.AreEqual(ServiceStatus.Success, services.Analytics.RecordEvent("session_start", null).status);
            Assert.AreEqual(ServiceStatus.ConsentRequired, services.Ads.InitializeAsync().Result.status);
            Assert.AreEqual(1, analytics.initializeCount);
            Assert.AreEqual(1, analytics.eventCount);
            Assert.AreEqual(0, ads.initializeCount);

            consent.SavePreferences(true, true, false);
            Assert.AreEqual(ServiceStatus.Success, services.Ads.InitializeAsync().Result.status);
            var reward = services.Ads.ShowRewardedAsync("free-shards").Result;
            Assert.AreEqual(ServiceStatus.Success, reward.status);
            Assert.IsTrue(reward.value.rewardEarned);
            Assert.AreEqual(1, ads.initializeCount);
            Assert.AreEqual(1, ads.showCount);

            consent.SavePreferences(false, false, false);
            Assert.AreEqual(ServiceStatus.ConsentRequired, services.Ads.ShowRewardedAsync("free-shards").Result.status);
            Assert.AreEqual(ServiceStatus.ConsentRequired, services.Analytics.RecordEvent("session_end", null).status);
            Assert.AreEqual(1, ads.showCount);
            Assert.AreEqual(1, analytics.eventCount);
        }

        [Test]
        public void OnlineServices_CannotInitializeOrExchangeDataUntilOptedIn_AndStopAfterWithdrawal()
        {
            var consent = new ConsentService(new MemoryConsentStore());
            var auth = new TestAuthService();
            var leaderboards = new TestLeaderboardService();
            var unavailable = new UnavailableFrontierProvider();
            var services = new FrontierServices(consent, unavailable, unavailable, auth, leaderboards,
                unavailable, unavailable, unavailable, new LocalTimeService(), unavailable);

            Assert.AreEqual(ServiceStatus.ConsentRequired, services.Authentication.InitializeAsync().Result.status);
            Assert.AreEqual(ServiceStatus.ConsentRequired, services.Authentication.SignInAnonymouslyAsync().Result.status);
            Assert.AreEqual(ServiceStatus.ConsentRequired, services.Leaderboards.InitializeAsync().Result.status);
            Assert.AreEqual(ServiceStatus.ConsentRequired, services.Leaderboards.GetScoresAsync("weekly", 10).Result.status);
            Assert.AreEqual(ServiceStatus.ConsentRequired, services.Leaderboards.SubmitScoreAsync("weekly", 100).Result.status);
            Assert.AreEqual(ServiceStatus.ConsentRequired, services.CloudSave.ReadAsync().Result.status);
            Assert.AreEqual(0, auth.signInCount);
            Assert.AreEqual(0, leaderboards.readCount);
            Assert.AreEqual(0, leaderboards.submitCount);

            consent.SavePreferences(false, false, true);
            Assert.AreEqual(ServiceStatus.Success, services.Authentication.SignInAnonymouslyAsync().Result.status);
            Assert.AreEqual(ServiceStatus.Success, services.Leaderboards.GetScoresAsync("weekly", 10).Result.status);
            Assert.AreEqual(ServiceStatus.Success, services.Leaderboards.SubmitScoreAsync("weekly", 100).Result.status);
            Assert.AreEqual(1, auth.signInCount);
            Assert.AreEqual(1, leaderboards.readCount);
            Assert.AreEqual(1, leaderboards.submitCount);

            consent.SavePreferences(false, false, false);
            Assert.AreEqual(ServiceStatus.ConsentRequired, services.Authentication.SignInAnonymouslyAsync().Result.status);
            Assert.AreEqual(ServiceStatus.ConsentRequired, services.Leaderboards.SubmitScoreAsync("weekly", 101).Result.status);
            Assert.AreEqual(1, auth.signInCount);
            Assert.AreEqual(1, leaderboards.submitCount);
        }

        [Test]
        public void LeaderboardRequests_RejectInvalidBoundsBeforeProviderCalls()
        {
            var consent = new ConsentService(new MemoryConsentStore());
            consent.SavePreferences(false, false, true);
            var leaderboards = new TestLeaderboardService();
            var unavailable = new UnavailableFrontierProvider();
            var services = new FrontierServices(consent, unavailable, unavailable, unavailable,
                leaderboards, unavailable, unavailable, unavailable, new LocalTimeService(), unavailable);

            Assert.AreEqual(ServiceStatus.InvalidRequest, services.Leaderboards.GetScoresAsync("", 10).Result.status);
            Assert.AreEqual(ServiceStatus.InvalidRequest, services.Leaderboards.GetScoresAsync("weekly", 0).Result.status);
            Assert.AreEqual(ServiceStatus.InvalidRequest, services.Leaderboards.SubmitScoreAsync("weekly", -1).Result.status);
            Assert.AreEqual(0, leaderboards.readCount);
            Assert.AreEqual(0, leaderboards.submitCount);
        }

        [Test]
        public void OfflineServices_ReportUnavailableInsteadOfSimulatingSuccess()
        {
            var consent = new ConsentService(new MemoryConsentStore());
            consent.SavePreferences(true, true, true);
            var services = FrontierServices.CreateOffline(consent);

            Assert.IsFalse(services.Ads.IsConfigured);
            Assert.AreEqual(ServiceStatus.NotConfigured, services.Ads.InitializeAsync().Result.status);
            Assert.AreEqual(ServiceStatus.NotConfigured, services.Purchases.InitializeAsync().Result.status);
            Assert.AreEqual(ServiceStatus.NotConfigured, services.Authentication.InitializeAsync().Result.status);
            Assert.AreEqual(ServiceStatus.NotConfigured, services.Leaderboards.InitializeAsync().Result.status);
            Assert.AreEqual(ServiceStatus.NotConfigured, services.CloudSave.InitializeAsync().Result.status);
            Assert.AreEqual(ServiceStatus.NotConfigured, services.Analytics.InitializeAsync().Result.status);
            Assert.AreEqual(ServiceStatus.NotConfigured, services.RemoteConfig.InitializeAsync().Result.status);
            Assert.AreEqual(ServiceStatus.NotConfigured, services.Notifications.InitializeAsync().Result.status);
            Assert.IsFalse(services.Time.IsServerTrusted);
        }
    }
}
