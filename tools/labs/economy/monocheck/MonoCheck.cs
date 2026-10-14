using System;
using Frontier.Economy;
using Frontier.Universe;
using ResourceDef = Frontier.Economy.ResourceDef;

/// <summary>
/// Prints digests of everything the economy computes (tuning, catalog, obstacles, shard curve, score, three simulated players). The cross-check script runs it on
/// CoreCLR and on Unity's Mono and demands byte-identical output.
/// </summary>
public static class MonoCheck
{
    static ulong H(ulong h, string s) { return Hash.Combine(h, Hash.Fnv1a64(s)); }
    static ulong H(ulong h, long v) { return Hash.Combine(h, (ulong)v); }
    static ulong H(ulong h, double v) { return Hash.Combine(h, (ulong)BitConverter.DoubleToInt64Bits(v)); }
    static ulong H(ulong h, float v) { return Hash.Combine(h, (ulong)BitConverter.SingleToInt32Bits(v)); }

    static ulong Items(ulong h, ItemAmount[] a)
    {
        for (int i = 0; i < a.Length; i++) { h = H(h, a[i].resourceId); h = H(h, a[i].amount); }
        return h;
    }

    static ulong CatalogDigest(EconomyCatalog c)
    {
        ulong h = 0xC47A106UL;
        foreach (ResourceDef r in c.resources) { h = H(h, r.id); h = H(h, r.baseValue); h = H(h, r.stackSize); h = H(h, r.tier); h = H(h, r.baseYield); }
        foreach (RecipeDef r in c.recipes) { h = H(h, r.id); h = Items(h, r.inputs); h = Items(h, r.outputs); }
        foreach (StructureDef s in c.structures)
        {
            h = H(h, s.id);
            foreach (StructureLevelDef l in s.levels) { h = Items(h, l.cost); h = H(h, l.buildSeconds); h = H(h, l.effect); h = H(h, l.capacity); h = H(h, l.requiresReachTier); }
        }
        foreach (ToolLadderDef l in c.tools) foreach (ToolTierDef t in l.tiers) { h = H(h, t.yieldMult); h = Items(h, t.cost); }
        foreach (UpgradeDef u in c.upgrades) foreach (UpgradeLevelDef l in u.levels) { h = H(h, l.value); h = Items(h, l.cost); }
        foreach (ResearchNodeDef n in c.research) { h = H(h, n.id); h = Items(h, n.cost); }
        foreach (ObstacleKindDef k in c.obstacles) foreach (ObstacleTierCost tc in k.byTier) { h = Items(h, tc.cost); h = Items(h, new[] { tc.reward }); }
        return h;
    }

    static string Bits(double v) { return BitConverter.DoubleToInt64Bits(v).ToString("x"); }

    public static int Main()
    {
        Console.WriteLine("runtime: " + Environment.Version);
        Tuning t = Tuning.CreateDefault();
        EconomyCatalog cat = CatalogBuilder.Build(t);
        Console.WriteLine("tuning " + t.Count + " " + t.Digest().ToString("x"));
        Console.WriteLine("catalog " + CatalogDigest(cat).ToString("x"));

        ulong oh = 0x0B57AC1EUL;
        int count = 0;
        for (ulong id = 1; id <= 600; id++)
        {
            ObstacleInstance[] list = ObstacleRules.Generate(cat, 0x46524F4E54494552UL, id, cat.obstacleRules.padsPerSize[(int)(id % 6)], (SizeClass)(id % 6), (Biome)(id % 11), id % 7 == 0 ? Hazards.Void : Hazards.None, (int)(id % 13), id % 29 == 0);
            foreach (ObstacleInstance o in list) { oh = H(oh, o.pad); oh = H(oh, o.kind); oh = Items(oh, o.cost); oh = H(oh, o.seconds); oh = H(oh, o.podShards); oh = H(oh, o.xp); count++; }
        }
        Console.WriteLine("obstacles " + count + " " + oh.ToString("x"));

        ulong sh = 0x5A4D;
        for (double s = 0; s < 12 * 86400.0; s = s * 1.01 + 0.5) sh = H(sh, ShardCurve.SkipCost(cat.shards, s));
        Console.WriteLine("shards " + sh.ToString("x"));

        var inputs = new ScoreInputs
        {
            maxDistanceFromHomeLy = 1234.5, worldsLandedByTier = new[] { 2, 9, 31, 80, 120, 60 }, structureLevels = new[] { 10, 7, 5, 3, 1 }, structureTiers = new[] { 0, 2, 3, 4, 5 },
            upgradeLevelsTotal = 41, toolTierPurchases = 17, waveWorldDanger = new[] { 3.5, 4.0 }, waveWorldCleared = new[] { 40, 7 }, resourcesDiscovered = 30, recipesCrafted = 20, megaprojectStages = 4, ascensionRank = 2
        };
        ScoreBreakdown b = ScoreCalculator.Compute(inputs, ScoreFormula.From(t));
        Console.WriteLine("score " + b.total + " " + Bits(b.reach) + " " + Bits(b.worlds) + " " + Bits(b.industry) + " " + Bits(b.tech) + " " + Bits(b.defense) + " " + Bits(b.atlas));

        SimWorld world = SimWorld.Build(cat, StarterSystem.CreateUniverse(cat, 1UL), 3600.0);
        foreach (Archetype a in Archetype.All())
        {
            SimResult r = SimEngine.Run(cat, t, world, a, new SimSettings { seed = 1UL, horizonDays = 300.0 });
            Console.WriteLine("sim " + a.name + " digest " + r.Digest().ToString("x") + " score " + r.finalScore + " runs " + r.harvestRuns + " trips " + r.expeditions + " goals " + r.goals.Count + " settlement " + Bits(r.Days("era_settlement")) + " industry " + Bits(r.Days("era_industry")));
        }
        return 0;
    }
}
