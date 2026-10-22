using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using Frontier.Services;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using Unity.Services.CloudSave.Models;
using Unity.Services.Core;
using Unity.Services.Leaderboards;
using Unity.Services.Leaderboards.Models;
using Unity.Services.RemoteConfig;
using UnityCustomEvent = Unity.Services.Analytics.CustomEvent;
using UnityAnalytics = Unity.Services.Analytics.AnalyticsService;
using UnityRemoteConfig = Unity.Services.RemoteConfig.RemoteConfigService;
using UgsLeaderboardEntry = Unity.Services.Leaderboards.Models.LeaderboardEntry;
using UnityEngine;

namespace Frontier.Services.Unity
{
    sealed class UnityServicesInitializer
    {
        readonly FrontierUnityServicesConfig config;
        Task<ServiceResult> initializationTask;

        public UnityServicesInitializer(FrontierUnityServicesConfig config)
        {
            this.config = config;
        }

        public Task<ServiceResult> InitializeAsync()
        {
            if (config == null || (!config.CanUseAuthentication && !config.CanUseAnalytics))
                return Task.FromResult(ServiceResult.NotConfigured("Unity Gaming Services"));

            if (UnityServices.State == ServicesInitializationState.Initialized)
                return Task.FromResult(ServiceResult.Success());

            if (initializationTask == null || initializationTask.IsCompleted)
                initializationTask = InitializeCoreAsync();
            return initializationTask;
        }

        async Task<ServiceResult> InitializeCoreAsync()
        {
            try
            {
                await UnityServices.InitializeAsync();
                return ServiceResult.Success();
            }
            catch (ServicesInitializationException exception)
            {
                Debug.LogWarning("Unity Gaming Services initialization failed: " + exception.Message);
                return ServiceResult.Failed(exception.Message);
            }
            catch (RequestFailedException exception)
            {
                Debug.LogWarning("Unity Gaming Services initialization failed: " + exception.Message);
                return ServiceResult.Failed(exception.Message);
            }
        }
    }

    sealed class UnityAuthenticationProvider : IAuthService
    {
        readonly FrontierUnityServicesConfig config;
        readonly UnityServicesInitializer initializer;
        readonly IRecoveryCredentialProvider recoveryCredentials;

        public UnityAuthenticationProvider(FrontierUnityServicesConfig config, UnityServicesInitializer initializer,
            IRecoveryCredentialProvider recoveryCredentials)
        {
            this.config = config;
            this.initializer = initializer;
            this.recoveryCredentials = recoveryCredentials;
        }

        public bool IsConfigured { get { return config != null && config.CanUseAuthentication; } }
        public string CurrentPlayerId
        {
            get
            {
                return UnityServices.State == ServicesInitializationState.Initialized && AuthenticationService.Instance.IsSignedIn
                    ? AuthenticationService.Instance.PlayerId : string.Empty;
            }
        }
        public bool CanRecoverAccount
        {
            get
            {
                return config != null && config.CanUseAccountRecovery &&
                    recoveryCredentials != null && recoveryCredentials.IsConfigured;
            }
        }
        public string RecoveryProviderName
        {
            get { return CanRecoverAccount ? recoveryCredentials.ProviderName : string.Empty; }
        }

        public Task<ServiceResult> InitializeAsync()
        {
            return initializer.InitializeAsync();
        }

        public async Task<ServiceResult<AuthIdentity>> SignInAnonymouslyAsync()
        {
            if (!IsConfigured)
                return ServiceResult<AuthIdentity>.NotConfigured("Unity Authentication");

            ServiceResult initialized = await InitializeAsync();
            if (!initialized.succeeded)
                return ServiceResult<AuthIdentity>.Failed(initialized.text);

            try
            {
                var authentication = AuthenticationService.Instance;
                if (!authentication.IsSignedIn)
                    await authentication.SignInAnonymouslyAsync();

                bool anonymous = authentication.PlayerInfo != null &&
                    authentication.PlayerInfo.Identities.Count == 0;
                return ServiceResult<AuthIdentity>.Success(
                    new AuthIdentity(authentication.PlayerId, anonymous));
            }
            catch (RequestFailedException exception)
            {
                Debug.LogWarning("Unity Authentication sign-in failed: " + exception.Message);
                return ServiceResult<AuthIdentity>.Failed(exception.Message);
            }
        }

        public Task<ServiceResult<AuthIdentity>> LinkRecoveryAccountAsync()
        {
            return RecoverAsync(linkCurrentAccount: true);
        }

        public Task<ServiceResult<AuthIdentity>> SignInWithRecoveryAccountAsync()
        {
            return RecoverAsync(linkCurrentAccount: false);
        }

        async Task<ServiceResult<AuthIdentity>> RecoverAsync(bool linkCurrentAccount)
        {
            if (!CanRecoverAccount)
                return ServiceResult<AuthIdentity>.NotConfigured("Account recovery");

            ServiceResult initialized = await InitializeAsync();
            if (!initialized.succeeded)
                return ServiceResult<AuthIdentity>.Failed(initialized.text);

            var authentication = AuthenticationService.Instance;
            if (linkCurrentAccount && !authentication.IsSignedIn)
                return ServiceResult<AuthIdentity>.InvalidRequest(
                    ServiceMessage.Get("service.recovery.sign-in"));

            ServiceResult<RecoveryCredential> credentialResult =
                await recoveryCredentials.AcquireAsync();
            if (!credentialResult.succeeded)
                return ServiceResult<AuthIdentity>.Failed(credentialResult.text);
            if (string.IsNullOrWhiteSpace(credentialResult.value.token))
                return ServiceResult<AuthIdentity>.Failed(
                    ServiceMessage.Get("service.recovery.credential"));

            try
            {
                RecoveryCredential credential = credentialResult.value;
                switch (credential.provider)
                {
                    case RecoveryProvider.Apple:
                        if (linkCurrentAccount)
                            await authentication.LinkWithAppleAsync(credential.token);
                        else
                            await authentication.SignInWithAppleAsync(credential.token);
                        break;
                    case RecoveryProvider.GooglePlayGames:
                        if (linkCurrentAccount)
                            await authentication.LinkWithGooglePlayGamesAsync(credential.token);
                        else
                            await authentication.SignInWithGooglePlayGamesAsync(credential.token);
                        break;
                    default:
                        return ServiceResult<AuthIdentity>.InvalidRequest(
                            ServiceMessage.Get("service.recovery.provider"));
                }

                bool anonymous = authentication.PlayerInfo != null &&
                    authentication.PlayerInfo.Identities.Count == 0;
                return ServiceResult<AuthIdentity>.Success(
                    new AuthIdentity(authentication.PlayerId, anonymous));
            }
            catch (AuthenticationException exception)
            {
                Debug.LogWarning("Unity Authentication account recovery failed: " + exception.Message);
                return ServiceResult<AuthIdentity>.Failed(exception.Message);
            }
            catch (RequestFailedException exception)
            {
                Debug.LogWarning("Unity Authentication account recovery failed: " + exception.Message);
                return ServiceResult<AuthIdentity>.Failed(exception.Message);
            }
        }

        public async Task<ServiceResult> DeleteAccountAsync(string expectedPlayerId)
        {
            if (!IsConfigured)
                return ServiceResult.NotConfigured("Unity Authentication");

            ServiceResult initialized = await InitializeAsync();
            if (!initialized.succeeded)
                return initialized;

            try
            {
                var authentication = AuthenticationService.Instance;
                if (!authentication.IsSignedIn || string.IsNullOrEmpty(expectedPlayerId) || authentication.PlayerId != expectedPlayerId)
                    return ServiceResult.InvalidRequest(ServiceMessage.Get("service.deletion.changed-account"));
                await authentication.DeleteAccountAsync();
                return ServiceResult.Success();
            }
            catch (RequestFailedException exception)
            {
                Debug.LogWarning("Unity Authentication account deletion failed: " + exception.Message);
                return ServiceResult.Failed(exception.Message);
            }
        }
    }

    sealed class UnityLeaderboardProvider : ILeaderboardService
    {
        const int MaximumPageSize = 100;
        const long MaximumExactIntegerScore = 9007199254740991L;

        readonly FrontierUnityServicesConfig config;
        readonly UnityServicesInitializer initializer;

        public UnityLeaderboardProvider(FrontierUnityServicesConfig config, UnityServicesInitializer initializer)
        {
            this.config = config;
            this.initializer = initializer;
        }

        public bool IsConfigured { get { return config != null && config.CanUseLeaderboard; } }

        public Task<ServiceResult> InitializeAsync()
        {
            return IsConfigured
                ? initializer.InitializeAsync()
                : Task.FromResult(ServiceResult.NotConfigured("Unity Leaderboards"));
        }

        public async Task<ServiceResult<IReadOnlyList<Frontier.Services.LeaderboardEntry>>> GetScoresAsync(
            string boardId, int limit)
        {
            if (!IsConfigured)
                return ServiceResult<IReadOnlyList<Frontier.Services.LeaderboardEntry>>.NotConfigured("Unity Leaderboards");
            if (!string.Equals(boardId, config.leaderboardId, StringComparison.Ordinal))
                return ServiceResult<IReadOnlyList<Frontier.Services.LeaderboardEntry>>.InvalidRequest(
                    ServiceMessage.Get("service.leaderboard.board"));
            if (limit < 1 || limit > MaximumPageSize)
                return ServiceResult<IReadOnlyList<Frontier.Services.LeaderboardEntry>>.InvalidRequest(
                    ServiceMessage.Get("service.leaderboard.limit", MaximumPageSize));

            ServiceResult initialized = await initializer.InitializeAsync();
            if (!initialized.succeeded)
                return ServiceResult<IReadOnlyList<Frontier.Services.LeaderboardEntry>>.Failed(initialized.text);

            try
            {
                if (!AuthenticationService.Instance.IsSignedIn)
                    return ServiceResult<IReadOnlyList<Frontier.Services.LeaderboardEntry>>.InvalidRequest(
                        ServiceMessage.Get("service.leaderboard.sign-in"));
                LeaderboardScoresPage page = await LeaderboardsService.Instance.GetScoresAsync(
                    config.leaderboardId, new GetScoresOptions { Limit = limit });
                var entries = new List<Frontier.Services.LeaderboardEntry>(
                    page.Results == null ? 0 : page.Results.Count);
                if (page.Results != null)
                {
                    foreach (UgsLeaderboardEntry entry in page.Results)
                    {
                        if (entry.Score < 0 || entry.Score > MaximumExactIntegerScore ||
                            entry.Score != Math.Truncate(entry.Score))
                            return ServiceResult<IReadOnlyList<Frontier.Services.LeaderboardEntry>>.Failed(
                                ServiceMessage.Get("service.leaderboard.score"));

                        entries.Add(ToEntry(entry));
                    }
                }

                return ServiceResult<IReadOnlyList<Frontier.Services.LeaderboardEntry>>.Success(entries);
            }
            catch (ServicesInitializationException exception)
            {
                Debug.LogWarning("Unity Leaderboards request failed: " + exception.Message);
                return ServiceResult<IReadOnlyList<Frontier.Services.LeaderboardEntry>>.Failed(exception.Message);
            }
            catch (RequestFailedException exception)
            {
                Debug.LogWarning("Unity Leaderboards request failed: " + exception.Message);
                return ServiceResult<IReadOnlyList<Frontier.Services.LeaderboardEntry>>.Failed(exception.Message);
            }
        }

        public async Task<ServiceResult<Frontier.Services.LeaderboardEntry>> SubmitScoreAsync(string boardId, long score)
        {
            if (!IsConfigured)
                return ServiceResult<Frontier.Services.LeaderboardEntry>.NotConfigured("Unity Leaderboards");
            if (!string.Equals(boardId, config.leaderboardId, StringComparison.Ordinal))
                return ServiceResult<Frontier.Services.LeaderboardEntry>.InvalidRequest(
                    ServiceMessage.Get("service.leaderboard.board"));
            if (score < 0 || score > MaximumExactIntegerScore)
                return ServiceResult<Frontier.Services.LeaderboardEntry>.InvalidRequest(
                    ServiceMessage.Get("service.leaderboard.submission"));
            var initialized = await initializer.InitializeAsync();
            if (!initialized.succeeded)
                return ServiceResult<Frontier.Services.LeaderboardEntry>.Failed(initialized.text);
            try
            {
                if (!AuthenticationService.Instance.IsSignedIn)
                    return ServiceResult<Frontier.Services.LeaderboardEntry>.InvalidRequest(
                        ServiceMessage.Get("service.leaderboard.sign-in"));
                var entry = await LeaderboardsService.Instance.AddPlayerScoreAsync(config.leaderboardId, score);
                if (entry.Score < 0 || entry.Score > MaximumExactIntegerScore || entry.Score != Math.Truncate(entry.Score))
                    return ServiceResult<Frontier.Services.LeaderboardEntry>.Failed(
                        ServiceMessage.Get("service.leaderboard.score"));
                return ServiceResult<Frontier.Services.LeaderboardEntry>.Success(ToEntry(entry));
            }
            catch (RequestFailedException exception)
            {
                Debug.LogWarning("Unity Leaderboards submission failed: " + exception.Message);
                return ServiceResult<Frontier.Services.LeaderboardEntry>.Failed(exception.Message);
            }
        }

        static Frontier.Services.LeaderboardEntry ToEntry(UgsLeaderboardEntry entry)
        {
            // Never display UGS profile names, which can contain identifying text.
            return new Frontier.Services.LeaderboardEntry(entry.PlayerId, string.Empty,
                long.Parse(entry.Score.ToString("0", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture),
                entry.Rank + 1, true);
        }
    }

    sealed class UnityCloudSaveProvider : ICloudSaveService
    {
        const string SaveKey = "frontier_save_v2";
        const int MaximumPayloadBytes = 5 * 1024 * 1024;

        readonly FrontierUnityServicesConfig config;
        readonly UnityServicesInitializer initializer;

        public UnityCloudSaveProvider(FrontierUnityServicesConfig config, UnityServicesInitializer initializer)
        {
            this.config = config;
            this.initializer = initializer;
        }

        public bool IsConfigured { get { return config != null && config.CanUseCloudSave; } }

        public Task<ServiceResult> InitializeAsync()
        {
            return IsConfigured
                ? initializer.InitializeAsync()
                : Task.FromResult(ServiceResult.NotConfigured("Unity Cloud Save"));
        }

        public async Task<ServiceResult<CloudSaveRecord>> ReadAsync()
        {
            if (!IsConfigured)
                return ServiceResult<CloudSaveRecord>.NotConfigured("Unity Cloud Save");

            ServiceResult initialized = await initializer.InitializeAsync();
            if (!initialized.succeeded)
                return ServiceResult<CloudSaveRecord>.Failed(initialized.text);

            if (!AuthenticationService.Instance.IsSignedIn)
                return ServiceResult<CloudSaveRecord>.InvalidRequest(
                    ServiceMessage.Get("service.cloud.sign-in-load"));

            try
            {
                var items = await CloudSaveService.Instance.Data.Player.LoadAsync(
                    new HashSet<string> { SaveKey });
                if (!items.TryGetValue(SaveKey, out var item))
                    return ServiceResult<CloudSaveRecord>.Success(
                        new CloudSaveRecord(false, string.Empty, string.Empty));

                string payload = item.Value.GetAs<string>();
                if (string.IsNullOrWhiteSpace(payload))
                    return ServiceResult<CloudSaveRecord>.Failed(
                        ServiceMessage.Get("service.cloud.empty"));

                return ServiceResult<CloudSaveRecord>.Success(
                    new CloudSaveRecord(true, payload, item.WriteLock));
            }
            catch (RequestFailedException exception)
            {
                Debug.LogWarning("Unity Cloud Save read failed: " + exception.Message);
                return ServiceResult<CloudSaveRecord>.Failed(exception.Message);
            }
        }

        public async Task<ServiceResult> DeleteSaveAsync(string expectedPlayerId)
        {
            if (!IsConfigured) return ServiceResult.NotConfigured("Unity Cloud Save");
            ServiceResult initialized = await initializer.InitializeAsync();
            if (!initialized.succeeded) return initialized;
            var authentication = AuthenticationService.Instance;
            if (!authentication.IsSignedIn || string.IsNullOrEmpty(expectedPlayerId) || authentication.PlayerId != expectedPlayerId)
                return ServiceResult.InvalidRequest(ServiceMessage.Get("service.cloud.changed-account"));
            try
            {
                var records = await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { SaveKey });
                if (authentication.PlayerId != expectedPlayerId || !authentication.IsSignedIn)
                    return ServiceResult.InvalidRequest(ServiceMessage.Get("service.cloud.changed-account"));
                if (!records.TryGetValue(SaveKey, out var record)) return ServiceResult.Success();
                if (string.IsNullOrWhiteSpace(record.WriteLock))
                    return ServiceResult.Failed(ServiceMessage.Get("service.cloud.delete-lock"));
                await CloudSaveService.Instance.Data.Player.DeleteAsync(SaveKey,
                    new global::Unity.Services.CloudSave.Models.Data.Player.DeleteOptions { WriteLock = record.WriteLock });
                return ServiceResult.Success();
            }
            catch (RequestFailedException exception)
            {
                Debug.LogWarning("Unity Cloud Save deletion failed: " + exception.Message);
                return ServiceResult.Failed(exception.Message);
            }
        }

        public async Task<ServiceResult<CloudSaveRecord>> WriteAsync(string payload, string writeLock)
        {
            if (!IsConfigured)
                return ServiceResult<CloudSaveRecord>.NotConfigured("Unity Cloud Save");
            if (string.IsNullOrWhiteSpace(payload))
                return ServiceResult<CloudSaveRecord>.InvalidRequest(
                    ServiceMessage.Get("service.cloud.nonempty"));
            if (Encoding.UTF8.GetByteCount(payload) > MaximumPayloadBytes)
                return ServiceResult<CloudSaveRecord>.InvalidRequest(
                    ServiceMessage.Get("service.cloud.limit"));

            ServiceResult initialized = await initializer.InitializeAsync();
            if (!initialized.succeeded)
                return ServiceResult<CloudSaveRecord>.Failed(initialized.text);

            if (!AuthenticationService.Instance.IsSignedIn)
                return ServiceResult<CloudSaveRecord>.InvalidRequest(
                    ServiceMessage.Get("service.cloud.sign-in-write"));

            try
            {
                if (string.IsNullOrWhiteSpace(writeLock))
                {
                    var existing = await CloudSaveService.Instance.Data.Player.LoadAsync(
                        new HashSet<string> { SaveKey });
                    if (existing.ContainsKey(SaveKey))
                        return ServiceResult<CloudSaveRecord>.InvalidRequest(
                            ServiceMessage.Get("service.cloud.replace-lock"));
                }

                var data = new Dictionary<string, SaveItem>
                {
                    { SaveKey, new SaveItem(payload, string.IsNullOrWhiteSpace(writeLock) ? null : writeLock) }
                };
                Dictionary<string, string> writeLocks =
                    await CloudSaveService.Instance.Data.Player.SaveAsync(data);
                if (writeLocks == null || !writeLocks.TryGetValue(SaveKey, out string updatedWriteLock) ||
                    string.IsNullOrWhiteSpace(updatedWriteLock))
                    return ServiceResult<CloudSaveRecord>.Failed(
                        ServiceMessage.Get("service.cloud.missing-lock"));

                return ServiceResult<CloudSaveRecord>.Success(
                    new CloudSaveRecord(true, payload, updatedWriteLock));
            }
            catch (CloudSaveConflictException exception)
            {
                Debug.LogWarning("Unity Cloud Save write conflict: " + exception.Message);
                return ServiceResult<CloudSaveRecord>.Failed(
                    ServiceMessage.Get("service.cloud.conflict"));
            }
            catch (RequestFailedException exception)
            {
                Debug.LogWarning("Unity Cloud Save write failed: " + exception.Message);
                return ServiceResult<CloudSaveRecord>.Failed(exception.Message);
            }
        }
    }

    sealed class UnityAnalyticsProvider : IAnalyticsService
    {
        readonly FrontierUnityServicesConfig config;
        readonly UnityServicesInitializer initializer;
        bool dataCollectionAllowed;
        bool initialized;

        public UnityAnalyticsProvider(FrontierUnityServicesConfig config, UnityServicesInitializer initializer)
        {
            this.config = config;
            this.initializer = initializer;
        }

        public bool IsConfigured { get { return config != null && config.CanUseAnalytics; } }

        public void SetDataCollectionAllowed(bool allowed)
        {
            dataCollectionAllowed = allowed;
            if (!initialized) return;

            if (allowed)
                UnityAnalytics.Instance.StartDataCollection();
            else
                UnityAnalytics.Instance.StopDataCollection();
        }

        public async Task<ServiceResult> InitializeAsync()
        {
            if (!IsConfigured)
                return ServiceResult.NotConfigured("Unity Analytics");

            ServiceResult initializedResult = await initializer.InitializeAsync();
            if (!initializedResult.succeeded)
                return initializedResult;

            initialized = true;
            if (dataCollectionAllowed)
                UnityAnalytics.Instance.StartDataCollection();
            return ServiceResult.Success();
        }

        public ServiceResult RecordEvent(string eventName, IReadOnlyDictionary<string, string> parameters)
        {
            if (!IsConfigured)
                return ServiceResult.NotConfigured("Unity Analytics");
            if (!initialized || !dataCollectionAllowed)
                return ServiceResult.ConsentRequired("analytics data collection");
            if (string.IsNullOrWhiteSpace(eventName))
                return ServiceResult.InvalidRequest(ServiceMessage.Get("service.analytics.event"));

            try
            {
                var analyticsEvent = new UnityCustomEvent(eventName);
                if (parameters != null)
                {
                    foreach (KeyValuePair<string, string> parameter in parameters)
                    {
                        if (string.IsNullOrWhiteSpace(parameter.Key) || parameter.Value == null)
                            return ServiceResult.InvalidRequest(ServiceMessage.Get("service.analytics.parameters"));
                        analyticsEvent.Add(parameter.Key, parameter.Value);
                    }
                }

                UnityAnalytics.Instance.RecordEvent(analyticsEvent);
                return ServiceResult.Success();
            }
            catch (ArgumentException exception)
            {
                Debug.LogWarning("Unity Analytics rejected an event parameter: " + exception.Message);
                return ServiceResult.InvalidRequest(exception.Message);
            }
            catch (InvalidOperationException exception)
            {
                Debug.LogWarning("Unity Analytics could not record an event: " + exception.Message);
                return ServiceResult.Failed(exception.Message);
            }
        }
    }

    sealed class UnityRemoteConfigProvider : IRemoteConfigService
    {
        readonly FrontierUnityServicesConfig config;
        readonly UnityServicesInitializer initializer;

        public UnityRemoteConfigProvider(FrontierUnityServicesConfig config, UnityServicesInitializer initializer)
        {
            this.config = config;
            this.initializer = initializer;
        }

        public bool IsConfigured { get { return config != null && config.CanUseRemoteConfig; } }

        public Task<ServiceResult> InitializeAsync()
        {
            return IsConfigured
                ? initializer.InitializeAsync()
                : Task.FromResult(ServiceResult.NotConfigured("Unity Remote Config"));
        }

        public async Task<ServiceResult<IReadOnlyDictionary<string, string>>> FetchAsync()
        {
            if (!IsConfigured)
                return ServiceResult<IReadOnlyDictionary<string, string>>.NotConfigured("Unity Remote Config");

            ServiceResult initializedResult = await initializer.InitializeAsync();
            if (!initializedResult.succeeded)
                return ServiceResult<IReadOnlyDictionary<string, string>>.Failed(initializedResult.text);

            if (!AuthenticationService.Instance.IsSignedIn)
                return ServiceResult<IReadOnlyDictionary<string, string>>.InvalidRequest(
                    ServiceMessage.Get("service.remote.sign-in"));

            try
            {
                RuntimeConfig runtimeConfig = await UnityRemoteConfig.Instance.FetchConfigsAsync(
                    new Dictionary<string, object>(), new Dictionary<string, object>());
                var settings = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (string key in runtimeConfig.GetKeys())
                    settings.Add(key, runtimeConfig.GetJson(key));
                return ServiceResult<IReadOnlyDictionary<string, string>>.Success(settings);
            }
            catch (RequestFailedException exception)
            {
                Debug.LogWarning("Unity Remote Config fetch failed: " + exception.Message);
                return ServiceResult<IReadOnlyDictionary<string, string>>.Failed(exception.Message);
            }
        }
    }

    public static class FrontierUnityServicesFactory
    {
        public static FrontierServices Create(IConsentService consent, FrontierUnityServicesConfig config)
        {
            var unavailable = new UnavailableFrontierProvider();
            var initializer = new UnityServicesInitializer(config);
            IRecoveryCredentialProvider recoveryCredentials = config != null && config.CanUseAccountRecovery
                ? UnityRecoveryCredentialProviderRegistry.GetOrCreate()
                : null;
            var authentication = new UnityAuthenticationProvider(config, initializer, recoveryCredentials);
            var leaderboards = new UnityLeaderboardProvider(config, initializer);
            var cloudSave = new UnityCloudSaveProvider(config, initializer);
            var analytics = new UnityAnalyticsProvider(config, initializer);
            var remoteConfig = new UnityRemoteConfigProvider(config, initializer);
            var time = new LocalTimeService();
            var adsPolicy = Resources.Load<RewardedAdsPolicy>("Frontier/RewardedAdsPolicy");
            if (adsPolicy == null) throw new InvalidOperationException("Frontier rewarded-ad policy is missing.");
            var ads = adsPolicy.Wrap(new LevelPlayAdsProvider(adsPolicy, config, consent, UnityLevelPlayAdsSdk.Instance),
                new PlayerPrefsRewardedAdUsageStore(), time);
            return new FrontierServices(consent, ads, unavailable, authentication, leaderboards,
                cloudSave, analytics, remoteConfig, time, new UnityNotificationProvider(config),
                config == null ? null : config.leaderboardId,
                config != null && config.HasValidPrivacyPolicy ? config.privacyPolicyUrl : null,
                new LevelPlayDevelopmentTests(adsPolicy, config, consent));
        }
    }
}
