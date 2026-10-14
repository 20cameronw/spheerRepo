#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Uni = Frontier.Universe.Universe;

namespace Frontier.EditorTools
{
    /// <summary>
    /// Developer tools for the universe generator (Frontier.Core, generator version 1). The universe is not wired into gameplay
    /// yet; this only visualises it. Batch: -executeMethod Frontier.EditorTools.UniverseTools.RenderDensityPng -universePng out.png
    /// </summary>
    public static class UniverseTools
    {
        public const string PathArg = "-universePng";
        const int Pixels = 1024;
        const double SpanLy = 30000.0;

        [MenuItem("Frontier/Universe/Render Density PNG")]
        public static void RenderDensityPng()
        {
            string path = ReadArg(PathArg);
            if (string.IsNullOrEmpty(path))
            {
                string project = Directory.GetParent(Application.dataPath).FullName;
                path = Path.Combine(project, "Screens", "universe", "density-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".png");
            }
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));

            var universe = new Uni(new Frontier.Universe.UniverseConfig(), Frontier.Universe.Landmarks.CreateDefault());
            var density = new float[Pixels * Pixels];
            universe.FillDensityGrid(-SpanLy * 0.5, -SpanLy * 0.5, SpanLy / Pixels, Pixels, Pixels, density);

            var pixels = new Color32[density.Length];
            for (int i = 0; i < density.Length; i++)
            {
                float d = density[i];
                float v = d / (d + 0.24f);
                pixels[i] = new Color32(
                    (byte)(255f * Mathf.Clamp01(0.02f + 0.9f * v)),
                    (byte)(255f * Mathf.Clamp01(0.03f + 0.8f * v)),
                    (byte)(255f * Mathf.Clamp01(0.08f + 1.0f * v)), 255);
            }

            var texture = new Texture2D(Pixels, Pixels, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            Debug.Log("Frontier: universe density map (" + Pixels + " px, " + SpanLy + " ly across, home at the centre) written to " + path);
            if (!Application.isBatchMode) EditorUtility.RevealInFinder(path);
        }

        static string ReadArg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == name) return args[i + 1];
            return null;
        }
    }
}
#endif
