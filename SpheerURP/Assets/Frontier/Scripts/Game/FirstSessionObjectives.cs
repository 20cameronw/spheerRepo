namespace Frontier
{
    public static class FirstSessionObjectives
    {
        public const int Count = 8;

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
            bool researched = false;
            foreach (var id in state.ResearchedIds) { researched = true; break; }
            if (!researched) return 6;
            foreach (var upgrade in state.db.upgrades)
                if (state.Level(upgrade) > 0) return Count;
            foreach (var tool in state.ToolTiers)
                if (tool.Value > 1) return Count;
            return 7;
        }
    }
}
