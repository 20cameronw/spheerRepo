using System;
using Frontier.Hyperspace;
using Frontier.Hyperspace.View;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Frontier.Tests
{
    public sealed class EncounterCatalogTests
    {
        [Test]
        public void SelectedFormationValidatorAcceptsFlyableContentAndRejectsImpossibleGaps()
        {
            var previousSelection = Selection.activeObject;
            var asset = ScriptableObject.CreateInstance<EncounterSO>();
            try
            {
                Selection.activeObject = asset;
                asset.template = new TemplateBuilder("fair", 0, 0, 1f, EncounterRole.Calm, EncounterFamily.Drones)
                    .Drone(0f, 0f, 0f).Done();
                Assert.DoesNotThrow(Frontier.Editor.FrontierEncounterAssets.ValidateSelected);
                asset.template = new TemplateBuilder("wall", 0, 0, 1f, EncounterRole.Peak, EncounterFamily.Gates)
                    .Gate(GateKind.Slit, 0f, 0f, 0f, 0.2f, scaleGap: false).Done();
                Assert.Throws<InvalidOperationException>(Frontier.Editor.FrontierEncounterAssets.ValidateSelected);
            }
            finally
            {
                Selection.activeObject = previousSelection;
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void AuthoredCatalogPreservesAllExistingTemplatesAndRuntimeUsesIt()
        {
            var catalog = Resources.Load<EncounterCatalogSO>("Frontier/EncounterCatalog");
            Assert.IsNotNull(catalog);
            var authored = catalog.BuildLibrary();
            var defaults = EncounterLibrary.CreateDefault();
            Assert.AreEqual(defaults.Count, authored.Count);
            for (int i = 0; i < authored.Count; i++)
            {
                Assert.AreEqual(JsonUtility.ToJson(defaults[i]), JsonUtility.ToJson(authored[i]), defaults[i].id);
                Assert.AreEqual(JsonUtility.ToJson(authored[i]), JsonUtility.ToJson(HyperspaceSetup.Library[i]));
            }
        }

        [Test]
        public void AuthoredCatalogPreservesNineCanonicalPlanEventAndStateDigests()
        {
            var catalog = Resources.Load<EncounterCatalogSO>("Frontier/EncounterCatalog");
            var actual = SelfCheck.Run(new HyperspaceTuning(), catalog.BuildLibrary());
            CollectionAssert.AreEqual(Frontier.Hyperspace.Tests.Golden.Digests, actual);
        }

        [Test]
        public void InvalidCatalogFailsExplicitlyAndBuildDoesNotMutateAssets()
        {
            var catalog = ScriptableObject.CreateInstance<EncounterCatalogSO>();
            var encounter = ScriptableObject.CreateInstance<EncounterSO>();
            try
            {
                Assert.Throws<ArgumentException>(() => catalog.BuildLibrary());
                catalog.encounters = new EncounterSO[] { null };
                Assert.Throws<InvalidOperationException>(() => catalog.BuildLibrary());
                encounter.template = new TemplateBuilder("test", 0, 12, 1f, EncounterRole.Calm, EncounterFamily.Drones)
                    .Drone(0f, 0f, 0f).Done();
                catalog.encounters = new[] { encounter };
                var library = catalog.BuildLibrary();
                library[0].entries[0].x = 90f;
                Assert.AreEqual(0f, encounter.template.entries[0].x);
                Assert.AreEqual(0f, catalog.BuildLibrary()[0].entries[0].x);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
                UnityEngine.Object.DestroyImmediate(encounter);
            }
        }
    }
}
