using System;
using System.Collections.Generic;
using Frontier.Universe;

namespace Frontier.Economy
{
    /// <summary>Deterministic helpers for turning value budgets into tidy integer costs.</summary>
    public static class CostMath
    {
        /// <summary>
        /// Rounds a real amount to a player-friendly integer: exact below 25, multiples of 5 up to 100, two significant digits above.
        /// Monotone (a larger input never gives a smaller output), so growing budgets give non-decreasing costs.
        /// </summary>
        public static int NiceAmount(double v)
        {
            if (!(v >= 0.5)) return 0;
            if (v < 25.0) return (int)Math.Floor(v + 0.5);
            if (v < 100.0) return 5 * (int)Math.Floor(v / 5.0 + 0.5);
            double scale = 1.0;
            while (v >= scale * 100.0) scale *= 10.0;
            double m = Math.Floor(v / scale + 0.5);
            return (int)(m * scale);
        }

        /// <summary>Rounds to the nearest integer, half away from zero.</summary>
        public static int RoundInt(double v)
        {
            return (int)(v < 0.0 ? Math.Ceiling(v - 0.5) : Math.Floor(v + 0.5));
        }

        /// <summary>Rounds to a number of decimals (for tidy JSON); the result is then stored once as float.</summary>
        public static double Round(double v, int decimals)
        {
            double k = 1.0;
            for (int i = 0; i < decimals; i++) k *= 10.0;
            return Math.Floor(v * k + 0.5) / k;
        }

        public static float F(double v, int decimals = 4) { return (float)Round(v, decimals); }

        /// <summary>Rounds a duration to a display-friendly number of seconds (5 min steps above an hour, 30 s above 10 min, ...).</summary>
        public static int NiceSeconds(double s)
        {
            if (s <= 0.0) return 0;
            double step = s >= 3600.0 ? 300.0 : (s >= 600.0 ? 30.0 : (s >= 60.0 ? 5.0 : 1.0));
            double r = Math.Floor(s / step + 0.5) * step;
            return r < 1.0 ? 1 : (int)r;
        }

        public static ItemAmount[] Items(params string[] idAmountPairs)
        {
            var list = new ItemAmount[idAmountPairs.Length / 2];
            for (int i = 0; i < list.Length; i++) list[i] = new ItemAmount(idAmountPairs[2 * i], int.Parse(idAmountPairs[2 * i + 1], System.Globalization.CultureInfo.InvariantCulture));
            return list;
        }
    }

    /// <summary>
    /// A cost recipe written as "scrap:1@1, wood:0.4@2, planks:0.5@4": resource, weight, first level it appears at. At a level the active
    /// weights are normalised and the level's value budget is split by them, so the cost keeps its composition and never shrinks (a new
    /// part must stay below about a third of the active weight for a growth of 1.5 per level; the catalog tests enforce monotone costs).
    /// </summary>
    public sealed class Bundle
    {
        public readonly string[] ids;
        public readonly double[] weights;
        public readonly int[] fromLevel;

        Bundle(string[] ids, double[] weights, int[] fromLevel)
        {
            this.ids = ids;
            this.weights = weights;
            this.fromLevel = fromLevel;
        }

        public static Bundle Parse(string spec)
        {
            string[] parts = spec.Split(',');
            var ids = new List<string>(parts.Length);
            var w = new List<double>(parts.Length);
            var f = new List<int>(parts.Length);
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i].Trim();
                int at = p.IndexOf('@');
                int level = 1;
                if (at >= 0)
                {
                    level = int.Parse(p.Substring(at + 1), System.Globalization.CultureInfo.InvariantCulture);
                    p = p.Substring(0, at);
                }
                int colon = p.IndexOf(':');
                double weight = 1.0;
                if (colon >= 0)
                {
                    weight = double.Parse(p.Substring(colon + 1), System.Globalization.CultureInfo.InvariantCulture);
                    p = p.Substring(0, colon);
                }
                string id = p.Trim();
                int existing = ids.IndexOf(id);
                if (existing >= 0)
                {
                    w[existing] += weight;
                    if (level < f[existing]) f[existing] = level;
                    continue;
                }
                ids.Add(id);
                w.Add(weight);
                f.Add(level);
            }
            return new Bundle(ids.ToArray(), w.ToArray(), f.ToArray());
        }

        /// <summary>
        /// Resource amounts for a value budget at a level, using the given price list. Parts whose rounded amount is 0 are skipped.
        /// When <paramref name="capSlots"/> is positive no single line may exceed that many backpack/warehouse slots of its resource (so a
        /// purchase never asks for more than a base can hold); the value a capped line cannot take is passed to the lines that still have room.
        /// </summary>
        public ItemAmount[] Cost(double valueBudget, int level, IDictionary<string, double> price, IDictionary<string, int> stackSize = null, double capSlots = 0.0)
        {
            int n = ids.Length;
            var active = new bool[n];
            var units = new double[n];
            var capped = new bool[n];
            var cap = new double[n];
            double[] unitPrice = new double[n];
            for (int i = 0; i < n; i++)
            {
                active[i] = fromLevel[i] <= level;
                if (!active[i]) continue;
                double v;
                if (!price.TryGetValue(ids[i], out v) || !(v > 0.0)) throw new InvalidOperationException("bundle references resource without a value: " + ids[i]);
                unitPrice[i] = v;
                int stack;
                cap[i] = capSlots > 0.0 && stackSize != null && stackSize.TryGetValue(ids[i], out stack) ? capSlots * stack : double.MaxValue;
            }
            double remaining = valueBudget;
            for (int round = 0; round <= n; round++)
            {
                double totalW = 0.0;
                for (int i = 0; i < n; i++) if (active[i] && !capped[i]) totalW += weights[i];
                if (totalW <= 0.0) break;
                bool clipped = false;
                for (int i = 0; i < n; i++)
                {
                    if (!active[i] || capped[i]) continue;
                    double u = remaining * (weights[i] / totalW) / unitPrice[i];
                    if (u > cap[i])
                    {
                        capped[i] = true;
                        units[i] = cap[i];
                        remaining -= cap[i] * unitPrice[i];
                        clipped = true;
                    }
                }
                if (clipped) continue;
                for (int i = 0; i < n; i++) if (active[i] && !capped[i]) units[i] = remaining * (weights[i] / totalW) / unitPrice[i];
                break;
            }
            var list = new List<ItemAmount>(n);
            for (int i = 0; i < n; i++)
            {
                if (!active[i]) continue;
                int amount = CostMath.NiceAmount(units[i]);
                if (amount > cap[i]) amount = (int)cap[i];
                if (amount > 0) list.Add(new ItemAmount(ids[i], amount));
            }
            if (list.Count == 0 && valueBudget >= 1.0) list.Add(new ItemAmount(ids[0], 1));
            return list.ToArray();
        }
    }
}
