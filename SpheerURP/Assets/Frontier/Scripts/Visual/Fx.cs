using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Frontier
{
    public class FadeOut : MonoBehaviour
    {
        LineRenderer line;
        Color color;
        float life, t, width;

        public void Init(LineRenderer lr, Color c, float duration)
        {
            line = lr; color = c; life = Mathf.Max(0.01f, duration); width = lr.widthMultiplier; t = 0f;
        }

        void Update()
        {
            t += Time.deltaTime;
            float k = 1f - Mathf.Clamp01(t / life);
            if (line != null) line.widthMultiplier = width * k;
            if (line != null)
            {
                var faded = new Color(color.r, color.g, color.b, color.a * k);
                line.startColor = faded;
                line.endColor = faded;
            }
            if (t >= life) FxTransientPool.Release(gameObject);
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
        static Material glowMaterial;

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
        public static Material GlowMaterial { get { if (glowMaterial == null) glowMaterial = FxTemplates.Load<Material>("Glow"); return glowMaterial; } }

        /// <summary>Clamps an HDR resource/accent colour into displayable range (largest channel at most 1), keeping its hue.</summary>
        public static Color Displayable(Color c, float alpha = 1f)
        {
            float m = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            if (m > 1f) c = new Color(c.r / m, c.g / m, c.b / m);
            return new Color(c.r, c.g, c.b, alpha);
        }

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
            return FxTransientPool.AcquireGlowQuad(parent, c, size, localPos ?? Vector3.zero);
        }

        public static void Burst(Vector3 pos, Color c, int count = 18, float speed = 3f, float size = 0.18f)
        {
            count = Mathf.Max(1, Mathf.RoundToInt(count * PresentationQuality.Budget.ParticleMultiplier));
            FxBurstPool.Emit(pos, c, count, speed, size);
        }

        public static void Bolt(Vector3 a, Vector3 b, Color c, float width = 0.06f, float life = 0.14f)
        {
            var go = FxTransientPool.AcquireBolt();
            var lr = go.GetComponent<LineRenderer>();
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
            lr.startColor = c;
            lr.endColor = c;
            var fade = go.GetComponent<FadeOut>();
            if (fade == null) fade = go.AddComponent<FadeOut>();
            fade.Init(lr, c, life);
        }

        public static void Ripple(Vector3 pos, Color c, float size = 1.2f, float life = 0.35f)
        {
            var go = FxTransientPool.AcquireRipple();
            go.transform.position = pos;
            var renderer = go.GetComponentInChildren<MeshRenderer>();
            FxTransientPool.SetColor(renderer, c);
            go.GetComponent<RippleAnim>().Init(size, life, c);
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
            go.AddComponent<FrontierPostQuality>().Init(cam, profile);
        }
    }

    public class RippleAnim : MonoBehaviour
    {
        float size, life, t;
        Color color;
        Renderer targetRenderer;

        public void Init(float s, float l, Color c)
        {
            size = s; life = Mathf.Max(0.01f, l); color = c; t = 0f;
            targetRenderer = GetComponentInChildren<Renderer>();
        }

        void Update()
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / life);
            transform.localScale = Vector3.one * Mathf.Lerp(0.1f, size, 1f - (1f - k) * (1f - k));
            if (targetRenderer != null)
                FxTransientPool.SetColor(targetRenderer, new Color(color.r, color.g, color.b, color.a * (1f - k)));
            if (k >= 1f) FxTransientPool.Release(gameObject);
        }
    }

    public static class FxTransientPool
    {
        const int Capacity = 24;
        const int GlowCapacity = 128;
        static readonly GameObject[] Bolts = new GameObject[Capacity];
        static readonly GameObject[] Ripples = new GameObject[Capacity];
        static readonly GameObject[] GlowQuads = new GameObject[GlowCapacity];
        static int boltCount, rippleCount, boltCursor, rippleCursor;
        static int glowCount, glowCursor;
        static GameObject root;
        static MaterialPropertyBlock properties;

        public static int BoltPoolCount { get { return boltCount; } }
        public static int RipplePoolCount { get { return rippleCount; } }
        public static int GlowQuadPoolCount { get { return glowCount; } }

        public static GameObject AcquireBolt()
        {
            var go = Acquire(Bolts, ref boltCursor, ref boltCount, "FxBolt");
            return go;
        }

        public static GameObject AcquireRipple()
        {
            var go = Acquire(Ripples, ref rippleCursor, ref rippleCount, "FxRipple");
            if (go.GetComponent<RippleAnim>() == null) go.AddComponent<RippleAnim>();
            return go;
        }

        public static GameObject AcquireGlowQuad(Transform owner, Color color, float size, Vector3 localPosition)
        {
            EnsureRoot();
            GameObject go = null;
            int emptySlot = -1;
            for (int offset = 0; offset < glowCount; offset++)
            {
                int index = (glowCursor + offset) % glowCount;
                if (GlowQuads[index] == null) { emptySlot = index; continue; }
                if (GlowQuads[index].activeSelf) continue;
                go = GlowQuads[index];
                glowCursor = (index + 1) % GlowCapacity;
                go.SetActive(true);
                break;
            }
            bool pooled = go != null || emptySlot >= 0 || glowCount < GlowCapacity;
            if (go == null)
            {
                go = FxTemplates.Create("FxGlow", root.transform);
                if (pooled)
                {
                    if (emptySlot >= 0) GlowQuads[emptySlot] = go;
                    else GlowQuads[glowCount++] = go;
                    glowCursor = glowCount % GlowCapacity;
                }
            }
            var follower = go.GetComponent<GlowQuadFollower>();
            if (follower == null) follower = go.AddComponent<GlowQuadFollower>();
            follower.Configure(owner, color, size, localPosition, pooled);
            return go;
        }

        static GameObject Acquire(GameObject[] pool, ref int cursor, ref int count, string name)
        {
            EnsureRoot();
            for (int offset = 0; offset < count; offset++)
            {
                int index = (cursor + offset) % count;
                if (pool[index] == null)
                {
                    pool[index] = FxTemplates.Create(name, root.transform);
                    cursor = (index + 1) % Capacity;
                    return pool[index];
                }
                if (pool[index].activeSelf) continue;
                cursor = (index + 1) % Capacity;
                pool[index].SetActive(true);
                return pool[index];
            }
            if (count < Capacity)
            {
                var go = FxTemplates.Create(name, root.transform);
                pool[count++] = go;
                cursor = count % Capacity;
                return go;
            }
            int steal = cursor;
            cursor = (cursor + 1) % Capacity;
            pool[steal].SetActive(true);
            return pool[steal];
        }

        static void EnsureRoot()
        {
            if (root != null) return;
            root = new GameObject("Frontier FX Transient Pool");
            root.AddComponent<FxTransientPoolLifetime>();
        }

        public static void SetColor(Renderer renderer, Color color)
        {
            if (renderer == null) return;
            if (properties == null) properties = new MaterialPropertyBlock();
            properties.Clear();
            properties.SetColor("_Color", color);
            renderer.SetPropertyBlock(properties);
        }

        public static void Release(GameObject effect)
        {
            if (effect != null) effect.SetActive(false);
        }

        public static void ReleaseGlowQuad(GameObject effect, bool pooled)
        {
            if (effect == null) return;
            if (pooled && root != null && effect.transform.parent == root.transform)
            {
                effect.SetActive(false);
                return;
            }
            Object.Destroy(effect);
        }

        public static void SetGlowQuadSize(Transform glow, float size)
        {
            if (glow == null) return;
            var follower = glow.GetComponent<GlowQuadFollower>();
            if (follower != null) follower.SetSize(size);
        }

        static void Clear(GameObject owner)
        {
            if (root != owner) return;
            root = null;
            boltCount = rippleCount = boltCursor = rippleCursor = 0;
            glowCount = glowCursor = 0;
            for (int i = 0; i < Capacity; i++) { Bolts[i] = null; Ripples[i] = null; }
            for (int i = 0; i < GlowCapacity; i++) GlowQuads[i] = null;
            properties = null;
        }

        sealed class FxTransientPoolLifetime : MonoBehaviour
        {
            void OnDestroy() { Clear(gameObject); }
        }
    }

    public sealed class GlowQuadFollower : MonoBehaviour
    {
        Transform owner;
        Vector3 localPosition;
        float size;
        bool pooled;
        MeshRenderer targetRenderer;

        public void Configure(Transform source, Color color, float quadSize, Vector3 offset, bool cached)
        {
            owner = source;
            localPosition = offset;
            size = quadSize;
            pooled = cached;
            targetRenderer = GetComponent<MeshRenderer>();
            FxTransientPool.SetColor(targetRenderer, color);
            UpdatePose();
        }

        public void SetSize(float quadSize)
        {
            size = quadSize;
            UpdatePose();
        }

        void LateUpdate()
        {
            if (owner == null)
            {
                FxTransientPool.ReleaseGlowQuad(gameObject, pooled);
                return;
            }
            UpdatePose();
        }

        void UpdatePose()
        {
            if (owner == null) return;
            targetRenderer.enabled = owner.gameObject.activeInHierarchy;
            transform.position = owner.TransformPoint(localPosition);
            var camera = Camera.main;
            transform.rotation = camera != null ? camera.transform.rotation : Quaternion.identity;
            transform.localScale = Vector3.Scale(owner.lossyScale, Vector3.one * size);
        }
    }

    public static partial class Sfx
    {
        public const int SynthClipCapacity = 64;
        static readonly SynthClip[] cache = new SynthClip[SynthClipCapacity];
        static int nextSynthClip;

        struct SynthClip
        {
            public float frequency, duration, sweep;
            public AudioClip clip;
        }

        public static int CachedSynthClipCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < cache.Length; i++) if (cache[i].clip != null) count++;
                return count;
            }
        }

        public static void Play(float freq, float dur = 0.12f, float vol = 0.25f, float sweep = 0f)
        {
            PlaySynth(freq, dur, vol, sweep, AudioBus.Sfx);
        }

        static void PlaySynth(float freq, float dur, float vol, float sweep, AudioBus bus)
        {
            if (service == null || Muted) return;
            AudioClip clip = null;
            for (int i = 0; i < cache.Length; i++)
            {
                var entry = cache[i];
                if (entry.clip != null && entry.frequency == freq && entry.duration == dur && entry.sweep == sweep)
                {
                    clip = entry.clip;
                    break;
                }
            }
            if (clip == null)
            {
                int slot = nextSynthClip;
                // Never destroy a clip while a pooled voice is still playing it.
                while (service.IsPlaying(cache[slot].clip)) slot = (slot + 1) % cache.Length;
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
                if (cache[slot].clip != null)
                {
                    if (Application.isPlaying) Object.Destroy(cache[slot].clip);
                    else Object.DestroyImmediate(cache[slot].clip);
                }
                cache[slot] = new SynthClip { frequency = freq, duration = dur, sweep = sweep, clip = clip };
                nextSynthClip = (slot + 1) % cache.Length;
            }
            service.Play(clip, vol, bus: bus);
        }

        static void Cue(AudioCue cue) { if (service != null) service.PlayCue(cue); }
        public static void Click() { Cue(AudioCue.Click); }
        public static void Good() { Cue(AudioCue.Good); }
        public static void Bad() { Cue(AudioCue.Bad); }
        public static void Zap() { Cue(AudioCue.Zap); }
        public static void Boom() { Cue(AudioCue.Boom); }
        public static void Warp() { Cue(AudioCue.Warp); }
        public static void ShipIgnition() { Cue(AudioCue.ShipIgnition); }
        public static void ShipTouchdown() { Cue(AudioCue.ShipTouchdown); }
    }
}
