using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Frontier.Tests
{
    /// <summary>
    /// The save format with the real JsonUtility codec: a genuine v1 file (written by the legacy GameState, see Fixtures/) is read,
    /// migrated and written as v2; v2 round trips field for field; damaged, truncated and newer files are handled by the store.
    /// </summary>
    [TestFixture]
    public class SaveFormatTests
    {
        static readonly SaveSerializer Codec = SaveSerializer.Instance;
        string dir;

        [SetUp]
        public void SetUp()
        {
            dir = Path.Combine(Application.temporaryCachePath, "frontier-saveformat-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(dir);
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }

        public static string Fixture(string name)
        {
            return File.ReadAllText(Path.Combine(Application.dataPath, "Frontier", "Tests", "EditMode", "Fixtures", name));
        }

        static MigrationContext Context()
        {
            return new MigrationContext { AppVersion = "test", UniverseSeed = 0x46524F4E54494552UL, GeneratorVersion = 1, NowUtc = new DateTime(2026, 10, 13, 20, 0, 0, DateTimeKind.Utc), SaveId = "fixture" };
        }

        // ---- the real v1 fixtures ---------------------------------------------------------------------------------------

        [Test]
        public void RealV1MidGameFile_ParsesWithTheLegacyClass()
        {
            string json = Fixture("save_v1_midgame.json");
            Assert.AreEqual(0, Codec.ReadSchemaVersion(json), "a v1 file has no schemaVersion");
            var v1 = Codec.DeserializeV1(json);
            Assert.AreEqual(212, v1.xp);
            Assert.AreEqual("glacia", v1.planetId);
            Assert.AreEqual(6, v1.totalWavesCleared);
            Assert.AreEqual(9, v1.itemIds.Count);
            Assert.AreEqual(9, v1.structIds.Count);
            Assert.AreEqual(2, v1.warehouses.Count);
            Assert.AreEqual(18, v1.warehouses[0].slots.Count);
            Assert.AreEqual("scrap", v1.warehouses[0].slots[1].resourceId);
            Assert.AreEqual(50, v1.warehouses[0].slots[1].count);
        }

        [Test]
        public void RealV1MidGameFile_MigratesToV2_AndRoundTripsThroughJson()
        {
            var v1 = Codec.DeserializeV1(Fixture("save_v1_midgame.json"));
            var warnings = new List<string>();
            var v2 = SaveMigrator.FromV1(v1, new FakeWorldMap(), Context(), warnings);
            Assert.AreEqual(0, warnings.Count, string.Join("; ", warnings));

            // what the legacy game had at that moment (see the scenario in the commit message / docs/RUNTIME_MODEL.md)
            Assert.AreEqual(212, v2.player.xp);
            Assert.AreEqual(6, v2.stats.totalWavesCleared);
            Assert.AreEqual("p:0:0:1000:1", v2.location.currentPlanetId);
            Assert.AreEqual(3, v2.worlds.Count);
            Assert.AreEqual(9, v2.worlds[0].structures.Count + v2.worlds[1].structures.Count + v2.worlds[2].structures.Count);
            Assert.AreEqual(3, v2.worlds[0].wavesCleared);
            Assert.AreEqual(1, v2.worlds[1].wavesCleared);
            Assert.AreEqual(2, v2.worlds[2].wavesCleared);

            string json = Codec.Serialize(v2);
            Assert.IsTrue(JsonShape.IsCompleteObject(json));
            Assert.AreEqual(SaveSchema.Current, Codec.ReadSchemaVersion(json));
            var back = Codec.DeserializeV2(json).Normalize();
            Assert.AreEqual(json, Codec.Serialize(back), "serialize(deserialize(x)) == x");
            Assert.AreEqual(v2.worlds[1].storages[0].slots[3].resourceId, back.worlds[1].storages[0].slots[3].resourceId);
            Assert.AreEqual(6, back.worlds[1].storages[0].slots[3].count);
            Assert.AreEqual(v2.player.inventory.Count, back.player.inventory.Count);
            Assert.AreEqual(v2.player.upgrades.Count, back.player.upgrades.Count);
        }

        [Test]
        public void SaveNormalization_DeduplicatesAtlasEntriesAndPreservesLegacyValues()
        {
            var save = new SaveV2();
            save.atlas.Add("wood");
            save.atlas.Add("resource:wood");
            save.atlas.Add("resource:wood");
            save.atlas.Add(null);
            save.Normalize();

            CollectionAssert.AreEqual(new[] { "wood", "resource:wood" }, save.atlas);
        }

        [Test]
        public void SaveRevision_RoundTripsAndOlderV2DefaultsToZero()
        {
            var save = new SaveV2 { saveId = "revisioned", revision = 9007199254740991L };
            string json = Codec.Serialize(save);
            Assert.AreEqual(save.revision, Codec.DeserializeV2(json).Normalize().revision);

            var older = Codec.DeserializeV2("{\"schemaVersion\":" + SaveSchema.Current + ",\"saveId\":\"older-v2\"}").Normalize();
            Assert.AreEqual(0L, older.revision);

            older.revision = -1;
            Assert.AreEqual(0L, older.Normalize().revision);
        }

        [Test]
        public void RealV1FreshFile_MigratesToTheHomeWorld()
        {
            var v1 = Codec.DeserializeV1(Fixture("save_v1_fresh.json"));
            var v2 = SaveMigrator.FromV1(v1, new FakeWorldMap(), Context(), null);
            Assert.AreEqual("p:0:0:1000:0", v2.location.currentPlanetId);
            Assert.AreEqual(3, v2.player.inventory.Count);
            Assert.AreEqual(0, v2.player.xp);
            Assert.AreEqual(1, v2.worlds.Count);
            Assert.AreEqual(0, v2.worlds[0].structures.Count);
        }

        [Test]
        public void StoreLoad_MigratesTheRealV1FileAndKeepsAnExactLegacyCopy()
        {
            string original = Fixture("save_v1_midgame.json");
            var store = new SaveStore(dir, Codec);
            File.WriteAllText(store.PrimaryPath, original);
            var result = store.Load((legacy, warnings) => SaveMigrator.FromV1(legacy, new FakeWorldMap(), Context(), warnings));
            Assert.AreEqual(1, result.report.migratedFrom);
            Assert.AreEqual(SaveSource.Primary, result.report.source);
            Assert.AreEqual(212, result.save.player.xp);
            Assert.AreEqual(original, File.ReadAllText(store.LegacyBackupPath));
            Assert.IsTrue(store.Write(result.save), store.LastWriteError);
            var again = store.Load((legacy, warnings) => { Assert.Fail("a v2 file must not be migrated again"); return null; });
            Assert.AreEqual(0, again.report.migratedFrom);
            Assert.AreEqual(212, again.save.player.xp);
            Assert.AreEqual(original, File.ReadAllText(store.BackupPath), "the v1 file is also the rolling backup right after migration");
        }

        // ---- v2 round trip --------------------------------------------------------------------------------------------

        static SaveV2 FullSave()
        {
            var s = new SaveV2
            {
                saveId = "0123456789abcdef",
                createdWith = new CreatedWithInfo { appVersion = "0.9.1", generatorVersion = 1, universeSeed = SeedText.Format(0x46524F4E54494552UL) }
            };
            s.player.xp = 1234;
            s.player.upgrades.Add(new IdLevel { id = "pack", level = 3 });
            s.player.upgrades.Add(new IdLevel { id = "axe", level = 1 });
            s.player.inventory.Add(new StackRecord { resourceId = "wood", count = 70 });
            s.player.inventory.Add(new StackRecord { resourceId = "fuel", count = 9 });
            s.location.currentPlanetId = "p:3:0:1001:2";
            var d = new PlanetDelta { planetId = "p:0:0:1000:0", wavesCleared = 4, visits = 3, discovered = true, firstVisitUtc = "2026-10-13T12:00:00.0000000Z", lastVisitUtc = "2026-10-13T14:30:15.5000000Z" };
            d.structures.Add(new StructureRecord { defId = "lumber", slot = 0, level = 2, buildEndUtc = "2026-10-13T15:00:00.0000000Z", pendingUpgradeLevel = 3 });
            d.structures.Add(new StructureRecord { defId = "warehouse", slot = 5, level = 5 });
            var storage = new StorageRecord { structureSlot = 5 };
            for (int i = 0; i < 54; i++) storage.slots.Add(new StackRecord());
            storage.slots[7] = new StackRecord { resourceId = "stone", count = 64 };
            d.storages.Add(storage);
            d.clearedObstacles.Add(3);
            d.clearedObstacles.Add(11);
            s.worlds.Add(d);
            s.stats = new PlayerStats { maxDistanceFromHomeLy = 1486.123456789, worldsLanded = 4, totalWavesCleared = 9, playSeconds = 36123.456 };
            s.clock.lastSavedUtc = "2026-10-13T20:15:00.0000000Z";
            s.wallet.Add(new WalletEntry { currency = "shards", amount = 12 });
            s.builders.Add(new BuilderSlot { slotId = "builder-1", target = "p:0:0:1000:0/lumber/0", busyUntilUtc = "2026-10-13T21:00:00.0000000Z" });
            s.atlas.Add("wood");
            s.settings.Add(new SettingEntry { key = "muted", value = "1" });
            return s;
        }

        [Test]
        public void V2_RoundTripsEveryFieldIncludingTheReservedSections()
        {
            var s = FullSave();
            string json = Codec.Serialize(s);
            var b = Codec.DeserializeV2(json).Normalize();
            Assert.AreEqual(SaveSchema.Current, b.schemaVersion);
            Assert.AreEqual("0123456789abcdef", b.saveId);
            Assert.AreEqual("0.9.1", b.createdWith.appVersion);
            Assert.AreEqual(1, b.createdWith.generatorVersion);
            Assert.AreEqual("46524F4E54494552", b.createdWith.universeSeed);
            Assert.AreEqual(1234, b.player.xp);
            Assert.AreEqual("pack", b.player.upgrades[0].id);
            Assert.AreEqual(3, b.player.upgrades[0].level);
            Assert.AreEqual("wood", b.player.inventory[0].resourceId);
            Assert.AreEqual(70, b.player.inventory[0].count);
            Assert.AreEqual("p:3:0:1001:2", b.location.currentPlanetId);
            var d = b.worlds[0];
            Assert.AreEqual("p:0:0:1000:0", d.planetId);
            Assert.AreEqual(4, d.wavesCleared);
            Assert.AreEqual(3, d.visits);
            Assert.IsTrue(d.discovered);
            Assert.AreEqual("2026-10-13T14:30:15.5000000Z", d.lastVisitUtc);
            Assert.AreEqual(2, d.structures[0].level);
            Assert.AreEqual("2026-10-13T15:00:00.0000000Z", d.structures[0].buildEndUtc);
            Assert.AreEqual(3, d.structures[0].pendingUpgradeLevel);
            Assert.AreEqual(54, d.storages[0].slots.Count);
            Assert.AreEqual("stone", d.storages[0].slots[7].resourceId);
            Assert.AreEqual(64, d.storages[0].slots[7].count);
            Assert.AreEqual(new List<int> { 3, 11 }, d.clearedObstacles);
            Assert.AreEqual(1486.123456789, b.stats.maxDistanceFromHomeLy, 1e-6);
            Assert.AreEqual(4, b.stats.worldsLanded);
            Assert.AreEqual(9, b.stats.totalWavesCleared);
            Assert.AreEqual(36123.456, b.stats.playSeconds, 1e-6);
            Assert.AreEqual("2026-10-13T20:15:00.0000000Z", b.clock.lastSavedUtc);
            Assert.AreEqual("shards", b.wallet[0].currency);
            Assert.AreEqual(12, b.wallet[0].amount);
            Assert.AreEqual("builder-1", b.builders[0].slotId);
            Assert.AreEqual("wood", b.atlas[0]);
            Assert.AreEqual("muted", b.settings[0].key);
            Assert.AreEqual(json, Codec.Serialize(b));
        }

        [Test]
        public void V2_FileWithMissingSections_GetsDefaults_AndUnknownFieldsAreIgnored()
        {
            // what an older v2 writer (before a field existed) or a hand edit could produce
            string json = "{\"schemaVersion\":2,\"saveId\":\"x\",\"player\":{\"xp\":5},\"somethingNew\":{\"a\":[1,2,3]},\"location\":{\"currentPlanetId\":\"p:0:0:1000:0\"}}";
            var s = Codec.DeserializeV2(json).Normalize();
            Assert.AreEqual(5, s.player.xp);
            Assert.IsNotNull(s.player.inventory);
            Assert.IsNotNull(s.worlds);
            Assert.IsNotNull(s.stats);
            Assert.IsNotNull(s.wallet);
            Assert.IsNotNull(s.atlas);
            Assert.AreEqual("", s.clock.lastSavedUtc);
            Assert.AreEqual("p:0:0:1000:0", s.location.currentPlanetId);
        }

        [Test]
        public void OnlyChangedWorlds_AreWritten()
        {
            var store = new WorldStore();
            store.Place("p:0:0:1000:0", "lumber", 0);
            store.Get("p:7:7:3:1");                         // looked at, never changed
            var save = new SaveV2();
            save.worlds = store.Export();
            string json = Codec.Serialize(save);
            Assert.AreEqual(1, Codec.DeserializeV2(json).worlds.Count);
            StringAssert.DoesNotContain("p:7:7:3:1", json);
        }

        // ---- the store with the real codec ----------------------------------------------------------------------------

        [Test]
        public void TruncatedPrimary_IsDetectedAndTheBackupRecovers()
        {
            var store = new SaveStore(dir, Codec);
            var a = FullSave(); a.player.xp = 100;
            var b = FullSave(); b.player.xp = 200;
            Assert.IsTrue(store.Write(a), store.LastWriteError);
            Assert.IsTrue(store.Write(b), store.LastWriteError);
            string full = File.ReadAllText(store.PrimaryPath);
            foreach (int cut in new[] { 0, 1, full.Length / 4, full.Length / 2, full.Length - 2, full.Length - 1 })
            {
                File.WriteAllText(store.PrimaryPath, full.Substring(0, cut));
                var r = new SaveStore(dir, Codec).Load(null);
                Assert.AreEqual(SaveSource.Backup, r.report.source, "cut at " + cut);
                Assert.IsTrue(r.report.recovered);
                Assert.AreEqual(100, r.save.player.xp, "the previous good save");
            }
        }

        [Test]
        public void GarbageThatIsStillJson_FallsBackToTheBackup()
        {
            var store = new SaveStore(dir, Codec);
            Assert.IsTrue(store.Write(FullSave()));
            Assert.IsTrue(store.Write(FullSave()));
            File.WriteAllText(store.PrimaryPath, "{\"schemaVersion\":\"two\",\"player\":[1,2,3]}");
            var r = new SaveStore(dir, Codec).Load(null);
            Assert.AreEqual(SaveSource.Backup, r.report.source, r.report.error);
        }

        [Test]
        public void NewerSchemaFile_WithUnknownFields_LoadsReadOnlyAndStaysUntouched()
        {
            string json = "{\"schemaVersion\":3,\"saveId\":\"future\",\"player\":{\"xp\":42,\"newThing\":{\"x\":[1]}},\"location\":{\"currentPlanetId\":\"p:0:0:1000:0\"},\"futureSection\":{\"k\":\"v\"}}";
            var store = new SaveStore(dir, Codec);
            File.WriteAllText(store.PrimaryPath, json);
            var r = store.Load(null);
            Assert.IsTrue(r.report.readOnly);
            Assert.AreEqual(42, r.save.player.xp);
            Assert.IsFalse(store.Write(r.save));
            Assert.AreEqual(json, File.ReadAllText(store.PrimaryPath), "a newer save is never overwritten");
        }

        [Test]
        public void WrittenFile_IsCompactJson_WithSchemaVersionFirst()
        {
            var store = new SaveStore(dir, Codec);
            Assert.IsTrue(store.Write(FullSave()));
            string json = File.ReadAllText(store.PrimaryPath);
            StringAssert.StartsWith("{\"schemaVersion\":2,", json);
            StringAssert.DoesNotContain("\n", json);
        }
    }
}
