using System;
using NUnit.Framework;

namespace Frontier.Universe.Tests
{
    [TestFixture]
    public class DetMathTests
    {
        [Test]
        public void Exp_MatchesMathExp()
        {
            double worst = 0.0;
            for (double x = -40.0; x <= 40.0; x += 0.0137)
            {
                double a = DetMath.Exp(x), b = Math.Exp(x);
                worst = Math.Max(worst, Math.Abs(a - b) / b);
            }
            TestLog.Info("Exp: worst relative error vs Math.Exp = " + worst.ToString("0.0e+0"));
            Assert.Less(worst, 1e-13);
            Assert.AreEqual(1.0, DetMath.Exp(0.0));
        }

        [Test]
        public void Log_MatchesMathLog()
        {
            double worst = 0.0;
            for (double x = 1e-6; x < 1e7; x *= 1.0137)
            {
                double a = DetMath.Log(x), b = Math.Log(x);
                worst = Math.Max(worst, Math.Abs(a - b) / Math.Max(1.0, Math.Abs(b)));
            }
            TestLog.Info("Log: worst error vs Math.Log = " + worst.ToString("0.0e+0"));
            Assert.Less(worst, 1e-13);
            Assert.AreEqual(0.0, DetMath.Log(1.0));
            Assert.IsTrue(double.IsNegativeInfinity(DetMath.Log(0.0)));
            Assert.IsTrue(double.IsNaN(DetMath.Log(-1.0)));
            Assert.AreEqual(Math.Log(5e-324), DetMath.Log(5e-324), 1e-9);
        }

        [Test]
        public void Log2_AndPow_AreAccurate()
        {
            Assert.AreEqual(10.0, DetMath.Log2(1024.0), 1e-13);
            Assert.AreEqual(Math.Pow(7.3, 2.6), DetMath.Pow(7.3, 2.6), 1e-11);
            Assert.AreEqual(1.0, DetMath.Pow(0.3, 0.0));
        }

        [Test]
        public void SinCos_MatchMath()
        {
            double worst = 0.0;
            for (double x = -300.0; x <= 300.0; x += 0.0113)
            {
                double s, c;
                DetMath.SinCos(x, out s, out c);
                worst = Math.Max(worst, Math.Max(Math.Abs(s - Math.Sin(x)), Math.Abs(c - Math.Cos(x))));
                Assert.AreEqual(1.0, s * s + c * c, 1e-14);
            }
            TestLog.Info("SinCos: worst absolute error vs Math.Sin/Cos on [-300,300] = " + worst.ToString("0.0e+0"));
            Assert.Less(worst, 5e-13);
        }

        [Test]
        public void Atan2_MatchesMathAtan2()
        {
            double worst = 0.0;
            for (double a = -DetMath.Pi; a < DetMath.Pi; a += 0.00731)
            {
                double y = Math.Sin(a) * 3.7, x = Math.Cos(a) * 3.7;
                worst = Math.Max(worst, Math.Abs(DetMath.Atan2(y, x) - Math.Atan2(y, x)));
            }
            TestLog.Info("Atan2: worst absolute error vs Math.Atan2 = " + worst.ToString("0.0e+0"));
            Assert.Less(worst, 5e-14);
            Assert.AreEqual(0.0, DetMath.Atan2(0.0, 0.0));
            Assert.AreEqual(DetMath.HalfPi, DetMath.Atan2(1.0, 0.0), 1e-15);
            Assert.AreEqual(DetMath.Pi, DetMath.Atan2(0.0, -1.0), 1e-15);
            Assert.AreEqual(Math.Atan(0.5), DetMath.Atan(0.5), 1e-15);
        }

        [Test]
        public void Noise_IsDeterministicBoundedAndContinuous()
        {
            double sum = 0.0;
            int n = 0;
            double maxStep = 0.0;
            for (double x = -50.0; x < 50.0; x += 0.37)
            {
                for (double y = -50.0; y < 50.0; y += 0.41)
                {
                    double v = Noise.Fbm(42UL, x, y, 3);
                    Assert.GreaterOrEqual(v, 0.0);
                    Assert.Less(v, 1.0);
                    Assert.AreEqual(v, Noise.Fbm(42UL, x, y, 3));
                    maxStep = Math.Max(maxStep, Math.Abs(Noise.Fbm(42UL, x + 0.01, y, 3) - v));
                    sum += v;
                    n++;
                }
            }
            TestLog.Info("fbm mean " + (sum / n).ToString("0.000") + ", largest change over 0.01 units: " + maxStep.ToString("0.0000"));
            Assert.AreEqual(0.5, sum / n, 0.03);
            Assert.Less(maxStep, 0.05);
        }

        [Test]
        public void FloorDiv_RoundsTowardNegativeInfinity()
        {
            Assert.AreEqual(-1, DetMath.FloorDiv(-1, 3));
            Assert.AreEqual(-1, DetMath.FloorDiv(-3, 3));
            Assert.AreEqual(-2, DetMath.FloorDiv(-4, 3));
            Assert.AreEqual(0, DetMath.FloorDiv(2, 3));
            Assert.AreEqual(1, DetMath.FloorDiv(3, 3));
        }
    }
}
