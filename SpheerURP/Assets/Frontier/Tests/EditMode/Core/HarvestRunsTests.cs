using System;
using NUnit.Framework;

namespace Frontier.Tests
{
    public class HarvestRunsTests
    {
        [TestCase(0f)]
        [TestCase(0.5f)]
        [TestCase(1f)]
        public void SiphonBot_CompletesWithCleanPumpsWithoutBypassingCooldown(float difficulty)
        {
            var run = new SiphonHarvestRun(difficulty, 1f);
            for (int frame = 0; frame < 60 * 90 && !run.Completed; frame++)
            {
                run.Advance(1f / 60f);
                if (Math.Abs(run.Needle - run.Target) < run.BandWidth * 0.2f)
                {
                    if (run.TryPump(out float hit))
                    {
                        Assert.GreaterOrEqual(hit, 0.9f);
                        Assert.IsFalse(run.TryPump(out _));
                    }
                }
            }
            Assert.IsTrue(run.Completed);
            Assert.AreEqual(SiphonHarvestRun.PulseCount, run.Pulses);
            Assert.AreEqual(run.Pulses, run.Perfects);
            Assert.That(run.Quality, Is.InRange(0.9f, 1f));
            float elapsed = run.Elapsed, quality = run.Quality;
            run.Advance(10f);
            Assert.AreEqual(elapsed, run.Elapsed);
            Assert.IsFalse(run.TryPump(out _));
            Assert.AreEqual(quality, run.Quality);
        }

        [Test]
        public void Siphon_MatchesExistingWindowAndQualityEquations()
        {
            var run = new SiphonHarvestRun(0.5f, 1.2f);
            float sum = 0f;
            int perfects = 0;
            for (int pulse = 0; pulse < SiphonHarvestRun.PulseCount; pulse++)
            {
                run.Advance(0.4f);
                float needle = 0.5f + 0.43f * (float)Math.Sin(run.Elapsed * 1.95f);
                float target = 0.5f + 0.31f * (float)Math.Sin(run.Elapsed * 0.83f + 1.1f);
                float expected = Math.Max(0f, Math.Min(1f, 1f - Math.Abs(needle - target) / (run.BandWidth * 2.2f)));
                Assert.IsTrue(run.TryPump(out float hit));
                Assert.AreEqual(expected, hit, 0.00001f);
                sum += expected;
                if (expected >= 0.78f) perfects++;
            }
            Assert.AreEqual(sum / SiphonHarvestRun.PulseCount, run.Quality, 0.00001f);
            Assert.AreEqual(perfects, run.Perfects);
        }

        [Test]
        public void Siphon_InvalidInputDoesNotMutateState()
        {
            foreach (float value in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
                Assert.Throws<ArgumentOutOfRangeException>(() => new SiphonHarvestRun(value, 1f));
            foreach (float value in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
                Assert.Throws<ArgumentOutOfRangeException>(() => new SiphonHarvestRun(0.5f, value));
            var run = new SiphonHarvestRun(0.5f, 1f);
            foreach (float value in new[] { -1f, float.NaN, float.PositiveInfinity })
                Assert.Throws<ArgumentOutOfRangeException>(() => run.Advance(value));
            Assert.AreEqual(0f, run.Elapsed);
            Assert.AreEqual(0, run.Pulses);
        }

        [Test]
        public void Excavation_RejectsDuplicatePings_AndReportsManhattanEcho()
        {
            var run = new ExcavationHarvestRun(42);
            int target = run.Target;
            int cell = (target + 1) % ExcavationHarvestRun.CellCount;
            Assert.IsTrue(run.TryPing(cell, out int distance));
            Assert.AreEqual(Math.Abs(cell / 3 - target / 3) + Math.Abs(cell % 3 - target % 3), distance);
            Assert.IsTrue(run.IsRevealed(cell));
            Assert.IsFalse(run.TryPing(cell, out _));
            Assert.AreEqual(1, run.Pings);
            Assert.IsTrue(run.TryPing(target, out distance));
            Assert.AreEqual(0, distance);
            Assert.AreEqual(1, run.SeamsFound);
            Assert.IsFalse(run.IsRevealed(cell), "Finding a seam resets all echoes for the next seam.");
        }

        [Test]
        public void Excavation_StopsAtScanBudget_AndUsesPartialYieldCurve()
        {
            var run = new ExcavationHarvestRun(7);
            run.TryPing(run.Target, out _);
            while (!run.Completed)
            {
                bool pinged = false;
                for (int cell = 0; cell < ExcavationHarvestRun.CellCount; cell++)
                {
                    if (cell == run.Target || run.IsRevealed(cell)) continue;
                    run.TryPing(cell, out _);
                    pinged = true;
                    break;
                }
                if (!pinged) run.TryPing(run.Target, out _);
            }
            Assert.AreEqual(ExcavationHarvestRun.MaxPings, run.Pings);
            Assert.AreEqual(2, run.SeamsFound);
            Assert.AreEqual(0.2f + 0.8f * 2f / 3f, run.Quality, 0.00001f);
            Assert.IsFalse(run.TryPing(run.Target, out _));
        }

        [Test]
        public void Excavation_EchoOnlyBotsCompleteAndReplayAcrossSeeds()
        {
            for (int seed = 0; seed < 64; seed++)
            {
                var run = new ExcavationHarvestRun(seed);
                var replay = new ExcavationHarvestRun(seed);
                DriveExcavationBot(run);
                DriveExcavationBot(replay);
                Assert.AreEqual(3, run.SeamsFound, "seed " + seed);
                Assert.AreEqual(1f, run.Quality);
                Assert.AreEqual(run.Pings, replay.Pings);
                Assert.AreEqual(run.Target, replay.Target);
            }
        }

        [Test]
        public void Excavation_InvalidCellsDoNotConsumeScans()
        {
            var run = new ExcavationHarvestRun(42);
            Assert.Throws<ArgumentOutOfRangeException>(() => run.TryPing(-1, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => run.TryPing(9, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => run.IsRevealed(9));
            Assert.AreEqual(0, run.Pings);
            Assert.AreEqual(0f, run.Quality);
        }

        [Test]
        public void Salvage_DuplicatesCannotConsumeCastsOrGrantExtraCaptures()
        {
            var run = new SalvageHarvestRun();
            for (int target = 0; target < SalvageHarvestRun.TargetCount; target++)
            {
                Assert.IsTrue(run.TryCast(target));
                Assert.IsTrue(run.IsCaptured(target));
                Assert.IsFalse(run.TryCast(target));
            }
            Assert.IsTrue(run.Completed);
            Assert.AreEqual(6, run.Casts);
            Assert.AreEqual(6, run.Captured);
            Assert.AreEqual(1f, run.Quality);
            Assert.IsFalse(run.TryCast(-1));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(3)]
        [TestCase(5)]
        public void Salvage_BoundedBotsPreservePartialAndZeroYield(int hits)
        {
            var run = new SalvageHarvestRun();
            for (int target = 0; target < hits; target++) run.TryCast(target);
            while (!run.Completed) Assert.IsTrue(run.TryCast(-1));
            Assert.AreEqual(SalvageHarvestRun.MaxCasts, run.Casts);
            Assert.AreEqual(hits, run.Captured);
            Assert.AreEqual(hits == 0 ? 0f : 0.2f + 0.8f * hits / 6f, run.Quality, 0.00001f);
            Assert.IsFalse(run.TryCast(hits));
        }

        [Test]
        public void Salvage_InvalidTargetsDoNotMutateState()
        {
            var run = new SalvageHarvestRun();
            Assert.Throws<ArgumentOutOfRangeException>(() => run.TryCast(-2));
            Assert.Throws<ArgumentOutOfRangeException>(() => run.TryCast(6));
            Assert.Throws<ArgumentOutOfRangeException>(() => run.IsCaptured(-1));
            Assert.AreEqual(0, run.Casts);
            Assert.AreEqual(0, run.Captured);
        }

        static void DriveExcavationBot(ExcavationHarvestRun run)
        {
            var candidates = new bool[9];
            for (int i = 0; i < candidates.Length; i++) candidates[i] = true;
            while (!run.Completed)
            {
                int cell = -1;
                for (int i = 0; i < candidates.Length; i++)
                    if (candidates[i]) { cell = i; break; }
                Assert.GreaterOrEqual(cell, 0, "Echo solver must retain a possible target.");
                Assert.IsTrue(run.TryPing(cell, out int distance));
                for (int i = 0; i < candidates.Length; i++)
                {
                    if (distance == 0) candidates[i] = true;
                    else candidates[i] &= i != cell && Math.Abs(i / 3 - cell / 3) + Math.Abs(i % 3 - cell % 3) == distance;
                }
            }
        }
    }
}
