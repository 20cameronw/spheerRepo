using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Frontier
{
    /// <summary>What the travel flow tells a travel mini-game about one jump leg.</summary>
    public struct TravelRunRequest
    {
        /// <summary>Run seed (a stable hash of the route mixed with a per-trip random part). The same seed and the same input give the same run.</summary>
        public ulong seed;
        /// <summary>Length of this leg in light-years.</summary>
        public double distanceLy;
        /// <summary>Danger of the destination (PlanetData.Danger).</summary>
        public float danger;
        /// <summary>Tier of the destination (PlanetData.Tier).</summary>
        public int tier;
        /// <summary>Zero based leg index and the number of legs of the whole route.</summary>
        public int legIndex, legCount;
        public PlanetData from, to;
    }

    /// <summary>What a travel mini-game reports back. The caller applies fuel refund, loot and XP.</summary>
    public struct TravelRunResult
    {
        /// <summary>Performance 0..1. The fuel refund is travelRefundMax x quality x fuel spent on the leg.</summary>
        public float quality;
        /// <summary>False when the run was abandoned or lost without reaching the end: no refund and no loot.</summary>
        public bool completed;
        public int revivesUsed;
        public int kills;
        public bool cleanRun;
        /// <summary>"S", "A", "B" or "C" ("" when the game has no grades).</summary>
        public string grade;
        /// <summary>Optional loot; may be null.</summary>
        public List<ItemAmount> loot;
        public int xp;
    }

    /// <summary>A mini-game played while warping between two worlds (Hyperspace Run, or the legacy tap game).</summary>
    public interface ITravelMinigame
    {
        /// <summary>Builds its UI inside <paramref name="host"/>, runs, removes itself and calls <paramref name="done"/> exactly once.</summary>
        void Run(VisualElement host, TravelRunRequest request, Action<TravelRunResult> done);
    }

    /// <summary>Factory and host hooks for travel mini-games. The app registers the real game here; the default is the legacy one.</summary>
    public static class TravelMinigames
    {
        public static Func<ITravelMinigame> Create = () => new LegacyTravelMinigame();

        /// <summary>
        /// Called when the player's hull is destroyed and a revive is possible. The host shows its own offer (shards, rewarded ad) and answers
        /// through the callback; null means "no revive available" and the game ends the run at once.
        /// </summary>
        public static Action<string, Action<bool>> ReviveOffer;
    }

    /// <summary>The original tap game behind the new interface.</summary>
    public sealed class LegacyTravelMinigame : ITravelMinigame
    {
        public void Run(VisualElement host, TravelRunRequest request, Action<TravelRunResult> done)
        {
            var game = new TravelGame();
            float tier = Mathf.Clamp01(request.tier / 8f);
            game.Run(host, UiKit.Magenta, 1f, tier, r =>
            {
                if (done != null) done(new TravelRunResult { quality = r.quality, completed = true, grade = "" });
            });
        }
    }
}
