using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Frontier.Services;
using Frontier.Services.Unity;
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
            public TaskCompletionSource<ServiceResult<RewardedAdResult>> pendingShow;
            public Task<ServiceResult> InitializeAsync()
            {
                initializeCount++;
                return Task.FromResult(ServiceResult.Success());
            }
            public Task<ServiceResult<RewardedAdResult>> ShowRewardedAsync(string placementId)
            {
                showCount++;
                if (pendingShow != null) return pendingShow.Task;
                return Task.FromResult(ServiceResult<RewardedAdResult>.Success(new RewardedAdResult(true, placementId)));
            }
        }

        sealed class TestAnalyticsService : IAnalyticsService
        {
            public bool IsConfigured { get { return true; } }
            public int initializeCount;
            public int eventCount;
            public bool dataCollectionAllowed;
            public void SetDataCollectionAllowed(bool allowed) { dataCollectionAllowed = allowed; }
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

        sealed class TestRemoteConfigService : IRemoteConfigService
        {
            public bool IsConfigured { get { return true; } }
            public int initializeCount;
            public int fetchCount;
            public TaskCompletionSource<ServiceResult<IReadOnlyDictionary<string, string>>> pendingFetch;
            public Task<ServiceResult> InitializeAsync()
            {
                initializeCount++;
                return Task.FromResult(ServiceResult.Success());
            }
            public Task<ServiceResult<IReadOnlyDictionary<string, string>>> FetchAsync()
            {
                fetchCount++;
                if (pendingFetch != null) return pendingFetch.Task;
                IReadOnlyDictionary<string, string> settings =
                    new Dictionary<string, string> { { "harvest_yield", "1.25" } };
                return Task.FromResult(ServiceResult<IReadOnlyDictionary<string, string>>.Success(settings));
            }
        }

        sealed class TestAuthService : IAuthService
        {
            public string playerId = "player";
            public string CurrentPlayerId { get { return playerId; } }
            public bool IsConfigured { get { return true; } }
            public bool CanRecoverAccount { get { return true; } }
            public string RecoveryProviderName { get { return "Test"; } }
            public int signInCount;
            public int linkRecoveryCount;
            public int recoverySignInCount;
            public Task<ServiceResult> InitializeAsync() { return Task.FromResult(ServiceResult.Success()); }
            public Task<ServiceResult<AuthIdentity>> SignInAnonymouslyAsync()
            {
                signInCount++;
                return Task.FromResult(ServiceResult<AuthIdentity>.Success(new AuthIdentity("player", true)));
            }
            public Task<ServiceResult<AuthIdentity>> LinkRecoveryAccountAsync()
            {
                linkRecoveryCount++;
                return Task.FromResult(ServiceResult<AuthIdentity>.Success(new AuthIdentity("player", false)));
            }
            public Task<ServiceResult<AuthIdentity>> SignInWithRecoveryAccountAsync()
            {
                recoverySignInCount++;
                return Task.FromResult(ServiceResult<AuthIdentity>.Success(new AuthIdentity("player", false)));
            }
            public int deleteCount;
            public ServiceResult deleteResult = ServiceResult.Success();
            public Task<ServiceResult> DeleteAccountAsync(string expectedPlayerId)
            {
                Assert.AreEqual(playerId, expectedPlayerId);
                deleteCount++;
                return Task.FromResult(deleteResult);
            }
        }

        sealed class TestLeaderboardService : ILeaderboardService
        {
            public bool IsConfigured { get { return true; } }
            public int readCount;
            public Task<ServiceResult> InitializeAsync() { return Task.FromResult(ServiceResult.Success()); }
            public Task<ServiceResult<IReadOnlyList<LeaderboardEntry>>> GetScoresAsync(string boardId, int limit)
            {
                readCount++;
                return Task.FromResult(ServiceResult<IReadOnlyList<LeaderboardEntry>>.Success(
                    new List<LeaderboardEntry>()));
            }
        }

        sealed class TestCloudSaveService : ICloudSaveService
        {
            public bool IsConfigured { get { return true; } }
            public int readCount;
            public int writeCount;
            public int deleteCount;
            public ServiceResult deleteResult = ServiceResult.Success();
            public Func<Task<ServiceResult>> delete;
            public Task<ServiceResult> DeleteSaveAsync(string expectedPlayerId)
            {
                Assert.AreEqual("player", expectedPlayerId);
                deleteCount++;
                return delete != null ? delete() : Task.FromResult(deleteResult);
            }
            public Task<ServiceResult> InitializeAsync() { return Task.FromResult(ServiceResult.Success()); }
            public Task<ServiceResult<CloudSaveRecord>> ReadAsync()
            {
                readCount++;
                return Task.FromResult(ServiceResult<CloudSaveRecord>.Success(
                    new CloudSaveRecord(true, "{}", "read-lock")));
            }
            public Task<ServiceResult<CloudSaveRecord>> WriteAsync(string payload, string writeLock)
            {
                writeCount++;
                return Task.FromResult(ServiceResult<CloudSaveRecord>.Success(
                    new CloudSaveRecord(true, payload, "next-lock")));
            }
        }

        static FrontierServices Services(IConsentService consent, IAdsService ads, IAnalyticsService analytics)
        {
            var unavailable = new UnavailableFrontierProvider();
            return new FrontierServices(consent, ads, unavailable, unavailable, unavailable, unavailable,
                analytics, unavailable, new LocalTimeService(), unavailable);
        }

        static FrontierServices DeletionServices(TestAuthService auth, TestCloudSaveService cloud)
        {
            var unavailable = new UnavailableFrontierProvider();
            return new FrontierServices(new ConsentService(new MemoryConsentStore()), unavailable, unavailable,
                auth, unavailable, cloud, unavailable, unavailable, new LocalTimeService(), unavailable);
        }

        [Test]
        public async Task AccountDeletion_RemainsAvailableAfterWithdrawalAndNeverSignsInOrUploads()
        {
            var auth = new TestAuthService();
            var cloud = new TestCloudSaveService();
            var services = DeletionServices(auth, cloud);
            Assert.IsFalse(services.Consent.Current.onlineServicesAllowed);
            cloud.delete = () =>
            {
                Assert.AreEqual(0, auth.deleteCount, "Cloud removal must precede account deletion.");
                return Task.FromResult(ServiceResult.Success());
            };
            var result = await services.DeleteCurrentAccountAndSaveAsync("player");
            Assert.IsTrue(result.succeeded, result.message);
            Assert.AreEqual(1, cloud.deleteCount);
            Assert.AreEqual(1, auth.deleteCount);
            Assert.AreEqual(0, auth.signInCount + auth.recoverySignInCount + auth.linkRecoveryCount);
            Assert.AreEqual(0, cloud.writeCount);
            Assert.IsFalse(services.AccountDeletionBusy);
            Assert.AreEqual(ServiceStatus.ConsentRequired, (await services.CloudSave.ReadAsync()).status);
        }

        [Test]
        public async Task AccountDeletion_RejectsMissingChangedAndUnconfiguredIdentityBeforeDeleting()
        {
            var auth = new TestAuthService();
            var cloud = new TestCloudSaveService();
            var services = DeletionServices(auth, cloud);
            foreach (string id in new[] { null, "", "different-player" })
                Assert.AreEqual(ServiceStatus.InvalidRequest, (await services.DeleteCurrentAccountAndSaveAsync(id)).status);
            auth.playerId = "";
            Assert.AreEqual(ServiceStatus.InvalidRequest, (await services.DeleteCurrentAccountAndSaveAsync("player")).status);
            Assert.AreEqual(0, cloud.deleteCount + auth.deleteCount);
            var offline = FrontierServices.CreateOffline(new ConsentService(new MemoryConsentStore()));
            Assert.AreEqual(ServiceStatus.NotConfigured, (await offline.DeleteCurrentAccountAndSaveAsync("player")).status);
        }

        [Test]
        public async Task AccountDeletion_CloudFailurePreservesAccountAndAllowsExplicitRetry()
        {
            var auth = new TestAuthService();
            var cloud = new TestCloudSaveService { deleteResult = ServiceResult.Failed("Write-lock conflict") };
            var services = DeletionServices(auth, cloud);
            var result = await services.DeleteCurrentAccountAndSaveAsync("player");
            Assert.IsFalse(result.succeeded);
            StringAssert.Contains("Write-lock conflict", result.message);
            Assert.AreEqual(0, auth.deleteCount);
            cloud.deleteResult = ServiceResult.Success();
            Assert.IsTrue((await services.DeleteCurrentAccountAndSaveAsync("player")).succeeded);
            Assert.AreEqual(2, cloud.deleteCount);
            Assert.AreEqual(1, auth.deleteCount);
        }

        [Test]
        public async Task AccountDeletion_ReportsPartialFailureAndRechecksIdentityAfterCloudRemoval()
        {
            var auth = new TestAuthService { deleteResult = ServiceResult.Failed("Account offline") };
            var cloud = new TestCloudSaveService();
            var services = DeletionServices(auth, cloud);
            var result = await services.DeleteCurrentAccountAndSaveAsync("player");
            Assert.IsFalse(result.succeeded);
            StringAssert.Contains("cloud save was deleted", result.message);
            StringAssert.Contains("Account offline", result.message);
            cloud.delete = () => { auth.playerId = "other"; return Task.FromResult(ServiceResult.Success()); };
            result = await services.DeleteCurrentAccountAndSaveAsync("player");
            Assert.IsFalse(result.succeeded);
            StringAssert.Contains("No account was deleted", result.message);
            Assert.AreEqual(1, auth.deleteCount);
        }

        [Test]
        public async Task AccountDeletion_BlocksDuplicatesAndSurfacesThrownProviderErrors()
        {
            var auth = new TestAuthService();
            var pending = new TaskCompletionSource<ServiceResult>();
            var cloud = new TestCloudSaveService { delete = () => pending.Task };
            var services = DeletionServices(auth, cloud);
            var first = services.DeleteCurrentAccountAndSaveAsync("player");
            Assert.IsTrue(services.AccountDeletionBusy);
            Assert.AreEqual(ServiceStatus.InvalidRequest, (await services.DeleteCurrentAccountAndSaveAsync("player")).status);
            Assert.AreEqual(1, cloud.deleteCount);
            pending.SetException(new InvalidOperationException("Provider unavailable"));
            var result = await first;
            Assert.IsFalse(result.succeeded);
            StringAssert.Contains("before cloud-save removal was confirmed", result.message);
            StringAssert.Contains("Provider unavailable", result.message);
            Assert.AreEqual(0, auth.deleteCount);
            Assert.IsFalse(services.AccountDeletionBusy);
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
            Assert.IsFalse(analytics.dataCollectionAllowed);

            consent.SavePreferences(true, false, false);
            Assert.IsTrue(analytics.dataCollectionAllowed);
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
            Assert.IsFalse(analytics.dataCollectionAllowed);
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task RewardedAds_DiscardInFlightRewardsAfterConsentWithdrawal(bool optBackIn)
        {
            var consent = new ConsentService(new MemoryConsentStore());
            consent.SavePreferences(false, true, false);
            var ads = new TestAdsService
            {
                pendingShow = new TaskCompletionSource<ServiceResult<RewardedAdResult>>()
            };
            var services = Services(consent, ads, new TestAnalyticsService());
            var request = services.Ads.ShowRewardedAsync("free-shards");
            Assert.AreEqual(1, ads.showCount);
            consent.SavePreferences(false, false, false);
            if (optBackIn) consent.SavePreferences(false, true, false);
            ads.pendingShow.SetResult(ServiceResult<RewardedAdResult>.Success(
                new RewardedAdResult(true, "free-shards")));
            var result = await request;
            Assert.AreEqual(ServiceStatus.ConsentRequired, result.status);
            Assert.IsFalse(result.value.rewardEarned);
        }

        [Test]
        public async Task RemoteConfig_RequiresOnlineConsentAndStopsAfterWithdrawal()
        {
            var consent = new ConsentService(new MemoryConsentStore());
            var remoteConfig = new TestRemoteConfigService();
            var unavailable = new UnavailableFrontierProvider();
            var services = new FrontierServices(consent, unavailable, unavailable, unavailable,
                unavailable, unavailable, unavailable, remoteConfig, new LocalTimeService(), unavailable);

            Assert.AreEqual(ServiceStatus.ConsentRequired,
                (await services.RemoteConfig.InitializeAsync()).status);
            Assert.AreEqual(ServiceStatus.ConsentRequired,
                (await services.RemoteConfig.FetchAsync()).status);
            Assert.AreEqual(0, remoteConfig.initializeCount + remoteConfig.fetchCount);

            consent.SavePreferences(false, false, true);
            Assert.AreEqual(ServiceStatus.Success,
                (await services.RemoteConfig.InitializeAsync()).status);
            ServiceResult<IReadOnlyDictionary<string, string>> result =
                await services.RemoteConfig.FetchAsync();
            Assert.AreEqual(ServiceStatus.Success, result.status);
            Assert.AreEqual("1.25", result.value["harvest_yield"]);
            Assert.AreEqual(1, remoteConfig.initializeCount);
            Assert.AreEqual(1, remoteConfig.fetchCount);

            remoteConfig.pendingFetch =
                new TaskCompletionSource<ServiceResult<IReadOnlyDictionary<string, string>>>();
            Task<ServiceResult<IReadOnlyDictionary<string, string>>> pending =
                services.RemoteConfig.FetchAsync();
            consent.SavePreferences(false, false, false);
            remoteConfig.pendingFetch.SetResult(ServiceResult<IReadOnlyDictionary<string, string>>.Success(
                new Dictionary<string, string> { { "harvest_yield", "2.0" } }));
            Assert.AreEqual(ServiceStatus.ConsentRequired, (await pending).status,
                "A response must not be applied after online consent is withdrawn.");
            Assert.AreEqual(ServiceStatus.ConsentRequired,
                (await services.RemoteConfig.FetchAsync()).status);
            Assert.AreEqual(2, remoteConfig.fetchCount);
        }

        [Test]
        public async Task RemoteConfig_WithdrawalAndReOptInStillDiscardThePendingResponse()
        {
            var consent = new ConsentService(new MemoryConsentStore());
            consent.SavePreferences(false, false, true);
            var remote = new TestRemoteConfigService
            {
                pendingFetch = new TaskCompletionSource<ServiceResult<IReadOnlyDictionary<string, string>>>()
            };
            var unavailable = new UnavailableFrontierProvider();
            var services = new FrontierServices(consent, unavailable, unavailable, unavailable,
                unavailable, unavailable, unavailable, remote, new LocalTimeService(), unavailable);
            var pending = services.RemoteConfig.FetchAsync();
            consent.SavePreferences(false, false, false);
            consent.SavePreferences(false, false, true);
            remote.pendingFetch.SetResult(ServiceResult<IReadOnlyDictionary<string, string>>.Success(
                new Dictionary<string, string> { { "travel.k", "0.5" } }));
            Assert.AreEqual(ServiceStatus.ConsentRequired, (await pending).status);
            remote.pendingFetch = null;
            Assert.IsTrue((await services.RemoteConfig.FetchAsync()).succeeded);
        }

        [Test]
        public void OnlineServices_CannotInitializeOrExchangeDataUntilOptedIn_AndStopAfterWithdrawal()
        {
            var consent = new ConsentService(new MemoryConsentStore());
            var auth = new TestAuthService();
            var leaderboards = new TestLeaderboardService();
            var cloudSave = new TestCloudSaveService();
            var unavailable = new UnavailableFrontierProvider();
            var services = new FrontierServices(consent, unavailable, unavailable, auth, leaderboards,
                cloudSave, unavailable, unavailable, new LocalTimeService(), unavailable);

            Assert.AreEqual(ServiceStatus.ConsentRequired, services.Authentication.InitializeAsync().Result.status);
            Assert.AreEqual(ServiceStatus.ConsentRequired, services.Authentication.SignInAnonymouslyAsync().Result.status);
            Assert.AreEqual(ServiceStatus.ConsentRequired, services.Leaderboards.InitializeAsync().Result.status);
            Assert.AreEqual(ServiceStatus.ConsentRequired, services.Leaderboards.GetScoresAsync("weekly", 10).Result.status);
            Assert.AreEqual(ServiceStatus.ConsentRequired, services.CloudSave.ReadAsync().Result.status);
            Assert.AreEqual(ServiceStatus.ConsentRequired, services.CloudSave.WriteAsync("{}", string.Empty).Result.status);
            Assert.IsFalse(new CloudSaveRecord(false, string.Empty, string.Empty).hasValue);
            Assert.AreEqual(0, auth.signInCount);
            Assert.AreEqual(0, leaderboards.readCount);
            Assert.AreEqual(0, cloudSave.readCount);
            Assert.AreEqual(0, cloudSave.writeCount);

            consent.SavePreferences(false, false, true);
            Assert.AreEqual(ServiceStatus.Success, services.Authentication.SignInAnonymouslyAsync().Result.status);
            Assert.AreEqual(ServiceStatus.Success, services.Leaderboards.GetScoresAsync("weekly", 10).Result.status);
            Assert.AreEqual(ServiceStatus.Success, services.CloudSave.ReadAsync().Result.status);
            ServiceResult<CloudSaveRecord> cloudWrite = services.CloudSave.WriteAsync("{}", "read-lock").Result;
            Assert.AreEqual(ServiceStatus.Success, cloudWrite.status);
            Assert.AreEqual("next-lock", cloudWrite.value.writeLock);
            Assert.AreEqual(1, auth.signInCount);
            Assert.AreEqual(1, leaderboards.readCount);
            Assert.AreEqual(1, cloudSave.readCount);
            Assert.AreEqual(1, cloudSave.writeCount);

            consent.SavePreferences(false, false, false);
            Assert.AreEqual(ServiceStatus.ConsentRequired, services.Authentication.SignInAnonymouslyAsync().Result.status);
            Assert.AreEqual(1, auth.signInCount);
            Assert.AreEqual(ServiceStatus.ConsentRequired, services.CloudSave.ReadAsync().Result.status);
            Assert.AreEqual(ServiceStatus.ConsentRequired, services.CloudSave.WriteAsync("{}", "read-lock").Result.status);
            Assert.AreEqual(1, cloudSave.readCount);
            Assert.AreEqual(1, cloudSave.writeCount);
        }

        [Test]
        public void AccountRecovery_RequiresOnlineConsentForLinkAndRestoreSignIn()
        {
            var consent = new ConsentService(new MemoryConsentStore());
            var authentication = new TestAuthService();
            var unavailable = new UnavailableFrontierProvider();
            var services = new FrontierServices(consent, unavailable, unavailable, authentication,
                unavailable, unavailable, unavailable, unavailable, new LocalTimeService(), unavailable);

            Assert.IsTrue(services.Authentication.CanRecoverAccount);
            Assert.AreEqual("Test", services.Authentication.RecoveryProviderName);
            Assert.AreEqual(ServiceStatus.ConsentRequired,
                services.Authentication.LinkRecoveryAccountAsync().Result.status);
            Assert.AreEqual(ServiceStatus.ConsentRequired,
                services.Authentication.SignInWithRecoveryAccountAsync().Result.status);
            Assert.AreEqual(0, authentication.linkRecoveryCount);
            Assert.AreEqual(0, authentication.recoverySignInCount);

            consent.SavePreferences(false, false, true);
            Assert.AreEqual(ServiceStatus.Success,
                services.Authentication.LinkRecoveryAccountAsync().Result.status);
            Assert.AreEqual(ServiceStatus.Success,
                services.Authentication.SignInWithRecoveryAccountAsync().Result.status);
            Assert.AreEqual(1, authentication.linkRecoveryCount);
            Assert.AreEqual(1, authentication.recoverySignInCount);

            consent.SavePreferences(false, false, false);
            Assert.AreEqual(ServiceStatus.ConsentRequired,
                services.Authentication.LinkRecoveryAccountAsync().Result.status);
            Assert.AreEqual(ServiceStatus.ConsentRequired,
                services.Authentication.SignInWithRecoveryAccountAsync().Result.status);
            Assert.AreEqual(1, authentication.linkRecoveryCount);
            Assert.AreEqual(1, authentication.recoverySignInCount);
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
            Assert.AreEqual(0, leaderboards.readCount);
        }

        [Test]
        public void LeaderboardClientContract_IsReadOnlyUntilServerValidatedSubmissionExists()
        {
            Assert.IsNull(typeof(ILeaderboardService).GetMethod("SubmitScoreAsync"));
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

        [TestCase("https://example.com/privacy", true)]
        [TestCase("http://example.com/privacy", false)]
        [TestCase("javascript:alert(1)", false)]
        [TestCase("", false)]
        public void UnityServicesConfig_OnlyAcceptsHttpsPrivacyPolicy(string url, bool expected)
        {
            var config = ScriptableObject.CreateInstance<FrontierUnityServicesConfig>();
            try
            {
                config.enableOnlineServices = false;
                config.privacyPolicyUrl = url;
                Assert.AreEqual(expected, config.HasValidPrivacyPolicy);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void UnityServicesConfig_AccountRecoveryRequiresExplicitConfiguration()
        {
            var config = ScriptableObject.CreateInstance<FrontierUnityServicesConfig>();
            try
            {
                config.enableOnlineServices = true;
                config.enableAccountRecovery = true;
                config.privacyPolicyUrl = "https://example.com/privacy";
                Assert.IsTrue(config.CanUseAccountRecovery);

                config.enableAccountRecovery = false;
                Assert.IsFalse(config.CanUseAccountRecovery);
                config.enableAccountRecovery = true;
                config.enableOnlineServices = false;
                Assert.IsFalse(config.CanUseAccountRecovery);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void UnityServicesConfig_AnalyticsAndRemoteConfigRemainDisabledByDefault()
        {
            var config = ScriptableObject.CreateInstance<FrontierUnityServicesConfig>();
            try
            {
                Assert.IsFalse(config.enableAnalytics);
                Assert.IsFalse(config.enableRemoteConfig);
                Assert.IsFalse(config.CanUseAnalytics);
                Assert.IsFalse(config.CanUseRemoteConfig);

                config.enableAnalytics = true;
                Assert.AreEqual(!string.IsNullOrWhiteSpace(Application.cloudProjectId),
                    config.CanUseAnalytics);
                config.enableRemoteConfig = true;
                config.enableOnlineServices = true;
                config.privacyPolicyUrl = "https://example.com/privacy";
                Assert.AreEqual(!string.IsNullOrWhiteSpace(Application.cloudProjectId),
                    config.CanUseRemoteConfig);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void UnityServicesFactory_LeavesProvidersUnavailableUntilConfigured()
        {
            var config = ScriptableObject.CreateInstance<FrontierUnityServicesConfig>();
            try
            {
                config.enableOnlineServices = false;
                config.leaderboardId = string.Empty;
                config.privacyPolicyUrl = string.Empty;
                var consent = new ConsentService(new MemoryConsentStore());
                consent.SavePreferences(true, false, true);
                var services = FrontierUnityServicesFactory.Create(
                    consent, config);

                Assert.IsFalse(services.Authentication.IsConfigured);
                Assert.IsFalse(services.Authentication.CanRecoverAccount);
                Assert.IsFalse(services.Leaderboards.IsConfigured);
                Assert.IsFalse(services.CloudSave.IsConfigured);
                Assert.IsFalse(services.Analytics.IsConfigured);
                Assert.IsFalse(services.RemoteConfig.IsConfigured);
                Assert.AreEqual(ServiceStatus.NotConfigured, services.Authentication.InitializeAsync().Result.status);
                Assert.AreEqual(ServiceStatus.NotConfigured, services.Leaderboards.InitializeAsync().Result.status);
                Assert.AreEqual(ServiceStatus.NotConfigured, services.CloudSave.InitializeAsync().Result.status);
                Assert.AreEqual(ServiceStatus.NotConfigured, services.Analytics.InitializeAsync().Result.status);
                Assert.AreEqual(ServiceStatus.NotConfigured, services.RemoteConfig.InitializeAsync().Result.status);
                Assert.AreEqual(string.Empty, services.PrivacyPolicyUrl);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void ResourcesUnityServicesConfig_RemainsDisabledAndUncredentialed()
        {
            FrontierUnityServicesConfig config =
                Resources.Load<FrontierUnityServicesConfig>("Frontier/FrontierUnityServicesConfig");

            Assert.IsNotNull(config);
            Assert.IsFalse(config.enableOnlineServices);
            Assert.IsFalse(config.enableCloudSave);
            Assert.IsFalse(config.enableAccountRecovery);
            Assert.IsFalse(config.enableAnalytics);
            Assert.IsFalse(config.enableRemoteConfig);
            Assert.IsTrue(string.IsNullOrWhiteSpace(config.leaderboardId));
            Assert.IsTrue(string.IsNullOrWhiteSpace(config.privacyPolicyUrl));
        }
    }
}
