using System;
using NUnit.Framework;

namespace Frontier.Tests
{
    /// <summary>Debounced saving, driven by a fake clock: nothing is written while changes keep coming, then once after a quiet second.</summary>
    [TestFixture]
    public class SaveDebouncerTests
    {
        [Test]
        public void Clean_NeverFlushes()
        {
            var d = new SaveDebouncer();
            var clock = new FakeClock();
            Assert.IsFalse(d.IsDirty);
            clock.Advance(1000);
            Assert.IsFalse(d.ShouldFlush(clock.MonotonicSeconds));
        }

        [Test]
        public void FlushesAfterOneQuietSecond_NotBefore()
        {
            var d = new SaveDebouncer();
            var clock = new FakeClock();
            d.MarkDirty(clock.MonotonicSeconds);
            clock.Advance(0.99);
            Assert.IsFalse(d.ShouldFlush(clock.MonotonicSeconds), "still inside the quiet window");
            clock.Advance(0.01);
            Assert.IsTrue(d.ShouldFlush(clock.MonotonicSeconds));
            d.Flushed();
            Assert.IsFalse(d.IsDirty);
            Assert.IsFalse(d.ShouldFlush(clock.MonotonicSeconds + 100.0));
        }

        [Test]
        public void EveryNewChange_RestartsTheQuietWindow()
        {
            var d = new SaveDebouncer();
            var clock = new FakeClock();
            d.MarkDirty(clock.MonotonicSeconds);
            for (int i = 0; i < 8; i++)
            {
                clock.Advance(0.5);
                Assert.IsFalse(d.ShouldFlush(clock.MonotonicSeconds), "a change every half second never lets the window close (step " + i + ")");
                d.MarkDirty(clock.MonotonicSeconds);
            }
            clock.Advance(1.0);
            Assert.IsTrue(d.ShouldFlush(clock.MonotonicSeconds));
        }

        [Test]
        public void ContinuousChanges_StillFlushAfterTheMaximumDelay()
        {
            var d = new SaveDebouncer(1.0, 10.0);
            var clock = new FakeClock();
            d.MarkDirty(clock.MonotonicSeconds);
            bool flushed = false;
            double flushedAt = 0.0;
            for (int i = 0; i < 100 && !flushed; i++)
            {
                clock.Advance(0.2);
                if (d.ShouldFlush(clock.MonotonicSeconds)) { flushed = true; flushedAt = clock.MonotonicSeconds; }
                else d.MarkDirty(clock.MonotonicSeconds);
            }
            Assert.IsTrue(flushed, "saving must not be postponed forever");
            Assert.AreEqual(10.0, flushedAt, 0.21);
        }

        [Test]
        public void ChangesAfterAFlush_StartANewWindow()
        {
            var d = new SaveDebouncer();
            var clock = new FakeClock();
            d.MarkDirty(clock.MonotonicSeconds);
            clock.Advance(1.5);
            Assert.IsTrue(d.ShouldFlush(clock.MonotonicSeconds));
            d.Flushed();
            clock.Advance(5.0);
            d.MarkDirty(clock.MonotonicSeconds);
            Assert.IsFalse(d.ShouldFlush(clock.MonotonicSeconds));
            clock.Advance(1.0);
            Assert.IsTrue(d.ShouldFlush(clock.MonotonicSeconds));
        }

        [Test]
        public void InvalidSettings_AreRejected()
        {
            Assert.Throws<ArgumentException>(() => new SaveDebouncer(2.0, 1.0));
            Assert.Throws<ArgumentException>(() => new SaveDebouncer(-1.0, 1.0));
        }
    }

    [TestFixture]
    public class ClockTests
    {
        [Test]
        public void FakeClock_MovesBothClocksTogether_AndWallClockEditsDoNotTouchTheMonotonicOne()
        {
            var c = new FakeClock(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            c.Advance(90);
            Assert.AreEqual(90.0, c.MonotonicSeconds, 1e-9);
            Assert.AreEqual(new DateTime(2026, 1, 1, 0, 1, 30, DateTimeKind.Utc), c.UtcNow);
            c.SetUtc(new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Unspecified));
            Assert.AreEqual(DateTimeKind.Utc, c.UtcNow.Kind);
            Assert.AreEqual(90.0, c.MonotonicSeconds, 1e-9, "setting the device date does not rewind monotonic time");
            Assert.Throws<ArgumentOutOfRangeException>(() => c.Advance(-1));
        }

        [Test]
        public void SystemClock_IsMonotonic_AndUtc()
        {
            var c = new SystemClock();
            double a = c.MonotonicSeconds;
            double b = c.MonotonicSeconds;
            Assert.GreaterOrEqual(b, a);
            Assert.AreEqual(DateTimeKind.Utc, c.UtcNow.Kind);
            Assert.Less(Math.Abs((DateTime.UtcNow - c.UtcNow).TotalSeconds), 5.0);
        }

        [Test]
        public void SessionClock_AdvancesFromMonotonicTimeAndIgnoresDeviceDateEdits()
        {
            var raw = new FakeClock();
            raw.Advance(20);
            var started = raw.UtcNow;
            var session = new SessionClock(raw);
            Assert.AreEqual(started, session.UtcNow);
            Assert.AreEqual(20, session.MonotonicSeconds);
            raw.SetUtc(started.AddYears(10));
            Assert.AreEqual(started, session.UtcNow, "A forward date edit must not finish active jobs.");
            raw.Advance(15.5);
            Assert.AreEqual(started.AddSeconds(15.5), session.UtcNow);
            raw.SetUtc(started.AddYears(-10));
            Assert.AreEqual(started.AddSeconds(15.5), session.UtcNow, "A rewind must not extend active jobs.");
            raw.Advance(30);
            Assert.AreEqual(started.AddSeconds(45.5), session.UtcNow);
            Assert.AreEqual(DateTimeKind.Utc, session.UtcNow.Kind);
        }

        [Test]
        public void SessionClock_RestartUsesANewDeviceAnchorAndIsNotServerTime()
        {
            var raw = new FakeClock();
            var original = new SessionClock(raw);
            raw.SetUtc(raw.UtcNow.AddDays(1));
            var restarted = new SessionClock(raw);
            Assert.AreEqual(original.UtcNow.AddDays(1), restarted.UtcNow,
                "Session protection must not be represented as offline or cross-device authority.");
            Assert.Throws<ArgumentNullException>(() => new SessionClock(null));
        }

        [Test]
        public void UtcText_RoundTripsExactlyAndRejectsGarbage()
        {
            var t = new DateTime(2026, 10, 13, 20, 15, 3, 456, DateTimeKind.Utc).AddTicks(1234);
            string text = UtcText.Format(t);
            StringAssert.EndsWith("Z", text);
            DateTime back;
            Assert.IsTrue(UtcText.TryParse(text, out back));
            Assert.AreEqual(t, back);
            Assert.AreEqual(DateTimeKind.Utc, back.Kind);
            Assert.IsFalse(UtcText.TryParse("", out back));
            Assert.IsFalse(UtcText.TryParse(null, out back));
            Assert.IsFalse(UtcText.TryParse("yesterday-ish", out back));
        }
    }
}
