#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Frontier.EditorTools;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Frontier.EditorTools
{
    public static class FrontierWorldModelBuilder
    {
        const string Root = "Assets/Frontier/Art/Models/Environment";
        const string MeshDir = Root + "/Meshes";
        const string MaterialDir = Root + "/Materials";
        const string PrefabDir = "Assets/Frontier/Art/Models/Prefabs";
        const string LibraryPath = "Assets/Frontier/Resources/Frontier/ModelLibrary.asset";
        const string ObstacleDir = "Assets/Frontier/Data/Economy/Obstacles";

        static readonly string[] PlanetIds =
        {
            "sylva", "ferrum", "glacia", "cinder", "arbor", "nyx", "aurum", "tide", "zephyr",
            "pyre", "halcyon", "mire", "obsidian", "vesper", "prism", "kaos", "tundra", "coral"
        };

        sealed class Geometry
        {
            readonly List<Vector3> vertices = new List<Vector3>();
            readonly List<int>[] triangles = { new List<int>(), new List<int>(), new List<int>() };

            public void Tri(Vector3 a, Vector3 b, Vector3 c, int material, Vector3 outward)
            {
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0f)
                {
                    Vector3 swap = b;
                    b = c;
                    c = swap;
                }
                int first = vertices.Count;
                vertices.Add(a);
                vertices.Add(b);
                vertices.Add(c);
                triangles[material].Add(first);
                triangles[material].Add(first + 1);
                triangles[material].Add(first + 2);
            }

            public void Box(Vector3 center, Vector3 size, Quaternion rotation, int material)
            {
                Vector3 h = size * 0.5f;
                Vector3 P(float x, float y, float z) { return center + rotation * new Vector3(x, y, z); }
                Vector3 a = P(-h.x, -h.y, -h.z), b = P(h.x, -h.y, -h.z);
                Vector3 c = P(h.x, h.y, -h.z), d = P(-h.x, h.y, -h.z);
                Vector3 e = P(-h.x, -h.y, h.z), f = P(h.x, -h.y, h.z);
                Vector3 g = P(h.x, h.y, h.z), h0 = P(-h.x, h.y, h.z);
                Tri(a, d, c, material, rotation * Vector3.back);
                Tri(a, c, b, material, rotation * Vector3.back);
                Tri(e, f, g, material, rotation * Vector3.forward);
                Tri(e, g, h0, material, rotation * Vector3.forward);
                Tri(a, e, h0, material, rotation * Vector3.left);
                Tri(a, h0, d, material, rotation * Vector3.left);
                Tri(b, c, g, material, rotation * Vector3.right);
                Tri(b, g, f, material, rotation * Vector3.right);
                Tri(d, h0, g, material, rotation * Vector3.up);
                Tri(d, g, c, material, rotation * Vector3.up);
                Tri(a, b, f, material, rotation * Vector3.down);
                Tri(a, f, e, material, rotation * Vector3.down);
            }

            public void Beam(Vector3 start, Vector3 end, float width, float depth, int material)
            {
                Vector3 delta = end - start;
                Box((start + end) * 0.5f, new Vector3(width, delta.magnitude, depth),
                    Quaternion.FromToRotation(Vector3.up, delta.normalized), material);
            }

            public void Cylinder(Vector3 start, Vector3 end, float radiusA, float radiusB, int sides, int material)
            {
                Vector3 axis = (end - start).normalized;
                Vector3 tangent = Vector3.Cross(axis, Mathf.Abs(Vector3.Dot(axis, Vector3.up)) > 0.92f
                    ? Vector3.forward : Vector3.up).normalized;
                Vector3 bitangent = Vector3.Cross(axis, tangent).normalized;
                for (int i = 0; i < sides; i++)
                {
                    float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                    Vector3 n0 = tangent * Mathf.Cos(a0) + bitangent * Mathf.Sin(a0);
                    Vector3 n1 = tangent * Mathf.Cos(a1) + bitangent * Mathf.Sin(a1);
                    Vector3 b0 = start + n0 * radiusA, b1 = start + n1 * radiusA;
                    Vector3 t0 = end + n0 * radiusB, t1 = end + n1 * radiusB;
                    Vector3 outward = n0 + n1;
                    Tri(b0, t0, t1, material, outward);
                    Tri(b0, t1, b1, material, outward);
                    Tri(start, b1, b0, material, -axis);
                    Tri(end, t0, t1, material, axis);
                }
            }

            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(vertices);
                mesh.subMeshCount = triangles.Length;
                for (int i = 0; i < triangles.Length; i++) mesh.SetTriangles(triangles[i], i, false);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            }
        }

        [MenuItem("Frontier/Models/Build Planet and World Models")]
        public static void Build()
        {
            EnsureFolder(Root);
            EnsureFolder(MeshDir);
            EnsureFolder(MaterialDir);
            EnsureFolder(PrefabDir);

            Material planet = SaveMaterial("Planet_Reference", new Color32(130, 145, 158, 255), 0.05f);
            Material[] treeMaterials =
            {
                SaveMaterial("World_Bark", new Color32(102, 62, 36, 255), 0.02f),
                SaveMaterial("World_Wood", new Color32(172, 119, 68, 255), 0.02f),
                SaveMaterial("World_Growth", new Color32(47, 105, 69, 255), 0.01f)
            };
            Material[] wreckMaterials =
            {
                SaveMaterial("World_Debris", new Color32(34, 41, 49, 255), 0.16f),
                SaveMaterial("World_DebrisMetal", new Color32(128, 141, 150, 255), 0.22f),
                SaveMaterial("World_DebrisAccent", new Color32(193, 77, 26, 255), 0.14f)
            };

            ModelLibrary library = AssetDatabase.LoadAssetAtPath<ModelLibrary>(LibraryPath);
            if (library == null) throw new InvalidOperationException("ModelLibrary asset is missing: " + LibraryPath);
            var entries = new List<ModelLibrary.Entry>(library.entries ?? new ModelLibrary.Entry[0]);
            int planetTriangles = 0;
            foreach (string id in PlanetIds)
            {
                Mesh mesh = SaveMesh("planet_" + id, CreatePlanetMesh(id));
                planetTriangles += mesh.triangles.Length / 3;
                string key = id;
                GameObject prefab = SavePrefab(key, mesh, new[] { planet });
                float diameter = Mathf.Max(mesh.bounds.size.x, Mathf.Max(mesh.bounds.size.y, mesh.bounds.size.z));
                Upsert(entries, new ModelLibrary.Entry
                {
                    key = key, prefab = prefab, referenceDiameter = 3f, scale = 3f / diameter, groundScale = 1f
                });
            }

            Mesh tree = SaveMesh("world_dead_tree", CreateDeadTree());
            Mesh wreckage = SaveMesh("world_wreckage", CreateWreckage());
            GameObject treePrefab = SavePrefab("world_dead_tree", tree, treeMaterials);
            GameObject wreckagePrefab = SavePrefab("world_wreckage", wreckage, wreckMaterials);
            SetObstacleModel("dead_tree", "world_dead_tree");
            SetObstacleModel("wreckage", "world_wreckage");
            Upsert(entries, Entry("world_dead_tree", treePrefab, tree.bounds, 0.72f));
            Upsert(entries, Entry("world_wreckage", wreckagePrefab, wreckage.bounds, 0.72f));

            library.entries = entries.ToArray();
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Frontier: built " + PlanetIds.Length + " low-poly planet meshes (" + planetTriangles +
                " triangles total), plus dead-tree and wreckage world models; preserved all other model-library entries.");
        }

        static Mesh CreatePlanetMesh(string id)
        {
            int seed = StableSeed(id);
            int subdivisions = 2;
            List<Vector3> vertices = new List<Vector3>
            {
                new Vector3(-1, (1f + Mathf.Sqrt(5f)) * 0.5f, 0), new Vector3(1, (1f + Mathf.Sqrt(5f)) * 0.5f, 0),
                new Vector3(-1, -(1f + Mathf.Sqrt(5f)) * 0.5f, 0), new Vector3(1, -(1f + Mathf.Sqrt(5f)) * 0.5f, 0),
                new Vector3(0, -1, (1f + Mathf.Sqrt(5f)) * 0.5f), new Vector3(0, 1, (1f + Mathf.Sqrt(5f)) * 0.5f),
                new Vector3(0, -1, -(1f + Mathf.Sqrt(5f)) * 0.5f), new Vector3(0, 1, -(1f + Mathf.Sqrt(5f)) * 0.5f),
                new Vector3((1f + Mathf.Sqrt(5f)) * 0.5f, 0, -1), new Vector3((1f + Mathf.Sqrt(5f)) * 0.5f, 0, 1),
                new Vector3(-(1f + Mathf.Sqrt(5f)) * 0.5f, 0, -1), new Vector3(-(1f + Mathf.Sqrt(5f)) * 0.5f, 0, 1)
            };
            for (int i = 0; i < vertices.Count; i++) vertices[i] = vertices[i].normalized;
            var faces = new List<int[]>
            {
                new[] {0,11,5}, new[] {0,5,1}, new[] {0,1,7}, new[] {0,7,10}, new[] {0,10,11},
                new[] {1,5,9}, new[] {5,11,4}, new[] {11,10,2}, new[] {10,7,6}, new[] {7,1,8},
                new[] {3,9,4}, new[] {3,4,2}, new[] {3,2,6}, new[] {3,6,8}, new[] {3,8,9},
                new[] {4,9,5}, new[] {2,4,11}, new[] {6,2,10}, new[] {8,6,7}, new[] {9,8,1}
            };
            for (int pass = 0; pass < subdivisions; pass++)
            {
                var midpointCache = new Dictionary<long, int>();
                var nextFaces = new List<int[]>(faces.Count * 4);
                foreach (int[] face in faces)
                {
                    int ab = Midpoint(face[0], face[1], vertices, midpointCache);
                    int bc = Midpoint(face[1], face[2], vertices, midpointCache);
                    int ca = Midpoint(face[2], face[0], vertices, midpointCache);
                    nextFaces.Add(new[] { face[0], ab, ca });
                    nextFaces.Add(new[] { face[1], bc, ab });
                    nextFaces.Add(new[] { face[2], ca, bc });
                    nextFaces.Add(new[] { ab, bc, ca });
                }
                faces = nextFaces;
            }

            float roughness = PlanetRoughness(id);
            var meshVertices = new List<Vector3>(faces.Count * 3);
            var triangles = new List<int>(faces.Count * 3);
            foreach (int[] face in faces)
            {
                for (int corner = 0; corner < 3; corner++)
                {
                    Vector3 direction = vertices[face[corner]];
                    float phase = seed * 0.173f;
                    float broad = Mathf.Sin(direction.x * 5.1f + phase) *
                        Mathf.Sin(direction.y * 6.7f - phase * 0.71f) *
                        Mathf.Sin(direction.z * 5.8f + phase * 0.37f);
                    float detail = Mathf.Sin(direction.x * 12.4f - phase * 0.2f) *
                        Mathf.Cos(direction.y * 10.1f + phase * 0.43f) *
                        Mathf.Sin(direction.z * 11.6f - phase * 0.33f);
                    float radius = 1f + roughness * (broad * 0.64f + detail * 0.36f);
                    meshVertices.Add(direction * radius);
                    triangles.Add(triangles.Count);
                }
            }
            var mesh = new Mesh { name = "planet_" + id + "_mesh" };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(meshVertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static int Midpoint(int a, int b, List<Vector3> vertices, Dictionary<long, int> cache)
        {
            long key = a < b ? ((long)a << 32) + (uint)b : ((long)b << 32) + (uint)a;
            if (cache.TryGetValue(key, out int index)) return index;
            vertices.Add(((vertices[a] + vertices[b]) * 0.5f).normalized);
            index = vertices.Count - 1;
            cache.Add(key, index);
            return index;
        }

        static float PlanetRoughness(string id)
        {
            switch (id)
            {
                case "zephyr": return 0.012f;
                case "sylva": case "tide": case "coral": case "halcyon": return 0.022f;
                case "ferrum": case "cinder": case "aurum": case "pyre": case "mire": case "vesper": return 0.04f;
                case "glacia": case "arbor": case "nyx": case "obsidian": case "prism": case "kaos": case "tundra": return 0.048f;
                default: return 0.03f;
            }
        }

        static Mesh CreateDeadTree()
        {
            var g = new Geometry();
            g.Cylinder(new Vector3(0f, 0.04f, 0f), new Vector3(-0.02f, 0.72f, 0f), 0.13f, 0.085f, 7, 0);
            g.Cylinder(new Vector3(-0.02f, 0.66f, 0f), new Vector3(-0.34f, 1.05f, 0.02f), 0.085f, 0.05f, 6, 0);
            g.Cylinder(new Vector3(-0.34f, 1.05f, 0.02f), new Vector3(-0.46f, 1.18f, 0.02f), 0.05f, 0.025f, 5, 1);
            g.Cylinder(new Vector3(-0.01f, 0.58f, 0f), new Vector3(0.31f, 0.98f, -0.02f), 0.075f, 0.038f, 6, 0);
            g.Cylinder(new Vector3(0.31f, 0.98f, -0.02f), new Vector3(0.49f, 1.05f, -0.02f), 0.038f, 0.018f, 5, 1);
            g.Cylinder(new Vector3(-0.04f, 0.42f, -0.02f), new Vector3(-0.12f, 0.78f, -0.34f), 0.065f, 0.03f, 6, 0);
            g.Cylinder(new Vector3(-0.12f, 0.78f, -0.34f), new Vector3(-0.08f, 0.96f, -0.41f), 0.03f, 0.014f, 5, 1);
            g.Cylinder(new Vector3(0f, 0.15f, 0f), new Vector3(-0.25f, 0.03f, 0.12f), 0.09f, 0.025f, 6, 0);
            g.Cylinder(new Vector3(0f, 0.12f, 0f), new Vector3(0.18f, 0.02f, -0.19f), 0.08f, 0.02f, 6, 0);
            g.Box(new Vector3(-0.46f, 1.18f, 0.02f), new Vector3(0.08f, 0.035f, 0.07f),
                Quaternion.Euler(0f, 0f, 24f), 2);
            g.Box(new Vector3(0.49f, 1.05f, -0.02f), new Vector3(0.07f, 0.03f, 0.06f),
                Quaternion.Euler(0f, 0f, -12f), 2);
            return g.ToMesh("world_dead_tree_mesh");
        }

        static Mesh CreateWreckage()
        {
            var g = new Geometry();
            g.Box(new Vector3(0f, 0.28f, 0f), new Vector3(0.58f, 0.14f, 0.38f),
                Quaternion.Euler(12f, 0f, -17f), 0);
            g.Box(new Vector3(-0.29f, 0.34f, -0.01f), new Vector3(0.48f, 0.07f, 0.22f),
                Quaternion.Euler(0f, 0f, 22f), 1);
            g.Box(new Vector3(0.31f, 0.24f, 0.02f), new Vector3(0.43f, 0.055f, 0.19f),
                Quaternion.Euler(10f, 8f, -36f), 0);
            g.Box(new Vector3(0.03f, 0.39f, -0.04f), new Vector3(0.22f, 0.055f, 0.12f),
                Quaternion.Euler(-14f, 7f, 31f), 2);
            g.Beam(new Vector3(-0.13f, 0.17f, -0.11f), new Vector3(0.17f, 0.38f, 0.11f), 0.045f, 0.045f, 1);
            g.Beam(new Vector3(-0.26f, 0.22f, 0.14f), new Vector3(-0.47f, 0.55f, 0.17f), 0.10f, 0.045f, 1);
            g.Box(new Vector3(-0.48f, 0.57f, 0.17f), new Vector3(0.11f, 0.04f, 0.08f),
                Quaternion.Euler(0f, 0f, 20f), 2);
            g.Beam(new Vector3(0.15f, 0.20f, -0.11f), new Vector3(0.43f, 0.48f, -0.17f), 0.07f, 0.04f, 0);
            g.Box(new Vector3(0.45f, 0.51f, -0.17f), new Vector3(0.08f, 0.03f, 0.06f),
                Quaternion.Euler(0f, 0f, -28f), 1);
            return g.ToMesh("world_wreckage_mesh");
        }

        static GameObject SavePrefab(string key, Mesh mesh, Material[] materials)
        {
            string path = PrefabDir + "/" + key + ".prefab";
            var root = new GameObject(key);
            try
            {
                root.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = root.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) throw new InvalidOperationException("Could not save model prefab: " + path);
            return prefab;
        }

        static Mesh SaveMesh(string id, Mesh generated)
        {
            string path = MeshDir + "/" + id + ".asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(generated, path);
                return generated;
            }
            EditorUtility.CopySerialized(generated, existing);
            existing.name = generated.name;
            UnityEngine.Object.DestroyImmediate(generated);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        static Material SaveMaterial(string name, Color color, float metallic)
        {
            string path = MaterialDir + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("No supported Lit shader is available.");
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader) material.shader = shader;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.22f);
            EditorUtility.SetDirty(material);
            return material;
        }

        static void SetObstacleModel(string id, string modelKey)
        {
            ObstacleKindSO definition = AssetDatabase.LoadAssetAtPath<ObstacleKindSO>(ObstacleDir + "/" + id + ".asset");
            if (definition == null) throw new InvalidOperationException("Obstacle definition is missing: " + id);
            definition.modelKey = modelKey;
            EditorUtility.SetDirty(definition);
        }

        static ModelLibrary.Entry Entry(string key, GameObject prefab, Bounds bounds, float diameter)
        {
            float size = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            if (size <= 0.0001f) throw new InvalidOperationException("Empty mesh for " + key);
            return new ModelLibrary.Entry
            {
                key = key, prefab = prefab, referenceDiameter = diameter, scale = diameter / size, groundScale = 1f
            };
        }

        static void Upsert(List<ModelLibrary.Entry> entries, ModelLibrary.Entry entry)
        {
            int index = entries.FindIndex(candidate => candidate.key == entry.key);
            if (index >= 0) entries[index] = entry;
            else entries.Add(entry);
        }

        static int StableSeed(string value)
        {
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < value.Length; i++) hash = hash * 31 + value[i];
                return hash;
            }
        }

        static void EnsureFolder(string path)
        {
            string absolute = Path.GetFullPath(path);
            if (!Directory.Exists(absolute)) Directory.CreateDirectory(absolute);
            AssetDatabase.Refresh();
        }
    }
}
#endif
