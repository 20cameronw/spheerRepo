using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Frontier.Hyperspace.View
{
    /// <summary>Owns one run; presentation never mutates simulation pools or clocks.</summary>
    public sealed class HyperspaceSession : MonoBehaviour
    {
        public HyperspaceRun Run { get; private set; }
        public bool Paused { get; private set; }
        public bool Closed { get; private set; }
        readonly HEvent[] events = new HEvent[EventRing.Capacity];
        HyperspaceTuning tuning;
        GameState game;
        TravelRunRequest request;
        Action<TravelRunResult> done;
        HyperspaceScene scene;
        HyperspaceHud hud;
        HyperspaceAudio sound;
        HyperspaceInput input;
        float accumulator, hudClock;
        bool background, unfocused, downedShown, resultShown, offerPending;
        int freeRevives;
        int confirmedShardCost = -1;

        public void Begin(GameState state, VisualElement host, TravelRunRequest req, Action<TravelRunResult> callback)
        {
            game = state; request = req; done = callback;
            tuning = new HyperspaceTuning();
            var ship = state != null ? ShipStatsMapper.Map(ShipFamiliesFromGame.Read(state)) : ShipStats.Starter();
            Run = HyperspaceSetup.CreateRun(req, ship, tuning, TravelPrefs.AssistMode);
            freeRevives = Mathf.Max(0, FrontierSettings.I.freeRevivesPerRun);
            scene = new HyperspaceScene(transform, state != null ? state.AscensionRank : 0);
            hud = new HyperspaceHud(host, req.safeInsets, scene.Texture, Pause, Resume, FinishTrip, Revive, Close, ReviveWithShards);
            hud.BuildRoute(Run);
            input = new HyperspaceInput(hud.Surface, tuning);
            hud.Bomb.clicked += input.PressBomb;
            sound = new HyperspaceAudio();
            if (TravelPrefs.ShowTutorial) { hud.ShowTip(); TravelPrefs.MarkTutorialRunShown(); }
            hud.Refresh(Run);
        }

        void Update()
        {
            if (Closed || Run == null || background) return;
            if (!Paused && Run.State == RunState.Running)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (!DebugFrozen)
#endif
                {
                    var controls = input.Read(Run.Player);
                    accumulator += Mathf.Min(Time.unscaledDeltaTime, 0.1f);
                    int guard = 0;
                    while (accumulator >= HyperspaceTuning.Dt && guard++ < 6 && Run.State == RunState.Running)
                    {
                        Run.Step(controls);
                        input.ConsumeBomb();
                        controls.bomb = false;
                        accumulator -= HyperspaceTuning.Dt;
                        Drain();
                    }
                }
            }
            if (Run.State == RunState.Downed && !downedShown)
            {
                downedShown = true; accumulator = 0; input.Cancel();
                sound.Stop();
                ShowDowned();
            }
            if (Run.State == RunState.Ended && !resultShown)
            {
                resultShown = true; input.Cancel();
                var result = Run.BuildResult();
                var travel = HyperspaceResults.ToTravelResult(result, FindResource);
                hud.ShowResult(result, travel, HyperspaceResults.FuelRefund(request.fuelCost, FrontierSettings.I.travelRefundMax, travel));
                sound.Stop();
            }
            scene.Present(Run, Paused ? 0 : Mathf.Clamp01(accumulator / HyperspaceTuning.Dt));
            hudClock += Time.unscaledDeltaTime;
            if (hudClock >= 0.1f)
            {
                hudClock = 0; hud.Refresh(Run);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                hud.Performance(Run, scene.TriangleCount);
#endif
            }
            if (!Paused && Run.State == RunState.Running) sound.Engine(Run.Speed);
        }

        ResourceSO FindResource(string id) { return game == null || id == null ? null : game.db.ResolveResource(id); }

        void Drain(bool present = true)
        {
            int count = Run.Events.Drain(events);
            for (int i = 0; i < count; i++)
            {
                if (!present) continue;
                scene.React(events[i]);
                sound.React(events[i]);
                hud.React(events[i]);
            }
        }

        bool CanOfferRevive()
        {
            return Run.CanRevive && (TravelMinigames.ReviveOffer != null || freeRevives > 0);
        }

        void ShowDowned(string feedback = null)
        {
            int cost = game != null && Run.CanRevive ? game.HyperspaceReviveCost : -1;
            bool enabled = cost >= 0 && !game.SaveIsReadOnly && game.ShardBalance >= cost && !offerPending;
            if (cost >= 0 && game.SaveIsReadOnly) feedback = UiText.Get("hyperspace.revive.read-only");
            hud.ShowDowned(CanOfferRevive(), TravelMinigames.ReviveOffer == null, cost,
                game != null ? game.ShardBalance : 0, enabled, confirmedShardCost >= 0, feedback);
        }

        public void ReviveWithShards()
        {
            if (Closed || offerPending || background || unfocused || game == null || !Run.CanRevive) return;
            int cost = game.HyperspaceReviveCost;
            if (cost < 0) return;
            if (confirmedShardCost < 0)
            {
                confirmedShardCost = cost;
                ShowDowned();
                return;
            }
            int quote = confirmedShardCost;
            confirmedShardCost = -1;
            var result = game.PurchaseHyperspaceRevive(Run, quote, out string message);
            if (result == PaidReviveResult.Rejected)
            {
                ShowDowned(message);
                return;
            }
            Revived();
            if (result == PaidReviveResult.PendingSave) hud.ShowPaymentNotice(message);
        }

        public void Pause()
        {
            if (Closed || Run.State != RunState.Running) return;
            Paused = true; accumulator = 0; input.Cancel(); sound.Stop(); hud.ShowPause();
        }

        public bool HandleBack()
        {
            if (Closed) return false;
            if (Run.State == RunState.Running)
            {
                if (Paused) Resume();
                else Pause();
            }
            return true;
        }

        public void Resume()
        {
            if (Closed || background || unfocused || Run.State != RunState.Running) return;
            Paused = false; accumulator = 0; input.Cancel(); hud.HideDialog();
        }

        public void Revive()
        {
            if (Closed || offerPending || Run.State != RunState.Downed || !CanOfferRevive()) return;
            confirmedShardCost = -1;
            if (TravelMinigames.ReviveOffer == null)
            {
                if (Run.Revive()) { freeRevives--; Revived(); }
                return;
            }
            offerPending = true;
            hud.SetOfferPending(true);
            bool answered = false;
            try
            {
                TravelMinigames.ReviveOffer("Hyperspace Run", accepted =>
                {
                    if (answered || this == null || Closed) return;
                    answered = true; offerPending = false;
                    if (Run.State != RunState.Downed) return;
                    if (accepted && Run.State == RunState.Downed && Run.Revive()) Revived();
                    else ShowDowned();
                });
            }
            catch (Exception e)
            {
                Debug.LogException(e); offerPending = false;
                if (!Closed && Run.State == RunState.Downed) ShowDowned();
            }
        }

        void Revived()
        {
            confirmedShardCost = -1;
            downedShown = false; Paused |= background || unfocused; accumulator = 0;
            input.Cancel(); Drain();
            if (Paused) hud.ShowPause();
            else hud.HideDialog();
        }

        public void FinishTrip()
        {
            if (Closed) return;
            Run.Abandon(); Paused = false; offerPending = false; confirmedShardCost = -1; Drain();
        }

        public void Close()
        {
            if (Closed) return;
            if (Run.State != RunState.Ended) Run.Abandon();
            var result = HyperspaceResults.ToTravelResult(Run.BuildResult(), FindResource);
            Closed = true;
            Cleanup();
            var callback = done; done = null;
            Destroy(gameObject);
            callback?.Invoke(result);
        }

        void OnApplicationPause(bool paused)
        {
            background = paused; accumulator = 0;
            if (paused) PauseForInterruption();
        }

        void OnApplicationFocus(bool focused)
        {
            unfocused = !focused;
            if (!focused) PauseForInterruption();
        }

        void PauseForInterruption()
        {
            if (Closed || Run == null) return;
            if (Run.State == RunState.Running) Pause();
            else if (Run.State == RunState.Downed)
            {
                // A revive callback may arrive after an ad or OS interruption.
                Paused = true; accumulator = 0; input.Cancel(); sound.Stop();
            }
        }

        void OnApplicationQuit() { if (Run != null && !Closed) Run.Abandon(); }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public bool DebugFrozen;

        /// <summary>Harness/test fast-forward uses real fixed steps and drains every event.</summary>
        public void DebugAdvance(int steps, bool pilot = true, bool autoRevive = false)
        {
            var bot = new Bot(BotSkill.Expert(), tuning, request.seed);
            for (int i = 0; i < steps && Run.State != RunState.Ended; i++)
            {
                if (Run.State == RunState.Downed)
                {
                    if (!autoRevive || !Run.Revive()) break;
                }
                Run.Step(pilot ? bot.Decide(Run) : new PlayerInput(0, 0, false));
                Drain(i >= steps - 8);
            }
            accumulator = 0;
        }
#endif

        void Cleanup()
        {
            input?.Dispose(); hud?.Dispose(); scene?.Dispose(); sound?.Dispose();
            input = null; hud = null; scene = null; sound = null;
        }

        void OnDestroy()
        {
            if (Closed) return;
            if (Run != null && Run.State != RunState.Ended) Run.Abandon();
            Closed = true; Cleanup();
            var callback = done; done = null;
            if (Run != null) callback?.Invoke(HyperspaceResults.ToTravelResult(Run.BuildResult(), FindResource));
        }
    }
}
