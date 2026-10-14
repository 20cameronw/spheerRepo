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
        public int SlotCount { get { return view.Current.BuildingSlots; } }
        public bool ShowStructureLabels = true;
        const float OrbitHeight = 1.0f;

        public event Action<PlacedStructure, Transform> StructureTapped;
        public event Action<int, Transform> SlotTapped;

        public Func<Vector2, bool> IsOverUi;
        public bool Locked;
        /// <summary>The currently selected structure anchor (gets a stronger pulse).</summary>
        public Transform Selected;
        public bool BuildMode { get { return buildMode; } set { if (buildMode != value) { buildMode = value; RebuildPads(); } } }

        PlanetView view;
        Camera cam;
        GameState gs;
        bool buildMode;
        float placedRadius = -1f;
        Vector3[] dirs;
        readonly List<Item> items = new List<Item>();
        readonly List<Item> pads = new List<Item>();
        string signature = "";
        static Font font;
        static Shader litShader;
        Material outlineMaterial;

        class Item
        {
            public Transform t;
            public Transform label;
            public PlacedStructure structure;
            public int slot;
            public float phase;
            public Transform glow;
            public Renderer labelRenderer;
            public bool orbit;
            public readonly List<Renderer> outlines = new List<Renderer>();
            public readonly List<Material> materials = new List<Material>();
        }

        // drag state
        bool down, dragging, blocked;
        Vector2 downPos, lastPos;
        float lastTime;

        public void Init(PlanetView pv, Camera camera, GameState state)
        {
            view = pv; cam = camera; gs = state;
            view.Rebuilt += OnPlanet;
            gs.Changed += OnChanged;
            if (view.Spin != null) OnPlanet(view.Current);
        }

        void OnDestroy()
        {
            if (view != null) view.Rebuilt -= OnPlanet;
            if (gs != null) gs.Changed -= OnChanged;
            ClearAll();
            if (outlineMaterial != null) Destroy(outlineMaterial);
        }

        public List<KeyValuePair<PlacedStructure, Transform>> TurretAnchors()
        {
            var list = new List<KeyValuePair<PlacedStructure, Transform>>();
            foreach (var it in items)
                if (it.structure != null && gs.IsOperational(it.structure) && it.structure.def.kind.IsTurret() && it.t != null)
                    list.Add(new KeyValuePair<PlacedStructure, Transform>(it.structure, it.t));
            return list;
        }

        public bool SlotOccupied(int slot)
        {
            return gs.StructureAt(gs.planet, slot) != null;
        }

        // ------------------------------------------------------------ build
        void OnPlanet(PlanetData p)
        {
            int count = p.BuildingSlots;
            foreach (var structure in gs.structures)
                if (structure.planetId == p.Id) count = Mathf.Max(count, structure.slot + 1);
            dirs = SlotDirections(count);
            signature = null;
            ClearAll();
            RebuildStructures();
            RebuildPads();
        }

        public static Vector3[] SlotDirections(int count)
        {
            var directions = new Vector3[Mathf.Max(12, count)];
            // Preserve the original twelve slots for old saves. Additional pads
            // split the largest available gaps without moving existing buildings.
            for (int i = 0; i < 12; i++)
            {
                float y = Mathf.Lerp(0.78f, -0.78f, (i + 0.5f) / 12f);
                float r = Mathf.Sqrt(1f - y * y), a = i * 2.39996323f;
                directions[i] = new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r).normalized;
            }
            for (int i = 12; i < directions.Length; i++)
            {
                float best = -1f;
                for (int candidate = 0; candidate < 512; candidate++)
                {
                    float y = 1f - 2f * (candidate + 0.5f) / 512f;
                    float r = Mathf.Sqrt(1f - y * y), a = candidate * 2.39996323f;
                    Vector3 direction = new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
                    float gap = float.MaxValue;
                    for (int j = 0; j < i; j++) gap = Mathf.Min(gap, (direction - directions[j]).sqrMagnitude);
                    if (gap > best) { best = gap; directions[i] = direction; }
                }
            }
            return directions;
        }

        void OnChanged()
        {
            if (view == null || view.Spin == null) return;
            // The state moves to a new world before the planet view shows it: until Rebuilt fires the pad layout still belongs to the previous world,
            // and the new world may have buildings on pads that layout does not have.
            if (view.Current == null || view.Current.Id != gs.planet.Id) return;
            RebuildStructures();
            if (buildMode) RebuildPads();
        }

        void ClearAll()
        {
            foreach (var i in items) DestroyItem(i);
            foreach (var i in pads) if (i.t != null) Destroy(i.t.gameObject);
            items.Clear(); pads.Clear();
        }

        void DestroyItem(Item item)
        {
            foreach (var material in item.materials) if (material != null) Destroy(material);
            if (item.t != null) Destroy(item.t.gameObject);
        }

        void RebuildStructures()
        {
            var sb = new StringBuilder(gs.planet.Id);
            foreach (var s in gs.structures)
                if (s.planetId == gs.planet.Id)
                    sb.Append('|').Append(s.def.id).Append(s.slot).Append(':').Append(s.level)
                        .Append(':').Append(s.record.buildEndUtc).Append(':').Append(s.record.upgradeEndUtc)
                        .Append(':').Append(s.record.pendingUpgradeLevel);
            string sig = sb.ToString();
            if (sig == signature) return;
            signature = sig;
            for (int i = items.Count - 1; i >= 0; i--)
                if (items[i].structure != null) { DestroyItem(items[i]); items.RemoveAt(i); }
            foreach (var s in gs.structures)
            {
                if (s.planetId != gs.planet.Id) continue;
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
            go.transform.localPosition = n * view.SurfaceRadius();
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, n);
            var col = go.AddComponent<SphereCollider>();
            go.transform.localScale = Vector3.one * view.Current.StructureScale;
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
            if (model == null)
            {
                var visual = new GameObject("StructureVisual");
                BuildFallbackStructure(visual.transform, s.def.kind, c);
                Bounds bounds;
                if (ModelLibrary.MeshBounds(visual, out bounds))
                {
                    float diameter = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
                    float scale = 0.75f / Mathf.Max(0.0001f, diameter);
                    visual.transform.localScale = Vector3.one * scale;
                    visual.transform.SetParent(it.t, false);
                    visual.transform.localPosition = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z) * scale;
                }
                else
                {
                    Debug.LogError("Frontier: fallback structure has no geometry: " + s.def.id, this);
                    Destroy(visual);
                }
            }
            PrepareStructure(it, c);
            if (s.def.kind == StructureKind.Drill) DrillDust(it.t, c);
            string label = gs.IsUnderConstruction(s) ? s.def.displayName.ToUpper() + "  BUILDING"
                : gs.IsUpgradeInProgress(s) ? s.def.displayName.ToUpper() + "  UPGRADING"
                : s.def.displayName.ToUpper() + "  LV" + s.level;
            it.label = MakeLabel(it.t, label, Fx.Displayable(c), 1.0f, 0.34f, out it.labelRenderer);
            it.phase = s.slot * 0.4f;
            it.orbit = s.def.kind == StructureKind.Satellite;
            return it;
        }

        void PrepareStructure(Item item, Color color)
        {
            if (outlineMaterial == null)
            {
                var shader = Resources.Load<Shader>("Frontier/StructureOutline");
                if (shader == null) { Debug.LogError("Frontier: StructureOutline shader is missing.", this); return; }
                outlineMaterial = new Material(shader);
            }
            foreach (var renderer in item.t.GetComponentsInChildren<MeshRenderer>())
            {
                var source = renderer.sharedMaterials;
                var materials = new Material[source.Length];
                for (int i = 0; i < source.Length; i++)
                {
                    var src = source[i];
                    Color tint = src != null && src.HasProperty("_BaseColor") ? src.GetColor("_BaseColor")
                        : src != null && src.HasProperty("_Color") ? src.GetColor("_Color") : Color.gray;
                    var material = Lit(Fx.Displayable(tint));
                    if (src != null && src.HasProperty("_BaseMap"))
                        material.SetTexture("_BaseMap", src.GetTexture("_BaseMap"));
                    material.SetFloat("_Smoothness", 0.25f);
                    materials[i] = material;
                    item.materials.Add(material);
                }
                renderer.sharedMaterials = materials;
                var mesh = renderer.GetComponent<MeshFilter>();
                if (mesh == null || mesh.sharedMesh == null) continue;
                var go = new GameObject("SelectionOutline");
                go.transform.SetParent(renderer.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh.sharedMesh;
                var outline = go.AddComponent<MeshRenderer>();
                var shells = new Material[mesh.sharedMesh.subMeshCount];
                for (int i = 0; i < shells.Length; i++) shells[i] = outlineMaterial;
                outline.sharedMaterials = shells;
                outline.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                outline.receiveShadows = false;
                var props = new MaterialPropertyBlock();
                props.SetColor("_Color", Fx.Displayable(color) * 2f);
                props.SetFloat("_Width", 0.018f * view.Current.StructureScale);
                outline.SetPropertyBlock(props);
                outline.enabled = false;
                item.outlines.Add(outline);
            }
        }

        // Models are fitted in an unrotated, unscaled holder so bounds are correct, then moved onto the surface.
        Transform SpawnModel(string key, Transform parent, float size)
        {
            if (string.IsNullOrEmpty(key)) return null;
            var holder = new GameObject("ModelHolder");
            holder.transform.position = new Vector3(5000f, 5000f, 5000f);
            var m = ModelLibrary.Spawn(key, holder.transform, size);
            if (m == null) { Destroy(holder); return null; }
            Bounds b;
            float lift = ModelLibrary.MeshBounds(m.gameObject, out b) ? holder.transform.position.y - b.min.y : size * 0.5f;
            m.SetParent(parent, false);
            m.localPosition += new Vector3(0f, lift, 0f);
            m.localRotation = Quaternion.identity;
            Destroy(holder);
            return m;
        }

        // Small, subtle grit-and-spark plume at the drill's base (replaces the prefab's oversized effect).
        static void DrillDust(Transform t, Color c)
        {
            var go = new GameObject("DrillDust");
            go.transform.SetParent(t, false);
            go.transform.localPosition = new Vector3(0f, 0.04f, 0f);
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.055f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1.6f, 1.1f, 0.5f, 1f), new Color(c.r, c.g, c.b, 1f));
            main.gravityModifier = 0.25f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = 40;
            var em = ps.emission; em.rateOverTime = 14f;
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = 40f; sh.radius = 0.05f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var sz = ps.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Fx.AddMat(Color.white, Fx.Glow);
            ps.Play();
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
            if (kind == StructureKind.Warehouse)
            {
                Prim(t, PrimitiveType.Cube, new Vector3(0f, 0.25f, 0f), new Vector3(0.65f, 0.5f, 0.55f), metal);
                Prim(t, PrimitiveType.Cube, new Vector3(0f, 0.53f, 0f), new Vector3(0.73f, 0.08f, 0.63f), glow);
                Prim(t, PrimitiveType.Cube, new Vector3(0f, 0.18f, -0.285f), new Vector3(0.25f, 0.34f, 0.025f), glow);
                for (int i = 0; i < 3; i++)
                    Prim(t, PrimitiveType.Cube, new Vector3(-0.2f + i * 0.2f, 0.25f, -0.3f), new Vector3(0.025f, 0.42f, 0.03f), metal);
                return;
            }
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

        /// <summary>True when the point is on screen and not hidden behind the planet body.</summary>
        public bool Visible(Vector3 worldPos)
        {
            Vector3 vp = cam.WorldToViewportPoint(worldPos);
            if (vp.z <= 0f || vp.x < -0.02f || vp.x > 1.02f || vp.y < -0.02f || vp.y > 1.02f) return false;
            Vector3 o = cam.transform.position;
            Vector3 d = worldPos - o;
            float dist = d.magnitude;
            if (dist < 0.0001f) return true;
            d /= dist;
            Vector3 c = view.Spin.position;
            float r = view.SurfaceRadius() * 0.97f;
            Vector3 oc = o - c;
            float b = Vector3.Dot(oc, d);
            float disc = b * b - (oc.sqrMagnitude - r * r);
            if (disc <= 0f) return true;
            float tHit = -b - Mathf.Sqrt(disc);
            return tHit <= 0f || tHit >= dist - 0.02f;
        }

        Vector3 ProbePoint(Item it)
        {
            return it.t.TransformPoint(Vector3.up * 0.3f);
        }

        public bool AnchorVisible(Transform t)
        {
            return t != null && Visible(t.TransformPoint(Vector3.up * 0.3f));
        }

        void Update()
        {
            if (view == null || view.Spin == null || cam == null) return;
            float r = view.SurfaceRadius();
            if (!Mathf.Approximately(r, placedRadius))
            {
                placedRadius = r;
                foreach (var it in items) if (it.t != null && !it.orbit) it.t.localPosition = dirs[it.slot] * r;
                foreach (var it in pads) if (it.t != null) it.t.localPosition = dirs[it.slot] * r;
            }
            AnimateItems(items);
            AnimateItems(pads);
            HandleInput();
        }

        void AnimateItems(List<Item> list)
        {
            foreach (var it in list)
            {
                if (it.t == null) continue;
                if (it.orbit && !view.Frozen)
                {
                    Vector3 n = dirs[it.slot];
                    Vector3 axis = Vector3.Cross(n, Vector3.up);
                    if (axis.sqrMagnitude < 0.01f) axis = Vector3.right;
                    Vector3 pos = Quaternion.AngleAxis(Time.time * 18f + it.slot * 40f, axis.normalized) * n * (view.SurfaceRadius() + OrbitHeight);
                    it.t.localPosition = pos;
                    it.t.localRotation = Quaternion.FromToRotation(Vector3.up, pos.normalized);
                }
                bool facing = Visible(ProbePoint(it));
                bool selected = Selected != null && Selected == it.t;
                foreach (var outline in it.outlines) outline.enabled = selected && !Locked;
                float bob = 0.04f * Mathf.Sin(Time.time * 2.2f + it.phase);
                if (it.label != null)
                {
                    it.label.localPosition = new Vector3(0f, (it.structure != null ? 1.0f : 0.6f) + bob, 0f);
                    it.labelRenderer.enabled = facing && !Locked && (it.structure == null || ShowStructureLabels || selected);
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
                downPos = lastPos = pos; lastTime = Time.unscaledTime;
                blocked = IsOverUi != null && IsOverUi(pos);
            }
            else if (down && Input.GetMouseButton(0) && !blocked)
            {
                if (!dragging && !view.Frozen && (pos - downPos).magnitude > 20f) { dragging = true; view.BeginDrag(); lastPos = pos; lastTime = Time.unscaledTime; }
                if (dragging)
                {
                    float dt = Mathf.Max(0.001f, Time.unscaledTime - lastTime);
                    view.Drag(pos - lastPos, dt);
                    lastPos = pos; lastTime = Time.unscaledTime;
                }
            }
            if (down && Input.GetMouseButtonUp(0))
            {
                down = false;
                if (blocked) return;
                if (dragging) view.EndDrag();
                else if ((pos - downPos).magnitude <= 40f) Pick(pos);
            }
        }

        void Pick(Vector2 screen)
        {
            var ray = cam.ScreenPointToRay(screen);
            var hits = Physics.RaycastAll(ray, 100f);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                foreach (var it in pads) if (it.t == h.transform && Visible(ProbePoint(it))) { Tap(it); return; }
                foreach (var it in items) if (it.t == h.transform && Visible(ProbePoint(it))) { Tap(it); return; }
            }

            // Generous screen-space fallback: anything visible near the tap counts.
            float best = 95f * Mathf.Max(0.6f, ScreenMetrics.Height / 1080f);
            Item pick = null;
            foreach (var list in new[] { items, pads })
                foreach (var it in list)
                {
                    if (it.t == null) continue;
                    Vector3 p = ProbePoint(it);
                    if (!Visible(p)) continue;
                    Vector3 sp = cam.WorldToScreenPoint(p);
                    float d = (new Vector2(sp.x, sp.y) - screen).magnitude;
                    if (d < best) { best = d; pick = it; }
                }
            if (pick != null) Tap(pick);
        }

        void Tap(Item it)
        {
            Sfx.Click();
            if (it.structure != null) { if (StructureTapped != null) StructureTapped(it.structure, it.t); }
            else if (SlotTapped != null) SlotTapped(it.slot, it.t);
        }

        /// <summary>Taps a placed structure exactly as a finger would (harness and tests).</summary>
        public bool SimulateTap(PlacedStructure structure)
        {
            foreach (var it in items)
                if (it.structure == structure && it.t != null) { Tap(it); return true; }
            return false;
        }
    }
}
