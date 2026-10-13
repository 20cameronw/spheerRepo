using System;
using UnityEngine;

namespace Frontier
{
    /// <summary>
    /// Persistence orchestration for the game state: owns the <see cref="SaveStore"/> (atomic writes, rolling backup, recovery) and the
    /// <see cref="SaveDebouncer"/>. Changes only mark the state dirty; <see cref="Tick"/> (called every frame by the app) writes after
    /// a second of quiet, and <see cref="Flush"/> writes immediately (app pause, focus loss, quit, travel, purchases). The state to write
    /// comes from a snapshot function, so this class knows nothing about inventories or planets. All time comes from an <see cref="IClock"/>,
    /// which makes the debounce testable without waiting.
    /// </summary>
    public sealed class SaveService
    {
        /// <summary>After a failed write the next attempt waits this long (no retry storm on a full disk).</summary>
        public const double RetrySeconds = 5.0;

        readonly SaveStore store;
        readonly SaveDebouncer debouncer;
        readonly IClock clock;
        readonly Func<SaveV2> snapshot;
        double retryAt = double.NegativeInfinity;

        public SaveService(string directory, IClock clock, Func<SaveV2> snapshot, ISaveCodec codec = null, SaveDebouncer debouncer = null)
        {
            if (clock == null) throw new ArgumentNullException("clock");
            if (snapshot == null) throw new ArgumentNullException("snapshot");
            this.clock = clock;
            this.snapshot = snapshot;
            this.debouncer = debouncer ?? new SaveDebouncer();
            store = new SaveStore(directory, codec ?? SaveSerializer.Instance);
        }

        public SaveStore Store { get { return store; } }

        /// <summary>What the last <see cref="Load"/> found.</summary>
        public SaveLoadReport LastLoadReport { get; private set; }

        /// <summary>True while there are changes that have not been written.</summary>
        public bool IsDirty { get { return debouncer.IsDirty; } }

        /// <summary>A write is due this long after the last change (the quiet period).</summary>
        public double QuietSeconds { get { return debouncer.QuietSeconds; } }

        /// <summary>A write is due at the latest this long after the first unsaved change, however busy the player is.</summary>
        public double MaxDelaySeconds { get { return debouncer.MaxDelaySeconds; } }

        /// <summary>Number of successful writes since this service was created.</summary>
        public int WriteCount { get; private set; }

        /// <summary>The error of the last failed write, or the reason the save is read-only; null when all is well.</summary>
        public string LastError { get { return store.ReadOnly ? store.ReadOnlyReason : store.LastWriteError; } }

        public SaveLoadResult Load(SaveMigrationFunc migrate)
        {
            var result = store.Load(migrate);
            LastLoadReport = result.report;
            return result;
        }

        /// <summary>Notes that the state changed. Nothing is written yet.</summary>
        public void MarkDirty() { debouncer.MarkDirty(clock.MonotonicSeconds); }

        /// <summary>Call every frame: writes when the debounce says the state has been quiet long enough. Returns true when a file was written.</summary>
        public bool Tick()
        {
            double now = clock.MonotonicSeconds;
            if (!debouncer.ShouldFlush(now) || now < retryAt) return false;
            return Write();
        }

        /// <summary>Writes now when there are unsaved changes (the lifecycle hooks call this). Returns true when a file was written.</summary>
        public bool Flush()
        {
            if (!debouncer.IsDirty) return false;
            return Write();
        }

        /// <summary>Writes now whether or not anything changed (tests, the harness, explicit saves).</summary>
        public bool Save() { return Write(); }

        bool Write()
        {
            if (store.ReadOnly)
            {
                debouncer.Flushed();
                return false;
            }
            bool ok;
            try { ok = store.Write(snapshot()); }
            catch (Exception e)
            {
                Debug.LogWarning("Frontier save failed: " + e.Message);
                retryAt = clock.MonotonicSeconds + RetrySeconds;
                return false;
            }
            if (ok)
            {
                debouncer.Flushed();
                WriteCount++;
                retryAt = double.NegativeInfinity;
                return true;
            }
            Debug.LogWarning("Frontier save failed: " + store.LastWriteError);
            retryAt = clock.MonotonicSeconds + RetrySeconds;
            return false;
        }

        /// <summary>Deletes every save file (a player-requested reset) and forgets pending changes.</summary>
        public void DeleteAll()
        {
            store.DeleteAll();
            debouncer.Flushed();
            retryAt = double.NegativeInfinity;
        }
    }
}
