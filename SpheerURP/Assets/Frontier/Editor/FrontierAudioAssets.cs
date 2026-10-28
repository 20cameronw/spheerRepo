using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace Frontier.Editor
{
    public static class FrontierAudioAssets
    {
        const string Path = "Assets/Frontier/Resources/Frontier/FrontierAudio.mixer";
        const string Root = "Assets/Frontier/Resources/Frontier/";

        [MenuItem("Frontier/Audio/Import Licensed Cue Library")]
        public static void ImportLicensedCues()
        {
            string libraryPath = Root + "AudioCueLibrary.asset";
            var library = AssetDatabase.LoadAssetAtPath<AudioCueLibrary>(libraryPath);
            if (library != null && library.entries != null
                && library.entries.Length == Enum.GetValues(typeof(AudioCue)).Length)
            {
                library.Validate();
                Debug.Log("Frontier: authored cue library retained; no mappings or import settings changed.");
                return;
            }
            AudioCueEntry Cue(AudioCue cue, string pack, string file, AudioBus bus, float volume)
            {
                string path = Root + "Audio/" + pack + "/" + file + ".ogg";
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                if (importer == null) throw new InvalidOperationException("Missing licensed audio asset: " + path);
                importer.forceToMono = true;
                importer.loadInBackground = false;
                var sample = importer.defaultSampleSettings;
                sample.preloadAudioData = true;
                sample.loadType = AudioClipLoadType.DecompressOnLoad;
                sample.compressionFormat = AudioCompressionFormat.PCM;
                sample.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                importer.defaultSampleSettings = sample;
                importer.SaveAndReimport();
                return new AudioCueEntry { cue = cue, clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path), bus = bus, volume = volume };
            }
            bool created = library == null;
            if (created) library = ScriptableObject.CreateInstance<AudioCueLibrary>();
            var entries = created ? new List<AudioCueEntry>(new[]
            {
                Cue(AudioCue.Click, "KenneyInterface", "click_001", AudioBus.Ui, 0.2f),
                Cue(AudioCue.Good, "KenneyInterface", "confirmation_001", AudioBus.Ui, 0.25f),
                Cue(AudioCue.Bad, "KenneyInterface", "error_001", AudioBus.Ui, 0.25f),
                Cue(AudioCue.Zap, "KenneySciFi", "laserSmall_000", AudioBus.Sfx, 0.15f),
                Cue(AudioCue.Boom, "KenneySciFi", "explosionCrunch_000", AudioBus.Sfx, 0.35f),
                Cue(AudioCue.Warp, "KenneySciFi", "forceField_000", AudioBus.Sfx, 0.3f),
                Cue(AudioCue.Impact, "KenneySciFi", "impactMetal_000", AudioBus.Sfx, 0.18f)
            }) : new List<AudioCueEntry>(library.entries ?? Array.Empty<AudioCueEntry>());
            if (!entries.Exists(entry => entry != null && entry.cue == AudioCue.WoodImpact))
                entries.Add(Cue(AudioCue.WoodImpact, "KenneyImpact", "impactWood_heavy_000", AudioBus.Sfx, 0.35f));
            if (!entries.Exists(entry => entry != null && entry.cue == AudioCue.StoneImpact))
                entries.Add(Cue(AudioCue.StoneImpact, "KenneyImpact", "impactMining_000", AudioBus.Sfx, 0.35f));
            if (!entries.Exists(entry => entry != null && entry.cue == AudioCue.ShipIgnition))
                entries.Add(new AudioCueEntry { cue = AudioCue.ShipIgnition,
                    clip = FrontierSoundscapeAssets.CreateIgnitionCue(), bus = AudioBus.Sfx, volume = 0.22f });
            if (!entries.Exists(entry => entry != null && entry.cue == AudioCue.ShipTouchdown))
                entries.Add(Cue(AudioCue.ShipTouchdown, "KenneySciFi", "impactMetal_000", AudioBus.Sfx, 0.32f));
            library.entries = entries.ToArray();
            library.Validate();
            if (created) AssetDatabase.CreateAsset(library, libraryPath);
            else EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            Debug.Log("Frontier: imported CC0 UI/combat/harvest/ship cues with explicit mixer routes and preloaded mono PCM.");
        }

        [MenuItem("Frontier/Audio/Create Mixer If Missing")]
        public static void CreateMixer()
        {
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(Path);
            if (mixer == null)
            {
                // Unity exposes mixer editing only through its internal editor API.
                var type = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.Audio.AudioMixerController", true);
                mixer = (AudioMixer)Method(type, "CreateMixerControllerAtPath").Invoke(null, new object[] { Path });
                var master = type.GetProperty("masterGroup").GetValue(mixer);
                foreach (string name in new[] { "Music", "SFX", "UI" })
                {
                    var group = Method(type, "CreateNewGroup").Invoke(mixer, new object[] { name, false });
                    Method(type, "AddChildToParent").Invoke(mixer, new[] { group, master });
                }
                EditorUtility.SetDirty(mixer);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(Path, ImportAssetOptions.ForceUpdate);
            }
            foreach (string name in new[] { "Music", "SFX", "UI" })
                if (mixer.FindMatchingGroups(name).Length != 1)
                    throw new InvalidOperationException("Frontier mixer requires exactly one " + name + " group.");
            Debug.Log("Frontier audio mixer ready: Music, SFX, UI.");
        }

        static MethodInfo Method(Type type, string name)
        {
            return type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
                ?? throw new MissingMethodException(type.FullName, name);
        }
    }
}
