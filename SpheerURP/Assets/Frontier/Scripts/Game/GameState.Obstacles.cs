using System;
using System.Collections.Generic;
using Frontier.Economy;
using Frontier.Universe;
using EItem = Frontier.Economy.ItemAmount;

namespace Frontier
{
    public partial class GameState
    {
        const string ShardCurrency = "shards";
        EconomyCatalog obstacleCatalog;
        readonly Dictionary<string, ObstacleInstance[]> obstacleCache = new Dictionary<string, ObstacleInstance[]>(StringComparer.Ordinal);

        public int ShardBalance
        {
            get
            {
                long total = 0;
                if (wallet != null)
                    foreach (var entry in wallet)
                        if (entry != null && entry.currency == ShardCurrency && entry.amount > 0) total += entry.amount;
                return (int)Math.Min(int.MaxValue, total);
            }
        }

        public List<ObstacleInstance> ObstaclesOn(PlanetData p)
        {
            var result = new List<ObstacleInstance>();
            if (p == null || !TryGetGeneratedObstacles(p, out var generated)) return result;
            world.TryGet(p.Id, out var delta);
            foreach (var obstacle in generated)
                if (delta == null || (!delta.clearedObstacles.Contains(obstacle.pad) &&
                    !HasStructureOnPad(delta, obstacle.pad)))
                    result.Add(obstacle);
            return result;
        }

        public ObstacleInstance? ObstacleAt(PlanetData p, int pad)
        {
            foreach (var obstacle in ObstaclesOn(p))
                if (obstacle.pad == pad) return obstacle;
            return null;
        }

        public ObstacleKindDef ObstacleKind(ObstacleInstance obstacle)
        {
            var catalog = GetObstacleCatalog();
            return catalog != null && obstacle.kind >= 0 && obstacle.kind < catalog.obstacles.Length
                ? catalog.obstacles[obstacle.kind] : null;
        }

        public List<ItemAmount> ObstacleClearCost(PlanetData p, int pad)
        {
            var list = new List<ItemAmount>();
            var obstacle = ObstacleAt(p, pad);
            if (!obstacle.HasValue) return list;
            double discount = ColonyHubObstacleDiscount(p);
            foreach (EItem cost in obstacle.Value.cost)
            {
                ResourceSO resource = db.FindResource(db.ResolveResourceId(cost.resourceId));
                if (resource == null) throw new InvalidOperationException("Obstacle cost references unknown resource '" + cost.resourceId + "'.");
                int amount = ObstacleRules.Discounted(cost.amount, discount);
                list.Add(new ItemAmount(resource, amount));
            }
            return list;
        }

        public string ObstacleClearLockReason(int pad)
        {
            if (!db.UsesImportedEconomy || GetObstacleCatalog() == null) return "Obstacle clearing is unavailable";
            var obstacle = ObstacleAt(planet, pad);
            if (!obstacle.HasValue) return "This obstruction is no longer here";
            if (FindObstacleJob(planet.Id, pad) != null) return "Clearance crew assigned";
            if (obstacle.Value.seconds > 0 && AvailableBuilderCount <= 0) return BuilderAvailabilityReason();
            return Has(ObstacleClearCost(planet, pad)) ? null : "Not enough resources";
        }

        public bool StartObstacleClear(int pad)
        {
            if (CompleteBuilderJobs()) Notify();
            var obstacle = ObstacleAt(planet, pad);
            if (!obstacle.HasValue || FindObstacleJob(planet.Id, pad) != null) return false;
            if (obstacle.Value.seconds > 0 && AvailableBuilderCount <= 0) return false;
            var cost = ObstacleClearCost(planet, pad);
            if (!SpendQuietly(cost)) return false;

            var delta = world.Get(planet.Id);
            if (obstacle.Value.seconds <= 0)
            {
                CompleteObstacleClear(delta, obstacle.Value);
            }
            else
            {
                string endUtc = UtcText.Format(clock.UtcNow.AddSeconds(obstacle.Value.seconds));
                delta.obstacleJobs.Add(new ObstacleJobRecord { pad = pad, endUtc = endUtc });
                string target = ObstacleBuilderTarget(planet.Id, pad);
                builders.Add(new BuilderSlot { slotId = NextBuilderId(), target = target, busyUntilUtc = endUtc });
                world.Touch();
            }
            Notify();
            return true;
        }

        public int ObstacleClearSecondsRemaining(int pad)
        {
            var job = FindObstacleJob(planet.Id, pad);
            if (job == null || !UtcText.TryParse(job.endUtc, out var end)) return 0;
            double remaining = (end - clock.UtcNow).TotalSeconds;
            return remaining <= 0 ? 0 : (int)Math.Ceiling(remaining);
        }

        public int ObstacleSkipCost(int pad)
        {
            if (db.shardRules == null || db.shardRules.data == null) return 0;
            return ShardCurve.SkipCost(db.shardRules.data, ObstacleClearSecondsRemaining(pad));
        }

        public bool SkipObstacleClear(int pad)
        {
            var job = FindObstacleJob(planet.Id, pad);
            var obstacle = ObstacleAt(planet, pad);
            if (job == null || !obstacle.HasValue) return false;
            int cost = ObstacleSkipCost(pad);
            if (ShardBalance < cost) return false;
            SpendShards(cost);
            CompleteObstacleClear(world.Get(planet.Id), obstacle.Value);
            Notify();
            return true;
        }

        public IReadOnlyList<ObstacleRewardRecord> ObstacleRewardsOn(PlanetData p)
        {
            if (p == null || !world.TryGet(p.Id, out var delta)) return new ObstacleRewardRecord[0];
            return delta.obstacleRewards.AsReadOnly();
        }

        public bool ClaimObstacleReward(PlanetData p, int pad)
        {
            if (p == null || !world.TryGet(p.Id, out var delta)) return false;
            for (int i = 0; i < delta.obstacleRewards.Count; i++)
            {
                var reward = delta.obstacleRewards[i];
                if (reward.pad != pad) continue;
                ResourceSO resource = db.FindResource(reward.resourceId);
                if (resource == null) throw new InvalidOperationException("Pending obstacle salvage references unknown resource '" + reward.resourceId + "'.");
                int accepted = backpack.Add(reward.resourceId, reward.amount);
                if (accepted <= 0) return false;
                RecordAtlasResourceAcquired(resource.id);
                reward.amount -= accepted;
                if (reward.amount <= 0) delta.obstacleRewards.RemoveAt(i);
                world.Touch();
                Notify();
                return true;
            }
            return false;
        }

        bool CompleteObstacleJobs()
        {
            bool changed = false;
            foreach (var delta in world.All)
            {
                for (int i = delta.obstacleJobs.Count - 1; i >= 0; i--)
                {
                    var job = delta.obstacleJobs[i];
                    if (!UtcText.TryParse(job.endUtc, out var end) || end > clock.UtcNow) continue;
                    PlanetData p = universe.Get(delta.planetId);
                    var obstacle = ObstacleAt(p, job.pad);
                    if (!obstacle.HasValue) continue;
                    CompleteObstacleClear(delta, obstacle.Value);
                    changed = true;
                }
            }
            return changed;
        }

        void CompleteObstacleClear(PlanetDelta delta, ObstacleInstance obstacle)
        {
            delta.obstacleJobs.RemoveAll(job => job.pad == obstacle.pad);
            if (!delta.clearedObstacles.Contains(obstacle.pad)) delta.clearedObstacles.Add(obstacle.pad);
            string target = ObstacleBuilderTarget(delta.planetId, obstacle.pad);
            if (builders != null) builders.RemoveAll(slot => slot != null && slot.target == target);

            if (obstacle.xp > 0) progression.AddXp(obstacle.xp);
            if (obstacle.podShards > 0) AddShards(obstacle.podShards);

            ResourceSO salvage = db.FindResource(obstacle.reward.resourceId);
            if (salvage == null) throw new InvalidOperationException("Obstacle reward references unknown resource '" + obstacle.reward.resourceId + "'.");
            int accepted = backpack.Add(salvage.id, obstacle.reward.amount);
            if (accepted > 0) RecordAtlasResourceAcquired(salvage.id);
            int overflow = obstacle.reward.amount - accepted;
            if (overflow > 0)
                delta.obstacleRewards.Add(new ObstacleRewardRecord
                {
                    pad = obstacle.pad,
                    resourceId = salvage.id,
                    amount = overflow
                });
            world.Touch();
        }

        bool TryGetGeneratedObstacles(PlanetData p, out ObstacleInstance[] obstacles)
        {
            obstacles = null;
            EconomyCatalog catalog = GetObstacleCatalog();
            if (catalog == null) return false;
            if (!obstacleCache.TryGetValue(p.Id, out obstacles))
            {
                obstacles = ObstacleRules.Generate(catalog, universe.Seed, Hash.Fnv1a64(p.Id), p.BuildingSlots,
                    p.Size, p.Biome, p.Hazards, p.Tier, p.Id == universe.Home.Id);
                obstacleCache.Add(p.Id, obstacles);
            }
            return true;
        }

        EconomyCatalog GetObstacleCatalog()
        {
            if (!db.UsesImportedEconomy || db.obstacleRules == null || db.shardRules == null) return null;
            if (obstacleCatalog == null) obstacleCatalog = db.ToCatalog();
            return obstacleCatalog;
        }

        ObstacleJobRecord FindObstacleJob(string planetId, int pad)
        {
            if (!world.TryGet(planetId, out var delta)) return null;
            foreach (var job in delta.obstacleJobs) if (job.pad == pad) return job;
            return null;
        }

        static bool HasStructureOnPad(PlanetDelta delta, int pad)
        {
            foreach (var structure in delta.structures) if (structure.slot == pad) return true;
            return false;
        }

        double ColonyHubObstacleDiscount(PlanetData p)
        {
            var hub = p == null ? null : StructureOfKind(p, StructureKind.ColonyHub);
            return hub == null ? 0.0 : Math.Max(0.0, hub.def.EffectAt(hub.level));
        }

        void AddShards(int amount)
        {
            if (amount <= 0) return;
            if (wallet == null) wallet = new List<WalletEntry>();
            WalletEntry entry = null;
            foreach (var candidate in wallet)
                if (candidate != null && candidate.currency == ShardCurrency) { entry = candidate; break; }
            if (entry == null)
            {
                entry = new WalletEntry { currency = ShardCurrency };
                wallet.Add(entry);
            }
            entry.amount = (int)Math.Min(int.MaxValue, (long)Math.Max(0, entry.amount) + amount);
        }

        void SpendShards(int amount)
        {
            int remaining = amount;
            if (wallet == null) return;
            foreach (var entry in wallet)
            {
                if (remaining <= 0) break;
                if (entry == null || entry.currency != ShardCurrency || entry.amount <= 0) continue;
                int used = Math.Min(entry.amount, remaining);
                entry.amount -= used;
                remaining -= used;
            }
        }
    }
}
