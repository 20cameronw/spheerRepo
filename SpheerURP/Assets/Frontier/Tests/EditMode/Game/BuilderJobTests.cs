using System.Linq;
using NUnit.Framework;

namespace Frontier.Tests
{
    [TestFixture]
    public class BuilderJobTests
    {
        SaveSandbox sandbox;
        GameDatabase db;
        FakeClock clock;

        [SetUp]
        public void SetUp()
        {
            sandbox = new SaveSandbox();
            db = GameDatabase.Load();
            clock = new FakeClock();
        }

        [TearDown]
        public void TearDown()
        {
            sandbox.Dispose();
        }

        [Test]
        public void ConstructionOccupiesCrewAndActivatesAfterPersistedTimer()
        {
            if (!db.UsesImportedEconomy) Assert.Ignore("Imported build-time content is unavailable");
            var state = new GameState(db, false, clock);
            GiveAllResources(state);
            StructureSO refinery = state.db.StructureOfKind(StructureKind.Refinery);
            int originalDuration = refinery.Row(1).buildSeconds;
            refinery.Row(1).buildSeconds = 5;
            try
            {
                int duration = state.StructureJobSeconds(refinery, 1);
                Assert.Greater(duration, 0, "the timer uses the authored catalog field");
                Assert.IsTrue(state.Place(refinery, 0));
                var placed = state.StructureAt(state.planet, 0);
                Assert.IsTrue(state.IsUnderConstruction(placed));
                Assert.IsFalse(state.IsOperational(placed));
                Assert.IsNull(state.StructureOfKind(state.planet, refinery.kind));
                Assert.AreEqual(1, state.BusyBuilderCount);
                Assert.AreEqual(0, state.AvailableBuilderCount);

                var second = state.db.structures.First(candidate => candidate != null && candidate != refinery &&
                    candidate.levels != null && candidate.levels.Count > 1 && candidate.levels[1].buildSeconds > 0 &&
                    candidate.levels[1].requiresReachTier <= state.ReachTier && candidate.levels[1].requiresLabLevel == 0 &&
                    string.IsNullOrEmpty(candidate.unlockResearch));
                var secondPlaced = state.DebugPlace(second, 1);
                Assert.IsNotNull(secondPlaced);
                Assert.AreEqual("All builder crews are busy", state.StructureUpgradeLockReason(secondPlaced),
                    "the sole builder cannot start an upgrade while constructing another structure");

                Assert.IsTrue(state.Save());
                var loaded = new GameState(db, true, clock);
                placed = loaded.StructureAt(loaded.planet, 0);
                Assert.IsTrue(loaded.IsUnderConstruction(placed), "the remaining job is restored from the save");

                clock.Advance(duration);
                loaded.Tick();
                Assert.IsFalse(loaded.IsUnderConstruction(placed));
                Assert.IsTrue(loaded.IsOperational(placed));
                Assert.AreEqual(0, loaded.BusyBuilderCount);
                Assert.AreSame(placed, loaded.StructureOfKind(loaded.planet, refinery.kind));
            }
            finally
            {
                refinery.Row(1).buildSeconds = originalDuration;
            }
        }

        [Test]
        public void UpgradeKeepsCurrentLevelUntilTimerCompletes()
        {
            if (!db.UsesImportedEconomy) Assert.Ignore("Imported upgrade-time content is unavailable");
            var state = new GameState(db, false, clock);
            GiveAllResources(state);
            StructureSO definition = db.structures.FirstOrDefault(candidate => candidate != null &&
                candidate.Implemented && candidate.levels != null && candidate.levels.Count > 1 &&
                candidate.levels[1].buildSeconds > 0 && candidate.levels[1].requiresReachTier <= state.ReachTier &&
                candidate.levels[1].requiresLabLevel == 0 && string.IsNullOrEmpty(candidate.unlockResearch));
            Assert.IsNotNull(definition, "catalog contains an available timed upgrade");
            var placed = state.DebugPlace(definition, 0);
            Assert.IsNotNull(placed);
            Assert.IsTrue(state.CanUpgradeStructure(placed), state.StructureUpgradeLockReason(placed));
            int duration = state.StructureJobSeconds(definition, 2);

            Assert.IsTrue(state.UpgradeStructure(placed));
            Assert.AreEqual(1, placed.level, "upgrade benefits do not apply until completion");
            Assert.IsTrue(state.IsUpgradeInProgress(placed));
            Assert.IsTrue(state.IsOperational(placed), "the existing structure remains usable during an upgrade");
            Assert.AreEqual(2, placed.record.pendingUpgradeLevel);

            clock.Advance(duration);
            state.Tick();
            Assert.AreEqual(2, placed.level);
            Assert.IsFalse(state.IsUpgradeInProgress(placed));
            Assert.AreEqual(0, placed.record.pendingUpgradeLevel);
            Assert.AreEqual(0, state.BusyBuilderCount);
        }

        [Test]
        public void InvalidSavedBuilderTimerIsClearedWithWarning()
        {
            var state = new GameState(db, false, clock);
            var placed = state.DebugPlace(db.StructureOfKind(StructureKind.Refinery), 0);
            placed.record.buildEndUtc = "not-a-utc-timestamp";
            Assert.IsTrue(state.Save());

            var loaded = new GameState(db, true, clock);
            var restored = loaded.StructureAt(loaded.planet, 0);
            Assert.IsFalse(loaded.IsUnderConstruction(restored));
            Assert.AreEqual(0, loaded.BusyBuilderCount);
            CollectionAssert.Contains(loaded.LoadReport.warnings,
                "cleared an invalid builder timer for '" + restored.def.id + "' on " + loaded.planet.Id);
        }

        [Test]
        public void LegacyContentBuildsRemainImmediate()
        {
            var legacy = GameDatabase.LoadLegacy();
            var state = new GameState(legacy, false, clock);
            GiveAllResources(state);
            var definition = legacy.StructureOfKind(StructureKind.Refinery);
            Assert.AreEqual(0, state.StructureJobSeconds(definition, 1));
            Assert.IsTrue(state.Place(definition, 0));
            var placed = state.StructureAt(state.planet, 0);
            Assert.IsTrue(state.IsOperational(placed));
            Assert.AreEqual(0, state.BusyBuilderCount);
        }

        static void GiveAllResources(GameState state)
        {
            foreach (var resource in state.db.resources)
                if (resource != null) state.backpack.Set(resource.id, 10000000);
        }

    }
}
