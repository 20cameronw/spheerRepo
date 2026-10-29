#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Frontier.EditorTools
{
    public static class StructureThumbnails
    {
        const string Folder = "Assets/Frontier/Resources/Frontier/StructureThumbnails";
        const int Resolution = 256;

        [MenuItem("Frontier/Models/Bake Structure Thumbnails")]
        public static void Bake()
        {
            Directory.CreateDirectory(Folder);
            foreach (var definition in GameDatabase.Load().structures)
            {
                if (definition == null) continue;
                var prefab = ModelLibrary.Prefab(definition.modelKey);
                if (prefab == null)
                    throw new InvalidOperationException("No thumbnail model for structure " + definition.id);
                Render(prefab, Folder + "/" + definition.id + ".png");
            }
            AssetDatabase.Refresh();
            foreach (var definition in GameDatabase.Load().structures)
            {
                if (definition == null) continue;
                var importer = (TextureImporter)AssetImporter.GetAtPath(Folder + "/" + definition.id + ".png");
                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.isReadable = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            Debug.Log("Frontier: baked structure thumbnails from the in-game ModelLibrary.");
        }

        static void Render(GameObject prefab, string path)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var target = new RenderTexture(Resolution, Resolution, 24, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            try
            {
                var model = ModelLibrary.InstantiateInert(prefab);
                SceneManager.MoveGameObjectToScene(model, scene);
                if (!ModelLibrary.MeshBounds(model, out var bounds))
                    throw new InvalidOperationException("No mesh bounds for thumbnail " + prefab.name);

                var cameraObject = new GameObject("ThumbnailCamera");
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                var camera = cameraObject.AddComponent<Camera>();
                camera.scene = scene;
                camera.orthographic = true;
                camera.aspect = 1f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
                float radius = Mathf.Max(0.01f, bounds.extents.magnitude);
                camera.transform.position = bounds.center + new Vector3(3f, 2.4f, -4f).normalized * radius * 4f;
                camera.transform.LookAt(bounds.center);
                camera.nearClipPlane = radius * 0.1f;
                camera.farClipPlane = radius * 8f;
                float extent = 0f;
                for (int i = 0; i < 8; i++)
                {
                    var corner = bounds.center + Vector3.Scale(bounds.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var projected = camera.transform.InverseTransformPoint(corner);
                    extent = Mathf.Max(extent, Mathf.Abs(projected.x), Mathf.Abs(projected.y));
                }
                camera.orthographicSize = extent * 1.12f;
                camera.targetTexture = target;

                AddLight(scene, new Vector3(35f, -35f, 0f), 1.8f);
                AddLight(scene, new Vector3(25f, 140f, 0f), 0.9f);
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, Resolution, Resolution), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(path, pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(pixels);
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        static void AddLight(Scene scene, Vector3 rotation, float intensity)
        {
            var go = new GameObject("ThumbnailLight");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.transform.rotation = Quaternion.Euler(rotation);
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = intensity;
        }
    }
}
#endif
