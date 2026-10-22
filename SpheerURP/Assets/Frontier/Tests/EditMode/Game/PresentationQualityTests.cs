using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Frontier.Tests
{
    public class PresentationQualityTests
    {
        bool hadQuality, hadFps;
        int quality, fps, target, vsync;

        [SetUp]
        public void SetUp()
        {
            hadQuality = PlayerPrefs.HasKey(PresentationQuality.QualityKey);
            hadFps = PlayerPrefs.HasKey(PresentationQuality.FrameRateKey);
            quality = PlayerPrefs.GetInt(PresentationQuality.QualityKey);
            fps = PlayerPrefs.GetInt(PresentationQuality.FrameRateKey);
            target = Application.targetFrameRate;
            vsync = QualitySettings.vSyncCount;
            PlayerPrefs.DeleteKey(PresentationQuality.QualityKey);
            PlayerPrefs.DeleteKey(PresentationQuality.FrameRateKey);
            PresentationQuality.Initialize(60);
        }

        [TearDown]
        public void TearDown()
        {
            if (hadQuality) PlayerPrefs.SetInt(PresentationQuality.QualityKey, quality);
            else PlayerPrefs.DeleteKey(PresentationQuality.QualityKey);
            if (hadFps) PlayerPrefs.SetInt(PresentationQuality.FrameRateKey, fps);
            else PlayerPrefs.DeleteKey(PresentationQuality.FrameRateKey);
            PlayerPrefs.Save();
            PresentationQuality.Initialize(FrontierSettings.I.targetFrameRate);
            Application.targetFrameRate = target;
            QualitySettings.vSyncCount = vsync;
        }

        [Test]
        public void ManualChoices_Persist_AndUpdateFrameBudgetImmediately()
        {
            PresentationQuality.Preset = QualityPreset.Low;
            Assert.AreEqual(PerformanceTier.Low, PresentationQuality.Budget.Tier);
            Assert.AreEqual(30, Application.targetFrameRate);
            Assert.AreEqual(0, QualitySettings.vSyncCount);
            PresentationQuality.FrameRate = 60;
            Assert.AreEqual(60, Application.targetFrameRate);
            PresentationQuality.Initialize(60);
            Assert.AreEqual(QualityPreset.Low, PresentationQuality.Preset);
            Assert.AreEqual(60, PresentationQuality.FrameRate);
            PresentationQuality.FrameRate = 0;
            PresentationQuality.Preset = QualityPreset.High;
            Assert.AreEqual(60, Application.targetFrameRate);
            PresentationQuality.Initialize(30);
            Assert.AreEqual(30, Application.targetFrameRate, "Authored frame cap must still apply.");
        }

        [Test]
        public void PanelAtlasesFollowTierWithoutChangingAuthoredSettings()
        {
            var authored = Resources.Load<PanelSettings>("Frontier/FrontierPanelSettings");
            int authoredCap = authored.dynamicAtlasSettings.maxAtlasSize;
            var runtime = UnityEngine.Object.Instantiate(authored);
            try
            {
                foreach (var preset in new[] { QualityPreset.Low, QualityPreset.Medium, QualityPreset.High })
                {
                    PresentationQuality.Preset = preset;
                    PresentationQuality.ApplyPanelBudget(runtime);
                    var atlas = runtime.dynamicAtlasSettings;
                    Assert.AreEqual(PresentationQuality.Budget.UiAtlasMaxSize, atlas.maxAtlasSize);
                    Assert.LessOrEqual(atlas.minAtlasSize, atlas.maxAtlasSize);
                    Assert.LessOrEqual(atlas.maxSubTextureSize, atlas.maxAtlasSize);
                }
                Assert.AreEqual(authoredCap, authored.dynamicAtlasSettings.maxAtlasSize);
                Assert.Throws<ArgumentNullException>(() => PresentationQuality.ApplyPanelBudget(null));
            }
            finally { UnityEngine.Object.DestroyImmediate(runtime); }
        }

        [Test]
        public void InvalidSavedSettings_Warn_AndResetToAutomatic()
        {
            PlayerPrefs.SetInt(PresentationQuality.QualityKey, 20);
            PlayerPrefs.SetInt(PresentationQuality.FrameRateKey, -1);
            LogAssert.Expect(LogType.Warning, "Frontier: invalid saved graphics settings; restoring automatic presentation settings.");
            PresentationQuality.Initialize(60);
            Assert.AreEqual(QualityPreset.Automatic, PresentationQuality.Preset);
            Assert.AreEqual(0, PresentationQuality.FrameRate);
            Assert.IsFalse(PlayerPrefs.HasKey(PresentationQuality.QualityKey));
            Assert.IsFalse(PlayerPrefs.HasKey(PresentationQuality.FrameRateKey));
        }

        [Test]
        public void InvalidRuntimeChoices_AreRejected_WithoutChangingPreferences()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => PresentationQuality.Preset = (QualityPreset)5);
            Assert.Throws<ArgumentOutOfRangeException>(() => PresentationQuality.FrameRate = 45);
            Assert.Throws<ArgumentOutOfRangeException>(() => PresentationQuality.Initialize(0));
            Assert.AreEqual(QualityPreset.Automatic, PresentationQuality.Preset);
            Assert.AreEqual(0, PresentationQuality.FrameRate);
        }
    }
}
