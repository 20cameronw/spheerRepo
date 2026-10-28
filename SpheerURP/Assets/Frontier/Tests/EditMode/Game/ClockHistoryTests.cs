using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Frontier.Tests
{
    public sealed class ClockHistoryTests
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
        public void TearDown() { sandbox.Dispose(); }

        GameState Seed()
        {
            var state = new GameState(db, false, clock);
            Assert.IsNotNull(state.DebugPlace(db.FindStructure("auto_harvester"), 0));
            Assert.IsNotNull(state.DebugPlace(db.StructureOfKind(StructureKind.DysonCollector), 1));
            Assert.IsTrue(state.Save());
            return state;
        }

        static double Fuel(GameState state) { return state.StructureAt(state.planet, 1).record.offlineFuelEnergy; }
        static int Raw(GameState state)
        {
            return state.StorageFor(state.StructureAt(state.planet, 0)).slots.Sum(slot => slot.count);
        }

        SaveV2 Read() { return SaveSerializer.Instance.DeserializeV2(File.ReadAllText(sandbox.SaveFile)).Normalize(); }

        GameState LoadRewound()
        {
            LogAssert.Expect(LogType.Warning, new Regex("Frontier save: The device clock is earlier"));
            var state = new GameState(db, true, clock);
            Assert.IsTrue(state.ClockRewindDetected);
            Assert.IsTrue(state.LoadReport.warnings.Contains(UiText.Get("clock.rewind-detected")));
            return state;
        }

        [Test]
        public void SameProcessResumeAccruesOnceAndReportsOutputInItsActualStorage()
        {
            var state = Seed();
            int cargo = state.Count(db.FindResource("wood"));
            clock.Advance(3600);
            state.ResumeOfflineProgress();
            Assert.AreEqual(400, Fuel(state), 0.001);
            Assert.Greater(Raw(state), 0);
            Assert.AreEqual(cargo, state.Count(db.FindResource("wood")));
            Assert.IsTrue(state.ReturnItems.Any(i => i.slot == 0 && i.amount > 0));
            Assert.IsTrue(state.ReturnItems.Any(i => i.slot == 1 && i.resourceId == db.fuel.id && i.amount > 0));
            int raw = Raw(state);
            state.ResumeOfflineProgress();
            Assert.AreEqual(400, Fuel(state), 0.001);
            Assert.AreEqual(raw, Raw(state));
            Assert.IsEmpty(state.ReturnItems);
            var loaded = new GameState(db, true, clock);
            Assert.AreEqual(400, Fuel(loaded), 0.001);
            Assert.AreEqual(raw, Raw(loaded));
        }

        [Test]
        public void RewindAndRepeatedSavesCannotReplayOfflineIntervalsForEitherProducer()
        {
            Seed();
            clock.Advance(3600);
            var first = new GameState(db, true, clock);
            Assert.AreEqual(400, Fuel(first), 0.001);
            Assert.Greater(Raw(first), 0);
            int raw = Raw(first);
            DateTime highWater = clock.UtcNow;
            clock.SetUtc(highWater.AddHours(-2));
            var rewound = LoadRewound();
            Assert.AreEqual(400, Fuel(rewound), 0.001);
            Assert.AreEqual(raw, Raw(rewound));
            Assert.IsTrue(rewound.Save());
            Assert.AreEqual(UtcText.Format(highWater), Read().clock.highWaterUtc);
            Assert.AreEqual(UtcText.Format(clock.UtcNow), Read().clock.lastSavedUtc);
            clock.Advance(1800);
            rewound = LoadRewound();
            Assert.AreEqual(400, Fuel(rewound), 0.001);
            Assert.AreEqual(raw, Raw(rewound));
            Assert.IsTrue(rewound.Save());
            clock.SetUtc(highWater);
            var caughtUp = new GameState(db, true, clock);
            Assert.IsFalse(caughtUp.ClockRewindDetected);
            Assert.AreEqual(400, Fuel(caughtUp), 0.001, "Reaching the watermark must not credit its old interval.");
            clock.Advance(3600);
            var later = new GameState(db, true, clock);
            Assert.AreEqual(800, Fuel(later), 0.001, "Only the new hour after the watermark produces fuel.");
            Assert.Greater(Raw(later), raw);
        }

        [Test]
        public void LegacySaveWithoutWatermarkUsesItsLastSavedTime()
        {
            Seed();
            var save = Read();
            save.clock.highWaterUtc = "";
            File.WriteAllText(sandbox.SaveFile, SaveSerializer.Instance.Serialize(save));
            clock.Advance(3600);
            var loaded = new GameState(db, true, clock);
            Assert.IsFalse(loaded.ClockRewindDetected);
            Assert.AreEqual(400, Fuel(loaded), 0.001);
            Assert.AreEqual(UtcText.Format(clock.UtcNow), Read().clock.highWaterUtc);
        }

        [Test]
        public void CorruptWatermarkProtectsDiskAndCannotAccrueOfflineProductionOrBeRestored()
        {
            Seed();
            var save = Read();
            save.clock.highWaterUtc = "not-a-date";
            string damaged = SaveSerializer.Instance.Serialize(save);
            File.WriteAllText(sandbox.SaveFile, damaged);
            clock.Advance(3600);
            LogAssert.Expect(LogType.Warning, new Regex("Frontier: Saved clock history is unreadable"));
            var loaded = new GameState(db, true, clock);
            Assert.IsTrue(loaded.SaveIsReadOnly);
            Assert.IsTrue(loaded.LoadReport.readOnly);
            Assert.AreEqual(0, Fuel(loaded));
            Assert.AreEqual(0, Raw(loaded));
            Assert.IsFalse(loaded.Save());
            Assert.AreEqual(damaged, File.ReadAllText(sandbox.SaveFile));
            var fresh = new GameState(db, false, clock);
            Assert.IsFalse(fresh.TryRestoreCloudSave(save, out string message));
            Assert.AreEqual(UiText.Get("clock.history-invalid"), message);
        }

        [Test]
        public void RestoreAndUndoRetainTheLocalWatermarkWithoutMutatingTheIncomingSnapshot()
        {
            var state = Seed();
            Assert.IsTrue(state.TryCreateCloudSaveSnapshot(out var old, out _));
            string original = SaveSerializer.Instance.Serialize(old);
            clock.Advance(3600);
            state = new GameState(db, true, clock);
            DateTime highWater = clock.UtcNow;
            Assert.IsTrue(state.TryRestoreCloudSave(old, out _));
            Assert.AreEqual(0, Fuel(state), "Explicit restore restores progression, not another already-observed offline interval.");
            Assert.AreEqual(original, SaveSerializer.Instance.Serialize(old));
            Assert.AreEqual(UtcText.Format(highWater), Read().clock.highWaterUtc);
            Assert.IsTrue(state.TryRestorePreviousLocalSave(out _));
            Assert.AreEqual(400, Fuel(state), 0.001);
            Assert.AreEqual(UtcText.Format(highWater), Read().clock.highWaterUtc);
            clock.SetUtc(highWater.AddHours(-1));
            state = LoadRewound();
            Assert.IsTrue(state.Save());
            Assert.AreEqual(UtcText.Format(highWater), Read().clock.highWaterUtc);
        }
    }
}
