/// <summary>
/// All hard-coded mission definitions. 9 categories × 5 tiers = 45 missions.
/// Missions never reset. Scaling gets progressively brutal — completing them all is a serious grind.
/// </summary>
public static class MissionDefinitions
{
    public static readonly MissionEntry[] All = new MissionEntry[]
    {
        // ── Reach Wave ────────────────────────────────────────────────────────────
        new MissionEntry(MissionType.ReachWave,        "Survive to Wave 10",        10f),
        new MissionEntry(MissionType.ReachWave,        "Survive to Wave 50",        50f),
        new MissionEntry(MissionType.ReachWave,        "Survive to Wave 100",       100f),
        new MissionEntry(MissionType.ReachWave,        "Survive to Wave 500",       500f),
        new MissionEntry(MissionType.ReachWave,        "Survive to Wave 1,000",     1000f),

        // ── Earn Total Money ──────────────────────────────────────────────────────
        new MissionEntry(MissionType.EarnTotalMoney,   "Earn $10,000 lifetime",     10000f),
        new MissionEntry(MissionType.EarnTotalMoney,   "Earn $1 Million lifetime",  1000000f),
        new MissionEntry(MissionType.EarnTotalMoney,   "Earn $1 Billion lifetime",  1000000000f),
        new MissionEntry(MissionType.EarnTotalMoney,   "Earn $1 Trillion lifetime", 1000000000000f),
        new MissionEntry(MissionType.EarnTotalMoney,   "Earn $1 Quadrillion lifetime", 1000000000000000f),

        // ── Kill Total Enemies ────────────────────────────────────────────────────
        new MissionEntry(MissionType.KillTotalEnemies, "Destroy 10 aliens",         10f),
        new MissionEntry(MissionType.KillTotalEnemies, "Destroy 100 aliens",        100f),
        new MissionEntry(MissionType.KillTotalEnemies, "Destroy 1,000 aliens",      1000f),
        new MissionEntry(MissionType.KillTotalEnemies, "Destroy 10,000 aliens",     10000f),
        new MissionEntry(MissionType.KillTotalEnemies, "Destroy 100,000 aliens",    100000f),

        // ── Complete Waves ────────────────────────────────────────────────────────
        new MissionEntry(MissionType.CompleteWaves,    "Complete 5 waves",          5f),
        new MissionEntry(MissionType.CompleteWaves,    "Complete 50 waves",         50f),
        new MissionEntry(MissionType.CompleteWaves,    "Complete 250 waves",        250f),
        new MissionEntry(MissionType.CompleteWaves,    "Complete 1,000 waves",      1000f),
        new MissionEntry(MissionType.CompleteWaves,    "Complete 5,000 waves",      5000f),

        // ── Reach XP Level ────────────────────────────────────────────────────────
        new MissionEntry(MissionType.ReachXPLevel,     "Reach XP Level 5",          5f),
        new MissionEntry(MissionType.ReachXPLevel,     "Reach XP Level 25",         25f),
        new MissionEntry(MissionType.ReachXPLevel,     "Reach XP Level 50",         50f),
        new MissionEntry(MissionType.ReachXPLevel,     "Reach XP Level 100",        100f),
        new MissionEntry(MissionType.ReachXPLevel,     "Reach XP Level 200",        200f),

        // ── Purchase Buildings ────────────────────────────────────────────────────
        new MissionEntry(MissionType.PurchaseBuildings, "Buy 5 buildings",           5f),
        new MissionEntry(MissionType.PurchaseBuildings, "Buy 25 buildings",          25f),
        new MissionEntry(MissionType.PurchaseBuildings, "Buy 100 buildings",         100f),
        new MissionEntry(MissionType.PurchaseBuildings, "Buy 500 buildings",         500f),
        new MissionEntry(MissionType.PurchaseBuildings, "Buy 2,000 buildings",       2000f),

        // ── Complete Research ─────────────────────────────────────────────────────
        new MissionEntry(MissionType.CompleteResearch,  "Purchase 3 research upgrades",   3f),
        new MissionEntry(MissionType.CompleteResearch,  "Purchase 15 research upgrades",  15f),
        new MissionEntry(MissionType.CompleteResearch,  "Purchase 50 research upgrades",  50f),
        new MissionEntry(MissionType.CompleteResearch,  "Purchase 150 research upgrades", 150f),
        new MissionEntry(MissionType.CompleteResearch,  "Purchase 500 research upgrades", 500f),

        // ── Reach Passive Income ──────────────────────────────────────────────────
        new MissionEntry(MissionType.ReachPassiveIncome, "Earn $100/sec passive",       100f),
        new MissionEntry(MissionType.ReachPassiveIncome, "Earn $10K/sec passive",       10000f),
        new MissionEntry(MissionType.ReachPassiveIncome, "Earn $1M/sec passive",        1000000f),
        new MissionEntry(MissionType.ReachPassiveIncome, "Earn $1B/sec passive",        1000000000f),
        new MissionEntry(MissionType.ReachPassiveIncome, "Earn $1T/sec passive",        1000000000000f),

        // ── Prestige ──────────────────────────────────────────────────────────────
        new MissionEntry(MissionType.Prestige,           "Prestige 1 time",            1f),
        new MissionEntry(MissionType.Prestige,           "Prestige 5 times",           5f),
        new MissionEntry(MissionType.Prestige,           "Prestige 15 times",          15f),
        new MissionEntry(MissionType.Prestige,           "Prestige 50 times",          50f),
        new MissionEntry(MissionType.Prestige,           "Prestige 100 times",         100f),
    };
}

/// <summary>
/// Plain C# data container for a single mission definition.
/// </summary>
public struct MissionEntry
{
    public MissionType type;
    public string goalText;
    public float targetValue;

    public MissionEntry(MissionType type, string goalText, float targetValue)
    {
        this.type        = type;
        this.goalText    = goalText;
        this.targetValue = targetValue;
    }
}
