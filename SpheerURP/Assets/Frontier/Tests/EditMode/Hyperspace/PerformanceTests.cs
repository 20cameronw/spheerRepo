using System;
using System.Diagnostics;
using NUnit.Framework;

namespace Frontier.Hyperspace.Tests
{
    [TestFixture]
    public class PerformanceTests
    {
        static PlayerInput Script(int step)
        {
            return new PlayerInput((float)(2.3 * Math.Sin(0.05 * step)), (float)(2.6 * Math.Cos(0.037 * step)), step % 500 == 499);
        }

        static long Allocated() { return GC.GetAllocatedBytesForCurrentThread(); }

        /// <summary>A deep route flown with the maximum ship: dense schedule, bullets, bolts, pickups and the Guardian.</summary>
        static HyperspaceRun Heavy(HyperspaceTuning t)
        {
            ShipStats ship = ShipStats.Endgame();
            return HyperspaceRun.Create(TestEnv.Route(99UL, 3000f, 12, t.ShipPower(ship), RunMode.Challenge), ship, t, TestEnv.Lib);
        }

        static void FlyHeavy(HyperspaceRun run, int steps)
        {
            for (int i = 0; i < steps && run.State != RunState.Ended; i++)
            {
                if (run.State == RunState.Downed) { if (run.CanRevive) run.Revive(); else break; }
                run.Step(Script(run.StepIndex));
                run.Events.Clear();
            }
        }

        [Test]
        public void StepAllocatesNothingInSteadyState()
        {
            long probe0 = Allocated();
            var junk = new byte[4096];
            if (Allocated() - probe0 < 4096 || junk.Length == 0) Assert.Ignore("GC.GetAllocatedBytesForCurrentThread is not supported on this runtime");
            HyperspaceTuning t = TestEnv.Tuning();
            FlyHeavy(Heavy(t), 6000);
            HyperspaceRun run = Heavy(t);
            FlyHeavy(run, 300);
            long before = Allocated();
            int steps = 0;
            while (run.State != RunState.Ended && steps < 4500)
            {
                if (run.State == RunState.Downed) { if (run.CanRevive) run.Revive(); else break; }
                run.Step(Script(run.StepIndex));
                run.Events.Clear();
                steps++;
            }
            long bytes = Allocated() - before;
            Assert.Greater(steps, 3000, "the measurement must cover a long stretch of the run");
            Assert.AreEqual(0, bytes, "Step allocated " + bytes + " bytes over " + steps + " steps");
            Console.WriteLine("    steady state: " + steps + " steps, " + bytes + " bytes allocated");
        }

        [Test]
        public void EventDrainAndStateReadsDoNotAllocate()
        {
            long probe0 = Allocated();
            var junk = new byte[4096];
            if (Allocated() - probe0 < 4096 || junk.Length == 0) Assert.Ignore("GC.GetAllocatedBytesForCurrentThread is not supported on this runtime");
            HyperspaceTuning t = TestEnv.Tuning();
            HyperspaceRun run = Heavy(t);
            var buf = new HEvent[EventRing.Capacity];
            long sum = 0;
            for (int i = 0; i < 900 && run.State == RunState.Running; i++)
            {
                run.Step(Script(run.StepIndex));
                sum += run.Events.Drain(buf);
                for (int k = 0; k < run.EnemyHigh; k++) if (run.Enemies[k].active) sum += (long)run.Enemies[k].z;
                sum += (long)run.StateDigest();
            }
            long before = Allocated();
            for (int i = 0; i < 1200 && run.State == RunState.Running; i++)
            {
                run.Step(Script(run.StepIndex));
                int n = run.Events.Drain(buf);
                sum += n;
                for (int k = 0; k < run.EnemyHigh; k++) if (run.Enemies[k].active) sum += (long)run.Enemies[k].z;
                sum += (long)run.StateDigest();
            }
            long bytes = Allocated() - before;
            Assert.AreEqual(0, bytes, "draining events, reading the pools and hashing the state must not allocate (" + sum + ")");
        }

        [Test]
        public void StressAtTheEntityCapStaysFastAndBounded()
        {
            HyperspaceTuning t = TestEnv.Tuning();
            ShipStats ship = Scenarios.Pacifist(1000000f, 5);
            ship.fireRate = 150f;
            ship.pierce = 3;
            HyperspaceRun run = Scenarios.MakeRun(t, ship, 12, 40f, route =>
            {
                var list = new System.Collections.Generic.List<SpawnRecord>();
                for (int i = 0; i < t.maxEnemies + 24; i++)
                {
                    double x = -2.6 + 5.2 * ((i * 37) % 101) / 100.0, y = -3.2 + 6.4 * ((i * 53) % 97) / 96.0;
                    if ((i % 7) == 3) { list.Add(MechanicsTests.TurretRec(route, t, (float)x, (float)y, 330 + (i % 12))); continue; }
                    EntityType type = (i % 5) == 1 ? EntityType.Mine : EntityType.Drone;
                    list.Add(Scenarios.Rec(route, t, type, (float)x, (float)y, 330 + (i % 12), PathType.Static));
                }
                return list.ToArray();
            });
            int peak = 0, peakBullets = 0, peakBolts = 0;
            double worst = 0, total = 0;
            int measured = 0;
            for (int i = 0; i < 900 && run.State != RunState.Ended; i++)
            {
                if (run.State == RunState.Downed) { run.Revive(); }
                long a = Stopwatch.GetTimestamp();
                run.Step(new PlayerInput((float)(2.0 * Math.Sin(0.04 * i)), (float)(2.5 * Math.Cos(0.03 * i)), false));
                double ms = (Stopwatch.GetTimestamp() - a) * 1000.0 / Stopwatch.Frequency;
                run.Events.Clear();
                if (run.EnemyCount > peak) peak = run.EnemyCount;
                if (run.ActiveBullets > peakBullets) peakBullets = run.ActiveBullets;
                if (run.ActiveBolts > peakBolts) peakBolts = run.ActiveBolts;
                if (i > 120) { total += ms; measured++; if (ms > worst) worst = ms; }
            }
            double avg = total / Math.Max(1, measured);
            Console.WriteLine("    entity cap stress: peak enemies " + peak + "/" + t.maxEnemies + ", bullets " + peakBullets + "/" + t.maxPlayerBullets + ", bolts " + peakBolts + "/" + t.maxEnemyBolts + ", avg " + avg.ToString("0.000") + " ms/step, worst " + worst.ToString("0.000") + " ms/step on " + (TestEnv.IsMono ? "Mono" : "CoreCLR"));
            Assert.GreaterOrEqual(peak, t.maxEnemies - 8, "the stress schedule must fill the enemy pool");
            Assert.GreaterOrEqual(peakBullets, 60, "and most of the bullet pool (one bullet per step is the maximum fire rate)");
            Assert.LessOrEqual(run.EnemyCount, t.maxEnemies);
            Assert.LessOrEqual(run.ActiveBullets, t.maxPlayerBullets);
            Assert.Less(avg, 1.5, "a step with full pools must stay far below the 16.6 ms frame budget (phones are ~5x slower than this machine)");
        }

        [Test]
        public void TypicalStepCost()
        {
            HyperspaceTuning t = TestEnv.Tuning();
            ShipStats ship = ShipStats.AtProgress(0.5f);
            HyperspaceRun run = HyperspaceRun.Create(TestEnv.Route(12UL, 200f, 6, t.ShipPower(ship)), ship, t, TestEnv.Lib);
            var bot = new Bot(BotSkill.Average(), t, 12UL);
            long ticks = 0;
            int steps = 0;
            while (run.State != RunState.Ended)
            {
                if (run.State == RunState.Downed) { if (run.CanRevive) run.Revive(); else break; }
                PlayerInput inp = bot.Decide(run);
                long a = Stopwatch.GetTimestamp();
                run.Step(inp);
                ticks += Stopwatch.GetTimestamp() - a;
                run.Events.Clear();
                steps++;
            }
            double avg = ticks * 1000.0 / Stopwatch.Frequency / steps;
            Console.WriteLine("    typical tier 6 run: " + steps + " steps, avg " + avg.ToString("0.0000") + " ms/step on " + (TestEnv.IsMono ? "Mono" : "CoreCLR"));
            Assert.Less(avg, 0.5);
        }
    }
}
