using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Frontier.Economy;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Frontier.EditorTools
{
    /// <summary>Non-destructive, field-owned import of the reviewed export. The legacy runtime database is not changed.</summary>
    public static class EconomyImporter
    {
        public const string Root = "Assets/Frontier/Data/Economy";
        public const string DatabasePath = "Assets/Frontier/Resources/Frontier/EconomyDatabase.asset";
        static int changed;
        static readonly HashSet<string> touched = new HashSet<string>(StringComparer.Ordinal);

        public static Func<string, string> Reader()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "docs", "economy", "data"));
            return file => File.ReadAllText(Path.Combine(dir, file));
        }

        [MenuItem("Frontier/Economy/Import JSON")]
        public static void Import()
        {
            var reader = Reader();
            var cat = CatalogJson.Read(reader);
            var errors = CatalogValidator.Validate(cat);
            if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
            var score = CatalogJson.ReadScoreFormula(reader);
            var tuning = ReadTuning(reader);
            foreach (var s in cat.structures)
                if (!StructureKindMap.TryGet(s.id, out _)) throw new InvalidOperationException("Unmapped structure: " + s.id);
            var legacy = GameDatabase.Load();
            var start = legacy.FindPlanet(cat.start.startPlanetId);
            if (start == null) throw new InvalidOperationException("Missing authored start planet: " + cat.start.startPlanetId);
            changed = 0;
            touched.Clear();

            var resources = cat.resources.Select(d => Upsert<ResourceSO>("Resources", d.id, a =>
            {
                ApplyDefinition(d, a);
                if (string.IsNullOrEmpty(a.description)) Set(a, "description", d.atlasDescription);
                if (!AssetDatabase.Contains(a) && ColorUtility.TryParseHtmlString(d.colorHex, out var color)) a.color = color;
            })).ToArray();
            ResourceSO Find(string id) => resources.FirstOrDefault(r => r.id == id);

            var recipes = cat.recipes.Select(d => Upsert<RecipeSO>("Recipes", d.id, a =>
            {
                ApplyDefinition(d, a);
                Set(a, "inputs", ItemAmounts.FromIds(d.inputs, Find));
                Set(a, "outputs", ItemAmounts.FromIds(d.outputs, Find));
                Set(a, "fabricator", d.stationId == "fabricator");
            })).ToArray();
            var structures = cat.structures.Select(d => Upsert<StructureSO>("Structures", d.id, a =>
            {
                ApplyDefinition(d, a);
                StructureKindMap.TryGet(d.id, out var kind);
                Set(a, "kind", kind);
                Set(a, "levels", d.levels.Select(l => new StructureLevel
                {
                    level = l.level, cost = ItemAmounts.FromIds(l.cost, Find), buildSeconds = l.buildSeconds,
                    requiresReachTier = l.requiresReachTier, requiresLabLevel = l.requiresLabLevel, effect = l.effect, capacity = l.capacity
                }).ToList());
                Set(a, "buildCost", ItemAmounts.FromIds(d.levels[0].cost, Find));
            })).ToArray();
            var upgrades = cat.upgrades.Select(d => Upsert<UpgradeSO>("Upgrades", d.id, a =>
            {
                ApplyDefinition(d, a);
                Set(a, "effect", (UpgradeEffect)Enum.Parse(typeof(UpgradeEffect), d.effect.ToString()));
                Set(a, "category", (UpgradeCategory)Enum.Parse(typeof(UpgradeCategory), d.group.ToString()));
                Set(a, "levels", d.levels.Select(l => new UpgradeLevel
                {
                    level = l.level, displayName = l.displayName, cost = ItemAmounts.FromIds(l.cost, Find),
                    value = l.value, researchPrereq = l.researchPrereq, requiresReachTier = l.requiresReachTier, eraTier = l.eraTier
                }).ToList());
            })).ToArray();
            var tools = cat.tools.Select(d => Upsert<ToolLadderSO>("Tools", d.id, a => Set(a, "data", d))).ToArray();
            var research = cat.research.Select(d => Upsert<ResearchNodeSO>("Research", d.id, a => Set(a, "data", d))).ToArray();
            var obstacles = cat.obstacles.Select(d => Upsert<ObstacleKindSO>("Obstacles", d.id, a => Set(a, "data", d))).ToArray();
            var habitats = Upsert<HabitatTableSO>("Rules", "habitats", a => Set(a, "rows", cat.habitats));
            var obstacleRules = Upsert<ObstacleRulesSO>("Rules", "obstacle_rules", a => Set(a, "data", cat.obstacleRules));
            var shardRules = Upsert<ShardRulesSO>("Rules", "shard_rules", a => Set(a, "data", cat.shards));
            var formula = Upsert<ScoreFormulaSO>("Rules", "score_formula", a => Set(a, "data", score));
            var ids = Upsert<LegacyIdMapSO>("Rules", "legacy_ids", a => Set(a, "rows", cat.legacyIds));
            var tuningAsset = Upsert<EconomyTuningSO>("Rules", "tuning", a =>
            {
                Set(a, "schemaVersion", Tuning.SchemaVersion);
                Set(a, "digest", tuning.Digest().ToString("x"));
                Set(a, "values", tuning.Entries.Select(e => new TuningValue { key = e.key, value = e.value }).ToArray());
            });
            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath);
            bool created = db == null;
            if (created) db = ScriptableObject.CreateInstance<GameDatabase>();
            string before = EditorJsonUtility.ToJson(db);
            db.resources = resources; db.recipes = recipes; db.structures = structures; db.upgrades = upgrades;
            db.toolLadders = tools; db.research = research; db.obstacleKinds = obstacles;
            db.habitats = habitats; db.obstacleRules = obstacleRules; db.shardRules = shardRules; db.scoreFormula = formula;
            db.legacyIds = ids; db.tuning = tuningAsset; db.schemaVersion = cat.schemaVersion; db.contentVersion = cat.contentVersion;
            db.startingItems = ItemAmounts.FromIds(cat.start.startingItems, Find);
            db.baseBackpackSlots = cat.start.baseBackpackSlots; db.builders = cat.start.builders; db.categoryHex = cat.categoryHex;
            db.startPlanet = start; db.fuel = Find("fuel"); db.scrap = Find("scrap");
            // Keep authored Core Worlds and combat assets available, but do not wire this database into the legacy scene yet.
            db.planets = legacy.planets; db.enemies = legacy.enemies; db.formula = legacy.formula;
            if (created) AssetDatabase.CreateAsset(db, DatabasePath);
            SaveChanged(db, before, created);
            AssetDatabase.SaveAssets();
            foreach (string guid in AssetDatabase.FindAssets("t:ContentSO", new[] { Root }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!touched.Contains(path)) Debug.LogWarning("Frontier: retired content retained (not deleted): " + path);
            }
            Validate();
            Debug.Log("Frontier: economy import passed; content=" + cat.contentVersion + " tuning=" + tuning.Digest().ToString("x") +
                " changed=" + changed + " (legacy runtime database unchanged)");
        }

        static T Upsert<T>(string family, string id, Action<T> fill) where T : ContentSO
        {
            if (string.IsNullOrEmpty(id) || id.Any(c => !(c >= 'a' && c <= 'z' || c >= '0' && c <= '9' || c == '_')))
                throw new InvalidOperationException("Unsafe content id: " + id);
            string dir = Root + "/" + family;
            Directory.CreateDirectory(dir);
            string path = dir + "/" + id + ".asset";
            touched.Add(path);
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            bool created = asset == null;
            if (created)
            {
                if (File.Exists(path)) throw new InvalidOperationException("Unexpected asset type at " + path);
                asset = ScriptableObject.CreateInstance<T>();
                asset.name = id;
            }
            string before = EditorJsonUtility.ToJson(asset);
            fill(asset);
            if (created) AssetDatabase.CreateAsset(asset, path);
            SaveChanged(asset, before, created);
            return asset;
        }

        static void SaveChanged(Object asset, string before, bool created)
        {
            if (!created && before == EditorJsonUtility.ToJson(asset)) return;
            EditorUtility.SetDirty(asset);
            changed++;
            Debug.Log("Frontier: " + (created ? "created " : "updated ") + AssetDatabase.GetAssetPath(asset));
        }

        public static void ApplyDefinition(object def, ContentSO asset)
        {
            foreach (FieldInfo source in def.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                FieldInfo target = asset.GetType().GetField(source.Name);
                if (target == null || target.FieldType != source.FieldType) continue;
                if ((source.Name == "displayName" || source.Name == "description") && !string.IsNullOrEmpty(target.GetValue(asset) as string)) continue;
                Set(asset, source.Name, source.GetValue(def));
            }
        }

        static void Set(ContentSO asset, string field, object value)
        {
            if (!asset.IsLocked(field)) asset.GetType().GetField(field).SetValue(asset, value);
        }

        public static Tuning ReadTuning(Func<string, string> reader)
        {
            var item = (Dictionary<string, object>)CatalogJson.Items(reader, "tuning.json");
            var values = JsonBind.To<TuningExport>(item);
            var t = new Tuning();
            foreach (var key in values.keys)
            {
                if (t.Has(key.key)) throw new FormatException("Duplicate tuning key: " + key.key);
                t.Register(key.group, key.key, key.defaultValue, key.min, key.max, key.description);
            }
            if (values.schemaVersion != Tuning.SchemaVersion || values.digest != t.Digest().ToString("x"))
                throw new FormatException("Tuning schema or digest mismatch");
            var manifest = (Dictionary<string, object>)MiniJson.Parse(reader("manifest.json"));
            if (values.digest != (string)manifest["tuningDigest"] || t.Count != JsonBind.To<int>(manifest["tuningKeys"]))
                throw new FormatException("Manifest tuning mismatch");
            return t;
        }

        public sealed class TuningExport { public int schemaVersion; public string digest; public TuningKeyExport[] keys; }
        public sealed class TuningKeyExport { public string key; public string group; public string description; public float defaultValue; public float min; public float max; }

        [MenuItem("Frontier/Economy/Validate Imported Content")]
        public static void Validate()
        {
            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath);
            if (db == null) throw new InvalidOperationException("Import economy first");
            var actual = db.ToCatalog();
            var expected = CatalogJson.Read(Reader());
            var errors = CatalogValidator.Validate(actual);
            errors.AddRange(ContentDrift.Compare(expected, actual, f =>
                new[] { "resources", "recipes", "structures", "upgrades", "tools", "research", "obstacles", "habitats", "obstacleRules", "shards", "legacyIds" }.Contains(f)));
            CompareFamily(expected.resources, db.resources, x => x.ToDef(), errors);
            CompareFamily(expected.recipes, db.recipes, x => x.ToDef(), errors);
            CompareFamily(expected.structures, db.structures, x => x.ToDef(), errors);
            CompareFamily(expected.upgrades, db.upgrades, x => x.ToDef(), errors);
            CompareFamily(expected.tools, db.toolLadders, x => x.ToDef(), errors, true);
            CompareFamily(expected.research, db.research, x => x.data, errors, true);
            CompareFamily(expected.obstacles, db.obstacleKinds, x => x.data, errors, true);
            CompareRule(expected.habitats, db.habitats.rows, db.habitats, "rows", errors);
            CompareRule(expected.obstacleRules, db.obstacleRules.data, db.obstacleRules, "data", errors);
            CompareRule(expected.shards, db.shardRules.data, db.shardRules, "data", errors);
            CompareRule(expected.legacyIds, db.legacyIds.rows, db.legacyIds, "rows", errors);
            CompareRule(CatalogJson.ReadScoreFormula(Reader()), db.scoreFormula.data, db.scoreFormula, "data", errors);
            db.tuning.Build();
            if (errors.Count > 0) throw new InvalidOperationException("Imported economy invalid:\n" + string.Join("\n", errors.Take(40)));
            Debug.Log("Frontier: economy validation passed");
        }

        static void CompareFamily<D, A>(D[] expected, A[] assets, Func<A, D> def, List<string> errors, bool wrapped = false) where A : ContentSO
        {
            if (assets.Length != expected.Length) { errors.Add(typeof(A).Name + ": count differs"); return; }
            for (int i = 0; i < expected.Length; i++)
            {
                A asset = assets[i];
                foreach (string field in asset.lockedFields ?? Array.Empty<string>())
                    if (field == "id" || asset.GetType().GetField(field) == null)
                        errors.Add(asset.name + ": invalid locked field " + field);
                var actual = def(asset);
                string expectedId = expected[i].GetType().GetField("id").GetValue(expected[i]) as string;
                string actualId = actual.GetType().GetField("id").GetValue(actual) as string;
                if (actualId != expectedId) errors.Add(asset.name + ": id or order differs");
                errors.AddRange(ContentDrift.Compare(expected[i], actual, f =>
                    wrapped ? asset.IsLocked("data") : asset.IsLocked(f) || f == "displayName" || f == "description")
                    .Select(s => asset.name + "." + s));
                if (asset is StructureSO structure &&
                    (!StructureKindMap.TryGet(structure.id, out var kind) || structure.kind != kind))
                    errors.Add(asset.name + ": kind differs from id mapping");
            }
        }

        static void CompareRule(object expected, object actual, ContentSO asset, string field, List<string> errors)
        {
            if (!asset.IsLocked(field)) errors.AddRange(ContentDrift.Compare(expected, actual).Select(s => asset.name + "." + s));
        }
    }
}
