using System;
using System.Collections.Generic;
using System.Reflection;

namespace Frontier
{
    /// <summary>Field-level drift, with explicit top-level authoring locks. Order is significant in all catalog arrays.</summary>
    public static class ContentDrift
    {
        public static List<string> Compare(object expected, object actual, Func<string, bool> skip = null)
        {
            var result = new List<string>();
            Walk(expected, actual, "", result, skip);
            return result;
        }

        static void Walk(object a, object b, string path, List<string> result, Func<string, bool> skip)
        {
            if (skip != null && skip(path.Split('.')[0].Split('[')[0])) return;
            if (a == null || b == null)
            {
                // Unity serializes null strings and arrays as empty.
                object other = a ?? b;
                if (other == null || other is string s && s.Length == 0 || other is Array arr && arr.Length == 0) return;
                result.Add(path + ": null differs");
                return;
            }
            Type t = a.GetType();
            if (t != b.GetType()) { result.Add(path + ": type differs"); return; }
            if (t.IsPrimitive || t.IsEnum || a is string)
            {
                if (!a.Equals(b)) result.Add(path + ": " + a + " != " + b);
                return;
            }
            if (a is Array x && b is Array y)
            {
                if (x.Length != y.Length) { result.Add(path + ": length differs"); return; }
                for (int i = 0; i < x.Length; i++) Walk(x.GetValue(i), y.GetValue(i), path + "[" + i + "]", result, skip);
                return;
            }
            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
                Walk(f.GetValue(a), f.GetValue(b), path.Length == 0 ? f.Name : path + "." + f.Name, result, skip);
        }
    }
}
