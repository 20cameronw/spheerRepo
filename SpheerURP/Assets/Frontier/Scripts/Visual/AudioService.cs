using System;
using UnityEngine;
using UnityEngine.Audio;

namespace Frontier
{
    public enum AudioBus { Sfx, Ui, Music }

    public sealed class AudioService : MonoBehaviour
    {
        public const int VoiceCount = 10;
        readonly AudioSource[] voices = new AudioSource[VoiceCount];
        readonly float[] started = new float[VoiceCount];
        readonly float[] voiceVolumes = new float[VoiceCount];
        readonly AudioBus[] voiceBuses = new AudioBus[VoiceCount];
        AudioSource bed, music;
        AudioMixerGroup sfxGroup, uiGroup, musicGroup;
        int nextVoice;
        AudioCueLibrary cues;
        bool initialized, muted;
        float effectsVolume = 1f, musicVolume = 1f, bedVolume, musicLevel;

        public void SetVolumes(float effects, float backgroundMusic)
        {
            if (float.IsNaN(effects) || float.IsInfinity(effects)) throw new ArgumentOutOfRangeException(nameof(effects));
            if (float.IsNaN(backgroundMusic) || float.IsInfinity(backgroundMusic)) throw new ArgumentOutOfRangeException(nameof(backgroundMusic));
            effectsVolume = Mathf.Clamp01(effects);
            musicVolume = Mathf.Clamp01(backgroundMusic);
            if (!initialized) return;
            for (int i = 0; i < voices.Length; i++)
                voices[i].volume = voiceVolumes[i] * (voiceBuses[i] == AudioBus.Music ? musicVolume : effectsVolume);
            bed.volume = bedVolume * effectsVolume;
            music.volume = musicLevel * musicVolume;
        }

        public int BusyVoices
        {
            get
            {
                int count = 0;
                for (int i = 0; i < voices.Length; i++)
                    if (voices[i] != null && voices[i].isPlaying) count++;
                return count;
            }
        }

        public bool Muted
        {
            get { return muted; }
            set
            {
                muted = value;
                if (!muted || !initialized) return;
                for (int i = 0; i < voices.Length; i++) voices[i].Stop();
                bed.Stop();
                music.Stop();
            }
        }

        public void Initialize()
        {
            if (initialized) return;
            var mixer = Resources.Load<AudioMixer>("Frontier/FrontierAudio");
            if (mixer == null) throw new InvalidOperationException("Frontier audio mixer is missing.");
            sfxGroup = Group(mixer, "SFX");
            uiGroup = Group(mixer, "UI");
            musicGroup = Group(mixer, "Music");
            cues = Resources.Load<AudioCueLibrary>("Frontier/AudioCueLibrary");
            if (cues == null) throw new InvalidOperationException("Frontier audio cue library is missing.");
            cues.Validate();
            for (int i = 0; i < voices.Length; i++) voices[i] = Source(sfxGroup, false);
            bed = Source(sfxGroup, true);
            music = Source(musicGroup, true);
            initialized = true;
        }

        static AudioMixerGroup Group(AudioMixer mixer, string name)
        {
            var groups = mixer.FindMatchingGroups(name);
            if (groups.Length != 1) throw new InvalidOperationException("Frontier audio mixer requires one " + name + " group.");
            return groups[0];
        }

        AudioSource Source(AudioMixerGroup group, bool loop)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.loop = loop;
            source.outputAudioMixerGroup = group;
            return source;
        }

        public bool Play(AudioClip clip, float volume = 1f, float pitch = 1f, float pan = 0f, AudioBus bus = AudioBus.Sfx)
        {
            if (!initialized || muted || clip == null) return false;
            int pick = -1;
            for (int offset = 0; offset < voices.Length; offset++)
            {
                int index = (nextVoice + offset) % voices.Length;
                if (!voices[index].isPlaying) { pick = index; break; }
            }

            if (pick < 0)
            {
                pick = 0;
                for (int i = 1; i < voices.Length; i++) if (started[i] < started[pick]) pick = i;
            }
            nextVoice = (pick + 1) % voices.Length;
            var voice = voices[pick];
            voice.Stop();
            voice.outputAudioMixerGroup = bus == AudioBus.Ui ? uiGroup : bus == AudioBus.Music ? musicGroup : sfxGroup;
            voice.clip = clip;
            voiceVolumes[pick] = Mathf.Clamp01(volume);
            voiceBuses[pick] = bus;
            voice.volume = voiceVolumes[pick] * (bus == AudioBus.Music ? musicVolume : effectsVolume);
            voice.pitch = pitch;
            voice.panStereo = pan;
            voice.Play();
            started[pick] = Time.unscaledTime;
            return true;
        }

        public bool PlayCue(AudioCue cue, float pitch = 1f, float pan = 0f)
        {
            if (!initialized || muted) return false;
            var entry = cues.Get(cue);
            return Play(entry.clip, entry.volume, pitch, pan, entry.bus);
        }

        public void SetBed(AudioClip clip, float volume, float pitch = 1f)
        {
            bedVolume = Mathf.Clamp01(volume);
            if (initialized) SetLoop(bed, clip, bedVolume * effectsVolume, pitch);
        }

        public void SetMusic(AudioClip clip, float volume = 1f)
        {
            musicLevel = Mathf.Clamp01(volume);
            if (initialized) SetLoop(music, clip, musicLevel * musicVolume, 1f);
        }

        public void ClearBedIfOwnedBy(AudioClip clip)
        {
            if (initialized && bed != null && bed.clip == clip) SetBed(null, 0);
        }

        void SetLoop(AudioSource source, AudioClip clip, float volume, float pitch)
        {
            if (clip == null || muted) { source.Stop(); source.clip = null; return; }
            if (source.clip != clip) { source.Stop(); source.clip = clip; }
            source.volume = volume;
            source.pitch = pitch;
            if (!source.isPlaying) source.Play();
        }

        public bool IsPlaying(AudioClip clip)
        {
            if (!initialized || clip == null) return false;
            if ((bed.isPlaying && bed.clip == clip) || (music.isPlaying && music.clip == clip)) return true;
            for (int i = 0; i < voices.Length; i++)
                if (voices[i].isPlaying && voices[i].clip == clip) return true;
            return false;
        }
    }
}
