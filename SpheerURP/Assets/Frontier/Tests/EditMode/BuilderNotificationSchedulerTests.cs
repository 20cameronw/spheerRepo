using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Frontier.Services;
using Frontier.Services.Unity;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class BuilderNotificationSchedulerTests
    {
        sealed class Provider : INotificationService
        {
            public bool configured = true;
            public bool IsConfigured { get { return configured; } }
            public int permissions, sends;
            public readonly List<string> cancellations = new List<string>();
            public ServiceResult permission = ServiceResult.Success();
            public ServiceResult schedule = ServiceResult.Success();
            public ServiceResult cancel = ServiceResult.Success();
            public TaskCompletionSource<ServiceResult> pending;
            public TaskCompletionSource<ServiceResult> pendingSchedule;
            public Task<ServiceResult> InitializeAsync() { return Task.FromResult(ServiceResult.Success()); }
            public Task<ServiceResult> RequestPermissionAsync()
            {
                permissions++;
                return pending != null ? pending.Task : Task.FromResult(permission);
            }
            public Task<ServiceResult> ScheduleAsync(NotificationRequest request)
            {
                sends++;
                return pendingSchedule != null ? pendingSchedule.Task : Task.FromResult(schedule);
            }
            public Task<ServiceResult> CancelAsync(string id)
            {
                cancellations.Add(id);
                return Task.FromResult(cancel);
            }
        }

        static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-19T12:00:00Z");
        static NotificationRequest Job(int slot = 1, int seconds = 60)
        {
            return new NotificationRequest("frontier-crew-" + slot, "Crew job complete", "Check your crews.",
                Now.AddSeconds(seconds));
        }
        static NotificationRequest[] NoJobs { get { return Array.Empty<NotificationRequest>(); } }

        [Test]
        public async Task NoLaunchPromptThenFirstActiveTimerRequestsPermissionOnlyOnce()
        {
            var provider = new Provider();
            var scheduler = new BuilderNotificationScheduler(provider);
            Assert.IsTrue((await scheduler.SyncAsync(true, NoJobs, Now, true)).succeeded);
            Assert.AreEqual(8, provider.cancellations.Count, "Clear our previous-session IDs without asking permission.");
            Assert.AreEqual(0, provider.permissions);
            Assert.IsTrue((await scheduler.SyncAsync(true, new[] { Job() }, Now, true)).succeeded);
            Assert.IsTrue((await scheduler.SyncAsync(true, new[] { Job() }, Now, true)).succeeded);
            Assert.AreEqual(1, provider.permissions);
            Assert.AreEqual(1, provider.sends);
        }

        [Test]
        public async Task DisabledAndExpiredJobsNeverAskOrSend()
        {
            var provider = new Provider();
            var scheduler = new BuilderNotificationScheduler(provider);
            Assert.IsTrue((await scheduler.SyncAsync(false, new[] { Job() }, Now, true)).succeeded);
            Assert.IsTrue((await scheduler.SyncAsync(true, new[] { Job(seconds: -1) }, Now, true)).succeeded);
            Assert.AreEqual(0, provider.permissions + provider.sends);
        }

        [Test]
        public async Task RemovedJobsAndOptOutCancelScheduledAndDeliveredIds()
        {
            var provider = new Provider();
            var scheduler = new BuilderNotificationScheduler(provider);
            await scheduler.SyncAsync(true, new[] { Job(), Job(2, 120) }, Now, true);
            provider.cancellations.Clear();
            await scheduler.SyncAsync(true, new[] { Job() }, Now, true);
            CollectionAssert.AreEqual(new[] { "frontier-crew-2" }, provider.cancellations);
            await scheduler.SyncAsync(false, NoJobs, Now, true);
            CollectionAssert.AreEqual(new[] { "frontier-crew-2", "frontier-crew-1" }, provider.cancellations);
        }

        [Test]
        public async Task DenialNeverSchedulesOrRepeatedlyPromptsAndExplicitOptOutAllowsRetry()
        {
            var provider = new Provider { permission = ServiceResult.InvalidRequest("Denied") };
            var scheduler = new BuilderNotificationScheduler(provider);
            Assert.IsFalse((await scheduler.SyncAsync(true, new[] { Job() }, Now, true)).succeeded);
            Assert.IsFalse((await scheduler.SyncAsync(true, new[] { Job() }, Now, true)).succeeded);
            Assert.AreEqual(1, provider.permissions);
            Assert.AreEqual(0, provider.sends);
            await scheduler.SyncAsync(false, NoJobs, Now, true);
            provider.permission = ServiceResult.Success();
            Assert.IsTrue((await scheduler.SyncAsync(true, new[] { Job() }, Now, true)).succeeded);
            Assert.AreEqual(2, provider.permissions);
        }

        [Test]
        public async Task BackgroundSyncDoesNotOpenAPermissionDialog()
        {
            var provider = new Provider();
            var scheduler = new BuilderNotificationScheduler(provider);
            Assert.IsFalse((await scheduler.SyncAsync(true, new[] { Job() }, Now, false)).succeeded);
            Assert.AreEqual(0, provider.permissions);
            Assert.IsTrue((await scheduler.SyncAsync(true, new[] { Job() }, Now, true)).succeeded);
        }

        [Test]
        public async Task PendingPermissionInvalidationAndOverlapsCannotSendStaleReminders()
        {
            var provider = new Provider { pending = new TaskCompletionSource<ServiceResult>() };
            var scheduler = new BuilderNotificationScheduler(provider);
            var pending = scheduler.SyncAsync(true, new[] { Job() }, Now, true);
            Assert.IsFalse((await scheduler.SyncAsync(false, NoJobs, Now, true)).succeeded);
            scheduler.Invalidate();
            provider.pending.SetResult(ServiceResult.Success());
            Assert.IsFalse((await pending).succeeded);
            Assert.AreEqual(0, provider.sends);
            Assert.IsTrue((await scheduler.SyncAsync(false, NoJobs, Now, true)).succeeded);
        }

        [Test]
        public async Task OptOutDuringProviderSchedulingCancelsItsLateDelivery()
        {
            var provider = new Provider { pendingSchedule = new TaskCompletionSource<ServiceResult>() };
            var scheduler = new BuilderNotificationScheduler(provider);
            var pending = scheduler.SyncAsync(true, new[] { Job() }, Now, true);
            provider.cancellations.Clear();
            scheduler.Invalidate();
            provider.pendingSchedule.SetResult(ServiceResult.Success());
            Assert.IsFalse((await pending).succeeded);
            CollectionAssert.AreEqual(new[] { "frontier-crew-1" }, provider.cancellations);
            Assert.IsTrue((await scheduler.SyncAsync(false, NoJobs, Now, true)).succeeded);
        }

        [Test]
        public async Task FailedScheduleCanRetryAndTimestampJitterDoesNotReschedule()
        {
            var provider = new Provider { schedule = ServiceResult.Failed("Native failure") };
            var scheduler = new BuilderNotificationScheduler(provider);
            Assert.IsFalse((await scheduler.SyncAsync(true, new[] { Job() }, Now, true)).succeeded);
            provider.schedule = ServiceResult.Success();
            Assert.IsTrue((await scheduler.SyncAsync(true, new[] { Job() }, Now, true)).succeeded);
            await scheduler.SyncAsync(true, new[] { Job(seconds: 61) }, Now, true);
            Assert.AreEqual(2, provider.sends);
            await scheduler.SyncAsync(true, new[] { Job(seconds: 30) }, Now, true);
            Assert.AreEqual(3, provider.sends, "A genuine shortened timer replaces the notification.");
        }

        [Test]
        public async Task FailedStartupCancellationDoesNotScheduleAndCanRetry()
        {
            var provider = new Provider { cancel = ServiceResult.Failed("Cancel failed") };
            var scheduler = new BuilderNotificationScheduler(provider);
            Assert.IsFalse((await scheduler.SyncAsync(true, new[] { Job() }, Now, true)).succeeded);
            Assert.AreEqual(0, provider.permissions + provider.sends);
            provider.cancel = ServiceResult.Success();
            Assert.IsTrue((await scheduler.SyncAsync(true, new[] { Job() }, Now, true)).succeeded);
        }

        [Test]
        public async Task InvalidOrDuplicateIdsAreRejectedBeforeTouchingNativeProvider()
        {
            var provider = new Provider();
            var scheduler = new BuilderNotificationScheduler(provider);
            Assert.IsFalse((await scheduler.SyncAsync(true, new[] { Job(9) }, Now, true)).succeeded);
            Assert.IsFalse((await scheduler.SyncAsync(true, new[] { Job(), Job() }, Now, true)).succeeded);
            Assert.AreEqual(0, provider.permissions + provider.sends + provider.cancellations.Count);
        }

        [Test]
        public async Task DesktopAndUnconfiguredNativeProviderRemainUnavailable()
        {
            var config = ScriptableObject.CreateInstance<FrontierUnityServicesConfig>();
            try
            {
                Assert.IsFalse(config.enableNotifications);
                var provider = new UnityNotificationProvider(config);
                Assert.IsFalse(provider.IsConfigured);
                Assert.AreEqual(ServiceStatus.NotConfigured, (await provider.RequestPermissionAsync()).status);
                config.enableNotifications = true;
                Assert.IsFalse(provider.IsConfigured, "Editor must never request a native permission.");
                Assert.AreEqual(ServiceStatus.InvalidRequest, (await provider.CancelAsync("other-app-id")).status);
                Assert.AreEqual(ServiceStatus.NotConfigured, (await provider.CancelAsync("frontier-crew-1")).status);
            }
            finally { UnityEngine.Object.DestroyImmediate(config); }
        }

        [Test]
        public void SafeNativeSettingsCannotEnableLaunchPromptPushOrExactAlarms()
        {
            EditorTools.FrontierNotificationSettings.Configure();
            Assert.DoesNotThrow(() => new EditorTools.FrontierNotificationSettings().OnPreprocessBuild(null));
        }
    }
}
