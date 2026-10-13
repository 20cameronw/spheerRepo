using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    public class PlanetView : MonoBehaviour
    {
        public const float Radius = PlanetSO.StandardSurfaceRadius;

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
        /// <summary>Holds the planet completely still (used while a structure is selected).</summary>
        public bool Frozen;
        float appear = 1f;
        GameObject glow;
        Color baseRim;
        bool punchWas;
        bool usesModel;
        List<Material> modelMats;
        Transform atmosphere;
        float displayedRadius, displayedModelScale, lastRadius;

        List<MeshRenderer> solids = new List<MeshRenderer>();

        /// <summary>The planet's ground. Its radius is the single source of truth for where structures sit.</summary>
        public SphereCollider Surface { get; private set; }

        public float SurfaceRadius() { return Surface != null ? Surface.radius : Radius; }
        void StyleModel(Transform model, PlanetSO p, System.Random rng)
        {
            float bands = 4f + (float)rng.NextDouble() * 6f, seed = (float)rng.NextDouble() * 10f;
            if (p.surfaceBands > 0f) bands = p.surfaceBands;
            var mats = new List<Material>();
            int layer = 0;
            foreach (var r in solids)
            {
                // Stacked layers (land/ocean) alternate the palette so they read as different terrain.
                bool alt = (layer++ % 2) == 1;
                var mat = new Material(Fx.PlanetShader);
                mat.SetColor("_BaseColor", alt ? Color.Lerp(p.secondary, p.primary, 0.35f) : p.primary);
                mat.SetColor("_AltColor", alt ? p.secondary * 0.6f : p.secondary);
                mat.SetColor("_RimColor", baseRim);
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
            baseRim = p.rim * p.glowIntensity;
            coreMat = null; modelMats = null;
            displayedRadius = p.surfaceRadius > 0.01f ? p.surfaceRadius : Radius;
            displayedModelScale = p.modelScale > 0f ? p.modelScale : 1f;
            lastRadius = displayedRadius;
            var model = ModelLibrary.Spawn(p.ModelKey, core, displayedRadius * 2f * displayedModelScale, false);
            if (model != null)
            {
                solids.Clear();
                solids.AddRange(model.GetComponentsInChildren<MeshRenderer>());
                StyleModel(model, p, rng);
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
                coreMat.SetColor("_RimColor", baseRim);
                coreMat.SetFloat("_Bands", p.surfaceBands > 0f ? p.surfaceBands : 4f + (float)rng.NextDouble() * 6f);
                coreMat.SetFloat("_Seed", (float)rng.NextDouble() * 10f);
                coreMat.SetFloat("_Style", p.style);
                sphere.GetComponent<MeshRenderer>().sharedMaterial = coreMat;
            }
            usesModel = model != null;

            var surfGo = new GameObject("Surface");
            surfGo.transform.SetParent(Spin, false);
            Surface = surfGo.AddComponent<SphereCollider>();
            Surface.radius = p.surfaceRadius > 0.01f ? p.surfaceRadius : Radius;

            var atmo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            atmo.name = "Atmosphere";
            Destroy(atmo.GetComponent<Collider>());
            atmo.transform.SetParent(root, false);
            atmosphere = atmo.transform;
            atmo.transform.localScale = Vector3.one * p.atmosphereScale;
            Color atmosphereColor = p.rim * (1.2f * p.atmosphereIntensity);
            atmo.GetComponent<MeshRenderer>().sharedMaterial = Fx.AtmoMat(new Color(atmosphereColor.r, atmosphereColor.g, atmosphereColor.b, 1f));

            glow = Fx.GlowQuad(root, new Color(baseRim.r, baseRim.g, baseRim.b, 0.28f), Radius * 6.5f, new Vector3(0, 0, 0.5f));

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
        static float DefaultSpin { get { return FrontierSettings.I.defaultSpin; } }
        static float DragDegPerPx { get { return FrontierSettings.I.dragDegPerPx; } }
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

        public IEnumerator Depart()
        {
            Frozen = true;
            float time = 0f;
            Vector3 start = root.localPosition;
            while (time < 0.9f)
            {
                time += Time.deltaTime;
                float k = Mathf.Clamp01(time / 0.9f);
                root.localPosition = start + new Vector3(-3f * k * k, -0.5f * k, 12f * k * k);
                root.localScale = Vector3.one * Mathf.Lerp(1f, 0.02f, k * k);
                yield return null;
            }
            root.gameObject.SetActive(false);
        }

        public IEnumerator Approach(PlanetSO planet)
        {
            Show(planet, false);
            Frozen = true;
            float time = 0f;
            while (time < 1.1f)
            {
                time += Time.deltaTime;
                float k = 1f - Mathf.Pow(1f - Mathf.Clamp01(time / 1.1f), 3f);
                root.localPosition = new Vector3(2.5f, 0.4f, 12f) * (1f - k);
                root.localScale = Vector3.one * Mathf.Lerp(0.02f, 1f, k);
                yield return null;
            }
            root.localPosition = Vector3.zero;
            root.localScale = Vector3.one;
            Frozen = false;
        }

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
            float pulse = 1f + punch * 0.08f;
            float size = SurfaceRadius() / Mathf.Max(0.01f, displayedRadius) *
                (Current.modelScale > 0f ? Current.modelScale : 1f) / displayedModelScale;
            core.localScale = Vector3.one * (usesModel ? 1f : displayedRadius * 2f * displayedModelScale) * size * pulse;
            if (atmosphere != null) atmosphere.localScale = Vector3.one * Current.atmosphereScale;
            if (Frozen) { angVel = Vector3.zero; }
            else if (!grabbed)
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
#if UNITY_EDITOR
            if (!Mathf.Approximately(Current.surfaceRadius, lastRadius))
            {
                Surface.radius = Mathf.Max(0.01f, Current.surfaceRadius);
                lastRadius = Surface.radius;
            }
            else if (!Mathf.Approximately(Surface.radius, lastRadius))
            {
                Current.surfaceRadius = Surface.radius;
                lastRadius = Surface.radius;
                UnityEditor.EditorUtility.SetDirty(Current);
            }
#endif
        }
    }
}
