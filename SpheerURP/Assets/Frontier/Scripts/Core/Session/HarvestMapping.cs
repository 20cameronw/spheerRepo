using Frontier.Universe;

namespace Frontier
{
    /// <summary>
    /// The one place that decides which mini-game plays for a harvest method of the universe generator.
    /// </summary>
    public static class HarvestMapping
    {
        public static MinigameKind ToMinigame(HarvestMethod method)
        {
            switch (method)
            {
                case HarvestMethod.Chop: return MinigameKind.Chop;
                case HarvestMethod.Mine: return MinigameKind.Mine;
                case HarvestMethod.Drill: return MinigameKind.Drill;
                case HarvestMethod.Siphon: return MinigameKind.Siphon;
                case HarvestMethod.Excavate: return MinigameKind.Mine;
                case HarvestMethod.Salvage: return MinigameKind.Mine;
                case HarvestMethod.Harvest: return MinigameKind.Chop;
                default: return MinigameKind.Mine;
            }
        }

        /// <summary>The harvest method of an authored gather spot (the first three methods are the old mini-games, value for value).</summary>
        public static HarvestMethod FromMinigame(MinigameKind kind)
        {
            switch (kind)
            {
                case MinigameKind.Chop: return HarvestMethod.Chop;
                case MinigameKind.Drill: return HarvestMethod.Drill;
                case MinigameKind.Siphon: return HarvestMethod.Siphon;
                default: return HarvestMethod.Mine;
            }
        }
    }
}
