using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Frontier.Hyperspace.View
{
    public sealed class HyperspaceGame : ITravelMinigame, ITravelBackHandler
    {
        readonly GameState game;
        public HyperspaceSession Session { get; private set; }
        public HyperspaceGame(GameState game) { this.game = game; }
        public bool HandleBack() { return Session != null && Session.HandleBack(); }

        public static string PrepareRecovery(GameState game, TravelCheckpoint trip, PlanetData from, PlanetData to)
        {
            try
            {
                for (int i = 0; i < trip.legs.Count; i++)
                {
                    var leg = trip.legs[i];
                    if (leg.replay == null) continue;
                    var tuning = new HyperspaceTuning();
                    var routeLeg = trip.plan.legs[i];
                    var request = new TravelRunRequest
                    {
                        seed = leg.replay.parameters.seed, distanceLy = routeLeg.lengthLy, seconds = routeLeg.seconds,
                        danger = to.Danger, tier = to.Tier, from = from, to = to
                    };
                    if (!SeedText.TryParse(leg.seed, out var seed) || seed != request.seed ||
                        !HyperspaceSetup.BuildParams(request, leg.replay.ship, tuning,
                            leg.replay.parameters.mode == RunMode.Assist).Equals(leg.replay.parameters))
                        throw new InvalidOperationException("The saved Hyperspace route inputs have changed.");
                    var run = leg.replay.Restore(tuning, HyperspaceSetup.Library, JsonUtility.ToJson(tuning));
                    if (leg.finished)
                    {
                        var result = HyperspaceResults.ToTravelResult(run.BuildResult(), game.db.ResolveResource);
                        if (run.State != RunState.Ended || result.completed != leg.completed ||
                            result.quality != leg.quality || result.xp != leg.xp || result.loot.Count != leg.loot.Count)
                            throw new InvalidOperationException("The saved Hyperspace leg result does not match its journal.");
                        for (int j = 0; j < result.loot.Count; j++)
                            if (result.loot[j].resource.id != leg.loot[j].resourceId || result.loot[j].amount != leg.loot[j].count)
                                throw new InvalidOperationException("The saved Hyperspace loot does not match its journal.");
                        leg.replay.LiveRun = null;
                    }
                }
                return null;
            }
            catch (InvalidOperationException e) { return e.Message; }
        }

        public void Run(VisualElement host, TravelRunRequest request, Action<TravelRunResult> done)
        {
            var go = new GameObject("Hyperspace Run");
            Session = go.AddComponent<HyperspaceSession>();
            Session.Begin(game, request.fullscreenHost ?? host, request, done);
        }
    }
}
