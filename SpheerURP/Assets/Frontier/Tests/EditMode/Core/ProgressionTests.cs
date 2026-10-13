using System;
using NUnit.Framework;
using TestLog = Frontier.Universe.Tests.TestLog;

namespace Frontier.Tests
{
    /// <summary>Progression must give every player the same level and level progress as the original GameState XP code.</summary>
    [TestFixture]
    public class ProgressionTests
    {
        // The original code, line for line (Mathf.FloorToInt/Sqrt/Clamp01/RoundToInt are the System.Math calls below).
        static int OldLevel(int xp, float xpBase) { return 1 + (int)Math.Floor((float)Math.Sqrt(xp / Math.Max(1f, xpBase))); }

        static float OldProgress(int xp, float xpBase)
        {
            float b = Math.Max(1f, xpBase);
            int l = OldLevel(xp, xpBase) - 1;
            float lo = b * l * l, hi = b * (l + 1) * (l + 1);
            float t = (xp - lo) / (hi - lo);
            return t < 0f ? 0f : (t > 1f ? 1f : t);
        }

        static int OldXpForLevel(int level, float xpBase)
        {
            float b = Math.Max(1f, xpBase);
            return (int)Math.Round(b * Math.Max(0, level - 1) * Math.Max(0, level - 1));
        }

        [TestCase(40f)]
        [TestCase(1f)]
        [TestCase(0.2f)]
        [TestCase(12.5f)]
        [TestCase(250f)]
        public void LevelAndProgress_MatchTheOriginalCurve(float xpBase)
        {
            var p = new Progression(() => xpBase);
            int checks = 0;
            for (int xp = 0; xp <= 3000000; xp += xp < 5000 ? 1 : 997)
            {
                p.SetXp(xp);
                Assert.AreEqual(OldLevel(xp, xpBase), p.Level, "level at xp " + xp + " base " + xpBase);
                Assert.AreEqual(OldProgress(xp, xpBase), p.LevelProgress, "progress at xp " + xp + " base " + xpBase);
                checks++;
            }
            TestLog.Info("progression parity (base " + xpBase + "): " + checks + " xp values identical to the original curve");
        }

        [Test]
        public void SetLevel_JumpsToTheFirstXpOfThatLevel()
        {
            var p = new Progression(() => 40f);
            for (int level = 0; level <= 120; level++)
            {
                p.SetLevel(level);
                Assert.AreEqual(OldXpForLevel(level, 40f), p.Xp, "xp for level " + level);
                Assert.AreEqual(Math.Max(1, level), p.Level, "level after SetLevel(" + level + ")");
            }
        }

        [Test]
        public void AddXp_AccumulatesAndIgnoresNonPositiveAmounts()
        {
            var p = new Progression(() => 40f);
            p.AddXp(0);
            p.AddXp(-5);
            Assert.AreEqual(0, p.Xp);
            p.AddXp(212);
            Assert.AreEqual(212, p.Xp);
            Assert.AreEqual(3, p.Level, "212 xp is level 3 with a base of 40 (sqrt(5.3) = 2.3)");
            p.AddXp(int.MaxValue);
            Assert.AreEqual(int.MaxValue, p.Xp, "xp saturates instead of overflowing");
            p.SetXp(-10);
            Assert.AreEqual(0, p.Xp);
        }

        [Test]
        public void UpgradeLevels_AreClampedAndRemovedAtZero()
        {
            var p = new Progression(() => 40f);
            Assert.AreEqual(0, p.UpgradeLevel("pack"));
            p.SetUpgradeLevel("pack", 2, 6);
            Assert.AreEqual(2, p.UpgradeLevel("pack"));
            p.SetUpgradeLevel("pack", 99, 6);
            Assert.AreEqual(6, p.UpgradeLevel("pack"), "clamped to the maximum level");
            p.SetUpgradeLevel("pack", -3, 6);
            Assert.AreEqual(0, p.UpgradeLevel("pack"));
            int entries = 0;
            foreach (var kv in p.Upgrades) entries++;
            Assert.AreEqual(0, entries, "level 0 leaves no entry behind");
            p.SetUpgradeLevel("axe", 1, 6);
            p.Clear();
            Assert.AreEqual(0, p.UpgradeLevel("axe"));
            Assert.AreEqual(0, p.Xp);
        }

        [Test]
        public void BaseBelowOne_IsTreatedAsOne()
        {
            var p = new Progression(() => 0f);
            p.SetXp(4);
            Assert.AreEqual(3, p.Level, "a base of 0 behaves like 1: level = 1 + floor(sqrt(4))");
        }
    }
}
