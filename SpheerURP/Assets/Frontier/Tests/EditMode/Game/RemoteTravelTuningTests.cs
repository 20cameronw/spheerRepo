using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Frontier.Economy;
using Frontier.Services;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Frontier.Tests
{
    public sealed class RemoteTravelTuningTests
    {
        sealed class ConsentStore : IConsentStore
        {
            public ConsentPreferences value;
            public ConsentPreferences Load() { return value; }
            public void Save(ConsentPreferences preferences) { value = preferences; }
        }

        sealed class Remote : IRemoteConfigService
        {
            public bool IsConfigured { get { return true; } }
            public int requests;
            public Task<ServiceResult> InitializeAsync() { return Task.FromResult(ServiceResult.Success()); }
            public Task<ServiceResult<IReadOnlyDictionary<string, string>>> FetchAsync()
            {
                requests++;
                return Task.FromResult(ServiceResult<IReadOnlyDictionary<string, string>>.Success(
                    new Dictionary<string, string>()));
            }
        }

        sealed class Auth : IAuthService
        {
            public string player = "player";
            public string CurrentPlayerId { get { return player; } }
            public bool IsConfigured { get { return true; } }
            public bool CanRecoverAccount { get { return false; } }
            public string RecoveryProviderName { get { return ""; } }
            public Task<ServiceResult> InitializeAsync() { return Task.FromResult(ServiceResult.Success()); }
            public Task<ServiceResult<AuthIdentity>> SignInAnonymouslyAsync()
            {
                return Task.FromResult(ServiceResult<AuthIdentity>.NotConfigured("Test"));
            }
            public Task<ServiceResult<AuthIdentity>> LinkRecoveryAccountAsync() { return SignInAnonymouslyAsync(); }
            public Task<ServiceResult<AuthIdentity>> SignInWithRecoveryAccountAsync() { return SignInAnonymouslyAsync(); }
            public Task<ServiceResult> DeleteAccountAsync(string expectedPlayerId)
            {
                return Task.FromResult(ServiceResult.NotConfigured("Test"));
            }
        }

        string cacheKey;
        GameDatabase bundled, selected;
        Remote remote;
        Auth auth;
        FrontierServices services;

        [SetUp]
        public void SetUp()
        {
            cacheKey = "frontier_test_remote_" + Guid.NewGuid().ToString("N");
            bundled = GameDatabase.Load();
            remote = new Remote();
            auth = new Auth();
            var consent = new ConsentService(new ConsentStore());
            consent.SavePreferences(false, false, true);
            var unavailable = new UnavailableFrontierProvider();
            services = new FrontierServices(consent, unavailable, unavailable, auth,
                unavailable, unavailable, unavailable, remote, new LocalTimeService(), unavailable);
        }

        [TearDown]
        public void TearDown()
        {
            if (selected != null && selected != bundled) UnityEngine.Object.DestroyImmediate(selected);
            PlayerPrefs.DeleteKey(cacheKey);
        }

        void Cache(string values, string digest = null)
        {
            PlayerPrefs.SetString(cacheKey, "{\"version\":1,\"playerId\":\"player\",\"bundledDigest\":\""
                + (digest ?? bundled.ContentSource.tuning.digest) + "\",\"values\":" + values + "}");
        }

        [Test]
        public void ValidCacheBuildsAnIsolatedSessionAndNeverFetchesOrChangesAuthoredContent()
        {
            string authoredDigest = bundled.ContentSource.tuning.digest;
            float oldK = bundled.tuning.Build().Get("travel.k");
            Cache("[{\"key\":\"travel.k\",\"value\":\"0.5\"}]");
            selected = FrontierRemoteTravelTuning.SelectForSession(bundled, services, cacheKey);
            Assert.AreNotSame(bundled, selected);
            Assert.AreSame(selected, GameDatabase.ForRuntime(selected));
            Assert.AreEqual(0.5f, selected.tuning.Build().Get("travel.k"));
            Assert.AreEqual(oldK, bundled.tuning.Build().Get("travel.k"));
            Assert.AreEqual(authoredDigest, bundled.ContentSource.tuning.digest);
            CollectionAssert.AreEqual(bundled.resources, selected.resources);
            Assert.AreSame(bundled.shardRules, selected.shardRules);
            Assert.AreEqual(0, remote.requests);
            var baseline = UniverseService.For(bundled);
            var tuned = UniverseService.For(selected);
            Assert.AreNotSame(baseline, tuned);
            Assert.AreNotSame(baseline.Rules, tuned.Rules);
            Assert.AreEqual(6, baseline.Rules.Model.LegFuelCost(20, 0, 0, 0, false));
            Assert.AreEqual(15, tuned.Rules.Model.LegFuelCost(20, 0, 0, 0, false));
        }

        [Test]
        public void DisabledProviderNoConsentAndMissingCacheAllKeepBundledTuning()
        {
            Assert.AreSame(bundled, FrontierRemoteTravelTuning.SelectForSession(bundled, services, cacheKey));
            Cache("[{\"key\":\"travel.k\",\"value\":\"0.5\"}]");
            Assert.AreSame(bundled, FrontierRemoteTravelTuning.SelectForSession(bundled,
                FrontierServices.CreateOffline(services.Consent), cacheKey));
            services.Consent.SavePreferences(false, false, false);
            Assert.AreSame(bundled, FrontierRemoteTravelTuning.SelectForSession(bundled, services, cacheKey));
            Assert.AreEqual(0, remote.requests);
        }

        [TestCase("[{\"key\":\"travel.k\",\"value\":\"NaN\"}]")]
        [TestCase("[{\"key\":\"travel.k\"}]")]
        [TestCase("[{\"key\":\"travel.k\",\"value\":\"999\"}]")]
        [TestCase("[{\"key\":\"shard.pack.1.shards\",\"value\":\"999\"}]")]
        [TestCase("[{\"key\":\"travel.k\",\"value\":\"0.5\"},{\"key\":\"travel.k\",\"value\":\"0.6\"}]")]
        public void InvalidCacheFallsBackExplicitlyWithoutChangingTheDatabase(string values)
        {
            Cache(values);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Frontier: .*remote travel cache"));
            Assert.AreSame(bundled, FrontierRemoteTravelTuning.SelectForSession(bundled, services, cacheKey));
        }

        [Test]
        public void CacheNeverCarriesAccountSpecificTuningToAnotherOrUnsignedIdentity()
        {
            Cache("[{\"key\":\"travel.k\",\"value\":\"0.5\"}]");
            auth.player = "different-player";
            Assert.AreSame(bundled, FrontierRemoteTravelTuning.SelectForSession(bundled, services, cacheKey));
            auth.player = null;
            Assert.AreSame(bundled, FrontierRemoteTravelTuning.SelectForSession(bundled, services, cacheKey));
        }

        [Test]
        public void CacheFromAnOlderBundledCatalogCannotApply()
        {
            Cache("[]", "old-catalog");
            LogAssert.Expect(LogType.Warning, "Frontier: stale or incomplete remote travel cache; using bundled tuning.");
            Assert.AreSame(bundled, FrontierRemoteTravelTuning.SelectForSession(bundled, services, cacheKey));
        }

        [TestCase("travel.run_base_seconds", "120")]
        [TestCase("travel.plating_l2_tier", "10")]
        [TestCase("travel.plating_l2_tier", "2.5")]
        public void RelatedThresholdsAreValidatedTogether(string key, string value)
        {
            var baseline = bundled.tuning.Build();
            Assert.IsFalse(TravelTuningOverrides.TryApply(baseline,
                new Dictionary<string, string> { { key, value }, { "travel.k", "0.5" } },
                out var effective, out var error));
            Assert.IsNotEmpty(error);
            Assert.AreEqual(baseline.Digest(), effective.Digest());
        }
    }
}
