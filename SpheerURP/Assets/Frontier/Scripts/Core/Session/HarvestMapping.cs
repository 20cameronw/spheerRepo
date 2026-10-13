using Frontier.Universe;

namespace Frontier
{
    /// <summary>
    /// The one place that decides which mini-game plays for a harvest method of the universe generator. The first three methods map
    /// one to one; the new methods borrow the closest existing mini-game until they get their own.
    /// </summary>
    public static class HarvestMapping
    {
        // TODO(new mini-games): Siphon, Excavate, Salvage and Harvest will get dedicated mini-games (roadmap section 6.2). Until then
        // each plays the nearest existing one. Change the mapping here and nowhere else.
        public static MinigameKind ToMinigame(HarvestMethod method)
        {
            switch (method)
            {
                case HarvestMethod.Chop: return MinigameKind.Chop;
                case HarvestMethod.Mine: return MinigameKind.Mine;
                case HarvestMethod.Drill: return MinigameKind.Drill;
                case HarvestMethod.Siphon: return MinigameKind.Drill;
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
                default: return HarvestMethod.Mine;
            }
        }
    }
}
