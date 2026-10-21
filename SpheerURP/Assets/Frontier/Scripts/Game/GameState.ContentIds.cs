using System;
using System.Collections.Generic;

namespace Frontier
{
    public partial class GameState
    {
        bool RemapContentIds(SaveV2 save)
        {
            if (!db.UsesImportedEconomy) return false;
            bool changed = false;
            Func<string, Func<string, string>, string> resolve = (id, map) =>
            {
                string result = map(id);
                if (result != id) changed = true;
                return result;
            };
            var inventory = new Dictionary<string, StackRecord>(StringComparer.Ordinal);
            foreach (var stack in save.player.inventory)
            {
                stack.resourceId = resolve(stack.resourceId, db.ResolveResourceId);
                if (inventory.TryGetValue(stack.resourceId, out var existing))
                {
                    existing.count = (int)Math.Min(int.MaxValue, (long)existing.count + stack.count);
                    changed = true;
                }
                else inventory.Add(stack.resourceId, stack);
            }
            save.player.inventory = new List<StackRecord>(inventory.Values);
            foreach (var stack in save.player.fuelTank)
                stack.resourceId = resolve(stack.resourceId, db.ResolveResourceId);
            var upgrades = new Dictionary<string, IdLevel>(StringComparer.Ordinal);
            foreach (var level in save.player.upgrades)
            {
                level.id = resolve(level.id, db.ResolveUpgradeId);
                if (upgrades.TryGetValue(level.id, out var existing))
                {
                    existing.level = Math.Max(existing.level, level.level);
                    changed = true;
                }
                else upgrades.Add(level.id, level);
            }
            save.player.upgrades = new List<IdLevel>(upgrades.Values);
            foreach (var worldDelta in save.worlds)
            {
                if (worldDelta == null) continue;
                if (worldDelta.structures != null)
                    foreach (var structure in worldDelta.structures)
                        if (structure != null) structure.defId = resolve(structure.defId, db.ResolveStructureId);
                if (worldDelta.storages != null)
                    foreach (var storage in worldDelta.storages)
                    {
                        if (storage == null || storage.slots == null) continue;
                        foreach (var stack in storage.slots)
                            if (stack != null && !string.IsNullOrEmpty(stack.resourceId))
                                stack.resourceId = resolve(stack.resourceId, db.ResolveResourceId);
                    }
            }
            return changed;
        }
    }
}
