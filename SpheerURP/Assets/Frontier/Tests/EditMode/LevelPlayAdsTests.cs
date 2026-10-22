using System;
using System.Threading.Tasks;
using Frontier.Services;
using Frontier.Services.Unity;
using NUnit.Framework;
using Unity.Services.LevelPlay;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class LevelPlayAdsTests
    {
        sealed class ConsentStore : IConsentStore
        {
            ConsentPreferences preferences;
            public ConsentPreferences Load() { return preferences; }
            public void Save(ConsentPreferences value) { preferences = value; }
        }

        sealed class Ad : ILevelPlayRewardedAd
        {
            public event Action<LevelPlayAdInfo> OnAdLoaded;
            public event Action<LevelPlayAdError> OnAdLoadFailed;
            public event Action<LevelPlayAdInfo> OnAdDisplayed { add { } remove { } }
            public event Action<LevelPlayAdInfo, LevelPlayAdError> OnAdDisplayFailed;
            public event Action<LevelPlayAdInfo, LevelPlayReward> OnAdRewarded;
            public event Action<LevelPlayAdInfo> OnAdClicked { add { } remove { } }
            public event Action<LevelPlayAdInfo> OnAdClosed;
            public event Action<LevelPlayAdInfo> OnAdInfoChanged { add { } remove { } }
            public Action load, show;
            public bool ready = true, disposed;
            public string AdUnitId { get { return "unit"; } }
            public string GetAdId() { return "ad"; }
            public LevelPlayReward GetReward(string placement) { return null; }
            public bool IsAdReady() { return ready; }
            public void LoadAd() { if (load != null) load(); else Loaded(); }
            public void ShowAd(string placementName = null) { show?.Invoke(); }
            public void DestroyAd() { disposed = true; }
            public void Dispose() { DestroyAd(); }
            public void Loaded() { OnAdLoaded?.Invoke(null); }
            public void LoadFailed() { OnAdLoadFailed?.Invoke(null); }
            public void DisplayFailed() { OnAdDisplayFailed?.Invoke(null, null); }
            public void Reward() { OnAdRewarded?.Invoke(null, null); }
            public void Close() { OnAdClosed?.Invoke(null); }
            public bool HasListeners { get { return OnAdLoaded != null || OnAdLoadFailed != null ||
                OnAdDisplayFailed != null || OnAdRewarded != null || OnAdClosed != null; } }
        }

        sealed class Sdk : ILevelPlayAdsSdk
        {
            public bool supported = true;
            public int initialized, created;
            public Ad ad = new Ad();
            public Task<ServiceResult> initialization;
            public bool IsSupported { get { return supported; } }
            public Task<ServiceResult> InitializeAsync(string appKey)
            {
                initialized++;
                return initialization ?? Task.FromResult(ServiceResult.Success());
            }
            public ILevelPlayRewardedAd CreateRewardedAd(string id) { created++; return ad; }
        }

        RewardedAdsPolicy policy;
        FrontierUnityServicesConfig config;
        ConsentService consent;
        Sdk sdk;
        LevelPlayAdsProvider provider;

        [SetUp]
        public void SetUp()
        {
            policy = ScriptableObject.CreateInstance<RewardedAdsPolicy>();
            policy.enableRewardedAds = true;
            policy.iosSetupVerified = true;
            policy.iosAppKey = "app";
            policy.iosRewardedAdUnitId = "unit";
            policy.placements = new[] { new RewardedAdPlacement { id = "double-yield", dailyCap = 2 } };
            config = ScriptableObject.CreateInstance<FrontierUnityServicesConfig>();
            config.privacyPolicyUrl = "https://example.com/privacy";
            consent = new ConsentService(new ConsentStore());
            consent.SavePreferences(false, true, false);
            sdk = new Sdk();
            provider = new LevelPlayAdsProvider(policy, config, consent, sdk, 100);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(policy);
            UnityEngine.Object.DestroyImmediate(config);
        }

        [Test]
        public async Task GatesPreventSdkRequests()
        {
            policy.iosSetupVerified = false;
            Assert.IsFalse(provider.IsConfigured);
            Assert.AreEqual(ServiceStatus.NotConfigured, (await provider.InitializeAsync()).status);
            policy.iosSetupVerified = true;
            config.privacyPolicyUrl = "";
            Assert.IsFalse(provider.IsConfigured);
            config.privacyPolicyUrl = "https://example.com/privacy";
            sdk.supported = false;
            Assert.IsFalse(provider.IsConfigured);
            sdk.supported = true;
            consent.Reset();
            Assert.AreEqual(ServiceStatus.ConsentRequired, (await provider.InitializeAsync()).status);
            Assert.AreEqual(ServiceStatus.ConsentRequired, (await provider.ShowRewardedAsync("double-yield")).status);
            Assert.AreEqual(0, sdk.initialized);
            Assert.AreEqual(0, sdk.created);
        }

        [Test]
        public async Task DevelopmentTestGateNeverEnablesProductionOrUnsupportedPlatforms()
        {
            policy.iosSetupVerified = false;
            policy.enableIosDevelopmentTests = true;
            Assert.IsTrue(policy.CanTestIos(config, RuntimePlatform.IPhonePlayer, true));
            Assert.IsFalse(policy.CanUseIos(config));
            Assert.IsFalse(policy.CanTestIos(config, RuntimePlatform.IPhonePlayer, false));
            Assert.IsFalse(policy.CanTestIos(config, RuntimePlatform.Android, true));
            Assert.IsFalse(policy.CanTestIos(config, RuntimePlatform.WindowsEditor, true));
            Assert.AreEqual(ServiceStatus.NotConfigured,
                (await LevelPlayDevelopmentTests.OpenAsync(policy, config, consent)).status);
            var diagnostics = new LevelPlayDevelopmentTests(policy, config, consent);
            Assert.IsFalse(diagnostics.CanOpenTestSuite);
            Assert.AreEqual(ServiceMessage.Get("service.ads.test-editor").ToString(), diagnostics.Status.ToString());
            Assert.AreEqual(ServiceStatus.NotConfigured, (await diagnostics.OpenTestSuiteAsync()).status);
            config.privacyPolicyUrl = "";
            Assert.IsFalse(policy.CanTestIos(config, RuntimePlatform.IPhonePlayer, true));
            config.privacyPolicyUrl = "https://example.com/privacy";
            policy.enableIosDevelopmentTests = false;
            Assert.IsFalse(policy.CanTestIos(config, RuntimePlatform.IPhonePlayer, true));
        }

        [Test]
        public void SdkAutoInitializationDoesNotBypassFrontierConsent()
        {
            var settings = Resources.Load<LevelPlayMediationSettings>("LevelPlayMediationSettings");
            if (settings != null)
            {
                Assert.IsFalse(settings.EnableIronsourceSDKInitAPI);
                Assert.IsFalse(settings.EnableIntegrationHelper);
            }
        }

        [Test]
        public async Task DuplicateRewardCallbacksGrantOneResultAndCleanUp()
        {
            sdk.ad.show = () => { sdk.ad.Reward(); sdk.ad.Reward(); sdk.ad.Close(); };
            var result = await provider.ShowRewardedAsync("double-yield");
            Assert.IsTrue(result.succeeded);
            Assert.IsTrue(result.value.rewardEarned);
            Assert.AreEqual("double-yield", result.value.placementId);
            Assert.IsTrue(sdk.ad.disposed);
            Assert.IsFalse(sdk.ad.HasListeners);
            sdk.ad.Reward();
            Assert.AreEqual(1, sdk.created);
        }

        [Test]
        public async Task CloseBeforeRewardStillEarnsTheReward()
        {
            sdk.ad.show = sdk.ad.Close;
            var request = provider.ShowRewardedAsync("double-yield");
            Assert.IsFalse(request.IsCompleted);
            sdk.ad.Reward();
            Assert.IsTrue((await request).value.rewardEarned);
        }

        [Test]
        public async Task WithdrawalAndReoptInDuringLoadingNeverShowsAd()
        {
            bool shown = false;
            sdk.ad.load = () => { consent.Reset(); consent.SavePreferences(false, true, false); sdk.ad.Loaded(); };
            sdk.ad.show = () => shown = true;
            Assert.AreEqual(ServiceStatus.ConsentRequired, (await provider.ShowRewardedAsync("double-yield")).status);
            Assert.IsFalse(shown);
            Assert.IsTrue(sdk.ad.disposed);
        }

        [Test]
        public async Task WithdrawalDuringInitializationNeverCreatesAd()
        {
            var pending = new TaskCompletionSource<ServiceResult>();
            sdk.initialization = pending.Task;
            var request = provider.ShowRewardedAsync("double-yield");
            consent.Reset();
            pending.SetResult(ServiceResult.Success());
            Assert.AreEqual(ServiceStatus.ConsentRequired, (await request).status);
            Assert.AreEqual(0, sdk.created);
        }

        [Test]
        public async Task PlaybackConsentWithdrawalDiscardsTheReward()
        {
            sdk.ad.show = () =>
            {
                consent.Reset();
                consent.SavePreferences(false, true, false);
                sdk.ad.Reward();
                sdk.ad.Close();
            };
            Assert.AreEqual(ServiceStatus.ConsentRequired, (await provider.ShowRewardedAsync("double-yield")).status);
        }

        [Test]
        public async Task UnknownPlacementAndConcurrentRequestsDoNotOpenAnotherAd()
        {
            Assert.AreEqual(ServiceStatus.InvalidRequest, (await provider.ShowRewardedAsync("missing")).status);
            Assert.AreEqual(0, sdk.initialized);
            var first = provider.ShowRewardedAsync("double-yield");
            Assert.AreEqual(ServiceStatus.InvalidRequest, (await provider.ShowRewardedAsync("double-yield")).status);
            sdk.ad.Reward();
            sdk.ad.Close();
            Assert.IsTrue((await first).succeeded);
            Assert.AreEqual(1, sdk.created);
        }

        [Test]
        public async Task LoadAndDisplayFailuresNeverGrantRewardsAndAllowRetry()
        {
            sdk.ad.load = sdk.ad.LoadFailed;
            Assert.AreEqual(ServiceStatus.Failed, (await provider.ShowRewardedAsync("double-yield")).status);
            Assert.IsTrue(sdk.ad.disposed);
            Assert.IsFalse(sdk.ad.HasListeners);
            sdk.ad = new Ad();
            sdk.ad.show = sdk.ad.DisplayFailed;
            Assert.AreEqual(ServiceStatus.Failed, (await provider.ShowRewardedAsync("double-yield")).status);
            Assert.IsTrue(sdk.ad.disposed);
            Assert.IsFalse(sdk.ad.HasListeners);
        }

        [Test]
        public async Task UnconfirmedRewardAndMissingCallbacksFailWithoutHanging()
        {
            sdk.ad.show = sdk.ad.Close;
            Assert.AreEqual(ServiceStatus.Failed, (await provider.ShowRewardedAsync("double-yield")).status);
            sdk.ad = new Ad { load = () => { } };
            Assert.AreEqual(ServiceStatus.Failed, (await provider.ShowRewardedAsync("double-yield")).status);
            Assert.IsTrue(sdk.ad.disposed);
            Assert.IsFalse(sdk.ad.HasListeners);
        }
    }
}
