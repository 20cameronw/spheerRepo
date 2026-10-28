using System;
using System.Collections.Generic;
using Frontier.Universe;

namespace Frontier.Economy
{
    /// <summary>
    /// Everything the Frontier Score reads, as plain numbers. There is deliberately no premium-currency, purchase or shard field here: the
    /// score cannot be bought (a test inspects this type by reflection).
    /// </summary>
    public sealed class ScoreInputs
    {
        public double maxDistanceFromHomeLy;
        /// <summary>Landed worlds per planet tier (index = tier).</summary>
        public int[] worldsLandedByTier = new int[0];
        /// <summary>Level and tier of every placed structure (parallel arrays).</summary>
        public int[] structureLevels = new int[0];
        public int[] structureTiers = new int[0];
        public int upgradeLevelsTotal;
        /// <summary>Tool tier purchases (tier - 1 summed over the six ladders).</summary>
        public int toolTierPurchases;
        /// <summary>Per world that has seen a wave: its danger and how many waves were cleared there (parallel arrays).</summary>
        public double[] waveWorldDanger = new double[0];
        public int[] waveWorldCleared = new int[0];
        public int resourcesDiscovered;
        public int recipesCrafted;
        public int megaprojectStages;
        public int ascensionRank;
    }

    /// <summary>
    /// The score weights as data (a Tuning section on the server, a ScoreFormulaSO in the project). The defaults are the plan 8.1 formula with the
    /// weights retuned against the simulator so that the first hour is worth 8-10 k, the Interstellar era entry about 70 k, a finished hardcore
    /// profile 1 M and a long-playing regular profile 100 k in its mid game (see reports/ECONOMY.md).
    /// </summary>
    [Serializable]
    public sealed class ScoreFormula
    {
        public double reachCoefficient = 700.0;
        public double reachDivisorLy = 10.0;
        public double worldBase = 130.0;
        public int worldFullValueCount = 500;
        public double worldExcessShare = 0.20;
        public double industryBase = 50.0;
        public double industryExponent = 1.6;
        public double techPerUpgradeLevel = 300.0;
        public double techPerToolTier = 800.0;
        public double defenseBase = 12.0;
        public int defenseWaveCap = 25;
        public double atlasPerResource = 100.0;
        public double atlasPerRecipe = 80.0;
        public double atlasPerMegaStage = 28000.0;
        public double ascensionStep = 0.10;

        public static void RegisterKeys(Tuning t)
        {
            t.Register("score", "score.reach_coefficient", 700, 0, 100000, "Reach = floor(coefficient x sqrt(maxDistanceLy / divisor)).");
            t.Register("score", "score.reach_divisor_ly", 10, 0.1, 1000, "Reach distance divisor in light years.");
            t.Register("score", "score.world_base", 130, 0, 10000, "Worlds: each landed world is worth base x (1 + tier).");
            t.Register("score", "score.world_full_count", 500, 1, 100000, "Worlds counted at full value (best tiers first); the rest count at the excess share.");
            t.Register("score", "score.world_excess_share", 0.2, 0, 1, "Share of value for landed worlds beyond the full count.");
            t.Register("score", "score.industry_base", 50, 0, 10000, "Industry: each structure is worth base x level^exponent x (1 + structure tier).");
            t.Register("score", "score.industry_exponent", 1.6, 0.5, 3, "Industry level exponent.");
            t.Register("score", "score.tech_per_upgrade_level", 300, 0, 100000, "Tech: points per ship/defense/logistics upgrade level.");
            t.Register("score", "score.tech_per_tool_tier", 800, 0, 100000, "Tech: points per tool tier purchased.");
            t.Register("score", "score.defense_base", 12, 0, 1000, "Defense: base x sqrt(danger) per cleared wave.");
            t.Register("score", "score.defense_wave_cap", 25, 1, 10000, "Anti-farm: waves beyond this count on one world add nothing.");
            t.Register("score", "score.atlas_per_resource", 100, 0, 100000, "Atlas: points per resource discovered.");
            t.Register("score", "score.atlas_per_recipe", 80, 0, 100000, "Atlas: points per recipe crafted.");
            t.Register("score", "score.atlas_per_mega_stage", 28000, 0, 1000000, "Atlas: points per megaproject stage completed.");
            t.Register("score", "score.ascension_step", 0.10, 0, 1, "Final multiplier = 1 + step x ascension rank.");
        }

        public static ScoreFormula From(Tuning t)
        {
            return new ScoreFormula
            {
                reachCoefficient = t.D("score.reach_coefficient"), reachDivisorLy = t.D("score.reach_divisor_ly"), worldBase = t.D("score.world_base"),
                worldFullValueCount = t.I("score.world_full_count"), worldExcessShare = t.D("score.world_excess_share"), industryBase = t.D("score.industry_base"),
                industryExponent = t.D("score.industry_exponent"), techPerUpgradeLevel = t.D("score.tech_per_upgrade_level"), techPerToolTier = t.D("score.tech_per_tool_tier"),
                defenseBase = t.D("score.defense_base"), defenseWaveCap = t.I("score.defense_wave_cap"), atlasPerResource = t.D("score.atlas_per_resource"),
                atlasPerRecipe = t.D("score.atlas_per_recipe"), atlasPerMegaStage = t.D("score.atlas_per_mega_stage"), ascensionStep = t.D("score.ascension_step")
            };
        }
    }

    public struct ScoreBreakdown
    {
        public double reach, worlds, industry, tech, defense, atlas, multiplier;
        public long total;
    }

    /// <summary>The Frontier Score: monotone in every input, capped against farming, deterministic.</summary>
    public static class ScoreCalculator
    {
        public const int WorldTierBucketCount = 24;

        public static ScoreBreakdown Compute(ScoreInputs s, ScoreFormula f)
        {
            var b = new ScoreBreakdown();
            double dist = s.maxDistanceFromHomeLy < 0.0 ? 0.0 : s.maxDistanceFromHomeLy;
            b.reach = Math.Floor(f.reachCoefficient * Math.Sqrt(dist / f.reachDivisorLy));

            // Worlds: the best worlds count in full up to the full-value count, the remainder at the excess share.
            int remainingFull = f.worldFullValueCount;
            double worlds = 0.0;
            for (int tier = s.worldsLandedByTier.Length - 1; tier >= 0; tier--)
            {
                int n = s.worldsLandedByTier[tier];
                if (n <= 0) continue;
                int full = Math.Min(n, remainingFull);
                remainingFull -= full;
                double each = f.worldBase * (1 + tier);
                worlds += full * each + (n - full) * each * f.worldExcessShare;
            }
            b.worlds = worlds;

            double industry = 0.0;
            for (int i = 0; i < s.structureLevels.Length; i++)
            {
                int level = s.structureLevels[i];
                if (level <= 0) continue;
                industry += f.industryBase * DetMath.Pow(level, f.industryExponent) * (1 + s.structureTiers[i]);
            }
            b.industry = industry;

            b.tech = f.techPerUpgradeLevel * s.upgradeLevelsTotal + f.techPerToolTier * s.toolTierPurchases;

            // Defense: n waves on a world pay base x sqrt(danger) each for the first cap waves and nothing after (n x min(1, cap / n) = min(n, cap)).
            double defense = 0.0;
            for (int i = 0; i < s.waveWorldDanger.Length; i++)
                defense += f.defenseBase * Math.Sqrt(Math.Max(0.0, s.waveWorldDanger[i])) * Math.Min(s.waveWorldCleared[i], f.defenseWaveCap);
            b.defense = defense;

            b.atlas = f.atlasPerResource * s.resourcesDiscovered + f.atlasPerRecipe * s.recipesCrafted + f.atlasPerMegaStage * s.megaprojectStages;
            b.multiplier = 1.0 + f.ascensionStep * s.ascensionRank;
            b.total = (long)Math.Floor((b.reach + b.worlds + b.industry + b.tech + b.defense + b.atlas) * b.multiplier);
            return b;
        }
    }
}
