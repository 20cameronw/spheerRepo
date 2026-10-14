using System;
using System.Collections.Generic;
using Frontier.Universe;

namespace Frontier.Economy
{
    public static partial class CatalogBuilder
    {
        sealed class ObstacleSpec
        {
            public string id, name, desc, biomes, bundle, reward, icon;
            public int minTier;
            public double weight, costMult, timeMult;
            public bool needsVoid;
        }

        static ObstacleSpec Ob(string id, string name, string biomes, int minTier, double weight, string bundle, string reward, double costMult, double timeMult, string desc, string icon, bool needsVoid = false)
        {
            return new ObstacleSpec { id = id, name = name, biomes = biomes, minTier = minTier, weight = weight, bundle = bundle, reward = reward, costMult = costMult, timeMult = timeMult, desc = desc, icon = icon, needsVoid = needsVoid };
        }

        /// <summary>The clutter families. Each is bound to the biomes it grows on; the clearing cost is a bundle of resources the player already owns at that tier.</summary>
        static readonly ObstacleSpec[] ObstacleSpecs =
        {
            Ob("boulder", "Boulder", "Mineral;Desert;Obsidian;Shattered;Barren;Volcanic", 0, 3.0, "stone:1,scrap:0.5,steel:1@5,titanium_plate:1@7", "stone", 1.0, 1.0,
                "A weathered lump of rock sits where a pad should be. Break it up with picks and haul the pieces away.",
                "A lone rounded boulder with one flat cut face and a chipped notch in its crown."),
            Ob("dead_tree", "Dead Tree", "Lush;Fungal;Desert;Oceanic", 0, 3.0, "wood:1,scrap:0.5,planks:1@4,steel:1@6,titanium_plate:0.8@8", "wood", 1.0, 0.8,
                "The trunk is long dead and the roots still grip the pad. Fell it, split it, and the wood is yours.",
                "A bare forked trunk with three snapped branches and a pale split at the base."),
            Ob("wreckage", "Wreckage", "Lush;Mineral;Glacial;Volcanic;Fungal;Crystal;Obsidian;Shattered;Desert;Oceanic;Barren", 0, 2.0, "scrap:1,iron_plate:0.5@2,steel:1@5,circuit_board:0.6@7", "scrap", 1.1, 1.2,
                "Someone else's landing ended here. Cut the hull apart for scrap before it blocks anything else.",
                "A snapped hull fin canted on the ground, ragged edge up, with two rows of rivets."),
            Ob("fungal_growth", "Fungal Growth", "Fungal;Lush;Oceanic", 1, 2.0, "biomass:1,fuel:0.5@4,biocomposite:0.8@6,thermal_alloy:0.4@8", "biomass", 1.0, 1.0,
                "A shelf of spore-heavy fungus has taken the pad. Burn it back and collect what the rot left behind.",
                "A tilted stack of three mushroom shelves, each wider than the last, with spotted caps."),
            Ob("ice_spire", "Ice Spire", "Glacial;Oceanic", 2, 2.5, "ice:1,planks:0.5@2,steel:0.8@5,cryo_composite:0.5@7", "ice", 1.0, 1.1,
                "A spear of old ice has grown through the pad. Melt it carefully; the water is worth keeping.",
                "A jagged ice spear leaning off the vertical, with a split foot and a small frozen splash around it."),
            Ob("crystal_cluster", "Crystal Cluster", "Crystal;Shattered", 2, 2.5, "iron_plate:1,glass:0.6,optic_lens:0.5@6,quantum_chip:0.2@9", "crystal", 1.0, 1.1,
                "Raw crystal has burst through the ground. Cut it free before the whole pad starts to hum.",
                "A fan of five crystal shards of unequal height rising from one faceted base."),
            Ob("magma_crust", "Magma Crust", "Volcanic;Obsidian", 3, 2.5, "steel:1,titanium_plate:0.5@5,thermal_alloy:0.6@7", "stone", 1.0, 1.3,
                "A plate of cooled magma has fused over the pad, still ticking as it shrinks. Break the crust and clear the slag.",
                "A cracked dark slab with a glowing seam zigzagging corner to corner."),
            Ob("void_scar", "Void Scar", "Lush;Mineral;Glacial;Volcanic;Fungal;Crystal;Obsidian;Shattered;Desert;Oceanic;Barren", 6, 4.0, "steel:1,rad_shield_alloy:0.6@7,void_alloy:0.3@9", "steel", 1.0, 1.5,
                "Space itself is torn here. Seal the scar with shielding alloy before it spreads under the next pad.",
                "A ragged black rip in the ground with a violet lip and loose pebbles floating above it.", true)
        };

        static ObstacleKindDef[] BuildObstacles(Tuning t, Dictionary<string, double> price, Dictionary<string, int> stack, ObstacleRulesDef rules)
        {
            var list = new ObstacleKindDef[ObstacleSpecs.Length];
            for (int i = 0; i < list.Length; i++)
            {
                ObstacleSpec s = ObstacleSpecs[i];
                uint mask = 0;
                foreach (string b in s.biomes.Split(';')) mask |= BiomeMask.Of((Biome)Enum.Parse(typeof(Biome), b));
                var k = new ObstacleKindDef
                {
                    id = s.id, displayName = s.name, description = s.desc, biomes = s.biomes, biomeMask = mask, needsVoid = s.needsVoid, minTier = s.minTier,
                    weight = (float)s.weight, costMult = (float)s.costMult, timeMult = (float)s.timeMult, rewardResource = s.reward, iconConcept = s.icon
                };
                Bundle bundle = Bundle.Parse(s.bundle);
                k.byTier = new ObstacleTierCost[rules.maxTier + 1];
                for (int tier = 0; tier <= rules.maxTier; tier++)
                {
                    double value = rules.valueBase * DetMath.Pow(rules.valueGrowth, tier) * s.costMult;
                    var tc = new ObstacleTierCost { tier = tier };
                    tc.cost = bundle.Cost(value, tier + 1, price, stack, t.D("obstacle.cap_base_slots") + t.D("obstacle.cap_slots_per_tier") * tier);
                    // a clearing never fills more than a modest share of the bags: scale the whole line set down when it would
                    double slots = 0.0;
                    for (int c = 0; c < tc.cost.Length; c++) slots += Math.Ceiling((double)tc.cost[c].amount / stack[tc.cost[c].resourceId]);
                    double totalCap = t.D("obstacle.cap_total_base_slots") + t.D("obstacle.cap_total_slots_per_tier") * tier;
                    if (slots > totalCap)
                    {
                        double f = totalCap / slots;
                        for (int c = 0; c < tc.cost.Length; c++) tc.cost[c] = new ItemAmount(tc.cost[c].resourceId, (int)Math.Max(1.0, Math.Floor(tc.cost[c].amount * f)));
                    }
                    double rewardUnits = Math.Floor(rules.rewardShare * value / price[s.reward]);
                    double rewardCap = 6.0 * stack[s.reward];
                    tc.reward = new ItemAmount(s.reward, (int)Math.Max(1.0, Math.Min(rewardUnits, rewardCap)));
                    k.byTier[tier] = tc;
                }
                list[i] = k;
            }
            return list;
        }
    }
}
