using System;
using System.Collections.Generic;
using Frontier.Universe;

namespace Frontier.Economy
{
    /// <summary>Builds the whole economy catalog from a <see cref="Tuning"/>. Pure and deterministic: same tuning, same catalog.</summary>
    public static partial class CatalogBuilder
    {
        /// <summary>Layout version of the exported data.</summary>
        public const int SchemaVersion = 1;

        /// <summary>Version of the content numbers (bump after a live balance change).</summary>
        public const int ContentVersion = 1;

        public const int EraCount = 9;

        /// <summary>Pacing multiplier per item tier (0..8), tuned with the simulator so the three archetypes land in their era bands.</summary>
        static readonly double[] EraDefault = { 1.0, 1.8, 3.2, 1.8, 1.2, 0.9, 0.8, 0.7, 0.7 };

        public static void RegisterKeys(Tuning t)
        {
            RegisterResourceKeys(t);
            t.Register("cost", "cost.scale.structure", 1.0, 0.05, 20, "Global multiplier on every structure value budget.");
            t.Register("cost", "cost.scale.tool", 1.0, 0.05, 20, "Global multiplier on tool ladder value budgets.");
            t.Register("cost", "cost.scale.ship", 1.0, 0.05, 20, "Global multiplier on ship and logistics upgrade value budgets.");
            t.Register("cost", "cost.scale.defense", 1.0, 0.05, 20, "Global multiplier on defense upgrade value budgets.");
            t.Register("cost", "cost.scale.research", 1.0, 0.05, 20, "Global multiplier on research value budgets.");
            t.Register("cost", "cost.scale.mega", 0.7, 0.05, 20, "Global multiplier on megaproject stage budgets (tuned by the simulator: late stages are weeks of play, not months).");
            for (int i = 0; i < EraCount; i++)
                t.Register("cost", "cost.era." + i, EraDefault[i], 0.05, 20, "Multiplier on every value budget whose item belongs to tier " + i + " (the era pacing knob; tuned by the simulator against the era bands).");
            t.Register("cost", "level.reach_step", 0.5, 0, 2, "A structure level needs reach tier = unlock tier + floor((level - 1) x step): upgrades reward going outward.");
            t.Register("cost", "cost.cap.base_slots", 12, 1, 1000, "No single line of a non-megaproject cost may exceed this many slots of its resource at tier 0 (so a base can hold what a purchase asks).");
            t.Register("cost", "cost.cap.slots_per_tier", 14, 0, 200, "Extra slots allowed per line for every tier of the purchase's reach requirement.");
            for (int i = 1; i <= 7; i++)
            {
                t.Register("tool", "tool.mult." + i, ToolMultDefault[i - 1], 1, 100, "Yield multiplier of tool tier " + i + " (all ladders).");
                t.Register("tool", "tool.runtime." + i, ToolRunTimeDefault[i - 1], 0.3, 1.5, "Harvest run time multiplier at tool tier " + i + " (faster swings).");
            }
            for (int i = 0; i < StructureSpecs.Length; i++)
            {
                StructSpec s = StructureSpecs[i];
                if (s.v1 > 0.0) t.Register("cost", "cost.v." + s.id, s.v1, 0, 1e9, "Value budget (scrap-equivalents) of " + s.name + " level 1" + (s.mega ? " (stage 1)." : "."));
                else
                {
                    t.Register("cost", "cost.v." + s.id, 0, 0, 1e9, s.name + " level 1 is free (set above 0 to charge for it).");
                    t.Register("cost", "cost.v2." + s.id, s.v2, 0, 1e9, "Value budget of " + s.name + " level 2 (the first paid level).");
                }
                t.Register("cost", "cost.g." + s.id, s.growth, 1.0, 5.0, "Geometric cost growth per level of " + s.name + ".");
                t.Register("time", "time.start." + s.id, s.timeStart, 0, 11, "Level index of " + s.name + " level 1 on the shared time curve.");
                t.Register("time", "time.step." + s.id, s.timeStep, 0, 3, "Level index added per level for " + s.name + ".");
            }
            for (int i = 0; i < ToolSpecs.Length; i++)
            {
                ToolSpec s = ToolSpecs[i];
                t.Register("cost", "cost.v." + s.id, s.v2, 0, 1e9, "Value budget of the first purchasable tier (tier 2) of the " + s.name + " ladder.");
                t.Register("cost", "cost.g." + s.id, s.growth, 1.0, 20, "Value growth per tool tier of the " + s.name + " ladder.");
            }
            for (int i = 0; i < UpgradeSpecs.Length; i++)
            {
                UpgSpec s = UpgradeSpecs[i];
                t.Register("cost", "cost.v." + s.id, s.v1, 0, 1e9, "Value budget of " + s.name + " level 1.");
                t.Register("cost", "cost.g." + s.id, s.growth, 1.0, 100, "Value growth per level of " + s.name + ".");
                for (int l = 0; l < s.values.Length; l++)
                    t.Register("upgrade", "upg." + s.id + "." + (l + 1), s.values[l], 0, 1e7, "Cumulative effect of " + s.name + " at level " + (l + 1) + " (" + s.effect + ").");
            }
            for (int i = 0; i < ResearchSpecs.Length; i++)
                t.Register("cost", "cost.v." + ResearchSpecs[i].id, ResearchSpecs[i].v, 0, 1e9, "Value budget of research node " + ResearchSpecs[i].name + ".");
            t.Register("start", "start.wood", 24, 0, 1000, "Starting wood.");
            t.Register("start", "start.scrap", 30, 0, 1000, "Starting scrap.");
            t.Register("start", "start.fuel", 10, 0, 1000, "Starting fuel cells.");
            t.Register("start", "start.backpack_slots", 9, 3, 60, "Starting backpack slots (small on purpose: it forces a Warehouse in the first session).");
            t.Register("start", "start.builders", 1, 1, 8, "Builders at the start.");
        }

        static double EraScale(Tuning t, int tier)
        {
            return t.D("cost.era." + Math.Max(0, Math.Min(EraCount - 1, tier)));
        }

        public static EconomyCatalog Build(Tuning t)
        {
            var cat = new EconomyCatalog();
            cat.schemaVersion = SchemaVersion;
            cat.contentVersion = ContentVersion;
            cat.categoryHex = (string[])CategoryFamilyHex.Clone();
            cat.resources = BuildResources(t);
            cat.recipes = BuildRecipes();
            ValueModel.Derive(cat.resources, cat.recipes, t);
            cat.habitats = BuildHabitats();

            var price = new Dictionary<string, double>();
            var stack = new Dictionary<string, int>();
            for (int i = 0; i < cat.resources.Length; i++) { price[cat.resources[i].id] = cat.resources[i].baseValue; stack[cat.resources[i].id] = cat.resources[i].stackSize; }

            var times = new JobTimes(t);
            cat.structures = BuildStructures(t, price, stack, times);
            cat.tools = BuildTools(t, price, stack);
            cat.upgrades = BuildUpgrades(t, price, stack);
            cat.research = BuildResearch(t, price, stack);
            cat.legacyIds = BuildLegacyIds();
            cat.shards = ShardRulesDef.From(t);
            cat.obstacleRules = ObstacleRules.RulesFrom(t);
            cat.obstacles = BuildObstacles(t, price, stack, cat.obstacleRules);
            cat.start = new StartDef
            {
                startingItems = new[] { new ItemAmount("fuel", t.I("start.fuel")), new ItemAmount("wood", t.I("start.wood")), new ItemAmount("scrap", t.I("start.scrap")) },
                baseBackpackSlots = t.I("start.backpack_slots"),
                startPlanetId = "sylva",
                builders = t.I("start.builders")
            };
            cat.Index();
            return cat;
        }

        // ------------------------------------------------------------------------------------------------ structures
        static double CapSlots(Tuning t, int tier)
        {
            return t.D("cost.cap.base_slots") + t.D("cost.cap.slots_per_tier") * Math.Max(0, tier);
        }

        static StructureDef[] BuildStructures(Tuning t, Dictionary<string, double> price, Dictionary<string, int> stack, JobTimes times)
        {
            var list = new StructureDef[StructureSpecs.Length];
            double reachStep = t.D("level.reach_step");
            double timeScale = t.D("time.scale");
            for (int i = 0; i < list.Length; i++)
            {
                StructSpec s = StructureSpecs[i];
                double v1 = t.D("cost.v." + s.id);
                double v2 = v1 > 0.0 ? 0.0 : t.D("cost.v2." + s.id);
                double g = t.D("cost.g." + s.id);
                double start = t.D("time.start." + s.id), step = t.D("time.step." + s.id);
                double scale = (s.mega ? t.D("cost.scale.mega") : t.D("cost.scale.structure")) * EraScale(t, s.tier);
                Bundle bundle = Bundle.Parse(s.bundle);
                var levels = new StructureLevelDef[s.maxLevel];
                for (int level = 1; level <= s.maxLevel; level++)
                {
                    double v;
                    if (v1 > 0.0) v = v1 * DetMath.Pow(g, level - 1);
                    else v = level == 1 ? 0.0 : v2 * DetMath.Pow(g, level - 2);
                    var d = new StructureLevelDef();
                    d.level = level;
                    int lvlReach = s.unlockTier + (int)Math.Floor((level - 1) * reachStep + 1e-9);
                    d.cost = v > 0.0 ? bundle.Cost(v * scale, level, price, stack, s.mega ? 0.0 : CapSlots(t, lvlReach)) : new ItemAmount[0];
                    d.buildSeconds = s.mega ? times.MegaStageSeconds(level, s.megaTime) : times.StructureSeconds(start, step, level, timeScale);
                    d.requiresReachTier = lvlReach;
                    d.requiresLabLevel = 0;
                    d.effect = CostMath.F(s.effTable != null ? s.effTable[Math.Min(level, s.effTable.Length) - 1] : s.effBase + s.effPer * (s.mega ? level : level - 1), 4);
                    d.capacity = s.capTable != null ? (int)s.capTable[Math.Min(level, s.capTable.Length) - 1] : CostMath.RoundInt(s.capBase + s.capPer * (level - 1));
                    levels[level - 1] = d;
                }
                list[i] = new StructureDef
                {
                    id = s.id, displayName = s.name, role = s.role, tier = s.tier, description = s.desc, footprint = s.footprint, maxPerPlanet = s.maxPer,
                    maxLevel = s.maxLevel, effectKind = s.effectKind, capacityKind = s.capacityKind, gathers = s.gathers, unlockResearch = s.research,
                    unlockTier = s.unlockTier, legacyKind = s.legacy, damage = (float)s.damage, fireInterval = (float)s.interval, range = (float)s.range, levels = levels
                };
            }
            return list;
        }

        // ------------------------------------------------------------------------------------------------ tools
        static int ResearchTier(string id)
        {
            if (id == null) return 0;
            for (int i = 0; i < ResearchSpecs.Length; i++) if (ResearchSpecs[i].id == id) return ResearchSpecs[i].tier;
            throw new InvalidOperationException("unknown research " + id);
        }

        static ToolLadderDef[] BuildTools(Tuning t, Dictionary<string, double> price, Dictionary<string, int> stack)
        {
            var list = new ToolLadderDef[ToolSpecs.Length];
            double scale = t.D("cost.scale.tool");
            for (int i = 0; i < list.Length; i++)
            {
                ToolSpec s = ToolSpecs[i];
                double v2 = t.D("cost.v." + s.id), g = t.D("cost.g." + s.id);
                var tiers = new ToolTierDef[7];
                for (int tier = 1; tier <= 7; tier++)
                {
                    var d = new ToolTierDef();
                    d.tier = tier;
                    d.displayName = s.names[tier - 1];
                    d.yieldMult = CostMath.F(t.D("tool.mult." + tier), 3);
                    d.runTimeMult = CostMath.F(t.D("tool.runtime." + tier), 3);
                    d.researchPrereq = ToolResearch[tier - 1];
                    d.requiresReachTier = tier == 1 ? 0 : s.reachBase + ToolReachSteps[tier - 2];
                    d.requiresLabLevel = ToolLabLevels[tier - 1];
                    d.eraTier = Math.Max(d.requiresReachTier, ResearchTier(d.researchPrereq));
                    if (tier == 1) d.cost = new ItemAmount[0];
                    else
                    {
                        double v = v2 * DetMath.Pow(g, tier - 2) * scale * EraScale(t, d.eraTier);
                        d.cost = Bundle.Parse(s.bundles[tier - 2]).Cost(v, 1, price, stack, CapSlots(t, d.eraTier));
                    }
                    tiers[tier - 1] = d;
                }
                list[i] = new ToolLadderDef { id = s.id, displayName = s.name, kind = s.kind, games = Methods(s.games), tiers = tiers };
            }
            return list;
        }

        // ------------------------------------------------------------------------------------------------ upgrades
        static UpgradeDef[] BuildUpgrades(Tuning t, Dictionary<string, double> price, Dictionary<string, int> stack)
        {
            var list = new UpgradeDef[UpgradeSpecs.Length];
            for (int i = 0; i < list.Length; i++)
            {
                UpgSpec s = UpgradeSpecs[i];
                double v1 = t.D("cost.v." + s.id), g = t.D("cost.g." + s.id);
                double scale = s.group == UpgradeGroup.Defense ? t.D("cost.scale.defense") : t.D("cost.scale.ship");
                Bundle bundle = Bundle.Parse(s.bundle);
                int n = s.values.Length;
                var levels = new UpgradeLevelDef[n];
                for (int level = 1; level <= n; level++)
                {
                    int reach = s.reach == null ? 0 : s.reach[level - 1];
                    var d = new UpgradeLevelDef();
                    d.level = level;
                    d.displayName = s.names == null ? s.name : s.names[level - 1];
                    d.researchPrereq = s.research == null ? null : s.research[level - 1];
                    d.requiresReachTier = reach;
                    d.eraTier = Math.Max(reach, ResearchTier(d.researchPrereq));
                    d.cost = bundle.Cost(v1 * DetMath.Pow(g, level - 1) * scale * EraScale(t, d.eraTier), level, price, stack, CapSlots(t, d.eraTier));
                    d.value = CostMath.F(t.D("upg." + s.id + "." + level), 4);
                    levels[level - 1] = d;
                }
                list[i] = new UpgradeDef { id = s.id, displayName = s.name, description = s.desc, group = s.group, effect = s.effect, maxLevel = n, hazard = s.hazard, levels = levels };
            }
            return list;
        }

        // ------------------------------------------------------------------------------------------------ research
        static ResearchNodeDef[] BuildResearch(Tuning t, Dictionary<string, double> price, Dictionary<string, int> stack)
        {
            var list = new ResearchNodeDef[ResearchSpecs.Length];
            double scale = t.D("cost.scale.research");
            for (int i = 0; i < list.Length; i++)
            {
                RsrSpec s = ResearchSpecs[i];
                string spec = ResearchDefaultBundle[s.tier] + (s.extra.Length > 0 ? "," + s.extra : "");
                double v = t.D("cost.v." + s.id) * scale * EraScale(t, s.tier);
                list[i] = new ResearchNodeDef
                {
                    id = s.id, displayName = s.name, description = s.desc, branch = s.branch, tier = s.tier,
                    cost = Bundle.Parse(spec).Cost(v, 1, price, stack, CapSlots(t, s.tier)), prereqs = s.prereqs, labLevel = s.tier, unlocks = s.unlocks.Split(';')
                };
            }
            return list;
        }

        // ------------------------------------------------------------------------------------------------ legacy ids
        static LegacyIdEntry[] BuildLegacyIds()
        {
            var list = new List<LegacyIdEntry>();
            string[] res = { "wood", "wood", "stone", "stone", "scrap", "scrap", "iron", "iron", "crystal", "crystal", "fuel", "fuel", "core", "core", "alloy", "steel", "plasma", "plasma" };
            for (int i = 0; i < res.Length; i += 2) list.Add(new LegacyIdEntry { kind = LegacyKind.Resource, legacyId = res[i], id = res[i + 1] });
            string[] str = { "refinery", "refinery", "fabricator", "fabricator", "alarm", "alarm", "lumber", "lumber", "mine", "mine", "driller", "driller", "gunner", "gunner", "laser", "laser", "satellite", "orbital_relay", "warehouse", "warehouse" };
            for (int i = 0; i < str.Length; i += 2) list.Add(new LegacyIdEntry { kind = LegacyKind.Structure, legacyId = str[i], id = str[i + 1] });
            string[] upg = { "axe", "pick", "drill", "pack", "engine", "turret", "turretdmg", "pulse", "shield", "salvage" };
            for (int i = 0; i < upg.Length; i++) list.Add(new LegacyIdEntry { kind = LegacyKind.Upgrade, legacyId = upg[i], id = upg[i] });
            return list.ToArray();
        }
    }
}
