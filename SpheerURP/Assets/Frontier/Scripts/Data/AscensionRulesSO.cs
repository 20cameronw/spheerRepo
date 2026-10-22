using Frontier.Economy;
using UnityEngine;

namespace Frontier
{
    [CreateAssetMenu(menuName = "Frontier/Economy/Ascension Rules")]
    public sealed class AscensionRulesSO : ContentSO
    {
        public bool enableAscension;
        public AscensionRules data = new AscensionRules();
    }
}
