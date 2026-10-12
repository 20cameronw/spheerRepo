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

        class Layer { public Transform tr; public Material mat; public Color color; public float drift, speed, phase, amp, ang; public Vector3 basePos; }

        struct Mood
        {
            public Color tint, neb1, neb2, bg;
            public float bright, spd, nebMul, rushRate, rushSpeed, flicker;
        }

        readonly System.Collections.Generic.List<Material> nebMats = new System.Collections.Generic.List<Material>();
        ParticleSystem rush;
        Mood cur, target;
        Color baseBg;
        float flash, nextFlash = 3f, rushRateNow;
        bool moodInit;

        static Mood MakeMood(BackdropMood m, Color baseBg)
        {
            switch (m)
            {
                case BackdropMood.Rush:
                    return new Mood { tint = new Color(0.8f, 1.1f, 1.6f), neb1 = new Color(0.1f, 0.6f, 1.3f, 0.3f), neb2 = new Color(1.2f, 0.2f, 0.9f, 0.25f), bg = new Color(0.0f, 0.02f, 0.06f), bright = 1.3f, spd = 3.5f, nebMul = 0.7f, rushRate = 70f, rushSpeed = 55f };
                case BackdropMood.Gloom:
                    return new Mood { tint = new Color(0.55f, 0.6f, 0.7f), neb1 = new Color(0.35f, 0.08f, 0.12f, 0.35f), neb2 = new Color(0.08f, 0.15f, 0.2f, 0.4f), bg = new Color(0.012f, 0.012f, 0.02f), bright = 0.4f, spd = 0.35f, nebMul = 0.9f, rushRate = 0f, rushSpeed = 4f };
                case BackdropMood.Glow:
                    return new Mood { tint = new Color(1.4f, 1.2f, 1.7f), neb1 = new Color(1.3f, 0.45f, 1.1f, 0.5f), neb2 = new Color(0.2f, 1.1f, 1.3f, 0.45f), bg = new Color(0.06f, 0.025f, 0.11f), bright = 1.6f, spd = 0.8f, nebMul = 1.7f, rushRate = 4f, rushSpeed = 3f };
                case BackdropMood.Storm:
                    return new Mood { tint = new Color(0.9f, 0.8f, 1.3f), neb1 = new Color(0.45f, 0.1f, 0.9f, 0.4f), neb2 = new Color(0.1f, 0.2f, 0.6f, 0.4f), bg = new Color(0.02f, 0.01f, 0.05f), bright = 0.75f, spd = 1.8f, nebMul = 1.2f, rushRate = 25f, rushSpeed = 32f, flicker = 1f };
                default:
                    return new Mood { tint = Color.white, neb1 = new Color(0.5f, 0.1f, 1.2f, 0.35f), neb2 = new Color(0f, 0.7f, 1.2f, 0.3f), bg = baseBg, bright = 1f, spd = 1f, nebMul = 1f, rushRate = 0f, rushSpeed = 4f };
            }
        }

        static BackdropMood Derive(PlanetSO p)
        {
            if (p.mood != BackdropMood.Auto) return p.mood;
            if (p.tier <= 1) return BackdropMood.Calm;
            switch (p.weather)
            {
                case WeatherKind.Storm: return BackdropMood.Storm;
                case WeatherKind.Rain: return BackdropMood.Gloom;
                case WeatherKind.Snow: return BackdropMood.Glow;
                case WeatherKind.Embers: return BackdropMood.Rush;
                case WeatherKind.Dust: return BackdropMood.Rush;
                case WeatherKind.Spores: return BackdropMood.Gloom;
            }
            int h = Mathf.Abs((p.id ?? "").GetHashCode()) % 3;
            return h == 0 ? BackdropMood.Calm : h == 1 ? BackdropMood.Glow : BackdropMood.Rush;
        }

        public void Apply(PlanetSO p)
        {
            if (p == null) return;
            target = MakeMood(Derive(p), baseBg);
            if (p.tier <= 1 && p.mood == BackdropMood.Auto)
            {
                target.bright *= 0.65f;
                target.nebMul *= 0.3f;
                target.spd *= 0.5f;
            }
            if (!moodInit) { cur = target; moodInit = true; }
        }

        public void Build(Camera camera)
        {
            cam = camera;
            baseBg = cam.backgroundColor;
            cur = target = MakeMood(BackdropMood.Calm, baseBg);
            BuildRush();
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
            float t = Time.time, dt = Time.deltaTime;
            float k = 1f - Mathf.Exp(-dt * 1.2f);
            cur.tint = Color.Lerp(cur.tint, target.tint, k);
            cur.neb1 = Color.Lerp(cur.neb1, target.neb1, k);
            cur.neb2 = Color.Lerp(cur.neb2, target.neb2, k);
            cur.bg = Color.Lerp(cur.bg, target.bg, k);
            cur.bright = Mathf.Lerp(cur.bright, target.bright, k);
            cur.spd = Mathf.Lerp(cur.spd, target.spd, k);
            cur.nebMul = Mathf.Lerp(cur.nebMul, target.nebMul, k);
            cur.rushSpeed = Mathf.Lerp(cur.rushSpeed, target.rushSpeed, k);
            cur.flicker = target.flicker;
            rushRateNow = Mathf.Lerp(rushRateNow, target.rushRate, k);
            if (!Warping) cam.backgroundColor = cur.bg;

            flash = Mathf.MoveTowards(flash, 0f, dt * 2.5f);
            if (cur.flicker > 0.01f && t > nextFlash) { nextFlash = t + Random.Range(2f, 6f); flash = 1f; }

            if (rush != null)
            {
                var em = rush.emission; em.rateOverTime = rushRateNow;
                var vel = rush.velocityOverLifetime; vel.x = new ParticleSystem.MinMaxCurve(-cur.rushSpeed);
            }

            float boost = 1f + flash * 1.8f;
            foreach (var l in layers)
            {
                l.ang += dt * 0.35f * l.drift * cur.spd;
                l.tr.localPosition = l.basePos + new Vector3(Mathf.Sin(t * 0.05f * l.drift + l.phase) * 2.2f * l.drift, Mathf.Cos(t * 0.04f * l.drift + l.phase) * 1.4f * l.drift, 0f);
                l.tr.localRotation = Quaternion.Euler(0f, 0f, l.ang);
                float tw = (1f - l.amp * (0.5f + 0.5f * Mathf.Sin(t * l.speed * (0.6f + 0.4f * cur.spd) + l.phase))) * cur.bright * boost;
                l.mat.SetColor("_Color", new Color(l.color.r * cur.tint.r * tw, l.color.g * cur.tint.g * tw, l.color.b * cur.tint.b * tw, l.color.a));
            }
            for (int i = 0; i < nebulae.Count; i++)
            {
                nebulae[i].localPosition += new Vector3(Mathf.Sin(t * 0.07f + i * 2f), Mathf.Cos(t * 0.05f + i), 0f) * 0.15f * cur.spd * dt;
                Color nc = i % 2 == 0 ? cur.neb1 : cur.neb2;
                float m = cur.nebMul * boost;
                nebMats[i].SetColor("_Color", new Color(nc.r * m, nc.g * m, nc.b * m, nc.a));
            }
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
            vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
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
            nebMats.Add(q.GetComponent<MeshRenderer>().sharedMaterial);
        }

        void BuildRush()
        {
            var go = new GameObject("Rush");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(30f, 0f, 24f);
            rush = go.AddComponent<ParticleSystem>();
            rush.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = rush.main;
            main.loop = true; main.playOnAwake = false;
            main.startLifetime = 2.2f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.6f, 1.2f, 2f, 0.8f), new Color(1.6f, 1.6f, 2.2f, 1f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 600;
            var em = rush.emission; em.rateOverTime = 0f;
            var sh = rush.shape;
            sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = new Vector3(1f, 40f, 12f);
            var vel = rush.velocityOverLifetime;
            vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-4f); vel.y = new ParticleSystem.MinMaxCurve(0f); vel.z = new ParticleSystem.MinMaxCurve(0f);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.12f; r.lengthScale = 2f;
            r.sharedMaterial = Fx.AddMat(Color.white, Fx.Glow);
            rush.Play();
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
