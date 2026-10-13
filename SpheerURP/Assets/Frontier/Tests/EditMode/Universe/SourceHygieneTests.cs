using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace Frontier.Universe.Tests
{
    /// <summary>
    /// Source hygiene: the core must stay free of engine references and of the nondeterministic or platform dependent APIs the
    /// generator forbids. It reads the sources of Frontier.Core (Scripts/Core/Universe) from wherever the tests run: the Unity editor
    /// (project folder), the plain .NET CI project (a bin folder inside the repository) or the original sandbox layout.
    /// </summary>
    [TestFixture]
    public class SourceHygieneTests
    {
        static readonly string[] CoreDirCandidates =
        {
            Path.Combine("SpheerURP", "Assets", "Frontier", "Scripts", "Core", "Universe"),
            Path.Combine("Assets", "Frontier", "Scripts", "Core", "Universe"),
            Path.Combine("src", "Core", "Universe")
        };

        static string FindCoreDir()
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
                    foreach (string relative in CoreDirCandidates)
                    {
                        string candidate = Path.Combine(dir, relative);
                        if (Directory.Exists(candidate)) return candidate;
                    }
                    dir = Path.GetDirectoryName(dir);
                }
            }
            return null;
        }

        // A hygiene test that silently scans nothing would protect nothing, so a missing or suspiciously small source folder fails.
        static string RequireCoreDir()
        {
            string dir = FindCoreDir();
            if (dir == null) Assert.Fail("Frontier.Core sources (Scripts/Core/Universe) were not found from " + AppContext.BaseDirectory + " or " + Directory.GetCurrentDirectory());
            Assert.GreaterOrEqual(Directory.GetFiles(dir, "*.cs").Length, 15, "unexpectedly few core source files in " + dir);
            return dir;
        }

        static readonly string[] Banned =
        {
            "UnityEngine", "UnityEditor", "System.Random", "new Random(", "string.GetHashCode", "Guid.NewGuid", "DateTime.", "Environment.TickCount",
            "Stopwatch", "System.Linq", "Thread.Sleep"
        };

        // Platform math: allowed only through DetMath (word-boundary match so that "DetMath.Exp(" is not flagged as "Math.Exp(").
        static readonly System.Text.RegularExpressions.Regex PlatformMath = new System.Text.RegularExpressions.Regex(
            @"(?<![A-Za-z0-9_])(Math|MathF)\.(Sin|Cos|Tan|Exp|Log|Log10|Log2|Pow|Atan|Atan2|Asin|Acos|Sinh|Cosh|Tanh|Cbrt|FusedMultiplyAdd|IEEERemainder|ScaleB|BitIncrement|BitDecrement)\b");

        [Test]
        public void CoreSources_AvoidForbiddenApis()
        {
            string dir = RequireCoreDir();
            int files = 0, lines = 0;
            var problems = new List<string>();
            foreach (string path in Directory.GetFiles(dir, "*.cs"))
            {
                files++;
                string name = Path.GetFileName(path);
                string[] src = File.ReadAllLines(path);
                for (int i = 0; i < src.Length; i++)
                {
                    lines++;
                    string t = src[i].Trim();
                    if (t.StartsWith("//") || t.StartsWith("*") || t.StartsWith("/*")) continue;
                    foreach (string b in Banned)
                        if (t.Contains(b)) problems.Add(name + ":" + (i + 1) + " uses " + b);
                    if (name != "DetMath.cs" && PlatformMath.IsMatch(t)) problems.Add(name + ":" + (i + 1) + " uses platform math (" + PlatformMath.Match(t).Value + "), use DetMath");
                    if (name == "DetMath.cs" && PlatformMath.IsMatch(t)) problems.Add(name + ":" + (i + 1) + " DetMath itself must not call platform math (" + PlatformMath.Match(t).Value + ")");
                    if (t.Contains("GetHashCode()") && !t.Contains("override int GetHashCode()")) problems.Add(name + ":" + (i + 1) + " calls GetHashCode()");
                    if (t.Contains("namespace ") && t.EndsWith(";")) problems.Add(name + ":" + (i + 1) + " file-scoped namespace (C# 10)");
                    if (t.StartsWith("public record ") || t.StartsWith("internal record ") || t.Contains(" required ") || t.Contains(" init;")) problems.Add(name + ":" + (i + 1) + " C# 9+ feature that Unity does not allow");
                }
            }
            TestLog.Info("scanned " + files + " source files, " + lines + " lines");
            Assert.AreEqual(0, problems.Count, string.Join("\n", problems));
        }

        [Test]
        public void CoreSources_HaveNoStaticMutableState()
        {
            string dir = RequireCoreDir();
            var problems = new List<string>();
            foreach (string path in Directory.GetFiles(dir, "*.cs"))
            {
                string[] src = File.ReadAllLines(path);
                for (int i = 0; i < src.Length; i++)
                {
                    string t = src[i].Trim();
                    if (t.StartsWith("//") || t.StartsWith("*")) continue;
                    bool isStaticMember = t.StartsWith("static ") || t.StartsWith("public static ") || t.StartsWith("internal static ") || t.StartsWith("private static ");
                    if (!isStaticMember) continue;
                    if (t.Contains("(") || t.Contains("readonly") || t.Contains("const ") || t.Contains("class ") || t.Contains("struct ")) continue;
                    if (t.Contains("{ get") || t.Contains("=>")) continue;
                    if (t.EndsWith(";")) problems.Add(Path.GetFileName(path) + ":" + (i + 1) + " " + t);
                }
            }
            Assert.AreEqual(0, problems.Count, "static mutable state: " + string.Join("\n", problems));
        }

        [Test]
        public void CoreSources_DoNoFloatArithmetic()
        {
            // Float arithmetic is evaluated with different intermediate precision by Mono (editor), IL2CPP (devices) and CoreCLR (server).
            // The generator computes in double and rounds to float once, with an explicit cast.
            string dir = RequireCoreDir();
            var floatOp = new System.Text.RegularExpressions.Regex(@"(?<![A-Za-z0-9_.])\d+\.?\d*[fF]\b\s*[*/+\-]|[A-Za-z0-9_)\]]\s*[*/+\-]\s*\d+\.?\d*[fF]\b");
            var problems = new List<string>();
            foreach (string path in Directory.GetFiles(dir, "*.cs"))
            {
                string name = Path.GetFileName(path);
                if (name == "Hash.cs") continue; // UnitFloat: exact power-of-two scaling, constant folded
                string[] src = File.ReadAllLines(path);
                for (int i = 0; i < src.Length; i++)
                {
                    string t = src[i].Trim();
                    if (t.StartsWith("//") || t.StartsWith("*")) continue;
                    if (floatOp.IsMatch(t)) problems.Add(name + ":" + (i + 1) + " float arithmetic: " + t);
                    if (t.Contains("NextFloat(") || t.Contains("UnitFloat(")) problems.Add(name + ":" + (i + 1) + " draws a float: " + t);
                }
            }
            Assert.AreEqual(0, problems.Count, string.Join("\n", problems));
        }

        [Test]
        public void CoreAssembly_ReferencesOnlyNetStandard()
        {
            var asm = typeof(Universe).Assembly;
            foreach (var r in asm.GetReferencedAssemblies())
                Assert.IsTrue(r.Name == "netstandard" || r.Name == "System.Runtime" || r.Name == "mscorlib", "unexpected reference " + r.Name);
        }
    }
}
