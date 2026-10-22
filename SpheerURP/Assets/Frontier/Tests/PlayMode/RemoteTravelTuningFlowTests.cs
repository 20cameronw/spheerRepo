using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Frontier.Services;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Frontier.Tests
{
    public sealed class RemoteTravelTuningFlowTests
    {
        sealed class ConsentStore : IConsentStore
        {
            public ConsentPreferences value;
            public ConsentPreferences Load() { return value; }
            public void Save(ConsentPreferences preferences) { value = preferences; }
        }

        sealed class Auth : IAuthService
        {
            public string player;
            public string CurrentPlayerId { get { return player; } }
            public bool IsConfigured { get { return true; } }
            public bool CanRecoverAccount { get { return false; } }
            public string RecoveryProviderName { get { return ""; } }
            public int signIns;
            public Task<ServiceResult> InitializeAsync() { return Task.FromResult(ServiceResult.Success()); }
            public Task<ServiceResult<AuthIdentity>> SignInAnonymouslyAsync()
            {
                signIns++;
                return Task.FromResult(ServiceResult<AuthIdentity>.InvalidRequest("Test never signs in."));
            }
            public Task<ServiceResult<AuthIdentity>> LinkRecoveryAccountAsync()
            {
                return Task.FromResult(ServiceResult<AuthIdentity>.InvalidRequest("Test never links."));
            }
            public Task<ServiceResult<AuthIdentity>> SignInWithRecoveryAccountAsync()
            {
                return Task.FromResult(ServiceResult<AuthIdentity>.InvalidRequest("Test never recovers."));
            }
            public Task<ServiceResult> DeleteAccountAsync(string expectedPlayerId)
            {
                return Task.FromResult(ServiceResult.InvalidRequest("Test never deletes."));
            }
        }

        sealed class Remote : IRemoteConfigService
        {
            public bool IsConfigured { get { return true; } }
            public int requests;
            public readonly TaskCompletionSource<ServiceResult<IReadOnlyDictionary<string, string>>> response =
                new TaskCompletionSource<ServiceResult<IReadOnlyDictionary<string, string>>>();
            public Task<ServiceResult> InitializeAsync() { return Task.FromResult(ServiceResult.Success()); }
            public Task<ServiceResult<IReadOnlyDictionary<string, string>>> FetchAsync()
            {
                requests++;
                return response.Task;
            }
            public void Complete()
            {
                response.SetResult(ServiceResult<IReadOnlyDictionary<string, string>>.Success(
                    new Dictionary<string, string> { { "travel.k", "0.5" } }));
            }
        }

        GameObject watcher;
        GameDatabase bundled, selected;
        FrontierServices services;
        Auth auth;
        Remote remote;
        string cacheKey;

        [SetUp]
        public void SetUp()
        {
            cacheKey = "frontier_test_remote_flow_" + Guid.NewGuid().ToString("N");
            bundled = GameDatabase.Load();
            auth = new Auth();
            remote = new Remote();
            var consent = new ConsentService(new ConsentStore());
            consent.SavePreferences(false, false, true);
            var unavailable = new UnavailableFrontierProvider();
            services = new FrontierServices(consent, unavailable, unavailable, auth,
                unavailable, unavailable, unavailable, remote, new LocalTimeService(), unavailable);
            watcher = new GameObject("Remote tuning test");
            watcher.AddComponent<FrontierRemoteTravelTuning>().Init(bundled, services, cacheKey);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            UnityEngine.Object.Destroy(watcher);
            if (selected != null && selected != bundled) UnityEngine.Object.Destroy(selected);
            PlayerPrefs.DeleteKey(cacheKey);
            yield return null;
        }

        [UnityTest]
        public IEnumerator FetchWaitsForExistingIdentityAndStagesWithoutChangingTheRunningSession()
        {
            yield return null;
            yield return null;
            Assert.AreEqual(0, remote.requests);
            Assert.AreEqual(0, auth.signIns);
            auth.player = "existing-player";
            yield return null;
            yield return null;
            Assert.AreEqual(1, remote.requests);
            float currentK = bundled.tuning.Build().Get("travel.k");
            remote.Complete();
            yield return null;
            yield return null;
            Assert.AreEqual(currentK, bundled.tuning.Build().Get("travel.k"));
            Assert.AreEqual(0, auth.signIns);
            Assert.AreEqual(1, remote.requests);
            Assert.IsTrue(PlayerPrefs.HasKey(cacheKey));
            selected = FrontierRemoteTravelTuning.SelectForSession(bundled, services, cacheKey);
            Assert.AreEqual(0.5f, selected.tuning.Build().Get("travel.k"));
        }

        [UnityTest]
        public IEnumerator IdentityChangesDiscardTheResponse()
        {
            auth.player = "before";
            yield return null;
            yield return null;
            auth.player = null;
            remote.Complete();
            yield return null;
            yield return null;
            Assert.IsFalse(PlayerPrefs.HasKey(cacheKey));
        }

        [UnityTest]
        public IEnumerator WithdrawalAfterResponseButBeforeApplicationClearsAndInvalidatesTheCache()
        {
            auth.player = "player";
            yield return null;
            yield return null;
            PlayerPrefs.SetString(cacheKey, "old-cache");
            remote.Complete();
            services.Consent.SavePreferences(false, false, false);
            services.Consent.SavePreferences(false, false, true);
            yield return null;
            Assert.IsFalse(PlayerPrefs.HasKey(cacheKey));
        }
    }
}
