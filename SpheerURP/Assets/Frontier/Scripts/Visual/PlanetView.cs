using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    public class PlanetView : MonoBehaviour
    {
        public const float Radius = 1.5f;

        Transform core;
        Material coreMat;
        readonly List<Transform> orbiters = new List<Transform>();
        readonly List<float> orbiterSpeed = new List<float>();
        Transform root;
        public Transform Spin { get; private set; }
        public Transform Root { get { return root; } }
        public PlanetSO Current { get; private set; }
        public event System.Action<PlanetSO> Rebuilt;
        float punch;
        public bool IdleSpin = true;
        float appear = 1f;
        GameObject glow;
        Color baseRim;
        bool punchWas;
        bool usesModel;
        List<Material> modelMats;

        // Raycasts the real mesh from outside so structures sit on the true surface, not an assumed radius.
        Vector3[] surfDirs;
        float[] surfR;

        void SampleSurface(Transform model)
        {
            var cols = new List<MeshCollider>();
            foreach (var mf in model.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                var mc = mf.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                cols.Add(mc);
            }
            if (cols.Count == 0) return;
            var set = new HashSet<Collider>(cols);
            var savedScale = root.localScale;
            root.localScale = Vector3.one;
            Physics.SyncTransforms();

            const int N = 400;
            var dirs = new Vector3[N]; var rs = new float[N];
            int hitCount = 0;
            for (int i = 0; i < N; i++)
            {
                float y = 1f - 2f * (i + 0.5f) / N;
                float r = Mathf.Sqrt(1f - y * y);
                float a = i * 2.39996323f;
                var d = new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
                dirs[i] = d;
                Vector3 from = Spin.TransformPoint(d * Radius * 5f);
                Vector3 to = Spin.TransformDirection(-d);
                float best = float.MaxValue; Vector3 bp = Vector3.zero;
                foreach (var h in Physics.RaycastAll(from, to, Radius * 8f))
                    if (set.Contains(h.collider) && h.distance < best) { best = h.distance; bp = h.point; }
                if (best < float.MaxValue) { rs[i] = Spin.InverseTransformPoint(bp).magnitude; hitCount++; }
                else rs[i] = Radius;
            }
            root.localScale = savedScale;
            foreach (var c in cols) Destroy(c);
            if (hitCount < N / 2) return;
            surfDirs = dirs; surfR = rs;
        }

        public float SurfaceRadius(Vector3 localDir)
        {
            if (surfR == null) return Radius;
            localDir.Normalize();
            float sum = 0f; int n = 0;
            for (int i = 0; i < surfDirs.Length; i++)
                if (Vector3.Dot(surfDirs[i], localDir) > 0.985f) { sum += surfR[i]; n++; }
            if (n == 0)
            {
                float bd = -2f; int bi = 0;
                for (int i = 0; i < surfDirs.Length; i++)
                {
                    float d = Vector3.Dot(surfDirs[i], localDir);
                    if (d > bd) { bd = d; bi = i; }
                }
                return surfR[bi];
            }
            return sum / n;
        }

        // Rescales a hand-made model so its typical surface sits exactly at Radius, whatever its shape or import scale.
        void NormalizeModel(Transform model, Transform center)
        {
            var dists = new List<float>();
            foreach (var mf in model.GetComponentsInChildren<MeshFilter>())
            {
                var m = mf.sharedMesh;
                if (m == null || !m.isReadable) continue;
                var vs = m.vertices;
                int step = Mathf.Max(1, vs.Length / 1500);
                for (int i = 0; i < vs.Length; i += step)
                    dists.Add((mf.transform.TransformPoint(vs[i]) - center.position).magnitude);
            }
            if (dists.Count < 8) return;
            dists.Sort();
            float median = dists[dists.Count / 2];
            if (median < 1e-4f) return;
            float k = Radius / median;
            model.localScale *= k;
            model.position = center.position + (model.position - center.position) * k;
        }

        void StyleModel(Transform model, PlanetSO p, System.Random rng)
        {
            float bands = 4f + (float)rng.NextDouble() * 6f, seed = (float)rng.NextDouble() * 10f;
            var mats = new List<Material>();
            foreach (var r in model.GetComponentsInChildren<MeshRenderer>())
            {
                var mat = new Material(Fx.PlanetShader);
                mat.SetColor("_BaseColor", p.primary);
                mat.SetColor("_AltColor", p.secondary);
                mat.SetColor("_RimColor", p.rim);
                mat.SetFloat("_Bands", bands);
                mat.SetFloat("_Seed", seed);
                mat.SetFloat("_Style", p.style);
                var lb = r.localBounds.center;
                mat.SetVector("_Center", new Vector4(lb.x, lb.y, lb.z, 0f));
                var arr = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                for (int i = 0; i < arr.Length; i++) arr[i] = mat;
                r.sharedMaterials = arr;
                mats.Add(mat);
            }
            modelMats = mats;
        }

        public void Show(PlanetSO p, bool animate)
        {
            if (root != null) Destroy(root.gameObject);
            orbiters.Clear(); orbiterSpeed.Clear();

            Current = p;
            root = new GameObject("PlanetRoot").transform;
            root.SetParent(transform, false);
            root.localRotation = Quaternion.Euler(16f, 0f, -7f);
            Spin = new GameObject("Spin").transform;
            Spin.SetParent(root, false);
            var rng = new System.Random(p.id.GetHashCode());

            var sphere = new GameObject("Core");
            sphere.transform.SetParent(Spin, false);
            core = sphere.transform;
            baseRim = p.rim;
            coreMat = null; modelMats = null;
            var model = ModelLibrary.Spawn(p.id, core, Radius * 2f);
            surfDirs = null; surfR = null;
            if (model != null)
            {
                NormalizeModel(model, core);
                StyleModel(model, p, rng);
                SampleSurface(model);
            }
            if (model == null)
            {
                Destroy(sphere);
                sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(sphere.GetComponent<Collider>());
                sphere.transform.SetParent(Spin, false);
                sphere.transform.localScale = Vector3.one * Radius * 2f;
                core = sphere.transform;
                coreMat = new Material(Fx.PlanetShader);
                coreMat.SetColor("_BaseColor", p.primary);
                coreMat.SetColor("_AltColor", p.secondary);
                coreMat.SetColor("_RimColor", p.rim);
                coreMat.SetFloat("_Bands", 4f + (float)rng.NextDouble() * 6f);
                coreMat.SetFloat("_Seed", (float)rng.NextDouble() * 10f);
                coreMat.SetFloat("_Style", p.style);
                sphere.GetComponent<MeshRenderer>().sharedMaterial = coreMat;
            }
            usesModel = model != null;

            var atmo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(atmo.GetComponent<Collider>());
            atmo.transform.SetParent(root, false);
            atmo.transform.localScale = Vector3.one * Radius * 2.28f;
            atmo.GetComponent<MeshRenderer>().sharedMaterial = Fx.AtmoMat(new Color(p.rim.r * 1.2f, p.rim.g * 1.2f, p.rim.b * 1.2f, 1f));

            glow = Fx.GlowQuad(root, new Color(p.rim.r, p.rim.g, p.rim.b, 0.28f), Radius * 6.5f, new Vector3(0, 0, 0.5f));

            if (p.hasRings)
                for (int i = 0; i < 2; i++)
                    BuildRing(Radius * (1.7f + i * 0.35f), p.rim, (float)rng.NextDouble() * 70f - 35f, (float)rng.NextDouble() * 360f);

            int moons = p.tier / 2;
            for (int i = 0; i < moons; i++)
                BuildMoon(Radius * (2.1f + i * 0.45f), p.rim, (float)rng.NextDouble() * 6.28f, 0.6f + (float)rng.NextDouble() * 0.6f);

            Weather.Build(root, p);

            appear = animate ? 0f : 1f;
            root.localScale = Vector3.one * appear;
            if (Rebuilt != null) Rebuilt(p);
        }

        // Free trackball control: drags rotate about the camera's up/right axes, so any direction works.
        const float DragDegPerPx = 0.32f;
        const float DefaultSpin = 5f;
        Vector3 angVel;
        bool grabbed;
        float returnAt;

        Vector3 DefaultVel() { return root != null ? -root.up * DefaultSpin : Vector3.zero; }

        public void BeginDrag() { grabbed = true; angVel = Vector3.zero; }

        public void Drag(Vector2 deltaPx, float dt)
        {
            if (Spin == null) return;
            var c = Camera.main;
            Vector3 up = c != null ? c.transform.up : Vector3.up, right = c != null ? c.transform.right : Vector3.right;
            Vector3 step = (-up * deltaPx.x + right * deltaPx.y) * DragDegPerPx;
            float ang = step.magnitude;
            if (ang > 0f) Spin.rotation = Quaternion.AngleAxis(ang, step / ang) * Spin.rotation;
            angVel = Vector3.Lerp(angVel, step / Mathf.Max(0.001f, dt), 0.4f);
        }

        public void EndDrag()
        {
            grabbed = false;
            if (angVel.magnitude > 400f) angVel = angVel.normalized * 400f;
            returnAt = Time.time + 1.5f;
        }

        void BuildRing(float radius, Color c, float tilt, float yaw)
        {
            var go = new GameObject("Ring");
            go.transform.SetParent(root, false);
            go.transform.localRotation = Quaternion.Euler(tilt + 60f, yaw, 0f);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = true;
            const int n = 96;
            lr.positionCount = n;
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2f;
                lr.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f));
            }
            lr.widthMultiplier = 0.035f;
            lr.sharedMaterial = Fx.AddMat(new Color(c.r, c.g, c.b, 0.9f));
        }

        void BuildMoon(float radius, Color c, float angle, float speed)
        {
            var pivot = new GameObject("MoonPivot").transform;
            pivot.SetParent(root, false);
            pivot.localRotation = Quaternion.Euler(Random.Range(-25f, 25f), 0f, Random.Range(-15f, 15f));
            pivot.localEulerAngles += new Vector3(0, Mathf.Rad2Deg * angle, 0);
            var moon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(moon.GetComponent<Collider>());
            moon.transform.SetParent(pivot, false);
            moon.transform.localPosition = new Vector3(radius, 0f, 0f);
            moon.transform.localScale = Vector3.one * 0.22f;
            moon.GetComponent<MeshRenderer>().sharedMaterial = Fx.AddMat(new Color(c.r, c.g, c.b, 1f), null, 1.2f);
            Fx.GlowQuad(moon.transform, new Color(c.r, c.g, c.b, 0.7f), 4f);
            orbiters.Add(pivot);
            orbiterSpeed.Add(speed * 25f);
        }

        public void Punch() { punch = 1f; }

        void Update()
        {
            if (root == null) return;
            if (appear < 1f)
            {
                appear = Mathf.MoveTowards(appear, 1f, Time.deltaTime * 1.4f);
                float e = 1f - Mathf.Pow(1f - appear, 3f);
                root.localScale = Vector3.one * Mathf.Max(0.001f, e * 1.0f);
            }
            punch = Mathf.MoveTowards(punch, 0f, Time.deltaTime * 4f);
            float pulse = 1f + Mathf.Sin(Time.time * 1.6f) * 0.012f + punch * 0.08f;
            core.localScale = Vector3.one * (usesModel ? 1f : Radius * 2f) * pulse;
            if (!grabbed)
            {
                Vector3 def = IdleSpin ? DefaultVel() : Vector3.zero;
                if (Time.time >= returnAt) angVel = Vector3.Lerp(angVel, def, 1f - Mathf.Exp(-1.6f * Time.deltaTime));
                else angVel = Vector3.Lerp(angVel, Vector3.zero, 1f - Mathf.Exp(-0.5f * Time.deltaTime));
                float a = angVel.magnitude * Time.deltaTime;
                if (a > 0f) Spin.rotation = Quaternion.AngleAxis(a, angVel.normalized) * Spin.rotation;
            }
            for (int i = 0; i < orbiters.Count; i++) orbiters[i].Rotate(0f, orbiterSpeed[i] * Time.deltaTime, 0f, Space.Self);
            if (punch > 0f || punchWas)
            {
                var rim = Color.Lerp(baseRim, new Color(3f, 0.4f, 0.4f), punch);
                if (coreMat != null) coreMat.SetColor("_RimColor", rim);
                if (modelMats != null) foreach (var m in modelMats) if (m != null) m.SetColor("_RimColor", rim);
            }
            punchWas = punch > 0f;
        }
    }
}
