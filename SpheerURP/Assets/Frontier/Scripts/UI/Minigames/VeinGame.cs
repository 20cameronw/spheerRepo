using UnityEngine;
using UnityEngine.UIElements;

namespace Frontier
{
    public sealed class VeinGame : Minigame
    {
        VeinHarvestRun run;
        readonly Transform[,] rocks = new Transform[6, VeinHarvestRun.Columns];
        readonly Renderer[,] renderers = new Renderer[6, VeinHarvestRun.Columns];
        Material ore, rich, unstable;
        Transform cutter;
        Label readout;
        bool dragging;
        int desiredColumn = VeinHarvestRun.Columns / 2;
        int dragPointer;
        float steerCooldown;
        HarvestHoldInput brake;
        public VeinHarvestRun DebugRun { get { return run; } }
        public bool DebugReady { get { return CanInteract; } }
        public void DebugAdvance(float seconds) { if (CanInteract) Tick(seconds); }
        protected override string Title { get { return UiText.Get("harvest.vein.title"); } }
        protected override string Hint { get { return UiText.Get("harvest.vein.hint"); } }
        protected override string DefaultToolName { get { return UiText.Get("tools.mining"); } }
        protected override MinigameKind FeedbackKind { get { return MinigameKind.Mine; } }
        protected override string ResultMetric { get { return UiText.Get("harvest.vein.metric"); } }

        protected override void Build()
        {
            run = new VeinHarvestRun(Random.Range(1, int.MaxValue), difficulty, toolDamage);
            var left = UiKit.Btn(UiText.Get("harvest.vein.left"), color, () => Steer(-1), true);
            var right = UiKit.Btn(UiText.Get("harvest.vein.right"), color, () => Steer(1), true);
            left.name = "vein-left"; right.name = "vein-right";
            left.style.flexGrow = right.style.flexGrow = 1;
            controls.Add(left); controls.Add(right);
            brake = new HarvestHoldInput(overlay, KeyCode.Space, () => CanInteract);
            var brakeButton = UiKit.Btn(UiText.Get("harvest.vein.brake"), UiKit.Amber, null, true);
            brakeButton.name = "vein-brake";
            brakeButton.style.flexGrow = 1; brakeButton.style.minWidth = 0;
            brake.Bind(brakeButton); controls.Add(brakeButton);
            readout = UiKit.Text("", "fr-small");
            overlay.Insert(overlay.IndexOf(arena), readout);
            arena.RegisterCallback<PointerDownEvent>(e =>
            {
                if (!CanInteract || dragging) return;
                dragging = true;
                dragPointer = e.pointerId;
                arena.CapturePointer(e.pointerId);
                Aim(e.localPosition.x);
            });
            arena.RegisterCallback<PointerMoveEvent>(e => { if (dragging && e.pointerId == dragPointer && CanInteract) Aim(e.localPosition.x); });
            arena.RegisterCallback<PointerUpEvent>(e => { if (e.pointerId == dragPointer) ResetDrag(); });
            arena.RegisterCallback<PointerCancelEvent>(e => { if (e.pointerId == dragPointer) ResetDrag(); });
            arena.RegisterCallback<PointerCaptureOutEvent>(e => { if (e.pointerId == dragPointer) dragging = false; });
            overlay.focusable = true;
            overlay.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode != KeyCode.LeftArrow && e.keyCode != KeyCode.RightArrow) return;
                Steer(e.keyCode == KeyCode.LeftArrow ? -1 : 1);
                e.StopPropagation();
            });
            overlay.Focus();
        }

        void Aim(float x)
        {
            desiredColumn = Mathf.Clamp(Mathf.FloorToInt(x / Mathf.Max(1, ArenaW) * VeinHarvestRun.Columns), 0, 4);
            Steer(desiredColumn - run.Column);
        }

        void Steer(int direction)
        {
            if (!CanInteract || steerCooldown > 0) return;
            run.Steer(direction);
            steerCooldown = 0.12f;
        }

        protected override void ResetInput()
        {
            brake?.Reset();
            ResetDrag();
        }

        void ResetDrag()
        {
            dragging = false;
            if (arena.HasPointerCapture(dragPointer)) arena.ReleasePointer(dragPointer);
        }

        protected override void BuildScene()
        {
            ore = stage.Lit(new Color(0.25f, 0.38f, 0.48f), rawColor, 0.5f, 3f, 0f);
            rich = stage.Lit(new Color(0.22f, 0.65f, 0.72f), rawColor * 2f, 0.7f, 3f, 0f);
            unstable = stage.Lit(new Color(0.12f, 0.08f, 0.08f), new Color(0.4f, 0.08f, 0.03f), 0.2f, 3f, 0f);
            for (int row = 0; row < 6; row++)
                for (int column = 0; column < VeinHarvestRun.Columns; column++)
                {
                    var rock = stage.AddMesh(MeshKit.Ico(1, d => 0.38f * (1f + d.x * d.y * 0.4f)),
                        ore, stage.root, Vector3.zero);
                    rocks[row, column] = rock.transform;
                    renderers[row, column] = rock.GetComponent<Renderer>();
                }
            cutter = HarvestToolVisual.Build(stage, MinigameKind.Mine, toolLevel, rawColor, Vector3.zero);
        }

        protected override void Frame(float aspect)
        {
            FramePose(6.2f, 10.6f, -0.1f, aspect, 0f);
        }

        protected override void Tick(float dt)
        {
            steerCooldown = Mathf.Max(0, steerCooldown - dt);
            if (dragging && desiredColumn != run.Column) Steer(desiredColumn - run.Column);
            int oldDepth = run.Depth, oldIntegrity = run.Integrity, oldRich = run.RichSeams;
            run.Advance(dt, brake.Held);
            if (run.Depth != oldDepth)
            {
                bool damaged = run.Integrity < oldIntegrity;
                HarvestFeedback.Play(damaged ? HarvestCue.Glance : run.RichSeams > oldRich ? HarvestCue.Perfect : HarvestCue.Impact, FeedbackKind);
                stage.Shake(damaged ? 0.3f : 0.1f);
                Fx.Burst(cutter.position, damaged ? new Color(1f, 0.2f, 0.06f) : rawColor, 8, 2f, 0.12f);
            }
            cutter.localPosition = Vector3.Lerp(cutter.localPosition, new Vector3(run.Column - 2, 2.9f, -0.6f), 1f - Mathf.Exp(-dt * 18));
            cutter.localRotation = Quaternion.Euler(0, 0, 180);
            for (int row = 0; row < 6; row++)
                for (int column = 0; column < VeinHarvestRun.Columns; column++)
                {
                    int depth = run.Depth + row;
                    rocks[row, column].gameObject.SetActive(depth < VeinHarvestRun.Rows);
                    if (depth >= VeinHarvestRun.Rows) continue;
                    rocks[row, column].localPosition = new Vector3(column - 2, 1.8f - row * 1.25f + run.Progress * 1.25f, 0);
                    int value = run.Cell(depth, column);
                    renderers[row, column].sharedMaterial = value < 0 ? unstable : value == 3 ? rich : ore;
                    rocks[row, column].localScale = value == 3 ? new Vector3(0.75f, 1.15f, 0.75f)
                        : value < 0 ? new Vector3(1.1f, 0.6f, 1f) : Vector3.one;
                }
            readout.text = UiText.Get("harvest.vein.status", run.Depth, VeinHarvestRun.Rows, run.Ore, run.Integrity);
            SetToolState(UiText.Get("harvest.vein.chain", run.RichChain));
            if (run.Completed)
                Finish(new MinigameResult { quality = run.Quality, perfects = run.RichSeams, total = VeinHarvestRun.Rows });
        }
    }
}
