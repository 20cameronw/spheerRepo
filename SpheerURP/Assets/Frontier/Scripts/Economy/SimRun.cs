using System;
using System.Collections.Generic;
using Frontier.Universe;

namespace Frontier.Economy
{
    public sealed partial class SimEngine
    {
        sealed class Trip
        {
            public int dest;
            public Goal build;
            public long[] want;
            public bool returning;
        }

        Trip trip;
        bool extended;
        double lastNoopLog = -1e9;
        long lastGain;
        double nextSample;

        public static void RegisterKeys(Tuning t)
        {
            RegisterGrowthKeys(t);
            t.Register("sim", "sim.harvest_seconds", 30, 10, 90, "Seconds of one harvest mini-game run including its cooldown (design: 25 to 40).");
            t.Register("sim", "sim.harvest_overhead", 3, 0, 30, "Seconds of menu and walking time added to every harvest run.");
            t.Register("sim", "sim.craft_tap_seconds", 2.5, 0.5, 20, "Seconds of one craft tap (crafting itself has no timer).");
            t.Register("sim", "sim.deposit_seconds", 4, 0, 30, "Seconds to drag the backpack into a warehouse.");
            t.Register("sim", "sim.buy_seconds", 4, 0, 30, "Seconds to place, upgrade or purchase one thing.");
            t.Register("sim", "sim.land_seconds", 8, 0, 60, "Seconds of takeoff and landing around every flight.");
            t.Register("sim", "sim.intra_hop_seconds", 22, 5, 120, "Seconds of a Hyperspace Run between two worlds of the same system.");
            t.Register("sim", "sim.base_scanner_ly", 40, 5, 500, "Survey radius around visited systems before any Scanner Range upgrade.");
            t.Register("sim", "sim.max_runs_per_step", 6, 1, 50, "Harvest runs a player does before re-planning.");
            t.Register("sim", "sim.instant_below_seconds", 15, 0, 120, "Build jobs shorter than this finish at once and need no builder.");
            t.Register("sim", "sim.fuel_reserve", 4, 0, 100, "Energy a player keeps above the cost of a trip.");
            t.Register("sim", "sim.auto_quality", 0.5, 0, 1, "Mini-game quality assumed for Auto-Harvester output.");
            t.Register("sim", "sim.storage_trigger", 0.9, 0.3, 1, "A purchase needing more than this share of all slots makes the player build storage first.");
            t.Register("sim", "sim.mega_chunk_share", 0.15, 0.02, 1, "Share of all slots a single megaproject installment may fill.");
            t.Register("sim", "sim.session_jitter_hours", 0.4, 0, 3, "Random shift of each session start (deterministic per seed).");
            t.Register("sim", "sim.pack_deposit_share", 0.6, 0.1, 1, "The player empties the backpack into storage once it is this full.");
            t.Register("sim", "sim.intermediate_share", 0.12, 0.02, 1, "An intermediate product may fill at most this share of all bag slots; beyond it the player waits for the other ingredients.");
            t.Register("sim", "sim.max_warehouses", 6, 1, 12, "Most warehouses the player builds on the home world.");
            t.Register("sim", "sim.max_wait_seconds", 120, 0, 3600, "A player waits for a job that finishes within this time instead of leaving.");
            t.Register("sim", "sim.wave_energy_base", 6, 1, 100, "Energy burned in the first called wave.");
            t.Register("sim", "sim.wave_energy_per_wave", 1.5, 0, 20, "Extra energy burned per wave already called.");
            t.Register("sim", "sim.wave_energy_cap", 60, 1, 1000, "Most energy a player burns in one called wave.");
        }

        public static SimResult Run(EconomyCatalog cat, Tuning tuning, SimWorld world, Archetype arch, SimSettings settings)
        {
            return new SimEngine(cat, tuning, world, arch, settings).Execute();
        }

        // ------------------------------------------------------------------------------------------------ the timeline
        SimResult Execute()
        {
            double horizon = cfg.horizonDays * 86400.0;
            double h0 = arch.sessionStartHours[0];
            bool over = false;
            now = 0.0;
            markClock = 0.0;
            nextSample = 0.0;
            for (int day = 0; !over && day * 86400.0 < horizon; day++)
            {
                for (int k = 0; k < arch.sessionsPerDay; k++)
                {
                    double start = day * 86400.0 + (arch.sessionStartHours[k] - h0) * 3600.0;
                    if (!(day == 0 && k == 0)) start += (rng.NextDouble() - 0.5) * 2.0 * tJitter * 3600.0;
                    if (start < now) start = now;
                    if (start >= horizon) { over = true; break; }
                    RunSession(start);
                }
            }
            AdvanceTo(Math.Min(horizon, Math.Max(now, horizon)));
            TakeSample();
            ScoreBreakdown b = ScoreCalculator.Compute(BuildScoreInputs(), scoreFormula);
            res.finalScore = b.total;
            res.endEra = EraIndex();
            res.lostItems = lostUnits;
            res.summary = Summary();
            return res;
        }

        string Summary()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("structures:");
            for (int i = 0; i < insts.Count; i++)
                if (insts[i].level > 0) sb.Append(' ').Append(cat.structures[insts[i].str].id).Append(insts[i].no > 1 ? "#" + insts[i].no : "").Append('=').Append(insts[i].level).Append(insts[i].planet == homePlanet ? "" : "@" + world.planets[insts[i].planet].name);
            sb.Append("\n  tools:");
            for (int i = 0; i < toolTier.Length; i++) sb.Append(' ').Append(cat.tools[i].id).Append('=').Append(toolTier[i]);
            sb.Append("\n  upgrades:");
            for (int i = 0; i < upgLevel.Length; i++) if (upgLevel[i] > 0) sb.Append(' ').Append(cat.upgrades[i].id).Append('=').Append(upgLevel[i]);
            sb.Append("\n  research ").Append(CountTrue(researched)).Append('/').Append(researched.Length).Append(", builders ").Append(Builders()).Append(", reach tier ").Append(maxTierLanded).Append(", farthest ").Append(maxDistLy.ToString("0")).Append(" ly, worlds landed ").Append(landedCount);
            sb.Append("\n  holdings: ").Append(Holdings());
            sb.Append("\n  mega stages: ").Append(mega[0].stage).Append('/').Append(mega[1].stage).Append('/').Append(mega[2].stage);
            ScoreBreakdown sp = ScoreCalculator.Compute(BuildScoreInputs(), scoreFormula);
            sb.Append("\n  score parts: reach ").Append((long)sp.reach).Append(" worlds ").Append((long)sp.worlds).Append(" industry ").Append((long)sp.industry).Append(" tech ").Append((long)sp.tech).Append(" defense ").Append((long)sp.defense).Append(" atlas ").Append((long)sp.atlas);
            if (mega[0].stage < 5 && !mega[0].building)
            {
                StructureLevelDef ml = cat.structures[megaStr[0]].levels[mega[0].stage];
                sb.Append("  terraformer stage ").Append(mega[0].stage + 1).Append(" filled:");
                for (int i = 0; i < ml.cost.Length; i++) sb.Append(' ').Append(ml.cost[i].resourceId).Append('=').Append(mega[0].filled[i]).Append('/').Append(ml.cost[i].amount);
            }
            return sb.ToString();
        }

        static int CountTrue(bool[] a) { int c = 0; for (int i = 0; i < a.Length; i++) if (a[i]) c++; return c; }

        void RunSession(double start)
        {
            OfflineAccrual(start - now);
            AdvanceTo(start);
            double end = start + arch.minutesPerSession * 60.0;
            double maxWait = tun.D("sim.max_wait_seconds");
            int guard = 0;
            while (now < end && guard++ < 600000)
            {
                double left = (end - now) * arch.efficiency;
                double dt = Step(left);
                if (dt <= 0.0)
                {
                    double nxt = NextJobEnd();
                    if (nxt <= end && nxt - now <= maxWait)
                    {
                        res.idleSeconds += nxt - now;
                        res.idleByEra[EraIndex()] += nxt - now;
                        AdvanceTo(nxt);
                        markClock = now;
                        CheckMilestones();
                        CheckLadderWindow();
                        continue;
                    }
                    res.idleSeconds += end - now;
                    res.idleByEra[EraIndex()] += end - now;
                    AdvanceTo(end);
                    break;
                }
                res.actions++;
                res.playSeconds += dt;
                Decay(dt);
                AdvanceTo(now + dt / arch.efficiency);
                markClock = now;
                CheckMilestones();
                CheckLadderWindow();
            }
        }

        /// <summary>Moves the clock forward, finishing jobs and taking samples in chronological order.</summary>
        void AdvanceTo(double t)
        {
            if (t < now) t = now;
            while (true)
            {
                double jobAt = NextJobEnd();
                double sampleAt = nextSample;
                if (jobAt <= t && jobAt <= sampleAt)
                {
                    CompleteJobs(jobAt);
                    continue;
                }
                if (sampleAt <= t)
                {
                    double keep = now;
                    now = sampleAt;
                    TakeSample();
                    now = keep;
                    // dense early (the first minutes matter), every sampleEveryHours later
                    double step = Math.Max(60.0, sampleAt * 0.25);
                    nextSample = sampleAt + Math.Min(step, cfg.sampleEveryHours * 3600.0);
                    continue;
                }
                break;
            }
            now = t;
        }

        /// <summary>Auto-Harvesters gather bulk goods while the player is away (no tool bonus, average quality, capped hours); the Dyson Collector feeds fuel.</summary>
        void OfflineAccrual(double gapSeconds)
        {
            double gapH = gapSeconds / 3600.0;
            if (gapH <= 0.0) return;
            for (int i = 0; i < insts.Count; i++)
            {
                Inst n = insts[i];
                if (n.str != sAuto || n.level <= 0) continue;
                StructureLevelDef ld = cat.structures[sAuto].levels[n.level - 1];
                double runs = ld.effect * Math.Min(gapH, ld.capacity);
                SimPlanet sp = world.planets[n.planet];
                int bestR = -1, bestD = -1;
                double bestHave = double.MaxValue;
                for (int d = 0; d < sp.depRes.Length; d++)
                {
                    int r = sp.depRes[d];
                    if (r != rWood && r != rStone && r != rScrap) continue;
                    double have = PoolHave(r) * val[r];
                    if (have < bestHave) { bestHave = have; bestR = r; bestD = d; }
                }
                if (bestR < 0) continue;
                double bonus = Math.Max(0.0, AccessBonus(n.planet, sp.depMethod[bestD]));
                double y = sp.depBaseYield[bestD] * (double)sp.depRichness[bestD] * (1.0 + bonus + GlobalYieldBonus()) * (0.4 + 1.1 * tAutoQ);
                AddPool(bestR, (long)Math.Floor(runs * y));
            }
            double passive = EffectOf(megaStr[2], mega[2].stage);
            if (passive > 0.0) AddPool(rFuel, (long)Math.Floor(passive * Math.Min(gapH, 24.0)));
        }

        void CompleteJobs(double at)
        {
            markClock = at;
            for (int i = 0; i < insts.Count; i++)
            {
                Inst n = insts[i];
                if (n.pending > 0 && n.jobEnd <= at + 1e-9)
                {
                    n.level = n.pending;
                    n.pending = 0;
                    OnStructureChanged(n.str);
                    Log("built " + cat.structures[n.str].id + " L" + n.level);
                }
            }
            for (int i = 0; i < 3; i++)
            {
                MegaState m = mega[i];
                if (m.building && m.jobEnd <= at + 1e-9)
                {
                    StructureDef md = cat.structures[megaStr[i]];
                    m.stage++;
                    m.building = false;
                    // the queued deliveries become the next stage's contributions; a fully stocked stage starts at once
                    m.filled = m.stage < md.maxLevel ? m.pre : new long[0];
                    m.pre = m.stage + 1 < md.maxLevel ? new long[md.levels[m.stage + 1].cost.Length] : new long[0];
                    if (m.stage < md.maxLevel)
                    {
                        StructureLevelDef nl = md.levels[m.stage];
                        bool full = true;
                        for (int k = 0; k < nl.cost.Length; k++) if (m.filled[k] < nl.cost[k].amount) full = false;
                        if (full && nl.requiresReachTier <= maxTierLanded) { m.building = true; m.jobEnd = at + nl.buildSeconds; res.megaSeconds.Add(nl.buildSeconds); }
                    }
                    Touch();
                    Log("megaproject " + md.id + " stage " + m.stage);
                }
            }
            CheckMilestones();
            CheckLadderWindow();
        }

        void OnStructureChanged(int str)
        {
            Touch();
            if (str == sRadar) RecomputeKnown();
        }

        void CheckLadderWindow()
        {
            int last = Math.Min(ladder.Length, ladderFirst + 3 * cfg.lookahead);
            for (int i = ladderFirst; i < last; i++) LadderDone(i);
        }

        // ------------------------------------------------------------------------------------------------ milestones
        void CheckMilestones()
        {
            if (res.harvestRuns > 0) Mark("first_harvest");
            if (insts.Count > 0) Mark("first_structure");
            if (BuiltLevel(homePlanet, sWarehouse, 1) >= 1) Mark("first_warehouse");
            if (craftedOnce[rFuel]) Mark("first_fuel");
            if (landedCount >= 2) Mark("first_hop");
            if (HomeLevel(sLab) >= 1) Mark("research_lab");
            if (wavesCalled >= 1) Mark("first_wave");
            if (upgLevel[uJump] >= 1) Mark("jump_range_1");
            if (Builders() >= 2) Mark("second_builder");
            if (craftedOnce[rSteel]) Mark("first_steel");
            if (HomeLevel(sCargoDepot) >= 1) Mark("cargo_depot");
            bool anySystem = false;
            for (int p = 0; p < landed.Length && !anySystem; p++) if (landed[p] && world.planets[p].system != homeSystem) anySystem = true;
            if (anySystem) Mark("second_system");
            bool plating = false, radPlating = upgLevel[uHull[3]] >= 1;
            for (int i = 0; i < TravelModel.HazardCount; i++) if (upgLevel[uHull[i]] >= 1) plating = true;
            if (plating) Mark("first_hazard_plating");
            if (HomeLevel(SI("chem_reactor")) >= 1) Mark("chem_reactor");
            if (craftedOnce[rPlasma]) Mark("plasma_crafted");
            if (HomeLevel(sRelay) >= 1) Mark("orbital_relay");
            if (HomeLevel(sHub) >= 1) Mark("colony_hub");
            if (HomeLevel(sHangar) >= 1) Mark("hangar");
            if (HomeLevel(sAuto) >= 1) Mark("auto_harvester");
            if (radPlating) Mark("rad_plating");
            if (craftedOnce[rAnti]) Mark("antimatter_crafted");
            if (craftedOnce[RI("void_alloy")]) Mark("void_alloy_crafted");
            int stages = mega[0].stage + mega[1].stage + mega[2].stage;
            if (stages >= 1) Mark("mega_first_stage");
            int highestStage = Math.Max(mega[0].stage, Math.Max(mega[1].stage, mega[2].stage));
            if (highestStage >= 3) Mark("mega_stage_3");
            var eraFacts = new EraFacts
            {
                worldsLanded = landedCount,
                researchLabBuilt = res.milestones.ContainsKey("research_lab"),
                jumpRangePurchased = res.milestones.ContainsKey("jump_range_1"),
                hazardPlatingPurchased = plating,
                radiationPlatingPurchased = radPlating,
                antimatterCrafted = craftedOnce[rAnti],
                highestMegaprojectStage = highestStage
            };
            for (var era = FrontierEra.Foothold; era <= FrontierEra.Ascension; era++)
                if (EraRules.Meets(era, eraFacts)) Mark(SimBands.EraMilestones[(int)era]);
            for (int k = 1; k <= 9; k++) if (maxTierLanded >= k) Mark("reach_tier_" + k);
            if (HomeLevel(SI("fuel_depot")) >= 1) Mark("fuel_depot");
            if (toolTier[TI("axe")] >= 2) Mark("axe_2");
        }

        // ------------------------------------------------------------------------------------------------ one decision step
        /// <summary>Does one thing the player would do next and returns the active seconds it took (0 when there is nothing useful to do).</summary>
        double Step(double left)
        {
            if (trip != null) return StepTrip(left);
            int first = FirstUndone();
            if (first >= ladder.Length)
            {
                if (extended) return 0.0;
                extended = true;
                markClock = now;
                Mark("ladder_complete");
                ExtendLadder();
                first = FirstUndone();
                if (first >= ladder.Length) return 0.0;
            }
            int last = Math.Min(ladder.Length, first + cfg.lookahead);
            string idleWhy = null;
            double storage = StorageStep(left);
            if (storage > 0.0) return storage;
            double growth = GrowthStep(left);
            if (growth > 0.0) return growth;
            for (int i = first; i < last; i++)
            {
                if (LadderDone(i)) continue;
                Eval e = Evaluate(ladder[i]);
                if (cfg.keepLog) idleWhy = (idleWhy == null ? "" : idleWhy + " | ") + ladder[i].text + ":" + e.kind + (e.why != null ? "(" + e.why + ")" : "") + (e.kind == EvalKind.Wait ? "(until " + FormatClock(e.waitUntil) + ")" : "");
                if (e.kind == EvalKind.Done || e.kind == EvalKind.Wait) continue;
                if (e.kind == EvalKind.Stuck)
                {
                    if (cfg.keepLog) Log("STUCK " + ladder[i].text + " " + e.why);
                    // the endless tail may ask for a world the finite sim universe does not contain (tier 10 lies beyond its radius); only headline goals count as stuck
                    if (ladder[i].text.StartsWith("auto:", StringComparison.Ordinal)) { res.stuckEndless++; continue; }
                    if (res.stuck < 3) res.Stat("stuck@d" + (int)(now / 86400.0) + ":" + ladder[i].text + ":" + Evaluate(ladder[i], true).why, 1);
                    res.stuck++;
                    continue;
                }
                if (e.kind == EvalKind.Buy || e.kind == EvalKind.Work)
                {
                    if (SlotShare() > 0.88) DiscardExcess(e, 0.72);
                }
                if (e.kind == EvalKind.Buy) { double b = ExecuteBuy(e, left); if (b <= 0.0 && cfg.keepLog && now - lastNoopLog > 21600.0) { lastNoopLog = now; Log("NOOP buy " + e.leaf.kind + " " + e.leaf.text + " idx" + e.leaf.idx + " L" + e.leaf.level); } return b; }
                double w = ExecuteWork(e, left);
                if (w <= 0.0 && cfg.keepLog && now - lastNoopLog > 21600.0) { lastNoopLog = now; Log("NOOP work " + e.act + " leaf " + e.leaf.kind + " idx" + e.leaf.idx + " L" + e.leaf.level + " top " + ladder[i].text + " res " + e.resource + " dest " + e.dest + (e.order != null ? " recipe " + e.order.rec.id : "") + " :: " + Holdings()); }
                return w;
            }
            if (cfg.keepLog && idleWhy != null) Log("IDLE " + idleWhy);
            return 0.0;
        }

        double ExecuteBuy(Eval e, double left)
        {
            Goal l = e.leaf;
            switch (l.kind)
            {
                case GoalKind.Build:
                    if (l.planet != homePlanet) return StartTrip(l.planet, l, null);
                    Pay(e.cost);
                    StartBuild(l.planet, l.idx, l.no, l.level);
                    res.buildJobs++;
                    return tBuy;
                case GoalKind.Tool:
                    Pay(e.cost);
                    toolTier[l.idx] = l.level;
                    Touch();
                    Log("tool " + cat.tools[l.idx].id + " tier " + l.level);
                    return tBuy;
                case GoalKind.Upgrade:
                    Pay(e.cost);
                    upgLevel[l.idx] = l.level;
                    Touch();
                    if (l.idx == uScanner) RecomputeKnown();
                    Log("upgrade " + cat.upgrades[l.idx].id + " L" + l.level);
                    return tBuy;
                case GoalKind.Research:
                    Pay(e.cost);
                    researched[l.idx] = true;
                    Touch();
                    Log("research " + cat.research[l.idx].id);
                    return tBuy;
                case GoalKind.Mega:
                    return ContributeMega(e, l);
                case GoalKind.Wave:
                    return DoWave(e);
                default:
                    return 0.0;
            }
        }

        double ExecuteWork(Eval e, double left)
        {
            if (cfg.keepLog) Log("work " + e.act + (e.act == ActKind.Craft ? " " + e.order.rec.id + " x" + e.order.batches : "") + " res=" + (e.resource >= 0 ? cat.resources[e.resource].id : "-") + " dest=" + (e.dest >= 0 ? world.planets[e.dest].name : "-") + " top=" + e.top.text + " leaf=" + Describe(e.leaf));
            switch (e.act)
            {
                case ActKind.Craft: return DoCraft(e, left);
                case ActKind.HarvestHome: return DoHarvestHome(e, left);
                case ActKind.Trip:
                {
                    double prep = PrepareTrip(e);
                    if (prep < 0.0) return 0.0;
                    double t = StartTrip(e.dest, null, e.rawShort);
                    return t > 0.0 ? t + prep : prep;
                }
                default: return 0.0;
            }
        }

        /// <summary>Empties the pack into storage (and throws junk away) so a gather trip has room for what it is sent to fetch; -1 when there is no way to make room.</summary>
        double PrepareTrip(Eval e)
        {
            double dt = 0.0;
            if (e.resource < 0) return dt;
            long need = Math.Min(e.rawShort[e.resource], StackOf(e.resource));
            if (PackRoom(e.resource) >= need) return dt;
            if (Deposit() > 0) dt += tDeposit;
            if (PackRoom(e.resource) < 1)
            {
                Discard(e);
                if (Deposit() > 0) dt += tDeposit;
            }
            return PackRoom(e.resource) < 1 ? -1.0 : dt;
        }

        void Pay(ItemAmount[] cost)
        {
            for (int i = 0; i < cost.Length; i++)
            {
                int r = RI(cost[i].resourceId);
                if (PoolHave(r) < cost[i].amount) throw new InvalidOperationException("sim: paid more than owned: " + cost[i].resourceId);
                SpendPool(r, cost[i].amount);
            }
            for (int i = 0; i < cost.Length; i++) xp += cost[i].amount * val[RI(cost[i].resourceId)] * 0.01;
        }

        void StartBuild(int planet, int str, int no, int level)
        {
            Inst inst = FindInst(planet, str, no);
            if (inst == null)
            {
                inst = new Inst { planet = planet, str = str, no = no };
                insts.Add(inst);
            }
            StructureLevelDef ld = cat.structures[str].levels[level - 1];
            if (ld.buildSeconds < tInstant)
            {
                inst.level = level;
                OnStructureChanged(str);
                Log("built " + cat.structures[str].id + " L" + level + " at " + world.planets[planet].name);
            }
            else
            {
                inst.pending = level;
                inst.jobEnd = now + ld.buildSeconds;
                res.jobSeconds.Add(ld.buildSeconds);
                Log("start " + cat.structures[str].id + " L" + level + " (" + ld.buildSeconds + " s)");
            }
        }

        // ------------------------------------------------------------------------------------------------ harvesting
        double HarvestRun(int planet, int dep)
        {
            SimPlanet sp = world.planets[planet];
            int r = sp.depRes[dep];
            HarvestMethod m = sp.depMethod[dep];
            double q = SampleQuality();
            double y = DepositYieldPerRun(sp, dep, 0.4 + 1.1 * q);
            long n = Math.Max(1L, (long)Math.Floor(y + 0.5));
            long room = PackRoom(r);
            long add = Math.Min(n, room);
            pack[r] += add;
            lostUnits += n - add;
            discovered[r] = true;
            lastGain = add;
            res.harvestRuns++;
            res.harvestedValue += add * val[r];
            NoteHarvest(planet, m, add * val[r]);
            res.Stat("harvest_s:" + cat.tools[LadderOf(m)].id, RunSeconds(m));
            res.Stat("harvest_v:" + cat.tools[LadderOf(m)].id, add * val[r]);
            xp += 1.0;
            return RunSeconds(m);
        }

        int PlayerLevel() { return 1 + (int)Math.Floor(Math.Sqrt(xp / 40.0)); }

        string Holdings()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("pack ").Append(PackSlotsCap).Append(" slots, store ").Append(StoreSlotsCap).Append(": ");
            for (int r = 0; r < nRes; r++)
            {
                long n = pack[r] + store[r];
                if (n <= 0) continue;
                sb.Append(cat.resources[r].id).Append(' ').Append(n).Append('(').Append(SlotsFor(n, StackOf(r))).Append(") ");
            }
            return sb.ToString();
        }

        /// <summary>The resources the next goals of the ladder will want (never thrown away).</summary>
        bool[] WindowKeep(Eval e)
        {
            var keep = new bool[nRes];
            if (e != null) for (int i = 0; i < e.cost.Length; i++) MarkKeep(RI(e.cost[i].resourceId), keep, 0);
            int last = Math.Min(ladder.Length, ladderFirst + cfg.lookahead + 2);
            for (int i = ladderFirst; i < last; i++)
            {
                ItemAmount[] c = PeekCost(ladder[i]);
                for (int k = 0; k < c.Length; k++) MarkKeep(RI(c[k].resourceId), keep, 0);
            }
            for (int r = 0; r < nRes; r++) if (isEnergy[r]) keep[r] = true;
            return keep;
        }

        void MarkKeep(int r, bool[] keep, int depth)
        {
            if (keep[r]) return;
            keep[r] = true;
            if (depth > 8) return;
            List<RecipeDef> list = recipesFor[r];
            for (int k = 0; k < list.Count; k++)
                for (int i = 0; i < list[k].inputs.Length; i++) MarkKeep(RI(list[k].inputs[i].resourceId), keep, depth + 1);
        }

        /// <summary>The cost of the next step of a goal, without resolving any prerequisite.</summary>
        ItemAmount[] PeekCost(Goal g)
        {
            switch (g.kind)
            {
                case GoalKind.Build:
                {
                    int cur = BuiltLevel(g.planet, g.idx, g.no);
                    if (cur >= g.level) return new ItemAmount[0];
                    return cat.structures[g.idx].levels[cur].cost;
                }
                case GoalKind.Tool:
                    return toolTier[g.idx] >= g.level ? new ItemAmount[0] : cat.tools[g.idx].tiers[toolTier[g.idx]].cost;
                case GoalKind.Upgrade:
                    return upgLevel[g.idx] >= g.level ? new ItemAmount[0] : cat.upgrades[g.idx].levels[upgLevel[g.idx]].cost;
                case GoalKind.Research:
                    return researched[g.idx] ? new ItemAmount[0] : cat.research[g.idx].cost;
                case GoalKind.Stock:
                    return new[] { new ItemAmount(cat.resources[g.idx].id, 1) };
                case GoalKind.Mega:
                    return mega[g.idx].stage >= g.level ? new ItemAmount[0] : cat.structures[megaStr[g.idx]].levels[mega[g.idx].stage].cost;
                default:
                    return new ItemAmount[0];
            }
        }

        /// <summary>Drops surplus the current plan does not need (the player throws junk away when the bags are full), cheapest slots first. Returns the units dropped.</summary>
        long Discard(Eval e)
        {
            return DiscardExcess(e, 0.0);
        }

        long[] KeepAmounts(Eval e, bool desperate = false)
        {
            var keep = new long[nRes];
            if (e != null)
            {
                for (int i = 0; i < e.cost.Length; i++) keep[RI(e.cost[i].resourceId)] += e.cost[i].amount;
                for (int k = 0; k < e.orders.Count; k++)
                {
                    RecipeDef rec = e.orders[k].rec;
                    for (int i = 0; i < rec.inputs.Length; i++) keep[RI(rec.inputs[i].resourceId)] += rec.inputs[i].amount * e.orders[k].batches;
                    keep[RI(rec.outputs[0].resourceId)] += (long)rec.outputs[0].amount * e.orders[k].batches;
                }
                if (!desperate) AddTopKeep(e, keep);
            }
            for (int r = 0; r < nRes; r++) if (isEnergy[r]) keep[r] += 600;
            if (e != null && e.chainKeep != null) for (int r = 0; r < nRes; r++) if (e.chainKeep[r]) keep[r] = long.MaxValue / 4;
            return keep;
        }

        /// <summary>
        /// When a blocker is being worked on, the top goal's own ingredients (not just the blocker's) must survive a clean-up of the bags.
        /// What the bags could never hold at once is not protected: only a share of the bags is reserved for the plan's raw inputs.
        /// </summary>
        void AddTopKeep(Eval e, long[] keep)
        {
            Goal top = e.top;
            if (top == null || ReferenceEquals(top, e.leaf)) return;
            ItemAmount[] cost = TopCost(top);
            if (cost.Length == 0) return;
            var t = new Eval { top = top, rawShort = new long[nRes], cost = cost };
            ExpandNeeds(t, 1.0);
            double slots = PlanSlots(t), budget = 0.5 * (PackSlotsCap + StoreSlotsCap);
            if (slots > budget) ExpandNeeds(t, Math.Max(0.02, budget / slots));
            for (int i = 0; i < cost.Length; i++) keep[RI(cost[i].resourceId)] += cost[i].amount;
            for (int k = 0; k < t.orders.Count; k++)
            {
                RecipeDef rec = t.orders[k].rec;
                for (int i = 0; i < rec.inputs.Length; i++) keep[RI(rec.inputs[i].resourceId)] += rec.inputs[i].amount * t.orders[k].batches;
            }
        }

        /// <summary>The cost of the next step of the top goal (a megaproject contributes one installment at a time).</summary>
        ItemAmount[] TopCost(Goal top)
        {
            if (top.kind == GoalKind.Mega)
            {
                MegaState m = mega[top.idx];
                if (m.stage >= top.level) return new ItemAmount[0];
                if (m.building)
                {
                    if (top.level < m.stage + 2 || m.stage + 2 > cat.structures[megaStr[top.idx]].maxLevel) return new ItemAmount[0];
                    return CostOf(new Goal { kind = GoalKind.Mega, idx = top.idx, level = m.stage + 2, amount = 1 });
                }
                return CostOf(new Goal { kind = GoalKind.Mega, idx = top.idx, level = m.stage + 1 });
            }
            return PeekCost(top);
        }

        /// <summary>Throws away what exceeds the plan's needs (plus one stack), lowest value per slot first, until the bags are at the target share.</summary>
        long DiscardExcess(Eval e, double targetShare)
        {
            long dropped = DropSurplus(KeepAmounts(e), targetShare);
            // still jammed and nothing was surplus: the player gives up the far-future needs and keeps only what this very step uses
            if (dropped == 0 && SlotShare() > Math.Max(targetShare, 0.97)) dropped = DropSurplus(KeepAmounts(e, true), targetShare);
            return dropped;
        }

        long DropSurplus(long[] keep, double targetShare)
        {
            long dropped = 0;
            for (int iter = 0; iter < 10 && SlotShare() > targetShare; iter++)
            {
                int worst = -1;
                double worstDensity = double.MaxValue;
                for (int r = 0; r < nRes; r++)
                {
                    long held = pack[r] + store[r];
                    long allowed = keep[r] + StackOf(r);
                    if (held - allowed < StackOf(r)) continue;
                    double density = val[r] * StackOf(r);
                    if (density < worstDensity) { worstDensity = density; worst = r; }
                }
                if (worst < 0) break;
                long drop = pack[worst] + store[worst] - (keep[worst] + StackOf(worst));
                long fromStore = Math.Min(store[worst], drop);
                store[worst] -= fromStore;
                pack[worst] -= drop - fromStore;
                dropped += drop;
                res.discardedValue += drop * val[worst];
                res.Stat("discard_v", drop * val[worst]);
                Log("discard " + drop + " " + cat.resources[worst].id);
            }
            return dropped;
        }

        string KeepReport(Eval e)
        {
            long[] keep = KeepAmounts(e);
            int pm, pe, sm, se;
            Usage(pack, out pm, out pe);
            Usage(store, out sm, out se);
            var sb = new System.Text.StringBuilder();
            sb.Append("main ").Append(pm + sm).Append(" energy ").Append(pe + se).Append(" tank ").Append(TankSlots).Append(" depot ").Append(DepotEnergySlots).Append(" cap ").Append(PackSlotsCap + StoreSlotsCap).Append(" ::");
            for (int r = 0; r < nRes; r++)
            {
                long held = pack[r] + store[r];
                if (held <= 0) continue;
                sb.Append(' ').Append(cat.resources[r].id).Append(' ').Append(held).Append(keep[r] >= long.MaxValue / 8 ? "/chain" : "/" + keep[r]).Append(e.chainKeep != null && e.chainKeep[r] ? "*" : "");
            }
            return sb.ToString();
        }

        double SlotShare()
        {
            int pm, pe, sm, se;
            Usage(pack, out pm, out pe);
            Usage(store, out sm, out se);
            int used = pm + Math.Max(0, pe - TankSlots) + sm + Math.Max(0, se - DepotEnergySlots);
            return used / (double)Math.Max(1, PackSlotsCap + StoreSlotsCap);
        }

        /// <summary>A player who sees the bags filling up expands storage before it blocks anything (warehouse rooms are cheap).</summary>
        double StorageStep(double left)
        {
            double share = SlotShare();
            if (share < tStorageTrigger * 0.8) return 0.0;
            Goal g = StorageBlocker();
            if (g == null) return 0.0;
            Eval e = Evaluate(g);
            if (e.kind == EvalKind.Buy) return ExecuteBuy(e, left);
            if (e.kind == EvalKind.Work && share > 0.97) return ExecuteWork(e, left);
            return 0.0;
        }

        double DoHarvestHome(Eval e, double left)
        {
            SimPlanet h = world.planets[homePlanet];
            var remaining = (long[])e.rawShort.Clone();
            double dt = 0.0;
            for (int runs = 0; runs < tMaxRuns && dt < left; runs++)
            {
                int pick = PickRaw(remaining, homePlanet);
                if (pick < 0) break;
                int dep = h.DepositOf(pick);
                double expected = DepositYieldPerRun(h, dep, MeanQualityFactor);
                if (PackRoom(pick) < expected * 0.9 + 1.0 || PackSlotsUsedShare() >= tPackDeposit)
                {
                    int moved = Deposit();
                    if (moved > 0) dt += tDeposit;
                    if (PackRoom(pick) < 1 && Discard(e) > 0)
                    {
                        moved = Deposit();
                        if (moved > 0) dt += tDeposit;
                    }
                    if (PackRoom(pick) < 1) break;
                }
                dt += HarvestRun(homePlanet, dep);
                remaining[pick] -= lastGain;
            }
            return dt;
        }

        double PackSlotsUsedShare()
        {
            int mainSlots, energySlots;
            Usage(pack, out mainSlots, out energySlots);
            int used = mainSlots + Math.Max(0, energySlots - TankSlots);
            return used / (double)Math.Max(1, PackSlotsCap);
        }

        // ------------------------------------------------------------------------------------------------ crafting
        double DoCraft(Eval e, double left)
        {
            Order o = e.order;
            RecipeDef rec = o.rec;
            int batchMax = BatchSize(rec.stationId);
            long possible = long.MaxValue;
            for (int i = 0; i < rec.inputs.Length; i++) possible = Math.Min(possible, PoolHave(RI(rec.inputs[i].resourceId)) / rec.inputs[i].amount);
            long batches = Math.Min(o.batches, possible);
            if (batches <= 0) return 0.0;
            int outR = RI(rec.outputs[0].resourceId);
            int outAmt = rec.outputs[0].amount;
            long taps = (batches + batchMax - 1) / batchMax;
            long maxTaps = Math.Max(1L, (long)Math.Floor(left / tCraft));
            if (taps > maxTaps) { taps = maxTaps; batches = Math.Min(batches, taps * batchMax); }
            for (int i = 0; i < rec.inputs.Length; i++) SpendPool(RI(rec.inputs[i].resourceId), rec.inputs[i].amount * batches);
            long room = StoreRoom(outR) + PackRoom(outR);
            if (room < (long)outAmt * batches && room < outAmt)
            {
                // no room for even one batch: throw some junk away
                if (Discard(e) > 0) { Deposit(); room = StoreRoom(outR) + PackRoom(outR); }
            }
            long fit = room / outAmt;
            if (fit < batches)
            {
                // the output does not fit even after the inputs left their slots: put the unused inputs back
                for (int i = 0; i < rec.inputs.Length; i++) AddPool(RI(rec.inputs[i].resourceId), rec.inputs[i].amount * (batches - fit));
                if (fit <= 0 && cfg.keepLog && now - lastNoopLog > 21600.0) Log("craft0 " + rec.id + " batches=" + batches + " room=" + room + " share=" + SlotShare().ToString("0.00") + " discarded=" + res.discardedValue.ToString("0") + " :: " + KeepReport(e));
                batches = fit;
                if (batches <= 0) return 0.0;
                taps = (batches + batchMax - 1) / batchMax;
            }
            AddPool(outR, (long)outAmt * batches);
            craftedOnce[outR] = true;
            res.craftActions += taps;
            res.Stat("craft_s:" + rec.stationId, taps * tCraft);
            NoteCraft(rec.stationId, taps * tCraft);
            return taps * tCraft;
        }

        // ------------------------------------------------------------------------------------------------ travel
        double StartTrip(int dest, Goal build, long[] want)
        {
            TripInfo t = TripCost(dest);
            if (!t.ok) return 0.0;
            if (!PayEnergy(2 * t.energy)) return 0.0;
            res.Stat("trip_s", 2.0 * (t.seconds + tLand));
            res.Stat("trip_energy", 2.0 * t.energy);
            if (build != null) Pay(CostOf(build));
            trip = new Trip { dest = dest, build = build, want = want == null ? null : (long[])want.Clone() };
            Land(dest);
            int refund = UseAutopilot ? 0 : travel.Refund(t.energy, SampleQuality());
            AddPackOnly(rFuel, refund);
            res.expeditions++;
            Log("trip to " + world.planets[dest].name + " (tier " + world.planets[dest].tier + ", " + (2 * t.energy) + " energy)");
            return t.seconds + tLand;
        }

        void AddPackOnly(int r, long qty)
        {
            long n = Math.Min(qty, PackRoom(r));
            if (n > 0) pack[r] += n;
        }

        double StepTrip(double left)
        {
            Trip tr = trip;
            if (tr.build != null)
            {
                Goal b = tr.build;
                tr.build = null;
                StartBuild(b.planet, b.idx, b.no, b.level);
                res.buildJobs++;
                return tBuy;
            }
            if (!tr.returning && tr.want != null)
            {
                double d = HarvestAtDest(tr, left);
                if (d > 0.0) return d;
            }
            tr.returning = true;
            TripInfo t = TripCost(tr.dest);
            double dt = t.seconds + tLand;
            AddPackOnly(rFuel, UseAutopilot ? 0 : travel.Refund(t.energy, SampleQuality()));
            int moved = Deposit();
            if (moved > 0) dt += tDeposit;
            trip = null;
            return dt;
        }

        double HarvestAtDest(Trip tr, double left)
        {
            SimPlanet sp = world.planets[tr.dest];
            double dt = 0.0;
            for (int runs = 0; runs < tMaxRuns && dt < left; runs++)
            {
                int pick = -1, pickDep = -1;
                var open = new long[nRes];
                for (int d = 0; d < sp.depRes.Length; d++)
                {
                    int r = sp.depRes[d];
                    if (r >= 0 && tr.want[r] > 0 && PackRoom(r) >= 1) open[r] = tr.want[r];
                }
                pick = PickRaw(open, tr.dest);
                if (pick < 0) break;
                pickDep = sp.DepositOf(pick);
                dt += HarvestRun(tr.dest, pickDep);
                tr.want[pick] -= lastGain;
            }
            return dt;
        }

        // ------------------------------------------------------------------------------------------------ megaprojects and waves
        double ContributeMega(Eval e, Goal l)
        {
            MegaState m = mega[l.idx];
            bool prefund = l.amount == 1;
            long[] arr = prefund ? m.pre : m.filled;
            StructureLevelDef ld = cat.structures[megaStr[l.idx]].levels[l.level - 1];
            if (e.cost.Length > 0)
            {
                ItemAmount line = e.cost[0];
                int r = RI(line.resourceId);
                SpendPool(r, line.amount);
                res.progressPlay.Add(res.playSeconds);
                for (int i = 0; i < ld.cost.Length; i++)
                {
                    if (ld.cost[i].resourceId == line.resourceId && arr[i] < ld.cost[i].amount) { arr[i] += line.amount; break; }
                }
            }
            if (prefund) return tBuy;
            bool complete = true;
            for (int i = 0; i < ld.cost.Length; i++) if (arr[i] < ld.cost[i].amount) complete = false;
            if (complete)
            {
                m.building = true;
                m.jobEnd = now + ld.buildSeconds;
                res.megaSeconds.Add(ld.buildSeconds);
                Log("megaproject " + cat.structures[megaStr[l.idx]].id + " stage " + l.level + " funded (" + ld.buildSeconds + " s)");
            }
            return tBuy;
        }

        double DoWave(Eval e)
        {
            int energy = e.cost[0].amount;
            if (!PayEnergy(energy)) return 0.0;
            SimPlanet sp = world.planets[homePlanet];
            WaveReward wr = waveModel.Reward(wavesCalled + 1, energy, energy * val[rFuel], sp.danger, sp.tier, EffectOf(sAlarm, HomeLevel(sAlarm)), UpgradeValue(uSalvage, 0.0), PlayerLevel(), val[rCore], val[rFuel]);
            long cores = (long)Math.Floor(wr.coresExpected);
            if (rng.NextDouble() < wr.coresExpected - cores) cores++;
            long cells = (long)Math.Floor(wr.fuelRefundExpected);
            if (rng.NextDouble() < wr.fuelRefundExpected - cells) cells++;
            AddPool(rCore, cores);
            AddPool(rFuel, cells);
            xp += wr.xp;
            wavesCalled++;
            res.waves++;
            Log("wave " + wavesCalled + " burned " + energy + " -> " + cores + " cores, " + cells + " fuel");
            return wr.seconds + tBuy;
        }
    }
}
