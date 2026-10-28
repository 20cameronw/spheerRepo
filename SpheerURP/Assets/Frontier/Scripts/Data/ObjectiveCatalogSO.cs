using System;
using System.Collections.Generic;
using Frontier.Economy;
using UnityEngine;

namespace Frontier
{
    public enum ObjectiveMetric { HarvestRuns = 0, StructuresBuilt = 1, StructureBuilt = 2, RecipeOutput = 3, WorldsLanded = 4, WavesCleared = 5, UpgradeLevel = 6, HazardPlating = 7, MegaprojectStage = 8, Era = 9, ReachTier = 10 }
    public enum ObjectiveRoute { Gather = 0, Build = 1, Map = 2, Command = 3, Atlas = 4, Ship = 5 }

    [Serializable]
    public sealed class ObjectiveDefinition
    {
        public string id, title;
        public string LocalizedTitle { get { return ContentText.Get("objective", id, "title", title); } }
        public string LocalizedDescription { get { return ContentText.Get("objective", id, "description", description); } }
        [TextArea] public string description;
        public ObjectiveMetric metric;
        public string targetId;
        public int target = 1;
        public ObjectiveRoute route;
    }

    [CreateAssetMenu(menuName = "Frontier/Economy/Objectives")]
    public sealed class ObjectiveCatalogSO : ContentSO
    {
        public ObjectiveDefinition[] entries = Array.Empty<ObjectiveDefinition>();

        public List<string> Validate(GameDatabase database)
        {
            var problems = new List<string>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (entries == null || entries.Length == 0) { problems.Add("Objective catalogue is empty."); return problems; }
            foreach (var entry in entries)
            {
                if (entry == null) { problems.Add("Null objective."); continue; }
                if (string.IsNullOrEmpty(entry.id) || !ids.Add(entry.id)) problems.Add("Missing/duplicate objective id: " + entry.id);
                if (!AchievementRewards.TrySize(entry.id, out _)) problems.Add("Unbudgeted objective: " + entry.id);
                if (string.IsNullOrWhiteSpace(entry.title) || string.IsNullOrWhiteSpace(entry.description)) problems.Add("Missing objective copy: " + entry.id);
                if (entry.target < 1 || !Enum.IsDefined(typeof(ObjectiveMetric), entry.metric) ||
                    !Enum.IsDefined(typeof(ObjectiveRoute), entry.route)) problems.Add("Invalid objective target/route: " + entry.id);
                if (entry.metric == ObjectiveMetric.StructureBuilt && database.FindStructure(entry.targetId) == null)
                    problems.Add("Unknown objective structure: " + entry.targetId);
                if (entry.metric == ObjectiveMetric.UpgradeLevel && database.FindUpgrade(entry.targetId) == null)
                    problems.Add("Unknown objective upgrade: " + entry.targetId);
                if (entry.metric == ObjectiveMetric.RecipeOutput && database.FindResource(entry.targetId) == null)
                    problems.Add("Unknown objective output: " + entry.targetId);
                if (entry.metric == ObjectiveMetric.Era && entry.target > (int)FrontierEra.Ascension)
                    problems.Add("Invalid objective era: " + entry.id);
                if (entry.metric == ObjectiveMetric.ReachTier && entry.target > 9) problems.Add("Unbudgeted reach tier: " + entry.id);
            }
            return problems;
        }
    }
}
