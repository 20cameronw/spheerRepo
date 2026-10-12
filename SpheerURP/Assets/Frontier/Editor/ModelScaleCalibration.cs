#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Frontier.EditorTools
{
    public static class ModelScaleCalibration
    {
        [MenuItem("Frontier/Calibrate Model Scales")]
        public static void Run()
        {
            var library = ModelLibrary.Instance;
            var models = new HashSet<string>();
            foreach (var entry in library.entries)
                foreach (var path in AssetDatabase.GetDependencies(AssetDatabase.GetAssetPath(entry.prefab), true))
                    if (AssetImporter.GetAtPath(path) is ModelImporter) models.Add(path);

            // Hand-assembled prefabs retain their own part offsets. Preserve their
            // shape when changing the units of the meshes they reference.
            var meshSizes = new Dictionary<string, Vector3>();
            foreach (var path in models)
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (asset is Mesh mesh) meshSizes[MeshKey(mesh)] = mesh.bounds.size;

            foreach (var path in models)
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var instance = ModelLibrary.InstantiateInert(prefab);
                try
                {
                    instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                    instance.transform.localScale = Vector3.one;
                    Bounds bounds;
                    if (!ModelLibrary.MeshBounds(instance, out bounds)) throw new InvalidOperationException("No mesh in " + path);
                    float diameter = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
                    if (diameter <= 0.0001f) throw new InvalidOperationException("Empty model in " + path);
                    float scale = importer.globalScale / diameter;
                    if (!Mathf.Approximately(scale, importer.globalScale))
                    {
                        importer.globalScale = scale;
                        importer.SaveAndReimport();
                    }
                    Debug.Log("Frontier model import: " + path + " scale=" + importer.globalScale);
                }
                finally { UnityEngine.Object.DestroyImmediate(instance); }
            }
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                bool affected = false;
                foreach (var dependency in AssetDatabase.GetDependencies(path, true))
                    if (models.Contains(dependency)) affected = true;
                if (!affected) continue;
                var contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach (var filter in contents.GetComponentsInChildren<MeshFilter>(true))
                    {
                        if (filter.sharedMesh == null || !meshSizes.TryGetValue(MeshKey(filter.sharedMesh), out var before)) continue;
                        Vector3 after = filter.sharedMesh.bounds.size;
                        float oldSize = Mathf.Max(before.x, Mathf.Max(before.y, before.z));
                        float newSize = Mathf.Max(after.x, Mathf.Max(after.y, after.z));
                        if (newSize > 0.0001f)
                        {
                            float correction = oldSize / newSize;
                            filter.transform.localScale *= correction;
                            foreach (Transform child in filter.transform)
                            {
                                child.localPosition /= correction;
                                child.localScale /= correction;
                            }
                        }
                    }
                    PrefabUtility.SaveAsPrefabAsset(contents, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
            }
            BakeLibrary(library);
            AssetDatabase.SaveAssets();
            FrontierPlanetChecks.Run();
            Debug.Log("Frontier: model import scales and library corrections calibrated.");
        }

        static string MeshKey(Mesh mesh)
        {
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh, out string guid, out long id);
            return guid + ":" + id;
        }

        public static void BakeLibrary(ModelLibrary library)
        {
            for (int i = 0; i < library.entries.Length; i++)
            {
                var entry = library.entries[i];
                var instance = ModelLibrary.InstantiateInert(entry.prefab);
                try
                {
                    instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                    instance.transform.localScale = Vector3.one;
                    Bounds bounds;
                    if (!ModelLibrary.MeshBounds(instance, out bounds)) throw new InvalidOperationException("No mesh for " + entry.key);
                    float diameter = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
                    if (diameter <= 0.0001f) throw new InvalidOperationException("Empty mesh for " + entry.key);
                    entry.referenceDiameter = entry.key.StartsWith("struct_", StringComparison.Ordinal) ? 0.75f : 3f;
                    entry.scale = entry.referenceDiameter / diameter;
                    library.entries[i] = entry;
                    Debug.Log("Frontier model correction: " + entry.key + " scale=" + entry.scale);
                }
                finally { UnityEngine.Object.DestroyImmediate(instance); }
            }
            EditorUtility.SetDirty(library);
        }
    }
}
#endif
