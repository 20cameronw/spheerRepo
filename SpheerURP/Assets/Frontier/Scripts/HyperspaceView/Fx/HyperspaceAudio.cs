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
            shot = SfxSynth.Render("warp-shot", 0.1f, 4000, new SfxSynth.Tone(1000, 220, 0, 0.1f, 0.15f, 1));
            hit = SfxSynth.Render("warp-hit", 0.15f, 2500, new SfxSynth.Tone(160, 50, 0, 0.15f, 0.35f, 4));
            explosion = SfxSynth.Render("warp-burst", 0.4f, 1800,
                new SfxSynth.Tone(90, 30, 0, 0.4f, 0.45f), new SfxSynth.Tone(300, 50, 0, 0.3f, 0.25f, 4));
            pickup = SfxSynth.Render("warp-pickup", 0.25f, 0,
                new SfxSynth.Tone(660, 660, 0, 0.12f, 0.25f), new SfxSynth.Tone(990, 990, 0.08f, 0.15f, 0.22f));
            warning = SfxSynth.Render("warp-warning", 0.4f, 2600, new SfxSynth.Tone(280, 750, 0, 0.4f, 0.2f, 1));
            chime = SfxSynth.Render("warp-clear", 0.6f, 0,
                new SfxSynth.Tone(523, 523, 0, 0.25f, 0.25f), new SfxSynth.Tone(784, 784, 0.12f, 0.3f, 0.23f),
                new SfxSynth.Tone(1047, 1047, 0.24f, 0.3f, 0.2f));
            // Integer cycles and a closed waveform keep the bed's loop seam silent.
            var samples = new float[SfxSynth.Rate];
            for (int i = 0; i < samples.Length; i++)
                samples[i] = 0.1f * Mathf.Sin(2 * Mathf.PI * 60 * i / samples.Length) +
                    0.04f * Mathf.Sin(2 * Mathf.PI * 121 * i / samples.Length);
            hum = AudioClip.Create("warp-engine", samples.Length, 1, SfxSynth.Rate, false); hum.SetData(samples, 0);
            for (int i = 0; i < lastCue.Length; i++) lastCue[i] = -100;
        }
        public void Engine(float speed) { Sfx.SetBed(hum, 0.25f, Mathf.Clamp(speed / 30, 0.7f, 1.6f)); }
        public void Stop() { Sfx.SetBed(null, 0); }
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
            UnityEngine.Object.Destroy(shot); UnityEngine.Object.Destroy(hit); UnityEngine.Object.Destroy(explosion);
            UnityEngine.Object.Destroy(pickup); UnityEngine.Object.Destroy(warning); UnityEngine.Object.Destroy(chime);
            UnityEngine.Object.Destroy(hum);
        }
    }
}
