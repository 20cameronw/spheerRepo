using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Frontier.Universe.Tests
{
    [TestFixture]
    public class HashTests
    {
        [Test]
        public void Fnv1a64_MatchesPublishedVectors()
        {
            Assert.AreEqual(0xCBF29CE484222325UL, Hash.Fnv1a64(""));
            Assert.AreEqual(0xAF63DC4C8601EC8CUL, Hash.Fnv1a64("a"));
            Assert.AreEqual(0x85944171F73967E8UL, Hash.Fnv1a64("foobar"));
        }

        [Test]
        public void Fnv1a64_IsStableAndHandlesUnicode()
        {
            Assert.AreEqual(Hash.Fnv1a64("galaxy.exists"), Hash.Fnv1a64("galaxy.exists"));
            Assert.AreNotEqual(Hash.Fnv1a64("galaxy.exists"), Hash.Fnv1a64("galaxy.exist"));
            // UTF-8 of U+00E9 is C3 A9, of U+1F680 is F0 9F 9A 80
            Assert.AreEqual(Hash.Fnv1a64(new byte[] { 0xC3, 0xA9 }), Hash.Fnv1a64("\u00E9"));
            Assert.AreEqual(Hash.Fnv1a64(new byte[] { 0xF0, 0x9F, 0x9A, 0x80 }), Hash.Fnv1a64("\uD83D\uDE80"));
        }

        [Test]
        public void SplitMix64_MatchesReferenceSequence()
        {
            // Reference outputs of Vigna's splitmix64 for seed 0.
            var r = new Rng(0UL);
            Assert.AreEqual(0xE220A8397B1DCDAFUL, r.NextULong());
            Assert.AreEqual(0x6E789E6AA1B965F4UL, r.NextULong());
            Assert.AreEqual(0x06C45D188009454FUL, r.NextULong());
        }

        [Test]
        public void Mix_HasGoodAvalanche()
        {
            long totalFlips = 0;
            int trials = 0;
            for (ulong x = 1; x < 4000; x += 7)
            {
                for (int bit = 0; bit < 64; bit += 5)
                {
                    ulong a = Hash.Mix(x), b = Hash.Mix(x ^ (1UL << bit));
                    totalFlips += PopCount(a ^ b);
                    trials++;
                }
            }
            double avg = (double)totalFlips / trials;
            TestLog.Info("avalanche: average " + avg.ToString("0.00") + " of 64 output bits flip per input bit flip");
            Assert.Greater(avg, 31.0);
            Assert.Less(avg, 33.0);
        }

        static int PopCount(ulong v)
        {
            int c = 0;
            while (v != 0UL) { v &= v - 1UL; c++; }
            return c;
        }

        [Test]
        public void Unit_IsUniformAndInRange()
        {
            var buckets = new int[64];
            const int n = 200000;
            double sum = 0.0;
            for (int i = 0; i < n; i++)
            {
                double u = Hash.Unit(Hash.Stream(12345UL, (ulong)i, "uniform-test"));
                Assert.GreaterOrEqual(u, 0.0);
                Assert.Less(u, 1.0);
                buckets[(int)(u * 64.0)]++;
                sum += u;
            }
            double mean = sum / n;
            double expected = n / 64.0, chi = 0.0;
            for (int i = 0; i < 64; i++) chi += (buckets[i] - expected) * (buckets[i] - expected) / expected;
            TestLog.Info("uniformity: mean " + mean.ToString("0.0000") + ", chi-square(63 dof) = " + chi.ToString("0.0"));
            Assert.AreEqual(0.5, mean, 0.005);
            Assert.Less(chi, 110.0);
        }

        [Test]
        public void Streams_WithDifferentSaltsAreUncorrelated()
        {
            const int n = 100000;
            double sx = 0, sy = 0, sxx = 0, syy = 0, sxy = 0;
            for (int i = 0; i < n; i++)
            {
                double a = Hash.Unit(Hash.Stream(99UL, (ulong)i, "biome"));
                double b = Hash.Unit(Hash.Stream(99UL, (ulong)i, "size"));
                sx += a; sy += b; sxx += a * a; syy += b * b; sxy += a * b;
            }
            double cov = sxy / n - (sx / n) * (sy / n);
            double corr = cov / Math.Sqrt((sxx / n - (sx / n) * (sx / n)) * (syy / n - (sy / n) * (sy / n)));
            TestLog.Info("correlation between the 'biome' and 'size' streams over the same keys: " + corr.ToString("0.0000"));
            Assert.Less(Math.Abs(corr), 0.01);
        }

        [Test]
        public void Stream_ChangesWithSeedKeyAndSalt()
        {
            ulong baseline = Hash.Stream(1UL, 2UL, "x");
            Assert.AreNotEqual(baseline, Hash.Stream(2UL, 2UL, "x"));
            Assert.AreNotEqual(baseline, Hash.Stream(1UL, 3UL, "x"));
            Assert.AreNotEqual(baseline, Hash.Stream(1UL, 2UL, "y"));
            Assert.AreEqual(baseline, Hash.Stream(1UL, 2UL, "x"));
            Assert.AreEqual(baseline, Hash.Stream(1UL, 2UL, Hash.Fnv1a64("x")));
            Assert.AreEqual(baseline, Hash.Stream(Hash.SeedFor(1UL, Hash.Fnv1a64("x")), 2UL));
        }

        [Test]
        public void Combine_IsOrderDependent()
        {
            Assert.AreNotEqual(Hash.Combine(1UL, 2UL), Hash.Combine(2UL, 1UL));
            Assert.AreNotEqual(Hash.Combine(1UL, 2UL, 3UL), Hash.Combine(3UL, 2UL, 1UL));
        }

        [Test]
        public void Rng_IsDeterministicAndRangesAreRespected()
        {
            var a = new Rng(777UL);
            var b = new Rng(777UL);
            for (int i = 0; i < 1000; i++) Assert.AreEqual(a.NextULong(), b.NextULong());
            var r = new Rng(5UL);
            for (int i = 0; i < 10000; i++)
            {
                int k = r.NextInt(7);
                Assert.IsTrue(k >= 0 && k < 7);
                int j = r.NextInt(-3, 4);
                Assert.IsTrue(j >= -3 && j < 4);
                double d = r.Range(2.0, 5.0);
                Assert.IsTrue(d >= 2.0 && d < 5.0);
                float f = r.NextFloat();
                Assert.IsTrue(f >= 0f && f < 1f);
            }
        }

        [Test]
        public void Index_CoversAllBucketsEvenly()
        {
            var counts = new int[10];
            for (int i = 0; i < 100000; i++) counts[Hash.Index(Hash.Stream(3UL, (ulong)i, "index"), 10)]++;
            for (int i = 0; i < 10; i++) Assert.AreEqual(10000, counts[i], 450, "bucket " + i);
        }
    }
}
