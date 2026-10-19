using System;

namespace Frontier.Hyperspace
{
    /// <summary>The player's ship as the host derives it from upgrades (see the integration guide for the mapping).</summary>
    public struct ShipStats
    {
        /// <summary>Hull hit points. Enemy hits cost 8-60 hull depending on type and tier.</summary>
        public float hull;
        /// <summary>Number of hits the shield absorbs before it breaks.</summary>
        public int shield;
        /// <summary>Seconds to regain one shield charge once the recharge pause is over.</summary>
        public float shieldRecharge;
        /// <summary>Damage per bullet.</summary>
        public float damage;
        /// <summary>Bullets per second.</summary>
        public float fireRate;
        /// <summary>Extra enemies a bullet passes through.</summary>
        public int pierce;
        /// <summary>Pickup attraction radius in world units.</summary>
        public float magnet;
        /// <summary>Multiplier on the follow speed of the ship (1 = starter).</summary>
        public float handling;
        /// <summary>Maximum stored bombs.</summary>
        public int bombCapacity;

        public static ShipStats Starter()
        {
            return new ShipStats { hull = 100f, shield = 1, shieldRecharge = 10f, damage = 10f, fireRate = 7f, pierce = 0, magnet = 1.5f, handling = 1f, bombCapacity = 1 };
        }

        public static ShipStats Endgame()
        {
            return new ShipStats { hull = 450f, shield = 5, shieldRecharge = 3.5f, damage = 44f, fireRate = 16f, pierce = 3, magnet = 4.5f, handling = 1.55f, bombCapacity = 4 };
        }

        /// <summary>Interpolates between Starter (0) and Endgame (1); used by the balance harness and by tests.</summary>
        public static ShipStats AtProgress(float t)
        {
            t = HMath.Clamp01(t);
            ShipStats a = Starter(), b = Endgame();
            return new ShipStats
            {
                hull = (float)HMath.Lerp(a.hull, b.hull, t),
                shield = (int)(HMath.Lerp(a.shield, b.shield, t) + 0.5f),
                shieldRecharge = (float)HMath.Lerp(a.shieldRecharge, b.shieldRecharge, t),
                damage = (float)HMath.Lerp(a.damage, b.damage, t),
                fireRate = (float)HMath.Lerp(a.fireRate, b.fireRate, t),
                pierce = (int)(HMath.Lerp(a.pierce, b.pierce, t) + 0.5f),
                magnet = (float)HMath.Lerp(a.magnet, b.magnet, t),
                handling = (float)HMath.Lerp(a.handling, b.handling, t),
                bombCapacity = (int)(HMath.Lerp(a.bombCapacity, b.bombCapacity, t) + 0.5f)
            };
        }

        /// <summary>Single power rating (starter = 1) that feeds route difficulty. See <see cref="HyperspaceTuning.ShipPower"/>.</summary>
        public float Power(HyperspaceTuning tuning)
        {
            return tuning.ShipPower(this);
        }
    }

    /// <summary>
    /// One sample of host input per fixed step. targetX/targetY are the point the ship should follow, in play-area world units
    /// (x right, y up; the area is [-W, W] x [-H, H], see HyperspaceTuning.areaHalfWidth/areaHalfHeight). Values outside the area are clamped; NaN/Inf are ignored.
    /// bomb is a level: the bomb fires on the step it is true and a charge is available (a held button does not retrigger inside the bomb cooldown).
    /// </summary>
    public struct PlayerInput
    {
        public float targetX;
        public float targetY;
        public bool bomb;

        public PlayerInput(float targetX, float targetY, bool bomb)
        {
            this.targetX = targetX;
            this.targetY = targetY;
            this.bomb = bomb;
        }
    }

    /// <summary>Everything the host supplies to start a run. The whole encounter schedule is a pure function of this struct, the ship stats and the tuning.</summary>
    public struct RunParams
    {
        public ulong seed;
        /// <summary>Length of the hop in light-years (same-system hops: pass a small value such as 1).</summary>
        public float distanceLy;
        /// <summary>Requested total run length in seconds; zero keeps the distance-based default.</summary>
        public float plannedDurationSeconds;
        /// <summary>Destination danger rating (PlanetData.danger, roughly 0.6 + 1.1 x tier).</summary>
        public float danger;
        /// <summary>Destination tier 0..12 (higher is clamped).</summary>
        public int tier;
        /// <summary>ShipStats.Power() of the ship that flies the run (starter = 1).</summary>
        public float shipPower;
        public RunMode mode;

        public RunParams(ulong seed, float distanceLy, float danger, int tier, float shipPower, RunMode mode)
            : this(seed, distanceLy, danger, tier, shipPower, mode, 0f)
        {
        }

        public RunParams(ulong seed, float distanceLy, float danger, int tier, float shipPower, RunMode mode, float plannedDurationSeconds)
        {
            this.seed = seed;
            this.distanceLy = distanceLy;
            this.plannedDurationSeconds = plannedDurationSeconds;
            this.danger = danger;
            this.tier = tier;
            this.shipPower = shipPower;
            this.mode = mode;
        }
    }

    public struct LootDrop
    {
        public LootKind kind;
        public int count;
    }

    /// <summary>Outcome of a run. Built by HyperspaceRun.BuildResult(); safe to call at any time, final once State == Ended.</summary>
    public sealed class RunResult
    {
        public Grade grade;
        /// <summary>0..1 performance score behind the grade; the host's yield multiplier is 0.4 + 1.1 x quality (see yieldMultiplier).</summary>
        public float quality;
        public float yieldMultiplier;
        public int killCount;
        public int hitsTaken;
        public int maxCombo;
        public bool cleanRun;
        /// <summary>0..1: the host refunds floor(fuelCost x travelRefundMax x fuelRefundFraction). Always 0 when the run was abandoned after defeat.</summary>
        public float fuelRefundFraction;
        /// <summary>Raw loot counts (Salvage/Component/Core); the host applies yieldMultiplier. Empty when the run was abandoned after defeat.</summary>
        public LootDrop[] lootList;
        public bool reachedEnd;
        public int revivesUsed;
        public bool bossDefeated;
        public bool defeated;
        public int pickupsCollected;
        public int score;
        public int xp;
        public float elapsedSeconds;
        public float durationSeconds;
        public ulong digest;
    }
}
