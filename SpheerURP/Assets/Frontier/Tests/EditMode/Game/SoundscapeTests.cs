using System;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Frontier.Tests
{
    public sealed class SoundscapeTests
    {
        [Test]
        public void BakedLoopsAreAudibleClosedPreloadedAndUnderEightMiB()
        {
            var library = Resources.Load<SoundscapeLibrary>("Frontier/SoundscapeLibrary");
            Assert.IsNotNull(library);
            library.Validate();
            long bytes = 0;
            foreach (var clip in new[] { library.exploration, library.combat, library.travel,
                library.calm, library.rain, library.snow, library.embers, library.engine })
            {
                Assert.AreEqual(1, clip.channels, clip.name);
                Assert.AreEqual(AudioClipLoadType.DecompressOnLoad, clip.loadType);
                Assert.AreEqual(AudioDataLoadState.Loaded, clip.loadState);
                Assert.That(clip.length, Is.InRange(1f, 16.01f));
                var data = new float[clip.samples];
                Assert.IsTrue(clip.GetData(data, 0));
                double power = 0;
                foreach (float sample in data)
                {
                    Assert.IsFalse(float.IsNaN(sample) || float.IsInfinity(sample));
                    Assert.Less(Math.Abs(sample), 0.99f);
                    power += sample * sample;
                }
                Assert.Greater(Math.Sqrt(power / data.Length), 0.001, clip.name);
                Assert.Less(Math.Abs(data[0] - data[data.Length - 1]), 0.06, clip.name + " loop seam");
                bytes += (long)data.Length * sizeof(float);
            }
            Assert.LessOrEqual(bytes, 8 * 1024 * 1024);
            Assert.That(library.exploration.length, Is.EqualTo(16).Within(0.001));
            Assert.That(library.combat.length, Is.EqualTo(16).Within(0.001));
            Assert.That(library.travel.length, Is.EqualTo(16).Within(0.001));
            string before = UnityEditor.EditorJsonUtility.ToJson(library);
            Editor.FrontierSoundscapeAssets.Create();
            Assert.AreEqual(before, UnityEditor.EditorJsonUtility.ToJson(library), "Never overwrite designer mappings.");
            StringAssert.Contains("commercial projects",
                Resources.Load<TextAsset>("Frontier/Audio/KenneySciFi/License").text);
        }

        [Test]
        public void ScenesFadeThroughSilenceAndRespectIndependentVolumesMuteAndEngineOwnership()
        {
            var host = new GameObject("Soundscape test");
            var library = Resources.Load<SoundscapeLibrary>("Frontier/SoundscapeLibrary");
            var service = host.AddComponent<AudioService>();
            try
            {
                service.Initialize();
                var player = new SoundscapePlayer(service, library);
                var sources = host.GetComponents<AudioSource>();
                var bed = sources[AudioService.VoiceCount];
                var music = sources[AudioService.VoiceCount + 1];
                service.SetVolumes(0.5f, 0.25f);
                player.Tick(MusicScene.Exploration, WeatherKind.Rain, false, 1);
                Assert.AreSame(library.exploration, music.clip);
                Assert.AreSame(library.rain, bed.clip);
                Assert.AreEqual(library.musicVolume * 0.25f, music.volume, 0.0001);
                Assert.AreEqual(library.ambienceVolume * 0.5f, bed.volume, 0.0001);
                Assert.AreEqual("Music", music.outputAudioMixerGroup.name);
                Assert.AreEqual("SFX", bed.outputAudioMixerGroup.name);
                player.Tick(MusicScene.Combat, WeatherKind.Snow, false, library.transitionSeconds * 0.5f);
                Assert.AreSame(library.exploration, music.clip, "Fade the old arrangement before replacing it.");
                Assert.AreEqual(library.musicVolume * 0.125f, music.volume, 0.0001);
                player.Tick(MusicScene.Combat, WeatherKind.Snow, false, library.transitionSeconds);
                Assert.AreSame(library.combat, music.clip);
                Assert.AreEqual(0, music.volume);
                player.Tick(MusicScene.Combat, WeatherKind.Snow, false, library.transitionSeconds);
                Assert.AreEqual(library.musicVolume * 0.25f, music.volume, 0.0001);
                service.SetBed(library.engine, 0.25f, 1.3f);
                player.Tick(MusicScene.Travel, WeatherKind.Storm, false, 1);
                Assert.AreSame(library.engine, bed.clip, "The director must not clear a newly started engine.");
                Assert.AreEqual(1.3f, bed.pitch);
                player.Tick(MusicScene.Travel, WeatherKind.Storm, false, 1);
                Assert.AreSame(library.travel, music.clip);
                Assert.AreSame(library.engine, bed.clip);
                service.Muted = true;
                player.Tick(MusicScene.Travel, WeatherKind.Storm, false, 1);
                Assert.IsNull(music.clip);
                service.Muted = false;
                player.Tick(MusicScene.Travel, WeatherKind.Storm, false, 1);
                Assert.AreSame(library.travel, music.clip, "Music must recover after unmute without changing scene.");
                player.Tick(MusicScene.Exploration, WeatherKind.Embers, false, 1);
                Assert.AreSame(library.embers, bed.clip);
                service.ClearBedIfOwnedBy(library.engine);
                Assert.AreSame(library.embers, bed.clip, "Late engine disposal must not stop weather.");
                player.Stop();
                Assert.IsNull(music.clip);
                Assert.IsNull(bed.clip);
                player.Tick(MusicScene.Exploration, WeatherKind.Rain, true, 1);
                Assert.IsNull(music.clip);
                Assert.IsNull(bed.clip);
                player.Tick(MusicScene.Exploration, WeatherKind.Rain, false, 1);
                Assert.AreSame(library.exploration, music.clip);
                Assert.AreSame(library.rain, bed.clip);
                Assert.AreEqual(AudioService.VoiceCount + 2, sources.Length);
                Assert.Throws<ArgumentOutOfRangeException>(() => player.Tick(MusicScene.Combat, WeatherKind.Rain, false, float.NaN));
            }
            finally { Object.DestroyImmediate(host); }
        }

        [Test]
        public void MissingLoopsAndInvalidMixValuesRejectTheLibrary()
        {
            var library = Object.Instantiate(Resources.Load<SoundscapeLibrary>("Frontier/SoundscapeLibrary"));
            try
            {
                library.Validate();
                library.musicVolume = float.NaN;
                Assert.Throws<InvalidOperationException>(library.Validate);
                library.musicVolume = 0.2f;
                library.transitionSeconds = float.PositiveInfinity;
                Assert.Throws<InvalidOperationException>(library.Validate);
                library.transitionSeconds = 0.5f;
                library.engine = null;
                Assert.Throws<InvalidOperationException>(library.Validate);
            }
            finally { Object.DestroyImmediate(library); }
        }
    }
}
