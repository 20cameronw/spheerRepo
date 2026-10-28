using System;
using UnityEngine;

namespace Frontier
{
    public enum MusicScene { Exploration, Combat, Travel }

    [CreateAssetMenu(menuName = "Frontier/Audio/Soundscape Library")]
    public sealed class SoundscapeLibrary : ScriptableObject
    {
        public AudioClip exploration, combat, travel;
        public AudioClip calm, rain, snow, embers;
        public AudioClip engine;
        [Range(0f, 1f)] public float musicVolume = 0.18f;
        [Range(0f, 1f)] public float ambienceVolume = 0.12f;
        [Min(0.05f)] public float transitionSeconds = 0.6f;

        public void Validate()
        {
            foreach (var clip in new[] { exploration, combat, travel, calm, rain, snow, embers, engine })
                if (clip == null || clip.length <= 0) throw new InvalidOperationException("Frontier soundscape requires all music and ambience loops.");
            if (!FiniteUnit(musicVolume) || !FiniteUnit(ambienceVolume)
                || float.IsNaN(transitionSeconds) || float.IsInfinity(transitionSeconds) || transitionSeconds < 0.05f)
                throw new InvalidOperationException("Frontier soundscape gains and transition duration are invalid.");
        }

        static bool FiniteUnit(float value) { return !float.IsNaN(value) && value >= 0 && value <= 1; }

        public AudioClip Music(MusicScene scene)
        {
            switch (scene)
            {
                case MusicScene.Exploration: return exploration;
                case MusicScene.Combat: return combat;
                case MusicScene.Travel: return travel;
                default: throw new ArgumentOutOfRangeException(nameof(scene));
            }
        }

        public AudioClip Ambience(WeatherKind weather)
        {
            switch (weather)
            {
                case WeatherKind.Rain: case WeatherKind.Storm: return rain;
                case WeatherKind.Snow: return snow;
                case WeatherKind.Embers: return embers;
                default: return calm;
            }
        }
    }
}
