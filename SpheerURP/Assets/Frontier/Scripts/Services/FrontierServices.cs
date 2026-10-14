using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Frontier.Services
{
    public enum ServiceStatus
    {
        Success,
        NotConfigured,
        ConsentRequired,
        InvalidRequest,
        Failed
    }

    public readonly struct ServiceResult
    {
        public readonly ServiceStatus status;
        public readonly string message;
        public bool succeeded { get { return status == ServiceStatus.Success; } }

        ServiceResult(ServiceStatus status, string message)
        {
            this.status = status;
            this.message = message ?? string.Empty;
        }

        public static ServiceResult Success(string message = "") { return new ServiceResult(ServiceStatus.Success, message); }
        public static ServiceResult NotConfigured(string service) { return new ServiceResult(ServiceStatus.NotConfigured, service + " is not configured."); }
        public static ServiceResult ConsentRequired(string category) { return new ServiceResult(ServiceStatus.ConsentRequired, "Opt-in consent is required for " + category + "."); }
        public static ServiceResult InvalidRequest(string message) { return new ServiceResult(ServiceStatus.InvalidRequest, message); }
        public static ServiceResult Failed(string message) { return new ServiceResult(ServiceStatus.Failed, message); }
    }

    public readonly struct ServiceResult<T>
    {
        public readonly ServiceStatus status;
        public readonly T value;
        public readonly string message;
        public bool succeeded { get { return status == ServiceStatus.Success; } }

        ServiceResult(ServiceStatus status, T value, string message)
        {
            this.status = status;
            this.value = value;
            this.message = message ?? string.Empty;
        }

        public static ServiceResult<T> Success(T value, string message = "") { return new ServiceResult<T>(ServiceStatus.Success, value, message); }
        public static ServiceResult<T> NotConfigured(string service) { return new ServiceResult<T>(ServiceStatus.NotConfigured, default(T), service + " is not configured."); }
        public static ServiceResult<T> ConsentRequired(string category) { return new ServiceResult<T>(ServiceStatus.ConsentRequired, default(T), "Opt-in consent is required for " + category + "."); }
        public static ServiceResult<T> InvalidRequest(string message) { return new ServiceResult<T>(ServiceStatus.InvalidRequest, default(T), message); }
        public static ServiceResult<T> Failed(string message) { return new ServiceResult<T>(ServiceStatus.Failed, default(T), message); }
    }

    public interface IServiceEndpoint
    {
        bool IsConfigured { get; }
        Task<ServiceResult> InitializeAsync();
    }

    public readonly struct StoreProduct
    {
        public readonly string id;
        public readonly string localizedPrice;
        public readonly string displayName;

        public StoreProduct(string id, string localizedPrice, string displayName)
        {
            this.id = id ?? string.Empty;
            this.localizedPrice = localizedPrice ?? string.Empty;
            this.displayName = displayName ?? string.Empty;
        }
    }

    public readonly struct PurchaseReceipt
    {
        public readonly string productId;
        public readonly string transactionId;

        public PurchaseReceipt(string productId, string transactionId)
        {
            this.productId = productId ?? string.Empty;
            this.transactionId = transactionId ?? string.Empty;
        }
    }

    public readonly struct RewardedAdResult
    {
        public readonly bool rewardEarned;
        public readonly string placementId;

        public RewardedAdResult(bool rewardEarned, string placementId)
        {
            this.rewardEarned = rewardEarned;
            this.placementId = placementId ?? string.Empty;
        }
    }

    public readonly struct AuthIdentity
    {
        public readonly string playerId;
        public readonly bool isAnonymous;

        public AuthIdentity(string playerId, bool isAnonymous)
        {
            this.playerId = playerId ?? string.Empty;
            this.isAnonymous = isAnonymous;
        }
    }

    public readonly struct LeaderboardEntry
    {
        public readonly string playerId;
        public readonly string displayName;
        public readonly long score;
        public readonly int rank;

        public LeaderboardEntry(string playerId, string displayName, long score, int rank)
        {
            this.playerId = playerId ?? string.Empty;
            this.displayName = displayName ?? string.Empty;
            this.score = score;
            this.rank = rank;
        }
    }

    public readonly struct CloudSaveRecord
    {
        public readonly string payload;
        public readonly string writeLock;

        public CloudSaveRecord(string payload, string writeLock)
        {
            this.payload = payload ?? string.Empty;
            this.writeLock = writeLock ?? string.Empty;
        }
    }

    public readonly struct NotificationRequest
    {
        public readonly string id;
        public readonly string title;
        public readonly string body;
        public readonly DateTimeOffset scheduledUtc;

        public NotificationRequest(string id, string title, string body, DateTimeOffset scheduledUtc)
        {
            this.id = id ?? string.Empty;
            this.title = title ?? string.Empty;
            this.body = body ?? string.Empty;
            this.scheduledUtc = scheduledUtc;
        }
    }

    public interface IAdsService : IServiceEndpoint
    {
        Task<ServiceResult<RewardedAdResult>> ShowRewardedAsync(string placementId);
    }

    public interface IPurchaseService : IServiceEndpoint
    {
        Task<ServiceResult<IReadOnlyList<StoreProduct>>> FetchProductsAsync(IReadOnlyList<string> productIds);
        Task<ServiceResult<PurchaseReceipt>> PurchaseAsync(string productId);
        Task<ServiceResult<IReadOnlyList<PurchaseReceipt>>> RestoreAsync();
    }

    public interface IAuthService : IServiceEndpoint
    {
        Task<ServiceResult<AuthIdentity>> SignInAnonymouslyAsync();
        Task<ServiceResult> DeleteAccountAsync();
    }

    public interface ILeaderboardService : IServiceEndpoint
    {
        Task<ServiceResult<IReadOnlyList<LeaderboardEntry>>> GetScoresAsync(string boardId, int limit);
        Task<ServiceResult> SubmitScoreAsync(string boardId, long score);
    }

    public interface ICloudSaveService : IServiceEndpoint
    {
        Task<ServiceResult<CloudSaveRecord>> ReadAsync();
        Task<ServiceResult<CloudSaveRecord>> WriteAsync(string payload, string writeLock);
    }

    public interface IAnalyticsService : IServiceEndpoint
    {
        ServiceResult RecordEvent(string eventName, IReadOnlyDictionary<string, string> parameters);
    }

    public interface IRemoteConfigService : IServiceEndpoint
    {
        Task<ServiceResult<IReadOnlyDictionary<string, string>>> FetchAsync();
    }

    public interface ITimeService
    {
        DateTimeOffset UtcNow { get; }
        bool IsServerTrusted { get; }
    }

    public interface INotificationService : IServiceEndpoint
    {
        Task<ServiceResult> RequestPermissionAsync();
        Task<ServiceResult> ScheduleAsync(NotificationRequest request);
        Task<ServiceResult> CancelAsync(string id);
    }

    public readonly struct ConsentPreferences
    {
        public readonly int policyVersion;
        public readonly bool hasDecision;
        public readonly bool analyticsAllowed;
        public readonly bool adsAllowed;
        public readonly bool onlineServicesAllowed;
        public readonly long decisionUtcTicks;

        public ConsentPreferences(int policyVersion, bool hasDecision, bool analyticsAllowed, bool adsAllowed,
            bool onlineServicesAllowed, long decisionUtcTicks)
        {
            this.policyVersion = Math.Max(0, policyVersion);
            this.hasDecision = hasDecision;
            this.analyticsAllowed = hasDecision && analyticsAllowed;
            this.adsAllowed = hasDecision && adsAllowed;
            this.onlineServicesAllowed = hasDecision && onlineServicesAllowed;
            this.decisionUtcTicks = Math.Max(0, decisionUtcTicks);
        }
    }

    public interface IConsentStore
    {
        ConsentPreferences Load();
        void Save(ConsentPreferences preferences);
    }

    public interface IConsentService
    {
        ConsentPreferences Current { get; }
        void SavePreferences(bool analyticsAllowed, bool adsAllowed, bool onlineServicesAllowed);
        void Reset();
    }

    public sealed class ConsentService : IConsentService
    {
        public const int CurrentPolicyVersion = 2;

        readonly IConsentStore store;
        readonly Func<DateTimeOffset> utcNow;
        ConsentPreferences current;

        public ConsentService(IConsentStore store, Func<DateTimeOffset> utcNow = null)
        {
            if (store == null) throw new ArgumentNullException(nameof(store));
            this.store = store;
            this.utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
            var saved = store.Load();
            current = saved.hasDecision && saved.policyVersion == CurrentPolicyVersion
                ? saved
                : new ConsentPreferences(CurrentPolicyVersion, false, false, false, false, 0);
        }

        public ConsentPreferences Current { get { return current; } }

        public void SavePreferences(bool analyticsAllowed, bool adsAllowed, bool onlineServicesAllowed)
        {
            current = new ConsentPreferences(CurrentPolicyVersion, true, analyticsAllowed, adsAllowed, onlineServicesAllowed,
                utcNow().UtcDateTime.Ticks);
            store.Save(current);
        }

        public void Reset()
        {
            current = new ConsentPreferences(CurrentPolicyVersion, false, false, false, false, 0);
            store.Save(current);
        }
    }

    sealed class ConsentGatedAdsService : IAdsService
    {
        readonly IAdsService inner;
        readonly IConsentService consent;

        public ConsentGatedAdsService(IAdsService inner, IConsentService consent)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.consent = consent ?? throw new ArgumentNullException(nameof(consent));
        }

        public bool IsConfigured { get { return inner.IsConfigured; } }

        public Task<ServiceResult> InitializeAsync()
        {
            if (!consent.Current.hasDecision || !consent.Current.adsAllowed)
                return Task.FromResult(ServiceResult.ConsentRequired("advertising"));
            return inner.InitializeAsync();
        }

        public Task<ServiceResult<RewardedAdResult>> ShowRewardedAsync(string placementId)
        {
            if (!consent.Current.hasDecision || !consent.Current.adsAllowed)
                return Task.FromResult(ServiceResult<RewardedAdResult>.ConsentRequired("advertising"));
            if (string.IsNullOrWhiteSpace(placementId))
                return Task.FromResult(ServiceResult<RewardedAdResult>.InvalidRequest("A rewarded-ad placement is required."));
            return inner.ShowRewardedAsync(placementId);
        }
    }

    sealed class ConsentGatedAnalyticsService : IAnalyticsService
    {
        readonly IAnalyticsService inner;
        readonly IConsentService consent;

        public ConsentGatedAnalyticsService(IAnalyticsService inner, IConsentService consent)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.consent = consent ?? throw new ArgumentNullException(nameof(consent));
        }

        public bool IsConfigured { get { return inner.IsConfigured; } }

        public Task<ServiceResult> InitializeAsync()
        {
            if (!consent.Current.hasDecision || !consent.Current.analyticsAllowed)
                return Task.FromResult(ServiceResult.ConsentRequired("analytics"));
            return inner.InitializeAsync();
        }

        public ServiceResult RecordEvent(string eventName, IReadOnlyDictionary<string, string> parameters)
        {
            if (!consent.Current.hasDecision || !consent.Current.analyticsAllowed)
                return ServiceResult.ConsentRequired("analytics");
            if (string.IsNullOrWhiteSpace(eventName))
                return ServiceResult.InvalidRequest("An analytics event name is required.");
            return inner.RecordEvent(eventName, parameters);
        }
    }

    sealed class ConsentGatedAuthenticationService : IAuthService
    {
        readonly IAuthService inner;
        readonly IConsentService consent;

        public ConsentGatedAuthenticationService(IAuthService inner, IConsentService consent)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.consent = consent ?? throw new ArgumentNullException(nameof(consent));
        }

        public bool IsConfigured { get { return inner.IsConfigured; } }

        public Task<ServiceResult> InitializeAsync()
        {
            return consent.Current.hasDecision && consent.Current.onlineServicesAllowed
                ? inner.InitializeAsync()
                : Task.FromResult(ServiceResult.ConsentRequired("online account services"));
        }

        public Task<ServiceResult<AuthIdentity>> SignInAnonymouslyAsync()
        {
            return consent.Current.hasDecision && consent.Current.onlineServicesAllowed
                ? inner.SignInAnonymouslyAsync()
                : Task.FromResult(ServiceResult<AuthIdentity>.ConsentRequired("online account services"));
        }

        public Task<ServiceResult> DeleteAccountAsync()
        {
            return consent.Current.hasDecision && consent.Current.onlineServicesAllowed
                ? inner.DeleteAccountAsync()
                : Task.FromResult(ServiceResult.ConsentRequired("online account services"));
        }
    }

    sealed class ConsentGatedLeaderboardService : ILeaderboardService
    {
        readonly ILeaderboardService inner;
        readonly IConsentService consent;

        public ConsentGatedLeaderboardService(ILeaderboardService inner, IConsentService consent)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.consent = consent ?? throw new ArgumentNullException(nameof(consent));
        }

        public bool IsConfigured { get { return inner.IsConfigured; } }

        public Task<ServiceResult> InitializeAsync()
        {
            return consent.Current.hasDecision && consent.Current.onlineServicesAllowed
                ? inner.InitializeAsync()
                : Task.FromResult(ServiceResult.ConsentRequired("online leaderboards"));
        }

        public Task<ServiceResult<IReadOnlyList<LeaderboardEntry>>> GetScoresAsync(string boardId, int limit)
        {
            if (!consent.Current.hasDecision || !consent.Current.onlineServicesAllowed)
                return Task.FromResult(ServiceResult<IReadOnlyList<LeaderboardEntry>>.ConsentRequired("online leaderboards"));
            if (string.IsNullOrWhiteSpace(boardId) || limit < 1)
                return Task.FromResult(ServiceResult<IReadOnlyList<LeaderboardEntry>>.InvalidRequest(
                    "A leaderboard ID and positive result limit are required."));
            return inner.GetScoresAsync(boardId, limit);
        }

        public Task<ServiceResult> SubmitScoreAsync(string boardId, long score)
        {
            if (!consent.Current.hasDecision || !consent.Current.onlineServicesAllowed)
                return Task.FromResult(ServiceResult.ConsentRequired("online leaderboards"));
            if (string.IsNullOrWhiteSpace(boardId) || score < 0)
                return Task.FromResult(ServiceResult.InvalidRequest("A leaderboard ID and non-negative score are required."));
            return inner.SubmitScoreAsync(boardId, score);
        }
    }

    sealed class ConsentGatedCloudSaveService : ICloudSaveService
    {
        readonly ICloudSaveService inner;
        readonly IConsentService consent;

        public ConsentGatedCloudSaveService(ICloudSaveService inner, IConsentService consent)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.consent = consent ?? throw new ArgumentNullException(nameof(consent));
        }

        public bool IsConfigured { get { return inner.IsConfigured; } }

        public Task<ServiceResult> InitializeAsync()
        {
            return consent.Current.hasDecision && consent.Current.onlineServicesAllowed
                ? inner.InitializeAsync()
                : Task.FromResult(ServiceResult.ConsentRequired("cloud saves"));
        }

        public Task<ServiceResult<CloudSaveRecord>> ReadAsync()
        {
            return consent.Current.hasDecision && consent.Current.onlineServicesAllowed
                ? inner.ReadAsync()
                : Task.FromResult(ServiceResult<CloudSaveRecord>.ConsentRequired("cloud saves"));
        }

        public Task<ServiceResult<CloudSaveRecord>> WriteAsync(string payload, string writeLock)
        {
            if (!consent.Current.hasDecision || !consent.Current.onlineServicesAllowed)
                return Task.FromResult(ServiceResult<CloudSaveRecord>.ConsentRequired("cloud saves"));
            if (payload == null)
                return Task.FromResult(ServiceResult<CloudSaveRecord>.InvalidRequest("A cloud-save payload is required."));
            return inner.WriteAsync(payload, writeLock);
        }
    }

    public sealed class UnavailableFrontierProvider : IAdsService, IPurchaseService, IAuthService,
        ILeaderboardService, ICloudSaveService, IAnalyticsService, IRemoteConfigService, INotificationService
    {
        public bool IsConfigured { get { return false; } }

        public Task<ServiceResult> InitializeAsync() { return Task.FromResult(ServiceResult.NotConfigured("External services")); }
        public Task<ServiceResult<RewardedAdResult>> ShowRewardedAsync(string placementId)
        { return Task.FromResult(ServiceResult<RewardedAdResult>.NotConfigured("Rewarded ads")); }
        public Task<ServiceResult<IReadOnlyList<StoreProduct>>> FetchProductsAsync(IReadOnlyList<string> productIds)
        { return Task.FromResult(ServiceResult<IReadOnlyList<StoreProduct>>.NotConfigured("Purchases")); }
        public Task<ServiceResult<PurchaseReceipt>> PurchaseAsync(string productId)
        { return Task.FromResult(ServiceResult<PurchaseReceipt>.NotConfigured("Purchases")); }
        public Task<ServiceResult<IReadOnlyList<PurchaseReceipt>>> RestoreAsync()
        { return Task.FromResult(ServiceResult<IReadOnlyList<PurchaseReceipt>>.NotConfigured("Purchases")); }
        public Task<ServiceResult<AuthIdentity>> SignInAnonymouslyAsync()
        { return Task.FromResult(ServiceResult<AuthIdentity>.NotConfigured("Authentication")); }
        public Task<ServiceResult> DeleteAccountAsync() { return Task.FromResult(ServiceResult.NotConfigured("Authentication")); }
        public Task<ServiceResult<IReadOnlyList<LeaderboardEntry>>> GetScoresAsync(string boardId, int limit)
        { return Task.FromResult(ServiceResult<IReadOnlyList<LeaderboardEntry>>.NotConfigured("Leaderboards")); }
        public Task<ServiceResult> SubmitScoreAsync(string boardId, long score)
        { return Task.FromResult(ServiceResult.NotConfigured("Leaderboards")); }
        public Task<ServiceResult<CloudSaveRecord>> ReadAsync()
        { return Task.FromResult(ServiceResult<CloudSaveRecord>.NotConfigured("Cloud Save")); }
        public Task<ServiceResult<CloudSaveRecord>> WriteAsync(string payload, string writeLock)
        { return Task.FromResult(ServiceResult<CloudSaveRecord>.NotConfigured("Cloud Save")); }
        public ServiceResult RecordEvent(string eventName, IReadOnlyDictionary<string, string> parameters)
        { return ServiceResult.NotConfigured("Analytics"); }
        public Task<ServiceResult<IReadOnlyDictionary<string, string>>> FetchAsync()
        { return Task.FromResult(ServiceResult<IReadOnlyDictionary<string, string>>.NotConfigured("Remote Config")); }
        public Task<ServiceResult> RequestPermissionAsync() { return Task.FromResult(ServiceResult.NotConfigured("Notifications")); }
        public Task<ServiceResult> ScheduleAsync(NotificationRequest request) { return Task.FromResult(ServiceResult.NotConfigured("Notifications")); }
        public Task<ServiceResult> CancelAsync(string id) { return Task.FromResult(ServiceResult.NotConfigured("Notifications")); }
    }

    public sealed class LocalTimeService : ITimeService
    {
        public DateTimeOffset UtcNow { get { return DateTimeOffset.UtcNow; } }
        public bool IsServerTrusted { get { return false; } }
    }

    public sealed class FrontierServices
    {
        public readonly IConsentService Consent;
        public readonly IAdsService Ads;
        public readonly IPurchaseService Purchases;
        public readonly IAuthService Authentication;
        public readonly ILeaderboardService Leaderboards;
        public readonly ICloudSaveService CloudSave;
        public readonly IAnalyticsService Analytics;
        public readonly IRemoteConfigService RemoteConfig;
        public readonly ITimeService Time;
        public readonly INotificationService Notifications;
        public readonly string LeaderboardId;
        public readonly string PrivacyPolicyUrl;

        public FrontierServices(IConsentService consent, IAdsService ads, IPurchaseService purchases,
            IAuthService authentication, ILeaderboardService leaderboards, ICloudSaveService cloudSave,
            IAnalyticsService analytics, IRemoteConfigService remoteConfig, ITimeService time,
            INotificationService notifications, string leaderboardId = null, string privacyPolicyUrl = null)
        {
            Consent = consent ?? throw new ArgumentNullException(nameof(consent));
            Ads = new ConsentGatedAdsService(ads, consent);
            Purchases = purchases ?? throw new ArgumentNullException(nameof(purchases));
            Authentication = new ConsentGatedAuthenticationService(authentication, consent);
            Leaderboards = new ConsentGatedLeaderboardService(leaderboards, consent);
            CloudSave = new ConsentGatedCloudSaveService(cloudSave, consent);
            Analytics = new ConsentGatedAnalyticsService(analytics, consent);
            RemoteConfig = remoteConfig ?? throw new ArgumentNullException(nameof(remoteConfig));
            Time = time ?? throw new ArgumentNullException(nameof(time));
            Notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
            LeaderboardId = leaderboardId ?? string.Empty;
            PrivacyPolicyUrl = privacyPolicyUrl ?? string.Empty;
        }

        public static FrontierServices CreateOffline(IConsentService consent)
        {
            var unavailable = new UnavailableFrontierProvider();
            return new FrontierServices(consent, unavailable, unavailable, unavailable, unavailable,
                unavailable, unavailable, unavailable, new LocalTimeService(), unavailable);
        }
    }
}
