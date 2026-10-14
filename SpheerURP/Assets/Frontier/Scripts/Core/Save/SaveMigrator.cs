using System;
using System.Collections.Generic;

namespace Frontier
{
    /// <summary>Resolves planet ids for the migrator without knowing how worlds are built (implemented by the game's authored worlds).</summary>
    public interface IWorldIdMap
    {
        /// <summary>Canonical id of the home world (the planet a new game starts on).</summary>
        string HomeId { get; }

        /// <summary>Resolves a legacy authored id ("sylva") or an id that is already canonical to the canonical id. False when unknown.</summary>
        bool TryCanonical(string id, out string canonicalId);

        /// <summary>Distance of a world's system from home in light-years, or a negative number when the world is unknown.</summary>
        double DistanceFromHomeLy(string canonicalId);
    }

    /// <summary>Inputs the migrator cannot know by itself.</summary>
    public sealed class MigrationContext
    {
        public string AppVersion = "";
        public ulong UniverseSeed;
        public int GeneratorVersion = 1;
        public DateTime NowUtc = DateTime.UtcNow;
        public string SaveId = "";
    }

    /// <summary>
    /// Pure function from the legacy v1 save to schema v2. Never throws on odd content: lists of unequal length are cut to the shortest
    /// (the old writer always wrote them in step), unknown planet ids are kept verbatim so no player data is destroyed, and everything
    /// questionable is reported through <c>warnings</c>.
    /// </summary>
    public static class SaveMigrator
    {
        public static SaveV2 FromV1(SaveDataV1 v1, IWorldIdMap map, MigrationContext context, List<string> warnings)
        {
            if (v1 == null) throw new ArgumentNullException("v1");
            if (map == null) throw new ArgumentNullException("map");
            if (context == null) context = new MigrationContext();
            Normalize(v1);

            var save = new SaveV2();
            save.saveId = context.SaveId ?? "";
            save.createdWith.appVersion = context.AppVersion ?? "";
            save.createdWith.generatorVersion = context.GeneratorVersion;
            save.createdWith.universeSeed = SeedText.Format(context.UniverseSeed);
            save.clock.lastSavedUtc = UtcText.Format(context.NowUtc);
            save.player.xp = Math.Max(0, v1.xp);

            // inventory and upgrades (parallel lists in v1)
            var stacks = new Dictionary<string, int>(StringComparer.Ordinal);
            var stackOrder = new List<string>();
            int itemCount = Math.Min(v1.itemIds.Count, v1.itemCounts.Count);
            for (int i = 0; i < itemCount; i++)
            {
                string id = v1.itemIds[i];
                int count = v1.itemCounts[i];
                if (string.IsNullOrEmpty(id) || count <= 0) continue;
                if (!stacks.ContainsKey(id)) { stacks[id] = 0; stackOrder.Add(id); }
                stacks[id] += count;
            }
            foreach (string id in stackOrder) save.player.inventory.Add(new StackRecord { resourceId = id, count = stacks[id] });

            var levels = new Dictionary<string, int>(StringComparer.Ordinal);
            var levelOrder = new List<string>();
            int upgradeCount = Math.Min(v1.upgradeIds.Count, v1.upgradeLevels.Count);
            for (int i = 0; i < upgradeCount; i++)
            {
                string id = v1.upgradeIds[i];
                if (string.IsNullOrEmpty(id) || v1.upgradeLevels[i] <= 0) continue;
                if (!levels.ContainsKey(id)) levelOrder.Add(id);
                levels[id] = v1.upgradeLevels[i];
            }
            foreach (string id in levelOrder) save.player.upgrades.Add(new IdLevel { id = id, level = levels[id] });

            // worlds
            var worlds = new Dictionary<string, PlanetDelta>(StringComparer.Ordinal);
            var canonicalOf = new Dictionary<string, string>(StringComparer.Ordinal);
            Func<string, string> canon = legacy =>
            {
                string known;
                if (canonicalOf.TryGetValue(legacy, out known)) return known;
                string resolved;
                if (map.TryCanonical(legacy, out resolved)) known = resolved;
                else
                {
                    known = legacy;
                    if (warnings != null) warnings.Add("unknown world '" + legacy + "' kept as is");
                }
                canonicalOf[legacy] = known;
                return known;
            };
            Func<string, PlanetDelta> delta = canonical =>
            {
                PlanetDelta d;
                if (!worlds.TryGetValue(canonical, out d)) worlds[canonical] = d = new PlanetDelta { planetId = canonical };
                return d;
            };

            int structureCount = Math.Min(Math.Min(v1.structPlanets.Count, v1.structIds.Count), Math.Min(v1.structSlots.Count, v1.structLevels.Count));
            for (int i = 0; i < structureCount; i++)
            {
                string planet = v1.structPlanets[i], def = v1.structIds[i];
                if (string.IsNullOrEmpty(planet) || string.IsNullOrEmpty(def) || v1.structSlots[i] < 0)
                {
                    if (warnings != null) warnings.Add("dropped an invalid structure record (" + planet + ", " + def + ", slot " + v1.structSlots[i] + ")");
                    continue;
                }
                var d = delta(canon(planet));
                bool taken = false;
                foreach (var existing in d.structures) if (existing.slot == v1.structSlots[i]) { taken = true; break; }
                if (taken)
                {
                    if (warnings != null) warnings.Add("two structures on " + planet + " slot " + v1.structSlots[i] + "; kept the first");
                    continue;
                }
                d.structures.Add(new StructureRecord { defId = def, slot = v1.structSlots[i], level = Math.Max(1, v1.structLevels[i]) });
            }

            int waveCount = Math.Min(v1.waveIds.Count, v1.waveCounts.Count);
            int waveSum = 0;
            for (int i = 0; i < waveCount; i++)
            {
                if (string.IsNullOrEmpty(v1.waveIds[i]) || v1.waveCounts[i] <= 0) continue;
                var d = delta(canon(v1.waveIds[i]));
                d.wavesCleared = Math.Max(d.wavesCleared, v1.waveCounts[i]);
                waveSum += v1.waveCounts[i];
            }

            foreach (var w in v1.warehouses)
            {
                if (w == null || string.IsNullOrEmpty(w.planetId)) continue;
                var d = delta(canon(w.planetId));
                var storage = new StorageRecord { structureSlot = w.structureSlot };
                foreach (var s in w.slots)
                {
                    bool filled = s != null && s.count > 0 && !string.IsNullOrEmpty(s.resourceId);
                    storage.slots.Add(new StackRecord { resourceId = filled ? s.resourceId : null, count = filled ? s.count : 0 });
                }
                bool duplicate = false;
                foreach (var existing in d.storages) if (existing.structureSlot == storage.structureSlot) { duplicate = true; break; }
                if (!duplicate) d.storages.Add(storage);
                else if (warnings != null) warnings.Add("two warehouses on " + w.planetId + " slot " + w.structureSlot + "; kept the first");
            }

            // where the player is, and which worlds the old save proves they have been to
            string current;
            if (string.IsNullOrEmpty(v1.planetId)) current = map.HomeId;
            else
            {
                current = canon(v1.planetId);
                string probe;
                if (!map.TryCanonical(current, out probe))
                {
                    if (warnings != null) warnings.Add("current world '" + v1.planetId + "' is unknown; returning to home");
                    current = map.HomeId;
                }
            }
            save.location.currentPlanetId = current;

            var landed = new HashSet<string>(StringComparer.Ordinal) { map.HomeId, current };
            foreach (var id in worlds.Keys) landed.Add(id);
            double farthest = 0.0;
            foreach (string id in landed)
            {
                var d = delta(id);
                d.discovered = true;
                if (d.visits < 1) d.visits = 1;
                double distance = map.DistanceFromHomeLy(id);
                if (distance > farthest) farthest = distance;
            }

            foreach (var d in worlds.Values) if (d.HasContent) save.worlds.Add(d);
            save.worlds.Sort((a, b) => string.CompareOrdinal(a.planetId, b.planetId));

            save.stats.totalWavesCleared = Math.Max(Math.Max(0, v1.totalWavesCleared), waveSum);
            save.stats.worldsLanded = landed.Count;
            save.stats.maxDistanceFromHomeLy = farthest;
            return save;
        }

        static void Normalize(SaveDataV1 v1)
        {
            if (v1.itemIds == null) v1.itemIds = new List<string>();
            if (v1.itemCounts == null) v1.itemCounts = new List<int>();
            if (v1.upgradeIds == null) v1.upgradeIds = new List<string>();
            if (v1.upgradeLevels == null) v1.upgradeLevels = new List<int>();
            if (v1.waveIds == null) v1.waveIds = new List<string>();
            if (v1.waveCounts == null) v1.waveCounts = new List<int>();
            if (v1.structPlanets == null) v1.structPlanets = new List<string>();
            if (v1.structIds == null) v1.structIds = new List<string>();
            if (v1.structSlots == null) v1.structSlots = new List<int>();
            if (v1.structLevels == null) v1.structLevels = new List<int>();
            if (v1.warehouses == null) v1.warehouses = new List<WarehouseStorageV1>();
            foreach (var w in v1.warehouses) if (w != null && w.slots == null) w.slots = new List<StoredStackV1>();
        }
    }
}
