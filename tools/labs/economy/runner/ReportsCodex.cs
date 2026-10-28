using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Frontier.Economy;
using Frontier.Universe;

namespace Frontier.Economy.Runner
{
    /// <summary>Text helpers shared by the generated reports.</summary>
    static class ReportData
    {
        public static string Name(EconomyCatalog cat, string resourceId)
        {
            ResourceDef r = cat.Resource(resourceId);
            return r == null ? resourceId : r.displayName;
        }

        public static string Items(EconomyCatalog cat, ItemAmount[] items)
        {
            var parts = new List<string>();
            foreach (ItemAmount a in items) parts.Add(a.amount + " " + Name(cat, a.resourceId));
            return string.Join(" + ", parts);
        }

        public static string BiomeName(Biome b) { return b == Biome.GasGiant ? "gas giants" : b.ToString(); }

        /// <summary>How a resource is obtained, built from the habitat rows that feed the universe's ResourceTable and from the recipes.</summary>
        public static string Obtain(EconomyCatalog cat, ResourceDef r)
        {
            var parts = new List<string>();
            if (r.category == ResourceCategory.Raw)
            {
                for (int m = 0; m < 7; m++)
                {
                    var method = (HarvestMethod)m;
                    var biomes = new List<Biome>();
                    bool belt = false;
                    int minTier = int.MaxValue;
                    foreach (HabitatDef h in cat.habitats)
                    {
                        if (h.resourceId != r.id || h.method != method) continue;
                        if (h.belt) belt = true;
                        else if (!biomes.Contains(h.biome)) biomes.Add(h.biome);
                        if (h.minTier < minTier) minTier = h.minTier;
                    }
                    if (minTier == int.MaxValue) continue;
                    biomes.Sort((a, b) => ((int)a).CompareTo((int)b));
                    var names = new List<string>();
                    foreach (Biome b in biomes) names.Add(BiomeName(b));
                    if (belt) names.Add("asteroid belts");
                    parts.Add(Verb(method) + " on " + string.Join(", ", names) + " (tier " + minTier + "+)");
                }
                if (parts.Count == 0) parts.Add("not found on any world");
            }
            List<RecipeDef> makers = cat.RecipesFor(r.id);
            for (int i = 0; i < makers.Count; i++)
            {
                RecipeDef c = makers[i];
                StructureDef st = cat.Structure(c.stationId);
                parts.Add((i == 0 ? "Craft" : "Or craft") + " at the " + st.displayName + " (level " + c.stationLevel + "): " + Items(cat, c.inputs) + " -> " + Items(cat, c.outputs));
            }
            return string.Join("; ", parts);
        }

        static string Verb(HarvestMethod m)
        {
            switch (m)
            {
                case HarvestMethod.Chop: return "Chop";
                case HarvestMethod.Mine: return "Mine";
                case HarvestMethod.Drill: return "Drill";
                case HarvestMethod.Siphon: return "Siphon";
                case HarvestMethod.Excavate: return "Excavate";
                case HarvestMethod.Salvage: return "Salvage";
                default: return "Harvest";
            }
        }

        static string Capped(List<string> names, int max)
        {
            if (names.Count <= max) return string.Join(", ", names);
            return string.Join(", ", names.GetRange(0, max)) + " and " + (names.Count - max) + " more";
        }

        /// <summary>Everything that consumes the resource, generated from the recipes, structure, tool, upgrade and research costs.</summary>
        public static string UsedIn(EconomyCatalog cat, ResourceDef r)
        {
            var parts = new List<string>();
            var rec = new List<string>();
            foreach (RecipeDef c in cat.RecipesUsing(r.id)) rec.Add(c.displayName);
            if (rec.Count > 0) parts.Add("Recipes: " + Capped(rec, 6));
            var str = new List<string>();
            foreach (StructureDef s in cat.structures)
            {
                bool uses = false;
                foreach (StructureLevelDef l in s.levels) foreach (ItemAmount a in l.cost) if (a.resourceId == r.id) uses = true;
                if (uses) str.Add(s.displayName);
            }
            if (str.Count > 0) parts.Add("Structures: " + Capped(str, 6));
            var tools = new List<string>();
            foreach (ToolLadderDef l in cat.tools)
            {
                bool uses = false;
                foreach (ToolTierDef t in l.tiers) foreach (ItemAmount a in t.cost) if (a.resourceId == r.id) uses = true;
                if (uses) tools.Add(l.displayName);
            }
            if (tools.Count > 0) parts.Add("Tools: " + Capped(tools, 6));
            var upg = new List<string>();
            foreach (UpgradeDef u in cat.upgrades)
            {
                bool uses = false;
                foreach (UpgradeLevelDef l in u.levels) foreach (ItemAmount a in l.cost) if (a.resourceId == r.id) uses = true;
                if (uses) upg.Add(u.displayName);
            }
            if (upg.Count > 0) parts.Add("Upgrades: " + Capped(upg, 6));
            int rsr = 0;
            foreach (ResearchNodeDef n in cat.research) foreach (ItemAmount a in n.cost) if (a.resourceId == r.id) rsr++;
            if (rsr > 0) parts.Add("Research: " + rsr + (rsr == 1 ? " node" : " nodes"));
            var obs = new List<string>();
            foreach (ObstacleKindDef k in cat.obstacles)
            {
                bool uses = false;
                foreach (ObstacleTierCost tc in k.byTier) foreach (ItemAmount a in tc.cost) if (a.resourceId == r.id) uses = true;
                if (uses) obs.Add(k.displayName);
            }
            if (obs.Count > 0) parts.Add("Clearing: " + Capped(obs, 4));
            if (r.fuelEnergy > 0) parts.Add("Ship fuel (" + r.fuelEnergy + " energy per unit)");
            return parts.Count == 0 ? "nothing yet" : string.Join(". ", parts);
        }

        public static string Value(double v)
        {
            if (v >= 1e9) return (v / 1e9).ToString("0.##") + "B";
            if (v >= 1e6) return (v / 1e6).ToString("0.##") + "M";
            if (v >= 1e4) return (v / 1e3).ToString("0.#") + "k";
            if (v >= 100) return v.ToString("0");
            return v.ToString("0.##");
        }

        public static string Seconds(double s)
        {
            if (s < 0.5) return "instant";
            if (s < 90) return s.ToString("0") + " s";
            if (s < 5400) return (s / 60.0).ToString("0.#") + " min";
            if (s < 172800) return (s / 3600.0).ToString("0.#") + " h";
            return (s / 86400.0).ToString("0.#") + " d";
        }

        public static string Days(double d)
        {
            if (double.IsNaN(d)) return "never";
            if (d < 1.0 / 24.0) return (d * 1440.0).ToString("0.#") + " min";
            if (d < 2.0) return (d * 24.0).ToString("0.#") + " h";
            return d.ToString("0.#") + " d";
        }

        public static void Table(StringBuilder sb, string[] headers, List<string[]> rows)
        {
            sb.Append("| ").Append(string.Join(" | ", headers)).AppendLine(" |");
            sb.Append("|");
            foreach (string h in headers) sb.Append("---|");
            sb.AppendLine();
            foreach (string[] row in rows)
            {
                sb.Append("| ");
                for (int i = 0; i < row.Length; i++) { if (i > 0) sb.Append(" | "); sb.Append(row[i].Replace("|", "/")); }
                sb.AppendLine(" |");
            }
            sb.AppendLine();
        }
    }

    /// <summary>ATLAS_TEXT.md, ICON_BRIEF.md and TUNING_KEYS.md.</summary>
    public static class CodexReports
    {
        public static int Run(string[] args)
        {
            string dir = args.Length > 1 ? Path.GetFullPath(args[1]) : Export.DefaultDir("reports");
            Directory.CreateDirectory(dir);
            Tuning t = Tuning.CreateDefault();
            EconomyCatalog cat = CatalogBuilder.Build(t);
            File.WriteAllText(Path.Combine(dir, "ATLAS_TEXT.md"), Atlas(cat));
            File.WriteAllText(Path.Combine(dir, "ICON_BRIEF.md"), IconBrief(cat));
            File.WriteAllText(Path.Combine(dir, "TUNING_KEYS.md"), TuningKeys(t));
            Console.WriteLine("codex reports written to " + dir);
            return 0;
        }

        static readonly string[] CategoryNames = { "Raw materials", "Processed goods", "Alloys", "Components", "Energy" };

        // ------------------------------------------------------------------------------------------------ atlas
        public static string Atlas(EconomyCatalog cat)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Atlas text: every resource and recipe entry as the in-game codex shows it");
            sb.AppendLine();
            sb.AppendLine("Generated by `run.ps1 report` from the catalog (schema " + cat.schemaVersion + ", content " + cat.contentVersion + "). The description is authored text; **How to obtain** is generated from the universe resource table (habitat rows: biome, harvest method, first tier) and the recipes; **Used in** is generated from every recipe, structure, tool, upgrade, research and clearing cost that consumes the item. Re-run the generator after any balance change.");
            sb.AppendLine();
            for (int c = 0; c < 5; c++)
            {
                sb.AppendLine("## " + CategoryNames[c]);
                sb.AppendLine();
                foreach (ResourceDef r in cat.resources)
                {
                    if ((int)r.category != c) continue;
                    sb.AppendLine("### " + r.displayName + " (`" + r.id + "`)");
                    sb.AppendLine();
                    sb.AppendLine(r.category + " | tier " + r.tier + " | " + r.stackSize + " per slot | worth " + ReportData.Value(r.baseValue) + " scrap" + (r.fuelEnergy > 0 ? " | burns for " + r.fuelEnergy + " energy" : ""));
                    sb.AppendLine();
                    sb.AppendLine("> " + r.atlasDescription);
                    sb.AppendLine();
                    sb.AppendLine("- **How to obtain:** " + ReportData.Obtain(cat, r) + ".");
                    sb.AppendLine("- **Used in:** " + ReportData.UsedIn(cat, r) + ".");
                    sb.AppendLine();
                }
            }
            sb.AppendLine("## Recipes");
            sb.AppendLine();
            foreach (RecipeDef r in cat.recipes)
            {
                StructureDef st = cat.Structure(r.stationId);
                sb.AppendLine("### " + r.displayName + " (`" + r.id + "`)");
                sb.AppendLine();
                sb.AppendLine(r.kind + " | " + st.displayName + " level " + r.stationLevel + " | tier " + r.tier);
                sb.AppendLine();
                sb.AppendLine("> " + Sentence(cat, r, st));
                sb.AppendLine();
                sb.AppendLine("- **Recipe:** " + ReportData.Items(cat, r.inputs) + " -> " + ReportData.Items(cat, r.outputs) + " (instant, one tap per batch).");
                ResearchNodeDef rs = cat.Research(r.unlockResearch);
                sb.AppendLine("- **Unlocked by:** " + (rs == null ? "known from the start" : "research " + rs.displayName) + ".");
                double vin = cat.Prices.Value(r.inputs), vout = cat.Prices.Value(r.outputs);
                sb.AppendLine("- **Value:** " + ReportData.Value(vin) + " in, " + ReportData.Value(vout) + " out (ratio " + (vout / vin).ToString("0.00") + ").");
                var users = new List<string>();
                foreach (ItemAmount o in r.outputs) { string u = ReportData.UsedIn(cat, cat.Resource(o.resourceId)); users.Add(cat.Resource(o.resourceId).displayName + ": " + u); }
                sb.AppendLine("- **Makes:** " + string.Join(" | ", users) + ".");
                sb.AppendLine();
            }
            return sb.ToString();
        }

        static string Sentence(EconomyCatalog cat, RecipeDef r, StructureDef st)
        {
            ResourceDef o = cat.Resource(r.outputs[0].resourceId);
            string first = ReportData.Name(cat, r.inputs[0].resourceId).ToLowerInvariant();
            return "The " + st.displayName + " turns " + first + (r.inputs.Length > 1 ? " and " + (r.inputs.Length - 1) + " more ingredient" + (r.inputs.Length > 2 ? "s" : "") : "") + " into " + o.displayName.ToLowerInvariant() + ".";
        }

        // ------------------------------------------------------------------------------------------------ icon brief
        public static string IconBrief(EconomyCatalog cat)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Icon brief");
            sb.AppendLine();
            sb.AppendLine("One icon per resource, drawn from the `iconConcept` below. Rules: the silhouette must read at **32 px** on a dark background; no two resources share a silhouette idea; fill with the category colour, add the accent as one small highlight or edge; no text, no emojis in the art. Colour family per category: ");
            sb.AppendLine();
            for (int c = 0; c < 5; c++) sb.AppendLine("- **" + CategoryNames[c] + "**: " + cat.categoryHex[c]);
            sb.AppendLine();
            for (int c = 0; c < 5; c++)
            {
                sb.AppendLine("## " + CategoryNames[c] + " (" + cat.categoryHex[c] + ")");
                sb.AppendLine();
                var rows = new List<string[]>();
                foreach (ResourceDef r in cat.resources)
                    if ((int)r.category == c) rows.Add(new[] { "`" + r.id + "`", r.displayName, "T" + r.tier, r.colorHex, r.accentHex, r.iconConcept });
                ReportData.Table(sb, new[] { "id", "name", "tier", "colour", "accent", "silhouette concept" }, rows);
            }
            sb.AppendLine("## Obstacle clutter (one sprite family per kind)");
            sb.AppendLine();
            var orows = new List<string[]>();
            foreach (ObstacleKindDef k in cat.obstacles) orows.Add(new[] { "`" + k.id + "`", k.displayName, k.biomes.Replace(";", ", "), k.minTier.ToString(), k.iconConcept });
            ReportData.Table(sb, new[] { "id", "name", "biomes", "first tier", "silhouette concept" }, orows);
            sb.AppendLine("Shard pods are a normal obstacle with a small violet glow (accent #B07CFF) in the debris; the art brief for the pod itself belongs to the shop and cosmetics pass.");
            return sb.ToString();
        }

        // ------------------------------------------------------------------------------------------------ tuning keys
        public static string TuningKeys(Tuning t)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Tuning keys (" + t.Count + ")");
            sb.AppendLine();
            sb.AppendLine("Every number of the economy is one key of the single `Tuning` object (string key -> float with default, min, max). Keys that should be exposed through Remote Config are marked in INTEGRATION.md; the rest are design-time knobs. Digest of the defaults: `" + t.Digest().ToString("x") + "`.");
            sb.AppendLine();
            var groups = new List<string>();
            for (int i = 0; i < t.Count; i++) if (!groups.Contains(t.EntryAt(i).group)) groups.Add(t.EntryAt(i).group);
            sb.AppendLine("| group | keys |");
            sb.AppendLine("|---|---|");
            foreach (string g in groups) { int n = 0; for (int i = 0; i < t.Count; i++) if (t.EntryAt(i).group == g) n++; sb.AppendLine("| " + g + " | " + n + " |"); }
            sb.AppendLine();
            foreach (string g in groups)
            {
                sb.AppendLine("## " + g);
                sb.AppendLine();
                var rows = new List<string[]>();
                for (int i = 0; i < t.Count; i++)
                {
                    TuningEntry e = t.EntryAt(i);
                    if (e.group != g) continue;
                    rows.Add(new[] { "`" + e.key + "`", e.defaultValue.ToString("0.####"), e.min.ToString("0.####") + " .. " + e.max.ToString("0.####"), e.description });
                }
                ReportData.Table(sb, new[] { "key", "default", "range", "meaning" }, rows);
            }
            return sb.ToString();
        }
    }
}
