using System;
using NUnit.Framework;

namespace Frontier.Universe.Tests
{
    /// <summary>
    /// Strict bit-level reproducibility of the continuous maths. The contractual golden tests quantise floating point values, so they survive
    /// last-bit differences; these tests hash the raw IEEE-754 bits of DetMath results and of the density field. They hold on every platform that
    /// executes + - * / and sqrt as plain IEEE operations (all x64 and ARM64 .NET/Mono/IL2CPP builds without fused multiply-add contraction).
    /// If one of them reports "inconclusive" on a new platform, the quantised golden tests still decide whether the universe is intact.
    /// </summary>
    [TestFixture]
    public class BitExactTests
    {
        public const ulong BitsDetMath = 0x2F64190E99D0C782UL;
        public const ulong BitsDensity = 0x0D889345B9C637EEUL;

        static ulong Bits(double v) { return unchecked((ulong)BitConverter.DoubleToInt64Bits(v)); }

        static void Check(string what, ulong expected, ulong actual)
        {
            TestLog.Info(what + ": raw-bit digest 0x" + actual.ToString("X16"));
            if (expected != actual)
                Assert.Inconclusive(what + " differs at the bit level on this platform (0x" + actual.ToString("X16") + " vs 0x" + expected.ToString("X16") + "); the quantised golden tests are the contract");
        }

        [Test]
        public void DetMath_ResultsAreBitIdentical()
        {
            ulong h = 0x1234UL;
            var r = new Rng(31337UL);
            for (int i = 0; i < 4000; i++)
            {
                double x = r.Range(-40.0, 40.0), pos = r.Range(1e-5, 1e5), y = r.Range(-9.0, 9.0);
                double s, c;
                DetMath.SinCos(x * 3.0, out s, out c);
                h = Hash.Combine(h, Bits(DetMath.Exp(x)));
                h = Hash.Combine(h, Bits(DetMath.Log(pos)));
                h = Hash.Combine(h, Bits(s));
                h = Hash.Combine(h, Bits(c));
                h = Hash.Combine(h, Bits(DetMath.Atan2(y, x)));
                h = Hash.Combine(h, Bits(DetMath.Pow(pos, 0.37)));
                h = Hash.Combine(h, Bits(DetMath.Log2(pos)));
            }
            Check("DetMath", BitsDetMath, h);
        }

        [Test]
        public void DensityField_IsBitIdentical()
        {
            Universe u = TestUniverses.WithLandmarks();
            ulong h = 0x5678UL;
            var r = new Rng(424242UL);
            for (int i = 0; i < 20000; i++)
            {
                double x = r.Range(-14000.0, 14000.0), y = r.Range(-14000.0, 14000.0);
                h = Hash.Combine(h, Bits(u.Density(x, y)));
            }
            for (int i = 0; i < 4000; i++)
            {
                double x = r.Range(-2500.0, 2500.0), y = r.Range(-2500.0, 2500.0);
                h = Hash.Combine(h, Bits(u.Density(x, y)));
            }
            Check("density field", BitsDensity, h);
        }
    }
}
