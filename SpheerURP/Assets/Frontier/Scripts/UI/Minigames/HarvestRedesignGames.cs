using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Frontier
{
    public static class HarvestGameFactory
    {
        public static Minigame Create(MinigameKind kind)
        {
            switch (kind)
            {
                case MinigameKind.Chop: return new DirectionalLumberGame();
                case MinigameKind.Mine: return new ConnectedMiningGame();
                case MinigameKind.Drill: return new LayerDrillGame();
                case MinigameKind.Siphon: return new PipeSiphonGame();
                case MinigameKind.Excavate: return new BuriedExcavationGame();
                case MinigameKind.Salvage: return new SortingSalvageGame();
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }
    }

    /// <summary>Only the run shell is shared; each harvest owns its board, controls and scoring rules.</summary>
    public abstract class HarvestBoardGame : Minigame
    {
        protected Label readout, feedback;
        protected Transform artifact, equipment;
        protected readonly int seed = UnityEngine.Random.Range(1, int.MaxValue);
        Button startButton, retryButton;
        int swipePointer = -1;
        Vector2 swipeStart;
        readonly System.Collections.Generic.HashSet<KeyCode> pressed = new System.Collections.Generic.HashSet<KeyCode>();
        bool started;
        float feedbackPulse;
        float unitsPerDp = 2f;
        int boardSide;
        VisualElement boardElement;
        Label boardStatus;
        protected abstract string Prefix { get; }
        protected abstract void BuildControls();
        protected abstract void NewRun();
        protected abstract void RefreshBoard();
        protected abstract void AdvanceRun(float seconds);
        protected bool Playing { get { return CanInteract && started; } }
        protected override string Title { get { return Text("title"); } }
        protected override string Hint { get { return Text("hint"); } }
        protected override string ResultMetric { get { return Text("metric"); } }
        protected string Text(string key, params object[] args) { return UiText.Get("harvest.redesign." + Prefix + "." + key, args); }
        public bool DebugReady { get { return CanInteract; } }
        public Transform DebugEquipment { get { return equipment; } }
        public bool DebugStarted { get { return started; } }
        public void DebugStart() { StartRun(); }
        public void DebugRetry() { Retry(); }
        public void DebugAdvance(float seconds) { if (Playing) Tick(seconds); }

        protected override void Build()
        {
            UpdateDensity();
            overlay.name = "harvest-" + Prefix;
            readout = UiKit.Text("", "fr-small", Color.white);
            readout.name = "harvest-readout";
            overlay.Insert(overlay.IndexOf(arena), readout);
            feedback = UiKit.Text(UiText.Get("harvest.ready"), "fr-small", UiKit.Cyan);
            feedback.name = "harvest-feedback";
            overlay.Insert(overlay.IndexOf(controls), feedback);
            controls.style.flexWrap = Wrap.Wrap;
            controls.style.width = Length.Percent(100);
            BuildControls();
            var actions = UiKit.Div("fr-row");
            actions.style.width = Length.Percent(100);
            startButton = UiKit.Btn(UiText.Get("harvest.redesign.start"), color, StartRun, true, true);
            startButton.name = "harvest-start";
            startButton.AddToClassList("fr-harvest-action-control");
            startButton.style.flexGrow = 1;
            retryButton = UiKit.Btn(UiText.Get("harvest.redesign.retry"), UiKit.Muted, Retry, true);
            retryButton.name = "harvest-retry";
            retryButton.AddToClassList("fr-harvest-action-control");
            actions.Add(startButton); actions.Add(retryButton);
            overlay.Add(actions);
            overlay.RegisterCallback<KeyDownEvent>(e =>
            {
                if (!Playing || !pressed.Add(e.keyCode)) return;
                if (KeyAction(e.keyCode)) e.StopPropagation();
            }, TrickleDown.TrickleDown);
            overlay.RegisterCallback<KeyUpEvent>(e => pressed.Remove(e.keyCode), TrickleDown.TrickleDown);
            overlay.Focus();
            NewRun(); RefreshBoard();
            ApplyLayout();
            overlay.RegisterCallback<GeometryChangedEvent>(e => ApplyLayout());
        }

        float Dp(float value) { return value * unitsPerDp; }

        void UpdateDensity()
        {
            if (overlay.panel == null) return;
            Vector2 origin = RuntimePanelUtils.ScreenToPanel(overlay.panel, Vector2.zero);
            Vector2 offset = RuntimePanelUtils.ScreenToPanel(overlay.panel, new Vector2(100f, 0f));
            unitsPerDp = Mathf.Max(0.1f, Mathf.Abs(offset.x - origin.x) * ScreenMetrics.Density / 100f);
        }

        void ApplyLayout()
        {
            UpdateDensity();
            titleLabel.style.fontSize = Dp(24);
            var pause = overlay.Q<Button>("minigame-pause");
            pause.style.width = Dp(84);
            pause.style.minHeight = pause.style.height = Dp(52);
            pause.style.fontSize = Dp(16);
            pause.style.paddingLeft = pause.style.paddingRight = Dp(6);
            pause.style.whiteSpace = WhiteSpace.NoWrap;
            var instructions = overlay.Q<Label>(className: "fr-muted");
            instructions.name = "harvest-instructions";
            instructions.style.fontSize = Dp(13);
            foreach (var label in overlay.Query<Label>(className: "fr-minigame-tool-name").ToList())
                label.style.fontSize = Dp(13);
            overlay.Q<Label>("minigame-tool-state").style.fontSize = Dp(13);
            readout.style.fontSize = Dp(14);
            feedback.style.fontSize = Dp(13);
            feedback.style.marginTop = feedback.style.marginBottom = 0;
            arena.style.marginTop = Dp(6);
            arena.style.marginBottom = Dp(6);
            foreach (var button in overlay.Query<Button>(className: "fr-harvest-action-control").ToList())
            {
                button.style.fontSize = Dp(16);
                button.style.minHeight = button.style.height = Dp(52);
                button.style.minWidth = Dp(50);
                button.style.paddingLeft = button.style.paddingRight = Dp(6);
                button.style.marginTop = Dp(3);
                button.style.whiteSpace = WhiteSpace.NoWrap;
                if (button.ClassListContains("fr-harvest-fixed-arrow"))
                    button.style.flexBasis = button.style.width = Dp(52);
            }
            foreach (var icon in overlay.Query<VisualElement>(className: "fr-harvest-direction-icon").ToList())
                icon.style.width = icon.style.height = Dp(24);
            foreach (var meter in controls.Query<VisualElement>(className: "fr-harvest-track").ToList())
            {
                meter.style.height = meter.style.minHeight = Dp(8);
                meter.style.marginBottom = Dp(3);
            }
            if (boardElement == null) return;
            // Reserve the grid's actual touch footprint instead of squeezing it into a percentage of the arena.
            boardElement.parent.style.top = boardElement.parent.style.bottom = Dp(8);
            boardStatus.style.fontSize = Dp(14);
            boardStatus.style.minHeight = Dp(24);
            float statusHeight = float.IsNaN(boardStatus.layout.height) ? 0f : boardStatus.layout.height;
            arena.style.minHeight = Dp(16 + 8 + boardSide * 53) + Mathf.Max(Dp(24), statusHeight) + 8f;
            boardElement.style.marginTop = Dp(8);
            boardElement.style.minHeight = Dp(boardSide * 53);
            foreach (var row in boardElement.Children()) row.style.minHeight = Dp(53);
            foreach (var cell in boardElement.Query<Button>().ToList())
            {
                cell.style.minWidth = cell.style.minHeight = Dp(50);
                cell.style.fontSize = Dp(14);
                cell.style.marginLeft = cell.style.marginRight = Dp(1.5f);
                cell.style.marginTop = cell.style.marginBottom = Dp(1.5f);
                cell.style.whiteSpace = WhiteSpace.NoWrap;
            }
        }

        void StartRun()
        {
            if (!CanInteract || started) return;
            started = true;
            startButton.style.display = DisplayStyle.None;
            feedback.text = UiText.Get("harvest.ready");
            overlay.Focus();
        }

        void Retry()
        {
            if (!CanInteract || finished) return;
            ResetInput();
            NewRun(); RefreshBoard();
            started = false;
            startButton.style.display = DisplayStyle.Flex;
            feedback.text = UiText.Get("harvest.redesign.retried");
            SetToolState(UiText.Get("harvest.ready"));
            overlay.Focus();
        }

        protected virtual bool KeyAction(KeyCode key) { return false; }

        protected Button ActionButton(string key, string name, Action action, Color? accent = null)
        {
            var button = UiKit.Btn(Text(key), accent ?? color, () => { if (Playing) action(); }, true);
            button.name = name;
            button.AddToClassList("fr-harvest-action-control");
            button.style.flexGrow = 1;
            button.style.flexBasis = 0;
            button.style.minWidth = 0;
            controls.Add(button);
            return button;
        }

        protected void DirectionIcon(Button button, CutDirection direction, bool fixedWidth = false)
        {
            button.tooltip = button.text;
            button.text = "";
            var icon = Icons.Make("back", color, Dp(24));
            icon.name = button.name + "-direction";
            icon.AddToClassList("fr-harvest-direction-icon");
            icon.style.marginRight = 0;
            icon.style.rotate = new Rotate(Angle.Degrees((int)direction * 90f));
            button.Add(icon);
            if (fixedWidth)
            {
                button.AddToClassList("fr-harvest-fixed-arrow");
                button.style.flexGrow = button.style.flexShrink = 0;
            }
        }

        protected Button[] Board(int side, Action<int> select)
        {
            boardStatus = arena.Q<Label>();
            boardStatus.AddToClassList("fr-harvest-board-status");
            boardStatus.style.position = Position.Relative;
            boardStatus.style.left = boardStatus.style.right = boardStatus.style.top = StyleKeyword.Auto;
            boardStatus.style.width = Length.Percent(100);
            boardStatus.style.flexShrink = 0;
            boardStatus.RegisterCallback<GeometryChangedEvent>(e => ApplyLayout());
            var field = UiKit.Div();
            field.name = "harvest-field";
            field.style.position = Position.Absolute;
            field.style.left = field.style.right = 12;
            field.style.top = field.style.bottom = Dp(8);
            field.style.flexDirection = FlexDirection.Column;
            field.Add(boardStatus);
            arena.Add(field);
            var board = UiKit.Div();
            boardElement = board; boardSide = side;
            board.name = "harvest-board";
            board.style.flexGrow = 1;
            board.style.flexBasis = 0;
            board.style.flexDirection = FlexDirection.Column;
            field.Add(board);
            var cells = new Button[side * side];
            for (int row = 0; row < side; row++)
            {
                var strip = UiKit.Div("fr-row");
                strip.style.flexGrow = 1; strip.style.flexBasis = 0;
                strip.style.alignItems = Align.Stretch;
                board.Add(strip);
                for (int col = 0; col < side; col++)
                {
                    int index = row * side + col;
                    var cell = UiKit.Btn("", color, () => { if (Playing) select(index); }, true);
                    cell.name = Prefix + "-cell-" + index;
                    cell.style.flexGrow = 1; cell.style.flexBasis = 0;
                    cell.style.minWidth = 0; cell.style.minHeight = 0;
                    cell.style.marginLeft = cell.style.marginRight = 3;
                    cell.style.marginTop = cell.style.marginBottom = 3;
                    cell.style.paddingLeft = cell.style.paddingRight = 0;
                    cell.style.paddingTop = cell.style.paddingBottom = 0;
                    cell.style.fontSize = Dp(14);
                    cell.style.whiteSpace = WhiteSpace.Normal;
                    strip.Add(cell); cells[index] = cell;
                }
            }
            return cells;
        }

        protected Label ArenaLabel(string name, float top, int size = 38)
        {
            var label = UiKit.Text("", "fr-h2", Color.white);
            label.name = name; label.pickingMode = PickingMode.Ignore;
            label.style.position = Position.Absolute;
            label.style.left = 12; label.style.right = 12;
            label.style.top = Length.Percent(top);
            label.style.fontSize = size;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            arena.Add(label);
            return label;
        }

        protected void BindSwipe(Action<Vector2> move, Action<Vector2> release)
        {
            arena.RegisterCallback<PointerDownEvent>(e =>
            {
                if (!Playing || swipePointer >= 0 || e.button != 0) return;
                swipePointer = e.pointerId; swipeStart = e.localPosition;
                arena.CapturePointer(e.pointerId); move?.Invoke(e.localPosition);
                e.StopPropagation();
            });
            arena.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (Playing && e.pointerId == swipePointer) move?.Invoke(e.localPosition);
            });
            arena.RegisterCallback<PointerUpEvent>(e =>
            {
                if (e.pointerId != swipePointer) return;
                Vector2 delta = ((Vector2)e.localPosition - swipeStart) / Mathf.Max(1f, Mathf.Min(ArenaW, ArenaH));
                ReleaseSwipe();
                if (Playing) release?.Invoke(delta);
                e.StopPropagation();
            });
            arena.RegisterCallback<PointerCancelEvent>(e => { if (e.pointerId == swipePointer) ReleaseSwipe(); });
            arena.RegisterCallback<PointerCaptureOutEvent>(e => { if (e.pointerId == swipePointer) swipePointer = -1; });
        }

        void ReleaseSwipe()
        {
            int old = swipePointer; swipePointer = -1;
            if (old >= 0 && arena.HasPointerCapture(old)) arena.ReleasePointer(old);
        }

        protected override void ResetInput()
        {
            // Keep pressed keys blocked until their physical key-up, including after a retry.
            ReleaseSwipe();
        }

        protected override void BuildScene()
        {
            var mount = new Vector3(0, 2.7f, 0);
            if (FeedbackKind == MinigameKind.Chop || FeedbackKind == MinigameKind.Mine)
            {
                equipment = new ToolRig(stage, FeedbackKind, toolLevel, rawColor, mount).tr;
                equipment.name = Prefix + "StrikingRig";
            }
            else equipment = HarvestToolVisual.Build(stage, FeedbackKind, toolLevel, rawColor, mount);
            equipment.localScale = Vector3.one * 0.7f;
            BuildArtifact();
        }
        protected virtual void BuildArtifact() { }
        protected override void Frame(float aspect) { FramePose(6.5f, 9f, 0f, aspect, 0.3f); }
        protected override void Tick(float dt)
        {
            if (started) AdvanceRun(dt);
            feedbackPulse = Mathf.Max(0f, feedbackPulse - dt * 3f);
            if (equipment != null) equipment.localRotation = Quaternion.Euler(0f, 0f, -feedbackPulse * 24f);
            RefreshBoard();
        }
        protected void Respond(string text, bool good, bool perfect = false)
        {
            feedback.text = text; feedback.style.color = good ? UiKit.Cyan : UiKit.Amber;
            feedbackPulse = 1f;
            SetToolState(UiText.Get("harvest.ready"), good);
            HarvestFeedback.Play(perfect ? HarvestCue.Perfect : good ? HarvestCue.Impact : HarvestCue.Glance, FeedbackKind);
            stage.Shake(good ? 0.06f : 0.14f);
        }
        protected void Complete(float quality, int perfects, int total)
        {
            Finish(new MinigameResult { quality = Mathf.Clamp01(quality), perfects = perfects, total = total });
        }
        protected static void Paint(Button cell, Color tint, bool selected)
        {
            cell.style.backgroundColor = new Color(tint.r * 0.22f + 0.02f, tint.g * 0.22f + 0.025f, tint.b * 0.22f + 0.04f, 0.97f);
            cell.style.color = Color.white;
            UiKit.Border(cell, selected ? Color.white : tint, selected ? 3f : 1f);
        }
        protected void FillBar(VisualElement fill, float amount, Color tint)
        {
            fill.style.width = Length.Percent(Mathf.Clamp01(amount) * 100f);
            fill.style.backgroundColor = tint;
        }
        protected VisualElement Meter(string name)
        {
            var track = UiKit.Div("fr-harvest-track");
            track.name = name; track.style.width = Length.Percent(100);
            var fill = UiKit.Div(); fill.style.height = Length.Percent(100);
            track.Add(fill); controls.Add(track);
            return fill;
        }
    }

    public sealed class DirectionalLumberGame : HarvestBoardGame
    {
        DirectionalCutRun run;
        Label arrow;
        protected override string Prefix { get { return "lumber"; } }
        protected override MinigameKind FeedbackKind { get { return MinigameKind.Chop; } }
        protected override string DefaultToolName { get { return UiText.Get("harvest.tool.axe"); } }
        public DirectionalCutRun DebugRun { get { return run; } }
        public void DebugCut(CutDirection direction) { if (Playing) DirectionCut(direction); }
        protected override void BuildControls()
        {
            arrow = ArenaLabel("lumber-direction", 43, 68);
            BindSwipe(null, delta => Cut(delta));
            for (int i = 0; i < 4; i++)
            {
                var direction = (CutDirection)i;
                var button = ActionButton("direction." + direction.ToString().ToLowerInvariant(), "lumber-" + direction.ToString().ToLowerInvariant(),
                    () => DirectionCut(direction));
                DirectionIcon(button, direction);
            }
        }
        protected override void NewRun() { run = new DirectionalCutRun(seed); }
        protected override void BuildArtifact()
        {
            var wood = stage.Lit(new Color(0.28f, 0.12f, 0.055f), new Color(0.5f, 0.22f, 0.06f), 0f, 4f);
            artifact = stage.AddMesh(stage.Own(MeshKit.Cyl(0.85f, 0.7f, 5.6f, 9)), wood, stage.root,
                new Vector3(0, -3f, 0), false).transform;
            equipment.localPosition = new Vector3(1.8f, 0.2f, -0.8f);
        }
        protected override bool KeyAction(KeyCode key)
        {
            if (key == KeyCode.LeftArrow) DirectionCut(CutDirection.Left);
            else if (key == KeyCode.RightArrow) DirectionCut(CutDirection.Right);
            else if (key == KeyCode.UpArrow) DirectionCut(CutDirection.Up);
            else if (key == KeyCode.DownArrow) DirectionCut(CutDirection.Down);
            else return false;
            return true;
        }
        void DirectionCut(CutDirection direction)
        {
            Cut(direction == CutDirection.Left ? Vector2.left : direction == CutDirection.Right ? Vector2.right
                : direction == CutDirection.Up ? Vector2.down : Vector2.up);
        }
        void Cut(Vector2 delta)
        {
            float precision; int old = run.Cuts;
            if (!run.Cut(delta.x, delta.y, out precision)) return;
            Respond(Text(run.Cuts > old ? precision >= 0.85f ? "clean" : "cut" : "wrong"), run.Cuts > old, precision >= 0.85f);
            RefreshBoard();
            if (run.Completed) Complete(run.Quality, run.Perfects, DirectionalCutRun.CutCount);
        }
        protected override void RefreshBoard()
        {
            if (run == null) return;
            arrow.text = Text("direction." + run.Direction.ToString().ToLowerInvariant());
            readout.text = Text("status", run.Cuts, DirectionalCutRun.CutCount, DirectionalCutRun.MaxAttempts - run.Attempts,
                Mathf.CeilToInt(45f - run.Elapsed));
            if (artifact != null) artifact.localScale = new Vector3(1f, 1f - run.Cuts * 0.055f, 1f);
        }
        protected override void AdvanceRun(float seconds)
        {
            run.Advance(seconds);
            if (run.Completed) Complete(run.Quality, run.Perfects, DirectionalCutRun.CutCount);
        }
    }

    public sealed class ConnectedMiningGame : HarvestBoardGame
    {
        ConnectedOreRun run;
        Button[] cells;
        Label selection;
        int aimed;
        protected override string Prefix { get { return "mining"; } }
        protected override MinigameKind FeedbackKind { get { return MinigameKind.Mine; } }
        protected override string DefaultToolName { get { return UiText.Get("tools.mining"); } }
        public ConnectedOreRun DebugRun { get { return run; } }
        public void DebugAim(int cell) { if (Playing) Aim(cell); }
        public void DebugBreak() { if (Playing) Strike(); }
        protected override void BuildControls()
        {
            selection = ArenaLabel("mining-aim", 5, 28);
            cells = Board(ConnectedOreRun.Side, Aim);
            ActionButton("break", "mining-break", Strike);
        }
        protected override void NewRun() { run = new ConnectedOreRun(seed, toolDamage); aimed = 0; }
        void Aim(int cell) { aimed = cell; RefreshBoard(); }
        void Strike()
        {
            if (!run.Break(aimed)) return;
            Respond(Text(run.LastChain > 0 ? "chain" : run.Cell(aimed) == -2 ? "waste" : "crack", run.LastChain),
                run.LastChain > 0, run.LastChain >= 3);
            RefreshBoard();
            if (run.Completed) Complete(run.Quality, run.Chains, ConnectedOreRun.MaxStrikes);
        }
        protected override bool KeyAction(KeyCode key)
        {
            if (key == KeyCode.Space || key == KeyCode.Return) Strike();
            else if (key == KeyCode.LeftArrow) Aim(Mathf.Max(aimed - 1, aimed / 5 * 5));
            else if (key == KeyCode.RightArrow) Aim(Mathf.Min(aimed + 1, aimed / 5 * 5 + 4));
            else if (key == KeyCode.UpArrow) Aim(Mathf.Max(0, aimed - 5));
            else if (key == KeyCode.DownArrow) Aim(Mathf.Min(24, aimed + 5));
            else return false;
            return true;
        }
        protected override void RefreshBoard()
        {
            if (run == null) return;
            int[] chain = run.Connected(aimed);
            for (int i = 0; i < cells.Length; i++)
            {
                int value = run.Cell(i);
                cells[i].text = value == -2 ? "" : Text(value == 2 ? "gem" : value == 1 ? "ore" : value == 0 ? "stone" : "fault");
                Paint(cells[i], value == 2 ? UiKit.Amber : value == 1 ? UiKit.Cyan : value == -1 ? UiKit.Red : UiKit.Muted,
                    Array.IndexOf(chain, i) >= 0 && value > 0);
                if (i == aimed) UiKit.Border(cells[i], Color.white, 4);
            }
            selection.text = Text("aim", chain.Length, run.Hardness(aimed).ToString("0.0"));
            readout.text = Text("status", run.Ore, ConnectedOreRun.MaxStrikes - run.Strikes, Mathf.CeilToInt(50f - run.Elapsed));
        }
        protected override void AdvanceRun(float seconds)
        {
            run.Advance(seconds);
            if (run.Completed) Complete(run.Quality, run.Chains, ConnectedOreRun.MaxStrikes);
        }
    }

    public sealed class LayerDrillGame : HarvestBoardGame
    {
        SteeredDrillRun run;
        Button[] cells;
        HarvestHoldInput left, right, thrust, boost;
        VisualElement heatFill;
        Label position;
        protected override string Prefix { get { return "drill"; } }
        protected override MinigameKind FeedbackKind { get { return MinigameKind.Drill; } }
        public SteeredDrillRun DebugRun { get { return run; } }
        public bool DebugThrustHeld { get { return thrust != null && thrust.Held; } }
        public bool DebugBoostHeld { get { return boost != null && boost.Held; } }
        public void DebugAim(int column) { if (Playing) { run.Aim(column); RefreshBoard(); } }
        protected override void BuildControls()
        {
            position = ArenaLabel("drill-position", 5, 26);
            cells = Board(5, cell => { run.Aim(cell % 5); RefreshBoard(); });
            heatFill = Meter("drill-heat");
            left = Hold("left", "drill-left", KeyCode.LeftArrow);
            thrust = Hold("thrust", "drill-thrust", KeyCode.Space);
            boost = Hold("boost", "drill-boost", KeyCode.LeftShift);
            right = Hold("right", "drill-right", KeyCode.RightArrow);
        }
        HarvestHoldInput Hold(string key, string name, KeyCode keyboard)
        {
            var input = new HarvestHoldInput(overlay, keyboard, () => Playing);
            var button = ActionButton(key, name, () => { });
            if (keyboard == KeyCode.LeftArrow || keyboard == KeyCode.RightArrow)
                DirectionIcon(button, keyboard == KeyCode.LeftArrow ? CutDirection.Left : CutDirection.Right, true);
            input.Bind(button);
            return input;
        }
        protected override void NewRun() { run = new SteeredDrillRun(seed, toolLevel, difficulty); }
        protected override void ResetInput()
        {
            base.ResetInput(); left?.Reset(); right?.Reset(); thrust?.Reset(); boost?.Reset(); run?.CancelInput();
        }
        protected override void RefreshBoard()
        {
            if (run == null) return;
            for (int row = 0; row < 5; row++)
                for (int col = 0; col < 5; col++)
                {
                    int depth = run.Depth + row;
                    int value = depth < SteeredDrillRun.Layers ? run.Cell(depth, col) : 0;
                    var cell = cells[row * 5 + col];
                    cell.text = depth >= SteeredDrillRun.Layers ? "" : Text(value == 3 ? "rich" : value < 0 ? "rock" : "layer");
                    Paint(cell, value == 3 ? UiKit.Amber : value < 0 ? UiKit.Red : UiKit.Cyan, col == run.Column && row == 0);
                }
            position.text = Text("position", run.Column + 1, Mathf.RoundToInt(run.Progress * 100));
            if (equipment != null)
            {
                equipment.localPosition = new Vector3((run.Column - 2) * 0.65f, 2.7f - run.Progress * 0.15f, 0f);
                equipment.Find("Spin").localRotation = Quaternion.Euler(0f, run.Elapsed * 800f, 0f);
            }
            FillBar(heatFill, run.Heat, run.Overheated ? UiKit.Red : run.Heat > 0.75f ? UiKit.Amber : color);
            readout.text = Text("status", run.Depth, SteeredDrillRun.Layers, Mathf.RoundToInt(run.Heat * 100),
                Mathf.CeilToInt(65f - run.Elapsed));
            if (run.Overheated) SetToolState(UiText.Get("harvest.drill.overheated"), false);
        }
        protected override void AdvanceRun(float seconds)
        {
            int depth = run.Depth, collisions = run.Collisions;
            bool hot = run.Overheated;
            run.Advance(seconds, (right.Held ? 1 : 0) - (left.Held ? 1 : 0), thrust.Held, boost.Held);
            if (run.Depth > depth) Respond(Text(run.Collisions > collisions ? "collision" : run.RichLayers > 0 && run.Cell(depth, run.Column) == 3
                ? "rich-hit" : "through"), run.Collisions == collisions, run.Cell(depth, run.Column) == 3);
            if (!hot && run.Overheated) Respond(Text("hot"), false);
            if (hot && !run.Overheated) Respond(Text("cooled"), true);
            if (run.Completed) Complete(run.Quality, run.RichLayers, SteeredDrillRun.Layers);
        }
    }

    public sealed class PipeSiphonGame : HarvestBoardGame
    {
        FlowBalanceRun run;
        Button[] cells;
        VisualElement[] pipeLines;
        VisualElement pressure;
        int selected;
        protected override string Prefix { get { return "siphon"; } }
        protected override MinigameKind FeedbackKind { get { return MinigameKind.Siphon; } }
        public FlowBalanceRun DebugRun { get { return run; } }
        public void DebugRotate(int cell) { if (Playing) Rotate(cell); }
        public void DebugValve(float amount) { if (Playing) run.SetValve(amount); }
        public void DebugConnect()
        {
            if (!Playing) return;
            int[] route = { 6, 10, 12, 9, 0, 3, 3, 10, 9 };
            for (int cell = 0; cell < route.Length; cell++)
                for (int turn = 0; turn < 4 && run.Pipe(cell) != route[cell]; turn++) Rotate(cell);
        }
        protected override void BuildControls()
        {
            ArenaLabel("siphon-route", 5, 24).text = Text("route");
            cells = Board(3, cell => { selected = cell; Rotate(cell); });
            pipeLines = new VisualElement[36];
            for (int cell = 0; cell < 9; cell++)
                for (int direction = 0; direction < 4; direction++)
                {
                    var line = UiKit.Div();
                    line.pickingMode = PickingMode.Ignore;
                    line.style.position = Position.Absolute;
                    bool vertical = direction == 0 || direction == 2;
                    line.style.width = Length.Percent(vertical ? 9 : 50);
                    line.style.height = Length.Percent(vertical ? 50 : 9);
                    line.style.left = Length.Percent(vertical ? 45.5f : direction == 1 ? 50 : 0);
                    line.style.top = Length.Percent(vertical ? direction == 0 ? 0 : 50 : 45.5f);
                    line.style.backgroundColor = UiKit.Cyan;
                    cells[cell].Add(line); pipeLines[cell * 4 + direction] = line;
                }
            pressure = Meter("siphon-pressure");
            ActionButton("close", "siphon-close", () => run.SetValve(run.Valve - 0.1f));
            ActionButton("open", "siphon-open", () => run.SetValve(run.Valve + 0.1f));
        }
        protected override void NewRun() { run = new FlowBalanceRun(seed); selected = 3; }
        void Rotate(int cell)
        {
            if (!run.Rotate(cell)) return;
            Respond(Text(run.Connected && run.Leaks == 0 ? "connected" : "leaks", run.Leaks), run.Connected && run.Leaks == 0);
            RefreshBoard();
        }
        protected override bool KeyAction(KeyCode key)
        {
            if (key == KeyCode.Space || key == KeyCode.Return) Rotate(selected);
            else if (key == KeyCode.LeftArrow) selected = Math.Max(selected / 3 * 3, selected - 1);
            else if (key == KeyCode.RightArrow) selected = Math.Min(selected / 3 * 3 + 2, selected + 1);
            else if (key == KeyCode.UpArrow) selected = Math.Max(0, selected - 3);
            else if (key == KeyCode.DownArrow) selected = Math.Min(8, selected + 3);
            else if (key == KeyCode.Minus) run.SetValve(run.Valve - 0.1f);
            else if (key == KeyCode.Equals) run.SetValve(run.Valve + 0.1f);
            else return false;
            return true;
        }
        protected override void RefreshBoard()
        {
            if (run == null) return;
            for (int cell = 0; cell < 9; cell++)
            {
                cells[cell].text = cell == 4 ? Text("blocked") : "";
                Paint(cells[cell], cell == 4 ? UiKit.Muted : run.Connected && run.Leaks == 0 ? UiKit.Cyan : UiKit.Amber, cell == selected);
                for (int direction = 0; direction < 4; direction++)
                    pipeLines[cell * 4 + direction].style.display = (run.Pipe(cell) & (1 << direction)) != 0
                        ? DisplayStyle.Flex : DisplayStyle.None;
            }
            FillBar(pressure, run.Pressure, Mathf.Abs(run.Pressure - run.Demand) <= 0.12f ? UiKit.Cyan : UiKit.Amber);
            readout.text = Text("status", run.Delivered.ToString("0.0"), Mathf.RoundToInt(run.Valve * 100),
                Mathf.RoundToInt(run.Demand * 100), Mathf.RoundToInt(run.Pressure * 100));
        }
        protected override void AdvanceRun(float seconds)
        {
            run.Advance(seconds);
            if (run.Completed) Complete(run.Quality, Mathf.FloorToInt(run.BalancedSeconds), 10);
        }
    }

    public sealed class BuriedExcavationGame : HarvestBoardGame
    {
        BuriedFindRun run;
        Button[] cells;
        Label selection;
        int selected;
        protected override string Prefix { get { return "excavation"; } }
        protected override MinigameKind FeedbackKind { get { return MinigameKind.Excavate; } }
        public BuriedFindRun DebugRun { get { return run; } }
        public void DebugSelect(int cell) { if (Playing) { selected = cell; RefreshBoard(); } }
        public void DebugClear(bool dig) { if (Playing) Clear(dig); }
        public void DebugScan() { if (Playing) Scan(); }
        protected override void BuildControls()
        {
            selection = ArenaLabel("excavation-soil", 5, 26);
            cells = Board(5, cell => { selected = cell; RefreshBoard(); });
            ActionButton("scan", "excavation-scan", Scan);
            ActionButton("dig", "excavation-dig", () => Clear(true));
            ActionButton("brush", "excavation-brush", () => Clear(false));
        }
        protected override void NewRun() { run = new BuriedFindRun(seed); selected = 0; }
        void Scan()
        {
            int distance = run.Scan(selected);
            if (distance >= 0) Respond(Text("signal", distance), true);
            else Respond(Text("no-scans"), false);
        }
        void Clear(bool dig)
        {
            int found = run.Found, damaged = run.Damaged;
            if (!run.Clear(selected, dig)) return;
            Respond(Text(run.Damaged > damaged ? "damaged" : run.Found > found ? "found" : "cleared"),
                run.Damaged == damaged, run.Found > found);
            RefreshBoard();
            if (run.Completed) Complete(run.Quality, run.Found, BuriedFindRun.FindCount);
        }
        protected override bool KeyAction(KeyCode key)
        {
            if (key == KeyCode.Alpha1) Scan();
            else if (key == KeyCode.Alpha2) Clear(true);
            else if (key == KeyCode.Alpha3 || key == KeyCode.Space) Clear(false);
            else if (key == KeyCode.LeftArrow) selected = Math.Max(selected / 5 * 5, selected - 1);
            else if (key == KeyCode.RightArrow) selected = Math.Min(selected / 5 * 5 + 4, selected + 1);
            else if (key == KeyCode.UpArrow) selected = Math.Max(0, selected - 5);
            else if (key == KeyCode.DownArrow) selected = Math.Min(24, selected + 5);
            else return false;
            return true;
        }
        protected override void RefreshBoard()
        {
            if (run == null) return;
            for (int i = 0; i < cells.Length; i++)
            {
                int soil = run.Soil(i);
                cells[i].text = run.Exposed(i) ? Text("find") : soil == 0 ? Text("empty") : soil.ToString();
                Paint(cells[i], run.Exposed(i) ? UiKit.Amber : new Color(0.72f, 0.42f, 0.22f), selected == i);
            }
            selection.text = Text("soil", run.Soil(selected));
            readout.text = Text("status", run.Found, BuriedFindRun.FindCount, BuriedFindRun.MaxActions - run.Actions, 3 - run.Scans);
        }
        protected override void AdvanceRun(float seconds)
        {
            run.Advance(seconds);
            if (run.Completed) Complete(run.Quality, run.Found, BuriedFindRun.FindCount);
        }
    }

    public sealed class SortingSalvageGame : HarvestBoardGame
    {
        SortingSalvageRun run;
        Label part, next;
        VisualElement deadline;
        Renderer partRenderer;
        MeshFilter partFilter;
        Mesh metalMesh, circuitMesh, wasteMesh;
        Material metalMaterial, circuitMaterial, wasteMaterial, hazardMaterial;
        protected override string Prefix { get { return "salvage"; } }
        protected override MinigameKind FeedbackKind { get { return MinigameKind.Salvage; } }
        public SortingSalvageRun DebugRun { get { return run; } }
        public void DebugSort(SalvageBin bin) { if (Playing) Sort(bin); }
        protected override void BuildControls()
        {
            part = ArenaLabel("salvage-part", 16, 44);
            next = ArenaLabel("salvage-next", 77, 26);
            deadline = Meter("salvage-deadline");
            var metal = ActionButton("metal", "salvage-metal", () => Sort(SalvageBin.Metal));
            var circuit = ActionButton("circuit", "salvage-circuit", () => Sort(SalvageBin.Circuit));
            var reject = ActionButton("reject", "salvage-reject", () => Sort(SalvageBin.Reject), UiKit.Amber);
            metal.style.flexBasis = circuit.style.flexBasis = Length.Percent(48);
            reject.style.flexBasis = Length.Percent(100);
            BindSwipe(null, delta =>
            {
                if (delta.magnitude < 0.15f) return;
                Sort(Mathf.Abs(delta.x) > Mathf.Abs(delta.y) ? delta.x < 0 ? SalvageBin.Metal : SalvageBin.Circuit : SalvageBin.Reject);
            });
        }
        protected override void NewRun() { run = new SortingSalvageRun(seed); }
        protected override void BuildArtifact()
        {
            metalMesh = stage.Own(MeshKit.OreChunk());
            circuitMesh = stage.Own(MeshKit.Cyl(0.65f, 0.65f, 0.18f, 4));
            wasteMesh = stage.Own(MeshKit.Shard(seed));
            metalMaterial = stage.Lit(new Color(0.62f, 0.7f, 0.78f), UiKit.Cyan, 0f, 4f);
            circuitMaterial = stage.Lit(new Color(0.08f, 0.65f, 0.3f), UiKit.Lime, 0f, 4f);
            wasteMaterial = stage.Lit(new Color(0.35f, 0.25f, 0.18f), UiKit.Amber, 0f, 4f);
            hazardMaterial = stage.Lit(new Color(0.7f, 0.12f, 0.05f), UiKit.Red, 0f, 4f);
            var item = stage.AddMesh(metalMesh, metalMaterial, stage.root, new Vector3(0, -0.2f, -0.5f), false);
            artifact = item.transform; artifact.localScale = Vector3.one * 3f;
            partRenderer = item.GetComponent<Renderer>(); partFilter = item.GetComponent<MeshFilter>();
        }
        void Sort(SalvageBin bin)
        {
            bool correct = run.Sort(bin);
            Respond(Text(correct ? "sorted" : "wrong", run.Chain), correct, correct && run.Chain >= 4);
            RefreshBoard();
            if (run.Completed) Complete(run.Quality, run.BestChain, SortingSalvageRun.PartCount);
        }
        protected override bool KeyAction(KeyCode key)
        {
            if (key == KeyCode.Alpha1 || key == KeyCode.LeftArrow) Sort(SalvageBin.Metal);
            else if (key == KeyCode.Alpha2 || key == KeyCode.RightArrow) Sort(SalvageBin.Circuit);
            else if (key == KeyCode.Alpha3 || key == KeyCode.DownArrow) Sort(SalvageBin.Reject);
            else return false;
            return true;
        }
        protected override void RefreshBoard()
        {
            if (run == null) return;
            part.text = Text("part." + run.Current.ToString().ToLowerInvariant());
            next.text = Text("next", run.Sorted + 1 >= SortingSalvageRun.PartCount ? Text("last")
                : Text("part." + run.Next.ToString().ToLowerInvariant()));
            FillBar(deadline, run.Remaining / 3f, run.Remaining < 0.8f ? UiKit.Amber : color);
            readout.text = Text("status", run.Sorted, SortingSalvageRun.PartCount, run.Saved, run.Chain);
            if (artifact != null)
            {
                partFilter.sharedMesh = run.Current == SalvagePart.Metal ? metalMesh : run.Current == SalvagePart.Circuit ? circuitMesh : wasteMesh;
                partRenderer.sharedMaterial = run.Current == SalvagePart.Metal ? metalMaterial : run.Current == SalvagePart.Circuit
                    ? circuitMaterial : run.Current == SalvagePart.Hazard ? hazardMaterial : wasteMaterial;
                artifact.localRotation = Quaternion.Euler(25f, run.Elapsed * 12f, 15f);
            }
        }
        protected override void AdvanceRun(float seconds)
        {
            int misses = run.Mistakes;
            run.Advance(seconds);
            if (run.Mistakes > misses) Respond(Text("missed"), false);
            if (run.Completed) Complete(run.Quality, run.BestChain, SortingSalvageRun.PartCount);
        }
    }
}
