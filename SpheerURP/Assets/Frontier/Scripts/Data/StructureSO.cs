using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    [CreateAssetMenu(fileName = "New Structure", menuName = "Frontier/Structure")]
    public class StructureSO : ScriptableObject
    {
        public string id;
        public string displayName;
        [TextArea] public string description;
        public StructureKind kind;
        [ColorUsage(false, true)] public Color color = new Color(0.4f, 1.6f, 2f);
        [Tooltip("Key into the ModelLibrary for the hand-made model.")]
        public string modelKey;
        public List<ItemAmount> buildCost = new List<ItemAmount>();
        public int maxLevel = 5;
        public float upgradeGrowth = 1.9f;
        [Tooltip("Per level above 1: refinery output, alarm reward/xp, gather yield or turret damage bonus.")]
        public float bonusPerLevel = 0.25f;
        [Tooltip("How many of this structure fit on one planet. 0 = unlimited (limited by pads).")]
        public int maxPerPlanet = 1;
        [Header("Turrets")]
        public float damage = 6f;
        public float fireInterval = 0.7f;
        public float range = 8f;
        public float beamWidth = 0.07f;
        [Header("Warehouse")]
        public int storageSlots = 18;
        public int storageSlotsPerLevel = 9;

        public int StorageCapacity(int level)
        {
            return Mathf.Max(1, storageSlots + Mathf.Max(0, level - 1) * storageSlotsPerLevel);
        }

        public List<ItemAmount> UpgradeCost(int currentLevel)
        {
            var list = new List<ItemAmount>();
            float k = Mathf.Pow(upgradeGrowth, Mathf.Max(0, currentLevel - 1));
            foreach (var c in buildCost)
                list.Add(new ItemAmount(c.resource, Mathf.Max(1, Mathf.RoundToInt(c.amount * 0.8f * k))));
            return list;
        }
    }
}
