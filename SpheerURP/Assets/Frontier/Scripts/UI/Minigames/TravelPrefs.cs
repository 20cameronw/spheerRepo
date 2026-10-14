using System;
using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    /// <summary>Key/value storage behind <see cref="TravelPrefs"/> (PlayerPrefs in the game, memory in tests).</summary>
    public interface IPrefsStore
    {
        int GetInt(string key, int fallback);
        void SetInt(string key, int value);
        float GetFloat(string key, float fallback);
        void SetFloat(string key, float value);
        void Save();
    }

    public sealed class PlayerPrefsStore : IPrefsStore
    {
        public int GetInt(string key, int fallback) { return PlayerPrefs.GetInt(key, fallback); }
        public void SetInt(string key, int value) { PlayerPrefs.SetInt(key, value); }
        public float GetFloat(string key, float fallback) { return PlayerPrefs.GetFloat(key, fallback); }
        public void SetFloat(string key, float value) { PlayerPrefs.SetFloat(key, value); }
        public void Save() { PlayerPrefs.Save(); }
    }

    public sealed class MemoryPrefsStore : IPrefsStore
    {
        readonly Dictionary<string, float> values = new Dictionary<string, float>();
        public int GetInt(string key, int fallback) { float v; return values.TryGetValue(key, out v) ? (int)v : fallback; }
        public void SetInt(string key, int value) { values[key] = value; }
        public float GetFloat(string key, float fallback) { float v; return values.TryGetValue(key, out v) ? v : fallback; }
        public void SetFloat(string key, float value) { values[key] = value; }
        public void Save() { }
    }

    /// <summary>
    /// Player options of the travel mini-game, kept in the same PlayerPrefs mechanism as the other settings (keys start with fr_).
    /// The store is replaceable so tests never touch the real preferences.
    /// </summary>
    public static class TravelPrefs
    {
        public const string AssistKey = "fr_hs_assist", SensitivityKey = "fr_hs_sensitivity", TutorialRunsKey = "fr_hs_tutorial_runs",
            LegacyKey = "fr_hs_legacy", OverlayKey = "fr_hs_overlay";
        /// <summary>The tips are shown on this many runs, then never again.</summary>
        public const int TutorialRuns = 2;
        public const float SensitivityMin = 0.7f, SensitivityMax = 2.0f, SensitivityStep = 0.1f, SensitivityDefault = 1.3f;

        public static IPrefsStore Store = new PlayerPrefsStore();

        /// <summary>Assist mode: easier travel (smaller encounter budget, softer hits) for lower rewards (quality is capped).</summary>
        public static bool AssistMode
        {
            get { return Store.GetInt(AssistKey, 0) == 1; }
            set { Store.SetInt(AssistKey, value ? 1 : 0); Store.Save(); }
        }

        /// <summary>Relative-drag gain: 1 = the whole play width takes 80 % of the screen width of finger travel.</summary>
        public static float DragSensitivity
        {
            get { return Mathf.Clamp(Store.GetFloat(SensitivityKey, SensitivityDefault), SensitivityMin, SensitivityMax); }
            set { Store.SetFloat(SensitivityKey, Mathf.Clamp(Mathf.Round(value * 10f) / 10f, SensitivityMin, SensitivityMax)); Store.Save(); }
        }

        /// <summary>How many runs showed the first-run tips so far.</summary>
        public static int TutorialRunsShown { get { return Mathf.Max(0, Store.GetInt(TutorialRunsKey, 0)); } }

        public static bool ShowTutorial { get { return TutorialRunsShown < TutorialRuns; } }

        /// <summary>Call once when a run that showed the tips starts.</summary>
        public static void MarkTutorialRunShown()
        {
            Store.SetInt(TutorialRunsKey, TutorialRunsShown + 1);
            Store.Save();
        }

        /// <summary>Debug switch (Settings > Debug in development builds): play the legacy tap game instead of Hyperspace Run.</summary>
        public static bool UseLegacyGame
        {
            get { return Store.GetInt(LegacyKey, 0) == 1; }
            set { Store.SetInt(LegacyKey, value ? 1 : 0); Store.Save(); }
        }

        /// <summary>Development builds: show the performance overlay during a run.</summary>
        public static bool DebugOverlay
        {
            get { return Store.GetInt(OverlayKey, 0) == 1; }
            set { Store.SetInt(OverlayKey, value ? 1 : 0); Store.Save(); }
        }
    }

    /// <summary>Debug-page actions contributed by assemblies above the UI layer (the Settings > Debug page lists them as buttons).</summary>
    public static class TravelDebug
    {
        public struct Entry
        {
            public string label;
            /// <summary>Runs the action and returns a short result line for a toast.</summary>
            public Func<string> run;
        }

        public static readonly List<Entry> Actions = new List<Entry>();

        public static void Register(string label, Func<string> run) { Actions.Add(new Entry { label = label, run = run }); }
    }
}
