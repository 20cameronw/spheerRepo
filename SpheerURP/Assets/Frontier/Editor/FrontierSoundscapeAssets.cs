using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Frontier.Editor
{
    public static class FrontierSoundscapeAssets
    {
        const string Root = "Assets/Frontier/Resources/Frontier/";
        const string LoopRoot = Root + "Audio/Original/";
        const int Rate = 22050;

        [MenuItem("Frontier/Audio/Create Original Soundscapes If Missing")]
        public static void Create()
        {
            Directory.CreateDirectory(LoopRoot);
            var library = AssetDatabase.LoadAssetAtPath<SoundscapeLibrary>(Root + "SoundscapeLibrary.asset");
            if (library != null)
            {
                library.Validate();
                Debug.Log("Frontier: authored soundscape retained.");
                return;
            }
            library = ScriptableObject.CreateInstance<SoundscapeLibrary>();
            library.exploration = Music("exploration", 0);
            library.combat = Music("combat", 1);
            library.travel = Music("travel", 2);
            library.calm = Ambience("calm", 0);
            library.rain = Ambience("rain", 1);
            library.snow = Ambience("snow", 2);
            library.embers = Ambience("embers", 3);
            library.engine = Engine();
            library.Validate();
            AssetDatabase.CreateAsset(library, Root + "SoundscapeLibrary.asset");
            AssetDatabase.SaveAssets();
            Debug.Log("Frontier: created three original adaptive music arrangements and four weather ambience loops; runtime synthesis is not used.");
        }

        static AudioClip Music(string name, int mode)
        {
            var samples = new float[Rate * 16];
            // Original A-minor progression; aligned four-second bars allow consistent mode changes.
            int[] roots = { 45, 41, 48, 43 };
            int[] thirds = { 3, 4, 4, 4 };
            for (int bar = 0; bar < 4; bar++)
            {
                double start = bar * 4;
                Note(samples, roots[bar], start, 4, 0.11, 0.2, 0.35, 0);
                Note(samples, roots[bar] + 12, start, 4, 0.08, 0.3, 0.4, 0);
                Note(samples, roots[bar] + 12 + thirds[bar], start, 4, 0.055, 0.4, 0.4, 0);
                Note(samples, roots[bar] + 19, start, 4, 0.045, 0.5, 0.4, 0);
                int count = mode == 0 ? 4 : mode == 1 ? 16 : 8;
                int[] arpeggio = { 12, 19, 24, 12 + thirds[bar] };
                for (int step = 0; step < count; step++)
                {
                    double time = start + step * 4.0 / count;
                    Note(samples, roots[bar] + arpeggio[step % 4], time, mode == 0 ? 0.5 : 0.22,
                        mode == 0 ? 0.065 : 0.1, 0.01, 0.12, mode == 0 ? 0 : 1);
                    if (mode == 1 && step % 2 == 0)
                        Note(samples, roots[bar] - 12, time, 0.2, 0.16, 0.004, 0.16, 0);
                }
            }
            return Write(name, samples);
        }

        static void Note(float[] samples, int midi, double start, double duration,
            double gain, double attack, double release, int voice)
        {
            double frequency = 440 * Math.Pow(2, (midi - 69) / 12.0);
            int first = (int)(start * Rate), count = (int)(duration * Rate);
            for (int i = 0; i < count && first + i < samples.Length; i++)
            {
                double t = i / (double)Rate;
                double envelope = Math.Min(1, t / attack) * Math.Min(1, (duration - t) / release);
                double phase = 2 * Math.PI * frequency * t;
                double wave = Math.Sin(phase);
                if (voice == 1) wave = (wave + 0.25 * Math.Sin(phase * 2)) * Math.Exp(-t * 6);
                samples[first + i] += (float)(wave * envelope * gain);
            }
        }

        static AudioClip Ambience(string name, int mode)
        {
            var samples = new float[Rate * 8];
            uint random = 0x19285ABu + (uint)mode;
            double filtered = 0;
            double previous = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                random ^= random << 13; random ^= random >> 17; random ^= random << 5;
                double noise = (random & 0xFFFF) / 32768.0 - 1;
                double smoothing = mode == 1 ? 0.22 : mode == 3 ? 0.08 : 0.015;
                filtered += smoothing * (noise - filtered);
                double t = i / (double)Rate;
                double wind = 0.65 + 0.2 * Math.Sin(2 * Math.PI * t / 8)
                    + 0.15 * Math.Sin(2 * Math.PI * t * 3 / 8);
                double value = filtered * wind;
                if (mode == 1) value += (noise - previous) * 0.045;
                if (mode == 3 && noise > 0.985) value += noise * 0.3;
                double edge = Math.Min(1, Math.Min(t, 8 - t) / 0.04);
                samples[i] = (float)(value * edge * 0.7);
                previous = noise;
            }
            return Write(name, samples);
        }

        static AudioClip Write(string name, float[] samples, string directory = LoopRoot)
        {
            string path = directory + name + ".wav";
            if (!File.Exists(path))
            {
                using (var writer = new BinaryWriter(File.Create(path)))
                {
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                    writer.Write(36 + samples.Length * 2);
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                    writer.Write(16); writer.Write((short)1); writer.Write((short)1);
                    writer.Write(Rate); writer.Write(Rate * 2); writer.Write((short)2); writer.Write((short)16);
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(samples.Length * 2);
                    foreach (float sample in samples) writer.Write((short)Math.Round(Math.Max(-0.98, Math.Min(0.98, sample)) * short.MaxValue));
                }
            }
            AssetDatabase.ImportAsset(path);
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            importer.forceToMono = true;
            importer.loadInBackground = false;
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.preloadAudioData = true;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        static AudioClip Engine()
        {
            string path = Root + "Audio/KenneySciFi/spaceEngineSmall_000.ogg";
            AssetDatabase.ImportAsset(path);
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            if (importer == null) throw new InvalidOperationException("Licensed Kenney engine source is missing.");
            importer.forceToMono = true;
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.preloadAudioData = true;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
            var source = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            var original = new float[source.samples * source.channels];
            if (!source.GetData(original, 0)) throw new InvalidOperationException("Cannot read licensed engine recording.");
            var mono = new float[(int)(source.length * Rate)];
            for (int i = 0; i < mono.Length; i++)
            {
                double position = i * (double)source.frequency / Rate;
                int first = Math.Min((int)position, source.samples - 1);
                int next = Math.Min(first + 1, source.samples - 1);
                for (int channel = 0; channel < source.channels; channel++)
                    mono[i] += Mathf.Lerp(original[first * source.channels + channel],
                        original[next * source.channels + channel], (float)(position - first)) / source.channels;
            }
            int overlap = Rate / 10;
            if (mono.Length <= overlap * 2) throw new InvalidOperationException("Engine recording is too short to loop.");
            var loop = new float[mono.Length - overlap];
            Array.Copy(mono, overlap, loop, 0, loop.Length);
            for (int i = 0; i < overlap; i++)
                loop[loop.Length - overlap + i] = Mathf.Lerp(mono[mono.Length - overlap + i], mono[i], i / (float)(overlap - 1));
            // Keep the CC0 source and its looped derivative together with the original pack license.
            return Write("engine-loop", loop, Root + "Audio/KenneySciFi/");
        }

        public static AudioClip CreateIgnitionCue()
        {
            var engine = Engine();
            var data = new float[(int)(Rate * 0.55f)];
            if (!engine.GetData(data, 0)) throw new InvalidOperationException("Cannot read engine loop for ignition cue.");
            float peak = 0;
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)Rate;
                data[i] *= Mathf.Min(1, t / 0.025f) * Mathf.Min(1, (0.55f - t) / 0.15f);
                peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            }
            if (peak < 0.001f) throw new InvalidOperationException("Engine ignition cue is inaudible.");
            for (int i = 0; i < data.Length; i++) data[i] *= 0.8f / peak;
            return Write("engine-ignition", data, Root + "Audio/KenneySciFi/");
        }
    }
}
