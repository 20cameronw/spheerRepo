using System;
using System.Collections.Generic;
using Frontier.Universe;

namespace Frontier.Economy
{
    /// <summary>
    /// Shards buy time, convenience, cosmetics and unlocked structures. They never convert into inventory resources.
    /// </summary>
    public enum ShardSpendKind { SkipTimer, BuilderSlot, Revive, Cosmetic, Structure }

    /// <summary>Where shards come from. Everything except Purchase is capped for a free player.</summary>
    public enum ShardEarnKind { LevelUp, AtlasFirst, Achievement, DailyStreak, RewardedAd, ObstaclePod, WeeklyBoard, Purchase }

    /// <summary>One consumable shard pack of the shop (the store owns the real price; the tier only orders the packs).</summary>
    [Serializable]
    public sealed class ShardPackDef
    {
        public string id;
        public int shards;
        /// <summary>Store price tier in dollars-equivalent (a guide for choosing the store product, never shown or hard-coded).</summary>
        public int priceTier;
    }

    /// <summary>Every number of the shard economy as plain data (built from the Tuning keys; becomes ShardRulesSO).</summary>
    [Serializable]
    public sealed class ShardRulesDef
    {
        // skip curve: shards = max(minSkipCost, ceil(skipMultiplier * minutes ^ skipExponent)), free when the wait is at most freeFinishSeconds
        public float skipMultiplier;
        public float skipExponent;
        public int minSkipCost;
        public int freeFinishSeconds;
        // builders: 1 at start, 2 through research, 3 and 4 through the Colony Hub or shards
        public int baseBuilders;
        public int maxBuilders;
        public int thirdBuilderShards;
        public int fourthBuilderShards;
        public int reviveRunShards;
        public int reviveWaveShards;
        public int cosmeticMinShards;
        public int cosmeticMaxShards;
        // faucets
        public int levelUpBase;
        public int levelUpEveryLevels;
        public int atlasResourceShards;
        public int atlasRecipeShards;
        public int atlasStructureShards;
        public int achievementSmall;
        public int achievementMedium;
        public int achievementLarge;
        public int[] streakDays;
        public int adReward;
        public int adsPerDay;
        public int boardParticipation;
        public int boardTop10Percent;
        public int boardTop1Percent;
        public int weeklyObjectives;
        public int weeklyObjectiveShards;
        public int podBase;
        public int podPerThreeTiers;
        public ShardPackDef[] packs;

        public static void RegisterKeys(Tuning t)
        {
            t.Register("shard", "shard.skip.multiplier", 1.2, 0.1, 20, "Skip price = ceil(multiplier x minutes ^ exponent) shards.");
            t.Register("shard", "shard.skip.exponent", 0.75, 0.3, 1, "Exponent of the skip price curve (below 1: long waits cost less per minute, so one big skip never costs more than its parts).");
            t.Register("shard", "shard.skip.min_cost", 1, 1, 50, "No paid skip is cheaper than this many shards.");
            t.Register("shard", "shard.skip.free_finish_seconds", 30, 0, 600, "A job with at most this long left finishes for free.");
            t.Register("shard", "shard.builder.max", 4, 2, 8, "Most builders a player can have, earned or bought.");
            t.Register("shard", "shard.builder.third_price", 250, 0, 5000, "Shards for the third builder (the Colony Hub gives it for free at level 3).");
            t.Register("shard", "shard.builder.fourth_price", 600, 0, 10000, "Shards for the fourth builder (the Colony Hub gives it for free at level 7).");
            t.Register("shard", "shard.revive.run", 5, 0, 100, "Shards to revive in a Hyperspace Run.");
            t.Register("shard", "shard.revive.wave", 8, 0, 100, "Shards to revive the reactor in a called wave.");
            t.Register("shard", "shard.cosmetic.min", 120, 0, 5000, "Cheapest cosmetic in shards.");
            t.Register("shard", "shard.cosmetic.max", 1200, 0, 50000, "Priciest regular cosmetic in shards.");
            t.Register("shard", "shard.earn.levelup_base", 3, 0, 50, "Shards for a level-up (plus one per full block of levels).");
            t.Register("shard", "shard.earn.levelup_block", 15, 1, 1000, "Levels per block of the level-up bonus.");
            t.Register("shard", "shard.earn.atlas_resource", 3, 0, 100, "First discovery of a resource.");
            t.Register("shard", "shard.earn.atlas_recipe", 2, 0, 100, "First craft of a recipe.");
            t.Register("shard", "shard.earn.atlas_structure", 1, 0, 100, "First build of a structure kind.");
            t.Register("shard", "shard.earn.achievement_small", 5, 0, 500, "Small achievement.");
            t.Register("shard", "shard.earn.achievement_medium", 10, 0, 500, "Medium achievement (new reach tier).");
            t.Register("shard", "shard.earn.achievement_large", 25, 0, 500, "Large achievement (era entry).");
            double[] streak = { 2, 2, 3, 3, 4, 4, 12 };
            for (int i = 0; i < 7; i++) t.Register("shard", "shard.earn.streak_day" + (i + 1), streak[i], 0, 100, "Daily login streak reward on day " + (i + 1) + " of the week.");
            t.Register("shard", "shard.earn.ad_reward", 2, 0, 50, "Shards for one opt-in rewarded ad (Free Shards placement).");
            t.Register("shard", "shard.earn.ads_per_day", 3, 0, 20, "Daily cap of shard ads.");
            t.Register("shard", "shard.earn.board_participation", 20, 0, 500, "Weekly board reward for an active week (the weekly Sector Challenge).");
            t.Register("shard", "shard.earn.board_top10", 50, 0, 1000, "Weekly board reward for the top 10 percent (instead of the participation reward).");
            t.Register("shard", "shard.earn.board_top1", 150, 0, 5000, "Weekly board reward for the top 1 percent (instead of the participation reward).");
            t.Register("shard", "shard.earn.weekly_objectives", 3, 0, 10, "Weekly objectives per week (recurring achievements).");
            t.Register("shard", "shard.earn.weekly_objective_reward", 5, 0, 100, "Shards per completed weekly objective.");
            t.Register("shard", "shard.earn.pod_base", 1, 0, 50, "Shards in an obstacle pod on a tier 0 world.");
            t.Register("shard", "shard.earn.pod_per_3_tiers", 1, 0, 50, "Extra shards in a pod for every 3 tiers of the world.");
            int[] packShards = { 80, 500, 1100, 2400, 6500 };
            int[] packTier = { 1, 5, 10, 20, 50 };
            for (int i = 0; i < 5; i++)
            {
                t.Register("shard", "shard.pack." + (i + 1) + ".shards", packShards[i], 1, 1000000, "Shards in consumable pack " + (i + 1) + ".");
                t.Register("shard", "shard.pack." + (i + 1) + ".price_tier", packTier[i], 1, 1000, "Store price tier (dollars) of pack " + (i + 1) + ", a guide only.");
            }
        }

        public static ShardRulesDef From(Tuning t)
        {
            var r = new ShardRulesDef
            {
                skipMultiplier = (float)t.D("shard.skip.multiplier"), skipExponent = (float)t.D("shard.skip.exponent"), minSkipCost = t.I("shard.skip.min_cost"),
                freeFinishSeconds = t.I("shard.skip.free_finish_seconds"), baseBuilders = t.I("start.builders"), maxBuilders = t.I("shard.builder.max"),
                thirdBuilderShards = t.I("shard.builder.third_price"), fourthBuilderShards = t.I("shard.builder.fourth_price"), reviveRunShards = t.I("shard.revive.run"),
                reviveWaveShards = t.I("shard.revive.wave"), cosmeticMinShards = t.I("shard.cosmetic.min"), cosmeticMaxShards = t.I("shard.cosmetic.max"),
                levelUpBase = t.I("shard.earn.levelup_base"), levelUpEveryLevels = t.I("shard.earn.levelup_block"), atlasResourceShards = t.I("shard.earn.atlas_resource"),
                atlasRecipeShards = t.I("shard.earn.atlas_recipe"), atlasStructureShards = t.I("shard.earn.atlas_structure"), achievementSmall = t.I("shard.earn.achievement_small"),
                achievementMedium = t.I("shard.earn.achievement_medium"), achievementLarge = t.I("shard.earn.achievement_large"), adReward = t.I("shard.earn.ad_reward"),
                adsPerDay = t.I("shard.earn.ads_per_day"), boardParticipation = t.I("shard.earn.board_participation"), boardTop10Percent = t.I("shard.earn.board_top10"),
                boardTop1Percent = t.I("shard.earn.board_top1"), weeklyObjectives = t.I("shard.earn.weekly_objectives"), weeklyObjectiveShards = t.I("shard.earn.weekly_objective_reward"),
                podBase = t.I("shard.earn.pod_base"), podPerThreeTiers = t.I("shard.earn.pod_per_3_tiers")
            };
            r.streakDays = new int[7];
            for (int i = 0; i < 7; i++) r.streakDays[i] = t.I("shard.earn.streak_day" + (i + 1));
            r.packs = new ShardPackDef[5];
            for (int i = 0; i < 5; i++) r.packs[i] = new ShardPackDef { id = "shards_" + (i + 1), shards = t.I("shard.pack." + (i + 1) + ".shards"), priceTier = t.I("shard.pack." + (i + 1) + ".price_tier") };
            return r;
        }
    }

    /// <summary>The price of skipping a wait: a pure function of the seconds left (never of the resources the job cost).</summary>
    public static class ShardCurve
    {
        public static int StructureCost(ShardRulesDef rules, double buildSeconds)
        {
            return (int)Math.Min(int.MaxValue, Math.Max(10L, 2L * SkipCost(rules, buildSeconds)));
        }

        /// <summary>Shards to finish a job that still has <paramref name="secondsLeft"/> to run; 0 when it is within the free-finish window.</summary>
        public static int SkipCost(ShardRulesDef r, double secondsLeft)
        {
            if (!(secondsLeft > r.freeFinishSeconds)) return 0;
            double minutes = secondsLeft / 60.0;
            double raw = Math.Ceiling(r.skipMultiplier * DetMath.Pow(minutes, r.skipExponent) - 1e-9);
            int cost = raw > 2000000000.0 ? 2000000000 : (int)raw;
            return cost < r.minSkipCost ? r.minSkipCost : cost;
        }
    }

    /// <summary>Builders: earned through progression, or bought with shards (never above the cap).</summary>
    public static class BuilderPlan
    {
        /// <summary>Builders owned: base + research + Colony Hub bonus + bought slots, never above the cap.</summary>
        public static int Count(ShardRulesDef r, bool fieldEngineeringDone, int hubBonusBuilders, int boughtBuilders)
        {
            int n = r.baseBuilders + (fieldEngineeringDone ? 1 : 0) + Math.Max(0, hubBonusBuilders) + Math.Max(0, boughtBuilders);
            return Math.Min(r.maxBuilders, n);
        }

        /// <summary>Shards for the next bought builder when the player owns <paramref name="owned"/> (-1 when the cap is reached or the slot is not for sale).</summary>
        public static int NextBuilderPrice(ShardRulesDef r, int owned)
        {
            if (owned >= r.maxBuilders) return -1;
            if (owned == 2) return r.thirdBuilderShards;
            if (owned == 3) return r.fourthBuilderShards;
            return -1;
        }
    }

    /// <summary>One week of a player's activity as the shard ledger sees it (plain numbers, so a server can recompute it).</summary>
    public sealed class ShardWeek
    {
        public int levelFrom, levelTo;
        public int newResources, newRecipes, newStructureKinds;
        public int achievementsSmall, achievementsMedium, achievementsLarge;
        /// <summary>Weekly objectives completed this week (recurring achievements, capped by the rules).</summary>
        public int objectivesDone;
        public int daysPlayed;
        public int obstaclesCleared;
        public double obstacleTier;
        public bool boardEntered;
        /// <summary>0..1: the share of the daily ad cap the player takes (0 = never watches, the free-player baseline).</summary>
        public double adShare;
    }

    public struct ShardEarnings
    {
        public int levelUp, atlas, achievements, streak, ads, pods, board;
        public int Total { get { return levelUp + atlas + achievements + streak + ads + pods + board; } }
    }

    /// <summary>
    /// A player's lifetime shard counters. The one-time faucets (Atlas firsts) can never pay more than the catalog holds, which is the anti-farm
    /// cap; the repeating ones (streak, ads, board, pods) are capped per week by their own rules.
    /// </summary>
    public sealed class ShardLedger
    {
        public int resourcesPaid, recipesPaid, structuresPaid;
        public long totalEarned;
        readonly int _resourceCap, _recipeCap, _structureCap;
        readonly double _podChance;
        double _podCarry;

        public ShardLedger(EconomyCatalog cat)
        {
            _resourceCap = cat.resources.Length;
            _recipeCap = cat.recipes.Length;
            _structureCap = cat.structures.Length;
            _podChance = cat.obstacleRules.shardPodChance;
        }

        /// <summary>Shards of a level-up into <paramref name="level"/> (level 2 is the first).</summary>
        public static int LevelUpShards(ShardRulesDef r, int level)
        {
            return r.levelUpBase + (level / Math.Max(1, r.levelUpEveryLevels));
        }

        public static int PodShards(ShardRulesDef r, int tier)
        {
            return r.podBase + (Math.Max(0, tier) / 3) * r.podPerThreeTiers;
        }

        /// <summary>Daily streak shards for a week with the given number of consecutive login days (counted from day 1 of the cycle).</summary>
        public static int StreakShards(ShardRulesDef r, int consecutiveDays)
        {
            int sum = 0;
            int n = Math.Min(7, Math.Max(0, consecutiveDays));
            for (int i = 0; i < n; i++) sum += r.streakDays[i];
            return sum;
        }

        /// <summary>Applies one week of activity and returns what was paid (one-time faucets are clipped to what is still unpaid).</summary>
        public ShardEarnings Earn(ShardRulesDef r, ShardWeek w)
        {
            var e = new ShardEarnings();
            for (int l = w.levelFrom + 1; l <= w.levelTo; l++) e.levelUp += LevelUpShards(r, l);
            int res = Math.Min(Math.Max(0, w.newResources), _resourceCap - resourcesPaid);
            int rec = Math.Min(Math.Max(0, w.newRecipes), _recipeCap - recipesPaid);
            int str = Math.Min(Math.Max(0, w.newStructureKinds), _structureCap - structuresPaid);
            resourcesPaid += res;
            recipesPaid += rec;
            structuresPaid += str;
            e.atlas = res * r.atlasResourceShards + rec * r.atlasRecipeShards + str * r.atlasStructureShards;
            e.achievements = Math.Max(0, w.achievementsSmall) * r.achievementSmall + Math.Max(0, w.achievementsMedium) * r.achievementMedium + Math.Max(0, w.achievementsLarge) * r.achievementLarge
                + Math.Min(Math.Max(0, w.objectivesDone), r.weeklyObjectives) * r.weeklyObjectiveShards;
            e.streak = StreakShards(r, w.daysPlayed);
            double share = Math.Min(1.0, Math.Max(0.0, w.adShare));
            e.ads = (int)Math.Floor(share * r.adsPerDay * r.adReward * Math.Min(7, Math.Max(0, w.daysPlayed)) + 1e-9);
            // pods: shardPodChance of the cleared obstacles in expectation (the exact draw is per obstacle in ObstacleRules; the ledger carries the fraction)
            _podCarry += Math.Max(0, w.obstaclesCleared) * _podChance * PodShards(r, (int)Math.Round(w.obstacleTier));
            e.pods = (int)Math.Floor(_podCarry + 1e-9);
            _podCarry -= e.pods;
            e.board = w.boardEntered ? r.boardParticipation : 0;
            totalEarned += e.Total;
            return e;
        }
    }
}
