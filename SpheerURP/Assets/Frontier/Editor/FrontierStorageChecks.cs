#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace Frontier.EditorTools
{
    public static class FrontierStorageChecks
    {
        [MenuItem("Frontier/Validate Warehouse Storage")]
        public static void Run()
        {
            CheckSupport.IsolateSaves();
            var previous = GameState.I;
            try
            {
                var db = DefaultContent.Create();
                var state = new GameState(db, false);
                var def = db.StructureOfKind(StructureKind.Warehouse);
                var wood = db.FindResource("wood");
                var warehouse = new PlacedStructure { planetId = state.planet.id, slot = 1, def = def };
                var second = new PlacedStructure { planetId = state.planet.id, slot = 2, def = def };
                state.structures.Add(warehouse); state.structures.Add(second);
                var storage = state.StorageFor(warehouse);
                Check(storage.slots.Count == 18, "Initial capacity.");
                state.Add(wood, 120, false);
                int total = state.Count(wood);
                Check(state.Deposit(warehouse, 0, wood, 100, false) == wood.stackSize, "Stack capacity.");
                Check(state.Count(wood) + storage.slots[0].count == total, "Deposit conservation.");
                Check(state.Deposit(warehouse, 0, wood, 1, false) == 0, "Full slot rejected.");
                Check(state.Deposit(warehouse, 0, db.scrap, 1, false) == 0, "Different resource rejected.");
                Check(state.MoveStoredSlot(warehouse, 0, 1, false), "Rearrangement.");
                Check(storage.slots[0].count == 0 && storage.slots[1].count == wood.stackSize, "Rearranged counts.");
                Check(state.Withdraw(warehouse, 1, 20, false) == 20, "Withdrawal.");
                Check(state.Count(wood) + storage.slots[1].count == total, "Withdrawal conservation.");
                Check(state.StorageFor(second).slots[1].count == 0, "Warehouse isolation.");
                warehouse.level = 5;
                Check(state.StorageFor(warehouse).slots.Count == 54 && storage.slots[1].count > 0, "Upgrade preserves storage.");
                var saved = new SaveData();
                saved.warehouses.Add(storage);
                var loaded = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(saved));
                Check(loaded.warehouses[0].planetId == warehouse.planetId &&
                    loaded.warehouses[0].structureSlot == warehouse.slot &&
                    loaded.warehouses[0].slots[1].count == storage.slots[1].count &&
                    loaded.warehouses[0].slots[1].resourceId == wood.id, "Serialized storage round trip.");
                state.planet = db.planets[1];
                Check(state.Deposit(warehouse, 2, wood, 1, false) == 0 && state.Withdraw(warehouse, 1, 1, false) == 0, "Remote access rejected.");
                state.planet = db.startPlanet;
                foreach (var resource in db.resources) state.Add(resource, 100000, false);
                Check(state.Withdraw(warehouse, 1, 1, false) == 0, "Full backpack rejected.");
                var authored = GameDatabase.Load().StructureOfKind(StructureKind.Warehouse);
                Check(authored != null && authored.StorageCapacity(1) == 18 && authored.StorageCapacity(5) == 54, "Authored warehouse asset.");
                Debug.Log("Frontier: warehouse checks passed (capacity, transfers, conservation, isolation, upgrades, persistence, full backpack).");
            }
            finally { GameState.I = previous; }
        }

        static void Check(bool value, string label)
        {
            if (!value) throw new InvalidOperationException("Warehouse check failed: " + label);
        }
    }
}
#endif
