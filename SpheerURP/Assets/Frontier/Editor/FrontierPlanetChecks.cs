#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace Frontier.EditorTools
{
    public static class FrontierPlanetChecks
    {
        [MenuItem("Frontier/Validate Planet Model Sizes")]
        public static void Run()
        {
            var holder = new GameObject("PlanetSizeChecks");
            try
            {
                var sources = new System.Collections.Generic.HashSet<string>();
                foreach (var entry in ModelLibrary.Instance.entries)
                    foreach (var path in AssetDatabase.GetDependencies(AssetDatabase.GetAssetPath(entry.prefab), true))
                        if (AssetImporter.GetAtPath(path) is ModelImporter) sources.Add(path);
                foreach (var path in sources)
                {
                    var imported = ModelLibrary.InstantiateInert(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                    imported.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                    imported.transform.localScale = Vector3.one;
                    Bounds importedBounds;
                    if (!ModelLibrary.MeshBounds(imported, out importedBounds))
                        throw new InvalidOperationException("No source mesh: " + path);
                    float importedDiameter = Mathf.Max(importedBounds.size.x, Mathf.Max(importedBounds.size.y, importedBounds.size.z));
                    UnityEngine.Object.DestroyImmediate(imported);
                    if (Mathf.Abs(importedDiameter - 1f) > 0.001f)
                        throw new InvalidOperationException("Source import is not standardized: " + path);
                }
                Debug.Log("Frontier: source import units passed (" + sources.Count + " models).");
                int count = 0;
                foreach (var planet in GameDatabase.Load().planets)
                {
                    var model = ModelLibrary.Spawn(planet.ModelKey, holder.transform, 3f, false);
                    if (model == null) throw new InvalidOperationException("Missing planet model: " + planet.id);
                    Bounds bounds;
                    if (!ModelLibrary.MeshBounds(model.gameObject, out bounds))
                        throw new InvalidOperationException("No planet mesh: " + planet.id);
                    float diameter = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
                    if (Mathf.Abs(diameter - 3f) > 0.01f)
                        throw new InvalidOperationException(planet.id + " diameter is " + diameter + ", expected 3.");
                    UnityEngine.Object.DestroyImmediate(model.gameObject);
                    count++;
                }
                Debug.Log("Frontier: planet size checks passed (" + count + " models, diameter 3).");
                foreach (var entry in ModelLibrary.Instance.entries)
                {
                    if (!entry.key.StartsWith("struct_", StringComparison.Ordinal)) continue;
                    var model = ModelLibrary.Spawn(entry.key, holder.transform, 0.75f);
                    Bounds bounds;
                    if (model == null || !ModelLibrary.MeshBounds(model.gameObject, out bounds))
                        throw new InvalidOperationException("Missing structure mesh: " + entry.key);
                    float diameter = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
                    if (Mathf.Abs(diameter - 0.75f) > 0.01f)
                        throw new InvalidOperationException(entry.key + " structure diameter is " + diameter);
                    UnityEngine.Object.DestroyImmediate(model.gameObject);
                }
                var db = GameDatabase.Load();
                int slots = 0;
                var originalSlots = WorldSurface.SlotDirections(12);
                foreach (var planet in db.planets)
                {
                    if (planet.buildingSlots < 12 || planet.worldSize < 1f)
                        throw new InvalidOperationException("Incorrect planet capacity: " + planet.id);
                    var directions = WorldSurface.SlotDirections(planet.BuildingSlots);
                    if (directions.Length != planet.BuildingSlots) throw new InvalidOperationException("Incorrect slot count.");
                    for (int i = 0; i < directions.Length; i++)
                    {
                        if (Mathf.Abs(directions[i].magnitude - 1f) > 0.001f) throw new InvalidOperationException("Slot off sphere.");
                        if (i < 12 && Vector3.Distance(directions[i], originalSlots[i]) > 0.001f)
                            throw new InvalidOperationException("Saved slot moved.");
                        for (int j = 0; j < i; j++)
                            if (Vector3.Distance(directions[i], directions[j]) < 0.35f)
                                throw new InvalidOperationException("Planet slots overlap.");
                    }
                    var structure = ModelLibrary.Spawn("struct_drill", holder.transform, 0.75f);
                    structure.localScale *= planet.StructureScale;
                    Bounds structureBounds;
                    ModelLibrary.MeshBounds(structure.gameObject, out structureBounds);
                    float structureDiameter = Mathf.Max(structureBounds.size.x, Mathf.Max(structureBounds.size.y, structureBounds.size.z));
                    if (Mathf.Abs(structureDiameter - 0.75f / planet.worldSize) > 0.001f)
                        throw new InvalidOperationException("Incorrect relative structure size: " + planet.id);
                    UnityEngine.Object.DestroyImmediate(structure.gameObject);
                    slots += planet.BuildingSlots;
                }
                Debug.Log("Frontier: structure diameters and tier capacities passed (" + slots + " total planet slots).");
                var route = typeof(UIRoot).GetMethod("RoutePoint", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                var point = typeof(UIRoot).GetMethod("MapPoint", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                var universe = UniverseService.For(GameDatabase.Load());
                var planets = new System.Collections.Generic.List<PlanetData>(universe.AuthoredPlanets);
                // a few generated worlds are only ever shown as a marker, but routes to and from them must still be well formed
                planets.Add(universe.FindGenerated(Frontier.Universe.Biome.Lush, null));
                planets.Add(universe.FindGenerated(Frontier.Universe.Biome.GasGiant, Frontier.Universe.SizeClass.Colossal, 6));
                foreach (var from in planets)
                    foreach (var to in planets)
                    {
                        if (from.Id == to.Id) continue;
                        Vector2 start = (Vector2)route.Invoke(null, new object[] { from, to, 0f });
                        Vector2 end = (Vector2)route.Invoke(null, new object[] { from, to, 1f });
                        if (Vector2.Distance(start, (Vector2)point.Invoke(null, new object[] { from })) > 0.01f ||
                            Vector2.Distance(end, (Vector2)point.Invoke(null, new object[] { to })) > 0.01f)
                            throw new InvalidOperationException("Route endpoints do not match planet positions.");
                        foreach (float join in new[] { 0.15f, 0.85f })
                        {
                            Vector2 before = (Vector2)route.Invoke(null, new object[] { from, to, join - 0.00001f });
                            Vector2 after = (Vector2)route.Invoke(null, new object[] { from, to, join + 0.00001f });
                            if (Vector2.Distance(before, after) > 1f)
                                throw new InvalidOperationException("Route discontinuity.");
                        }
                    }
                Debug.Log("Frontier: all map route endpoints and orbital joins passed.");
            }
            finally { UnityEngine.Object.DestroyImmediate(holder); }
        }
    }
}
#endif
