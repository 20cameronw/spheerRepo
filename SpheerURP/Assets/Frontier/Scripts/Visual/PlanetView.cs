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
        float spinVelocity;
        public bool IdleSpin = true;
        float punch;
        float appear = 1f;
        GameObject glow;
        Color baseRim;
        bool punchWas;
        bool usesModel;

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
            coreMat = null;
            var model = ModelLibrary.Spawn(p.id, core, Radius * 2f);
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
                sphere.GetComponent<MeshRenderer>().sharedMaterial = coreMat;
            }
            usesModel = model != null;

            var atmo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(atmo.GetComponent<Collider>());
            atmo.transform.SetParent(root, false);
            atmo.transform.localScale = Vector3.one * Radius * 2.4f;
            atmo.GetComponent<MeshRenderer>().sharedMaterial = Fx.AddMat(new Color(p.rim.r, p.rim.g, p.rim.b, 0.9f), null, 2.2f);

            glow = Fx.GlowQuad(root, new Color(p.rim.r, p.rim.g, p.rim.b, 0.55f), Radius * 6.5f, new Vector3(0, 0, 0.5f));

            int rings = Mathf.Clamp(1 + p.tier / 2, 1, 3);
            for (int i = 0; i < rings; i++)
                BuildRing(Radius * (1.7f + i * 0.35f), p.rim, (float)rng.NextDouble() * 70f - 35f, (float)rng.NextDouble() * 360f);

            int moons = Mathf.Clamp(1 + p.tier, 1, 4);
            for (int i = 0; i < moons; i++)
                BuildMoon(Radius * (2.1f + i * 0.45f), p.rim, (float)rng.NextDouble() * 6.28f, 0.6f + (float)rng.NextDouble() * 0.6f);

            appear = animate ? 0f : 1f;
            root.localScale = Vector3.one * appear;
            if (Rebuilt != null) Rebuilt(p);
        }

        public void AddSpin(float degrees) { if (Spin != null) Spin.Rotate(0f, -degrees, 0f, Space.Self); }
        public void SetSpinVelocity(float degPerSec) { spinVelocity = degPerSec; }

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
            spinVelocity = Mathf.MoveTowards(spinVelocity, 0f, Time.deltaTime * 140f);
            Spin.Rotate(0f, -(spinVelocity + (IdleSpin ? 5f : 0f)) * Time.deltaTime, 0f, Space.Self);
            for (int i = 0; i < orbiters.Count; i++) orbiters[i].Rotate(0f, orbiterSpeed[i] * Time.deltaTime, 0f, Space.Self);
            if (coreMat != null && (punch > 0f || punchWas)) coreMat.SetColor("_RimColor", Color.Lerp(baseRim, new Color(3f, 0.4f, 0.4f), punch));
            punchWas = punch > 0f;
        }
    }
}
