namespace Frontier
{
    public static class FirstSessionObjectives
    {
        public const int Count = 6;

        public static int CurrentStep(GameState state)
        {
            if (state == null) return Count;

            PlanetData home = state.universe.Home;
            if (state.StructureOfKind(home, StructureKind.Lumber) == null) return 0;
            if (state.stats.harvestRunsCompleted <= 0) return 1;
            if (state.StructureOfKind(home, StructureKind.Warehouse) == null) return 2;
            if (state.AtlasDiscoveredCount("recipe") <= 0) return 3;
            if (state.stats.totalWavesCleared <= 0) return 4;
            if (state.stats.worldsLanded < 2) return 5;
            return Count;
        }
    }
}
