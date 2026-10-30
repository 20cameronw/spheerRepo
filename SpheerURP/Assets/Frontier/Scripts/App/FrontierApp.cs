using System;
using Frontier.Services;
using UnityEngine;

namespace Frontier
{
    /// <summary>
    /// The app lifecycle of a running session: ticks the game state every frame (debounced save, play time) and writes the save
    /// immediately when the app goes to the background, loses focus, quits or is torn down. Created by <see cref="FrontierBootstrap"/>.
    /// </summary>
    public sealed class FrontierApp : MonoBehaviour
    {
        GameState state;
        FrontierServices services;
        bool analyticsInitializing;
        bool analyticsInitialized;
        bool resumePending;

        public void Init(GameState gs, FrontierServices serviceSet = null)
        {
            state = gs;
            services = serviceSet;
            if (services == null) return;
            services.Consent.PreferencesChanged += OnConsentChanged;
            InitializeAnalytics();
        }

        void OnConsentChanged(ConsentPreferences preferences) { InitializeAnalytics(); }

        async void InitializeAnalytics()
        {
            if (services == null || analyticsInitializing || analyticsInitialized ||
                !services.Analytics.IsConfigured || !services.Consent.Current.hasDecision ||
                !services.Consent.Current.analyticsAllowed) return;
            analyticsInitializing = true;
            var current = services;
            try
            {
                var result = await current.Analytics.InitializeAsync();
                if (this == null || services != current) return;
                analyticsInitialized = result.succeeded;
                if (!result.succeeded)
                    Debug.LogWarning("Frontier: analytics initialization failed: " + result.text.Resolve(UiText.Get));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally { analyticsInitializing = false; }
        }

        void Update() { if (state != null) state.Tick(); }

        void OnApplicationPause(bool paused)
        {
            if (paused) { resumePending = true; if (state != null) state.Save(); }
            else ResumeProgress();
        }

        void ResumeProgress()
        {
            if (!resumePending) return;
            resumePending = false;
            state?.ResumeOfflineProgress();
        }

        void OnApplicationFocus(bool focused)
        {
            if (!focused) { resumePending = true; if (state != null) state.Save(); }
            if (focused) { ResumeProgress(); InitializeAnalytics(); }
        }

        void OnApplicationQuit() { if (state != null) state.Flush(); }

        void OnDestroy()
        {
            if (services != null)
            {
                services.Consent.PreferencesChanged -= OnConsentChanged;
                services.Analytics.SetDataCollectionAllowed(false);
                services = null;
            }
            if (state != null) state.Flush();
        }
    }
}
