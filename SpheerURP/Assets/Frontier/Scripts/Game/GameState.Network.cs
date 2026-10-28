using System;
using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    public partial class GameState
    {
        NetworkProgress network = new NetworkProgress();
        NetworkRulesSO networkRules;
        public NetworkRulesSO NetworkRules { get { return networkRules; } }
        public bool NetworkComplete { get { return network.completed; } }
        public string NetworkArchiveId { get { return network.archivePlanetId; } }
        public IEnumerable<PlanetLink> PlanetLinks
        {
            get
            {
                foreach (var link in network.links)
                    yield return new PlanetLink { fromPlanetId = link.fromPlanetId, toPlanetId = link.toPlanetId };
            }
        }

        void InitializeNetworkRules()
        {
            if (!db.UsesImportedEconomy) return;
            networkRules = Resources.Load<NetworkRulesSO>("Frontier/NetworkRules");
            if (networkRules != null && networkRules.IsValid(db)) return;
            Debug.LogError("Frontier: Network rules are missing or invalid; planet linking is unavailable.");
            networkRules = null;
        }

        HashSet<string> ConnectedWorlds(string origin)
        {
            var result = new HashSet<string>(StringComparer.Ordinal) { origin };
            bool changed;
            do
            {
                changed = false;
                foreach (var link in network.links)
                {
                    if (result.Contains(link.fromPlanetId)) changed |= result.Add(link.toPlanetId);
                    if (result.Contains(link.toPlanetId)) changed |= result.Add(link.fromPlanetId);
                }
            } while (changed);
            return result;
        }

        public bool HasPlanetLink(PlanetData from, PlanetData to)
        {
            return from != null && to != null && from.Id != to.Id && ConnectedWorlds(from.Id).Contains(to.Id);
        }

        public bool CanInstantTravel(PlanetData to)
        {
            return to != null && to.Id != planet.Id &&
                (HasPlanetLink(planet, to) || NetworkComplete && world.Visits(to.Id) > 0);
        }

        RoutePlan NetworkRoute(PlanetData from, PlanetData to, bool autopilot)
        {
            if (from.Id == to.Id || !(HasPlanetLink(from, to) || NetworkComplete && world.Visits(to.Id) > 0)) return null;
            var gaps = Rules.LandingGaps(ShipFor(from), to.Hazards, to.Tier);
            bool land = Rules.CanLand(ShipFor(from), to.Hazards, to.Tier);
            return new RoutePlan
            {
                reachable = land, reasons = land ? RouteReasons.None : RouteReasons.Hazard,
                hazardGaps = gaps, fromPlanetId = from.Id, toPlanetId = to.Id,
                intraSystem = from.Star.Key == to.Star.Key, autopilot = autopilot, instant = true
            };
        }

        public string PlanetLinkLockReason(PlanetData to)
        {
            if (SaveIsReadOnly || PendingTravel != null) return UiText.Get("network.lock.busy");
            if (networkRules == null) return UiText.Get("network.lock.unavailable");
            if (to == null || universe.Get(to.Id) == null || to.Id == planet.Id) return UiText.Get("network.lock.destination");
            if (HasPlanetLink(planet, to)) return UiText.Get("network.lock.connected");
            if (!IsResearched(networkRules.linkResearch)) return UiText.Get("gate.research", ContentText.Label(networkRules.linkResearch));
            if (world.Visits(to.Id) <= 0) return UiText.Get("network.lock.visit");
            var fromGate = StructureOfKind(planet, StructureKind.WarpGate);
            var toGate = StructureOfKind(to, StructureKind.WarpGate);
            if (fromGate == null || toGate == null || fromGate.level < networkRules.gateLevel || toGate.level < networkRules.gateLevel)
                return UiText.Get("network.lock.gates", networkRules.gateLevel);
            if (!Rules.CanLand(Ship, to.Hazards, to.Tier)) return UiText.Get("network.lock.plating");
            if (!HasPlanetResources(NetworkCosts(false))) return UiText.Get("network.lock.resources");
            return null;
        }

        public List<ItemAmount> NetworkCosts(bool finale)
        {
            var result = new List<ItemAmount>();
            if (networkRules == null) return result;
            foreach (var cost in finale ? networkRules.finaleCost : networkRules.linkCost)
                result.Add(new ItemAmount(db.FindResource(cost.resourceId), cost.amount));
            return result;
        }

        public bool TryLinkPlanet(PlanetData to, out string message)
        {
            message = PlanetLinkLockReason(to);
            if (message != null) return false;
            var candidate = DetachedNetworkSave();
            var link = new PlanetLink { fromPlanetId = planet.Id, toPlanetId = to.Id };
            candidate.network.links.Add(link);
            if (!CommitNetworkCost(candidate, NetworkCosts(false), out message)) return false;
            network.links.Add(link);
            Notify();
            message = UiText.Get("network.linked", to.Name);
            return true;
        }

        public int NetworkWarehouseRequirement
        {
            get
            {
                if (networkRules == null) return 0;
                int support = db.FindStructure("warp_gate").footprint + db.FindStructure("cargo_depot").footprint;
                return Math.Max(networkRules.minimumWarehouses, planet.BuildingSlots - support);
            }
        }

        public void NetworkWarehouseStatus(out int count, out int slots)
        {
            count = 0; slots = 0;
            foreach (var structure in StructuresOn(planet))
                if (structure.def.kind == StructureKind.Warehouse && IsOperational(structure))
                {
                    count++;
                    slots += structure.def.StorageCapacity(structure.level);
                }
        }

        bool HasDeposit(PlanetData p, string resourceId)
        {
            foreach (var deposit in p.Descriptor.Deposits)
                if (deposit.ResourceId == resourceId && deposit.Richness > 0) return true;
            return false;
        }

        // Maximum bipartite matching ensures each required deposit is supplied by a different linked world.
        public string[] NetworkAssignedWorlds()
        {
            if (networkRules == null) return Array.Empty<string>();
            var worlds = new List<string>(ConnectedWorlds(planet.Id));
            worlds.Remove(planet.Id);
            worlds.Sort(StringComparer.Ordinal);
            var assignments = new string[networkRules.resourceWorlds.Length];
            bool Assign(int role, HashSet<string> visited)
            {
                foreach (string id in worlds)
                {
                    var p = universe.Get(id);
                    if (p == null || !HasDeposit(p, networkRules.resourceWorlds[role])) continue;
                    if (!visited.Add(id)) continue;
                    int previous = Array.IndexOf(assignments, id);
                    if (previous >= 0 && !Assign(previous, visited)) continue;
                    assignments[role] = id;
                    return true;
                }
                return false;
            }
            for (int role = 0; role < assignments.Length; role++) Assign(role, new HashSet<string>(StringComparer.Ordinal));
            return assignments;
        }

        public string NetworkCompletionLockReason()
        {
            if (SaveIsReadOnly || PendingTravel != null) return UiText.Get("network.lock.busy");
            if (networkRules == null) return UiText.Get("network.lock.unavailable");
            if (NetworkComplete) return UiText.Get("network.lock.complete");
            if (!IsResearched(networkRules.finaleResearch)) return UiText.Get("gate.research", ContentText.Label(networkRules.finaleResearch));
            if (StructureOfKind(planet, StructureKind.WarpGate) == null || !CargoDepotActive)
                return UiText.Get("network.lock.archive");
            foreach (string id in NetworkAssignedWorlds())
                if (id == null) return UiText.Get("network.lock.worlds");
            NetworkWarehouseStatus(out int count, out int slots);
            if (count < NetworkWarehouseRequirement || slots < networkRules.minimumStorageSlots)
                return UiText.Get("network.lock.warehouses", NetworkWarehouseRequirement, networkRules.minimumStorageSlots);
            if (!HasPlanetResources(NetworkCosts(true))) return UiText.Get("network.lock.resources");
            return null;
        }

        public bool TryCompleteNetwork(out string message)
        {
            message = NetworkCompletionLockReason();
            if (message != null) return false;
            var candidate = DetachedNetworkSave();
            candidate.network.completed = true;
            candidate.network.archivePlanetId = planet.Id;
            if (!CommitNetworkCost(candidate, NetworkCosts(true), out message)) return false;
            network.completed = true;
            network.archivePlanetId = planet.Id;
            Notify();
            message = UiText.Get("network.complete.title");
            return true;
        }

        SaveV2 DetachedNetworkSave()
        {
            return SaveSerializer.Instance.DeserializeV2(SaveSerializer.Instance.Serialize(BuildSave())).Normalize();
        }

        bool CommitNetworkCost(SaveV2 candidate, List<ItemAmount> costs, out string message)
        {
            var pooled = PooledWarehouseStorages(planet);
            var storageSlots = new HashSet<int>();
            foreach (var storage in pooled) storageSlots.Add(storage.structureSlot);
            foreach (var cost in costs)
            {
                int remaining = cost.amount;
                void Take(List<StackRecord> stacks)
                {
                    foreach (var stack in stacks)
                    {
                        if (stack.resourceId != cost.resource.id || stack.count <= 0) continue;
                        int amount = Math.Min(remaining, stack.count);
                        stack.count -= amount;
                        remaining -= amount;
                    }
                }
                Take(IsTankFuel(cost.resource.id) ? candidate.player.fuelTank : candidate.player.inventory);
                foreach (var delta in candidate.worlds)
                    if (delta.planetId == planet.Id)
                        foreach (var storage in delta.storages)
                            if (storageSlots.Contains(storage.structureSlot)) Take(storage.slots);
                if (remaining != 0) throw new InvalidOperationException("The validated network project cost is missing.");
            }
            if (!saves.CommitSnapshot(candidate))
            {
                message = UiText.Get("network.save-failed", saves.LastError);
                return false;
            }
            if (!SpendPlanetResourcesQuietly(ToIds(costs))) throw new InvalidOperationException("The committed network cost changed.");
            message = "";
            return true;
        }

        public bool TryInstantTravel(PlanetData to, out string message)
        {
            message = UiText.Get("network.lock.destination");
            to = to == null ? null : universe.Get(to.Id);
            if (SaveIsReadOnly || PendingTravel != null || !CanInstantTravel(to)) return false;
            var plan = PlanRoute(to);
            if (!plan.reachable) { message = UiText.Get("network.lock.plating"); return false; }
            var candidate = DetachedNetworkSave();
            candidate.location.currentPlanetId = to.Id;
            var worldCopy = new WorldStore();
            worldCopy.Import(candidate.worlds);
            bool first = worldCopy.RecordVisit(to.Id, clock.UtcNow);
            candidate.worlds = worldCopy.Export();
            candidate.stats.RecordLanding(universe.DistanceFromHomeLy(to), first);
            if (!saves.CommitSnapshot(candidate)) { message = UiText.Get("network.save-failed", saves.LastError); return false; }
            planet = to;
            RecordArrival(to);
            Notify();
            message = "";
            return true;
        }
    }
}
