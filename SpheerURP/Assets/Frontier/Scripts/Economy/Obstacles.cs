using System;
using System.Collections.Generic;
using Frontier.Universe;

namespace Frontier.Economy
{
    /// <summary>The cost and salvage of one obstacle kind on a world of one tier (before the planet size multiplier).</summary>
    [Serializable]
    public sealed class ObstacleTierCost
    {
        public int tier;
        public ItemAmount[] cost;
        public ItemAmount reward;
    }

    /// <summary>One kind of clutter that blocks a building pad. The art side picks the sprite family from the id.</summary>
    [Serializable]
    public sealed class ObstacleKindDef
    {
        public string id;
        public string displayName;
        public string description;
        /// <summary>Biomes it grows on (Frontier.Universe.Biome names separated by ';').</summary>
        public string biomes;
        /// <summary>The same set as a bit mask (see BiomeMask).</summary>
        public uint biomeMask;
        /// <summary>Only on worlds with the Void hazard.</summary>
        public bool needsVoid;
        public int minTier;
        /// <summary>Spawn weight among the kinds a world allows.</summary>
        public float weight;
        public float costMult;
        public float timeMult;
        public string rewardResource;
        public string iconConcept;
        /// <summary>Cost and salvage per world tier 0..maxTier (higher tiers use the last entry).</summary>
        public ObstacleTierCost[] byTier;
    }

    /// <summary>Everything that is not a kind: density, free pads, pod chance, size multipliers and the per-tier timers and XP.</summary>
    [Serializable]
    public sealed class ObstacleRulesDef
    {
        /// <summary>Share of the pads blocked on an ordinary world (before the per-planet jitter).</summary>
        public float density;
        public float densityJitter;
        /// <summary>Share blocked on the home world: it starts mostly clear.</summary>
        public float homeDensity;
        /// <summary>The first pads of every world are never blocked.</summary>
        public int freePads;
        public float shardPodChance;
        /// <summary>Building pads per size class (Dwarf..Colossal).</summary>
        public int[] padsPerSize;
        public float[] sizeCostMult;
        /// <summary>Share of the clearing cost (in scrap-equivalents) that comes back as salvage; below 1, so clearing never mints value.</summary>
        public float rewardShare;
        public float xpPerValue;
        public float valueBase;
        public float valueGrowth;
        public int maxTier;
        /// <summary>Builder seconds per world tier 0..maxTier at time multiplier 1.</summary>
        public int[] clearSeconds;
        public int timeCapSeconds;
    }

    /// <summary>One generated obstacle on a pad.</summary>
    public struct ObstacleInstance
    {
        public int pad;
        public int kind;
        public ItemAmount[] cost;
        /// <summary>Builder seconds (0 = clears on the spot).</summary>
        public int seconds;
        public int xp;
        public ItemAmount reward;
        /// <summary>Shards inside when this is a shard pod (0 = a plain obstacle).</summary>
        public int podShards;
        /// <summary>Shards to clear it instantly (the skip price of its timer; 0 when it has no timer).</summary>
        public int instantShards;
    }

    /// <summary>Deterministic clutter: the same planet in the same universe always has the same obstacles (only the cleared set is saved).</summary>
    public static class ObstacleRules
    {
        public static void RegisterKeys(Tuning t)
        {
            t.Register("obstacle", "obstacle.density", 0.30, 0, 0.8, "Share of the building pads blocked on an ordinary world.");
            t.Register("obstacle", "obstacle.density_jitter", 0.10, 0, 0.3, "Per-planet random swing of the density (plus or minus).");
            t.Register("obstacle", "obstacle.home_density", 0.08, 0, 0.5, "Share of pads blocked on the home world (it starts mostly clear).");
            t.Register("obstacle", "obstacle.free_pads", 4, 0, 12, "The first pads of every world never carry an obstacle.");
            t.Register("obstacle", "obstacle.pod_chance", 0.05, 0, 0.5, "Chance that an obstacle is a shard pod.");
            t.Register("obstacle", "obstacle.value_base", 8, 0.5, 1000, "Clearing cost in scrap-equivalents on a tier 0 world (size multiplier 1).");
            t.Register("obstacle", "obstacle.value_growth", 2.0, 1, 10, "Clearing cost growth per world tier.");
            t.Register("obstacle", "obstacle.cap_base_slots", 3, 1, 100, "No cost line of a clearing may exceed this many slots on a tier 0 world (the value the cap cuts moves to denser lines).");
            t.Register("obstacle", "obstacle.cap_slots_per_tier", 1.2, 0, 20, "Extra slots a clearing cost line may take for every world tier.");
            t.Register("obstacle", "obstacle.cap_total_base_slots", 14, 2, 200, "Most slots of items a whole clearing may ask for on a tier 0 world.");
            t.Register("obstacle", "obstacle.cap_total_slots_per_tier", 2.5, 0, 50, "Extra slots of items a whole clearing may ask for per world tier.");
            t.Register("obstacle", "obstacle.reward_share", 0.40, 0, 0.9, "Share of the clearing cost that comes back as salvage (must stay below 1).");
            t.Register("obstacle", "obstacle.xp_per_value", 0.05, 0, 5, "XP per scrap-equivalent of clearing cost.");
            t.Register("obstacle", "obstacle.time_cap_seconds", 18000, 60, 86400, "No obstacle takes a builder longer than this.");
            int[] pads = { 12, 16, 24, 36, 48, 64 };
            double[] mult = { 0.7, 0.85, 1.0, 1.25, 1.5, 1.8 };
            for (int i = 0; i < 6; i++)
            {
                t.Register("obstacle", "obstacle.pads." + i, pads[i], 4, 200, "Building pads of size class " + i + " (Dwarf, Small, Medium, Large, Giant, Colossal).");
                t.Register("obstacle", "obstacle.size_mult." + i, mult[i], 0.1, 10, "Clearing cost multiplier of size class " + i + ".");
            }
            double[] secs = { 0, 6, 20, 60, 180, 480, 1200, 2400, 4200, 6300, 9000, 12600, 18000 };
            for (int i = 0; i < secs.Length; i++) t.Register("obstacle", "obstacle.time.t" + i, secs[i], 0, 86400, "Builder seconds to clear an obstacle on a tier " + i + " world.");
        }

        public const int MaxTier = 12;

        public static ObstacleRulesDef RulesFrom(Tuning t)
        {
            var r = new ObstacleRulesDef
            {
                density = (float)t.D("obstacle.density"), densityJitter = (float)t.D("obstacle.density_jitter"), homeDensity = (float)t.D("obstacle.home_density"),
                freePads = t.I("obstacle.free_pads"), shardPodChance = (float)t.D("obstacle.pod_chance"), rewardShare = (float)t.D("obstacle.reward_share"),
                xpPerValue = (float)t.D("obstacle.xp_per_value"), valueBase = (float)t.D("obstacle.value_base"), valueGrowth = (float)t.D("obstacle.value_growth"),
                maxTier = MaxTier, timeCapSeconds = t.I("obstacle.time_cap_seconds")
            };
            r.padsPerSize = new int[6];
            r.sizeCostMult = new float[6];
            for (int i = 0; i < 6; i++) { r.padsPerSize[i] = t.I("obstacle.pads." + i); r.sizeCostMult[i] = (float)t.D("obstacle.size_mult." + i); }
            r.clearSeconds = new int[MaxTier + 1];
            for (int i = 0; i <= MaxTier; i++) r.clearSeconds[i] = t.I("obstacle.time.t" + i);
            return r;
        }

        /// <summary>Expected share of pads blocked on a world (before the jitter).</summary>
        public static double Density(ObstacleRulesDef r, bool home) { return home ? r.homeDensity : r.density; }

        /// <summary>Builder seconds of a kind on a world tier (capped).</summary>
        public static int ClearSeconds(ObstacleRulesDef r, ObstacleKindDef k, int tier)
        {
            // double arithmetic on purpose: int x float would be evaluated in single precision on some runtimes (Mono differs from CoreCLR)
            double s = (double)r.clearSeconds[Math.Max(0, Math.Min(r.maxTier, tier))] * (double)k.timeMult;
            if (s > r.timeCapSeconds) s = r.timeCapSeconds;
            return CostMath.NiceSeconds(s);
        }

        /// <summary>One cost line after the Colony Hub's obstacle discount (its capacity column, in percent); never below 1, never more than 90 percent off.</summary>
        public static int Discounted(int amount, double discountPercent)
        {
            double pct = Math.Min(90.0, Math.Max(0.0, discountPercent));
            return (int)Math.Max(1.0, Math.Round(amount * (1.0 - pct / 100.0)));
        }

        /// <summary>
        /// The clutter of one world. <paramref name="pads"/> is the world's building slot count (12/16/24/36/48/64); gas giants carry none.
        /// Pure: same arguments, same result.
        /// </summary>
        public static ObstacleInstance[] Generate(EconomyCatalog cat, ulong universeSeed, ulong planetId, int pads, SizeClass size, Biome biome, Hazards hazards, int tier, bool home)
        {
            ObstacleRulesDef r = cat.obstacleRules;
            if (biome == Biome.GasGiant || pads <= r.freePads) return new ObstacleInstance[0];
            var rng = new Rng(Hash.Stream(universeSeed, planetId, "obstacles"));
            double density = home ? r.homeDensity : r.density + (rng.NextDouble() * 2.0 - 1.0) * r.densityJitter;
            if (density < 0.0) density = 0.0;
            int candidates = pads - r.freePads;
            int count = (int)Math.Floor(pads * density + rng.NextDouble());
            if (count > candidates) count = candidates;
            if (count < 0) count = 0;
            var order = new int[candidates];
            for (int i = 0; i < candidates; i++) order[i] = r.freePads + i;
            for (int i = 0; i < count; i++)
            {
                int j = i + rng.NextInt(candidates - i);
                int swap = order[i]; order[i] = order[j]; order[j] = swap;
            }
            int t = Math.Max(0, Math.Min(r.maxTier, tier));
            double sizeMult = r.sizeCostMult[Math.Max(0, Math.Min(5, (int)size))];
            double totalWeight = 0.0;
            for (int k = 0; k < cat.obstacles.Length; k++) if (Allowed(cat.obstacles[k], biome, hazards, tier)) totalWeight += cat.obstacles[k].weight;
            var list = new ObstacleInstance[totalWeight > 0.0 ? count : 0];
            for (int i = 0; i < list.Length; i++)
            {
                double pick = rng.NextDouble() * totalWeight;
                int kind = -1;
                for (int k = 0; k < cat.obstacles.Length; k++)
                {
                    if (!Allowed(cat.obstacles[k], biome, hazards, tier)) continue;
                    kind = k;
                    pick -= cat.obstacles[k].weight;
                    if (pick < 0.0) break;
                }
                ObstacleKindDef def = cat.obstacles[kind];
                ObstacleTierCost tc = def.byTier[t];
                var o = new ObstacleInstance { pad = order[i], kind = kind };
                o.cost = new ItemAmount[tc.cost.Length];
                double value = 0.0;
                for (int c = 0; c < tc.cost.Length; c++)
                {
                    int amount = (int)Math.Max(1.0, Math.Round((double)tc.cost[c].amount * sizeMult));
                    o.cost[c] = new ItemAmount(tc.cost[c].resourceId, amount);
                    value += (double)amount * (double)cat.Resource(tc.cost[c].resourceId).baseValue;
                }
                int rewardAmount = (int)Math.Max(1.0, Math.Round((double)tc.reward.amount * sizeMult));
                o.reward = new ItemAmount(tc.reward.resourceId, rewardAmount);
                o.seconds = ClearSeconds(r, def, t);
                o.xp = (int)Math.Max(1.0, Math.Round(value * (double)r.xpPerValue));
                o.instantShards = ShardCurve.SkipCost(cat.shards, o.seconds);
                bool pod = rng.Chance(r.shardPodChance);
                o.podShards = pod ? ShardLedger.PodShards(cat.shards, t) : 0;
                list[i] = o;
            }
            Array.Sort(list, (a, b) => a.pad.CompareTo(b.pad));
            return list;
        }

        static bool Allowed(ObstacleKindDef k, Biome biome, Hazards hazards, int tier)
        {
            if (tier < k.minTier) return false;
            if (!BiomeMask.Has(k.biomeMask, biome)) return false;
            if (k.needsVoid && (hazards & Hazards.Void) == 0) return false;
            return true;
        }
    }
}
