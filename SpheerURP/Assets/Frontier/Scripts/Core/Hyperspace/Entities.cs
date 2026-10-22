namespace Frontier.Hyperspace
{
    /// <summary>
    /// One enemy, hazard or boss in a fixed pool slot (<see cref="HyperspaceRun.Enemies"/>). Read it directly from the array (no copy needed): iterate
    /// 0..EnemyHigh-1 and skip slots with active == false. A slot is reused after despawn, so use uid (unique per spawn) to decide whether a pooled view still
    /// belongs to the same enemy. Coordinates are in the ship frame: x right, y up, z forward (distance ahead of the ship).
    /// </summary>
    public struct Enemy
    {
        public bool active;
        public EntityType type;
        /// <summary>AsteroidSize, GateKind or TurretMode depending on type.</summary>
        public byte variant;
        public int uid;
        /// <summary>EnemyFlags bit mask.</summary>
        public int flags;
        public float x, y, z;
        /// <summary>Current lateral velocity (u/s), handy for banking and prediction.</summary>
        public float vx, vy;
        /// <summary>Own approach speed in addition to the world scroll (u/s).</summary>
        public float vzOwn;
        public float radius;
        public float halfDepth;
        public float hp, maxHp;
        public float shield, maxShield;
        /// <summary>Seconds since the enemy appeared.</summary>
        public float age;
        /// <summary>Seconds after spawn at which the enemy nominally crosses the ship plane (paths are functions of age - tArrive).</summary>
        public float tArrive;
        public PathType path;
        public float ox, oy;
        public float p0, p1, p2, p3, p4, p5;
        public float timer;
        public int state;
        /// <summary>Turret lock point, seeker target, or gate gap centre (x) depending on type.</summary>
        public float aimX, aimY;
        /// <summary>Seconds since the last bullet hit (renderer hit flash while below tuning.hitFlashSeconds).</summary>
        public float flash;
        public int spawnStep;
        public int lockStep;
        public float prevZ;
    }

    /// <summary>A player bullet (<see cref="HyperspaceRun.Bullets"/>), in the ship frame; vz is relative to the ship, not the world.</summary>
    public struct Bullet
    {
        public bool active;
        public float x, y, z;
        public float vx, vy, vz;
        public float damage;
        public int pierceLeft;
        public int targetUid;
        public int targetSlot;
        public int lastHitUid;
        public int spawnStep;
    }

    /// <summary>An enemy projectile (<see cref="HyperspaceRun.Bolts"/>). Velocity is relative to the ship (vz is negative: toward the ship).</summary>
    public struct Bolt
    {
        public bool active;
        public int uid;
        public float x, y, z;
        public float vx, vy, vz;
        public float radius;
        public float damage;
        public EntityType source;
        /// <summary>Step at which the aim point was locked (the telegraph started).</summary>
        public int lockStep;
    }

    public struct Pickup
    {
        public bool active;
        public PickupType type;
        public int uid;
        public float x, y, z;
        public float vx, vy;
        public float age;
        public bool magnetized;
    }

    /// <summary>One entry of the precomputed encounter schedule: what appears at which step. Built by the director, consumed by the run.</summary>
    public struct SpawnRecord
    {
        public int step;
        /// <summary>Step at which the entity crosses the ship plane (z = 0) if nobody shoots it; the solver and the renders use it.</summary>
        public int arrivalStep;
        /// <summary>Distance ahead of the ship at which the entity appears.</summary>
        public float z0;
        public EntityType type;
        public PickupType pickup;
        public byte variant;
        public int flags;
        public float x, y;
        public float vzOwn;
        public PathType path;
        public float p0, p1, p2, p3, p4, p5;
        public short template;
        public short group;
    }

    /// <summary>Player state, read by the HUD and the renderer.</summary>
    public struct PlayerState
    {
        public float x, y;
        public float vx, vy;
        public float hull, maxHull;
        public int shield, maxShield;
        public float shieldRegenTimer;
        public float invulnerable;
        public float overdrive;
        public int bombs, maxBombs;
        public int bombKillProgress;
        public float bombCooldown;
        public float fireCooldown;
        public int combo;
        public float comboTimer;
        public float targetX, targetY;
        public float maxSpeed;
    }
}
