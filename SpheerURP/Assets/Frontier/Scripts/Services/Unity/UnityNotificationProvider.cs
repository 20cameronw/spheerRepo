using System;
using System.Globalization;
using System.Threading.Tasks;
using UnityEngine;
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
using Unity.Notifications;
#endif

namespace Frontier.Services.Unity
{
    public sealed class UnityNotificationProvider : INotificationService
    {
        readonly FrontierUnityServicesConfig config;
        Task<ServiceResult> pendingPermission;

        public UnityNotificationProvider(FrontierUnityServicesConfig config) { this.config = config; }

        public bool IsConfigured
        {
            get
            {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
                return config != null && config.enableNotifications;
#else
                return false;
#endif
            }
        }

        public Task<ServiceResult> InitializeAsync()
        {
            if (!IsConfigured) return Task.FromResult(ServiceResult.NotConfigured("Local notifications"));
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            var args = NotificationCenterArgs.Default;
            args.AndroidChannelId = "frontier-builders";
            args.AndroidChannelName = "Construction";
            args.AndroidChannelDescription = "Completed construction, upgrades and clearance";
            NotificationCenter.Initialize(args);
#endif
            return Task.FromResult(ServiceResult.Success());
        }

        public Task<ServiceResult> RequestPermissionAsync()
        {
            if (pendingPermission != null && !pendingPermission.IsCompleted) return pendingPermission;
            pendingPermission = RequestNativePermissionAsync();
            return pendingPermission;
        }

        async Task<ServiceResult> RequestNativePermissionAsync()
        {
            var initialized = await InitializeAsync();
            if (!initialized.succeeded) return initialized;
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            var request = NotificationCenter.RequestPermission();
            while (request.Status == NotificationsPermissionStatus.RequestPending) await Task.Yield();
            return request.Status == NotificationsPermissionStatus.Granted
                ? ServiceResult.Success()
                : ServiceResult.InvalidRequest("Notification permission was denied. Enable it in device settings to receive completion alerts.");
#else
            return ServiceResult.NotConfigured("Local notifications");
#endif
        }

        public async Task<ServiceResult> ScheduleAsync(NotificationRequest request)
        {
            if (!TryIdentifier(request.id, out int identifier) || string.IsNullOrWhiteSpace(request.title)
                || string.IsNullOrWhiteSpace(request.body) || request.scheduledUtc <= DateTimeOffset.UtcNow)
                return ServiceResult.InvalidRequest("A future Frontier crew notification with title and body is required.");
            var initialized = await InitializeAsync();
            if (!initialized.succeeded) return initialized;
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            bool allowed;
#if UNITY_ANDROID
            allowed = global::Unity.Notifications.Android.AndroidNotificationCenter.UserPermissionToPost
                == global::Unity.Notifications.Android.PermissionStatus.Allowed;
#else
            allowed = global::Unity.Notifications.iOS.iOSNotificationCenter.GetNotificationSettings().AuthorizationStatus
                == global::Unity.Notifications.iOS.AuthorizationStatus.Authorized;
#endif
            if (!allowed) return ServiceResult.InvalidRequest("Device notification permission is no longer granted.");
            NotificationCenter.ScheduleNotification(new Notification
            {
                Identifier = identifier, Title = request.title, Text = request.body
            }, new NotificationIntervalSchedule(request.scheduledUtc - DateTimeOffset.UtcNow));
#endif
            return ServiceResult.Success();
        }

        public async Task<ServiceResult> CancelAsync(string id)
        {
            if (!TryIdentifier(id, out int identifier))
                return ServiceResult.InvalidRequest("A valid Frontier crew notification ID is required.");
            var initialized = await InitializeAsync();
            if (!initialized.succeeded) return initialized;
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            NotificationCenter.CancelScheduledNotification(identifier);
            NotificationCenter.CancelDeliveredNotification(identifier);
#endif
            return ServiceResult.Success();
        }

        static bool TryIdentifier(string id, out int identifier)
        {
            identifier = 0;
            const string prefix = "frontier-crew-";
            if (id == null || !id.StartsWith(prefix, StringComparison.Ordinal)
                || !int.TryParse(id.Substring(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture,
                    out int slot) || slot < 1 || slot > 8) return false;
            identifier = 27000 + slot;
            return true;
        }
    }
}
