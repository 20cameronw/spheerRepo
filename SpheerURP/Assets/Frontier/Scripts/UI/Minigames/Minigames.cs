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
        public bool abandoned;
    }

    /// <summary>
    /// Shared shell for all mini-games: overlay, title, an arena that shows an off-screen 3D scene, and a tick loop.
    /// </summary>
    public abstract class Minigame
    {
        protected VisualElement overlay, arena, controls;
        protected Label status, titleLabel;
        Label toolState;
        protected float tolerance = 1f, difficulty = 0.3f;
        /// <summary>Damage multiplier of the equipped tool (1 = bare hands).</summary>
        public float toolDamage = 1f;
        public int toolLevel;
        public string toolName = "";
        protected Color color = UiKit.Cyan;
        protected Color rawColor = Color.cyan;
        protected bool finished;
        public bool IsPaused { get; private set; }
        protected MinigameStage stage;
        IVisualElementScheduledItem loop;
        Action<MinigameResult> onDone;
        VisualElement pauseLayer;
        long lastMs;
        bool sceneReady, cleaned, confirmingAbandon;
        bool resultAwaitingConfirmation;
        MinigameResult pendingResult;

        protected abstract string Title { get; }
        protected abstract string Hint { get; }
        protected virtual string DefaultToolName { get { return UiText.Get("harvest.tool"); } }
        protected virtual MinigameKind FeedbackKind { get { return MinigameKind.Chop; } }
        protected virtual string ResultMetric { get { return UiText.Get("harvest.perfect-hits"); } }
        protected virtual Color StageBg { get { return new Color(0.015f, 0.02f, 0.05f); } }
        protected abstract void Build();
        protected abstract void BuildScene();
        protected abstract void Frame(float aspect);
        protected abstract void Tick(float dt);

        protected float ArenaW { get { float w = arena.layout.width; return float.IsNaN(w) ? 0f : w; } }
        protected float ArenaH { get { float h = arena.layout.height; return float.IsNaN(h) ? 0f : h; } }
        protected bool CanInteract { get { return sceneReady && !finished && !IsPaused && !cleaned && stage != null; } }

        public void Run(VisualElement host, Color accent, float tol, float diff, Action<MinigameResult> done)
        {
            HarvestFeedback.Warmup();
            rawColor = accent;
            color = UiKit.Ui(accent); tolerance = tol; difficulty = diff; onDone = done;

            overlay = UiKit.Div("fr-overlay");
            titleLabel = UiKit.Text(Title, "fr-title", color);
            titleLabel.style.flexGrow = 1f;
            titleLabel.style.minWidth = 0f;
            var heading = UiKit.Div("fr-row fr-minigame-heading");
            heading.Add(titleLabel);
            var pause = UiKit.Btn(UiText.Get("harvest.pause"), UiKit.Cyan, Pause);
            pause.name = "minigame-pause";
            pause.AddToClassList("fr-minigame-pause-button");
            heading.Add(pause);
            overlay.Add(heading);
            overlay.Add(UiKit.Text(Hint, "fr-muted"));
            var toolFeedback = UiKit.Div("fr-minigame-tool-feedback");
            toolFeedback.name = "minigame-tool-feedback";
            toolFeedback.Add(UiKit.Text(string.IsNullOrEmpty(toolName) ? DefaultToolName : toolName,
                "fr-minigame-tool-name", color));
            toolState = UiKit.Text(UiText.Get("harvest.getting-ready"), "fr-minigame-tool-state", UiKit.Muted);
            toolState.name = "minigame-tool-state";
            toolFeedback.Add(toolState);
            overlay.Add(toolFeedback);
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
            stage.FocusLost = Pause;
            Build();
            lastMs = (long)(Time.realtimeSinceStartup * 1000f);
            loop = overlay.schedule.Execute(() =>
            {
                long now = (long)(Time.realtimeSinceStartup * 1000f);
                float dt = Mathf.Min(0.05f, (now - lastMs) / 1000f);
                lastMs = now;
                if (finished || IsPaused || ArenaW <= 1f || stage == null) return;
                bool changed = SyncStage();
                if (!sceneReady) { sceneReady = true; BuildScene(); SetToolState(UiText.Get("harvest.ready")); changed = true; }
                if (changed) Frame(stage.cam.aspect);
                Tick(dt);
            }).Every(16);
        }

        protected void SetToolState(string message, bool ready = true)
        {
            if (toolState.text == message) return;
            toolState.text = message;
            toolState.style.color = ready ? UiKit.Cyan : UiKit.Muted;
        }

        bool SyncStage()
        {
            float k = ScreenMetrics.ShortSide / Mathf.Max(100f, FrontierSettings.I.uiReferenceHeight);
            if (!stage.Resize(ArenaW * k, ArenaH * k)) return false;
            arena.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(stage.rt));
            arena.style.backgroundSize = new BackgroundSize(Length.Percent(100f), Length.Percent(100f));
            return true;
        }

        void Cleanup()
        {
            if (cleaned) return;
            cleaned = true;
            if (loop != null) loop.Pause();
            if (stage != null) stage.FocusLost = null;
            if (stage != null) UnityEngine.Object.Destroy(stage.gameObject);
            stage = null;
        }

        public void Pause()
        {
            if (finished || IsPaused || cleaned || overlay == null) return;
            IsPaused = true;
            confirmingAbandon = false;
            lastMs = (long)(Time.realtimeSinceStartup * 1000f);
            ShowPauseDialog(false);
        }

        public void Resume()
        {
            if (finished || !IsPaused) return;
            IsPaused = false;
            confirmingAbandon = false;
            lastMs = (long)(Time.realtimeSinceStartup * 1000f);
            if (pauseLayer != null) pauseLayer.RemoveFromHierarchy();
            pauseLayer = null;
        }

        public void RequestAbandon()
        {
            if (finished || !IsPaused) return;
            confirmingAbandon = true;
            ShowPauseDialog(true);
        }

        public bool HandleBack()
        {
            if (finished) return true; // Collecting a result must remain an explicit action.
            if (confirmingAbandon) CancelAbandon();
            else if (IsPaused) Resume();
            else Pause();
            return true;
        }

        public void CancelAbandon()
        {
            if (finished || !IsPaused || !confirmingAbandon) return;
            confirmingAbandon = false;
            ShowPauseDialog(false);
        }

        public void ConfirmAbandon()
        {
            if (finished || !IsPaused || !confirmingAbandon) return;
            Finish(new MinigameResult { abandoned = true });
        }

        void ShowPauseDialog(bool confirm)
        {
            if (pauseLayer != null) pauseLayer.RemoveFromHierarchy();
            pauseLayer = UiKit.Div("fr-minigame-pause-layer");
            pauseLayer.name = "minigame-pause-layer";
            pauseLayer.pickingMode = PickingMode.Position;
            var panel = UiKit.Div("fr-minigame-pause-panel");
            panel.pickingMode = PickingMode.Position;
            panel.Add(UiKit.Text(UiText.Get(confirm ? "harvest.abandon.title" : "harvest.paused.title"),
                "fr-minigame-pause-title", confirm ? UiKit.Amber : UiKit.Cyan));
            panel.Add(UiKit.Text(UiText.Get(confirm ? "harvest.abandon.help" : "harvest.paused.help"),
                "fr-minigame-pause-copy"));
            var actions = UiKit.Div("fr-minigame-pause-actions");
            if (confirm)
            {
                actions.Add(PauseAction(UiText.Get("harvest.keep-playing"), UiKit.Cyan, CancelAbandon, "minigame-keep-playing", true));
                actions.Add(PauseAction(UiText.Get("harvest.abandon"), UiKit.Red, ConfirmAbandon, "minigame-confirm-abandon", false));
            }
            else
            {
                actions.Add(PauseAction(UiText.Get("harvest.resume"), UiKit.Cyan, Resume, "minigame-resume", true));
                actions.Add(PauseAction(UiText.Get("harvest.abandon"), UiKit.Red, RequestAbandon, "minigame-abandon", false));
            }
            panel.Add(actions);
            pauseLayer.Add(panel);
            overlay.Add(pauseLayer);
        }

        static Button PauseAction(string text, Color accent, Action action, string name, bool primary)
        {
            var button = UiKit.Btn(text, accent, action, true, primary);
            button.name = name;
            button.style.width = Length.Percent(100f);
            button.style.marginLeft = 0f;
            return button;
        }

        protected void Finish(MinigameResult r)
        {
            if (finished) return;
            finished = true;
            if (!r.abandoned) HarvestFeedback.Play(HarvestCue.Complete, FeedbackKind);
            overlay.schedule.Execute(() =>
            {
                Cleanup();
                if (r.abandoned) Complete(r);
                else ShowResult(r);
            }).StartingIn(r.abandoned ? 250 : 1000);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void DebugComplete(float quality, int perfects, int total)
        {
            Finish(new MinigameResult { quality = quality, perfects = perfects, total = total });
        }
#endif

        void ShowResult(MinigameResult result)
        {
            overlay.Clear();
            var panel = UiKit.Div("fr-minigame-result-panel");
            panel.name = "minigame-result";
            panel.Add(UiKit.Text(UiText.Get("harvest.complete"), "fr-minigame-result-title", color));
            panel.Add(UiKit.Text(UiText.Get("harvest.grade", Grade(result.quality)), "fr-minigame-result-grade", color));
            panel.Add(UiKit.Text(UiText.Get("harvest.performance", Mathf.Clamp01(result.quality).ToString("P0")),
                "fr-minigame-result-quality", UiKit.Muted));
            panel.Add(UiKit.Text(UiText.Get("harvest.result.metric", ResultMetric, Mathf.Max(0, result.perfects), Mathf.Max(0, result.total)),
                "fr-minigame-result-detail", UiKit.Muted));
            pendingResult = result;
            resultAwaitingConfirmation = true;
            var next = PauseAction(UiText.Get("harvest.collect"), UiKit.Cyan, ContinueResult,
                "minigame-collect-resources", true);
            panel.Add(next);
            overlay.Add(panel);
        }

        static string Grade(float quality)
        {
            if (quality >= 0.9f) return "S";
            if (quality >= 0.75f) return "A";
            if (quality >= 0.55f) return "B";
            return "C";
        }

        public void ContinueResult()
        {
            if (!resultAwaitingConfirmation) return;
            resultAwaitingConfirmation = false;
            Complete(pendingResult);
        }

        void Complete(MinigameResult result)
        {
            resultAwaitingConfirmation = false;
            if (overlay != null) overlay.RemoveFromHierarchy();
            var callback = onDone;
            onDone = null;
            callback?.Invoke(result);
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
        const int Stages = NodeHarvestRun.StageCount;
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
        NodeHarvestRun run;
        float pulse, pop, nodeShake, frameW, frameH, lookY;
        int rockSeed;
        int step { get { return run.CompletedStages; } }
        VisualElement[] pipFill;
        VisualElement swingFill;

        public NodeGame(bool tree) { this.tree = tree; }

        protected override string Title { get { return UiText.Get(tree ? "harvest.node.title.tree" : "harvest.node.title.rock"); } }
        protected override string DefaultToolName { get { return UiText.Get(tree ? "harvest.tool.axe" : "harvest.tool.pick"); } }
        protected override MinigameKind FeedbackKind { get { return tree ? MinigameKind.Chop : MinigameKind.Mine; } }
        protected override string Hint { get { return UiText.Get("harvest.node.hint"); } }
        protected override Color StageBg { get { return new Color(color.r * 0.05f + 0.01f, color.g * 0.05f + 0.015f, color.b * 0.06f + 0.03f); } }

        // ------------------------------------------------------------ UI
        protected override void Build()
        {
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
            run = new NodeHarvestRun(toolDamage, difficulty);
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
            if (!CanInteract || tool == null) return;
            if (run.Cooldown > 0f) { HarvestFeedback.Play(HarvestCue.Recovering, FeedbackKind); return; }

            Ray ray = PointerRay(e);
            Vector3 sp = spot.position;
            float d = Vector3.Cross(ray.direction, sp - ray.origin).magnitude;
            Vector3 hitW;
            int kind;
            if (d <= spotR * 0.4f) { kind = 2; hitW = sp; }
            else if (d <= spotR) { kind = 1; hitW = sp; }
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
            }

            Strike(kind, hitW);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void DebugStrike(HarvestStrike strike)
        {
            if (spot != null) Strike((int)strike, spot.position);
        }
#endif

        void Strike(int kind, Vector3 hitW)
        {
            if (!CanInteract || run == null || tool == null) return;
            int previousStep = step;
            if (!run.TryStrike((HarvestStrike)kind)) return;
            SetToolState(UiText.Get("harvest.state.recovering"), false);
            tool.Strike(stage.root.InverseTransformPoint(hitW), run.SwingSeconds);
            if (kind == 2)
            {
                HarvestFeedback.Play(HarvestCue.Perfect, FeedbackKind);
                stage.Shake(0.5f); nodeShake = 1f; pop = 1f;
                Fx.Burst(hitW, new Color(2f, 1.6f, 0.5f), 26, 4.5f, 0.2f);
                Fx.Burst(hitW, new Color(rawColor.r, rawColor.g, rawColor.b) * 1.6f, 14, 3f, 0.16f);
                Fx.Ripple(hitW + Vector3.back * 0.2f, new Color(2f, 1.6f, 0.5f, 1f), 1.6f, 0.4f);
            }
            else if (kind == 1)
            {
                HarvestFeedback.Play(HarvestCue.Impact, FeedbackKind);
                stage.Shake(0.3f); nodeShake = 0.7f; pop = 0.6f;
                Fx.Burst(hitW, new Color(1.6f, 1.3f, 0.5f), 16, 3.5f, 0.16f);
            }
            else if (kind == 0)
            {
                HarvestFeedback.Play(HarvestCue.Glance, FeedbackKind);
                stage.Shake(0.1f); nodeShake = 0.25f;
                Fx.Burst(hitW, new Color(0.6f, 0.6f, 0.7f), 6, 1.8f, 0.1f);
            }
            else HarvestFeedback.Play(HarvestCue.Miss, FeedbackKind);

            if (kind < 0) return;
            if (kind >= 1) MoveSpot();
            if (step != previousStep) BreakStep();
        }

        // ------------------------------------------------------------ breaking
        void BreakStep()
        {
            HarvestFeedback.Play(HarvestCue.Break, FeedbackKind);
            stage.Shake(0.8f);
            if (step >= Stages) { FinalBreak(); return; }
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
            Finish(new MinigameResult { quality = run.Quality, perfects = run.Perfects, total = run.Swings });
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
            run.Advance(dt);
            SetToolState(UiText.Get(run.Cooldown > 0f ? "harvest.state.recovering" : "harvest.ready"),
                run.Cooldown <= 0f);
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

            float ready = run.Cooldown > 0f ? 0.45f : 1f;
            float pu = 1f + 0.12f * Mathf.Sin(pulse);
            spotCore.localScale = Vector3.one * spotR * 0.9f * pu * ready;
            spotRing.localScale = Vector3.one * spotR * 3.2f * (1f + 0.1f * Mathf.Sin(pulse * 0.7f)) * ready;
            spotGlow.localScale = Vector3.one * spotR * 5.5f * ready;

            for (int i = 0; i < Stages; i++)
            {
                float f = i < step ? 1f : i == step ? Mathf.Clamp01(run.Health / run.StageHealth) : 0f;
                pipFill[i].style.width = Length.Percent(f * 100f);
            }
            swingFill.style.width = Length.Percent((1f - run.Cooldown / run.SwingSeconds) * 100f);
            swingFill.style.backgroundColor = run.Cooldown > 0f ? new Color(1f, 1f, 1f, 0.5f) : color;
        }
    }

    /// <summary>Hold to bore: keep the gauge in the moving band while a 3D drill sinks into the ground. No timer - better drills sink faster.</summary>
    public class DrillGame : Minigame
    {
        VisualElement gauge, band, needle, progFill;
        DrillHarvestRun run;
        float sparkT, chunkT, spinAngle, impact;
        float t { get { return run.Elapsed; } }
        float pressure { get { return run.Pressure; } }
        float progress { get { return run.Progress; } }
        bool inside { get { return run.Inside; } }
        bool contact { get { return run.Contact; } }
        bool holding;
        Transform rig, spin, ore, oreGlow, dust;
        Material oreMat;
        Mesh chunkMesh;
        float frameW = 5.4f, frameH = 7.8f;

        protected override string Title { get { return UiText.Get("harvest.drill.title"); } }
        protected override string DefaultToolName { get { return UiText.Get("harvest.tool.drill"); } }
        protected override MinigameKind FeedbackKind { get { return MinigameKind.Drill; } }
        protected override string Hint { get { return UiText.Get("harvest.drill.hint"); } }
        protected override string ResultMetric { get { return UiText.Get("harvest.drill.metric"); } }
        protected override Color StageBg { get { return new Color(color.r * 0.04f + 0.01f, color.g * 0.04f + 0.012f, color.b * 0.05f + 0.025f); } }

        protected override void Build()
        {
            run = new DrillHarvestRun(toolDamage, difficulty, tolerance);

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

            var btn = UiKit.Btn(UiText.Get("harvest.drill.hold-control"), color, null, true);
            btn.style.minWidth = 420;
            // TrickleDown: the Button's own clickable stops pointer events before normal callbacks run.
            HoldArea(btn);
            HoldArea(arena);
            controls.Add(btn);
        }

        void HoldArea(VisualElement el)
        {
            el.RegisterCallback<PointerDownEvent>(e =>
            {
                if (!CanInteract) return;
                holding = true;
                el.CapturePointer(e.pointerId);
            }, TrickleDown.TrickleDown);
            el.RegisterCallback<PointerUpEvent>(e => { holding = false; if (el.HasPointerCapture(e.pointerId)) el.ReleasePointer(e.pointerId); }, TrickleDown.TrickleDown);
            el.RegisterCallback<PointerCancelEvent>(e => holding = false, TrickleDown.TrickleDown);
            el.RegisterCallback<PointerCaptureOutEvent>(e => holding = false);
        }

        protected override void BuildScene()
        {
            var st = stage;
            Color rim = new Color(rawColor.r, rawColor.g, rawColor.b) * 1.4f;
            st.GlowQuad(st.root, new Color(color.r, color.g, color.b, 0.3f), 12f, new Vector3(0f, 1.5f, 5f));

            var groundM = st.Lit(new Color(0.18f + color.r * 0.2f, 0.18f + color.g * 0.2f, 0.2f + color.b * 0.2f), rim, 2f, 3f, 0f);
            st.AddMesh(MeshKit.Cyl(2.8f, 2.6f, 0.9f, 22), groundM, st.root, new Vector3(0f, -0.9f, 0f));

            // The ore node the drill bites into: shrinks and cracks as you work it.
            float seed = UnityEngine.Random.value * 50f;
            oreMat = st.Lit(new Color(0.2f + color.r * 0.25f, 0.2f + color.g * 0.25f, 0.22f + color.b * 0.25f), rim * 1.3f, 3f, 3.5f, 0f, seed);
            ore = st.AddMesh(MeshKit.Ico(2, d => OreR * (0.82f + 0.3f * Mathf.PerlinNoise(d.x * 1.7f + seed, d.y * 1.7f + d.z * 1.3f)) * (d.y < -0.2f ? 0.85f : 1f)),
                oreMat, st.root, new Vector3(0f, OreCenter, 0f)).transform;
            ore.localScale = new Vector3(1.15f, 0.9f, 1.15f);
            oreGlow = st.GlowQuad(st.root, new Color(color.r, color.g, color.b, 0.5f), 2.6f, new Vector3(0f, OreCenter, 0.6f));
            chunkMesh = st.Own(MeshKit.Ico(0, d => 0.5f));

            dust = st.GlowQuad(st.root, new Color(1.4f, 1.0f, 0.4f, 0.6f), 1.6f, new Vector3(0f, OreTop(), -0.6f));

            rig = new GameObject("Rig").transform;
            rig.SetParent(st.root, false);
            spin = new GameObject("Spin").transform;
            spin.SetParent(rig, false);

            HarvestToolVisual.Palette(toolLevel, rawColor, out var casingColor, out var rimC);
            if (!UseDrillPrefab(rimC)) BuildFallbackBit(rimC);
            var casing = st.Lit(casingColor, rimC, 0f, 6f);
            var active = st.Lit(casingColor * 0.6f, rimC * 1.6f, 0f, 3f);
            HarvestToolVisual.AddTierHardware(st, spin, toolLevel, casing, active);
            rig.localPosition = new Vector3(0f, run.TipY, 0f);
        }

        const float OreR = DrillHarvestRun.OreRadius, OreCenter = DrillHarvestRun.OreCenter;
        float OreScale { get { return run.OreScale; } }
        float OreTop() { return run.OreTop; }

        // Uses the moving part ("PartsToMove": drill head + staff) of the real drill structure, tip at the rig origin.
        bool UseDrillPrefab(Color rimC)
        {
            var prefab = ModelLibrary.Prefab("struct_drill");
            if (prefab == null) return false;
            var inst = ModelLibrary.InstantiateInert(prefab);
            inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            inst.transform.localScale = Vector3.one;
            var partsT = inst.transform.Find("PartsToMove");
            if (partsT == null) { UnityEngine.Object.Destroy(inst); return false; }
            var parts = partsT.gameObject;
            Bounds b;
            if (!ModelLibrary.MeshBounds(parts, out b)) { UnityEngine.Object.Destroy(inst); return false; }
            Vector3 lp = partsT.localPosition; Quaternion lr = partsT.localRotation; Vector3 ls = partsT.localScale;

            const float height = 3.2f;
            float s = height / Mathf.Max(0.001f, b.size.y);
            var holder = new GameObject("DrillBit").transform;
            holder.SetParent(spin, false);
            holder.localScale = Vector3.one * s;
            parts.transform.SetParent(holder, false);
            parts.transform.localRotation = lr; parts.transform.localScale = ls;
            // Bounds were measured with the prefab at the origin, so they're already in holder space: tip to origin, centered on the axis.
            parts.transform.localPosition = lp - new Vector3(b.center.x, b.min.y, b.center.z);
            UnityEngine.Object.Destroy(inst);
            foreach (var r in parts.GetComponentsInChildren<MeshRenderer>())
            {
                var src = r.sharedMaterial;
                Color baseC = src != null && src.HasProperty("_BaseColor") ? src.GetColor("_BaseColor") : (src != null ? src.color : Color.gray);
                var m = stage.Lit(baseC, rimC, 0f, 14f, 0f);
                var arr = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                for (int i = 0; i < arr.Length; i++) arr[i] = m;
                r.sharedMaterials = arr;
            }
            return true;
        }

        void BuildFallbackBit(Color rimC)
        {
            var st = stage;
            Color headC = new Color(0.7f, 0.74f, 0.8f);
            var bitM = st.Lit(headC, rimC, 0f, 14f, 0f);
            var glowM = st.Lit(headC * 0.6f, rimC * 1.4f, 0f, 3f, 0f);
            st.AddMesh(MeshKit.Cyl(0.02f, 0.42f, 1.7f, 8), bitM, spin, Vector3.zero);
            for (int i = 0; i < 3; i++)
                st.Prim(PrimitiveType.Cube, spin, new Vector3(0f, 0.5f + i * 0.42f, 0f), new Vector3(0.5f + i * 0.2f, 0.07f, 0.07f), new Vector3(0f, i * 60f, 0f), glowM);
            st.Prim(PrimitiveType.Cylinder, spin, new Vector3(0f, 2.6f, 0f), new Vector3(0.2f, 1.0f, 0.2f), Vector3.zero, glowM);
        }

        protected override void Frame(float aspect) { FramePose(frameW, frameH, 2.0f, aspect, 1.6f); }

        protected override void Tick(float dt)
        {
            // Safety net in case a pointer-up is swallowed elsewhere; Space also works as hold.
            if (holding && !Input.GetMouseButton(0) && Input.touchCount == 0) holding = false;
            bool press = holding || Input.GetKey(KeyCode.Space);
            Advance(dt, press);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public DrillHarvestRun DebugRun { get { return run; } }
        public void DebugAdvance(float seconds, bool press)
        {
            if (CanInteract && rig != null) Advance(seconds, press);
        }
#endif

        void Advance(float dt, bool press)
        {
            bool wasContact = contact;
            run.Advance(dt, press);
            string stateKey = !contact ? "harvest.drill.hold" :
                inside ? "harvest.drill.locked" : "harvest.drill.adjust";
            SetToolState(UiText.Get(stateKey), contact && inside);
            float center = run.BandCenter;
            float half = run.BandHalfWidth;
            band.style.bottom = Length.Percent((center - half) * 100f);
            band.style.height = Length.Percent(half * 2f * 100f);
            needle.style.bottom = Length.Percent(pressure * 100f);
            needle.style.backgroundColor = inside ? Color.white : new Color(1f, 0.4f, 0.4f);

            float top = run.ContactHeight;

            if (contact && !wasContact)
            {
                Vector3 hitP = stage.root.TransformPoint(new Vector3(0f, top, -0.3f));
                Fx.Burst(hitP, inside ? new Color(2.2f, 1.6f, 0.5f) : new Color(1.8f, 0.4f, 0.3f), 18, 4.5f, 0.16f);
                stage.Shake(0.28f);
                impact = 1f;
                Chunks(4, 0.14f, 3.2f);
                HarvestFeedback.Play(inside ? HarvestCue.Perfect : HarvestCue.Impact, FeedbackKind);
            }

            progFill.style.height = Length.Percent(Mathf.Clamp01(progress) * 100f);

            impact = Mathf.MoveTowards(impact, 0f, dt * 5f);
            float shakeAmt = contact ? (inside ? 0.035f : 0.07f) + impact * 0.06f : 0f;
            if (contact) stage.Shake(inside ? 0.05f : 0.09f);
            rig.localPosition = new Vector3(UnityEngine.Random.Range(-1f, 1f) * shakeAmt, Mathf.Max(run.TipY, top - 0.12f), UnityEngine.Random.Range(-1f, 1f) * shakeAmt);
            spinAngle += dt * (press ? 500f + 900f * pressure : 90f) * (contact ? 1.4f : 1f);
            spin.localRotation = Quaternion.Euler(0f, spinAngle, 0f);

            float os = OreScale;
            float jig = contact ? 0.025f + impact * 0.05f : impact * 0.03f;
            ore.localScale = new Vector3(1.15f, 0.9f, 1.15f) * os * (1f - impact * 0.06f);
            ore.localPosition = new Vector3(UnityEngine.Random.Range(-1f, 1f) * jig, OreCenter - (1f - os) * 0.35f, UnityEngine.Random.Range(-1f, 1f) * jig);
            oreGlow.localScale = Vector3.one * (2.2f + Mathf.Clamp01(progress) * 1.6f + (contact && inside ? Mathf.Sin(t * 40f) * 0.3f : 0f)) * os;
            dust.localPosition = new Vector3(0f, top, -0.6f);
            dust.localScale = Vector3.one * (contact ? (inside ? 2.2f : 1.4f) + Mathf.Sin(t * 30f) * 0.3f + impact : 0.001f);

            sparkT -= dt;
            if (sparkT <= 0f && contact)
            {
                Vector3 p = stage.root.TransformPoint(new Vector3(UnityEngine.Random.Range(-0.15f, 0.15f), top + 0.05f, -0.3f));
                if (inside) Fx.Burst(p, new Color(2f, 1.5f, 0.5f), 8, 3.4f, 0.12f);
                else Fx.Burst(p, new Color(1.6f, 0.3f, 0.3f), 6, 2.2f, 0.14f);
                sparkT = 0.06f;
            }
            chunkT -= dt;
            if (chunkT <= 0f && contact && inside)
            {
                Chunks(2, 0.1f, 2.6f);
                chunkT = 0.22f;
            }

            if (progress >= 1f)
            {
                stage.Shake(0.6f);
                Chunks(16, 0.2f, 5f);
                Fx.Burst(stage.root.TransformPoint(new Vector3(0f, OreCenter, -0.2f)), new Color(rawColor.r, rawColor.g, rawColor.b) * 1.8f, 40, 6f, 0.24f);
                Finish(new MinigameResult { quality = run.Quality, perfects = Mathf.RoundToInt(run.InBandSeconds), total = Mathf.Max(1, Mathf.RoundToInt(t)) });
            }
        }

        void Chunks(int n, float size, float speed)
        {
            Vector3 c = stage.root.TransformPoint(new Vector3(0f, OreTop(), -0.2f));
            for (int i = 0; i < n; i++)
                stage.Chunk(chunkMesh, oreMat, c + UnityEngine.Random.insideUnitSphere * 0.25f, size * UnityEngine.Random.Range(0.6f, 1.4f),
                    new Vector3(UnityEngine.Random.Range(-1f, 1f) * speed, UnityEngine.Random.Range(0.5f, 1f) * speed, UnityEngine.Random.Range(-1f, 0.2f) * speed), 12f, UnityEngine.Random.Range(0.7f, 1.2f));
        }
    }

    /// <summary>Time each pump to the moving pressure window and draw gas from a glowing planetary vent.</summary>
    public class SiphonGame : Minigame
    {
        const int Pulses = SiphonHarvestRun.PulseCount;
        VisualElement track, catchBand, needle;
        SiphonHarvestRun run;
        float elapsed { get { return run.Elapsed; } }
        int pulses { get { return run.Pulses; } }
        Transform reservoir, vent, stream;
        readonly Transform[] bubbles = new Transform[9];
        float frameW = 6.4f, frameH = 7.6f;

        protected override string Title { get { return UiText.Get("harvest.siphon.title"); } }
        protected override string DefaultToolName { get { return UiText.Get("harvest.tool.hand-pump"); } }
        protected override MinigameKind FeedbackKind { get { return MinigameKind.Siphon; } }
        protected override string ResultMetric { get { return UiText.Get("harvest.siphon.metric"); } }
        protected override string Hint { get { return UiText.Get("harvest.siphon.hint"); } }
        protected override Color StageBg { get { return new Color(color.r * 0.04f + 0.005f, color.g * 0.04f + 0.01f, color.b * 0.05f + 0.03f); } }

        protected override void Build()
        {
            run = new SiphonHarvestRun(difficulty, tolerance);
            status.style.display = DisplayStyle.Flex;
            track = UiKit.Div();
            track.pickingMode = PickingMode.Ignore;
            track.style.position = Position.Absolute;
            track.style.left = 76; track.style.right = 76; track.style.top = Length.Percent(69);
            track.style.height = 36;
            track.style.backgroundColor = new Color(0f, 0f, 0f, 0.62f);
            UiKit.Radius(track, 18f);
            UiKit.Border(track, new Color(1f, 1f, 1f, 0.35f), 2f);

            catchBand = UiKit.Div();
            catchBand.pickingMode = PickingMode.Ignore;
            catchBand.style.position = Position.Absolute;
            catchBand.style.top = 0; catchBand.style.bottom = 0;
            catchBand.style.backgroundColor = new Color(UiKit.Lime.r, UiKit.Lime.g, UiKit.Lime.b, 0.52f);
            UiKit.Radius(catchBand, 16f);
            UiKit.Halo(catchBand, UiKit.Lime, 22f, 0.4f);
            track.Add(catchBand);

            needle = UiKit.Div();
            needle.pickingMode = PickingMode.Ignore;
            needle.style.position = Position.Absolute;
            needle.style.top = -5; needle.style.bottom = -5; needle.style.width = 8;
            needle.style.backgroundColor = Color.white;
            UiKit.Radius(needle, 4f);
            UiKit.Halo(needle, color, 18f, 0.8f);
            track.Add(needle);
            arena.Add(track);

            var pump = UiKit.Btn(UiText.Get("harvest.siphon.pump"), color, null, true);
            pump.clicked += Pump;
            controls.Add(pump);
            UpdateStatus(UiText.Get("harvest.siphon.watch"));
        }

        protected override void BuildScene()
        {
            var st = stage;
            Color rim = new Color(rawColor.r, rawColor.g, rawColor.b) * 2f;
            var metal = st.Lit(new Color(0.18f, 0.24f, 0.34f), rim * 0.6f, 1f, 8f, 0f);
            var gas = st.Lit(new Color(color.r * 0.2f, color.g * 0.2f, color.b * 0.24f), rim * 2.5f, 0f, 3f, 0f);
            st.GlowQuad(st.root, new Color(color.r, color.g, color.b, 0.28f), 13f, new Vector3(0f, 1f, 5f));
            st.Prim(PrimitiveType.Cube, st.root, new Vector3(-1.9f, 0.9f, 0f), new Vector3(0.16f, 2.3f, 0.16f), Vector3.zero, metal);
            st.Prim(PrimitiveType.Cube, st.root, new Vector3(1.9f, 0.9f, 0f), new Vector3(0.16f, 2.3f, 0.16f), Vector3.zero, metal);
            st.Prim(PrimitiveType.Cube, st.root, new Vector3(0f, 2.05f, 0f), new Vector3(4f, 0.16f, 0.16f), Vector3.zero, metal);
            st.Prim(PrimitiveType.Cylinder, st.root, new Vector3(0f, -0.55f, 0f), new Vector3(0.85f, 1.55f, 0.85f), Vector3.zero, metal);
            st.Prim(PrimitiveType.Cylinder, st.root, new Vector3(0f, 0.3f, -0.02f), new Vector3(0.43f, 0.55f, 0.43f), Vector3.zero, gas);
            reservoir = st.AddMesh(MeshKit.Ico(2, d => 0.95f * (0.84f + 0.18f * Mathf.PerlinNoise(d.x * 2f, d.y * 2f + d.z))),
                gas, st.root, new Vector3(0f, 1.05f, 0.02f)).transform;
            vent = HarvestToolVisual.Build(st, MinigameKind.Siphon, toolLevel, rawColor, new Vector3(0f, 1.9f, 0f));
            stream = st.GlowQuad(st.root, new Color(color.r, color.g, color.b, 0.8f), 1.5f, new Vector3(0f, 2.8f, -0.25f));
            var bubbleMat = st.Additive(new Color(1f, 1.5f, 2f, 0.7f));
            for (int i = 0; i < bubbles.Length; i++)
            {
                bubbles[i] = st.AddMesh(MeshKit.Ico(1, d => 0.15f), bubbleMat, st.root,
                    new Vector3(UnityEngine.Random.Range(-0.8f, 0.8f), 0f, UnityEngine.Random.Range(-0.2f, 0.2f))).transform;
            }
        }

        protected override void Frame(float aspect) { FramePose(frameW, frameH, 0.85f, aspect, 0.9f); }

        protected override void Tick(float dt)
        {
            run.Advance(dt);
            SetToolState(UiText.Get(run.Cooldown > 0f
                    ? "harvest.state.recovering" : "harvest.siphon.ready"),
                run.Cooldown <= 0f);
            catchBand.style.left = Length.Percent(Mathf.Clamp01(run.Target - run.BandWidth) * 100f);
            catchBand.style.width = Length.Percent(run.BandWidth * 200f);
            needle.style.left = Length.Percent(run.Needle * 100f);

            float pulse = 1f + 0.08f * Mathf.Sin(elapsed * 5f);
            reservoir.localScale = Vector3.one * pulse;
            vent.localRotation = Quaternion.Euler(0f, elapsed * 28f, 0f);
            stream.localScale = Vector3.one * (0.75f + 0.22f * pulse);
            for (int i = 0; i < bubbles.Length; i++)
            {
                float phase = elapsed * (0.65f + difficulty * 0.3f) + i * 0.73f;
                bubbles[i].localPosition = new Vector3(Mathf.Sin(phase * 1.7f) * 0.62f, -0.2f + Mathf.PingPong(phase, 2.4f), Mathf.Cos(phase) * 0.22f);
            }
        }

        void Pump()
        {
            if (!CanInteract || !run.TryPump(out float hit)) return;
            SetToolState(UiText.Get("harvest.state.recovering"), false);
            string resultKey = hit >= 0.78f ? "harvest.siphon.clean" :
                hit > 0.05f ? "harvest.siphon.partial" : "harvest.siphon.lost";
            UpdateStatus(UiText.Get(resultKey));
            if (hit > 0.05f)
            {
                Vector3 point = stage.root.TransformPoint(reservoir.localPosition);
                Fx.Burst(point, color * (hit >= 0.78f ? 2f : 1.1f), hit >= 0.78f ? 18 : 8, 3.5f, 0.18f);
                stage.Shake(hit >= 0.78f ? 0.28f : 0.12f);
                HarvestFeedback.Play(hit >= 0.78f ? HarvestCue.Perfect : HarvestCue.Impact, FeedbackKind);
            }
            else HarvestFeedback.Play(HarvestCue.Miss, FeedbackKind);
            if (pulses >= Pulses)
                Finish(new MinigameResult { quality = run.Quality, perfects = run.Perfects, total = Pulses });
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public SiphonHarvestRun DebugRun { get { return run; } }
        public void DebugPump() { Pump(); }
        public void DebugAdvance(float seconds) { if (CanInteract) Tick(seconds); }
#endif

        void UpdateStatus(string message)
        {
            status.text = UiText.Get("harvest.siphon.status", pulses, Pulses, message);
        }
    }

    /// <summary>Use sonar echoes to locate buried seams without wasting the scanner's limited pulses.</summary>
    public class ExcavationGame : Minigame
    {
        const int GridSide = ExcavationHarvestRun.GridSide;
        const int Seams = ExcavationHarvestRun.SeamCount;
        const int MaxPings = ExcavationHarvestRun.MaxPings;
        readonly Button[] cells = new Button[GridSide * GridSide];
        ExcavationHarvestRun run;
        int pings { get { return run.Pings; } }
        int seamsFound { get { return run.SeamsFound; } }
        float elapsed;
        Transform scanner, echo, pulseRing, ground;
        readonly Vector3[] echoPositions = { new Vector3(-0.75f, -0.28f, -0.15f), new Vector3(0.5f, 0.05f, -0.15f), new Vector3(0.15f, 0.4f, -0.15f) };
        const float frameW = 6.2f, frameH = 7.3f;

        protected override string Title { get { return UiText.Get("harvest.excavate.title"); } }
        protected override string DefaultToolName { get { return UiText.Get("harvest.tool.trowel"); } }
        protected override MinigameKind FeedbackKind { get { return MinigameKind.Excavate; } }
        protected override string ResultMetric { get { return UiText.Get("harvest.excavate.metric"); } }
        protected override string Hint { get { return UiText.Get("harvest.excavate.hint"); } }
        protected override Color StageBg { get { return new Color(0.008f, 0.025f, 0.045f); } }

        protected override void Build()
        {
            arena.style.flexGrow = 0f;
            arena.style.flexBasis = Length.Percent(38f);
            arena.style.minHeight = 220f;
            status.style.display = DisplayStyle.Flex;
            controls.style.flexDirection = FlexDirection.Column;
            controls.style.alignItems = Align.Stretch;
            controls.style.flexShrink = 0f;
            overlay.Remove(status);
            controls.Add(status);
            status.style.marginBottom = 8f;
            run = new ExcavationHarvestRun(UnityEngine.Random.Range(0, int.MaxValue));
            var grid = UiKit.Div();
            grid.style.flexDirection = FlexDirection.Row;
            grid.style.flexWrap = Wrap.Wrap;
            grid.style.justifyContent = Justify.Center;
            grid.style.alignSelf = Align.Center;
            grid.style.width = Length.Percent(84f);
            for (int i = 0; i < cells.Length; i++)
            {
                int cell = i;
                var button = UiKit.Btn(UiText.Get("harvest.excavate.ping"), color, () => Ping(cell), false);
                button.name = "excavate-cell-" + i;
                button.style.width = Length.Percent(30f);
                button.style.height = 58;
                button.style.marginLeft = 3;
                button.style.marginRight = 3;
                button.style.marginTop = 3;
                button.style.marginBottom = 3;
                cells[i] = button;
                grid.Add(button);
            }
            controls.Add(grid);
            UpdateStatus(UiText.Get("harvest.excavate.progress", 0, Seams, MaxPings));
        }

        protected override void BuildScene()
        {
            var st = stage;
            Color rim = new Color(rawColor.r, rawColor.g, rawColor.b) * 1.7f;
            var basalt = st.Lit(new Color(0.06f, 0.12f, 0.18f), new Color(0.08f, 0.35f, 0.5f), 1f, 6f, 0f);
            var stratum = st.Lit(new Color(0.12f, 0.2f, 0.27f), rim * 0.8f, 1f, 8f, 0f);
            var crystal = st.Lit(new Color(0.08f, 0.35f, 0.45f), new Color(color.r, color.g, color.b) * 2.8f, 0f, 3f, 0f);
            ground = st.AddMesh(MeshKit.Cyl(2.6f, 2.3f, 0.7f, 24), basalt, st.root, new Vector3(0f, -1.35f, 0f)).transform;
            st.AddMesh(MeshKit.Cyl(2.25f, 2.2f, 0.1f, 24), stratum, st.root, new Vector3(0f, -0.63f, 0f));
            st.AddMesh(MeshKit.Cyl(1.85f, 1.8f, 0.1f, 24), basalt, st.root, new Vector3(0f, -0.52f, 0f));
            echo = st.AddMesh(MeshKit.Ico(1, d => 0.42f * (0.84f + 0.12f * Mathf.PerlinNoise(d.x * 3f, d.y * 3f + d.z))),
                crystal, st.root, echoPositions[0]).transform;
            echo.gameObject.SetActive(false);

            scanner = HarvestToolVisual.Build(st, MinigameKind.Excavate, toolLevel, rawColor, new Vector3(0f, 1.2f, 0.1f));
            pulseRing = st.GlowQuad(st.root, new Color(color.r, color.g, color.b, 0.8f), 1.7f,
                new Vector3(0f, -0.48f, -0.1f), true);
            pulseRing.localScale = Vector3.zero;
            st.GlowQuad(st.root, new Color(color.r, color.g, color.b, 0.2f), 11f, new Vector3(0f, 0.6f, 5f));
        }

        protected override void Frame(float aspect) { FramePose(frameW, frameH, 0.1f, aspect, 1.1f); }

        void Ping(int cell)
        {
            if (!CanInteract || !run.TryPing(cell, out int distance)) return;
            cells[cell].SetEnabled(false);
            if (distance == 0)
            {
                echo.localPosition = echoPositions[(seamsFound - 1) % echoPositions.Length];
                echo.gameObject.SetActive(true);
                Fx.Burst(stage.root.TransformPoint(echo.localPosition), color * 1.8f, 22, 4f, 0.18f);
                stage.Shake(0.34f);
                HarvestFeedback.Play(HarvestCue.Perfect, FeedbackKind);
                if (seamsFound >= Seams || pings >= MaxPings) { Complete(); return; }
                foreach (var button in cells)
                {
                    button.SetEnabled(true);
                    button.text = UiText.Get("harvest.excavate.ping");
                    button.style.backgroundColor = StyleKeyword.Null;
                    button.style.color = StyleKeyword.Null;
                }
                SetToolState(UiText.Get("harvest.excavate.new-seam"));
                UpdateStatus(UiText.Get("harvest.excavate.progress", seamsFound, Seams, MaxPings - pings));
                return;
            }

            Color echoColor = distance == 1 ? new Color(0.9f, 0.92f, 0.32f) :
                distance == 2 ? new Color(0.95f, 0.55f, 0.2f) : new Color(0.18f, 0.38f, 0.5f);
            cells[cell].text = UiText.Get(distance == 1 ? "harvest.excavate.near" :
                distance == 2 ? "harvest.excavate.warm" : "harvest.excavate.faint");
            cells[cell].style.backgroundColor = new Color(echoColor.r * 0.22f, echoColor.g * 0.22f, echoColor.b * 0.22f);
            cells[cell].style.color = echoColor;
            SetToolState(UiText.Get(distance == 1
                ? "harvest.excavate.near-seam" : "harvest.excavate.follow-echo"));
            pulseRing.localScale = Vector3.one * 1.1f;
            HarvestFeedback.Play(distance == 1 ? HarvestCue.ScanNear : HarvestCue.ScanFar, FeedbackKind);
            string echoKey = distance == 1 ? "harvest.excavate.strong-echo" :
                distance == 2 ? "harvest.excavate.warm-return" : "harvest.excavate.weak-return";
            UpdateStatus(UiText.Get("harvest.excavate.echo-status", UiText.Get(echoKey),
                seamsFound, Seams, MaxPings - pings));
            if (pings >= MaxPings) Complete();
        }

        void Complete()
        {
            UpdateStatus(UiText.Get("harvest.excavate.located", seamsFound, Seams));
            Finish(new MinigameResult { quality = run.Quality, perfects = seamsFound, total = Seams });
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public ExcavationHarvestRun DebugRun { get { return run; } }
        public void DebugPing(int cell) { Ping(cell); }
#endif

        void UpdateStatus(string message) { status.text = message; }

        protected override void Tick(float dt)
        {
            elapsed += dt;
            float ping = Mathf.PingPong(elapsed * 0.9f, 1.8f);
            pulseRing.localScale = Vector3.one * (0.35f + ping);
            scanner.localRotation = Quaternion.Euler(0f, elapsed * (25f + difficulty * 20f), 0f);
            ground.localScale = Vector3.one * (1f + Mathf.Sin(elapsed * 1.5f) * 0.012f);
            if (echo.gameObject.activeSelf)
                echo.localScale = Vector3.one * (0.88f + Mathf.Sin(elapsed * 5f) * 0.12f);
        }
    }

    /// <summary>Cast a magnetic claw to collect drifting wreckage before it escapes the salvage field.</summary>
    public class SalvageGame : Minigame
    {
        const int Targets = SalvageHarvestRun.TargetCount;
        const int MaxCasts = SalvageHarvestRun.MaxCasts;
        readonly Transform[] debris = new Transform[Targets];
        readonly float[] radii = new float[Targets];
        readonly float[] phases = new float[Targets];
        readonly SalvageHarvestRun run = new SalvageHarvestRun();
        int casts { get { return run.Casts; } }
        int captured { get { return run.Captured; } }
        float elapsed;
        Transform claw, tether;
        const float frameW = 8f, frameH = 7.2f;

        protected override string Title { get { return UiText.Get("harvest.salvage.title"); } }
        protected override string DefaultToolName { get { return UiText.Get("harvest.tool.grabber"); } }
        protected override MinigameKind FeedbackKind { get { return MinigameKind.Salvage; } }
        protected override string ResultMetric { get { return UiText.Get("harvest.salvage.metric"); } }
        protected override string Hint { get { return UiText.Get("harvest.salvage.hint"); } }
        protected override Color StageBg { get { return new Color(0.018f, 0.015f, 0.04f); } }

        protected override void Build()
        {
            status.style.display = DisplayStyle.Flex;
            arena.RegisterCallback<PointerDownEvent>(OnCast);
            UpdateStatus(UiText.Get("harvest.salvage.help", Targets));
        }

        protected override void BuildScene()
        {
            var st = stage;
            Color accent = new Color(rawColor.r, rawColor.g, rawColor.b);
            st.GlowQuad(st.root, new Color(color.r, color.g, color.b, 0.25f), 16f, new Vector3(0f, 0f, 8f));
            var field = st.Lit(new Color(0.08f, 0.08f, 0.17f), accent * 0.6f, 1f, 5f, 0f);
            var alloy = st.Lit(new Color(0.32f, 0.38f, 0.48f), accent * 1.3f, 0f, 9f, 0f);
            var glowing = st.Lit(new Color(0.12f, 0.35f, 0.42f), accent * 2f, 0f, 4f, 0f);
            st.AddMesh(MeshKit.Cyl(4.6f, 4.6f, 0.06f, 32), field, st.root, new Vector3(0f, -2.45f, 1.3f));
            tether = st.GlowQuad(st.root, new Color(color.r, color.g, color.b, 0.72f), 0.18f, new Vector3(0f, -1.8f, -0.7f));
            tether.localScale = new Vector3(0.06f, 0f, 0.06f);

            for (int i = 0; i < Targets; i++)
            {
                var root = new GameObject("SalvageTarget" + i).transform;
                root.SetParent(st.root, false);
                root.localPosition = new Vector3(-3.4f + (i % 3) * 3.4f, -0.8f + (i / 3) * 1.7f, (i % 2) * 0.45f);
                phases[i] = i * 0.93f;
                radii[i] = 0.48f + (i % 3) * 0.08f;
                Color targetColor = i % 2 == 0 ? new Color(0.65f, 0.88f, 1.1f) : accent * 1.5f;
                var material = i % 2 == 0 ? alloy : glowing;
                if (i % 3 == 0)
                {
                    st.Prim(PrimitiveType.Cube, root, Vector3.zero, new Vector3(0.8f, 0.58f, 0.52f), new Vector3(0f, 0f, 18f), material);
                    st.Prim(PrimitiveType.Cube, root, Vector3.zero, new Vector3(0.92f, 0.08f, 0.08f), Vector3.zero, alloy);
                }
                else if (i % 3 == 1)
                {
                    st.AddMesh(MeshKit.Ico(1, d => 0.47f * (0.8f + 0.25f * Mathf.PerlinNoise(d.x * 3f + i, d.y * 3f + d.z))),
                        material, root, Vector3.zero);
                    st.Prim(PrimitiveType.Cylinder, root, new Vector3(0f, 0.42f, 0f), new Vector3(0.3f, 0.12f, 0.3f), new Vector3(0f, 0f, 90f), alloy);
                }
                else
                {
                    st.Prim(PrimitiveType.Cube, root, Vector3.zero, new Vector3(0.65f, 0.65f, 0.65f), new Vector3(20f, 30f, 15f), material);
                    st.Prim(PrimitiveType.Cube, root, new Vector3(0.35f, 0f, 0f), new Vector3(0.1f, 0.8f, 0.1f), Vector3.zero, alloy);
                }
                st.GlowQuad(root, new Color(targetColor.r, targetColor.g, targetColor.b, 0.45f), 1.5f, new Vector3(0f, 0f, -0.5f));
                debris[i] = root;
            }

            claw = HarvestToolVisual.Build(st, MinigameKind.Salvage, toolLevel, rawColor, new Vector3(0f, -1.55f, -0.65f));
        }

        protected override void Frame(float aspect) { FramePose(frameW, frameH, -0.15f, aspect, 0.75f); }

        void OnCast(PointerDownEvent e)
        {
            if (!CanInteract || casts >= MaxCasts) return;
            Ray ray = PointerRay(e);
            int hit = -1;
            float nearest = float.MaxValue;
            for (int i = 0; i < debris.Length; i++)
            {
                if (!debris[i].gameObject.activeSelf) continue;
                Vector3 delta = debris[i].position - ray.origin;
                float along = Vector3.Dot(delta, ray.direction);
                if (along < 0f) continue;
                Vector3 closest = ray.origin + ray.direction * along;
                float r = radii[i] * 0.72f;
                if ((debris[i].position - closest).sqrMagnitude > r * r) continue;
                float distance = along - Mathf.Sqrt(Mathf.Max(0f, r * r - (debris[i].position - closest).sqrMagnitude));
                if (distance < nearest) { nearest = distance; hit = i; }
            }

            Cast(hit);
        }

        void Cast(int hit)
        {
            if (!CanInteract || !run.TryCast(hit)) return;
            if (hit >= 0)
            {
                Vector3 point = debris[hit].position;
                debris[hit].gameObject.SetActive(false);
                claw.localPosition = new Vector3(point.x, -1.55f, -0.65f);
                tether.localPosition = (point + claw.position) * 0.5f - stage.root.position;
                tether.localScale = new Vector3(0.08f, Vector3.Distance(point, claw.position) * 0.5f, 0.08f);
                Fx.Burst(point, color * 1.8f, 20, 4f, 0.18f);
                stage.Shake(0.22f);
                HarvestFeedback.Play(HarvestCue.Impact, FeedbackKind);
                UpdateStatus(UiText.Get("harvest.salvage.locked", captured, Targets, MaxCasts - casts));
                SetToolState(UiText.Get("harvest.salvage.recovered-target"));
            }
            else
            {
                HarvestFeedback.Play(HarvestCue.Miss, FeedbackKind);
                UpdateStatus(UiText.Get("harvest.salvage.missed", captured, Targets, MaxCasts - casts));
                SetToolState(UiText.Get("harvest.salvage.aim"), false);
            }

            if (captured >= Targets || casts >= MaxCasts) Complete();
        }

        void Complete()
        {
            UpdateStatus(UiText.Get("harvest.salvage.complete", captured, Targets));
            Finish(new MinigameResult { quality = run.Quality, perfects = captured, total = Targets });
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public SalvageHarvestRun DebugRun { get { return run; } }
        public void DebugCast(int target) { Cast(target); }
#endif

        void UpdateStatus(string message) { status.text = UiText.Get("harvest.salvage.status", casts, MaxCasts, message); }

        protected override void Tick(float dt)
        {
            elapsed += dt;
            for (int i = 0; i < debris.Length; i++)
            {
                if (!debris[i].gameObject.activeSelf) continue;
                float phase = elapsed * (0.28f + difficulty * 0.08f) + phases[i];
                debris[i].localPosition = new Vector3(Mathf.Sin(phase) * 3.15f, -0.85f + Mathf.Sin(phase * 1.7f + i) * 0.85f,
                    (i % 2) * 0.45f + Mathf.Cos(phase * 1.3f) * 0.18f);
                debris[i].localRotation = Quaternion.Euler(phase * 37f, phase * 63f, Mathf.Sin(phase) * 25f);
            }
            claw.localRotation = Quaternion.Euler(0f, Mathf.Sin(elapsed * 1.4f) * 18f, Mathf.Sin(elapsed * 1.8f) * 7f);
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

        protected override string Title { get { return UiText.Get("harvest.travel.title"); } }
        protected override string DefaultToolName { get { return UiText.Get("harvest.tool.ship"); } }
        protected override string ResultMetric { get { return UiText.Get("harvest.travel.metric"); } }
        protected override string Hint { get { return UiText.Get("harvest.travel.hint"); } }
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

        void Status() { status.text = UiText.Get("harvest.travel.status", orbs, hits); }

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
