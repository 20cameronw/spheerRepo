using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace Frontier.Universe.Runner
{
    /// <summary>
    /// Tiny reflection-based runner for NUnit-attribute tests ([TestFixture], [Test], [TestCase], [SetUp], [TearDown],
    /// [OneTimeSetUp], [OneTimeTearDown], [Ignore], [Category]). It matches attributes by name, so the very same test assembly runs
    /// unchanged under the Unity Test Framework.
    /// </summary>
    public static class TestRunner
    {
        sealed class Result
        {
            public string Name;
            public string Status;
            public double Millis;
            public string Message;
        }

        static bool Is(object attr, string fullName)
        {
            return attr.GetType().FullName == fullName;
        }

        static bool Has(MemberInfo m, string fullName)
        {
            foreach (object a in m.GetCustomAttributes(true)) if (Is(a, fullName)) return true;
            return false;
        }

        static List<MethodInfo> Methods(Type t, string attr)
        {
            var list = new List<MethodInfo>();
            foreach (MethodInfo m in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                if (Has(m, attr)) list.Add(m);
            return list;
        }

        public static int Run(string[] args)
        {
            string filter = null;
            bool list = false;
            string assemblyPath = Path.Combine(AppContext.BaseDirectory, "Frontier.Core.Tests.dll");
            for (int i = 1; i < args.Length; i++)
            {
                if (args[i] == "--filter" && i + 1 < args.Length) filter = args[++i].ToLowerInvariant();
                else if (args[i] == "--list") list = true;
                else if (args[i] == "--assembly" && i + 1 < args.Length) assemblyPath = args[++i];
            }

            Assembly asm = Assembly.LoadFrom(assemblyPath);
            var results = new List<Result>();
            var total = Stopwatch.StartNew();
            Console.WriteLine("Running tests in " + Path.GetFileName(assemblyPath) + " on " + System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription);

            var fixtures = new List<Type>();
            foreach (Type t in asm.GetTypes())
                if (t.IsClass && !t.IsAbstract && (Has(t, "NUnit.Framework.TestFixtureAttribute") || Methods(t, "NUnit.Framework.TestAttribute").Count > 0)) fixtures.Add(t);
            fixtures.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));

            foreach (Type fx in fixtures)
            {
                var tests = new List<KeyValuePair<MethodInfo, object[]>>();
                foreach (MethodInfo m in fx.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    if (Has(m, "NUnit.Framework.TestAttribute")) tests.Add(new KeyValuePair<MethodInfo, object[]>(m, null));
                    foreach (object a in m.GetCustomAttributes(true))
                    {
                        if (!Is(a, "NUnit.Framework.TestCaseAttribute")) continue;
                        object[] ca = (object[])a.GetType().GetProperty("Arguments").GetValue(a, null);
                        tests.Add(new KeyValuePair<MethodInfo, object[]>(m, ca));
                    }
                }
                if (tests.Count == 0) continue;

                var selected = new List<KeyValuePair<MethodInfo, object[]>>();
                foreach (var kv in tests)
                {
                    string name = fx.Name + "." + kv.Key.Name + (kv.Value == null ? "" : "(" + string.Join(",", kv.Value) + ")");
                    if (filter != null && name.ToLowerInvariant().IndexOf(filter, StringComparison.Ordinal) < 0) continue;
                    selected.Add(kv);
                }
                if (selected.Count == 0) continue;
                if (list)
                {
                    foreach (var kv in selected) Console.WriteLine(fx.Name + "." + kv.Key.Name);
                    continue;
                }

                Console.WriteLine();
                Console.WriteLine("== " + fx.Name);
                object instance = Activator.CreateInstance(fx);
                bool setupOk = true;
                foreach (MethodInfo m in Methods(fx, "NUnit.Framework.OneTimeSetUpAttribute"))
                {
                    try { m.Invoke(instance, null); }
                    catch (TargetInvocationException e)
                    {
                        setupOk = false;
                        results.Add(new Result { Name = fx.Name + ".OneTimeSetUp", Status = "FAIL", Message = Describe(e.InnerException) });
                        Console.WriteLine("[FAIL] " + fx.Name + ".OneTimeSetUp: " + Describe(e.InnerException));
                    }
                }
                if (setupOk)
                {
                    foreach (var kv in selected)
                    {
                        string name = fx.Name + "." + kv.Key.Name + (kv.Value == null ? "" : "(" + string.Join(",", kv.Value) + ")");
                        var r = new Result { Name = name };
                        MethodInfo method = kv.Key;
                        if (Has(method, "NUnit.Framework.IgnoreAttribute"))
                        {
                            r.Status = "IGNORED";
                            results.Add(r);
                            Console.WriteLine("[IGNORED] " + name);
                            continue;
                        }
                        var sw = Stopwatch.StartNew();
                        try
                        {
                            foreach (MethodInfo m in Methods(fx, "NUnit.Framework.SetUpAttribute")) m.Invoke(instance, null);
                            method.Invoke(instance, kv.Value);
                            r.Status = "PASS";
                        }
                        catch (TargetInvocationException e)
                        {
                            Exception inner = e.InnerException;
                            string tn = inner.GetType().FullName;
                            if (tn == "NUnit.Framework.SuccessException") r.Status = "PASS";
                            else if (tn == "NUnit.Framework.IgnoreException") { r.Status = "IGNORED"; r.Message = inner.Message; }
                            else if (tn == "NUnit.Framework.InconclusiveException") { r.Status = "INCONCLUSIVE"; r.Message = inner.Message; }
                            else { r.Status = "FAIL"; r.Message = Describe(inner); }
                        }
                        finally
                        {
                            try { foreach (MethodInfo m in Methods(fx, "NUnit.Framework.TearDownAttribute")) m.Invoke(instance, null); }
                            catch (TargetInvocationException) { }
                        }
                        sw.Stop();
                        r.Millis = sw.Elapsed.TotalMilliseconds;
                        results.Add(r);
                        Console.WriteLine("[" + r.Status + "] " + name + " (" + r.Millis.ToString("0") + " ms)" + (r.Status == "PASS" || r.Message == null ? "" : "\n       " + r.Message.Replace("\n", "\n       ")));
                    }
                }
                foreach (MethodInfo m in Methods(fx, "NUnit.Framework.OneTimeTearDownAttribute"))
                {
                    try { m.Invoke(instance, null); } catch (TargetInvocationException) { }
                }
            }

            if (list) return 0;
            int pass = 0, fail = 0, ignored = 0, inconclusive = 0;
            foreach (Result r in results)
            {
                if (r.Status == "PASS") pass++;
                else if (r.Status == "FAIL") fail++;
                else if (r.Status == "IGNORED") ignored++;
                else inconclusive++;
            }
            Console.WriteLine();
            Console.WriteLine("==================== SUMMARY ====================");
            Console.WriteLine("Total: " + results.Count + "   Passed: " + pass + "   Failed: " + fail + "   Ignored: " + ignored + "   Inconclusive: " + inconclusive
                + "   Time: " + (total.Elapsed.TotalSeconds).ToString("0.00") + " s");
            if (fail > 0)
            {
                Console.WriteLine("FAILED TESTS:");
                foreach (Result r in results) if (r.Status == "FAIL") Console.WriteLine("  " + r.Name);
            }
            return fail == 0 ? 0 : 1;
        }

        static string Describe(Exception e)
        {
            string s = e.GetType().Name + ": " + e.Message;
            string st = e.StackTrace;
            if (st != null)
            {
                string[] lines = st.Split('\n');
                for (int i = 0; i < Math.Min(4, lines.Length); i++) s += "\n   " + lines[i].Trim();
            }
            return s;
        }
    }
}
