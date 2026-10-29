#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Frontier.EditorTools
{
    public static class StructureModelBuilder
    {
        const string Root = "Assets/Frontier/Art/Models/Structures";
        const string MeshDir = Root + "/Meshes";
        const string MaterialDir = Root + "/Materials";
        const string PrefabDir = "Assets/Frontier/Art/Models/Prefabs";
        const string LibraryPath = "Assets/Frontier/Resources/Frontier/ModelLibrary.asset";
        const string DataDir = "Assets/Frontier/Data/Economy/Structures";

        static readonly string[] Ids =
        {
            "lumber", "mine", "refinery", "warehouse", "gunner",
            "driller", "biodome", "siphon_rig", "excavator", "salvage_yard", "fabricator", "foundry",
            "chem_reactor", "quantum_lab", "fuel_depot", "cargo_depot", "orbital_relay", "research_lab",
            "radar_tower", "hangar", "auto_harvester", "alarm", "laser", "shield_pylon", "missile_silo",
            "tesla_coil", "repair_bay", "terraformer", "warp_gate", "dyson_collector"
        };

        const int Base = 0, Armor = 1, Accent = 2, Machinery = 3, Metal = 4, Glass = 5, Emissive = 6;

        sealed class Geometry
        {
            public readonly List<Vector3> vertices = new List<Vector3>();
            public readonly List<int> triangles = new List<int>();
            public int foundationVertexCount = -1;

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int first = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
                triangles.Add(first); triangles.Add(first + 1); triangles.Add(first + 2);
                triangles.Add(first); triangles.Add(first + 2); triangles.Add(first + 3);
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

            public void Cylinder(Vector3 start, Vector3 end, float radius, int sides = 8, float endRadius = -1f)
            {
                float radiusB = endRadius < 0f ? radius : endRadius;
                Vector3 axis = (end - start).normalized;
                Vector3 tangent = Vector3.Cross(axis, Mathf.Abs(Vector3.Dot(axis, Vector3.up)) > 0.92f
                    ? Vector3.forward : Vector3.up).normalized;
                Vector3 bitangent = Vector3.Cross(axis, tangent).normalized;
                int first = vertices.Count;
                for (int ring = 0; ring < 2; ring++)
                {
                    Vector3 center = ring == 0 ? start : end;
                    float r = ring == 0 ? radius : radiusB;
                    for (int i = 0; i < sides; i++)
                    {
                        float angle = i * Mathf.PI * 2f / sides;
                        vertices.Add(center + (tangent * Mathf.Cos(angle) + bitangent * Mathf.Sin(angle)) * r);
                    }
                }
                int startCap = vertices.Count;
                vertices.Add(start);
                int endCap = vertices.Count;
                vertices.Add(end);
                for (int i = 0; i < sides; i++)
                {
                    int next = (i + 1) % sides;
                    triangles.Add(first + i); triangles.Add(first + next); triangles.Add(first + sides + next);
                    triangles.Add(first + i); triangles.Add(first + sides + next); triangles.Add(first + sides + i);
                    triangles.Add(startCap); triangles.Add(first + next); triangles.Add(first + i);
                    triangles.Add(endCap); triangles.Add(first + sides + i); triangles.Add(first + sides + next);
                }
            }

            public void Dome(Vector3 center, float radius, float height, int sides = 12, int rings = 5)
            {
                int first = vertices.Count;
                for (int ring = 0; ring <= rings; ring++)
                {
                    float angle = Mathf.Lerp(0f, Mathf.PI * 0.5f, ring / (float)rings);
                    float r = Mathf.Cos(angle) * radius;
                    float y = Mathf.Sin(angle) * height;
                    for (int i = 0; i < sides; i++)
                    {
                        float a = i * Mathf.PI * 2f / sides;
                        vertices.Add(center + new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r));
                    }
                }
                for (int ring = 0; ring < rings; ring++)
                {
                    int lower = first + ring * sides;
                    int upper = lower + sides;
                    for (int i = 0; i < sides; i++)
                    {
                        int next = (i + 1) % sides;
                        triangles.Add(lower + i); triangles.Add(upper + next); triangles.Add(lower + next);
                        triangles.Add(lower + i); triangles.Add(upper + i); triangles.Add(upper + next);
                    }
                }
            }

            public void Ring(Vector3 center, float radius, float tube, int around = 24, int radial = 6,
                Quaternion planeRotation = default(Quaternion))
            {
                if (planeRotation == default(Quaternion)) planeRotation = Quaternion.identity;
                int first = vertices.Count;
                for (int i = 0; i < around; i++)
                {
                    float a = i * Mathf.PI * 2f / around;
                    Vector3 normal = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                    for (int j = 0; j < radial; j++)
                    {
                        float b = j * Mathf.PI * 2f / radial;
                        vertices.Add(center + planeRotation * (normal * (radius + tube * Mathf.Cos(b)) +
                            Vector3.forward * (tube * Mathf.Sin(b))));
                    }
                }
                for (int i = 0; i < around; i++)
                {
                    int nextI = (i + 1) % around;
                    for (int j = 0; j < radial; j++)
                    {
                        int nextJ = (j + 1) % radial;
                        int a = first + i * radial + j, b = first + nextI * radial + j;
                        int c = first + nextI * radial + nextJ, d = first + i * radial + nextJ;
                        triangles.Add(a); triangles.Add(b); triangles.Add(c);
                        triangles.Add(a); triangles.Add(c); triangles.Add(d);
                    }
                }
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
                    AddTwoSided(centerIndex, first + i, first + next);
                }
                for (int ring = 0; ring < rings; ring++)
                {
                    int inner = first + ring * segments;
                    int outer = inner + segments;
                    for (int i = 0; i < segments; i++)
                    {
                        int next = (i + 1) % segments;
                        AddTwoSided(inner + i, outer + i, outer + next);
                        AddTwoSided(inner + i, outer + next, inner + next);
                    }
                }
            }

            void AddTwoSided(int a, int b, int c)
            {
                triangles.Add(a); triangles.Add(b); triangles.Add(c);
                triangles.Add(c); triangles.Add(b); triangles.Add(a);
            }

            public void Sphere(Vector3 center, float radius, int sides = 10, int rings = 6)
            {
                int first = vertices.Count;
                for (int y = 0; y <= rings; y++)
                {
                    float lat = Mathf.PI * y / rings;
                    float r = Mathf.Sin(lat) * radius;
                    float h = Mathf.Cos(lat) * radius;
                    for (int x = 0; x < sides; x++)
                    {
                        float lon = Mathf.PI * 2f * x / sides;
                        vertices.Add(center + new Vector3(Mathf.Cos(lon) * r, h, Mathf.Sin(lon) * r));
                    }
                }
                for (int y = 0; y < rings; y++)
                {
                    for (int x = 0; x < sides; x++)
                    {
                        int next = (x + 1) % sides;
                        int a = first + y * sides + x, b = first + y * sides + next;
                        int c = a + sides, d = b + sides;
                        triangles.Add(a); triangles.Add(b); triangles.Add(d);
                        triangles.Add(a); triangles.Add(d); triangles.Add(c);
                    }
                }
            }

            public void AppendTo(List<Vector3> allVertices, List<int>[] submeshes, int materialIndex)
            {
                int offset = allVertices.Count;
                allVertices.AddRange(vertices);
                List<int> destination = submeshes[materialIndex];
                for (int i = 0; i < triangles.Count; i++) destination.Add(offset + triangles[i]);
            }

            public void LowerStructureGeometry(float amount)
            {
                if (foundationVertexCount < 0) return;
                for (int i = foundationVertexCount; i < vertices.Count; i++)
                    vertices[i] += Vector3.down * amount;
            }
        }

        [MenuItem("Frontier/Models/Build Structure Set")]
        public static void Build()
        {
            EnsureFolder(Root);
            EnsureFolder(MeshDir);
            EnsureFolder(MaterialDir);
            EnsureFolder(PrefabDir);

            Material[] materials =
            {
                SaveMaterial("Structure_Base", new Color32(18, 21, 24, 255), 0.18f),
                SaveMaterial("Structure_Armor", new Color32(174, 181, 178, 255), 0.08f),
                SaveMaterial("Structure_Accent", new Color32(224, 91, 20, 255), 0.12f),
                SaveMaterial("Structure_Machinery", new Color32(34, 41, 49, 255), 0.18f),
                SaveMaterial("Structure_LightMetal", new Color32(132, 145, 151, 255), 0.2f),
                SaveGlass(),
                SaveEmissive()
            };

            ModelLibrary library = AssetDatabase.LoadAssetAtPath<ModelLibrary>(LibraryPath);
            if (library == null) throw new InvalidOperationException("ModelLibrary asset is missing: " + LibraryPath);
            var entries = new List<ModelLibrary.Entry>(library.entries ?? new ModelLibrary.Entry[0]);
            int totalVertices = 0, totalTriangles = 0;

            foreach (string id in Ids)
            {
                string dataPath = DataDir + "/" + id + ".asset";
                StructureSO definition = AssetDatabase.LoadAssetAtPath<StructureSO>(dataPath);
                if (definition == null) throw new InvalidOperationException("Structure definition is missing: " + dataPath);

                Geometry[] geometry = CreateGeometry(id);
                Mesh generated = Combine(id, geometry, out int vertices, out int triangles);
                totalVertices += vertices;
                totalTriangles += triangles;
                Mesh mesh = SaveMesh(id, generated);

                string key = "struct_" + id;
                string prefabPath = PrefabDir + "/" + key + ".prefab";
                var root = new GameObject(key);
                try
                {
                    root.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = root.AddComponent<MeshRenderer>();
                    renderer.sharedMaterials = materials;
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null) throw new InvalidOperationException("Could not save model prefab: " + prefabPath);
                definition.modelKey = key;
                EditorUtility.SetDirty(definition);
                UpsertEntry(entries, CreateEntry(key, prefab, mesh.bounds));
            }

            library.entries = entries.ToArray();
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Frontier: built " + Ids.Length + " structure models, " + totalVertices +
                " vertices, " + totalTriangles + " triangles; 7 shared material roles.");
        }

        public static void UpsertLibraryEntries(List<ModelLibrary.Entry> entries)
        {
            foreach (string id in Ids)
            {
                string key = "struct_" + id;
                string path = PrefabDir + "/" + key + ".prefab";
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) throw new InvalidOperationException("Build the structure model set before rebuilding ModelLibrary: " + path);
                MeshFilter filter = prefab.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null)
                    throw new InvalidOperationException("Structure model has no mesh: " + path);
                UpsertEntry(entries, CreateEntry(key, prefab, filter.sharedMesh.bounds));
            }
        }

        static ModelLibrary.Entry CreateEntry(string key, GameObject prefab, Bounds bounds)
        {
            float diameter = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            if (diameter <= 0.0001f) throw new InvalidOperationException("Empty structure model: " + key);
            return new ModelLibrary.Entry
            {
                key = key,
                prefab = prefab,
                referenceDiameter = 0.75f,
                scale = 0.75f / diameter,
                groundScale = 1f
            };
        }

        static void UpsertEntry(List<ModelLibrary.Entry> entries, ModelLibrary.Entry entry)
        {
            int index = entries.FindIndex(candidate => candidate.key == entry.key);
            if (index >= 0) entries[index] = entry;
            else entries.Add(entry);
        }

        static Mesh Combine(string id, Geometry[] geometry, out int vertices, out int triangles)
        {
            var allVertices = new List<Vector3>();
            var submeshes = new List<int>[geometry.Length];
            for (int i = 0; i < submeshes.Length; i++) submeshes[i] = new List<int>();
            for (int i = 0; i < geometry.Length; i++)
                geometry[i].AppendTo(allVertices, submeshes, i);
            var mesh = new Mesh { name = id + "_mesh" };
            mesh.SetVertices(allVertices);
            mesh.subMeshCount = geometry.Length;
            for (int i = 0; i < submeshes.Length; i++) mesh.SetTriangles(submeshes[i], i, false);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            vertices = mesh.vertexCount;
            triangles = 0;
            foreach (List<int> indices in submeshes) triangles += indices.Count / 3;
            return mesh;
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
            existing.name = id + "_mesh";
            UnityEngine.Object.DestroyImmediate(generated);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        static Geometry[] CreateGeometry(string id)
        {
            var g = new Geometry[7];
            for (int i = 0; i < g.Length; i++) g[i] = new Geometry();
            switch (id)
            {
                case "lumber": BuildLumber(g); break;
                case "mine": BuildMine(g); break;
                case "refinery": BuildRefinery(g); break;
                case "warehouse": BuildWarehouse(g); break;
                case "gunner": BuildGunner(g, false); break;
                case "driller": BuildDriller(g); break;
                case "biodome": BuildBiodome(g); break;
                case "siphon_rig": BuildSiphon(g); break;
                case "excavator": BuildExcavator(g); break;
                case "salvage_yard": BuildSalvage(g); break;
                case "fabricator": BuildFabricator(g); break;
                case "foundry": BuildFoundry(g); break;
                case "chem_reactor": BuildChemReactor(g); break;
                case "quantum_lab": BuildQuantumLab(g); break;
                case "fuel_depot": BuildFuelDepot(g); break;
                case "cargo_depot": BuildCargoDepot(g); break;
                case "orbital_relay": BuildOrbitalRelay(g); break;
                case "research_lab": BuildResearchLab(g); break;
                case "radar_tower": BuildRadar(g); break;
                case "hangar": BuildHangar(g); break;
                case "auto_harvester": BuildAutoHarvester(g); break;
                case "alarm": BuildAlarm(g); break;
                case "laser": BuildGunner(g, true); break;
                case "shield_pylon": BuildShieldPylon(g); break;
                case "missile_silo": BuildMissileSilo(g); break;
                case "tesla_coil": BuildTesla(g); break;
                case "repair_bay": BuildRepairBay(g); break;
                case "terraformer": BuildTerraformer(g); break;
                case "warp_gate": BuildWarpGate(g); break;
                case "dyson_collector": BuildDyson(g); break;
                default: throw new ArgumentOutOfRangeException(nameof(id), id, "No authored model geometry.");
            }
            foreach (Geometry materialGeometry in g) materialGeometry.LowerStructureGeometry(0.08f);
            return g;
        }

        static void Box(Geometry[] g, int role, Vector3 center, Vector3 size, Quaternion rotation = default(Quaternion))
        {
            if (rotation == default(Quaternion)) rotation = Quaternion.identity;
            g[role].Box(center, size, rotation);
        }

        static void Beam(Geometry[] g, int role, Vector3 a, Vector3 b, float width, float depth = -1f)
        {
            g[role].Beam(a, b, width, depth < 0f ? width : depth);
        }

        static void Cylinder(Geometry[] g, int role, Vector3 a, Vector3 b, float radius, int sides = 8, float endRadius = -1f)
        {
            g[role].Cylinder(a, b, radius, sides, endRadius);
        }

        static void Platform(Geometry[] g, float width = 0.96f, float depth = 0.82f)
        {
            width *= 0.84f;
            depth *= 0.84f;
            Box(g, Base, new Vector3(0f, 0.09f, 0f), new Vector3(width, 0.18f, depth));
            foreach (Geometry materialGeometry in g)
                if (materialGeometry.foundationVertexCount < 0)
                    materialGeometry.foundationVertexCount = materialGeometry.vertices.Count;
        }

        static void Tank(Geometry[] g, float x, float z, float bottom, float height, float radius,
            bool orangeBand = true, int role = Armor)
        {
            Cylinder(g, role, new Vector3(x, bottom, z), new Vector3(x, bottom + height, z), radius, 10);
            Cylinder(g, Base, new Vector3(x, bottom + 0.03f, z), new Vector3(x, bottom + 0.08f, z), radius * 1.08f, 10);
            Cylinder(g, Metal, new Vector3(x, bottom + height - 0.035f, z),
                new Vector3(x, bottom + height + 0.012f, z), radius * 1.04f, 10);
            if (orangeBand)
                Cylinder(g, Accent, new Vector3(x, bottom + height * 0.70f, z),
                    new Vector3(x, bottom + height * 0.78f, z), radius * 1.035f, 10);
            Cylinder(g, Machinery, new Vector3(x, bottom + height, z),
                new Vector3(x, bottom + height + 0.06f, z), radius * 0.25f, 8);
            Cylinder(g, Accent, new Vector3(x, bottom + height + 0.045f, z),
                new Vector3(x, bottom + height + 0.07f, z), radius * 0.31f, 8);
        }

        static void Pipe(Geometry[] g, Vector3 a, Vector3 b, float radius = 0.022f)
        {
            Cylinder(g, Metal, a, b, radius, 6);
            Vector3 direction = (b - a).normalized;
            Cylinder(g, Accent, a + direction * 0.08f, a + direction * 0.11f, radius * 1.55f, 6);
        }

        static void Crate(Geometry[] g, Vector3 center, Vector3 size, bool accent = true)
        {
            Box(g, Machinery, center, size);
            Box(g, Armor, center + Vector3.forward * (size.z * 0.51f), new Vector3(size.x * 0.78f, size.y * 0.72f, 0.025f));
            if (!accent) return;
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    Box(g, Accent, center + new Vector3(x * size.x * 0.44f, y * size.y * 0.42f, size.z * 0.52f),
                        new Vector3(0.025f, size.y * 0.15f, 0.03f));
        }

        static void Gantry(Geometry[] g, float xSpan, float z, float height, float beamWidth = 0.07f)
        {
            for (int x = -1; x <= 1; x += 2)
            {
                Beam(g, Accent, new Vector3(x * xSpan, 0.12f, z), new Vector3(x * xSpan, height, z), beamWidth, beamWidth);
                Beam(g, Metal, new Vector3(x * xSpan, height - 0.09f, z), new Vector3(0f, height - 0.09f, z), beamWidth * 0.72f);
                Beam(g, Machinery, new Vector3(x * xSpan, 0.22f, z),
                    new Vector3(0f, height - 0.10f, z), beamWidth * 0.42f);
            }
        }

        static void Arm(Geometry[] g, Vector3 pivot, Vector3 elbow, Vector3 tool, float width = 0.075f)
        {
            Cylinder(g, Machinery, pivot, elbow, width * 0.68f, 8);
            Beam(g, Accent, pivot, elbow, width);
            Cylinder(g, Metal, elbow, tool, width * 0.48f, 8);
            Beam(g, Accent, elbow, tool, width * 0.66f);
            foreach (Vector3 joint in new[] { pivot, elbow })
            {
                Cylinder(g, Machinery, joint - Vector3.forward * width * 0.45f,
                    joint + Vector3.forward * width * 0.45f, width * 0.68f, 8);
                Cylinder(g, Metal, joint + Vector3.forward * width * 0.46f,
                    joint + Vector3.forward * width * 0.53f, width * 0.32f, 8);
            }
        }

        static void AddSolarWing(Geometry[] g, Vector3 center, Vector2 size, float yaw)
        {
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
            Box(g, Machinery, center, new Vector3(size.x, 0.025f, size.y), rotation);
            for (int x = -1; x <= 1; x += 2)
            {
                Vector3 p = center + rotation * new Vector3(x * size.x * 0.48f, 0.018f, 0f);
                Box(g, Metal, p, new Vector3(0.025f, 0.035f, size.y * 1.05f), rotation);
            }
            int columns = 3;
            for (int i = 0; i < columns; i++)
            {
                Vector3 p = center + rotation * new Vector3((i - 1) * size.x * 0.29f, 0.018f, 0f);
                Box(g, Glass, p, new Vector3(size.x * 0.24f, 0.008f, size.y * 0.82f), rotation);
            }
        }

        static void AddFins(Geometry[] g, Vector3 center, float radius, float height, int count, int role = Accent)
        {
            for (int i = 0; i < count; i++)
            {
                float a = i * Mathf.PI * 2f / count;
                Vector3 p = center + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                Box(g, role, p, new Vector3(0.035f, height, 0.08f), Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f));
            }
        }

        static void BuildLumber(Geometry[] g)
        {
            Platform(g, 0.98f, 0.88f);
            Gantry(g, 0.31f, 0.12f, 0.70f);
            Box(g, Armor, new Vector3(0f, 0.70f, 0.12f), new Vector3(0.74f, 0.10f, 0.27f), Quaternion.Euler(0f, 0f, -7f));
            Box(g, Accent, new Vector3(0f, 0.755f, 0.12f), new Vector3(0.68f, 0.035f, 0.24f), Quaternion.Euler(0f, 0f, -7f));
            for (int i = 0; i < 4; i++)
                Cylinder(g, Metal, new Vector3(-0.31f, 0.19f + i * 0.07f, -0.22f),
                    new Vector3(0.29f, 0.19f + i * 0.07f, -0.22f), 0.06f, 8);
            Cylinder(g, Machinery, new Vector3(-0.34f, 0.20f, 0.12f), new Vector3(-0.34f, 0.50f, 0.12f), 0.13f, 12);
            g[Metal].Ring(new Vector3(-0.34f, 0.50f, 0.12f), 0.15f, 0.025f, 16, 5);
            for (int i = 0; i < 5; i++)
                Beam(g, Metal, new Vector3(-0.34f, 0.50f, 0.12f), new Vector3(-0.34f + (i - 2) * 0.12f, 0.50f + (i % 2) * 0.04f, 0.12f), 0.018f);
            for (int i = 0; i < 3; i++)
                Cylinder(g, Metal, new Vector3(-0.30f + i * 0.20f, 0.25f, -0.20f),
                    new Vector3(0.08f + i * 0.20f, 0.25f, -0.20f), 0.055f, 8);
            Box(g, Machinery, new Vector3(0.29f, 0.22f, 0.24f), new Vector3(0.24f, 0.18f, 0.22f));
            Box(g, Armor, new Vector3(0.29f, 0.34f, 0.24f), new Vector3(0.27f, 0.05f, 0.24f));
        }

        static void BuildMine(Geometry[] g)
        {
            Platform(g, 0.92f, 0.90f);
            Box(g, Machinery, new Vector3(0f, 0.18f, 0.05f), new Vector3(0.72f, 0.16f, 0.52f));
            Crate(g, new Vector3(-0.27f, 0.30f, 0.05f), new Vector3(0.32f, 0.28f, 0.30f));
            Crate(g, new Vector3(0.29f, 0.23f, -0.18f), new Vector3(0.27f, 0.18f, 0.23f), false);
            Arm(g, new Vector3(0.18f, 0.36f, 0.18f), new Vector3(0.34f, 0.72f, 0.05f),
                new Vector3(0.08f, 0.56f, -0.18f), 0.085f);
            Box(g, Machinery, new Vector3(0.07f, 0.31f, -0.28f), new Vector3(0.48f, 0.10f, 0.18f));
            for (int i = 0; i < 3; i++)
            {
                float x = -0.22f + i * 0.20f;
                Box(g, Metal, new Vector3(x, 0.46f, -0.22f), new Vector3(0.12f, 0.08f, 0.12f));
            }
            for (int i = 0; i < 5; i++)
            {
                Vector3 p = new Vector3(-0.31f + i * 0.14f, 0.15f + (i % 2) * 0.08f, -0.34f);
                Box(g, Machinery, p, new Vector3(0.13f, 0.12f + (i % 3) * 0.04f, 0.13f),
                    Quaternion.Euler(i * 7f, i * 19f, i * 23f));
            }
        }

        static void BuildRefinery(Geometry[] g)
        {
            Platform(g, 0.94f, 0.82f);
            Cylinder(g, Armor, new Vector3(0f, 0.13f, 0.02f), new Vector3(0f, 0.61f, 0.02f), 0.23f, 12, 0.18f);
            Cylinder(g, Machinery, new Vector3(0f, 0.14f, 0.02f), new Vector3(0f, 0.27f, 0.02f), 0.24f, 12);
            Cylinder(g, Accent, new Vector3(0f, 0.53f, 0.02f), new Vector3(0f, 0.59f, 0.02f), 0.22f, 12);
            g[Armor].Dome(new Vector3(0f, 0.61f, 0.02f), 0.18f, 0.17f, 12, 4);
            Tank(g, -0.30f, -0.21f, 0.12f, 0.36f, 0.105f, true);
            Tank(g, 0.29f, -0.22f, 0.12f, 0.29f, 0.09f, false);
            Cylinder(g, Metal, new Vector3(0.31f, 0.12f, 0.25f), new Vector3(0.31f, 0.88f, 0.25f), 0.085f, 10);
            Cylinder(g, Machinery, new Vector3(0.31f, 0.81f, 0.25f), new Vector3(0.31f, 0.90f, 0.25f), 0.10f, 10);
            for (int y = 0; y < 4; y++)
                Cylinder(g, Accent, new Vector3(0.31f, 0.20f + y * 0.18f, 0.25f),
                    new Vector3(0.31f, 0.23f + y * 0.18f, 0.25f), 0.095f, 10);
            Pipe(g, new Vector3(-0.30f, 0.49f, -0.21f), new Vector3(-0.30f, 0.68f, 0.05f));
            Pipe(g, new Vector3(-0.30f, 0.68f, 0.05f), new Vector3(0.31f, 0.68f, 0.25f));
            Crate(g, new Vector3(-0.32f, 0.20f, 0.28f), new Vector3(0.17f, 0.18f, 0.17f));
        }

        static void BuildWarehouse(Geometry[] g)
        {
            Platform(g, 1.0f, 0.94f);
            Box(g, Armor, new Vector3(0f, 0.29f, -0.22f), new Vector3(0.69f, 0.36f, 0.10f));
            Box(g, Metal, new Vector3(0f, 0.47f, -0.22f), new Vector3(0.76f, 0.09f, 0.18f));
            Box(g, Armor, new Vector3(0f, 0.54f, 0.02f), new Vector3(0.88f, 0.08f, 0.70f));
            Box(g, Accent, new Vector3(0f, 0.59f, 0.02f), new Vector3(0.91f, 0.035f, 0.72f));
            for (int x = -1; x <= 1; x += 2)
            {
                Beam(g, Accent, new Vector3(x * 0.38f, 0.12f, 0.28f), new Vector3(x * 0.38f, 0.58f, 0.28f), 0.09f);
                Beam(g, Metal, new Vector3(x * 0.38f, 0.16f, -0.22f), new Vector3(x * 0.38f, 0.55f, 0.28f), 0.035f);
                Box(g, Armor, new Vector3(x * 0.32f, 0.29f, 0.02f), new Vector3(0.08f, 0.30f, 0.50f));
            }
            Box(g, Metal, new Vector3(0f, 0.16f, 0.15f), new Vector3(0.52f, 0.025f, 0.42f));
            for (int i = 0; i < 3; i++)
                Crate(g, new Vector3(-0.22f + i * 0.22f, 0.18f, 0.12f), new Vector3(0.16f, 0.16f, 0.15f), i % 2 == 0);
        }

        static void BuildGunner(Geometry[] g, bool laser)
        {
            Platform(g, 0.84f, 0.76f);
            Cylinder(g, Machinery, new Vector3(0f, 0.11f, 0f), new Vector3(0f, 0.21f, 0f), 0.31f, 12);
            Cylinder(g, Accent, new Vector3(0f, 0.19f, 0f), new Vector3(0f, 0.24f, 0f), 0.29f, 12);
            Cylinder(g, Machinery, new Vector3(0f, 0.22f, 0f), new Vector3(0f, 0.40f, 0f), 0.20f, 10);
            if (laser)
            {
                Box(g, Armor, new Vector3(0f, 0.45f, -0.03f), new Vector3(0.34f, 0.23f, 0.34f));
                Box(g, Accent, new Vector3(0f, 0.56f, -0.03f), new Vector3(0.26f, 0.045f, 0.32f));
                Beam(g, Accent, new Vector3(0f, 0.47f, 0.02f), new Vector3(0f, 0.57f, 0.61f), 0.13f, 0.13f);
                Cylinder(g, Machinery, new Vector3(0f, 0.57f, 0.47f), new Vector3(0f, 0.60f, 0.70f), 0.065f, 8, 0.035f);
                Cylinder(g, Emissive, new Vector3(0f, 0.60f, 0.69f), new Vector3(0f, 0.60f, 0.73f), 0.04f, 8);
            }
            else
            {
                Cylinder(g, Armor, new Vector3(0f, 0.34f, 0f), new Vector3(0f, 0.53f, 0f), 0.25f, 10);
                Cylinder(g, Accent, new Vector3(0f, 0.50f, 0f), new Vector3(0f, 0.57f, 0f), 0.25f, 10);
            }
            if (laser)
            {
                Cylinder(g, Machinery, new Vector3(-0.11f, 0.41f, 0.11f), new Vector3(-0.11f, 0.53f, 0.61f), 0.025f, 8);
                Cylinder(g, Machinery, new Vector3(0.11f, 0.41f, 0.11f), new Vector3(0.11f, 0.53f, 0.61f), 0.025f, 8);
            }
            else
            {
                Box(g, Armor, new Vector3(0f, 0.50f, 0.16f), new Vector3(0.40f, 0.22f, 0.34f));
                for (int x = -1; x <= 1; x += 2)
                    for (int z = -1; z <= 1; z += 2)
                    {
                        float y = 0.49f + z * 0.07f;
                        Cylinder(g, Machinery, new Vector3(x * 0.11f, y, 0.24f),
                            new Vector3(x * 0.11f, y, 0.66f), 0.035f, 8);
                        Cylinder(g, Metal, new Vector3(x * 0.11f, y, 0.58f),
                            new Vector3(x * 0.11f, y, 0.67f), 0.024f, 8);
                    }
            }
        }

        static void BuildDriller(Geometry[] g)
        {
            Platform(g, 0.82f, 0.82f);
            for (int x = -1; x <= 1; x += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 foot = new Vector3(x * 0.34f, 0.13f, z * 0.31f);
                    Vector3 top = new Vector3(x * 0.19f, 0.88f, z * 0.18f);
                    Beam(g, Accent, foot, top, 0.055f);
                    Beam(g, Metal, new Vector3(x * 0.34f, 0.40f, z * 0.31f),
                        new Vector3(-x * 0.22f, 0.40f, -z * 0.18f), 0.028f);
                }
            }
            Box(g, Machinery, new Vector3(0f, 0.88f, 0f), new Vector3(0.48f, 0.10f, 0.46f));
            Box(g, Accent, new Vector3(0f, 0.94f, 0f), new Vector3(0.54f, 0.06f, 0.50f));
            Cylinder(g, Armor, new Vector3(0f, 0.80f, 0f), new Vector3(0f, 0.94f, 0f), 0.18f, 10);
            Cylinder(g, Metal, new Vector3(0f, 0.10f, 0f), new Vector3(0f, 0.69f, 0f), 0.08f, 8, 0.12f);
            Cylinder(g, Machinery, new Vector3(0f, 0.10f, 0f), new Vector3(0f, 0.24f, 0f), 0.14f, 8, 0.035f);
            Cylinder(g, Accent, new Vector3(0f, 0.70f, 0f), new Vector3(0f, 0.76f, 0f), 0.16f, 8);
        }

        static void BuildBiodome(Geometry[] g)
        {
            Platform(g, 0.98f, 0.86f);
            Cylinder(g, Machinery, new Vector3(0f, 0.10f, 0f), new Vector3(0f, 0.22f, 0f), 0.39f, 12);
            Cylinder(g, Accent, new Vector3(0f, 0.19f, 0f), new Vector3(0f, 0.24f, 0f), 0.39f, 12);
            g[Glass].Dome(new Vector3(0f, 0.23f, 0f), 0.36f, 0.51f, 12, 5);
            g[Metal].Ring(new Vector3(0f, 0.235f, 0f), 0.37f, 0.025f, 12, 5, Quaternion.Euler(90f, 0f, 0f));
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                Vector3 edge = new Vector3(Mathf.Cos(a) * 0.35f, 0.24f, Mathf.Sin(a) * 0.35f);
                Vector3 crown = new Vector3(0f, 0.73f, 0f);
                Beam(g, Accent, edge, crown, 0.025f);
            }
            Cylinder(g, Accent, new Vector3(0f, 0.72f, 0f), new Vector3(0f, 0.79f, 0f), 0.055f, 8);
            for (int i = 0; i < 5; i++)
            {
                float x = (i - 2) * 0.12f;
                Cylinder(g, Machinery, new Vector3(x, 0.25f, -0.02f), new Vector3(x, 0.32f, 0.06f), 0.035f, 5);
                Box(g, Glass, new Vector3(x, 0.34f, 0.07f), new Vector3(0.055f, 0.10f, 0.055f));
            }
        }

        static void BuildSiphon(Geometry[] g)
        {
            Platform(g, 0.90f, 0.80f);
            Cylinder(g, Armor, new Vector3(-0.25f, 0.34f, -0.02f), new Vector3(-0.25f, 0.68f, -0.02f), 0.095f, 10, 0.20f);
            Cylinder(g, Armor, new Vector3(0.20f, 0.31f, -0.06f), new Vector3(0.20f, 0.60f, -0.06f), 0.085f, 10, 0.17f);
            Cylinder(g, Accent, new Vector3(-0.25f, 0.65f, -0.02f), new Vector3(-0.25f, 0.70f, -0.02f), 0.21f, 10);
            Cylinder(g, Accent, new Vector3(0.20f, 0.57f, -0.06f), new Vector3(0.20f, 0.62f, -0.06f), 0.18f, 10);
            Tank(g, 0f, -0.28f, 0.14f, 0.31f, 0.10f, false, Machinery);
            for (int x = -1; x <= 1; x += 2)
            {
                Beam(g, Accent, new Vector3(x * 0.30f, 0.16f, -0.02f), new Vector3(x * 0.24f, 0.58f, -0.02f), 0.045f);
                Beam(g, Metal, new Vector3(x * 0.30f, 0.28f, -0.02f), new Vector3(x * 0.12f, 0.48f, -0.02f), 0.025f);
            }
            for (int x = -1; x <= 1; x += 2)
            {
                Pipe(g, new Vector3(x * 0.25f, 0.60f, 0.08f), new Vector3(x * 0.25f, 0.80f, 0.24f), 0.025f);
                Pipe(g, new Vector3(x * 0.25f, 0.80f, 0.24f), new Vector3(0f, 0.80f, 0.24f), 0.025f);
            }
            Box(g, Machinery, new Vector3(0f, 0.22f, 0.30f), new Vector3(0.28f, 0.20f, 0.17f));
            Box(g, Armor, new Vector3(0f, 0.31f, 0.39f), new Vector3(0.18f, 0.08f, 0.025f));
        }

        static void BuildExcavator(Geometry[] g)
        {
            Platform(g, 0.96f, 0.82f);
            for (int x = -1; x <= 1; x += 2)
            {
                Box(g, Machinery, new Vector3(x * 0.31f, 0.16f, -0.02f), new Vector3(0.16f, 0.16f, 0.72f));
                for (int z = -1; z <= 1; z += 2)
                    Cylinder(g, Metal, new Vector3(x * 0.31f, 0.16f, z * 0.27f),
                        new Vector3(x * 0.31f, 0.16f, z * 0.32f), 0.09f, 8);
                Box(g, Accent, new Vector3(x * 0.31f, 0.25f, -0.02f), new Vector3(0.11f, 0.035f, 0.42f));
            }
            Box(g, Machinery, new Vector3(-0.02f, 0.28f, -0.02f), new Vector3(0.44f, 0.17f, 0.44f));
            Crate(g, new Vector3(-0.19f, 0.34f, -0.04f), new Vector3(0.30f, 0.31f, 0.29f));
            Box(g, Armor, new Vector3(0.20f, 0.31f, 0.10f), new Vector3(0.30f, 0.27f, 0.30f));
            Box(g, Glass, new Vector3(0.20f, 0.38f, 0.26f), new Vector3(0.18f, 0.10f, 0.02f));
            Arm(g, new Vector3(0.05f, 0.48f, 0.18f), new Vector3(0.32f, 0.77f, 0.19f),
                new Vector3(0.50f, 0.50f, 0.22f), 0.085f);
            for (int i = 0; i < 3; i++)
                Box(g, Machinery, new Vector3(-0.27f + i * 0.16f, 0.13f, -0.32f),
                    new Vector3(0.12f, 0.09f, 0.10f), Quaternion.Euler(0f, 0f, 12f));
            for (int x = -1; x <= 1; x += 2)
                Box(g, Accent, new Vector3(x * 0.38f, 0.23f, 0.21f), new Vector3(0.06f, 0.24f, 0.07f));
        }

        static void BuildSalvage(Geometry[] g)
        {
            Platform(g, 0.98f, 0.86f);
            Gantry(g, 0.36f, 0.05f, 0.74f);
            Beam(g, Accent, new Vector3(-0.36f, 0.72f, -0.25f), new Vector3(0.36f, 0.72f, -0.25f), 0.07f);
            Beam(g, Metal, new Vector3(-0.30f, 0.68f, -0.25f), new Vector3(-0.30f, 0.38f, -0.25f), 0.025f);
            Cylinder(g, Metal, new Vector3(-0.30f, 0.38f, -0.25f), new Vector3(-0.30f, 0.29f, -0.25f), 0.025f, 6);
            Cylinder(g, Accent, new Vector3(-0.30f, 0.24f, -0.25f), new Vector3(-0.30f, 0.32f, -0.25f), 0.09f, 8, 0.055f);
            Crate(g, new Vector3(-0.28f, 0.22f, 0.25f), new Vector3(0.30f, 0.22f, 0.27f));
            Crate(g, new Vector3(0.25f, 0.18f, 0.22f), new Vector3(0.24f, 0.16f, 0.20f), false);
            for (int i = 0; i < 4; i++)
            {
                float x = -0.31f + i * 0.19f;
                Box(g, Machinery, new Vector3(x, 0.16f + (i % 2) * 0.06f, -0.20f),
                    new Vector3(0.15f, 0.12f + (i % 3) * 0.04f, 0.13f), Quaternion.Euler(i * 11f, i * 29f, i * 17f));
            }
        }

        static void BuildFabricator(Geometry[] g)
        {
            Platform(g, 0.90f, 0.98f);
            Box(g, Machinery, new Vector3(0f, 0.28f, -0.22f), new Vector3(0.68f, 0.34f, 0.12f));
            Box(g, Armor, new Vector3(0f, 0.48f, -0.08f), new Vector3(0.78f, 0.10f, 0.42f));
            Box(g, Accent, new Vector3(0f, 0.54f, -0.08f), new Vector3(0.82f, 0.045f, 0.45f));
            for (int x = -1; x <= 1; x += 2)
            {
                Beam(g, Accent, new Vector3(x * 0.36f, 0.13f, 0.22f), new Vector3(x * 0.36f, 0.54f, 0.22f), 0.085f);
                Beam(g, Metal, new Vector3(x * 0.36f, 0.18f, -0.20f), new Vector3(x * 0.36f, 0.50f, 0.22f), 0.035f);
            }
            Box(g, Machinery, new Vector3(0f, 0.20f, 0.20f), new Vector3(0.48f, 0.08f, 0.34f));
            Box(g, Metal, new Vector3(0f, 0.25f, 0.31f), new Vector3(0.42f, 0.025f, 0.16f));
            for (int i = 0; i < 3; i++)
                Box(g, Metal, new Vector3(-0.16f + i * 0.16f, 0.30f, 0.32f), new Vector3(0.07f, 0.08f, 0.08f));
            Arm(g, new Vector3(-0.18f, 0.34f, 0.20f), new Vector3(-0.24f, 0.59f, 0.14f),
                new Vector3(0.02f, 0.50f, 0.08f), 0.045f);
            Cylinder(g, Accent, new Vector3(0.19f, 0.22f, -0.01f), new Vector3(0.19f, 0.46f, -0.01f), 0.09f, 8);
        }

        static void BuildFoundry(Geometry[] g)
        {
            Platform(g, 0.86f, 0.80f);
            Cylinder(g, Machinery, new Vector3(0f, 0.12f, -0.08f), new Vector3(0f, 0.48f, -0.08f), 0.27f, 10, 0.19f);
            Cylinder(g, Armor, new Vector3(0f, 0.34f, -0.08f), new Vector3(0f, 0.53f, -0.08f), 0.21f, 10);
            Cylinder(g, Accent, new Vector3(0f, 0.45f, -0.08f), new Vector3(0f, 0.52f, -0.08f), 0.23f, 10);
            Cylinder(g, Machinery, new Vector3(0f, 0.49f, -0.08f), new Vector3(0f, 0.83f, -0.08f), 0.13f, 8, 0.075f);
            Cylinder(g, Metal, new Vector3(0f, 0.80f, -0.08f), new Vector3(0f, 0.88f, -0.08f), 0.09f, 8);
            Cylinder(g, Accent, new Vector3(0f, 0.86f, -0.08f), new Vector3(0f, 0.91f, -0.08f), 0.10f, 8);
            Tank(g, -0.30f, -0.25f, 0.13f, 0.38f, 0.10f);
            Tank(g, 0.30f, -0.25f, 0.13f, 0.30f, 0.085f);
            for (int x = -1; x <= 1; x += 2)
            {
                Cylinder(g, Armor, new Vector3(x * 0.30f, 0.12f, 0.10f), new Vector3(x * 0.30f, 0.33f, 0.10f), 0.09f, 8, 0.12f);
                Cylinder(g, Accent, new Vector3(x * 0.30f, 0.30f, 0.10f), new Vector3(x * 0.30f, 0.34f, 0.10f), 0.13f, 8);
            }
            Box(g, Machinery, new Vector3(0f, 0.18f, 0.31f), new Vector3(0.28f, 0.18f, 0.13f));
            Box(g, Accent, new Vector3(0f, 0.28f, 0.35f), new Vector3(0.10f, 0.05f, 0.035f));
            Pipe(g, new Vector3(-0.30f, 0.50f, -0.25f), new Vector3(-0.10f, 0.62f, -0.08f));
            Pipe(g, new Vector3(0.30f, 0.45f, -0.25f), new Vector3(0.10f, 0.65f, -0.08f));
        }

        static void BuildChemReactor(Geometry[] g)
        {
            Platform(g, 0.94f, 0.84f);
            Cylinder(g, Machinery, new Vector3(0f, 0.12f, -0.12f), new Vector3(0f, 0.62f, -0.12f), 0.18f, 10, 0.22f);
            Cylinder(g, Armor, new Vector3(0f, 0.25f, -0.12f), new Vector3(0f, 0.65f, -0.12f), 0.19f, 10);
            Cylinder(g, Accent, new Vector3(0f, 0.55f, -0.12f), new Vector3(0f, 0.62f, -0.12f), 0.21f, 10);
            g[Armor].Dome(new Vector3(0f, 0.65f, -0.12f), 0.19f, 0.12f, 10, 3);
            for (int x = -1; x <= 1; x += 2)
            {
                Tank(g, x * 0.29f, -0.20f, 0.12f, x < 0 ? 0.48f : 0.58f, 0.105f, true);
                Cylinder(g, Machinery, new Vector3(x * 0.29f, 0.12f, 0.21f), new Vector3(x * 0.29f, 0.30f, 0.21f), 0.12f, 8, 0.07f);
            }
            Cylinder(g, Machinery, new Vector3(0f, 0.17f, 0.31f), new Vector3(0f, 0.38f, 0.31f), 0.12f, 8);
            Cylinder(g, Emissive, new Vector3(0f, 0.24f, 0.31f), new Vector3(0f, 0.31f, 0.31f), 0.10f, 8);
            Pipe(g, new Vector3(-0.29f, 0.60f, -0.20f), new Vector3(0f, 0.70f, -0.12f), 0.025f);
            Pipe(g, new Vector3(0.29f, 0.70f, -0.20f), new Vector3(0f, 0.70f, -0.12f), 0.025f);
            Pipe(g, new Vector3(0f, 0.39f, 0.31f), new Vector3(0.28f, 0.50f, 0.14f), 0.025f);
        }

        static void BuildQuantumLab(Geometry[] g)
        {
            Platform(g, 0.86f, 0.90f);
            Cylinder(g, Machinery, new Vector3(0f, 0.10f, 0f), new Vector3(0f, 0.25f, 0f), 0.36f, 12);
            g[Glass].Dome(new Vector3(0f, 0.25f, 0f), 0.30f, 0.40f, 12, 5);
            g[Metal].Ring(new Vector3(0f, 0.25f, 0f), 0.31f, 0.035f, 12, 5, Quaternion.Euler(90f, 0f, 0f));
            g[Accent].Ring(new Vector3(0f, 0.48f, 0f), 0.34f, 0.028f, 16, 5);
            g[Metal].Ring(new Vector3(0f, 0.48f, 0f), 0.34f, 0.024f, 16, 5,
                Quaternion.Euler(90f, 0f, 0f));
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f;
                Vector3 p = new Vector3(Mathf.Cos(a) * 0.33f, 0.31f, Mathf.Sin(a) * 0.33f);
                Beam(g, Accent, p, new Vector3(0f, 0.48f, 0f), 0.045f);
            }
            g[Emissive].Sphere(new Vector3(0f, 0.50f, 0f), 0.12f, 10, 5);
            Cylinder(g, Metal, new Vector3(0f, 0.50f, 0f), new Vector3(0f, 0.74f, 0f), 0.025f, 6);
            Cylinder(g, Accent, new Vector3(0f, 0.70f, 0f), new Vector3(0f, 0.77f, 0f), 0.08f, 8);
        }

        static void BuildFuelDepot(Geometry[] g)
        {
            Platform(g, 0.98f, 0.86f);
            Tank(g, -0.22f, 0.02f, 0.13f, 0.54f, 0.17f);
            Tank(g, 0.20f, 0.02f, 0.13f, 0.54f, 0.17f);
            for (int x = -1; x <= 1; x += 2)
            {
                Box(g, Machinery, new Vector3(x * 0.35f, 0.21f, 0.30f), new Vector3(0.13f, 0.20f, 0.13f));
                Box(g, Accent, new Vector3(x * 0.35f, 0.25f, 0.37f), new Vector3(0.07f, 0.10f, 0.025f));
                Pipe(g, new Vector3(x * 0.22f, 0.69f, 0.02f), new Vector3(x * 0.22f, 0.76f, 0.30f), 0.025f);
            }
            Pipe(g, new Vector3(-0.22f, 0.77f, 0.30f), new Vector3(0.22f, 0.77f, 0.30f), 0.025f);
            Box(g, Armor, new Vector3(0f, 0.20f, -0.35f), new Vector3(0.22f, 0.18f, 0.12f));
        }

        static void BuildCargoDepot(Geometry[] g)
        {
            Platform(g, 1.0f, 0.94f);
            for (int x = -1; x <= 1; x += 2)
            {
                Beam(g, Accent, new Vector3(x * 0.40f, 0.12f, -0.10f), new Vector3(x * 0.40f, 0.76f, -0.10f), 0.075f);
                Beam(g, Metal, new Vector3(x * 0.40f, 0.15f, -0.10f), new Vector3(x * 0.40f, 0.15f, 0.35f), 0.045f);
            }
            Gantry(g, 0.40f, -0.10f, 0.76f, 0.075f);
            Beam(g, Accent, new Vector3(-0.40f, 0.76f, -0.10f), new Vector3(0.40f, 0.76f, -0.10f), 0.075f);
            Box(g, Metal, new Vector3(0f, 0.71f, -0.10f), new Vector3(0.14f, 0.08f, 0.12f));
            Cylinder(g, Machinery, new Vector3(0f, 0.67f, -0.10f), new Vector3(0f, 0.37f, -0.10f), 0.018f, 6);
            Box(g, Accent, new Vector3(0f, 0.31f, -0.10f), new Vector3(0.19f, 0.07f, 0.19f));
            Crate(g, new Vector3(-0.26f, 0.24f, 0.22f), new Vector3(0.27f, 0.25f, 0.23f));
            Crate(g, new Vector3(0.24f, 0.21f, 0.23f), new Vector3(0.23f, 0.20f, 0.20f), false);
            Crate(g, new Vector3(-0.24f, 0.44f, 0.18f), new Vector3(0.21f, 0.17f, 0.18f), false);
            for (int i = 0; i < 3; i++)
                Box(g, Metal, new Vector3(-0.28f + i * 0.28f, 0.14f, -0.35f),
                    new Vector3(0.22f, 0.06f, 0.16f));
        }

        static void BuildOrbitalRelay(Geometry[] g)
        {
            Box(g, Machinery, new Vector3(0f, 0.42f, 0f), new Vector3(0.33f, 0.23f, 0.32f));
            Box(g, Armor, new Vector3(0f, 0.50f, 0f), new Vector3(0.37f, 0.12f, 0.36f));
            Box(g, Accent, new Vector3(0f, 0.57f, 0f), new Vector3(0.34f, 0.035f, 0.34f));
            Cylinder(g, Metal, new Vector3(0f, 0.56f, 0f), new Vector3(0f, 0.88f, 0f), 0.025f, 8);
            Cylinder(g, Accent, new Vector3(0f, 0.72f, 0f), new Vector3(0f, 0.77f, 0f), 0.08f, 8);
            g[Metal].Dish(new Vector3(0f, 0.90f, -0.04f), 0.18f, 0.045f,
                Quaternion.LookRotation(new Vector3(0f, 0.25f, -1f), Vector3.up), 12);
            g[Metal].Ring(new Vector3(0f, 0.90f, -0.04f), 0.18f, 0.018f, 12, 4,
                Quaternion.LookRotation(new Vector3(0f, 0.25f, -1f), Vector3.up));
            Cylinder(g, Accent, new Vector3(0f, 0.88f, -0.04f), new Vector3(0f, 0.98f, -0.04f), 0.014f, 6);
            for (int side = -1; side <= 1; side += 2)
            {
                Beam(g, Metal, new Vector3(side * 0.14f, 0.46f, 0f), new Vector3(side * 0.45f, 0.46f, 0f), 0.025f);
                AddSolarWing(g, new Vector3(side * 0.57f, 0.46f, 0f), new Vector2(0.28f, 0.34f), 0f);
                Beam(g, Accent, new Vector3(side * 0.14f, 0.46f, 0f), new Vector3(side * 0.55f, 0.48f, 0f), 0.018f);
            }
        }

        static void BuildResearchLab(Geometry[] g)
        {
            Platform(g, 0.98f, 0.84f);
            Box(g, Machinery, new Vector3(0f, 0.23f, -0.13f), new Vector3(0.70f, 0.28f, 0.38f));
            Box(g, Armor, new Vector3(0f, 0.40f, -0.13f), new Vector3(0.76f, 0.10f, 0.44f));
            Box(g, Accent, new Vector3(0f, 0.46f, -0.13f), new Vector3(0.80f, 0.04f, 0.48f));
            Cylinder(g, Machinery, new Vector3(0f, 0.43f, -0.13f), new Vector3(0f, 0.52f, -0.13f), 0.25f, 12);
            g[Armor].Dome(new Vector3(0f, 0.50f, -0.13f), 0.24f, 0.31f, 12, 5);
            g[Accent].Ring(new Vector3(0f, 0.51f, -0.13f), 0.24f, 0.028f, 12, 4);
            for (int x = -1; x <= 1; x += 2)
            {
                Beam(g, Accent, new Vector3(x * 0.37f, 0.12f, 0.13f), new Vector3(x * 0.37f, 0.47f, 0.13f), 0.07f);
                Box(g, Machinery, new Vector3(x * 0.31f, 0.30f, 0.115f), new Vector3(0.09f, 0.25f, 0.025f));
            }
            Box(g, Glass, new Vector3(0f, 0.31f, 0.14f), new Vector3(0.42f, 0.12f, 0.018f));
            Cylinder(g, Metal, new Vector3(0.30f, 0.46f, 0.15f), new Vector3(0.30f, 0.79f, 0.15f), 0.015f, 6);
            g[Armor].Dish(new Vector3(0.30f, 0.78f, 0.15f), 0.11f, 0.028f,
                Quaternion.LookRotation(new Vector3(0f, 0.30f, -1f), Vector3.up), 12);
            Box(g, Metal, new Vector3(-0.31f, 0.18f, 0.19f), new Vector3(0.19f, 0.16f, 0.13f));
        }

        static void BuildRadar(Geometry[] g)
        {
            Platform(g, 0.82f, 0.82f);
            for (int x = -1; x <= 1; x += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 foot = new Vector3(x * 0.28f, 0.13f, z * 0.27f);
                    Vector3 top = new Vector3(x * 0.09f, 0.98f, z * 0.09f);
                    Beam(g, Metal, foot, top, 0.035f);
                    Beam(g, Accent, foot, new Vector3(-x * 0.09f, 0.44f, -z * 0.09f), 0.022f);
                }
            }
            for (int i = 0; i < 4; i++)
            {
                float y = 0.29f + i * 0.16f;
                Box(g, Machinery, new Vector3(0f, y, 0f), new Vector3(0.40f - i * 0.06f, 0.035f, 0.40f - i * 0.06f));
            }
            Cylinder(g, Accent, new Vector3(0f, 0.90f, 0f), new Vector3(0f, 1.04f, 0f), 0.10f, 8);
            Cylinder(g, Metal, new Vector3(0f, 1.02f, 0f), new Vector3(0f, 1.28f, 0f), 0.017f, 8);
            Vector3 dish = new Vector3(0f, 1.27f, 0f);
            Quaternion aim = Quaternion.LookRotation(new Vector3(0.05f, 0.25f, -1f), Vector3.up);
            g[Armor].Dish(dish, 0.22f, 0.055f, aim, 12);
            g[Metal].Ring(dish, 0.22f, 0.02f, 12, 5, aim);
            Cylinder(g, Accent, dish + aim * new Vector3(0f, 0f, -0.05f),
                dish + aim * new Vector3(0f, 0f, 0.05f), 0.018f, 6);
        }

        static void BuildHangar(Geometry[] g)
        {
            Platform(g, 0.98f, 0.90f);
            for (int x = -1; x <= 1; x += 2)
            {
                Beam(g, Accent, new Vector3(x * 0.40f, 0.12f, -0.05f), new Vector3(x * 0.40f, 0.68f, -0.05f), 0.085f);
                Beam(g, Metal, new Vector3(x * 0.40f, 0.68f, -0.05f), new Vector3(x * 0.30f, 0.78f, -0.05f), 0.08f);
            }
            Beam(g, Accent, new Vector3(-0.30f, 0.78f, -0.05f), new Vector3(0.30f, 0.78f, -0.05f), 0.09f);
            Box(g, Armor, new Vector3(0f, 0.71f, -0.05f), new Vector3(0.70f, 0.07f, 0.66f));
            Box(g, Accent, new Vector3(0f, 0.76f, -0.05f), new Vector3(0.74f, 0.035f, 0.70f));
            Box(g, Machinery, new Vector3(0f, 0.34f, 0.30f), new Vector3(0.66f, 0.42f, 0.025f));
            Box(g, Machinery, new Vector3(0f, 0.33f, 0.315f), new Vector3(0.56f, 0.35f, 0.02f));
            Box(g, Accent, new Vector3(-0.29f, 0.34f, 0.34f), new Vector3(0.035f, 0.38f, 0.03f));
            Box(g, Accent, new Vector3(0.29f, 0.34f, 0.34f), new Vector3(0.035f, 0.38f, 0.03f));
            for (int i = 0; i < 3; i++)
                Box(g, Metal, new Vector3(-0.23f + i * 0.23f, 0.13f, 0.48f), new Vector3(0.17f, 0.012f, 0.12f));
            for (int x = -1; x <= 1; x += 2)
                Crate(g, new Vector3(x * 0.31f, 0.22f, -0.28f), new Vector3(0.17f, 0.20f, 0.18f), false);
        }

        static void BuildAutoHarvester(Geometry[] g)
        {
            Platform(g, 0.90f, 0.82f);
            Box(g, Machinery, new Vector3(0f, 0.19f, -0.02f), new Vector3(0.58f, 0.18f, 0.44f));
            Crate(g, new Vector3(-0.08f, 0.39f, -0.03f), new Vector3(0.40f, 0.32f, 0.36f));
            Box(g, Glass, new Vector3(0f, 0.43f, 0.16f), new Vector3(0.23f, 0.10f, 0.025f));
            for (int x = -1; x <= 1; x += 2)
            {
                Cylinder(g, Machinery, new Vector3(x * 0.30f, 0.15f, -0.22f), new Vector3(x * 0.30f, 0.15f, 0.22f), 0.10f, 8);
                for (int i = 0; i < 3; i++)
                    Box(g, Metal, new Vector3(x * 0.30f, 0.15f, -0.15f + i * 0.15f), new Vector3(0.025f, 0.10f, 0.035f));
            }
            Arm(g, new Vector3(0.18f, 0.47f, 0.10f), new Vector3(0.35f, 0.73f, 0.18f),
                new Vector3(0.48f, 0.55f, 0.28f), 0.065f);
            Cylinder(g, Machinery, new Vector3(0.46f, 0.49f, 0.27f), new Vector3(0.54f, 0.60f, 0.32f), 0.06f, 6, 0.015f);
            Cylinder(g, Accent, new Vector3(-0.10f, 0.55f, -0.02f), new Vector3(-0.10f, 0.67f, -0.02f), 0.09f, 8);
        }

        static void BuildAlarm(Geometry[] g)
        {
            Platform(g, 0.78f, 0.78f);
            Cylinder(g, Machinery, new Vector3(0f, 0.11f, 0f), new Vector3(0f, 0.22f, 0f), 0.26f, 10);
            Cylinder(g, Accent, new Vector3(0f, 0.20f, 0f), new Vector3(0f, 0.25f, 0f), 0.28f, 10);
            for (int i = 0; i < 3; i++)
            {
                float a = i * Mathf.PI * 2f / 3f;
                Vector3 foot = new Vector3(Mathf.Cos(a) * 0.24f, 0.20f, Mathf.Sin(a) * 0.24f);
                Vector3 top = new Vector3(0f, 1.02f, 0f);
                Beam(g, Metal, foot, top, 0.036f);
                Cylinder(g, Accent, foot - Vector3.up * 0.04f, foot + Vector3.up * 0.02f, 0.045f, 6);
            }
            Cylinder(g, Machinery, new Vector3(0f, 0.97f, 0f), new Vector3(0f, 1.11f, 0f), 0.14f, 8);
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f;
                Vector3 p = new Vector3(Mathf.Cos(a) * 0.17f, 1.06f, Mathf.Sin(a) * 0.17f);
                Cylinder(g, Accent, p, p + new Vector3(Mathf.Cos(a) * 0.07f, 0f, Mathf.Sin(a) * 0.07f), 0.055f, 8, 0.035f);
            }
            g[Emissive].Sphere(new Vector3(0f, 1.18f, 0f), 0.075f, 8, 4);
            Cylinder(g, Metal, new Vector3(0f, 1.12f, 0f), new Vector3(0f, 1.28f, 0f), 0.025f, 6);
        }

        static void BuildShieldPylon(Geometry[] g)
        {
            Platform(g, 0.88f, 0.84f);
            Cylinder(g, Machinery, new Vector3(0f, 0.11f, 0f), new Vector3(0f, 0.20f, 0f), 0.29f, 10);
            for (int i = 0; i < 3; i++)
            {
                float a = i * Mathf.PI * 2f / 3f - Mathf.PI * 0.5f;
                Vector3 foot = new Vector3(Mathf.Cos(a) * 0.29f, 0.17f, Mathf.Sin(a) * 0.29f);
                Vector3 crown = new Vector3(Mathf.Cos(a) * 0.13f, 0.98f, Mathf.Sin(a) * 0.13f);
                Beam(g, Armor, foot, crown, 0.11f, 0.085f);
                Beam(g, Accent, foot + Vector3.up * 0.13f, crown - Vector3.up * 0.10f, 0.045f);
                Box(g, Accent, foot, new Vector3(0.13f, 0.13f, 0.13f));
                Cylinder(g, Machinery, crown, crown + Vector3.up * 0.15f, 0.045f, 8);
            }
            Cylinder(g, Machinery, new Vector3(0f, 0.23f, 0f), new Vector3(0f, 0.72f, 0f), 0.09f, 8);
            Cylinder(g, Accent, new Vector3(0f, 0.68f, 0f), new Vector3(0f, 0.75f, 0f), 0.12f, 8);
            g[Emissive].Sphere(new Vector3(0f, 0.83f, 0f), 0.10f, 8, 4);
            for (int i = 0; i < 3; i++)
            {
                float a = i * Mathf.PI * 2f / 3f;
                Vector3 p = new Vector3(Mathf.Cos(a) * 0.20f, 0.60f, Mathf.Sin(a) * 0.20f);
                Beam(g, Metal, new Vector3(0f, 0.65f, 0f), p, 0.02f);
            }
        }

        static void BuildMissileSilo(Geometry[] g)
        {
            Platform(g, 0.96f, 0.86f);
            Box(g, Armor, new Vector3(0f, 0.18f, 0f), new Vector3(0.76f, 0.16f, 0.62f));
            for (int x = -1; x <= 1; x += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 p = new Vector3(x * 0.22f, 0.48f, z * 0.18f);
                    Box(g, Accent, p, new Vector3(0.20f, 0.56f, 0.17f), Quaternion.Euler(x * 7f, 0f, -x * 7f));
                    Cylinder(g, Machinery, p + Vector3.up * 0.26f, p + Vector3.up * 0.30f, 0.072f, 8);
                    Cylinder(g, Metal, p + Vector3.up * 0.28f, p + Vector3.up * 0.32f, 0.048f, 8);
                    Cylinder(g, Emissive, p + Vector3.up * 0.23f, p + Vector3.up * 0.25f, 0.024f, 8);
                }
            }
            for (int x = -1; x <= 1; x += 2)
                Beam(g, Metal, new Vector3(x * 0.36f, 0.18f, -0.27f), new Vector3(x * 0.36f, 0.74f, -0.27f), 0.035f);
            Box(g, Armor, new Vector3(0f, 0.76f, 0f), new Vector3(0.65f, 0.07f, 0.55f));
            for (int i = 0; i < 4; i++)
                Cylinder(g, Machinery, new Vector3(-0.22f + (i % 2) * 0.44f, 0.81f, -0.18f + (i / 2) * 0.36f),
                    new Vector3(-0.22f + (i % 2) * 0.44f, 0.83f, -0.18f + (i / 2) * 0.36f), 0.065f, 8);
        }

        static void BuildTesla(Geometry[] g)
        {
            Platform(g, 0.84f, 0.84f);
            Cylinder(g, Machinery, new Vector3(0f, 0.11f, 0f), new Vector3(0f, 0.27f, 0f), 0.29f, 12);
            Cylinder(g, Armor, new Vector3(0f, 0.24f, 0f), new Vector3(0f, 0.39f, 0f), 0.18f, 10);
            for (int i = 0; i < 6; i++)
            {
                float y = 0.36f + i * 0.09f;
                float r = 0.17f - i * 0.012f;
                g[Accent].Ring(new Vector3(0f, y, 0f), r, 0.025f, 12, 4, Quaternion.Euler(90f, 0f, 0f));
            }
            Cylinder(g, Machinery, new Vector3(0f, 0.36f, 0f), new Vector3(0f, 0.88f, 0f), 0.055f, 8, 0.025f);
            g[Emissive].Sphere(new Vector3(0f, 0.96f, 0f), 0.11f, 8, 4);
            Cylinder(g, Metal, new Vector3(0f, 0.84f, 0f), new Vector3(0f, 0.89f, 0f), 0.12f, 8);
            for (int i = 0; i < 3; i++)
            {
                float a = i * Mathf.PI * 2f / 3f;
                Vector3 p = new Vector3(Mathf.Cos(a) * 0.23f, 0.25f, Mathf.Sin(a) * 0.23f);
                Beam(g, Metal, p, new Vector3(0f, 0.57f, 0f), 0.022f);
            }
        }

        static void BuildRepairBay(Geometry[] g)
        {
            Platform(g, 0.98f, 0.86f);
            Box(g, Machinery, new Vector3(0f, 0.16f, 0f), new Vector3(0.58f, 0.06f, 0.42f));
            Box(g, Metal, new Vector3(0f, 0.20f, 0.05f), new Vector3(0.42f, 0.025f, 0.28f));
            for (int x = -1; x <= 1; x += 2)
            {
                Beam(g, Accent, new Vector3(x * 0.38f, 0.13f, 0.08f), new Vector3(x * 0.38f, 0.70f, 0.08f), 0.07f);
                Beam(g, Metal, new Vector3(x * 0.38f, 0.70f, 0.08f), new Vector3(x * 0.30f, 0.79f, 0.08f), 0.065f);
            }
            Beam(g, Accent, new Vector3(-0.30f, 0.79f, 0.08f), new Vector3(0.30f, 0.79f, 0.08f), 0.07f);
            Box(g, Armor, new Vector3(0f, 0.75f, 0.08f), new Vector3(0.62f, 0.08f, 0.46f));
            for (int x = -1; x <= 1; x += 2)
            {
                Arm(g, new Vector3(x * 0.30f, 0.62f, 0.15f), new Vector3(x * 0.22f, 0.48f, 0.27f),
                    new Vector3(x * 0.18f, 0.34f, 0.28f), 0.045f);
                Box(g, Machinery, new Vector3(x * 0.37f, 0.23f, -0.25f), new Vector3(0.14f, 0.20f, 0.12f));
            }
            Box(g, Accent, new Vector3(0f, 0.20f, 0.34f), new Vector3(0.46f, 0.025f, 0.12f));
            for (int i = 0; i < 3; i++)
                Box(g, Metal, new Vector3(-0.19f + i * 0.19f, 0.145f, 0.34f), new Vector3(0.025f, 0.012f, 0.08f));
        }

        static void BuildTerraformer(Geometry[] g)
        {
            Platform(g, 0.98f, 0.88f);
            Cylinder(g, Machinery, new Vector3(0f, 0.11f, 0f), new Vector3(0f, 0.31f, 0f), 0.31f, 12);
            Cylinder(g, Armor, new Vector3(0f, 0.28f, 0f), new Vector3(0f, 0.53f, 0f), 0.25f, 10, 0.16f);
            g[Glass].Dome(new Vector3(0f, 0.52f, 0f), 0.16f, 0.18f, 10, 4);
            Cylinder(g, Accent, new Vector3(0f, 0.49f, 0f), new Vector3(0f, 0.55f, 0f), 0.26f, 10);
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f + Mathf.PI * 0.25f;
                float x = Mathf.Cos(a), z = Mathf.Sin(a);
                Vector3 foot = new Vector3(x * 0.36f, 0.15f, z * 0.34f);
                Vector3 top = new Vector3(x * 0.24f, 0.86f, z * 0.22f);
                Beam(g, Accent, foot, top, 0.065f);
                Tank(g, x * 0.28f, z * 0.25f, 0.20f, 0.36f, 0.065f, true);
                Beam(g, Metal, foot, new Vector3(-x * 0.18f, 0.50f, -z * 0.16f), 0.022f);
            }
            Cylinder(g, Machinery, new Vector3(0f, 0.52f, 0f), new Vector3(0f, 0.89f, 0f), 0.07f, 8);
            Cylinder(g, Metal, new Vector3(0f, 0.85f, 0f), new Vector3(0f, 1.04f, 0f), 0.04f, 8, 0.018f);
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f;
                Vector3 p = new Vector3(Mathf.Cos(a) * 0.31f, 0.62f, Mathf.Sin(a) * 0.29f);
                Beam(g, Metal, new Vector3(0f, 0.68f, 0f), p, 0.022f);
            }
        }

        static void BuildWarpGate(Geometry[] g)
        {
            Vector3 center = new Vector3(0f, 0.62f, 0f);
            g[Metal].Ring(center, 0.43f, 0.08f, 32, 8);
            g[Armor].Ring(center, 0.34f, 0.035f, 28, 6);
            g[Emissive].Ring(center, 0.27f, 0.022f, 28, 6);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * 0.25f;
                Vector3 p = center + new Vector3(Mathf.Cos(a) * 0.42f, Mathf.Sin(a) * 0.42f, 0f);
                Box(g, Accent, p, new Vector3(0.13f, 0.12f, 0.15f), Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg));
                Box(g, Machinery, p + new Vector3(0f, 0f, 0.10f), new Vector3(0.065f, 0.06f, 0.06f),
                    Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg));
            }
            for (int x = -1; x <= 1; x += 2)
            {
                Beam(g, Accent, new Vector3(x * 0.30f, 0.15f, 0f), new Vector3(x * 0.20f, 0.48f, 0f), 0.09f);
                Box(g, Machinery, new Vector3(x * 0.30f, 0.13f, 0f), new Vector3(0.18f, 0.20f, 0.18f));
                Beam(g, Metal, new Vector3(x * 0.30f, 0.23f, -0.02f), new Vector3(-x * 0.20f, 0.47f, -0.02f), 0.04f);
            }
            Beam(g, Accent, new Vector3(-0.20f, 0.42f, 0f), new Vector3(0.20f, 0.42f, 0f), 0.075f);
            g[Emissive].Sphere(new Vector3(0f, 0.62f, 0.03f), 0.08f, 8, 4);
        }

        static void BuildDyson(Geometry[] g)
        {
            g[Machinery].Sphere(new Vector3(0f, 0.58f, 0f), 0.17f, 10, 5);
            g[Armor].Sphere(new Vector3(0f, 0.58f, 0f), 0.13f, 10, 5);
            g[Accent].Ring(new Vector3(0f, 0.58f, 0f), 0.18f, 0.025f, 16, 5);
            g[Emissive].Sphere(new Vector3(0f, 0.58f, 0.04f), 0.065f, 8, 4);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * 0.25f;
                Vector3 radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 wingCenter = new Vector3(radial.x * 0.48f, 0.58f, radial.z * 0.48f);
                Beam(g, Metal, radial * 0.13f + new Vector3(0f, 0.58f, 0f),
                    radial * 0.34f + new Vector3(0f, 0.58f, 0f), 0.032f);
                Box(g, Machinery, wingCenter, new Vector3(0.29f, 0.025f, 0.16f),
                    Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f));
                Box(g, Glass, wingCenter + new Vector3(0f, 0.018f, 0f), new Vector3(0.23f, 0.008f, 0.11f),
                    Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f));
                Cylinder(g, Accent, radial * 0.30f + new Vector3(0f, 0.58f, 0f),
                    radial * 0.37f + new Vector3(0f, 0.58f, 0f), 0.028f, 6);
            }
            g[Metal].Ring(new Vector3(0f, 0.58f, 0f), 0.28f, 0.018f, 20, 4, Quaternion.Euler(90f, 0f, 0f));
            g[Metal].Ring(new Vector3(0f, 0.58f, 0f), 0.28f, 0.018f, 20, 4, Quaternion.identity);
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
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.24f);
            EditorUtility.SetDirty(material);
            return material;
        }

        static Material SaveGlass()
        {
            Material material = SaveMaterial("Structure_Glass", new Color(0.22f, 0.58f, 0.66f, 0.48f), 0.08f);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.renderQueue = (int)RenderQueue.Transparent;
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.SetShaderPassEnabled("DepthOnly", false);
            material.SetShaderPassEnabled("ShadowCaster", false);
            EditorUtility.SetDirty(material);
            return material;
        }

        static Material SaveEmissive()
        {
            Material material = SaveMaterial("Structure_Emissive", new Color(0.14f, 0.72f, 0.82f, 1f), 0f);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", new Color(0.08f, 0.42f, 0.55f, 1f));
            EditorUtility.SetDirty(material);
            return material;
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
