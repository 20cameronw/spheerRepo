using UnityEngine;
using Frontier.Economy;
using Frontier.Universe;
using ResourceDef = Frontier.Economy.ResourceDef;

namespace Frontier
{
    /// <summary>
    /// One resource. The catalog (docs/economy) owns the numbers and the category; the colour, the legacy blurb and the optional icon override are authored.
    /// Ids are never renamed once a save can contain them (the legacy ids of the first game are mapped by <see cref="LegacyIdMapSO"/>).
    /// </summary>
    [CreateAssetMenu(fileName = "New Resource", menuName = "Frontier/Resource")]
    public class ResourceSO : ContentSO
    {
        public string id;
        public string displayName;
        [ColorUsage(false, true)] public Color color = Color.white;
        [Tooltip("How many of this resource fit in one backpack slot.")]
        public int stackSize = 20;
        [TextArea] public string description;

        [Header("Economy catalog (imported)")]
        public ResourceCategory category;
        [Tooltip("First planet tier on which the resource is useful or available.")]
        public int tier;
        [Tooltip("Harvest methods that can yield it; empty for crafted items.")]
        public HarvestMethod[] harvestMethods = new HarvestMethod[0];
        [Tooltip("Price in scrap-equivalents (scrap = 1).")]
        public float baseValue = 1f;
        public Hazards hazardAffinity;
        [Tooltip("Energy units when burned as ship fuel (0 = not a fuel).")]
        public int fuelEnergy;
        [Tooltip("The catalog's colour family and accent, as hex (the in-game Color above is authored).")]
        public string colorHex = "#FFFFFF";
        public string accentHex = "#FFFFFF";
        [Tooltip("Raw resources: deposit generation (base yield, yield per planet tier, difficulty offset).")]
        public float baseYield;
        public float yieldPerTier;
        public float difficultyOffset;
        [TextArea] public string atlasDescription;
        [TextArea, Tooltip("The silhouette brief the icon was drawn from.")] public string iconConcept;

        [Header("Authored")]
        [Tooltip("Icon file name in Resources/Frontier/Icons; empty means the resource id.")]
        public string icon;

        public bool IsRaw { get { return category == ResourceCategory.Raw; } }

        public string IconName { get { return string.IsNullOrEmpty(icon) ? id : icon; } }

        /// <summary>The text shown in the backpack and the Atlas: the catalog text, or the legacy blurb when none was imported.</summary>
        public string DetailText { get { return !string.IsNullOrEmpty(atlasDescription) ? atlasDescription : description; } }

        public ResourceDef ToDef()
        {
            return new ResourceDef
            {
                id = id, displayName = displayName, category = category, tier = tier, harvestMethods = ItemAmounts.CloneOrNull(harvestMethods),
                baseValue = baseValue, stackSize = stackSize, hazardAffinity = hazardAffinity, fuelEnergy = fuelEnergy, atlasDescription = atlasDescription,
                iconConcept = iconConcept, colorHex = colorHex, accentHex = accentHex, baseYield = baseYield, yieldPerTier = yieldPerTier, difficultyOffset = difficultyOffset
            };
        }
    }
}
