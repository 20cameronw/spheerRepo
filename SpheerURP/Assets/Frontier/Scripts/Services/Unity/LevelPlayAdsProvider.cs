using System;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.LevelPlay;
using UnityEngine;

namespace Frontier.Services.Unity
{
    public interface ILevelPlayAdsSdk
    {
        bool IsSupported { get; }
        Task<ServiceResult> InitializeAsync(string appKey);
        ILevelPlayRewardedAd CreateRewardedAd(string adUnitId);
    }

    public sealed class LevelPlayAdsProvider : IAdsService
    {
        readonly RewardedAdsPolicy policy;
        readonly FrontierUnityServicesConfig config;
        readonly IConsentService consent;
        readonly ILevelPlayAdsSdk sdk;
        readonly int timeoutMilliseconds;
        bool showing;

        public LevelPlayAdsProvider(RewardedAdsPolicy policy, FrontierUnityServicesConfig config,
            IConsentService consent, ILevelPlayAdsSdk sdk, int timeoutMilliseconds = 300000)
        {
            this.policy = policy ?? throw new ArgumentNullException(nameof(policy));
            this.config = config;
            this.consent = consent ?? throw new ArgumentNullException(nameof(consent));
            this.sdk = sdk ?? throw new ArgumentNullException(nameof(sdk));
            if (timeoutMilliseconds <= 0) throw new ArgumentOutOfRangeException(nameof(timeoutMilliseconds));
            this.timeoutMilliseconds = timeoutMilliseconds;
        }

        public bool IsConfigured { get { return sdk.IsSupported && policy.CanUseIos(config); } }
        bool HasConsent { get { return consent.Current.hasDecision && consent.Current.adsAllowed; } }

        public Task<ServiceResult> InitializeAsync()
        {
            if (!IsConfigured) return Task.FromResult(ServiceResult.NotConfigured("Rewarded ads"));
            if (!HasConsent) return Task.FromResult(ServiceResult.ConsentRequired("advertising"));
            return sdk.InitializeAsync(policy.iosAppKey);
        }

        public async Task<ServiceResult<RewardedAdResult>> ShowRewardedAsync(string placementId)
        {
            if (!IsConfigured) return ServiceResult<RewardedAdResult>.NotConfigured("Rewarded ads");
            if (!HasConsent) return ServiceResult<RewardedAdResult>.ConsentRequired("advertising");
            if (Array.Find(policy.placements, row => row != null && row.id == placementId && row.dailyCap > 0) == null)
                return ServiceResult<RewardedAdResult>.InvalidRequest(ServiceMessage.Get("service.ads.unknown"));
            if (showing)
                return ServiceResult<RewardedAdResult>.InvalidRequest(ServiceMessage.Get("service.ads.busy"));

            showing = true;
            bool withdrawn = false;
            void Observe(ConsentPreferences preferences)
            {
                if (!preferences.hasDecision || !preferences.adsAllowed) withdrawn = true;
            }
            consent.PreferencesChanged += Observe;
            try
            {
                var initialized = await InitializeAsync();
                if (!initialized.succeeded) return ServiceResult<RewardedAdResult>.Failed(initialized.text);
                if (withdrawn || !HasConsent) return ServiceResult<RewardedAdResult>.ConsentRequired("advertising");

                // A fresh instance isolates duplicate/late callbacks from subsequent reward requests.
                using (var ad = sdk.CreateRewardedAd(policy.iosRewardedAdUnitId))
                {
                    var loaded = new TaskCompletionSource<ServiceResult>();
                    var rewarded = new TaskCompletionSource<bool>();
                    var closed = new TaskCompletionSource<ServiceResult>();
                    bool showRequested = false;
                    void OnLoaded(LevelPlayAdInfo info) { loaded.TrySetResult(ServiceResult.Success()); }
                    void OnLoadFailed(LevelPlayAdError error)
                    {
                        Debug.LogWarning("LevelPlay rewarded load failed: " + error);
                        loaded.TrySetResult(ServiceResult.Failed(ServiceMessage.Get("service.ads.load-failed")));
                    }
                    void OnDisplayFailed(LevelPlayAdInfo info, LevelPlayAdError error)
                    {
                        Debug.LogWarning("LevelPlay rewarded display failed: " + error);
                        if (showRequested)
                            closed.TrySetResult(ServiceResult.Failed(ServiceMessage.Get("service.ads.display-failed")));
                    }
                    void OnRewarded(LevelPlayAdInfo info, LevelPlayReward reward)
                    {
                        if (showRequested) rewarded.TrySetResult(true);
                    }
                    void OnClosed(LevelPlayAdInfo info)
                    {
                        if (showRequested) closed.TrySetResult(ServiceResult.Success());
                    }
                    ad.OnAdLoaded += OnLoaded;
                    ad.OnAdLoadFailed += OnLoadFailed;
                    ad.OnAdDisplayFailed += OnDisplayFailed;
                    ad.OnAdRewarded += OnRewarded;
                    ad.OnAdClosed += OnClosed;
                    try
                    {
                        ad.LoadAd();
                        var loadResult = await WithTimeout(loaded.Task, Math.Min(timeoutMilliseconds, 45000));
                        if (!loadResult.succeeded) return ServiceResult<RewardedAdResult>.Failed(loadResult.text);
                        if (withdrawn || !HasConsent)
                            return ServiceResult<RewardedAdResult>.ConsentRequired("advertising");
                        if (!ad.IsAdReady())
                            return ServiceResult<RewardedAdResult>.Failed(ServiceMessage.Get("service.ads.load-failed"));

                        // Use the default dashboard placement; game placement IDs own the local limits/rewards.
                        showRequested = true;
                        ad.ShowAd();
                        var closeResult = await WithTimeout(closed.Task, timeoutMilliseconds);
                        if (!closeResult.succeeded) return ServiceResult<RewardedAdResult>.Failed(closeResult.text);
                        // LevelPlay permits the reward callback to arrive after the close callback.
                        if (!rewarded.Task.IsCompleted)
                        {
                            using (var timeout = new CancellationTokenSource())
                            {
                                await Task.WhenAny(rewarded.Task, Task.Delay(Math.Min(timeoutMilliseconds, 30000), timeout.Token));
                                timeout.Cancel();
                            }
                        }
                        if (withdrawn || !HasConsent)
                            return ServiceResult<RewardedAdResult>.ConsentRequired("advertising");
                        if (!rewarded.Task.IsCompleted)
                            return ServiceResult<RewardedAdResult>.Failed(ServiceMessage.Get("service.ads.unconfirmed"));
                        return ServiceResult<RewardedAdResult>.Success(new RewardedAdResult(true, placementId));
                    }
                    finally
                    {
                        ad.OnAdLoaded -= OnLoaded;
                        ad.OnAdLoadFailed -= OnLoadFailed;
                        ad.OnAdDisplayFailed -= OnDisplayFailed;
                        ad.OnAdRewarded -= OnRewarded;
                        ad.OnAdClosed -= OnClosed;
                    }
                }
            }
            finally
            {
                consent.PreferencesChanged -= Observe;
                showing = false;
            }
        }

        async Task<ServiceResult> WithTimeout(Task<ServiceResult> task, int milliseconds)
        {
            using (var timeout = new CancellationTokenSource())
            {
                var first = await Task.WhenAny(task, Task.Delay(milliseconds, timeout.Token));
                timeout.Cancel();
                return first == task ? await task
                    : ServiceResult.Failed(ServiceMessage.Get("service.ads.timeout"));
            }
        }
    }

    sealed class UnityLevelPlayAdsSdk : ILevelPlayAdsSdk
    {
        public static readonly UnityLevelPlayAdsSdk Instance = new UnityLevelPlayAdsSdk();
        Task<ServiceResult> initialization;
        string initializedKey;

        public bool IsSupported { get { return Application.platform == RuntimePlatform.IPhonePlayer; } }

        public Task<ServiceResult> InitializeAsync(string appKey)
        {
            if (initialization != null && initialization.Status == TaskStatus.RanToCompletion &&
                !initialization.Result.succeeded)
                initialization = null;
            if (initialization != null)
            {
                if (initializedKey != appKey)
                    return Task.FromResult(ServiceResult.Failed(ServiceMessage.Get("service.ads.config-changed")));
                return initialization;
            }
            initializedKey = appKey;
            initialization = InitializeCoreAsync(appKey);
            return initialization;
        }

        async Task<ServiceResult> InitializeCoreAsync(string appKey)
        {
            var result = new TaskCompletionSource<ServiceResult>();
            void Success(LevelPlayConfiguration configuration) { result.TrySetResult(ServiceResult.Success()); }
            void Failed(LevelPlayInitError error)
            {
                Debug.LogWarning("LevelPlay initialization failed: " + error);
                result.TrySetResult(ServiceResult.Failed(ServiceMessage.Get("service.ads.init-failed")));
            }
            LevelPlay.OnInitSuccess += Success;
            LevelPlay.OnInitFailed += Failed;
            try
            {
                // An ad opt-in is not consent to tracking, personalization, or sale/sharing.
                LevelPlay.SetConsent(false);
                LevelPlay.SetMetaData("do_not_sell", "true");
                LevelPlay.SetMetaData("is_deviceid_optout", "true");
                LevelPlay.SetPauseGame(true);
                LevelPlay.Init(appKey);
                using (var timeout = new CancellationTokenSource())
                {
                    var first = await Task.WhenAny(result.Task, Task.Delay(45000, timeout.Token));
                    timeout.Cancel();
                    return first == result.Task ? await result.Task
                        : ServiceResult.Failed(ServiceMessage.Get("service.ads.timeout"));
                }
            }
            finally
            {
                LevelPlay.OnInitSuccess -= Success;
                LevelPlay.OnInitFailed -= Failed;
            }
        }

        public ILevelPlayRewardedAd CreateRewardedAd(string adUnitId)
        {
            return new LevelPlayRewardedAd(adUnitId);
        }
    }
}
