using System;
using System.Collections.Generic;
using Frontier.Hyperspace;

namespace Frontier
{
    [Serializable]
    public sealed class TravelLegCheckpoint
    {
        public string seed;
        public RunReplay replay;
        public bool hasReplay;
        public int freeRevives;
        public float autopilotRemaining;
        public bool finished, completed;
        public float quality;
        public int xp;
        public List<StackRecord> loot = new List<StackRecord>();
    }

    /// <summary>Fuel and rewards settle together only after every leg; until then the player remains at the origin.</summary>
    [Serializable]
    public sealed class TravelCheckpoint
    {
        public const int CurrentVersion = 1;
        public int version = CurrentVersion;
        public RoutePlan plan;
        public int nextLeg;
        public bool hasDepartureCargoFill;
        public double departureCargoFill;
        public List<TravelLegCheckpoint> legs = new List<TravelLegCheckpoint>();

        public void Capture()
        {
            if (legs != null)
                foreach (var leg in legs) leg?.replay?.Capture();
        }

        public void Normalize()
        {
            // Unity JSON materializes null inline objects; presence flags distinguish them from genuine checkpoints.
            if (legs == null) return;
            foreach (var leg in legs)
            {
                if (leg == null) continue;
                if (!leg.hasReplay) leg.replay = null;
                else if (leg.replay == null) leg.replay = new RunReplay { version = 0 };
            }
        }
    }
}
