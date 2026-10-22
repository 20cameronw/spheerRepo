namespace Frontier.Hyperspace
{
    public sealed partial class HyperspaceTuning
    {
        // ---------------------------------------------------------------- route length
        /// <summary>duration = durationBase + durationScale x distanceLy^durationExp, clamped: ~26 s at 5 ly, ~35 s at 50 ly, ~64 s at 800 ly, ~90 s at 3000 ly.</summary>
        public float durationBase = 18f;
        public float durationScale = 4.4f;
        public float durationExp = 0.35f;
        public float durationMin = 20f;
        public float durationMax = 92f;

        // ---------------------------------------------------------------- difficulty and budget
        public float dangerRef = 14f;
        public float distanceRefLy = 3000f;
        /// <summary>Threat points per second at the peak of a tier-0, distance-0 route with a starter ship.</summary>
        public float budgetRateBase = 1.0f;
        public float budgetPerTier = 0.16f;
        public float budgetPerDanger = 0.006f;
        public float budgetDistanceSpan = 0.3f;
        public float budgetPerPowerLog = 0.08f;
        public float powerLogCap = 4f;
        public float expectedPowerBase = 1f;
        public float expectedPowerPerTier = 0.75f;
        public float assistBudgetScale = 0.65f;
        public float assistDamageScale = 0.6f;
        public float assistQualityCap = 0.85f;
        public float challengeBudgetScale = 1.3f;
        public float challengeQualityBonus = 0.05f;
        /// <summary>Tiers below this get the gentle intro (first encounter later, extra calm).</summary>
        public int gentleTiers = 2;

        // ---------------------------------------------------------------- tier scaling of authored encounters (the fairness solver verifies every template at every tier with these values)
        /// <summary>Gate openings shrink by this fraction of their authored size per tier (floor gapScaleMin): 0.82 at tier 2, 0.73 at tier 3, 0.58 from tier 5 on.</summary>
        public float gapScalePerTier = 0.14f;
        public float gapScaleMin = 0.58f;
        /// <summary>Template z offsets are compressed by this fraction per tier (floor timeScaleMin): the same formation arrives faster and demands more lateral speed.</summary>
        public float timeScalePerTier = 0.07f;
        public float timeScaleMin = 0.74f;
        public float zigAmpPerTier = 0.04f;
        public float zigRatePerTier = 0.06f;
        public float gateSlidePerTier = 0.05f;
        public float gateSpinPerTier = 0.10f;
        public float swooperCurvePerTier = 0.02f;
        public float linePathSpeedPerTier = 0.07f;
        public float dartSpeedPerTier = 0.08f;

        // ---------------------------------------------------------------- pacing
        public float introSeconds = 4.2f;
        public float outroSeconds = 3.6f;
        public float waveSeconds = 24f;
        public float calmFraction = 0.12f;
        public float buildFraction = 0.38f;
        public float peakFraction = 0.38f;
        public float calmRate = 0.35f;
        public float buildRate = 0.85f;
        public float peakRate = 1.35f;
        public float relaxRate = 0.30f;
        public float gapTier0 = 2.3f;
        public float gapTier12 = 1.35f;
        public float bossReserveFraction = 0.30f;
        public float bossIntroSeconds = 3.5f;

        // ---------------------------------------------------------------- fairness contract (used by the solver, the director and the tests)
        /// <summary>Human reaction budget (s): time to notice a telegraph and start moving.</summary>
        public float reactionBudget = 0.30f;
        /// <summary>The solver assumes the player only uses this fraction of the starter ship's top speed.</summary>
        public float fairnessSpeedFraction = 0.80f;
        /// <summary>Extra clearance (u) the solver keeps around every hazard, on top of the real radii. Tier 0 value; it shrinks to fairnessMarginDeep over marginRampTiers tiers (deeper routes demand a more precise ship).</summary>
        public float fairnessMargin = 0.22f;
        public float fairnessMarginDeep = 0.10f;
        public float marginRampTiers = 4f;
        /// <summary>Spare room (u) the instantiator keeps on top of ship radius + margin when it narrows a gate; shrinks the same way.</summary>
        public float gapSlack = 0.25f;
        public float gapSlackDeep = 0.12f;
        /// <summary>The player can be anywhere in the area this long (s) before the first hazard of a template arrives.</summary>
        public float entryLead = 1.35f;

        // ---------------------------------------------------------------- results
        public float gradeS = 0.90f;
        /// <summary>A needs a clean-ish run: one shield-absorbed hit still makes it, a hull hit does not.</summary>
        public float gradeA = 0.78f;
        public float gradeB = 0.55f;
        public float qualityBase = 0.42f;
        public float qualityKills = 0.20f;
        /// <summary>Kill ratio at which the kill component is full (tougher enemies at depth make 100% kills unrealistic).</summary>
        public float killRatioForFull = 0.6f;
        public float qualityCombo = 0.10f;
        public float qualityPickups = 0.10f;
        public float pickupRatioForFull = 0.5f;
        /// <summary>Bonus for a run in which nothing (not even a shield) was hit; the main skill reward at depth.</summary>
        public float qualityClean = 0.18f;
        public float qualityPerHit = 0.07f;
        public float qualityHitCap = 0.50f;
        public float qualityPerRevive = 0.10f;
        public int comboForFullBonus = 12;
        public float comboWindow = 2.4f;
        public float refundQualityPower = 1f;
        public float yieldBase = 0.4f;
        public float yieldPerQuality = 1.1f;
        public float xpPerKill = 2f;
        public float xpPerTier = 0.5f;

        // ---------------------------------------------------------------- loot
        public float dropChanceShield = 0.025f;
        public float dropChanceBomb = 0.035f;
        public float dropChanceOverdrive = 0.025f;
        public float dropChanceFuel = 0.05f;
        public float dropChanceCrate = 0.07f;
        public int dropPityKills = 18;
        public float crateComponentChance = 0.18f;
        public float crateCoreChancePerTier = 0.015f;
        public float crateSalvageBase = 3f;
        public float crateSalvagePerTier = 0.6f;
        public float componentChancePerElite = 0.5f;
        public int bossCores = 2;

        // ---------------------------------------------------------------- ship power rating
        public float powerShieldHitValue = 30f;
        public float powerHullRef = 130f;
        public float powerDpsRef = 70f;
        public float powerPierceDps = 0.2f;
        public float powerHandlingWeight = 0.5f;
        public float powerBombWeight = 0.03f;

        // ---------------------------------------------------------------- caps (steady-state arrays are allocated once at run start)
        public int maxEnemies = 96;
        public int maxPlayerBullets = 96;
        public int maxEnemyBolts = 160;
        public int maxPickups = 40;
        public int maxLootEntries = 8;
    }
}
