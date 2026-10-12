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
            [Tooltip("Extra multiplier on top of the auto-fit size.")]
            public float scale;
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

        /// <summary>Instantiates the model for a key, strips behaviours, and fits it to the given diameter. Returns null if none.</summary>
        public static Transform Spawn(string key, Transform parent, float diameter)
        {
            var lib = Instance;
            if (lib == null || lib.entries == null) return null;
            foreach (var e in lib.entries)
            {
                if (e.key != key || e.prefab == null) continue;
                var go = Instantiate(e.prefab, parent, false);
                go.name = key + "_model";
                foreach (var b in go.GetComponentsInChildren<MonoBehaviour>(true)) Destroy(b);
                foreach (var c in go.GetComponentsInChildren<Collider>(true)) Destroy(c);

                var rends = go.GetComponentsInChildren<Renderer>();
                if (rends.Length == 0) { Destroy(go); return null; }
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
                var b2 = rends[0].bounds;
                foreach (var r in rends) b2.Encapsulate(r.bounds);
                float size = Mathf.Max(b2.size.x, Mathf.Max(b2.size.y, b2.size.z));
                if (size < 1e-4f) size = 1f;
                float s = diameter / size * (e.scale > 0f ? e.scale : 1f);
                go.transform.localScale = Vector3.one * s;
                var b3 = rends[0].bounds;
                foreach (var r in rends) b3.Encapsulate(r.bounds);
                go.transform.position += parent.position - b3.center;
                return go.transform;
            }
            return null;
        }
    }
}
