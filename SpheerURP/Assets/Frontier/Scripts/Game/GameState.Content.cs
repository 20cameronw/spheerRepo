using System;
using UnityEngine;

namespace Frontier
{
    public partial class GameState
    {
        public int ReachTier
        {
            get
            {
                int tier = planet != null ? planet.Tier : 0;
                foreach (var delta in world.Export())
                {
                    if (delta.visits <= 0) continue;
                    var visited = universe.Get(delta.planetId);
                    if (visited != null) tier = Math.Max(tier, visited.Tier);
                }
                return tier;
            }
        }

        // Research has no completion service yet. Atlas/discovery entries are NOT research grants.
        string GateReason(string research, int reach, int lab = 0)
        {
            if (!string.IsNullOrEmpty(research)) return "Requires research: " + research.Replace('_', ' ');
            if (reach > ReachTier) return "Reach planet tier " + reach;
            if (lab > 0)
            {
                var placed = StructureOfKind(planet, StructureKind.ResearchLab);
                if (placed == null || placed.level < lab) return "Requires Research Lab level " + lab;
            }
            return null;
        }

        public string BuildLockReason(StructureSO def)
        {
            if (def == null || db.FindStructure(def.id) != def) return "Unknown structure";
            if (!def.Implemented) return "Structure gameplay coming later";
            if (!db.UsesImportedEconomy) return null;
            var row = def.Row(1);
            return GateReason(def.unlockResearch, Math.Max(def.unlockTier, row.requiresReachTier), row.requiresLabLevel);
        }

        public bool CanBuy(UpgradeSO upgrade) { return UpgradeLockReason(upgrade) == null && Has(upgrade.CostAt(Level(upgrade))); }

        public string UpgradeLockReason(UpgradeSO upgrade)
        {
            if (upgrade == null || db.FindUpgrade(upgrade.id) != upgrade) return "Unknown upgrade";
            int level = Level(upgrade);
            if (level >= upgrade.MaxLevel) return "Max level";
            if (!db.UsesImportedEconomy || !upgrade.HasLevelRows) return null;
            if (upgrade.effect == UpgradeEffect.StackMultiplier || upgrade.effect == UpgradeEffect.FuelTankSlots)
                return "Upgrade gameplay coming later";
            var row = upgrade.NextRow(level);
            return GateReason(row.researchPrereq, row.requiresReachTier);
        }

        public bool CanUpgradeStructure(PlacedStructure placed)
        {
            return StructureUpgradeLockReason(placed) == null && Has(placed.def.UpgradeCost(placed.level));
        }

        public string StructureUpgradeLockReason(PlacedStructure placed)
        {
            if (!IsPlaced(placed)) return "Unknown structure";
            if (placed.level >= placed.def.maxLevel) return "Max level";
            if (!db.UsesImportedEconomy) return null;
            if (!placed.def.Implemented) return "Structure gameplay coming later";
            var row = placed.def.Row(placed.level + 1);
            return GateReason(placed.def.unlockResearch, row.requiresReachTier, row.requiresLabLevel);
        }

        public bool RecipeAt(RecipeSO recipe, PlacedStructure station)
        {
            if (recipe == null || station == null) return false;
            if (db.UsesImportedEconomy) return recipe.stationId == station.def.id;
            return station.def.kind == (recipe.fabricator ? StructureKind.Fabricator : StructureKind.Refinery);
        }

        public string RecipeLockReason(RecipeSO recipe, PlacedStructure station)
        {
            if (recipe == null || Array.IndexOf(db.recipes, recipe) < 0) return "Unknown recipe";
            if (!IsPlaced(station) || station.planetId != planet.Id || !RecipeAt(recipe, station)) return "Requires its crafting station";
            if (!db.UsesImportedEconomy) return null;
            if (station.level < recipe.stationLevel) return "Requires station level " + recipe.stationLevel;
            return GateReason(recipe.unlockResearch, recipe.tier);
        }

        public bool CanCraft(RecipeSO recipe)
        {
            // Preserve the legacy API for old/custom databases, where crafting was inventory-only.
            if (!db.UsesImportedEconomy) return recipe != null && Array.IndexOf(db.recipes, recipe) >= 0;
            foreach (var station in structures)
                if (RecipeLockReason(recipe, station) == null) return true;
            return false;
        }
    }
}
