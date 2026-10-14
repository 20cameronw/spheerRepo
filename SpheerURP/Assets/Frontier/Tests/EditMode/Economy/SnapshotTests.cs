using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Frontier.Economy.Tests
{
    /// <summary>
    /// docs/economy/data is the reviewable snapshot of the economy (regenerate it with tools/labs/economy/run.ps1 export docs/economy/data).
    /// If the catalog or the default tuning changes without the snapshot being regenerated, these tests fail so the change shows up in review.
    /// </summary>
    [TestFixture]
    public class SnapshotTests
    {
        static string FindManifest()
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
                    string candidate = Path.Combine(dir, "docs", "economy", "data", "manifest.json");
                    if (File.Exists(candidate)) return candidate;
                    dir = Path.GetDirectoryName(dir);
                }
            }
            return null;
        }

        static string ReadManifest()
        {
            string path = FindManifest();
            if (path == null) Assert.Fail("docs/economy/data/manifest.json was not found from " + AppContext.BaseDirectory + " or " + Directory.GetCurrentDirectory());
            return File.ReadAllText(path);
        }

        static string Field(string json, string name)
        {
            Match m = Regex.Match(json, "\"" + Regex.Escape(name) + "\"\\s*:\\s*\"?([A-Za-z0-9_.\\-]+)\"?");
            if (!m.Success) Assert.Fail("manifest field missing: " + name);
            return m.Groups[1].Value;
        }

        [Test]
        public void CommittedManifest_MatchesTheDefaultTuning()
        {
            string json = ReadManifest();
            Tuning t = Tuning.CreateDefault();
            Assert.AreEqual(Field(json, "tuningDigest"), t.Digest().ToString("x"), "tuning digest drifted: regenerate docs/economy/data");
        }

        [Test]
        public void CommittedManifest_MatchesTheCatalogCounts()
        {
            string json = ReadManifest();
            EconomyCatalog c = CatalogBuilder.Build(Tuning.CreateDefault());
            Assert.AreEqual(int.Parse(Field(json, "resources")), c.resources.Length, "resources");
            Assert.AreEqual(int.Parse(Field(json, "recipes")), c.recipes.Length, "recipes");
            Assert.AreEqual(int.Parse(Field(json, "structures")), c.structures.Length, "structures");
            Assert.AreEqual(int.Parse(Field(json, "tools")), c.tools.Length, "tools");
            Assert.AreEqual(int.Parse(Field(json, "upgrades")), c.upgrades.Length, "upgrades");
            Assert.AreEqual(int.Parse(Field(json, "research")), c.research.Length, "research");
            Assert.AreEqual(int.Parse(Field(json, "habitats")), c.habitats.Length, "habitats");
            Assert.AreEqual(int.Parse(Field(json, "obstacleKinds")), c.obstacles.Length, "obstacle kinds");
            Assert.AreEqual(int.Parse(Field(json, "legacyIds")), c.legacyIds.Length, "legacy ids");
        }

        [Test]
        public void CommittedSnapshot_ListsEveryFileTheManifestNames()
        {
            string manifest = FindManifest();
            string json = ReadManifest();
            string dir = Path.GetDirectoryName(manifest);
            MatchCollection files = Regex.Matches(json, "\"([a-z_]+\\.json)\"");
            Assert.GreaterOrEqual(files.Count, 16, "manifest should name the 16 exported files");
            foreach (Match m in files) Assert.IsTrue(File.Exists(Path.Combine(dir, m.Groups[1].Value)), "missing snapshot file " + m.Groups[1].Value);
        }
    }
}
