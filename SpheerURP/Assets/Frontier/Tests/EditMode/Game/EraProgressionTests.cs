using System.IO;
using System.Linq;
using Frontier.Economy;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    public class EraProgressionTests
    {
        SaveSandbox sandbox;
        GameDatabase db;
        [SetUp] public void SetUp() { sandbox = new SaveSandbox(); db = GameDatabase.Load(); }
        [TearDown] public void TearDown() { sandbox.Dispose(); }

        [Test]
        public void LiveFacts_UseActualLandings_LabAndPurchasedUpgrade_NotTemporaryShipOverrides()
        {
            var state = new GameState(db, false);
            Assert.AreEqual(FrontierEra.Landfall, state.Era);
            state.DebugTeleport(state.universe.Get(db.planets[1].id));
            Assert.AreEqual(FrontierEra.Foothold, state.Era);
            state.DebugPlace(db.StructureOfKind(StructureKind.ResearchLab), 0);
            Assert.AreEqual(FrontierEra.Foothold, state.Era);
            state.DebugSetUpgrade(db.FindUpgrade("jump"), 1);
            Assert.AreEqual(FrontierEra.Settlement, state.Era);
            state.DebugSetUpgrade(db.FindUpgrade("hull_toxic"), 1);
            Assert.AreEqual(FrontierEra.Industry, state.Era);
        }

        [Test]
        public void ReachedEra_PersistsAfterProjectRemoval_AndResetClearsIt_WithoutShardRewards()
        {
            var state = new GameState(db, false);
            int shards = state.ShardBalance;
            var mega = state.DebugPlace(db.StructureOfKind(StructureKind.Terraformer), 0, 3);
            Assert.IsNotNull(mega);
            Assert.AreEqual(FrontierEra.Ascension, state.Era);
            state.world.Remove(state.planet.Id, mega.record);
            state.Notify();
            Assert.AreEqual(0, state.EraProgress.highestMegaprojectStage);
            Assert.AreEqual(FrontierEra.Ascension, state.Era);
            state.Save();
            var restored = new GameState(db, true);
            Assert.AreEqual(FrontierEra.Ascension, restored.Era);
            Assert.AreEqual(shards, restored.ShardBalance, "Era inference/reload must never grant retroactive currency.");
            restored.ResetProgress();
            Assert.AreEqual(FrontierEra.Landfall, restored.Era);
        }

        [Test]
        public void OlderSaveWithoutEraMarker_InfersOnlyProvenProgress_AndDoesNotDuplicateRewards()
        {
            var state = new GameState(db, false);
            state.DebugSetUpgrade(db.FindUpgrade("hull_rad"), 1);
            state.Add(db.FindResource(GameState.AntimatterId), 1, false);
            Assert.AreEqual(FrontierEra.Industry, state.Era, "Owning antimatter is not evidence of crafting it.");
            state.Save();
            var save = JsonUtility.FromJson<SaveV2>(File.ReadAllText(sandbox.SaveFile));
            save.settings.RemoveAll(x => x.key == "frontier_era_reached");
            File.WriteAllText(sandbox.SaveFile, JsonUtility.ToJson(save));
            var restored = new GameState(db, true);
            Assert.AreEqual(FrontierEra.Industry, restored.Era);
            Assert.IsFalse(restored.EraProgress.antimatterCrafted);
            Assert.AreEqual(state.ShardBalance, restored.ShardBalance);
            Assert.IsTrue(restored.Flush());
            var savedAgain = JsonUtility.FromJson<SaveV2>(File.ReadAllText(sandbox.SaveFile));
            Assert.AreEqual("3", savedAgain.settings.Single(x => x.key == "frontier_era_reached").value);
            var antimatterRecipe = db.recipes.First(recipe => recipe.outputs.Any(item => item.resource.id == GameState.AntimatterId));
            savedAgain.atlas.Add("recipe:" + antimatterRecipe.id);
            File.WriteAllText(sandbox.SaveFile, JsonUtility.ToJson(savedAgain));
            var crafted = new GameState(db, true);
            Assert.IsTrue(crafted.EraProgress.antimatterCrafted);
            Assert.AreEqual(FrontierEra.Interstellar, crafted.Era);
            Assert.AreEqual(state.ShardBalance, crafted.ShardBalance);
        }
    }
}
