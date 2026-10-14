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
        CreatedWithInfo createdWith = new CreatedWithInfo();
        // Reserved save sections: this build does not interpret them but writes back what it loaded.
        List<WalletEntry> wallet = new List<WalletEntry>();
        List<BuilderSlot> builders = new List<BuilderSlot>();
        List<string> atlas = new List<string>();
        List<SettingEntry> settings = new List<SettingEntry>();
        double lastTickAt, playTimeMarkedAt;

        /// <summary>What happened when the save was loaded (source, migration, recovery, warnings). Null never: a new game reports <see cref="SaveSource.NewGame"/>.</summary>
        public SaveLoadReport LoadReport { get; private set; }

        /// <summary>True when the save on disk was written by a newer build: this session plays on, but nothing is written back.</summary>
        public bool SaveIsReadOnly { get { return saves.Store.ReadOnly; } }

        static string SaveDirectory()
        {
            return string.IsNullOrEmpty(SaveDirectoryOverride) ? Application.persistentDataPath : SaveDirectoryOverride;
        }

        static string NewSaveId() { return Guid.NewGuid().ToString("N"); }

        // ---------- Persistence ----------
        /// <summary>Marks the state changed and tells the UI. Nothing is written yet: the app flushes after a second of quiet (see <see cref="Tick"/>).</summary>
        public void Notify()
        {
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
            Apply(result.save, result.report);
            LogLoad();
            // A migrated or recovered save replaces the primary file right away, so the next launch reads a clean v2 file.
            if (contentRemapped || result.report.migratedFrom != 0 || result.report.recovered) { saves.MarkDirty(); Flush(); }
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
            backpack.Clear();
            progression.Clear();
            world.Clear();
            ClearProgressionContent();
            stats = new PlayerStats();
            wallet = new List<WalletEntry>();
            builders = new List<BuilderSlot>();
            atlas = new List<string>();
            settings = new List<SettingEntry>();
        }

        void StartNewGame()
        {
            ClearState();
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

        void Apply(SaveV2 save, SaveLoadReport report)
        {
            var warnings = report.warnings;
            ClearState();
            saveId = !string.IsNullOrEmpty(save.saveId) ? save.saveId : NewSaveId();
            createdWith = save.createdWith;
            wallet = save.wallet; builders = save.builders; atlas = save.atlas; settings = save.settings;

            ulong seed;
            if (SeedText.TryParse(createdWith.universeSeed, out seed) && seed != universe.Seed)
                warnings.Add("the save was created in another universe (seed " + createdWith.universeSeed + "); its worlds will look different");
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
                    if (db.FindStructure(record.defId) != null) return false;
                    warnings.Add("dropped the unknown structure '" + record.defId + "' on " + delta.planetId);
                    return true;
                });
            }
            world.Import(save.worlds);
            RecoverBuilderJobs(warnings);
            SeedAtlasFromExistingProgress();
            stats = save.stats ?? new PlayerStats();

            planet = universe.Get(save.location.currentPlanetId);
            if (planet == null)
            {
                if (!string.IsNullOrEmpty(save.location.currentPlanetId)) warnings.Add("the world '" + save.location.currentPlanetId + "' does not exist; returning home");
                planet = universe.Home;
            }
        }

        SaveV2 BuildSave()
        {
            var save = new SaveV2
            {
                saveId = saveId,
                createdWith = createdWith,
                wallet = wallet,
                builders = builders,
                atlas = atlas,
                settings = settings,
                stats = stats
            };
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
            return save;
        }

        /// <summary>Erases every save file and starts a new game on the home world.</summary>
        public void ResetProgress()
        {
            saves.DeleteAll();
            StartNewGame();
            Notify();
            Flush();
        }

        // ---------- Debug ----------
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
