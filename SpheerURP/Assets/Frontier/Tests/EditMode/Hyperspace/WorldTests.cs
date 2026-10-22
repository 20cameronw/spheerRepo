using System;
using NUnit.Framework;

namespace Frontier.Hyperspace.Tests
{
    [TestFixture]
    public class WorldTests
    {
        static readonly PlayerInput Hold = new PlayerInput(0f, 0f, false);

        static SpawnRecord PickupRec(RouteSpec route, HyperspaceTuning t, PickupType type, float x, float y, int arrival)
        {
            SpawnRecord r = Scenarios.Rec(route, t, EntityType.None, x, y, arrival);
            r.pickup = type;
            return r;
        }

        static void StepUntil(HyperspaceRun run, Func<bool> done, int maxSteps, PlayerInput input)
        {
            for (int i = 0; i < maxSteps && !done() && run.State == RunState.Running; i++) run.Step(input);
        }

        [Test]
        public void AsteroidsSplitIntoSmallerFragmentsInsideTheParentFootprint()
        {
            HyperspaceTuning t = TestEnv.Tuning();
            ShipStats ship = ShipStats.Endgame();
            HyperspaceRun run = Scenarios.MakeRun(t, ship, 0, 30f, route => new[] { Scenarios.Rec(route, t, EntityType.Asteroid, 0.4f, 0.2f, 300, PathType.Static) });
            float parentRadius = 0f, parentX = 0f, parentY = 0f;
            int generations = 0;
            for (int i = 0; i < 1500 && run.State == RunState.Running; i++)
            {
                run.Step(Hold);
                for (int k = 0; k < run.EnemyHigh; k++)
                {
                    ref Enemy e = ref run.Enemies[k];
                    if (e.active && e.type == EntityType.Asteroid && e.variant == (byte)AsteroidSize.Large) { parentRadius = e.radius; parentX = e.x; parentY = e.y; }
                }
                if (Scenarios.Count(run, EventType.AsteroidSplit) > generations) { generations = Scenarios.Count(run, EventType.AsteroidSplit); break; }
            }
            Assert.AreEqual(2, generations, "the large asteroid splits into two fragments when destroyed (one AsteroidSplit event per fragment)");
            Assert.Greater(parentRadius, 0.5f);
            int children = 0;
            for (int k = 0; k < run.EnemyHigh; k++)
            {
                ref Enemy c = ref run.Enemies[k];
                if (!c.active || (c.flags & EnemyFlags.FromSplit) == 0) continue;
                children++;
                Assert.AreEqual((byte)AsteroidSize.Medium, c.variant);
                Assert.Less(c.radius, parentRadius * 0.6f, "fragments are smaller");
                double reach = Math.Sqrt((c.x - parentX) * (c.x - parentX) + (c.y - parentY) * (c.y - parentY)) + c.radius;
                Assert.LessOrEqual(reach, parentRadius + 0.02, "a fragment starts inside the footprint of its parent, so it never appears out of nowhere on top of the ship");
            }
            Assert.AreEqual(2, children);
            for (int i = 0; i < 900 && run.State == RunState.Running; i++) run.Step(Hold);
            Assert.LessOrEqual(Scenarios.Count(run, EventType.AsteroidSplit), 6, "large -> 2 medium -> 4 small; small rocks do not split further");
        }

        [Test]
        public void PickupsApplyTheirEffects()
        {
            HyperspaceTuning t = TestEnv.Tuning();
            t.startBombFraction = 0f;
            ShipStats ship = Scenarios.Pacifist(100f, 1);
            ship.bombCapacity = 3;
            HyperspaceRun run = Scenarios.MakeRun(t, ship, 2, 30f, route => new[]
            {
                PickupRec(route, t, PickupType.Overdrive, 0f, 0f, 200),
                PickupRec(route, t, PickupType.Fuel, 0f, 0f, 260),
                PickupRec(route, t, PickupType.LootCrate, 0f, 0f, 320),
                PickupRec(route, t, PickupType.Bomb, 0f, 0f, 380),
            });
            Scenarios.RunSteps(run, 215, Hold);
            Assert.Greater(run.Player.overdrive, 0f, "overdrive collected");
            Assert.AreEqual(1, Scenarios.Count(run, EventType.OverdriveStart));
            Scenarios.RunSteps(run, 600 - 215, Hold);
            Assert.AreEqual(1, run.FuelCanisters);
            Assert.AreEqual(t.fuelRefundPerCanister, run.FuelBonus, 1e-5f);
            Assert.Greater(run.LootCount(LootKind.Salvage), 0, "the crate paid out salvage");
            Assert.AreEqual((int)t.bombPickupCharges, run.Player.bombs);
            Assert.AreEqual(4, run.PickupsCollected);
            Assert.AreEqual(4, Scenarios.Count(run, EventType.PickupCollected));
        }

        [Test]
        public void MagnetPullsNearbyPickupsIn()
        {
            HyperspaceTuning t = TestEnv.Tuning();
            ShipStats ship = Scenarios.Pacifist(100f, 1);
            ship.magnet = 1.8f;
            HyperspaceRun run = Scenarios.MakeRun(t, ship, 0, 20f, route => new[] { PickupRec(route, t, PickupType.Fuel, 1.5f, 0f, 240) });
            float start = -1f, end = -1f;
            for (int i = 0; i < 400 && run.State == RunState.Running; i++)
            {
                run.Step(Hold);
                for (int k = 0; k < run.PickupHigh; k++)
                    if (run.Pickups[k].active && run.Pickups[k].z < 20f) { if (start < 0f) start = run.Pickups[k].x; end = run.Pickups[k].x; }
            }
            Assert.Greater(start, 1.4f);
            Assert.IsTrue(run.PickupsCollected == 1 && end < start, "the magnet drags the canister toward the ship and it is collected (x " + start + " -> " + end + ")");
        }

        [Test]
        public void ComboBuildsOnConsecutiveKillsAndBreaksOnAHit()
        {
            HyperspaceTuning t = TestEnv.Tuning();
            ShipStats ship = ShipStats.Endgame();
            ship.shield = 0;
            HyperspaceRun run = Scenarios.MakeRun(t, ship, 0, 30f, route =>
            {
                var list = new System.Collections.Generic.List<SpawnRecord>();
                for (int i = 0; i < 7; i++) list.Add(Scenarios.Rec(route, t, EntityType.Drone, 0.15f * (i - 3), 0.2f, 330 + 8 * i, PathType.Static));
                SpawnRecord g = Scenarios.Rec(route, t, EntityType.LaserGate, 2.0f, 0f, 440, PathType.Static);
                g.variant = (byte)GateKind.Slit;
                g.p1 = 0.8f;
                list.Add(g);
                return list.ToArray();
            });
            int comboAtHit = 0;
            for (int i = 0; i < 1500 && run.State == RunState.Running && run.HitsTaken == 0; i++)
            {
                if (run.Player.combo > comboAtHit) comboAtHit = run.Player.combo;
                run.Step(Hold);
            }
            Assert.GreaterOrEqual(run.Kills, 6, "the drones die");
            Assert.GreaterOrEqual(comboAtHit, 4, "consecutive kills inside the combo window build a combo");
            Assert.GreaterOrEqual(run.HitsTaken, 1, "the gate wall hits a ship parked outside its gap");
            Assert.AreEqual(0, run.Player.combo, "a hit breaks the combo");
            Assert.GreaterOrEqual(Scenarios.Count(run, EventType.ComboBreak), 1);
            Assert.GreaterOrEqual(run.MaxCombo, 4);
        }

        [Test]
        public void PlayerMustLineUpWithAnOffAxisTarget()
        {
            HyperspaceTuning t = TestEnv.Tuning();
            ShipStats ship = ShipStats.Starter();
            int[] kills = new int[2];
            for (int mode = 0; mode < 2; mode++)
            {
                HyperspaceRun run = Scenarios.MakeRun(t, ship, 0, 25f, route => new[] { Scenarios.Rec(route, t, EntityType.Drone, 1.1f, 0.0f, 300, PathType.Static) });
                Scenarios.RunSteps(run, 330, mode == 0 ? new PlayerInput(1.1f, 0, false) : Hold);
                kills[mode] = run.Kills;
            }
            Assert.AreEqual(1, kills[0], "lining up the ship hits the target");
            Assert.AreEqual(0, kills[1], "straight bullets miss an enemy 1.1 u off the line of fire");
        }

        [Test]
        public void TelegraphsAnnounceThreatsEarlyEnough()
        {
            HyperspaceTuning t = TestEnv.Tuning();
            ShipStats ship = Scenarios.Pacifist(1000f, 3);
            HyperspaceRun run = Scenarios.MakeRun(t, ship, 3, 30f, route => new[]
            {
                MechanicsTests.TurretRec(route, t, 2.0f, 1.0f, 300),
                Scenarios.Rec(route, t, EntityType.Seeker, -2.0f, 2.0f, 420, PathType.Homing, 6f),
            });
            var buf = new HEvent[EventRing.Capacity];
            bool turret = false, seeker = false;
            for (int i = 0; i < 900 && run.State == RunState.Running; i++)
            {
                run.Step(Hold);
                int n = run.Events.Drain(buf);
                for (int k = 0; k < n; k++)
                {
                    if (buf[k].type != EventType.TurretTelegraph) continue;
                    if (buf[k].a == (int)EntityType.TurretPod) { turret = true; Assert.GreaterOrEqual(buf[k].v, t.reactionBudget + 0.25f, "turret telegraph " + buf[k].v); }
                    if (buf[k].a == (int)EntityType.Seeker) { seeker = true; Assert.GreaterOrEqual(buf[k].v, t.reactionBudget + 0.25f, "seeker commit " + buf[k].v); }
                }
            }
            Assert.IsTrue(turret, "the turret telegraphs before it fires");
            Assert.IsTrue(seeker, "the seeker announces its lock");
        }

        [Test]
        public void ShipFollowsTheFingerSmoothlyWithinItsSpeedLimit()
        {
            HyperspaceTuning t = TestEnv.Tuning();
            ShipStats slow = ShipStats.Starter(), fast = ShipStats.Endgame();
            double[] arrive = new double[2];
            for (int s = 0; s < 2; s++)
            {
                HyperspaceRun run = Scenarios.MakeRun(t, s == 0 ? slow : fast, 0, 10f, route => new SpawnRecord[0]);
                var target = new PlayerInput(2.4f, 3.0f, false);
                float maxSpeed = run.Player.maxSpeed, overshoot = 0f;
                for (int i = 0; i < 180; i++)
                {
                    run.Step(target);
                    double v = Math.Sqrt(run.Player.vx * run.Player.vx + run.Player.vy * run.Player.vy);
                    Assert.LessOrEqual(v, maxSpeed + 1e-3, "speed limit");
                    if (run.Player.x > 2.4f) overshoot = Math.Max(overshoot, run.Player.x - 2.4f);
                    if (arrive[s] == 0 && Math.Abs(run.Player.x - 2.4) < 0.05 && Math.Abs(run.Player.y - 3.0) < 0.05) arrive[s] = i / 60.0;
                }
                Assert.Less(overshoot, 0.05f, "critically damped: no visible overshoot");
                Assert.IsTrue(arrive[s] > 0.2 && arrive[s] < 2.5, "arrival time " + arrive[s]);
            }
            Assert.Less(arrive[1], arrive[0], "better handling follows faster");
        }

        [Test]
        public void WorldScrollsAtARampingSpeedAndEntitiesDespawnBehindTheShip()
        {
            HyperspaceTuning t = TestEnv.Tuning();
            ShipStats ship = ShipStats.AtProgress(0.5f);
            HyperspaceRun run = HyperspaceRun.Create(TestEnv.Route(6UL, 200f, 5, t.ShipPower(ship)), ship, t, TestEnv.Lib);
            float[] sp = run.Route.speed;
            Assert.Less(sp[0], sp[sp.Length / 2], "the run ramps up from a slow start");
            Assert.Less(sp[sp.Length - 1], sp[sp.Length / 2], "and brakes at the end");
            var bot = new Bot(BotSkill.Expert(), t, 6UL);
            float maxZ = 0f;
            while (run.State != RunState.Ended)
            {
                if (run.State == RunState.Downed) { if (run.CanRevive) run.Revive(); else run.Abandon(); continue; }
                int k = run.StepIndex;
                run.Step(bot.Decide(run));
                run.Events.Clear();
                Assert.AreEqual(sp[k], run.Speed, 1e-4f, "scroll speed follows the profile");
                for (int i = 0; i < run.EnemyHigh; i++)
                {
                    if (!run.Enemies[i].active) continue;
                    if (run.Enemies[i].z > maxZ) maxZ = run.Enemies[i].z;
                    Assert.GreaterOrEqual(run.Enemies[i].z, t.despawnZ - 1f, "nothing lingers behind the ship");
                }
            }
            Assert.LessOrEqual(maxZ, t.spawnDistance + 1f, "entities appear at the spawn distance");
            Assert.Greater(maxZ, t.spawnDistance * 0.5f);
        }

        [Test]
        public void GuardianFightHasPhasesAndCanBeDefeated()
        {
            HyperspaceTuning t = TestEnv.Tuning();
            ShipStats ship = ShipStats.AtProgress(0.7f);
            RunParams p = TestEnv.Route(31UL, 800f, 8, t.ShipPower(ship));
            HyperspaceRun run = HyperspaceRun.Create(p, ship, t, TestEnv.Lib);
            Assert.IsTrue(run.Route.hasBoss);
            var bot = new Bot(BotSkill.Expert(), t, p.seed);
            var buf = new HEvent[EventRing.Capacity];
            int lastPhase = 0, warning = 0, defeated = 0;
            bool order = true;
            while (run.State != RunState.Ended)
            {
                if (run.State == RunState.Downed) { if (run.CanRevive) run.Revive(); else run.Abandon(); continue; }
                var input = bot.Decide(run);
                if (run.BossActive && run.BossPhase > 0)
                    for (int i = 0; i < run.EnemyHigh; i++)
                        if (run.Enemies[i].active && run.Enemies[i].type == EntityType.Guardian)
                            input = new PlayerInput(run.Enemies[i].aimX, run.Enemies[i].aimY, run.BossPhase == 3);
                run.Step(input);
                int n = run.Events.Drain(buf);
                for (int k = 0; k < n; k++)
                {
                    if (buf[k].type == EventType.BossWarning) warning++;
                    if (buf[k].type == EventType.BossDefeated) defeated++;
                    if (buf[k].type == EventType.BossPhase) { if (buf[k].a < lastPhase) order = false; lastPhase = buf[k].a; }
                }
            }
            Assert.IsTrue(order, "boss phases only advance");
            Assert.GreaterOrEqual(lastPhase, 3, "the Guardian reaches its final phase");
            Assert.AreEqual(1, warning, "one warning before the boss arrives");
            Assert.AreEqual(1, defeated, "a well-equipped ship defeats it");
            RunResult r = run.BuildResult();
            Assert.IsTrue(r.bossDefeated);
            Assert.IsTrue(r.reachedEnd);
            Assert.Greater(r.lootList.Length, 0);
        }
    }
}
