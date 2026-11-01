using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Frontier.Tests
{
    public class HarvestRedesignFlowTests
    {
        GameObject host;
        PanelSettings panel;
        VisualElement root;
        [SetUp]
        public void SetUp()
        {
            host = new GameObject("Harvest redesign tests");
            panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.scaleMode = PanelScaleMode.ConstantPixelSize;
            panel.themeStyleSheet = Resources.Load<ThemeStyleSheet>("Frontier/FrontierTheme");
            var doc = host.AddComponent<UIDocument>();
            doc.panelSettings = panel;
            root = doc.rootVisualElement;
            UiKit.ApplyStyles(root);
            root.style.width = 1080; root.style.height = 2340;
        }
        [TearDown]
        public void TearDown()
        {
            root.Clear();
            Object.Destroy(host); Object.Destroy(panel);
        }
        static void Key(VisualElement target, KeyCode key, bool down)
        {
            var input = new Event { type = down ? EventType.KeyDown : EventType.KeyUp, keyCode = key };
            if (down)
            {
                using (var e = KeyDownEvent.GetPooled(input)) { e.target = target; target.SendEvent(e); }
            }
            else using (var e = KeyUpEvent.GetPooled(input)) { e.target = target; target.SendEvent(e); }
        }
        void Press(KeyCode key) { Key(root.Q("harvest-start"), key, true); Key(root.Q("harvest-start"), key, false); }
        IEnumerator Ready(HarvestBoardGame game, VisualElement gameHost = null, bool start = true)
        {
            game.Run(gameHost ?? root, UiKit.Cyan, 1f, 0.5f, null);
            for (int i = 0; i < 120 && !game.DebugReady; i++) yield return null;
            Assert.IsTrue(game.DebugReady);
            yield return null;
            Assert.IsNotNull(game.DebugEquipment, "Readiness requires the scheduled scene build to finish.");
            Assert.Greater(game.DebugEquipment.GetComponentsInChildren<Renderer>().Length, 0,
                "Every replacement must have actual tool geometry, not an empty fallback.");
            Assert.IsFalse(game.DebugStarted, "Onboarding must not start a timer without the player.");
            if (start) game.DebugStart();
        }

        static void AssertLayout(VisualElement gameHost, float scale, float density)
        {
            var arena = gameHost.Q(className: "fr-arena");
            var board = gameHost.Q("harvest-board");
            if (board != null)
            {
                var fieldStatus = gameHost.Q<Label>(className: "fr-harvest-board-status");
                float gap = (board.worldBound.yMin - fieldStatus.worldBound.yMax) * scale / density;
                Assert.GreaterOrEqual(gap, 7.5f, fieldStatus.name + " needs an explicit 8dp status/board gap");
                Assert.AreEqual(Position.Relative, fieldStatus.resolvedStyle.position,
                    "Field status must flow above the board, not track a percentage of the arena height.");
            }
            foreach (var button in gameHost.Query<Button>().ToList())
            {
                if (button.resolvedStyle.display == DisplayStyle.None) continue;
                Rect bounds = button.worldBound;
                Assert.GreaterOrEqual(Mathf.Min(bounds.width, bounds.height) * scale / density, 48f,
                    button.name + " must be a 48dp physical touch target.");
                Assert.GreaterOrEqual(bounds.yMin, gameHost.worldBound.yMin - 1f, button.name + " clipped above the host");
                Assert.LessOrEqual(bounds.yMax, gameHost.worldBound.yMax + 1f, button.name + " clipped below the host");
                if (button.name.Contains("-cell-"))
                {
                    Assert.GreaterOrEqual(bounds.yMin, arena.worldBound.yMin);
                    Assert.LessOrEqual(bounds.yMax, arena.worldBound.yMax, button.name + " clipped by the arena");
                }
                if (string.IsNullOrEmpty(button.text)) continue;
                Assert.AreEqual(WhiteSpace.NoWrap, button.resolvedStyle.whiteSpace, button.name + " must not split a word");
                Vector2 text = button.MeasureTextSize(button.text, 0f, VisualElement.MeasureMode.Undefined,
                    0f, VisualElement.MeasureMode.Undefined);
                float available = bounds.width - button.resolvedStyle.paddingLeft - button.resolvedStyle.paddingRight
                    - button.resolvedStyle.borderLeftWidth - button.resolvedStyle.borderRightWidth;
                Assert.LessOrEqual(text.x, available + 1f, button.name + " must fit its complete control label");
            }
            foreach (var text in gameHost.Query<TextElement>().ToList())
                if (!string.IsNullOrWhiteSpace(text.text) && text.resolvedStyle.display != DisplayStyle.None)
                    Assert.GreaterOrEqual(text.resolvedStyle.fontSize * scale / density, 12f, text.text + " is below 12dp");
        }

        [UnityTest]
        public IEnumerator BothCaptureProfiles_KeepWholeControlLabelsAnd48DpCellsWithoutClipping()
        {
            int[] widths = { 1080, 1536 }, heights = { 2340, 2048 };
            float[] densities = { 3f, 2f };
            try
            {
                for (int profile = 0; profile < 2; profile++)
                {
                    float scale = UIRoot.ComputeScale(widths[profile], densities[profile]);
                    ScreenMetrics.SetVirtual(widths[profile], heights[profile], null, densities[profile]);
                    panel.scale = scale;
                    root.style.width = widths[profile] / scale;
                    root.style.height = heights[profile] / scale;
                    for (int kind = 0; kind < 6; kind++)
                    {
                        var gameHost = new VisualElement();
                        gameHost.style.position = Position.Absolute;
                        gameHost.style.left = gameHost.style.right = 0;
                        gameHost.style.top = UIRoot.HudTopInset;
                        gameHost.style.bottom = profile == 0 ? 80f / scale : 0f;
                        root.Add(gameHost);
                        var game = (HarvestBoardGame)HarvestGameFactory.Create((MinigameKind)kind);
                        yield return Ready(game, gameHost, false);
                        yield return null;
                        AssertLayout(gameHost, scale, densities[profile]);
                        game.DebugStart();
                        if (game is DirectionalLumberGame lumber) lumber.DebugCut(lumber.DebugRun.Direction);
                        else if (game is ConnectedMiningGame mine) { mine.DebugBreak(); mine.DebugBreak(); mine.DebugBreak(); }
                        else if (game is PipeSiphonGame siphon) siphon.DebugConnect();
                        else if (game is BuriedExcavationGame excavation) excavation.DebugClear(true);
                        else if (game is SortingSalvageGame salvage) salvage.DebugSort(SalvageBin.Reject);
                        game.DebugAdvance(0.1f);
                        yield return null;
                        AssertLayout(gameHost, scale, densities[profile]);
                        gameHost.RemoveFromHierarchy();
                    }
                }
            }
            finally { ScreenMetrics.ClearVirtual(); }
        }

        [UnityTest]
        public IEnumerator AllSixFactoryGamesHaveDistinctControlsAndFreshRetryWithoutReward()
        {
            string[] controls = { "lumber-left", "mining-break", "drill-thrust", "siphon-open", "excavation-brush", "salvage-reject" };
            for (int kind = 0; kind < 6; kind++)
            {
                var game = (HarvestBoardGame)HarvestGameFactory.Create((MinigameKind)kind);
                yield return Ready(game);
                Assert.IsNotNull(root.Q(controls[kind]));
                game.DebugAdvance(0.5f);
                game.Pause();
                string pausedReadout = root.Q<Label>("harvest-readout").text;
                game.DebugAdvance(60f);
                Assert.AreEqual(pausedReadout, root.Q<Label>("harvest-readout").text);
                game.Resume();
                game.DebugRetry();
                Assert.IsFalse(game.DebugStarted);
                Assert.IsNull(root.Q("minigame-result"), "Retry resets only this attempt; it must not award or spend harvest progress.");
                root.Clear();
            }
        }

        [UnityTest]
        public IEnumerator LumberAndMining_ActualKeyboardActionsChangeTheirModelsAndDoNotAutoRepeat()
        {
            var lumber = new DirectionalLumberGame();
            yield return Ready(lumber);
            var direction = lumber.DebugRun.Direction;
            KeyCode cut = direction == CutDirection.Left ? KeyCode.LeftArrow : direction == CutDirection.Right ? KeyCode.RightArrow
                : direction == CutDirection.Up ? KeyCode.UpArrow : KeyCode.DownArrow;
            var target = root.Q("harvest-start");
            Key(target, cut, true);
            Assert.AreEqual(1, lumber.DebugRun.Cuts);
            Key(target, cut, true);
            Assert.AreEqual(1, lumber.DebugRun.Attempts, "A held key is one cut, not an automatic harvest.");
            lumber.Pause(); lumber.Resume();
            Key(target, cut, true);
            Assert.AreEqual(1, lumber.DebugRun.Attempts);
            Key(target, cut, false);
            Assert.Greater(lumber.DebugRun.Quality, 0f);
            root.Clear();
            var mining = new ConnectedMiningGame { toolDamage = 3f };
            yield return Ready(mining);
            Press(KeyCode.Space);
            Assert.AreEqual(4, mining.DebugRun.Ore);
            Assert.Greater(mining.DebugRun.Quality, 0f);
            mining.DebugRetry();
            Assert.AreEqual(0, mining.DebugRun.Ore);
            Assert.AreEqual(0, mining.DebugRun.Strikes);
        }

        [UnityTest]
        public IEnumerator Drill_ActualSteerThrustAndBoostKeysAffectDepthAndCleanUpOnPauseRetryDetach()
        {
            var game = new LayerDrillGame();
            yield return Ready(game);
            var target = root.Q("drill-thrust");
            Key(target, KeyCode.Space, true);
            Key(target, KeyCode.LeftShift, true);
            game.DebugAdvance(0.5f);
            Assert.Greater(game.DebugRun.Progress, 0f);
            Assert.Greater(game.DebugRun.Heat, 0f);
            Key(target, KeyCode.RightArrow, true);
            game.DebugAdvance(0.4f);
            Assert.Greater(game.DebugRun.Position, 2f);
            game.Pause();
            Assert.IsFalse(game.DebugThrustHeld); Assert.IsFalse(game.DebugBoostHeld);
            float time = game.DebugRun.Elapsed;
            game.DebugAdvance(1f);
            Assert.AreEqual(time, game.DebugRun.Elapsed);
            game.Resume();
            Key(target, KeyCode.Space, true);
            Assert.IsFalse(game.DebugThrustHeld, "Repeating an unreleased key cannot resume a drill.");
            Key(target, KeyCode.Space, false); Key(target, KeyCode.LeftShift, false); Key(target, KeyCode.RightArrow, false);
            game.DebugRetry(); game.DebugStart();
            Key(target, KeyCode.Space, true);
            Assert.IsTrue(game.DebugThrustHeld);
            root.Clear();
            Assert.IsFalse(game.DebugThrustHeld);
            Assert.IsFalse(game.DebugRun.Thrusting);
        }

        [UnityTest]
        public IEnumerator SiphonExcavationAndSalvage_ActualActionsChangeFlowSoilAndRecoveredParts()
        {
            var siphon = new PipeSiphonGame();
            yield return Ready(siphon);
            int valve = Mathf.RoundToInt(siphon.DebugRun.Valve * 100);
            Press(KeyCode.Equals);
            Assert.Greater(siphon.DebugRun.Valve * 100, valve);
            int before = siphon.DebugRun.Pipe(3);
            Press(KeyCode.Space);
            Assert.AreNotEqual(before, siphon.DebugRun.Pipe(3), "Rotate must alter route topology.");
            siphon.DebugConnect();
            for (int i = 0; i < 20; i++)
            {
                siphon.DebugValve(siphon.DebugRun.Demand); siphon.DebugAdvance(0.1f);
            }
            Assert.Greater(siphon.DebugRun.Delivered, 0f);
            root.Clear();
            var excavation = new BuriedExcavationGame();
            yield return Ready(excavation);
            int soil = excavation.DebugRun.Soil(0);
            Press(KeyCode.Alpha2);
            Assert.AreEqual(soil - 2, excavation.DebugRun.Soil(0));
            Press(KeyCode.Alpha1);
            Assert.AreEqual(1, excavation.DebugRun.Scans);
            root.Clear();
            var salvage = new SortingSalvageGame();
            yield return Ready(salvage);
            var current = salvage.DebugRun.Current;
            Press(current == SalvagePart.Metal ? KeyCode.Alpha1 : current == SalvagePart.Circuit ? KeyCode.Alpha2 : KeyCode.Alpha3);
            Assert.AreEqual(1, salvage.DebugRun.Sorted);
            Assert.AreEqual(1, salvage.DebugRun.Chain);
            Assert.AreEqual(0, salvage.DebugRun.Mistakes);
        }
    }
}
