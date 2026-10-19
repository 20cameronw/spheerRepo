#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Frontier.Universe;
using UnityEditor;
using UnityEngine;

namespace Frontier.EditorTools
{
    public static class FrontierPlanetContactSheet
    {
        const int Columns = 20;
        const int Rows = 10;
        const int Width = 2000;
        const int Height = 1200;
        const float CellSize = 1.2f;
        const string OutputPath = "Assets/Frontier/Art/Validation/PlanetContactSheet.png";

        public static PlanetData[] BuildSample(UniverseService service)
        {
            if (service == null) throw new ArgumentNullException(nameof(service));
            var authored = new List<PlanetData>(service.AuthoredPlanets);
            var generated = new List<PlanetData>();
            var systems = service.Universe.SystemsInRect(-1024, -1024, 1024, 1024);
            foreach (StarSystem system in systems)
                foreach (PlanetDescriptor descriptor in service.Universe.PlanetsOf(system))
                    if (!descriptor.IsAuthored) generated.Add(service.Get(descriptor));
            generated.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));

            int generatedNeeded = Mathf.Max(0, Columns * Rows - authored.Count);
            if (generated.Count < generatedNeeded)
                throw new InvalidOperationException("The contact sheet needs " + generatedNeeded + " generated planets but found " + generated.Count + ".");

            var sample = new List<PlanetData>(Columns * Rows);
            sample.AddRange(authored);
            for (int i = 0; sample.Count < Columns * Rows; i++)
            {
                int index = (int)((long)i * generated.Count / generatedNeeded);
                sample.Add(generated[index]);
            }
            return sample.ToArray();
        }

        [MenuItem("Frontier/Planets/Render 200 Planet Contact Sheet")]
        public static void Render()
        {
            RenderSample(BuildSample(UniverseService.For(GameDatabase.Load())), OutputPath);
        }

        public static void RenderSample(PlanetData[] planets, string outputPath)
        {
            if (planets == null || planets.Length != Columns * Rows)
                throw new ArgumentException("The contact sheet requires exactly " + (Columns * Rows) + " planets.", nameof(planets));
            if (string.IsNullOrWhiteSpace(outputPath))
                throw new ArgumentException("An output asset path is required.", nameof(outputPath));

            var root = new GameObject("PlanetContactSheet");
            var renderTexture = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            var readback = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            var materials = new List<Material>();
            try
            {
                var cameraObject = new GameObject("ContactSheetCamera");
                cameraObject.transform.SetParent(root.transform, false);
                cameraObject.transform.position = new Vector3(0f, 0f, -30f);
                var camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true;
                camera.aspect = Width / (float)Height;
                camera.orthographicSize = Mathf.Max(Rows * CellSize * 0.56f,
                    Columns * CellSize / (2f * camera.aspect) * 1.08f);
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 100f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.008f, 0.016f, 0.035f, 1f);
                camera.targetTexture = renderTexture;

                var lightObject = new GameObject("ContactSheetLight");
                lightObject.transform.SetParent(root.transform, false);
                lightObject.transform.rotation = Quaternion.LookRotation(new Vector3(0.4f, -0.25f, 1f));
                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.25f;

                for (int i = 0; i < planets.Length; i++)
                {
                    PlanetData planet = planets[i];
                    float x = (i % Columns - (Columns - 1) * 0.5f) * CellSize;
                    float y = ((Rows - 1) * 0.5f - i / Columns) * CellSize;
                    Transform model = ModelLibrary.Spawn(planet.ModelKey, root.transform, 0.9f, false);
                    if (model == null)
                    {
                        var fallback = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                        UnityEngine.Object.DestroyImmediate(fallback.GetComponent<Collider>());
                        model = fallback.transform;
                        model.SetParent(root.transform, false);
                        model.localScale = Vector3.one * 0.9f;
                    }
                    model.localPosition = new Vector3(x, y, 0f);
                    model.localRotation = Quaternion.identity;

                    var shader = Fx.PlanetShader;
                    if (shader == null || !shader.isSupported)
                        throw new InvalidOperationException("Frontier/NeonPlanet shader is missing or unsupported.");
                    foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>())
                    {
                        var material = new Material(shader);
                        materials.Add(material);
                        material.SetColor("_BaseColor", planet.Primary);
                        material.SetColor("_AltColor", planet.Secondary);
                        material.SetColor("_RimColor", planet.Rim * planet.GlowIntensity);
                        material.SetFloat("_Bands", Mathf.Max(planet.SurfaceBands, 6f));
                        material.SetFloat("_Seed", (float)(planet.Seed & 0xFFFFFF) / 16777216f * 10f);
                        material.SetFloat("_Style", planet.Style);
                        material.SetFloat("_Biome", (int)planet.Biome);
                        material.SetFloat("_CraterSoft", planet.IsAuthored ? 0f : 1f);
                        PlanetSurfaceStyle.ApplyClimate(material, planet, planet.Primary);
                        var center = renderer.localBounds.center;
                        material.SetVector("_Center", new Vector4(center.x, center.y, center.z, 0f));
                        renderer.sharedMaterial = material;
                    }
                }

                camera.Render();
                var previous = RenderTexture.active;
                try
                {
                    RenderTexture.active = renderTexture;
                    readback.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);
                    readback.Apply(false, false);
                }
                finally { RenderTexture.active = previous; }

                string directory = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                File.WriteAllBytes(outputPath, readback.EncodeToPNG());
                AssetDatabase.Refresh();
                Debug.Log("Frontier: rendered " + planets.Length + " authored/generated planets to " + outputPath + ".");
            }
            finally
            {
                foreach (Material material in materials) UnityEngine.Object.DestroyImmediate(material);
                UnityEngine.Object.DestroyImmediate(readback);
                renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(renderTexture);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

    }
}
#endif
