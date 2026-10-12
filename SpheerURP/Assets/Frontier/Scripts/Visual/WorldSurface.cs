using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Frontier
{
    /// <summary>
    /// Everything that sits on the planet surface: placed structures,
    /// build pads, floating 3D labels, drag-to-rotate and tap picking.
    /// </summary>
    public class WorldSurface : MonoBehaviour
    {
        public const int SlotCount = 12;

        public event Action<PlacedStructure, Transform> StructureTapped;
        public event Action<int, Transform> SlotTapped;

        public Func<Vector2, bool> IsOverUi;
        public bool Locked;
        public bool BuildMode { get { return buildMode; } set { if (buildMode != value) { buildMode = value; RebuildPads(); } } }

        PlanetView view;
        Camera cam;
        GameState gs;
        bool buildMode;
        Vector3[] dirs;
        readonly List<Item> items = new List<Item>();
        readonly List<Item> pads = new List<Item>();
        string signature = "";
        static Font font;
        static Shader litShader;

        class Item
        {
            public Transform t;
            public Transform label;
            public PlacedStructure structure;
            public int slot;
            public float phase;
            public Transform glow;
            public Renderer labelRenderer;
        }

        // drag state
        bool down, dragging, blocked;
        Vector2 downPos, lastPos;
        float lastTime;
        float velocity;

        public void Init(PlanetView pv, Camera camera, GameState state)
        {
            view = pv; cam = camera; gs = state;
            dirs = new Vector3[SlotCount];
            for (int i = 0; i < SlotCount; i++)
            {
                float y = Mathf.Lerp(0.78f, -0.78f, (i + 0.5f) / SlotCount);
                float r = Mathf.Sqrt(1f - y * y);
                float a = i * 2.39996323f;
                dirs[i] = new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r).normalized;
            }
            view.Rebuilt += OnPlanet;
            gs.Changed += OnChanged;
            if (view.Spin != null) OnPlanet(view.Current);
        }

        void OnDestroy()
        {
            if (view != null) view.Rebuilt -= OnPlanet;
            if (gs != null) gs.Changed -= OnChanged;
        }

        public List<KeyValuePair<PlacedStructure, Transform>> TurretAnchors()
        {
            var list = new List<KeyValuePair<PlacedStructure, Transform>>();
            foreach (var it in items)
                if (it.structure != null && it.structure.def.kind.IsTurret() && it.t != null)
                    list.Add(new KeyValuePair<PlacedStructure, Transform>(it.structure, it.t));
            return list;
        }

        public bool SlotOccupied(int slot)
        {
            return gs.StructureAt(gs.planet, slot) != null;
        }

        // ------------------------------------------------------------ build
        void OnPlanet(PlanetSO p)
        {
            signature = null;
            ClearAll();
            RebuildStructures();
            RebuildPads();
        }

        void OnChanged()
        {
            if (view == null || view.Spin == null) return;
            RebuildStructures();
            if (buildMode) RebuildPads();
        }

        void ClearAll()
        {
            foreach (var i in items) if (i.t != null) Destroy(i.t.gameObject);
            foreach (var i in pads) if (i.t != null) Destroy(i.t.gameObject);
            items.Clear(); pads.Clear();
        }

        void RebuildStructures()
        {
            var sb = new StringBuilder(gs.planet.id);
            foreach (var s in gs.structures) if (s.planetId == gs.planet.id) sb.Append('|').Append(s.def.id).Append(s.slot).Append(':').Append(s.level);
            string sig = sb.ToString();
            if (sig == signature) return;
            signature = sig;
            for (int i = items.Count - 1; i >= 0; i--)
                if (items[i].structure != null) { Destroy(items[i].t.gameObject); items.RemoveAt(i); }
            foreach (var s in gs.structures)
            {
                if (s.planetId != gs.planet.id) continue;
                items.Add(MakeStructure(s));
            }
            if (buildMode) RebuildPads();
        }

        void RebuildPads()
        {
            foreach (var p in pads) if (p.t != null) Destroy(p.t.gameObject);
            pads.Clear();
            if (!buildMode || view == null || view.Spin == null) return;
            for (int i = 0; i < SlotCount; i++)
            {
                if (SlotOccupied(i)) continue;
                var it = NewItem(i, "Pad");
                it.phase = i;
                var ring = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(ring.GetComponent<Collider>());
                ring.transform.SetParent(it.t, false);
                ring.transform.localPosition = new Vector3(0f, 0.18f, 0f);
                ring.transform.localScale = Vector3.one * 0.9f;
                ring.GetComponent<MeshRenderer>().sharedMaterial = Fx.AddMat(new Color(0.2f, 2.2f, 2.4f, 1f), Fx.Ring);
                ring.AddComponent<Billboard>();
                it.glow = Fx.GlowQuad(it.t, new Color(0.1f, 1f, 1.1f, 0.6f), 1.6f, new Vector3(0f, 0.18f, 0f)).transform;
                it.label = MakeLabel(it.t, "+", new Color(0.4f, 1f, 1f), 0.6f, 0.55f, out it.labelRenderer);
                pads.Add(it);
            }
        }

        Item NewItem(int slot, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(view.Spin, false);
            Vector3 n = dirs[slot];
            go.transform.localPosition = n * PlanetView.Radius;
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, n);
            var col = go.AddComponent<SphereCollider>();
            col.radius = 0.42f;
            col.center = new Vector3(0f, 0.3f, 0f);
            return new Item { t = go.transform, slot = slot };
        }

        Item MakeStructure(PlacedStructure s)
        {
            var it = NewItem(s.slot, "Struct_" + s.def.id);
            it.structure = s;
            Color c = s.def.color;
            var model = SpawnModel(s.def.modelKey, it.t, 0.75f);
            if (model == null) BuildFallbackStructure(it.t, s.def.kind, c);
            it.glow = Fx.GlowQuad(it.t, new Color(c.r, c.g, c.b, 0.5f), 1.8f, new Vector3(0f, 0.3f, 0f)).transform;
            it.label = MakeLabel(it.t, s.def.displayName.ToUpper() + "  LV" + s.level, UiKit.Ui(c), 1.0f, 0.34f, out it.labelRenderer);
            it.phase = s.slot * 0.4f;
            return it;
        }

        // Models are fitted in an unrotated, unscaled holder so bounds are correct, then moved onto the surface.
        Transform SpawnModel(string key, Transform parent, float size)
        {
            if (string.IsNullOrEmpty(key)) return null;
            var holder = new GameObject("ModelHolder");
            holder.transform.position = new Vector3(5000f, 5000f, 5000f);
            var m = ModelLibrary.Spawn(key, holder.transform, size);
            if (m == null) { Destroy(holder); return null; }
            m.SetParent(parent, false);
            m.localPosition = new Vector3(0f, size * 0.5f, 0f);
            m.localRotation = Quaternion.identity;
            Destroy(holder);
            return m;
        }

        static Material Lit(Color c, float emission = 0f)
        {
            if (litShader == null) litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader == null) litShader = Shader.Find("Sprites/Default");
            var m = new Material(litShader);
            m.SetColor("_BaseColor", c);
            m.color = c;
            if (emission > 0f && m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", c * emission);
            }
            return m;
        }

        static Transform Prim(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Material m, Vector3? euler = null)
        {
            var g = GameObject.CreatePrimitive(type);
            Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localScale = scale;
            if (euler.HasValue) g.transform.localEulerAngles = euler.Value;
            g.GetComponent<MeshRenderer>().sharedMaterial = m;
            return g.transform;
        }

        static void BuildFallbackStructure(Transform t, StructureKind kind, Color c)
        {
            var metal = Lit(new Color(0.45f, 0.5f, 0.6f), 0.15f);
            var glow = Lit(c, 1.2f);
            if (kind == StructureKind.Lumber)
            {
                var bark = Lit(new Color(0.35f, 0.2f, 0.1f));
                var leaf = Lit(new Color(0.15f, 0.65f, 0.3f), 0.15f);
                Prim(t, PrimitiveType.Cylinder, new Vector3(0f, 0.16f, 0f), new Vector3(0.1f, 0.16f, 0.1f), bark);
                Prim(t, PrimitiveType.Sphere, new Vector3(0f, 0.42f, 0f), new Vector3(0.42f, 0.36f, 0.42f), leaf);
                Prim(t, PrimitiveType.Sphere, new Vector3(0.03f, 0.62f, 0f), new Vector3(0.3f, 0.28f, 0.3f), leaf);
                return;
            }
            Prim(t, PrimitiveType.Cylinder, new Vector3(0f, 0.2f, 0f), new Vector3(0.34f, 0.2f, 0.34f), metal);
            if (kind == StructureKind.Refinery || kind == StructureKind.Satellite)
                Prim(t, PrimitiveType.Sphere, new Vector3(0f, 0.52f, 0f), new Vector3(0.36f, 0.36f, 0.36f), glow);
            else if (kind.IsTurret())
                Prim(t, PrimitiveType.Cylinder, new Vector3(0f, 0.6f, 0f), new Vector3(0.1f, kind == StructureKind.Laser ? 0.3f : 0.2f, 0.1f), glow);
            else
                Prim(t, PrimitiveType.Cylinder, new Vector3(0f, 0.52f, 0f), new Vector3(0.12f, 0.22f, 0.12f), glow);
        }

        // ------------------------------------------------------------ labels
        Transform MakeLabel(Transform parent, string text, Color color, float height, float size, out Renderer renderer)
        {
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, height, 0f);
            var tm = go.AddComponent<TextMesh>();
            tm.font = font;
            tm.text = text;
            tm.fontSize = 64;
            tm.characterSize = size * 0.1f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = font.material;
            go.AddComponent<Billboard>();
            return go.transform;
        }

        // ------------------------------------------------------------ frame
        public bool Facing(Vector3 worldPos)
        {
            Vector3 c = view.Spin.position;
            return Vector3.Dot((worldPos - c).normalized, (cam.transform.position - c).normalized) > 0.18f;
        }

        void Update()
        {
            if (view == null || view.Spin == null || cam == null) return;
            AnimateItems(items);
            AnimateItems(pads);
            HandleInput();
        }

        void AnimateItems(List<Item> list)
        {
            foreach (var it in list)
            {
                if (it.t == null) continue;
                bool facing = Facing(it.t.position);
                float bob = 0.04f * Mathf.Sin(Time.time * 2.2f + it.phase);
                if (it.label != null)
                {
                    it.label.localPosition = new Vector3(0f, (it.structure != null ? 1.0f : 0.6f) + bob, 0f);
                    it.labelRenderer.enabled = facing && !Locked;
                }
                if (it.glow != null)
                {
                    float k = 1f + 0.12f * Mathf.Sin(Time.time * 3f + it.phase);
                    it.glow.localScale = Vector3.one * (it.structure != null ? 1.8f : 1.6f) * k;
                    it.glow.gameObject.SetActive(facing);
                }
            }
        }

        // ------------------------------------------------------------ input
        void HandleInput()
        {
            if (Locked) { down = false; return; }
            Vector2 pos = Input.mousePosition;
            if (Input.GetMouseButtonDown(0))
            {
                down = true; dragging = false;
                downPos = lastPos = pos; lastTime = Time.unscaledTime; velocity = 0f;
                blocked = IsOverUi != null && IsOverUi(pos);
            }
            else if (down && Input.GetMouseButton(0) && !blocked)
            {
                if (!dragging && (pos - downPos).magnitude > 20f) dragging = true;
                if (dragging)
                {
                    float dx = pos.x - lastPos.x;
                    float deg = dx * 0.35f;
                    view.AddSpin(deg);
                    float dt = Mathf.Max(0.001f, Time.unscaledTime - lastTime);
                    velocity = Mathf.Lerp(velocity, deg / dt, 0.4f);
                    lastPos = pos; lastTime = Time.unscaledTime;
                }
            }
            if (down && Input.GetMouseButtonUp(0))
            {
                down = false;
                if (blocked) return;
                if (dragging) view.SetSpinVelocity(Mathf.Clamp(velocity, -360f, 360f));
                else Pick(pos);
            }
        }

        void Pick(Vector2 screen)
        {
            var ray = cam.ScreenPointToRay(screen);
            var hits = Physics.RaycastAll(ray, 100f);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                if (!Facing(h.transform.position)) continue;
                foreach (var it in pads) if (it.t == h.transform) { Sfx.Click(); if (SlotTapped != null) SlotTapped(it.slot, it.t); return; }
                foreach (var it in items)
                {
                    if (it.t != h.transform) continue;
                    Sfx.Click();
                    if (it.structure != null) { if (StructureTapped != null) StructureTapped(it.structure, it.t); }
                    return;
                }
            }
        }
    }
}
