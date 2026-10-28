using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Frontier.Economy;
using Frontier.Universe;

namespace Frontier.Economy.Runner
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            string cmd = args.Length > 0 ? args[0] : "test";
            try
            {
                switch (cmd)
                {
                    case "test": return TestRunner.Run(args);
                    case "tables": return Tables.Run(args);
                    case "sim": return SimCommand.Run(args);
                    case "reach": return SimCommand.Reach(args);
                    case "charts": return Charts.Run(args);
                    case "shards": return ShardsCommand.Run(args);
                    case "export": return Export.Run(args);
                    case "codex": return CodexReports.Run(args);
                    case "report": return EconomyReport.Run(args);
                    case "integration": return IntegrationDoc.Run(args);
                    default:
                        Console.WriteLine("unknown command " + cmd);
                        return 2;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return 3;
            }
        }
    }

    /// <summary>Quick text dumps of the catalog for designing and tuning.</summary>
    public static class Tables
    {
        static string Items(ItemAmount[] a)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < a.Length; i++) { if (i > 0) sb.Append(' '); sb.Append(a[i].amount).Append(' ').Append(a[i].resourceId); }
            return sb.ToString();
        }

        static string Time(int s)
        {
            if (s < 60) return s + "s";
            if (s < 3600) return (s / 60.0).ToString("0.#") + "m";
            if (s < 86400 * 2) return (s / 3600.0).ToString("0.#") + "h";
            return (s / 86400.0).ToString("0.#") + "d";
        }

        public static int Run(string[] args)
        {
            string what = args.Length > 1 ? args[1] : "all";
            Tuning t = Tuning.CreateDefault();
            EconomyCatalog c = CatalogBuilder.Build(t);
            var pb = c.Prices;
            if (what == "all" || what == "res")
            {
                Console.WriteLine("== resources (" + c.resources.Length + ")");
                foreach (ResourceDef r in c.resources)
                    Console.WriteLine(r.id.PadRight(18) + r.category.ToString().PadRight(10) + " t" + r.tier + " v=" + r.baseValue.ToString("0.00").PadLeft(8) + " stack " + r.stackSize.ToString().PadLeft(3) + " t0vol " + pb.T0Volume(r.id).ToString("0.0"));
            }
            if (what == "all" || what == "rec")
            {
                Console.WriteLine("== recipes (" + c.recipes.Length + ")");
                foreach (RecipeDef r in c.recipes)
                {
                    double vin = pb.Value(r.inputs), vout = pb.Value(r.outputs);
                    Console.WriteLine(r.id.PadRight(24) + r.stationId.PadRight(13) + "L" + r.stationLevel + " t" + r.tier + "  " + Items(r.inputs) + " -> " + Items(r.outputs) + "   ratio " + (vout / vin).ToString("0.00"));
                }
            }
            if (what == "all" || what == "str")
            {
                Console.WriteLine("== structures (" + c.structures.Length + ")");
                foreach (StructureDef s in c.structures)
                {
                    Console.WriteLine(s.id + " (" + s.role + " t" + s.tier + " fp" + s.footprint + ")");
                    int[] show = { 1, 2, 3, 5, 8, 10 };
                    foreach (int l in show)
                    {
                        if (l > s.maxLevel) continue;
                        StructureLevelDef d = s.levels[l - 1];
                        Console.WriteLine("   L" + l.ToString().PadRight(3) + Time(d.buildSeconds).PadRight(7) + "reach" + d.requiresReachTier + "  val " + pb.Value(d.cost).ToString("0").PadLeft(9) + "  " + Items(d.cost));
                    }
                }
            }
            if (what == "all" || what == "tool")
            {
                foreach (ToolLadderDef l in c.tools)
                {
                    Console.WriteLine("tool " + l.id);
                    foreach (ToolTierDef d in l.tiers) Console.WriteLine("   T" + d.tier + " " + d.displayName.PadRight(20) + "x" + d.yieldMult + " reach" + d.requiresReachTier + " lab" + d.requiresLabLevel + " val " + pb.Value(d.cost).ToString("0").PadLeft(8) + "  " + Items(d.cost));
                }
            }
            if (what == "all" || what == "upg")
            {
                foreach (UpgradeDef u in c.upgrades)
                {
                    Console.WriteLine("upgrade " + u.id);
                    foreach (UpgradeLevelDef d in u.levels) Console.WriteLine("   L" + d.level + " " + d.displayName.PadRight(22) + "=" + d.value + " reach" + d.requiresReachTier + " " + (d.researchPrereq ?? "") + " val " + pb.Value(d.cost).ToString("0").PadLeft(8) + "  " + Items(d.cost));
                }
            }
            if (what == "all" || what == "rsr")
            {
                foreach (ResearchNodeDef n in c.research)
                    Console.WriteLine("research " + n.id.PadRight(24) + "t" + n.tier + " [" + string.Join(" ", n.prereqs) + "] val " + pb.Value(n.cost).ToString("0").PadLeft(8) + "  " + Items(n.cost));
            }
            if (what == "slots")
            {
                var rows = new List<string>();
                var stackOf = new Dictionary<string, int>();
                foreach (ResourceDef r in c.resources) stackOf[r.id] = r.stackSize;
                Func<ItemAmount[], int> slotsOf = a => { int s = 0; for (int i = 0; i < a.Length; i++) s += (a[i].amount + stackOf[a[i].resourceId] - 1) / stackOf[a[i].resourceId]; return s; };
                foreach (StructureDef s in c.structures)
                    foreach (StructureLevelDef d in s.levels) if (d.cost.Length > 0 && !(s.id == "terraformer" || s.id == "warp_gate" || s.id == "dyson_collector")) rows.Add(slotsOf(d.cost).ToString("0000") + " reach" + d.requiresReachTier + " str " + s.id + " L" + d.level + " val " + pb.Value(d.cost).ToString("0"));
                foreach (ToolLadderDef l in c.tools) foreach (ToolTierDef d in l.tiers) if (d.cost.Length > 0) rows.Add(slotsOf(d.cost).ToString("0000") + " reach" + d.requiresReachTier + " tool " + l.id + " T" + d.tier + " val " + pb.Value(d.cost).ToString("0"));
                foreach (UpgradeDef u in c.upgrades) foreach (UpgradeLevelDef d in u.levels) rows.Add(slotsOf(d.cost).ToString("0000") + " reach" + d.requiresReachTier + " upg " + u.id + " L" + d.level + " val " + pb.Value(d.cost).ToString("0"));
                foreach (ResearchNodeDef n in c.research) rows.Add(slotsOf(n.cost).ToString("0000") + " t" + n.tier + " rsr " + n.id + " val " + pb.Value(n.cost).ToString("0"));
                rows.Sort(StringComparer.Ordinal);
                rows.Reverse();
                for (int i = 0; i < Math.Min(60, rows.Count); i++) Console.WriteLine(rows[i]);
            }
            Console.WriteLine("tuning keys: " + t.Count);
            return 0;
        }
    }
}
