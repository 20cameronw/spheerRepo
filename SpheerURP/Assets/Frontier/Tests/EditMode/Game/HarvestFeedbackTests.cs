using System;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public class HarvestFeedbackTests
    {
        [Test]
        public void WarmedFeedback_ReusesClipsAcrossAllToolAndCueCombinations()
        {
            bool muted = Sfx.Muted;
            try
            {
                Sfx.Muted = true;
                HarvestFeedback.Warmup();
                int clips = Resources.FindObjectsOfTypeAll<AudioClip>().Length;
                foreach (MinigameKind kind in new[] { MinigameKind.Chop, MinigameKind.Mine, MinigameKind.Drill,
                    MinigameKind.Siphon, MinigameKind.Excavate, MinigameKind.Salvage })
                    foreach (HarvestCue cue in Enum.GetValues(typeof(HarvestCue)))
                        HarvestFeedback.Play(cue, kind);
                HarvestFeedback.Warmup();
                Assert.AreEqual(clips, Resources.FindObjectsOfTypeAll<AudioClip>().Length,
                    "Harvest play calls must not synthesize new clips.");
            }
            finally { Sfx.Muted = muted; }
        }

        [Test]
        public void HapticHook_IsAudioMuteIndependent_AndDoesNotFireForMissesOrCooldownSpam()
        {
            bool muted = Sfx.Muted;
            int pulses = 0;
            HarvestCue last = default;
            float strength = 0f;
            Action<HarvestCue, float> listener = (cue, value) => { pulses++; last = cue; strength = value; };
            HarvestFeedback.HapticRequested += listener;
            try
            {
                Sfx.Muted = true;
                foreach (var cue in new[] { HarvestCue.Miss, HarvestCue.Glance, HarvestCue.Recovering,
                    HarvestCue.ScanNear, HarvestCue.ScanFar })
                    HarvestFeedback.Play(cue, MinigameKind.Chop);
                Assert.AreEqual(0, pulses);
                HarvestFeedback.Play(HarvestCue.Perfect, MinigameKind.Chop);
                Assert.AreEqual(1, pulses);
                Assert.AreEqual(HarvestCue.Perfect, last);
                Assert.That(strength, Is.InRange(0f, 1f));
                HarvestFeedback.Play(HarvestCue.Complete, MinigameKind.Chop);
                Assert.AreEqual(2, pulses);
                Assert.AreEqual(HarvestCue.Complete, last);
            }
            finally
            {
                HarvestFeedback.HapticRequested -= listener;
                Sfx.Muted = muted;
            }
        }

        [Test]
        public void InvalidCue_IsRejectedBeforeDispatch()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => HarvestFeedback.Play((HarvestCue)(-1), MinigameKind.Chop));
            Assert.Throws<ArgumentOutOfRangeException>(() => HarvestFeedback.Play((HarvestCue)99, MinigameKind.Chop));
            Assert.Throws<ArgumentOutOfRangeException>(() => HarvestFeedback.Play(HarvestCue.Impact, (MinigameKind)99));
        }

        [Test]
        public void WarmedSynthPlayback_DoesNotCreateAdditionalClips()
        {
            bool muted = Sfx.Muted;
            var host = new GameObject("Synth reuse test");
            try
            {
                Sfx.Muted = false;
                Sfx.Init(host);
                Sfx.Click();
                Sfx.Zap();
                int clips = Resources.FindObjectsOfTypeAll<AudioClip>().Length;
                for (int i = 0; i < 128; i++) { Sfx.Click(); Sfx.Zap(); }
                Assert.AreEqual(clips, Resources.FindObjectsOfTypeAll<AudioClip>().Length);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
                Sfx.Muted = muted;
            }
        }

        [Test]
        public void WarmedSynthPlayback_ReusesClipsWithoutManagedAllocations()
        {
            bool muted = Sfx.Muted;
            var host = new GameObject("Synth cache test");
            try
            {
                Sfx.Muted = false;
                Sfx.Init(host);
                for (int i = 0; i < 32; i++) { Sfx.Click(); Sfx.Zap(); }
                int clips = Sfx.CachedSynthClipCount;
                long probe = GC.GetAllocatedBytesForCurrentThread();
                var sentinel = new byte[4096];
                bool allocationCounterSupported = GC.GetAllocatedBytesForCurrentThread() - probe >= sentinel.Length;

                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 128; i++) { Sfx.Click(); Sfx.Zap(); }
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

                Assert.AreEqual(clips, Sfx.CachedSynthClipCount);
                Assert.AreEqual(Sfx.VoiceCount + 2, host.GetComponents<AudioSource>().Length);
                if (!allocationCounterSupported)
                    Assert.Ignore("Clip reuse and voice bounds passed; allocation counter is not supported on this runtime");
                Assert.AreEqual(0, allocated);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
                Sfx.Muted = muted;
            }
        }

        [Test]
        public void RandomizedSynthPlayback_KeepsTheClipCacheBounded()
        {
            bool muted = Sfx.Muted;
            var host = new GameObject("Synth capacity test");
            try
            {
                Sfx.Muted = false;
                Sfx.Init(host);
                for (int i = 0; i < Sfx.SynthClipCapacity * 3; i++)
                    Sfx.Play(200f + i, 0.01f, 0.1f);
                Assert.AreEqual(Sfx.SynthClipCapacity, Sfx.CachedSynthClipCount);
                Assert.LessOrEqual(Sfx.BusyVoices, Sfx.VoiceCount);
                Assert.AreEqual(Sfx.VoiceCount + 2, host.GetComponents<AudioSource>().Length);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
                Sfx.Muted = muted;
            }
        }

        [Test]
        public void LivePlayback_UsesBoundedVoices_AndDestroyedHostIsUninitialised()
        {
            bool muted = Sfx.Muted;
            var host = new GameObject("Harvest audio test");
            AudioClip clip = null;
            try
            {
                Sfx.Muted = false;
                Sfx.Init(host);
                clip = SfxSynth.Render("Voice test", 0.1f, 1000f,
                    new SfxSynth.Tone(200f, 100f, 0f, 0.1f, 0.1f));
                for (int i = 0; i < 64; i++)
                {
                    HarvestFeedback.Play(HarvestCue.Impact, MinigameKind.Mine);
                    Assert.IsTrue(Sfx.PlayVoice(clip));
                }
                Assert.AreEqual(Sfx.VoiceCount + 2, host.GetComponents<AudioSource>().Length);
                Assert.LessOrEqual(Sfx.BusyVoices, Sfx.VoiceCount);
                UnityEngine.Object.DestroyImmediate(host);
                Assert.IsFalse(Sfx.PlayVoice(clip));
                Assert.AreEqual(0, Sfx.BusyVoices);
            }
            finally
            {
                if (host != null) UnityEngine.Object.DestroyImmediate(host);
                if (clip != null) UnityEngine.Object.DestroyImmediate(clip);
                Sfx.Muted = muted;
            }
        }
    }
}
