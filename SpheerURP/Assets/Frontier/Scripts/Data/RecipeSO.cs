using System.Collections.Generic;
using UnityEngine;
using Frontier.Economy;

namespace Frontier
{
    /// <summary>
    /// A station recipe with level/research gates enforced by the selected economy runtime.
    /// Legacy/custom databases still use the fabricator flag.
    /// </summary>
    [CreateAssetMenu(fileName = "New Recipe", menuName = "Frontier/Recipe")]
    public class RecipeSO : ContentSO
    {
        [Tooltip("lower_snake id of the catalog (distill_wood, smelt_steel ...). Never rename once saves can refer to it.")]
        public string id;
        public string displayName;
        public string LocalizedName { get { return ContentText.Get("recipe", id, "name", displayName); } }
        public List<ItemAmount> inputs = new List<ItemAmount>();
        public List<ItemAmount> outputs = new List<ItemAmount>();
        [Tooltip("Legacy flag: crafted at a Fabricator instead of a Refinery. The importer derives it from the station.")]
        public bool fabricator;

        [Header("Economy catalog (imported)")]
        public RecipeKind kind;
        [Tooltip("Structure id that runs the recipe.")]
        public string stationId = "refinery";
        public int stationLevel = 1;
        [Tooltip("Required player XP level.")]
        public int tier;
        [Tooltip("Research node that reveals the recipe; empty when it is known from the start.")]
        public string unlockResearch;
        [Tooltip("Explicit bonus recipes may exceed the value conservation ratio (none ship today).")]
        public bool bonus;

        public RecipeDef ToDef()
        {
            return new RecipeDef
            {
                id = id, displayName = displayName, kind = kind, stationId = stationId, stationLevel = stationLevel, tier = tier,
                inputs = ItemAmounts.ToIds(inputs), outputs = ItemAmounts.ToIds(outputs), unlockResearch = ItemAmounts.NullIfEmpty(unlockResearch), bonus = bonus
            };
        }
    }
}
