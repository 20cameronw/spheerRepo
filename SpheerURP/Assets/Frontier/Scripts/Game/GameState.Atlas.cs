using System;
using System.Collections.Generic;

namespace Frontier
{
    public partial class GameState
    {
        const string AtlasResourceType = "resource";
        const string AtlasRecipeType = "recipe";
        const string AtlasStructureType = "structure";
        const string AtlasEnemyType = "enemy";

        public bool IsAtlasDiscovered(string entryType, string id)
        {
            if (string.IsNullOrEmpty(entryType) || string.IsNullOrEmpty(id) || atlas == null) return false;
            return atlas.Contains(AtlasKey(entryType, id));
        }

        public int AtlasDiscoveredCount(string entryType)
        {
            if (string.IsNullOrEmpty(entryType) || atlas == null) return 0;
            string prefix = entryType + ":";
            var discovered = new HashSet<string>(StringComparer.Ordinal);
            foreach (string entry in atlas)
            {
                if (string.IsNullOrEmpty(entry) || !entry.StartsWith(prefix, StringComparison.Ordinal)) continue;
                string id = entry.Substring(prefix.Length);
                if (IsAtlasCatalogEntry(entryType, id)) discovered.Add(id);
            }
            return discovered.Count;
        }

        public int AtlasCatalogCount(string entryType)
        {
            if (entryType == AtlasResourceType)
            {
                int count = 0;
                if (db.resources != null)
                    foreach (var resource in db.resources)
                        if (resource != null && !string.IsNullOrEmpty(resource.id)) count++;
                return count;
            }
            if (entryType == AtlasRecipeType)
            {
                int count = 0;
                if (db.recipes != null)
                    foreach (var recipe in db.recipes)
                        if (recipe != null && !string.IsNullOrEmpty(recipe.id)) count++;
                return count;
            }
            if (entryType == AtlasStructureType)
            {
                var kinds = new HashSet<string>(StringComparer.Ordinal);
                if (db.structures != null)
                    foreach (var structure in db.structures)
                        if (structure != null && structure.Implemented) kinds.Add(structure.kind.ToString());
                return kinds.Count;
            }
            if (entryType == AtlasEnemyType)
            {
                int count = 0;
                if (db.enemies != null)
                    foreach (var enemy in db.enemies)
                        if (enemy != null && !string.IsNullOrEmpty(enemy.id)) count++;
                return count;
            }
            return 0;
        }

        static string AtlasKey(string entryType, string id) { return entryType + ":" + id; }

        bool IsAtlasCatalogEntry(string entryType, string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            if (entryType == AtlasResourceType) return db.FindResource(id) != null;
            if (entryType == AtlasRecipeType)
            {
                if (db.recipes == null) return false;
                foreach (var recipe in db.recipes)
                    if (recipe != null && recipe.id == id) return true;
                return false;
            }
            if (entryType == AtlasStructureType)
            {
                if (db.structures == null) return false;
                foreach (var structure in db.structures)
                    if (structure != null && structure.Implemented && structure.kind.ToString() == id) return true;
            }
            if (entryType == AtlasEnemyType && db.enemies != null)
                foreach (var enemy in db.enemies)
                    if (enemy != null && enemy.id == id) return true;
            return false;
        }

        int AtlasReward(string entryType)
        {
            if (db.shardRules == null || db.shardRules.data == null) return 0;
            var rules = db.shardRules.data;
            if (entryType == AtlasResourceType) return Math.Max(0, rules.atlasResourceShards);
            if (entryType == AtlasRecipeType) return Math.Max(0, rules.atlasRecipeShards);
            if (entryType == AtlasStructureType) return Math.Max(0, rules.atlasStructureShards);
            return 0;
        }

        bool RecordAtlasDiscovery(string entryType, string id, bool payReward)
        {
            if (!IsAtlasCatalogEntry(entryType, id)) return false;
            if (atlas == null) atlas = new List<string>();
            string key = AtlasKey(entryType, id);
            if (atlas.Contains(key)) return false;
            atlas.Add(key);
            if (payReward) AddShards(AtlasReward(entryType));
            saves.MarkDirty();
            return true;
        }

        void RecordAtlasResourceAcquired(string resourceId, bool payReward = true)
        {
            RecordAtlasDiscovery(AtlasResourceType, resourceId, payReward);
        }

        void RecordAtlasRecipeCrafted(RecipeSO recipe)
        {
            if (recipe != null) RecordAtlasDiscovery(AtlasRecipeType, recipe.id, true);
        }

        public bool RecordAtlasEnemySeen(EnemySO enemy)
        {
            if (enemy == null || !RecordAtlasDiscovery(AtlasEnemyType, enemy.id, false)) return false;
            Notify();
            return true;
        }

        void RecordAtlasStructureBuilt(StructureSO structure, bool payReward = true)
        {
            if (structure != null && structure.Implemented)
                RecordAtlasDiscovery(AtlasStructureType, structure.kind.ToString(), payReward);
        }

        void SeedAtlasFromExistingProgress()
        {
            bool changed = false;
            foreach (var stack in backpack.Stacks)
            {
                if (stack.Value > 0 && RecordAtlasDiscovery(AtlasResourceType, stack.Key, false)) changed = true;
            }
            foreach (var structure in structures)
            {
                if (IsOperational(structure) && RecordAtlasDiscovery(AtlasStructureType, structure.def.kind.ToString(), false))
                    changed = true;
            }
            foreach (var delta in world.All)
            {
                if (delta == null || delta.storages == null) continue;
                foreach (var storage in delta.storages)
                {
                    if (storage == null || storage.slots == null) continue;
                    foreach (var stack in storage.slots)
                    {
                        if (stack != null && stack.count > 0 &&
                            RecordAtlasDiscovery(AtlasResourceType, stack.resourceId, false)) changed = true;
                    }
                }
            }
            if (changed) saves.MarkDirty();
        }
    }
}
