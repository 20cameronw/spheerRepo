using System;
using System.Collections.Generic;

namespace Frontier
{
    /// <summary>A resource id with an amount: costs, rewards and recipe sides in the engine-free layer.</summary>
    public readonly struct IdAmount
    {
        public readonly string Id;
        public readonly int Amount;

        public IdAmount(string id, int amount)
        {
            Id = id;
            Amount = amount;
        }

        public override string ToString() { return Id + " x" + Amount; }
    }

    /// <summary>
    /// The player's backpack: slot based, resources identified by id. The rules are exactly those of the original GameState inventory
    /// (a stack of <c>stackSize</c> items takes one slot; a resource may use any free slot) so that save games and balance are
    /// unchanged. The capacity (slots) and the stack sizes are supplied by the host because they come from upgrades and data assets.
    /// </summary>
    public sealed class Inventory
    {
        readonly Dictionary<string, int> items = new Dictionary<string, int>(StringComparer.Ordinal);
        readonly Func<int> capacity;
        readonly Func<string, int> stackSizes;

        public Inventory(Func<int> capacity, Func<string, int> stackSizes)
        {
            if (capacity == null) throw new ArgumentNullException("capacity");
            if (stackSizes == null) throw new ArgumentNullException("stackSizes");
            this.capacity = capacity;
            this.stackSizes = stackSizes;
        }

        /// <summary>Number of backpack slots right now (base slots plus upgrades).</summary>
        public int Capacity { get { return capacity(); } }

        /// <summary>Resource ids that currently have a non-zero count, with their counts (live view, do not modify the inventory while iterating).</summary>
        public IEnumerable<KeyValuePair<string, int>> Stacks { get { return items; } }

        public int Count(string id)
        {
            int c;
            return !string.IsNullOrEmpty(id) && items.TryGetValue(id, out c) ? c : 0;
        }

        /// <summary>Items per slot for a resource (at least 1).</summary>
        public int StackSizeOf(string id)
        {
            return Math.Max(1, stackSizes(id));
        }

        /// <summary>True when the host knows the resource (its stack size lookup returns a positive number).</summary>
        public bool IsKnown(string id)
        {
            return !string.IsNullOrEmpty(id) && stackSizes(id) > 0;
        }

        /// <summary>Slots needed to hold <paramref name="count"/> items of a resource.</summary>
        public int SlotsFor(string id, int count)
        {
            if (count <= 0) return 0;
            long stack = StackSizeOf(id);
            return (int)(((long)count + stack - 1) / stack);
        }

        public int SlotsUsed
        {
            get
            {
                int n = 0;
                foreach (var kv in items) n += SlotsFor(kv.Key, kv.Value);
                return n;
            }
        }

        /// <summary>How many more items of the resource fit, given what everything else occupies.</summary>
        public int FreeSpace(string id)
        {
            int usedByOthers = 0;
            foreach (var kv in items) if (kv.Key != id) usedByOthers += SlotsFor(kv.Key, kv.Value);
            int freeSlots = Math.Max(0, Capacity - usedByOthers);
            return Math.Max(0, freeSlots * StackSizeOf(id) - Count(id));
        }

        /// <summary>Adds items and returns how many actually fit. Unknown resources are refused.</summary>
        public int Add(string id, int amount)
        {
            if (!IsKnown(id)) return 0;
            int free = FreeSpace(id);
            int accepted = amount < 0 ? 0 : (amount > free ? free : amount);
            if (accepted > 0) items[id] = Count(id) + accepted;
            return accepted;
        }

        /// <summary>Removes up to <paramref name="amount"/> items and returns how many were removed.</summary>
        public int Remove(string id, int amount)
        {
            int have = Count(id);
            int removed = amount < 0 ? 0 : (amount > have ? have : amount);
            if (removed <= 0) return 0;
            int left = have - removed;
            if (left <= 0) items.Remove(id); else items[id] = left;
            return removed;
        }

        public bool Has(string id, int amount) { return Count(id) >= amount; }

        public bool Has(IList<IdAmount> cost)
        {
            for (int i = 0; i < cost.Count; i++) if (Count(cost[i].Id) < cost[i].Amount) return false;
            return true;
        }

        /// <summary>Spends the whole cost, or nothing when any part is missing.</summary>
        public bool Spend(IList<IdAmount> cost)
        {
            if (!Has(cost)) return false;
            for (int i = 0; i < cost.Count; i++)
            {
                int left = Count(cost[i].Id) - cost[i].Amount;
                if (left <= 0) items.Remove(cost[i].Id); else items[cost[i].Id] = left;
            }
            return true;
        }

        /// <summary>
        /// True when the inputs are present and every output fits once the inputs have been taken out (an input stack that empties
        /// frees its slot for the output).
        /// </summary>
        public bool CanCraft(IList<IdAmount> inputs, IList<IdAmount> outputs)
        {
            if (!Has(inputs)) return false;
            for (int i = 0; i < outputs.Count; i++)
                if (FreeSpace(outputs[i].Id) < outputs[i].Amount && !CanFitAfter(inputs, outputs, outputs[i].Id)) return false;
            return true;
        }

        /// <summary>
        /// Checks whether outputs fit after the specified portion of the cost is removed from this inventory.
        /// This also supports costs sourced partly from external storage.
        /// </summary>
        public bool CanFitAfterSpending(IList<IdAmount> spending, IList<IdAmount> outputs)
        {
            var spent = new Dictionary<string, long>(StringComparer.Ordinal);
            for (int i = 0; i < spending.Count; i++)
            {
                var item = spending[i];
                if (string.IsNullOrEmpty(item.Id) || item.Amount < 0) return false;
                long amount;
                spent.TryGetValue(item.Id, out amount);
                spent[item.Id] = amount + item.Amount;
            }

            int usedSlots = 0;
            foreach (var item in items)
            {
                long removed;
                spent.TryGetValue(item.Key, out removed);
                if (removed > item.Value) return false;
                long remaining = item.Value - removed;
                if (remaining > 0) usedSlots += SlotsFor(item.Key, (int)remaining);
            }
            foreach (var item in spent)
                if (!items.ContainsKey(item.Key) && item.Value > 0) return false;

            var added = new Dictionary<string, long>(StringComparer.Ordinal);
            for (int i = 0; i < outputs.Count; i++)
            {
                var item = outputs[i];
                if (!IsKnown(item.Id) || item.Amount < 0) return false;
                long amount;
                added.TryGetValue(item.Id, out amount);
                added[item.Id] = amount + item.Amount;
            }
            foreach (var item in added)
            {
                long removed;
                spent.TryGetValue(item.Key, out removed);
                long remaining = Count(item.Key) - removed;
                long stackSize = StackSizeOf(item.Key);
                long before = remaining <= 0 ? 0 : (remaining + stackSize - 1) / stackSize;
                long afterCount = remaining + item.Value;
                long after = afterCount <= 0 ? 0 : (afterCount + stackSize - 1) / stackSize;
                usedSlots += (int)(after - before);
            }
            return usedSlots <= Capacity;
        }

        bool CanFitAfter(IList<IdAmount> inputs, IList<IdAmount> outputs, string id)
        {
            int need = 0;
            for (int i = 0; i < outputs.Count; i++) if (outputs[i].Id == id) need += outputs[i].Amount;
            int have = Count(id), inputUse = 0;
            for (int i = 0; i < inputs.Count; i++) if (inputs[i].Id == id) inputUse += inputs[i].Amount;
            int usedOthers = 0;
            foreach (var kv in items) if (kv.Key != id) usedOthers += SlotsFor(kv.Key, kv.Value);
            for (int i = 0; i < inputs.Count; i++)
            {
                string input = inputs[i].Id;
                if (input != id && items.ContainsKey(input))
                    usedOthers += SlotsFor(input, Count(input) - inputs[i].Amount) - SlotsFor(input, Count(input));
            }
            int cap = Math.Max(0, Capacity - usedOthers) * StackSizeOf(id);
            return have - inputUse + need <= cap;
        }

        /// <summary>Spends the inputs and adds the outputs; false (and nothing changed) when <see cref="CanCraft"/> says no.</summary>
        public bool Craft(IList<IdAmount> inputs, IList<IdAmount> outputs)
        {
            if (!CanCraft(inputs, outputs)) return false;
            Spend(inputs);
            for (int i = 0; i < outputs.Count; i++) Add(outputs[i].Id, outputs[i].Amount);
            return true;
        }

        /// <summary>Sets a count directly (debug tools, loading a save). A count of zero or less removes the stack. No capacity check.</summary>
        public void Set(string id, int count)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (count <= 0) items.Remove(id); else items[id] = count;
        }

        public void Clear() { items.Clear(); }
    }
}
