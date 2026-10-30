using System;
using System.Collections.Generic;

namespace Frontier
{
    public enum ResourceSourceKind { Structure, Build, Planet, Unavailable, Usable, LocalStock, RemoteStock }

    public sealed class ResourceSource
    {
        public ResourceSourceKind kind;
        public ResourceSO resource;
        public PlacedStructure structure;
        public StructureSO build;
        public RecipeSO recipe;
        public PlanetData planet;
        public string reason;
        public int amount, energy;
        public PlacedStructure relay;
    }

    public partial class GameState
    {
        public ResourceSource FindResourceSource(ResourceSO resource)
        {
            if (resource == null || db.FindResource(resource.id) != resource)
                throw new ArgumentException("Resource must belong to the active database.", nameof(resource));
            return FindResourceSource(resource, new HashSet<string>(StringComparer.Ordinal));
        }

        ResourceSource FindResourceSource(ResourceSO resource, HashSet<string> visiting)
        {
            var missing = new ResourceSource { kind = ResourceSourceKind.Unavailable, resource = resource };
            if (!visiting.Add(resource.id)) return missing;
            try
            {
                ResourceSource pending = null;
                foreach (var structure in StructuresOn(planet))
                {
                    if (structure.def.kind.IsGather() && SourceHasDeposit(planet, structure.def, resource))
                    {
                        var source = new ResourceSource { kind = ResourceSourceKind.Structure, resource = resource, structure = structure };
                        if (IsOperational(structure)) return source;
                        if (pending == null) pending = source;
                    }
                    if (!structure.def.kind.IsStation()) continue;
                    foreach (var recipe in db.recipes)
                    {
                        if (!RecipeProduces(recipe, resource) || !RecipeAt(recipe, structure)) continue;
                        var source = new ResourceSource { kind = ResourceSourceKind.Structure, resource = resource,
                            structure = structure, recipe = recipe, reason = RecipeLockReason(recipe, structure) };
                        if (source.reason == null) return source;
                        if (pending == null) pending = source;
                    }
                }
                // A producer being built/upgraded should open its status, not suggest a duplicate building.
                if (pending != null) return pending;

                ResourceSource locked = null;
                foreach (var def in db.structures)
                {
                    if (def == null || !def.Implemented) continue;
                    bool gathers = def.kind.IsGather() && SourceHasDeposit(planet, def, resource);
                    RecipeSO matching = null;
                    if (def.kind.IsStation())
                        foreach (var recipe in db.recipes)
                            if (RecipeProduces(recipe, resource) && RecipeUsesStation(recipe, def))
                            { matching = recipe; break; }
                    if (!gathers && matching == null) continue;
                    var source = new ResourceSource { kind = ResourceSourceKind.Build, resource = resource,
                        build = def, recipe = matching, reason = BuildLockReason(def) };
                    if (source.reason == null && CanBuildHere(def)) return source;
                    if (locked == null) locked = source;
                }

                // Search finite authored and visited worlds; do not block the UI scanning the generated universe.
                PlanetData nearest = null;
                double nearestDistance = double.MaxValue;
                var worlds = new List<PlanetData>(universe.AuthoredPlanets);
                foreach (var record in world.All)
                    if (record != null && record.visits > 0)
                    {
                        var visited = universe.Get(record.planetId);
                        if (visited != null) worlds.Add(visited);
                    }
                foreach (var candidate in worlds)
                {
                    if (candidate.Id == planet.Id || !IsMapDiscovered(candidate.Star.Id) && world.Visits(candidate.Id) <= 0) continue;
                    bool supplies = false;
                    foreach (var def in db.structures)
                        if (def != null && def.Implemented && def.kind.IsGather() &&
                            SourceHasDeposit(candidate, def, resource)) { supplies = true; break; }
                    if (!supplies) continue;
                    double dx = candidate.Star.X - planet.Star.X, dy = candidate.Star.Y - planet.Star.Y;
                    double distance = dx * dx + dy * dy;
                    // A reachable supplier outranks a nearer world that cannot be landed on.
                    if (!PlanRoute(candidate).reachable) distance += 1e20;
                    if (distance < nearestDistance) { nearest = candidate; nearestDistance = distance; }
                }
                if (nearest != null) return new ResourceSource { kind = ResourceSourceKind.Planet, resource = resource, planet = nearest };
                if (locked != null) return locked;

                // Processed resources without a local station route through their ingredient sources.
                foreach (var recipe in db.recipes)
                {
                    if (!RecipeProduces(recipe, resource)) continue;
                    foreach (var input in recipe.inputs)
                    {
                        if (input.resource == null || input.amount <= 0) continue;
                        var source = FindResourceSource(input.resource, visiting);
                        if (source.kind != ResourceSourceKind.Unavailable) return source;
                    }
                }
                return missing;
            }
            finally { visiting.Remove(resource.id); }
        }

        bool SourceHasDeposit(PlanetData target, StructureSO def, ResourceSO resource)
        {
            foreach (var spot in SpotsFor(target, def.kind))
                if (spot.resource == resource) return true;
            return false;
        }

        bool RecipeUsesStation(RecipeSO recipe, StructureSO def)
        {
            return db.UsesImportedEconomy ? recipe.stationId == def.id
                : def.kind == (recipe.fabricator ? StructureKind.Fabricator : StructureKind.Refinery);
        }

        static bool RecipeProduces(RecipeSO recipe, ResourceSO resource)
        {
            if (recipe == null) return false;
            foreach (var output in recipe.outputs)
                if (output.resource == resource && output.amount > 0) return true;
            return false;
        }
    }
}
