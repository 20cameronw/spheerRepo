using System;
using NUnit.Framework;

namespace Frontier.Tests
{
    public class HarvestRunsTests
    {
        [Test]
        public void ChopTiming_IsDeterministicForgivingAndAffectedByToolsAndDifficulty()
        {
            var run = new NodeHarvestRun(1f, 0.5f);
            var replay = new NodeHarvestRun(1f, 0.5f);
            Assert.AreEqual(HarvestStrike.Perfect, run.TimingStrike);
            run.Advance(0.1f); replay.Advance(0.1f);
            Assert.AreEqual(HarvestStrike.WeakSpot, run.TimingStrike);
            run.Advance(0.5f); replay.Advance(0.5f);
            Assert.AreEqual(HarvestStrike.Miss, run.TimingStrike);
            Assert.AreEqual(run.Marker, replay.Marker);
            Assert.Greater(new NodeHarvestRun(3f, 0.5f, 1.4f).SweetHalfWidth, run.SweetHalfWidth);
            var easy = new NodeHarvestRun(1f, 0f);
            var hard = new NodeHarvestRun(1f, 1f);
            easy.Advance(0.2f); hard.Advance(0.2f);
            Assert.Greater(hard.Marker, easy.Marker);
            for (int frame = 0; frame < 60 * 90 && !run.Completed; frame++)
            {
                run.Advance(1f / 60f);
                if (run.TimingStrike == HarvestStrike.Perfect) run.TryStrike(run.TimingStrike);
            }
            Assert.IsTrue(run.Completed);
            Assert.AreEqual(1f, run.Quality);
            Assert.AreEqual(run.Swings, run.Perfects);
        }

        [Test]
        public void DrillBoost_MeasurablyAcceleratesDepthThenOverheatsAndRecovers()
        {
            var normal = new DrillHarvestRun(0.5f, 0.5f, 10f);
            var boosted = new DrillHarvestRun(0.5f, 0.5f, 10f);
            normal.Advance(2f, true);
            boosted.Advance(2f, true, true);
            Assert.AreEqual(2.2f, boosted.Progress / normal.Progress, 0.02f);
            Assert.Less(boosted.TipY, normal.TipY, "The real bit must follow accelerated depth.");
            Assert.AreEqual(0.56f, boosted.Heat, 0.01f);
            TestContext.WriteLine("2s normal progress={0:F4}, boost={1:F4}; tip normal={2:F3}, boost={3:F3}",
                normal.Progress, boosted.Progress, normal.TipY, boosted.TipY);
            boosted.Advance(2f, true, true);
            Assert.IsTrue(boosted.Overheated);
            float depth = boosted.Progress;
            boosted.Advance(0.1f, true, true);
            Assert.AreEqual(depth, boosted.Progress, "Overheat pauses cutting, not earned progress.");
            boosted.Advance(2f, false, false);
            Assert.IsFalse(boosted.Overheated);
            Assert.Less(boosted.Heat, 0.35f);
            boosted.Advance(0.5f, true, true);
            Assert.IsTrue(boosted.Boosting);
            Assert.Greater(boosted.Progress, depth);
            Assert.That(boosted.Quality, Is.InRange(0f, 1f));
            var idle = new DrillHarvestRun(1f, 0.5f, 1f);
            idle.Advance(100f, false);
            Assert.IsTrue(idle.Completed);
            Assert.AreEqual(0f, idle.Quality, "A timed-out idle run earns no harvest quality.");
        }

        [Test]
        public void VeinBrake_HalvesRealRowSpeed_AndRichChainRepairsWithoutExtraOre()
        {
            var normal = new VeinHarvestRun(42, 0.5f, 1f);
            var braking = new VeinHarvestRun(42, 0.5f, 1f);
            normal.Advance(0.5f); braking.Advance(0.5f, true);
            Assert.AreEqual(normal.Progress * 0.5f, braking.Progress, 0.0001f);
            var run = new VeinHarvestRun(42, 0.5f, 1f);
            int dark = -1;
            for (int col = 0; col < VeinHarvestRun.Columns; col++)
                if (run.Cell(0, col) < 0) { dark = col; break; }
            Assert.GreaterOrEqual(dark, 0);
            while (run.Column != dark) run.Steer(dark - run.Column);
            run.Advance(1.41f);
            Assert.AreEqual(3, run.Integrity);
            for (int row = 1; row <= 3; row++)
            {
                int rich = 0;
                while (run.Cell(row, rich) != 3) rich++;
                while (run.Column != rich) run.Steer(rich - run.Column);
                run.Advance(1.4f);
            }
            Assert.AreEqual(4, run.Integrity);
            Assert.AreEqual(3, run.RichChain);
            Assert.AreEqual(9, run.Ore, "Chain repairs survival; it never multiplies ore.");
        }

        [Test]
        public void SiphonVenting_RecoversPressureAndBlocksSimultaneousPumping()
        {
            var run = new SiphonHarvestRun(0.5f, 1f);
            run.TryPump(out _);
            run.Advance(0.3f);
            run.TryPump(out _);
            float pressure = run.Pressure, band = run.EffectiveBand;
            run.Advance(0.5f, true);
            Assert.AreEqual(pressure - 0.25f, run.Pressure, 0.0001f);
            Assert.Greater(run.EffectiveBand, band);
            Assert.IsFalse(run.TryPump(out _));
            Assert.AreEqual(2, run.Pulses);
            run.Advance(0f, false);
            Assert.IsTrue(run.TryPump(out _));
        }

        [Test]
        public void ExcavationDirection_IsActionableAcrossSeedsAndGridCells()
        {
            for (int seed = 0; seed < 32; seed++)
                for (int cell = 0; cell < 9; cell++)
                {
                    var run = new ExcavationHarvestRun(seed);
                    int target = run.Target;
                    run.TryPing(cell, out int distance);
                    Assert.AreEqual(Math.Sign(target % 3 - cell % 3), run.HintX);
                    Assert.AreEqual(Math.Sign(target / 3 - cell / 3), run.HintY);
                    Assert.AreEqual(Math.Abs(target / 3 - cell / 3) + Math.Abs(target % 3 - cell % 3), distance);
                }
        }

        [Test]
        public void SalvageTether_EarlyReadyLateAndCancelledAttemptsPreserveCastBudget()
        {
            var run = new SalvageHarvestRun();
            Assert.IsTrue(run.Select(0));
            run.Advance(0.3f); Assert.IsFalse(run.TetherReady);
            Assert.IsTrue(run.Release()); Assert.AreEqual(0, run.Captured);
            run.Select(0); run.Advance(0.8f); Assert.IsTrue(run.TetherReady);
            Assert.IsTrue(run.Release()); Assert.AreEqual(1, run.Captured);
            Assert.IsFalse(run.Release(), "Release is exactly once.");
            Assert.IsFalse(run.Select(0));
            run.Select(1); run.Advance(1.5f); Assert.IsFalse(run.TetherReady);
            run.Release(); Assert.AreEqual(1, run.Captured);
            run.Select(1); run.Advance(0.8f); run.CancelTether();
            Assert.IsFalse(run.Release()); Assert.AreEqual(3, run.Casts);
            Assert.Throws<ArgumentOutOfRangeException>(() => run.Advance(float.NaN));
        }

        [TestCase(0f)]
        [TestCase(0.5f)]
        [TestCase(1f)]
        public void SiphonBot_CompletesWithCleanPumpsWithoutBypassingCooldown(float difficulty)
        {
            var run = new SiphonHarvestRun(difficulty, 1f);
            for (int frame = 0; frame < 60 * 90 && !run.Completed; frame++)
            {
                run.Advance(1f / 60f);
                if (Math.Abs(run.Needle - run.Target) < run.EffectiveBand * 0.2f)
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
        public void Siphon_MatchesPressureWindowAndUnchangedQualityAverage()
        {
            var run = new SiphonHarvestRun(0.5f, 1.2f);
            float sum = 0f;
            int perfects = 0;
            for (int pulse = 0; pulse < SiphonHarvestRun.PulseCount; pulse++)
            {
                run.Advance(0.4f);
                float needle = 0.5f + 0.43f * (float)Math.Sin(run.Elapsed * 1.95f);
                float target = 0.5f + 0.31f * (float)Math.Sin(run.Elapsed * 0.83f + 1.1f);
                float expected = Math.Max(0f, Math.Min(1f, 1f - Math.Abs(needle - target) / (run.EffectiveBand * 2.2f)));
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
