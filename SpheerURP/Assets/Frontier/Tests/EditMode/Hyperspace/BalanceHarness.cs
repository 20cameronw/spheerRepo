using System;
using System.Threading.Tasks;
using Frontier.Universe;

namespace Frontier.Hyperspace.Tests
{
    public struct RunSummary
    {
        public bool survived;
        public bool reachedEnd;
        public int hits;
        public int hullHits;
        public int kills;
        public int killable;
        public Grade grade;
        public float quality;
        public int steps;
        public bool hadBoss;
        public bool bossDefeated;
        public int bombs;
        public float maxEnemies;
        public int spawnFailures;
    }

    public sealed class ScenarioStats
    {
        public int runs;
        public int survived;
        public double hits;
        public double hullHits;
        public double kills;
        public double killRatio;
        public double quality;
        public int[] grades = new int[4];
        public int bossRuns;
        public int bossKills;
        public double seconds;

        public double Survival { get { return runs == 0 ? 0 : (double)survived / runs; } }
        public double HitsPerRun { get { return runs == 0 ? 0 : hits / runs; } }
        public double Share(Grade g) { return runs == 0 ? 0 : (double)grades[(int)g] / runs; }
        public double GoodShare { get { return runs == 0 ? 0 : (double)(grades[(int)Grade.S] + grades[(int)Grade.A]) / runs; } }
        public double BPlusShare { get { return runs == 0 ? 0 : (double)(grades[(int)Grade.S] + grades[(int)Grade.A] + grades[(int)Grade.B]) / runs; } }
    }

    /// <summary>Plays bots through seeded runs in parallel and aggregates survival, hits, kills and grades.</summary>
    public static class Balance
    {
        public static float ShipPowerAt(HyperspaceTuning t, float progress) { return t.ShipPower(ShipStats.AtProgress(progress)); }

        /// <summary>Ship progress (0 = starter, 1 = endgame) whose power rating is closest to the given power.</summary>
        public static float ProgressForPower(HyperspaceTuning t, float power)
        {
            float lo = 0f, hi = 1f;
            if (power <= 1f) return 0f;
            if (power >= ShipPowerAt(t, 1f)) return 1f;
            for (int i = 0; i < 24; i++)
            {
                float mid = 0.5f * (lo + hi);
                if (ShipPowerAt(t, mid) < power) lo = mid; else hi = mid;
            }
            return 0.5f * (lo + hi);
        }

        public static float DangerFor(int tier) { return 0.6f + 1.1f * tier; }

        public static RunSummary Play(HyperspaceTuning t, EncounterLibrary lib, RunParams p, ShipStats ship, BotSkill skill, bool reviveAlways, long[] hitSources = null)
        {
            HyperspaceRun run = HyperspaceRun.Create(p, ship, t, lib);
            var bot = new Bot(skill, t, p.seed);
            var s = new RunSummary { hadBoss = run.Route.hasBoss };
            bool downed = false;
            int maxEnemies = 0;
            var buf = hitSources != null ? new HEvent[EventRing.Capacity] : null;
            while (run.State != RunState.Ended)
            {
                if (run.State == RunState.Downed)
                {
                    downed = true;
                    if (reviveAlways && run.CanRevive) run.Revive();
                    else
                    {
                        run.Abandon();
                        break;
                    }
                }
                PlayerInput inp = bot.Decide(run);
                run.Step(inp);
                if (run.EnemyCount > maxEnemies) maxEnemies = run.EnemyCount;
                if (buf != null)
                {
                    int n = run.Events.Drain(buf);
                    for (int i = 0; i < n; i++)
                        if (buf[i].type == EventType.PlayerHit || buf[i].type == EventType.ShieldBreak) System.Threading.Interlocked.Increment(ref hitSources[buf[i].a & 15]);
                }
                else run.Events.Clear();
            }
            RunResult r = run.BuildResult();
            s.survived = !downed;
            s.reachedEnd = r.reachedEnd;
            s.hits = r.hitsTaken;
            s.hullHits = run.HullHits;
            s.kills = r.killCount;
            s.killable = run.KillableSpawned;
            s.grade = r.grade;
            s.quality = r.quality;
            s.steps = run.StepIndex;
            s.bossDefeated = r.bossDefeated;
            s.bombs = bot.BombsUsed;
            s.maxEnemies = maxEnemies;
            s.spawnFailures = run.SpawnFailures;
            return s;
        }

        public static ScenarioStats RunScenario(HyperspaceTuning t, EncounterLibrary lib, float dist, int tier, float danger, float shipProgress, BotSkill skill, int runs, ulong seedBase, long[] hitSources = null)
        {
            var results = new RunSummary[runs];
            ShipStats ship = ShipStats.AtProgress(shipProgress);
            float power = t.ShipPower(ship);
            Parallel.For(0, runs, i =>
            {
                ulong seed = Hash.Combine(seedBase, (ulong)i, (ulong)(dist * 10f), (ulong)tier);
                var p = new RunParams(seed, dist, danger, tier, power, RunMode.Normal);
                results[i] = Play(t, lib, p, ship, skill, false, hitSources);
            });
            var st = new ScenarioStats { runs = runs };
            for (int i = 0; i < runs; i++)
            {
                RunSummary r = results[i];
                if (r.survived) st.survived++;
                st.hits += r.hits;
                st.hullHits += r.hullHits;
                st.kills += r.kills;
                st.killRatio += r.killable > 0 ? (double)r.kills / r.killable : 1.0;
                st.quality += r.quality;
                if (r.survived) st.grades[(int)r.grade]++;
                else st.grades[(int)Grade.C]++;
                if (r.hadBoss)
                {
                    st.bossRuns++;
                    if (r.bossDefeated) st.bossKills++;
                }
                st.seconds += r.steps / 60.0;
            }
            return st;
        }
    }
}
