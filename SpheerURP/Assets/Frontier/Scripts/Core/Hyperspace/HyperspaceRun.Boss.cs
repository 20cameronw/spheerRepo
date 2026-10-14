namespace Frontier.Hyperspace
{
    public sealed partial class HyperspaceRun
    {
        int bossSlot;
        int bossPhase;
        float bossFightClock;
        float bossTransition;
        float bossAttackClock;
        int bossCycle;
        int bossAction;
        float bossVolleyAt;
        float bossAimX, bossAimY;
        int bossLockStep;
        int bossVolleyCount;

        /// <summary>Tool hook: when 1..3 the Guardian stays in that phase forever (no HP transitions, no timeout). Used by the fairness solver to analyse one phase at a time.</summary>
        public int BossPhaseLock;

        /// <summary>The Guardian's current phase (0 while it is flying in, 1-3 in the fight).</summary>
        public int BossPhase { get { return bossPhase; } }

        /// <summary>0..1 hull fraction of the Guardian for the HUD boss bar (0 when there is no boss alive).</summary>
        public float BossHpFraction
        {
            get
            {
                if (bossSlot < 0 || !Enemies[bossSlot].active) return 0f;
                return (float)HMath.Clamp01((double)Enemies[bossSlot].hp / Enemies[bossSlot].maxHp);
            }
        }

        /// <summary>The Guardian's pending aimed volley (for bots and telegraph HUD): seconds until it fires, or -1 when none.</summary>
        public float BossVolleyEta { get { return (float)(bossVolleyAt < 0f ? -1f : (double)bossVolleyAt - bossFightClock); } }
        public float BossVolleyAimX { get { return bossAimX; } }
        public float BossVolleyAimY { get { return bossAimY; } }
        public int BossVolleyCount { get { return bossVolleyCount; } }

        public bool BossActive { get { return bossSlot >= 0 && Enemies[bossSlot].active; } }

        void ResetBoss()
        {
            bossSlot = -1;
            bossPhase = 0;
            bossFightClock = 0f;
            bossTransition = 0f;
            bossAttackClock = 0f;
            bossCycle = 0;
            bossAction = 0;
            bossVolleyAt = -1f;
            bossVolleyCount = 0;
        }

        void InitBoss(ref Enemy e, int slot)
        {
            bossSlot = slot;
            bossPhase = 0;
            e.flags |= EnemyFlags.Boss | EnemyFlags.Invulnerable;
            double hp = Route.bossHp > 0f ? Route.bossHp : Tuning.enemyHp[(int)EntityType.Guardian];
            e.maxHp = e.hp = (float)hp;
            e.path = PathType.Hold;
            Emit(EventType.BossWarning, (int)EntityType.Guardian, e.uid, 0, e.x, e.y, e.z, Tuning.bossArriveSeconds);
        }

        static float BossPeriod(int phase) { return phase == 1 ? 6.0f : (phase == 2 ? 5.2f : 4.4f); }

        void UpdateBoss()
        {
            if (bossSlot < 0) return;
            ref Enemy b = ref Enemies[bossSlot];
            if (!b.active)
            {
                bossSlot = -1;
                return;
            }
            double arrive = Tuning.bossArriveSeconds;
            if (b.age < arrive)
            {
                double s = HMath.SmoothStep(0f, arrive, b.age);
                b.z = (float)HMath.Lerp(Tuning.spawnDistance, Tuning.bossZ, s);
                b.prevZ = b.z;
                return;
            }
            if (bossPhase == 0)
            {
                bossPhase = BossPhaseLock > 0 ? BossPhaseLock : 1;
                b.flags &= ~EnemyFlags.Invulnerable;
                bossFightClock = 0f;
                bossAttackClock = -1.0f;
                bossAction = 0;
                Emit(EventType.BossPhase, bossPhase, b.uid, 0, b.x, b.y, b.z, 1f);
            }
            b.z = Tuning.bossZ;
            b.prevZ = b.z;
            bossFightClock = (float)((double)bossFightClock + Dt);

            double amp = bossPhase == 1 ? 1.2f : (bossPhase == 2 ? 1.6f : 2.0f);
            double om = bossPhase == 1 ? 0.7f : (bossPhase == 2 ? 0.9f : 1.1f);
            double nx = amp * HMath.Sin(om * bossFightClock);
            double ny = 0.6f + 0.5f * HMath.Sin(0.6 * (double)bossFightClock);
            b.vx = (float)((nx - b.x) / Dt);
            b.vy = (float)((ny - b.y) / Dt);
            b.x = (float)nx;
            b.y = (float)ny;
            b.aimX = b.x;
            b.aimY = (float)((double)b.y - 1.1);

            if (BossPhaseLock == 0)
            {
                double frac = (double)b.hp / b.maxHp;
                if ((bossPhase == 1 && frac <= Tuning.bossPhase2Hp) || (bossPhase == 2 && frac <= Tuning.bossPhase3Hp)) BeginBossPhase(ref b, bossPhase + 1);
                if (bossFightClock >= Tuning.bossMaxSeconds)
                {
                    BossRetreat();
                    return;
                }
            }

            if (bossTransition > 0f)
            {
                bossTransition = (float)((double)bossTransition - Dt);
                if (bossTransition <= 0f)
                {
                    b.flags &= ~EnemyFlags.Invulnerable;
                    bossAttackClock = -0.6f;
                }
                return;
            }

            UpdateWeakPoint(ref b);
            bossAttackClock = (float)((double)bossAttackClock + Dt);
            if (bossVolleyAt >= 0f && bossFightClock >= bossVolleyAt) FireBossVolley(ref b);
            RunBossScript(ref b);
        }

        void BeginBossPhase(ref Enemy b, int phase)
        {
            bossPhase = phase;
            bossTransition = Tuning.bossTransitionSeconds;
            b.flags |= EnemyFlags.Invulnerable;
            b.flags &= ~EnemyFlags.WeakPointExposed;
            bossCycle = 0;
            bossAction = 0;
            bossVolleyAt = -1f;
            for (int i = 0; i < BoltHigh; i++)
                if (Bolts[i].active) FreeBolt(i);
            Emit(EventType.BossPhase, phase, b.uid, 0, b.x, b.y, b.z, (float)((double)b.hp / b.maxHp));
        }

        void UpdateWeakPoint(ref Enemy b)
        {
            double period = bossPhase == 3 ? 4.0f : 5.0f;
            double open = bossPhase == 3 ? 0.8f : 1.0f;
            double len = bossPhase == 3 ? 2.2f : 2.8f;
            double n = (int)(bossFightClock / period);
            double cyc = bossFightClock - n * period;
            bool exposed = cyc >= open && cyc < open + len;
            bool was = (b.flags & EnemyFlags.WeakPointExposed) != 0;
            if (exposed && !was)
            {
                b.flags |= EnemyFlags.WeakPointExposed;
                Emit(EventType.BossWeakPoint, bossPhase, b.uid, 1, b.aimX, b.aimY, b.z, (float)len);
            }
            else if (!exposed && was) b.flags &= ~EnemyFlags.WeakPointExposed;
        }

        /// <summary>How much of a bullet's damage lands on the Guardian at (x, y): the exposed weak point takes extra, the armour takes little, a miss takes none (returns 0).</summary>
        float BossBulletMultiplier(ref Enemy b, float x, float y, float br)
        {
            if ((b.flags & EnemyFlags.WeakPointExposed) != 0)
            {
                double rr = (double)Tuning.bossWeakPointRadius + br;
                if (HMath.Dist2(x, y, b.aimX, b.aimY) <= rr * rr) return Tuning.bossWeakPointDamageMul;
            }
            double r2 = (double)b.radius + br;
            if (HMath.Dist2(x, y, b.x, b.y) <= r2 * r2) return Tuning.bossArmorDamageMul;
            return 0f;
        }

        // ---------------------------------------------------------------- scripted attacks
        void RunBossScript(ref Enemy b)
        {
            double period = BossPeriod(bossPhase);
            if (bossAttackClock >= period)
            {
                bossAttackClock = (float)(bossAttackClock - period);
                bossCycle++;
                bossAction = 0;
            }
            double t = bossAttackClock;
            double side = (bossCycle & 1) == 0 ? -1f : 1f;
            switch (bossPhase)
            {
                case 1:
                    if (bossAction == 0 && t >= 0f) { SpawnBossGate(ref b, GateKind.Slit, (float)(side * 1.3), 0f, 1.45f, 0f, 0f, 0f, 0f); bossAction = 1; }
                    else if (bossAction == 1 && t >= 3.0f) { BeginBossVolley(ref b, 1); bossAction = 2; }
                    break;
                case 2:
                    if (bossAction == 0 && t >= 0f) { SpawnBossGate(ref b, GateKind.Ring, (float)side, 0.8f, 1.3f, 0.5f, 1.2f, 0f, 0.4f); bossAction = 1; }
                    else if (bossAction == 1 && t >= 2.4f) { SpawnBossSeeker(ref b); bossAction = 2; }
                    else if (bossAction == 2 && t >= 4.0f) { BeginBossVolley(ref b, 3); bossAction = 3; }
                    break;
                default:
                    if (bossAction == 0 && t >= 0f) { SpawnBossGate(ref b, GateKind.Slit, (float)(side * 1.4), 0f, 1.35f, 0f, 0f, 0f, 0f); bossAction = 1; }
                    else if (bossAction == 1 && t >= 1.2f) { SpawnBossGate(ref b, GateKind.Slot, 0f, (float)(-side * 1.5), 1.3f, 0f, 0f, 0f, 0f); bossAction = 2; }
                    else if (bossAction == 2 && t >= 2.4f) { BeginBossVolley(ref b, 3); bossAction = 3; }
                    break;
            }
        }

        void SpawnBossGate(ref Enemy boss, GateKind kind, float gx, float gy, float half, float amp, float omega, float phase, float ampY)
        {
            int slot = AllocEnemy();
            if (slot < 0) return;
            ref Enemy e = ref Enemies[slot];
            e = default(Enemy);
            e.active = true;
            e.type = EntityType.LaserGate;
            e.variant = (byte)kind;
            e.uid = nextUid++;
            e.flags = EnemyFlags.Invulnerable;
            e.z = boss.z;
            e.prevZ = boss.z;
            e.halfDepth = Tuning.gateHalfDepth;
            e.maxHp = e.hp = 1f;
            e.path = PathType.Static;
            e.ox = gx;
            e.oy = gy;
            e.p1 = half;
            e.p2 = amp;
            e.p3 = omega;
            e.p4 = phase;
            e.p5 = ampY;
            e.tArrive = (float)((double)boss.z / Speed);
            e.spawnStep = StepIndex;
        }

        void SpawnBossSeeker(ref Enemy boss)
        {
            int slot = AllocEnemy();
            if (slot < 0) return;
            ref Enemy e = ref Enemies[slot];
            e = default(Enemy);
            e.active = true;
            e.type = EntityType.Seeker;
            e.uid = nextUid++;
            e.x = boss.x;
            e.y = boss.y;
            e.ox = boss.x;
            e.oy = boss.y;
            e.z = boss.z;
            e.prevZ = boss.z;
            e.vzOwn = Tuning.seekerClosing;
            e.radius = Tuning.enemyRadius[(int)EntityType.Seeker];
            e.halfDepth = Tuning.enemyHalfDepth[(int)EntityType.Seeker];
            e.maxHp = e.hp = (float)((double)Tuning.enemyHp[(int)EntityType.Seeker] * Route.enemyHpScale);
            e.path = PathType.Homing;
            e.tArrive = (float)(boss.z / ((double)Speed + e.vzOwn));
            e.spawnStep = StepIndex;
            KillableSpawned++;
            Emit(EventType.SeekerLaunched, (int)e.type, e.uid, 0, e.x, e.y, e.z, 0f);
        }

        void BeginBossVolley(ref Enemy boss, int count)
        {
            bossAimX = Player.x;
            bossAimY = Player.y;
            bossLockStep = StepIndex;
            bossVolleyCount = count == 3 ? Tuning.FanCountFor(Route.tier) : count;
            double tele = Tuning.TurretTelegraphFor(Route.tier);
            bossVolleyAt = (float)(bossFightClock + tele);
            Emit(EventType.TurretTelegraph, (int)EntityType.Guardian, boss.uid, bossVolleyCount, bossAimX, bossAimY, boss.z, (float)tele);
        }

        void FireBossVolley(ref Enemy boss)
        {
            bossVolleyAt = -1f;
            int n = bossVolleyCount;
            double flight = (double)boss.z / Tuning.BoltSpeedFor(Route.tier);
            for (int k = 0; k < n; k++)
            {
                double off = n == 1 ? 0f : (k - (double)(n - 1) * 0.5) * Tuning.turretFanSpread;
                int slot = AllocBolt();
                if (slot < 0) break;
                ref Bolt bo = ref Bolts[slot];
                bo = default(Bolt);
                bo.active = true;
                bo.uid = nextUid++;
                bo.x = boss.x;
                bo.y = boss.y;
                bo.z = boss.z;
                bo.vx = (float)((bossAimX + off - boss.x) / flight);
                bo.vy = (float)(((double)bossAimY - boss.y) / flight);
                bo.vz = (float)(-boss.z / flight);
                bo.radius = Tuning.boltRadius;
                bo.damage = Tuning.boltDamage;
                bo.source = EntityType.Guardian;
                bo.lockStep = bossLockStep;
                if (Log != null) Log.AddLock(bossLockStep, StepIndex + (int)(flight / Dt + 0.5), (float)off, 0f, Tuning.boltRadius, (int)EntityType.Guardian);
            }
        }

        // ---------------------------------------------------------------- end of the fight
        void OnBossKilled(ref Enemy e)
        {
            BossDefeated = true;
            lootCounts[(int)LootKind.Core] += Tuning.bossCores;
            lootCounts[(int)LootKind.Salvage] += RollCount(Tuning.enemyScrap[(int)EntityType.Guardian]);
            lootCounts[(int)LootKind.Component] += 1 + (lootRng.NextFloat() < 0.5f ? 1 : 0);
            SpawnPickup(PickupType.LootCrate, (float)((double)e.x - 0.8), e.y, 40f);
            SpawnPickup(PickupType.LootCrate, (float)((double)e.x + 0.8), e.y, 46f);
            Emit(EventType.BossDefeated, 0, e.uid, 1, e.x, e.y, e.z, bossFightClock);
            bossSlot = -1;
            ClearThreatsAfterBoss(e.uid);
            EndNow();
        }

        void BossRetreat()
        {
            ref Enemy b = ref Enemies[bossSlot];
            Emit(EventType.BossDefeated, 0, b.uid, 0, b.x, b.y, b.z, bossFightClock);
            int slot = bossSlot;
            bossSlot = -1;
            ClearThreatsAfterBoss(b.uid);
            FreeEnemy(slot);
            EndNow();
        }

        void ClearThreatsAfterBoss(int keepUid)
        {
            for (int i = 0; i < BoltHigh; i++)
                if (Bolts[i].active) FreeBolt(i);
            for (int i = 0; i < EnemyHigh; i++)
            {
                ref Enemy x = ref Enemies[i];
                if (!x.active || x.uid == keepUid) continue;
                Emit(EventType.EnemyKilled, (int)x.type, x.uid, 0, x.x, x.y, x.z, 0f);
                FreeEnemy(i);
            }
        }

        /// <summary>Shortens the route so it ends after the usual outro; used when the Guardian is gone.</summary>
        void EndNow()
        {
            int end = StepIndex + (int)((double)Tuning.outroSeconds * HyperspaceTuning.StepsPerSecond);
            if (end < EndStep) EndStep = end;
        }
    }
}
