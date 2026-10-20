using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Frontier.Services
{
    public sealed class BuilderNotificationScheduler
    {
        readonly INotificationService provider;
        readonly Dictionary<string, NotificationRequest> scheduled = new Dictionary<string, NotificationRequest>();
        bool busy, cleared, permissionGranted, permissionAttempted;
        int revision;

        public void Invalidate() { revision++; }

        public BuilderNotificationScheduler(INotificationService provider)
        {
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public async Task<ServiceResult> SyncAsync(bool enabled, IReadOnlyList<NotificationRequest> jobs,
            DateTimeOffset now, bool mayRequestPermission)
        {
            if (jobs == null) throw new ArgumentNullException(nameof(jobs));
            if (busy) return ServiceResult.InvalidRequest(ServiceMessage.Get("service.notifications.busy"));
            if (!provider.IsConfigured) return ServiceResult.NotConfigured("Local notifications");
            var desired = new Dictionary<string, NotificationRequest>(StringComparer.Ordinal);
            if (enabled)
            {
                if (jobs.Count > 8) return ServiceResult.InvalidRequest(ServiceMessage.Get("service.notifications.limit"));
                foreach (var job in jobs)
                {
                    bool known = false;
                    for (int i = 1; i <= 8; i++) known |= job.id == "frontier-crew-" + i;
                    if (!known || desired.ContainsKey(job.id)
                        || string.IsNullOrWhiteSpace(job.title) || string.IsNullOrWhiteSpace(job.body))
                        return ServiceResult.InvalidRequest(ServiceMessage.Get("service.notifications.jobs"));
                    if (job.scheduledUtc > now) desired.Add(job.id, job);
                }
            }
            busy = true;
            int requestedRevision = revision;
            try
            {
                if (!cleared)
                {
                    for (int i = 1; i <= 8; i++)
                    {
                        var cancelled = await provider.CancelAsync("frontier-crew-" + i);
                        if (!cancelled.succeeded) return cancelled;
                    }
                    cleared = true;
                }
                foreach (string id in new List<string>(scheduled.Keys))
                {
                    if (desired.ContainsKey(id)) continue;
                    var cancelled = await provider.CancelAsync(id);
                    if (!cancelled.succeeded) return cancelled;
                    scheduled.Remove(id);
                }
                if (!enabled)
                {
                    permissionAttempted = false;
                    permissionGranted = false;
                    return ServiceResult.Success();
                }
                if (desired.Count == 0) return ServiceResult.Success();
                if (!permissionGranted)
                {
                    if (!mayRequestPermission || permissionAttempted)
                        return ServiceResult.InvalidRequest(ServiceMessage.Get("service.notifications.request"));
                    permissionAttempted = true;
                    var permission = await provider.RequestPermissionAsync();
                    if (!permission.succeeded) return permission;
                    permissionGranted = true;
                }
                if (requestedRevision != revision)
                    return ServiceResult.InvalidRequest(ServiceMessage.Get("service.notifications.permission-changed"));
                foreach (var pair in desired)
                {
                    if (requestedRevision != revision)
                        return ServiceResult.InvalidRequest(ServiceMessage.Get("service.notifications.sync-changed"));
                    if (scheduled.TryGetValue(pair.Key, out var previous)
                        && Math.Abs((previous.scheduledUtc - pair.Value.scheduledUtc).TotalSeconds) < 2
                        && previous.title == pair.Value.title && previous.body == pair.Value.body) continue;
                    var result = await provider.ScheduleAsync(pair.Value);
                    if (!result.succeeded) return result;
                    scheduled[pair.Key] = pair.Value;
                    if (requestedRevision != revision)
                    {
                        var cancelled = await provider.CancelAsync(pair.Key);
                        if (!cancelled.succeeded) return cancelled;
                        scheduled.Remove(pair.Key);
                        return ServiceResult.InvalidRequest(ServiceMessage.Get("service.notifications.schedule-changed"));
                    }
                }
                return ServiceResult.Success();
            }
            finally { busy = false; }
        }
    }
}
