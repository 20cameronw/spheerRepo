using System;
using System.IO;
using System.Xml;
using Frontier.EditorTools;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public class HapticPulseTests
    {
        sealed class Device : IHapticDevice
        {
            public bool IsSupported { get; set; } = true;
            public int pulses, stops, disposals;
            public float lastStrength;
            public void Pulse(float strength) { pulses++; lastStrength = strength; }
            public void Stop() { stops++; }
            public void Dispose() { disposals++; }
        }

        [Test]
        public void OptInAndSupport_AreRequired_IndependentOfAudioMute()
        {
            var device = new Device();
            using (var controller = new HapticPulseController(device))
            {
                Assert.IsFalse(controller.TryPulse(0.5f, 1));
                controller.Enabled = true;
                Assert.IsTrue(controller.TryPulse(0.5f, 1));
                Assert.AreEqual(0.5f, device.lastStrength);
                device.IsSupported = false;
                Assert.IsFalse(controller.TryPulse(1f, 2));
                Assert.AreEqual(1, device.pulses);
            }
        }

        [Test]
        public void RapidImpacts_AreThrottled_AndZeroStrengthConsumesNoBudget()
        {
            var device = new Device();
            using (var controller = new HapticPulseController(device) { Enabled = true })
            {
                Assert.IsFalse(controller.TryPulse(0f, 1));
                Assert.IsTrue(controller.TryPulse(0.3f, 1));
                for (int i = 1; i < 120; i++)
                    Assert.IsFalse(controller.TryPulse(0.8f, 1 + i / 1000.0));
                Assert.IsTrue(controller.TryPulse(0.8f, 1.121));
                Assert.AreEqual(2, device.pulses);
            }
        }

        [Test]
        public void DisableFocusLossAndDisposal_StopFeedbackWithoutDuplicateDisposal()
        {
            var device = new Device();
            var controller = new HapticPulseController(device) { Enabled = true };
            controller.Active = false;
            Assert.AreEqual(1, device.stops);
            Assert.IsFalse(controller.TryPulse(0.5f, 1));
            controller.Active = true;
            Assert.IsTrue(controller.TryPulse(0.5f, 1));
            controller.Enabled = false;
            Assert.AreEqual(2, device.stops);
            Assert.IsFalse(controller.TryPulse(0.5f, 2));
            controller.Dispose();
            controller.Dispose();
            Assert.AreEqual(3, device.stops);
            Assert.AreEqual(1, device.disposals);
            Assert.IsFalse(controller.TryPulse(0.5f, 3));
            Assert.IsFalse(controller.IsSupported);
            Assert.Throws<ObjectDisposedException>(() => controller.Enabled = true);
        }

        [Test]
        public void InvalidInputs_AreRejectedBeforeDeviceCalls()
        {
            var device = new Device();
            using (var controller = new HapticPulseController(device) { Enabled = true })
            {
                foreach (float value in new[] { -1f, 1.1f, float.NaN, float.PositiveInfinity })
                    Assert.Throws<ArgumentOutOfRangeException>(() => controller.TryPulse(value, 0));
                foreach (double value in new[] { -1.0, double.NaN, double.PositiveInfinity })
                    Assert.Throws<ArgumentOutOfRangeException>(() => controller.TryPulse(0.5f, value));
                Assert.AreEqual(0, device.pulses);
            }
        }

        [Test]
        public void HapticsPreference_IsOptIn_PersistsAndDoesNotChangeSoundPreference()
        {
            bool existed = PlayerPrefs.HasKey(HarvestHaptics.PreferenceKey);
            int saved = PlayerPrefs.GetInt(HarvestHaptics.PreferenceKey, 0);
            bool muted = Sfx.Muted;
            try
            {
                PlayerPrefs.DeleteKey(HarvestHaptics.PreferenceKey);
                Assert.IsFalse(HarvestHaptics.Enabled);
                Sfx.Muted = true;
                HarvestHaptics.Enabled = true;
                Assert.AreEqual(1, PlayerPrefs.GetInt(HarvestHaptics.PreferenceKey));
                Assert.IsTrue(Sfx.Muted);
                HarvestHaptics.Enabled = false;
                Assert.AreEqual(0, PlayerPrefs.GetInt(HarvestHaptics.PreferenceKey));
                Assert.IsTrue(Sfx.Muted);
            }
            finally
            {
                if (existed) PlayerPrefs.SetInt(HarvestHaptics.PreferenceKey, saved);
                else PlayerPrefs.DeleteKey(HarvestHaptics.PreferenceKey);
                PlayerPrefs.Save();
                Sfx.Muted = muted;
            }
        }

        [Test]
        public void AndroidPermission_IsAddedOnce_WithoutRemovingExistingManifestData()
        {
            string path = Path.Combine(Application.temporaryCachePath, "haptics-manifest-" + Guid.NewGuid().ToString("N") + ".xml");
            try
            {
                File.WriteAllText(path, "<manifest xmlns:android=\"http://schemas.android.com/apk/res/android\" package=\"test.app\">" +
                    "<uses-permission android:name=\"android.permission.INTERNET\"/><application android:label=\"test\"/></manifest>");
                FrontierHapticsBuild.EnsureVibrationPermission(path);
                FrontierHapticsBuild.EnsureVibrationPermission(path);
                var manifest = new XmlDocument();
                manifest.Load(path);
                var namespaces = new XmlNamespaceManager(manifest.NameTable);
                namespaces.AddNamespace("android", "http://schemas.android.com/apk/res/android");
                Assert.AreEqual(1, manifest.SelectNodes("/manifest/uses-permission[@android:name='android.permission.VIBRATE']", namespaces).Count);
                Assert.AreEqual(1, manifest.SelectNodes("/manifest/uses-permission[@android:name='android.permission.INTERNET']", namespaces).Count);
                Assert.AreEqual("test", manifest.SelectSingleNode("/manifest/application", namespaces).Attributes["android:label"].Value);
                Assert.AreEqual("test.app", manifest.DocumentElement.GetAttribute("package"));
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }
    }
}
