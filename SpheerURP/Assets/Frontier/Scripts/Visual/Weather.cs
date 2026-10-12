using UnityEngine;

namespace Frontier
{
    /// <summary>Particle weather that hugs the planet's atmosphere. Built per planet from PlanetSO.weather.</summary>
    public static class Weather
    {
        public static void Build(Transform root, PlanetSO p)
        {
            if (p.weather == WeatherKind.Clear) return;
            var go = new GameObject("Weather_" + p.weather);
            go.transform.SetParent(root, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 600;

            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Sphere;
            sh.radius = PlanetView.Radius * 1.07f;
            sh.radiusThickness = 0f;

            var em = ps.emission;
            em.enabled = true;

            Color rim = p.rim;
            Color tint;
            float noise = 0f;
            var r = go.GetComponent<ParticleSystemRenderer>();

            switch (p.weather)
            {
                case WeatherKind.Rain:
                case WeatherKind.Storm:
                    tint = new Color(0.55f, 0.9f, 2.2f, 0.8f);
                    main.startLifetime = 0.22f;
                    main.startSpeed = new ParticleSystem.MinMaxCurve(-1.6f, -2.4f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.05f);
                    em.rateOverTime = p.weather == WeatherKind.Storm ? 260f : 150f;
                    r.renderMode = ParticleSystemRenderMode.Stretch;
                    r.lengthScale = 5f;
                    r.velocityScale = 0.1f;
                    break;
                case WeatherKind.Snow:
                    tint = new Color(1.6f, 2f, 2.4f, 0.9f);
                    main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(-0.22f, -0.1f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.045f, 0.09f);
                    em.rateOverTime = 70f;
                    noise = 0.25f;
                    break;
                case WeatherKind.Embers:
                    tint = new Color(2.6f, 0.9f, 0.25f, 1f);
                    main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 2f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.09f);
                    em.rateOverTime = 45f;
                    noise = 0.4f;
                    break;
                case WeatherKind.Spores:
                    tint = new Color(rim.r, rim.g, rim.b, 0.9f);
                    main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(0.03f, 0.14f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.13f);
                    em.rateOverTime = 28f;
                    noise = 0.35f;
                    break;
                default: // Dust
                    tint = new Color(rim.r * 0.8f, rim.g * 0.7f, rim.b * 0.6f, 0.35f);
                    main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 3.5f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.08f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.24f);
                    em.rateOverTime = 22f;
                    var vel = ps.velocityOverLifetime;
                    vel.enabled = true;
                    vel.space = ParticleSystemSimulationSpace.Local;
                    vel.orbitalY = 0.5f;
                    break;
            }
            main.startColor = tint;

            if (noise > 0f)
            {
                var n = ps.noise;
                n.enabled = true;
                n.strength = noise;
                n.frequency = 0.8f;
            }

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = g;

            r.sharedMaterial = Fx.AddMat(Color.white, Fx.Glow);
            ps.Play();

            if (p.weather == WeatherKind.Storm) go.AddComponent<Lightning>().Init(root, p.rim);
        }
    }

    public class Lightning : MonoBehaviour
    {
        Transform planetRoot;
        Color color;
        float next;

        public void Init(Transform root, Color c)
        {
            planetRoot = root; color = new Color(Mathf.Max(c.r, 1.4f), Mathf.Max(c.g, 1.4f), 2.6f, 1f);
            next = Time.time + Random.Range(1f, 3f);
        }

        void Update()
        {
            if (Time.time < next || planetRoot == null) return;
            next = Time.time + Random.Range(1.6f, 4.5f);
            var cam = Camera.main;
            if (cam == null) return;
            Vector3 c = planetRoot.position;
            Vector3 toCam = (cam.transform.position - c).normalized;
            Vector3 dir = (toCam + Random.insideUnitSphere * 0.9f).normalized;
            if (Vector3.Dot(dir, toCam) < 0.2f) dir = (dir + toCam).normalized;
            float s = planetRoot.lossyScale.x;
            Vector3 top = c + dir * PlanetView.Radius * 1.45f * s;
            Vector3 bottom = c + dir * PlanetView.Radius * 1.03f * s;
            Fx.Bolt(top, bottom, color, 0.05f, 0.22f);
            Fx.Burst(bottom, color, 14, 1.6f, 0.12f);
            Fx.Ripple(bottom, color, 0.8f, 0.4f);
        }
    }
}
