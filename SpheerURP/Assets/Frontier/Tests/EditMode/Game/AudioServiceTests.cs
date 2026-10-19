using System;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Frontier.Tests
{
    public sealed class AudioServiceTests
    {
        [Test]
        public void LicensedCueLibraryIsCompleteAudiblePreloadedAndWithinDecodedMemoryBudget()
        {
            var library = Resources.Load<AudioCueLibrary>("Frontier/AudioCueLibrary");
            Assert.IsNotNull(library);
            library.Validate();
            long bytes = 0;
            var measured = new System.Collections.Generic.HashSet<AudioClip>();
            foreach (var entry in library.entries)
            {
                Assert.That(entry.clip.length, Is.InRange(0.01f, 3f), entry.cue.ToString());
                Assert.AreEqual(1, entry.clip.channels);
                Assert.AreEqual(AudioClipLoadType.DecompressOnLoad, entry.clip.loadType);
                Assert.AreEqual(AudioDataLoadState.Loaded, entry.clip.loadState);
                var importer = (UnityEditor.AudioImporter)UnityEditor.AssetImporter.GetAtPath(
                    UnityEditor.AssetDatabase.GetAssetPath(entry.clip));
                Assert.IsTrue(importer.defaultSampleSettings.preloadAudioData);
                var samples = new float[entry.clip.samples];
                Assert.IsTrue(entry.clip.GetData(samples, 0));
                double power = 0;
                foreach (float sample in samples)
                {
                    Assert.IsFalse(float.IsNaN(sample) || float.IsInfinity(sample));
                    Assert.That(Math.Abs(sample), Is.LessThanOrEqualTo(1.01f));
                    power += sample * sample;
                }
                Assert.Greater(Math.Sqrt(power / samples.Length), 0.001, entry.cue + " is silent.");
                if (measured.Add(entry.clip))
                    bytes += (long)entry.clip.samples * entry.clip.channels * sizeof(float);
            }
            Assert.LessOrEqual(bytes, 1024 * 1024, "Shared short cues must fit in one MiB of decoded sample data.");
            foreach (string pack in new[] { "KenneyInterface", "KenneySciFi", "KenneyImpact" })
            {
                var license = Resources.Load<TextAsset>("Frontier/Audio/" + pack + "/License");
                Assert.IsNotNull(license);
                StringAssert.Contains("License: (Creative Commons Zero, CC0)", license.text);
                StringAssert.Contains("commercial projects", license.text);
            }
        }

        [Test]
        public void NamedCuesReuseClipsAndRespectAuthoredVolumesMixerRoutesAndMute()
        {
            var host = new GameObject("Authored cue test");
            var service = host.AddComponent<AudioService>();
            var library = Resources.Load<AudioCueLibrary>("Frontier/AudioCueLibrary");
            try
            {
                service.Initialize();
                service.SetVolumes(0.5f, 0.25f);
                var sources = host.GetComponents<AudioSource>();
                for (int i = 0; i < library.entries.Length; i++)
                {
                    var entry = library.entries[i];
                    Assert.IsTrue(service.PlayCue(entry.cue));
                    int voice = i % AudioService.VoiceCount;
                    Assert.AreSame(entry.clip, sources[voice].clip);
                    Assert.AreEqual(entry.volume * 0.5f, sources[voice].volume, 0.0001f);
                    Assert.AreEqual(entry.bus == AudioBus.Ui ? "UI" : "SFX", sources[voice].outputAudioMixerGroup.name);
                }
                service.Muted = true;
                Assert.IsFalse(service.PlayCue(AudioCue.Click));
                Assert.AreEqual(AudioService.VoiceCount + 2, host.GetComponents<AudioSource>().Length);
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void CueLibraryRejectsMissingAndDuplicatedAssetsAndRetainsAuthoredMappingsOnImport()
        {
            var authored = Resources.Load<AudioCueLibrary>("Frontier/AudioCueLibrary");
            string before = UnityEditor.EditorJsonUtility.ToJson(authored);
            Editor.FrontierAudioAssets.ImportLicensedCues();
            Assert.AreEqual(before, UnityEditor.EditorJsonUtility.ToJson(authored));
            var copy = ScriptableObject.CreateInstance<AudioCueLibrary>();
            try
            {
                copy.entries = new AudioCueEntry[authored.entries.Length];
                Array.Copy(authored.entries, copy.entries, copy.entries.Length);
                copy.entries[0] = copy.entries[1];
                Assert.Throws<InvalidOperationException>(copy.Validate);
                copy.entries[0] = new AudioCueEntry { cue = AudioCue.Click, clip = null };
                Assert.Throws<InvalidOperationException>(copy.Validate);
            }
            finally { Object.DestroyImmediate(copy); }
        }

        [Test]
        public void MaterialHarvestImpactsUseTheAuthoredClipsWithoutChangingHapticStrength()
        {
            var host = new GameObject("Harvest authored cue test");
            bool muted = Sfx.Muted;
            int pulses = 0;
            void OnPulse(HarvestCue cue, float strength)
            {
                Assert.AreEqual(HarvestCue.Impact, cue);
                Assert.AreEqual(0.3f, strength);
                pulses++;
            }
            try
            {
                Sfx.Muted = false;
                Sfx.Init(host);
                HarvestFeedback.HapticRequested += OnPulse;
                var library = Resources.Load<AudioCueLibrary>("Frontier/AudioCueLibrary");
                var kinds = new[] { MinigameKind.Chop, MinigameKind.Mine, MinigameKind.Drill };
                var cues = new[] { AudioCue.WoodImpact, AudioCue.StoneImpact, AudioCue.Impact };
                var sources = host.GetComponents<AudioSource>();
                for (int i = 0; i < kinds.Length; i++)
                {
                    HarvestFeedback.Play(HarvestCue.Impact, kinds[i]);
                    var entry = library.Get(cues[i]);
                    Assert.AreSame(entry.clip, sources[i].clip);
                    Assert.AreEqual(entry.volume * Sfx.EffectsVolume, sources[i].volume, 0.0001f);
                }
                Assert.AreEqual(3, pulses);
            }
            finally
            {
                HarvestFeedback.HapticRequested -= OnPulse;
                Object.DestroyImmediate(host);
                Sfx.Muted = muted;
            }
        }

        [Test]
        public void ShipPresentationCallsReuseLicensedCuesThroughTheExistingVoicePool()
        {
            var host = new GameObject("Licensed ship cue test");
            bool muted = Sfx.Muted;
            try
            {
                Sfx.Muted = false;
                Sfx.Init(host);
                var library = Resources.Load<AudioCueLibrary>("Frontier/AudioCueLibrary");
                Sfx.ShipIgnition();
                Sfx.ShipTouchdown();
                var sources = host.GetComponents<AudioSource>();
                Assert.AreSame(library.Get(AudioCue.ShipIgnition).clip, sources[0].clip);
                Assert.AreSame(library.Get(AudioCue.ShipTouchdown).clip, sources[1].clip);
                Assert.AreEqual(library.Get(AudioCue.ShipIgnition).volume * Sfx.EffectsVolume, sources[0].volume, 0.0001);
                Assert.AreEqual(library.Get(AudioCue.ShipTouchdown).volume * Sfx.EffectsVolume, sources[1].volume, 0.0001);
                Assert.AreEqual(AudioService.VoiceCount + 2, sources.Length);
            }
            finally { Object.DestroyImmediate(host); Sfx.Muted = muted; }
        }

        [Test]
        public void IndependentVolumesUpdateExistingAndFutureVoicesAndLoopsWithoutCompounding()
        {
            var host = new GameObject("Independent volume test");
            var clip = AudioClip.Create("Volume cue", 44100, 1, 44100, false);
            var service = host.AddComponent<AudioService>();
            try
            {
                service.Initialize();
                service.Play(clip, 0.8f, bus: AudioBus.Ui);
                service.Play(clip, 0.6f);
                service.Play(clip, 0.4f, bus: AudioBus.Music);
                service.SetBed(clip, 0.5f);
                service.SetMusic(clip, 0.7f);
                service.SetVolumes(0.25f, 0.5f);
                var sources = host.GetComponents<AudioSource>();
                Assert.AreEqual(0.2f, sources[0].volume, 0.0001f);
                Assert.AreEqual(0.15f, sources[1].volume, 0.0001f);
                Assert.AreEqual(0.2f, sources[2].volume, 0.0001f);
                Assert.AreEqual(0.125f, sources[AudioService.VoiceCount].volume, 0.0001f);
                Assert.AreEqual(0.35f, sources[AudioService.VoiceCount + 1].volume, 0.0001f);
                service.SetVolumes(0f, 0f);
                foreach (var source in sources) Assert.AreEqual(0f, source.volume);
                service.SetVolumes(1f, 1f);
                Assert.AreEqual(0.8f, sources[0].volume, 0.0001f);
                Assert.AreEqual(0.7f, sources[AudioService.VoiceCount + 1].volume, 0.0001f);
                service.SetVolumes(0.5f, 0.25f);
                service.Play(clip, 0.8f);
                service.SetBed(clip, 0.4f);
                service.SetMusic(clip, 0.6f);
                Assert.AreEqual(0.4f, sources[3].volume, 0.0001f);
                Assert.AreEqual(0.2f, sources[AudioService.VoiceCount].volume, 0.0001f);
                Assert.AreEqual(0.15f, sources[AudioService.VoiceCount + 1].volume, 0.0001f);
                service.Muted = true;
                service.SetVolumes(1f, 1f);
                Assert.IsFalse(service.Play(clip));
                foreach (var source in sources) Assert.IsFalse(source.isPlaying);
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(clip);
            }
        }

        [Test]
        public void VolumePreferencesClampPersistAndApplyWhenAudioHostIsRecreated()
        {
            bool hadEffects = PlayerPrefs.HasKey(Sfx.EffectsVolumeKey), hadMusic = PlayerPrefs.HasKey(Sfx.MusicVolumeKey);
            float effects = PlayerPrefs.GetFloat(Sfx.EffectsVolumeKey), music = PlayerPrefs.GetFloat(Sfx.MusicVolumeKey);
            bool muted = Sfx.Muted;
            var host = new GameObject("Saved volume test");
            var clip = AudioClip.Create("Saved volume cue", 44100, 1, 44100, false);
            try
            {
                PlayerPrefs.DeleteKey(Sfx.EffectsVolumeKey);
                PlayerPrefs.DeleteKey(Sfx.MusicVolumeKey);
                Assert.AreEqual(1f, Sfx.EffectsVolume);
                Assert.AreEqual(1f, Sfx.MusicVolume);
                Sfx.EffectsVolume = -1;
                Sfx.MusicVolume = 2;
                Assert.AreEqual(0f, Sfx.EffectsVolume);
                Assert.AreEqual(1f, Sfx.MusicVolume);
                Assert.Throws<System.ArgumentOutOfRangeException>(() => Sfx.EffectsVolume = float.NaN);
                Assert.Throws<System.ArgumentOutOfRangeException>(() => Sfx.MusicVolume = float.PositiveInfinity);
                Sfx.EffectsVolume = 0.25f;
                Sfx.MusicVolume = 0.5f;
                Sfx.Muted = false;
                Sfx.Init(host);
                var service = host.GetComponent<AudioService>();
                service.Play(clip, 0.8f);
                service.SetMusic(clip, 0.6f);
                Assert.AreEqual(0.2f, host.GetComponents<AudioSource>()[0].volume, 0.0001f);
                Assert.AreEqual(0.3f, host.GetComponents<AudioSource>()[AudioService.VoiceCount + 1].volume, 0.0001f);
                Sfx.EffectsVolume = 0.5f;
                Assert.AreEqual(0.4f, host.GetComponents<AudioSource>()[0].volume, 0.0001f);
                Assert.AreEqual(0.5f, PlayerPrefs.GetFloat(Sfx.EffectsVolumeKey));
                Assert.AreEqual(0.5f, PlayerPrefs.GetFloat(Sfx.MusicVolumeKey));
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(clip);
                if (hadEffects) PlayerPrefs.SetFloat(Sfx.EffectsVolumeKey, effects); else PlayerPrefs.DeleteKey(Sfx.EffectsVolumeKey);
                if (hadMusic) PlayerPrefs.SetFloat(Sfx.MusicVolumeKey, music); else PlayerPrefs.DeleteKey(Sfx.MusicVolumeKey);
                PlayerPrefs.Save();
                Sfx.Muted = muted;
            }
        }

        [Test]
        public void InitializationIsIdempotentAndAllSourcesUseMixerGroups()
        {
            var host = new GameObject("Mixer routing test");
            bool muted = Sfx.Muted;
            try
            {
                Sfx.Init(host);
                Sfx.Init(host);
                var sources = host.GetComponents<AudioSource>();
                Assert.AreEqual(AudioService.VoiceCount + 2, sources.Length);
                Assert.AreEqual(1, host.GetComponents<AudioService>().Length);
                int effects = 0, music = 0;
                foreach (var source in sources)
                {
                    Assert.IsNotNull(source.outputAudioMixerGroup);
                    Assert.IsFalse(source.playOnAwake);
                    if (source.outputAudioMixerGroup.name == "SFX") effects++;
                    if (source.outputAudioMixerGroup.name == "Music") music++;
                }
                Assert.AreEqual(AudioService.VoiceCount + 1, effects);
                Assert.AreEqual(1, music);
            }
            finally
            {
                Object.DestroyImmediate(host);
                Sfx.Muted = muted;
            }
        }

        [Test]
        public void PlaybackRoutesUiEffectsAndMusicAndMuteStopsEverySource()
        {
            var host = new GameObject("Mixer playback test");
            var clip = AudioClip.Create("Routing cue", 44100, 1, 44100, false);
            bool muted = Sfx.Muted;
            try
            {
                Sfx.Muted = false;
                Sfx.Init(host);
                var service = host.GetComponent<AudioService>();
                Assert.IsTrue(service.Play(clip, bus: AudioBus.Ui));
                Assert.IsTrue(service.Play(clip));
                service.SetMusic(clip);
                service.SetBed(clip, 0.2f);
                int ui = 0, music = 0;
                foreach (var source in host.GetComponents<AudioSource>())
                {
                    if (source.clip == clip && source.outputAudioMixerGroup.name == "UI") ui++;
                    if (source.clip == clip && source.outputAudioMixerGroup.name == "Music") music++;
                }
                Assert.AreEqual(1, ui);
                Assert.AreEqual(1, music);
                Sfx.Muted = true;
                foreach (var source in host.GetComponents<AudioSource>()) Assert.IsFalse(source.isPlaying);
                Assert.IsFalse(service.Play(clip));
                Sfx.Muted = false;
                Assert.IsTrue(service.Play(clip));
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(clip);
                Sfx.Muted = muted;
            }
        }
    }
}
