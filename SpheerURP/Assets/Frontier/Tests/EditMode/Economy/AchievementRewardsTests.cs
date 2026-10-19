using System;
using NUnit.Framework;

namespace Frontier.Economy.Tests
{
    public class AchievementRewardsTests
    {
        [TestCase("first_harvest", 5)]
        [TestCase("reach_tier_9", 10)]
        [TestCase("era_ascension", 25)]
        public void Milestones_UseEstablishedShardBudget(string id, int expected)
        {
            Assert.AreEqual(expected, AchievementRewards.Amount(id, new ShardRulesDef
                { achievementSmall = 5, achievementMedium = 10, achievementLarge = 25 }));
        }

        [TestCase("daily_login")]
        [TestCase("goal_lab")]
        [TestCase("reach_tier_10")]
        [TestCase("era_landfall")]
        public void UnbudgetedIds_CannotMintShards(string id)
        {
            Assert.IsFalse(AchievementRewards.TrySize(id, out _));
            Assert.Throws<ArgumentException>(() => AchievementRewards.Amount(id, new ShardRulesDef()));
        }
    }
}
