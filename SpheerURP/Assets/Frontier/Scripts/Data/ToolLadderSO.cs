using Frontier.Economy;
using UnityEngine;
using System.Linq;

namespace Frontier
{
    [CreateAssetMenu(menuName = "Frontier/Economy/Tool Ladder")]
    public sealed class ToolLadderSO : ContentSO
    {
        public ToolLadderDef data;
        public string LocalizedName { get { return ContentText.Get("tool", data.id, "name", data.displayName); } }
        public string TierName(ToolTierDef tier) { return ContentText.Get("tool", data.id, "tier-" + tier.tier, tier.displayName); }
        public string icon;
        public Color color = Color.white;

        public ToolLadderDef ToDef()
        {
            return new ToolLadderDef
            {
                id = data.id, displayName = data.displayName, kind = data.kind, games = data.games,
                tiers = data.tiers.Select(t => new ToolTierDef
                {
                    tier = t.tier, displayName = t.displayName, yieldMult = t.yieldMult, runTimeMult = t.runTimeMult,
                    cost = t.cost, researchPrereq = ItemAmounts.NullIfEmpty(t.researchPrereq), requiresReachTier = t.requiresReachTier,
                    requiresLabLevel = t.requiresLabLevel, eraTier = t.eraTier
                }).ToArray()
            };
        }
    }
}
