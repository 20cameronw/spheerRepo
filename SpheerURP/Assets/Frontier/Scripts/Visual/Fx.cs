using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Frontier
{
    public class FadeOut : MonoBehaviour
    {
        LineRenderer line;
        Material mat;
        Color color;
        float life, t, width;

        public void Init(LineRenderer lr, Color c, float duration)
        {
            line = lr; mat = lr.sharedMaterial; color = c; life = duration; width = lr.widthMultiplier;
        }

        void Update()
        {
            t += Time.deltaTime;
            float k = 1f - Mathf.Clamp01(t / life);
            if (line != null) line.widthMultiplier = width * k;
            if (mat != null) mat.SetColor("_Color", new Color(color.r, color.g, color.b, color.a * k));
            if (t >= life) { if (mat != null) Destroy(mat); Destroy(gameObject); }
        }
    }

    public class Billboard : MonoBehaviour
    {
        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam != null) transform.rotation = cam.transform.rotation;
        }
    }

    public static class Fx
    {
        static Texture2D glow, ring;
        static Shader additive, planetShader;

        public static Shader Additive
        {
            get
            {
                if (additive == null) additive = Resources.Load<Shader>("Frontier/NeonAdditive");
                if (additive == null) additive = Shader.Find("Sprites/Default");
                return additive;
            }
        }

        public static Shader PlanetShader
        {
            get
            {
                if (planetShader == null) planetShader = Resources.Load<Shader>("Frontier/NeonPlanet");
                if (planetShader == null) planetShader = Shader.Find("Universal Render Pipeline/Unlit");
                return planetShader;
            }
        }

        public static Texture2D Glow { get { if (glow == null) glow = MakeRadial(128, 2.2f, false); return glow; } }
        public static Texture2D Ring { get { if (ring == null) ring = MakeRadial(128, 1f, true); return ring; } }

        static Texture2D MakeRadial(int size, float power, bool asRing)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(half, half)) / half;
                    float a;
                    if (asRing) a = Mathf.Clamp01(1f - Mathf.Abs(d - 0.82f) / 0.12f);
                    else a = Mathf.Pow(Mathf.Clamp01(1f - d), power);
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            t.SetPixels(px);
            t.Apply();
            return t;
        }

        public static Material AtmoMat(Color c, float power = 3.2f, float intensity = 1.3f)
        {
            var sh = Resources.Load<Shader>("Frontier/NeonAtmosphere");
            if (sh == null) return AddMat(new Color(c.r, c.g, c.b, 0.08f), null, 3f);
            var m = new Material(sh);
            m.SetColor("_Color", c);
            m.SetFloat("_Power", power);
            m.SetFloat("_Intensity", intensity);
            return m;
        }

        public static Material AddMat(Color c, Texture tex = null, float fresnel = 0f)
        {
            var m = new Material(Additive);
            m.SetColor("_Color", c);
            if (tex != null) m.SetTexture("_MainTex", tex);
            m.SetFloat("_Fresnel", fresnel);
            return m;
        }

        public static GameObject GlowQuad(Transform parent, Color c, float size, Vector3? localPos = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos ?? Vector3.zero;
            go.transform.localScale = Vector3.one * size;
            go.GetComponent<MeshRenderer>().sharedMaterial = AddMat(c, Glow);
            go.AddComponent<Billboard>();
            return go;
        }

        public static void Burst(Vector3 pos, Color c, int count = 18, float speed = 3f, float size = 0.18f)
        {
            var go = new GameObject("FxBurst");
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 1f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
            main.startColor = c;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = count + 8;

            var em = ps.emission;
            em.enabled = true;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Sphere;
            sh.radius = 0.05f;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = g;

            var sz = ps.sizeOverLifetime;
            sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = AddMat(Color.white, Glow);

            ps.Play();
            Object.Destroy(go, 1.6f);
        }

        public static void Bolt(Vector3 a, Vector3 b, Color c, float width = 0.06f, float life = 0.14f)
        {
            var go = new GameObject("FxBolt");
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.numCapVertices = 4;
            lr.positionCount = 5;
            Vector3 dir = b - a;
            Vector3 perp = Vector3.Cross(dir, Vector3.forward).normalized;
            for (int i = 0; i < 5; i++)
            {
                float k = i / 4f;
                float jitter = (i == 0 || i == 4) ? 0f : Random.Range(-0.18f, 0.18f);
                lr.SetPosition(i, a + dir * k + perp * jitter);
            }
            lr.widthMultiplier = width;
            lr.sharedMaterial = AddMat(c);
            go.AddComponent<FadeOut>().Init(lr, c, life);
        }

        public static void Ripple(Vector3 pos, Color c, float size = 1.2f, float life = 0.35f)
        {
            var go = GlowQuad(null, c, 0.1f);
            go.transform.position = pos;
            go.GetComponent<MeshRenderer>().sharedMaterial = AddMat(c, Ring);
            go.AddComponent<RippleAnim>().Init(size, life, c);
        }

        public static void SetupPost(Camera cam)
        {
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;

            var go = new GameObject("Post Volume");
            var vol = go.AddComponent<Volume>();
            vol.isGlobal = true;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();

            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(1.7f);
            bloom.threshold.Override(0.85f);
            bloom.scatter.Override(0.75f);

            var vig = profile.Add<Vignette>(true);
            vig.intensity.Override(0.38f);

            var ca = profile.Add<ChromaticAberration>(true);
            ca.intensity.Override(0.12f);

            vol.profile = profile;
        }
    }

    public class RippleAnim : MonoBehaviour
    {
        float size, life, t;
        Color color;
        Material mat;

        public void Init(float s, float l, Color c)
        {
            size = s; life = l; color = c;
            mat = GetComponent<MeshRenderer>().sharedMaterial;
        }

        void Update()
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / life);
            transform.localScale = Vector3.one * Mathf.Lerp(0.1f, size, 1f - (1f - k) * (1f - k));
            mat.SetColor("_Color", new Color(color.r, color.g, color.b, color.a * (1f - k)));
            if (k >= 1f) { Destroy(mat); Destroy(gameObject); }
        }
    }

    public static class Sfx
    {
        static AudioSource src;
        static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();

        public static bool Muted;
        public static void Init(GameObject host) { src = host.AddComponent<AudioSource>(); src.playOnAwake = false; }

        public static void Play(float freq, float dur = 0.12f, float vol = 0.25f, float sweep = 0f)
        {
            if (src == null || Muted) return;
            string key = freq + "_" + dur + "_" + sweep;
            AudioClip clip;
            if (!cache.TryGetValue(key, out clip))
            {
                const int rate = 44100;
                int n = Mathf.Max(1, (int)(rate * dur));
                var data = new float[n];
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)rate;
                    float env = Mathf.Pow(1f - i / (float)n, 2f) * Mathf.Min(1f, i / 200f);
                    data[i] = Mathf.Sin(2f * Mathf.PI * (freq * t + 0.5f * sweep * t * t)) * env;
                }
                clip = AudioClip.Create("sfx", n, 1, rate, false);
                clip.SetData(data, 0);
                cache[key] = clip;
            }
            src.PlayOneShot(clip, vol);
        }

        public static void Click() { Play(660f, 0.06f, 0.2f); }
        public static void Good() { Play(520f, 0.18f, 0.25f, 900f); }
        public static void Bad() { Play(180f, 0.2f, 0.25f, -200f); }
        public static void Zap() { Play(900f, 0.09f, 0.15f, -4000f); }
        public static void Boom() { Play(120f, 0.3f, 0.35f, -250f); }
        public static void Warp() { Play(200f, 1.2f, 0.3f, 900f); }
    }
}
