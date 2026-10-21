using System;

namespace Frontier.Hyperspace
{
    /// <summary>
    /// Every number of Hyperspace Run lives here with its default (units: world units "u", seconds "s"). The host may overwrite any field from Remote Config
    /// via <see cref="TrySet"/>; a run copies nothing, so create one instance per run (or per session) and do not change it while a run is in progress.
    /// Per-type arrays are indexed by (int)EntityType and addressed by key as "enemyHp.Drone".
    /// </summary>
    [Serializable]
    public sealed partial class HyperspaceTuning
    {
        /// <summary>Fixed simulation step. The host runs Step() this often with an accumulator; never use the frame delta inside the simulation.</summary>
        public const float Dt = 1f / 60f;
        public const int StepsPerSecond = 60;
        public const int EntityTypeCount = 11;

        // ---------------------------------------------------------------- world
        /// <summary>Play area is x in [-W, W], y in [-H, H] (portrait friendly: taller than wide).</summary>
        public float areaHalfWidth = 2.7f;
        public float areaHalfHeight = 3.4f;
        /// <summary>Entities appear at this z (ship is at z = 0, the world scrolls toward -z).</summary>
        public float spawnDistance = 140f;
        public float despawnZ = -12f;
        /// <summary>Authoring speed: template z offsets are converted to seconds with this speed, so encounter timing does not depend on the route speed.</summary>
        public float nominalSpeed = 30f;
        public float speedBase = 25f;
        public float speedPerTier = 1.3f;
        public float speedPerDistanceTerm = 4f;
        public float speedStartFraction = 0.62f;
        public float speedRampSeconds = 5f;
        public float speedBrakeSeconds = 2.5f;
        public float speedBrakeFraction = 0.5f;

        // ---------------------------------------------------------------- player
        public float playerRadius = 0.30f;
        public float playerHalfDepth = 0.6f;
        /// <summary>Natural frequency (rad/s) of the critically damped follow; 15 is about a 100 ms lag.</summary>
        public float followOmega = 15f;
        /// <summary>Maximum ship speed (u/s) at handling 1. The fairness solver uses fairnessSpeedFraction of it.</summary>
        public float baseMaxSpeed = 9.5f;
        /// <summary>Acceleration cap (u/s^2) of the follow motion; 140 reaches top speed in about 70 ms.</summary>
        public float playerMaxAccel = 140f;
        public float invulnSeconds = 1.1f;
        public float shieldDelay = 3f;
        public float overdriveSeconds = 6f;
        public float overdriveFireMul = 1.6f;
        public float overdriveDamageMul = 1.2f;
        public int overdrivePierce = 2;
        public float reviveHullFraction = 0.5f;
        public float reviveInvulnSeconds = 3f;
        public float reviveClearZ = 75f;
        public int maxRevives = 2;
        public float startShieldFraction = 1f;
        public float startBombFraction = 1f;

        // ---------------------------------------------------------------- weapons
        public float bulletSpeed = 110f;
        public float bulletRadius = 0.16f;
        public float bulletRange = 135f;
        public float bombDamageBase = 40f;
        public float bombDamagePerShipDamage = 6f;
        public float bombBossFraction = 0.18f;
        public float bombSpeed = 70f;
        public float bombBlastRadius = 7f;
        public float bombCooldown = 0.8f;
        public int bombKillsPerCharge = 18;

        // ---------------------------------------------------------------- pickups
        public float pickupRadius = 0.55f;
        public float magnetPullSpeed = 16f;
        public float fuelRefundPerCanister = 0.05f;
        public float shieldPickupHeal = 0f;
        public float bombPickupCharges = 1f;

        // ---------------------------------------------------------------- enemies (index = (int)EntityType; 0 unused)
        public float[] enemyRadius = { 0f, 0.42f, 0.40f, 0.38f, 0.70f, 0.45f, 1.6f, 0.50f, 0.40f, 0.85f, 1.8f };
        public float[] enemyHalfDepth = { 0f, 0.45f, 0.45f, 0.45f, 0.8f, 0.5f, 1.2f, 0.5f, 0.6f, 0.9f, 2.0f };
        public float[] enemyHp = { 0f, 12f, 12f, 10f, 40f, 30f, 70f, 1e9f, 14f, 90f, 700f };
        /// <summary>Hull damage of a collision / bolt hit of this type at tier 0 (scaled by damageScale of the route).</summary>
        public float[] enemyDamage = { 0f, 14f, 14f, 14f, 16f, 26f, 22f, 20f, 22f, 24f, 30f };
        public float[] enemyScore = { 0f, 10f, 15f, 20f, 60f, 5f, 25f, 0f, 40f, 120f, 1500f };
        /// <summary>Expected Salvage dropped on a kill.</summary>
        public float[] enemyScrap = { 0f, 0.8f, 1f, 1.2f, 3f, 0f, 2f, 0f, 1.5f, 6f, 40f };
        /// <summary>Threat points one enemy of this type counts for in the director budget (templates carry their own authored cost).</summary>
        public float[] enemyCost = { 0f, 1f, 1.5f, 1.8f, 4f, 1.2f, 3f, 3.5f, 3.5f, 7f, 40f };
        public float hpPerTier = 1.6f;
        public float hpPerDanger = 0.02f;
        public float damagePerTier = 0.25f;
        public float asteroidSplitMinZ = 16f;
        public float asteroidChildRadius = 0.5f;
        public float asteroidChildHp = 0.45f;
        public float asteroidChildOffset = 0.25f;
        public float asteroidChildDrift = 0.22f;
        public float eliteShieldHp = 60f;
        public float hitFlashSeconds = 0.12f;

        // ---------------------------------------------------------------- turret / bolts
        public float boltSpeed = 40f;
        public float boltSpeedPerTier = 0.04f;
        public float boltRadius = 0.22f;
        public float boltDamage = 12f;
        public float turretRange = 96f;
        public float turretTelegraph = 0.85f;
        public float turretTelegraphMin = 0.6f;
        public float turretCycle = 2.6f;
        /// <summary>Distance (at the ship plane) between the bolts of a Fan volley.</summary>
        public float turretFanSpread = 1.0f;
        public int turretFanCount = 3;
        /// <summary>From this tier on a Fan volley has two more bolts.</summary>
        public int turretFanTier = 8;

        // ---------------------------------------------------------------- mines / gates / seekers
        public float mineArmRadius = 2.1f;
        public float mineArmZ = 20f;
        public float mineBlastRadius = 1.25f;
        public float mineDetonateZ = 1.0f;
        public float mineBlastHalfDepth = 1.6f;
        public float gateTelegraphSeconds = 1.6f;
        public float gateHalfDepth = 0.5f;
        public float gateWallThickness = 0.5f;
        public float seekerClosing = 14f;
        public float seekerLateralSpeed = 3.6f;
        public float seekerLateralAccel = 16f;
        public float seekerCommitSeconds = 0.8f;
        public float seekerCommitMin = 0.62f;
        public float seekerSpeedPerTier = 0.03f;
        public float seekerTurnWarmup = 0.35f;
        /// <summary>Lateral speed of the committed (final, straight) flight toward the locked point.</summary>
        public float seekerCommitSpeed = 4.5f;
        /// <summary>Extra radius the fairness solver adds around a seeker's locked point to cover tracking error.</summary>
        public float seekerLockPad = 0.5f;

        // ---------------------------------------------------------------- boss
        public float bossZ = 62f;
        public float bossArriveSeconds = 2.2f;
        /// <summary>Boss hull is sized so a ship that lands every bullet needs about bossFightSeconds + bossFightPerTier x tier (before armor/weak-point effects); bossHitRatio is the share that lands.</summary>
        public float bossFightSeconds = 11f;
        public float bossFightPerTier = 0.8f;
        public float bossHitRatio = 0.55f;
        public float bossMaxSeconds = 24f;
        public float bossWeakPointDamageMul = 3f;
        public float bossArmorDamageMul = 0.35f;
        public float bossWeakPointRadius = 0.6f;
        public float bossPhase2Hp = 0.66f;
        public float bossPhase3Hp = 0.33f;
        public float bossTransitionSeconds = 2.0f;
        public float bossAttackGap = 1.0f;
        public float bossLeaveSpeed = 40f;
        public float bossMinDistanceLy = 250f;
        public int bossMinTier = 6;
    }
}
