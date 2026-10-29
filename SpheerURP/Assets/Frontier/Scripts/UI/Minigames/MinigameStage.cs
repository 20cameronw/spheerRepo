using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Frontier
{
    /// <summary>Procedural low-poly meshes (flat shaded) used by the 3D mini-games.</summary>
    public static class MeshKit
    {
        class Builder
        {
            public readonly List<Vector3> v = new List<Vector3>();
            public readonly List<int> t = new List<int>();

            public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 outward)
            {
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0f) { Vector3 s = b; b = c; c = s; }
                int i = v.Count;
                v.Add(a); v.Add(b); v.Add(c);
                t.Add(i); t.Add(i + 1); t.Add(i + 2);
            }

            public Mesh ToMesh()
            {
                var m = new Mesh();
                m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                m.SetVertices(v);
                m.SetTriangles(t, 0);
                m.RecalculateNormals();
                m.RecalculateBounds();
                return m;
            }
        }

        /// <summary>Flat-shaded icosphere whose radius is driven by a function of the direction. Optionally returns surface points.</summary>
        public static Mesh Ico(int sub, Func<Vector3, float> radius, List<Vector3> surface = null)
        {
            float k = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var v = new List<Vector3>
            {
                new Vector3(-1, k, 0), new Vector3(1, k, 0), new Vector3(-1, -k, 0), new Vector3(1, -k, 0),
                new Vector3(0, -1, k), new Vector3(0, 1, k), new Vector3(0, -1, -k), new Vector3(0, 1, -k),
                new Vector3(k, 0, -1), new Vector3(k, 0, 1), new Vector3(-k, 0, -1), new Vector3(-k, 0, 1)
            };
            for (int i = 0; i < v.Count; i++) v[i] = v[i].normalized;
            var f = new List<int[]>
            {
                new[] {0,11,5}, new[] {0,5,1}, new[] {0,1,7}, new[] {0,7,10}, new[] {0,10,11},
                new[] {1,5,9}, new[] {5,11,4}, new[] {11,10,2}, new[] {10,7,6}, new[] {7,1,8},
                new[] {3,9,4}, new[] {3,4,2}, new[] {3,2,6}, new[] {3,6,8}, new[] {3,8,9},
                new[] {4,9,5}, new[] {2,4,11}, new[] {6,2,10}, new[] {8,6,7}, new[] {9,8,1}
            };
            for (int s = 0; s < sub; s++)
            {
                var cache = new Dictionary<long, int>();
                var nf = new List<int[]>();
                Func<int, int, int> mid = (a, b) =>
                {
                    long key = a < b ? ((long)a << 32) + b : ((long)b << 32) + a;
                    int idx;
                    if (cache.TryGetValue(key, out idx)) return idx;
                    v.Add(((v[a] + v[b]) * 0.5f).normalized);
                    cache[key] = v.Count - 1;
                    return v.Count - 1;
                };
                foreach (var tri in f)
                {
                    int a = tri[0], b = tri[1], c = tri[2];
                    int ab = mid(a, b), bc = mid(b, c), ca = mid(c, a);
                    nf.Add(new[] { a, ab, ca }); nf.Add(new[] { b, bc, ab });
                    nf.Add(new[] { c, ca, bc }); nf.Add(new[] { ab, bc, ca });
                }
                f = nf;
            }
            var p = new Vector3[v.Count];
            for (int i = 0; i < p.Length; i++) p[i] = v[i] * radius(v[i]);
            if (surface != null) surface.AddRange(p);
            var bd = new Builder();
            foreach (var tri in f)
            {
                Vector3 a = p[tri[0]], b = p[tri[1]], c = p[tri[2]];
                bd.Tri(a, b, c, (a + b + c) / 3f);
            }
            return bd.ToMesh();
        }

        /// <summary>Flat-shaded tapered cylinder from y=0 (radius r0) to y=h (radius r1).</summary>
        public static Mesh Cyl(float r0, float r1, float h, int sides)
        {
            var bd = new Builder();
            for (int i = 0; i < sides; i++)
            {
                float a0 = i / (float)sides * Mathf.PI * 2f, a1 = (i + 1) / (float)sides * Mathf.PI * 2f;
                Vector3 b0 = new Vector3(Mathf.Cos(a0) * r0, 0f, Mathf.Sin(a0) * r0);
                Vector3 b1 = new Vector3(Mathf.Cos(a1) * r0, 0f, Mathf.Sin(a1) * r0);
                Vector3 t0 = new Vector3(Mathf.Cos(a0) * r1, h, Mathf.Sin(a0) * r1);
                Vector3 t1 = new Vector3(Mathf.Cos(a1) * r1, h, Mathf.Sin(a1) * r1);
                float am = (a0 + a1) * 0.5f;
                Vector3 outward = new Vector3(Mathf.Cos(am), 0f, Mathf.Sin(am));
                bd.Tri(b0, t0, t1, outward);
                bd.Tri(b0, t1, b1, outward);
                bd.Tri(Vector3.zero, b0, b1, Vector3.down);
                bd.Tri(new Vector3(0f, h, 0f), t0, t1, Vector3.up);
            }
            return bd.ToMesh();
        }

        public static Mesh Shard(int seed)
        {
            var bd = new Builder();
            float phase = seed * 0.37f;
            Vector3[] lower = new Vector3[5];
            Vector3[] upper = new Vector3[5];
            for (int i = 0; i < 5; i++)
            {
                float a = i * Mathf.PI * 2f / 5f + phase;
                float r = 0.28f + 0.06f * Mathf.Sin(i * 2.7f + phase);
                lower[i] = new Vector3(Mathf.Cos(a) * r, -0.18f + 0.04f * Mathf.Sin(i + phase), Mathf.Sin(a) * r);
                upper[i] = new Vector3(Mathf.Cos(a + 0.14f) * r * 0.72f,
                    0.07f + 0.12f * Mathf.Sin(i * 1.9f + phase), Mathf.Sin(a + 0.14f) * r * 0.72f);
            }
            Vector3 bottom = new Vector3(0f, -0.22f, 0f);
            Vector3 crown = new Vector3(0.06f * Mathf.Sin(phase), 0.24f, -0.04f * Mathf.Cos(phase));
            for (int i = 0; i < 5; i++)
            {
                int next = (i + 1) % 5;
                bd.Tri(bottom, lower[next], lower[i], Vector3.down);
                bd.Tri(lower[i], lower[next], upper[next], (lower[i] + upper[next]) * 0.5f);
                bd.Tri(lower[i], upper[next], upper[i], (lower[i] + upper[i]) * 0.5f);
                bd.Tri(upper[i], upper[next], crown, (upper[i] + upper[next] + crown) / 3f);
            }
            return bd.ToMesh();
        }

        public static Mesh CrystalCluster()
        {
            var bd = new Builder();
            float[] heights = { 0.54f, 0.34f, 0.42f, 0.28f };
            Vector2[] centers = { new Vector2(-0.12f, 0.02f), new Vector2(0.13f, 0.08f),
                new Vector2(0.02f, -0.14f), new Vector2(-0.18f, -0.12f) };
            for (int i = 0; i < heights.Length; i++)
            {
                float a = i * 1.73f;
                float h = heights[i];
                Vector3 center = new Vector3(centers[i].x, 0f, centers[i].y);
                Vector3 tip = center + new Vector3(0.06f * Mathf.Sin(a), h, 0.05f * Mathf.Cos(a));
                Vector3[] ring = new Vector3[4];
                for (int j = 0; j < ring.Length; j++)
                {
                    float angle = a + j * Mathf.PI * 0.5f;
                    ring[j] = center + new Vector3(Mathf.Cos(angle) * 0.105f, h * 0.38f, Mathf.Sin(angle) * 0.105f);
                }
                for (int j = 0; j < ring.Length; j++)
                {
                    int next = (j + 1) % ring.Length;
                    bd.Tri(center, ring[next], ring[j], Vector3.down);
                    bd.Tri(ring[j], ring[next], tip, (ring[j] + ring[next] + tip) / 3f);
                }
            }
            return bd.ToMesh();
        }
    }

    /// <summary>
    /// An off-screen 3D scene rendered into a RenderTexture that a mini-game arena shows as its background.
    /// It lives far from the origin so the main world never sees it.
    /// </summary>
    public class MinigameStage : MonoBehaviour
    {
        public static readonly Vector3 Origin = new Vector3(1000f, 0f, 0f);
        public Camera cam;
        public Transform root;
        public RenderTexture rt;
        public Action FocusLost;
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        float shake;
        Vector3 camBase;

        public static MinigameStage Create(Color bg)
        {
            var go = new GameObject("MinigameStage");
            go.transform.position = Origin;
            var s = go.AddComponent<MinigameStage>();
            s.root = go.transform;
            var cgo = new GameObject("StageCam");
            cgo.transform.SetParent(go.transform, false);
            s.cam = cgo.AddComponent<Camera>();
            s.cam.clearFlags = CameraClearFlags.SolidColor;
            s.cam.backgroundColor = bg;
            s.cam.fieldOfView = 38f;
            s.cam.nearClipPlane = 0.1f;
            s.cam.farClipPlane = 150f;
            s.cam.allowHDR = PresentationQuality.Budget.PostProcessing;
            s.cam.GetUniversalAdditionalCameraData().renderPostProcessing = PresentationQuality.Budget.PostProcessing;
            return s;
        }

        public void Pose(Vector3 localPos, Vector3 lookAt)
        {
            camBase = localPos;
            cam.transform.localPosition = localPos;
            cam.transform.localRotation = Quaternion.LookRotation(lookAt - localPos);
        }

        public bool Resize(float w, float h)
        {
            cam.allowHDR = PresentationQuality.Budget.PostProcessing;
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = PresentationQuality.Budget.PostProcessing;
            PresentationQuality.Budget.FitSurface(w, h, SystemInfo.maxTextureSize, out int iw, out int ih);
            if (rt != null && rt.width == iw && rt.height == ih) return false;
            cam.targetTexture = null;
            if (rt != null) { rt.Release(); Destroy(rt); }
            rt = new RenderTexture(iw, ih, 24, RenderTextureFormat.Default);
            rt.Create();
            cam.targetTexture = rt;
            cam.aspect = (float)iw / ih;
            return true;
        }

        public void Shake(float a) { shake = Mathf.Max(shake, a); }

        void OnApplicationPause(bool paused) { if (paused) FocusLost?.Invoke(); }
        void OnApplicationFocus(bool focused) { if (!focused) FocusLost?.Invoke(); }

        void LateUpdate()
        {
            shake = Mathf.MoveTowards(shake, 0f, Time.unscaledDeltaTime * 1.6f);
            Vector3 off = shake > 0f ? (Vector3)(UnityEngine.Random.insideUnitCircle * shake * 0.3f) : Vector3.zero;
            cam.transform.localPosition = camBase + cam.transform.localRotation * off;
        }

        void OnDestroy()
        {
            if (cam != null) cam.targetTexture = null;
            if (rt != null) { rt.Release(); Destroy(rt); }
            foreach (var o in owned) if (o != null) Destroy(o);
        }

        public T Own<T>(T o) where T : UnityEngine.Object { owned.Add(o); return o; }

        public Material Lit(Color baseC, Color rim, float style, float bands = 4f, float lines = 0f, float seed = 0f)
        {
            var m = new Material(Fx.PlanetShader);
            m.SetColor("_BaseColor", baseC);
            m.SetColor("_AltColor", new Color(baseC.r * 0.22f, baseC.g * 0.22f, baseC.b * 0.22f, 1f));
            m.SetColor("_RimColor", rim);
            m.SetFloat("_Style", style);
            m.SetFloat("_Bands", bands);
            m.SetFloat("_Seed", seed);
            if (m.HasProperty("_Lines")) m.SetFloat("_Lines", lines);
            return Own(m);
        }

        public Material Additive(Color c, Texture tex = null) { return Own(Fx.AddMat(c, tex)); }

        public GameObject AddMesh(Mesh mesh, Material mat, Transform parent, Vector3 pos, bool ownMesh = true)
        {
            var go = new GameObject("Mesh");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.AddComponent<MeshFilter>().sharedMesh = ownMesh ? Own(mesh) : mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        public GameObject Prim(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Vector3 euler, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localEulerAngles = euler;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        /// <summary>A soft additive glow quad. The stage camera never rotates, so an unrotated quad always faces it.</summary>
        public Transform GlowQuad(Transform parent, Color c, float size, Vector3 pos, bool ring = false)
        {
            var q = Prim(PrimitiveType.Quad, parent, pos, Vector3.one * size, Vector3.zero, Additive(c, ring ? Fx.Ring : Fx.Glow));
            return q.transform;
        }

        public void Sparks(Vector3 worldPos, Color c, int n, float speed, float size) { Fx.Burst(worldPos, c, n, speed, size); }

        public GameObject Chunk(Mesh mesh, Material mat, Vector3 worldPos, float scale, Vector3 vel, float gravity, float life)
        {
            var go = new GameObject("Chunk");
            go.transform.SetParent(root, true);
            go.transform.position = worldPos;
            go.transform.localScale = Vector3.one * scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            var d = go.AddComponent<Debris>();
            d.vel = vel; d.gravity = gravity; d.life = life;
            d.spin = UnityEngine.Random.insideUnitSphere * 400f;
            return go;
        }
    }

    public class Debris : MonoBehaviour
    {
        public Vector3 vel, spin;
        public float gravity = 10f, life = 1.4f;
        float t;
        Vector3 s0;

        void Start() { s0 = transform.localScale; }

        void Update()
        {
            float dt = Time.deltaTime;
            t += dt;
            vel.y -= gravity * dt;
            transform.position += vel * dt;
            transform.Rotate(spin * dt, Space.Self);
            float k = Mathf.Clamp01((t / life - 0.55f) / 0.45f);
            transform.localScale = s0 * (1f - k * k);
            if (t >= life) Destroy(gameObject);
        }
    }

    /// <summary>A 3D hand / pick / axe / jackhammer held next to the node. Its origin is the striking tip.</summary>
    public class ToolRig
    {
        public Transform tr;
        readonly MinigameStage st;
        readonly bool vibrate;
        readonly Vector3 rest;
        readonly Quaternion restRot, hitRot;
        Transform readyGlow;
        Vector3 target;
        float t = -1f, dur = 0.5f;

        public bool Ready { get { return t < 0f; } }

        public ToolRig(MinigameStage stage, MinigameKind kind, int level, Color accent, Vector3 restPos)
        {
            st = stage;
            rest = restPos;
            tr = new GameObject("Tool").transform;
            tr.SetParent(st.root, false);
            vibrate = kind == MinigameKind.Mine && level >= 3;
            restRot = Quaternion.Euler(0f, -12f, -58f);
            hitRot = Quaternion.Euler(0f, -6f, vibrate ? -4f : -10f);

            Color wood = new Color(0.5f, 0.3f, 0.13f);
            HarvestToolVisual.Palette(level, accent, out var headC, out var rim);
            var handleM = st.Lit(wood, new Color(0.4f, 0.2f, 0.05f) * 0.4f, 0f, 10f);
            var headM = st.Lit(headC, rim, 0f, 3f);
            var glowM = st.Lit(headC * 0.6f, rim * 1.6f, 0f, 3f);
            if (level >= 3) handleM = glowM;

            if (level <= 0)
            {
                st.Prim(PrimitiveType.Sphere, tr, new Vector3(0.28f, 0.1f, 0f), new Vector3(0.62f, 0.52f, 0.5f), Vector3.zero, headM);
                for (int i = 0; i < 3; i++)
                    st.Prim(PrimitiveType.Sphere, tr, new Vector3(0.02f, 0.28f - i * 0.2f, 0f), Vector3.one * 0.19f, Vector3.zero, headM);
                st.Prim(PrimitiveType.Cylinder, tr, new Vector3(0.62f, 0.7f, 0f), new Vector3(0.28f, 0.7f, 0.28f), new Vector3(0, 0, -25f), headM);
            }
            else if (kind == MinigameKind.Chop)
            {
                st.Prim(PrimitiveType.Cylinder, tr, new Vector3(0.62f, 0.9f, 0f), new Vector3(0.09f, 1.2f, 0.09f), Vector3.zero, handleM);
                st.Prim(PrimitiveType.Cube, tr, new Vector3(0.38f, 0f, 0f), new Vector3(0.7f, 0.9f, 0.14f), Vector3.zero, headM);
                st.Prim(PrimitiveType.Cube, tr, new Vector3(0.04f, 0f, 0f), new Vector3(0.36f, 0.36f, 0.12f), new Vector3(0, 0, 45f), glowM);
            }
            else if (!vibrate)
            {
                st.Prim(PrimitiveType.Cylinder, tr, new Vector3(0.9f, 1f, 0f), new Vector3(0.09f, 1.15f, 0.09f), Vector3.zero, handleM);
                st.Prim(PrimitiveType.Cube, tr, new Vector3(0.9f, 0f, 0f), new Vector3(1.6f, 0.17f, 0.17f), Vector3.zero, headM);
                st.Prim(PrimitiveType.Cube, tr, new Vector3(0.02f, 0f, 0f), new Vector3(0.3f, 0.3f, 0.17f), new Vector3(0, 0, 45f), glowM);
                st.Prim(PrimitiveType.Cube, tr, new Vector3(1.78f, 0f, 0f), new Vector3(0.3f, 0.3f, 0.17f), new Vector3(0, 0, 45f), glowM);
            }
            else
            {
                st.Prim(PrimitiveType.Cylinder, tr, new Vector3(0f, 0.42f, 0f), new Vector3(0.1f, 0.42f, 0.1f), Vector3.zero, glowM);
                st.Prim(PrimitiveType.Cube, tr, new Vector3(0f, 1.3f, 0f), new Vector3(0.62f, 1f, 0.62f), Vector3.zero, headM);
                st.Prim(PrimitiveType.Cube, tr, new Vector3(0f, 1.95f, 0f), new Vector3(0.28f, 0.28f, 0.28f), Vector3.zero, glowM);
                st.Prim(PrimitiveType.Cube, tr, new Vector3(0f, 2.15f, 0f), new Vector3(1.4f, 0.14f, 0.14f), Vector3.zero, handleM);
            }

            readyGlow = st.GlowQuad(tr, new Color(accent.r, accent.g, accent.b, 0.8f) * 1.2f, 0.9f, new Vector3(0f, 0f, -0.15f));
            tr.localPosition = rest;
            tr.localRotation = restRot;
        }

        public void Strike(Vector3 targetLocal, float cooldown)
        {
            target = targetLocal;
            dur = Mathf.Max(0.18f, cooldown);
            t = 0f;
        }

        public void Tick(float dt, float time)
        {
            if (t < 0f)
            {
                tr.localPosition = rest + Vector3.up * (0.07f * Mathf.Sin(time * 2.2f));
                tr.localRotation = restRot;
                float p = 0.9f + 0.2f * Mathf.Sin(time * 6f);
                readyGlow.localScale = Vector3.one * 0.9f * p;
                return;
            }
            t += dt;
            float k = t / dur;
            const float hitK = 0.2f;
            Vector3 pos; Quaternion rot;
            if (k < hitK)
            {
                float e = k / hitK; e *= e;
                pos = Vector3.Lerp(rest, target, e);
                rot = Quaternion.Slerp(restRot, hitRot, e);
            }
            else if (k < 1f)
            {
                float e = (k - hitK) / (1f - hitK); e = 1f - (1f - e) * (1f - e);
                pos = Vector3.Lerp(target, rest, e);
                rot = Quaternion.Slerp(hitRot, restRot, e);
                if (vibrate && k < hitK * 2.2f) pos += UnityEngine.Random.insideUnitSphere * 0.05f;
            }
            else { t = -1f; pos = rest; rot = restRot; }
            tr.localPosition = pos;
            tr.localRotation = rot;
            readyGlow.localScale = Vector3.zero;
        }
    }
}
