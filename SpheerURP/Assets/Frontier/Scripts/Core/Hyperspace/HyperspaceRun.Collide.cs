namespace Frontier.Hyperspace
{
    public sealed partial class HyperspaceRun
    {
        const int BucketCount = 24;
        const float BucketSize = 8f;
        const float MaxEnemyHalfDepth = 3.2f;

        int[] bucketStart;
        int[] bucketFill;
        int[] bucketItems;

        void InitBroadPhase()
        {
            bucketStart = new int[BucketCount + 1];
            bucketFill = new int[BucketCount];
            bucketItems = new int[Tuning.maxEnemies];
        }

        static int BucketOf(float z)
        {
            int k = (int)(((double)z + BucketSize) * (1f / BucketSize));
            return k < 0 ? 0 : (k >= BucketCount ? BucketCount - 1 : k);
        }

        /// <summary>Counting sort of the live enemies into z buckets of 8 u (O(enemies), no allocation). Bullets and the ship only visit the buckets their swept z range touches.</summary>
        void BuildBroadPhase()
        {
            for (int b = 0; b <= BucketCount; b++) bucketStart[b] = 0;
            for (int i = 0; i < EnemyHigh; i++)
            {
                if (!Enemies[i].active) continue;
                bucketStart[BucketOf(Enemies[i].z) + 1]++;
            }
            for (int b = 0; b < BucketCount; b++)
            {
                bucketStart[b + 1] += bucketStart[b];
                bucketFill[b] = bucketStart[b];
            }
            for (int i = 0; i < EnemyHigh; i++)
            {
                if (!Enemies[i].active) continue;
                bucketItems[bucketFill[BucketOf(Enemies[i].z)]++] = i;
            }
        }

        // ---------------------------------------------------------------- bullets against enemies
        void CollideBullets()
        {
            double br = Tuning.bulletRadius;
            for (int i = 0; i < BulletHigh; i++)
            {
                ref Bullet b = ref Bullets[i];
                if (!b.active) continue;
                double z1 = b.z, z0 = b.z - (double)b.vz * Dt;
                int lo = BucketOf((float)(z0 - MaxEnemyHalfDepth - br)), hi = BucketOf((float)(z1 + MaxEnemyHalfDepth + br));
                bool consumed = false;
                for (int bk = lo; bk <= hi && !consumed; bk++)
                {
                    int end = bucketStart[bk + 1];
                    for (int n = bucketStart[bk]; n < end; n++)
                    {
                        int ei = bucketItems[n];
                        ref Enemy e = ref Enemies[ei];
                        if (!e.active || (e.flags & EnemyFlags.Invulnerable) != 0 || e.uid == b.lastHitUid) continue;
                        double ez0 = (double)(e.z < e.prevZ ? e.z : e.prevZ) - e.halfDepth - br;
                        double ez1 = (double)(e.z > e.prevZ ? e.z : e.prevZ) + e.halfDepth + br;
                        if (z1 < ez0 || z0 > ez1) continue;
                        double mult = 1.0;
                        if (e.type == EntityType.Guardian)
                        {
                            mult = BossBulletMultiplier(ref e, b.x, b.y, (float)br);
                            if (mult <= 0f) continue;
                        }
                        else
                        {
                            double rr = e.radius + br;
                            if (HMath.Dist2(b.x, b.y, e.x, e.y) > rr * rr) continue;
                        }
                        b.lastHitUid = e.uid;
                        DamageEnemy(ei, (float)(b.damage * mult));
                        if (b.pierceLeft > 0) b.pierceLeft--;
                        else
                        {
                            consumed = true;
                            break;
                        }
                    }
                }
                if (consumed) FreeBullet(i);
            }
        }

        // ---------------------------------------------------------------- the ship against everything
        static bool ConsumedOnImpact(EntityType t)
        {
            return t == EntityType.Drone || t == EntityType.Zigzag || t == EntityType.Swooper || t == EntityType.Seeker || t == EntityType.Asteroid;
        }

        void CollidePlayer()
        {
            ref PlayerState p = ref Player;
            double pr = Tuning.playerRadius, hz = Tuning.playerHalfDepth;
            for (int bk = 0; bk <= 2; bk++)
            {
                int end = bucketStart[bk + 1];
                for (int n = bucketStart[bk]; n < end; n++)
                {
                    int i = bucketItems[n];
                    ref Enemy e = ref Enemies[i];
                    if (!e.active || (e.flags & EnemyFlags.Spent) != 0 || e.type == EntityType.Guardian) continue;
                    double lo = (double)(e.z < e.prevZ ? e.z : e.prevZ) - e.halfDepth - hz;
                    double hi = (double)(e.z > e.prevZ ? e.z : e.prevZ) + e.halfDepth + hz;
                    if (lo > 0f || hi < 0f) continue;
                    if (e.type == EntityType.Mine)
                    {
                        CollideMine(i, ref e);
                        continue;
                    }
                    Hazard h = e.type == EntityType.LaserGate ? Hazards.Gate(e) : Hazard.Disc(e.x, e.y, e.radius);
                    if (Ghost)
                    {
                        if (Log != null) Log.AddHazard(StepIndex, h, e.type, e.uid);
                        continue;
                    }
                    if (State != RunState.Running || p.invulnerable > 0f || !h.Hits(p.x, p.y, (float)pr)) continue;
                    DamagePlayer(Tuning.enemyDamage[(int)e.type], (int)e.type);
                    if (ConsumedOnImpact(e.type))
                    {
                        Emit(EventType.EnemyKilled, (int)e.type, e.uid, 0, e.x, e.y, e.z, 0f);
                        FreeEnemy(i);
                    }
                    else e.flags |= EnemyFlags.Spent;
                }
            }

            if (Ghost || State != RunState.Running) return;

            for (int i = 0; i < BoltHigh; i++)
            {
                ref Bolt b = ref Bolts[i];
                if (!b.active) continue;
                double zPrev = b.z - (double)b.vz * Dt;
                double lo = b.z < zPrev ? b.z : zPrev, hi = b.z > zPrev ? b.z : zPrev;
                double w = hz + b.radius;
                if (hi < -w || lo > w) continue;
                double rr = b.radius + pr;
                if (p.invulnerable > 0f || HMath.Dist2(b.x, b.y, p.x, p.y) > rr * rr) continue;
                DamagePlayer(b.damage, (int)b.source);
                FreeBolt(i);
                if (State != RunState.Running) return;
            }

            for (int i = 0; i < PickupHigh; i++)
            {
                ref Pickup pk = ref Pickups[i];
                if (!pk.active) continue;
                double zPrev = pk.z + (double)Speed * Dt;
                if (zPrev < -hz - 0.3 || pk.z > hz + 0.3) continue;
                double rr = Tuning.pickupRadius + pr;
                if (HMath.Dist2(pk.x, pk.y, p.x, p.y) <= rr * rr) CollectPickup(i);
            }
        }

        void CollideMine(int slot, ref Enemy e)
        {
            if (e.prevZ <= Tuning.mineDetonateZ || e.z > Tuning.mineDetonateZ) return;
            bool armed = e.state != 0 || Ghost;
            if (!armed) return;
            Hazard h = Hazard.Disc(e.x, e.y, Tuning.mineBlastRadius);
            if (Ghost)
            {
                if (Log != null) Log.AddHazard(StepIndex, h, e.type, e.uid);
                return;
            }
            Emit(EventType.MineBlast, (int)e.type, e.uid, 0, e.x, e.y, e.z, Tuning.mineBlastRadius);
            if (State == RunState.Running && Player.invulnerable <= 0f && h.Hits(Player.x, Player.y, Tuning.playerRadius))
                DamagePlayer(Tuning.enemyDamage[(int)EntityType.Mine], (int)EntityType.Mine);
            FreeEnemy(slot);
        }

        /// <summary>The hazard an enemy presents to the ship plane right now (for tools and the bot; the run itself uses the same function inline).</summary>
        public static Hazard HazardOf(in Enemy e)
        {
            return e.type == EntityType.LaserGate ? Hazards.Gate(e) : Hazard.Disc(e.x, e.y, e.radius);
        }

        /// <summary>Optional recorder used by the fairness solver (set while Ghost is true).</summary>
        public HazardLog Log;
    }
}
