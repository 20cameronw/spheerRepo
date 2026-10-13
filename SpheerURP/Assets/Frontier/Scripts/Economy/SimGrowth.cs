using System;
using System.Collections.Generic;
using Frontier.Universe;

namespace Frontier.Economy
{
    /// <summary>
    /// Return-on-investment purchases of the average player. Beside the headline ladder a player keeps upgrading what they actually use:
    /// the tool of the method they harvest most, the camps of the worlds they farm, the station that eats their crafting taps. An upgrade is
    /// bought (when it is affordable right now and a builder is free) if it pays itself back within a few hours of play.
    /// </summary>
    public sealed partial class SimEngine
    {
        const double EmaTau = 72000.0;

        double emaPlay, emaValue;
        double[] emaLadder;
        double[] emaTap;
        double nextGrowthCheck;

        public static void RegisterGrowthKeys(Tuning t)
        {
            t.Register("sim", "sim.growth_payback_hours", 10, 0, 1000, "A player buys an income upgrade that pays itself back within this many hours of play (0 turns the habit off).");
            t.Register("sim", "sim.growth_urgent_hours", 3, 0, 1000, "An upgrade that pays back within this many hours of play is worked toward at once, like a headline goal.");
            t.Register("sim", "sim.growth_check_seconds", 60, 5, 3600, "Seconds of play between two looks at the growth options.");
        }

        void NoteHarvest(int planet, HarvestMethod m, double value)
        {
            if (emaLadder == null) { emaLadder = new double[cat.tools.Length]; emaTap = new double[cat.structures.Length]; }
            emaValue += value;
            emaLadder[LadderOf(m)] += value;
            Inst s = BestServer(planet, m);
            if (s != null) s.use += value;
        }

        void NoteCraft(string stationId, double seconds)
        {
            if (emaLadder == null) { emaLadder = new double[cat.tools.Length]; emaTap = new double[cat.structures.Length]; }
            emaTap[SI(stationId)] += seconds;
        }

        void Decay(double playSeconds)
        {
            if (emaLadder == null) return;
            double k = 1.0 - playSeconds / EmaTau;
            if (k < 0.0) k = 0.0;
            emaPlay = emaPlay * k + playSeconds;
            emaValue *= k;
            for (int i = 0; i < emaLadder.Length; i++) emaLadder[i] *= k;
            for (int i = 0; i < emaTap.Length; i++) emaTap[i] *= k;
            for (int i = 0; i < insts.Count; i++) insts[i].use *= k;
        }

        Inst BestServer(int planet, HarvestMethod m)
        {
            Inst best = null;
            double bestE = -1.0;
            List<int> list = servers[(int)m];
            for (int k = 0; k < list.Count; k++)
                for (int i = 0; i < insts.Count; i++)
                {
                    Inst n = insts[i];
                    if (n.planet != planet || n.str != list[k] || n.level <= 0) continue;
                    double e = EffectOf(n.str, n.level);
                    if (e > bestE) { bestE = e; best = n; }
                }
            return best;
        }

        Goal growthGoal;
        bool growthUrgent;

        /// <summary>
        /// Looks at the upgrade options once in a while and remembers the one with the shortest payback. A purchase that pays back within the
        /// urgent window is worked toward like a ladder goal; any other good one is bought when it happens to be affordable.
        /// </summary>
        void RefreshGrowth()
        {
            double limitHours = tun.D("sim.growth_payback_hours");
            if (limitHours <= 0.0 || emaLadder == null || emaPlay < 300.0 || emaValue <= 0.0) { growthGoal = null; return; }
            if (emaPlay < nextGrowthCheck) return;
            nextGrowthCheck = emaPlay + tun.D("sim.growth_check_seconds");
            double income = emaValue / emaPlay;
            double limit = limitHours * 3600.0;
            Goal best = null;
            double bestPayback = limit;

            for (int l = 0; l < cat.tools.Length; l++)
            {
                int tier = toolTier[l];
                if (tier >= cat.tools[l].tiers.Length || emaLadder[l] <= 0.0) continue;
                ToolTierDef cur = cat.tools[l].tiers[tier - 1], nxt = cat.tools[l].tiers[tier];
                double gain = nxt.yieldMult / cur.yieldMult - 1.0;
                double benefit = gain / (1.0 + gain) * (emaLadder[l] / emaValue);
                ConsiderGrowth(ref best, ref bestPayback, new Goal { kind = GoalKind.Tool, idx = l, level = tier + 1, text = "growth" }, cat.Prices.Value(nxt.cost), benefit, income);
            }
            for (int i = 0; i < insts.Count; i++)
            {
                Inst n = insts[i];
                StructureDef def = cat.structures[n.str];
                if (n.level <= 0 || n.pending > 0 || n.level >= def.maxLevel) continue;
                if (def.role == StructureRole.Gather && n.use > 0.0)
                {
                    double e0 = EffectOf(n.str, n.level), e1 = EffectOf(n.str, n.level + 1), g = GlobalYieldBonus();
                    double gain = (1.0 + e1 + g) / (1.0 + e0 + g) - 1.0;
                    double benefit = gain / (1.0 + gain) * (n.use / emaValue);
                    ConsiderGrowth(ref best, ref bestPayback, Goal.Build(n.str, n.level + 1, n.planet, n.no), cat.Prices.Value(def.levels[n.level].cost), benefit, income);
                }
                else if (def.role == StructureRole.Process && n.planet == homePlanet && emaTap[n.str] > 0.0)
                {
                    double b0 = def.levels[n.level - 1].effect, b1 = def.levels[n.level].effect;
                    double saved = b1 > b0 ? 1.0 - b0 / b1 : 0.0;
                    double benefit = emaTap[n.str] / emaPlay * saved;
                    ConsiderGrowth(ref best, ref bestPayback, Goal.Build(n.str, n.level + 1, n.planet, n.no), cat.Prices.Value(def.levels[n.level].cost), benefit, income);
                }
            }
            growthGoal = best;
            growthUrgent = best != null && bestPayback <= tun.D("sim.growth_urgent_hours") * 3600.0;
        }

        /// <summary>Buys or works toward the growth goal; returns the seconds spent (0 when there is nothing to do).</summary>
        double GrowthStep(double left)
        {
            RefreshGrowth();
            if (growthGoal == null) return 0.0;
            Goal g = growthGoal;
            Eval e = Evaluate(g);
            if (e.kind == EvalKind.Buy)
            {
                bool same = e.leaf.kind == g.kind && e.leaf.idx == g.idx && e.leaf.level == g.level && e.leaf.planet == g.planet && e.leaf.no == g.no;
                if (!same && !growthUrgent) return 0.0;
                Log("growth " + Describe(e.leaf));
                growthGoal = null;
                nextGrowthCheck = 0.0;
                return ExecuteBuy(e, left);
            }
            if (e.kind == EvalKind.Work && growthUrgent) return ExecuteWork(e, left);
            return 0.0;
        }

        void ConsiderGrowth(ref Goal best, ref double bestPayback, Goal goal, double costValue, double benefit, double income)
        {
            if (!(benefit > 1e-6)) return;
            double payback = costValue / income / benefit;
            if (payback < bestPayback) { bestPayback = payback; best = goal; }
        }
    }
}
