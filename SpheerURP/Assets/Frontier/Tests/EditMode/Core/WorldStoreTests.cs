using System;
using System.Collections.Generic;
using NUnit.Framework;
using TestLog = Frontier.Universe.Tests.TestLog;

namespace Frontier.Tests
{
    /// <summary>The per-planet delta store: structures, warehouse storage, waves, visits, export/import.</summary>
    [TestFixture]
    public class WorldStoreTests
    {
        const string Home = "p:0:0:1000:0";
        const string Glacia = "p:0:0:1000:1";

        [Test]
        public void Place_RecordsStructuresAndRefusesTakenPads()
        {
            var store = new WorldStore();
            int v0 = store.Version;
            var lumber = store.Place(Home, "lumber", 0);
            Assert.IsNotNull(lumber);
            Assert.AreEqual(1, lumber.level);
            Assert.Greater(store.Version, v0);
            Assert.IsNull(store.Place(Home, "mine", 0), "a pad holds one structure");
            Assert.IsNotNull(store.Place(Glacia, "mine", 0), "the same pad number on another planet is free");
            Assert.AreSame(lumber, store.StructureAt(Home, 0));
            Assert.IsNull(store.StructureAt(Home, 7));
            Assert.AreEqual(2, store.TotalStructures);
            Assert.AreEqual(1, store.StructuresOn(Home).Count);
            Assert.AreEqual(0, store.StructuresOn("p:nowhere").Count);
            Assert.IsNull(store.Place(Home, "", 3));
            Assert.IsNull(store.Place(Home, "lumber", -1));
        }

        [Test]
        public void Upgrading_ChangesTheLevelAndBumpsTheVersion()
        {
            var store = new WorldStore();
            var s = store.Place(Home, "refinery", 3);
            int before = store.Version;
            store.SetLevel(s, 4);
            Assert.AreEqual(4, s.level);
            Assert.Greater(store.Version, before);
            store.SetLevel(s, 0);
            Assert.AreEqual(1, s.level, "levels never drop below 1");
        }

        [Test]
        public void Waves_CountPerPlanet()
        {
            var store = new WorldStore();
            Assert.AreEqual(1, store.NextWave(Home));
            store.RecordWaveCleared(Home);
            store.RecordWaveCleared(Home);
            store.RecordWaveCleared(Glacia);
            Assert.AreEqual(3, store.NextWave(Home));
            Assert.AreEqual(2, store.NextWave(Glacia));
            Assert.AreEqual(2, store.WavesCleared(Home));
        }

        [Test]
        public void Visits_RecordFirstAndLastTimeAndDiscovery()
        {
            var store = new WorldStore();
            var clock = new FakeClock();
            Assert.IsFalse(store.IsDiscovered(Glacia));
            Assert.IsTrue(store.RecordVisit(Glacia, clock.UtcNow), "the first landing is reported as the first visit");
            string first = store.Get(Glacia).firstVisitUtc;
            clock.Advance(3600);
            Assert.IsFalse(store.RecordVisit(Glacia, clock.UtcNow));
            var d = store.Get(Glacia);
            Assert.AreEqual(2, d.visits);
            Assert.IsTrue(d.discovered);
            Assert.AreEqual(first, d.firstVisitUtc, "the first visit time never changes");
            DateTime last, firstParsed;
            Assert.IsTrue(UtcText.TryParse(d.lastVisitUtc, out last));
            Assert.IsTrue(UtcText.TryParse(d.firstVisitUtc, out firstParsed));
            Assert.AreEqual(TimeSpan.FromHours(1), last - firstParsed);
            Assert.AreEqual(DateTimeKind.Utc, last.Kind);
        }

        [Test]
        public void Storage_IsCreatedPerPadGrowsAndKeepsItsContents()
        {
            var store = new WorldStore();
            var a = store.StorageFor(Home, 5, 18);
            Assert.AreEqual(18, a.slots.Count);
            a.slots[1].resourceId = "wood"; a.slots[1].count = 40;
            var again = store.StorageFor(Home, 5, 54);
            Assert.AreSame(a, again);
            Assert.AreEqual(54, again.slots.Count);
            Assert.AreEqual(40, again.slots[1].count, "growing a warehouse keeps what it holds");
            var smaller = store.StorageFor(Home, 5, 18);
            Assert.AreEqual(54, smaller.slots.Count, "storage never shrinks");
            var other = store.StorageFor(Home, 6, 18);
            Assert.AreNotSame(a, other);
            Assert.AreEqual(0, other.slots[1].count, "warehouses are isolated from each other");
        }

        [Test]
        public void Export_ContainsOnlyPlanetsTheyPlayerChanged_InStableOrder()
        {
            var store = new WorldStore();
            store.Get("p:untouched");                  // created by a lookup, never changed
            store.Place(Glacia, "mine", 1);
            store.RecordWaveCleared(Home);
            var list = store.Export();
            Assert.AreEqual(2, list.Count);
            Assert.AreEqual(Home, list[0].planetId, "sorted by id");
            Assert.AreEqual(Glacia, list[1].planetId);
        }

        [Test]
        public void Import_ReplacesTheStoreAndRepairsBrokenRecords()
        {
            var store = new WorldStore();
            store.Place(Home, "old", 0);
            var broken = new PlanetDelta { planetId = Glacia, structures = null, storages = null, clearedObstacles = null, visits = -4, wavesCleared = -2 };
            var d = new PlanetDelta { planetId = Home };
            d.structures.Add(new StructureRecord { defId = "lumber", slot = 2, level = 0 });
            d.structures.Add(new StructureRecord { defId = "dup", slot = 2, level = 5 });
            d.structures.Add(new StructureRecord { defId = "", slot = 3 });
            d.structures.Add(null);
            d.storages.Add(new StorageRecord { structureSlot = 1, slots = null });
            var twin = new PlanetDelta { planetId = Home, wavesCleared = 4, visits = 2, discovered = true };
            twin.structures.Add(new StructureRecord { defId = "mine", slot = 9 });
            store.Import(new List<PlanetDelta> { broken, d, null, new PlanetDelta { planetId = "" }, twin });

            Assert.AreEqual(2, store.WorldCount);
            Assert.IsNull(store.StructureAt(Home, 0), "the old content is gone");
            var home = store.Get(Home);
            Assert.AreEqual(2, home.structures.Count, "the invalid and duplicate records were dropped, the merged twin's structure kept");
            Assert.AreEqual(1, store.StructureAt(Home, 2).level, "level is repaired to at least 1");
            Assert.AreEqual("lumber", store.StructureAt(Home, 2).defId, "the first record of a slot wins");
            Assert.IsNotNull(store.StructureAt(Home, 9));
            Assert.AreEqual(4, home.wavesCleared);
            Assert.AreEqual(2, home.visits);
            Assert.IsNotNull(home.storages[0].slots);
            var b = store.Get(Glacia);
            Assert.AreEqual(0, b.visits);
            Assert.AreEqual(0, b.wavesCleared);
            Assert.IsNotNull(b.structures);
        }

        [Test]
        public void Remove_DropsAStructure()
        {
            var store = new WorldStore();
            var s = store.Place(Home, "lumber", 0);
            Assert.IsTrue(store.Remove(Home, s));
            Assert.IsFalse(store.Remove(Home, s));
            Assert.AreEqual(0, store.TotalStructures);
        }
    }

    /// <summary>Warehouse drag rules (the semantics FrontierStorageChecks and StorageTests assert on the live game), engine free.</summary>
    [TestFixture]
    public class WarehouseOpsTests
    {
        Inventory pack;
        StorageRecord storage;
        const int Capacity = 18;

        [SetUp]
        public void SetUp()
        {
            pack = new Inventory(() => 9, id => id == "ghost" ? 0 : 64);
            var store = new WorldStore();
            storage = store.StorageFor("p:0:0:1000:0", 1, Capacity);
            pack.Add("wood", 120);
        }

        static int Held(Inventory pack, StorageRecord storage, string id) { return pack.Count(id) + WarehouseOps.Held(storage, id); }

        [Test]
        public void Deposit_StopsAtTheStackLimitAndConservesItems()
        {
            Assert.AreEqual(64, WarehouseOps.Deposit(pack, storage, 0, "wood", 100), "a slot holds one stack");
            Assert.AreEqual(120, Held(pack, storage, "wood"));
            Assert.AreEqual(56, pack.Count("wood"));
        }

        [Test]
        public void FullSlots_DifferentResourcesAndBadSlots_AreRejected()
        {
            WarehouseOps.Deposit(pack, storage, 0, "wood", 100);
            Assert.AreEqual(0, WarehouseOps.Deposit(pack, storage, 0, "wood", 1), "a full stack takes nothing more");
            pack.Add("scrap", 5);
            Assert.AreEqual(0, WarehouseOps.Deposit(pack, storage, 0, "scrap", 1), "a slot holds a single resource");
            Assert.AreEqual(0, WarehouseOps.Deposit(pack, storage, -1, "wood", 1));
            Assert.AreEqual(0, WarehouseOps.Deposit(pack, storage, Capacity, "wood", 1));
            Assert.AreEqual(0, WarehouseOps.Deposit(pack, storage, 3, "wood", 0));
            Assert.AreEqual(0, WarehouseOps.Deposit(pack, storage, 3, "ghost", 5));
            Assert.AreEqual(0, WarehouseOps.Deposit(pack, storage, 3, "stone", 5), "nothing to deposit");
        }

        [Test]
        public void Move_MergesSameResourceUpToTheStackAndSwapsOtherwise()
        {
            WarehouseOps.Deposit(pack, storage, 0, "wood", 40);
            WarehouseOps.Deposit(pack, storage, 1, "wood", 40);
            Assert.IsTrue(WarehouseOps.Move(pack, storage, 0, 1));
            Assert.AreEqual(64, storage.slots[1].count);
            Assert.AreEqual(16, storage.slots[0].count, "only what fits merges; the rest stays");
            Assert.IsFalse(WarehouseOps.Move(pack, storage, 0, 1), "a full target stack cannot take more");
            Assert.IsTrue(WarehouseOps.Move(pack, storage, 0, 5), "a move to an empty slot swaps");
            Assert.AreEqual(0, storage.slots[0].count);
            Assert.AreEqual(16, storage.slots[5].count);
            Assert.AreEqual("wood", storage.slots[5].resourceId);
            Assert.IsFalse(WarehouseOps.Move(pack, storage, 0, 5), "an empty source moves nothing");
            Assert.IsFalse(WarehouseOps.Move(pack, storage, 5, 5));
            Assert.IsFalse(WarehouseOps.Move(pack, storage, 5, 99));
            Assert.IsFalse(WarehouseOps.Move(pack, storage, -1, 5));
            pack.Add("stone", 3);
            WarehouseOps.Deposit(pack, storage, 6, "stone", 3);
            Assert.IsTrue(WarehouseOps.Move(pack, storage, 5, 6), "different resources swap");
            Assert.AreEqual("stone", storage.slots[5].resourceId);
            Assert.AreEqual("wood", storage.slots[6].resourceId);
        }

        [Test]
        public void Withdraw_ReturnsItemsToTheBackpackAndConserves()
        {
            WarehouseOps.Deposit(pack, storage, 0, "wood", 100);
            Assert.AreEqual(20, WarehouseOps.Withdraw(pack, storage, 0, 20));
            Assert.AreEqual(120, Held(pack, storage, "wood"));
            Assert.AreEqual(44, storage.slots[0].count);
            Assert.AreEqual(44, WarehouseOps.Withdraw(pack, storage, 0, 1000));
            Assert.IsNull(storage.slots[0].resourceId, "an emptied slot forgets its resource");
            Assert.AreEqual(0, WarehouseOps.Withdraw(pack, storage, 0, 5), "nothing left");
            Assert.AreEqual(0, WarehouseOps.Withdraw(pack, storage, 99, 5));
        }

        [Test]
        public void Withdraw_IsRejectedWhenTheBackpackIsFull_AndKeepsTheItems()
        {
            WarehouseOps.Deposit(pack, storage, 1, "wood", 50);
            foreach (string id in new[] { "stone", "iron", "crystal", "fuel", "core", "scrap", "alloy", "plasma" }) pack.Add(id, 64);
            pack.Add("wood", 1000);
            Assert.AreEqual(0, pack.FreeSpace("wood"));
            Assert.AreEqual(0, WarehouseOps.Withdraw(pack, storage, 1, 1));
            Assert.AreEqual(50, storage.slots[1].count, "the slot still holds its items");
        }

        [Test]
        public void RandomOperations_NeverCreateOrDestroyItemsOrOverfillSlots()
        {
            var rng = new Random(12345);
            int total = pack.Count("wood");
            int moves = 0;
            for (int i = 0; i < 4000; i++)
            {
                int slot = rng.Next(storage.slots.Count);
                switch (rng.Next(4))
                {
                    case 0: moves += WarehouseOps.Deposit(pack, storage, slot, "wood", rng.Next(1, 90)) > 0 ? 1 : 0; break;
                    case 1: moves += WarehouseOps.Withdraw(pack, storage, slot, rng.Next(1, 90)) > 0 ? 1 : 0; break;
                    case 2: moves += WarehouseOps.Move(pack, storage, slot, rng.Next(storage.slots.Count)) ? 1 : 0; break;
                    default: pack.Add("wood", rng.Next(0, 3)); total = Math.Max(total, Held(pack, storage, "wood")); break;
                }
                Assert.AreEqual(total, Held(pack, storage, "wood"), "operation " + i + " changed the item total");
                foreach (var s in storage.slots)
                {
                    Assert.LessOrEqual(s.count, 64, "operation " + i + " overfilled a slot");
                    if (s.count > 0) Assert.IsNotNull(s.resourceId, "operation " + i + " left items without a resource");
                    if (s.count == 0) Assert.IsTrue(string.IsNullOrEmpty(s.resourceId) || s.resourceId == "wood");
                }
            }
            TestLog.Info("warehouse random operations: 4000 steps, " + moves + " effective moves, item totals conserved");
            Assert.Greater(moves, 500);
        }
    }
}
