using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

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
        public event Action<int, Transform> ObstacleTapped;

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
        readonly List<Item> obstacles = new List<Item>();
        string signature = "";
        string obstacleSignature = "";
        PanelSettings labelPanel;
        static Shader litShader;
        Material outlineMaterial;

        class Item
        {
            public Transform t;
            public Transform label;
            public PlacedStructure structure;
            public bool obstacle;
            public int slot;
            public float phase;
            public Transform glow;
            public VisualElement labelElement;
            public bool orbit;
            public readonly List<Renderer> outlines = new List<Renderer>();
            public readonly List<Material> materials = new List<Material>();
            public Transform visual, scan;
            public string revision;
            public int level;
            public bool pending;
            public float momentAt = -10f;
            public bool buildIn;
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
            PresentationQuality.Changed += ApplyLabelBudget;
            if (view.Spin != null) OnPlanet(view.Current);
        }

        void OnDestroy()
        {
            if (view != null) view.Rebuilt -= OnPlanet;
            if (gs != null) gs.Changed -= OnChanged;
            PresentationQuality.Changed -= ApplyLabelBudget;
            ClearAll();
            if (labelPanel != null) Destroy(labelPanel);
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
            obstacleSignature = null;
            ClearAll();
            RebuildStructures();
            RebuildObstacles();
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
            RebuildObstacles();
            if (buildMode) RebuildPads();
        }

        void ClearAll()
        {
            foreach (var i in items) DestroyItem(i);
            foreach (var i in pads) if (i.t != null) Destroy(i.t.gameObject);
            foreach (var i in obstacles) DestroyItem(i);
            items.Clear(); pads.Clear(); obstacles.Clear();
            obstacleSignature = null;
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
            bool liveChange = signature != null;
            signature = sig;
            var previous = new Dictionary<int, Item>();
            foreach (var item in items) previous.Add(item.slot, item);
            items.Clear();
            foreach (var s in gs.structures)
            {
                if (s.planetId != gs.planet.Id) continue;
                string revision = StructureRevision(s);
                previous.TryGetValue(s.slot, out var old);
                if (old != null && old.revision == revision)
                {
                    old.structure = s;
                    items.Add(old);
                    previous.Remove(s.slot);
                    continue;
                }
                var item = MakeStructure(s);
                items.Add(item);
                if (liveChange)
                {
                    bool completed = old != null && old.pending && !item.pending;
                    bool upgraded = old != null && item.level > old.level;
                    if (old == null || completed || upgraded)
                    {
                        item.momentAt = Time.unscaledTime;
                        item.buildIn = old == null;
                        Vector3 position = item.t.TransformPoint(Vector3.up * 0.35f);
                        Color color = completed ? new Color(0.5f, 2f, 1f) : s.def.color;
                        Fx.Ripple(position, color, completed ? 1.8f : 1.1f, 0.6f);
                        Fx.Burst(position, color, completed ? 18 : 10, 1.4f, 0.1f);
                    }
                }
                if (old != null) { DestroyItem(old); previous.Remove(s.slot); }
            }
            foreach (var old in previous.Values) DestroyItem(old);
            if (buildMode) RebuildPads();
        }

        static string StructureRevision(PlacedStructure s)
        {
            return s.def.id + ":" + s.level + ":" + s.record.buildEndUtc + ":" +
                s.record.upgradeEndUtc + ":" + s.record.pendingUpgradeLevel;
        }

        void RebuildPads()
        {
            foreach (var p in pads) if (p.t != null) Destroy(p.t.gameObject);
            pads.Clear();
            if (!buildMode || view == null || view.Spin == null) return;
            var blocked = new HashSet<int>();
            foreach (var obstacle in gs.ObstaclesOn(gs.planet)) blocked.Add(obstacle.pad);
            for (int i = 0; i < SlotCount; i++)
            {
                if (SlotOccupied(i) || blocked.Contains(i)) continue;
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
                it.label = MakeLabel(it.t, "+", new Color(0.4f, 1f, 1f), 0.6f, true, out it.labelElement);
                pads.Add(it);
            }
        }

        void RebuildObstacles()
        {
            if (view == null || view.Spin == null || gs == null) return;
            var generated = gs.ObstaclesOn(gs.planet);
            var sb = new StringBuilder(gs.planet.Id);
            foreach (var obstacle in generated) sb.Append('|').Append(obstacle.pad).Append(':').Append(obstacle.kind);
            string sig = sb.ToString();
            if (sig == obstacleSignature) return;
            bool liveChange = obstacleSignature != null;
            obstacleSignature = sig;
            foreach (var item in obstacles)
            {
                bool remains = false;
                foreach (var obstacle in generated) if (obstacle.pad == item.slot) { remains = true; break; }
                if (liveChange && !remains && item.t != null)
                {
                    Vector3 position = item.t.TransformPoint(Vector3.up * 0.2f);
                    Fx.Burst(position, new Color(2f, 0.9f, 0.2f), 18, 2f, 0.12f);
                    Fx.Ripple(position, new Color(1f, 0.65f, 0.25f), 1.4f, 0.5f);
                }
                DestroyItem(item);
            }
            obstacles.Clear();
            foreach (var obstacle in generated)
                obstacles.Add(MakeObstacle(obstacle));
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
            it.revision = StructureRevision(s);
            it.level = s.level;
            it.pending = !string.IsNullOrEmpty(s.record.buildEndUtc) || !string.IsNullOrEmpty(s.record.upgradeEndUtc);
            it.visual = new GameObject("StructureVisualRoot").transform;
            it.visual.SetParent(it.t, false);
            it.visual.localPosition = Vector3.down * 0.045f;
            Color c = s.def.color;
            var model = SpawnModel(s.def.modelKey, it.visual, 0.75f);
            if (model == null)
            {
                var visual = new GameObject("StructureVisual");
                BuildFallbackStructure(visual.transform, s.def.kind, c, it.materials);
                Bounds bounds;
                if (ModelLibrary.MeshBounds(visual, out bounds))
                {
                    float diameter = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
                    float scale = 0.75f / Mathf.Max(0.0001f, diameter);
                    visual.transform.localScale = Vector3.one * scale;
                    visual.transform.SetParent(it.visual, false);
                    visual.transform.localPosition = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z) * scale;
                }
                else
                {
                    Debug.LogError("Frontier: fallback structure has no geometry: " + s.def.id, this);
                    Destroy(visual);
                }
            }
            PrepareStructure(it, c);
            if (it.pending)
            {
                var scan = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(scan.GetComponent<Collider>());
                scan.name = "ConstructionScan";
                scan.transform.SetParent(it.t, false);
                scan.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                scan.transform.localScale = Vector3.one;
                var scanMaterial = Fx.AddMat(new Color(0.2f, 1.4f, 2f), Fx.Ring);
                scan.GetComponent<MeshRenderer>().sharedMaterial = scanMaterial;
                it.materials.Add(scanMaterial);
                it.scan = scan.transform;
                foreach (var material in it.materials)
                    if (material.HasProperty("_BaseColor"))
                        material.SetColor("_BaseColor", Color.Lerp(material.GetColor("_BaseColor"), new Color(0.1f, 0.6f, 0.8f), 0.65f));
            }
            if (s.def.kind == StructureKind.Drill) DrillDust(it.t, c);
            string label = UiText.Get(gs.IsUnderConstruction(s) ? "world.label.building"
                : gs.IsUpgradeInProgress(s) ? "world.label.upgrading" : "world.label.level",
                s.def.LocalizedName.ToUpper(), s.level);
            it.label = MakeLabel(it.t, label, Fx.Displayable(c), 1.0f, false, out it.labelElement);
            it.phase = s.slot * 0.4f;
            it.orbit = s.def.kind == StructureKind.Satellite || s.def.kind == StructureKind.WarpGate ||
                s.def.kind == StructureKind.DysonCollector;
            return it;
        }

        Item MakeObstacle(Frontier.Economy.ObstacleInstance obstacle)
        {
            var it = NewItem(obstacle.pad, "Obstacle_" + obstacle.pad);
            it.obstacle = true;
            var kind = gs.ObstacleKind(obstacle);
            Transform model = kind != null ? SpawnModel(gs.db.obstacleKinds[obstacle.kind].modelKey, it.t, 0.72f) : null;
            if (model == null) BuildFallbackObstacle(it.t, obstacle.kind, it.materials);
            it.phase = obstacle.pad * 0.31f;
            return it;
        }

        static void BuildFallbackObstacle(Transform parent, int kind, List<Material> materials)
        {
            Color[] palettes =
            {
                new Color(0.38f, 0.42f, 0.5f), new Color(0.31f, 0.2f, 0.13f),
                new Color(0.25f, 0.58f, 0.52f), new Color(0.48f, 0.35f, 0.57f),
                new Color(0.6f, 0.3f, 0.2f), new Color(0.45f, 0.52f, 0.56f)
            };
            Color baseColor = palettes[Mathf.Abs(kind) % palettes.Length];
            var body = Lit(baseColor);
            var trim = Lit(Color.Lerp(baseColor, new Color(0.8f, 0.72f, 0.55f), 0.3f));
            materials.Add(body);
            materials.Add(trim);
            for (int i = 0; i < 4; i++)
            {
                float angle = i * 90f + (kind % 5) * 13f;
                float x = Mathf.Cos(angle * Mathf.Deg2Rad) * (0.16f + (i % 2) * 0.08f);
                float z = Mathf.Sin(angle * Mathf.Deg2Rad) * (0.12f + (i % 2) * 0.08f);
                Transform shard = Prim(parent, i % 2 == 0 ? PrimitiveType.Sphere : PrimitiveType.Capsule,
                    new Vector3(x, 0.14f + (i % 3) * 0.06f, z),
                    new Vector3(0.25f + (i % 2) * 0.08f, 0.22f + (i % 3) * 0.05f, 0.24f), i % 2 == 0 ? body : trim,
                    new Vector3(i * 17f, angle, i * 29f));
                shard.name = "Debris";
            }
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
                    bool preserveSurface = src != null &&
                        (src.renderQueue >= (int)UnityEngine.Rendering.RenderQueue.Transparent ||
                         src.IsKeywordEnabled("_EMISSION"));
                    var material = preserveSurface ? new Material(src) : Lit(Fx.Displayable(tint));
                    if (!preserveSurface && src != null && src.HasProperty("_BaseMap"))
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
            lift -= ModelLibrary.FoundationDepth(key) * m.localScale.y;
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

        static void BuildFallbackStructure(Transform t, StructureKind kind, Color c, List<Material> materials)
        {
            var metal = Lit(new Color(0.45f, 0.5f, 0.6f), 0.15f);
            var glow = Lit(c, 1.2f);
            materials.Add(metal); materials.Add(glow);
            if (kind == StructureKind.FuelDepot)
            {
                Prim(t, PrimitiveType.Cylinder, new Vector3(-0.2f, 0.27f, 0f), new Vector3(0.2f, 0.54f, 0.2f), metal);
                Prim(t, PrimitiveType.Cylinder, new Vector3(0.2f, 0.27f, 0f), new Vector3(0.2f, 0.54f, 0.2f), metal);
                Prim(t, PrimitiveType.Cylinder, new Vector3(0f, 0.56f, 0f), new Vector3(0.06f, 0.27f, 0.06f), glow, new Vector3(0, 0, 90));
                return;
            }
            if (kind == StructureKind.Warehouse)
            {
                Prim(t, PrimitiveType.Cylinder, new Vector3(0f, 0.05f, 0f), new Vector3(0.85f, 0.05f, 0.75f), metal);
                for (int i = 0; i < 3; i++)
                {
                    Prim(t, PrimitiveType.Capsule, new Vector3(-0.24f + i * 0.24f, 0.24f, 0),
                        new Vector3(0.2f, 0.3f, 0.2f), metal, new Vector3(90, 0, 0));
                    Prim(t, PrimitiveType.Cylinder, new Vector3(-0.24f + i * 0.24f, 0.24f, -0.24f),
                        new Vector3(0.14f, 0.015f, 0.14f), glow, new Vector3(90, 0, 0));
                }
                return;
            }
            if (kind == StructureKind.Lumber)
            {
                var bark = Lit(new Color(0.35f, 0.2f, 0.1f));
                var leaf = Lit(new Color(0.15f, 0.65f, 0.3f), 0.15f);
                materials.Add(bark); materials.Add(leaf);
                Prim(t, PrimitiveType.Cylinder, new Vector3(0f, 0.16f, 0f), new Vector3(0.1f, 0.16f, 0.1f), bark);
                Prim(t, PrimitiveType.Sphere, new Vector3(0f, 0.42f, 0f), new Vector3(0.42f, 0.36f, 0.42f), leaf);
                Prim(t, PrimitiveType.Sphere, new Vector3(0.03f, 0.62f, 0f), new Vector3(0.3f, 0.28f, 0.3f), leaf);
                return;
            }
            if (kind == StructureKind.ResearchLab || kind == StructureKind.RadarTower)
            {
                Prim(t, PrimitiveType.Cylinder, new Vector3(0, 0.2f, 0), new Vector3(0.35f, 0.2f, 0.35f), metal);
                Prim(t, PrimitiveType.Sphere, new Vector3(0, 0.45f, 0), new Vector3(0.65f, 0.38f, 0.65f), glow);
                Prim(t, PrimitiveType.Cylinder, new Vector3(0, 0.7f, 0), new Vector3(0.035f, 0.2f, 0.035f), metal);
                return;
            }
            if (kind == StructureKind.Foundry || kind == StructureKind.Fabricator)
            {
                for (int i = 0; i < 2; i++)
                {
                    Prim(t, PrimitiveType.Cylinder, new Vector3(-0.18f + i * 0.36f, 0.24f, 0), new Vector3(0.32f, 0.24f, 0.32f), metal);
                    Prim(t, PrimitiveType.Cylinder, new Vector3(-0.18f + i * 0.36f, 0.5f, 0), new Vector3(0.23f, 0.035f, 0.23f), glow);
                }
                return;
            }
            if (kind == StructureKind.Hangar || kind == StructureKind.CargoDepot || kind == StructureKind.ColonyHub)
            {
                Prim(t, PrimitiveType.Sphere, new Vector3(0, 0.15f, 0), new Vector3(0.85f, 0.55f, 0.7f), metal);
                Prim(t, PrimitiveType.Cylinder, new Vector3(0, 0.12f, -0.32f), new Vector3(0.35f, 0.025f, 0.35f), glow, new Vector3(90, 0, 0));
                for (int i = -1; i <= 1; i += 2)
                    Prim(t, PrimitiveType.Cylinder, new Vector3(i * 0.35f, 0.14f, 0), new Vector3(0.15f, 0.14f, 0.15f), metal);
                return;
            }
            if (kind == StructureKind.Biodome || kind == StructureKind.Terraformer)
            {
                Prim(t, PrimitiveType.Cylinder, new Vector3(0, 0.04f, 0), new Vector3(0.8f, 0.04f, 0.8f), metal);
                Prim(t, PrimitiveType.Sphere, new Vector3(0, 0.18f, 0), new Vector3(0.66f, 0.5f, 0.66f), glow);
                for (int i = -1; i <= 1; i += 2)
                    Prim(t, PrimitiveType.Capsule, new Vector3(i * 0.26f, 0.22f, 0), new Vector3(0.05f, 0.24f, 0.05f), metal, new Vector3(0, 0, i * 25));
                return;
            }
            if (kind == StructureKind.ChemReactor || kind == StructureKind.QuantumLab)
            {
                for (int i = 0; i < 3; i++)
                {
                    float angle = i * Mathf.PI * 2 / 3;
                    Vector3 pos = new Vector3(Mathf.Cos(angle) * 0.24f, 0.28f, Mathf.Sin(angle) * 0.24f);
                    Prim(t, PrimitiveType.Capsule, pos, new Vector3(0.22f, 0.27f, 0.22f), metal);
                    Prim(t, PrimitiveType.Sphere, pos + Vector3.up * 0.15f, Vector3.one * 0.18f, glow);
                }
                return;
            }
            if (kind == StructureKind.WarpGate || kind == StructureKind.DysonCollector)
            {
                for (int i = 0; i < 12; i++)
                {
                    float a = i * Mathf.PI * 2 / 12, b = (i + 1) * Mathf.PI * 2 / 12;
                    Vector3 start = new Vector3(Mathf.Cos(a) * 0.4f, 0.48f + Mathf.Sin(a) * 0.4f, 0);
                    Vector3 end = new Vector3(Mathf.Cos(b) * 0.4f, 0.48f + Mathf.Sin(b) * 0.4f, 0);
                    var segment = Prim(t, PrimitiveType.Cylinder, (start + end) * 0.5f,
                        new Vector3(0.07f, (end - start).magnitude * 0.5f, 0.07f), i % 3 == 0 ? glow : metal);
                    segment.localRotation = Quaternion.FromToRotation(Vector3.up, end - start);
                }
                return;
            }
            if (kind == StructureKind.ShieldPylon || kind == StructureKind.TeslaCoil)
            {
                Prim(t, PrimitiveType.Cylinder, new Vector3(0, 0.33f, 0), new Vector3(0.12f, 0.33f, 0.12f), metal);
                for (int i = 0; i < 3; i++)
                    Prim(t, PrimitiveType.Cylinder, new Vector3(0, 0.2f + i * 0.18f, 0), new Vector3(0.42f - i * 0.07f, 0.025f, 0.42f - i * 0.07f), glow);
                return;
            }
            if (kind == StructureKind.Mine || kind == StructureKind.Drill || kind == StructureKind.SiphonRig ||
                kind == StructureKind.Excavator || kind == StructureKind.SalvageYard || kind == StructureKind.AutoHarvester)
            {
                Prim(t, PrimitiveType.Cylinder, new Vector3(0, 0.04f, 0), new Vector3(0.7f, 0.04f, 0.65f), metal);
                for (int i = -1; i <= 1; i += 2)
                    Prim(t, PrimitiveType.Cylinder, new Vector3(i * 0.24f, 0.35f, 0), new Vector3(0.055f, 0.35f, 0.055f), metal);
                Prim(t, PrimitiveType.Capsule, new Vector3(0, 0.65f, 0), new Vector3(0.1f, 0.3f, 0.1f), metal, new Vector3(0, 0, 90));
                Prim(t, PrimitiveType.Cylinder, new Vector3(0, 0.34f, 0), new Vector3(0.15f, 0.24f, 0.15f), glow);
                for (int i = 0; i < 3; i++)
                    Prim(t, PrimitiveType.Sphere, new Vector3(0, 0.16f + i * 0.12f, 0),
                        new Vector3(0.28f - i * 0.04f, 0.06f, 0.28f - i * 0.04f), metal);
                return;
            }
            if (kind == StructureKind.RepairBay)
            {
                Prim(t, PrimitiveType.Cylinder, new Vector3(0, 0.06f, 0), new Vector3(0.8f, 0.06f, 0.8f), metal);
                Prim(t, PrimitiveType.Capsule, new Vector3(0, 0.18f, 0), new Vector3(0.1f, 0.28f, 0.1f), glow);
                Prim(t, PrimitiveType.Capsule, new Vector3(0, 0.18f, 0), new Vector3(0.1f, 0.28f, 0.1f), glow, new Vector3(0, 0, 90));
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
        Transform MakeLabel(Transform parent, string text, Color color, float height, bool pad, out VisualElement element)
        {
            if (labelPanel == null)
            {
                labelPanel = ScriptableObject.CreateInstance<PanelSettings>();
                labelPanel.name = "Frontier World Labels";
                labelPanel.renderMode = PanelRenderMode.WorldSpace;
                labelPanel.scaleMode = PanelScaleMode.ConstantPixelSize;
                labelPanel.themeStyleSheet = Resources.Load<ThemeStyleSheet>("Frontier/FrontierTheme");
                ApplyLabelBudget();
            }
            var go = new GameObject("Label");
            go.SetActive(false);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, height, 0f);
            var document = go.AddComponent<UIDocument>();
            document.panelSettings = labelPanel;
            document.worldSpaceSizeMode = UIDocument.WorldSpaceSizeMode.Dynamic;
            go.SetActive(true);
            element = document.rootVisualElement;
            element.pickingMode = PickingMode.Ignore;
            foreach (var path in new[] { "Frontier/Tokens", "Frontier/Frontier" })
            {
                var sheet = Resources.Load<StyleSheet>(path);
                if (sheet == null) Debug.LogError("Frontier: world label stylesheet is missing: " + path, this);
                else element.styleSheets.Add(sheet);
            }
            element.AddToClassList("fr-root");
            element.AddToClassList(pad ? "fr-world-pad" : "fr-world-label");
            var caption = new Label(text) { pickingMode = PickingMode.Ignore };
            caption.style.color = color;
            element.Add(caption);
            go.AddComponent<Billboard>();
            return go.transform;
        }

        void ApplyLabelBudget()
        {
            if (labelPanel != null) PresentationQuality.ApplyPanelBudget(labelPanel);
        }

        public void RefreshLocalizedLabels()
        {
            foreach (var item in items)
            {
                var structure = item.structure;
                if (structure == null || item.labelElement == null) continue;
                var caption = item.labelElement.Q<Label>();
                if (caption != null)
                    caption.text = UiText.Get(gs.IsUnderConstruction(structure) ? "world.label.building"
                        : gs.IsUpgradeInProgress(structure) ? "world.label.upgrading" : "world.label.level",
                        structure.def.LocalizedName.ToUpper(), structure.level);
            }
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
                foreach (var it in obstacles) if (it.t != null) it.t.localPosition = dirs[it.slot] * r;
            }
            AnimateItems(items);
            AnimateItems(pads);
            AnimateItems(obstacles);
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
                float age = Time.unscaledTime - it.momentAt;
                bool moment = age >= 0f && age < 0.65f;
                foreach (var outline in it.outlines) outline.enabled = (selected && !Locked) || it.pending || moment;
                if (it.visual != null)
                {
                    float t = Mathf.Clamp01(age / 0.65f);
                    float size = moment ? (it.buildIn ? 1f - 0.85f * Mathf.Pow(1f - t, 3f) : 1f + 0.12f * Mathf.Sin(t * Mathf.PI)) : 1f;
                    it.visual.localScale = Vector3.one * size;
                }
                if (it.scan != null)
                    it.scan.localPosition = new Vector3(0f, 0.1f + 0.65f * Mathf.Repeat(Time.unscaledTime * 0.65f + it.phase, 1f), 0f);
                float bob = 0.04f * Mathf.Sin(Time.time * 2.2f + it.phase);
                if (it.label != null)
                {
                    it.label.localPosition = new Vector3(0f, (it.structure != null ? 1.0f : 0.6f) + bob, 0f);
                    bool show = facing && !Locked && (it.structure == null || ShowStructureLabels || selected);
                    it.labelElement.style.visibility = show ? Visibility.Visible : Visibility.Hidden;
                }
                if (it.glow != null)
                {
                    float k = 1f + 0.12f * Mathf.Sin(Time.time * 3f + it.phase);
                    FxTransientPool.SetGlowQuadSize(it.glow, (it.structure != null ? 1.8f : 1.6f) * k);
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
                foreach (var it in obstacles) if (it.t == h.transform && Visible(ProbePoint(it))) { Tap(it); return; }
                foreach (var it in pads) if (it.t == h.transform && Visible(ProbePoint(it))) { Tap(it); return; }
                foreach (var it in items) if (it.t == h.transform && Visible(ProbePoint(it))) { Tap(it); return; }
            }

            // Generous screen-space fallback: anything visible near the tap counts.
            float best = 95f * Mathf.Max(0.6f, ScreenMetrics.Height / 1080f);
            Item pick = null;
            foreach (var list in new[] { items, pads, obstacles })
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
            else if (it.obstacle) { if (ObstacleTapped != null) ObstacleTapped(it.slot, it.t); }
            else if (SlotTapped != null) SlotTapped(it.slot, it.t);
        }

        /// <summary>Selects a placed structure from the UI using the same path as a world tap.</summary>
        public bool SelectStructure(PlacedStructure structure)
        {
            if (structure == null || structure.planetId != gs.planet.Id) return false;
            var current = gs.StructureAt(gs.planet, structure.slot);
            if (current == null || current.def.id != structure.def.id) return false;
            foreach (var it in items)
                if (SameStructure(it.structure, current) && it.t != null)
                { it.structure = current; Tap(it); return true; }
            return false;
        }

        /// <summary>Taps a placed structure exactly as a finger would (harness and tests).</summary>
        public bool SimulateTap(PlacedStructure structure) { return SelectStructure(structure); }

        /// <summary>Taps a visible obstruction exactly as a finger would (harness and tests).</summary>
        public bool SimulateObstacleTap(int pad)
        {
            foreach (var it in obstacles)
                if (it.slot == pad && it.t != null) { Tap(it); return true; }
            return false;
        }

        public Transform AnchorFor(PlacedStructure structure)
        {
            foreach (var item in items)
                if (SameStructure(item.structure, structure)) return item.t;
            return null;
        }

        static bool SameStructure(PlacedStructure a, PlacedStructure b)
        {
            return a != null && b != null && a.planetId == b.planetId && a.slot == b.slot && a.def.id == b.def.id;
        }
    }
}
