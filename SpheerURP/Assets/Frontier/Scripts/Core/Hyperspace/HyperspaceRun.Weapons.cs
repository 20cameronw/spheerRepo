namespace Frontier.Hyperspace
{
    public sealed partial class HyperspaceRun
    {
        // ---------------------------------------------------------------- auto-fire and bullets
        void UpdateWeapons()
        {
            ref PlayerState p = ref Player;
            if (!Ghost)
            {
                p.fireCooldown = (float)((double)p.fireCooldown - Dt);
                if (p.fireCooldown <= 0f)
                {
                    FireBullet();
                    double rate = (double)Ship.fireRate * (p.overdrive > 0f ? Tuning.overdriveFireMul : 1f);
                    if (rate < 0.5f) rate = 0.5;
                    p.fireCooldown = (float)(p.fireCooldown + (1.0 / rate));
                    if (p.fireCooldown < 0f) p.fireCooldown = 0f;
                }
            }
            double k = HMath.Clamp01((double)Tuning.assistStrength * Tuning.assistRate * Dt);
            for (int i = 0; i < BulletHigh; i++)
            {
                ref Bullet b = ref Bullets[i];
                if (!b.active) continue;
                if (b.targetSlot >= 0)
                {
                    ref Enemy t = ref Enemies[b.targetSlot];
                    if (t.active && t.uid == b.targetUid) SteerBullet(ref b, ref t, (float)k);
                    else b.targetSlot = -1;
                }
                b.x = (float)(b.x + ((double)b.vx * Dt));
                b.y = (float)(b.y + ((double)b.vy * Dt));
                b.z = (float)(b.z + ((double)b.vz * Dt));
                if (b.z > Tuning.bulletRange) FreeBullet(i);
            }
        }

        void FireBullet()
        {
            int slot = AllocBullet();
            if (slot < 0) return;
            ref PlayerState p = ref Player;
            ref Bullet b = ref Bullets[slot];
            b = default(Bullet);
            b.active = true;
            b.x = p.x;
            b.y = p.y;
            b.z = 0.8f;
            b.vz = Tuning.bulletSpeed;
            bool od = p.overdrive > 0f;
            b.damage = (float)((double)Ship.damage * (od ? Tuning.overdriveDamageMul : 1f));
            b.pierceLeft = Ship.pierce + (od ? Tuning.overdrivePierce : 0);
            b.spawnStep = StepIndex;
            b.targetSlot = -1;
            int ts = SelectTarget(p.x, p.y);
            if (ts >= 0)
            {
                b.targetSlot = ts;
                b.targetUid = Enemies[ts].uid;
                SteerBullet(ref b, ref Enemies[ts], 0.6f);
            }
            if (StepIndex - lastShotEvent >= 4)
            {
                lastShotEvent = StepIndex;
                Emit(EventType.ShotFired, 0, 0, 0, p.x, p.y, 0f, 1f);
            }
        }

        /// <summary>Where a bullet should aim on an enemy (the exposed weak point of the Guardian, otherwise the centre).</summary>
        static void AimPoint(ref Enemy e, out double x, out double y)
        {
            if (e.type == EntityType.Guardian && (e.flags & EnemyFlags.WeakPointExposed) != 0)
            {
                x = e.aimX;
                y = e.aimY;
                return;
            }
            x = e.x;
            y = e.y;
        }

        void SteerBullet(ref Bullet b, ref Enemy e, float k)
        {
            double dz = (double)e.z - b.z;
            if (dz < 0.5f) return;
            double closing = (double)Speed + e.vzOwn;
            if (e.type == EntityType.Guardian) closing = 0.0;
            double t = dz / (b.vz + closing);
            double ax, ay;
            AimPoint(ref e, out ax, out ay);
            ax += e.vx * t;
            ay += e.vy * t;
            double lim = Tuning.assistMaxLateral;
            double dvx = HMath.Clamp((ax - b.x) / t, -lim, lim);
            double dvy = HMath.Clamp((ay - b.y) / t, -lim, lim);
            b.vx = (float)(b.vx + ((dvx - b.vx) * k));
            b.vy = (float)(b.vy + ((dvy - b.vy) * k));
        }

        /// <summary>Picks the enemy the next bullet should chase: the closest one inside the aim-assist pyramid, preferring centred targets. -1 when none.</summary>
        int SelectTarget(float px, float py)
        {
            int best = -1;
            double bestScore = float.MaxValue;
            double cone = Tuning.assistConeTan;
            for (int i = 0; i < EnemyHigh; i++)
            {
                ref Enemy e = ref Enemies[i];
                if (!e.active || (e.flags & (EnemyFlags.Invulnerable | EnemyFlags.Hidden)) != 0) continue;
                if (e.z < Tuning.assistMinZ || e.z > Tuning.assistMaxZ) continue;
                double ax, ay;
                AimPoint(ref e, out ax, out ay);
                double dx = ax - px, dy = ay - py;
                double reach = cone * e.z + e.radius + 0.3;
                if (HMath.Abs(dx) > reach || HMath.Abs(dy) > reach) continue;
                double score = e.z + 3.0 * ((double)HMath.Abs(dx) + HMath.Abs(dy));
                if (e.type == EntityType.Guardian) score += 40.0;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }
            return best;
        }

        // ---------------------------------------------------------------- damage, kills, loot
        /// <summary>Applies damage to an enemy (shield layer first). Returns true when it died.</summary>
        bool DamageEnemy(int slot, float dmg)
        {
            ref Enemy e = ref Enemies[slot];
            if (!e.active || (e.flags & EnemyFlags.Invulnerable) != 0 || dmg <= 0f) return false;
            e.flash = 0f;
            int shieldBroke = 0;
            if (e.shield > 0f)
            {
                double absorb = e.shield < dmg ? e.shield : dmg;
                e.shield = (float)(e.shield - absorb);
                dmg = (float)(dmg - absorb);
                if (e.shield <= 0f) shieldBroke = 1;
            }
            e.hp = (float)((double)e.hp - dmg);
            Emit(EventType.EnemyHit, (int)e.type, e.uid, shieldBroke, e.x, e.y, e.z, dmg);
            if (e.hp <= 0f)
            {
                KillEnemy(slot);
                return true;
            }
            return false;
        }

        void KillEnemy(int slot)
        {
            ref Enemy e = ref Enemies[slot];
            int ti = (int)e.type;
            Kills++;
            ref PlayerState p = ref Player;
            p.combo++;
            p.comboTimer = Tuning.comboWindow;
            if (p.combo > MaxCombo) MaxCombo = p.combo;
            int combo = p.combo < 20 ? p.combo : 20;
            int score = (int)(Tuning.enemyScore[ti] * (1.0 + 0.05 * (double)combo) + 0.5);
            Score += score;
            int scrap = RollCount(Tuning.enemyScrap[ti]);
            lootCounts[(int)LootKind.Salvage] += scrap;
            if (e.type == EntityType.Elite && lootRng.NextFloat() < Tuning.componentChancePerElite) lootCounts[(int)LootKind.Component]++;
            if (e.type == EntityType.Mine) Emit(EventType.MineBlast, ti, e.uid, 0, e.x, e.y, e.z, 0f);
            if (e.type == EntityType.Asteroid) SplitAsteroid(ref e);
            if (e.type == EntityType.Guardian) OnBossKilled(ref e);
            else RollDrop(e.x, e.y, e.z, e.type == EntityType.Elite);
            if (++p.bombKillProgress >= Tuning.bombKillsPerCharge)
            {
                p.bombKillProgress = 0;
                if (p.bombs < p.maxBombs)
                {
                    p.bombs++;
                    Emit(EventType.BombCharged, 0, 0, p.bombs, p.x, p.y, 0f, 0f);
                }
            }
            Emit(EventType.EnemyKilled, ti, e.uid, scrap, e.x, e.y, e.z, score);
            FreeEnemy(slot);
        }

        int RollCount(float expected)
        {
            int n = (int)expected;
            double frac = (double)expected - n;
            if (frac > 0f && lootRng.NextFloat() < frac) n++;
            return n;
        }

        void SplitAsteroid(ref Enemy parent)
        {
            if (parent.variant >= (byte)AsteroidSize.Small || parent.z < Tuning.asteroidSplitMinZ) return;
            double toPlane = parent.z / ((double)Speed + parent.vzOwn);
            if (toPlane < 0.2f) return;
            double arrX, arrY;
            Paths.Position(parent, 0.0, out arrX, out arrY);
            double r = parent.radius;
            double drift = Tuning.asteroidChildDrift * r, off = Tuning.asteroidChildOffset * r;
            double pvx = parent.path == PathType.Line ? parent.p0 : 0f, pvy = parent.path == PathType.Line ? parent.p1 : 0f;
            for (int s = -1; s <= 1; s += 2)
            {
                int slot = AllocEnemy();
                if (slot < 0) break;
                ref Enemy c = ref Enemies[slot];
                c = default(Enemy);
                c.active = true;
                c.type = EntityType.Asteroid;
                c.variant = (byte)(parent.variant + 1);
                c.uid = nextUid++;
                c.flags = EnemyFlags.FromSplit;
                c.radius = (float)(r * Tuning.asteroidChildRadius);
                c.halfDepth = (float)((double)parent.halfDepth * Tuning.asteroidChildRadius);
                c.maxHp = c.hp = (float)((double)parent.maxHp * Tuning.asteroidChildHp);
                c.z = parent.z;
                c.prevZ = parent.z;
                c.vzOwn = parent.vzOwn;
                c.tArrive = (float)toPlane;
                c.spawnStep = StepIndex;
                c.path = PathType.Line;
                c.ox = (float)(arrX + s * (off + drift));
                c.oy = (float)arrY;
                c.p0 = (float)(pvx + s * drift / toPlane);
                c.p1 = (float)pvy;
                c.x = (float)(parent.x + s * off);
                c.y = parent.y;
                KillableSpawned++;
                Emit(EventType.AsteroidSplit, (int)parent.variant, c.uid, 0, c.x, c.y, c.z, 0f);
            }
        }

        /// <summary>Chance based pickup drop: crate, fuel, bomb, shield, overdrive; a pity counter guarantees a drop every few kills.</summary>
        void RollDrop(float x, float y, float z, bool elite)
        {
            killsSinceDrop++;
            double m = elite ? 3f : 1f;
            bool bombsFull = Player.bombs >= Player.maxBombs;
            bool shieldFull = Player.shield >= Player.maxShield;
            double wCrate = Tuning.dropChanceCrate * m;
            double wFuel = Tuning.dropChanceFuel * m;
            double wBomb = bombsFull ? 0f : Tuning.dropChanceBomb * m;
            double wShield = shieldFull ? 0f : Tuning.dropChanceShield * m;
            double wOver = Tuning.dropChanceOverdrive * m;
            double total = wCrate + wFuel + wBomb + wShield + wOver;
            double u = lootRng.NextFloat();
            bool pity = killsSinceDrop >= Tuning.dropPityKills;
            if (!pity && u >= total) return;
            if (pity) u = u * total;
            PickupType t;
            if (u < wCrate) t = PickupType.LootCrate;
            else if (u < wCrate + wFuel) t = PickupType.Fuel;
            else if (u < wCrate + wFuel + wBomb) t = PickupType.Bomb;
            else if (u < wCrate + wFuel + wBomb + wShield) t = PickupType.Shield;
            else t = PickupType.Overdrive;
            killsSinceDrop = 0;
            SpawnPickup(t, x, y, z > 6f ? z : 6f);
        }

        void CollectPickup(int slot)
        {
            ref Pickup pk = ref Pickups[slot];
            ref PlayerState p = ref Player;
            int count = 0;
            double kind = 0.0;
            switch (pk.type)
            {
                case PickupType.Bomb:
                    p.bombs += (int)Tuning.bombPickupCharges;
                    if (p.bombs > p.maxBombs) p.bombs = p.maxBombs;
                    break;
                case PickupType.Shield:
                    if (p.shield < p.maxShield) p.shield++;
                    break;
                case PickupType.Overdrive:
                    p.overdrive = Tuning.overdriveSeconds;
                    Emit(EventType.OverdriveStart, 0, 0, 0, p.x, p.y, 0f, Tuning.overdriveSeconds);
                    break;
                case PickupType.Fuel:
                    FuelCanisters++;
                    FuelBonus = (float)((double)FuelBonus + Tuning.fuelRefundPerCanister);
                    break;
                case PickupType.LootCrate:
                    count = RollCount((float)(Tuning.crateSalvageBase + (double)Tuning.crateSalvagePerTier * Route.tier));
                    lootCounts[(int)LootKind.Salvage] += count;
                    if (lootRng.NextFloat() < Tuning.crateComponentChance)
                    {
                        lootCounts[(int)LootKind.Component]++;
                        kind = 1.0;
                    }
                    if (lootRng.NextFloat() < (double)Tuning.crateCoreChancePerTier * Route.tier)
                    {
                        lootCounts[(int)LootKind.Core]++;
                        kind = 2.0;
                    }
                    break;
            }
            PickupsCollected++;
            Emit(EventType.PickupCollected, (int)pk.type, pk.uid, count, pk.x, pk.y, pk.z, (float)kind);
            FreePickup(slot);
        }
    }
}
