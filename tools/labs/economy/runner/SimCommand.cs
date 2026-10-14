using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Frontier.Economy;
using Frontier.Universe;

namespace Frontier.Economy.Runner
{
    /// <summary>
    /// sim [casual|regular|hardcore|all] [days=N] [seed=N] [log=1] [ladder=N] [key=value ...]
    /// Runs the balance simulator and prints the milestone timeline of each archetype.
    /// </summary>
    public static class SimCommand
    {
        public static string Days(double seconds)
        {
            double d = seconds / 86400.0;
            if (d < 1.0 / 24.0) return (seconds / 60.0).ToString("0.0") + "m";
            if (d < 1.0) return (seconds / 3600.0).ToString("0.0") + "h";
            return d.ToString("0.0") + "d";
        }

        public static SimWorld BuildWorld(EconomyCatalog cat, ulong seed, double radius)
        {
            Frontier.Universe.Universe u = StarterSystem.CreateUniverse(cat, seed);
            return SimWorld.Build(cat, u, radius);
        }

        public static int Reach(string[] args)
        {
            Tuning t = Tuning.CreateDefault();
            EconomyCatalog cat = CatalogBuilder.Build(t);
            SimWorld w = BuildWorld(cat, 1, 3600);
            var travel = new TravelModel(t, cat);
            int home = w.planets[w.home].system;
            string[] watch = { "titanium_ore", "crystal", "methane", "helium3", "core", "radium", "dark_matter" };
            Console.WriteLine("jump  range  systems  tier>=3  tier>=4  tier>=5  tier>=6  tier>=7  tier>=8  tier>=9  | resource worlds reachable (landable-agnostic): " + string.Join(" ", watch));
            for (int lvl = 1; lvl <= 10; lvl++)
            {
                double range = travel.JumpRangeLy(lvl);
                int n = w.systemCount;
                var seen = new bool[n];
                var queue = new Queue<int>();
                queue.Enqueue(home);
                seen[home] = true;
                while (queue.Count > 0)
                {
                    int u = queue.Dequeue();
                    for (int v = 0; v < n; v++)
                    {
                        if (seen[v]) continue;
                        double dx = w.sysX[u] - w.sysX[v], dy = w.sysY[u] - w.sysY[v];
                        if (Math.Sqrt(dx * dx + dy * dy) <= range) { seen[v] = true; queue.Enqueue(v); }
                    }
                }
                int count = 0;
                var tierCount = new int[12];
                var resCount = new int[watch.Length];
                var resNear = new double[watch.Length];
                for (int i = 0; i < watch.Length; i++) resNear[i] = double.MaxValue;
                foreach (SimPlanet p in w.planets)
                {
                    if (!seen[p.system]) continue;
                    for (int k = 3; k <= 9; k++) if (p.tier >= k) tierCount[k]++;
                    for (int i = 0; i < watch.Length; i++)
                    {
                        int r = cat.ResourceIndex(watch[i]);
                        if (p.DepositOf(r) >= 0) { resCount[i]++; if (p.distHome < resNear[i]) resNear[i] = p.distHome; }
                    }
                }
                for (int s = 0; s < n; s++) if (seen[s]) count++;
                var sb = new StringBuilder();
                sb.Append(("L" + lvl).PadRight(6)).Append(range.ToString("0").PadLeft(6)).Append(count.ToString().PadLeft(9));
                for (int k = 3; k <= 9; k++) sb.Append(tierCount[k].ToString().PadLeft(9));
                sb.Append("  |");
                for (int i = 0; i < watch.Length; i++) sb.Append(" " + resCount[i] + "@" + (resNear[i] == double.MaxValue ? "-" : resNear[i].ToString("0")));
                Console.WriteLine(sb.ToString());
            }
            return 0;
        }

        public static int Run(string[] args)
        {
            string who = "all";
            double days = 365;
            ulong seed = 1;
            ulong wseed = 0;
            bool log = false, stats = false, goals = false, brief = false;
            int logn = 60;
            int ladderCut = -1;
            double radius = 3600;
            Tuning t = Tuning.CreateDefault();
            for (int i = 1; i < args.Length; i++)
            {
                string a = args[i];
                int eq = a.IndexOf('=');
                if (eq < 0) { who = a; continue; }
                string k = a.Substring(0, eq), v = a.Substring(eq + 1);
                double num = double.Parse(v, CultureInfo.InvariantCulture);
                if (k == "days") days = num;
                else if (k == "seed") seed = (ulong)num;
                else if (k == "wseed") wseed = (ulong)num;
                else if (k == "log") log = num > 0;
                else if (k == "stats") stats = num > 0;
                else if (k == "goals") goals = num > 0;
                else if (k == "brief") brief = num > 0;
                else if (k == "logn") logn = (int)num;
                else if (k == "ladder") ladderCut = (int)num;
                else if (k == "radius") radius = num;
                else t.Set(k, num);
            }
            EconomyCatalog cat = CatalogBuilder.Build(t);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            SimWorld world = BuildWorld(cat, wseed == 0 ? seed : wseed, radius);
            Console.WriteLine("world: " + world.planets.Length + " planets in " + world.systemCount + " systems within " + radius + " ly (" + sw.ElapsedMilliseconds + " ms)");
            string[] lines = SimEngine.DefaultLadderLines;
            if (ladderCut > 0)
            {
                var cut = new List<string>();
                int n = 0;
                foreach (string l in lines) { cut.Add(l); if (l.Length > 0 && l[0] != '#' && ++n >= ladderCut) break; }
                lines = cut.ToArray();
            }
            foreach (Archetype arch in Archetype.All())
            {
                if (who != "all" && who != arch.name) continue;
                var s = new SimSettings { seed = seed, horizonDays = days, keepLog = log, ladder = lines };
                sw.Restart();
                SimResult r = SimEngine.Run(cat, t, world, arch, s);
                Console.WriteLine();
                Console.WriteLine("==== " + arch.name + ": " + arch.sessionsPerDay + " x " + arch.minutesPerSession + " min/day, quality " + arch.quality + ", " + days + " days simulated in " + sw.ElapsedMilliseconds + " ms");
                foreach (string id in r.milestoneOrder) if (!id.StartsWith("goal_") || !goals) if (!brief) Console.WriteLine("  " + Days(r.milestones[id]).PadLeft(8) + "  " + id);
                Console.Write(SimBands.Table(r));
                foreach (string gap in SimBands.CheckGoalGaps(r)) Console.WriteLine("  DEAD ZONE " + gap);
                if (goals)
                {
                    double prevPlay = 0.0;
                    foreach (SimGoalTime g in r.goals)
                    {
                        Console.WriteLine("    #" + g.index.ToString().PadLeft(3) + " " + Days(g.seconds).PadLeft(8) + "  +" + ((g.playSeconds - prevPlay) / 60.0).ToString("0").PadLeft(6) + " min  " + g.text);
                        prevPlay = g.playSeconds;
                    }
                }
                Console.WriteLine("  runs " + r.harvestRuns + "  trips " + r.expeditions + "  waves " + r.waves + "  crafts " + r.craftActions + "  builds " + r.buildJobs + "  stuck " + r.stuck + "  lost " + r.lostItems);
                Console.WriteLine("  play " + (r.playSeconds / 3600.0).ToString("0.0") + " h   idle " + (r.idleSeconds / 3600.0).ToString("0.0") + " h   final score " + r.finalScore + "   era " + r.endEra + "   digest " + r.Digest().ToString("x"));
                if (stats)
                    foreach (double h in new[] { 0.25, 1.0, 3.0, 10.0 })
                    {
                        SimSample sm = r.SampleAtPlayHours(h);
                        Console.WriteLine("    @" + h + "h parts: reach " + (long)sm.scoreParts[0] + " worlds " + (long)sm.scoreParts[1] + " industry " + (long)sm.scoreParts[2] + " tech " + (long)sm.scoreParts[3] + " defense " + (long)sm.scoreParts[4] + " atlas " + (long)sm.scoreParts[5]);
                    }
                Console.WriteLine("  score: " + r.ScoreAtPlayHours(0.25) + " @15min  " + r.ScoreAtPlayHours(1.0) + " @1h  " + r.ScoreAtPlayHours(3.0) + " @3h  " + r.ScoreAtPlayHours(10.0) + " @10h  " + r.ScoreAtDay(7) + " @d7  " + r.ScoreAtDay(30) + " @d30  "
                    + r.ScoreAtDay(Math.Max(0.0, r.Days("era_industry"))) + " @industry  " + r.ScoreAtDay(Math.Max(0.0, r.Days("era_interstellar"))) + " @interstellar  " + r.ScoreAtDay(Math.Max(0.0, r.Days("era_ascension"))) + " @ascension  " + r.finalScore + " final");
                if (stats)
                {
                    var sb = new StringBuilder("  stats:");
                    foreach (string k in r.statOrder) sb.Append(' ').Append(k).Append('=').Append(r.stats[k].ToString("0"));
                    Console.WriteLine(sb.ToString());
                    Console.WriteLine("  " + r.summary);
                }
                if (log)
                {
                    int total = r.log.Count;
                    int from = Math.Max(0, total - logn);
                    for (int i = from; i < total; i++) Console.WriteLine("    " + (r.log[i].Length > 1200 ? r.log[i].Substring(0, 1200) : r.log[i]));
                }
            }
            return 0;
        }
    }
}
