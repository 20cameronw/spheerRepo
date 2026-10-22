using System;
using NUnit.Framework;

namespace Frontier.Tests
{
    public class NodeHarvestRunTests
    {
        [TestCase(HarvestStrike.Miss, 0f, 0, 0)]
        [TestCase(HarvestStrike.Glance, 0.15f, 0, 0)]
        [TestCase(HarvestStrike.WeakSpot, 1f, 1, 0)]
        [TestCase(HarvestStrike.Perfect, 1.7f, 1, 1)]
        public void Strike_PreservesDamageAndAccuracyRules(HarvestStrike strike, float damage, int hits, int perfects)
        {
            var run = new NodeHarvestRun(1f, 0.5f);
            Assert.IsTrue(run.TryStrike(strike));
            Assert.AreEqual(3.6f - damage, run.Health, 0.00001f);
            Assert.AreEqual(1, run.Swings);
            Assert.AreEqual(hits, run.WeakSpotHits);
            Assert.AreEqual(perfects, run.Perfects);
            Assert.AreEqual(0.3f + 0.7f * hits, run.Quality, 0.00001f);
            Assert.AreEqual(0.55f, run.Cooldown, 0.00001f);
        }

        [Test]
        public void Cooldown_RejectsSpamWithoutChangingHealthOrScore()
        {
            var run = new NodeHarvestRun(1f, 1f);
            run.TryStrike(HarvestStrike.Perfect);
            float health = run.Health;
            for (int i = 0; i < 100; i++) Assert.IsFalse(run.TryStrike(HarvestStrike.Perfect));
            run.Advance(0.2f);
            Assert.IsFalse(run.TryStrike(HarvestStrike.Miss));
            Assert.AreEqual(health, run.Health);
            Assert.AreEqual(1, run.Swings);
            Assert.AreEqual(1, run.Perfects);
            run.Advance(run.Cooldown);
            Assert.IsTrue(run.TryStrike(HarvestStrike.WeakSpot));
            Assert.AreEqual(2, run.Swings);
        }

        [Test]
        public void Overkill_BreaksOneStagePerSwing_AndCannotRewardAgainAfterCompletion()
        {
            var run = new NodeHarvestRun(100f, 0f);
            for (int stage = 1; stage <= NodeHarvestRun.StageCount; stage++)
            {
                Assert.IsTrue(run.TryStrike(HarvestStrike.Perfect));
                Assert.AreEqual(stage, run.CompletedStages);
                Assert.AreEqual(stage == NodeHarvestRun.StageCount ? 0f : run.StageHealth, run.Health);
                run.Advance(run.SwingSeconds);
            }
            Assert.IsTrue(run.Completed);
            Assert.AreEqual(1f, run.Quality);
            Assert.IsFalse(run.TryStrike(HarvestStrike.Perfect));
            Assert.AreEqual(NodeHarvestRun.StageCount, run.Swings);
        }

        [TestCase(0f)]
        [TestCase(0.25f)]
        [TestCase(0.5f)]
        [TestCase(1f)]
        public void PerfectBots_BetterToolsFinishFasterAtEveryDifficulty(float difficulty)
        {
            int previousSwings = int.MaxValue;
            float previousSeconds = float.MaxValue;
            foreach (float damage in new[] { 1f, 2f, 4f, 8f, 16f })
            {
                var run = new NodeHarvestRun(damage, difficulty);
                DriveBot(run, 42, true);
                Assert.LessOrEqual(run.Swings, previousSwings);
                float seconds = (run.Swings - 1) * run.SwingSeconds;
                Assert.Less(seconds, previousSeconds);
                Assert.AreEqual(1f, run.Quality);
                Assert.AreEqual(run.Swings, run.Perfects);
                previousSwings = run.Swings;
                previousSeconds = seconds;
            }
        }

        [Test]
        public void SeededBots_ReplayIdenticallyAcrossToolsAndDifficulty()
        {
            for (int seed = 0; seed < 32; seed++)
            {
                float damage = 1f + seed % 8;
                float difficulty = (seed % 11) / 10f;
                var first = new NodeHarvestRun(damage, difficulty);
                var replay = new NodeHarvestRun(damage, difficulty);
                DriveBot(first, seed, false);
                DriveBot(replay, seed, false);
                Assert.AreEqual(first.Swings, replay.Swings);
                Assert.AreEqual(first.Perfects, replay.Perfects);
                Assert.AreEqual(first.Quality, replay.Quality);
                Assert.That(first.Quality, Is.InRange(0.3f, 1f));
                Assert.AreEqual(0.3f + 0.7f * first.WeakSpotHits / first.Swings, first.Quality, 0.00001f);
            }
        }

        [Test]
        public void Difficulty_ClampsLikeTheRenderedGame()
        {
            Assert.AreEqual(2.6f, new NodeHarvestRun(1f, -1f).StageHealth);
            Assert.AreEqual(4.6f, new NodeHarvestRun(1f, 2f).StageHealth);
        }

        [Test]
        public void InvalidInputs_AreRejectedWithoutMutatingRun()
        {
            foreach (float value in new[] { 0f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
                Assert.Throws<ArgumentOutOfRangeException>(() => new NodeHarvestRun(value, 0.5f));
            foreach (float value in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
                Assert.Throws<ArgumentOutOfRangeException>(() => new NodeHarvestRun(1f, value));

            var run = new NodeHarvestRun(1f, 0.5f);
            foreach (float value in new[] { -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
                Assert.Throws<ArgumentOutOfRangeException>(() => run.Advance(value));
            Assert.Throws<ArgumentOutOfRangeException>(() => run.TryStrike((HarvestStrike)3));
            Assert.Throws<ArgumentOutOfRangeException>(() => run.TryStrike((HarvestStrike)(-2)));
            Assert.AreEqual(0, run.Swings);
            Assert.AreEqual(run.StageHealth, run.Health);
        }

        static void DriveBot(NodeHarvestRun run, int seed, bool perfect)
        {
            var random = new Random(seed);
            for (int attempts = 0; attempts < 1000 && !run.Completed; attempts++)
            {
                var strike = perfect ? HarvestStrike.Perfect : (HarvestStrike)random.Next(-1, 3);
                Assert.IsTrue(run.TryStrike(strike));
                run.Advance(run.SwingSeconds);
            }
            Assert.IsTrue(run.Completed, "Bot must finish within the bounded action budget.");
        }
    }
}
