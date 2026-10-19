using System;
using UnityEngine;

namespace Frontier.Hyperspace.View
{
    /// <summary>Warm up once per run; event cues share the app's bounded voice pool.</summary>
    public sealed class HyperspaceAudio : IDisposable
    {
        readonly AudioClip shot, hit, explosion, pickup, warning, chime, hum;
        readonly float[] lastCue = new float[32];

        public HyperspaceAudio()
        {
            var library = Resources.Load<AudioCueLibrary>("Frontier/AudioCueLibrary");
            if (library == null) throw new InvalidOperationException("Frontier audio cue library is missing.");
            library.Validate();
            shot = library.Get(AudioCue.Zap).clip;
            hit = library.Get(AudioCue.Impact).clip;
            explosion = library.Get(AudioCue.Boom).clip;
            pickup = library.Get(AudioCue.Warp).clip;
            warning = library.Get(AudioCue.Bad).clip;
            chime = library.Get(AudioCue.Good).clip;
            var soundscape = Resources.Load<SoundscapeLibrary>("Frontier/SoundscapeLibrary");
            if (soundscape == null) throw new InvalidOperationException("Frontier soundscape library is missing.");
            soundscape.Validate();
            hum = soundscape.engine;
            for (int i = 0; i < lastCue.Length; i++) lastCue[i] = -100;
        }
        public void Engine(float speed) { Sfx.SetBed(hum, 0.25f, Mathf.Clamp(speed / 30, 0.7f, 1.6f)); }
        public void Stop() { Sfx.ClearBedIfOwnedBy(hum); }
        public void React(HEvent e)
        {
            int key = (int)e.type;
            float now = Time.unscaledTime;
            if (now - lastCue[key] < (e.type == EventType.EnemyHit ? 0.04f : 0.07f)) return;
            lastCue[key] = now;
            AudioClip clip = null; float volume = 0.45f, pitch = 1;
            switch (e.type)
            {
                case EventType.ShotFired: clip = shot; volume = 0.2f; break;
                case EventType.EnemyHit: clip = hit; volume = 0.18f; break;
                case EventType.PlayerHit: case EventType.ShieldBreak: clip = hit; volume = 0.8f; break;
                case EventType.EnemyKilled: clip = explosion; pitch = 1.3f; break;
                case EventType.BombUsed: case EventType.MineBlast: case EventType.BossDefeated: clip = explosion; volume = 0.8f; break;
                case EventType.PickupCollected: clip = pickup; pitch = 0.9f + e.a * 0.08f; break;
                case EventType.BombCharged: case EventType.ShieldRecharged: case EventType.Revived: clip = pickup; break;
                case EventType.TurretTelegraph: case EventType.GateTelegraph: case EventType.BossWarning:
                case EventType.BossPhase: case EventType.PlayerDowned: clip = warning; break;
                case EventType.RunEnded: clip = chime; pitch = 0.8f + e.a * 0.12f; break;
            }
            if (clip != null) Sfx.PlayVoice(clip, volume, pitch, Mathf.Clamp(e.x / 5, -0.6f, 0.6f));
        }
        public void Dispose()
        {
            Stop();
        }
    }
}
