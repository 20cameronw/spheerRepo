using System;
using UnityEngine;

namespace Frontier
{
    public enum HarvestCue { Impact, Perfect, Glance, Miss, Break, Complete, ScanNear, ScanFar, Recovering }

    /// <summary>Cached harvest cues played through the bounded voice pool; native haptics can subscribe independently of audio mute.</summary>
    public static class HarvestFeedback
    {
        static AudioClip wood, stone, drill, gas, scan, salvage;
        static AudioClip perfect, miss, stageBreak, complete, near, far, recovering;
        static float woodVolume, stoneVolume, drillVolume;
        public static event Action<HarvestCue, float> HapticRequested;

        public static void Warmup()
        {
            if (wood != null) return;
            var library = Resources.Load<AudioCueLibrary>("Frontier/AudioCueLibrary");
            if (library == null) throw new InvalidOperationException("Frontier audio cue library is missing.");
            library.Validate();
            var woodCue = library.Get(AudioCue.WoodImpact);
            var stoneCue = library.Get(AudioCue.StoneImpact);
            var drillCue = library.Get(AudioCue.Impact);
            wood = woodCue.clip; woodVolume = woodCue.volume;
            stone = stoneCue.clip; stoneVolume = stoneCue.volume;
            drill = drillCue.clip; drillVolume = drillCue.volume;
            gas = Impact("Gas capture", 135f, 900f, 0);
            scan = Impact("Scanner return", 560f, 1100f, 0);
            salvage = Impact("Magnet lock", 230f, 1200f, 1);
            perfect = SfxSynth.Render("Harvest perfect", 0.18f, 5000f,
                new SfxSynth.Tone(520f, 1420f, 0f, 0.18f, 0.24f),
                new SfxSynth.Tone(1040f, 1800f, 0.03f, 0.12f, 0.1f, 1));
            miss = SfxSynth.Render("Harvest miss", 0.16f, 1400f,
                new SfxSynth.Tone(180f, 70f, 0f, 0.16f, 0.2f, 1));
            stageBreak = SfxSynth.Render("Harvest break", 0.22f, 2400f,
                new SfxSynth.Tone(300f, 100f, 0f, 0.22f, 0.22f, 3),
                new SfxSynth.Tone(800f, 200f, 0f, 0.13f, 0.13f, 4));
            complete = SfxSynth.Render("Harvest complete", 0.32f, 5000f,
                new SfxSynth.Tone(520f, 780f, 0f, 0.14f, 0.2f),
                new SfxSynth.Tone(780f, 1040f, 0.09f, 0.14f, 0.2f),
                new SfxSynth.Tone(1040f, 1560f, 0.18f, 0.14f, 0.18f));
            near = Impact("Strong sonar", 560f, 480f, 0);
            far = Impact("Weak sonar", 310f, 230f, 0);
            recovering = Impact("Tool recovery", 260f, 220f, 0);
        }

        public static void Play(HarvestCue cue, MinigameKind kind)
        {
            if (cue < HarvestCue.Impact || cue > HarvestCue.Recovering)
                throw new ArgumentOutOfRangeException(nameof(cue));
            if (kind < MinigameKind.Chop || kind > MinigameKind.Salvage)
                throw new ArgumentOutOfRangeException(nameof(kind));
            Warmup();
            AudioClip impact = kind == MinigameKind.Chop ? wood : kind == MinigameKind.Mine ? stone
                : kind == MinigameKind.Drill ? drill : kind == MinigameKind.Siphon ? gas
                : kind == MinigameKind.Excavate ? scan : salvage;
            AudioClip clip = cue == HarvestCue.Perfect ? perfect : cue == HarvestCue.Miss ? miss
                : cue == HarvestCue.Break ? stageBreak : cue == HarvestCue.Complete ? complete
                : cue == HarvestCue.ScanNear ? near : cue == HarvestCue.ScanFar ? far
                : cue == HarvestCue.Recovering ? recovering : impact;
            float volume = cue == HarvestCue.Glance || cue == HarvestCue.Recovering ? 0.35f : 1f;
            if (clip == wood) volume *= woodVolume;
            else if (clip == stone) volume *= stoneVolume;
            else if (clip == drill) volume *= drillVolume;
            Sfx.PlayVoice(clip, volume);
            float strength = cue == HarvestCue.Complete ? 0.8f
                : cue == HarvestCue.Perfect || cue == HarvestCue.Break ? 0.65f
                : cue == HarvestCue.Impact ? 0.3f : 0f;
            if (strength > 0f) HapticRequested?.Invoke(cue, strength);
        }

        static AudioClip Impact(string name, float low, float high, int wave)
        {
            return SfxSynth.Render(name, 0.12f, 3600f,
                new SfxSynth.Tone(low, low * 0.6f, 0f, 0.12f, 0.22f, wave),
                new SfxSynth.Tone(high, low, 0f, 0.055f, 0.12f, 4));
        }
    }
}
