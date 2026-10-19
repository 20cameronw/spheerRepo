using System;
using System.Collections.Generic;
using Frontier.Economy;
using UnityEngine;

namespace Frontier
{
    public enum ObjectiveClaimResult { Rejected, Saved, PendingSave }

    public partial class GameState
    {
        ObjectiveCatalogSO objectiveCatalog;
        ObjectiveLedger objectiveLedger = new ObjectiveLedger();
        bool objectivesInitialized;
        static readonly ObjectiveDefinition[] NoObjectives = Array.Empty<ObjectiveDefinition>();
        public IReadOnlyList<ObjectiveDefinition> Objectives { get { return objectiveCatalog != null ? objectiveCatalog.entries : NoObjectives; } }
        public bool ObjectiveCompleted(string id) { return objectiveLedger.Completed(id); }
        public bool ObjectiveClaimed(string id) { return objectiveLedger.Claimed(id); }

        void InitializeObjectiveCatalog()
        {
            if (!db.UsesImportedEconomy) return;
            var catalog = Resources.Load<ObjectiveCatalogSO>("Frontier/ObjectiveCatalog");
            if (catalog == null)
            {
                Debug.LogError("Frontier: objective catalogue is missing; achievements are unavailable.");
                return;
            }
            var problems = catalog.Validate(db);
            if (problems.Count != 0)
            {
                Debug.LogError("Frontier: invalid objective catalogue; achievements are unavailable: " + string.Join("; ", problems));
                return;
            }
            objectiveCatalog = catalog;
        }

        public int ObjectiveProgress(ObjectiveDefinition objective)
        {
            return ObjectiveProgress(objective, EraProgress, HighestVisitedTier());
        }

        int HighestVisitedTier()
        {
            int highest = 0;
            foreach (var delta in world.All)
            {
                if (delta == null || delta.visits <= 0) continue;
                var visited = universe.Get(delta.planetId);
                if (visited != null) highest = Math.Max(highest, visited.Tier);
            }
            return highest;
        }

        int ObjectiveProgress(ObjectiveDefinition objective, EraFacts facts, int highestTier)
        {
            if (objective == null) throw new ArgumentNullException(nameof(objective));
            if (objectiveLedger.Completed(objective.id)) return objective.target;
            int value;
            switch (objective.metric)
            {
                case ObjectiveMetric.HarvestRuns: value = stats.harvestRunsCompleted; break;
                case ObjectiveMetric.StructuresBuilt:
                    value = AtlasDiscoveredCount(AtlasStructureType);
                    if (value == 0)
                        foreach (var placed in structures)
                            if (placed.def.Implemented && IsOperational(placed)) { value = 1; break; }
                    break;
                case ObjectiveMetric.StructureBuilt:
                    var structure = db.FindStructure(objective.targetId);
                    value = structure != null && IsAtlasDiscovered(AtlasStructureType, structure.kind.ToString()) ? 1 : 0;
                    if (value == 0 && structure != null)
                        foreach (var placed in structures)
                            if (placed.def.kind == structure.kind && IsOperational(placed)) { value = 1; break; }
                    break;
                case ObjectiveMetric.RecipeOutput: value = HasCraftedOutput(objective.targetId) ? 1 : 0; break;
                case ObjectiveMetric.WorldsLanded: value = facts.worldsLanded; break;
                case ObjectiveMetric.WavesCleared: value = stats.totalWavesCleared; break;
                case ObjectiveMetric.UpgradeLevel: value = progression.UpgradeLevel(objective.targetId); break;
                case ObjectiveMetric.HazardPlating: value = facts.hazardPlatingPurchased ? 1 : 0; break;
                case ObjectiveMetric.MegaprojectStage: value = facts.highestMegaprojectStage; break;
                case ObjectiveMetric.Era:
                    value = EraRules.Meets((FrontierEra)objective.target, facts) ? objective.target : 0;
                    break;
                case ObjectiveMetric.ReachTier: value = highestTier; break;
                default: throw new ArgumentOutOfRangeException(nameof(objective.metric));
            }
            return Math.Min(objective.target, Math.Max(0, value));
        }

        bool HasCraftedOutput(string resourceId)
        {
            if (db.recipes == null) return false;
            foreach (var recipe in db.recipes)
            {
                if (recipe == null || recipe.outputs == null || !IsAtlasDiscovered(AtlasRecipeType, recipe.id)) continue;
                foreach (var output in recipe.outputs)
                    if (output.resource != null && output.resource.id == resourceId && output.amount > 0) return true;
            }
            return false;
        }

        bool ObserveObjectives(bool historical = false)
        {
            if (objectiveCatalog == null) return false;
            var facts = EraProgress;
            int highestTier = HighestVisitedTier();
            bool changed = false;
            foreach (var objective in Objectives)
                changed |= objectiveLedger.Observe(objective.id, ObjectiveProgress(objective, facts, highestTier) >= objective.target, historical);
            return changed;
        }

        public int ObjectiveReward(ObjectiveDefinition objective)
        {
            if (objective == null) throw new ArgumentNullException(nameof(objective));
            return AchievementRewards.Amount(objective.id, db.shardRules != null ? db.shardRules.data : null);
        }

        public ObjectiveClaimResult ClaimObjective(string id, out string message)
        {
            if (SaveIsReadOnly)
            {
                message = UiText.Get("claim.read-only");
                return ObjectiveClaimResult.Rejected;
            }
            ObjectiveDefinition definition = null;
            foreach (var objective in Objectives) if (objective.id == id) { definition = objective; break; }
            if (definition == null)
            {
                message = UiText.Get("claim.unavailable");
                return ObjectiveClaimResult.Rejected;
            }
            ObserveObjectives();
            if (!objectiveLedger.Completed(id) || objectiveLedger.Claimed(id))
            {
                message = UiText.Get(objectiveLedger.Claimed(id) ? "claim.collected" : "claim.incomplete");
                return ObjectiveClaimResult.Rejected;
            }
            int reward = ObjectiveReward(definition);
            if ((long)ShardBalance + reward > int.MaxValue)
            {
                message = UiText.Get("claim.wallet-full");
                return ObjectiveClaimResult.Rejected;
            }
            objectiveLedger.TryClaim(id);
            AddShards(reward);
            Notify();
            if (!saves.Save())
            {
                message = UiText.Get("claim.pending-save", saves.LastError);
                return ObjectiveClaimResult.PendingSave;
            }
            message = reward > 0 ? UiText.Get("claim.shards", reward) : UiText.Get("claim.done");
            return ObjectiveClaimResult.Saved;
        }
    }
}
