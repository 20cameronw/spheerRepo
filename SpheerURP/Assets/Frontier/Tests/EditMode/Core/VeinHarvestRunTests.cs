using System;
using NUnit.Framework;

namespace Frontier.Tests
{
    public class VeinHarvestRunTests
    {
        [Test]
        public void SeededLayoutsAreStableAndHaveAReachableRichRoute()
        {
            for (int seed = 1; seed <= 30; seed++)
            {
                var run = new VeinHarvestRun(seed, 0.8f, 1);
                var same = new VeinHarvestRun(seed, 0.8f, 1);
                bool[] reachable = new bool[VeinHarvestRun.Columns];
                reachable[2] = true;
                for (int row = 0; row < VeinHarvestRun.Rows; row++)
                {
                    var next = new bool[VeinHarvestRun.Columns];
                    for (int column = 0; column < VeinHarvestRun.Columns; column++)
                    {
                        Assert.AreEqual(run.Cell(row, column), same.Cell(row, column));
                        next[column] = run.Cell(row, column) == 3 &&
                            (reachable[column] || column > 0 && reachable[column - 1] ||
                             column < VeinHarvestRun.Columns - 1 && reachable[column + 1]);
                    }
                    CollectionAssert.Contains(next, true, "the rich route must remain connected");
                    reachable = next;
                }
            }
        }

        [Test]
        public void ReadingTheSeamsOutperformsIdleAndCompletesOnlyOnce()
        {
            var skilled = new VeinHarvestRun(42, 0.6f, 1);
            var idle = new VeinHarvestRun(42, 0.6f, 1);
            while (!skilled.Completed)
            {
                int nearest = -1;
                for (int column = 0; column < VeinHarvestRun.Columns; column++)
                    if (skilled.Cell(skilled.Depth, column) == 3 &&
                        (nearest < 0 || Math.Abs(column - skilled.Column) < Math.Abs(nearest - skilled.Column)))
                        nearest = column;
                while (skilled.Column != nearest) skilled.Steer(nearest - skilled.Column);
                int depth = skilled.Depth;
                while (skilled.Depth == depth) skilled.Advance(0.01f);
            }
            idle.Advance(100);
            Assert.AreEqual(1f, skilled.Quality);
            Assert.AreEqual(VeinHarvestRun.Rows, skilled.RichSeams);
            Assert.Less(idle.Quality, skilled.Quality);
            Assert.AreEqual(4, skilled.Integrity);
            int ore = skilled.Ore, columnBefore = skilled.Column;
            skilled.Advance(100);
            skilled.Steer(1);
            Assert.AreEqual(ore, skilled.Ore);
            Assert.AreEqual(columnBefore, skilled.Column);
        }

        [Test]
        public void StrongerToolsGiveMoreReactionTimeAndInputsAreValidated()
        {
            var basic = new VeinHarvestRun(5, 1, 1);
            var upgraded = new VeinHarvestRun(5, 1, 20);
            basic.Advance(1.3f); upgraded.Advance(1.3f);
            Assert.AreEqual(1, basic.Depth);
            Assert.AreEqual(0, upgraded.Depth);
            Assert.Throws<ArgumentOutOfRangeException>(() => new VeinHarvestRun(1, float.NaN, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VeinHarvestRun(1, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => basic.Advance(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => basic.Cell(-1, 0));
        }
    }
}
