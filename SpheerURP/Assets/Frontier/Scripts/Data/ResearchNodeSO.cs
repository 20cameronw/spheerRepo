using Frontier.Economy;
using UnityEngine;

namespace Frontier
{
    [CreateAssetMenu(menuName = "Frontier/Economy/Research Node")]
    public sealed class ResearchNodeSO : ContentSO
    {
        public ResearchNodeDef data;
        public string LocalizedName { get { return ContentText.Get("research", data.id, "name", data.displayName); } }
        public string LocalizedDescription { get { return ContentText.Get("research", data.id, "description", data.description); } }
        public string icon = "research";
    }
}
