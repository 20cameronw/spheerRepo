using System;
using System.Collections.Generic;
using System.Linq;
using Frontier.Economy;
using UnityEngine;

namespace Frontier
{
    [CreateAssetMenu(fileName = "GameDatabase", menuName = "Frontier/Game Database")]
    public partial class GameDatabase : ScriptableObject
    {
        public ResourceSO[] resources;
        public PlanetSO[] planets;
        public UpgradeSO[] upgrades;
        public EnemySO[] enemies;
        public RecipeSO[] recipes;
        public StructureSO[] structures;
        public WaveFormulaSO formula;

        public ResourceSO fuel;
        public ResourceSO scrap;
        public PlanetSO startPlanet;
        public List<ItemAmount> startingItems = new List<ItemAmount>();

        public int baseBackpackSlots = 9;
        public float baseReactorHealth = 100f;

        [Header("Imported economy")]
        public int schemaVersion;
        public int contentVersion;
        public int builders = 1;
        public string[] categoryHex;
        public ToolLadderSO[] toolLadders;
        public ResearchNodeSO[] research;
        public ObstacleKindSO[] obstacleKinds;
        public HabitatTableSO habitats;
        public ObstacleRulesSO obstacleRules;
        public ShardRulesSO shardRules;
        public ScoreFormulaSO scoreFormula;
        public LegacyIdMapSO legacyIds;
        public EconomyTuningSO tuning;

        /// <summary>Reconstructs the imported catalog without compatibility-only runtime definitions.</summary>
        public EconomyCatalog ToCatalog()
        {
            if (ContentSource != null) return ContentSource.ToCatalog();
            var catalog = new EconomyCatalog
            {
                schemaVersion = schemaVersion, contentVersion = contentVersion,
                resources = resources.Select(x => x.ToDef()).ToArray(),
                recipes = recipes.Select(x => x.ToDef()).ToArray(),
                structures = structures.Select(x => x.ToDef()).ToArray(),
                upgrades = upgrades.Select(x => x.ToDef()).ToArray(),
                tools = toolLadders.Select(x => x.ToDef()).ToArray(),
                research = research.Select(x => x.data).ToArray(),
                obstacles = obstacleKinds.Select(x => x.data).ToArray(),
                habitats = habitats.rows, obstacleRules = obstacleRules.data, shards = shardRules.data,
                legacyIds = legacyIds.rows, categoryHex = categoryHex,
                start = new StartDef
                {
                    startingItems = ItemAmounts.ToIds(startingItems), baseBackpackSlots = baseBackpackSlots,
                    startPlanetId = startPlanet != null ? startPlanet.id : null, builders = builders
                }
            };
            catalog.Index();
            return catalog;
        }

        /// <summary>Loads the validated economy runtime view, falling back to legacy content on incomplete projects.</summary>
        public static GameDatabase Load()
        {
            if (loaded != null) return loaded;
            loaded = Select(Resources.Load<GameDatabase>("Frontier/EconomyDatabase"), LoadLegacy(), out var reason);
            if (!string.IsNullOrEmpty(reason)) Debug.LogWarning("Frontier: using legacy content: " + reason);
            return loaded;
        }

        public static GameDatabase LoadLegacy()
        {
            var legacy = Resources.Load<GameDatabase>("Frontier/GameDatabase");
            return legacy != null ? legacy : MissingLegacyContent();
        }

        static GameDatabase MissingLegacyContent()
        {
#if UNITY_EDITOR
            return DefaultContent.Create();
#else
            throw new InvalidOperationException("Frontier legacy content asset is missing from Resources/Frontier/GameDatabase.");
#endif
        }

        public PlanetSO FindPlanet(string id)
        {
            foreach (var p in planets) if (p != null && p.id == id) return p;
            return null;
        }

        public ResourceSO FindResource(string id)
        {
            foreach (var r in resources) if (r != null && r.id == id) return r;
            return null;
        }

        public UpgradeSO FindUpgrade(string id)
        {
            foreach (var u in upgrades) if (u != null && u.id == id) return u;
            return null;
        }

        public StructureSO FindStructure(string id)
        {
            if (structures == null) return null;
            foreach (var s in structures) if (s != null && s.id == id) return s;
            return null;
        }

        public StructureSO StructureOfKind(StructureKind k)
        {
            if (structures == null) return null;
            foreach (var s in structures) if (s != null && s.kind == k) return s;
            return null;
        }
    }
}
