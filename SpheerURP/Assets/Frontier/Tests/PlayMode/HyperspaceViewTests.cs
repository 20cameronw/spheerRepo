using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Frontier.Hyperspace;
using Frontier.Hyperspace.View;
using Object = UnityEngine.Object;

namespace Frontier.Tests
{
    public class HyperspaceViewTests
    {
        GameObject host;
        PanelSettings panel;
        VisualElement root;
        HyperspaceGame game;
        IPrefsStore previousPrefs;
        Action<string, Action<bool>> previousOffer;
        int callbacks;
        TravelRunResult result;

        [SetUp]
        public void SetUp()
        {
            previousPrefs = TravelPrefs.Store; previousOffer = TravelMinigames.ReviveOffer;
            TravelPrefs.Store = new MemoryPrefsStore(); TravelMinigames.ReviveOffer = null;
            host = new GameObject("Hyperspace test UI");
            panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.scaleMode = PanelScaleMode.ConstantPixelSize;
            panel.themeStyleSheet = Resources.Load<ThemeStyleSheet>("Frontier/FrontierTheme");
            var doc = host.AddComponent<UIDocument>(); doc.panelSettings = panel;
            root = doc.rootVisualElement; UiKit.ApplyStyles(root);
            root.style.flexGrow = 1;
            callbacks = 0;
        }

        HyperspaceSession Begin(int tier = 0, ulong seed = 1)
        {
            game = new HyperspaceGame(null);
            game.Run(root, new TravelRunRequest { seed = seed, tier = tier, distanceLy = tier == 0 ? 5 : 3000,
                danger = 0.6f + 1.1f * tier, fuelCost = 20 }, r => { callbacks++; result = r; });
            game.Session.DebugFrozen = true;
            return game.Session;
        }

        [TearDown]
        public void TearDown()
        {
            if (game != null && game.Session != null && !game.Session.Closed) game.Session.Close();
            Object.Destroy(host); Object.Destroy(panel);
            TravelPrefs.Store = previousPrefs; TravelMinigames.ReviveOffer = previousOffer;
        }

        [UnityTest]
        public IEnumerator CompletedRun_ShowsResultAndCallsBackOnlyOnContinue()
        {
            var session = Begin();
            session.DebugAdvance(15000, true, true);
            Assert.AreEqual(RunState.Ended, session.Run.State);
            yield return null;
            Assert.IsNotNull(root.Q<Button>("hyperspace-continue"));
            Assert.AreEqual(0, callbacks);
            session.Close(); session.Close();
            Assert.AreEqual(1, callbacks);
            Assert.IsTrue(result.completed);
            Assert.Greater(result.xp, 0);
            Assert.IsNull(root.Q("hyperspace-run"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator PauseAndResume_DoNotAdvanceSimulation()
        {
            var session = Begin(); session.DebugFrozen = false;
            session.Pause();
            int step = session.Run.StepIndex;
            yield return new WaitForSecondsRealtime(0.12f);
            Assert.AreEqual(step, session.Run.StepIndex);
            session.Resume();
            yield return new WaitForSecondsRealtime(0.12f);
            Assert.Greater(session.Run.StepIndex, step);
            session.FinishTrip(); yield return null;
            session.Close();
            Assert.AreEqual(1, callbacks); Assert.IsFalse(result.completed);
            Assert.AreEqual(0, result.xp); Assert.AreEqual(0, result.loot.Count);
        }

        [UnityTest]
        public IEnumerator Downed_ReviveOfferCanAnswerOnce_AndLateAnswerCannotReplaceResults()
        {
            var session = Begin(12, 3);
            session.DebugAdvance(15000, false);
            Assert.AreEqual(RunState.Downed, session.Run.State);
            yield return null;
            Action<bool> answer = null;
            TravelMinigames.ReviveOffer = (_, callback) => answer = callback;
            session.Revive(); session.Revive();
            Assert.IsNotNull(answer);
            answer(true); answer(true);
            Assert.AreEqual(1, session.Run.RevivesUsed);
            Assert.AreEqual(RunState.Running, session.Run.State);
            session.DebugAdvance(15000, false);
            Assert.AreEqual(RunState.Downed, session.Run.State);
            yield return null;
            session.Revive();
            session.FinishTrip(); yield return null;
            answer(true); yield return null;
            Assert.IsNotNull(root.Q<Button>("hyperspace-continue"));
            Assert.AreEqual(1, session.Run.RevivesUsed);
        }

        [UnityTest]
        public IEnumerator RemovingFullScreenHost_AbandonsAndReleasesSession()
        {
            var session = Begin();
            yield return null;
            root.Clear();
            yield return null;
            Assert.AreEqual(1, callbacks); Assert.IsFalse(result.completed);
            Assert.IsTrue(session == null);
        }

        [UnityTest]
        public IEnumerator FreeReviveAllowance_IsFiniteAndNeverRequiresAnOffer()
        {
            var settings = FrontierSettings.I;
            int previous = settings.freeRevivesPerRun;
            HyperspaceSession session;
            try { settings.freeRevivesPerRun = 1; session = Begin(12, 3); }
            finally { settings.freeRevivesPerRun = previous; }
            session.DebugAdvance(15000, false);
            Assert.AreEqual(RunState.Downed, session.Run.State);
            yield return null;
            session.Revive();
            Assert.AreEqual(1, session.Run.RevivesUsed);
            session.DebugAdvance(15000, false);
            Assert.AreEqual(RunState.Downed, session.Run.State);
            yield return null;
            Assert.IsNull(root.Q<Button>("hyperspace-revive"));
            session.Revive();
            Assert.AreEqual(1, session.Run.RevivesUsed);
            session.FinishTrip(); yield return null;
            session.Close(); Assert.IsFalse(result.completed);
        }

        [UnityTest]
        public IEnumerator Renderer_HasVisibleGeometry_AndDoesNotChangeSimulationDigest()
        {
            var session = Begin(); session.DebugAdvance(700);
            ulong digest = session.Run.StateDigest();
            yield return null; yield return null;
            Assert.AreEqual(digest, session.Run.StateDigest());
            var image = root.Q<Image>();
            Assert.IsNotNull(image);
            var rt = image.image as RenderTexture;
            Assert.IsNotNull(rt); Assert.IsTrue(rt.IsCreated());
            var texture = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            var prev = RenderTexture.active; RenderTexture.active = rt;
            texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); texture.Apply();
            RenderTexture.active = prev;
            var pixels = texture.GetPixels32(); int bright = 0;
            for (int i = 0; i < pixels.Length; i++) if (pixels[i].g > 100) bright++;
            Object.Destroy(texture);
            Assert.Greater(bright, 50, "The isolated warp camera must draw actual geometry, not a blank texture.");
        }

        [UnityTest]
        public IEnumerator PointerDrag_UsesPanelUnits_InvertsY_AndKeepsTheTargetOnRelease()
        {
            Begin(); yield return null;
            var surface = root.Q("hyperspace-input");
            using (var input = new HyperspaceInput(surface, new HyperspaceTuning()))
            {
                input.Read(new PlayerState { x = 0.5f, y = 0.25f });
                using (var e = PointerDownEvent.GetPooled(new Event { type = UnityEngine.EventType.MouseDown,
                    mousePosition = new Vector2(100, 100), button = 0 })) surface.SendEvent(e);
                using (var e = PointerMoveEvent.GetPooled(new Event { type = UnityEngine.EventType.MouseMove,
                    mousePosition = new Vector2(150, 150), button = 0 })) surface.SendEvent(e);
                var controls = input.Read(default);
                float delta = 50 * 5.4f / (surface.resolvedStyle.width * 0.8f) * TravelPrefs.DragSensitivity;
                Assert.AreEqual(0.5f + delta, controls.targetX, 0.001f);
                Assert.AreEqual(0.25f - delta, controls.targetY, 0.001f);
                using (var e = PointerUpEvent.GetPooled(new Event { type = UnityEngine.EventType.MouseUp,
                    mousePosition = new Vector2(150, 150), button = 0 })) surface.SendEvent(e);
                Assert.AreEqual(controls.targetX, input.Read(default).targetX);
                Assert.AreEqual(controls.targetY, input.Read(default).targetY);
            }
        }

        [Test]
        public void DeviceSelfCheck_MatchesAllNineGoldenDigests()
        {
            StringAssert.Contains("9/9", HyperspaceDiagnostics.Check());
        }
    }
}
