using System;
using System.Diagnostics;

namespace Frontier
{
    /// <summary>
    /// Time source for everything that persists or debounces. <see cref="UtcNow"/> is wall-clock time (saved, shown, tamper-prone);
    /// <see cref="MonotonicSeconds"/> only ever grows and is used for debounce and play time. Tests inject <see cref="FakeClock"/>.
    /// </summary>
    public interface IClock
    {
        DateTime UtcNow { get; }

        /// <summary>Seconds since an arbitrary start; never decreases, unaffected by wall-clock changes.</summary>
        double MonotonicSeconds { get; }
    }

    /// <summary>The real clock: <see cref="DateTime.UtcNow"/> plus a <see cref="Stopwatch"/>.</summary>
    public sealed class SystemClock : IClock
    {
        readonly Stopwatch watch = Stopwatch.StartNew();

        public DateTime UtcNow { get { return DateTime.UtcNow; } }

        public double MonotonicSeconds { get { return watch.Elapsed.TotalSeconds; } }
    }

    /// <summary>
    /// Anchors UTC once at startup and advances it only by elapsed monotonic time. This protects active-session
    /// timers from device-date edits, but the startup anchor is still untrusted offline device time.
    /// </summary>
    public sealed class SessionClock : IClock
    {
        readonly IClock source;
        readonly DateTime startedUtc;
        readonly double startedSeconds;

        public SessionClock(IClock source)
        {
            this.source = source ?? throw new ArgumentNullException(nameof(source));
            startedSeconds = source.MonotonicSeconds;
            if (double.IsNaN(startedSeconds) || double.IsInfinity(startedSeconds))
                throw new InvalidOperationException("The session clock requires finite monotonic time.");
            startedUtc = DateTime.SpecifyKind(source.UtcNow, DateTimeKind.Utc);
        }

        public DateTime UtcNow
        {
            get
            {
                double elapsed = MonotonicSeconds - startedSeconds;
                return startedUtc.AddSeconds(elapsed);
            }
        }

        public double MonotonicSeconds
        {
            get
            {
                double now = source.MonotonicSeconds;
                if (double.IsNaN(now) || double.IsInfinity(now) || now < startedSeconds)
                    throw new InvalidOperationException("The session clock's monotonic time is invalid.");
                return now;
            }
        }
    }

    /// <summary>A manually driven clock for tests: time only moves when the test says so.</summary>
    public sealed class FakeClock : IClock
    {
        DateTime utc;
        double seconds;

        public FakeClock() : this(new DateTime(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc)) { }

        public FakeClock(DateTime startUtc)
        {
            utc = DateTime.SpecifyKind(startUtc, DateTimeKind.Utc);
        }

        public DateTime UtcNow { get { return utc; } }

        public double MonotonicSeconds { get { return seconds; } }

        /// <summary>Moves both clocks forward.</summary>
        public void Advance(double deltaSeconds)
        {
            if (deltaSeconds < 0.0) throw new ArgumentOutOfRangeException("deltaSeconds");
            seconds += deltaSeconds;
            utc = utc.AddSeconds(deltaSeconds);
        }

        /// <summary>Changes only the wall clock, like a player setting the device date (the monotonic clock is unaffected).</summary>
        public void SetUtc(DateTime value)
        {
            utc = DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }
    }

    /// <summary>Culture independent timestamp text for saves (ISO 8601, round trip format).</summary>
    public static class UtcText
    {
        public static string Format(DateTime utc)
        {
            return DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToString("o", System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>Parses text written by <see cref="Format"/>; false for null, empty or malformed text.</summary>
        public static bool TryParse(string text, out DateTime utc)
        {
            utc = default(DateTime);
            if (string.IsNullOrEmpty(text)) return false;
            DateTime parsed;
            if (!DateTime.TryParse(text, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out parsed)) return false;
            utc = DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
            return true;
        }
    }
}
