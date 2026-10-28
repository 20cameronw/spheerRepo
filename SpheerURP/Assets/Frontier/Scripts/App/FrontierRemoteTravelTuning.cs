using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Frontier.Economy;
using Frontier.Services;
using UnityEngine;

namespace Frontier
{
    public sealed class FrontierRemoteTravelTuning : MonoBehaviour
    {
        const string CacheKey = "fr_remote_travel_tuning";

        [Serializable]
        sealed class CachedValue
        {
            public string key;
            public string value;
        }

        [Serializable]
        sealed class Snapshot
        {
            public int version;
            public string bundledDigest;
            public string playerId;
            public CachedValue[] values;
        }

        GameDatabase bundled;
        FrontierServices services;
        string requestedPlayer;
        string cacheKey = CacheKey;
        int consentRevision;
        bool pending;

        public static GameDatabase SelectForSession(GameDatabase bundled, FrontierServices services, string cacheKey = CacheKey)
        {
            if (!Allowed(services) || !bundled.UsesImportedEconomy
                || string.IsNullOrEmpty(services.Authentication.CurrentPlayerId)
                || !PlayerPrefs.HasKey(cacheKey)) return bundled;
            var snapshot = new Snapshot();
            try { JsonUtility.FromJsonOverwrite(PlayerPrefs.GetString(cacheKey), snapshot); }
            catch (ArgumentException error)
            {
                Debug.LogWarning("Frontier: invalid remote travel cache; using bundled tuning. " + error.Message);
                return bundled;
            }
            if (snapshot.playerId != services.Authentication.CurrentPlayerId) return bundled;
            if (snapshot.version != 1 || snapshot.bundledDigest != bundled.ContentSource.tuning.digest
                || snapshot.values == null)
            {
                Debug.LogWarning("Frontier: stale or incomplete remote travel cache; using bundled tuning.");
                return bundled;
            }
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var row in snapshot.values)
            {
                if (row == null || string.IsNullOrEmpty(row.key) || values.ContainsKey(row.key))
                {
                    Debug.LogWarning("Frontier: invalid remote travel cache entries; using bundled tuning.");
                    return bundled;
                }
                values.Add(row.key, row.value);
            }
            if (bundled.TryWithTravelOverrides(values, out var effective, out var reason)) return effective;
            Debug.LogWarning("Frontier: remote travel cache rejected; using bundled tuning. " + reason);
            return bundled;
        }

        static bool Allowed(FrontierServices serviceSet)
        {
            return serviceSet != null && serviceSet.RemoteConfig.IsConfigured
                && serviceSet.Consent.Current.hasDecision && serviceSet.Consent.Current.onlineServicesAllowed;
        }

        public void Init(GameDatabase database, FrontierServices serviceSet, string cacheKey = CacheKey)
        {
            bundled = database;
            services = serviceSet;
            this.cacheKey = cacheKey;
            services.Consent.PreferencesChanged += OnConsentChanged;
        }

        void OnConsentChanged(ConsentPreferences preferences)
        {
            if (preferences.hasDecision && preferences.onlineServicesAllowed) return;
            consentRevision++;
            PlayerPrefs.DeleteKey(cacheKey);
            PlayerPrefs.Save();
        }

        void OnDestroy()
        {
            if (services != null) services.Consent.PreferencesChanged -= OnConsentChanged;
        }

        void Update()
        {
            if (pending || !Allowed(services) || !bundled.UsesImportedEconomy) return;
            string player = services.Authentication.CurrentPlayerId;
            if (string.IsNullOrEmpty(player) || player == requestedPlayer) return;
            requestedPlayer = player;
            StartCoroutine(Fetch(player));
        }

        IEnumerator Fetch(string player)
        {
            pending = true;
            int revision = consentRevision;
            try
            {
                var task = services.RemoteConfig.FetchAsync();
                while (!task.IsCompleted) yield return null;
                if (task.IsFaulted)
                {
                    Debug.LogException(task.Exception);
                    yield break;
                }
                if (task.IsCanceled)
                {
                    Debug.LogWarning("Frontier: remote travel tuning fetch cancelled; retaining previous tuning.");
                    yield break;
                }
                var result = task.Result;
                if (!result.succeeded)
                {
                    Debug.LogWarning("Frontier: remote travel tuning fetch failed; retaining previous tuning. " + result.message);
                    yield break;
                }
                if (revision != consentRevision || !Allowed(services)
                    || services.Authentication.CurrentPlayerId != player) yield break;
                string error = null;
                if (result.value == null || !TravelTuningOverrides.TryApply(bundled.ContentSource.tuning.Build(),
                    result.value, out var effective, out error))
                {
                    Debug.LogWarning("Frontier: remote travel tuning rejected; retaining previous tuning. "
                        + (result.value == null ? "Missing settings." : error));
                    yield break;
                }
                var snapshot = new Snapshot
                {
                    version = 1,
                    bundledDigest = bundled.ContentSource.tuning.digest,
                    playerId = player,
                    values = result.value.Select(pair => new CachedValue
                        { key = pair.Key, value = effective.Get(pair.Key).ToString("R", CultureInfo.InvariantCulture) }).ToArray()
                };
                PlayerPrefs.SetString(cacheKey, JsonUtility.ToJson(snapshot));
                PlayerPrefs.Save();
                Debug.Log("Frontier: validated remote travel tuning staged for the next scene startup; active prices and trips unchanged.");
            }
            finally { pending = false; }
        }
    }
}
