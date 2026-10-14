using System;
using System.Collections.Generic;
using Frontier.Universe;

namespace Frontier.Economy
{
    /// <summary>One week of a simulated player's shard income.</summary>
    public sealed class ShardWeekRow
    {
        public int week;
        public int level;
        public ShardEarnings earned;
        public long cumulative;
    }

    /// <summary>Totals the shard model reports for one simulated player.</summary>
    public sealed class ShardSummary
    {
        public readonly List<ShardWeekRow> weeks = new List<ShardWeekRow>();
        public long totalEarned;
        public double meanPerWeek, medianPerWeek;
        /// <summary>Shards that skipping every ordinary build/upgrade job of the timeline would cost.</summary>
        public long skipAllOrdinaryJobs;
        /// <summary>Shards that skipping every megaproject stage timer would cost.</summary>
        public long skipAllMegaTimers;
        public int ordinaryJobs, megaTimers;
    }

    /// <summary>The faucet side of the shard economy, replayed over a simulated timeline (the sim itself never spends or earns shards).</summary>
    public static class ShardEconomy
    {
        /// <summary>
        /// Weekly income of a free player: level-ups, Atlas firsts, achievements (the sim's milestones), the daily streak, the weekly board,
        /// obstacle pods and, when <paramref name="adShare"/> is above 0, opt-in rewarded ads. Players in the sim log in every day.
        /// </summary>
        public static ShardSummary FreePlayer(EconomyCatalog cat, SimResult r, double adShare, double obstaclesPerBuild)
        {
            var sum = new ShardSummary();
            var ledger = new ShardLedger(cat);
            int weeks = (int)Math.Floor(r.horizonDays / 7.0);
            SimSample prev = null;
            long cumulative = 0;
            var totals = new List<double>();
            for (int w = 1; w <= weeks; w++)
            {
                SimSample s = LastAtOrBefore(r, 7.0 * w);
                if (s == null) continue;
                var wk = new ShardWeek
                {
                    levelFrom = prev == null ? 1 : prev.level,
                    levelTo = s.level,
                    newResources = s.discovered - (prev == null ? 0 : prev.discovered),
                    newRecipes = s.crafted - (prev == null ? 0 : prev.crafted),
                    newStructureKinds = s.structureKinds - (prev == null ? 0 : prev.structureKinds),
                    daysPlayed = 7,
                    objectivesDone = cat.shards.weeklyObjectives,
                    obstaclesCleared = (int)Math.Round((s.buildJobs - (prev == null ? 0 : prev.buildJobs)) * obstaclesPerBuild),
                    obstacleTier = s.reachTier,
                    boardEntered = true,
                    adShare = adShare
                };
                CountAchievements(r, 7.0 * (w - 1), 7.0 * w, wk);
                ShardEarnings e = ledger.Earn(cat.shards, wk);
                cumulative += e.Total;
                sum.weeks.Add(new ShardWeekRow { week = w, level = s.level, earned = e, cumulative = cumulative });
                totals.Add(e.Total);
                prev = s;
            }
            sum.totalEarned = cumulative;
            if (totals.Count > 0)
            {
                double acc = 0.0;
                for (int i = 0; i < totals.Count; i++) acc += totals[i];
                sum.meanPerWeek = acc / totals.Count;
                var sorted = new List<double>(totals);
                sorted.Sort();
                sum.medianPerWeek = sorted[sorted.Count / 2];
            }
            for (int i = 0; i < r.jobSeconds.Count; i++) sum.skipAllOrdinaryJobs += ShardCurve.SkipCost(cat.shards, r.jobSeconds[i]);
            for (int i = 0; i < r.megaSeconds.Count; i++) sum.skipAllMegaTimers += ShardCurve.SkipCost(cat.shards, r.megaSeconds[i]);
            sum.ordinaryJobs = r.jobSeconds.Count;
            sum.megaTimers = r.megaSeconds.Count;
            return sum;
        }

        static SimSample LastAtOrBefore(SimResult r, double day)
        {
            SimSample best = null;
            for (int i = 0; i < r.samples.Count; i++)
            {
                if (r.samples[i].days > day + 1e-9) break;
                best = r.samples[i];
            }
            return best;
        }

        /// <summary>The sim's milestones are the achievements: an era entry is large, a new reach tier medium, everything else small.</summary>
        static void CountAchievements(SimResult r, double fromDay, double toDay, ShardWeek wk)
        {
            for (int i = 0; i < r.milestoneOrder.Count; i++)
            {
                string id = r.milestoneOrder[i];
                if (id.StartsWith("goal_", StringComparison.Ordinal) || id == "ladder_complete") continue;
                double d = r.milestones[id] / 86400.0;
                if (d <= fromDay || d > toDay) continue;
                if (id.StartsWith("era_", StringComparison.Ordinal)) wk.achievementsLarge++;
                else if (id.StartsWith("reach_tier_", StringComparison.Ordinal)) wk.achievementsMedium++;
                else wk.achievementsSmall++;
            }
        }
    }
}
