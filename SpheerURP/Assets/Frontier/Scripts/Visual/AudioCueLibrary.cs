using System;
using UnityEngine;

namespace Frontier
{
    public enum AudioCue { Click, Good, Bad, Zap, Boom, Warp, Impact, WoodImpact, StoneImpact, ShipIgnition, ShipTouchdown }

    [Serializable]
    public sealed class AudioCueEntry
    {
        public AudioCue cue;
        public AudioClip clip;
        public AudioBus bus;
        [Range(0f, 1f)] public float volume = 0.25f;
    }

    [CreateAssetMenu(menuName = "Frontier/Audio/Cue Library")]
    public sealed class AudioCueLibrary : ScriptableObject
    {
        public AudioCueEntry[] entries;

        public void Validate()
        {
            if (entries == null || entries.Length != Enum.GetValues(typeof(AudioCue)).Length)
                throw new InvalidOperationException("Frontier audio cue library must define every cue exactly once.");
            var seen = new bool[entries.Length];
            foreach (var entry in entries)
            {
                if (entry == null || !Enum.IsDefined(typeof(AudioCue), entry.cue) || seen[(int)entry.cue]
                    || entry.clip == null || !Enum.IsDefined(typeof(AudioBus), entry.bus)
                    || float.IsNaN(entry.volume) || float.IsInfinity(entry.volume) || entry.volume < 0 || entry.volume > 1)
                    throw new InvalidOperationException("Frontier audio cue library has an invalid, missing or duplicate cue.");
                seen[(int)entry.cue] = true;
            }
        }

        public AudioCueEntry Get(AudioCue cue)
        {
            foreach (var entry in entries) if (entry.cue == cue) return entry;
            throw new ArgumentOutOfRangeException(nameof(cue));
        }
    }
}
