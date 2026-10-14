using System.Collections.Generic;

namespace Frontier.Hyperspace
{
    public struct SolveResult
    {
        /// <summary>A collision-free path exists from every starting position (the reachable set never empties).</summary>
        public bool solvable;
        /// <summary>Lock windows overlap, which the solver does not model: separate the templates' aimed threats in time.</summary>
        public bool unsupported;
        /// <summary>Smallest tested fraction of the fair ship speed with a surviving path (1.01 when even full speed fails).</summary>
        public float minSpeedFraction;
        /// <summary>Seconds relative to the template anchor at which the last survivor died (only when not solvable).</summary>
        public float failTime;
        public string failSources;
        public int hazardRecords;
        public int lockGroups;
        public int survivors;
    }

    /// <summary>
    /// The perfect solver: proves that an encounter template can be flown without damage. It replays the template through the real simulation with a ghost ship (nothing is shot, nothing hits),
    /// records every hazard shape that crosses the ship plane and every aimed threat, then runs a breadth-first reachability search over a lattice of ship positions, one layer per
    /// simulation step (z-slab). Rules of the contract it checks:
    /// (1) the ship may start anywhere entryLead seconds before the first activity; (2) it moves at most fairSpeed x speedFraction (a 3x3 lattice kernel whose cell is exactly one step of travel
    /// divided by sqrt 2); (3) it keeps ship radius + fairnessMargin + half a cell away from every hazard; (4) aimed threats (turret bolts, seeker lock) freeze the ship for reactionBudget
    /// after the lock and must then be escaped: the position at arrival must clear the position at the lock plus the bolt's offset by the bolt radius, evaluated for every possible lock position.
    /// Nothing is assumed about shooting, so killing enemies can only make a template easier.
    /// </summary>
    public sealed partial class FairnessSolver
    {
        readonly HyperspaceTuning t;
        readonly float fairSpeed;
        readonly float cell;
        readonly int cols, rows;
        readonly ulong colMask;
        float reff;
        readonly int reactSteps;

        public FairnessSolver(HyperspaceTuning tuning)
        {
            t = tuning;
            fairSpeed = tuning.FairSpeed;
            cell = (float)((double)fairSpeed * HyperspaceTuning.Dt / 1.41421356);
            cols = (int)(2.0 * (double)tuning.areaHalfWidth / cell) + 1;
            rows = (int)(2.0 * (double)tuning.areaHalfHeight / cell) + 1;
            colMask = cols >= 64 ? ~0UL : ((1UL << cols) - 1UL);
            SetTier(0);
            reactSteps = (int)((double)tuning.reactionBudget * HyperspaceTuning.StepsPerSecond + 0.5);
            Alloc();
        }

        /// <summary>The clearance the proof keeps around hazards depends on the tier (the precision demanded of the player grows with depth).</summary>
        void SetTier(int tier)
        {
            reff = (float)((double)t.playerRadius + t.FairnessMarginFor(tier) + 0.5 * (double)cell);
        }

        public int Cols { get { return cols; } }
        public int Rows { get { return rows; } }
        public float CellSize { get { return cell; } }

        // ---------------------------------------------------------------- recording
        struct Recording
        {
            public HazardLog log;
            public int anchorStep;
            public int firstStep;
            public int lastStep;
        }

        Recording RecordTemplate(EncounterTemplate tp, int tier, bool mirror, float routeSpeed)
        {
            SetTier(tier);
            double lead = (double)t.spawnDistance / routeSpeed;
            double ts = t.TimeScaleFor(tier);
            double anchorTime = lead + 1.5 + tp.PreSecondsAt((float)ts);
            double seconds = anchorTime + tp.SpanSecondsAt((float)ts) + 8.0;
            RouteSpec route = RouteSpec.ForAnalysis(t, tier, routeSpeed, (float)seconds, false);
            int anchorStep = (int)(anchorTime * HyperspaceTuning.StepsPerSecond + 0.5);
            var recs = new List<SpawnRecord>();
            EncounterInstantiator.Instantiate(tp, mirror, anchorStep, route, t, 0, recs);
            SpawnRecord[] arr = SortedRecords(recs);
            var plan = new Schedule { records = arr, encounters = new PlannedEncounter[0] };
            return RunGhost(route, plan, anchorStep, 0, 0);
        }

        Recording RunGhost(RouteSpec route, Schedule plan, int anchorStep, int bossPhase, int stopStep)
        {
            var log = new HazardLog();
            var run = new HyperspaceRun(route, plan, ShipStats.Starter(), t);
            run.Ghost = true;
            run.Log = log;
            run.BossPhaseLock = bossPhase;
            var input = new PlayerInput(0f, 0f, false);
            int last = stopStep > 0 ? stopStep : route.totalSteps - 60;
            while (run.StepIndex < last && run.State == RunState.Running) run.Step(input);
            int first = int.MaxValue, end = 0;
            for (int i = 0; i < log.hazardCount; i++)
            {
                int s = log.hazards[i].step;
                if (s < first) first = s;
                if (s > end) end = s;
            }
            for (int i = 0; i < log.lockCount; i++)
            {
                if (log.locks[i].lockStep < first) first = log.locks[i].lockStep;
                if (log.locks[i].arriveStep + 3 > end) end = log.locks[i].arriveStep + 3;
            }
            if (first == int.MaxValue) first = anchorStep;
            if (end < first + 1) end = first + 1;
            return new Recording { log = log, anchorStep = anchorStep, firstStep = first, lastStep = end };
        }

        static SpawnRecord[] SortedRecords(List<SpawnRecord> recs)
        {
            SpawnRecord[] arr = recs.ToArray();
            for (int i = 1; i < arr.Length; i++)
            {
                SpawnRecord key = arr[i];
                int j = i - 1;
                while (j >= 0 && arr[j].step > key.step)
                {
                    arr[j + 1] = arr[j];
                    j--;
                }
                arr[j + 1] = key;
            }
            return arr;
        }

        // ---------------------------------------------------------------- public API
        /// <summary>Solves one template instance at a route speed (u/s) with the ship limited to speedFraction of the fair speed.</summary>
        public SolveResult SolveTemplate(EncounterTemplate tp, int tier, bool mirror, float routeSpeed, float speedFraction)
        {
            Recording rec = RecordTemplate(tp, tier, mirror, routeSpeed);
            return Solve(rec, speedFraction);
        }

        /// <summary>Smallest speed fraction (steps of 0.1 from 0.3) at which the template is solvable; fills minSpeedFraction of the returned result.</summary>
        public SolveResult MeasureTemplate(EncounterTemplate tp, int tier, bool mirror, float routeSpeed)
        {
            Recording rec = RecordTemplate(tp, tier, mirror, routeSpeed);
            return Measure(rec);
        }

        /// <summary>Solves one phase of the Guardian's attack script for its whole possible duration (bossMaxSeconds).</summary>
        public SolveResult SolveBossPhase(int phase, int tier, float routeSpeed, float speedFraction)
        {
            Recording rec = RecordBoss(phase, tier, routeSpeed);
            return Solve(rec, speedFraction);
        }

        public SolveResult MeasureBossPhase(int phase, int tier, float routeSpeed)
        {
            Recording rec = RecordBoss(phase, tier, routeSpeed);
            return Measure(rec);
        }

        Recording RecordBoss(int phase, int tier, float routeSpeed)
        {
            SetTier(tier);
            double seconds = 2.0 + (double)t.bossArriveSeconds + t.bossMaxSeconds + 8.0;
            RouteSpec route = RouteSpec.ForAnalysis(t, tier, routeSpeed, (float)seconds, true);
            int bossStep = (int)((double)route.bossStart * HyperspaceTuning.StepsPerSecond + 0.5);
            var rec = new SpawnRecord
            {
                step = bossStep, arrivalStep = bossStep + (int)((double)t.bossArriveSeconds * HyperspaceTuning.StepsPerSecond + 0.5), type = EntityType.Guardian,
                x = 0f, y = 0.6f, z0 = t.spawnDistance, path = PathType.Hold
            };
            var plan = new Schedule { records = new[] { rec }, encounters = new PlannedEncounter[0] };
            int fightStart = rec.arrivalStep;
            int stop = fightStart + (int)(((double)t.bossMaxSeconds + 1.0) * HyperspaceTuning.StepsPerSecond);
            Recording r = RunGhost(route, plan, fightStart, phase, stop);
            return r;
        }

        SolveResult Measure(Recording rec)
        {
            SolveResult best = Solve(rec, 1f);
            if (!best.solvable)
            {
                best.minSpeedFraction = 1.01f;
                return best;
            }
            for (int k = 3; k <= 10; k++)
            {
                SolveResult r = Solve(rec, (float)((double)k / 10.0));
                if (r.solvable)
                {
                    r.minSpeedFraction = (float)((double)k / 10.0);
                    return r;
                }
            }
            best.minSpeedFraction = 1f;
            return best;
        }
    }
}
