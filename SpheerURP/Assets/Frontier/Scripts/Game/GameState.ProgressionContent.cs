using System;
using System.Collections.Generic;
using Frontier.Economy;
using Frontier.Universe;
using EItem = Frontier.Economy.ItemAmount;

namespace Frontier
{
    public partial class GameState
    {
        readonly HashSet<string> researched = new HashSet<string>(StringComparer.Ordinal);
        readonly Dictionary<string, int> toolTiers = new Dictionary<string, int>(StringComparer.Ordinal);

        public IEnumerable<string> ResearchedIds { get { return researched; } }
        public IEnumerable<KeyValuePair<string, int>> ToolTiers { get { return toolTiers; } }

        public bool IsResearched(string id)
        {
            return !string.IsNullOrEmpty(id) && researched.Contains(id);
        }

        public string ResearchLockReason(ResearchNodeSO node)
        {
            if (node == null || node.data == null || !ContainsResearch(node)) return "Unknown research";
            if (!db.UsesImportedEconomy) return "Research is unavailable in this database";
            if (IsResearched(node.data.id)) return "Already researched";
            if (node.data.tier > ReachTier) return "Reach planet tier " + node.data.tier;
            if (node.data.labLevel > 0)
            {
                var lab = StructureOfKind(planet, StructureKind.ResearchLab);
                if (lab == null || lab.level < node.data.labLevel)
                    return "Requires Research Lab level " + node.data.labLevel + " on this world";
            }
            if (node.data.prereqs != null)
                foreach (string prereq in node.data.prereqs)
                    if (!IsResearched(prereq)) return "Requires " + DisplayId(prereq);
            if (!TryBuildCost(node.data.cost, out var cost)) return "Invalid research cost";
            return Has(cost) ? null : "Missing research materials";
        }

        public bool CanResearch(ResearchNodeSO node)
        {
            return ResearchLockReason(node) == null;
        }

        public bool Research(ResearchNodeSO node)
        {
            if (!CanResearch(node) || !TryBuildCost(node.data.cost, out var cost) || !SpendQuietly(cost)) return false;
            researched.Add(node.data.id);
            Notify();
            return true;
        }

        public int ToolTier(ToolLadderSO ladder)
        {
            if (ladder == null || ladder.data == null) return 0;
            int tier;
            return toolTiers.TryGetValue(ladder.data.id, out tier) ? tier : 1;
        }

        public string ToolTierLockReason(ToolLadderSO ladder)
        {
            if (!IsSupportedTool(ladder)) return "This tool's harvest game is not implemented";
            int current = ToolTier(ladder);
            var next = FindToolTier(ladder, current + 1);
            if (next == null) return "Maximum tool tier";
            if (next.requiresReachTier > ReachTier) return "Reach planet tier " + next.requiresReachTier;
            if (!string.IsNullOrEmpty(next.researchPrereq) && !IsResearched(next.researchPrereq))
                return "Requires " + DisplayId(next.researchPrereq);
            if (next.requiresLabLevel > 0)
            {
                var lab = StructureOfKind(planet, StructureKind.ResearchLab);
                if (lab == null || lab.level < next.requiresLabLevel)
                    return "Requires Research Lab level " + next.requiresLabLevel + " on this world";
            }
            if (!TryBuildCost(next.cost, out var cost)) return "Invalid tool cost";
            return Has(cost) ? null : "Missing tool materials";
        }

        public bool BuyToolTier(ToolLadderSO ladder)
        {
            if (ToolTierLockReason(ladder) != null) return false;
            int nextTier = ToolTier(ladder) + 1;
            var row = FindToolTier(ladder, nextTier);
            if (!TryBuildCost(row.cost, out var cost) || !SpendQuietly(cost)) return false;
            toolTiers[ladder.data.id] = nextTier;
            Notify();
            return true;
        }

        public float CatalogToolYieldMultiplier(MinigameKind game)
        {
            if (!db.UsesImportedEconomy || db.toolLadders == null) return 1f;
            HarvestMethod method = HarvestMapping.FromMinigame(game);
            foreach (var ladder in db.toolLadders)
            {
                if (!IsSupportedTool(ladder) || ladder.data.games == null ||
                    Array.IndexOf(ladder.data.games, method) < 0) continue;
                var row = FindToolTier(ladder, ToolTier(ladder));
                if (row != null) return Math.Max(1f, row.yieldMult);
            }
            return 1f;
        }

        void ClearProgressionContent()
        {
            researched.Clear();
            toolTiers.Clear();
        }

        void LoadProgressionContent(PlayerSave player, List<string> warnings)
        {
            if (!db.UsesImportedEconomy)
            {
                foreach (string id in player.researched) researched.Add(id);
                foreach (var entry in player.toolTiers) toolTiers[entry.id] = entry.level;
                return;
            }
            var nodes = new HashSet<string>(StringComparer.Ordinal);
            if (db.research != null)
                foreach (var node in db.research)
                    if (node != null && node.data != null && !string.IsNullOrEmpty(node.data.id)) nodes.Add(node.data.id);
            foreach (string id in player.researched)
            {
                if (nodes.Contains(id)) researched.Add(id);
                else warnings.Add("dropped the unknown research node '" + id + "'");
            }
            var ladders = new Dictionary<string, ToolLadderSO>(StringComparer.Ordinal);
            if (db.toolLadders != null)
                foreach (var ladder in db.toolLadders)
                    if (ladder != null && ladder.data != null && !string.IsNullOrEmpty(ladder.data.id))
                        ladders[ladder.data.id] = ladder;
            foreach (var entry in player.toolTiers)
            {
                ToolLadderSO ladder;
                if (!ladders.TryGetValue(entry.id, out ladder))
                {
                    warnings.Add("dropped the unknown tool ladder '" + entry.id + "'");
                    continue;
                }
                int maxTier = MaxToolTier(ladder);
                int level = Math.Min(entry.level, maxTier);
                if (level != entry.level) warnings.Add("clamped the saved tier for tool '" + entry.id + "'");
                if (level > 1) toolTiers[entry.id] = level;
            }
        }

        bool ContainsResearch(ResearchNodeSO node)
        {
            return db.research != null && Array.IndexOf(db.research, node) >= 0;
        }

        bool IsSupportedTool(ToolLadderSO ladder)
        {
            if (!db.UsesImportedEconomy || ladder == null || ladder.data == null ||
                db.toolLadders == null || Array.IndexOf(db.toolLadders, ladder) < 0 ||
                ladder.data.tiers == null || ladder.data.tiers.Length == 0) return false;
            return ladder.data.kind == ToolKind.Axe || ladder.data.kind == ToolKind.Pick || ladder.data.kind == ToolKind.Drill;
        }

        static ToolTierDef FindToolTier(ToolLadderSO ladder, int tier)
        {
            if (ladder == null || ladder.data == null || ladder.data.tiers == null) return null;
            foreach (var row in ladder.data.tiers)
                if (row != null && row.tier == tier) return row;
            return null;
        }

        static int MaxToolTier(ToolLadderSO ladder)
        {
            int max = 1;
            if (ladder == null || ladder.data == null || ladder.data.tiers == null) return max;
            foreach (var row in ladder.data.tiers)
                if (row != null && row.tier > max) max = row.tier;
            return max;
        }

        bool TryBuildCost(EItem[] items, out List<ItemAmount> cost)
        {
            cost = new List<ItemAmount>();
            if (items == null) return true;
            foreach (var item in items)
            {
                var resource = db.ResolveResource(item.resourceId);
                if (resource == null || item.amount <= 0) return false;
                cost.Add(new ItemAmount(resource, item.amount));
            }
            return true;
        }

        static string DisplayId(string id)
        {
            return string.IsNullOrEmpty(id) ? "unknown research" : id.Replace('_', ' ');
        }
    }
}
