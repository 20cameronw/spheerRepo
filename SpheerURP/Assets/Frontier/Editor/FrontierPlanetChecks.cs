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
                int count = 0;
                foreach (var planet in GameDatabase.Load().planets)
                {
                    var model = ModelLibrary.Spawn(planet.id, holder.transform, 3f, false);
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
                var route = typeof(UIRoot).GetMethod("RoutePoint", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                var point = typeof(UIRoot).GetMethod("MapPoint", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                var planets = GameDatabase.Load().planets;
                foreach (var from in planets)
                    foreach (var to in planets)
                    {
                        if (from == to) continue;
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
