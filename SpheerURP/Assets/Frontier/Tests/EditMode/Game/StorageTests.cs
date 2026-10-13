using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    /// <summary>
    /// Warehouse storage rules, ported from <c>Frontier &gt; Validate Warehouse Storage</c> (that editor check stays as the batch gate):
    /// capacity, stack limits, conservation of items, isolation between warehouses, upgrades, persistence and rejection rules.
    /// </summary>
    [TestFixture]
    public class StorageTests
    {
        SaveSandbox sandbox;
        GameDatabase db;
        GameState state;
        ResourceSO wood;
        PlacedStructure warehouse, second;
        WarehouseStorage storage;
        int total;

        [SetUp]
        public void SetUp()
        {
            sandbox = new SaveSandbox();
            db = DefaultContent.Create();
            state = new GameState(db, false);
            wood = db.FindResource("wood");
            var def = db.StructureOfKind(StructureKind.Warehouse);
            warehouse = new PlacedStructure { planetId = state.planet.id, slot = 1, def = def };
            second = new PlacedStructure { planetId = state.planet.id, slot = 2, def = def };
            state.structures.Add(warehouse);
            state.structures.Add(second);
            storage = state.StorageFor(warehouse);
            state.Add(wood, 120, false);
            total = state.Count(wood);
        }

        [TearDown]
        public void TearDown()
        {
            sandbox.Dispose();
        }

        // One full stack deposited into slot 0 and moved to slot 1.
        void StockSlotOne()
        {
            Assert.AreEqual(wood.stackSize, state.Deposit(warehouse, 0, wood, 100, false));
            Assert.IsTrue(state.MoveStoredSlot(warehouse, 0, 1, false));
        }

        // Wood in the backpack plus wood in every slot of the first warehouse.
        int WoodHeld()
        {
            int held = state.Count(wood);
            foreach (var slot in storage.slots) if (slot.resourceId == wood.id) held += slot.count;
            return held;
        }

        [Test]
        public void NewWarehouse_HasTheInitialCapacity()
        {
            Assert.AreEqual(18, storage.slots.Count);
        }

        [Test]
        public void Deposit_StopsAtTheStackLimitAndConservesItems()
        {
            Assert.Greater(total, 0);
            Assert.AreEqual(wood.stackSize, state.Deposit(warehouse, 0, wood, 100, false), "a slot holds one stack");
            Assert.AreEqual(total, state.Count(wood) + storage.slots[0].count, "items were created or destroyed");
        }

        [Test]
        public void FullSlot_AndADifferentResource_AreRejected()
        {
            state.Deposit(warehouse, 0, wood, 100, false);
            Assert.AreEqual(0, state.Deposit(warehouse, 0, wood, 1, false), "a full stack takes nothing more");
            Assert.AreEqual(0, state.Deposit(warehouse, 0, db.scrap, 1, false), "a slot holds a single resource");
        }

        [Test]
        public void MoveStoredSlot_RearrangesStacks()
        {
            state.Deposit(warehouse, 0, wood, 100, false);
            Assert.IsTrue(state.MoveStoredSlot(warehouse, 0, 1, false));
            Assert.AreEqual(0, storage.slots[0].count);
            Assert.AreEqual(wood.stackSize, storage.slots[1].count);
        }

        [Test]
        public void Withdraw_ReturnsItemsToTheBackpackAndConserves()
        {
            StockSlotOne();
            Assert.AreEqual(20, state.Withdraw(warehouse, 1, 20, false));
            Assert.AreEqual(total, state.Count(wood) + storage.slots[1].count, "items were created or destroyed");
        }

        [Test]
        public void Warehouses_AreIsolatedFromEachOther()
        {
            StockSlotOne();
            Assert.AreEqual(0, state.StorageFor(second).slots[1].count);
        }

        [Test]
        public void Upgrading_GrowsTheCapacityAndKeepsTheContents()
        {
            StockSlotOne();
            warehouse.level = 5;
            var upgraded = state.StorageFor(warehouse);
            Assert.AreEqual(54, upgraded.slots.Count);
            Assert.Greater(upgraded.slots[1].count, 0);
        }

        [Test]
        public void Storage_SurvivesAJsonRoundTrip()
        {
            StockSlotOne();
            var saved = new SaveData();
            saved.warehouses.Add(storage);
            var loaded = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(saved));
            Assert.AreEqual(warehouse.planetId, loaded.warehouses[0].planetId);
            Assert.AreEqual(warehouse.slot, loaded.warehouses[0].structureSlot);
            Assert.AreEqual(storage.slots[1].count, loaded.warehouses[0].slots[1].count);
            Assert.AreEqual(wood.id, loaded.warehouses[0].slots[1].resourceId);
        }

        [Test]
        public void AWarehouseOnAnotherPlanet_CannotBeUsedFromHere()
        {
            StockSlotOne();
            state.planet = db.planets[1];
            Assert.AreEqual(0, state.Deposit(warehouse, 2, wood, 1, false));
            Assert.AreEqual(0, state.Withdraw(warehouse, 1, 1, false));
        }

        [Test]
        public void Withdraw_IsRejectedWhenTheBackpackIsFull()
        {
            StockSlotOne();
            foreach (var resource in db.resources) state.Add(resource, 100000, false);
            Assert.AreEqual(0, state.Withdraw(warehouse, 1, 1, false));
            Assert.Greater(storage.slots[1].count, 0, "the slot must still hold its items");
        }

        [Test]
        public void RandomOperations_NeverCreateOrDestroyItems()
        {
            var rng = new System.Random(12345);
            for (int i = 0; i < 600; i++)
            {
                int slot = rng.Next(storage.slots.Count);
                switch (rng.Next(3))
                {
                    case 0: state.Deposit(warehouse, slot, wood, rng.Next(1, 60), false); break;
                    case 1: state.Withdraw(warehouse, slot, rng.Next(1, 60), false); break;
                    default: state.MoveStoredSlot(warehouse, slot, rng.Next(storage.slots.Count), false); break;
                }
                Assert.AreEqual(total, WoodHeld(), "operation " + i + " changed the item total");
                foreach (var s in storage.slots)
                {
                    Assert.LessOrEqual(s.count, wood.stackSize, "operation " + i + " overfilled a slot");
                    if (s.count > 0) Assert.IsNotNull(s.resourceId, "operation " + i + " left items without a resource");
                }
            }
        }

        [Test]
        public void TheAuthoredWarehouseAsset_HasTheDocumentedCapacities()
        {
            var authored = GameDatabase.Load().StructureOfKind(StructureKind.Warehouse);
            Assert.IsNotNull(authored, "the authored database has no warehouse");
            Assert.AreEqual(18, authored.StorageCapacity(1));
            Assert.AreEqual(54, authored.StorageCapacity(5));
        }
    }
}
