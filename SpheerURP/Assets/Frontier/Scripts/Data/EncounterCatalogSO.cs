using System;
using Frontier.Hyperspace;
using UnityEngine;

namespace Frontier
{
    [CreateAssetMenu(fileName = "EncounterCatalog", menuName = "Frontier/Hyperspace Encounter Catalog")]
    public sealed class EncounterCatalogSO : ScriptableObject
    {
        public EncounterSO[] encounters = Array.Empty<EncounterSO>();

        public EncounterLibrary BuildLibrary()
        {
            if (encounters == null) throw new InvalidOperationException("Hyperspace encounter catalog is missing its entries.");
            var templates = new EncounterTemplate[encounters.Length];
            for (int i = 0; i < encounters.Length; i++)
            {
                if (encounters[i] == null) throw new InvalidOperationException("Missing Hyperspace encounter at index " + i);
                templates[i] = encounters[i].template;
            }
            return EncounterLibrary.FromTemplates(templates);
        }
    }
}
