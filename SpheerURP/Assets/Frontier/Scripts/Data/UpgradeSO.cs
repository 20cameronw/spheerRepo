using System;
using System.Collections.Generic;
using UnityEngine;
using Frontier.Economy;
using Frontier.Universe;

namespace Frontier
{
    [Serializable]
    public class CostTier { public List<ItemAmount> cost = new List<ItemAmount>(); }

    /// <summary>One purchasable level of an upgrade: its name, cost, cumulative effect value and what gates it.</summary>
    [Serializable]
    public class UpgradeLevel
    {
        public int level = 1;
        public string displayName;
        public List<ItemAmount> cost = new List<ItemAmount>();
        [Tooltip("Cumulative effect at this level: total slots, ly, a fuel factor, an added fraction ... (see UpgradeSO.effect).")]
        public float value;
        [Tooltip("Research node that must be done before this level can be bought; empty = none.")]
        public string researchPrereq;
        [Tooltip("Required player XP level. Serialized name retained for existing content.")]
        public int requiresReachTier;
        [Tooltip("Pacing tier of the purchase (reach and research combined); informational.")]
        public int eraTier;
    }

    /// <summary>
    /// A ship, defense or logistics upgrade. The per-level rows (<see cref="levels"/>) come from the economy catalog; while a legacy asset has none,
    /// the older baseCost, costGrowth and valuePerLevel fields still work. Tools are <see cref="ToolLadderSO"/>, not upgrades.
    /// </summary>
    [CreateAssetMenu(fileName = "New Upgrade", menuName = "Frontier/Upgrade")]
    public class UpgradeSO : ContentSO
    {
        public string id;
        public string displayName;
        public string LocalizedName { get { return ContentText.Get("upgrade", id, "name", displayName); } }
        public string LocalizedDescription { get { return ContentText.Get("upgrade", id, "description", description); } }
        [TextArea] public string description;
        public UpgradeCategory category;
        public UpgradeEffect effect;
        [Tooltip("Legacy ToolPower upgrade target; retained until the tool-tier save/runtime migration.")]
        public MinigameKind tool;
        [Tooltip("Legacy effect per level, used only while Levels is empty.")]
        public float valuePerLevel = 0.5f;
        public int maxLevel = 5;
        [Tooltip("Legacy cost, used only while Levels is empty.")]
        public List<ItemAmount> baseCost = new List<ItemAmount>();
        [Tooltip("Legacy cost multiplier per level owned.")]
        public float costGrowth = 1.8f;
        [ColorUsage(false, true)] public Color color = Color.cyan;
        [Tooltip("Legacy names per level, used only while Levels is empty.")]
        public string[] levelNames;
        [Tooltip("Legacy explicit cost per level.")]
        public List<CostTier> levelCosts = new List<CostTier>();

        [Header("Economy catalog (imported)")]
        public UpgradeGroup group;
        [Tooltip("The hazard a hull plating upgrade protects against (HullPlating only).")]
        public Hazards hazard;
        public List<UpgradeLevel> levels = new List<UpgradeLevel>();

        [Header("Authored")]
        [Tooltip("Icon file name in Resources/Frontier/Icons; empty means the upgrade id.")]
        public string icon;

        public string IconName { get { return string.IsNullOrEmpty(icon) ? id : icon; } }

        public bool HasLevelRows { get { return levels != null && levels.Count > 0; } }

        public int MaxLevel { get { return HasLevelRows ? levels.Count : maxLevel; } }

        /// <summary>The row bought when going from <paramref name="currentLevel"/> up one (null at the maximum or without rows).</summary>
        public UpgradeLevel NextRow(int currentLevel)
        {
            return HasLevelRows && currentLevel >= 0 && currentLevel < levels.Count ? levels[currentLevel] : null;
        }

        /// <summary>The cost of the next level when <paramref name="level"/> levels are owned.</summary>
        public List<ItemAmount> CostAt(int level)
        {
            var row = NextRow(level);
            if (row != null) return new List<ItemAmount>(row.cost);
            if (HasLevelRows) return new List<ItemAmount>();
            if (levelCosts != null && level >= 0 && level < levelCosts.Count && levelCosts[level] != null)
                return new List<ItemAmount>(levelCosts[level].cost);
            var list = new List<ItemAmount>();
            float mult = Mathf.Pow(costGrowth, level);
            foreach (var c in baseCost)
                list.Add(new ItemAmount(c.resource, Mathf.Max(1, Mathf.CeilToInt(c.amount * mult))));
            return list;
        }

        /// <summary>The cumulative effect value when <paramref name="level"/> levels are owned; <paramref name="atZero"/> is the value without the upgrade.</summary>
        public float ValueAt(int level, float atZero = 0f)
        {
            if (level <= 0) return atZero;
            if (HasLevelRows) return levels[Mathf.Min(level, levels.Count) - 1].value;
            return valuePerLevel * level;
        }

        /// <summary>Contribution in the additive units expected by legacy gameplay (catalog pack/fuel rows are absolute values).</summary>
        public float ContributionAt(int level, int baseSlots)
        {
            if (level <= 0) return 0f;
            float value = ValueAt(level);
            if (HasLevelRows && effect == UpgradeEffect.BackpackSlots) return Mathf.Max(0f, value - baseSlots);
            if (HasLevelRows && effect == UpgradeEffect.FuelEfficiency) return 1f - value;
            return value;
        }

        public string NameAtLevel(int level)
        {
            if (HasLevelRows)
            {
                if (level <= 0) return LocalizedName;
                var row = levels[Mathf.Min(level, levels.Count) - 1];
                return string.IsNullOrEmpty(row.displayName) ? LocalizedName
                    : ContentText.Get("upgrade", id, "level-" + row.level, row.displayName);
            }
            if (levelNames != null && levelNames.Length > 0)
            {
                int index = Mathf.Clamp(level, 0, levelNames.Length - 1);
                return ContentText.Get("upgrade", id, "legacy-level-" + index, levelNames[index]);
            }
            return LocalizedName;
        }

        public UpgradeDef ToDef()
        {
            var rows = new UpgradeLevelDef[levels == null ? 0 : levels.Count];
            for (int i = 0; i < rows.Length; i++)
            {
                var l = levels[i];
                rows[i] = new UpgradeLevelDef
                {
                    level = l.level, displayName = l.displayName, cost = ItemAmounts.ToIds(l.cost), value = l.value,
                    researchPrereq = ItemAmounts.NullIfEmpty(l.researchPrereq), requiresReachTier = l.requiresReachTier, eraTier = l.eraTier
                };
            }
            return new UpgradeDef
            {
                id = id, displayName = displayName, description = description, group = group, effect = (EffectKind)Enum.Parse(typeof(EffectKind), effect.ToString()),
                maxLevel = rows.Length > 0 ? rows.Length : maxLevel, hazard = hazard, levels = rows
            };
        }
    }
}
