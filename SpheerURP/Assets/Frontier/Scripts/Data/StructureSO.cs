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
        [Tooltip("Per level above 1: refinery output bonus, or alarm reward/xp bonus.")]
        public float bonusPerLevel = 0.25f;

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
