using System;
using System.Collections.Generic;
using Frontier.Universe;

namespace Frontier.Economy
{
    sealed class Order
    {
        public RecipeDef rec;
        public long batches;
    }

    enum EvalKind { Done, Wait, Buy, Work, Stuck }

    enum ActKind { None, Craft, HarvestHome, Trip }

    enum GateKind { Done, Wait, Blocker, Ok }

    /// <summary>The resolved plan for one ladder goal: what the next action is (buy the leaf, craft, harvest, travel, or wait).</summary>
    sealed class Eval
    {
        public EvalKind kind;
        public Goal top;
        public Goal leaf;
        public ItemAmount[] cost = new ItemAmount[0];
        public long[] rawShort;
        public readonly List<Order> orders = new List<Order>();
        public double waitUntil;
        public ActKind act;
        public Order order;
        public int resource = -1;
        public int dest = -1;
        public string why;
        /// <summary>Everything the top goal and the blockers resolved on the way need (their whole recipe trees): never thrown away as junk.</summary>
        public bool[] chainKeep;
    }

    struct Src
    {
        public bool ok;
        public int planet, dep, needStr;
        public double sec, yieldRun;
    }

    struct TripInfo
    {
        public bool ok;
        public int energy;
        public double seconds;
    }

    public sealed partial class SimEngine
    {
        Src[] srcCache;
        int[] srcVer;
        double[] usCache;
        int[] usVer;
        double hrVal;
        int hrVer;

        // ------------------------------------------------------------------------------------------------ evaluation of one goal
        bool homeOnly;

        Eval Evaluate(Goal top, bool traced = false)
        {
            homeOnly = false;
            Eval e = EvaluateCore(top, traced);
            homeOnly = false;
            return e;
        }

        Eval EvaluateCore(Goal top, bool traced)
        {
            var e = new Eval { top = top, rawShort = new long[nRes], chainKeep = new bool[nRes] };
            AddChain(e, TopCost(top));
            Goal leaf = top;
            var trace = cfg.keepLog || traced ? new List<string>() : null;
            for (int iter = 0; iter < 48; iter++)
            {
                if (trace != null) trace.Add(Describe(leaf));
                Goal next;
                GateKind gk = CheckGates(leaf, e, out next);
                if (gk == GateKind.Done)
                {
                    if (ReferenceEquals(leaf, top)) { e.kind = EvalKind.Done; return e; }
                    leaf = top;
                    homeOnly = false;
                    continue;
                }
                if (gk == GateKind.Wait) { e.kind = EvalKind.Wait; return e; }
                if (gk == GateKind.Blocker) { leaf = next; homeOnly = false; continue; }
                leaf = next;
                homeOnly = leaf.kind == GoalKind.Stock && leaf.idx == rFuel && !ReferenceEquals(leaf, top);
                e.leaf = leaf;
                e.cost = CostOf(leaf);
                AddChain(e, e.cost);
                bool travelGoal = leaf.kind == GoalKind.Explore || leaf.kind == GoalKind.ExploreTo;
                Goal wb = travelGoal ? null : ExpandNeeds(e, 1.0);
                if (wb != null) { leaf = wb; continue; }
                if (!travelGoal && e.orders.Count == 0 && NoShortage(e))
                {
                    wb = BuyBlocker(e);
                    if (wb != null) { leaf = wb; continue; }
                    e.kind = EvalKind.Buy;
                    return e;
                }
                if (!travelGoal)
                {
                    double slots = PlanSlots(e), budget = TrancheBudget();
                    if (slots > budget)
                    {
                        wb = ExpandNeeds(e, Math.Max(0.02, budget / slots));
                        if (wb != null) { leaf = wb; continue; }
                    }
                }
                wb = PlanWork(e);
                if (wb != null) { leaf = wb; continue; }
                if (e.act == ActKind.None) { e.kind = EvalKind.Stuck; e.why = "no action for " + leaf.kind + " " + leaf.text; return e; }
                e.kind = EvalKind.Work;
                return e;
            }
            e.kind = EvalKind.Stuck;
            e.why = "gate loop at " + top.text + " :: " + (trace == null ? "" : string.Join(" > ", trace.GetRange(Math.Max(0, trace.Count - 14), Math.Min(14, trace.Count))));
            return e;
        }

        string Describe(Goal g)
        {
            switch (g.kind)
            {
                case GoalKind.Build: return "build:" + cat.structures[g.idx].id + ":" + g.level + (g.planet != homePlanet ? "@" + world.planets[g.planet].name : "") + (g.no > 1 ? "#" + g.no : "");
                case GoalKind.Tool: return "tool:" + cat.tools[g.idx].id + ":" + g.level;
                case GoalKind.Upgrade: return "upg:" + cat.upgrades[g.idx].id + ":" + g.level;
                case GoalKind.Research: return "research:" + cat.research[g.idx].id;
                case GoalKind.Explore: return "explore:" + g.level;
                case GoalKind.ExploreTo: return "exploreTo:" + world.planets[g.planet].name;
                case GoalKind.Mega: return "mega:" + cat.structures[megaStr[g.idx]].id + ":" + g.level;
                case GoalKind.Wave: return "wave:" + g.level;
                case GoalKind.Stock: return "stock:" + cat.resources[g.idx].id + ":" + g.amount;
                default: return g.kind.ToString();
            }
        }

        static bool NoShortage(Eval e)
        {
            for (int i = 0; i < e.rawShort.Length; i++) if (e.rawShort[i] > 0) return false;
            return true;
        }

        void AddChain(Eval e, ItemAmount[] lines)
        {
            for (int i = 0; i < lines.Length; i++) e.chainKeep[RI(lines[i].resourceId)] = true;
        }

        double NextJobEnd()
        {
            double t = double.MaxValue;
            for (int i = 0; i < insts.Count; i++) if (insts[i].pending > 0 && insts[i].jobEnd < t) t = insts[i].jobEnd;
            for (int i = 0; i < 3; i++) if (mega[i].building && mega[i].jobEnd < t) t = mega[i].jobEnd;
            return t;
        }

        // ------------------------------------------------------------------------------------------------ gates
        GateKind CheckGates(Goal g, Eval e, out Goal next)
        {
            next = null;
            switch (g.kind)
            {
                case GoalKind.Build:
                {
                    Inst inst = FindInst(g.planet, g.idx, g.no);
                    int cur = inst == null ? 0 : inst.level;
                    if (cur >= g.level) return GateKind.Done;
                    if (inst != null && inst.pending > 0) { e.waitUntil = inst.jobEnd; return GateKind.Wait; }
                    int n = cur + 1;
                    StructureDef def = cat.structures[g.idx];
                    StructureLevelDef ld = def.levels[n - 1];
                    if (n == 1 && def.unlockResearch != null && !ResearchDone(def.unlockResearch)) { next = Goal.Research(RsI(def.unlockResearch)); return GateKind.Blocker; }
                    if (ld.requiresReachTier > maxTierLanded) { next = Goal.Explore(ld.requiresReachTier); return GateKind.Blocker; }
                    if (ld.buildSeconds >= tInstant && BusyBuilders() >= Builders()) { e.waitUntil = NextJobEnd(); return GateKind.Wait; }
                    next = Goal.Build(g.idx, n, g.planet, g.no);
                    return GateKind.Ok;
                }
                case GoalKind.Tool:
                {
                    int cur = toolTier[g.idx];
                    if (cur >= g.level) return GateKind.Done;
                    int n = cur + 1;
                    ToolTierDef td = cat.tools[g.idx].tiers[n - 1];
                    if (td.researchPrereq != null && !ResearchDone(td.researchPrereq)) { next = Goal.Research(RsI(td.researchPrereq)); return GateKind.Blocker; }
                    if (td.requiresReachTier > maxTierLanded) { next = Goal.Explore(td.requiresReachTier); return GateKind.Blocker; }
                    if (td.requiresLabLevel > LabLevel) { next = Goal.Build(sLab, td.requiresLabLevel, homePlanet, 1); return GateKind.Blocker; }
                    next = new Goal { kind = GoalKind.Tool, idx = g.idx, level = n, text = g.text };
                    return GateKind.Ok;
                }
                case GoalKind.Upgrade:
                {
                    int cur = upgLevel[g.idx];
                    if (cur >= g.level) return GateKind.Done;
                    int n = cur + 1;
                    UpgradeLevelDef ul = cat.upgrades[g.idx].levels[n - 1];
                    if (ul.researchPrereq != null && !ResearchDone(ul.researchPrereq)) { next = Goal.Research(RsI(ul.researchPrereq)); return GateKind.Blocker; }
                    if (ul.requiresReachTier > maxTierLanded) { next = Goal.Explore(ul.requiresReachTier); return GateKind.Blocker; }
                    next = Goal.Upgrade(g.idx, n);
                    return GateKind.Ok;
                }
                case GoalKind.Research:
                {
                    if (researched[g.idx]) return GateKind.Done;
                    ResearchNodeDef node = cat.research[g.idx];
                    for (int i = 0; i < node.prereqs.Length; i++)
                        if (!ResearchDone(node.prereqs[i])) { next = Goal.Research(RsI(node.prereqs[i])); return GateKind.Blocker; }
                    if (LabLevel < node.labLevel) { next = Goal.Build(sLab, node.labLevel, homePlanet, 1); return GateKind.Blocker; }
                    next = g;
                    return GateKind.Ok;
                }
                case GoalKind.Explore:
                    if (maxTierLanded >= g.level) return GateKind.Done;
                    next = g;
                    return GateKind.Ok;
                case GoalKind.ExploreTo:
                    if (landed[g.planet]) return GateKind.Done;
                    next = g;
                    return GateKind.Ok;
                case GoalKind.Mega:
                {
                    MegaState m = mega[g.idx];
                    if (m.stage >= g.level) return GateKind.Done;
                    StructureDef def = cat.structures[megaStr[g.idx]];
                    if (m.building)
                    {
                        // while a stage builds, the materials of the next one may already be delivered
                        if (g.level >= m.stage + 2 && m.stage + 2 <= def.maxLevel && def.levels[m.stage + 1].requiresReachTier <= maxTierLanded)
                        {
                            next = new Goal { kind = GoalKind.Mega, idx = g.idx, level = m.stage + 2, amount = 1, text = g.text };
                            return GateKind.Ok;
                        }
                        e.waitUntil = m.jobEnd;
                        return GateKind.Wait;
                    }
                    StructureLevelDef ld = def.levels[m.stage];
                    if (m.stage == 0 && def.unlockResearch != null && !ResearchDone(def.unlockResearch)) { next = Goal.Research(RsI(def.unlockResearch)); return GateKind.Blocker; }
                    if (ld.requiresReachTier > maxTierLanded) { next = Goal.Explore(ld.requiresReachTier); return GateKind.Blocker; }
                    next = new Goal { kind = GoalKind.Mega, idx = g.idx, level = m.stage + 1, text = g.text };
                    return GateKind.Ok;
                }
                case GoalKind.Wave:
                {
                    if (wavesCalled >= g.level) return GateKind.Done;
                    if (BuiltLevel(homePlanet, sAlarm, 1) < 1) { next = Goal.Build(sAlarm, 1, homePlanet, 1); return GateKind.Blocker; }
                    Goal d = DefenseBlocker();
                    if (d != null) { next = d; return GateKind.Blocker; }
                    next = g;
                    return GateKind.Ok;
                }
                case GoalKind.Stock:
                    if (PoolHave(g.idx) >= g.amount) return GateKind.Done;
                    next = g;
                    return GateKind.Ok;
                default:
                    return GateKind.Done;
            }
        }

        // ------------------------------------------------------------------------------------------------ costs and needs
        ItemAmount[] CostOf(Goal leaf)
        {
            switch (leaf.kind)
            {
                case GoalKind.Build: return cat.structures[leaf.idx].levels[leaf.level - 1].cost;
                case GoalKind.Tool: return cat.tools[leaf.idx].tiers[leaf.level - 1].cost;
                case GoalKind.Upgrade: return cat.upgrades[leaf.idx].levels[leaf.level - 1].cost;
                case GoalKind.Research: return cat.research[leaf.idx].cost;
                case GoalKind.Stock: return new[] { new ItemAmount(cat.resources[leaf.idx].id, (int)Math.Min(int.MaxValue, leaf.amount)) };
                case GoalKind.Wave: return new[] { new ItemAmount("fuel", WaveEnergy()) };
                case GoalKind.Mega:
                {
                    MegaState m = mega[leaf.idx];
                    StructureLevelDef ld = cat.structures[megaStr[leaf.idx]].levels[leaf.level - 1];
                    long[] arr = leaf.amount == 1 ? m.pre : m.filled;
                    for (int i = 0; i < ld.cost.Length; i++)
                    {
                        long remaining = ld.cost[i].amount - arr[i];
                        if (remaining <= 0) continue;
                        int r = RI(ld.cost[i].resourceId);
                        long chunk = Math.Min(remaining, ChunkCap(r));
                        return new[] { new ItemAmount(ld.cost[i].resourceId, (int)chunk) };
                    }
                    return new ItemAmount[0];
                }
                default: return new ItemAmount[0];
            }
        }

        long ChunkCap(int r)
        {
            long slots = PackSlotsCap + StoreSlotsCap;
            long c = (long)(slots * StackOf(r) * tMegaChunk);
            return Math.Max(c, 2L * StackOf(r));
        }

        int BatchSize(string stationId)
        {
            int s = SI(stationId);
            int level = HomeLevel(s);
            if (level <= 0) return 1;
            return Math.Max(1, (int)cat.structures[s].levels[level - 1].effect);
        }

        bool RecipeAvailable(RecipeDef c)
        {
            return ResearchDone(c.unlockResearch) && HomeLevel(SI(c.stationId)) >= c.stationLevel;
        }

        double RecipeSeconds(RecipeDef c)
        {
            double sum = tCraft / BatchSize(c.stationId);
            for (int i = 0; i < c.inputs.Length; i++) sum += c.inputs[i].amount * UnitSeconds(RI(c.inputs[i].resourceId));
            return sum / c.outputs[0].amount;
        }

        /// <summary>Estimated active seconds to obtain one unit of a resource with what the player has now (memoised per state version).</summary>
        double UnitSeconds(int r)
        {
            if (usCache == null) { usCache = new double[nRes]; usVer = new int[nRes]; }
            if (usVer[r] == version) return usCache[r];
            double v;
            if (raw[r])
            {
                Src s = BestSource(r);
                v = s.ok ? s.sec : 1e7;
            }
            else
            {
                v = double.MaxValue;
                List<RecipeDef> list = recipesFor[r];
                for (int k = 0; k < list.Count; k++)
                {
                    double s = RecipeSeconds(list[k]);
                    if (s < v) v = s;
                }
                if (v == double.MaxValue) v = 1e7;
            }
            usVer[r] = version;
            usCache[r] = v;
            return v;
        }

        /// <summary>Picks the recipe to make r (cheapest available); when none is available returns the cheapest gate (research or station level).</summary>
        Goal ChooseRecipe(int r, out RecipeDef rec)
        {
            rec = null;
            double best = double.MaxValue;
            List<RecipeDef> list = recipesFor[r];
            for (int k = 0; k < list.Count; k++)
            {
                if (!RecipeAvailable(list[k])) continue;
                if (homeOnly && !RecipeHomeOnly(list[k])) continue;
                double s = RecipeSeconds(list[k]);
                if (s < best) { best = s; rec = list[k]; }
            }
            if (rec != null) return null;
            Goal bestGate = null;
            double bestCost = double.MaxValue;
            for (int k = 0; k < list.Count; k++)
            {
                RecipeDef c = list[k];
                Goal gate;
                double gc;
                if (!ResearchDone(c.unlockResearch))
                {
                    int ri = RsI(c.unlockResearch);
                    gate = Goal.Research(ri);
                    gc = cat.Prices.Value(cat.research[ri].cost);
                }
                else
                {
                    int s = SI(c.stationId);
                    int lv = Math.Max(1, HomeLevel(s) + 1);
                    gate = Goal.Build(s, c.stationLevel, homePlanet, 1);
                    gc = cat.Prices.Value(cat.structures[s].levels[Math.Min(lv, cat.structures[s].maxLevel) - 1].cost);
                }
                if (gc < bestCost) { bestCost = gc; bestGate = gate; }
            }
            return bestGate;
        }

        /// <summary>Turns the cost of the leaf into raw shortfalls and an ordered list of crafts; returns a recipe gate when a needed recipe is not available yet.
        /// With a tranche share below 1 only that share of what is still missing is planned, so the bags are never asked to hold the whole chain at once.</summary>
        Goal ExpandNeeds(Eval e, double tranche)
        {
            Array.Clear(e.rawShort, 0, e.rawShort.Length);
            e.orders.Clear();
            var avail = new long[nRes];
            for (int r = 0; r < nRes; r++) avail[r] = PoolHave(r);
            for (int i = 0; i < e.cost.Length; i++)
            {
                int r = RI(e.cost[i].resourceId);
                long amount = e.cost[i].amount;
                long held = PoolHave(r);
                long missing = amount - held;
                long target = amount;
                if (tranche < 1.0 && missing > 0) target = held + (long)Math.Ceiling(missing * tranche);
                Goal b = Take(r, target, e, avail, 0);
                if (b != null) return b;
            }
            return null;
        }

        /// <summary>Slots that the whole plan would add: the raw shortfalls plus every crafted output.</summary>
        double PlanSlots(Eval e)
        {
            double s = 0.0;
            for (int r = 0; r < nRes; r++) if (e.rawShort[r] > 0) s += e.rawShort[r] / (double)StackOf(r);
            for (int k = 0; k < e.orders.Count; k++)
            {
                ItemAmount o = e.orders[k].rec.outputs[0];
                s += (double)o.amount * e.orders[k].batches / StackOf(RI(o.resourceId));
            }
            return s;
        }

        double TrancheBudget()
        {
            double cap = PackSlotsCap + StoreSlotsCap;
            double free = cap - SlotShare() * cap;
            return Math.Max(8.0, free * 0.85);
        }

        Goal Take(int r, long qty, Eval e, long[] avail, int depth)
        {
            if (depth > 12) throw new InvalidOperationException("sim: recipe depth at " + cat.resources[r].id);
            long use = Math.Min(avail[r], qty);
            avail[r] -= use;
            long shortQty = qty - use;
            if (shortQty <= 0) return null;
            if (raw[r]) { e.rawShort[r] += shortQty; return null; }
            RecipeDef rec;
            Goal blocker = ChooseRecipe(r, out rec);
            if (rec == null) return blocker;
            int outAmt = rec.outputs[0].amount;
            long batches = (shortQty + outAmt - 1) / outAmt;
            for (int i = 0; i < rec.inputs.Length; i++)
            {
                Goal b = Take(RI(rec.inputs[i].resourceId), rec.inputs[i].amount * batches, e, avail, depth + 1);
                if (b != null) return b;
            }
            e.orders.Add(new Order { rec = rec, batches = batches });
            avail[r] += outAmt * batches - shortQty;
            return null;
        }

        // ------------------------------------------------------------------------------------------------ storage
        Goal StorageBlocker()
        {
            int count = InstanceCount(homePlanet, sWarehouse);
            if (count < tMaxWarehouses) return Goal.Build(sWarehouse, 1, homePlanet, count + 1);
            int bestNo = -1, bestLevel = int.MaxValue;
            for (int i = 0; i < insts.Count; i++)
            {
                Inst n = insts[i];
                if (n.planet != homePlanet || n.str != sWarehouse) continue;
                if (n.level < bestLevel && n.level < cat.structures[sWarehouse].maxLevel) { bestLevel = n.level; bestNo = n.no; }
            }
            if (bestNo >= 0) return Goal.Build(sWarehouse, bestLevel + 1, homePlanet, bestNo);
            // every warehouse is at its top level: denser stacks are the last answer, once the player can already buy them
            UpgradeDef comp = cat.upgrades[uCompression];
            int next = upgLevel[uCompression] + 1;
            if (next > comp.maxLevel) return null;
            UpgradeLevelDef cl = comp.levels[next - 1];
            if (cl.requiresReachTier > maxTierLanded || (cl.researchPrereq != null && !ResearchDone(cl.researchPrereq))) return null;
            return Goal.Upgrade(uCompression, next);
        }

        /// <summary>The goals that are themselves the answer to a full store (they must not wait for more room).</summary>
        bool IsStorageGoal(Goal g)
        {
            return (g.kind == GoalKind.Build && g.idx == sWarehouse) || (g.kind == GoalKind.Upgrade && g.idx == uCompression);
        }

        /// <summary>A purchase whose line items cannot all fit in the pack and storage at once needs more room first.</summary>
        Goal CapacityBlocker(ItemAmount[] cost)
        {
            long demand = 0;
            for (int i = 0; i < cost.Length; i++) demand += SlotsFor(cost[i].amount, StackOf(RI(cost[i].resourceId)));
            long total = PackSlotsCap + StoreSlotsCap + TankSlots + DepotEnergySlots;
            if (demand <= tStorageTrigger * total) return null;
            Goal g = StorageBlocker();
            return g;
        }

        // ------------------------------------------------------------------------------------------------ sources
        double HomeValueRate()
        {
            if (hrVer == version) return hrVal;
            double best = 0.01;
            SimPlanet h = world.planets[homePlanet];
            double q = MeanQualityFactor;
            for (int d = 0; d < h.depRes.Length; d++)
            {
                if (h.depRes[d] < 0) continue;
                HarvestMethod m = h.depMethod[d];
                if (AccessBonus(homePlanet, m) < 0.0) continue;
                double rate = DepositYieldPerRun(h, d, q) * val[h.depRes[d]] / RunSeconds(m);
                if (rate > best) best = rate;
            }
            hrVal = best;
            hrVer = version;
            return best;
        }

        double FuelUnitSeconds() { return val[rFuel] / HomeValueRate() * 1.3; }

        /// <summary>Players with short sessions fly on autopilot once it is researched: no mini-game and no refund, but flights take seconds.</summary>
        bool UseAutopilot { get { return arch.minutesPerSession <= 6.0 && researched[rsrIx["nav_autopilot"]]; } }

        TripInfo TripCost(int planet)
        {
            var t = new TripInfo();
            if (planet == homePlanet) { t.ok = true; return t; }
            SimPlanet sp = world.planets[planet], h = world.planets[homePlanet];
            int engine = upgLevel[uEngine];
            double disc = TravelDiscount();
            bool auto = UseAutopilot;
            if (sp.system == homeSystem)
            {
                t.energy = travel.IntraSystemFuelCost(Math.Abs(sp.orbit - h.orbit), engine, disc, auto);
                t.seconds = auto ? travel.AutopilotLegSeconds : tIntra;
                t.ok = true;
                return t;
            }
            if (JumpLevel <= 0) return t;
            RouteTable rt = Routes(JumpLevel);
            if (!rt.reachable[sp.system]) return t;
            t.energy = travel.RouteFuelCost(rt.baseFuel[sp.system], engine, 0.5, disc, auto);
            t.seconds = auto ? rt.legs[sp.system] * travel.AutopilotLegSeconds : rt.secSum[sp.system];
            t.ok = true;
            return t;
        }

        int CheapestServer(HarvestMethod m)
        {
            List<int> list = servers[(int)m];
            int best = -1;
            double bestScore = double.MaxValue;
            for (int k = 0; k < list.Count; k++)
            {
                StructureDef sd = cat.structures[list[k]];
                double score = cat.Prices.Value(sd.levels[0].cost);
                if (sd.unlockResearch != null && !ResearchDone(sd.unlockResearch)) score += 400.0 + 100.0 * sd.tier;
                if (sd.levels[0].requiresReachTier > maxTierLanded) score += 800.0;
                if (score < bestScore) { bestScore = score; best = list[k]; }
            }
            return best;
        }

        /// <summary>One candidate place to harvest r: seconds per unit including trips, fuel and a missing gather structure (ok = false when unusable).</summary>
        Src Candidate(SimPlanet sp, int r, double q, double cap, double secPerValue, double fuelSec)
        {
            var c = new Src { sec = double.MaxValue };
            if (!IsKnown(sp) || !IsReachable(sp) || !CanLand(sp)) return c;
            int d = sp.DepositOf(r);
            if (d < 0) return c;
            HarvestMethod m = sp.depMethod[d];
            double bonus = AccessBonus(sp.index, m);
            int needStr = -1;
            double setup = 0.0;
            if (bonus < 0.0)
            {
                needStr = CheapestServer(m);
                if (needStr < 0) return c;
                StructureDef sd = cat.structures[needStr];
                setup = cat.Prices.Value(sd.levels[0].cost) * secPerValue;
                if (sd.unlockResearch != null && !ResearchDone(sd.unlockResearch)) setup += 1800.0 + 600.0 * sd.tier;
                if (sd.levels[0].requiresReachTier > maxTierLanded) setup += 3600.0;
                bonus = 0.0;
            }
            double yRun = sp.depBaseYield[d] * (double)sp.depRichness[d] * ToolMult(m) * (1.0 + bonus + GlobalYieldBonus()) * q;
            if (yRun < 0.5) yRun = 0.5;
            double runSec = RunSeconds(m);
            double sec;
            if (sp.index == homePlanet)
            {
                sec = (runSec + tDeposit / Math.Max(1.0, cap / yRun)) / yRun + setup / cap;
            }
            else
            {
                TripInfo t = TripCost(sp.index);
                double refund = UseAutopilot ? 1.0 : 1.0 - travel.Refund(1000, arch.quality) / 1000.0;
                double payload = Math.Max(yRun, cap);
                double runs = Math.Ceiling(payload / yRun);
                double total = runs * runSec + 2.0 * (t.seconds + tLand) + tDeposit + 2.0 * t.energy * refund * fuelSec + setup;
                sec = total / payload;
            }
            c.ok = true;
            c.sec = sec;
            c.planet = sp.index;
            c.dep = d;
            c.needStr = needStr;
            c.yieldRun = yRun;
            return c;
        }

        /// <summary>The cheapest known, reachable, landable place to harvest a raw resource.</summary>
        Src BestSource(int r)
        {
            if (srcCache == null) { srcCache = new Src[nRes]; srcVer = new int[nRes]; }
            if (srcVer[r] == version) return srcCache[r];
            var best = new Src { sec = double.MaxValue };
            double q = MeanQualityFactor;
            int stack = StackOf(r);
            double cap = Math.Max(stack, PackSlotsCap * (double)stack * 0.9);
            double secPerValue = 1.0 / HomeValueRate();
            double fuelSec = FuelUnitSeconds();
            List<int> list = planetsWith[r];
            for (int i = 0; i < list.Count; i++)
            {
                Src c = Candidate(world.planets[list[i]], r, q, cap, secPerValue, fuelSec);
                if (c.ok && c.sec < best.sec) best = c;
            }
            srcVer[r] = version;
            srcCache[r] = best;
            return best;
        }

        /// <summary>The home world as the only source (used while making the fuel that every trip needs).</summary>
        Src HomeSource(int r)
        {
            int stack = StackOf(r);
            double cap = Math.Max(stack, PackSlotsCap * (double)stack * 0.9);
            return Candidate(world.planets[homePlanet], r, MeanQualityFactor, cap, 1.0 / HomeValueRate(), 0.0);
        }

        bool HomeRaw(int r) { return raw[r] && world.planets[homePlanet].DepositOf(r) >= 0; }

        bool RecipeHomeOnly(RecipeDef c)
        {
            for (int i = 0; i < c.inputs.Length; i++) if (!HomeRaw(RI(c.inputs[i].resourceId))) return false;
            return true;
        }

        // ------------------------------------------------------------------------------------------------ work planning
        /// <summary>Chooses the next action for a leaf that is not affordable yet. Returns a blocker goal to resolve first, or null with e.act set.</summary>
        Goal PlanWork(Eval e)
        {
            Goal leaf = e.leaf;
            e.act = ActKind.None;
            if (leaf.kind == GoalKind.Explore || leaf.kind == GoalKind.ExploreTo)
            {
                int dest;
                Goal b = leaf.kind == GoalKind.Explore ? ExploreTarget(leaf.level, out dest) : ExploreToTarget(leaf.planet, out dest);
                if (b != null) return b;
                if (dest < 0) return null;
                Goal f = FuelFor(dest);
                if (f != null) return f;
                e.act = ActKind.Trip;
                e.dest = dest;
                return null;
            }
            Goal cap = CapacityBlocker(e.cost);
            if (cap != null && !IsStorageGoal(leaf)) return cap;
            // crafting first: any order whose inputs are all in the pool (an intermediate is not piled up beyond its share of the bags)
            for (int i = 0; i < e.orders.Count; i++)
            {
                if (!OrderExecutable(e.orders[i])) continue;
                if (IsPiledUp(e, e.orders[i])) continue;
                e.act = ActKind.Craft;
                e.order = e.orders[i];
                return null;
            }
            // gather the raw shortfall that is furthest from covered (share still missing), so the bags fill in proportion to the need
            int pick = PickRaw(e.rawShort, -1);
            if (pick < 0) return null;
            Src s = homeOnly ? HomeSource(pick) : BestSource(pick);
            if (!s.ok) return homeOnly ? null : NoSourceBlocker(pick);
            if (s.needStr >= 0) return Goal.Build(s.needStr, 1, s.planet, 1);
            long need = e.rawShort[pick];
            bool storageLeaf = IsStorageGoal(leaf);
            if (!storageLeaf && PackRoom(pick) + StoreRoom(pick) < Math.Min(need, StackOf(pick)))
            {
                Goal g = StorageBlocker();
                if (g != null) return g;
            }
            if (s.planet == homePlanet)
            {
                e.act = ActKind.HarvestHome;
                e.resource = pick;
                return null;
            }
            Goal fuel = FuelFor(s.planet);
            if (fuel != null) return fuel;
            e.act = ActKind.Trip;
            e.dest = s.planet;
            e.resource = pick;
            return null;
        }

        /// <summary>A remote build or an exploration trip is a Buy whose only extra need is the fuel for the round trip.</summary>
        Goal BuyBlocker(Eval e)
        {
            Goal leaf = e.leaf;
            if (leaf.kind == GoalKind.Build && leaf.planet != homePlanet)
            {
                SimPlanet sp = world.planets[leaf.planet];
                if (!IsReachable(sp))
                {
                    Goal b = ReachBlocker();
                    if (b != null) return b;
                }
                else if (!CanLand(sp))
                {
                    Goal b = PlatingBlocker(leaf.planet);
                    if (b != null) return b;
                }
                return FuelFor(leaf.planet);
            }
            return null;
        }

        Goal FuelFor(int planet)
        {
            TripInfo t = TripCost(planet);
            long need = 2L * t.energy + (long)tFuelReserve;
            long have = EnergyInPool();
            if (have >= need) return null;
            long cells = PoolHave(rFuel) + (need - have);
            return Goal.Stock(rFuel, cells);
        }

        /// <summary>
        /// Which raw resource to gather next: the one whose missing share (short / (short + held)) is largest, the estimated work breaking ties.
        /// With a planet given, only resources that can be harvested there (structure present or hand salvage) with room in the pack qualify.
        /// </summary>
        int PickRaw(long[] shortfall, int planet)
        {
            int pick = -1;
            double best = -1.0;
            SimPlanet sp = planet >= 0 ? world.planets[planet] : null;
            for (int r = 0; r < nRes; r++)
            {
                if (shortfall[r] <= 0) continue;
                if (sp != null)
                {
                    int d = sp.DepositOf(r);
                    if (d < 0 || AccessBonus(planet, sp.depMethod[d]) < 0.0) continue;
                }
                double held = PoolHave(r);
                double share = shortfall[r] / (shortfall[r] + held);
                double score = Math.Floor(share * 20.0) + Math.Min(0.99, shortfall[r] * UnitSeconds(r) / 1e6);
                if (score > best) { best = score; pick = r; }
            }
            return pick;
        }

        /// <summary>An intermediate product (not a line of the cost) already fills its share of the bags: the player waits for the other ingredients instead of crafting more.</summary>
        bool IsPiledUp(Eval e, Order o)
        {
            int outR = RI(o.rec.outputs[0].resourceId);
            for (int i = 0; i < e.cost.Length; i++) if (RI(e.cost[i].resourceId) == outR) return false;
            double cap = (PackSlotsCap + StoreSlotsCap) * tIntermediateShare;
            return SlotsFor(PoolHave(outR), StackOf(outR)) > Math.Max(4.0, cap);
        }

        /// <summary>A craft is worth a tap when a full tap (or the whole remaining order) can run; smaller trickles wait for more inputs.</summary>
        bool OrderExecutable(Order o)
        {
            long possible = long.MaxValue;
            for (int i = 0; i < o.rec.inputs.Length; i++)
                possible = Math.Min(possible, PoolHave(RI(o.rec.inputs[i].resourceId)) / o.rec.inputs[i].amount);
            return possible >= Math.Min(o.batches, BatchSize(o.rec.stationId));
        }

        Goal ReachBlocker()
        {
            if (JumpLevel < cat.upgrades[uJump].maxLevel) return Goal.Upgrade(uJump, JumpLevel + 1);
            return null;
        }

        /// <summary>True when every plating level this world demands can be bought with the reach the player already has (not a circular requirement).</summary>
        bool PlatingFeasible(SimPlanet sp)
        {
            int need = travel.RequiredPlatingLevel(sp.tier);
            for (int i = 0; i < TravelModel.HazardCount; i++)
            {
                if ((sp.hazards & TravelModel.HazardOrder[i]) == 0 || PlatingLevel(i) >= need) continue;
                UpgradeDef u = cat.upgrades[uHull[i]];
                if (need > u.levels.Length) return false;
                if (u.levels[need - 1].requiresReachTier > maxTierLanded) return false;
            }
            return true;
        }

        /// <summary>The first missing hull plating level that stops a landing on this world.</summary>
        Goal PlatingBlocker(int planet)
        {
            SimPlanet sp = world.planets[planet];
            int need = travel.RequiredPlatingLevel(sp.tier);
            for (int i = 0; i < TravelModel.HazardCount; i++)
                if ((sp.hazards & TravelModel.HazardOrder[i]) != 0 && PlatingLevel(i) < need) return Goal.Upgrade(uHull[i], need);
            return null;
        }

        /// <summary>Why a raw resource cannot be harvested anywhere the player can go now, and the step that fixes it.</summary>
        Goal NoSourceBlocker(int r)
        {
            int blocked = -1, unreachable = -1;
            double bd = double.MaxValue, ud = double.MaxValue;
            List<int> list = planetsWith[r];
            for (int i = 0; i < list.Count; i++)
            {
                SimPlanet sp = world.planets[list[i]];
                if (!IsKnown(sp)) continue;
                if (!IsReachable(sp)) { if (sp.distHome < ud) { ud = sp.distHome; unreachable = sp.index; } continue; }
                if (!CanLand(sp) && sp.distHome < bd && PlatingFeasible(sp)) { bd = sp.distHome; blocked = sp.index; }
            }
            if (blocked >= 0)
            {
                Goal g = PlatingBlocker(blocked);
                if (g != null) return g;
            }
            if (unreachable >= 0)
            {
                Goal g = ReachBlocker();
                if (g != null) return g;
            }
            int dest;
            Goal f = FrontierTarget(r, -1, out dest);
            if (f != null) return f;
            return dest < 0 ? null : Goal.ExploreTo(dest);
        }

        /// <summary>Destination for "reach a world of tier T": the cheapest landable one, or the step that makes one available.</summary>
        Goal ExploreTarget(int tier, out int dest)
        {
            dest = -1;
            double bestCost = double.MaxValue;
            int blocked = -1, unreachable = -1;
            double bd = double.MaxValue, ud = double.MaxValue;
            double fuelSec = FuelUnitSeconds();
            for (int p = 0; p < world.planets.Length; p++)
            {
                SimPlanet sp = world.planets[p];
                if (sp.tier < tier || !IsKnown(sp)) continue;
                if (!IsReachable(sp)) { if (sp.distHome < ud) { ud = sp.distHome; unreachable = p; } continue; }
                if (!CanLand(sp)) { if (sp.distHome < bd && PlatingFeasible(sp)) { bd = sp.distHome; blocked = p; } continue; }
                TripInfo t = TripCost(p);
                double c = 2.0 * t.energy * fuelSec + 2.0 * t.seconds;
                if (c < bestCost) { bestCost = c; dest = p; }
            }
            if (dest >= 0) return null;
            if (blocked >= 0)
            {
                Goal g = PlatingBlocker(blocked);
                if (g != null) return g;
            }
            if (unreachable >= 0)
            {
                Goal g = ReachBlocker();
                if (g != null) return g;
            }
            Goal f = FrontierTarget(-1, tier, out dest);
            if (f != null) return f;
            return null;
        }

        Goal ExploreToTarget(int planet, out int dest)
        {
            dest = planet;
            SimPlanet sp = world.planets[planet];
            if (!IsReachable(sp)) return ReachBlocker();
            if (!CanLand(sp)) return PlatingBlocker(planet);
            return null;
        }

        /// <summary>
        /// Nothing the player knows serves the need: visit the known, reachable, unvisited system that lies closest to where the need can be found
        /// (a world of the resource, or of the wanted tier), which widens the surveyed area. Returns null with dest set, or a blocker (jump range).
        /// </summary>
        Goal FrontierTarget(int resource, int tier, out int dest)
        {
            dest = -1;
            int n = world.systemCount;
            var target = new bool[n];
            bool any = false;
            for (int p = 0; p < world.planets.Length; p++)
            {
                SimPlanet sp = world.planets[p];
                bool hit = resource >= 0 ? sp.DepositOf(resource) >= 0 : sp.tier >= tier;
                if (hit) { target[sp.system] = true; any = true; }
            }
            if (!any) return null;
            RouteTable rt = Routes(JumpLevel);
            int bestSys = -1;
            double bestD = double.MaxValue, bestFuel = double.MaxValue;
            int bestPlanet = -1;
            for (int s = 0; s < n; s++)
            {
                if (visitedSys[s] || !knownSys[s] || !rt.reachable[s]) continue;
                int pl = LandablePlanetIn(s);
                if (pl < 0) continue;
                double dmin = double.MaxValue;
                for (int t = 0; t < n; t++)
                {
                    if (!target[t]) continue;
                    double d = s == t ? 0.0 : SysDist(s, t);
                    if (d < dmin) dmin = d;
                }
                if (dmin < bestD - 1e-9 || (Math.Abs(dmin - bestD) <= 1e-9 && rt.baseFuel[s] < bestFuel))
                {
                    bestD = dmin;
                    bestFuel = rt.baseFuel[s];
                    bestSys = s;
                    bestPlanet = pl;
                }
            }
            if (bestSys >= 0) { dest = bestPlanet; return null; }
            // nothing at the frontier can be landed on: clear the hazards of the most promising world, then widen the survey, then the jump range
            int blockedPl = -1;
            double blockedD = double.MaxValue;
            for (int s = 0; s < n; s++)
            {
                if (visitedSys[s] || !knownSys[s] || !rt.reachable[s]) continue;
                double dmin = double.MaxValue;
                for (int t = 0; t < n; t++)
                {
                    if (!target[t]) continue;
                    double d = s == t ? 0.0 : SysDist(s, t);
                    if (d < dmin) dmin = d;
                }
                if (dmin >= blockedD) continue;
                for (int p = 0; p < world.planets.Length; p++)
                {
                    if (world.planets[p].system != s) continue;
                    if (PlatingFeasible(world.planets[p]) && PlatingBlocker(p) != null) { blockedD = dmin; blockedPl = p; break; }
                }
            }
            if (blockedPl >= 0) return PlatingBlocker(blockedPl);
            if (upgLevel[uScanner] < cat.upgrades[uScanner].maxLevel) return Goal.Upgrade(uScanner, upgLevel[uScanner] + 1);
            return ReachBlocker();
        }

        int LandablePlanetIn(int system)
        {
            int best = -1;
            for (int p = 0; p < world.planets.Length; p++)
            {
                SimPlanet sp = world.planets[p];
                if (sp.system != system || !CanLand(sp)) continue;
                if (best < 0 || sp.tier > world.planets[best].tier) best = p;
            }
            return best;
        }

        // ------------------------------------------------------------------------------------------------ defense and waves
        double DefenseRating()
        {
            double dps = 0.0;
            for (int i = 0; i < insts.Count; i++)
            {
                Inst n = insts[i];
                if (n.planet != homePlanet || n.level <= 0) continue;
                StructureDef d = cat.structures[n.str];
                if (!(d.damage > 0f)) continue;
                dps += d.damage / d.fireInterval * EffectOf(n.str, n.level);
            }
            double fire = UpgradeValue(uTurret, 0.0), dmg = UpgradeValue(uTurretDmg, 0.0), tap = UpgradeValue(uPulse, 0.0);
            double health = 100.0 + UpgradeValue(uShield, 0.0);
            return 1.0 + 0.6 * dps * (1.0 + fire) * (1.0 + dmg) + health / 40.0 + 2.0 * tap;
        }

        /// <summary>Energy the player burns in the next wave: grows with the wave number, limited by what the defenses can survive.</summary>
        int WaveEnergy()
        {
            int e = (int)Math.Min(tun.D("sim.wave_energy_cap"), tun.D("sim.wave_energy_base") + tun.D("sim.wave_energy_per_wave") * wavesCalled);
            double rating = DefenseRating() * waveModel.DefenseK;
            double danger = world.planets[homePlanet].danger;
            while (e > 1 && waveModel.Budget(wavesCalled + 1, danger, e) > rating) e--;
            return Math.Max(1, e);
        }

        Goal DefenseBlocker()
        {
            double danger = world.planets[homePlanet].danger;
            if (waveModel.Budget(wavesCalled + 1, danger, 1) <= DefenseRating() * waveModel.DefenseK) return null;
            int gunner = SI("gunner");
            int lv = BuiltLevel(homePlanet, gunner, 1);
            if (lv < cat.structures[gunner].maxLevel) return Goal.Build(gunner, lv + 1, homePlanet, 1);
            int next = InstanceCount(homePlanet, gunner) + 1;
            return next <= 4 ? Goal.Build(gunner, 1, homePlanet, next) : null;
        }
    }
}
