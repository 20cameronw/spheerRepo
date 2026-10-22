using System;
using Frontier.Universe;

namespace Frontier.Economy
{
    [Serializable]
    public sealed class AscensionRules
    {
        public string requiredResearch = "ascension_protocol";
        public int requiredProjectStage = 3;
        public int legacyPointsPerRank = 1;
        public float harvestBonusPerPoint = 0.05f;
        public float maxHarvestBonus = 0.5f;

        public bool IsValid
        {
            get
            {
                return !string.IsNullOrEmpty(requiredResearch) && requiredProjectStage >= 3 && requiredProjectStage <= 5 &&
                    legacyPointsPerRank >= 1 && legacyPointsPerRank <= 100 &&
                    !float.IsNaN(harvestBonusPerPoint) && !float.IsInfinity(harvestBonusPerPoint) &&
                    !float.IsNaN(maxHarvestBonus) && !float.IsInfinity(maxHarvestBonus) &&
                    harvestBonusPerPoint >= 0 && harvestBonusPerPoint <= 0.1f && maxHarvestBonus >= 0 && maxHarvestBonus <= 0.5f;
            }
        }

        public float HarvestMultiplier(int legacyPoints)
        {
            if (!IsValid) throw new InvalidOperationException("Invalid Ascension rules.");
            return 1f + (float)Math.Min(maxHarvestBonus, Math.Max(0, (double)legacyPoints) * harvestBonusPerPoint);
        }

        public static ulong NextSeed(ulong previous, int rank)
        {
            if (rank < 1) throw new ArgumentOutOfRangeException(nameof(rank));
            ulong seed = Hash.Combine(previous, (ulong)rank, 0x415343454E53494FUL);
            return seed == previous ? unchecked(previous + Hash.Golden) : seed;
        }
    }
}
