using System.IO;
using System.Linq;
using Frontier.Economy;
using NUnit.Framework;

namespace Frontier.Tests
{
    public class AscensionProgressionTests
    {
        SaveSandbox sandbox;
        GameDatabase db;
        AscensionRulesSO rules;
        bool previousEnabled;
        [SetUp]
        public void SetUp()
        {
            sandbox = new SaveSandbox();
            db = GameDatabase.Load();
            rules = UnityEngine.Resources.Load<AscensionRulesSO>("Frontier/AscensionRules");
            previousEnabled = rules.enableAscension;
        }
        [TearDown] public void TearDown() { rules.enableAscension = previousEnabled; sandbox.Dispose(); }
        SaveV2 Read() { return UnityEngine.JsonUtility.FromJson<SaveV2>(File.ReadAllText(sandbox.SaveFile)); }
        void Write(SaveV2 save) { File.WriteAllText(sandbox.SaveFile, UnityEngine.JsonUtility.ToJson(save)); }

        GameState Ready()
        {
            rules.enableAscension = true;
            var state = new GameState(db, false);
            state.DebugPlace(db.StructureOfKind(StructureKind.Terraformer), 0, 3);
            state.RecordHarvestRunCompleted();
            Assert.AreEqual(ObjectiveClaimResult.Saved, state.ClaimObjective("first_harvest", out _));
            state.DebugSetUpgrade(db.FindUpgrade("pack"), 1);
            state.AddXp(100);
            state.Save();
            var save = Read();
            save.player.researched.Add("ascension_protocol");
            save.settings.Add(new SettingEntry { key = "future_cosmetic", value = "neon_ship" });
            Write(save);
            return new GameState(db, true);
        }

        [Test]
        public void Ascension_RequiresResearchAndACurrentCompletedProject_NotOnlyAnEraMarker()
        {
            rules.enableAscension = true;
            var state = new GameState(db, false);
            Assert.IsFalse(state.TryAscend(out var message));
            StringAssert.Contains("Research", message);
            state = Ready();
            var project = state.structures.Single();
            state.world.Remove(state.planet.Id, project.record);
            state.Notify();
            Assert.AreEqual(FrontierEra.Ascension, state.Era);
            Assert.IsFalse(state.TryAscend(out message));
            StringAssert.Contains("megaproject", message);
            Assert.AreEqual(0, state.AscensionRank);
        }

        [Test]
        public void UnapprovedAscension_IsDisabledInTheAuthoredBuild_AndCannotResetProgress()
        {
            Assert.IsFalse(previousEnabled, "The draft bonus is not an approved gameplay setting.");
            var state = new GameState(db, false);
            Assert.IsFalse(state.AscensionAvailable);
            Assert.AreEqual(1f, state.LegacyHarvestMultiplier);
            Assert.IsFalse(state.TryAscend(out var message));
            StringAssert.Contains("not enabled", message);
            Assert.AreEqual(0, state.AscensionRank);
        }

        [Test]
        public void Ascension_RetainsKnowledgeCurrencyAndClaims_ResetsWorldProgress_AndReloadsTheNewSeed()
        {
            var state = Ready();
            int shards = state.ShardBalance, atlasCount = state.AtlasDiscoveredCount("resource");
            ulong oldSeed = state.universe.Seed;
            string oldId = Read().saveId;
            long previousRevision = Read().revision;
            Assert.IsTrue(state.TryAscend(out var message), message);
            Assert.AreEqual(1, state.AscensionRank);
            Assert.AreEqual(1, state.LegacyPoints);
            Assert.AreEqual(1.05f, state.LegacyHarvestMultiplier, 0.00001f);
            Assert.AreEqual(1.1, state.FrontierScore.multiplier, 0.00001);
            Assert.AreNotEqual(oldSeed, state.universe.Seed);
            Assert.AreEqual(state.universe.Home.Id, state.planet.Id);
            Assert.AreEqual(0, state.structures.Count);
            Assert.AreEqual(0, state.xp);
            Assert.AreEqual(0, state.Level(db.FindUpgrade("pack")));
            Assert.AreEqual(0, state.stats.harvestRunsCompleted);
            Assert.IsFalse(state.FirstSessionGuideAvailable, "Ascension must not restart first-time onboarding.");
            Assert.AreEqual(shards, state.ShardBalance);
            Assert.AreEqual(atlasCount, state.AtlasDiscoveredCount("resource"));
            Assert.IsTrue(state.IsResearched("ascension_protocol"));
            Assert.IsTrue(state.ObjectiveClaimed("first_harvest"));
            Assert.AreEqual(ObjectiveClaimResult.Rejected, state.ClaimObjective("first_harvest", out _));
            var persisted = Read();
            Assert.AreEqual(oldId, persisted.saveId, "Ascension retains account/save identity.");
            Assert.Greater(persisted.revision, previousRevision);
            Assert.AreEqual("neon_ship", persisted.settings.Single(x => x.key == "future_cosmetic").value);
            Assert.AreEqual(1, persisted.worlds.Count);
            Assert.AreEqual(0, persisted.builders.Count);
            foreach (var item in db.startingItems)
                Assert.AreEqual(item.amount, state.Count(item.resource), "Only starter cargo is carried into the new universe.");
            var restored = new GameState(db, true);
            Assert.AreEqual(state.universe.Seed, restored.universe.Seed);
            Assert.AreEqual(1, restored.AscensionRank);
            Assert.AreEqual(shards, restored.ShardBalance);
            Assert.IsFalse(restored.TryAscend(out _), "Retained research cannot bypass the new-universe project requirement.");
            restored.ResetProgress();
            Assert.AreEqual(0, restored.AscensionRank);
            Assert.AreEqual(UniverseService.DefaultSeed, restored.universe.Seed);
            Assert.AreEqual(1f, restored.LegacyHarvestMultiplier);
        }

        [Test]
        public void RetainedRecoveryCopy_CanUndoAscension_AndRestoreThePreviousUniverseAndRank()
        {
            var state = Ready();
            ulong seed = state.universe.Seed;
            int shards = state.ShardBalance;
            Assert.IsTrue(state.TryAscend(out _));
            Assert.IsTrue(state.HasCloudRestoreBackup);
            Assert.IsTrue(state.TryRestorePreviousLocalSave(out var message), message);
            Assert.AreEqual(seed, state.universe.Seed);
            Assert.AreEqual(0, state.AscensionRank);
            Assert.AreEqual(3, state.structures.Single().level);
            Assert.AreEqual(shards, state.ShardBalance);
        }

        [Test]
        public void FailedRecoveryCopy_DoesNotMutateTheLiveUniverseOrConsumeLegacyProgress()
        {
            var state = Ready();
            ulong seed = state.universe.Seed;
            int shards = state.ShardBalance;
            string blocked = state.saves.Store.CloudRestoreBackupPath + ".tmp";
            Directory.CreateDirectory(blocked);
            Assert.IsFalse(state.TryAscend(out var message));
            StringAssert.Contains("not restored", message);
            Assert.AreEqual(seed, state.universe.Seed);
            Assert.AreEqual(0, state.AscensionRank);
            Assert.AreEqual(shards, state.ShardBalance);
            Assert.AreEqual(1, state.structures.Count);
            Directory.Delete(blocked);
            Assert.IsTrue(state.TryAscend(out message), message);
            Assert.AreEqual(1, state.AscensionRank);
        }

        [Test]
        public void ReadOnly_RejectsAscension_WithoutWritingOrChangingState()
        {
            var state = Ready();
            string before = File.ReadAllText(sandbox.SaveFile);
            state.saves.Store.MarkReadOnly("Future save");
            Assert.IsFalse(state.TryAscend(out var message));
            StringAssert.Contains("read-only", message);
            Assert.AreEqual(0, state.AscensionRank);
            Assert.AreEqual(before, File.ReadAllText(sandbox.SaveFile));
        }

        [Test]
        public void CustomSeed_ReloadsGeneratedWorldExactly_AndRejectsInvalidRestoreSeeds()
        {
            var state = Ready();
            Assert.IsTrue(state.TryAscend(out _));
            var destination = state.universe.FindGenerated(p => true, 1, 0, 10000);
            Assert.IsNotNull(destination);
            state.DebugTeleport(destination);
            state.Save();
            var restored = new GameState(db, true);
            Assert.AreEqual(state.universe.Seed, restored.universe.Seed);
            Assert.AreEqual(destination.Id, restored.planet.Id);
            Assert.AreEqual(destination.Seed, restored.planet.Seed);
            Assert.AreEqual(destination.Primary, restored.planet.Primary);
            var invalid = Read();
            invalid.createdWith.universeSeed = "not-a-seed";
            Assert.IsFalse(restored.CanRestoreCloudSaveSnapshot(invalid, out var message));
            StringAssert.Contains("seed", message);
        }
    }
}
