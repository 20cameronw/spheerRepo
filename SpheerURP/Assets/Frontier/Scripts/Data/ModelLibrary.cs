using System;
using UnityEngine;

namespace Frontier
{
    /// <summary>Maps planet ids, alien ids and "turret" to hand-made models. Edit the asset in Resources/Frontier to swap them.</summary>
    [CreateAssetMenu(fileName = "ModelLibrary", menuName = "Frontier/Model Library")]
    public class ModelLibrary : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string key;
            public GameObject prefab;
            [Tooltip("Measured source-to-reference scale correction, baked by Frontier/Calibrate Model Scales.")]
            public float scale;
            public float referenceDiameter;
            [Tooltip("Where structures sit, as a fraction of the standard ground radius (0 = not baked, treated as 1). Round models have 1; models with " +
                     "spikes or a smaller core have less, so buildings touch the visible body instead of floating. Baked by Frontier/Bake Model Ground Radii.")]
            public float groundScale;
            [Tooltip("Authored foundation depth below the original ground plane. Placement must leave this portion buried.")]
            public float foundationDepth;
        }

        public Entry[] entries;

        static ModelLibrary cached;
        static bool loaded;

        public static ModelLibrary Instance
        {
            get
            {
                if (!loaded) { cached = Resources.Load<ModelLibrary>("Frontier/ModelLibrary"); loaded = true; }
                return cached;
            }
        }

        /// <summary>Ground radius of a planet model as a fraction of the standard ground radius: 1 for round models, less when the visible body is smaller than the model's bounds.</summary>
        public static float GroundScale(string key)
        {
            var lib = Instance;
            if (lib == null || lib.entries == null) return 1f;
            foreach (var e in lib.entries) if (e.key == key) return e.groundScale > 0.01f ? Mathf.Min(1f, e.groundScale) : 1f;
            return 1f;
        }

        public static float FoundationDepth(string key)
        {
            var lib = Instance;
            if (lib != null && lib.entries != null)
                foreach (var entry in lib.entries)
                    if (entry.key == key) return entry.foundationDepth;
            return 0f;
        }

        /// <summary>Instantiates an inert model using its baked scale correction and the requested diameter.</summary>
        public static Transform Spawn(string key, Transform parent, float diameter, bool useEntryScale = true)
        {
            var lib = Instance;
            if (lib == null || lib.entries == null) return null;
            foreach (var e in lib.entries)
            {
                if (e.key != key || e.prefab == null) continue;
                var go = InstantiateInert(e.prefab);
                go.name = key + "_model";
                // Fit in an unrotated, unparented pose so bounds are true (no rotated-AABB inflation).
                go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                go.transform.localScale = Vector3.one;
                Bounds b;
                if (!MeshBounds(go, out b)) { Destroy(go); return null; }
                float size = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                if (size < 1e-4f) size = 1f;
                float s = e.referenceDiameter > 0f
                    ? e.scale * diameter / e.referenceDiameter
                    : diameter / size * (useEntryScale && e.scale > 0f ? e.scale : 1f);
                go.transform.localScale = Vector3.one * s;
                go.transform.position = -b.center * s;
                go.transform.SetParent(parent, false);
                return go.transform;
            }
            return null;
        }

        public static GameObject Prefab(string key)
        {
            var lib = Instance;
            if (lib == null || lib.entries == null) return null;
            foreach (var e in lib.entries) if (e.key == key && e.prefab != null) return e.prefab;
            return null;
        }

        /// <summary>Instantiates a prefab without running its scripts (spawned under an inactive parent), then strips it.</summary>
        public static GameObject InstantiateInert(GameObject prefab)
        {
            var parking = new GameObject("Inert");
            parking.SetActive(false);
            var go = Instantiate(prefab, parking.transform, false);
            Strip(go);
            go.transform.SetParent(null, false);
            DestroyImmediate(parking);
            return go;
        }

        /// <summary>Removes gameplay scripts, colliders and baked particle effects from a spawned prefab.</summary>
        public static void Strip(GameObject go)
        {
            foreach (var b in go.GetComponentsInChildren<MonoBehaviour>(true)) DestroyImmediate(b);
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);
            foreach (var p in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (p.gameObject != go && p.transform.childCount == 0) { DestroyImmediate(p.gameObject); continue; }
                var pr = p.GetComponent<ParticleSystemRenderer>();
                if (pr != null) DestroyImmediate(pr);
                if (p != null) DestroyImmediate(p);
            }
        }

        /// <summary>Bounds of mesh renderers only, in world space.</summary>
        public static bool MeshBounds(GameObject go, out Bounds b)
        {
            b = new Bounds();
            bool any = false;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
                if (r is MeshRenderer) { var mf = r.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue; }
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            return any;
        }
    }
}
