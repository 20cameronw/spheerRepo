using System;
using System.Collections.Generic;
using UnityEngine;
using Frontier.Economy;
using Frontier.Universe;

namespace Frontier
{
    /// <summary>One level of a structure: the cost to build or upgrade into it, the (stored, not yet enforced) build time, requirements and effect.</summary>
    [Serializable]
    public class StructureLevel
    {
        public int level = 1;
        public List<ItemAmount> cost = new List<ItemAmount>();
        public int buildSeconds;
        [Tooltip("Highest planet tier the player must have reached before this level can start.")]
        public int requiresReachTier;
        [Tooltip("Research Lab level required (0 = none).")]
        public int requiresLabLevel;
        [Tooltip("Primary effect at this level; what it means is StructureSO.effectKind (yieldBonus, batchSize, damageMult, ...).")]
        public float effect;
        [Tooltip("Storage slots, or the structure's own capacity (see StructureSO.capacityKind).")]
        public int capacity;
    }

    [CreateAssetMenu(fileName = "New Structure", menuName = "Frontier/Structure")]
    public class StructureSO : ContentSO
    {
        public string id;
        public string displayName;
        [TextArea] public string description;
        [Tooltip("Behaviour of the structure; the importer derives it from the id (StructureKindMap).")]
        public StructureKind kind;
        [ColorUsage(false, true)] public Color color = new Color(0.4f, 1.6f, 2f);
        [Tooltip("Key into the ModelLibrary for the hand-made model. Empty: the structure gets the procedural kit-bash model of its kind.")]
        public string modelKey;
        [Tooltip("Level 1 cost (the importer copies it from Levels).")]
        public List<ItemAmount> buildCost = new List<ItemAmount>();
        public int maxLevel = 5;
        [Tooltip("Legacy cost growth, used only while Levels is empty.")]
        public float upgradeGrowth = 1.9f;
        [Tooltip("Legacy per level bonus, used only while Levels is empty.")]
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

        [Header("Economy catalog (imported)")]
        public StructureRole role;
        public int tier;
        [Tooltip("Building slots (pads) occupied on a planet of any size class.")]
        public int footprint = 1;
        [Tooltip("What StructureLevel.effect means (yieldBonus, batchSize, damageMult, labLevel ...).")]
        public string effectKind;
        [Tooltip("What StructureLevel.capacity means (storageSlots, energySlots, linkRadiusLy ...); empty when unused.")]
        public string capacityKind;
        public HarvestMethod[] gathers = new HarvestMethod[0];
        [Tooltip("Research node that unlocks the structure; empty = available with the tier.")]
        public string unlockResearch;
        [Tooltip("Reach tier needed to build level 1.")]
        public int unlockTier;
        [Tooltip("The StructureKind name of the first game's structure, when it had one.")]
        public string legacyKind;
        public List<StructureLevel> levels = new List<StructureLevel>();

        public bool HasLevelRows { get { return levels != null && levels.Count > 0; } }

        /// <summary>Classifies existing behaviours; catalog-only kinds cannot be bought until gameplay is implemented.</summary>
        public bool Implemented { get { return kind.IsImplemented(); } }

        /// <summary>The row of a level (1 based, clamped); null when the structure has no rows.</summary>
        public StructureLevel Row(int level)
        {
            if (!HasLevelRows) return null;
            return levels[Mathf.Clamp(level, 1, levels.Count) - 1];
        }

        /// <summary>The cost to build level 1 or to upgrade into <paramref name="level"/>.</summary>
        public List<ItemAmount> CostOfLevel(int level)
        {
            var row = Row(level);
            return row != null ? new List<ItemAmount>(row.cost) : (level <= 1 ? new List<ItemAmount>(buildCost) : UpgradeCost(level - 1));
        }

        /// <summary>The effect value of a level (see <see cref="effectKind"/>); legacy structures use bonusPerLevel x (level - 1).</summary>
        public float EffectAt(int level)
        {
            var row = Row(level);
            return row != null ? row.effect : bonusPerLevel * Mathf.Max(0, level - 1);
        }

        public int CapacityAt(int level)
        {
            var row = Row(level);
            return row != null ? row.capacity : 0;
        }

        public int StorageCapacity(int level)
        {
            if (HasLevelRows && Row(level).capacity > 0) return Row(level).capacity;
            return Mathf.Max(1, storageSlots + Mathf.Max(0, level - 1) * storageSlotsPerLevel);
        }

        /// <summary>The cost to upgrade from <paramref name="currentLevel"/> to the next level.</summary>
        public List<ItemAmount> UpgradeCost(int currentLevel)
        {
            if (HasLevelRows)
            {
                var row = levels[Mathf.Clamp(currentLevel + 1, 1, levels.Count) - 1];
                return new List<ItemAmount>(row.cost);
            }
            var list = new List<ItemAmount>();
            float k = Mathf.Pow(upgradeGrowth, Mathf.Max(0, currentLevel - 1));
            foreach (var c in buildCost)
                list.Add(new ItemAmount(c.resource, Mathf.Max(1, Mathf.RoundToInt(c.amount * 0.8f * k))));
            return list;
        }

        public StructureDef ToDef()
        {
            var rows = new StructureLevelDef[levels == null ? 0 : levels.Count];
            for (int i = 0; i < rows.Length; i++)
            {
                var l = levels[i];
                rows[i] = new StructureLevelDef
                {
                    level = l.level, cost = ItemAmounts.ToIds(l.cost), buildSeconds = l.buildSeconds, requiresReachTier = l.requiresReachTier,
                    requiresLabLevel = l.requiresLabLevel, effect = l.effect, capacity = l.capacity
                };
            }
            return new StructureDef
            {
                id = id, displayName = displayName, role = role, tier = tier, description = description, footprint = footprint, maxPerPlanet = maxPerPlanet,
                maxLevel = rows.Length > 0 ? rows.Length : maxLevel, effectKind = effectKind ?? "", capacityKind = capacityKind ?? "",
                gathers = ItemAmounts.CloneOrNull(gathers) ?? new HarvestMethod[0], unlockResearch = ItemAmounts.NullIfEmpty(unlockResearch), unlockTier = unlockTier,
                legacyKind = legacyKind ?? "", damage = damage, fireInterval = fireInterval, range = range, levels = rows
            };
        }
    }
}
