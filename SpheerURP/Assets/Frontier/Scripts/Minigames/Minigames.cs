using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Frontier
{
    public struct MinigameResult
    {
        public float quality;
        public int perfects;
        public int total;
    }

    /// <summary>
    /// Shared shell for all mini-games: overlay, title, an arena that shows an off-screen 3D scene, and a tick loop.
    /// </summary>
    public abstract class Minigame
    {
        protected VisualElement overlay, arena, controls;
        protected Label status, titleLabel;
        protected float tolerance = 1f, difficulty = 0.3f;
        /// <summary>Damage multiplier of the equipped tool (1 = bare hands).</summary>
        public float toolDamage = 1f;
        public int toolLevel;
        public string toolName = "";
        protected Color color = UiKit.Cyan;
        protected Color rawColor = Color.cyan;
        protected bool finished;
        protected MinigameStage stage;
        IVisualElementScheduledItem loop;
        Action<MinigameResult> onDone;
        long lastMs;
        bool sceneReady, cleaned;

        protected abstract string Title { get; }
        protected abstract string Hint { get; }
        protected virtual Color StageBg { get { return new Color(0.015f, 0.02f, 0.05f); } }
        protected abstract void Build();
        protected abstract void BuildScene();
        protected abstract void Frame(float aspect);
        protected abstract void Tick(float dt);

        protected float ArenaW { get { float w = arena.layout.width; return float.IsNaN(w) ? 0f : w; } }
        protected float ArenaH { get { float h = arena.layout.height; return float.IsNaN(h) ? 0f : h; } }

        public void Run(VisualElement host, Color accent, float tol, float diff, Action<MinigameResult> done)
        {
            rawColor = accent;
            color = UiKit.Ui(accent); tolerance = tol; difficulty = diff; onDone = done;

            overlay = UiKit.Div("fr-overlay");
            titleLabel = UiKit.Text(Title, "fr-title", color);
            overlay.Add(titleLabel);
            overlay.Add(UiKit.Text(Hint, "fr-muted"));
            arena = UiKit.Div("fr-arena");
            UiKit.Border(arena, new Color(color.r, color.g, color.b, 0.7f), 3f);
            overlay.Add(arena);
            controls = UiKit.Div("fr-row");
            overlay.Add(controls);
            status = UiKit.Text("", "fr-h2", Color.white);
            status.style.display = DisplayStyle.None;
            overlay.Add(status);
            host.Add(overlay);
            overlay.RegisterCallback<DetachFromPanelEvent>(e => Cleanup());

            stage = MinigameStage.Create(StageBg);
            Build();
            lastMs = (long)(Time.realtimeSinceStartup * 1000f);
            loop = overlay.schedule.Execute(() =>
            {
                long now = (long)(Time.realtimeSinceStartup * 1000f);
                float dt = Mathf.Min(0.05f, (now - lastMs) / 1000f);
                lastMs = now;
                if (finished || ArenaW <= 1f || stage == null) return;
                bool changed = SyncStage();
                if (!sceneReady) { sceneReady = true; BuildScene(); changed = true; }
                if (changed) Frame(stage.cam.aspect);
                Tick(dt);
            }).Every(16);
        }

        bool SyncStage()
        {
            float k = Mathf.Min(Screen.width, Screen.height) / Mathf.Max(100f, FrontierSettings.I.uiReferenceHeight);
            if (!stage.Resize(ArenaW * k, ArenaH * k)) return false;
            arena.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(stage.rt));
            arena.style.unityBackgroundScaleMode = ScaleMode.StretchToFill;
            return true;
        }

        void Cleanup()
        {
            if (cleaned) return;
            cleaned = true;
            if (loop != null) loop.Pause();
            if (stage != null) UnityEngine.Object.Destroy(stage.gameObject);
            stage = null;
        }

        protected void Finish(MinigameResult r)
        {
            if (finished) return;
            finished = true;
            overlay.schedule.Execute(() =>
            {
                Cleanup();
                overlay.RemoveFromHierarchy();
                if (onDone != null) onDone(r);
            }).StartingIn(1000);
        }

        protected Ray PointerRay(PointerDownEvent e)
        {
            Vector2 p = arena.WorldToLocal(e.position);
            return stage.cam.ViewportPointToRay(new Vector3(p.x / Mathf.Max(1f, ArenaW), 1f - p.y / Mathf.Max(1f, ArenaH), 0f));
        }

        protected void FramePose(float needW, float needH, float lookY, float aspect, float camLift = 0.5f)
        {
            float t = Mathf.Tan(stage.cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float d = Mathf.Max(needH / (2f * t), needW / (2f * t * Mathf.Max(0.2f, aspect)));
            stage.Pose(new Vector3(0f, lookY + camLift, -d), new Vector3(0f, lookY, 0f));
        }

        protected static VisualElement Pill(string text, Color c)
        {
            var chip = UiKit.Div();
            chip.pickingMode = PickingMode.Ignore;
            chip.style.position = Position.Absolute;
            chip.style.left = 14; chip.style.top = 14;
            chip.style.flexDirection = FlexDirection.Row;
            chip.style.alignItems = Align.Center;
            chip.style.paddingLeft = 14; chip.style.paddingRight = 14;
            chip.style.paddingTop = 6; chip.style.paddingBottom = 6;
            chip.style.backgroundColor = new Color(0f, 0f, 0f, 0.55f);
            UiKit.Radius(chip, 18f);
            UiKit.Border(chip, new Color(c.r, c.g, c.b, 0.8f), 2f);
            chip.Add(UiKit.Text(text, "fr-small", c));
            return chip;
        }

        protected static bool RaySphere(Ray r, Vector3 c, float rad, out Vector3 hit)
        {
            Vector3 oc = r.origin - c;
            float b = Vector3.Dot(oc, r.direction), cc = Vector3.Dot(oc, oc) - rad * rad;
            float disc = b * b - cc;
            hit = Vector3.zero;
            if (disc < 0f) return false;
            float t = -b - Mathf.Sqrt(disc);
            if (t < 0f) return false;
            hit = r.origin + r.direction * t;
            return true;
        }
    }

    /// <summary>
    /// Rust-style resource node in 3D: a rock (or tree) that breaks down through four stages. Strike the glowing weak spot
    /// to do real damage; it keeps moving, glancing blows barely scratch it, and every swing has a tool-speed cooldown.
    /// </summary>
    public class NodeGame : Minigame
    {
        const int Stages = 4;
        static readonly float[] RockScale = { 1f, 0.86f, 0.7f, 0.54f };
        static readonly Color SpotColor = new Color(1.8f, 1.4f, 0.4f, 1f);

        readonly bool tree;
        Transform nodeRoot, spot, spotCore, spotRing, spotGlow, trunk;
        readonly Transform[] blobs = new Transform[3];
        GameObject rockGo;
        Material rockMat, gemMat;
        Mesh chunkMesh, gemMesh;
        readonly List<Vector3> surf = new List<Vector3>();
        ToolRig tool;
        Vector3 spotLocal;
        float spotR, spotBaseY, trunkScaleY = 1f;
        float stageHp, hp, cd, swingTime, pulse, pop, nodeShake, frameW, frameH, lookY;
        int step, swings, spotHits, crits, rockSeed;
        VisualElement[] pipFill;
        VisualElement swingFill;

        public NodeGame(bool tree) { this.tree = tree; }

        protected override string Title { get { return tree ? "FELL THE TREE" : "BREAK THE NODE"; } }
        protected override string Hint { get { return "Hit the glowing weak spot. It keeps moving, and every swing needs a moment to recover"; } }
        protected override Color StageBg { get { return new Color(color.r * 0.05f + 0.01f, color.g * 0.05f + 0.015f, color.b * 0.06f + 0.03f); } }

        // ------------------------------------------------------------ UI
        protected override void Build()
        {
            arena.Add(Pill(string.IsNullOrEmpty(toolName) ? (tree ? "Axe" : "Pick") : toolName, color));

            var pips = UiKit.Div();
            pips.pickingMode = PickingMode.Ignore;
            pips.style.position = Position.Absolute;
            pips.style.left = 0; pips.style.right = 0; pips.style.top = 18;
            pips.style.flexDirection = FlexDirection.Row;
            pips.style.justifyContent = Justify.Center;
            pipFill = new VisualElement[Stages];
            for (int i = 0; i < Stages; i++)
            {
                var track = UiKit.Div();
                track.pickingMode = PickingMode.Ignore;
                track.style.width = 56; track.style.height = 12;
                track.style.marginLeft = 5; track.style.marginRight = 5;
                track.style.backgroundColor = new Color(0, 0, 0, 0.6f);
                UiKit.Radius(track, 6f);
                UiKit.Border(track, new Color(1, 1, 1, 0.3f), 2f);
                var fill = UiKit.Div();
                fill.style.height = Length.Percent(100);
                UiKit.Radius(fill, 6f);
                fill.style.backgroundColor = color;
                track.Add(fill);
                pipFill[i] = fill;
                pips.Add(track);
            }
            arena.Add(pips);

            var swBar = UiKit.Div();
            swBar.pickingMode = PickingMode.Ignore;
            swBar.style.position = Position.Absolute;
            swBar.style.left = Length.Percent(32); swBar.style.right = Length.Percent(32);
            swBar.style.bottom = 14; swBar.style.height = 10;
            swBar.style.backgroundColor = new Color(0, 0, 0, 0.5f);
            UiKit.Radius(swBar, 5f);
            swingFill = UiKit.Div();
            swingFill.style.height = Length.Percent(100);
            UiKit.Radius(swingFill, 5f);
            swingFill.style.backgroundColor = Color.white;
            swBar.Add(swingFill);
            arena.Add(swBar);

            arena.RegisterCallback<PointerDownEvent>(OnTap);
        }

        // ------------------------------------------------------------ 3D scene
        protected override void BuildScene()
        {
            swingTime = 0.55f / Mathf.Sqrt(Mathf.Max(1f, toolDamage));
            stageHp = Mathf.Lerp(2.6f, 4.6f, difficulty);
            hp = stageHp;
            spotR = Mathf.Clamp(0.36f * tolerance * (1.1f - 0.35f * difficulty), 0.24f, 0.5f);
            rockSeed = UnityEngine.Random.Range(1, 999);
            var st = stage;
            Color rim = new Color(rawColor.r, rawColor.g, rawColor.b) * 1.5f;

            st.GlowQuad(st.root, new Color(color.r, color.g, color.b, 0.35f), 11f, new Vector3(0f, 0.6f, 4f));
            st.GlowQuad(st.root, new Color(color.r, color.g, color.b, 0.22f), 6f, new Vector3(0f, -1.4f, 1.2f));

            nodeRoot = new GameObject("Node").transform;
            nodeRoot.SetParent(st.root, false);

            chunkMesh = st.Own(MeshKit.Ico(0, d => 0.5f));
            gemMesh = st.Own(MeshKit.Ico(1, d => 0.5f));
            gemMat = st.Lit(color * 0.5f, rim * 1.8f, 4f, 3f, 0f);

            if (tree) BuildTree(st, rim);
            else
            {
                rockMat = st.Lit(new Color(0.2f + color.r * 0.2f, 0.2f + color.g * 0.2f, 0.22f + color.b * 0.2f), rim, 3f, 3.5f, 0f, rockSeed);
                rockGo = st.AddMesh(MeshKit.Ico(2, d => RockR(d, rockSeed), surf), rockMat, nodeRoot, Vector3.zero);
                rockGo.name = "Rock";
                BuildRockMesh();
            }

            spot = new GameObject("Spot").transform;
            spot.SetParent(nodeRoot, false);
            var coreM = st.Additive(SpotColor);
            spotCore = st.Prim(PrimitiveType.Sphere, spot, Vector3.zero, Vector3.one, Vector3.zero, coreM).transform;
            spotRing = st.GlowQuad(spot, new Color(1.6f, 1.2f, 0.4f, 0.9f), 1f, new Vector3(0f, 0f, -0.1f), true);
            spotGlow = st.GlowQuad(spot, new Color(1.4f, 0.9f, 0.2f, 0.55f), 1f, new Vector3(0f, 0f, -0.05f));

            MoveSpot(true);

            frameW = tree ? 3.8f : 4.4f;
            frameH = tree ? 6.4f : 4.6f;
            lookY = tree ? 0.7f : 0.2f;
            Vector3 restPos = tree ? new Vector3(1.35f, 0.3f, -0.7f) : new Vector3(1.55f, 0.9f, -0.5f);
            tool = new ToolRig(st, tree ? MinigameKind.Chop : MinigameKind.Mine, toolLevel, color, restPos);
        }

        protected override void Frame(float aspect) { FramePose(frameW, frameH, lookY, aspect); }

        static float RockR(Vector3 d, int seed)
        {
            float sx = seed * 0.37f;
            float n = (Mathf.PerlinNoise(d.x * 1.7f + sx, d.y * 1.7f + 3f) + Mathf.PerlinNoise(d.y * 1.9f + sx, d.z * 1.9f + 7f) + Mathf.PerlinNoise(d.z * 1.5f + sx, d.x * 1.5f + 11f)) / 3f;
            n = Mathf.Round(n * 5f) / 5f * 0.5f + n * 0.5f;
            return 1.35f * (0.72f + 0.55f * n);
        }

        void BuildRockMesh()
        {
            float s = RockScale[Mathf.Min(step, Stages - 1)];
            rockGo.transform.localScale = Vector3.one * s;
        }

        void BuildTree(MinigameStage st, Color rim)
        {
            var barkM = st.Lit(new Color(0.42f, 0.26f, 0.13f), new Color(0.35f, 0.18f, 0.05f), 0f, 10f, 0f);
            var leafM = st.Lit(new Color(color.r * 0.5f + 0.1f, color.g * 0.5f + 0.1f, color.b * 0.5f + 0.1f), rim * 0.9f, 4f, 3f, 0f, rockSeed);
            rockMat = barkM;
            trunk = st.AddMesh(MeshKit.Cyl(0.5f, 0.34f, 3.2f, 9), barkM, nodeRoot, new Vector3(0f, -1.6f, 0f)).transform;
            float[] ys = { 1.5f, 2.35f, 3.05f }, rs = { 1.25f, 0.98f, 0.7f };
            for (int i = 0; i < 3; i++)
            {
                int seed = rockSeed + i * 13;
                float r = rs[i];
                blobs[i] = st.AddMesh(MeshKit.Ico(1, d => r * (0.8f + 0.35f * Mathf.PerlinNoise(d.x * 2f + seed, d.y * 2f + d.z))), leafM, nodeRoot, new Vector3(0f, ys[i], 0f)).transform;
            }
        }

        float TrunkR(float y) { return Mathf.Lerp(0.5f, 0.34f, Mathf.Clamp01((y + 1.6f) / 3.2f)); }

        void MoveSpot(bool first = false)
        {
            if (!tree)
            {
                float sc = RockScale[Mathf.Min(step, Stages - 1)];
                Vector3 old = spotLocal;
                for (int i = 0; i < 40; i++)
                {
                    Vector3 p = surf[UnityEngine.Random.Range(0, surf.Count)];
                    Vector3 d = p.normalized;
                    if (d.z > -0.62f || Mathf.Abs(d.x) > 0.8f || Mathf.Abs(d.y) > 0.75f) continue;
                    spotLocal = p * sc * 0.97f;
                    if (first || ((Vector2)spotLocal - (Vector2)old).magnitude > spotR * 2f) break;
                }
            }
            else
            {
                float old = spotBaseY;
                float top = 0.8f * trunkScaleY - 0.2f;
                for (int i = 0; i < 20; i++)
                {
                    spotBaseY = UnityEngine.Random.Range(-1.0f, top);
                    if (first || Mathf.Abs(spotBaseY - old) > 0.5f) break;
                }
                spotLocal = new Vector3(UnityEngine.Random.Range(-0.12f, 0.12f), spotBaseY, -(TrunkR(spotBaseY) + 0.03f));
            }
            spot.localPosition = spotLocal;
            pop = Mathf.Max(pop, 0.0f);
            if (!first) { Fx.Burst(spot.position, new Color(1.5f, 1.2f, 0.4f), 8, 1.5f, 0.12f); }
        }

        // ------------------------------------------------------------ input
        void OnTap(PointerDownEvent e)
        {
            if (finished || stage == null || tool == null) return;
            if (cd > 0f) { Sfx.Play(260f, 0.04f, 0.08f); return; }

            Ray ray = PointerRay(e);
            Vector3 sp = spot.position;
            float d = Vector3.Cross(ray.direction, sp - ray.origin).magnitude;
            swings++;
            float dmg;
            Vector3 hitW;
            int kind;
            if (d <= spotR * 0.4f) { kind = 2; dmg = toolDamage * 1.7f; hitW = sp; crits++; spotHits++; }
            else if (d <= spotR) { kind = 1; dmg = toolDamage; hitW = sp; spotHits++; }
            else
            {
                bool glance;
                if (tree)
                {
                    float tt = (nodeRoot.position.z - ray.origin.z) / (Mathf.Abs(ray.direction.z) < 1e-4f ? 1e-4f : ray.direction.z);
                    hitW = ray.origin + ray.direction * tt;
                    Vector3 l = nodeRoot.InverseTransformPoint(hitW);
                    glance = Mathf.Abs(l.x) < 0.55f && l.y > -1.7f && l.y < (step >= 3 ? 0.6f : 3.6f);
                }
                else
                {
                    Vector3 h;
                    glance = RaySphere(ray, nodeRoot.position, 1.15f * RockScale[Mathf.Min(step, Stages - 1)], out h);
                    if (glance) hitW = h;
                    else
                    {
                        float tt = (nodeRoot.position.z - ray.origin.z) / (Mathf.Abs(ray.direction.z) < 1e-4f ? 1e-4f : ray.direction.z);
                        hitW = ray.origin + ray.direction * tt;
                    }
                }
                kind = glance ? 0 : -1;
                dmg = glance ? toolDamage * 0.15f : 0f;
            }

            cd = swingTime;
            tool.Strike(stage.root.InverseTransformPoint(hitW), swingTime);
            if (kind == 2)
            {
                Sfx.Good();
                stage.Shake(0.5f); nodeShake = 1f; pop = 1f;
                Fx.Burst(hitW, new Color(2f, 1.6f, 0.5f), 26, 4.5f, 0.2f);
                Fx.Burst(hitW, new Color(rawColor.r, rawColor.g, rawColor.b) * 1.6f, 14, 3f, 0.16f);
                Fx.Ripple(hitW + Vector3.back * 0.2f, new Color(2f, 1.6f, 0.5f, 1f), 1.6f, 0.4f);
            }
            else if (kind == 1)
            {
                Sfx.Play(520f, 0.1f, 0.3f);
                stage.Shake(0.3f); nodeShake = 0.7f; pop = 0.6f;
                Fx.Burst(hitW, new Color(1.6f, 1.3f, 0.5f), 16, 3.5f, 0.16f);
            }
            else if (kind == 0)
            {
                Sfx.Bad();
                stage.Shake(0.1f); nodeShake = 0.25f;
                Fx.Burst(hitW, new Color(0.6f, 0.6f, 0.7f), 6, 1.8f, 0.1f);
            }
            else Sfx.Play(200f, 0.1f, 0.1f);

            if (dmg <= 0f) return;
            hp -= dmg;
            if (kind >= 1) MoveSpot();
            if (hp <= 0f) BreakStep();
        }

        // ------------------------------------------------------------ breaking
        void BreakStep()
        {
            step++;
            Sfx.Play(300f, 0.22f, 0.4f, -120f);
            stage.Shake(0.8f);
            if (step >= Stages) { FinalBreak(); return; }
            hp = stageHp;
            Chunks(8, 0.28f, 4.5f);
            pop = 1.4f;
            if (tree)
            {
                int blob = 3 - step;
                if (blob >= 0 && blobs[blob] != null) Fall(blobs[blob], new Vector3(UnityEngine.Random.Range(-1f, 1f), 1.5f, -1f));
                if (step == 3) { trunkScaleY = 0.75f; trunk.localScale = new Vector3(1f, trunkScaleY, 1f); }
            }
            else BuildRockMesh();
            MoveSpot();
        }

        void FinalBreak()
        {
            Chunks(16, 0.3f, 6f);
            Fx.Burst(nodeRoot.position, new Color(2f, 1.6f, 0.5f), 40, 6f, 0.25f);
            Fx.Ripple(nodeRoot.position + Vector3.back * 0.5f, new Color(rawColor.r, rawColor.g, rawColor.b) * 2f, 4f, 0.6f);
            for (int i = 0; i < 9; i++)
                stage.Chunk(gemMesh, gemMat, nodeRoot.position + UnityEngine.Random.insideUnitSphere * 0.4f, UnityEngine.Random.Range(0.25f, 0.4f),
                    new Vector3(UnityEngine.Random.Range(-3f, 3f), UnityEngine.Random.Range(3f, 6f), UnityEngine.Random.Range(-3f, -1f)), 6f, 1.4f);
            if (tree)
            {
                foreach (var b in blobs) if (b != null) Fall(b, new Vector3(UnityEngine.Random.Range(-2f, 2f), 2f, -1f));
                Fall(trunk, new Vector3(1.5f, 3f, -1f));
            }
            else rockGo.SetActive(false);
            spot.gameObject.SetActive(false);
            for (int i = 0; i < Stages; i++) pipFill[i].style.width = Length.Percent(100f);
            float acc = swings > 0 ? (float)spotHits / swings : 0f;
            Finish(new MinigameResult { quality = Mathf.Clamp01(0.3f + 0.7f * acc), perfects = crits, total = swings });
        }

        void Chunks(int n, float size, float speed)
        {
            Vector3 c = nodeRoot.position + (tree ? Vector3.up * 0.3f : Vector3.zero);
            for (int i = 0; i < n; i++)
                stage.Chunk(chunkMesh, rockMat, c + UnityEngine.Random.insideUnitSphere * 0.5f, size * UnityEngine.Random.Range(0.6f, 1.4f),
                    new Vector3(UnityEngine.Random.Range(-1f, 1f) * speed, UnityEngine.Random.Range(0.4f, 1f) * speed, UnityEngine.Random.Range(-1f, 0.2f) * speed), 12f, UnityEngine.Random.Range(0.9f, 1.4f));
        }

        void Fall(Transform t, Vector3 vel)
        {
            t.SetParent(stage.root, true);
            var d = t.gameObject.AddComponent<Debris>();
            d.vel = vel; d.gravity = 8f; d.life = 1.4f;
            d.spin = new Vector3(UnityEngine.Random.Range(-60f, 60f), UnityEngine.Random.Range(-60f, 60f), UnityEngine.Random.Range(-200f, 200f));
        }

        // ------------------------------------------------------------ per-frame
        protected override void Tick(float dt)
        {
            float t = Time.time;
            cd = Mathf.Max(0f, cd - dt);
            pulse += dt * 5f;
            pop = Mathf.MoveTowards(pop, 0f, dt * 3.2f);
            nodeShake = Mathf.MoveTowards(nodeShake, 0f, dt * 3f);
            if (tool != null) tool.Tick(dt, t);
            if (step >= Stages) return;

            if (!tree)
            {
                float amp = 8f + 24f * difficulty;
                nodeRoot.localRotation = Quaternion.Euler(Mathf.Sin(t * 0.7f) * 4f, Mathf.Sin(t * (0.8f + difficulty * 1.2f)) * amp, 0f);
                float s = RockScale[Mathf.Min(step, Stages - 1)] * (1f + 0.14f * pop);
                rockGo.transform.localScale = Vector3.one * s;
            }
            else
            {
                float slide = Mathf.Sin(t * (1.1f + 2f * difficulty)) * (0.18f + 0.3f * difficulty);
                float y = Mathf.Clamp(spotBaseY + slide, -1.3f, 0.9f * trunkScaleY - 0.2f);
                spotLocal = new Vector3(spotLocal.x, y, -(TrunkR(y) + 0.03f));
                for (int i = 0; i < 3; i++)
                    if (blobs[i] != null && blobs[i].parent == nodeRoot)
                        blobs[i].localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 1.3f + i) * 2.5f * (1f + pop));
            }
            nodeRoot.localPosition = UnityEngine.Random.insideUnitSphere * 0.06f * nodeShake;
            spot.localPosition = spotLocal;
            spot.rotation = stage.root.rotation;

            float ready = cd > 0f ? 0.45f : 1f;
            float pu = 1f + 0.12f * Mathf.Sin(pulse);
            spotCore.localScale = Vector3.one * spotR * 0.9f * pu * ready;
            spotRing.localScale = Vector3.one * spotR * 3.2f * (1f + 0.1f * Mathf.Sin(pulse * 0.7f)) * ready;
            spotGlow.localScale = Vector3.one * spotR * 5.5f * ready;

            for (int i = 0; i < Stages; i++)
            {
                float f = i < step ? 1f : i == step ? Mathf.Clamp01(hp / stageHp) : 0f;
                pipFill[i].style.width = Length.Percent(f * 100f);
            }
            swingFill.style.width = Length.Percent((swingTime > 0f ? 1f - cd / swingTime : 1f) * 100f);
            swingFill.style.backgroundColor = cd > 0f ? new Color(1f, 1f, 1f, 0.5f) : color;
        }
    }

    /// <summary>Hold to bore: keep the gauge in the moving band while a 3D drill sinks into the ground. No timer - better drills sink faster.</summary>
    public class DrillGame : Minigame
    {
        VisualElement gauge, band, needle, progFill;
        float t, pressure = 0.1f, vel, inBand, progress, sparkT, spinAngle;
        bool holding, inside;
        Transform rig, spin, hole, dust;
        float frameW = 5.4f, frameH = 6.4f;
        Material bitM;

        protected override string Title { get { return "BORE"; } }
        protected override string Hint { get { return "Hold to raise pressure. Keep it inside the band to sink the drill"; } }
        protected override Color StageBg { get { return new Color(color.r * 0.04f + 0.01f, color.g * 0.04f + 0.012f, color.b * 0.05f + 0.025f); } }

        protected override void Build()
        {
            arena.Add(Pill(string.IsNullOrEmpty(toolName) ? "Drill" : toolName, color));

            gauge = UiKit.Div();
            gauge.pickingMode = PickingMode.Ignore;
            gauge.style.position = Position.Absolute;
            gauge.style.right = 16; gauge.style.width = 76;
            gauge.style.top = 70; gauge.style.bottom = 30;
            gauge.style.backgroundColor = new Color(0, 0, 0, 0.55f);
            UiKit.Radius(gauge, 26);
            UiKit.Border(gauge, new Color(1, 1, 1, 0.3f), 3f);
            arena.Add(gauge);

            band = UiKit.Div();
            band.pickingMode = PickingMode.Ignore;
            band.style.position = Position.Absolute; band.style.left = 0; band.style.right = 0;
            band.style.backgroundColor = new Color(UiKit.Lime.r, UiKit.Lime.g, UiKit.Lime.b, 0.5f);
            UiKit.Radius(band, 18);
            UiKit.Halo(band, UiKit.Lime, 24f, 0.6f);
            gauge.Add(band);

            needle = UiKit.Div();
            needle.pickingMode = PickingMode.Ignore;
            needle.style.position = Position.Absolute; needle.style.left = -14; needle.style.right = -14;
            needle.style.height = 12;
            needle.style.backgroundColor = Color.white;
            UiKit.Radius(needle, 6);
            UiKit.Halo(needle, color, 20f, 0.8f);
            gauge.Add(needle);

            var prog = UiKit.Div();
            prog.pickingMode = PickingMode.Ignore;
            prog.style.position = Position.Absolute;
            prog.style.left = 16; prog.style.width = 32;
            prog.style.top = 70; prog.style.bottom = 30;
            prog.style.backgroundColor = new Color(0, 0, 0, 0.55f);
            UiKit.Radius(prog, 16);
            UiKit.Border(prog, new Color(1, 1, 1, 0.3f), 3f);
            progFill = UiKit.Div();
            progFill.pickingMode = PickingMode.Ignore;
            progFill.style.position = Position.Absolute;
            progFill.style.left = 0; progFill.style.right = 0; progFill.style.bottom = 0;
            UiKit.Radius(progFill, 14);
            progFill.style.backgroundColor = color;
            UiKit.Halo(progFill, color, 24f, 0.6f);
            prog.Add(progFill);
            arena.Add(prog);

            var btn = UiKit.Btn("HOLD", color, null, true);
            btn.style.minWidth = 420;
            btn.RegisterCallback<PointerDownEvent>(e => { holding = true; btn.CapturePointer(e.pointerId); });
            btn.RegisterCallback<PointerUpEvent>(e => { holding = false; if (btn.HasPointerCapture(e.pointerId)) btn.ReleasePointer(e.pointerId); });
            btn.RegisterCallback<PointerCancelEvent>(e => holding = false);
            controls.Add(btn);
        }

        protected override void BuildScene()
        {
            var st = stage;
            Color rim = new Color(rawColor.r, rawColor.g, rawColor.b) * 1.4f;
            st.GlowQuad(st.root, new Color(color.r, color.g, color.b, 0.3f), 12f, new Vector3(0f, 1.5f, 5f));

            var groundM = st.Lit(new Color(0.18f + color.r * 0.2f, 0.18f + color.g * 0.2f, 0.2f + color.b * 0.2f), rim, 2f, 3f, 0f);
            st.AddMesh(MeshKit.Cyl(2.8f, 2.6f, 0.9f, 22), groundM, st.root, new Vector3(0f, -0.9f, 0f));
            var holeM = st.Lit(new Color(0.01f, 0.01f, 0.015f), Color.black, 0f, 2f, 0f);
            hole = st.AddMesh(MeshKit.Cyl(0.6f, 0.6f, 0.02f, 16), holeM, st.root, new Vector3(0f, 0.012f, 0f)).transform;
            hole.localScale = new Vector3(0.2f, 1f, 0.2f);
            dust = st.GlowQuad(st.root, new Color(1.4f, 1.0f, 0.4f, 0.5f), 2.4f, new Vector3(0f, 0.15f, -0.4f));

            rig = new GameObject("Rig").transform;
            rig.SetParent(st.root, false);
            spin = new GameObject("Spin").transform;
            spin.SetParent(rig, false);

            Color headC, rimC;
            if (toolLevel <= 0) { headC = new Color(0.45f, 0.45f, 0.5f); rimC = new Color(0.5f, 0.6f, 0.7f) * 0.5f; }
            else if (toolLevel == 1) { headC = new Color(0.7f, 0.74f, 0.8f); rimC = new Color(0.3f, 0.9f, 1.4f) * 0.7f; }
            else { headC = new Color(0.12f, 0.3f, 0.4f); rimC = new Color(rawColor.r, rawColor.g, rawColor.b) * 2.4f; }
            bitM = st.Lit(headC, rimC, 0f, 14f, 0f);
            var bodyM = st.Lit(headC * 0.8f, rimC * 0.6f, 0f, 3f, 0f);
            var glowM = st.Lit(headC * 0.6f, rimC * 1.4f, 0f, 3f, 0f);

            st.AddMesh(MeshKit.Cyl(0.02f, 0.42f, 1.7f, 8), bitM, spin, Vector3.zero);
            for (int i = 0; i < 3; i++)
                st.Prim(PrimitiveType.Cube, spin, new Vector3(0f, 0.5f + i * 0.42f, 0f), new Vector3(0.5f + i * 0.2f, 0.07f, 0.07f), new Vector3(0f, i * 60f, 0f), glowM);
            st.Prim(PrimitiveType.Cube, rig, new Vector3(0f, 2.0f, 0f), new Vector3(0.95f, 0.55f, 0.95f), Vector3.zero, bodyM);
            st.Prim(PrimitiveType.Cylinder, rig, new Vector3(0f, 3.5f, 0f), new Vector3(0.2f, 1.5f, 0.2f), Vector3.zero, glowM);
            rig.localPosition = new Vector3(0f, 1.8f, 0f);
        }

        protected override void Frame(float aspect) { FramePose(frameW, frameH, 1.1f, aspect, 1.6f); }

        protected override void Tick(float dt)
        {
            t += dt;
            float target = holding ? 0.7f : -0.8f;
            vel = Mathf.Lerp(vel, target, 5f * dt);
            pressure = Mathf.Clamp01(pressure + vel * dt);
            if (pressure <= 0f || pressure >= 1f) vel = 0f;

            float center = 0.5f + 0.3f * Mathf.Sin(t * (0.6f + difficulty)) + 0.08f * Mathf.Sin(t * 2.1f);
            float half = Mathf.Lerp(0.16f, 0.07f, difficulty) * tolerance;
            inside = Mathf.Abs(pressure - center) <= half;
            if (inside)
            {
                inBand += dt;
                progress += dt * 0.075f * toolDamage / (0.6f + difficulty);
            }

            band.style.bottom = Length.Percent((center - half) * 100f);
            band.style.height = Length.Percent(half * 2f * 100f);
            needle.style.bottom = Length.Percent(pressure * 100f);
            needle.style.backgroundColor = inside ? Color.white : new Color(1f, 0.4f, 0.4f);
            progFill.style.height = Length.Percent(Mathf.Clamp01(progress) * 100f);

            float k = Mathf.Clamp01(progress);
            float tipY = Mathf.Lerp(1.8f, -1.0f, k);
            float shakeAmt = (holding ? 0.02f + 0.03f * pressure : 0f) * (inside ? 0.6f : 1.6f);
            rig.localPosition = new Vector3(UnityEngine.Random.Range(-1f, 1f) * shakeAmt, tipY, UnityEngine.Random.Range(-1f, 1f) * shakeAmt);
            spinAngle += dt * (holding ? 300f + 900f * pressure : 80f) * (inside ? 1f : 0.5f);
            spin.localRotation = Quaternion.Euler(0f, spinAngle, 0f);
            float hs = 0.2f + 0.8f * k;
            hole.localScale = new Vector3(hs, 1f, hs);
            dust.localScale = Vector3.one * (inside && holding ? 2.6f + Mathf.Sin(t * 30f) * 0.3f : 0.001f);

            sparkT -= dt;
            if (sparkT <= 0f && holding && tipY < 1.0f)
            {
                Vector3 p = stage.root.TransformPoint(new Vector3(0f, 0.1f, -0.25f));
                if (inside) { Fx.Burst(p, new Color(2f, 1.5f, 0.5f), 7, 3.2f, 0.12f); stage.Shake(0.05f); }
                else Fx.Burst(p, new Color(1.6f, 0.3f, 0.3f), 5, 1.8f, 0.14f);
                sparkT = 0.07f;
            }

            if (progress >= 1f)
            {
                Sfx.Good();
                stage.Shake(0.6f);
                Fx.Burst(stage.root.TransformPoint(new Vector3(0f, 0.2f, -0.2f)), new Color(rawColor.r, rawColor.g, rawColor.b) * 1.8f, 40, 6f, 0.24f);
                float eff = inBand / Mathf.Max(0.01f, t);
                Finish(new MinigameResult { quality = Mathf.Clamp01(0.3f + 0.7f * eff), perfects = Mathf.RoundToInt(inBand), total = Mathf.Max(1, Mathf.RoundToInt(t)) });
            }
        }
    }

    /// <summary>3D asteroid-field flight during warp. Quality (fewer hits) refunds part of the fuel.</summary>
    public class TravelGame : Minigame
    {
        const int Spawns = 18;
        class Obj { public Transform tr; public int lane; public float z; public bool orb; public Vector3 spin; }
        readonly List<Obj> objs = new List<Obj>();
        readonly List<Transform> bars = new List<Transform>();
        Transform ship, shipGlow;
        Mesh[] rockMeshes;
        Mesh orbMesh;
        Material rockM, orbM;
        int lane = 1, spawned, resolved, hits, orbs;
        float spawnT = 0.6f, hitFlash, shipX, gap = 2.2f, unit = 1f, bob;
        bool shipReady;

        protected override string Title { get { return "ASTEROID FIELD"; } }
        protected override string Hint { get { return "Tap a lane to steer. Dodge rocks, grab orbs. Clean flying refunds fuel"; } }
        protected override Color StageBg { get { return new Color(0.01f, 0.01f, 0.04f); } }

        protected override void Build()
        {
            status.style.display = DisplayStyle.Flex;
            arena.RegisterCallback<PointerDownEvent>(e =>
            {
                float x = arena.WorldToLocal(e.position).x;
                lane = Mathf.Clamp((int)(x / Mathf.Max(1f, ArenaW) * 3f), 0, 2);
            });
            Status();
        }

        void Status() { status.text = "Orbs " + orbs + "     Hits " + hits; }

        protected override void BuildScene()
        {
            var st = stage;
            Color rim = new Color(rawColor.r, rawColor.g, rawColor.b) * 1.6f;
            rockM = st.Lit(new Color(0.35f, 0.17f, 0.2f), new Color(1.6f, 0.4f, 0.35f), 4f, 3f, 0f);
            orbM = st.Lit(new Color(0.3f, 0.7f, 0.2f), new Color(UiKit.Lime.r, UiKit.Lime.g, UiKit.Lime.b) * 2.2f, 0f, 2f, 0f);
            rockMeshes = new Mesh[3];
            for (int i = 0; i < 3; i++)
            {
                int sd = i * 17 + 5;
                rockMeshes[i] = st.Own(MeshKit.Ico(1, d => 0.9f * (0.7f + 0.6f * Mathf.PerlinNoise(d.x * 2f + sd, d.y * 2f + d.z * 1.3f))));
            }
            orbMesh = st.Own(MeshKit.Ico(1, d => 0.5f));

            var shipM = st.Lit(new Color(0.15f, 0.2f, 0.3f), rim, 1f, 4f, 0f);
            var glowM = st.Lit(new Color(0.1f, 0.3f, 0.4f), rim * 1.6f, 0f, 3f, 0f);
            ship = new GameObject("Ship").transform;
            ship.SetParent(st.root, false);
            var body = st.AddMesh(MeshKit.Cyl(0.05f, 0.42f, 1.9f, 8), shipM, ship, new Vector3(0f, 0f, 0.9f));
            body.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            st.Prim(PrimitiveType.Cube, ship, new Vector3(-0.6f, 0f, -0.55f), new Vector3(1.0f, 0.07f, 0.75f), new Vector3(0f, -12f, 8f), glowM);
            st.Prim(PrimitiveType.Cube, ship, new Vector3(0.6f, 0f, -0.55f), new Vector3(1.0f, 0.07f, 0.75f), new Vector3(0f, 12f, -8f), glowM);
            st.Prim(PrimitiveType.Cube, ship, new Vector3(0f, 0.3f, -0.9f), new Vector3(0.07f, 0.5f, 0.7f), Vector3.zero, glowM);
            st.Prim(PrimitiveType.Sphere, ship, new Vector3(0f, 0.3f, 0.05f), new Vector3(0.32f, 0.22f, 0.55f), Vector3.zero, glowM);
            st.GlowQuad(ship, new Color(rawColor.r, rawColor.g, rawColor.b, 0.9f) * 1.4f, 1.3f, new Vector3(0f, 0f, -1.35f));
            shipGlow = st.GlowQuad(ship, new Color(2f, 0.2f, 0.2f, 0f), 3f, new Vector3(0f, 0f, 0f));

            var streak = new GameObject("Streaks");
            streak.transform.SetParent(st.root, false);
            streak.transform.localPosition = new Vector3(0f, 2f, 45f);
            var ps = streak.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true; main.playOnAwake = false;
            main.startLifetime = 1.6f; main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.14f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.5f, 1.2f, 2f, 0.8f), new Color(1.6f, 1.6f, 2.2f, 1f));
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 500;
            var em = ps.emission; em.rateOverTime = 160f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(40f, 16f, 2f);
            var vo = ps.velocityOverLifetime; vo.enabled = true; vo.space = ParticleSystemSimulationSpace.Local;
            vo.x = 0f; vo.y = 0f; vo.z = -55f;
            var pr = streak.GetComponent<ParticleSystemRenderer>();
            pr.renderMode = ParticleSystemRenderMode.Stretch;
            pr.velocityScale = 0.1f; pr.lengthScale = 2f;
            pr.sharedMaterial = st.Additive(Color.white, Fx.Glow);
            ps.Play();

            st.GlowQuad(st.root, new Color(color.r, color.g, color.b, 0.3f), 40f, new Vector3(0f, 3f, 55f));
        }

        protected override void Frame(float aspect)
        {
            stage.cam.fieldOfView = 55f;
            stage.Pose(new Vector3(0f, 2.3f, -5.2f), new Vector3(0f, 0.3f, 6f));
            float halfW = 5.7f * Mathf.Tan(27.5f * Mathf.Deg2Rad) * aspect;
            gap = Mathf.Clamp(halfW * 0.95f - 0.95f, 0.9f, 2.2f);
            unit = Mathf.Clamp(gap / 2.2f, 0.5f, 1f);
            ship.localScale = Vector3.one * unit;
            RebuildGuides();
            shipX = LaneX(lane);
        }

        float LaneX(int l) { return (l - 1) * gap; }

        void RebuildGuides()
        {
            foreach (var b in bars) if (b != null) UnityEngine.Object.Destroy(b.gameObject);
            bars.Clear();
            var st = stage;
            var lineM = st.Additive(new Color(color.r, color.g, color.b, 0.5f) * 1.2f);
            for (int i = 0; i < 4; i++)
                bars.Add(st.Prim(PrimitiveType.Quad, st.root, new Vector3((i - 1.5f) * gap, -0.02f, 30f), new Vector3(0.05f, 80f, 1f), new Vector3(90f, 0f, 0f), lineM).transform);
            var barM = st.Additive(new Color(color.r, color.g, color.b, 0.35f) * 1.2f);
            for (int i = 0; i < 14; i++)
                bars.Add(st.Prim(PrimitiveType.Quad, st.root, new Vector3(0f, -0.03f, i * 5f), new Vector3(gap * 3.1f, 0.1f, 1f), new Vector3(90f, 0f, 0f), barM).transform);
        }

        protected override void Tick(float dt)
        {
            float speed = 26f * (1f + difficulty * 0.8f);
            bob += dt;
            spawnT -= dt;
            if (spawned < Spawns && spawnT <= 0f)
            {
                bool orb = UnityEngine.Random.value < 0.22f;
                var o = new Obj { lane = UnityEngine.Random.Range(0, 3), z = 55f, orb = orb, spin = UnityEngine.Random.insideUnitSphere * 120f };
                var go = new GameObject(orb ? "Orb" : "Rock");
                go.transform.SetParent(stage.root, false);
                go.AddComponent<MeshFilter>().sharedMesh = orb ? orbMesh : rockMeshes[UnityEngine.Random.Range(0, rockMeshes.Length)];
                go.AddComponent<MeshRenderer>().sharedMaterial = orb ? orbM : rockM;
                float sz = unit * (orb ? 0.9f : UnityEngine.Random.Range(0.95f, 1.25f));
                go.transform.localScale = Vector3.one * sz;
                if (orb) stage.GlowQuad(go.transform, new Color(UiKit.Lime.r, UiKit.Lime.g, UiKit.Lime.b, 0.7f) * 1.2f, 2.6f / Mathf.Max(0.01f, sz), Vector3.zero);
                o.tr = go.transform;
                objs.Add(o);
                spawned++;
                spawnT = Mathf.Lerp(0.85f, 0.5f, difficulty);
            }

            shipX = Mathf.Lerp(shipX, LaneX(lane), Mathf.Clamp01(dt * 12f));
            float roll = Mathf.Clamp((LaneX(lane) - shipX) * 14f, -35f, 35f);
            ship.localPosition = new Vector3(shipX, 0.15f + Mathf.Sin(bob * 3f) * 0.05f, 0f);
            ship.localRotation = Quaternion.Euler(Mathf.Sin(bob * 2f) * 2f, 0f, -roll);
            hitFlash = Mathf.MoveTowards(hitFlash, 0f, dt * 2.5f);
            shipGlow.localScale = Vector3.one * (hitFlash > 0f ? 3f : 0.001f);
            shipGlow.GetComponent<MeshRenderer>().sharedMaterial.SetColor("_Color", new Color(2f, 0.2f, 0.2f, hitFlash * 0.9f));

            foreach (var b in bars)
            {
                if (b.localScale.y > 20f) continue;
                Vector3 p = b.localPosition;
                p.z -= speed * dt;
                if (p.z < -6f) p.z += 70f;
                b.localPosition = p;
            }

            for (int i = objs.Count - 1; i >= 0; i--)
            {
                var o = objs[i];
                o.z -= speed * dt;
                o.tr.localPosition = new Vector3(LaneX(o.lane), 0.55f * unit + (o.orb ? Mathf.Sin(bob * 5f + i) * 0.15f : 0f), o.z);
                o.tr.Rotate(o.spin * dt, Space.Self);
                if (o.lane == lane && Mathf.Abs(o.z) < 1.1f * unit + 0.3f)
                {
                    Vector3 wp = o.tr.position;
                    if (o.orb)
                    {
                        orbs++; Sfx.Good();
                        Fx.Burst(wp, new Color(UiKit.Lime.r, UiKit.Lime.g, UiKit.Lime.b) * 1.8f, 16, 3f, 0.16f);
                    }
                    else
                    {
                        hits++; hitFlash = 1f; Sfx.Bad();
                        stage.Shake(0.7f);
                        Fx.Burst(wp, new Color(2f, 0.6f, 0.4f), 24, 5f, 0.2f);
                    }
                    Remove(i, o);
                }
                else if (o.z < -7f) Remove(i, o);
            }
            Status();
            if (spawned >= Spawns && resolved >= Spawns)
            {
                float q = Mathf.Clamp01(1f - hits * 0.17f + orbs * 0.03f);
                Finish(new MinigameResult { quality = q, perfects = orbs, total = Spawns });
            }
        }

        void Remove(int i, Obj o)
        {
            objs.RemoveAt(i);
            UnityEngine.Object.Destroy(o.tr.gameObject);
            resolved++;
        }
    }
}
