using UnityEngine;

namespace Frontier
{
    /// <summary>
    /// A small voice pool for pre-rendered clips (no per-play allocation): used by games that play many short cues per second.
    /// The legacy <see cref="Play(float, float, float, float)"/> synth path is unchanged.
    /// </summary>
    public static partial class Sfx
    {
        /// <summary>Ten one-shots plus the engine bed and legacy UI source keep total voices at most twelve.</summary>
        public const int VoiceCount = 10;
        static AudioSource[] voices;
        static float[] voiceStart;
        static AudioSource bed;
        static int nextVoice;

        static void InitVoices(GameObject host)
        {
            voices = new AudioSource[VoiceCount];
            voiceStart = new float[VoiceCount];
            for (int i = 0; i < VoiceCount; i++)
            {
                var s = host.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 0f;
                voices[i] = s;
            }
            bed = host.AddComponent<AudioSource>();
            bed.playOnAwake = false;
            bed.loop = true;
            bed.spatialBlend = 0f;
        }

        /// <summary>Plays a clip on a free voice (the oldest voice is stolen when all are busy). Returns false when muted or not initialised.</summary>
        public static bool PlayVoice(AudioClip clip, float volume = 1f, float pitch = 1f, float pan = 0f)
        {
            if (voices == null || clip == null || Muted) return false;
            int pick = -1;
            for (int k = 0; k < VoiceCount; k++)
            {
                int i = (nextVoice + k) % VoiceCount;
                if (!voices[i].isPlaying) { pick = i; break; }
            }
            if (pick < 0)
            {
                float oldest = float.MaxValue;
                for (int i = 0; i < VoiceCount; i++) if (voiceStart[i] < oldest) { oldest = voiceStart[i]; pick = i; }
            }
            nextVoice = (pick + 1) % VoiceCount;
            var v = voices[pick];
            v.clip = clip;
            v.volume = volume;
            v.pitch = pitch;
            v.panStereo = pan;
            v.Play();
            voiceStart[pick] = Time.unscaledTime;
            return true;
        }

        /// <summary>Starts (or retunes) the looping bed (engine hum, warp rumble). Pass a null clip to stop it.</summary>
        public static void SetBed(AudioClip clip, float volume, float pitch = 1f)
        {
            if (bed == null) return;
            if (clip == null || Muted) { if (bed.isPlaying) bed.Stop(); return; }
            if (bed.clip != clip) { bed.clip = clip; bed.Play(); }
            else if (!bed.isPlaying) bed.Play();
            bed.volume = volume;
            bed.pitch = pitch;
        }

        public static int BusyVoices
        {
            get
            {
                if (voices == null) return 0;
                int n = 0;
                for (int i = 0; i < VoiceCount; i++) if (voices[i].isPlaying) n++;
                return n;
            }
        }
    }
}
