using System;
using NUnit.Framework;

namespace Frontier.Hyperspace.Tests
{
    [TestFixture]
    public class MechanicsTests
    {
        static readonly PlayerInput Hold = new PlayerInput(0f, 0f, false);

        /// <summary>A run in which static drones sit exactly on the ship at the given arrival steps (a pacifist ship never kills them).</summary>
        static HyperspaceRun Wall(HyperspaceTuning t, ShipStats ship, float seconds, params int[] arrivals)
        {
            return Scenarios.MakeRun(t, ship, 0, seconds, route =>
            {
                var recs = new SpawnRecord[arrivals.Length];
                for (int i = 0; i < arrivals.Length; i++) recs[i] = Scenarios.Rec(route, t, EntityType.Drone, 0f, 0f, arrivals[i]);
                return recs;
            });
        }

        static void StepUntil(HyperspaceRun run, Func<bool> done, int maxSteps, PlayerInput input)
        {
            for (int i = 0; i < maxSteps && !done() && run.State == RunState.Running; i++) run.Step(input);
        }

        /// <summary>An aimed turret that starts its lock cycle 1.7 s before it would reach the ship (p0 = offset seconds, like the library templates).</summary>
        public static SpawnRecord TurretRec(RouteSpec route, HyperspaceTuning t, float x, float y, int arrival)
        {
            SpawnRecord r = Scenarios.Rec(route, t, EntityType.TurretPod, x, y, arrival, PathType.Static);
            r.p0 = 1.7f;
            return r;
        }

        [Test]
        public void HullZeroFreezesTheWorldUntilTheHostDecides()
        {
            HyperspaceTuning t = TestEnv.Tuning();
            HyperspaceRun run = Wall(t, Scenarios.Pacifist(20f, 0), 30f, 150, 230, 310, 390);
            StepUntil(run, () => run.State == RunState.Downed, 900, Hold);
            Assert.AreEqual(RunState.Downed, run.State, "the ship must go down");
            Assert.IsTrue(run.Defeated);
            Assert.AreEqual(0f, run.Player.hull);
            Assert.Greater(Scenarios.Count(run, EventType.PlayerDowned), 0);
            ulong frozen = run.StateDigest();
            int step = run.StepIndex;
            for (int i = 0; i < 120; i++) run.Step(Hold);
            Assert.AreEqual(step, run.StepIndex, "a downed run does not advance");
            Assert.AreEqual(frozen, run.StateDigest(), "nothing moves while downed");
            Assert.IsTrue(run.CanRevive);
        }

        [Test]
        public void ReviveRestoresPartialHullGrantsInvulnerabilityAndClearsThreats()
        {
            HyperspaceTuning t = TestEnv.Tuning();
            HyperspaceRun run = Wall(t, Scenarios.Pacifist(20f, 0), 40f, 150, 230, 240, 250, 260, 300, 330);
            StepUntil(run, () => run.State == RunState.Downed, 900, Hold);
            Assert.AreEqual(RunState.Downed, run.State);
            int hitsBefore = run.HitsTaken;
            Assert.IsTrue(run.Revive());
            Assert.AreEqual(RunState.Running, run.State);
            Assert.AreEqual(1, run.RevivesUsed);
            Assert.AreEqual(run.Player.maxHull * t.reviveHullFraction, run.Player.hull, 0.01f, "partial hull");
            Assert.Less(run.Player.hull, run.Player.maxHull);
            Assert.GreaterOrEqual(run.Player.invulnerable, t.reviveInvulnSeconds - 0.01f);
            Assert.Greater(Scenarios.Count(run, EventType.Revived), 0);
            for (int i = 0; i < run.EnemyHigh; i++) if (run.Enemies[i].active) Assert.GreaterOrEqual(run.Enemies[i].z, t.reviveClearZ, "threats near the ship are cleared");
            Scenarios.RunSteps(run, (int)(t.reviveInvulnSeconds * 60f) - 2, Hold);
            Assert.AreEqual(hitsBefore, run.HitsTaken, "the invulnerability window absorbs the cleared wave's leftovers");
            Assert.AreEqual(RunState.Running, run.State);
        }

        [Test]
        public void RevivesAreLimitedAndAbandoningIsAlwaysPossible()
        {
            HyperspaceTuning t = TestEnv.Tuning();
            t.maxRevives = 1;
            var arrivals = new int[40];
            for (int i = 0; i < arrivals.Length; i++) arrivals[i] = 150 + 90 * i;
            HyperspaceRun run = Wall(t, Scenarios.Pacifist(20f, 0), 70f, arrivals);
            StepUntil(run, () => run.State == RunState.Downed, 3000, Hold);
            Assert.IsTrue(run.Revive());
            StepUntil(run, () => run.State == RunState.Downed, 3000, Hold);
            Assert.AreEqual(RunState.Downed, run.State, "second defeat");
            Assert.IsFalse(run.CanRevive, "no revives left");
            Assert.IsFalse(run.Revive(), "Revive must refuse instead of looping forever");
            run.Abandon();
            Assert.AreEqual(RunState.Ended, run.State, "the player can always finish the trip");
            RunResult r = run.BuildResult();
            Assert.IsTrue(r.defeated);
            Assert.IsFalse(r.reachedEnd);
            Assert.AreEqual(0f, r.fuelRefundFraction, "no fuel refund after defeat");
            Assert.AreEqual(0, r.lootList.Length, "no loot after defeat");
            Assert.AreEqual(1, r.revivesUsed);
            Assert.IsFalse(run.Revive(), "an ended run cannot be revived");
        }

        [Test]
        public void RevivedRunsStillFinishWithRefundAndLootButScoreLower()
        {
            HyperspaceTuning t = TestEnv.Tuning();
            HyperspaceRun run = Wall(t, Scenarios.Pacifist(20f, 0), 25f, 150, 230, 310);
            StepUntil(run, () => run.State == RunState.Downed, 900, Hold);
            Assert.AreEqual(RunState.Downed, run.State);
            float qDowned = run.ComputeQuality();
            Assert.Greater(qDowned, t.qualityPerRevive);
            Assert.IsTrue(run.Revive());
            Assert.AreEqual(qDowned - t.qualityPerRevive, run.ComputeQuality(), 1e-3f, "a revive costs a fixed slice of quality");
            while (run.State == RunState.Running) run.Step(Hold);
            RunResult r = run.BuildResult();
            Assert.IsTrue(r.reachedEnd, "after a revive the trip is finished normally");
            Assert.IsFalse(r.defeated);
            Assert.AreEqual(1, r.revivesUsed);
            Assert.Greater(r.fuelRefundFraction, 0f, "revived runs keep their refund");
            Assert.IsFalse(r.cleanRun);
        }

        [Test]
        public void ShieldAbsorbsHitsAndRechargesAfterThePause()
        {
            HyperspaceTuning t = TestEnv.Tuning();
            ShipStats ship = Scenarios.Pacifist(100f, 2);
            ship.shieldRecharge = 2f;
            HyperspaceRun run = Wall(t, ship, 25f, 150);
            Assert.AreEqual(2, run.Player.shield);
            StepUntil(run, () => run.Player.shield < 2, 400, Hold);
            Assert.AreEqual(1, run.Player.shield, "one charge absorbed the hit");
            Assert.AreEqual(100f, run.Player.hull, "hull untouched while a shield is up");
            Assert.AreEqual(1, Scenarios.Count(run, EventType.ShieldBreak));
            int hitStep = run.StepIndex;
            StepUntil(run, () => run.Player.shield == 2, 1200, Hold);
            Assert.AreEqual(2, run.Player.shield, "the shield recharges");
            double seconds = (run.StepIndex - hitStep) / 60.0;
            Assert.GreaterOrEqual(seconds, t.shieldDelay + ship.shieldRecharge - 0.1, "no recharge before the pause and the recharge time are over");
            Assert.LessOrEqual(seconds, t.shieldDelay + ship.shieldRecharge + 0.5);
            Assert.Greater(Scenarios.Count(run, EventType.ShieldRecharged), 0);
        }

        [Test]
        public void BombClearsBoltsDamagesEnemiesAndConsumesACharge()
        {
            HyperspaceTuning t = TestEnv.Tuning();
            ShipStats ship = Scenarios.Pacifist(1000f, 3);
            ship.bombCapacity = 3;
            HyperspaceRun run = Scenarios.MakeRun(t, ship, 6, 30f, route => new[]
            {
                TurretRec(route, t, 2.0f, 1.0f, 200),
                Scenarios.Rec(route, t, EntityType.Drone, -1.5f, 0.5f, 215, PathType.Static),
                Scenarios.Rec(route, t, EntityType.Drone, 1.5f, -0.5f, 220, PathType.Static),
                Scenarios.Rec(route, t, EntityType.Drone, 0f, -2.0f, 225, PathType.Static),
            });
            int bombs = run.Player.bombs;
            Assert.AreEqual(3, bombs);
            var away = new PlayerInput(-2.5f, -3f, false);
            StepUntil(run, () => run.ActiveBolts > 0, 600, away);
            Assert.Greater(run.ActiveBolts, 0, "the turret must have fired for this test to mean something");
            run.Events.Clear();
            run.Step(new PlayerInput(-2.5f, -3f, true));
            Assert.AreEqual(bombs - 1, run.Player.bombs, "a charge is consumed");
            Assert.AreEqual(0, run.ActiveBolts, "bombs clear every enemy projectile");
            Assert.AreEqual(1, Scenarios.Count(run, EventType.BombUsed));
            Assert.Greater(Scenarios.Count(run, EventType.EnemyHit) + Scenarios.Count(run, EventType.EnemyKilled), 0, "enemies on screen take bomb damage");
            run.Events.Clear();
            run.Step(new PlayerInput(-2.5f, -3f, true));
            Assert.AreEqual(bombs - 1, run.Player.bombs, "the bomb cooldown swallows a held button");
            Assert.AreEqual(0, Scenarios.Count(run, EventType.BombUsed));
            ShipStats empty = ShipStats.Starter();
            HyperspaceTuning t2 = TestEnv.Tuning();
            t2.startBombFraction = 0f;
            HyperspaceRun dry = Scenarios.MakeRun(t2, empty, 0, 10f, route => new SpawnRecord[0]);
            dry.Step(new PlayerInput(0f, 0f, true));
            Assert.AreEqual(0, Scenarios.Count(dry, EventType.BombUsed), "no charge, no bomb");
        }

        [Test]
        public void KillsRechargeBombs()
        {
            HyperspaceTuning t = TestEnv.Tuning();
            t.startBombFraction = 0f;
            t.bombKillsPerCharge = 3;
            ShipStats ship = ShipStats.Endgame();
            HyperspaceRun run = Scenarios.MakeRun(t, ship, 0, 20f, route =>
            {
                var recs = new SpawnRecord[8];
                for (int i = 0; i < recs.Length; i++) recs[i] = Scenarios.Rec(route, t, EntityType.Drone, 0.2f * (i - 4), 0.3f, 200 + 6 * i, PathType.Static);
                return recs;
            });
            Assert.AreEqual(0, run.Player.bombs);
            Scenarios.RunSteps(run, 700, Hold);
            Assert.GreaterOrEqual(run.Kills, 3, "the auto-fire should have killed the drones");
            Assert.GreaterOrEqual(run.Player.bombs, 1, "kills recharge the bomb");
            Assert.Greater(Scenarios.Count(run, EventType.BombCharged), 0);
        }

        [Test]
        public void GradeThresholdsAndYieldCurve()
        {
            HyperspaceTuning t = TestEnv.Tuning();
            Assert.AreEqual(Grade.S, HyperspaceRun.GradeFor(1f, t));
            Assert.AreEqual(Grade.S, HyperspaceRun.GradeFor(t.gradeS, t));
            Assert.AreEqual(Grade.A, HyperspaceRun.GradeFor(t.gradeS - 0.001f, t));
            Assert.AreEqual(Grade.A, HyperspaceRun.GradeFor(t.gradeA, t));
            Assert.AreEqual(Grade.B, HyperspaceRun.GradeFor(t.gradeA - 0.001f, t));
            Assert.AreEqual(Grade.B, HyperspaceRun.GradeFor(t.gradeB, t));
            Assert.AreEqual(Grade.C, HyperspaceRun.GradeFor(t.gradeB - 0.001f, t));
            Assert.AreEqual(Grade.C, HyperspaceRun.GradeFor(0f, t));
            Assert.IsTrue(t.gradeS > t.gradeA && t.gradeA > t.gradeB && t.gradeB > 0f);
            Assert.AreEqual(0.4f, t.yieldBase, 1e-6f);
            Assert.AreEqual(1.1f, t.yieldPerQuality, 1e-6f);

            ShipStats ship = ShipStats.AtProgress(0.3f);
            HyperspaceRun good = TestEnv.PlayOut(TestEnv.Route(2UL, 20f, 1, t.ShipPower(ship)), ship, BotSkill.Expert(), t);
            RunResult g = good.BuildResult();
            Assert.IsTrue(g.reachedEnd);
            Assert.AreEqual(Grade.S, g.grade, "a clean first hop is an S");
            Assert.IsTrue(g.cleanRun);
            Assert.AreEqual(0.4 + 1.1 * g.quality, g.yieldMultiplier, 1e-4, "yield = 0.4 + 1.1 x quality");
            Assert.Greater(g.fuelRefundFraction, 0.8f);
            Assert.LessOrEqual(g.fuelRefundFraction, 1f);
            Assert.Greater(g.killCount, 0);
            Assert.Greater(g.xp, 0);

            ShipStats weak = Scenarios.Pacifist(400f, 3);
            HyperspaceRun bad = HyperspaceRun.Create(TestEnv.Route(2UL, 20f, 4, 1f), weak, t, TestEnv.Lib);
            while (bad.State == RunState.Running) bad.Step(Hold);
            RunResult b = bad.BuildResult();
            Assert.IsFalse(b.cleanRun);
            Assert.Less(b.quality, g.quality, "idling through a tier 4 route must score lower than flying it well");
            Assert.Less(b.fuelRefundFraction, g.fuelRefundFraction);
            Assert.GreaterOrEqual(b.yieldMultiplier, t.yieldBase - 1e-4f);
        }
    }
}
