using System;
using System.Collections;
using System.IO;
using System.Text.RegularExpressions;
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
        string saveDirectory, previousSaveDirectory;
        GameState previousState;

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
            previousSaveDirectory = GameState.SaveDirectoryOverride;
            previousState = GameState.I;
            saveDirectory = Path.Combine(Application.temporaryCachePath, "hyperspace-revive-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(saveDirectory);
            GameState.SaveDirectoryOverride = saveDirectory;
        }

        HyperspaceSession Begin(int tier = 0, ulong seed = 1, GameState state = null)
        {
            return BeginRequest(state, new TravelRunRequest { seed = seed, tier = tier, distanceLy = tier == 0 ? 5 : 3000,
                danger = 0.6f + 1.1f * tier, fuelCost = 20 });
        }

        HyperspaceSession BeginRequest(GameState state, TravelRunRequest request)
        {
            game = new HyperspaceGame(state);
            game.Run(root, request, r => { callbacks++; result = r; });
            game.Session.DebugFrozen = true;
            return game.Session;
        }

        [TearDown]
        public void TearDown()
        {
            if (game != null && game.Session != null && !game.Session.Closed) game.Session.Close();
            Object.Destroy(host); Object.Destroy(panel);
            TravelPrefs.Store = previousPrefs; TravelMinigames.ReviveOffer = previousOffer;
            GameState.SaveDirectoryOverride = previousSaveDirectory;
            GameState.I = previousState;
            if (Directory.Exists(saveDirectory)) Directory.Delete(saveDirectory, true);
        }

        GameState FundedState(int shards)
        {
            var db = GameDatabase.Load();
            var state = new GameState(db, false);
            Assert.IsTrue(state.Save());
            string path = Path.Combine(saveDirectory, "frontier_save.json");
            var save = SaveSerializer.Instance.DeserializeV2(File.ReadAllText(path)).Normalize();
            save.wallet.Clear();
            save.wallet.Add(new WalletEntry { currency = "shards", amount = shards });
            File.WriteAllText(path, SaveSerializer.Instance.Serialize(save));
            return new GameState(db, true);
        }

        [UnityTest]
        public IEnumerator NewJourneyIgnoresPreviouslySavedAssistPreference()
        {
            TravelPrefs.AssistMode = true;
            var session = Begin();
            yield return null;
            Assert.AreEqual(RunMode.Normal, session.Run.Route.run.mode);
            session.Close();
        }

        [UnityTest]
        public IEnumerator PaidReviveAndJournalPersistTogetherWithoutRechargingOnRecovery()
        {
            var state = FundedState(1000);
            foreach (var upgrade in state.db.upgrades)
                if (upgrade.id == "pack" || upgrade.id == "jump" || upgrade.id == "engine" || upgrade.id.StartsWith("hull_", StringComparison.Ordinal))
                    state.DebugSetUpgrade(upgrade, upgrade.MaxLevel);
            state.backpack.Set(state.db.fuel.id, 100000);
            PlanetData target = null;
            foreach (var planet in state.universe.AuthoredPlanets)
                if (target == null || planet.Tier > target.Tier) target = planet;
            var plan = state.PlanRoute(target);
            Assert.IsTrue(plan.reachable);
            Assert.IsTrue(state.BeginTravelCheckpoint(plan, out string error), error);
            var checkpoint = state.PendingTravel.legs[0];
            checkpoint.seed = SeedText.Format(3);
            var leg = plan.legs[0];
            var request = new TravelRunRequest { seed = 3, tier = target.Tier, danger = target.Danger,
                distanceLy = leg.lengthLy, seconds = leg.seconds, fuelCost = leg.fuel, checkpoint = checkpoint };
            var session = BeginRequest(state, request);
            session.DebugAdvance(20000, false);
            Assert.AreEqual(RunState.Downed, session.Run.State);
            yield return null;
            int balance = state.ShardBalance, price = state.HyperspaceReviveCost;
            session.ReviveWithShards();
            string blockedTemp = Path.Combine(saveDirectory, SaveSchema.FileName + SaveSchema.TempSuffix);
            Directory.CreateDirectory(blockedTemp);
            LogAssert.Expect(LogType.Warning, new Regex("Frontier save failed:"));
            try { session.ReviveWithShards(); }
            finally { Directory.Delete(blockedTemp); }
            Assert.AreEqual(balance - price, state.ShardBalance);
            ulong digest = session.Run.StateDigest();
            var uncommitted = new GameState(state.db, true);
            Assert.AreEqual(balance, uncommitted.ShardBalance, "Failed writes must preserve both the old debit and old run state on disk.");
            Assert.IsTrue(uncommitted.ValidatePendingTravel(out var uncommittedTarget, out error), error);
            Assert.IsNull(HyperspaceGame.PrepareRecovery(uncommitted, uncommitted.PendingTravel, uncommitted.planet, uncommittedTarget));
            Assert.AreEqual(RunState.Downed, uncommitted.PendingTravel.legs[0].replay.LiveRun.State);
            Assert.AreEqual(0, uncommitted.PendingTravel.legs[0].replay.LiveRun.RevivesUsed);
            Assert.IsTrue(state.Save(), "The pending payment and journal should persist together on retry.");
            var restarted = new GameState(state.db, true);
            Assert.AreEqual(balance - price, restarted.ShardBalance);
            Assert.IsTrue(restarted.ValidatePendingTravel(out var recoveredTarget, out error), error);
            Assert.IsNull(HyperspaceGame.PrepareRecovery(restarted, restarted.PendingTravel, restarted.planet, recoveredTarget));
            var recoveredCheckpoint = restarted.PendingTravel.legs[0];
            Assert.AreEqual(digest, recoveredCheckpoint.replay.LiveRun.StateDigest());
            Assert.AreEqual(1, recoveredCheckpoint.replay.LiveRun.RevivesUsed);
            Assert.AreEqual(checkpoint.freeRevives, recoveredCheckpoint.freeRevives);
            Assert.AreEqual(balance - price, restarted.ShardBalance, "Replay must never invoke a purchase again.");
            Assert.AreEqual(0, callbacks);
        }

        [UnityTest]
        public IEnumerator ShardReviveConfirmsSpendsOncePersistsAndPreservesInterruptionPause()
        {
            var state = FundedState(1000);
            var session = Begin(12, 3, state);
            session.DebugAdvance(15000, false);
            Assert.AreEqual(RunState.Downed, session.Run.State);
            yield return null;
            var button = root.Q<Button>("hyperspace-revive-shards");
            Assert.IsNotNull(button);
            Assert.IsTrue(button.enabledInHierarchy);
            int balance = state.ShardBalance;
            int price = state.HyperspaceReviveCost;
            using (var submit = NavigationSubmitEvent.GetPooled())
            {
                submit.target = button;
                button.SendEvent(submit);
            }
            Assert.AreEqual(RunState.Downed, session.Run.State);
            Assert.AreEqual(balance, state.ShardBalance);
            Assert.AreEqual(UiText.Get("hyperspace.revive.shards-confirm", price), root.Q<Button>("hyperspace-revive-shards").text);
            session.SendMessage("OnApplicationPause", true);
            session.ReviveWithShards();
            Assert.AreEqual(balance, state.ShardBalance, "Backgrounded runs cannot spend shards.");
            session.SendMessage("OnApplicationPause", false);
            using (var submit = NavigationSubmitEvent.GetPooled())
            {
                var confirm = root.Q<Button>("hyperspace-revive-shards");
                submit.target = confirm;
                confirm.SendEvent(submit);
            }
            session.ReviveWithShards();
            Assert.AreEqual(RunState.Running, session.Run.State);
            Assert.AreEqual(1, session.Run.RevivesUsed);
            Assert.AreEqual(balance - price, state.ShardBalance);
            Assert.IsTrue(session.Paused, "Revival must preserve an interruption pause.");
            var reloaded = new GameState(state.db, true);
            Assert.AreEqual(balance - price, reloaded.ShardBalance);
            session.Resume();
            session.DebugAdvance(15000, false);
            Assert.AreEqual(RunState.Downed, session.Run.State);
            yield return null;
            session.ReviveWithShards();
            session.FinishTrip();
            session.ReviveWithShards();
            Assert.AreEqual(balance - price, state.ShardBalance, "Finishing cancels a pending paid confirmation.");
            Assert.AreEqual(1, session.Run.RevivesUsed);
        }

        [UnityTest]
        public IEnumerator ShardReviveRevalidatesQuoteAndRunStateAndHonorsTheSharedCap()
        {
            var state = FundedState(1000);
            var session = Begin(12, 3, state);
            int price = state.HyperspaceReviveCost;
            Assert.Greater(price, 0);
            Assert.AreEqual(PaidReviveResult.Rejected, state.PurchaseHyperspaceRevive(null, price, out _));
            Assert.AreEqual(PaidReviveResult.Rejected, state.PurchaseHyperspaceRevive(session.Run, price, out _));
            for (int i = 0; i < session.Run.Tuning.maxRevives; i++)
            {
                session.DebugAdvance(15000, false);
                Assert.AreEqual(RunState.Downed, session.Run.State);
                Assert.AreEqual(PaidReviveResult.Rejected, state.PurchaseHyperspaceRevive(session.Run, price - 1, out _));
                Assert.AreEqual(1000 - i * price, state.ShardBalance);
                Assert.AreEqual(PaidReviveResult.Saved, state.PurchaseHyperspaceRevive(session.Run, price, out _));
                Assert.AreEqual(PaidReviveResult.Rejected, state.PurchaseHyperspaceRevive(session.Run, price, out _));
            }
            session.DebugAdvance(15000, false);
            Assert.AreEqual(RunState.Downed, session.Run.State);
            Assert.IsFalse(session.Run.CanRevive);
            Assert.AreEqual(PaidReviveResult.Rejected, state.PurchaseHyperspaceRevive(session.Run, price, out _));
            yield return null;
            Assert.IsNull(root.Q<Button>("hyperspace-revive-shards"));
            int balance = state.ShardBalance;
            session.FinishTrip();
            Assert.AreEqual(PaidReviveResult.Rejected, state.PurchaseHyperspaceRevive(session.Run, price, out _));
            Assert.AreEqual(balance, state.ShardBalance);
        }

        [UnityTest]
        public IEnumerator PendingExternalOfferBlocksShardSpendingAndLateAnswersCannotReviveTwice()
        {
            var state = FundedState(1000);
            Action<bool> answer = null;
            TravelMinigames.ReviveOffer = (_, callback) => answer = callback;
            var session = Begin(12, 3, state);
            session.DebugAdvance(15000, false);
            yield return null;
            session.ReviveWithShards();
            session.Revive();
            Assert.IsNotNull(answer);
            Assert.IsFalse(root.Q<Button>("hyperspace-revive-shards").enabledSelf);
            session.ReviveWithShards();
            session.ReviveWithShards();
            Assert.AreEqual(1000, state.ShardBalance);
            answer(false);
            session.ReviveWithShards();
            Assert.AreEqual(RunState.Downed, session.Run.State, "Starting an offer clears the previous shard confirmation.");
            session.ReviveWithShards();
            answer(true);
            Assert.AreEqual(RunState.Running, session.Run.State);
            Assert.AreEqual(1, session.Run.RevivesUsed);
            Assert.AreEqual(1000 - state.HyperspaceReviveCost, state.ShardBalance);
        }

        [UnityTest]
        public IEnumerator ShardReviveRejectsInsufficientReadOnlyAndStalePricesWithoutMutatingRun()
        {
            var state = FundedState(0);
            var session = Begin(12, 3, state);
            session.DebugAdvance(15000, false);
            yield return null;
            Assert.AreEqual(RunState.Downed, session.Run.State);
            Assert.IsFalse(root.Q<Button>("hyperspace-revive-shards").enabledSelf);
            Assert.AreEqual(PaidReviveResult.Rejected, state.PurchaseHyperspaceRevive(session.Run, state.HyperspaceReviveCost, out var message));
            Assert.IsNotEmpty(message);
            Assert.AreEqual(0, state.ShardBalance);
            Assert.AreEqual(0, session.Run.RevivesUsed);
            Assert.AreEqual(PaidReviveResult.Rejected, state.PurchaseHyperspaceRevive(session.Run, -1, out _));
            state.saves.Store.MarkReadOnly("Test future save");
            Assert.AreEqual(PaidReviveResult.Rejected, state.PurchaseHyperspaceRevive(session.Run, int.MaxValue, out message));
            StringAssert.Contains("read-only", message);
            Assert.AreEqual(RunState.Downed, session.Run.State);
            Assert.AreEqual(0, session.Run.RevivesUsed);
        }

        [UnityTest]
        public IEnumerator ShardReviveSaveFailureReportsPendingWithoutRepeatingTheDebit()
        {
            var state = FundedState(1000);
            var session = Begin(12, 3, state);
            session.DebugAdvance(15000, false);
            yield return null;
            string path = Path.Combine(saveDirectory, "frontier_save.json");
            File.Delete(path);
            Directory.CreateDirectory(path);
            int price = state.HyperspaceReviveCost;
            int balance = state.ShardBalance;
            session.ReviveWithShards();
            LogAssert.Expect(LogType.Warning, new Regex("Frontier save failed:"));
            session.ReviveWithShards();
            Assert.AreEqual(RunState.Running, session.Run.State);
            Assert.AreEqual(balance - price, state.ShardBalance);
            Assert.IsTrue(state.saves.IsDirty);
            Assert.IsTrue(root.Query<Label>().ToList().Exists(label => label.text != null && label.text.Contains("saving failed")));
            session.ReviveWithShards();
            Assert.AreEqual(balance - price, state.ShardBalance);
            Directory.Delete(path);
            Assert.IsTrue(state.Save());
            Assert.AreEqual(balance - price, new GameState(state.db, true).ShardBalance);
        }

        [UnityTest]
        public IEnumerator BackPausesAndResumesWithoutFinishingTheTrip()
        {
            var session = Begin();
            Assert.IsTrue(game.HandleBack());
            Assert.IsTrue(session.Paused);
            Assert.AreEqual(UiText.Get("harvest.resume"), root.Q<Button>("hyperspace-resume").text);
            Assert.AreEqual(UiText.Get("hyperspace.finish"), root.Q<Button>("hyperspace-finish").text);
            Assert.IsTrue(root.Q("hyperspace-dialog").Query<Label>().ToList()
                .Exists(label => label.text == UiText.Get("hyperspace.pause.help")));
            int step = session.Run.StepIndex;
            yield return null;
            Assert.AreEqual(step, session.Run.StepIndex);
            Assert.IsTrue(game.HandleBack());
            Assert.IsFalse(session.Paused);
            Assert.AreEqual(0, callbacks);
            Assert.IsFalse(session.Closed);
        }

        [UnityTest]
        public IEnumerator AppInterruptionRequiresExplicitResumeAndDoesNotAdvanceInBackground()
        {
            var session = Begin();
            session.SendMessage("OnApplicationPause", true);
            Assert.IsTrue(session.Paused);
            session.Resume();
            Assert.IsTrue(session.Paused);
            int step = session.Run.StepIndex;
            yield return null;
            Assert.AreEqual(step, session.Run.StepIndex);
            session.SendMessage("OnApplicationPause", false);
            Assert.IsTrue(session.Paused, "Returning to the app must not auto-resume the run.");
            session.Resume();
            Assert.IsFalse(session.Paused);
            session.SendMessage("OnApplicationFocus", false);
            session.Resume();
            Assert.IsTrue(session.Paused);
            session.SendMessage("OnApplicationFocus", true);
            Assert.IsTrue(session.Paused);
            Assert.IsTrue(game.HandleBack());
            Assert.IsFalse(session.Paused);
            Assert.AreEqual(0, callbacks);
        }

        [UnityTest]
        public IEnumerator ReviveAfterAppInterruptionRemainsPausedUntilExplicitResume()
        {
            var session = Begin(12, 3);
            session.DebugAdvance(15000, false);
            Assert.AreEqual(RunState.Downed, session.Run.State);
            yield return null;
            Action<bool> answer = null;
            TravelMinigames.ReviveOffer = (_, callback) => answer = callback;
            session.Revive();
            session.SendMessage("OnApplicationPause", true);
            answer(true);
            Assert.AreEqual(RunState.Running, session.Run.State);
            Assert.AreEqual(1, session.Run.RevivesUsed);
            Assert.IsTrue(session.Paused);
            session.SendMessage("OnApplicationPause", false);
            Assert.IsTrue(session.Paused);
            Assert.IsNotNull(root.Q<Button>("hyperspace-resume"));
            int step = session.Run.StepIndex;
            yield return null;
            Assert.AreEqual(step, session.Run.StepIndex);
            session.Resume();
            Assert.IsFalse(session.Paused);
            Assert.AreEqual(0, callbacks);
        }

        [UnityTest]
        public IEnumerator CompletedRun_ShowsResultAndCallsBackOnlyOnContinue()
        {
            var session = Begin();
            session.DebugAdvance(15000, true, true);
            Assert.AreEqual(RunState.Ended, session.Run.State);
            yield return null;
            Assert.IsNotNull(root.Q<Button>("hyperspace-continue"));
            Assert.AreEqual(UiText.Get("hyperspace.result.continue"), root.Q<Button>("hyperspace-continue").text);
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
            Assert.IsTrue(root.Q("hyperspace-dialog").Query<Label>().ToList()
                .Exists(label => label.text == UiText.Get("hyperspace.result.unfinished")));
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
            Assert.AreEqual(UiText.Get("hyperspace.offer.pending"), root.Q<Button>("hyperspace-revive").text);
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
