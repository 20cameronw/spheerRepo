using System;
using System.Collections.Generic;
using UnityEngine;

namespace Frontier
{
    // Persistence, new games and debug helpers of the session.
    public partial class GameState
    {
        /// <summary>Play time is only counted for frames shorter than this (a long gap means the app was suspended or loading).</summary>
        public const double MaxCountedFrameSeconds = 5.0;
        /// <summary>While the app runs, play time alone marks the state dirty this often so the stat survives a crash.</summary>
        public const double PlayTimeSaveSeconds = 60.0;

        string saveId = "";
        long saveRevision;
        CreatedWithInfo createdWith = new CreatedWithInfo();
        // Reserved save sections: this build does not interpret them but writes back what it loaded.
        List<WalletEntry> wallet = new List<WalletEntry>();
        List<PurchaseGrantRecord> purchaseGrants = new List<PurchaseGrantRecord>();
        string purchaseContextId = Guid.NewGuid().ToString("N");
        List<BuilderSlot> builders = new List<BuilderSlot>();
        List<string> atlas = new List<string>();
        List<string> mapPins = new List<string>();
        List<string> mapDiscoveries = new List<string>();
        List<SettingEntry> settings = new List<SettingEntry>();
        double lastTickAt, playTimeMarkedAt;
        DateTime clockHighWaterUtc;

        public bool ClockRewindDetected { get { return clock.UtcNow < clockHighWaterUtc; } }

        const string FirstSessionGuideStartedKey = "first_session_guide_started";
        const string FirstSessionGuideDismissedKey = "first_session_guide_dismissed";

        /// <summary>What happened when the save was loaded (source, migration, recovery, warnings). Null never: a new game reports <see cref="SaveSource.NewGame"/>.</summary>
        public SaveLoadReport LoadReport { get; private set; }

        public bool FirstSessionGuideAvailable
        {
            get { return HasSetting(FirstSessionGuideStartedKey) && !HasSetting(FirstSessionGuideDismissedKey); }
        }

        public string FirstSessionGuideIdentity { get { return saveId; } }

        bool HasSetting(string key)
        {
            if (settings == null) return false;
            foreach (var setting in settings)
                if (setting != null && setting.key == key && setting.value == "1") return true;
            return false;
        }

        void SetSetting(string key, string value)
        {
            if (settings == null) settings = new List<SettingEntry>();
            bool found = false;
            foreach (var setting in settings)
            {
                if (setting == null || setting.key != key) continue;
                setting.value = value;
                found = true;
            }
            if (!found) settings.Add(new SettingEntry { key = key, value = value });
        }

        public void DismissFirstSessionGuide()
        {
            if (!FirstSessionGuideAvailable) return;
            SetSetting(FirstSessionGuideDismissedKey, "1");
            Notify();
        }

        /// <summary>True when the save on disk was written by a newer build: this session plays on, but nothing is written back.</summary>
        public bool SaveIsReadOnly { get { return saves.Store.ReadOnly; } }
        public bool HasCloudRestoreBackup { get { return saves.Store.HasCloudRestoreBackup; } }

        static string SaveDirectory()
        {
            return string.IsNullOrEmpty(SaveDirectoryOverride) ? Application.persistentDataPath : SaveDirectoryOverride;
        }

        static string NewSaveId() { return Guid.NewGuid().ToString("N"); }

        // ---------- Persistence ----------
        /// <summary>Marks the state changed and tells the UI. Nothing is written yet: the app flushes after a second of quiet (see <see cref="Tick"/>).</summary>
        public void Notify()
        {
            ObserveEraProgress();
            ObserveObjectives();
            saves.MarkDirty();
            if (Changed != null) Changed();
        }

        /// <summary>Call every frame from the app: counts play time and writes the save when the debounce is due. Returns true when a file was written.</summary>
        public bool Tick()
        {
            double now = clock.MonotonicSeconds;
            double dt = now - lastTickAt;
            lastTickAt = now;
            if (dt > 0.0 && dt <= MaxCountedFrameSeconds) stats.AddPlaySeconds(dt);
            if (CompleteBuilderJobs()) Notify();
            if (now - playTimeMarkedAt >= PlayTimeSaveSeconds)
            {
                playTimeMarkedAt = now;
                saves.MarkDirty();
            }
            return saves.Tick();
        }

        /// <summary>Writes now if anything changed: app pause, focus loss, quit, travel, purchases. Returns true when a file was written.</summary>
        public bool Flush() { return saves.Flush(); }

        /// <summary>Writes now whether or not anything changed (tests, the harness).</summary>
        public bool Save() { return saves.Save(); }

        /// <summary>Writes the latest local state and returns exactly the snapshot that was persisted.</summary>
        public bool TryCreateCloudSaveSnapshot(out SaveV2 snapshot, out string message)
        {
            snapshot = null;
            message = "";
            if (SaveIsReadOnly)
            {
                message = saves.LastError;
                return false;
            }
            if (!saves.Save())
            {
                message = string.IsNullOrEmpty(saves.LastError)
                    ? UiText.Get("snapshot.write-failed")
                    : UiText.Get("snapshot.write-error", saves.LastError);
                return false;
            }
            snapshot = saves.LastWrittenSnapshot;
            if (snapshot == null)
            {
                message = UiText.Get("snapshot.unreadable");
                return false;
            }
            return true;
        }

        /// <summary>Restores a player-selected cloud snapshot and retains the previous local snapshot for an explicit undo.</summary>
        public bool TryRestoreCloudSave(SaveV2 cloudSnapshot, out string message)
        {
            return TryRestoreSnapshot(cloudSnapshot, UiText.Get("restore.source.cloud"), out message);
        }

        /// <summary>Swaps the current save with the retained pre-restore local snapshot.</summary>
        public bool TryRestorePreviousLocalSave(out string message)
        {
            string readError;
            SaveV2 previous = saves.Store.ReadCloudRestoreBackup(out readError);
            if (previous == null)
            {
                message = readError;
                return false;
            }
            return TryRestoreSnapshot(previous, UiText.Get("restore.source.local"), out message);
        }

        public bool CanRestoreCloudSaveSnapshot(SaveV2 snapshot, out string message)
        {
            message = "";
            if (snapshot == null)
            {
                message = UiText.Get("restore.empty");
                return false;
            }
            if (SaveIsReadOnly)
            {
                message = saves.LastError;
                return false;
            }

            if (HasPurchaseHistory || snapshot.purchaseGrants != null && snapshot.purchaseGrants.Count != 0)
            {
                message = UiText.Get("purchase.recovery-required");
                return false;
            }

            snapshot.Normalize();
            if (!string.IsNullOrEmpty(snapshot.clock.highWaterUtc) &&
                !UtcText.TryParse(snapshot.clock.highWaterUtc, out _))
            {
                message = UiText.Get("clock.history-invalid");
                return false;
            }
            if (snapshot.schemaVersion != SaveSchema.Current)
            {
                message = UiText.Get("restore.schema", snapshot.schemaVersion, SaveSchema.Current);
                return false;
            }
            if (snapshot.createdWith.generatorVersion > universe.Universe.GeneratorVersion)
            {
                message = UiText.Get("restore.generator-newer");
                return false;
            }
            if (!string.IsNullOrEmpty(snapshot.createdWith.universeSeed) &&
                !SeedText.TryParse(snapshot.createdWith.universeSeed, out _))
            {
                message = UiText.Get("restore.invalid-seed");
                return false;
            }
            return true;
        }

        bool TryRestoreSnapshot(SaveV2 snapshot, string source, out string message)
        {
            if (!CanRestoreCloudSaveSnapshot(snapshot, out message)) return false;
            // Keep local elapsed-time history even when restoring older progression.
            snapshot = SaveSerializer.Instance.DeserializeV2(SaveSerializer.Instance.Serialize(snapshot)).Normalize();
            DateTime retainedTime = clockHighWaterUtc > clock.UtcNow ? clockHighWaterUtc : clock.UtcNow;
            if (!UtcText.TryParse(snapshot.clock.highWaterUtc, out var incomingTime) || incomingTime < retainedTime)
                snapshot.clock.highWaterUtc = UtcText.Format(retainedTime);
            if (!saves.Save())
            {
                message = string.IsNullOrEmpty(saves.LastError)
                    ? UiText.Get("restore.secure-failed")
                    : UiText.Get("restore.secure-error", saves.LastError);
                return false;
            }
            snapshot.revision = saveRevision == long.MaxValue ? long.MaxValue : Math.Max(snapshot.revision, saveRevision + 1);
            if (!saves.Store.WriteWithRestoreBackup(snapshot))
            {
                message = UiText.Get("restore.write-failed", saves.Store.LastWriteError);
                return false;
            }

            var report = new SaveLoadReport
            {
                source = SaveSource.Primary,
                schemaVersion = snapshot.schemaVersion
            };
            Apply(snapshot, report);
            LoadReport = report;
            Notify();
            if (!saves.Save())
            {
                message = UiText.Get("restore.pending-save", source, saves.LastError);
                return true;
            }

            message = UiText.Get("restore.done", source);
            return true;
        }

        void LoadOrStart()
        {
            var result = saves.Load(MigrateLegacy);
            LoadReport = result.report;
            if (result.save == null)
            {
                StartNewGame();
                LogLoad();
                return;
            }
            bool contentRemapped = RemapContentIds(result.save);
            bool offlineChanged = Apply(result.save, result.report);
            LogLoad();
            // A migrated or recovered save replaces the primary file right away, so the next launch reads a clean v2 file.
            if (contentRemapped || result.report.migratedFrom != 0 || result.report.recovered || offlineChanged)
            {
                saves.MarkDirty();
                Flush();
            }
        }

        SaveV2 MigrateLegacy(SaveDataV1 legacy, List<string> warnings)
        {
            var context = new MigrationContext
            {
                AppVersion = Application.version,
                UniverseSeed = universe.Seed,
                GeneratorVersion = universe.Universe.GeneratorVersion,
                NowUtc = clock.UtcNow,
                SaveId = NewSaveId()
            };
            return SaveMigrator.FromV1(legacy, universe, context, warnings);
        }

        void LogLoad()
        {
            var r = LoadReport;
            if (r == null) return;
            if (r.recovered) Debug.LogWarning("Frontier: the save was recovered from " + r.source + (string.IsNullOrEmpty(r.error) ? "" : " (" + r.error + ")"));
            else if (!string.IsNullOrEmpty(r.error)) Debug.LogWarning("Frontier: " + r.error);
            if (r.migratedFrom != 0) Debug.Log("Frontier: migrated the v" + r.migratedFrom + " save to schema " + SaveSchema.Current + "; the original is kept as " + SaveSchema.LegacyBackupName);
            foreach (string w in r.warnings) Debug.LogWarning("Frontier save: " + w);
        }

        void ClearState()
        {
            purchaseContextId = Guid.NewGuid().ToString("N");
            purchaseGrants = new List<PurchaseGrantRecord>();
            eraReached = Frontier.Economy.FrontierEra.Landfall;
            objectiveLedger = new ObjectiveLedger();
            objectivesInitialized = false;
            ascension = new AscensionProgress();
            backpack.Clear();
            progression.Clear();
            world.Clear();
            ClearProgressionContent();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            shipLevelOverrides.Clear();
#endif
            saveRevision = 0;
            stats = new PlayerStats();
            wallet = new List<WalletEntry>();
            builders = new List<BuilderSlot>();
            atlas = new List<string>();
            mapPins = new List<string>();
            mapDiscoveries = new List<string>();
            mapDiscoverySet.Clear();
            settings = new List<SettingEntry>();
            clockHighWaterUtc = DateTime.MinValue;
        }

        void StartNewGame()
        {
            ClearState();
            universe = UniverseService.For(db);
            objectivesInitialized = true;
            SetSetting(FirstSessionGuideStartedKey, "1");
            saveId = NewSaveId();
            createdWith = new CreatedWithInfo
            {
                appVersion = Application.version,
                generatorVersion = universe.Universe.GeneratorVersion,
                universeSeed = SeedText.Format(universe.Seed)
            };
            foreach (var s in db.startingItems)
            {
                if (s.resource == null) continue;
                backpack.Set(s.resource.id, s.amount);
                if (s.amount > 0) RecordAtlasResourceAcquired(s.resource.id);
            }
            planet = universe.Home;
            RecordArrival(planet);
            saves.MarkDirty();
        }

        bool Apply(SaveV2 save, SaveLoadReport report)
        {
            var warnings = report.warnings;
            ClearState();
            saveId = !string.IsNullOrEmpty(save.saveId) ? save.saveId : NewSaveId();
            saveRevision = save.revision;
            createdWith = save.createdWith;
            wallet = save.wallet; builders = save.builders; atlas = save.atlas; mapPins = save.mapPins;
            purchaseGrants = save.purchaseGrants;
            mapDiscoveries = new List<string>(MapDiscoveryBits.Decode(save.mapDiscoverySectors)); settings = save.settings;
            mapDiscoverySet.Clear();
            mapDiscoverySet.UnionWith(mapDiscoveries);

            ulong seed = UniverseService.DefaultSeed;
            if (!string.IsNullOrEmpty(createdWith.universeSeed) && !SeedText.TryParse(createdWith.universeSeed, out seed))
            {
                seed = UniverseService.DefaultSeed;
                string why = "the save has an invalid universe seed; loaded read-only to avoid overwriting its worlds";
                warnings.Add(why);
                saves.Store.MarkReadOnly(why);
                report.readOnly = true;
                report.error = why;
            }
            if (seed != universe.Seed)
                universe = seed == UniverseService.DefaultSeed ? UniverseService.For(db) : new UniverseService(db, seed: seed);
            if (createdWith.generatorVersion > universe.Universe.GeneratorVersion)
            {
                string why = "the save belongs to universe generator v" + createdWith.generatorVersion + ", this build has v" + universe.Universe.GeneratorVersion +
                             "; it is loaded read-only and will not be overwritten";
                saves.Store.MarkReadOnly(why);
                report.readOnly = true;
                if (string.IsNullOrEmpty(report.error)) report.error = why;
            }

            progression.SetXp(save.player.xp);
            foreach (var u in save.player.upgrades)
            {
                var upgrade = db.FindUpgrade(u.id);
                if (upgrade == null) { warnings.Add("dropped the unknown upgrade '" + u.id + "'"); continue; }
                progression.SetUpgradeLevel(upgrade.id, u.level, upgrade.MaxLevel);
            }
            LoadProgressionContent(save.player, warnings);
            foreach (var stack in save.player.inventory)
            {
                if (!backpack.IsKnown(stack.resourceId)) { warnings.Add("dropped " + stack.count + " of the unknown resource '" + stack.resourceId + "'"); continue; }
                backpack.Set(stack.resourceId, stack.count);
            }
            foreach (var delta in save.worlds)
            {
                if (delta == null || delta.structures == null) continue;
                delta.structures.RemoveAll(record =>
                {
                    if (record == null || string.IsNullOrEmpty(record.defId))
                    {
                        warnings.Add("dropped an empty structure record on " + delta.planetId);
                        return true;
                    }
                    if (db.FindStructure(record.defId) != null) return false;
                    warnings.Add("dropped the unknown structure '" + record.defId + "' on " + delta.planetId);
                    return true;
                });
            }
            world.Import(save.worlds);
            stats = save.stats ?? new PlayerStats();
            objectiveLedger = new ObjectiveLedger(save.objectives);
            objectivesInitialized = save.objectivesInitialized;
            ascension = save.ascension ?? new AscensionProgress();
            string offlineSince = LoadClockHistory(save.clock, report);
            bool offlineChanged = AccrueOfflineHarvesters(offlineSince);
            offlineChanged |= AccrueOfflineDysonCollectors(offlineSince);
            bool builderJobsChanged = RecoverBuilderJobs(warnings);
            SeedAtlasFromExistingProgress();
            LoadEraProgress(report);
            bool objectivesChanged = ObserveObjectives(!objectivesInitialized);
            if (!objectivesInitialized)
            {
                objectivesInitialized = true;
                objectivesChanged = true;
            }

            planet = universe.Get(save.location.currentPlanetId);
            if (planet == null)
            {
                if (!string.IsNullOrEmpty(save.location.currentPlanetId)) warnings.Add("the world '" + save.location.currentPlanetId + "' does not exist; returning home");
                planet = universe.Home;
            }
            return offlineChanged || builderJobsChanged || objectivesChanged;
        }

        SaveV2 BuildSave()
        {
            ObserveObjectives();
            if (saveRevision < long.MaxValue) saveRevision++;
            var save = new SaveV2
            {
                saveId = saveId,
                revision = saveRevision,
                createdWith = createdWith,
                wallet = wallet,
                purchaseGrants = purchaseGrants,
                builders = builders,
                atlas = atlas,
                mapPins = mapPins,
                mapDiscoverySectors = MapDiscoveryBits.Encode(mapDiscoveries),
                settings = settings,
                stats = stats
            };
            save.objectivesInitialized = objectivesInitialized;
            save.objectives = objectiveLedger.Export();
            save.ascension = new AscensionProgress { rank = AscensionRank, legacyPoints = LegacyPoints };
            save.player.xp = progression.Xp;
            foreach (var kv in progression.Upgrades) save.player.upgrades.Add(new IdLevel { id = kv.Key, level = kv.Value });
            save.player.upgrades.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            save.player.researched.AddRange(new List<string>(ResearchedIds));
            save.player.researched.Sort(StringComparer.Ordinal);
            foreach (var kv in ToolTiers) save.player.toolTiers.Add(new IdLevel { id = kv.Key, level = kv.Value });
            save.player.toolTiers.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            foreach (var kv in backpack.Stacks) save.player.inventory.Add(new StackRecord { resourceId = kv.Key, count = kv.Value });
            save.player.inventory.Sort((a, b) => string.CompareOrdinal(a.resourceId, b.resourceId));
            save.location.currentPlanetId = planet.Id;
            save.worlds = world.Export();
            save.clock.lastSavedUtc = UtcText.Format(clock.UtcNow);
            if (clock.UtcNow > clockHighWaterUtc) clockHighWaterUtc = clock.UtcNow;
            save.clock.highWaterUtc = UtcText.Format(clockHighWaterUtc);
            return save;
        }

        string LoadClockHistory(ClockSave savedClock, SaveLoadReport report)
        {
            if (UtcText.TryParse(savedClock.lastSavedUtc, out var lastSaved))
                clockHighWaterUtc = lastSaved;
            if (!string.IsNullOrEmpty(savedClock.highWaterUtc))
            {
                if (!UtcText.TryParse(savedClock.highWaterUtc, out var highWater))
                {
                    string reason = UiText.Get("clock.history-invalid");
                    saves.Store.MarkReadOnly(reason);
                    report.readOnly = true;
                    report.error = reason;
                    return "";
                }
                if (highWater > clockHighWaterUtc) clockHighWaterUtc = highWater;
            }
            if (ClockRewindDetected)
                report.warnings.Add(UiText.Get("clock.rewind-detected"));
            return clockHighWaterUtc == DateTime.MinValue ? "" : UtcText.Format(clockHighWaterUtc);
        }

        /// <summary>Erases every save file and starts a new game on the home world.</summary>
        public void ResetProgress()
        {
            if (!TryResetProgress(out string message))
                Debug.LogWarning("Frontier reset refused: " + message);
        }

        public bool TryResetProgress(out string message)
        {
            if (HasPurchaseHistory)
            {
                message = UiText.Get("purchase.recovery-required");
                return false;
            }
            saves.DeleteAll();
            StartNewGame();
            Notify();
            Flush();
            message = "";
            return true;
        }

        // ---------- Debug ----------
        public void DebugSetBuilderJob(PlacedStructure structure, int seconds, int upgradeLevel = 0)
        {
            if (!IsPlaced(structure)) throw new ArgumentException("A placed structure is required.", nameof(structure));
            if (seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
            if (upgradeLevel != 0 && (upgradeLevel <= structure.level || upgradeLevel > structure.def.maxLevel))
                throw new ArgumentOutOfRangeException(nameof(upgradeLevel));
            string target = BuilderTarget(structure.planetId, structure.slot);
            builders.RemoveAll(slot => slot != null && slot.target == target);
            structure.record.buildEndUtc = "";
            structure.record.upgradeEndUtc = "";
            structure.record.pendingUpgradeLevel = 0;
            StartBuilderJob(structure, seconds, upgradeLevel);
            world.Touch();
            Notify();
        }

        public void DebugSetUpgrade(UpgradeSO u, int level)
        {
            progression.SetUpgradeLevel(u.id, level, u.MaxLevel);
            Notify();
        }

        public void DebugSetLevel(int level)
        {
            progression.SetLevel(level);
            Notify();
        }

        /// <summary>Moves the player to another world for free. Counts as a visit of the world but not as a landing in the stats.</summary>
        public void DebugTeleport(PlanetData p)
        {
            if (p == null) return;
            planet = p;
            world.RecordVisit(p.Id, clock.UtcNow);
            Notify();
        }

        /// <summary>Builds a structure on the current world without cost, limits or build rules (tests, harness). Null when the pad is taken.</summary>
        public PlacedStructure DebugPlace(StructureSO def, int slot, int level = 1)
        {
            if (def == null || slot < 0) return null;
            var record = world.Place(planet.Id, def.id, slot, level);
            if (record == null) return null;
            Notify();
            foreach (var s in structures) if (s.record == record) return s;
            return null;
        }

        public void DebugSetStructureLevel(PlacedStructure s, int level)
        {
            if (!IsPlaced(s)) return;
            world.SetLevel(s.record, level);
            Notify();
        }
    }
}
