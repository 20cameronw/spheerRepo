using Frontier.Hyperspace;
using UnityEngine;

namespace Frontier
{
    [CreateAssetMenu(fileName = "Encounter", menuName = "Frontier/Hyperspace Encounter")]
    public sealed class EncounterSO : ScriptableObject
    {
        public EncounterTemplate template = new EncounterTemplate();
    }
}
