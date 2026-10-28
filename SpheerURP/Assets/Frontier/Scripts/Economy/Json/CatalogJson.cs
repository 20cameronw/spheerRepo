using System;
using System.Collections.Generic;
using System.Reflection;

namespace Frontier.Economy
{
    /// <summary>Binds parsed JSON (<see cref="MiniJson"/>) onto the public fields of the economy data classes. Strict: unknown properties and wrong types throw.</summary>
    public static class JsonBind
    {
        public static T To<T>(object json) { return (T)Convert(json, typeof(T), typeof(T).Name); }

        static object Convert(object v, Type t, string path)
        {
            if (v == null)
            {
                if (t.IsValueType) throw Bad(path, "a non-null " + t.Name);
                return null;
            }
            if (t == typeof(string)) { if (v is string) return v; throw Bad(path, "a string"); }
            if (t == typeof(bool)) { if (v is bool) return v; throw Bad(path, "a boolean"); }
            if (t == typeof(double)) return Number(v, path);
            if (t == typeof(float))
            {
                double d = Number(v, path);
                if (d < -float.MaxValue || d > float.MaxValue) throw Bad(path, "a finite float");
                return (float)d;
            }
            if (t == typeof(int))
            {
                double d = Number(v, path);
                if (d < int.MinValue || d > int.MaxValue) throw Bad(path, "an integer");
                int n = (int)d;
                if (n != d) throw Bad(path, "an integer");
                return n;
            }
            if (t == typeof(uint))
            {
                double d = Number(v, path);
                if (d < uint.MinValue || d > uint.MaxValue) throw Bad(path, "an unsigned integer");
                uint n = (uint)d;
                if (n != d) throw Bad(path, "an unsigned integer");
                return n;
            }
            if (t.IsEnum)
            {
                string name = v as string;
                if (name == null || !Enum.IsDefined(t, name)) throw Bad(path, "a " + t.Name + " name");
                return Enum.Parse(t, name);
            }
            if (t.IsArray)
            {
                var list = v as List<object>;
                if (list == null) throw Bad(path, "an array");
                Type element = t.GetElementType();
                Array array = Array.CreateInstance(element, list.Count);
                for (int i = 0; i < list.Count; i++) array.SetValue(Convert(list[i], element, path + "[" + i + "]"), i);
                return array;
            }
            var dict = v as Dictionary<string, object>;
            if (dict == null) throw Bad(path, "an object");
            object instance = Activator.CreateInstance(t);
            var fields = new Dictionary<string, FieldInfo>(StringComparer.Ordinal);
            foreach (FieldInfo f in t.GetFields(BindingFlags.Public | BindingFlags.Instance)) fields[f.Name] = f;
            foreach (var field in fields)
                if (!dict.ContainsKey(field.Key)) throw new FormatException(path + " is missing '" + field.Key + "'");
            foreach (KeyValuePair<string, object> kv in dict)
            {
                FieldInfo field;
                if (!fields.TryGetValue(kv.Key, out field)) throw new FormatException(path + " has the unknown property '" + kv.Key + "'; the exporter and the data classes disagree");
                field.SetValue(instance, Convert(kv.Value, field.FieldType, path + "." + kv.Key));
            }
            return instance;
        }

        static double Number(object v, string path)
        {
            if (v is double) return (double)v;
            throw Bad(path, "a number");
        }

        static FormatException Bad(string path, string expected) { return new FormatException(path + " must be " + expected); }
    }

    /// <summary>
    /// Reads the committed economy export (docs/economy/data) back into an <see cref="EconomyCatalog"/>. The file access is a delegate so this
    /// stays engine free and platform free: the importer and the tests pass a reader for the folder.
    /// </summary>
    public static class CatalogJson
    {
        /// <summary>Reads every family and returns an indexed catalog. Throws <see cref="FormatException"/> when the files are newer than this code or malformed.</summary>
        public static EconomyCatalog Read(Func<string, string> readFile)
        {
            var manifest = Dict(MiniJson.Parse(readFile("manifest.json")), "manifest");
            int schema = JsonBind.To<int>(manifest["schemaVersion"]);
            if (schema != CatalogBuilder.SchemaVersion)
                throw new FormatException("the export has schema " + schema + " but this build reads schema " + CatalogBuilder.SchemaVersion + "; update the importer or re-export");
            int version = JsonBind.To<int>(manifest["contentVersion"]);
            var files = new HashSet<string>(JsonBind.To<string[]>(manifest["files"]), StringComparer.Ordinal);
            foreach (string required in new[] { "resources", "resource_habitats", "recipes", "structures", "tools", "upgrades", "research",
                "legacy_ids", "start", "obstacles", "shards", "score_formula", "travel", "era_targets", "tuning", "remote_config" })
                if (!files.Contains(required + ".json")) throw new FormatException("Manifest is missing " + required + ".json");
            foreach (string file in files)
            {
                if (file.IndexOfAny(new[] { '/', '\\' }) >= 0 || !file.EndsWith(".json", StringComparison.Ordinal))
                    throw new FormatException("Invalid manifest file name: " + file);
                var envelope = Dict(MiniJson.Parse(readFile(file)), file);
                if (JsonBind.To<int>(envelope["schemaVersion"]) != schema || JsonBind.To<int>(envelope["contentVersion"]) != version)
                    throw new FormatException(file + " has a different schema/content version from the manifest");
                if (JsonBind.To<string>(envelope["family"]) != file.Substring(0, file.Length - 5))
                    throw new FormatException(file + " has the wrong family");
            }
            var cat = new EconomyCatalog
            {
                schemaVersion = schema,
                contentVersion = JsonBind.To<int>(manifest["contentVersion"]),
                resources = Items<ResourceDef[]>(readFile, "resources.json"),
                habitats = Items<HabitatDef[]>(readFile, "resource_habitats.json"),
                recipes = Items<RecipeDef[]>(readFile, "recipes.json"),
                structures = Items<StructureDef[]>(readFile, "structures.json"),
                tools = Items<ToolLadderDef[]>(readFile, "tools.json"),
                upgrades = Items<UpgradeDef[]>(readFile, "upgrades.json"),
                research = Items<ResearchNodeDef[]>(readFile, "research.json"),
                legacyIds = Items<LegacyIdEntry[]>(readFile, "legacy_ids.json")
            };
            var start = Dict(Items(readFile, "start.json"), "start");
            cat.start = JsonBind.To<StartDef>(start["start"]);
            cat.categoryHex = JsonBind.To<string[]>(start["categoryHex"]);
            var obstacles = Dict(Items(readFile, "obstacles.json"), "obstacles");
            cat.obstacleRules = JsonBind.To<ObstacleRulesDef>(obstacles["rules"]);
            cat.obstacles = JsonBind.To<ObstacleKindDef[]>(obstacles["kinds"]);
            cat.shards = JsonBind.To<ShardRulesDef>(Dict(Items(readFile, "shards.json"), "shards")["rules"]);
            cat.Index();
            return cat;
        }

        /// <summary>The score weights of the export.</summary>
        public static ScoreFormula ReadScoreFormula(Func<string, string> readFile)
        {
            return JsonBind.To<ScoreFormula>(Dict(Items(readFile, "score_formula.json"), "score_formula")["formula"]);
        }

        public static object Items(Func<string, string> readFile, string file)
        {
            var envelope = Dict(MiniJson.Parse(readFile(file)), file);
            return envelope["items"];
        }

        static T Items<T>(Func<string, string> readFile, string file) { return JsonBind.To<T>(Items(readFile, file)); }

        static Dictionary<string, object> Dict(object value, string what)
        {
            var dict = value as Dictionary<string, object>;
            if (dict == null) throw new FormatException(what + " must be a JSON object");
            return dict;
        }
    }
}
