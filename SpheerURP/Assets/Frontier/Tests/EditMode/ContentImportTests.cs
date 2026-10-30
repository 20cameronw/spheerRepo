using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Frontier.Economy;
using Frontier.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Frontier.Tests
{
    public sealed class ContentImportTests
    {
        [Test]
        public void DefaultBootstrap_RefusesToOverwriteExistingAssets()
        {
            Assert.IsFalse(FrontierMenu.CanGenerateDefaults(
                "Assets/Frontier/Resources/Frontier/GameDatabase.asset",
                "Assets/Frontier/Resources/Frontier/Data", out var reason));
            StringAssert.Contains("already exists", reason);
        }

        [Test]
        public void CommittedAssets_RoundTripAndValidate()
        {
            EconomyImporter.Validate();
            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>(EconomyImporter.DatabasePath);
            Assert.AreEqual(34, db.resources.Length);
            Assert.AreEqual(24, db.recipes.Length);
            Assert.AreEqual(31, db.structures.Length);
            Assert.AreEqual(6, db.toolLadders.Length);
            Assert.AreEqual(16, db.upgrades.Length);
            Assert.AreEqual(60, db.research.Length);
            Assert.AreEqual(8, db.obstacleKinds.Length);
            Assert.AreEqual(Tuning.CreateDefault().Count, db.tuning.Build().Count);
            Assert.AreEqual("sylva", db.startPlanet.id);
            Assert.AreEqual(1, db.builders);
            Assert.AreSame(db, GameDatabase.Load().ContentSource);
        }

        [Test]
        public void ImportTwice_IsByteIdentical_AndPreservesLegacyDatabase()
        {
            var before = Snapshot();
            string legacyPath = AssetDatabase.GetAssetPath(GameDatabase.LoadLegacy());
            byte[] legacy = File.ReadAllBytes(legacyPath);
            EconomyImporter.Import();
            EconomyImporter.Import();
            var after = Snapshot();
            CollectionAssert.AreEquivalent(before.Keys, after.Keys);
            foreach (var entry in before) CollectionAssert.AreEqual(entry.Value, after[entry.Key], entry.Key);
            CollectionAssert.AreEqual(legacy, File.ReadAllBytes(legacyPath));
        }

        static Dictionary<string, byte[]> Snapshot()
        {
            var paths = Directory.GetFiles(EconomyImporter.Root, "*", SearchOption.AllDirectories)
                .Concat(new[] { EconomyImporter.DatabasePath, EconomyImporter.DatabasePath + ".meta" });
            return paths.ToDictionary(p => p, File.ReadAllBytes);
        }

        [Test]
        public void ApplyDefinition_PreservesAuthoredPresentation_AndLockedFields()
        {
            var asset = ScriptableObject.CreateInstance<ResourceSO>();
            try
            {
                asset.displayName = "Authored name";
                asset.description = "Authored description";
                asset.color = Color.magenta;
                asset.icon = "crystal";
                asset.stackSize = 777;
                asset.lockedFields = new[] { "stackSize" };
                var source = CatalogBuilder.Build(Tuning.CreateDefault()).resources[0];
                EconomyImporter.ApplyDefinition(source, asset);
                Assert.AreEqual(source.id, asset.id);
                Assert.AreEqual("Authored name", asset.displayName);
                Assert.AreEqual("Authored description", asset.description);
                Assert.AreEqual(Color.magenta, asset.color);
                Assert.AreEqual("crystal", asset.icon);
                Assert.AreEqual(777, asset.stackSize);
                Assert.IsEmpty(ContentDrift.Compare(source, asset.ToDef(), f => f == "displayName" || f == "stackSize"));
                StringAssert.Contains("stackSize", string.Join("\n", ContentDrift.Compare(source, asset.ToDef())));
            }
            finally { Object.DestroyImmediate(asset); }
        }

        [Test]
        public void ImportedCostsAndCapacities_UseExactTables()
        {
            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>(EconomyImporter.DatabasePath);
            var source = CatalogJson.Read(EconomyImporter.Reader());
            var warehouse = db.FindStructure("warehouse");
            for (int level = 1; level <= 10; level++)
            {
                Assert.AreEqual(source.Structure("warehouse").levels[level - 1].capacity, warehouse.StorageCapacity(level));
                Assert.IsEmpty(ContentDrift.Compare(source.Structure("warehouse").levels[level - 1].cost,
                    ItemAmounts.ToIds(warehouse.CostOfLevel(level))));
            }
            foreach (var recipe in db.recipes)
            {
                Assert.IsTrue(recipe.inputs.All(x => x.resource != null));
                Assert.IsTrue(recipe.outputs.All(x => x.resource != null));
                Assert.IsNotNull(db.FindStructure(recipe.stationId));
            }
            Assert.AreEqual(0, db.fuel.harvestMethods.Length, "Fuel remains crafted only");
        }

        [Test]
        public void StructureMapping_IsCompleteUnique_AndPreservesSerializedValues()
        {
            Assert.AreEqual(0, (int)StructureKind.Refinery);
            Assert.AreEqual(9, (int)StructureKind.Warehouse);
            Assert.AreEqual(1, (int)UpgradeEffect.BackpackSlots);
            var source = CatalogJson.Read(EconomyImporter.Reader());
            var seen = new HashSet<StructureKind>();
            foreach (var structure in source.structures)
            {
                Assert.IsTrue(StructureKindMap.TryGet(structure.id, out var kind), structure.id);
                Assert.IsTrue(seen.Add(kind), "Kind must have one canonical id: " + kind);
            }
            Assert.AreEqual(Enum.GetValues(typeof(StructureKind)).Length, seen.Count);
            foreach (var structure in GameDatabase.Load().structures)
                Assert.IsTrue(structure.Implemented, "Catalog structure has no implemented behavior: " + structure.id);
        }
    }
}
