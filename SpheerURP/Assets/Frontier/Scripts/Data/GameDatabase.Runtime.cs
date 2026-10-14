using System;
using System.Collections.Generic;
using System.Linq;
using Frontier.Economy;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Frontier
{
    public partial class GameDatabase
    {
        static GameDatabase loaded;
        readonly List<Object> runtimeCopies = new List<Object>();

        /// <summary>The imported asset backing this transient runtime view; null on legacy/authoring databases.</summary>
        public GameDatabase ContentSource { get; private set; }
        public bool UsesImportedEconomy { get { return ContentSource != null; } }

        /// <summary>Inspector overrides use the same guard as the default; legacy/custom databases remain supported.</summary>
        public static GameDatabase ForRuntime(GameDatabase database)
        {
            if (database == null) return Load();
            if (database.UsesImportedEconomy) return database;
            if (database == Resources.Load<GameDatabase>("Frontier/EconomyDatabase")) return Load();
            if (database.schemaVersion == 0 && database.contentVersion == 0) return database;
            var selected = Select(database, LoadLegacy(), out var reason);
            if (!string.IsNullOrEmpty(reason)) Debug.LogWarning("Frontier: using legacy content: " + reason);
            return selected;
        }

        /// <summary>Pure selection inputs make missing/old/broken imports testable without modifying Resources.</summary>
        public static GameDatabase Select(GameDatabase imported, GameDatabase legacy, out string reason)
        {
            if (!CanActivate(imported, legacy, out reason)) return legacy != null ? legacy : DefaultContent.Create();
            var view = Instantiate(imported);
            view.hideFlags = HideFlags.HideAndDontSave;
            view.ContentSource = imported;
            view.planets = imported.planets.Select(p =>
            {
                var copy = view.Copy(p);
                copy.spots = p.spots.Where(s => view.FindResource(view.ResolveResourceId(s.resource.id)).IsRaw).Select(s => new GatherSpot
                {
                    label = s.label, game = s.game, resource = view.FindResource(view.ResolveResourceId(s.resource.id)),
                    baseYield = s.baseYield, difficulty = s.difficulty
                }).ToList();
                return copy;
            }).ToArray();
            view.startPlanet = view.FindPlanet(imported.startPlanet.id);
            view.enemies = imported.enemies.Select(e =>
            {
                var copy = view.Copy(e);
                copy.bounty = view.Rebind(e.bounty);
                return copy;
            }).ToArray();
            // The three original tool upgrades remain usable and saveable until tool ladders have a runtime/save model.
            var upgrades = new List<UpgradeSO>(imported.upgrades);
            foreach (var old in legacy.upgrades)
            {
                if (imported.FindUpgrade(old.id) != null) continue;
                var copy = view.Copy(old);
                copy.baseCost = view.Rebind(old.baseCost);
                copy.levelCosts = old.levelCosts.Select(row => new CostTier { cost = view.Rebind(row.cost) }).ToList();
                upgrades.Add(copy);
            }
            view.upgrades = upgrades.ToArray();
            return view;
        }

        T Copy<T>(T source) where T : Object
        {
            var copy = Instantiate(source);
            copy.hideFlags = HideFlags.HideAndDontSave;
            runtimeCopies.Add(copy);
            return copy;
        }

        List<ItemAmount> Rebind(List<ItemAmount> items)
        {
            return items.Select(x => new ItemAmount(FindResource(ResolveResourceId(x.resource.id)), x.amount)).ToList();
        }

        void OnDestroy()
        {
            foreach (var copy in runtimeCopies)
                if (copy != null)
                {
                    if (Application.isPlaying) Destroy(copy);
                    else DestroyImmediate(copy);
                }
            runtimeCopies.Clear();
        }

        public string ResolveResourceId(string id) { return ResolveId(LegacyKind.Resource, id); }
        public ResourceSO ResolveResource(string id) { return FindResource(ResolveResourceId(id)); }
        public string ResolveStructureId(string id) { return ResolveId(LegacyKind.Structure, id); }
        public string ResolveUpgradeId(string id) { return ResolveId(LegacyKind.Upgrade, id); }

        string ResolveId(LegacyKind kind, string id)
        {
            // Stable ids win. Applying aliases only to missing ids makes v1/v2 reloads idempotent.
            if (string.IsNullOrEmpty(id) || Known(kind, id) || legacyIds == null || legacyIds.rows == null) return id;
            foreach (var row in legacyIds.rows)
                if (row.kind == kind && row.legacyId == id && Known(kind, row.id)) return row.id;
            return id;
        }

        bool Known(LegacyKind kind, string id)
        {
            return kind == LegacyKind.Resource ? FindResource(id) != null :
                kind == LegacyKind.Structure ? FindStructure(id) != null : FindUpgrade(id) != null;
        }

        public static bool CanActivate(GameDatabase candidate, GameDatabase legacy, out string reason)
        {
            reason = null;
            try
            {
                Require(candidate != null, "economy asset is missing");
                Require(candidate.schemaVersion == 1 && candidate.contentVersion == 1, "unsupported economy version");
                var catalog = candidate.ToCatalog();
                // This adapter supports the reviewed v1 catalog, not a partially populated import.
                Require(catalog.resources.Length == 34 && catalog.recipes.Length == 24 && catalog.structures.Length == 31 &&
                    catalog.upgrades.Length == 16 && catalog.tools.Length == 6 && catalog.research.Length == 60 &&
                    catalog.obstacles.Length == 8, "incomplete economy catalog");
                var errors = CatalogValidator.Validate(catalog);
                Require(errors.Count == 0, string.Join("; ", errors.Take(5)));
                Require(candidate.scoreFormula != null && candidate.scoreFormula.data != null && candidate.tuning != null, "missing economy rules");
                candidate.tuning.Build();
                Require(candidate.baseBackpackSlots > 0 && candidate.builders > 0 && candidate.baseReactorHealth > 0, "invalid starting capacities");
                Require(candidate.fuel == candidate.FindResource("fuel") && candidate.fuel != null &&
                    candidate.scrap == candidate.FindResource("scrap") && candidate.scrap != null, "fuel/scrap link is broken");
                Require(legacy != null && legacy.planets != null && legacy.enemies != null && legacy.upgrades != null, "legacy compatibility source is missing");
                Require(candidate.startPlanet != null && candidate.planets.Contains(candidate.startPlanet) &&
                    candidate.startPlanet.id == catalog.start.startPlanetId && candidate.startPlanet == legacy.startPlanet, "authored home link is broken");
                Require(candidate.planets.Length == legacy.planets.Length && candidate.planets.Length > 0 &&
                    candidate.planets.All(p => p != null && legacy.planets.Contains(p)), "authored world links are broken");
                Require(candidate.formula != null && candidate.formula == legacy.formula && candidate.enemies.Length == legacy.enemies.Length &&
                    candidate.enemies.Length > 0 && candidate.enemies.All(e => e != null && legacy.enemies.Contains(e)), "combat links are broken");
                Require(candidate.planets.Select(p => p.id).Distinct().Count() == candidate.planets.Length &&
                    candidate.enemies.Select(e => e.id).Distinct().Count() == candidate.enemies.Length, "duplicate authored ids");
                var mappings = new HashSet<string>();
                foreach (var row in candidate.legacyIds.rows)
                    Require(mappings.Add(row.kind + ":" + row.legacyId), "duplicate legacy alias");
                foreach (var old in legacy.resources) Require(candidate.FindResource(candidate.ResolveResourceId(old.id)) != null, "unmapped legacy resource " + old.id);
                foreach (var old in legacy.structures) Require(candidate.FindStructure(candidate.ResolveStructureId(old.id)) != null, "unmapped legacy structure " + old.id);
                foreach (var old in legacy.upgrades)
                    Require(candidate.FindUpgrade(candidate.ResolveUpgradeId(old.id)) != null || old.effect == UpgradeEffect.ToolPower &&
                        candidate.toolLadders.Any(t => t.data.id == old.id), "unmapped legacy upgrade " + old.id);
                Action<List<ItemAmount>> checkItems = items =>
                {
                    Require(items != null, "missing item list");
                    foreach (var item in items)
                        Require(item.resource != null && item.amount > 0 &&
                            candidate.FindResource(candidate.ResolveResourceId(item.resource.id)) != null, "unresolved item reference");
                };
                Action<List<ItemAmount>> checkCatalogItems = items =>
                {
                    checkItems(items);
                    Require(items.All(i => candidate.resources.Contains(i.resource)), "catalog item links to a legacy/foreign resource asset");
                };
                checkItems(candidate.startingItems);
                Require(candidate.startingItems.Count > 0 && candidate.startingItems.All(i => candidate.resources.Contains(i.resource)), "invalid starting inventory");
                Require(candidate.startingItems.Sum(i => ((long)i.amount + i.resource.stackSize - 1) / i.resource.stackSize) <= candidate.baseBackpackSlots,
                    "starting inventory exceeds backpack");
                foreach (var planet in candidate.planets)
                {
                    Require(planet.spots != null && planet.spots.Count > 0, "authored world has no deposits");
                    foreach (var spot in planet.spots)
                        Require(spot != null && spot.resource != null && candidate.FindResource(candidate.ResolveResourceId(spot.resource.id)) != null &&
                            Enum.IsDefined(typeof(MinigameKind), spot.game) && spot.baseYield > 0, "invalid authored deposit");
                    Require(planet.spots.Any(s => candidate.FindResource(candidate.ResolveResourceId(s.resource.id)).IsRaw),
                        "authored world has no raw deposits");
                }
                foreach (var enemy in candidate.enemies) checkItems(enemy.bounty);
                foreach (var recipe in candidate.recipes)
                {
                    Require(candidate.FindStructure(recipe.stationId).kind.IsStation(), "recipe station has no crafting behaviour");
                    checkCatalogItems(recipe.inputs);
                    checkCatalogItems(recipe.outputs);
                }
                foreach (var structure in candidate.structures)
                {
                    Require(StructureKindMap.TryGet(structure.id, out var kind) && structure.kind == kind, "invalid structure kind " + structure.id);
                    Require(structure.levels.Count == structure.maxLevel, "structure max level mismatch");
                    Require(structure.levels.Select((row, i) => row.level == i + 1).All(x => x), "unordered structure levels");
                    checkCatalogItems(structure.buildCost);
                    foreach (var level in structure.levels) checkCatalogItems(level.cost);
                }
                foreach (var upgrade in candidate.upgrades)
                {
                    Require(Enum.IsDefined(typeof(UpgradeEffect), upgrade.effect) && Enum.IsDefined(typeof(UpgradeCategory), upgrade.category) &&
                        upgrade.levels.Count == upgrade.maxLevel, "invalid upgrade adapter " + upgrade.id);
                    Require(upgrade.category == (UpgradeCategory)Enum.Parse(typeof(UpgradeCategory), upgrade.group.ToString()) &&
                        upgrade.levels.Select((row, i) => row.level == i + 1).All(x => x), "invalid upgrade rows/category");
                    foreach (var level in upgrade.levels) checkCatalogItems(level.cost);
                }
                foreach (var old in legacy.upgrades.Where(u => candidate.FindUpgrade(u.id) == null))
                {
                    checkItems(old.baseCost);
                    foreach (var row in old.levelCosts) checkItems(row.cost);
                }
                return true;
            }
            catch (Exception error)
            {
                reason = error.Message;
                return false;
            }
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
