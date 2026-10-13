using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace Frontier.Tests
{
    /// <summary>
    /// The save file protocol (temp file + verify + replace, rolling backup, recovery order, read-only future schemas, legacy backup),
    /// exercised with real files and a tiny fake codec so it runs without Unity. The real JsonUtility codec is covered by the EditMode
    /// save tests.
    /// </summary>
    [TestFixture]
    public class SaveStoreTests
    {
        /// <summary>A minimal flat JSON object with the few fields the store verifies.</summary>
        sealed class FakeCodec : ISaveCodec
        {
            public bool CorruptOnWrite;

            public string Serialize(SaveV2 s)
            {
                string text = "{\"schemaVersion\":" + s.schemaVersion + ",\"saveId\":\"" + (CorruptOnWrite ? "garbled" : s.saveId) + "\",\"xp\":" + s.player.xp +
                              ",\"worlds\":" + s.worlds.Count + ",\"inventory\":" + s.player.inventory.Count + ",\"planetId\":\"" + s.location.currentPlanetId + "\"}";
                return text;
            }

            static bool TryNumber(string text, string key, out int value)
            {
                value = 0;
                int i = text.IndexOf("\"" + key + "\":", StringComparison.Ordinal);
                if (i < 0) return false;
                i += key.Length + 3;
                int j = i;
                while (j < text.Length && (char.IsDigit(text[j]) || text[j] == '-')) j++;
                return int.TryParse(text.Substring(i, j - i), out value);
            }

            static string Text(string text, string key)
            {
                int i = text.IndexOf("\"" + key + "\":\"", StringComparison.Ordinal);
                if (i < 0) return "";
                i += key.Length + 4;
                int j = text.IndexOf('"', i);
                return j < 0 ? "" : text.Substring(i, j - i);
            }

            public SaveV2 DeserializeV2(string text)
            {
                int version, xp, worlds, inventory;
                if (!TryNumber(text, "schemaVersion", out version)) throw new FormatException("no schemaVersion");
                TryNumber(text, "xp", out xp);
                TryNumber(text, "worlds", out worlds);
                TryNumber(text, "inventory", out inventory);
                var s = new SaveV2 { schemaVersion = version, saveId = Text(text, "saveId") };
                s.player.xp = xp;
                s.location.currentPlanetId = Text(text, "planetId");
                for (int i = 0; i < worlds; i++) s.worlds.Add(new PlanetDelta { planetId = "p:" + i });
                for (int i = 0; i < inventory; i++) s.player.inventory.Add(new StackRecord { resourceId = "r" + i, count = 1 });
                return s;
            }

            public SaveDataV1 DeserializeV1(string text)
            {
                int xp;
                TryNumber(text, "xp", out xp);
                return new SaveDataV1 { xp = xp, planetId = Text(text, "planetId") };
            }

            public int ReadSchemaVersion(string text)
            {
                int v;
                return TryNumber(text, "schemaVersion", out v) ? v : 0;
            }
        }

        string dir;
        FakeCodec codec;
        SaveStore store;
        int migrated;

        [SetUp]
        public void SetUp()
        {
            dir = Path.Combine(Path.GetTempPath(), "frontier-store-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(dir);
            codec = new FakeCodec();
            store = new SaveStore(dir, codec);
            migrated = 0;
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }

        SaveMigrationFunc Migrate
        {
            get
            {
                return (legacy, warnings) =>
                {
                    migrated++;
                    var s = new SaveV2 { saveId = "migrated" };
                    s.player.xp = legacy.xp;
                    s.location.currentPlanetId = legacy.planetId;
                    return s;
                };
            }
        }

        static SaveV2 Save(int xp, string id = "save-1", int worlds = 0)
        {
            var s = new SaveV2 { saveId = id };
            s.player.xp = xp;
            for (int i = 0; i < worlds; i++) s.worlds.Add(new PlanetDelta { planetId = "p:" + i });
            return s;
        }

        void Fresh() { store = new SaveStore(dir, codec); }

        [Test]
        public void NothingOnDisk_StartsANewGame()
        {
            var r = store.Load(Migrate);
            Assert.IsNull(r.save);
            Assert.AreEqual(SaveSource.NewGame, r.report.source);
            Assert.IsFalse(r.report.recovered);
            Assert.AreEqual("", r.report.error);
        }

        [Test]
        public void WriteThenLoad_RoundTripsFromThePrimaryFile()
        {
            Assert.IsTrue(store.Write(Save(77, worlds: 2)), store.LastWriteError);
            Assert.IsTrue(File.Exists(store.PrimaryPath));
            Assert.IsFalse(File.Exists(store.TempPath), "the temp file is gone after a successful write");
            var r = store.Load(Migrate);
            Assert.AreEqual(SaveSource.Primary, r.report.source);
            Assert.AreEqual(77, r.save.player.xp);
            Assert.AreEqual(2, r.save.worlds.Count);
            Assert.IsFalse(r.report.recovered);
            Assert.AreEqual(0, r.report.migratedFrom);
            Assert.AreEqual(SaveSchema.Current, r.report.schemaVersion);
            Assert.AreEqual(0, migrated);
        }

        [Test]
        public void EveryWrite_KeepsThePreviousGoodFileAsTheBackup()
        {
            Assert.IsFalse(File.Exists(store.BackupPath), "the very first write has nothing to back up");
            store.Write(Save(1));
            Assert.IsFalse(File.Exists(store.BackupPath));
            string first = File.ReadAllText(store.PrimaryPath);
            store.Write(Save(2));
            Assert.AreEqual(first, File.ReadAllText(store.BackupPath), "the backup is the previous primary file");
            string second = File.ReadAllText(store.PrimaryPath);
            store.Write(Save(3));
            Assert.AreEqual(second, File.ReadAllText(store.BackupPath));
            Assert.AreEqual(3, store.Load(Migrate).save.player.xp);
        }

        [Test]
        public void CorruptPrimary_RecoversFromTheBackup()
        {
            store.Write(Save(10));
            store.Write(Save(20));            // primary 20, backup 10
            File.WriteAllText(store.PrimaryPath, "{\"schemaVersion\":2,\"saveId\":\"sav");   // truncated mid-write
            var r = store.Load(Migrate);
            Assert.AreEqual(SaveSource.Backup, r.report.source);
            Assert.IsTrue(r.report.recovered);
            Assert.AreEqual(10, r.save.player.xp, "the previous good state");
            StringAssert.Contains("frontier_save.json", r.report.error);
        }

        [Test]
        public void EmptyPrimary_RecoversFromTheBackup()
        {
            store.Write(Save(10));
            store.Write(Save(20));
            File.WriteAllText(store.PrimaryPath, "");
            var r = store.Load(Migrate);
            Assert.AreEqual(SaveSource.Backup, r.report.source);
            Assert.AreEqual(10, r.save.player.xp);
        }

        [Test]
        public void MissingPrimary_WithABackup_IsARecovery()
        {
            store.Write(Save(10));
            store.Write(Save(20));
            File.Delete(store.PrimaryPath);
            var r = store.Load(Migrate);
            Assert.AreEqual(SaveSource.Backup, r.report.source);
            Assert.IsTrue(r.report.recovered);
        }

        [Test]
        public void LeftoverTempFile_IsUsedWhenPrimaryAndBackupAreGone()
        {
            File.WriteAllText(store.TempPath, codec.Serialize(Save(55)));   // a fully written temp file from an interrupted replace
            var r = store.Load(Migrate);
            Assert.AreEqual(SaveSource.TempRecovery, r.report.source);
            Assert.IsTrue(r.report.recovered);
            Assert.AreEqual(55, r.save.player.xp);
        }

        [Test]
        public void HalfWrittenTempFile_IsIgnored()
        {
            File.WriteAllText(store.TempPath, "{\"schemaVersion\":2,\"saveId\":\"x\",\"xp\":5");
            var r = store.Load(Migrate);
            Assert.IsNull(r.save);
            Assert.AreEqual(SaveSource.NewGame, r.report.source);
        }

        [Test]
        public void ALeftoverTempFile_DoesNotShadowAGoodPrimary_AndTheNextWriteReplacesIt()
        {
            store.Write(Save(10));
            File.WriteAllText(store.TempPath, codec.Serialize(Save(99)));
            Assert.AreEqual(10, store.Load(Migrate).save.player.xp, "the primary wins over a stale temp file");
            Assert.IsTrue(store.Write(Save(11)));
            Assert.IsFalse(File.Exists(store.TempPath));
            Assert.AreEqual(11, store.Load(Migrate).save.player.xp);
        }

        [Test]
        public void EverythingCorrupt_StartsANewGameAndReportsWhy()
        {
            File.WriteAllText(store.PrimaryPath, "not json at all");
            File.WriteAllText(store.BackupPath, "[1,2,3]");
            File.WriteAllText(store.TempPath, "{");
            var r = store.Load(Migrate);
            Assert.IsNull(r.save);
            Assert.AreEqual(SaveSource.NewGame, r.report.source);
            Assert.IsNotEmpty(r.report.error);
        }

        [Test]
        public void CorruptPrimary_IsNeverCopiedOverTheLastGoodBackup()
        {
            store.Write(Save(10));
            store.Write(Save(20));            // primary 20, backup 10
            File.WriteAllText(store.PrimaryPath, "garbage");
            Fresh();
            Assert.IsTrue(store.Write(Save(30)));
            Assert.AreEqual(10, JsonOf(store.BackupPath), "a corrupt primary must never replace the last good backup");
            Assert.AreEqual(30, store.Load(Migrate).save.player.xp);
        }

        int JsonOf(string path) { return codec.DeserializeV2(File.ReadAllText(path)).player.xp; }

        [Test]
        public void NewerSchema_IsLoadedReadOnly_AndNeverOverwritten()
        {
            string future = "{\"schemaVersion\":" + (SaveSchema.Current + 1) + ",\"saveId\":\"from-the-future\",\"xp\":321,\"worlds\":0,\"inventory\":0,\"planetId\":\"p:9\"}";
            File.WriteAllText(store.PrimaryPath, future);
            var r = store.Load(Migrate);
            Assert.AreEqual(SaveSource.Primary, r.report.source);
            Assert.IsTrue(r.report.readOnly);
            Assert.IsTrue(store.ReadOnly);
            Assert.AreEqual(321, r.save.player.xp);
            StringAssert.Contains("newer", r.report.error);
            Assert.AreEqual(SaveSchema.Current + 1, r.report.schemaVersion);

            Assert.IsFalse(store.Write(Save(1)), "writes are refused");
            StringAssert.Contains("read-only", store.LastWriteError);
            Assert.AreEqual(future, File.ReadAllText(store.PrimaryPath), "the file is untouched");
            Assert.IsFalse(File.Exists(store.BackupPath));
            Assert.IsFalse(File.Exists(store.TempPath));
        }

        [Test]
        public void LegacyV1File_IsMigrated_AfterBeingCopiedToTheLegacyBackup()
        {
            string legacy = "{\"xp\":212,\"planetId\":\"glacia\",\"itemIds\":[],\"itemCounts\":[]}";
            File.WriteAllText(store.PrimaryPath, legacy);
            var r = store.Load(Migrate);
            Assert.AreEqual(1, migrated);
            Assert.AreEqual(1, r.report.migratedFrom);
            Assert.AreEqual(SaveSource.Primary, r.report.source);
            Assert.AreEqual(212, r.save.player.xp);
            Assert.AreEqual("glacia", r.save.location.currentPlanetId);
            Assert.IsTrue(File.Exists(store.LegacyBackupPath));
            Assert.AreEqual(legacy, File.ReadAllText(store.LegacyBackupPath), "an exact copy of the original file");
            Assert.AreEqual(legacy, File.ReadAllText(store.PrimaryPath), "the primary is only replaced by the next write");

            Assert.IsTrue(store.Write(r.save));
            Assert.AreEqual(legacy, File.ReadAllText(store.BackupPath), "the v1 file also becomes the rolling backup");
            Assert.AreEqual(legacy, File.ReadAllText(store.LegacyBackupPath));
            var again = store.Load(Migrate);
            Assert.AreEqual(0, again.report.migratedFrom, "the v2 file needs no migration");
            Assert.AreEqual(1, migrated);
        }

        [Test]
        public void LegacyBackup_IsNeverOverwrittenByALaterMigration()
        {
            File.WriteAllText(store.LegacyBackupPath, "the very first v1 file");
            File.WriteAllText(store.PrimaryPath, "{\"xp\":1}");
            store.Load(Migrate);
            Assert.AreEqual("the very first v1 file", File.ReadAllText(store.LegacyBackupPath));
        }

        [Test]
        public void FailedVerification_LeavesThePreviousFileIntact_AndNoTempBehind()
        {
            store.Write(Save(10));
            string before = File.ReadAllText(store.PrimaryPath);
            codec.CorruptOnWrite = true;
            Assert.IsFalse(store.Write(Save(20)));
            StringAssert.Contains("verification", store.LastWriteError);
            Assert.AreEqual(before, File.ReadAllText(store.PrimaryPath));
            Assert.IsFalse(File.Exists(store.TempPath));
            codec.CorruptOnWrite = false;
            Assert.IsTrue(store.Write(Save(30)));
            Assert.IsNull(store.LastWriteError);
        }

        [Test]
        public void DeleteAll_RemovesEveryFile()
        {
            store.Write(Save(1));
            store.Write(Save(2));
            File.WriteAllText(store.LegacyBackupPath, "{}");
            File.WriteAllText(store.TempPath, "{}");
            store.DeleteAll();
            Assert.IsFalse(File.Exists(store.PrimaryPath));
            Assert.IsFalse(File.Exists(store.BackupPath));
            Assert.IsFalse(File.Exists(store.TempPath));
            Assert.IsFalse(File.Exists(store.LegacyBackupPath));
            Assert.IsNull(store.Load(Migrate).save);
        }

        [Test]
        public void ReadOnlyFlag_IsClearedByDeletingTheSave()
        {
            File.WriteAllText(store.PrimaryPath, "{\"schemaVersion\":99,\"saveId\":\"f\",\"xp\":1,\"worlds\":0,\"inventory\":0}");
            store.Load(Migrate);
            Assert.IsTrue(store.ReadOnly);
            store.DeleteAll();
            Assert.IsFalse(store.ReadOnly);
            Assert.IsTrue(store.Write(Save(5)));
        }

        [Test]
        public void WritingIntoAMissingDirectory_CreatesIt()
        {
            string nested = Path.Combine(dir, "a", "b");
            var s = new SaveStore(nested, codec);
            Assert.IsTrue(s.Write(Save(3)), s.LastWriteError);
            Assert.AreEqual(3, s.Load(Migrate).save.player.xp);
        }

        [Test]
        public void JsonShape_RecognisesCompleteObjectsOnly()
        {
            Assert.IsTrue(JsonShape.IsCompleteObject("{}"));
            Assert.IsTrue(JsonShape.IsCompleteObject("  {\"a\":{\"b\":[1,2,{\"c\":\"}\"}]}}\r\n"));
            Assert.IsTrue(JsonShape.IsCompleteObject("\uFEFF{\"s\":\"quote \\\" and brace }\"}"));
            Assert.IsFalse(JsonShape.IsCompleteObject(""));
            Assert.IsFalse(JsonShape.IsCompleteObject(null));
            Assert.IsFalse(JsonShape.IsCompleteObject("{\"a\":{\"b\":1}"));
            Assert.IsFalse(JsonShape.IsCompleteObject("{\"a\":1}}"));
            Assert.IsFalse(JsonShape.IsCompleteObject("{\"a\":1} trailing"));
            Assert.IsFalse(JsonShape.IsCompleteObject("[1,2]"));
            Assert.IsFalse(JsonShape.IsCompleteObject("{\"a\":\"unterminated}"));
        }
    }
}
