using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Frontier.Economy;
using Frontier.Universe;

namespace Frontier.Economy.Runner
{
    /// <summary>How a tuning key may be changed after release.</summary>
    public enum Exposure { Remote, Server, Content, Dev }

    /// <summary>
    /// Fills INTEGRATION.template.md with the facts that must never drift from the code: the field list of every data type (reflected) and the
    /// Remote Config classification of every tuning key. Writes INTEGRATION.md and data\remote_config.json.
    /// </summary>
    public static class IntegrationDoc
    {
        public static Exposure Classify(TuningEntry e)
        {
            string k = e.key;
            switch (e.group)
            {
                case "sim": return Exposure.Dev;
                case "value":
                case "yield":
                case "upgrade": return Exposure.Content;
                case "score": return Exposure.Server;
                case "cost":
                    return k.StartsWith("cost.v.", StringComparison.Ordinal) || k.StartsWith("cost.v2.", StringComparison.Ordinal) || k.StartsWith("cost.g.", StringComparison.Ordinal) ? Exposure.Content : Exposure.Remote;
                case "time":
                    return k.StartsWith("time.start.", StringComparison.Ordinal) || k.StartsWith("time.step.", StringComparison.Ordinal) ? Exposure.Content : Exposure.Remote;
                default: return Exposure.Remote;
            }
        }

        static readonly object[][] Types =
        {
            new object[] { typeof(ResourceDef), "ResourceSO", "extend the existing asset; one per resource", "resources.json" },
            new object[] { typeof(HabitatDef), "HabitatTableSO.rows[]", "new, one asset for the whole resource-availability profile (feeds the universe ResourceTable)", "resource_habitats.json" },
            new object[] { typeof(RecipeDef), "RecipeSO", "extend the existing asset (keep the legacy `fabricator` flag, derive it from stationId)", "recipes.json" },
            new object[] { typeof(StructureDef), "StructureSO", "extend the existing asset (keep buildCost = level 1 cost and the legacy turret/warehouse fields)", "structures.json" },
            new object[] { typeof(StructureLevelDef), "StructureSO.levels[]", "serializable row inside StructureSO", "structures.json" },
            new object[] { typeof(ToolLadderDef), "ToolLadderSO", "new; replaces the legacy axe/pick/drill UpgradeSOs (ids stay)", "tools.json" },
            new object[] { typeof(ToolTierDef), "ToolLadderSO.tiers[]", "serializable row", "tools.json" },
            new object[] { typeof(UpgradeDef), "UpgradeSO", "extend the existing asset", "upgrades.json" },
            new object[] { typeof(UpgradeLevelDef), "UpgradeSO.levels[]", "serializable row (replaces levelCosts/levelNames/valuePerLevel)", "upgrades.json" },
            new object[] { typeof(ResearchNodeDef), "ResearchNodeSO", "new; one per node", "research.json" },
            new object[] { typeof(ObstacleKindDef), "ObstacleKindSO", "new; one per clutter kind", "obstacles.json" },
            new object[] { typeof(ObstacleTierCost), "ObstacleKindSO.byTier[]", "serializable row", "obstacles.json" },
            new object[] { typeof(ObstacleRulesDef), "ObstacleRulesSO", "new, single asset", "obstacles.json" },
            new object[] { typeof(ShardRulesDef), "ShardRulesSO", "new, single asset", "shards.json" },
            new object[] { typeof(ShardPackDef), "ShardRulesSO.packs[]", "serializable row (store product ids are mapped by id)", "shards.json" },
            new object[] { typeof(ScoreFormula), "ScoreFormulaSO", "new, single asset (the weights of plan 8.1)", "score_formula.json" },
            new object[] { typeof(LegacyIdEntry), "LegacyIdMapSO.rows[]", "new, single asset used by the save migrator", "legacy_ids.json" },
            new object[] { typeof(StartDef), "GameDatabase (fields)", "startingItems, baseBackpackSlots, startPlanetId, builders go onto the existing GameDatabase", "start.json" },
            new object[] { typeof(TuningEntry), "EconomyTuningSO.entries[]", "new, single asset: the default value, range and group of every key", "tuning.json" }
        };

        static string TypeName(Type t)
        {
            if (t == typeof(int)) return "int";
            if (t == typeof(float)) return "float";
            if (t == typeof(double)) return "double";
            if (t == typeof(bool)) return "bool";
            if (t == typeof(string)) return "string";
            if (t == typeof(uint)) return "uint";
            if (t.IsArray) return TypeName(t.GetElementType()) + "[]";
            return t.Name;
        }

        static string Schema()
        {
            var sb = new StringBuilder();
            foreach (object[] row in Types)
            {
                var type = (Type)row[0];
                sb.AppendLine("#### `" + type.Name + "` -> `" + row[1] + "`");
                sb.AppendLine();
                sb.AppendLine((string)row[2] + ". Source file: `data\\" + row[3] + "`.");
                sb.AppendLine();
                var rows = new List<string[]>();
                foreach (FieldInfo f in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
                    rows.Add(new[] { "`" + f.Name + "`", "`" + TypeName(f.FieldType) + "`", f.FieldType.IsEnum ? "enum: " + string.Join(", ", Enum.GetNames(f.FieldType)) : "" });
                ReportData.Table(sb, new[] { "field", "type", "values" }, rows);
            }
            return sb.ToString();
        }

        static string Remote(Tuning t)
        {
            var sb = new StringBuilder();
            var counts = new int[4];
            var rows = new List<string[]>();
            var seen = new List<string>();
            for (int i = 0; i < t.Count; i++)
            {
                TuningEntry e = t.EntryAt(i);
                Exposure x = Classify(e);
                counts[(int)x]++;
            }
            // one row per (exposure, group) pair with an example, so the table stays readable; the full list is data\remote_config.json
            foreach (Exposure x in new[] { Exposure.Remote, Exposure.Server, Exposure.Content, Exposure.Dev })
            {
                var groups = new List<string>();
                for (int i = 0; i < t.Count; i++) if (Classify(t.EntryAt(i)) == x && !groups.Contains(t.EntryAt(i).group)) groups.Add(t.EntryAt(i).group);
                foreach (string g in groups)
                {
                    int n = 0; string first = "";
                    for (int i = 0; i < t.Count; i++) if (Classify(t.EntryAt(i)) == x && t.EntryAt(i).group == g) { if (n == 0) first = t.EntryAt(i).key; n++; }
                    rows.Add(new[] { x.ToString(), g, n.ToString(), "`" + first + "`" });
                }
            }
            ReportData.Table(sb, new[] { "exposure", "group", "keys", "first key" }, rows);
            sb.AppendLine("Totals: **" + counts[(int)Exposure.Remote] + "** client Remote Config keys, **" + counts[(int)Exposure.Server] + "** server-side keys (score weights, read by the Cloud Code validator), **" + counts[(int)Exposure.Content] + "** content keys (change only with a content update), **" + counts[(int)Exposure.Dev] + "** simulator-only keys.");
            return sb.ToString();
        }

        public static int Run(string[] args)
        {
            string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, ".."));
            string template = Path.Combine(root, "INTEGRATION.template.md");
            if (!File.Exists(template)) { Console.WriteLine("missing " + template); return 1; }
            Tuning t = Tuning.CreateDefault();
            EconomyCatalog cat = CatalogBuilder.Build(t);
            string text = File.ReadAllText(template);
            text = text.Replace("{{SCHEMA}}", Schema());
            text = text.Replace("{{REMOTE}}", Remote(t));
            text = text.Replace("{{COUNTS}}", cat.resources.Length + " resources, " + cat.recipes.Length + " recipes, " + cat.structures.Length + " structures, " + cat.tools.Length + " tool ladders, " + cat.upgrades.Length + " upgrades, "
                + cat.research.Length + " research nodes, " + cat.obstacles.Length + " obstacle kinds, " + t.Count + " tuning keys (tuning digest `" + t.Digest().ToString("x") + "`)");
            File.WriteAllText(Path.Combine(root, "INTEGRATION.md"), text);
            Console.WriteLine("INTEGRATION.md written to " + root);
            return 0;
        }
    }
}
