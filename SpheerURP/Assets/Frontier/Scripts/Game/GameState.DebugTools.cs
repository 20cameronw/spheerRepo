#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;

namespace Frontier
{
    public partial class GameState
    {
        public void DebugSetShards(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            wallet.RemoveAll(entry => entry != null && entry.currency == ShardCurrency);
            AddShards(amount);
            Notify();
        }

        public void DebugSetXp(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            progression.SetXp(amount);
            Notify();
        }

        public void DebugSetResource(ResourceSO resource, int amount)
        {
            if (resource == null || db.FindResource(resource.id) != resource)
                throw new ArgumentException("Resource must belong to this database.", nameof(resource));
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            backpack.Set(resource.id, amount);
            Notify();
        }

        public void DebugSetResearch(ResearchNodeSO node, bool complete)
        {
            if (!ContainsResearch(node)) throw new ArgumentException("Research must belong to this database.", nameof(node));
            if (complete) researched.Add(node.data.id);
            else researched.Remove(node.data.id);
            Notify();
        }

        public void DebugSetToolTier(ToolLadderSO ladder, int tier)
        {
            ladder = ToolUpgradeLadder(ladder);
            if (ladder == null || ladder.data == null || Array.IndexOf(db.toolLadders, ladder) < 0 ||
                tier < 1 || tier > MaxToolTier(ladder)) throw new ArgumentOutOfRangeException(nameof(tier));
            toolTiers[ladder.data.id] = tier;
            if (IsMiningTool(ladder))
                foreach (var other in db.toolLadders)
                    if (IsMiningTool(other)) toolTiers[other.data.id] = tier;
            Notify();
        }

        public void DebugFinishAllJobs()
        {
            string now = UtcText.Format(clock.UtcNow);
            foreach (var structure in structures)
            {
                if (IsUnderConstruction(structure)) structure.record.buildEndUtc = now;
                if (IsUpgradeInProgress(structure)) structure.record.upgradeEndUtc = now;
            }
            foreach (var builder in builders)
                if (builder != null && !string.IsNullOrEmpty(builder.target)) builder.busyUntilUtc = now;
            foreach (var delta in world.All)
                foreach (var job in delta.obstacleJobs) job.endUtc = now;
            CompleteBuilderJobs();
            world.Touch();
            Notify();
        }

        public void DebugSetObstacleTimer(PlanetData planet, int pad, int seconds)
        {
            if (seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
            var job = FindObstacleJob(planet.Id, pad);
            if (job == null) throw new ArgumentException("No active clearance job on this pad.", nameof(pad));
            job.endUtc = UtcText.Format(clock.UtcNow.AddSeconds(seconds));
            string target = ObstacleBuilderTarget(planet.Id, pad);
            foreach (var builder in builders)
                if (builder != null && builder.target == target) builder.busyUntilUtc = job.endUtc;
            CompleteBuilderJobs();
            world.Touch();
            Notify();
        }
    }
}
#endif
