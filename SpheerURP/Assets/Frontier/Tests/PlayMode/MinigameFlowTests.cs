using System.Collections;
using System.Threading.Tasks;
using Frontier.Services;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Frontier.Tests
{
    public class MinigameFlowTests
    {
        sealed class PauseProbeMinigame : Minigame
        {
            protected override string Title { get { return "PAUSE TEST"; } }
            protected override string Hint { get { return "Testing pause and abandon."; } }
            protected override void Build() { }
            protected override void BuildScene() { }
            protected override void Frame(float aspect) { }
            protected override void Tick(float dt) { }
            public bool InputAllowed { get { return CanInteract; } }
            public void Complete(float quality, int perfects, int total)
            {
                Finish(new MinigameResult { quality = quality, perfects = perfects, total = total });
            }
            public void RequestDoubleYield() { base.RequestDoubleYield(); }
        }

        sealed class NodeProbeMinigame : NodeGame
        {
            public NodeProbeMinigame(bool tree) : base(tree) { }
            public bool Ready { get { return stage != null && stage.root.Find("Node/Spot") != null; } }
            public GameObject Scene { get { return stage != null ? stage.gameObject : null; } }
            public void Advance(float seconds) { Tick(seconds); }
        }

        sealed class DrillProbeMinigame : DrillGame
        {
            public bool Ready { get { return stage != null && stage.root.Find("Rig/Spin") != null; } }
        }

        sealed class SiphonProbeMinigame : SiphonGame
        {
            public bool Ready { get { return CanInteract; } }
        }

        sealed class ExcavationProbeMinigame : ExcavationGame
        {
            public bool Ready { get { return CanInteract; } }
        }

        sealed class SalvageProbeMinigame : SalvageGame
        {
            public bool Ready { get { return CanInteract; } }
        }

        GameObject host;
        PanelSettings panel;
        VisualElement root;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("Minigame flow test UI");
            panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.scaleMode = PanelScaleMode.ConstantPixelSize;
            panel.themeStyleSheet = Resources.Load<ThemeStyleSheet>("Frontier/FrontierTheme");
            var doc = host.AddComponent<UIDocument>();
            doc.panelSettings = panel;
            root = doc.rootVisualElement;
            UiKit.ApplyStyles(root);
            root.style.width = 1080f;
            root.style.height = 1920f;
        }

        [TearDown]
        public void TearDown()
        {
            if (host != null) Object.Destroy(host);
            if (panel != null) Object.Destroy(panel);
        }

        IEnumerator WaitForResult()
        {
            float deadline = Time.realtimeSinceStartup + 5f;
            while (root.Q("minigame-result") == null && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsNotNull(root.Q("minigame-result"), "The scheduled result must appear within five seconds.");
        }

        [UnityTest]
        public IEnumerator HapticsHost_UnsubscribesWhenDisabledAndDestroyed()
        {
            var feedbackHost = new GameObject("Harvest haptics lifecycle test");
            var feedback = feedbackHost.AddComponent<HarvestHaptics>();
            Assert.IsFalse(HarvestHaptics.IsSupported, "Editor haptics must stay unsupported.");
            feedback.enabled = false;
            HarvestFeedback.Play(HarvestCue.Perfect, MinigameKind.Chop);
            feedback.enabled = true;
            Object.Destroy(feedbackHost);
            yield return null;
            Assert.IsFalse(HarvestHaptics.IsSupported);
            HarvestFeedback.Play(HarvestCue.Complete, MinigameKind.Chop);
        }

        [UnityTest]
        public IEnumerator InputGate_BlocksUnreadyPausedFinishedAndDetachedGames()
        {
            var game = new PauseProbeMinigame();
            game.Run(root, UiKit.Cyan, 1f, 0.2f, null);
            Assert.IsFalse(game.InputAllowed, "Input must wait for the 3D scene.");
            for (int frame = 0; frame < 120 && !game.InputAllowed; frame++) yield return null;
            Assert.IsTrue(game.InputAllowed);
            game.Pause();
            Assert.IsFalse(game.InputAllowed);
            game.Resume();
            Assert.IsTrue(game.InputAllowed);
            game.Complete(1f, 1, 1);
            Assert.IsFalse(game.InputAllowed);
            root.Clear();
            Assert.IsFalse(game.InputAllowed);
        }

        [UnityTest]
        public IEnumerator PauseUi_ResumesAndConfirmsAbandonWithoutReward()
        {
            var game = new PauseProbeMinigame();
            int callbacks = 0;
            MinigameResult result = default;
            game.Run(root, UiKit.Cyan, 1f, 0.2f, r => { callbacks++; result = r; });
            yield return null;
            Assert.IsNotNull(root.Q<Button>("minigame-pause"));

            game.Pause();
            Assert.IsTrue(game.IsPaused);
            Assert.IsNotNull(root.Q<Button>("minigame-resume"));

            game.Resume();
            Assert.IsFalse(game.IsPaused);

            game.Pause();
            game.RequestAbandon();
            Assert.IsNotNull(root.Q<Button>("minigame-keep-playing"));
            Assert.IsNotNull(root.Q<Button>("minigame-confirm-abandon"));
            game.CancelAbandon();
            Assert.IsNotNull(root.Q<Button>("minigame-resume"));
            game.RequestAbandon();
            game.ConfirmAbandon();
            yield return new WaitForSecondsRealtime(0.35f);

            Assert.AreEqual(1, callbacks);
            Assert.IsTrue(result.abandoned);
            Assert.AreEqual(0f, result.quality);
            Assert.IsNull(root.Q("minigame-pause-layer"));
        }

        [UnityTest]
        public IEnumerator SharedPanelMotionUsesTransformsAndSettlesWithoutMovingLayout()
        {
            yield return null;
            var modal = new FrontierModal();
            var popup = new FrontierPopup();
            var toast = new FrontierToast { Text = "Saved" };
            var initialOffsets = new System.Collections.Generic.Dictionary<VisualElement, float>();
            foreach (var element in new VisualElement[] { modal, popup, toast })
            {
                Assert.AreNotEqual(0, element.usageHints & UsageHints.DynamicTransform);
                element.RegisterCallbackOnce<GeometryChangedEvent>(e =>
                    initialOffsets[element] = element.resolvedStyle.translate.y);
                root.Add(element);
                Assert.IsTrue(element.ClassListContains("fr-surface-enter-start"));
            }
            float deadline = Time.realtimeSinceStartup + 3f;
            while (toast.ClassListContains("fr-surface-enter-start") && Time.realtimeSinceStartup < deadline)
                yield return null;
            yield return new WaitForSecondsRealtime(0.2f);
            yield return null;
            Assert.AreEqual(16f, initialOffsets[modal], 0.01f);
            Assert.AreEqual(6f, initialOffsets[popup], 0.01f);
            Assert.AreEqual(-8f, initialOffsets[toast], 0.01f);
            foreach (var element in new VisualElement[] { modal, popup, toast })
            {
                Assert.IsFalse(element.ClassListContains("fr-surface-enter-start"));
                Assert.AreEqual(0f, element.resolvedStyle.translate.y, 0.01f);
                Assert.AreEqual(1f, element.resolvedStyle.opacity, 0.001f);
            }
        }

        [UnityTest]
        public IEnumerator BackPausesResumesAndCancelsAbandonWithoutCollecting()
        {
            var game = new PauseProbeMinigame();
            int callbacks = 0;
            game.Run(root, UiKit.Cyan, 1f, 0.2f, r => callbacks++);
            yield return null;
            Assert.IsTrue(game.HandleBack());
            Assert.IsTrue(game.IsPaused);
            game.RequestAbandon();
            game.HandleBack();
            Assert.IsTrue(game.IsPaused);
            Assert.IsNull(root.Q<Button>("minigame-confirm-abandon"));
            game.HandleBack();
            Assert.IsFalse(game.IsPaused);
            game.Complete(0.8f, 8, 10);
            yield return WaitForResult();
            game.HandleBack();
            Assert.AreEqual(0, callbacks);
            Assert.IsNotNull(root.Q("minigame-result"));
            game.ContinueResult();
            Assert.AreEqual(1, callbacks);
        }

        [UnityTest]
        public IEnumerator ResultScreen_ShowsGradeAndWaitsForCollectAction()
        {
            var game = new PauseProbeMinigame();
            int callbacks = 0;
            MinigameResult result = default;
            game.Run(root, UiKit.Cyan, 1f, 0.2f, r => { callbacks++; result = r; });
            yield return null;

            game.Complete(0.91f, 9, 10);
            yield return WaitForResult();

            Assert.AreEqual(0, callbacks, "Rewards must not be collected before the player acknowledges the result.");
            Assert.AreEqual("GRADE S", root.Q<Label>(className: "fr-minigame-result-grade").text);
            Assert.AreEqual("Perfect hits  9 / 10", root.Q<Label>(className: "fr-minigame-result-detail").text);
            var collect = root.Q<Button>("minigame-collect-resources");
            Assert.IsNotNull(collect);
            game.ContinueResult();

            Assert.AreEqual(1, callbacks);
            Assert.AreEqual(0.91f, result.quality, 0.0001f);
            Assert.IsFalse(result.abandoned);
            Assert.IsNull(root.Q("minigame-result"));
            game.ContinueResult();
            Assert.AreEqual(1, callbacks, "The result handoff must only run once.");
        }

        [UnityTest]
        public IEnumerator ResultScreen_AppliesOnlyACompletedDoubleYieldOffer()
        {
            var game = new PauseProbeMinigame();
            MinigameResult result = default;
            game.Run(root, UiKit.Cyan, 1f, 0.2f, r => result = r,
                () => Task.FromResult(ServiceResult<RewardedAdResult>.Success(
                    new RewardedAdResult(true, "double-yield"))),
                _ => true);
            yield return null;
            game.Complete(0.8f, 8, 10);
            yield return WaitForResult();
            Assert.IsNotNull(root.Q<Button>("minigame-double-yield"));

            game.RequestDoubleYield();
            Assert.AreEqual("BONUS UNLOCKED", root.Q<Button>("minigame-double-yield").text);
            Assert.IsTrue(root.Q<Button>("minigame-collect-resources").enabledSelf);
            game.ContinueResult();
            Assert.IsTrue(result.rewardedDoubleYield);
        }

        [UnityTest]
        public IEnumerator ResultScreen_HidesDoubleYieldWhenCallerRejectsTheOffer()
        {
            var game = new PauseProbeMinigame();
            game.Run(root, UiKit.Cyan, 1f, 0.2f, null,
                () => Task.FromResult(ServiceResult<RewardedAdResult>.Success(
                    new RewardedAdResult(true, "double-yield"))),
                _ => false);
            yield return null;
            game.Complete(0.8f, 8, 10);
            yield return WaitForResult();
            Assert.IsNull(root.Q<Button>("minigame-double-yield"));
        }

        [UnityTest]
        public IEnumerator ResultScreen_MapsQualityToGradeThresholds()
        {
            var qualities = new[] { 0.9f, 0.75f, 0.55f, 0.54f };
            var grades = new[] { "GRADE S", "GRADE A", "GRADE B", "GRADE C" };

            for (int i = 0; i < qualities.Length; i++)
            {
                var game = new PauseProbeMinigame();
                game.Run(root, UiKit.Cyan, 1f, 0.2f, null);
                yield return null;

                game.Complete(qualities[i], 0, 0);
                yield return WaitForResult();

                Assert.AreEqual(grades[i], root.Q<Label>(className: "fr-minigame-result-grade").text);
                game.ContinueResult();
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator ChopAndMineBots_UseLiveRulesThroughStageBreaksAndResultHandoff()
        {
            foreach (bool tree in new[] { true, false })
            {
                var game = new NodeProbeMinigame(tree);
                int callbacks = 0;
                MinigameResult result = default;
                game.Run(root, UiKit.Cyan, 1f, 0.2f, r => { callbacks++; result = r; });
                for (int frame = 0; frame < 120 && !game.Ready; frame++) yield return null;
                Assert.IsTrue(game.Ready, "The rendered node scene must be ready before bot input.");
                var scene = game.Scene;
                Assert.IsNotNull(root.Q("minigame-tool-feedback"));

                game.Pause();
                for (int hit = 0; hit < 20; hit++) game.DebugStrike(HarvestStrike.Perfect);
                game.Resume();

                // 3 HP per stage at this difficulty; each perfect swing deals 1.7.
                for (int hit = 0; hit < 8; hit++)
                {
                    game.DebugStrike(HarvestStrike.Perfect);
                    if (hit < 7)
                    {
                        Assert.AreEqual("RECOVERING", root.Q<Label>("minigame-tool-state").text);
                        game.Advance(0.6f);
                        Assert.AreEqual("READY", root.Q<Label>("minigame-tool-state").text);
                    }
                }
                yield return WaitForResult();
                Assert.IsNotNull(root.Q("minigame-result"));
                StringAssert.StartsWith("Perfect hits", root.Q<Label>(className: "fr-minigame-result-detail").text);
                Assert.AreEqual(0, callbacks);
                Assert.IsTrue(scene == null, "The 3D stage must be released while awaiting collection.");
                game.ContinueResult();
                game.ContinueResult();
                Assert.AreEqual(1, callbacks);
                Assert.AreEqual(8, result.total, "Paused input must not count as swings.");
                Assert.AreEqual(8, result.perfects);
                Assert.AreEqual(1f, result.quality);
                Assert.IsFalse(result.abandoned);
            }
        }

        [UnityTest]
        public IEnumerator DrillBot_UsesPressureAndContactThroughResultHandoff()
        {
            var game = new DrillProbeMinigame { toolDamage = 8f };
            int callbacks = 0;
            MinigameResult result = default;
            game.Run(root, UiKit.Cyan, 1f, 0.2f, r => { callbacks++; result = r; });
            for (int frame = 0; frame < 120 && !game.Ready; frame++) yield return null;
            Assert.IsTrue(game.Ready);

            game.Pause();
            float elapsed = game.DebugRun.Elapsed;
            game.DebugAdvance(0.05f, true);
            Assert.AreEqual(elapsed, game.DebugRun.Elapsed, "Paused debug input must not advance the live drill.");
            game.Resume();
            for (int frame = 0; frame < 1800 && !game.DebugRun.Completed; frame++)
                game.DebugAdvance(1f / 60f, game.DebugRun.Pressure < game.DebugRun.BandCenter - 0.02f);
            Assert.IsTrue(game.DebugRun.Completed, "The live pressure bot must complete within 30 simulated seconds.");
            yield return WaitForResult();
            Assert.IsNotNull(root.Q("minigame-result"));
            StringAssert.StartsWith("Seconds on target", root.Q<Label>(className: "fr-minigame-result-detail").text);
            Assert.AreEqual(0, callbacks);
            game.ContinueResult();
            game.ContinueResult();
            Assert.AreEqual(1, callbacks);
            Assert.AreEqual(game.DebugRun.Quality, result.quality);
            Assert.AreEqual(Mathf.RoundToInt(game.DebugRun.InBandSeconds), result.perfects);
            Assert.AreEqual(Mathf.Max(1, Mathf.RoundToInt(game.DebugRun.Elapsed)), result.total);
            Assert.IsFalse(result.abandoned);
        }

        [UnityTest]
        public IEnumerator SiphonBot_UsesLiveWindowsAndPauseGateThroughCollection()
        {
            var game = new SiphonProbeMinigame();
            int callbacks = 0;
            MinigameResult result = default;
            game.Run(root, UiKit.Cyan, 1f, 0.5f, r => { callbacks++; result = r; });
            for (int frame = 0; frame < 120 && !game.Ready; frame++) yield return null;
            Assert.IsTrue(game.Ready);
            game.Pause();
            game.DebugPump();
            game.DebugAdvance(1f);
            Assert.AreEqual(0, game.DebugRun.Pulses);
            game.Resume();
            for (int frame = 0; frame < 5400 && !game.DebugRun.Completed; frame++)
            {
                game.DebugAdvance(1f / 60f);
                if (Mathf.Abs(game.DebugRun.Needle - game.DebugRun.Target) < game.DebugRun.BandWidth * 0.2f)
                {
                    game.DebugPump();
                    if (game.DebugRun.Cooldown > 0f)
                        Assert.AreEqual("RECOVERING", root.Q<Label>("minigame-tool-state").text);
                }
            }
            Assert.IsTrue(game.DebugRun.Completed);
            yield return WaitForResult();
            StringAssert.StartsWith("Perfect pumps", root.Q<Label>(className: "fr-minigame-result-detail").text);
            Assert.AreEqual(0, callbacks);
            game.ContinueResult();
            game.ContinueResult();
            Assert.AreEqual(1, callbacks);
            Assert.AreEqual(7, result.perfects);
            Assert.AreEqual(7, result.total);
            Assert.AreEqual(game.DebugRun.Quality, result.quality);
        }

        [UnityTest]
        public IEnumerator ExcavationBot_ResetsGridAndFinishesOnlyAfterThreeSeams()
        {
            var game = new ExcavationProbeMinigame();
            int callbacks = 0;
            MinigameResult result = default;
            game.Run(root, UiKit.Cyan, 1f, 0.5f, r => { callbacks++; result = r; });
            for (int frame = 0; frame < 120 && !game.Ready; frame++) yield return null;
            Assert.IsTrue(game.Ready);
            game.Pause();
            game.DebugPing(game.DebugRun.Target);
            Assert.AreEqual(0, game.DebugRun.Pings);
            game.Resume();
            for (int seam = 0; seam < 3; seam++)
            {
                int target = game.DebugRun.Target;
                int wrong = (target + 1) % 9;
                game.DebugPing(wrong);
                Assert.IsFalse(root.Q<Button>("excavate-cell-" + wrong).enabledSelf);
                game.DebugPing(target);
                if (seam < 2)
                {
                    Assert.IsTrue(root.Q<Button>("excavate-cell-" + wrong).enabledSelf);
                    Assert.AreEqual("PING", root.Q<Button>("excavate-cell-" + wrong).text,
                        "A new seam must not retain the old seam's echo clues.");
                }
            }
            yield return WaitForResult();
            StringAssert.StartsWith("Seams uncovered", root.Q<Label>(className: "fr-minigame-result-detail").text);
            Assert.AreEqual(0, callbacks);
            game.ContinueResult();
            game.ContinueResult();
            Assert.AreEqual(1, callbacks);
            Assert.AreEqual(3, result.perfects);
            Assert.AreEqual(3, result.total);
            Assert.AreEqual(1f, result.quality);
        }

        [UnityTest]
        public IEnumerator SalvageBot_RejectsPausedAndDuplicateCastsThenCollectsOnce()
        {
            var game = new SalvageProbeMinigame();
            int callbacks = 0;
            MinigameResult result = default;
            game.Run(root, UiKit.Cyan, 1f, 0.5f, r => { callbacks++; result = r; });
            for (int frame = 0; frame < 120 && !game.Ready; frame++) yield return null;
            Assert.IsTrue(game.Ready);
            game.Pause();
            game.DebugCast(0);
            Assert.AreEqual(0, game.DebugRun.Casts);
            game.Resume();
            for (int target = 0; target < 6; target++)
            {
                game.DebugCast(target);
                game.DebugCast(target);
            }
            yield return WaitForResult();
            StringAssert.StartsWith("Caches recovered", root.Q<Label>(className: "fr-minigame-result-detail").text);
            Assert.AreEqual(0, callbacks);
            game.ContinueResult();
            game.ContinueResult();
            Assert.AreEqual(1, callbacks);
            Assert.AreEqual(6, game.DebugRun.Casts);
            Assert.AreEqual(6, result.perfects);
            Assert.AreEqual(6, result.total);
            Assert.AreEqual(1f, result.quality);
        }
    }
}
