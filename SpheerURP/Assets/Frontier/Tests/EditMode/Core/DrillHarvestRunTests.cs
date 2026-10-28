using System;
using NUnit.Framework;

namespace Frontier.Tests
{
    public class DrillHarvestRunTests
    {
        [Test]
        public void IdleDrill_CannotEarnProgress()
        {
            var run = new DrillHarvestRun(1f, 0.5f, 1f);
            for (int frame = 0; frame < 600; frame++) run.Advance(1f / 60f, false);
            Assert.AreEqual(0f, run.Progress);
            Assert.AreEqual(0f, run.InBandSeconds);
            Assert.IsFalse(run.Contact);
            Assert.IsFalse(run.Completed);
            Assert.AreEqual(0.3f, run.Quality);
        }

        [Test]
        public void Contact_RequiresBitToReachOre_AndReleaseStopsContactImmediately()
        {
            var run = new DrillHarvestRun(1f, 0.5f, 10f);
            run.Advance(1f / 60f, true);
            Assert.IsFalse(run.Contact);
            Assert.AreEqual(0f, run.Progress);
            for (int frame = 0; frame < 30; frame++) run.Advance(1f / 60f, true);
            Assert.IsTrue(run.Contact);
            Assert.Greater(run.Progress, 0f);
            float progress = run.Progress;
            run.Advance(1f / 60f, false);
            Assert.IsFalse(run.Contact);
            Assert.AreEqual(progress, run.Progress);
        }

        [TestCase(0f)]
        [TestCase(0.5f)]
        [TestCase(1f)]
        public void PressureBot_CompletesAndReplaysAtEveryDifficulty(float difficulty)
        {
            var run = new DrillHarvestRun(1f, difficulty, 1f);
            var replay = new DrillHarvestRun(1f, difficulty, 1f);
            DriveBot(run);
            DriveBot(replay);
            Assert.AreEqual(run.Elapsed, replay.Elapsed);
            Assert.AreEqual(run.InBandSeconds, replay.InBandSeconds);
            Assert.AreEqual(run.Quality, replay.Quality);
            Assert.That(run.Quality, Is.InRange(0.3f, 1f));
            Assert.That(run.Progress, Is.EqualTo(1f));
            Assert.That(run.Pressure, Is.InRange(0f, 1f));
        }

        [Test]
        public void BetterDrill_ReducesProductiveSecondsRequired()
        {
            var starter = new DrillHarvestRun(1f, 0.5f, 1f);
            var upgraded = new DrillHarvestRun(4f, 0.5f, 1f);
            DriveBot(starter);
            DriveBot(upgraded);
            Assert.Less(upgraded.Elapsed, starter.Elapsed);
            Assert.AreEqual(starter.InBandSeconds / 4f, upgraded.InBandSeconds, 0.04f);
        }

        [Test]
        public void CompletedRun_IsFrozen()
        {
            var run = new DrillHarvestRun(4f, 0.5f, 1f);
            DriveBot(run);
            float time = run.Elapsed, pressure = run.Pressure, quality = run.Quality;
            for (int frame = 0; frame < 600; frame++) run.Advance(1f / 60f, frame % 2 == 0);
            Assert.AreEqual(time, run.Elapsed);
            Assert.AreEqual(pressure, run.Pressure);
            Assert.AreEqual(quality, run.Quality);
        }

        [Test]
        public void Simulation_MatchesOriginalPressureAndProgressEquations()
        {
            const float dt = 1f / 60f, difficulty = 0.5f, tolerance = 1f, damage = 2f;
            var run = new DrillHarvestRun(damage, difficulty, tolerance);
            float elapsed = 0f, pressure = 0.1f, velocity = 0f, progress = 0f, inBand = 0f;
            float tipY = 0.2f + 0.95f * 0.9f + 1.5f;
            for (int frame = 0; frame < 600 && !run.Completed; frame++)
            {
                bool press = frame % 100 < 60;
                elapsed += dt;
                velocity += ((press ? 0.7f : -0.8f) - velocity) * 5f * dt;
                pressure = Clamp01(pressure + velocity * dt);
                if (pressure <= 0f || pressure >= 1f) velocity = 0f;
                float center = 0.5f + 0.3f * (float)Math.Sin(elapsed * (0.6f + difficulty))
                    + 0.08f * (float)Math.Sin(elapsed * 2.1f);
                bool inside = Math.Abs(pressure - center) <= (0.16f - 0.09f * difficulty) * tolerance;
                float top = 0.2f + 0.95f * 0.9f * (1f - 0.55f * Clamp01(progress));
                tipY += ((press ? top - 0.08f : top + 1.5f) - tipY)
                    * (1f - (float)Math.Exp(-(press ? 22f : 7f) * dt));
                bool contact = press && tipY <= top + 0.04f;
                if (contact && inside)
                {
                    inBand += dt;
                    progress += dt * 0.11f * damage / (0.6f + difficulty);
                }
                run.Advance(dt, press);
                Assert.AreEqual(pressure, run.Pressure, 0.00001f);
                Assert.AreEqual(tipY, run.TipY, 0.00001f);
                Assert.AreEqual(contact, run.Contact);
                Assert.AreEqual(Clamp01(progress), run.Progress, 0.00001f);
                Assert.AreEqual(inBand, run.InBandSeconds, 0.00001f);
            }
        }

        [Test]
        public void InvalidInputs_AreRejected()
        {
            foreach (float value in new[] { -1f, 0f, float.NaN, float.PositiveInfinity })
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => new DrillHarvestRun(value, 0.5f, 1f));
                Assert.Throws<ArgumentOutOfRangeException>(() => new DrillHarvestRun(1f, 0.5f, value));
            }
            Assert.Throws<ArgumentOutOfRangeException>(() => new DrillHarvestRun(1f, float.NaN, 1f));
            var run = new DrillHarvestRun(1f, 0.5f, 1f);
            foreach (float value in new[] { -1f, float.NaN, float.PositiveInfinity })
                Assert.Throws<ArgumentOutOfRangeException>(() => run.Advance(value, true));
            Assert.AreEqual(0f, run.Elapsed);
            run.Advance(0f, true);
            Assert.AreEqual(0f, run.Elapsed);
        }

        static void DriveBot(DrillHarvestRun run)
        {
            for (int frame = 0; frame < 60 * 180 && !run.Completed; frame++)
            {
                // Anticipate the pressure needle's inertia instead of oscillating at the band edge.
                bool press = run.Pressure < run.BandCenter - 0.02f;
                run.Advance(1f / 60f, press);
            }
            Assert.IsTrue(run.Completed, "Pressure bot must finish within three simulated minutes.");
        }

        static float Clamp01(float value) { return Math.Max(0f, Math.Min(1f, value)); }
    }
}
