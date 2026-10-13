using System;
using System.Collections.Generic;
using NUnit.Framework;
using TestLog = Frontier.Universe.Tests.TestLog;

namespace Frontier.Tests
{
    /// <summary>
    /// The new id based <see cref="Inventory"/> must behave exactly like the inventory that used to live inside GameState. The old
    /// algorithm is kept here as an oracle (a line for line port, including its float arithmetic) and both are driven with the same
    /// seeded random operations.
    /// </summary>
    [TestFixture]
    public class InventoryParityTests
    {
        /// <summary>The original GameState inventory code, with string ids instead of ResourceSO keys.</summary>
        sealed class LegacyBackpack
        {
            public readonly Dictionary<string, int> items = new Dictionary<string, int>();
            public int Slots;
            public Func<string, int> Stack;

            public int Count(string r) { int c; return items.TryGetValue(r, out c) ? c : 0; }

            int SlotsFor(string r, int count) { return count <= 0 ? 0 : (int)Math.Ceiling(count / (float)Math.Max(1, Stack(r))); }

            public int SlotsUsed { get { int n = 0; foreach (var kv in items) n += SlotsFor(kv.Key, kv.Value); return n; } }

            public int FreeSpace(string r)
            {
                int usedByOthers = 0;
                foreach (var kv in items) if (kv.Key != r) usedByOthers += SlotsFor(kv.Key, kv.Value);
                int freeSlots = Math.Max(0, Slots - usedByOthers);
                return Math.Max(0, freeSlots * Math.Max(1, Stack(r)) - Count(r));
            }

            public int Add(string r, int amount)
            {
                int free = FreeSpace(r);
                int accepted = amount < 0 ? 0 : (amount > free ? free : amount);
                if (accepted > 0) items[r] = Count(r) + accepted;
                return accepted;
            }

            public bool Has(List<IdAmount> cost)
            {
                foreach (var c in cost) if (Count(c.Id) < c.Amount) return false;
                return true;
            }

            public bool Spend(List<IdAmount> cost)
            {
                if (!Has(cost)) return false;
                foreach (var c in cost)
                {
                    int left = Count(c.Id) - c.Amount;
                    if (left <= 0) items.Remove(c.Id); else items[c.Id] = left;
                }
                return true;
            }

            public bool Craft(List<IdAmount> inputs, List<IdAmount> outs)
            {
                if (!Has(inputs)) return false;
                foreach (var o in outs) if (FreeSpace(o.Id) < o.Amount && !CanFitAfter(inputs, outs, o.Id)) return false;
                Spend(inputs);
                foreach (var o in outs) Add(o.Id, o.Amount);
                return true;
            }

            bool CanFitAfter(List<IdAmount> inputs, List<IdAmount> outs, string res)
            {
                int need = 0;
                foreach (var o in outs) if (o.Id == res) need += o.Amount;
                int have = Count(res), inputUse = 0;
                foreach (var i in inputs) if (i.Id == res) inputUse += i.Amount;
                int usedOthers = 0;
                foreach (var kv in items) if (kv.Key != res) usedOthers += SlotsFor(kv.Key, kv.Value);
                foreach (var i in inputs)
                    if (i.Id != res && items.ContainsKey(i.Id))
                        usedOthers += SlotsFor(i.Id, Count(i.Id) - i.Amount) - SlotsFor(i.Id, Count(i.Id));
                int cap = Math.Max(0, Slots - usedOthers) * Math.Max(1, Stack(res));
                return have - inputUse + need <= cap;
            }
        }

        static readonly string[] Ids = { "wood", "stone", "iron", "crystal", "fuel", "core", "scrap" };

        // Stack sizes per id. Includes stack size 1 (one item per slot), awkward sizes and uniform packs.
        static readonly int[][] StackSets =
        {
            new[] { 64, 64, 64, 64, 64, 64, 64 },
            new[] { 1, 3, 8, 16, 64, 100, 5 },
            new[] { 7, 7, 7, 7, 7, 7, 7 },
            new[] { 2, 99, 1, 40, 13, 64, 21 }
        };

        const int StepsPerSet = 3000;

        static List<IdAmount> RandomList(Random rng, int maxItems, int maxAmount)
        {
            var list = new List<IdAmount>();
            int n = rng.Next(0, maxItems + 1);
            for (int i = 0; i < n; i++) list.Add(new IdAmount(Ids[rng.Next(Ids.Length)], rng.Next(0, maxAmount + 1)));
            return list;
        }

        static void AssertSame(LegacyBackpack oracle, Inventory inv, string context)
        {
            foreach (string id in Ids)
            {
                Assert.AreEqual(oracle.Count(id), inv.Count(id), context + ": count of " + id);
                Assert.AreEqual(oracle.FreeSpace(id), inv.FreeSpace(id), context + ": free space for " + id);
            }
            Assert.AreEqual(oracle.SlotsUsed, inv.SlotsUsed, context + ": slots used");
        }

        [Test]
        public void RandomOperations_MatchTheOriginalAlgorithm_OverTwelveThousandSteps()
        {
            int operations = 0, adds = 0, spends = 0, crafts = 0, craftsDone = 0, probes = 0;
            for (int set = 0; set < StackSets.Length; set++)
            {
                int[] stacks = StackSets[set];
                Func<string, int> stackOf = id => stacks[Array.IndexOf(Ids, id)];
                var rng = new Random(20261013 + set * 7919);
                var oracle = new LegacyBackpack { Slots = 9, Stack = stackOf };
                var inv = new Inventory(() => oracle.Slots, stackOf);
                for (int step = 0; step < StepsPerSet; step++)
                {
                    if (rng.Next(25) == 0) oracle.Slots = rng.Next(1, 61); // upgrades change the capacity, sometimes below what is carried
                    string context = "set " + set + " step " + step;
                    switch (rng.Next(5))
                    {
                        case 0:
                        case 1:
                        {
                            string id = Ids[rng.Next(Ids.Length)];
                            int amount = rng.Next(-5, 260);
                            Assert.AreEqual(oracle.Add(id, amount), inv.Add(id, amount), context + ": add accepted");
                            adds++;
                            break;
                        }
                        case 2:
                        {
                            var cost = RandomList(rng, 3, 70);
                            Assert.AreEqual(oracle.Has(cost), inv.Has(cost), context + ": has");
                            Assert.AreEqual(oracle.Spend(cost), inv.Spend(cost), context + ": spend");
                            spends++;
                            break;
                        }
                        case 3:
                        {
                            var inputs = RandomList(rng, 2, 40);
                            var outputs = RandomList(rng, 2, 40);
                            bool expected = oracle.Craft(inputs, outputs);
                            Assert.AreEqual(expected, inv.Craft(inputs, outputs), context + ": craft");
                            crafts++;
                            if (expected) craftsDone++;
                            break;
                        }
                        default:
                        {
                            // CanCraft must agree with the oracle's Craft and must not change anything
                            var inputs = RandomList(rng, 2, 40);
                            var outputs = RandomList(rng, 2, 40);
                            var before = new Dictionary<string, int>();
                            foreach (string id in Ids) before[id] = inv.Count(id);
                            bool can = inv.CanCraft(inputs, outputs);
                            foreach (string id in Ids) Assert.AreEqual(before[id], inv.Count(id), context + ": CanCraft changed the inventory");
                            var probe = new LegacyBackpack { Slots = oracle.Slots, Stack = stackOf };
                            foreach (var kv in oracle.items) probe.items[kv.Key] = kv.Value;
                            Assert.AreEqual(probe.Craft(inputs, outputs), can, context + ": CanCraft");
                            probes++;
                            break;
                        }
                    }
                    operations++;
                    AssertSame(oracle, inv, context);
                }
            }
            TestLog.Info("inventory parity: " + operations + " operations (" + adds + " adds, " + spends + " spends, " + crafts + " crafts of which " + craftsDone +
                         " succeeded, " + probes + " CanCraft probes), no differences from the original algorithm");
            Assert.GreaterOrEqual(operations, 10000);
            Assert.Greater(craftsDone, 40, "the random mix should craft successfully now and then");
        }

        [Test]
        public void StarterBackpack_FollowsTheDocumentedRules()
        {
            var inv = new Inventory(() => 9, id => 64);
            Assert.AreEqual(64, inv.Add("wood", 64));
            Assert.AreEqual(1, inv.SlotsUsed);
            Assert.AreEqual(65, inv.Add("wood", 65));
            Assert.AreEqual(129, inv.Count("wood"));
            Assert.AreEqual(3, inv.SlotsUsed, "129 items of a 64 stack need three slots");
            Assert.AreEqual(9 * 64 - 129, inv.FreeSpace("wood"));
            Assert.AreEqual(6 * 64, inv.FreeSpace("stone"), "other resources may use the remaining six slots");
            Assert.AreEqual(9 * 64 - 129, inv.Add("wood", 10000), "a full backpack accepts only what fits");
            Assert.AreEqual(0, inv.FreeSpace("stone"));
            Assert.AreEqual(0, inv.Add("stone", 1));
            Assert.IsFalse(inv.Spend(new List<IdAmount> { new IdAmount("wood", 1000) }), "a cost the player cannot pay changes nothing");
            Assert.AreEqual(9 * 64, inv.Count("wood"));
        }

        [Test]
        public void UnknownResources_AreRefused()
        {
            var inv = new Inventory(() => 9, id => id == "wood" ? 64 : 0);
            Assert.AreEqual(0, inv.Add("ghost", 5));
            Assert.IsFalse(inv.IsKnown("ghost"));
            Assert.IsFalse(inv.IsKnown(null));
            Assert.IsTrue(inv.IsKnown("wood"));
            Assert.AreEqual(5, inv.Add("wood", 5));
            Assert.AreEqual(0, inv.Count(null));
        }

        [Test]
        public void SettingRemovingAndClearing_AreExactAndSkipEmptyStacks()
        {
            var inv = new Inventory(() => 9, id => 64);
            inv.Set("wood", 10);
            inv.Set("stone", 0);
            inv.Set("iron", -3);
            Assert.AreEqual(10, inv.Count("wood"));
            int stacks = 0;
            foreach (var kv in inv.Stacks) { stacks++; Assert.Greater(kv.Value, 0); }
            Assert.AreEqual(1, stacks);
            Assert.AreEqual(10, inv.Remove("wood", 99));
            Assert.AreEqual(0, inv.Count("wood"));
            inv.Set("wood", 4);
            inv.Clear();
            Assert.AreEqual(0, inv.SlotsUsed);
        }

        [Test]
        public void CraftingFreesTheSlotOfAnEmptiedInput()
        {
            // 2 slots of stack 4: [wood 4][stone 4]. Crafting 4 wood into 4 fuel only fits because the wood stack empties first.
            var inv = new Inventory(() => 2, id => 4);
            inv.Add("wood", 4);
            inv.Add("stone", 4);
            var inputs = new List<IdAmount> { new IdAmount("wood", 4) };
            var outputs = new List<IdAmount> { new IdAmount("fuel", 4) };
            Assert.AreEqual(0, inv.FreeSpace("fuel"));
            Assert.IsTrue(inv.CanCraft(inputs, outputs));
            Assert.IsTrue(inv.Craft(inputs, outputs));
            Assert.AreEqual(0, inv.Count("wood"));
            Assert.AreEqual(4, inv.Count("fuel"));
            Assert.IsFalse(inv.Craft(inputs, outputs), "no wood left");
        }
    }
}
