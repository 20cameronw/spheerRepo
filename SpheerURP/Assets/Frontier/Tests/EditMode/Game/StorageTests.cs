using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    /// <summary>
    /// Planetary storage rules, ported from <c>Frontier &gt; Validate Warehouse Storage</c> (that editor check stays as the batch gate):
    /// capacity, stack limits, conservation of items, isolation between structures, upgrades, persistence and rejection rules.
    /// </summary>
    [TestFixture]
    public class StorageTests
    {
        SaveSandbox sandbox;
        GameDatabase db;
        GameState state;
        ResourceSO wood;
        PlacedStructure warehouse, second;
        StorageRecord storage;
        int total;

        [SetUp]
        public void SetUp()
        {
            sandbox = new SaveSandbox();
            db = DefaultContent.Create();
            state = new GameState(db, false);
            wood = db.FindResource("wood");
            var def = db.StructureOfKind(StructureKind.Warehouse);
            warehouse = state.DebugPlace(def, 1);
            second = state.DebugPlace(def, 2);
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
            state.DebugSetStructureLevel(warehouse, 5);
            var upgraded = state.StorageFor(warehouse);
            Assert.AreEqual(54, upgraded.slots.Count);
            Assert.Greater(upgraded.slots[1].count, 0);
        }

        [Test]
        public void Storage_SurvivesAJsonRoundTrip()
        {
            StockSlotOne();
            var saved = new PlanetDelta { planetId = warehouse.planetId };
            saved.storages.Add(storage);
            var loaded = JsonUtility.FromJson<PlanetDelta>(JsonUtility.ToJson(saved));
            Assert.AreEqual(warehouse.planetId, loaded.planetId);
            Assert.AreEqual(warehouse.slot, loaded.storages[0].structureSlot);
            Assert.AreEqual(storage.slots[1].count, loaded.storages[0].slots[1].count);
            Assert.AreEqual(wood.id, loaded.storages[0].slots[1].resourceId);
        }

        [Test]
        public void AWarehouseOnAnotherPlanet_CannotBeUsedFromHere()
        {
            StockSlotOne();
            state.DebugTeleport(state.universe.Get(db.planets[1].id));
            Assert.AreEqual(0, state.Deposit(warehouse, 2, wood, 1, false));
            Assert.AreEqual(0, state.Withdraw(warehouse, 1, 1, false));
        }

        [Test]
        public void FuelDepot_StoresOnlyFuelAndItsUpgradeExpandsFuelStacks()
        {
            state = new GameState(GameDatabase.Load(), false);
            db = state.db;
            var depot = state.DebugPlace(db.StructureOfKind(StructureKind.FuelDepot), 3);
            var fuel = db.FindResource("fuel");
            Assert.IsNotNull(depot, "fuel depot did not place");
            Assert.IsNotNull(fuel, "fuel resource is missing");
            Assert.AreEqual(StructureKind.FuelDepot, depot.def.kind);
            Assert.IsTrue(depot.def.kind.IsStorage());
            Assert.IsTrue(state.CanStore(depot, fuel), "fuel should be accepted by a fuel depot");
            Assert.IsTrue(state.CanStore(depot, db.FindResource("plasma")));
            Assert.IsTrue(state.CanStore(depot, db.FindResource("antimatter")));
            Assert.IsFalse(state.CanStore(depot, wood));
            Assert.IsFalse(state.CanStore(depot, db.scrap));

            state.DebugSetStructureLevel(depot, 2);
            state.Add(fuel, 10000, false);
            int held = state.Count(fuel);
            int expectedStack = Mathf.FloorToInt(fuel.stackSize * (1f + depot.def.EffectAt(depot.level)));
            Assert.GreaterOrEqual(held, expectedStack);
            Assert.AreEqual(expectedStack, state.Deposit(depot, 0, fuel, held, false));
            Assert.AreEqual(0, state.Deposit(depot, 1, wood, 1, false));
            Assert.AreEqual(expectedStack, state.StorageFor(depot).slots[0].count);
        }

        [Test]
        public void FuelDepotUpgradeUsesAuthoredFuelSlotCapacity()
        {
            var def = GameDatabase.Load().StructureOfKind(StructureKind.FuelDepot);
            Assert.IsNotNull(def, "the authored database has no fuel depot");
            Assert.AreEqual(6, def.StorageCapacity(1));
            Assert.AreEqual(9, def.StorageCapacity(2));
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
