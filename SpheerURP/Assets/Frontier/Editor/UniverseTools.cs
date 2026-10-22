#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Uni = Frontier.Universe.Universe;

namespace Frontier.EditorTools
{
    /// <summary>
    /// Developer tools for the universe generator. Batch: -executeMethod Frontier.EditorTools.UniverseTools.RenderDensityPng -universePng out.png
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
            var texture = CreateDensityTexture(universe, 0, 0, SpanLy, Pixels);
            try { File.WriteAllBytes(path, texture.EncodeToPNG()); }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
            Debug.Log("Frontier: universe density map (" + Pixels + " px, " + SpanLy + " ly across, home at the centre) written to " + path);
            if (!Application.isBatchMode) EditorUtility.RevealInFinder(path);
        }

        public static Texture2D CreateDensityTexture(Uni universe, double centerX, double centerY, double span, int pixels)
        {
            if (universe == null) throw new ArgumentNullException(nameof(universe));
            if (double.IsNaN(centerX) || double.IsInfinity(centerX) || double.IsNaN(centerY) || double.IsInfinity(centerY) ||
                double.IsNaN(span) || double.IsInfinity(span) || span <= 0)
                throw new ArgumentOutOfRangeException(nameof(span), "Preview coordinates and span must be finite, with a positive span.");
            if (pixels < 16 || pixels > 1024) throw new ArgumentOutOfRangeException(nameof(pixels));
            var density = new float[pixels * pixels];
            universe.FillDensityGrid(centerX - span * 0.5, centerY - span * 0.5, span / pixels, pixels, pixels, density);

            var colors = new Color32[density.Length];
            for (int i = 0; i < density.Length; i++)
            {
                float d = density[i];
                float v = d / (d + 0.24f);
                colors[i] = new Color32(
                    (byte)(255f * Mathf.Clamp01(0.02f + 0.9f * v)),
                    (byte)(255f * Mathf.Clamp01(0.03f + 0.8f * v)),
                    (byte)(255f * Mathf.Clamp01(0.08f + 1.0f * v)), 255);
            }

            var texture = new Texture2D(pixels, pixels, TextureFormat.RGBA32, false)
                { name = "Universe density preview", hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear };
            texture.SetPixels32(colors);
            texture.Apply(false);
            return texture;
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
