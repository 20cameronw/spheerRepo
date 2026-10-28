using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Frontier
{
    [Serializable]
    public sealed class PlayerProject
    {
        public string kind;
        public string id;
        public string planetId;
        public int slot = -1;
        public int target = 1;
        public int quantity = 1;
    }

    public sealed class ProjectStatus
    {
        public string name, blocker;
        public bool complete;
        public List<ItemAmount> costs = new List<ItemAmount>();
    }

    public sealed class SessionReturnItem
    {
        public string planetId;
        public int slot;
        public string resourceId;
        public int amount;
    }

    public partial class GameState
    {
        const string TrackedProjectKey = "player.tracked-project";
        public readonly List<SessionReturnItem> ReturnItems = new List<SessionReturnItem>();
        public int ReturnSummaryVersion { get; private set; }

        public void ResumeOfflineProgress()
        {
            if (SaveIsReadOnly || ClockRewindDetected || clockHighWaterUtc == DateTime.MinValue) return;
            ReturnItems.Clear();
            var jobs = structures.Where(s => IsBuilderBusy(s) ||
                !string.IsNullOrEmpty(s.record.buildEndUtc) || !string.IsNullOrEmpty(s.record.upgradeEndUtc)).ToList();
            string since = UtcText.Format(clockHighWaterUtc);
            bool changed = AccrueOfflineHarvesters(since) | AccrueOfflineDysonCollectors(since);
            changed |= CompleteBuilderJobs();
            foreach (var job in jobs)
                if (string.IsNullOrEmpty(job.record.buildEndUtc) && string.IsNullOrEmpty(job.record.upgradeEndUtc))
                    ReturnItems.Add(new SessionReturnItem { planetId = job.planetId, slot = job.slot });
            if (ReturnItems.Count > 0) ReturnSummaryVersion++;
            if (changed) Notify();
            Save();
        }
        public bool HasTrackedProject { get { return settings.Any(s => s != null && s.key == TrackedProjectKey && !string.IsNullOrEmpty(s.value)); } }
        public int ProjectResourceCount(PlayerProject project, ResourceSO resource)
        {
            return project != null && project.kind == "obstacle" ? Count(resource) : PlanetCount(resource);
        }

        public PlayerProject TrackedProject
        {
            get
            {
                var entry = settings.FirstOrDefault(s => s != null && s.key == TrackedProjectKey);
                if (entry == null || string.IsNullOrEmpty(entry.value)) return null;
                try { return JsonUtility.FromJson<PlayerProject>(entry.value); }
                catch (ArgumentException error)
                {
                    Debug.LogWarning("Frontier: tracked project is unreadable: " + error.Message);
                    return new PlayerProject();
                }
            }
        }

        public bool TrackProject(PlayerProject project, out string error)
        {
            error = SaveIsReadOnly ? UiText.Get("project.read-only") : null;
            if (error != null) return false;
            if (project != null && ProjectState(project).name == null)
            {
                error = UiText.Get("project.missing");
                return false;
            }
            string value = project == null ? "" : JsonUtility.ToJson(project);
            var candidate = SaveSerializer.Instance.DeserializeV2(SaveSerializer.Instance.Serialize(BuildSave())).Normalize();
            var entry = candidate.settings.FirstOrDefault(s => s != null && s.key == TrackedProjectKey);
            if (entry == null) { entry = new SettingEntry { key = TrackedProjectKey }; candidate.settings.Add(entry); }
            entry.value = value;
            if (!saves.CommitSnapshot(candidate)) { error = UiText.Get("project.save-failed"); return false; }
            SetSetting(TrackedProjectKey, value);
            Notify();
            return true;
        }

        public ProjectStatus ProjectState(PlayerProject project)
        {
            var result = new ProjectStatus { blocker = UiText.Get("project.missing") };
            if (project == null || project.target < 1 || project.quantity < 1 || project.quantity > 999) return result;
            var local = project.planetId == planet.Id;
            var structure = structures.FirstOrDefault(s => s.planetId == project.planetId && s.slot == project.slot);
            switch (project.kind)
            {
                case "upgrade":
                    var upgrade = db.FindUpgrade(project.id);
                    if (upgrade == null) return result;
                    result.name = upgrade.LocalizedName;
                    result.complete = Level(upgrade) >= project.target;
                    result.costs = upgrade.CostAt(Level(upgrade));
                    result.blocker = UpgradeLockReason(upgrade);
                    break;
                case "research":
                    var research = db.research.FirstOrDefault(n => n != null && n.data.id == project.id);
                    if (research == null) return result;
                    result.name = research.LocalizedName;
                    result.complete = IsResearched(project.id);
                    if (TryBuildCost(research.data.cost, out var researchCost)) result.costs = researchCost;
                    result.blocker = ResearchLockReason(research);
                    break;
                case "tool":
                    var tool = db.toolLadders.FirstOrDefault(t => t != null && t.data.id == project.id);
                    if (tool == null) return result;
                    result.name = tool.LocalizedName;
                    result.complete = ToolTier(tool) >= project.target;
                    var tier = FindToolTier(tool, ToolTier(tool) + 1);
                    if (tier != null && TryBuildCost(tier.cost, out var toolCost)) result.costs = toolCost;
                    result.blocker = ToolTierLockReason(tool);
                    break;
                case "build":
                    var definition = db.FindStructure(project.id);
                    if (definition == null) return result;
                    result.name = definition.LocalizedName;
                    result.complete = structures.Any(s => s.planetId == project.planetId && s.def == definition && IsOperational(s));
                    result.costs = definition.buildCost;
                    result.blocker = local ? BuildLockReason(definition) : UiText.Get("project.return-world");
                    break;
                case "structure":
                    if (structure == null || structure.def.id != project.id) return result;
                    result.name = structure.def.LocalizedName;
                    result.complete = structure.level >= project.target && IsOperational(structure);
                    result.costs = structure.def.UpgradeCost(structure.level);
                    result.blocker = local ? StructureUpgradeLockReason(structure) : UiText.Get("project.return-world");
                    break;
                case "recipe":
                    var recipe = db.recipes.FirstOrDefault(r => r != null && r.id == project.id);
                    if (recipe == null || structure == null) return result;
                    result.name = recipe.LocalizedName;
                    result.costs = recipe.inputs.Select(i => new ItemAmount(i.resource,
                        (int)Math.Min(int.MaxValue, (long)i.amount * project.quantity))).ToList();
                    result.blocker = local ? RecipeLockReason(recipe, structure) : UiText.Get("project.return-world");
                    // A recipe project asks for owned output, not merely having performed an unrelated craft.
                    result.complete = local && recipe.outputs.All(o => PlanetCount(o.resource) >=
                        (long)OutputAmount(o, StructureBonus(structure)) * project.quantity);
                    break;
                case "network":
                    if (NetworkRules == null) return result;
                    result.name = UiText.Get("network.project.title");
                    result.complete = NetworkComplete;
                    result.costs = NetworkCosts(true);
                    result.blocker = local ? NetworkCompletionLockReason() : UiText.Get("project.return-world");
                    break;
                case "link":
                    var destination = universe.Get(project.id);
                    var origin = universe.Get(project.planetId);
                    if (destination == null || origin == null) return result;
                    result.name = UiText.Get("network.link", destination.Name);
                    result.complete = HasPlanetLink(origin, destination);
                    result.costs = NetworkCosts(false);
                    result.blocker = local ? PlanetLinkLockReason(destination) : UiText.Get("project.return-world");
                    break;
                case "obstacle":
                    var obstacleWorld = universe.Get(project.planetId);
                    if (obstacleWorld == null || project.slot < 0) return result;
                    result.name = UiText.Get("obstacle.title");
                    result.complete = !ObstacleAt(obstacleWorld, project.slot).HasValue;
                    result.costs = ObstacleClearCost(obstacleWorld, project.slot);
                    result.blocker = local ? ObstacleClearLockReason(project.slot) : UiText.Get("project.return-world");
                    break;
                default: return result;
            }
            if (result.complete) { result.costs = new List<ItemAmount>(); result.blocker = null; }
            else if (!local) result.blocker = UiText.Get("project.return-world");
            return result;
        }

        public List<ResourceSource> ResourceOptions(ResourceSO resource, int required, bool carriedOnly = false)
        {
            if (required < 1) throw new ArgumentOutOfRangeException(nameof(required));
            var result = new List<ResourceSource>();
            int usable = carriedOnly ? Count(resource) : PlanetCount(resource);
            if (usable >= required)
                result.Add(new ResourceSource { kind = ResourceSourceKind.Usable, resource = resource, amount = usable });
            var relay = StructureOfKind(planet, StructureKind.Satellite);
            var accessible = relay == null ? new List<PlacedStructure>() : RelayWarehouses(relay);
            foreach (var storage in structures)
            {
                if (!storage.def.kind.IsStorage() || storage.def.kind == StructureKind.FuelDepot ||
                    !IsOperational(storage) || world.Visits(storage.planetId) <= 0) continue;
                int held = WarehouseOps.Held(StorageFor(storage), resource.id);
                if (held <= 0) continue;
                bool local = storage.planetId == planet.Id;
                if (local && CargoDepotActive && !carriedOnly) continue;
                int energy = !local && accessible.Contains(storage)
                    ? RelayTransferEnergyCost(relay, storage, Math.Min(held, required)) : int.MaxValue;
                result.Add(new ResourceSource { kind = local ? ResourceSourceKind.LocalStock : ResourceSourceKind.RemoteStock,
                    resource = resource, structure = storage, planet = universe.Get(storage.planetId),
                    amount = held, relay = energy == int.MaxValue ? null : relay, energy = energy });
            }
            result = result.OrderBy(s => s.kind == ResourceSourceKind.Usable ? 0 :
                s.kind == ResourceSourceKind.LocalStock ? 1 :
                s.relay != null && Rules.Pay(s.energy, FuelOnBoard).ok && backpack.FreeSpace(resource.id) > 0 ? 2 : 4)
                .ThenBy(s => s.energy).ThenBy(s => s.planet == null ? "" : s.planet.Id, StringComparer.Ordinal).ToList();
            var production = FindResourceSource(resource);
            int index = result.FindIndex(s => s.kind == ResourceSourceKind.RemoteStock && s.relay == null);
            result.Insert(index >= 0 ? index : result.Count, production);
            return result;
        }
    }
}
