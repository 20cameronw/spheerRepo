using Frontier.Economy;
using UnityEngine;

namespace Frontier
{
    [CreateAssetMenu(menuName = "Frontier/Economy/Research Node")]
    public sealed class ResearchNodeSO : ContentSO
    {
        public ResearchNodeDef data;
        public string icon = "research";
    }
}
