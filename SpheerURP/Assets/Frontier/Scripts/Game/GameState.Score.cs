using System;
using System.Collections.Generic;
using Frontier.Economy;
using Frontier.Universe;

namespace Frontier
{
    public partial class GameState
    {
        public ScoreBreakdown FrontierScore
        {
            get
            {
                ScoreFormula formula = db.scoreFormula != null && db.scoreFormula.data != null
                    ? db.scoreFormula.data
                    : new ScoreFormula();
                return ScoreCalculator.Compute(BuildFrontierScoreInputs(), formula);
            }
        }

        ScoreInputs BuildFrontierScoreInputs()
        {
            var inputs = new ScoreInputs();
            var landedByTier = new int[ScoreCalculator.WorldTierBucketCount];
            double maxDistance = Math.Max(0.0, stats.maxDistanceFromHomeLy);
            var waveDangers = new List<double>();
            var waveCounts = new List<int>();

            foreach (PlanetDelta delta in world.All)
            {
                if (delta == null) continue;
                PlanetData visited = universe.Get(delta.planetId);
                if (visited == null) continue;

                if (delta.visits > 0)
                {
                    landedByTier[Math.Min(ScoreCalculator.WorldTierBucketCount - 1, Math.Max(0, visited.Tier))]++;
                    maxDistance = Math.Max(maxDistance, universe.DistanceFromHomeLy(visited));
                }

                if (delta.wavesCleared > 0)
                {
                    waveDangers.Add(Math.Max(0.0, visited.Danger));
                    waveCounts.Add(delta.wavesCleared);
                }
            }

            inputs.maxDistanceFromHomeLy = maxDistance;
            inputs.worldsLandedByTier = landedByTier;
            inputs.waveWorldDanger = waveDangers.ToArray();
            inputs.waveWorldCleared = waveCounts.ToArray();

            var structureLevels = new List<int>();
            var structureTiers = new List<int>();
            foreach (PlacedStructure structure in structures)
            {
                if (structure == null || structure.def == null || structure.level <= 0 || !IsOperational(structure))
                    continue;

                structureLevels.Add(structure.level);
                structureTiers.Add(structure.def.tier);
                if (structure.def.role != StructureRole.Megaproject) continue;

                inputs.megaprojectStages += structure.level;
                structureLevels.Add(structure.level * 2);
                structureTiers.Add(structure.def.tier);
            }
            inputs.structureLevels = structureLevels.ToArray();
            inputs.structureTiers = structureTiers.ToArray();

            foreach (KeyValuePair<string, int> upgrade in progression.Upgrades)
                inputs.upgradeLevelsTotal += Math.Max(0, upgrade.Value);

            if (db.toolLadders != null)
            {
                foreach (ToolLadderSO ladder in db.toolLadders)
                    inputs.toolTierPurchases += Math.Max(0, ToolTier(ladder) - 1);
            }

            inputs.resourcesDiscovered = AtlasDiscoveredCount(AtlasResourceType);
            inputs.recipesCrafted = AtlasDiscoveredCount(AtlasRecipeType);
            return inputs;
        }
    }
}
