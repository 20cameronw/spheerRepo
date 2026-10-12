using System.Collections;
using UnityEngine;

namespace Frontier
{
    /// <summary>Starfield, nebula glows and the hyperspace warp effect.</summary>
    public class SpaceBackdrop : MonoBehaviour
    {
        Camera cam;
        ParticleSystem warp;
        public bool Warping { get; private set; }

        readonly System.Collections.Generic.List<Layer> layers = new System.Collections.Generic.List<Layer>();
        readonly System.Collections.Generic.List<Transform> nebulae = new System.Collections.Generic.List<Transform>();
        float nextShoot = 2f;

        class Layer { public Transform tr; public Material mat; public Color color; public float drift, speed, phase, amp; public Vector3 basePos; }

        public void Build(Camera camera)
        {
            cam = camera;
            BuildStars(450, 40f, 0.04f, 0.10f, new Color(0.7f, 0.9f, 2f, 1f), 0.25f, 0f, 0.8f, 0.25f);
            BuildStars(300, 38f, 0.06f, 0.14f, new Color(1.6f, 1.6f, 2f, 1f), 0.6f, 1.7f, 1.3f, 0.45f);
            BuildStars(150, 34f, 0.10f, 0.22f, new Color(2f, 1.3f, 0.8f, 1f), 1.1f, 3.1f, 1.9f, 0.5f);
            BuildStars(70, 30f, 0.2f, 0.45f, new Color(2f, 0.6f, 1.8f, 0.8f), 1.8f, 4.6f, 0.9f, 0.4f);
            BuildNebula(new Vector3(-6f, 4f, 25f), new Color(0.5f, 0.1f, 1.2f, 0.35f), 30f);
            BuildNebula(new Vector3(7f, -5f, 28f), new Color(0.0f, 0.7f, 1.2f, 0.3f), 34f);
            BuildShooting();
            BuildWarp();
        }

        void Update()
        {
            float t = Time.time;
            foreach (var l in layers)
            {
                l.tr.localPosition = l.basePos + new Vector3(Mathf.Sin(t * 0.05f * l.drift + l.phase) * 2.2f * l.drift, Mathf.Cos(t * 0.04f * l.drift + l.phase) * 1.4f * l.drift, 0f);
                l.tr.localRotation = Quaternion.Euler(0f, 0f, t * 0.35f * l.drift);
                float tw = 1f - l.amp * (0.5f + 0.5f * Mathf.Sin(t * l.speed + l.phase));
                l.mat.SetColor("_Color", new Color(l.color.r * tw, l.color.g * tw, l.color.b * tw, l.color.a));
            }
            for (int i = 0; i < nebulae.Count; i++)
                nebulae[i].localPosition += new Vector3(Mathf.Sin(t * 0.07f + i * 2f), Mathf.Cos(t * 0.05f + i), 0f) * 0.15f * Time.deltaTime;
            if (shooting != null && t > nextShoot && !Warping)
            {
                nextShoot = t + Random.Range(2.5f, 6f);
                shooting.transform.localPosition = new Vector3(Random.Range(-18f, 18f), Random.Range(6f, 16f), 22f);
                shooting.Emit(1);
            }
        }

        ParticleSystem shooting;

        void BuildShooting()
        {
            var go = new GameObject("ShootingStars");
            go.transform.SetParent(transform, false);
            shooting = go.AddComponent<ParticleSystem>();
            shooting.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = shooting.main;
            main.loop = true; main.playOnAwake = false;
            main.startLifetime = 1.1f;
            main.startSpeed = 0f;
            main.startSize = 0.35f;
            main.startColor = new Color(1.4f, 2f, 2.6f, 1f);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 8;
            var em = shooting.emission; em.rateOverTime = 0f;
            var vel = shooting.velocityOverLifetime;
            vel.enabled = true; vel.space = ParticleSystemSimulationSpace.Local;
            vel.x = new ParticleSystem.MinMaxCurve(-26f, -18f);
            vel.y = new ParticleSystem.MinMaxCurve(-16f, -10f);
            vel.z = 0f;
            var col = shooting.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.08f; r.lengthScale = 6f;
            r.sharedMaterial = Fx.AddMat(Color.white, Fx.Glow);
            shooting.Play();
        }

        void BuildStars(int count, float radius, float minSize, float maxSize, Color color, float drift, float phase, float twinkleSpeed, float amp)
        {
            var go = new GameObject("Stars");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = 100000f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
            main.startColor = new ParticleSystem.MinMaxGradient(color * 0.4f, color);
            main.maxParticles = count;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var em = ps.emission;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = new Vector3(radius * 1.8f, radius * 1.8f, 10f);
            sh.position = new Vector3(0, 0, 20f);
            var mat = Fx.AddMat(Color.white, Fx.Glow);
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = mat;
            ps.Play();
            layers.Add(new Layer { tr = go.transform, mat = mat, color = Color.white, drift = drift, speed = twinkleSpeed, phase = phase, amp = amp, basePos = Vector3.zero });
        }

        void BuildNebula(Vector3 pos, Color c, float size)
        {
            var q = Fx.GlowQuad(transform, c, size, pos);
            q.name = "Nebula";
            nebulae.Add(q.transform);
        }

        void BuildWarp()
        {
            var go = new GameObject("Warp");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(0, 0, 30f);
            go.transform.rotation = Quaternion.LookRotation(Vector3.back);
            warp = go.AddComponent<ParticleSystem>();
            warp.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = warp.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = 0.7f;
            main.startSpeed = 60f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.4f, 1.6f, 2.4f, 1f), new Color(2.2f, 0.6f, 2.2f, 1f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 800;
            var em = warp.emission;
            em.rateOverTime = 600f;
            var sh = warp.shape;
            sh.shapeType = ParticleSystemShapeType.Circle;
            sh.radius = 9f;
            sh.radiusThickness = 1f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.06f;
            r.lengthScale = 4f;
            r.sharedMaterial = Fx.AddMat(Color.white, Fx.Glow);
        }

        public IEnumerator DoWarp(System.Action atPeak)
        {
            Warping = true;
            Sfx.Warp();
            warp.Play();
            float baseFov = cam.fieldOfView;
            float t = 0f;
            bool fired = false;
            while (t < 1.8f)
            {
                t += Time.deltaTime;
                float k = Mathf.Sin(Mathf.Clamp01(t / 1.8f) * Mathf.PI);
                cam.fieldOfView = baseFov + k * 38f;
                if (!fired && t > 0.9f) { fired = true; if (atPeak != null) atPeak(); }
                yield return null;
            }
            cam.fieldOfView = baseFov;
            warp.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Warping = false;
        }
    }
}
