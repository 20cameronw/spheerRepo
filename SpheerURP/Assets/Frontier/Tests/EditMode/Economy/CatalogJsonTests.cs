using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Frontier.Economy.Tests
{
    /// <summary>
    /// The importer reads docs/economy/data and the game reads ScriptableObjects. This proves the middle link of that chain without Unity:
    /// the committed JSON equals what <see cref="CatalogBuilder"/> builds from the default tuning, field by field, and the reader is strict.
    /// </summary>
    [TestFixture]
    public class CatalogJsonTests
    {
        [Test]
        public void MiniJson_ReadsEveryValueKind()
        {
            var root = (Dictionary<string, object>)MiniJson.Parse("{\"a\": [1, -2.5, 3e2, true, false, null], \"b\": {\"s\": \"x\\ny\\u0041\\\"\"}, \"e\": []}");
            var a = (List<object>)root["a"];
            Assert.AreEqual(1.0, a[0]);
            Assert.AreEqual(-2.5, a[1]);
            Assert.AreEqual(300.0, a[2]);
            Assert.AreEqual(true, a[3]);
            Assert.AreEqual(false, a[4]);
            Assert.IsNull(a[5]);
            Assert.AreEqual("x\nyA\"", ((Dictionary<string, object>)root["b"])["s"]);
            Assert.AreEqual(0, ((List<object>)root["e"]).Count);
        }

        [TestCase("{")]
        [TestCase("[1,]")]
        [TestCase("{\"a\" 1}")]
        [TestCase("\"open")]
        [TestCase("1 2")]
        [TestCase("tru")]
        [TestCase("01")]
        [TestCase("1.")]
        [TestCase("-.1")]
        [TestCase("1e9999")]
        [TestCase("{\"a\":1,\"a\":2}")]
        [TestCase("\"raw\nnewline\"")]
        public void MiniJson_RejectsMalformedText(string text)
        {
            Assert.Throws<FormatException>(() => MiniJson.Parse(text));
        }

        [Test]
        public void JsonBind_IsStrict_AboutUnknownPropertiesAndTypes()
        {
            Assert.Throws<FormatException>(() => JsonBind.To<LegacyIdEntry>(MiniJson.Parse("{\"kind\":\"Resource\",\"legacyId\":\"a\",\"id\":\"b\",\"extra\":1}")));
            Assert.Throws<FormatException>(() => JsonBind.To<LegacyIdEntry>(MiniJson.Parse("{\"kind\":\"Gadget\",\"legacyId\":\"a\",\"id\":\"b\"}")));
            Assert.Throws<FormatException>(() => JsonBind.To<ItemAmount>(MiniJson.Parse("{\"resourceId\":\"a\",\"amount\":1.5}")));
            Assert.Throws<FormatException>(() => JsonBind.To<ItemAmount>(MiniJson.Parse("{\"resourceId\":\"a\"}")));
            Assert.Throws<FormatException>(() => JsonBind.To<int>(null));
            var ok = JsonBind.To<ItemAmount>(MiniJson.Parse("{\"resourceId\":\"scrap\",\"amount\":7}"));
            Assert.AreEqual("scrap", ok.resourceId);
            Assert.AreEqual(7, ok.amount);
        }

        [Test]
        public void CommittedExport_ReadsBackEqualToTheBuiltCatalog()
        {
            EconomyCatalog fromJson = CatalogJson.Read(ExportFiles.Reader());
            EconomyCatalog built = Fx.NewCatalog();
            var diffs = new List<string>();
            diffs.AddRange(CatalogCompare.Diff(built.resources, fromJson.resources, "resources"));
            diffs.AddRange(CatalogCompare.Diff(built.habitats, fromJson.habitats, "habitats"));
            diffs.AddRange(CatalogCompare.Diff(built.recipes, fromJson.recipes, "recipes"));
            diffs.AddRange(CatalogCompare.Diff(built.structures, fromJson.structures, "structures"));
            diffs.AddRange(CatalogCompare.Diff(built.tools, fromJson.tools, "tools"));
            diffs.AddRange(CatalogCompare.Diff(built.upgrades, fromJson.upgrades, "upgrades"));
            diffs.AddRange(CatalogCompare.Diff(built.research, fromJson.research, "research"));
            diffs.AddRange(CatalogCompare.Diff(built.legacyIds, fromJson.legacyIds, "legacyIds"));
            diffs.AddRange(CatalogCompare.Diff(built.start, fromJson.start, "start"));
            diffs.AddRange(CatalogCompare.Diff(built.categoryHex, fromJson.categoryHex, "categoryHex"));
            diffs.AddRange(CatalogCompare.Diff(built.obstacleRules, fromJson.obstacleRules, "obstacleRules"));
            diffs.AddRange(CatalogCompare.Diff(built.obstacles, fromJson.obstacles, "obstacles"));
            diffs.AddRange(CatalogCompare.Diff(built.shards, fromJson.shards, "shards"));
            diffs.AddRange(CatalogCompare.Diff(ScoreFormula.From(Fx.NewTuning()), CatalogJson.ReadScoreFormula(ExportFiles.Reader()), "scoreFormula"));
            Assert.IsEmpty(diffs, "docs/economy/data is stale (run tools/labs/economy/run.ps1 export docs/economy/data):\n" + string.Join("\n", diffs));
            Assert.AreEqual(built.schemaVersion, fromJson.schemaVersion);
            Assert.AreEqual(built.contentVersion, fromJson.contentVersion);
        }

        [Test]
        public void ReadCatalog_IsIndexed_AndPassesTheCatalogValidator()
        {
            EconomyCatalog fromJson = CatalogJson.Read(ExportFiles.Reader());
            Assert.IsNotNull(fromJson.Resource("steel"));
            Assert.IsNotNull(fromJson.Structure("orbital_relay"));
            Assert.IsEmpty(CatalogValidator.Validate(fromJson), string.Join("\n", CatalogValidator.Validate(fromJson)));
        }

        [TestCase("manifest.json", "\"schemaVersion\": 1", "\"schemaVersion\": 2")]
        [TestCase("recipes.json", "\"contentVersion\": 1", "\"contentVersion\": 2")]
        [TestCase("tools.json", "\"family\": \"tools\"", "\"family\": \"resources\"")]
        [TestCase("manifest.json", "\"tools.json\",", "")]
        public void ReadCatalog_RejectsMixedOrIncompleteExports(string file, string from, string to)
        {
            var reader = ExportFiles.Reader();
            Assert.Throws<FormatException>(() => CatalogJson.Read(name => name == file ? reader(name).Replace(from, to) : reader(name)));
        }

        [Test]
        public void CatalogCompare_FindsADifference()
        {
            EconomyCatalog a = Fx.NewCatalog();
            EconomyCatalog b = Fx.NewCatalog();
            Assert.IsEmpty(CatalogCompare.Diff(a.recipes, b.recipes, "recipes"));
            b.recipes[0].inputs[0].amount += 1;
            var diffs = CatalogCompare.Diff(a.recipes, b.recipes, "recipes");
            Assert.AreEqual(1, diffs.Count);
            StringAssert.Contains("recipes[distill_wood].inputs[0].amount", diffs[0]);
        }
    }
}
