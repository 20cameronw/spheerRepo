using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Frontier.Economy.Tests
{
    /// <summary>
    /// The economy sources live in a Unity asmdef with noEngineReferences and must be deterministic. This scans the files themselves for
    /// anything that would break that. It reads the sources of Frontier.Economy (Scripts/Economy) from wherever the tests run: the Unity
    /// editor (project folder), the plain .NET CI project or the console labs (a bin folder inside the repository) or the original
    /// sandbox layout.
    /// </summary>
    [TestFixture]
    public class SourceHygieneTests
    {
        static readonly string[] SourceDirCandidates =
        {
            Path.Combine("SpheerURP", "Assets", "Frontier", "Scripts", "Economy"),
            Path.Combine("Assets", "Frontier", "Scripts", "Economy"),
            Path.Combine("src", "Economy")
        };

        static string FindSourceDir()
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
                    foreach (string relative in SourceDirCandidates)
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
        static string SourceDir()
        {
            string dir = FindSourceDir();
            if (dir == null) Assert.Fail("Frontier.Economy sources (Scripts/Economy) were not found from " + AppContext.BaseDirectory + " or " + Directory.GetCurrentDirectory());
            Assert.GreaterOrEqual(Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories).Length, 31, "unexpectedly few economy source files in " + dir);
            return dir;
        }

        static string StripComments(string line)
        {
            int c = line.IndexOf("//", StringComparison.Ordinal);
            if (c < 0) return line;
            // keep "//" inside string literals (none of the economy strings contain URLs; a quote before the slashes means it is text)
            int q = line.IndexOf('"');
            return q >= 0 && q < c ? line : line.Substring(0, c);
        }

        static readonly string[] ForbiddenPatterns =
        {
            @"UnityEngine", @"System\.Random", @"new Random\(", @"\bDateTime\b", @"\bStopwatch\b", @"System\.Linq", @"Environment\.TickCount", @"Guid\.NewGuid", @"\bThread\.", @"\bTask\.", @"\bFile\.", @"\bDirectory\.",
            @"\bMath\.Pow\(", @"\bMath\.Exp\(", @"\bMath\.Log\(", @"\bMath\.Log10\(", @"\bMath\.Sin\(", @"\bMath\.Cos\(", @"\bMath\.Tan\(", @"\bMath\.Atan", @"\bMath\.Cbrt\(", @"\bMathF\.", @"\.GetHashCode\(\)", @"Debug\.Log", @"\bConsole\."
        };

        [Test]
        public void EconomySources_UseNoEngineNoClockNoPlatformMathNoStringHash()
        {
            var problems = new List<string>();
            int files = 0, lines = 0;
            foreach (string path in Directory.GetFiles(SourceDir(), "*.cs", SearchOption.AllDirectories))
            {
                files++;
                string[] all = File.ReadAllLines(path);
                for (int i = 0; i < all.Length; i++)
                {
                    lines++;
                    string code = StripComments(all[i]);
                    foreach (string bad in ForbiddenPatterns)
                        if (Regex.IsMatch(code, bad) && !path.EndsWith("DetMath.cs", StringComparison.Ordinal))
                            problems.Add(Path.GetFileName(path) + ":" + (i + 1) + " uses " + bad);
                }
            }
            TestLog.Info(files + " files, " + lines + " lines scanned");
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        [Test]
        public void EconomySources_HaveNoStaticMutableState()
        {
            var problems = new List<string>();
            var field = new Regex(@"^\s*(public |private |internal |protected )*static (?!readonly|class|partial|void)[^(=]*(=[^>].*)?;\s*$");
            var autoProp = new Regex(@"static [^(]*\{\s*get;\s*(private |internal )?set;\s*\}");
            foreach (string path in Directory.GetFiles(SourceDir(), "*.cs", SearchOption.AllDirectories))
            {
                string[] all = File.ReadAllLines(path);
                for (int i = 0; i < all.Length; i++)
                {
                    string code = StripComments(all[i]);
                    if (code.Contains("const ")) continue;
                    if ((field.IsMatch(code) && !code.Contains("readonly")) || autoProp.IsMatch(code)) problems.Add(Path.GetFileName(path) + ":" + (i + 1) + " static mutable state: " + code.Trim());
                }
            }
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        [Test]
        public void EveryEconomyFile_LivesInTheEconomyNamespace_AndNothingIsUnityOnly()
        {
            var problems = new List<string>();
            foreach (string path in Directory.GetFiles(SourceDir(), "*.cs", SearchOption.AllDirectories))
            {
                string text = File.ReadAllText(path);
                if (!text.Contains("namespace Frontier.Economy")) problems.Add(Path.GetFileName(path) + " is outside namespace Frontier.Economy");
                string[] all = File.ReadAllLines(path);
                for (int i = 0; i < all.Length; i++)
                {
                    string code = StripComments(all[i]);
                    if (Regex.IsMatch(code, @"^\s*using\s+Unity")) problems.Add(Path.GetFileName(path) + ":" + (i + 1) + " imports a Unity namespace");
                    if (code.Contains("[SerializeField]") || Regex.IsMatch(code, @":\s*(ScriptableObject|MonoBehaviour)\b")) problems.Add(Path.GetFileName(path) + ":" + (i + 1) + " derives from or mentions a Unity serialization type");
                }
            }
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        [Test]
        public void EconomyAssembly_ReferencesOnlyNetStandardAndCore()
        {
            foreach (var r in typeof(EconomyCatalog).Assembly.GetReferencedAssemblies())
                Assert.IsTrue(r.Name == "netstandard" || r.Name == "System.Runtime" || r.Name == "mscorlib" || r.Name == "Frontier.Core", "unexpected reference " + r.Name);
        }
    }
}
