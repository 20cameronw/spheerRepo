using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Frontier.Services;
using Frontier.Services.Unity;
using UnityEngine;

namespace Frontier
{
    public sealed class FrontierBuilderNotifications : MonoBehaviour
    {
        GameState state;
        INotificationService provider;
        BuilderNotificationScheduler scheduler;
        bool dirty = true, pending, paused;
        bool previousEnabled, synchronized;
        List<DateTimeOffset> previousTimes = new List<DateTimeOffset>();

        public void Init(GameState gameState, INotificationService notifications)
        {
            state = gameState;
            provider = notifications;
            scheduler = new BuilderNotificationScheduler(notifications);
            state.Changed += MarkDirty;
            NotificationPreferences.Changed += MarkDirty;
        }

        void MarkDirty()
        {
            dirty = true;
            scheduler.Invalidate();
        }

        void OnApplicationPause(bool value)
        {
            paused = value;
            if (!value) MarkDirty();
        }

        void Update()
        {
            if (state == null || !provider.IsConfigured || !dirty || pending || paused) return;
            dirty = false;
            _ = SynchronizeAsync();
        }

        async Task SynchronizeAsync()
        {
            pending = true;
            try
            {
                bool enabled = NotificationPreferences.Enabled && !state.SaveIsReadOnly;
                var times = enabled ? state.ActiveBuilderCompletionTimes() : new List<DateTimeOffset>();
                bool sameTimes = times.Count == previousTimes.Count;
                for (int i = 0; sameTimes && i < times.Count; i++) sameTimes = times[i] == previousTimes[i];
                if (synchronized && enabled == previousEnabled && sameTimes) return;
                if (enabled) state.Flush();
                // A reminder must not describe a job that failed to reach the persistent save.
                enabled &= !state.saves.IsDirty;
                var jobs = new List<NotificationRequest>();
                var now = DateTimeOffset.UtcNow;
                if (enabled)
                {
                    for (int i = 0; i < times.Count; i++)
                    {
                        var remaining = times[i] - state.clock.UtcNow;
                        jobs.Add(new NotificationRequest("frontier-crew-" + (i + 1),
                            UiText.Get("notifications.job.title"), UiText.Get("notifications.job.body"), now + remaining));
                    }
                }
                var result = await scheduler.SyncAsync(enabled, jobs, now, !paused);
                if (!result.succeeded && !dirty)
                {
                    NotificationPreferences.LastError = result.message;
                    Debug.LogWarning("Frontier: builder notification synchronization failed. " + result.message);
                    synchronized = true;
                    previousEnabled = enabled;
                    previousTimes = times;
                }
                else if (result.succeeded)
                {
                    NotificationPreferences.LastError = null;
                    synchronized = true;
                    previousEnabled = enabled;
                    previousTimes = times;
                }
            }
            catch (Exception error)
            {
                NotificationPreferences.LastError = error.Message;
                Debug.LogException(error);
            }
            finally { pending = false; }
        }

        void OnDestroy()
        {
            if (state != null) state.Changed -= MarkDirty;
            NotificationPreferences.Changed -= MarkDirty;
            scheduler?.Invalidate();
        }
    }
}
