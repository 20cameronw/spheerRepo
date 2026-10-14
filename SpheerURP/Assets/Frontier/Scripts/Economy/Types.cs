using System;
using Frontier.Universe;

namespace Frontier.Economy
{
    // =====================================================================================================================
    // Plain data types. Field names mirror the future ScriptableObject fields; ids are lower_snake strings.
    // Fractions are stored as float (SO friendly) but every computation runs in double (see Tuning / ValueModel).
    // =====================================================================================================================

    public enum ResourceCategory { Raw, Processed, Alloy, Component, Energy }

    public enum StructureRole { Gather, Process, Storage, Defense, Utility, Megaproject }

    public enum RecipeKind { Processing, Alloy, Component, Energy }

    /// <summary>Tool ladders. Scanner is the excavation tool (sonar scanner, seismic excavator ...).</summary>
    public enum ToolKind { Axe, Pick, Drill, Siphon, Scanner, Claw }

    /// <summary>Groups of non-tool upgrades (the game's UpgradeCategory has Tools/Ship/Defense; Logistics is new).</summary>
    public enum UpgradeGroup { Ship, Defense, Logistics }

    /// <summary>What an upgrade level changes. The first values keep the game's UpgradeEffect names.</summary>
    public enum EffectKind
    {
        BackpackSlots, FuelEfficiency, TurretFireRate, TurretDamage, TapDamage, ReactorHealth, RewardBonus,
        JumpRange, ScannerRange, FuelTankSlots, HullPlating, StackMultiplier
    }

    public enum LegacyKind { Resource, Structure, Upgrade }

    [Serializable]
    public struct ItemAmount
    {
        public string resourceId;
        public int amount;

        public ItemAmount(string resourceId, int amount)
        {
            this.resourceId = resourceId;
            this.amount = amount;
        }

        public override string ToString() { return amount + " " + resourceId; }
    }

    [Serializable]
    public sealed class ResourceDef
    {
        public string id;
        public string displayName;
        public ResourceCategory category;
        /// <summary>First planet tier on which the resource is useful or available.</summary>
        public int tier;
        /// <summary>Harvest methods that can yield it; null for crafted items.</summary>
        public HarvestMethod[] harvestMethods;
        /// <summary>Price in scrap-equivalents (scrap = 1): score weighting, shard sanity checks, cost generation.</summary>
        public float baseValue;
        /// <summary>Items per backpack slot: bulky low tiers stack small, dense high tiers stack large.</summary>
        public int stackSize;
        /// <summary>Hazard the item is found with or protects against (None for neutral goods).</summary>
        public Hazards hazardAffinity;
        /// <summary>Energy units when burned as ship fuel (Fuel Cell 1, Plasma Cell 8, Antimatter Pod 64); 0 = not a fuel.</summary>
        public int fuelEnergy;
        public string atlasDescription;
        public string iconConcept;
        public string colorHex;
        public string accentHex;
        // Raw resources only: deposit generation (feeds Frontier.Universe.ResourceDef).
        public float baseYield;
        public float yieldPerTier;
        public float difficultyOffset;
    }

    /// <summary>One row of a biome's (or asteroid belt's) resource profile: what can be found, how it is harvested, how likely, from which tier.</summary>
    [Serializable]
    public sealed class HabitatDef
    {
        public string resourceId;
        public Biome biome;
        public bool belt;
        public HarvestMethod method;
        public float weight;
        public int minTier;
    }

    [Serializable]
    public sealed class RecipeDef
    {
        public string id;
        public string displayName;
        public RecipeKind kind;
        /// <summary>Structure that runs the recipe (refinery, foundry, fabricator, chem_reactor, quantum_lab).</summary>
        public string stationId;
        public int stationLevel;
        public int tier;
        public ItemAmount[] inputs;
        public ItemAmount[] outputs;
        /// <summary>Research node that reveals the recipe; null when it is known from the start.</summary>
        public string unlockResearch;
        /// <summary>Explicit bonus recipes may exceed the value conservation ratio (none ship today).</summary>
        public bool bonus;
    }

    /// <summary>One level of a structure: what it costs to build or upgrade into it, how long that takes, and what it does.</summary>
    [Serializable]
    public sealed class StructureLevelDef
    {
        public int level;
        public ItemAmount[] cost;
        public int buildSeconds;
        /// <summary>Highest planet tier the player must have reached before this level can start.</summary>
        public int requiresReachTier;
        /// <summary>Research Lab level required (0 = none).</summary>
        public int requiresLabLevel;
        /// <summary>Primary effect at this level (yield bonus, output bonus, damage multiplier, builders, ...; see StructureDef.effectKind).</summary>
        public float effect;
        /// <summary>Warehouse/depot slots, or relay link radius in ly, depending on the structure.</summary>
        public int capacity;
    }

    [Serializable]
    public sealed class StructureDef
    {
        public string id;
        public string displayName;
        public StructureRole role;
        public int tier;
        public string description;
        /// <summary>Building slots (pads) occupied on a planet of any size class.</summary>
        public int footprint;
        /// <summary>0 = unlimited (limited by pads).</summary>
        public int maxPerPlanet;
        public int maxLevel;
        /// <summary>What <see cref="StructureLevelDef.effect"/> means (documented in ECONOMY.md).</summary>
        public string effectKind;
        /// <summary>What <see cref="StructureLevelDef.capacity"/> means (slots, link radius in ly, percent, ...); empty when unused.</summary>
        public string capacityKind;
        /// <summary>Harvest methods a gather structure serves (empty for other roles).</summary>
        public HarvestMethod[] gathers;
        /// <summary>Research node that unlocks the structure (null = available with the tier).</summary>
        public string unlockResearch;
        /// <summary>Reach tier needed to build level 1.</summary>
        public int unlockTier;
        public string legacyKind;
        // Turret data (legacy StructureSO fields), 0 for non-turrets.
        public float damage;
        public float fireInterval;
        public float range;
        public StructureLevelDef[] levels;
    }

    [Serializable]
    public sealed class ToolTierDef
    {
        public int tier;
        public string displayName;
        /// <summary>Yield multiplier of the harvest rewards of every method the ladder serves.</summary>
        public float yieldMult;
        /// <summary>Run-time multiplier (swings are faster with better tools); 1 = unchanged.</summary>
        public float runTimeMult;
        public ItemAmount[] cost;
        public string researchPrereq;
        public int requiresReachTier;
        public int requiresLabLevel;
        /// <summary>Pacing tier of the purchase: the later of its reach requirement and the tier of its research node.</summary>
        public int eraTier;
    }

    [Serializable]
    public sealed class ToolLadderDef
    {
        public string id;
        public string displayName;
        public ToolKind kind;
        public HarvestMethod[] games;
        public ToolTierDef[] tiers;
    }

    [Serializable]
    public sealed class UpgradeLevelDef
    {
        public int level;
        public string displayName;
        public ItemAmount[] cost;
        /// <summary>Cumulative effect value at this level (slots, ly, fuel factor, ...).</summary>
        public float value;
        public string researchPrereq;
        public int requiresReachTier;
        /// <summary>Pacing tier of the purchase: the later of its reach requirement and the tier of its research node.</summary>
        public int eraTier;
    }

    [Serializable]
    public sealed class UpgradeDef
    {
        public string id;
        public string displayName;
        public string description;
        public UpgradeGroup group;
        public EffectKind effect;
        public int maxLevel;
        /// <summary>Hazard a hull plating upgrade protects against (HullPlating only).</summary>
        public Hazards hazard;
        public UpgradeLevelDef[] levels;
    }

    [Serializable]
    public sealed class ResearchNodeDef
    {
        public string id;
        public string displayName;
        public string description;
        public string branch;
        public int tier;
        public ItemAmount[] cost;
        public string[] prereqs;
        public int labLevel;
        /// <summary>Things the node unlocks: "structure:foundry", "recipe:...", "tool:axe:4", "upgrade:jump:3", "feature:autopilot".</summary>
        public string[] unlocks;
    }

    [Serializable]
    public sealed class LegacyIdEntry
    {
        public LegacyKind kind;
        public string legacyId;
        public string id;
    }

    [Serializable]
    public sealed class StartDef
    {
        public ItemAmount[] startingItems;
        public int baseBackpackSlots;
        public string startPlanetId;
        public int builders;
    }
}
