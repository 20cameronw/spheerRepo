using System;
using NUnit.Framework;

namespace Frontier.Hyperspace.Tests
{
    [TestFixture]
    public class DeterminismTests
    {
        static PlayerInput Script(int step)
        {
            double x = 2.4 * Math.Sin(0.043 * step) + 0.6 * Math.Sin(0.31 * step);
            double y = 2.8 * Math.Cos(0.029 * step);
            return new PlayerInput((float)x, (float)y, step % 700 == 699);
        }

        static HyperspaceRun Start(ulong seed, float ly, int tier, ShipStats ship)
        {
            HyperspaceTuning t = TestEnv.Tuning();
            return HyperspaceRun.Create(TestEnv.Route(seed, ly, tier, t.ShipPower(ship)), ship, t, TestEnv.Lib);
        }

        static void Fly(HyperspaceRun run, bool drain)
        {
            var buf = new HEvent[EventRing.Capacity];
            while (run.State != RunState.Ended)
            {
                if (run.State == RunState.Downed)
                {
                    if (run.CanRevive) run.Revive();
                    else run.Abandon();
                    continue;
                }
                run.Step(Script(run.StepIndex));
                if (drain) run.Events.Drain(buf);
            }
        }

        [Test]
        public void SameParamsAndInputGiveIdenticalEventStreamAndResult()
        {
            for (int tier = 0; tier <= 12; tier += 4)
            {
                ShipStats ship = ShipStats.AtProgress(tier / 12f);
                HyperspaceRun a = Start(11UL + (ulong)tier, 120f, tier, ship), b = Start(11UL + (ulong)tier, 120f, tier, ship);
                Fly(a, true);
                Fly(b, true);
                Assert.AreEqual(a.Plan.Digest(), b.Plan.Digest(), "schedule tier " + tier);
                Assert.AreEqual(a.Events.Digest, b.Events.Digest, "events tier " + tier);
                Assert.AreEqual(a.StateDigest(), b.StateDigest(), "state tier " + tier);
                Assert.AreEqual(a.BuildResult().digest, b.BuildResult().digest, "result tier " + tier);
                Assert.AreEqual(a.Events.TotalPushed, b.Events.TotalPushed);
            }
        }

        [Test]
        public void ResetReplaysTheSameRun()
        {
            HyperspaceRun run = Start(5UL, 300f, 5, ShipStats.AtProgress(0.4f));
            Fly(run, true);
            ulong events = run.Events.Digest, state = run.StateDigest();
            int pushed = run.Events.TotalPushed;
            run.Reset();
            Assert.AreEqual(RunState.Running, run.State);
            Assert.AreEqual(0, run.StepIndex);
            Fly(run, true);
            Assert.AreEqual(events, run.Events.Digest);
            Assert.AreEqual(state, run.StateDigest());
            Assert.AreEqual(pushed, run.Events.TotalPushed);
        }

        [Test]
        public void DigestDoesNotDependOnHowOftenTheHostDrainsEvents()
        {
            HyperspaceRun a = Start(9UL, 200f, 7, ShipStats.AtProgress(0.6f)), b = Start(9UL, 200f, 7, ShipStats.AtProgress(0.6f));
            Fly(a, true);
            Fly(b, false);
            Assert.AreEqual(a.Events.Digest, b.Events.Digest);
            Assert.AreEqual(a.StateDigest(), b.StateDigest());
            Assert.IsTrue(b.Events.Dropped > 0, "the undrained ring must have overflowed for this test to mean something");
        }

        [Test]
        public void BotRunsAreReproducibleAndSeedsDiffer()
        {
            HyperspaceTuning t = TestEnv.Tuning();
            ShipStats ship = ShipStats.AtProgress(0.5f);
            RunParams p = TestEnv.Route(77UL, 150f, 6, t.ShipPower(ship));
            Scenarios.Outcome a = Scenarios.Play(p, ship, BotSkill.Average(), TestEnv.Lib, t, true);
            Scenarios.Outcome b = Scenarios.Play(p, ship, BotSkill.Average(), TestEnv.Lib, t, true);
            Assert.AreEqual(a.events, b.events);
            Assert.AreEqual(a.state, b.state);
            Assert.AreEqual(a.result, b.result);
            p.seed = 78UL;
            Scenarios.Outcome c = Scenarios.Play(p, ship, BotSkill.Average(), TestEnv.Lib, t, true);
            Assert.AreNotEqual(a.schedule, c.schedule, "a different seed must change the schedule");
        }

        [Test]
        public void ScheduleIsAPureFunctionOfItsInputs()
        {
            HyperspaceTuning t = TestEnv.Tuning();
            ShipStats ship = ShipStats.Starter();
            ulong Digest(RunParams p) { return HyperspaceRun.Create(p, ship, t, TestEnv.Lib).Plan.Digest(); }
            RunParams baseP = TestEnv.Route(3UL, 100f, 4, 1f);
            Assert.AreEqual(Digest(baseP), Digest(baseP));
            RunParams q = baseP;
            q.tier = 5;
            Assert.AreNotEqual(Digest(baseP), Digest(q), "tier");
            q = baseP;
            q.distanceLy = 400f;
            Assert.AreNotEqual(Digest(baseP), Digest(q), "distance");
            q = baseP;
            q.mode = RunMode.Challenge;
            Assert.AreNotEqual(Digest(baseP), Digest(q), "mode");
        }

        [Test]
        public void GoldenDigestsMatchOnThisRuntime()
        {
            ulong[] d = SelfCheck.Run(TestEnv.Tuning(), TestEnv.Lib);
            Assert.AreEqual(Golden.Digests.Length, d.Length);
            string[] names = { "first hop", "tier 6 + Guardian", "tier 12 + revives" };
            for (int s = 0; s < SelfCheck.ScenarioCount; s++)
                Console.WriteLine("    " + (TestEnv.IsMono ? "Mono   " : "CoreCLR") + " " + names[s].PadRight(18) + " plan " + Scenarios.Hex(d[s * 3]) + "  events " + Scenarios.Hex(d[s * 3 + 1]) + "  state " + Scenarios.Hex(d[s * 3 + 2]));
            for (int i = 0; i < d.Length; i++)
                Assert.AreEqual(Golden.Digests[i], d[i], "digest " + i + " (scenario " + i / 3 + ", " + new[] { "plan", "events", "state" }[i % 3] + ") on " + (TestEnv.IsMono ? "Mono" : "CoreCLR") + " = " + Scenarios.Hex(d[i]));
        }

        [Test]
        public void EventStreamIsOrderedAndEndsWithExactlyOneRunEnded()
        {
            HyperspaceTuning t = TestEnv.Tuning();
            HyperspaceRun run = HyperspaceRun.Create(TestEnv.Route(21UL, 80f, 3, 2f), ShipStats.AtProgress(0.2f), t, TestEnv.Lib);
            var bot = new Bot(BotSkill.Average(), t, 21UL);
            var all = new System.Collections.Generic.List<HEvent>();
            var buf = new HEvent[EventRing.Capacity];
            while (run.State != RunState.Ended)
            {
                if (run.State == RunState.Downed) { if (run.CanRevive) run.Revive(); else run.Abandon(); }
                else run.Step(bot.Decide(run));
                int n = run.Events.Drain(buf);
                for (int i = 0; i < n; i++) all.Add(buf[i]);
            }
            Assert.IsTrue(all.Count > 20, "a whole run must produce events");
            int ended = 0;
            for (int i = 0; i < all.Count; i++)
            {
                if (i > 0) Assert.GreaterOrEqual(all[i].step, all[i - 1].step, "events must be ordered by step");
                Assert.IsTrue(Enum.IsDefined(typeof(EventType), all[i].type));
                if (all[i].type == EventType.RunEnded) ended++;
            }
            Assert.AreEqual(1, ended);
            Assert.AreEqual(EventType.RunEnded, all[all.Count - 1].type);
        }

        [Test]
        public void NoNaNOrInfinityInStateEvenWithHostileInput()
        {
            HyperspaceTuning t = TestEnv.Tuning();
            float[] hostile = { float.NaN, float.PositiveInfinity, float.NegativeInfinity, 1e30f, -1e30f, 0f, 3f, -3f };
            for (int tier = 0; tier <= 12; tier += 6)
            {
                ShipStats ship = ShipStats.AtProgress(tier / 12f);
                HyperspaceRun run = HyperspaceRun.Create(TestEnv.Route(40UL + (ulong)tier, 3000f, tier, t.ShipPower(ship)), ship, t, TestEnv.Lib);
                int step = 0;
                while (run.State != RunState.Ended && step < 5400)
                {
                    if (run.State == RunState.Downed) { if (run.CanRevive) run.Revive(); else run.Abandon(); continue; }
                    PlayerInput inp = (step / 40) % 3 == 0 ? new PlayerInput(hostile[(step / 40) % hostile.Length], hostile[(step / 53 + 3) % hostile.Length], (step & 1) == 0) : Script(step);
                    run.Step(inp);
                    run.Events.Clear();
                    if ((step & 7) == 0) Assert.IsNull(TestEnv.FindNonFinite(run), "tier " + tier + " step " + step);
                    step++;
                }
                Assert.IsTrue(Math.Abs(run.Player.x) <= t.areaHalfWidth + 1e-3 && Math.Abs(run.Player.y) <= t.areaHalfHeight + 1e-3, "the ship must stay inside the play area");
            }
        }

        [Test]
        public void EntityCapsAreRespected()
        {
            HyperspaceTuning t = TestEnv.Tuning();
            t.maxEnemies = 10;
            t.maxPlayerBullets = 6;
            t.maxEnemyBolts = 8;
            t.maxPickups = 3;
            ShipStats ship = ShipStats.Endgame();
            HyperspaceRun run = HyperspaceRun.Create(TestEnv.Route(8UL, 3000f, 12, t.ShipPower(ship)), ship, t, TestEnv.Lib);
            var bot = new Bot(BotSkill.Expert(), t, 8UL);
            int peakEnemies = 0;
            while (run.State != RunState.Ended)
            {
                if (run.State == RunState.Downed) { if (run.CanRevive) run.Revive(); else run.Abandon(); continue; }
                run.Step(bot.Decide(run));
                run.Events.Clear();
                Assert.LessOrEqual(run.EnemyCount, t.maxEnemies);
                Assert.LessOrEqual(run.ActiveBullets, t.maxPlayerBullets);
                Assert.LessOrEqual(run.ActiveBolts, t.maxEnemyBolts);
                Assert.LessOrEqual(run.ActivePickups, t.maxPickups);
                Assert.LessOrEqual(run.EnemyHigh, t.maxEnemies);
                if (run.EnemyCount > peakEnemies) peakEnemies = run.EnemyCount;
            }
            Assert.AreEqual(t.maxEnemies, peakEnemies, "the tiny cap must actually have been reached (peak " + peakEnemies + ")");
            Assert.Greater(run.SpawnFailures, 0, "dropped spawns are counted, never thrown");
        }
    }
}
