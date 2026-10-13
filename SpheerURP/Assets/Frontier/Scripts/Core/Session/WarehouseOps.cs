using System;

namespace Frontier
{
    /// <summary>
    /// The warehouse drag rules between the backpack and a storage grid, engine-free and id based. These are the semantics of the
    /// original GameState: a slot holds one resource up to its stack size, transfers move only what fits and never discard the
    /// remainder, the total number of items is conserved. Callers resolve which storage and slot are meant (and whether the player
    /// is on the right planet); every method here is a pure rule over an <see cref="Inventory"/> and a <see cref="StorageRecord"/>.
    /// </summary>
    public static class WarehouseOps
    {
        /// <summary>Moves items from the backpack into a storage slot. Returns how many moved (0 when rejected).</summary>
        public static int Deposit(Inventory backpack, StorageRecord storage, int slot, string resourceId, int amount)
        {
            if (!backpack.IsKnown(resourceId) || amount <= 0) return 0;
            if (slot < 0 || slot >= storage.slots.Count) return 0;
            var target = storage.slots[slot];
            if (target.count > 0 && target.resourceId != resourceId) return 0;
            int moved = Math.Min(amount, Math.Min(backpack.Count(resourceId), Math.Max(0, backpack.StackSizeOf(resourceId) - target.count)));
            if (moved <= 0) return 0;
            backpack.Remove(resourceId, moved);
            target.resourceId = resourceId;
            target.count += moved;
            return moved;
        }

        /// <summary>Moves items from a storage slot into the backpack, as many as fit. Returns how many moved.</summary>
        public static int Withdraw(Inventory backpack, StorageRecord storage, int slot, int amount)
        {
            if (amount <= 0 || slot < 0 || slot >= storage.slots.Count) return 0;
            var source = storage.slots[slot];
            if (!backpack.IsKnown(source.resourceId) || source.count <= 0) return 0;
            int moved = backpack.Add(source.resourceId, Math.Min(source.count, amount));
            if (moved <= 0) return 0;
            source.count -= moved;
            if (source.count == 0) source.resourceId = null;
            return moved;
        }

        /// <summary>
        /// Rearranges a storage grid: same resource merges up to the stack size, anything else swaps. False when nothing moved
        /// (empty source, same slot, out of range, or a full target stack).
        /// </summary>
        public static bool Move(Inventory resources, StorageRecord storage, int from, int to)
        {
            if (from < 0 || to < 0 || from >= storage.slots.Count || to >= storage.slots.Count || from == to) return false;
            var source = storage.slots[from];
            var target = storage.slots[to];
            if (source.count <= 0) return false;
            if (source.resourceId == target.resourceId && target.count > 0)
            {
                if (!resources.IsKnown(source.resourceId)) return false;
                int moved = Math.Min(source.count, Math.Max(0, resources.StackSizeOf(source.resourceId) - target.count));
                if (moved == 0) return false;
                source.count -= moved;
                target.count += moved;
                if (source.count == 0) source.resourceId = null;
            }
            else
            {
                storage.slots[from] = target;
                storage.slots[to] = source;
            }
            return true;
        }

        /// <summary>Total number of items of a resource held by a storage grid.</summary>
        public static int Held(StorageRecord storage, string resourceId)
        {
            int n = 0;
            foreach (var s in storage.slots) if (s.count > 0 && s.resourceId == resourceId) n += s.count;
            return n;
        }

        /// <summary>Number of occupied slots.</summary>
        public static int UsedSlots(StorageRecord storage)
        {
            int n = 0;
            foreach (var s in storage.slots) if (s.count > 0) n++;
            return n;
        }
    }
}
