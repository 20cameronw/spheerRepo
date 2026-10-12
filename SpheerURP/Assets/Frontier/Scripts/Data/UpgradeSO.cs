using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    [CreateAssetMenu(fileName = "New Upgrade", menuName = "Frontier/Upgrade")]
    public class UpgradeSO : ScriptableObject
    {
        public string id;
        public string displayName;
        [TextArea] public string description;
        public UpgradeCategory category;
        public UpgradeEffect effect;
        [Tooltip("Only used by ToolPower.")]
        public MinigameKind tool;
        [Tooltip("Effect added per level.")]
        public float valuePerLevel = 0.5f;
        public int maxLevel = 5;
        public List<ItemAmount> baseCost = new List<ItemAmount>();
        [Tooltip("Cost multiplier per level owned.")]
        public float costGrowth = 1.8f;
        [ColorUsage(false, true)] public Color color = Color.cyan;
        [Tooltip("Optional names per level, e.g. Stone Axe, Iron Axe...")]
        public string[] levelNames;

        public List<ItemAmount> CostAt(int level)
        {
            var list = new List<ItemAmount>();
            float mult = Mathf.Pow(costGrowth, level);
            foreach (var c in baseCost)
                list.Add(new ItemAmount(c.resource, Mathf.Max(1, Mathf.CeilToInt(c.amount * mult))));
            return list;
        }

        public string NameAtLevel(int level)
        {
            if (levelNames != null && levelNames.Length > 0)
                return levelNames[Mathf.Clamp(level, 0, levelNames.Length - 1)];
            return displayName;
        }
    }
}
