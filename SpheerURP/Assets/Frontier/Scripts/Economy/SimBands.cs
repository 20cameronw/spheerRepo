using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Frontier.Economy
{
    /// <summary>One target band: a milestone of a player archetype must land between two times (days since the first session started).</summary>
    public sealed class SimBand
    {
        public string archetype;
        public string milestone;
        public string label;
        public double minDays, maxDays;
    }

    /// <summary>
    /// The pacing targets of the six eras (plan 5.8) for the three archetypes, as bands on the day the era is ENTERED, plus the first-session
    /// promises. The bands encode the owner's intent: first session reaches the first Warehouse, first fuel and first hop; day one is Foothold;
    /// Settlement in the first week; Industry within the first month; Interstellar after two to four months for a regular player; the rest is endgame.
    /// </summary>
    public static class SimBands
    {
        public static readonly string[] EraNames = { "Landfall", "Foothold", "Settlement", "Industry", "Interstellar", "Ascension" };

        public static readonly string[] EraMilestones = { "", "era_foothold", "era_settlement", "era_industry", "era_interstellar", "era_ascension" };

        const double Min = 1.0 / 1440.0;

        static SimBand B(string a, string m, string label, double min, double max) { return new SimBand { archetype = a, milestone = m, label = label, minDays = min, maxDays = max }; }

        public static List<SimBand> For(string archetype)
        {
            var l = new List<SimBand>();
            switch (archetype)
            {
                case "casual":
                    l.Add(B(archetype, "first_warehouse", "first Warehouse", 0, 1.0));
                    l.Add(B(archetype, "first_fuel", "first fuel", 0, 1.0));
                    l.Add(B(archetype, "first_hop", "first hop", 0, 1.0));
                    l.Add(B(archetype, "era_settlement", "Settlement", 4, 16));
                    l.Add(B(archetype, "era_industry", "Industry", 25, 70));
                    l.Add(B(archetype, "era_interstellar", "Interstellar", 140, 330));
                    l.Add(B(archetype, "era_ascension", "Ascension", 330, 720));
                    break;
                case "regular":
                    l.Add(B(archetype, "first_warehouse", "first Warehouse", 0, 6 * Min));
                    l.Add(B(archetype, "first_fuel", "first fuel", 0, 12 * Min));
                    l.Add(B(archetype, "first_hop", "first hop", 0, 15 * Min));
                    l.Add(B(archetype, "era_settlement", "Settlement", 1.5, 7));
                    l.Add(B(archetype, "era_industry", "Industry", 8, 30));
                    l.Add(B(archetype, "era_interstellar", "Interstellar", 55, 120));
                    l.Add(B(archetype, "era_ascension", "Ascension", 140, 300));
                    break;
                default:
                    l.Add(B(archetype, "first_warehouse", "first Warehouse", 0, 6 * Min));
                    l.Add(B(archetype, "first_fuel", "first fuel", 0, 12 * Min));
                    l.Add(B(archetype, "first_hop", "first hop", 0, 15 * Min));
                    l.Add(B(archetype, "era_settlement", "Settlement", 0.3, 2));
                    l.Add(B(archetype, "era_industry", "Industry", 2, 10));
                    l.Add(B(archetype, "era_interstellar", "Interstellar", 14, 50));
                    l.Add(B(archetype, "era_ascension", "Ascension", 40, 120));
                    break;
            }
            return l;
        }

        /// <summary>
        /// Longest stretch of active play (minutes) without visible progress: a goal done, a megaproject installment or a first landing on a new world, by the era
        /// the player is in. A longer stretch is a dead zone. The Industry limit leaves headroom over the 240 k antimatter research (about 7 hours for the heaviest player).
        /// </summary>
        public static double MaxGoalMinutes(int era)
        {
            switch (era)
            {
                case 0: return 40;
                case 1: return 60;
                case 2: return 150;
                case 3: return 450;
                case 4: return 1500;
                default: return 3600;
            }
        }

        /// <summary>The times that fall outside their band (empty = all good).</summary>
        public static List<string> Check(SimResult r)
        {
            var bad = new List<string>();
            foreach (SimBand b in For(r.archetype))
            {
                double d = r.Days(b.milestone);
                if (double.IsNaN(d)) { bad.Add(r.archetype + ": " + b.label + " never reached (band " + F(b.minDays) + " to " + F(b.maxDays) + " days)"); continue; }
                if (d < b.minDays - 1e-9 || d > b.maxDays + 1e-9) bad.Add(r.archetype + ": " + b.label + " at " + F(d) + " days, band " + F(b.minDays) + " to " + F(b.maxDays));
            }
            return bad;
        }

        /// <summary>Dead zones: stretches of active play with no goal completed, no megaproject installment contributed and no first landing on a new world, longer than the era allows.</summary>
        public static List<string> CheckGoalGaps(SimResult r)
        {
            var bad = new List<string>();
            double prev = 0.0;
            int p = 0;
            foreach (SimGoalTime g in r.goals)
            {
                // every installment before this completion is visible progress, so each gap between visible events is checked
                while (p < r.progressPlay.Count && r.progressPlay[p] <= g.playSeconds + 1e-9)
                {
                    CheckGap(r, bad, g, prev, r.progressPlay[p]);
                    prev = Math.Max(prev, r.progressPlay[p]);
                    p++;
                }
                CheckGap(r, bad, g, prev, g.playSeconds);
                prev = Math.Max(prev, g.playSeconds);
            }
            return bad;
        }

        /// <summary>The longest stretch of active play (minutes) without a goal completion or megaproject installment, per era.</summary>
        public static double[] MaxGapMinutesByEra(SimResult r)
        {
            var max = new double[6];
            double prev = 0.0;
            int p = 0;
            foreach (SimGoalTime g in r.goals)
            {
                int era = EraAt(r, g.seconds);
                while (p < r.progressPlay.Count && r.progressPlay[p] <= g.playSeconds + 1e-9)
                {
                    max[era] = Math.Max(max[era], (r.progressPlay[p] - prev) / 60.0);
                    prev = Math.Max(prev, r.progressPlay[p]);
                    p++;
                }
                max[era] = Math.Max(max[era], (g.playSeconds - prev) / 60.0);
                prev = Math.Max(prev, g.playSeconds);
            }
            return max;
        }

        static void CheckGap(SimResult r, List<string> bad, SimGoalTime g, double fromPlay, double toPlay)
        {
            double minutes = (toPlay - fromPlay) / 60.0;
            int era = EraAt(r, g.seconds);
            if (minutes > MaxGoalMinutes(era)) bad.Add(r.archetype + ": '" + g.text + "' needed " + minutes.ToString("0") + " active minutes without visible progress in the " + EraNames[era] + " era (limit " + MaxGoalMinutes(era).ToString("0") + ")");
        }

        public static int EraAt(SimResult r, double seconds)
        {
            int era = 0;
            for (int i = 1; i < EraMilestones.Length; i++)
            {
                double s;
                if (r.milestones.TryGetValue(EraMilestones[i], out s) && s <= seconds + 1e-9) era = i;
            }
            return era;
        }

        static string F(double d)
        {
            if (d < 1.0 / 24.0) return (d * 1440.0).ToString("0.#", CultureInfo.InvariantCulture) + " min";
            return d.ToString("0.#", CultureInfo.InvariantCulture);
        }

        public static string Table(SimResult r)
        {
            var sb = new StringBuilder();
            foreach (SimBand b in For(r.archetype))
            {
                double d = r.Days(b.milestone);
                bool ok = !double.IsNaN(d) && d >= b.minDays - 1e-9 && d <= b.maxDays + 1e-9;
                sb.Append("  ").Append(b.label.PadRight(16)).Append((double.IsNaN(d) ? "never" : F(d) + (d < 1.0 / 24.0 ? "" : " d")).PadLeft(12)).Append("   band ").Append(F(b.minDays)).Append(" to ").Append(F(b.maxDays)).Append(b.maxDays < 1.0 / 24.0 ? "" : " d").Append(ok ? "   ok" : "   OUT").AppendLine();
            }
            return sb.ToString();
        }
    }
}
