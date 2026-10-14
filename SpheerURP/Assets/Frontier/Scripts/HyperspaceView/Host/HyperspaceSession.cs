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
        bool background, downedShown, resultShown, offerPending;
        int freeRevives;

        public void Begin(GameState state, VisualElement host, TravelRunRequest req, Action<TravelRunResult> callback)
        {
            game = state; request = req; done = callback;
            tuning = new HyperspaceTuning();
            var ship = state != null ? ShipStatsMapper.Map(ShipFamiliesFromGame.Read(state)) : ShipStats.Starter();
            Run = HyperspaceSetup.CreateRun(req, ship, tuning, TravelPrefs.AssistMode);
            freeRevives = Mathf.Max(0, FrontierSettings.I.freeRevivesPerRun);
            scene = new HyperspaceScene(transform);
            hud = new HyperspaceHud(host, req.safeInsets, scene.Texture, Pause, Resume, FinishTrip, Revive, Close);
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
                hud.ShowDowned(CanOfferRevive(), TravelMinigames.ReviveOffer == null);
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

        ResourceSO FindResource(string id) { return game == null || id == null ? null : game.db.FindResource(id); }

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

        public void Pause()
        {
            if (Closed || Run.State != RunState.Running) return;
            Paused = true; accumulator = 0; input.Cancel(); sound.Stop(); hud.ShowPause();
        }

        public void Resume()
        {
            if (Closed || Run.State != RunState.Running) return;
            Paused = false; accumulator = 0; input.Cancel(); hud.HideDialog();
        }

        public void Revive()
        {
            if (Closed || offerPending || Run.State != RunState.Downed || !CanOfferRevive()) return;
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
                    else hud.ShowDowned(CanOfferRevive(), false);
                });
            }
            catch (Exception e)
            {
                Debug.LogException(e); offerPending = false;
                if (!Closed && Run.State == RunState.Downed) hud.ShowDowned(CanOfferRevive(), false);
            }
        }

        void Revived()
        {
            downedShown = false; Paused = false; accumulator = 0;
            input.Cancel(); hud.HideDialog(); Drain();
        }

        public void FinishTrip()
        {
            if (Closed) return;
            Run.Abandon(); Paused = false; offerPending = false; Drain();
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
            if (paused && Run != null && Run.State == RunState.Running) Pause();
        }

        void OnApplicationFocus(bool focused)
        {
            if (!focused && Run != null && Run.State == RunState.Running) Pause();
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
