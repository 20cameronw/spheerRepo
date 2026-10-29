using UnityEngine;
using UnityEngine.UIElements;

namespace Frontier
{
    public sealed class VeinGame : Minigame
    {
        VeinHarvestRun run;
        readonly Transform[,] rocks = new Transform[6, VeinHarvestRun.Columns];
        readonly Renderer[,] renderers = new Renderer[6, VeinHarvestRun.Columns];
        readonly MeshFilter[,] filters = new MeshFilter[6, VeinHarvestRun.Columns];
        readonly GameObject[,] crystals = new GameObject[6, VeinHarvestRun.Columns];
        readonly GameObject[,] inclusions = new GameObject[6, VeinHarvestRun.Columns];
        readonly Transform[] chips = new Transform[32];
        readonly Renderer[] chipRenderers = new Renderer[32];
        readonly Vector3[] chipVelocity = new Vector3[32];
        readonly float[] chipLife = new float[32];
        int nextChip;
        float drillClock, drillPhase;
        Material ore, rich, unstable, metal;
        Mesh oreMesh, fracturedMesh, crystalMesh;
        Transform cutter, bit;
        Label readout;
        bool dragging;
        int desiredColumn = VeinHarvestRun.Columns / 2;
        int dragPointer;
        float steerCooldown;
        HarvestHoldInput boost;
        Button boostButton;
        public VeinHarvestRun DebugRun { get { return run; } }
        public bool DebugReady { get { return CanInteract; } }
        public Transform DebugScene { get { return stage != null ? stage.root : null; } }
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
            boost = new HarvestHoldInput(overlay, KeyCode.Space, () => CanInteract);
            boostButton = UiKit.Btn(UiText.Get("harvest.vein.speed"), UiKit.Amber, null, true);
            boostButton.name = "vein-speed";
            boostButton.style.flexGrow = 1; boostButton.style.minWidth = 0;
            boost.Bind(boostButton); controls.Add(boostButton);
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
            boost?.Reset();
            run?.Advance(0f, false);
            ResetDrag();
        }

        void ResetDrag()
        {
            dragging = false;
            if (arena.HasPointerCapture(dragPointer)) arena.ReleasePointer(dragPointer);
        }

        protected override void BuildScene()
        {
            ore = stage.Lit(new Color(0.22f, 0.30f, 0.38f), new Color(0.32f, 0.48f, 0.58f), 0f, 4f, 0f);
            metal = stage.Lit(new Color(0.52f, 0.62f, 0.68f), new Color(0.4f, 0.65f, 0.7f), 0f, 4f);
            rich = stage.Lit(new Color(1f, 0.64f, 0.12f), new Color(1f, 0.8f, 0.24f), 0f, 4f, 0f);
            unstable = stage.Lit(new Color(0.12f, 0.08f, 0.08f), new Color(0.4f, 0.08f, 0.03f), 0.2f, 3f, 0f);
            oreMesh = stage.Own(MeshKit.OreChunk());
            fracturedMesh = stage.Own(MeshKit.Shard(41));
            crystalMesh = stage.Own(MeshKit.CrystalCluster());
            for (int row = 0; row < 6; row++)
                for (int column = 0; column < VeinHarvestRun.Columns; column++)
                {
                    var rock = stage.AddMesh(oreMesh, ore, stage.root, Vector3.zero, false);
                    rock.name = "VeinCell_" + row + "_" + column;
                    rocks[row, column] = rock.transform;
                    renderers[row, column] = rock.GetComponent<Renderer>();
                    filters[row, column] = rock.GetComponent<MeshFilter>();
                    var crystal = stage.AddMesh(crystalMesh, rich, rock.transform, new Vector3(0f, 0.02f, -0.16f), false);
                    crystal.name = "RichCrystalCrown";
                    crystal.transform.localRotation = Quaternion.Euler(-72f, 0f, 12f);
                    crystal.transform.localScale = Vector3.one * 1.1f;
                    crystal.SetActive(false);
                    crystals[row, column] = crystal;
                    var seams = new GameObject("NormalOreInclusions");
                    seams.transform.SetParent(rock.transform, false);
                    for (int i = 0; i < 3; i++)
                    {
                        var nugget = stage.AddMesh(oreMesh, metal, seams.transform,
                            new Vector3(-0.2f + i * 0.17f, (i % 2 == 0 ? -1 : 1) * 0.10f, -0.30f), false);
                        nugget.transform.localScale = new Vector3(0.22f, 0.32f, 0.24f);
                        nugget.transform.localRotation = Quaternion.Euler(0, 0, i * 35f);
                    }
                    inclusions[row, column] = seams;
                }
            cutter = HarvestToolVisual.Build(stage, MinigameKind.Mine, toolLevel, rawColor, Vector3.zero);
            bit = cutter.GetChild(0);
            for (int i = 0; i < chips.Length; i++)
            {
                var chip = stage.AddMesh(fracturedMesh, ore, stage.root, Vector3.zero, false);
                chip.name = "DrillingChip_" + i;
                chips[i] = chip.transform;
                chipRenderers[i] = chip.GetComponent<Renderer>();
                chip.SetActive(false);
            }
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
            run.Advance(dt, boost.Held);
            boostButton.EnableInClassList("fr-btn-primary", run.Boosting);
            drillPhase += dt * (run.Boosting ? 1600f : 800f);
            bit.localRotation = Quaternion.Euler(0f, drillPhase, 0f);
            UpdateChips(dt);
            if (run.Depth != oldDepth)
            {
                bool damaged = run.Integrity < oldIntegrity;
                HarvestFeedback.Play(damaged ? HarvestCue.Glance : run.RichSeams > oldRich ? HarvestCue.Perfect : HarvestCue.Impact, FeedbackKind);
                stage.Shake(damaged ? 0.3f : 0.1f);
                for (int depth = oldDepth; depth < run.Depth; depth++)
                    EmitChips(new Vector3(run.Column - 2, 2.35f, -0.45f),
                        run.Cell(depth, run.Column) < 0 ? unstable : run.Cell(depth, run.Column) == 3 ? rich : metal, 12, 2.4f);
            }
            drillClock += dt * (run.Boosting ? 2f : 1f);
            if (!run.Completed && drillClock >= 0.08f)
            {
                drillClock %= 0.08f;
                EmitChips(new Vector3(run.Column - 2, 2.35f, -0.45f), metal, 2, 1.2f);
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
                    renderers[row, column].sharedMaterial = value < 0 ? unstable : ore;
                    filters[row, column].sharedMesh = value < 0 ? fracturedMesh : oreMesh;
                    crystals[row, column].SetActive(value == 3);
                    inclusions[row, column].SetActive(value == 1);
                    rocks[row, column].localScale = value == 3 ? new Vector3(1f, 1.1f, 1f)
                        : value < 0 ? new Vector3(1.1f, 0.6f, 1f) : Vector3.one;
                }
            readout.text = UiText.Get("harvest.vein.status", run.Depth, VeinHarvestRun.Rows, run.Ore, run.Integrity);
            SetToolState(UiText.Get("harvest.vein.chain", run.RichChain));
            if (run.Completed)
                Finish(new MinigameResult { quality = run.Quality, perfects = run.RichSeams, total = VeinHarvestRun.Rows });
        }

        void EmitChips(Vector3 position, Material material, int count, float speed)
        {
            count = Mathf.Max(1, Mathf.RoundToInt(count * PresentationQuality.Budget.ParticleMultiplier));
            for (int i = 0; i < count; i++)
            {
                int index = nextChip++ % chips.Length;
                chips[index].localPosition = position;
                chips[index].localScale = Vector3.one * Random.Range(0.10f, 0.22f);
                chipVelocity[index] = new Vector3(Random.Range(-1f, 1f), Random.Range(0.3f, 1f), -0.2f) * speed;
                chipLife[index] = 0.48f;
                chipRenderers[index].sharedMaterial = material;
                chips[index].gameObject.SetActive(true);
            }
        }

        void UpdateChips(float dt)
        {
            for (int i = 0; i < chips.Length; i++)
            {
                if (chipLife[i] <= 0f) continue;
                chipLife[i] -= dt;
                if (chipLife[i] <= 0f) { chips[i].gameObject.SetActive(false); continue; }
                chipVelocity[i] += Vector3.down * (5f * dt);
                chips[i].localPosition += chipVelocity[i] * dt;
                chips[i].Rotate(new Vector3(140f, 230f, 80f) * dt, Space.Self);
                if (chipLife[i] < 0.15f) chips[i].localScale *= Mathf.Exp(-dt * 12f);
            }
        }
    }
}
