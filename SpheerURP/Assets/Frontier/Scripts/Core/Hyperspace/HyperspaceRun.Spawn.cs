using Frontier.Universe;

namespace Frontier.Hyperspace
{
    public sealed partial class HyperspaceRun
    {
        // ---------------------------------------------------------------- spawning
        void SpawnDue()
        {
            SpawnRecord[] recs = Plan.records;
            while (cursor < recs.Length && recs[cursor].step <= StepIndex)
            {
                SpawnFromRecord(ref recs[cursor]);
                cursor++;
            }
        }

        void SpawnFromRecord(ref SpawnRecord r)
        {
            if (r.type == EntityType.None)
            {
                if (r.pickup != PickupType.None) SpawnPickup(r.pickup, r.x, r.y, r.z0);
                return;
            }
            int slot = AllocEnemy();
            if (slot < 0) return;
            ref Enemy e = ref Enemies[slot];
            e = default(Enemy);
            e.active = true;
            e.type = r.type;
            e.variant = r.variant;
            e.uid = nextUid++;
            e.x = r.x;
            e.y = r.y;
            e.z = r.z0;
            e.prevZ = r.z0;
            e.vzOwn = r.vzOwn;
            e.path = r.path;
            e.ox = r.x;
            e.oy = r.y;
            e.p0 = r.p0; e.p1 = r.p1; e.p2 = r.p2; e.p3 = r.p3; e.p4 = r.p4; e.p5 = r.p5;
            e.tArrive = (float)((double)(r.arrivalStep - r.step) * Dt);
            e.spawnStep = StepIndex;
            int ti = (int)r.type;
            double hpScale = Route.enemyHpScale;
            e.radius = Tuning.enemyRadius[ti];
            e.halfDepth = Tuning.enemyHalfDepth[ti];
            e.maxHp = e.hp = (float)(Tuning.enemyHp[ti] * hpScale);
            switch (r.type)
            {
                case EntityType.Asteroid:
                {
                    double f = 1.0, hf = 1.0;
                    if (r.variant == (byte)AsteroidSize.Medium) { f = Tuning.asteroidChildRadius; hf = Tuning.asteroidChildHp; }
                    else if (r.variant == (byte)AsteroidSize.Small) { f = (double)Tuning.asteroidChildRadius * Tuning.asteroidChildRadius; hf = (double)Tuning.asteroidChildHp * Tuning.asteroidChildHp; }
                    e.radius = (float)(e.radius * f);
                    e.halfDepth = (float)(e.halfDepth * f);
                    e.maxHp = e.hp = (float)(Tuning.enemyHp[ti] * hpScale * hf);
                    break;
                }
                case EntityType.LaserGate:
                    e.flags |= EnemyFlags.Invulnerable;
                    e.radius = 0f;
                    e.halfDepth = r.variant == (byte)GateKind.Bar ? 3f : Tuning.gateHalfDepth;
                    e.maxHp = e.hp = 1f;
                    e.x = 0f;
                    e.y = 0f;
                    break;
                case EntityType.Elite:
                    e.maxShield = e.shield = (float)(Tuning.eliteShieldHp * hpScale);
                    break;
                case EntityType.TurretPod:
                    e.timer = (float)((double)e.tArrive - e.p0);
                    break;
                case EntityType.Seeker:
                    if (e.vzOwn <= 0f) e.vzOwn = Tuning.seekerClosing;
                    Emit(EventType.SeekerLaunched, (int)r.type, e.uid, 0, e.x, e.y, e.z, 0f);
                    break;
                case EntityType.Guardian:
                    InitBoss(ref e, slot);
                    break;
            }
            if (e.path != PathType.Homing && e.path != PathType.Hold && r.type != EntityType.LaserGate)
            {
                double nx, ny;
                Paths.Position(e, -e.tArrive, out nx, out ny);
                e.x = (float)nx;
                e.y = (float)ny;
            }
            if (r.type != EntityType.LaserGate) KillableSpawned++;
        }

        int SpawnPickup(PickupType type, float x, float y, float z)
        {
            int slot = AllocPickup();
            if (slot < 0) return -1;
            ref Pickup p = ref Pickups[slot];
            p = default(Pickup);
            p.active = true;
            p.type = type;
            p.uid = nextUid++;
            p.x = x;
            p.y = y;
            p.z = z;
            PickupsSpawned++;
            Emit(EventType.PickupSpawned, (int)type, p.uid, 0, x, y, z, 0f);
            return slot;
        }

        // ---------------------------------------------------------------- enemies
        void UpdateEnemies()
        {
            for (int i = 0; i < EnemyHigh; i++)
            {
                ref Enemy e = ref Enemies[i];
                if (!e.active) continue;
                e.prevZ = e.z;
                e.age = (float)((double)e.age + Dt);
                e.flash = (float)((double)e.flash + Dt);
                if (e.type == EntityType.Guardian) continue;
                e.z = (float)(e.z - (((double)Speed + e.vzOwn) * Dt));
                switch (e.path)
                {
                    case PathType.Homing:
                        UpdateSeeker(ref e);
                        break;
                    case PathType.Hold:
                        break;
                    default:
                    {
                        if (e.type == EntityType.LaserGate) break;
                        double nx, ny;
                        Paths.Position(e, (double)e.age - e.tArrive, out nx, out ny);
                        e.vx = (float)((nx - e.x) / Dt);
                        e.vy = (float)((ny - e.y) / Dt);
                        e.x = (float)nx;
                        e.y = (float)ny;
                        break;
                    }
                }
                switch (e.type)
                {
                    case EntityType.TurretPod: UpdateTurret(ref e); break;
                    case EntityType.Mine: UpdateMine(ref e); break;
                    case EntityType.LaserGate: UpdateGate(ref e); break;
                }
            }
        }

        void UpdateSeeker(ref Enemy e)
        {
            double closing = (double)Speed + e.vzOwn;
            double toPlane = e.z / closing;
            int tier = Route.tier;
            double commit = Tuning.SeekerCommitFor(tier);
            double sScale = Tuning.SeekerSpeedScale(tier);
            if (e.state == 0)
            {
                if (e.age >= Tuning.seekerTurnWarmup && toPlane > commit)
                {
                    double cap = Tuning.seekerLateralSpeed * sScale;
                    double wx = HMath.Clamp(((double)Player.x - e.x) * 3.0, -cap, cap);
                    double wy = HMath.Clamp(((double)Player.y - e.y) * 3.0, -cap, cap);
                    e.vx = (float)HMath.MoveTowards(e.vx, wx, Tuning.seekerLateralAccel * sScale * Dt);
                    e.vy = (float)HMath.MoveTowards(e.vy, wy, Tuning.seekerLateralAccel * sScale * Dt);
                }
                if (toPlane <= commit)
                {
                    e.state = 1;
                    e.aimX = Player.x;
                    e.aimY = Player.y;
                    e.lockStep = StepIndex;
                    double t = toPlane > 0.05f ? toPlane : 0.05f;
                    double cx = ((double)e.aimX - e.x) / t, cy = ((double)e.aimY - e.y) / t;
                    double len = HMath.Length(cx, cy);
                    double lim = Tuning.seekerCommitSpeed * sScale;
                    if (len > lim)
                    {
                        cx *= lim / len;
                        cy *= lim / len;
                    }
                    e.vx = (float)cx;
                    e.vy = (float)cy;
                    e.flags |= EnemyFlags.Telegraphing;
                    Emit(EventType.TurretTelegraph, (int)e.type, e.uid, 0, e.aimX, e.aimY, e.z, (float)toPlane);
                    if (Log != null) Log.AddLock(StepIndex, StepIndex + (int)(toPlane / Dt + 0.5), 0f, 0f, (float)((double)Tuning.enemyRadius[(int)EntityType.Seeker] + Tuning.seekerLockPad), (int)EntityType.Seeker);
                }
            }
            e.x = (float)(e.x + ((double)e.vx * Dt));
            e.y = (float)(e.y + ((double)e.vy * Dt));
        }

        void UpdateTurret(ref Enemy e)
        {
            double toPlane = (double)e.tArrive - e.age;
            double tele = Tuning.TurretTelegraphFor(Route.tier);
            if (e.state == 0)
            {
                if (e.age < e.timer) return;
                if (toPlane < tele + 0.45 || e.z > Tuning.turretRange)
                {
                    if (toPlane < tele + 0.45) e.timer = float.MaxValue;
                    return;
                }
                e.state = 1;
                e.aimX = Player.x;
                e.aimY = Player.y;
                e.lockStep = StepIndex;
                e.p2 = e.age;
                e.p1 = (float)(e.age + tele);
                e.flags |= EnemyFlags.Telegraphing;
                Emit(EventType.TurretTelegraph, (int)e.type, e.uid, e.variant, e.aimX, e.aimY, e.z, (float)tele);
            }
            else if (e.age >= e.p1)
            {
                FireTurret(ref e);
                e.state = 0;
                e.flags &= ~EnemyFlags.Telegraphing;
                e.timer = (float)((double)e.p2 + Tuning.turretCycle);
            }
        }

        void FireTurret(ref Enemy e)
        {
            int n = e.variant == (byte)TurretMode.Fan ? Tuning.FanCountFor(Route.tier) : 1;
            double speed = Tuning.BoltSpeedFor(Route.tier);
            double flight = e.z / speed;
            if (flight < 0.1f) flight = 0.1;
            for (int k = 0; k < n; k++)
            {
                double off = n == 1 ? 0f : (k - (double)(n - 1) * 0.5) * Tuning.turretFanSpread;
                double tx = e.aimX + off, ty = e.aimY;
                int slot = AllocBolt();
                if (slot < 0) break;
                ref Bolt b = ref Bolts[slot];
                b = default(Bolt);
                b.active = true;
                b.uid = nextUid++;
                b.x = e.x; b.y = e.y; b.z = e.z;
                b.vx = (float)((tx - e.x) / flight);
                b.vy = (float)((ty - e.y) / flight);
                b.vz = (float)(-e.z / flight);
                b.radius = Tuning.boltRadius;
                b.damage = Tuning.boltDamage;
                b.source = e.type;
                b.lockStep = e.lockStep;
                if (Log != null) Log.AddLock(e.lockStep, StepIndex + (int)(flight / Dt + 0.5), (float)off, 0f, Tuning.boltRadius, (int)EntityType.TurretPod);
            }
        }

        void UpdateMine(ref Enemy e)
        {
            if (e.state != 0 || e.z > Tuning.mineArmZ || e.z <= Tuning.mineDetonateZ) return;
            double r = Tuning.mineArmRadius;
            if (HMath.Dist2(Player.x, Player.y, e.x, e.y) <= r * r)
            {
                e.state = 1;
                e.flags |= EnemyFlags.Armed;
                Emit(EventType.MineArmed, (int)e.type, e.uid, 0, e.x, e.y, e.z, 0f);
            }
        }

        void UpdateGate(ref Enemy e)
        {
            if (e.state != 0) return;
            double toPlane = (double)e.z / Speed;
            if (toPlane > Tuning.gateTelegraphSeconds) return;
            e.state = 1;
            double gx, gy;
            Hazards.GapCentre(e, out gx, out gy);
            e.aimX = (float)gx;
            e.aimY = (float)gy;
            Emit(EventType.GateTelegraph, e.variant, e.uid, 0, (float)gx, (float)gy, e.z, (float)toPlane);
        }

        // ---------------------------------------------------------------- bolts and pickups
        void UpdateBolts()
        {
            for (int i = 0; i < BoltHigh; i++)
            {
                ref Bolt b = ref Bolts[i];
                if (!b.active) continue;
                b.x = (float)(b.x + ((double)b.vx * Dt));
                b.y = (float)(b.y + ((double)b.vy * Dt));
                b.z = (float)(b.z + ((double)b.vz * Dt));
                if (b.z < Tuning.despawnZ) FreeBolt(i);
            }
        }

        void UpdatePickups()
        {
            double magnet = Ship.magnet;
            for (int i = 0; i < PickupHigh; i++)
            {
                ref Pickup p = ref Pickups[i];
                if (!p.active) continue;
                p.age = (float)((double)p.age + Dt);
                p.z = (float)(p.z - ((double)Speed * Dt));
                if (State == RunState.Running && p.z > -2f && p.z < 18f)
                {
                    double dx = (double)Player.x - p.x, dy = (double)Player.y - p.y;
                    double d2 = dx * dx + dy * dy;
                    if (d2 <= magnet * magnet && d2 > 1e-6f)
                    {
                        double d = HMath.Sqrt(d2);
                        double step = HMath.Min((double)Tuning.magnetPullSpeed * Dt, d);
                        p.x = (float)(p.x + (dx / d * step));
                        p.y = (float)(p.y + (dy / d * step));
                        p.magnetized = true;
                    }
                }
                if (p.z < Tuning.despawnZ) FreePickup(i);
            }
        }

        // ---------------------------------------------------------------- cleanup and wave events
        void CleanUp()
        {
            for (int i = 0; i < EnemyHigh; i++)
            {
                ref Enemy e = ref Enemies[i];
                if (e.active && e.type != EntityType.Guardian && e.z < Tuning.despawnZ) FreeEnemy(i);
            }
        }

        void UpdateWaveEvents()
        {
            PlannedEncounter[] enc = Plan.encounters;
            while (waveClearedIndex < enc.Length)
            {
                PlannedEncounter pe = enc[waveClearedIndex];
                if (pe.kind != PacingKind.Boss && StepIndex < pe.endStep) break;
                if (pe.kind != PacingKind.Boss) Emit(EventType.WaveCleared, waveClearedIndex, pe.template, 0, 0f, 0f, 0f, pe.cost);
                waveClearedIndex++;
            }
        }

        void FinishRun(bool reachedEnd)
        {
            if (State == RunState.Ended) return;
            State = RunState.Ended;
            ReachedEnd = reachedEnd;
            RunResult r = BuildResult(reachedEnd);
            Emit(EventType.RunEnded, (int)r.grade, reachedEnd ? 1 : 0, Kills, 0f, 0f, 0f, r.quality);
        }
    }
}
