#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Frontier.EditorTools
{
    public static class ColonyHubModelBuilder
    {
        const string Root = "Assets/Frontier/Art/Models";
        const string MeshDir = Root + "/ColonyHub";
        const string MaterialDir = MeshDir + "/Materials";
        const string PrefabPath = Root + "/Prefabs/colony_hub.prefab";
        const string LibraryPath = "Assets/Frontier/Resources/Frontier/ModelLibrary.asset";
        const string StructurePath = "Assets/Frontier/Data/Economy/Structures/colony_hub.asset";

        sealed class Geometry
        {
            public readonly List<Vector3> vertices = new List<Vector3>();
            public readonly List<int> triangles = new List<int>();

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int first = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
                triangles.Add(first); triangles.Add(first + 1); triangles.Add(first + 2);
                triangles.Add(first); triangles.Add(first + 2); triangles.Add(first + 3);
            }

            public void PolygonPrism(Vector2[] profile, float nearZ, float farZ)
            {
                int count = profile.Length;
                int near = vertices.Count;
                for (int i = 0; i < count; i++) vertices.Add(new Vector3(profile[i].x, profile[i].y, nearZ));
                int far = vertices.Count;
                for (int i = 0; i < count; i++) vertices.Add(new Vector3(profile[i].x, profile[i].y, farZ));

                for (int i = 1; i < count - 1; i++)
                {
                    triangles.Add(near); triangles.Add(near + i + 1); triangles.Add(near + i);
                    triangles.Add(far); triangles.Add(far + i); triangles.Add(far + i + 1);
                }
                for (int i = 0; i < count; i++)
                {
                    int next = (i + 1) % count;
                    Quad(vertices[near + i], vertices[near + next],
                        vertices[far + next], vertices[far + i]);
                }
            }

            public void Box(Vector3 center, Vector3 size, Quaternion rotation)
            {
                Vector3 h = size * 0.5f;
                Vector3 P(float x, float y, float z) { return center + rotation * new Vector3(x, y, z); }
                Vector3 a = P(-h.x, -h.y, -h.z), b = P(h.x, -h.y, -h.z);
                Vector3 c = P(h.x, h.y, -h.z), d = P(-h.x, h.y, -h.z);
                Vector3 e = P(-h.x, -h.y, h.z), f = P(h.x, -h.y, h.z);
                Vector3 g = P(h.x, h.y, h.z), h0 = P(-h.x, h.y, h.z);
                Quad(a, d, c, b); Quad(e, f, g, h0);
                Quad(a, e, h0, d); Quad(b, c, g, f);
                Quad(d, h0, g, c); Quad(a, b, f, e);
            }

            public void Beam(Vector3 start, Vector3 end, float width, float depth)
            {
                Vector3 delta = end - start;
                Box((start + end) * 0.5f, new Vector3(width, delta.magnitude, depth),
                    Quaternion.FromToRotation(Vector3.up, delta.normalized));
            }

            public void Cylinder(Vector3 start, Vector3 end, float radius, int sides)
            {
                Vector3 axis = (end - start).normalized;
                Vector3 tangent = Vector3.Cross(axis, Mathf.Abs(Vector3.Dot(axis, Vector3.up)) > 0.92f
                    ? Vector3.forward : Vector3.up).normalized;
                Vector3 bitangent = Vector3.Cross(axis, tangent).normalized;
                int first = vertices.Count;
                for (int ring = 0; ring < 2; ring++)
                {
                    Vector3 center = ring == 0 ? start : end;
                    for (int i = 0; i < sides; i++)
                    {
                        float angle = i * Mathf.PI * 2f / sides;
                        vertices.Add(center + (tangent * Mathf.Cos(angle) + bitangent * Mathf.Sin(angle)) * radius);
                    }
                }
                for (int i = 0; i < sides; i++)
                {
                    int next = (i + 1) % sides;
                    triangles.Add(first + i); triangles.Add(first + next); triangles.Add(first + sides + next);
                    triangles.Add(first + i); triangles.Add(first + sides + next); triangles.Add(first + sides + i);
                    triangles.Add(first + sides * 2); triangles.Add(first + next); triangles.Add(first + i);
                    triangles.Add(first + sides * 2 + 1); triangles.Add(first + sides + i); triangles.Add(first + sides + next);
                }
                vertices.Add(start); vertices.Add(end);
            }

            public void Dish(Vector3 center, float radius, float depth, Quaternion rotation, int segments)
            {
                const int rings = 3;
                int first = vertices.Count;
                for (int ring = 0; ring <= rings; ring++)
                {
                    float r = (ring + 1f) / (rings + 1f);
                    for (int i = 0; i < segments; i++)
                    {
                        float angle = i * Mathf.PI * 2f / segments;
                        Vector3 point = new Vector3(Mathf.Cos(angle) * r * radius,
                            Mathf.Sin(angle) * r * radius, -depth * (1f - r * r));
                        vertices.Add(center + rotation * point);
                    }
                }
                int centerIndex = vertices.Count;
                vertices.Add(center + rotation * new Vector3(0f, 0f, -depth));
                for (int i = 0; i < segments; i++)
                {
                    int next = (i + 1) % segments;
                    TwoSidedTriangle(centerIndex, first + i, first + next);
                }
                for (int ring = 0; ring < rings; ring++)
                {
                    int inner = first + ring * segments;
                    int outer = inner + segments;
                    for (int i = 0; i < segments; i++)
                    {
                        int next = (i + 1) % segments;
                        TwoSidedTriangle(inner + i, outer + i, outer + next);
                        TwoSidedTriangle(inner + i, outer + next, inner + next);
                    }
                }
            }

            void TwoSidedTriangle(int a, int b, int c)
            {
                triangles.Add(a); triangles.Add(b); triangles.Add(c);
                triangles.Add(c); triangles.Add(b); triangles.Add(a);
            }

            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(vertices);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            }
        }

        [MenuItem("Frontier/Models/Build Colony Hub")]
        public static void Build()
        {
            EnsureFolder(MeshDir);
            EnsureFolder(MaterialDir);
            EnsureFolder(Root + "/Prefabs");

            var materials = new[]
            {
                SaveMaterial("Hub_Base", new Color32(91, 101, 111, 255), 0.18f),
                SaveMaterial("Hub_Armor", new Color32(174, 181, 178, 255), 0.08f),
                SaveMaterial("Hub_Orange", new Color32(224, 91, 20, 255), 0.12f),
                SaveMaterial("Hub_Charcoal", new Color32(34, 41, 49, 255), 0.18f),
                SaveMaterial("Hub_LightMetal", new Color32(132, 145, 151, 255), 0.2f)
            };
            Geometry[] geometry = { new Geometry(), new Geometry(), new Geometry(), new Geometry(), new Geometry() };
            BuildGeometry(geometry);

            string[] names = { "Base_and_Ramp", "Armor_Shell", "Structural_Orange", "Recesses_and_Machinery", "Light_Metal_Details" };
            var root = new GameObject("colony_hub");
            try
            {
                for (int i = 0; i < geometry.Length; i++)
                {
                    Mesh mesh = SaveMesh(names[i], geometry[i].ToMesh(names[i]));
                    var part = new GameObject(names[i]);
                    part.transform.SetParent(root.transform, false);
                    part.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = part.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = materials[i];
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                }

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }

            var library = AssetDatabase.LoadAssetAtPath<ModelLibrary>(LibraryPath);
            if (library == null) throw new InvalidOperationException("ModelLibrary asset is missing: " + LibraryPath);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var instance = ModelLibrary.InstantiateInert(prefab);
            float correction = 0f;
            try
            {
                if (!ModelLibrary.MeshBounds(instance, out Bounds bounds))
                    throw new InvalidOperationException("The Colony Hub prefab contains no mesh renderers.");
                float diameter = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
                correction = 0.75f / diameter;
                var entries = new List<ModelLibrary.Entry>(library.entries ?? new ModelLibrary.Entry[0]);
                int index = entries.FindIndex(entry => entry.key == "colony_hub");
                var entry = new ModelLibrary.Entry
                {
                    key = "colony_hub",
                    prefab = prefab,
                    scale = correction,
                    referenceDiameter = 0.75f,
                    groundScale = 1f
                };
                if (index >= 0) entries[index] = entry; else entries.Add(entry);
                library.entries = entries.ToArray();
                EditorUtility.SetDirty(library);
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }

            var structure = AssetDatabase.LoadAssetAtPath<StructureSO>(StructurePath);
            if (structure == null) throw new InvalidOperationException("Colony Hub structure definition is missing: " + StructurePath);
            var serialized = new SerializedObject(structure);
            serialized.FindProperty("modelKey").stringValue = "colony_hub";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(structure);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            int vertices = 0, triangles = 0;
            foreach (Geometry part in geometry)
            {
                vertices += part.vertices.Count;
                triangles += part.triangles.Count / 3;
            }
            Debug.Log("Frontier: Colony Hub model built: " + vertices + " vertices, " + triangles +
                " triangles, 5 mesh parts, 5 shared materials; ModelLibrary scale " + correction.ToString("F4") + ".");
        }

        static void BuildGeometry(Geometry[] g)
        {
            Geometry baseMetal = g[0], armor = g[1], orange = g[2], dark = g[3], light = g[4];

            baseMetal.Box(new Vector3(0f, 0.045f, 0f), new Vector3(1.05f, 0.09f, 0.91f), Quaternion.identity);
            baseMetal.Box(new Vector3(0f, 0.101f, 0f), new Vector3(0.99f, 0.035f, 0.85f), Quaternion.identity);
            baseMetal.Box(new Vector3(0f, 0.123f, 0.015f), new Vector3(0.91f, 0.02f, 0.78f), Quaternion.identity);

            armor.Box(new Vector3(0f, 0.25f, 0.26f), new Vector3(0.77f, 0.25f, 0.19f), Quaternion.identity);
            armor.Box(new Vector3(-0.345f, 0.25f, -0.025f), new Vector3(0.15f, 0.25f, 0.48f), Quaternion.identity);
            armor.Box(new Vector3(0.345f, 0.25f, -0.025f), new Vector3(0.15f, 0.25f, 0.48f), Quaternion.identity);
            armor.Box(new Vector3(-0.325f, 0.26f, -0.34f), new Vector3(0.20f, 0.28f, 0.13f), Quaternion.identity);
            armor.Box(new Vector3(0.325f, 0.26f, -0.34f), new Vector3(0.20f, 0.28f, 0.13f), Quaternion.identity);
            armor.Box(new Vector3(0f, 0.425f, -0.325f), new Vector3(0.50f, 0.09f, 0.14f), Quaternion.identity);
            armor.PolygonPrism(new[]
            {
                new Vector2(-0.47f, 0.36f), new Vector2(0.47f, 0.36f),
                new Vector2(0.47f, 0.43f), new Vector2(0.34f, 0.54f),
                new Vector2(-0.34f, 0.54f), new Vector2(-0.47f, 0.43f)
            }, -0.40f, 0.40f);

            dark.Box(new Vector3(0f, 0.245f, -0.247f), new Vector3(0.29f, 0.23f, 0.025f), Quaternion.identity);
            dark.Box(new Vector3(0f, 0.249f, -0.268f), new Vector3(0.23f, 0.19f, 0.012f), Quaternion.identity);
            dark.Box(new Vector3(0f, 0.133f, -0.303f), new Vector3(0.36f, 0.018f, 0.17f), Quaternion.identity);
            dark.Box(new Vector3(-0.335f, 0.25f, -0.416f), new Vector3(0.09f, 0.14f, 0.018f), Quaternion.identity);
            dark.Box(new Vector3(0.335f, 0.25f, -0.416f), new Vector3(0.09f, 0.14f, 0.018f), Quaternion.identity);
            dark.Box(new Vector3(0f, 0.455f, 0.015f), new Vector3(0.60f, 0.018f, 0.43f), Quaternion.identity);

            light.Box(new Vector3(-0.267f, 0.25f, -0.42f), new Vector3(0.035f, 0.19f, 0.025f), Quaternion.identity);
            light.Box(new Vector3(0.267f, 0.25f, -0.42f), new Vector3(0.035f, 0.19f, 0.025f), Quaternion.identity);
            light.Box(new Vector3(0f, 0.365f, -0.415f), new Vector3(0.55f, 0.035f, 0.035f), Quaternion.identity);
            dark.Box(new Vector3(0f, 0.26f, -0.29f), new Vector3(0.18f, 0.22f, 0.02f), Quaternion.identity);
            light.Box(new Vector3(-0.025f, 0.26f, -0.305f), new Vector3(0.035f, 0.16f, 0.008f), Quaternion.identity);
            orange.Box(new Vector3(0.075f, 0.26f, -0.31f), new Vector3(0.018f, 0.045f, 0.012f), Quaternion.identity);

            AddHeavyFrame(orange);
            AddRamp(baseMetal, dark, orange);
            AddSideHousings(baseMetal, armor, dark, orange, light);
            AddRoofMachinery(armor, dark, orange, light);
            AddCommunications(orange, dark, light);
        }

        static void AddHeavyFrame(Geometry orange)
        {
            for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    float px = x * 0.445f, pz = z * 0.355f;
                    orange.Box(new Vector3(px, 0.275f, pz), new Vector3(0.105f, 0.39f, 0.105f), Quaternion.identity);
                    orange.Box(new Vector3(px, 0.475f, pz), new Vector3(0.13f, 0.08f, 0.13f), Quaternion.identity);
                }
            for (int z = -1; z <= 1; z += 2)
                orange.Box(new Vector3(0f, 0.49f, z * 0.355f), new Vector3(0.91f, 0.09f, 0.105f), Quaternion.identity);
            for (int x = -1; x <= 1; x += 2)
                orange.Box(new Vector3(x * 0.445f, 0.49f, 0f), new Vector3(0.105f, 0.09f, 0.66f), Quaternion.identity);

            for (int x = -1; x <= 1; x += 2)
            {
                orange.Beam(new Vector3(x * 0.44f, 0.395f, -0.355f),
                    new Vector3(x * 0.34f, 0.535f, -0.355f), 0.09f, 0.08f);
                orange.Beam(new Vector3(x * 0.44f, 0.395f, 0.355f),
                    new Vector3(x * 0.34f, 0.535f, 0.355f), 0.09f, 0.08f);
            }
        }

        static void AddSideHousings(Geometry baseMetal, Geometry armor, Geometry dark, Geometry orange, Geometry light)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * 0.49f;
                baseMetal.Box(new Vector3(x, 0.205f, 0.12f), new Vector3(0.12f, 0.17f, 0.29f), Quaternion.identity);
                armor.Box(new Vector3(x, 0.235f, 0.12f), new Vector3(0.13f, 0.15f, 0.25f), Quaternion.identity);
                dark.Box(new Vector3(side * 0.56f, 0.23f, 0.12f), new Vector3(0.012f, 0.095f, 0.15f), Quaternion.identity);
                orange.Box(new Vector3(side * 0.565f, 0.21f, 0.12f), new Vector3(0.018f, 0.025f, 0.20f), Quaternion.identity);
                light.Box(new Vector3(side * 0.565f, 0.26f, 0.12f), new Vector3(0.018f, 0.015f, 0.16f), Quaternion.identity);
                orange.Box(new Vector3(side * 0.49f, 0.33f, 0.12f), new Vector3(0.12f, 0.025f, 0.31f), Quaternion.identity);
            }
        }

        static void AddRoofMachinery(Geometry armor, Geometry dark, Geometry orange, Geometry light)
        {
            armor.Box(new Vector3(0f, 0.554f, 0.02f), new Vector3(0.66f, 0.035f, 0.46f), Quaternion.identity);
            dark.Box(new Vector3(-0.18f, 0.58f, 0.06f), new Vector3(0.17f, 0.055f, 0.13f), Quaternion.identity);
            dark.Box(new Vector3(0.10f, 0.58f, 0.06f), new Vector3(0.20f, 0.055f, 0.13f), Quaternion.identity);
            for (int vent = 0; vent < 3; vent++)
            {
                float x = -0.22f + vent * 0.14f;
                light.Box(new Vector3(x, 0.615f, 0.06f), new Vector3(0.105f, 0.018f, 0.15f), Quaternion.identity);
                for (int slat = -1; slat <= 1; slat++)
                    dark.Box(new Vector3(x, 0.627f, 0.06f + slat * 0.035f),
                        new Vector3(0.09f, 0.012f, 0.012f), Quaternion.identity);
            }
            orange.Box(new Vector3(-0.33f, 0.57f, -0.12f), new Vector3(0.09f, 0.035f, 0.15f), Quaternion.identity);
            orange.Box(new Vector3(0.30f, 0.57f, -0.12f), new Vector3(0.11f, 0.035f, 0.14f), Quaternion.identity);
            dark.Box(new Vector3(-0.33f, 0.592f, -0.12f), new Vector3(0.05f, 0.012f, 0.08f), Quaternion.identity);
            dark.Box(new Vector3(0.30f, 0.592f, -0.12f), new Vector3(0.06f, 0.012f, 0.08f), Quaternion.identity);
        }

        static void AddCommunications(Geometry orange, Geometry dark, Geometry light)
        {
            Vector3 mastBase = new Vector3(0.36f, 0.54f, 0.29f);
            Vector3 mastTop = new Vector3(0.40f, 1.04f, 0.31f);
            light.Cylinder(mastBase, mastTop, 0.018f, 8);
            orange.Cylinder(mastBase + Vector3.up * 0.06f, mastBase + Vector3.up * 0.10f, 0.035f, 8);
            orange.Cylinder(mastBase + Vector3.up * 0.31f, mastBase + Vector3.up * 0.35f, 0.03f, 8);
            light.Beam(new Vector3(0.36f, 0.61f, 0.29f), new Vector3(0.23f, 0.54f, 0.21f), 0.018f, 0.018f);
            light.Beam(new Vector3(0.36f, 0.61f, 0.29f), new Vector3(0.50f, 0.54f, 0.39f), 0.018f, 0.018f);
            orange.Beam(new Vector3(0.23f, 0.54f, 0.21f), new Vector3(0.50f, 0.54f, 0.39f), 0.016f, 0.016f);
            Vector3 dishCenter = new Vector3(0.40f, 1.04f, 0.31f);
            Quaternion dishAim = Quaternion.LookRotation(new Vector3(-0.35f, 0.38f, -0.86f).normalized, Vector3.up);
            light.Dish(dishCenter, 0.14f, 0.042f, dishAim, 12);
            AddDishRim(light, dishCenter, 0.14f, dishAim, 12);
            orange.Cylinder(dishCenter + dishAim * new Vector3(0f, 0f, -0.04f),
                dishCenter + dishAim * new Vector3(0f, 0f, 0.04f), 0.014f, 6);
            dark.Cylinder(dishCenter + dishAim * new Vector3(0f, 0f, 0.055f),
                dishCenter + dishAim * new Vector3(0f, 0f, 0.12f), 0.007f, 5);

            light.Cylinder(new Vector3(-0.31f, 0.53f, 0.30f), new Vector3(-0.31f, 0.86f, 0.30f), 0.012f, 6);
            dark.Cylinder(new Vector3(-0.20f, 0.53f, 0.29f), new Vector3(-0.20f, 0.76f, 0.29f), 0.01f, 6);
            orange.Box(new Vector3(-0.31f, 0.86f, 0.30f), new Vector3(0.05f, 0.025f, 0.045f), Quaternion.identity);
            orange.Box(new Vector3(-0.20f, 0.76f, 0.29f), new Vector3(0.04f, 0.02f, 0.04f), Quaternion.identity);
        }

        static void AddRamp(Geometry baseMetal, Geometry dark, Geometry orange)
        {
            Vector3 leftTop = new Vector3(-0.16f, 0.20f, -0.39f);
            Vector3 rightTop = new Vector3(0.16f, 0.20f, -0.39f);
            Vector3 leftEnd = new Vector3(-0.16f, 0.13f, -0.66f);
            Vector3 rightEnd = new Vector3(0.16f, 0.13f, -0.66f);
            Vector3 thickness = Vector3.up * 0.035f;
            baseMetal.Quad(leftTop, leftEnd, rightEnd, rightTop);
            baseMetal.Quad(leftTop - thickness, rightTop - thickness, rightEnd - thickness, leftEnd - thickness);
            baseMetal.Quad(leftTop, rightTop, rightTop - thickness, leftTop - thickness);
            baseMetal.Quad(leftEnd, leftEnd - thickness, rightEnd - thickness, rightEnd);
            baseMetal.Quad(leftTop, leftTop - thickness, leftEnd - thickness, leftEnd);
            baseMetal.Quad(rightTop, rightEnd, rightEnd - thickness, rightTop - thickness);
            dark.Box(new Vector3(0f, 0.19f, -0.45f), new Vector3(0.12f, 0.012f, 0.12f),
                Quaternion.Euler(-22f, 0f, 0f));
            orange.Beam(leftTop + Vector3.up * 0.022f, leftEnd + Vector3.up * 0.022f, 0.035f, 0.035f);
            orange.Beam(rightTop + Vector3.up * 0.022f, rightEnd + Vector3.up * 0.022f, 0.035f, 0.035f);
        }

        static void AddDishRim(Geometry geometry, Vector3 center, float radius, Quaternion rotation, int segments)
        {
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                float next = (i + 1) * Mathf.PI * 2f / segments;
                Vector3 a = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
                Vector3 b = new Vector3(Mathf.Cos(next) * radius, Mathf.Sin(next) * radius, 0f);
                geometry.Beam(center + rotation * a, center + rotation * b, 0.012f, 0.012f);
            }
        }

        static Material SaveMaterial(string name, Color color, float metallic)
        {
            string path = MaterialDir + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
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
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.24f);
            EditorUtility.SetDirty(material);
            return material;
        }

        static Mesh SaveMesh(string name, Mesh generated)
        {
            string path = MeshDir + "/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(generated, path);
                return generated;
            }
            EditorUtility.CopySerialized(generated, existing);
            existing.name = name;
            UnityEngine.Object.DestroyImmediate(generated);
            EditorUtility.SetDirty(existing);
            return existing;
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
