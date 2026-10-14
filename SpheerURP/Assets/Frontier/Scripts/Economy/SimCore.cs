using System;
using System.Collections.Generic;
using Frontier.Universe;

namespace Frontier.Economy
{
    /// <summary>Run parameters of one simulated player.</summary>
    public sealed class SimSettings
    {
        public ulong seed = 1UL;
        public double horizonDays = 730.0;
        /// <summary>How many unfinished goals of the ladder the player looks at (the first one that can progress gets the action).</summary>
        public int lookahead = 6;
        public double sampleEveryHours = 12.0;
        public bool keepLog;
        /// <summary>Replaces the default headline ladder (one goal per line, see SimLadder).</summary>
        public string[] ladder;
    }

    /// <summary>A placed structure (one pad group on one planet).</summary>
    sealed class Inst
    {
        public int planet, str, no, level, pending;
        public double jobEnd, use;
    }

    sealed class MegaState
    {
        public int stage;
        public long[] filled = new long[0];
        /// <summary>Deliveries for the stage after the one under construction (a queue: materials may arrive while a timer runs).</summary>
        public long[] pre = new long[0];
        public double jobEnd;
        public bool building;
    }

    sealed class SimRng
    {
        ulong _s;

        public SimRng(ulong seed) { _s = seed ^ 0x9E3779B97F4A7C15UL; }

        public ulong Next()
        {
            unchecked
            {
                _s += 0x9E3779B97F4A7C15UL;
                ulong z = _s;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        public double NextDouble() { return (Next() >> 11) * (1.0 / 9007199254740992.0); }

        /// <summary>Close to a standard normal: the sum of four uniforms, rescaled (arithmetic only, so it is identical on every runtime).</summary>
        public double Gauss()
        {
            double s = NextDouble() + NextDouble() + NextDouble() + NextDouble();
            return (s - 2.0) * 1.7320508075688772;
        }
    }

    /// <summary>Shortest fuel routes from the home system for one jump range.</summary>
    sealed class RouteTable
    {
        public double[] baseFuel, lenSum, secSum;
        public int[] legs;
        public bool[] reachable;
    }

    /// <summary>
    /// The deterministic "average player". It follows a headline goal ladder (what the player WANTS next), resolves what each goal
    /// needs (research, station levels, reach, plating, crafted ingredients, raw harvests), runs harvest trips, builds with a limited
    /// number of builders, and records a timeline. All numbers come from the catalog and the tuning, nothing is hard-coded here
    /// except behaviour.
    /// </summary>
    public sealed partial class SimEngine
    {
        readonly EconomyCatalog cat;
        readonly Tuning tun;
        readonly SimWorld world;
        readonly Archetype arch;
        readonly SimSettings cfg;
        readonly TravelModel travel;
        readonly WaveEconomy waveModel;
        readonly ScoreFormula scoreFormula;
        readonly SimResult res;
        readonly SimRng rng;

        // tuning snapshot
        readonly double tHarvest, tOverhead, tCraft, tDeposit, tBuy, tLand, tIntra, tBaseScan, tInstant, tFuelReserve, tAutoQ, tStorageTrigger, tMegaChunk, tJitter, tPackDeposit, tIntermediateShare;
        readonly int tMaxRuns, tMaxWarehouses;

        // catalog caches
        readonly int nRes;
        readonly int[] baseStack;
        readonly double[] val;
        readonly bool[] raw, isEnergy;
        readonly int[] energyOf;
        readonly int rFuel, rPlasma, rAnti, rScrap, rWood, rStone, rCore, rSteel;
        readonly Dictionary<string, int> strIx = new Dictionary<string, int>();
        readonly Dictionary<string, int> toolIx = new Dictionary<string, int>();
        readonly Dictionary<string, int> upgIx = new Dictionary<string, int>();
        readonly Dictionary<string, int> rsrIx = new Dictionary<string, int>();
        readonly int sLumber, sMine, sRefinery, sWarehouse, sFuelDepot, sCargoDepot, sLab, sRadar, sHub, sHangar, sAuto, sAlarm, sRelay;
        readonly int uPack, uEngine, uJump, uScanner, uTank, uCompression, uTurret, uTurretDmg, uPulse, uShield, uSalvage;
        readonly int[] uHull = new int[TravelModel.HazardCount];
        readonly int[] megaStr = new int[3];
        readonly int rsrFieldEngineering;
        readonly List<int>[] servers = new List<int>[7];
        readonly List<RecipeDef>[] recipesFor;
        readonly List<int>[] planetsWith;
        readonly int homePlanet, homeSystem;

        // game state
        double now, markClock;
        long[] pack, store;
        int[] toolTier, upgLevel;
        bool[] researched, craftedOnce, discovered, landed, visitedSys, knownSys;
        readonly List<Inst> insts = new List<Inst>();
        readonly MegaState[] mega = new MegaState[3];
        int maxTierLanded;
        double maxDistLy;
        readonly int[] landedByTier = new int[ScoreCalculator.WorldTierBucketCount];
        int landedCount;
        int wavesCalled;
        double xp;
        long lostUnits;
        int version = 1;
        readonly RouteTable[] routes = new RouteTable[12];

        public SimEngine(EconomyCatalog cat, Tuning tuning, SimWorld world, Archetype arch, SimSettings settings)
        {
            this.cat = cat;
            tun = tuning;
            this.world = world;
            this.arch = arch;
            cfg = settings ?? new SimSettings();
            travel = new TravelModel(tuning, cat);
            waveModel = new WaveEconomy(tuning);
            scoreFormula = ScoreFormula.From(tuning);
            rng = new SimRng(cfg.seed * 0x2545F4914F6CDD1DUL + Hash.Fnv1a64(arch.name));
            res = new SimResult { archetype = arch.name, seed = cfg.seed, horizonDays = cfg.horizonDays };

            tHarvest = tuning.D("sim.harvest_seconds");
            tOverhead = tuning.D("sim.harvest_overhead");
            tCraft = tuning.D("sim.craft_tap_seconds");
            tDeposit = tuning.D("sim.deposit_seconds");
            tBuy = tuning.D("sim.buy_seconds");
            tLand = tuning.D("sim.land_seconds");
            tIntra = tuning.D("sim.intra_hop_seconds");
            tBaseScan = tuning.D("sim.base_scanner_ly");
            tInstant = tuning.D("sim.instant_below_seconds");
            tFuelReserve = tuning.D("sim.fuel_reserve");
            tAutoQ = tuning.D("sim.auto_quality");
            tStorageTrigger = tuning.D("sim.storage_trigger");
            tMegaChunk = tuning.D("sim.mega_chunk_share");
            tJitter = tuning.D("sim.session_jitter_hours");
            tPackDeposit = tuning.D("sim.pack_deposit_share");
            tIntermediateShare = tuning.D("sim.intermediate_share");
            tMaxRuns = tuning.I("sim.max_runs_per_step");
            tMaxWarehouses = tuning.I("sim.max_warehouses");

            nRes = cat.resources.Length;
            baseStack = new int[nRes];
            val = new double[nRes];
            raw = new bool[nRes];
            isEnergy = new bool[nRes];
            energyOf = new int[nRes];
            for (int i = 0; i < nRes; i++)
            {
                ResourceDef r = cat.resources[i];
                baseStack[i] = r.stackSize;
                val[i] = r.baseValue;
                raw[i] = r.category == ResourceCategory.Raw;
                isEnergy[i] = r.fuelEnergy > 0;
                energyOf[i] = r.fuelEnergy;
            }
            rFuel = RI("fuel"); rPlasma = RI("plasma"); rAnti = RI("antimatter"); rScrap = RI("scrap"); rWood = RI("wood"); rStone = RI("stone"); rCore = RI("core"); rSteel = RI("steel");
            for (int i = 0; i < cat.structures.Length; i++) strIx[cat.structures[i].id] = i;
            for (int i = 0; i < cat.tools.Length; i++) toolIx[cat.tools[i].id] = i;
            for (int i = 0; i < cat.upgrades.Length; i++) upgIx[cat.upgrades[i].id] = i;
            for (int i = 0; i < cat.research.Length; i++) rsrIx[cat.research[i].id] = i;
            sLumber = SI("lumber"); sMine = SI("mine"); sRefinery = SI("refinery"); sWarehouse = SI("warehouse"); sFuelDepot = SI("fuel_depot"); sCargoDepot = SI("cargo_depot");
            sLab = SI("research_lab"); sRadar = SI("radar_tower"); sHub = SI("colony_hub"); sHangar = SI("hangar"); sAuto = SI("auto_harvester"); sAlarm = SI("alarm"); sRelay = SI("orbital_relay");
            uPack = UI("pack"); uEngine = UI("engine"); uJump = UI("jump"); uScanner = UI("scanner_range"); uTank = UI("fuel_tank"); uCompression = UI("compression");
            uTurret = UI("turret"); uTurretDmg = UI("turretdmg"); uPulse = UI("pulse"); uShield = UI("shield"); uSalvage = UI("salvage");
            for (int i = 0; i < TravelModel.HazardCount; i++) uHull[i] = UI(TravelModel.HullIds[i]);
            megaStr[0] = SI("terraformer"); megaStr[1] = SI("warp_gate"); megaStr[2] = SI("dyson_collector");
            rsrFieldEngineering = RsI("field_engineering");

            for (int m = 0; m < servers.Length; m++) servers[m] = new List<int>();
            for (int s = 0; s < cat.structures.Length; s++)
                for (int g = 0; g < cat.structures[s].gathers.Length; g++)
                    servers[(int)cat.structures[s].gathers[g]].Add(s);
            recipesFor = new List<RecipeDef>[nRes];
            for (int i = 0; i < nRes; i++) recipesFor[i] = cat.RecipesFor(cat.resources[i].id);

            planetsWith = new List<int>[nRes];
            for (int i = 0; i < nRes; i++) planetsWith[i] = new List<int>();
            for (int p = 0; p < world.planets.Length; p++)
            {
                SimPlanet sp = world.planets[p];
                for (int d = 0; d < sp.depRes.Length; d++) if (sp.depRes[d] >= 0) planetsWith[sp.depRes[d]].Add(p);
            }
            homePlanet = world.home;
            homeSystem = world.planets[homePlanet].system;

            pack = new long[nRes];
            store = new long[nRes];
            toolTier = new int[cat.tools.Length];
            for (int i = 0; i < toolTier.Length; i++) toolTier[i] = 1;
            upgLevel = new int[cat.upgrades.Length];
            researched = new bool[cat.research.Length];
            craftedOnce = new bool[nRes];
            discovered = new bool[nRes];
            landed = new bool[world.planets.Length];
            visitedSys = new bool[world.systemCount];
            knownSys = new bool[world.systemCount];
            for (int i = 0; i < 3; i++) mega[i] = new MegaState { filled = new long[cat.structures[megaStr[i]].levels[0].cost.Length], pre = new long[cat.structures[megaStr[i]].levels[1].cost.Length] };
            for (int i = 0; i < cat.start.startingItems.Length; i++)
            {
                ItemAmount it = cat.start.startingItems[i];
                int r = RI(it.resourceId);
                pack[r] += it.amount;
                discovered[r] = true;
            }
            Land(homePlanet);
            BuildLadder();
        }

        // ------------------------------------------------------------------------------------------------ lookups
        int RI(string id) { int i = cat.ResourceIndex(id); if (i < 0) throw new InvalidOperationException("sim: unknown resource " + id); return i; }

        int SI(string id) { int i; if (!strIx.TryGetValue(id, out i)) throw new InvalidOperationException("sim: unknown structure " + id); return i; }

        int UI(string id) { int i; if (!upgIx.TryGetValue(id, out i)) throw new InvalidOperationException("sim: unknown upgrade " + id); return i; }

        int RsI(string id) { int i; if (!rsrIx.TryGetValue(id, out i)) throw new InvalidOperationException("sim: unknown research " + id); return i; }

        int TI(string id) { int i; if (!toolIx.TryGetValue(id, out i)) throw new InvalidOperationException("sim: unknown tool ladder " + id); return i; }

        bool ResearchDone(string id) { return id == null || researched[RsI(id)]; }

        void Touch() { version++; }

        void Log(string s)
        {
            if (!cfg.keepLog || res.log.Count > 60000) return;
            res.log.Add(FormatClock(now) + " " + s);
        }

        static string FormatClock(double s)
        {
            double d = s / 86400.0;
            int day = (int)Math.Floor(d);
            double rem = s - day * 86400.0;
            int h = (int)(rem / 3600.0);
            int m = (int)((rem - h * 3600.0) / 60.0);
            int sec = (int)(rem - h * 3600.0 - m * 60.0);
            return "d" + day + " " + h.ToString("00") + ":" + m.ToString("00") + ":" + sec.ToString("00");
        }

        void Mark(string id) { res.Mark(id, markClock); }

        // ------------------------------------------------------------------------------------------------ inventory and capacity
        int StackOf(int r)
        {
            double m = UpgradeValue(uCompression, 1.0);
            int s = (int)Math.Floor(baseStack[r] * m + 1e-9);
            return s < 1 ? 1 : s;
        }

        double UpgradeValue(int upg, double baseValue)
        {
            int l = upgLevel[upg];
            return l <= 0 ? baseValue : cat.upgrades[upg].levels[l - 1].value;
        }

        int PackSlotsCap { get { return (int)UpgradeValue(uPack, cat.start.baseBackpackSlots); } }

        int TankSlots { get { return (int)UpgradeValue(uTank, 0.0); } }

        int StoreSlotsCap
        {
            get
            {
                int total = 0;
                for (int i = 0; i < insts.Count; i++)
                {
                    Inst n = insts[i];
                    if (n.planet != homePlanet || n.level <= 0) continue;
                    if (n.str == sWarehouse || n.str == sCargoDepot) total += cat.structures[n.str].levels[n.level - 1].capacity;
                }
                return total;
            }
        }

        int DepotEnergySlots
        {
            get
            {
                int total = 0;
                for (int i = 0; i < insts.Count; i++)
                {
                    Inst n = insts[i];
                    if (n.planet == homePlanet && n.str == sFuelDepot && n.level > 0) total += cat.structures[n.str].levels[n.level - 1].capacity;
                }
                return total;
            }
        }

        static int SlotsFor(long amount, int stack) { return amount <= 0 ? 0 : (int)((amount + stack - 1) / stack); }

        void Usage(long[] arr, out int mainSlots, out int energySlots)
        {
            mainSlots = 0;
            energySlots = 0;
            for (int r = 0; r < nRes; r++)
            {
                if (arr[r] <= 0) continue;
                int s = SlotsFor(arr[r], StackOf(r));
                if (isEnergy[r]) energySlots += s; else mainSlots += s;
            }
        }

        /// <summary>Units of resource r that still fit in a holder (pack or store).</summary>
        long Room(long[] arr, int mainCap, int energyCap, int r)
        {
            int mainSlots, energySlots;
            Usage(arr, out mainSlots, out energySlots);
            int st = StackOf(r);
            long partial = (long)SlotsFor(arr[r], st) * st - arr[r];
            int freeMain, freeEnergy;
            int overflow = Math.Max(0, energySlots - energyCap);
            freeMain = mainCap - mainSlots - overflow;
            freeEnergy = Math.Max(0, energyCap - energySlots);
            if (freeMain < 0) freeMain = 0;
            long slots = isEnergy[r] ? freeEnergy + freeMain : freeMain;
            return partial + slots * st;
        }

        long PackRoom(int r) { return Room(pack, PackSlotsCap, TankSlots, r); }

        long StoreRoom(int r) { return Room(store, StoreSlotsCap, DepotEnergySlots, r); }

        long PoolHave(int r) { return pack[r] + store[r]; }

        /// <summary>Moves what fits from the pack into the home storage. Returns how many stacks were moved (for the time cost).</summary>
        int Deposit()
        {
            int moved = 0;
            for (int r = 0; r < nRes; r++)
            {
                if (pack[r] <= 0) continue;
                long room = StoreRoom(r);
                long n = Math.Min(room, pack[r]);
                if (n <= 0) continue;
                pack[r] -= n;
                store[r] += n;
                moved++;
            }
            return moved;
        }

        void AddPool(int r, long qty)
        {
            if (qty <= 0) return;
            discovered[r] = true;
            long room = StoreRoom(r);
            long n = Math.Min(room, qty);
            store[r] += n;
            qty -= n;
            if (qty > 0)
            {
                room = PackRoom(r);
                n = Math.Min(room, qty);
                pack[r] += n;
                qty -= n;
            }
            lostUnits += qty;
        }

        /// <summary>Removes qty of r from the pool (store first, then pack); the caller checked availability.</summary>
        void SpendPool(int r, long qty)
        {
            long n = Math.Min(store[r], qty);
            store[r] -= n;
            qty -= n;
            if (qty > 0) pack[r] -= qty;
        }

        long EnergyInPool()
        {
            long e = 0;
            for (int r = 0; r < nRes; r++) if (isEnergy[r]) e += PoolHave(r) * energyOf[r];
            return e;
        }

        /// <summary>Pays an energy cost with the densest fuels first; false (nothing spent) when the pool cannot cover it.</summary>
        bool PayEnergy(int cost)
        {
            FuelPayment p = FuelPayment.Plan(cost, (int)Math.Min(int.MaxValue, PoolHave(rFuel)), (int)Math.Min(int.MaxValue, PoolHave(rPlasma)), (int)Math.Min(int.MaxValue, PoolHave(rAnti)));
            if (!p.ok) return false;
            SpendPool(rFuel, p.fuelCells);
            SpendPool(rPlasma, p.plasmaCells);
            SpendPool(rAnti, p.antimatterPods);
            if (p.overpay > 0) AddPool(rFuel, p.overpay);
            return true;
        }

        // ------------------------------------------------------------------------------------------------ structures, builders, effects
        Inst FindInst(int planet, int str, int no)
        {
            for (int i = 0; i < insts.Count; i++)
            {
                Inst n = insts[i];
                if (n.planet == planet && n.str == str && n.no == no) return n;
            }
            return null;
        }

        int BuiltLevel(int planet, int str, int no)
        {
            Inst n = FindInst(planet, str, no);
            return n == null ? 0 : n.level;
        }

        int InstanceCount(int planet, int str)
        {
            int c = 0;
            for (int i = 0; i < insts.Count; i++) if (insts[i].planet == planet && insts[i].str == str) c++;
            return c;
        }

        int HomeLevel(int str)
        {
            int best = 0;
            for (int i = 0; i < insts.Count; i++)
            {
                Inst n = insts[i];
                if (n.planet == homePlanet && n.str == str && n.level > best) best = n.level;
            }
            return best;
        }

        int LabLevel { get { return HomeLevel(sLab); } }

        int Builders()
        {
            int b = cat.start.builders + (researched[rsrFieldEngineering] ? 1 : 0);
            int hub = HomeLevel(sHub);
            if (hub > 0) b += (int)cat.structures[sHub].levels[hub - 1].effect;
            return b;
        }

        int BusyBuilders()
        {
            int c = 0;
            for (int i = 0; i < insts.Count; i++) if (insts[i].pending > 0) c++;
            return c;
        }

        double EffectOf(int str, int level) { return level <= 0 ? 0.0 : cat.structures[str].levels[level - 1].effect; }

        double GlobalYieldBonus() { return EffectOf(megaStr[0], mega[0].stage); }

        double TravelDiscount()
        {
            return EffectOf(sHangar, HomeLevel(sHangar)) + EffectOf(megaStr[1], mega[1].stage);
        }

        int LadderOf(HarvestMethod m)
        {
            switch (m)
            {
                case HarvestMethod.Chop:
                case HarvestMethod.Harvest: return TI("axe");
                case HarvestMethod.Mine: return TI("pick");
                case HarvestMethod.Drill: return TI("drill");
                case HarvestMethod.Siphon: return TI("siphon");
                case HarvestMethod.Excavate: return TI("scanner");
                default: return TI("claw");
            }
        }

        double ToolMult(HarvestMethod m)
        {
            int l = LadderOf(m);
            return cat.tools[l].tiers[toolTier[l] - 1].yieldMult;
        }

        double ToolRunMult(HarvestMethod m)
        {
            int l = LadderOf(m);
            return cat.tools[l].tiers[toolTier[l] - 1].runTimeMult;
        }

        /// <summary>The best yield bonus of the structures at a planet that serve the harvest method; -1 when none stands there (scrap salvage needs no structure).</summary>
        double AccessBonus(int planet, HarvestMethod m)
        {
            double best = -1.0;
            List<int> list = servers[(int)m];
            for (int k = 0; k < list.Count; k++)
            {
                for (int i = 0; i < insts.Count; i++)
                {
                    Inst n = insts[i];
                    if (n.planet != planet || n.str != list[k] || n.level <= 0) continue;
                    double e = EffectOf(n.str, n.level);
                    if (e > best) best = e;
                }
            }
            if (best < 0.0 && m == HarvestMethod.Salvage) return 0.0;
            return best;
        }

        // ------------------------------------------------------------------------------------------------ yields and harvest numbers
        double MeanQualityFactor { get { return 0.4 + 1.1 * arch.quality; } }

        double SampleQuality()
        {
            double q = arch.quality + arch.qualitySd * rng.Gauss();
            return q < 0.05 ? 0.05 : (q > 1.0 ? 1.0 : q);
        }

        double DepositYieldPerRun(SimPlanet sp, int dep, double qualityFactor)
        {
            HarvestMethod m = sp.depMethod[dep];
            double bonus = AccessBonus(sp.index, m);
            if (bonus < 0.0) bonus = 0.0;
            return sp.depBaseYield[dep] * (double)sp.depRichness[dep] * ToolMult(m) * (1.0 + bonus + GlobalYieldBonus()) * qualityFactor;
        }

        double RunSeconds(HarvestMethod m) { return tHarvest * ToolRunMult(m) + tOverhead; }

        // ------------------------------------------------------------------------------------------------ map: routes, knowledge, landing
        int JumpLevel { get { return upgLevel[uJump]; } }

        double ScannerLy
        {
            get
            {
                double s = Math.Max(tBaseScan, UpgradeValue(uScanner, 0.0));
                return s + EffectOf(sRadar, HomeLevel(sRadar));
            }
        }

        double SysDist(int a, int b)
        {
            double dx = world.sysX[a] - world.sysX[b], dy = world.sysY[a] - world.sysY[b];
            return Math.Sqrt(dx * dx + dy * dy);
        }

        RouteTable Routes(int level)
        {
            RouteTable t = routes[level];
            if (t != null) return t;
            int n = world.systemCount;
            t = new RouteTable { baseFuel = new double[n], lenSum = new double[n], secSum = new double[n], legs = new int[n], reachable = new bool[n] };
            double range = travel.JumpRangeLy(level);
            var done = new bool[n];
            for (int i = 0; i < n; i++) t.baseFuel[i] = double.MaxValue;
            t.baseFuel[homeSystem] = 0.0;
            t.reachable[homeSystem] = true;
            while (true)
            {
                int u = -1;
                double bu = double.MaxValue;
                for (int i = 0; i < n; i++) if (!done[i] && t.baseFuel[i] < bu) { bu = t.baseFuel[i]; u = i; }
                if (u < 0) break;
                done[u] = true;
                if (!(range > 0.0)) continue;
                for (int v = 0; v < n; v++)
                {
                    if (done[v]) continue;
                    double d = SysDist(u, v);
                    if (d > range || d <= 0.0) continue;
                    double w = bu + travel.LegBaseFuel(d);
                    if (w < t.baseFuel[v])
                    {
                        t.baseFuel[v] = w;
                        t.lenSum[v] = t.lenSum[u] + d;
                        t.secSum[v] = t.secSum[u] + travel.RunSeconds(d);
                        t.legs[v] = t.legs[u] + 1;
                        t.reachable[v] = true;
                    }
                }
            }
            routes[level] = t;
            return t;
        }

        bool IsReachable(SimPlanet sp)
        {
            if (sp.system == homeSystem) return true;
            return Routes(JumpLevel).reachable[sp.system];
        }

        bool IsKnown(SimPlanet sp) { return sp.system == homeSystem || knownSys[sp.system]; }

        void RecomputeKnown()
        {
            double r = ScannerLy;
            for (int s = 0; s < world.systemCount; s++)
            {
                if (knownSys[s]) continue;
                if (s == homeSystem) { knownSys[s] = true; continue; }
                for (int v = 0; v < world.systemCount; v++)
                    if (visitedSys[v] && SysDist(v, s) <= r) { knownSys[s] = true; break; }
            }
            Touch();
        }

        int PlatingLevel(int hazardIndex)
        {
            int l = upgLevel[uHull[hazardIndex]];
            return l <= 0 ? 0 : (int)cat.upgrades[uHull[hazardIndex]].levels[l - 1].value;
        }

        bool CanLand(SimPlanet sp)
        {
            int need = travel.RequiredPlatingLevel(sp.tier);
            for (int i = 0; i < TravelModel.HazardCount; i++)
                if ((sp.hazards & TravelModel.HazardOrder[i]) != 0 && PlatingLevel(i) < need) return false;
            return true;
        }

        /// <summary>Marks a world as landed on (first time: score, knowledge, reach).</summary>
        void Land(int planet)
        {
            SimPlanet sp = world.planets[planet];
            if (!landed[planet])
            {
                landed[planet] = true;
                landedCount++;
                landedByTier[Math.Min(sp.tier, landedByTier.Length - 1)]++;
                if (sp.tier > maxTierLanded) { maxTierLanded = sp.tier; Touch(); }
                if (sp.distHome > maxDistLy) maxDistLy = sp.distHome;
                // every first landing is visible progress: a new world, new deposits discovered, a bigger Worlds and Reach score
                res.progressPlay.Add(res.playSeconds);
                for (int d = 0; d < sp.depRes.Length; d++) if (sp.depRes[d] >= 0) discovered[sp.depRes[d]] = true;
            }
            if (!visitedSys[sp.system])
            {
                visitedSys[sp.system] = true;
                knownSys[sp.system] = true;
                RecomputeKnown();
            }
        }

        // ------------------------------------------------------------------------------------------------ score and samples
        ScoreInputs BuildScoreInputs()
        {
            var s = new ScoreInputs();
            s.maxDistanceFromHomeLy = maxDistLy;
            s.worldsLandedByTier = (int[])landedByTier.Clone();
            var lv = new List<int>();
            var tr = new List<int>();
            for (int i = 0; i < insts.Count; i++)
            {
                if (insts[i].level <= 0) continue;
                lv.Add(insts[i].level);
                tr.Add(cat.structures[insts[i].str].tier);
            }
            int stages = 0;
            for (int i = 0; i < 3; i++)
            {
                stages += mega[i].stage;
                if (mega[i].stage > 0) { lv.Add(mega[i].stage * 2); tr.Add(cat.structures[megaStr[i]].tier); }
            }
            s.structureLevels = lv.ToArray();
            s.structureTiers = tr.ToArray();
            int up = 0;
            for (int i = 0; i < upgLevel.Length; i++) up += upgLevel[i];
            s.upgradeLevelsTotal = up;
            int tp = 0;
            for (int i = 0; i < toolTier.Length; i++) tp += toolTier[i] - 1;
            s.toolTierPurchases = tp;
            if (wavesCalled > 0)
            {
                s.waveWorldDanger = new[] { world.planets[homePlanet].danger };
                s.waveWorldCleared = new[] { wavesCalled };
            }
            int disc = 0;
            for (int i = 0; i < nRes; i++) if (discovered[i]) disc++;
            s.resourcesDiscovered = disc;
            int rc = 0;
            for (int i = 0; i < nRes; i++) if (craftedOnce[i]) rc++;
            s.recipesCrafted = rc;
            s.megaprojectStages = stages;
            return s;
        }

        double StockValue(int category)
        {
            double v = 0.0;
            for (int r = 0; r < nRes; r++)
                if ((int)cat.resources[r].category == category) v += (store[r] + pack[r]) * val[r];
            return v;
        }

        void TakeSample()
        {
            var smp = new SimSample();
            smp.days = now / 86400.0;
            smp.playHours = res.playSeconds / 3600.0;
            smp.harvestedValue = res.harvestedValue;
            ScoreBreakdown b = ScoreCalculator.Compute(BuildScoreInputs(), scoreFormula);
            smp.score = b.total;
            smp.scoreParts[0] = b.reach; smp.scoreParts[1] = b.worlds; smp.scoreParts[2] = b.industry; smp.scoreParts[3] = b.tech; smp.scoreParts[4] = b.defense; smp.scoreParts[5] = b.atlas;
            smp.reachTier = maxTierLanded;
            int tp = 0;
            for (int i = 0; i < toolTier.Length; i++) tp += toolTier[i] - 1;
            smp.toolSteps = tp;
            int up = 0;
            for (int i = 0; i < upgLevel.Length; i++) up += upgLevel[i];
            smp.upgradeLevels = up;
            smp.builders = Builders();
            int sl = 0;
            for (int i = 0; i < insts.Count; i++) sl += insts[i].level;
            smp.structureLevels = sl;
            smp.era = EraIndex();
            double total = 0.0;
            for (int c = 0; c < 5; c++)
            {
                smp.categoryValue[c] = StockValue(c);
                total += smp.categoryValue[c];
            }
            smp.stockValue = total;
            smp.maxDistance = maxDistLy;
            smp.megaStages = mega[0].stage + mega[1].stage + mega[2].stage;
            smp.level = PlayerLevel();
            int dsc = 0, crf = 0;
            for (int i = 0; i < nRes; i++) { if (discovered[i]) dsc++; if (craftedOnce[i]) crf++; }
            smp.discovered = dsc;
            smp.crafted = crf;
            var kinds = new bool[cat.structures.Length];
            int kindCount = 0;
            for (int i = 0; i < insts.Count; i++) if (insts[i].level > 0 && !kinds[insts[i].str]) { kinds[insts[i].str] = true; kindCount++; }
            for (int i = 0; i < 3; i++) if (mega[i].stage > 0 && !kinds[megaStr[i]]) { kinds[megaStr[i]] = true; kindCount++; }
            smp.structureKinds = kindCount;
            smp.buildJobs = (int)res.buildJobs;
            res.samples.Add(smp);
        }

        int EraIndex()
        {
            int e = 0;
            if (res.milestones.ContainsKey("era_foothold")) e = 1;
            if (res.milestones.ContainsKey("era_settlement")) e = 2;
            if (res.milestones.ContainsKey("era_industry")) e = 3;
            if (res.milestones.ContainsKey("era_interstellar")) e = 4;
            if (res.milestones.ContainsKey("era_ascension")) e = 5;
            return e;
        }
    }
}
