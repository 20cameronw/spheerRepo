namespace Frontier.Hyperspace
{
    /// <summary>Enemy and hazard kinds that live in <see cref="HyperspaceRun.Enemies"/>.</summary>
    public enum EntityType : byte
    {
        None = 0,
        Drone = 1,
        Zigzag = 2,
        Swooper = 3,
        TurretPod = 4,
        Mine = 5,
        Asteroid = 6,
        LaserGate = 7,
        Seeker = 8,
        Elite = 9,
        Guardian = 10
    }

    public enum PickupType : byte
    {
        None = 0,
        Bomb = 1,
        Shield = 2,
        Overdrive = 3,
        Fuel = 4,
        LootCrate = 5
    }

    /// <summary>How an enemy moves laterally while the world scrolls toward the ship. See <see cref="Paths"/>.</summary>
    public enum PathType : byte
    {
        /// <summary>Fixed in the world: only the scroll moves it.</summary>
        Static = 0,
        /// <summary>Constant lateral velocity (p0, p1).</summary>
        Line = 1,
        /// <summary>x = ox + p0 sin(p1 t + p2), y = oy + p3 sin(p1 t + p2 + p4).</summary>
        Sine = 2,
        /// <summary>Quadratic Bezier from the spawn point through control (p0, p1) to (p2, p3) in p4 seconds, then straight on.</summary>
        Curve = 3,
        /// <summary>Circle (or spiral) around the spawn point: radius p0 + p3 t, angular speed p1, phase p2.</summary>
        Orbit = 4,
        /// <summary>Stays at a fixed place relative to the ship (boss).</summary>
        Hold = 5,
        /// <summary>Steers toward the ship until it commits (seeker missiles).</summary>
        Homing = 6
    }

    public enum GateKind : byte
    {
        /// <summary>Full-height wall with a vertical slit: the player slides sideways into the gap.</summary>
        Slit = 0,
        /// <summary>Full-width wall with a horizontal slot: the player moves up or down into the gap.</summary>
        Slot = 1,
        /// <summary>Wall with a round hole.</summary>
        Ring = 2,
        /// <summary>A rotating bar through a point; everything except the bar is safe.</summary>
        Bar = 3
    }

    public enum AsteroidSize : byte { Large = 0, Medium = 1, Small = 2 }

    public enum TurretMode : byte
    {
        /// <summary>Locks the ship position, telegraphs a ring there, then fires one bolt at it.</summary>
        Aimed = 0,
        /// <summary>Telegraphed fan of bolts at fixed angles.</summary>
        Fan = 1,
        /// <summary>Telegraphed straight volley down its own lane.</summary>
        Lane = 2
    }

    public enum EventType : byte
    {
        None = 0,
        EnemyHit = 1,
        EnemyKilled = 2,
        PlayerHit = 3,
        ShieldBreak = 4,
        ShieldRecharged = 5,
        PickupCollected = 6,
        BombUsed = 7,
        GateTelegraph = 8,
        BossPhase = 9,
        WaveCleared = 10,
        RunEnded = 11,
        PlayerDowned = 12,
        Revived = 13,
        OverdriveStart = 14,
        OverdriveEnd = 15,
        TurretTelegraph = 16,
        SeekerLaunched = 17,
        MineArmed = 18,
        MineBlast = 19,
        AsteroidSplit = 20,
        ComboBreak = 21,
        BossWarning = 22,
        BossDefeated = 23,
        BossWeakPoint = 24,
        PickupSpawned = 25,
        ShotFired = 26,
        BombCharged = 27
    }

    public enum Grade : byte { C = 0, B = 1, A = 2, S = 3 }

    public enum RunMode : byte
    {
        Normal = 0,
        /// <summary>Accessibility assist: smaller budget, softer hits, quality capped so it cannot out-earn Normal.</summary>
        Assist = 1,
        /// <summary>Weekly-board style: bigger budget and a small quality bonus.</summary>
        Challenge = 2
    }

    public enum LootKind : byte
    {
        /// <summary>Common scrap; the host maps it to its basic salvage resource.</summary>
        Salvage = 0,
        Component = 1,
        Core = 2
    }

    public enum RunState : byte
    {
        Running = 0,
        /// <summary>Hull reached 0; the world is frozen until the host calls Revive() or Abandon().</summary>
        Downed = 1,
        Ended = 2
    }

    /// <summary>Bit flags of <see cref="Enemy.flags"/>.</summary>
    public static class EnemyFlags
    {
        public const int Invulnerable = 1;
        public const int Boss = 2;
        public const int NoLoot = 4;
        public const int Armed = 8;
        public const int FromSplit = 16;
        public const int WeakPointExposed = 32;
        public const int Telegraphing = 64;
        public const int Hidden = 128;
        /// <summary>The enemy already hit the ship once on this pass (gates, elites, boss attacks) and cannot hit it again.</summary>
        public const int Spent = 256;
    }
}
