using System;
using System.Collections.Generic;
using System.Text;
using Frontier.Economy;
using Frontier.Universe;

namespace Frontier.Economy.Runner
{
    /// <summary>Prints the weekly shard income of a simulated free player and what skipping its timers would cost.</summary>
    public static class ShardsCommand
    {
        public static int Run(string[] args)
        {
            string who = args.Length > 1 ? args[1] : "regular";
            double adShare = args.Length > 2 ? double.Parse(args[2]) : 0.0;
            Tuning t = Tuning.CreateDefault();
            EconomyCatalog cat = CatalogBuilder.Build(t);
            SimWorld world = SimCommand.BuildWorld(cat, 1UL, 3600.0);
            foreach (Archetype a in Archetype.All())
            {
                if (who != "all" && who != a.name) continue;
                SimResult r = SimEngine.Run(cat, t, world, a, new SimSettings { seed = 1UL, horizonDays = 730.0 });
                ShardSummary s = ShardEconomy.FreePlayer(cat, r, adShare, 0.35);
                Console.WriteLine("== " + a.name + " (ads share " + adShare + "): total " + s.totalEarned + " shards in " + s.weeks.Count + " weeks, mean " + s.meanPerWeek.ToString("0.0") + "/week, median " + s.medianPerWeek + "/week");
                Console.WriteLine("   skip every ordinary job: " + s.skipAllOrdinaryJobs + " shards (" + s.ordinaryJobs + " jobs);  skip every megaproject timer: " + s.skipAllMegaTimers + " shards (" + s.megaTimers + " timers)");
                var sb = new StringBuilder();
                foreach (ShardWeekRow w in s.weeks)
                {
                    if (w.week > 14 && w.week % 8 != 0) continue;
                    sb.Append("   w").Append(w.week.ToString().PadLeft(3)).Append(" L").Append(w.level.ToString().PadLeft(3)).Append("  level ").Append(w.earned.levelUp.ToString().PadLeft(3)).Append(" atlas ").Append(w.earned.atlas.ToString().PadLeft(3))
                      .Append(" ach ").Append(w.earned.achievements.ToString().PadLeft(3)).Append(" streak ").Append(w.earned.streak.ToString().PadLeft(3)).Append(" ads ").Append(w.earned.ads.ToString().PadLeft(3))
                      .Append(" pods ").Append(w.earned.pods.ToString().PadLeft(2)).Append(" board ").Append(w.earned.board.ToString().PadLeft(3)).Append("  = ").Append(w.earned.Total.ToString().PadLeft(4)).Append("   cum ").Append(w.cumulative).AppendLine();
                }
                Console.Write(sb.ToString());
            }
            return 0;
        }
    }
}
