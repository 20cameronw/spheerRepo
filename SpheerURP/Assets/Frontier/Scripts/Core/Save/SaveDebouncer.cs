using System;

namespace Frontier
{
    /// <summary>
    /// Decides when a dirty game state is written: after <see cref="QuietSeconds"/> without further changes, but never later than
    /// <see cref="MaxDelaySeconds"/> after the first unsaved change (a player who taps continuously must not postpone saving forever).
    /// Pure logic over a monotonic clock value, so it is tested with a fake clock.
    /// </summary>
    public sealed class SaveDebouncer
    {
        public readonly double QuietSeconds;
        public readonly double MaxDelaySeconds;

        bool dirty;
        double firstDirtyAt, lastDirtyAt;

        public SaveDebouncer() : this(1.0, 10.0) { }

        public SaveDebouncer(double quietSeconds, double maxDelaySeconds)
        {
            if (quietSeconds < 0.0 || maxDelaySeconds < quietSeconds) throw new ArgumentException("max delay must be at least the quiet time");
            QuietSeconds = quietSeconds;
            MaxDelaySeconds = maxDelaySeconds;
        }

        public bool IsDirty { get { return dirty; } }

        /// <summary>Notes a change at monotonic time <paramref name="now"/>.</summary>
        public void MarkDirty(double now)
        {
            if (!dirty)
            {
                dirty = true;
                firstDirtyAt = now;
            }
            lastDirtyAt = now;
        }

        /// <summary>True when a write is due at monotonic time <paramref name="now"/>.</summary>
        public bool ShouldFlush(double now)
        {
            return dirty && (now - lastDirtyAt >= QuietSeconds || now - firstDirtyAt >= MaxDelaySeconds);
        }

        /// <summary>Call after the state was written (or intentionally discarded).</summary>
        public void Flushed() { dirty = false; }
    }
}
