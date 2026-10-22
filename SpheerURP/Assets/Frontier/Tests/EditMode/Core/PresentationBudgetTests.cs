using System;
using NUnit.Framework;

namespace Frontier.Tests
{
    public class PresentationBudgetTests
    {
        [TestCase(false, 0, 0, 0, PerformanceTier.High)]
        [TestCase(true, 0, 0, 0, PerformanceTier.Low)]
        [TestCase(true, 3072, 2048, 8, PerformanceTier.Low)]
        [TestCase(true, 8192, 4096, 4, PerformanceTier.Low)]
        [TestCase(true, 8192, 512, 8, PerformanceTier.Low)]
        [TestCase(true, 4096, 0, 8, PerformanceTier.Medium)]
        [TestCase(true, 6144, 2048, 8, PerformanceTier.Medium)]
        [TestCase(true, 8192, 0, 8, PerformanceTier.Medium)]
        [TestCase(true, 8192, 2048, 6, PerformanceTier.Medium)]
        [TestCase(true, 8192, 2048, 8, PerformanceTier.High)]
        public void Automatic_IsConservativeOnMobile(bool mobile, int memory, int graphicsMemory, int processors, PerformanceTier expected)
        {
            Assert.AreEqual(expected, PresentationBudget.Automatic(mobile, memory, graphicsMemory, processors));
        }

        [TestCase(PerformanceTier.Low, 768, 80, 30, false, false)]
        [TestCase(PerformanceTier.Medium, 1024, 120, 60, true, false)]
        [TestCase(PerformanceTier.High, 1280, 180, 60, true, true)]
        public void Presets_HaveExplicitPresentationLimits(PerformanceTier tier, int edge, int stars, int fps, bool post, bool aberration)
        {
            var budget = PresentationBudget.For(tier);
            Assert.AreEqual(edge, budget.RenderDimension);
            Assert.AreEqual(stars, budget.TravelStars);
            Assert.AreEqual(fps, budget.DefaultFrameRate);
            Assert.AreEqual(post, budget.PostProcessing);
            Assert.AreEqual(aberration, budget.ChromaticAberration);
            Assert.That(budget.ParticleMultiplier, Is.GreaterThan(0).And.LessThanOrEqualTo(1));
            Assert.AreEqual(tier == PerformanceTier.Low ? 1024 : tier == PerformanceTier.Medium ? 2048 : 4096,
                budget.UiAtlasMaxSize);
        }

        [TestCase(PerformanceTier.Low)]
        [TestCase(PerformanceTier.Medium)]
        [TestCase(PerformanceTier.High)]
        public void Surfaces_BoundBothDimensions_KeepAspect_AndNeverUpscale(PerformanceTier tier)
        {
            var budget = PresentationBudget.For(tier);
            foreach (var source in new[] { new[] { 1080, 2340 }, new[] { 3840, 2160 }, new[] { 360, 640 }, new[] { 8, 12 } })
            foreach (int hardwareLimit in new[] { 512, 16384 })
            {
                budget.FitSurface(source[0], source[1], hardwareLimit, out int w, out int h);
                int cap = Math.Min(budget.RenderDimension, hardwareLimit);
                Assert.That(w, Is.InRange(1, Math.Min(cap, source[0])));
                Assert.That(h, Is.InRange(1, Math.Min(cap, source[1])));
                double k = Math.Min(1, cap / (double)Math.Max(source[0], source[1]));
                Assert.That(Math.Abs(w - source[0] * k), Is.LessThanOrEqualTo(0.5));
                Assert.That(Math.Abs(h - source[1] * k), Is.LessThanOrEqualTo(0.5));
            }
        }

        [Test]
        public void InvalidSizesAndTiers_AreRejected()
        {
            var budget = PresentationBudget.For(PerformanceTier.Low);
            foreach (float invalid in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => budget.FitSurface(invalid, 500, 1024, out _, out _));
                Assert.Throws<ArgumentOutOfRangeException>(() => budget.FitSurface(500, invalid, 1024, out _, out _));
            }
            Assert.Throws<ArgumentOutOfRangeException>(() => budget.FitSurface(500, 500, 0, out _, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => PresentationBudget.For((PerformanceTier)99));
        }
    }
}
