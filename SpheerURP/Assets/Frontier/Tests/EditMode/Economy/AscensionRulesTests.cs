using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Frontier.Economy.Tests
{
    public class AscensionRulesTests
    {
        [TestCase(-1, 1f)]
        [TestCase(0, 1f)]
        [TestCase(1, 1.05f)]
        [TestCase(10, 1.5f)]
        [TestCase(int.MaxValue, 1.5f)]
        public void LegacyHarvest_IsBounded_AndNeverChangesUnascendedPacing(int points, float expected)
        {
            Assert.AreEqual(expected, new AscensionRules().HarvestMultiplier(points), 0.00001f);
        }

        [Test]
        public void NewSeeds_AreStableDistinct_AndIncludePreviousUniverseAndRank()
        {
            var seen = new HashSet<ulong>();
            ulong seed = 1;
            for (int rank = 1; rank <= 1000; rank++)
            {
                var next = AscensionRules.NextSeed(seed, rank);
                Assert.AreEqual(next, AscensionRules.NextSeed(seed, rank));
                Assert.AreNotEqual(seed, next);
                Assert.IsTrue(seen.Add(next));
                seed = next;
            }
            Assert.AreNotEqual(AscensionRules.NextSeed(1, 1), AscensionRules.NextSeed(2, 1));
            Assert.AreNotEqual(AscensionRules.NextSeed(1, 1), AscensionRules.NextSeed(1, 2));
            Assert.Throws<ArgumentOutOfRangeException>(() => AscensionRules.NextSeed(1, 0));
        }

        [Test]
        public void InvalidOrUnboundedRules_AreRejected()
        {
            var rules = new AscensionRules { harvestBonusPerPoint = float.NaN };
            Assert.IsFalse(rules.IsValid);
            Assert.Throws<InvalidOperationException>(() => rules.HarvestMultiplier(1));
            rules.harvestBonusPerPoint = 0.05f;
            rules.maxHarvestBonus = 1;
            Assert.IsFalse(rules.IsValid);
            rules.maxHarvestBonus = 0.5f;
            rules.requiredProjectStage = 1;
            Assert.IsFalse(rules.IsValid);
        }
    }
}
