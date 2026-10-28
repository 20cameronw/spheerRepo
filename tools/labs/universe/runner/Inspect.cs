using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Frontier.Universe.Runner
{
    /// <summary>Console diagnostics used while tuning the generator.</summary>
    public static class Inspect
    {
        public static int Run(string[] args)
        {
            if (args.Length > 1 && args[1] == "region") return FindRegion();
            if (args.Length > 1 && args[1] == "biomes") return BiomeBands();
            var cfg = new UniverseConfig();
            if (args.Length > 1 && args[1] == "planned") cfg.EnablePlannedResources = true;
            var u = new Universe(cfg, Landmarks.CreateDefault());

            Console.WriteLine("Home galaxy density at origin: " + u.Density(0, 0).ToString("0.000"));
            var sw = Stopwatch.StartNew();
            int sectors = 0, systems = 0, planets = 0;
            var classCount = new int[9];
            var biomeCount = new int[12];
            var sizeCount = new int[6];
            var kindCount = new int[3];
            var tierHist = new int[41];
            var resCount = new Dictionary<string, int>();
            var depositsHist = new int[10];
            long hazardMask = 0;
            for (int sy = -25; sy < 25; sy++)
            {
                for (int sx = -25; sx < 25; sx++)
                {
                    sectors++;
                    var sys = u.SystemsInSector(new SectorCoord(sx, sy));
                    systems += sys.Length;
                    for (int i = 0; i < sys.Length; i++)
                    {
                        classCount[(int)sys[i].StarClass]++;
                        var ps = u.PlanetsOf(sys[i]);
                        planets += ps.Length;
                        for (int p = 0; p < ps.Length; p++)
                        {
                            biomeCount[(int)ps[p].Biome]++;
                            sizeCount[(int)ps[p].Size]++;
                            kindCount[(int)ps[p].Kind]++;
                            tierHist[ps[p].Tier]++;
                            depositsHist[Math.Min(9, ps[p].Deposits.Length)]++;
                            hazardMask |= (long)ps[p].Hazards;
                            for (int d = 0; d < ps[p].Deposits.Length; d++)
                            {
                                int c;
                                resCount.TryGetValue(ps[p].Deposits[d].ResourceId, out c);
                                resCount[ps[p].Deposits[d].ResourceId] = c + 1;
                            }
                        }
                    }
                }
            }
            Console.WriteLine("50x50 sectors around origin: " + systems + " systems, " + planets + " planets in " + sw.ElapsedMilliseconds + " ms");
            Console.WriteLine("star classes  : " + Join(Enum.GetNames(typeof(StarClass)), classCount));
            Console.WriteLine("biomes        : " + Join(Enum.GetNames(typeof(Biome)), biomeCount));
            Console.WriteLine("sizes         : " + Join(Enum.GetNames(typeof(SizeClass)), sizeCount));
            Console.WriteLine("kinds         : " + Join(Enum.GetNames(typeof(PlanetKind)), kindCount));
            var tiers = new StringBuilder();
            for (int t = 0; t < tierHist.Length; t++) if (tierHist[t] > 0) tiers.Append("T" + t + "=" + tierHist[t] + " ");
            Console.WriteLine("tiers         : " + tiers);
            var rs = new StringBuilder();
            foreach (var kv in resCount) rs.Append(kv.Key + "=" + kv.Value + " ");
            Console.WriteLine("resources     : " + rs);
            Console.WriteLine("deposit counts: " + string.Join(",", depositsHist));
            Console.WriteLine("hazard bits seen: " + hazardMask);

            Console.WriteLine();
            var home = u.SystemsInSector(new SectorCoord(0, 0));
            Console.WriteLine("Systems in sector 0,0: " + home.Length);
            for (int i = 0; i < Math.Min(2, home.Length); i++)
            {
                Console.WriteLine("  " + home[i] + " " + home[i].Designation + " L=" + home[i].Luminosity.ToString("0.###") + " tier " + home[i].Tier);
                foreach (var p in u.PlanetsOf(home[i]))
                {
                    var sb = new StringBuilder();
                    foreach (var d in p.Deposits) sb.Append(d.ResourceId + "(" + d.Method + " x" + d.Richness.ToString("0.00") + " y" + d.BaseYield + " d" + d.Difficulty.ToString("0.00") + ") ");
                    Console.WriteLine("    " + p.Id + " " + p.Name + " " + p.Designation + " " + p.Biome + "/" + p.Kind + "/" + p.Size + " T" + p.Tier + " slots " + p.BuildingSlots + " danger " + p.Danger.ToString("0.0") + " haz " + p.Hazards + " wx " + p.Weather + " model " + p.ModelKey + " temp " + p.Temperature.ToString("0") + "K rings " + p.HasRings + " moons " + p.MoonCount);
                    Console.WriteLine("       " + sb);
                }
            }
            Console.WriteLine("Region digest 50x40 @ (-25,-20): 0x" + UniverseDigest.OfRegion(u, -25, -20, 25, 20).ToString("X16"));
            return 0;
        }

        static string Join(string[] names, int[] counts)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < counts.Length; i++) sb.Append(names[i] + "=" + counts[i] + " ");
            return sb.ToString();
        }

        static int BiomeBands()
        {
            var u = new Universe(new UniverseConfig(), Landmarks.CreateDefault());
            int[] edges = { 0, 3, 6, 9, 13, 41 };
            var counts = new int[edges.Length - 1][];
            for (int i = 0; i < counts.Length; i++) counts[i] = new int[12];
            var totals = new int[edges.Length - 1];
            for (int sy = -100; sy < 100; sy++)
                for (int sx = -100; sx < 100; sx++)
                    foreach (var s in u.SystemsInSector(new SectorCoord(sx, sy)))
                        foreach (var p in u.PlanetsOf(s))
                            for (int b = 0; b < counts.Length; b++)
                                if (p.Tier >= edges[b] && p.Tier < edges[b + 1]) { counts[b][(int)p.Biome]++; totals[b]++; }
            var names = Enum.GetNames(typeof(Biome));
            for (int b = 0; b < counts.Length; b++)
            {
                var sb = new StringBuilder();
                for (int k = 0; k < 12; k++) sb.Append(names[k] + " " + (100.0 * counts[b][k] / Math.Max(1, totals[b])).ToString("0.0") + "%  ");
                Console.WriteLine("tiers " + edges[b] + "-" + (edges[b + 1] - 1) + " (n=" + totals[b] + "): " + sb);
            }
            return 0;
        }

        static int FindRegion()
        {
            var u = new Universe(new UniverseConfig());
            const int R = 250, W = 50, H = 40;
            int size = 2 * R;
            var grid = new int[size + 1, size + 1];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    grid[x + 1, y + 1] = grid[x, y + 1] + grid[x + 1, y] - grid[x, y] + u.SystemsInSector(new SectorCoord(x - R, y - R)).Length;
            var best = new List<int[]>();
            for (int y = 0; y + H <= size; y += 5)
                for (int x = 0; x + W <= size; x += 5)
                {
                    int c = grid[x + W, y + H] - grid[x, y + H] - grid[x + W, y] + grid[x, y];
                    best.Add(new[] { c, x - R, y - R });
                }
            best.Sort((a, b) => b[0].CompareTo(a[0]));
            for (int i = 0; i < 8; i++)
                Console.WriteLine("window " + W + "x" + H + " at sectors (" + best[i][1] + "," + best[i][2] + ") holds " + best[i][0] + " systems");
            return 0;
        }
    }
}
