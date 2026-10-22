using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace Frontier
{
    public static class PresentationQuality
    {
        public const string QualityKey = "fr_quality";
        public const string FrameRateKey = "fr_frame_rate";
        public static event Action Changed;
        static bool initialized;
        static PerformanceTier automaticTier;
        static int frameCap = 60;
        static PresentationBudget budget;

        public static PresentationBudget Budget { get { EnsureInitialized(); return budget; } }
        public static int TargetFrameRate
        {
            get { return Math.Min(FrameRate == 0 ? Budget.DefaultFrameRate : FrameRate, frameCap); }
        }

        public static void ApplyPanelBudget(PanelSettings panel)
        {
            if (panel == null) throw new ArgumentNullException(nameof(panel));
            var atlas = panel.dynamicAtlasSettings;
            atlas.maxAtlasSize = Budget.UiAtlasMaxSize;
            atlas.minAtlasSize = Math.Min(atlas.minAtlasSize, atlas.maxAtlasSize);
            atlas.maxSubTextureSize = Math.Min(atlas.maxSubTextureSize, atlas.maxAtlasSize);
            panel.dynamicAtlasSettings = atlas;
        }
        public static QualityPreset Preset
        {
            get { EnsureInitialized(); return (QualityPreset)PlayerPrefs.GetInt(QualityKey, 0); }
            set
            {
                if (value < QualityPreset.Automatic || value > QualityPreset.High)
                    throw new ArgumentOutOfRangeException(nameof(value));
                EnsureInitialized();
                PlayerPrefs.SetInt(QualityKey, (int)value);
                PlayerPrefs.Save();
                Apply();
            }
        }
        public static int FrameRate
        {
            get { EnsureInitialized(); return PlayerPrefs.GetInt(FrameRateKey, 0); }
            set
            {
                if (value != 0 && value != 30 && value != 60) throw new ArgumentOutOfRangeException(nameof(value));
                EnsureInitialized();
                PlayerPrefs.SetInt(FrameRateKey, value);
                PlayerPrefs.Save();
                Apply();
            }
        }

        public static void Initialize(int maximumFrameRate)
        {
            if (maximumFrameRate < 1) throw new ArgumentOutOfRangeException(nameof(maximumFrameRate));
            frameCap = maximumFrameRate;
            automaticTier = PresentationBudget.Automatic(Application.isMobilePlatform, SystemInfo.systemMemorySize,
                SystemInfo.graphicsMemorySize, SystemInfo.processorCount);
            int quality = PlayerPrefs.GetInt(QualityKey, 0), fps = PlayerPrefs.GetInt(FrameRateKey, 0);
            bool invalid = quality < 0 || quality > 3 || (fps != 0 && fps != 30 && fps != 60);
            if (invalid)
            {
                Debug.LogWarning("Frontier: invalid saved graphics settings; restoring automatic presentation settings.");
                PlayerPrefs.DeleteKey(QualityKey);
                PlayerPrefs.DeleteKey(FrameRateKey);
                PlayerPrefs.Save();
            }
            initialized = true;
            Apply();
        }

        static void EnsureInitialized()
        {
            if (!initialized) Initialize(60);
        }

        static void Apply()
        {
            var preset = (QualityPreset)PlayerPrefs.GetInt(QualityKey, 0);
            budget = PresentationBudget.For(preset == QualityPreset.Automatic ? automaticTier : (PerformanceTier)((int)preset - 1));
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = TargetFrameRate;
            Changed?.Invoke();
        }
    }

    /// <summary>Owns the runtime post profile and updates only Frontier's camera, never an authored URP asset.</summary>
    public sealed class FrontierPostQuality : MonoBehaviour
    {
        Camera sceneCamera;
        VolumeProfile profile;

        public void Init(Camera camera, VolumeProfile ownedProfile)
        {
            sceneCamera = camera;
            profile = ownedProfile;
            Apply();
        }

        void OnEnable() { PresentationQuality.Changed += Apply; Apply(); }
        void OnDisable() { PresentationQuality.Changed -= Apply; }

        void Apply()
        {
            if (sceneCamera == null || profile == null) return;
            var budget = PresentationQuality.Budget;
            sceneCamera.GetUniversalAdditionalCameraData().renderPostProcessing = budget.PostProcessing;
            sceneCamera.allowHDR = budget.PostProcessing;
            if (profile.TryGet<ChromaticAberration>(out var aberration)) aberration.active = budget.ChromaticAberration;
        }

        void OnDestroy()
        {
            if (profile == null) return;
            foreach (var component in profile.components) if (component != null) Destroy(component);
            Destroy(profile);
        }
    }
}
