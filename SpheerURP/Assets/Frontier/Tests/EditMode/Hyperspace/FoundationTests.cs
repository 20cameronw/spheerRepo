using NUnit.Framework;

namespace Frontier.Hyperspace.Tests
{
    [TestFixture]
    public class FoundationTests
    {
        [Test]
        public void SinCosMatchReference()
        {
            double worst = 0;
            for (int i = -4000; i <= 4000; i++)
            {
                float x = i * 0.0173f;
                double e1 = System.Math.Abs(HMath.Sin(x) - System.Math.Sin(x));
                double e2 = System.Math.Abs(HMath.Cos(x) - System.Math.Cos(x));
                if (e1 > worst) worst = e1;
                if (e2 > worst) worst = e2;
            }
            Assert.Less(worst, 2e-5, "sin/cos error");
        }

        [Test]
        public void Atan2MatchesReference()
        {
            double worst = 0;
            for (int iy = -20; iy <= 20; iy++)
            {
                for (int ix = -20; ix <= 20; ix++)
                {
                    if (ix == 0 && iy == 0) continue;
                    double e = System.Math.Abs(HMath.Atan2(iy, ix) - System.Math.Atan2(iy, ix));
                    if (e > worst) worst = e;
                }
            }
            Assert.Less(worst, 5e-5, "atan2 error");
        }

        [Test]
        public void TuningKeysRoundTrip()
        {
            var t = new HyperspaceTuning();
            string[] keys = HyperspaceTuning.AllKeys();
            Assert.Greater(keys.Length, 100);
            foreach (string k in keys)
            {
                float v;
                Assert.IsTrue(t.TryGet(k, out v), k);
                Assert.IsTrue(t.TrySet(k, v), k);
            }
            Assert.IsTrue(t.TrySet("enemyHp.Drone", 20f));
            Assert.AreEqual(20f, t.enemyHp[(int)EntityType.Drone]);
            Assert.IsFalse(t.TrySet("noSuchKey", 1f));
        }

        [Test]
        public void StarterPowerIsOne()
        {
            var t = new HyperspaceTuning();
            Assert.AreEqual(1f, t.ShipPower(ShipStats.Starter()), 1e-4f);
            Assert.Greater(t.ShipPower(ShipStats.Endgame()), 6f);
        }

        [Test]
        public void EventRingDropsOldestAndKeepsDigest()
        {
            var r = new EventRing();
            r.Reset();
            for (int i = 0; i < EventRing.Capacity + 10; i++) r.Push(EventType.EnemyHit, i, i, 0, 0, 0f, 0f, 0f, 0f);
            Assert.AreEqual(EventRing.Capacity, r.Count);
            Assert.AreEqual(10, r.Dropped);
            HEvent e;
            Assert.IsTrue(r.TryDequeue(out e));
            Assert.AreEqual(10, e.step);
        }
    }
}
