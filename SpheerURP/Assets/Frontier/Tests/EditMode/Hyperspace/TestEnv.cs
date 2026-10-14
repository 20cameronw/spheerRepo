using System;

namespace Frontier.Hyperspace.Tests
{
    /// <summary>Shared fixtures: one immutable library and default tuning, plus a few scan helpers used by several fixtures.</summary>
    public static class TestEnv
    {
        public static readonly EncounterLibrary Lib = EncounterLibrary.CreateDefault();

        public static HyperspaceTuning Tuning() { return new HyperspaceTuning(); }

        public static bool IsMono { get { return Type.GetType("Mono.Runtime") != null; } }

        public static bool Finite(float v) { return !float.IsNaN(v) && !float.IsInfinity(v); }

        /// <summary>First non-finite value found in the public state, or null.</summary>
        public static string FindNonFinite(HyperspaceRun run)
        {
            ref PlayerState p = ref run.Player;
            if (!Finite(p.x) || !Finite(p.y) || !Finite(p.vx) || !Finite(p.vy) || !Finite(p.hull)) return "player";
            if (!Finite(run.Speed) || !Finite(run.Scroll)) return "speed/scroll";
            for (int i = 0; i < run.EnemyHigh; i++)
            {
                ref Enemy e = ref run.Enemies[i];
                if (!e.active) continue;
                if (!Finite(e.x) || !Finite(e.y) || !Finite(e.z) || !Finite(e.vx) || !Finite(e.vy) || !Finite(e.hp) || !Finite(e.shield) || !Finite(e.aimX) || !Finite(e.aimY)) return "enemy " + e.type;
            }
            for (int i = 0; i < run.BulletHigh; i++)
            {
                ref Bullet b = ref run.Bullets[i];
                if (b.active && (!Finite(b.x) || !Finite(b.y) || !Finite(b.z) || !Finite(b.vx) || !Finite(b.vy))) return "bullet";
            }
            for (int i = 0; i < run.BoltHigh; i++)
            {
                ref Bolt b = ref run.Bolts[i];
                if (b.active && (!Finite(b.x) || !Finite(b.y) || !Finite(b.z) || !Finite(b.vx) || !Finite(b.vy) || !Finite(b.vz))) return "bolt";
            }
            for (int i = 0; i < run.PickupHigh; i++)
            {
                ref Pickup k = ref run.Pickups[i];
                if (k.active && (!Finite(k.x) || !Finite(k.y) || !Finite(k.z))) return "pickup";
            }
            return null;
        }

        /// <summary>Runs a bot-driven route to the end (reviving as often as allowed) and returns the finished run.</summary>
        public static HyperspaceRun PlayOut(RunParams p, ShipStats ship, BotSkill skill, HyperspaceTuning t, bool revive = true)
        {
            HyperspaceRun run = HyperspaceRun.Create(p, ship, t, Lib);
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
                run.Events.Clear();
            }
            return run;
        }

        public static RunParams Route(ulong seed, float ly, int tier, float power, RunMode mode = RunMode.Normal)
        {
            return new RunParams(seed, ly, (float)(0.6 + 1.1 * tier), tier, power, mode);
        }
    }
}
