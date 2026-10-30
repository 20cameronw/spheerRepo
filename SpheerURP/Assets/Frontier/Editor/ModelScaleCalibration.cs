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
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Frontier/Art/Models/Prefabs" }))
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

        // Planet models are the ones a planet asset names or the universe generator can pick; the rest (aliens, turrets, structures) have no ground.
        public static bool IsPlanetModel(string key)
        {
            if (string.IsNullOrEmpty(key) || key.StartsWith("struct_", StringComparison.Ordinal)) return false;
            foreach (var planet in GameDatabase.Load().planets) if (planet != null && planet.ModelKey == key) return true;
            foreach (string generated in new Frontier.Universe.UniverseConfig().ModelKeys) if (generated == key) return true;
            return false;
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
                    if (entry.key == "colony_hub") entry.foundationDepth = Mathf.Max(0f, -bounds.min.y);
                    float diameter = Mathf.Max(bounds.size.x,
                        Mathf.Max(bounds.size.y - entry.foundationDepth, bounds.size.z));
                    if (diameter <= 0.0001f) throw new InvalidOperationException("Empty mesh for " + entry.key);
                    entry.referenceDiameter = entry.key.StartsWith("struct_", StringComparison.Ordinal) ? 0.75f : 3f;
                    entry.scale = entry.referenceDiameter / diameter;
                    if (!IsPlanetModel(entry.key)) entry.groundScale = 1f;
                    else
                    {
                        // fitted exactly as ModelLibrary.Spawn does, then measured
                        instance.transform.localScale = Vector3.one * entry.scale;
                        instance.transform.position = -bounds.center * entry.scale;
                        entry.groundScale = MeasureGroundScale(instance, entry);
                    }
                    library.entries[i] = entry;
                    Debug.Log("Frontier model correction: " + entry.key + " scale=" + entry.scale + " ground=" + entry.groundScale);
                }
                finally { UnityEngine.Object.DestroyImmediate(instance); }
            }
            EditorUtility.SetDirty(library);
        }

        [MenuItem("Frontier/Bake Model Ground Radii")]
        public static void BakeGroundRadii()
        {
            var library = ModelLibrary.Instance;
            if (library == null) { Debug.LogError("Frontier: no ModelLibrary asset."); return; }
            int changed = 0;
            for (int i = 0; i < library.entries.Length; i++)
            {
                var entry = library.entries[i];
                if (entry.prefab == null) continue;
                float ground = 1f;
                if (IsPlanetModel(entry.key))
                {
                    var instance = ModelLibrary.Spawn(entry.key, null, entry.referenceDiameter > 0f ? entry.referenceDiameter : 3f, true);
                    if (instance == null) continue;
                    try { ground = MeasureGroundScale(instance.gameObject, entry); }
                    finally { UnityEngine.Object.DestroyImmediate(instance.gameObject); }
                }
                if (!Mathf.Approximately(ground, entry.groundScale)) changed++;
                entry.groundScale = ground;
                library.entries[i] = entry;
                Debug.Log("Frontier model ground radius: " + entry.key + " " + ground);
            }
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            Debug.Log("Frontier: model ground radii baked (" + changed + " changed).");
        }

        /// <summary>
        /// Ground radius of a planet model, as a fraction of its reference radius. The model is fitted so that its bounds are the reference
        /// diameter, which makes models with spikes or an inner core look smaller than the standard ground sphere structures sit on. This
        /// measures the model's outer radius in 400 directions (the farthest vertex in each) and takes the 40th percentile, so a few spikes do
        /// not count and a flattened body counts a little less than its equator. Near-round models (0.94 and up) return exactly 1.
        /// </summary>
        public static float MeasureGroundScale(GameObject fitted, ModelLibrary.Entry entry)
        {
            const int Directions = 400;
            var bounds = new Bounds();
            if (!ModelLibrary.MeshBounds(fitted, out bounds)) return 1f;
            var dirs = new Vector3[Directions];
            for (int i = 0; i < Directions; i++)
            {
                float y = 1f - 2f * (i + 0.5f) / Directions;
                float r = Mathf.Sqrt(1f - y * y), a = i * 2.39996323f;
                dirs[i] = new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
            }
            var outer = new float[Directions];
            foreach (var filter in fitted.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null) continue;
                var vertices = filter.sharedMesh.vertices;
                for (int v = 0; v < vertices.Length; v++)
                {
                    Vector3 p = filter.transform.TransformPoint(vertices[v]) - bounds.center;
                    float distance = p.magnitude;
                    if (distance < 1e-4f) continue;
                    Vector3 d = p / distance;
                    int best = 0;
                    float bestDot = -2f;
                    for (int k = 0; k < Directions; k++)
                    {
                        float dot = d.x * dirs[k].x + d.y * dirs[k].y + d.z * dirs[k].z;
                        if (dot > bestDot) { bestDot = dot; best = k; }
                    }
                    if (distance > outer[best]) outer[best] = distance;
                }
            }
            var seen = new List<float>();
            foreach (float r in outer) if (r > 0f) seen.Add(r);
            if (seen.Count == 0) return 1f;
            seen.Sort();
            float radius = seen[Mathf.Clamp(Mathf.FloorToInt(0.4f * (seen.Count - 1)), 0, seen.Count - 1)];
            float reference = (entry.referenceDiameter > 0f ? entry.referenceDiameter : 3f) * 0.5f;
            float scale = radius / reference;
            return scale >= 0.94f ? 1f : Mathf.Round(scale * 100f) / 100f;
        }
    }
}
#endif
