namespace Frontier.Hyperspace
{
    public sealed partial class HyperspaceRun
    {
        // ---------------------------------------------------------------- movement and timers
        void UpdatePlayer(in PlayerInput inp)
        {
            ref PlayerState p = ref Player;
            double w = Tuning.areaHalfWidth, h = Tuning.areaHalfHeight;
            double tx = HMath.Clamp(HMath.Finite(inp.targetX, p.targetX), -w, w);
            double ty = HMath.Clamp(HMath.Finite(inp.targetY, p.targetY), -h, h);
            p.targetX = (float)tx;
            p.targetY = (float)ty;

            double om = Tuning.followOmega;
            double ax = om * om * (tx - p.x) - 2.0 * om * p.vx;
            double ay = om * om * (ty - p.y) - 2.0 * om * p.vy;
            double am = HMath.Length(ax, ay);
            if (am > Tuning.playerMaxAccel)
            {
                double s = Tuning.playerMaxAccel / am;
                ax *= s;
                ay *= s;
            }
            p.vx = (float)(p.vx + (ax * Dt));
            p.vy = (float)(p.vy + (ay * Dt));
            double sp = HMath.Length(p.vx, p.vy);
            if (sp > p.maxSpeed)
            {
                double s = p.maxSpeed / sp;
                p.vx = (float)(p.vx * s);
                p.vy = (float)(p.vy * s);
            }
            p.x = (float)(p.x + ((double)p.vx * Dt));
            p.y = (float)(p.y + ((double)p.vy * Dt));
            if (p.x < -w) { p.x = (float)(-w); if (p.vx < 0f) p.vx = 0f; }
            else if (p.x > w) { p.x = (float)w; if (p.vx > 0f) p.vx = 0f; }
            if (p.y < -h) { p.y = (float)(-h); if (p.vy < 0f) p.vy = 0f; }
            else if (p.y > h) { p.y = (float)h; if (p.vy > 0f) p.vy = 0f; }

            if (p.invulnerable > 0f)
            {
                p.invulnerable = (float)((double)p.invulnerable - Dt);
                if (p.invulnerable < 0f) p.invulnerable = 0f;
            }
            if (p.overdrive > 0f)
            {
                p.overdrive = (float)((double)p.overdrive - Dt);
                if (p.overdrive <= 0f)
                {
                    p.overdrive = 0f;
                    Emit(EventType.OverdriveEnd, 0, 0, 0, p.x, p.y, 0f, 0f);
                }
            }
            if (p.bombCooldown > 0f) p.bombCooldown = (float)((double)p.bombCooldown - Dt);
            if (p.comboTimer > 0f)
            {
                p.comboTimer = (float)((double)p.comboTimer - Dt);
                if (p.comboTimer <= 0f) BreakCombo();
            }
            if (p.shield < p.maxShield)
            {
                p.shieldRegenTimer = (float)((double)p.shieldRegenTimer - Dt);
                if (p.shieldRegenTimer <= 0f)
                {
                    p.shield++;
                    Emit(EventType.ShieldRecharged, 0, 0, p.shield, p.x, p.y, 0f, 0f);
                    p.shieldRegenTimer = Ship.shieldRecharge;
                }
            }
            if (inp.bomb) TryBomb();
        }

        void BreakCombo()
        {
            if (Player.combo > 0) Emit(EventType.ComboBreak, 0, 0, Player.combo, Player.x, Player.y, 0f, 0f);
            Player.combo = 0;
            Player.comboTimer = 0f;
        }

        // ---------------------------------------------------------------- damage
        /// <summary>Applies one hit to the ship (shield first, then hull). Ignored while invulnerable. Returns true when the hit landed.</summary>
        bool DamagePlayer(float baseDamage, int source)
        {
            if (Ghost || State != RunState.Running) return false;
            ref PlayerState p = ref Player;
            if (p.invulnerable > 0f) return false;
            double dmg = (double)baseDamage * Route.enemyDamageScale;
            HitsTaken++;
            p.shieldRegenTimer = (float)((double)Tuning.shieldDelay + Ship.shieldRecharge);
            if (p.shield > 0)
            {
                p.shield--;
                p.invulnerable = (float)((double)Tuning.invulnSeconds * 0.7);
                Emit(EventType.ShieldBreak, source, 0, p.shield, p.x, p.y, 0f, (float)dmg);
            }
            else
            {
                HullHits++;
                p.hull = (float)(p.hull - dmg);
                p.invulnerable = Tuning.invulnSeconds;
                Emit(EventType.PlayerHit, source, 0, p.hull > 0f ? (int)((double)p.hull + 0.5) : 0, p.x, p.y, 0f, (float)dmg);
                if (p.hull <= 0f)
                {
                    p.hull = 0f;
                    State = RunState.Downed;
                    Defeated = true;
                    Emit(EventType.PlayerDowned, source, 0, RevivesUsed, p.x, p.y, 0f, 0f);
                }
            }
            BreakCombo();
            return true;
        }

        // ---------------------------------------------------------------- bomb
        void TryBomb()
        {
            ref PlayerState p = ref Player;
            if (Ghost || p.bombs <= 0 || p.bombCooldown > 0f) return;
            p.bombs--;
            p.bombCooldown = Tuning.bombCooldown;
            double dmg = Tuning.bombDamageBase + (double)Tuning.bombDamagePerShipDamage * Ship.damage;
            for (int i = 0; i < EnemyHigh; i++)
            {
                ref Enemy e = ref Enemies[i];
                if (!e.active || (e.flags & EnemyFlags.Invulnerable) != 0 || e.z > Tuning.bombRange || e.z < 0f) continue;
                double d = dmg;
                if (e.type == EntityType.Guardian)
                {
                    double cap = (double)e.maxHp * Tuning.bombBossFraction;
                    if (d > cap) d = cap;
                }
                DamageEnemy(i, (float)d);
            }
            for (int i = 0; i < BoltHigh; i++)
                if (Bolts[i].active) FreeBolt(i);
            Emit(EventType.BombUsed, 0, 0, p.bombs, p.x, p.y, 0f, (float)dmg);
        }

        // ---------------------------------------------------------------- revive / abandon
        /// <summary>Host decision after the hull hit 0: restores part of the hull, grants brief invulnerability and clears nearby threats. False when not downed or no revives are left.</summary>
        public bool Revive()
        {
            if (!CanRevive) return false;
            Replay?.RecordAction(1);
            RevivesUsed++;
            ref PlayerState p = ref Player;
            p.hull = (float)((double)p.maxHull * Tuning.reviveHullFraction);
            p.invulnerable = Tuning.reviveInvulnSeconds;
            if (p.shield < 1 && p.maxShield >= 1) p.shield = 1;
            for (int i = 0; i < BoltHigh; i++)
                if (Bolts[i].active) FreeBolt(i);
            for (int i = 0; i < EnemyHigh; i++)
            {
                ref Enemy e = ref Enemies[i];
                if (!e.active || e.type == EntityType.Guardian || e.z >= Tuning.reviveClearZ) continue;
                Emit(EventType.EnemyKilled, (int)e.type, e.uid, 0, e.x, e.y, e.z, 0f);
                FreeEnemy(i);
            }
            State = RunState.Running;
            Defeated = false;
            Emit(EventType.Revived, RevivesUsed, 0, RevivesUsed, p.x, p.y, 0f, p.hull);
            return true;
        }

        /// <summary>Ends the run without refund or loot (the host then simply finishes the trip). Valid while running or downed.</summary>
        public void Abandon()
        {
            if (State == RunState.Ended) return;
            Replay?.RecordAction(2);
            Abandoned = true;
            Defeated = true;
            FinishRun(false);
        }
    }
}
