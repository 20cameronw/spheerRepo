using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Frontier.Services;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Frontier.Tests
{
    public sealed class BuilderNotificationFlowTests
    {
        sealed class Provider : INotificationService
        {
            public bool IsConfigured { get { return true; } }
            public int permissions;
            public readonly List<NotificationRequest> sent = new List<NotificationRequest>();
            public readonly List<string> cancelled = new List<string>();
            public TaskCompletionSource<ServiceResult> pending;
            public Task<ServiceResult> InitializeAsync() { return Task.FromResult(ServiceResult.Success()); }
            public Task<ServiceResult> RequestPermissionAsync()
            {
                permissions++;
                return pending != null ? pending.Task : Task.FromResult(ServiceResult.Success());
            }
            public Task<ServiceResult> ScheduleAsync(NotificationRequest request)
            {
                sent.Add(request);
                return Task.FromResult(ServiceResult.Success());
            }
            public Task<ServiceResult> CancelAsync(string id)
            {
                cancelled.Add(id);
                return Task.FromResult(ServiceResult.Success());
            }
        }

        string directory, previousDirectory, previousError;
        bool previousPreference;
        GameState state, previousState;
        Provider provider;
        GameObject watcher;

        [SetUp]
        public void SetUp()
        {
            previousPreference = NotificationPreferences.Enabled;
            previousError = NotificationPreferences.LastError;
            NotificationPreferences.Enabled = true;
            previousDirectory = GameState.SaveDirectoryOverride;
            previousState = GameState.I;
            directory = Path.Combine(Path.GetTempPath(), "frontier-notifications-test-" + Guid.NewGuid().ToString("N"));
            GameState.SaveDirectoryOverride = directory;
            state = new GameState(GameDatabase.Load(), false, new FakeClock());
            provider = new Provider();
            watcher = new GameObject("Builder notification test");
            watcher.AddComponent<FrontierBuilderNotifications>().Init(state, provider);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            UnityEngine.Object.Destroy(watcher);
            yield return null;
            NotificationPreferences.Enabled = previousPreference;
            NotificationPreferences.LastError = previousError;
            GameState.SaveDirectoryOverride = previousDirectory;
            GameState.I = previousState;
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        void StartJob()
        {
            var refinery = state.DebugPlace(state.db.StructureOfKind(StructureKind.Refinery), 0);
            state.DebugSetBuilderJob(refinery, 120);
        }

        [UnityTest]
        public IEnumerator FirstSavedJobSchedulesAndResetCancelsWithoutChangingGameplay()
        {
            yield return null;
            yield return null;
            Assert.AreEqual(0, provider.permissions);
            StartJob();
            yield return null;
            yield return null;
            Assert.AreEqual(1, provider.permissions);
            Assert.AreEqual(1, provider.sent.Count);
            Assert.IsFalse(state.saves.IsDirty);
            Assert.AreEqual(1, state.BusyBuilderCount);
            double delay = (provider.sent[0].scheduledUtc - DateTimeOffset.UtcNow).TotalSeconds;
            Assert.That(delay, Is.InRange(115, 121));
            state.Add(state.db.fuel, 1);
            yield return null;
            yield return null;
            Assert.AreEqual(1, provider.sent.Count, "Inventory changes must not reschedule a stable timer.");
            provider.cancelled.Clear();
            state.ResetProgress();
            yield return null;
            yield return null;
            CollectionAssert.Contains(provider.cancelled, "frontier-crew-1");
            Assert.IsEmpty(state.ActiveBuilderCompletionTimes());
        }

        [UnityTest]
        public IEnumerator OptOutWhilePermissionIsPendingCannotDeliverAReminder()
        {
            provider.pending = new TaskCompletionSource<ServiceResult>();
            StartJob();
            yield return null;
            yield return null;
            Assert.AreEqual(1, provider.permissions);
            NotificationPreferences.Enabled = false;
            provider.pending.SetResult(ServiceResult.Success());
            yield return null;
            yield return null;
            Assert.AreEqual(0, provider.sent.Count);
            Assert.AreEqual(1, state.BusyBuilderCount);
        }
    }
}
