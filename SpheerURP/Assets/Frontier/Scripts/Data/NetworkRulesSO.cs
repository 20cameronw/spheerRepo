using System;
using UnityEngine;

namespace Frontier
{
    [Serializable]
    public sealed class NetworkCost
    {
        public string resourceId;
        public int amount;
    }

    [CreateAssetMenu(menuName = "Frontier/Economy/Network Rules")]
    public sealed class NetworkRulesSO : ContentSO
    {
        public string linkResearch = "warp_gate_engineering";
        public string finaleResearch = "stellar_engineering";
        public int gateLevel = 1;
        public int minimumWarehouses = 12;
        public int minimumStorageSlots = 4600;
        public string[] resourceWorlds;
        public NetworkCost[] linkCost;
        public NetworkCost[] finaleCost;

        public bool IsValid(GameDatabase db)
        {
            if (gateLevel < 1 || minimumWarehouses < 1 || minimumStorageSlots < 1 ||
                resourceWorlds == null || resourceWorlds.Length == 0) return false;
            var seen = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            foreach (string id in resourceWorlds)
                if (db.FindResource(id) == null || !seen.Add(id)) return false;
            bool linkGate = false, finaleGate = false;
            foreach (var node in db.research)
            {
                if (node == null || node.data == null) continue;
                linkGate |= node.data.id == linkResearch;
                finaleGate |= node.data.id == finaleResearch;
            }
            return ValidCost(db, linkCost) && ValidCost(db, finaleCost) &&
                linkGate && finaleGate && db.FindStructure("warp_gate") != null && db.FindStructure("cargo_depot") != null;
        }

        static bool ValidCost(GameDatabase db, NetworkCost[] costs)
        {
            if (costs == null || costs.Length == 0) return false;
            var seen = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            foreach (var cost in costs)
                if (cost == null || cost.amount <= 0 || db.FindResource(cost.resourceId) == null || !seen.Add(cost.resourceId)) return false;
            return true;
        }
    }
}
