using System;
using UnityEngine;

namespace Frontier
{
    /// <summary>
    /// Offline procedural sound design: renders short mono clips from a few building blocks (oscillators, noise, sweeps, envelopes).
    /// Called while a game warms up, never per play. Deterministic: noise comes from a fixed-seed generator.
    /// </summary>
    public static class SfxSynth
    {
        public const int Rate = 44100;

        /// <summary>One voice of a cue: a waveform sweeping between two frequencies with an exponential-ish envelope.</summary>
        public struct Tone
        {
            public float f0, f1;
            public float start, length;
            public float volume;
            /// <summary>0 sine, 1 triangle, 2 square, 3 saw, 4 noise.</summary>
            public int wave;
            /// <summary>Envelope attack in seconds and decay power (higher = snappier).</summary>
            public float attack, decay;
            /// <summary>Vibrato / FM depth in Hz and rate in Hz.</summary>
            public float vibDepth, vibRate;

            public Tone(float f0, float f1, float start, float length, float volume, int wave = 0, float attack = 0.004f, float decay = 2f)
            {
                this.f0 = f0; this.f1 = f1; this.start = start; this.length = length; this.volume = volume; this.wave = wave;
                this.attack = attack; this.decay = decay; vibDepth = 0f; vibRate = 0f;
            }

            public Tone Vib(float depth, float rate) { vibDepth = depth; vibRate = rate; return this; }
        }

        /// <summary>Renders the tones into one clip. A one-pole low-pass (cutoff in Hz, 0 = off) tames noise-based tones.</summary>
        public static AudioClip Render(string name, float duration, float lowPassHz, params Tone[] tones)
        {
            int n = Mathf.Max(1, (int)(duration * Rate));
            var data = new float[n];
            uint seed = 0x9E3779B9u ^ (uint)name.Length * 2654435761u;
            for (int t = 0; t < tones.Length; t++)
            {
                var tone = tones[t];
                int i0 = Mathf.Max(0, (int)(tone.start * Rate));
                int len = Mathf.Max(1, (int)(tone.length * Rate));
                double phase = 0;
                float lp = 0f;
                float k = lowPassHz > 0f ? 1f - Mathf.Exp(-2f * Mathf.PI * lowPassHz / Rate) : 1f;
                for (int i = 0; i < len && i0 + i < n; i++)
                {
                    float u = i / (float)len;
                    float f = Mathf.Lerp(tone.f0, tone.f1, u);
                    if (tone.vibDepth > 0f) f += tone.vibDepth * Mathf.Sin(2f * Mathf.PI * tone.vibRate * i / Rate);
                    phase += f / Rate;
                    float ph = (float)(phase - Math.Floor(phase));
                    float s;
                    switch (tone.wave)
                    {
                        case 1: s = 4f * Mathf.Abs(ph - 0.5f) - 1f; break;
                        case 2: s = ph < 0.5f ? 1f : -1f; break;
                        case 3: s = 2f * ph - 1f; break;
                        case 4:
                            seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5;
                            s = (seed & 0xFFFF) / 32768f - 1f;
                            break;
                        default: s = Mathf.Sin(2f * Mathf.PI * ph); break;
                    }
                    float env = Mathf.Min(1f, i / Mathf.Max(1f, tone.attack * Rate)) * Mathf.Pow(1f - u, tone.decay);
                    float v = s * env * tone.volume;
                    if (lowPassHz > 0f) { lp += k * (v - lp); v = lp; }
                    data[i0 + i] += v;
                }
            }
            float peak = 0f;
            for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            if (peak > 0.98f) for (int i = 0; i < n; i++) data[i] /= peak * 1.02f;
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
