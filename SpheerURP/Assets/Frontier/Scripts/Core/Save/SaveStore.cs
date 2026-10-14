using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Frontier
{
    /// <summary>Turns saves into text and back. The game implements it with Unity's JsonUtility; tests use a tiny fake.</summary>
    public interface ISaveCodec
    {
        string Serialize(SaveV2 save);

        /// <summary>Throws when the text is not a readable v2 (or newer) save.</summary>
        SaveV2 DeserializeV2(string text);

        /// <summary>Throws when the text is not a readable legacy save.</summary>
        SaveDataV1 DeserializeV1(string text);

        /// <summary>The schemaVersion field, 0 when the file has none (legacy v1). Throws when the text is unreadable.</summary>
        int ReadSchemaVersion(string text);
    }

    public enum SaveSource
    {
        /// <summary>Nothing usable was found: a new game starts.</summary>
        NewGame,
        Primary,
        /// <summary>The rolling backup of the previous good save.</summary>
        Backup,
        /// <summary>A fully written temp file left behind by an interrupted write.</summary>
        TempRecovery
    }

    /// <summary>What happened when a save was loaded (for the UI, logs and analytics).</summary>
    public sealed class SaveLoadReport
    {
        public SaveSource source;
        /// <summary>The schema the data was migrated from; 0 when it was already current.</summary>
        public int migratedFrom;
        /// <summary>True when the data came from the backup or a leftover temp file because the primary file was unusable.</summary>
        public bool recovered;
        /// <summary>Why the primary file was not used, or a note about the save (for example a newer schema). Empty when everything was fine.</summary>
        public string error = "";
        /// <summary>The file was written by a newer build: it is loaded for reading but never overwritten.</summary>
        public bool readOnly;
        /// <summary>The schema version of the data that was loaded.</summary>
        public int schemaVersion;
        public readonly List<string> warnings = new List<string>();
    }

    public sealed class SaveLoadResult
    {
        /// <summary>The loaded save, or null when a new game should start.</summary>
        public SaveV2 save;
        public SaveLoadReport report = new SaveLoadReport();
    }

    public delegate SaveV2 SaveMigrationFunc(SaveDataV1 legacy, List<string> warnings);

    /// <summary>
    /// The save file protocol. Write: serialize to <c>frontier_save.json.tmp</c>, re-read and verify it, copy the previous good file to
    /// <c>frontier_save.json.bak</c>, then replace the primary file. Load: primary, then backup, then a leftover temp file, then a new
    /// game; the first one that parses wins and the report says where it came from. A file written by a newer schema is loaded read-only
    /// and never overwritten. The original v1 file is copied to <c>frontier_save.v1.bak.json</c> before its first migration.
    /// </summary>
    public sealed class SaveStore
    {
        static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

        readonly ISaveCodec codec;

        public SaveStore(string directory, ISaveCodec codec)
        {
            if (string.IsNullOrEmpty(directory)) throw new ArgumentException("directory is required");
            if (codec == null) throw new ArgumentNullException("codec");
            DirectoryPath = directory;
            this.codec = codec;
        }

        public string DirectoryPath { get; private set; }
        public string PrimaryPath { get { return Path.Combine(DirectoryPath, SaveSchema.FileName); } }
        public string BackupPath { get { return PrimaryPath + SaveSchema.BackupSuffix; } }
        public string CloudRestoreBackupPath { get { return PrimaryPath + ".restore.bak"; } }
        public bool HasCloudRestoreBackup { get { return File.Exists(CloudRestoreBackupPath); } }
        public string TempPath { get { return PrimaryPath + SaveSchema.TempSuffix; } }
        public string LegacyBackupPath { get { return Path.Combine(DirectoryPath, SaveSchema.LegacyBackupName); } }

        /// <summary>True after loading a file from a newer schema: <see cref="Write"/> refuses.</summary>
        public bool ReadOnly { get; private set; }

        /// <summary>Why <see cref="ReadOnly"/> is set (shown to the player and in logs); empty otherwise.</summary>
        public string ReadOnlyReason { get; private set; }

        /// <summary>The message of the last failed <see cref="Write"/>, or null.</summary>
        public string LastWriteError { get; private set; }

        /// <summary>Protects the loaded file from being overwritten, for example when it belongs to a newer universe generator than this build has.</summary>
        public void MarkReadOnly(string reason)
        {
            ReadOnly = true;
            ReadOnlyReason = string.IsNullOrEmpty(reason) ? "the save file is read-only" : reason;
        }

        // ---- loading ---------------------------------------------------------------------------------------------------

        public SaveLoadResult Load(SaveMigrationFunc migrate)
        {
            var result = new SaveLoadResult();
            var report = result.report;
            ReadOnly = false;
            ReadOnlyReason = "";
            string[] paths = { PrimaryPath, BackupPath, TempPath };
            SaveSource[] sources = { SaveSource.Primary, SaveSource.Backup, SaveSource.TempRecovery };
            string primaryProblem = null;
            for (int i = 0; i < paths.Length; i++)
            {
                string problem;
                var save = TryLoad(paths[i], migrate, report, out problem);
                if (save != null)
                {
                    result.save = save;
                    report.source = sources[i];
                    report.recovered = i > 0;
                    if (i > 0 && primaryProblem != null) report.error = primaryProblem;
                    return result;
                }
                if (i == 0) primaryProblem = problem;
            }
            report.source = SaveSource.NewGame;
            if (!string.IsNullOrEmpty(primaryProblem)) report.error = primaryProblem;
            return result;
        }

        SaveV2 TryLoad(string path, SaveMigrationFunc migrate, SaveLoadReport report, out string problem)
        {
            problem = null;
            string text;
            try
            {
                if (!File.Exists(path)) return null;
                text = File.ReadAllText(path, Utf8);
            }
            catch (Exception e)
            {
                problem = Path.GetFileName(path) + ": " + e.Message;
                return null;
            }
            if (!JsonShape.IsCompleteObject(text))
            {
                problem = Path.GetFileName(path) + ": empty, truncated or not a JSON object";
                return null;
            }
            try
            {
                int version = codec.ReadSchemaVersion(text);
                SaveV2 save;
                if (version > SaveSchema.Current)
                {
                    save = codec.DeserializeV2(text).Normalize();
                    report.error = "The save was written by a newer version of the game (schema " + version + ", this build reads " + SaveSchema.Current +
                                   "). It is loaded read-only and will not be overwritten.";
                    MarkReadOnly(report.error);
                    report.readOnly = true;
                }
                else if (version == SaveSchema.Current)
                {
                    save = codec.DeserializeV2(text).Normalize();
                }
                else
                {
                    if (migrate == null) throw new InvalidOperationException("a legacy save needs a migration function");
                    var legacy = codec.DeserializeV1(text);
                    TryCopyLegacy(path);
                    save = migrate(legacy, report.warnings).Normalize();
                    report.migratedFrom = 1;
                }
                report.schemaVersion = save.schemaVersion;
                return save;
            }
            catch (Exception e)
            {
                problem = Path.GetFileName(path) + ": " + e.Message;
                return null;
            }
        }

        void TryCopyLegacy(string path)
        {
            try { if (!File.Exists(LegacyBackupPath)) File.Copy(path, LegacyBackupPath, false); }
            catch (Exception) { /* the backup is a courtesy; migration must not fail because of it */ }
        }

        // ---- writing ---------------------------------------------------------------------------------------------------

        /// <summary>Writes the save atomically (see the class summary). Returns false and sets <see cref="LastWriteError"/> on failure; the previous file stays intact.</summary>
        public bool Write(SaveV2 save)
        {
            LastWriteError = null;
            if (ReadOnly)
            {
                LastWriteError = ReadOnlyReason;
                return false;
            }
            try
            {
                Directory.CreateDirectory(DirectoryPath);
                string json = codec.Serialize(save);
                WriteDurable(TempPath, json);
                Verify(save, File.ReadAllText(TempPath, Utf8));
                if (File.Exists(PrimaryPath))
                {
                    if (IsReadable(PrimaryPath)) File.Copy(PrimaryPath, BackupPath, true);
                    try { File.Replace(TempPath, PrimaryPath, null); }
                    catch (Exception)
                    {
                        File.Copy(TempPath, PrimaryPath, true);
                        File.Delete(TempPath);
                    }
                }
                else File.Move(TempPath, PrimaryPath);
                return true;
            }
            catch (Exception e)
            {
                LastWriteError = e.Message;
                try { if (File.Exists(TempPath)) File.Delete(TempPath); }
                catch (Exception) { /* nothing more to do */ }
                return false;
            }
        }

        /// <summary>
        /// Explicitly replaces the primary save while retaining the current primary as a separate, one-deep restore point.
        /// Ordinary writes never rotate or delete this player-selected recovery snapshot.
        /// </summary>
        public bool WriteWithRestoreBackup(SaveV2 save)
        {
            LastWriteError = null;
            if (ReadOnly)
            {
                LastWriteError = ReadOnlyReason;
                return false;
            }

            if (File.Exists(PrimaryPath))
            {
                if (!IsReadable(PrimaryPath))
                {
                    LastWriteError = "the current local save cannot be safely preserved before replacement";
                    return false;
                }

                string temporaryBackup = CloudRestoreBackupPath + ".tmp";
                try
                {
                    Directory.CreateDirectory(DirectoryPath);
                    File.Copy(PrimaryPath, temporaryBackup, true);
                    if (!IsReadable(temporaryBackup))
                        throw new IOException("the preserved local save did not pass verification");

                    if (File.Exists(CloudRestoreBackupPath))
                    {
                        try { File.Replace(temporaryBackup, CloudRestoreBackupPath, null); }
                        catch (Exception)
                        {
                            File.Copy(temporaryBackup, CloudRestoreBackupPath, true);
                            File.Delete(temporaryBackup);
                        }
                    }
                    else File.Move(temporaryBackup, CloudRestoreBackupPath);
                }
                catch (Exception e)
                {
                    LastWriteError = e.Message;
                    try { if (File.Exists(temporaryBackup)) File.Delete(temporaryBackup); }
                    catch (Exception cleanupError)
                    {
                        LastWriteError += " The temporary restore backup could not be removed: " + cleanupError.Message;
                    }
                    return false;
                }
            }

            return Write(save);
        }

        /// <summary>Reads the one-deep player recovery snapshot without changing the active save or its read-only state.</summary>
        public SaveV2 ReadCloudRestoreBackup(out string problem)
        {
            problem = null;
            try
            {
                if (!File.Exists(CloudRestoreBackupPath))
                {
                    problem = "No previous local save is available to restore.";
                    return null;
                }

                string text = File.ReadAllText(CloudRestoreBackupPath, Utf8);
                if (!JsonShape.IsCompleteObject(text))
                {
                    problem = "The retained local save is incomplete and cannot be restored.";
                    return null;
                }

                int version = codec.ReadSchemaVersion(text);
                if (version != SaveSchema.Current)
                {
                    problem = "The retained local save uses schema " + version +
                        "; this build can restore only schema " + SaveSchema.Current + ".";
                    return null;
                }

                var save = codec.DeserializeV2(text);
                if (save == null)
                {
                    problem = "The retained local save could not be decoded.";
                    return null;
                }
                return save.Normalize();
            }
            catch (Exception e)
            {
                problem = "The retained local save could not be read: " + e.Message;
                return null;
            }
        }

        static void WriteDurable(string path, string text)
        {
            byte[] bytes = Utf8.GetBytes(text);
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
        }

        void Verify(SaveV2 written, string text)
        {
            if (!JsonShape.IsCompleteObject(text)) throw new IOException("verification failed: the written file is not a complete JSON object");
            var back = codec.DeserializeV2(text);
            if (back == null || back.schemaVersion != written.schemaVersion || back.saveId != written.saveId ||
                back.revision != written.revision ||
                back.player == null || back.player.xp != written.player.xp ||
                back.worlds == null || back.worlds.Count != written.worlds.Count ||
                back.player.inventory == null || back.player.inventory.Count != written.player.inventory.Count)
                throw new IOException("verification failed: the written file does not match the state");
        }

        bool IsReadable(string path)
        {
            try
            {
                string text = File.ReadAllText(path, Utf8);
                if (!JsonShape.IsCompleteObject(text)) return false;
                codec.ReadSchemaVersion(text);
                return true;
            }
            catch (Exception) { return false; }
        }

        /// <summary>Deletes the active save and every recovery copy (a player-requested reset).</summary>
        public void DeleteAll()
        {
            foreach (string path in new[]
            {
                PrimaryPath, BackupPath, TempPath, LegacyBackupPath,
                CloudRestoreBackupPath, CloudRestoreBackupPath + ".tmp"
            })
            {
                try { if (File.Exists(path)) File.Delete(path); }
                catch (Exception) { /* best effort */ }
            }
            ReadOnly = false;
            ReadOnlyReason = "";
            LastWriteError = null;
        }
    }
}
