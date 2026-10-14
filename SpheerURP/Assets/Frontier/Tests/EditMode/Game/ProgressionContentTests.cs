using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    [TestFixture]
    public class ProgressionContentTests
    {
        SaveSandbox sandbox;
        GameDatabase db;

        [SetUp]
        public void SetUp()
        {
            sandbox = new SaveSandbox();
            db = GameDatabase.Load();
        }

        [TearDown]
        public void TearDown()
        {
            sandbox.Dispose();
        }

        [Test]
        public void ResearchPurchase_SpendsCostAndSurvivesSaveLoad()
        {
            var state = new GameState(db, false);
            if (!db.UsesImportedEconomy || db.research == null || db.research.Length == 0)
                Assert.Ignore("Imported research catalog is unavailable");
            ReachHighestAuthoredTier(state);
            PlaceResearchLab(state);
            GiveMaterials(state);

            var node = db.research.FirstOrDefault(candidate => candidate != null && candidate.data != null &&
                candidate.data.tier <= state.ReachTier && candidate.data.labLevel <= 20 &&
                (candidate.data.prereqs == null || candidate.data.prereqs.Length == 0));
            Assert.IsNotNull(node, "catalog should contain a reachable root research node");
            Assert.IsTrue(state.CanResearch(node), state.ResearchLockReason(node));
            var firstCost = node.data.cost.FirstOrDefault(item => item.amount > 0);
            int before = firstCost.amount > 0 ? state.Count(db.ResolveResource(firstCost.resourceId)) : 0;

            Assert.IsTrue(state.Research(node));
            Assert.IsTrue(state.IsResearched(node.data.id));
            if (firstCost.amount > 0)
                Assert.AreEqual(before - firstCost.amount, state.Count(db.ResolveResource(firstCost.resourceId)));

            Assert.IsTrue(state.Save());
            var loaded = new GameState(db, true);
            Assert.IsTrue(loaded.IsResearched(node.data.id));
            Assert.IsTrue(loaded.LoadReport.warnings.Count == 0, string.Join("; ", loaded.LoadReport.warnings));
            loaded.ResetProgress();
            Assert.IsFalse(loaded.IsResearched(node.data.id), "reset must clear purchased research");
        }

        [Test]
        public void SupportedToolTierPurchase_ChangesHarvestYieldAndSurvivesSaveLoad()
        {
            var state = new GameState(db, false);
            if (!db.UsesImportedEconomy || db.toolLadders == null || db.toolLadders.Length == 0)
                Assert.Ignore("Imported tool ladders are unavailable");
            ReachHighestAuthoredTier(state);
            PlaceResearchLab(state);
            GiveMaterials(state);

            ToolLadderSO selected = null;
            foreach (var ladder in db.toolLadders)
            {
                if (ladder == null || ladder.data == null ||
                    (ladder.data.kind != Economy.ToolKind.Axe && ladder.data.kind != Economy.ToolKind.Pick &&
                     ladder.data.kind != Economy.ToolKind.Drill)) continue;
                var next = ladder.data.tiers.FirstOrDefault(row => row != null && row.tier == state.ToolTier(ladder) + 1);
                if (next == null || next.requiresReachTier > state.ReachTier ||
                    next.requiresLabLevel > 20 || !string.IsNullOrEmpty(next.researchPrereq)) continue;
                if (state.ToolTierLockReason(ladder) == null) { selected = ladder; break; }
            }
            Assert.IsNotNull(selected, "catalog should expose an affordable first supported tool tier");
            var purchased = selected.data.tiers.First(row => row.tier == 2);
            Assert.IsTrue(state.BuyToolTier(selected));
            Assert.AreEqual(2, state.ToolTier(selected));

            MinigameKind game = selected.data.kind == Economy.ToolKind.Axe ? MinigameKind.Chop :
                selected.data.kind == Economy.ToolKind.Pick ? MinigameKind.Mine : MinigameKind.Drill;
            Assert.AreEqual(purchased.yieldMult, state.CatalogToolYieldMultiplier(game), 0.001f);
            Assert.AreEqual(1, state.ToolLevel(game), "the purchased tier should be reflected in the active tool level");

            Assert.IsTrue(state.Save());
            var loaded = new GameState(db, true);
            var loadedLadder = db.toolLadders.First(ladder => ladder.data.id == selected.data.id);
            Assert.AreEqual(2, loaded.ToolTier(loadedLadder));
            Assert.AreEqual(purchased.yieldMult, loaded.CatalogToolYieldMultiplier(game), 0.001f);
            loaded.ResetProgress();
            Assert.AreEqual(1, loaded.ToolTier(loadedLadder), "reset must restore starting tool tiers");
        }

        [Test]
        public void LoadingProgression_DropsUnknownEntriesAndClampsOutOfRangeTiers()
        {
            if (!db.UsesImportedEconomy || db.toolLadders == null || db.toolLadders.Length == 0)
                Assert.Ignore("Imported progression catalog is unavailable");
            var state = new GameState(db, false);
            Assert.IsTrue(state.Save());
            var save = SaveSerializer.Instance.DeserializeV2(File.ReadAllText(sandbox.SaveFile)).Normalize();
            var ladder = db.toolLadders.First(candidate => candidate != null && candidate.data != null);
            int maxTier = ladder.data.tiers.Max(row => row.tier);
            save.player.researched.Add("missing_research");
            save.player.toolTiers.Add(new IdLevel { id = "missing_ladder", level = 2 });
            save.player.toolTiers.Add(new IdLevel { id = ladder.data.id, level = maxTier + 100 });
            File.WriteAllText(sandbox.SaveFile, JsonUtility.ToJson(save));

            var loaded = new GameState(db, true);
            Assert.IsFalse(loaded.IsResearched("missing_research"));
            Assert.AreEqual(maxTier, loaded.ToolTier(ladder));
            CollectionAssert.Contains(loaded.LoadReport.warnings, "dropped the unknown research node 'missing_research'");
            CollectionAssert.Contains(loaded.LoadReport.warnings, "dropped the unknown tool ladder 'missing_ladder'");
            CollectionAssert.Contains(loaded.LoadReport.warnings, "clamped the saved tier for tool '" + ladder.data.id + "'");
        }

        [Test]
        public void ProgressionSurvivesLegacyContentFallbackAndCanBeRestored()
        {
            if (!db.UsesImportedEconomy) Assert.Ignore("Imported progression catalog is unavailable");
            var state = new GameState(db, false);
            ReachHighestAuthoredTier(state);
            PlaceResearchLab(state);
            GiveMaterials(state);
            var root = db.research.FirstOrDefault(node => node != null && node.data != null &&
                node.data.tier <= state.ReachTier && node.data.labLevel <= 20 &&
                (node.data.prereqs == null || node.data.prereqs.Length == 0));
            Assert.IsNotNull(root);
            Assert.IsTrue(state.Research(root));
            ToolLadderSO ladder = null;
            foreach (var candidate in db.toolLadders)
            {
                if (candidate == null || candidate.data == null ||
                    (candidate.data.kind != Economy.ToolKind.Axe && candidate.data.kind != Economy.ToolKind.Pick &&
                     candidate.data.kind != Economy.ToolKind.Drill)) continue;
                var tier = candidate.data.tiers.FirstOrDefault(row => row != null && row.tier == 2);
                if (tier != null && tier.requiresReachTier <= state.ReachTier &&
                    tier.requiresLabLevel <= 20 && string.IsNullOrEmpty(tier.researchPrereq))
                {
                    ladder = candidate;
                    break;
                }
            }
            Assert.IsNotNull(ladder, "catalog should contain a supported tier-two tool without additional research");
            Assert.IsTrue(state.BuyToolTier(ladder));
            Assert.IsTrue(state.Save());

            var legacy = new GameState(GameDatabase.LoadLegacy(), true);
            Assert.IsTrue(legacy.IsResearched(root.data.id));
            Assert.AreEqual(2, legacy.ToolTier(ladder));
            Assert.IsTrue(legacy.Save());

            var restored = new GameState(db, true);
            Assert.IsTrue(restored.IsResearched(root.data.id));
            Assert.AreEqual(2, restored.ToolTier(ladder));
        }

        static void ReachHighestAuthoredTier(GameState state)
        {
            PlanetSO highest = null;
            foreach (var planet in state.db.planets)
                if (planet != null && (highest == null || planet.tier > highest.tier)) highest = planet;
            Assert.IsNotNull(highest, "database should contain authored planets");
            var destination = state.universe.Get(highest.id);
            Assert.IsNotNull(destination);
            if (destination.Tier < 1)
                destination = state.universe.FindGenerated(planet => planet.Tier >= 1, 1, 0, 10000);
            Assert.IsNotNull(destination, "universe should generate at least one tier-one world");
            state.DebugTeleport(destination);
        }

        static void GiveMaterials(GameState state)
        {
            foreach (var resource in state.db.resources)
                if (resource != null) state.backpack.Set(resource.id, 100000000);
        }

        static void PlaceResearchLab(GameState state)
        {
            var labDef = state.db.StructureOfKind(StructureKind.ResearchLab);
            if (labDef == null) return;
            var lab = state.DebugPlace(labDef, 0);
            if (lab != null) state.DebugSetStructureLevel(lab, 20);
        }
    }
}
