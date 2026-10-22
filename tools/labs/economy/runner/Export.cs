using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Frontier.Economy;
using Frontier.Universe;

namespace Frontier.Economy.Runner
{
    /// <summary>Writes the versioned JSON the editor generator will import (one file per data family, stable field names).</summary>
    public static class Export
    {
        public const string Generator = "frontier-economy-dev";

        static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            WriteIndented = true,
            IncludeFields = true,
            Converters = { new JsonStringEnumConverter() },
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        sealed class Envelope<T>
        {
            public int schemaVersion;
            public int contentVersion;
            public string generator;
            public string family;
            public T items;
        }

        public static string DefaultDir(string name) { return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", name)); }

        static void Write<T>(string dir, string file, string family, EconomyCatalog cat, T items)
        {
            var env = new Envelope<T> { schemaVersion = CatalogBuilder.SchemaVersion, contentVersion = CatalogBuilder.ContentVersion, generator = Generator, family = family, items = items };
            File.WriteAllText(Path.Combine(dir, file), JsonSerializer.Serialize(env, Options) + "\n");
            Console.WriteLine("  " + file);
        }

        public static int Run(string[] args)
        {
            string dir = args.Length > 1 ? Path.GetFullPath(args[1]) : DefaultDir("data");
            Directory.CreateDirectory(dir);
            Tuning t = Tuning.CreateDefault();
            EconomyCatalog cat = CatalogBuilder.Build(t);
            Console.WriteLine("exporting to " + dir);
            Write(dir, "resources.json", "resources", cat, cat.resources);
            Write(dir, "resource_habitats.json", "resource_habitats", cat, cat.habitats);
            Write(dir, "recipes.json", "recipes", cat, cat.recipes);
            Write(dir, "structures.json", "structures", cat, cat.structures);
            Write(dir, "tools.json", "tools", cat, cat.tools);
            Write(dir, "upgrades.json", "upgrades", cat, cat.upgrades);
            Write(dir, "research.json", "research", cat, cat.research);
            Write(dir, "legacy_ids.json", "legacy_ids", cat, cat.legacyIds);
            Write(dir, "start.json", "start", cat, new { cat.start, cat.categoryHex });
            Write(dir, "obstacles.json", "obstacles", cat, new { rules = cat.obstacleRules, kinds = cat.obstacles });
            Write(dir, "shards.json", "shards", cat, new { rules = cat.shards, builders = BuildersExport(cat), skipCurve = SkipCurveExport(cat) });
            Write(dir, "score_formula.json", "score_formula", cat, new { formula = ScoreFormula.From(t), keys = KeysOf(t, "score"), notes = "Score = floor((R + W + I + T + D + A) x (1 + ascensionStep x ascensionRank)); see reports/ECONOMY.md" });
            Write(dir, "travel.json", "travel", cat, TravelExport(t, cat));
            Write(dir, "era_targets.json", "era_targets", cat, EraTargets());
            Write(dir, "tuning.json", "tuning", cat, TuningExport(t));
            Write(dir, "remote_config.json", "remote_config", cat, RemoteExport(t));
            var manifest = new
            {
                schemaVersion = CatalogBuilder.SchemaVersion, contentVersion = CatalogBuilder.ContentVersion, tuningSchemaVersion = Tuning.SchemaVersion, generator = Generator,
                tuningKeys = t.Count, tuningDigest = t.Digest().ToString("x"),
                counts = new
                {
                    resources = cat.resources.Length, recipes = cat.recipes.Length, structures = cat.structures.Length, tools = cat.tools.Length, upgrades = cat.upgrades.Length, research = cat.research.Length,
                    habitats = cat.habitats.Length, obstacleKinds = cat.obstacles.Length, legacyIds = cat.legacyIds.Length
                },
                files = new[]
                {
                    "resources.json", "resource_habitats.json", "recipes.json", "structures.json", "tools.json", "upgrades.json", "research.json", "legacy_ids.json", "start.json", "obstacles.json", "shards.json",
                    "score_formula.json", "travel.json", "era_targets.json", "tuning.json", "remote_config.json"
                }
            };
            File.WriteAllText(Path.Combine(dir, "manifest.json"), JsonSerializer.Serialize(manifest, Options) + "\n");
            Console.WriteLine("  manifest.json");
            return 0;
        }

        static object RemoteExport(Tuning t)
        {
            var list = new List<object>();
            for (int i = 0; i < t.Count; i++)
            {
                TuningEntry e = t.EntryAt(i);
                list.Add(new { key = e.key, exposure = IntegrationDoc.Classify(e).ToString(), group = e.group, defaultValue = e.defaultValue, min = e.min, max = e.max });
            }
            return new { note = "Remote = client Remote Config; Server = read by the Cloud Code validator (score weights); Content = changes only with a content update; Dev = simulator only.", keys = list };
        }

        static object TuningExport(Tuning t)
        {
            var list = new List<object>();
            for (int i = 0; i < t.Count; i++)
            {
                TuningEntry e = t.EntryAt(i);
                list.Add(new { key = e.key, group = e.group, description = e.description, defaultValue = e.defaultValue, min = e.min, max = e.max });
            }
            return new { schemaVersion = Tuning.SchemaVersion, digest = t.Digest().ToString("x"), keys = list };
        }

        static object KeysOf(Tuning t, string group)
        {
            var list = new List<object>();
            for (int i = 0; i < t.Count; i++) if (t.EntryAt(i).group == group) list.Add(new { key = t.EntryAt(i).key, defaultValue = t.EntryAt(i).defaultValue });
            return list;
        }

        static object BuildersExport(EconomyCatalog cat)
        {
            StructureDef hub = cat.Structure("colony_hub");
            return new
            {
                start = cat.shards.baseBuilders, max = cat.shards.maxBuilders, researchBuilder = "field_engineering", hubStructure = "colony_hub",
                hubLevelForThird = 3, hubLevelForFourth = 7, thirdBuilderShards = cat.shards.thirdBuilderShards, fourthBuilderShards = cat.shards.fourthBuilderShards,
                hubExtraBuildersByLevel = Levels(hub, l => l.effect)
            };
        }

        static float[] Levels(StructureDef s, Func<StructureLevelDef, float> f)
        {
            var a = new float[s.levels.Length];
            for (int i = 0; i < a.Length; i++) a[i] = f(s.levels[i]);
            return a;
        }

        static object SkipCurveExport(EconomyCatalog cat)
        {
            var rows = new List<object>();
            foreach (double seconds in new[] { 30.0, 60, 300, 600, 1800, 3600, 4 * 3600, 8 * 3600, 12 * 3600, 86400, 2 * 86400, 5 * 86400, 10 * 86400 })
                rows.Add(new { seconds = (int)seconds, shards = ShardCurve.SkipCost(cat.shards, seconds) });
            return rows;
        }

        static object TravelExport(Tuning t, EconomyCatalog cat)
        {
            var travel = new TravelModel(t, cat);
            var rules = new WorldRules(cat, new UniverseConfig());
            UpgradeDef jump = cat.Upgrade("jump"), engine = cat.Upgrade("engine");
            var jumpRange = new List<object>();
            for (int i = 0; i < jump.levels.Length; i++) jumpRange.Add(new { level = i + 1, rangeLy = jump.levels[i].value, researchPrereq = jump.levels[i].researchPrereq });
            var engineRows = new List<object>();
            for (int i = 0; i < engine.levels.Length; i++) engineRows.Add(new { level = i + 1, fuelFactor = engine.levels[i].value });
            var tiers = new List<object>();
            for (int tier = 0; tier <= 12; tier++)
            {
                double d = rules.DistanceForTier(tier);
                var fuel = new List<object>();
                foreach (int ej in new[] { 0, 5, 10 })
                {
                    int jl = 1;
                    while (jl < jump.levels.Length && travel.JumpRangeLy(jl) < Math.Max(d, 1.0)) jl++;
                    TripPlan p = travel.PlanInterSystem(Math.Max(d, 10.0), Math.Max(jl, 1), ej, 0.5, 0.0, false);
                    fuel.Add(new { engineLevel = ej, jumpLevelNeeded = jl, legs = p.hops, fuelOneWay = p.fuel, seconds = p.seconds });
                }
                tiers.Add(new { tier, startsAtLy = Math.Round(d, 1), meanDanger = Math.Round(rules.DangerForTier(tier), 2), platingLevelRequired = travel.RequiredPlatingLevel(tier), fuelToReachItsStart = fuel });
            }
            var plating = new List<object>();
            for (int i = 0; i < TravelModel.HazardCount; i++)
            {
                UpgradeDef hull = cat.Upgrade(TravelModel.HullIds[i]);
                var lv = new List<object>();
                for (int l = 0; l < hull.levels.Length; l++) lv.Add(new { level = l + 1, requiresReachTier = hull.levels[l].requiresReachTier, researchPrereq = hull.levels[l].researchPrereq });
                plating.Add(new { hazard = TravelModel.HazardOrder[i].ToString(), upgradeId = hull.id, levels = lv });
            }
            var refunds = new List<object>();
            foreach (double q in new[] { 0.0, 0.25, 0.5, 0.75, 1.0 }) refunds.Add(new { quality = q, refundOf100Fuel = travel.Refund(100, q) });
            return new
            {
                fuelFormula = "legFuel = travel.base_fuel + travel.k x legLy ^ travel.p; routeFuel = (legs x base + k x routeLy ^ p) x engineFactor x (1 + cargoFactor x cargoFill) x (1 - discount) x autopilot",
                tierFormula = "tier = clamp(floor(1.6 x log2(1 + distanceFromHomeLy / 60)), 0, tierMax); danger = dangerBase + dangerPerTier x tier",
                parameters = KeysOf(t, "travel"),
                jumpRanges = jumpRange,
                engineFactors = engineRows,
                tiers,
                hullPlating = plating,
                refundByQuality = refunds,
                landingRule = "a planet can be landed on only when the hull plating level of every hazard it has is at least RequiredPlatingLevel(planetTier)"
            };
        }

        static object EraTargets()
        {
            var rows = new List<object>();
            foreach (Archetype a in Archetype.All())
            {
                var bands = new List<object>();
                foreach (SimBand b in SimBands.For(a.name)) bands.Add(new { milestone = b.milestone, label = b.label, minDays = b.minDays, maxDays = b.maxDays });
                rows.Add(new { archetype = a.name, sessionsPerDay = a.sessionsPerDay, minutesPerSession = a.minutesPerSession, quality = a.quality, efficiency = a.efficiency, bands });
            }
            return new { maxActiveMinutesWithoutVisibleProgress = new[] { SimBands.MaxGoalMinutes(0), SimBands.MaxGoalMinutes(1), SimBands.MaxGoalMinutes(2), SimBands.MaxGoalMinutes(3), SimBands.MaxGoalMinutes(4), SimBands.MaxGoalMinutes(5) }, archetypes = rows };
        }
    }
}
