using UnityEngine;

namespace Frontier
{
    public static partial class Sfx
    {
        public const int VoiceCount = AudioService.VoiceCount;
        static AudioService service;
        static bool muted;
        public const string EffectsVolumeKey = "fr_effects_volume";
        public const string MusicVolumeKey = "fr_music_volume";

        public static float EffectsVolume
        {
            get { return ReadVolume(EffectsVolumeKey); }
            set { SaveVolume(EffectsVolumeKey, value); }
        }

        public static float MusicVolume
        {
            get { return ReadVolume(MusicVolumeKey); }
            set { SaveVolume(MusicVolumeKey, value); }
        }

        static float ReadVolume(string key)
        {
            float value = PlayerPrefs.GetFloat(key, 1f);
            return float.IsNaN(value) || float.IsInfinity(value) ? 1f : Mathf.Clamp01(value);
        }

        static void SaveVolume(string key, float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) throw new System.ArgumentOutOfRangeException(nameof(value));
            PlayerPrefs.SetFloat(key, Mathf.Clamp01(value));
            PlayerPrefs.Save();
            if (service != null) service.SetVolumes(EffectsVolume, MusicVolume);
        }

        public static bool Muted
        {
            get { return muted; }
            set { muted = value; if (service != null) service.Muted = value; }
        }

        public static void Init(GameObject host)
        {
            service = host.GetComponent<AudioService>();
            if (service == null) service = host.AddComponent<AudioService>();
            service.Initialize();
            service.SetVolumes(EffectsVolume, MusicVolume);
            service.Muted = muted;
        }

        public static bool PlayVoice(AudioClip clip, float volume = 1f, float pitch = 1f, float pan = 0f)
        {
            return service != null && service.Play(clip, volume, pitch, pan);
        }

        public static void SetBed(AudioClip clip, float volume, float pitch = 1f)
        {
            if (service != null) service.SetBed(clip, volume, pitch);
        }

        public static void ClearBedIfOwnedBy(AudioClip clip)
        {
            if (service != null) service.ClearBedIfOwnedBy(clip);
        }

        public static int BusyVoices { get { return service != null ? service.BusyVoices : 0; } }
    }
}
