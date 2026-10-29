using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Frontier
{
    /// <summary>Shared presentation-only ship. Forward is +Z; gear and engines never affect simulation.</summary>
    public sealed class ShipRig : MonoBehaviour
    {
        readonly List<Mesh> meshes = new List<Mesh>();
        readonly List<Transform> legs = new List<Transform>();
        readonly List<Transform> engines = new List<Transform>();
        readonly List<MeshFilter> parts = new List<MeshFilter>();
        readonly List<ParticleSystem> exhaust = new List<ParticleSystem>();
        readonly List<Transform> landingJets = new List<Transform>();
        Transform hull, lamps;
        Material material;
        public float Gear { get; private set; }
        public float Throttle { get; private set; }
        public float LandingThrust { get; private set; }
        public int TriangleCount { get; private set; }

        public static ShipRig Create(Transform parent, int layer = 0, bool cameraOnly = false)
        {
            var go = new GameObject("FrontierShip");
            go.transform.SetParent(parent, false);
            go.layer = layer;
            var rig = go.AddComponent<ShipRig>();
            rig.Build(layer);
            if (cameraOnly)
                foreach (var renderer in rig.GetComponentsInChildren<MeshRenderer>(true))
                    renderer.enabled = false;
            else rig.BuildExhaust(layer);
            rig.SetFlight(0f, 1f, 0f);
            return rig;
        }

        void BuildExhaust(int layer)
        {
            foreach (var engine in engines)
            {
                var go = FxTemplates.Create("Burst", hull);
                go.name = "EngineExhaust";
                go.layer = layer;
                go.transform.localPosition = engine.localPosition;
                go.transform.localRotation = Quaternion.LookRotation(Vector3.back);
                var particles = go.GetComponent<ParticleSystem>();
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = particles.main;
                main.loop = true;
                main.playOnAwake = false;
                main.useUnscaledTime = true;
                main.startLifetime = 0.18f;
                main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 2f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.065f);
                main.startColor = new Color(0.15f, 0.9f, 1f);
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                var shape = particles.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 9f;
                shape.radius = 0.045f;
                var emission = particles.emission;
                emission.SetBursts(Array.Empty<ParticleSystem.Burst>());
                exhaust.Add(particles);
            }
            PresentationQuality.Changed += ApplyExhaustBudget;
            ApplyExhaustBudget();
        }

        void ApplyExhaustBudget()
        {
            foreach (var particles in exhaust)
            {
                var main = particles.main;
                main.maxParticles = Mathf.CeilToInt(24 * PresentationQuality.Budget.ParticleMultiplier);
                var emission = particles.emission;
                emission.rateOverTime = 60f * Throttle * PresentationQuality.Budget.ParticleMultiplier;
            }
        }

        void Build(int layer)
        {
            var shader = Resources.Load<Shader>("Frontier/ShipVertex");
            if (shader == null) throw new InvalidOperationException("Frontier ship shader is missing.");
            material = new Material(shader) { name = "Frontier ship vertex palette" };
            Color metal = new Color(0.48f, 0.6f, 0.76f), cyan = new Color(0.15f, 0.9f, 1f);
            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var indices = new List<int>();
            Vector3 nose = new Vector3(0f, 0f, 1f), tail = new Vector3(0f, 0f, -0.7f);
            Vector3 left = new Vector3(-0.36f, 0f, -0.45f), right = new Vector3(0.36f, 0f, -0.45f);
            Vector3 top = new Vector3(0f, 0.3f, -0.15f), bottom = new Vector3(0f, -0.16f, -0.2f);
            Triangle(vertices, colors, indices, nose, left, top, metal);
            Triangle(vertices, colors, indices, nose, top, right, metal * 0.8f);
            Triangle(vertices, colors, indices, left, tail, top, metal * 0.6f);
            Triangle(vertices, colors, indices, tail, right, top, metal * 0.7f);
            Triangle(vertices, colors, indices, nose, bottom, left, metal * 0.45f);
            Triangle(vertices, colors, indices, nose, right, bottom, metal * 0.5f);
            Triangle(vertices, colors, indices, left, bottom, tail, metal * 0.4f);
            Triangle(vertices, colors, indices, tail, bottom, right, metal * 0.4f);
            for (int side = -1; side <= 1; side += 2)
            {
                var tip = new Vector3(side * 1f, -0.02f, -0.65f);
                var front = new Vector3(side * 0.25f, 0f, 0.35f);
                var rear = new Vector3(side * 0.3f, 0f, -0.65f);
                Triangle(vertices, colors, indices, front, tip, rear, metal * 0.65f);
                Triangle(vertices, colors, indices, tip, tip + Vector3.up * 0.12f, rear, cyan);
            }
            Triangle(vertices, colors, indices, new Vector3(-0.12f, 0.2f, 0.1f),
                new Vector3(0f, 0.31f, -0.12f), new Vector3(0.12f, 0.2f, 0.1f), cyan);
            hull = Part("Hull", Mesh("Ship hull", vertices, colors, indices), transform, layer);
            var strut = BoxMesh("Landing strut", metal);
            var flame = FlameMesh(cyan);
            lamps = new GameObject("LandingLights").transform;
            lamps.SetParent(hull, false);
            var lampMesh = BoxMesh("Landing lamp", new Color(0.9f, 1f, 0.7f));
            for (int side = -1; side <= 1; side += 2)
            {
                var engine = Part("Engine_" + side, flame, hull, layer);
                engine.localPosition = new Vector3(side * 0.34f, -0.03f, -0.55f);
                engines.Add(engine);
                var lamp = Part("Lamp_" + side, lampMesh, lamps, layer);
                lamp.localPosition = new Vector3(side * 0.45f, -0.08f, 0.12f);
                lamp.localScale = new Vector3(0.1f, 0.06f, 0.16f);
            }
            for (int i = 0; i < 3; i++)
            {
                var pivot = new GameObject("LandingLeg_" + i).transform;
                pivot.SetParent(hull, false);
                pivot.localPosition = i == 0 ? new Vector3(0f, -0.1f, 0.4f)
                    : new Vector3(i == 1 ? -0.55f : 0.55f, -0.07f, -0.4f);
                var foot = Part("Strut", strut, pivot, layer);
                foot.localPosition = Vector3.down * 0.16f;
                foot.localScale = new Vector3(0.08f, 0.32f, 0.12f);
                legs.Add(pivot);
            }
        }

        public void SetFlight(float throttle, float gear, float bank)
        {
            if (float.IsNaN(throttle) || float.IsInfinity(throttle) || float.IsNaN(gear) ||
                float.IsInfinity(gear) || float.IsNaN(bank) || float.IsInfinity(bank))
                throw new ArgumentOutOfRangeException(nameof(throttle), "Ship presentation values must be finite.");
            Throttle = Mathf.Clamp01(throttle);
            Gear = Mathf.Clamp01(gear);
            hull.localRotation = Quaternion.Euler(0f, 0f, Mathf.Clamp(bank, -35f, 35f));
            foreach (var leg in legs) leg.localRotation = Quaternion.Euler((1f - Gear) * 90f, 0f, 0f);
            foreach (var engine in engines)
            {
                engine.gameObject.SetActive(Throttle > 0f);
                engine.localScale = new Vector3(0.12f, 0.12f, 0.2f + Throttle * 0.9f);
            }
            lamps.gameObject.SetActive(Gear > 0.1f);
            ApplyExhaustBudget();
            foreach (var particles in exhaust)
            {
                if (Throttle > 0f && !particles.isPlaying) particles.Play();
                else if (Throttle == 0f) particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        public void SetLandingThrust(float thrust, float phase)
        {
            if (float.IsNaN(thrust) || float.IsInfinity(thrust) || float.IsNaN(phase) || float.IsInfinity(phase))
                throw new ArgumentOutOfRangeException(nameof(thrust), "Landing presentation values must be finite.");
            LandingThrust = Mathf.Clamp01(thrust);
            if (landingJets.Count == 0 && LandingThrust > 0f)
            {
                var nozzleMesh = BoxMesh("Landing booster nozzle", new Color(0.48f, 0.6f, 0.76f));
                var airMesh = FlameMesh(new Color(0.65f, 0.95f, 1f));
                for (int side = -1; side <= 1; side += 2)
                {
                    var nozzle = Part("LandingBooster_" + side, nozzleMesh, hull, gameObject.layer);
                    nozzle.localPosition = new Vector3(0f, -0.15f, side * 0.4f);
                    nozzle.localScale = new Vector3(0.18f, 0.1f, 0.18f);
                    var jet = Part("LandingAir_" + side, airMesh, hull, gameObject.layer);
                    jet.localPosition = nozzle.localPosition + Vector3.down * 0.05f;
                    jet.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                    landingJets.Add(jet);
                }
            }
            float pulse = 1f + Mathf.Sin(phase * 46f) * 0.12f;
            foreach (var jet in landingJets)
            {
                jet.gameObject.SetActive(LandingThrust > 0f);
                jet.localScale = new Vector3(0.11f, 0.11f, 0.65f) * (LandingThrust * pulse);
            }
        }

        public void DrawForCamera(Camera camera)
        {
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            foreach (var part in parts)
                if (part.gameObject.activeInHierarchy)
                    Graphics.DrawMesh(part.sharedMesh, part.transform.localToWorldMatrix, material,
                        part.gameObject.layer, camera, 0, null, ShadowCastingMode.Off, false);
        }

        Transform Part(string name, Mesh mesh, Transform parent, int layer)
        {
            var go = new GameObject(name) { layer = layer };
            go.transform.SetParent(parent, false);
            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            parts.Add(filter);
            TriangleCount += (int)mesh.GetIndexCount(0) / 3;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go.transform;
        }

        Mesh Mesh(string name, List<Vector3> vertices, List<Color> colors, List<int> indices)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateBounds();
            meshes.Add(mesh);
            return mesh;
        }

        Mesh FlameMesh(Color color)
        {
            var v = new List<Vector3>(); var c = new List<Color>(); var t = new List<int>();
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f, b = (i + 1) * Mathf.PI / 3f;
                Triangle(v, c, t, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f),
                    new Vector3(Mathf.Cos(b), Mathf.Sin(b), 0f), Vector3.back, color);
            }
            return Mesh("Engine plume", v, c, t);
        }

        Mesh BoxMesh(string name, Color color)
        {
            var v = new List<Vector3>(); var c = new List<Color>(); var t = new List<int>();
            var corners = new[]
            {
                new Vector3(-.5f,-.5f,-.5f), new Vector3(.5f,-.5f,-.5f),
                new Vector3(.5f,.5f,-.5f), new Vector3(-.5f,.5f,-.5f),
                new Vector3(-.5f,-.5f,.5f), new Vector3(.5f,-.5f,.5f),
                new Vector3(.5f,.5f,.5f), new Vector3(-.5f,.5f,.5f)
            };
            int[] faces = { 0,2,1,0,3,2,4,5,6,4,6,7,0,1,5,0,5,4,3,7,6,3,6,2,0,4,7,0,7,3,1,2,6,1,6,5 };
            for (int i = 0; i < faces.Length; i += 3)
                Triangle(v, c, t, corners[faces[i]], corners[faces[i + 1]], corners[faces[i + 2]], color);
            return Mesh(name, v, c, t);
        }

        static void Triangle(List<Vector3> v, List<Color> c, List<int> t, Vector3 a, Vector3 b, Vector3 d, Color color)
        {
            int start = v.Count;
            v.Add(a); v.Add(b); v.Add(d);
            c.Add(color); c.Add(color); c.Add(color);
            t.Add(start); t.Add(start + 1); t.Add(start + 2);
        }

        void OnDestroy()
        {
            PresentationQuality.Changed -= ApplyExhaustBudget;
            foreach (var mesh in meshes) if (mesh != null) Destroy(mesh);
            if (material != null) Destroy(material);
        }
    }
}
