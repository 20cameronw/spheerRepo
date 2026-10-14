using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Frontier.Economy.Tests
{
    /// <summary>Structural comparison of two economy objects by their public fields (exact equality, null equals null only). Used by the export and the ScriptableObject drift tests.</summary>
    public static class CatalogCompare
    {
        /// <summary>Human readable differences between two values (empty when equal), at most <paramref name="limit"/> of them.</summary>
        public static List<string> Diff(object expected, object actual, string path, int limit = 30)
        {
            var diffs = new List<string>();
            Walk(expected, actual, path, diffs, limit);
            return diffs;
        }

        static string Show(object v)
        {
            if (v == null) return "null";
            var s = v as string;
            if (s != null) return "\"" + s + "\"";
            return Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture);
        }

        static string Label(object element, int index)
        {
            if (element == null) return "[" + index + "]";
            FieldInfo id = element.GetType().GetField("id", BindingFlags.Public | BindingFlags.Instance);
            if (id != null && id.FieldType == typeof(string)) return "[" + id.GetValue(element) + "]";
            FieldInfo tier = element.GetType().GetField("level", BindingFlags.Public | BindingFlags.Instance);
            return "[" + (tier != null ? "level " + tier.GetValue(element) : index.ToString()) + "]";
        }

        static void Walk(object a, object b, string path, List<string> diffs, int limit)
        {
            if (diffs.Count >= limit) return;
            if (a == null || b == null)
            {
                if (a != b) diffs.Add(path + ": " + Show(a) + " vs " + Show(b));
                return;
            }
            Type t = a.GetType();
            if (t != b.GetType()) { diffs.Add(path + ": types " + t.Name + " vs " + b.GetType().Name); return; }
            if (t.IsPrimitive || t == typeof(string) || t.IsEnum)
            {
                if (!a.Equals(b)) diffs.Add(path + ": " + Show(a) + " vs " + Show(b));
                return;
            }
            if (t.IsArray)
            {
                var x = (Array)a;
                var y = (Array)b;
                if (x.Length != y.Length) { diffs.Add(path + ": length " + x.Length + " vs " + y.Length); return; }
                for (int i = 0; i < x.Length; i++) Walk(x.GetValue(i), y.GetValue(i), path + Label(x.GetValue(i), i), diffs, limit);
                return;
            }
            foreach (FieldInfo f in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
                Walk(f.GetValue(a), f.GetValue(b), path + "." + f.Name, diffs, limit);
        }
    }

    /// <summary>Finds the committed economy export (docs/economy/data) from wherever the tests run.</summary>
    public static class ExportFiles
    {
        public static string Dir()
        {
            var starts = new List<string>();
#if UNITY_EDITOR
            starts.Add(UnityEngine.Application.dataPath);
#endif
            starts.Add(AppContext.BaseDirectory);
            starts.Add(Directory.GetCurrentDirectory());
            foreach (string start in starts)
            {
                string dir = start;
                for (int i = 0; i < 12 && !string.IsNullOrEmpty(dir); i++)
                {
                    string candidate = Path.Combine(dir, "docs", "economy", "data");
                    if (File.Exists(Path.Combine(candidate, "manifest.json"))) return candidate;
                    dir = Path.GetDirectoryName(dir);
                }
            }
            throw new FileNotFoundException("docs/economy/data/manifest.json was not found from " + AppContext.BaseDirectory + " or " + Directory.GetCurrentDirectory());
        }

        public static Func<string, string> Reader()
        {
            string dir = Dir();
            return file => File.ReadAllText(Path.Combine(dir, file));
        }
    }
}
