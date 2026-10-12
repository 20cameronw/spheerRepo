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

        public void Build(Camera camera)
        {
            cam = camera;
            BuildStars(900, 40f, 0.05f, 0.14f, new Color(0.7f, 0.9f, 2f, 1f));
            BuildStars(80, 30f, 0.2f, 0.45f, new Color(2f, 0.6f, 1.8f, 0.8f));
            BuildNebula(new Vector3(-6f, 4f, 25f), new Color(0.5f, 0.1f, 1.2f, 0.35f), 30f);
            BuildNebula(new Vector3(7f, -5f, 28f), new Color(0.0f, 0.7f, 1.2f, 0.3f), 34f);
            BuildWarp();
        }

        void BuildStars(int count, float radius, float minSize, float maxSize, Color color)
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
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = new Vector3(radius * 1.4f, radius * 1.4f, 10f);
            sh.position = new Vector3(0, 0, 20f);
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = Fx.AddMat(Color.white, Fx.Glow);
            ps.Play();
        }

        void BuildNebula(Vector3 pos, Color c, float size)
        {
            var q = Fx.GlowQuad(transform, c, size, pos);
            q.name = "Nebula";
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
