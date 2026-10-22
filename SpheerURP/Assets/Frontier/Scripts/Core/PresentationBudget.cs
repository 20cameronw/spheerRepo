using System;

namespace Frontier
{
    public enum PerformanceTier { Low, Medium, High }
    public enum QualityPreset { Automatic = 0, Low = 1, Medium = 2, High = 3 }

    /// <summary>Presentation-only limits. Simulation, hit targets and combat telegraphs never depend on these values.</summary>
    public readonly struct PresentationBudget
    {
        public readonly PerformanceTier Tier;
        public readonly int RenderDimension, TravelStars, DefaultFrameRate;
        public readonly float ParticleMultiplier;
        public bool PostProcessing { get { return Tier != PerformanceTier.Low; } }
        public bool ChromaticAberration { get { return Tier == PerformanceTier.High; } }
        public int UiAtlasMaxSize { get { return Tier == PerformanceTier.Low ? 1024 : Tier == PerformanceTier.Medium ? 2048 : 4096; } }

        PresentationBudget(PerformanceTier tier, int renderDimension, int travelStars, int fps, float particles)
        {
            Tier = tier;
            RenderDimension = renderDimension;
            TravelStars = travelStars;
            DefaultFrameRate = fps;
            ParticleMultiplier = particles;
        }

        public static PresentationBudget For(PerformanceTier tier)
        {
            switch (tier)
            {
                case PerformanceTier.Low: return new PresentationBudget(tier, 768, 80, 30, 0.45f);
                case PerformanceTier.Medium: return new PresentationBudget(tier, 1024, 120, 60, 0.7f);
                case PerformanceTier.High: return new PresentationBudget(tier, 1280, 180, 60, 1f);
                default: throw new ArgumentOutOfRangeException(nameof(tier));
            }
        }

        public static PerformanceTier Automatic(bool mobile, int memoryMb, int graphicsMemoryMb, int processors)
        {
            if (!mobile) return PerformanceTier.High;
            if (memoryMb <= 3072 || processors <= 4 || (graphicsMemoryMb > 0 && graphicsMemoryMb <= 512))
                return PerformanceTier.Low;
            return memoryMb >= 8192 && graphicsMemoryMb >= 2048 && processors >= 8
                ? PerformanceTier.High : PerformanceTier.Medium;
        }

        public void FitSurface(float width, float height, int hardwareLimit, out int targetWidth, out int targetHeight)
        {
            if (float.IsNaN(width) || float.IsInfinity(width) || width <= 0f)
                throw new ArgumentOutOfRangeException(nameof(width));
            if (float.IsNaN(height) || float.IsInfinity(height) || height <= 0f)
                throw new ArgumentOutOfRangeException(nameof(height));
            if (hardwareLimit < 1) throw new ArgumentOutOfRangeException(nameof(hardwareLimit));
            int cap = Math.Min(RenderDimension, hardwareLimit);
            double scale = Math.Min(1.0, cap / (double)Math.Max(width, height));
            targetWidth = Math.Max(1, (int)Math.Round(width * scale));
            targetHeight = Math.Max(1, (int)Math.Round(height * scale));
        }
    }
}
