using System;
using System.Collections.Generic;
using Frontier.Universe;

namespace Frontier.Economy
{
    /// <summary>The complete economy as plain data: what the editor generator will turn into ScriptableObjects. Built by <see cref="CatalogBuilder"/>.</summary>
    [Serializable]
    public sealed class EconomyCatalog
    {
        /// <summary>Version of the data layout (bump when field names or meanings change).</summary>
        public int schemaVersion;
        /// <summary>Version of the content (bump when numbers change after release).</summary>
        public int contentVersion;
        public ResourceDef[] resources;
        public HabitatDef[] habitats;
        public RecipeDef[] recipes;
        public StructureDef[] structures;
        public ToolLadderDef[] tools;
        public UpgradeDef[] upgrades;
        public ResearchNodeDef[] research;
        public LegacyIdEntry[] legacyIds;
        public StartDef start;
        public ObstacleKindDef[] obstacles;
        public ObstacleRulesDef obstacleRules;
        public ShardRulesDef shards;
        /// <summary>Colour family per <see cref="ResourceCategory"/> (index = enum value).</summary>
        public string[] categoryHex;

        Dictionary<string, ResourceDef> _res;
        Dictionary<string, RecipeDef> _rec;
        Dictionary<string, StructureDef> _str;
        Dictionary<string, ToolLadderDef> _tool;
        Dictionary<string, UpgradeDef> _upg;
        Dictionary<string, ResearchNodeDef> _rsr;
        PriceBook _prices;

        /// <summary>Builds the lookup tables. Call after the arrays are final (CatalogBuilder does).</summary>
        public void Index()
        {
            _res = new Dictionary<string, ResourceDef>();
            _rec = new Dictionary<string, RecipeDef>();
            _str = new Dictionary<string, StructureDef>();
            _tool = new Dictionary<string, ToolLadderDef>();
            _upg = new Dictionary<string, UpgradeDef>();
            _rsr = new Dictionary<string, ResearchNodeDef>();
            for (int i = 0; i < resources.Length; i++) _res[resources[i].id] = resources[i];
            for (int i = 0; i < recipes.Length; i++) _rec[recipes[i].id] = recipes[i];
            for (int i = 0; i < structures.Length; i++) _str[structures[i].id] = structures[i];
            for (int i = 0; i < tools.Length; i++) _tool[tools[i].id] = tools[i];
            for (int i = 0; i < upgrades.Length; i++) _upg[upgrades[i].id] = upgrades[i];
            for (int i = 0; i < research.Length; i++) _rsr[research[i].id] = research[i];
            _prices = new PriceBook(resources, recipes);
        }

        public PriceBook Prices { get { return _prices; } }

        public ResourceDef Resource(string id) { ResourceDef d; return id != null && _res.TryGetValue(id, out d) ? d : null; }
        public RecipeDef Recipe(string id) { RecipeDef d; return id != null && _rec.TryGetValue(id, out d) ? d : null; }
        public StructureDef Structure(string id) { StructureDef d; return id != null && _str.TryGetValue(id, out d) ? d : null; }
        public ToolLadderDef Tool(string id) { ToolLadderDef d; return id != null && _tool.TryGetValue(id, out d) ? d : null; }
        public UpgradeDef Upgrade(string id) { UpgradeDef d; return id != null && _upg.TryGetValue(id, out d) ? d : null; }
        public ResearchNodeDef Research(string id) { ResearchNodeDef d; return id != null && _rsr.TryGetValue(id, out d) ? d : null; }

        public int ResourceIndex(string id)
        {
            for (int i = 0; i < resources.Length; i++) if (resources[i].id == id) return i;
            return -1;
        }

        /// <summary>Recipes that make the resource (in catalog order; the first one is the primary recipe).</summary>
        public List<RecipeDef> RecipesFor(string resourceId)
        {
            var list = new List<RecipeDef>();
            for (int i = 0; i < recipes.Length; i++)
                for (int k = 0; k < recipes[i].outputs.Length; k++)
                    if (recipes[i].outputs[k].resourceId == resourceId) { list.Add(recipes[i]); break; }
            return list;
        }

        /// <summary>Recipes that consume the resource.</summary>
        public List<RecipeDef> RecipesUsing(string resourceId)
        {
            var list = new List<RecipeDef>();
            for (int i = 0; i < recipes.Length; i++)
                for (int k = 0; k < recipes[i].inputs.Length; k++)
                    if (recipes[i].inputs[k].resourceId == resourceId) { list.Add(recipes[i]); break; }
            return list;
        }

        public int RawCount
        {
            get { int n = 0; for (int i = 0; i < resources.Length; i++) if (resources[i].category == ResourceCategory.Raw) n++; return n; }
        }
    }
}
