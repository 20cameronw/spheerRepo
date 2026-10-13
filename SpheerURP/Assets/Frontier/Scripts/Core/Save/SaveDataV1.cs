using System;
using System.Collections.Generic;

namespace Frontier
{
    // The legacy v1 save format: one flat record with parallel lists, planet ids are the authored ids ("sylva"), no schema field.
    // These classes exist only so v1 files can still be read and migrated (SaveMigrator); the game never writes them any more.
    // JsonUtility matches fields by name, so the class names are free to differ from the original ones.

    [Serializable]
    public class StoredStackV1
    {
        public string resourceId;
        public int count;
    }

    [Serializable]
    public class WarehouseStorageV1
    {
        public string planetId;
        public int structureSlot;
        public List<StoredStackV1> slots = new List<StoredStackV1>();
    }

    /// <summary>The v1 save file (the original GameState save), kept as the legacy input of the migrator.</summary>
    [Serializable]
    public class SaveDataV1
    {
        public List<string> itemIds = new List<string>();
        public List<int> itemCounts = new List<int>();
        public List<string> upgradeIds = new List<string>();
        public List<int> upgradeLevels = new List<int>();
        public List<string> waveIds = new List<string>();
        public List<int> waveCounts = new List<int>();
        public List<string> structPlanets = new List<string>();
        public List<string> structIds = new List<string>();
        public List<int> structSlots = new List<int>();
        public List<int> structLevels = new List<int>();
        public int xp;
        public string planetId;
        public int totalWavesCleared;
        public List<WarehouseStorageV1> warehouses = new List<WarehouseStorageV1>();
    }
}
