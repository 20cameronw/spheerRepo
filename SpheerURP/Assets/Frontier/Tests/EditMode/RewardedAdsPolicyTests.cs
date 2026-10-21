using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Frontier.Services;
using Frontier.Services.Unity;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class RewardedAdsPolicyTests
    {
        sealed class Clock : ITimeService
        {
            public DateTimeOffset now = new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);
            public DateTimeOffset UtcNow { get { return now; } }
            public bool IsServerTrusted { get { return false; } }
        }

        sealed class Store : IRewardedAdUsageStore
        {
            public readonly Dictionary<string, RewardedAdUsage> rows = new Dictionary<string, RewardedAdUsage>();
            public bool failLoad, failSave;
            public bool TryLoad(string id, out RewardedAdUsage usage, out string error)
            {
                usage = rows.TryGetValue(id, out var row) ? row : new RewardedAdUsage();
                error = failLoad ? "Test read failed" : null;
                return !failLoad;
            }
            public bool TrySave(string id, RewardedAdUsage usage, out string error)
            {
                error = failSave ? "Test write failed" : null;
                if (failSave) return false;
                rows[id] = usage;
                return true;
            }
        }

        sealed class Provider : IAdsService
        {
            public bool configured = true;
            public bool IsConfigured { get { return configured; } }
            public int shows, initializes;
            public Func<string, Task<ServiceResult<RewardedAdResult>>> show;
            public Task<ServiceResult> InitializeAsync()
            {
                initializes++;
                return Task.FromResult(ServiceResult.Success());
            }
            public Task<ServiceResult<RewardedAdResult>> ShowRewardedAsync(string id)
            {
                shows++;
                return show != null ? show(id) : Task.FromResult(Earned(id));
            }
        }

        static ServiceResult<RewardedAdResult> Earned(string id)
        {
            return ServiceResult<RewardedAdResult>.Success(new RewardedAdResult(true, id));
        }

        static RewardedAdsPolicyService Policy(Provider provider, Store store, Clock clock,
            int cap = 2, int cooldown = 60, bool enabled = true)
        {
            return new RewardedAdsPolicyService(provider, store, clock, enabled, new[]
            {
                new RewardedAdPlacement { id = "double-yield", dailyCap = cap, cooldownSeconds = cooldown },
                new RewardedAdPlacement { id = "free-shards", dailyCap = 1, cooldownSeconds = 0 }
            });
        }

        [Test]
        public async Task DisabledUnknownAndZeroCapPlacementsNeverReachTheProvider()
        {
            var provider = new Provider();
            var store = new Store();
            var clock = new Clock();
            var disabled = Policy(provider, store, clock, enabled: false);
            Assert.IsFalse(disabled.IsConfigured);
            Assert.AreEqual(ServiceStatus.NotConfigured, (await disabled.InitializeAsync()).status);
            Assert.AreEqual(ServiceStatus.NotConfigured, (await disabled.ShowRewardedAsync("double-yield")).status);
            var policy = Policy(provider, store, clock);
            foreach (var id in new[] { null, "", "unknown", "DOUBLE-YIELD" })
                Assert.AreEqual(ServiceStatus.InvalidRequest, (await policy.ShowRewardedAsync(id)).status);
            Assert.AreEqual(ServiceStatus.InvalidRequest,
                (await Policy(provider, store, clock, cap: 0).ShowRewardedAsync("double-yield")).status);
            Assert.AreEqual(0, provider.shows + provider.initializes);
            Assert.AreEqual(0, store.rows.Count);
            provider.configured = false;
            Assert.IsFalse(policy.IsConfigured);
            Assert.AreEqual(ServiceStatus.NotConfigured, (await policy.ShowRewardedAsync("double-yield")).status);
        }

        [Test]
        public async Task CapsAndCooldownsPersistAcrossServiceRecreationAndArePerPlacement()
        {
            var provider = new Provider();
            var store = new Store();
            var clock = new Clock();
            Assert.IsTrue((await Policy(provider, store, clock).ShowRewardedAsync("double-yield")).succeeded);
            var restarted = Policy(provider, store, clock);
            clock.now = clock.now.AddSeconds(59);
            Assert.AreEqual(ServiceStatus.InvalidRequest, (await restarted.ShowRewardedAsync("double-yield")).status);
            Assert.IsTrue((await restarted.ShowRewardedAsync("free-shards")).succeeded);
            clock.now = clock.now.AddSeconds(1);
            Assert.IsTrue((await restarted.ShowRewardedAsync("double-yield")).succeeded);
            clock.now = clock.now.AddSeconds(60);
            Assert.AreEqual(ServiceStatus.InvalidRequest, (await restarted.ShowRewardedAsync("double-yield")).status);
            Assert.AreEqual(3, provider.shows);
            Assert.AreEqual(2, store.rows["double-yield"].rewards);
        }

        [Test]
        public async Task UtcDayResetDoesNotBypassAnActiveCooldown()
        {
            var provider = new Provider();
            var store = new Store();
            var clock = new Clock { now = new DateTimeOffset(2026, 10, 19, 23, 59, 30, TimeSpan.Zero) };
            var policy = Policy(provider, store, clock, cap: 1);
            Assert.IsTrue((await policy.ShowRewardedAsync("double-yield")).succeeded);
            clock.now = clock.now.AddSeconds(30);
            Assert.AreEqual(ServiceStatus.InvalidRequest, (await policy.ShowRewardedAsync("double-yield")).status);
            clock.now = clock.now.AddSeconds(30);
            Assert.IsTrue((await policy.ShowRewardedAsync("double-yield")).succeeded);
            Assert.AreEqual(1, store.rows["double-yield"].rewards);
            Assert.AreEqual(clock.now.UtcDateTime.Date.Ticks, store.rows["double-yield"].dayUtcTicks);
        }

        [Test]
        public async Task PendingRequestsAreExclusiveAndCompletionAfterMidnightCountsOnTheNewDay()
        {
            var provider = new Provider();
            var store = new Store();
            var clock = new Clock { now = new DateTimeOffset(2026, 10, 19, 23, 59, 59, TimeSpan.Zero) };
            var pending = new TaskCompletionSource<ServiceResult<RewardedAdResult>>();
            provider.show = _ => pending.Task;
            var policy = Policy(provider, store, clock);
            var first = policy.ShowRewardedAsync("double-yield");
            Assert.AreEqual(ServiceStatus.InvalidRequest, (await policy.ShowRewardedAsync("free-shards")).status);
            clock.now = clock.now.AddSeconds(2);
            pending.SetResult(Earned("double-yield"));
            Assert.IsTrue((await first).succeeded);
            Assert.AreEqual(1, provider.shows);
            Assert.AreEqual(clock.now.UtcDateTime.Date.Ticks, store.rows["double-yield"].dayUtcTicks);
        }

        [Test]
        public async Task FailedIncompleteAndMismatchedProviderResultsNeverConsumeAReward()
        {
            var provider = new Provider();
            var store = new Store();
            var clock = new Clock();
            var policy = Policy(provider, store, clock);
            foreach (var response in new[]
            {
                ServiceResult<RewardedAdResult>.Failed("Test provider failure"),
                ServiceResult<RewardedAdResult>.Success(new RewardedAdResult(false, "double-yield")),
                Earned("free-shards")
            })
            {
                provider.show = _ => Task.FromResult(response);
                var result = await policy.ShowRewardedAsync("double-yield");
                Assert.IsFalse(result.succeeded && result.value.rewardEarned);
                Assert.AreEqual(0, store.rows["double-yield"].rewards);
            }
            provider.show = null;
            Assert.IsTrue((await policy.ShowRewardedAsync("double-yield")).succeeded);
        }

        [Test]
        public async Task ReadAndWriteFailuresDoNotDeliverARewardAndCanBeRetried()
        {
            var provider = new Provider();
            var store = new Store { failLoad = true };
            var policy = Policy(provider, store, new Clock());
            Assert.AreEqual("Test read failed", (await policy.ShowRewardedAsync("double-yield")).message);
            store.failLoad = false;
            store.failSave = true;
            Assert.AreEqual("Test write failed", (await policy.ShowRewardedAsync("double-yield")).message);
            Assert.AreEqual(0, provider.shows);
            store.failSave = false;
            provider.show = id =>
            {
                store.failSave = true;
                return Task.FromResult(Earned(id));
            };
            Assert.AreEqual(ServiceStatus.Failed, (await policy.ShowRewardedAsync("double-yield")).status);
            Assert.AreEqual(0, store.rows["double-yield"].rewards);
            store.failSave = false;
            provider.show = null;
            Assert.IsTrue((await policy.ShowRewardedAsync("double-yield")).succeeded);
            Assert.AreEqual(1, store.rows["double-yield"].rewards);
        }

        [Test]
        public async Task InvalidUsageAndClockRewindFailClosed()
        {
            var provider = new Provider();
            var store = new Store();
            var clock = new Clock();
            var policy = Policy(provider, store, clock);
            foreach (var usage in new[]
            {
                new RewardedAdUsage { version = 2 },
                new RewardedAdUsage { rewards = -1 },
                new RewardedAdUsage { dayUtcTicks = 1 },
                new RewardedAdUsage { lastObservedUtcTicks = -1 },
                new RewardedAdUsage { lastRewardUtcTicks = 1 }
            })
            {
                store.rows["double-yield"] = usage;
                Assert.AreEqual(ServiceStatus.InvalidRequest, (await policy.ShowRewardedAsync("double-yield")).status);
            }
            Assert.AreEqual(0, provider.shows);
            store.rows.Clear();
            Assert.IsTrue((await policy.ShowRewardedAsync("double-yield")).succeeded);
            clock.now = clock.now.AddDays(-1);
            Assert.AreEqual(ServiceStatus.InvalidRequest, (await policy.ShowRewardedAsync("double-yield")).status);
            Assert.AreEqual(1, provider.shows);
        }

        [Test]
        public async Task ClockRewindDuringPlaybackRejectsRewardAndAllowsLaterRetry()
        {
            var provider = new Provider();
            var store = new Store();
            var clock = new Clock();
            var policy = Policy(provider, store, clock);
            var original = clock.now;
            provider.show = id =>
            {
                clock.now = clock.now.AddSeconds(-1);
                return Task.FromResult(Earned(id));
            };
            Assert.AreEqual(ServiceStatus.InvalidRequest, (await policy.ShowRewardedAsync("double-yield")).status);
            Assert.AreEqual(0, store.rows["double-yield"].rewards);
            clock.now = original;
            provider.show = null;
            Assert.IsTrue((await policy.ShowRewardedAsync("double-yield")).succeeded);
        }

        [Test]
        public async Task ProviderExceptionsReleaseThePendingGateWithoutGranting()
        {
            var provider = new Provider
            {
                show = _ => throw new InvalidOperationException("Test provider exception")
            };
            var store = new Store();
            var policy = Policy(provider, store, new Clock());
            Assert.ThrowsAsync<InvalidOperationException>(async () => await policy.ShowRewardedAsync("double-yield"));
            Assert.AreEqual(0, store.rows["double-yield"].rewards);
            provider.show = null;
            Assert.IsTrue((await policy.ShowRewardedAsync("double-yield")).succeeded);
        }

        [Test]
        public void AuthoredPolicyHasCappedPlacementsButIosRemainsGated()
        {
            var policy = Resources.Load<RewardedAdsPolicy>("Frontier/RewardedAdsPolicy");
            Assert.IsNotNull(policy);
            Assert.IsTrue(policy.enableRewardedAds);
            Assert.IsFalse(policy.iosSetupVerified);
            Assert.AreEqual("28e95b2f5", policy.iosAppKey);
            Assert.AreEqual("eurexbly1unznz2g", policy.iosRewardedAdUnitId);
            CollectionAssert.AreEquivalent(new[] { "double-yield", "emergency-fuel", "timer-skip", "revive", "daily-supply", "free-shards" },
                Array.ConvertAll(policy.placements, row => row.id));
            foreach (var placement in policy.placements) Assert.Greater(placement.dailyCap, 0);
            Assert.IsFalse(policy.CanUseIos(Resources.Load<FrontierUnityServicesConfig>("Frontier/FrontierUnityServicesConfig")));
        }

        [Test]
        public void InvalidAndDuplicatePolicyEntriesAreRejectedAndAuthoredLimitsAreCopied()
        {
            foreach (var placements in new[]
            {
                new RewardedAdPlacement[] { null },
                new[] { new RewardedAdPlacement { id = "" } },
                new[] { new RewardedAdPlacement { id = "x", dailyCap = -1 } },
                new[] { new RewardedAdPlacement { id = "x", cooldownSeconds = -1 } },
                new[] { new RewardedAdPlacement { id = "x" }, new RewardedAdPlacement { id = "x" } }
            })
                Assert.Throws<ArgumentException>(() => new RewardedAdsPolicyService(new Provider(), new Store(), new Clock(), true, placements));
            var row = new RewardedAdPlacement { id = "x", dailyCap = 0 };
            var provider = new Provider();
            var policy = new RewardedAdsPolicyService(provider, new Store(), new Clock(), true, new[] { row });
            row.dailyCap = 10;
            Assert.AreEqual(ServiceStatus.InvalidRequest, policy.ShowRewardedAsync("x").Result.status);
            Assert.AreEqual(0, provider.shows);
        }

        [Test]
        public void PlayerPrefsStoreRoundTripsWithoutResettingMalformedUsage()
        {
            string prefix = "fr_rewarded_test_" + Guid.NewGuid().ToString("N");
            string key = prefix + "x";
            try
            {
                var store = new PlayerPrefsRewardedAdUsageStore(prefix);
                Assert.IsTrue(store.TryLoad("x", out var empty, out _));
                Assert.AreEqual(0, empty.rewards);
                var usage = new RewardedAdUsage { dayUtcTicks = 0, lastObservedUtcTicks = 10, lastRewardUtcTicks = 5, rewards = 1 };
                Assert.IsTrue(store.TrySave("x", usage, out _));
                Assert.IsTrue(store.TryLoad("x", out var actual, out _));
                Assert.AreEqual(usage.rewards, actual.rewards);
                Assert.AreEqual(usage.lastObservedUtcTicks, actual.lastObservedUtcTicks);
                Assert.AreEqual(usage.lastRewardUtcTicks, actual.lastRewardUtcTicks);
                foreach (string json in new[] { "", "{}", "invalid json", "{\"version\":2}" })
                {
                    PlayerPrefs.SetString(key, json);
                    Assert.IsFalse(store.TryLoad("x", out _, out string error), json);
                    Assert.IsNotEmpty(error);
                    Assert.AreEqual(json, PlayerPrefs.GetString(key), "Corrupt usage must not be erased or reset.");
                }
            }
            finally { PlayerPrefs.DeleteKey(key); PlayerPrefs.Save(); }
        }
    }
}
