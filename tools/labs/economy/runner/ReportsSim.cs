using System;
using System.Collections.Generic;
using System.Text;
using Frontier.Economy;
using Frontier.Economy.Tests;
using Frontier.Universe;

namespace Frontier.Economy.Runner
{
    public static partial class EconomyReport
    {
        static readonly string[] Archetypes = { "casual", "regular", "hardcore" };
        const double Horizon = 730.0;

        // ------------------------------------------------------------------------------------------------ eras
        static void EraSection(StringBuilder sb, EconomyCatalog cat)
        {
            sb.AppendLine("## 9. Eras, target bands and simulated times");
            sb.AppendLine();
            sb.AppendLine("The simulator (`SimEngine`) is a deterministic average player: sessions per day, minutes per session, a harvest run of 25 to 40 s with the `0.4 + 1.1 x quality` yield curve, tool and camp multipliers, crafting taps, builders and timers, called waves, travel to the cheapest world that offers what is missing (and back to the starter worlds when bulk volume is needed), pack and warehouse limits and a greedy upgrade policy over the goal ladder (section 10). Shard use is disabled in the base model. The same seed gives the same timeline (checked by a test and a digest).");
            sb.AppendLine();
            var arows = new List<string[]>();
            foreach (Archetype a in Archetype.All()) arows.Add(new[] { a.name, a.sessionsPerDay + " x " + a.minutesPerSession.ToString("0") + " min", (a.DailyMinutes / 60.0).ToString("0.0") + " h", a.quality.ToString("0.00"), a.efficiency.ToString("0.00") });
            ReportData.Table(sb, new[] { "archetype", "sessions a day", "play a day", "mini-game quality", "play efficiency" }, arows);
            sb.AppendLine("Era entry rules (ROADMAP 5.8, expressed as gates): **Landfall** = start; **Foothold** = first landing on another world; **Settlement** = Research Lab plus jump level 1; **Industry** = first hazard plating; **Interstellar** = radiation plating and the first antimatter; **Ascension** = megaproject stage 3.");
            sb.AppendLine();
            foreach (string name in Archetypes)
            {
                SimResult r = SimFx.Run(name, 1UL, Horizon);
                var seeds = new List<SimResult>();
                for (ulong s = 1; s <= 4; s++) seeds.Add(SimFx.Run(name, s, Horizon));
                sb.AppendLine("**" + name + "**");
                sb.AppendLine();
                var rows = new List<string[]>();
                foreach (SimBand b in SimBands.For(name))
                {
                    double d = r.Days(b.milestone);
                    double lo = double.MaxValue, hi = 0.0;
                    foreach (SimResult x in seeds) { double v = x.Days(b.milestone); lo = Math.Min(lo, v); hi = Math.Max(hi, v); }
                    bool ok = d >= b.minDays - 1e-9 && d <= b.maxDays + 1e-9 && lo >= b.minDays - 1e-9 && hi <= b.maxDays + 1e-9;
                    rows.Add(new[] { b.label, ReportData.Days(b.minDays) + " to " + ReportData.Days(b.maxDays), ReportData.Days(d), ReportData.Days(lo) + " to " + ReportData.Days(hi), ok ? "in band" : "OUT" });
                }
                ReportData.Table(sb, new[] { "milestone", "target band", "simulated (seed 1)", "across 4 player seeds", "result" }, rows);
            }
            SimResult reg = SimFx.Run("regular", 1UL, Horizon);
            sb.AppendLine("First session of the regular player (15 minutes offered): first Warehouse after " + ReportData.Days(reg.Days("first_warehouse")) + ", first fuel after " + ReportData.Days(reg.Days("first_fuel")) + ", first hop to another world after " + ReportData.Days(reg.Days("first_hop")) + "; day 1 ends in the Foothold era, the Settlement era begins on day " + reg.Days("era_settlement").ToString("0.0") + ", Industry on day " + reg.Days("era_industry").ToString("0") + ", Interstellar on day " + reg.Days("era_interstellar").ToString("0") + ", Ascension on day " + reg.Days("era_ascension").ToString("0") + ".");
            sb.AppendLine();
            sb.AppendLine("**No dead zones.** The longest stretch of active play without visible progress (a goal completed, a megaproject installment contributed, or a first landing on a new world), against the limit per era (the same bands hold in universes 3, 4 and 5 and for 3 player seeds in each of 5 universes):");
            sb.AppendLine();
            var grows = new List<string[]>();
            for (int era = 0; era < 6; era++)
            {
                var cells = new List<string> { SimBands.EraNames[era], SimBands.MaxGoalMinutes(era).ToString("0") + " min" };
                foreach (string name in Archetypes)
                {
                    double[] gaps = SimBands.MaxGapMinutesByEra(SimFx.Run(name, 1UL, Horizon));
                    cells.Add(gaps[era] > 0.0 ? gaps[era].ToString("0") + " min" : "-");
                }
                grows.Add(cells.ToArray());
            }
            ReportData.Table(sb, new[] { "era", "limit", "casual", "regular", "hardcore" }, grows);
            SimResult hard = SimFx.Run("hardcore", 1UL, Horizon);
            sb.AppendLine("Beyond the bands: the casual player reaches Ascension at about day " + SimFx.Run("casual", 1UL, Horizon).Days("era_ascension").ToString("0") + " and has " + (SimFx.Run("casual", 1UL, Horizon).goals.Count) + " headline goals done after two years; the regular player is still finding goals at the end of year two; the hardcore player (2.5 hours a day) ticks the last headline goal on day " + hard.Days("ladder_complete").ToString("0") + " and every level of every structure, tool and upgrade by about day 480, after which only the endless goals remain (see Known risks).");
            sb.AppendLine();
            sb.AppendLine("Charts (viewed and iterated on; see PROGRESS.md for what changed): `chart_resources_over_time.png`, `chart_tier_over_time.png`, `chart_time_per_era.png`, `chart_score_over_time.png`.");
            sb.AppendLine();
        }

        // ------------------------------------------------------------------------------------------------ goal ladder
        static void LadderSection(StringBuilder sb, EconomyCatalog cat)
        {
            string[] lines = SimEngine.DefaultLadderLines;
            sb.AppendLine("## 10. The goal ladder: a clear next shiny goal at every stage");
            sb.AppendLine();
            sb.AppendLine("The headline wants of a typical player, in the order the planner chases them (prerequisites such as research, station levels, reach, plating and ingredients are resolved automatically and are not listed). Days are the day each simulated archetype completes the goal; the era is the one the regular player is in at that moment. After the list is complete the simulator adds an endless tail (every remaining structure level, tool tier and upgrade level, cheapest first).");
            sb.AppendLine();
            SimResult c = SimFx.Run("casual", 1UL, Horizon), g = SimFx.Run("regular", 1UL, Horizon), h = SimFx.Run("hardcore", 1UL, Horizon);
            var rows = new List<string[]>();
            int shown = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                string text = lines[i];
                int eq = text.IndexOf('=');
                if (eq >= 0) text = text.Substring(0, eq);
                double dc = GoalDay(c, i), dg = GoalDay(g, i), dh = GoalDay(h, i);
                string era = double.IsNaN(dg) ? "-" : SimBands.EraNames[SimBands.EraAt(g, dg * 86400.0)];
                rows.Add(new[] { (++shown).ToString(), GoalLabel(cat, text), era, ReportData.Days(dc), ReportData.Days(dg), ReportData.Days(dh) });
            }
            ReportData.Table(sb, new[] { "#", "want", "regular's era", "casual", "regular", "hardcore" }, rows);
            var perEra = new int[6];
            foreach (SimGoalTime x in g.goals) if (x.index < lines.Length) perEra[SimBands.EraAt(g, x.seconds)]++;
            sb.AppendLine("Headline goals completed by the regular player per era: Landfall " + perEra[0] + ", Foothold " + perEra[1] + ", Settlement " + perEra[2] + ", Industry " + perEra[3] + ", Interstellar " + perEra[4] + ", Ascension " + perEra[5] + ".");
            sb.AppendLine();
        }

        static double GoalDay(SimResult r, int index)
        {
            foreach (SimGoalTime g in r.goals) if (g.index == index) return g.seconds / 86400.0;
            return double.NaN;
        }

        static string GoalLabel(EconomyCatalog cat, string text)
        {
            string[] p = text.Split(':');
            try
            {
                switch (p[0])
                {
                    case "build":
                    {
                        string lvl = p[2];
                        string where = "";
                        int at = lvl.IndexOf('@');
                        if (at >= 0) { where = " on " + lvl.Substring(at + 1); lvl = lvl.Substring(0, at); }
                        int no = lvl.IndexOf('#');
                        if (no >= 0) lvl = lvl.Substring(0, no);
                        return cat.Structure(p[1]).displayName + " level " + lvl + where;
                    }
                    case "tool":
                    {
                        ToolLadderDef l = cat.Tool(p[1]);
                        int tier = int.Parse(p[2]);
                        return l.displayName + " tier " + tier + " (" + l.tiers[tier - 1].displayName + ")";
                    }
                    case "upg":
                    {
                        UpgradeDef u = cat.Upgrade(p[1]);
                        int lv = int.Parse(p[2]);
                        return u.displayName + " level " + lv + " (" + u.levels[lv - 1].displayName + ")";
                    }
                    case "research": return "Research: " + cat.Research(p[1]).displayName;
                    case "explore": return "Reach a tier " + p[1] + " world";
                    case "wave": return "Call wave " + p[1];
                    case "stock": return "Hold " + p[2] + " " + cat.Resource(p[1]).displayName;
                    case "mega": return cat.Structure(p[1]).displayName + " stage " + p[2];
                    default: return text;
                }
            }
            catch (Exception)
            {
                return text;
            }
        }

        // ------------------------------------------------------------------------------------------------ starter loop
        static double WoodPerRun(EconomyCatalog c, int toolTier, int campLevel, double quality)
        {
            double tool = c.Tool("axe").tiers[toolTier - 1].yieldMult;
            double camp = c.Structure("lumber").levels[campLevel - 1].effect;
            return c.Resource("wood").baseYield * tool * (1.0 + camp) * (0.4 + 1.1 * quality);
        }

        static void StarterLoopSection(StringBuilder sb, EconomyCatalog cat)
        {
            sb.AppendLine("## 11. The return-to-starter-worlds loop, in numbers");
            sb.AppendLine();
            sb.AppendLine("Harvest runs on a tier-0 forest needed to cover the whole tier-0 volume of one megaproject stage (a run is about 33 s; expected wood per run = base yield 10 x tool multiplier x (1 + camp bonus) x (0.4 + 1.1 x 0.74 quality)). The mini-game is identical in every column; only the tool tier and the Lumber Camp level change.");
            sb.AppendLine();
            var rows = new List<string[]>();
            foreach (string id in new[] { "terraformer", "warp_gate", "dyson_collector" })
            {
                StructureDef s = cat.Structure(id);
                foreach (int stage in new[] { 1, 3, 5 })
                {
                    double v = cat.Prices.T0Volume(s.levels[stage - 1].cost);
                    double r1 = v / WoodPerRun(cat, 1, 1, 0.74), r3 = v / WoodPerRun(cat, 3, 5, 0.74), r5 = v / WoodPerRun(cat, 5, 8, 0.74), r7 = v / WoodPerRun(cat, 7, 10, 0.74);
                    rows.Add(new[] { s.displayName + " stage " + stage, ReportData.Value(v), ReportData.Value(r1) + " (" + (r1 * 33.0 / 3600.0).ToString("0") + " h)", ReportData.Value(r3), ReportData.Value(r5), ReportData.Value(r7) + " (" + (r7 * 33.0 / 3600.0).ToString("0") + " h)", (r1 / r7).ToString("0") + "x" });
                }
            }
            ReportData.Table(sb, new[] { "project", "tier-0 volume (wood-eq)", "runs, tool 1 + camp 1", "tool 3 + camp 5", "tool 5 + camp 8", "runs, tool 7 + camp 10", "speed-up" }, rows);
            sb.AppendLine("So with starter tools a late stage is tens of thousands of runs (hundreds of hours: it is out of reach on purpose), and with the end-game tool and a level-10 camp it is a few dozen hours spread over the weeks of the stage timer. Mid-game purchases sit in between, which is why the loop stays interesting for the whole game.");
            sb.AppendLine();
            sb.AppendLine("Tier-0 volume of the level-1 cost of structures by unlock tier (the average over the structures first available at that tier): ");
            sb.AppendLine();
            var trows = new List<string[]>();
            for (int tier = 0; tier <= 8; tier++)
            {
                double sum = 0.0; int n = 0;
                foreach (StructureDef s in cat.structures)
                {
                    if (s.unlockTier != tier || s.role == StructureRole.Megaproject) continue;
                    double v = cat.Prices.T0Volume(s.levels[0].cost);
                    if (v <= 0.0) continue;
                    sum += v; n++;
                }
                if (n > 0) trows.Add(new[] { tier.ToString(), n.ToString(), ReportData.Value(sum / n) });
            }
            ReportData.Table(sb, new[] { "unlock tier", "structures", "mean tier-0 volume of level 1" }, trows);
            SimResult r = SimFx.Run("regular", 1UL, Horizon);
            double axe = r.stats["harvest_v:axe"], pick = r.stats["harvest_v:pick"], claw = r.stats["harvest_v:claw"], total = 0.0;
            foreach (string k in r.statOrder) if (k.StartsWith("harvest_v:", StringComparison.Ordinal)) total += r.stats[k];
            sb.AppendLine("In the simulation the regular player harvests " + ((axe + claw + 0.5 * pick) / total * 100.0).ToString("0") + " % of all harvested value from the wood, scrap and stone of starter-style harvests over two years (axe " + ReportData.Value(axe) + ", pick " + ReportData.Value(pick) + ", claw " + ReportData.Value(claw) + " of " + ReportData.Value(total) + " scrap-equivalents), and makes " + r.expeditions + " trips.");
            sb.AppendLine();
        }

        // ------------------------------------------------------------------------------------------------ shards
        static void ShardSection(StringBuilder sb, EconomyCatalog cat)
        {
            ShardRulesDef s = cat.shards;
            sb.AppendLine("## 12. Shards, builders and timers");
            sb.AppendLine();
            sb.AppendLine("**Timers.** Job time per structure level follows one shared curve (index 0 = instant, 5 s, 20 s, 1 min, 3 min, 10 min, 30 min, 1 h, 3 h, 8 h, 16 h, 24 h) shifted per structure (`time.start.*`, `time.step.*`); ordinary jobs are capped at one day and megaproject stages at 10 days. The full table is in section 5 and `data\\structures.json`; tests enforce that times never fall with level and never exceed their cap.");
            sb.AppendLine();
            sb.AppendLine("**Builders.** 1 at the start; the second comes from the research `field_engineering`; the third and fourth from the Colony Hub (level 3 and level 7) or, as a shortcut, from shards (" + s.thirdBuilderShards + " and " + s.fourthBuilderShards + "). The cap is " + s.maxBuilders + " whichever route is used. Megaprojects use no builder.");
            sb.AppendLine();
            sb.AppendLine("**Skip price.** `shards = max(" + s.minSkipCost + ", ceil(" + s.skipMultiplier.ToString("0.0#") + " x minutes ^ " + s.skipExponent.ToString("0.00") + "))`, free for the last " + s.freeFinishSeconds + " seconds. It depends on time only: skipping never changes what a job costs in resources, and no function converts shards to resources. The curve is monotone and concave (finishing one job never costs more than finishing it in two paid halves).");
            sb.AppendLine();
            var crow = new List<string[]>();
            foreach (double sec in new[] { 60.0, 300, 600, 1800, 3600, 4 * 3600, 8 * 3600, 86400, 2 * 86400, 5 * 86400, 10 * 86400 })
                crow.Add(new[] { ReportData.Seconds(sec), ShardCurve.SkipCost(s, sec).ToString(), (ShardCurve.SkipCost(s, sec) / (sec / 60.0)).ToString("0.00") });
            ReportData.Table(sb, new[] { "time left", "shards", "shards per minute" }, crow);
            sb.AppendLine("**Sinks.** Skip timers (curve above), builder 3 for " + s.thirdBuilderShards + " and builder 4 for " + s.fourthBuilderShards + " shards, revive in a Hyperspace Run " + s.reviveRunShards + " and in a wave " + s.reviveWaveShards + ", cosmetics " + s.cosmeticMinShards + " to " + s.cosmeticMaxShards + " (ship skins, trails, structure skins, UI themes). Nothing else.");
            sb.AppendLine();
            sb.AppendLine("**Faucets.** Level-ups (" + s.levelUpBase + " plus 1 per " + s.levelUpEveryLevels + " levels), first Atlas discoveries (" + s.atlasResourceShards + " per resource, " + s.atlasRecipeShards + " per recipe, " + s.atlasStructureShards + " per structure kind; paid once per catalog entry, the anti-farm cap), achievements (" + s.achievementSmall + " / " + s.achievementMedium + " / " + s.achievementLarge + "; the simulator's milestones: era entry is large, a new reach tier medium), daily streak (" + string.Join(", ", Array.ConvertAll(s.streakDays, x => x.ToString())) + " = " + ShardLedger.StreakShards(s, 7) + " a week), weekly objectives (" + s.weeklyObjectives + " x " + s.weeklyObjectiveShards + "), weekly board (" + s.boardParticipation + " for an active week, " + s.boardTop10Percent + " top 10 %, " + s.boardTop1Percent + " top 1 %), opt-in rewarded ads (" + s.adReward + " each, " + s.adsPerDay + " a day), obstacle pods (" + s.podBase + " plus 1 per 3 tiers, 5 % of clutter) and IAP.");
            sb.AppendLine();
            var prow = new List<string[]>();
            foreach (ShardPackDef p in s.packs) prow.Add(new[] { p.id, p.shards.ToString(), "tier " + p.priceTier, (p.shards / (double)p.priceTier).ToString("0") + " per price unit", ShardCurve.SkipCost(s, 86400) <= p.shards ? "a day-long skip" : "less than a day-long skip" });
            ReportData.Table(sb, new[] { "pack", "shards", "price tier", "value", "buys" }, prow);
            sb.AppendLine("**Weekly income of a free player** (replayed from each simulated timeline; the player logs in daily; obstacles cleared = 0.35 per build job):");
            sb.AppendLine();
            var wrows = new List<string[]>();
            foreach (string name in Archetypes)
            {
                SimResult r = SimFx.Run(name, 1UL, Horizon);
                ShardSummary free = ShardEconomy.FreePlayer(cat, r, 0.0, 0.35), ads = ShardEconomy.FreePlayer(cat, r, 1.0, 0.35);
                wrows.Add(new[] { name, free.weeks[0].earned.Total.ToString(), free.weeks[1].earned.Total.ToString(), free.weeks[3].earned.Total.ToString(), free.weeks[25].earned.Total.ToString(), free.medianPerWeek.ToString("0"), free.meanPerWeek.ToString("0"), ads.meanPerWeek.ToString("0"), free.totalEarned.ToString(), free.skipAllOrdinaryJobs.ToString(), free.skipAllMegaTimers.ToString() });
            }
            ReportData.Table(sb, new[] { "archetype", "week 1", "week 2", "week 4", "week 26", "median", "mean", "mean with every ad", "2 years", "price to skip all ordinary jobs", "price to skip all megaproject timers" }, wrows);
            sb.AppendLine("A free player earns about 65 shards a week long-run (a 3-hour skip, or builder 3 in four weeks, or a cosmetic in a month) and 100 to 400 in the first weeks; ads add about 40 a week and never dominate. Income is flat in play volume (the faucets reward showing up, not grinding), so the hardcore player does not out-earn the casual one by more than a third. **A paying player is never required:** the simulator takes no shard input and still meets every era band; ordinary jobs are short (the whole two years of a regular player's 148 timed jobs would cost about 1.4 k shards), and the real time sink is the multi-day megaproject timers, where shards are a convenience that cannot buy the ingredients.");
            sb.AppendLine();
        }

        // ------------------------------------------------------------------------------------------------ obstacles
        static void ObstacleSection(StringBuilder sb, EconomyCatalog cat)
        {
            ObstacleRulesDef r = cat.obstacleRules;
            sb.AppendLine("## 13. Obstacles (clutter)");
            sb.AppendLine();
            sb.AppendLine("Each planet's clutter is a pure function of (universe seed, planet id, size class, biome, hazards, tier, home flag) from the universe hash streams, so it is never stored; only the cleared set is saved. Rules: the first " + r.freePads + " pads of every world are always free; an ordinary world blocks " + (r.density * 100).ToString("0") + " % of its pads (plus or minus " + (r.densityJitter * 100).ToString("0") + " %, per planet), the home world " + (r.homeDensity * 100).ToString("0") + " %; gas giants carry none; " + (r.shardPodChance * 100).ToString("0") + " % of obstacles are shard pods (" + ShardLedger.PodShards(cat.shards, 0) + " shard at tier 0, +1 per 3 tiers). Clearing costs resources (cost value " + r.valueBase.ToString("0") + " x " + r.valueGrowth.ToString("0.0#") + "^tier, each line capped to a few slots so the bags can hold it, times a size multiplier " + string.Join("/", Array.ConvertAll(r.sizeCostMult, x => x.ToString("0.##"))) + "), a short builder timer, pays XP and salvage worth at most " + (r.rewardShare * 100).ToString("0") + " % of the cost, and can be finished instantly for the shard skip price of its timer.");
            sb.AppendLine();
            var srows = new List<string[]>();
            string[] names = { "Dwarf", "Small", "Medium", "Large", "Giant", "Colossal" };
            for (int i = 0; i < 6; i++) srows.Add(new[] { names[i], r.padsPerSize[i].ToString(), (r.padsPerSize[i] * r.density).ToString("0.#"), (r.padsPerSize[i] * r.homeDensity).ToString("0.#"), (r.padsPerSize[i] - r.freePads).ToString() });
            ReportData.Table(sb, new[] { "size class", "building pads", "expected obstacles", "on the home world", "blockable pads" }, srows);
            var krows = new List<string[]>();
            foreach (ObstacleKindDef k in cat.obstacles)
                krows.Add(new[] { "`" + k.id + "`", k.biomes.Replace(";", ", ") + (k.needsVoid ? " (Void hazard only)" : ""), "T" + k.minTier, ReportData.Items(cat, k.byTier[Math.Max(k.minTier, 1)].cost), ReportData.Items(cat, k.byTier[Math.Max(k.minTier, 4)].cost), ReportData.Items(cat, k.byTier[8].cost), ReportData.Seconds(ObstacleRules.ClearSeconds(r, k, 4)) + " / " + ReportData.Seconds(ObstacleRules.ClearSeconds(r, k, 8)), ShardCurve.SkipCost(cat.shards, ObstacleRules.ClearSeconds(r, k, 8)) + " shards at T8" });
            ReportData.Table(sb, new[] { "kind", "biomes", "from tier", "cost at its first tier (medium world)", "cost at tier 4+", "cost at tier 8", "time at tier 4 / 8", "instant clear" }, krows);
        }

        // ------------------------------------------------------------------------------------------------ score
        static void ScoreSection(StringBuilder sb, EconomyCatalog cat, Tuning t)
        {
            ScoreFormula f = ScoreFormula.From(t);
            sb.AppendLine("## 14. Frontier Score");
            sb.AppendLine();
            sb.AppendLine("Exactly the plan 8.1 formula as a pure function over `ScoreInputs` (no premium or purchase input exists; a test inspects the type), weights in `ScoreFormula` / `score.*` tuning keys:");
            sb.AppendLine();
            sb.AppendLine("```");
            sb.AppendLine("Reach     R = floor(" + f.reachCoefficient.ToString("0") + " x sqrt(maxDistanceFromHome_ly / " + f.reachDivisorLy.ToString("0") + "))");
            sb.AppendLine("Worlds    W = sum over landed worlds of " + f.worldBase.ToString("0") + " x (1 + tier)       (full value up to " + f.worldFullValueCount + " worlds, best tiers first, then " + (f.worldExcessShare * 100).ToString("0") + " %)");
            sb.AppendLine("Industry  I = sum over structures of " + f.industryBase.ToString("0") + " x level^" + f.industryExponent.ToString("0.0#") + " x (1 + structureTier)");
            sb.AppendLine("Tech      T = " + f.techPerUpgradeLevel.ToString("0") + " x sum(upgradeLevels) + " + f.techPerToolTier.ToString("0") + " x sum(toolTiers bought)");
            sb.AppendLine("Defense   D = sum of cleared waves " + f.defenseBase.ToString("0") + " x sqrt(danger) x min(1, " + f.defenseWaveCap + " / wavesClearedOnThatWorld)   (anti-farm: nothing past " + f.defenseWaveCap + " waves per world)");
            sb.AppendLine("Atlas     A = " + f.atlasPerResource.ToString("0") + " x resourcesDiscovered + " + f.atlasPerRecipe.ToString("0") + " x recipesCrafted + " + f.atlasPerMegaStage.ToString("0") + " x megaprojectStages");
            sb.AppendLine("Score = floor((R + W + I + T + D + A) x (1 + " + f.ascensionStep.ToString("0.00") + " x ascensionRank))");
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine("The weights were retuned against the simulator (the plan's weights gave 12 to 14 k after one hour and about 0.5 M for a finished profile): reach 1200 -> 700, worlds 100 -> 130, industry 60 -> 50 with exponent 1.25 -> 1.6, upgrades 400 -> 300, tool tiers 1500 -> 800, Atlas 250/150/400 -> 100/80/28000. Targets: early hours 5 to 10 k, mid about 100 k, end game 1 M+.");
            sb.AppendLine();
            var rows = new List<string[]>();
            foreach (string name in Archetypes)
            {
                SimResult r = SimFx.Run(name, 1UL, Horizon);
                SimSample last = r.samples[r.samples.Count - 1];
                rows.Add(new[] { name, r.ScoreAtPlayHours(0.25).ToString(), r.ScoreAtPlayHours(1.0).ToString(), r.ScoreAtPlayHours(3.0).ToString(), r.ScoreAtPlayHours(10.0).ToString(), r.ScoreAtDay(30).ToString(), r.ScoreAtDay(r.Days("era_industry")).ToString(), r.ScoreAtDay(r.Days("era_interstellar")).ToString(), r.ScoreAtDay(r.Days("era_ascension")).ToString(), r.ScoreAtDay(365).ToString(), r.finalScore.ToString() });
            }
            ReportData.Table(sb, new[] { "archetype", "15 min played", "1 h", "3 h", "10 h", "day 30", "Industry entry", "Interstellar entry", "Ascension entry", "day 365", "day 730" }, rows);
            var prow = new List<string[]>();
            foreach (string name in Archetypes)
            {
                SimSample last = SimFx.Run(name, 1UL, Horizon).samples[SimFx.Run(name, 1UL, Horizon).samples.Count - 1];
                prow.Add(new[] { name, ((long)last.scoreParts[0]).ToString(), ((long)last.scoreParts[1]).ToString(), ((long)last.scoreParts[2]).ToString(), ((long)last.scoreParts[3]).ToString(), ((long)last.scoreParts[4]).ToString(), ((long)last.scoreParts[5]).ToString() });
            }
            ReportData.Table(sb, new[] { "after 2 years", "Reach", "Worlds", "Industry", "Tech", "Defense", "Atlas" }, prow);
            sb.AppendLine("Defense is tiny in the simulation because the simulated player only calls home-world waves (10 of them); real players who call waves on other worlds will fill that bar (up to " + f.defenseWaveCap + " waves per world pay). The Worlds term is large because the simulated explorer lands on 100 to 440 worlds; both are caps, not targets.");
            sb.AppendLine();
        }

        // ------------------------------------------------------------------------------------------------ tuning
        static void TuningSection(StringBuilder sb, Tuning t)
        {
            sb.AppendLine("## 15. Tuning surface (" + t.Count + " keys)");
            sb.AppendLine();
            sb.AppendLine("All numbers live in one `Tuning` object (string key -> float with default, min, max, group, description); rebuilding the catalog from a modified `Tuning` re-derives every cost, time and effect, so the app can expose any key through Remote Config. Keys per group:");
            sb.AppendLine();
            var groups = new List<string>();
            for (int i = 0; i < t.Count; i++) if (!groups.Contains(t.EntryAt(i).group)) groups.Add(t.EntryAt(i).group);
            var rows = new List<string[]>();
            foreach (string g in groups)
            {
                int n = 0; string example = "";
                for (int i = 0; i < t.Count; i++) if (t.EntryAt(i).group == g) { if (n == 0) example = t.EntryAt(i).key; n++; }
                rows.Add(new[] { g, n.ToString(), "`" + example + "`" });
            }
            ReportData.Table(sb, new[] { "group", "keys", "first key" }, rows);
            sb.AppendLine("The complete list with ranges and descriptions is `TUNING_KEYS.md` (and `data\\tuning.json`).");
            sb.AppendLine();
        }

        // ------------------------------------------------------------------------------------------------ risks
        static void Risks(StringBuilder sb)
        {
            sb.AppendLine("## 16. Known risks");
            sb.AppendLine();
            string[] risks =
            {
                "**The finite content ends.** A hardcore player (2.5 h a day) ticks the last headline goal after about a year and every structure, tool and upgrade level after about 16 months. Ascension (new seeded universe, permanent bonuses), weekly boards and megaprojects beyond stage 5 must exist before the first such players arrive; the simulator does not model an Ascension cycle yet.",
                "**The simulated player is an average, not a person.** It follows a greedy policy over a fixed ladder; real players will wander, skip goals and optimise differently. The bands are wide on purpose (about 2.5x per milestone); use Remote Config (`cost.era.*`, `cost.scale.*`) to retune from live telemetry.",
                "**Storage is a real constraint** (the largest late purchases need 400 to 900 slots of items). The accelerated Warehouse curve, Cargo Compression and the Cargo Depot pooling carry it; if the UI makes managing hundreds of slots painful, raise stack sizes (`value`/stack data) rather than shrinking costs.",
                "**Shard prices were never tested against real buyers.** The curve is the plan's; the pack sizes and cosmetic prices are placeholders chosen so the smallest pack cannot skip a day and larger packs are better value. Free income is about 65 shards a week; if timers turn out to matter more or less than in the simulation, move `shard.skip.*` and the faucet keys.",
                "**Timers matter little in the simulation** (idle waiting on builders is close to zero for regular and casual players) because gathering volume is the bottleneck, as designed. Monetisation pressure must therefore come from convenience and cosmetics and the multi-day megaproject timers, not from ordinary jobs.",
                "**Score term balance.** Atlas via megaproject stages (about 40 % of a finished profile) and Worlds (about a third) dominate late; Defense is nearly empty in the simulation. The weights are data and the Rank screen shows the breakdown, so rebalance after the first leaderboard data.",
                "**Obstacles and pads are not simulated** (the simulator ignores pad counts and clutter), so remote-base build-out is slightly optimistic; the rules keep clutter cheap (a few percent of a structure's cost) to bound the error.",
                "**Universe generator coupling.** Resource availability uses the generator's tier formula and resource table; the Monte-Carlo test proves coverage within 576 ly for generator version 1. A generator change must rerun the coverage tests."
            };
            foreach (string r in risks) sb.AppendLine("- " + r);
            sb.AppendLine();
        }
    }
}
