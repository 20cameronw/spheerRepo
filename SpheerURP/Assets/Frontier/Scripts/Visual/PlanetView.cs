using System.Collections;
using System.Collections.Generic;
using Frontier.Universe;
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
        public PlanetData Current { get; private set; }
        public event System.Action<PlanetData> Rebuilt;
        float punch;
        public bool IdleSpin = true;
        public bool CloseView;
        /// <summary>Holds the planet completely still (used while a structure is selected).</summary>
        public bool Frozen;
        float appear = 1f;
        GameObject glow;
        Material atmosphereMat;
        MeshRenderer glowRenderer;
        Color baseRim;
        int terraformingStage;
        bool punchWas;
        bool usesModel;
        List<Material> modelMats;
        Transform atmosphere;
        float displayedRadius, displayedModelScale, lastRadius;

        List<MeshRenderer> solids = new List<MeshRenderer>();

        /// <summary>The planet's ground. Its radius is the single source of truth for where structures sit.</summary>
        public SphereCollider Surface { get; private set; }

        public float SurfaceRadius() { return Surface != null ? Surface.radius : Radius; }
        /// <summary>The crater pattern (style 2) turns into black and white dice when its cells are big and the palette is dark: generated worlds get a finer grid and keep the terrain between the discs. Authored worlds keep their values and look.</summary>
        const float MinGeneratedCraterBands = 9f;

        static float EffectiveBands(PlanetData p, float bands)
        {
            return !p.IsAuthored && p.Style == 2 ? Mathf.Max(bands, MinGeneratedCraterBands) : bands;
        }

        static float CraterSoftness(PlanetData p) { return p.IsAuthored ? 0f : 1f; }

        static void SetWeatherSurface(Material material, PlanetData p, Color primary)
        {
            PlanetSurfaceStyle.ApplyClimate(material, p, primary);
        }

        static void TerraformPalette(PlanetData p, int level, out Color primary, out Color secondary, out Color rim)
        {
            float terraforming = Mathf.Clamp01(level / 5f);
            primary = Color.Lerp(p.Primary, new Color(0.13f, 0.72f, 0.45f), terraforming * 0.35f);
            secondary = Color.Lerp(p.Secondary, new Color(0.10f, 0.42f, 0.30f), terraforming * 0.35f);
            rim = Color.Lerp(p.Rim, new Color(0.18f, 1f, 0.58f), terraforming * 0.4f);
        }

        void StyleModel(Transform model, PlanetData p, System.Random rng, Color primary, Color secondary)
        {
            float bands = 4f + (float)rng.NextDouble() * 6f, seed = (float)rng.NextDouble() * 10f;
            if (p.SurfaceBands > 0f) bands = p.SurfaceBands;
            bands = EffectiveBands(p, bands);
            var mats = new List<Material>();
            int layer = 0;
            foreach (var r in solids)
            {
                // Stacked layers (land/ocean) alternate the palette so they read as different terrain.
                bool alt = (layer++ % 2) == 1;
                var mat = new Material(Fx.PlanetShader);
                mat.SetColor("_BaseColor", alt ? Color.Lerp(secondary, primary, 0.35f) : primary);
                mat.SetColor("_AltColor", alt ? secondary * 0.6f : secondary);
                mat.SetColor("_RimColor", baseRim);
                SetWeatherSurface(mat, p, primary);
                mat.SetFloat("_Bands", bands);
                mat.SetFloat("_Seed", seed);
                mat.SetFloat("_Style", p.Style);
                mat.SetFloat("_CraterSoft", CraterSoftness(p));
                var lb = r.localBounds.center;
                mat.SetVector("_Center", new Vector4(lb.x, lb.y, lb.z, 0f));
                var arr = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                for (int i = 0; i < arr.Length; i++) arr[i] = mat;
                r.sharedMaterials = arr;
                mats.Add(mat);
            }
            modelMats = mats;
        }

        public void Show(PlanetData p, bool animate, int terraformingLevel = 0)
        {
            if (root != null) Destroy(root.gameObject);
            orbiters.Clear(); orbiterSpeed.Clear();

            Current = p;
            root = new GameObject("PlanetRoot").transform;
            root.SetParent(transform, false);
            root.localRotation = Quaternion.Euler(16f, 0f, -7f);
            Spin = new GameObject("Spin").transform;
            Spin.SetParent(root, false);
            // The stable visual seed (never string.GetHashCode): for the 30 Core Worlds it equals the legacy id hash, so their look is unchanged.
            var rng = new System.Random(unchecked((int)p.Seed));
            terraformingStage = Mathf.Clamp(terraformingLevel, 0, 5);

            var sphere = new GameObject("Core");
            sphere.transform.SetParent(Spin, false);
            core = sphere.transform;
            Color primary, secondary, rim;
            TerraformPalette(p, terraformingStage, out primary, out secondary, out rim);
            baseRim = rim * p.GlowIntensity;
            coreMat = null; modelMats = null;
            float ground = p.SurfaceRadius > 0.01f ? p.SurfaceRadius : Radius;
            // The model is fitted to its own radius (the standard 1.5 for generated worlds, whatever their ground radius), so every planet is the same size on screen.
            displayedRadius = p.ModelRadius > 0.01f ? p.ModelRadius : Radius;
            displayedModelScale = p.ModelScale > 0f ? p.ModelScale : 1f;
            lastRadius = ground;
            var model = ModelLibrary.Spawn(p.ModelKey, core, displayedRadius * 2f * displayedModelScale, false);
            if (model != null)
            {
                solids.Clear();
                solids.AddRange(model.GetComponentsInChildren<MeshRenderer>());
                StyleModel(model, p, rng, primary, secondary);
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
                coreMat.SetColor("_BaseColor", primary);
                coreMat.SetColor("_AltColor", secondary);
                coreMat.SetColor("_RimColor", baseRim);
                SetWeatherSurface(coreMat, p, primary);
                coreMat.SetFloat("_Bands", EffectiveBands(p, p.SurfaceBands > 0f ? p.SurfaceBands : 4f + (float)rng.NextDouble() * 6f));
                coreMat.SetFloat("_Seed", (float)rng.NextDouble() * 10f);
                coreMat.SetFloat("_Style", p.Style);
                coreMat.SetFloat("_CraterSoft", CraterSoftness(p));
                sphere.GetComponent<MeshRenderer>().sharedMaterial = coreMat;
            }
            usesModel = model != null;

            var surfGo = new GameObject("Surface");
            surfGo.transform.SetParent(Spin, false);
            Surface = surfGo.AddComponent<SphereCollider>();
            Surface.radius = ground;

            var atmo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            atmo.name = "Atmosphere";
            Destroy(atmo.GetComponent<Collider>());
            atmo.transform.SetParent(root, false);
            atmosphere = atmo.transform;
            atmo.transform.localScale = Vector3.one * p.AtmosphereScale;
            Color atmosphereColor = PlanetSurfaceStyle.AtmosphereColor(p, rim) * (1.2f * p.AtmosphereIntensity);
            atmosphereMat = Fx.AtmoMat(new Color(atmosphereColor.r, atmosphereColor.g, atmosphereColor.b, 1f));
            atmo.GetComponent<MeshRenderer>().sharedMaterial = atmosphereMat;

            glow = Fx.GlowQuad(root, new Color(baseRim.r, baseRim.g, baseRim.b, 0.28f), Radius * 6.5f, new Vector3(0, 0, 0.5f));
            glowRenderer = glow.GetComponent<MeshRenderer>();

            if (p.HasRings)
                for (int i = 0; i < 2; i++)
                    BuildRing(Radius * (1.7f + i * 0.35f), rim, (float)rng.NextDouble() * 70f - 35f, (float)rng.NextDouble() * 360f);

            // Moons: angle and speed come from the same stream as before (authored worlds keep their layout); the orbit tilt has its own seeded stream.
            // More than four moons share the same outer limit instead of drifting off screen.
            int moons = p.MoonCount;
            float moonStep = Mathf.Min(0.45f, 1.5f / Mathf.Max(1, moons - 1));
            var tiltRng = new System.Random(unchecked((int)p.Seed ^ (int)(p.Seed >> 32) ^ 0x4D4F4F4E));
            for (int i = 0; i < moons; i++)
                BuildMoon(Radius * (2.1f + i * moonStep), rim, (float)rng.NextDouble() * 6.28f, 0.6f + (float)rng.NextDouble() * 0.6f,
                    Mathf.Lerp(-25f, 25f, (float)tiltRng.NextDouble()), Mathf.Lerp(-15f, 15f, (float)tiltRng.NextDouble()));

            Weather.Build(root, p);

            appear = animate ? 0f : 1f;
            root.localScale = Vector3.one * appear;
            if (Rebuilt != null) Rebuilt(p);
        }

        public void SetTerraformingStage(int level)
        {
            if (Current == null) return;
            int stage = Mathf.Clamp(level, 0, 5);
            if (stage == terraformingStage) return;
            terraformingStage = stage;

            Color primary, secondary, rim;
            TerraformPalette(Current, terraformingStage, out primary, out secondary, out rim);
            baseRim = rim * Current.GlowIntensity;
            if (coreMat != null)
            {
                coreMat.SetColor("_BaseColor", primary);
                coreMat.SetColor("_AltColor", secondary);
                coreMat.SetColor("_RimColor", baseRim);
                coreMat.SetColor("_CloudColor", PlanetSurfaceStyle.CloudColor(Current, primary));
            }
            if (modelMats != null)
            {
                foreach (var material in modelMats)
                {
                    if (material == null) continue;
                    material.SetColor("_BaseColor", primary);
                    material.SetColor("_AltColor", secondary);
                    material.SetColor("_RimColor", baseRim);
                    material.SetColor("_CloudColor", PlanetSurfaceStyle.CloudColor(Current, primary));
                }
            }

            Color atmosphereColor = PlanetSurfaceStyle.AtmosphereColor(Current, rim) * (1.2f * Current.AtmosphereIntensity);
            if (atmosphereMat != null)
                atmosphereMat.SetColor("_Color", new Color(atmosphereColor.r, atmosphereColor.g, atmosphereColor.b, 1f));
            if (glowRenderer != null)
                FxTransientPool.SetColor(glowRenderer, new Color(baseRim.r, baseRim.g, baseRim.b, 0.28f));
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

        void BuildMoon(float radius, Color c, float angle, float speed, float tiltX, float tiltZ)
        {
            var pivot = new GameObject("MoonPivot").transform;
            pivot.SetParent(root, false);
            pivot.localRotation = Quaternion.Euler(tiltX, 0f, tiltZ);
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

        public IEnumerator Approach(PlanetData planet, int terraformingLevel = 0)
        {
            Show(planet, false, terraformingLevel);
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
            // Authored worlds tie the model to the ground sphere (tuning the collider in the editor resizes the planet); generated worlds keep the standard size.
            float size = (Current.IsAuthored ? SurfaceRadius() / Mathf.Max(0.01f, displayedRadius) : 1f) *
                (Current.ModelScale > 0f ? Current.ModelScale : 1f) / displayedModelScale;
            core.localScale = Vector3.one * (usesModel ? 1f : displayedRadius * 2f * displayedModelScale) * size * pulse;
            if (atmosphere != null) atmosphere.localScale = Vector3.one * Current.AtmosphereScale;
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
            // Authoring aid for the hand made worlds: the surface collider and the asset's radius follow each other. Generated worlds have no asset.
            var authored = Current.Authored;
            if (authored != null)
            {
                if (!Mathf.Approximately(authored.surfaceRadius, lastRadius))
                {
                    Surface.radius = Mathf.Max(0.01f, authored.surfaceRadius);
                    lastRadius = Surface.radius;
                }
                else if (!Mathf.Approximately(Surface.radius, lastRadius))
                {
                    authored.surfaceRadius = Surface.radius;
                    lastRadius = Surface.radius;
                    UnityEditor.EditorUtility.SetDirty(authored);
                }
            }
#endif
        }
    }
}
