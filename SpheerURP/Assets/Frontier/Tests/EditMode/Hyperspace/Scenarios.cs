using System;
using System.Collections.Generic;
using Frontier.Universe;

namespace Frontier.Hyperspace.Tests
{
    /// <summary>Shared helpers: canonical scenarios with stable digests, tiny hand-made schedules for unit tests.</summary>
    public static class Scenarios
    {
        public sealed class Outcome
        {
            public ulong schedule;
            public ulong events;
            public ulong state;
            public ulong result;
            public int steps;
            public int kills;
            public int hits;
            public Grade grade;
            public float quality;
        }

        public static Outcome Play(RunParams p, ShipStats ship, BotSkill skill, EncounterLibrary lib, HyperspaceTuning t, bool revive)
        {
            HyperspaceRun run = HyperspaceRun.Create(p, ship, t, lib);
            var bot = new Bot(skill, t, p.seed);
            while (run.State != RunState.Ended)
            {
                if (run.State == RunState.Downed)
                {
                    if (revive && run.CanRevive) run.Revive();
                    else
                    {
                        run.Abandon();
                        break;
                    }
                }
                run.Step(bot.Decide(run));
            }
            RunResult r = run.BuildResult();
            return new Outcome { schedule = run.Plan.Digest(), events = run.Events.Digest, state = run.StateDigest(), result = r.digest, steps = run.StepIndex, kills = r.killCount, hits = r.hitsTaken, grade = r.grade, quality = r.quality };
        }

        /// <summary>The three golden scenarios: a gentle first hop, a long deep route with the Guardian, and a tier-12 stress run.</summary>
        public static Outcome[] Golden(EncounterLibrary lib, HyperspaceTuning t)
        {
            var a = Play(new RunParams(1UL, 5f, 0.6f, 0, 1f, RunMode.Normal), ShipStats.Starter(), BotSkill.Average(), lib, t, false);
            ShipStats mid = ShipStats.AtProgress(0.5f);
            var b = Play(new RunParams(2UL, 800f, 7.2f, 6, t.ShipPower(mid), RunMode.Normal), mid, BotSkill.Expert(), lib, t, false);
            ShipStats end = ShipStats.Endgame();
            var c = Play(new RunParams(3UL, 3000f, 13.8f, 12, t.ShipPower(end), RunMode.Challenge), end, BotSkill.Novice(), lib, t, true);
            return new[] { a, b, c };
        }

        // ---------------------------------------------------------------- hand-made schedules
        public static SpawnRecord Rec(RouteSpec route, HyperspaceTuning t, EntityType type, float x, float y, int arrivalStep, PathType path = PathType.Static, float vzOwn = 0f)
        {
            var r = new SpawnRecord { type = type, x = x, y = y, arrivalStep = arrivalStep, path = path, vzOwn = vzOwn };
            EncounterInstantiator.SpawnStep(ref r, route, t);
            return r;
        }

        public static HyperspaceRun MakeRun(HyperspaceTuning t, ShipStats ship, int tier, float seconds, Func<RouteSpec, SpawnRecord[]> build)
        {
            RouteSpec route = RouteSpec.ForAnalysis(t, tier, 30f, seconds, false);
            SpawnRecord[] recs = build(route);
            for (int i = 1; i < recs.Length; i++)
            {
                SpawnRecord key = recs[i];
                int j = i - 1;
                while (j >= 0 && recs[j].step > key.step)
                {
                    recs[j + 1] = recs[j];
                    j--;
                }
                recs[j + 1] = key;
            }
            var plan = new Schedule { records = recs, encounters = new PlannedEncounter[0] };
            return new HyperspaceRun(route, plan, ship, t);
        }

        /// <summary>A ship that deals no damage, so a scripted enemy always reaches it.</summary>
        public static ShipStats Pacifist(float hull, int shield)
        {
            ShipStats s = ShipStats.Starter();
            s.hull = hull;
            s.shield = shield;
            s.damage = 0.001f;
            return s;
        }

        public static void RunSteps(HyperspaceRun run, int steps, PlayerInput input)
        {
            for (int i = 0; i < steps && run.State == RunState.Running; i++) run.Step(input);
        }

        public static int Count(HyperspaceRun run, EventType type)
        {
            int n = 0;
            for (int i = 0; i < run.Events.Count; i++) if (run.Events.Peek(i).type == type) n++;
            return n;
        }

        public static string Hex(ulong v) { return "0x" + v.ToString("X16"); }
    }
}
