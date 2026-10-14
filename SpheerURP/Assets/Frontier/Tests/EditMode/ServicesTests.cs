using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Frontier.Services;
using NUnit.Framework;

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

            consent.SavePreferences(true, false);
            Assert.IsTrue(consent.Current.hasDecision);
            Assert.IsTrue(consent.Current.analyticsAllowed);
            Assert.IsFalse(consent.Current.adsAllowed);
            Assert.AreEqual(fixedTime.UtcDateTime.Ticks, store.saved.decisionUtcTicks);

            var restored = new ConsentService(store, () => fixedTime.AddDays(1));
            Assert.IsTrue(restored.Current.analyticsAllowed);
            Assert.IsFalse(restored.Current.adsAllowed);
            restored.Reset();
            Assert.IsFalse(restored.Current.hasDecision);
            Assert.IsFalse(store.saved.analyticsAllowed);
        }

        [Test]
        public void Consent_PolicyVersionChangeRequiresNewChoice()
        {
            var store = new MemoryConsentStore();
            store.saved = new ConsentPreferences(0, true, true, true, 1);

            var consent = new ConsentService(store);

            Assert.AreEqual(ConsentService.CurrentPolicyVersion, consent.Current.policyVersion);
            Assert.IsFalse(consent.Current.hasDecision);
            Assert.IsFalse(consent.Current.analyticsAllowed);
            Assert.IsFalse(consent.Current.adsAllowed);
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

            consent.SavePreferences(true, false);
            Assert.AreEqual(ServiceStatus.Success, services.Analytics.InitializeAsync().Result.status);
            Assert.AreEqual(ServiceStatus.Success, services.Analytics.RecordEvent("session_start", null).status);
            Assert.AreEqual(ServiceStatus.ConsentRequired, services.Ads.InitializeAsync().Result.status);
            Assert.AreEqual(1, analytics.initializeCount);
            Assert.AreEqual(1, analytics.eventCount);
            Assert.AreEqual(0, ads.initializeCount);

            consent.SavePreferences(true, true);
            Assert.AreEqual(ServiceStatus.Success, services.Ads.InitializeAsync().Result.status);
            var reward = services.Ads.ShowRewardedAsync("free-shards").Result;
            Assert.AreEqual(ServiceStatus.Success, reward.status);
            Assert.IsTrue(reward.value.rewardEarned);
            Assert.AreEqual(1, ads.initializeCount);
            Assert.AreEqual(1, ads.showCount);

            consent.SavePreferences(false, false);
            Assert.AreEqual(ServiceStatus.ConsentRequired, services.Ads.ShowRewardedAsync("free-shards").Result.status);
            Assert.AreEqual(ServiceStatus.ConsentRequired, services.Analytics.RecordEvent("session_end", null).status);
            Assert.AreEqual(1, ads.showCount);
            Assert.AreEqual(1, analytics.eventCount);
        }

        [Test]
        public void OfflineServices_ReportUnavailableInsteadOfSimulatingSuccess()
        {
            var consent = new ConsentService(new MemoryConsentStore());
            consent.SavePreferences(true, true);
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
