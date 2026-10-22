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

        public string PlayerLevelLockReason(int requiredLevel)
        {
            return requiredLevel > PlayerLevel
                ? UiText.Get("gate.player-level", requiredLevel, PlayerLevel) : null;
        }

        string GateReason(string research, int requiredLevel, int lab = 0)
        {
            if (!string.IsNullOrEmpty(research) && !IsResearched(research))
                return UiText.Get("gate.research", research.Replace('_', ' '));
            string levelGate = PlayerLevelLockReason(requiredLevel);
            if (levelGate != null) return levelGate;
            if (lab > 0)
            {
                var placed = StructureOfKind(planet, StructureKind.ResearchLab);
                if (placed == null || placed.level < lab) return UiText.Get("gate.lab-level", lab);
            }
            return null;
        }

        public string BuildLockReason(StructureSO def)
        {
            if (def == null || db.FindStructure(def.id) != def) return UiText.Get("gate.structure.unknown");
            if (!def.Implemented) return UiText.Get("gate.structure.future");
            if (!db.UsesImportedEconomy) return null;
            var row = def.Row(1);
            return GateReason(def.unlockResearch, Math.Max(def.unlockTier, row.requiresReachTier), row.requiresLabLevel);
        }

        public bool CanBuy(UpgradeSO upgrade) { return UpgradeLockReason(upgrade) == null && Has(upgrade.CostAt(Level(upgrade))); }

        public string UpgradeLockReason(UpgradeSO upgrade)
        {
            if (upgrade == null || db.FindUpgrade(upgrade.id) != upgrade) return UiText.Get("gate.upgrade.unknown");
            int level = Level(upgrade);
            if (level >= upgrade.MaxLevel) return UiText.Get("gate.max-level");
            if (!db.UsesImportedEconomy || !upgrade.HasLevelRows) return null;
            if (upgrade.effect == UpgradeEffect.StackMultiplier)
                return UiText.Get("gate.upgrade.future");
            var row = upgrade.NextRow(level);
            return GateReason(row.researchPrereq, row.requiresReachTier);
        }

        public bool CanUpgradeStructure(PlacedStructure placed)
        {
            return StructureUpgradeLockReason(placed) == null && HasPlanetResources(placed.def.UpgradeCost(placed.level));
        }

        public string StructureUpgradeLockReason(PlacedStructure placed)
        {
            if (!IsPlaced(placed)) return UiText.Get("gate.structure.unknown");
            if (!IsOperational(placed)) return UiText.Get("gate.structure.building");
            if (IsUpgradeInProgress(placed)) return UiText.Get("gate.structure.upgrading");
            if (placed.level >= placed.def.maxLevel) return UiText.Get("gate.max-level");
            if (!db.UsesImportedEconomy) return null;
            if (!placed.def.Implemented) return UiText.Get("gate.structure.future");
            var row = placed.def.Row(placed.level + 1);
            string gate = GateReason(placed.def.unlockResearch, row.requiresReachTier, row.requiresLabLevel);
            if (gate != null) return gate;
            return StructureJobSeconds(placed.def, placed.level + 1) > 0 ? BuilderAvailabilityReason() : null;
        }

        public bool RecipeAt(RecipeSO recipe, PlacedStructure station)
        {
            if (recipe == null || station == null) return false;
            if (db.UsesImportedEconomy) return recipe.stationId == station.def.id;
            return station.def.kind == (recipe.fabricator ? StructureKind.Fabricator : StructureKind.Refinery);
        }

        public string RecipeLockReason(RecipeSO recipe, PlacedStructure station)
        {
            if (recipe == null || Array.IndexOf(db.recipes, recipe) < 0) return UiText.Get("gate.recipe.unknown");
            if (!IsOperational(station) || station.planetId != planet.Id || !RecipeAt(recipe, station)) return UiText.Get("gate.recipe.station");
            if (!db.UsesImportedEconomy) return null;
            if (station.level < recipe.stationLevel) return UiText.Get("gate.recipe.station-level", recipe.stationLevel);
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
